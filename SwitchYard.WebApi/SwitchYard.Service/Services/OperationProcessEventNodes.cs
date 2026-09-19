using SwitchYard.Service.Models;

namespace SwitchYard.Service.Services;

/// <summary>Node candidates are projected from current station routes, never from submitted or stored NodeList values.</summary>
public static class OperationProcessEventNodes
{
    public static void Refresh(OperationProcessTemplate template, ProcessCatalog catalog)
    {
        if (template.Events is null) return;
        var validNodes = catalog.Nodes.Select(node => node.Id).ToHashSet(StringComparer.Ordinal);
        var routes = catalog.Routes.GroupBy(route => route.Id)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var nodesByEvent = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var seenByEvent = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var ev in template.Events)
        {
            if (ev is null) continue;
            ev.NodeList = new();
            if (string.IsNullOrWhiteSpace(ev.Id) || nodesByEvent.ContainsKey(ev.Id)) continue;
            nodesByEvent[ev.Id] = new();
            seenByEvent[ev.Id] = new(StringComparer.Ordinal);
        }

        foreach (var activity in template.Activities ?? Enumerable.Empty<ProcessActivity>())
        {
            if (activity is null || activity.Type == "Dwelling" || activity.RouteList is null) continue;
            foreach (var routeID in activity.RouteList)
            {
                if (routeID is null || !routes.TryGetValue(routeID, out var route) ||
                    !string.Equals(route.Type, activity.Type, StringComparison.OrdinalIgnoreCase)) continue;
                AddCandidate(activity.StartEvent, route.StartNodeID);
                AddCandidate(activity.EndEvent, route.EndNodeID);
            }
        }
        foreach (var ev in template.Events)
            if (ev is not null && ev.Id is not null && nodesByEvent.TryGetValue(ev.Id, out var candidates))
                ev.NodeList = new(candidates);

        void AddCandidate(string? eventID, string? nodeID)
        {
            if (eventID is null || nodeID is null || !validNodes.Contains(nodeID) ||
                !nodesByEvent.TryGetValue(eventID, out var candidates)) return;
            if (seenByEvent[eventID].Add(nodeID)) candidates.Add(nodeID);
        }
    }
}
