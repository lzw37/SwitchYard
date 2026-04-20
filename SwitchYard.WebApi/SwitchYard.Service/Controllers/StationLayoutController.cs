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
    }
}
