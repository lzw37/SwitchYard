using SwitchYard.Capacity;

namespace SwitchYard.Service.Services;

/// <summary>Startup upgrade for existing SQLite and MySQL capacity databases.</summary>
public static class StationRouteTrainTypesMigration
{
    public static void Apply(DBConnector db)
    {
        const string version = "station-route-train-types-v1";
        var mysql = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName);
        db.ExecuteNonQuery(mysql
            ? "CREATE TABLE IF NOT EXISTS capacityschemamigration (Version VARCHAR(100) CHARACTER SET ascii COLLATE ascii_bin PRIMARY KEY, AppliedAtUtc DATETIME NOT NULL)"
            : "CREATE TABLE IF NOT EXISTS capacityschemamigration (Version TEXT PRIMARY KEY, AppliedAtUtc TEXT NOT NULL)");
        if ((db.Query<int>("SELECT COUNT(*) FROM capacityschemamigration WHERE Version=@version", new { version })?.Single() ?? 0) > 0) return;
        var columns = db.Query<Column>(mysql
            ? "SELECT COLUMN_NAME AS Name FROM information_schema.columns WHERE table_schema=DATABASE() AND table_name='stationroute'"
            : "PRAGMA table_info(stationroute)") ?? [];
        if (!columns.Any(column => string.Equals(column.Name, "TrainTypes", StringComparison.OrdinalIgnoreCase)))
            db.ExecuteNonQuery("ALTER TABLE stationroute ADD COLUMN TrainTypes TEXT NULL");

        db.BeginTransaction();
        try
        {
            var types = db.Query<RouteType>("SELECT DISTINCT Type FROM stationroute WHERE TrainTypes IS NULL OR TRIM(TrainTypes)=''") ?? [];
            foreach (var type in types)
                db.ExecuteNonQuery("UPDATE stationroute SET TrainTypes=@trainTypes WHERE (Type=@Type OR (Type IS NULL AND @Type IS NULL)) AND (TrainTypes IS NULL OR TRIM(TrainTypes)='')",
                    new { type.Type, trainTypes = StationRouteTrainTypes.DefaultJson(type.Type) });
            db.ExecuteNonQuery("INSERT INTO capacityschemamigration(Version,AppliedAtUtc) VALUES(@version,@now)", new { version, now = DateTime.UtcNow });
            db.Commit();
        }
        catch { db.Rollback(); throw; }
    }

    private sealed class Column { public string Name { get; set; } = ""; }
    private sealed class RouteType { public string? Type { get; set; } }
}
