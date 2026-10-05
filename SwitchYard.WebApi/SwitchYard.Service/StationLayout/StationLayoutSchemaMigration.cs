using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using SwitchYard.StationLayout;
using static SwitchYard.Service.StationLayout.StationLayoutRelationalStore;

namespace SwitchYard.Service.StationLayout;

/// <summary>Upgrades existing rows before the application accepts requests.</summary>
public static class StationLayoutSchemaMigration
{
    public static void Apply(DBConnector db, bool mysql)
    {
        // DDL is idempotent. MySQL commits DDL implicitly; data conversion below has its own transaction.
        foreach (var entity in Entities)
        {
            foreach (var field in entity.Fields) AddColumn(db, mysql, entity.Table, field.Column, SqlType(field.Type, mysql));
            AddColumn(db, mysql, entity.Table, "ExtraProperties", mysql ? "LONGTEXT" : "TEXT");
            AddColumn(db, mysql, entity.Table, "LayoutOrder", mysql ? "INT" : "INTEGER");
        }
        AddColumn(db, mysql, "switchbranchvector", "ExtraProperties", mysql ? "LONGTEXT" : "TEXT");
        AddColumn(db, mysql, "stationscheme", "LayoutMetadata", mysql ? "LONGTEXT" : "TEXT");
        AddColumn(db, mysql, "stationscheme", "LayoutExtensions", mysql ? "LONGTEXT" : "TEXT");
        if (mysql && Columns(db, true, "cell").Any(column => column.Name == "LinkIDList" && column.Type != "longtext"))
            db.ExecuteNonQuery("ALTER TABLE `cell` MODIFY COLUMN `LinkIDList` LONGTEXT NULL");

        foreach (var table in Tables(db, mysql))
        {
            var tableColumns = Columns(db, mysql, table);
            var changes = tableColumns.Where(column =>
                    IsIdColumn(column.Name) &&
                    Regex.IsMatch(column.Type, @"^(?:TINYINT|SMALLINT|MEDIUMINT|INT|INTEGER|BIGINT)\b", RegexOptions.IgnoreCase))
                .ToDictionary(column => column.Name, _ => "TEXT", StringComparer.OrdinalIgnoreCase);
            if (table == "curve" && Columns(db, mysql, table).Any(column => column.Name == "Radius" && column.Type.Contains("INT", StringComparison.OrdinalIgnoreCase)))
                changes["Radius"] = "REAL";
            if (mysql)
            {
                foreach (var (column, type) in changes)
                {
                    var sqlType = type == "TEXT"
                        ? $"VARCHAR({(IsLayoutIdColumn(table, column) ? 255 : 50)}) CHARACTER SET utf8mb4 COLLATE utf8mb4_bin"
                        : "DOUBLE";
                    db.ExecuteNonQuery($"ALTER TABLE {Q(table)} MODIFY COLUMN {Q(column)} {sqlType} {(tableColumns.Single(item => item.Name == column).Nullable == "NO" ? "NOT NULL" : "NULL")}");
                }
                foreach (var column in tableColumns.Where(column => IsIdColumn(column.Name)
                    && !changes.ContainsKey(column.Name) && !string.IsNullOrEmpty(column.Collation)))
                {
                    var length = Regex.Match(column.Type, @"^(?:var)?char\((\d+)\)", RegexOptions.IgnoreCase);
                    // Only drawing identities/references need room for restored original IDs.
                    // Business keys already store strings; widening every part of a composite key
                    // to utf8mb4 VARCHAR(255) exceeds MySQL's 3072-byte limit for four-part keys.
                    // Preserve existing business widths, including DDL committed by an interrupted run.
                    var type = IsLayoutIdColumn(table, column.Name) && length.Success
                        && int.Parse(length.Groups[1].Value, CultureInfo.InvariantCulture) < 255 ? "VARCHAR(255)" : column.Type;
                    if (type == column.Type && column.Collation == "utf8mb4_bin") continue;
                    db.ExecuteNonQuery($"ALTER TABLE {Q(table)} MODIFY COLUMN {Q(column.Name)} {type} CHARACTER SET utf8mb4 COLLATE utf8mb4_bin {(column.Nullable == "NO" ? "NOT NULL" : "NULL")}");
                }
            }
            else if (changes.Count > 0) RetypeSqliteTable(db, table, changes);
        }

        if (!Columns(db, mysql, "stationscheme").Any(column => column.Name.Equals("LayoutDocument", StringComparison.OrdinalIgnoreCase))) return;
        var snapshots = Rows(db, "SELECT InstanceID, ID, LayoutDocument FROM `stationscheme` WHERE LayoutDocument IS NOT NULL AND TRIM(LayoutDocument)<>''");
        foreach (var row in snapshots)
        {
            var scopeId = Convert.ToString(row["InstanceID"], CultureInfo.InvariantCulture)!;
            var schemeId = Convert.ToString(row["ID"], CultureInfo.InvariantCulture)!;
            db.BeginTransaction();
            try
            {
                var document = RestoreOriginalIds(db, scopeId, schemeId, (string)row["LayoutDocument"]!);
                Write(db, scopeId, schemeId, document);
                // A committed conversion is not repeated after an interrupted deployment.
                db.ExecuteNonQuery("UPDATE `stationscheme` SET LayoutDocument=NULL WHERE InstanceID=@scopeId AND ID=@schemeId", new { scopeId, schemeId });
                db.Commit();
            }
            catch { db.Rollback(); throw; }
        }
        db.ExecuteNonQuery("ALTER TABLE `stationscheme` DROP COLUMN `LayoutDocument`");
    }

