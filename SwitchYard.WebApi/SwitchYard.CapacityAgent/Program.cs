using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Serilog;
using Serilog.Events;
using SwitchYard.Capacity;
using SwitchYard.CapacityAgent;

Console.OutputEncoding = Encoding.UTF8;
var workerMode = CapacitySolveWorkerHost.IsWorker(args);
var logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
var loggerConfiguration = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(
        outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}");
if (!workerMode)
{
    Directory.CreateDirectory(logDirectory);
    loggerConfiguration.WriteTo.File(
        path: Path.Combine(logDirectory, "capacity-agent-.log"),
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] {Message:lj} {Properties:j}{NewLine}{Exception}",
        retainedFileCountLimit: 30,
        fileSizeLimitBytes: 50 * 1024 * 1024,
        rollOnFileSizeLimit: true,
        shared: true,
        flushToDiskInterval: TimeSpan.FromSeconds(1));
}

Log.Logger = loggerConfiguration.CreateLogger();

try
{
    if (workerMode)
    {
        Environment.ExitCode = await CapacitySolveWorkerHost.RunAsync(args);
        return;
    }

    Log.Information("SwitchYard CapacityAgent 启动，日志目录：{LogDirectory}", logDirectory);
    var configPath = ResolveArgumentPath(args, "--config")
        ?? Path.Combine(AppContext.BaseDirectory, "capacity-agent.json");
    var options = CapacityAgentOptions.Load(configPath);
    Log.Information(
        "已加载配置 {ConfigPath}：并发上限 {MaxConcurrentJobs}，每任务 CPU {CpuCoresPerJob} 核、内存 {MemoryLimitMbPerJob} MB，状态上报间隔 {StatusIntervalSeconds} 秒",
        configPath,
        options.MaxConcurrentJobs,
        options.CpuCoresPerJob,
        options.MemoryLimitMbPerJob,
        options.StatusReportIntervalSeconds);

    if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
    {
        await RunSelfTestAsync(options);
        return;
    }

    Console.WriteLine("SwitchYard CapacityAgent");
    Console.WriteLine("========================");

    var serverUri = ReadServerUri();
    var username = ReadRequired("用户名：");
    var password = ReadPassword("密码：");
    var agentName = string.IsNullOrWhiteSpace(options.AgentName)
        ? Environment.UserName
        : options.AgentName.Trim();
    var agentId = CreateAgentId(agentName, username);
    var models = new CapacityModelRegistry(new ICapacityModel[] { new StationCapacityModel() });

    Console.WriteLine();
    Console.WriteLine($"Agent 名称：{agentName}");
    Console.WriteLine($"支持模型：{string.Join(", ", models.Descriptors.Select(model => model.Name))}");
    Console.WriteLine($"并发上限：{options.MaxConcurrentJobs}，每任务 CPU：{options.CpuCoresPerJob} 核，内存：{options.MemoryLimitMbPerJob} MB");
    Log.Information(
        "Agent 初始化完成：{AgentName} ({AgentId})，服务器 {ServerUri}，用户 {Username}，模型 {Models}",
        agentName,
        agentId,
        serverUri,
        username,
        models.Descriptors.Select(model => model.Id).ToArray());

    using var cancellationSource = new CancellationTokenSource();
    Console.CancelKeyPress += (_, eventArgs) =>
    {
        eventArgs.Cancel = true;
        cancellationSource.Cancel();
        Log.Information("收到退出信号，正在停止 CapacityAgent");
    };

    var client = new CapacityAgentClient(serverUri, username, password, agentId, agentName, models, options);
    await client.RunAsync(cancellationSource.Token);
}
catch (Exception exception)
{
    Log.Fatal(exception, "CapacityAgent 因未处理异常退出");
    Environment.ExitCode = 1;
}
finally
{
    Log.Information("CapacityAgent 已退出");
    Log.CloseAndFlush();
}

