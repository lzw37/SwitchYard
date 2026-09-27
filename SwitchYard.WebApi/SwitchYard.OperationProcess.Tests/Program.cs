using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwitchYard.Capacity;
using SwitchYard.Service;
using SwitchYard.Service.Controllers;
using SwitchYard.Service.Models;
using SwitchYard.Service.Services;
using SwitchYard.Service.Utils;

// This executable hosts the real controllers on an ephemeral loopback port. Authentication is
// substituted only in this test host; every persistence operation targets a new temporary DB.
var testRoot = Path.Combine(Path.GetTempPath(), $"switchyard-operation-process-{Guid.NewGuid():N}");
Directory.CreateDirectory(testRoot);
var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
var checks = 0;
WebApplication? app = null;
HttpClient? client = null;
const string scope = "instanceID=scope-a&stationSchemeID=scheme-a&operationPlanID=plan-a";
try
{
    DBConnector.SetConfiguration(new ConfigurationBuilder().AddInMemoryCollection(
        new Dictionary<string, string?>
        {
            ["CapacityDatabase:DatabaseType"] = "SQLite",
            ["CapacityDatabase:SqlliteConfig:DatabaseFile"] = Path.Combine(testRoot, "capacity.db")
        }).Build());
    SeedDatabase();
    (app, client) = await StartHost();
    await TestAccessAndCatalog();
    await TestAggregateAndValidation();
    await TestEntityCrud();
    await TestFixedActivityEndpointsAndNamedDwellingTracks();
    await TestActivityRenameSynchronizesEventNames();
    await TestConcurrentWrites();
    await TestOperationPlanLifecycle();
    await TestSchemeTemplateOwnershipAndMigration();
    await TestEventNodeListsDerivedFromCandidateRoutes();
    await TestLegacyGenerationProjectsSecondsIntoMinutes();
    await TestGenerateTrainTemplateFromSavedProcess();
    checks += SchedulerConstraintChecks.Run();
    await TestGenerateActualTrainsFromProcess();
    await TestGeneratedProcessEventsAreEarliest();
    checks += await TrainBatchDeletionChecks.Run(client!);
    checks += await CellOccupancyImportChecks.Run(client!);

    // Restart the HTTP host to prove data is read back from SQLite rather than controller memory.
    client.Dispose();
    await app.DisposeAsync();
    (app, client) = await StartHost();
    var reloaded = await GetTemplate("persisted");
    Assert(reloaded.Name == "已更新的模板" && reloaded.Revision == 2,
        "template and revision survive host restart");
    await Request(HttpMethod.Delete, $"DeleteTemplate?{scope}&templateID=persisted&revision=1",
        expected: HttpStatusCode.Conflict);
    await Request(HttpMethod.Delete, $"DeleteTemplate?{scope}&templateID=persisted&revision=2", expected: HttpStatusCode.NoContent);
    await Request(HttpMethod.Get, $"GetTemplate?{scope}&templateID=persisted",
        expected: HttpStatusCode.NotFound);
    var list = await Request(HttpMethod.Get, $"GetTemplates?{scope}");
    Assert(list!.AsArray().All(item => item!["id"]!.GetValue<string>() != "persisted"),
        "deleted template absent from list");
    Console.WriteLine($"Operation process HTTP/SQLite integration tests passed ({checks} assertions).");
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception);
    Environment.ExitCode = 1;
}
finally
{
    client?.Dispose();
    if (app is not null) await app.DisposeAsync();
    SqliteConnection.ClearAllPools();
    if (Directory.Exists(testRoot)) Directory.Delete(testRoot, recursive: true);
}

async Task<(WebApplication, HttpClient)> StartHost()
{
    var builder = WebApplication.CreateBuilder(new WebApplicationOptions
    {
        EnvironmentName = "Testing", ContentRootPath = testRoot
    });
    builder.WebHost.UseUrls("http://127.0.0.1:0");
    builder.Logging.ClearProviders();
    builder.Services.AddControllers().AddApplicationPart(typeof(OperationPlanController).Assembly);
    builder.Services.AddSingleton<SnowflakeIdGenerator>();
    builder.Services.AddAuthentication(TestAuthentication.SchemeName)
        .AddScheme<AuthenticationSchemeOptions, TestAuthentication>(TestAuthentication.SchemeName, _ => { });
    builder.Services.AddAuthorization();
    var host = builder.Build();
    host.UseAuthentication();
    host.UseAuthorization();
    host.MapControllers();
    await host.StartAsync();
    var address = host.Services.GetRequiredService<IServer>().Features
        .Get<IServerAddressesFeature>()!.Addresses.Single();
    return (host, new HttpClient { BaseAddress = new Uri($"{address}/OperationProcess/") });
}

