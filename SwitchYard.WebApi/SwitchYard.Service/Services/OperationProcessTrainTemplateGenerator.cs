using System.Globalization;
using SwitchYard.Capacity;
using SwitchYard.Service.Models;

namespace SwitchYard.Service.Services;

/// <summary>Projects a validated saved process into the existing, less expressive template fields.</summary>
internal static class OperationProcessTrainTemplateGenerator
{
    internal sealed record Projection(TrainTemplateRow TrainTemplate, List<MovementTemplateRow> Movements, List<string> Warnings);

    internal static Projection Build(OperationProcessTemplate source, ProcessCatalog catalog, string trainTemplateID, Func<string> nextMovementID)
    {
        var warnings = new List<string> {
            "本次仅生成列车模板，保留活动名称、备选进路、最小持续时间及作业顺序。最大持续时间、事件时刻/地点、次序间隔、锚和选定资源等约束仍保留在原作业过程中，未写入列车模板。"
        };
        var train = new TrainTemplateRow {
            InstanceID = source.InstanceID, StationSchemeID = source.StationSchemeID, OperationPlanID = SchemeTemplateStore.PlanKey,
            TrainTemplateID = trainTemplateID, Name = LegacyName(source.Name, "列车模板", warnings), Type = "", Number = 1, IsFixedOperation = 0
        };
        var (ordered, edges) = OrderActivities(source);
        if (ordered.Zip(ordered.Skip(1)).Any(pair => !CanReach(pair.First.EndEvent, pair.Second.StartEvent, edges)))
            warnings.Add("源过程包含并行、重叠或未完全排序的活动。已按事件依赖和原事件列表顺序生成稳定的作业顺序；现有列车模板的线性顺序及连续作业约束不能完整表达这些关系，请在使用前核对。");
        if (source.Precedences.Any(p => p.Interval > 0 ||
                !source.Activities.Any(a => a.EndEvent == p.LeadingEvent) ||
                !source.Activities.Any(a => a.StartEvent == p.FollowingEvent)))
            warnings.Add("源过程含有带间隔或非“前活动结束→后活动开始”的次序；这些关系只用于稳定排序，其时间或重叠含义仍需查阅原作业过程。");

        var movements = new List<MovementTemplateRow>();
        foreach (var activity in ordered)
        {
            var routeIDs = activity.Type == "Dwelling"
                ? DwellingRoutes(activity, catalog, warnings)
                : activity.RouteList.ToList();
            if (routeIDs.Count == 0)
                warnings.Add($"活动“{activity.Name}”未找到可用备选进路，已保留作业行且进路列表为空。请先补充进路；现有求解器可能将空列表视为全站进路候选，不能据此直接求解。");
            // Legacy consumers split these delimiters rather than decoding JSON; reject an
            // unrepresentable identifier instead of silently changing the route candidates.
            if (routeIDs.Any(id => id.IndexOfAny(new[] { ',', ';', '，', '；', '\r', '\n', '\t', ' ' }) >= 0) ||
                routeIDs.Distinct(StringComparer.OrdinalIgnoreCase).Count() != routeIDs.Count)
                throw new ArgumentException($"活动“{activity.Name}”的进路 id 含旧列车模板分隔符或仅大小写不同，无法完整保存全部候选，请先调整进路 id。");

            movements.Add(new MovementTemplateRow {
                InstanceID = source.InstanceID, StationSchemeID = source.StationSchemeID, OperationPlanID = SchemeTemplateStore.PlanKey,
                TrainTemplateID = trainTemplateID, MovementID = nextMovementID(), Name = LegacyName(activity.Name, $"活动 {activity.Id}", warnings),
                RouteIDList = string.Join(",", routeIDs), MinDuration = DurationSeconds(activity), SortOrder = movements.Count
            });
        }
        return new Projection(train, movements, warnings);
    }