    private static StationLayoutDocument RestoreOriginalIds(DBConnector db, string scopeId, string schemeId, string json)
    {
        var document = StationLayoutDocument.FromJson(json);
        if (!document.IsArchive) throw new InvalidDataException($"Scheme {schemeId}: unsupported historical layout format; migration stopped without removing it.");
        var root = JsonNode.Parse(json)!.AsObject();
        var nodeMap = HistoricalIds(document.Nodes.Select(node => node.ID!));
        var linkMap = HistoricalIds(document.Tracks.Select(track => track.ID!));
        var scope = new { scopeId, schemeId };
        var storedNodes = Rows(db, "SELECT * FROM `node` WHERE InstanceID=@scopeId AND StationSchemeID=@schemeId", scope)
            .ToDictionary(row => Convert.ToString(row["ID"], CultureInfo.InvariantCulture)!);
        var storedLinks = Rows(db, "SELECT * FROM `link` WHERE InstanceID=@scopeId AND StationSchemeID=@schemeId", scope)
            .ToDictionary(row => Convert.ToString(row["ID"], CultureInfo.InvariantCulture)!);
        if (storedLinks.Count != linkMap.Count || linkMap.Keys.Any(id => !storedLinks.ContainsKey(id)) || nodeMap.Keys.Any(id => !storedNodes.ContainsKey(id)))
            throw new InvalidDataException($"Scheme {schemeId}: layout and relational topology disagree; migration stopped before replacing data.");

        var nodeBySource = document.Nodes.ToDictionary(node => node.ID!, StringComparer.Ordinal);
        foreach (var (old, original) in nodeMap)
        {
            var source = nodeBySource[original];
            var row = storedNodes[old];
            if (Math.Abs(Convert.ToDouble(row["X"]) - source.X) > .00051 || Math.Abs(Convert.ToDouble(row["Y"]) - source.Y) > .00051)
                throw new InvalidDataException($"Scheme {schemeId}: node {original} differs between historical storage forms; migration stopped.");
        }
        var used = nodeMap.Values.ToHashSet(StringComparer.Ordinal);
        foreach (var (id, row) in storedNodes.Where(pair => !nodeMap.ContainsKey(pair.Key)))
        {
            // Old saves materialized unbound endpoints outside the archive. Retain those nodes too.
            var original = id;
            if (!used.Add(original))
                throw new InvalidDataException($"Scheme {schemeId}: generated node {id} conflicts with an original ID; migration stopped.");
            nodeMap[id] = original;
            root["nodes"]!.AsArray().Add(new JsonObject { ["id"] = original,
                ["x"] = Convert.ToDouble(row["X"]), ["y"] = Convert.ToDouble(row["Y"]) });
        }
        var trackBySource = root["tracks"]!.AsArray().OfType<JsonObject>().ToDictionary(track => track["id"]!.GetValue<string>());
        foreach (var (old, original) in linkMap)
        {
            var track = trackBySource[original];
            var row = storedLinks[old];
            foreach (var (column, field) in new[] { ("FromNodeID", "fromNodeID"), ("ToNodeID", "toNodeID") })
            {
                var endpoint = nodeMap[Convert.ToString(row[column], CultureInfo.InvariantCulture)!];
                if (track[field] is JsonValue value && !string.IsNullOrWhiteSpace(value.GetValue<string>()) && value.GetValue<string>() != endpoint)
                    throw new InvalidDataException($"Scheme {schemeId}: track {original} has inconsistent endpoints; migration stopped.");
                track[field] = endpoint;
            }
        }
        MergeRelationalValues(db, scopeId, schemeId, root, nodeMap, linkMap);
        RewriteBusinessReferences(db, scopeId, schemeId, nodeMap, linkMap);
        var repaired = StationLayoutTopologyRepair.Process(root.ToJsonString(), 1);
        return StationLayoutDocument.FromJson(repaired.Json);
    }

