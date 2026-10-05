using System.Globalization;
using System.Text.RegularExpressions;

namespace SwitchYard.StationLayout;

internal static partial class StationLayoutRouteSearcher
{
    private const double CoordinateTolerance = 1e-9;

    public static StationRouteSearchResponse Search(
        string scopeId,
        string schemeId,
        StationLayoutDocument document,
        string startNodeId,
        string endNodeId)
    {
        ArgumentNullException.ThrowIfNull(document);

        var nodes = BuildNodes(document.Nodes);
        var links = BuildLinks(document.Tracks);
        if (!nodes.ContainsKey(startNodeId))
        {
            throw new StationLayoutValidationException($"Start node {startNodeId} does not exist.");
        }

        if (!nodes.ContainsKey(endNodeId))
        {
            throw new StationLayoutValidationException($"End node {endNodeId} does not exist.");
        }

        var routes = new List<RoutePath>();
        routes.AddRange(SearchDirection(nodes, links, startNodeId, endNodeId, "LeftToRight"));
        routes.AddRange(SearchDirection(nodes, links, startNodeId, endNodeId, "RightToLeft"));

        var response = new StationRouteSearchResponse
        {
            InstanceID = scopeId,
            StationSchemeID = schemeId,
            StartNodeId = startNodeId,
            EndNodeId = endNodeId,
            Routes = routes
                .Select(route => ToResult(route, document))
                .OrderBy(result => result.CellIds.Count)
                .ThenBy(result => result.LinkIds.Count)
                .ThenBy(result => result.Direction, StringComparer.Ordinal)
                .ThenBy(result => string.Join(",", result.NodeIds))
                .ToList()
        };
        return response;
    }

    private static Dictionary<string, StationRouteNode> BuildNodes(
        IEnumerable<StationLayoutNode> source)
    {
        var nodes = new Dictionary<string, StationRouteNode>();
        foreach (var item in source)
        {
            var id = item.ID;
            if (string.IsNullOrWhiteSpace(id))
                throw new StationLayoutValidationException("Element ID is required for route search.");

            if (!double.IsFinite(item.X) || !double.IsFinite(item.Y))
            {
                throw new StationLayoutValidationException($"Node {id} has invalid coordinates.");
            }

            if (!nodes.TryAdd(id, new StationRouteNode(id, item.X, item.Y)))
            {
                throw new StationLayoutValidationException($"Duplicate node ID {id}.");
            }
        }

        return nodes;
    }

    private static Dictionary<string, StationRouteLink> BuildLinks(
        IEnumerable<StationLayoutTrack> source)
    {
        var links = new Dictionary<string, StationRouteLink>();
        foreach (var item in source)
        {
            var id = item.ID;
            if (string.IsNullOrWhiteSpace(id))
                throw new StationLayoutValidationException("Element ID is required for route search.");

            var fromNodeId = item.FromNodeID;
            var toNodeId = item.ToNodeID;
            if (string.IsNullOrWhiteSpace(fromNodeId) || string.IsNullOrWhiteSpace(toNodeId))
                throw new StationLayoutValidationException($"Track {id} must reference endpoint node IDs.");

            var link = new StationRouteLink(
                id,
                item.Name,
                item.ArrowDirection,
                item.ArrowType,
                fromNodeId,
                toNodeId);
            if (!links.TryAdd(id, link))
            {
                throw new StationLayoutValidationException($"Duplicate track ID {id}.");
            }
        }

        return links;
    }

    private static IReadOnlyList<RoutePath> SearchDirection(
        IReadOnlyDictionary<string, StationRouteNode> nodes,
        IReadOnlyDictionary<string, StationRouteLink> links,
        string startNodeId,
        string endNodeId,
        string direction)
    {
        var outgoing = nodes.Keys.ToDictionary(id => id, _ => new List<DirectedLink>());
        var incomingCounts = nodes.Keys.ToDictionary(id => id, _ => 0);

        foreach (var link in links.Values.OrderBy(link => link.ID))
        {
            if (!nodes.TryGetValue(link.FromNodeID, out var originalFrom) ||
                !nodes.TryGetValue(link.ToNodeID, out var originalTo))
            {
                throw new StationLayoutValidationException(
                    $"Track {link.ID} references a missing endpoint.");
            }

            if (link.FromNodeID == link.ToNodeID)
            {
                throw new StationLayoutValidationException(
                    $"Track {link.ID} contains a self-loop on node {link.FromNodeID}.");
            }

            var xComparison = CompareCoordinate(originalFrom.X, originalTo.X);
            var leftNodeId = xComparison < 0
                ? originalFrom.ID
                : xComparison > 0
                    ? originalTo.ID
                    : link.FromNodeID;
            var rightNodeId = leftNodeId == link.FromNodeID ? link.ToNodeID : link.FromNodeID;
            var fromNodeId = direction == "LeftToRight" ? leftNodeId : rightNodeId;
            var toNodeId = direction == "LeftToRight" ? rightNodeId : leftNodeId;

            outgoing[fromNodeId].Add(new DirectedLink(link, fromNodeId, toNodeId));
            incomingCounts[toNodeId]++;
        }

        EnsureAcyclic(outgoing, incomingCounts, direction);

        var results = new List<RoutePath>();
        var currentLinks = new List<DirectedLink>();
        var visitedNodes = new HashSet<string> { startNodeId };

        void SearchFrom(string currentNodeId)
        {
            if (currentNodeId == endNodeId)
            {
                var nodeIds = new List<string> { startNodeId };
                nodeIds.AddRange(currentLinks.Select(link => link.ToNodeId));
                results.Add(new RoutePath(
                    direction,
                    nodeIds.Select(id => nodes[id]).ToList(),
                    currentLinks.Select(link => link.Source).ToList()));
                return;
            }

            foreach (var link in outgoing[currentNodeId])
            {
                if (!visitedNodes.Add(link.ToNodeId))
                {
                    continue;
                }

                currentLinks.Add(link);
                SearchFrom(link.ToNodeId);
                currentLinks.RemoveAt(currentLinks.Count - 1);
                visitedNodes.Remove(link.ToNodeId);
            }
        }

        SearchFrom(startNodeId);
        return results;
    }

