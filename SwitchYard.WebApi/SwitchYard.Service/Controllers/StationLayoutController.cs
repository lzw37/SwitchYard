using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ACadSharp.IO;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using SwitchYard.Capacity;
using SwitchYard.Service.Utils;
using SwitchYard.Service.Services;

namespace SwitchYard.Service.Controllers
{
    [ApiController]
    [Route("[controller]/[action]")]
    [Authorize]
    public partial class StationLayoutController : ControllerBase
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
            "stationroutetime",
            "calculationparameterset",
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
        
        [HttpPost(Name = "RepairJson")]
        public async Task<IActionResult> RepairJson(
            [FromBody] SwitchYard.StationLayout.StationLayoutSaveRequest request,
            [FromServices] SwitchYard.StationLayout.IStationLayoutService service,
            [FromQuery] string? instanceID = null)
        {
            try
            {
                request.InstanceID = FirstNonEmpty(instanceID, request.InstanceID);
                return Ok(await service.RepairJsonAsync(User, request, HttpContext.RequestAborted));
            }
            catch (SwitchYard.StationLayout.StationLayoutValidationException ex) { return BadRequest(ex.Message); }
            catch (SwitchYard.StationLayout.StationLayoutNotFoundException ex) { return NotFound(ex.Message); }
            catch (SwitchYard.StationLayout.StationLayoutUnauthenticatedException ex) { return Unauthorized(ex.Message); }
            catch (SwitchYard.StationLayout.StationLayoutForbiddenException ex) { return StatusCode(403, ex.Message); }
        }

