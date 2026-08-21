using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwitchYard.StationLayout;

await RunServiceContractTestsAsync();
await RunHttpContractTestsAsync();

Console.WriteLine(
    $"SwitchYard.StationLayout smoke tests passed on {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}.");

static async Task RunServiceContractTestsAsync()
{
    var repository = new InMemoryRepository();
    var service = new StationLayoutService(
        repository,
        new TestAuthorization(),
        new SequentialIdGenerator());
    var user = SmokeUser();

    var empty = await service.GetJsonAsync(user, "empty-scope", null);
    Assert(!empty.Exists && empty.Revision == 0, "legacy empty read contract");
    Assert(empty.Document.Tracks.Count == 0 && empty.Document.Nodes.Count == 0, "legacy empty document");
    Assert(empty.Document.Metadata?.GridSettings is not null, "legacy default grid settings");
    Assert(empty.Document.Metadata?.Revision == 0, "empty document body revision");

    var created = await service.CreateStationSchemeAsync(
        user,
        new StationSchemeCreateRequest { InstanceID = "scope-1", Name = "默认方案" });
    Assert(created.ID == "scheme-1", "scheme creation");

    var document = BuildRouteDocument();
    var saved = await service.SaveJsonAsync(
        user,
        new StationLayoutSaveRequest
        {
            InstanceID = "scope-1",
            StationSchemeID = created.ID,
            ExpectedRevision = 0,
            Json = JsonSerializer.Serialize(document)
        });
    Assert(saved.Revision == 1, "revision increment");
    Assert(saved.NodeCount == 3 && saved.LinkCount == 2, "entity counts");

    var loaded = await service.GetJsonAsync(user, "scope-1", created.ID);
    Assert(loaded.Exists && loaded.Revision == 1, "load exposes revision");
    Assert(loaded.Document.Metadata?.InstanceID == "scope-1", "scope normalization");
    Assert(loaded.Document.Metadata?.StationSchemeID == created.ID, "scheme normalization");
    Assert(loaded.Document.Metadata?.Revision == 1, "persisted document body revision");
    Assert(loaded.Document.Cells.Single().InstanceID == "scope-1", "cell scope normalization");
    Assert(
        loaded.Document.Nodes.Single(node => node.ID == "2").AdjacentLineIDList.SequenceEqual(["11", "12"]),
        "node adjacency is restored from track endpoints");
    Assert(
        loaded.Document.Switches.Single().BranchVectorList.Count == 2,
        "switch branches survive save and load");

    var routes = await service.SearchRoutesAsync(
        user,
        new StationRouteSearchRequest
        {
            InstanceID = "scope-1",
            StationSchemeID = created.ID,
            StartNodeId = 1,
            EndNodeId = 3
        });
    Assert(routes.Routes.Count == 1, "route count");
    Assert(routes.Routes[0].NodeIds.SequenceEqual([1, 2, 3]), "route node order");
    Assert(routes.Routes[0].LinkIds.SequenceEqual([11, 12]), "route link order");
    Assert(routes.Routes[0].CellIds.SequenceEqual(["C1"]), "route cell mapping");
    Assert(routes.Routes[0].SwitchIds.SequenceEqual(["S1"]), "route switch mapping");
    Assert(routes.Routes[0].SignalIds.SequenceEqual(["X1"]), "route signal mapping");

    await AssertThrowsAsync<StationLayoutConflictException>(
        () => service.SaveJsonAsync(
            user,
            new StationLayoutSaveRequest
            {
                InstanceID = "scope-1",
                StationSchemeID = created.ID,
                ExpectedRevision = 0,
                Json = JsonSerializer.Serialize(document)
            }),
        "direct CAS conflict");

    var automatic = await service.SaveJsonAsync(
        user,
        new StationLayoutSaveRequest
        {
            InstanceID = "auto-scope",
            ExpectedRevision = 0,
            Json = JsonSerializer.Serialize(new StationLayoutDocument())
        });
    Assert(automatic.StationSchemeID == "station_layout_scheme", "legacy automatic default scheme");
    Assert(await repository.SchemeExistsAsync(
        "auto-scope",
        "station_layout_scheme",
        CancellationToken.None), "automatic scheme persisted through host repository");
}

