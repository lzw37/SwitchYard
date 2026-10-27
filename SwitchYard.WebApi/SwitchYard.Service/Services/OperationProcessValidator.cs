using SwitchYard.Service.Models;

namespace SwitchYard.Service.Services;

/// <summary>Separates editable document structure from the complete graph required for execution.</summary>
public static class OperationProcessValidator
{
    private static readonly HashSet<string> Types = new(StringComparer.Ordinal)
        { "Arrival", "Departure", "Shunting", "Locomotive", "Dwelling" };

    /// <summary>Drafts may have unfinished names, resources, references and constraints.</summary>
    public static List<string> ValidateForStorage(OperationProcessTemplate template)
    {
        var errors = new List<string>();
        if (!ValidID(template.Id)) errors.Add("模板 id 必须为 1–100 个字符，且不能有首尾空白。");
        CheckText(template.Name, 200, "模板名称");
        if (template.Description is null || template.Description.Length > 4000)
            errors.Add("模板说明不能为 null，且最多 4000 个字符。");
        if (template.Activities is null || template.Events is null || template.Precedences is null ||
            template.Anchors is null || template.RouteAnchors is null)
        {
            errors.Add("activities、events、precedences、anchors 和 routeAnchors 必须为数组。");
            return errors;
        }
        if (template.Activities.Count > 2000 || template.Events.Count > 4000 ||
            template.Precedences.Count > 10000 || template.Anchors.Count > 4000 || template.RouteAnchors.Count > 4000)
        {
            errors.Add("模板对象数量超出限制（活动 2000、事件/锚/进路锚配置各 4000、次序 10000）。");
            return errors;
        }
        if (template.Activities.Any(x => x is null) || template.Events.Any(x => x is null) ||
            template.Precedences.Any(x => x is null) || template.Anchors.Any(x => x is null) ||
            template.RouteAnchors.Any(x => x is null))
        {
            errors.Add("对象数组不能包含 null。");
            return errors;
        }
        Index(template.Activities, x => x.Id, "活动", errors);
        Index(template.Events, x => x.Id, "事件", errors);
        Index(template.Precedences, x => x.Id, "次序", errors);
        Index(template.Anchors, x => x.Id, "锚", errors);
        Index(template.RouteAnchors, x => x.RouteID, "进路锚配置", errors);
        foreach (var activity in template.Activities)
        {
            CheckText(activity.Name, 200, $"活动 {activity.Id} 名称");
            if (activity.Type is null || activity.StartEvent is null || activity.EndEvent is null)
                errors.Add($"活动 {activity.Id} 的类型与事件引用必须为字符串。");
            CheckList(activity.RouteList, $"活动 {activity.Id} 的备选进路");
            if (!double.IsFinite(activity.MinDuration) || !double.IsFinite(activity.MaxDuration) ||
                !double.IsFinite(activity.X) || !double.IsFinite(activity.Y))
                errors.Add($"活动 {activity.Id} 的持续时间和坐标必须为有限数值。");
        }
        foreach (var ev in template.Events)
        {
            CheckText(ev.Name, 200, $"事件 {ev.Id} 名称");
            CheckList(ev.AnchorList, $"事件 {ev.Id} 的备选锚");
            if (ev.Time is double time && !double.IsFinite(time)) errors.Add($"事件 {ev.Id} 时刻必须是有限数值或 null。");
        }
        foreach (var anchor in template.Anchors)
        {
            CheckText(anchor.Name, 200, $"锚 {anchor.Id} 名称");
            CheckList(anchor.RouteIDs, $"锚 {anchor.Id} 的停留进路");
        }
        foreach (var precedence in template.Precedences)
        {
            if (precedence.LeadingEvent is null || precedence.FollowingEvent is null)
                errors.Add($"次序 {precedence.Id} 的事件引用必须为字符串。");
            if (!double.IsFinite(precedence.Interval)) errors.Add($"次序 {precedence.Id} 间隔必须为有限数值。");
        }
        return errors;

        void CheckText(string? value, int max, string label)
        {
            if (value is null || value.Length > max) errors.Add($"{label}必须为字符串，且最多 {max} 个字符。");
        }
        void CheckList(List<string>? values, string label)
        {
            if (values is null || values.Any(value => value is null)) errors.Add($"{label}必须为字符串数组。");
        }
    }

