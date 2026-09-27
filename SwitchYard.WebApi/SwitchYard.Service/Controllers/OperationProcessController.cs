using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SwitchYard.Capacity;
using SwitchYard.Service.Models;
using SwitchYard.Service.Services;

namespace SwitchYard.Service.Controllers;

/// <summary>Station-scheme process templates with revision-checked updates.</summary>
[ApiController]
[Route("OperationProcess")]
[Authorize]
public sealed class OperationProcessController : ControllerBase
{
    private readonly ILogger<OperationProcessController> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const string ScopeFilter = "InstanceID = @InstanceID AND StationSchemeID = @StationSchemeID AND OperationPlanID = ''";

    public OperationProcessController(ILogger<OperationProcessController> logger) => _logger = logger;

    [HttpGet("GetTemplates")]
    public IActionResult GetTemplates([FromQuery] ProcessScope scope) => Execute(scope, (db, normalized) =>
    {
        var catalog = LoadCatalog(db, normalized);
        var templates = (db.Query<TemplateRow>($"SELECT InstanceID, StationSchemeID, OperationPlanID, TemplateID, Revision, Document FROM operationprocesstemplate WHERE {ScopeFilter} ORDER BY UpdatedAtUtc DESC, TemplateID", normalized) ?? new())
            .Select(ReadDocument).ToList();
        foreach (var template in templates) OperationProcessEventNodes.Refresh(template, catalog);
        return Ok(templates);
    });

    [HttpGet("GetTemplate")]
    public IActionResult GetTemplate([FromQuery] ProcessScope scope, [FromQuery] string templateID) =>
        Execute(scope, (db, normalized) => Ok(LoadTemplate(db, normalized, templateID)));

    [HttpGet("GetCatalog")]
    public IActionResult GetCatalog([FromQuery] ProcessScope scope) => Execute(scope, (db, normalized) => Ok(LoadCatalog(db, normalized)));