    private static void MergeRelationalValues(DBConnector db, string scopeId, string schemeId, JsonObject root,
        Dictionary<string, string> nodeMap, Dictionary<string, string> linkMap)
    {
        // The archive supplies original IDs and properties the old tables could not represent.
        // Current relational values remain authoritative, including independent names/binding edits.
        foreach (var entity in Entities)
        {
            var source = (root[entity.Collection] as JsonArray ?? []).OfType<JsonObject>()
                .ToDictionary(item => item["id"]!.GetValue<string>(), StringComparer.Ordinal);
            var rows = Rows(db, $"SELECT * FROM {Q(entity.Table)} WHERE InstanceID=@scopeId AND StationSchemeID=@schemeId", new { scopeId, schemeId });
            if (rows.Count != source.Count) throw new InvalidDataException($"Scheme {schemeId}: {entity.Table} objects differ between storage forms; migration stopped.");
            foreach (var row in rows)
            {
                var storedId = Convert.ToString(row["ID"], CultureInfo.InvariantCulture)!;
                var originalId = entity.Collection == "nodes" ? nodeMap[storedId] : entity.Collection == "tracks" ? linkMap[storedId] : storedId;
                if (!source.TryGetValue(originalId, out var item))
                    throw new InvalidDataException($"Scheme {schemeId}: {entity.Table} ID {storedId} is absent from the historical archive; migration stopped.");
                foreach (var field in entity.Fields.Where(field => field.Column != "ID"))
                {
                    var raw = GetPath(item, field.Path) ?? field.Aliases.Select(alias => GetPath(item, alias)).FirstOrDefault(value => value is not null);
                    var actual = row.GetValueOrDefault(field.Column);
                    // These geometry columns were absent from the old schema.
                    if (actual is null && (entity.Collection == "tracks" && field.Column is "X1" or "X2" or "Y1" or "Y2"
                        || field.Path.StartsWith("position.", StringComparison.Ordinal) && entity.Collection != "annotations")) continue;
                    if (entity.Collection == "nodes" && field.Column is "X" or "Y") continue; // Already verified against rounded projection.
                    if (actual is not null && field.Column is "BindingNodeID" or "FromNodeID" or "ToNodeID")
                        actual = nodeMap.GetValueOrDefault(Convert.ToString(actual, CultureInfo.InvariantCulture)!, Convert.ToString(actual, CultureInfo.InvariantCulture)!);
                    if (actual is not null && field.Column is "BindingLink1ID" or "BindingLink2ID")
                        actual = linkMap.GetValueOrDefault(Convert.ToString(actual, CultureInfo.InvariantCulture)!, Convert.ToString(actual, CultureInfo.InvariantCulture)!);
                    if (actual is not null && field.Column == "LinkIDList") actual = RewriteList(Convert.ToString(actual, CultureInfo.InvariantCulture)!, linkMap);

                    var expected = LegacyValue(entity.Collection, field, raw, originalId);
                    if (Equals(Convert.ToString(expected, CultureInfo.InvariantCulture), Convert.ToString(actual, CultureInfo.InvariantCulture))) continue;
                    JsonNode? replacement = actual is null ? null : field.Type switch
                    {
                        "real" => JsonValue.Create(Convert.ToDouble(actual, CultureInfo.InvariantCulture)),
                        "integer" => JsonValue.Create(Convert.ToInt32(actual, CultureInfo.InvariantCulture)),
                        _ => JsonValue.Create(Convert.ToString(actual, CultureInfo.InvariantCulture))
                    };
                    SetPath(item, field.Path, replacement);
                }
            }
        }
        var switches = (root["switches"] as JsonArray ?? []).OfType<JsonObject>().ToDictionary(item => item["id"]!.GetValue<string>());
        var branches = Rows(db, "SELECT * FROM `switchbranchvector` WHERE InstanceID=@scopeId AND StationSchemeID=@schemeId ORDER BY SwitchID, Sequence", new { scopeId, schemeId });
        foreach (var (id, sw) in switches)
        {
            var source = (sw["branchVectorList"] as JsonArray ?? []).OfType<JsonObject>().ToList();
            var result = new JsonArray();
            foreach (var row in branches.Where(row => Convert.ToString(row["SwitchID"], CultureInfo.InvariantCulture) == id))
            {
                var oldLink = Convert.ToString(row["BindingLinkID"], CultureInfo.InvariantCulture) ?? "";
                var link = linkMap.GetValueOrDefault(oldLink, oldLink);
                var item = source.FirstOrDefault(branch => branch["lineID"]?.GetValue<string>() == link)?.DeepClone().AsObject() ?? new JsonObject();
                item["lineID"] = link;
                item["x"] = Convert.ToDouble(row["X"], CultureInfo.InvariantCulture);
                item["y"] = Convert.ToDouble(row["Y"], CultureInfo.InvariantCulture);
                result.Add(item);
            }
            sw["branchVectorList"] = result;
        }
        var scheme = Rows(db, "SELECT DisplayStyles, GridSettings FROM `stationscheme` WHERE InstanceID=@scopeId AND ID=@schemeId", new { scopeId, schemeId }).Single();
        if (root["metadata"] is not JsonObject metadata) root["metadata"] = metadata = new();
        foreach (var (column, field) in new[] { ("DisplayStyles", "displayStyles"), ("GridSettings", "gridSettings") })
            if (scheme.GetValueOrDefault(column) is string text && !string.IsNullOrWhiteSpace(text)) metadata[field] = JsonNode.Parse(text);
    }

