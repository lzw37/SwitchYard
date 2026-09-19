using System.Text.Json;

namespace SwitchYard.Capacity;

public static class CapacityAgentProtocol
{
    public const string ClientTypeClaim = "switchyard_client_type";
    public const string CapacityAgentClientType = "capacity-agent";
    public const string StationCapacityModelId = "station-capacity-v1";
}

public sealed class CapacityAgentAuthenticationRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class CapacityAgentAuthenticationResponse
{
    public string Token { get; set; } = string.Empty;
    public string TokenType { get; set; } = "Bearer";
    public int ExpiresIn { get; set; }
    public string Username { get; set; } = string.Empty;
    public bool IsAdministrator { get; set; }
}

public sealed class CapacityModelDescriptor
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public sealed class CapacityAgentInfo
{
    public string AgentId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public bool IsConnected { get; set; }
    public bool IsBusy { get; set; }
    public bool IsAvailable { get; set; }
    public bool CanUse { get; set; }
    public DateTimeOffset ConnectedAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
    public List<CapacityModelDescriptor> Models { get; set; } = new();
    public CapacityAgentResourceStatus Resources { get; set; } = new();
}

public sealed class CapacityAgentRegistration
{
    public string AgentId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<CapacityModelDescriptor> Models { get; set; } = new();
    public List<string> ActiveJobIds { get; set; } = new();
    public CapacityAgentResourceStatus Resources { get; set; } = new();
    public CapacityAgentAccessPolicy AccessPolicy { get; set; } = new();
}

public sealed class CapacityAgentAccessPolicy
{
    public bool AllowAllUsers { get; set; }
    public List<string> AllowedUsers { get; set; } = new();
}