    public static List<string> Validate(OperationProcessTemplate template, ProcessCatalog catalog)
    {
        var errors = ValidateForStorage(template);
        if (errors.Count > 0) return errors;
        ValidateName(template.Name, "模板", errors);
        OperationProcessEventNodes.Refresh(template, catalog);
        var activities = Index(template.Activities, x => x.Id, "活动", errors);
        var events = Index(template.Events, x => x.Id, "事件", errors);
        var precedences = Index(template.Precedences, x => x.Id, "次序", errors);
        var anchors = Index(template.Anchors, x => x.Id, "锚", errors);
        Index(template.RouteAnchors, x => x.RouteID, "进路锚配置", errors);
        var nodes = catalog.Nodes.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
        var routes = catalog.Routes.GroupBy(x => x.Id).ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);

        foreach (var anchor in template.Anchors)
        {
            ValidateCandidates(anchor.RouteIDs, null, routes, $"锚 {anchor.Id} 的停留进路", errors);
            if (anchor.RouteIDs is { Count: 0 }) errors.Add($"锚 {anchor.Id} 必须至少引用一条停留进路。");
            if (anchor.RouteIDs is not null && anchor.RouteIDs.Any(id => Has(routes, id) && routes[id].Type != "Dwelling"))
                errors.Add($"锚 {anchor.Id} 必须引用当前站场方案的停留进路。");
            ValidateName(anchor.Name, $"锚 {anchor.Id}", errors);
        }
        foreach (var ev in template.Events)
        {
            ValidateName(ev.Name, $"事件 {ev.Id}", errors);
            if (ev.Time is double time && !NonNegative(time)) errors.Add($"事件 {ev.Id} 时刻必须是非负有限分钟数或 null。");
            if (ev.NodeID is not null && !nodes.Contains(ev.NodeID)) errors.Add($"事件 {ev.Id} 的节点 {ev.NodeID} 不属于当前站场方案。");
            ValidateCandidates(ev.AnchorList, ev.SelectedAnchor, anchors, $"事件 {ev.Id} 的锚", errors);
            if (ev.NodeList.Count > 0 && ev.AnchorList is not null)
                foreach (var anchorID in ev.AnchorList.Where(id => Has(anchors, id)))
                {
                    var anchor = anchors[anchorID];
                    if (!ev.NodeList.Any(nodeID => OperationProcessResourceLocations.AnchorAtNode(anchor, nodeID, routes)))
                        errors.Add($"事件 {ev.Id} 的备选锚 {anchor.Id} 的停留进路不连接任何备选节点。");
                }
        }
        foreach (var activity in template.Activities)
        {
            ValidateName(activity.Name, $"活动 {activity.Id}", errors);
            if (!Types.Contains(activity.Type ?? "")) errors.Add($"活动 {activity.Id} 类型无效。");
            if (!NonNegative(activity.MinDuration) || !NonNegative(activity.MaxDuration) || activity.MinDuration > activity.MaxDuration)
                errors.Add($"活动 {activity.Id} 持续时间必须满足 0 ≤ 最小值 ≤ 最大值。");
            if (!double.IsFinite(activity.X) || !double.IsFinite(activity.Y)) errors.Add($"活动 {activity.Id} 图形坐标必须是有限数值。");
            if (!Has(events, activity.StartEvent) || !Has(events, activity.EndEvent)) errors.Add($"活动 {activity.Id} 的开始或结束事件不存在。");
            if (activity.StartEvent == activity.EndEvent) errors.Add($"活动 {activity.Id} 的开始和结束事件不能相同。");
            ValidateCandidates(activity.RouteList, activity.SelectedRoute, routes, $"活动 {activity.Id} 的进路", errors);
            if (activity.RouteList is { Count: 0 }) errors.Add($"活动 {activity.Id} 必须配置备选进路。");
            if (activity.RouteList is not null)
                foreach (var routeID in activity.RouteList.Where(id => Has(routes, id)))
                    if (routes[routeID].Type != activity.Type) errors.Add($"活动 {activity.Id} 与进路 {routeID} 类型不一致。");
            if (Has(events, activity.StartEvent) && Has(events, activity.EndEvent) &&
                events[activity.StartEvent].Time is double start && events[activity.EndEvent].Time is double end &&
                (end - start < activity.MinDuration - 0.000001 || end - start > activity.MaxDuration + 0.000001))
                errors.Add($"活动 {activity.Id} 两端事件时刻差必须位于持续时间范围内。");
        }
        foreach (var precedence in template.Precedences)
        {
            if (!Has(events, precedence.LeadingEvent) || !Has(events, precedence.FollowingEvent)) errors.Add($"次序 {precedence.Id} 引用的事件不存在。");
            if (precedence.LeadingEvent == precedence.FollowingEvent) errors.Add($"次序 {precedence.Id} 不能连接同一个事件。");
            if (!NonNegative(precedence.Interval)) errors.Add($"次序 {precedence.Id} 间隔必须是非负有限分钟数。");
            if (Has(events, precedence.LeadingEvent) && Has(events, precedence.FollowingEvent) &&
                events[precedence.LeadingEvent].Time is double leading && events[precedence.FollowingEvent].Time is double following &&
                (following - leading < precedence.Interval - 0.000001 || (precedence.IsExact && following - leading > precedence.Interval + 0.000001)))
                errors.Add(precedence.IsExact
                    ? $"次序 {precedence.Id} 前后序事件时刻差必须严格等于 {precedence.Interval} 分钟。"
                    : $"次序 {precedence.Id} 后序事件时刻必须至少晚于前序事件 {precedence.Interval} 分钟。");
        }
        foreach (var binding in template.RouteAnchors)
        {
            if (!Has(routes, binding.RouteID)) { errors.Add($"进路锚配置引用的进路 {binding.RouteID} 不存在。"); continue; }
            foreach (var (anchorID, nodeID, endpoint) in new[] {
                         (binding.StartAnchor, routes[binding.RouteID].StartNodeID, "起点"),
                         (binding.EndAnchor, routes[binding.RouteID].EndNodeID, "终点") })
            {
                if (anchorID is null) continue;
                if (!Has(anchors, anchorID)) errors.Add($"进路 {binding.RouteID} 的{endpoint}锚不存在。");
                else if (nodeID is not null) ValidateAnchorAtNode(anchors[anchorID], nodeID, routes, $"进路 {binding.RouteID} 的{endpoint}锚", errors);
            }
        }
        if (HasCycle(events.Keys, template.Activities, template.Precedences))
            errors.Add("活动与次序组成了循环依赖，请删除或反向调整相关次序。");
        return errors;
    }

    private static Dictionary<string, T> Index<T>(IEnumerable<T> items, Func<T, string> id, string label, List<string> errors)
    {
        var result = new Dictionary<string, T>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            var key = id(item);
            if (!ValidID(key)) errors.Add($"{label} id 必须为 1–100 个字符，且不能有首尾空白。");
            else if (!result.TryAdd(key, item)) errors.Add($"{label} id {key} 重复。");
        }
        return result;
    }

    private static void ValidateCandidates<T>(List<string>? candidates, string? selected, Dictionary<string, T> known, string label, List<string> errors)
    {
        if (candidates is null) { errors.Add($"{label}备选列表必须为数组。"); return; }
        if (candidates.Count != candidates.Distinct(StringComparer.Ordinal).Count()) errors.Add($"{label}备选列表包含重复项。");
        foreach (var id in candidates)
            if (!Has(known, id)) errors.Add($"{label}备选项 {id} 不存在或不属于当前范围。");
        if (selected is not null && !candidates.Contains(selected, StringComparer.Ordinal)) errors.Add($"{label}选定值必须属于备选列表。");
    }

    private static void ValidateAnchorAtNode(ProcessAnchor anchor, string nodeID, Dictionary<string, ProcessCatalogRoute> routes, string label, List<string> errors)
    {
        if (!OperationProcessResourceLocations.AnchorAtNode(anchor, nodeID, routes))
            errors.Add($"{label} {anchor.Id} 的停留进路不连接节点 {nodeID}。");
    }

    private static bool HasCycle(IEnumerable<string> eventIDs, IEnumerable<ProcessActivity> activities, IEnumerable<ProcessPrecedence> precedences)
    {
        var adjacency = eventIDs.ToDictionary(x => x, _ => new List<string>(), StringComparer.Ordinal);
        var incoming = adjacency.Keys.ToDictionary(x => x, _ => 0, StringComparer.Ordinal);
        var edges = activities.Select(x => (x.StartEvent, x.EndEvent))
            .Concat(precedences.Select(x => (x.LeadingEvent, x.FollowingEvent)));
        foreach (var (from, to) in edges)
            if (Has(adjacency, from) && Has(adjacency, to)) { adjacency[from].Add(to); incoming[to]++; }
        var queue = new Queue<string>(incoming.Where(x => x.Value == 0).Select(x => x.Key));
        var count = 0;
        while (queue.TryDequeue(out var node))
        {
            count++;
            foreach (var next in adjacency[node]) if (--incoming[next] == 0) queue.Enqueue(next);
        }
        return count != adjacency.Count;
    }

    private static bool ValidID(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 100 && value == value.Trim();
    private static bool NonNegative(double value) => double.IsFinite(value) && value >= 0;
    private static bool Has<T>(Dictionary<string, T> items, string? key) => key is not null && items.ContainsKey(key);
    private static void ValidateName(string? name, string label, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200) errors.Add($"{label}名称必须为 1–200 个字符。");
    }
}
