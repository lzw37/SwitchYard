using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Serilog;
using Serilog.Context;
using SwitchYard.Capacity;

namespace SwitchYard.CapacityAgent;

internal sealed class CapacityAgentClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly ILogger Logger = Log.ForContext<CapacityAgentClient>();
    private readonly Uri _serverUri;
    private readonly string _username;
    private readonly string _passwordHash;
    private readonly string _agentId;
    private readonly string _agentName;
    private readonly CapacityModelRegistry _models;
    private readonly CapacityAgentOptions _options;
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _jobSlots;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private readonly ConcurrentDictionary<string, ActiveJob> _activeJobs = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _resourceSampleLock = new();
    private string _accessToken = string.Empty;
    private DateTimeOffset _accessTokenExpiresAt = DateTimeOffset.MinValue;
    private string _authenticatedUsername = string.Empty;
    private bool _isAdministrator;
    private bool _accessPolicyLogged;
    private DateTimeOffset _lastResourceSampleAt = DateTimeOffset.UtcNow;
    private TimeSpan _lastResourceCpuTime;
    private double _lastCpuUsagePercent;

    public CapacityAgentClient(
        Uri serverUri,
        string username,
        string password,
        string agentId,
        string agentName,
        CapacityModelRegistry models,
        CapacityAgentOptions options)
    {
        _serverUri = serverUri;
        _username = username;
        _passwordHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password))).ToLowerInvariant();
        _agentId = agentId;
        _agentName = agentName;
        _models = models;
        _options = options;
        _httpClient = new HttpClient { BaseAddress = serverUri, Timeout = TimeSpan.FromSeconds(30) };
        _jobSlots = new SemaphoreSlim(options.MaxConcurrentJobs, options.MaxConcurrentJobs);
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await RunConnectionAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                Logger.Error(exception, "连接失败；将在 5 秒后重试");
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
        }
    }

    private async Task RunConnectionAsync(CancellationToken cancellationToken)
    {
        await EnsureTokenAsync(cancellationToken);
        var hubUri = new Uri(_serverUri, "hubs/capacity-agent");
        await using var connection = new HubConnectionBuilder()
            .WithUrl(hubUri, options =>
            {
                options.AccessTokenProvider = () => GetAccessTokenAsync(cancellationToken);
            })
            .WithAutomaticReconnect(new[]
            {
                TimeSpan.Zero,
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(30)
            })
            .Build();

        connection.On<CapacitySolveCommand>("Solve", command =>
        {
            StartJob(connection, command, cancellationToken);
            return Task.CompletedTask;
        });
        connection.Reconnecting += exception =>
        {
            if (exception is null)
            {
                Logger.Warning("长连接中断，正在重连");
            }
            else
            {
                Logger.Warning(exception, "长连接中断，正在重连");
            }
            return Task.CompletedTask;
        };
        connection.Reconnected += async _ =>
        {
            await RegisterAsync(connection, cancellationToken);
            Logger.Information("长连接已恢复并重新注册");
        };

        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.Closed += exception =>
        {
            if (exception != null)
            {
                Logger.Warning(exception, "长连接已关闭");
            }
            closed.TrySetResult();
            return Task.CompletedTask;
        };

        Logger.Information("正在连接 {ServerUri}", _serverUri);
        await connection.StartAsync(cancellationToken);
        await RegisterAsync(connection, cancellationToken);
        Logger.Information("CapacityAgent 已注册：{AgentName} ({AgentId})", _agentName, _agentId);
        Logger.Information("等待求解指令，按 Ctrl+C 退出");

        using var statusCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var statusTask = ReportStatusLoopAsync(connection, statusCancellation.Token);
        try
        {
            await closed.Task.WaitAsync(cancellationToken);
        }
        finally
        {
            statusCancellation.Cancel();
            try
            {
                await statusTask;
            }
            catch (OperationCanceledException) when (statusCancellation.IsCancellationRequested)
            {
                // Normal connection shutdown.
            }
        }
    }

    private void StartJob(
        HubConnection connection,
        CapacitySolveCommand command,
        CancellationToken cancellationToken)
    {
        if (!_jobSlots.Wait(0))
        {
            Logger.Warning(
                "拒绝任务 {JobId}：并发槽位已满（{ActiveJobCount}/{MaxConcurrentJobs}）",
                command.JobId,
                _activeJobs.Count,
                _options.MaxConcurrentJobs);
            _ = SafeCompleteAsync(connection, new CapacitySolveJobCompletion
            {
                JobId = command.JobId,
                Success = false,
                Error = $"CapacityAgent 并发任务已达上限 {_options.MaxConcurrentJobs}。"
            }, cancellationToken);
            return;
        }

        var resources = NormalizeTaskResources(command.Resources);
        if (!_activeJobs.TryAdd(command.JobId, new ActiveJob(command.JobId, resources)))
        {
            _jobSlots.Release();
            Logger.Warning("拒绝重复任务 {JobId}", command.JobId);
            return;
        }

        _ = ProcessJobAsync(connection, command, resources, cancellationToken);
    }

    private async Task ProcessJobAsync(
        HubConnection connection,
        CapacitySolveCommand command,
        CapacityTaskResourceLimits resources,
        CancellationToken cancellationToken)
    {
        using var jobContext = LogContext.PushProperty("JobId", command.JobId);
        try
        {
            Logger.Information(
                "开始求解任务，模型 {ModelId}，分配 CPU {CpuCoreCount} 核、内存 {MemoryLimitMb} MB，当前并发 {ActiveJobCount}/{MaxConcurrentJobs}",
                command.ModelId,
                resources.CpuCoreCount,
                resources.MemoryLimitBytes / (1024L * 1024L),
                _activeJobs.Count,
                _options.MaxConcurrentJobs);
            await TryReportStatusAsync(connection, cancellationToken);
            if (!_models.TryGet(command.ModelId, out var model))
            {
                throw new InvalidOperationException($"不支持模型 {command.ModelId}。");
            }

            async Task ReportProgress(int percent, string message)
            {
                Logger.Information("任务进度 {Percent}%：{ProgressMessage}", percent, message);
                try
                {
                    await connection.InvokeAsync("ReportProgress", new CapacitySolveJobProgress
                    {
                        JobId = command.JobId,
                        Percent = percent,
                        Message = message
                    }, cancellationToken);
                }
                catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
                {
                    Logger.Warning(exception, "进度回传暂时失败，将继续求解");
                }
            }

            var result = await model.SolveAsync(
                command.Input,
                new CapacityModelExecutionContext(command.JobId, resources),
                ReportProgress,
                cancellationToken);
            await SafeCompleteAsync(connection, new CapacitySolveJobCompletion
            {
                JobId = command.JobId,
                Success = true,
                Result = result
            }, cancellationToken);
            Logger.Information("求解任务已完成并回传结果");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Logger.Warning("求解任务因 CapacityAgent 停止而取消");
            await SafeCompleteAsync(connection, new CapacitySolveJobCompletion
            {
                JobId = command.JobId,
                Success = false,
                Error = "CapacityAgent 已停止。"
            }, CancellationToken.None);
        }
        catch (Exception exception)
        {
            Logger.Error(exception, "求解任务失败");
            await SafeCompleteAsync(connection, new CapacitySolveJobCompletion
            {
                JobId = command.JobId,
                Success = false,
                Error = exception.Message
            }, cancellationToken);
        }
        finally
        {
            _activeJobs.TryRemove(command.JobId, out _);
            _jobSlots.Release();
            await TryReportStatusAsync(connection, CancellationToken.None);
        }
    }

    private async Task RegisterAsync(HubConnection connection, CancellationToken cancellationToken)
    {
        await connection.InvokeAsync(
            "RegisterAgent",
            new CapacityAgentRegistration
            {
                AgentId = _agentId,
                Name = _agentName,
                Models = _models.Descriptors.ToList(),
                ActiveJobIds = _activeJobs.Keys.OrderBy(id => id, StringComparer.Ordinal).ToList(),
                Resources = CreateResourceStatus(),
                AccessPolicy = CreateAccessPolicy()
            },
            cancellationToken);
    }

    private CapacityAgentAccessPolicy CreateAccessPolicy()
    {
        if (!_isAdministrator)
        {
            return new CapacityAgentAccessPolicy();
        }

        return new CapacityAgentAccessPolicy
        {
            AllowAllUsers = _options.AllowAllUsers,
            AllowedUsers = _options.AllowedUsers.ToList()
        };
    }

    private async Task ReportStatusLoopAsync(
        HubConnection connection,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await TryReportStatusAsync(connection, cancellationToken);
            await Task.Delay(TimeSpan.FromSeconds(_options.StatusReportIntervalSeconds), cancellationToken);
        }
    }

    private async Task TryReportStatusAsync(
        HubConnection connection,
        CancellationToken cancellationToken)
    {
        try
        {
            if (connection.State == HubConnectionState.Connected)
            {
                await connection.InvokeAsync("ReportStatus", CreateResourceStatus(), cancellationToken);
            }
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            Logger.Debug(exception, "上报 CapacityAgent 资源状态失败");
        }
    }

    private CapacityAgentResourceStatus CreateResourceStatus()
    {
        var activeJobs = _activeJobs.Values.ToList();
        var logicalCpuCores = Math.Max(1, Environment.ProcessorCount);
        var processUsage = CapacityWorkerProcessRegistry.GetUsage();
        using var currentProcess = Process.GetCurrentProcess();
        var totalCpuTime = currentProcess.TotalProcessorTime + processUsage.CpuTime;
        var now = DateTimeOffset.UtcNow;
        double cpuUsagePercent;
        lock (_resourceSampleLock)
        {
            var elapsed = now - _lastResourceSampleAt;
            var cpuDelta = totalCpuTime - _lastResourceCpuTime;
            if (elapsed.TotalMilliseconds >= 100 && cpuDelta >= TimeSpan.Zero)
            {
                _lastCpuUsagePercent = Math.Clamp(
                    cpuDelta.TotalMilliseconds / elapsed.TotalMilliseconds / logicalCpuCores * 100,
                    0,
                    100);
            }

            _lastResourceSampleAt = now;
            _lastResourceCpuTime = totalCpuTime;
            cpuUsagePercent = _lastCpuUsagePercent;
        }

        var gcInfo = GC.GetGCMemoryInfo();
        var totalMemoryBytes = Math.Max(0, gcInfo.TotalAvailableMemoryBytes);
        var memoryLoadBytes = Math.Max(0, gcInfo.MemoryLoadBytes);
        var allocatedCpuCores = activeJobs.Sum(job => job.Resources.CpuCoreCount);
        var allocatedMemoryBytes = activeJobs.Sum(job => job.Resources.MemoryLimitBytes);
        var processWorkingSetBytes = Math.Max(0, currentProcess.WorkingSet64) + processUsage.WorkingSetBytes;
        var agentMemoryCapacity = allocatedMemoryBytes > 0
            ? allocatedMemoryBytes
            : _options.MemoryLimitBytesPerJob;
        var memoryUsagePercent = agentMemoryCapacity > 0
            ? Math.Clamp(processWorkingSetBytes * 100d / agentMemoryCapacity, 0, 100)
            : 0;

        return new CapacityAgentResourceStatus
        {
            MaxConcurrentJobs = _options.MaxConcurrentJobs,
            ActiveJobCount = activeJobs.Count,
            AvailableJobSlots = Math.Max(0, _options.MaxConcurrentJobs - activeJobs.Count),
            LogicalCpuCores = logicalCpuCores,
            CpuCoresPerJob = _options.CpuCoresPerJob,
            AllocatedCpuCores = allocatedCpuCores,
            CpuUsagePercent = cpuUsagePercent,
            MemoryLimitBytesPerJob = _options.MemoryLimitBytesPerJob,
            AllocatedMemoryBytes = allocatedMemoryBytes,
            TotalMemoryBytes = totalMemoryBytes,
            MemoryLoadBytes = memoryLoadBytes,
            ProcessWorkingSetBytes = processWorkingSetBytes,
            MemoryUsagePercent = memoryUsagePercent,
            CollectedAt = now
        };
    }

    private CapacityTaskResourceLimits NormalizeTaskResources(CapacityTaskResourceLimits? requested)
    {
        var cpuCoreCount = requested?.CpuCoreCount > 0
            ? Math.Min(requested.CpuCoreCount, _options.CpuCoresPerJob)
            : _options.CpuCoresPerJob;
        var memoryLimitBytes = requested?.MemoryLimitBytes > 0
            ? Math.Min(requested.MemoryLimitBytes, _options.MemoryLimitBytesPerJob)
            : _options.MemoryLimitBytesPerJob;
        return new CapacityTaskResourceLimits
        {
            CpuCoreCount = Math.Max(1, cpuCoreCount),
            MemoryLimitBytes = Math.Max(256L * 1024L * 1024L, memoryLimitBytes)
        };
    }

    private static async Task SafeCompleteAsync(
        HubConnection connection,
        CapacitySolveJobCompletion completion,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            try
            {
                await connection.InvokeAsync("CompleteJob", completion, cancellationToken);
                return;
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested && attempt < 5)
            {
                Logger.Warning(exception, "回传任务结果失败，正在重试（{Attempt}/5）", attempt);
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
            catch (Exception exception)
            {
                Logger.Error(exception, "回传任务结果失败，已停止重试");
                return;
            }
        }
    }

    private async Task<string> EnsureTokenAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_accessToken) &&
            _accessTokenExpiresAt > DateTimeOffset.UtcNow.AddMinutes(2))
        {
            return _accessToken;
        }

        await _tokenLock.WaitAsync(cancellationToken);
        try
        {
            if (!string.IsNullOrWhiteSpace(_accessToken) &&
                _accessTokenExpiresAt > DateTimeOffset.UtcNow.AddMinutes(2))
            {
                return _accessToken;
            }

            using var response = await _httpClient.PostAsJsonAsync(
                "api/capacity-agents/authenticate",
                new CapacityAgentAuthenticationRequest
                {
                    Username = _username,
                    Password = _passwordHash
                },
                JsonOptions,
                cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"身份验证失败 ({(int)response.StatusCode})：{ReadErrorMessage(content)}");
            }

            var authentication = JsonSerializer.Deserialize<CapacityAgentAuthenticationResponse>(content, JsonOptions)
                ?? throw new InvalidOperationException("身份验证响应格式不正确。");
            _accessToken = authentication.Token;
            _accessTokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(60, authentication.ExpiresIn));
            _authenticatedUsername = authentication.Username;
            _isAdministrator = authentication.IsAdministrator;
            Logger.Information(
                "身份验证成功：{AuthenticatedUsername}（{Role}），访问令牌将在 {ExpiresAt} 到期",
                _authenticatedUsername,
                _isAdministrator ? "管理员" : "普通用户",
                _accessTokenExpiresAt);
            if (!_accessPolicyLogged)
            {
                LogEffectiveAccessPolicy();
                _accessPolicyLogged = true;
            }
            return _accessToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        return await EnsureTokenAsync(cancellationToken);
    }

    private static string ReadErrorMessage(string content)
    {
        try
        {
            using var document = JsonDocument.Parse(content);
            return document.RootElement.TryGetProperty("message", out var message)
                ? message.GetString() ?? content
                : content;
        }
        catch (JsonException)
        {
            return content;
        }
    }

    private void LogEffectiveAccessPolicy()
    {
        if (!_isAdministrator)
        {
            if (_options.AllowAllUsers || _options.AllowedUsers.Count > 0)
            {
                Logger.Warning("普通用户启动的 CapacityAgent 不能授权其他用户；配置的 allowAllUsers/allowedUsers 已忽略");
            }

            Logger.Information("CapacityAgent 访问策略：仅允许所有者 {OwnerUsername}", _authenticatedUsername);
            return;
        }

        if (_options.AllowAllUsers)
        {
            Logger.Information("CapacityAgent 访问策略：允许所有已登录用户使用");
        }
        else if (_options.AllowedUsers.Count > 0)
        {
            Logger.Information(
                "CapacityAgent 访问策略：允许所有者和白名单用户 {AllowedUsers}",
                _options.AllowedUsers);
        }
        else
        {
            Logger.Information("CapacityAgent 访问策略：未配置白名单，仅允许所有者 {OwnerUsername}", _authenticatedUsername);
        }
    }

    private sealed record ActiveJob(string JobId, CapacityTaskResourceLimits Resources);
}
