using System.Text.Json;
using SwitchYard.Service.Models;

namespace SwitchYard.Service.Services;

public static class TrainProcessSnapshotStore
{
    public const string TableName = "trainprocesssnapshot";
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const string ScopeFilter = "InstanceID = @InstanceID AND StationSchemeID = @StationSchemeID AND OperationPlanID = @OperationPlanID";

    public static void EnsureSchema(DBConnector db)
    {
        db.ExecuteNonQuery(DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName)
            ? @"CREATE TABLE IF NOT EXISTS trainprocesssnapshot (
                InstanceID VARCHAR(50) NOT NULL, StationSchemeID VARCHAR(50) NOT NULL,
                OperationPlanID VARCHAR(50) NOT NULL, TrainID VARCHAR(50) NOT NULL,
                Document LONGTEXT NOT NULL,
                PRIMARY KEY (InstanceID, StationSchemeID, OperationPlanID, TrainID)
                ) ENGINE=InnoDB CHARACTER SET utf8mb4 COLLATE utf8mb4_bin"
            : @"CREATE TABLE IF NOT EXISTS trainprocesssnapshot (
                InstanceID TEXT NOT NULL, StationSchemeID TEXT NOT NULL,
                OperationPlanID TEXT NOT NULL, TrainID TEXT NOT NULL, Document TEXT NOT NULL,
                PRIMARY KEY (InstanceID, StationSchemeID, OperationPlanID, TrainID))");
    }

    public static List<TrainProcessSnapshot> LoadAll(DBConnector db, ProcessScope scope)
    {
        if (!Exists(db)) return new();
        return (db.Query<SnapshotRow>($"SELECT TrainID, Document FROM trainprocesssnapshot WHERE {ScopeFilter} ORDER BY TrainID", scope) ?? new())
            .Select(row => {
                var snapshot = JsonSerializer.Deserialize<TrainProcessSnapshot>(row.Document, JsonOptions)
                    ?? throw new InvalidOperationException("Stored train process snapshot is null.");
                if (snapshot.Process is null || snapshot.ActivityMovementMap is null || snapshot.EventTimes is null || snapshot.SelectedTrackIDs is null)
                    throw new InvalidOperationException("Stored train process snapshot is incomplete.");
                // Scoped plan copy/rename updates SQL columns, not the immutable source JSON.
                // Treat row keys as authoritative execution ownership after materialization.
                snapshot.InstanceID = scope.InstanceID;
                snapshot.StationSchemeID = scope.StationSchemeID;
                snapshot.OperationPlanID = scope.OperationPlanID;
                snapshot.TrainID = row.TrainID;
                snapshot.Process.InstanceID = scope.InstanceID;
                snapshot.Process.StationSchemeID = scope.StationSchemeID;
                snapshot.Process.OperationPlanID = scope.OperationPlanID;
                return snapshot;
            }).ToList();
    }

    public static void Insert(DBConnector db, ProcessScope scope, TrainProcessSnapshot snapshot)
    {
        var inserted = db.ExecuteNonQuery($@"INSERT INTO trainprocesssnapshot
            (InstanceID, StationSchemeID, OperationPlanID, TrainID, Document)
            VALUES (@InstanceID, @StationSchemeID, @OperationPlanID, @TrainID, @Document)",
            new { scope.InstanceID, scope.StationSchemeID, scope.OperationPlanID, snapshot.TrainID,
                Document = JsonSerializer.Serialize(snapshot, JsonOptions) });
        if (inserted != 1) throw new InvalidOperationException("Expected exactly one train process snapshot to be inserted.");
    }

    public static bool HasTrain(DBConnector db, ProcessScope scope, string trainID) => Exists(db) &&
        (db.Query<int>($"SELECT COUNT(1) FROM trainprocesssnapshot WHERE {ScopeFilter} AND TrainID = @TrainID",
            new { scope.InstanceID, scope.StationSchemeID, scope.OperationPlanID, TrainID = trainID })?.FirstOrDefault() ?? 0) > 0;

    public static void DeleteTrain(DBConnector db, ProcessScope scope, string trainID)
    {
        if (Exists(db)) db.ExecuteNonQuery($"DELETE FROM trainprocesssnapshot WHERE {ScopeFilter} AND TrainID = @TrainID",
            new { scope.InstanceID, scope.StationSchemeID, scope.OperationPlanID, TrainID = trainID });
    }

    public static void DeleteScope(DBConnector db, ProcessScope scope)
    {
        if (Exists(db)) db.ExecuteNonQuery($"DELETE FROM trainprocesssnapshot WHERE {ScopeFilter}", scope);
    }

    private static bool Exists(DBConnector db) => (db.Query<int>(DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName)
        ? "SELECT COUNT(1) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = @TableName"
        : "SELECT COUNT(1) FROM sqlite_master WHERE type = 'table' AND name = @TableName", new { TableName })?.FirstOrDefault() ?? 0) > 0;

    private sealed class SnapshotRow
    {
        public string TrainID { get; set; } = "";
        public string Document { get; set; } = "";
    }
}
