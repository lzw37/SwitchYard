using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;
using SwitchYard.Capacity;

namespace SwitchYard.Service.Controllers
{
    [ApiController]
    public class StationLayoutController : ControllerBase
    {
        private readonly ILogger<StationLayoutController> _logger;
        private readonly IWebHostEnvironment _environment;

        public StationLayoutController(ILogger<StationLayoutController> logger, IWebHostEnvironment environment)
        {
            _logger = logger;
            _environment = environment;
        }

        [HttpPost("/saveJson")]
        [AllowAnonymous]
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

        [HttpPost("/getJson")]
        [AllowAnonymous]
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
            foreach (var startDirectory in GetSearchStartDirectories())
            {
                var currentDirectory = new DirectoryInfo(startDirectory);
                for (var depth = 0; currentDirectory != null && depth < 8; depth += 1)
                {
                    var candidateDirectory = Path.Combine(currentDirectory.FullName, "LocalData", "Capacity");
                    if (Directory.Exists(candidateDirectory))
                    {
                        filePath = Path.Combine(candidateDirectory, "StationLayout.json");
                        errorMessage = string.Empty;
                        return true;
                    }

                    currentDirectory = currentDirectory.Parent;
                }
            }

            try
            {
                filePath = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, "..", "..", "LocalData", "Capacity", "StationLayout.json"));
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

        private IEnumerable<string> GetSearchStartDirectories()
        {
            if (!string.IsNullOrWhiteSpace(_environment.ContentRootPath))
            {
                yield return _environment.ContentRootPath;
            }

            var baseDirectory = AppContext.BaseDirectory;
            if (!string.IsNullOrWhiteSpace(baseDirectory) &&
                !string.Equals(baseDirectory, _environment.ContentRootPath, StringComparison.OrdinalIgnoreCase))
            {
                yield return baseDirectory;
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

        [HttpGet("/extractDwgFile")]

        public void ExtractDwgFile()
        {

        }
    }
}
