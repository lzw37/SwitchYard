using SwitchYard.Service.Models;
using System.Text.Json;

namespace SwitchYard.Service.Services;

/// <summary>Scoped cleanup shared by legacy controllers and the station-layout adapter.
/// Callers own the transaction. Optional tables are never created during cleanup.</summary>
public static class CapacityDataLifecycle
{
    public static readonly IReadOnlyList<string> PlanTables = new[]
    {
        "trainprocesssnapshot", "operationthroughputsummaryroute", "operationthroughputsummaryresult",
        "operationbottleneckanalysisresult", "operationoccupationtimesubtable", "stationplanviewsettings",
        "operationoccupationtimecell", "operationoccupationtimerow", "operationanalysiscell",
        "operationanalysismeta", "operationbottlenecksummarycategoryroute", "operationbottlenecksummarycategory",
        "movement", "train"
    };

    private static readonly string[] AnalysisTables =
    {
        "operationthroughputsummaryroute", "operationthroughputsummaryresult", "operationbottleneckanalysisresult",
        "operationoccupationtimecell", "operationoccupationtimerow", "operationanalysiscell", "operationanalysismeta"
    };

    public static readonly IReadOnlyList<string> SchemeTables = PlanTables.Concat(new[]
    {
        "operationplan", "operationprocesstemplate", "movementtemplate", "traintemplate",
        "stationroutetime", "stationroute", "stationrouteend", "switchbranchvector", "switch",
        "bufferstop", "insulationjoint", "signal", "platform", "cell", "annotation", "curve", "link", "node",
        "stationlayoutrevision"
    }).ToArray();

    public static void DeleteInstanceChildren(DBConnector db, string instanceID) =>
        DeleteRows(db, SchemeTables.Concat(new[] { "stationscheme" }), instanceID);

    public static void DeleteSchemeChildren(DBConnector db, string instanceID, string stationSchemeID) =>
        DeleteRows(db, SchemeTables, instanceID, stationSchemeID);

    public static void DeletePlanChildren(DBConnector db, string instanceID, string stationSchemeID, string operationPlanID) =>
        DeleteRows(db, PlanTables, instanceID, stationSchemeID, operationPlanID);

    public static void VerifyInstanceDeleted(DBConnector db, string instanceID) =>
        DeleteRows(db, SchemeTables.Concat(new[] { "stationscheme" }), instanceID, verifyOnly: true);

    public static void VerifySchemeDeleted(DBConnector db, string instanceID, string stationSchemeID) =>
        DeleteRows(db, SchemeTables, instanceID, stationSchemeID, verifyOnly: true);

    public static void VerifyPlanDeleted(DBConnector db, string instanceID, string stationSchemeID, string operationPlanID) =>
        DeleteRows(db, PlanTables, instanceID, stationSchemeID, operationPlanID, verifyOnly: true);

    public static void InvalidateAnalysis(DBConnector db, string instanceID, string stationSchemeID, string? operationPlanID = null) =>
        DeleteRows(db, AnalysisTables, instanceID, stationSchemeID, operationPlanID);

    public static void DeleteRouteParameters(DBConnector db, string instanceID, string stationSchemeID, string routeID)
    {
        var parameters = new { instanceID, stationSchemeID, routeID };
        foreach (var table in new[] { "stationroutetime", "operationbottlenecksummarycategoryroute" })
        {
            if (!TableExists(db, table)) continue;
            DeleteAndVerify(db, table, "InstanceID=@instanceID AND StationSchemeID=@stationSchemeID AND RouteID=@routeID", parameters);
        }
        InvalidateAnalysis(db, instanceID, stationSchemeID);
    }

