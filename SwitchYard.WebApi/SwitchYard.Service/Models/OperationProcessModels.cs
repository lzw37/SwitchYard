using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace SwitchYard.Service.Models;

/// <summary>Scope is always resolved against the authenticated user's capacity instance.</summary>
public class ProcessScope
{
    public string InstanceID { get; set; } = "";
    public string StationSchemeID { get; set; } = "";
    // Library endpoints ignore this compatibility field; execution snapshots keep their plan ID.
    public string OperationPlanID { get; set; } = "";
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

/// <summary>All activity types and anchors reference routes. Durations are minutes.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
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
    /// <summary>References maintained by dwelling ownership and direct event connections in the editor.</summary>
    public List<string> AutomaticAnchorIDs { get; set; } = new();
}

public sealed class ProcessPrecedence
{
    public string Id { get; set; } = "";
    public string LeadingEvent { get; set; } = "";
    public string FollowingEvent { get; set; } = "";
    /// <summary>Separation in minutes: a lower bound unless IsExact is true.</summary>
    public double Interval { get; set; }
    public bool IsExact { get; set; }

    // Accept the spelling in the original requirements, but consistently return interval.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Intervel { get => null; set { if (value.HasValue) Interval = value.Value; } }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class ProcessAnchor
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DwellingActivityID { get; set; }
    private List<string> routeIDs = new();
    private bool routeIDsSpecified;
    /// <summary>Alternative dwelling routes whose endpoints define this anchor's locations.</summary>
    public List<string> RouteIDs
    {
        get => routeIDs;
        set { routeIDs = value; routeIDsSpecified = true; }
    }

    // Read old templates and execution snapshots; always write the collection.
    // An explicit routeIDs array takes precedence regardless of JSON property order.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RouteID
    {
        get => null;
        set { if (!routeIDsSpecified) routeIDs = string.IsNullOrEmpty(value) ? new() : new() { value }; }
    }
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
    public List<ProcessCatalogRoute> Routes { get; set; } = new();
}

public sealed class ProcessCatalogNode
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
}

public sealed class ProcessCatalogRoute
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string? StartNodeID { get; set; }
    public string? EndNodeID { get; set; }
}
