using System.Text.Json;
using System.Text.RegularExpressions;

namespace SwitchYard.Capacity;

/// <summary>Reconstructs solver constraints from a process reference and its train's execution times.</summary>
public static class StationCapacityProcessReferenceResolver
{
    private const double MaximumTimeSeconds = 7 * 86_400;

    public static void Resolve(StationCapacitySolveInput input)
    {
        if (input.Trains is null) throw new InvalidOperationException("求解输入缺少列车数据。");
        Dictionary<string, StationCapacityOperationProcessInput>? processes = null;
        foreach (var train in input.Trains)
        {
            if (train is null) throw new InvalidOperationException("求解输入包含空列车。");
            if (string.IsNullOrWhiteSpace(train.OperationProcessId))
            {
                if (train.ProcessOriginSeconds.HasValue ||
                    train.Movements?.Any(movement => movement?.ActivityId is not null) == true)
                    throw Invalid(train, "存在过程执行数据，但未指定作业过程编号。");
                // Saved jobs may still carry the former inline constraints.
                continue;
            }

            processes ??= IndexProcesses(input.OperationProcesses);
            if (!processes.TryGetValue(train.OperationProcessId, out var source))
                throw Invalid(train, $"引用的作业过程 {train.OperationProcessId} 不存在。");
            ResolveTrain(input, train, source);
        }
    }

    private static Dictionary<string, StationCapacityOperationProcessInput> IndexProcesses(
        List<StationCapacityOperationProcessInput>? processes)
    {
        if (processes is null) throw new InvalidOperationException("求解输入缺少作业过程定义。");
        var result = new Dictionary<string, StationCapacityOperationProcessInput>(StringComparer.Ordinal);
        foreach (var process in processes)
            if (process is null || string.IsNullOrWhiteSpace(process.Id) || !result.TryAdd(process.Id, process))
                throw new InvalidOperationException("作业过程定义为空、编号为空或编号重复。");
        return result;
    }

