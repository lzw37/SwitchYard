using System.Security.Claims;
using System.Text.Json;

namespace SwitchYard.StationLayout;

public interface IStationLayoutService
{
    Task<IReadOnlyList<StationSchemeDto>> GetStationSchemesAsync(
        ClaimsPrincipal user,
        string scopeId,
        CancellationToken cancellationToken = default);

    Task<StationSchemeDto> CreateStationSchemeAsync(
        ClaimsPrincipal user,
        StationSchemeCreateRequest request,
        CancellationToken cancellationToken = default);

    Task<StationSchemeDto> EditStationSchemeAsync(
        ClaimsPrincipal user,
        StationSchemeUpdateRequest request,
        CancellationToken cancellationToken = default);

    Task<StationSchemeDto> CopyStationSchemeAsync(
        ClaimsPrincipal user,
        StationSchemeCopyRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteStationSchemeAsync(
        ClaimsPrincipal user,
        string scopeId,
        string schemeId,
        CancellationToken cancellationToken = default);

    Task<StationLayoutReadResult> GetJsonAsync(
        ClaimsPrincipal user,
        string scopeId,
        string? schemeId,
        CancellationToken cancellationToken = default);

    Task<StationLayoutSaveResult> SaveJsonAsync(
        ClaimsPrincipal user,
        StationLayoutSaveRequest request,
        CancellationToken cancellationToken = default);

    Task<StationRouteSearchResponse> SearchRoutesAsync(
        ClaimsPrincipal user,
        StationRouteSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<StationLayoutDwgResult> ExtractDwgAsync(
        ClaimsPrincipal user,
        Stream stream,
        string fileName,
        string? layerName,
        CancellationToken cancellationToken = default);
}

public sealed class StationLayoutService : IStationLayoutService
{
    private const int MaximumSchemeCreateAttempts = 10;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IStationLayoutRepository _repository;
    private readonly IStationLayoutAuthorization _authorization;
    private readonly IStationLayoutIdGenerator _idGenerator;
    private readonly IStationLayoutArtifactSink _artifactSink;
    private readonly StationLayoutModuleOptions _options;

    public StationLayoutService(
        IStationLayoutRepository repository,
        IStationLayoutAuthorization authorization,
        IStationLayoutIdGenerator idGenerator,
        IStationLayoutArtifactSink? artifactSink = null,
        StationLayoutModuleOptions? options = null)
    {
        _repository = repository;
        _authorization = authorization;
        _idGenerator = idGenerator;
        _artifactSink = artifactSink ?? new NullStationLayoutArtifactSink();
        _options = options ?? new StationLayoutModuleOptions();
    }

    public async Task<IReadOnlyList<StationSchemeDto>> GetStationSchemesAsync(
        ClaimsPrincipal user,
        string scopeId,
        CancellationToken cancellationToken = default)
    {
        scopeId = RequireId(scopeId, "instanceID");
        await AuthorizeScopeAsync(user, scopeId, StationLayoutPermission.View, cancellationToken);
        var schemes = await _repository.ListSchemesAsync(scopeId, cancellationToken);
        return schemes
            .Select(item => new StationSchemeDto(item.SchemeId, item.Name, item.Revision))
            .ToList();
    }

    public async Task<StationSchemeDto> CreateStationSchemeAsync(
        ClaimsPrincipal user,
        StationSchemeCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var scopeId = RequireId(request.InstanceID, "instanceID");
        await AuthorizeScopeAsync(user, scopeId, StationLayoutPermission.ManageSchemes, cancellationToken);

        for (var attempt = 0; attempt < MaximumSchemeCreateAttempts; attempt++)
        {
            var schemeId = RequireId(_idGenerator.NextId(), "generated stationSchemeID");
            var name = NormalizeName(request.Name, schemeId);
            var scheme = new StationSchemeRecord(scopeId, schemeId, name);
            if (await _repository.TryCreateSchemeAsync(scheme, cancellationToken))
            {
                return new StationSchemeDto(schemeId, name);
            }
        }

        throw new StationLayoutConflictException(
            "Could not allocate a unique station scheme ID.");
    }

    public async Task<StationSchemeDto> CopyStationSchemeAsync(
        ClaimsPrincipal user,
        StationSchemeCopyRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var scopeId = RequireId(request.InstanceID, "instanceID");
        var sourceSchemeId = RequireId(request.SourceStationSchemeID, "sourceStationSchemeID");
        await AuthorizeScopeAsync(user, scopeId, StationLayoutPermission.ManageSchemes, cancellationToken);
        var source = (await _repository.ListSchemesAsync(scopeId, cancellationToken))
            .FirstOrDefault(item => string.Equals(item.SchemeId, sourceSchemeId, StringComparison.Ordinal));
        if (source is null)
        {
            throw new StationLayoutNotFoundException($"Station scheme '{sourceSchemeId}' was not found.");
        }

        var defaultName = source.Name + " 副本";
        if (defaultName.Length > _options.MaximumSchemeNameLength)
            defaultName = defaultName[.._options.MaximumSchemeNameLength];
        var name = NormalizeName(request.Name, defaultName);
        for (var attempt = 0; attempt < MaximumSchemeCreateAttempts; attempt++)
        {
            var schemeId = RequireId(_idGenerator.NextId(), "generated stationSchemeID");
            var target = new StationSchemeRecord(scopeId, schemeId, name);
            if (await _repository.TryCopySchemeAsync(sourceSchemeId, target, cancellationToken))
            {
                return new StationSchemeDto(schemeId, name);
            }
        }

        throw new StationLayoutConflictException("Could not allocate a unique station scheme ID.");
    }

    public async Task<StationSchemeDto> EditStationSchemeAsync(
        ClaimsPrincipal user,
        StationSchemeUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var scopeId = RequireId(request.InstanceID, "instanceID");
        var schemeId = RequireId(request.OriginalID, "originalID");
        var name = NormalizeName(request.Name, schemeId);
        await AuthorizeScopeAsync(user, scopeId, StationLayoutPermission.ManageSchemes, cancellationToken);

        if (!await _repository.RenameSchemeAsync(scopeId, schemeId, name, cancellationToken))
        {
            throw new StationLayoutNotFoundException(
                $"Station scheme '{schemeId}' was not found.");
        }

        var scheme = (await _repository.ListSchemesAsync(scopeId, cancellationToken))
            .FirstOrDefault(item => string.Equals(item.SchemeId, schemeId, StringComparison.Ordinal));
        return new StationSchemeDto(schemeId, name, scheme?.Revision ?? 0);
    }

    public async Task DeleteStationSchemeAsync(
        ClaimsPrincipal user,
        string scopeId,
        string schemeId,
        CancellationToken cancellationToken = default)
    {
        scopeId = RequireId(scopeId, "instanceID");
        schemeId = RequireId(schemeId, "stationSchemeID");
        await AuthorizeScopeAsync(user, scopeId, StationLayoutPermission.ManageSchemes, cancellationToken);

        if (!await _repository.DeleteSchemeAsync(scopeId, schemeId, cancellationToken))
        {
            throw new StationLayoutNotFoundException(
                $"Station scheme '{schemeId}' was not found.");
        }
    }

    public async Task<StationLayoutReadResult> GetJsonAsync(
        ClaimsPrincipal user,
        string scopeId,
        string? schemeId,
        CancellationToken cancellationToken = default)
    {
        scopeId = RequireId(scopeId, "instanceID");
        schemeId = NormalizeOptionalId(schemeId);
        await AuthorizeScopeAsync(user, scopeId, StationLayoutPermission.View, cancellationToken);
        var record = await _repository.LoadAsync(scopeId, schemeId, cancellationToken);
        if (record is null)
        {
            if (!_options.LegacyV1Compatibility)
            {
                throw new StationLayoutNotFoundException(
                    schemeId is null
                        ? "No station scheme is available for this instance."
                        : $"Station scheme '{schemeId}' was not found.");
            }

            var empty = CreateEmptyDocument(scopeId, schemeId);
            empty.Metadata!.Revision = 0;
            return new StationLayoutReadResult(empty, schemeId, 0, false);
        }

        var document = CloneDocument(record.Document);
        NormalizeDocument(document, scopeId, record.Scheme.SchemeId);
        document.Metadata!.Revision = record.Scheme.Revision;
        return new StationLayoutReadResult(
            document,
            record.Scheme.SchemeId,
            record.Scheme.Revision,
            true);
    }

    public async Task<StationLayoutSaveResult> SaveJsonAsync(
        ClaimsPrincipal user,
        StationLayoutSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var document = ParseDocument(request.Json);
        if (request.ExpectedRevision is < 0)
        {
            throw new StationLayoutValidationException("expectedRevision cannot be negative.");
        }

        var scopeId = RequireId(
            NormalizeOptionalId(request.InstanceID) ??
            NormalizeOptionalId(document.Metadata?.InstanceID),
            "instanceID");
        await AuthorizeScopeAsync(user, scopeId, StationLayoutPermission.SaveLayout, cancellationToken);
        var schemeId = NormalizeOptionalId(request.StationSchemeID) ??
                       NormalizeOptionalId(document.Metadata?.StationSchemeID);
        if (schemeId is null)
        {
            var available = await _repository.ListSchemesAsync(scopeId, cancellationToken);
            schemeId = available.FirstOrDefault(item => item.IsDefault)?.SchemeId ??
                       available.FirstOrDefault()?.SchemeId;
        }

        schemeId ??= RequireId(_options.DefaultSchemeId, "default stationSchemeID");
        schemeId = RequireId(schemeId, "stationSchemeID");
        if (!await _repository.SchemeExistsAsync(scopeId, schemeId, cancellationToken))
        {
            if (!_options.LegacyV1Compatibility)
            {
                throw new StationLayoutNotFoundException(
                    $"Station scheme '{schemeId}' was not found.");
            }

            var defaultName = NormalizeName(_options.DefaultSchemeName, schemeId);
            var created = await _repository.TryCreateSchemeAsync(
                new StationSchemeRecord(scopeId, schemeId, defaultName, IsDefault: true),
                cancellationToken);
            if (!created && !await _repository.SchemeExistsAsync(scopeId, schemeId, cancellationToken))
            {
                throw new StationLayoutConflictException(
                    $"Station scheme '{schemeId}' could not be created.");
            }
        }

        NormalizeDocument(document, scopeId, schemeId);
        var writeResult = await _repository.ReplaceLayoutAsync(
            new StationLayoutWriteRequest(
                scopeId,
                schemeId,
                document,
                request.ExpectedRevision,
                user.Identity?.Name),
            cancellationToken);

        return BuildSaveResult(scopeId, writeResult, document);
    }

    public async Task<StationRouteSearchResponse> SearchRoutesAsync(
        ClaimsPrincipal user,
        StationRouteSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var scopeId = RequireId(request.InstanceID, "instanceID");
        await AuthorizeScopeAsync(user, scopeId, StationLayoutPermission.View, cancellationToken);
        var record = await _repository.LoadAsync(
                scopeId,
                NormalizeOptionalId(request.StationSchemeID),
                cancellationToken)
            ?? throw new StationLayoutNotFoundException("The station layout was not found.");

        var document = CloneDocument(record.Document);
        NormalizeDocument(document, scopeId, record.Scheme.SchemeId);
        return StationLayoutRouteSearcher.Search(
            scopeId,
            record.Scheme.SchemeId,
            document,
            request.StartNodeId,
            request.EndNodeId);
    }

    public async Task<StationLayoutDwgResult> ExtractDwgAsync(
        ClaimsPrincipal user,
        Stream stream,
        string fileName,
        string? layerName,
        CancellationToken cancellationToken = default)
    {
        await AuthorizeGlobalAsync(user, StationLayoutPermission.ImportDwg, cancellationToken);
        var result = StationLayoutDwgImporter.Import(stream, fileName, layerName);
        await _artifactSink.OnDwgExtractedAsync(
            new StationLayoutDwgArtifact(
                fileName,
                string.IsNullOrWhiteSpace(layerName) ? "0" : layerName.Trim(),
                result.Layout),
            cancellationToken);
        return result;
    }

    private async Task AuthorizeScopeAsync(
        ClaimsPrincipal user,
        string scopeId,
        StationLayoutPermission permission,
        CancellationToken cancellationToken)
    {
        var decision = await _authorization.AuthorizeScopeAsync(
            user,
            scopeId,
            permission,
            cancellationToken);
        ThrowIfDenied(decision);
    }

    private async Task AuthorizeGlobalAsync(
        ClaimsPrincipal user,
        StationLayoutPermission permission,
        CancellationToken cancellationToken)
    {
        var decision = await _authorization.AuthorizeGlobalAsync(
            user,
            permission,
            cancellationToken);
        ThrowIfDenied(decision);
    }

    private static void ThrowIfDenied(StationLayoutAccessDecision decision)
    {
        switch (decision.Status)
        {
            case StationLayoutAccessStatus.Allowed:
                return;
            case StationLayoutAccessStatus.Unauthenticated:
                throw new StationLayoutUnauthenticatedException(
                    decision.Message ?? "User is not authenticated.");
            case StationLayoutAccessStatus.Forbidden:
                throw new StationLayoutForbiddenException(
                    decision.Message ?? "Station-layout access is forbidden.");
            case StationLayoutAccessStatus.NotFound:
                throw new StationLayoutNotFoundException(
                    decision.Message ?? "The station-layout scope was not found.");
            default:
                throw new InvalidOperationException(
                    $"Unknown station-layout access status '{decision.Status}'.");
        }
    }

    private static StationLayoutDocument ParseDocument(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new StationLayoutValidationException("Station-layout JSON is required.");
        }

        try
        {
            return JsonSerializer.Deserialize<StationLayoutDocument>(json, JsonOptions)
                   ?? throw new StationLayoutValidationException("Station-layout JSON is empty.");
        }
        catch (JsonException exception)
        {
            throw new StationLayoutValidationException(
                "Station-layout JSON is invalid.",
                exception);
        }
    }

    private static void NormalizeDocument(
        StationLayoutDocument document,
        string scopeId,
        string schemeId)
    {
        document.Metadata ??= new StationLayoutMetadata();
        document.Metadata.InstanceID = scopeId;
        document.Metadata.StationSchemeID = schemeId;
        document.Metadata.GridSettings ??= JsonSerializer.SerializeToElement(new
        {
            showGrid = true,
            spacing = 20,
            originX = 0,
            originY = 0
        });
        document.Tracks ??= [];
        document.Curves ??= [];
        document.Nodes ??= [];
        document.Signals ??= [];
        document.InsulationJoints ??= [];
        document.BufferStops ??= [];
        document.Platforms ??= [];
        document.Switches ??= [];
        document.Cells ??= [];
        document.Annotations ??= [];

        // Match the legacy controller contract: node adjacency is derived from
        // track endpoints. This also repairs documents written by early shared
        // module versions that did not preserve adjacentLineIDList.
        foreach (var node in document.Nodes)
        {
            var nodeId = NormalizeOptionalId(node.ID);
            if (nodeId is null)
            {
                node.AdjacentLineIDList = [];
                continue;
            }

            node.AdjacentLineIDList = document.Tracks
                .Where(track =>
                    string.Equals(NormalizeOptionalId(track.FromNodeID), nodeId, StringComparison.Ordinal) ||
                    string.Equals(NormalizeOptionalId(track.ToNodeID), nodeId, StringComparison.Ordinal))
                .Select(track => NormalizeOptionalId(track.ID))
                .Where(trackId => trackId is not null)
                .Select(trackId => trackId!)
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        foreach (var cell in document.Cells)
        {
            cell.InstanceID = scopeId;
            cell.StationSchemeID = schemeId;
        }
    }

    private static StationLayoutSaveResult BuildSaveResult(
        string scopeId,
        StationLayoutWriteResult writeResult,
        StationLayoutDocument document)
    {
        return new StationLayoutSaveResult(
            "OK",
            scopeId,
            writeResult.SchemeId,
            writeResult.Revision,
            document.Nodes.Count,
            document.Tracks.Count,
            document.Curves.Count,
            document.Signals.Count,
            document.InsulationJoints.Count,
            document.BufferStops.Count,
            document.Platforms.Count,
            document.Switches.Count,
            document.Switches.Sum(item => item.BranchVectorList?.Count ?? 0),
            document.Cells.Count,
            document.Annotations.Count);
    }

    private static string RequireId(string? value, string fieldName)
    {
        var normalized = NormalizeOptionalId(value);
        return normalized ?? throw new StationLayoutValidationException(
            $"{fieldName} is required.");
    }

    private static string? NormalizeOptionalId(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private string NormalizeName(string? value, string fallback)
    {
        var name = value?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            name = fallback;
        }

        if (name.Length > _options.MaximumSchemeNameLength)
        {
            throw new StationLayoutValidationException(
                $"Station scheme name cannot exceed {_options.MaximumSchemeNameLength} characters.");
        }

        return name;
    }

    private static StationLayoutDocument CreateEmptyDocument(
        string scopeId,
        string? schemeId)
    {
        var document = new StationLayoutDocument();
        NormalizeDocument(document, scopeId, schemeId ?? string.Empty);
        if (schemeId is null)
        {
            document.Metadata!.StationSchemeID = null;
        }

        return document;
    }

    private static StationLayoutDocument CloneDocument(StationLayoutDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return JsonSerializer.Deserialize<StationLayoutDocument>(
                   JsonSerializer.Serialize(document, JsonOptions),
                   JsonOptions)
               ?? throw new StationLayoutStoreException(
                   "The station-layout repository returned an invalid document.",
                   new InvalidOperationException("Document cloning returned null."));
    }
}
