using System.Text.Json;
using Dapper;
using SwitchYard.Service.Models;

namespace SwitchYard.Service.Services;

public static class TractionProjectTrainSchema
{
    public static readonly string[] Tables = ["tractionprojecttrainpoint", "tractionprojecttrainbrakingpoint", "tractionprojecttrainresistancepoint", "tractionprojecttrainpreset", "tractionprojecttrains"];
    public static void Ensure(DBConnector db)
    {
        var mysql = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName);
        string Text(int n) => mysql ? $"VARCHAR({n}) COLLATE utf8mb4_bin" : "TEXT";
        var number = mysql ? "DOUBLE" : "REAL";
        var suffix = mysql ? " ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_bin" : "";
        db.ExecuteNonQuery($"CREATE TABLE IF NOT EXISTS tractionprojecttrains(InstanceID {Text(50)} NOT NULL PRIMARY KEY,Revision BIGINT NOT NULL,Version INTEGER NOT NULL,SelectedPresetID {Text(200)} NOT NULL){suffix}");
        var scalars = string.Join(",", new[] { "LengthM", "MassT", "RotatingMassFactor", "MaxSpeedKmh", "MaxBrakeForceKn", "ResistanceA", "ResistanceB", "ResistanceC" }.Select(n => n + " " + number + " NOT NULL"));
        db.ExecuteNonQuery($"CREATE TABLE IF NOT EXISTS tractionprojecttrainpreset(InstanceID {Text(50)} NOT NULL,PresetID {Text(200)} NOT NULL,OrderIndex INTEGER NOT NULL,Name {Text(80)} NOT NULL,NameEn {Text(80)} NULL,{scalars},PRIMARY KEY(InstanceID,PresetID),UNIQUE(InstanceID,OrderIndex),FOREIGN KEY(InstanceID) REFERENCES tractionprojecttrains(InstanceID) ON DELETE CASCADE){suffix}");
        foreach (var table in Tables.Take(3))
            db.ExecuteNonQuery($"CREATE TABLE IF NOT EXISTS {table}(InstanceID {Text(50)} NOT NULL,PresetID {Text(200)} NOT NULL,OrderIndex INTEGER NOT NULL,SpeedKmh {number} NOT NULL,ForceKn {number} NOT NULL,PRIMARY KEY(InstanceID,PresetID,OrderIndex),FOREIGN KEY(InstanceID,PresetID) REFERENCES tractionprojecttrainpreset(InstanceID,PresetID) ON DELETE CASCADE){suffix}");
    }
}

public sealed partial class TractionWorkspaceStore
{
    public TractionProjectTrains? GetProjectTrains(TractionScope scope) => Transaction(scope, () => ReadProjectTrains(scope.InstanceID), requireSet: false);

    public TractionProjectTrains SaveProjectTrains(TractionScope scope, long revision, TractionTrainPresetCatalog catalog)
    {
        TractionWorkspaceValidation.Revision(revision);
        TractionWorkspaceValidation.Catalog(catalog);
        catalog = NormalizeCatalog(catalog); TractionWorkspaceValidation.Catalog(catalog);
        return Transaction(scope, () => {
            var old = ReadProjectTrains(scope.InstanceID);
            if ((old?.Revision ?? 0) != revision) throw new TractionProjectTrainConflictException(old?.Revision ?? 0);
            if (revision >= TractionWorkspaceValidation.MaximumRevision) throw new ArgumentException("Project train revision is exhausted.");
            var nextById = catalog.Presets.ToDictionary(p => p.Id, StringComparer.Ordinal);
            var affected = (old?.Catalog.Presets ?? []).Where(p => !nextById.TryGetValue(p.Id, out var next) || !SameTrain(p.Parameters, next.Parameters)).Select(p => p.Id).ToHashSet(StringComparer.Ordinal);
            foreach (var item in ProjectWorkspaces(scope.InstanceID)) {
                if (!affected.Contains(item.SelectedPresetID)) continue;
                var selected = nextById.ContainsKey(item.SelectedPresetID) ? item.SelectedPresetID : catalog.SelectedId;
                if (item.Revision >= TractionWorkspaceValidation.MaximumRevision) throw new ArgumentException("Workspace revision is exhausted.");
                db.ExecuteNonQuery($"UPDATE {item.Table} SET SelectedPresetID=@selected,Revision=Revision+1 WHERE {ScopedFilter("tractionsetworkspace", item.Scope)}",
                    new { item.Scope.InstanceID, item.Scope.StationSchemeID, item.Scope.CalculationSetID, item.Scope.RouteCaseID, selected });
                TouchSet(item.Scope);
            }
            WriteProjectTrains(scope.InstanceID, revision + 1, catalog);
            return ReadProjectTrains(scope.InstanceID)!;
        }, requireSet: false);
    }

