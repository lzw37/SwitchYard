using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace SwitchYard.StationLayout;

/// <summary>Assigns persisted identities once, then rewrites every topology reference atomically.</summary>
public static class StationLayoutIdentity
{
    public static readonly string[] Collections =
        ["nodes", "tracks", "curves", "signals", "insulationJoints", "bufferStops", "platforms", "switches", "cells", "annotations"];

    public static StationLayoutIdentityResult Assign(
        StationLayoutDocument document,
        IReadOnlyDictionary<string, HashSet<string>> persistedIds,
        IStationLayoutIdGenerator generator)
    {
        var root = JsonNode.Parse(document.ToJson())!.AsObject();
        EnsureTrackEndpoints(root);
        var maps = Collections.ToDictionary(name => name, _ => new Dictionary<string, string>(StringComparer.Ordinal));
        var used = persistedIds.Values.SelectMany(ids => ids).ToHashSet(StringComparer.Ordinal);
        foreach (var collection in Collections)
            foreach (var item in Items(root, collection))
                if (Text(item["id"]) is { Length: > 0 } id) used.Add(id);

        foreach (var collection in Collections)
        {
            persistedIds.TryGetValue(collection, out var existing);
            foreach (var item in Items(root, collection))
            {
                var old = Text(item["id"]);
                item.Remove("isNew");
                item.Remove("IsNew");
                if (old.Length > 0 && existing?.Contains(old) == true) continue;
                string id;
                var attempts = 0;
                do
                {
                    id = generator.NextId();
                    if (++attempts > 100 || string.IsNullOrWhiteSpace(id))
                        throw new StationLayoutValidationException("The ID generator did not produce a unique non-empty ID.");
                } while (!used.Add(id));
                if (old.Length > 0) maps[collection].Add(old, id);
                item["id"] = id;
                item.Remove("isNew");
                item.Remove("IsNew");
            }
        }

        RewriteReferences(root, maps);
        return new(StationLayoutDocument.FromJson(root.ToJsonString()), maps);
    }

    public static void RewriteReferences(JsonObject root, IReadOnlyDictionary<string, Dictionary<string, string>> maps)
    {
        var nodes = maps.GetValueOrDefault("nodes") ?? [];
        var tracks = maps.GetValueOrDefault("tracks") ?? [];
        foreach (var item in Items(root, "tracks")) RemapFields(item, nodes, "fromNodeID", "toNodeID");
        foreach (var item in Items(root, "nodes")) RemapList(item, "adjacentLineIDList", tracks);
        foreach (var name in new[] { "signals", "insulationJoints", "bufferStops", "switches" })
            foreach (var item in Items(root, name)) RemapFields(item, nodes, "bindingNodeID", "nodeID", "nodeId");
        foreach (var item in Items(root, "curves"))
        {
            RemapFields(item, nodes, "nodeID", "vertexNodeID", "bindingNodeID", "nodeId");
            RemapFields(item, tracks, "tangentLinkID1", "tangentLinkID2", "linkID1", "linkID2", "bindingLink1ID", "bindingLink2ID");
        }
        foreach (var item in Items(root, "switches"))
            foreach (var branch in (item["branchVectorList"] as JsonArray ?? []).OfType<JsonObject>())
                RemapFields(branch, tracks, "lineID", "bindingLinkID", "linkID");
        foreach (var item in Items(root, "cells")) RemapList(item, "linkIDList", tracks);
    }

    private static void RemapFields(JsonObject item, Dictionary<string, string> map, params string[] fields)
    {
        foreach (var key in item.Select(pair => pair.Key).Where(key => fields.Contains(key, StringComparer.OrdinalIgnoreCase)).ToArray())
            if (map.TryGetValue(Text(item[key]), out var id)) item[key] = id;
    }

    private static void RemapList(JsonObject item, string field, Dictionary<string, string> map)
    {
        foreach (var key in item.Select(pair => pair.Key).Where(key => key.Equals(field, StringComparison.OrdinalIgnoreCase)).ToArray())
        {
            if (item[key] is JsonArray array)
            {
                for (var i = 0; i < array.Count; i++)
                    if (map.TryGetValue(Text(array[i]), out var id)) array[i] = id;
            }
            else if (item[key] is JsonValue)
                item[key] = string.Join(",", Regex.Split(Text(item[key]), @"[\s,，;；]+")
                    .Where(id => id.Length > 0).Select(id => map.GetValueOrDefault(id, id)));
        }
    }

    private static void EnsureTrackEndpoints(JsonObject root)
    {
        if (root["nodes"] is not JsonArray nodes) root["nodes"] = nodes = [];
        var used = nodes.OfType<JsonObject>().Select(node => Text(node["id"])).ToHashSet(StringComparer.Ordinal);
        foreach (var track in Items(root, "tracks"))
        {
            for (var end = 1; end <= 2; end++)
            {
                var field = end == 1 ? "fromNodeID" : "toNodeID";
                if (Text(track[field]).Length > 0) continue;
                var x = track[$"x{end}"]!.GetValue<double>();
                var y = track[$"y{end}"]!.GetValue<double>();
                var candidates = nodes.OfType<JsonObject>().Where(node =>
                    Math.Abs(node["x"]!.GetValue<double>() - x) < 1e-9 &&
                    Math.Abs(node["y"]!.GetValue<double>() - y) < 1e-9).ToList();
                if (candidates.Count > 1)
                    throw new StationLayoutValidationException($"Track {Text(track["id"])} has an ambiguous unbound endpoint.");
                if (candidates.Count == 0)
                {
                    var index = nodes.Count;
                    string id;
                    do { id = "__new_endpoint_" + (index++).ToString(CultureInfo.InvariantCulture); } while (!used.Add(id));
                    var node = new JsonObject { ["id"] = id, ["x"] = x, ["y"] = y, ["adjacentLineIDList"] = new JsonArray() };
                    nodes.Add(node);
                    candidates.Add(node);
                }
                track[field] = Text(candidates[0]["id"]);
            }
        }
        foreach (var node in nodes.OfType<JsonObject>())
        {
            var id = Text(node["id"]);
            node["adjacentLineIDList"] = new JsonArray(Items(root, "tracks")
                .Where(track => Text(track["fromNodeID"]) == id || Text(track["toNodeID"]) == id)
                .Select(track => (JsonNode?)JsonValue.Create(Text(track["id"]))).ToArray());
        }
    }

    private static IEnumerable<JsonObject> Items(JsonObject root, string name) => (root[name] as JsonArray ?? []).OfType<JsonObject>();
    private static string Text(JsonNode? value) => value?.GetValue<string>() ?? string.Empty;
}

public sealed record StationLayoutIdentityResult(
    StationLayoutDocument Document,
    Dictionary<string, Dictionary<string, string>> IdMappings);
