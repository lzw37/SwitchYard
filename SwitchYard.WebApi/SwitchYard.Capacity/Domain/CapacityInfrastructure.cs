using System.Text.Json;
using System.Text.RegularExpressions;

namespace SwitchYard.Capacity;

public sealed record CapacityReferenceIssue(string Path, string TargetType, string TargetId);

/// <summary>Canonical infrastructure identities shared by layout, parameters and train executions.</summary>
public sealed class CapacityInfrastructure
{
    private readonly Dictionary<string, CapacityRoute> _routes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CapacityCell> _cells = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CapacityNode> _nodes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CapacityLink> _links = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CapacityParameterSet> _parameterSets = new(StringComparer.Ordinal);
    private readonly List<CapacityRoute> _solverRoutes = [];
    private readonly List<CapacityRouteOccupation> _solverOccupations = [];
    private readonly List<CapacityRouteOccupation> _layoutOccupations = [];
    private readonly List<CapacityRouteOperationTime> _routeOperationTimes = [];
    private readonly Dictionary<CapacityRoute, string> _routeLayoutPaths = [];
    private readonly Dictionary<CapacityRoute, string> _routeInputPaths = [];
    private readonly Dictionary<CapacityCell, string> _cellPaths = [];
    private readonly Dictionary<CapacityLink, string> _linkPaths = [];
    private readonly HashSet<CapacityReferenceIssue> _recordedIssues = [];
    private readonly bool _hasRouteMetadata;
    private readonly bool _hasCellMetadata;
    private readonly bool _hasNodeMetadata;
    private readonly bool _hasLinkMetadata;
    private readonly bool _hasParameterSetMetadata;

    private CapacityInfrastructure(StationCapacitySolveInput input)
    {
        Input = input;
        var layout = input.StationLayout;
        _hasRouteMetadata = layout?.Routes is { Count: > 0 };
        _hasCellMetadata = layout?.Cells is { Count: > 0 };
        _hasNodeMetadata = layout?.Nodes is { Count: > 0 };
        _hasLinkMetadata = layout?.Links is { Count: > 0 };
        _hasParameterSetMetadata = layout?.CalculationParameterSets?.Sets is { Count: > 0 };
    }

    public StationCapacitySolveInput Input { get; }
    public IReadOnlyDictionary<string, CapacityRoute> Routes => _routes;
    public IReadOnlyList<CapacityRoute> SolverRoutes => _solverRoutes;
    public IReadOnlyDictionary<string, CapacityCell> Cells => _cells;
    public IReadOnlyDictionary<string, CapacityNode> Nodes => _nodes;
    public IReadOnlyDictionary<string, CapacityLink> Links => _links;
    public IReadOnlyDictionary<string, CapacityParameterSet> ParameterSets => _parameterSets;
    public IReadOnlyList<CapacityRouteOccupation> SolverOccupations => _solverOccupations;
    public IReadOnlyList<CapacityRouteOccupation> LayoutOccupations => _layoutOccupations;
    public IReadOnlyList<CapacityRouteOperationTime> RouteOperationTimes => _routeOperationTimes;
    public List<CapacityReferenceIssue> Issues { get; } = [];

    public static CapacityInfrastructure Build(StationCapacitySolveInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var result = new CapacityInfrastructure(input);
        result.IndexDefinitions();
        result.LinkLayout();
        result.LinkParameters();
        return result;
    }

    public CapacityRoute? ResolveRoute(string id, string path) =>
        Resolve(_routes, id, path, "Route", _hasRouteMetadata, key => new CapacityRoute(key));

    public CapacityCell? ResolveCell(string id, string path) =>
        Resolve(_cells, id, path, "Cell", _hasCellMetadata, key => new CapacityCell(key));

    public CapacityNode? ResolveNode(string id, string path) =>
        Resolve(_nodes, id, path, "Node", _hasNodeMetadata, key => new CapacityNode(key));

