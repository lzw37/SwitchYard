using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SwitchYard.Service.Models;
using SwitchYard.Service.Services;
using SwitchYard.SpeedProfile;

namespace SwitchYard.Service.Controllers;

[ApiController]
[Authorize]
[Route("api/SpeedProfile")]
[SpeedProfileModelValidation]
public sealed class SpeedProfileController(SpeedProfileCalculationService calculationService) : ControllerBase
{
    [HttpPost("calculate")]
    [RequestSizeLimit(8 * 1024 * 1024)]
    [ProducesResponseType(typeof(SpeedProfileCalculationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(SpeedProfileApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(SpeedProfileApiError), StatusCodes.Status422UnprocessableEntity)]
    public ActionResult<SpeedProfileCalculationResponse> Calculate([FromBody] SpeedProfileCalculationRequest request)
    {
        try
        {
            return Ok(calculationService.Calculate(request, HttpContext.RequestAborted));
        }
        catch (SpeedProfileInputException exception)
        {
            return BadRequest(new SpeedProfileApiError(exception.Code, exception.Message));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new SpeedProfileApiError("invalid-input", exception.Message));
        }
        catch (InfeasibleProfileException exception)
        {
            return UnprocessableEntity(new SpeedProfileApiError("infeasible-profile", exception.Message));
        }
    }
}

/// <summary>Keep JSON binding failures on the same error contract, without changing other controllers.</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class SpeedProfileModelValidationAttribute : ActionFilterAttribute
{
    // ApiController's automatic ModelStateInvalidFilter runs at -2000.
    public SpeedProfileModelValidationAttribute() => Order = -3000;

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (context.ModelState.IsValid) return;
        var errors = context.ModelState.Where(entry => entry.Value?.Errors.Count > 0).Take(20)
            .ToDictionary(entry => entry.Key, entry => entry.Value!.Errors.Take(3)
                .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage) ? "JSON 内容或字段类型无效。" : error.ErrorMessage)
                .ToArray());
        var message = string.Join(" ", errors.SelectMany(entry => entry.Value).Take(3));
        context.Result = new BadRequestObjectResult(new SpeedProfileApiError("invalid-input", message, errors));
    }
}