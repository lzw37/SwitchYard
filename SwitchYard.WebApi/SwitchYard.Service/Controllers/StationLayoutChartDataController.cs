using Microsoft.AspNetCore.Mvc;
using SwitchYard.Capacity;
using System.Text.Json.Nodes;

namespace SwitchYard.Service.Controllers;

public partial class StationLayoutController
{
    [HttpPost(Name = "GetOperationPlanChartResources")]
    public IActionResult GetOperationPlanChartResources([FromBody] ChartResourcesRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.InstanceID) || string.IsNullOrWhiteSpace(request.StationSchemeID)
            || request.Pairs is null || request.Pairs.Count > 10000 || request.Pairs.Any(pair => pair is null || string.IsNullOrWhiteSpace(pair.RouteID)))
            return BadRequest("A station scope and at most 10000 route/type pairs are required.");
        var db = GetCapacityDbConnector();
        var auth = ValidateCapacityInstanceOwnershipOrFail(db, request.InstanceID);
        if (auth is not null) return auth;
        try
        {
            db.BeginTransaction();
            const string filter = "InstanceID=@InstanceID AND StationSchemeID=@StationSchemeID";
            var cells = db.Query<StationCellRow>($"SELECT ID,Name,LinkIDList FROM cell WHERE {filter}", request) ?? [];
            var nodes = (db.Query<ChartNodeRow>($"SELECT ID,ExtraProperties FROM node WHERE {filter}", request) ?? []).Select(row =>
            {
                var extra = string.IsNullOrWhiteSpace(row.ExtraProperties) ? null : JsonNode.Parse(row.ExtraProperties);
                return new { row.ID, Name = (extra?["name"] ?? extra?["Name"] ?? extra?["description"] ?? extra?["Description"])?.GetValue<string>() ?? "" };
            }).ToList();
            var tracks = (db.Query<StationLinkRow>($"SELECT ID,Name,FromNodeID,ToNodeID FROM link WHERE {filter}", request) ?? [])
                .Select(row => new { row.ID, row.Name, row.FromNodeID, row.ToNodeID }).ToList();
            var pairs = request.Pairs.DistinctBy(pair => (pair.RouteID, pair.TrainTypeID ?? "")).ToList();
            var routeIDs = pairs.Select(pair => pair.RouteID).Distinct(StringComparer.Ordinal).ToArray();
            var parameters = new { request.InstanceID, request.StationSchemeID, routeIDs };
            var routes = routeIDs.Length == 0 ? [] : db.Query<StationRouteRow>($"SELECT ID,CellList,InterruptCellList FROM stationroute WHERE {filter} AND ID IN @routeIDs", parameters) ?? [];
            var times = routeIDs.Length == 0 ? [] : db.Query<StationRouteTimeRow>($"SELECT RouteID,TrainTypeID,CellID,StartOccupationShift,EndOccupationShift FROM stationroutetime WHERE {filter} AND RouteID IN @routeIDs", parameters) ?? [];
            var routesById = routes.ToDictionary(route => route.ID!, StringComparer.Ordinal);
            var timesByPair = times.ToLookup(row => (row.RouteID, row.TrainTypeID ?? ""));
            var routeTimes = pairs.Select(pair =>
            {
                var route = routesById.GetValueOrDefault(pair.RouteID);
                var rows = timesByPair[(pair.RouteID, pair.TrainTypeID ?? "")].ToList();
                return new { pair.RouteID, TrainTypeID = pair.TrainTypeID ?? "", Rows = OrderStationRouteTimesByCellList(rows, route?.CellList, route?.InterruptCellList, rows.Count > 0) };
            }).ToList();
            return Ok(new { cells = cells.Select(row => new { row.ID, row.Name, row.LinkIDList }), nodes, tracks, routeTimes });
        }
        catch (Exception ex) { _logger.LogError(ex, "Failed to load chart resources."); return StatusCode(500, "Failed to load chart resources."); }
        finally { db.Rollback(); }
    }
    public sealed class ChartResourcesRequest
    {
        public string InstanceID { get; set; } = "";
        public string StationSchemeID { get; set; } = "";
        public List<ChartRoutePair> Pairs { get; set; } = [];
    }
    public sealed class ChartRoutePair { public string RouteID { get; set; } = ""; public string? TrainTypeID { get; set; } }
    private sealed class ChartNodeRow { public string ID { get; set; } = ""; public string? ExtraProperties { get; set; } }
}
