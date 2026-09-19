using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace SwitchYard.Service.Models;

/// <summary>Scope is always resolved against the authenticated user's capacity instance.</summary>
public class ProcessScope
{
    public string InstanceID { get; set; } = "";
    public string StationSchemeID { get; set; } = "";
    public string OperationPlanID { get; set; } = "default";
}

public sealed class OperationProcessTemplate : ProcessScope
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int Revision { get; set; }
    public List<ProcessActivity> Activities { get; set; } = new();
    public List<ProcessEvent> Events { get; set; } = new();
    public List<ProcessPrecedence> Precedences { get; set; } = new();
    public List<ProcessAnchor> Anchors { get; set; } = new();
    public List<ProcessRouteAnchors> RouteAnchors { get; set; } = new();
}

/// <summary>Movement types use RouteList; Dwelling uses TrackList. All durations are minutes.</summary>
public sealed class ProcessActivity
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Type { get; set; } = "Dwelling";
    public double MinDuration { get; set; }
    public double MaxDuration { get; set; }
    public string StartEvent { get; set; } = "";
    public string EndEvent { get; set; } = "";
    public List<string> RouteList { get; set; } = new();
    public string? SelectedRoute { get; set; }
    public List<string> TrackList { get; set; } = new();
    public string? SelectedTrack { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
}

public sealed class ProcessEvent
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>Minutes relative to the template origin; null means not scheduled.</summary>
    public double? Time { get; set; }
    public string? NodeID { get; set; }
    /// <summary>Read-only candidates derived from the owning activities' alternative route endpoints.</summary>
    [ValidateNever]
    public List<string> NodeList { get; set; } = new();
    public List<string> AnchorList { get; set; } = new();
    public string? SelectedAnchor { get; set; }
}

public sealed class ProcessPrecedence
{
    public string Id { get; set; } = "";
    public string LeadingEvent { get; set; } = "";
    public string FollowingEvent { get; set; } = "";
    /// <summary>Minimum separation in minutes.</summary>
    public double Interval { get; set; }

    // Accept the spelling in the original requirements, but consistently return interval.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Intervel { get => null; set { if (value.HasValue) Interval = value.Value; } }
}

public sealed class ProcessAnchor
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string TrackID { get; set; } = "";
}

public sealed class ProcessRouteAnchors
{
    public string RouteID { get; set; } = "";
    public string? StartAnchor { get; set; }
    public string? EndAnchor { get; set; }
}

public sealed class ProcessCatalog
{
    public List<ProcessCatalogNode> Nodes { get; set; } = new();
    public List<ProcessCatalogTrack> Tracks { get; set; } = new();
    public List<ProcessCatalogRoute> Routes { get; set; } = new();
}

public sealed class ProcessCatalogNode
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
}

/// <summary>A Track is an existing station link; no duplicate physical track records are created.</summary>
public sealed class ProcessCatalogTrack
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string FromNodeID { get; set; } = "";
    public string ToNodeID { get; set; } = "";
}

public sealed class ProcessCatalogRoute
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string? StartNodeID { get; set; }
    public string? EndNodeID { get; set; }
    public List<string> TrackIDs { get; set; } = new();
}
