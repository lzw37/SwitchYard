using System.Text;
using SwitchYard.Capacity;
using SwitchYard.Service.Controllers;
using SwitchYard.Service.Models;

namespace SwitchYard.Service.Services;

/// <summary>Adds the immutable generated process graph to the legacy movement projection.</summary>
internal static class StationCapacityProcessInputBuilder
{
    internal const double MaximumHorizonSeconds = 7 * 86_400;

    public static void Apply(DBConnector db, ProcessScope scope, StationCapacitySolveInput input)
    {
        var snapshots = TrainProcessSnapshotStore.LoadAll(db, scope);
        if (snapshots.Count == 0) return;
        input.MinimumModelVersion = "2.0.0";
        var catalog = OperationProcessController.LoadCatalog(db, scope);
        var cells = db.Query<StationCellRow>(
            "SELECT ID, LinkIDList FROM cell WHERE InstanceID = @InstanceID AND StationSchemeID = @StationSchemeID",
            scope) ?? new List<StationCellRow>();
        var trains = input.Trains.ToDictionary(train => train.Id, StringComparer.Ordinal);
        var routes = input.Routes.ToDictionary(route => route.Id, StringComparer.Ordinal);
        var seenTrains = new HashSet<string>(StringComparer.Ordinal);
        foreach (var snapshot in snapshots)
        {
            if (!seenTrains.Add(snapshot.TrainID) || !trains.TryGetValue(snapshot.TrainID, out var train))
                throw Invalid(snapshot, "列车不存在或过程快照重复。");
            var validation = OperationProcessValidator.Validate(snapshot.Process, catalog);
            if (validation.Count > 0) throw Invalid(snapshot, string.Join("；", validation));
            if (snapshot.Process.Activities.Count == 0 || !ValidTime(snapshot.OriginSeconds))
                throw Invalid(snapshot, "活动为空或基准时刻无效。");
            var activities = snapshot.Process.Activities;
            var events = snapshot.Process.Events;
            var movements = train.Movements.ToDictionary(movement => movement.Id, StringComparer.Ordinal);
            if (snapshot.ActivityMovementMap.Count != activities.Count || movements.Count != activities.Count ||
                snapshot.ActivityMovementMap.Values.Distinct(StringComparer.Ordinal).Count() != activities.Count ||
                activities.Any(activity => !snapshot.ActivityMovementMap.TryGetValue(activity.Id, out var id) || !movements.ContainsKey(id)))
                throw Invalid(snapshot, "活动与实际移动的映射不完整或不唯一。");
            if (snapshot.EventTimes.Count != events.Count || events.Any(ev =>
                    !snapshot.EventTimes.TryGetValue(ev.Id, out var time) || !ValidTime(time)))
                throw Invalid(snapshot, "事件时刻映射不完整或超过七天范围。");
            OperationProcessPlanScheduler.ValidateSchedule(snapshot.Process, snapshot.EventTimes, snapshot.OriginSeconds);

            var resources = OperationProcessPlanScheduler.ResolveResources(snapshot.Process, catalog);
            train.ProcessConstraints = new StationCapacityProcessInput
            {
                OriginSeconds = snapshot.OriginSeconds,
                Events = events.Select(ev => new StationCapacityEventInput
                {
                    Id = ev.Id,
                    OriginalTimeSeconds = snapshot.EventTimes[ev.Id],
                    FixedTimeSeconds = ev.Time.HasValue ? snapshot.OriginSeconds + Seconds(ev.Time.Value) : null
                }).ToList(),
                Precedences = snapshot.Process.Precedences.Select(precedence => new StationCapacityPrecedenceInput
                {
                    LeadingEventId = precedence.LeadingEvent,
                    FollowingEventId = precedence.FollowingEvent,
                    IntervalSeconds = Seconds(precedence.Interval)
                }).ToList()
            };
            foreach (var ev in train.ProcessConstraints.Events)
                if (ev.FixedTimeSeconds is double fixedTime &&
                    (!ValidTime(fixedTime) || Math.Abs(snapshot.EventTimes[ev.Id] - fixedTime) > 0.000001))
                    throw Invalid(snapshot, $"固定事件 {ev.Id} 的实际时刻不符合原约束。");

            foreach (var activity in activities)
            {
                var movement = movements[snapshot.ActivityMovementMap[activity.Id]];
                movement.StartEventId = activity.StartEvent;
                movement.EndEventId = activity.EndEvent;
                movement.OriginalStartSeconds = snapshot.EventTimes[activity.StartEvent];
                movement.OriginalEndSeconds = snapshot.EventTimes[activity.EndEvent];
                movement.MinDurationSeconds = Seconds(activity.MinDuration);
                movement.MaxDurationSeconds = Seconds(activity.MaxDuration);
                movement.RequiredRouteTags.Clear();
                var elapsed = movement.OriginalEndSeconds - movement.OriginalStartSeconds;
                if (elapsed < movement.MinDurationSeconds - 0.000001 || elapsed > movement.MaxDurationSeconds + 0.000001)
                    throw Invalid(snapshot, $"活动 {activity.Id} 的实际时长不符合原约束。");
                if (!resources.TryGetValue(activity.Id, out var candidates))
                    throw Invalid(snapshot, $"活动 {activity.Id} 缺少资源候选。");
                if (activity.Type == "Dwelling")
                {
                    if (!snapshot.SelectedTrackIDs.TryGetValue(activity.Id, out var selectedTrack) ||
                        !candidates.TrackIDs.Contains(selectedTrack, StringComparer.Ordinal))
                        throw Invalid(snapshot, $"停留活动 {activity.Id} 的实际轨道不符合原约束。");
                    movement.CandidateRouteIds = candidates.TrackIDs.Select(trackId =>
                        AddDwellingRoute(snapshot, trackId, cells, routes, input)).ToList();
                    for (var index = 0; index < candidates.TrackIDs.Count; index++)
                        AddLocations(candidates.TrackIDs[index], movement.CandidateRouteIds[index]);
                }
                else
                {
                    movement.CandidateRouteIds = candidates.RouteIDs.ToList();
                    if (movement.CandidateRouteIds.Any(id => !routes.ContainsKey(id)))
                        throw Invalid(snapshot, $"活动 {activity.Id} 的备选进路不存在或没有轨道电路。");
                    foreach (var routeId in movement.CandidateRouteIds) AddLocations(routeId, routeId);
                }
                if (movement.CandidateRouteIds.Count == 0)
                    throw Invalid(snapshot, $"活动 {activity.Id} 没有满足全部约束的候选资源。");

                void AddLocations(string resourceId, string routeId)
                {
                    movement.StartLocationIdsByRoute[routeId] = OperationProcessResourceLocations.Get(
                        snapshot.Process, catalog, activity, resourceId, true);
                    movement.EndLocationIdsByRoute[routeId] = OperationProcessResourceLocations.Get(
                        snapshot.Process, catalog, activity, resourceId, false);
                    if (movement.StartLocationIdsByRoute[routeId].Count == 0 || movement.EndLocationIdsByRoute[routeId].Count == 0)
                        throw Invalid(snapshot, $"活动 {activity.Id} 的候选资源没有合法事件地点。");
                }
            }
            foreach (var ev in train.ProcessConstraints.Events)
            {
                HashSet<string>? possible = null;
                foreach (var movement in train.Movements)
                {
                    if (movement.StartEventId == ev.Id) Intersect(movement.StartLocationIdsByRoute);
                    if (movement.EndEventId == ev.Id) Intersect(movement.EndLocationIdsByRoute);
                }
                if (possible is { Count: 0 }) throw Invalid(snapshot, $"共享事件 {ev.Id} 的候选资源没有共同地点或锚。");
                ev.LocationIds = possible?.ToList() ?? new();

                void Intersect(Dictionary<string, List<string>> choices)
                {
                    var locations = choices.Values.SelectMany(value => value).ToHashSet(StringComparer.Ordinal);
                    if (possible is null) possible = locations;
                    else possible.IntersectWith(locations);
                }
            }
            foreach (var precedence in train.ProcessConstraints.Precedences)
                if (snapshot.EventTimes[precedence.FollowingEventId] - snapshot.EventTimes[precedence.LeadingEventId]
                    < precedence.IntervalSeconds - 0.000001)
                    throw Invalid(snapshot, "实际事件时刻不符合原次序间隔。");

            var lastTime = snapshot.EventTimes.Values.Max();
            input.HorizonSeconds = Math.Max(input.HorizonSeconds,
                Math.Min(MaximumHorizonSeconds, lastTime + Math.Max(0, input.Settings.RightShiftToleranceSeconds)));
        }
    }