    /// <summary>References in live plans, scheme templates and immutable execution constraints
    /// must be removed by their editor before deleting the route.</summary>
    public static bool RouteIsReferenced(DBConnector db, string instanceID, string stationSchemeID, string routeID)
    {
        var scope = new { instanceID, stationSchemeID };
        const string filter = "InstanceID=@instanceID AND StationSchemeID=@stationSchemeID";
        var lockSuffix = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName) ? " FOR UPDATE" : "";
        foreach (var table in new[] { "movement", "movementtemplate" })
        {
            if (!TableExists(db, table)) continue;
            var rows = db.Query<RouteReferenceRow>($"SELECT * FROM {Q(table)} WHERE {filter}{lockSuffix}", scope) ?? new();
            if (rows.Any(row => Same(row.Route, routeID) || ContainsRoute(row.RouteIDList, routeID))) return true;
        }
        foreach (var table in new[] { "operationprocesstemplate", "trainprocesssnapshot" })
        {
            if (!TableExists(db, table)) continue;
            var rows = db.Query<DocumentRow>($"SELECT Document FROM {Q(table)} WHERE {filter}{lockSuffix}", scope) ?? new();
            foreach (var row in rows)
            {
                var process = table == "operationprocesstemplate"
                    ? JsonSerializer.Deserialize<OperationProcessTemplate>(row.Document, TrainProcessSnapshotStore.JsonOptions)
                    : JsonSerializer.Deserialize<TrainProcessSnapshot>(row.Document, TrainProcessSnapshotStore.JsonOptions)?.Process;
                if (process is null) throw new InvalidDataException("Stored process document is incomplete.");
                if (process.Activities.Any(a => a.RouteList.Any(id => Same(id, routeID)) || Same(a.SelectedRoute, routeID)) ||
                    process.RouteAnchors.Any(a => Same(a.RouteID, routeID))) return true;
            }
        }
        return false;
    }

    public static bool TableExists(DBConnector db, string table) => (db.Query<int>(
        DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName)
            ? "SELECT COUNT(1) FROM information_schema.tables WHERE table_schema=DATABASE() AND table_name=@table"
            : "SELECT COUNT(1) FROM sqlite_master WHERE type='table' AND name=@table", new { table })?.FirstOrDefault() ?? 0) > 0;

    private static void DeleteRows(DBConnector db, IEnumerable<string> tables, string instanceID,
        string? stationSchemeID = null, string? operationPlanID = null, bool verifyOnly = false)
    {
        var filter = "InstanceID=@instanceID";
        if (stationSchemeID is not null) filter += " AND StationSchemeID=@stationSchemeID";
        if (operationPlanID is not null) filter += " AND OperationPlanID=@operationPlanID";
        foreach (var table in tables)
        {
            if (!TableExists(db, table)) continue;
            var parameters = new { instanceID, stationSchemeID, operationPlanID };
            if (!verifyOnly) db.ExecuteNonQuery($"DELETE FROM {Q(table)} WHERE {filter}", parameters);
            VerifyEmpty(db, table, filter, parameters);
        }
    }

    private static void DeleteAndVerify(DBConnector db, string table, string filter, object parameters)
    {
        db.ExecuteNonQuery($"DELETE FROM {Q(table)} WHERE {filter}", parameters);
        VerifyEmpty(db, table, filter, parameters);
    }

    private static void VerifyEmpty(DBConnector db, string table, string filter, object parameters)
    {
        if ((db.Query<long>($"SELECT COUNT(1) FROM {Q(table)} WHERE {filter}", parameters)?.FirstOrDefault() ?? 0) != 0)
            throw new InvalidOperationException($"Deletion left scoped rows in {table}.");
    }

    private static bool Same(string? a, string b) => string.Equals(a?.Trim(), b, StringComparison.OrdinalIgnoreCase);
    private static bool ContainsRoute(string? list, string routeID) => (list ?? "")
        .Split(new[] { ',', ';', '，', '；', '\r', '\n', '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries)
        .Any(id => Same(id, routeID));
    private static string Q(string table) => DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName) ? $"`{table}`" : $"\"{table}\"";
    private sealed class RouteReferenceRow { public string? Route { get; set; } public string? RouteIDList { get; set; } }
    private sealed class DocumentRow { public string Document { get; set; } = ""; }
}
