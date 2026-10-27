using SwitchYard.Capacity;
using SwitchYard.Service.Controllers;
using SwitchYard.Service.Models;

namespace SwitchYard.Service.Services;

/// <summary>Links generated trains to the current scheme process library and retains their execution times.</summary>
internal static class StationCapacityProcessInputBuilder
{
    internal const double MaximumHorizonSeconds = 7 * 86_400;

    public static void Apply(DBConnector db, ProcessScope scope, StationCapacitySolveInput input,
        IReadOnlyCollection<OperationProcessTemplate> operationProcesses)
    {
        var snapshots = TrainProcessSnapshotStore.LoadAll(db, scope);
        if (snapshots.Count == 0) return;
        var processes = operationProcesses.ToDictionary(process => process.Id, StringComparer.Ordinal);
        var catalog = OperationProcessController.LoadCatalog(db, scope);
        var trains = input.Trains.ToDictionary(train => train.Id, StringComparer.Ordinal);
        var routes = input.Routes.ToDictionary(route => route.Id, StringComparer.Ordinal);
        var seenTrains = new HashSet<string>(StringComparer.Ordinal);
        foreach (var snapshot in snapshots)
        {
            if (!seenTrains.Add(snapshot.TrainID) || !trains.TryGetValue(snapshot.TrainID, out var train))
                throw Invalid(snapshot, "列车不存在或过程记录重复。");
            if (!processes.TryGetValue(snapshot.SourceTemplateID, out var process))
                throw Invalid(snapshot, "当前作业过程模板已删除或不存在，请重新选择模板并生成作业计划。");
            if (!ValidTime(snapshot.OriginSeconds))
                throw Invalid(snapshot, "基准时刻无效。");
            var movements = train.Movements.ToDictionary(movement => movement.Id, StringComparer.Ordinal);
            if (process.Activities.Count == 0 || snapshot.ActivityMovementMap.Count != process.Activities.Count || movements.Count != process.Activities.Count ||
                snapshot.ActivityMovementMap.Values.Distinct(StringComparer.Ordinal).Count() != process.Activities.Count ||
                process.Activities.Any(activity => !snapshot.ActivityMovementMap.TryGetValue(activity.Id, out var id) || !movements.ContainsKey(id)))
                throw Invalid(snapshot, "当前模板的活动与列车作业不再对应，请重新生成作业计划。");
            var validation = OperationProcessValidator.Validate(process, catalog);
            if (validation.Count > 0) throw Invalid(snapshot, string.Join("；", validation));

            train.OperationProcessId = process.Id;
            train.ProcessOriginSeconds = snapshot.OriginSeconds;
            var eventTimes = process.Events.ToDictionary(ev => ev.Id, ev =>
                snapshot.EventTimes.TryGetValue(ev.Id, out var time) ? time :
                    snapshot.OriginSeconds + (ev.Time is double minutes ? Seconds(minutes) : 0), StringComparer.Ordinal);
            if (eventTimes.Values.Any(time => !ValidTime(time) || time < snapshot.OriginSeconds))
                throw Invalid(snapshot, "原事件时刻无效或超过七天范围。");

            var resources = OperationProcessPlanScheduler.ResolveResources(process, catalog);
            foreach (var activity in process.Activities)
            {
                var movement = movements[snapshot.ActivityMovementMap[activity.Id]];
                movement.ActivityId = activity.Id;
                movement.StartEventId = activity.StartEvent;
                movement.EndEventId = activity.EndEvent;
                movement.OriginalStartSeconds = eventTimes[activity.StartEvent];
                movement.OriginalEndSeconds = eventTimes[activity.EndEvent];
                movement.MinDurationSeconds = Seconds(activity.MinDuration);
                movement.MaxDurationSeconds = Seconds(activity.MaxDuration);
                movement.RequiredRouteTags.Clear();
                if (!resources.TryGetValue(activity.Id, out var candidates))
                    throw Invalid(snapshot, $"活动 {activity.Id} 缺少资源候选。");
                movement.CandidateRouteIds = candidates.RouteIDs.ToList();
                if (movement.CandidateRouteIds.Any(id => !routes.ContainsKey(id)))
                    throw Invalid(snapshot, $"活动 {activity.Id} 的备选进路不存在或没有轨道电路。");
                if (movement.CandidateRouteIds.Count == 0)
                    throw Invalid(snapshot, $"活动 {activity.Id} 没有满足全部约束的候选资源。");
                movement.StartLocationsByRoute.Clear();
                movement.EndLocationsByRoute.Clear();
                foreach (var routeId in movement.CandidateRouteIds)
                {
                    movement.StartLocationsByRoute[routeId] = OperationProcessResourceLocations.Get(process, catalog, activity, routeId, true)
                        .Select(StationCapacityLocationInput.FromLegacyToken).ToList();
                    movement.EndLocationsByRoute[routeId] = OperationProcessResourceLocations.Get(process, catalog, activity, routeId, false)
                        .Select(StationCapacityLocationInput.FromLegacyToken).ToList();
                    if (movement.StartLocationsByRoute[routeId].Count == 0 || movement.EndLocationsByRoute[routeId].Count == 0)
                        throw Invalid(snapshot, $"活动 {activity.Id} 的候选资源没有合法事件地点。");
                }
            }

            var lastTime = eventTimes.Values.Max();
            input.HorizonSeconds = Math.Max(input.HorizonSeconds,
                Math.Min(MaximumHorizonSeconds, lastTime + Math.Max(0, input.Settings.RightShiftToleranceSeconds)));
        }
        StationCapacityProcessReferenceResolver.Resolve(input);
    }

    private static double Seconds(double minutes)
    {
        var seconds = Math.Abs(minutes) <= (double)decimal.MaxValue / 60
            ? (double)((decimal)minutes * 60m) : minutes * 60d;
        if (!double.IsFinite(seconds)) throw new InvalidOperationException("作业过程时间超出可求解范围。");
        return seconds;
    }

    private static bool ValidTime(double value) => double.IsFinite(value) && value >= 0 && value <= MaximumHorizonSeconds;
    private static InvalidOperationException Invalid(TrainProcessSnapshot snapshot, string reason) =>
        new($"列车 {snapshot.TrainID} 引用的作业过程无效：{reason}");
}