void SeedDatabase()
{
    var db = DBConnector.GetDBConnector(DBConnector.CapacityDatabaseSectionName);
    var schema = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Database", "capacity-sqlite-schema.sql"));
    for (var pass = 0; pass < 2; pass++)
        foreach (var statement in schema.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            db.ExecuteNonQuery(statement);
    db.ExecuteNonQuery("INSERT INTO capacityinstance (ID, Name, Owner, IsActive) VALUES " +
        "('scope-a','A','owner',1),('scope-b','B','someone-else',1)");
    db.ExecuteNonQuery("INSERT INTO stationscheme (InstanceID,ID,Name) VALUES " +
        "('scope-a','scheme-a','A'),('scope-a','foreign-scheme','Foreign'),('scope-b','scheme-a','B')");
    db.ExecuteNonQuery("INSERT INTO operationplan (InstanceID,StationSchemeID,OperationPlanID,Name) VALUES " +
        "('scope-a','scheme-a','plan-a','A'),('scope-a','foreign-scheme','plan-a','Foreign')," +
        "('scope-b','scheme-a','plan-a','B')");
    db.ExecuteNonQuery("INSERT INTO node (InstanceID,StationSchemeID,ID,X,Y) VALUES " +
        "('scope-a','scheme-a',1,0,0),('scope-a','scheme-a',2,100,0),('scope-a','scheme-a',3,200,0)," +
        "('scope-a','foreign-scheme',999,999,999)");
    db.ExecuteNonQuery("INSERT INTO link (InstanceID,StationSchemeID,ID,Name,FromNodeID,ToNodeID) VALUES " +
        "('scope-a','scheme-a',10,'1道',1,2),('scope-a','scheme-a',20,'2道',2,3)," +
        "('scope-a','scheme-a',30,NULL,1,2),('scope-a','scheme-a',40,'',1,2),('scope-a','scheme-a',50,'   ',1,2)," +
        "('scope-a','foreign-scheme',999,'其他站场',999,999)");
    db.ExecuteNonQuery("INSERT INTO stationroute " +
        "(InstanceID,StationSchemeID,ID,Type,Description,NodeList,LinkList,StartNodeID,EndNodeID) VALUES " +
        "('scope-a','scheme-a','arrival-1','arrival','接车','1,2','10','1','2')," +
        "('scope-a','scheme-a','departure-1','发车','发车','2,3','20','2','3')," +
        "('scope-a','scheme-a','shunting-1','Shunting','调车','1,2','10','1','2')," +
        "('scope-a','scheme-a','locomotive-1','Locomotive','机车','1,2','50','1','2')," +
        "('scope-a','foreign-scheme','foreign-route','Arrival','其他站场','999','999','999','999')");
}

async Task TestAccessAndCatalog()
{
    await Request(HttpMethod.Get, $"GetTemplates?{scope}", user: null, expected: HttpStatusCode.Unauthorized);
    await Request(HttpMethod.Get, $"GetTemplates?{scope}", user: "outsider", expected: HttpStatusCode.Forbidden);
    await Request(HttpMethod.Get, $"GetTemplates?{scope}", user: "admin");
    await Request(HttpMethod.Get, $"GetTemplates?{scope}", user: "multi-role-admin");
    await Request(HttpMethod.Get, "GetTemplates", expected: HttpStatusCode.BadRequest);
    await Request(HttpMethod.Get, "GetTemplates?instanceID=scope-a&stationSchemeID=missing&operationPlanID=plan-a",
        expected: HttpStatusCode.NotFound);
    await Request(HttpMethod.Get, "GetTemplates?instanceID=scope-a&stationSchemeID=scheme-a&operationPlanID=missing");
    await Request(HttpMethod.Get, "GetTemplates?instanceID=scope-a&stationSchemeID=scheme-a");
    var catalog = await Request(HttpMethod.Get, $"GetCatalog?{scope}");
    Assert(catalog!["nodes"]!.AsArray().Count == 3, "catalog contains only nodes in the selected scheme");
    Assert(catalog["tracks"]!.AsArray().Count == 5, "catalog preserves every track in the selected scheme");
    foreach (var trackID in new[] { "30", "40", "50" })
        Assert(catalog["tracks"]!.AsArray().Single(x => x!["id"]!.GetValue<string>() == trackID)!["name"]!.GetValue<string>() == "",
            $"track {trackID} with null, empty, or whitespace source name retains an empty catalog name");
    Assert(catalog["routes"]!.AsArray().Count == 4, "catalog contains all four movement route types");
    Assert(catalog["routes"]!.AsArray().Single(x => x!["id"]!.GetValue<string>() == "arrival-1")!["type"]!.GetValue<string>() == "Arrival",
        "lowercase legacy route type is normalized");
    Assert(catalog["routes"]!.AsArray().Single(x => x!["id"]!.GetValue<string>() == "departure-1")!["type"]!.GetValue<string>() == "Departure",
        "Chinese legacy route type is normalized");
    var list = await Request(HttpMethod.Get, $"GetTemplates?{scope}");
    Assert(list!.AsArray().Count == 0, "new scope starts without templates");
}

async Task TestAggregateAndValidation()
{
    var candidate = BuildTemplate("persisted");
    await Request(HttpMethod.Post, "CreateTemplate", candidate, user: "outsider", expected: HttpStatusCode.Forbidden);
    var created = await Request(HttpMethod.Post, "CreateTemplate", candidate);
    var saved = created!.Deserialize<OperationProcessTemplate>(jsonOptions)!;
    Assert(saved.Revision == 1, "first persisted revision is one");
    Assert(saved.Activities.Count == 3 && saved.Events.Count == 6 && saved.Precedences.Count == 2 && saved.Anchors.Count == 2,
        "all four object families round trip");
    Assert(saved.RouteAnchors.Single().StartAnchor is null && saved.RouteAnchors.Single().EndAnchor is null,
        "both route endpoint anchors may remain unassigned");
    var duplicate = await Request(HttpMethod.Post, "CreateTemplate", candidate, expected: HttpStatusCode.Conflict);
    Assert(duplicate is not null, "duplicate template creation returns a useful conflict");
    saved.Name = "已更新的模板";
    var updated = (await Request(HttpMethod.Put, "UpdateTemplate", saved))!.Deserialize<OperationProcessTemplate>(jsonOptions)!;
    Assert(updated.Revision == 2, "update increments revision");
    saved.Name = "旧版本不应覆盖";
    await Request(HttpMethod.Put, "UpdateTemplate", saved, expected: HttpStatusCode.Conflict);
    Assert((await GetTemplate("persisted")).Name == "已更新的模板", "stale update leaves saved template intact");
    await Request(HttpMethod.Get,
        "GetTemplate?instanceID=scope-a&stationSchemeID=foreign-scheme&operationPlanID=plan-a&templateID=persisted",
        expected: HttpStatusCode.NotFound);

    await Invalid(t => t.Activities[0].MinDuration = -1, "negative duration");
    await Invalid(t => t.Activities[0].MaxDuration = 1, "maximum duration below minimum");
    await Invalid(t => t.Activities[0].Type = "Teleport", "unknown activity type");
    await Invalid(t => t.Activities[0].StartEvent = "missing", "activity references missing event");
    await Invalid(t => t.Activities[0].SelectedRoute = "departure-1", "selected route not among alternatives");
    await Invalid(t => t.Activities[0].RouteList = ["departure-1"], "route type incompatible with activity");
    await Invalid(t => { t.Activities[0].RouteList = ["foreign-route"]; t.Activities[0].SelectedRoute = "foreign-route"; },
        "route from another station scheme");
    await Invalid(t => t.Activities[1].SelectedTrack = "20", "selected track not among alternatives");
    await Invalid(t => { t.Activities[1].TrackList = ["999"]; t.Activities[1].SelectedTrack = "999"; },
        "track from another station scheme");
    await Invalid(t => t.Events[0].NodeID = "999", "node from another station scheme");
    await Invalid(t => t.Events[0].SelectedAnchor = "a20", "selected anchor not among alternatives");
    await Invalid(t => t.Events[0].AnchorList = ["missing"], "missing alternative anchor");
    await Invalid(t => { t.Events[0].AnchorList = ["a20"]; t.Events[0].SelectedAnchor = "a20"; },
        "selected anchor track does not touch event node");
    await Invalid(t => { t.Events[0].NodeList = ["2"]; t.Events[0].AnchorList = ["a20"]; t.Events[0].SelectedAnchor = "a20"; },
        "forged node candidates cannot authorize an anchor disconnected from actual route candidates");
    await Invalid(t => t.Anchors[0].TrackID = "999", "anchor track belongs to another scheme");
    await Invalid(t => t.Precedences[0].FollowingEvent = "missing", "precedence references missing event");
    await Invalid(t => t.Precedences[0].Interval = -1, "negative precedence interval");
    await Invalid(t => t.Precedences.Add(new ProcessPrecedence
        { Id = "cycle", LeadingEvent = "departure-end", FollowingEvent = "arrival-start", Interval = 0 }),
        "cycle spanning activity and precedence edges");
    await Invalid(t => t.RouteAnchors[0].StartAnchor = "a20", "route anchor is not on its endpoint");
    await Invalid(t => t.RouteAnchors[0].EndAnchor = "missing", "route references nonexistent anchor");
    await Invalid(t => t.Events.Add(new ProcessEvent { Id = "arrival-start" }), "duplicate event ID");
    await Invalid(t => t.Events[1].Time = 1, "fixed event times violate activity minimum");
    await Invalid(t => t.Precedences[0].Interval = 20, "fixed event times violate precedence minimum");

    var malformed = JsonSerializer.SerializeToNode(updated, jsonOptions)!;
    malformed["events"] = null;
    await Request(HttpMethod.Put, "UpdateTemplate", malformed, expected: HttpStatusCode.BadRequest);
    malformed = JsonSerializer.SerializeToNode(updated, jsonOptions)!;
    malformed["events"]!.AsArray().Add((JsonNode?)null);
    await Request(HttpMethod.Put, "UpdateTemplate", malformed, expected: HttpStatusCode.BadRequest);
    Assert((await GetTemplate("persisted")).Revision == 2, "all invalid mutations are atomic and leave revision unchanged");

    async Task Invalid(Action<OperationProcessTemplate> mutate, string name)
    {
        var invalid = JsonSerializer.Deserialize<OperationProcessTemplate>(JsonSerializer.Serialize(updated), jsonOptions)!;
        mutate(invalid);
        var result = await Request(HttpMethod.Put, "UpdateTemplate", invalid, expected: HttpStatusCode.BadRequest);
        Assert(result?["errors"]?.AsArray().Count > 0, $"{name} yields validation details");
    }
}

async Task TestEntityCrud()
{
    var template = new OperationProcessTemplate
    {
        Id = "entity-crud", Name = "单对象增删改查", InstanceID = "scope-a", StationSchemeID = "scheme-a", OperationPlanID = "plan-a"
    };
    template = (await Request(HttpMethod.Post, "CreateTemplate", template))!.Deserialize<OperationProcessTemplate>(jsonOptions)!;
    var id = template.Id;

    async Task Mutate(HttpMethod method, string collection, string? entityID, object? entity)
    {
        var route = $"{id}/{collection}{(entityID is null ? "" : "/" + entityID)}?{scope}&revision={template.Revision}";
        template = (await Request(method, route, entity))!.Deserialize<OperationProcessTemplate>(jsonOptions)!;
    }

    await Mutate(HttpMethod.Post, "anchors", null, new ProcessAnchor { Id = "free-anchor", Name = "锚", TrackID = "10" });
    var anchor = await Request(HttpMethod.Get, $"{id}/anchors/free-anchor?{scope}");
    Assert(anchor!["trackID"]!.GetValue<string>() == "10", "anchor GET returns its track");
    await Mutate(HttpMethod.Put, "anchors", "free-anchor", new ProcessAnchor { Id = "free-anchor", Name = "已编辑的锚", TrackID = "10" });
    Assert(template.Anchors.Single().Name == "已编辑的锚", "anchor update persists");

    await Mutate(HttpMethod.Post, "events", null, new ProcessEvent { Id = "start", Name = "开始", NodeID = "1" });
    await Mutate(HttpMethod.Post, "events", null, new ProcessEvent { Id = "end", Name = "结束", NodeID = "2" });
    await Mutate(HttpMethod.Post, "events", null, new ProcessEvent { Id = "temporary", Name = "临时事件", NodeID = "1" });
    await Mutate(HttpMethod.Delete, "events", "temporary", null);
    Assert(template.Events.Count == 2, "unreferenced event deletion persists");
    await Mutate(HttpMethod.Put, "events", "start", new ProcessEvent
        { Id = "start", Name = "起点", NodeID = "1", AnchorList = ["free-anchor"], SelectedAnchor = "free-anchor" });
    var eventResult = await Request(HttpMethod.Get, $"{id}/events/start?{scope}");
    Assert(eventResult!["selectedAnchor"]!.GetValue<string>() == "free-anchor", "event update and GET preserve anchor selection");
    await Mutate(HttpMethod.Post, "activities", null, new ProcessActivity
        { Id = "activity", Name = "接车", Type = "Arrival", StartEvent = "start", EndEvent = "end", MinDuration = 1, MaxDuration = 10,
            RouteList = ["arrival-1"], SelectedRoute = "arrival-1" });
    var activity = template.Activities.Single();
    activity.Name = "已编辑的活动";
    activity.MaxDuration = 15;
    await Mutate(HttpMethod.Put, "activities", "activity", activity);
    var activityResult = await Request(HttpMethod.Get, $"{id}/activities/activity?{scope}");
    Assert(activityResult!["name"]!.GetValue<string>() == "已编辑的活动", "activity update and GET");
    await Mutate(HttpMethod.Post, "precedences", null, new
        { id = "order", leadingEvent = "start", followingEvent = "end", intervel = 2 });
    Assert(template.Precedences.Single().Interval == 2, "legacy Intervel spelling is accepted and canonicalized");
    await Mutate(HttpMethod.Put, "precedences", "order", new ProcessPrecedence
        { Id = "order", LeadingEvent = "start", FollowingEvent = "end", Interval = 3 });
    var precedence = await Request(HttpMethod.Get, $"{id}/precedences/order?{scope}");
    Assert(precedence!["interval"]!.GetValue<double>() == 3, "precedence update and GET");
    foreach (var collection in new[] { "activities", "events", "precedences", "anchors" })
    {
        var objects = await Request(HttpMethod.Get, $"{id}/{collection}?{scope}");
        Assert(objects!.AsArray().Count > 0, $"{collection} list endpoint returns objects");
    }

    await Request(HttpMethod.Put, $"{id}/anchors/free-anchor?{scope}&revision=1",
        new ProcessAnchor { Id = "free-anchor", Name = "锚", TrackID = "10" }, expected: HttpStatusCode.Conflict);
    await Request(HttpMethod.Put, $"{id}/events/start?{scope}&revision={template.Revision}",
        new ProcessEvent { Id = "different-id" }, expected: HttpStatusCode.BadRequest);
    await Request(HttpMethod.Get, $"{id}/activities/missing?{scope}", expected: HttpStatusCode.NotFound);
    await Request(HttpMethod.Get, $"{id}/unknown?{scope}", expected: HttpStatusCode.BadRequest);
    await Request(HttpMethod.Get, $"{id}/events?{scope}", user: "outsider", expected: HttpStatusCode.Forbidden);
    await Request(HttpMethod.Delete, $"{id}/events/start?{scope}&revision={template.Revision}", expected: HttpStatusCode.BadRequest);

    await Mutate(HttpMethod.Delete, "precedences", "order", null);
    Assert(template.Precedences.Count == 0, "precedence deletion persists");
    await Mutate(HttpMethod.Delete, "activities", "activity", null);
    Assert(template.Activities.Count == 0, "activity deletion persists");
    Assert(template.Events.Count == 0, "activity deletion removes its unused endpoint events");
    await Mutate(HttpMethod.Delete, "anchors", "free-anchor", null);
    Assert(template.Anchors.Count == 0, "anchor deletion persists");
    await Request(HttpMethod.Delete, $"DeleteTemplate?{scope}&templateID={id}&revision={template.Revision}", expected: HttpStatusCode.NoContent);
}

async Task TestFixedActivityEndpointsAndNamedDwellingTracks()
{
    var fixture = new OperationProcessTemplate
    {
        Id = "fixed-endpoints", Name = "固定活动端点", InstanceID = "scope-a", StationSchemeID = "scheme-a", OperationPlanID = "plan-a",
        Anchors =
        [
            new() { Id = "named-anchor", Name = "有名称轨道锚", TrackID = "10" },
            new() { Id = "null-name-anchor", Name = "空名称轨道锚", TrackID = "30" },
            new() { Id = "empty-name-anchor", Name = "空字符串轨道锚", TrackID = "40" },
            new() { Id = "blank-name-anchor", Name = "空白名称轨道锚", TrackID = "50" }
        ],
        Events =
        [
            new() { Id = "start", Name = "活动开始", NodeID = "1" },
            new() { Id = "end", Name = "活动结束", NodeID = "2" },
            new() { Id = "other-start", Name = "另一活动开始", NodeID = "1" },
            new() { Id = "other-end", Name = "另一活动结束", NodeID = "2" },
            new() { Id = "standalone-start", Name = "独立开始事件", NodeID = "1" },
            new() { Id = "standalone-end", Name = "独立结束事件", NodeID = "2" },
            new() { Id = "movement-start", Name = "机车开始", NodeID = "1" },
            new() { Id = "movement-end", Name = "机车结束", NodeID = "2" }
        ],
        Activities =
        [
            new() { Id = "fixed", Name = "固定端点活动", Type = "Dwelling", StartEvent = "start", EndEvent = "end",
                MinDuration = 0, MaxDuration = 10, TrackList = ["10"], SelectedTrack = "10" },
            new() { Id = "other", Name = "另一活动", Type = "Dwelling", StartEvent = "other-start", EndEvent = "other-end",
                MinDuration = 0, MaxDuration = 10, TrackList = ["10"], SelectedTrack = "10" },
            new() { Id = "unnamed-route-movement", Name = "经过未命名轨道", Type = "Locomotive", StartEvent = "movement-start", EndEvent = "movement-end",
                MinDuration = 0, MaxDuration = 10, RouteList = ["locomotive-1"], SelectedRoute = "locomotive-1" }
        ]
    };
    var saved = (await Request(HttpMethod.Post, "CreateTemplate", fixture))!
        .Deserialize<OperationProcessTemplate>(jsonOptions)!;
    Assert(saved.Anchors.Count == 4, "anchors may reference tracks without genuine names");
    Assert(saved.Activities.Single(a => a.Id == "unnamed-route-movement").SelectedRoute == "locomotive-1",
        "movement routes may pass through tracks without genuine names");

    foreach (var targetPrefix in new[] { "other", "standalone" })
        foreach (var endpoint in new[] { "start", "end" })
        {
            var attempted = Clone(saved);
            var activity = attempted.Activities.Single(a => a.Id == "fixed");
            if (endpoint == "start") activity.StartEvent = $"{targetPrefix}-start";
            else activity.EndEvent = $"{targetPrefix}-end";
            // All referenced nodes, duration ranges and graph edges remain valid. The rejection
            // must come from changing the endpoint association of an already-created activity.
            await Request(HttpMethod.Put, "UpdateTemplate", attempted, expected: HttpStatusCode.BadRequest);
            await Request(HttpMethod.Put, $"{saved.Id}/activities/fixed?{scope}&revision={saved.Revision}",
                activity, expected: HttpStatusCode.BadRequest);
            var unchanged = await GetTemplate(saved.Id);
            Assert(JsonSerializer.Serialize(unchanged, jsonOptions) == JsonSerializer.Serialize(saved, jsonOptions),
                $"reassigning {endpoint} to {targetPrefix} event leaves all persisted fields and revision unchanged");
        }

    foreach (var trackID in new[] { "30", "40", "50" })
    {
        var invalidCreation = BuildTemplate($"unnamed-dwelling-{trackID}");
        invalidCreation.Activities.Single(a => a.Type == "Dwelling").TrackList.Add(trackID);
        await Request(HttpMethod.Post, "CreateTemplate", invalidCreation, expected: HttpStatusCode.BadRequest);
        await Request(HttpMethod.Get, $"GetTemplate?{scope}&templateID={invalidCreation.Id}", expected: HttpStatusCode.NotFound);

        var invalidUpdate = Clone(saved);
        var dwelling = invalidUpdate.Activities.Single(a => a.Id == "fixed");
        dwelling.TrackList.Add(trackID);
        await Request(HttpMethod.Put, "UpdateTemplate", invalidUpdate, expected: HttpStatusCode.BadRequest);
        dwelling.TrackList = [trackID];
        dwelling.SelectedTrack = trackID;
        await Request(HttpMethod.Put, $"{saved.Id}/activities/fixed?{scope}&revision={saved.Revision}",
            dwelling, expected: HttpStatusCode.BadRequest);
    }
    var afterInvalidDwelling = await GetTemplate(saved.Id);
    Assert(JsonSerializer.Serialize(afterInvalidDwelling, jsonOptions) == JsonSerializer.Serialize(saved, jsonOptions),
        "unnamed dwelling candidates and selections are rejected atomically");

    var editedStart = new ProcessEvent
    {
        Id = "start", Name = "允许修改的事件属性", Time = 1, NodeID = "2",
        AnchorList = ["named-anchor", "null-name-anchor"], SelectedAnchor = "null-name-anchor"
    };
    saved = (await Request(HttpMethod.Put, $"{saved.Id}/events/start?{scope}&revision={saved.Revision}", editedStart))!
        .Deserialize<OperationProcessTemplate>(jsonOptions)!;
    Assert(saved.Revision == 2 && saved.Events.Single(e => e.Id == "start").NodeID == "2" &&
           saved.Events.Single(e => e.Id == "start").SelectedAnchor == "null-name-anchor",
        "event entity PUT can edit name, time, node and anchor properties without reassigning activity endpoints");
    var editedEnd = saved.Events.Single(e => e.Id == "end");
    editedEnd.Name = "整体更新的结束事件";
    editedEnd.Time = 5;
    editedEnd.NodeID = "1";
    editedEnd.AnchorList = ["blank-name-anchor"];
    editedEnd.SelectedAnchor = "blank-name-anchor";
    saved = (await Request(HttpMethod.Put, "UpdateTemplate", saved))!
        .Deserialize<OperationProcessTemplate>(jsonOptions)!;
    var editable = await GetTemplate(saved.Id);
    Assert(editable.Revision == 3 && editable.Events.Single(e => e.Id == "start").Name == editedStart.Name &&
           editable.Events.Single(e => e.Id == "start").Time == 1 &&
           editable.Events.Single(e => e.Id == "end").Name == editedEnd.Name &&
           editable.Events.Single(e => e.Id == "end").Time == 5 &&
           editable.Events.Single(e => e.Id == "end").NodeID == "1" &&
           editable.Events.Single(e => e.Id == "end").SelectedAnchor == "blank-name-anchor" &&
           editable.Activities.Single(a => a.Id == "fixed").StartEvent == "start" &&
           editable.Activities.Single(a => a.Id == "fixed").EndEvent == "end",
        "aggregate PUT edits event properties while preserving the activity's original endpoint IDs");
    await Request(HttpMethod.Delete, $"DeleteTemplate?{scope}&templateID={saved.Id}&revision={saved.Revision}", expected: HttpStatusCode.NoContent);

    OperationProcessTemplate Clone(OperationProcessTemplate template) =>
        JsonSerializer.Deserialize<OperationProcessTemplate>(JsonSerializer.Serialize(template, jsonOptions), jsonOptions)!;
}

async Task TestActivityRenameSynchronizesEventNames()
{
    var fixture = BuildTemplate("rename-events");
    foreach (var ev in fixture.Events) ev.Name = $"手工命名：{ev.Id}";
    fixture.Events.Single(e => e.Id == "arrival-end").AnchorList = ["a10", "a20"];
    fixture.Events.Single(e => e.Id == "arrival-end").SelectedAnchor = "a20";
    var expected = Clone(fixture);
    expected.Revision = 1;
    expected.OperationPlanID = "";
    var saved = await AssertResponseAndReload(
        await Request(HttpMethod.Post, "CreateTemplate", fixture), expected,
        "creation preserves supplied event names and properties");

    var aggregateRename = Clone(saved);
    aggregateRename.Activities.Single(a => a.Id == "arrival").Name = "重新命名的接车活动";
    expected = ExpectedRename(aggregateRename, "arrival");
    saved = await AssertResponseAndReload(
        await Request(HttpMethod.Put, "UpdateTemplate", aggregateRename), expected,
        "aggregate activity rename synchronizes exactly its two endpoint names without changing event IDs, references, properties, precedences or other activities");

    var entityRename = Clone(saved).Activities.Single(a => a.Id == "arrival");
    entityRename.Name = "单活动接口改名";
    var expectedEntityRename = Clone(saved);
    expectedEntityRename.Activities.Single(a => a.Id == "arrival").Name = entityRename.Name;
    expected = ExpectedRename(expectedEntityRename, "arrival");
    saved = await AssertResponseAndReload(
        await Request(HttpMethod.Put, $"{saved.Id}/activities/arrival?{scope}&revision={saved.Revision}", entityRename), expected,
        "activity entity PUT uses activity-name plus 开始/结束 and preserves every other stored field");

    foreach (var eventID in new[] { "arrival-start", "arrival-end" })
    {
        var manuallyNamedEvent = Clone(saved).Events.Single(e => e.Id == eventID);
        manuallyNamedEvent.Name = $"用户自定义：{eventID}";
        expected = Clone(saved);
        expected.Events.Single(e => e.Id == eventID).Name = manuallyNamedEvent.Name;
        expected.Revision++;
        saved = await AssertResponseAndReload(
            await Request(HttpMethod.Put, $"{saved.Id}/events/{eventID}?{scope}&revision={saved.Revision}", manuallyNamedEvent), expected,
            $"manual event name {eventID} can be persisted after an activity rename");
    }

    var unrelatedSave = Clone(saved);
    unrelatedSave.Description = "只修改模板说明";
    expected = Clone(unrelatedSave);
    expected.Revision++;
    saved = await AssertResponseAndReload(
        await Request(HttpMethod.Put, "UpdateTemplate", unrelatedSave), expected,
        "unrelated aggregate save keeps custom event names when the activity name is unchanged");

    var sameNameActivity = Clone(saved).Activities.Single(a => a.Id == "arrival");
    sameNameActivity.MaxDuration = 8;
    expected = Clone(saved);
    expected.Activities.Single(a => a.Id == "arrival").MaxDuration = 8;
    expected.Revision++;
    saved = await AssertResponseAndReload(
        await Request(HttpMethod.Put, $"{saved.Id}/activities/arrival?{scope}&revision={saved.Revision}", sameNameActivity), expected,
        "activity update with the same name keeps custom event names");

    var invalidRename = Clone(saved);
    var invalidActivity = invalidRename.Activities.Single(a => a.Id == "arrival");
    invalidActivity.Name = "不能持久化的改名";
    invalidActivity.MinDuration = -1;
    await Request(HttpMethod.Put, "UpdateTemplate", invalidRename, expected: HttpStatusCode.BadRequest);
    Assert(Canonical(await GetTemplate(saved.Id)) == Canonical(saved),
        "failed aggregate rename leaves event names, activity name, other data and revision unchanged");
    await Request(HttpMethod.Put, $"{saved.Id}/activities/arrival?{scope}&revision={saved.Revision}", invalidActivity,
        expected: HttpStatusCode.BadRequest);
    Assert(Canonical(await GetTemplate(saved.Id)) == Canonical(saved),
        "failed activity entity rename leaves the complete persisted document unchanged");

    var staleRename = Clone(saved);
    staleRename.Revision--;
    staleRename.Activities.Single(a => a.Id == "arrival").Name = "过期版本改名";
    await Request(HttpMethod.Put, "UpdateTemplate", staleRename, expected: HttpStatusCode.Conflict);
    Assert(Canonical(await GetTemplate(saved.Id)) == Canonical(saved),
        "stale rename cannot overwrite manually named events");

    var tooLongRename = Clone(saved);
    tooLongRename.Activities.Single(a => a.Id == "arrival").Name = new string('长', 200);
    await Request(HttpMethod.Put, "UpdateTemplate", tooLongRename, expected: HttpStatusCode.BadRequest);
    await Request(HttpMethod.Put, $"{saved.Id}/activities/arrival?{scope}&revision={saved.Revision}",
        tooLongRename.Activities.Single(a => a.Id == "arrival"), expected: HttpStatusCode.BadRequest);
    Assert(Canonical(await GetTemplate(saved.Id)) == Canonical(saved),
        "200-character activity rename is rejected before derived event names exceed 200 characters, with no stored changes");

    var longestRename = Clone(saved);
    longestRename.Activities.Single(a => a.Id == "arrival").Name = new string('长', 198);
    expected = ExpectedRename(longestRename, "arrival");
    saved = await AssertResponseAndReload(
        await Request(HttpMethod.Put, "UpdateTemplate", longestRename), expected,
        "198-character activity rename is accepted without truncating the generated names");
    Assert(saved.Events.Single(e => e.Id == "arrival-start").Name.Length == 200 &&
           saved.Events.Single(e => e.Id == "arrival-end").Name.Length == 200,
        "generated endpoint names satisfy the 200-character limit exactly");
    await Request(HttpMethod.Delete, $"DeleteTemplate?{scope}&templateID={saved.Id}&revision={saved.Revision}",
        expected: HttpStatusCode.NoContent);

    OperationProcessTemplate ExpectedRename(OperationProcessTemplate renamed, string activityID)
    {
        var result = Clone(renamed);
        var activity = result.Activities.Single(a => a.Id == activityID);
        result.Events.Single(e => e.Id == activity.StartEvent).Name = $"{activity.Name}开始";
        result.Events.Single(e => e.Id == activity.EndEvent).Name = $"{activity.Name}结束";
        result.Revision++;
        return result;
    }

    async Task<OperationProcessTemplate> AssertResponseAndReload(JsonNode? response, OperationProcessTemplate expectedTemplate, string name)
    {
        var actual = response!.Deserialize<OperationProcessTemplate>(jsonOptions)!;
        Assert(Canonical(actual) == Canonical(expectedTemplate), $"{name}: response");
        var reloaded = await GetTemplate(expectedTemplate.Id);
        Assert(Canonical(reloaded) == Canonical(expectedTemplate), $"{name}: persisted reload");
        return reloaded;
    }

    // Entity PUT may change collection order; compare all field values by stable object IDs.
    string Canonical(OperationProcessTemplate template)
    {
        var ordered = Clone(template);
        ordered.Activities = ordered.Activities.OrderBy(a => a.Id, StringComparer.Ordinal).ToList();
        ordered.Events = ordered.Events.OrderBy(e => e.Id, StringComparer.Ordinal).ToList();
        ordered.Precedences = ordered.Precedences.OrderBy(p => p.Id, StringComparer.Ordinal).ToList();
        ordered.Anchors = ordered.Anchors.OrderBy(a => a.Id, StringComparer.Ordinal).ToList();
        ordered.RouteAnchors = ordered.RouteAnchors.OrderBy(r => r.RouteID, StringComparer.Ordinal).ToList();
        return JsonSerializer.Serialize(ordered, jsonOptions);
    }

    OperationProcessTemplate Clone(OperationProcessTemplate template) =>
        JsonSerializer.Deserialize<OperationProcessTemplate>(JsonSerializer.Serialize(template, jsonOptions), jsonOptions)!;
}

async Task TestConcurrentWrites()
{
    var template = (await Request(HttpMethod.Post, "CreateTemplate", BuildTemplate("concurrent")))!
        .Deserialize<OperationProcessTemplate>(jsonOptions)!;
    var contenders = Enumerable.Range(0, 2).Select(async index =>
    {
        var body = JsonSerializer.SerializeToNode(template, jsonOptions)!;
        body["name"] = $"writer-{index}";
        using var request = new HttpRequestMessage(HttpMethod.Put, "UpdateTemplate")
            { Content = JsonContent.Create(body, options: jsonOptions) };
        request.Headers.Add("X-Test-User", "owner");
        using var response = await client!.SendAsync(request);
        return response.StatusCode;
    });
    var statuses = await Task.WhenAll(contenders);
    Assert(statuses.Count(x => x == HttpStatusCode.OK) == 1 && statuses.Count(x => x == HttpStatusCode.Conflict) == 1,
        $"two simultaneous writes have one winner and one conflict (actual: {string.Join(",", statuses)})");
    var winner = await GetTemplate("concurrent");
    Assert(winner.Revision == 2 && winner.Name.StartsWith("writer-"), "one winning concurrent update persists");
    await Request(HttpMethod.Delete, $"DeleteTemplate?{scope}&templateID=concurrent&revision=2", expected: HttpStatusCode.NoContent);
}

async Task TestOperationPlanLifecycle()
{
    var db = DBConnector.GetDBConnector(DBConnector.CapacityDatabaseSectionName);
    var before = db.Query<int>("SELECT COUNT(1) FROM operationprocesstemplate WHERE InstanceID='scope-a' AND StationSchemeID='scheme-a'")!.Single();
    await Request(HttpMethod.Post, "../OperationPlan/CopyOperationPlan", new
    {
        instanceID = "scope-a", stationSchemeID = "scheme-a", sourceOperationPlanID = "plan-a",
        operationPlanID = "plan-copy", name = "复制计划"
    });
    const string copiedScope = "instanceID=scope-a&stationSchemeID=scheme-a&operationPlanID=plan-copy";
    var copied = (await Request(HttpMethod.Get, $"GetTemplate?{copiedScope}&templateID=persisted"))!
        .Deserialize<OperationProcessTemplate>(jsonOptions)!;
    Assert(copied.OperationPlanID == "" && copied.Activities.Count == 3 && copied.Revision == 2,
        "copied plans see the same scheme-owned process template");
    Assert(db.Query<int>("SELECT COUNT(1) FROM operationprocesstemplate WHERE InstanceID='scope-a' AND StationSchemeID='scheme-a'")!.Single() == before,
        "copying a plan does not duplicate scheme-owned templates");
    await Request(HttpMethod.Put, "../OperationPlan/EditOperationPlan", new
    {
        instanceID = "scope-a", stationSchemeID = "scheme-a", originalOperationPlanID = "plan-copy",
        operationPlanID = "plan-renamed", name = "重命名计划"
    });
    const string renamedScope = "instanceID=scope-a&stationSchemeID=scheme-a&operationPlanID=plan-renamed";
    var renamed = (await Request(HttpMethod.Get, $"GetTemplate?{renamedScope}&templateID=persisted"))!
        .Deserialize<OperationProcessTemplate>(jsonOptions)!;
    Assert(renamed.OperationPlanID == "" && renamed.Name == "已更新的模板",
        "renaming a plan preserves access to its scheme-owned templates");
    await Request(HttpMethod.Delete, $"../OperationPlan/DeleteOperationPlan?{renamedScope}");
    Assert(db.Query<int>("SELECT COUNT(1) FROM operationprocesstemplate WHERE OperationPlanID IN ('plan-copy','plan-renamed')")!.Single() == 0,
        "plan lifecycle does not create plan-owned template rows");
    Assert(db.Query<int>("SELECT COUNT(1) FROM operationprocesstemplate WHERE InstanceID='scope-a' AND StationSchemeID='scheme-a'")!.Single() == before,
        "deleting a plan leaves every scheme template intact");
    Assert((await GetTemplate("persisted")).Revision == 2, "operation plan lifecycle leaves original template unchanged");
}

async Task TestSchemeTemplateOwnershipAndMigration()
{
    const string schemeQuery = "instanceID=scope-a&stationSchemeID=template-scheme";
    var db = DBConnector.GetDBConnector(DBConnector.CapacityDatabaseSectionName);
    db.ExecuteNonQuery("INSERT INTO stationscheme (InstanceID,ID,Name) VALUES ('scope-a','template-scheme','方案模板回归')");
    var process = (await Request(HttpMethod.Post, "CreateTemplate", new OperationProcessTemplate {
        InstanceID = "scope-a", StationSchemeID = "template-scheme", Id = "scheme-process", Name = "不依赖计划的过程"
    }))!.Deserialize<OperationProcessTemplate>(jsonOptions)!;
    Assert(process.OperationPlanID == "", "a process can be created before the scheme has any operation plan");
    var train = (await Request(HttpMethod.Post, "../OperationPlan/CreateTrainTemplate", new TrainTemplateRequest {
        InstanceID = "scope-a", StationSchemeID = "template-scheme", TrainTemplateID = "scheme-train", Name = "不依赖计划的列车", Number = 1
    }))!.Deserialize<TrainTemplateRow>(jsonOptions)!;
    Assert(train.OperationPlanID == "", "a train template can be created without a plan");
    var movement = (await Request(HttpMethod.Post, "../OperationPlan/CreateMovementTemplate", new MovementTemplateRequest {
        InstanceID = "scope-a", StationSchemeID = "template-scheme", TrainTemplateID = train.TrainTemplateID,
        MovementID = "scheme-movement", Name = "方案移动", MinDuration = 12
    }))!.Deserialize<MovementTemplateRow>(jsonOptions)!;
    Assert(movement.OperationPlanID == "", "movement templates inherit station scheme ownership");
    Assert(db.Query<int>("SELECT COUNT(*) FROM operationplan WHERE InstanceID='scope-a' AND StationSchemeID='template-scheme'")!.Single() == 0,
        "editing templates does not synthesize an operation plan");
    foreach (var legacyPlan in new[] { "plan-does-not-exist", "different-plan" })
    {
        var list = (await Request(HttpMethod.Get, $"../OperationPlan/GetTrainTemplates?{schemeQuery}&operationPlanID={legacyPlan}"))!
            .Deserialize<List<TrainTemplateRow>>(jsonOptions)!;
        Assert(list.Single().TrainTemplateID == train.TrainTemplateID, "obsolete plan parameters do not partition train templates");
        var processes = (await Request(HttpMethod.Get, $"GetTemplates?{schemeQuery}&operationPlanID={legacyPlan}"))!
            .Deserialize<List<OperationProcessTemplate>>(jsonOptions)!;
        Assert(processes.Single().Id == process.Id, "obsolete plan parameters do not partition process templates");
    }
    process.Name = "方案共享修改";
    process.OperationPlanID = "obsolete-plan";
    process = (await Request(HttpMethod.Put, "UpdateTemplate", process))!.Deserialize<OperationProcessTemplate>(jsonOptions)!;
    Assert(process.OperationPlanID == "" && process.Revision == 2, "legacy update payloads cannot reattach a process to a plan");
    await Request(HttpMethod.Put, "../OperationPlan/EditTrainTemplate", new TrainTemplateRequest {
        InstanceID = "scope-a", StationSchemeID = "template-scheme", OperationPlanID = "obsolete-plan",
        TrainTemplateID = train.TrainTemplateID, Name = "共享列车修改", Number = 1
    });
    await Request(HttpMethod.Put, "../OperationPlan/EditMovementTemplate", new MovementTemplateRequest {
        InstanceID = "scope-a", StationSchemeID = "template-scheme", OperationPlanID = "obsolete-plan",
        TrainTemplateID = train.TrainTemplateID, MovementID = movement.MovementID, Name = "共享移动修改", MinDuration = 34
    });
    var changed = (await Request(HttpMethod.Get, $"../OperationPlan/GetMovementTemplates?{schemeQuery}&trainTemplateID={train.TrainTemplateID}"))!
        .Deserialize<List<MovementTemplateRow>>(jsonOptions)!;
    Assert(changed.Single().Name == "共享移动修改" && changed.Single().MinDuration == 34,
        "movement edits are shared independently of the selected plan");
    var otherScheme = (await Request(HttpMethod.Get, "../OperationPlan/GetTrainTemplates?instanceID=scope-a&stationSchemeID=foreign-scheme"))!
        .Deserialize<List<TrainTemplateRow>>(jsonOptions)!;
    Assert(otherScheme.All(row => row.TrainTemplateID != train.TrainTemplateID), "scheme templates do not leak into another scheme");

    // Older plans may reuse template IDs for different content. Preserve both definitions,
    // their children, and existing actual-train provenance when promoting them to a scheme.
    TrainProcessSnapshotStore.EnsureSchema(db);
    foreach (var oldPlan in new[] { "legacy-template-a", "legacy-template-b" })
    {
        db.ExecuteNonQuery("INSERT INTO operationplan (InstanceID,StationSchemeID,OperationPlanID,Name) " +
            "VALUES ('scope-a','template-scheme',@oldPlan,@oldPlan)", new { oldPlan });
        var oldProcess = new OperationProcessTemplate {
            InstanceID = "scope-a", StationSchemeID = "template-scheme", OperationPlanID = oldPlan,
            Id = "colliding-process", Name = oldPlan, Revision = 4
        };
        db.ExecuteNonQuery("INSERT INTO operationprocesstemplate (InstanceID,StationSchemeID,OperationPlanID,TemplateID,Document,Revision,UpdatedAtUtc) " +
            "VALUES ('scope-a','template-scheme',@oldPlan,'colliding-process',@document,4,@updatedAt)",
            new { oldPlan, document = JsonSerializer.Serialize(oldProcess, jsonOptions), updatedAt = DateTime.UtcNow });
        db.ExecuteNonQuery("INSERT INTO traintemplate (InstanceID,StationSchemeID,OperationPlanID,TrainTemplateID,Name,Number,IsFixedOperation) " +
            "VALUES ('scope-a','template-scheme',@oldPlan,'colliding-train',@oldPlan,1,0)", new { oldPlan });
        db.ExecuteNonQuery("INSERT INTO movementtemplate (InstanceID,StationSchemeID,OperationPlanID,TrainTemplateID,MovementID,Name,MinDuration,SortOrder) " +
            "VALUES ('scope-a','template-scheme',@oldPlan,'colliding-train','colliding-move',@oldPlan,10,0)", new { oldPlan });
        db.ExecuteNonQuery("INSERT INTO train (InstanceID,StationSchemeID,OperationPlanID,ID,TrainTemplateID,Name) " +
            "VALUES ('scope-a','template-scheme',@oldPlan,'actual-train','colliding-train',@oldPlan)", new { oldPlan });
        db.ExecuteNonQuery("INSERT INTO movement (InstanceID,StationSchemeID,OperationPlanID,TrainID,TrainTemplateID,MovementID,Name) " +
            "VALUES ('scope-a','template-scheme',@oldPlan,'actual-train','colliding-train','actual-move',@oldPlan)", new { oldPlan });
        var snapshot = new TrainProcessSnapshot {
            InstanceID = "scope-a", StationSchemeID = "template-scheme", OperationPlanID = oldPlan,
            TrainID = "actual-train", SourceTemplateID = oldProcess.Id, SourceRevision = 4, SourceName = oldPlan,
            Process = oldProcess, OriginSeconds = 28800, EventTimes = new() { ["retained-event"] = 28805 }
        };
        TrainProcessSnapshotStore.Insert(db, snapshot, snapshot);
    }
    var migratedProcesses = (await Request(HttpMethod.Get, $"GetTemplates?{schemeQuery}"))!
        .Deserialize<List<OperationProcessTemplate>>(jsonOptions)!;
    var migratedTrains = (await Request(HttpMethod.Get, $"../OperationPlan/GetTrainTemplates?{schemeQuery}"))!
        .Deserialize<List<TrainTemplateRow>>(jsonOptions)!;
    Assert(migratedProcesses.Count == 3 && migratedProcesses.Select(row => row.Id).Distinct().Count() == 3 &&
        migratedProcesses.All(row => row.OperationPlanID == ""), "legacy process collisions retain every definition under distinct scheme IDs");
    Assert(migratedTrains.Count == 3 && migratedTrains.Select(row => row.TrainTemplateID).Distinct().Count() == 3 &&
        migratedTrains.All(row => row.OperationPlanID == ""), "legacy train template collisions are promoted without overwriting definitions");
    foreach (var oldPlan in new[] { "legacy-template-a", "legacy-template-b" })
    {
        var promoted = migratedTrains.Single(row => row.Name?.StartsWith(oldPlan, StringComparison.Ordinal) == true);
        var children = (await Request(HttpMethod.Get, $"../OperationPlan/GetMovementTemplates?{schemeQuery}&trainTemplateID={promoted.TrainTemplateID}"))!
            .Deserialize<List<MovementTemplateRow>>(jsonOptions)!;
        Assert(children.Count == 1 && children[0].Name == oldPlan && children[0].OperationPlanID == "",
            "colliding train templates retain their own movement children");
        Assert(db.Query<string>("SELECT TrainTemplateID FROM train WHERE InstanceID='scope-a' AND StationSchemeID='template-scheme' AND OperationPlanID=@oldPlan",
            new { oldPlan })!.Single() == promoted.TrainTemplateID, "migration preserves actual-train provenance after template ID remapping");
        Assert(db.Query<string>("SELECT TrainTemplateID FROM movement WHERE InstanceID='scope-a' AND StationSchemeID='template-scheme' AND OperationPlanID=@oldPlan",
            new { oldPlan })!.Single() == promoted.TrainTemplateID, "migration preserves actual-movement provenance after template ID remapping");
        var processSource = migratedProcesses.Single(row => row.Name.StartsWith(oldPlan, StringComparison.Ordinal));
        var migratedSnapshot = TrainProcessSnapshotStore.LoadAll(db, new ProcessScope {
            InstanceID = "scope-a", StationSchemeID = "template-scheme", OperationPlanID = oldPlan
        }).Single();
        Assert(migratedSnapshot.SourceTemplateID == processSource.Id && migratedSnapshot.Process.Id == processSource.Id,
            $"process ID collision remapping preserves snapshot provenance to its own promoted source ({oldPlan}: source={migratedSnapshot.SourceTemplateID}, process={migratedSnapshot.Process.Id}, expected={processSource.Id})");
        Assert(migratedSnapshot.OperationPlanID == oldPlan && migratedSnapshot.Process.OperationPlanID == oldPlan &&
            migratedSnapshot.SourceRevision == 4 && migratedSnapshot.EventTimes["retained-event"] == 28805,
            "process migration leaves actual execution ownership, source revision and event schedule intact");
    }
    var again = (await Request(HttpMethod.Get, $"../OperationPlan/GetTrainTemplates?{schemeQuery}&operationPlanID=unrelated"))!
        .Deserialize<List<TrainTemplateRow>>(jsonOptions)!;
    Assert(again.Select(row => row.TrainTemplateID).ToHashSet().SetEquals(migratedTrains.Select(row => row.TrainTemplateID)),
        "legacy migration is idempotent across repeated API reads");
    await Request(HttpMethod.Post, "../OperationPlan/CopyOperationPlan", new {
        instanceID = "scope-a", stationSchemeID = "template-scheme", sourceOperationPlanID = "legacy-template-a",
        operationPlanID = "legacy-template-copy", name = "只复制实际作业"
    });
    await Request(HttpMethod.Delete, $"../OperationPlan/DeleteOperationPlan?{schemeQuery}&operationPlanID=legacy-template-a");
    await Request(HttpMethod.Delete, $"../OperationPlan/DeleteOperationPlan?{schemeQuery}&operationPlanID=legacy-template-copy");
    foreach (var table in new[] { "operationprocesstemplate", "traintemplate", "movementtemplate" })
        Assert(db.Query<int>($"SELECT COUNT(*) FROM {table} WHERE InstanceID='scope-a' AND StationSchemeID='template-scheme'")!.Single() == 3,
            $"copying and deleting operation plans preserves all scheme rows in {table}");
    await Request(HttpMethod.Delete, $"../OperationPlan/DeleteTrainTemplate?{schemeQuery}&trainTemplateID={train.TrainTemplateID}");
    Assert(db.Query<int>("SELECT COUNT(*) FROM movementtemplate WHERE InstanceID='scope-a' AND StationSchemeID='template-scheme' AND TrainTemplateID=@id",
        new { id = train.TrainTemplateID })!.Single() == 0, "deleting a scheme train template removes its movement templates");
    await Request(HttpMethod.Delete, $"DeleteTemplate?{schemeQuery}&templateID={process.Id}&revision={process.Revision}", expected: HttpStatusCode.NoContent);
    await Request(HttpMethod.Delete, $"../StationLayout/DeleteStationScheme?{schemeQuery}");
    foreach (var table in new[] { "operationprocesstemplate", "traintemplate", "movementtemplate" })
        Assert(db.Query<int>($"SELECT COUNT(*) FROM {table} WHERE InstanceID='scope-a' AND StationSchemeID='template-scheme'")!.Single() == 0,
            $"deleting the owning station scheme removes its rows in {table}");
    Assert((await GetTemplate("persisted")).Revision == 2, "deleting one scheme does not alter another scheme's template library");
}

async Task TestEventNodeListsDerivedFromCandidateRoutes()
{
    var mixedCaseTemplate = new OperationProcessTemplate
    {
        Events = [new() { Id = "start" }, new() { Id = "end" }],
        Activities = [new() { Id = "movement", Type = "Arrival", StartEvent = "start", EndEvent = "end", RouteList = ["matching", "mismatched"] }]
    };
    OperationProcessEventNodes.Refresh(mixedCaseTemplate, new ProcessCatalog
    {
        Nodes = [new() { Id = "1" }, new() { Id = "2" }, new() { Id = "3" }],
        Routes =
        [
            new() { Id = "matching", Type = "aRrIvAl", StartNodeID = "1", EndNodeID = "2" },
            new() { Id = "mismatched", Type = "Departure", StartNodeID = "3", EndNodeID = "3" }
        ]
    });
    Assert(mixedCaseTemplate.Events.Single(e => e.Id == "start").NodeList.SequenceEqual(["1"]),
        "node candidate helper matches route types case-insensitively and filters mismatched start endpoints");
    Assert(mixedCaseTemplate.Events.Single(e => e.Id == "end").NodeList.SequenceEqual(["2"]),
        "node candidate helper matches route types case-insensitively and filters mismatched end endpoints");

    var db = DBConnector.GetDBConnector(DBConnector.CapacityDatabaseSectionName);
    var routeIDs = new[] { "node-list-a", "node-list-b", "node-list-c", "node-list-d", "node-list-missing" };
    db.ExecuteNonQuery("INSERT INTO stationroute " +
        "(InstanceID,StationSchemeID,ID,Type,Description,NodeList,LinkList,StartNodeID,EndNodeID) VALUES " +
        "('scope-a','scheme-a','node-list-a','Arrival','节点候选A','1,2','10','1','2')," +
        "('scope-a','scheme-a','node-list-b','Arrival','节点候选B','3,2','20','3','2')," +
        "('scope-a','scheme-a','node-list-c','Arrival','节点候选C','1,3','10,20','1','3')," +
        "('scope-a','scheme-a','node-list-d','Arrival','节点候选D','2,1','10','2','1')," +
        "('scope-a','scheme-a','node-list-missing','Arrival','无效节点端点','404','10','404','404')");
    var fixture = new OperationProcessTemplate
    {
        Id = "derived-event-nodes", Name = "事件节点候选派生", InstanceID = "scope-a", StationSchemeID = "scheme-a", OperationPlanID = "plan-a",
        Anchors = [new() { Id = "a10", Name = "一轨道锚", TrackID = "10" }, new() { Id = "a20", Name = "二轨道锚", TrackID = "20" }],
        Events =
        [
            new() { Id = "shared-start", Name = "共享开始事件", NodeID = "2", NodeList = ["999"], AnchorList = ["a20"], SelectedAnchor = "a20" },
            new() { Id = "first-end", Name = "第一结束事件", NodeID = "1", NodeList = ["999"], AnchorList = ["a20"], SelectedAnchor = "a20" },
            new() { Id = "second-end", Name = "第二结束事件", NodeList = [] },
            new() { Id = "dwell-start", Name = "停留开始事件", NodeID = "3", NodeList = ["999"], AnchorList = ["a10"], SelectedAnchor = "a10" },
            new() { Id = "dwell-end", Name = "停留结束事件", NodeID = "3", NodeList = ["999"] },
            new() { Id = "standalone", Name = "独立事件", NodeID = "3", NodeList = ["999"], AnchorList = ["a10"], SelectedAnchor = "a10" },
            new() { Id = "empty-start", Name = "未配置进路开始事件", NodeList = ["999"] },
            new() { Id = "empty-end", Name = "未配置进路结束事件", NodeList = ["999"] }
        ],
        Activities =
        [
            new() { Id = "first", Name = "第一接车活动", Type = "Arrival", StartEvent = "shared-start", EndEvent = "first-end",
                MinDuration = 0, MaxDuration = 10, RouteList = ["node-list-a", "node-list-b", "node-list-c", "node-list-missing"], SelectedRoute = "node-list-a" },
            new() { Id = "second", Name = "第二接车活动", Type = "Arrival", StartEvent = "shared-start", EndEvent = "second-end",
                MinDuration = 0, MaxDuration = 10, RouteList = ["node-list-b", "node-list-d"], SelectedRoute = null },
            new() { Id = "dwelling", Name = "停留活动", Type = "Dwelling", StartEvent = "dwell-start", EndEvent = "dwell-end",
                MinDuration = 0, MaxDuration = 10, TrackList = ["10"], SelectedTrack = "10" },
            new() { Id = "empty-route", Name = "未配置进路活动", Type = "Shunting", StartEvent = "empty-start", EndEvent = "empty-end",
                MinDuration = 0, MaxDuration = 10 }
        ]
    };
    var expectedNodes = new Dictionary<string, string[]>
    {
        ["shared-start"] = ["1", "3", "2"], ["first-end"] = ["2", "3"], ["second-end"] = ["2", "1"],
        ["dwell-start"] = [], ["dwell-end"] = [], ["standalone"] = [], ["empty-start"] = [], ["empty-end"] = []
    };
    var saved = (await Request(HttpMethod.Post, "CreateTemplate", fixture))!
        .Deserialize<OperationProcessTemplate>(jsonOptions)!;
    AssertNodeLists(saved.Events, "create overwrites submitted node lists, includes unselected routes, merges shared endpoints in order and filters missing nodes");
    Assert(saved.Events.Single(e => e.Id == "shared-start").NodeID == "2" &&
           saved.Events.Single(e => e.Id == "first-end").NodeID == "1",
        "legacy node IDs remain stored without restricting selected-route endpoints or candidate-based anchors");
    Assert(saved.Events.Single(e => e.Id == "shared-start").SelectedAnchor == "a20" &&
           saved.Events.Single(e => e.Id == "first-end").SelectedAnchor == "a20" &&
           saved.Events.Single(e => e.Id == "dwell-start").SelectedAnchor == "a10" &&
           saved.Events.Single(e => e.Id == "standalone").SelectedAnchor == "a10",
        "anchors may connect any derived candidate and empty candidate lists impose no node restriction");
    await AssertEveryReadView("initial derived node lists");

    var second = saved.Activities.Single(a => a.Id == "second");
    second.RouteList = ["node-list-d", "node-list-c"];
    expectedNodes["second-end"] = ["1", "3"];
    saved = (await Request(HttpMethod.Put, $"{saved.Id}/activities/second?{scope}&revision={saved.Revision}", second))!
        .Deserialize<OperationProcessTemplate>(jsonOptions)!;
    AssertNodeLists(saved.Events, "activity PUT immediately derives endpoint candidates from the new route alternatives");

    var first = saved.Activities.Single(a => a.Id == "first");
    first.RouteList = ["node-list-b", "node-list-c"];
    first.SelectedRoute = null;
    foreach (var ev in saved.Events) ev.NodeList = ["999"];
    expectedNodes["shared-start"] = ["3", "1", "2"];
    saved = (await Request(HttpMethod.Put, "UpdateTemplate", saved))!
        .Deserialize<OperationProcessTemplate>(jsonOptions)!;
    AssertNodeLists(saved.Events, "aggregate PUT ignores forged candidates and respects changed route-list order");

    var sharedEvent = saved.Events.Single(e => e.Id == "shared-start");
    sharedEvent.NodeList = [];
    saved = (await Request(HttpMethod.Put, $"{saved.Id}/events/shared-start?{scope}&revision={saved.Revision}", sharedEvent))!
        .Deserialize<OperationProcessTemplate>(jsonOptions)!;
    AssertNodeLists(saved.Events, "event PUT cannot clear candidates derived from activities");

    var nullCandidateDocument = JsonSerializer.SerializeToNode(saved, jsonOptions)!;
    foreach (var ev in nullCandidateDocument["events"]!.AsArray()) ev!["nodeList"] = null;
    saved = (await Request(HttpMethod.Put, "UpdateTemplate", nullCandidateDocument))!
        .Deserialize<OperationProcessTemplate>(jsonOptions)!;
    AssertNodeLists(saved.Events, "JSON null nodeList inputs are ignored and replaced with non-null derived lists");
    await AssertEveryReadView("updated route-list candidates");

    foreach (var routeType in new[] { "Departure", "Arrival", "arrival" })
    {
        db.ExecuteNonQuery("UPDATE stationroute SET Type = @routeType " +
            "WHERE InstanceID = 'scope-a' AND StationSchemeID = 'scheme-a' AND ID = 'node-list-c'",
            new { routeType });
        var matchesActivity = routeType != "Departure";
        expectedNodes["shared-start"] = matchesActivity ? ["3", "1", "2"] : ["3", "2"];
        expectedNodes["first-end"] = matchesActivity ? ["2", "3"] : ["2"];
        expectedNodes["second-end"] = matchesActivity ? ["1", "3"] : ["1"];
        await AssertEveryReadView(matchesActivity
            ? $"matching catalog route type {routeType} restores endpoint candidates without a template save"
            : "mismatched catalog route type excludes its endpoints from every read without changing revision");
    }

    db.ExecuteNonQuery("UPDATE stationroute SET StartNodeID = '2', EndNodeID = '2' " +
        "WHERE InstanceID = 'scope-a' AND StationSchemeID = 'scheme-a' AND ID = 'node-list-c'");
    expectedNodes["shared-start"] = ["3", "2"];
    expectedNodes["first-end"] = ["2"];
    expectedNodes["second-end"] = ["1", "2"];
    await AssertEveryReadView("catalog endpoint changes are reflected immediately without saving the template");

    var document = JsonNode.Parse(db.Query<string>("SELECT Document FROM operationprocesstemplate " +
        "WHERE InstanceID = 'scope-a' AND StationSchemeID = 'scheme-a' AND OperationPlanID = '' AND TemplateID = @id",
        new { id = saved.Id })!.Single())!;
    foreach (var ev in document["events"]!.AsArray()) ev!.AsObject().Remove("nodeList");
    db.ExecuteNonQuery("UPDATE operationprocesstemplate SET Document = @document " +
        "WHERE InstanceID = 'scope-a' AND StationSchemeID = 'scheme-a' AND OperationPlanID = '' AND TemplateID = @id",
        new { id = saved.Id, document = document.ToJsonString() });
    await AssertEveryReadView("legacy persisted documents without nodeList derive current candidates on every read");
    await Request(HttpMethod.Delete, $"DeleteTemplate?{scope}&templateID={saved.Id}&revision={saved.Revision}",
        expected: HttpStatusCode.NoContent);
    db.ExecuteNonQuery("DELETE FROM stationroute WHERE InstanceID = 'scope-a' AND StationSchemeID = 'scheme-a' AND ID IN @routeIDs",
        new { routeIDs });

    void AssertNodeLists(IEnumerable<ProcessEvent> events, string name)
    {
        var byID = events.ToDictionary(e => e.Id);
        Assert(byID.Count == expectedNodes.Count && expectedNodes.All(pair =>
                byID.TryGetValue(pair.Key, out var ev) && ev.NodeList.SequenceEqual(pair.Value)), name);
    }

    async Task AssertEveryReadView(string name)
    {
        var detail = await GetTemplate(saved.Id);
        AssertNodeLists(detail.Events, $"{name}: GetTemplate");
        Assert(detail.Revision == saved.Revision, $"{name}: derived reads do not write a new revision");
        var templates = (await Request(HttpMethod.Get, $"GetTemplates?{scope}"))!
            .Deserialize<List<OperationProcessTemplate>>(jsonOptions)!;
        AssertNodeLists(templates.Single(t => t.Id == saved.Id).Events, $"{name}: GetTemplates");
        var events = (await Request(HttpMethod.Get, $"{saved.Id}/events?{scope}"))!
            .Deserialize<List<ProcessEvent>>(jsonOptions)!;
        AssertNodeLists(events, $"{name}: events list");
        foreach (var eventID in new[] { "shared-start", "first-end", "second-end", "standalone" })
        {
            var ev = (await Request(HttpMethod.Get, $"{saved.Id}/events/{eventID}?{scope}"))!
                .Deserialize<ProcessEvent>(jsonOptions)!;
            Assert(ev.NodeList.SequenceEqual(expectedNodes[eventID]), $"{name}: event {eventID} GET");
        }
    }
}

async Task TestGenerateTrainTemplateFromSavedProcess()
{
    const string planID = "generation-plan";
    const string generationScope = "instanceID=scope-a&stationSchemeID=scheme-a&operationPlanID=generation-plan";
    const string endpoint = "../OperationPlan/GenerateTrainTemplateFromProcess";
    var db = DBConnector.GetDBConnector(DBConnector.CapacityDatabaseSectionName);
    db.ExecuteNonQuery("INSERT INTO operationplan (InstanceID,StationSchemeID,OperationPlanID,Name) " +
        "VALUES ('scope-a','scheme-a','generation-plan','过程生成测试')");
    db.ExecuteNonQuery("INSERT INTO link (InstanceID,StationSchemeID,ID,Name,FromNodeID,ToNodeID) " +
        "VALUES ('scope-a','scheme-a',60,'无对应停留进路股道',1,2)");
    db.ExecuteNonQuery("INSERT INTO stationroute " +
        "(InstanceID,StationSchemeID,ID,Type,Description,NodeList,LinkList,StartNodeID,EndNodeID) VALUES " +
        "('scope-a','scheme-a','arrival-alt','Arrival','接车备选','1,2','10','1','2')," +
        "('scope-a','scheme-a','dwell-10','Dwelling','一股道停留','1,2','10','1','2')," +
        "('scope-a','scheme-a','dwell-20','停留','二股道停留','2,3','[\"20\"]','2','3')," +
        "('scope-a','scheme-a','dwell-many','Dwelling','多轨道不是单股道停留','1,2,3','10,20','1','3')," +
        "('scope-a','scheme-a','dwell-none','None','None不是停留','1,2','10','1','2')," +
        "('scope-a','scheme-a','dwell-zero',0,'零类型不是停留','1,2','10','1','2')," +
        "('scope-a','scheme-a','unmatched-none','None','None不能代替停留','1,2','60','1','2')," +
        "('scope-a','scheme-a','unmatched-zero',0,'零类型不能代替停留','1,2','60','1','2')," +
        "('scope-a','scheme-a','unmatched-many','Dwelling','包含股道不是精确单股道','1,2','60,10','1','2')," +
        "('scope-a','scheme-a','unmatched-substring','Dwelling','股道号不得子串匹配','1,2','160','1','2')," +
        "('scope-a','scheme-a','comma,route','Arrival','旧列表无法表达的ID','1,2','10','1','2')," +
        "('scope-a','scheme-a','case-route','Arrival','小写ID','1,2','10','1','2')," +
        "('scope-a','scheme-a','CASE-ROUTE','Arrival','大写ID','1,2','10','1','2')");

    var source = FiveActivitySource("generation-source");
    source = await CreateSource(source);
    var sourceDocument = StoredDocument(source.Id);
    var sourceSnapshot = JsonSerializer.Serialize(await ReadSource(source.Id), jsonOptions);
    var request = RequestFor(source);
    request["name"] = "客户端未保存的伪造名称";
    request["trainTemplateID"] = "client-forced-id";
    request["number"] = 999;
    request["type"] = "伪造类型";
    request["activities"] = new JsonArray(new JsonObject { ["name"] = "未保存活动", ["minDuration"] = 99999 });
    request["template"] = JsonSerializer.SerializeToNode(FiveActivitySource("unsaved-client-source"), jsonOptions);
    var result = await Request(HttpMethod.Post, endpoint, request);
    var trainTemplate = result!["trainTemplate"]!.Deserialize<TrainTemplateRow>(jsonOptions)!;
    Assert(result["movementCount"]!.GetValue<int>() == 5, "all five process activity types become movement template rows");
    Assert(trainTemplate.Name == source.Name && trainTemplate.Type == "" && trainTemplate.Number == 1 && trainTemplate.IsFixedOperation == 0,
        "generated template defaults and name come from the saved source, ignoring client-only fields");
    Assert(trainTemplate.InstanceID == "scope-a" && trainTemplate.StationSchemeID == "scheme-a" && trainTemplate.OperationPlanID == "" &&
           !string.IsNullOrWhiteSpace(trainTemplate.TrainTemplateID) && trainTemplate.TrainTemplateID != "client-forced-id",
        "generated template receives a server identity inside the station scheme");
    var movements = await ReadMovements(trainTemplate.TrainTemplateID!);
    Assert(movements.Select(m => m.Name).SequenceEqual(["接车", "停留", "机车出段", "调车", "发车"]),
        "movement rows follow the event DAG rather than the shuffled activity array");
    Assert(movements.Select(m => m.MinDuration).SequenceEqual(new int?[] { 66, 121, 1, 0, 30 }),
        "minute durations become ceiling seconds without rounding 1.1 minutes up to 67 seconds");
    Assert(movements.Select(m => m.SortOrder).SequenceEqual(new int?[] { 0, 1, 2, 3, 4 }), "movement order is stored explicitly");
    Assert(RouteIDs(movements[0]).SequenceEqual(["arrival-alt", "arrival-1"]), "every movement route alternative is retained in order, not only the selected route");
    Assert(RouteIDs(movements[1]).ToHashSet().SetEquals(["dwell-10", "dwell-20"]),
        "dwelling matches only Dwelling/停留 routes with an exact single candidate track, excluding None, zero and multiple tracks");
    Assert(RouteIDs(movements[2]).SequenceEqual(["locomotive-1"]) && RouteIDs(movements[3]).SequenceEqual(["shunting-1"]) &&
           RouteIDs(movements[4]).SequenceEqual(["departure-1"]), "all four movement route types retain their candidate routes");
    Assert(movements.Select(m => m.MovementID).Distinct().Count() == 5 &&
           movements.All(m => !source.Activities.Any(a => a.Id == m.MovementID)), "generated movement IDs are distinct and independent of source activity IDs");
    Assert(Warnings(result).Length > 0, "unrepresentable process information produces explicit conversion warnings");
    await AssertSourceUnchanged("successful generation is a projection and does not rewrite the process");

    var duplicate = await Request(HttpMethod.Post, endpoint, RequestFor(source));
    var secondTrain = duplicate!["trainTemplate"]!.Deserialize<TrainTemplateRow>(jsonOptions)!;
    var secondMovements = await ReadMovements(secondTrain.TrainTemplateID!);
    Assert(secondTrain.TrainTemplateID != trainTemplate.TrainTemplateID && secondMovements.Count == 5 &&
           !secondMovements.Select(m => m.MovementID).Intersect(movements.Select(m => m.MovementID)).Any(),
        "repeated generation creates independent train and movement identities");
    var trainList = (await Request(HttpMethod.Get, $"../OperationPlan/GetTrainTemplates?{generationScope}"))!
        .Deserialize<List<TrainTemplateRow>>(jsonOptions)!;
    Assert(trainList.Any(t => t.TrainTemplateID == trainTemplate.TrainTemplateID) && trainList.Any(t => t.TrainTemplateID == secondTrain.TrainTemplateID),
        "generated templates are visible through the existing train template API");

    var anotherPlan = RequestFor(source); anotherPlan["operationPlanID"] = "plan-a";
    var sharedResult = await Request(HttpMethod.Post, endpoint, anotherPlan);
    Assert(sharedResult!["trainTemplate"]!["operationPlanID"]!.GetValue<string>() == "",
        "a process created through one plan can generate a scheme train template through another plan");
    var beforeRejections = TableCounts();
    await Request(HttpMethod.Post, endpoint, RequestFor(source), user: null, expected: HttpStatusCode.Unauthorized);
    await Request(HttpMethod.Post, endpoint, RequestFor(source), user: "outsider", expected: HttpStatusCode.Forbidden);
    var missing = RequestFor(source); missing["processTemplateID"] = "missing-source";
    await Request(HttpMethod.Post, endpoint, missing, expected: HttpStatusCode.NotFound);
    var foreignScheme = RequestFor(source); foreignScheme["stationSchemeID"] = "foreign-scheme"; foreignScheme["operationPlanID"] = "plan-a";
    await Request(HttpMethod.Post, endpoint, foreignScheme, expected: HttpStatusCode.NotFound);
    var foreignInstance = RequestFor(source); foreignInstance["instanceID"] = "scope-b"; foreignInstance["operationPlanID"] = "plan-a";
    await Request(HttpMethod.Post, endpoint, foreignInstance, expected: HttpStatusCode.Forbidden);
    var stale = RequestFor(source); stale["revision"] = source.Revision + 1;
    await Request(HttpMethod.Post, endpoint, stale, expected: HttpStatusCode.Conflict);
    var noRevision = RequestFor(source); noRevision.Remove("revision");
    await Request(HttpMethod.Post, endpoint, noRevision, expected: HttpStatusCode.BadRequest);
    Assert(TableCounts() == beforeRejections, "authorization, scope and revision failures create no rows");

    var empty = await CreateSource(new OperationProcessTemplate
        { Id = "empty-generation-source", Name = "空过程", InstanceID = "scope-a", StationSchemeID = "scheme-a", OperationPlanID = planID });
    await Request(HttpMethod.Post, endpoint, RequestFor(empty), expected: HttpStatusCode.BadRequest);
    Assert(TableCounts() == beforeRejections, "empty process cannot generate an empty train template");

    await RejectStoredMutation(t => t.Activities[0].MinDuration = -1, "negative saved duration");
    await RejectStoredMutation(t => { t.Activities[0].MinDuration = double.MaxValue; t.Activities[0].MaxDuration = double.MaxValue; },
        "overflowing saved duration", "整数范围");
    await RejectStoredMutation(t => t.Activities[0].StartEvent = "missing-event", "dangling saved event reference");
    await RejectStoredMutation(t => { t.Activities[0].RouteList = ["foreign-route"]; t.Activities[0].SelectedRoute = "foreign-route"; },
        "foreign saved route reference");
    await RejectStoredMutation(t => { var arrival = t.Activities.Single(a => a.Type == "Arrival"); arrival.RouteList = ["comma,route"]; arrival.SelectedRoute = "comma,route"; },
        "route IDs containing legacy separators cannot silently lose candidates", "分隔符");
    await RejectStoredMutation(t => { var arrival = t.Activities.Single(a => a.Type == "Arrival"); arrival.RouteList = ["case-route", "CASE-ROUTE"]; arrival.SelectedRoute = "case-route"; },
        "case-distinct route IDs cannot silently collapse in legacy readers", "大小写");
    db.ExecuteNonQuery("UPDATE stationroute SET Type = 'Departure' WHERE InstanceID = 'scope-a' AND StationSchemeID = 'scheme-a' AND ID = 'arrival-alt'");
    try
    {
        await Request(HttpMethod.Post, endpoint, RequestFor(source), expected: HttpStatusCode.BadRequest);
        Assert(TableCounts() == beforeRejections, "catalog changes invalidating saved route candidates prevent generation");
    }
    finally
    {
        db.ExecuteNonQuery("UPDATE stationroute SET Type = 'Arrival' WHERE InstanceID = 'scope-a' AND StationSchemeID = 'scheme-a' AND ID = 'arrival-alt'");
    }
    await AssertSourceUnchanged("rejected generation leaves saved source and revision intact");

    db.ExecuteNonQuery("CREATE TRIGGER abort_process_generation BEFORE INSERT ON movementtemplate " +
        "WHEN NEW.StationSchemeID = 'scheme-a' AND NEW.OperationPlanID = '' AND NEW.Name = '机车出段' " +
        "BEGIN SELECT RAISE(ABORT, 'intentional generation rollback test'); END");
    try
    {
        await Request(HttpMethod.Post, endpoint, RequestFor(source), expected: HttpStatusCode.InternalServerError);
        Assert(TableCounts() == beforeRejections, "failure after inserting initial movements rolls back the train and every movement row");
    }
    finally { db.ExecuteNonQuery("DROP TRIGGER abort_process_generation"); }
    await AssertSourceUnchanged("transaction rollback never alters its process source");

    db.ExecuteNonQuery("CREATE TRIGGER ignore_process_movement BEFORE INSERT ON movementtemplate " +
        "WHEN NEW.StationSchemeID = 'scheme-a' AND NEW.OperationPlanID = '' AND NEW.Name = '机车出段' " +
        "BEGIN SELECT RAISE(IGNORE); END");
    try
    {
        await Request(HttpMethod.Post, endpoint, RequestFor(source), expected: HttpStatusCode.InternalServerError);
        Assert(TableCounts() == beforeRejections,
            "a silently skipped middle movement insert is treated as failure and rolls back both train and movement additions");
    }
    finally { db.ExecuteNonQuery("DROP TRIGGER ignore_process_movement"); }

    var overlapping = new OperationProcessTemplate
    {
        Id = "overlapping-source", Name = "合法重叠事件图", InstanceID = "scope-a", StationSchemeID = "scheme-a", OperationPlanID = planID,
        Events = [new() { Id = "b-start", Name = "B开始" }, new() { Id = "b-end", Name = "B结束" },
            new() { Id = "a-start", Name = "A开始" }, new() { Id = "a-end", Name = "A结束" }],
        Activities = [new() { Id = "b", Name = "B", Type = "Departure", StartEvent = "b-start", EndEvent = "b-end", MaxDuration = 10,
                RouteList = ["departure-1"], SelectedRoute = "departure-1" },
            new() { Id = "a", Name = "A", Type = "Arrival", StartEvent = "a-start", EndEvent = "a-end", MaxDuration = 10,
                RouteList = ["arrival-1"], SelectedRoute = "arrival-1" }],
        Precedences = [new() { Id = "start-start", LeadingEvent = "a-start", FollowingEvent = "b-start" },
            new() { Id = "end-end", LeadingEvent = "b-end", FollowingEvent = "a-end" }]
    };
    overlapping = await CreateSource(overlapping);
    var overlappingDocument = StoredDocument(overlapping.Id);
    var overlapResult = await Request(HttpMethod.Post, endpoint, RequestFor(overlapping));
    var overlapTrainID = overlapResult!["trainTemplate"]!["trainTemplateID"]!.GetValue<string>();
    Assert((await ReadMovements(overlapTrainID)).Select(m => m.Name).SequenceEqual(["A", "B"]),
        "legal start-start and end-end constraints use event topology without inventing an activity-level cycle");
    Assert(Warnings(overlapResult).Length > 0 && StoredDocument(overlapping.Id) == overlappingDocument,
        "overlap projection reports lost relationship detail and preserves its saved source");

    var warningSource = new OperationProcessTemplate
    {
        Id = "warning-source", Name = new string('长', 70), InstanceID = "scope-a", StationSchemeID = "scheme-a", OperationPlanID = planID,
        Events = [new() { Id = "d-start", Name = "停留开始" }, new() { Id = "d-end", Name = "停留结束" },
            new() { Id = "l-start", Name = "机车开始" }, new() { Id = "l-end", Name = "机车结束" }],
        Activities = [new() { Id = "long-dwelling", Name = new string('停', 65), Type = "Dwelling", StartEvent = "d-start", EndEvent = "d-end",
                MinDuration = 1.25, MaxDuration = 1.25, TrackList = ["60"], SelectedTrack = "60" },
            new() { Id = "empty-route-locomotive", Name = "无进路机车", Type = "Locomotive", StartEvent = "l-start", EndEvent = "l-end",
                MinDuration = 0.01, MaxDuration = 0.01 }],
        Precedences = [new() { Id = "warning-chain", LeadingEvent = "d-end", FollowingEvent = "l-start" }]
    };
    warningSource = await CreateSource(warningSource);
    var warningDocument = StoredDocument(warningSource.Id);
    var warningResult = await Request(HttpMethod.Post, endpoint, RequestFor(warningSource));
    var warningTrain = warningResult!["trainTemplate"]!.Deserialize<TrainTemplateRow>(jsonOptions)!;
    var warningRows = await ReadMovements(warningTrain.TrainTemplateID!);
    var warnings = Warnings(warningResult);
    Assert(warningTrain.Name == new string('长', 50) && warningRows[0].Name == new string('停', 50),
        "legacy train and movement name limits truncate the projection to 50 characters");
    Assert(warningRows.Count == 2 && warningRows.All(row => RouteIDs(row).Length == 0),
        "unmatched dwelling and route-less movement remain present with empty route alternatives");
    Assert(warningRows[0].MinDuration == 75 && warningRows[1].MinDuration == 1, "fractional durations remain ceiling seconds even on warning rows");
    Assert(warnings.Any(w => w.Contains("50") || w.Contains("截断")) &&
           warnings.Any(w => w.Contains("停留") || w.Contains("匹配")) &&
           warnings.Any(w => w.Contains("无进路机车") || w.Contains("空进路") || w.Contains("未配置")),
        "warnings explicitly explain truncated names and missing route mappings");
    Assert(StoredDocument(warningSource.Id) == warningDocument, "warning conversions retain original full names and graph data");

    OperationProcessTemplate FiveActivitySource(string id)
    {
        var process = new OperationProcessTemplate
            { Id = id, Name = "已保存的五类过程", InstanceID = "scope-a", StationSchemeID = "scheme-a", OperationPlanID = planID };
        foreach (var (activityID, name, type, minutes, routes) in new (string, string, string, double, string[])[]
        {
            ("arrival", "接车", "Arrival", 1.1, ["arrival-alt", "arrival-1"]),
            ("dwell", "停留", "Dwelling", 2.01, []),
            ("loco", "机车出段", "Locomotive", 0.01, ["locomotive-1"]),
            ("shunt", "调车", "Shunting", 0, ["shunting-1"]),
            ("depart", "发车", "Departure", 0.5, ["departure-1"])
        })
        {
            process.Events.Add(new ProcessEvent { Id = activityID + "-start", Name = name + "开始" });
            process.Events.Add(new ProcessEvent { Id = activityID + "-end", Name = name + "结束" });
            process.Activities.Add(new ProcessActivity { Id = activityID, Name = name, Type = type,
                StartEvent = activityID + "-start", EndEvent = activityID + "-end", MinDuration = minutes, MaxDuration = minutes + 1,
                RouteList = routes.ToList(), SelectedRoute = routes.LastOrDefault(),
                TrackList = type == "Dwelling" ? ["10", "20"] : [], SelectedTrack = type == "Dwelling" ? "20" : null });
        }
        for (var i = 1; i < process.Activities.Count; i++) process.Precedences.Add(new ProcessPrecedence
        {
            Id = $"chain-{i}", LeadingEvent = process.Activities[i - 1].EndEvent,
            FollowingEvent = process.Activities[i].StartEvent, Interval = i == 1 ? 0.5 : 0
        });
        process.Activities.Reverse();
        process.Events.Reverse();
        return process;
    }

    JsonObject RequestFor(OperationProcessTemplate process) => new()
    {
        ["instanceID"] = process.InstanceID, ["stationSchemeID"] = process.StationSchemeID, ["operationPlanID"] = process.OperationPlanID,
        ["processTemplateID"] = process.Id, ["revision"] = process.Revision
    };
    async Task<OperationProcessTemplate> CreateSource(OperationProcessTemplate process) =>
        (await Request(HttpMethod.Post, "CreateTemplate", process))!.Deserialize<OperationProcessTemplate>(jsonOptions)!;
    async Task<OperationProcessTemplate> ReadSource(string id) =>
        (await Request(HttpMethod.Get, $"GetTemplate?{generationScope}&templateID={id}"))!.Deserialize<OperationProcessTemplate>(jsonOptions)!;
    async Task<List<MovementTemplateRow>> ReadMovements(string trainID) =>
        (await Request(HttpMethod.Get, $"../OperationPlan/GetMovementTemplates?{generationScope}&trainTemplateID={trainID}"))!
        .Deserialize<List<MovementTemplateRow>>(jsonOptions)!;
    string StoredDocument(string id) => db.Query<string>("SELECT Document FROM operationprocesstemplate " +
        "WHERE InstanceID = 'scope-a' AND StationSchemeID = 'scheme-a' AND OperationPlanID = '' AND TemplateID = @id", new { id })!.Single();
    (int Trains, int Movements) TableCounts() =>
        (db.Query<int>("SELECT COUNT(1) FROM traintemplate WHERE InstanceID='scope-a' AND StationSchemeID='scheme-a'")!.Single(),
         db.Query<int>("SELECT COUNT(1) FROM movementtemplate WHERE InstanceID='scope-a' AND StationSchemeID='scheme-a'")!.Single());
    string[] RouteIDs(MovementTemplateRow movement) => (movement.RouteIDList ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    string[] Warnings(JsonNode response) => response["warnings"]!.AsArray().Select(w => w!.GetValue<string>()).ToArray();
    async Task AssertSourceUnchanged(string label) => Assert(StoredDocument(source.Id) == sourceDocument &&
        JsonSerializer.Serialize(await ReadSource(source.Id), jsonOptions) == sourceSnapshot, label);
    async Task RejectStoredMutation(Action<OperationProcessTemplate> mutate, string label, string? messageContains = null)
    {
        var changed = JsonSerializer.Deserialize<OperationProcessTemplate>(sourceDocument, jsonOptions)!;
        mutate(changed);
        db.ExecuteNonQuery("UPDATE operationprocesstemplate SET Document = @document WHERE InstanceID='scope-a' AND StationSchemeID='scheme-a' AND OperationPlanID = '' AND TemplateID = @id",
            new { id = source.Id, document = JsonSerializer.Serialize(changed, jsonOptions) });
        try
        {
            var rejection = await Request(HttpMethod.Post, endpoint, RequestFor(source), expected: HttpStatusCode.BadRequest);
            if (messageContains is not null) Assert(rejection!["message"]!.GetValue<string>().Contains(messageContains), $"{label} explains why projection is impossible");
            Assert(TableCounts() == beforeRejections, $"{label} is rejected before writing any generated rows");
        }
        finally
        {
            db.ExecuteNonQuery("UPDATE operationprocesstemplate SET Document = @document WHERE InstanceID='scope-a' AND StationSchemeID='scheme-a' AND OperationPlanID = '' AND TemplateID = @id",
                new { id = source.Id, document = sourceDocument });
        }
    }
}

async Task TestLegacyGenerationProjectsSecondsIntoMinutes()
{
    var db = DBConnector.GetDBConnector(DBConnector.CapacityDatabaseSectionName);
    db.ExecuteNonQuery("INSERT INTO operationplan (InstanceID,StationSchemeID,OperationPlanID,Name) " +
        "VALUES ('scope-a','scheme-a','projection-plan','秒到分钟投影')");
    db.ExecuteNonQuery("INSERT INTO traintemplate (InstanceID,StationSchemeID,OperationPlanID,TrainTemplateID,Name,Type,Number,IsFixedOperation) " +
        "VALUES ('scope-a','scheme-a','projection-plan','projection-train','时长单位回归','',1,0)");
    db.ExecuteNonQuery("INSERT INTO movementtemplate (InstanceID,StationSchemeID,OperationPlanID,TrainTemplateID,MovementID,Name,RouteIDList,MinDuration,SortOrder) VALUES " +
        "('scope-a','scheme-a','projection-plan','projection-train','exact-minute','整分钟','arrival-1',60,0)," +
        "('scope-a','scheme-a','projection-plan','projection-train','fraction-minute','小数分钟','arrival-1',61,1)," +
        "('scope-a','scheme-a','projection-plan','projection-train','multi-minute','跨分钟','arrival-1',121,2)");
    var generated = (await Request(HttpMethod.Post, "../OperationPlan/GenerateTrainOperationPlan", new
    {
        instanceID = "scope-a", stationSchemeID = "scheme-a", operationPlanID = "projection-plan", startTime = "08:00", endTime = "09:00"
    }))!.Deserialize<TrainOperationPlanResponse>(jsonOptions)!;
    Assert(generated.Trains.Count == 1 && generated.Movements.Count == 3, "legacy generation still produces every configured movement");
    Assert(generated.Movements.Select(m => m.MinDuration).SequenceEqual(new int?[] { 60, 61, 121 }),
        "legacy generated movement minimum durations remain stored in seconds");
    Assert(generated.Movements.Select(m => Minutes(m.LatestEndTime!) - Minutes(m.EarliestStartTime!)).SequenceEqual([1, 2, 3]),
        "legacy time windows project seconds into ceiling minutes without multiplying durations by 60");
    Assert(generated.Movements[0].EarliestStartTime == "08:00" && generated.Movements[^1].EarliestStartTime == "08:57" &&
           generated.Movements[^1].LatestEndTime == "09:00", "distribution window uses maximum duration in minutes and ends at the requested hour");
    static int Minutes(string time) { var fields = time.Split(':'); return int.Parse(fields[0]) * 60 + int.Parse(fields[1]); }
}

async Task TestGenerateActualTrainsFromProcess()
{
    const string planID = "process-actual-plan";
    const string targetScope = "instanceID=scope-a&stationSchemeID=scheme-a&operationPlanID=process-actual-plan";
    const string endpoint = "../OperationPlan/GenerateTrainOperationPlanFromProcess";
    var db = DBConnector.GetDBConnector(DBConnector.CapacityDatabaseSectionName);
    // The preceding rejection test deliberately adds case-colliding route IDs. Remove
    // that invalid catalog fixture before exercising the real solver's global catalog.
    db.ExecuteNonQuery("DELETE FROM stationroute WHERE InstanceID='scope-a' AND StationSchemeID='scheme-a' AND ID='CASE-ROUTE'");
    db.ExecuteNonQuery("INSERT INTO operationplan (InstanceID,StationSchemeID,OperationPlanID,Name) VALUES ('scope-a','scheme-a',@id,'完整过程生成')", new { id = planID });
    await Request(HttpMethod.Post, "../OperationPlan/CreateTrain", new TrainRow { InstanceID = "scope-a", StationSchemeID = "scheme-a", OperationPlanID = planID, ID = "existing-train", Name = "保留原列车", TrainNumber = "7" });
    var source = BuildTemplate("actual-source");
    source.OperationPlanID = planID;
    foreach (var (id, type, routeID, start, end) in new[] { ("loco", "Locomotive", "locomotive-1", 2d, 3d), ("shunt", "Shunting", "shunting-1", 6d, 6.001d) })
    {
        source.Events.Add(new ProcessEvent { Id = id + "-s", Name = id + "开始", Time = start });
        source.Events.Add(new ProcessEvent { Id = id + "-e", Name = id + "结束", Time = end });
        source.Activities.Add(new ProcessActivity { Id = id, Name = id, Type = type, StartEvent = id + "-s", EndEvent = id + "-e", MinDuration = end - start, MaxDuration = end - start, RouteList = [routeID] });
    }
    source = (await Request(HttpMethod.Post, "CreateTemplate", source))!.Deserialize<OperationProcessTemplate>(jsonOptions)!;
    var savedSource = Document(source.Id);
    var generated = (await Request(HttpMethod.Post, endpoint, Payload(3)))!.Deserialize<TrainOperationPlanResponse>(jsonOptions)!;
    Assert(generated.GeneratedTrainIDs.Count == 3 && generated.Trains.Count == 4 && generated.Trains.Any(t => t.ID == "existing-train"), "actual generation appends requested trains without replacing existing trains");
    Assert(generated.Movements.Count == 15 && generated.ProcessConstraints.Count == 3, "all five activities and independent full snapshots are created for every train");
    Assert(generated.GeneratedTrainIDs.Distinct().Count() == 3 && generated.Movements.Select(m => m.MovementID).Distinct().Count() == 15, "generated actual trains and activities have independent server IDs");
    var snapshots = generated.ProcessConstraints.Select(item => item.Deserialize<TrainProcessSnapshot>(jsonOptions)!).ToList();
    foreach (var snapshot in snapshots)
    {
        Assert(snapshot.SourceTemplateID == source.Id && snapshot.SourceRevision == source.Revision && snapshot.ActivityMovementMap.Count == 5, "snapshot keeps source version and complete movement map");
        var expectedProcess = JsonSerializer.Deserialize<OperationProcessTemplate>(JsonSerializer.Serialize(source, jsonOptions), jsonOptions)!;
        expectedProcess.OperationPlanID = planID;
        Assert(JsonSerializer.Serialize(snapshot.Process, jsonOptions) == JsonSerializer.Serialize(expectedProcess, jsonOptions),
            "complete scheme source constraints survive with the actual execution plan ownership");
        OperationProcessPlanScheduler.ValidateSchedule(snapshot.Process, snapshot.EventTimes, snapshot.OriginSeconds, 8 * 3600, 9 * 3600);
        Assert(snapshot.EventTimes["loco-s"] < snapshot.EventTimes["arrival-end"], "parallel locomotive operation is not forced after preceding table activity");
        Assert(Math.Abs(snapshot.EventTimes["shunt-e"] - snapshot.EventTimes["shunt-s"] - 0.06) < 0.000001, "subsecond activity constraints survive generation");
        Assert(snapshot.SelectedTrackIDs["dwelling"] == "10", "dwelling uses original track resource without inventing a station route");
        var dwelling = generated.Movements.Single(m => m.TrainID == snapshot.TrainID && m.MovementID == snapshot.ActivityMovementMap["dwelling"]);
        Assert(dwelling.Route == "" && dwelling.RouteIDList == "", "dwelling does not fall back to an arbitrary movement route");
    }
    Assert(Document(source.Id) == savedSource, "generation leaves source document unchanged");
    db.ExecuteNonQuery("INSERT INTO cell (InstanceID,StationSchemeID,ID,LinkIDList,Name) VALUES ('scope-a','scheme-a','actual-cell','10,20,50','过程占用轨道电路')");
    db.ExecuteNonQuery("UPDATE stationroute SET CellList='actual-cell' WHERE InstanceID='scope-a' AND StationSchemeID='scheme-a'");
    db.ExecuteNonQuery("INSERT INTO stationroutetime (InstanceID,StationSchemeID,RouteID,TrainTypeID,CellID,StartOccupationShift,EndOccupationShift) SELECT InstanceID,StationSchemeID,ID,'','actual-cell',0,0 FROM stationroute WHERE InstanceID='scope-a' AND StationSchemeID='scheme-a'");
    var solveInput = new StationCapacityInputBuilder().Build(new() { InstanceId = "scope-a", StationSchemeId = "scheme-a", OperationPlanId = planID });
    Assert(solveInput.MinimumModelVersion == "2.0.0" && solveInput.Trains.Count(t => t.ProcessConstraints is not null) == 3, "solver input transports complete event graphs and requires a compatible worker");
    foreach (var snapshot in snapshots)
    {
        var inputTrain = solveInput.Trains.Single(t => t.Id == snapshot.TrainID);
        var dwellInput = inputTrain.Movements.Single(m => m.Id == snapshot.ActivityMovementMap["dwelling"]);
        Assert(inputTrain.ProcessConstraints!.Events.Count == 10 && inputTrain.ProcessConstraints.Precedences.Count == 2, "solver input keeps all events and precedence edges");
        Assert(dwellInput.MinDurationSeconds == 480 && dwellInput.MaxDurationSeconds == 900 && dwellInput.CandidateRouteIds.Count == 1, "solver receives exact duration bounds and an explicit dwelling resource");
        Assert(solveInput.Routes.Single(r => r.Id == dwellInput.CandidateRouteIds[0]).CellIds.Contains("actual-cell"), "dwelling track participates in real cell occupation conflicts");
        var fractional = inputTrain.Movements.Single(m => m.Id == snapshot.ActivityMovementMap["shunt"]);
        Assert(Math.Abs(fractional.MinDurationSeconds - .06) < .000001 && Math.Abs(fractional.MaxDurationSeconds - .06) < .000001, "solver preserves fractional seconds rather than legacy integer projection");
    }
    var workerPath = Environment.GetEnvironmentVariable("SWITCHYARD_PROCESS_TEST_WORKER");
    if (!string.IsNullOrWhiteSpace(workerPath))
    {
        // Isolate one generated train: this checks its parallel graph and fractional times;
        // the initial generator deliberately does not optimize cross-train cell conflicts.
        solveInput.Trains = solveInput.Trains.Where(train => train.Id == snapshots[0].TrainID).ToList();
        solveInput.Settings.TimeLimitSeconds = 20;
        solveInput.Settings.ThreadCount = 1;
        var requestPath = Path.Combine(testRoot, "process-worker-request.json");
        var responsePath = Path.Combine(testRoot, "process-worker-response.json");
        await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(new { jobId = "process-integration", input = solveInput, resources = new CapacityTaskResourceLimits { CpuCoreCount = 1 } }, jsonOptions));
        var startInfo = new System.Diagnostics.ProcessStartInfo(workerPath) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "--solve-worker", "--request", requestPath, "--response", responsePath }) startInfo.ArgumentList.Add(arg);
        using var worker = System.Diagnostics.Process.Start(startInfo)!;
        var workerOut = worker.StandardOutput.ReadToEndAsync();
        var workerError = worker.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        try { await worker.WaitForExitAsync(timeout.Token); }
        catch { worker.Kill(entireProcessTree: true); throw; }
        var output = await workerOut + await workerError;
        Assert(worker.ExitCode == 0, "actual capacity worker accepts process input: " + output);
        var response = JsonNode.Parse(await File.ReadAllTextAsync(responsePath))!;
        var solved = response["result"]!.Deserialize<StationCapacitySolveResult>(jsonOptions)!;
        Assert(solved.HasSolution && solved.Trains.Count == 1, "actual solver finds a feasible parallel process schedule");
        var solvedDwell = solved.Trains[0].Movements.Single(m => m.Id == snapshots[0].ActivityMovementMap["dwelling"]);
        Assert(solvedDwell.RouteId == "" && solvedDwell.TrackId == "10", "solver returns dwelling track without leaking synthetic route IDs");
        var solvedTiny = solved.Trains[0].Movements.Single(m => m.Id == snapshots[0].ActivityMovementMap["shunt"]);
        Assert(Math.Abs(solvedTiny.EndSeconds - solvedTiny.StartSeconds - .06) < .000001, "actual solver retains subsecond maximum and minimum duration");
        var saturated = new SaturatedOperationPlanService().Save(new() { InstanceId = "scope-a", StationSchemeId = "scheme-a", SourceOperationPlanId = planID, RequestedBy = "owner", PresetName = "测试" }, JsonSerializer.SerializeToElement(solved, jsonOptions), "process-test");
        var saturatedScope = new ProcessScope { InstanceID = "scope-a", StationSchemeID = "scheme-a", OperationPlanID = saturated.OperationPlanID! };
        var solvedSnapshots = TrainProcessSnapshotStore.LoadAll(db, saturatedScope);
        Assert(solvedSnapshots.Count == 1 && solvedSnapshots[0].Process.Precedences.Count == 2 && solvedSnapshots[0].SelectedTrackIDs["dwelling"] == "10", "saving a saturated plan retains complete process constraints and selected dwelling track");
        var rebuilt = new StationCapacityInputBuilder().Build(new() { InstanceId = "scope-a", StationSchemeId = "scheme-a", OperationPlanId = saturated.OperationPlanID! });
        Assert(rebuilt.Trains.Single().ProcessConstraints is not null, "saved saturated process plan can be loaded for another constrained solve");
        await Request(HttpMethod.Delete, $"../OperationPlan/DeleteOperationPlan?instanceID=scope-a&stationSchemeID=scheme-a&operationPlanID={saturated.OperationPlanID}");
    }
    var reloaded = (await Request(HttpMethod.Get, $"../OperationPlan/GetTrainOperationPlan?{targetScope}"))!.Deserialize<TrainOperationPlanResponse>(jsonOptions)!;
    Assert(reloaded.ProcessConstraints.Count == 3 && reloaded.Movements.Count == 15, "full constraints are returned after a fresh GET");
    var counts = Counts();
    var otherPlanPayload = Payload(1); otherPlanPayload["operationPlanID"] = "plan-a";
    var otherPlanGenerated = (await Request(HttpMethod.Post, endpoint, otherPlanPayload))!.Deserialize<TrainOperationPlanResponse>(jsonOptions)!;
    Assert(otherPlanGenerated.GeneratedTrainIDs.Count == 1 && otherPlanGenerated.Trains.All(train => train.OperationPlanID == "plan-a") && Counts() == counts,
        "one scheme process generates independently into another plan without changing the original plan");
    await Request(HttpMethod.Post, endpoint, Payload(1), user: null, expected: HttpStatusCode.Unauthorized);
    await Request(HttpMethod.Post, endpoint, Payload(1), user: "outsider", expected: HttpStatusCode.Forbidden);
    var stale = Payload(1); stale["revision"] = source.Revision + 1;
    await Request(HttpMethod.Post, endpoint, stale, expected: HttpStatusCode.Conflict);
    await Request(HttpMethod.Post, endpoint, Payload(0), expected: HttpStatusCode.BadRequest);
    await Request(HttpMethod.Post, endpoint, Payload(1001), expected: HttpStatusCode.BadRequest);
    var foreign = Payload(1); foreign["operationPlanID"] = "missing-target-plan";
    await Request(HttpMethod.Post, endpoint, foreign, expected: HttpStatusCode.NotFound);
    var tight = Payload(2); tight["endTime"] = "08:10";
    await Request(HttpMethod.Post, endpoint, tight, expected: HttpStatusCode.BadRequest);
    Assert(Counts() == counts, "invalid or infeasible batch writes no trains, movements or snapshots");

    db.ExecuteNonQuery("CREATE TRIGGER ignore_actual_snapshot BEFORE INSERT ON trainprocesssnapshot WHEN NEW.OperationPlanID='process-actual-plan' BEGIN SELECT RAISE(IGNORE); END");
    try { await Request(HttpMethod.Post, endpoint, Payload(2), expected: HttpStatusCode.InternalServerError); }
    finally { db.ExecuteNonQuery("DROP TRIGGER ignore_actual_snapshot"); }
    Assert(Counts() == counts, "silent snapshot insert failure rolls back already inserted train and movements");

    var first = snapshots[0];
    var firstMovement = generated.Movements.First(m => m.TrainID == first.TrainID);
    var mutable = JsonSerializer.Deserialize<MovementRow>(JsonSerializer.Serialize(firstMovement))!;
    mutable.MinDuration = (mutable.MinDuration ?? 0) + 1;
    await Request(HttpMethod.Put, "../OperationPlan/EditMovement", mutable, expected: HttpStatusCode.Conflict);
    await Request(HttpMethod.Delete, $"../OperationPlan/DeleteMovement?{targetScope}&trainID={first.TrainID}&movementID={firstMovement.MovementID}", expected: HttpStatusCode.Conflict);
    await Request(HttpMethod.Post, "../OperationPlan/CreateMovement", new MovementRow { InstanceID = "scope-a", StationSchemeID = "scheme-a", OperationPlanID = planID, TrainID = first.TrainID, Name = "不可插入" }, expected: HttpStatusCode.Conflict);
    await Request(HttpMethod.Put, "../OperationPlan/UpdateMovementOrder", new MovementOrderRequest { InstanceID = "scope-a", StationSchemeID = "scheme-a", OperationPlanID = planID, TrainID = first.TrainID, Items = [new() { MovementID = firstMovement.MovementID, SortOrder = 99 }] }, expected: HttpStatusCode.Conflict);
    firstMovement.Name = "允许改显示名称";
    await Request(HttpMethod.Put, "../OperationPlan/EditMovement", firstMovement);
    Assert(Counts() == counts, "bound movement edits cannot destroy process structure");

    var copy = await Request(HttpMethod.Post, "../OperationPlan/CopyOperationPlan", new { instanceID = "scope-a", stationSchemeID = "scheme-a", sourceOperationPlanID = planID, operationPlanID = "process-actual-copy", name = "过程实例复制" });
    var copiedScope = new ProcessScope { InstanceID = "scope-a", StationSchemeID = "scheme-a", OperationPlanID = "process-actual-copy" };
    var copied = TrainProcessSnapshotStore.LoadAll(db, copiedScope);
    Assert(copied.Count == 3 && copied.All(item => item.Process.OperationPlanID == copiedScope.OperationPlanID && item.ActivityMovementMap.Count == 5), "plan copy preserves constraints and rebinds their execution scope");
    await Request(HttpMethod.Put, "../OperationPlan/EditOperationPlan", new { instanceID = "scope-a", stationSchemeID = "scheme-a", originalOperationPlanID = "process-actual-copy", operationPlanID = "process-actual-renamed", name = "约束改号" });
    copiedScope.OperationPlanID = "process-actual-renamed";
    Assert(TrainProcessSnapshotStore.LoadAll(db, copiedScope).Count == 3, "plan ID change moves complete constraints");
    await Request(HttpMethod.Delete, "../OperationPlan/DeleteOperationPlan?instanceID=scope-a&stationSchemeID=scheme-a&operationPlanID=process-actual-renamed");
    Assert(TrainProcessSnapshotStore.LoadAll(db, copiedScope).Count == 0, "plan delete cleans copied constraints");

    // Changes to the source never rewrite an already generated train's independent constraints.
    source.Name = "后续编辑的原过程";
    source = (await Request(HttpMethod.Put, "UpdateTemplate", source))!.Deserialize<OperationProcessTemplate>(jsonOptions)!;
    Assert(TrainProcessSnapshotStore.LoadAll(db, new() { InstanceID = "scope-a", StationSchemeID = "scheme-a", OperationPlanID = planID }).All(s => s.SourceRevision == 1 && s.Process.Name != source.Name), "source edits do not mutate generated constraints");
    await Request(HttpMethod.Delete, $"DeleteTemplate?{targetScope}&templateID={source.Id}&revision={source.Revision}", expected: HttpStatusCode.NoContent);
    Assert((await Request(HttpMethod.Get, $"../OperationPlan/GetTrainOperationPlan?{targetScope}"))!["processConstraints"]!.AsArray().Count == 3, "source deletion keeps independent generated constraints readable");
    await Request(HttpMethod.Delete, $"../OperationPlan/DeleteTrain?{targetScope}&id={first.TrainID}");
    Assert(Counts().Snapshots == 2 && Counts().Movements == 10, "delete whole train cleans its activities and snapshot together");
    var replacement = (await Request(HttpMethod.Post, "../OperationPlan/GenerateTrainOperationPlan", new { instanceID = "scope-a", stationSchemeID = "scheme-a", operationPlanID = planID, startTime = "00:00", endTime = "24:00" }))!
        .Deserialize<TrainOperationPlanResponse>(jsonOptions)!;
    Assert(Counts().Snapshots == 0 && replacement.Trains.Count > 0 && replacement.Trains.All(train => !generated.GeneratedTrainIDs.Contains(train.ID!)),
        "replace-generation uses scheme templates and clears snapshots along with replaced process trains");

    // A model version gate must reserve no slot when an old worker cannot understand constraints.
    var registry = new CapacityAgentRegistry();
    registry.Register("worker", "worker", "owner", "connection", [new() { Id = CapacityAgentProtocol.StationCapacityModelId, Version = "1.0.0" }], [], new() { TotalMemoryBytes = 8L * 1024 * 1024 * 1024 }, false, new());
    Assert(!registry.TryReserve("worker", CapacityAgentProtocol.StationCapacityModelId, "incompatible", "owner", out _, out _, out _, out var error, "2.0.0") && error.Contains("2.0.0"), "old worker cannot silently discard process constraints");
    Assert(registry.TryReserve("worker", CapacityAgentProtocol.StationCapacityModelId, "legacy", "owner", out _, out _, out _, out _), "old worker remains compatible with a legacy plan");

    JsonObject Payload(int count) => new() { ["instanceID"] = "scope-a", ["stationSchemeID"] = "scheme-a", ["operationPlanID"] = planID, ["processTemplateID"] = source.Id, ["revision"] = source.Revision, ["trainCount"] = count, ["startTime"] = "08:00", ["endTime"] = "09:00" };
    string Document(string id) => db.Query<string>("SELECT Document FROM operationprocesstemplate WHERE InstanceID='scope-a' AND StationSchemeID='scheme-a' AND OperationPlanID='' AND TemplateID=@id", new { id })!.Single();
    (int Trains, int Movements, int Snapshots) Counts() => (db.Query<int>("SELECT COUNT(*) FROM train WHERE OperationPlanID=@planID", new { planID })!.Single(), db.Query<int>("SELECT COUNT(*) FROM movement WHERE OperationPlanID=@planID", new { planID })!.Single(), db.Query<int>("SELECT COUNT(*) FROM trainprocesssnapshot WHERE OperationPlanID=@planID", new { planID })!.Single());
}

