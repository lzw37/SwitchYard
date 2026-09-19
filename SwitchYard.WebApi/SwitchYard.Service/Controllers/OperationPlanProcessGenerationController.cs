using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SwitchYard.Capacity;
using SwitchYard.Service.Models;
using SwitchYard.Service.Services;

namespace SwitchYard.Service.Controllers;

public partial class OperationPlanController
{
    [HttpPost(Name = "GenerateTrainTemplateFromProcess")]
    public IActionResult GenerateTrainTemplateFromProcess([FromBody] GenerateTrainTemplateFromProcessRequest? request)
    {
        DBConnector? db = null;
        try
        {
            var scope = new ProcessScope {
                InstanceID = request?.InstanceID?.Trim() ?? "", StationSchemeID = request?.StationSchemeID?.Trim() ?? "",
                OperationPlanID = request?.OperationPlanID?.Trim() ?? ""
            };
            if (new[] { scope.InstanceID, scope.StationSchemeID, scope.OperationPlanID }.Any(id => id.Length is 0 or > 50) ||
                string.IsNullOrWhiteSpace(request?.ProcessTemplateID) || request.ProcessTemplateID.Length > 100 || request.Revision is null or <= 0)
                return BadRequest(new { message = "请提供有效的实例、站场方案、作业计划、已保存作业过程 id 和正整数 revision。" });

            db = GetCapacityDbConnector();
            var permissionError = AuthorizeProcessGenerationScope(db, scope);
            if (permissionError is not null) return permissionError;
            // Schema preparation may issue DDL (and backfills); keep it outside the atomic insert.
            EnsureOperationPlanTemplateSchema(db);
            OperationProcessController.EnsureSchema(db);
            db.BeginTransaction();
            // SQLite's write transaction and MySQL's locking read serialize source edits/deletes
            // until commit. All validation and inserts use this same transaction connection.
            var source = OperationProcessController.FindTemplate(db, scope, request.ProcessTemplateID.Trim(), lockForUpdate: true);
            if (source is null) { db.Rollback(); return NotFound(new { message = "当前作业计划下不存在此已保存作业过程。" }); }
            if (source.Revision != request.Revision) { db.Rollback(); return ProcessGenerationRevisionConflict(); }
            var catalog = OperationProcessController.LoadCatalog(db, scope);
            var errors = OperationProcessValidator.Validate(source, catalog);
            if (source.Activities is { Count: 0 }) errors.Add("作业过程没有活动，请先添加活动并保存。");
            if (errors.Count > 0)
            {
                db.Rollback();
                return BadRequest(new { message = "已保存作业过程与当前站场数据不一致，请检查并重新保存。", errors });
            }
            var trainTemplateID = GenerateTrainTemplateID(db, scope.InstanceID, scope.StationSchemeID, scope.OperationPlanID);
            var projection = OperationProcessTrainTemplateGenerator.Build(source, catalog, trainTemplateID,
                () => GenerateMovementID(db, scope.InstanceID, scope.StationSchemeID, scope.OperationPlanID, trainTemplateID));
            var insertedTrainCount = db.ExecuteNonQuery($@"INSERT INTO {QuoteIdentifier("traintemplate")}
                (InstanceID, StationSchemeID, OperationPlanID, TrainTemplateID, Name, {QuoteIdentifier("Type")}, {QuoteIdentifier("Number")}, IsFixedOperation)
                VALUES (@InstanceID, @StationSchemeID, @OperationPlanID, @TrainTemplateID, @Name, @Type, @Number, @IsFixedOperation)", projection.TrainTemplate);
            if (insertedTrainCount != 1) throw new InvalidOperationException("Expected exactly one generated train template to be inserted.");
            foreach (var movement in projection.Movements)
            {
                var insertedMovementCount = db.ExecuteNonQuery($@"INSERT INTO {QuoteIdentifier("movementtemplate")}
                    (InstanceID, StationSchemeID, OperationPlanID, TrainTemplateID, MovementID, Name, RouteIDList, MinDuration, SortOrder)
                    VALUES (@InstanceID, @StationSchemeID, @OperationPlanID, @TrainTemplateID, @MovementID, @Name, @RouteIDList, @MinDuration, @SortOrder)", movement);
                if (insertedMovementCount != 1) throw new InvalidOperationException("Expected exactly one generated movement template to be inserted.");
            }
            var current = OperationProcessController.FindTemplate(db, scope, source.Id, lockForUpdate: true);
            if (current is null || current.Revision != request.Revision)
            {
                db.Rollback();
                return ProcessGenerationRevisionConflict();
            }
            db.Commit();
            return Ok(new GenerateTrainTemplateFromProcessResponse {
                TrainTemplate = projection.TrainTemplate, MovementCount = projection.Movements.Count, Warnings = projection.Warnings
            });
        }
        catch (ArgumentException ex)
        {
            db?.Rollback();
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            db?.Rollback();
            _logger.LogError(ex, "Failed to generate train template from operation process.");
            return StatusCode(500, new { message = "生成列车模板失败，本次列车模板及作业行均未保存。" });
        }
    }

    private ConflictObjectResult ProcessGenerationRevisionConflict() => Conflict(new {
        message = "作业过程已被修改或删除，请刷新并重新选择已保存版本。"
    });

    private IActionResult? AuthorizeProcessGenerationScope(DBConnector db, ProcessScope scope)
    {
        var instance = db.Query<CapacityInstance>("SELECT * FROM capacityinstance WHERE ID = @InstanceID", scope)?.FirstOrDefault();
        if (instance is null) return NotFound(new { message = "能力分析实例不存在。" });
        var username = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(username)) return Unauthorized(new { message = "登录用户信息无效。" });
        var admin = string.Equals(username, "Admin", StringComparison.OrdinalIgnoreCase) ||
            User.Claims.Any(c => c.Type == ClaimTypes.Role && string.Equals(c.Value, "Admin", StringComparison.OrdinalIgnoreCase));
        if (!admin && !string.Equals(instance.Owner, username, StringComparison.Ordinal))
            return StatusCode(403, new { message = "无权访问此能力分析实例。" });
        if ((db.Query<int>("SELECT COUNT(1) FROM stationscheme WHERE InstanceID = @InstanceID AND ID = @StationSchemeID", scope)?.FirstOrDefault() ?? 0) == 0)
            return NotFound(new { message = "当前实例下不存在此站场方案。" });
        if (!OperationPlanExists(db, scope.InstanceID, scope.StationSchemeID, scope.OperationPlanID))
            return NotFound(new { message = "当前站场方案下不存在此作业计划。" });
        return null;
    }
}
