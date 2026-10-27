namespace SwitchYard.Capacity;

/// <summary>
/// A saved process from the station-scheme editor library. Scope is supplied by the enclosing solve input.
/// Times, durations and intervals remain in minutes, as in the editor.
/// </summary>
public sealed class StationCapacityOperationProcessInput
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Revision { get; set; }
    public List<StationCapacityProcessActivityInput> Activities { get; set; } = new();
    public List<StationCapacityProcessEventInput> Events { get; set; } = new();
    public List<StationCapacityProcessPrecedenceInput> Precedences { get; set; } = new();
    public List<StationCapacityProcessAnchorInput> Anchors { get; set; } = new();
    public List<StationCapacityProcessRouteAnchorsInput> RouteAnchors { get; set; } = new();
}

public sealed class StationCapacityProcessActivityInput
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public double MinDuration { get; set; }
    public double MaxDuration { get; set; }
    public string StartEvent { get; set; } = string.Empty;
    public string EndEvent { get; set; } = string.Empty;
    public List<string> RouteList { get; set; } = new();
    public string? SelectedRoute { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
}

public sealed class StationCapacityProcessEventInput
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public double? Time { get; set; }
    public string? NodeId { get; set; }
    public List<string> NodeList { get; set; } = new();
    public List<string> AnchorList { get; set; } = new();
    public string? SelectedAnchor { get; set; }
    public List<string> AutomaticAnchorIds { get; set; } = new();
}

public sealed class StationCapacityProcessPrecedenceInput
{
    public string Id { get; set; } = string.Empty;
    public string LeadingEvent { get; set; } = string.Empty;
    public string FollowingEvent { get; set; } = string.Empty;
    public double Interval { get; set; }
    public bool IsExact { get; set; }
}

public sealed class StationCapacityProcessAnchorInput
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? DwellingActivityId { get; set; }
    public List<string> RouteIds { get; set; } = new();
}

public sealed class StationCapacityProcessRouteAnchorsInput
{
    public string RouteId { get; set; } = string.Empty;
    public string? StartAnchor { get; set; }
    public string? EndAnchor { get; set; }
}
