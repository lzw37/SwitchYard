using System.Security.Cryptography;
using System.Text;
using Serilog;
using Serilog.Events;
using SwitchYard.Capacity;
using SwitchYard.CapacityAgent;
using SwitchYard.CapacitySolver;

// 控制台统一使用 UTF-8 输出，确保中文提示和求解日志正确显示。
Console.OutputEncoding = Encoding.UTF8;
// 代理文件日志目录，位于程序基目录的 logs 子目录。
var logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
// 日志配置构建器：默认记录信息级别，Microsoft 组件仅记录警告及以上，并输出到控制台。
var loggerConfiguration = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(
        outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}");
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

// 将日志记录器设为全局实例，供代理和求解库共同使用。
Log.Logger = loggerConfiguration.CreateLogger();

try
{
    Log.Information("SwitchYard CapacityAgent 启动，日志目录：{LogDirectory}", logDirectory);
    // 优先使用 --config 指定的配置路径，未指定时读取程序基目录中的 capacity-agent.json。
    var configPath = ResolveArgumentPath(args, "--config")
        ?? Path.Combine(AppContext.BaseDirectory, "capacity-agent.json");
    // 已加载并校验的代理运行选项，包含并发上限、单任务资源配额及访问策略等设置。
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
        throw new ArgumentException("求解自检已迁至 SwitchYard.CapacitySolver.Tests，请运行该测试项目。");
    }

    Console.WriteLine("SwitchYard CapacityAgent");
    Console.WriteLine("========================");

    // 用户输入并通过格式校验的服务器基础地址，供认证接口和 SignalR 连接使用。
    var serverUri = ReadServerUri();
    // 用于服务端身份验证的登录用户名，同时参与生成稳定的代理标识。
    var username = ReadRequired("用户名：");
    // 通过星号回显读取的原始登录密码，创建客户端时将其转换为认证所需的 SHA-256 摘要。
    var password = ReadPassword("密码：");
    // 代理展示名称：配置为空或仅含空白时使用操作系统用户名，否则使用去除首尾空白的配置值。
    var agentName = string.IsNullOrWhiteSpace(options.AgentName)
        ? Environment.UserName
        : options.AgentName.Trim();
    // 根据代理展示名称和登录用户名生成的稳定标识，用于向服务端注册当前代理。
    var agentId = CreateAgentId(agentName, username);
    // 创建能力求解器注册表，客户端收到任务后直接传入组装好的领域对象。
    var solvers = new CapacitySolverRegistry();

    Console.WriteLine();
    Console.WriteLine($"Agent 名称：{agentName}");
    Console.WriteLine($"支持求解器：{string.Join(", ", solvers.Descriptors.Select(solver => solver.Name))}");
    Console.WriteLine($"并发上限：{options.MaxConcurrentJobs}，每任务 CPU：{options.CpuCoresPerJob} 核，内存：{options.MemoryLimitMbPerJob} MB");
    Log.Information(
        "Agent 初始化完成：{AgentName} ({AgentId})，服务器 {ServerUri}，用户 {Username}，求解器 {Solvers}",
        agentName,
        agentId,
        serverUri,
        username,
        solvers.Descriptors.Select(solver => solver.Id).ToArray());

    // 代理运行期间共享的退出信号源，按下 Ctrl+C 时取消，驱动连接、状态上报及运行中任务停止。
    using var cancellationSource = new CancellationTokenSource();
    Console.CancelKeyPress += (_, eventArgs) =>
    {
        eventArgs.Cancel = true;
        cancellationSource.Cancel();
        Log.Information("收到退出信号，正在停止 CapacityAgent");
    };

    // 负责认证、服务端通信、任务调度及资源上报的代理客户端，使用上述身份、求解器和配置运行。
    var client = new CapacityAgentClient(serverUri, username, password, agentId, agentName, solvers, options);
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

// 读取指定命令行选项后的路径并转换为绝对路径；选项缺失或值为空时返回 null。
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

// 反复提示输入服务器地址，直到获得有效的 HTTP 或 HTTPS 绝对地址，并补齐末尾斜杠。
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

// 显示提示并读取去除首尾空白的文本，输入为空时继续提示。
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

// 逐键读取非空密码，以星号回显并支持退格删除。
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

// 对名称和用户名的组合计算 SHA-256，截取摘要生成稳定的代理标识。
static string CreateAgentId(string machineName, string username)
{
    var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{machineName}\n{username}"));
    return $"agent-{Convert.ToHexString(bytes)[..16].ToLowerInvariant()}";
}
