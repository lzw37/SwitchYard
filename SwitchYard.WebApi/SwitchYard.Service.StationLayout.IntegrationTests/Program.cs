using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using SwitchYard.Service;
using SwitchYard.Service.StationLayout;
using SwitchYard.Service.Utils;
using SwitchYard.StationLayout;

var testRoot = Path.Combine(
    Path.GetTempPath(),
    $"switchyard-station-layout-{Guid.NewGuid():N}");
Directory.CreateDirectory(testRoot);
try
{
    var databasePath = Path.Combine(testRoot, "capacity.db");
    var configuration = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["CapacityDatabase:DatabaseType"] = "SQLite",
            ["CapacityDatabase:SqlliteConfig:DatabaseFile"] = databasePath
        })
        .Build();
    DBConnector.SetConfiguration(configuration);
    ApplySchemaTwice();
    AssertSchema();

    var database = DBConnector.GetDBConnector(DBConnector.CapacityDatabaseSectionName);
    database.ExecuteNonQuery(
        "INSERT INTO capacityinstance (ID, Name, Owner, IsActive) " +
        "VALUES ('scope', 'Scope', 'owner', 1)");

    var idGenerator = new SequentialIdGenerator();
    var repository = new LegacyStationLayoutRepository(idGenerator);
    await TestDefaultSchemeAndCasAsync(repository, database);
    await TestRollbackAsync(repository, database);
    await TestAuthorizationAsync();
    await TestArtifactSinkAsync();
    TestDependencyInjection(configuration);

    Console.WriteLine("SwitchYard legacy station-layout host integration tests passed.");
}
finally
{
    SqliteConnection.ClearAllPools();
    GC.Collect();
    GC.WaitForPendingFinalizers();
    if (Directory.Exists(testRoot))
    {
        Directory.Delete(testRoot, recursive: true);
    }
}

void ApplySchemaTwice()
{
    var scriptPath = Path.Combine(AppContext.BaseDirectory, "Database", "capacity-sqlite-schema.sql");
    var script = File.ReadAllText(scriptPath);
    for (var pass = 0; pass < 2; pass++)
    {
        var database = DBConnector.GetDBConnector(DBConnector.CapacityDatabaseSectionName);
        foreach (var statement in script.Split(
                     ';',
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            database.ExecuteNonQuery(statement);
        }
    }
}

void AssertSchema()
{
    var expectedTables = new[]
    {
        "stationscheme", "stationlayoutrevision", "node", "link", "curve", "signal",
        "insulationjoint", "bufferstop", "platform", "switch", "switchbranchvector",
        "cell", "annotation", "stationroute", "stationrouteend", "stationroutetime"
    };
    var database = DBConnector.GetDBConnector(DBConnector.CapacityDatabaseSectionName);
    var actual = (database.Query<NameRow>(
            "SELECT name AS Name FROM sqlite_master WHERE type = 'table'") ?? [])
        .Select(row => row.Name)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);
    Assert(expectedTables.All(actual.Contains), "host schema contains every adapter table");

    var primaryKeyColumns = (database.Query<PragmaColumn>(
            "PRAGMA table_info(\"stationlayoutrevision\")") ?? [])
        .Where(column => column.Pk > 0)
        .OrderBy(column => column.Pk)
        .Select(column => column.Name)
        .ToArray();
    Assert(primaryKeyColumns.SequenceEqual(["InstanceID", "StationSchemeID"]),
        "revision composite primary key");
}

