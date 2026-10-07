using System.Text.Json;
using SwitchYard.Capacity;
using SwitchYard.Service.Models;

namespace SwitchYard.Service.Services;

/// <summary>Physical event choices shared by initial generation and the capacity solver.</summary>
public static class OperationProcessResourceLocations
{
    public static bool AnchorAtNode(ProcessAnchor anchor, string? node, IReadOnlyDictionary<string, ProcessCatalogRoute> routes) =>
        node is not null && anchor.RouteIDs is not null && anchor.RouteIDs.Any(id => id is not null &&
            routes.TryGetValue(id, out var route) && route.Type == "Dwelling" &&
            (route.StartNodeID == node || route.EndNodeID == node));

    public static List<string> Get(OperationProcessTemplate source, ProcessCatalog catalog,
        ProcessActivity activity, string resourceID, bool start)
    {
        var ev = source.Events.First(item => item.Id == (start ? activity.StartEvent : activity.EndEvent));
        var allowedAnchors = ev.SelectedAnchor is not null ? new[] { ev.SelectedAnchor } : ev.AnchorList.ToArray();
        var routes = catalog.Routes.ToDictionary(route => route.Id, StringComparer.Ordinal);
        var anchors = source.Anchors.ToDictionary(anchor => anchor.Id, StringComparer.Ordinal);
        var result = new List<string>();
        var route = catalog.Routes.FirstOrDefault(item => item.Id == resourceID);
        if (route is null) return result;
        var binding = source.RouteAnchors.FirstOrDefault(item => item.RouteID == resourceID);
        if (activity.Type == "Dwelling")
        {
            // Dwelling occupies a track; either end can meet the preceding or following route.
            // Keep each anchor attached to its physical endpoint when the route is reversed.
            Add(route.StartNodeID, binding?.StartAnchor);
            Add(route.EndNodeID, binding?.EndAnchor);
        }
        else Add(start ? route.StartNodeID : route.EndNodeID, start ? binding?.StartAnchor : binding?.EndAnchor);
        return result.Distinct(StringComparer.Ordinal).ToList();

        void Add(string? node, string? boundAnchor)
        {
            if (ev.NodeID is not null && ev.NodeID != node) return;
            if (ev.NodeList.Count > 0 && (node is null || !ev.NodeList.Contains(node, StringComparer.Ordinal))) return;
            if (node is null)
            {
                // An unknown endpoint never silently matches a known physical node.
                if (allowedAnchors.Length == 0 && boundAnchor is null) result.Add(Token(null, null, null));
                return;
            }
            if (boundAnchor is not null)
            {
                if ((allowedAnchors.Length == 0 || allowedAnchors.Contains(boundAnchor, StringComparer.Ordinal)) && AtNode(boundAnchor))
                    AddAnchor(boundAnchor);
                return;
            }
            if (allowedAnchors.Length > 0)
            {
                foreach (var id in allowedAnchors.Where(AtNode)) AddAnchor(id);
                return;
            }
            result.Add(Token(node, null, null));
            // An unspecified route anchor may agree with an explicit anchor of another
            // activity at this event. A dwelling activity must remain on its own track.
            foreach (var id in anchors.Keys.Where(AtNode)) AddAnchor(id);

            bool AtNode(string id) => anchors.TryGetValue(id, out var anchor) &&
                AnchorAtNode(anchor, node, routes);

            void AddAnchor(string id)
            {
                foreach (var dwellingID in anchors[id].RouteIDs)
                    if (routes.TryGetValue(dwellingID, out var dwelling) && dwelling.Type == "Dwelling" &&
                        (dwelling.StartNodeID == node || dwelling.EndNodeID == node) &&
                        (activity.Type != "Dwelling" || dwellingID == resourceID))
                        result.Add(Token(node, id, dwellingID));
            }
        }
    }

