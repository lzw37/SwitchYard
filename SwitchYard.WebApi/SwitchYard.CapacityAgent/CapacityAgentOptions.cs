using System.Text.Json;

namespace SwitchYard.CapacityAgent;

internal sealed class CapacityAgentOptions
{
    private const long Megabyte = 1024L * 1024L;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public string AgentName { get; set; } = string.Empty;
    public int MaxConcurrentJobs { get; set; } = 2;
    public int CpuCoresPerJob { get; set; } = 2;
    public int MemoryLimitMbPerJob { get; set; } = 2048;
    public int StatusReportIntervalSeconds { get; set; } = 2;
    public bool AllowAllUsers { get; set; }
    public List<string> AllowedUsers { get; set; } = new();

    public long MemoryLimitBytesPerJob => MemoryLimitMbPerJob * Megabyte;

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