async Task TestDefaultSchemeAndCasAsync(
    LegacyStationLayoutRepository repository,
    DBConnector database)
{
    Assert(await repository.TryCreateSchemeAsync(
        new StationSchemeRecord("scope", "scheme-a", "A"),
        CancellationToken.None), "create scheme A");
    Assert(await repository.TryCreateSchemeAsync(
        new StationSchemeRecord("scope", "scheme-b", "B"),
        CancellationToken.None), "create scheme B");

    var empty = await repository.LoadAsync("scope", "scheme-a", CancellationToken.None);
    Assert(empty?.Scheme.Revision == 0, "missing revision row is revision zero");

    var first = await repository.ReplaceLayoutAsync(
        new StationLayoutWriteRequest(
            "scope", "scheme-a", BuildDocument(2), 0, "admin"),
        CancellationToken.None);
    Assert(first.Revision == 1, "first revision is one");

    var second = await repository.ReplaceLayoutAsync(
        new StationLayoutWriteRequest(
            "scope", "scheme-b", BuildDocument(3), 0, "admin"),
        CancellationToken.None);
    Assert(second.Revision == 1, "independent scheme revision");

    var defaultLayout = await repository.LoadAsync("scope", null, CancellationToken.None);
    Assert(defaultLayout?.Scheme.SchemeId == "scheme-b", "legacy default chooses scheme with most nodes");
    var defaultDocument = defaultLayout?.Document
                          ?? throw new InvalidOperationException("Default layout was not loaded.");
    Assert(defaultDocument.Nodes.Count == 3, "legacy document round trip");
    Assert(defaultDocument.Metadata?.Revision == 1, "repository metadata revision");
    Assert(defaultDocument.Metadata?.LatestElementID == 102,
        "legacy latestElementID is next whole numeric ID");
    Assert(defaultDocument.Tracks[0].ArrowDirection == "E", "legacy arrow code normalization");
    Assert(defaultDocument.Curves.Single().Radius == 101, "legacy radius midpoint rounding");
    Assert(defaultDocument.Signals.Count == 1 &&
           defaultDocument.InsulationJoints.Count == 1 &&
           defaultDocument.BufferStops.Single().Type == "ext" &&
           defaultDocument.Platforms.Count == 1 &&
           defaultDocument.Switches.Single().BranchVectorList.Count == 1 &&
           defaultDocument.Cells.Count == 1 &&
           defaultDocument.Annotations.Count == 1,
        "all legacy layout entity families round trip");

    await AssertThrowsAsync<StationLayoutConflictException>(
        () => repository.ReplaceLayoutAsync(
            new StationLayoutWriteRequest(
                "scope", "scheme-b", BuildDocument(4), 0, "admin"),
            CancellationToken.None),
        "stale revision conflicts");
    var afterConflict = await repository.LoadAsync("scope", "scheme-b", CancellationToken.None);
    Assert(afterConflict?.Document.Nodes.Count == 3 && afterConflict.Scheme.Revision == 1,
        "conflict leaves aggregate unchanged");

    var winnerCount = 0;
    var conflictCount = 0;
    var contenders = Enumerable.Range(0, 2).Select(index => Task.Run(async () =>
    {
        try
        {
            var result = await repository.ReplaceLayoutAsync(
                new StationLayoutWriteRequest(
                    "scope", "scheme-b", BuildDocument(4 + index), 1, $"writer-{index}"),
                CancellationToken.None);
            Assert(result.Revision == 2, "CAS winner revision");
            Interlocked.Increment(ref winnerCount);
        }
        catch (StationLayoutConflictException)
        {
            Interlocked.Increment(ref conflictCount);
        }
    }));
    await Task.WhenAll(contenders);
    Assert(winnerCount == 1 && conflictCount == 1, "concurrent CAS has one winner");

    var revision = (database.Query<RevisionRow>(
            "SELECT Revision FROM stationlayoutrevision " +
            "WHERE InstanceID = 'scope' AND StationSchemeID = 'scheme-b'") ?? [])
        .Single().Revision;
    Assert(revision == 2, "revision update persisted once");

    Assert(await repository.TryCreateSchemeAsync(
        new StationSchemeRecord("scope", "delete-me", "Delete"),
        CancellationToken.None), "create deletion fixture");
    database.ExecuteNonQuery(
        "INSERT INTO stationroutetime " +
        "(InstanceID, StationSchemeID, RouteID, TrainTypeID, CellID) " +
        "VALUES ('scope', 'delete-me', 'route', 'train', 'cell')");
    Assert(await repository.DeleteSchemeAsync("scope", "delete-me", CancellationToken.None),
        "delete scheme with stationroutetime dependency");
    Assert(!await repository.SchemeExistsAsync("scope", "delete-me", CancellationToken.None),
        "deleted scheme no longer exists");
    var routeTimeRows = database.Query<NameRow>(
        "SELECT RouteID AS Name FROM stationroutetime " +
        "WHERE InstanceID = 'scope' AND StationSchemeID = 'delete-me'") ?? [];
    Assert(routeTimeRows.Count == 0, "stationroutetime rows deleted with scheme");
}

