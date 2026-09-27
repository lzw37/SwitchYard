using System.Text.Json;
using SwitchYard.Capacity;
using SwitchYard.Service.Controllers;
using SwitchYard.Service.Models;

namespace SwitchYard.Service.Services;

public sealed class SaturatedPlanJobContext
{
    public string InstanceId { get; init; } = string.Empty;
    public string StationSchemeId { get; init; } = string.Empty;
    public string SourceOperationPlanId { get; init; } = string.Empty;
    public string PresetId { get; init; } = string.Empty;
    public string PresetName { get; init; } = string.Empty;
    public string RequestedBy { get; init; } = string.Empty;
    public bool RequestedByAdmin { get; init; }
}

public sealed class SaturatedOperationPlanService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public OperationPlanRow Save(
        SaturatedPlanJobContext context,
        JsonElement resultElement,
        string jobId)
    {
        var result = resultElement.Deserialize<StationCapacitySolveResult>(JsonOptions)
            ?? throw new InvalidOperationException("CapacityAgent 返回的求解结果为空。");
        if (!result.HasSolution ||
            !(string.Equals(result.Status, "optimal", StringComparison.OrdinalIgnoreCase) ||
              string.Equals(result.Status, "feasible", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"求解未得到可保存的可行解，状态：{result.Status}。");
        }
        if (result.Trains.Count == 0 || result.Trains.All(train => train.Movements.Count == 0))
        {
            throw new InvalidOperationException("求解结果中没有列车作业。");
        }

        var db = DBConnector.GetDBConnector(DBConnector.CapacityDatabaseSectionName);
        var instance = (db.Query<CapacityInstance>(
            "SELECT ID, Name, Owner, CreatedDate, IsActive FROM capacityinstance WHERE ID = @id",
            new { id = context.InstanceId }) ?? new List<CapacityInstance>()).FirstOrDefault()
            ?? throw new InvalidOperationException("能力计算实例不存在。");
        if (!context.RequestedByAdmin &&
            !string.Equals(instance.Owner, context.RequestedBy, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("当前用户无权保存该实例的饱和计划。");
        }

        var sourcePlan = (db.Query<OperationPlanRow>(
            $@"SELECT InstanceID, StationSchemeID, OperationPlanID, Name, Description, SortOrder, CreatedDate, UpdatedDate
               FROM {Quote("operationplan")}
               WHERE InstanceID = @instanceId
                 AND StationSchemeID = @stationSchemeId
                 AND OperationPlanID = @sourceOperationPlanId
               LIMIT 1",
            new
            {
                instanceId = context.InstanceId,
                stationSchemeId = context.StationSchemeId,
                sourceOperationPlanId = context.SourceOperationPlanId
            }) ?? new List<OperationPlanRow>()).FirstOrDefault()
            ?? throw new InvalidOperationException("源作业计划不存在。");

        SchemeTemplateStore.Migrate(db, context.InstanceId, context.StationSchemeId);
        var sourceTrains = db.Query<TrainRow>(
            $@"SELECT InstanceID, StationSchemeID, OperationPlanID, {Quote("ID")}, TrainTemplateID,
                      TrainNumber, Name, TrainType, IsFixedOperation
               FROM {Quote("train")}
               WHERE InstanceID = @instanceId
                 AND StationSchemeID = @stationSchemeId
                 AND OperationPlanID = @sourceOperationPlanId",
            new
            {
                instanceId = context.InstanceId,
                stationSchemeId = context.StationSchemeId,
                sourceOperationPlanId = context.SourceOperationPlanId
            }) ?? new List<TrainRow>();
        var sourceMovements = db.Query<MovementRow>(
            $@"SELECT InstanceID, StationSchemeID, OperationPlanID, TrainID, TrainTemplateID,
                      MovementID, Name, RouteIDList, MinDuration, EarliestStartTime, LatestEndTime,
                      {Quote("Route")}, Tag, SortOrder
               FROM {Quote("movement")}
               WHERE InstanceID = @instanceId
                 AND StationSchemeID = @stationSchemeId
                 AND OperationPlanID = @sourceOperationPlanId",
            new
            {
                instanceId = context.InstanceId,
                stationSchemeId = context.StationSchemeId,
                sourceOperationPlanId = context.SourceOperationPlanId
            }) ?? new List<MovementRow>();

        var trainMap = sourceTrains
            .Where(train => !string.IsNullOrWhiteSpace(train.ID))
            .ToDictionary(train => train.ID!, StringComparer.OrdinalIgnoreCase);
        var movementMap = sourceMovements
            .Where(movement => !string.IsNullOrWhiteSpace(movement.TrainID) && !string.IsNullOrWhiteSpace(movement.MovementID))
            .ToDictionary(
                movement => MovementKey(movement.TrainID!, movement.MovementID!),
                StringComparer.OrdinalIgnoreCase);
        var sourceScope = new ProcessScope
        {
            InstanceID = context.InstanceId, StationSchemeID = context.StationSchemeId,
            OperationPlanID = context.SourceOperationPlanId
        };
        var processSnapshots = TrainProcessSnapshotStore.LoadAll(db, sourceScope);
        var solvedSnapshots = PrepareProcessSnapshots(db, sourceScope, result, processSnapshots);

        var now = DateTime.Now;
        var targetPlanId = GenerateOperationPlanId(db, context.InstanceId, context.StationSchemeId);
        var sourceName = string.IsNullOrWhiteSpace(sourcePlan.Name)
            ? sourcePlan.OperationPlanID ?? "作业计划"
            : sourcePlan.Name;
        var targetPlan = new OperationPlanRow
        {
            InstanceID = context.InstanceId,
            StationSchemeID = context.StationSchemeId,
            OperationPlanID = targetPlanId,
            Name = Trim($"{sourceName} - 饱和计划 {now:yyyyMMdd HHmm}", 100),
            Description = Trim($"由作业计划“{sourceName}”使用求解预设“{context.PresetName}”生成；任务 {jobId}。", 500),
            SortOrder = GetNextSortOrder(db, context.InstanceId, context.StationSchemeId),
            CreatedDate = now,
            UpdatedDate = now
        };

        db.BeginTransaction();
        try
        {
            InsertOperationPlan(db, targetPlan);
            InsertSolvedPlan(db, context, targetPlanId, result, trainMap, movementMap);
            foreach (var snapshot in solvedSnapshots)
            {
                snapshot.OperationPlanID = targetPlanId;
                snapshot.Process.OperationPlanID = targetPlanId;
                TrainProcessSnapshotStore.Insert(db, new ProcessScope
                {
                    InstanceID = context.InstanceId, StationSchemeID = context.StationSchemeId,
                    OperationPlanID = targetPlanId
                }, snapshot);
            }
            db.Commit();
            return targetPlan;
        }
        catch
        {
            db.Rollback();
            throw;
        }
    }

    private static List<TrainProcessSnapshot> PrepareProcessSnapshots(DBConnector db, ProcessScope scope,
        StationCapacitySolveResult result, IReadOnlyList<TrainProcessSnapshot> snapshots)
    {
        if (snapshots.Count == 0) return new();
        var catalog = OperationProcessController.LoadCatalog(db, scope);
        var solvedTrains = result.Trains.ToDictionary(train => train.Id, StringComparer.Ordinal);
        var copies = new List<TrainProcessSnapshot>();
        foreach (var snapshot in snapshots)
        {
            if (!solvedTrains.TryGetValue(snapshot.TrainID, out var solved))
                continue;
            var errors = OperationProcessValidator.Validate(snapshot.Process, catalog);
            if (errors.Count > 0) throw new InvalidOperationException("过程快照或当前站场资源无效：" + string.Join("；", errors));
            if (solved.EventTimes is null || solved.EventTimes.Count != snapshot.Process.Events.Count)
                throw new InvalidOperationException($"求解结果缺少列车 {snapshot.TrainID} 的完整事件时刻。");
            OperationProcessPlanScheduler.ValidateSchedule(snapshot.Process, solved.EventTimes, snapshot.OriginSeconds);
            var resources = OperationProcessPlanScheduler.ResolveResources(snapshot.Process, catalog);
            var movements = solved.Movements.ToDictionary(movement => movement.Id, StringComparer.Ordinal);
            if (snapshot.ActivityMovementMap.Count != snapshot.Process.Activities.Count ||
                movements.Count != snapshot.Process.Activities.Count ||
                snapshot.ActivityMovementMap.Values.Distinct(StringComparer.Ordinal).Count() != snapshot.Process.Activities.Count)
                throw new InvalidOperationException($"列车 {snapshot.TrainID} 的过程活动映射不完整。");
            var locationsByEvent = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            var selectedTracks = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var activity in snapshot.Process.Activities)
            {
                if (!snapshot.ActivityMovementMap.TryGetValue(activity.Id, out var movementId) ||
                    !movements.TryGetValue(movementId, out var movement))
                    throw new InvalidOperationException($"求解结果缺少活动 {activity.Id} 对应的移动。");
                if (!double.IsFinite(movement.StartSeconds) || !double.IsFinite(movement.EndSeconds) ||
                    Math.Abs(movement.StartSeconds - solved.EventTimes[activity.StartEvent]) > 0.0000001 ||
                    Math.Abs(movement.EndSeconds - solved.EventTimes[activity.EndEvent]) > 0.0000001)
                    throw new InvalidOperationException($"求解结果的活动 {activity.Id} 端点时刻与事件图不一致。");
                string resourceId;
                if (activity.Type == "Dwelling")
                {
                    if (movement.TrackId is null || !resources[activity.Id].TrackIDs.Contains(movement.TrackId, StringComparer.Ordinal) ||
                        !string.IsNullOrEmpty(movement.RouteId))
                        throw new InvalidOperationException($"求解结果的停留活动 {activity.Id} 股道不符合原约束。");
                    resourceId = movement.TrackId;
                    selectedTracks[activity.Id] = resourceId;
                }
                else
                {
                    if (movement.TrackId is not null || !resources[activity.Id].RouteIDs.Contains(movement.RouteId, StringComparer.Ordinal))
                        throw new InvalidOperationException($"求解结果的活动 {activity.Id} 进路不符合原约束。");
                    resourceId = movement.RouteId;
                }
                Intersect(activity.StartEvent, OperationProcessResourceLocations.Get(snapshot.Process, catalog, activity, resourceId, true));
                Intersect(activity.EndEvent, OperationProcessResourceLocations.Get(snapshot.Process, catalog, activity, resourceId, false));
            }
            var copy = JsonSerializer.Deserialize<TrainProcessSnapshot>(
                JsonSerializer.Serialize(snapshot, TrainProcessSnapshotStore.JsonOptions), TrainProcessSnapshotStore.JsonOptions)!;
            copy.EventTimes = new Dictionary<string, double>(solved.EventTimes, StringComparer.Ordinal);
            copy.SelectedTrackIDs = selectedTracks;
            copies.Add(copy);

            void Intersect(string eventId, List<string> locations)
            {
                if (locationsByEvent.TryGetValue(eventId, out var known)) known.IntersectWith(locations);
                else locationsByEvent[eventId] = known = locations.ToHashSet(StringComparer.Ordinal);
                if (known.Count == 0) throw new InvalidOperationException($"求解结果的共享事件 {eventId} 没有一致的地点或锚。");
            }
        }
        return copies;
    }

    private static void InsertSolvedPlan(
        DBConnector db,
        SaturatedPlanJobContext context,
        string targetPlanId,
        StationCapacitySolveResult result,
        IReadOnlyDictionary<string, TrainRow> trainMap,
        IReadOnlyDictionary<string, MovementRow> movementMap)
    {
        foreach (var solvedTrain in result.Trains)
        {
            if (!trainMap.TryGetValue(solvedTrain.Id, out var sourceTrain))
            {
                throw new InvalidOperationException($"求解结果中的列车 {solvedTrain.Id} 无法在源作业计划中找到。");
            }

            var insertedTrain = db.ExecuteNonQuery(
                $@"INSERT INTO {Quote("train")} (
                       InstanceID, StationSchemeID, OperationPlanID, {Quote("ID")}, TrainTemplateID,
                       TrainNumber, Name, TrainType, IsFixedOperation)
                   VALUES (
                       @InstanceID, @StationSchemeID, @OperationPlanID, @ID, @TrainTemplateID,
                       @TrainNumber, @Name, @TrainType, @IsFixedOperation)",
                new TrainRow
                {
                    InstanceID = context.InstanceId,
                    StationSchemeID = context.StationSchemeId,
                    OperationPlanID = targetPlanId,
                    ID = sourceTrain.ID,
                    TrainTemplateID = sourceTrain.TrainTemplateID,
                    TrainNumber = sourceTrain.TrainNumber,
                    Name = sourceTrain.Name,
                    TrainType = sourceTrain.TrainType,
                    IsFixedOperation = sourceTrain.IsFixedOperation
                });
            if (insertedTrain != 1) throw new InvalidOperationException("保存饱和计划列车失败。");

            for (var index = 0; index < solvedTrain.Movements.Count; index++)
            {
                var solvedMovement = solvedTrain.Movements[index];
                if (!movementMap.TryGetValue(MovementKey(solvedTrain.Id, solvedMovement.Id), out var sourceMovement))
                {
                    throw new InvalidOperationException(
                        $"求解结果中的列车作业 {solvedTrain.Id}/{solvedMovement.Id} 无法在源作业计划中找到。");
                }

                var insertedMovement = db.ExecuteNonQuery(
                    $@"INSERT INTO {Quote("movement")} (
                           InstanceID, StationSchemeID, OperationPlanID, TrainID, TrainTemplateID,
                           MovementID, Name, RouteIDList, MinDuration, EarliestStartTime, LatestEndTime,
                           {Quote("Route")}, Tag, SortOrder)
                       VALUES (
                           @InstanceID, @StationSchemeID, @OperationPlanID, @TrainID, @TrainTemplateID,
                           @MovementID, @Name, @RouteIDList, @MinDuration, @EarliestStartTime, @LatestEndTime,
                           @Route, @Tag, @SortOrder)",
                    new MovementRow
                    {
                        InstanceID = context.InstanceId,
                        StationSchemeID = context.StationSchemeId,
                        OperationPlanID = targetPlanId,
                        TrainID = sourceMovement.TrainID,
                        TrainTemplateID = sourceMovement.TrainTemplateID,
                        MovementID = sourceMovement.MovementID,
                        Name = sourceMovement.Name,
                        RouteIDList = sourceMovement.RouteIDList,
                        MinDuration = sourceMovement.MinDuration,
                        EarliestStartTime = FormatTime(solvedMovement.StartSeconds, solvedMovement.StartTime),
                        LatestEndTime = FormatTime(solvedMovement.EndSeconds, solvedMovement.EndTime),
                        Route = solvedMovement.RouteId,
                        Tag = sourceMovement.Tag,
                        SortOrder = sourceMovement.SortOrder ?? index
                    });
                if (insertedMovement != 1) throw new InvalidOperationException("保存饱和计划移动失败。");
            }
        }
    }

    private static void InsertOperationPlan(DBConnector db, OperationPlanRow plan)
    {
        var inserted = db.ExecuteNonQuery(
            $@"INSERT INTO {Quote("operationplan")} (
                   InstanceID, StationSchemeID, OperationPlanID, Name, Description, SortOrder, CreatedDate, UpdatedDate)
               VALUES (
                   @InstanceID, @StationSchemeID, @OperationPlanID, @Name, @Description, @SortOrder, @CreatedDate, @UpdatedDate)",
            plan);
        if (inserted != 1) throw new InvalidOperationException("保存饱和作业计划失败。");
    }

    private static string GenerateOperationPlanId(DBConnector db, string instanceId, string stationSchemeId)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var candidate = $"sat-{DateTime.Now:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..31];
            var exists = (db.Query<OperationPlanRow>(
                $@"SELECT OperationPlanID
                   FROM {Quote("operationplan")}
                   WHERE InstanceID = @instanceId
                     AND StationSchemeID = @stationSchemeId
                     AND OperationPlanID = @candidate
                   LIMIT 1",
                new { instanceId, stationSchemeId, candidate }) ?? new List<OperationPlanRow>()).Any();
            if (!exists)
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("无法生成唯一的饱和作业计划 ID。");
    }

    private static int GetNextSortOrder(DBConnector db, string instanceId, string stationSchemeId)
    {
        var values = db.Query<SortOrderRow>(
            $@"SELECT MAX(SortOrder) AS MaxSortOrder
               FROM {Quote("operationplan")}
               WHERE InstanceID = @instanceId AND StationSchemeID = @stationSchemeId",
            new { instanceId, stationSchemeId }) ?? new List<SortOrderRow>();
        return (values.FirstOrDefault()?.MaxSortOrder ?? -1) + 1;
    }

    private static string MovementKey(string trainId, string movementId) => $"{trainId}\u001f{movementId}";

    private static string FormatTime(double seconds, string? formatted)
    {
        if (!double.IsFinite(seconds) || seconds < 0 || seconds > OperationProcessPlanScheduler.MaximumHorizonSeconds)
            throw new InvalidOperationException("求解结果的移动时刻无效或超出七天范围。");
        return OperationProcessPlanScheduler.FormatTime(seconds);
    }

    private static string Trim(string value, int maxLength)
    {
        var normalized = value.Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private static string Quote(string identifier) =>
        DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName)
            ? $"`{identifier.Replace("`", "``", StringComparison.Ordinal)}`"
            : $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    private sealed class SortOrderRow
    {
        public int? MaxSortOrder { get; set; }
    }
}
