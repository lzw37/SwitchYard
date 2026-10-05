using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using SwitchYard.Capacity;
using SwitchYard.Service.Services;
using SwitchYard.StationLayout;

namespace SwitchYard.Service.StationLayout;

/// <summary>
/// SwitchYard-hosted persistence adapter for the legacy capacity database.
/// SQL and transaction handling intentionally remain outside SwitchYard.StationLayout.
/// No schema is created from request paths; DatabaseSchemaInitializer owns that task.
/// </summary>
public sealed class LegacyStationLayoutRepository : IStationLayoutRepository
{
    private const string DefaultSchemeId = "station_layout_scheme";
    private const string DefaultSchemeName = "车站布置图";
    private const string DefaultGridSettings =
        "{\"showGrid\":true,\"spacing\":20,\"originX\":0,\"originY\":0}";

    private static readonly string[] LayoutTables =
    [
        "switchbranchvector",
        "switch",
        "bufferstop",
        "insulationjoint",
        "signal",
        "platform",
        "cell",
        "annotation",
        "curve",
        "link",
        "node"
    ];

    private static readonly string[] SchemeDataTables =
    [
        "node",
        "link",
        "curve",
        "signal",
        "insulationjoint",
        "bufferstop",
        "platform",
        "switch",
        "switchbranchvector",
        "stationroute",
        "stationrouteend",
        "stationroutetime",
        "cell",
        "annotation"
    ];

    private readonly IStationLayoutIdGenerator _idGenerator;

    public LegacyStationLayoutRepository(IStationLayoutIdGenerator idGenerator)
    {
        _idGenerator = idGenerator;
    }

    public Task<IReadOnlyList<StationSchemeRecord>> ListSchemesAsync(
        string scopeId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var database = OpenDatabase();
        var schemes = new Dictionary<string, MutableScheme>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in database.Query<SchemeRow>(
                     $"SELECT ID, Name FROM {Q("stationscheme")} WHERE InstanceID = @scopeId",
                     new { scopeId }) ?? [])
        {
            AddScheme(schemes, row.ID, row.Name);
        }

        foreach (var table in SchemeDataTables.Where(table => table != "stationroutetime"))
        {
            foreach (var row in database.Query<SchemeRow>(
                         $"SELECT StationSchemeID AS ID FROM {Q(table)} " +
                         "WHERE InstanceID = @scopeId AND StationSchemeID IS NOT NULL " +
                         "AND TRIM(StationSchemeID) <> '' GROUP BY StationSchemeID",
                         new { scopeId }) ?? [])
            {
                AddScheme(schemes, row.ID, row.ID);
            }
        }

        var revisions = (database.Query<RevisionRow>(
                $"SELECT StationSchemeID, Revision FROM {Q("stationlayoutrevision")} " +
                "WHERE InstanceID = @scopeId",
                new { scopeId }) ?? [])
            .Where(row => !string.IsNullOrWhiteSpace(row.StationSchemeID))
            .ToDictionary(
                row => row.StationSchemeID!,
                row => Math.Max(0, row.Revision),
                StringComparer.OrdinalIgnoreCase);
        var defaultSchemeId = ResolveDefaultSchemeId(database, scopeId);

