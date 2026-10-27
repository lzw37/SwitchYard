using System.Text.Json.Serialization;

namespace SwitchYard.SketchLayout;

public record Point(double X, double Y);
public sealed class Facility
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "polygon";
    public List<Point> Vertices { get; set; } = [];
    public Point Center { get; set; } = new(0, 0);
    public double Radius { get; set; } = 24;
    public string? FillColor { get; set; }
    public string? StrokeColor { get; set; }
}
public sealed class ConnectionPort
{
    public string FacilityId { get; set; } = "";
    public string Kind { get; set; } = "center";
    public int Index { get; set; }
}
public sealed class LogisticsLink
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public ConnectionPort From { get; set; } = new();
    public ConnectionPort To { get; set; } = new();
    // Missing direction in older schemes retains their bidirectional behavior.
    public string Direction { get; set; } = "both";
    public string? Color { get; set; }
    public bool AllowsTravel(string from, string to) =>
        (Direction is "forward" or "both" && From.FacilityId == from && To.FacilityId == to) ||
        (Direction is "reverse" or "both" && To.FacilityId == from && From.FacilityId == to);
    // Anchors are polyline corners; rounded display replaces corners with tangent circular arcs.
    public List<Point> Anchors { get; set; } = [];
}
public sealed class ProcessStep
{
    public string FacilityId { get; set; } = "";
    public double DwellSeconds { get; set; } = 30;
    public string? IncomingLinkId { get; set; }
    public double TravelSeconds { get; set; }
}
public sealed class ProcessChain
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    // Read old documents only. The parent plan consumes and removes this value.
    [JsonPropertyName("quantity"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? LegacyQuantity { get; set; }
    public List<ProcessStep> Steps { get; set; } = [];
}
public sealed class ProcessPlan : IJsonOnDeserialized
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    private int? quantity;
    // One quantity applies to every parallel chain and every step in this plan.
    public int Quantity { get => quantity ?? Chains?.FirstOrDefault()?.LegacyQuantity ?? 1; set => quantity = value; }
    public void NormalizeQuantity()
    {
        Quantity = Quantity;
        if (Chains != null) foreach (var chain in Chains) if (chain != null) chain.LegacyQuantity = null;
    }
    void IJsonOnDeserialized.OnDeserialized() => NormalizeQuantity();
    // Chains are independent peers in one analysis window, not successive stages.
    // The default preserves documents saved before the mode was explicit.
    public string ExecutionMode { get; set; } = "parallel";
    // Read legacy per-plan settings; new settings belong to the whole layout.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public double? HorizonSeconds { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public double? AThreshold { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public double? EThreshold { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public double? IThreshold { get; set; }
    public List<ProcessChain> Chains { get; set; } = [];
    public SketchAnalysisSettings LegacyAnalysisSettings() => new()
    {
        HorizonSeconds = HorizonSeconds ?? 3600, AThreshold = AThreshold ?? .8,
        EThreshold = EThreshold ?? .5, IThreshold = IThreshold ?? .2
    };
}
public sealed record SketchAnalysisSettings
{
    public double HorizonSeconds { get; set; } = 3600;
    public double AThreshold { get; set; } = .8;
    public double EThreshold { get; set; } = .5;
    public double IThreshold { get; set; } = .2;
    public string CrossingModel { get; set; } = "poisson-fixed";
}
public sealed class SketchGridSettings
{
    public bool ShowGrid { get; set; } = true;
    public bool SnapToGrid { get; set; } = true;
    public double Spacing { get; set; } = 20;
    public double OriginX { get; set; }
    public double OriginY { get; set; }
}
public sealed class SketchLayoutScheme
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public List<Facility> Facilities { get; set; } = [];
    public List<LogisticsLink> Links { get; set; } = [];
    public List<ProcessPlan> Plans { get; set; } = [];
    public SketchAnalysisSettings? AnalysisSettings { get; set; }
    public SketchAnalysisSettings ResolveAnalysisSettings()
    {
        if (AnalysisSettings != null) return AnalysisSettings;
        // Preserve a common legacy configuration; conflicting plans use layout defaults.
        // Never pick settings according to the selected plan or list order.
        var legacy = Plans.Select(p => p.LegacyAnalysisSettings()).Distinct().ToList();
        return legacy.Count == 1 ? legacy[0] : new();
    }
    public SketchGridSettings? GridSettings { get; set; }
    public bool ShowLinkNames { get; set; }
    public bool ShowCurveArc { get; set; } = true;
    public bool ShowPolygonFillets { get; set; } = true;
    public List<RouteCrossing> Crossings { get; set; } = [];
    public bool ShowCrossings { get; set; } = true;
}
public sealed class RouteCrossing
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public Point Center { get; set; } = new(0, 0);
    public double Radius { get; set; } = 24;
    public List<CrossingSourceLink> SourceLinks { get; set; } = [];
}
public sealed class CrossingSourceLink
{
    public string LinkId { get; set; } = "";
    public string Name { get; set; } = "";
    // Null uses the traffic-weighted link travel duration; exponential models use this as the mean.
    public double? OccupancySeconds { get; set; }
}
public sealed class SchemeWriteRequest
{
    public int Revision { get; set; }
    public SketchLayoutScheme Document { get; set; } = new();
}
public sealed record SchemeSummary(string Id, string OwnerId, string OwnerName, string Name, string Description, int Revision, string CreatedAt, string UpdatedAt);
public sealed record SchemeDetail(string Id, string OwnerId, string OwnerName, int Revision, string CreatedAt, string UpdatedAt, SketchLayoutScheme Document);
public sealed record FlowRelation(string FromId, string ToId, long Forward, long Reverse, long Total, double Intensity, string Grade);
public sealed record ConflictResource(string Kind, string FirstId, string SecondId, string Name);
public sealed record ProcessConflict(string FirstPlanId, string FirstChainId, string SecondPlanId, string SecondChainId, double PairProbability, long JobPairs, double ExpectedConflictingPairs, List<ConflictResource> Resources);
public sealed record CrossingRouteRisk(string LinkId, string Name, bool MissingLink, long Passages, double RatePerHour, double? OccupancySeconds, bool UsesTravelTime, double? ConflictProbability);
public sealed record CrossingQueueMetrics(string Model, string Status, double? LoadRatio, double? MeanDelaySeconds, double? MeanQueueLength, int SimulationRuns = 0, double? DelayStandardErrorSeconds = null, double? QueueLengthStandardError = null);
public sealed record CrossingRisk(string CrossingId, string Name, string Model, string Status, long Passages, double? ConflictProbability, List<CrossingRouteRisk> Routes, bool CorrelatedRoutes)
{
    public CrossingQueueMetrics? Queue { get; init; }
}
public sealed record AnalysisResult(string Scope, int PlanCount, int ChainCount, long JobCount, double HorizonSeconds, string Model, List<FlowRelation> Relations, List<ProcessConflict> Conflicts, double ExpectedConflictingPairs, List<string> Warnings)
{
    public List<CrossingRisk> CrossingRisks { get; init; } = [];
}
