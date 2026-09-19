using System.Globalization;
using SwitchYard.Service.Models;

namespace SwitchYard.Service.Services;

public sealed class ProcessScheduledTrain
{
    public double OriginSeconds { get; set; }
    public Dictionary<string, double> EventTimes { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> SelectedRouteIDs { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> SelectedTrackIDs { get; set; } = new(StringComparer.Ordinal);
}

public sealed class ProcessActivityResourceCandidates
{
    public List<string> RouteIDs { get; set; } = new();
    public List<string> TrackIDs { get; set; } = new();
}

/// <summary>Distributes train origins using legacy slots, then schedules every event as early as all constraints allow.</summary>
public static class OperationProcessPlanScheduler
{
    private const double Epsilon = 0.0000001;
    public const double MaximumHorizonSeconds = 7 * 86400;

    public static List<ProcessScheduledTrain> Build(OperationProcessTemplate source, ProcessCatalog catalog,
        int trainCount, double startSeconds, double endSeconds)
    {
        if (trainCount is < 1 or > 1000 || (long)trainCount * source.Activities.Count > 20000)
            throw new ArgumentException("生成数量应为 1–1000，且本次生成的活动总数不能超过 20000。");
        if (source.Activities.Count == 0) throw new ArgumentException("作业过程没有活动。");
        if (!double.IsFinite(startSeconds) || !double.IsFinite(endSeconds) || startSeconds < 0 ||
            endSeconds <= startSeconds || endSeconds > MaximumHorizonSeconds)
            throw new ArgumentException("排布时间范围无效，最长支持从第 1 天起的 7 天。");
        var ordered = OrderedActivities(source);
        var resources = ResolveResources(source, catalog);
        var eventIndex = source.Events.Select((ev, index) => (ev.Id, Index: index + 2))
            .ToDictionary(item => item.Id, item => item.Index, StringComparer.Ordinal);
        var maxMinimum = source.Activities.Max(activity => Seconds(activity.MinDuration));
        // Retain the existing generator's minute-based formula for each train's first
        // slot. Only train origins use these targets; activities within a train do not.
        // Hard graph constraints, including the requested window, take precedence.
        var available = Math.Max(0, endSeconds - Math.Ceiling(maxMinimum / 60d) * 60 - startSeconds);
        var total = (long)trainCount * ordered.Count;
        var slot = total > 1 ? available / 60d / (total - 1) : 0;
        var result = new List<ProcessScheduledTrain>();
        long remainingWork = 100_000_000;
        for (var trainIndex = 0; trainIndex < trainCount; trainIndex++)
        {
            var graph = new DifferenceGraph(source.Events.Count + 2);
            for (var node = 1; node < source.Events.Count + 2; node++)
                graph.Range(node, startSeconds, endSeconds);
            foreach (var ev in source.Events)
            {
                var node = eventIndex[ev.Id];
                graph.Add(node, 1, 0); // origin <= every event
                if (ev.Time is double relativeTime)
                {
                    var offset = Seconds(relativeTime);
                    graph.Add(1, node, offset);
                    graph.Add(node, 1, -offset);
                }
            }
            foreach (var activity in source.Activities)
            {
                var start = eventIndex[activity.StartEvent];
                var end = eventIndex[activity.EndEvent];
                graph.Add(end, start, -Seconds(activity.MinDuration));
                graph.Add(start, end, Seconds(activity.MaxDuration));
            }
            foreach (var precedence in source.Precedences)
                graph.Add(eventIndex[precedence.FollowingEvent], eventIndex[precedence.LeadingEvent], -Seconds(precedence.Interval));

            var firstTarget = Target(trainIndex * ordered.Count);
            var origin = graph.PinNearest(1, firstTarget, ref remainingWork);
            // With the origin fixed, -d(event, zero) is each event's tightest lower
            // bound. These bounds form a feasible schedule together, so parallel and
            // isolated events are also earliest. Reverse propagation includes maximum
            // durations and fixed event times; no event needs an artificial slot delay.
            var distancesToZero = graph.Distances(reverse: true, ref remainingWork);
            var train = new ProcessScheduledTrain {
                OriginSeconds = origin,
                EventTimes = eventIndex.ToDictionary(item => item.Key, item => Clean(-distancesToZero[item.Value]), StringComparer.Ordinal)
            };
            SelectResources(source, catalog, ordered, resources, trainIndex, train);
            ValidateSchedule(source, train.EventTimes, origin, startSeconds, endSeconds);
            result.Add(train);
        }
        return result;

        double Target(int index) => startSeconds + Math.Round(index * slot) * 60;
    }

    private static void SelectResources(OperationProcessTemplate source, ProcessCatalog catalog,
        List<ProcessActivity> activities, Dictionary<string, ProcessActivityResourceCandidates> candidates,
        int trainIndex, ProcessScheduledTrain train)
    {
        var shared = activities.SelectMany(activity => new[] { activity.StartEvent, activity.EndEvent })
            .GroupBy(id => id, StringComparer.Ordinal).Where(group => group.Count() > 1)
            .Select(group => group.Key).ToHashSet(StringComparer.Ordinal);
        var linked = activities.Where(activity => shared.Contains(activity.StartEvent) || shared.Contains(activity.EndEvent)).ToList();
        foreach (var activity in activities)
        {
            var ids = activity.Type == "Dwelling" ? candidates[activity.Id].TrackIDs : candidates[activity.Id].RouteIDs;
            Set(activity, ids[trainIndex % ids.Count]);
        }
        var domains = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var cache = new Dictionary<(string Activity, string Resource, bool Start), HashSet<string>>();
        var tries = 0;
        if (!Search(0)) throw new ArgumentException("共享事件的备选进路、股道、地点或锚之间存在冲突，无法生成完整列车。");

        bool Search(int index)
        {
            if (index == linked.Count) return true;
            var activity = linked[index];
            var ids = activity.Type == "Dwelling" ? candidates[activity.Id].TrackIDs : candidates[activity.Id].RouteIDs;
            for (var option = 0; option < ids.Count; option++)
            {
                if (++tries > 100000) throw new ArgumentException("共享事件资源组合较复杂，请缩小备选范围后重试。");
                var id = ids[(trainIndex + option) % ids.Count];
                var previous = new Dictionary<string, HashSet<string>?>(StringComparer.Ordinal);
                var matches = true;
                foreach (var (eventID, start) in new[] { (activity.StartEvent, true), (activity.EndEvent, false) })
                {
                    if (!shared.Contains(eventID)) continue;
                    var key = (activity.Id, id, start);
                    if (!cache.TryGetValue(key, out var locations))
                        cache[key] = locations = OperationProcessResourceLocations.Get(source, catalog, activity, id, start).ToHashSet(StringComparer.Ordinal);
                    domains.TryGetValue(eventID, out var old);
                    previous[eventID] = old;
                    var intersection = new HashSet<string>(locations, StringComparer.Ordinal);
                    if (old is not null) intersection.IntersectWith(old);
                    if (intersection.Count == 0) { matches = false; break; }
                    domains[eventID] = intersection;
                }
                if (matches)
                {
                    Set(activity, id);
                    if (Search(index + 1)) return true;
                }
                foreach (var (eventID, old) in previous)
                    if (old is null) domains.Remove(eventID); else domains[eventID] = old;
            }
            return false;
        }
        void Set(ProcessActivity activity, string id)
        {
            if (activity.Type == "Dwelling") train.SelectedTrackIDs[activity.Id] = id;
            else train.SelectedRouteIDs[activity.Id] = id;
        }
    }

    public static List<ProcessActivity> OrderedActivities(OperationProcessTemplate source)
    {
        var indices = source.Events.Select((ev, index) => (ev.Id, index)).ToDictionary(item => item.Id, item => item.index, StringComparer.Ordinal);
        var edges = indices.Keys.ToDictionary(id => id, _ => new HashSet<string>(StringComparer.Ordinal), StringComparer.Ordinal);
        var incoming = indices.Keys.ToDictionary(id => id, _ => 0, StringComparer.Ordinal);
        foreach (var (from, to) in source.Activities.Select(a => (a.StartEvent, a.EndEvent))
                     .Concat(source.Precedences.Select(p => (p.LeadingEvent, p.FollowingEvent))))
            if (edges[from].Add(to)) incoming[to]++;
        var ready = new PriorityQueue<string, int>();
        foreach (var id in indices.Keys) if (incoming[id] == 0) ready.Enqueue(id, indices[id]);
        var rank = new Dictionary<string, int>(StringComparer.Ordinal);
        while (ready.TryDequeue(out var id, out _))
        {
            rank[id] = rank.Count;
            foreach (var next in edges[id]) if (--incoming[next] == 0) ready.Enqueue(next, indices[next]);
        }
        if (rank.Count != indices.Count) throw new ArgumentException("作业过程包含循环依赖。");
        return source.Activities.Select((activity, index) => (activity, index))
            .OrderBy(item => rank[item.activity.StartEvent]).ThenBy(item => item.index).Select(item => item.activity).ToList();
    }

    public static Dictionary<string, ProcessActivityResourceCandidates> ResolveResources(OperationProcessTemplate source, ProcessCatalog catalog)
    {
        var routes = catalog.Routes.ToDictionary(route => route.Id, StringComparer.Ordinal);
        var tracks = catalog.Tracks.ToDictionary(track => track.Id, StringComparer.Ordinal);
        var events = source.Events.ToDictionary(ev => ev.Id, StringComparer.Ordinal);
        var anchors = source.Anchors.ToDictionary(anchor => anchor.Id, StringComparer.Ordinal);
        var bindings = source.RouteAnchors.ToDictionary(binding => binding.RouteID, StringComparer.Ordinal);
        var result = new Dictionary<string, ProcessActivityResourceCandidates>(StringComparer.Ordinal);
        foreach (var activity in source.Activities)
        {
            var candidate = new ProcessActivityResourceCandidates();
            if (activity.Type == "Dwelling")
            {
                candidate.TrackIDs = activity.TrackList.Where(id => tracks.TryGetValue(id, out var track) &&
                    !string.IsNullOrWhiteSpace(track.Name) && (activity.SelectedTrack is null || activity.SelectedTrack == id) &&
                    TrackMatchesEvent(track, events[activity.StartEvent]) && TrackMatchesEvent(track, events[activity.EndEvent])).ToList();
                if (candidate.TrackIDs.Count == 0) throw new ArgumentException($"停留活动“{activity.Name}”没有满足地点和锚约束的备选股道，请先完善作业过程。");
            }
            else
            {
                candidate.RouteIDs = activity.RouteList.Where(id => routes.TryGetValue(id, out var route) && route.Type == activity.Type &&
                    (activity.SelectedRoute is null || activity.SelectedRoute == id) &&
                    RouteEndpointMatches(events[activity.StartEvent], route.StartNodeID, bindings.GetValueOrDefault(id)?.StartAnchor) &&
                    RouteEndpointMatches(events[activity.EndEvent], route.EndNodeID, bindings.GetValueOrDefault(id)?.EndAnchor)).ToList();
                if (candidate.RouteIDs.Count == 0) throw new ArgumentException($"活动“{activity.Name}”没有满足地点和锚约束的备选进路，请先完善作业过程。");
            }
            result[activity.Id] = candidate;
        }
        return result;

        bool RouteEndpointMatches(ProcessEvent ev, string? node, string? boundAnchor)
        {
            if (ev.NodeID is not null && ev.NodeID != node) return false;
            if (ev.NodeList.Count > 0 && (node is null || !ev.NodeList.Contains(node, StringComparer.Ordinal))) return false;
            var allowed = ev.SelectedAnchor is not null ? new[] { ev.SelectedAnchor } : ev.AnchorList.ToArray();
            if (boundAnchor is not null && allowed.Length > 0 && !allowed.Contains(boundAnchor, StringComparer.Ordinal)) return false;
            if (boundAnchor is not null && !AnchorAtNode(boundAnchor, node)) return false;
            return allowed.Length == 0 || allowed.Any(id => AnchorAtNode(id, node));
        }
        bool AnchorAtNode(string anchorID, string? node) => node is not null && anchors.TryGetValue(anchorID, out var anchor) &&
            tracks.TryGetValue(anchor.TrackID, out var track) && (track.FromNodeID == node || track.ToNodeID == node);
        bool TrackMatchesEvent(ProcessCatalogTrack track, ProcessEvent ev)
        {
            if (ev.NodeID is not null && ev.NodeID != track.FromNodeID && ev.NodeID != track.ToNodeID) return false;
            if (ev.NodeList.Count > 0 && !ev.NodeList.Contains(track.FromNodeID) && !ev.NodeList.Contains(track.ToNodeID)) return false;
            var allowed = ev.SelectedAnchor is not null ? new[] { ev.SelectedAnchor } : ev.AnchorList.ToArray();
            return allowed.Length == 0 || allowed.Any(id => anchors.TryGetValue(id, out var anchor) && anchor.TrackID == track.Id);
        }
    }

    public static void ValidateSchedule(OperationProcessTemplate source, IReadOnlyDictionary<string, double> times,
        double origin, double start = 0, double end = MaximumHorizonSeconds)
    {
        foreach (var ev in source.Events)
        {
            if (!times.TryGetValue(ev.Id, out var time) || !double.IsFinite(time) || time < start - Epsilon || time > end + Epsilon || time < origin - Epsilon)
                throw new ArgumentException("事件时刻缺失或超出排布时间范围。");
            if (ev.Time is double fixedTime && Math.Abs(time - origin - Seconds(fixedTime)) > Epsilon)
                throw new ArgumentException($"事件“{ev.Name}”不满足原过程的固定相对时刻。");
        }
        foreach (var activity in source.Activities)
        {
            var duration = times[activity.EndEvent] - times[activity.StartEvent];
            if (duration < Seconds(activity.MinDuration) - Epsilon || duration > Seconds(activity.MaxDuration) + Epsilon)
                throw new ArgumentException($"活动“{activity.Name}”不满足最小/最大持续时间约束。");
        }
        foreach (var precedence in source.Precedences)
            if (times[precedence.FollowingEvent] - times[precedence.LeadingEvent] < Seconds(precedence.Interval) - Epsilon)
                throw new ArgumentException($"次序“{precedence.Id}”不满足最小间隔约束。");
    }

    public static double Seconds(double minutes)
    {
        if (!double.IsFinite(minutes) || minutes < 0 || minutes > int.MaxValue / 60d)
            throw new ArgumentException("作业过程时长或事件时刻超出支持范围。");
        return (double)((decimal)minutes * 60m);
    }

    public static string FormatTime(double seconds)
    {
        var time = TimeSpan.FromSeconds(Clean(seconds));
        var clock = $"{time.Hours:00}:{time.Minutes:00}:{time.Seconds:00}";
        var fraction = (time.Ticks % TimeSpan.TicksPerSecond).ToString("D7", CultureInfo.InvariantCulture).TrimEnd('0');
        if (fraction.Length > 0) clock += "." + fraction;
        return time.Days > 0 ? $"D+{time.Days} {clock}" : clock;
    }

    private static double Clean(double value) => Math.Round(value, 7);

    private sealed class DifferenceGraph
    {
        private readonly List<(int From, int To, double Bound)> edges = new();
        private readonly int count;
        public DifferenceGraph(int count) => this.count = count;
        public void Add(int from, int to, double bound) => edges.Add((from, to, bound));
        public void Range(int node, double lower, double upper) { Add(0, node, upper); Add(node, 0, -lower); }
        public double PinNearest(int node, double target, ref long remainingWork)
        {
            var upper = Distances(false, ref remainingWork)[node];
            var lower = -Distances(true, ref remainingWork)[node];
            if (lower > upper + Epsilon) throw Infeasible();
            var value = Math.Max(lower, Math.Min(upper, target));
            Range(node, value, value);
            return Clean(value);
        }
        public double[] Distances(bool reverse, ref long remainingWork)
        {
            var adjacency = Enumerable.Range(0, count).Select(_ => new List<(int To, double Bound)>()).ToArray();
            foreach (var edge in edges)
                adjacency[reverse ? edge.To : edge.From].Add((reverse ? edge.From : edge.To, edge.Bound));
            var distance = Enumerable.Repeat(double.PositiveInfinity, count).ToArray();
            var depth = new int[count];
            var queued = new bool[count];
            var pending = new Queue<int>();
            distance[0] = 0;
            queued[0] = true;
            pending.Enqueue(0);
            while (pending.TryDequeue(out var from))
            {
                queued[from] = false;
                foreach (var (to, bound) in adjacency[from])
                {
                    if (--remainingWork < 0) throw new ArgumentException("本次过程约束较复杂，请减少一次生成数量后重试。");
                    var next = distance[from] + bound;
                    if (next >= distance[to] - Epsilon) continue;
                    distance[to] = next;
                    if ((depth[to] = depth[from] + 1) >= count) throw Infeasible();
                    if (!queued[to]) { queued[to] = true; pending.Enqueue(to); }
                }
            }
            return distance;
        }
        private static ArgumentException Infeasible() => new("无法在指定时间范围内同时满足作业过程的时长、事件时刻和次序约束；请扩大时间范围或检查过程。本次未生成列车。");
    }
}
