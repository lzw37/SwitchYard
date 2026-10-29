using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Serilog;
using Serilog.Context;
using SwitchYard.Capacity;
using SwitchYard.CapacitySolver;

namespace SwitchYard.CapacityAgent;

/// <summary>
/// 计算代理客户端，负责服务端认证、SignalR 连接、任务调度、资源上报及任务结果的确认回传。
/// </summary>
internal sealed class CapacityAgentClient
{
    /// <summary>
    /// 与服务端交换认证、任务及错误数据时使用的 Web 默认 JSON 序列化配置。
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    /// <summary>
    /// 带有代理客户端类型上下文的日志记录器，用于记录连接、认证、调度和结果回传状态。
    /// </summary>
    private static readonly Serilog.ILogger Logger = Log.ForContext<CapacityAgentClient>();
    /// <summary>
    /// 服务端基础地址，用于构造认证接口和 SignalR Hub 地址。
    /// </summary>
    private readonly Uri _serverUri;
    /// <summary>
    /// 启动时输入的登录用户名，用于向服务端提交身份验证请求。
    /// </summary>
    private readonly string _username;
    /// <summary>
    /// 启动时输入密码的 SHA-256 摘要，以小写十六进制保存并提交给认证接口。
    /// </summary>
    private readonly string _passwordHash;
    /// <summary>
    /// 当前代理的稳定标识，用于向服务端注册并关联代理身份。
    /// </summary>
    private readonly string _agentId;
    /// <summary>
    /// 当前代理向控制台和服务端展示的名称，已在启动阶段处理默认值与首尾空白。
    /// </summary>
    private readonly string _agentName;
    /// <summary>
    /// 实际能力求解器的注册表，供任务查找求解器及向服务端公布描述信息。
    /// </summary>
    private readonly CapacitySolverRegistry _capacitySolverRegistry;
    /// <summary>
    /// 已加载并校验的代理配置，提供并发上限、资源配额、状态上报间隔和共享策略。
    /// </summary>
    private readonly CapacityAgentOptions _options;
    /// <summary>
    /// 用于调用服务端认证接口的 HTTP 客户端，基础地址为服务端地址，请求超时为三十秒。
    /// </summary>
    private readonly HttpClient _httpClient;
    /// <summary>
    /// 串行执行任务去重和并发上限检查，运行中的任务集合就是唯一的槽位计数。
    /// </summary>
    private readonly object _jobLock = new();
    /// <summary>负责生成代理进程资源状态的采样器。</summary>
    private readonly CapacityAgentResources _resources = new();
    /// <summary>
    /// 串行执行身份验证的互斥信号量，避免多个调用同时刷新访问令牌。
    /// </summary>
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    /// <summary>
    /// 按不区分大小写的任务标识保存运行中任务，供重复任务检查、取消处理和资源统计使用。
    /// </summary>
    private readonly ConcurrentDictionary<string, ActiveJob> _activeJobs = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>
    /// 按不区分大小写的任务标识在内存中保留待确认结果，连接恢复后重发，收到服务端确认后移除。
    /// </summary>
    private readonly ConcurrentDictionary<string, CapacitySolveJobCompletion> _pendingCompletions = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>
    /// 保护结果回传流程的互斥信号量，避免状态上报、任务完成和重新注册同时发送队列中的结果。
    /// </summary>
    private readonly SemaphoreSlim _completionLock = new(1, 1);
    /// <summary>
    /// 最近一次成功注册的 SignalR 连接，用于任务进度和结果回传；初始状态、重连中或连接关闭时为空。
    /// </summary>
    private HubConnection? _registeredConnection;

    /// <summary>
    /// 最近一次认证成功后缓存的访问令牌，初始为空，由 SignalR 连接及重连时使用。
    /// </summary>
    private string _accessToken = string.Empty;
    /// <summary>
    /// 缓存令牌的预计 UTC 到期时刻，初始为最小值；剩余有效期不超过两分钟时重新认证。
    /// </summary>
    private DateTimeOffset _accessTokenExpiresAt = DateTimeOffset.MinValue;
    /// <summary>
    /// 服务端认证响应确认的用户名，初始为空，用于访问策略日志中的所有者说明。
    /// </summary>
    private string _authenticatedUsername = string.Empty;
    /// <summary>
    /// 最近一次认证响应确认的管理员标志，默认关闭，决定共享访问配置是否生效。
    /// </summary>
    private bool _isAdministrator;
    /// <summary>
    /// 是否已在首次认证成功后记录实际生效的访问策略，避免令牌更新时重复输出该策略。
    /// </summary>
    private bool _accessPolicyLogged;

