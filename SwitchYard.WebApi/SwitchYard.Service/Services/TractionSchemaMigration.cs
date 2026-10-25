namespace SwitchYard.Service.Services;

/// <summary>Normalized traction storage; each collection has its own ordered relation.</summary>
public static class TractionSchemaMigration
{
    public static readonly string[] CalculationTables = [
        "tractionsetcalculationwarning", "tractionsetcalculationcelltime", "tractionsetcalculationjointtime", "tractionsetcalculationsample",
        "tractionsetcalculationstop", "tractionsetcalculationcell", "tractionsetcalculationjoint", "tractionsetcalculationdevice",
        "tractionsetcalculationbrakingpoint", "tractionsetcalculationresistancepoint", "tractionsetcalculationsegment", "tractionsetcalculationpoint", "tractionsetcalculation"
    ];
    public static readonly string[] WorkspaceChildTables = [
        "tractionsetworkspacelimitpoint", "tractionsetworkspaceoverride", "tractionsetworkspacestop", "tractionsetpresetpoint", "tractionsetpresetbrakingpoint", "tractionsetpresetresistancepoint", "tractionsettrainpreset"
    ];
    /// <summary>Safe explicit deletion order, including connectors with disabled FK cascades.</summary>
    public static readonly string[] Tables = [.. CalculationTables, .. WorkspaceChildTables, "tractionsetworkspace", "tractionsetselection", "tractioncalculationset"];
    public static readonly string[] DataTables = Tables[..^2];
    public static readonly string[] LegacyTables = DataTables.Where(name => name != "tractionsetworkspacelimitpoint" && !name.Contains("brakingpoint") && !name.Contains("resistancepoint")).Select(name => name.Replace("tractionset", "traction")).ToArray();

