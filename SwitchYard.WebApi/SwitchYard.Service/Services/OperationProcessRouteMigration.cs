using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SwitchYard.Capacity;
using SwitchYard.Service.Models;

namespace SwitchYard.Service.Services;

/// <summary>One-way upgrade of stored workflow resources. Legacy fields exist only in this reader.</summary>
public static class OperationProcessRouteMigration
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private const string Scope = "InstanceID=@InstanceID AND StationSchemeID=@StationSchemeID";
    private const string Plan = Scope + " AND OperationPlanID=@OperationPlanID";
    public sealed record Report(int Templates, int Snapshots, int Movements, int CreatedRoutes, string? BackupPath);
    private sealed class DocumentRow : ProcessScope
    {
        public string TemplateID { get; set; } = "";
        public string TrainID { get; set; } = "";
        public int Revision { get; set; }
        public string Document { get; set; } = "";
        public DateTime UpdatedAtUtc { get; set; }
    }
    private sealed record DocumentChange(DocumentRow Before, string Document, bool IsTemplate);
    private sealed record MovementChange(MovementRow Before, MovementRow After);

    public static Report Run(DBConnector db, bool apply, string? backupDirectory = null)
    {
        var changes = new List<DocumentChange>();
        var movements = new List<MovementChange>();
        var newRoutes = new List<StationRouteRow>();
        string? backupPath = null;
        db.BeginTransaction();
        try
        {
            var suffix = apply && DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName) ? " FOR UPDATE" : "";
            var templates = db.Query<DocumentRow>("SELECT * FROM operationprocesstemplate" + suffix) ?? [];
            var snapshots = db.Query<DocumentRow>("SELECT * FROM trainprocesssnapshot" + suffix) ?? [];
            foreach (var group in templates.Concat(snapshots).GroupBy(row => (row.InstanceID, row.StationSchemeID)))
            {
                var scope = new ProcessScope { InstanceID = group.Key.InstanceID, StationSchemeID = group.Key.StationSchemeID };
                var routes = db.Query<StationRouteRow>($"SELECT * FROM stationroute WHERE {Scope}" + suffix, scope) ?? [];
                var links = db.Query<StationLinkRow>($"SELECT * FROM link WHERE {Scope}", scope) ?? [];
                var nodes = db.Query<StationNodeRow>($"SELECT * FROM node WHERE {Scope}", scope) ?? [];
                var cells = db.Query<StationCellRow>($"SELECT * FROM cell WHERE {Scope}", scope) ?? [];
                var switches = db.Query<StationSwitchRow>($"SELECT * FROM `switch` WHERE {Scope}", scope) ?? [];
                var signals = db.Query<StationSignalRow>($"SELECT * FROM `signal` WHERE {Scope}", scope) ?? [];
                List<string> RoutesFor(string trackID)
                {
                    var matches = routes.Where(route => Dwelling(route.Type) && IDs(route.LinkList).SequenceEqual([trackID])).Select(route => route.ID!).ToList();
                    if (matches.Count > 0) return matches;
                    var link = links.SingleOrDefault(link => link.ID == trackID);
                    if (link is null || link.FromNodeID == link.ToNodeID ||
                        !nodes.Any(n => n.ID == link.FromNodeID) || !nodes.Any(n => n.ID == link.ToNodeID))
                        throw new InvalidDataException($"{scope.InstanceID}/{scope.StationSchemeID}: 股道 {trackID} 无法映射到停留进路（缺少股道或有效端点）。");
                    // Stable across dry runs, retries and concurrent startup attempts; this is a real station route.
                    var id = "dw-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{scope.InstanceID}/{scope.StationSchemeID}/{trackID}")))[..32].ToLowerInvariant();
                    if (routes.Any(route => route.ID == id)) throw new InvalidDataException("Dwelling route identity collision: " + id);
                    var ends = new[] { link.FromNodeID, link.ToNodeID };
                    var route = new StationRouteRow {
                        InstanceID = scope.InstanceID, StationSchemeID = scope.StationSchemeID, ID = id, Type = "Dwelling",
                        Description = string.IsNullOrWhiteSpace(link.Name) ? $"线路 {trackID} 停留进路" : link.Name.Trim() + (link.Name.Trim().EndsWith('道') ? "" : "道") + "停留进路",
                        StartNodeID = link.FromNodeID, EndNodeID = link.ToNodeID, NodeList = JsonSerializer.Serialize(ends), LinkList = JsonSerializer.Serialize(new[] { trackID }),
                        CellList = JsonSerializer.Serialize(cells.Where(cell => IDs(cell.LinkIDList).Contains(trackID)).Select(cell => cell.ID)),
                        SwitchList = JsonSerializer.Serialize(switches.Where(item => ends.Contains(item.BindingNodeID)).Select(item => item.ID)),
                        SignalList = JsonSerializer.Serialize(signals.Where(item => ends.Contains(item.BindingNodeID)).Select(item => item.ID)),
                        InterruptCellList = "[]", AllowanceTags = "[]", ForbiddenTags = "[]"
                    };
                    routes.Add(route); newRoutes.Add(route);
                    return [id];
                }
                string Unique(string trackID)
                {
                    var ids = RoutesFor(trackID);
                    if (ids.Count != 1) throw new InvalidDataException($"{scope.InstanceID}/{scope.StationSchemeID}: 股道 {trackID} 对应多个停留进路，无法确定执行进路：{string.Join(',', ids)}。");
                    return ids[0];
                }
                void ConvertProcess(JsonObject process)
                {
                    foreach (var activity in Objects(process, "activities"))
                    {
                        if (Dwelling(Text(activity, "type")) && Strings(activity["routeList"]).Count == 0)
                        {
                            var ids = Strings(activity["trackList"]).SelectMany(RoutesFor).Distinct().ToList();
                            activity["routeList"] = JsonSerializer.SerializeToNode(ids);
                            if (Text(activity, "selectedTrack") is { Length: > 0 } selected) activity["selectedRoute"] = Unique(selected);
                        }
                        activity.Remove("trackList"); activity.Remove("selectedTrack");
                    }
                    foreach (var anchor in Objects(process, "anchors"))
                    {
                        if (!anchor.ContainsKey("routeIDs"))
                        {
                            var ids = Text(anchor, "routeID") is { Length: > 0 } routeID ? new List<string> { routeID }
                                : Text(anchor, "trackID") is { Length: > 0 } track ? RoutesFor(track) : new List<string>();
                            anchor["routeIDs"] = JsonSerializer.SerializeToNode(ids);
                        }
                        anchor.Remove("routeID"); anchor.Remove("trackID");
                    }
                    var typed = process.Deserialize<OperationProcessTemplate>(TrainProcessSnapshotStore.JsonOptions)!;
                    var catalog = new ProcessCatalog { Nodes = nodes.Select(node => new ProcessCatalogNode { Id = node.ID }).ToList(), Routes = routes.Select(route => new ProcessCatalogRoute {
                        Id = route.ID!, Name = route.Description ?? "", Type = route.Type ?? "", StartNodeID = route.StartNodeID, EndNodeID = route.EndNodeID
                    }).ToList() };
                    OperationProcessEventNodes.Refresh(typed, catalog);
                    foreach (var ev in Objects(process, "events"))
                        ev["nodeList"] = JsonSerializer.SerializeToNode(typed.Events.Single(item => item.Id == Text(ev, "id")).NodeList);
                }
                foreach (var row in group)
                {
                    var root = JsonNode.Parse(row.Document)!.AsObject();
                    var isTemplate = row.TrainID.Length == 0;
                    var process = isTemplate ? root : root["process"]!.AsObject();
                    var legacy = Objects(process, "activities").Any(a => a.ContainsKey("trackList") || a.ContainsKey("selectedTrack")) ||
                        Objects(process, "anchors").Any(a => a.ContainsKey("trackID")) || root.ContainsKey("selectedTrackIDs");
                    if (!legacy) continue;
                    var oldActivities = Objects(process, "activities").Where(a => Dwelling(Text(a, "type")) && Strings(a["trackList"]).Count > 0)
                        .ToDictionary(a => Text(a, "id"), a => Text(a, "selectedTrack"));
                    ConvertProcess(process);
                    if (isTemplate) root["revision"] = checked(row.Revision + 1);
                    else
                    {
                        var stored = db.Query<MovementRow>($"SELECT * FROM movement WHERE {Plan} AND TrainID=@TrainID" + suffix, row) ?? [];
                        var selectedRoutes = root["selectedRouteIDs"]?.DeepClone()?.AsObject() ?? new JsonObject();
                        var selectedTracks = root["selectedTrackIDs"] as JsonObject;
                        var map = root["activityMovementMap"]!.AsObject();
                        foreach (var activity in Objects(process, "activities"))
                        {
                            var activityID = Text(activity, "id");
                            var movementID = Text(map, activityID);
                            var before = stored.SingleOrDefault(m => m.MovementID == movementID)
                                ?? throw new InvalidDataException($"列车 {row.TrainID} 活动 {activityID} 缺少执行作业 {movementID}。");
                            var current = MovementCellOccupationStore.ResolveRouteID(before);
                            if (current.Length == 0 || current.StartsWith("__process_dwelling__", StringComparison.Ordinal))
                            {
                                var track = selectedTracks is null ? "" : Text(selectedTracks, activityID);
                                if (track.Length == 0) track = oldActivities.GetValueOrDefault(activityID, "");
                                current = track.Length > 0 ? Unique(track) : Text(activity, "selectedRoute");
                                if (current.Length == 0 && Strings(activity["routeList"]) is { Count: 1 } candidates) current = candidates[0];
                            }
                            if (!routes.Any(route => route.ID == current)) throw new InvalidDataException($"列车 {row.TrainID} 活动 {activityID} 缺少有效执行进路。");
                            selectedRoutes[activityID] = current;
                            if (Dwelling(Text(activity, "type")) && (oldActivities.ContainsKey(activityID) || selectedTracks?.ContainsKey(activityID) == true ||
                                before.CellOccupationsJson?.Contains("__process_dwelling__", StringComparison.Ordinal) == true))
                            {
                                if (!routes.Any(route => route.ID == current && Dwelling(route.Type))) throw new InvalidDataException("停留作业执行进路类型不正确：" + current);
                                var after = JsonSerializer.Deserialize<MovementRow>(JsonSerializer.Serialize(before))!;
                                after.Route = current;
                                after.RouteIDList = current;
                                var overrides = MovementCellOccupationOverrides.Read(after.CellOccupationOverridesJson);
                                foreach (var entry in overrides.Values.Where(entry => entry.RouteID.StartsWith("__process_dwelling__", StringComparison.Ordinal))) entry.RouteID = current;
                                if (after.CellOccupationOverridesJson is not null) after.CellOccupationOverridesJson = JsonSerializer.Serialize(overrides, TrainProcessSnapshotStore.JsonOptions);
                                movements.Add(new(before, after));
                            }
                        }
                        root["selectedRouteIDs"] = selectedRoutes;
                        root.Remove("selectedTrackIDs");
                    }
                    changes.Add(new(row, root.ToJsonString(TrainProcessSnapshotStore.JsonOptions), isTemplate));
                }
            }
            if (apply && changes.Count > 0)
            {
                var directory = backupDirectory ?? Path.Combine(AppContext.BaseDirectory, "migration-backups");
                Directory.CreateDirectory(directory);
                backupPath = Path.Combine(directory, $"process-routes-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}.json");
                // Complete backup must reach disk before the first database write.
                using (var file = new FileStream(backupPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    JsonSerializer.Serialize(file, new { templatesAndSnapshots = changes.Select(c => c.Before), movements = movements.Select(c => c.Before), createdRoutes = newRoutes }, Json);
                    file.Flush(true);
                }
                foreach (var route in newRoutes)
                    MustUpdate(db.ExecuteNonQuery(@"INSERT INTO stationroute(InstanceID,StationSchemeID,ID,Type,Description,StartNodeID,EndNodeID,NodeList,LinkList,CellList,SwitchList,SignalList,InterruptCellList,AllowanceTags,ForbiddenTags)
                        VALUES(@InstanceID,@StationSchemeID,@ID,@Type,@Description,@StartNodeID,@EndNodeID,@NodeList,@LinkList,@CellList,@SwitchList,@SignalList,@InterruptCellList,@AllowanceTags,@ForbiddenTags)", route));
                foreach (var planGroup in movements.GroupBy(m => (m.After.InstanceID, m.After.StationSchemeID, m.After.OperationPlanID)))
                {
                    var catalog = new MovementCellOccupationStore(db, planGroup.Key.InstanceID!, planGroup.Key.StationSchemeID!, planGroup.Key.OperationPlanID!);
                    foreach (var change in planGroup)
                    {
                        var after = change.After;
                        var generated = catalog.Generate(after);
                        foreach (var edited in (MovementCellOccupations.Read(change.Before.CellOccupationsJson) ?? []).Where(cell => cell.IsEdited))
                        {
                            if (edited.RouteID.StartsWith("__process_dwelling__", StringComparison.Ordinal)) edited.RouteID = after.Route!;
                            var index = generated.FindIndex(cell => cell.CellID == edited.CellID);
                            if (index < 0) generated.Add(edited); else generated[index] = edited;
                        }
                        after.CellOccupationsJson = MovementCellOccupations.Write(generated);
                        MustUpdate(db.ExecuteNonQuery($"UPDATE movement SET Route=@Route,RouteIDList=@RouteIDList,CellOccupationsJson=@CellOccupationsJson,CellOccupationOverridesJson=@CellOccupationOverridesJson WHERE {Plan} AND TrainID=@TrainID AND MovementID=@MovementID", after));
                    }
                }
                foreach (var change in changes)
                {
                    var row = change.Before;
                    var args = new { row.InstanceID, row.StationSchemeID, row.OperationPlanID, row.TemplateID, row.TrainID, Document = change.Document, Revision = row.Revision + 1, now = DateTime.UtcNow };
                    MustUpdate(db.ExecuteNonQuery(change.IsTemplate
                        ? $"UPDATE operationprocesstemplate SET Document=@Document,Revision=@Revision,UpdatedAtUtc=@now WHERE {Plan} AND TemplateID=@TemplateID"
                        : $"UPDATE trainprocesssnapshot SET Document=@Document WHERE {Plan} AND TrainID=@TrainID", args));
                }
            }
            if (apply) db.Commit(); else db.Rollback();
            return new(changes.Count(c => c.IsTemplate), changes.Count(c => !c.IsTemplate), movements.Count, newRoutes.Count, backupPath);
        }
        catch { db.Rollback(); throw; }
    }
    private static void MustUpdate(int count) { if (count != 1) throw new InvalidDataException("Workflow migration encountered duplicate or missing identities."); }
    private static bool Dwelling(string? type) => type?.Trim().ToUpperInvariant() is "DWELLING" or "停留" or "停留进路";
    private static List<string> IDs(string? text) => StationCapacityInputBuilder.ParseList(text);
    private static string Text(JsonObject node, string key) => node[key]?.GetValue<string>() ?? "";
    private static List<string> Strings(JsonNode? node) => node is JsonArray array ? array.Select(n => n!.GetValue<string>()).ToList() : [];
    private static IEnumerable<JsonObject> Objects(JsonObject node, string key) => (node[key] as JsonArray ?? []).Select(n => n!.AsObject());
}
