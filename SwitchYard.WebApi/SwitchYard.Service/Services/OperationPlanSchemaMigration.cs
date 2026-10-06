using SwitchYard.Capacity;
using SwitchYard.Service.Controllers;
using SwitchYard.Service.Models;

namespace SwitchYard.Service.Services;

/// <summary>Startup-only upgrades. Request handlers never run DDL or historical backfills.</summary>
public static class OperationPlanSchemaMigration
{
    public static void EnsureSchema(DBConnector db)
    {
        var mysql = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName);
        db.ExecuteNonQuery(mysql
            ? "CREATE TABLE IF NOT EXISTS capacityschemamigration (Version VARCHAR(100) CHARACTER SET ascii COLLATE ascii_bin PRIMARY KEY, AppliedAtUtc DATETIME NOT NULL)"
            : "CREATE TABLE IF NOT EXISTS capacityschemamigration (Version TEXT PRIMARY KEY, AppliedAtUtc TEXT NOT NULL)");
        if (Completed(db, "plan-chart-schema-v1")) return;
        OperationPlanController.EnsureSchemaForMigration(db);
        StationLayoutController.EnsureChartSchemaForMigration(db);
        OperationProcessController.EnsureSchema(db);
        TrainProcessSnapshotStore.EnsureSchema(db);

