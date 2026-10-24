using System.Globalization;
using Dapper;
using SwitchYard.Service.Models;

namespace SwitchYard.Service.Services;

/// <summary>Transactional, revisioned relational persistence. Never opens an application database by itself.</summary>
public sealed class TractionWorkspaceStore
{
    private readonly DBConnector db;
    private const string StationFilter = "InstanceID=@InstanceID AND StationSchemeID=@StationSchemeID";
    private const string Filter = StationFilter + " AND CalculationSetID=@CalculationSetID";
    private const string CaseFilter = Filter + " AND RouteCaseID=@RouteCaseID";
    private static bool IsRouteTable(string table, TractionScope scope) => scope.RouteCaseID.Length > 0 && TractionSchemaMigration.DataTables.Contains(table);
    private static string ScopedTable(string table, TractionScope scope) => IsRouteTable(table, scope) ? TractionRouteSchemaMigration.Table(table) : table;
    private static string ScopedFilter(string table, TractionScope scope) => IsRouteTable(table, scope) ? CaseFilter : Filter;
    public TractionWorkspaceStore(DBConnector db) => this.db = db;

    public TractionWorkspace? GetWorkspace(TractionScope scope) => Transaction(scope, () => ReadWorkspace(scope));
    public StoredTractionCalculation? GetCalculation(TractionScope scope) => Transaction(scope, () => ReadCalculation(scope));

    public TractionWorkspace SaveWorkspace(TractionScope scope, TractionWorkspace workspace)
    {
        TractionWorkspaceValidation.Workspace(workspace);
        workspace = workspace with { Catalog = workspace.Catalog with { Presets = workspace.Catalog.Presets.Select(p => p with { Parameters = TrainCharacteristicTables.Normalize(p.Parameters) }).ToArray() } };
        TractionWorkspaceValidation.Workspace(workspace);
        return Transaction(scope, () => {
            var current = ReadWorkspace(scope);
            var revision = current?.Revision ?? 0;
            if (workspace.Revision != revision) throw new TractionRevisionConflictException(revision);
            if (revision >= TractionWorkspaceValidation.MaximumRevision) throw new ArgumentException("Workspace revision is exhausted.");
            ValidateRouteReferences(scope, workspace);
            ValidateCaseIdentity(scope, workspace);
            var next = workspace with { Revision = revision + 1 };
            WriteWorkspace(scope, next);
            TouchSet(scope);
            return ReadWorkspace(scope)!;
        });
    }

    internal void WriteWorkspace(TractionScope scope, TractionWorkspace workspace)
    {
        workspace = workspace with { Catalog = workspace.Catalog with { Presets = workspace.Catalog.Presets.Select(p => p with { Parameters = TrainCharacteristicTables.Normalize(p.Parameters) }).ToArray() } };
        var root = WorkspaceData(workspace);
        if (ReadWorkspace(scope) is null) Insert("tractionsetworkspace", scope, root);
        else Update("tractionsetworkspace", scope, root);
        foreach (var table in TractionSchemaMigration.WorkspaceChildTables) db.ExecuteNonQuery($"DELETE FROM {ScopedTable(table, scope)} WHERE {ScopedFilter(table, scope)}", scope);
        InsertMany("tractionsettrainpreset", scope, workspace.Catalog.Presets.Select((preset, index) => {
            var row = Data(("PresetID", preset.Id), ("OrderIndex", index), ("Name", preset.Name), ("NameEn", preset.NameEn));
            AddTrain(row, preset.Parameters); return row;
        }));
        InsertMany("tractionsetpresetpoint", scope, workspace.Catalog.Presets.SelectMany(preset => preset.Parameters.TractionCurve.Select((point, index) =>
            Data(("PresetID", preset.Id), ("OrderIndex", index), ("SpeedKmh", point.SpeedKmh), ("ForceKn", point.ForceKn)))));
        foreach (var (table, braking) in new[] { ("tractionsetpresetbrakingpoint", true), ("tractionsetpresetresistancepoint", false) })
            InsertMany(table, scope, workspace.Catalog.Presets.SelectMany(preset => (braking ? preset.Parameters.BrakingCurve! : preset.Parameters.ResistanceCurve!).Select((point, index) =>
                Data(("PresetID", preset.Id), ("OrderIndex", index), ("SpeedKmh", point.SpeedKmh), ("ForceKn", point.ForceKn)))));
        InsertMany("tractionsetworkspacelimitpoint", scope, workspace.SpeedLimitPoints.Select((point, index) => Data(("OrderIndex", index), ("PositionM", point.PositionM), ("SpeedLimitKmh", point.SpeedLimitKmh))));
        InsertMany("tractionsetworkspacestop", scope, StopData(workspace.Settings.Run.Stops));
        InsertMany("tractionsetworkspaceoverride", scope, workspace.Overrides.Select((item, index) => Data(
            ("OrderIndex", index), ("RouteID", item.RouteId), ("RouteStartNodeID", item.RouteStartNodeId), ("RouteEndNodeID", item.RouteEndNodeId),
            ("ContinuationID", item.ContinuationId), ("SegmentIndex", item.SegmentIndex), ("SegmentID", item.SegmentId),
            ("SpeedLimitKmh", item.SpeedLimitKmh), ("GradientPermille", item.GradientPermille), ("AdditionalResistanceKn", item.AdditionalResistanceKn))));
    }

