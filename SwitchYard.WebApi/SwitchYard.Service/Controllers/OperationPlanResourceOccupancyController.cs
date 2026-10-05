using Microsoft.AspNetCore.Mvc;
using SwitchYard.Capacity;
using SwitchYard.Service.Models;
using System.Text.Json;

namespace SwitchYard.Service.Controllers;

public partial class OperationPlanController
{
    private const string ResourceOccupancyChartSettingsTable = "resourceoccupancychartsettings";

    [HttpGet(Name = "GetResourceOccupancyCharts")]
    public IActionResult GetResourceOccupancyCharts(string? instanceID, string? stationSchemeID, string? operationPlanID)
    {
        try
        {
            var scope = NormalizeOperationPlanScope(instanceID, stationSchemeID, operationPlanID, requireOperationPlanID: true);
            if (scope.ErrorResult is not null) return scope.ErrorResult;
            var db = GetCapacityDbConnector();
            var auth = ValidateCapacityInstanceOwnershipOrFail(db, scope.InstanceID!);
            if (auth is not null) return auth;
            EnsureOperationPlanObjectSchema(db);
            EnsureDefaultOperationPlan(db, scope.InstanceID!, scope.StationSchemeID!);
            if (!OperationPlanExists(db, scope.InstanceID!, scope.StationSchemeID!, scope.OperationPlanID!)) return NotFound("Operation plan not found.");
            EnsureResourceOccupancyChartSettingsSchema(db);
            return Ok(LoadResourceOccupancyCharts(db, scope.InstanceID!, scope.StationSchemeID!, scope.OperationPlanID!));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load resource occupancy charts.");
            return StatusCode(500, "Failed to load resource occupancy charts.");
        }
    }

    [HttpPut(Name = "SaveResourceOccupancyCharts")]
    public IActionResult SaveResourceOccupancyCharts([FromBody] ResourceOccupancyChartSettingsRequest? request)
    {
        DBConnector? db = null;
        try
        {
            var scope = NormalizeOperationPlanScope(request?.InstanceID, request?.StationSchemeID, request?.OperationPlanID, requireOperationPlanID: true);
            if (scope.ErrorResult is not null) return scope.ErrorResult;
            List<ResourceOccupancyChart> charts;
            try { charts = NormalizeResourceOccupancyCharts(request?.Charts); }
            catch (ArgumentException ex) { return BadRequest(ex.Message); }
            db = GetCapacityDbConnector();
            var auth = ValidateCapacityInstanceOwnershipOrFail(db, scope.InstanceID!);
            if (auth is not null) return auth;
            EnsureOperationPlanObjectSchema(db);
            EnsureDefaultOperationPlan(db, scope.InstanceID!, scope.StationSchemeID!);
            if (!OperationPlanExists(db, scope.InstanceID!, scope.StationSchemeID!, scope.OperationPlanID!)) return NotFound("Operation plan not found.");
            EnsureResourceOccupancyChartSettingsSchema(db);
            var parameters = new
            {
                instanceID = scope.InstanceID, stationSchemeID = scope.StationSchemeID, operationPlanID = scope.OperationPlanID,
                chartsJson = JsonSerializer.Serialize(charts)
            };
            db.BeginTransaction();
            db.ExecuteNonQuery($@"DELETE FROM {QuoteIdentifier(ResourceOccupancyChartSettingsTable)}
                WHERE InstanceID=@instanceID AND StationSchemeID=@stationSchemeID AND OperationPlanID=@operationPlanID", parameters);
            var inserted = db.ExecuteNonQuery($@"INSERT INTO {QuoteIdentifier(ResourceOccupancyChartSettingsTable)}
                (InstanceID,StationSchemeID,OperationPlanID,ChartsJson)
                VALUES (@instanceID,@stationSchemeID,@operationPlanID,@chartsJson)", parameters);
            if (inserted != 1) throw new InvalidOperationException("Resource occupancy charts were not saved.");
            db.Commit();
            return Ok(new ResourceOccupancyChartSettings { IsConfigured = true, Charts = charts });
        }
        catch (Exception ex)
        {
            db?.Rollback();
            _logger.LogError(ex, "Failed to save resource occupancy charts.");
            return StatusCode(500, "Failed to save resource occupancy charts.");
        }
    }

    private static List<ResourceOccupancyChart> NormalizeResourceOccupancyCharts(List<ResourceOccupancyChart>? source)
    {
        if (source is null || source.Count is < 1 or > 100)
            throw new ArgumentException("Charts must be an array containing 1 to 100 charts.");
        var charts = new List<ResourceOccupancyChart>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var chart in source)
        {
            if (chart is null || string.IsNullOrWhiteSpace(chart.ChartID) || chart.ChartID.Trim().Length > 50)
                throw new ArgumentException("Chart IDs must contain 1 to 50 characters.");
            if (string.IsNullOrWhiteSpace(chart.ChartName) || chart.ChartName.Trim().Length > 100)
                throw new ArgumentException("Chart names must contain 1 to 100 characters.");
            var cells = NormalizeStationPlanAxisIDs(chart.CellIDs);
            var chartID = chart.ChartID.Trim();
            if (!seen.Add(chartID)) continue;
            charts.Add(new ResourceOccupancyChart { ChartID = chartID, ChartName = chart.ChartName.Trim(), CellIDs = cells });
        }
        return charts;
    }

    private static ResourceOccupancyChartSettings LoadResourceOccupancyCharts(DBConnector db, string instanceID, string stationSchemeID, string operationPlanID)
    {
        var row = db.Query<ResourceOccupancyChartSettingsRow>($@"SELECT ChartsJson FROM {QuoteIdentifier(ResourceOccupancyChartSettingsTable)}
            WHERE InstanceID=@instanceID AND StationSchemeID=@stationSchemeID AND OperationPlanID=@operationPlanID",
            new { instanceID, stationSchemeID, operationPlanID })?.SingleOrDefault();
        return row is null ? new() : new()
        {
            IsConfigured = true,
            Charts = NormalizeResourceOccupancyCharts(JsonSerializer.Deserialize<List<ResourceOccupancyChart>>(row.ChartsJson))
        };
    }

    private static void EnsureResourceOccupancyChartSettingsSchema(DBConnector db)
    {
        var mysql = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName);
        var idType = mysql ? "VARCHAR(50)" : "TEXT";
        var jsonType = mysql ? "LONGTEXT" : "TEXT";
        db.ExecuteNonQuery($@"CREATE TABLE IF NOT EXISTS {QuoteIdentifier(ResourceOccupancyChartSettingsTable)} (
            InstanceID {idType} NOT NULL, StationSchemeID {idType} NOT NULL, OperationPlanID {idType} NOT NULL,
            ChartsJson {jsonType} NOT NULL,
            PRIMARY KEY (InstanceID,StationSchemeID,OperationPlanID)) {(mysql ? "ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci" : "")}");
    }

    private sealed class ResourceOccupancyChartSettingsRow
    {
        public string ChartsJson { get; set; } = "[]";
    }
}