        var keys = new Dictionary<string, string[]>
        {
            ["capacityinstance"] = ["ID"], ["stationscheme"] = ["InstanceID", "ID"],
            ["operationplan"] = ["InstanceID", "StationSchemeID", "OperationPlanID"],
            ["train"] = ["InstanceID", "StationSchemeID", "OperationPlanID", "ID"],
            ["movement"] = ["InstanceID", "StationSchemeID", "OperationPlanID", "TrainID", "MovementID"],
            ["traintemplate"] = ["InstanceID", "StationSchemeID", "OperationPlanID", "TrainTemplateID"],
            ["movementtemplate"] = ["InstanceID", "StationSchemeID", "OperationPlanID", "TrainTemplateID", "MovementID"],
            ["stationroutetime"] = ["InstanceID", "StationSchemeID", "RouteID", "TrainTypeID"],
            ["switchbranchvector"] = ["InstanceID", "StationSchemeID", "SwitchID"]
        };
        foreach (var table in new[] { "node", "link", "cell", "stationroute", "stationrouteend", "signal", "switch", "platform", "bufferstop", "insulationjoint", "curve", "annotation" })
            keys[table] = ["InstanceID", "StationSchemeID", "ID"];
        foreach (var table in CapacityDataLifecycle.PlanTables.Where(table => table.StartsWith("operation", StringComparison.Ordinal)))
            keys[table] = ["InstanceID", "StationSchemeID", "OperationPlanID"];
        foreach (var (table, columns) in keys) EnsureIndex(db, mysql, table, columns);
        MarkCompleted(db, "plan-chart-schema-v1");
    }

    public static void MigrateData(DBConnector db)
    {
        if (!Completed(db, "plan-chart-backfill-v1"))
        {
            var scopes = db.Query<ProcessScope>("SELECT DISTINCT InstanceID, StationSchemeID FROM traintemplate UNION SELECT DISTINCT InstanceID, StationSchemeID FROM movementtemplate UNION SELECT DISTINCT InstanceID, StationSchemeID FROM operationprocesstemplate") ?? [];
            foreach (var scope in scopes.Where(scope => !string.IsNullOrWhiteSpace(scope.InstanceID) && !string.IsNullOrWhiteSpace(scope.StationSchemeID)))
                SchemeTemplateStore.Migrate(db, scope.InstanceID, scope.StationSchemeID);
            db.BeginTransaction();
            try { OperationPlanController.BackfillForMigration(db); MarkCompleted(db, "plan-chart-backfill-v1"); db.Commit(); }
            catch { db.Rollback(); throw; }
        }
        if (Completed(db, "plan-chart-occupations-v1")) return;
        var plans = db.Query<ProcessScope>("SELECT DISTINCT InstanceID, StationSchemeID, OperationPlanID FROM movement WHERE CellOccupationsJson IS NULL") ?? [];
        foreach (var plan in plans)
        {
            if (string.IsNullOrWhiteSpace(plan.InstanceID) || string.IsNullOrWhiteSpace(plan.StationSchemeID) || string.IsNullOrWhiteSpace(plan.OperationPlanID))
                throw new InvalidDataException("Cannot migrate occupations of a movement without a plan identity.");
            var catalog = new MovementCellOccupationStore(db, plan.InstanceID, plan.StationSchemeID, plan.OperationPlanID);
            while (true)
            {
                db.BeginTransaction();
                try
                {
                    var lockSuffix = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName) ? " FOR UPDATE" : "";
                    var rows = db.Query<MovementRow>("SELECT * FROM movement WHERE InstanceID=@InstanceID AND StationSchemeID=@StationSchemeID AND OperationPlanID=@OperationPlanID AND CellOccupationsJson IS NULL LIMIT 200" + lockSuffix, plan) ?? [];
                    if (rows.Count == 0) { db.Commit(); break; }
                    var args = new Dictionary<string, object?> { ["InstanceID"] = plan.InstanceID, ["StationSchemeID"] = plan.StationSchemeID, ["OperationPlanID"] = plan.OperationPlanID };
                    var clauses = new List<string>();
                    for (var i = 0; i < rows.Count; i++)
                    {
                        catalog.Prepare(rows[i]);
                        args[$"train{i}"] = rows[i].TrainID; args[$"movement{i}"] = rows[i].MovementID; args[$"json{i}"] = rows[i].CellOccupationsJson;
                        clauses.Add($"WHEN TrainID=@train{i} AND MovementID=@movement{i} THEN @json{i}");
                    }
                    var changed = db.ExecuteNonQuery($"UPDATE movement SET CellOccupationsJson=CASE {string.Join(' ', clauses)} ELSE CellOccupationsJson END WHERE InstanceID=@InstanceID AND StationSchemeID=@StationSchemeID AND OperationPlanID=@OperationPlanID AND CellOccupationsJson IS NULL AND ({string.Join(" OR ", Enumerable.Range(0, rows.Count).Select(i => $"(TrainID=@train{i} AND MovementID=@movement{i})"))})", args);
                    if (changed != rows.Count) throw new InvalidDataException("Occupation migration encountered duplicate or missing movement identities.");
                    db.Commit();
                }
                catch { db.Rollback(); throw; }
            }
        }
        MarkCompleted(db, "plan-chart-occupations-v1");
    }

    private static bool Completed(DBConnector db, string version) => (db.Query<int>("SELECT COUNT(*) FROM capacityschemamigration WHERE Version=@version", new { version })?.Single() ?? 0) > 0;
    private static void MarkCompleted(DBConnector db, string version) => db.ExecuteNonQuery("INSERT INTO capacityschemamigration(Version,AppliedAtUtc) VALUES(@version,@now)", new { version, now = DateTime.UtcNow });
    private static void EnsureIndex(DBConnector db, bool mysql, string table, string[] columns)
    {
        var name = "ix_" + table + "_scope";
        if (mysql)
        {
            if ((db.Query<int>("SELECT COUNT(*) FROM information_schema.statistics WHERE table_schema=DATABASE() AND table_name=@table AND index_name=@name", new { table, name })?.Single() ?? 0) > 0) return;
            var lengths = db.Query<ColumnLength>("SELECT COLUMN_NAME AS Name, CHARACTER_MAXIMUM_LENGTH AS Length FROM information_schema.columns WHERE table_schema=DATABASE() AND table_name=@table", new { table })!.ToDictionary(column => column.Name, StringComparer.OrdinalIgnoreCase);
            // A non-unique prefix index narrows candidates; queries still compare the full IDs.
            // Five utf8mb4 VARCHAR(255) keys would exceed 3072 bytes. Never shorten stored IDs.
            var parts = columns.Select(column => $"`{column}`" + (lengths[column].Length > 50 ? "(50)" : ""));
            db.ExecuteNonQuery($"CREATE INDEX `{name}` ON `{table}` ({string.Join(',', parts)})");
        }
        else db.ExecuteNonQuery($"CREATE INDEX IF NOT EXISTS `{name}` ON `{table}` ({string.Join(',', columns.Select(column => $"`{column}`"))})");
    }
    private sealed class ColumnLength { public string Name { get; set; } = ""; public long? Length { get; set; } }
}