    private static List<string> DwellingRoutes(ProcessActivity activity, ProcessCatalog catalog, List<string> warnings)
    {
        var routes = new List<string>();
        var missingTracks = new List<string>();
        foreach (var trackID in activity.TrackList)
        {
            var matches = catalog.Routes.Where(route =>
                (string.Equals(route.Type, "Dwelling", StringComparison.OrdinalIgnoreCase) || route.Type is "停留" or "停留进路") &&
                route.TrackIDs.Count == 1 && route.TrackIDs[0] == trackID).Select(route => route.Id).ToList();
            if (matches.Count == 0)
                missingTracks.Add(catalog.Tracks.First(track => track.Id == trackID).Name);
            routes.AddRange(matches);
        }
        if (missingTracks.Count > 0)
            warnings.Add($"停留活动“{activity.Name}”的备选轨道（{string.Join("、", missingTracks)}）没有仅占用该轨道的停留进路，无法映射为备选进路；这些轨道仍保留在原作业过程中。");
        return routes.Distinct(StringComparer.Ordinal).ToList();
    }

    private static int DurationSeconds(ProcessActivity activity)
    {
        // Convert before multiplication so e.g. 1.1 minutes becomes 66, not 67 seconds
        // due to binary floating point's 66.00000000000001 intermediate result.
        if (!double.IsFinite(activity.MinDuration) || activity.MinDuration < 0 || activity.MinDuration > int.MaxValue / 60d)
            throw new ArgumentException($"活动“{activity.Name}”的最小持续时间换算为秒后超出整数范围。");
        var seconds = decimal.Ceiling((decimal)activity.MinDuration * 60m);
        if (seconds > int.MaxValue)
            throw new ArgumentException($"活动“{activity.Name}”的最小持续时间换算为秒后超出整数范围。");
        return (int)seconds;
    }

    private static string LegacyName(string name, string label, List<string> warnings)
    {
        if (name.Length <= 50) return name;
        // Keep a complete Unicode text element at the old 50 UTF-16-unit boundary.
        var boundary = StringInfo.ParseCombiningCharacters(name).TakeWhile(index => index <= 50).LastOrDefault();
        if (boundary == 0) throw new ArgumentException($"{label}名称的首个 Unicode 字符超过旧名称字段长度，无法生成。");
        var shortened = name[..boundary];
        warnings.Add($"{label}名称超过旧模板的 50 字符限制，已截短为“{shortened}”；原名称“{name}”保留在作业过程中。");
        return shortened;
    }

    private static (List<ProcessActivity> Activities, Dictionary<string, HashSet<string>> Edges) OrderActivities(OperationProcessTemplate source)
    {
        var eventIndex = source.Events.Select((ev, index) => (ev.Id, index)).ToDictionary(x => x.Id, x => x.index, StringComparer.Ordinal);
        var edges = eventIndex.Keys.ToDictionary(id => id, _ => new HashSet<string>(StringComparer.Ordinal), StringComparer.Ordinal);
        var incoming = eventIndex.Keys.ToDictionary(id => id, _ => 0, StringComparer.Ordinal);
        foreach (var (from, to) in source.Activities.Select(a => (a.StartEvent, a.EndEvent))
                     .Concat(source.Precedences.Select(p => (p.LeadingEvent, p.FollowingEvent))))
            if (edges[from].Add(to)) incoming[to]++;
        var ready = new PriorityQueue<string, int>();
        foreach (var id in eventIndex.Keys) if (incoming[id] == 0) ready.Enqueue(id, eventIndex[id]);
        var rank = new Dictionary<string, int>(StringComparer.Ordinal);
        while (ready.TryDequeue(out var id, out _))
        {
            rank[id] = rank.Count;
            foreach (var next in edges[id]) if (--incoming[next] == 0) ready.Enqueue(next, eventIndex[next]);
        }
        if (rank.Count != source.Events.Count) throw new ArgumentException("作业过程包含循环依赖，无法生成列车模板。");
        return (source.Activities.Select((activity, index) => (activity, index))
            .OrderBy(x => rank[x.activity.StartEvent]).ThenBy(x => x.index).Select(x => x.activity).ToList(), edges);
    }

    private static bool CanReach(string from, string to, Dictionary<string, HashSet<string>> edges)
    {
        var pending = new Stack<string>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        pending.Push(from);
        while (pending.TryPop(out var id))
        {
            if (id == to) return true;
            if (!visited.Add(id)) continue;
            foreach (var next in edges[id]) pending.Push(next);
        }
        return false;
    }
}
