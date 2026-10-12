namespace SwitchYard.Service.Services;

public static class CalculationParameterSetMigration
{
    public static void Apply(DBConnector db)
    {
        const string version = "calculation-parameter-sets-v1";
        var mysql = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName);
        db.ExecuteNonQuery(mysql
            ? "CREATE TABLE IF NOT EXISTS calculationparameterset (InstanceID VARCHAR(50) COLLATE utf8mb4_bin NOT NULL, StationSchemeID VARCHAR(50) COLLATE utf8mb4_bin NOT NULL, ID VARCHAR(50) COLLATE utf8mb4_bin NOT NULL, Name VARCHAR(200) NOT NULL, IsDefault INTEGER NOT NULL DEFAULT 0, PRIMARY KEY(InstanceID,StationSchemeID,ID)) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci"
            : "CREATE TABLE IF NOT EXISTS calculationparameterset (InstanceID TEXT NOT NULL, StationSchemeID TEXT NOT NULL, ID TEXT NOT NULL, Name TEXT NOT NULL, IsDefault INTEGER NOT NULL DEFAULT 0, PRIMARY KEY(InstanceID,StationSchemeID,ID))");
        if ((db.Query<int>("SELECT COUNT(*) FROM capacityschemamigration WHERE Version=@version", new { version })?.Single() ?? 0) > 0) return;
        foreach (var table in new[] { "stationroutetime", "train" })
        {
            var columns = db.Query<Column>(mysql
                ? "SELECT COLUMN_NAME AS Name FROM information_schema.columns WHERE table_schema=DATABASE() AND table_name=@table"
                : $"PRAGMA table_info({table})", new { table }) ?? [];
            if (!columns.Any(column => column.Name.Equals("CalculationParameterSetID", StringComparison.OrdinalIgnoreCase)))
                db.ExecuteNonQuery($"ALTER TABLE {table} ADD COLUMN CalculationParameterSetID " +
                    (mysql ? "VARCHAR(50) COLLATE utf8mb4_bin NULL" : "TEXT NULL"));
        }
        const string index = "ix_stationroutetime_parameter_set";
        if (mysql)
        {
            if ((db.Query<int>("SELECT COUNT(*) FROM information_schema.statistics WHERE table_schema=DATABASE() AND table_name='stationroutetime' AND index_name=@index", new { index })?.Single() ?? 0) == 0)
                db.ExecuteNonQuery($"CREATE INDEX {index} ON stationroutetime(InstanceID(50),StationSchemeID(50),CalculationParameterSetID(50),RouteID(50),TrainTypeID(50))");
        }
        else db.ExecuteNonQuery($"CREATE INDEX IF NOT EXISTS {index} ON stationroutetime(InstanceID,StationSchemeID,CalculationParameterSetID,RouteID,TrainTypeID)");
        db.BeginTransaction();
        try
        {
            var scopes = db.Query<Scope>("SELECT InstanceID, ID AS StationSchemeID FROM stationscheme UNION SELECT InstanceID,StationSchemeID FROM stationroutetime UNION SELECT InstanceID,StationSchemeID FROM train") ?? [];
            foreach (var scope in scopes.Where(scope => !string.IsNullOrWhiteSpace(scope.InstanceID) && !string.IsNullOrWhiteSpace(scope.StationSchemeID)))
                CalculationParameterSets.EnsureDefault(db, scope.InstanceID, scope.StationSchemeID);
            db.ExecuteNonQuery("UPDATE stationroutetime SET CalculationParameterSetID='default' WHERE CalculationParameterSetID IS NULL OR TRIM(CalculationParameterSetID)=''");
            db.ExecuteNonQuery("INSERT INTO capacityschemamigration(Version,AppliedAtUtc) VALUES(@version,@now)", new { version, now = DateTime.UtcNow });
            db.Commit();
        }
        catch { db.Rollback(); throw; }
    }

    private sealed class Column { public string Name { get; set; } = ""; }
    private sealed class Scope { public string InstanceID { get; set; } = ""; public string StationSchemeID { get; set; } = ""; }
}