    public CapacityLink? ResolveLink(string id, string path) =>
        Resolve(_links, id, path, "Link", _hasLinkMetadata, key => new CapacityLink(key));

    public CapacityParameterSet? ResolveParameterSet(string id, string path) =>
        Resolve(_parameterSets, id, path, "CalculationParameterSet", _hasParameterSetMetadata,
            key => new CapacityParameterSet(key), allowEmpty: true);

    private T? Resolve<T>(Dictionary<string, T> definitions, string id, string path, string type,
        bool hasMetadata, Func<string, T> create, bool allowEmpty = false) where T : class
    {
        if (id is null || (!allowEmpty && string.IsNullOrWhiteSpace(id)))
        {
            Issue(path, type, id ?? "");
            return null;
        }
        if (definitions.TryGetValue(id, out var found)) return found;
        if (hasMetadata)
        {
            Issue(path, type, id);
            return null;
        }
        // Older solver payloads contain identities without the optional layout tables.
        definitions[id] = found = create(id);
        return found;
    }

    private void IndexDefinitions()
    {
        var layout = Input.StationLayout;
        foreach (var (source, index) in Indexed(layout?.Nodes))
        {
            var path = $"stationLayout.nodes[{index}]";
            if (!CanIndex(_nodes, source?.Id, path, "Node")) continue;
            _nodes.Add(source!.Id!, new(source.Id!) { Layout = source });
        }
        foreach (var (source, index) in Indexed(layout?.Links))
        {
            var path = $"stationLayout.links[{index}]";
            if (!CanIndex(_links, source?.Id, path, "Link")) continue;
            var link = new CapacityLink(source!.Id!) { Layout = source };
            _links.Add(link.Id, link);
            _linkPaths.Add(link, path);
        }
        foreach (var (source, index) in Indexed(layout?.Cells))
        {
            var path = $"stationLayout.cells[{index}]";
            if (!CanIndex(_cells, source?.Id, path, "Cell")) continue;
            var cell = new CapacityCell(source!.Id!) { Layout = source };
            _cells.Add(cell.Id, cell);
            _cellPaths.Add(cell, path);
        }
        foreach (var (source, index) in Indexed(layout?.Routes))
        {
            var path = $"stationLayout.routes[{index}]";
            if (!CanIndex(_routes, source?.Id, path, "Route")) continue;
            var route = new CapacityRoute(source!.Id!) { Layout = source };
            _routes.Add(route.Id, route);
            _routeLayoutPaths.Add(route, path);
        }
        foreach (var (source, index) in Indexed(layout?.CalculationParameterSets?.Sets))
        {
            var path = $"stationLayout.calculationParameterSets.sets[{index}]";
            if (!CanIndex(_parameterSets, source?.Id, path, "CalculationParameterSet", allowEmpty: true)) continue;
            _parameterSets.Add(source!.Id, new(source.Id) { Layout = source });
        }
        foreach (var (source, index) in Indexed(Input.Routes))
        {
            var path = $"routes[{index}]";
            if (source is null || string.IsNullOrWhiteSpace(source.Id))
            {
                Issue(path + ".id", "Route", source?.Id ?? "");
                continue;
            }
            if (!_routes.TryGetValue(source.Id, out var route))
            {
                // A solver projection is an actual definition even if its layout metadata is absent.
                _routes.Add(source.Id, route = new(source.Id));
                if (_hasRouteMetadata) Issue(path + ".id", "LayoutRoute", source.Id);
            }
            if (route.Input is not null)
            {
                Issue(path + ".id", "DuplicateRoute", source.Id);
                continue;
            }
            route.Input = source;
            _solverRoutes.Add(route);
            _routeInputPaths.Add(route, path);
            foreach (var (cellId, cellIndex) in Indexed(source.CellIds))
            {
                var cellPath = $"{path}.cellIds[{cellIndex}]";
                if (string.IsNullOrWhiteSpace(cellId))
                {
                    Issue(cellPath, "Cell", cellId ?? "");
                    continue;
                }
                // Solver cell IDs declare resources even when optional layout metadata is incomplete.
                if (!_cells.TryGetValue(cellId, out var cell))
                {
                    _cells.Add(cellId, cell = new(cellId));
                    if (_hasCellMetadata) Issue(cellPath, "LayoutCell", cellId);
                }
                cell.SolverId ??= cellId;
            }
        }
    }