async Task TestGeneratedProcessEventsAreEarliest()
{
    const string planID = "process-earliest-plan";
    const string query = "instanceID=scope-a&stationSchemeID=scheme-a&operationPlanID=process-earliest-plan";
    var db = DBConnector.GetDBConnector(DBConnector.CapacityDatabaseSectionName);
    db.ExecuteNonQuery("INSERT INTO operationplan (InstanceID,StationSchemeID,OperationPlanID,Name) " +
        "VALUES ('scope-a','scheme-a',@planID,'最早事件回归')", new { planID });
    var source = BuildTemplate("earliest-source");
    source.OperationPlanID = planID;
    source.Name = "无固定时刻的连续接车停留发车";
    foreach (var ev in source.Events) ev.Time = null;
    foreach (var precedence in source.Precedences) precedence.Interval = 0;
    source = (await Request(HttpMethod.Post, "CreateTemplate", source))!.Deserialize<OperationProcessTemplate>(jsonOptions)!;
    var document = SourceDocument();
    var generated = (await Request(HttpMethod.Post, "../OperationPlan/GenerateTrainOperationPlanFromProcess", new {
        instanceID = "scope-a", stationSchemeID = "scheme-a", operationPlanID = planID,
        processTemplateID = source.Id, revision = source.Revision, trainCount = 1,
        startTime = "08:00", endTime = "09:00"
    }))!.Deserialize<TrainOperationPlanResponse>(jsonOptions)!;
    AssertSchedule(generated, "generation response");
    var reloaded = (await Request(HttpMethod.Get, $"../OperationPlan/GetTrainOperationPlan?{query}"))!
        .Deserialize<TrainOperationPlanResponse>(jsonOptions)!;
    AssertSchedule(reloaded, "fresh GET");
    Assert(SourceDocument() == document, "earliest-event generation leaves the original untimed process document unchanged");

    void AssertSchedule(TrainOperationPlanResponse plan, string phase)
    {
        Assert(plan.Trains.Count == 1 && plan.Movements.Count == 3 && plan.ProcessConstraints.Count == 1,
            $"{phase} contains one complete serial train with its original graph");
        var snapshot = plan.ProcessConstraints.Single().Deserialize<TrainProcessSnapshot>(jsonOptions)!;
        Assert(snapshot.OriginSeconds == 28800 && snapshot.Process.Events.All(ev => ev.Time is null),
            $"{phase} preserves the 08:00 origin and does not replace unset source times with fixed constraints");
        foreach (var (activityId, startClock, endClock, startSeconds, endSeconds) in new[] {
            ("arrival", "08:00:00", "08:02:00", 28800d, 28920d),
            ("dwelling", "08:02:00", "08:10:00", 28920d, 29400d),
            ("departure", "08:10:00", "08:12:00", 29400d, 29520d)
        })
        {
            var activity = snapshot.Process.Activities.Single(item => item.Id == activityId);
            var movement = plan.Movements.Single(item => item.MovementID == snapshot.ActivityMovementMap[activityId]);
            Assert(movement.EarliestStartTime == startClock && movement.LatestEndTime == endClock,
                $"{phase} {activityId} movement uses earliest clock times without uniform-slot gaps");
            Assert(snapshot.EventTimes[activity.StartEvent] == startSeconds && snapshot.EventTimes[activity.EndEvent] == endSeconds,
                $"{phase} {activityId} snapshot endpoints match the actual movement clock times");
        }
        foreach (var precedence in snapshot.Process.Precedences)
            Assert(snapshot.EventTimes[precedence.FollowingEvent] == snapshot.EventTimes[precedence.LeadingEvent],
                $"{phase} zero-interval successor follows immediately at the predecessor's end");
    }

    string SourceDocument() => db.Query<string>("SELECT Document FROM operationprocesstemplate WHERE InstanceID='scope-a' " +
        "AND StationSchemeID='scheme-a' AND OperationPlanID='' AND TemplateID=@id", new { id = source.Id })!.Single();
}