    private static void ResolveTrain(StationCapacitySolveInput input, StationCapacityTrainInput train,
        StationCapacityOperationProcessInput source)
    {
        if (train.ProcessOriginSeconds is not double origin || !ValidTime(origin))
            throw Invalid(train, "缺少有效的过程基准时刻。");
        if (source.Activities is null || source.Events is null || source.Precedences is null || source.Anchors is null ||
            source.Activities.Count == 0 || source.Events.Count == 0 || train.Movements is null)
            throw Invalid(train, "引用的作业过程缺少活动、事件或约束集合。");

        var events = new Dictionary<string, StationCapacityProcessEventInput>(StringComparer.Ordinal);
        foreach (var ev in source.Events)
            if (ev is null || string.IsNullOrWhiteSpace(ev.Id) || !events.TryAdd(ev.Id, ev))
                throw Invalid(train, "作业过程包含空事件、无编号事件或重复事件。");
        var movements = new Dictionary<string, StationCapacityMovementInput>(StringComparer.Ordinal);
        var originalTimes = new Dictionary<string, double>(StringComparer.Ordinal);
        var movementIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var movement in train.Movements)
            if (movement is null || string.IsNullOrWhiteSpace(movement.Id) || !movementIds.Add(movement.Id) ||
                string.IsNullOrWhiteSpace(movement.ActivityId) || !movements.TryAdd(movement.ActivityId, movement))
                throw Invalid(train, "移动缺少有效的活动映射，或移动、活动映射重复。");
        var activityIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var activity in source.Activities)
        {
            if (activity is null || string.IsNullOrWhiteSpace(activity.Id) || !activityIds.Add(activity.Id) ||
                !movements.TryGetValue(activity.Id, out var movement))
                throw Invalid(train, "活动与移动必须一一对应。");
            if (activity.StartEvent is null || activity.EndEvent is null ||
                activity.StartEvent == activity.EndEvent || !events.ContainsKey(activity.StartEvent) || !events.ContainsKey(activity.EndEvent) ||
                movement.StartEventId != activity.StartEvent || movement.EndEventId != activity.EndEvent)
                throw Invalid(train, $"活动 {activity.Id} 的起止事件与移动不一致。");
            AddOriginalTime(activity.StartEvent, movement.OriginalStartSeconds);
            AddOriginalTime(activity.EndEvent, movement.OriginalEndSeconds);
            var minimum = Seconds(activity.MinDuration, train);
            var maximum = Seconds(activity.MaxDuration, train);
            if (minimum < 0 || maximum < minimum ||
                Math.Abs(movement.MinDurationSeconds - minimum) > 0.000001 ||
                Math.Abs(movement.MaxDurationSeconds - maximum) > 0.000001 ||
                !double.IsFinite(movement.MinDurationSeconds) || !double.IsFinite(movement.MaxDurationSeconds))
                throw Invalid(train, $"活动 {activity.Id} 的持续时间与引用的作业过程不一致。");
            if (movement.CandidateRouteIds is null || movement.CandidateRouteIds.Count == 0 ||
                movement.StartLocationsByRoute is null || movement.EndLocationsByRoute is null ||
                movement.CandidateRouteIds.Any(id => string.IsNullOrWhiteSpace(id) ||
                    !movement.StartLocationsByRoute.TryGetValue(id, out var starts) || starts is null || starts.Count == 0 || starts.Any(location => location is null) ||
                    !movement.EndLocationsByRoute.TryGetValue(id, out var ends) || ends is null || ends.Count == 0 || ends.Any(location => location is null)))
                throw Invalid(train, $"活动 {activity.Id} 缺少候选进路或起止地点。");
        }
        if (activityIds.Count != movements.Count)
            throw Invalid(train, "活动与移动必须一一对应。");

        void AddOriginalTime(string eventId, double time)
        {
            if (!ValidTime(time) || time < origin)
                throw Invalid(train, $"事件 {eventId} 的移动原始时刻无效或早于基准时刻。");
            if (originalTimes.TryGetValue(eventId, out var existing) && existing != time)
                throw Invalid(train, $"共享事件 {eventId} 在不同移动中的原始时刻不一致。");
            originalTimes[eventId] = time;
        }

        var process = new StationCapacityProcessInput { OriginSeconds = origin };
        var anchors = new Dictionary<string, StationCapacityProcessAnchorInput>(StringComparer.Ordinal);
        foreach (var anchor in source.Anchors)
        {
            if (anchor is null || string.IsNullOrWhiteSpace(anchor.Id) || !anchors.TryAdd(anchor.Id, anchor) ||
                anchor.RouteIds is null || anchor.RouteIds.Count == 0 || anchor.RouteIds.Any(string.IsNullOrWhiteSpace) ||
                anchor.RouteIds.Distinct(StringComparer.Ordinal).Count() != anchor.RouteIds.Count)
                throw Invalid(train, "作业过程的锚编号或候选进路无效。");
            string? movementId = null;
            if (anchor.DwellingActivityId is not null)
            {
                if (!movements.TryGetValue(anchor.DwellingActivityId, out var owner))
                    throw Invalid(train, $"锚 {anchor.Id} 引用的停留活动不存在。");
                movementId = owner.Id;
            }
            process.Anchors.Add(new StationCapacityAnchorInput
            {
                Id = anchor.Id, RouteIds = anchor.RouteIds.ToList(), MovementId = movementId
            });
        }

        var standalone = StandaloneEvents(input, source, train);
        foreach (var ev in source.Events)
        {
            double? fixedTime = ev.Time is double time ? origin + Seconds(time, train) : null;
            if (fixedTime is double fixedSeconds && (!ValidTime(fixedSeconds) || fixedSeconds < origin))
                throw Invalid(train, $"事件 {ev.Id} 的固定时刻无效。");
            var resolved = new StationCapacityEventInput
            {
                Id = ev.Id,
                OriginalTimeSeconds = originalTimes.TryGetValue(ev.Id, out var originalTime) ? originalTime : fixedTime,
                FixedTimeSeconds = fixedTime
            };

            HashSet<string>? possible = standalone.GetValueOrDefault(ev.Id);
            var endpointCount = train.Movements.Sum(movement =>
                (movement.StartEventId == ev.Id ? 1 : 0) + (movement.EndEventId == ev.Id ? 1 : 0));
            foreach (var movement in train.Movements)
            {
                if (movement.StartEventId == ev.Id) Intersect(movement.StartLocationsByRoute);
                if (movement.EndEventId == ev.Id) Intersect(movement.EndLocationsByRoute);
            }
            if (possible is { Count: 0 })
                throw Invalid(train, $"事件 {ev.Id} 的候选资源没有共同地点或锚。");
            resolved.LocationIds = possible?.ToList() ?? new();
            foreach (var location in resolved.LocationIds)
            {
                var decoded = StationCapacityLocationInput.FromLegacyToken(location);
                if (decoded.AnchorId is not { } anchorId || decoded.DwellingRouteId is not { } routeId) continue;
                if (!anchors.TryGetValue(anchorId, out var anchor) || !anchor.RouteIds.Contains(routeId, StringComparer.Ordinal))
                    throw Invalid(train, $"事件 {ev.Id} 包含不存在的锚或锚进路。");
                resolved.AnchorChoicesByLocation[location] = new() { AnchorId = anchorId, RouteId = routeId };
            }
            process.Events.Add(resolved);

            void Intersect(Dictionary<string, List<StationCapacityLocationInput>> choices)
            {
                if (choices.Values.Any(values => values is null || values.Any(location => location is null)))
                    throw Invalid(train, $"事件 {ev.Id} 的候选地点为空。");
                var locations = choices.Values.SelectMany(values => values)
                    .Where(location => endpointCount <= 1 || location.NodeId is not null)
                    .Select(location => location.ToSolverKey())
                    .ToHashSet(StringComparer.Ordinal);
                if (possible is null) possible = locations;
                else possible.IntersectWith(locations);
            }
        }
        foreach (var precedence in source.Precedences)
        {
            if (precedence is null || precedence.LeadingEvent is null || precedence.FollowingEvent is null ||
                precedence.LeadingEvent == precedence.FollowingEvent || !events.ContainsKey(precedence.LeadingEvent) ||
                !events.ContainsKey(precedence.FollowingEvent))
                throw Invalid(train, "作业过程次序引用了无效事件。");
            var interval = Seconds(precedence.Interval, train);
            if (interval < 0) throw Invalid(train, "作业过程次序间隔不能为负数。");
            process.Precedences.Add(new StationCapacityPrecedenceInput
            {
                LeadingEventId = precedence.LeadingEvent, FollowingEventId = precedence.FollowingEvent,
                IntervalSeconds = interval, IsExact = precedence.IsExact
            });
        }
        train.ProcessConstraints = process;
    }

    private static Dictionary<string, HashSet<string>> StandaloneEvents(StationCapacitySolveInput input,
        StationCapacityOperationProcessInput source, StationCapacityTrainInput train)
    {
        var used = source.Activities.SelectMany(activity => new[] { activity.StartEvent, activity.EndEvent })
            .ToHashSet(StringComparer.Ordinal);
        var result = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var ev in source.Events.Where(ev => !used.Contains(ev.Id)))
        {
            if (ev.AnchorList is null) throw Invalid(train, $"事件 {ev.Id} 缺少锚列表。");
            var allowed = ev.SelectedAnchor is not null ? new[] { ev.SelectedAnchor } : ev.AnchorList.ToArray();
            if (allowed.Length == 0) continue;
            if (input.StationLayout?.Routes is null) throw Invalid(train, "独立事件的锚缺少布局进路数据。");
            var locations = new HashSet<string>(StringComparer.Ordinal);
            foreach (var anchor in source.Anchors.Where(anchor => allowed.Contains(anchor.Id, StringComparer.Ordinal)))
                foreach (var route in input.StationLayout.Routes.Where(route => route is not null &&
                    route.Id is not null && anchor.RouteIds.Contains(route.Id, StringComparer.Ordinal) && IsDwelling(route.Type)))
                {
                    var nodes = ParseNodeList(route.NodeList);
                    var start = NullIfBlank(route.StartNodeId) ?? nodes.FirstOrDefault();
                    var end = NullIfBlank(route.EndNodeId) ?? nodes.LastOrDefault();
                    foreach (var node in new[] { start, end })
                        if (node is not null && (ev.NodeId is null || ev.NodeId == node))
                            locations.Add(JsonSerializer.Serialize(new[] { node, anchor.Id, route.Id }));
                }
            result[ev.Id] = locations;
        }
        return result;
    }

    private static List<string> ParseNodeList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return new();
        var text = value.Trim();
        if (text.StartsWith('['))
        {
            try
            {
                using var document = JsonDocument.Parse(text);
                return document.RootElement.EnumerateArray().Select(item => item.ToString().Trim())
                    .Where(item => item.Length > 0).Distinct(StringComparer.Ordinal).ToList();
            }
            catch (JsonException) { }
        }
        return Regex.Split(text.Trim('[', ']'), @"(?:\s*->\s*)|[\s,，;；]+")
            .Select(item => item.Trim('"', '\'')).Where(item => item.Length > 0).Distinct(StringComparer.Ordinal).ToList();
    }

    private static bool IsDwelling(string? type) => type?.Trim().ToLowerInvariant() is "dwelling" or "停留" or "停留进路";
    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static bool ValidTime(double value) => double.IsFinite(value) && value >= 0 && value <= MaximumTimeSeconds;

    private static double Seconds(double minutes, StationCapacityTrainInput train)
    {
        var seconds = Math.Abs(minutes) <= (double)decimal.MaxValue / 60
            ? (double)((decimal)minutes * 60m) : minutes * 60d;
        if (!double.IsFinite(seconds)) throw Invalid(train, "作业过程时间超出可求解范围。");
        return seconds;
    }

    private static InvalidOperationException Invalid(StationCapacityTrainInput train, string reason) =>
        new($"列车 {train.Id} 的作业过程引用无效：{reason}");
}
