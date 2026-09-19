using Microsoft.AspNetCore.Mvc;
using SwitchYard.Service.Models;
using SwitchYard.Service.Services;

namespace SwitchYard.Service.Controllers;

public partial class OperationPlanController
{
    [HttpPost(Name = "DeleteTrains")]
    public IActionResult DeleteTrains([FromBody] TrainBatchDeleteRequest? request)
    {
        DBConnector? db = null;
        try
        {
            var scope = new ProcessScope {
                InstanceID = request?.InstanceID?.Trim() ?? "", StationSchemeID = request?.StationSchemeID?.Trim() ?? "",
                OperationPlanID = request?.OperationPlanID?.Trim() ?? ""
            };
            if (new[] { scope.InstanceID, scope.StationSchemeID, scope.OperationPlanID }.Any(id => id.Length is 0 or > 50))
                return BadRequest(new { message = "实例、站场方案和作业计划 id 均必填且最多 50 个字符。" });
            if (request?.TrainIDs is not { Count: >= 1 and <= 10000 } ||
                request.TrainIDs.Any(id => string.IsNullOrWhiteSpace(id) || id.Trim().Length > 50))
                return BadRequest(new { message = "请选择 1–10000 列车；列车 id 不能为空，去除首尾空白后最多 50 个字符。" });

            var trainIDs = request.TrainIDs.Select(id => id.Trim()).Distinct(StringComparer.Ordinal).ToArray();
            // Keep response order, but take database locks in a consistent order. Each IN
            // expansion stays below SQLite's legacy 999-parameter limit, including scope.
            var batches = trainIDs.OrderBy(id => id, StringComparer.Ordinal).Chunk(400).ToArray();
            db = GetCapacityDbConnector();
            var permissionError = AuthorizeProcessGenerationScope(db, scope);
            if (permissionError is not null) return permissionError;
            // MySQL schema changes may commit implicitly; finish them before deletion starts.
            EnsureTrainSchema(db);
            EnsureMovementSchema(db);
            TrainProcessSnapshotStore.EnsureSchema(db);
            db.BeginTransaction();

            const string scopeFilter = "InstanceID = @InstanceID AND StationSchemeID = @StationSchemeID AND OperationPlanID = @OperationPlanID";
            var trainTable = QuoteIdentifier("train");
            var trainKey = QuoteIdentifier("ID");
            var lockClause = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName) ? " FOR UPDATE" : "";
            var existing = new HashSet<string>(StringComparer.Ordinal);
            foreach (var batch in batches)
                existing.UnionWith(db.Query<string>($@"SELECT {trainKey} FROM {trainTable}
                    WHERE {scopeFilter} AND {trainKey} IN @TrainIDs ORDER BY {trainKey}{lockClause}",
                    new { scope.InstanceID, scope.StationSchemeID, scope.OperationPlanID, TrainIDs = batch }) ?? new());
            var missing = trainIDs.Where(id => !existing.Contains(id)).ToArray();
            if (missing.Length > 0)
            {
                db.Rollback();
                return Conflict(new { message = "部分选中列车已不存在或不属于当前作业计划，本次未删除任何列车，请刷新后重试。", missingTrainIDs = missing });
            }

            foreach (var batch in batches)
            {
                var parameters = new { scope.InstanceID, scope.StationSchemeID, scope.OperationPlanID, TrainIDs = batch };
                db.ExecuteNonQuery($"DELETE FROM {QuoteIdentifier("movement")} WHERE {scopeFilter} AND TrainID IN @TrainIDs", parameters);
                db.ExecuteNonQuery($"DELETE FROM {QuoteIdentifier(TrainProcessSnapshotStore.TableName)} WHERE {scopeFilter} AND TrainID IN @TrainIDs", parameters);
                var deleted = db.ExecuteNonQuery($"DELETE FROM {trainTable} WHERE {scopeFilter} AND {trainKey} IN @TrainIDs", parameters);
                if (deleted != batch.Length)
                    throw new InvalidOperationException("Batch deletion did not delete exactly the selected train rows.");
            }
            // A trigger can silently ignore a child deletion (e.g. SQLite RAISE(IGNORE)).
            // Check all three tables after the final batch so no orphan or skipped row can
            // be reported as successfully deleted, including changes made by later triggers.
            foreach (var batch in batches)
            {
                var parameters = new { scope.InstanceID, scope.StationSchemeID, scope.OperationPlanID, TrainIDs = batch };
                foreach (var (table, key) in new[] { ("movement", "TrainID"), (TrainProcessSnapshotStore.TableName, "TrainID"), ("train", "ID") })
                    if ((db.Query<long>($"SELECT COUNT(1) FROM {QuoteIdentifier(table)} WHERE {scopeFilter} AND {QuoteIdentifier(key)} IN @TrainIDs", parameters)?.FirstOrDefault() ?? 0) != 0)
                        throw new InvalidOperationException($"Batch deletion left selected rows in {table}.");
            }
            db.Commit();
            // Train templates and source operation-process templates are never deleted here.
            return Ok(new { deletedTrainIDs = trainIDs, deletedCount = trainIDs.Length });
        }
        catch (Exception ex)
        {
            db?.Rollback();
            _logger.LogError(ex, "Failed to atomically delete selected trains.");
            return StatusCode(500, new { message = "批量删除失败，本次列车、移动及过程约束删除已全部回滚；列车模板和原作业过程未删除。" });
        }
    }
}
