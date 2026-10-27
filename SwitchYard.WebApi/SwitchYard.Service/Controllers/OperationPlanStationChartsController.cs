using Microsoft.AspNetCore.Mvc;
using SwitchYard.Capacity;
using SwitchYard.Service.Models;
using System.Text.Json;

namespace SwitchYard.Service.Controllers;

public partial class OperationPlanController
{
    private const string StationPlanChartSettingsTable = "stationplanchartsettings";

    [HttpGet(Name = "GetStationPlanCharts")]
    public IActionResult GetStationPlanCharts(string? instanceID, string? stationSchemeID, string? operationPlanID)
    {
        try
        {
            var scope = NormalizeOperationPlanScope(instanceID, stationSchemeID, operationPlanID, requireOperationPlanID: true);
            if (scope.ErrorResult is not null) return scope.ErrorResult;
            var db = GetCapacityDbConnector();
            var auth = ValidateCapacityInstanceOwnershipOrFail(db, scope.InstanceID!);
            if (auth is not null) return auth;

            EnsureDefaultOperationPlan(db, scope.InstanceID!, scope.StationSchemeID!);
            if (!OperationPlanExists(db, scope.InstanceID!, scope.StationSchemeID!, scope.OperationPlanID!)) return NotFound("Operation plan not found.");

            var settings = LoadStationPlanCharts(db, scope.InstanceID!, scope.StationSchemeID!, scope.OperationPlanID!);
            if (!settings.IsConfigured)
            {

                settings.LegacySettings = LoadStationPlanViewSettings(db, scope.InstanceID!, scope.StationSchemeID!, scope.OperationPlanID!);
            }
            return Ok(settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load station plan charts.");
            return StatusCode(500, "Failed to load station plan charts.");
        }
    }

    [HttpPut(Name = "SaveStationPlanCharts")]
    public IActionResult SaveStationPlanCharts([FromBody] StationPlanChartSettingsRequest? request)
    {
        DBConnector? db = null;
        try
        {
            var scope = NormalizeOperationPlanScope(request?.InstanceID, request?.StationSchemeID, request?.OperationPlanID, requireOperationPlanID: true);
            if (scope.ErrorResult is not null) return scope.ErrorResult;
            List<StationPlanChart> charts;
            try { charts = NormalizeStationPlanCharts(request?.Charts); }
            catch (ArgumentException ex) { return BadRequest(ex.Message); }
            db = GetCapacityDbConnector();
            var auth = ValidateCapacityInstanceOwnershipOrFail(db, scope.InstanceID!);
            if (auth is not null) return auth;

            EnsureDefaultOperationPlan(db, scope.InstanceID!, scope.StationSchemeID!);
            if (!OperationPlanExists(db, scope.InstanceID!, scope.StationSchemeID!, scope.OperationPlanID!)) return NotFound("Operation plan not found.");

            var parameters = new
            {
                instanceID = scope.InstanceID, stationSchemeID = scope.StationSchemeID, operationPlanID = scope.OperationPlanID,
                chartsJson = JsonSerializer.Serialize(charts)
            };
            var upsert = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName)
                ? "ON DUPLICATE KEY UPDATE ChartsJson=VALUES(ChartsJson)"
                : "ON CONFLICT(InstanceID,StationSchemeID,OperationPlanID) DO UPDATE SET ChartsJson=excluded.ChartsJson";
            db.ExecuteNonQuery($@"INSERT INTO {QuoteIdentifier(StationPlanChartSettingsTable)}
                (InstanceID,StationSchemeID,OperationPlanID,ChartsJson)
                VALUES (@instanceID,@stationSchemeID,@operationPlanID,@chartsJson) {upsert}", parameters);
            return Ok(new StationPlanChartSettings { IsConfigured = true, Charts = charts });
        }
        catch (Exception ex)
        {
            db?.Rollback();
            _logger.LogError(ex, "Failed to save station plan charts.");
            return StatusCode(500, "Failed to save station plan charts.");
        }
    }

    private static List<StationPlanChart> NormalizeStationPlanCharts(List<StationPlanChart>? source)
    {
        if (source is null || source.Count is < 1 or > 100)
            throw new ArgumentException("Charts must be an array containing 1 to 100 charts.");
        var charts = new List<StationPlanChart>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var chart in source)
        {
            if (chart is null || string.IsNullOrWhiteSpace(chart.ChartID) || chart.ChartID.Trim().Length > 50)
                throw new ArgumentException("Chart IDs must contain 1 to 50 characters.");
            if (string.IsNullOrWhiteSpace(chart.ChartName) || chart.ChartName.Trim().Length > 100)
                throw new ArgumentException("Chart names must contain 1 to 100 characters.");
            var endpoints = NormalizeStationPlanAxisIDs(chart.EndpointNodeIDs);
            var chartID = chart.ChartID.Trim();
            if (!seen.Add(chartID)) continue;
            charts.Add(new StationPlanChart { ChartID = chartID, ChartName = chart.ChartName.Trim(), EndpointNodeIDs = endpoints });
        }
        return charts;
    }

    private static StationPlanChartSettings LoadStationPlanCharts(DBConnector db, string instanceID, string stationSchemeID, string operationPlanID)
    {
        var row = db.Query<StationPlanChartSettingsRow>($@"SELECT ChartsJson FROM {QuoteIdentifier(StationPlanChartSettingsTable)}
            WHERE InstanceID=@instanceID AND StationSchemeID=@stationSchemeID AND OperationPlanID=@operationPlanID",
            new { instanceID, stationSchemeID, operationPlanID })?.SingleOrDefault();
        return row is null ? new() : new()
        {
            IsConfigured = true,
            Charts = NormalizeStationPlanCharts(JsonSerializer.Deserialize<List<StationPlanChart>>(row.ChartsJson))
        };
    }

    private static void EnsureStationPlanChartSettingsSchema(DBConnector db)
    {
        var mysql = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName);
        var idType = mysql ? "VARCHAR(50)" : "TEXT";
        var jsonType = mysql ? "LONGTEXT" : "TEXT";
        db.ExecuteNonQuery($@"CREATE TABLE IF NOT EXISTS {QuoteIdentifier(StationPlanChartSettingsTable)} (
            InstanceID {idType} NOT NULL, StationSchemeID {idType} NOT NULL, OperationPlanID {idType} NOT NULL,
            ChartsJson {jsonType} NOT NULL,
            PRIMARY KEY (InstanceID,StationSchemeID,OperationPlanID)) {(mysql ? "ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci" : "")}");
    }

    private sealed class StationPlanChartSettingsRow
    {
        public string ChartsJson { get; set; } = "[]";
    }
}