static async Task RunHttpContractTestsAsync()
{
    var repository = new InMemoryRepository();
    var authorization = new TestAuthorization();
    var builder = WebApplication.CreateBuilder();
    builder.Logging.ClearProviders();
    builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
    builder.Services.AddSingleton<IStationLayoutRepository>(repository);
    builder.Services.AddSingleton<IStationLayoutAuthorization>(authorization);
    builder.Services.AddSingleton<IStationLayoutIdGenerator, SequentialIdGenerator>();
    builder.Services
        .AddAuthentication(SmokeAuthenticationHandler.SchemeName)
        .AddScheme<AuthenticationSchemeOptions, SmokeAuthenticationHandler>(
            SmokeAuthenticationHandler.SchemeName,
            _ => { });
    builder.Services.AddAuthorization();
    builder.Services.AddSwitchYardStationLayout();

    var app = builder.Build();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapSwitchYardStationLayout();
    await app.StartAsync();
    try
    {
        var server = app.Services.GetRequiredService<IServer>();
        var address = server.Features.Get<IServerAddressesFeature>()?.Addresses.Single()
                      ?? throw new InvalidOperationException("Kestrel did not publish an address.");
        using var client = new HttpClient { BaseAddress = new Uri(address) };

        var emptyResponse = await client.PostAsync(
            "/StationLayout/GetJson?instanceID=http-empty",
            content: null);
        await AssertStatusAsync(emptyResponse, HttpStatusCode.OK, "empty GetJson");
        Assert(emptyResponse.Headers.ETag?.Tag == "\"0\"", "empty ETag revision");
        Assert(Header(emptyResponse, "X-Station-Layout-Revision") == "0", "empty revision header");
        Assert(Header(emptyResponse, "X-Station-Layout-Exists") == "false", "empty existence header");
        var emptyDocument = await emptyResponse.Content.ReadFromJsonAsync<StationLayoutDocument>();
        Assert(emptyDocument?.Nodes.Count == 0 && emptyDocument.Tracks.Count == 0, "empty HTTP document");
        Assert(emptyDocument?.Metadata?.Revision == 0, "empty HTTP body revision");

        var createResponse = await client.PostAsJsonAsync(
            "/StationLayout/CreateStationScheme",
            new StationSchemeCreateRequest { InstanceID = "http-scope", Name = "初始方案" });
        await AssertStatusAsync(createResponse, HttpStatusCode.OK, "CreateStationScheme");
        var created = await createResponse.Content.ReadFromJsonAsync<StationSchemeDto>()
                      ?? throw new InvalidOperationException("Create response was empty.");

        var listResponse = await client.GetAsync(
            "/StationLayout/GetStationSchemes?instanceID=http-scope");
        await AssertStatusAsync(listResponse, HttpStatusCode.OK, "GetStationSchemes");
        var listed = await listResponse.Content.ReadFromJsonAsync<List<StationSchemeDto>>();
        Assert(listed?.Count == 1 && listed[0].ID == created.ID, "HTTP scheme list");

        var editResponse = await client.PutAsJsonAsync(
            "/StationLayout/EditStationScheme",
            new StationSchemeUpdateRequest
            {
                InstanceID = "http-scope",
                OriginalID = created.ID,
                Name = "改名方案"
            });
        await AssertStatusAsync(editResponse, HttpStatusCode.OK, "EditStationScheme");
        var edited = await editResponse.Content.ReadFromJsonAsync<StationSchemeDto>();
        Assert(edited?.Name == "改名方案", "HTTP scheme edit");

        var missingEdit = await client.PutAsJsonAsync(
            "/StationLayout/EditStationScheme",
            new StationSchemeUpdateRequest
            {
                InstanceID = "http-scope",
                OriginalID = "missing",
                Name = "missing"
            });
        await AssertStatusAsync(missingEdit, HttpStatusCode.NotFound, "missing scheme maps 404");

        var deleteResponse = await client.DeleteAsync(
            $"/StationLayout/DeleteStationScheme?instanceID=http-scope&stationSchemeID={Uri.EscapeDataString(created.ID)}");
        await AssertStatusAsync(deleteResponse, HttpStatusCode.OK, "DeleteStationScheme");
        var missingDelete = await client.DeleteAsync(
            $"/StationLayout/DeleteStationScheme?instanceID=http-scope&stationSchemeID={Uri.EscapeDataString(created.ID)}");
        await AssertStatusAsync(missingDelete, HttpStatusCode.NotFound, "deleted scheme maps 404");

        var routeDocument = BuildRouteDocument();
        var routeJson = JsonSerializer.Serialize(routeDocument);
        var saveResponse = await client.PostAsJsonAsync(
            "/StationLayout/SaveJson",
            new StationLayoutSaveRequest
            {
                InstanceID = "http-auto",
                ExpectedRevision = 0,
                Json = routeJson
            });
        await AssertStatusAsync(saveResponse, HttpStatusCode.OK, "SaveJson automatic default");
        Assert(saveResponse.Headers.ETag?.Tag == "\"1\"", "save ETag revision");
        var saveResult = await saveResponse.Content.ReadFromJsonAsync<StationLayoutSaveResult>();
        Assert(saveResult?.StationSchemeID == "station_layout_scheme", "HTTP automatic default scheme");

        var loadedResponse = await client.PostAsync(
            "/StationLayout/GetJson?instanceID=http-auto",
            content: null);
        await AssertStatusAsync(loadedResponse, HttpStatusCode.OK, "GetJson persisted");
        Assert(loadedResponse.Headers.ETag?.Tag == "\"1\"", "load ETag revision");
        Assert(Header(loadedResponse, "X-Station-Layout-Exists") == "true", "persisted existence header");
        var loadedDocument = await loadedResponse.Content.ReadFromJsonAsync<StationLayoutDocument>();
        Assert(loadedDocument?.Metadata?.Revision == 1, "persisted HTTP body revision");
        Assert(
            loadedDocument?.Nodes.Single(node => node.ID == "2").AdjacentLineIDList.SequenceEqual(["11", "12"]) == true,
            "HTTP load restores node adjacency");
        Assert(
            loadedDocument?.Switches.Single().BranchVectorList.Count == 2,
            "HTTP load preserves switch branches");

        using var staleRequest = new HttpRequestMessage(HttpMethod.Post, "/StationLayout/SaveJson")
        {
            Content = JsonContent.Create(new StationLayoutSaveRequest
            {
                InstanceID = "http-auto",
                StationSchemeID = "station_layout_scheme",
                Json = routeJson
            })
        };
        staleRequest.Headers.TryAddWithoutValidation("If-Match", "\"0\"");
        var staleResponse = await client.SendAsync(staleRequest);
        await AssertStatusAsync(staleResponse, HttpStatusCode.Conflict, "stale If-Match maps 409");

        var searchResponse = await client.PostAsJsonAsync(
            "/StationLayout/SearchRoutes?instanceID=http-auto&stationSchemeID=station_layout_scheme",
            new StationRouteSearchRequest
            {
                InstanceID = "body-is-overridden",
                StartNodeId = 1,
                EndNodeId = 3
            });
        await AssertStatusAsync(searchResponse, HttpStatusCode.OK, "SearchRoutes");
        var search = await searchResponse.Content.ReadFromJsonAsync<StationRouteSearchResponse>();
        Assert(search?.Routes.Count == 1, "HTTP route search");

        using var emptyMultipart = new MultipartFormDataContent();
        var dwgResponse = await client.PostAsync(
            "/StationLayout/ExtractDwgFile",
            emptyMultipart);
        await AssertStatusAsync(dwgResponse, HttpStatusCode.BadRequest, "ExtractDwgFile validation");

        using var anonymousRequest = new HttpRequestMessage(
            HttpMethod.Get,
            "/StationLayout/GetStationSchemes?instanceID=http-scope");
        anonymousRequest.Headers.Add("X-Smoke-Authentication", "anonymous");
        var unauthenticated = await client.SendAsync(anonymousRequest);
        await AssertStatusAsync(unauthenticated, HttpStatusCode.Unauthorized, "unauthenticated maps 401");

        authorization.ScopeStatus = StationLayoutAccessStatus.Forbidden;
        var forbidden = await client.GetAsync(
            "/StationLayout/GetStationSchemes?instanceID=http-scope");
        await AssertStatusAsync(forbidden, HttpStatusCode.Forbidden, "forbidden maps 403");

        authorization.ScopeStatus = StationLayoutAccessStatus.NotFound;
        var missingScope = await client.GetAsync(
            "/StationLayout/GetStationSchemes?instanceID=missing-scope");
        await AssertStatusAsync(missingScope, HttpStatusCode.NotFound, "missing scope maps 404");

        authorization.ScopeStatus = StationLayoutAccessStatus.Allowed;
        using var malformed = new StringContent("{", Encoding.UTF8, "application/json");
        var malformedResponse = await client.PostAsync(
            "/StationLayout/CreateStationScheme",
            malformed);
        await AssertStatusAsync(malformedResponse, HttpStatusCode.BadRequest, "malformed JSON maps 400");
    }
    finally
    {
        await app.StopAsync();
        await app.DisposeAsync();
    }
}

