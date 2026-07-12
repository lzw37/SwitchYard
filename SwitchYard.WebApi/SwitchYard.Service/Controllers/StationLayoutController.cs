using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ACadSharp.IO;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using SwitchYard.Capacity;
using SwitchYard.Service.Utils;

namespace SwitchYard.Service.Controllers
{
    [ApiController]
    [Route("[controller]/[action]")]
    [Authorize]
    public class StationLayoutController : ControllerBase
    {
        private readonly ILogger<StationLayoutController> _logger;
        private readonly IWebHostEnvironment _environment;
        private readonly SnowflakeIdGenerator _snowflakeIdGenerator;

        private const long MaxDwgFileSize = 20L * 1024 * 1024; // 20 MB
        private const string DefaultStationSchemeID = "station_layout_scheme";
        private const string DefaultStationSchemeName = "车站布置图";
        private const string DefaultStationSchemeGridSettingsJson = "{\"showGrid\":true,\"spacing\":20,\"originX\":0,\"originY\":0}";
        private const string MissingStationRouteEndTagPlaceholder = "【】";
        private static readonly string[] NamedDeviceTables = new[] { "signal", "switch", "cell", "route", "platform" };
        private static readonly string[] StationLayoutDataTables = new[]
        {
            "cell",
            "stationroute",
            "stationrouteend",
            "switchbranchvector",
            "switch",
            "bufferstop",
            "insulationjoint",
            "signal",
            "platform",
            "annotation",
            "curve",
            "link",
            "node"
        };
        private static readonly Regex LayerNameRegex = new("^[A-Za-z0-9_\\-]{1,255}$", RegexOptions.Compiled);
        private static readonly JsonSerializerOptions StationLayoutJsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public StationLayoutController(
            ILogger<StationLayoutController> logger,
            IWebHostEnvironment environment,
            SnowflakeIdGenerator snowflakeIdGenerator)
        {
            _logger = logger;
            _environment = environment;
            _snowflakeIdGenerator = snowflakeIdGenerator;
        }
        
