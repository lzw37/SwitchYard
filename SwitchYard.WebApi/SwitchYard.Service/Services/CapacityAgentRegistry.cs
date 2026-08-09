using SwitchYard.Capacity;

namespace SwitchYard.Service.Services;

public sealed class CapacityAgentRegistry
{
    private readonly object _syncRoot = new();
    private readonly Dictionary<string, AgentRecord> _agents = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _agentIdByConnection = new(StringComparer.Ordinal);

    public IReadOnlyList<string> Register(
        string agentId,
        string name,
        string username,
        string connectionId,
        IReadOnlyCollection<CapacityModelDescriptor> models,
        IReadOnlyCollection<string> activeJobIds,
        CapacityAgentResourceStatus resources,
        bool ownerIsAdministrator,
        CapacityAgentAccessPolicy accessPolicy)
    {
        lock (_syncRoot)
        {
            var abandonedJobIds = new List<string>();
            if (!_agents.TryGetValue(agentId, out var record))
            {
                record = new AgentRecord { AgentId = agentId };
                _agents.Add(agentId, record);
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(record.ConnectionId))
                {
                    _agentIdByConnection.Remove(record.ConnectionId);
                }

                var resumedJobs = activeJobIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
                abandonedJobIds.AddRange(record.ActiveJobIds.Where(jobId => !resumedJobs.Contains(jobId)));
            }

            var now = DateTimeOffset.UtcNow;
            record.Name = name;
            record.Username = username;
            record.OwnerIsAdministrator = ownerIsAdministrator;
            record.ConnectionId = connectionId;
            record.IsConnected = true;
            record.ActiveJobIds = activeJobIds
                .Where(jobId => !string.IsNullOrWhiteSpace(jobId))
                .Select(jobId => jobId.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            record.ConnectedAt = now;
            record.LastSeenAt = now;
            record.Models = models
                .GroupBy(model => model.Id, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
            record.Resources = NormalizeResources(resources);
            ApplyAccessPolicy(record, accessPolicy);
            UpdateDerivedResources(record);
            _agentIdByConnection[connectionId] = agentId;
            return abandonedJobIds;
        }
    }

    public void Unregister(string connectionId)
    {
        lock (_syncRoot)
        {
            if (!_agentIdByConnection.Remove(connectionId, out var agentId) ||
                !_agents.TryGetValue(agentId, out var record) ||
                !string.Equals(record.ConnectionId, connectionId, StringComparison.Ordinal))
            {
                return;
            }

            record.ConnectionId = null;
            record.IsConnected = false;
            record.LastSeenAt = DateTimeOffset.UtcNow;
        }
    }

    public IReadOnlyList<CapacityAgentInfo> GetAgents(string requestedBy)
    {
        lock (_syncRoot)
        {
            return _agents.Values
                .Select(record => ToInfo(record, requestedBy))
                .OrderByDescending(agent => agent.IsConnected)
                .ThenByDescending(agent => agent.IsAvailable)
                .ThenBy(agent => agent.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    public bool TryReserve(
        string agentId,
        string modelId,
        string jobId,
        string requestedBy,
        out string connectionId,
        out CapacityAgentInfo? agent,
        out CapacityTaskResourceLimits resourceLimits,
        out string error)
    {
        lock (_syncRoot)
        {
            connectionId = string.Empty;
            agent = null;
            resourceLimits = new CapacityTaskResourceLimits();
            error = string.Empty;
            if (!_agents.TryGetValue(agentId, out var record) ||
                !record.IsConnected ||
                string.IsNullOrWhiteSpace(record.ConnectionId))
            {
                error = "指定的 CapacityAgent 当前未连接。";
                return false;
            }

            if (!CanUserUse(record, requestedBy))
            {
                error = "当前用户无权使用指定的 CapacityAgent。";
                return false;
            }

            if (record.Resources.AvailableJobSlots <= 0)
            {
                error = $"指定的 CapacityAgent 当前没有可用任务槽位或剩余 CPU/内存不足；活动任务 {record.ActiveJobIds.Count}/{record.Resources.MaxConcurrentJobs}。";
                return false;
            }

            if (!record.Models.Any(model => string.Equals(model.Id, modelId, StringComparison.OrdinalIgnoreCase)))
            {
                error = "指定的 CapacityAgent 不支持所选模型。";
                return false;
            }

            record.ActiveJobIds.Add(jobId);
            record.LastSeenAt = DateTimeOffset.UtcNow;
            UpdateDerivedResources(record);
            connectionId = record.ConnectionId;
            agent = ToInfo(record, requestedBy);
            resourceLimits = new CapacityTaskResourceLimits
            {
                CpuCoreCount = record.Resources.CpuCoresPerJob,
                MemoryLimitBytes = record.Resources.MemoryLimitBytesPerJob
            };
            return true;
        }
    }

    public bool IsCurrentConnection(string agentId, string connectionId)
    {
        lock (_syncRoot)
        {
            return _agents.TryGetValue(agentId, out var record) &&
                   string.Equals(record.ConnectionId, connectionId, StringComparison.Ordinal);
        }
    }

    public string? GetAgentId(string connectionId)
    {
        lock (_syncRoot)
        {
            return _agentIdByConnection.TryGetValue(connectionId, out var agentId) ? agentId : null;
        }
    }

    public void UpdateStatus(string agentId, string connectionId, CapacityAgentResourceStatus resources)
    {
        lock (_syncRoot)
        {
            if (!_agents.TryGetValue(agentId, out var record) ||
                !string.Equals(record.ConnectionId, connectionId, StringComparison.Ordinal))
            {
                return;
            }

            record.Resources = NormalizeResources(resources);
            record.LastSeenAt = DateTimeOffset.UtcNow;
            UpdateDerivedResources(record);
        }
    }

    public void Touch(string agentId)
    {
        lock (_syncRoot)
        {
            if (_agents.TryGetValue(agentId, out var record))
            {
                record.LastSeenAt = DateTimeOffset.UtcNow;
            }
        }
    }

    public void Release(string agentId, string jobId)
    {
        lock (_syncRoot)
        {
            if (_agents.TryGetValue(agentId, out var record) && record.ActiveJobIds.Remove(jobId))
            {
                record.LastSeenAt = DateTimeOffset.UtcNow;
                UpdateDerivedResources(record);
            }
        }
    }

    private static CapacityAgentInfo ToInfo(AgentRecord record, string requestedBy) => new()
    {
        AgentId = record.AgentId,
        Name = record.Name,
        Username = record.Username,
        IsConnected = record.IsConnected,
        IsBusy = record.ActiveJobIds.Count > 0,
        IsAvailable = record.IsConnected && record.Resources.AvailableJobSlots > 0,
        CanUse = CanUserUse(record, requestedBy),
        ConnectedAt = record.ConnectedAt,
        LastSeenAt = record.LastSeenAt,
        Models = record.Models.Select(model => new CapacityModelDescriptor
        {
            Id = model.Id,
            Name = model.Name,
            Version = model.Version,
            Description = model.Description
        }).ToList(),
        Resources = CloneResources(record.Resources)
    };

    private static void ApplyAccessPolicy(AgentRecord record, CapacityAgentAccessPolicy? requestedPolicy)
    {
        record.AllowAllUsers = record.OwnerIsAdministrator && requestedPolicy?.AllowAllUsers == true;
        record.AllowedUsers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            record.Username
        };
        if (!record.OwnerIsAdministrator || record.AllowAllUsers)
        {
            return;
        }

        foreach (var username in (requestedPolicy?.AllowedUsers ?? new List<string>())
                     .Where(username => !string.IsNullOrWhiteSpace(username))
                     .Select(username => username.Trim())
                     .Where(username => username.Length <= 100)
                     .Take(500))
        {
            record.AllowedUsers.Add(username);
        }
    }

    private static bool CanUserUse(AgentRecord record, string requestedBy) =>
        !string.IsNullOrWhiteSpace(requestedBy) &&
        (record.AllowAllUsers || record.AllowedUsers.Contains(requestedBy.Trim()));

    private static CapacityAgentResourceStatus NormalizeResources(CapacityAgentResourceStatus? source)
    {
        source ??= new CapacityAgentResourceStatus();
        return new CapacityAgentResourceStatus
        {
            MaxConcurrentJobs = Math.Clamp(source.MaxConcurrentJobs, 1, 128),
            ActiveJobCount = Math.Max(0, source.ActiveJobCount),
            AvailableJobSlots = Math.Max(0, source.AvailableJobSlots),
            LogicalCpuCores = Math.Clamp(source.LogicalCpuCores, 1, 1024),
            CpuCoresPerJob = Math.Clamp(source.CpuCoresPerJob, 1, 128),
            AllocatedCpuCores = Math.Max(0, source.AllocatedCpuCores),
            CpuUsagePercent = Math.Clamp(source.CpuUsagePercent, 0, 100),
            MemoryLimitBytesPerJob = Math.Max(0, source.MemoryLimitBytesPerJob),
            AllocatedMemoryBytes = Math.Max(0, source.AllocatedMemoryBytes),
            TotalMemoryBytes = Math.Max(0, source.TotalMemoryBytes),
            MemoryLoadBytes = Math.Max(0, source.MemoryLoadBytes),
            ProcessWorkingSetBytes = Math.Max(0, source.ProcessWorkingSetBytes),
            MemoryUsagePercent = Math.Clamp(source.MemoryUsagePercent, 0, 100),
            CollectedAt = source.CollectedAt
        };
    }

    private static void UpdateDerivedResources(AgentRecord record)
    {
        record.Resources.ActiveJobCount = record.ActiveJobIds.Count;
        record.Resources.AllocatedCpuCores = record.ActiveJobIds.Count * record.Resources.CpuCoresPerJob;
        record.Resources.AllocatedMemoryBytes = record.ActiveJobIds.Count * record.Resources.MemoryLimitBytesPerJob;
        var concurrencySlots = Math.Max(0, record.Resources.MaxConcurrentJobs - record.ActiveJobIds.Count);
        var cpuSlots = record.Resources.CpuCoresPerJob <= 0
            ? concurrencySlots
            : Math.Max(0, record.Resources.LogicalCpuCores - record.Resources.AllocatedCpuCores) /
              record.Resources.CpuCoresPerJob;
        var reservationMemorySlots = record.Resources.MemoryLimitBytesPerJob <= 0 || record.Resources.TotalMemoryBytes <= 0
            ? concurrencySlots
            : (int)Math.Min(
                int.MaxValue,
                Math.Max(0, record.Resources.TotalMemoryBytes - record.Resources.AllocatedMemoryBytes) /
                record.Resources.MemoryLimitBytesPerJob);
        var physicalMemorySlots = record.Resources.MemoryLimitBytesPerJob <= 0 || record.Resources.TotalMemoryBytes <= 0
            ? concurrencySlots
            : (int)Math.Min(
                int.MaxValue,
                Math.Max(0, record.Resources.TotalMemoryBytes - record.Resources.MemoryLoadBytes) /
                record.Resources.MemoryLimitBytesPerJob);
        var memorySlots = Math.Min(reservationMemorySlots, physicalMemorySlots);
        record.Resources.AvailableJobSlots = Math.Min(concurrencySlots, Math.Min(cpuSlots, memorySlots));
    }

    private static CapacityAgentResourceStatus CloneResources(CapacityAgentResourceStatus source) => new()
    {
        MaxConcurrentJobs = source.MaxConcurrentJobs,
        ActiveJobCount = source.ActiveJobCount,
        AvailableJobSlots = source.AvailableJobSlots,
        LogicalCpuCores = source.LogicalCpuCores,
        CpuCoresPerJob = source.CpuCoresPerJob,
        AllocatedCpuCores = source.AllocatedCpuCores,
        CpuUsagePercent = source.CpuUsagePercent,
        MemoryLimitBytesPerJob = source.MemoryLimitBytesPerJob,
        AllocatedMemoryBytes = source.AllocatedMemoryBytes,
        TotalMemoryBytes = source.TotalMemoryBytes,
        MemoryLoadBytes = source.MemoryLoadBytes,
        ProcessWorkingSetBytes = source.ProcessWorkingSetBytes,
        MemoryUsagePercent = source.MemoryUsagePercent,
        CollectedAt = source.CollectedAt
    };

    private sealed class AgentRecord
    {
        public string AgentId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public bool OwnerIsAdministrator { get; set; }
        public bool AllowAllUsers { get; set; }
        public HashSet<string> AllowedUsers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public string? ConnectionId { get; set; }
        public bool IsConnected { get; set; }
        public HashSet<string> ActiveJobIds { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public DateTimeOffset ConnectedAt { get; set; }
        public DateTimeOffset LastSeenAt { get; set; }
        public List<CapacityModelDescriptor> Models { get; set; } = new();
        public CapacityAgentResourceStatus Resources { get; set; } = new();
    }
}