static ClaimsPrincipal SmokeUser() => new(new ClaimsIdentity(
    [new Claim(ClaimTypes.Name, "smoke-test")],
    "smoke"));

static StationLayoutDocument BuildRouteDocument() => new()
{
    Metadata = new StationLayoutMetadata { LatestElementID = 12 },
    Nodes =
    [
        new StationLayoutNode { ID = "1", X = 0, Y = 0 },
        new StationLayoutNode { ID = "2", X = 100, Y = 0 },
        new StationLayoutNode { ID = "3", X = 200, Y = 0 }
    ],
    Tracks =
    [
        new StationLayoutTrack { ID = "11", FromNodeID = "1", ToNodeID = "2" },
        new StationLayoutTrack { ID = "12", FromNodeID = "2", ToNodeID = "3" }
    ],
    Switches =
    [
        new StationLayoutSwitch
        {
            ID = "S1",
            BindingNodeID = "2",
            BranchVectorList =
            [
                new StationLayoutSwitchBranch { X = -100, Y = 0, LineID = "11" },
                new StationLayoutSwitchBranch { X = 100, Y = 0, LineID = "12" }
            ]
        }
    ],
    Signals =
    [
        new StationLayoutSignal { ID = "X1", BindingNodeID = "1" }
    ],
    Cells =
    [
        new StationLayoutCell { ID = "C1", LinkIDList = "11,12" }
    ]
};