static string? ResolveArgumentPath(IReadOnlyList<string> arguments, string name)
{
    for (var index = 0; index < arguments.Count - 1; index++)
    {
        if (string.Equals(arguments[index], name, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(arguments[index + 1]))
        {
            return Path.GetFullPath(arguments[index + 1]);
        }
    }

    return null;
}

static Uri ReadServerUri()
{
    while (true)
    {
        var value = ReadRequired("Web 服务器 URL（例如 https://localhost:7297）：").Trim();
        if (Uri.TryCreate(value.EndsWith('/') ? value : value + "/", UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return uri;
        }

        Console.WriteLine("请输入有效的 HTTP 或 HTTPS URL。");
    }
}

static string ReadRequired(string prompt)
{
    while (true)
    {
        Console.Write(prompt);
        var value = Console.ReadLine()?.Trim();
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }
    }
}

static string ReadPassword(string prompt)
{
    Console.Write(prompt);
    var password = new StringBuilder();
    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter)
        {
            Console.WriteLine();
            if (password.Length > 0)
            {
                return password.ToString();
            }
            Console.Write(prompt);
            continue;
        }

        if (key.Key == ConsoleKey.Backspace)
        {
            if (password.Length > 0)
            {
                password.Length--;
                Console.Write("\b \b");
            }
            continue;
        }

        if (!char.IsControl(key.KeyChar))
        {
            password.Append(key.KeyChar);
            Console.Write('*');
        }
    }
}

static string CreateAgentId(string machineName, string username)
{
    var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{machineName}\n{username}"));
    return $"agent-{Convert.ToHexString(bytes)[..16].ToLowerInvariant()}";
}

static async Task RunSelfTestAsync(CapacityAgentOptions options)
{
    var input = new StationCapacitySolveInput
    {
        Settings = new StationCapacitySolveSettings
        {
            LeftShiftToleranceSeconds = 0,
            RightShiftToleranceSeconds = 600,
            TimeLimitSeconds = 30,
            ThreadCount = 1
        },
        Routes =
        {
            new StationCapacityRouteInput { Id = "R1", CellIds = { "C1" } }
        },
        RouteOccupations =
        {
            new StationCapacityRouteOccupationInput
            {
                RouteId = "R1",
                CellId = "C1",
                StartOccupationShiftSeconds = 0,
                EndOccupationShiftSeconds = 0
            }
        },
        Trains =
        {
            new StationCapacityTrainInput
            {
                Id = "T1",
                Movements =
                {
                    new StationCapacityMovementInput
                    {
                        Id = "M1",
                        CandidateRouteIds = { "R1" },
                        OriginalStartSeconds = 0,
                        OriginalEndSeconds = 60,
                        MinDurationSeconds = 60,
                        MaxDurationSeconds = 60
                    }
                }
            },
            new StationCapacityTrainInput
            {
                Id = "T2",
                Movements =
                {
                    new StationCapacityMovementInput
                    {
                        Id = "M1",
                        CandidateRouteIds = { "R1" },
                        OriginalStartSeconds = 0,
                        OriginalEndSeconds = 60,
                        MinDurationSeconds = 60,
                        MaxDurationSeconds = 60
                    }
                }
            }
        }
    };

    var model = new StationCapacityModel();
    var serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
    var parallelTaskCount = Math.Min(2, options.MaxConcurrentJobs);
    var solveTasks = Enumerable.Range(1, parallelTaskCount)
        .Select(taskIndex => model.SolveAsync(
            JsonSerializer.SerializeToElement(input, serializerOptions),
            new CapacityModelExecutionContext(
                $"self-test-{taskIndex}",
                new CapacityTaskResourceLimits
                {
                    CpuCoreCount = options.CpuCoresPerJob,
                    MemoryLimitBytes = options.MemoryLimitBytesPerJob
                }),
            (percent, message) =>
            {
                Log.Information(
                    "并发自检任务 {TaskIndex}/{TaskCount} 进度 {Percent}%：{ProgressMessage}",
                    taskIndex,
                    parallelTaskCount,
                    percent,
                    message);
                return Task.CompletedTask;
            },
            CancellationToken.None))
        .ToArray();
    var results = await Task.WhenAll(solveTasks);
    var resultModels = results
        .Select(result => result.Deserialize<StationCapacitySolveResult>(serializerOptions))
        .ToList();
    if (resultModels.Any(result => result?.HasSolution != true || result.Trains.Count != 2))
    {
        throw new InvalidOperationException("CapacityAgent 自检失败：求解器未返回预期结果。");
    }

    Console.WriteLine(JsonSerializer.Serialize(resultModels[0], new JsonSerializerOptions(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    }));
    Log.Information("CapacityAgent 并发自检通过：{ParallelTaskCount} 个 Worker 任务", parallelTaskCount);
}
