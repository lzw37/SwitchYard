using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ACadSharp.IO;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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
        private static readonly string[] NamedDeviceTables = new[] { "signal", "switch", "cell", "route", "platform" };
        private static readonly string[] StationLayoutDataTables = new[]
        {
            "switchbranchvector",
            "switch",
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
                    $@"INSERT INTO {stationSchemeTable} (InstanceID, ID, Name)
                       VALUES (@InstanceID, @ID, @Name)",
                    new
                    {
                        InstanceID = normalizedInstanceID,
                        ID = normalizedID,
                        Name = normalizedName
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
                metadata = new { latestElementID = 0 },
                tracks = Array.Empty<object>(),
                curves = Array.Empty<object>(),
                nodes = Array.Empty<object>(),
                signals = Array.Empty<object>(),
                insulationJoints = Array.Empty<object>(),
                platforms = Array.Empty<object>(),
                switches = Array.Empty<object>(),
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
                "platform",
                "switch",
                "switchbranchvector",
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
                $@"INSERT INTO {stationSchemeTable} (InstanceID, ID, Name)
                   VALUES (@InstanceID, @ID, @Name)",
                new
                {
                    InstanceID = instanceID,
                    ID = stationSchemeID,
                    Name = name
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
                $@"INSERT INTO {stationSchemeTable} (InstanceID, ID, Name)
                   VALUES (@InstanceID, @ID, @Name)",
                new
                {
                    InstanceID = instanceID,
                    ID = stationSchemeID,
                    Name = stationSchemeID == DefaultStationSchemeID ? DefaultStationSchemeName : stationSchemeID
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
            var platformTable = QuoteIdentifier("platform");
            var switchTable = QuoteIdentifier("switch");
            var switchBranchVectorTable = QuoteIdentifier("switchbranchvector");
            var annotationTable = QuoteIdentifier("annotation");

            EnsureLinkSchema(dbConnector);
            EnsureCurveSchema(dbConnector);
            EnsureNamedDeviceSchemas(dbConnector);

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
            var latestElementID = CalculateLatestElementID(
                nodes.Select(node => ToInvariantString(node.ID))
                    .Concat(links.Select(link => ToInvariantString(link.ID)))
                    .Concat(curves.Select(curve => curve.ID ?? string.Empty))
                    .Concat(signals.Select(signal => signal.ID ?? string.Empty))
                    .Concat(insulationJoints.Select(insulationJoint => insulationJoint.ID ?? string.Empty))
                    .Concat(platforms.Select(platform => platform.ID ?? string.Empty))
                    .Concat(switches.Select(sw => sw.ID ?? string.Empty))
                    .Concat(annotations.Select(annotation => annotation.ID ?? string.Empty)));

            return JsonSerializer.Serialize(new
            {
                metadata = new
                {
                    latestElementID,
                    instanceID,
                    stationSchemeID,
                    coordinateTransform = nodeTransform.ToMetadata(),
                    displayStyles
                },
                tracks = trackViews,
                curves = curveViews,
                nodes = nodeViews,
                signals = signalViews,
                insulationJoints = insulationJointViews,
                platforms = platformViews,
                switches = switchViews,
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
            var platformTable = QuoteIdentifier("platform");
            var switchTable = QuoteIdentifier("switch");
            var switchBranchVectorTable = QuoteIdentifier("switchbranchvector");
            var annotationTable = QuoteIdentifier("annotation");

            EnsureLinkSchema(dbConnector);
            EnsureCurveSchema(dbConnector);
            EnsureNamedDeviceSchemas(dbConnector);

            var transform = StationLayoutPersistenceTransform.FromMetadata(layout.Metadata?.CoordinateTransform);
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

                DeleteStationLayoutTableRows(dbConnector, switchBranchVectorTable, instanceID, stationSchemeID);
                DeleteStationLayoutTableRows(dbConnector, switchTable, instanceID, stationSchemeID);
                DeleteStationLayoutTableRows(dbConnector, insulationJointTable, instanceID, stationSchemeID);
                DeleteStationLayoutTableRows(dbConnector, signalTable, instanceID, stationSchemeID);
                DeleteStationLayoutTableRows(dbConnector, platformTable, instanceID, stationSchemeID);
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
                    PlatformCount = platformCount,
                    SwitchCount = switchSaveResult.SwitchCount,
                    SwitchBranchVectorCount = switchSaveResult.SwitchBranchVectorCount,
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
            const double canvasWidth = 1920;
            const double canvasHeight = 1080;
            const double padding = 80;

            if (nodes.Count == 0)
            {
                return StationLayoutCoordinateTransform.Identity;
            }

            var minX = nodes.Min(node => node.X);
            var maxX = nodes.Max(node => node.X);
            var minY = nodes.Min(node => node.Y);
            var maxY = nodes.Max(node => node.Y);
            var sourceWidth = Math.Max(maxX - minX, 1);
            var sourceHeight = Math.Max(maxY - minY, 1);
            var scale = Math.Min((canvasWidth - padding * 2) / sourceWidth, (canvasHeight - padding * 2) / sourceHeight);

            var alreadyCanvasCoordinates =
                minX >= 0 &&
                minY >= 0 &&
                maxX <= canvasWidth &&
                maxY <= canvasHeight &&
                scale <= 1.25;
            if (alreadyCanvasCoordinates)
            {
                return StationLayoutCoordinateTransform.Identity;
            }

            return new StationLayoutCoordinateTransform(minX, minY, scale, padding, true);
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
                            {QuoteIdentifier("DisplayStyles")} TEXT NULL
                        )");
                }
                else
                {
                    dbConnector.ExecuteNonQuery(
                        $@"CREATE TABLE IF NOT EXISTS {tableName} (
                            {QuoteIdentifier("InstanceID")} TEXT NULL,
                            {QuoteIdentifier("ID")} TEXT NULL,
                            {QuoteIdentifier("Name")} TEXT NULL,
                            {QuoteIdentifier("DisplayStyles")} TEXT NULL
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
                    ["DisplayStyles"] = "TEXT NULL"
                }
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["InstanceID"] = "TEXT NULL",
                    ["ID"] = "TEXT NULL",
                    ["Name"] = "TEXT NULL",
                    ["DisplayStyles"] = "TEXT NULL"
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

        private sealed class StationLayoutCoordinateTransform
        {
            public static readonly StationLayoutCoordinateTransform Identity = new(0, 0, 1, 0, false);

            private readonly double _minX;
            private readonly double _minY;
            private readonly double _scale;
            private readonly double _padding;
            private readonly bool _applied;

            public StationLayoutCoordinateTransform(double minX, double minY, double scale, double padding, bool applied)
            {
                _minX = minX;
                _minY = minY;
                _scale = scale;
                _padding = padding;
                _applied = applied;
            }

            public (double x, double y) MapPoint(double x, double y)
            {
                return (
                    Math.Round(_padding + (x - _minX) * _scale, 3),
                    Math.Round(_padding + (y - _minY) * _scale, 3));
            }

            public double MapLength(double length)
            {
                return Math.Round(length * _scale, 3);
            }

            public object ToMetadata()
            {
                return new
                {
                    applied = _applied,
                    minX = _minX,
                    minY = _minY,
                    scale = _scale,
                    padding = _padding
                };
            }
        }

        private sealed class StationLayoutPersistenceTransform
        {
            public static readonly StationLayoutPersistenceTransform Identity = new(0, 0, 1, 0, false);

            private readonly double _minX;
            private readonly double _minY;
            private readonly double _scale;
            private readonly double _padding;
            private readonly bool _applied;

            private StationLayoutPersistenceTransform(double minX, double minY, double scale, double padding, bool applied)
            {
                _minX = minX;
                _minY = minY;
                _scale = scale;
                _padding = padding;
                _applied = applied;
            }

            public static StationLayoutPersistenceTransform FromMetadata(StationLayoutJsonCoordinateTransform? metadata)
            {
                if (metadata == null ||
                    !metadata.Applied ||
                    !double.IsFinite(metadata.Scale) ||
                    metadata.Scale <= 0)
                {
                    return Identity;
                }

                return new StationLayoutPersistenceTransform(
                    metadata.MinX,
                    metadata.MinY,
                    metadata.Scale,
                    metadata.Padding,
                    true);
            }

            public (double x, double y) UnmapPoint(double x, double y)
            {
                if (!_applied)
                {
                    return (Math.Round(x, 3), Math.Round(y, 3));
                }

                return (
                    Math.Round(((x - _padding) / _scale) + _minX, 6),
                    Math.Round(((y - _padding) / _scale) + _minY, 6));
            }

            public double UnmapLength(double length)
            {
                if (!_applied)
                {
                    return Math.Round(length, 3);
                }

                return Math.Round(length / _scale, 6);
            }
        }

        private sealed class IntegerIdAllocator
        {
            private readonly HashSet<int> _usedIDs = new();
            private int _nextID;

            public int Allocate(string? preferredID)
            {
                if (int.TryParse(preferredID, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) &&
                    parsed >= 0 &&
                    _usedIDs.Add(parsed))
                {
                    _nextID = Math.Max(_nextID, parsed + 1);
                    return parsed;
                }

                while (_usedIDs.Contains(_nextID))
                {
                    _nextID++;
                }

                var allocated = _nextID;
                _usedIDs.Add(allocated);
                _nextID++;
                return allocated;
            }
        }

        private sealed class StationSchemeLookupRow
        {
            public string? ID { get; set; }

            public string? Name { get; set; }

            public string? DisplayStyles { get; set; }
        }

        private sealed class StationNodeRow
        {
            public int ID { get; set; }

            public double X { get; set; }

            public double Y { get; set; }
        }

        private sealed class StationLinkRow
        {
            public int ID { get; set; }

            public string? Name { get; set; }

            public string? ArrowDirection { get; set; }

            public string? ArrowType { get; set; }

            public int FromNodeID { get; set; }

            public int ToNodeID { get; set; }
        }

        private sealed class StationCurveRow
        {
            public string? ID { get; set; }

            public string? BindingNodeID { get; set; }

            public string? BindingLink1ID { get; set; }

            public string? BindingLink2ID { get; set; }

            public string? VertexNodeID { get; set; }

            public string? TangentLinkID1 { get; set; }

            public string? TangentLinkID2 { get; set; }

            public object? Radius { get; set; }

            public double Angle { get; set; }

            public double TangentDistance { get; set; }

            public double StartX { get; set; }

            public double StartY { get; set; }

            public double EndX { get; set; }

            public double EndY { get; set; }

            public double CenterX { get; set; }

            public double CenterY { get; set; }

            public int LargeArcFlag { get; set; }

            public int SweepFlag { get; set; }
        }

        private sealed class StationSignalRow
        {
            public string? ID { get; set; }

            public string? Name { get; set; }

            public string? Type { get; set; }

            public string? Direction { get; set; }

            public string? BindingNodeID { get; set; }
        }

        private sealed class StationInsulationJointRow
        {
            public string? ID { get; set; }

            public string? Type { get; set; }

            public string? BindingNodeID { get; set; }
        }

        private sealed class StationPlatformRow
        {
            public string? ID { get; set; }

            public string? Name { get; set; }

            public double X { get; set; }

            public double Y { get; set; }

            public double Width { get; set; }

            public double Height { get; set; }
        }

        private sealed class StationSwitchRow
        {
            public string? ID { get; set; }

            public string? Name { get; set; }

            public string? Type { get; set; }

            public string? BindingNodeID { get; set; }
        }

        private sealed class DatabaseNameLookupRow
        {
            public string? Name { get; set; }
        }

        private sealed class SwitchBranchVectorRow
        {
            public string? SwitchID { get; set; }

            public int Sequence { get; set; }

            public double X { get; set; }

            public double Y { get; set; }

            public string? BindingLinkID { get; set; }
        }

        private sealed class StationAnnotationRow
        {
            public string? ID { get; set; }

            public string? Text { get; set; }

            public double X { get; set; }

            public double Y { get; set; }

            public string? FontFamily { get; set; }

            public double FontSize { get; set; }

            public string? FontWeight { get; set; }

            public string? FontStyle { get; set; }

            public double Angle { get; set; }

            public string? TextColor { get; set; }
        }

        private sealed class StationLayoutJson
        {
            [JsonPropertyName("metadata")]
            public StationLayoutJsonMetadata? Metadata { get; set; }

            [JsonPropertyName("tracks")]
            public List<StationLayoutTrackJson> Tracks { get; set; } = new();

            [JsonPropertyName("curves")]
            public List<StationLayoutCurveJson> Curves { get; set; } = new();

            [JsonPropertyName("nodes")]
            public List<StationLayoutNodeJson> Nodes { get; set; } = new();

            [JsonPropertyName("signals")]
            public List<StationLayoutSignalJson> Signals { get; set; } = new();

            [JsonPropertyName("insulationJoints")]
            public List<StationLayoutInsulationJointJson> InsulationJoints { get; set; } = new();

            [JsonPropertyName("platforms")]
            public List<StationLayoutPlatformJson> Platforms { get; set; } = new();

            [JsonPropertyName("switches")]
            public List<StationLayoutSwitchJson> Switches { get; set; } = new();

            [JsonPropertyName("annotations")]
            public List<StationLayoutAnnotationJson> Annotations { get; set; } = new();
        }

        private sealed class StationLayoutJsonMetadata
        {
            [JsonPropertyName("latestElementID")]
            public int LatestElementID { get; set; }

            [JsonPropertyName("instanceID")]
            public string? InstanceID { get; set; }

            [JsonPropertyName("stationSchemeID")]
            public string? StationSchemeID { get; set; }

            [JsonPropertyName("coordinateTransform")]
            public StationLayoutJsonCoordinateTransform? CoordinateTransform { get; set; }

            [JsonPropertyName("displayStyles")]
            public JsonElement? DisplayStyles { get; set; }
        }

        private sealed class StationLayoutJsonCoordinateTransform
        {
            [JsonPropertyName("applied")]
            public bool Applied { get; set; }

            [JsonPropertyName("minX")]
            public double MinX { get; set; }

            [JsonPropertyName("minY")]
            public double MinY { get; set; }

            [JsonPropertyName("scale")]
            public double Scale { get; set; } = 1;

            [JsonPropertyName("padding")]
            public double Padding { get; set; }
        }

        private sealed class StationLayoutTrackJson
        {
            [JsonPropertyName("id")]
            public string? ID { get; set; }

            [JsonPropertyName("name")]
            public string? Name { get; set; }

            [JsonPropertyName("arrowDirection")]
            public string? ArrowDirection { get; set; }

            [JsonPropertyName("arrowType")]
            public string? ArrowType { get; set; }

            [JsonPropertyName("x1")]
            public double X1 { get; set; }

            [JsonPropertyName("y1")]
            public double Y1 { get; set; }

            [JsonPropertyName("x2")]
            public double X2 { get; set; }

            [JsonPropertyName("y2")]
            public double Y2 { get; set; }

            [JsonPropertyName("fromNodeID")]
            public string? FromNodeID { get; set; }

            [JsonPropertyName("toNodeID")]
            public string? ToNodeID { get; set; }
        }

        private sealed class StationLayoutCurveJson
        {
            [JsonPropertyName("id")]
            public string? ID { get; set; }

            [JsonPropertyName("nodeID")]
            public string? NodeID { get; set; }

            [JsonPropertyName("tangentLinkID1")]
            public string? TangentLinkID1 { get; set; }

            [JsonPropertyName("tangentLinkID2")]
            public string? TangentLinkID2 { get; set; }

            [JsonPropertyName("radius")]
            public double Radius { get; set; }

            [JsonPropertyName("angle")]
            public double Angle { get; set; }

            [JsonPropertyName("tangentDistance")]
            public double TangentDistance { get; set; }

            [JsonPropertyName("start")]
            public StationLayoutPositionJson? Start { get; set; }

            [JsonPropertyName("end")]
            public StationLayoutPositionJson? End { get; set; }

            [JsonPropertyName("center")]
            public StationLayoutPositionJson? Center { get; set; }

            [JsonPropertyName("largeArcFlag")]
            public int LargeArcFlag { get; set; }

            [JsonPropertyName("sweepFlag")]
            public int SweepFlag { get; set; }
        }

        private sealed class StationLayoutNodeJson
        {
            [JsonPropertyName("id")]
            public string? ID { get; set; }

            [JsonPropertyName("x")]
            public double X { get; set; }

            [JsonPropertyName("y")]
            public double Y { get; set; }
        }

        private sealed class StationLayoutPositionJson
        {
            [JsonPropertyName("x")]
            public double X { get; set; }

            [JsonPropertyName("y")]
            public double Y { get; set; }
        }

        private sealed class StationLayoutSignalJson
        {
            [JsonPropertyName("id")]
            public string? ID { get; set; }

            [JsonPropertyName("name")]
            public string? Name { get; set; }

            [JsonPropertyName("type")]
            public string? Type { get; set; }

            [JsonPropertyName("position")]
            public StationLayoutPositionJson? Position { get; set; }

            [JsonPropertyName("direction")]
            public string? Direction { get; set; }

            [JsonPropertyName("bindingNodeID")]
            public string? BindingNodeID { get; set; }
        }

        private sealed class StationLayoutInsulationJointJson
        {
            [JsonPropertyName("id")]
            public string? ID { get; set; }

            [JsonPropertyName("type")]
            public string? Type { get; set; }

            [JsonPropertyName("position")]
            public StationLayoutPositionJson? Position { get; set; }

            [JsonPropertyName("bindingNodeID")]
            public string? BindingNodeID { get; set; }
        }

        private sealed class StationLayoutPlatformJson
        {
            [JsonPropertyName("id")]
            public string? ID { get; set; }

            [JsonPropertyName("name")]
            public string? Name { get; set; }

            [JsonPropertyName("x")]
            public double X { get; set; }

            [JsonPropertyName("y")]
            public double Y { get; set; }

            [JsonPropertyName("width")]
            public double Width { get; set; }

            [JsonPropertyName("height")]
            public double Height { get; set; }
        }

        private sealed class StationLayoutSwitchJson
        {
            [JsonPropertyName("id")]
            public string? ID { get; set; }

            [JsonPropertyName("name")]
            public string? Name { get; set; }

            [JsonPropertyName("type")]
            public string? Type { get; set; }

            [JsonPropertyName("position")]
            public StationLayoutPositionJson? Position { get; set; }

            [JsonPropertyName("bindingNodeID")]
            public string? BindingNodeID { get; set; }

            [JsonPropertyName("branchVectorList")]
            public List<StationLayoutSwitchBranchVectorJson> BranchVectorList { get; set; } = new();
        }

        private sealed class StationLayoutSwitchBranchVectorJson
        {
            [JsonPropertyName("x")]
            public double X { get; set; }

            [JsonPropertyName("y")]
            public double Y { get; set; }

            [JsonPropertyName("lineID")]
            public string? LineID { get; set; }
        }

        private sealed class StationLayoutAnnotationJson
        {
            [JsonPropertyName("id")]
            public string? ID { get; set; }

            [JsonPropertyName("text")]
            public string? Text { get; set; }

            [JsonPropertyName("position")]
            public StationLayoutPositionJson? Position { get; set; }

            [JsonPropertyName("fontFamily")]
            public string? FontFamily { get; set; }

            [JsonPropertyName("fontSize")]
            public double FontSize { get; set; }

            [JsonPropertyName("fontWeight")]
            public string? FontWeight { get; set; }

            [JsonPropertyName("fontStyle")]
            public string? FontStyle { get; set; }

            [JsonPropertyName("angle")]
            public double Angle { get; set; }

            [JsonPropertyName("textColor")]
            public string? TextColor { get; set; }
        }

        private sealed class StationLayoutNodeSaveContext
        {
            public IntegerIdAllocator Allocator { get; set; } = new();

            public StationLayoutPersistenceTransform Transform { get; set; } = StationLayoutPersistenceTransform.Identity;

            public List<StationLayoutNodeSaveEntry> Nodes { get; } = new();

            public Dictionary<string, int> NodeIDBySourceID { get; } = new(StringComparer.Ordinal);

            public Dictionary<string, int> NodeIDByPointKey { get; } = new(StringComparer.Ordinal);
        }

        private sealed class StationLayoutNodeSaveEntry
        {
            public string SourceID { get; set; } = string.Empty;

            public int ID { get; set; }

            public double DisplayX { get; set; }

            public double DisplayY { get; set; }

            public double DatabaseX { get; set; }

            public double DatabaseY { get; set; }
        }

        private sealed class StationLayoutLinkSaveContext
        {
            public List<StationLayoutLinkSaveEntry> Links { get; } = new();

            public Dictionary<string, int> LinkIDBySourceID { get; } = new(StringComparer.Ordinal);
        }

        private sealed class StationLayoutLinkSaveEntry
        {
            public string SourceID { get; set; } = string.Empty;

            public int ID { get; set; }

            public string Name { get; set; } = string.Empty;

            public string? ArrowDirection { get; set; }

            public string? ArrowType { get; set; }

            public int FromNodeID { get; set; }

            public int ToNodeID { get; set; }
        }

        private sealed class StationLayoutSwitchSaveResult
        {
            public int SwitchCount { get; set; }

            public int SwitchBranchVectorCount { get; set; }
        }

        private sealed class StationLayoutSaveResult
        {
            [JsonPropertyName("message")]
            public string Message { get; set; } = string.Empty;

            [JsonPropertyName("instanceID")]
            public string InstanceID { get; set; } = string.Empty;

            [JsonPropertyName("stationSchemeID")]
            public string StationSchemeID { get; set; } = string.Empty;

            [JsonPropertyName("nodeCount")]
            public int NodeCount { get; set; }

            [JsonPropertyName("linkCount")]
            public int LinkCount { get; set; }

            [JsonPropertyName("curveCount")]
            public int CurveCount { get; set; }

            [JsonPropertyName("signalCount")]
            public int SignalCount { get; set; }

            [JsonPropertyName("insulationJointCount")]
            public int InsulationJointCount { get; set; }

            [JsonPropertyName("platformCount")]
            public int PlatformCount { get; set; }

            [JsonPropertyName("switchCount")]
            public int SwitchCount { get; set; }

            [JsonPropertyName("switchBranchVectorCount")]
            public int SwitchBranchVectorCount { get; set; }

            [JsonPropertyName("annotationCount")]
            public int AnnotationCount { get; set; }
        }

        public sealed class StationLayoutSaveRequest
        {
            public string Json { get; set; } = string.Empty;

            public string? InstanceID { get; set; }

            public string? StationSchemeID { get; set; }
        }

        public sealed class StationSchemeRequest
        {
            public string InstanceID { get; set; } = string.Empty;

            public string? Name { get; set; }
        }

        public sealed class StationSchemeUpdateRequest
        {
            public string InstanceID { get; set; } = string.Empty;

            public string OriginalID { get; set; } = string.Empty;

            public string? Name { get; set; }
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
                metadata = new { latestElementID = segments.Count },
                tracks,
                curves = Array.Empty<object>(),
                nodes = Array.Empty<object>(),
                signals = Array.Empty<object>(),
                insulationJoints = Array.Empty<object>(),
                platforms = Array.Empty<object>(),
                switches = Array.Empty<object>(),
                annotations = Array.Empty<object>()
            }, new JsonSerializerOptions
            {
                WriteIndented = true
            });

        }
    }
}