static async Task AssertStatusAsync(
    HttpResponseMessage response,
    HttpStatusCode expected,
    string name)
{
    if (response.StatusCode == expected)
    {
        return;
    }

    var body = await response.Content.ReadAsStringAsync();
    throw new InvalidOperationException(
        $"Smoke assertion failed: {name}; expected {(int)expected}, got {(int)response.StatusCode}: {body}");
}

static string? Header(HttpResponseMessage response, string name) =>
    response.Headers.TryGetValues(name, out var values) ? values.SingleOrDefault() : null;

static async Task AssertThrowsAsync<TException>(Func<Task> action, string name)
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
        $"Smoke assertion failed: {name}; expected {typeof(TException).Name}.");
}

static void Assert(bool condition, string name)
{
    if (!condition)
    {
        throw new InvalidOperationException($"Smoke assertion failed: {name}");
    }
}

file sealed class SequentialIdGenerator : IStationLayoutIdGenerator
{
    private int _next;

    public string NextId() => $"scheme-{Interlocked.Increment(ref _next)}";
}

file sealed class SmokeAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Smoke";

    public SmokeAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (string.Equals(
                Request.Headers["X-Smoke-Authentication"].ToString(),
                "anonymous",
                StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "smoke-http")],
            SchemeName);
        var principal = new ClaimsPrincipal(identity);
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(principal, SchemeName)));
    }
}

