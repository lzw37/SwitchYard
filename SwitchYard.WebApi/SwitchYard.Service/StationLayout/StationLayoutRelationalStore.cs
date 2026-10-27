using System.Globalization;
using System.Text.Json.Nodes;
using SwitchYard.StationLayout;

namespace SwitchYard.Service.StationLayout;

/// <summary>Each object is stored once in its own table; documents are assembled on demand.</summary>
public static class StationLayoutRelationalStore
{
    public sealed record Field(string Column, string Path, string Type = "text", params string[] Aliases);
    public sealed record Entity(string Collection, string Table, Field[] Fields);
    private static Field F(string column, string path, string type = "text", params string[] aliases) => new(column, path, type, aliases);

    public static readonly Entity[] Entities =
    [
        new("nodes", "node", [F("ID", "id"), F("X", "x", "real"), F("Y", "y", "real")]),
        new("tracks", "link", [F("ID", "id"), F("Name", "name"), F("FromNodeID", "fromNodeID"), F("ToNodeID", "toNodeID"),
            F("X1", "x1", "real"), F("Y1", "y1", "real"), F("X2", "x2", "real"), F("Y2", "y2", "real"),
            F("ArrowDirection", "arrowDirection"), F("ArrowType", "arrowType")]),
        new("curves", "curve", [F("ID", "id"), F("BindingNodeID", "nodeID", "text", "vertexNodeID", "bindingNodeID"),
            F("BindingLink1ID", "tangentLinkID1", "text", "linkID1", "bindingLink1ID"),
            F("BindingLink2ID", "tangentLinkID2", "text", "linkID2", "bindingLink2ID"),
            F("Radius", "radius", "real"), F("Angle", "angle", "real"), F("TangentDistance", "tangentDistance", "real"),
            F("StartX", "start.x", "real"), F("StartY", "start.y", "real"), F("EndX", "end.x", "real"), F("EndY", "end.y", "real"),
            F("CenterX", "center.x", "real"), F("CenterY", "center.y", "real"), F("LargeArcFlag", "largeArcFlag", "integer"), F("SweepFlag", "sweepFlag", "integer")]),
        new("signals", "signal", [F("ID", "id"), F("Name", "name"), F("Type", "type"), F("Direction", "direction"),
            F("BindingNodeID", "bindingNodeID", "text", "nodeID", "nodeId"), F("X", "position.x", "real"), F("Y", "position.y", "real")]),
        new("insulationJoints", "insulationjoint", [F("ID", "id"), F("Type", "type"),
            F("BindingNodeID", "bindingNodeID", "text", "nodeID", "nodeId"), F("X", "position.x", "real"), F("Y", "position.y", "real")]),
        new("bufferStops", "bufferstop", [F("ID", "id"), F("Type", "type"), F("Direction", "direction"),
            F("BindingNodeID", "bindingNodeID", "text", "nodeID", "nodeId"), F("X", "position.x", "real"), F("Y", "position.y", "real")]),
        new("platforms", "platform", [F("ID", "id"), F("Name", "name"), F("X", "x", "real"), F("Y", "y", "real"), F("Width", "width", "real"), F("Height", "height", "real")]),
        new("switches", "switch", [F("ID", "id"), F("Name", "name"), F("Type", "type"),
            F("BindingNodeID", "bindingNodeID", "text", "nodeID", "nodeId"), F("X", "position.x", "real"), F("Y", "position.y", "real")]),
        new("cells", "cell", [F("ID", "id"), F("Name", "name"), F("LinkIDList", "linkIDList")]),
        new("annotations", "annotation", [F("ID", "id"), F("Text", "text"), F("X", "position.x", "real"), F("Y", "position.y", "real"),
            F("FontFamily", "fontFamily"), F("FontSize", "fontSize", "real"), F("FontWeight", "fontWeight"), F("FontStyle", "fontStyle"),
            F("Angle", "angle", "real"), F("TextColor", "textColor")])
    ];
    public static readonly Field[] BranchFields = [F("X", "x", "real"), F("Y", "y", "real"), F("BindingLinkID", "lineID", "text", "bindingLinkID", "linkID")];

    public static string Q(string name) => "`" + name.Replace("`", "``", StringComparison.Ordinal) + "`";
    public static List<Dictionary<string, object?>> Rows(DBConnector db, string sql, object? parameters = null) =>
        (db.Query<dynamic>(sql, parameters) ?? []).Select(row => new Dictionary<string, object?>(
            (IDictionary<string, object?>)row, StringComparer.OrdinalIgnoreCase)).ToList();

    public static IReadOnlyDictionary<string, HashSet<string>> ExistingIds(DBConnector db, string scopeId, string schemeId) =>
        Entities.ToDictionary(entity => entity.Collection, entity =>
            (db.Query<string>($"SELECT ID FROM {Q(entity.Table)} WHERE InstanceID=@scopeId AND StationSchemeID=@schemeId", new { scopeId, schemeId }) ?? [])
            .ToHashSet(StringComparer.Ordinal));

