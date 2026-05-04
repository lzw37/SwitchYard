using Microsoft.AspNetCore.Mvc;

namespace SwitchYard.Service.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SystemController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public SystemController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet("version")]
        public IActionResult GetVersion()
        {
            var version = _configuration["App:Version"]?.Trim();
            if (string.IsNullOrWhiteSpace(version))
            {
                version = "unknown";
            }

            return Ok(new { version });
        }
    }
}
