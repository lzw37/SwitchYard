using SwitchYard.Service.Models;
namespace SwitchYard.Service.Services;

/// <summary>A calculation set contains independent route/continuation workspaces and snapshots.</summary>
public static class TractionRouteSchemaMigration
{
    public static readonly string[] DataTables = TractionSchemaMigration.DataTables.Select(Table).ToArray();
    public static readonly string[] Tables = [.. DataTables, "tractionroutecase"];
    internal static string Table(string table) => table.Replace("tractionset", "tractionroute");
    public static void Ensure(DBConnector db)
    {
        var mysql = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName);
        var number = mysql ? "DOUBLE" : "REAL";
        var integer = mysql ? "BIGINT" : "INTEGER";
        string Text(int size) => mysql ? $"VARCHAR({size}) COLLATE utf8mb4_bin" : "TEXT";
        var scope = $"InstanceID {Text(50)} NOT NULL, StationSchemeID {Text(50)} NOT NULL, CalculationSetID {Text(50)} NOT NULL, RouteCaseID {Text(50)} NOT NULL";
        var suffix = mysql ? " ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_bin" : "";
        string Train(string prefix) => string.Join(",", new[] { "LengthM", "MassT", "RotatingMassFactor", "MaxSpeedKmh", "MaxBrakeForceKn", "ResistanceA", "ResistanceB", "ResistanceC" }.Select(name => $"{prefix}{name} {number} NOT NULL"));
        string Signal(string prefix) => string.Join(",", new[] { "RouteSettingS", "ReactionS", "ClearanceS" }.Select(name => $"{prefix}{name} {number} NOT NULL"));
        string Options(string prefix) => $"{prefix}Mode {Text(20)} NOT NULL,{prefix}InitialSpeedKmh {number} NOT NULL,{prefix}FinalSpeedKmh {number} NOT NULL,{prefix}StepM {number} NOT NULL,{prefix}MaxPointCount INTEGER NOT NULL";
        void Create(string table, string columns, string key, string? parent = null, string parentKey = "InstanceID,StationSchemeID,CalculationSetID,RouteCaseID")
        {
            var foreignKey = parent is null ? "" : $", FOREIGN KEY ({parentKey}) REFERENCES {parent}({parentKey}) ON DELETE CASCADE";
            db.ExecuteNonQuery($"CREATE TABLE IF NOT EXISTS {table} ({scope},{columns},PRIMARY KEY ({key}){foreignKey}){suffix}");
        }
        const string scopeKey = "InstanceID,StationSchemeID,CalculationSetID,RouteCaseID";
        const string orderedKey = scopeKey + ",OrderIndex";