    private bool CanIndex<T>(Dictionary<string, T> definitions, string? id, string path, string type,
        bool allowEmpty = false)
    {
        if (id is null || (!allowEmpty && string.IsNullOrWhiteSpace(id)))
        {
            Issue(path + ".id", type, id ?? "");
            return false;
        }
        if (!definitions.ContainsKey(id)) return true;
        Issue(path + ".id", "Duplicate" + type, id);
        return false;
    }

    private void LinkLayout()
    {
        foreach (var (link, path) in _linkPaths)
        {
            var source = link.Layout!;
            if (NullIfBlank(source.FromNodeId) is { } from)
                link.FromNode = ResolveNode(from, path + ".fromNodeId");
            if (NullIfBlank(source.ToNodeId) is { } to)
                link.ToNode = ResolveNode(to, path + ".toNodeId");
            link.FromNode?.OutgoingLinks.Add(link);
            link.ToNode?.IncomingLinks.Add(link);
        }
        foreach (var (cell, path) in _cellPaths)
        {
            cell.Links = ResolveList(ParseList(cell.Layout!.LinkIdList), path + ".linkIdList", ResolveLink);
            foreach (var link in cell.Links) link.Cells.Add(cell);
        }
        foreach (var route in _routes.Values.ToArray())
        {
            if (route.Layout is { } source)
            {
                var path = _routeLayoutPaths[route];
                var nodeIds = ParseList(source.NodeList);
                route.Nodes = ResolveList(nodeIds, path + ".nodeList", ResolveNode);
                route.Links = ResolveList(ParseList(source.LinkList), path + ".linkList", ResolveLink);
                route.LayoutCells = ResolveList(ParseList(source.CellList, StringComparer.OrdinalIgnoreCase),
                    path + ".cellList", ResolveCell);
                route.InterruptCells = ResolveList(ParseList(source.InterruptCellList, StringComparer.OrdinalIgnoreCase),
                    path + ".interruptCellList", ResolveCell);
                if ((NullIfBlank(source.StartNodeId) ?? nodeIds.FirstOrDefault()) is { } start)
                    route.StartNode = ResolveNode(start, path + ".startNodeId");
                if ((NullIfBlank(source.EndNodeId) ?? nodeIds.LastOrDefault()) is { } end)
                    route.EndNode = ResolveNode(end, path + ".endNodeId");
                route.TrainTypeCodes = ParseList(source.TrainTypes);
                route.SwitchIds = ParseList(source.SwitchList);
                route.SignalIds = ParseList(source.SignalList);
                for (var index = 0; index < route.SwitchIds.Count; index++)
                    Issue($"{path}.switchList[{index}]", "Switch", route.SwitchIds[index]);
                for (var index = 0; index < route.SignalIds.Count; index++)
                    Issue($"{path}.signalList[{index}]", "Signal", route.SignalIds[index]);
            }
            if (route.Input is { } input)
            {
                route.Cells = ResolveList(input.CellIds ?? [], _routeInputPaths[route] + ".cellIds", ResolveCell);
                route.Tags = input.Tags?.ToList() ?? [];
            }
            else
            {
                route.Cells = route.LayoutCells;
                route.Tags = ParseList(route.Layout?.AllowanceTags, StringComparer.OrdinalIgnoreCase);
            }
            foreach (var cell in route.Cells.Concat(route.LayoutCells).Concat(route.InterruptCells).Distinct())
                cell.Routes.Add(route);
        }
    }