    public StoredTractionCalculation SaveCalculation(TractionScope scope, long revision, SpeedProfileCalculationRequest request, SpeedProfileCalculationResponse result)
    {
        TractionWorkspaceValidation.Revision(revision);
        TractionWorkspaceValidation.Request(request);
        request = request with { Train = TrainCharacteristicTables.Normalize(request.Train) };
        TractionWorkspaceValidation.Request(request);
        TractionWorkspaceValidation.Result(request, result);
        return Transaction(scope, () => {
            var workspace = ReadWorkspace(scope) ?? throw new KeyNotFoundException("Traction workspace does not exist.");
            if (workspace.Revision != revision) throw new TractionRevisionConflictException(workspace.Revision);
            TractionWorkspaceValidation.Matches(workspace, request);
            foreach (var table in TractionSchemaMigration.CalculationTables) db.ExecuteNonQuery($"DELETE FROM {ScopedTable(table, scope)} WHERE {ScopedFilter(table, scope)}", scope);
            var root = Data(("WorkspaceRevision", revision), ("SavedAtUtc", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)),
                ("SelectedRouteID", workspace.Settings.SelectedRouteId), ("ContinuationID", workspace.Settings.ContinuationId),
                ("ProfileLengthM", request.Profile.LengthM), ("ResultLengthM", result.LengthM), ("ResultTrainLengthM", result.TrainLengthM),
                ("ResultMode", result.Mode), ("DurationS", result.DurationS), ("MotionStartS", result.MotionStartS), ("TotalTimeS", result.TotalTimeS),
                ("RunningTimeS", result.RunningTimeS), ("DwellTimeS", result.DwellTimeS), ("ResultMaxSpeedKmh", result.MaxSpeedKmh), ("Engine", result.Engine));
            AddTrain(root, request.Train, "Train"); AddSignal(root, request.Signal); AddOptions(root, request.Options, "Input");
            Insert("tractionsetcalculation", scope, root);
            InsertMany("tractionsetcalculationpoint", scope, request.Train.TractionCurve.Select((point, index) => Data(("OrderIndex", index), ("SpeedKmh", point.SpeedKmh), ("ForceKn", point.ForceKn))));
            InsertMany("tractionsetcalculationbrakingpoint", scope, request.Train.BrakingCurve!.Select((point, index) => Data(("OrderIndex", index), ("SpeedKmh", point.SpeedKmh), ("ForceKn", point.ForceKn))));
            InsertMany("tractionsetcalculationresistancepoint", scope, request.Train.ResistanceCurve!.Select((point, index) => Data(("OrderIndex", index), ("SpeedKmh", point.SpeedKmh), ("ForceKn", point.ForceKn))));
            InsertMany("tractionsetcalculationsegment", scope, request.Profile.Segments.Select((item, index) => Data(("OrderIndex", index), ("StartM", item.StartM), ("EndM", item.EndM), ("SpeedLimitKmh", item.SpeedLimitKmh), ("GradientPermille", item.GradientPermille), ("AdditionalResistanceKn", item.AdditionalResistanceKn))));
            InsertMany("tractionsetcalculationdevice", scope, request.Profile.Devices.Select((item, index) => Data(("OrderIndex", index), ("ID", item.Id), ("Label", item.Label), ("Type", item.Type), ("PositionM", item.PositionM))));
            InsertMany("tractionsetcalculationjoint", scope, request.Profile.Joints.Select((item, index) => JointData(item, index)));
            InsertMany("tractionsetcalculationcell", scope, request.Profile.Cells.Select((item, index) => CellData(item, index)));
            InsertMany("tractionsetcalculationstop", scope, StopData(request.Options.Stops));
            InsertMany("tractionsetcalculationsample", scope, result.Samples.Select((item, index) => Data(("OrderIndex", index), ("PositionM", item.PositionM), ("SpeedKmh", item.SpeedKmh), ("LimitKmh", item.LimitKmh), ("TimeS", item.TimeS))));
            InsertMany("tractionsetcalculationjointtime", scope, result.JointTimes.Select((item, index) => {
                var row = JointData(item, index); row["HeadTimeS"] = item.HeadTimeS; row["TailTimeS"] = item.TailTimeS; row["ReleaseTimeS"] = item.ReleaseTimeS; return row;
            }));
            InsertMany("tractionsetcalculationcelltime", scope, result.CellOccupancies.Select((item, index) => {
                var row = CellData(item, index); row["EntryTimeS"] = item.EntryTimeS; row["ExitTimeS"] = item.ExitTimeS; row["ReleaseTimeS"] = item.ReleaseTimeS; return row;
            }));
            InsertMany("tractionsetcalculationwarning", scope, result.Warnings.Select((warning, index) => Data(("OrderIndex", index), ("Warning", warning))));
            TouchSet(scope);
            return ReadCalculation(scope)!;
        });
    }

    public TractionCalculationSetCatalog ListSets(TractionScope station) => Transaction(station, () => {
        if (!ReadSets(station).Any()) InsertSet(station with { CalculationSetID = "default" }, "默认计算方案");
        var sets = ReadSets(station);
        var selected = db.Query<string>($"SELECT SelectedCalculationSetID FROM tractionsetselection WHERE {StationFilter}", station)?.SingleOrDefault();
        if (!sets.Any(item => item.Id == selected)) { selected = sets[0].Id; SelectSetInternal(station with { CalculationSetID = selected }); }
        return new TractionCalculationSetCatalog(selected!, sets);
    }, requireSet: false);

    public TractionCalculationSet CreateSet(TractionScope station, string name, string? sourceId = null, long? sourceRevision = null) => Transaction(station, () => {
        ValidateSetName(station, name);
        if (ReadSets(station).Count >= 100) throw new ArgumentException("每个车站最多保留 100 套牵引计算方案。");
        TractionScope? source = null;
        if (sourceId is not null) {
            source = station with { CalculationSetID = sourceId }; TractionWorkspaceValidation.Scope(source);
            CheckSetRevision(source, sourceRevision ?? -1);
        }
        var target = station with { CalculationSetID = Guid.NewGuid().ToString("N") };
        InsertSet(target, name.Trim());
        if (source is not null) {
            var mysql = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName);
            foreach (var table in TractionSchemaMigration.DataTables.Reverse().Concat(TractionRouteSchemaMigration.Tables.Reverse())) {
                var columns = TractionSchemaMigration.Columns(db, mysql, table);
                var fields = string.Join(",", columns.Select(column => ((char)96) + column + ((char)96)));
                var values = string.Join(",", columns.Select(column => column == "CalculationSetID" ? "@targetID" : ((char)96) + column + ((char)96)));
                db.ExecuteNonQuery($"INSERT INTO {table}({fields}) SELECT {values} FROM {table} WHERE {Filter}", new { source.InstanceID, source.StationSchemeID, source.CalculationSetID, targetID = target.CalculationSetID });
            }
        }
        SelectSetInternal(target);
        return ReadSets(station).Single(item => item.Id == target.CalculationSetID);
    }, requireSet: false);

    public TractionCalculationSet RenameSet(TractionScope scope, long revision, string name) => Transaction(scope, () => {
        CheckSetRevision(scope, revision); ValidateSetName(scope, name, scope.CalculationSetID);
        db.ExecuteNonQuery($"UPDATE tractioncalculationset SET Name=@name WHERE {Filter}", new { scope.InstanceID, scope.StationSchemeID, scope.CalculationSetID, name = name.Trim() });
        TouchSet(scope);
        return ReadSets(scope).Single(item => item.Id == scope.CalculationSetID);
    });

    public void DeleteSet(TractionScope scope, long revision) => Transaction(scope, () => {
        CheckSetRevision(scope, revision);
        var remaining = ReadSets(scope).Where(item => item.Id != scope.CalculationSetID).ToArray();
        if (remaining.Length == 0) throw new ArgumentException("至少保留一套牵引计算方案。");
        var selected = db.Query<string>($"SELECT SelectedCalculationSetID FROM tractionsetselection WHERE {StationFilter}", scope)?.SingleOrDefault();
        if (selected == scope.CalculationSetID) SelectSetInternal(scope with { CalculationSetID = remaining[0].Id });
        foreach (var table in TractionRouteSchemaMigration.Tables) db.ExecuteNonQuery($"DELETE FROM {table} WHERE {Filter}", scope);
        foreach (var table in TractionSchemaMigration.DataTables) db.ExecuteNonQuery($"DELETE FROM {ScopedTable(table, scope)} WHERE {ScopedFilter(table, scope)}", scope);
        db.ExecuteNonQuery($"DELETE FROM tractioncalculationset WHERE {Filter}", scope);
        return true;
    });

    public void SelectSet(TractionScope scope) => Transaction(scope, () => { SelectSetInternal(scope); return true; });
    private void SelectSetInternal(TractionScope scope) {
        db.ExecuteNonQuery($"DELETE FROM tractionsetselection WHERE {StationFilter}", scope);
        db.ExecuteNonQuery("INSERT INTO tractionsetselection(InstanceID,StationSchemeID,SelectedCalculationSetID) VALUES(@InstanceID,@StationSchemeID,@CalculationSetID)", scope);
    }
    private void InsertSet(TractionScope scope, string name) {
        var now = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        Insert("tractioncalculationset", scope, Data(("Name", name), ("Revision", 1), ("CreatedAtUtc", now), ("UpdatedAtUtc", now)));
    }
    private void ValidateSetName(TractionScope station, string name, string? excludingId = null) {
        TractionWorkspaceValidation.Text(name, 80);
        if (ReadSets(station).Any(item => item.Id != excludingId && string.Equals(item.Name, name.Trim(), StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("计算方案名称已存在，请使用其他名称。");
    }
    private void RequireSet(TractionScope scope) {
        if (!Read("tractioncalculationset", scope).Any()) throw new KeyNotFoundException("牵引计算方案不存在或已删除，请刷新列表。");
    }
    private void CheckSetRevision(TractionScope scope, long revision) {
        var row = Read("tractioncalculationset", scope).SingleOrDefault() ?? throw new KeyNotFoundException("牵引计算方案不存在或已删除。");
        if (row.Long("Revision") != revision) throw new TractionRevisionConflictException(row.Long("Revision"));
    }
    private void TouchSet(TractionScope scope) {
        var now = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        if (scope.RouteCaseID.Length > 0) db.ExecuteNonQuery($"UPDATE tractionroutecase SET Revision=Revision+1 WHERE {CaseFilter}", scope);
        if (db.ExecuteNonQuery($"UPDATE tractioncalculationset SET Revision=Revision+1,UpdatedAtUtc=@now WHERE {Filter} AND Revision<@maximum", new { scope.InstanceID, scope.StationSchemeID, scope.CalculationSetID, now, maximum = TractionWorkspaceValidation.MaximumRevision }) != 1)
            throw new InvalidOperationException("Calculation set revision could not be updated.");
    }
    private List<TractionCalculationSet> ReadSets(TractionScope scope) => db.Query<TractionCalculationSet>(@"
        SELECT s.CalculationSetID AS Id,s.Name,s.Revision,s.CreatedAtUtc,s.UpdatedAtUtc,w.SelectedRouteID AS SelectedRouteId,p.Name AS TrainName,
            c.SavedAtUtc,c.DurationS,
            (SELECT COUNT(*) FROM tractionroutecase r WHERE r.InstanceID=s.InstanceID AND r.StationSchemeID=s.StationSchemeID AND r.CalculationSetID=s.CalculationSetID) AS RouteCount,
            (SELECT COUNT(*) FROM tractionroutecalculation r WHERE r.InstanceID=s.InstanceID AND r.StationSchemeID=s.StationSchemeID AND r.CalculationSetID=s.CalculationSetID) AS CalculatedCount,
            (SELECT COUNT(*) FROM tractionroutecalculation r JOIN tractionrouteworkspace w ON w.InstanceID=r.InstanceID AND w.StationSchemeID=r.StationSchemeID AND w.CalculationSetID=r.CalculationSetID AND w.RouteCaseID=r.RouteCaseID AND w.Revision=r.WorkspaceRevision WHERE r.InstanceID=s.InstanceID AND r.StationSchemeID=s.StationSchemeID AND r.CalculationSetID=s.CalculationSetID) AS CurrentCount,
            CASE WHEN c.WorkspaceRevision=w.Revision THEN 1 ELSE 0 END AS IsCurrent
        FROM tractioncalculationset s
        LEFT JOIN tractionsetworkspace w ON w.InstanceID=s.InstanceID AND w.StationSchemeID=s.StationSchemeID AND w.CalculationSetID=s.CalculationSetID
        LEFT JOIN tractionsettrainpreset p ON p.InstanceID=s.InstanceID AND p.StationSchemeID=s.StationSchemeID AND p.CalculationSetID=s.CalculationSetID AND p.PresetID=w.SelectedPresetID
        LEFT JOIN tractionsetcalculation c ON c.InstanceID=s.InstanceID AND c.StationSchemeID=s.StationSchemeID AND c.CalculationSetID=s.CalculationSetID
        WHERE s.InstanceID=@InstanceID AND s.StationSchemeID=@StationSchemeID ORDER BY s.CreatedAtUtc,s.CalculationSetID", scope) ?? [];

    public IReadOnlyList<TractionRouteCase> ListRoutes(TractionScope scope) => Transaction(scope, () => ReadRoutes(scope));
    private List<TractionRouteCase> ReadRoutes(TractionScope scope) => db.Query<TractionRouteCase>(@"
        SELECT r.RouteCaseID AS Id,r.RouteID AS RouteId,r.ContinuationID AS ContinuationId,r.IsSelected,r.Revision,p.Name AS TrainName,c.SavedAtUtc,c.DurationS,
            CASE WHEN c.WorkspaceRevision=w.Revision THEN 1 ELSE 0 END AS IsCurrent
        FROM tractionroutecase r
        LEFT JOIN tractionrouteworkspace w ON w.InstanceID=r.InstanceID AND w.StationSchemeID=r.StationSchemeID AND w.CalculationSetID=r.CalculationSetID AND w.RouteCaseID=r.RouteCaseID
        LEFT JOIN tractionroutetrainpreset p ON p.InstanceID=w.InstanceID AND p.StationSchemeID=w.StationSchemeID AND p.CalculationSetID=w.CalculationSetID AND p.RouteCaseID=w.RouteCaseID AND p.PresetID=w.SelectedPresetID
        LEFT JOIN tractionroutecalculation c ON c.InstanceID=r.InstanceID AND c.StationSchemeID=r.StationSchemeID AND c.CalculationSetID=r.CalculationSetID AND c.RouteCaseID=r.RouteCaseID
        WHERE r.InstanceID=@InstanceID AND r.StationSchemeID=@StationSchemeID AND r.CalculationSetID=@CalculationSetID ORDER BY r.RouteID,r.ContinuationID", scope) ?? [];

    public TractionRouteCase OpenRoute(TractionScope scope, string routeId, string continuationId) => Transaction(scope, () => {
        TractionWorkspaceValidation.ID(routeId); TractionWorkspaceValidation.ID(continuationId, empty: true);
        var existing = ReadRoutes(scope).SingleOrDefault(item => item.RouteId == routeId && item.ContinuationId == continuationId);
        var target = scope with { RouteCaseID = existing?.Id ?? Guid.NewGuid().ToString("N") };
        if (existing is null) {
            var ids = new[] { routeId, continuationId }.Where(id => id.Length > 0).Distinct().ToArray();
            var routes = db.Query<RouteIdentity>($"SELECT ID,StartNodeID,EndNodeID FROM stationroute WHERE {StationFilter} AND ID IN @ids", new { scope.InstanceID, scope.StationSchemeID, ids }) ?? [];
            if (routes.Count != ids.Length) throw new ArgumentException("进路不属于当前车站。");
            if (continuationId.Length > 0 && (routeId == continuationId || string.IsNullOrEmpty(routes.Single(r => r.ID == routeId).EndNodeID) || routes.Single(r => r.ID == routeId).EndNodeID != routes.Single(r => r.ID == continuationId).StartNodeID))
                throw new ArgumentException("接续进路与主进路不相连。");
            TractionRouteSchemaMigration.InsertCase(db, target, routeId, continuationId, false);
            TouchSet(target);
        }
        db.ExecuteNonQuery($"UPDATE tractionroutecase SET IsSelected=0 WHERE {Filter}", scope);
        db.ExecuteNonQuery($"UPDATE tractionroutecase SET IsSelected=1 WHERE {CaseFilter}", target);
        return ReadRoutes(scope).Single(item => item.Id == target.RouteCaseID);
    });
    public void DeleteRouteCase(TractionScope scope, long revision) => Transaction(scope, () => {
        var current = RequireCase(scope);
        if (current.Revision != revision) throw new TractionRevisionConflictException(current.Revision);
        foreach (var table in TractionRouteSchemaMigration.Tables) db.ExecuteNonQuery($"DELETE FROM {table} WHERE {CaseFilter}", scope);
        TouchSet(scope with { RouteCaseID = "" });
        return true;
    });
    private TractionRouteCase RequireCase(TractionScope scope) => ReadRoutes(scope).SingleOrDefault(item => item.Id == scope.RouteCaseID)
        ?? throw new KeyNotFoundException("方案中的进路记录不存在或已删除，请刷新列表。");
    private void ValidateCaseIdentity(TractionScope scope, TractionWorkspace workspace) {
        if (scope.RouteCaseID.Length == 0) return;
        var current = RequireCase(scope);
        if (current.RouteId != workspace.Settings.SelectedRouteId || current.ContinuationId != workspace.Settings.ContinuationId)
            throw new ArgumentException("参数中的进路与当前进路记录不一致。");
    }

    private sealed class RouteIdentity
    {
        public string ID { get; set; } = "";
        public string? StartNodeID { get; set; }
        public string? EndNodeID { get; set; }
    }
    private void ValidateRouteReferences(TractionScope scope, TractionWorkspace workspace)
    {
        var required = workspace.Overrides.SelectMany(item => new[] { item.RouteId, item.ContinuationId })
            .Concat(new[] { workspace.Settings.SelectedRouteId, workspace.Settings.ContinuationId })
            .Where(id => id.Length > 0).Distinct(StringComparer.Ordinal).ToArray();
        var found = new Dictionary<string, RouteIdentity>(StringComparer.Ordinal);
        foreach (var ids in required.Chunk(250)) {
            var rows = db.Query<RouteIdentity>($"SELECT ID,StartNodeID,EndNodeID FROM stationroute WHERE {StationFilter} AND ID IN @ids",
                new { scope.InstanceID, scope.StationSchemeID, ids }) ?? [];
            foreach (var row in rows) {
                if (!found.TryAdd(row.ID, row)) throw new ArgumentException("Duplicate route identity in station scheme.");
            }
        }
        if (required.Any(id => !found.ContainsKey(id))) throw new ArgumentException("A traction route no longer belongs to the selected station scheme.");
        if (workspace.Settings.ContinuationId.Length > 0) {
            var first = found[workspace.Settings.SelectedRouteId]; var next = found[workspace.Settings.ContinuationId];
            if (string.IsNullOrWhiteSpace(first.EndNodeID) || string.IsNullOrWhiteSpace(next.StartNodeID) || first.EndNodeID != next.StartNodeID)
                throw new ArgumentException("The continuation route does not start at the selected route's end node.");
        }
        // Inactive overrides retain their original geometry identity; edited routes may make them inapplicable.
    }
    private T Transaction<T>(TractionScope scope, Func<T> body, bool requireSet = true)
    {
        TractionWorkspaceValidation.Scope(scope);
        db.BeginTransaction();
        try { LockScope(db, scope); if (requireSet) { RequireSet(scope); if (scope.RouteCaseID.Length > 0) RequireCase(scope); } var value = body(); db.Commit(); return value; }
        catch { db.Rollback(); throw; }
    }
    private static void LockScope(DBConnector db, TractionScope scope)
    {
        var suffix = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName) ? " FOR UPDATE" : "";
        var schemes = db.Query<string>($"SELECT ID FROM stationscheme WHERE InstanceID=@InstanceID AND ID=@StationSchemeID{suffix}", scope);
        if (schemes is null || schemes.Count == 0) throw new KeyNotFoundException("Station scheme does not exist.");
    }

    /// <summary>Called inside the lifecycle's existing transaction; no optional tables are created.</summary>
    public static void DeleteRouteData(DBConnector db, string instanceID, string stationSchemeID, string routeID)
    {
        var scope = new TractionScope(instanceID, stationSchemeID);
        TractionWorkspaceValidation.Scope(scope); TractionWorkspaceValidation.ID(routeID);
        var mysql = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName);
        var exists = db.Query<int>(mysql
            ? "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema=DATABASE() AND table_name='tractionsetworkspace'"
            : "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='tractionsetworkspace'")?.Single() ?? 0;
        if (exists == 0) return;
        LockScope(db, scope);
        var store = new TractionWorkspaceStore(db);
        if (TractionSchemaMigration.Exists(db, mysql, "tractionroutecase")) {
            var cases = db.Query<CaseScope>($"SELECT CalculationSetID,RouteCaseID FROM tractionroutecase WHERE {StationFilter}", scope) ?? [];
            foreach (var item in cases) {
                var target = scope with { CalculationSetID = item.CalculationSetID, RouteCaseID = item.RouteCaseID };
                var workspace = store.ReadWorkspace(target);
                if (workspace is null) continue;
                var affected = workspace.Settings.SelectedRouteId == routeID || workspace.Settings.ContinuationId == routeID;
                var parameters = new { target.InstanceID, target.StationSchemeID, target.CalculationSetID, target.RouteCaseID, routeID };
                var deleted = db.ExecuteNonQuery($"DELETE FROM tractionrouteworkspaceoverride WHERE {CaseFilter} AND (RouteID=@routeID OR ContinuationID=@routeID)", parameters);
                if (!affected && deleted == 0) continue;
                if (workspace.Revision >= TractionWorkspaceValidation.MaximumRevision) throw new ArgumentException("Workspace revision is exhausted.");
                // Retain the historical route identity and complete result for inspection.
                db.ExecuteNonQuery($"UPDATE tractionrouteworkspace SET Revision=Revision+1 WHERE {CaseFilter}", target);
                store.TouchSet(target);
            }
        }
        var roots = db.Query<string>($"SELECT CalculationSetID FROM tractionsetworkspace WHERE {StationFilter}", scope) ?? [];
        foreach (var id in roots) {
            var target = scope with { CalculationSetID = id };
            var root = store.Read("tractionsetworkspace", target).Single();
            var selected = root.String("SelectedRouteID") == routeID;
            var continuation = root.String("ContinuationID") == routeID;
            var parameters = new { target.InstanceID, target.StationSchemeID, target.CalculationSetID, routeID, selectedRouteID = selected ? "" : root.String("SelectedRouteID") };
            var deleted = db.ExecuteNonQuery($"DELETE FROM tractionsetworkspaceoverride WHERE {Filter} AND (RouteID=@routeID OR ContinuationID=@routeID)", parameters);
            if (!selected && !continuation && deleted == 0) continue;
            if (root.Long("Revision") >= TractionWorkspaceValidation.MaximumRevision) throw new ArgumentException("Workspace revision is exhausted.");
            if (selected || continuation) {
                db.ExecuteNonQuery($"DELETE FROM tractionsetworkspacestop WHERE {Filter}", target);
                db.ExecuteNonQuery($"UPDATE tractionsetworkspace SET SelectedRouteID=@selectedRouteID,ContinuationID='',Revision=Revision+1 WHERE {Filter}", parameters);
            } else db.ExecuteNonQuery($"UPDATE tractionsetworkspace SET Revision=Revision+1 WHERE {Filter}", target);
            store.TouchSet(target);
        }
    }

    private sealed class CaseScope { public string CalculationSetID { get; set; } = ""; public string RouteCaseID { get; set; } = ""; }
    internal TractionWorkspace? ReadWorkspace(TractionScope scope)
    {
        var root = Read("tractionsetworkspace", scope).SingleOrDefault();
        if (root is null) return null;
        var points = Read("tractionsetpresetpoint", scope, "PresetID,OrderIndex").GroupBy(row => row.String("PresetID"), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<SpeedProfileForceInput>)group.Select(Force).ToArray(), StringComparer.Ordinal);
        Dictionary<string, IReadOnlyList<SpeedProfileForceInput>> Curves(string table) => Read(table, scope, "PresetID,OrderIndex")
            .GroupBy(row => row.String("PresetID"), StringComparer.Ordinal).ToDictionary(g => g.Key, g => (IReadOnlyList<SpeedProfileForceInput>)g.Select(Force).ToArray(), StringComparer.Ordinal);
        var brakes = Curves("tractionsetpresetbrakingpoint"); var resistance = Curves("tractionsetpresetresistancepoint");
        return new TractionWorkspace {
            Revision = root.Long("Revision"),
            Catalog = new TractionTrainPresetCatalog { Version = root.Int("CatalogVersion"), SelectedId = root.String("SelectedPresetID"),
                Presets = Read("tractionsettrainpreset", scope, "OrderIndex").Select(row => new TractionTrainPreset {
                    Id = row.String("PresetID"), Name = row.String("Name"), NameEn = row.NullableString("NameEn"),
                    Parameters = Train(row, points.GetValueOrDefault(row.String("PresetID")) ?? [], brakes.GetValueOrDefault(row.String("PresetID")), resistance.GetValueOrDefault(row.String("PresetID"))),
                }).ToArray() },
            Settings = new TractionWorkspaceSettings {
                SelectedRouteId = root.String("SelectedRouteID"), ContinuationId = root.String("ContinuationID"), DistanceMode = root.String("DistanceMode"),
                MetersPerUnit = root.Number("MetersPerUnit"), DefaultLimit = root.Number("DefaultLimit"), Signal = Signal(root),
                Run = Options(root, "Run", Read("tractionsetworkspacestop", scope, "OrderIndex").Select(Stop).ToArray()),
            },
            SpeedLimitPoints = Read("tractionsetworkspacelimitpoint", scope, "OrderIndex").Select(row => new TractionSpeedLimitPoint { PositionM = row.Number("PositionM"), SpeedLimitKmh = row.Number("SpeedLimitKmh") }).ToArray(),
            Overrides = Read("tractionsetworkspaceoverride", scope, "OrderIndex").Select(row => new TractionSegmentOverride {
                RouteId = row.String("RouteID"), RouteStartNodeId = row.String("RouteStartNodeID"), RouteEndNodeId = row.String("RouteEndNodeID"), ContinuationId = row.String("ContinuationID"),
                SegmentIndex = row.Int("SegmentIndex"), SegmentId = row.String("SegmentID"), SpeedLimitKmh = row.NullableNumber("SpeedLimitKmh"), GradientPermille = row.NullableNumber("GradientPermille"), AdditionalResistanceKn = row.NullableNumber("AdditionalResistanceKn"),
            }).ToArray(),
        };
    }

    internal StoredTractionCalculation? ReadCalculation(TractionScope scope)
    {
        var root = Read("tractionsetcalculation", scope).SingleOrDefault();
        if (root is null) return null;
        var current = Read("tractionsetworkspace", scope).Single();
        var request = new SpeedProfileCalculationRequest {
            Train = Train(root, Read("tractionsetcalculationpoint", scope, "OrderIndex").Select(Force).ToArray(), Read("tractionsetcalculationbrakingpoint", scope, "OrderIndex").Select(Force).ToArray(), Read("tractionsetcalculationresistancepoint", scope, "OrderIndex").Select(Force).ToArray(), "Train"), Signal = Signal(root),
            Options = Options(root, "Input", Read("tractionsetcalculationstop", scope, "OrderIndex").Select(Stop).ToArray()),
            Profile = new SpeedProfileRouteInput {
                LengthM = root.Number("ProfileLengthM"),
                Segments = Read("tractionsetcalculationsegment", scope, "OrderIndex").Select(row => new SpeedProfileSegmentInput { StartM = row.Number("StartM"), EndM = row.Number("EndM"), SpeedLimitKmh = row.Number("SpeedLimitKmh"), GradientPermille = row.Number("GradientPermille"), AdditionalResistanceKn = row.Number("AdditionalResistanceKn") }).ToArray(),
                Devices = Read("tractionsetcalculationdevice", scope, "OrderIndex").Select(row => new SpeedProfileDeviceInput { Id = row.String("ID"), Label = row.String("Label"), Type = row.String("Type"), PositionM = row.Number("PositionM") }).ToArray(),
                Joints = Read("tractionsetcalculationjoint", scope, "OrderIndex").Select(row => new SpeedProfileJointInput { Id = row.String("ID"), Label = row.String("Label"), PositionM = row.Number("PositionM") }).ToArray(),
                Cells = Read("tractionsetcalculationcell", scope, "OrderIndex").Select(row => new SpeedProfileCellInput { Id = row.String("ID"), Label = row.String("Label"), StartM = row.Number("StartM"), EndM = row.Number("EndM"), EntryBoundaryKnown = row.Boolean("EntryBoundaryKnown"), ExitBoundaryKnown = row.Boolean("ExitBoundaryKnown") }).ToArray(),
            },
        };
        return new StoredTractionCalculation {
            SelectedRouteId = root.String("SelectedRouteID"), ContinuationId = root.String("ContinuationID"),
            Revision = root.Long("WorkspaceRevision"), CurrentWorkspaceRevision = current.Long("Revision"), SavedAtUtc = DateTime.Parse(root.String("SavedAtUtc"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind), Request = request,
            Result = new SpeedProfileCalculationResponse {
                ApproachDistanceM = Math.Max(0, root.Number("ResultLengthM") - root.Number("ProfileLengthM")),
                LengthM = root.Number("ResultLengthM"), TrainLengthM = root.Number("ResultTrainLengthM"), Mode = root.String("ResultMode"), DurationS = root.Number("DurationS"), MotionStartS = root.Number("MotionStartS"), TotalTimeS = root.Number("TotalTimeS"), RunningTimeS = root.Number("RunningTimeS"), DwellTimeS = root.Number("DwellTimeS"), MaxSpeedKmh = root.Number("ResultMaxSpeedKmh"), Engine = root.String("Engine"),
                Samples = Read("tractionsetcalculationsample", scope, "OrderIndex").Select(row => new SpeedProfileSample(row.Number("PositionM"), row.Number("SpeedKmh"), row.Number("LimitKmh"), row.Number("TimeS"))).ToArray(),
                JointTimes = Read("tractionsetcalculationjointtime", scope, "OrderIndex").Select(row => new SpeedProfileJointTime { Id = row.String("ID"), Label = row.String("Label"), PositionM = row.Number("PositionM"), HeadTimeS = row.Number("HeadTimeS"), TailTimeS = row.NullableNumber("TailTimeS"), ReleaseTimeS = row.NullableNumber("ReleaseTimeS") }).ToArray(),
                CellOccupancies = Read("tractionsetcalculationcelltime", scope, "OrderIndex").Select(row => new SpeedProfileCellOccupancy { Id = row.String("ID"), Label = row.String("Label"), StartM = row.Number("StartM"), EndM = row.Number("EndM"), EntryBoundaryKnown = row.Boolean("EntryBoundaryKnown"), ExitBoundaryKnown = row.Boolean("ExitBoundaryKnown"), EntryTimeS = row.NullableNumber("EntryTimeS"), ExitTimeS = row.NullableNumber("ExitTimeS"), ReleaseTimeS = row.NullableNumber("ReleaseTimeS") }).ToArray(),
                Warnings = Read("tractionsetcalculationwarning", scope, "OrderIndex").Select(row => row.String("Warning")).ToArray(),
            },
        };
    }

    private List<DbRow> Read(string table, TractionScope scope, string? order = null) =>
        (db.Query<dynamic>($"SELECT * FROM {ScopedTable(table, scope)} WHERE {ScopedFilter(table, scope)}" + (order is null ? "" : $" ORDER BY {order}"), scope) ?? []).Select(value => new DbRow((IDictionary<string, object>)value)).ToList();
    private sealed class DbRow
    {
        private readonly IDictionary<string, object> values;
        public DbRow(IDictionary<string, object> values) => this.values = values;
        public string String(string name) => Convert.ToString(values[name], CultureInfo.InvariantCulture) ?? "";
        public string? NullableString(string name) => values[name] is null or DBNull ? null : String(name);
        public double Number(string name) => Convert.ToDouble(values[name], CultureInfo.InvariantCulture);
        public double? NullableNumber(string name) => values[name] is null or DBNull ? null : Number(name);
        public int Int(string name) => Convert.ToInt32(values[name], CultureInfo.InvariantCulture);
        public long Long(string name) => Convert.ToInt64(values[name], CultureInfo.InvariantCulture);
        public bool Boolean(string name) => Convert.ToInt32(values[name], CultureInfo.InvariantCulture) != 0;
    }
    private static Dictionary<string, object?> Data(params (string Key, object? Value)[] values) => values.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
    private static Dictionary<string, object?> WorkspaceData(TractionWorkspace workspace)
    {
        var settings = workspace.Settings;
        var row = Data(("Revision", workspace.Revision), ("CatalogVersion", workspace.Catalog.Version), ("SelectedPresetID", workspace.Catalog.SelectedId),
            ("SelectedRouteID", settings.SelectedRouteId), ("ContinuationID", settings.ContinuationId), ("DistanceMode", settings.DistanceMode), ("MetersPerUnit", settings.MetersPerUnit), ("DefaultLimit", settings.DefaultLimit));
        AddSignal(row, settings.Signal); AddOptions(row, settings.Run, "Run"); return row;
    }
    private static void AddTrain(Dictionary<string, object?> row, SpeedProfileTrainInput train, string prefix = "")
    {
        row[prefix + "LengthM"] = train.LengthM; row[prefix + "MassT"] = train.MassT; row[prefix + "RotatingMassFactor"] = train.RotatingMassFactor;
        row[prefix + "MaxSpeedKmh"] = train.MaxSpeedKmh; row[prefix + "MaxBrakeForceKn"] = train.MaxBrakeForceKn;
        row[prefix + "ResistanceA"] = train.ResistanceA; row[prefix + "ResistanceB"] = train.ResistanceB; row[prefix + "ResistanceC"] = train.ResistanceC;
    }
    private static SpeedProfileTrainInput Train(DbRow row, IReadOnlyList<SpeedProfileForceInput> curve, IReadOnlyList<SpeedProfileForceInput>? braking, IReadOnlyList<SpeedProfileForceInput>? resistance, string prefix = "") => TrainCharacteristicTables.Normalize(new() {
        LengthM = row.Number(prefix + "LengthM"), MassT = row.Number(prefix + "MassT"), RotatingMassFactor = row.Number(prefix + "RotatingMassFactor"), MaxSpeedKmh = row.Number(prefix + "MaxSpeedKmh"),
        MaxBrakeForceKn = row.Number(prefix + "MaxBrakeForceKn"), ResistanceA = row.Number(prefix + "ResistanceA"), ResistanceB = row.Number(prefix + "ResistanceB"), ResistanceC = row.Number(prefix + "ResistanceC"), TractionCurve = curve,
        BrakingCurve = braking is { Count: > 0 } ? braking : null, ResistanceCurve = resistance is { Count: > 0 } ? resistance : null,
    });
    private static void AddSignal(Dictionary<string, object?> row, SpeedProfileSignalInput signal) { row["SignalRouteSettingS"] = signal.RouteSettingS; row["SignalReactionS"] = signal.ReactionS; row["SignalClearanceS"] = signal.ClearanceS; }
    private static SpeedProfileSignalInput Signal(DbRow row) => new() { RouteSettingS = row.Number("SignalRouteSettingS"), ReactionS = row.Number("SignalReactionS"), ClearanceS = row.Number("SignalClearanceS") };
    private static void AddOptions(Dictionary<string, object?> row, SpeedProfileOptionsInput options, string prefix) { row[prefix + "Mode"] = options.Mode; row[prefix + "InitialSpeedKmh"] = options.InitialSpeedKmh; row[prefix + "FinalSpeedKmh"] = options.FinalSpeedKmh; row[prefix + "StepM"] = options.StepM; row[prefix + "MaxPointCount"] = options.MaxPointCount; }
    private static SpeedProfileOptionsInput Options(DbRow row, string prefix, IReadOnlyList<SpeedProfileStopInput> stops) => new() { Mode = row.String(prefix + "Mode"), InitialSpeedKmh = row.Number(prefix + "InitialSpeedKmh"), FinalSpeedKmh = row.Number(prefix + "FinalSpeedKmh"), StepM = row.Number(prefix + "StepM"), MaxPointCount = row.Int(prefix + "MaxPointCount"), Stops = stops };
    private static SpeedProfileForceInput Force(DbRow row) => new() { SpeedKmh = row.Number("SpeedKmh"), ForceKn = row.Number("ForceKn") };
    private static SpeedProfileStopInput Stop(DbRow row) => new() { PositionM = row.Number("PositionM"), DwellS = row.Number("DwellS") };
    private static IEnumerable<Dictionary<string, object?>> StopData(IReadOnlyList<SpeedProfileStopInput> stops) => stops.Select((stop, index) => Data(("OrderIndex", index), ("PositionM", stop.PositionM), ("DwellS", stop.DwellS)));
    private static Dictionary<string, object?> JointData(SpeedProfileJointInput item, int index) => Data(("OrderIndex", index), ("ID", item.Id), ("Label", item.Label), ("PositionM", item.PositionM));
    private static Dictionary<string, object?> CellData(SpeedProfileCellInput item, int index) => Data(("OrderIndex", index), ("ID", item.Id), ("Label", item.Label), ("StartM", item.StartM), ("EndM", item.EndM), ("EntryBoundaryKnown", item.EntryBoundaryKnown ? 1 : 0), ("ExitBoundaryKnown", item.ExitBoundaryKnown ? 1 : 0));
    private void Insert(string table, TractionScope scope, Dictionary<string, object?> row) => InsertMany(table, scope, [row]);
    private void InsertMany(string table, TractionScope scope, IEnumerable<Dictionary<string, object?>> rows)
    {
        // Bounded multi-row inserts avoid one round trip per speed sample on MySQL.
        foreach (var chunk in rows.Chunk(250)) {
            var columns = chunk[0].Keys.ToArray(); var parameters = new DynamicParameters(); var groups = new List<string>(chunk.Length);
            parameters.Add("scopeInstance", scope.InstanceID); parameters.Add("scopeScheme", scope.StationSchemeID); parameters.Add("scopeSet", scope.CalculationSetID);
            parameters.Add("scopeCase", scope.RouteCaseID);
            for (int index = 0; index < chunk.Length; index++) {
                var names = new List<string> { "@scopeInstance", "@scopeScheme", "@scopeSet" };
                if (IsRouteTable(table, scope)) names.Add("@scopeCase");
                foreach (var column in columns) { var name = $"p{index}_{column}"; parameters.Add(name, chunk[index][column]); names.Add("@" + name); }
                groups.Add("(" + string.Join(",", names) + ")");
            }
            db.ExecuteNonQuery($"INSERT INTO {ScopedTable(table, scope)}(InstanceID,StationSchemeID,CalculationSetID,{(IsRouteTable(table, scope) ? "RouteCaseID," : "")}{string.Join(",", columns)}) VALUES {string.Join(",", groups)}", parameters);
        }
    }
    private void Update(string table, TractionScope scope, Dictionary<string, object?> row)
    {
        var parameters = new DynamicParameters(row); parameters.Add("InstanceID", scope.InstanceID); parameters.Add("StationSchemeID", scope.StationSchemeID); parameters.Add("CalculationSetID", scope.CalculationSetID); parameters.Add("RouteCaseID", scope.RouteCaseID);
        if (db.ExecuteNonQuery($"UPDATE {ScopedTable(table, scope)} SET {string.Join(",", row.Keys.Select(key => key + "=@" + key))} WHERE {ScopedFilter(table, scope)}", parameters) != 1) throw new InvalidOperationException("Workspace update did not affect exactly one row.");
    }
}