file sealed class TestAuthorization : IStationLayoutAuthorization
{
    public StationLayoutAccessStatus ScopeStatus { get; set; } = StationLayoutAccessStatus.Allowed;

    public StationLayoutAccessStatus GlobalStatus { get; set; } = StationLayoutAccessStatus.Allowed;

    public ValueTask<StationLayoutAccessDecision> AuthorizeScopeAsync(
        ClaimsPrincipal user,
        string scopeId,
        StationLayoutPermission permission,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(Decision(ScopeStatus));

    public ValueTask<StationLayoutAccessDecision> AuthorizeGlobalAsync(
        ClaimsPrincipal user,
        StationLayoutPermission permission,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(Decision(GlobalStatus));

    private static StationLayoutAccessDecision Decision(StationLayoutAccessStatus status) =>
        status switch
        {
            StationLayoutAccessStatus.Allowed => StationLayoutAccessDecision.Allow(),
            StationLayoutAccessStatus.Unauthenticated => StationLayoutAccessDecision.Unauthenticated(),
            StationLayoutAccessStatus.Forbidden => StationLayoutAccessDecision.Forbid(),
            StationLayoutAccessStatus.NotFound => StationLayoutAccessDecision.Missing(),
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };
}

file sealed class InMemoryRepository : IStationLayoutRepository
{
    private static readonly JsonSerializerOptions CloneOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly object _gate = new();
    private readonly Dictionary<(string Scope, string Scheme), StationLayoutRecord> _layouts = [];
    private readonly Dictionary<(string Scope, string Scheme), StationSchemeRecord> _schemes = [];

    public Task<IReadOnlyList<StationSchemeRecord>> ListSchemesAsync(
        string scopeId,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<StationSchemeRecord>>(
                _schemes.Values.Where(item => item.ScopeId == scopeId).ToList());
        }
    }

    public Task<StationLayoutRecord?> LoadAsync(
        string scopeId,
        string? requestedSchemeId,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var schemeId = requestedSchemeId ?? _schemes.Values
                .Where(item => item.ScopeId == scopeId)
                .OrderByDescending(item => item.IsDefault)
                .FirstOrDefault()?.SchemeId;
            if (schemeId is null || !_layouts.TryGetValue((scopeId, schemeId), out var layout))
            {
                return Task.FromResult<StationLayoutRecord?>(null);
            }

            return Task.FromResult<StationLayoutRecord?>(
                new StationLayoutRecord(layout.Scheme, Clone(layout.Document)));
        }
    }

    public Task<bool> SchemeExistsAsync(
        string scopeId,
        string schemeId,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult(_schemes.ContainsKey((scopeId, schemeId)));
        }
    }

    public Task<bool> TryCreateSchemeAsync(
        StationSchemeRecord scheme,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var key = (scheme.ScopeId, scheme.SchemeId);
            if (!_schemes.TryAdd(key, scheme))
            {
                return Task.FromResult(false);
            }

            _layouts[key] = new StationLayoutRecord(scheme, new StationLayoutDocument());
            return Task.FromResult(true);
        }
    }

    public Task<bool> RenameSchemeAsync(
        string scopeId,
        string schemeId,
        string name,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var key = (scopeId, schemeId);
            if (!_schemes.TryGetValue(key, out var scheme))
            {
                return Task.FromResult(false);
            }

            var renamed = scheme with { Name = name };
            _schemes[key] = renamed;
            if (_layouts.TryGetValue(key, out var layout))
            {
                _layouts[key] = layout with { Scheme = renamed };
            }

            return Task.FromResult(true);
        }
    }

    public Task<bool> DeleteSchemeAsync(
        string scopeId,
        string schemeId,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _layouts.Remove((scopeId, schemeId));
            return Task.FromResult(_schemes.Remove((scopeId, schemeId)));
        }
    }

    public Task<StationLayoutWriteResult> ReplaceLayoutAsync(
        StationLayoutWriteRequest request,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var key = (request.ScopeId, request.SchemeId);
            if (!_schemes.TryGetValue(key, out var scheme))
            {
                throw new StationLayoutNotFoundException("scheme not found");
            }

            if (request.ExpectedRevision.HasValue && request.ExpectedRevision.Value != scheme.Revision)
            {
                throw new StationLayoutConflictException("revision conflict");
            }

            scheme = scheme with { Revision = checked(scheme.Revision + 1) };
            _schemes[key] = scheme;
            _layouts[key] = new StationLayoutRecord(scheme, Clone(request.Document));
            return Task.FromResult(new StationLayoutWriteResult(request.SchemeId, scheme.Revision));
        }
    }

    private static StationLayoutDocument Clone(StationLayoutDocument document) =>
        JsonSerializer.Deserialize<StationLayoutDocument>(
            JsonSerializer.Serialize(document, CloneOptions),
            CloneOptions) ?? throw new InvalidOperationException("clone failed");
}