    private void LinkParameters()
    {
        var layout = Input.StationLayout;
        if (layout?.CalculationParameterSets?.RouteOccupations is { } groups)
            foreach (var (setId, rows) in groups)
            {
                var path = $"stationLayout.calculationParameterSets.routeOccupations[{setId}]";
                var set = ResolveParameterSet(setId, path);
                if (rows is null)
                {
                    Issue(path, "RouteOccupation", setId);
                    continue;
                }
                foreach (var (source, index) in Indexed(rows))
                {
                    if (source is null) { Issue($"{path}[{index}]", "RouteOccupation", ""); continue; }
                    var occupation = new CapacityRouteOccupation(setId) { Layout = source, ParameterSet = set };
                    occupation.Route = ResolveRoute(source.RouteId!, $"{path}[{index}].routeId");
                    occupation.Cell = ResolveCell(source.CellId!, $"{path}[{index}].cellId");
                    _layoutOccupations.Add(occupation);
                    set?.RouteOccupations.Add(occupation);
                }
            }
        foreach (var (source, index) in Indexed(Input.RouteOccupations))
        {
            var path = $"routeOccupations[{index}]";
            if (source is null) { Issue(path, "RouteOccupation", ""); continue; }
            var setId = source.CalculationParameterSetId ?? "";
            var occupation = new CapacityRouteOccupation(setId)
            {
                Input = source,
                Route = ResolveRoute(source.RouteId, path + ".routeId"),
                Cell = ResolveCell(source.CellId, path + ".cellId"),
                ParameterSet = ResolveParameterSet(setId, path + ".calculationParameterSetId")
            };
            _solverOccupations.Add(occupation);
            occupation.ParameterSet?.RouteOccupations.Add(occupation);
        }
        foreach (var (source, index) in Indexed(layout?.RouteOperationTimes))
        {
            var path = $"stationLayout.routeOperationTimes[{index}]";
            if (source is null) { Issue(path, "RouteOperationTime", ""); continue; }
            var operationTime = new CapacityRouteOperationTime(source)
            {
                Route = ResolveRoute(source.RouteId!, path + ".routeId"),
                ParameterSet = ResolveParameterSet(source.CalculationParameterSetId ?? "", path + ".calculationParameterSetId")
            };
            _routeOperationTimes.Add(operationTime);
            operationTime.ParameterSet?.RouteOperationTimes.Add(operationTime);
        }
    }

    private static IReadOnlyList<T> ResolveList<T>(IReadOnlyList<string> ids, string path,
        Func<string, string, T?> resolve) where T : class
    {
        var result = new List<T>();
        for (var index = 0; index < ids.Count; index++)
            if (resolve(ids[index], $"{path}[{index}]") is { } target) result.Add(target);
        return result;
    }

    private void Issue(string path, string type, string id)
    {
        var issue = new CapacityReferenceIssue(path, type, id);
        if (_recordedIssues.Add(issue)) Issues.Add(issue);
    }

    private static IEnumerable<(T Item, int Index)> Indexed<T>(IEnumerable<T>? items) =>
        (items ?? []).Select((item, index) => (item, index));

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IReadOnlyList<string> ParseList(string? value, StringComparer? comparer = null)
    {
        if (string.IsNullOrWhiteSpace(value)) return [];
        comparer ??= StringComparer.Ordinal;
        var text = value.Trim();
        if (text.StartsWith('['))
        {
            try
            {
                using var document = JsonDocument.Parse(text);
                if (document.RootElement.ValueKind == JsonValueKind.Array)
                    return document.RootElement.EnumerateArray()
                        .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() ?? "" :
                            item.ValueKind == JsonValueKind.Number ? item.GetRawText() : "")
                        .Select(item => item.Trim()).Where(item => item.Length > 0).Distinct(comparer).ToList();
            }
            catch (JsonException) { /* Legacy lists can contain unquoted identifiers. */ }
        }
        return Regex.Split(text.Trim('[', ']'), @"(?:\s*->\s*)|[\s,，;；]+")
            .Select(item => item.Trim('"', '\'')).Where(item => item.Length > 0).Distinct(comparer).ToList();
    }
}