        db.ExecuteNonQuery($"CREATE TABLE IF NOT EXISTS tractionroutecase ({scope},RouteID {Text(200)} NOT NULL,ContinuationID {Text(200)} NOT NULL,Revision {integer} NOT NULL,IsSelected INTEGER NOT NULL,PRIMARY KEY ({scopeKey}),UNIQUE(InstanceID,StationSchemeID,CalculationSetID,RouteID,ContinuationID),FOREIGN KEY(InstanceID,StationSchemeID,CalculationSetID) REFERENCES tractioncalculationset(InstanceID,StationSchemeID,CalculationSetID) ON DELETE CASCADE){suffix}");
        Create("tractionrouteworkspace", $"Revision {integer} NOT NULL,CatalogVersion INTEGER NOT NULL,SelectedPresetID {Text(256)} NOT NULL,SelectedRouteID {Text(256)} NOT NULL,ContinuationID {Text(256)} NOT NULL,DistanceMode {Text(10)} NOT NULL,MetersPerUnit {number} NOT NULL,DefaultLimit {number} NOT NULL,{Signal("Signal")},{Options("Run")}", scopeKey, "tractionroutecase");
        Create("tractionroutetrainpreset", $"PresetID {Text(256)} NOT NULL,OrderIndex INTEGER NOT NULL,Name {Text(80)} NOT NULL,NameEn {Text(80)} NULL,{Train("")},UNIQUE(InstanceID,StationSchemeID,CalculationSetID,RouteCaseID,OrderIndex)", scopeKey + ",PresetID", "tractionrouteworkspace");
        Create("tractionroutepresetpoint", $"PresetID {Text(256)} NOT NULL,OrderIndex INTEGER NOT NULL,SpeedKmh {number} NOT NULL,ForceKn {number} NOT NULL", scopeKey + ",PresetID,OrderIndex", "tractionroutetrainpreset", scopeKey + ",PresetID");
        Create("tractionroutepresetbrakingpoint", $"PresetID {Text(256)} NOT NULL,OrderIndex INTEGER NOT NULL,SpeedKmh {number} NOT NULL,ForceKn {number} NOT NULL", scopeKey + ",PresetID,OrderIndex", "tractionroutetrainpreset", scopeKey + ",PresetID");
        Create("tractionroutepresetresistancepoint", $"PresetID {Text(256)} NOT NULL,OrderIndex INTEGER NOT NULL,SpeedKmh {number} NOT NULL,ForceKn {number} NOT NULL", scopeKey + ",PresetID,OrderIndex", "tractionroutetrainpreset", scopeKey + ",PresetID");
        Create("tractionrouteworkspacelimitpoint", $"OrderIndex INTEGER NOT NULL,PositionM {number} NOT NULL,SpeedLimitKmh {number} NOT NULL", orderedKey, "tractionrouteworkspace");
        Create("tractionrouteworkspacestop", $"OrderIndex INTEGER NOT NULL,PositionM {number} NOT NULL,DwellS {number} NOT NULL", orderedKey, "tractionrouteworkspace");
        Create("tractionrouteworkspaceoverride", $"OrderIndex INTEGER NOT NULL,RouteID {Text(256)} NOT NULL,RouteStartNodeID {Text(256)} NOT NULL,RouteEndNodeID {Text(256)} NOT NULL,ContinuationID {Text(256)} NOT NULL,SegmentIndex INTEGER NOT NULL,SegmentID {Text(256)} NOT NULL,SpeedLimitKmh {number} NULL,GradientPermille {number} NULL,AdditionalResistanceKn {number} NULL", orderedKey, "tractionrouteworkspace");
        Create("tractionroutecalculation", $"WorkspaceRevision {integer} NOT NULL,SavedAtUtc {Text(40)} NOT NULL,SelectedRouteID {Text(256)} NOT NULL,ContinuationID {Text(256)} NOT NULL,ProfileLengthM {number} NOT NULL,{Train("Train")},{Signal("Signal")},{Options("Input")},ResultLengthM {number} NOT NULL,ResultTrainLengthM {number} NOT NULL,ResultMode {Text(20)} NOT NULL,DurationS {number} NOT NULL,MotionStartS {number} NOT NULL,TotalTimeS {number} NOT NULL,RunningTimeS {number} NOT NULL,DwellTimeS {number} NOT NULL,ResultMaxSpeedKmh {number} NOT NULL,Engine {Text(80)} NOT NULL", scopeKey, "tractionrouteworkspace");
        Create("tractionroutecalculationpoint", $"OrderIndex INTEGER NOT NULL,SpeedKmh {number} NOT NULL,ForceKn {number} NOT NULL", orderedKey, "tractionroutecalculation");
        Create("tractionroutecalculationbrakingpoint", $"OrderIndex INTEGER NOT NULL,SpeedKmh {number} NOT NULL,ForceKn {number} NOT NULL", orderedKey, "tractionroutecalculation");
        Create("tractionroutecalculationresistancepoint", $"OrderIndex INTEGER NOT NULL,SpeedKmh {number} NOT NULL,ForceKn {number} NOT NULL", orderedKey, "tractionroutecalculation");
        Create("tractionroutecalculationsegment", $"OrderIndex INTEGER NOT NULL,StartM {number} NOT NULL,EndM {number} NOT NULL,SpeedLimitKmh {number} NOT NULL,GradientPermille {number} NOT NULL,AdditionalResistanceKn {number} NOT NULL", orderedKey, "tractionroutecalculation");
        Create("tractionroutecalculationdevice", $"OrderIndex INTEGER NOT NULL,ID {Text(256)} NOT NULL,Label {Text(1000)} NOT NULL,Type {Text(256)} NOT NULL,PositionM {number} NOT NULL", orderedKey, "tractionroutecalculation");
        Create("tractionroutecalculationjoint", $"OrderIndex INTEGER NOT NULL,ID {Text(256)} NOT NULL,Label {Text(1000)} NOT NULL,PositionM {number} NOT NULL", orderedKey, "tractionroutecalculation");
        var cell = $"ID {Text(256)} NOT NULL,Label {Text(1000)} NOT NULL,StartM {number} NOT NULL,EndM {number} NOT NULL,EntryBoundaryKnown INTEGER NOT NULL,ExitBoundaryKnown INTEGER NOT NULL";
        Create("tractionroutecalculationcell", $"OrderIndex INTEGER NOT NULL,{cell}", orderedKey, "tractionroutecalculation");
        Create("tractionroutecalculationstop", $"OrderIndex INTEGER NOT NULL,PositionM {number} NOT NULL,DwellS {number} NOT NULL", orderedKey, "tractionroutecalculation");
        Create("tractionroutecalculationsample", $"OrderIndex INTEGER NOT NULL,PositionM {number} NOT NULL,SpeedKmh {number} NOT NULL,LimitKmh {number} NOT NULL,TimeS {number} NOT NULL", orderedKey, "tractionroutecalculation");
        Create("tractionroutecalculationjointtime", $"OrderIndex INTEGER NOT NULL,ID {Text(256)} NOT NULL,Label {Text(1000)} NOT NULL,PositionM {number} NOT NULL,HeadTimeS {number} NOT NULL,TailTimeS {number} NULL,ReleaseTimeS {number} NULL", orderedKey, "tractionroutecalculation");
        Create("tractionroutecalculationcelltime", $"OrderIndex INTEGER NOT NULL,{cell},EntryTimeS {number} NULL,ExitTimeS {number} NULL,ReleaseTimeS {number} NULL", orderedKey, "tractionroutecalculation");
        Create("tractionroutecalculationwarning", $"OrderIndex INTEGER NOT NULL,Warning {Text(1000)} NOT NULL", orderedKey, "tractionroutecalculation");
        Index(db, mysql, "ix_tractionrouteworkspaceoverride_route", "tractionrouteworkspaceoverride", "InstanceID,StationSchemeID,CalculationSetID,RouteID");
        Index(db, mysql, "ix_tractionrouteworkspaceoverride_continuation", "tractionrouteworkspaceoverride", "InstanceID,StationSchemeID,CalculationSetID,ContinuationID");
        Index(db, mysql, "ix_tractionroutecalculation_route", "tractionroutecalculation", "InstanceID,StationSchemeID,CalculationSetID,SelectedRouteID");

