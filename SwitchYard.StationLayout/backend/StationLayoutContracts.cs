namespace SwitchYard.StationLayout;

public sealed class StationLayoutSaveRequest
{
    public string Json { get; set; } = string.Empty;
    public string? InstanceID { get; set; }
    public string? StationSchemeID { get; set; }
    public long? ExpectedRevision { get; set; }
}

public sealed class StationSchemeCreateRequest
{
    public string InstanceID { get; set; } = string.Empty;
    public string? Name { get; set; }
}

public sealed class StationSchemeUpdateRequest
{
    public string InstanceID { get; set; } = string.Empty;
    public string OriginalID { get; set; } = string.Empty;
    public string? Name { get; set; }
}

public sealed class StationRouteSearchRequest
{
    public string? InstanceID { get; set; }
    public string? StationSchemeID { get; set; }
    public int StartNodeId { get; set; }
    public int EndNodeId { get; set; }
}

public sealed record StationSchemeDto(string ID, string Name, long Revision = 0);

public sealed record StationLayoutSaveResult(
    string Message,
    string InstanceID,
    string StationSchemeID,
    long Revision,
    int NodeCount,
    int LinkCount,
    int CurveCount,
    int SignalCount,
    int InsulationJointCount,
    int BufferStopCount,
    int PlatformCount,
    int SwitchCount,
    int SwitchBranchVectorCount,
    int CellCount,
    int AnnotationCount);

public sealed class StationRouteSearchResponse
{
    public string InstanceID { get; set; } = string.Empty;
    public string StationSchemeID { get; set; } = string.Empty;
    public int StartNodeId { get; set; }
    public int EndNodeId { get; set; }
    public List<StationRouteSearchResult> Routes { get; set; } = [];
}

public sealed class StationRouteSearchResult
{
    public string Direction { get; set; } = string.Empty;
    public List<int> NodeIds { get; set; } = [];
    public List<int> LinkIds { get; set; } = [];
    public List<string> SwitchIds { get; set; } = [];
    public List<string> CellIds { get; set; } = [];
    public List<string> SignalIds { get; set; } = [];
    public List<StationRouteNode> Nodes { get; set; } = [];
    public List<StationRouteLink> Links { get; set; } = [];
    public List<StationLayoutSwitch> Switches { get; set; } = [];
    public List<StationLayoutCell> Cells { get; set; } = [];
    public List<StationLayoutSignal> Signals { get; set; } = [];
}

public sealed record StationRouteNode(int ID, double X, double Y);

public sealed record StationRouteLink(
    int ID,
    string? Name,
    string? ArrowDirection,
    string? ArrowType,
    int FromNodeID,
    int ToNodeID);

public sealed record StationLayoutDwgResult(
    string Message,
    int SegmentCount,
    StationLayoutDocument Layout);
