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
        int startNodeId,
        int endNodeId)
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
                .ThenBy(result => result.Direction, StringComparer.OrdinalIgnoreCase)
                .ThenBy(result => string.Join(",", result.NodeIds))
                .ToList()
        };
        return response;
    }

    private static Dictionary<int, StationRouteNode> BuildNodes(
        IEnumerable<StationLayoutNode> source)
    {
        var nodes = new Dictionary<int, StationRouteNode>();
        foreach (var item in source)
        {
            if (!TryParseIntegerId(item.ID, out var id))
            {
                throw new StationLayoutValidationException(
                    $"Node ID '{item.ID}' must be an integer for route search.");
            }

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

    private static Dictionary<int, StationRouteLink> BuildLinks(
        IEnumerable<StationLayoutTrack> source)
    {
        var links = new Dictionary<int, StationRouteLink>();
        foreach (var item in source)
        {
            if (!TryParseIntegerId(item.ID, out var id))
            {
                throw new StationLayoutValidationException(
                    $"Track ID '{item.ID}' must be an integer for route search.");
            }

            if (!TryParseIntegerId(item.FromNodeID, out var fromNodeId) ||
                !TryParseIntegerId(item.ToNodeID, out var toNodeId))
            {
                throw new StationLayoutValidationException(
                    $"Track {id} must reference integer from/to node IDs.");
            }

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
        IReadOnlyDictionary<int, StationRouteNode> nodes,
        IReadOnlyDictionary<int, StationRouteLink> links,
        int startNodeId,
        int endNodeId,
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
        var visitedNodes = new HashSet<int> { startNodeId };

        void SearchFrom(int currentNodeId)
        {
            if (currentNodeId == endNodeId)
            {
                var nodeIds = new List<int> { startNodeId };
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
        IReadOnlyDictionary<int, List<DirectedLink>> outgoing,
        Dictionary<int, int> incomingCounts,
        string direction)
    {
        var queue = new Queue<int>(incomingCounts
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
            .Select((node, index) => new { NodeId = node.ID.ToString(CultureInfo.InvariantCulture), index })
            .ToDictionary(item => item.NodeId, item => item.index, StringComparer.OrdinalIgnoreCase);
        var nodeIds = nodeIndex.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var linkIndex = route.Links
            .Select((link, index) => new { LinkId = link.ID.ToString(CultureInfo.InvariantCulture), index })
            .ToDictionary(item => item.LinkId, item => item.index, StringComparer.OrdinalIgnoreCase);

        var switches = document.Switches
            .Where(item => HasBindingNode(item.BindingNodeID, nodeIds))
            .OrderBy(item => NodeOrder(item.BindingNodeID, nodeIndex))
            .ThenBy(item => item.ID ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var signals = document.Signals
            .Where(item => HasBindingNode(item.BindingNodeID, nodeIds))
            .OrderBy(item => NodeOrder(item.BindingNodeID, nodeIndex))
            .ThenBy(item => item.ID ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var cells = document.Cells
            .Select(item => new { Cell = item, LinkOrder = FirstMatchingLink(item.LinkIDList, linkIndex) })
            .Where(item => item.LinkOrder < int.MaxValue)
            .OrderBy(item => item.LinkOrder)
            .ThenBy(item => item.Cell.ID ?? string.Empty, StringComparer.OrdinalIgnoreCase)
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
            .Distinct(StringComparer.OrdinalIgnoreCase)!;
    }

    private static bool TryParseIntegerId(string? value, out int id) =>
        int.TryParse(value?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out id);

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

    private sealed record DirectedLink(StationRouteLink Source, int FromNodeId, int ToNodeId);
    private sealed record RoutePath(
        string Direction,
        List<StationRouteNode> Nodes,
        List<StationRouteLink> Links);
}