        return Task.FromResult<IReadOnlyList<StationSchemeRecord>>(
            schemes.Values
                .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
                .Select(item => new StationSchemeRecord(
                    scopeId,
                    item.Id,
                    item.Name,
                    revisions.GetValueOrDefault(item.Id),
                    string.Equals(item.Id, defaultSchemeId, StringComparison.OrdinalIgnoreCase)))
                .ToList());
    }

    public Task<StationLayoutRecord?> LoadAsync(
        string scopeId,
        string? requestedSchemeId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var database = OpenDatabase();
        // One read transaction keeps nodes, links, equipment and revision consistent.
        database.BeginTransaction();
        try
        {
            var schemeId = string.IsNullOrWhiteSpace(requestedSchemeId)
                ? ResolveDefaultSchemeId(database, scopeId)
                : requestedSchemeId.Trim();
            if (string.IsNullOrWhiteSpace(schemeId) || !SchemeExists(database, scopeId, schemeId))
            {
                return Task.FromResult<StationLayoutRecord?>(null);
            }

            var scheme = (database.Query<SchemeRow>(
                $"SELECT ID, Name FROM {Q("stationscheme")} WHERE InstanceID=@scopeId AND ID=@schemeId",
                new { scopeId, schemeId }) ?? []).FirstOrDefault();
            var revision = ReadRevision(database, scopeId, schemeId);
            var document = StationLayoutRelationalStore.Read(database, scopeId, schemeId, revision);
            return Task.FromResult<StationLayoutRecord?>(new StationLayoutRecord(
                new StationSchemeRecord(scopeId, schemeId, scheme?.Name ?? schemeId, revision,
                    string.Equals(schemeId, ResolveDefaultSchemeId(database, scopeId), StringComparison.Ordinal)), document));
        }
        finally { database.Rollback(); }
    }

    public Task<bool> SchemeExistsAsync(
        string scopeId,
        string schemeId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(SchemeExists(OpenDatabase(), scopeId, schemeId));
    }

    public Task<bool> TryCreateSchemeAsync(
        StationSchemeRecord scheme,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var database = OpenDatabase();
        database.BeginTransaction();
        try
        {
            if (SchemeExists(database, scheme.ScopeId, scheme.SchemeId))
            {
                database.Rollback();
                return Task.FromResult(false);
            }

            database.ExecuteNonQuery(
                $"INSERT INTO {Q("stationscheme")} " +
                "(InstanceID, ID, Name, GridSettings) VALUES (@ScopeId, @SchemeId, @Name, @GridSettings)",
                new
                {
                    scheme.ScopeId,
                    scheme.SchemeId,
                    scheme.Name,
                    GridSettings = DefaultGridSettings
                });
            database.Commit();
            return Task.FromResult(true);
        }
        catch
        {
            database.Rollback();
            throw;
        }
    }

    public Task<bool> TryCopySchemeAsync(
        string sourceSchemeId,
        StationSchemeRecord targetScheme,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var database = OpenDatabase();
        database.BeginTransaction();
        try
        {
            var scopeId = targetScheme.ScopeId;
            var targetSchemeId = targetScheme.SchemeId;
            if (!SchemeExists(database, scopeId, sourceSchemeId))
            {
                throw new StationLayoutNotFoundException($"Station scheme '{sourceSchemeId}' was not found.");
            }
            if (SchemeExists(database, scopeId, targetSchemeId))
            {
                database.Rollback();
                return Task.FromResult(false);
            }

            EnsureRevisionRowLocked(database, new StationLayoutWriteRequest(
                scopeId, sourceSchemeId, new StationLayoutDocument(), null, null));

            var source = (database.Query<SchemeRow>(
                $"SELECT DisplayStyles, GridSettings, LayoutMetadata, LayoutExtensions FROM {Q("stationscheme")} " +
                "WHERE InstanceID = @scopeId AND ID = @sourceSchemeId",
                new { scopeId, sourceSchemeId }) ?? []).FirstOrDefault();
            database.ExecuteNonQuery(
                $"INSERT INTO {Q("stationscheme")} (InstanceID, ID, Name, DisplayStyles, GridSettings, LayoutMetadata, LayoutExtensions) " +
                "VALUES (@scopeId, @targetSchemeId, @Name, @DisplayStyles, @GridSettings, @LayoutMetadata, @LayoutExtensions)",
                new
                {
                    scopeId, targetSchemeId, targetScheme.Name,
                    source?.DisplayStyles,
                    GridSettings = source?.GridSettings ?? DefaultGridSettings,
                    source?.LayoutMetadata, source?.LayoutExtensions
                });

            // Child identifiers are scoped by InstanceID and StationSchemeID, so retaining
            // them preserves all route, track, template and plan references in the copy.
            foreach (var table in CapacityDataLifecycle.SchemeTables.Reverse()
                         .Where(table => table != "stationlayoutrevision"))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!CapacityDataLifecycle.TableExists(database, table)) continue;
                var columns = (database.Query<ColumnRow>(
                    DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName)
                        ? "SELECT COLUMN_NAME AS Name FROM information_schema.columns " +
                          "WHERE table_schema = DATABASE() AND table_name = @table ORDER BY ORDINAL_POSITION"
                        : $"PRAGMA table_info({Q(table)})",
                    new { table }) ?? []).Select(column => column.Name).ToArray();
                var insertColumns = string.Join(", ", columns.Select(Q));
                var selectColumns = string.Join(", ", columns.Select(column =>
                    string.Equals(column, "StationSchemeID", StringComparison.OrdinalIgnoreCase)
                        ? "@targetSchemeId" : Q(column)));
                database.ExecuteNonQuery(
                    $"INSERT INTO {Q(table)} ({insertColumns}) SELECT {selectColumns} FROM {Q(table)} " +
                    "WHERE InstanceID = @scopeId AND StationSchemeID = @sourceSchemeId",
                    new { scopeId, sourceSchemeId, targetSchemeId });

                if (table is "operationprocesstemplate" or "trainprocesssnapshot")
                {
                    foreach (var row in database.Query<CopyDocumentRow>(
                                 $"SELECT DISTINCT Document FROM {Q(table)} " +
                                 "WHERE InstanceID = @scopeId AND StationSchemeID = @targetSchemeId",
                                 new { scopeId, targetSchemeId }) ?? [])
                    {
                        var document = JsonNode.Parse(row.Document)
                            ?? throw new InvalidDataException("Stored process document is empty.");
                        RewriteSchemeScope(document, targetSchemeId);
                        database.ExecuteNonQuery(
                            $"UPDATE {Q(table)} SET Document = @document " +
                            "WHERE InstanceID = @scopeId AND StationSchemeID = @targetSchemeId AND Document = @original",
                            new { scopeId, targetSchemeId, document = document.ToJsonString(), original = row.Document });
                    }
                }
            }
            cancellationToken.ThrowIfCancellationRequested();
            database.Commit();
            return Task.FromResult(true);
        }
        catch
        {
            database.Rollback();
            throw;
        }
    }

    private static void RewriteSchemeScope(JsonNode node, string schemeId)
    {
        if (node is JsonObject obj)
        {
            foreach (var key in obj.Select(property => property.Key).ToArray())
            {
                if (string.Equals(key, "StationSchemeID", StringComparison.OrdinalIgnoreCase))
                    obj[key] = schemeId;
                else if (obj[key] is { } child)
                    RewriteSchemeScope(child, schemeId);
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var child in array)
                if (child is not null) RewriteSchemeScope(child, schemeId);
        }
    }

    private sealed class ColumnRow { public string Name { get; set; } = string.Empty; }
    private sealed class CopyDocumentRow { public string Document { get; set; } = string.Empty; }

    public Task<bool> RenameSchemeAsync(
        string scopeId,
        string schemeId,
        string name,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var database = OpenDatabase();
        database.BeginTransaction();
        try
        {
            if (!SchemeExists(database, scopeId, schemeId))
            {
                database.Rollback();
                return Task.FromResult(false);
            }

            var updated = database.ExecuteNonQuery(
                $"UPDATE {Q("stationscheme")} SET Name = @name " +
                "WHERE InstanceID = @scopeId AND ID = @schemeId",
                new { scopeId, schemeId, name });
            if (updated == 0)
            {
                database.ExecuteNonQuery(
                    $"INSERT INTO {Q("stationscheme")} " +
                    "(InstanceID, ID, Name, GridSettings) VALUES (@scopeId, @schemeId, @name, @gridSettings)",
                    new { scopeId, schemeId, name, gridSettings = DefaultGridSettings });
            }

            database.Commit();
            return Task.FromResult(true);
        }
        catch
        {
            database.Rollback();
            throw;
        }
    }

    public Task<bool> DeleteSchemeAsync(
        string scopeId,
        string schemeId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var database = OpenDatabase();
        database.BeginTransaction();
        try
        {
            if (!SchemeExists(database, scopeId, schemeId))
            {
                database.Rollback();
                return Task.FromResult(false);
            }

            CapacityDataLifecycle.DeleteSchemeChildren(database, scopeId, schemeId);

            var deleted = database.ExecuteNonQuery(
                $"DELETE FROM {Q("stationscheme")} WHERE InstanceID = @scopeId AND ID = @schemeId",
                new { scopeId, schemeId });
            if (deleted != 1) throw new InvalidOperationException("Station scheme deletion was not saved.");
            CapacityDataLifecycle.VerifySchemeDeleted(database, scopeId, schemeId);
            database.Commit();
            return Task.FromResult(true);
        }
        catch
        {
            database.Rollback();
            throw;
        }
    }

    public Task<StationLayoutWriteResult> ReplaceLayoutAsync(
        StationLayoutWriteRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var database = OpenDatabase();
        database.BeginTransaction();
        try
        {
            EnsureRevisionRowLocked(database, request);
            var currentRevision = ReadRevision(database, request.ScopeId, request.SchemeId);
            if (request.ExpectedRevision.HasValue && request.ExpectedRevision.Value != currentRevision)
            {
                throw new StationLayoutConflictException(
                    $"Station layout revision mismatch. Expected {request.ExpectedRevision.Value}, current {currentRevision}.");
            }

            EnsureScheme(database, request.ScopeId, request.SchemeId);
            // Allocate new identities while holding the same revision lock as the write.
            var identities = StationLayoutIdentity.Assign(request.Document,
                StationLayoutRelationalStore.ExistingIds(database, request.ScopeId, request.SchemeId), _idGenerator);
            var document = identities.Document;
            var nextRevision = checked(currentRevision + 1);
            document.SetArchiveScope(request.ScopeId, request.SchemeId, nextRevision);
            StationLayoutRelationalStore.Write(database, request.ScopeId, request.SchemeId, document);
            database.ExecuteNonQuery(
                $"UPDATE {Q("stationlayoutrevision")} " +
                "SET Revision = @nextRevision, UpdatedBy = @ActorName, UpdatedAtUtc = @updatedAtUtc " +
                "WHERE InstanceID = @ScopeId AND StationSchemeID = @SchemeId",
                new
                {
                    request.ScopeId,
                    request.SchemeId,
                    nextRevision,
                    request.ActorName,
                    updatedAtUtc = DateTime.UtcNow
                });
            database.Commit();
            return Task.FromResult(new StationLayoutWriteResult(request.SchemeId, nextRevision)
            { Document = document, IdMappings = identities.IdMappings });
        }
        catch
        {
            database.Rollback();
            throw;
        }
    }

    private static void EnsureRevisionRowLocked(
        DBConnector database,
        StationLayoutWriteRequest request)
    {
        var now = DateTime.UtcNow;
        if (DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName))
        {
            database.ExecuteNonQuery(
                $"INSERT INTO {Q("stationlayoutrevision")} " +
                "(InstanceID, StationSchemeID, Revision, UpdatedBy, UpdatedAtUtc) " +
                "VALUES (@ScopeId, @SchemeId, 0, @ActorName, @now) " +
                "ON DUPLICATE KEY UPDATE Revision = Revision",
                new { request.ScopeId, request.SchemeId, request.ActorName, now });
            return;
        }

        // INSERT OR IGNORE promotes SQLite's deferred transaction to a writer
        // before the revision is read, so competing writers serialize here.
        database.ExecuteNonQuery(
            $"INSERT OR IGNORE INTO {Q("stationlayoutrevision")} " +
            "(InstanceID, StationSchemeID, Revision, UpdatedBy, UpdatedAtUtc) " +
            "VALUES (@ScopeId, @SchemeId, 0, @ActorName, @now)",
            new { request.ScopeId, request.SchemeId, request.ActorName, now });
    }

    private static DBConnector OpenDatabase() =>
        DBConnector.GetDBConnector(DBConnector.CapacityDatabaseSectionName);

    private static string Q(string identifier)
    {
        if (DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName))
        {
            return $"`{identifier.Replace("`", "``", StringComparison.Ordinal)}`";
        }

        return $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private static List<T> Query<T>(
        DBConnector database,
        string table,
        string scopeId,
        string schemeId) =>
        database.Query<T>(
            $"SELECT * FROM {Q(table)} " +
            "WHERE InstanceID = @scopeId AND StationSchemeID = @schemeId ORDER BY ID",
            new { scopeId, schemeId }) ?? [];

    private static void AddScheme(
        IDictionary<string, MutableScheme> schemes,
        string? id,
        string? name)
    {
        id = id?.Trim();
        if (string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        var normalizedName = string.IsNullOrWhiteSpace(name) ? id : name.Trim();
        if (schemes.TryGetValue(id, out var existing))
        {
            if (string.Equals(existing.Name, existing.Id, StringComparison.OrdinalIgnoreCase))
            {
                existing.Name = normalizedName;
            }

            return;
        }

        schemes[id] = new MutableScheme(id, normalizedName);
    }

    private static string? ResolveDefaultSchemeId(DBConnector database, string scopeId)
    {
        var fromNodes = (database.Query<SchemeRow>(
                $"SELECT StationSchemeID AS ID FROM {Q("node")} " +
                "WHERE InstanceID = @scopeId AND StationSchemeID IS NOT NULL " +
                "GROUP BY StationSchemeID ORDER BY COUNT(1) DESC, StationSchemeID LIMIT 1",
                new { scopeId }) ?? [])
            .FirstOrDefault()?.ID;
        if (!string.IsNullOrWhiteSpace(fromNodes))
        {
            return fromNodes.Trim();
        }

        var fromLinks = (database.Query<SchemeRow>(
                $"SELECT StationSchemeID AS ID FROM {Q("link")} " +
                "WHERE InstanceID = @scopeId AND StationSchemeID IS NOT NULL " +
                "GROUP BY StationSchemeID ORDER BY COUNT(1) DESC, StationSchemeID LIMIT 1",
                new { scopeId }) ?? [])
            .FirstOrDefault()?.ID;
        if (!string.IsNullOrWhiteSpace(fromLinks))
        {
            return fromLinks.Trim();
        }

        return (database.Query<SchemeRow>(
                $"SELECT ID FROM {Q("stationscheme")} " +
                "WHERE InstanceID = @scopeId ORDER BY ID LIMIT 1",
                new { scopeId }) ?? [])
            .FirstOrDefault()?.ID?.Trim();
    }

    private static bool SchemeExists(
        DBConnector database,
        string scopeId,
        string schemeId)
    {
        if ((database.Query<ExistsRow>(
                $"SELECT 1 AS Value FROM {Q("stationscheme")} " +
                "WHERE InstanceID = @scopeId AND ID = @schemeId LIMIT 1",
                new { scopeId, schemeId }) ?? []).Count > 0)
        {
            return true;
        }

        foreach (var table in SchemeDataTables)
        {
            if ((database.Query<ExistsRow>(
                    $"SELECT 1 AS Value FROM {Q(table)} " +
                    "WHERE InstanceID = @scopeId AND StationSchemeID = @schemeId LIMIT 1",
                    new { scopeId, schemeId }) ?? []).Count > 0)
            {
                return true;
            }
        }

        return false;
    }

    private static long ReadRevision(
        DBConnector database,
        string scopeId,
        string schemeId) =>
        Math.Max(0, (database.Query<RevisionRow>(
                $"SELECT Revision FROM {Q("stationlayoutrevision")} " +
                "WHERE InstanceID = @scopeId AND StationSchemeID = @schemeId LIMIT 1",
                new { scopeId, schemeId }) ?? [])
            .FirstOrDefault()?.Revision ?? 0);

    private static void EnsureScheme(
        DBConnector database,
        string scopeId,
        string schemeId)
    {
        var exists = (database.Query<ExistsRow>(
                $"SELECT 1 AS Value FROM {Q("stationscheme")} " +
                "WHERE InstanceID = @scopeId AND ID = @schemeId LIMIT 1",
                new { scopeId, schemeId }) ?? []).Count > 0;
        if (exists)
        {
            return;
        }

        database.ExecuteNonQuery(
            $"INSERT INTO {Q("stationscheme")} " +
            "(InstanceID, ID, Name, GridSettings) VALUES (@scopeId, @schemeId, @name, @gridSettings)",
            new
            {
                scopeId,
                schemeId,
                name = string.Equals(schemeId, DefaultSchemeId, StringComparison.Ordinal)
                    ? DefaultSchemeName
                    : schemeId,
                gridSettings = DefaultGridSettings
            });
    }

    private sealed class MutableScheme
    {
        public MutableScheme(string id, string name)
        {
            Id = id;
            Name = name;
        }

        public string Id { get; }

        public string Name { get; set; }
    }

    private sealed class SchemeRow
    {
        public string? ID { get; set; }

        public string? Name { get; set; }

        public string? DisplayStyles { get; set; }

        public string? GridSettings { get; set; }

        public string? LayoutMetadata { get; set; }
        public string? LayoutExtensions { get; set; }
    }

    private sealed class RevisionRow
    {
        public string? StationSchemeID { get; set; }

        public long Revision { get; set; }
    }

    private sealed class ExistsRow
    {
        public int Value { get; set; }
    }
}
