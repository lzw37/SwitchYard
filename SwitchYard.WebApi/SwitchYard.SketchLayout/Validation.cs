namespace SwitchYard.SketchLayout;

public static class Validation
{
    private static void Require([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool condition, string message) { if (!condition) throw new ArgumentException(message); }
    private static bool Text(string? s, int max) => !string.IsNullOrWhiteSpace(s) && s.Length <= max;
    private static bool Number(double n, double min, double max) => double.IsFinite(n) && n >= min && n <= max;
    private static bool PointValid(Point? p) => p != null && double.IsFinite(p.X) && double.IsFinite(p.Y);
    private static bool ColorValid(string? color) => color == null || (color.Length == 7 && color[0] == '#' && color.Skip(1).All(Uri.IsHexDigit));
    private static void Ids(IEnumerable<string> ids) { var list = ids.ToList(); Require(list.All(s => Text(s, 64)) && list.Distinct(StringComparer.Ordinal).Count() == list.Count, "元素 ID 不能为空、重复或超过 64 字符。"); }
    private static void AnalysisSettings(SketchAnalysisSettings s)
    {
        Require(s.CrossingModel is "poisson-fixed" or "uniform-fixed" or "poisson-exponential", "交叉点概率模型必须为泊松固定占用、均匀固定占用或泊松指数占用。");
        Require(Number(s.HorizonSeconds, 1, 86400 * 365), "分析时段应为 1 秒至 365 天。");
        Require(Number(s.AThreshold, 0, 1) && Number(s.EThreshold, 0, 1) && Number(s.IThreshold, 0, 1) && s.AThreshold > s.EThreshold && s.EThreshold > s.IThreshold && s.IThreshold > 0, "SLP 阈值必须满足 1 ≥ A > E > I > 0。");
    }
    public static void Validate(SketchLayoutScheme d)
    {
        Require(d != null, "方案不能为空。");
        Require(Text(d!.Name, 120) && d.Description != null && d.Description.Length <= 2000, "请填写方案名称（最多 120 字符），说明最多 2000 字符。");
        if (d.GridSettings is { } grid)
        {
            Require(Number(grid.Spacing, 1, 500), "网格间距必须为 1–500 的有限数值。");
            Require(double.IsFinite(grid.OriginX) && double.IsFinite(grid.OriginY), "网格原点必须为有限数值。");
        }
        Require(d.Facilities != null && d.Links != null && d.Plans != null, "设施、进路和流程方案不能为空值。");
        Require(d.Facilities!.Count <= 200 && d.Links!.Count <= 400 && d.Plans!.Count <= 50, "最多支持 200 处设施、400 条进路和 50 套流程方案。");
        Require(d.Facilities.All(f => f != null) && d.Links!.All(l => l != null) && d.Plans!.All(p => p != null), "元素不能为空值。");
        Ids(d.Facilities.Select(f => f.Id)); Ids(d.Links!.Select(l => l.Id)); Ids(d.Plans!.Select(p => p.Id));
        AnalysisSettings(d.ResolveAnalysisSettings());
        var facilities = d.Facilities.ToDictionary(f => f.Id);
        foreach (var f in d.Facilities)
        {
            Require(Text(f.Name, 120), "设施名称不能为空或超过 120 字符。");
            Require(ColorValid(f.FillColor) && ColorValid(f.StrokeColor), "设施颜色必须为 #RRGGBB 格式，或为空以使用默认颜色。");
            Require(f.Kind is "polygon" or "circle", "未知的设施类型。");
            Require(f.Vertices != null && f.Center != null, "设施坐标不能为空。");
            if (f.Kind == "circle") Require(PointValid(f.Center) && Number(f.Radius, 5, 200), "出入口圆心坐标必须为有限数值，半径为 5–200。");
            else Require(f.Vertices!.Count is >= 3 and <= 100 && f.Vertices.All(PointValid) && Geometry.SimplePolygon(f.Vertices), "多边形需至少 3 个不重合的顶点、面积大于零且不能自交，坐标须为有限数值。");
        }
        void CheckPort(ConnectionPort? p)
        {
            Require(p != null && p.FacilityId != null && facilities.ContainsKey(p.FacilityId), "作业进路引用了不存在的设施。");
            var f = facilities[p!.FacilityId];
            Require(f.Kind == "circle" ? p.Kind == "center" && p.Index == 0 : p.Kind is "vertex" or "midpoint" && p.Index >= 0 && p.Index < f.Vertices.Count, "作业进路端点必须为圆心、多边形顶点或边中点。");
        }
        foreach (var l in d.Links)
        {
            Require(Text(l.Name, 120), "作业进路名称不能为空或超过 120 字符。"); CheckPort(l.From); CheckPort(l.To);
            Require(l.From.FacilityId != l.To.FacilityId, "作业进路应连接两处不同的设施。");
            Require(l.Direction is "forward" or "reverse" or "both", "作业进路方向必须为正向、反向或双向。");
            Require(ColorValid(l.Color), "作业进路颜色必须为 #RRGGBB 格式，或为空以使用默认颜色。");
            Require(l.Anchors != null && l.Anchors.Count <= 24 && l.Anchors.All(PointValid), "曲线最多 24 个锚点，锚点坐标必须为有限数值。");
        }
        var links = d.Links.ToDictionary(l => l.Id);
        Require(d.Crossings != null && d.Crossings.Count <= 1000 && d.Crossings.All(c => c != null), "进路交叉标记不能为空值，且最多 1000 个。");
        Ids(d.Crossings.Select(c => c.Id));
        foreach (var c in d.Crossings)
        {
            Require(!facilities.ContainsKey(c.Id) && !links.ContainsKey(c.Id), "进路交叉标记 ID 不能与设施或进路重复。");
            Require(Text(c.Name, 120), "进路交叉名称不能为空或超过 120 字符。");
            Require(PointValid(c.Center) && Number(c.Radius, 5, 200), "进路交叉中心坐标必须为有限数值，半径为 5–200 的有限数值。");
            Require(c.SourceLinks != null && (c.SourceLinks.Count == 0 || c.SourceLinks.Count is >= 2 and <= 400) && c.SourceLinks.All(s => s != null && Text(s.Name, 120)), "进路交叉来源需记录 2–400 条进路及名称，旧标记可为空列表。");
            Ids(c.SourceLinks.Select(s => s.LinkId));
            Require(c.SourceLinks.All(s => s.OccupancySeconds == null || Number(s.OccupancySeconds.Value, .01, 86400 * 365)), "交叉点占用时间应为 0.01 秒至 365 天，或留空以使用通道通行时间。");
            // Source routes are historical records and may have since been deleted.
        }
        foreach (var p in d.Plans)
        {
            Require(Text(p.Name, 120), "流程方案名称不能为空或超过 120 字符。");
            Require(p.ExecutionMode == "parallel", "流程方案中的作业内容须为平行关系。");
            if (d.AnalysisSettings == null) AnalysisSettings(p.LegacyAnalysisSettings());
            Require(p.Chains != null && p.Chains.Count <= 100 && p.Chains.All(c => c != null && c.Steps != null), "每套方案最多 100 项作业内容，作业内容和步骤不能为空值。");
            p.NormalizeQuantity();
            Require(p.Quantity is >= 1 and <= 1000000, "流程方案作业数量须为 1–1000000 的整数，统一应用于全部作业内容。");
            Require(p.Chains!.Sum(c => c.Steps.Count) <= 1000, "每套方案最多 1000 个作业步骤。");
            Ids(p.Chains.Select(c => c.Id));
            foreach (var c in p.Chains)
            {
                Require(Text(c.Name, 120), "作业内容名称不能为空或超过 120 字符。");
                Require(c.Steps.Count <= 100 && c.Steps.All(s => s != null), "每项作业内容最多 100 步，步骤不能为空值。");
                for (var i = 0; i < c.Steps.Count; i++)
                {
                    var s = c.Steps[i];
                    Require(s.FacilityId != null && facilities.ContainsKey(s.FacilityId), "流程引用了不存在的设施。");
                    Require(Number(s.DwellSeconds, 0, 86400 * 365) && Number(s.TravelSeconds, 0, 86400 * 365), "停留和通行时间必须为有限的非负秒数（最多 365 天）。");
                    if (i == 0) Require(string.IsNullOrEmpty(s.IncomingLinkId) && s.TravelSeconds == 0, "首个步骤不能设置来向通道或通行时间。");
                    else
                    {
                        Require(s.IncomingLinkId != null && links.ContainsKey(s.IncomingLinkId), "相邻作业步骤必须选择有效的作业进路。");
                        var l = links[s.IncomingLinkId!]; var before = c.Steps[i - 1].FacilityId;
                        Require((l.From.FacilityId == before && l.To.FacilityId == s.FacilityId) || (l.To.FacilityId == before && l.From.FacilityId == s.FacilityId), "作业内容不连续：所选作业进路未连接相邻设施。");
                        Require(l.AllowsTravel(before, s.FacilityId), "作业内容与作业进路的通行方向不一致。");
                        Require(s.TravelSeconds > 0, "通行时间必须大于 0 秒。");
                    }
                }
            }
        }
    }
}