    [HttpPost("CreateTemplate")]
    public IActionResult CreateTemplate([FromBody] OperationProcessTemplate? request)
    {
        if (request is null) return BadRequest(new { message = "请求必须包含模板对象。" });
        return Execute(request, (db, scope) =>
        {
            SetScope(request, scope);
            if (string.IsNullOrWhiteSpace(request.Id)) request.Id = Guid.NewGuid().ToString("N");
            if (request.Revision != 0) throw new ProcessRequestException(400, "新建模板 revision 必须为 0。");
            Validate(request, LoadCatalog(db, scope));
            if (FindTemplate(db, scope, request.Id) is not null) throw new ProcessRequestException(409, "此范围内已存在相同 id 的模板。");
            request.Revision = 1;
            var parameters = Parameters(request, JsonSerializer.Serialize(request, JsonOptions));
            db.BeginTransaction();
            try
            {
                db.ExecuteNonQuery(@"INSERT INTO operationprocesstemplate
                    (InstanceID, StationSchemeID, OperationPlanID, TemplateID, Revision, Document, UpdatedAtUtc)
                    VALUES (@InstanceID, @StationSchemeID, @OperationPlanID, @TemplateID, @Revision, @Document, @UpdatedAtUtc)", parameters);
                db.Commit();
            }
            catch { db.Rollback(); throw; }
            return Ok(request);
        });
    }

    [HttpPut("UpdateTemplate")]
    public IActionResult UpdateTemplate([FromBody] OperationProcessTemplate? request)
    {
        if (request is null) return BadRequest(new { message = "请求必须包含模板对象。" });
        return Execute(request, (db, scope) =>
        {
            SetScope(request, scope);
            SaveTemplate(db, scope, request);
            return Ok(request);
        });
    }

    [HttpDelete("DeleteTemplate")]
    public IActionResult DeleteTemplate([FromQuery] ProcessScope scope, [FromQuery] string templateID, [FromQuery] int? revision = null) =>
        Execute(scope, (db, normalized) =>
        {
            if (revision is null or <= 0) throw new ProcessRequestException(400, "删除模板时必须提供当前 revision。");
            var template = LoadTemplate(db, normalized, templateID);
            if (template.Revision != revision) throw ConflictException();
            var deleted = db.ExecuteNonQuery($"DELETE FROM operationprocesstemplate WHERE {ScopeFilter} AND TemplateID = @TemplateID AND Revision = @Revision", Parameters(template));
            if (deleted == 0) throw ConflictException();
            return NoContent();
        });

    [HttpGet("{templateID}/{collection}")]
    public IActionResult GetEntities(string templateID, string collection, [FromQuery] ProcessScope scope) =>
        Execute(scope, (db, normalized) => Ok(Entities(LoadTemplate(db, normalized, templateID), collection)));

    [HttpGet("{templateID}/{collection}/{id}")]
    public IActionResult GetEntity(string templateID, string collection, string id, [FromQuery] ProcessScope scope) =>
        Execute(scope, (db, normalized) =>
        {
            var entity = Entities(LoadTemplate(db, normalized, templateID), collection).FirstOrDefault(x => EntityID(x) == id);
            return entity is null ? NotFound(new { message = "对象不存在。" }) : Ok(entity);
        });

    [HttpPost("{templateID}/{collection}")]
    public IActionResult CreateEntity(string templateID, string collection, [FromQuery] ProcessScope scope, [FromQuery] int revision, [FromBody] JsonElement entity) =>
        MutateEntity(templateID, collection, null, scope, revision, entity, "create");

    [HttpPut("{templateID}/{collection}/{id}")]
    public IActionResult UpdateEntity(string templateID, string collection, string id, [FromQuery] ProcessScope scope, [FromQuery] int revision, [FromBody] JsonElement entity) =>
        MutateEntity(templateID, collection, id, scope, revision, entity, "update");

    [HttpDelete("{templateID}/{collection}/{id}")]
    public IActionResult DeleteEntity(string templateID, string collection, string id, [FromQuery] ProcessScope scope, [FromQuery] int revision) =>
        MutateEntity(templateID, collection, id, scope, revision, default, "delete");

    private IActionResult MutateEntity(string templateID, string collection, string? id, ProcessScope scope, int revision, JsonElement entity, string operation) =>
        Execute(scope, (db, normalized) =>
        {
            if (revision <= 0) throw new ProcessRequestException(400, "修改对象时必须提供当前模板 revision。");
            var template = LoadTemplate(db, normalized, templateID);
            if (template.Revision != revision) throw ConflictException();
            var existing = Entities(template, collection);
            if (operation != "create" && !existing.Any(x => EntityID(x) == id)) throw new ProcessRequestException(404, "对象不存在。");
            if (operation == "delete") RemoveEntity(template, collection, id!);
            else
            {
                var parsed = ParseEntity(entity, collection);
                var parsedID = EntityID(parsed);
                if (operation == "create" && existing.Any(x => EntityID(x) == parsedID)) throw new ProcessRequestException(409, "对象 id 已存在。");
                if (operation == "update" && id != parsedID) throw new ProcessRequestException(400, "请求路径与对象 id 必须一致；修改引用请整体更新模板。");
                PutEntity(template, collection, parsed, operation == "update");
            }
            SaveTemplate(db, normalized, template);
            return Ok(template);
        });

    private IActionResult Execute(ProcessScope scope, Func<DBConnector, ProcessScope, IActionResult> action)
    {
        try
        {
            var normalized = new ProcessScope {
                InstanceID = scope.InstanceID?.Trim() ?? "",
                StationSchemeID = scope.StationSchemeID?.Trim() ?? "",
                OperationPlanID = SchemeTemplateStore.PlanKey
            };
            if (new[] { normalized.InstanceID, normalized.StationSchemeID }.Any(x => x.Length is 0 or > 50))
                throw new ProcessRequestException(400, "instanceID、stationSchemeID 均必填且最多 50 个字符。");
            var db = DBConnector.GetDBConnector(DBConnector.CapacityDatabaseSectionName);
            var permissionError = AuthorizeScope(db, normalized);
            if (permissionError is not null) return permissionError;
            EnsureSchema(db);
            SchemeTemplateStore.Migrate(db, normalized.InstanceID, normalized.StationSchemeID);
            return action(db, normalized);
        }
        catch (ProcessRequestException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message, errors = ex.Errors }); }
        catch (JsonException) { return BadRequest(new { message = "对象 JSON 格式或字段类型不正确。" }); }
        catch (Exception ex) when (IsDuplicate(ex)) { return Conflict(new { message = "此 id 已存在，请刷新后重试。" }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Operation process request failed.");
            return StatusCode(500, new { message = "作业过程模板操作失败，请稍后重试。" });
        }
    }