    private static object? LegacyValue(string collection, Field field, JsonNode? raw, string id)
    {
        if (field.Type != "text")
        {
            var value = raw?.GetValue<double>() ?? 0;
            if (field.Column == "Radius") return Math.Round(Math.Min(value <= 0 ? 100 : value, int.MaxValue), MidpointRounding.AwayFromZero);
            if (field.Column == "FontSize") return value <= 0 ? 16 : value;
            return value;
        }
        var text = raw?.GetValue<string>();
        if (field.Column == "Name") return collection == "tracks" ? text ?? "" : string.IsNullOrWhiteSpace(text) ? id : text.Trim();
        if (!string.IsNullOrWhiteSpace(text)) return text;
        return (collection, field.Column) switch
        {
            ("signals", "Type") => "departure", ("signals", "Direction") => "e",
            ("insulationJoints", "Type") => "normal", ("bufferStops", "Type") => "normal",
            ("bufferStops", "Direction") => "right", ("switches", "Type") => "unknown",
            ("annotations", "FontFamily") => "Arial", ("annotations", "FontWeight" or "FontStyle") => "normal",
            ("annotations", "TextColor") => "#ffffff", _ => text ?? ""
        };
    }

    private static JsonNode? GetPath(JsonObject item, string path)
    {
        JsonNode? value = item;
        foreach (var part in path.Split('.')) value = (value as JsonObject)?[part];
        return value;
    }
    private static void SetPath(JsonObject item, string path, JsonNode? value)
    {
        var parts = path.Split('.');
        foreach (var part in parts[..^1])
        {
            if (item[part] is not JsonObject child) item[part] = child = new();
            item = child;
        }
        if (value is null) item.Remove(parts[^1]); else item[parts[^1]] = value;
    }

