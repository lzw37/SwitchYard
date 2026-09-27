using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace SwitchYard.Service.Services;

/// <summary>
/// Template libraries belong to a station scheme. The empty plan key preserves the
/// existing SQLite/MySQL schemas while separating libraries from execution plans.
/// </summary>
public static class SchemeTemplateStore
{
    public const string PlanKey = "";
    private const string Scope = "InstanceID = @instanceID AND StationSchemeID = @stationSchemeID";
    private static readonly object MigrationLock = new();

    public static void Migrate(DBConnector db, string instanceID, string stationSchemeID)
    {
        lock (MigrationLock)
        {
            var tables = new[] { "operationprocesstemplate", "traintemplate", "movementtemplate" }
                .Where(table => HasColumn(db, table, "OperationPlanID")).ToHashSet(StringComparer.Ordinal);
            if (!tables.Any(table => (db.Query<int>($"SELECT COUNT(1) FROM {table} WHERE {Scope} AND (OperationPlanID IS NULL OR OperationPlanID <> '')",
                    new { instanceID, stationSchemeID })?.FirstOrDefault() ?? 0) > 0)) return;

            db.BeginTransaction();
            try
            {
                // A scheme lock also serializes migration across MySQL service processes.
                if (DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName))
                    db.Query<string>("SELECT ID FROM stationscheme WHERE InstanceID = @instanceID AND ID = @stationSchemeID FOR UPDATE", new { instanceID, stationSchemeID });
                if (tables.Contains("operationprocesstemplate")) MigrateProcesses(db, instanceID, stationSchemeID);
                if (tables.Contains("traintemplate") && tables.Contains("movementtemplate")) MigrateTrains(db, instanceID, stationSchemeID);
                db.Commit();
            }
            catch { db.Rollback(); throw; }
        }
    }

    private static void MigrateProcesses(DBConnector db, string instanceID, string stationSchemeID)
    {
        var rows = db.Query<ProcessRow>($"SELECT OperationPlanID, TemplateID, Document FROM operationprocesstemplate WHERE {Scope} ORDER BY OperationPlanID, TemplateID", new { instanceID, stationSchemeID }) ?? new();
        var used = rows.Where(row => row.OperationPlanID == PlanKey).Select(row => row.TemplateID).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var reserved = rows.Select(row => row.TemplateID).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows.Where(row => row.OperationPlanID != PlanKey))
        {
            var id = ReserveID(row.TemplateID, row.OperationPlanID, 100, used, reserved);
            var document = JsonNode.Parse(row.Document)?.AsObject() ?? throw new InvalidOperationException("Stored process template is invalid.");
            document["operationPlanID"] = PlanKey;
            document["id"] = id;
            if (id != row.TemplateID) document["name"] = ImportedName(document["name"]?.GetValue<string>(), row.OperationPlanID, 200);
            db.ExecuteNonQuery($@"UPDATE operationprocesstemplate SET OperationPlanID = '', TemplateID = @id, Document = @document
                WHERE {Scope} AND TemplateID = @oldID AND (OperationPlanID = @oldPlanID OR (OperationPlanID IS NULL AND @oldPlanID IS NULL))",
                new { instanceID, stationSchemeID, id, document = document.ToJsonString(), oldID = row.TemplateID, oldPlanID = row.OperationPlanID });
            if (id != row.TemplateID) RebindSnapshotSource(db, instanceID, stationSchemeID, row.OperationPlanID, row.TemplateID, id);
        }
    }

    private static void RebindSnapshotSource(DBConnector db, string instanceID, string stationSchemeID, string? oldPlanID, string oldID, string id)
    {
        if (!HasColumn(db, TrainProcessSnapshotStore.TableName, "Document")) return;
        var snapshots = db.Query<SnapshotRow>($@"SELECT TrainID, Document FROM trainprocesssnapshot WHERE {Scope}
            AND (OperationPlanID = @oldPlanID OR (OperationPlanID IS NULL AND @oldPlanID IS NULL))",
            new { instanceID, stationSchemeID, oldPlanID }) ?? new();
        foreach (var row in snapshots)
        {
            var snapshot = JsonNode.Parse(row.Document)?.AsObject() ?? throw new InvalidOperationException("Stored process snapshot is invalid.");
            if (snapshot["sourceTemplateID"]?.GetValue<string>() != oldID) continue;
            snapshot["sourceTemplateID"] = id;
            if (snapshot["process"] is JsonObject process) process["id"] = id;
            // Only rebind identity; the historical constraints, revision and execution plan stay intact.
            db.ExecuteNonQuery($@"UPDATE trainprocesssnapshot SET Document = @document WHERE {Scope} AND TrainID = @trainID
                AND (OperationPlanID = @oldPlanID OR (OperationPlanID IS NULL AND @oldPlanID IS NULL))",
                new { instanceID, stationSchemeID, oldPlanID, trainID = row.TrainID, document = snapshot.ToJsonString() });
        }
    }

    private static void MigrateTrains(DBConnector db, string instanceID, string stationSchemeID)
    {
        var rows = db.Query<TrainRow>($"SELECT OperationPlanID, TrainTemplateID, Name FROM traintemplate WHERE {Scope} ORDER BY OperationPlanID, TrainTemplateID", new { instanceID, stationSchemeID }) ?? new();
        var used = rows.Where(row => row.OperationPlanID == PlanKey).Select(row => row.TrainTemplateID).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var reserved = rows.Select(row => row.TrainTemplateID).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows.Where(row => row.OperationPlanID != PlanKey))
        {
            var id = ReserveID(row.TrainTemplateID, row.OperationPlanID, 50, used, reserved);
            var parameters = new { instanceID, stationSchemeID, id, oldID = row.TrainTemplateID, oldPlanID = row.OperationPlanID,
                name = id == row.TrainTemplateID ? row.Name : ImportedName(row.Name, row.OperationPlanID, 50) };
            const string source = "TrainTemplateID = @oldID AND (OperationPlanID = @oldPlanID OR (OperationPlanID IS NULL AND @oldPlanID IS NULL))";
            db.ExecuteNonQuery($"UPDATE traintemplate SET OperationPlanID = '', TrainTemplateID = @id, Name = @name WHERE {Scope} AND {source}", parameters);
            db.ExecuteNonQuery($"UPDATE movementtemplate SET OperationPlanID = '', TrainTemplateID = @id WHERE {Scope} AND {source}", parameters);
            if (id != row.TrainTemplateID)
                foreach (var table in new[] { "train", "movement" })
                    if (HasColumn(db, table, "TrainTemplateID") && HasColumn(db, table, "OperationPlanID"))
                        db.ExecuteNonQuery($"UPDATE {table} SET TrainTemplateID = @id WHERE {Scope} AND {source}", parameters);
        }
    }

    private static string ReserveID(string original, string? planID, int maxLength, HashSet<string> used, HashSet<string> reserved)
    {
        if (used.Add(original)) return original;
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{planID}\n{original}")))[..10].ToLowerInvariant();
        for (var index = 1; ; index++)
        {
            var suffix = $"~{hash}{(index == 1 ? "" : $"-{index}")}";
            var candidate = original[..Math.Min(original.Length, maxLength - suffix.Length)] + suffix;
            if (reserved.Add(candidate) && used.Add(candidate)) return candidate;
        }
    }

    private static string ImportedName(string? name, string? planID, int maxLength)
    {
        var suffix = $"（来自 {planID ?? "默认计划"}）";
        if (suffix.Length > maxLength / 2) suffix = suffix[..(maxLength / 2 - 1)] + "）";
        var original = name ?? "模板";
        return original[..Math.Min(original.Length, maxLength - suffix.Length)] + suffix;
    }

    public static void DeleteScheme(DBConnector db, string instanceID, string stationSchemeID)
    {
        foreach (var table in new[] { "operationprocesstemplate", "movementtemplate", "traintemplate" })
            if (HasColumn(db, table, "StationSchemeID"))
                db.ExecuteNonQuery($"DELETE FROM {table} WHERE {Scope}", new { instanceID, stationSchemeID });
    }

    private static bool HasColumn(DBConnector db, string table, string column) => DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName)
        ? (db.Query<int>("SELECT COUNT(1) FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = @table AND column_name = @column", new { table, column })?.FirstOrDefault() ?? 0) > 0
        : (db.Query<ColumnRow>($"PRAGMA table_info({table})") ?? new()).Any(row => string.Equals(row.Name, column, StringComparison.OrdinalIgnoreCase));

    private sealed class ColumnRow { public string Name { get; set; } = ""; }
    private sealed class ProcessRow { public string? OperationPlanID { get; set; } public string TemplateID { get; set; } = ""; public string Document { get; set; } = ""; }
    private sealed class TrainRow { public string? OperationPlanID { get; set; } public string TrainTemplateID { get; set; } = ""; public string? Name { get; set; } }
    private sealed class SnapshotRow { public string TrainID { get; set; } = ""; public string Document { get; set; } = ""; }
}