    private static TractionTrainPresetCatalog NormalizeCatalog(TractionTrainPresetCatalog catalog) => catalog with {
        Presets = catalog.Presets.Select(p => p with { Parameters = TrainCharacteristicTables.Normalize(p.Parameters) }).ToArray()
    };
    private static bool SameTrain(SpeedProfileTrainInput a, SpeedProfileTrainInput b) => JsonSerializer.Serialize(TrainCharacteristicTables.Normalize(a)) == JsonSerializer.Serialize(TrainCharacteristicTables.Normalize(b));
    private static bool SamePresets(TractionTrainPresetCatalog a, TractionTrainPresetCatalog b) => a.Version == b.Version && JsonSerializer.Serialize(a.Presets) == JsonSerializer.Serialize(b.Presets);

    private void LockProjectTrains(TractionScope scope)
    {
        var mysql = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName);
        // INSERT IGNORE takes a shared duplicate-key lock in InnoDB. Upgrading that lock
        // with SELECT FOR UPDATE deadlocks when parallel workspace/result reads arrive.
        // The no-op upsert takes the exclusive project lock immediately, without changing
        // its revision or defaults. SQLite serializes writers with its transaction lock.
        var sql = mysql
            ? "INSERT INTO tractionprojecttrains(InstanceID,Revision,Version,SelectedPresetID) VALUES(@InstanceID,0,1,'') ON DUPLICATE KEY UPDATE Revision=Revision"
            : "INSERT OR IGNORE INTO tractionprojecttrains(InstanceID,Revision,Version,SelectedPresetID) VALUES(@InstanceID,0,1,'')";
        db.ExecuteNonQuery(sql, scope);
    }

    private TractionProjectTrains? ReadProjectTrains(string instanceID)
    {
        var root = ProjectRows("tractionprojecttrains", instanceID).SingleOrDefault();
        if (root is null || root.Long("Revision") == 0) return null;
        Dictionary<string, IReadOnlyList<SpeedProfileForceInput>> Points(string table) => ProjectRows(table, instanceID, "PresetID,OrderIndex")
            .GroupBy(row => row.String("PresetID"), StringComparer.Ordinal).ToDictionary(g => g.Key, g => (IReadOnlyList<SpeedProfileForceInput>)g.Select(Force).ToArray(), StringComparer.Ordinal);
        var traction = Points("tractionprojecttrainpoint"); var braking = Points("tractionprojecttrainbrakingpoint"); var resistance = Points("tractionprojecttrainresistancepoint");
        return new(root.Long("Revision"), new() { Version = root.Int("Version"), SelectedId = root.String("SelectedPresetID"),
            Presets = ProjectRows("tractionprojecttrainpreset", instanceID, "OrderIndex").Select(row => new TractionTrainPreset {
                Id = row.String("PresetID"), Name = row.String("Name"), NameEn = row.NullableString("NameEn"),
                Parameters = Train(row, traction.GetValueOrDefault(row.String("PresetID")) ?? [], braking.GetValueOrDefault(row.String("PresetID")), resistance.GetValueOrDefault(row.String("PresetID")))
            }).ToArray() }) { InstanceID = instanceID };
    }
    private List<DbRow> ProjectRows(string table, string instanceID, string? order = null) =>
        (db.Query<dynamic>($"SELECT * FROM {table} WHERE InstanceID=@instanceID" + (order is null ? "" : " ORDER BY " + order), new { instanceID }) ?? [])
        .Select(row => new DbRow((IDictionary<string, object>)row)).ToList();

    private sealed class ProjectWorkspaceRow
    {
        public string InstanceID { get; set; } = ""; public string StationSchemeID { get; set; } = "";
        public string CalculationSetID { get; set; } = ""; public string RouteCaseID { get; set; } = "";
        public string SelectedPresetID { get; set; } = ""; public long Revision { get; set; }
        public TractionScope Scope => new(InstanceID, StationSchemeID, CalculationSetID, RouteCaseID);
        public string Table => RouteCaseID.Length == 0 ? "tractionsetworkspace" : "tractionrouteworkspace";
    }
    private List<ProjectWorkspaceRow> ProjectWorkspaces(string instanceID) => db.Query<ProjectWorkspaceRow>(@"
        SELECT InstanceID,StationSchemeID,CalculationSetID,RouteCaseID,SelectedPresetID,Revision FROM tractionrouteworkspace WHERE InstanceID=@instanceID
        UNION ALL SELECT InstanceID,StationSchemeID,CalculationSetID,'' AS RouteCaseID,SelectedPresetID,Revision FROM tractionsetworkspace WHERE InstanceID=@instanceID
        ORDER BY StationSchemeID,CalculationSetID,RouteCaseID", new { instanceID }) ?? [];

    private TractionProjectTrains? EnsureProjectTrains(TractionScope scope, TractionTrainPresetCatalog? seed = null)
    {
        var project = ReadProjectTrains(scope.InstanceID);
        if (project is not null) return project;
        var presets = new List<TractionTrainPreset>(); var merged = new Dictionary<string, TractionTrainPreset>(StringComparer.Ordinal); string? selected = null;
        string UniqueName(string name, bool english)
        {
            var original = name; var index = 1;
            while (presets.Any(p => string.Equals((english ? p.NameEn : p.Name)?.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase))) {
                var suffix = $" ({++index})";
                name = string.Concat(original.EnumerateRunes().Take(80 - suffix.Length).Select(r => r.ToString())) + suffix;
            }
            return name;
        }
        foreach (var item in ProjectWorkspaces(scope.InstanceID)) {
            var workspace = ReadWorkspace(item.Scope, legacyCatalog: true)!;
            string? routeSelection = null;
            foreach (var preset in workspace.Catalog.Presets) {
                var signature = JsonSerializer.Serialize(new { Name = preset.Name.Trim().ToUpperInvariant(), NameEn = preset.NameEn?.Trim().ToUpperInvariant(), Train = TrainCharacteristicTables.Normalize(preset.Parameters) });
                merged.TryGetValue(signature, out var same);
                if (same is null) {
                    var id = presets.Any(p => p.Id == preset.Id) ? Guid.NewGuid().ToString("N") : preset.Id;
                    same = preset with { Id = id, Name = UniqueName(preset.Name, false), NameEn = preset.NameEn is null ? null : UniqueName(preset.NameEn, true) };
                    presets.Add(same);
                    merged[signature] = same;
                }
                if (workspace.Catalog.SelectedId == preset.Id) routeSelection = same.Id;
            }
            if (routeSelection is null) throw new ArgumentException("旧进路的列车选择无效，无法合并项目列车参数。");
            selected ??= routeSelection;
            db.ExecuteNonQuery($"UPDATE {item.Table} SET SelectedPresetID=@routeSelection WHERE {ScopedFilter("tractionsetworkspace", item.Scope)}",
                new { item.Scope.InstanceID, item.Scope.StationSchemeID, item.Scope.CalculationSetID, item.Scope.RouteCaseID, routeSelection });
        }
        if (presets.Count == 0) {
            if (seed is null) return null;
            presets.AddRange(seed.Presets); selected = seed.SelectedId;
        }
        var catalog = NormalizeCatalog(new() { Version = 1, SelectedId = selected!, Presets = presets });
        TractionWorkspaceValidation.Catalog(catalog);
        WriteProjectTrains(scope.InstanceID, 1, catalog);
        // Old route/set libraries have been merged, including divergent presets and selections.
        foreach (var prefix in new[] { "tractionset", "tractionroute" })
            foreach (var suffix in new[] { "presetpoint", "presetbrakingpoint", "presetresistancepoint", "trainpreset" })
                db.ExecuteNonQuery($"DELETE FROM {prefix}{suffix} WHERE InstanceID=@InstanceID", scope);
        return ReadProjectTrains(scope.InstanceID);
    }

    private void WriteProjectTrains(string instanceID, long revision, TractionTrainPresetCatalog catalog)
    {
        foreach (var table in TractionProjectTrainSchema.Tables.Take(4)) db.ExecuteNonQuery($"DELETE FROM {table} WHERE InstanceID=@instanceID", new { instanceID });
        db.ExecuteNonQuery("UPDATE tractionprojecttrains SET Revision=@revision,Version=@Version,SelectedPresetID=@SelectedId WHERE InstanceID=@instanceID", new { instanceID, revision, catalog.Version, catalog.SelectedId });
        InsertProjectRows("tractionprojecttrainpreset", instanceID, catalog.Presets.Select((preset, index) => {
            var row = Data(("PresetID", preset.Id), ("OrderIndex", index), ("Name", preset.Name), ("NameEn", preset.NameEn)); AddTrain(row, preset.Parameters); return row;
        }));
        foreach (var (table, kind) in new[] { ("tractionprojecttrainpoint", 0), ("tractionprojecttrainbrakingpoint", 1), ("tractionprojecttrainresistancepoint", 2) })
            InsertProjectRows(table, instanceID, catalog.Presets.SelectMany(p => (kind == 0 ? p.Parameters.TractionCurve : kind == 1 ? p.Parameters.BrakingCurve! : p.Parameters.ResistanceCurve!).Select((point, index) =>
                Data(("PresetID", p.Id), ("OrderIndex", index), ("SpeedKmh", point.SpeedKmh), ("ForceKn", point.ForceKn)))));
    }
    private void InsertProjectRows(string table, string instanceID, IEnumerable<Dictionary<string, object?>> rows)
    {
        foreach (var chunk in rows.Chunk(250)) {
            var columns = chunk[0].Keys.ToArray(); var parameters = new DynamicParameters(new { instanceID }); var values = new List<string>();
            for (int i = 0; i < chunk.Length; i++) {
                var names = new List<string> { "@instanceID" };
                foreach (var column in columns) { var name = $"p{i}_{column}"; parameters.Add(name, chunk[i][column]); names.Add("@" + name); }
                values.Add("(" + string.Join(",", names) + ")");
            }
            db.ExecuteNonQuery($"INSERT INTO {table}(InstanceID,{string.Join(",", columns)}) VALUES {string.Join(",", values)}", parameters);
        }
    }
}