    // Node identity still matters for a shared event. Across different events only the
    // chosen anchor's dwelling route must agree, including that route's opposite ends.
    private static string Token(string? node, string? anchor, string? route) => JsonSerializer.Serialize(new[] { node, anchor, route });
    public static bool HasNode(string token) => JsonSerializer.Deserialize<string?[]>(token)![0] is not null;
    public static StationCapacityAnchorChoiceInput? AnchorChoice(string token)
    {
        var parts = JsonSerializer.Deserialize<string?[]>(token)!;
        return parts[1] is { } anchor && parts[2] is { } route
            ? new() { AnchorId = anchor, RouteId = route } : null;
    }

    public static Dictionary<string, HashSet<string>> StandaloneEvents(OperationProcessTemplate source, ProcessCatalog catalog)
    {
        var used = source.Activities.SelectMany(a => new[] { a.StartEvent, a.EndEvent }).ToHashSet(StringComparer.Ordinal);
        var result = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var ev in source.Events.Where(ev => !used.Contains(ev.Id)))
        {
            var anchors = ev.SelectedAnchor is not null ? new[] { ev.SelectedAnchor } : ev.AnchorList.ToArray();
            if (anchors.Length == 0) continue;
            result[ev.Id] = source.Anchors.Where(a => anchors.Contains(a.Id, StringComparer.Ordinal))
                .SelectMany(a => catalog.Routes.Where(r => a.RouteIDs.Contains(r.Id) && r.Type == "Dwelling")
                    .SelectMany(r => new[] { r.StartNodeID, r.EndNodeID }
                        .Where(node => node is not null && (ev.NodeID is null || ev.NodeID == node))
                        .Select(node => Token(node, a.Id, r.Id))))
                .ToHashSet(StringComparer.Ordinal);
        }
        return result;
    }

    /// <summary>Candidate anchors are alternatives, not a transitive union of event groups.</summary>
    public static bool Compatible(IReadOnlyDictionary<string, HashSet<string>> events,
        IReadOnlyDictionary<string, HashSet<string>> ownedAnchors)
    {
        if (events.Values.Any(values => values.Count == 0) || ownedAnchors.Values.Any(values => values.Count == 0)) return false;
        var domains = ownedAnchors.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var constraints = events.Values.Select(values => values.Select(AnchorChoice).ToList())
            .Where(values => values.All(choice => choice is not null))
            .Select(values => values.Select(choice => (choice!.AnchorId, choice.RouteId)).Distinct().ToList()).ToList();
        var work = 100000;
        return Search(constraints, domains);

        bool Search(List<List<(string Anchor, string Route)>> remaining, Dictionary<string, HashSet<string>> choices)
        {
            if (--work < 0) throw new ArgumentException("锚的备选地点组合较复杂，请缩小备选范围后重试。");
            // Propagate constraints that require one anchor before branching on alternatives.
            bool changed;
            do
            {
                changed = false;
                foreach (var constraint in remaining)
                {
                    constraint.RemoveAll(option => choices.TryGetValue(option.Anchor, out var routes) && !routes.Contains(option.Route));
                    if (constraint.Count == 0) return false;
                    var anchor = constraint[0].Anchor;
                    if (constraint.Any(option => option.Anchor != anchor)) continue;
                    var routes = constraint.Select(option => option.Route).ToHashSet(StringComparer.Ordinal);
                    if (!choices.TryGetValue(anchor, out var old) || !old.SetEquals(routes))
                    { choices[anchor] = routes; changed = true; }
                }
            } while (changed);
            var unresolved = remaining.Where(c => !c.Any(option => choices.TryGetValue(option.Anchor, out var routes) &&
                routes.Count == 1 && routes.Contains(option.Route))).OrderBy(c => c.Count).ToList();
            if (unresolved.Count == 0) return true;
            foreach (var option in unresolved[0])
            {
                var next = new Dictionary<string, HashSet<string>>(choices, StringComparer.Ordinal) { [option.Anchor] = [option.Route] };
                if (Search(unresolved.Skip(1).Select(c => c.ToList()).ToList(), next)) return true;
            }
            return false;
        }
    }
}
