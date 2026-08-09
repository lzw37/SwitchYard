using System.Text.Json;
using SwitchYard.Capacity;

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
                context.InstanceId,
                context.StationSchemeId,
                context.SourceOperationPlanId
            }) ?? new List<OperationPlanRow>()).FirstOrDefault()
            ?? throw new InvalidOperationException("源作业计划不存在。");

        var sourceTrains = db.Query<TrainRow>(
            $@"SELECT InstanceID, StationSchemeID, OperationPlanID, {Quote("ID")}, TrainTemplateID,
                      TrainNumber, Name, TrainType, IsFixedOperation
               FROM {Quote("train")}
               WHERE InstanceID = @instanceId
                 AND StationSchemeID = @stationSchemeId
                 AND OperationPlanID = @sourceOperationPlanId",
            new
            {
                context.InstanceId,
                context.StationSchemeId,
                context.SourceOperationPlanId
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
                context.InstanceId,
                context.StationSchemeId,
                context.SourceOperationPlanId
            }) ?? new List<MovementRow>();

        var trainMap = sourceTrains
            .Where(train => !string.IsNullOrWhiteSpace(train.ID))
            .ToDictionary(train => train.ID!, StringComparer.OrdinalIgnoreCase);
        var movementMap = sourceMovements
            .Where(movement => !string.IsNullOrWhiteSpace(movement.TrainID) && !string.IsNullOrWhiteSpace(movement.MovementID))
            .ToDictionary(
                movement => MovementKey(movement.TrainID!, movement.MovementID!),
                StringComparer.OrdinalIgnoreCase);

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
            CopyTemplates(db, context, targetPlanId);
            InsertSolvedPlan(db, context, targetPlanId, result, trainMap, movementMap);
            db.Commit();
            return targetPlan;
        }
        catch
        {
            db.Rollback();
            throw;
        }
    }

    private static void CopyTemplates(DBConnector db, SaturatedPlanJobContext context, string targetPlanId)
    {
        db.ExecuteNonQuery(
            $@"INSERT INTO {Quote("traintemplate")} (
                   InstanceID, StationSchemeID, OperationPlanID, TrainTemplateID, Name, {Quote("Type")}, {Quote("Number")}, IsFixedOperation)
               SELECT InstanceID, StationSchemeID, @targetPlanId, TrainTemplateID, Name, {Quote("Type")}, {Quote("Number")}, IsFixedOperation
               FROM {Quote("traintemplate")}
               WHERE InstanceID = @instanceId
                 AND StationSchemeID = @stationSchemeId
                 AND OperationPlanID = @sourceOperationPlanId",
            new
            {
                targetPlanId,
                context.InstanceId,
                context.StationSchemeId,
                context.SourceOperationPlanId
            });
        db.ExecuteNonQuery(
            $@"INSERT INTO {Quote("movementtemplate")} (
                   InstanceID, StationSchemeID, OperationPlanID, TrainTemplateID, MovementID,
                   Name, RouteIDList, MinDuration, SortOrder)
               SELECT InstanceID, StationSchemeID, @targetPlanId, TrainTemplateID, MovementID,
                      Name, RouteIDList, MinDuration, SortOrder
               FROM {Quote("movementtemplate")}
               WHERE InstanceID = @instanceId
                 AND StationSchemeID = @stationSchemeId
                 AND OperationPlanID = @sourceOperationPlanId",
            new
            {
                targetPlanId,
                context.InstanceId,
                context.StationSchemeId,
                context.SourceOperationPlanId
            });
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

            db.ExecuteNonQuery(
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

            for (var index = 0; index < solvedTrain.Movements.Count; index++)
            {
                var solvedMovement = solvedTrain.Movements[index];
                if (!movementMap.TryGetValue(MovementKey(solvedTrain.Id, solvedMovement.Id), out var sourceMovement))
                {
                    throw new InvalidOperationException(
                        $"求解结果中的列车作业 {solvedTrain.Id}/{solvedMovement.Id} 无法在源作业计划中找到。");
                }

                db.ExecuteNonQuery(
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
            }
        }
    }

    private static void InsertOperationPlan(DBConnector db, OperationPlanRow plan)
    {
        db.ExecuteNonQuery(
            $@"INSERT INTO {Quote("operationplan")} (
                   InstanceID, StationSchemeID, OperationPlanID, Name, Description, SortOrder, CreatedDate, UpdatedDate)
               VALUES (
                   @InstanceID, @StationSchemeID, @OperationPlanID, @Name, @Description, @SortOrder, @CreatedDate, @UpdatedDate)",
            plan);
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

    private static string FormatTime(int seconds, string? formatted)
    {
        if (!string.IsNullOrWhiteSpace(formatted))
        {
            return formatted.Trim();
        }

        var clamped = Math.Clamp(seconds, 0, 86_400);
        return $"{clamped / 3600:00}:{clamped % 3600 / 60:00}:{clamped % 60:00}";
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
