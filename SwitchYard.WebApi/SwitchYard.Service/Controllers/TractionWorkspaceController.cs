using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SwitchYard.Service.Models;
using SwitchYard.Service.Services;
using SwitchYard.SpeedProfile;

namespace SwitchYard.Service.Controllers;

[ApiController]
[Authorize]
[Route("api/SpeedProfile/workspace")]
[SpeedProfileModelValidation]
public sealed class TractionWorkspaceController(
    TractionWorkspaceStore store,
    SpeedProfileCalculationService calculationService,
    ILogger<TractionWorkspaceController> logger) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(TractionWorkspace), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult GetWorkspace([FromQuery] string? instanceId, [FromQuery] string? stationSchemeId, [FromQuery] string calculationSetId = "default", [FromQuery] string routeCaseId = "") =>
        InScope(instanceId, stationSchemeId, scope =>
        {
            var workspace = store.GetWorkspace(scope with { CalculationSetID = calculationSetId, RouteCaseID = routeCaseId });
            return workspace is null ? NoContent() : Ok(workspace);
        });

    [HttpPut]
    [RequestSizeLimit(32 * 1024 * 1024)]
    [ProducesResponseType(typeof(TractionWorkspace), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(SpeedProfileApiError), StatusCodes.Status409Conflict)]
    public IActionResult SaveWorkspace([FromBody] TractionWorkspaceSaveRequest body) =>
        InScope(body.InstanceId, body.StationSchemeId, scope =>
        {
            if (body.Workspace is null) throw new ArgumentException("必须提供 workspace。");
            HttpContext.RequestAborted.ThrowIfCancellationRequested();
            return Ok(store.SaveWorkspace(scope with { CalculationSetID = body.CalculationSetId, RouteCaseID = body.RouteCaseId }, body.Workspace));
        });

    [HttpPost("calculate")]
    [RequestSizeLimit(8 * 1024 * 1024)]
    [ProducesResponseType(typeof(StoredTractionCalculation), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(SpeedProfileApiError), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(SpeedProfileApiError), StatusCodes.Status422UnprocessableEntity)]
    public IActionResult Calculate([FromBody] TractionWorkspaceCalculationRequest body) =>
        InScope(body.InstanceId, body.StationSchemeId, scope =>
        {
            if (body.Request is null) throw new ArgumentException("必须提供 request。");
            TractionWorkspaceValidation.Revision(body.Revision);
            if (body.Revision < 1) throw new ArgumentException("revision 必须为已保存工作区的正整数版本。");
            scope = scope with { CalculationSetID = body.CalculationSetId, RouteCaseID = body.RouteCaseId };
            var workspace = store.GetWorkspace(scope);
            if (workspace is null) return NotFound(new SpeedProfileApiError("workspace-not-found", "请先保存牵引计算参数。"));
            if (workspace.Revision != body.Revision)
                throw new TractionRevisionConflictException(workspace.Revision);
            var result = calculationService.Calculate(body.Request, HttpContext.RequestAborted);
            HttpContext.RequestAborted.ThrowIfCancellationRequested();
            // The response is successful only after the snapshot and result commit together.
            return Ok(store.SaveCalculation(scope, body.Revision, body.Request, result));
        });

    [HttpGet("calculation")]
    [ProducesResponseType(typeof(StoredTractionCalculation), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult GetCalculation([FromQuery] string? instanceId, [FromQuery] string? stationSchemeId, [FromQuery] string calculationSetId = "default", [FromQuery] string routeCaseId = "") =>
        InScope(instanceId, stationSchemeId, scope =>
        {
            var calculation = store.GetCalculation(scope with { CalculationSetID = calculationSetId, RouteCaseID = routeCaseId });
            return calculation is null ? NoContent() : Ok(calculation);
        });

    [HttpGet("routes")]
    public IActionResult ListRoutes([FromQuery] string? instanceId, [FromQuery] string? stationSchemeId, [FromQuery] string calculationSetId = "default") =>
        InScope(instanceId, stationSchemeId, scope => Ok(store.ListRoutes(scope with { CalculationSetID = calculationSetId })));

    [HttpGet("trains")]
    public IActionResult GetProjectTrains([FromQuery] string? instanceId, [FromQuery] string? stationSchemeId) =>
        InScope(instanceId, stationSchemeId, scope => store.GetProjectTrains(scope) is { } trains ? Ok(trains) : NoContent());

    [HttpPut("trains")]
    [RequestSizeLimit(32 * 1024 * 1024)]
    public IActionResult SaveProjectTrains([FromBody] TractionProjectTrainSaveRequest body) =>
        InScope(body.InstanceId, body.StationSchemeId, scope => Ok(store.SaveProjectTrains(scope, body.Revision, body.Catalog)));
    [HttpPost("routes/open")]
    public IActionResult OpenRoute([FromBody] TractionRouteMutation body) =>
        InScope(body.InstanceId, body.StationSchemeId, scope => Ok(store.OpenRoute(scope with { CalculationSetID = body.CalculationSetId }, body.RouteId, body.ContinuationId, body.Select)));
    [HttpDelete("routes")]
    public IActionResult DeleteRoute([FromBody] TractionRouteMutation body) =>
        InScope(body.InstanceId, body.StationSchemeId, scope => { store.DeleteRouteCase(scope with { CalculationSetID = body.CalculationSetId, RouteCaseID = body.RouteCaseId }, body.Revision); return NoContent(); });

    [HttpGet("sets")]
    public IActionResult ListSets([FromQuery] string? instanceId, [FromQuery] string? stationSchemeId) =>
        InScope(instanceId, stationSchemeId, scope => Ok(store.ListSets(scope)));

    [HttpPost("sets")]
    public IActionResult CreateSet([FromBody] TractionSetMutation body) =>
        InScope(body.InstanceId, body.StationSchemeId, scope => Ok(store.CreateSet(scope, body.Name ?? "", body.SourceId, body.Revision)));

    [HttpPut("sets/name")]
    public IActionResult RenameSet([FromBody] TractionSetMutation body) =>
        InScope(body.InstanceId, body.StationSchemeId, scope => Ok(store.RenameSet(scope with { CalculationSetID = body.CalculationSetId }, body.Revision ?? -1, body.Name ?? "")));

    [HttpPut("sets/selection")]
    public IActionResult SelectSet([FromBody] TractionSetMutation body) =>
        InScope(body.InstanceId, body.StationSchemeId, scope => { store.SelectSet(scope with { CalculationSetID = body.CalculationSetId }); return NoContent(); });

    [HttpDelete("sets")]
    public IActionResult DeleteSet([FromBody] TractionSetMutation body) =>
        InScope(body.InstanceId, body.StationSchemeId, scope => { store.DeleteSet(scope with { CalculationSetID = body.CalculationSetId }, body.Revision ?? -1); return NoContent(); });

    private IActionResult InScope(string? instanceId, string? stationSchemeId, Func<TractionScope, IActionResult> action)
    {
        try
        {
            var instance = instanceId?.Trim() ?? "";
            var scheme = stationSchemeId?.Trim() ?? "";
            if (instance.EnumerateRunes().Count() is < 1 or > 50 || scheme.EnumerateRunes().Count() is < 1 or > 50 || instance.Any(char.IsControl) || scheme.Any(char.IsControl))
                return BadRequest(new SpeedProfileApiError("invalid-scope", "instanceId 和 stationSchemeId 须为 1–50 个字符，且不能包含控制字符。"));
            var scope = new TractionScope(instance, scheme);
            var authorization = AuthorizeScope(scope);
            return authorization ?? action(scope);
        }
        catch (TractionProjectTrainConflictException exception)
        {
            return Conflict(new SpeedProfileApiError("train-catalog-conflict", exception.Message,
                new Dictionary<string, string[]> { ["currentRevision"] = [exception.CurrentRevision.ToString(System.Globalization.CultureInfo.InvariantCulture)] }));
        }
        catch (TractionRevisionConflictException exception)
        {
            return Conflict(new SpeedProfileApiError("revision-conflict", "牵引计算参数已更新，请重新加载后重试。",
                new Dictionary<string, string[]> { ["currentRevision"] = [exception.CurrentRevision.ToString(System.Globalization.CultureInfo.InvariantCulture)] }));
        }
        catch (SpeedProfileInputException exception)
        {
            return BadRequest(new SpeedProfileApiError(exception.Code, exception.Message));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new SpeedProfileApiError("invalid-input", exception.Message));
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new SpeedProfileApiError("workspace-not-found", exception.Message));
        }
        catch (InfeasibleProfileException exception)
        {
            return UnprocessableEntity(new SpeedProfileApiError("infeasible-profile", exception.Message));
        }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Traction workspace operation failed for {InstanceID}/{StationSchemeID}.", instanceId, stationSchemeId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new SpeedProfileApiError("storage-error", "牵引计算数据读取或保存失败，请重试。"));
        }
    }

    private IActionResult? AuthorizeScope(TractionScope scope)
    {
        var db = DBConnector.GetDBConnector(DBConnector.CapacityDatabaseSectionName);
        var instance = db.Query<OwnerRow>("SELECT Owner FROM capacityinstance WHERE ID=@InstanceID", scope)?.FirstOrDefault();
        if (instance is null) return NotFound(new SpeedProfileApiError("instance-not-found", "实例不存在。"));
        var username = User.Identity?.Name;
        var isAdmin = string.Equals(username, "Admin", StringComparison.OrdinalIgnoreCase)
            || User.Claims.Any(claim => claim.Type == ClaimTypes.Role && string.Equals(claim.Value, "Admin", StringComparison.OrdinalIgnoreCase));
        if (!isAdmin && (string.IsNullOrWhiteSpace(username) || !string.Equals(instance.Owner, username, StringComparison.Ordinal)))
            return StatusCode(StatusCodes.Status403Forbidden, new SpeedProfileApiError("forbidden", "无权访问此实例。"));
        if ((db.Query<int>("SELECT COUNT(1) FROM stationscheme WHERE InstanceID=@InstanceID AND ID=@StationSchemeID", scope)?.FirstOrDefault() ?? 0) == 0)
            return NotFound(new SpeedProfileApiError("station-scheme-not-found", "站场方案不存在。"));
        return null;
    }

    private sealed class OwnerRow { public string? Owner { get; set; } }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TractionWorkspaceSaveRequest
{
    [JsonRequired] public string InstanceId { get; init; } = null!;
    [JsonRequired] public string StationSchemeId { get; init; } = null!;
    public string CalculationSetId { get; init; } = "default";
    public string RouteCaseId { get; init; } = "";
    [JsonRequired] public TractionWorkspace Workspace { get; init; } = null!;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TractionWorkspaceCalculationRequest
{
    [JsonRequired] public string InstanceId { get; init; } = null!;
    [JsonRequired] public string StationSchemeId { get; init; } = null!;
    public string CalculationSetId { get; init; } = "default";
    public string RouteCaseId { get; init; } = "";
    [JsonRequired] public long Revision { get; init; }
    [JsonRequired] public SpeedProfileCalculationRequest Request { get; init; } = null!;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TractionSetMutation
{
    [JsonRequired] public string InstanceId { get; init; } = null!;
    [JsonRequired] public string StationSchemeId { get; init; } = null!;
    public string CalculationSetId { get; init; } = "default";
    public string RouteCaseId { get; init; } = "";
    public string? Name { get; init; }
    public string? SourceId { get; init; }
    public long? Revision { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TractionRouteMutation
{
    [JsonRequired] public string InstanceId { get; init; } = null!;
    [JsonRequired] public string StationSchemeId { get; init; } = null!;
    public string CalculationSetId { get; init; } = "default";
    public string RouteCaseId { get; init; } = "";
    public string RouteId { get; init; } = "";
    public string ContinuationId { get; init; } = "";
    public bool Select { get; init; } = true;
    public long Revision { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TractionProjectTrainSaveRequest
{
    [JsonRequired] public string InstanceId { get; init; } = null!;
    [JsonRequired] public string StationSchemeId { get; init; } = null!;
    [JsonRequired] public long Revision { get; init; }
    [JsonRequired] public TractionTrainPresetCatalog Catalog { get; init; } = null!;
}
