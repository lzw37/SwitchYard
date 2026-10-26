using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SwitchYard.Service.Models;
using SwitchYard.Service.Services;

namespace SwitchYard.Service.Controllers;

[ApiController]
[Authorize]
[Route("api/SpeedProfile/train-characteristics")]
public sealed class TrainCharacteristicsController : ControllerBase
{
    [HttpPost("import")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(TrainCharacteristicsExcel.MaxFileBytes + 64 * 1024)]
    [ProducesResponseType(typeof(TrainCharacteristicsImport), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(SpeedProfileApiError), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TrainCharacteristicsImport>> Import(IFormFile? file)
    {
        if (file is null || file.Length == 0)
            return InvalidFile("请选择已填写的 Excel 文件。");
        if (!Path.GetExtension(file.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            return InvalidFile("仅支持 .xlsx 格式，请使用下载的 Excel 模板。");
        if (file.Length > TrainCharacteristicsExcel.MaxFileBytes)
            return InvalidFile("Excel 文件不能超过 2 MB。");
        try
        {
            using var stream = new MemoryStream((int)file.Length);
            await file.CopyToAsync(stream, HttpContext.RequestAborted);
            stream.Position = 0;
            return Ok(TrainCharacteristicsExcel.Read(stream, HttpContext.RequestAborted));
        }
        catch (TrainCharacteristicsExcelException exception)
        {
            return InvalidFile(exception.Message);
        }
    }

    private BadRequestObjectResult InvalidFile(string message) =>
        BadRequest(new SpeedProfileApiError("invalid-train-characteristics-excel", message));
}