async Task TestRollbackAsync(
    LegacyStationLayoutRepository repository,
    DBConnector database)
{
    Assert(await repository.TryCreateSchemeAsync(
        new StationSchemeRecord("scope", "rollback", "Rollback"),
        CancellationToken.None), "create rollback scheme");
    database.ExecuteNonQuery(
        "INSERT INTO node (InstanceID, StationSchemeID, ID, X, Y) " +
        "VALUES ('scope', 'rollback', 77, 7, 7)");
    database.ExecuteNonQuery(
        "INSERT INTO stationlayoutrevision " +
        "(InstanceID, StationSchemeID, Revision, UpdatedAtUtc) " +
        "VALUES ('scope', 'rollback', @revision, @now)",
        new { revision = long.MaxValue, now = DateTime.UtcNow });

    await AssertThrowsAsync<OverflowException>(
        () => repository.ReplaceLayoutAsync(
            new StationLayoutWriteRequest(
                "scope", "rollback", BuildDocument(2), long.MaxValue, "admin"),
            CancellationToken.None),
        "revision overflow rolls back");

    var nodes = database.Query<NodeRow>(
        "SELECT ID, X, Y FROM node WHERE InstanceID = 'scope' AND StationSchemeID = 'rollback'") ?? [];
    Assert(nodes.Count == 1 && nodes[0].ID == 77 && nodes[0].X == 7,
        "failed replace rolls back deleted rows");
    var revision = (database.Query<RevisionRow>(
            "SELECT Revision FROM stationlayoutrevision " +
            "WHERE InstanceID = 'scope' AND StationSchemeID = 'rollback'") ?? [])
        .Single().Revision;
    Assert(revision == long.MaxValue, "failed replace rolls back revision");
}

async Task TestAuthorizationAsync()
{
    var authorization = new LegacyStationLayoutAuthorization();
    var owner = Principal("owner", "User");
    var ownerView = await authorization.AuthorizeScopeAsync(
        owner, "scope", StationLayoutPermission.View, CancellationToken.None);
    Assert(ownerView.Status == StationLayoutAccessStatus.Allowed, "owner can view");
    var ownerSave = await authorization.AuthorizeScopeAsync(
        owner, "scope", StationLayoutPermission.SaveLayout, CancellationToken.None);
    Assert(ownerSave.Status == StationLayoutAccessStatus.Forbidden, "legacy save remains admin-only");
    var ownerSaveMissing = await authorization.AuthorizeScopeAsync(
        owner, "missing", StationLayoutPermission.SaveLayout, CancellationToken.None);
    Assert(ownerSaveMissing.Status == StationLayoutAccessStatus.Forbidden,
        "legacy role denial precedes missing instance lookup");
    var nonOwnerView = await authorization.AuthorizeScopeAsync(
        Principal("not-owner", "User"),
        "scope",
        StationLayoutPermission.View,
        CancellationToken.None);
    Assert(nonOwnerView.Status == StationLayoutAccessStatus.Unauthenticated,
        "legacy authenticated non-owner remains HTTP 401");

    var admin = Principal("another-user", "Auditor", "Admin");
    Assert(admin.IsInRole("Admin"), "multiple role claims support IsInRole");
    var adminSave = await authorization.AuthorizeScopeAsync(
        admin, "scope", StationLayoutPermission.SaveLayout, CancellationToken.None);
    Assert(adminSave.Status == StationLayoutAccessStatus.Allowed, "admin role can save");
    var adminImport = await authorization.AuthorizeGlobalAsync(
        admin, StationLayoutPermission.ImportDwg, CancellationToken.None);
    Assert(adminImport.Status == StationLayoutAccessStatus.Allowed, "admin role can import DWG");
}

async Task TestArtifactSinkAsync()
{
    var contentRoot = Path.Combine(testRoot, "content-root");
    Directory.CreateDirectory(contentRoot);
    var environment = new TestEnvironment(contentRoot);
    var sink = new LegacyStationLayoutArtifactSink(environment);
    await sink.OnDwgExtractedAsync(
        new StationLayoutDwgArtifact("layout.dwg", "0", BuildDocument(2)),
        CancellationToken.None);
    var expected = Path.Combine(contentRoot, "LocalData", "Capacity", "StationLayout.json");
    Assert(File.Exists(expected), "legacy artifact path is ContentRoot/LocalData/Capacity/StationLayout.json");
}

