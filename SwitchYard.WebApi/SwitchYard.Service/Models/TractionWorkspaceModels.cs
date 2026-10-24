using System.Text.Json.Serialization;

namespace SwitchYard.Service.Models;

public sealed record TractionScope(string InstanceID, string StationSchemeID, string CalculationSetID = "default", string RouteCaseID = "");
public sealed record TractionCalculationSet
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public long Revision { get; init; }
    public string CreatedAtUtc { get; init; } = "";
    public string UpdatedAtUtc { get; init; } = "";
    public int RouteCount { get; init; }
    public int CalculatedCount { get; init; }
    public int CurrentCount { get; init; }
    public string? SelectedRouteId { get; init; }
    public string? TrainName { get; init; }
    public string? SavedAtUtc { get; init; }
    public double? DurationS { get; init; }
    public bool IsCurrent { get; init; }
}
public sealed record TractionCalculationSetCatalog(string SelectedId, IReadOnlyList<TractionCalculationSet> Sets);

public sealed record TractionRouteCase
{
    public string Id { get; init; } = "";
    public string RouteId { get; init; } = "";
    public string ContinuationId { get; init; } = "";
    public bool IsSelected { get; init; }
    public long Revision { get; init; }
    public string? TrainName { get; init; }
    public string? SavedAtUtc { get; init; }
    public double? DurationS { get; init; }
    public bool IsCurrent { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TractionWorkspace
{
    [JsonRequired] public long Revision { get; init; }
    [JsonRequired] public TractionTrainPresetCatalog Catalog { get; init; } = null!;
    [JsonRequired] public TractionWorkspaceSettings Settings { get; init; } = null!;
    public IReadOnlyList<TractionSpeedLimitPoint> SpeedLimitPoints { get; init; } = [];
    [JsonRequired] public IReadOnlyList<TractionSegmentOverride> Overrides { get; init; } = [];
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TractionTrainPresetCatalog
{
    [JsonRequired] public int Version { get; init; } = 1;
    [JsonRequired] public string SelectedId { get; init; } = null!;
    [JsonRequired] public IReadOnlyList<TractionTrainPreset> Presets { get; init; } = null!;
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TractionTrainPreset
{
    [JsonRequired] public string Id { get; init; } = null!;
    [JsonRequired] public string Name { get; init; } = null!;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? NameEn { get; init; }
    [JsonRequired] public SpeedProfileTrainInput Parameters { get; init; } = null!;
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TractionWorkspaceSettings
{
    [JsonRequired] public string SelectedRouteId { get; init; } = "";
    public string ContinuationId { get; init; } = "";
    [JsonRequired] public SpeedProfileSignalInput Signal { get; init; } = null!;
    [JsonRequired] public SpeedProfileOptionsInput Run { get; init; } = null!;
    [JsonRequired] public string DistanceMode { get; init; } = null!;
    [JsonRequired] public double MetersPerUnit { get; init; }
    [JsonRequired] public double DefaultLimit { get; init; }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TractionSegmentOverride
{
    [JsonRequired] public string RouteId { get; init; } = null!;
    [JsonRequired] public string RouteStartNodeId { get; init; } = null!;
    [JsonRequired] public string RouteEndNodeId { get; init; } = null!;
    public string ContinuationId { get; init; } = "";
    [JsonRequired] public int SegmentIndex { get; init; }
    [JsonRequired] public string SegmentId { get; init; } = null!;
    public double? SpeedLimitKmh { get; init; }
    public double? GradientPermille { get; init; }
    public double? AdditionalResistanceKn { get; init; }
}
public sealed record StoredTractionCalculation
{
    public long Revision { get; init; }
    public long CurrentWorkspaceRevision { get; init; }
    public bool IsCurrent => Revision == CurrentWorkspaceRevision;
    public DateTime SavedAtUtc { get; init; }
    public string SelectedRouteId { get; init; } = "";
    public string ContinuationId { get; init; } = "";
    public required SpeedProfileCalculationRequest Request { get; init; }
    public required SpeedProfileCalculationResponse Result { get; init; }
}
public sealed class TractionRevisionConflictException : Exception
{
    public long CurrentRevision { get; }
    public TractionRevisionConflictException(long currentRevision)
        : base("The traction workspace changed. Reload it before saving or calculating.") => CurrentRevision = currentRevision;
}



[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TractionSpeedLimitPoint
{
    [JsonRequired] public double PositionM { get; init; }
    [JsonRequired] public double SpeedLimitKmh { get; init; }
}
