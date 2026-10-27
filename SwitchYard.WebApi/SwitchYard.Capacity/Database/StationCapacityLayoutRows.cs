namespace SwitchYard.Capacity;

// Database query projections used when preparing station layout communication records.
public sealed class StationCapacityLayoutCellRow
{
    public string? Id { get; set; }
    public string? LinkIdList { get; set; }
    public string? Name { get; set; }
    public int? LayoutOrder { get; set; }
}

public sealed class StationCapacityLayoutRouteRow
{
    public string? Id { get; set; }
    public string? Type { get; set; }
    public string? TrainTypes { get; set; }
    public string? Description { get; set; }
    public string? NodeList { get; set; }
    public string? LinkList { get; set; }
    public string? SwitchList { get; set; }
    public string? CellList { get; set; }
    public string? InterruptCellList { get; set; }
    public string? SignalList { get; set; }
    public string? AllowanceTags { get; set; }
    public string? ForbiddenTags { get; set; }
    public string? StartNodeId { get; set; }
    public string? EndNodeId { get; set; }
}

public sealed class StationCapacityLayoutRouteOperationTimeRow
{
    public string? CalculationParameterSetId { get; set; }
    public string? RouteId { get; set; }
    public string? TrainTypeId { get; set; }
    public double OperationTimeS { get; set; }
}

public sealed class StationCapacityLayoutLinkRow
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? FromNodeId { get; set; }
    public string? ToNodeId { get; set; }
    public string? ArrowDirection { get; set; }
    public string? ArrowType { get; set; }
    public double? X1 { get; set; }
    public double? Y1 { get; set; }
    public double? X2 { get; set; }
    public double? Y2 { get; set; }
    public int? LayoutOrder { get; set; }
}

public sealed class StationCapacityLayoutNodeRow
{
    public string? Id { get; set; }
    public double? X { get; set; }
    public double? ActualX { get; set; }
    public double? Y { get; set; }
    public int? LayoutOrder { get; set; }
}