void TestDependencyInjection(IConfiguration configuration)
{
    var services = new ServiceCollection();
    services.AddSingleton(configuration);
    services.AddSingleton<SnowflakeIdGenerator>();
    services.AddSingleton<IWebHostEnvironment>(new TestEnvironment(testRoot));
    services.AddSwitchYardStationLayoutLegacyHost();
    using var provider = services.BuildServiceProvider(
        new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    using var scope = provider.CreateScope();
    Assert(scope.ServiceProvider.GetRequiredService<IStationLayoutRepository>()
        is LegacyStationLayoutRepository, "legacy repository DI");
    Assert(scope.ServiceProvider.GetRequiredService<IStationLayoutAuthorization>()
        is LegacyStationLayoutAuthorization, "legacy authorization DI");
    Assert(scope.ServiceProvider.GetRequiredService<IStationLayoutService>() is not null,
        "shared application service DI");
}

StationLayoutDocument BuildDocument(int nodeCount)
{
    var document = new StationLayoutDocument
    {
        Metadata = new StationLayoutMetadata(),
        Nodes = Enumerable.Range(0, nodeCount)
            .Select(index => new StationLayoutNode
            {
                ID = index.ToString(),
                X = index * 10,
                Y = 0
            }).ToList()
    };
    document.Tracks = Enumerable.Range(0, Math.Max(0, nodeCount - 1))
        .Select(index => new StationLayoutTrack
        {
            ID = (100 + index).ToString(),
            FromNodeID = index.ToString(),
            ToNodeID = (index + 1).ToString(),
            X1 = index * 10,
            Y1 = 0,
            X2 = (index + 1) * 10,
            Y2 = 0,
            ArrowDirection = "e"
        }).ToList();
    if (nodeCount >= 2)
    {
        document.Curves.Add(new StationLayoutCurve
        {
            ID = "curve-a",
            NodeID = "1",
            TangentLinkID1 = "100",
            Radius = 100.5,
            Start = new StationLayoutPosition { X = 0, Y = 0 },
            End = new StationLayoutPosition { X = 10, Y = 0 },
            Center = new StationLayoutPosition { X = 5, Y = 5 }
        });
        document.Signals.Add(new StationLayoutSignal
        {
            ID = "signal-a",
            Name = "X",
            BindingNodeID = "0",
            Position = new StationLayoutPosition { X = 0, Y = 0 }
        });
        document.InsulationJoints.Add(new StationLayoutInsulationJoint
        {
            ID = "joint-a",
            BindingNodeID = "1",
            Position = new StationLayoutPosition { X = 10, Y = 0 }
        });
        document.BufferStops.Add(new StationLayoutBufferStop
        {
            ID = "buffer-a",
            Type = "extended",
            Direction = "L",
            BindingNodeID = "1",
            Position = new StationLayoutPosition { X = 10, Y = 0 }
        });
        document.Platforms.Add(new StationLayoutPlatform
        {
            ID = "platform-a",
            Name = "站台",
            X = 1,
            Y = 2,
            Width = 3,
            Height = 4
        });
        document.Switches.Add(new StationLayoutSwitch
        {
            ID = "switch-a",
            Name = "1号",
            BindingNodeID = "1",
            Position = new StationLayoutPosition { X = 10, Y = 0 },
            BranchVectorList =
            [
                new StationLayoutSwitchBranch { X = 1, Y = 0, LineID = "100" }
            ]
        });
        document.Cells.Add(new StationLayoutCell
        {
            ID = "cell-a",
            Name = "区段",
            LinkIDList = "100"
        });
        document.Annotations.Add(new StationLayoutAnnotation
        {
            ID = "annotation-a",
            Text = "文字",
            Position = new StationLayoutPosition { X = 1, Y = 1 }
        });
    }
    return document;
}

ClaimsPrincipal Principal(string name, params string[] roles) =>
    new(new ClaimsIdentity(
        new[] { new Claim(ClaimTypes.Name, name) }
            .Concat(roles.Select(role => new Claim(ClaimTypes.Role, role))),
        "test"));

async Task AssertThrowsAsync<TException>(Func<Task> action, string name)
    where TException : Exception
{
    try
    {
        await action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException(
        $"Integration assertion failed: {name}; expected {typeof(TException).Name}.");
}

void Assert(bool condition, string name)
{
    if (!condition)
    {
        throw new InvalidOperationException($"Integration assertion failed: {name}");
    }
}

file sealed class SequentialIdGenerator : IStationLayoutIdGenerator
{
    private long _value;

    public string NextId() => $"generated-{Interlocked.Increment(ref _value)}";
}

file sealed class TestEnvironment : IWebHostEnvironment
{
    public TestEnvironment(string contentRoot)
    {
        ContentRootPath = contentRoot;
        ContentRootFileProvider = new PhysicalFileProvider(contentRoot);
    }

    public string ApplicationName { get; set; } = "StationLayout.IntegrationTests";
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    public string WebRootPath { get; set; } = string.Empty;
    public string EnvironmentName { get; set; } = Environments.Development;
    public string ContentRootPath { get; set; }
    public IFileProvider ContentRootFileProvider { get; set; }
}

file sealed class NameRow
{
    public string Name { get; set; } = string.Empty;
}

file sealed class PragmaColumn
{
    public string Name { get; set; } = string.Empty;
    public int Pk { get; set; }
}

file sealed class RevisionRow
{
    public long Revision { get; set; }
}

file sealed class NodeRow
{
    public int ID { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
}
