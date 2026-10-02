using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using SwitchYard.Capacity;
using SwitchYard.Service.Models;
using SwitchYard.Service.Services;

namespace SwitchYard.Service.Controllers;

public partial class OperationPlanController
{
    [HttpPost(Name = "GenerateTrainOperationPlanFromProcess")]
    public IActionResult GenerateTrainOperationPlanFromProcess([FromBody] GenerateTrainOperationPlanFromProcessRequest? request)
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
                return BadRequest(new { message = "请提供有效的实例、站场方案、作业计划、已保存过程 id 和正整数 revision。" });
            if (request.TrainCount is < 1 or > 1000)
                return BadRequest(new { message = "一次生成的列车数量必须为 1–1000。" });
            if (!TryParseProcessPlanTime(request.StartTime ?? "00:00", out var startSeconds) ||
                !TryParseProcessPlanTime(request.EndTime ?? "24:00", out var endSeconds))
                return BadRequest(new { message = "时间范围格式应为 HH:mm、HH:mm:ss 或 D+天数 HH:mm:ss。" });
            if (endSeconds <= startSeconds)
                endSeconds += (Math.Floor((startSeconds - endSeconds) / 86400d) + 1) * 86400d;
            if (endSeconds > int.MaxValue)
                return BadRequest(new { message = "时间范围超出支持的秒数范围。" });

            db = GetCapacityDbConnector();
            var permissionError = AuthorizeProcessGenerationScope(db, scope);
            if (permissionError is not null) return permissionError;
            EnsureTrainOperationPlanSchema(db);
            OperationProcessController.EnsureSchema(db);
            SchemeTemplateStore.Migrate(db, scope.InstanceID, scope.StationSchemeID);
            TrainProcessSnapshotStore.EnsureSchema(db);
            db.BeginTransaction();
            var source = OperationProcessController.FindTemplate(db, scope, request.ProcessTemplateID.Trim(), lockForUpdate: true);
            if (source is null) { db.Rollback(); return NotFound(new { message = "当前站场方案下不存在此已保存作业过程。" }); }
            if (source.Revision != request.Revision) { db.Rollback(); return ProcessGenerationRevisionConflict(); }
            var catalog = OperationProcessController.LoadCatalog(db, scope);
            var errors = OperationProcessValidator.Validate(source, catalog);
            if (source.Activities is { Count: 0 }) errors.Add("作业过程没有活动，请先添加活动并保存。");
            if ((long)(source.Activities?.Count ?? 0) * request.TrainCount > 20000)
                errors.Add("本次生成的活动总数不能超过 20000，请减少列车数量。");
            if (errors.Count > 0)
            {
                db.Rollback();
                return BadRequest(new { message = "已保存作业过程未通过生成检查。", errors });
            }