    private static void EnsureAcyclic(
        IReadOnlyDictionary<string, List<DirectedLink>> outgoing,
        Dictionary<string, int> incomingCounts,
        string direction)
    {
        var queue = new Queue<string>(incomingCounts
            .Where(pair => pair.Value == 0)
            .Select(pair => pair.Key));
        var visitedCount = 0;

        while (queue.Count > 0)
        {
            var nodeId = queue.Dequeue();
            visitedCount++;
            foreach (var link in outgoing[nodeId])
            {
                incomingCounts[link.ToNodeId]--;
                if (incomingCounts[link.ToNodeId] == 0)
                {
                    queue.Enqueue(link.ToNodeId);
                }
            }
        }

        if (visitedCount != incomingCounts.Count)
        {
            var cycleIds = incomingCounts
                .Where(pair => pair.Value > 0)
                .Select(pair => pair.Key)
                .Order()
                .ToArray();
            throw new StationLayoutValidationException(
                $"Directional graph {direction} contains a closed cycle involving node IDs: {string.Join(", ", cycleIds)}.");
        }
    }

    private static StationRouteSearchResult ToResult(
        RoutePath route,
        StationLayoutDocument document)
    {
        var nodeIndex = route.Nodes
            .Select((node, index) => new { NodeId = node.ID, index })
            .ToDictionary(item => item.NodeId, item => item.index, StringComparer.Ordinal);
        var nodeIds = nodeIndex.Keys.ToHashSet(StringComparer.Ordinal);
        var linkIndex = route.Links
            .Select((link, index) => new { LinkId = link.ID, index })
            .ToDictionary(item => item.LinkId, item => item.index, StringComparer.Ordinal);

        var switches = document.Switches
            .Where(item => HasBindingNode(item.BindingNodeID, nodeIds))
            .OrderBy(item => NodeOrder(item.BindingNodeID, nodeIndex))
            .ThenBy(item => item.ID ?? string.Empty, StringComparer.Ordinal)
            .ToList();
        var signals = document.Signals
            .Where(item => HasBindingNode(item.BindingNodeID, nodeIds))
            .OrderBy(item => NodeOrder(item.BindingNodeID, nodeIndex))
            .ThenBy(item => item.ID ?? string.Empty, StringComparer.Ordinal)
            .ToList();
        var cells = document.Cells
            .Select(item => new { Cell = item, LinkOrder = FirstMatchingLink(item.LinkIDList, linkIndex) })
            .Where(item => item.LinkOrder < int.MaxValue)
            .OrderBy(item => item.LinkOrder)
            .ThenBy(item => item.Cell.ID ?? string.Empty, StringComparer.Ordinal)
            .Select(item => item.Cell)
            .ToList();

        return new StationRouteSearchResult
        {
            Direction = route.Direction,
            NodeIds = route.Nodes.Select(node => node.ID).ToList(),
            LinkIds = route.Links.Select(link => link.ID).ToList(),
            SwitchIds = switches.Select(item => item.ID?.Trim()).Where(NotEmpty).Select(item => item!).ToList(),
            CellIds = cells.Select(item => item.ID?.Trim()).Where(NotEmpty).Select(item => item!).ToList(),
            SignalIds = signals.Select(item => item.ID?.Trim()).Where(NotEmpty).Select(item => item!).ToList(),
            Nodes = route.Nodes,
            Links = route.Links,
            Switches = switches,
            Cells = cells,
            Signals = signals
        };
    }

    private static bool HasBindingNode(string? bindingNodeId, IReadOnlySet<string> nodeIds) =>
        !string.IsNullOrWhiteSpace(bindingNodeId) && nodeIds.Contains(bindingNodeId.Trim());

    private static int NodeOrder(
        string? bindingNodeId,
        IReadOnlyDictionary<string, int> nodeIndex) =>
        !string.IsNullOrWhiteSpace(bindingNodeId) && nodeIndex.TryGetValue(bindingNodeId.Trim(), out var index)
            ? index
            : int.MaxValue;

    private static int FirstMatchingLink(
        string? linkIdList,
        IReadOnlyDictionary<string, int> linkIndex)
    {
        var result = int.MaxValue;
        foreach (var linkId in ParseDelimitedIds(linkIdList))
        {
            if (linkIndex.TryGetValue(linkId, out var index))
            {
                result = Math.Min(result, index);
            }
        }

        return result;
    }

    private static IEnumerable<string> ParseDelimitedIds(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return DelimiterRegex()
            .Split(value.Trim())
            .Select(item => item.Trim())
            .Where(NotEmpty)
            .Distinct(StringComparer.Ordinal)!;
    }

    private static bool NotEmpty(string? value) => !string.IsNullOrWhiteSpace(value);

    private static int CompareCoordinate(double left, double right)
    {
        var difference = left - right;
        if (Math.Abs(difference) <= CoordinateTolerance)
        {
            return 0;
        }

        return difference < 0 ? -1 : 1;
    }

    [GeneratedRegex(@"[\s,，;；]+")]
    private static partial Regex DelimiterRegex();

    private sealed record DirectedLink(StationRouteLink Source, string FromNodeId, string ToNodeId);
    private sealed record RoutePath(
        string Direction,
        List<StationRouteNode> Nodes,
        List<StationRouteLink> Links);
}