        [HttpPost(Name ="SaveJson")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> SaveJson(
            [FromBody] StationLayoutSaveRequest? request,
            [FromServices] SwitchYard.StationLayout.IStationLayoutService service,
            [FromQuery] string? instanceID = null,
            [FromQuery] string? stationSchemeID = null)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Json))
            {
                return BadRequest("Request body must contain a non-empty json field.");
            }

            try
            {
                return Ok(await service.SaveJsonAsync(User, new SwitchYard.StationLayout.StationLayoutSaveRequest
                {
                    Json = request.Json,
                    InstanceID = FirstNonEmpty(instanceID, request.InstanceID),
                    StationSchemeID = FirstNonEmpty(stationSchemeID, request.StationSchemeID),
                    ExpectedRevision = request.ExpectedRevision
                }, HttpContext.RequestAborted));
            }
            catch (SwitchYard.StationLayout.StationLayoutValidationException ex) { return BadRequest(ex.Message); }
            catch (SwitchYard.StationLayout.StationLayoutNotFoundException ex) { return NotFound(ex.Message); }
            catch (SwitchYard.StationLayout.StationLayoutUnauthenticatedException ex) { return Unauthorized(ex.Message); }
            catch (SwitchYard.StationLayout.StationLayoutForbiddenException ex) { return StatusCode(403, ex.Message); }
            catch (SwitchYard.StationLayout.StationLayoutConflictException ex) { return Conflict(ex.Message); }
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
        public async Task<IActionResult> GetJson(
            [FromServices] SwitchYard.StationLayout.IStationLayoutService service,
            [FromQuery] string? instanceID = null, [FromQuery] string? stationSchemeID = null)
        {
            try
            {
                var normalizedInstanceID = instanceID?.Trim();
                if (string.IsNullOrWhiteSpace(normalizedInstanceID))
                {
                    return GetJsonFromFileFallback();
                }

                var result = await service.GetJsonAsync(User, normalizedInstanceID, stationSchemeID, HttpContext.RequestAborted);
                return Content(result.Document.ToJson(), "application/json", Encoding.UTF8);
            }
            catch (SwitchYard.StationLayout.StationLayoutNotFoundException ex) { return NotFound(ex.Message); }
            catch (SwitchYard.StationLayout.StationLayoutUnauthenticatedException ex) { return Unauthorized(ex.Message); }
            catch (SwitchYard.StationLayout.StationLayoutForbiddenException ex) { return StatusCode(403, ex.Message); }
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
                dbConnector.BeginTransaction();
                try
                {
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
                                .ToDictionary(item => item.NodeID, item => item.index, StringComparer.Ordinal);
                            var routeNodeIDSet = routeNodeIndexByID.Keys.ToHashSet(StringComparer.Ordinal);
                            var routeLinkIndexByID = route.Links
                                .Select((link, index) => new { LinkID = ToInvariantString(link.ID), index })
                                .GroupBy(item => item.LinkID, StringComparer.Ordinal)
                                .ToDictionary(group => group.Key, group => group.Min(item => item.index), StringComparer.Ordinal);
                            var routeSwitches = switches
                                .Where(sw => !string.IsNullOrWhiteSpace(sw.BindingNodeID) && routeNodeIDSet.Contains(sw.BindingNodeID.Trim()))
                                .OrderBy(sw => routeNodeIndexByID.TryGetValue(sw.BindingNodeID!.Trim(), out var index) ? index : int.MaxValue)
                                .ThenBy(sw => sw.ID ?? string.Empty, StringComparer.Ordinal)
                                .ToList();
                            var routeSignals = signals
                                .Where(signal => !string.IsNullOrWhiteSpace(signal.BindingNodeID) && routeNodeIDSet.Contains(signal.BindingNodeID.Trim()))
                                .OrderBy(signal => routeNodeIndexByID.TryGetValue(signal.BindingNodeID!.Trim(), out var index) ? index : int.MaxValue)
                                .ThenBy(signal => signal.ID ?? string.Empty, StringComparer.Ordinal)
                                .ToList();
                            var routeCells = cells
                                .Select(cell => new
                                {
                                    Cell = cell,
                                    FirstLinkIndex = GetFirstMatchingRouteLinkIndex(cell, routeLinkIndexByID)
                                })
                                .Where(item => item.FirstLinkIndex < int.MaxValue)
                                .OrderBy(item => item.FirstLinkIndex)
                                .ThenBy(item => item.Cell.ID ?? string.Empty, StringComparer.Ordinal)
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
                        .ThenBy(result => result.Direction, StringComparer.Ordinal)
                        .ThenBy(result => string.Join(",", result.NodeIds))
                        .ToList()
                    };

                    return Ok(response);
                }
                finally { dbConnector.Rollback(); }
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
            [FromQuery] string? stationSchemeID = null,
            [FromQuery] string? calculationParameterSetID = null)
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

                var normalizedStationSchemeID = ResolveStationSchemeID(dbConnector, normalizedInstanceID, stationSchemeID);
                if (string.IsNullOrWhiteSpace(normalizedStationSchemeID))
                {
                    return Ok(new List<StationRouteRow>());
                }

                return Ok(LoadStationRoutes(dbConnector, normalizedInstanceID, normalizedStationSchemeID, calculationParameterSetID));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load station routes.");
                return StatusCode(500, "Failed to load station routes.");
            }
        }

        [HttpPost(Name = "GenerateStationRouteInterruptCells")]
        public IActionResult GenerateStationRouteInterruptCells([FromBody] StationRouteInterruptCellGenerateRequest? request)
        {
            try
            {
                var normalizedInstanceID = request?.InstanceID?.Trim();
                if (string.IsNullOrWhiteSpace(normalizedInstanceID))
                {
                    return BadRequest("instanceID is required when generating station route interrupt cells.");
                }

                var dbConnector = GetCapacityDbConnector();
                var authResult = ValidateCapacityInstanceOwnershipOrFail(dbConnector, normalizedInstanceID);
                if (authResult != null)
                {
                    return authResult;
                }

                var normalizedStationSchemeID = ResolveStationSchemeID(
                    dbConnector,
                    normalizedInstanceID,
                    request?.StationSchemeID);
                if (string.IsNullOrWhiteSpace(normalizedStationSchemeID))
                {
                    return BadRequest("stationSchemeID is required when generating station route interrupt cells.");
                }

                if (!StationSchemeIDExists(dbConnector, normalizedInstanceID, normalizedStationSchemeID))
                {
                    return NotFound("Station scheme not found.");
                }

                var routes = LoadStationRoutes(dbConnector, normalizedInstanceID, normalizedStationSchemeID);
                var universeCellIDs = BuildStationRouteInterruptCellUniverse(routes);

                dbConnector.BeginTransaction();
                try
                {
                    foreach (var route in routes)
                    {
                        route.InterruptCellList = BuildStationRouteInterruptCellList(route, routes, universeCellIDs);
                        UpdateStationRouteInterruptCellList(
                            dbConnector,
                            normalizedInstanceID,
                            normalizedStationSchemeID,
                            route.ID,
                            route.InterruptCellList);
                    }

                    dbConnector.Commit();
                }
                catch
                {
                    dbConnector.Rollback();
                    throw;
                }

                return Ok(new
                {
                    instanceID = normalizedInstanceID,
                    stationSchemeID = normalizedStationSchemeID,
                    updated = routes.Count,
                    routes
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate station route interrupt cells.");
                return StatusCode(500, "Failed to generate station route interrupt cells.");
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

                var dwellingError = ValidateStationDwellingRoute(dbConnector, route);
                if (dwellingError != null) return dwellingError;

                EnsureStationRouteDescription(dbConnector, route);

                if (StationRouteIDExists(dbConnector, route.InstanceID!, route.StationSchemeID!, route.ID!))
                {
                    return BadRequest("Station route ID already exists in the selected station scheme.");
                }

                var tableName = QuoteIdentifier("stationroute");
                var result = dbConnector.ExecuteNonQuery(
                    $@"INSERT INTO {tableName} (
                           InstanceID, StationSchemeID, ID, {QuoteIdentifier("Type")}, Description,
                           NodeList, LinkList, SwitchList, CellList, InterruptCellList, SignalList,
                           AllowanceTags, ForbiddenTags, TrainTypes, StartNodeID, EndNodeID)
                       VALUES (
                           @InstanceID, @StationSchemeID, @ID, @Type, @Description,
                           @NodeList, @LinkList, @SwitchList, @CellList, @InterruptCellList, @SignalList,
                           @AllowanceTags, @ForbiddenTags, @TrainTypes, @StartNodeID, @EndNodeID)",
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

        [HttpGet(Name = "GetStationRouteTimes")]
        public IActionResult GetStationRouteTimes(
            [FromQuery] string? instanceID = null,
            [FromQuery] string? stationSchemeID = null,
            [FromQuery] string? routeID = null,
            [FromQuery] string? trainTypeID = null,
            [FromQuery] string? calculationParameterSetID = null)
        {
            try
            {
                var dbConnector = GetCapacityDbConnector();
                var normalizedInstanceID = instanceID?.Trim();
                var normalizedStationSchemeID = stationSchemeID?.Trim();
                var normalizedRouteID = routeID?.Trim();
                var normalizedTrainTypeID = trainTypeID?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(normalizedInstanceID) ||
                    string.IsNullOrWhiteSpace(normalizedStationSchemeID) ||
                    string.IsNullOrWhiteSpace(normalizedRouteID))
                {
                    return BadRequest("instanceID, stationSchemeID and routeID are required.");
                }

                var authResult = ValidateCapacityInstanceOwnershipOrFail(dbConnector, normalizedInstanceID);
                if (authResult != null)
                {
                    return authResult;
                }

                var route = FindStationRouteByID(
                    dbConnector,
                    normalizedInstanceID,
                    normalizedStationSchemeID,
                    normalizedRouteID);
                var routeTimes = LoadStationRouteTimes(
                    dbConnector,
                    normalizedInstanceID,
                    normalizedStationSchemeID,
                    normalizedRouteID,
                    normalizedTrainTypeID, calculationParameterSetID);
                return Ok(OrderStationRouteTimesByCellList(
                    routeTimes,
                    route?.CellList,
                    route?.InterruptCellList,
                    routeTimes.Count > 0));
            }
            catch (ArgumentException ex) { return BadRequest(ex.Message); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load station route times.");
                return StatusCode(500, "Failed to load station route times.");
            }
        }

        [HttpPost(Name = "CreateStationRouteTimes")]
        public IActionResult CreateStationRouteTimes([FromBody] StationRouteTimeCreateRequest? request)
        {
            try
            {
                var dbConnector = GetCapacityDbConnector();
                var instanceID = request?.InstanceID?.Trim();
                var stationSchemeID = request?.StationSchemeID?.Trim();
                var routeID = request?.RouteID?.Trim();
                var trainTypeID = request?.TrainTypeID?.Trim() ?? string.Empty;
                var calculationParameterSetID = request?.CalculationParameterSetID;
                if (string.IsNullOrWhiteSpace(instanceID) ||
                    string.IsNullOrWhiteSpace(stationSchemeID) ||
                    string.IsNullOrWhiteSpace(routeID))
                {
                    return BadRequest("instanceID, stationSchemeID and routeID are required.");
                }

                var authResult = ValidateCapacityInstanceOwnershipOrFail(dbConnector, instanceID);
                if (authResult != null)
                {
                    return authResult;
                }

                var route = FindStationRouteByID(dbConnector, instanceID, stationSchemeID, routeID);
                if (route == null)
                {
                    return NotFound("Station route not found.");
                }

                var cellIDs = BuildStationRouteTimeCellIDs(route);
                if (cellIDs.Count == 0)
                {
                    return BadRequest("The selected route does not contain any cells.");
                }

                ReplaceStationRouteTimes(
                    dbConnector,
                    instanceID,
                    stationSchemeID,
                    routeID,
                    trainTypeID,
                    cellIDs.Select(cellID => new StationRouteTimeRow
                    {
                        CellID = cellID,
                        StartOccupationShift = 0,
                        EndOccupationShift = 0,
                    }).ToList(), calculationParameterSetID);

                return Ok(OrderStationRouteTimesByCellList(
                    LoadStationRouteTimes(dbConnector, instanceID, stationSchemeID, routeID, trainTypeID, calculationParameterSetID),
                    route.CellList,
                    route.InterruptCellList,
                    true));
            }
            catch (ArgumentException ex) { return BadRequest(ex.Message); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create station route times.");
                return StatusCode(500, "Failed to create station route times.");
            }
        }

        [HttpPut(Name = "SaveStationRouteTimes")]
        public IActionResult SaveStationRouteTimes([FromBody] StationRouteTimeSaveRequest? request)
        {
            try
            {
                var dbConnector = GetCapacityDbConnector();
                var instanceID = request?.InstanceID?.Trim();
                var stationSchemeID = request?.StationSchemeID?.Trim();
                var routeID = request?.RouteID?.Trim();
                var trainTypeID = request?.TrainTypeID?.Trim() ?? string.Empty;
                var calculationParameterSetID = request?.CalculationParameterSetID;
                if (string.IsNullOrWhiteSpace(instanceID) ||
                    string.IsNullOrWhiteSpace(stationSchemeID) ||
                    string.IsNullOrWhiteSpace(routeID))
                {
                    return BadRequest("instanceID, stationSchemeID and routeID are required.");
                }

                var authResult = ValidateCapacityInstanceOwnershipOrFail(dbConnector, instanceID);
                if (authResult != null)
                {
                    return authResult;
                }

                var route = FindStationRouteByID(dbConnector, instanceID, stationSchemeID, routeID);
                if (route == null)
                {
                    return NotFound("Station route not found.");
                }

                var routeTimePlan = BuildStationRouteTimeCellPlan(route.CellList, route.InterruptCellList);
                var routeCellIDs = routeTimePlan.CellIDs;
                if (routeCellIDs.Count == 0)
                {
                    return BadRequest("The selected route does not contain any cells.");
                }

                var routeCellIDSet = routeCellIDs.ToHashSet(StringComparer.Ordinal);
                var rows = (request?.Times ?? new List<StationRouteTimeRow>())
                    .Where(row => row != null)
                    .Select(row => new StationRouteTimeRow
                    {
                        CellID = row.CellID?.Trim(),
                        StartOccupationShift = row.StartOccupationShift,
                        EndOccupationShift = row.EndOccupationShift,
                    })
                    .Where(row => !string.IsNullOrWhiteSpace(row.CellID))
                    .ToList();
                if (rows.Count == 0)
                {
                    return BadRequest("At least one station route time row is required.");
                }

                var invalidCellID = rows
                    .Select(row => row.CellID ?? string.Empty)
                    .FirstOrDefault(cellID => !routeCellIDSet.Contains(cellID));
                if (!string.IsNullOrEmpty(invalidCellID))
                {
                    return BadRequest($"CellID '{invalidCellID}' is not part of the selected route occupancy cell list.");
                }

                ApplyStationRouteInterruptCellTimeDefaults(rows, routeTimePlan.InterruptCellIDSet);
                ReplaceStationRouteTimes(dbConnector, instanceID, stationSchemeID, routeID, trainTypeID, rows, calculationParameterSetID);

                return Ok(OrderStationRouteTimesByCellList(
                    LoadStationRouteTimes(dbConnector, instanceID, stationSchemeID, routeID, trainTypeID, calculationParameterSetID),
                    route.CellList,
                    route.InterruptCellList,
                    true));
            }
            catch (ArgumentException ex) { return BadRequest(ex.Message); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save station route times.");
                return StatusCode(500, "Failed to save station route times.");
            }
        }

        [HttpPut(Name = "BatchSetStationRouteTimes")]
        public IActionResult BatchSetStationRouteTimes([FromBody] StationRouteTimeBatchSetRequest? request)
        {
            try
            {
                var dbConnector = GetCapacityDbConnector();
                var instanceID = request?.InstanceID?.Trim();
                var stationSchemeID = request?.StationSchemeID?.Trim();
                var trainTypeID = request?.TrainTypeID?.Trim() ?? string.Empty;
                var calculationParameterSetID = request?.CalculationParameterSetID;
                if (string.IsNullOrWhiteSpace(instanceID) ||
                    string.IsNullOrWhiteSpace(stationSchemeID))
                {
                    return BadRequest("instanceID and stationSchemeID are required.");
                }

                var settingsByType = new Dictionary<string, StationRouteTimeBatchSetItem>(StringComparer.Ordinal);
                foreach (var setting in request?.Settings ?? new List<StationRouteTimeBatchSetItem>())
                {
                    var type = setting.Type?.Trim();
                    if (string.IsNullOrWhiteSpace(type))
                    {
                        continue;
                    }

                    settingsByType[type] = new StationRouteTimeBatchSetItem
                    {
                        Type = type,
                        RouteIDs = setting.RouteIDs?
                            .Select(routeID => routeID?.Trim())
                            .Where(routeID => !string.IsNullOrWhiteSpace(routeID))
                            .Select(routeID => routeID!)
                            .Distinct(StringComparer.Ordinal)
                            .ToList(),
                        StartOccupationShift = setting.StartOccupationShift,
                        EndOccupationShift = setting.EndOccupationShift,
                    };
                }
                if (settingsByType.Count == 0)
                {
                    return BadRequest("At least one route type setting is required.");
                }

                var authResult = ValidateCapacityInstanceOwnershipOrFail(dbConnector, instanceID);
                if (authResult != null)
                {
                    return authResult;
                }

                var routes = LoadStationRoutes(dbConnector, instanceID, stationSchemeID);
                var updatedRouteCount = 0;
                var updatedRowCount = 0;
                foreach (var route in routes)
                {
                    var routeID = route.ID?.Trim();
                    var routeType = route.Type?.Trim() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(routeID) ||
                        string.IsNullOrWhiteSpace(routeType) ||
                        !settingsByType.TryGetValue(routeType, out var setting))
                    {
                        continue;
                    }
                    if (setting.RouteIDs != null &&
                        !setting.RouteIDs.Contains(routeID, StringComparer.Ordinal))
                    {
                        continue;
                    }

                    var cellIDs = BuildStationRouteTimeCellIDs(route);
                    if (cellIDs.Count == 0)
                    {
                        continue;
                    }

                    ReplaceStationRouteTimes(
                        dbConnector,
                        instanceID,
                        stationSchemeID,
                        routeID,
                        trainTypeID,
                        cellIDs.Select(cellID => new StationRouteTimeRow
                        {
                            CellID = cellID,
                            StartOccupationShift = setting.StartOccupationShift,
                            EndOccupationShift = setting.EndOccupationShift,
                        }).ToList(), calculationParameterSetID);
                    updatedRouteCount++;
                    updatedRowCount += cellIDs.Count;
                }

                return Ok(new { routeCount = updatedRouteCount, rowCount = updatedRowCount });
            }
            catch (ArgumentException ex) { return BadRequest(ex.Message); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to batch set station route times.");
                return StatusCode(500, "Failed to batch set station route times.");
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

                if (!StationRouteIDExists(dbConnector, route.InstanceID!, route.StationSchemeID!, originalID!))
                {
                    return NotFound("Station route not found.");
                }

                if (!StationNodeIDExists(dbConnector, route.InstanceID!, route.StationSchemeID!, route.StartNodeID!) ||
                    !StationNodeIDExists(dbConnector, route.InstanceID!, route.StationSchemeID!, route.EndNodeID!))
                {
                    return BadRequest("StartNodeID and EndNodeID must reference existing nodes in the selected station scheme.");
                }

                if (string.IsNullOrWhiteSpace(request?.TrainTypes))
                {
                    var existing = FindStationRouteByID(dbConnector, route.InstanceID!, route.StationSchemeID!, originalID!);
                    if (StationRouteTrainTypes.AllowedFor(existing?.Type).SequenceEqual(StationRouteTrainTypes.AllowedFor(route.Type)))
                        route.TrainTypes = StationRouteTrainTypes.Normalize(existing?.TrainTypes, route.Type);
                }

                var dwellingError = ValidateStationDwellingRoute(dbConnector, route);
                if (dwellingError != null) return dwellingError;

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
                           InterruptCellList = @InterruptCellList,
                           SignalList = @SignalList,
                           AllowanceTags = @AllowanceTags,
                           ForbiddenTags = @ForbiddenTags,
                           TrainTypes = @TrainTypes,
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
                        route.InterruptCellList,
                        route.SignalList,
                        route.AllowanceTags,
                        route.ForbiddenTags,
                        route.TrainTypes,
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
                        route.Type!,
                        route.LinkList)
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
            DBConnector? dbConnector = null;
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

                dbConnector = GetCapacityDbConnector();
                var authResult = ValidateCapacityInstanceOwnershipOrFail(dbConnector, normalizedInstanceID);
                if (authResult != null)
                {
                    return authResult;
                }

                if (!StationRouteIDExists(dbConnector, normalizedInstanceID, normalizedStationSchemeID, normalizedID))
                {
                    return NotFound("Station route not found.");
                }

                dbConnector.BeginTransaction();
                if (CapacityDataLifecycle.RouteIsReferenced(dbConnector, normalizedInstanceID, normalizedStationSchemeID, normalizedID))
                {
                    dbConnector.Rollback();
                    return Conflict("此进路仍被列车作业、模板或过程约束引用，请先解除引用后再删除。");
                }
                CapacityDataLifecycle.DeleteRouteParameters(dbConnector, normalizedInstanceID, normalizedStationSchemeID, normalizedID);
                var tableName = QuoteIdentifier("stationroute");
                var deleted = dbConnector.ExecuteNonQuery(
                    $@"DELETE FROM {tableName}
                       WHERE InstanceID = @normalizedInstanceID
                         AND StationSchemeID = @normalizedStationSchemeID
                         AND ID = @normalizedID",
                    new { normalizedInstanceID, normalizedStationSchemeID, normalizedID });

                if (deleted != 1) throw new InvalidOperationException("Station route deletion was not saved.");
                dbConnector.Commit();
                return Ok("Station route deleted successfully.");
            }
            catch (Exception ex)
            {
                dbConnector?.Rollback();
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

        [HttpPost(Name = "CopyStationScheme")]
        public async Task<IActionResult> CopyStationScheme(
            [FromBody] SwitchYard.StationLayout.StationSchemeCopyRequest request,
            [FromServices] SwitchYard.StationLayout.IStationLayoutService service,
            CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await service.CopyStationSchemeAsync(User, request, cancellationToken));
            }
            catch (SwitchYard.StationLayout.StationLayoutValidationException ex) { return BadRequest(ex.Message); }
            catch (SwitchYard.StationLayout.StationLayoutNotFoundException ex) { return NotFound(ex.Message); }
            catch (SwitchYard.StationLayout.StationLayoutUnauthenticatedException ex) { return Unauthorized(ex.Message); }
            catch (SwitchYard.StationLayout.StationLayoutForbiddenException ex) { return StatusCode(403, ex.Message); }
            catch (SwitchYard.StationLayout.StationLayoutConflictException ex) { return Conflict(ex.Message); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to copy station scheme.");
                return StatusCode(500, "Failed to copy station scheme.");
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

                if (!StationSchemeIDExists(dbConnector, normalizedInstanceID, normalizedStationSchemeID))
                {
                    return NotFound("Station scheme not found.");
                }

                dbConnector.BeginTransaction();
                CapacityDataLifecycle.DeleteSchemeChildren(dbConnector, normalizedInstanceID, normalizedStationSchemeID);
                DeleteStationSchemeMetadata(
                    dbConnector,
                    normalizedInstanceID,
                    normalizedStationSchemeID);
                CapacityDataLifecycle.VerifySchemeDeleted(dbConnector, normalizedInstanceID, normalizedStationSchemeID);
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

            var stationSchemeTable = QuoteIdentifier("stationscheme");
            var rows = dbConnector.Query<StationSchemeLookupRow>(
                $@"SELECT ID, Name
                   FROM {stationSchemeTable}
                   WHERE InstanceID = @instanceID
                   ORDER BY CASE WHEN Name IS NULL OR TRIM(Name) = '' THEN ID ELSE Name END, ID",
                new { instanceID }) ?? new List<StationSchemeLookupRow>();

            var schemesByID = new Dictionary<string, StationSchemeLookupRow>(StringComparer.Ordinal);
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
                    StringComparer.Ordinal)
                .ThenBy(row => row.ID ?? string.Empty, StringComparer.Ordinal)
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
            var deleted = dbConnector.ExecuteNonQuery(
                $@"DELETE FROM {QuoteIdentifier("stationscheme")}
                   WHERE InstanceID = @instanceID AND ID = @stationSchemeID",
                new { instanceID, stationSchemeID });
            if (deleted != 1) throw new InvalidOperationException("Station scheme deletion was not saved.");
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

        private static void DeleteStationLayoutTableRows(DBConnector db, string tableName, string instanceID, string stationSchemeID)
        {
            db.ExecuteNonQuery($"DELETE FROM {QuoteIdentifier(tableName)} WHERE InstanceID=@instanceID AND StationSchemeID=@stationSchemeID", new { instanceID, stationSchemeID });
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

            string trainTypes;
            try { trainTypes = StationRouteTrainTypes.Normalize(request.TrainTypes, request.Type); }
            catch (ArgumentException ex) { return (null, BadRequest(ex.Message)); }

            return (new StationRouteRow
            {
                InstanceID = instanceID,
                StationSchemeID = stationSchemeID,
                ID = id,
                Type = NormalizeNullableRouteEndField(request.Type),
                TrainTypes = trainTypes,
                Description = NormalizeNullableRouteEndField(request.Description),
                NodeList = NormalizeNullableRouteEndField(request.NodeList),
                LinkList = NormalizeNullableRouteEndField(request.LinkList),
                SwitchList = NormalizeNullableRouteEndField(request.SwitchList),
                CellList = NormalizeNullableRouteEndField(request.CellList),
                InterruptCellList = NormalizeNullableRouteEndField(request.InterruptCellList),
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
            string stationSchemeID,
            string? calculationParameterSetID = null)
        {

            var parameterSetID = new CalculationParameterSets(dbConnector, instanceID, stationSchemeID).Resolve(calculationParameterSetID);
            var tableName = QuoteIdentifier("stationroute");
            var timeTableName = QuoteIdentifier("stationroutetime");
            return dbConnector.Query<StationRouteRow>(
                $@"SELECT InstanceID, StationSchemeID, ID, {QuoteIdentifier("Type")} AS {QuoteIdentifier("Type")},
                          Description, NodeList, LinkList, SwitchList, CellList, InterruptCellList, SignalList,
                          AllowanceTags, ForbiddenTags, TrainTypes, StartNodeID, EndNodeID,
                          CASE WHEN EXISTS (
                              SELECT 1
                              FROM {timeTableName}
                              WHERE {timeTableName}.InstanceID = {tableName}.InstanceID
                                AND {timeTableName}.StationSchemeID = {tableName}.StationSchemeID
                                AND {timeTableName}.RouteID = {tableName}.ID
                                AND COALESCE({timeTableName}.CalculationParameterSetID, 'default') = @parameterSetID
                          ) THEN 1 ELSE 0 END AS OccupancyTimeConfigured
                   FROM {tableName}
                   WHERE InstanceID = @instanceID AND StationSchemeID = @stationSchemeID
                   ORDER BY ID",
                new { instanceID, stationSchemeID, parameterSetID }) ?? new List<StationRouteRow>();
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

        private static StationRouteRow? FindStationRouteByID(
            DBConnector dbConnector,
            string instanceID,
            string stationSchemeID,
            string id)
        {

            var tableName = QuoteIdentifier("stationroute");
            return (dbConnector.Query<StationRouteRow>(
                $@"SELECT InstanceID, StationSchemeID, ID, {QuoteIdentifier("Type")} AS {QuoteIdentifier("Type")},
                          Description, NodeList, LinkList, SwitchList, CellList, InterruptCellList, SignalList,
                          AllowanceTags, ForbiddenTags, TrainTypes, StartNodeID, EndNodeID
                   FROM {tableName}
                   WHERE InstanceID = @instanceID
                     AND StationSchemeID = @stationSchemeID
                     AND ID = @id
                   LIMIT 1",
                new { instanceID, stationSchemeID, id }) ?? new List<StationRouteRow>()).FirstOrDefault();
        }

        private static List<string> ParseStationRouteIdList(string? value)
        {
            var text = value?.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                return new List<string>();
            }

            try
            {
                var parsed = JsonSerializer.Deserialize<List<string>>(text);
                if (parsed != null)
                {
                    return parsed
                        .Select(id => id?.Trim())
                        .Where(id => !string.IsNullOrWhiteSpace(id))
                        .Select(id => id!)
                        .ToList();
                }
            }
            catch (JsonException)
            {
            }

            return Regex.Split(text, @"(?:\s*->\s*)|(?:\s*[,;\r\n]\s*)|\s+")
                .Select(id => id.Trim())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToList();
        }

        private static string SerializeStationRouteIdList(IEnumerable<string> ids)
        {
            return JsonSerializer.Serialize(NormalizeStationRouteIdList(ids));
        }

        private static List<string> NormalizeStationRouteIdList(IEnumerable<string> ids)
        {
            var normalized = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in ids)
            {
                var trimmed = id?.Trim();
                if (string.IsNullOrWhiteSpace(trimmed) || !seen.Add(trimmed))
                {
                    continue;
                }

                normalized.Add(trimmed);
            }

            return normalized;
        }

        private static List<string> BuildStationRouteInterruptCellUniverse(
            IEnumerable<StationRouteRow> routes)
        {
            return NormalizeStationRouteIdList(routes.SelectMany(route => ParseStationRouteIdList(route.CellList)));
        }

        private static string BuildStationRouteInterruptCellList(
            StationRouteRow route,
            IEnumerable<StationRouteRow> routes,
            IReadOnlyCollection<string> universeCellIDs)
        {
            var routeCellSet = new HashSet<string>(
                ParseStationRouteIdList(route.CellList),
                StringComparer.Ordinal);
            var parallelCellSet = new HashSet<string>(StringComparer.Ordinal);

            foreach (var otherRoute in routes)
            {
                if (ReferenceEquals(route, otherRoute) ||
                    string.Equals(route.ID, otherRoute.ID, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var otherCellIDs = ParseStationRouteIdList(otherRoute.CellList);
                if (otherCellIDs.Any(cellID => routeCellSet.Contains(cellID)))
                {
                    continue;
                }

                foreach (var cellID in otherCellIDs)
                {
                    parallelCellSet.Add(cellID);
                }
            }

            return SerializeStationRouteIdList(
                universeCellIDs.Where(cellID => !routeCellSet.Contains(cellID) && !parallelCellSet.Contains(cellID)));
        }

        private static List<string> BuildStationRouteTimeCellIDs(StationRouteRow route)
        {
            return BuildStationRouteTimeCellPlan(route.CellList, route.InterruptCellList).CellIDs;
        }

        private static (List<string> CellIDs, HashSet<string> InterruptCellIDSet) BuildStationRouteTimeCellPlan(
            string? cellList,
            string? interruptCellList)
        {
            var routeCellIDs = NormalizeStationRouteIdList(ParseStationRouteIdList(cellList));
            var routeCellIDSet = new HashSet<string>(routeCellIDs, StringComparer.Ordinal);
            var interruptCellIDs = NormalizeStationRouteIdList(ParseStationRouteIdList(interruptCellList))
                .Where(cellID => !routeCellIDSet.Contains(cellID))
                .ToList();
            var interruptCellIDSet = new HashSet<string>(interruptCellIDs, StringComparer.Ordinal);
            return (NormalizeStationRouteIdList(routeCellIDs.Concat(interruptCellIDs)), interruptCellIDSet);
        }

        private static (int StartOccupationShift, int EndOccupationShift) GetStationRouteTimeDefaultWindow(
            IEnumerable<StationRouteTimeRow> rows,
            IReadOnlySet<string> interruptCellIDSet)
        {
            var routeRows = rows
                .Where(row =>
                {
                    var cellID = row.CellID?.Trim();
                    return !string.IsNullOrWhiteSpace(cellID) && !interruptCellIDSet.Contains(cellID);
                })
                .ToList();
            var startValues = routeRows
                .Select(row => row.StartOccupationShift)
                .Where(value => value.HasValue)
                .Select(value => value!.Value)
                .ToList();
            var endValues = routeRows
                .Select(row => row.EndOccupationShift)
                .Where(value => value.HasValue)
                .Select(value => value!.Value)
                .ToList();
            return (
                startValues.Count > 0 ? startValues.Min() : 0,
                endValues.Count > 0 ? endValues.Max() : 0);
        }

        private static void ApplyStationRouteInterruptCellTimeDefaults(
            IEnumerable<StationRouteTimeRow> rows,
            IReadOnlySet<string> interruptCellIDSet)
        {
            var routeTimeRows = rows.ToList();
            if (interruptCellIDSet.Count == 0 || routeTimeRows.Count == 0)
            {
                return;
            }

            var defaultWindow = GetStationRouteTimeDefaultWindow(routeTimeRows, interruptCellIDSet);
            foreach (var row in routeTimeRows)
            {
                var cellID = row.CellID?.Trim();
                if (string.IsNullOrWhiteSpace(cellID) || !interruptCellIDSet.Contains(cellID))
                {
                    continue;
                }

                row.IsInterruptCell = true;
                var hasDefaultZeroShift =
                    (row.StartOccupationShift ?? 0) == 0 &&
                    (row.EndOccupationShift ?? 0) == 0 &&
                    (defaultWindow.StartOccupationShift != 0 || defaultWindow.EndOccupationShift != 0);
                if (hasDefaultZeroShift)
                {
                    row.StartOccupationShift = defaultWindow.StartOccupationShift;
                    row.EndOccupationShift = defaultWindow.EndOccupationShift;
                    continue;
                }

                row.StartOccupationShift ??= defaultWindow.StartOccupationShift;
                row.EndOccupationShift ??= defaultWindow.EndOccupationShift;
            }
        }

        private static void UpdateStationRouteInterruptCellList(
            DBConnector dbConnector,
            string instanceID,
            string stationSchemeID,
            string? routeID,
            string? interruptCellList)
        {
            var normalizedRouteID = routeID?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedRouteID))
            {
                return;
            }

            var tableName = QuoteIdentifier("stationroute");
            dbConnector.ExecuteNonQuery(
                $@"UPDATE {tableName}
                   SET InterruptCellList = @interruptCellList
                   WHERE InstanceID = @instanceID
                     AND StationSchemeID = @stationSchemeID
                     AND ID = @routeID",
                new
                {
                    instanceID,
                    stationSchemeID,
                    routeID = normalizedRouteID,
                    interruptCellList
                });
        }

        private static List<StationRouteTimeRow> OrderStationRouteTimesByCellList(
            List<StationRouteTimeRow> rows,
            string? cellList,
            string? interruptCellList,
            bool includeMissingCells = false)
        {
            var plan = BuildStationRouteTimeCellPlan(cellList, interruptCellList);
            var orderedRows = new List<StationRouteTimeRow>();
            var usedIndexes = new HashSet<int>();
            var defaultWindow = GetStationRouteTimeDefaultWindow(rows, plan.InterruptCellIDSet);

            var orderByCellID = plan.CellIDs
                .Select((cellID, index) => new { cellID, index })
                .GroupBy(item => item.cellID, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First().index, StringComparer.Ordinal);

            foreach (var cellID in plan.CellIDs)
            {
                var match = rows
                    .Select((row, index) => new { row, index })
                    .FirstOrDefault(item =>
                        !usedIndexes.Contains(item.index) &&
                        string.Equals(item.row.CellID?.Trim(), cellID, StringComparison.OrdinalIgnoreCase));
                if (match == null)
                {
                    if (includeMissingCells)
                    {
                        orderedRows.Add(new StationRouteTimeRow
                        {
                            CellID = cellID,
                            StartOccupationShift = plan.InterruptCellIDSet.Contains(cellID) ? defaultWindow.StartOccupationShift : 0,
                            EndOccupationShift = plan.InterruptCellIDSet.Contains(cellID) ? defaultWindow.EndOccupationShift : 0,
                            IsInterruptCell = plan.InterruptCellIDSet.Contains(cellID)
                        });
                    }

                    continue;
                }

                usedIndexes.Add(match.index);
                var matchedCellID = match.row.CellID?.Trim();
                if (!string.IsNullOrWhiteSpace(matchedCellID))
                {
                    match.row.CellID = matchedCellID;
                }

                match.row.IsInterruptCell = plan.InterruptCellIDSet.Contains(cellID);
                orderedRows.Add(match.row);
            }

            if (plan.CellIDs.Count == 0)
            {
                orderedRows.AddRange(rows
                    .Select((row, index) => new { row, index })
                    .Where(item => !usedIndexes.Contains(item.index))
                    .OrderBy(item => orderByCellID.TryGetValue(item.row.CellID ?? string.Empty, out var order) ? order : int.MaxValue)
                    .ThenBy(item => item.index)
                    .Select(item =>
                    {
                        var cellID = item.row.CellID?.Trim();
                        if (!string.IsNullOrWhiteSpace(cellID))
                        {
                            item.row.CellID = cellID;
                        }

                        item.row.IsInterruptCell = !string.IsNullOrWhiteSpace(cellID) &&
                            plan.InterruptCellIDSet.Contains(cellID);
                        return item.row;
                    }));
            }

            ApplyStationRouteInterruptCellTimeDefaults(orderedRows, plan.InterruptCellIDSet);
            return orderedRows;
        }

        private static void ReplaceStationRouteTimes(
            DBConnector dbConnector,
            string instanceID,
            string stationSchemeID,
            string routeID,
            string trainTypeID,
            List<StationRouteTimeRow> rows,
            string? calculationParameterSetID = null)
        {
            dbConnector.BeginTransaction();
            try
            {
                LockParameterSetScheme(dbConnector, instanceID, stationSchemeID);
                CalculationParameterSets.EnsureDefault(dbConnector, instanceID, stationSchemeID);
                var parameterSetID = RequireCalculationParameterSet(dbConnector, instanceID, stationSchemeID, calculationParameterSetID);
                var tableName = QuoteIdentifier("stationroutetime");
                dbConnector.ExecuteNonQuery(
                    $@"DELETE FROM {tableName}
                       WHERE InstanceID = @instanceID
                         AND StationSchemeID = @stationSchemeID
                         AND RouteID = @routeID
                         AND COALESCE(TrainTypeID, '') = @trainTypeID
                         AND COALESCE(CalculationParameterSetID, 'default') = @parameterSetID",
                    new { instanceID, stationSchemeID, routeID, trainTypeID, parameterSetID });

                foreach (var row in rows)
                {
                    var cellID = row.CellID?.Trim();
                    if (string.IsNullOrWhiteSpace(cellID))
                    {
                        continue;
                    }

                    dbConnector.ExecuteNonQuery(
                        $@"INSERT INTO {tableName} (
                               InstanceID, StationSchemeID, CalculationParameterSetID, RouteID, TrainTypeID,
                               CellID, StartOccupationShift, EndOccupationShift)
                           VALUES (
                               @InstanceID, @StationSchemeID, @CalculationParameterSetID, @RouteID, @TrainTypeID,
                               @CellID, @StartOccupationShift, @EndOccupationShift)",
                        new StationRouteTimeRow
                        {
                            InstanceID = instanceID,
                            StationSchemeID = stationSchemeID,
                            RouteID = routeID,
                            CalculationParameterSetID = parameterSetID,
                            TrainTypeID = trainTypeID,
                            CellID = cellID,
                            StartOccupationShift = row.StartOccupationShift,
                            EndOccupationShift = row.EndOccupationShift,
                        });
                }
                dbConnector.Commit();
            }
            catch { dbConnector.Rollback(); throw; }
        }

        private static List<StationRouteTimeRow> LoadStationRouteTimes(
            DBConnector dbConnector,
            string instanceID,
            string stationSchemeID,
            string routeID,
            string trainTypeID,
            string? calculationParameterSetID = null)
        {

            var parameterSetID = RequireCalculationParameterSet(dbConnector, instanceID, stationSchemeID, calculationParameterSetID);
            var tableName = QuoteIdentifier("stationroutetime");
            return dbConnector.Query<StationRouteTimeRow>(
                $@"SELECT InstanceID, StationSchemeID, CalculationParameterSetID, RouteID, TrainTypeID, CellID,
                          StartOccupationShift, EndOccupationShift
                   FROM {tableName}
                   WHERE InstanceID = @instanceID
                     AND StationSchemeID = @stationSchemeID
                     AND RouteID = @routeID
                     AND COALESCE(TrainTypeID, '') = @trainTypeID
                     AND COALESCE(CalculationParameterSetID, 'default') = @parameterSetID",
                new { instanceID, stationSchemeID, routeID, trainTypeID, parameterSetID }) ?? new List<StationRouteTimeRow>();
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
                route.Type,
                route.LinkList);
        }

        private static string BuildStationRouteDescription(
            DBConnector dbConnector,
            string instanceID,
            string stationSchemeID,
            string startNodeID,
            string endNodeID,
            string routeType,
            string? linkList = null)
        {
            if (IsDwellingRouteType(routeType))
            {
                var linkIDs = ParseStationRouteIdList(linkList);
                if (linkIDs.Count == 1)
                {
                    var link = (dbConnector.Query<StationLinkRow>(
                        $@"SELECT * FROM {QuoteIdentifier("link")}
                           WHERE InstanceID = @instanceID AND StationSchemeID = @stationSchemeID AND ID = @linkID",
                        new { instanceID, stationSchemeID, linkID = linkIDs[0] }) ?? new()).FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(link?.Name))
                        return BuildStationDwellingRouteDescription(link.Name);
                }
            }

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
                "DWELLING" => "停留",
                _ => normalizedRouteType
            };
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
                .Distinct(StringComparer.Ordinal)
                .ToList();
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

        internal static void EnsureChartSchemaForMigration(DBConnector dbConnector)
        {
            EnsureStationSchemeSchema(dbConnector);
            EnsureLinkSchema(dbConnector);
            EnsureCellSchema(dbConnector);
            EnsureStationRouteSchema(dbConnector);
            EnsureStationRouteTimeSchema(dbConnector);
            EnsureStationRouteEndSchema(dbConnector);
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
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["InstanceID"] = "VARCHAR(50) NULL",
                    ["ID"] = "VARCHAR(50) NULL",
                    ["Name"] = "VARCHAR(100) NULL",
                    ["DisplayStyles"] = "TEXT NULL",
                    ["GridSettings"] = "TEXT NULL"
                }
                : new Dictionary<string, string>(StringComparer.Ordinal)
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
                            {QuoteIdentifier("LinkIDList")} LONGTEXT NULL,
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
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["InstanceID"] = "VARCHAR(50) NULL",
                    ["StationSchemeID"] = "VARCHAR(50) NULL",
                    ["ID"] = "VARCHAR(50) NULL",
                    ["LinkIDList"] = "LONGTEXT NULL",
                    ["Name"] = "VARCHAR(100) NULL"
                }
                : new Dictionary<string, string>(StringComparer.Ordinal)
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
                            {QuoteIdentifier("Radius")} DOUBLE NULL,
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
                            {QuoteIdentifier("Radius")} REAL NULL,
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
            var requiredColumns = new Dictionary<string, string>(StringComparer.Ordinal)
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
                    $@"ALTER TABLE {tableName} MODIFY COLUMN {QuoteIdentifier("Radius")} DOUBLE NULL");
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
            var requiredColumns = new Dictionary<string, string>(StringComparer.Ordinal)
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
                            {QuoteIdentifier("InterruptCellList")} LONGTEXT NULL,
                            {QuoteIdentifier("SignalList")} LONGTEXT NULL,
                            {QuoteIdentifier("AllowanceTags")} LONGTEXT NULL,
                            {QuoteIdentifier("ForbiddenTags")} LONGTEXT NULL,
                            {QuoteIdentifier("TrainTypes")} TEXT NULL,
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
                            {QuoteIdentifier("InterruptCellList")} TEXT NULL,
                            {QuoteIdentifier("SignalList")} TEXT NULL,
                            {QuoteIdentifier("AllowanceTags")} TEXT NULL,
                            {QuoteIdentifier("ForbiddenTags")} TEXT NULL,
                            {QuoteIdentifier("TrainTypes")} TEXT NULL,
                            {QuoteIdentifier("StartNodeID")} TEXT NULL,
                            {QuoteIdentifier("EndNodeID")} TEXT NULL
                        )");
                }

                return;
            }

            var existingColumns = GetColumnNames(dbConnector, "stationroute");
            var shortTextType = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName) ? "VARCHAR(50) NULL" : "TEXT NULL";
            var longTextType = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName) ? "LONGTEXT NULL" : "TEXT NULL";
            var requiredColumns = new Dictionary<string, string>(StringComparer.Ordinal)
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
                ["InterruptCellList"] = longTextType,
                ["SignalList"] = longTextType,
                ["AllowanceTags"] = longTextType,
                ["ForbiddenTags"] = longTextType,
                ["TrainTypes"] = "TEXT NULL",
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

        private static void EnsureStationRouteTimeSchema(DBConnector dbConnector)
        {
            var tableName = QuoteIdentifier("stationroutetime");
            if (!TableExists(dbConnector, "stationroutetime"))
            {
                if (DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName))
                {
                    dbConnector.ExecuteNonQuery(
                        $@"CREATE TABLE IF NOT EXISTS {tableName} (
                            {QuoteIdentifier("InstanceID")} VARCHAR(50) NULL,
                            {QuoteIdentifier("StationSchemeID")} VARCHAR(50) NULL,
                            {QuoteIdentifier("RouteID")} VARCHAR(50) NULL,
                            {QuoteIdentifier("TrainTypeID")} VARCHAR(50) NULL,
                            {QuoteIdentifier("CalculationParameterSetID")} VARCHAR(50) NULL,
                            {QuoteIdentifier("CellID")} VARCHAR(50) NULL,
                            {QuoteIdentifier("StartOccupationShift")} INT NULL,
                            {QuoteIdentifier("EndOccupationShift")} INT NULL
                        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci");
                }
                else
                {
                    dbConnector.ExecuteNonQuery(
                        $@"CREATE TABLE IF NOT EXISTS {tableName} (
                            {QuoteIdentifier("InstanceID")} TEXT NULL,
                            {QuoteIdentifier("StationSchemeID")} TEXT NULL,
                            {QuoteIdentifier("RouteID")} TEXT NULL,
                            {QuoteIdentifier("TrainTypeID")} TEXT NULL,
                            {QuoteIdentifier("CalculationParameterSetID")} TEXT NULL,
                            {QuoteIdentifier("CellID")} TEXT NULL,
                            {QuoteIdentifier("StartOccupationShift")} INTEGER NULL,
                            {QuoteIdentifier("EndOccupationShift")} INTEGER NULL
                        )");
                }

                return;
            }

            var existingColumns = GetColumnNames(dbConnector, "stationroutetime");
            var textType = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName) ? "VARCHAR(50) NULL" : "TEXT NULL";
            var intType = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName) ? "INT NULL" : "INTEGER NULL";
            var requiredColumns = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["InstanceID"] = textType,
                ["StationSchemeID"] = textType,
                ["RouteID"] = textType,
                ["TrainTypeID"] = textType,
                ["CalculationParameterSetID"] = textType,
                ["CellID"] = textType,
                ["StartOccupationShift"] = intType,
                ["EndOccupationShift"] = intType
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
            var requiredColumns = new Dictionary<string, string>(StringComparer.Ordinal)
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

        private static string ToInvariantString(string value)
        {
            return value;
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
                        .Order(StringComparer.Ordinal)
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

        [HttpPost(Name = "SaveToDatabase")]
        public IActionResult SaveToDatabase([FromBody] StationLayoutSaveRequest? request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Json))
            {
                return BadRequest("Request body must contain a non-empty json field.");
            }

            try
            {
                var stationScheme = ParseRequestToBuildStationScheme(request);
                var saveResult = WriteStationSchemeToDatabase(stationScheme);
                if (saveResult)
                {
                    return Ok(saveResult);
                }

                return StatusCode(500, "Failed to save station scheme to capacity database.");
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Invalid station layout JSON payload for database save.");
                return BadRequest("Invalid station layout JSON payload.");
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid station layout payload for database save.");
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while saving station scheme to capacity database.");
                return StatusCode(500, "Failed to save station scheme to capacity database.");
            }
        }

        [HttpPost(Name = "GetDatabaseJson")]
        public IActionResult GetDatabaseJson([FromBody] StationLayoutDatabaseRequest? request)
        {
            try
            {
                var instanceId = FirstNonEmpty(request?.InstanceID, DefaultCapacityInstanceId);
                var stationSchemeId = FirstNonEmpty(request?.StationSchemeID, DefaultStationSchemeId);
                var stationScheme = ReadStationSchemeFromDatabase(instanceId, stationSchemeId);
                if (stationScheme == null)
                {
                    return NotFound($"No station scheme '{stationSchemeId}' was found for instance '{instanceId}'.");
                }

                var payload = BuildStationLayoutPayload(stationScheme);
                return Ok(payload);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load station scheme from capacity database.");
                return StatusCode(500, "Failed to load station scheme from capacity database.");
            }
        }

        private StationScheme ParseRequestToBuildStationScheme(StationLayoutSaveRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (string.IsNullOrWhiteSpace(request.Json))
            {
                throw new ArgumentException("Request body must contain a non-empty json field.", nameof(request));
            }

            var payload = JsonSerializer.Deserialize<StationLayoutPayload>(request.Json, LayoutJsonSerializerOptions)
                ?? throw new JsonException("Station layout payload is empty.");

            var metadata = payload.Metadata ?? new StationLayoutMetadata();
            var instanceId = FirstNonEmpty(request.InstanceID, metadata.InstanceID, DefaultCapacityInstanceId);
            var stationSchemeId = FirstNonEmpty(request.StationSchemeID, metadata.StationSchemeID, metadata.ID, DefaultStationSchemeId);
            var stationSchemeName = FirstNonEmpty(request.Name, metadata.Name, DefaultStationSchemeName);

            var resolvedNodes = new List<Node>();
            var nodeIdMap = new Dictionary<string, int>(StringComparer.Ordinal);
            var nodeById = new Dictionary<int, Node>();

            var nextNodeId = InitializeNodes(payload.Nodes, instanceId, stationSchemeId, resolvedNodes, nodeIdMap, nodeById);

            var resolvedLinks = new List<Link>();
            var linkIdMap = new Dictionary<string, int>(StringComparer.Ordinal);
            var nextLinkId = InitializeLinks(
                payload.Tracks,
                instanceId,
                stationSchemeId,
                resolvedNodes,
                nodeIdMap,
                nodeById,
                resolvedLinks,
                linkIdMap,
                nextNodeId);

            // `ResolveOrCreateNode` may have added extra nodes while repairing missing track bindings.
            nextNodeId = nextLinkId.NextNodeId;

            var signals = (payload.Signals ?? Enumerable.Empty<SignalLayoutDto>())
                .Select(signal => BuildSignal(signal, instanceId, stationSchemeId, resolvedNodes, nodeIdMap, nodeById, ref nextNodeId))
                .ToList();

            var insulationJoints = (payload.InsulationJoints ?? Enumerable.Empty<InsulationJointLayoutDto>())
                .Select(joint => BuildInsulationJoint(joint, instanceId, stationSchemeId, resolvedNodes, nodeIdMap, nodeById, ref nextNodeId))
                .ToList();

            var platforms = (payload.Platforms ?? Enumerable.Empty<PlatformLayoutDto>())
                .Select(platform => BuildPlatform(platform, instanceId, stationSchemeId))
                .ToList();

            var switches = (payload.Switches ?? Enumerable.Empty<SwitchLayoutDto>())
                .Select(sw => BuildSwitch(sw, instanceId, stationSchemeId, resolvedNodes, nodeIdMap, nodeById, resolvedLinks, linkIdMap, ref nextNodeId))
                .ToList();

            return new StationScheme
            {
                InstanceID = instanceId,
                ID = stationSchemeId,
                Name = stationSchemeName,
                Signals = signals,
                InsulationJoints = insulationJoints,
                Platforms = platforms,
                LayoutSwitches = switches,
                Nodes = resolvedNodes.ToHashSet(),
                Links = resolvedLinks.ToHashSet(),
                Cells = new HashSet<Cell>(),
                Routes = new HashSet<SwitchYard.Capacity.Route>(),
                AdjecentSegments = new HashSet<Segment>()
            };
        }

        private bool WriteStationSchemeToDatabase(StationScheme stationScheme)
        {
            if (stationScheme == null)
            {
                throw new ArgumentNullException(nameof(stationScheme));
            }

            if (string.IsNullOrWhiteSpace(stationScheme.InstanceID))
            {
                throw new ArgumentException("StationScheme.InstanceID is required.", nameof(stationScheme));
            }

            if (string.IsNullOrWhiteSpace(stationScheme.ID))
            {
                throw new ArgumentException("StationScheme.ID is required.", nameof(stationScheme));
            }

            var dbConnector = DBConnector.GetDBConnector(DBConnector.CapacityDatabaseSectionName);
            var stationSchemeId = stationScheme.ID;
            var instanceId = stationScheme.InstanceID;

            try
            {
                dbConnector.BeginTransaction();
                EnsureCapacityStationSchemeTables(dbConnector);

                var keyParameters = new
                {
                    instanceID = instanceId,
                    stationSchemeID = stationSchemeId
                };

                dbConnector.ExecuteNonQuery("DELETE FROM switchbranchvector WHERE InstanceID = @instanceID AND StationSchemeID = @stationSchemeID", keyParameters);
                dbConnector.ExecuteNonQuery("DELETE FROM `switch` WHERE InstanceID = @instanceID AND StationSchemeID = @stationSchemeID", keyParameters);
                dbConnector.ExecuteNonQuery("DELETE FROM platform WHERE InstanceID = @instanceID AND StationSchemeID = @stationSchemeID", keyParameters);
                dbConnector.ExecuteNonQuery("DELETE FROM insulationjoint WHERE InstanceID = @instanceID AND StationSchemeID = @stationSchemeID", keyParameters);
                dbConnector.ExecuteNonQuery("DELETE FROM `signal` WHERE InstanceID = @instanceID AND StationSchemeID = @stationSchemeID", keyParameters);
                dbConnector.ExecuteNonQuery("DELETE FROM stationroutetime WHERE InstanceID = @instanceID AND StationSchemeID = @stationSchemeID", keyParameters);
                dbConnector.ExecuteNonQuery("DELETE FROM route WHERE InstanceID = @instanceID AND StationSchemeID = @stationSchemeID", keyParameters);
                dbConnector.ExecuteNonQuery("DELETE FROM cell WHERE InstanceID = @instanceID AND StationSchemeID = @stationSchemeID", keyParameters);
                dbConnector.ExecuteNonQuery("DELETE FROM link WHERE InstanceID = @instanceID AND StationSchemeID = @stationSchemeID", keyParameters);
                dbConnector.ExecuteNonQuery("DELETE FROM node WHERE InstanceID = @instanceID AND StationSchemeID = @stationSchemeID", keyParameters);
                dbConnector.ExecuteNonQuery("DELETE FROM stationscheme WHERE InstanceID = @instanceID AND ID = @stationSchemeID", keyParameters);
                dbConnector.ExecuteNonQuery("DELETE FROM capacityinstance WHERE ID = @instanceID", new { instanceID = instanceId });

                RequireWriteSuccess(
                    dbConnector.ExecuteNonQuery(
                        "INSERT INTO capacityinstance (ID, Name, Owner, CreatedDate, IsActive) VALUES (@ID, @Name, @Owner, @CreatedDate, @IsActive)",
                        new CapacityInstance
                        {
                            ID = instanceId,
                            Name = stationScheme.Name,
                            Owner = User?.Identity?.Name,
                            CreatedDate = DateTime.Now,
                            IsActive = true
                        }),
                    "capacityinstance");

                RequireWriteSuccess(
                    dbConnector.ExecuteNonQuery(
                        "INSERT INTO stationscheme (InstanceID, ID, Name) VALUES (@InstanceID, @ID, @Name)",
                        new
                        {
                            InstanceID = instanceId,
                            ID = stationSchemeId,
                            Name = stationScheme.Name
                        }),
                    "stationscheme");

                InsertNodes(dbConnector, stationScheme);
                InsertLinks(dbConnector, stationScheme);
                InsertSignals(dbConnector, stationScheme);
                InsertInsulationJoints(dbConnector, stationScheme);
                InsertPlatforms(dbConnector, stationScheme);
                InsertSwitches(dbConnector, stationScheme);
                InsertCells(dbConnector, stationScheme);
                InsertRoutes(dbConnector, stationScheme);
                InsertStationRouteTimes(dbConnector, stationScheme);
                InsertSegments(dbConnector, stationScheme);

                dbConnector.Commit();
                _logger.LogInformation(
                    "Station scheme {StationSchemeID} for instance {InstanceID} saved to capacity database with {NodeCount} nodes, {LinkCount} links, {SignalCount} signals, {JointCount} insulation joints, {PlatformCount} platforms, and {SwitchCount} switches.",
                    stationSchemeId,
                    instanceId,
                    stationScheme.Nodes?.Count ?? 0,
                    stationScheme.Links?.Count ?? 0,
                    stationScheme.Signals.Count,
                    stationScheme.InsulationJoints.Count,
                    stationScheme.Platforms.Count,
                    stationScheme.LayoutSwitches.Count);
                return true;
            }
            catch (Exception ex)
            {
                dbConnector.Rollback();
                _logger.LogError(
                    ex,
                    "Failed to save station scheme {StationSchemeID} for instance {InstanceID} to capacity database.",
                    stationSchemeId,
                    instanceId);
                return false;
            }
        }

        private StationScheme? ReadStationSchemeFromDatabase(string instanceId, string stationSchemeId)
        {
            var dbConnector = DBConnector.GetDBConnector(DBConnector.CapacityDatabaseSectionName);
            EnsureCapacityStationSchemeTables(dbConnector);

            var queryParameters = new
            {
                instanceID = instanceId,
                stationSchemeID = stationSchemeId
            };

            var stationSchemeHeader = (dbConnector.Query<StationScheme>(
                "SELECT InstanceID, ID, Name FROM stationscheme WHERE InstanceID = @instanceID AND ID = @stationSchemeID",
                queryParameters) ?? new List<StationScheme>()).FirstOrDefault();
            if (stationSchemeHeader == null)
            {
                return null;
            }

            var nodes = dbConnector.Query<Node>(
                "SELECT InstanceID, StationSchemeID, ID, X, Y FROM node WHERE InstanceID = @instanceID AND StationSchemeID = @stationSchemeID ORDER BY ID",
                queryParameters) ?? new List<Node>();
            var links = dbConnector.Query<Link>(
                "SELECT InstanceID, StationSchemeID, ID, FromNodeID, ToNodeID FROM link WHERE InstanceID = @instanceID AND StationSchemeID = @stationSchemeID ORDER BY ID",
                queryParameters) ?? new List<Link>();
            var signals = dbConnector.Query<Signal>(
                "SELECT InstanceID, StationSchemeID, ID, Type, Direction, BindingNodeID FROM `signal` WHERE InstanceID = @instanceID AND StationSchemeID = @stationSchemeID ORDER BY ID",
                queryParameters) ?? new List<Signal>();
            var insulationJoints = dbConnector.Query<InsulationJoint>(
                "SELECT InstanceID, StationSchemeID, ID, Type, BindingNodeID FROM insulationjoint WHERE InstanceID = @instanceID AND StationSchemeID = @stationSchemeID ORDER BY ID",
                queryParameters) ?? new List<InsulationJoint>();
            var platforms = dbConnector.Query<Platform>(
                "SELECT InstanceID, StationSchemeID, ID, Name, X, Y, Width, Height FROM platform WHERE InstanceID = @instanceID AND StationSchemeID = @stationSchemeID ORDER BY ID",
                queryParameters) ?? new List<Platform>();
            var switches = dbConnector.Query<Switch>(
                "SELECT InstanceID, StationSchemeID, ID, Type, BindingNodeID FROM `switch` WHERE InstanceID = @instanceID AND StationSchemeID = @stationSchemeID ORDER BY ID",
                queryParameters) ?? new List<Switch>();
            var branchVectors = dbConnector.Query<SwitchBranchVectorRecord>(
                "SELECT InstanceID, StationSchemeID, SwitchID, Sequence, X, Y, BindingLinkID FROM switchbranchvector WHERE InstanceID = @instanceID AND StationSchemeID = @stationSchemeID ORDER BY SwitchID, Sequence",
                queryParameters) ?? new List<SwitchBranchVectorRecord>();

            foreach (var node in nodes)
            {
                node.InLinks = new HashSet<Link>();
                node.OutLinks = new HashSet<Link>();
            }

            var nodeById = nodes
                .Where(node => node.ID.HasValue)
                .ToDictionary(node => node.ID!.Value);
            foreach (var link in links)
            {
                if (link.FromNodeID.HasValue && nodeById.TryGetValue(link.FromNodeID.Value, out var fromNode))
                {
                    link.FromNode = fromNode;
                    fromNode.OutLinks.Add(link);
                }

                if (link.ToNodeID.HasValue && nodeById.TryGetValue(link.ToNodeID.Value, out var toNode))
                {
                    link.ToNode = toNode;
                    toNode.InLinks.Add(link);
                }
            }

            var linkById = links
                .Where(link => link.ID.HasValue)
                .ToDictionary(link => link.ID!.Value.ToString(), StringComparer.Ordinal);
            var switchById = switches
                .Where(sw => !string.IsNullOrWhiteSpace(sw.ID))
                .ToDictionary(sw => sw.ID, StringComparer.Ordinal);

            foreach (var sw in switches)
            {
                var bindingNode = ResolveNodeFromStringId(sw.BindingNodeID, nodeById);
                sw.BindingNode = bindingNode ?? new Node
                {
                    InstanceID = sw.InstanceID,
                    StationSchemeID = sw.StationSchemeID
                };
                sw.BranchVectorList = new List<SwitchBranchVector>();
            }

            foreach (var branchVector in branchVectors)
            {
                if (string.IsNullOrWhiteSpace(branchVector.SwitchID) || !switchById.TryGetValue(branchVector.SwitchID, out var bindingSwitch))
                {
                    continue;
                }

                bindingSwitch.BranchVectorList.Add(new SwitchBranchVector
                {
                    X = branchVector.X,
                    Y = branchVector.Y,
                    BindingLinkID = branchVector.BindingLinkID,
                    BindingLink = !string.IsNullOrWhiteSpace(branchVector.BindingLinkID) && linkById.TryGetValue(branchVector.BindingLinkID, out var bindingLink)
                        ? bindingLink
                        : null
                });
            }

            return new StationScheme
            {
                InstanceID = stationSchemeHeader.InstanceID,
                ID = stationSchemeHeader.ID,
                Name = stationSchemeHeader.Name,
                Signals = signals,
                InsulationJoints = insulationJoints,
                Platforms = platforms,
                LayoutSwitches = switches,
                Nodes = nodes.ToHashSet(),
                Links = links.ToHashSet(),
                Cells = new HashSet<Cell>(),
                Routes = new HashSet<SwitchYard.Capacity.Route>(),
                AdjecentSegments = new HashSet<Segment>()
            };
        }

        private static StationLayoutPayload BuildStationLayoutPayload(StationScheme stationScheme)
        {
            var nodes = (stationScheme.Nodes ?? new HashSet<Node>())
                .OrderBy(node => node.ID ?? int.MaxValue)
                .ToList();
            var links = (stationScheme.Links ?? new HashSet<Link>())
                .OrderBy(link => link.ID ?? int.MaxValue)
                .ToList();
            var nodeById = nodes
                .Where(node => node.ID.HasValue)
                .ToDictionary(node => node.ID!.Value);

            var adjacentLineIdsByNodeId = new Dictionary<int, List<string>>();
            foreach (var link in links)
            {
                var lineId = link.ID?.ToString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(lineId))
                {
                    continue;
                }

                if (link.FromNodeID.HasValue)
                {
                    AddAdjacentLineId(adjacentLineIdsByNodeId, link.FromNodeID.Value, lineId);
                }

                if (link.ToNodeID.HasValue)
                {
                    AddAdjacentLineId(adjacentLineIdsByNodeId, link.ToNodeID.Value, lineId);
                }
            }

            var trackDtos = links
                .Where(link => link.FromNode?.X.HasValue == true && link.FromNode?.Y.HasValue == true && link.ToNode?.X.HasValue == true && link.ToNode?.Y.HasValue == true)
                .Select(link => new TrackLayoutDto
                {
                    Id = link.ID?.ToString(),
                    X1 = link.FromNode!.X!.Value,
                    Y1 = link.FromNode.Y!.Value,
                    X2 = link.ToNode!.X!.Value,
                    Y2 = link.ToNode.Y!.Value,
                    FromNodeID = link.FromNodeID?.ToString(),
                    ToNodeID = link.ToNodeID?.ToString()
                })
                .ToList();

            var nodeDtos = nodes.Select(node => new NodeLayoutDto
            {
                Id = node.ID?.ToString(),
                X = node.X ?? 0,
                Y = node.Y ?? 0,
                AdjacentLineIDList = node.ID.HasValue && adjacentLineIdsByNodeId.TryGetValue(node.ID.Value, out var adjacentLineIds)
                    ? adjacentLineIds
                    : new List<string>()
            }).ToList();

            var signalDtos = stationScheme.Signals
                .OrderBy(signal => signal.ID, StringComparer.Ordinal)
                .Select(signal => new SignalLayoutDto
                {
                    Id = signal.ID,
                    Type = signal.Type,
                    Direction = signal.Direction,
                    BindingNodeID = signal.BindingNodeID,
                    Position = BuildPositionDto(signal.BindingNodeID, nodeById)
                })
                .ToList();

            var insulationJointDtos = stationScheme.InsulationJoints
                .OrderBy(joint => joint.ID, StringComparer.Ordinal)
                .Select(joint => new InsulationJointLayoutDto
                {
                    Id = joint.ID,
                    Type = joint.Type,
                    BindingNodeID = joint.BindingNodeID,
                    Position = BuildPositionDto(joint.BindingNodeID, nodeById)
                })
                .ToList();

            var platformDtos = stationScheme.Platforms
                .OrderBy(platform => platform.ID, StringComparer.Ordinal)
                .Select(platform => new PlatformLayoutDto
                {
                    Id = platform.ID,
                    Name = platform.Name,
                    X = platform.X ?? 0,
                    Y = platform.Y ?? 0,
                    Width = platform.Width ?? 0,
                    Height = platform.Height ?? 0
                })
                .ToList();

            var switchDtos = stationScheme.LayoutSwitches
                .OrderBy(sw => sw.ID, StringComparer.Ordinal)
                .Select(sw => new SwitchLayoutDto
                {
                    Id = sw.ID,
                    Type = sw.Type,
                    BindingNodeID = sw.BindingNodeID,
                    Position = BuildPositionDto(sw.BindingNodeID, nodeById),
                    BranchVectorList = sw.BranchVectorList
                        .Select(vector => new SwitchBranchVectorDto
                        {
                            X = vector.X ?? 0,
                            Y = vector.Y ?? 0,
                            LineID = vector.BindingLinkID,
                            BindingLinkID = vector.BindingLinkID
                        })
                        .ToList()
                })
                .ToList();

            return new StationLayoutPayload
            {
                Metadata = new StationLayoutMetadata
                {
                    LatestElementID = ComputeNextElementId(trackDtos, nodeDtos, signalDtos, insulationJointDtos, platformDtos, switchDtos),
                    InstanceID = stationScheme.InstanceID,
                    StationSchemeID = stationScheme.ID,
                    ID = stationScheme.ID,
                    Name = stationScheme.Name
                },
                Tracks = trackDtos,
                Nodes = nodeDtos,
                Signals = signalDtos,
                InsulationJoints = insulationJointDtos,
                Platforms = platformDtos,
                Switches = switchDtos
            };
        }

        private static void AddAdjacentLineId(Dictionary<int, List<string>> adjacentLineIdsByNodeId, int nodeId, string lineId)
        {
            if (!adjacentLineIdsByNodeId.TryGetValue(nodeId, out var adjacentLineIds))
            {
                adjacentLineIds = new List<string>();
                adjacentLineIdsByNodeId[nodeId] = adjacentLineIds;
            }

            adjacentLineIds.Add(lineId);
        }

        private static Node? ResolveNodeFromStringId(string? nodeId, IReadOnlyDictionary<int, Node> nodeById)
        {
            return int.TryParse(nodeId, out var parsedNodeId) && nodeById.TryGetValue(parsedNodeId, out var bindingNode)
                ? bindingNode
                : null;
        }

        private static PositionDto BuildPositionDto(string? nodeId, IReadOnlyDictionary<int, Node> nodeById)
        {
            var bindingNode = ResolveNodeFromStringId(nodeId, nodeById);
            return new PositionDto
            {
                X = bindingNode?.X ?? 0,
                Y = bindingNode?.Y ?? 0
            };
        }

        private static int ComputeNextElementId(
            IEnumerable<TrackLayoutDto> tracks,
            IEnumerable<NodeLayoutDto> nodes,
            IEnumerable<SignalLayoutDto> signals,
            IEnumerable<InsulationJointLayoutDto> insulationJoints,
            IEnumerable<PlatformLayoutDto> platforms,
            IEnumerable<SwitchLayoutDto> switches)
        {
            var maxId = -1;

            foreach (var id in tracks.Select(track => track.Id)
                .Concat(nodes.Select(node => node.Id))
                .Concat(signals.Select(signal => signal.Id))
                .Concat(insulationJoints.Select(joint => joint.Id))
                .Concat(platforms.Select(platform => platform.Id))
                .Concat(switches.Select(sw => sw.Id)))
            {
                if (int.TryParse(id, out var parsedId))
                {
                    maxId = Math.Max(maxId, parsedId);
                }
            }

            return maxId + 1;
        }

        private static int InitializeNodes(
            IReadOnlyList<NodeLayoutDto>? nodeDtos,
            string instanceId,
            string stationSchemeId,
            List<Node> resolvedNodes,
            Dictionary<string, int> nodeIdMap,
            Dictionary<int, Node> nodeById)
        {
            var nextNodeId = 1;
            var usedNodeIds = new HashSet<int>();

            foreach (var nodeDto in nodeDtos ?? Enumerable.Empty<NodeLayoutDto>())
            {
                var resolvedId = ResolveOrAllocateIntegerId(nodeDto.Id, usedNodeIds, ref nextNodeId, "node");
                ValidateFiniteCoordinate(nodeDto.X, "node.x");
                ValidateFiniteCoordinate(nodeDto.Y, "node.y");

                if (!string.IsNullOrWhiteSpace(nodeDto.Id) && !nodeIdMap.TryAdd(nodeDto.Id, resolvedId))
                {
                    throw new ArgumentException($"Duplicate node id '{nodeDto.Id}' was found in the station layout payload.");
                }

                var node = new Node
                {
                    InstanceID = instanceId,
                    StationSchemeID = stationSchemeId,
                    ID = resolvedId,
                    X = nodeDto.X,
                    Y = nodeDto.Y,
                    InLinks = new HashSet<Link>(),
                    OutLinks = new HashSet<Link>()
                };

                resolvedNodes.Add(node);
                nodeById[resolvedId] = node;
            }

            return nextNodeId;
        }

        private static (int NextLinkId, int NextNodeId) InitializeLinks(
            IReadOnlyList<TrackLayoutDto>? trackDtos,
            string instanceId,
            string stationSchemeId,
            List<Node> resolvedNodes,
            Dictionary<string, int> nodeIdMap,
            Dictionary<int, Node> nodeById,
            List<Link> resolvedLinks,
            Dictionary<string, int> linkIdMap,
            int nextNodeId)
        {
            var nextLinkId = 1;
            var usedLinkIds = new HashSet<int>();

            foreach (var trackDto in trackDtos ?? Enumerable.Empty<TrackLayoutDto>())
            {
                ValidateFiniteCoordinate(trackDto.X1, "track.x1");
                ValidateFiniteCoordinate(trackDto.Y1, "track.y1");
                ValidateFiniteCoordinate(trackDto.X2, "track.x2");
                ValidateFiniteCoordinate(trackDto.Y2, "track.y2");

                var fromNode = ResolveOrCreateNode(
                    trackDto.FromNodeID,
                    trackDto.X1,
                    trackDto.Y1,
                    instanceId,
                    stationSchemeId,
                    resolvedNodes,
                    nodeIdMap,
                    nodeById,
                    ref nextNodeId);

                var toNode = ResolveOrCreateNode(
                    trackDto.ToNodeID,
                    trackDto.X2,
                    trackDto.Y2,
                    instanceId,
                    stationSchemeId,
                    resolvedNodes,
                    nodeIdMap,
                    nodeById,
                    ref nextNodeId);

                var resolvedLinkId = ResolveOrAllocateIntegerId(trackDto.Id, usedLinkIds, ref nextLinkId, "track");
                if (!string.IsNullOrWhiteSpace(trackDto.Id) && !linkIdMap.TryAdd(trackDto.Id, resolvedLinkId))
                {
                    throw new ArgumentException($"Duplicate track id '{trackDto.Id}' was found in the station layout payload.");
                }

                var link = new Link
                {
                    InstanceID = instanceId,
                    StationSchemeID = stationSchemeId,
                    ID = resolvedLinkId,
                    FromNodeID = fromNode.ID,
                    ToNodeID = toNode.ID,
                    FromNode = fromNode,
                    ToNode = toNode
                };

                fromNode.OutLinks.Add(link);
                toNode.InLinks.Add(link);
                resolvedLinks.Add(link);
            }

            return (nextLinkId, nextNodeId);
        }

        private static Signal BuildSignal(
            SignalLayoutDto signalDto,
            string instanceId,
            string stationSchemeId,
            List<Node> resolvedNodes,
            Dictionary<string, int> nodeIdMap,
            Dictionary<int, Node> nodeById,
            ref int nextNodeId)
        {
            var bindingNode = ResolveBindingNode(
                signalDto.BindingNodeID,
                signalDto.Position,
                instanceId,
                stationSchemeId,
                resolvedNodes,
                nodeIdMap,
                nodeById,
                ref nextNodeId);

            return new Signal
            {
                InstanceID = instanceId,
                StationSchemeID = stationSchemeId,
                ID = FirstNonEmpty(signalDto.Id, $"signal_{bindingNode.ID}"),
                Type = signalDto.Type,
                Direction = signalDto.Direction,
                BindingNodeID = bindingNode.ID?.ToString()
            };
        }

        private static InsulationJoint BuildInsulationJoint(
            InsulationJointLayoutDto jointDto,
            string instanceId,
            string stationSchemeId,
            List<Node> resolvedNodes,
            Dictionary<string, int> nodeIdMap,
            Dictionary<int, Node> nodeById,
            ref int nextNodeId)
        {
            var bindingNode = ResolveBindingNode(
                jointDto.BindingNodeID,
                jointDto.Position,
                instanceId,
                stationSchemeId,
                resolvedNodes,
                nodeIdMap,
                nodeById,
                ref nextNodeId);

            return new InsulationJoint
            {
                InstanceID = instanceId,
                StationSchemeID = stationSchemeId,
                ID = FirstNonEmpty(jointDto.Id, $"joint_{bindingNode.ID}"),
                Type = jointDto.Type,
                BindingNodeID = bindingNode.ID?.ToString()
            };
        }

        private static Platform BuildPlatform(PlatformLayoutDto platformDto, string instanceId, string stationSchemeId)
        {
            ValidateFiniteCoordinate(platformDto.X, "platform.x");
            ValidateFiniteCoordinate(platformDto.Y, "platform.y");
            ValidateFiniteCoordinate(platformDto.Width, "platform.width");
            ValidateFiniteCoordinate(platformDto.Height, "platform.height");

            return new Platform
            {
                InstanceID = instanceId,
                StationSchemeID = stationSchemeId,
                ID = FirstNonEmpty(platformDto.Id, Guid.NewGuid().ToString("N")),
                Name = platformDto.Name,
                X = platformDto.X,
                Y = platformDto.Y,
                Width = platformDto.Width,
                Height = platformDto.Height
            };
        }

        private static Switch BuildSwitch(
            SwitchLayoutDto switchDto,
            string instanceId,
            string stationSchemeId,
            List<Node> resolvedNodes,
            Dictionary<string, int> nodeIdMap,
            Dictionary<int, Node> nodeById,
            List<Link> resolvedLinks,
            Dictionary<string, int> linkIdMap,
            ref int nextNodeId)
        {
            var bindingNode = ResolveBindingNode(
                switchDto.BindingNodeID,
                switchDto.Position,
                instanceId,
                stationSchemeId,
                resolvedNodes,
                nodeIdMap,
                nodeById,
                ref nextNodeId);

            var branchVectors = (switchDto.BranchVectorList?.Count ?? 0) > 0
                ? switchDto.BranchVectorList!
                    .Select(vectorDto => BuildSwitchBranchVector(vectorDto, resolvedLinks, linkIdMap))
                    .ToList()
                : BuildFallbackSwitchBranchVectors(bindingNode, resolvedLinks);

            return new Switch
            {
                InstanceID = instanceId,
                StationSchemeID = stationSchemeId,
                ID = FirstNonEmpty(switchDto.Id, $"switch_{bindingNode.ID}"),
                Type = switchDto.Type ?? "unknown",
                BindingNodeID = bindingNode.ID?.ToString() ?? string.Empty,
                BindingNode = bindingNode,
                BranchVectorList = branchVectors
            };
        }

        private static SwitchBranchVector BuildSwitchBranchVector(
            SwitchBranchVectorDto vectorDto,
            List<Link> resolvedLinks,
            Dictionary<string, int> linkIdMap)
        {
            ValidateFiniteCoordinate(vectorDto.X, "switch.branchVector.x");
            ValidateFiniteCoordinate(vectorDto.Y, "switch.branchVector.y");

            var bindingLinkID = ResolveLinkId(vectorDto.BindingLinkID, vectorDto.LineID, linkIdMap);
            Link? bindingLink = null;
            if (!string.IsNullOrWhiteSpace(bindingLinkID) && int.TryParse(bindingLinkID, out var numericLinkId))
            {
                bindingLink = resolvedLinks.FirstOrDefault(link => link.ID == numericLinkId);
            }

            return new SwitchBranchVector
            {
                X = vectorDto.X,
                Y = vectorDto.Y,
                BindingLinkID = bindingLinkID,
                BindingLink = bindingLink
            };
        }

        private static List<SwitchBranchVector> BuildFallbackSwitchBranchVectors(Node bindingNode, IEnumerable<Link> links)
        {
            var bindingNodeId = bindingNode.ID;
            if (bindingNodeId == null)
            {
                return new List<SwitchBranchVector>();
            }

            var vectors = new List<SwitchBranchVector>();
            foreach (var link in links.Where(link => link.FromNodeID == bindingNodeId || link.ToNodeID == bindingNodeId))
            {
                var otherNode = link.FromNodeID == bindingNodeId ? link.ToNode : link.FromNode;
                if (otherNode?.X == null || otherNode.Y == null || bindingNode.X == null || bindingNode.Y == null)
                {
                    continue;
                }

                vectors.Add(new SwitchBranchVector
                {
                    X = otherNode.X - bindingNode.X,
                    Y = otherNode.Y - bindingNode.Y,
                    BindingLinkID = link.ID?.ToString(),
                    BindingLink = link
                });
            }

            return vectors;
        }

        private static Node ResolveBindingNode(
            string? bindingNodeId,
            PositionDto? position,
            string instanceId,
            string stationSchemeId,
            List<Node> resolvedNodes,
            Dictionary<string, int> nodeIdMap,
            Dictionary<int, Node> nodeById,
            ref int nextNodeId)
        {
            if (!string.IsNullOrWhiteSpace(bindingNodeId) && nodeIdMap.TryGetValue(bindingNodeId, out var resolvedNodeId))
            {
                return nodeById[resolvedNodeId];
            }

            if (position != null)
            {
                ValidateFiniteCoordinate(position.X, "binding.position.x");
                ValidateFiniteCoordinate(position.Y, "binding.position.y");
                return ResolveOrCreateNode(
                    bindingNodeId,
                    position.X,
                    position.Y,
                    instanceId,
                    stationSchemeId,
                    resolvedNodes,
                    nodeIdMap,
                    nodeById,
                    ref nextNodeId);
            }

            throw new ArgumentException("A layout equipment item is missing both bindingNodeID and position.");
        }

        private static Node ResolveOrCreateNode(
            string? originalNodeId,
            double x,
            double y,
            string instanceId,
            string stationSchemeId,
            List<Node> resolvedNodes,
            Dictionary<string, int> nodeIdMap,
            Dictionary<int, Node> nodeById,
            ref int nextNodeId)
        {
            ValidateFiniteCoordinate(x, "node.x");
            ValidateFiniteCoordinate(y, "node.y");

            if (!string.IsNullOrWhiteSpace(originalNodeId) && nodeIdMap.TryGetValue(originalNodeId, out var resolvedNodeId))
            {
                return nodeById[resolvedNodeId];
            }

            var existingNode = resolvedNodes.FirstOrDefault(node =>
                node.X.HasValue &&
                node.Y.HasValue &&
                Math.Abs(node.X.Value - x) <= CoordinateMatchTolerance &&
                Math.Abs(node.Y.Value - y) <= CoordinateMatchTolerance);

            if (existingNode != null)
            {
                if (!string.IsNullOrWhiteSpace(originalNodeId))
                {
                    nodeIdMap[originalNodeId] = existingNode.ID!.Value;
                }

                return existingNode;
            }

            var createdNode = new Node
            {
                InstanceID = instanceId,
                StationSchemeID = stationSchemeId,
                ID = nextNodeId++,
                X = x,
                Y = y,
                InLinks = new HashSet<Link>(),
                OutLinks = new HashSet<Link>()
            };

            resolvedNodes.Add(createdNode);
            nodeById[createdNode.ID.Value] = createdNode;
            if (!string.IsNullOrWhiteSpace(originalNodeId))
            {
                nodeIdMap[originalNodeId] = createdNode.ID.Value;
            }

            return createdNode;
        }

        private static int ResolveOrAllocateIntegerId(
            string? originalId,
            HashSet<int> usedIds,
            ref int nextId,
            string entityName)
        {
            if (!string.IsNullOrWhiteSpace(originalId) && int.TryParse(originalId, out var numericId) && usedIds.Add(numericId))
            {
                nextId = Math.Max(nextId, numericId + 1);
                return numericId;
            }

            while (!usedIds.Add(nextId))
            {
                nextId += 1;
            }

            var allocatedId = nextId;
            nextId += 1;
            return allocatedId;
        }

        private static void ValidateFiniteCoordinate(double value, string fieldName)
        {
            if (!double.IsFinite(value))
            {
                throw new ArgumentException($"Field '{fieldName}' must be a finite number.");
            }
        }

        private static string? ResolveLinkId(string? bindingLinkId, string? lineId, Dictionary<string, int> linkIdMap)
        {
            if (!string.IsNullOrWhiteSpace(bindingLinkId))
            {
                return bindingLinkId;
            }

            if (!string.IsNullOrWhiteSpace(lineId) && linkIdMap.TryGetValue(lineId, out var resolvedLinkId))
            {
                return resolvedLinkId.ToString();
            }

            return null;
        }

        private void EnsureCapacityStationSchemeTables(DBConnector dbConnector)
        {
            var sqlStatements = DBConnector.IsSqlite(DBConnector.CapacityDatabaseSectionName)
                ? GetSqliteCapacityStationSchemeTableStatements()
                : GetMySqlCapacityStationSchemeTableStatements();

            foreach (var sql in sqlStatements)
            {
                dbConnector.ExecuteNonQuery(sql);
            }
        }

        private static IReadOnlyList<string> GetMySqlCapacityStationSchemeTableStatements()
        {
            return new[]
            {
                """
                CREATE TABLE IF NOT EXISTS `capacityinstance` (
                    `ID` VARCHAR(50) NULL,
                    `Name` VARCHAR(100) NULL,
                    `Owner` VARCHAR(50) NULL,
                    `CreatedDate` DATETIME NULL,
                    `IsActive` TINYINT NULL
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
                """,
                """
                CREATE TABLE IF NOT EXISTS `stationscheme` (
                    `InstanceID` VARCHAR(50) NULL,
                    `ID` VARCHAR(50) NULL,
                    `Name` VARCHAR(100) NULL
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
                """,
                """
                CREATE TABLE IF NOT EXISTS `node` (
                    `InstanceID` VARCHAR(50) NULL,
                    `StationSchemeID` VARCHAR(50) NULL,
                    `ID` INT NULL,
                    `X` DOUBLE NULL,
                    `Y` DOUBLE NULL
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
                """,
                """
                CREATE TABLE IF NOT EXISTS `link` (
                    `InstanceID` VARCHAR(50) NULL,
                    `StationSchemeID` VARCHAR(50) NULL,
                    `ID` INT NULL,
                    `FromNodeID` INT NULL,
                    `ToNodeID` INT NULL
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
                """,
                """
                CREATE TABLE IF NOT EXISTS `cell` (
                    `InstanceID` VARCHAR(50) NULL,
                    `StationSchemeID` VARCHAR(50) NULL,
                    `ID` VARCHAR(50) NULL,
                    `LinkIDList` VARCHAR(255) NULL
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
                """,
                """
                CREATE TABLE IF NOT EXISTS `route` (
                    `InstanceID` VARCHAR(50) NULL,
                    `StationSchemeID` VARCHAR(50) NULL,
                    `ID` VARCHAR(50) NULL,
                    `LinkIDList` VARCHAR(255) NULL,
                    `CellIDList` VARCHAR(255) NULL,
                    `Tag` VARCHAR(255) NULL
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
                """,
                """
                CREATE TABLE IF NOT EXISTS `stationroutetime` (
                    `InstanceID` VARCHAR(50) NULL,
                    `StationSchemeID` VARCHAR(50) NULL,
                    `RouteID` VARCHAR(50) NULL,
                    `CellID` VARCHAR(50) NULL,
                    `StartOccupationShift` INT NULL,
                    `EndOccupationShift` INT NULL
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
                """,
                """
                CREATE TABLE IF NOT EXISTS `segment` (
                    `InstanceID` VARCHAR(50) NULL,
                    `ID` VARCHAR(50) NULL,
                    `FromStationID` VARCHAR(50) NULL,
                    `ToStationID` VARCHAR(50) NULL,
                    `Name` VARCHAR(100) NULL
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
                """,
                """
                CREATE TABLE IF NOT EXISTS `signal` (
                    `InstanceID` VARCHAR(50) NULL,
                    `StationSchemeID` VARCHAR(50) NULL,
                    `ID` VARCHAR(50) NULL,
                    `Type` VARCHAR(50) NULL,
                    `Direction` VARCHAR(20) NULL,
                    `BindingNodeID` VARCHAR(50) NULL
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
                """,
                """
                CREATE TABLE IF NOT EXISTS `insulationjoint` (
                    `InstanceID` VARCHAR(50) NULL,
                    `StationSchemeID` VARCHAR(50) NULL,
                    `ID` VARCHAR(50) NULL,
                    `Type` VARCHAR(50) NULL,
                    `BindingNodeID` VARCHAR(50) NULL
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
                """,
                """
                CREATE TABLE IF NOT EXISTS `platform` (
                    `InstanceID` VARCHAR(50) NULL,
                    `StationSchemeID` VARCHAR(50) NULL,
                    `ID` VARCHAR(50) NULL,
                    `Name` VARCHAR(100) NULL,
                    `X` DOUBLE NULL,
                    `Y` DOUBLE NULL,
                    `Width` DOUBLE NULL,
                    `Height` DOUBLE NULL
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
                """,
                """
                CREATE TABLE IF NOT EXISTS `switch` (
                    `InstanceID` VARCHAR(50) NULL,
                    `StationSchemeID` VARCHAR(50) NULL,
                    `ID` VARCHAR(50) NULL,
                    `Type` VARCHAR(50) NULL,
                    `BindingNodeID` VARCHAR(50) NULL
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
                """,
                """
                CREATE TABLE IF NOT EXISTS `switchbranchvector` (
                    `InstanceID` VARCHAR(50) NULL,
                    `StationSchemeID` VARCHAR(50) NULL,
                    `SwitchID` VARCHAR(50) NULL,
                    `Sequence` INT NULL,
                    `X` DOUBLE NULL,
                    `Y` DOUBLE NULL,
                    `BindingLinkID` VARCHAR(50) NULL
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
                """
            };
        }

        private static IReadOnlyList<string> GetSqliteCapacityStationSchemeTableStatements()
        {
            return new[]
            {
                """
                CREATE TABLE IF NOT EXISTS "capacityinstance" (
                    "ID" TEXT NULL,
                    "Name" TEXT NULL,
                    "Owner" TEXT NULL,
                    "CreatedDate" DATETIME NULL,
                    "IsActive" INTEGER NULL
                )
                """,
                """
                CREATE TABLE IF NOT EXISTS "stationscheme" (
                    "InstanceID" TEXT NULL,
                    "ID" TEXT NULL,
                    "Name" TEXT NULL
                )
                """,
                """
                CREATE TABLE IF NOT EXISTS "node" (
                    "InstanceID" TEXT NULL,
                    "StationSchemeID" TEXT NULL,
                    "ID" INTEGER NULL,
                    "X" REAL NULL,
                    "Y" REAL NULL
                )
                """,
                """
                CREATE TABLE IF NOT EXISTS "link" (
                    "InstanceID" TEXT NULL,
                    "StationSchemeID" TEXT NULL,
                    "ID" INTEGER NULL,
                    "FromNodeID" INTEGER NULL,
                    "ToNodeID" INTEGER NULL
                )
                """,
                """
                CREATE TABLE IF NOT EXISTS "cell" (
                    "InstanceID" TEXT NULL,
                    "StationSchemeID" TEXT NULL,
                    "ID" TEXT NULL,
                    "LinkIDList" TEXT NULL
                )
                """,
                """
                CREATE TABLE IF NOT EXISTS "route" (
                    "InstanceID" TEXT NULL,
                    "StationSchemeID" TEXT NULL,
                    "ID" TEXT NULL,
                    "LinkIDList" TEXT NULL,
                    "CellIDList" TEXT NULL,
                    "Tag" TEXT NULL
                )
                """,
                """
                CREATE TABLE IF NOT EXISTS "stationroutetime" (
                    "InstanceID" TEXT NULL,
                    "StationSchemeID" TEXT NULL,
                    "RouteID" TEXT NULL,
                    "CellID" TEXT NULL,
                    "StartOccupationShift" INTEGER NULL,
                    "EndOccupationShift" INTEGER NULL
                )
                """,
                """
                CREATE TABLE IF NOT EXISTS "segment" (
                    "InstanceID" TEXT NULL,
                    "ID" TEXT NULL,
                    "FromStationID" TEXT NULL,
                    "ToStationID" TEXT NULL,
                    "Name" TEXT NULL
                )
                """,
                """
                CREATE TABLE IF NOT EXISTS "signal" (
                    "InstanceID" TEXT NULL,
                    "StationSchemeID" TEXT NULL,
                    "ID" TEXT NULL,
                    "Type" TEXT NULL,
                    "Direction" TEXT NULL,
                    "BindingNodeID" TEXT NULL
                )
                """,
                """
                CREATE TABLE IF NOT EXISTS "insulationjoint" (
                    "InstanceID" TEXT NULL,
                    "StationSchemeID" TEXT NULL,
                    "ID" TEXT NULL,
                    "Type" TEXT NULL,
                    "BindingNodeID" TEXT NULL
                )
                """,
                """
                CREATE TABLE IF NOT EXISTS "platform" (
                    "InstanceID" TEXT NULL,
                    "StationSchemeID" TEXT NULL,
                    "ID" TEXT NULL,
                    "Name" TEXT NULL,
                    "X" REAL NULL,
                    "Y" REAL NULL,
                    "Width" REAL NULL,
                    "Height" REAL NULL
                )
                """,
                """
                CREATE TABLE IF NOT EXISTS "switch" (
                    "InstanceID" TEXT NULL,
                    "StationSchemeID" TEXT NULL,
                    "ID" TEXT NULL,
                    "Type" TEXT NULL,
                    "BindingNodeID" TEXT NULL
                )
                """,
                """
                CREATE TABLE IF NOT EXISTS "switchbranchvector" (
                    "InstanceID" TEXT NULL,
                    "StationSchemeID" TEXT NULL,
                    "SwitchID" TEXT NULL,
                    "Sequence" INTEGER NULL,
                    "X" REAL NULL,
                    "Y" REAL NULL,
                    "BindingLinkID" TEXT NULL
                )
                """
            };
        }

        private static void InsertNodes(DBConnector dbConnector, StationScheme stationScheme)
        {
            var nodeRows = (stationScheme.Nodes ?? Enumerable.Empty<Node>())
                .Select(node => new
                {
                    node.InstanceID,
                    node.StationSchemeID,
                    node.ID,
                    node.X,
                    node.Y
                })
                .ToList();

            if (nodeRows.Count == 0)
            {
                return;
            }

            RequireWriteSuccess(
                dbConnector.ExecuteNonQuery(
                    "INSERT INTO node (InstanceID, StationSchemeID, ID, X, Y) VALUES (@InstanceID, @StationSchemeID, @ID, @X, @Y)",
                    nodeRows),
                "node",
                nodeRows.Count);
        }

        private static void InsertLinks(DBConnector dbConnector, StationScheme stationScheme)
        {
            var linkRows = (stationScheme.Links ?? Enumerable.Empty<Link>())
                .Select(link => new
                {
                    link.InstanceID,
                    link.StationSchemeID,
                    link.ID,
                    link.FromNodeID,
                    link.ToNodeID
                })
                .ToList();

            if (linkRows.Count == 0)
            {
                return;
            }

            RequireWriteSuccess(
                dbConnector.ExecuteNonQuery(
                    "INSERT INTO link (InstanceID, StationSchemeID, ID, FromNodeID, ToNodeID) VALUES (@InstanceID, @StationSchemeID, @ID, @FromNodeID, @ToNodeID)",
                    linkRows),
                "link",
                linkRows.Count);
        }

        private static void InsertSignals(DBConnector dbConnector, StationScheme stationScheme)
        {
            if (stationScheme.Signals.Count == 0)
            {
                return;
            }

            RequireWriteSuccess(
                dbConnector.ExecuteNonQuery(
                    "INSERT INTO `signal` (InstanceID, StationSchemeID, ID, Type, Direction, BindingNodeID) VALUES (@InstanceID, @StationSchemeID, @ID, @Type, @Direction, @BindingNodeID)",
                    stationScheme.Signals),
                "signal",
                stationScheme.Signals.Count);
        }

        private static void InsertInsulationJoints(DBConnector dbConnector, StationScheme stationScheme)
        {
            if (stationScheme.InsulationJoints.Count == 0)
            {
                return;
            }

            RequireWriteSuccess(
                dbConnector.ExecuteNonQuery(
                    "INSERT INTO insulationjoint (InstanceID, StationSchemeID, ID, Type, BindingNodeID) VALUES (@InstanceID, @StationSchemeID, @ID, @Type, @BindingNodeID)",
                    stationScheme.InsulationJoints),
                "insulationjoint",
                stationScheme.InsulationJoints.Count);
        }

        private static void InsertPlatforms(DBConnector dbConnector, StationScheme stationScheme)
        {
            if (stationScheme.Platforms.Count == 0)
            {
                return;
            }

            RequireWriteSuccess(
                dbConnector.ExecuteNonQuery(
                    "INSERT INTO platform (InstanceID, StationSchemeID, ID, Name, X, Y, Width, Height) VALUES (@InstanceID, @StationSchemeID, @ID, @Name, @X, @Y, @Width, @Height)",
                    stationScheme.Platforms),
                "platform",
                stationScheme.Platforms.Count);
        }

        private static void InsertSwitches(DBConnector dbConnector, StationScheme stationScheme)
        {
            if (stationScheme.LayoutSwitches.Count == 0)
            {
                return;
            }

            var switchRows = stationScheme.LayoutSwitches
                .Select(sw => new
                {
                    sw.InstanceID,
                    sw.StationSchemeID,
                    sw.ID,
                    sw.Type,
                    sw.BindingNodeID
                })
                .ToList();

            RequireWriteSuccess(
                dbConnector.ExecuteNonQuery(
                    "INSERT INTO `switch` (InstanceID, StationSchemeID, ID, Type, BindingNodeID) VALUES (@InstanceID, @StationSchemeID, @ID, @Type, @BindingNodeID)",
                    switchRows),
                "switch",
                switchRows.Count);

            var branchVectorRows = stationScheme.LayoutSwitches
                .SelectMany(sw => sw.BranchVectorList.Select((vector, index) => new
                {
                    sw.InstanceID,
                    sw.StationSchemeID,
                    SwitchID = sw.ID,
                    Sequence = index,
                    vector.X,
                    vector.Y,
                    vector.BindingLinkID
                }))
                .ToList();

            if (branchVectorRows.Count == 0)
            {
                return;
            }

            RequireWriteSuccess(
                dbConnector.ExecuteNonQuery(
                    "INSERT INTO switchbranchvector (InstanceID, StationSchemeID, SwitchID, Sequence, X, Y, BindingLinkID) VALUES (@InstanceID, @StationSchemeID, @SwitchID, @Sequence, @X, @Y, @BindingLinkID)",
                    branchVectorRows),
                "switchbranchvector",
                branchVectorRows.Count);
        }

        private static void InsertCells(DBConnector dbConnector, StationScheme stationScheme)
        {
            var cellRows = (stationScheme.Cells ?? Enumerable.Empty<Cell>())
                .Select(cell => new
                {
                    cell.InstanceID,
                    cell.StationSchemeID,
                    cell.ID,
                    cell.LinkIDList
                })
                .ToList();

            if (cellRows.Count == 0)
            {
                return;
            }

            RequireWriteSuccess(
                dbConnector.ExecuteNonQuery(
                    "INSERT INTO cell (InstanceID, StationSchemeID, ID, LinkIDList) VALUES (@InstanceID, @StationSchemeID, @ID, @LinkIDList)",
                    cellRows),
                "cell",
                cellRows.Count);
        }

        private static void InsertRoutes(DBConnector dbConnector, StationScheme stationScheme)
        {
            if (stationScheme.Routes == null || stationScheme.Routes.Count == 0)
            {
                return;
            }

            var routeRows = stationScheme.Routes
                .Select(route => new
                {
                    route.InstanceID,
                    route.StationSchemeID,
                    route.ID,
                    route.LinkIDList,
                    route.CellIDList,
                    Tag = route.Tags == null || route.Tags.Count == 0 ? null : string.Join(";", route.Tags.OrderBy(tag => tag, StringComparer.Ordinal))
                })
                .ToList();

            if (routeRows.Count == 0)
            {
                return;
            }

            RequireWriteSuccess(
                dbConnector.ExecuteNonQuery(
                    "INSERT INTO route (InstanceID, StationSchemeID, ID, LinkIDList, CellIDList, Tag) VALUES (@InstanceID, @StationSchemeID, @ID, @LinkIDList, @CellIDList, @Tag)",
                    routeRows),
                "route",
                routeRows.Count);
        }

        private static void InsertStationRouteTimes(DBConnector dbConnector, StationScheme stationScheme)
        {
            var stationRouteTimes = Enumerable.Empty<StationRouteTime>().ToList();
            if (stationRouteTimes.Count == 0)
            {
                return;
            }

            RequireWriteSuccess(
                dbConnector.ExecuteNonQuery(
                    "INSERT INTO stationroutetime (InstanceID, StationSchemeID, RouteID, CellID, StartOccupationShift, EndOccupationShift) VALUES (@InstanceID, @StationSchemeID, @RouteID, @CellID, @StartOccupationShift, @EndOccupationShift)",
                    stationRouteTimes),
                "stationroutetime",
                stationRouteTimes.Count);
        }

        private static void InsertSegments(DBConnector dbConnector, StationScheme stationScheme)
        {
            var segmentRows = (stationScheme.AdjecentSegments ?? Enumerable.Empty<Segment>())
                .Select(segment => new
                {
                    segment.InstanceID,
                    segment.ID,
                    FromStationID = segment.FromStationSchemeID,
                    ToStationID = segment.ToStationSchemeID,
                    segment.Name
                })
                .ToList();

            if (segmentRows.Count == 0)
            {
                return;
            }

            RequireWriteSuccess(
                dbConnector.ExecuteNonQuery(
                    "INSERT INTO segment (InstanceID, ID, FromStationID, ToStationID, Name) VALUES (@InstanceID, @ID, @FromStationID, @ToStationID, @Name)",
                    segmentRows),
                "segment",
                segmentRows.Count);
        }

        private static void RequireWriteSuccess(int affectedRows, string tableName, int? expectedRows = null)
        {
            if (expectedRows.HasValue)
            {
                if (affectedRows != expectedRows.Value)
                {
                    throw new InvalidOperationException($"Failed to persist {tableName}. Expected to affect {expectedRows.Value} rows, but affected {affectedRows} rows.");
                }

                return;
            }

            if (affectedRows <= 0)
            {
                throw new InvalidOperationException($"Failed to persist {tableName}.");
            }
        }

        private static string FirstNonEmpty(params string?[] values)
        {
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return string.Empty;
        }

        private sealed class StationLayoutPayload
        {
            public StationLayoutMetadata? Metadata { get; set; }

            public List<TrackLayoutDto>? Tracks { get; set; }

            public List<NodeLayoutDto>? Nodes { get; set; }

            public List<SignalLayoutDto>? Signals { get; set; }

            public List<InsulationJointLayoutDto>? InsulationJoints { get; set; }

            public List<PlatformLayoutDto>? Platforms { get; set; }

            public List<SwitchLayoutDto>? Switches { get; set; }
        }

        private sealed class StationLayoutMetadata
        {
            public int LatestElementID { get; set; }

            public string? InstanceID { get; set; }

            public string? StationSchemeID { get; set; }

            public string? ID { get; set; }

            public string? Name { get; set; }
        }

        private sealed class TrackLayoutDto
        {
            public string? Id { get; set; }

            public double X1 { get; set; }

            public double Y1 { get; set; }

            public double X2 { get; set; }

            public double Y2 { get; set; }

            public string? FromNodeID { get; set; }

            public string? ToNodeID { get; set; }
        }

        private sealed class NodeLayoutDto
        {
            public string? Id { get; set; }

            public double X { get; set; }

            public double Y { get; set; }

            public List<string>? AdjacentLineIDList { get; set; }
        }

        private sealed class PositionDto
        {
            public double X { get; set; }

            public double Y { get; set; }
        }

        private sealed class SignalLayoutDto
        {
            public string? Id { get; set; }

            public string? Type { get; set; }

            public PositionDto? Position { get; set; }

            public string? Direction { get; set; }

            public string? BindingNodeID { get; set; }
        }

        private sealed class InsulationJointLayoutDto
        {
            public string? Id { get; set; }

            public string? Type { get; set; }

            public PositionDto? Position { get; set; }

            public string? BindingNodeID { get; set; }
        }

        private sealed class PlatformLayoutDto
        {
            public string? Id { get; set; }

            public string? Name { get; set; }

            public double X { get; set; }

            public double Y { get; set; }

            public double Width { get; set; }

            public double Height { get; set; }
        }

        private sealed class SwitchLayoutDto
        {
            public string? Id { get; set; }

            public string? Type { get; set; }

            public PositionDto? Position { get; set; }

            public string? BindingNodeID { get; set; }

            public List<SwitchBranchVectorDto>? BranchVectorList { get; set; }
        }

        private sealed class SwitchBranchVectorDto
        {
            public double X { get; set; }

            public double Y { get; set; }

            public string? LineID { get; set; }

            public string? BindingLinkID { get; set; }
        }

        private sealed class SwitchBranchVectorRecord
        {
            public string? InstanceID { get; set; }

            public string? StationSchemeID { get; set; }

            public string? SwitchID { get; set; }

            public int Sequence { get; set; }

            public double X { get; set; }

            public double Y { get; set; }

            public string? BindingLinkID { get; set; }
        }
    }
}