    /// <summary>
    /// 初始化代理身份、服务地址、求解器注册表和运行配置，并创建认证用的 HTTP 客户端。
    /// </summary>
    public CapacityAgentClient(
        Uri serverUri,
        string username,
        string password,
        string agentId,
        string agentName,
        CapacitySolverRegistry solvers,
        CapacityAgentOptions options)
    {
        _serverUri = serverUri;
        _username = username;
        _passwordHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password))).ToLowerInvariant();
        _agentId = agentId;
        _agentName = agentName;
        _capacitySolverRegistry = solvers;
        _options = options;
        _httpClient = new HttpClient { BaseAddress = serverUri, Timeout = TimeSpan.FromSeconds(30) };
    }

    /// <summary>在一个循环中连接、注册、补发结果及上报状态，连接失败后等待五秒重试。</summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        await using var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(_serverUri, "hubs/capacity-agent"), options =>
                options.AccessTokenProvider = () => EnsureTokenAsync(cancellationToken))
            .Build();
        connection.On<CapacitySolveCommand>("Solve", command =>
        {
            StartJob(command, cancellationToken);
            return Task.CompletedTask;
        });
        connection.On<string>("CancelJob", jobId =>
        {
            if (_activeJobs.TryGetValue(jobId, out var job)) job.Cancel();
            return Task.CompletedTask;
        });

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (connection.State == HubConnectionState.Disconnected)
                    {
                        _registeredConnection = null;
                        Logger.Information("正在连接 {ServerUri}", _serverUri);
                        await connection.StartAsync(cancellationToken);
                    }
                    if (_registeredConnection is null)
                    {
                        await RegisterAsync(connection, cancellationToken);
                        Logger.Information("CapacityAgent 已注册：{AgentName} ({AgentId})", _agentName, _agentId);
                    }

                    await FlushCompletionsAsync();
                    await TryReportStatusAsync(cancellationToken);
                    await Task.Delay(TimeSpan.FromSeconds(_options.StatusReportIntervalSeconds), cancellationToken);
                }
                catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
                {
                    _registeredConnection = null;
                    Logger.Warning(exception, "连接或注册失败；将在五秒后重试");
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Ctrl+C 或调用方取消时正常结束循环，运行中的任务使用同一个关闭信号。
        }
        finally
        {
            _registeredConnection = null;
            // 同一进程内需要等待取消生效，确保求解器退出后才结束 Agent。
            Task[] running;
            lock (_jobLock) running = _activeJobs.Values.Select(job => job.Completion).ToArray();
            await Task.WhenAll(running);
        }
    }

    /// <summary>检查重复任务和并发上限，通过后直接启动任务；满载时记录失败结果等待回传。</summary>
    private void StartJob(CapacitySolveCommand command, CancellationToken shutdown)
    {
        ActiveJob? job;
        lock (_jobLock)
        {
            if (shutdown.IsCancellationRequested) return;
            if (_activeJobs.ContainsKey(command.JobId) || _pendingCompletions.ContainsKey(command.JobId)) return;
            if (_activeJobs.Count >= _options.MaxConcurrentJobs)
            {
                job = null;
                _pendingCompletions[command.JobId] = new CapacitySolveJobCompletion
                {
                    JobId = command.JobId,
                    Error = $"CapacityAgent 并发任务已达上限 {_options.MaxConcurrentJobs}。"
                };
            }
            else
            {
                job = new ActiveJob(NormalizeTaskResources(command.Resources), shutdown);
                _activeJobs[command.JobId] = job;
            }
        }

        if (job is null)
        {
            Logger.Warning("拒绝任务 {JobId}：并发任务已满", command.JobId);
            _ = FlushCompletionsAsync();
        }
        else
        {
            _ = Task.Run(() => ProcessJobAsync(command, job));
        }
    }

    /// <summary>解析输入、调用共享领域层组装对象，直接求解并统一处理结果。</summary>
    private async Task ProcessJobAsync(CapacitySolveCommand command, ActiveJob job)
    {
        using var jobContext = LogContext.PushProperty("JobId", command.JobId);
        var completion = new CapacitySolveJobCompletion { JobId = command.JobId };
        try
        {
            Logger.Information("开始求解，求解器 {SolverId}，CPU {CpuCoreCount} 核、内存 {MemoryLimitMb} MB",
                command.ModelId, job.Resources.CpuCoreCount, job.Resources.MemoryLimitBytes / (1024L * 1024L));
            await TryReportStatusAsync(job.Token);
            if (!_capacitySolverRegistry.TryGet(command.ModelId, out var solver))
                throw new InvalidOperationException($"不支持求解器 {command.ModelId}。");

            // JSON 只在网络边界解析一次；共享领域层组装完成后直接传对象给 Solver。
            var input = command.Input.Deserialize<StationCapacitySolveInput>(JsonOptions)
                ?? throw new InvalidOperationException("求解输入为空或格式不正确。");
            job.Token.ThrowIfCancellationRequested();
            var problem = CapacityProblem.Build(input);
            var result = await solver.SolveAsync(
                problem, new CapacitySolverExecutionContext(command.JobId, job.Resources),
                (percent, message) => ReportProgressAsync(command.JobId, percent, message, job.Token), job.Token);
            completion.Result = JsonSerializer.SerializeToElement(result, JsonOptions);
            completion.Success = true;
            Logger.Information("求解任务已完成");
        }
        catch (OperationCanceledException) when (job.Token.IsCancellationRequested)
        {
            completion.Cancelled = true;
            completion.Error = "求解任务已取消。";
            Logger.Warning("求解任务已取消");
        }
        catch (Exception exception)
        {
            completion.Error = exception.Message;
            Logger.Error(exception, "求解任务失败");
        }
        finally
        {
            // 先保存结果再移除运行标记，避免重复指令在任务状态切换时被再次接受。
            _pendingCompletions[command.JobId] = completion;
            _activeJobs.TryRemove(command.JobId, out _);
            job.Dispose();
        }

        await FlushCompletionsAsync();
        await TryReportStatusAsync(CancellationToken.None);
    }

    /// <summary>通过当前已注册连接回传进度，临时通信故障只记录日志，求解继续执行。</summary>
    private async Task ReportProgressAsync(string jobId, int percent, string message, CancellationToken cancellationToken)
    {
        Logger.Information("任务 {JobId} 进度 {Percent}%：{ProgressMessage}", jobId, percent, message);
        try
        {
            var connection = _registeredConnection ?? throw new InvalidOperationException("CapacityAgent 正在重连。");
            await connection.InvokeAsync("ReportProgress", new CapacitySolveJobProgress
            {
                JobId = jobId,
                Percent = percent,
                Message = message
            }, cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            Logger.Warning(exception, "进度回传暂时失败，将继续求解");
        }
    }

    /// <summary>
    /// 向服务端注册代理身份、求解器、未完成任务、资源状态和访问策略，注册成功后补发待确认结果。
    /// </summary>
    private async Task RegisterAsync(HubConnection connection, CancellationToken cancellationToken)
    {
        await connection.InvokeAsync(
            "RegisterAgent",
            new CapacityAgentRegistration
            {
                AgentId = _agentId,
                Name = _agentName,
                Models = _capacitySolverRegistry.Descriptors.ToList(),
                ActiveJobIds = _activeJobs.Keys.Concat(_pendingCompletions.Keys).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(id => id, StringComparer.Ordinal).ToList(),
                Resources = _resources.CreateStatus(_options, _activeJobs.Values.Select(job => job.Resources)),
                AccessPolicy = CreateAccessPolicy()
            },
            cancellationToken);
        _registeredConnection = connection;
        await FlushCompletionsAsync();
    }

    /// <summary>
    /// 生成供服务端使用的共享访问策略；仅管理员可启用所有用户访问或配置白名单。
    /// </summary>
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

    /// <summary>
    /// 在连接可用时上报资源状态；记录非取消异常，避免临时通信故障中断代理运行。
    /// </summary>
    private async Task TryReportStatusAsync(CancellationToken cancellationToken)
    {
        try
        {
            var connection = _registeredConnection;
            if (connection?.State == HubConnectionState.Connected)
            {
                await connection.InvokeAsync("ReportStatus", _resources.CreateStatus(_options, _activeJobs.Values.Select(job => job.Resources)), cancellationToken);
            }
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            Logger.Debug(exception, "上报 CapacityAgent 资源状态失败");
        }
    }

    /// <summary>
    /// 以代理配置补齐未指定的任务资源，并限制请求上限，保证至少一个 CPU 核和 256 MiB 内存。
    /// </summary>
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

    /// <summary>
    /// 通过互斥锁串行发送待确认结果；单次确认超时为十五秒，失败时保留队列以便后续重试。
    /// </summary>
    private async Task FlushCompletionsAsync()
    {
        if (!await _completionLock.WaitAsync(0)) return;
        try
        {
            var connection = _registeredConnection;
            if (connection?.State != HubConnectionState.Connected) return;
            foreach (var completion in _pendingCompletions.Values)
            {
                try
                {
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                    await connection.InvokeAsync("CompleteJob", completion, timeout.Token);
                    _pendingCompletions.TryRemove(completion.JobId, out _);
                }
                catch (Exception exception)
                {
                    Logger.Warning(exception, "任务 {JobId} 结果尚未确认，将在连接恢复后继续回传", completion.JobId);
                    break;
                }
            }
        }
        finally { _completionLock.Release(); }
    }

    /// <summary>
    /// 复用有效期超过两分钟的令牌，否则串行执行身份验证并更新令牌、到期时间及用户角色。
    /// </summary>
    private async Task<string?> EnsureTokenAsync(CancellationToken cancellationToken)
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

    /// <summary>
    /// 优先从 JSON 响应的 message 字段提取错误信息；不存在该字段或 JSON 无效时返回原始内容。
    /// </summary>
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

    /// <summary>
    /// 根据认证角色和配置记录实际生效的访问策略，并提示普通用户的共享配置被忽略。
    /// </summary>
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

    /// <summary>
    /// 运行中任务的资源配额和取消信号；任务标识由外层字典保存。
    /// </summary>
    private sealed class ActiveJob : IDisposable
    {
        /// <summary>
        /// 运行中任务的取消令牌源，与代理关闭令牌关联，并允许对单个任务发出取消请求。
        /// </summary>
        private readonly CancellationTokenSource _cancellation;
        /// <summary>
        /// 任务取消令牌源是否已进入释放状态，默认关闭，在实例锁内更新并用于阻止释放后的取消操作。
        /// </summary>
        private bool _disposed;
        /// <summary>任务清理完成信号，Agent 退出时等待所有任务释放求解器。</summary>
        private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        /// <summary>表示求解器已完成执行及清理的任务。</summary>
        public Task Completion => _completion.Task;
        /// <summary>
        /// 经代理配置限制后的实际任务资源配额，包含 CPU 核数与内存字节数上限。
        /// </summary>
        public CapacityTaskResourceLimits Resources { get; }
        /// <summary>
        /// 任务执行使用的取消令牌；任务单独取消或代理关闭时均会触发。
        /// </summary>
        public CancellationToken Token => _cancellation.Token;
        /// <summary>
        /// 保存任务资源配额，并创建随代理关闭而取消的任务令牌源。
        /// </summary>
        public ActiveJob(CapacityTaskResourceLimits resources, CancellationToken shutdown)
        {
            Resources = resources;
            _cancellation = CancellationTokenSource.CreateLinkedTokenSource(shutdown);
        }
        /// <summary>
        /// 在锁保护下请求取消尚未释放的任务，避免与令牌源释放发生竞争。
        /// </summary>
        public void Cancel() { lock (this) { if (!_disposed) _cancellation.Cancel(); } }
        /// <summary>
        /// 在锁保护下标记任务已释放并释放其取消令牌源。
        /// </summary>
        public void Dispose()
        {
            lock (this)
            {
                _disposed = true;
                _cancellation.Dispose();
                _completion.TrySetResult();
            }
        }
    }
}