    public static void Write(DBConnector db, string scopeId, string schemeId, StationLayoutDocument document)
    {
        var root = JsonNode.Parse(document.ToJson())!.AsObject();
        db.ExecuteNonQuery("DELETE FROM `switchbranchvector` WHERE InstanceID=@scopeId AND StationSchemeID=@schemeId", new { scopeId, schemeId });
        foreach (var entity in Entities)
        {
            db.ExecuteNonQuery($"DELETE FROM {Q(entity.Table)} WHERE InstanceID=@scopeId AND StationSchemeID=@schemeId", new { scopeId, schemeId });
            var sequence = 0;
            foreach (var item in (root[entity.Collection] as JsonArray ?? []).OfType<JsonObject>())
            {
                var extra = item.DeepClone().AsObject();
                var values = new Dictionary<string, object?>
                {
                    ["InstanceID"] = scopeId, ["StationSchemeID"] = schemeId, ["LayoutOrder"] = sequence++
                };
                AddFields(values, extra, item, entity.Fields);
                Remove(extra, "instanceID", "stationSchemeID", "isNew", "adjacentLineIDList", "branchVectorList");
                values["ExtraProperties"] = extra.ToJsonString();
                Insert(db, entity.Table, values);
                if (entity.Collection != "switches") continue;
                var branchSequence = 0;
                foreach (var branch in (item["branchVectorList"] as JsonArray ?? []).OfType<JsonObject>())
                {
                    var branchExtra = branch.DeepClone().AsObject();
                    var branchValues = new Dictionary<string, object?>
                    {
                        ["InstanceID"] = scopeId, ["StationSchemeID"] = schemeId,
                        ["SwitchID"] = values["ID"], ["Sequence"] = branchSequence++
                    };
                    AddFields(branchValues, branchExtra, branch, BranchFields);
                    branchValues["ExtraProperties"] = branchExtra.ToJsonString();
                    Insert(db, "switchbranchvector", branchValues);
                }
            }
        }

        var metadata = root["metadata"]?.DeepClone() as JsonObject ?? new();
        Remove(metadata, "instanceID", "stationSchemeID", "revision", "displayStyles", "gridSettings");
        var extensions = root.DeepClone().AsObject();
        Remove(extensions, [.. StationLayoutIdentity.Collections, "metadata", "format", "formatVersion"]);
        db.ExecuteNonQuery("UPDATE `stationscheme` SET LayoutMetadata=@metadata, LayoutExtensions=@extensions, " +
            "DisplayStyles=@styles, GridSettings=@grid WHERE InstanceID=@scopeId AND ID=@schemeId", new
            {
                scopeId, schemeId, metadata = metadata.ToJsonString(), extensions = extensions.ToJsonString(),
                styles = root["metadata"]?["displayStyles"]?.ToJsonString(), grid = root["metadata"]?["gridSettings"]?.ToJsonString()
            });
    }

    public static StationLayoutDocument Read(DBConnector db, string scopeId, string schemeId, long revision)
    {
        var scope = new { scopeId, schemeId };
        var scheme = Rows(db, "SELECT * FROM `stationscheme` WHERE InstanceID=@scopeId AND ID=@schemeId", scope).FirstOrDefault() ?? new();
        var root = ParseObject(scheme.GetValueOrDefault("LayoutExtensions"));
        var metadata = ParseObject(scheme.GetValueOrDefault("LayoutMetadata"));
        root["format"] = StationLayoutDocument.ArchiveFormat;
        root["formatVersion"] = 1;
        root["metadata"] = metadata;
        metadata["instanceID"] = scopeId;
        metadata["stationSchemeID"] = schemeId;
        metadata["revision"] = revision;
        metadata["latestElementID"] ??= 0;
        metadata["coordinateTransform"] ??= new JsonObject { ["applied"] = false, ["scale"] = 1 };
        foreach (var (column, field) in new[] { ("DisplayStyles", "displayStyles"), ("GridSettings", "gridSettings") })
            if (scheme.GetValueOrDefault(column) is string text && !string.IsNullOrWhiteSpace(text)) metadata[field] = JsonNode.Parse(text);

        foreach (var entity in Entities)
        {
            var array = new JsonArray();
            foreach (var row in Rows(db, $"SELECT * FROM {Q(entity.Table)} WHERE InstanceID=@scopeId AND StationSchemeID=@schemeId ORDER BY LayoutOrder, ID", scope))
                array.Add(ReadObject(row, entity.Fields));
            root[entity.Collection] = array;
        }
        var nodes = root["nodes"]!.AsArray().OfType<JsonObject>().ToDictionary(node => Text(node["id"]), StringComparer.Ordinal);
        var tracks = root["tracks"]!.AsArray().OfType<JsonObject>().ToList();
        foreach (var track in tracks)
        {
            // Historical rows store endpoints via their nodes. New rows retain exact drawing coordinates.
            FillPosition(track, "fromNodeID", nodes, "x1", "y1");
            FillPosition(track, "toNodeID", nodes, "x2", "y2");
        }
        foreach (var (id, node) in nodes)
            node["adjacentLineIDList"] = new JsonArray(tracks
                .Where(track => Text(track["fromNodeID"]) == id || Text(track["toNodeID"]) == id)
                .Select(track => (JsonNode?)JsonValue.Create(Text(track["id"]))).ToArray());

        foreach (var collection in new[] { "signals", "insulationJoints", "bufferStops", "switches" })
            foreach (var item in root[collection]!.AsArray().OfType<JsonObject>())
            {
                FillPosition(item, "bindingNodeID", nodes, "position.x", "position.y");
                if (collection == "switches") item["branchVectorList"] = new JsonArray();
            }
        var switches = root["switches"]!.AsArray().OfType<JsonObject>().ToDictionary(item => Text(item["id"]), StringComparer.Ordinal);
        foreach (var row in Rows(db, "SELECT * FROM `switchbranchvector` WHERE InstanceID=@scopeId AND StationSchemeID=@schemeId ORDER BY SwitchID, Sequence", scope))
            if (switches.TryGetValue(Convert.ToString(row["SwitchID"], CultureInfo.InvariantCulture)!, out var sw))
                sw["branchVectorList"]!.AsArray().Add(ReadObject(row, BranchFields));
        foreach (var cell in root["cells"]!.AsArray().OfType<JsonObject>())
        {
            cell["instanceID"] = scopeId;
            cell["stationSchemeID"] = schemeId;
        }
        return StationLayoutDocument.FromJson(root.ToJsonString());
    }

