using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SwitchYard.Capacity;
using SwitchYard.Service.Models;
using SwitchYard.Service.Services;

namespace SwitchYard.Service.Controllers;

public partial class OperationPlanController
{
    [HttpPost(Name = "PreviewProcessTrain")]
    public IActionResult PreviewProcessTrain([FromBody] ProcessTrainCreationRequest? request) => BuildProcessTrain(request, save: false);

    [HttpPost(Name = "CreateProcessTrain")]
    public IActionResult CreateProcessTrain([FromBody] ProcessTrainCreationRequest? request) => BuildProcessTrain(request, save: true);

    private IActionResult BuildProcessTrain(ProcessTrainCreationRequest? request, bool save)
    {
        DBConnector? db = null;
        var inTransaction = false;
        try
        {
            var scope = new ProcessScope { InstanceID = request?.InstanceID?.Trim() ?? "", StationSchemeID = request?.StationSchemeID?.Trim() ?? "", OperationPlanID = request?.OperationPlanID?.Trim() ?? "" };
            if (request is null || new[] { scope.InstanceID, scope.StationSchemeID, scope.OperationPlanID }.Any(id => id.Length is 0 or > 50) ||
                string.IsNullOrWhiteSpace(request.ProcessTemplateID) || request.ProcessTemplateID.Length > 100 || request.Revision <= 0)
                return BadRequest(new { message = "请提供有效的计划范围、已保存作业过程和 revision。" });
            var trainID = request.Train?.ID?.Trim() ?? "";
            var trainNumber = request.Train?.TrainNumber?.Trim() ?? "";
            var trainType = request.Train?.TrainType?.Trim() ?? "";
            if (trainID.Length is 0 or > 50 || trainNumber.Length is 0 or > 50 || trainType.Length > 20)
                return BadRequest(new { message = "请填写列车 id、车次（最多 50 字符）及有效列车类型（最多 20 字符）。" });
            if (!TryParseProcessPlanTime(request.OriginTime ?? "", out var origin) || !TryParseProcessPlanTime(request.EndTime ?? "", out var end))
                return BadRequest(new { message = "基准时刻和排布结束时刻应为 HH:mm、HH:mm:ss 或 D+天数 HH:mm:ss。" });
            if (end <= origin) end += (Math.Floor((origin - end) / 86400d) + 1) * 86400d;
            if (end > OperationProcessPlanScheduler.MaximumHorizonSeconds)
                return BadRequest(new { message = "排布范围最长支持从第 1 天起的 7 天。" });

            db = GetCapacityDbConnector();
            var permission = AuthorizeProcessGenerationScope(db, scope);
            if (permission is not null) return permission;

            db.BeginTransaction();
            inTransaction = true;
            var lockSuffix = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName) ? " FOR UPDATE" : "";
            const string filter = "InstanceID=@InstanceID AND StationSchemeID=@StationSchemeID AND OperationPlanID=@OperationPlanID";
            if (!(db.Query<string>($"SELECT OperationPlanID FROM operationplan WHERE {filter}{lockSuffix}", scope)?.Any() ?? false))
                return Abort(NotFound(new { message = "作业计划已被删除。" }));
            var usedIDs = (db.Query<string>($"SELECT ID FROM train WHERE {filter}", scope) ?? new())
                .Concat(db.Query<string>($"SELECT DISTINCT TrainID FROM movement WHERE {filter}", scope) ?? new())
                .Concat(db.Query<string>($"SELECT TrainID FROM trainprocesssnapshot WHERE {filter}", scope) ?? new());
            if (usedIDs.Contains(trainID, StringComparer.OrdinalIgnoreCase))
                return Abort(Conflict(new { message = "列车 id 已存在，请刷新计划，避免重复创建。" }));
            var source = OperationProcessController.FindTemplate(db, scope, request.ProcessTemplateID.Trim(), lockForUpdate: true);
            if (source is null) return Abort(NotFound(new { message = "当前站场方案下不存在此已保存作业过程。" }));
            if (source.Revision != request.Revision) return Abort(ProcessGenerationRevisionConflict());
            var catalog = OperationProcessController.LoadCatalog(db, scope);
            var errors = OperationProcessValidator.Validate(source, catalog);
            if (source.Activities is { Count: 0 }) errors.Add("作业过程没有活动。");
            if (errors.Count > 0) return Abort(BadRequest(new { message = "已保存作业过程未通过生成检查。", errors }));

            // The library template is immutable here. The train snapshot narrows its candidates
            // to the user's permitted choices, retaining every activity, event and constraint.
            var execution = JsonSerializer.Deserialize<OperationProcessTemplate>(JsonSerializer.Serialize(source, TrainProcessSnapshotStore.JsonOptions), TrainProcessSnapshotStore.JsonOptions)!;
            execution.InstanceID = scope.InstanceID;
            execution.StationSchemeID = scope.StationSchemeID;
            execution.OperationPlanID = scope.OperationPlanID;
            ApplyProcessTrainSelections(execution, request.Selections);
            var executionErrors = OperationProcessValidator.Validate(execution, catalog);
            if (executionErrors.Count > 0)
                return Abort(BadRequest(new { message = "所选资源无法形成有效的完整作业过程。", errors = executionErrors }));
            var schedule = OperationProcessPlanScheduler.Build(execution, catalog, 1, origin, end).Single();
            if (Math.Abs(schedule.OriginSeconds - origin) > 0.0000001)
                throw new ArgumentException("无法在指定基准时刻满足全部过程约束，请调整排布结束时刻或过程配置。");
            OperationProcessPlanScheduler.ValidateSchedule(execution, schedule.EventTimes, origin, origin, end);
            var warnings = new List<string> { "已按完整作业过程约束生成单列车；本次未做跨列车 Cell 占用冲突优化。" };
            var train = new TrainRow {
                InstanceID = scope.InstanceID, StationSchemeID = scope.StationSchemeID, OperationPlanID = scope.OperationPlanID,
                ID = trainID, TrainTemplateID = "", TrainNumber = trainNumber,
                Name = ProcessPlanName(string.IsNullOrWhiteSpace(request.Train?.Name) ? source.Name : request.Train.Name.Trim(), "列车", warnings),
                TrainType = trainType, IsFixedOperation = 0
            };
            var snapshot = new TrainProcessSnapshot {
                InstanceID = scope.InstanceID, StationSchemeID = scope.StationSchemeID, OperationPlanID = scope.OperationPlanID,
                TrainID = trainID, SourceTemplateID = source.Id, SourceRevision = source.Revision, SourceName = source.Name,
                OriginSeconds = schedule.OriginSeconds, Process = execution, EventTimes = schedule.EventTimes,
                SelectedRouteIDs = schedule.SelectedRouteIDs
            };
            var movements = new List<MovementRow>();
            var events = execution.Events.ToDictionary(ev => ev.Id, StringComparer.Ordinal);
            foreach (var activity in OperationProcessPlanScheduler.OrderedActivities(execution))
            {
                var movementID = (movements.Count + 1).ToString(CultureInfo.InvariantCulture);
                snapshot.ActivityMovementMap.Add(activity.Id, movementID);
                var routeID = schedule.SelectedRouteIDs[activity.Id];
                movements.Add(new MovementRow {
                    InstanceID = scope.InstanceID, StationSchemeID = scope.StationSchemeID, OperationPlanID = scope.OperationPlanID,
                    TrainID = trainID, TrainTemplateID = "", MovementID = movementID,
                    Name = ProcessPlanName(activity.Name, $"活动 {activity.Id}", warnings), MinDuration = ProcessPlanDurationSeconds(activity),
                    Route = routeID, RouteIDList = routeID, SortOrder = movements.Count, Tag = "",
                    EarliestStartTime = OperationProcessPlanScheduler.FormatTime(schedule.EventTimes[activity.StartEvent]),
                    LatestEndTime = OperationProcessPlanScheduler.FormatTime(schedule.EventTimes[activity.EndEvent]),
                    StartNodeID = events[activity.StartEvent].NodeID, EndNodeID = events[activity.EndEvent].NodeID
                });
            }
            var occupations = new MovementCellOccupationStore(db, scope.InstanceID, scope.StationSchemeID, scope.OperationPlanID, train);
            foreach (var movement in movements) occupations.Prepare(movement);
            TrainProcessSnapshotStore.EnsurePrecedences(snapshot);
            if (save)
            {
                InsertProcessPlanTrain(db, train);
                TrainProcessSnapshotStore.Insert(db, scope, snapshot);
                foreach (var movement in movements)
                    if (InsertMovement(db, movement, occupations) != 1) throw new InvalidOperationException("Process movement was not saved.");
                CapacityDataLifecycle.InvalidateAnalysis(db, scope.InstanceID, scope.StationSchemeID, scope.OperationPlanID);
                db.Commit();
            }
            else db.Rollback();
            inTransaction = false;
            return Ok(new ProcessTrainCreationResponse { Train = train, Movements = movements, ProcessConstraint = snapshot, Warnings = warnings });

            IActionResult Abort(IActionResult error) { db.Rollback(); inTransaction = false; return error; }
        }
        catch (ArgumentException ex)
        {
            if (inTransaction) db?.Rollback();
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            if (inTransaction) db?.Rollback();
            _logger.LogError(ex, "Failed to preview or create a process train.");
            return StatusCode(500, new { message = "未能生成过程列车，本次列车、全部作业及过程快照均未保存。" });
        }
    }

    private static void ApplyProcessTrainSelections(OperationProcessTemplate execution,
        List<ProcessTrainActivitySelection>? selections)
    {
        if (selections is null || selections.Count != execution.Activities.Count || selections.Any(selection => selection is null) ||
            selections.Select(selection => selection.ActivityID?.Trim() ?? "").Distinct(StringComparer.Ordinal).Count() != selections.Count)
            throw new ArgumentException("必须为每个活动提供且只提供一组选项。");
        var selectionMap = selections.ToDictionary(selection => selection.ActivityID?.Trim() ?? "", StringComparer.Ordinal);
        foreach (var activity in execution.Activities)
        {
            if (!selectionMap.TryGetValue(activity.Id, out var selection)) throw new ArgumentException($"缺少活动“{activity.Name}”的选项。");
            var routeID = selection.RouteID?.Trim() ?? "";
            if (!activity.RouteList.Contains(routeID, StringComparer.Ordinal))
                throw new ArgumentException($"活动“{activity.Name}”必须选择原过程备选列表中的进路。");
            if (activity.SelectedRoute is not null && activity.SelectedRoute != routeID)
                throw new ArgumentException($"活动“{activity.Name}”已固定进路，不能改选其他候选。");
            activity.SelectedRoute = routeID;
        }
        // The scheduler resolves shared events and anchor locations after all choices
        // are applied. Precedence contributes only its minimum time interval.
    }
}
