using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using SwitchYard.Service.Models;

namespace SwitchYard.Service.Services;

/// <summary>Parses and plans an append-only import against a supplied station snapshot.</summary>
public sealed class CellOccupancyImportBuilder
{
    private const string TrainType = "旅客列车";
    private static readonly string[] Columns = { "TrainID", "MovementID", "MovementType", "CellID", "OccupancyStartTime", "OccupancyEndTime" };
    private sealed record Occupation(string Train, string Movement, string Type, string SourceCell, int Start, int End, int Line);
    private sealed record Path(List<string> Nodes, List<string> Links, List<string> Cells);

    public CellOccupancyImportResult Build(CellOccupancyImportRequest request, CellOccupancyImportCatalog catalog)
    {
        var result = new CellOccupancyImportResult();
        var preview = result.Preview;
        preview.AvailableCells = catalog.Cells.OrderBy(c => c.Name, StringComparer.Ordinal).ThenBy(c => c.ID, StringComparer.Ordinal)
            .Select(c => new CellOccupancyAvailableCell { ID = c.ID, Name = c.Name }).ToList();
        var records = ReadCsv(request.CsvText ?? "", preview.Errors);
        preview.OccupancyCount = records.Count;
        preview.TrainCount = records.Select(r => r.Train).Distinct(StringComparer.Ordinal).Count();
        var groups = records.GroupBy(r => (r.Train, r.Movement)).ToList();
        preview.MovementCount = groups.Count;
        if (records.Select(r => r.Train).Distinct(StringComparer.Ordinal).Count()
            != records.Select(r => r.Train).Distinct(StringComparer.OrdinalIgnoreCase).Count())
            preview.Errors.Add("TrainID 不能只以大小写区分不同列车。");
        if (records.GroupBy(r => r.Train).Any(g => g.Select(r => r.Movement).Distinct(StringComparer.Ordinal).Count()
            != g.Select(r => r.Movement).Distinct(StringComparer.OrdinalIgnoreCase).Count()))
            preview.Errors.Add("同一列车的 MovementID 不能只以大小写区分不同作业。");
        if (preview.Errors.Count > 0) return result;
        if (catalog.Cells.Select(c => c.ID).Distinct().Count() != catalog.Cells.Count
            || catalog.Links.Select(l => l.ID).Distinct().Count() != catalog.Links.Count
            || catalog.Nodes.Select(n => n.ID).Distinct().Count() != catalog.Nodes.Count)
        {
            preview.Errors.Add("当前站场包含重复的 Cell、节点或 Link ID，请先修复站场数据。");
            return result;
        }

        var topology = new Topology(catalog);
        var mappings = ResolveCells(records, request.CellMappings ?? new(), catalog, topology, preview);
        if (preview.Errors.Count > 0) return result;
        var dwellingCells = records.Where(r => r.Type == "Dwelling").Select(r => mappings[r.SourceCell]).ToHashSet(StringComparer.Ordinal);
        var pathCache = new Dictionary<string, Path?>();
        var selectedRoutes = new Dictionary<string, CellOccupancyRoute>();
        var allRoutes = catalog.Routes.ToList();
        var allTimes = catalog.RouteTimes.ToList();
        // Production MySQL identifiers use a case-insensitive collation.
        var occupiedRouteIds = catalog.Routes.Select(r => r.ID).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var train in groups.GroupBy(g => g.Key.Train).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            result.Trains.Add(new CellOccupancyTrainDraft { ID = train.Key, TrainNumber = train.Key, Name = train.Key });
            Path? previousPath = null;
            int? previousEnd = null;
            var order = 0;
            foreach (var group in train.OrderBy(g => MovementOrder(g.Key.Movement)).ThenBy(g => g.Key.Movement, StringComparer.Ordinal))
            {
                var label = $"列车 {group.Key.Train} / 作业 {group.Key.Movement}";
                var types = group.Select(r => r.Type).Distinct().ToArray();
                if (types.Length != 1) { preview.Errors.Add($"{label} 包含不同 MovementType。"); continue; }
                var type = types[0];
                var rows = group.ToList();
                var byCell = rows.GroupBy(r => mappings[r.SourceCell]).ToList();
                if (byCell.Any(g => g.Count() != 1)) { preview.Errors.Add($"{label} 的同一 Cell 出现多条占用记录，无法无损导入。"); continue; }
                var wanted = byCell.Select(g => g.Key).ToHashSet(StringComparer.Ordinal);
                var tracks = wanted.Where(id => dwellingCells.Contains(id) || IsTrack(topology.Cells[id].Name)
                    || rows.Any(r => mappings[r.SourceCell] == id && IsTrack(r.SourceCell))).ToList();
                // A single-cell Dw movement identifies its platform even when the station uses a custom name.
                if (type == "Dwelling" && wanted.Count == 1) tracks = wanted.ToList();
                if (tracks.Count != 1 || (type == "Dwelling" && wanted.Count != 1))
                {
                    preview.Errors.Add($"{label} 必须能确定唯一股道；Dw 停留作业必须仅占用一条股道。");
                    continue;
                }
                var track = tracks[0];
                var key = type + "|" + string.Join(",", wanted.OrderBy(x => x, StringComparer.Ordinal));
                if (!pathCache.TryGetValue(key, out var path))
                {
                    path = topology.FindPath(wanted, track, type, out var reason);
                    pathCache[key] = path;
                    if (path == null) preview.Errors.Add($"{label}：{reason}");
                }
                if (path == null) continue;
                // Departure locking can precede actual departure. Anchor to the platform-cell entry,
                // retaining earlier throat occupation as a negative offset, exactly as in the CSV.
                var start = type == "Departure" ? rows.Single(r => mappings[r.SourceCell] == track).Start : rows.Max(r => r.Start);
                var end = rows.Max(r => r.End);
                if (end < start) { preview.Errors.Add($"{label} 结束时刻早于作业开始时刻。"); continue; }
                if (previousEnd.HasValue && start < previousEnd.Value)
                    preview.Errors.Add($"{label} 的作业开始早于前一作业结束；请检查 MovementID 顺序及股道占用时间。");
                if (previousPath != null && previousPath.Cells[^1] != path.Cells[0])
                    preview.Errors.Add($"{label} 与前一作业的首尾 Cell 不衔接。");
                previousPath = path;
                previousEnd = end;
                var shifts = rows.Select(r => new CellOccupancyRouteTime
                {
                    CellID = mappings[r.SourceCell], StartOccupationShift = r.Start - start, EndOccupationShift = r.End - end
                }).OrderBy(r => r.CellID, StringComparer.Ordinal).ToList();
                var signature = key + "|" + string.Join(",", path.Links) + "|" + string.Join(",", path.Nodes)
                    + "|" + string.Join(";", shifts.Select(t => $"{t.CellID}:{t.StartOccupationShift}:{t.EndOccupationShift}"));
                if (!selectedRoutes.TryGetValue(signature, out var route))
                {
                    route = allRoutes.OrderBy(r => r.ID, StringComparer.Ordinal).FirstOrDefault(r =>
                        NormalizeType(r.Type) == type && topology.RouteMatches(r, path) && TimesMatch(r.ID, shifts, allTimes));
                    if (route == null)
                    {
                        var routeId = "CO_" + Hash(signature)[..32];
                        var suffix = 0;
                        while (occupiedRouteIds.Contains(routeId)) routeId = "CO_" + Hash(signature + "|" + ++suffix)[..32];
                        occupiedRouteIds.Add(routeId);
                        route = topology.MakeRoute(routeId, type, path, $"CellOccupancy导入·{topology.Cells[track].Name}·{TypeLabel(type)}");
                        result.NewRoutes.Add(route);
                        foreach (var shift in shifts)
                        {
                            shift.RouteID = route.ID;
                            result.NewRouteTimes.Add(shift);
                            allTimes.Add(shift);
                        }
                        allRoutes.Add(route);
                    }
                    selectedRoutes[signature] = route;
                    preview.RouteMatches.Add(new CellOccupancyRouteMatch
                    {
                        MovementType = type, RouteID = route.ID, Description = route.Description,
                        IsNew = result.NewRoutes.Any(r => r.ID == route.ID)
                    });
                }
                preview.RouteMatches.Single(r => r.RouteID == route.ID).MovementCount++;
                result.Movements.Add(new CellOccupancyMovementDraft
                {
                    TrainID = group.Key.Train, MovementID = group.Key.Movement, Name = TypeLabel(type),
                    RouteIDList = route.ID, Route = route.ID, MinDuration = end - start,
                    EarliestStartTime = FormatTime(start), LatestEndTime = FormatTime(end), SortOrder = order++
                });
            }
        }
        preview.NewRouteCount = result.NewRoutes.Count;
        preview.ReusedRouteCount = preview.RouteMatches.Count(r => !r.IsNew);
        preview.Valid = preview.Errors.Count == 0 && result.Movements.Count == groups.Count;
        if (!preview.Valid)
        {
            // Invalid previews are diagnostic only: never expose a partial set of writable rows.
            result.NewRoutes.Clear(); result.NewRouteTimes.Clear(); result.Trains.Clear(); result.Movements.Clear();
        }
        return result;
    }

    private static Dictionary<string, string> ResolveCells(List<Occupation> records, Dictionary<string, string> overrides,
        CellOccupancyImportCatalog catalog, Topology topology, CellOccupancyImportPreview preview)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var source in records.Select(r => r.SourceCell).Distinct().OrderBy(x => x, StringComparer.Ordinal))
        {
            var match = new CellOccupancyCellMatch { SourceCell = source };
            CellOccupancyCell? cell = null;
            if (overrides.TryGetValue(source, out var mapped))
            {
                cell = catalog.Cells.SingleOrDefault(c => c.ID == mapped);
                match.Method = "手动指定";
                if (cell == null) preview.Errors.Add($"Cell {source} 指定的目标 ID 不属于当前站场。");
            }
            else
            {
                cell = catalog.Cells.SingleOrDefault(c => c.ID == source);
                match.Method = "Cell ID";
                if (cell == null)
                {
                    var names = catalog.Cells.Where(c => string.Equals(c.Name, source, StringComparison.OrdinalIgnoreCase)).ToList();
                    match.Method = "唯一名称";
                    if (names.Count == 1) cell = names[0];
                    else
                    {
                        var alternate = AlternateName(source);
                        if (names.Count == 0 && alternate != null)
                        {
                            names = catalog.Cells.Where(c => string.Equals(c.Name, alternate, StringComparison.OrdinalIgnoreCase)).ToList();
                            match.Method = "股道/区段名称标准化";
                            if (names.Count == 1) cell = names[0];
                        }
                        if (cell == null)
                        {
                            var numbers = Regex.IsMatch(source, "^[0-9]+(?:[-/][0-9]+)*DG?$", RegexOptions.IgnoreCase)
                                ? SwitchNumbers(source) : new HashSet<string>();
                            var candidates = (names.Count > 1 ? names : catalog.Cells).Where(c => numbers.Count > 0
                                && topology.CellSwitchNumbers(c.ID).Overlaps(numbers)).ToList();
                            if (candidates.Count == 1)
                            {
                                cell = candidates[0];
                                match.Method = "道岔拓扑与进路连续性";
                                preview.Warnings.Add($"Cell {source} 根据道岔拓扑匹配为 {cell.Name}（{cell.ID}），导入前请核对预览。");
                            }
                        }
                    }
                }
            }
            if (cell == null)
            {
                match.NeedsSelection = true;
                match.Method = "需要选择目标 Cell";
                preview.Errors.Add($"Cell {source} 无法唯一可靠匹配，请显式选择当前站场中的目标 Cell。");
            }
            else
            {
                match.CellID = cell.ID; match.CellName = cell.Name; result[source] = cell.ID;
            }
            preview.CellMatches.Add(match);
        }
        return result;
    }

    private static bool TimesMatch(string routeId, List<CellOccupancyRouteTime> expected, List<CellOccupancyRouteTime> catalog)
    {
        var routeTimes = catalog.Where(t => t.RouteID == routeId).ToList();
        // Playback chooses one complete profile: any specific train-type rows suppress the
        // default profile as a whole. Do not fill missing specific cells from default rows.
        var effective = routeTimes.Where(t => t.TrainTypeID == TrainType).ToList();
        if (effective.Count == 0) effective = routeTimes.Where(t => string.IsNullOrWhiteSpace(t.TrainTypeID)).ToList();
        foreach (var item in expected)
        {
            var relevant = effective.Where(t => t.CellID == item.CellID).ToList();
            if (relevant.Count != 1 || relevant[0].StartOccupationShift != item.StartOccupationShift
                || relevant[0].EndOccupationShift != item.EndOccupationShift) return false;
        }
        return !effective.Any(t => !expected.Any(e => e.CellID == t.CellID));
    }

    private static List<Occupation> ReadCsv(string text, List<string> errors)
    {
        var result = new List<Occupation>();
        if (string.IsNullOrWhiteSpace(text)) { errors.Add("请选择非空的 CellOccupancy CSV 文件。"); return result; }
        if (text.Length > 12_000_000) { errors.Add("CSV 文件过大，请将单次导入控制在 12 MB 以内。"); return result; }
        List<(int Line, List<string> Values)> table;
        try { table = ParseCsv(text.TrimStart('\uFEFF')); }
        catch (FormatException ex) { errors.Add(ex.Message); return result; }
        if (table.Count == 0) { errors.Add("CSV 缺少表头。"); return result; }
        var header = table[0].Values.Select(v => v.Trim()).ToList();
        if (header.Count != Columns.Length || Columns.Any(c => header.Count(h => h.Equals(c, StringComparison.OrdinalIgnoreCase)) != 1))
        {
            errors.Add("CSV 表头必须包含且仅包含 TrainID、MovementID、MovementType、CellID、OccupancyStartTime、OccupancyEndTime 六列。");
            return result;
        }
        var indexes = Columns.Select(c => header.FindIndex(h => h.Equals(c, StringComparison.OrdinalIgnoreCase))).ToArray();
        foreach (var (line, values) in table.Skip(1))
        {
            if (result.Count >= 100_000) { errors.Add("单次最多导入 100000 条占用记录。"); break; }
            if (values.Count != 6) { errors.Add($"CSV 第 {line} 行不是六列。"); continue; }
            var fields = indexes.Select(i => values[i].Trim()).ToArray();
            if (fields.Any(string.IsNullOrEmpty)) { errors.Add($"CSV 第 {line} 行包含空字段。"); continue; }
            if (fields[0].Length > 50 || fields[1].Length > 50 || fields[3].Length > 200
                || fields.Take(4).Any(f => f.Any(char.IsControl)))
            { errors.Add($"CSV 第 {line} 行标识符过长或含控制字符。"); continue; }
            var type = NormalizeType(fields[2]);
            if (type is not ("Arrival" or "Departure" or "Dwelling"))
            { errors.Add($"CSV 第 {line} 行 MovementType 必须为 Arr、Dep 或 Dw。"); continue; }
            if (!TryTime(fields[4], out var start) || !TryTime(fields[5], out var end))
            { errors.Add($"CSV 第 {line} 行时间必须为同一天的 HH:mm:ss 整秒时刻；不支持小数秒或跨日。"); continue; }
            if (end < start) { errors.Add($"CSV 第 {line} 行结束时间早于开始时间，不支持隐式跨日。"); continue; }
            result.Add(new Occupation(fields[0], fields[1], type, fields[3], start, end, line));
        }
        if (result.Count == 0 && errors.Count == 0) errors.Add("CSV 中没有占用记录。");
        return result;
    }

    private static List<(int Line, List<string> Values)> ParseCsv(string text)
    {
        var records = new List<(int, List<string>)>();
        var fields = new List<string>(); var field = new StringBuilder();
        var quoted = false; var closed = false; var line = 1; var firstLine = 1;
        void EndField() { fields.Add(field.ToString()); field.Clear(); closed = false; }
        void EndRow()
        {
            EndField();
            if (fields.Any(f => !string.IsNullOrWhiteSpace(f))) records.Add((firstLine, fields));
            fields = new List<string>(); firstLine = line + 1;
        }
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quoted)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else { quoted = false; closed = true; }
                }
                else { field.Append(c); if (c == '\n') line++; }
                continue;
            }
            if (c == ',') { EndField(); continue; }
            if (c is '\r' or '\n')
            {
                EndRow(); if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++; line++; continue;
            }
            if (closed)
            {
                if (!char.IsWhiteSpace(c)) throw new FormatException($"CSV 第 {line} 行引号后包含非法字符。");
                continue;
            }
            if (c == '"')
            {
                if (field.Length > 0) throw new FormatException($"CSV 第 {line} 行引号格式错误。");
                quoted = true;
            }
            else field.Append(c);
        }
        if (quoted) throw new FormatException($"CSV 第 {firstLine} 行引号未闭合。");
        if (field.Length > 0 || fields.Count > 0 || closed) EndRow();
        return records;
    }

    private static string NormalizeType(string? type) => (type ?? "").Trim().ToLowerInvariant() switch
    { "arr" or "arrival" => "Arrival", "dep" or "departure" => "Departure", "dw" or "dwelling" => "Dwelling", _ => "" };
    private static string TypeLabel(string type) => type == "Arrival" ? "接车" : type == "Departure" ? "发车" : "停留";
    private static long MovementOrder(string value) => long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : long.MaxValue;
    private static bool IsTrack(string value) => Regex.IsMatch(value.Trim(), "^(?:[0-9]+|[IVXLCDMⅠⅡⅢⅣⅤⅥⅦⅧⅨⅩ]+)G$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static HashSet<string> SwitchNumbers(string value) => Regex.Matches(value, "[0-9]+").Select(m => m.Value.TrimStart('0')).ToHashSet(StringComparer.Ordinal);
    private static string? AlternateName(string value)
    {
        if (Regex.IsMatch(value, "^[0-9]+D$", RegexOptions.IgnoreCase)) return value + "G";
        var match = Regex.Match(value, "^([0-9]+)G$", RegexOptions.IgnoreCase);
        if (!match.Success || !int.TryParse(match.Groups[1].Value, out var n) || n < 1 || n > 99) return null;
        var roman = new StringBuilder();
        foreach (var pair in new[] { (90, "XC"), (50, "L"), (40, "XL"), (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I") })
            while (n >= pair.Item1) { roman.Append(pair.Item2); n -= pair.Item1; }
        return roman + "G";
    }
    private static bool TryTime(string value, out int seconds)
    {
        seconds = 0;
        var match = Regex.Match(value, "^([0-9]{1,2}):([0-9]{2})(?::([0-9]{2}))?$");
        if (!match.Success) return false;
        var h = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        var m = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
        var s = match.Groups[3].Success ? int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture) : 0;
        if (h > 23 || m > 59 || s > 59) return false;
        seconds = h * 3600 + m * 60 + s; return true;
    }
    private static string FormatTime(int value) => $"{value / 3600:00}:{value / 60 % 60:00}:{value % 60:00}";
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static List<string> List(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return new();
        if (!value.TrimStart().StartsWith('[')) return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        try
        {
            using var document = JsonDocument.Parse(value);
            return document.RootElement.EnumerateArray().Select(e => e.ValueKind == JsonValueKind.String ? e.GetString() ?? "" : e.ToString()).ToList();
        }
        catch (JsonException) { return new(); }
    }

    private sealed class Topology
    {
        public Dictionary<string, CellOccupancyCell> Cells { get; }
        private readonly CellOccupancyImportCatalog catalog;
        private readonly Dictionary<string, CellOccupancyLink> links;
        private readonly Dictionary<string, CellOccupancyNode> nodes;
        private readonly Dictionary<string, List<string>> owners = new();
        private readonly Dictionary<string, List<string>> adjacent = new();
        private readonly Dictionary<string, HashSet<string>> turns = new();
        private readonly HashSet<string> endpoints;
        private readonly HashSet<string> switches;

        public Topology(CellOccupancyImportCatalog catalog)
        {
            this.catalog = catalog;
            Cells = catalog.Cells.ToDictionary(c => c.ID, StringComparer.Ordinal);
            links = catalog.Links.ToDictionary(l => l.ID, StringComparer.Ordinal);
            nodes = catalog.Nodes.ToDictionary(n => n.ID, StringComparer.Ordinal);
            endpoints = catalog.RouteEnds.Select(e => e.BindingNodeID).ToHashSet(StringComparer.Ordinal);
            switches = catalog.Switches.Select(e => e.BindingNodeID).ToHashSet(StringComparer.Ordinal);
            foreach (var cell in catalog.Cells)
                foreach (var link in List(cell.LinkIDList).Distinct())
                { if (!owners.TryGetValue(link, out var list)) owners[link] = list = new(); list.Add(cell.ID); }
            foreach (var link in catalog.Links)
                foreach (var node in new[] { link.FromNodeID, link.ToNodeID })
                { if (!adjacent.TryGetValue(node, out var list)) adjacent[node] = list = new(); list.Add(link.ID); }
            foreach (var route in catalog.Routes)
            {
                var path = ReadPath(route);
                if (path == null) continue;
                for (var i = 1; i < path.Nodes.Count - 1; i++)
                {
                    if (!turns.TryGetValue(path.Nodes[i], out var known)) turns[path.Nodes[i]] = known = new();
                    known.Add(TurnKey(path.Links[i - 1], path.Links[i]));
                }
            }
        }

        public HashSet<string> CellSwitchNumbers(string cellId)
        {
            var cellNodes = List(Cells[cellId].LinkIDList).Where(links.ContainsKey)
                .SelectMany(id => new[] { links[id].FromNodeID, links[id].ToNodeID }).ToHashSet();
            return catalog.Switches.Where(s => cellNodes.Contains(s.BindingNodeID))
                .SelectMany(s => SwitchNumbers(s.Name)).ToHashSet(StringComparer.Ordinal);
        }

        private Path? ReadPath(CellOccupancyRoute route)
        {
            var n = List(route.NodeList); var l = List(route.LinkList);
            if (n.Count != l.Count + 1 || l.Count == 0 || n.Distinct().Count() != n.Count) return null;
            if (n.Any(id => !nodes.ContainsKey(id))) return null;
            var cells = new List<string>();
            for (var i = 0; i < l.Count; i++)
            {
                if (!links.TryGetValue(l[i], out var link) || !Connects(link, n[i], n[i + 1])
                    || !owners.TryGetValue(l[i], out var owned) || owned.Count != 1) return null;
                if (cells.Count == 0 || cells[^1] != owned[0]) cells.Add(owned[0]);
            }
            if (cells.Distinct().Count() != cells.Count) return null;
            return new Path(n, l, cells);
        }

        public bool RouteMatches(CellOccupancyRoute route, Path expected)
        {
            var actual = ReadPath(route);
            return actual != null && actual.Nodes.SequenceEqual(expected.Nodes) && actual.Links.SequenceEqual(expected.Links)
                && List(route.CellList).ToHashSet().SetEquals(expected.Cells)
                && route.StartNodeID == expected.Nodes[0] && route.EndNodeID == expected.Nodes[^1];
        }

        public Path? FindPath(HashSet<string> wanted, string track, string type, out string reason)
        {
            var candidates = new Dictionary<string, Path>();
            void Add(List<string> n, List<string> l)
            {
                var p = ReadPath(new CellOccupancyRoute { NodeList = JsonSerializer.Serialize(n), LinkList = JsonSerializer.Serialize(l) });
                if (p == null || !p.Cells.ToHashSet().SetEquals(wanted) || !DirectionMatches(p, track, type)) return;
                if (endpoints.Count > 0 && (!endpoints.Contains(n[0]) || !endpoints.Contains(n[^1]))) return;
                // A stationary Dw path has no travel direction. Deduplicate its reverse while
                // retaining the stored route's orientation so a compatible existing route is reused.
                var reverseKey = type == "Dwelling" && string.CompareOrdinal(n[0], n[^1]) > 0;
                var identityNodes = reverseKey ? p.Nodes.AsEnumerable().Reverse() : p.Nodes;
                var identityLinks = reverseKey ? p.Links.AsEnumerable().Reverse() : p.Links;
                candidates.TryAdd(string.Join(",", identityNodes) + "|" + string.Join(",", identityLinks), p);
            }
            foreach (var route in catalog.Routes.Where(r => NormalizeType(r.Type) == type).OrderBy(r => r.ID, StringComparer.Ordinal))
            {
                var p = ReadPath(route);
                if (p == null) continue;
                var n = p.Nodes.ToList(); var l = p.Links.ToList();
                while (l.Count > 0 && !wanted.Contains(owners[l[0]][0])) { l.RemoveAt(0); n.RemoveAt(0); }
                while (l.Count > 0 && !wanted.Contains(owners[l[^1]][0])) { l.RemoveAt(l.Count - 1); n.RemoveAt(n.Count - 1); }
                if (l.Count == 0 || l.Any(id => !wanted.Contains(owners[id][0]))) continue;
                Add(n, l);
                // Existing departure routes often start at the platform signal, omitting the track.
                if (type == "Departure" && !l.Any(id => owners[id][0] == track))
                    ExtendTrack(n, l, track, prepend: true, Add);
                if (type == "Arrival" && !l.Any(id => owners[id][0] == track))
                    ExtendTrack(n, l, track, prepend: false, Add);
            }
            if (candidates.Count == 0)
            {
                var allowed = links.Keys.Where(id => owners.TryGetValue(id, out var cells) && cells.Count == 1 && wanted.Contains(cells[0])).ToHashSet();
                var starts = allowed.SelectMany(id => new[] { links[id].FromNodeID, links[id].ToNodeID }).Distinct()
                    .Where(n => endpoints.Count == 0 || endpoints.Contains(n)).OrderBy(n => n, StringComparer.Ordinal).ToList();
                var explored = 0;
                foreach (var first in starts)
                {
                    Search(new List<string> { first }, new List<string>(), new HashSet<string> { first });
                    if (explored > 50_000 || candidates.Count > 1) break;
                }
                if (explored > 50_000) { reason = "可行路径搜索过多，无法可靠判定；请先在进路管理中建立对应进路。"; return null; }
                void Search(List<string> ns, List<string> ls, HashSet<string> visited)
                {
                    if (++explored > 50_000 || candidates.Count > 1 || ls.Count > 200) return;
                    if (ls.Count > 0 && (endpoints.Count > 0 ||
                        (adjacent[ns[0]].Count(allowed.Contains) == 1 && adjacent[ns[^1]].Count(allowed.Contains) == 1))) Add(ns, ls);
                    if (!adjacent.TryGetValue(ns[^1], out var nextLinks)) return;
                    foreach (var linkId in nextLinks.Where(allowed.Contains).OrderBy(id => id, StringComparer.Ordinal))
                    {
                        var next = Other(links[linkId], ns[^1]);
                        if (visited.Contains(next) || (ls.Count > 0 && !CanTurn(ns[^2], ns[^1], next, ls[^1], linkId))) continue;
                        if (ls.Count == 0 && type is "Departure" or "Dwelling" && owners[linkId][0] != track) continue;
                        var cell = owners[linkId][0];
                        var traversed = ls.Select(id => owners[id][0]).Distinct().ToList();
                        if (ls.Count > 0 && owners[ls[^1]][0] != cell && traversed.Contains(cell)) continue;
                        if (ls.Count > 0 && type == "Arrival" && owners[ls[^1]][0] == track && cell != track) continue;
                        ns.Add(next); ls.Add(linkId); visited.Add(next);
                        Search(ns, ls, visited);
                        visited.Remove(next); ls.RemoveAt(ls.Count - 1); ns.RemoveAt(ns.Count - 1);
                    }
                }
            }
            if (candidates.Count != 1)
            {
                reason = candidates.Count == 0 ? "找不到满足 Cell 集合、股道方向和道岔转向的连续进路，请先完善站场进路。"
                    : "存在多条不同的可行进路，无法唯一确定运行路径，请先完善站场进路。";
                return null;
            }
            reason = ""; return candidates.Values.Single();
        }

        private void ExtendTrack(List<string> baseNodes, List<string> baseLinks, string track, bool prepend, Action<List<string>, List<string>> add)
        {
            var origin = prepend ? baseNodes[0] : baseNodes[^1];
            var trackLinks = List(Cells[track].LinkIDList).Where(links.ContainsKey).ToHashSet();
            var ns = new List<string> { origin }; var ls = new List<string>();
            void Search()
            {
                if (ls.Count > 100) return;
                if (ls.Count > 0)
                {
                    var combinedNodes = prepend ? ns.Skip(1).Reverse().Concat(baseNodes).ToList() : baseNodes.Concat(ns.Skip(1)).ToList();
                    var combinedLinks = prepend ? ls.AsEnumerable().Reverse().Concat(baseLinks).ToList() : baseLinks.Concat(ls).ToList();
                    var junction = prepend ? ls.Count : baseLinks.Count;
                    if (CanTurn(combinedNodes[junction - 1], combinedNodes[junction], combinedNodes[junction + 1], combinedLinks[junction - 1], combinedLinks[junction]))
                        add(combinedNodes, combinedLinks);
                }
                if (!adjacent.TryGetValue(ns[^1], out var available)) return;
                foreach (var id in available.Where(trackLinks.Contains))
                {
                    var next = Other(links[id], ns[^1]);
                    if (ns.Contains(next) || baseNodes.Contains(next)) continue;
                    if (ls.Count > 0 && !CanTurn(ns[^2], ns[^1], next, ls[^1], id)) continue;
                    ns.Add(next); ls.Add(id); Search(); ls.RemoveAt(ls.Count - 1); ns.RemoveAt(ns.Count - 1);
                }
            }
            Search();
        }

        private bool CanTurn(string previous, string current, string next, string inLink, string outLink)
        {
            if (inLink == outLink || previous == next) return false;
            if (!switches.Contains(current) && (!adjacent.TryGetValue(current, out var edges) || edges.Count <= 2)) return true;
            if (turns.TryGetValue(current, out var known) && known.Count > 0) return known.Contains(TurnKey(inLink, outLink));
            if (!nodes.TryGetValue(previous, out var a) || !nodes.TryGetValue(current, out var b) || !nodes.TryGetValue(next, out var c)) return false;
            // Same-side turnout branches must never be connected by an artificial U-turn.
            var x1 = b.X - a.X; var x2 = c.X - b.X;
            if (Math.Abs(x1) > 1e-9 && Math.Abs(x2) > 1e-9) return x1 * x2 > 0;
            return x1 * x2 + (b.Y - a.Y) * (c.Y - b.Y) > 1e-9;
        }

        public CellOccupancyRoute MakeRoute(string id, string type, Path path, string description) => new()
        {
            ID = id, Type = type, Description = description,
            NodeList = JsonSerializer.Serialize(path.Nodes), LinkList = JsonSerializer.Serialize(path.Links),
            CellList = JsonSerializer.Serialize(path.Cells), StartNodeID = path.Nodes[0], EndNodeID = path.Nodes[^1],
            SwitchList = JsonSerializer.Serialize(path.Nodes.SelectMany(n => catalog.Switches.Where(s => s.BindingNodeID == n).Select(s => s.ID)).Distinct()),
            SignalList = JsonSerializer.Serialize(path.Nodes.SelectMany(n => catalog.Signals.Where(s => s.BindingNodeID == n).Select(s => s.ID)).Distinct())
        };
        private static bool DirectionMatches(Path path, string track, string type) => type == "Arrival" ? path.Cells[^1] == track
            : type == "Departure" ? path.Cells[0] == track : path.Cells.Count == 1 && path.Cells[0] == track;
        private static bool Connects(CellOccupancyLink link, string a, string b) => (link.FromNodeID == a && link.ToNodeID == b) || (link.FromNodeID == b && link.ToNodeID == a);
        private static string Other(CellOccupancyLink link, string node) => link.FromNodeID == node ? link.ToNodeID : link.FromNodeID;
        private static string TurnKey(string a, string b) => string.CompareOrdinal(a, b) < 0 ? a + "|" + b : b + "|" + a;
    }
}
