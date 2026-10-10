namespace SwitchYard.SketchLayout;

public static class SketchLayoutAnalyzer
{
    private sealed record Occupation(string Kind, string Id, double Start, double End);
    private static List<Occupation> Occupations(ProcessChain c)
    {
        var result = new List<Occupation>(); var t = 0d;
        foreach (var s in c.Steps)
        {
            if (s.IncomingLinkId != null) { result.Add(new("link", s.IncomingLinkId, t, t + s.TravelSeconds)); t += s.TravelSeconds; }
            if (s.DwellSeconds > 0) result.Add(new("facility", s.FacilityId, t, t + s.DwellSeconds));
            t += s.DwellSeconds;
        }
        return result;
    }
    // For two independent U[0,H] starts, delta = startB - startA has a triangular CDF.
    public static double DifferenceCdf(double delta, double horizon) => delta <= -horizon ? 0 : delta >= horizon ? 1 : delta <= 0 ? Math.Pow((delta + horizon) / horizon, 2) / 2 : 1 - Math.Pow((horizon - delta) / horizon, 2) / 2;
    public static double UnionProbability(IEnumerable<(double Start, double End)> intervals, double horizon)
    {
        var sorted = intervals.Where(x => x.Start < x.End).OrderBy(x => x.Start).ToList();
        if (sorted.Count == 0) return 0;
        var start = sorted[0].Start; var end = sorted[0].End; var p = 0d;
        foreach (var next in sorted.Skip(1))
        {
            if (next.Start <= end) end = Math.Max(end, next.End);
            else { p += DifferenceCdf(end, horizon) - DifferenceCdf(start, horizon); start = next.Start; end = next.End; }
        }
        return Math.Clamp(p + DifferenceCdf(end, horizon) - DifferenceCdf(start, horizon), 0, 1);
    }
    public static AnalysisResult Analyze(SketchLayoutScheme d)
    {
        Validation.Validate(d);
        var settings = d.ResolveAnalysisSettings();
        var entries = d.Plans.SelectMany(p => p.Chains.Select(c => (Plan: p, Chain: c))).ToList();
        var chains = entries.Select(e => e.Chain).ToList();
        if (chains.Count == 0) throw new ArgumentException("分析至少需要一项完整的作业内容。");
        var draft = entries.FirstOrDefault(e => e.Chain.Steps.Count < 2);
        if (draft.Chain != null) throw new ArgumentException($"请先补全作业内容“{draft.Plan.Name} / {draft.Chain.Name}”：所有流程方案中的作业内容均需至少两处设施。");
        var facilities = d.Facilities.ToDictionary(f => f.Id); var links = d.Links.ToDictionary(l => l.Id);
        var flows = new Dictionary<(string, string), (long Forward, long Reverse)>();
        foreach (var entry in entries) for (var i = 1; i < entry.Chain.Steps.Count; i++)
        {
            var c = entry.Chain; var quantity = entry.Plan.Quantity;
            var a = c.Steps[i - 1].FacilityId; var b = c.Steps[i].FacilityId;
            var forward = string.CompareOrdinal(a, b) < 0; var key = forward ? (a, b) : (b, a);
            var v = flows.GetValueOrDefault(key);
            flows[key] = forward ? (v.Forward + quantity, v.Reverse) : (v.Forward, v.Reverse + quantity);
        }
        var max = flows.Values.Max(v => v.Forward + v.Reverse);
        var relations = new List<FlowRelation>();
        for (var i = 0; i < d.Facilities.Count; i++) for (var j = i + 1; j < d.Facilities.Count; j++)
        {
            var a = d.Facilities[i].Id; var b = d.Facilities[j].Id;
            if (string.CompareOrdinal(a, b) > 0) (a, b) = (b, a);
            var v = flows.GetValueOrDefault((a, b)); var total = v.Forward + v.Reverse; var intensity = (double)total / max;
            var grade = total == 0 ? "U" : intensity >= settings.AThreshold ? "A" : intensity >= settings.EThreshold ? "E" : intensity >= settings.IThreshold ? "I" : "O";
            relations.Add(new(a, b, v.Forward, v.Reverse, total, intensity, grade));
        }
        var used = chains.SelectMany(c => c.Steps).Select(s => s.IncomingLinkId).OfType<string>().Distinct().ToList();
        var paths = used.ToDictionary(id => id, id => Geometry.Flatten(links[id], facilities, d.ShowCurveArc));
        if (paths.Values.Sum(points => points.Count - 1) > 5000)
            throw new ArgumentException("参与分析的通道曲线过于复杂，请减少曲线锚点（整张布置图最多 5000 个近似线段）。");
        var crossings = new HashSet<(string, string)>();
        for (var i = 0; i < used.Count; i++) for (var j = i + 1; j < used.Count; j++)
            if (Geometry.PathsIntersect(paths[used[i]], paths[used[j]])) { crossings.Add((used[i], used[j])); crossings.Add((used[j], used[i])); }
        // Chain IDs are unique within a plan, not across plans. Keep each occurrence distinct.
        var occupations = chains.Select(Occupations).ToList();
        var totalOccupations = occupations.Sum(o => (long)o.Count);
        var comparisons = (totalOccupations * totalOccupations + occupations.Sum(o => (long)o.Count * o.Count)) / 2;
        var chainPairs = (long)chains.Count * (chains.Count + 1) / 2;
        for (var i = 0; i < chains.Count; i++) if (entries[i].Plan.Quantity == 1)
        {
            comparisons -= (long)occupations[i].Count * occupations[i].Count;
            chainPairs--;
        }
        // Reject oversized work before creating any partial result or a huge conflict table.
        if (comparisons > 5_000_000 || chainPairs > 50_000)
            throw new ArgumentException("全部作业内容的分析组合过多（最多 5 万组作业内容组合、500 万组占用比较）。请合并路径和时间完全相同的重复作业内容，用作业数量表示重复次数后再分析。");
        var conflicts = new List<ProcessConflict>();
        for (var i = 0; i < chains.Count; i++) for (var j = i; j < chains.Count; j++)
        {
            var a = chains[i]; var b = chains[j];
            var quantityA = entries[i].Plan.Quantity; var quantityB = entries[j].Plan.Quantity;
            var count = i == j ? (long)quantityA * (quantityA - 1) / 2 : (long)quantityA * quantityB;
            if (count == 0) continue;
            var intervals = new List<(double, double)>(); var resources = new Dictionary<(string, string, string), ConflictResource>();
            foreach (var oa in occupations[i]) foreach (var ob in occupations[j])
            {
                var shared = oa.Kind == ob.Kind && oa.Id == ob.Id;
                var crossing = oa.Kind == "link" && ob.Kind == "link" && crossings.Contains((oa.Id, ob.Id));
                if (!shared && !crossing) continue;
                // Open occupancy intervals overlap iff delta lies in (a.start-b.end, a.end-b.start).
                intervals.Add((oa.Start - ob.End, oa.End - ob.Start));
                var kind = crossing ? "crossing" : oa.Kind;
                var name = crossing ? $"{links[oa.Id].Name} × {links[ob.Id].Name}" : oa.Kind == "facility" ? facilities[oa.Id].Name : links[oa.Id].Name;
                var key = string.CompareOrdinal(oa.Id, ob.Id) <= 0 ? (kind, oa.Id, ob.Id) : (kind, ob.Id, oa.Id);
                resources.TryAdd(key, new(kind, oa.Id, ob.Id, name));
            }
            var probability = UnionProbability(intervals, settings.HorizonSeconds);
            if (probability > 0) conflicts.Add(new(entries[i].Plan.Id, a.Id, entries[j].Plan.Id, b.Id, probability, count, count * probability, resources.Values.ToList()));
        }
        List<string> warnings = ["各次作业独立地在 [0,分析时段] 内均匀随机出发，停留与通行时间固定；作业可延续至时段结束之后。设施与通道均按单容量互斥资源处理，同向和反向均可能冲突。",
            "通道按当前折线或圆角曲线形状分析；每段倒角圆弧按 24 等分近似。交叉或重叠通道按整个通行期间互斥，结果是保守的占用冲突估计，不是碰撞概率。",
            "冲突概率表示随机抽取的两次作业至少发生一次占用冲突的概率；期望冲突作业对数可相加，但不是全系统至少一次冲突的概率。",
            "SLP 按双向累计作业量与最大设施对作业量之比划分 A/E/I/O，零流量为 U；阈值为项目配置，未评价非作业关系或 X（不宜接近）。"];
        if (chains.Any(c => c.Steps.Sum(s => s.DwellSeconds + s.TravelSeconds) > settings.HorizonSeconds)) warnings.Add("部分作业持续时间超过分析出发时段，请检查时间单位与时段设置。");
        return new("all-plans", d.Plans.Count, chains.Count, entries.Sum(e => (long)e.Plan.Quantity), settings.HorizonSeconds, "independent-uniform-start/exclusive-resources/all-plans/v2", relations.OrderByDescending(r => r.Total).ToList(), conflicts.OrderByDescending(c => c.ExpectedConflictingPairs).ToList(), conflicts.Sum(c => c.ExpectedConflictingPairs), warnings)
        { CrossingRisks = CrossingConflictAnalyzer.Analyze(d) };
    }
}