public sealed class CapacityRoute(string id)
{
    public string Id => Input?.Id ?? id;
    public StationCapacityRouteInput? Input { get; internal set; }
    public StationCapacityLayoutRouteInput? Layout { get; internal set; }
    public IReadOnlyList<CapacityCell> Cells { get; internal set; } = [];
    public IReadOnlyList<CapacityCell> LayoutCells { get; internal set; } = [];
    public IReadOnlyList<CapacityCell> InterruptCells { get; internal set; } = [];
    public IReadOnlyList<CapacityNode> Nodes { get; internal set; } = [];
    public IReadOnlyList<CapacityLink> Links { get; internal set; } = [];
    public CapacityNode? StartNode { get; internal set; }
    public CapacityNode? EndNode { get; internal set; }
    public IReadOnlyList<string> Tags { get; internal set; } = [];
    public IReadOnlyList<string> TrainTypeCodes { get; internal set; } = [];
    public IReadOnlyList<string> SwitchIds { get; internal set; } = [];
    public IReadOnlyList<string> SignalIds { get; internal set; } = [];
}

public sealed class CapacityCell(string id)
{
    public string Id => SolverId ?? id;
    internal string? SolverId { get; set; }
    public StationCapacityLayoutCellInput? Layout { get; internal set; }
    public IReadOnlyList<CapacityLink> Links { get; internal set; } = [];
    public List<CapacityRoute> Routes { get; } = [];
}

public sealed class CapacityNode(string id)
{
    public string Id { get; } = id;
    public StationCapacityLayoutNodeInput? Layout { get; internal set; }
    public List<CapacityLink> IncomingLinks { get; } = [];
    public List<CapacityLink> OutgoingLinks { get; } = [];
}

public sealed class CapacityLink(string id)
{
    public string Id { get; } = id;
    public StationCapacityLayoutLinkInput? Layout { get; internal set; }
    public CapacityNode? FromNode { get; internal set; }
    public CapacityNode? ToNode { get; internal set; }
    public List<CapacityCell> Cells { get; } = [];
}

public sealed class CapacityParameterSet(string id)
{
    public string Id { get; } = id;
    public string Name => Layout?.Name ?? Id;
    public bool IsDefault => Layout?.IsDefault ?? false;
    public StationCapacityLayoutParameterSetInput? Layout { get; internal set; }
    // Both input projections are retained; solver consumers use Infrastructure.SolverOccupations.
    public List<CapacityRouteOccupation> RouteOccupations { get; } = [];
    public List<CapacityRouteOperationTime> RouteOperationTimes { get; } = [];
}

public sealed class CapacityRouteOccupation(string parameterSetId)
{
    public StationCapacityRouteOccupationInput? Input { get; internal set; }
    public StationCapacityLayoutRouteOccupationInput? Layout { get; internal set; }
    // Keep the grouping key even when its parameter-set definition is missing.
    public string ParameterSetId { get; } = parameterSetId;
    public CapacityRoute? Route { get; internal set; }
    public CapacityCell? Cell { get; internal set; }
    public CapacityParameterSet? ParameterSet { get; internal set; }
    public string TrainTypeId => Input?.TrainTypeId ?? Layout?.TrainTypeId ?? "";
    public double StartOccupationShiftSeconds => Input?.StartOccupationShiftSeconds ?? Layout?.StartOccupationShift ?? 0;
    public double EndOccupationShiftSeconds => Input?.EndOccupationShiftSeconds ?? Layout?.EndOccupationShift ?? 0;
}

public sealed class CapacityRouteOperationTime(StationCapacityLayoutRouteOperationTimeInput input)
{
    public StationCapacityLayoutRouteOperationTimeInput Input { get; } = input;
    public CapacityRoute? Route { get; internal set; }
    public CapacityParameterSet? ParameterSet { get; internal set; }
    public string TrainTypeId => Input.TrainTypeId ?? "";
    public double OperationTimeSeconds => Input.OperationTimeS;
}