    private static string AddDwellingRoute(TrainProcessSnapshot snapshot, string trackId,
        IReadOnlyList<StationCellRow> cells, IDictionary<string, StationCapacityRouteInput> routes,
        StationCapacitySolveInput input)
    {
        var routeId = "__process_dwelling__" + Convert.ToHexString(Encoding.UTF8.GetBytes(trackId));
        if (routes.TryGetValue(routeId, out var existing))
        {
            if (existing.TrackId != trackId) throw Invalid(snapshot, "停留资源 ID 与已有进路冲突。");
            return routeId;
        }
        if (routes.Keys.Any(id => string.Equals(id, routeId, StringComparison.OrdinalIgnoreCase)))
            throw Invalid(snapshot, "停留资源 ID 与已有进路大小写冲突。");
        var cellIds = cells.Where(cell => StationCapacityInputBuilder.ParseList(cell.LinkIDList)
                .Contains(trackId, StringComparer.Ordinal))
            .Select(cell => cell.ID).Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id!)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (cellIds.Count == 0) throw Invalid(snapshot, $"停留轨道 {trackId} 没有关联轨道电路，无法建立占用约束。");
        var route = new StationCapacityRouteInput { Id = routeId, TrackId = trackId, CellIds = cellIds };
        routes.Add(routeId, route);
        input.Routes.Add(route);
        foreach (var cellId in cellIds)
            input.RouteOccupations.Add(new StationCapacityRouteOccupationInput { RouteId = routeId, CellId = cellId });
        return routeId;
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
        new($"列车 {snapshot.TrainID} 的作业过程快照无效：{reason}");
}
