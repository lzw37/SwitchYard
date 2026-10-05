using System.Security.Claims;

namespace SwitchYard.StationLayout;

public interface IStationLayoutRepository
{
    /// <summary>
    /// Returns detached scheme snapshots. Implementations must not return mutable tracked state.
    /// </summary>
    Task<IReadOnlyList<StationSchemeRecord>> ListSchemesAsync(
        string scopeId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Returns a detached layout snapshot, including its current revision. When
    /// <paramref name="requestedSchemeId"/> is null, the host applies its established
    /// default-scheme resolution rule.
    /// Read related tables and the revision from one consistent transaction. Preserve
    /// optional-field presence and extension attributes when assembling the document.
    /// </summary>
    Task<StationLayoutRecord?> LoadAsync(
        string scopeId,
        string? requestedSchemeId,
        CancellationToken cancellationToken);

    Task<bool> SchemeExistsAsync(
        string scopeId,
        string schemeId,
        CancellationToken cancellationToken);

    Task<bool> TryCreateSchemeAsync(
        StationSchemeRecord scheme,
        CancellationToken cancellationToken);

    /// <summary>
    /// Atomically copies the source scheme and its scoped data into a new scheme.
    /// Returns false if the target ID is already in use; throws when the source is missing.
    /// The copy starts at revision zero and must not share mutable state with the source.
    /// </summary>
    Task<bool> TryCopySchemeAsync(
        string sourceSchemeId,
        StationSchemeRecord targetScheme,
        CancellationToken cancellationToken);

    Task<bool> RenameSchemeAsync(
        string scopeId,
        string schemeId,
        string name,
        CancellationToken cancellationToken);

    Task<bool> DeleteSchemeAsync(
        string scopeId,
        string schemeId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Atomically replaces a complete layout aggregate. If ExpectedRevision is present,
    /// the implementation must compare it with the persisted revision in the same
    /// transaction as the write, throw StationLayoutConflictException on mismatch, and
    /// increment the revision exactly once on success.
    /// If the host assigns identities, return the persisted Document and per-collection
    /// IdMappings so clients can acknowledge them without discarding newer edits.
    /// </summary>
    Task<StationLayoutWriteResult> ReplaceLayoutAsync(
        StationLayoutWriteRequest request,
        CancellationToken cancellationToken);
}

public interface IStationLayoutAuthorization
{
    ValueTask<StationLayoutAccessDecision> AuthorizeScopeAsync(
        ClaimsPrincipal user,
        string scopeId,
        StationLayoutPermission permission,
        CancellationToken cancellationToken);

    ValueTask<StationLayoutAccessDecision> AuthorizeGlobalAsync(
        ClaimsPrincipal user,
        StationLayoutPermission permission,
        CancellationToken cancellationToken);
}

public interface IStationLayoutIdGenerator
{
    string NextId();
}

public interface IStationLayoutArtifactSink
{
    Task OnDwgExtractedAsync(
        StationLayoutDwgArtifact artifact,
        CancellationToken cancellationToken);
}

public sealed record StationSchemeRecord(
    string ScopeId,
    string SchemeId,
    string Name,
    long Revision = 0,
    bool IsDefault = false);

public sealed record StationLayoutRecord(
    StationSchemeRecord Scheme,
    StationLayoutDocument Document);

public sealed record StationLayoutWriteRequest(
    string ScopeId,
    string SchemeId,
    StationLayoutDocument Document,
    long? ExpectedRevision,
    string? ActorName);

public sealed record StationLayoutWriteResult(
    string SchemeId,
    long Revision)
{
    public StationLayoutDocument? Document { get; init; }
    public Dictionary<string, Dictionary<string, string>> IdMappings { get; init; } = [];
}

public sealed record StationLayoutReadResult(
    StationLayoutDocument Document,
    string? SchemeId,
    long Revision,
    bool Exists);

public sealed record StationLayoutDwgArtifact(
    string FileName,
    string LayerName,
    StationLayoutDocument Layout);

public enum StationLayoutPermission
{
    View,
    ManageSchemes,
    SaveLayout,
    ImportDwg
}

public sealed class StationLayoutModuleOptions
{
    public bool LegacyV1Compatibility { get; set; } = true;

    public string DefaultSchemeId { get; set; } = "station_layout_scheme";

    public string DefaultSchemeName { get; set; } = "车站布置图";

    public int MaximumSchemeNameLength { get; set; } = 100;

    /// <summary>Maximum positional error, in layout coordinates, for an unambiguous binding repair.</summary>
    public double TopologyRepairTolerance { get; set; } = 1;
}

public enum StationLayoutAccessStatus
{
    Allowed,
    Unauthenticated,
    Forbidden,
    NotFound
}

public sealed record StationLayoutAccessDecision(
    StationLayoutAccessStatus Status,
    string? Message = null)
{
    public static StationLayoutAccessDecision Allow() => new(StationLayoutAccessStatus.Allowed);
    public static StationLayoutAccessDecision Unauthenticated(string? message = null) =>
        new(StationLayoutAccessStatus.Unauthenticated, message);
    public static StationLayoutAccessDecision Forbid(string? message = null) =>
        new(StationLayoutAccessStatus.Forbidden, message);
    public static StationLayoutAccessDecision Missing(string? message = null) =>
        new(StationLayoutAccessStatus.NotFound, message);
}

public sealed class NullStationLayoutArtifactSink : IStationLayoutArtifactSink
{
    public Task OnDwgExtractedAsync(
        StationLayoutDwgArtifact artifact,
        CancellationToken cancellationToken) => Task.CompletedTask;
}

public abstract class StationLayoutException : Exception
{
    protected StationLayoutException(string message) : base(message)
    {
    }

    protected StationLayoutException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class StationLayoutValidationException : StationLayoutException
{
    public StationLayoutValidationException(string message) : base(message)
    {
    }

    public StationLayoutValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class StationLayoutNotFoundException : StationLayoutException
{
    public StationLayoutNotFoundException(string message) : base(message)
    {
    }
}

public sealed class StationLayoutUnauthenticatedException : StationLayoutException
{
    public StationLayoutUnauthenticatedException(string message) : base(message)
    {
    }
}

public sealed class StationLayoutForbiddenException : StationLayoutException
{
    public StationLayoutForbiddenException(string message) : base(message)
    {
    }
}

public sealed class StationLayoutConflictException : StationLayoutException
{
    public StationLayoutConflictException(string message) : base(message)
    {
    }
}

public sealed class StationLayoutStoreException : StationLayoutException
{
    public StationLayoutStoreException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