        Migrate(db, mysql);
        TractionCharacteristicMigration.Ensure(db, "tractionroute");
    }
    private static void Migrate(DBConnector db, bool mysql)
    {
        db.BeginTransaction();
        try {
            var store = new TractionWorkspaceStore(db);
            var scopes = db.Query<SetScope>("SELECT InstanceID,StationSchemeID,CalculationSetID FROM tractionsetworkspace") ?? [];
            foreach (var oldScope in scopes) {
                var scope = new TractionScope(oldScope.InstanceID, oldScope.StationSchemeID, oldScope.CalculationSetID);
                var workspace = store.ReadWorkspace(scope)!;
                var calculation = store.ReadCalculation(scope);
                var current = scope with { RouteCaseID = Guid.NewGuid().ToString("N") };
                // Even a draft without a selected route remains recoverable.
                InsertCase(db, current, workspace.Settings.SelectedRouteId, workspace.Settings.ContinuationId, true);
                store.WriteWorkspace(current, workspace);
                if (calculation is not null) {
                    var target = current;
                    if (calculation.SelectedRouteId != workspace.Settings.SelectedRouteId || calculation.ContinuationId != workspace.Settings.ContinuationId) {
                        target = scope with { RouteCaseID = Guid.NewGuid().ToString("N") };
                        InsertCase(db, target, calculation.SelectedRouteId, calculation.ContinuationId, false);
                        var input = calculation.Request;
                        var snapshotWorkspace = workspace with {
                            Revision = calculation.Revision,
                            Catalog = workspace.Catalog with { Presets = workspace.Catalog.Presets.Select(preset => preset.Id == workspace.Catalog.SelectedId ? preset with { Parameters = input.Train } : preset).ToArray() },
                            Settings = workspace.Settings with { SelectedRouteId = calculation.SelectedRouteId, ContinuationId = calculation.ContinuationId, Signal = input.Signal, Run = input.Options },
                        };
                        store.WriteWorkspace(target, snapshotWorkspace);
                    }
                    foreach (var table in TractionSchemaMigration.CalculationTables.Reverse()) {
                        var fields = string.Join(",", TractionSchemaMigration.Columns(db, mysql, table).Select(column => ((char)96) + column + ((char)96)));
                        db.ExecuteNonQuery($"INSERT INTO {Table(table)}({fields},RouteCaseID) SELECT {fields},@RouteCaseID FROM {table} WHERE InstanceID=@InstanceID AND StationSchemeID=@StationSchemeID AND CalculationSetID=@CalculationSetID", target);
                    }
                }
                foreach (var table in TractionSchemaMigration.DataTables)
                    db.ExecuteNonQuery($"DELETE FROM {table} WHERE InstanceID=@InstanceID AND StationSchemeID=@StationSchemeID AND CalculationSetID=@CalculationSetID", scope);
            }
            db.Commit();
        } catch { db.Rollback(); throw; }
    }
    private sealed class SetScope { public string InstanceID { get; set; } = ""; public string StationSchemeID { get; set; } = ""; public string CalculationSetID { get; set; } = ""; }
    internal static void InsertCase(DBConnector db, TractionScope scope, string routeId, string continuationId, bool selected)
    {
        db.ExecuteNonQuery("INSERT INTO tractionroutecase(InstanceID,StationSchemeID,CalculationSetID,RouteCaseID,RouteID,ContinuationID,Revision,IsSelected) VALUES(@InstanceID,@StationSchemeID,@CalculationSetID,@RouteCaseID,@routeId,@continuationId,1,@selected)",
            new { scope.InstanceID, scope.StationSchemeID, scope.CalculationSetID, scope.RouteCaseID, routeId, continuationId, selected = selected ? 1 : 0 });
    }
    private static void Index(DBConnector db, bool mysql, string name, string table, string columns)
    {
        if (!mysql) db.ExecuteNonQuery($"CREATE INDEX IF NOT EXISTS {name} ON {table}({columns})");
        else if ((db.Query<int>("SELECT COUNT(*) FROM information_schema.statistics WHERE table_schema=DATABASE() AND table_name=@table AND index_name=@name", new { table, name })?.Single() ?? 0) == 0)
            db.ExecuteNonQuery($"CREATE INDEX {name} ON {table}({columns})");
    }
}
