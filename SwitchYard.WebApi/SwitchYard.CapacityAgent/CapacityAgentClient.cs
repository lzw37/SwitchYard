using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using SwitchYard.Capacity;

namespace SwitchYard.CapacityAgent;

internal sealed class CapacityAgentClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly Uri _serverUri;
    private readonly string _username;
    private readonly string _passwordHash;
    private readonly string _agentId;
    private readonly string _agentName;
    private readonly CapacityModelRegistry _models;
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _solveLock = new(1, 1);
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private string _accessToken = string.Empty;
    private DateTimeOffset _accessTokenExpiresAt = DateTimeOffset.MinValue;
    private string? _activeJobId;

    public CapacityAgentClient(
        Uri serverUri,
        string username,
        string password,
        string agentId,
        string agentName,
        CapacityModelRegistry models)
    {
        _serverUri = serverUri;
        _username = username;
        _passwordHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password))).ToLowerInvariant();
        _agentId = agentId;
        _agentName = agentName;
        _models = models;
        _httpClient = new HttpClient { BaseAddress = serverUri, Timeout = TimeSpan.FromSeconds(30) };
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
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"连接失败：{exception.Message}");
                Console.ResetColor();
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
            ProcessJobAsync(connection, command, cancellationToken));
        connection.Reconnecting += exception =>
        {
            Console.WriteLine($"长连接中断，正在重连：{exception?.Message}");
            return Task.CompletedTask;
        };
        connection.Reconnected += async _ =>
        {
            await RegisterAsync(connection, cancellationToken);
            Console.WriteLine("长连接已恢复。");
        };

        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.Closed += exception =>
        {
            if (exception != null)
            {
                Console.WriteLine($"长连接已关闭：{exception.Message}");
            }
            closed.TrySetResult();
            return Task.CompletedTask;
        };

        Console.WriteLine($"正在连接 {_serverUri} ...");
        await connection.StartAsync(cancellationToken);
        await RegisterAsync(connection, cancellationToken);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"CapacityAgent 已注册：{_agentName} ({_agentId})");
        Console.ResetColor();
        Console.WriteLine("等待求解指令，按 Ctrl+C 退出。");

        await closed.Task.WaitAsync(cancellationToken);
    }

    private async Task ProcessJobAsync(
        HubConnection connection,
        CapacitySolveCommand command,
        CancellationToken cancellationToken)
    {
        if (!await _solveLock.WaitAsync(0, cancellationToken))
        {
            await SafeCompleteAsync(connection, new CapacitySolveJobCompletion
            {
                JobId = command.JobId,
                Success = false,
                Error = "CapacityAgent 正在执行另一个任务。"
            }, cancellationToken);
            return;
        }

        try
        {
            _activeJobId = command.JobId;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 开始任务 {command.JobId}，模型 {command.ModelId}");
            if (!_models.TryGet(command.ModelId, out var model))
            {
                throw new InvalidOperationException($"不支持模型 {command.ModelId}。");
            }

            async Task ReportProgress(int percent, string message)
            {
                Console.WriteLine($"[{command.JobId}] {percent,3}% {message}");
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
                    Console.WriteLine($"进度回传暂时失败，将继续求解：{exception.Message}");
                }
            }

            var result = await model.SolveAsync(command.Input, ReportProgress, cancellationToken);
            await SafeCompleteAsync(connection, new CapacitySolveJobCompletion
            {
                JobId = command.JobId,
                Success = true,
                Result = result
            }, cancellationToken);
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 任务 {command.JobId} 已完成");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await SafeCompleteAsync(connection, new CapacitySolveJobCompletion
            {
                JobId = command.JobId,
                Success = false,
                Error = "CapacityAgent 已停止。"
            }, CancellationToken.None);
        }
        catch (Exception exception)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"任务 {command.JobId} 失败：{exception.Message}");
            Console.ResetColor();
            await SafeCompleteAsync(connection, new CapacitySolveJobCompletion
            {
                JobId = command.JobId,
                Success = false,
                Error = exception.Message
            }, cancellationToken);
        }
        finally
        {
            _activeJobId = null;
            _solveLock.Release();
        }
    }

    private async Task RegisterAsync(HubConnection connection, CancellationToken cancellationToken)
    {
        await connection.InvokeAsync(
            "RegisterAgent",
            _agentId,
            _agentName,
            _models.Descriptors,
            _activeJobId,
            cancellationToken);
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
                Console.WriteLine($"回传任务结果失败，正在重试（{attempt}/5）：{exception.Message}");
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
            catch (Exception exception)
            {
                Console.WriteLine($"回传任务结果失败：{exception.Message}");
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
}
