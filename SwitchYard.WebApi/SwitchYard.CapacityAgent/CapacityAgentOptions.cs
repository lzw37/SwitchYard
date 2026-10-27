using System.Text.Json;

namespace SwitchYard.CapacityAgent;

/// <summary>
/// 从 JSON 文件加载的代理运行配置，定义代理名称、任务并发、单任务资源、上报间隔和共享访问策略。
/// </summary>
internal sealed class CapacityAgentOptions
{
    /// <summary>
    /// 一 MiB 对应的字节数，即 1024 的平方，用于将单任务内存配置换算为字节。
    /// </summary>
    private const long Megabyte = 1024L * 1024L;
    /// <summary>
    /// 读取代理配置的 Web 默认 JSON 选项，额外允许尾随逗号并跳过注释。
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    /// <summary>
    /// 向服务端显示的代理名称；默认为空，启动时使用当前操作系统用户名。
    /// </summary>
    public string AgentName { get; set; } = string.Empty;
    /// <summary>
    /// 代理同时执行的任务数量上限，默认为 2，允许范围为 1 至 128。
    /// </summary>
    public int MaxConcurrentJobs { get; set; } = 2;
    /// <summary>
    /// 每个任务默认分配及最多可请求的 CPU 核数，默认为 2，允许范围为 1 至 128。
    /// </summary>
    public int CpuCoresPerJob { get; set; } = 2;
    /// <summary>
    /// 每个任务传给 SCIP 的内部内存限制，单位为 MiB，默认为 2048，允许范围为 256 至 1048576；不限制 Agent 进程工作集。
    /// </summary>
    public int MemoryLimitMbPerJob { get; set; } = 2048;
    /// <summary>
    /// 周期性上报代理资源状态的间隔秒数，默认为 2，允许范围为 1 至 60。
    /// </summary>
    public int StatusReportIntervalSeconds { get; set; } = 2;
    /// <summary>
    /// 是否允许所有已登录用户使用代理；默认关闭，仅管理员身份启动的代理可使此配置生效。
    /// </summary>
    public bool AllowAllUsers { get; set; }
    /// <summary>
    /// 允许使用代理的额外用户名白名单，默认为空；仅管理员身份启动时生效，加载后去空白并忽略大小写去重。
    /// </summary>
    public List<string> AllowedUsers { get; set; } = new();

    /// <summary>
    /// 由单任务内存配置换算得到的字节数上限，按每 MiB 等于 1024 的平方字节计算。
    /// </summary>
    public long MemoryLimitBytesPerJob => MemoryLimitMbPerJob * Megabyte;

    /// <summary>
    /// 读取支持注释和尾随逗号的 JSON 配置，校验后返回代理运行选项。
    /// </summary>
    /// <param name="path">配置文件路径，也用于定位读取或校验错误。</param>
    /// <returns>通过校验且已清理用户白名单的代理配置。</returns>
    public static CapacityAgentOptions Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"CapacityAgent 配置文件不存在：{path}", path);
        }

        var options = JsonSerializer.Deserialize<CapacityAgentOptions>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidOperationException($"CapacityAgent 配置文件为空：{path}");
        options.Validate(path);
        return options;
    }

    /// <summary>
    /// 校验并发数、单任务资源和状态上报间隔的范围，并去除用户白名单中的空项、首尾空白及重复项。
    /// </summary>
    /// <param name="path">写入校验异常信息的配置文件路径。</param>
    /// <exception cref="InvalidOperationException">配置数值或白名单长度超出允许范围。</exception>
    private void Validate(string path)
    {
        if (MaxConcurrentJobs is < 1 or > 128)
        {
            throw new InvalidOperationException($"{path} 中 maxConcurrentJobs 必须在 1 到 128 之间。");
        }

        if (CpuCoresPerJob is < 1 or > 128)
        {
            throw new InvalidOperationException($"{path} 中 cpuCoresPerJob 必须在 1 到 128 之间。");
        }

        if (MemoryLimitMbPerJob is < 256 or > 1_048_576)
        {
            throw new InvalidOperationException($"{path} 中 memoryLimitMbPerJob 必须在 256 到 1048576 之间。");
        }

        if (StatusReportIntervalSeconds is < 1 or > 60)
        {
            throw new InvalidOperationException($"{path} 中 statusReportIntervalSeconds 必须在 1 到 60 之间。");
        }

        AllowedUsers = (AllowedUsers ?? new List<string>())
            .Where(username => !string.IsNullOrWhiteSpace(username))
            .Select(username => username.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (AllowedUsers.Count > 500 || AllowedUsers.Any(username => username.Length > 100))
        {
            throw new InvalidOperationException($"{path} 中 allowedUsers 最多包含 500 个用户名，且每项不能超过 100 个字符。");
        }
    }
}
