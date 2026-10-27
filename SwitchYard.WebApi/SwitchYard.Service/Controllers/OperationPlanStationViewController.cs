using Microsoft.AspNetCore.Mvc;
using SwitchYard.Capacity;
using SwitchYard.Service.Models;
using System.Text.Json;

namespace SwitchYard.Service.Controllers;

public partial class OperationPlanController
{
    private const string StationPlanViewSettingsTable = "stationplanviewsettings";

    [HttpGet(Name = "GetStationPlanViewSettings")]
    public IActionResult GetStationPlanViewSettings(string? instanceID, string? stationSchemeID, string? operationPlanID)
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

            return Ok(LoadStationPlanViewSettings(db, scope.InstanceID!, scope.StationSchemeID!, scope.OperationPlanID!));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load station plan view settings.");
            return StatusCode(500, "Failed to load station plan view settings.");
        }
    }

    [HttpPut(Name = "SaveStationPlanViewSettings")]
    public IActionResult SaveStationPlanViewSettings([FromBody] StationPlanViewSettingsRequest? request)
    {
        DBConnector? db = null;
        try
        {
            var scope = NormalizeOperationPlanScope(request?.InstanceID, request?.StationSchemeID, request?.OperationPlanID, requireOperationPlanID: true);
            if (scope.ErrorResult is not null) return scope.ErrorResult;
            List<string> cells, endpoints;
            try
            {
                cells = NormalizeStationPlanAxisIDs(request?.CellIDs);
                endpoints = NormalizeStationPlanAxisIDs(request?.EndpointNodeIDs);
            }
            catch (ArgumentException ex) { return BadRequest(ex.Message); }
            db = GetCapacityDbConnector();
            var auth = ValidateCapacityInstanceOwnershipOrFail(db, scope.InstanceID!);
            if (auth is not null) return auth;

            EnsureDefaultOperationPlan(db, scope.InstanceID!, scope.StationSchemeID!);
            if (!OperationPlanExists(db, scope.InstanceID!, scope.StationSchemeID!, scope.OperationPlanID!)) return NotFound("Operation plan not found.");

            var parameters = new
            {
                instanceID = scope.InstanceID, stationSchemeID = scope.StationSchemeID, operationPlanID = scope.OperationPlanID,
                cellIDsJson = JsonSerializer.Serialize(cells), endpointNodeIDsJson = JsonSerializer.Serialize(endpoints)
            };
            var upsert = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName)
                ? "ON DUPLICATE KEY UPDATE CellIDsJson=VALUES(CellIDsJson), EndpointNodeIDsJson=VALUES(EndpointNodeIDsJson)"
                : "ON CONFLICT(InstanceID,StationSchemeID,OperationPlanID) DO UPDATE SET CellIDsJson=excluded.CellIDsJson, EndpointNodeIDsJson=excluded.EndpointNodeIDsJson";
            db.ExecuteNonQuery($@"INSERT INTO {QuoteIdentifier(StationPlanViewSettingsTable)}
                (InstanceID,StationSchemeID,OperationPlanID,CellIDsJson,EndpointNodeIDsJson)
                VALUES (@instanceID,@stationSchemeID,@operationPlanID,@cellIDsJson,@endpointNodeIDsJson) {upsert}", parameters);
            return Ok(new StationPlanViewSettings { IsConfigured = true, CellIDs = cells, EndpointNodeIDs = endpoints });
        }
        catch (Exception ex)
        {
            db?.Rollback();
            _logger.LogError(ex, "Failed to save station plan view settings.");
            return StatusCode(500, "Failed to save station plan view settings.");
        }
    }

    private static List<string> NormalizeStationPlanAxisIDs(List<string>? source)
    {
        if (source is null || source.Count > 2000) throw new ArgumentException("Axis selections must be arrays with at most 2000 IDs.");
        if (source.Any(id => string.IsNullOrWhiteSpace(id) || id.Trim().Length > 50)) throw new ArgumentException("Invalid axis element ID.");
        return source.Select(id => id.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static StationPlanViewSettings LoadStationPlanViewSettings(DBConnector db, string instanceID, string stationSchemeID, string operationPlanID)
    {
        var row = db.Query<StationPlanViewSettingsRow>($@"SELECT CellIDsJson,EndpointNodeIDsJson FROM {QuoteIdentifier(StationPlanViewSettingsTable)}
            WHERE InstanceID=@instanceID AND StationSchemeID=@stationSchemeID AND OperationPlanID=@operationPlanID",
            new { instanceID, stationSchemeID, operationPlanID })?.SingleOrDefault();
        return row is null ? new() : new()
        {
            IsConfigured = true,
            CellIDs = JsonSerializer.Deserialize<List<string>>(row.CellIDsJson) ?? new(),
            EndpointNodeIDs = JsonSerializer.Deserialize<List<string>>(row.EndpointNodeIDsJson) ?? new()
        };
    }

    private static void EnsureStationPlanViewSettingsSchema(DBConnector db)
    {
        var mysql = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName);
        var idType = mysql ? "VARCHAR(50)" : "TEXT";
        var jsonType = mysql ? "LONGTEXT" : "TEXT";
        db.ExecuteNonQuery($@"CREATE TABLE IF NOT EXISTS {QuoteIdentifier(StationPlanViewSettingsTable)} (
            InstanceID {idType} NOT NULL, StationSchemeID {idType} NOT NULL, OperationPlanID {idType} NOT NULL,
            CellIDsJson {jsonType} NOT NULL, EndpointNodeIDsJson {jsonType} NOT NULL,
            PRIMARY KEY (InstanceID,StationSchemeID,OperationPlanID)) {(mysql ? "ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci" : "")}");
    }

    private sealed class StationPlanViewSettingsRow
    {
        public string CellIDsJson { get; set; } = "[]";
        public string EndpointNodeIDsJson { get; set; } = "[]";
    }
}
