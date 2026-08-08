using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Text.Json;
using SwitchYard.Capacity;
using SwitchYard.Service.Models;
using SwitchYard.Service.Services;

namespace SwitchYard.Service.Controllers;

[ApiController]
[Route("api/capacity-agents")]
public sealed class CapacityAgentsController : ControllerBase
{
    private readonly UserService _users;
    private readonly JwtTokenService _tokens;
    private readonly CapacityAgentRegistry _agents;
    private readonly CapacitySolveJobService _jobs;
    private readonly StationCapacityInputBuilder _inputBuilder;
    private readonly ILogger<CapacityAgentsController> _logger;

    public CapacityAgentsController(
        UserService users,
        JwtTokenService tokens,
        CapacityAgentRegistry agents,
        CapacitySolveJobService jobs,
        StationCapacityInputBuilder inputBuilder,
        ILogger<CapacityAgentsController> logger)
    {
        _users = users;
        _tokens = tokens;
        _agents = agents;
        _jobs = jobs;
        _inputBuilder = inputBuilder;
        _logger = logger;
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("authenticate")]
    public IActionResult Authenticate([FromBody] CapacityAgentAuthenticationRequest request)
    {
        var user = _users.ValidateUser(request.Username?.Trim() ?? string.Empty, request.Password ?? string.Empty);
        if (user == null || user.MustChangePassword == 1)
        {
            _logger.LogWarning(
                "CapacityAgent authentication failed for {Username} from {ClientIp}.",
                request.Username,
                HttpContext.Connection.RemoteIpAddress);
            return Unauthorized(new { message = "用户名或密码错误，账号未激活，或账号需要先修改密码。" });
        }

        return Ok(new CapacityAgentAuthenticationResponse
        {
            Token = _tokens.GenerateCapacityAgentToken(user),
            ExpiresIn = _tokens.GetCapacityAgentExpirationSeconds(),
            Username = user.Name
        });
    }

    [Authorize]
    [HttpGet]
    public IActionResult GetAgents()
    {
        return Ok(_agents.GetAgents());
    }

    [Authorize]
    [HttpPost("preview-input")]
    public IActionResult PreviewInput([FromBody] StationCapacityInputRequest request)
    {
        var validation = ValidateInputScope(request);
        if (validation != null)
        {
            return validation;
        }

        try
        {
            var input = _inputBuilder.Build(request);
            if (input.Routes.Count == 0)
            {
                return BadRequest(new { message = "所选站场方案没有可用于求解的进路。" });
            }

            if (input.Trains.Count == 0 || input.Trains.All(train => train.Movements.Count == 0))
            {
                return BadRequest(new { message = "所选作业计划没有可用于求解的列车作业。" });
            }

            return Ok(input);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to build station capacity input.");
            return StatusCode(500, new { message = "生成求解输入失败。", detail = exception.Message });
        }
    }

    [Authorize]
    [HttpPost("jobs")]
    public async Task<IActionResult> SubmitJob(
        [FromBody] CapacitySolveJobRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.AgentId) ||
            string.IsNullOrWhiteSpace(request.ModelId) ||
            request.Input.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return BadRequest(new { message = "Agent、模型和求解输入均不能为空。" });
        }

        var username = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(username))
        {
            return Unauthorized();
        }

        var (job, error) = await _jobs.SubmitAsync(request, username, cancellationToken);
        if (job == null)
        {
            return Conflict(new { message = error });
        }

        if (!string.IsNullOrWhiteSpace(error))
        {
            return StatusCode(503, job);
        }

        return AcceptedAtAction(nameof(GetJob), new { jobId = job.JobId }, job);
    }

    [Authorize]
    [HttpGet("jobs")]
    public IActionResult GetJobs([FromQuery] int limit = 50)
    {
        var username = User.Identity?.Name ?? string.Empty;
        return Ok(_jobs.GetRecent(username, IsAdmin(), limit));
    }

    [Authorize]
    [HttpGet("jobs/{jobId}")]
    public IActionResult GetJob(string jobId)
    {
        var job = _jobs.Get(jobId);
        if (job == null)
        {
            return NotFound();
        }

        if (!IsAdmin() && !string.Equals(job.RequestedBy, User.Identity?.Name, StringComparison.Ordinal))
        {
            return Forbid();
        }

        return Ok(job);
    }

    private IActionResult? ValidateInputScope(StationCapacityInputRequest request)
    {
        request.InstanceId = request.InstanceId?.Trim() ?? string.Empty;
        request.StationSchemeId = request.StationSchemeId?.Trim() ?? string.Empty;
        request.OperationPlanId = request.OperationPlanId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(request.InstanceId) ||
            string.IsNullOrWhiteSpace(request.StationSchemeId) ||
            string.IsNullOrWhiteSpace(request.OperationPlanId))
        {
            return BadRequest(new { message = "实例、站场方案和作业计划均不能为空。" });
        }

        var dbConnector = DBConnector.GetDBConnector(DBConnector.CapacityDatabaseSectionName);
        var instance = (dbConnector.Query<CapacityInstance>(
            "SELECT ID, Name, Owner, CreatedDate, IsActive FROM capacityinstance WHERE ID = @id",
            new { id = request.InstanceId }) ?? new List<CapacityInstance>()).FirstOrDefault();
        if (instance == null)
        {
            return NotFound(new { message = "能力计算实例不存在。" });
        }

        if (!IsAdmin() && !string.Equals(instance.Owner, User.Identity?.Name, StringComparison.Ordinal))
        {
            return Forbid();
        }

        return null;
    }

    private bool IsAdmin()
    {
        return string.Equals(User.Identity?.Name, "Admin", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value, "Admin", StringComparison.OrdinalIgnoreCase);
    }
}
