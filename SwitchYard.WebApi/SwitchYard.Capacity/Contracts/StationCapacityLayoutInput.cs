using System.Text.Json.Serialization;

namespace SwitchYard.Capacity;

/// <summary>
/// Layout data and effective parameter sets for one station scheme.
/// Raw lists and nulls are retained separately from the solver projections.
/// </summary>
public sealed class StationCapacityLayoutInput
{
    public string StationSchemeId { get; set; } = string.Empty;
    public List<StationCapacityLayoutCellInput> Cells { get; set; } = new();
    public List<StationCapacityLayoutRouteInput> Routes { get; set; } = new();
    public List<StationCapacityLayoutRouteOperationTimeInput> RouteOperationTimes { get; set; } = new();
    public StationCapacityLayoutParameterSetsInput CalculationParameterSets { get; set; } = new();
    public List<StationCapacityLayoutLinkInput> Links { get; set; } = new();
    public List<StationCapacityLayoutNodeInput> Nodes { get; set; } = new();
}

public sealed class StationCapacityLayoutRouteOccupationInput
{
    public string? RouteId { get; set; }
    public string? TrainTypeId { get; set; }
    public string? CellId { get; set; }
    public int? StartOccupationShift { get; set; }
    public int? EndOccupationShift { get; set; }
}

[JsonConverter(typeof(StationCapacityLayoutParameterSetsInputConverter))]
public sealed class StationCapacityLayoutParameterSetsInput
{
    public List<StationCapacityLayoutParameterSetInput> Sets { get; set; } = new();
    public Dictionary<string, List<StationCapacityLayoutRouteOccupationInput>> RouteOccupations { get; set; } = new(StringComparer.Ordinal);
}

public sealed class StationCapacityLayoutParameterSetInput
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
}