            var ordered = OperationProcessPlanScheduler.OrderedActivities(source);
            foreach (var activity in ordered)
            {
                if (activity.RouteList.Any(id => id.IndexOfAny(new[] { ',', ';', '，', '；', '\r', '\n', '\t', ' ' }) >= 0) ||
                    activity.RouteList.Distinct(StringComparer.OrdinalIgnoreCase).Count() != activity.RouteList.Count)
                    throw new ArgumentException($"活动“{activity.Name}”的进路 id 无法在现有进路列表字段中完整表达，请先调整分隔符或仅大小写不同的 id。");
                ProcessPlanDurationSeconds(activity);
            }
            var schedules = OperationProcessPlanScheduler.Build(source, catalog, request.TrainCount, startSeconds, endSeconds);
            if (schedules.Count != request.TrainCount) throw new InvalidOperationException("Scheduler did not return the requested train count.");
            var warnings = new List<string> { "已保存每列车完整的作业过程约束，列车起点按平均时间槽定位，同一列车内的事件在固定时刻、时长范围和次序间隔等全部约束下尽早发生；次序间隔为 0 且无其他约束阻碍时直接衔接。本次未做跨列车占用冲突优化。" };
            var trainName = ProcessPlanName(source.Name, "列车", warnings);
            var activityNames = ordered.ToDictionary(activity => activity.Id, activity => ProcessPlanName(activity.Name, $"活动 {activity.Id}", warnings), StringComparer.Ordinal);
            var sourceJson = JsonSerializer.Serialize(source, TrainProcessSnapshotStore.JsonOptions);
            var generatedTrainIDs = new List<string>();
            var nextTrainNumber = (long)LoadTrains(db, scope.InstanceID, scope.StationSchemeID, scope.OperationPlanID)
                .Select(train => GetTrainNumberSortValue(train.TrainNumber)).Where(number => number != int.MaxValue).DefaultIfEmpty(0).Max() + 1;
            foreach (var schedule in schedules)
            {
                var trainID = GenerateOperationTrainID(db, scope.InstanceID, scope.StationSchemeID, scope.OperationPlanID);
                var train = new TrainRow {
                    InstanceID = scope.InstanceID, StationSchemeID = scope.StationSchemeID, OperationPlanID = scope.OperationPlanID,
                    ID = trainID, TrainTemplateID = "", TrainNumber = (nextTrainNumber++).ToString(CultureInfo.InvariantCulture),
                    Name = trainName, TrainType = "", IsFixedOperation = 0
                };
                InsertProcessPlanTrain(db, train);
                var snapshot = new TrainProcessSnapshot {
                    InstanceID = scope.InstanceID, StationSchemeID = scope.StationSchemeID, OperationPlanID = scope.OperationPlanID,
                    TrainID = trainID, SourceTemplateID = source.Id, SourceRevision = source.Revision, SourceName = source.Name,
                    OriginSeconds = schedule.OriginSeconds,
                    Process = JsonSerializer.Deserialize<OperationProcessTemplate>(sourceJson, TrainProcessSnapshotStore.JsonOptions)!,
                    EventTimes = new Dictionary<string, double>(schedule.EventTimes, StringComparer.Ordinal),
                    SelectedTrackIDs = new Dictionary<string, string>(schedule.SelectedTrackIDs, StringComparer.Ordinal)
                };
                var sortOrder = 0;
                foreach (var activity in ordered)
                {
                    var movementID = GenerateOperationMovementID(db, scope.InstanceID, scope.StationSchemeID, scope.OperationPlanID, trainID);
                    snapshot.ActivityMovementMap.Add(activity.Id, movementID);
                    InsertProcessPlanMovement(db, new MovementRow {
                        InstanceID = scope.InstanceID, StationSchemeID = scope.StationSchemeID, OperationPlanID = scope.OperationPlanID,
                        TrainID = trainID, TrainTemplateID = "", MovementID = movementID, Name = activityNames[activity.Id],
                        RouteIDList = string.Join(",", activity.RouteList), MinDuration = ProcessPlanDurationSeconds(activity),
                        EarliestStartTime = OperationProcessPlanScheduler.FormatTime(schedule.EventTimes[activity.StartEvent]),
                        LatestEndTime = OperationProcessPlanScheduler.FormatTime(schedule.EventTimes[activity.EndEvent]),
                        Route = schedule.SelectedRouteIDs.GetValueOrDefault(activity.Id, ""), Tag = "", SortOrder = sortOrder++
                    });
                }
                TrainProcessSnapshotStore.Insert(db, scope, snapshot);
                generatedTrainIDs.Add(trainID);
            }
            var current = OperationProcessController.FindTemplate(db, scope, source.Id, lockForUpdate: true);
            if (current is null || current.Revision != request.Revision)
            {
                db.Rollback();
                return ProcessGenerationRevisionConflict();
            }
            var result = LoadTrainOperationPlan(db, scope.InstanceID, scope.StationSchemeID, scope.OperationPlanID);
            result.GeneratedTrainIDs = generatedTrainIDs;
            result.Warnings = warnings;
            db.Commit();
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            db?.Rollback();
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            db?.Rollback();
            _logger.LogError(ex, "Failed to generate train operation plan from operation process.");
            return StatusCode(500, new { message = "生成列车作业计划失败，本次列车、移动及过程约束均未保存。" });
        }
    }

    private static void InsertProcessPlanTrain(DBConnector db, TrainRow train)
    {
        var inserted = db.ExecuteNonQuery($@"INSERT INTO {QuoteIdentifier("train")}
            (InstanceID, StationSchemeID, OperationPlanID, {QuoteIdentifier("ID")}, TrainTemplateID, TrainNumber, Name, TrainType, IsFixedOperation)
            VALUES (@InstanceID, @StationSchemeID, @OperationPlanID, @ID, @TrainTemplateID, @TrainNumber, @Name, @TrainType, @IsFixedOperation)", train);
        if (inserted != 1) throw new InvalidOperationException("Expected exactly one process train to be inserted.");
    }

    private static void InsertProcessPlanMovement(DBConnector db, MovementRow movement)
    {
        var inserted = db.ExecuteNonQuery($@"INSERT INTO {QuoteIdentifier("movement")}
            (InstanceID, StationSchemeID, OperationPlanID, TrainID, TrainTemplateID, MovementID, Name, RouteIDList,
             MinDuration, EarliestStartTime, LatestEndTime, {QuoteIdentifier("Route")}, Tag, SortOrder)
            VALUES (@InstanceID, @StationSchemeID, @OperationPlanID, @TrainID, @TrainTemplateID, @MovementID, @Name, @RouteIDList,
             @MinDuration, @EarliestStartTime, @LatestEndTime, @Route, @Tag, @SortOrder)", movement);
        if (inserted != 1) throw new InvalidOperationException("Expected exactly one process movement to be inserted.");
    }

    private static bool IsProcessBoundTrain(DBConnector db, string instanceID, string stationSchemeID, string operationPlanID, string trainID) =>
        TrainProcessSnapshotStore.HasTrain(db, new ProcessScope { InstanceID = instanceID, StationSchemeID = stationSchemeID, OperationPlanID = operationPlanID }, trainID);

    private ConflictObjectResult ProcessBoundMovementConflict() => Conflict(new {
        message = "此列车绑定完整作业过程；移动仅可修改名称和标签。请修改原过程后重新生成，或删除整列车，避免破坏事件与次序约束。"
    });

    private static bool IsProcessMovementMetadataOnlyEdit(DBConnector db, MovementRow movement)
    {
        var old = db.Query<MovementRow>($@"SELECT TrainTemplateID, RouteIDList, MinDuration, EarliestStartTime,
            LatestEndTime, {QuoteIdentifier("Route")} AS {QuoteIdentifier("Route")}, SortOrder, CellOccupationOverridesJson FROM {QuoteIdentifier("movement")}
            WHERE InstanceID = @InstanceID AND StationSchemeID = @StationSchemeID AND OperationPlanID = @OperationPlanID
            AND TrainID = @TrainID AND MovementID = @MovementID", movement)?.SingleOrDefault();
        return old is not null && (old.TrainTemplateID ?? "") == (movement.TrainTemplateID ?? "") &&
            (old.RouteIDList ?? "") == (movement.RouteIDList ?? "") && old.MinDuration == movement.MinDuration &&
            (old.EarliestStartTime ?? "") == (movement.EarliestStartTime ?? "") && (old.LatestEndTime ?? "") == (movement.LatestEndTime ?? "") &&
            (old.Route ?? "") == (movement.Route ?? "") && old.SortOrder == movement.SortOrder &&
            (movement.CellOccupationOverridesJson is null ||
             (MovementCellOccupationOverrides.Normalize(old.CellOccupationOverridesJson) ?? "{}") == movement.CellOccupationOverridesJson);
    }

    private static int ProcessPlanDurationSeconds(ProcessActivity activity)
    {
        if (!double.IsFinite(activity.MinDuration) || activity.MinDuration < 0 || activity.MinDuration > int.MaxValue / 60d)
            throw new ArgumentException($"活动“{activity.Name}”的最小持续时间换算为秒后超出整数范围。");
        var seconds = decimal.Ceiling((decimal)activity.MinDuration * 60m);
        if (seconds > int.MaxValue) throw new ArgumentException($"活动“{activity.Name}”的最小持续时间换算为秒后超出整数范围。");
        return (int)seconds;
    }

    private static string ProcessPlanName(string name, string label, List<string> warnings)
    {
        if (name.Length <= 50) return name;
        var boundary = StringInfo.ParseCombiningCharacters(name).TakeWhile(index => index <= 50).LastOrDefault();
        if (boundary == 0) throw new ArgumentException($"{label}名称的首个 Unicode 字符超过旧名称字段长度，无法生成。");
        var shortened = name[..boundary];
        warnings.Add($"{label}名称超过旧字段的 50 字符限制，列表中截短为“{shortened}”，完整名称保留在过程约束快照中。");
        return shortened;
    }

    private static bool TryParseProcessPlanTime(string value, out double seconds)
    {
        seconds = 0;
        var match = Regex.Match(value.Trim(), @"^(?:D\+(\d+)\s+)?(\d+):(\d{2})(?::(\d{2}(?:\.\d+)?))?$", RegexOptions.IgnoreCase);
        if (!match.Success || !double.TryParse(match.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var hours) ||
            !double.TryParse(match.Groups[3].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var minutes) || minutes >= 60)
            return false;
        var days = 0d;
        if (match.Groups[1].Success && !double.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out days)) return false;
        var fraction = 0d;
        if (match.Groups[4].Success && (!double.TryParse(match.Groups[4].Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out fraction) || fraction >= 60)) return false;
        seconds = days * 86400d + hours * 3600d + minutes * 60d + fraction;
        return double.IsFinite(seconds) && seconds >= 0 && seconds <= int.MaxValue;
    }
}