public sealed class CapacityAgentResourceStatus
{
    public int MaxConcurrentJobs { get; set; } = 1;
    public int ActiveJobCount { get; set; }
    public int AvailableJobSlots { get; set; } = 1;
    public int LogicalCpuCores { get; set; } = 1;
    public int CpuCoresPerJob { get; set; } = 1;
    public int AllocatedCpuCores { get; set; }
    public double CpuUsagePercent { get; set; }
    public long MemoryLimitBytesPerJob { get; set; }
    public long AllocatedMemoryBytes { get; set; }
    public long TotalMemoryBytes { get; set; }
    public long MemoryLoadBytes { get; set; }
    public long ProcessWorkingSetBytes { get; set; }
    public double MemoryUsagePercent { get; set; }
    public DateTimeOffset CollectedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class CapacityTaskResourceLimits
{
    public int CpuCoreCount { get; set; } = 1;
    public long MemoryLimitBytes { get; set; }
}

public sealed class CapacitySolveJobRequest
{
    public string AgentId { get; set; } = string.Empty;
    public string ModelId { get; set; } = CapacityAgentProtocol.StationCapacityModelId;
    public JsonElement Input { get; set; }
}

public sealed class StationCapacityInputRequest
{
    public string InstanceId { get; set; } = string.Empty;
    public string StationSchemeId { get; set; } = string.Empty;
    public string OperationPlanId { get; set; } = string.Empty;
}

public sealed class CapacitySolveCommand
{
    public string JobId { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public JsonElement Input { get; set; }
    public DateTimeOffset SubmittedAt { get; set; }
    public CapacityTaskResourceLimits Resources { get; set; } = new();
}

public sealed class CapacitySolveJobProgress
{
    public string JobId { get; set; } = string.Empty;
    public int Percent { get; set; }
    public string Message { get; set; } = string.Empty;
}

public sealed class CapacitySolveJobCompletion
{
    public string JobId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Error { get; set; }
    public JsonElement? Result { get; set; }
}

public sealed class CapacitySolveJob
{
    public string JobId { get; set; } = string.Empty;
    public string AgentId { get; set; } = string.Empty;
    public string AgentName { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public string RequestedBy { get; set; } = string.Empty;
    public string Status { get; set; } = "queued";
    public int Progress { get; set; }
    public string ProgressMessage { get; set; } = string.Empty;
    public DateTimeOffset SubmittedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? Error { get; set; }
    public JsonElement? Result { get; set; }
    public string? PresetId { get; set; }
    public string? PresetName { get; set; }
    public string? SourceOperationPlanId { get; set; }
    public string? ResultOperationPlanId { get; set; }
    public string? ResultOperationPlanName { get; set; }
}

public sealed class CapacitySolvePreset
{
    public string PresetId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string AgentId { get; set; } = string.Empty;
    public string ModelId { get; set; } = CapacityAgentProtocol.StationCapacityModelId;
    public StationCapacitySolveSettings Settings { get; set; } = new();
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public sealed class CapacitySolvePresetSaveRequest
{
    public string? PresetId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string AgentId { get; set; } = string.Empty;
    public string ModelId { get; set; } = CapacityAgentProtocol.StationCapacityModelId;
    public StationCapacitySolveSettings Settings { get; set; } = new();
}

public sealed class SaturatedPlanSolveRequest
{
    public string PresetId { get; set; } = string.Empty;
    public string InstanceId { get; set; } = string.Empty;
    public string StationSchemeId { get; set; } = string.Empty;
    public string OperationPlanId { get; set; } = string.Empty;
}

public sealed class StationCapacitySolveInput
{
    public string InstanceId { get; set; } = string.Empty;
    public string StationSchemeId { get; set; } = string.Empty;
    public string OperationPlanId { get; set; } = string.Empty;
    public double HorizonSeconds { get; set; } = 86_400;
    public string MinimumModelVersion { get; set; } = string.Empty;
    public StationCapacitySolveSettings Settings { get; set; } = new();
    public List<StationCapacityRouteInput> Routes { get; set; } = new();
    public List<StationCapacityRouteOccupationInput> RouteOccupations { get; set; } = new();
    public List<StationCapacityTrainInput> Trains { get; set; } = new();
}

public sealed class StationCapacitySolveSettings
{
    public int LeftShiftToleranceSeconds { get; set; } = 50_400;
    public int RightShiftToleranceSeconds { get; set; } = 50_400;
    public int TimeLimitSeconds { get; set; } = 3_600;
    public int ThreadCount { get; set; } = 12;
    public string Objective { get; set; } = "min-end-time";
}

public sealed class StationCapacityRouteInput
{
    public string Id { get; set; } = string.Empty;
    public List<string> CellIds { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    /// <summary>Only set for an input-only dwelling resource; never a persisted station route.</summary>
    public string? TrackId { get; set; }
}

public sealed class StationCapacityRouteOccupationInput
{
    public string RouteId { get; set; } = string.Empty;
    public string TrainTypeId { get; set; } = string.Empty;
    public string CellId { get; set; } = string.Empty;
    public int StartOccupationShiftSeconds { get; set; }
    public int EndOccupationShiftSeconds { get; set; }
}

public sealed class StationCapacityTrainInput
{
    public string Id { get; set; } = string.Empty;
    public string TrainType { get; set; } = string.Empty;
    public List<StationCapacityMovementInput> Movements { get; set; } = new();
    public StationCapacityProcessInput? ProcessConstraints { get; set; }
}

public sealed class StationCapacityProcessInput
{
    public double OriginSeconds { get; set; }
    public List<StationCapacityEventInput> Events { get; set; } = new();
    public List<StationCapacityPrecedenceInput> Precedences { get; set; } = new();
}

public sealed class StationCapacityEventInput
{
    public string Id { get; set; } = string.Empty;
    public double? FixedTimeSeconds { get; set; }
    public double OriginalTimeSeconds { get; set; }
    public List<string> LocationIds { get; set; } = new();
}

public sealed class StationCapacityPrecedenceInput
{
    public string LeadingEventId { get; set; } = string.Empty;
    public string FollowingEventId { get; set; } = string.Empty;
    public double IntervalSeconds { get; set; }
}

public sealed class StationCapacityMovementInput
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Sequence { get; set; }
    public List<string> CandidateRouteIds { get; set; } = new();
    public List<string> RequiredRouteTags { get; set; } = new();
    public double OriginalStartSeconds { get; set; }
    public double OriginalEndSeconds { get; set; }
    public double MinDurationSeconds { get; set; }
    public double MaxDurationSeconds { get; set; } = 86_400;
    public string? StartEventId { get; set; }
    public string? EndEventId { get; set; }
    public Dictionary<string, List<string>> StartLocationIdsByRoute { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, List<string>> EndLocationIdsByRoute { get; set; } = new(StringComparer.Ordinal);
}

public sealed class StationCapacitySolveResult
{
    public string ModelId { get; set; } = CapacityAgentProtocol.StationCapacityModelId;
    public string Solver { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool HasSolution { get; set; }
    public double ObjectiveValue { get; set; }
    public double BestBound { get; set; }
    public double MipGap { get; set; }
    public double SolveTimeSeconds { get; set; }
    public int TrainCount { get; set; }
    public double TotalOccupationTimeSeconds { get; set; }
    public double CapacityValue { get; set; }
    public DateTimeOffset CompletedAt { get; set; }
    public List<StationCapacityTrainResult> Trains { get; set; } = new();
}

public sealed class StationCapacityTrainResult
{
    public string Id { get; set; } = string.Empty;
    public string TrainType { get; set; } = string.Empty;
    public List<StationCapacityMovementResult> Movements { get; set; } = new();
    public Dictionary<string, double> EventTimes { get; set; } = new(StringComparer.Ordinal);
}

public sealed class StationCapacityMovementResult
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string RouteId { get; set; } = string.Empty;
    public string? TrackId { get; set; }
    public double StartSeconds { get; set; }
    public double EndSeconds { get; set; }
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public List<StationCapacityCellOccupationResult> CellOccupations { get; set; } = new();
}

public sealed class StationCapacityCellOccupationResult
{
    public string CellId { get; set; } = string.Empty;
    public double StartSeconds { get; set; }
    public double EndSeconds { get; set; }
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
}