OperationProcessTemplate BuildTemplate(string id) => new()
{
    Id = id, Name = "接车－停留－发车", InstanceID = "scope-a", StationSchemeID = "scheme-a", OperationPlanID = "plan-a",
    Anchors = [new() { Id = "a10", Name = "1道锚", TrackID = "10" }, new() { Id = "a20", Name = "2道锚", TrackID = "20" }],
    Events =
    [
        new() { Id = "arrival-start", Name = "接车开始", NodeID = "1", NodeList = ["1"], Time = 0, AnchorList = ["a10"], SelectedAnchor = "a10" },
        new() { Id = "arrival-end", Name = "接车结束", NodeID = "2", NodeList = ["2"], Time = 5 },
        new() { Id = "dwelling-start", Name = "停留开始", NodeID = "2", Time = 5 },
        new() { Id = "dwelling-end", Name = "停留结束", NodeID = "2", Time = 15 },
        new() { Id = "departure-start", Name = "发车开始", NodeID = "2", NodeList = ["2"], Time = 16 },
        new() { Id = "departure-end", Name = "发车结束", NodeID = "3", NodeList = ["3"], Time = 20 }
    ],
    Activities =
    [
        new() { Id = "arrival", Name = "接车", Type = "Arrival", MinDuration = 2, MaxDuration = 6,
            StartEvent = "arrival-start", EndEvent = "arrival-end", RouteList = ["arrival-1"], SelectedRoute = "arrival-1" },
        new() { Id = "dwelling", Name = "停留", Type = "Dwelling", MinDuration = 8, MaxDuration = 15,
            StartEvent = "dwelling-start", EndEvent = "dwelling-end", TrackList = ["10"], SelectedTrack = "10" },
        new() { Id = "departure", Name = "发车", Type = "Departure", MinDuration = 2, MaxDuration = 5,
            StartEvent = "departure-start", EndEvent = "departure-end", RouteList = ["departure-1"], SelectedRoute = "departure-1" }
    ],
    Precedences =
    [
        new() { Id = "arrival-to-dwell", LeadingEvent = "arrival-end", FollowingEvent = "dwelling-start", Interval = 0 },
        new() { Id = "dwell-to-departure", LeadingEvent = "dwelling-end", FollowingEvent = "departure-start", Interval = 1 }
    ],
    RouteAnchors = [new() { RouteID = "arrival-1", StartAnchor = null, EndAnchor = null }]
};