    public static void Ensure(DBConnector db)
    {
        TractionProjectTrainSchema.Ensure(db);
        var mysql = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName);
        var number = mysql ? "DOUBLE" : "REAL";
        var integer = mysql ? "BIGINT" : "INTEGER";
        string Text(int size) => mysql ? $"VARCHAR({size}) COLLATE utf8mb4_bin" : "TEXT";
        var scope = $"InstanceID {Text(50)} NOT NULL, StationSchemeID {Text(50)} NOT NULL, CalculationSetID {Text(50)} NOT NULL";
        var suffix = mysql ? " ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_bin" : "";
        string Train(string prefix) => string.Join(",", new[] { "LengthM", "MassT", "RotatingMassFactor", "MaxSpeedKmh", "MaxBrakeForceKn", "ResistanceA", "ResistanceB", "ResistanceC" }.Select(name => $"{prefix}{name} {number} NOT NULL"));
        string Signal(string prefix) => string.Join(",", new[] { "RouteSettingS", "ReactionS", "ClearanceS" }.Select(name => $"{prefix}{name} {number} NOT NULL"));
        string Options(string prefix) => $"{prefix}Mode {Text(20)} NOT NULL,{prefix}InitialSpeedKmh {number} NOT NULL,{prefix}FinalSpeedKmh {number} NOT NULL,{prefix}StepM {number} NOT NULL,{prefix}MaxPointCount INTEGER NOT NULL,{prefix}DepartureSectionLengthM {number} NOT NULL DEFAULT 0,{prefix}BrakingDistanceM {number} NULL,{prefix}BrakingMaxSpeedKmh {number} NULL,{prefix}DepartureSectionMaxSpeedKmh {number} NULL";
        void Create(string table, string columns, string key, string? parent = null, string parentKey = "InstanceID,StationSchemeID,CalculationSetID")
        {
            var foreignKey = parent is null ? "" : $", FOREIGN KEY ({parentKey}) REFERENCES {parent}({parentKey}) ON DELETE CASCADE";
            db.ExecuteNonQuery($"CREATE TABLE IF NOT EXISTS {table} ({scope},{columns},PRIMARY KEY ({key}){foreignKey}){suffix}");
        }
        const string scopeKey = "InstanceID,StationSchemeID,CalculationSetID";
        const string orderedKey = scopeKey + ",OrderIndex";
        // Legacy stationscheme has no unique composite key on both database providers.
        // The store locks/verifies that parent scope; all traction-owned children use real foreign keys.
        Create("tractioncalculationset", $"Name {Text(80)} NOT NULL,Revision {integer} NOT NULL,CreatedAtUtc {Text(40)} NOT NULL,UpdatedAtUtc {Text(40)} NOT NULL", scopeKey);
        db.ExecuteNonQuery($"CREATE TABLE IF NOT EXISTS tractionsetselection (InstanceID {Text(50)} NOT NULL,StationSchemeID {Text(50)} NOT NULL,SelectedCalculationSetID {Text(50)} NOT NULL,PRIMARY KEY(InstanceID,StationSchemeID),FOREIGN KEY(InstanceID,StationSchemeID,SelectedCalculationSetID) REFERENCES tractioncalculationset(InstanceID,StationSchemeID,CalculationSetID) ON DELETE CASCADE){suffix}");
        Create("tractionsetworkspace", $"Revision {integer} NOT NULL,CatalogVersion INTEGER NOT NULL,SelectedPresetID {Text(256)} NOT NULL,SelectedRouteID {Text(256)} NOT NULL,ContinuationID {Text(256)} NOT NULL,DistanceMode {Text(10)} NOT NULL,MetersPerUnit {number} NOT NULL,DefaultLimit {number} NOT NULL,{Signal("Signal")},{Options("Run")}", scopeKey, "tractioncalculationset");
        Create("tractionsettrainpreset", $"PresetID {Text(256)} NOT NULL,OrderIndex INTEGER NOT NULL,Name {Text(80)} NOT NULL,NameEn {Text(80)} NULL,{Train("")},UNIQUE(InstanceID,StationSchemeID,CalculationSetID,OrderIndex)", scopeKey + ",PresetID", "tractionsetworkspace");
        Create("tractionsetpresetpoint", $"PresetID {Text(256)} NOT NULL,OrderIndex INTEGER NOT NULL,SpeedKmh {number} NOT NULL,ForceKn {number} NOT NULL", scopeKey + ",PresetID,OrderIndex", "tractionsettrainpreset", scopeKey + ",PresetID");
        Create("tractionsetpresetbrakingpoint", $"PresetID {Text(256)} NOT NULL,OrderIndex INTEGER NOT NULL,SpeedKmh {number} NOT NULL,ForceKn {number} NOT NULL", scopeKey + ",PresetID,OrderIndex", "tractionsettrainpreset", scopeKey + ",PresetID");
        Create("tractionsetpresetresistancepoint", $"PresetID {Text(256)} NOT NULL,OrderIndex INTEGER NOT NULL,SpeedKmh {number} NOT NULL,ForceKn {number} NOT NULL", scopeKey + ",PresetID,OrderIndex", "tractionsettrainpreset", scopeKey + ",PresetID");
        Create("tractionsetworkspacelimitpoint", $"OrderIndex INTEGER NOT NULL,PositionM {number} NOT NULL,SpeedLimitKmh {number} NOT NULL", orderedKey, "tractionsetworkspace");
        Create("tractionsetworkspacestop", $"OrderIndex INTEGER NOT NULL,PositionM {number} NOT NULL,DwellS {number} NOT NULL", orderedKey, "tractionsetworkspace");
        Create("tractionsetworkspaceoverride", $"OrderIndex INTEGER NOT NULL,RouteID {Text(256)} NOT NULL,RouteStartNodeID {Text(256)} NOT NULL,RouteEndNodeID {Text(256)} NOT NULL,ContinuationID {Text(256)} NOT NULL,SegmentIndex INTEGER NOT NULL,SegmentID {Text(256)} NOT NULL,SpeedLimitKmh {number} NULL,GradientPermille {number} NULL,AdditionalResistanceKn {number} NULL", orderedKey, "tractionsetworkspace");
        Create("tractionsetcalculation", $"WorkspaceRevision {integer} NOT NULL,SavedAtUtc {Text(40)} NOT NULL,SelectedRouteID {Text(256)} NOT NULL,ContinuationID {Text(256)} NOT NULL,ProfileLengthM {number} NOT NULL,{Train("Train")},{Signal("Signal")},{Options("Input")},ResultLengthM {number} NOT NULL,ResultTrainLengthM {number} NOT NULL,ResultMode {Text(20)} NOT NULL,DurationS {number} NOT NULL,MotionStartS {number} NOT NULL,TotalTimeS {number} NOT NULL,RunningTimeS {number} NOT NULL,DwellTimeS {number} NOT NULL,ResultMaxSpeedKmh {number} NOT NULL,Engine {Text(80)} NOT NULL", scopeKey, "tractionsetworkspace");
        Create("tractionsetcalculationpoint", $"OrderIndex INTEGER NOT NULL,SpeedKmh {number} NOT NULL,ForceKn {number} NOT NULL", orderedKey, "tractionsetcalculation");
        Create("tractionsetcalculationbrakingpoint", $"OrderIndex INTEGER NOT NULL,SpeedKmh {number} NOT NULL,ForceKn {number} NOT NULL", orderedKey, "tractionsetcalculation");
        Create("tractionsetcalculationresistancepoint", $"OrderIndex INTEGER NOT NULL,SpeedKmh {number} NOT NULL,ForceKn {number} NOT NULL", orderedKey, "tractionsetcalculation");
        Create("tractionsetcalculationsegment", $"OrderIndex INTEGER NOT NULL,StartM {number} NOT NULL,EndM {number} NOT NULL,SpeedLimitKmh {number} NOT NULL,GradientPermille {number} NOT NULL,AdditionalResistanceKn {number} NOT NULL", orderedKey, "tractionsetcalculation");
        Create("tractionsetcalculationdevice", $"OrderIndex INTEGER NOT NULL,ID {Text(256)} NOT NULL,Label {Text(1000)} NOT NULL,Type {Text(256)} NOT NULL,PositionM {number} NOT NULL", orderedKey, "tractionsetcalculation");
        Create("tractionsetcalculationjoint", $"OrderIndex INTEGER NOT NULL,ID {Text(256)} NOT NULL,Label {Text(1000)} NOT NULL,PositionM {number} NOT NULL", orderedKey, "tractionsetcalculation");
        var cell = $"ID {Text(256)} NOT NULL,Label {Text(1000)} NOT NULL,StartM {number} NOT NULL,EndM {number} NOT NULL,EntryBoundaryKnown INTEGER NOT NULL,ExitBoundaryKnown INTEGER NOT NULL";
        Create("tractionsetcalculationcell", $"OrderIndex INTEGER NOT NULL,{cell}", orderedKey, "tractionsetcalculation");
        Create("tractionsetcalculationstop", $"OrderIndex INTEGER NOT NULL,PositionM {number} NOT NULL,DwellS {number} NOT NULL", orderedKey, "tractionsetcalculation");
        Create("tractionsetcalculationsample", $"OrderIndex INTEGER NOT NULL,PositionM {number} NOT NULL,SpeedKmh {number} NOT NULL,LimitKmh {number} NOT NULL,TimeS {number} NOT NULL", orderedKey, "tractionsetcalculation");
        Create("tractionsetcalculationjointtime", $"OrderIndex INTEGER NOT NULL,ID {Text(256)} NOT NULL,Label {Text(1000)} NOT NULL,PositionM {number} NOT NULL,HeadTimeS {number} NOT NULL,TailTimeS {number} NULL,ReleaseTimeS {number} NULL", orderedKey, "tractionsetcalculation");
        Create("tractionsetcalculationcelltime", $"OrderIndex INTEGER NOT NULL,{cell},EntryTimeS {number} NULL,ExitTimeS {number} NULL,ReleaseTimeS {number} NULL", orderedKey, "tractionsetcalculation");
        Create("tractionsetcalculationwarning", $"OrderIndex INTEGER NOT NULL,Warning {Text(1000)} NOT NULL", orderedKey, "tractionsetcalculation");
        Index(db, mysql, "ix_tractionsetworkspaceoverride_route", "tractionsetworkspaceoverride", "InstanceID,StationSchemeID,CalculationSetID,RouteID");
        Index(db, mysql, "ix_tractionsetworkspaceoverride_continuation", "tractionsetworkspaceoverride", "InstanceID,StationSchemeID,CalculationSetID,ContinuationID");
        Index(db, mysql, "ix_tractionsetcalculation_route", "tractionsetcalculation", "InstanceID,StationSchemeID,CalculationSetID,SelectedRouteID");
        EnsureRunOptionColumns(db, "tractionset");
        MigrateLegacy(db, mysql);
        TractionCharacteristicMigration.Ensure(db, "tractionset");
        TractionRouteSchemaMigration.Ensure(db);
    }
    internal static void EnsureRunOptionColumns(DBConnector db, string prefix)
    {
        if (prefix is not ("tractionset" or "tractionroute")) throw new ArgumentException("Invalid table scope.");
        var mysql = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName);
        foreach (var (suffix, optionPrefix) in new[] { ("workspace", "Run"), ("calculation", "Input") }) {
            var table = prefix + suffix;
            var columns = Columns(db, mysql, table);
            foreach (var (option, definition) in new[] { ("DepartureSectionLengthM", "NOT NULL DEFAULT 0"), ("BrakingDistanceM", "NULL"), ("BrakingMaxSpeedKmh", "NULL"), ("DepartureSectionMaxSpeedKmh", "NULL") }) {
                var column = optionPrefix + option;
                if (!columns.Contains(column, StringComparer.OrdinalIgnoreCase))
                    db.ExecuteNonQuery($"ALTER TABLE {table} ADD COLUMN {column} {(mysql ? "DOUBLE" : "REAL")} {definition}");
            }
        }
    }

    private static void MigrateLegacy(DBConnector db, bool mysql)
    {
        if (!Exists(db, mysql, "tractionworkspace")) return;
        db.BeginTransaction();
        try {
            var scopes = db.Query<LegacyScope>("SELECT InstanceID,StationSchemeID FROM tractionworkspace") ?? [];
            foreach (var scope in scopes) {
                var now = DateTime.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
                db.ExecuteNonQuery("INSERT INTO tractioncalculationset(InstanceID,StationSchemeID,CalculationSetID,Name,Revision,CreatedAtUtc,UpdatedAtUtc) VALUES(@InstanceID,@StationSchemeID,'default','默认计算方案',1,@now,@now)", new { scope.InstanceID, scope.StationSchemeID, now });
                foreach (var target in DataTables.Reverse()) {
                    var source = target.Replace("tractionset", "traction");
                    if (!Exists(db, mysql, source)) continue;
                    var columns = Columns(db, mysql, source);
                    var fields = string.Join(",", columns.Select(name => ((char)96) + name + ((char)96)));
                    db.ExecuteNonQuery($"INSERT INTO {target}({fields},CalculationSetID) SELECT {fields},'default' FROM {source} WHERE InstanceID=@InstanceID AND StationSchemeID=@StationSchemeID", scope);
                }
                db.ExecuteNonQuery("INSERT INTO tractionsetselection(InstanceID,StationSchemeID,SelectedCalculationSetID) VALUES(@InstanceID,@StationSchemeID,'default')", scope);
                // Move every normalized row atomically; an interrupted migration retains all legacy data.
                foreach (var table in LegacyTables) if (Exists(db, mysql, table))
                    db.ExecuteNonQuery($"DELETE FROM {table} WHERE InstanceID=@InstanceID AND StationSchemeID=@StationSchemeID", scope);
            }
            db.Commit();
        } catch { db.Rollback(); throw; }
    }
    internal static bool Exists(DBConnector db, bool mysql, string table) => (db.Query<int>(mysql
        ? "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema=DATABASE() AND table_name=@table"
        : "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@table", new { table })?.Single() ?? 0) > 0;
    internal static string[] Columns(DBConnector db, bool mysql, string table) => mysql
        ? (db.Query<string>("SELECT COLUMN_NAME FROM information_schema.columns WHERE table_schema=DATABASE() AND table_name=@table ORDER BY ORDINAL_POSITION", new { table }) ?? []).ToArray()
        : (db.Query<Column>($"PRAGMA table_info({table})") ?? []).Select(column => column.Name).ToArray();
    private sealed class Column { public string Name { get; set; } = ""; }
    private sealed class LegacyScope { public string InstanceID { get; set; } = ""; public string StationSchemeID { get; set; } = ""; }
    private static void Index(DBConnector db, bool mysql, string name, string table, string columns)
    {
        if (!mysql) db.ExecuteNonQuery($"CREATE INDEX IF NOT EXISTS {name} ON {table}({columns})");
        else if ((db.Query<int>("SELECT COUNT(*) FROM information_schema.statistics WHERE table_schema=DATABASE() AND table_name=@table AND index_name=@name", new { table, name })?.Single() ?? 0) == 0)
            db.ExecuteNonQuery($"CREATE INDEX {name} ON {table}({columns})");
    }
}



