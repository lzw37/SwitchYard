using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SwitchYard.Service.Services;
using SwitchYard.SketchLayout;

namespace SwitchYard.Service.Controllers;

[ApiController]
[Authorize]
[Route("SketchLayout/schemes")]
[RequestSizeLimit(8 * 1024 * 1024)]
public sealed class SketchLayoutController(UserService users, SketchLayoutStore store) : Controller
{
    private string OwnerId = "";
    private string OwnerName = "";
    private bool Admin;
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        // Always resolve current status and role from hump; do not trust stale role claims.
        var name = User.Identity?.Name;
        var user = string.IsNullOrEmpty(name) ? null : users.GetActiveUserByUsername(name);
        if (user == null) { context.Result = Unauthorized(); return; }
        OwnerId = user.Id; OwnerName = user.Name; Admin = string.Equals(user.Role, "Admin", StringComparison.OrdinalIgnoreCase);
    }
    [HttpGet] public IActionResult List() => Ok(store.List(OwnerId, Admin));
    [HttpGet("{id}")] public IActionResult Get(string id) => store.Get(id, OwnerId, Admin) is { } scheme ? Ok(scheme) : NotFound();
    [HttpPost]
    public IActionResult Create(SketchLayoutScheme document)
    {
        try { var result = store.Create(document, OwnerId, OwnerName); return CreatedAtAction(nameof(Get), new { id = result.Id }, result); }
        catch (ArgumentException e) { return BadRequest(new { message = e.Message }); }
    }
    [HttpPut("{id}")]
    public IActionResult Update(string id, SchemeWriteRequest request)
    {
        var existing = store.Get(id, OwnerId, Admin);
        if (existing == null) return NotFound();
        var updatedAt = DateTime.UtcNow.ToString("O");
        // Return the snapshot committed by this request, even if another writer immediately updates it.
        try
        {
            return store.Update(id, OwnerId, Admin, request, updatedAt)
            ? Ok(existing with { Document = request.Document, Revision = request.Revision + 1, UpdatedAt = updatedAt })
            : Conflict(new { message = "方案已被其他窗口修改，请重新加载后再编辑。" });
        }
        catch (ArgumentException e) { return BadRequest(new { message = e.Message }); }
    }
    [HttpDelete("{id}")]
    public IActionResult Delete(string id, [FromQuery] int revision)
    {
        if (store.Get(id, OwnerId, Admin) == null) return NotFound();
        return store.Delete(id, OwnerId, Admin, revision) ? NoContent() : Conflict(new { message = "方案已更新，请重新加载后再删除。" });
    }
    [HttpPost("{id}/analysis")]
    public IActionResult Analyze(string id)
    {
        var scheme = store.Get(id, OwnerId, Admin); if (scheme == null) return NotFound();
        try { return Ok(SketchLayoutAnalyzer.Analyze(scheme.Document)); }
        catch (ArgumentException e) { return BadRequest(new { message = e.Message }); }
    }
}
