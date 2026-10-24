using Dapper;
using SwitchYard.Service.Models;

namespace SwitchYard.Service.Services;

/// <summary>Materialize legacy generators as ordered relational rows, without replacing existing curves or results.</summary>
public static class TractionCharacteristicMigration
{
    public static void Ensure(DBConnector db, string prefix)
    {
        if (prefix is not ("tractionset" or "tractionroute")) throw new ArgumentException("Invalid table scope.");
        var scope = "InstanceID,StationSchemeID,CalculationSetID" + (prefix == "tractionroute" ? ",RouteCaseID" : "");
        db.BeginTransaction();
        try {
            foreach (var preset in new[] { true, false }) {
                var parent = prefix + (preset ? "trainpreset" : "calculation");
                var keys = scope + (preset ? ",PresetID" : "");
                var fields = keys.Split(',');
                var scalar = preset ? "" : "Train";
                foreach (var braking in new[] { true, false }) {
                    var table = prefix + (preset ? "preset" : "calculation") + (braking ? "brakingpoint" : "resistancepoint");
                    var join = string.Join(" AND ", fields.Select(f => "c." + f + "=p." + f));
                    var rows = db.Query<LegacyTrainRow>($"SELECT p.* FROM {parent} p WHERE NOT EXISTS (SELECT 1 FROM {table} c WHERE {join})") ?? [];
                    foreach (var row in rows) {
                        var train = new SpeedProfileTrainInput { MaxSpeedKmh = preset ? row.MaxSpeedKmh : row.TrainMaxSpeedKmh, MaxBrakeForceKn = preset ? row.MaxBrakeForceKn : row.TrainMaxBrakeForceKn,
                            ResistanceA = preset ? row.ResistanceA : row.TrainResistanceA, ResistanceB = preset ? row.ResistanceB : row.TrainResistanceB, ResistanceC = preset ? row.ResistanceC : row.TrainResistanceC };
                        var points = TrainCharacteristicTables.Generate(train, braking);
                        var parameters = new DynamicParameters(row); var values = new List<string>();
                        for (int i = 0; i < points.Count; i++) {
                            parameters.Add("speed" + i, points[i].SpeedKmh); parameters.Add("force" + i, points[i].ForceKn);
                            values.Add("(" + string.Join(",", fields.Select(f => "@" + f)) + $",{i},@speed{i},@force{i})");
                        }
                        db.ExecuteNonQuery($"INSERT INTO {table}({keys},OrderIndex,SpeedKmh,ForceKn) VALUES " + string.Join(",", values), parameters);
                    }
                }
            }
            db.Commit();
        } catch { db.Rollback(); throw; }
    }
    private sealed class LegacyTrainRow {
        public string InstanceID { get; set; } = ""; public string StationSchemeID { get; set; } = ""; public string CalculationSetID { get; set; } = ""; public string RouteCaseID { get; set; } = ""; public string PresetID { get; set; } = "";
        public double MaxSpeedKmh { get; set; } public double MaxBrakeForceKn { get; set; } public double ResistanceA { get; set; } public double ResistanceB { get; set; } public double ResistanceC { get; set; }
        public double TrainMaxSpeedKmh { get; set; } public double TrainMaxBrakeForceKn { get; set; } public double TrainResistanceA { get; set; } public double TrainResistanceB { get; set; } public double TrainResistanceC { get; set; }
    }
}