        [HttpPost(Name ="SaveJson")]
        [Authorize(Roles = "Admin")]
        public IActionResult SaveJson(
            [FromBody] StationLayoutSaveRequest? request,
            [FromQuery] string? instanceID = null,
            [FromQuery] string? stationSchemeID = null)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Json))
            {
                return BadRequest("Request body must contain a non-empty json field.");
            }

            try
            {
                var layout = JsonSerializer.Deserialize<StationLayoutJson>(
                    request.Json,
                    StationLayoutJsonOptions) ?? new StationLayoutJson();
                var normalizedInstanceID = FirstNonEmpty(instanceID, request.InstanceID, layout.Metadata?.InstanceID);
                if (string.IsNullOrWhiteSpace(normalizedInstanceID))
                {
                    return BadRequest("instanceID is required when saving station layout data.");
                }

                var dbConnector = GetCapacityDbConnector();
                var authResult = ValidateCapacityInstanceOwnershipOrFail(dbConnector, normalizedInstanceID);
                if (authResult != null)
                {
                    return authResult;
                }

                EnsureStationSchemeSchema(dbConnector);
                var normalizedStationSchemeID = ResolveStationSchemeIDForSave(
                    dbConnector,
                    normalizedInstanceID,
                    FirstNonEmpty(stationSchemeID, request.StationSchemeID, layout.Metadata?.StationSchemeID));
                var saveResult = SaveStationLayoutJsonToDatabase(
                    dbConnector,
                    normalizedInstanceID,
                    normalizedStationSchemeID,
                    layout);

                _logger.LogInformation(
                    "Station layout saved to database. InstanceID: {InstanceID}, StationSchemeID: {StationSchemeID}, Nodes: {NodeCount}, Links: {LinkCount}",
                    normalizedInstanceID,
                    normalizedStationSchemeID,
                    saveResult.NodeCount,
                    saveResult.LinkCount);
                return Ok(saveResult);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Invalid station layout JSON payload.");
                return BadRequest("Invalid JSON payload.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save station layout JSON to capacity database.");
                return StatusCode(500, "Failed to save station layout data.");
            }
        }

        [HttpPost(Name = "GetJson")]
        public IActionResult GetJson([FromQuery] string? instanceID = null, [FromQuery] string? stationSchemeID = null)
        {
            try
            {
                var normalizedInstanceID = instanceID?.Trim();
                if (string.IsNullOrWhiteSpace(normalizedInstanceID))
                {
                    return GetJsonFromFileFallback();
                }

                var dbConnector = GetCapacityDbConnector();
                var authResult = ValidateCapacityInstanceOwnershipOrFail(dbConnector, normalizedInstanceID);
                if (authResult != null)
                {
                    return authResult;
                }

                EnsureStationSchemeSchema(dbConnector);
                var normalizedStationSchemeID = ResolveStationSchemeID(dbConnector, normalizedInstanceID, stationSchemeID);
                if (string.IsNullOrWhiteSpace(normalizedStationSchemeID))
                {
                    return Content(BuildEmptyStationLayoutJson(), "application/json", Encoding.UTF8);
                }

                var layoutJson = BuildStationLayoutJsonFromDatabase(
                    dbConnector,
                    normalizedInstanceID,
                    normalizedStationSchemeID);

                return Content(layoutJson, "application/json", Encoding.UTF8);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Station layout JSON file contains invalid JSON.");
                return StatusCode(500, "StationLayout.json contains invalid JSON.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load station layout JSON from capacity database.");
                return StatusCode(500, "Failed to load station layout data.");
            }
        }

        [HttpPost(Name = "SearchRoutes")]
        public IActionResult SearchRoutes(
            [FromBody] StationRouteSearchRequest? request,
            [FromQuery] string? instanceID = null,
            [FromQuery] string? stationSchemeID = null)
        {
            if (request == null)
            {
                return BadRequest("Request body is required.");
            }

            try
            {
                var normalizedInstanceID = FirstNonEmpty(instanceID, request.InstanceID);
                if (string.IsNullOrWhiteSpace(normalizedInstanceID))
                {
                    return BadRequest("instanceID is required when searching station routes.");
                }

                var dbConnector = GetCapacityDbConnector();
                var authResult = ValidateCapacityInstanceOwnershipOrFail(dbConnector, normalizedInstanceID);
                if (authResult != null)
                {
                    return authResult;
                }

                EnsureStationSchemeSchema(dbConnector);
                EnsureLinkSchema(dbConnector);
                EnsureCellSchema(dbConnector);
                var normalizedStationSchemeID = ResolveStationSchemeID(
                    dbConnector,
                    normalizedInstanceID,
                    FirstNonEmpty(stationSchemeID, request.StationSchemeID));
                if (string.IsNullOrWhiteSpace(normalizedStationSchemeID))
                {
                    return BadRequest("stationSchemeID is required when searching station routes.");
                }

                var nodeTable = QuoteIdentifier("node");
                var linkTable = QuoteIdentifier("link");
                var switchTable = QuoteIdentifier("switch");
                var signalTable = QuoteIdentifier("signal");
                var cellTable = QuoteIdentifier("cell");
                var nodes = dbConnector.Query<StationNodeRow>(
                    $@"SELECT *
                       FROM {nodeTable}
                       WHERE InstanceID = @normalizedInstanceID AND StationSchemeID = @normalizedStationSchemeID
                       ORDER BY ID",
                    new { normalizedInstanceID, normalizedStationSchemeID }) ?? new List<StationNodeRow>();
                var links = dbConnector.Query<StationLinkRow>(
                    $@"SELECT *
                       FROM {linkTable}
                       WHERE InstanceID = @normalizedInstanceID AND StationSchemeID = @normalizedStationSchemeID
                       ORDER BY ID",
                    new { normalizedInstanceID, normalizedStationSchemeID }) ?? new List<StationLinkRow>();
                var switches = dbConnector.Query<StationSwitchRow>(
                    $@"SELECT ID, Name, {QuoteIdentifier("Type")} AS {QuoteIdentifier("Type")}, BindingNodeID
                       FROM {switchTable}
                       WHERE InstanceID = @normalizedInstanceID AND StationSchemeID = @normalizedStationSchemeID
                       ORDER BY ID",
                    new { normalizedInstanceID, normalizedStationSchemeID }) ?? new List<StationSwitchRow>();
                var signals = dbConnector.Query<StationSignalRow>(
                    $@"SELECT ID, Name, {QuoteIdentifier("Type")} AS {QuoteIdentifier("Type")}, Direction, BindingNodeID
                       FROM {signalTable}
                       WHERE InstanceID = @normalizedInstanceID AND StationSchemeID = @normalizedStationSchemeID
                       ORDER BY ID",
                    new { normalizedInstanceID, normalizedStationSchemeID }) ?? new List<StationSignalRow>();
                var cells = dbConnector.Query<StationCellRow>(
                    $@"SELECT InstanceID, StationSchemeID, ID, LinkIDList, Name
                       FROM {cellTable}
                       WHERE InstanceID = @normalizedInstanceID AND StationSchemeID = @normalizedStationSchemeID
                       ORDER BY ID",
                    new { normalizedInstanceID, normalizedStationSchemeID }) ?? new List<StationCellRow>();

                var startNode = nodes.FirstOrDefault(node => node.ID == request.StartNodeId);
                if (startNode == null)
                {
                    return BadRequest($"Start node {request.StartNodeId} does not exist.");
                }

                var endNode = nodes.FirstOrDefault(node => node.ID == request.EndNodeId);
                if (endNode == null)
                {
                    return BadRequest($"End node {request.EndNodeId} does not exist.");
                }

                var routeSearcher = new StationRouteSearcher(nodes, links);
                var routes = routeSearcher.Search(startNode, endNode);
                var response = new StationRouteSearchResponse
                {
                    InstanceID = normalizedInstanceID,
                    StationSchemeID = normalizedStationSchemeID,
                    StartNodeId = request.StartNodeId,
                    EndNodeId = request.EndNodeId,
                    Routes = routes.Select(route =>
                    {
                        var routeNodeIndexByID = route.Nodes
                            .Select((node, index) => new { NodeID = ToInvariantString(node.ID), index })
                            .ToDictionary(item => item.NodeID, item => item.index, StringComparer.OrdinalIgnoreCase);
                        var routeNodeIDSet = routeNodeIndexByID.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
                        var routeLinkIndexByID = route.Links
                            .Select((link, index) => new { LinkID = ToInvariantString(link.ID), index })
                            .GroupBy(item => item.LinkID, StringComparer.OrdinalIgnoreCase)
                            .ToDictionary(group => group.Key, group => group.Min(item => item.index), StringComparer.OrdinalIgnoreCase);
                        var routeSwitches = switches
                            .Where(sw => !string.IsNullOrWhiteSpace(sw.BindingNodeID) && routeNodeIDSet.Contains(sw.BindingNodeID.Trim()))
                            .OrderBy(sw => routeNodeIndexByID.TryGetValue(sw.BindingNodeID!.Trim(), out var index) ? index : int.MaxValue)
                            .ThenBy(sw => sw.ID ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                            .ToList();
                        var routeSignals = signals
                            .Where(signal => !string.IsNullOrWhiteSpace(signal.BindingNodeID) && routeNodeIDSet.Contains(signal.BindingNodeID.Trim()))
                            .OrderBy(signal => routeNodeIndexByID.TryGetValue(signal.BindingNodeID!.Trim(), out var index) ? index : int.MaxValue)
                            .ThenBy(signal => signal.ID ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                            .ToList();
                        var routeCells = cells
                            .Select(cell => new
                            {
                                Cell = cell,
                                FirstLinkIndex = GetFirstMatchingRouteLinkIndex(cell, routeLinkIndexByID)
                            })
                            .Where(item => item.FirstLinkIndex < int.MaxValue)
                            .OrderBy(item => item.FirstLinkIndex)
                            .ThenBy(item => item.Cell.ID ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                            .Select(item => item.Cell)
                            .ToList();

                        return new StationRouteSearchResult
                        {
                            Direction = route.Direction.ToString(),
                            NodeIds = route.Nodes.Select(node => node.ID).ToList(),
                            LinkIds = route.Links.Select(link => link.ID).ToList(),
                            SwitchIds = routeSwitches
                                .Select(sw => sw.ID?.Trim())
                                .Where(id => !string.IsNullOrWhiteSpace(id))
                                .Select(id => id!)
                                .ToList(),
                            CellIds = routeCells
                                .Select(cell => cell.ID?.Trim())
                                .Where(id => !string.IsNullOrWhiteSpace(id))
                                .Select(id => id!)
                                .ToList(),
                            SignalIds = routeSignals
                                .Select(signal => signal.ID?.Trim())
                                .Where(id => !string.IsNullOrWhiteSpace(id))
                                .Select(id => id!)
                                .ToList(),
                            Nodes = route.Nodes,
                            Links = route.Links,
                            Switches = routeSwitches,
                            Cells = routeCells,
                            Signals = routeSignals
                        };
                    })
                    .OrderBy(result => result.CellIds.Count)
                    .ThenBy(result => result.LinkIds.Count)
                    .ThenBy(result => result.Direction, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(result => string.Join(",", result.NodeIds))
                    .ToList()
                };

                return Ok(response);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid station route search request.");
                return BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Station route search failed.");
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to search station routes.");
                return StatusCode(500, "Failed to search station routes.");
            }
        }

        [HttpGet(Name = "GetStationRoutes")]
        public IActionResult GetStationRoutes(
            [FromQuery] string? instanceID = null,
            [FromQuery] string? stationSchemeID = null)
        {
            try
            {
                var normalizedInstanceID = instanceID?.Trim();
                if (string.IsNullOrWhiteSpace(normalizedInstanceID))
                {
                    return BadRequest("instanceID is required when loading station routes.");
                }

                var dbConnector = GetCapacityDbConnector();
                var authResult = ValidateCapacityInstanceOwnershipOrFail(dbConnector, normalizedInstanceID);
                if (authResult != null)
                {
                    return authResult;
                }

                EnsureStationSchemeSchema(dbConnector);
                EnsureStationRouteSchema(dbConnector);
                var normalizedStationSchemeID = ResolveStationSchemeID(dbConnector, normalizedInstanceID, stationSchemeID);
                if (string.IsNullOrWhiteSpace(normalizedStationSchemeID))
                {
                    return Ok(new List<StationRouteRow>());
                }

                return Ok(LoadStationRoutes(dbConnector, normalizedInstanceID, normalizedStationSchemeID));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load station routes.");
                return StatusCode(500, "Failed to load station routes.");
            }
        }

        [HttpPost(Name = "CreateStationRoute")]
        public IActionResult CreateStationRoute([FromBody] StationRouteRequest? request)
        {
            try
            {
                var dbConnector = GetCapacityDbConnector();
                var normalized = NormalizeStationRouteRequest(
                    request,
                    allowMissingID: true);
                if (normalized.ErrorResult != null)
                {
                    return normalized.ErrorResult;
                }

                var route = normalized.Route!;
                var authResult = ValidateCapacityInstanceOwnershipOrFail(dbConnector, route.InstanceID!);
                if (authResult != null)
                {
                    return authResult;
                }

                EnsureStationRouteSchema(dbConnector);
                if (!StationSchemeIDExists(dbConnector, route.InstanceID!, route.StationSchemeID!))
                {
                    return NotFound("Station scheme not found.");
                }

                if (string.IsNullOrWhiteSpace(route.ID))
                {
                    route.ID = GenerateStationRouteID(dbConnector, route.InstanceID!, route.StationSchemeID!);
                }

                if (!StationNodeIDExists(dbConnector, route.InstanceID!, route.StationSchemeID!, route.StartNodeID!) ||
                    !StationNodeIDExists(dbConnector, route.InstanceID!, route.StationSchemeID!, route.EndNodeID!))
                {
                    return BadRequest("StartNodeID and EndNodeID must reference existing nodes in the selected station scheme.");
                }

                EnsureStationRouteDescription(dbConnector, route);

                if (StationRouteIDExists(dbConnector, route.InstanceID!, route.StationSchemeID!, route.ID!))
                {
                    return BadRequest("Station route ID already exists in the selected station scheme.");
                }

                var tableName = QuoteIdentifier("stationroute");
                var result = dbConnector.ExecuteNonQuery(
                    $@"INSERT INTO {tableName} (
                           InstanceID, StationSchemeID, ID, {QuoteIdentifier("Type")}, Description,
                           NodeList, LinkList, SwitchList, CellList, SignalList,
                           AllowanceTags, ForbiddenTags, StartNodeID, EndNodeID)
                       VALUES (
                           @InstanceID, @StationSchemeID, @ID, @Type, @Description,
                           @NodeList, @LinkList, @SwitchList, @CellList, @SignalList,
                           @AllowanceTags, @ForbiddenTags, @StartNodeID, @EndNodeID)",
                    route);
                if (result <= 0)
                {
                    return StatusCode(500, "Failed to create station route.");
                }

                return Ok(route);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create station route.");
                return StatusCode(500, "Failed to create station route.");
            }
        }

        [HttpPut(Name = "EditStationRoute")]
        public IActionResult EditStationRoute([FromBody] StationRouteRequest? request)
        {
            try
            {
                var dbConnector = GetCapacityDbConnector();
                var normalized = NormalizeStationRouteRequest(
                    request,
                    allowMissingID: false);
                if (normalized.ErrorResult != null)
                {
                    return normalized.ErrorResult;
                }

                var route = normalized.Route!;
                var originalID = request?.OriginalID?.Trim();
                if (string.IsNullOrWhiteSpace(originalID))
                {
                    originalID = route.ID;
                }

                var authResult = ValidateCapacityInstanceOwnershipOrFail(dbConnector, route.InstanceID!);
                if (authResult != null)
                {
                    return authResult;
                }

                EnsureStationRouteSchema(dbConnector);
                if (!StationRouteIDExists(dbConnector, route.InstanceID!, route.StationSchemeID!, originalID!))
                {
                    return NotFound("Station route not found.");
                }

                if (!StationNodeIDExists(dbConnector, route.InstanceID!, route.StationSchemeID!, route.StartNodeID!) ||
                    !StationNodeIDExists(dbConnector, route.InstanceID!, route.StationSchemeID!, route.EndNodeID!))
                {
                    return BadRequest("StartNodeID and EndNodeID must reference existing nodes in the selected station scheme.");
                }

                EnsureStationRouteDescription(dbConnector, route);

                if (!string.Equals(originalID, route.ID, StringComparison.OrdinalIgnoreCase) &&
                    StationRouteIDExists(dbConnector, route.InstanceID!, route.StationSchemeID!, route.ID!))
                {
                    return BadRequest("Station route ID already exists in the selected station scheme.");
                }

                var tableName = QuoteIdentifier("stationroute");
                dbConnector.ExecuteNonQuery(
                    $@"UPDATE {tableName}
                       SET ID = @ID,
                           {QuoteIdentifier("Type")} = @Type,
                           Description = @Description,
                           NodeList = @NodeList,
                           LinkList = @LinkList,
                           SwitchList = @SwitchList,
                           CellList = @CellList,
                           SignalList = @SignalList,
                           AllowanceTags = @AllowanceTags,
                           ForbiddenTags = @ForbiddenTags,
                           StartNodeID = @StartNodeID,
                           EndNodeID = @EndNodeID
                       WHERE InstanceID = @InstanceID
                         AND StationSchemeID = @StationSchemeID
                         AND ID = @OriginalID",
                    new
                    {
                        route.InstanceID,
                        route.StationSchemeID,
                        route.ID,
                        route.Type,
                        route.Description,
                        route.NodeList,
                        route.LinkList,
                        route.SwitchList,
                        route.CellList,
                        route.SignalList,
                        route.AllowanceTags,
                        route.ForbiddenTags,
                        route.StartNodeID,
                        route.EndNodeID,
                        OriginalID = originalID
                    });

                return Ok(route);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update station route.");
                return StatusCode(500, "Failed to update station route.");
            }
        }

        [HttpPost(Name = "GenerateStationRouteDescription")]
        public IActionResult GenerateStationRouteDescription([FromBody] StationRouteRequest? request)
        {
            try
            {
                var dbConnector = GetCapacityDbConnector();
                var normalized = NormalizeStationRouteRequest(
                    request,
                    allowMissingID: true);
                if (normalized.ErrorResult != null)
                {
                    return normalized.ErrorResult;
                }

                var route = normalized.Route!;
                if (string.IsNullOrWhiteSpace(route.Type))
                {
                    return BadRequest("Type is required when generating a station route description.");
                }

                var authResult = ValidateCapacityInstanceOwnershipOrFail(dbConnector, route.InstanceID!);
                if (authResult != null)
                {
                    return authResult;
                }

                EnsureStationSchemeSchema(dbConnector);
                if (!StationSchemeIDExists(dbConnector, route.InstanceID!, route.StationSchemeID!))
                {
                    return NotFound("Station scheme not found.");
                }

                return Ok(new
                {
                    description = BuildStationRouteDescription(
                        dbConnector,
                        route.InstanceID!,
                        route.StationSchemeID!,
                        route.StartNodeID!,
                        route.EndNodeID!,
                        route.Type!)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate station route description.");
                return StatusCode(500, "Failed to generate station route description.");
            }
        }

        [HttpDelete(Name = "DeleteStationRoute")]
        public IActionResult DeleteStationRoute(
            [FromQuery] string? instanceID = null,
            [FromQuery] string? stationSchemeID = null,
            [FromQuery] string? id = null)
        {
            try
            {
                var normalizedInstanceID = instanceID?.Trim();
                var normalizedStationSchemeID = stationSchemeID?.Trim();
                var normalizedID = id?.Trim();
                if (string.IsNullOrWhiteSpace(normalizedInstanceID) ||
                    string.IsNullOrWhiteSpace(normalizedStationSchemeID) ||
                    string.IsNullOrWhiteSpace(normalizedID))
                {
                    return BadRequest("instanceID, stationSchemeID and id are required when deleting a station route.");
                }

                var dbConnector = GetCapacityDbConnector();
                var authResult = ValidateCapacityInstanceOwnershipOrFail(dbConnector, normalizedInstanceID);
                if (authResult != null)
                {
                    return authResult;
                }

                EnsureStationRouteSchema(dbConnector);
                if (!StationRouteIDExists(dbConnector, normalizedInstanceID, normalizedStationSchemeID, normalizedID))
                {
                    return NotFound("Station route not found.");
                }

                var tableName = QuoteIdentifier("stationroute");
                dbConnector.ExecuteNonQuery(
                    $@"DELETE FROM {tableName}
                       WHERE InstanceID = @normalizedInstanceID
                         AND StationSchemeID = @normalizedStationSchemeID
                         AND ID = @normalizedID",
                    new { normalizedInstanceID, normalizedStationSchemeID, normalizedID });

                return Ok("Station route deleted successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete station route.");
                return StatusCode(500, "Failed to delete station route.");
            }
        }

        [HttpGet(Name = "GetStationRouteEnds")]
        public IActionResult GetStationRouteEnds(
            [FromQuery] string? instanceID = null,
            [FromQuery] string? stationSchemeID = null)
        {
            try
            {
                var normalizedInstanceID = instanceID?.Trim();
                if (string.IsNullOrWhiteSpace(normalizedInstanceID))
                {
                    return BadRequest("instanceID is required when loading station route ends.");
                }

                var dbConnector = GetCapacityDbConnector();
                var authResult = ValidateCapacityInstanceOwnershipOrFail(dbConnector, normalizedInstanceID);
                if (authResult != null)
                {
                    return authResult;
                }

                EnsureStationSchemeSchema(dbConnector);
                EnsureStationRouteEndSchema(dbConnector);
                var normalizedStationSchemeID = ResolveStationSchemeID(dbConnector, normalizedInstanceID, stationSchemeID);
                if (string.IsNullOrWhiteSpace(normalizedStationSchemeID))
                {
                    return Ok(new List<StationRouteEndRow>());
                }

                return Ok(LoadStationRouteEnds(dbConnector, normalizedInstanceID, normalizedStationSchemeID));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load station route ends.");
                return StatusCode(500, "Failed to load station route ends.");
            }
        }

        [HttpPost(Name = "CreateStationRouteEnd")]
        public IActionResult CreateStationRouteEnd([FromBody] StationRouteEndRequest? request)
        {
            try
            {
                var dbConnector = GetCapacityDbConnector();
                var normalized = NormalizeStationRouteEndRequest(
                    request,
                    allowMissingID: true);
                if (normalized.ErrorResult != null)
                {
                    return normalized.ErrorResult;
                }

                var routeEnd = normalized.RouteEnd!;
                var authResult = ValidateCapacityInstanceOwnershipOrFail(dbConnector, routeEnd.InstanceID!);
                if (authResult != null)
                {
                    return authResult;
                }

                EnsureStationRouteEndSchema(dbConnector);
                if (!StationSchemeIDExists(dbConnector, routeEnd.InstanceID!, routeEnd.StationSchemeID!))
                {
                    return NotFound("Station scheme not found.");
                }

                if (string.IsNullOrWhiteSpace(routeEnd.ID))
                {
                    routeEnd.ID = GenerateStationRouteEndID(dbConnector, routeEnd.InstanceID!, routeEnd.StationSchemeID!);
                }

                if (!StationRouteEndBindingNodeExists(
                    dbConnector,
                    routeEnd.InstanceID!,
                    routeEnd.StationSchemeID!,
                    routeEnd.BindingNodeID!))
                {
                    return BadRequest("BindingNodeID must reference an existing node in the selected station scheme.");
                }

                if (StationRouteEndIDExists(dbConnector, routeEnd.InstanceID!, routeEnd.StationSchemeID!, routeEnd.ID!))
                {
                    return BadRequest("Station route end ID already exists in the selected station scheme.");
                }

                var tableName = QuoteIdentifier("stationrouteend");
                var result = dbConnector.ExecuteNonQuery(
                    $@"INSERT INTO {tableName} (
                           InstanceID, StationSchemeID, ID, BindingNodeID, {QuoteIdentifier("Type")}, SegmentTag, SidingTag)
                       VALUES (
                           @InstanceID, @StationSchemeID, @ID, @BindingNodeID, @Type, @SegmentTag, @SidingTag)",
                    routeEnd);
                if (result <= 0)
                {
                    return StatusCode(500, "Failed to create station route end.");
                }

                return Ok(routeEnd);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create station route end.");
                return StatusCode(500, "Failed to create station route end.");
            }
        }

        [HttpPut(Name = "EditStationRouteEnd")]
        public IActionResult EditStationRouteEnd([FromBody] StationRouteEndRequest? request)
        {
            try
            {
                var dbConnector = GetCapacityDbConnector();
                var normalized = NormalizeStationRouteEndRequest(
                    request,
                    allowMissingID: false);
                if (normalized.ErrorResult != null)
                {
                    return normalized.ErrorResult;
                }

                var routeEnd = normalized.RouteEnd!;
                var originalID = request?.OriginalID?.Trim();
                if (string.IsNullOrWhiteSpace(originalID))
                {
                    originalID = routeEnd.ID;
                }

                var authResult = ValidateCapacityInstanceOwnershipOrFail(dbConnector, routeEnd.InstanceID!);
                if (authResult != null)
                {
                    return authResult;
                }

                EnsureStationRouteEndSchema(dbConnector);
                if (!StationRouteEndIDExists(dbConnector, routeEnd.InstanceID!, routeEnd.StationSchemeID!, originalID!))
                {
                    return NotFound("Station route end not found.");
                }

                if (!StationRouteEndBindingNodeExists(
                    dbConnector,
                    routeEnd.InstanceID!,
                    routeEnd.StationSchemeID!,
                    routeEnd.BindingNodeID!))
                {
                    return BadRequest("BindingNodeID must reference an existing node in the selected station scheme.");
                }

                if (!string.Equals(originalID, routeEnd.ID, StringComparison.OrdinalIgnoreCase) &&
                    StationRouteEndIDExists(dbConnector, routeEnd.InstanceID!, routeEnd.StationSchemeID!, routeEnd.ID!))
                {
                    return BadRequest("Station route end ID already exists in the selected station scheme.");
                }

                var tableName = QuoteIdentifier("stationrouteend");
                dbConnector.ExecuteNonQuery(
                    $@"UPDATE {tableName}
                       SET ID = @ID,
                           BindingNodeID = @BindingNodeID,
                           {QuoteIdentifier("Type")} = @Type,
                           SegmentTag = @SegmentTag,
                           SidingTag = @SidingTag
                       WHERE InstanceID = @InstanceID
                         AND StationSchemeID = @StationSchemeID
                         AND ID = @OriginalID",
                    new
                    {
                        routeEnd.InstanceID,
                        routeEnd.StationSchemeID,
                        routeEnd.ID,
                        routeEnd.BindingNodeID,
                        routeEnd.Type,
                        routeEnd.SegmentTag,
                        routeEnd.SidingTag,
                        OriginalID = originalID
                    });

                return Ok(routeEnd);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update station route end.");
                return StatusCode(500, "Failed to update station route end.");
            }
        }

        [HttpDelete(Name = "DeleteStationRouteEnd")]
        public IActionResult DeleteStationRouteEnd(
            [FromQuery] string? instanceID = null,
            [FromQuery] string? stationSchemeID = null,
            [FromQuery] string? id = null)
        {
            try
            {
                var normalizedInstanceID = instanceID?.Trim();
                var normalizedStationSchemeID = stationSchemeID?.Trim();
                var normalizedID = id?.Trim();
                if (string.IsNullOrWhiteSpace(normalizedInstanceID) ||
                    string.IsNullOrWhiteSpace(normalizedStationSchemeID) ||
                    string.IsNullOrWhiteSpace(normalizedID))
                {
                    return BadRequest("instanceID, stationSchemeID and id are required when deleting a station route end.");
                }

                var dbConnector = GetCapacityDbConnector();
                var authResult = ValidateCapacityInstanceOwnershipOrFail(dbConnector, normalizedInstanceID);
                if (authResult != null)
                {
                    return authResult;
                }

                EnsureStationRouteEndSchema(dbConnector);
                if (!StationRouteEndIDExists(dbConnector, normalizedInstanceID, normalizedStationSchemeID, normalizedID))
                {
                    return NotFound("Station route end not found.");
                }

                var tableName = QuoteIdentifier("stationrouteend");
                dbConnector.ExecuteNonQuery(
                    $@"DELETE FROM {tableName}
                       WHERE InstanceID = @normalizedInstanceID
                         AND StationSchemeID = @normalizedStationSchemeID
                         AND ID = @normalizedID",
                    new { normalizedInstanceID, normalizedStationSchemeID, normalizedID });

                return Ok("Station route end deleted successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete station route end.");
                return StatusCode(500, "Failed to delete station route end.");
            }
        }

        [HttpGet(Name = "GetStationSchemes")]
        public IActionResult GetStationSchemes([FromQuery] string? instanceID = null)
        {
            try
            {
                var normalizedInstanceID = instanceID?.Trim();
                if (string.IsNullOrWhiteSpace(normalizedInstanceID))
                {
                    return BadRequest("instanceID is required when loading station schemes.");
                }

                var dbConnector = GetCapacityDbConnector();
                var authResult = ValidateCapacityInstanceOwnershipOrFail(dbConnector, normalizedInstanceID);
                if (authResult != null)
                {
                    return authResult;
                }

                var stationSchemes = LoadStationSchemes(dbConnector, normalizedInstanceID);
                return Ok(stationSchemes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load station schemes from capacity database.");
                return StatusCode(500, "Failed to load station schemes.");
            }
        }

        [HttpPost(Name = "CreateStationScheme")]
        public IActionResult CreateStationScheme([FromBody] StationSchemeRequest? request)
        {
            try
            {
                var normalizedInstanceID = request?.InstanceID?.Trim();
                if (string.IsNullOrWhiteSpace(normalizedInstanceID))
                {
                    return BadRequest("instanceID is required when creating a station scheme.");
                }

                var dbConnector = GetCapacityDbConnector();
                var authResult = ValidateCapacityInstanceOwnershipOrFail(dbConnector, normalizedInstanceID);
                if (authResult != null)
                {
                    return authResult;
                }

                EnsureStationSchemeSchema(dbConnector);
                var normalizedID = GenerateStationSchemeID(dbConnector, normalizedInstanceID);
                var normalizedName = NormalizeStationSchemeName(request?.Name, normalizedID);

                var stationSchemeTable = QuoteIdentifier("stationscheme");
                var result = dbConnector.ExecuteNonQuery(
                    $@"INSERT INTO {stationSchemeTable} (InstanceID, ID, Name, {QuoteIdentifier("GridSettings")})
                       VALUES (@InstanceID, @ID, @Name, @GridSettings)",
                    new
                    {
                        InstanceID = normalizedInstanceID,
                        ID = normalizedID,
                        Name = normalizedName,
                        GridSettings = DefaultStationSchemeGridSettingsJson
                    });
                if (result <= 0)
                {
                    return StatusCode(500, "Failed to create station scheme.");
                }

                return Ok(new StationSchemeLookupRow { ID = normalizedID, Name = normalizedName });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create station scheme.");
                return StatusCode(500, "Failed to create station scheme.");
            }
        }

        [HttpPut(Name = "EditStationScheme")]
        public IActionResult EditStationScheme([FromBody] StationSchemeUpdateRequest? request)
        {
            DBConnector? dbConnector = null;
            try
            {
                var normalizedInstanceID = request?.InstanceID?.Trim();
                var originalID = request?.OriginalID?.Trim();
                if (string.IsNullOrWhiteSpace(normalizedInstanceID) ||
                    string.IsNullOrWhiteSpace(originalID))
                {
                    return BadRequest("instanceID and originalID are required when editing a station scheme.");
                }

                var normalizedName = NormalizeStationSchemeName(request?.Name, originalID);
                dbConnector = GetCapacityDbConnector();
                var authResult = ValidateCapacityInstanceOwnershipOrFail(dbConnector, normalizedInstanceID);
                if (authResult != null)
                {
                    return authResult;
                }

                EnsureStationSchemeSchema(dbConnector);
                if (!StationSchemeIDExists(dbConnector, normalizedInstanceID, originalID))
                {
                    return NotFound("Station scheme not found.");
                }

                dbConnector.BeginTransaction();
                UpsertStationSchemeMetadata(
                    dbConnector,
                    normalizedInstanceID,
                    originalID,
                    originalID,
                    normalizedName);
                dbConnector.Commit();

                return Ok(new StationSchemeLookupRow { ID = originalID, Name = normalizedName });
            }
            catch (Exception ex)
            {
                dbConnector?.Rollback();
                _logger.LogError(ex, "Failed to edit station scheme.");
                return StatusCode(500, "Failed to edit station scheme.");
            }
        }

        [HttpDelete(Name = "DeleteStationScheme")]
        public IActionResult DeleteStationScheme(
            [FromQuery] string? instanceID = null,
            [FromQuery] string? stationSchemeID = null)
        {
            DBConnector? dbConnector = null;
            try
            {
                var normalizedInstanceID = instanceID?.Trim();
                var normalizedStationSchemeID = stationSchemeID?.Trim();
                if (string.IsNullOrWhiteSpace(normalizedInstanceID) ||
                    string.IsNullOrWhiteSpace(normalizedStationSchemeID))
                {
                    return BadRequest("instanceID and stationSchemeID are required when deleting a station scheme.");
                }

                dbConnector = GetCapacityDbConnector();
                var authResult = ValidateCapacityInstanceOwnershipOrFail(dbConnector, normalizedInstanceID);
                if (authResult != null)
                {
                    return authResult;
                }

                EnsureStationSchemeSchema(dbConnector);
                if (!StationSchemeIDExists(dbConnector, normalizedInstanceID, normalizedStationSchemeID))
                {
                    return NotFound("Station scheme not found.");
                }

                dbConnector.BeginTransaction();
                DeleteStationLayoutTableRowsForExistingTables(
                    dbConnector,
                    normalizedInstanceID,
                    normalizedStationSchemeID);
                DeleteStationSchemeMetadata(
                    dbConnector,
                    normalizedInstanceID,
                    normalizedStationSchemeID);
                dbConnector.Commit();

                return Ok("Station scheme deleted successfully.");
            }
            catch (Exception ex)
            {
                dbConnector?.Rollback();
                _logger.LogError(ex, "Failed to delete station scheme.");
                return StatusCode(500, "Failed to delete station scheme.");
            }
        }

        private IActionResult GetJsonFromFileFallback()
        {
            if (!TryResolveStationLayoutFilePath(out var filePath, out var errorMessage))
            {
                return StatusCode(500, errorMessage);
            }

            try
            {
                if (!System.IO.File.Exists(filePath))
                {
                    return Content(BuildEmptyStationLayoutJson(), "application/json", Encoding.UTF8);
                }

                var jsonContent = System.IO.File.ReadAllText(filePath, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(jsonContent))
                {
                    jsonContent = BuildEmptyStationLayoutJson();
                }
                else
                {
                    using var document = JsonDocument.Parse(jsonContent);
                    jsonContent = JsonSerializer.Serialize(document.RootElement);
                }

                return Content(jsonContent, "application/json", Encoding.UTF8);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Station layout JSON file contains invalid JSON. FilePath: {FilePath}", filePath);
                return StatusCode(500, "StationLayout.json contains invalid JSON.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load station layout JSON from {FilePath}", filePath);
                return StatusCode(500, "Failed to load StationLayout.json.");
            }
        }

        private bool TryResolveStationLayoutFilePath(out string filePath, out string errorMessage)
        {
            try
            {
                var rootPath = Path.GetFullPath(_environment.ContentRootPath);
                var targetDirectory = Path.GetFullPath(Path.Combine(rootPath, "LocalData", "Capacity"));

                // Ensure the resolved directory does not escape the content root.
                if (!targetDirectory.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogError("Resolved StationLayout directory escapes the content root. Target: {Target}", targetDirectory);
                    filePath = string.Empty;
                    errorMessage = "Failed to resolve StationLayout.json path.";
                    return false;
                }

                filePath = Path.Combine(targetDirectory, "StationLayout.json");
                errorMessage = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to resolve StationLayout.json path.");
                filePath = string.Empty;
                errorMessage = "Failed to resolve StationLayout.json path.";
                return false;
            }
        }

        private static string BuildEmptyStationLayoutJson()
        {
            return JsonSerializer.Serialize(new
            {
                metadata = new
                {
                    latestElementID = 0,
                    gridSettings = new { showGrid = true, spacing = 20, originX = 0, originY = 0 }
                },
                tracks = Array.Empty<object>(),
                curves = Array.Empty<object>(),
                nodes = Array.Empty<object>(),
                signals = Array.Empty<object>(),
                insulationJoints = Array.Empty<object>(),
                bufferStops = Array.Empty<object>(),
                platforms = Array.Empty<object>(),
                switches = Array.Empty<object>(),
                cells = Array.Empty<object>(),
                annotations = Array.Empty<object>()
            });
        }

        private DBConnector GetCapacityDbConnector()
        {
            return DBConnector.GetDBConnector(DBConnector.CapacityDatabaseSectionName);
        }

        private string? ResolveStationSchemeID(DBConnector dbConnector, string instanceID, string? stationSchemeID)
        {
            var normalizedStationSchemeID = stationSchemeID?.Trim();
            if (!string.IsNullOrWhiteSpace(normalizedStationSchemeID))
            {
                return normalizedStationSchemeID;
            }

            var nodeTable = QuoteIdentifier("node");
            var linkTable = QuoteIdentifier("link");
            var stationSchemeTable = QuoteIdentifier("stationscheme");

            var schemeFromNodes = (dbConnector.Query<StationSchemeLookupRow>(
                $@"SELECT StationSchemeID AS ID
                   FROM {nodeTable}
                   WHERE InstanceID = @instanceID
                   GROUP BY StationSchemeID
                   ORDER BY COUNT(1) DESC, StationSchemeID
                   LIMIT 1",
                new { instanceID }) ?? new List<StationSchemeLookupRow>()).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(schemeFromNodes?.ID))
            {
                return schemeFromNodes.ID;
            }

            var schemeFromLinks = (dbConnector.Query<StationSchemeLookupRow>(
                $@"SELECT StationSchemeID AS ID
                   FROM {linkTable}
                   WHERE InstanceID = @instanceID
                   GROUP BY StationSchemeID
                   ORDER BY COUNT(1) DESC, StationSchemeID
                   LIMIT 1",
                new { instanceID }) ?? new List<StationSchemeLookupRow>()).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(schemeFromLinks?.ID))
            {
                return schemeFromLinks.ID;
            }

            return (dbConnector.Query<StationSchemeLookupRow>(
                $@"SELECT ID
                   FROM {stationSchemeTable}
                   WHERE InstanceID = @instanceID
                   ORDER BY ID
                   LIMIT 1",
                new { instanceID }) ?? new List<StationSchemeLookupRow>())
                .FirstOrDefault()
                ?.ID;
        }

        private string ResolveStationSchemeIDForSave(DBConnector dbConnector, string instanceID, string? stationSchemeID)
        {
            return FirstNonEmpty(stationSchemeID, ResolveStationSchemeID(dbConnector, instanceID, null))
                ?? DefaultStationSchemeID;
        }

        private List<StationSchemeLookupRow> LoadStationSchemes(DBConnector dbConnector, string instanceID)
        {
            EnsureStationSchemeSchema(dbConnector);

            var stationSchemeTable = QuoteIdentifier("stationscheme");
            var rows = dbConnector.Query<StationSchemeLookupRow>(
                $@"SELECT ID, Name
                   FROM {stationSchemeTable}
                   WHERE InstanceID = @instanceID
                   ORDER BY CASE WHEN Name IS NULL OR TRIM(Name) = '' THEN ID ELSE Name END, ID",
                new { instanceID }) ?? new List<StationSchemeLookupRow>();

            var schemesByID = new Dictionary<string, StationSchemeLookupRow>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows)
            {
                AddStationSchemeLookupRow(schemesByID, row.ID, row.Name);
            }

            foreach (var tableName in new[]
            {
                "node",
                "link",
                "curve",
                "signal",
                "insulationjoint",
                "bufferstop",
                "platform",
                "switch",
                "switchbranchvector",
                "stationroute",
                "stationrouteend",
                "cell",
                "annotation"
            })
            {
                AddStationSchemeIdsFromLayoutTable(dbConnector, schemesByID, tableName, instanceID);
            }

            return schemesByID.Values
                .OrderBy(
                    row => string.IsNullOrWhiteSpace(row.Name) ? row.ID ?? string.Empty : row.Name ?? string.Empty,
                    StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.ID ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static void AddStationSchemeLookupRow(
            IDictionary<string, StationSchemeLookupRow> schemesByID,
            string? id,
            string? name)
        {
            var normalizedID = id?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedID))
            {
                return;
            }

            var normalizedName = string.IsNullOrWhiteSpace(name) ? normalizedID : name.Trim();
            if (schemesByID.TryGetValue(normalizedID, out var existing))
            {
                if (string.IsNullOrWhiteSpace(existing.Name) ||
                    string.Equals(existing.Name, existing.ID, StringComparison.OrdinalIgnoreCase))
                {
                    existing.Name = normalizedName;
                }

                return;
            }

            schemesByID[normalizedID] = new StationSchemeLookupRow
            {
                ID = normalizedID,
                Name = normalizedName
            };
        }

        private static void AddStationSchemeIdsFromLayoutTable(
            DBConnector dbConnector,
            IDictionary<string, StationSchemeLookupRow> schemesByID,
            string tableName,
            string instanceID)
        {
            var columnNames = GetColumnNames(dbConnector, tableName);
            if (!columnNames.Any(column => string.Equals(column, "StationSchemeID", StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            var rows = dbConnector.Query<StationSchemeLookupRow>(
                $@"SELECT {QuoteIdentifier("StationSchemeID")} AS ID
                   FROM {QuoteIdentifier(tableName)}
                   WHERE {QuoteIdentifier("InstanceID")} = @instanceID
                     AND {QuoteIdentifier("StationSchemeID")} IS NOT NULL
                     AND TRIM({QuoteIdentifier("StationSchemeID")}) <> ''
                   GROUP BY {QuoteIdentifier("StationSchemeID")}
                   ORDER BY {QuoteIdentifier("StationSchemeID")}",
                new { instanceID }) ?? new List<StationSchemeLookupRow>();

            foreach (var row in rows)
            {
                AddStationSchemeLookupRow(schemesByID, row.ID, row.ID);
            }
        }

        private static bool StationSchemeIDExists(DBConnector dbConnector, string instanceID, string stationSchemeID)
        {
            if (string.IsNullOrWhiteSpace(stationSchemeID))
            {
                return false;
            }

            if (StationSchemeMetadataExists(dbConnector, instanceID, stationSchemeID))
            {
                return true;
            }

            return StationLayoutDataTables.Any(tableName =>
                StationSchemeIDExistsInLayoutTable(dbConnector, tableName, instanceID, stationSchemeID));
        }

        private string GenerateStationSchemeID(DBConnector dbConnector, string instanceID)
        {
            for (var attempt = 0; attempt < 10; attempt++)
            {
                var candidate = _snowflakeIdGenerator.NextIdString();
                if (!StationSchemeIDExists(dbConnector, instanceID, candidate))
                {
                    return candidate;
                }
            }

            throw new InvalidOperationException("Failed to generate a unique station scheme ID.");
        }

        private static bool StationSchemeMetadataExists(DBConnector dbConnector, string instanceID, string stationSchemeID)
        {
            EnsureStationSchemeSchema(dbConnector);
            var stationSchemeTable = QuoteIdentifier("stationscheme");
            return (dbConnector.Query<StationSchemeLookupRow>(
                $@"SELECT ID
                   FROM {stationSchemeTable}
                   WHERE InstanceID = @instanceID AND ID = @stationSchemeID
                   LIMIT 1",
                new { instanceID, stationSchemeID }) ?? new List<StationSchemeLookupRow>()).Any();
        }

        private static bool StationSchemeIDExistsInLayoutTable(
            DBConnector dbConnector,
            string tableName,
            string instanceID,
            string stationSchemeID)
        {
            var columnNames = GetColumnNames(dbConnector, tableName);
            if (!columnNames.Any(column => string.Equals(column, "StationSchemeID", StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            var rows = dbConnector.Query<StationSchemeLookupRow>(
                $@"SELECT {QuoteIdentifier("StationSchemeID")} AS ID
                   FROM {QuoteIdentifier(tableName)}
                   WHERE {QuoteIdentifier("InstanceID")} = @instanceID
                     AND {QuoteIdentifier("StationSchemeID")} = @stationSchemeID
                   LIMIT 1",
                new { instanceID, stationSchemeID }) ?? new List<StationSchemeLookupRow>();
            return rows.Any();
        }

        private static void UpsertStationSchemeMetadata(
            DBConnector dbConnector,
            string instanceID,
            string originalStationSchemeID,
            string stationSchemeID,
            string name)
        {
            var stationSchemeTable = QuoteIdentifier("stationscheme");
            if (StationSchemeMetadataExists(dbConnector, instanceID, originalStationSchemeID))
            {
                dbConnector.ExecuteNonQuery(
                    $@"UPDATE {stationSchemeTable}
                       SET ID = @stationSchemeID, Name = @name
                       WHERE InstanceID = @instanceID AND ID = @originalStationSchemeID",
                    new { instanceID, originalStationSchemeID, stationSchemeID, name });
                return;
            }

            dbConnector.ExecuteNonQuery(
                $@"INSERT INTO {stationSchemeTable} (InstanceID, ID, Name, {QuoteIdentifier("GridSettings")})
                   VALUES (@InstanceID, @ID, @Name, @GridSettings)",
                new
                {
                    InstanceID = instanceID,
                    ID = stationSchemeID,
                    Name = name,
                    GridSettings = DefaultStationSchemeGridSettingsJson
                });
        }

        private static string? LoadStationSchemeDisplayStyles(
            DBConnector dbConnector,
            string instanceID,
            string stationSchemeID)
        {
            EnsureStationSchemeSchema(dbConnector);
            var stationSchemeTable = QuoteIdentifier("stationscheme");
            return (dbConnector.Query<StationSchemeLookupRow>(
                $@"SELECT {QuoteIdentifier("DisplayStyles")} AS DisplayStyles
                   FROM {stationSchemeTable}
                   WHERE InstanceID = @instanceID AND ID = @stationSchemeID
                   LIMIT 1",
                new { instanceID, stationSchemeID }) ?? new List<StationSchemeLookupRow>())
                .FirstOrDefault()
                ?.DisplayStyles;
        }

        private static JsonElement? ParseStationSchemeDisplayStyles(string? displayStyles)
        {
            if (string.IsNullOrWhiteSpace(displayStyles))
            {
                return null;
            }

            try
            {
                using var document = JsonDocument.Parse(displayStyles);
                return document.RootElement.Clone();
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static void PersistStationSchemeDisplayStyles(
            DBConnector dbConnector,
            string instanceID,
            string stationSchemeID,
            JsonElement? displayStyles)
        {
            if (!displayStyles.HasValue ||
                displayStyles.Value.ValueKind == JsonValueKind.Null ||
                displayStyles.Value.ValueKind == JsonValueKind.Undefined)
            {
                return;
            }

            var stationSchemeTable = QuoteIdentifier("stationscheme");
            dbConnector.ExecuteNonQuery(
                $@"UPDATE {stationSchemeTable}
                   SET {QuoteIdentifier("DisplayStyles")} = @displayStyles
                   WHERE InstanceID = @instanceID AND ID = @stationSchemeID",
                new
                {
                    instanceID,
                    stationSchemeID,
                    displayStyles = displayStyles.Value.GetRawText()
                });
        }

        private static string? LoadStationSchemeGridSettings(
            DBConnector dbConnector,
            string instanceID,
            string stationSchemeID)
        {
            EnsureStationSchemeSchema(dbConnector);
            var stationSchemeTable = QuoteIdentifier("stationscheme");
            return (dbConnector.Query<StationSchemeLookupRow>(
                $@"SELECT {QuoteIdentifier("GridSettings")} AS GridSettings
                   FROM {stationSchemeTable}
                   WHERE InstanceID = @instanceID AND ID = @stationSchemeID
                   LIMIT 1",
                new { instanceID, stationSchemeID }) ?? new List<StationSchemeLookupRow>())
                .FirstOrDefault()
                ?.GridSettings;
        }

        private static JsonElement? ParseStationSchemeGridSettings(string? gridSettings)
        {
            if (string.IsNullOrWhiteSpace(gridSettings))
            {
                return null;
            }

            try
            {
                using var document = JsonDocument.Parse(gridSettings);
                return document.RootElement.Clone();
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static void PersistStationSchemeGridSettings(
            DBConnector dbConnector,
            string instanceID,
            string stationSchemeID,
            JsonElement? gridSettings)
        {
            if (!gridSettings.HasValue ||
                gridSettings.Value.ValueKind == JsonValueKind.Null ||
                gridSettings.Value.ValueKind == JsonValueKind.Undefined)
            {
                return;
            }

            var stationSchemeTable = QuoteIdentifier("stationscheme");
            dbConnector.ExecuteNonQuery(
                $@"UPDATE {stationSchemeTable}
                   SET {QuoteIdentifier("GridSettings")} = @gridSettings
                   WHERE InstanceID = @instanceID AND ID = @stationSchemeID",
                new
                {
                    instanceID,
                    stationSchemeID,
                    gridSettings = gridSettings.Value.GetRawText()
                });
        }

        private static void DeleteStationSchemeMetadata(
            DBConnector dbConnector,
            string instanceID,
            string stationSchemeID)
        {
            dbConnector.ExecuteNonQuery(
                $@"DELETE FROM {QuoteIdentifier("stationscheme")}
                   WHERE InstanceID = @instanceID AND ID = @stationSchemeID",
                new { instanceID, stationSchemeID });
        }

        private static void DeleteStationLayoutTableRowsForExistingTables(
            DBConnector dbConnector,
            string instanceID,
            string stationSchemeID)
        {
            foreach (var tableName in StationLayoutDataTables)
            {
                var columnNames = GetColumnNames(dbConnector, tableName);
                if (!columnNames.Any(column => string.Equals(column, "StationSchemeID", StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                DeleteStationLayoutTableRows(dbConnector, QuoteIdentifier(tableName), instanceID, stationSchemeID);
            }
        }

        private static string NormalizeStationSchemeName(string? name, string stationSchemeID)
        {
            return string.IsNullOrWhiteSpace(name) ? stationSchemeID : name.Trim();
        }

        private void EnsureStationSchemeExists(DBConnector dbConnector, string instanceID, string stationSchemeID)
        {
            EnsureStationSchemeSchema(dbConnector);
            var stationSchemeTable = QuoteIdentifier("stationscheme");
            var exists = (dbConnector.Query<StationSchemeLookupRow>(
                $@"SELECT ID
                   FROM {stationSchemeTable}
                   WHERE InstanceID = @instanceID AND ID = @stationSchemeID
                   LIMIT 1",
                new { instanceID, stationSchemeID }) ?? new List<StationSchemeLookupRow>()).Any();
            if (exists)
            {
                return;
            }

            var result = dbConnector.ExecuteNonQuery(
                $@"INSERT INTO {stationSchemeTable} (InstanceID, ID, Name, {QuoteIdentifier("GridSettings")})
                   VALUES (@InstanceID, @ID, @Name, @GridSettings)",
                new
                {
                    InstanceID = instanceID,
                    ID = stationSchemeID,
                    Name = stationSchemeID == DefaultStationSchemeID ? DefaultStationSchemeName : stationSchemeID,
                    GridSettings = DefaultStationSchemeGridSettingsJson
                });
            if (result <= 0)
            {
                throw new InvalidOperationException("Failed to create station scheme.");
            }
        }

        private string BuildStationLayoutJsonFromDatabase(DBConnector dbConnector, string instanceID, string stationSchemeID)
        {
            var nodeTable = QuoteIdentifier("node");
            var linkTable = QuoteIdentifier("link");
            var curveTable = QuoteIdentifier("curve");
            var signalTable = QuoteIdentifier("signal");
            var insulationJointTable = QuoteIdentifier("insulationjoint");
            var bufferStopTable = QuoteIdentifier("bufferstop");
            var platformTable = QuoteIdentifier("platform");
            var switchTable = QuoteIdentifier("switch");
            var cellTable = QuoteIdentifier("cell");
            var switchBranchVectorTable = QuoteIdentifier("switchbranchvector");
            var annotationTable = QuoteIdentifier("annotation");

            EnsureLinkSchema(dbConnector);
            EnsureCurveSchema(dbConnector);
            EnsureBufferStopSchema(dbConnector);
            EnsureNamedDeviceSchemas(dbConnector);
            EnsureCellSchema(dbConnector);

            var nodes = dbConnector.Query<StationNodeRow>(
                $@"SELECT *
                   FROM {nodeTable}
                   WHERE InstanceID = @instanceID AND StationSchemeID = @stationSchemeID
                   ORDER BY ID",
                new { instanceID, stationSchemeID }) ?? new List<StationNodeRow>();

            var links = dbConnector.Query<StationLinkRow>(
                $@"SELECT *
                   FROM {linkTable}
                   WHERE InstanceID = @instanceID AND StationSchemeID = @stationSchemeID
                   ORDER BY ID",
                new { instanceID, stationSchemeID }) ?? new List<StationLinkRow>();

            var curves = dbConnector.Query<StationCurveRow>(
                $@"SELECT *
                   FROM {curveTable}
                   WHERE InstanceID = @instanceID AND StationSchemeID = @stationSchemeID
                   ORDER BY ID",
                new { instanceID, stationSchemeID }) ?? new List<StationCurveRow>();

            var signals = dbConnector.Query<StationSignalRow>(
                $@"SELECT *
                   FROM {signalTable}
                   WHERE InstanceID = @instanceID AND StationSchemeID = @stationSchemeID
                   ORDER BY ID",
                new { instanceID, stationSchemeID }) ?? new List<StationSignalRow>();

            var insulationJoints = dbConnector.Query<StationInsulationJointRow>(
                $@"SELECT *
                   FROM {insulationJointTable}
                   WHERE InstanceID = @instanceID AND StationSchemeID = @stationSchemeID
                   ORDER BY ID",
                new { instanceID, stationSchemeID }) ?? new List<StationInsulationJointRow>();

            var bufferStops = dbConnector.Query<StationBufferStopRow>(
                $@"SELECT *
                   FROM {bufferStopTable}
                   WHERE InstanceID = @instanceID AND StationSchemeID = @stationSchemeID
                   ORDER BY ID",
                new { instanceID, stationSchemeID }) ?? new List<StationBufferStopRow>();

            var platforms = dbConnector.Query<StationPlatformRow>(
                $@"SELECT *
                   FROM {platformTable}
                   WHERE InstanceID = @instanceID AND StationSchemeID = @stationSchemeID
                   ORDER BY ID",
                new { instanceID, stationSchemeID }) ?? new List<StationPlatformRow>();

            var switches = dbConnector.Query<StationSwitchRow>(
                $@"SELECT *
                   FROM {switchTable}
                   WHERE InstanceID = @instanceID AND StationSchemeID = @stationSchemeID
                   ORDER BY ID",
                new { instanceID, stationSchemeID }) ?? new List<StationSwitchRow>();

            var switchBranchVectors = dbConnector.Query<SwitchBranchVectorRow>(
                $@"SELECT *
                   FROM {switchBranchVectorTable}
                   WHERE InstanceID = @instanceID AND StationSchemeID = @stationSchemeID
                   ORDER BY SwitchID, Sequence",
                new { instanceID, stationSchemeID }) ?? new List<SwitchBranchVectorRow>();

            var cells = dbConnector.Query<StationCellRow>(
                $@"SELECT InstanceID, StationSchemeID, ID, LinkIDList, Name
                   FROM {cellTable}
                   WHERE InstanceID = @instanceID AND StationSchemeID = @stationSchemeID
                   ORDER BY ID",
                new { instanceID, stationSchemeID }) ?? new List<StationCellRow>();

            var annotations = dbConnector.Query<StationAnnotationRow>(
                $@"SELECT *
                   FROM {annotationTable}
                   WHERE InstanceID = @instanceID AND StationSchemeID = @stationSchemeID
                   ORDER BY ID",
                new { instanceID, stationSchemeID }) ?? new List<StationAnnotationRow>();

            var nodeTransform = BuildCoordinateTransform(nodes);
            var nodeViews = nodes
                .Select(node =>
                {
                    var point = nodeTransform.MapPoint(node.X, node.Y);
                    return new
                    {
                        id = ToInvariantString(node.ID),
                        x = point.x,
                        y = point.y,
                        adjacentLineIDList = links
                            .Where(link => link.FromNodeID == node.ID || link.ToNodeID == node.ID)
                            .Select(link => ToInvariantString(link.ID))
                            .ToArray()
                    };
                })
                .ToArray();

            var nodeByID = nodes.ToDictionary(node => node.ID);
            var trackViews = links
                .Select(link =>
                {
                    if (!nodeByID.TryGetValue(link.FromNodeID, out var fromNode) ||
                        !nodeByID.TryGetValue(link.ToNodeID, out var toNode))
                    {
                        return null;
                    }

                    var fromPoint = nodeTransform.MapPoint(fromNode.X, fromNode.Y);
                    var toPoint = nodeTransform.MapPoint(toNode.X, toNode.Y);
                    return new
                    {
                        id = ToInvariantString(link.ID),
                        name = link.Name ?? string.Empty,
                        arrowDirection = link.ArrowDirection ?? string.Empty,
                        arrowType = link.ArrowType ?? string.Empty,
                        x1 = fromPoint.x,
                        y1 = fromPoint.y,
                        x2 = toPoint.x,
                        y2 = toPoint.y,
                        fromNodeID = ToInvariantString(link.FromNodeID),
                        toNodeID = ToInvariantString(link.ToNodeID)
                    };
                })
                .Where(track => track != null)
                .ToArray();

            var curveViews = curves
                .Select(curve =>
                {
                    var start = nodeTransform.MapPoint(curve.StartX, curve.StartY);
                    var end = nodeTransform.MapPoint(curve.EndX, curve.EndY);
                    var center = nodeTransform.MapPoint(curve.CenterX, curve.CenterY);
                    return new
                    {
                        id = curve.ID ?? string.Empty,
                        nodeID = FirstNonEmpty(curve.BindingNodeID, curve.VertexNodeID) ?? string.Empty,
                        tangentLinkID1 = FirstNonEmpty(curve.BindingLink1ID, curve.TangentLinkID1) ?? string.Empty,
                        tangentLinkID2 = FirstNonEmpty(curve.BindingLink2ID, curve.TangentLinkID2) ?? string.Empty,
                        radius = nodeTransform.MapLength(ParseDoubleOrDefault(curve.Radius)),
                        angle = curve.Angle,
                        tangentDistance = nodeTransform.MapLength(curve.TangentDistance),
                        start = new { x = start.x, y = start.y },
                        end = new { x = end.x, y = end.y },
                        center = new { x = center.x, y = center.y },
                        largeArcFlag = curve.LargeArcFlag == 1 ? 1 : 0,
                        sweepFlag = curve.SweepFlag == 1 ? 1 : 0
                    };
                })
                .ToArray();

            var signalViews = signals
                .Select(signal =>
                {
                    var bindingNodeID = ParseNullableInt(signal.BindingNodeID);
                    if (bindingNodeID == null || !nodeByID.TryGetValue(bindingNodeID.Value, out var bindingNode))
                    {
                        return null;
                    }

                    var point = nodeTransform.MapPoint(bindingNode.X, bindingNode.Y);
                    return new
                    {
                        id = signal.ID ?? string.Empty,
                        name = NormalizeEquipmentName(signal.Name, signal.ID ?? string.Empty),
                        type = signal.Type ?? string.Empty,
                        position = new { x = point.x, y = point.y },
                        direction = string.IsNullOrWhiteSpace(signal.Direction) ? "e" : signal.Direction,
                        bindingNodeID = ToInvariantString(bindingNode.ID)
                    };
                })
                .Where(signal => signal != null)
                .ToArray();

            var insulationJointViews = insulationJoints
                .Select(insulationJoint =>
                {
                    var bindingNodeID = ParseNullableInt(insulationJoint.BindingNodeID);
                    if (bindingNodeID == null || !nodeByID.TryGetValue(bindingNodeID.Value, out var bindingNode))
                    {
                        return null;
                    }

                    var point = nodeTransform.MapPoint(bindingNode.X, bindingNode.Y);
                    return new
                    {
                        id = insulationJoint.ID ?? string.Empty,
                        type = insulationJoint.Type ?? string.Empty,
                        position = new { x = point.x, y = point.y },
                        bindingNodeID = ToInvariantString(bindingNode.ID)
                    };
                })
                .Where(insulationJoint => insulationJoint != null)
                .ToArray();

            var bufferStopViews = bufferStops
                .Select(bufferStop =>
                {
                    var bindingNodeID = ParseNullableInt(bufferStop.BindingNodeID);
                    if (bindingNodeID == null || !nodeByID.TryGetValue(bindingNodeID.Value, out var bindingNode))
                    {
                        return null;
                    }

                    var point = nodeTransform.MapPoint(bindingNode.X, bindingNode.Y);
                    return new
                    {
                        id = bufferStop.ID ?? string.Empty,
                        type = NormalizeBufferStopType(bufferStop.Type),
                        direction = string.IsNullOrWhiteSpace(bufferStop.Direction) ? "right" : bufferStop.Direction,
                        position = new { x = point.x, y = point.y },
                        bindingNodeID = ToInvariantString(bindingNode.ID)
                    };
                })
                .Where(bufferStop => bufferStop != null)
                .ToArray();

            var platformViews = platforms
                .Select(platform =>
                {
                    var point = nodeTransform.MapPoint(platform.X, platform.Y);
                    return new
                    {
                        id = platform.ID ?? string.Empty,
                        name = NormalizeEquipmentName(platform.Name, platform.ID ?? string.Empty),
                        x = point.x,
                        y = point.y,
                        width = nodeTransform.MapLength(platform.Width),
                        height = nodeTransform.MapLength(platform.Height)
                    };
                })
                .ToArray();

            var switchBranchVectorLookup = switchBranchVectors
                .GroupBy(vector => vector.SwitchID ?? string.Empty)
                .ToDictionary(group => group.Key, group => group.OrderBy(vector => vector.Sequence).ToList());
            var switchViews = switches
                .Select(sw =>
                {
                    var bindingNodeID = ParseNullableInt(sw.BindingNodeID);
                    if (bindingNodeID == null || !nodeByID.TryGetValue(bindingNodeID.Value, out var bindingNode))
                    {
                        return null;
                    }

                    var point = nodeTransform.MapPoint(bindingNode.X, bindingNode.Y);
                    switchBranchVectorLookup.TryGetValue(sw.ID ?? string.Empty, out var branchVectors);

                    return new
                    {
                        id = sw.ID ?? string.Empty,
                        name = NormalizeEquipmentName(sw.Name, sw.ID ?? string.Empty),
                        type = sw.Type ?? "unknown",
                        position = new { x = point.x, y = point.y },
                        bindingNodeID = ToInvariantString(bindingNode.ID),
                        branchVectorList = (branchVectors ?? new List<SwitchBranchVectorRow>())
                            .Select(vector => new
                            {
                                x = nodeTransform.MapLength(vector.X),
                                y = nodeTransform.MapLength(vector.Y),
                                lineID = vector.BindingLinkID ?? string.Empty
                            })
                            .ToArray()
                    };
                })
                .Where(sw => sw != null)
                .ToArray();

            var cellViews = cells
                .Select(cell => new
                {
                    instanceID = cell.InstanceID ?? instanceID,
                    stationSchemeID = cell.StationSchemeID ?? stationSchemeID,
                    id = cell.ID ?? string.Empty,
                    linkIDList = cell.LinkIDList ?? string.Empty,
                    name = NormalizeEquipmentName(cell.Name, cell.ID ?? string.Empty)
                })
                .ToArray();

            var annotationViews = annotations
                .Select(annotation =>
                {
                    var point = nodeTransform.MapPoint(annotation.X, annotation.Y);
                    return new
                    {
                        id = annotation.ID ?? string.Empty,
                        text = annotation.Text ?? string.Empty,
                        position = new { x = point.x, y = point.y },
                        fontFamily = string.IsNullOrWhiteSpace(annotation.FontFamily) ? "Arial" : annotation.FontFamily,
                        fontSize = annotation.FontSize <= 0 ? 16 : annotation.FontSize,
                        fontWeight = string.IsNullOrWhiteSpace(annotation.FontWeight) ? "normal" : annotation.FontWeight,
                        fontStyle = string.IsNullOrWhiteSpace(annotation.FontStyle) ? "normal" : annotation.FontStyle,
                        angle = annotation.Angle,
                        textColor = string.IsNullOrWhiteSpace(annotation.TextColor) ? "#ffffff" : annotation.TextColor
                    };
                })
                .ToArray();

            var displayStyles = ParseStationSchemeDisplayStyles(
                LoadStationSchemeDisplayStyles(dbConnector, instanceID, stationSchemeID));
            var gridSettings = ParseStationSchemeGridSettings(
                LoadStationSchemeGridSettings(dbConnector, instanceID, stationSchemeID));
            var latestElementID = CalculateLatestElementID(
                nodes.Select(node => ToInvariantString(node.ID))
                    .Concat(links.Select(link => ToInvariantString(link.ID)))
                    .Concat(curves.Select(curve => curve.ID ?? string.Empty))
                    .Concat(signals.Select(signal => signal.ID ?? string.Empty))
                    .Concat(insulationJoints.Select(insulationJoint => insulationJoint.ID ?? string.Empty))
                    .Concat(bufferStops.Select(bufferStop => bufferStop.ID ?? string.Empty))
                    .Concat(platforms.Select(platform => platform.ID ?? string.Empty))
                    .Concat(switches.Select(sw => sw.ID ?? string.Empty))
                    .Concat(cells.Select(cell => cell.ID ?? string.Empty))
                    .Concat(annotations.Select(annotation => annotation.ID ?? string.Empty)));

            return JsonSerializer.Serialize(new
            {
                metadata = new
                {
                    latestElementID,
                    instanceID,
                    stationSchemeID,
                    coordinateTransform = nodeTransform.ToMetadata(),
                    displayStyles,
                    gridSettings
                },
                tracks = trackViews,
                curves = curveViews,
                nodes = nodeViews,
                signals = signalViews,
                insulationJoints = insulationJointViews,
                bufferStops = bufferStopViews,
                platforms = platformViews,
                switches = switchViews,
                cells = cellViews,
                annotations = annotationViews
            });
        }

        private StationLayoutSaveResult SaveStationLayoutJsonToDatabase(
            DBConnector dbConnector,
            string instanceID,
            string stationSchemeID,
            StationLayoutJson layout)
        {
            var nodeTable = QuoteIdentifier("node");
            var linkTable = QuoteIdentifier("link");
            var curveTable = QuoteIdentifier("curve");
            var signalTable = QuoteIdentifier("signal");
            var insulationJointTable = QuoteIdentifier("insulationjoint");
            var bufferStopTable = QuoteIdentifier("bufferstop");
            var platformTable = QuoteIdentifier("platform");
            var switchTable = QuoteIdentifier("switch");
            var cellTable = QuoteIdentifier("cell");
            var switchBranchVectorTable = QuoteIdentifier("switchbranchvector");
            var annotationTable = QuoteIdentifier("annotation");

            EnsureLinkSchema(dbConnector);
            EnsureCurveSchema(dbConnector);
            EnsureBufferStopSchema(dbConnector);
            EnsureNamedDeviceSchemas(dbConnector);
            EnsureCellSchema(dbConnector);

            var transform = StationLayoutPersistenceTransform.Identity;
            var nodeSaveContext = BuildNodeSaveContext(layout, transform);
            var linkSaveContext = BuildLinkSaveContext(layout, nodeSaveContext);

            dbConnector.BeginTransaction();
            try
            {
                EnsureStationSchemeExists(dbConnector, instanceID, stationSchemeID);
                PersistStationSchemeDisplayStyles(
                    dbConnector,
                    instanceID,
                    stationSchemeID,
                    layout.Metadata?.DisplayStyles);
                PersistStationSchemeGridSettings(
                    dbConnector,
                    instanceID,
                    stationSchemeID,
                    layout.Metadata?.GridSettings);

                DeleteStationLayoutTableRows(dbConnector, switchBranchVectorTable, instanceID, stationSchemeID);
                DeleteStationLayoutTableRows(dbConnector, switchTable, instanceID, stationSchemeID);
                DeleteStationLayoutTableRows(dbConnector, bufferStopTable, instanceID, stationSchemeID);
                DeleteStationLayoutTableRows(dbConnector, insulationJointTable, instanceID, stationSchemeID);
                DeleteStationLayoutTableRows(dbConnector, signalTable, instanceID, stationSchemeID);
                DeleteStationLayoutTableRows(dbConnector, platformTable, instanceID, stationSchemeID);
                DeleteStationLayoutTableRows(dbConnector, cellTable, instanceID, stationSchemeID);
                DeleteStationLayoutTableRows(dbConnector, annotationTable, instanceID, stationSchemeID);
                DeleteStationLayoutTableRows(dbConnector, curveTable, instanceID, stationSchemeID);
                DeleteStationLayoutTableRows(dbConnector, linkTable, instanceID, stationSchemeID);
                DeleteStationLayoutTableRows(dbConnector, nodeTable, instanceID, stationSchemeID);

                foreach (var node in nodeSaveContext.Nodes)
                {
                    EnsureInserted(
                        dbConnector.ExecuteNonQuery(
                            $@"INSERT INTO {nodeTable} (InstanceID, StationSchemeID, ID, X, Y)
                               VALUES (@InstanceID, @StationSchemeID, @ID, @X, @Y)",
                            new
                            {
                                InstanceID = instanceID,
                                StationSchemeID = stationSchemeID,
                                node.ID,
                                X = node.DatabaseX,
                                Y = node.DatabaseY
                            }),
                        "node");
                }

                foreach (var link in linkSaveContext.Links)
                {
                    EnsureInserted(
                        dbConnector.ExecuteNonQuery(
                            $@"INSERT INTO {linkTable} (InstanceID, StationSchemeID, ID, Name, FromNodeID, ToNodeID, ArrowDirection, ArrowType)
                               VALUES (@InstanceID, @StationSchemeID, @ID, @Name, @FromNodeID, @ToNodeID, @ArrowDirection, @ArrowType)",
                            new
                            {
                                InstanceID = instanceID,
                                StationSchemeID = stationSchemeID,
                                link.ID,
                                link.Name,
                                link.FromNodeID,
                                link.ToNodeID,
                                link.ArrowDirection,
                                link.ArrowType
                            }),
                        "link");
                }

                var platformCount = SavePlatforms(dbConnector, platformTable, instanceID, stationSchemeID, layout, transform);
                var annotationCount = SaveAnnotations(dbConnector, annotationTable, instanceID, stationSchemeID, layout, transform);
                var curveCount = SaveCurves(dbConnector, curveTable, instanceID, stationSchemeID, layout, transform, nodeSaveContext, linkSaveContext);
                var signalCount = SaveSignals(dbConnector, signalTable, instanceID, stationSchemeID, layout, nodeSaveContext);
                var insulationJointCount = SaveInsulationJoints(dbConnector, insulationJointTable, instanceID, stationSchemeID, layout, nodeSaveContext);
                var bufferStopCount = SaveBufferStops(dbConnector, bufferStopTable, instanceID, stationSchemeID, layout, nodeSaveContext);
                var cellCount = SaveCells(dbConnector, cellTable, instanceID, stationSchemeID, layout, linkSaveContext);
                var switchSaveResult = SaveSwitches(
                    dbConnector,
                    switchTable,
                    switchBranchVectorTable,
                    instanceID,
                    stationSchemeID,
                    layout,
                    transform,
                    nodeSaveContext,
                    linkSaveContext);

                dbConnector.Commit();
                return new StationLayoutSaveResult
                {
                    Message = "OK",
                    InstanceID = instanceID,
                    StationSchemeID = stationSchemeID,
                    NodeCount = nodeSaveContext.Nodes.Count,
                    LinkCount = linkSaveContext.Links.Count,
                    CurveCount = curveCount,
                    SignalCount = signalCount,
                    InsulationJointCount = insulationJointCount,
                    BufferStopCount = bufferStopCount,
                    PlatformCount = platformCount,
                    SwitchCount = switchSaveResult.SwitchCount,
                    SwitchBranchVectorCount = switchSaveResult.SwitchBranchVectorCount,
                    CellCount = cellCount,
                    AnnotationCount = annotationCount
                };
            }
            catch
            {
                dbConnector.Rollback();
                throw;
            }
        }

        private static void DeleteStationLayoutTableRows(DBConnector dbConnector, string tableName, string instanceID, string stationSchemeID)
        {
            dbConnector.ExecuteNonQuery(
                $@"DELETE FROM {tableName}
                   WHERE InstanceID = @instanceID AND StationSchemeID = @stationSchemeID",
                new { instanceID, stationSchemeID });
        }

        private (StationRouteRow? Route, IActionResult? ErrorResult) NormalizeStationRouteRequest(
            StationRouteRequest? request,
            bool allowMissingID)
        {
            if (request == null)
            {
                return (null, BadRequest("Request body is required."));
            }

            var instanceID = request.InstanceID?.Trim();
            if (string.IsNullOrWhiteSpace(instanceID))
            {
                return (null, BadRequest("instanceID is required when saving a station route."));
            }

            var stationSchemeID = request.StationSchemeID?.Trim();
            if (string.IsNullOrWhiteSpace(stationSchemeID))
            {
                return (null, BadRequest("stationSchemeID is required when saving a station route."));
            }

            var startNodeID = request.StartNodeID?.Trim();
            var endNodeID = request.EndNodeID?.Trim();
            if (string.IsNullOrWhiteSpace(startNodeID) || string.IsNullOrWhiteSpace(endNodeID))
            {
                return (null, BadRequest("StartNodeID and EndNodeID are required when saving a station route."));
            }

            var id = request.ID?.Trim();
            if (string.IsNullOrWhiteSpace(id))
            {
                if (!allowMissingID)
                {
                    return (null, BadRequest("ID is required when updating a station route."));
                }
            }

            return (new StationRouteRow
            {
                InstanceID = instanceID,
                StationSchemeID = stationSchemeID,
                ID = id,
                Type = NormalizeNullableRouteEndField(request.Type),
                Description = NormalizeNullableRouteEndField(request.Description),
                NodeList = NormalizeNullableRouteEndField(request.NodeList),
                LinkList = NormalizeNullableRouteEndField(request.LinkList),
                SwitchList = NormalizeNullableRouteEndField(request.SwitchList),
                CellList = NormalizeNullableRouteEndField(request.CellList),
                SignalList = NormalizeNullableRouteEndField(request.SignalList),
                AllowanceTags = NormalizeNullableRouteEndField(request.AllowanceTags),
                ForbiddenTags = NormalizeNullableRouteEndField(request.ForbiddenTags),
                StartNodeID = startNodeID,
                EndNodeID = endNodeID
            }, null);
        }

        private string GenerateStationRouteID(DBConnector dbConnector, string instanceID, string stationSchemeID)
        {
            for (var attempt = 0; attempt < 10; attempt++)
            {
                var candidate = _snowflakeIdGenerator.NextIdString();
                if (!StationRouteIDExists(dbConnector, instanceID, stationSchemeID, candidate))
                {
                    return candidate;
                }
            }

            throw new InvalidOperationException("Failed to generate a unique station route ID.");
        }

        private static List<StationRouteRow> LoadStationRoutes(
            DBConnector dbConnector,
            string instanceID,
            string stationSchemeID)
        {
            var tableName = QuoteIdentifier("stationroute");
            return dbConnector.Query<StationRouteRow>(
                $@"SELECT InstanceID, StationSchemeID, ID, {QuoteIdentifier("Type")} AS {QuoteIdentifier("Type")},
                          Description, NodeList, LinkList, SwitchList, CellList, SignalList,
                          AllowanceTags, ForbiddenTags, StartNodeID, EndNodeID
                   FROM {tableName}
                   WHERE InstanceID = @instanceID AND StationSchemeID = @stationSchemeID
                   ORDER BY ID",
                new { instanceID, stationSchemeID }) ?? new List<StationRouteRow>();
        }

        private static bool StationRouteIDExists(
            DBConnector dbConnector,
            string instanceID,
            string stationSchemeID,
            string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return false;
            }

            EnsureStationRouteSchema(dbConnector);
            var tableName = QuoteIdentifier("stationroute");
            return (dbConnector.Query<StationRouteRow>(
                $@"SELECT ID
                   FROM {tableName}
                   WHERE InstanceID = @instanceID
                     AND StationSchemeID = @stationSchemeID
                     AND ID = @id
                   LIMIT 1",
                new { instanceID, stationSchemeID, id }) ?? new List<StationRouteRow>()).Any();
        }

        private static bool StationNodeIDExists(
            DBConnector dbConnector,
            string instanceID,
            string stationSchemeID,
            string nodeID)
        {
            if (string.IsNullOrWhiteSpace(nodeID))
            {
                return false;
            }

            var nodeTable = QuoteIdentifier("node");
            return (dbConnector.Query<StationNodeRow>(
                $@"SELECT ID
                   FROM {nodeTable}
                   WHERE InstanceID = @instanceID
                     AND StationSchemeID = @stationSchemeID
                     AND ID = @nodeID
                   LIMIT 1",
                new { instanceID, stationSchemeID, nodeID }) ?? new List<StationNodeRow>()).Any();
        }

        private (StationRouteEndRow? RouteEnd, IActionResult? ErrorResult) NormalizeStationRouteEndRequest(
            StationRouteEndRequest? request,
            bool allowMissingID)
        {
            if (request == null)
            {
                return (null, BadRequest("Request body is required."));
            }

            var instanceID = request.InstanceID?.Trim();
            if (string.IsNullOrWhiteSpace(instanceID))
            {
                return (null, BadRequest("instanceID is required when saving a station route end."));
            }

            var stationSchemeID = request.StationSchemeID?.Trim();
            if (string.IsNullOrWhiteSpace(stationSchemeID))
            {
                return (null, BadRequest("stationSchemeID is required when saving a station route end."));
            }

            var bindingNodeID = request.BindingNodeID?.Trim();
            if (string.IsNullOrWhiteSpace(bindingNodeID))
            {
                return (null, BadRequest("BindingNodeID is required when saving a station route end."));
            }

            var id = request.ID?.Trim();
            if (string.IsNullOrWhiteSpace(id))
            {
                if (!allowMissingID)
                {
                    return (null, BadRequest("ID is required when updating a station route end."));
                }
            }

            return (new StationRouteEndRow
            {
                InstanceID = instanceID,
                StationSchemeID = stationSchemeID,
                ID = id,
                BindingNodeID = bindingNodeID,
                Type = NormalizeNullableRouteEndField(request.Type),
                SegmentTag = NormalizeNullableRouteEndField(request.SegmentTag),
                SidingTag = NormalizeNullableRouteEndField(request.SidingTag)
            }, null);
        }

        private string GenerateStationRouteEndID(DBConnector dbConnector, string instanceID, string stationSchemeID)
        {
            for (var attempt = 0; attempt < 10; attempt++)
            {
                var candidate = _snowflakeIdGenerator.NextIdString();
                if (!StationRouteEndIDExists(dbConnector, instanceID, stationSchemeID, candidate))
                {
                    return candidate;
                }
            }

            throw new InvalidOperationException("Failed to generate a unique station route end ID.");
        }

        private static string? NormalizeNullableRouteEndField(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static List<StationRouteEndRow> LoadStationRouteEnds(
            DBConnector dbConnector,
            string instanceID,
            string stationSchemeID)
        {
            var tableName = QuoteIdentifier("stationrouteend");
            return dbConnector.Query<StationRouteEndRow>(
                $@"SELECT InstanceID, StationSchemeID, ID, BindingNodeID, {QuoteIdentifier("Type")} AS {QuoteIdentifier("Type")}, SegmentTag, SidingTag
                   FROM {tableName}
                   WHERE InstanceID = @instanceID AND StationSchemeID = @stationSchemeID
                   ORDER BY ID",
                new { instanceID, stationSchemeID }) ?? new List<StationRouteEndRow>();
        }

        private static bool StationRouteEndIDExists(
            DBConnector dbConnector,
            string instanceID,
            string stationSchemeID,
            string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return false;
            }

            EnsureStationRouteEndSchema(dbConnector);
            var tableName = QuoteIdentifier("stationrouteend");
            return (dbConnector.Query<StationRouteEndRow>(
                $@"SELECT ID
                   FROM {tableName}
                   WHERE InstanceID = @instanceID
                     AND StationSchemeID = @stationSchemeID
                     AND ID = @id
                   LIMIT 1",
                new { instanceID, stationSchemeID, id }) ?? new List<StationRouteEndRow>()).Any();
        }

        private static bool StationRouteEndBindingNodeExists(
            DBConnector dbConnector,
            string instanceID,
            string stationSchemeID,
            string bindingNodeID)
        {
            if (string.IsNullOrWhiteSpace(bindingNodeID))
            {
                return false;
            }

            var nodeTable = QuoteIdentifier("node");
            return (dbConnector.Query<StationNodeRow>(
                $@"SELECT ID
                   FROM {nodeTable}
                   WHERE InstanceID = @instanceID
                     AND StationSchemeID = @stationSchemeID
                     AND ID = @bindingNodeID
                   LIMIT 1",
                new { instanceID, stationSchemeID, bindingNodeID }) ?? new List<StationNodeRow>()).Any();
        }

        private static StationRouteEndRow? FindStationRouteEndByBindingNodeID(
            DBConnector dbConnector,
            string instanceID,
            string stationSchemeID,
            string bindingNodeID)
        {
            if (string.IsNullOrWhiteSpace(bindingNodeID))
            {
                return null;
            }

            EnsureStationRouteEndSchema(dbConnector);
            var tableName = QuoteIdentifier("stationrouteend");
            return (dbConnector.Query<StationRouteEndRow>(
                $@"SELECT InstanceID, StationSchemeID, ID, BindingNodeID, {QuoteIdentifier("Type")} AS {QuoteIdentifier("Type")}, SegmentTag, SidingTag
                   FROM {tableName}
                   WHERE InstanceID = @instanceID
                     AND StationSchemeID = @stationSchemeID
                     AND BindingNodeID = @bindingNodeID
                   ORDER BY ID
                   LIMIT 1",
                new { instanceID, stationSchemeID, bindingNodeID }) ?? new List<StationRouteEndRow>()).FirstOrDefault();
        }

        private static void EnsureStationRouteDescription(DBConnector dbConnector, StationRouteRow route)
        {
            if (!string.IsNullOrWhiteSpace(route.Description) ||
                string.IsNullOrWhiteSpace(route.InstanceID) ||
                string.IsNullOrWhiteSpace(route.StationSchemeID) ||
                string.IsNullOrWhiteSpace(route.StartNodeID) ||
                string.IsNullOrWhiteSpace(route.EndNodeID) ||
                string.IsNullOrWhiteSpace(route.Type))
            {
                return;
            }

            route.Description = BuildStationRouteDescription(
                dbConnector,
                route.InstanceID,
                route.StationSchemeID,
                route.StartNodeID,
                route.EndNodeID,
                route.Type);
        }

        private static string BuildStationRouteDescription(
            DBConnector dbConnector,
            string instanceID,
            string stationSchemeID,
            string startNodeID,
            string endNodeID,
            string routeType)
        {
            EnsureStationRouteEndSchema(dbConnector);
            var startRouteEnd = FindStationRouteEndByBindingNodeID(
                dbConnector,
                instanceID,
                stationSchemeID,
                startNodeID);

            var endRouteEnd = FindStationRouteEndByBindingNodeID(
                dbConnector,
                instanceID,
                stationSchemeID,
                endNodeID);

            var startTag = BuildStationRouteEndTag(startRouteEnd);
            var endTag = BuildStationRouteEndTag(endRouteEnd);
            var routeTypeDescription = GetStationRouteTypeDescription(routeType);
            return $"自{startTag}往{endTag}的{routeTypeDescription}进路";
        }

        private static string BuildStationRouteEndTag(StationRouteEndRow? routeEnd)
        {
            if (routeEnd == null)
            {
                return MissingStationRouteEndTagPlaceholder;
            }

            var tag = string.Concat(
                routeEnd.SegmentTag?.Trim() ?? string.Empty,
                routeEnd.SidingTag?.Trim() ?? string.Empty).Trim();
            return string.IsNullOrWhiteSpace(tag) ? MissingStationRouteEndTagPlaceholder : tag;
        }

        private static string GetStationRouteTypeDescription(string routeType)
        {
            var normalizedRouteType = routeType.Trim();
            return normalizedRouteType.ToUpperInvariant() switch
            {
                "ARRIVAL" => "接车",
                "DEPARTURE" => "发车",
                "SHUNTING" => "调车",
                "LOCOMOTIVE" => "机车出入段",
                _ => normalizedRouteType
            };
        }

        private StationLayoutNodeSaveContext BuildNodeSaveContext(
            StationLayoutJson layout,
            StationLayoutPersistenceTransform transform)
        {
            var allocator = new IntegerIdAllocator();
            var context = new StationLayoutNodeSaveContext();

            foreach (var node in layout.Nodes ?? new List<StationLayoutNodeJson>())
            {
                var sourceID = string.IsNullOrWhiteSpace(node.ID)
                    ? $"node_{context.Nodes.Count}"
                    : node.ID.Trim();
                var dbID = allocator.Allocate(sourceID);
                var databasePoint = transform.UnmapPoint(node.X, node.Y);
                var entry = new StationLayoutNodeSaveEntry
                {
                    SourceID = sourceID,
                    ID = dbID,
                    DisplayX = node.X,
                    DisplayY = node.Y,
                    DatabaseX = databasePoint.x,
                    DatabaseY = databasePoint.y
                };

                context.Nodes.Add(entry);
                if (!context.NodeIDBySourceID.ContainsKey(sourceID))
                {
                    context.NodeIDBySourceID[sourceID] = dbID;
                }

                context.NodeIDByPointKey.TryAdd(BuildPointKey(node.X, node.Y), dbID);
            }

            context.Allocator = allocator;
            context.Transform = transform;
            return context;
        }

        private StationLayoutLinkSaveContext BuildLinkSaveContext(
            StationLayoutJson layout,
            StationLayoutNodeSaveContext nodeContext)
        {
            var allocator = new IntegerIdAllocator();
            var context = new StationLayoutLinkSaveContext();

            foreach (var track in layout.Tracks ?? new List<StationLayoutTrackJson>())
            {
                var sourceID = string.IsNullOrWhiteSpace(track.ID)
                    ? $"track_{context.Links.Count}"
                    : track.ID.Trim();
                var dbID = allocator.Allocate(sourceID);
                var fromNodeID = ResolveTrackEndpointNodeID(
                    nodeContext,
                    track.FromNodeID,
                    track.X1,
                    track.Y1);
                var toNodeID = ResolveTrackEndpointNodeID(
                    nodeContext,
                    track.ToNodeID,
                    track.X2,
                    track.Y2);

                context.Links.Add(new StationLayoutLinkSaveEntry
                {
                    SourceID = sourceID,
                    ID = dbID,
                    Name = track.Name ?? string.Empty,
                    ArrowDirection = NormalizeOptionalCode(track.ArrowDirection),
                    ArrowType = NormalizeOptionalCode(track.ArrowType),
                    FromNodeID = fromNodeID,
                    ToNodeID = toNodeID
                });

                if (!context.LinkIDBySourceID.ContainsKey(sourceID))
                {
                    context.LinkIDBySourceID[sourceID] = dbID;
                }
            }

            return context;
        }

        private static int ResolveTrackEndpointNodeID(
            StationLayoutNodeSaveContext nodeContext,
            string? sourceNodeID,
            double displayX,
            double displayY)
        {
            if (!string.IsNullOrWhiteSpace(sourceNodeID) &&
                nodeContext.NodeIDBySourceID.TryGetValue(sourceNodeID.Trim(), out var nodeID))
            {
                return nodeID;
            }

            return GetOrCreateNodeForPoint(nodeContext, displayX, displayY);
        }

        private static int GetOrCreateNodeForPoint(
            StationLayoutNodeSaveContext nodeContext,
            double displayX,
            double displayY)
        {
            var key = BuildPointKey(displayX, displayY);
            if (nodeContext.NodeIDByPointKey.TryGetValue(key, out var existingID))
            {
                return existingID;
            }

            var generatedSourceID = $"__generated_node_{nodeContext.Nodes.Count}";
            var dbID = nodeContext.Allocator.Allocate(generatedSourceID);
            var databasePoint = nodeContext.Transform.UnmapPoint(displayX, displayY);
            var entry = new StationLayoutNodeSaveEntry
            {
                SourceID = generatedSourceID,
                ID = dbID,
                DisplayX = displayX,
                DisplayY = displayY,
                DatabaseX = databasePoint.x,
                DatabaseY = databasePoint.y
            };

            nodeContext.Nodes.Add(entry);
            nodeContext.NodeIDBySourceID[generatedSourceID] = dbID;
            nodeContext.NodeIDByPointKey[key] = dbID;
            return dbID;
        }

        private int SavePlatforms(
            DBConnector dbConnector,
            string platformTable,
            string instanceID,
            string stationSchemeID,
            StationLayoutJson layout,
            StationLayoutPersistenceTransform transform)
        {
            var count = 0;
            foreach (var platform in layout.Platforms ?? new List<StationLayoutPlatformJson>())
            {
                var platformID = NormalizeStringID(platform.ID, "platform", count);
                var databasePoint = transform.UnmapPoint(platform.X, platform.Y);
                EnsureInserted(
                    dbConnector.ExecuteNonQuery(
                        $@"INSERT INTO {platformTable} (InstanceID, StationSchemeID, ID, Name, X, Y, Width, Height)
                           VALUES (@InstanceID, @StationSchemeID, @ID, @Name, @X, @Y, @Width, @Height)",
                        new
                        {
                            InstanceID = instanceID,
                            StationSchemeID = stationSchemeID,
                            ID = platformID,
                            Name = NormalizeEquipmentName(platform.Name, platformID),
                            X = databasePoint.x,
                            Y = databasePoint.y,
                            Width = transform.UnmapLength(platform.Width),
                            Height = transform.UnmapLength(platform.Height)
                        }),
                    "platform");
                count++;
            }

            return count;
        }

        private int SaveCells(
            DBConnector dbConnector,
            string cellTable,
            string instanceID,
            string stationSchemeID,
            StationLayoutJson layout,
            StationLayoutLinkSaveContext linkContext)
        {
            var count = 0;
            var usedCellIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var cell in layout.Cells ?? new List<StationLayoutCellJson>())
            {
                var cellID = string.IsNullOrWhiteSpace(cell.ID)
                    ? GenerateStationCellID(dbConnector, instanceID, stationSchemeID, usedCellIds)
                    : cell.ID.Trim();
                if (usedCellIds.Contains(cellID))
                {
                    cellID = GenerateStationCellID(dbConnector, instanceID, stationSchemeID, usedCellIds);
                }

                usedCellIds.Add(cellID);
                var linkIDList = NormalizeCellLinkIDList(cell.LinkIDList, linkContext);
                var cellName = NormalizeEquipmentName(cell.Name, cellID);
                cell.ID = cellID;
                cell.LinkIDList = linkIDList;
                cell.Name = cellName;
                EnsureInserted(
                    dbConnector.ExecuteNonQuery(
                        $@"INSERT INTO {cellTable} (InstanceID, StationSchemeID, ID, LinkIDList, Name)
                           VALUES (@InstanceID, @StationSchemeID, @ID, @LinkIDList, @Name)",
                        new
                        {
                            InstanceID = instanceID,
                            StationSchemeID = stationSchemeID,
                            ID = cellID,
                            LinkIDList = linkIDList,
                            Name = cellName
                        }),
                    "cell");
                count++;
            }

            return count;
        }

        private string GenerateStationCellID(
            DBConnector dbConnector,
            string instanceID,
            string stationSchemeID,
            ISet<string> reservedIds)
        {
            for (var attempt = 0; attempt < 10; attempt++)
            {
                var candidate = _snowflakeIdGenerator.NextIdString();
                if (!reservedIds.Contains(candidate) && !StationCellIDExists(dbConnector, instanceID, stationSchemeID, candidate))
                {
                    return candidate;
                }
            }

            throw new InvalidOperationException("Failed to generate a unique station cell ID.");
        }

        private static bool StationCellIDExists(
            DBConnector dbConnector,
            string instanceID,
            string stationSchemeID,
            string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return false;
            }

            EnsureCellSchema(dbConnector);
            var tableName = QuoteIdentifier("cell");
            return (dbConnector.Query<StationCellRow>(
                $@"SELECT ID
                   FROM {tableName}
                   WHERE InstanceID = @instanceID
                     AND StationSchemeID = @stationSchemeID
                     AND ID = @id
                   LIMIT 1",
                new { instanceID, stationSchemeID, id }) ?? new List<StationCellRow>()).Any();
        }

        private static string NormalizeCellLinkIDList(string? linkIDList, StationLayoutLinkSaveContext linkContext)
        {
            if (string.IsNullOrWhiteSpace(linkIDList))
            {
                return string.Empty;
            }

            var normalizedLinkIDs = ParseDelimitedIDList(linkIDList)
                .Select(id => ResolveBindingLinkID(linkContext, id))
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            return string.Join(",", normalizedLinkIDs);
        }

        private static int GetFirstMatchingRouteLinkIndex(
            StationCellRow cell,
            IReadOnlyDictionary<string, int> routeLinkIndexByID)
        {
            var firstIndex = int.MaxValue;
            foreach (var linkID in ParseDelimitedIDList(cell.LinkIDList))
            {
                if (routeLinkIndexByID.TryGetValue(linkID, out var linkIndex))
                {
                    firstIndex = Math.Min(firstIndex, linkIndex);
                }
            }

            return firstIndex;
        }

        private static List<string> ParseDelimitedIDList(string? idList)
        {
            if (string.IsNullOrWhiteSpace(idList))
            {
                return new List<string>();
            }

            return Regex.Split(idList.Trim(), @"[\s,，;；]+")
                .Select(id => id.Trim())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private int SaveAnnotations(
            DBConnector dbConnector,
            string annotationTable,
            string instanceID,
            string stationSchemeID,
            StationLayoutJson layout,
            StationLayoutPersistenceTransform transform)
        {
            var count = 0;
            foreach (var annotation in layout.Annotations ?? new List<StationLayoutAnnotationJson>())
            {
                var position = annotation.Position ?? new StationLayoutPositionJson();
                var databasePoint = transform.UnmapPoint(position.X, position.Y);
                EnsureInserted(
                    dbConnector.ExecuteNonQuery(
                        $@"INSERT INTO {annotationTable} (
                               InstanceID, StationSchemeID, ID, Text, X, Y,
                               FontFamily, FontSize, FontWeight, FontStyle, Angle, TextColor)
                           VALUES (
                               @InstanceID, @StationSchemeID, @ID, @Text, @X, @Y,
                               @FontFamily, @FontSize, @FontWeight, @FontStyle, @Angle, @TextColor)",
                        new
                        {
                            InstanceID = instanceID,
                            StationSchemeID = stationSchemeID,
                            ID = NormalizeStringID(annotation.ID, "annotation", count),
                            Text = annotation.Text ?? string.Empty,
                            X = databasePoint.x,
                            Y = databasePoint.y,
                            FontFamily = string.IsNullOrWhiteSpace(annotation.FontFamily) ? "Arial" : annotation.FontFamily,
                            FontSize = annotation.FontSize <= 0 ? 16 : annotation.FontSize,
                            FontWeight = string.IsNullOrWhiteSpace(annotation.FontWeight) ? "normal" : annotation.FontWeight,
                            FontStyle = string.IsNullOrWhiteSpace(annotation.FontStyle) ? "normal" : annotation.FontStyle,
                            Angle = annotation.Angle,
                            TextColor = string.IsNullOrWhiteSpace(annotation.TextColor) ? "#ffffff" : annotation.TextColor
                        }),
                    "annotation");
                count++;
            }

            return count;
        }

        private int SaveCurves(
            DBConnector dbConnector,
            string curveTable,
            string instanceID,
            string stationSchemeID,
            StationLayoutJson layout,
            StationLayoutPersistenceTransform transform,
            StationLayoutNodeSaveContext nodeContext,
            StationLayoutLinkSaveContext linkContext)
        {
            var count = 0;
            foreach (var curve in layout.Curves ?? new List<StationLayoutCurveJson>())
            {
                var start = curve.Start ?? new StationLayoutPositionJson();
                var end = curve.End ?? new StationLayoutPositionJson();
                var center = curve.Center ?? new StationLayoutPositionJson();
                var databaseStart = transform.UnmapPoint(start.X, start.Y);
                var databaseEnd = transform.UnmapPoint(end.X, end.Y);
                var databaseCenter = transform.UnmapPoint(center.X, center.Y);
                var curveID = NormalizeStringID(curve.ID, "curve", count);

                EnsureInserted(
                    dbConnector.ExecuteNonQuery(
                        $@"INSERT INTO {curveTable} (
                               InstanceID, StationSchemeID, ID, BindingNodeID, BindingLink1ID, BindingLink2ID,
                               Radius, Angle, TangentDistance, StartX, StartY, EndX, EndY,
                               CenterX, CenterY, LargeArcFlag, SweepFlag)
                           VALUES (
                               @InstanceID, @StationSchemeID, @ID, @BindingNodeID, @BindingLink1ID, @BindingLink2ID,
                               @Radius, @Angle, @TangentDistance, @StartX, @StartY, @EndX, @EndY,
                               @CenterX, @CenterY, @LargeArcFlag, @SweepFlag)",
                        new
                        {
                            InstanceID = instanceID,
                            StationSchemeID = stationSchemeID,
                            ID = curveID,
                            BindingNodeID = ResolveCurveNodeID(nodeContext, curve.NodeID),
                            BindingLink1ID = ResolveBindingLinkID(linkContext, curve.TangentLinkID1),
                            BindingLink2ID = ResolveBindingLinkID(linkContext, curve.TangentLinkID2),
                            Radius = ToRoundedInt(transform.UnmapLength(curve.Radius <= 0 ? 100 : curve.Radius)),
                            Angle = curve.Angle,
                            TangentDistance = transform.UnmapLength(curve.TangentDistance),
                            StartX = databaseStart.x,
                            StartY = databaseStart.y,
                            EndX = databaseEnd.x,
                            EndY = databaseEnd.y,
                            CenterX = databaseCenter.x,
                            CenterY = databaseCenter.y,
                            LargeArcFlag = curve.LargeArcFlag == 1 ? 1 : 0,
                            SweepFlag = curve.SweepFlag == 1 ? 1 : 0
                        }),
                    "curve");
                count++;
            }

            return count;
        }

        private int SaveSignals(
            DBConnector dbConnector,
            string signalTable,
            string instanceID,
            string stationSchemeID,
            StationLayoutJson layout,
            StationLayoutNodeSaveContext nodeContext)
        {
            var count = 0;
            foreach (var signal in layout.Signals ?? new List<StationLayoutSignalJson>())
            {
                var bindingNodeID = ResolveEquipmentBindingNodeID(nodeContext, signal.BindingNodeID, signal.Position);
                if (bindingNodeID == null)
                {
                    continue;
                }

                var signalID = NormalizeStringID(signal.ID, "signal", count);
                EnsureInserted(
                    dbConnector.ExecuteNonQuery(
                        $@"INSERT INTO {signalTable} (InstanceID, StationSchemeID, ID, Name, Type, Direction, BindingNodeID)
                           VALUES (@InstanceID, @StationSchemeID, @ID, @Name, @Type, @Direction, @BindingNodeID)",
                        new
                        {
                            InstanceID = instanceID,
                            StationSchemeID = stationSchemeID,
                            ID = signalID,
                            Name = NormalizeEquipmentName(signal.Name, signalID),
                            Type = string.IsNullOrWhiteSpace(signal.Type) ? "departure" : signal.Type,
                            Direction = string.IsNullOrWhiteSpace(signal.Direction) ? "e" : signal.Direction,
                            BindingNodeID = ToInvariantString(bindingNodeID.Value)
                        }),
                    "signal");
                count++;
            }

            return count;
        }

        private int SaveInsulationJoints(
            DBConnector dbConnector,
            string insulationJointTable,
            string instanceID,
            string stationSchemeID,
            StationLayoutJson layout,
            StationLayoutNodeSaveContext nodeContext)
        {
            var count = 0;
            foreach (var insulationJoint in layout.InsulationJoints ?? new List<StationLayoutInsulationJointJson>())
            {
                var bindingNodeID = ResolveEquipmentBindingNodeID(nodeContext, insulationJoint.BindingNodeID, insulationJoint.Position);
                if (bindingNodeID == null)
                {
                    continue;
                }

                EnsureInserted(
                    dbConnector.ExecuteNonQuery(
                        $@"INSERT INTO {insulationJointTable} (InstanceID, StationSchemeID, ID, Type, BindingNodeID)
                           VALUES (@InstanceID, @StationSchemeID, @ID, @Type, @BindingNodeID)",
                        new
                        {
                            InstanceID = instanceID,
                            StationSchemeID = stationSchemeID,
                            ID = NormalizeStringID(insulationJoint.ID, "insulationjoint", count),
                            Type = string.IsNullOrWhiteSpace(insulationJoint.Type) ? "normal" : insulationJoint.Type,
                            BindingNodeID = ToInvariantString(bindingNodeID.Value)
                        }),
                    "insulationjoint");
                count++;
            }

            return count;
        }

        private int SaveBufferStops(
            DBConnector dbConnector,
            string bufferStopTable,
            string instanceID,
            string stationSchemeID,
            StationLayoutJson layout,
            StationLayoutNodeSaveContext nodeContext)
        {
            var count = 0;
            foreach (var bufferStop in layout.BufferStops ?? new List<StationLayoutBufferStopJson>())
            {
                var bindingNodeID = ResolveEquipmentBindingNodeID(nodeContext, bufferStop.BindingNodeID, bufferStop.Position);
                if (bindingNodeID == null)
                {
                    continue;
                }

                EnsureInserted(
                    dbConnector.ExecuteNonQuery(
                        $@"INSERT INTO {bufferStopTable} (InstanceID, StationSchemeID, ID, {QuoteIdentifier("Type")}, Direction, BindingNodeID)
                           VALUES (@InstanceID, @StationSchemeID, @ID, @Type, @Direction, @BindingNodeID)",
                        new
                        {
                            InstanceID = instanceID,
                            StationSchemeID = stationSchemeID,
                            ID = NormalizeStringID(bufferStop.ID, "bufferstop", count),
                            Type = NormalizeBufferStopType(bufferStop.Type),
                            Direction = NormalizeBufferStopDirection(bufferStop.Direction),
                            BindingNodeID = ToInvariantString(bindingNodeID.Value)
                        }),
                    "bufferstop");
                count++;
            }

            return count;
        }

        private StationLayoutSwitchSaveResult SaveSwitches(
            DBConnector dbConnector,
            string switchTable,
            string switchBranchVectorTable,
            string instanceID,
            string stationSchemeID,
            StationLayoutJson layout,
            StationLayoutPersistenceTransform transform,
            StationLayoutNodeSaveContext nodeContext,
            StationLayoutLinkSaveContext linkContext)
        {
            var switchCount = 0;
            var branchVectorCount = 0;
            foreach (var sw in layout.Switches ?? new List<StationLayoutSwitchJson>())
            {
                var bindingNodeID = ResolveEquipmentBindingNodeID(nodeContext, sw.BindingNodeID, sw.Position);
                if (bindingNodeID == null)
                {
                    continue;
                }

                var switchID = NormalizeStringID(sw.ID, "switch", switchCount);
                EnsureInserted(
                    dbConnector.ExecuteNonQuery(
                        $@"INSERT INTO {switchTable} (InstanceID, StationSchemeID, ID, Name, Type, BindingNodeID)
                           VALUES (@InstanceID, @StationSchemeID, @ID, @Name, @Type, @BindingNodeID)",
                        new
                        {
                            InstanceID = instanceID,
                            StationSchemeID = stationSchemeID,
                            ID = switchID,
                            Name = NormalizeEquipmentName(sw.Name, switchID),
                            Type = string.IsNullOrWhiteSpace(sw.Type) ? "unknown" : sw.Type,
                            BindingNodeID = ToInvariantString(bindingNodeID.Value)
                        }),
                    "switch");

                var sequence = 0;
                foreach (var vector in sw.BranchVectorList ?? new List<StationLayoutSwitchBranchVectorJson>())
                {
                    var bindingLinkID = ResolveBindingLinkID(linkContext, vector.LineID);
                    EnsureInserted(
                        dbConnector.ExecuteNonQuery(
                            $@"INSERT INTO {switchBranchVectorTable} (InstanceID, StationSchemeID, SwitchID, Sequence, X, Y, BindingLinkID)
                               VALUES (@InstanceID, @StationSchemeID, @SwitchID, @Sequence, @X, @Y, @BindingLinkID)",
                            new
                            {
                                InstanceID = instanceID,
                                StationSchemeID = stationSchemeID,
                                SwitchID = switchID,
                                Sequence = sequence,
                                X = transform.UnmapLength(vector.X),
                                Y = transform.UnmapLength(vector.Y),
                                BindingLinkID = bindingLinkID
                            }),
                        "switchbranchvector");
                    sequence++;
                    branchVectorCount++;
                }

                switchCount++;
            }

            return new StationLayoutSwitchSaveResult
            {
                SwitchCount = switchCount,
                SwitchBranchVectorCount = branchVectorCount
            };
        }

        private static int? ResolveEquipmentBindingNodeID(
            StationLayoutNodeSaveContext nodeContext,
            string? sourceNodeID,
            StationLayoutPositionJson? position)
        {
            if (!string.IsNullOrWhiteSpace(sourceNodeID) &&
                nodeContext.NodeIDBySourceID.TryGetValue(sourceNodeID.Trim(), out var nodeID))
            {
                return nodeID;
            }

            if (position == null)
            {
                return null;
            }

            var key = BuildPointKey(position.X, position.Y);
            if (nodeContext.NodeIDByPointKey.TryGetValue(key, out var pointNodeID))
            {
                return pointNodeID;
            }

            var nearestNode = nodeContext.Nodes
                .Select(node => new
                {
                    node.ID,
                    Distance = Math.Sqrt(
                        Math.Pow(node.DisplayX - position.X, 2) +
                        Math.Pow(node.DisplayY - position.Y, 2))
                })
                .OrderBy(item => item.Distance)
                .FirstOrDefault();
            return nearestNode?.Distance <= 2 ? nearestNode.ID : null;
        }

        private static string ResolveBindingLinkID(StationLayoutLinkSaveContext linkContext, string? sourceLineID)
        {
            if (string.IsNullOrWhiteSpace(sourceLineID))
            {
                return string.Empty;
            }

            return linkContext.LinkIDBySourceID.TryGetValue(sourceLineID.Trim(), out var linkID)
                ? ToInvariantString(linkID)
                : sourceLineID.Trim();
        }

        private static string ResolveCurveNodeID(StationLayoutNodeSaveContext nodeContext, string? sourceNodeID)
        {
            if (string.IsNullOrWhiteSpace(sourceNodeID))
            {
                return string.Empty;
            }

            return nodeContext.NodeIDBySourceID.TryGetValue(sourceNodeID.Trim(), out var nodeID)
                ? ToInvariantString(nodeID)
                : sourceNodeID.Trim();
        }

        private static string NormalizeStringID(string? id, string prefix, int index)
        {
            return string.IsNullOrWhiteSpace(id)
                ? $"{prefix}_{index}"
                : id.Trim();
        }

        private static void EnsureInserted(int result, string tableName)
        {
            if (result <= 0)
            {
                throw new InvalidOperationException($"Failed to insert {tableName} row.");
            }
        }

        private IActionResult? ValidateCapacityInstanceOwnershipOrFail(DBConnector dbConnector, string instanceID)
        {
            var instance = (dbConnector.Query<CapacityInstance>(
                "SELECT * FROM capacityinstance WHERE ID = @instanceID",
                new { instanceID }) ?? new List<CapacityInstance>()).FirstOrDefault();
            if (instance == null)
            {
                return NotFound("Instance not found.");
            }

            if (IsCurrentUserAdmin())
            {
                return null;
            }

            var username = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(username))
            {
                return Unauthorized("Invalid user context.");
            }

            if (!string.Equals(instance.Owner, username, StringComparison.Ordinal))
            {
                _logger.LogWarning(
                    "User {Username} is not the owner of capacity instance {InstanceID}.",
                    username,
                    instanceID);
                return Unauthorized("Instance not owned by user.");
            }

            return null;
        }

        private bool IsCurrentUserAdmin()
        {
            var username = User?.Identity?.Name;
            if (string.Equals(username, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var role = User?.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            return string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase);
        }

        private static StationLayoutCoordinateTransform BuildCoordinateTransform(IReadOnlyCollection<StationNodeRow> nodes)
        {
            // Station layouts are stored in editor coordinates; auto-fitting on load changes the user's saved scale.
            return StationLayoutCoordinateTransform.Identity;
        }

        private static string QuoteIdentifier(string identifier)
        {
            var escapedIdentifier = identifier.Replace("\"", "\"\"", StringComparison.Ordinal);
            if (DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName))
            {
                return $"`{identifier.Replace("`", "``", StringComparison.Ordinal)}`";
            }

            return $"\"{escapedIdentifier}\"";
        }

        private static void EnsureStationSchemeSchema(DBConnector dbConnector)
        {
            var tableName = QuoteIdentifier("stationscheme");
            if (!TableExists(dbConnector, "stationscheme"))
            {
                if (DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName))
                {
                    dbConnector.ExecuteNonQuery(
                        $@"CREATE TABLE IF NOT EXISTS {tableName} (
                            {QuoteIdentifier("InstanceID")} VARCHAR(50) NULL,
                            {QuoteIdentifier("ID")} VARCHAR(50) NULL,
                            {QuoteIdentifier("Name")} VARCHAR(100) NULL,
                            {QuoteIdentifier("DisplayStyles")} TEXT NULL,
                            {QuoteIdentifier("GridSettings")} TEXT NULL
                        )");
                }
                else
                {
                    dbConnector.ExecuteNonQuery(
                        $@"CREATE TABLE IF NOT EXISTS {tableName} (
                            {QuoteIdentifier("InstanceID")} TEXT NULL,
                            {QuoteIdentifier("ID")} TEXT NULL,
                            {QuoteIdentifier("Name")} TEXT NULL,
                            {QuoteIdentifier("DisplayStyles")} TEXT NULL,
                            {QuoteIdentifier("GridSettings")} TEXT NULL
                        )");
                }

                return;
            }

            var existingColumns = GetColumnNames(dbConnector, "stationscheme");
            var requiredColumns = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName)
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["InstanceID"] = "VARCHAR(50) NULL",
                    ["ID"] = "VARCHAR(50) NULL",
                    ["Name"] = "VARCHAR(100) NULL",
                    ["DisplayStyles"] = "TEXT NULL",
                    ["GridSettings"] = "TEXT NULL"
                }
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["InstanceID"] = "TEXT NULL",
                    ["ID"] = "TEXT NULL",
                    ["Name"] = "TEXT NULL",
                    ["DisplayStyles"] = "TEXT NULL",
                    ["GridSettings"] = "TEXT NULL"
                };

            foreach (var column in requiredColumns)
            {
                if (existingColumns.Any(existing => string.Equals(existing, column.Key, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                dbConnector.ExecuteNonQuery(
                    $@"ALTER TABLE {tableName} ADD COLUMN {QuoteIdentifier(column.Key)} {column.Value}");
            }
        }

        private static void EnsureNamedDeviceSchemas(DBConnector dbConnector)
        {
            foreach (var tableName in NamedDeviceTables)
            {
                EnsureNamedDeviceSchema(dbConnector, tableName);
            }
        }

        private static void EnsureCellSchema(DBConnector dbConnector)
        {
            var tableName = QuoteIdentifier("cell");
            if (!TableExists(dbConnector, "cell"))
            {
                if (DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName))
                {
                    dbConnector.ExecuteNonQuery(
                        $@"CREATE TABLE IF NOT EXISTS {tableName} (
                            {QuoteIdentifier("InstanceID")} VARCHAR(50) NULL,
                            {QuoteIdentifier("StationSchemeID")} VARCHAR(50) NULL,
                            {QuoteIdentifier("ID")} VARCHAR(50) NULL,
                            {QuoteIdentifier("LinkIDList")} VARCHAR(255) NULL,
                            {QuoteIdentifier("Name")} VARCHAR(100) NULL
                        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci");
                }
                else
                {
                    dbConnector.ExecuteNonQuery(
                        $@"CREATE TABLE IF NOT EXISTS {tableName} (
                            {QuoteIdentifier("InstanceID")} TEXT NULL,
                            {QuoteIdentifier("StationSchemeID")} TEXT NULL,
                            {QuoteIdentifier("ID")} TEXT NULL,
                            {QuoteIdentifier("LinkIDList")} TEXT NULL,
                            {QuoteIdentifier("Name")} TEXT NULL
                        )");
                }

                return;
            }

            var existingColumns = GetColumnNames(dbConnector, "cell");
            var requiredColumns = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName)
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["InstanceID"] = "VARCHAR(50) NULL",
                    ["StationSchemeID"] = "VARCHAR(50) NULL",
                    ["ID"] = "VARCHAR(50) NULL",
                    ["LinkIDList"] = "VARCHAR(255) NULL",
                    ["Name"] = "VARCHAR(100) NULL"
                }
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["InstanceID"] = "TEXT NULL",
                    ["StationSchemeID"] = "TEXT NULL",
                    ["ID"] = "TEXT NULL",
                    ["LinkIDList"] = "TEXT NULL",
                    ["Name"] = "TEXT NULL"
                };

            foreach (var column in requiredColumns)
            {
                if (existingColumns.Any(existing => string.Equals(existing, column.Key, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                dbConnector.ExecuteNonQuery(
                    $@"ALTER TABLE {tableName} ADD COLUMN {QuoteIdentifier(column.Key)} {column.Value}");
            }
        }

        private static void EnsureLinkSchema(DBConnector dbConnector)
        {
            EnsureNullableNameColumn(dbConnector, "link");
            var columnNames = GetColumnNames(dbConnector, "link");
            if (columnNames.Count == 0)
            {
                return;
            }

            var textType = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName)
                ? "VARCHAR(10) NULL"
                : "TEXT NULL";
            var tableName = QuoteIdentifier("link");
            foreach (var columnName in new[] { "ArrowDirection", "ArrowType" })
            {
                if (columnNames.Any(column => string.Equals(column, columnName, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                dbConnector.ExecuteNonQuery(
                    $@"ALTER TABLE {tableName} ADD COLUMN {QuoteIdentifier(columnName)} {textType}");
            }
        }

        private static void EnsureCurveSchema(DBConnector dbConnector)
        {
            var tableName = QuoteIdentifier("curve");
            if (!TableExists(dbConnector, "curve"))
            {
                if (DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName))
                {
                    dbConnector.ExecuteNonQuery(
                        $@"CREATE TABLE IF NOT EXISTS {tableName} (
                            {QuoteIdentifier("InstanceID")} VARCHAR(50) NULL,
                            {QuoteIdentifier("StationSchemeID")} VARCHAR(50) NULL,
                            {QuoteIdentifier("ID")} VARCHAR(50) NULL,
                            {QuoteIdentifier("BindingNodeID")} VARCHAR(50) NULL,
                            {QuoteIdentifier("BindingLink1ID")} VARCHAR(50) NULL,
                            {QuoteIdentifier("BindingLink2ID")} VARCHAR(50) NULL,
                            {QuoteIdentifier("Radius")} INT NULL,
                            {QuoteIdentifier("Angle")} DOUBLE NULL,
                            {QuoteIdentifier("TangentDistance")} DOUBLE NULL,
                            {QuoteIdentifier("StartX")} DOUBLE NULL,
                            {QuoteIdentifier("StartY")} DOUBLE NULL,
                            {QuoteIdentifier("EndX")} DOUBLE NULL,
                            {QuoteIdentifier("EndY")} DOUBLE NULL,
                            {QuoteIdentifier("CenterX")} DOUBLE NULL,
                            {QuoteIdentifier("CenterY")} DOUBLE NULL,
                            {QuoteIdentifier("LargeArcFlag")} TINYINT NULL,
                            {QuoteIdentifier("SweepFlag")} TINYINT NULL
                        )");
                }
                else
                {
                    dbConnector.ExecuteNonQuery(
                        $@"CREATE TABLE IF NOT EXISTS {tableName} (
                            {QuoteIdentifier("InstanceID")} TEXT NULL,
                            {QuoteIdentifier("StationSchemeID")} TEXT NULL,
                            {QuoteIdentifier("ID")} TEXT NULL,
                            {QuoteIdentifier("BindingNodeID")} TEXT NULL,
                            {QuoteIdentifier("BindingLink1ID")} TEXT NULL,
                            {QuoteIdentifier("BindingLink2ID")} TEXT NULL,
                            {QuoteIdentifier("Radius")} INTEGER NULL,
                            {QuoteIdentifier("Angle")} REAL NULL,
                            {QuoteIdentifier("TangentDistance")} REAL NULL,
                            {QuoteIdentifier("StartX")} REAL NULL,
                            {QuoteIdentifier("StartY")} REAL NULL,
                            {QuoteIdentifier("EndX")} REAL NULL,
                            {QuoteIdentifier("EndY")} REAL NULL,
                            {QuoteIdentifier("CenterX")} REAL NULL,
                            {QuoteIdentifier("CenterY")} REAL NULL,
                            {QuoteIdentifier("LargeArcFlag")} INTEGER NULL,
                            {QuoteIdentifier("SweepFlag")} INTEGER NULL
                        )");
                }

                return;
            }

            var existingColumns = GetColumnNames(dbConnector, "curve");
            var textType = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName) ? "VARCHAR(50) NULL" : "TEXT NULL";
            var numberType = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName) ? "DOUBLE NULL" : "REAL NULL";
            var flagType = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName) ? "TINYINT NULL" : "INTEGER NULL";
            var radiusType = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName) ? "INT NULL" : "INTEGER NULL";
            var requiredColumns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["InstanceID"] = textType,
                ["StationSchemeID"] = textType,
                ["ID"] = textType,
                ["BindingNodeID"] = textType,
                ["BindingLink1ID"] = textType,
                ["BindingLink2ID"] = textType,
                ["Radius"] = radiusType,
                ["Angle"] = numberType,
                ["TangentDistance"] = numberType,
                ["StartX"] = numberType,
                ["StartY"] = numberType,
                ["EndX"] = numberType,
                ["EndY"] = numberType,
                ["CenterX"] = numberType,
                ["CenterY"] = numberType,
                ["LargeArcFlag"] = flagType,
                ["SweepFlag"] = flagType
            };

            foreach (var column in requiredColumns)
            {
                if (existingColumns.Any(existing => string.Equals(existing, column.Key, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                dbConnector.ExecuteNonQuery(
                    $@"ALTER TABLE {tableName} ADD COLUMN {QuoteIdentifier(column.Key)} {column.Value}");
            }

            if (DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName))
            {
                dbConnector.ExecuteNonQuery(
                    $@"ALTER TABLE {tableName} MODIFY COLUMN {QuoteIdentifier("Radius")} INT NULL");
            }
        }

        private static void EnsureBufferStopSchema(DBConnector dbConnector)
        {
            var tableName = QuoteIdentifier("bufferstop");
            if (!TableExists(dbConnector, "bufferstop"))
            {
                if (DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName))
                {
                    dbConnector.ExecuteNonQuery(
                        $@"CREATE TABLE IF NOT EXISTS {tableName} (
                            {QuoteIdentifier("InstanceID")} VARCHAR(50) NULL,
                            {QuoteIdentifier("StationSchemeID")} VARCHAR(50) NULL,
                            {QuoteIdentifier("ID")} VARCHAR(50) NULL,
                            {QuoteIdentifier("Type")} VARCHAR(20) NULL,
                            {QuoteIdentifier("Direction")} VARCHAR(20) NULL,
                            {QuoteIdentifier("BindingNodeID")} VARCHAR(50) NULL
                        )");
                }
                else
                {
                    dbConnector.ExecuteNonQuery(
                        $@"CREATE TABLE IF NOT EXISTS {tableName} (
                            {QuoteIdentifier("InstanceID")} TEXT NULL,
                            {QuoteIdentifier("StationSchemeID")} TEXT NULL,
                            {QuoteIdentifier("ID")} TEXT NULL,
                            {QuoteIdentifier("Type")} TEXT NULL,
                            {QuoteIdentifier("Direction")} TEXT NULL,
                            {QuoteIdentifier("BindingNodeID")} TEXT NULL
                        )");
                }

                return;
            }

            var existingColumns = GetColumnNames(dbConnector, "bufferstop");
            var textType = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName) ? "VARCHAR(50) NULL" : "TEXT NULL";
            var directionType = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName) ? "VARCHAR(20) NULL" : "TEXT NULL";
            var requiredColumns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["InstanceID"] = textType,
                ["StationSchemeID"] = textType,
                ["ID"] = textType,
                ["Type"] = directionType,
                ["Direction"] = directionType,
                ["BindingNodeID"] = textType
            };

            foreach (var column in requiredColumns)
            {
                if (existingColumns.Any(existing => string.Equals(existing, column.Key, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                dbConnector.ExecuteNonQuery(
                    $@"ALTER TABLE {tableName} ADD COLUMN {QuoteIdentifier(column.Key)} {column.Value}");
            }
        }

        private static void EnsureStationRouteSchema(DBConnector dbConnector)
        {
            var tableName = QuoteIdentifier("stationroute");
            if (!TableExists(dbConnector, "stationroute"))
            {
                if (DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName))
                {
                    dbConnector.ExecuteNonQuery(
                        $@"CREATE TABLE IF NOT EXISTS {tableName} (
                            {QuoteIdentifier("InstanceID")} VARCHAR(50) NULL,
                            {QuoteIdentifier("StationSchemeID")} VARCHAR(50) NULL,
                            {QuoteIdentifier("ID")} VARCHAR(50) NULL,
                            {QuoteIdentifier("Type")} VARCHAR(50) NULL,
                            {QuoteIdentifier("Description")} LONGTEXT NULL,
                            {QuoteIdentifier("NodeList")} LONGTEXT NULL,
                            {QuoteIdentifier("LinkList")} LONGTEXT NULL,
                            {QuoteIdentifier("SwitchList")} LONGTEXT NULL,
                            {QuoteIdentifier("CellList")} LONGTEXT NULL,
                            {QuoteIdentifier("SignalList")} LONGTEXT NULL,
                            {QuoteIdentifier("AllowanceTags")} LONGTEXT NULL,
                            {QuoteIdentifier("ForbiddenTags")} LONGTEXT NULL,
                            {QuoteIdentifier("StartNodeID")} VARCHAR(50) NULL,
                            {QuoteIdentifier("EndNodeID")} VARCHAR(50) NULL
                        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci");
                }
                else
                {
                    dbConnector.ExecuteNonQuery(
                        $@"CREATE TABLE IF NOT EXISTS {tableName} (
                            {QuoteIdentifier("InstanceID")} TEXT NULL,
                            {QuoteIdentifier("StationSchemeID")} TEXT NULL,
                            {QuoteIdentifier("ID")} TEXT NULL,
                            {QuoteIdentifier("Type")} TEXT NULL,
                            {QuoteIdentifier("Description")} TEXT NULL,
                            {QuoteIdentifier("NodeList")} TEXT NULL,
                            {QuoteIdentifier("LinkList")} TEXT NULL,
                            {QuoteIdentifier("SwitchList")} TEXT NULL,
                            {QuoteIdentifier("CellList")} TEXT NULL,
                            {QuoteIdentifier("SignalList")} TEXT NULL,
                            {QuoteIdentifier("AllowanceTags")} TEXT NULL,
                            {QuoteIdentifier("ForbiddenTags")} TEXT NULL,
                            {QuoteIdentifier("StartNodeID")} TEXT NULL,
                            {QuoteIdentifier("EndNodeID")} TEXT NULL
                        )");
                }

                return;
            }

            var existingColumns = GetColumnNames(dbConnector, "stationroute");
            var shortTextType = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName) ? "VARCHAR(50) NULL" : "TEXT NULL";
            var longTextType = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName) ? "LONGTEXT NULL" : "TEXT NULL";
            var requiredColumns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["InstanceID"] = shortTextType,
                ["StationSchemeID"] = shortTextType,
                ["ID"] = shortTextType,
                ["Type"] = shortTextType,
                ["Description"] = longTextType,
                ["NodeList"] = longTextType,
                ["LinkList"] = longTextType,
                ["SwitchList"] = longTextType,
                ["CellList"] = longTextType,
                ["SignalList"] = longTextType,
                ["AllowanceTags"] = longTextType,
                ["ForbiddenTags"] = longTextType,
                ["StartNodeID"] = shortTextType,
                ["EndNodeID"] = shortTextType
            };

            foreach (var column in requiredColumns)
            {
                if (existingColumns.Any(existing => string.Equals(existing, column.Key, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                dbConnector.ExecuteNonQuery(
                    $@"ALTER TABLE {tableName} ADD COLUMN {QuoteIdentifier(column.Key)} {column.Value}");
            }
        }

        private static void EnsureStationRouteEndSchema(DBConnector dbConnector)
        {
            var tableName = QuoteIdentifier("stationrouteend");
            if (!TableExists(dbConnector, "stationrouteend"))
            {
                if (DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName))
                {
                    dbConnector.ExecuteNonQuery(
                        $@"CREATE TABLE IF NOT EXISTS {tableName} (
                            {QuoteIdentifier("InstanceID")} VARCHAR(50) NULL,
                            {QuoteIdentifier("StationSchemeID")} VARCHAR(50) NULL,
                            {QuoteIdentifier("ID")} VARCHAR(50) NULL,
                            {QuoteIdentifier("BindingNodeID")} VARCHAR(50) NULL,
                            {QuoteIdentifier("Type")} VARCHAR(50) NULL,
                            {QuoteIdentifier("SegmentTag")} VARCHAR(50) NULL,
                            {QuoteIdentifier("SidingTag")} VARCHAR(50) NULL
                        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci");
                }
                else
                {
                    dbConnector.ExecuteNonQuery(
                        $@"CREATE TABLE IF NOT EXISTS {tableName} (
                            {QuoteIdentifier("InstanceID")} TEXT NULL,
                            {QuoteIdentifier("StationSchemeID")} TEXT NULL,
                            {QuoteIdentifier("ID")} TEXT NULL,
                            {QuoteIdentifier("BindingNodeID")} TEXT NULL,
                            {QuoteIdentifier("Type")} TEXT NULL,
                            {QuoteIdentifier("SegmentTag")} TEXT NULL,
                            {QuoteIdentifier("SidingTag")} TEXT NULL
                        )");
                }

                return;
            }

            var existingColumns = GetColumnNames(dbConnector, "stationrouteend");
            var textType = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName) ? "VARCHAR(50) NULL" : "TEXT NULL";
            var requiredColumns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["InstanceID"] = textType,
                ["StationSchemeID"] = textType,
                ["ID"] = textType,
                ["BindingNodeID"] = textType,
                ["Type"] = textType,
                ["SegmentTag"] = textType,
                ["SidingTag"] = textType
            };

            foreach (var column in requiredColumns)
            {
                if (existingColumns.Any(existing => string.Equals(existing, column.Key, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                dbConnector.ExecuteNonQuery(
                    $@"ALTER TABLE {tableName} ADD COLUMN {QuoteIdentifier(column.Key)} {column.Value}");
            }
        }

        private static void EnsureNullableNameColumn(DBConnector dbConnector, string tableName)
        {
            var columnNames = GetColumnNames(dbConnector, tableName);
            if (columnNames.Count == 0 ||
                columnNames.Any(column => string.Equals(column, "Name", StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            var nameColumnType = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName)
                ? "VARCHAR(100) NULL"
                : "TEXT NULL";
            dbConnector.ExecuteNonQuery(
                $@"ALTER TABLE {QuoteIdentifier(tableName)} ADD COLUMN {QuoteIdentifier("Name")} {nameColumnType}");
        }

        private static void EnsureNamedDeviceSchema(DBConnector dbConnector, string tableName)
        {
            var columnNames = GetColumnNames(dbConnector, tableName);
            if (columnNames.Count == 0)
            {
                return;
            }

            var hasName = columnNames.Any(column => string.Equals(column, "Name", StringComparison.OrdinalIgnoreCase));
            var hasID = columnNames.Any(column => string.Equals(column, "ID", StringComparison.OrdinalIgnoreCase));
            var quotedTableName = QuoteIdentifier(tableName);
            var nameColumn = QuoteIdentifier("Name");
            var idColumn = QuoteIdentifier("ID");

            if (!hasName)
            {
                var nameColumnType = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName)
                    ? "VARCHAR(100) NULL"
                    : "TEXT NULL";
                dbConnector.ExecuteNonQuery($@"ALTER TABLE {quotedTableName} ADD COLUMN {nameColumn} {nameColumnType}");
            }

            if (!hasID)
            {
                return;
            }

            dbConnector.ExecuteNonQuery(
                $@"UPDATE {quotedTableName}
                   SET {nameColumn} = {idColumn}
                   WHERE ({nameColumn} IS NULL OR TRIM({nameColumn}) = '')
                     AND {idColumn} IS NOT NULL");
        }

        private static List<string> GetColumnNames(DBConnector dbConnector, string tableName)
        {
            if (!TableExists(dbConnector, tableName))
            {
                return new List<string>();
            }

            if (DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName))
            {
                return (dbConnector.Query<DatabaseNameLookupRow>(
                    @"SELECT COLUMN_NAME AS Name
                      FROM INFORMATION_SCHEMA.COLUMNS
                      WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @tableName",
                    new { tableName }) ?? new List<DatabaseNameLookupRow>())
                    .Select(column => column.Name ?? string.Empty)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .ToList();
            }

            return (dbConnector.Query<DatabaseNameLookupRow>(
                $@"PRAGMA table_info({QuoteIdentifier(tableName)})") ?? new List<DatabaseNameLookupRow>())
                .Select(column => column.Name ?? string.Empty)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToList();
        }

        private static bool TableExists(DBConnector dbConnector, string tableName)
        {
            List<DatabaseNameLookupRow>? rows;
            if (DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName))
            {
                rows = dbConnector.Query<DatabaseNameLookupRow>(
                    @"SELECT TABLE_NAME AS Name
                      FROM INFORMATION_SCHEMA.TABLES
                      WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @tableName
                      LIMIT 1",
                    new { tableName });
            }
            else
            {
                rows = dbConnector.Query<DatabaseNameLookupRow>(
                    @"SELECT name AS Name
                      FROM sqlite_master
                      WHERE type = 'table' AND lower(name) = lower(@tableName)
                      LIMIT 1",
                    new { tableName });
            }

            return rows?.Any() == true;
        }

        private static string NormalizeEquipmentName(string? name, string id)
        {
            return string.IsNullOrWhiteSpace(name) ? id : name.Trim();
        }

        private static string? NormalizeOptionalCode(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim().ToUpperInvariant();
        }

        private static string NormalizeBufferStopDirection(string? value)
        {
            var normalized = value?.Trim().ToLowerInvariant();
            return normalized switch
            {
                "left" or "l" or "左" or "向左" => "left",
                "right" or "r" or "右" or "向右" => "right",
                _ => "right"
            };
        }

        private static string NormalizeBufferStopType(string? value)
        {
            var normalized = value?.Trim().ToLowerInvariant();
            return normalized switch
            {
                "ext" or "e" or "extend" or "extended" or "extension" or "延伸" or "延申" => "ext",
                _ => "normal"
            };
        }

        private static string ToInvariantString(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static int ToRoundedInt(double value)
        {
            return Convert.ToInt32(Math.Round(value, MidpointRounding.AwayFromZero));
        }

        private static string? FirstNonEmpty(params string?[] values)
        {
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return null;
        }

        private static double ParseDoubleOrDefault(object? value)
        {
            if (value == null)
            {
                return 0;
            }

            return double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : 0;
        }

        private static int? ParseNullableInt(string? value)
        {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }

            return null;
        }

        private static int CalculateLatestElementID(IEnumerable<string> ids)
        {
            var maxID = -1;
            foreach (var id in ids)
            {
                if (int.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                {
                    maxID = Math.Max(maxID, parsed);
                }
            }

            return maxID + 1;
        }

        private static string BuildPointKey(double x, double y)
        {
            return $"{Math.Round(x, 3).ToString(CultureInfo.InvariantCulture)}|{Math.Round(y, 3).ToString(CultureInfo.InvariantCulture)}";
        }

        [HttpPost(Name = "ExtractDwgFile")]
        [Authorize(Roles = "Admin")]
        [RequestSizeLimit(MaxDwgFileSize)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxDwgFileSize)]
        public async Task<IActionResult> ExtractDwgFile(IFormFile? file, [FromForm] string? layerName = "0")
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("Please upload a non-empty DWG file.");
            }

            if (file.Length > MaxDwgFileSize)
            {
                return BadRequest($"File size exceeds the {MaxDwgFileSize / (1024 * 1024)} MB limit.");
            }

            if (!string.Equals(Path.GetExtension(file.FileName), ".dwg", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("Only DWG files are supported.");
            }

            if (string.IsNullOrWhiteSpace(layerName))
            {
                layerName = "0";
            }

            if (!LayerNameRegex.IsMatch(layerName))
            {
                return BadRequest("Invalid layer name.");
            }

            if (!TryResolveStationLayoutFilePath(out var filePath, out var errorMessage))
            {
                return StatusCode(500, errorMessage);
            }

            try
            {
                await using var stream = file.OpenReadStream();
                var document = DwgReader.Read(stream, new DwgReaderConfiguration
                {
                    Failsafe = false
                }, notification: null);

                if (!document.Layers.Any(layer => string.Equals(layer.Name, layerName, StringComparison.OrdinalIgnoreCase)))
                {
                    var availableLayers = document.Layers
                        .Select(layer => layer.Name)
                        .Order(StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                    _logger.LogWarning(
                        "Requested DWG layer not found. Requested: {LayerName}, Available: {Layers}",
                        layerName,
                        string.Join(", ", availableLayers));
                    return BadRequest("The specified layer was not found in the DWG file.");
                }

                var extractor = new AutoCADLayerLineExtractor();
                var segments = extractor.ExtractFile(document, layerName);
                if (segments == null || segments.Count == 0)
                {
                    return BadRequest($"No line segments were extracted from layer '{layerName}'.");
                }

                var stationLayoutJson = BuildStationLayoutJsonFromSegments(segments);
                var directory = Path.GetDirectoryName(filePath);
                if (string.IsNullOrWhiteSpace(directory))
                {
                    return StatusCode(500, "Unable to resolve the StationLayout.json directory.");
                }

                Directory.CreateDirectory(directory);
                await System.IO.File.WriteAllTextAsync(filePath, stationLayoutJson, new UTF8Encoding(false));

                _logger.LogInformation(
                    "Extracted {SegmentCount} DWG line segments from layer {LayerName} and saved to {FilePath}",
                    segments.Count,
                    layerName,
                    filePath);

                using var stationLayoutDocument = JsonDocument.Parse(stationLayoutJson);
                return Ok(new
                {
                    message = "OK",
                    segmentCount = segments.Count,
                    layout = stationLayoutDocument.RootElement.Clone()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to extract DWG file {FileName}.", file.FileName);
                return StatusCode(500, "Failed to extract DWG file.");
            }
        }

        private static string BuildStationLayoutJsonFromSegments(IReadOnlyList<AutoCADLayerLineExtractor.LineSegmentRecord> segments)
        {
            const double canvasWidth = 1920;
            const double canvasHeight = 1080;
            const double padding = 80;

            var minX = segments.Min(segment => Math.Min(segment.StartX, segment.EndX));
            var maxX = segments.Max(segment => Math.Max(segment.StartX, segment.EndX));
            var minY = segments.Min(segment => Math.Min(segment.StartY, segment.EndY));
            var maxY = segments.Max(segment => Math.Max(segment.StartY, segment.EndY));
            var sourceWidth = Math.Max(maxX - minX, 1);
            var sourceHeight = Math.Max(maxY - minY, 1);
            var scale = Math.Min((canvasWidth - padding * 2) / sourceWidth, (canvasHeight - padding * 2) / sourceHeight);

            double MapX(double x) => Math.Round(padding + (x - minX) * scale, 3);
            double MapY(double y) => Math.Round(padding + (maxY - y) * scale, 3);

            var tracks = segments.Select((segment, index) => new
            {
                id = index.ToString(),
                x1 = MapX(segment.StartX),
                x2 = MapX(segment.EndX),
                y1 = MapY(segment.StartY),
                y2 = MapY(segment.EndY),
                fromNodeID = string.Empty,
                toNodeID = string.Empty
            });

            return JsonSerializer.Serialize(new
            {
                metadata = new
                {
                    latestElementID = segments.Count,
                    gridSettings = new { showGrid = true, spacing = 20, originX = 0, originY = 0 }
                },
                tracks,
                curves = Array.Empty<object>(),
                nodes = Array.Empty<object>(),
                signals = Array.Empty<object>(),
                insulationJoints = Array.Empty<object>(),
                bufferStops = Array.Empty<object>(),
                platforms = Array.Empty<object>(),
                switches = Array.Empty<object>(),
                cells = Array.Empty<object>(),
                annotations = Array.Empty<object>()
            }, new JsonSerializerOptions
            {
                WriteIndented = true
            });

        }
    }
}