    // Only migration reproduces the old allocation order to recover original IDs.
    private static Dictionary<string, string> HistoricalIds(IEnumerable<string> source)
    {
        var used = new HashSet<int>();
        var next = 0;
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var id in source)
        {
            int allocated;
            if (int.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) && number >= 0 && used.Add(number))
            { allocated = number; next = Math.Max(next, number + 1); }
            else
            { while (used.Contains(next)) next++; allocated = next++; used.Add(allocated); }
            result.Add(allocated.ToString(CultureInfo.InvariantCulture), id);
        }
        return result;
    }

    private static readonly string[] NodeFields = ["BindingNodeID", "StartNodeID", "EndNodeID", "NodeID"];
    private static readonly string[] LinkFields = ["TrackID", "LinkID", "BindingLinkID"];
    private static readonly string[] NodeLists = ["NodeList", "NodeIds"];
    private static readonly string[] LinkLists = ["LinkList", "LinkIDList", "LinkIds"];

    private static void RewriteBusinessReferences(DBConnector db, string scopeId, string schemeId,
        Dictionary<string, string> nodes, Dictionary<string, string> links)
    {
        var mysql = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName);
        var layoutTables = Entities.Select(entity => entity.Table).Append("switchbranchvector").ToHashSet();
        foreach (var table in Tables(db, mysql).Where(table => !layoutTables.Contains(table)))
        {
            var columns = Columns(db, mysql, table);
            if (!columns.Any(column => column.Name == "InstanceID") || !columns.Any(column => column.Name == "StationSchemeID")) continue;
            foreach (var column in columns)
            {
                var map = NodeFields.Contains(column.Name, StringComparer.OrdinalIgnoreCase) || NodeLists.Contains(column.Name, StringComparer.OrdinalIgnoreCase) ? nodes
                    : LinkFields.Contains(column.Name, StringComparer.OrdinalIgnoreCase) || LinkLists.Contains(column.Name, StringComparer.OrdinalIgnoreCase) ? links : null;
                var list = NodeLists.Concat(LinkLists).Contains(column.Name, StringComparer.OrdinalIgnoreCase);
                var structured = column.Name is "Document" or "ChartsJson";
                if (map is null && !structured) continue;
                var values = db.Query<string>($"SELECT DISTINCT {Q(column.Name)} FROM {Q(table)} WHERE InstanceID=@scopeId AND StationSchemeID=@schemeId AND {Q(column.Name)} IS NOT NULL", new { scopeId, schemeId }) ?? [];
                var changes = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var value in values)
                {
                    string updated;
                    if (structured)
                    {
                        var tree = JsonNode.Parse(value)!;
                        RewriteJson(tree, nodes, links);
                        updated = tree.ToJsonString();
                    }
                    else updated = list ? RewriteList(value, map!) : map!.GetValueOrDefault(value, value);
                    if (updated != value) changes[value] = updated;
                }
                if (changes.Count == 0) continue;
                // CASE evaluates the original value once, so overlapping old/new IDs cannot cascade.
                var args = new Dictionary<string, object?> { ["scopeId"] = scopeId, ["schemeId"] = schemeId };
                var cases = new List<string>();
                foreach (var (old, updated) in changes)
                {
                    var index = cases.Count;
                    args[$"old{index}"] = old; args[$"new{index}"] = updated;
                    cases.Add($"WHEN @old{index} THEN @new{index}");
                }
                db.ExecuteNonQuery($"UPDATE {Q(table)} SET {Q(column.Name)}=CASE {Q(column.Name)} {string.Join(" ", cases)} ELSE {Q(column.Name)} END WHERE InstanceID=@scopeId AND StationSchemeID=@schemeId", args);
            }
        }
    }

    private static string RewriteList(string value, Dictionary<string, string> map) =>
        Regex.Replace(value, @"[^\s,，;；]+", match => map.GetValueOrDefault(match.Value, match.Value));
    private static void RewriteJson(JsonNode node, Dictionary<string, string> nodes, Dictionary<string, string> links)
    {
        if (node is JsonArray array) { foreach (var item in array) if (item is not null) RewriteJson(item, nodes, links); return; }
        if (node is not JsonObject obj) return;
        foreach (var (key, value) in obj.ToArray())
        {
            var map = NodeFields.Concat(NodeLists).Contains(key, StringComparer.OrdinalIgnoreCase) ? nodes
                : LinkFields.Concat(LinkLists).Contains(key, StringComparer.OrdinalIgnoreCase) ? links : null;
            if (map is not null && value is JsonValue scalar && scalar.TryGetValue<string>(out var text)) obj[key] = RewriteList(text, map);
            else if (map is not null && value is JsonArray ids)
            { for (var i = 0; i < ids.Count; i++) if (ids[i] is JsonValue id && id.TryGetValue<string>(out var textId)) ids[i] = map.GetValueOrDefault(textId, textId); }
            else if (value is not null) RewriteJson(value, nodes, links);
        }
    }

    public sealed class Column
    {
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public string? Collation { get; set; }
        public string? Nullable { get; set; }
    }
    private static bool IsIdColumn(string name) => name.Equals("id", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith("ID", StringComparison.Ordinal) || name.EndsWith("Id", StringComparison.Ordinal)
        || name.EndsWith("_id", StringComparison.OrdinalIgnoreCase);
    private static bool IsLayoutIdColumn(string table, string column) =>
        Entities.Any(entity => entity.Table.Equals(table, StringComparison.OrdinalIgnoreCase)
            && entity.Fields.Any(field => field.Column.Equals(column, StringComparison.OrdinalIgnoreCase) && IsIdColumn(field.Column)))
        || NodeFields.Concat(LinkFields).Concat(["SwitchID", "CellID", "SignalID"]).Contains(column, StringComparer.OrdinalIgnoreCase);
    private static List<Column> Columns(DBConnector db, bool mysql, string table) => db.Query<Column>(mysql
        ? "SELECT COLUMN_NAME AS Name, COLUMN_TYPE AS Type, COLLATION_NAME AS Collation, IS_NULLABLE AS Nullable FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME=@table"
        : $"PRAGMA table_info({Q(table)})", new { table }) ?? [];
    private static List<string> Tables(DBConnector db, bool mysql) => db.Query<string>(mysql
        ? "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_TYPE='BASE TABLE'"
        : "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'") ?? [];
    private static string SqlType(string type, bool mysql) => type switch
    { "real" => mysql ? "DOUBLE" : "REAL", "integer" => mysql ? "INT" : "INTEGER", _ => mysql ? "LONGTEXT" : "TEXT" };
    private static void AddColumn(DBConnector db, bool mysql, string table, string column, string type)
    {
        if (!Columns(db, mysql, table).Any(existing => existing.Name.Equals(column, StringComparison.OrdinalIgnoreCase)))
            db.ExecuteNonQuery($"ALTER TABLE {Q(table)} ADD COLUMN {Q(column)} {type} NULL");
    }

    private static void RetypeSqliteTable(DBConnector db, string table, Dictionary<string, string> changes)
    {
        var sql = db.Query<string>("SELECT sql FROM sqlite_master WHERE type='table' AND name=@table", new { table })!.Single();
        var objects = db.Query<string>("SELECT sql FROM sqlite_master WHERE tbl_name=@table AND type IN ('index','trigger') AND sql IS NOT NULL", new { table }) ?? [];
        var temporary = "__station_layout_migration_" + table;
        var create = Regex.Replace(sql, @"^CREATE\s+TABLE\s+(?:IF\s+NOT\s+EXISTS\s+)?(?:""[^""]+""|`[^`]+`|\[[^\]]+\]|\w+)", "CREATE TABLE " + Q(temporary), RegexOptions.IgnoreCase);
        foreach (var (column, type) in changes)
            create = Regex.Replace(create, $@"(?<!\w)([""`\[]?{Regex.Escape(column)}[""`\]]?\s+)(?:TINYINT|SMALLINT|MEDIUMINT|INTEGER|BIGINT|INT)\b", "$1" + type, RegexOptions.IgnoreCase);
        var columns = Columns(db, false, table).Select(column => column.Name).ToList();
        var select = string.Join(",", columns.Select(column => changes.TryGetValue(column, out var type) ? $"CAST({Q(column)} AS {type})" : Q(column)));
        db.BeginTransaction();
        try
        {
            db.ExecuteNonQuery(create);
            db.ExecuteNonQuery($"INSERT INTO {Q(temporary)} ({string.Join(",", columns.Select(Q))}) SELECT {select} FROM {Q(table)}");
            db.ExecuteNonQuery($"DROP TABLE {Q(table)}");
            db.ExecuteNonQuery($"ALTER TABLE {Q(temporary)} RENAME TO {Q(table)}");
            foreach (var statement in objects) db.ExecuteNonQuery(statement);
            db.Commit();
        }
        catch { db.Rollback(); throw; }
    }
}