    private static void FillPosition(JsonObject item, string binding, Dictionary<string, JsonObject> nodes, string x, string y)
    {
        if (!nodes.TryGetValue(Text(item[binding]), out var node)) return;
        if (Get(item, x) is null) Set(item, x, node["x"]?.DeepClone());
        if (Get(item, y) is null) Set(item, y, node["y"]?.DeepClone());
    }

    private static void AddFields(Dictionary<string, object?> values, JsonObject extra, JsonObject item, Field[] fields)
    {
        foreach (var field in fields)
        {
            var value = Get(item, field.Path) ?? field.Aliases.Select(alias => Get(item, alias)).FirstOrDefault(node => node is not null);
            values[field.Column] = value is null ? null : field.Type switch
            {
                "real" => value.GetValue<double>(),
                "integer" => value.GetValue<int>(),
                _ => value.GetValue<string>()
            };
            DeletePath(extra, field.Path);
            foreach (var alias in field.Aliases) DeletePath(extra, alias);
        }
    }

    private static JsonObject ReadObject(Dictionary<string, object?> row, Field[] fields)
    {
        var item = ParseObject(row.GetValueOrDefault("ExtraProperties"));
        foreach (var field in fields)
        {
            DeletePath(item, field.Path);
            foreach (var alias in field.Aliases) DeletePath(item, alias);
            if (row.GetValueOrDefault(field.Column) is not { } value || value is DBNull) continue;
            JsonNode? node = field.Type switch
            {
                "real" => JsonValue.Create(Convert.ToDouble(value, CultureInfo.InvariantCulture)),
                "integer" => JsonValue.Create(Convert.ToInt32(value, CultureInfo.InvariantCulture)),
                _ => JsonValue.Create(Convert.ToString(value, CultureInfo.InvariantCulture))
            };
            Set(item, field.Path, node);
        }
        return item;
    }

    private static void Insert(DBConnector db, string table, Dictionary<string, object?> values) =>
        db.ExecuteNonQuery($"INSERT INTO {Q(table)} ({string.Join(",", values.Keys.Select(Q))}) VALUES ({string.Join(",", values.Keys.Select(key => "@" + key))})", values);

    private static JsonObject ParseObject(object? value) => value is string text && !string.IsNullOrWhiteSpace(text)
        ? JsonNode.Parse(text)!.AsObject() : new();
    private static string Text(JsonNode? node) => node?.GetValue<string>() ?? string.Empty;
    private static JsonNode? Get(JsonObject item, string path)
    {
        JsonNode? current = item;
        foreach (var part in path.Split('.')) current = (current as JsonObject)?[part];
        return current;
    }
    private static void Set(JsonObject item, string path, JsonNode? value)
    {
        var parts = path.Split('.');
        foreach (var part in parts[..^1])
        {
            if (item[part] is not JsonObject child) item[part] = child = new();
            item = child;
        }
        item[parts[^1]] = value;
    }
    private static void DeletePath(JsonObject item, string path)
    {
        var parts = path.Split('.');
        if (parts.Length == 1) { Remove(item, parts[0]); return; }
        if (item[parts[0]] is not JsonObject child) return;
        Remove(child, parts[1]);
        if (child.Count == 0) item.Remove(parts[0]);
    }
    private static void Remove(JsonObject item, params string[] names)
    {
        foreach (var key in names) item.Remove(key);
    }
}
