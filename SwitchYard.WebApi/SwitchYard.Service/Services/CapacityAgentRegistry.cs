using SwitchYard.Capacity;

namespace SwitchYard.Service.Services;

public sealed class CapacityAgentRegistry
{
    private readonly object _syncRoot = new();
    private readonly Dictionary<string, AgentRecord> _agents = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _agentIdByConnection = new(StringComparer.Ordinal);

    public string? Register(
        string agentId,
        string name,
        string username,
        string connectionId,
        IReadOnlyCollection<CapacityModelDescriptor> models,
        string? activeJobId)
    {
        lock (_syncRoot)
        {
            string? abandonedJobId = null;
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

                if (!string.IsNullOrWhiteSpace(record.ActiveJobId) &&
                    !string.Equals(record.ActiveJobId, activeJobId, StringComparison.Ordinal))
                {
                    abandonedJobId = record.ActiveJobId;
                }
            }

            var now = DateTimeOffset.UtcNow;
            record.Name = name;
            record.Username = username;
            record.ConnectionId = connectionId;
            record.IsConnected = true;
            record.IsBusy = !string.IsNullOrWhiteSpace(activeJobId);
            record.ActiveJobId = string.IsNullOrWhiteSpace(activeJobId) ? null : activeJobId;
            record.ConnectedAt = now;
            record.LastSeenAt = now;
            record.Models = models
                .GroupBy(model => model.Id, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
            _agentIdByConnection[connectionId] = agentId;
            return abandonedJobId;
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

    public IReadOnlyList<CapacityAgentInfo> GetAgents()
    {
        lock (_syncRoot)
        {
            return _agents.Values
                .Select(ToInfo)
                .OrderByDescending(agent => agent.IsConnected)
                .ThenBy(agent => agent.IsBusy)
                .ThenBy(agent => agent.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    public bool TryReserve(
        string agentId,
        string modelId,
        string jobId,
        out string connectionId,
        out CapacityAgentInfo? agent,
        out string error)
    {
        lock (_syncRoot)
        {
            connectionId = string.Empty;
            agent = null;
            error = string.Empty;
            if (!_agents.TryGetValue(agentId, out var record) ||
                !record.IsConnected ||
                string.IsNullOrWhiteSpace(record.ConnectionId))
            {
                error = "指定的 CapacityAgent 当前未连接。";
                return false;
            }

            if (record.IsBusy)
            {
                error = "指定的 CapacityAgent 正在执行其他任务。";
                return false;
            }

            if (!record.Models.Any(model => string.Equals(model.Id, modelId, StringComparison.OrdinalIgnoreCase)))
            {
                error = "指定的 CapacityAgent 不支持所选模型。";
                return false;
            }

            record.IsBusy = true;
            record.ActiveJobId = jobId;
            record.LastSeenAt = DateTimeOffset.UtcNow;
            connectionId = record.ConnectionId;
            agent = ToInfo(record);
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
            if (_agents.TryGetValue(agentId, out var record) &&
                string.Equals(record.ActiveJobId, jobId, StringComparison.Ordinal))
            {
                record.IsBusy = false;
                record.ActiveJobId = null;
                record.LastSeenAt = DateTimeOffset.UtcNow;
            }
        }
    }

    private static CapacityAgentInfo ToInfo(AgentRecord record) => new()
    {
        AgentId = record.AgentId,
        Name = record.Name,
        Username = record.Username,
        IsConnected = record.IsConnected,
        IsBusy = record.IsBusy,
        ConnectedAt = record.ConnectedAt,
        LastSeenAt = record.LastSeenAt,
        Models = record.Models.Select(model => new CapacityModelDescriptor
        {
            Id = model.Id,
            Name = model.Name,
            Version = model.Version,
            Description = model.Description
        }).ToList()
    };

    private sealed class AgentRecord
    {
        public string AgentId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string? ConnectionId { get; set; }
        public bool IsConnected { get; set; }
        public bool IsBusy { get; set; }
        public string? ActiveJobId { get; set; }
        public DateTimeOffset ConnectedAt { get; set; }
        public DateTimeOffset LastSeenAt { get; set; }
        public List<CapacityModelDescriptor> Models { get; set; } = new();
    }
}
