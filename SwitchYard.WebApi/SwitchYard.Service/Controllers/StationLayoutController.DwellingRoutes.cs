using Microsoft.AspNetCore.Mvc;
using SwitchYard.Capacity;
using SwitchYard.Service.Utils;

namespace SwitchYard.Service.Controllers
{
    public partial class StationLayoutController
    {
        [HttpPost(Name = "GenerateStationDwellingRoutes")]
        public IActionResult GenerateStationDwellingRoutes([FromBody] StationDwellingRouteGenerateRequest? request)
        {
            try
            {
                var instanceID = request?.InstanceID?.Trim();
                var stationSchemeID = request?.StationSchemeID?.Trim();
                if (string.IsNullOrWhiteSpace(instanceID) || string.IsNullOrWhiteSpace(stationSchemeID))
                    return BadRequest("instanceID and stationSchemeID are required when generating dwelling routes.");

                var db = GetCapacityDbConnector();
                var authResult = ValidateCapacityInstanceOwnershipOrFail(db, instanceID);
                if (authResult != null) return authResult;

                EnsureStationSchemeSchema(db);
                EnsureLinkSchema(db);
                EnsureCellSchema(db);
                EnsureStationRouteSchema(db);
                if (!StationSchemeIDExists(db, instanceID, stationSchemeID))
                    return NotFound("Station scheme not found.");

                var scope = new { instanceID, stationSchemeID };
                var links = db.Query<StationLinkRow>(
                    $@"SELECT * FROM {QuoteIdentifier("link")}
                       WHERE InstanceID = @instanceID AND StationSchemeID = @stationSchemeID ORDER BY ID", scope) ?? new();
                var nodeIDs = (db.Query<StationNodeRow>(
                    $@"SELECT ID FROM {QuoteIdentifier("node")}
                       WHERE InstanceID = @instanceID AND StationSchemeID = @stationSchemeID", scope) ?? new())
                    .Select(node => node.ID).ToHashSet();
                var cells = db.Query<StationCellRow>(
                    $@"SELECT * FROM {QuoteIdentifier("cell")}
                       WHERE InstanceID = @instanceID AND StationSchemeID = @stationSchemeID ORDER BY ID", scope) ?? new();
                var switches = db.Query<StationSwitchRow>(
                    $@"SELECT * FROM {QuoteIdentifier("switch")}
                       WHERE InstanceID = @instanceID AND StationSchemeID = @stationSchemeID ORDER BY ID", scope) ?? new();
                var signals = db.Query<StationSignalRow>(
                    $@"SELECT * FROM {QuoteIdentifier("signal")}
                       WHERE InstanceID = @instanceID AND StationSchemeID = @stationSchemeID ORDER BY ID", scope) ?? new();
                var existingRoutes = LoadStationRoutes(db, instanceID, stationSchemeID);
                var routeIDs = existingRoutes.Select(route => route.ID ?? string.Empty).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var existingLinkIDs = existingRoutes
                    .Where(route => IsDwellingRouteType(route.Type))
                    .Select(route => ParseStationRouteIdList(route.LinkList))
                    .Where(ids => ids.Count == 1)
                    .Select(ids => ids[0]).ToHashSet(StringComparer.Ordinal);
                var createdRoutes = new List<StationRouteRow>();
                var skipped = 0;
                var invalid = 0;

                db.BeginTransaction();
                try
                {
                    foreach (var link in links.Where(link => !string.IsNullOrWhiteSpace(link.Name)))
                    {
                        var linkID = ToInvariantString(link.ID);
                        if (existingLinkIDs.Contains(linkID))
                        {
                            skipped++;
                            continue;
                        }
                        if (link.FromNodeID == link.ToNodeID || !nodeIDs.Contains(link.FromNodeID) || !nodeIDs.Contains(link.ToNodeID))
                        {
                            invalid++;
                            continue;
                        }

                        var startNodeID = ToInvariantString(link.FromNodeID);
                        var endNodeID = ToInvariantString(link.ToNodeID);
                        var endpoints = new[] { startNodeID, endNodeID };
                        // Avoid schema checks/DDL inside the batch transaction.
                        var routeID = _snowflakeIdGenerator.NextIdString();
                        while (!routeIDs.Add(routeID)) routeID = _snowflakeIdGenerator.NextIdString();
                        var route = new StationRouteRow
                        {
                            InstanceID = instanceID,
                            StationSchemeID = stationSchemeID,
                            ID = routeID,
                            Type = StationRouteTypes.Dwelling.ToString(),
                            Description = BuildStationDwellingRouteDescription(link.Name!),
                            StartNodeID = startNodeID,
                            EndNodeID = endNodeID,
                            NodeList = SerializeStationRouteIdList(endpoints),
                            LinkList = SerializeStationRouteIdList(new[] { linkID }),
                            CellList = SerializeStationRouteIdList(cells
                                .Where(cell => ParseStationRouteIdList(cell.LinkIDList).Contains(linkID))
                                .Select(cell => cell.ID ?? string.Empty)),
                            SwitchList = SerializeStationRouteIdList(switches
                                .Where(item => endpoints.Contains(item.BindingNodeID?.Trim()))
                                .Select(item => item.ID ?? string.Empty)),
                            SignalList = SerializeStationRouteIdList(signals
                                .Where(item => endpoints.Contains(item.BindingNodeID?.Trim()))
                                .Select(item => item.ID ?? string.Empty)),
                            InterruptCellList = "[]",
                            AllowanceTags = "[]",
                            ForbiddenTags = "[]"
                        };
                        var inserted = db.ExecuteNonQuery(
                            $@"INSERT INTO {QuoteIdentifier("stationroute")} (
                                   InstanceID, StationSchemeID, ID, {QuoteIdentifier("Type")}, Description,
                                   NodeList, LinkList, SwitchList, CellList, InterruptCellList, SignalList,
                                   AllowanceTags, ForbiddenTags, StartNodeID, EndNodeID)
                               VALUES (
                                   @InstanceID, @StationSchemeID, @ID, @Type, @Description,
                                   @NodeList, @LinkList, @SwitchList, @CellList, @InterruptCellList, @SignalList,
                                   @AllowanceTags, @ForbiddenTags, @StartNodeID, @EndNodeID)", route);
                        if (inserted != 1) throw new InvalidOperationException("Failed to insert a dwelling route.");
                        createdRoutes.Add(route);
                        existingLinkIDs.Add(linkID);
                    }
                    db.Commit();
                }
                catch
                {
                    db.Rollback();
                    throw;
                }

                return Ok(new { created = createdRoutes.Count, skipped, invalid, routes = createdRoutes });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate station dwelling routes.");
                return StatusCode(500, "Failed to generate station dwelling routes.");
            }
        }

        private static string BuildStationDwellingRouteDescription(string linkName)
        {
            var name = linkName.Trim();
            return $"{name}{(name.EndsWith("道", StringComparison.Ordinal) ? string.Empty : "道")}停留进路";
        }

        private static bool IsDwellingRouteType(string? type) =>
            type?.Trim().ToUpperInvariant() is "DWELLING" or "停留" or "停留进路";

        private IActionResult? ValidateStationDwellingRoute(DBConnector db, StationRouteRow route)
        {
            if (!IsDwellingRouteType(route.Type)) return null;
            var linkIDs = ParseStationRouteIdList(route.LinkList);
            if (linkIDs.Count != 1)
                return BadRequest("A dwelling route must contain exactly one named Link.");

            var link = (db.Query<StationLinkRow>(
                $@"SELECT * FROM {QuoteIdentifier("link")}
                   WHERE InstanceID = @InstanceID AND StationSchemeID = @StationSchemeID AND ID = @linkID",
                new { route.InstanceID, route.StationSchemeID, linkID = linkIDs[0] }) ?? new()).FirstOrDefault();
            if (link == null || string.IsNullOrWhiteSpace(link.Name) || link.FromNodeID == link.ToNodeID)
                return BadRequest("A dwelling route must reference a named Link with two distinct endpoints.");

            var from = ToInvariantString(link.FromNodeID);
            var to = ToInvariantString(link.ToNodeID);
            if (!((route.StartNodeID == from && route.EndNodeID == to) || (route.StartNodeID == to && route.EndNodeID == from)))
                return BadRequest("A dwelling route must start and end at its Link endpoints.");

            route.Type = StationRouteTypes.Dwelling.ToString();
            route.NodeList = SerializeStationRouteIdList(new[] { route.StartNodeID!, route.EndNodeID! });
            route.LinkList = SerializeStationRouteIdList(linkIDs);
            return null;
        }
    }
}
