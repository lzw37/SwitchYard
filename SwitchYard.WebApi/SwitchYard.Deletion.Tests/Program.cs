using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using SwitchYard.Service;
using SwitchYard.Service.Controllers;
using SwitchYard.Service.Models;
using SwitchYard.Service.Services;
using SwitchYard.Service.StationLayout;
using SwitchYard.Service.Utils;
using SwitchYard.StationLayout;

// Real deletion methods and SQLite transactions; no application configuration or data is read.
var temporaryRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "switchyard-deletion-" + Guid.NewGuid().ToString("N")));
Directory.CreateDirectory(temporaryRoot);
var checks = 0;
var run = 0;
IConfiguration configuration = null!;
var idGenerator = new SnowflakeIdGenerator(2, 2);
var environment = new TestEnvironment { ContentRootPath = temporaryRoot };
try
{
    foreach (var kind in new[] { "instance", "legacy-scheme", "adapter-scheme" })
    {
        Reset(); SeedAllCapacity("i", "s"); SeedAllCapacity("i", "other"); SeedAllCapacity("j", "s");
        var foreign = Dump(C(), "j");
        var otherScheme = SchemeDump("i", "other");
        if (kind == "instance") Ok(Capacity().DeleteInstance("i"));
        else if (kind == "legacy-scheme") Ok(Layout().DeleteStationScheme("i", "s"));
        else Check(await new LegacyStationLayoutRepository(new TestIds()).DeleteSchemeAsync("i", "s", CancellationToken.None), "adapter deletion succeeds");
        foreach (var table in CapacityTables())
        {
            if (table == "capacityinstance" && kind != "instance") continue;
            Check(ScopedCount(table, "i", kind == "instance" ? null : "s") == 0, $"{kind} removes {table}");
        }
        Check(foreign == Dump(C(), "j"), "another instance remains identical");
        if (kind != "instance") Check(otherScheme == SchemeDump("i", "other"), "another scheme remains identical");
    }
    foreach (var action in new[] { "ABORT", "IGNORE" })
    {
        Reset(); SeedAllCapacity("i", "s");
        var before = Dump(C(), "i");
        Trigger(C(), "train", action);
        Status(Capacity().DeleteInstance("i"), 500);
        Check(Dump(C(), "i") == before, "all instance children roll back after " + action);
        Status(Layout().DeleteStationScheme("i", "s"), 500);
        Check(Dump(C(), "i") == before, "all scheme children roll back after " + action);
    }
    foreach (var kind in new[] { "instance", "legacy-scheme", "adapter-scheme", "plan" })
    {
        Reset();
        foreach (var table in new[] { "trainprocesssnapshot", "operationprocesstemplate", "stationplanviewsettings" }) C().ExecuteNonQuery($"DROP TABLE {table}");
        if (kind == "instance") Ok(Capacity().DeleteInstance("i"));
        else if (kind == "legacy-scheme") Ok(Layout().DeleteStationScheme("i", "s"));
        else if (kind == "adapter-scheme") Check(await new LegacyStationLayoutRepository(new TestIds()).DeleteSchemeAsync("i", "s", CancellationToken.None), "adapter tolerates absent optional tables");
        else { C().ExecuteNonQuery("INSERT INTO operationplan(InstanceID,StationSchemeID,OperationPlanID) VALUES('i','s','p')"); Ok(Plans().DeleteOperationPlan("i", "s", "p")); }
    }
    // Route guards inspect tokens and process JSON, rather than substring matching.
    foreach (var reference in new[] { "selected", "alternatives", "template", "process-list", "process-selected", "process-anchor", "snapshot" })
    {
        Reset(); SeedRoute();
        if (reference is "selected" or "alternatives")
            C().ExecuteNonQuery("INSERT INTO movement(InstanceID,StationSchemeID,OperationPlanID,TrainID,MovementID,Route,RouteIDList) VALUES('i','s','p','t','m',@route,@list)",
                new { route = reference == "selected" ? "r" : "", list = reference == "alternatives" ? "rr； r\nother" : "" });
        else if (reference == "template")
            C().ExecuteNonQuery("INSERT INTO movementtemplate(InstanceID,StationSchemeID,TrainTemplateID,MovementID,RouteIDList) VALUES('i','s','tt','m','r')");
        else
        {
            var process = new OperationProcessTemplate();
            if (reference == "process-anchor") process.RouteAnchors.Add(new() { RouteID = "r" });
            else process.Activities.Add(new() { RouteList = reference is "process-list" or "snapshot" ? new() { "r" } : new(), SelectedRoute = reference == "process-selected" ? "r" : null });
            var document = reference == "snapshot" ? JsonSerializer.Serialize(new TrainProcessSnapshot { Process = process }) : JsonSerializer.Serialize(process);
            var table = reference == "snapshot" ? "trainprocesssnapshot" : "operationprocesstemplate";
            C().ExecuteNonQuery($"INSERT INTO {table}(InstanceID,StationSchemeID,OperationPlanID,{(reference == "snapshot" ? "TrainID" : "TemplateID")},Document) VALUES('i','s','p','t',@document)", new { document });
        }
        var before = Dump(C(), "i");
        Status(Layout().DeleteStationRoute("i", "s", "r"), 409);
        Check(Dump(C(), "i") == before, reference + " blocks deletion without mutations");
    }
    Reset(); SeedRoute();
    C().ExecuteNonQuery("INSERT INTO movement(InstanceID,StationSchemeID,RouteIDList) VALUES('i','s','rr'); INSERT INTO movement(InstanceID,StationSchemeID,Route) VALUES('j','s','r')");
    C().ExecuteNonQuery("INSERT INTO operationbottlenecksummarycategoryroute(InstanceID,StationSchemeID,RouteID) VALUES('i','s','r'); INSERT INTO operationanalysismeta(InstanceID,StationSchemeID) VALUES('i','s')");
    Ok(Layout().DeleteStationRoute("i", "s", "r"));
    Check(Count(C(), "stationroutetime") == 0 && Count(C(), "operationbottlenecksummarycategoryroute") == 0 && Count(C(), "operationanalysismeta") == 0, "route parameters and analysis are cleared");
    Check(Count(C(), "movement") == 2, "unrelated route token and foreign instance references remain");
    Reset(); SeedRoute(); Trigger(C(), "stationroute", "IGNORE");
    var routeBefore = Dump(C(), "i"); Status(Layout().DeleteStationRoute("i", "s", "r"), 500);
    Check(Dump(C(), "i") == routeBefore, "route parent failure rolls back parameter cleanup");

    foreach (var kind in new[] { "calculation", "condition", "wagon" })
    {
        Reset(); SeedHump(); var before = Dump(H(), "i");
        var response = kind == "calculation" ? Hump().DeleteHumpCalculation("i", "hs", "hc") : kind == "condition" ? Hump().DeleteOperationCondition("oc") : Hump().DeleteWagonConcept("i", "wt");
        Status(response, 409); Check(Dump(H(), "i") == before, kind + " guard leaves inputs and results intact");
        H().ExecuteNonQuery("DELETE FROM headwaycheckwagon");
        Ok(Hump().DeleteHumpCalculation("i", "hs", "hc"));
        Check(Count(H(), "humpcalculationdata") == 0 && Count(H(), "retarderstatus") == 0, "unreferenced calculation artifacts removed");
        Ok(Hump().DeleteOperationCondition("oc")); Ok(Hump().DeleteWagonConcept("i", "wt"));
    }
    foreach (var layout in new[] { "flat", "slope", "replace-flat", "replace-slope" })
    {
        Reset(); SeedHump(); SeedHump("j"); var foreign = Dump(H(), "j");
        var flat = new SwitchYard.Hump.FlatLayout { InstanceID = "i", SlopeLineID = "sl", PositionList = new(), PositionSegmentList = new(), SwitchList = new(), RetarderList = new() };
        var response = layout == "flat" ? Hump().DeleteFlatLayout(flat) : layout == "slope" ? Hump().DeleteSlopeLayout("i", "hs") :
            layout == "replace-flat" ? Hump().EditFlatLayout(flat) : Hump().EditSlopeLayout(new() { PositionList = new(), PositionSegmentList = new() }, "i", "hs");
        Ok(response);
        foreach (var table in new[] { "humpcalculationdata", "retarderstatus", "headwaycheckdata", "headwaycheckresult" })
            Check(Count(H(), table, "WHERE InstanceID='i'") == 0, layout + " invalidates " + table);
        Check(Count(H(), "humpcalculation", "WHERE InstanceID='i'") == 1 && Count(H(), "headwaycheckwagon", "WHERE InstanceID='i'") == 1, "editable hump configuration remains");
        Check(foreign == Dump(H(), "j"), "geometry invalidation preserves another instance");
    }
    Reset(); SeedHump();
    H().ExecuteNonQuery("INSERT INTO headwaycheckscheme(InstanceID,ID,HumpSchemeID,SlopeLineID) VALUES('i','other-check','hs','other-line'); INSERT INTO headwaycheckwagon(InstanceID,HeadwayCheckID,HumpCalculationID) VALUES('i','other-check','hc'); INSERT INTO headwaycheckresult(InstanceID,HeadwayCheckID) VALUES('i','other-check')");
    Ok(Hump().DeleteSlopeLine("sl"));
    Check(Count(H(), "headwaycheckwagon") == 0 && Count(H(), "headwaycheckresult") == 0, "parent cascade removes remaining cross-line calculation references");
    Check(Count(H(), "headwaycheckscheme") == 1, "unrelated headway header remains editable");
    Reset(); SeedHump(); Trigger(H(), "headwaycheckresult", "ABORT");
    var humpBefore = Dump(H(), "i"); Status(Hump().DeleteFlatLayout(new() { InstanceID="i", SlopeLineID="sl" }), 500);
    Check(humpBefore == Dump(H(), "i"), "layout and earlier invalidations roll back together");

    foreach (var kind in new[] { "single", "batch", "movement" })
    {
        Reset(); SeedAllCapacity("i", "s"); SeedAllCapacity("i", "other");
        if (kind == "movement") C().ExecuteNonQuery("DELETE FROM trainprocesssnapshot WHERE InstanceID='i' AND StationSchemeID='s'");
        var other = SchemeDump("i", "other");
        Ok(DeleteTrainOrMovement(kind));
        foreach (var table in AnalysisTables()) Check(ScopedCount(table, "i", "s") == 0, kind + " invalidates " + table);
        Check(ScopedCount("movement", "i", "s") == 0, "selected movements removed");
        if (kind != "movement") Check(ScopedCount("train", "i", "s") == 0 && ScopedCount("trainprocesssnapshot", "i", "s") == 0, "train execution children removed");
        foreach (var table in new[] { "traintemplate", "operationoccupationtimesubtable", "operationbottlenecksummarycategory", "stationplanviewsettings" })
            Check(ScopedCount(table, "i", "s") == 1, kind + " preserves configuration " + table);
        Check(other == SchemeDump("i", "other"), "analysis invalidation preserves another scheme");
        foreach (var action in new[] { "ABORT", "IGNORE" })
        {
            Reset(); SeedAllCapacity("i", "s");
            if (kind == "movement") C().ExecuteNonQuery("DELETE FROM trainprocesssnapshot");
            Ok(Plans().GetTrainOperationPlan("i", "s", "p")); // Finish lazy schema/default-plan initialization before the transaction baseline.
            var before = Dump(C(), "i"); Trigger(C(), "operationanalysismeta", action);
            Status(DeleteTrainOrMovement(kind), 500); Check(before == Dump(C(), "i"), kind + " rolls back when analysis invalidation fails");
        }
    }
    Reset(); SeedAllCapacity("i", "s");
    Ok(Plans().DeleteOperationPlan("i", "s", "p"));
    foreach (var table in CapacityDataLifecycle.PlanTables) Check(ScopedCount(table,"i","s") == 0,"plan cascade cleans " + table);
    Check(ScopedCount("traintemplate","i","s") == 1,"scheme template intentionally survives plan deletion");
    foreach (var table in new[] { "movement", "trainprocesssnapshot", "train" })
    {
        Reset(); SeedAllCapacity("i", "s"); Ok(Plans().GetTrainOperationPlan("i", "s", "p"));
        var before = Dump(C(), "i"); Trigger(C(), table, "IGNORE");
        Status(Plans().DeleteTrain("i", "s", "p", "t"), 500);
        Check(before == Dump(C(), "i"), "single deletion rolls back when " + table + " silently skips deletion");
    }
    Reset(); SeedAllCapacity("i", "s");
    var schemeBefore = Dump(C(), "i"); Trigger(C(), "stationscheme", "IGNORE");
    Status(Layout().DeleteStationScheme("i", "s"), 500);
    Check(schemeBefore == Dump(C(), "i"), "ignored scheme parent deletion restores all children");
    Reset(); SeedAllCapacity("i", "s"); Ok(Plans().GetTrainOperationPlan("i", "s", "p"));
    var planBefore = Dump(C(), "i"); Trigger(C(), "operationplan", "IGNORE");
    Status(Plans().DeleteOperationPlan("i", "s", "p"), 500);
    Check(planBefore == Dump(C(), "i"), "ignored plan parent deletion restores all children");
    Reset(); SeedAllCapacity("i", "s");
    var reinsertBefore = Dump(C(), "i");
    C().ExecuteNonQuery("CREATE TRIGGER reinsert_child AFTER DELETE ON capacityinstance BEGIN INSERT INTO movement(InstanceID,StationSchemeID) VALUES(OLD.ID,'s'); END");
    Status(Capacity().DeleteInstance("i"), 500);
    Check(reinsertBefore == Dump(C(), "i"), "post-parent verification catches reinserted execution data");
    Console.WriteLine($"Deletion lifecycle regression passed: {checks} assertions.");
}
finally
{
    SqliteConnection.ClearAllPools();
    var allowedRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
    if (!temporaryRoot.StartsWith(allowedRoot, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(temporaryRoot).StartsWith("switchyard-deletion-"))
        throw new InvalidOperationException("Unexpected test cleanup directory.");
    Directory.Delete(temporaryRoot, recursive: true);
}

void Check(bool value,string message) { checks++; if (!value) throw new InvalidOperationException(message); }
void Status(IActionResult result,int expected) => Check(result is ObjectResult o && o.StatusCode == expected, $"Expected {expected}: {JsonSerializer.Serialize(result,result.GetType())}");
void Ok(IActionResult result) => Check(result is OkObjectResult or OkResult, $"Expected success: {JsonSerializer.Serialize(result,result.GetType())}");
DBConnector C() => DBConnector.GetDBConnector(DBConnector.CapacityDatabaseSectionName);
DBConnector H() => DBConnector.GetDBConnector();
int Count(DBConnector db,string table,string filter="") => db.Query<int>($"SELECT COUNT(*) FROM \"{table}\" {filter}")!.Single();
T Actor<T>(T controller) where T:ControllerBase
{
    controller.ControllerContext = new() { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(
        new[] {new Claim(ClaimTypes.Name,"owner"),new Claim(ClaimTypes.Role,"Admin")},"test")) }};
    return controller;
}
UserService Users() => new(NullLogger<UserService>.Instance, configuration);
CapacityController Capacity() => Actor(new CapacityController(NullLogger<CapacityController>.Instance,idGenerator,Users()));
StationLayoutController Layout() => Actor(new StationLayoutController(NullLogger<StationLayoutController>.Instance,environment,idGenerator));
OperationPlanController Plans() => Actor(new OperationPlanController(NullLogger<OperationPlanController>.Instance,idGenerator));
HumpController Hump() => Actor(new HumpController(NullLogger<HumpController>.Instance,configuration,idGenerator,new InstanceAuthorizationService(NullLogger<InstanceAuthorizationService>.Instance),Users(),null!));
IActionResult DeleteTrainOrMovement(string kind) => kind == "single" ? Plans().DeleteTrain("i","s","p","t") : kind == "batch" ? Plans().DeleteTrains(new() {InstanceID="i",StationSchemeID="s",OperationPlanID="p",TrainIDs=new(){"t"}}) : Plans().DeleteMovement("i","s","p","t","m");
void Reset()
{
    run++;
    configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
        ["HumpDatabase:DatabaseType"]="SQLite", ["CapacityDatabase:DatabaseType"]="SQLite",
        ["HumpDatabase:SqlliteConfig:DatabaseFile"]=Path.Combine(temporaryRoot,run+"-hump.db"),
        ["CapacityDatabase:SqlliteConfig:DatabaseFile"]=Path.Combine(temporaryRoot,run+"-capacity.db") }).Build();
    DBConnector.SetConfiguration(configuration);
    H().ExecuteNonQuery(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Database/sqlite-schema.sql")));
    C().ExecuteNonQuery(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Database/capacity-sqlite-schema.sql")));
    C().ExecuteNonQuery("INSERT INTO capacityinstance(ID,Owner) VALUES('i','owner'),('j','other'); INSERT INTO stationscheme(InstanceID,ID) VALUES('i','s'),('i','other'),('j','s')");
    TrainProcessSnapshotStore.EnsureSchema(C());
    C().ExecuteNonQuery("CREATE TABLE operationprocesstemplate(InstanceID TEXT,StationSchemeID TEXT,OperationPlanID TEXT,TemplateID TEXT,Document TEXT)");
    C().ExecuteNonQuery("CREATE TABLE stationplanviewsettings(InstanceID TEXT NOT NULL,StationSchemeID TEXT NOT NULL,OperationPlanID TEXT NOT NULL,CellIDsJson TEXT NOT NULL,EndpointNodeIDsJson TEXT NOT NULL,PRIMARY KEY(InstanceID,StationSchemeID,OperationPlanID))");
}
string[] CapacityTables() => C().Query<string>("SELECT name FROM sqlite_master WHERE type='table' ORDER BY name")!.ToArray();
int ScopedCount(string table,string instance,string? scheme) => Count(C(),table, table=="capacityinstance" ? $"WHERE ID='{instance}'" :
    $"WHERE InstanceID='{instance}'" + (scheme is null ? "" : table=="stationscheme" ? $" AND ID='{scheme}'" : $" AND StationSchemeID='{scheme}'"));
string Dump(DBConnector db,string instance) => string.Join("|",db.Query<string>("SELECT name FROM sqlite_master WHERE type='table' ORDER BY name")!
    .Select(t=>t+":"+JsonSerializer.Serialize(db.Query<dynamic>($"SELECT * FROM \"{t}\" " +
        (t is "user" or "refreshtoken" ? "" : $"WHERE {(t is "capacityinstance" or "humpinstance" ? "ID" : "InstanceID")}=@instance"),new{instance}))));
string SchemeDump(string instance,string scheme) => string.Join("|",CapacityTables().Where(t=>t!="capacityinstance").Select(t=>t+":"+ScopedCount(t,instance,scheme)));
string[] AnalysisTables() => new[] {"operationanalysismeta","operationanalysiscell","operationoccupationtimerow","operationoccupationtimecell","operationbottleneckanalysisresult","operationthroughputsummaryresult","operationthroughputsummaryroute"};
void SeedAllCapacity(string instance,string scheme)
{
    foreach(var table in CapacityTables().Where(t=>t is not "capacityinstance" and not "stationscheme"))
    {
        var columns=C().Query<Column>($"PRAGMA table_info(\"{table}\")")!;
        var fields=new List<string>();var values=new List<string>();
        foreach(var c in columns)
        {
            string? value=c.Name switch {
                "InstanceID"=>instance,"StationSchemeID"=>scheme,"OperationPlanID"=> table is "traintemplate" or "movementtemplate" or "operationprocesstemplate" ? "" : "p",
                "ID"=> c.Type.Contains("INT") ? "1" : "t", "TrainID"=>"t","MovementID"=>"m","TrainTemplateID"=>"tt","TemplateID"=>"pt",
                "Document"=>JsonSerializer.Serialize(table=="trainprocesssnapshot" ? (object)new TrainProcessSnapshot() : new OperationProcessTemplate()),
                "CellIDsJson" or "EndpointNodeIDsJson"=>"[]", _=>c.NotNull!=0 && c.DefaultValue is null ? c.Type.Contains("INT") || c.Type.Contains("REAL") ? "0" : "test" : null };
            if(value is null) continue;
            fields.Add('"'+c.Name+'"');values.Add("'"+value.Replace("'","''")+"'");
        }
        C().ExecuteNonQuery($"INSERT INTO \"{table}\"({string.Join(',',fields)}) VALUES({string.Join(',',values)})");
    }
}
void SeedRoute() => C().ExecuteNonQuery("INSERT INTO stationroute(InstanceID,StationSchemeID,ID) VALUES('i','s','r'); INSERT INTO stationroutetime(InstanceID,StationSchemeID,RouteID) VALUES('i','s','r')");
void Trigger(DBConnector db,string table,string action) => db.ExecuteNonQuery($"CREATE TRIGGER fail_delete BEFORE DELETE ON {table} BEGIN SELECT RAISE({action}{(action=="ABORT" ? ", 'regression failure'" : "")}); END");
void SeedHump(string instance="i")
{
    H().ExecuteNonQuery("""
        INSERT INTO humpinstance(ID,Owner) VALUES(@instance,'owner');
        INSERT INTO slopeline(InstanceID,ID) VALUES(@instance,'sl');
        INSERT INTO humpscheme(InstanceID,ID) VALUES(@instance,'hs');
        INSERT INTO operationcondition(InstanceID,ID) VALUES(@instance,'oc');
        INSERT INTO wagonconcept(InstanceID,TypeName) VALUES(@instance,'wt');
        INSERT INTO humpcalculation(InstanceID,HumpSchemeID,ID,SlopeLineID,OperationConditionID,WagonType) VALUES(@instance,'hs','hc','sl','oc','wt');
        INSERT INTO humpcalculationdata(InstanceID,HumpSchemeID,HumpCalculationID) VALUES(@instance,'hs','hc');
        INSERT INTO retarder(InstanceID,SlopeLineID,ID) VALUES(@instance,'sl','ret');
        INSERT INTO retarderstatus(InstanceID,HumpCalculationID,RetarderID) VALUES(@instance,'hc','ret');
        INSERT INTO vposition(InstanceID,HumpSchemeID,ID) VALUES(@instance,'hs','vp');
        INSERT INTO headwaycheckscheme(InstanceID,ID,HumpSchemeID,SlopeLineID) VALUES(@instance,'hw','hs','sl');
        INSERT INTO headwaycheckwagon(InstanceID,HeadwayCheckID,HumpCalculationID) VALUES(@instance,'hw','hc');
        INSERT INTO headwaycheckdata(InstanceID,HeadwayCheckID) VALUES(@instance,'hw');
        INSERT INTO headwaycheckresult(InstanceID,HeadwayCheckID) VALUES(@instance,'hw');
        """,new{instance});
}
sealed class Column { public string Name{get;set;}="";public string Type{get;set;}="";public int NotNull{get;set;} public string? DefaultValue{get;set;} }
sealed class TestIds:IStationLayoutIdGenerator { public string NextId()=>Guid.NewGuid().ToString("N"); }
sealed class TestEnvironment:IWebHostEnvironment
{
    public string ApplicationName{get;set;}="DeletionTests";public string EnvironmentName{get;set;}="Testing";
    public string ContentRootPath{get;set;}="";public string WebRootPath{get;set;}="";
    public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider(); public IFileProvider WebRootFileProvider{get;set;}=new NullFileProvider();
}
