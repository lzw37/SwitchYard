using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ACadSharp.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using SwitchYard.Capacity;

namespace SwitchYard.Service.Controllers
{
    [ApiController]
    [Route("[controller]/[action]")]
    [Authorize]
    public class StationLayoutController : ControllerBase
    {
        private readonly ILogger<StationLayoutController> _logger;
        private readonly IWebHostEnvironment _environment;

        private const long MaxDwgFileSize = 20L * 1024 * 1024; // 20 MB
        private static readonly Regex LayerNameRegex = new("^[A-Za-z0-9_\\-]{1,255}$", RegexOptions.Compiled);
        private const string DefaultCapacityInstanceId = "default_capacity_instance";
        private const string DefaultStationSchemeId = "station_layout_scheme";
        private const string DefaultStationSchemeName = "Station Layout Scheme";
        private const double CoordinateMatchTolerance = 0.001d;
        private static readonly JsonSerializerOptions LayoutJsonSerializerOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public StationLayoutController(ILogger<StationLayoutController> logger, IWebHostEnvironment environment)
        {
            _logger = logger;
            _environment = environment;
        }
        
        [HttpPost(Name ="SaveJson")]
        [Authorize(Roles = "Admin")]
        public IActionResult SaveJson([FromBody] StationLayoutSaveRequest? request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Json))
            {
                return BadRequest("Request body must contain a non-empty json field.");
            }

            if (!TryResolveStationLayoutFilePath(out var filePath, out var errorMessage))
            {
                return StatusCode(500, errorMessage);
            }

            try
            {
                string normalizedJson;
                using (var document = JsonDocument.Parse(request.Json))
                {
                    normalizedJson = JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });
                }

                var directory = Path.GetDirectoryName(filePath);
                if (string.IsNullOrWhiteSpace(directory))
                {
                    return StatusCode(500, "Unable to resolve the StationLayout.json directory.");
                }

                Directory.CreateDirectory(directory);
                System.IO.File.WriteAllText(filePath, normalizedJson, new UTF8Encoding(false));

                _logger.LogInformation("Station layout JSON saved to {FilePath}", filePath);
                return Ok("OK");
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Invalid station layout JSON payload.");
                return BadRequest("Invalid JSON payload.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save station layout JSON to {FilePath}", filePath);
                return StatusCode(500, "Failed to save StationLayout.json.");
            }
        }

        [HttpPost(Name = "GetJson")]
        public IActionResult GetJson()
        {
            if (!TryResolveStationLayoutFilePath(out var filePath, out var errorMessage))
            {
                return StatusCode(500, errorMessage);
            }

            try
            {
                if (!System.IO.File.Exists(filePath))
                {
                    var emptyLayoutJson = BuildEmptyStationLayoutJson();
                    return Content(emptyLayoutJson, "application/json", Encoding.UTF8);
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
                metadata = new { latestElementID = 0 },
                tracks = Array.Empty<object>(),
                nodes = Array.Empty<object>(),
                signals = Array.Empty<object>(),
                insulationJoints = Array.Empty<object>(),
                platforms = Array.Empty<object>(),
                switches = Array.Empty<object>()
            });
        }

        public sealed class StationLayoutSaveRequest
        {
            public string Json { get; set; } = string.Empty;

            public string? InstanceID { get; set; }

            public string? StationSchemeID { get; set; }

            public string? Name { get; set; }
        }

        public sealed class StationLayoutDatabaseRequest
        {
            public string? InstanceID { get; set; }

            public string? StationSchemeID { get; set; }
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

                return Ok(new
                {
                    message = "OK",
                    segmentCount = segments.Count
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
                metadata = new { latestElementID = segments.Count },
                tracks,
                nodes = Array.Empty<object>(),
                signals = Array.Empty<object>(),
                insulationJoints = Array.Empty<object>(),
                platforms = Array.Empty<object>(),
                switches = Array.Empty<object>()
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
