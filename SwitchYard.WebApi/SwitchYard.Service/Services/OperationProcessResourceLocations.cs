using SwitchYard.Service.Models;

namespace SwitchYard.Service.Services;

/// <summary>Physical event choices shared by initial generation and the capacity solver.</summary>
public static class OperationProcessResourceLocations
{
    public static List<string> Get(OperationProcessTemplate source, ProcessCatalog catalog,
        ProcessActivity activity, string resourceID, bool start, bool useRouteEndpoints = false)
    {
        var ev = source.Events.First(item => item.Id == (start ? activity.StartEvent : activity.EndEvent));
        var allowedAnchors = ev.SelectedAnchor is not null ? new[] { ev.SelectedAnchor } : ev.AnchorList.ToArray();
        var tracks = catalog.Tracks.ToDictionary(track => track.Id, StringComparer.Ordinal);
        var anchors = source.Anchors.ToDictionary(anchor => anchor.Id, StringComparer.Ordinal);
        var result = new List<string>();
        if (activity.Type == "Dwelling" && !useRouteEndpoints)
        {
            if (!tracks.TryGetValue(resourceID, out var track)) return result;
            foreach (var node in new[] { track.FromNodeID, track.ToNodeID }.Distinct(StringComparer.Ordinal))
                Add(node, null, resourceID);
        }
        else
        {
            var route = catalog.Routes.FirstOrDefault(item => item.Id == resourceID);
            if (route is null) return result;
            var binding = source.RouteAnchors.FirstOrDefault(item => item.RouteID == resourceID);
            Add(start ? route.StartNodeID : route.EndNodeID, start ? binding?.StartAnchor : binding?.EndAnchor, null);
        }
        return result.Distinct(StringComparer.Ordinal).ToList();

        void Add(string? node, string? boundAnchor, string? dwellingTrack)
        {
            if (ev.NodeID is not null && ev.NodeID != node) return;
            if (ev.NodeList.Count > 0 && (node is null || !ev.NodeList.Contains(node, StringComparer.Ordinal))) return;
            if (node is null)
            {
                // An unknown endpoint never silently matches a known physical node.
                if (allowedAnchors.Length == 0 && boundAnchor is null) result.Add("\u001f");
                return;
            }
            if (boundAnchor is not null)
            {
                if ((allowedAnchors.Length == 0 || allowedAnchors.Contains(boundAnchor, StringComparer.Ordinal)) && AtNode(boundAnchor))
                    result.Add(Token(node, boundAnchor));
                return;
            }
            if (allowedAnchors.Length > 0)
            {
                foreach (var id in allowedAnchors.Where(AtNode)) result.Add(Token(node, id));
                return;
            }
            result.Add(Token(node, null));
            // An unspecified route anchor may agree with an explicit anchor of another
            // activity at this event. A dwelling activity must remain on its own track.
            foreach (var id in anchors.Keys.Where(AtNode)) result.Add(Token(node, id));

            bool AtNode(string id) => anchors.TryGetValue(id, out var anchor) &&
                (dwellingTrack is null || anchor.TrackID == dwellingTrack) &&
                tracks.TryGetValue(anchor.TrackID, out var track) &&
                (track.FromNodeID == node || track.ToNodeID == node);
        }
    }

    private static string Token(string node, string? anchor) => node + "\u001f" + (anchor ?? "");
}