    private IActionResult? AuthorizeScope(DBConnector db, ProcessScope scope)
    {
        var instance = db.Query<CapacityInstance>("SELECT * FROM capacityinstance WHERE ID = @InstanceID", scope)?.FirstOrDefault();
        if (instance is null) return NotFound(new { message = "能力分析实例不存在。" });
        var username = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(username)) return Unauthorized(new { message = "登录用户信息无效。" });
        var admin = string.Equals(username, "Admin", StringComparison.OrdinalIgnoreCase) ||
                    User.Claims.Any(x => x.Type == ClaimTypes.Role && string.Equals(x.Value, "Admin", StringComparison.OrdinalIgnoreCase));
        if (!admin && !string.Equals(instance.Owner, username, StringComparison.Ordinal))
            return StatusCode(403, new { message = "无权访问此能力分析实例。" });
        if ((db.Query<int>("SELECT COUNT(1) FROM stationscheme WHERE InstanceID = @InstanceID AND ID = @StationSchemeID", scope)?.FirstOrDefault() ?? 0) == 0)
            return NotFound(new { message = "当前实例下不存在此站场方案。" });
        return null;
    }

    public static void EnsureSchema(DBConnector db)
    {
        // Only this new table is created. Existing layout and operation-plan tables are untouched.
        var sql = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName)
            ? @"CREATE TABLE IF NOT EXISTS operationprocesstemplate (
                InstanceID VARCHAR(50) NOT NULL, StationSchemeID VARCHAR(50) NOT NULL,
                OperationPlanID VARCHAR(50) NOT NULL, TemplateID VARCHAR(100) NOT NULL,
                Revision INT NOT NULL, Document LONGTEXT NOT NULL, UpdatedAtUtc DATETIME NOT NULL,
                PRIMARY KEY (InstanceID, StationSchemeID, OperationPlanID, TemplateID)
                ) CHARACTER SET utf8mb4 COLLATE utf8mb4_bin"
            : @"CREATE TABLE IF NOT EXISTS operationprocesstemplate (
                InstanceID TEXT NOT NULL, StationSchemeID TEXT NOT NULL,
                OperationPlanID TEXT NOT NULL, TemplateID TEXT NOT NULL,
                Revision INTEGER NOT NULL, Document TEXT NOT NULL, UpdatedAtUtc DATETIME NOT NULL,
                PRIMARY KEY (InstanceID, StationSchemeID, OperationPlanID, TemplateID))";
        db.ExecuteNonQuery(sql);
    }

    internal static OperationProcessTemplate? FindTemplate(DBConnector db, ProcessScope scope, string templateID, bool lockForUpdate = false)
    {
        var lockClause = lockForUpdate && DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName) ? " FOR UPDATE" : "";
        var row = db.Query<TemplateRow>($"SELECT InstanceID, StationSchemeID, OperationPlanID, TemplateID, Revision, Document FROM operationprocesstemplate WHERE {ScopeFilter} AND TemplateID = @TemplateID{lockClause}",
            new { scope.InstanceID, scope.StationSchemeID, scope.OperationPlanID, TemplateID = templateID })?.FirstOrDefault();
        return row is null ? null : ReadDocument(row);
    }

    private static OperationProcessTemplate LoadTemplate(DBConnector db, ProcessScope scope, string templateID, ProcessCatalog? catalog = null)
    {
        var template = FindTemplate(db, scope, templateID) ?? throw new ProcessRequestException(404, "当前站场方案下不存在此模板。");
        OperationProcessEventNodes.Refresh(template, catalog ?? LoadCatalog(db, scope));
        return template;
    }

    private static OperationProcessTemplate ReadDocument(TemplateRow row)
    {
        var template = JsonSerializer.Deserialize<OperationProcessTemplate>(row.Document, JsonOptions)
                       ?? throw new InvalidOperationException("Stored operation process document was null.");
        template.Revision = row.Revision;
        // Scheme ownership is authoritative; template libraries have no execution plan.
        template.InstanceID = row.InstanceID;
        template.StationSchemeID = row.StationSchemeID;
        template.OperationPlanID = row.OperationPlanID;
        template.Id = row.TemplateID;
        return template;
    }

    private static void SaveTemplate(DBConnector db, ProcessScope scope, OperationProcessTemplate template)
    {
        if (template.Revision <= 0) throw new ProcessRequestException(400, "修改模板时必须提供当前 revision。");
        var catalog = LoadCatalog(db, scope);
        var original = LoadTemplate(db, scope, template.Id, catalog);
        if (original.Revision != template.Revision) throw ConflictException();
        Validate(template, catalog);
        var originalActivities = original.Activities.ToDictionary(activity => activity.Id, StringComparer.Ordinal);
        var endpointErrors = template.Activities
            .Where(activity => originalActivities.TryGetValue(activity.Id, out var existing) &&
                (activity.StartEvent != existing.StartEvent || activity.EndEvent != existing.EndEvent))
            .Select(activity => $"活动 {activity.Id} 的开始和结束事件创建后不能更换；可编辑事件本身的属性。")
            .ToList();
        if (endpointErrors.Count > 0) throw new ProcessRequestException(400, endpointErrors[0], endpointErrors);
        var renamedActivities = template.Activities
            .Where(activity => originalActivities.TryGetValue(activity.Id, out var existing) &&
                !string.Equals(activity.Name, existing.Name, StringComparison.Ordinal))
            .ToList();
        var eventNameErrors = renamedActivities.Where(activity => activity.Name.Length > 198)
            .Select(activity => $"活动 {activity.Id} 名称最多为 198 个字符，以便自动生成不超过 200 个字符的起止事件名称。")
            .ToList();
        if (eventNameErrors.Count > 0) throw new ProcessRequestException(400, eventNameErrors[0], eventNameErrors);
        if (renamedActivities.Count > 0)
        {
            var events = template.Events.ToDictionary(item => item.Id, StringComparer.Ordinal);
            foreach (var activity in renamedActivities)
            {
                // Match the editor's naming convention; unrelated saves preserve hand-edited event names.
                events[activity.StartEvent].Name = $"{activity.Name}开始";
                events[activity.EndEvent].Name = $"{activity.Name}结束";
            }
        }
        var expectedRevision = template.Revision;
        template.Revision = checked(template.Revision + 1);
        var document = JsonSerializer.Serialize(template, JsonOptions);
        db.BeginTransaction();
        try
        {
            var rows = db.ExecuteNonQuery($@"UPDATE operationprocesstemplate
                SET Revision = @Revision, Document = @Document, UpdatedAtUtc = @UpdatedAtUtc
                WHERE {ScopeFilter} AND TemplateID = @TemplateID AND Revision = @ExpectedRevision",
                new { template.InstanceID, template.StationSchemeID, template.OperationPlanID, TemplateID = template.Id,
                    template.Revision, Document = document, UpdatedAtUtc = DateTime.UtcNow, ExpectedRevision = expectedRevision });
            if (rows != 1) throw ConflictException();
            db.Commit();
        }
        catch { db.Rollback(); template.Revision = expectedRevision; throw; }
    }

    private static void Validate(OperationProcessTemplate template, ProcessCatalog catalog)
    {
        var errors = OperationProcessValidator.Validate(template, catalog);
        if (errors.Count > 0) throw new ProcessRequestException(400, errors[0], errors);
    }

    private static object Parameters(OperationProcessTemplate template, string? document = null) => new {
        template.InstanceID, template.StationSchemeID, template.OperationPlanID, TemplateID = template.Id,
        template.Revision, Document = document, UpdatedAtUtc = DateTime.UtcNow
    };

    private static void SetScope(OperationProcessTemplate template, ProcessScope scope)
    {
        template.InstanceID = scope.InstanceID;
        template.StationSchemeID = scope.StationSchemeID;
        template.OperationPlanID = scope.OperationPlanID;
    }

    internal static ProcessCatalog LoadCatalog(DBConnector db, ProcessScope scope)
    {
        const string layoutFilter = "InstanceID = @InstanceID AND StationSchemeID = @StationSchemeID";
        var nodes = db.Query<StationNodeRow>($"SELECT ID, X, Y FROM node WHERE {layoutFilter} ORDER BY ID", scope) ?? new();
        var links = db.Query<StationLinkRow>($"SELECT ID, Name, FromNodeID, ToNodeID FROM link WHERE {layoutFilter} ORDER BY ID", scope) ?? new();
        var routes = db.Query<StationRouteRow>($"SELECT ID, Type, Description, StartNodeID, EndNodeID, NodeList, LinkList FROM stationroute WHERE {layoutFilter} ORDER BY ID", scope) ?? new();
        return new ProcessCatalog {
            Nodes = nodes.Select(x => new ProcessCatalogNode { Id = x.ID.ToString(), Name = $"节点 {x.ID}" }).ToList(),
            Tracks = links.Select(x => new ProcessCatalogTrack { Id = x.ID.ToString(), Name = string.IsNullOrWhiteSpace(x.Name) ? "" : x.Name,
                FromNodeID = x.FromNodeID.ToString(), ToNodeID = x.ToNodeID.ToString() }).ToList(),
            Routes = routes.Where(x => !string.IsNullOrWhiteSpace(x.ID)).Select(x => {
                var nodeIDs = ParseIDs(x.NodeList);
                return new ProcessCatalogRoute { Id = x.ID!, Name = string.IsNullOrWhiteSpace(x.Description) ? x.ID! : x.Description,
                    Type = NormalizeRouteType(x.Type), StartNodeID = NullIfBlank(x.StartNodeID) ?? nodeIDs.FirstOrDefault(),
                    EndNodeID = NullIfBlank(x.EndNodeID) ?? nodeIDs.LastOrDefault(), TrackIDs = ParseIDs(x.LinkList) };
            }).ToList()
        };
    }

    private static List<string> ParseIDs(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return new();
        var text = value.Trim();
        if (text.StartsWith('['))
        {
            try
            {
                using var document = JsonDocument.Parse(text);
                return document.RootElement.EnumerateArray().Select(x => x.ToString().Trim())
                    .Where(x => x.Length > 0).Distinct(StringComparer.Ordinal).ToList();
            }
            catch (JsonException) { /* Some legacy lists use unquoted identifiers inside brackets. */ }
        }
        return Regex.Split(text.Trim('[', ']'), @"(?:\s*->\s*)|[\s,，;；]+").Select(x => x.Trim('"', '\''))
            .Where(x => x.Length > 0).Distinct(StringComparer.Ordinal).ToList();
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NormalizeRouteType(string? value) => value?.Trim().ToLowerInvariant() switch {
        "arrival" or "接车" or "接车进路" => "Arrival",
        "departure" or "发车" or "发车进路" => "Departure",
        "shunting" or "调车" or "调车进路" => "Shunting",
        "locomotive" or "机车出入段" or "机车出入段进路" or "机车走行" => "Locomotive",
        _ => value?.Trim() ?? ""
    };

    private static List<object> Entities(OperationProcessTemplate template, string collection) => collection switch {
        "activities" => template.Activities.Cast<object>().ToList(),
        "events" => template.Events.Cast<object>().ToList(),
        "precedences" => template.Precedences.Cast<object>().ToList(),
        "anchors" => template.Anchors.Cast<object>().ToList(),
        _ => throw new ProcessRequestException(400, "对象类型必须是 activities、events、precedences 或 anchors。")
    };

    private static string EntityID(object entity) => entity switch {
        ProcessActivity item => item.Id, ProcessEvent item => item.Id,
        ProcessPrecedence item => item.Id, ProcessAnchor item => item.Id,
        _ => throw new InvalidOperationException("Unknown process object type.")
    };

    private static object ParseEntity(JsonElement json, string collection)
    {
        if (json.ValueKind != JsonValueKind.Object) throw new ProcessRequestException(400, "请求体必须为对象。");
        return collection switch {
            "activities" => json.Deserialize<ProcessActivity>(JsonOptions)!,
            "events" => json.Deserialize<ProcessEvent>(JsonOptions)!,
            "precedences" => json.Deserialize<ProcessPrecedence>(JsonOptions)!,
            "anchors" => json.Deserialize<ProcessAnchor>(JsonOptions)!,
            _ => throw new ProcessRequestException(400, "未知的对象类型。")
        };
    }

    private static void PutEntity(OperationProcessTemplate template, string collection, object entity, bool replace)
    {
        var id = EntityID(entity);
        switch (collection)
        {
            case "activities": if (replace) template.Activities.RemoveAll(x => x.Id == id); template.Activities.Add((ProcessActivity)entity); break;
            case "events": if (replace) template.Events.RemoveAll(x => x.Id == id); template.Events.Add((ProcessEvent)entity); break;
            case "precedences": if (replace) template.Precedences.RemoveAll(x => x.Id == id); template.Precedences.Add((ProcessPrecedence)entity); break;
            case "anchors": if (replace) template.Anchors.RemoveAll(x => x.Id == id); template.Anchors.Add((ProcessAnchor)entity); break;
        }
    }

    private static void RemoveEntity(OperationProcessTemplate template, string collection, string id)
    {
        switch (collection)
        {
            case "activities":
                var activity = template.Activities.Single(x => x.Id == id);
                template.Activities.Remove(activity);
                var unusedEventIDs = new[] { activity.StartEvent, activity.EndEvent }
                    .Where(eventID => !template.Activities.Any(x => x.StartEvent == eventID || x.EndEvent == eventID)).ToHashSet(StringComparer.Ordinal);
                template.Events.RemoveAll(x => unusedEventIDs.Contains(x.Id));
                template.Precedences.RemoveAll(x => unusedEventIDs.Contains(x.LeadingEvent) || unusedEventIDs.Contains(x.FollowingEvent));
                break;
            case "events":
                if (template.Activities.Any(x => x.StartEvent == id || x.EndEvent == id) || template.Precedences.Any(x => x.LeadingEvent == id || x.FollowingEvent == id))
                    throw new ProcessRequestException(400, "此事件仍被活动或次序引用，请先删除关联对象或整体更新模板。");
                template.Events.RemoveAll(x => x.Id == id);
                break;
            case "precedences": template.Precedences.RemoveAll(x => x.Id == id); break;
            case "anchors":
                template.Anchors.RemoveAll(x => x.Id == id);
                foreach (var ev in template.Events) { ev.AnchorList.RemoveAll(x => x == id); if (ev.SelectedAnchor == id) ev.SelectedAnchor = null; }
                foreach (var binding in template.RouteAnchors) { if (binding.StartAnchor == id) binding.StartAnchor = null; if (binding.EndAnchor == id) binding.EndAnchor = null; }
                break;
        }
    }

    private static bool IsDuplicate(Exception ex)
    {
        for (Exception? current = ex; current is not null; current = current.InnerException)
            if (current is Microsoft.Data.Sqlite.SqliteException { SqliteErrorCode: 19 } ||
                current is MySqlConnector.MySqlException { Number: 1062 }) return true;
        return false;
    }

    private static ProcessRequestException ConflictException() => new(409, "模板已被其他操作修改，请重新加载后再保存。");
    private sealed class TemplateRow
    {
        public string InstanceID { get; set; } = "";
        public string StationSchemeID { get; set; } = "";
        public string OperationPlanID { get; set; } = "";
        public string TemplateID { get; set; } = "";
        public int Revision { get; set; }
        public string Document { get; set; } = "";
    }
    private sealed class ProcessRequestException : Exception
    {
        public int StatusCode { get; }
        public List<string>? Errors { get; }
        public ProcessRequestException(int statusCode, string message, List<string>? errors = null) : base(message)
            { StatusCode = statusCode; Errors = errors; }
    }
}