async Task<OperationProcessTemplate> GetTemplate(string id) =>
    (await Request(HttpMethod.Get, $"GetTemplate?{scope}&templateID={id}"))!
        .Deserialize<OperationProcessTemplate>(jsonOptions)!;

async Task<JsonNode?> Request(HttpMethod method, string url, object? body = null,
    string? user = "owner", HttpStatusCode expected = HttpStatusCode.OK)
{
    using var request = new HttpRequestMessage(method, url);
    if (user is not null) request.Headers.Add("X-Test-User", user);
    if (body is not null) request.Content = JsonContent.Create(body, options: jsonOptions);
    using var response = await client!.SendAsync(request);
    var content = await response.Content.ReadAsStringAsync();
    Assert(response.StatusCode == expected,
        $"{method} {url} expected {(int)expected}, received {(int)response.StatusCode}: {content}");
    if (string.IsNullOrWhiteSpace(content)) return null;
    try { return JsonNode.Parse(content); }
    catch (JsonException) { return JsonValue.Create(content); }
}

void Assert(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException($"Integration assertion failed: {name}");
    checks++;
}

file sealed class TestAuthentication : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "OperationProcessTest";
    public TestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder) : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var name = Request.Headers["X-Test-User"].ToString();
        if (string.IsNullOrWhiteSpace(name)) return Task.FromResult(AuthenticateResult.NoResult());
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, name), new Claim(ClaimTypes.Role, name == "admin" ? "Admin" : "User")], SchemeName);
        if (name == "multi-role-admin") identity.AddClaim(new Claim(ClaimTypes.Role, "Admin"));
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
