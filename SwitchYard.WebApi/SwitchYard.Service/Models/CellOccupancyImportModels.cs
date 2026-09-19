namespace SwitchYard.Service.Models;

public sealed class CellOccupancyImportRequest
{
    public string InstanceID { get; set; } = "";
    public string StationSchemeID { get; set; } = "";
    public string OperationPlanID { get; set; } = "";
    public string CsvText { get; set; } = "";
    public string FileName { get; set; } = "";
    public bool CreateNewPlan { get; set; } = true;
    public string PlanName { get; set; } = "CellOccupancy导入";
    public Dictionary<string, string> CellMappings { get; set; } = new();
    public string? PreviewToken { get; set; }
}

public sealed class CellOccupancyImportPreview
{
    public bool Valid { get; set; }
    public int TrainCount { get; set; }
    public int MovementCount { get; set; }
    public int OccupancyCount { get; set; }
    public int NewRouteCount { get; set; }
    public int ReusedRouteCount { get; set; }
    public List<string> Warnings { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public List<CellOccupancyCellMatch> CellMatches { get; set; } = new();
    public List<CellOccupancyAvailableCell> AvailableCells { get; set; } = new();
    public List<CellOccupancyRouteMatch> RouteMatches { get; set; } = new();
    public string PreviewToken { get; set; } = "";
}

public sealed class CellOccupancyCellMatch
{
    public string SourceCell { get; set; } = "";
    public string? CellID { get; set; }
    public string? CellName { get; set; }
    public string Method { get; set; } = "";
    public bool NeedsSelection { get; set; }
}

public sealed class CellOccupancyAvailableCell
{
    public string ID { get; set; } = "";
    public string Name { get; set; } = "";
}

public sealed class CellOccupancyRouteMatch
{
    public string MovementType { get; set; } = "";
    public string RouteID { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsNew { get; set; }
    public int MovementCount { get; set; }
}

/// <summary>A read-only station snapshot. The builder never accesses a database.</summary>
public sealed class CellOccupancyImportCatalog
{
    public List<CellOccupancyCell> Cells { get; set; } = new();
    public List<CellOccupancyLink> Links { get; set; } = new();
    public List<CellOccupancyNode> Nodes { get; set; } = new();
    public List<CellOccupancyBoundItem> Switches { get; set; } = new();
    public List<CellOccupancyBoundItem> Signals { get; set; } = new();
    public List<CellOccupancyBoundItem> RouteEnds { get; set; } = new();
    public List<CellOccupancyRoute> Routes { get; set; } = new();
    public List<CellOccupancyRouteTime> RouteTimes { get; set; } = new();
}

public sealed class CellOccupancyCell
{
    public string ID { get; set; } = "";
    public string Name { get; set; } = "";
    public string LinkIDList { get; set; } = "";
}

public sealed class CellOccupancyLink
{
    public string ID { get; set; } = "";
    public string Name { get; set; } = "";
    public string FromNodeID { get; set; } = "";
    public string ToNodeID { get; set; } = "";
}

public sealed class CellOccupancyNode
{
    public string ID { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
}

public sealed class CellOccupancyBoundItem
{
    public string ID { get; set; } = "";
    public string Name { get; set; } = "";
    public string BindingNodeID { get; set; } = "";
}

public sealed class CellOccupancyRoute
{
    public string ID { get; set; } = "";
    public string Type { get; set; } = "";
    public string Description { get; set; } = "";
    public string NodeList { get; set; } = "[]";
    public string LinkList { get; set; } = "[]";
    public string SwitchList { get; set; } = "[]";
    public string CellList { get; set; } = "[]";
    public string SignalList { get; set; } = "[]";
    public string InterruptCellList { get; set; } = "[]";
    public string AllowanceTags { get; set; } = "[]";
    public string ForbiddenTags { get; set; } = "[]";
    public string StartNodeID { get; set; } = "";
    public string EndNodeID { get; set; } = "";
}

public sealed class CellOccupancyRouteTime
{
    public string RouteID { get; set; } = "";
    public string TrainTypeID { get; set; } = "";
    public string CellID { get; set; } = "";
    public int StartOccupationShift { get; set; }
    public int EndOccupationShift { get; set; }
}

public sealed class CellOccupancyTrainDraft
{
    public string ID { get; set; } = "";
    public string TrainNumber { get; set; } = "";
    public string Name { get; set; } = "";
    public string TrainType { get; set; } = "旅客列车";
    public int IsFixedOperation { get; set; }
}

public sealed class CellOccupancyMovementDraft
{
    public string TrainID { get; set; } = "";
    public string MovementID { get; set; } = "";
    public string Name { get; set; } = "";
    public string RouteIDList { get; set; } = "";
    public int MinDuration { get; set; }
    public string EarliestStartTime { get; set; } = "";
    public string LatestEndTime { get; set; } = "";
    public string Route { get; set; } = "";
    public string Tag { get; set; } = "";
    public int SortOrder { get; set; }
}

public sealed class CellOccupancyImportResult
{
    public CellOccupancyImportPreview Preview { get; set; } = new();
    public List<CellOccupancyRoute> NewRoutes { get; set; } = new();
    public List<CellOccupancyRouteTime> NewRouteTimes { get; set; } = new();
    public List<CellOccupancyTrainDraft> Trains { get; set; } = new();
    public List<CellOccupancyMovementDraft> Movements { get; set; } = new();
}
