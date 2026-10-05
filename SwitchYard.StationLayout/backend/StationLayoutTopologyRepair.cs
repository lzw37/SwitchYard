using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace SwitchYard.StationLayout;

/// <summary>Repairs references on a detached JSON tree; geometry and extension fields are retained.</summary>
public sealed class StationLayoutTopologyRepair
{
    private readonly JsonObject _root;
    private readonly bool _archive;
    private readonly double _tolerance;
    private readonly Dictionary<string, JsonObject> _nodes;
    private readonly Dictionary<string, JsonObject> _tracks;
    private readonly List<string> _changes = [];
    private static readonly string[] BindingKeys = ["bindingNodeID", "BindingNodeID", "bindingNodeId", "BindingNodeId"];
    private static readonly string[] CurveNodeKeys = ["nodeID", "vertexNodeID"];
    private static readonly string[] CurveTrackKeys = ["tangentLinkID1", "tangentLinkID2", "linkID1", "linkID2"];

    private StationLayoutTopologyRepair(JsonObject root, double tolerance)
    {
        _root = root;
        _archive = root.ContainsKey("format");
        _tolerance = tolerance;
        _nodes = Index("nodes");
        _tracks = Index("tracks");
    }

    public static StationLayoutRepairResult Process(string json, double tolerance)
    {
        if (!double.IsFinite(tolerance) || tolerance <= 0)
            throw new InvalidOperationException("Topology repair tolerance must be positive and finite.");
        StationLayoutJsonValidation.Validate(json);
        var root = JsonNode.Parse(json) as JsonObject
            ?? throw new StationLayoutValidationException("Station-layout JSON must be an object.");
        var repair = new StationLayoutTopologyRepair(root, tolerance);
        // Deserialization rejects malformed known fields before any spatial inference.
        _ = StationLayoutDocument.FromJson(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (!repair.NeedsRepair()) return new(json, 0, []);
        repair.Apply();
        if (repair.NeedsRepair()) Fail("layout", "references remain inconsistent after repair");
        var repairedJson = root.ToJsonString();
        StationLayoutJsonValidation.Validate(repairedJson);
        _ = StationLayoutDocument.FromJson(repairedJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        return new(repairedJson, repair._changes.Count, repair._changes);
    }

    private JsonNode? Field(JsonObject obj, string key) => obj[Key(obj, key)];
    private string Key(JsonObject obj, string key) => obj.ContainsKey(key) || _archive ? key
        : obj.Select(item => item.Key).FirstOrDefault(name => name.Equals(key, StringComparison.OrdinalIgnoreCase)) ?? key;
    private IEnumerable<JsonObject> Items(string name)
    {
        if (Field(_root, name) is not JsonArray array) yield break;
        foreach (var item in array)
            yield return item as JsonObject ?? throw new StationLayoutValidationException($"Invalid '{name}' element.");
    }
    private Dictionary<string, JsonObject> Index(string name)
    {
        var result = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var item in Items(name))
        {
            var id = Text(Field(item, "id"));
            if (string.IsNullOrWhiteSpace(id) || !result.TryAdd(id, item)) Fail(name, "IDs must be unique non-empty strings");
        }
        return result;
    }
    private static string Text(JsonNode? value) => value is null ? "" : value.GetValue<string>();
    private string Id(JsonObject item) => Text(Field(item, "id"));
    private string Ref(JsonObject item, string key) => Text(Field(item, key));
    private bool Missing(JsonObject item, string key, Dictionary<string, JsonObject> index)
    {
        var id = Ref(item, key);
        return !string.IsNullOrWhiteSpace(id) && !index.ContainsKey(id);
    }
    private string Path(string collection, JsonObject item, string key) => $"{collection}[{Id(item)}].{key}";
    private void Set(JsonObject item, string key, JsonNode? value, string path)
    {
        key = Key(item, key);
        if (JsonNode.DeepEquals(item[key], value)) return;
        item[key] = value;
        _changes.Add(path);
    }
    private static void Fail(string path, string reason) => throw new StationLayoutValidationException(
        $"Station-layout automatic repair failed: {path}: {reason}.");
    private static double? Number(JsonObject obj, string key)
        => obj[key] is JsonValue value && value.TryGetValue<double>(out var number) && double.IsFinite(number) ? number : null;
    private (double X, double Y)? Point(JsonObject obj, string x = "x", string y = "y")
    {
        var px = Number(obj, Key(obj, x));
        var py = Number(obj, Key(obj, y));
        return px.HasValue && py.HasValue ? (px.Value, py.Value) : null;
    }
    private (double X, double Y)? Position(JsonObject obj, string field = "position")
        => Field(obj, field) is JsonObject point ? Point(point) : null;
    private bool Near((double X, double Y) a, (double X, double Y) b)
        => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2)) <= _tolerance;
    private JsonObject Unique(IEnumerable<JsonObject> candidates, string path)
    {
        var matches = candidates.Distinct().Take(2).ToArray();
        if (matches.Length != 1) Fail(path, matches.Length == 0 ? "no position-matched candidate" : "multiple position-matched candidates");
        return matches[0];
    }
    private IEnumerable<JsonObject> NodesAt((double X, double Y)? point) => point is null ? []
        : _nodes.Values.Where(node => Point(node) is { } candidate && Near(candidate, point.Value));
    private bool Incident(JsonObject line, string nodeId) => Ref(line, "fromNodeID") == nodeId || Ref(line, "toNodeID") == nodeId;
    private IEnumerable<string> Adjacent(string nodeId) => _tracks.Where(pair => Incident(pair.Value, nodeId)).Select(pair => pair.Key);
    private static string[] IdList(JsonNode? value)
    {
        if (value is JsonArray array) return array.Select(Text).Where(id => !string.IsNullOrWhiteSpace(id)).ToArray();
        if (value is null) return [];
        return Regex.Split(Text(value), @"[,，;；\s]+").Where(id => id.Length > 0).ToArray();
    }
    private bool NeedsRepair()
    {
        foreach (var line in _tracks.Values)
            if (Missing(line, "fromNodeID", _nodes) || Missing(line, "toNodeID", _nodes)) return true;
        foreach (var node in _nodes.Values)
            if (Field(node, "adjacentLineIDList") is { } links && !IdList(links).ToHashSet().SetEquals(Adjacent(Id(node)))) return true;
        foreach (var name in new[] { "signals", "insulationJoints", "bufferStops", "switches" })
            foreach (var equipment in Items(name))
                if (BindingKeys.Any(key => Missing(equipment, key, _nodes))) return true;
        foreach (var sw in Items("switches"))
            foreach (var branch in Branches(sw))
                if (Missing(branch, "lineID", _tracks)) return true;
        foreach (var curve in Items("curves"))
            if (CurveNodeKeys.Any(key => Missing(curve, key, _nodes)) || CurveTrackKeys.Any(key => Missing(curve, key, _tracks))) return true;
        foreach (var cell in Items("cells"))
            if (IdList(Field(cell, "linkIDList")).Any(id => !_tracks.ContainsKey(id))) return true;
        return false;
    }
    private IEnumerable<JsonObject> Branches(JsonObject sw)
        => Field(sw, "branchVectorList") is JsonArray branches ? branches.Select(item => item as JsonObject
            ?? throw new StationLayoutValidationException("Invalid switch branch.")) : [];

    private void Apply()
    {
        // Preserve the old node/track evidence until all references (including Cells) are resolved.
        var oldAdjacency = _nodes.ToDictionary(pair => pair.Key, pair => IdList(Field(pair.Value, "adjacentLineIDList")));
        foreach (var line in _tracks.Values)
        {
            foreach (var (key, x, y) in new[] { ("fromNodeID", "x1", "y1"), ("toNodeID", "x2", "y2") })
            {
                if (!Missing(line, key, _nodes) && !string.IsNullOrWhiteSpace(Ref(line, key))) continue;
                var candidates = NodesAt(Point(line, x, y)).ToArray();
                if (string.IsNullOrWhiteSpace(Ref(line, key)) && candidates.Length == 0) continue;
                var match = Unique(candidates, Path("tracks", line, key));
                Set(line, key, JsonValue.Create(Id(match)), Path("tracks", line, key));
            }
        }
        foreach (var name in new[] { "signals", "insulationJoints", "bufferStops", "switches" })
            foreach (var equipment in Items(name))
                foreach (var key in BindingKeys.Where(key => Missing(equipment, key, _nodes)))
                {
                    var match = Unique(NodesAt(Position(equipment)), Path(name, equipment, key));
                    Set(equipment, key, JsonValue.Create(Id(match)), Path(name, equipment, key));
                    if (Field(equipment, "bindingNodeID") is null)
                        Set(equipment, "bindingNodeID", JsonValue.Create(Id(match)), Path(name, equipment, "bindingNodeID"));
                }
        foreach (var sw in Items("switches"))
        {
            var binding = BindingKeys.Select(key => Ref(sw, key)).FirstOrDefault(_nodes.ContainsKey);
            var anchor = binding is not null ? Point(_nodes[binding]) : Position(sw);
            foreach (var branch in Branches(sw).Where(branch => Missing(branch, "lineID", _tracks)))
            {
                var path = Path("switches", sw, "branchVectorList.lineID");
                var vector = Point(branch);
                var match = Unique(_tracks.Values.Where(line => (binding is null || Incident(line, binding))
                    && Outgoing(line, anchor) is { } direction
                    && vector is { } v && SameDirection(direction, v)), path);
                var outgoing = Outgoing(match, anchor)!.Value;
                Set(branch, "lineID", JsonValue.Create(Id(match)), path);
                Set(branch, "x", JsonValue.Create(outgoing.X), Path("switches", sw, "branchVectorList.x"));
                Set(branch, "y", JsonValue.Create(outgoing.Y), Path("switches", sw, "branchVectorList.y"));
            }
        }
        foreach (var curve in Items("curves")) RepairCurve(curve);
        foreach (var cell in Items("cells"))
        {
            var ids = IdList(Field(cell, "linkIDList"));
            if (ids.All(_tracks.ContainsKey)) continue;
            var resolved = new List<string>();
            foreach (var id in ids)
            {
                if (_tracks.ContainsKey(id)) { resolved.Add(id); continue; }
                var endpoints = oldAdjacency.Where(pair => pair.Value.Contains(id)).Select(pair => _nodes[pair.Key]).ToArray();
                var path = Path("cells", cell, "linkIDList");
                if (endpoints.Length != 2) Fail(path, $"cannot infer endpoints of missing track '{id}'");
                resolved.AddRange(ResolveStraightChain(endpoints[0], endpoints[1], path));
            }
            Set(cell, "linkIDList", JsonValue.Create(string.Join(",", resolved.Distinct())), Path("cells", cell, "linkIDList"));
        }
        foreach (var node in _nodes.Values)
        {
            var adjacent = Adjacent(Id(node)).ToArray();
            if (IdList(Field(node, "adjacentLineIDList")).ToHashSet().SetEquals(adjacent)) continue;
            Set(node, "adjacentLineIDList", new JsonArray(adjacent.Select(id => (JsonNode?)JsonValue.Create(id)).ToArray()),
                Path("nodes", node, "adjacentLineIDList"));
        }
    }

    private (double X, double Y)? Outgoing(JsonObject line, (double X, double Y)? anchor)
    {
        if (anchor is null || Point(line, "x1", "y1") is not { } a || Point(line, "x2", "y2") is not { } b) return null;
        if (Near(anchor.Value, a)) return (b.X - a.X, b.Y - a.Y);
        if (Near(anchor.Value, b)) return (a.X - b.X, a.Y - b.Y);
        return null;
    }
    private bool SameDirection((double X, double Y) a, (double X, double Y) b)
    {
        var length = Math.Sqrt(a.X * a.X + a.Y * a.Y);
        var otherLength = Math.Sqrt(b.X * b.X + b.Y * b.Y);
        return length > 0 && otherLength > 0 && a.X * b.X + a.Y * b.Y > 0
            && Math.Abs(a.X * b.Y - a.Y * b.X) / (length * otherLength) < 1e-6;
    }
    private bool OnSegment((double X, double Y) point, (double X, double Y) start, (double X, double Y) end)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var lengthSquared = dx * dx + dy * dy;
        if (lengthSquared == 0) return Near(point, start);
        var t = Math.Clamp(((point.X - start.X) * dx + (point.Y - start.Y) * dy) / lengthSquared, 0, 1);
        return Near(point, (start.X + t * dx, start.Y + t * dy));
    }
    private bool OnTrack(JsonObject line, (double X, double Y)? point) => point is { } p
        && Point(line, "x1", "y1") is { } start && Point(line, "x2", "y2") is { } end && OnSegment(p, start, end);

    private void RepairCurve(JsonObject curve)
    {
        var missingNodes = CurveNodeKeys.Where(key => Missing(curve, key, _nodes)).ToArray();
        var missingTracks = CurveTrackKeys.Where(key => Missing(curve, key, _tracks)).ToArray();
        if (missingNodes.Length + missingTracks.Length == 0) return;
        var nodeId = CurveNodeKeys.Select(key => Ref(curve, key)).FirstOrDefault(_nodes.ContainsKey);
        if (nodeId is null)
        {
            var validTracks = CurveTrackKeys.Select(key => Ref(curve, key)).Where(_tracks.ContainsKey).Distinct().ToArray();
            var match = Unique(_nodes.Values.Where(node =>
                validTracks.Length >= 2 ? validTracks.All(id => Incident(_tracks[id], Id(node))) : IsCurveVertex(curve, Point(node))),
                Path("curves", curve, "nodeID"));
            nodeId = Id(match);
        }
        foreach (var key in missingNodes) Set(curve, key, JsonValue.Create(nodeId), Path("curves", curve, key));
        if (Field(curve, "nodeID") is null) Set(curve, "nodeID", JsonValue.Create(nodeId), Path("curves", curve, "nodeID"));
        foreach (var key in missingTracks)
        {
            var tangent = Position(curve, key.EndsWith('1') ? "start" : "end");
            var match = Unique(_tracks.Values.Where(line => Incident(line, nodeId) && OnTrack(line, tangent)), Path("curves", curve, key));
            Set(curve, key, JsonValue.Create(Id(match)), Path("curves", curve, key));
            var canonical = key.EndsWith('1') ? "tangentLinkID1" : "tangentLinkID2";
            if (Field(curve, canonical) is null) Set(curve, canonical, JsonValue.Create(Id(match)), Path("curves", curve, canonical));
        }
    }
    private bool IsCurveVertex(JsonObject curve, (double X, double Y)? point)
    {
        if (point is null || Position(curve, "start") is not { } start || Position(curve, "end") is not { } end
            || Position(curve, "center") is not { } center) return false;
        bool OnTangent((double X, double Y) tangent)
        {
            var rx = tangent.X - center.X;
            var ry = tangent.Y - center.Y;
            var radius = Math.Sqrt(rx * rx + ry * ry);
            return radius > 0 && Math.Abs((point.Value.X - tangent.X) * rx + (point.Value.Y - tangent.Y) * ry) / radius <= _tolerance;
        }
        return OnTangent(start) && OnTangent(end);
    }
    private IEnumerable<string> ResolveStraightChain(JsonObject from, JsonObject to, string path)
    {
        if (Point(from) is not { } start || Point(to) is not { } end || Near(start, end))
        { Fail(path, "missing or ambiguous track endpoint geometry"); return []; }
        var current = Id(from);
        var target = Id(to);
        var result = new List<string>();
        var visited = new HashSet<string>();
        while (current != target)
        {
            if (!visited.Add(current)) Fail(path, "ambiguous track chain");
            var nodePoint = Point(_nodes[current])!.Value;
            var next = Unique(_tracks.Values.Where(line => Incident(line, current)
                && Point(line, "x1", "y1") is { } a && Point(line, "x2", "y2") is { } b
                && OnSegment(a, start, end) && OnSegment(b, start, end)
                && Outgoing(line, nodePoint) is { } direction
                && direction.X * (end.X - start.X) + direction.Y * (end.Y - start.Y) > 0), path);
            result.Add(Id(next));
            current = Ref(next, "fromNodeID") == current ? Ref(next, "toNodeID") : Ref(next, "fromNodeID");
            if (!_nodes.ContainsKey(current)) Fail(path, "track chain has an unbound endpoint");
        }
        return result;
    }
}
