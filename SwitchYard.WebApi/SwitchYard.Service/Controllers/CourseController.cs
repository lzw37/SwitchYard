using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;

namespace SwitchYard.Service.Controllers
{
    [ApiController]
    [Route("Course/[action]")]
    public class CourseController : ControllerBase
    {
        private static readonly HashSet<string> SupportedVideoExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4",
            ".webm",
            ".m4v",
            ".mov",
            ".avi",
            ".mkv"
        };

        private static readonly HashSet<string> SupportedTeachingExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf",
            ".doc",
            ".docx"
        };

        private static readonly FileExtensionContentTypeProvider ContentTypeProvider = new();

        private readonly ILogger<CourseController> _logger;
        private readonly string _configuredVideoDir;
        private readonly string _configuredDocDir;

        public CourseController(ILogger<CourseController> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuredVideoDir = configuration["Course:VideoDir"]?.Trim() ?? string.Empty;
            _configuredDocDir = configuration["Course:DocDir"]?.Trim() ?? string.Empty;
        }

        [HttpGet(Name = "GetVideoManifest")]
        [AllowAnonymous]
        public IActionResult GetVideoManifest()
        {
            if (!TryResolveVideoRoot(out var videoRoot))
            {
                return Ok(Array.Empty<CourseAssetManifestItem>());
            }

            try
            {
                var items = EnumerateManifest(videoRoot, SupportedVideoExtensions, "/Course/StreamVideo?relativePath={0}");
                return Ok(items);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to enumerate videos from configured directory {VideoDir}", _configuredVideoDir);
                return StatusCode(500, "Failed to load video list.");
            }
        }

        [HttpGet(Name = "GetTeachingManifest")]
        [AllowAnonymous]
        public IActionResult GetTeachingManifest()
        {
            if (!TryResolveTeachingRoot(out var teachingRoot))
            {
                return Ok(Array.Empty<CourseAssetManifestItem>());
            }

            try
            {
                var items = EnumerateManifest(teachingRoot, SupportedTeachingExtensions, "/Course/StreamTeachingFile?relativePath={0}");
                return Ok(items);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to enumerate teaching assets from configured directory {DocDir}", _configuredDocDir);
                return StatusCode(500, "Failed to load teaching asset list.");
            }
        }

        [HttpGet(Name = "StreamVideo")]
        [AllowAnonymous]
        public IActionResult StreamVideo([FromQuery] string relativePath)
        {
            if (!TryResolveVideoRoot(out var videoRoot))
            {
                return NotFound("Video directory does not exist.");
            }

            return StreamFile(videoRoot, relativePath, SupportedVideoExtensions);
        }

        [HttpGet(Name = "StreamTeachingFile")]
        [AllowAnonymous]
        public IActionResult StreamTeachingFile([FromQuery] string relativePath)
        {
            if (!TryResolveTeachingRoot(out var teachingRoot))
            {
                return NotFound("Teaching directory does not exist.");
            }

            return StreamFile(teachingRoot, relativePath, SupportedTeachingExtensions);
        }

        private IActionResult StreamFile(string rootPath, string relativePath, HashSet<string> supportedExtensions)
        {
            if (!TryResolveSafeFilePath(rootPath, relativePath, out var fullPath))
            {
                return BadRequest("Invalid asset path.");
            }

            if (!System.IO.File.Exists(fullPath))
            {
                return NotFound("Asset not found.");
            }

            if (!HasSupportedExtension(fullPath, supportedExtensions))
            {
                return BadRequest("Unsupported asset extension.");
            }

            if (!ContentTypeProvider.TryGetContentType(fullPath, out var contentType))
            {
                contentType = "application/octet-stream";
            }

            return PhysicalFile(fullPath, contentType, enableRangeProcessing: true);
        }

        private List<CourseAssetManifestItem> EnumerateManifest(
            string rootPath,
            HashSet<string> supportedExtensions,
            string urlTemplate)
        {
            return Directory
                .EnumerateFiles(rootPath, "*.*", SearchOption.AllDirectories)
                .Where(filePath => HasSupportedExtension(filePath, supportedExtensions))
                .Where(filePath => !Path.GetFileName(filePath).StartsWith("~$", StringComparison.OrdinalIgnoreCase))
                .Select(filePath =>
                {
                    var relativePath = NormalizeSlashes(Path.GetRelativePath(rootPath, filePath));
                    var escapedPath = Uri.EscapeDataString(relativePath);
                    var url = string.Format(urlTemplate, escapedPath);

                    return new CourseAssetManifestItem
                    {
                        Name = Path.GetFileName(filePath),
                        Path = relativePath,
                        Url = url
                    };
                })
                .OrderBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private bool TryResolveVideoRoot(out string videoRoot)
        {
            videoRoot = string.Empty;
            if (string.IsNullOrWhiteSpace(_configuredVideoDir))
            {
                return false;
            }

            return TryResolveRootWithOptionalChild(_configuredVideoDir, "站场视频", out videoRoot);
        }

        private bool TryResolveTeachingRoot(out string teachingRoot)
        {
            teachingRoot = string.Empty;
            var sourceDir = string.IsNullOrWhiteSpace(_configuredDocDir) ? _configuredVideoDir : _configuredDocDir;
            if (string.IsNullOrWhiteSpace(sourceDir))
            {
                return false;
            }

            return TryResolveRootWithOptionalChild(sourceDir, "教学文档", out teachingRoot);
        }

        private static bool TryResolveRootWithOptionalChild(string configuredRoot, string optionalChildDir, out string resolvedRoot)
        {
            resolvedRoot = string.Empty;
            try
            {
                var root = Path.GetFullPath(configuredRoot);
                if (!Directory.Exists(root))
                {
                    return false;
                }

                var candidate = Path.Combine(root, optionalChildDir);
                resolvedRoot = Directory.Exists(candidate) ? candidate : root;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryResolveSafeFilePath(string rootPath, string relativePath, out string fullPath)
        {
            fullPath = string.Empty;
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return false;
            }

            var normalizedRelative = NormalizeSlashes(relativePath).Trim('/');
            if (normalizedRelative.Length == 0)
            {
                return false;
            }

            var segments = normalizedRelative.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Any(segment => segment is "." or ".."))
            {
                return false;
            }

            var normalizedRoot = Path.GetFullPath(rootPath);
            var candidatePath = Path.Combine(normalizedRoot, normalizedRelative.Replace('/', Path.DirectorySeparatorChar));
            fullPath = Path.GetFullPath(candidatePath);

            return fullPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasSupportedExtension(string filePath, HashSet<string> supportedExtensions)
        {
            var extension = Path.GetExtension(filePath);
            return supportedExtensions.Contains(extension);
        }

        private static string NormalizeSlashes(string value)
        {
            return value.Replace('\\', '/');
        }

        public class CourseAssetManifestItem
        {
            public string Name { get; set; } = string.Empty;
            public string Path { get; set; } = string.Empty;
            public string Url { get; set; } = string.Empty;
        }
    }
}
