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
        var schemeId = string.IsNullOrWhiteSpace(requestedSchemeId)
            ? ResolveDefaultSchemeId(database, scopeId)
            : requestedSchemeId.Trim();
        if (string.IsNullOrWhiteSpace(schemeId) || !SchemeExists(database, scopeId, schemeId))
        {
            return Task.FromResult<StationLayoutRecord?>(null);
        }

        var scheme = (database.Query<SchemeRow>(
                $"SELECT ID, Name, DisplayStyles, GridSettings FROM {Q("stationscheme")} " +
                "WHERE InstanceID = @scopeId AND ID = @schemeId LIMIT 1",
                new { scopeId, schemeId }) ?? [])
            .FirstOrDefault();
        var nodes = Query<StationNodeRow>(database, "node", scopeId, schemeId);
        var links = Query<StationLinkRow>(database, "link", scopeId, schemeId);
        var curves = Query<StationCurveRow>(database, "curve", scopeId, schemeId);
        var signals = Query<StationSignalRow>(database, "signal", scopeId, schemeId);
        var joints = Query<StationInsulationJointRow>(database, "insulationjoint", scopeId, schemeId);
        var bufferStops = Query<StationBufferStopRow>(database, "bufferstop", scopeId, schemeId);
        var platforms = Query<StationPlatformRow>(database, "platform", scopeId, schemeId);
        var switches = Query<StationSwitchRow>(database, "switch", scopeId, schemeId);
        var branches = (database.Query<SwitchBranchVectorRow>(
                $"SELECT * FROM {Q("switchbranchvector")} " +
                "WHERE InstanceID = @scopeId AND StationSchemeID = @schemeId " +
                "ORDER BY SwitchID, Sequence",
                new { scopeId, schemeId }) ?? [])
            .ToList();
        var cells = Query<StationCellRow>(database, "cell", scopeId, schemeId);
        var annotations = Query<StationAnnotationRow>(database, "annotation", scopeId, schemeId);
        var nodeById = nodes.ToDictionary(node => node.ID);
        var branchLookup = branches
            .GroupBy(item => item.SwitchID ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        var document = new StationLayoutDocument
        {
            Metadata = new StationLayoutMetadata
            {
                InstanceID = scopeId,
                StationSchemeID = schemeId,
                Revision = ReadRevision(database, scopeId, schemeId),
                CoordinateTransform = new SwitchYard.StationLayout.StationLayoutCoordinateTransform
                {
                    Applied = false,
                    Scale = 1
                },
                DisplayStyles = ParseJson(scheme?.DisplayStyles),
                GridSettings = ParseJson(scheme?.GridSettings) ?? ParseJson(DefaultGridSettings),
                LatestElementID = CalculateLatestElementId(
                    nodes.Select(item => item.ID.ToString(CultureInfo.InvariantCulture))
                        .Concat(links.Select(item => item.ID.ToString(CultureInfo.InvariantCulture)))
                        .Concat(curves.Select(item => item.ID))
                        .Concat(signals.Select(item => item.ID))
                        .Concat(joints.Select(item => item.ID))
                        .Concat(bufferStops.Select(item => item.ID))
                        .Concat(platforms.Select(item => item.ID))
                        .Concat(switches.Select(item => item.ID))
                        .Concat(cells.Select(item => item.ID))
                        .Concat(annotations.Select(item => item.ID)))
            },
            Nodes = nodes.Select(node => new StationLayoutNode
            {
                ID = ToText(node.ID),
                X = node.X,
                Y = node.Y
            }).ToList(),
            Tracks = links.Select(link =>
            {
                if (!nodeById.TryGetValue(link.FromNodeID, out var from) ||
                    !nodeById.TryGetValue(link.ToNodeID, out var to))
                {
                    return null;
                }

                return new StationLayoutTrack
                {
                    ID = ToText(link.ID),
                    Name = link.Name ?? string.Empty,
                    ArrowDirection = link.ArrowDirection ?? string.Empty,
                    ArrowType = link.ArrowType ?? string.Empty,
                    X1 = from.X,
                    Y1 = from.Y,
                    X2 = to.X,
                    Y2 = to.Y,
                    FromNodeID = ToText(from.ID),
                    ToNodeID = ToText(to.ID)
                };
            }).Where(item => item is not null).Select(item => item!).ToList(),
            Curves = curves.Select(curve => new StationLayoutCurve
            {
                ID = curve.ID ?? string.Empty,
                NodeID = First(curve.BindingNodeID, curve.VertexNodeID) ?? string.Empty,
                TangentLinkID1 = First(curve.BindingLink1ID, curve.TangentLinkID1) ?? string.Empty,
                TangentLinkID2 = First(curve.BindingLink2ID, curve.TangentLinkID2) ?? string.Empty,
                Radius = ParseDouble(curve.Radius),
                Angle = curve.Angle,
                TangentDistance = curve.TangentDistance,
                Start = new StationLayoutPosition { X = curve.StartX, Y = curve.StartY },
                End = new StationLayoutPosition { X = curve.EndX, Y = curve.EndY },
                Center = new StationLayoutPosition { X = curve.CenterX, Y = curve.CenterY },
                LargeArcFlag = curve.LargeArcFlag == 1 ? 1 : 0,
                SweepFlag = curve.SweepFlag == 1 ? 1 : 0
            }).ToList(),
            Signals = signals.Select(signal => MapSignal(signal, nodeById))
                .Where(item => item is not null).Select(item => item!).ToList(),
            InsulationJoints = joints.Select(joint => MapJoint(joint, nodeById))
                .Where(item => item is not null).Select(item => item!).ToList(),
            BufferStops = bufferStops.Select(stop => MapBufferStop(stop, nodeById))
                .Where(item => item is not null).Select(item => item!).ToList(),
            Platforms = platforms.Select(platform => new StationLayoutPlatform
            {
                ID = platform.ID ?? string.Empty,
                Name = EquipmentName(platform.Name, platform.ID),
                X = platform.X,
                Y = platform.Y,
                Width = platform.Width,
                Height = platform.Height
            }).ToList(),
            Switches = switches.Select(item => MapSwitch(item, nodeById, branchLookup))
                .Where(item => item is not null).Select(item => item!).ToList(),
            Cells = cells.Select(cell => new StationLayoutCell
            {
                InstanceID = cell.InstanceID ?? scopeId,
                StationSchemeID = cell.StationSchemeID ?? schemeId,
                ID = cell.ID ?? string.Empty,
                LinkIDList = cell.LinkIDList ?? string.Empty,
                Name = EquipmentName(cell.Name, cell.ID)
            }).ToList(),
            Annotations = annotations.Select(annotation => new StationLayoutAnnotation
            {
                ID = annotation.ID ?? string.Empty,
                Text = annotation.Text ?? string.Empty,
                Position = new StationLayoutPosition { X = annotation.X, Y = annotation.Y },
                FontFamily = string.IsNullOrWhiteSpace(annotation.FontFamily) ? "Arial" : annotation.FontFamily,
                FontSize = annotation.FontSize <= 0 ? 16 : annotation.FontSize,
                FontWeight = string.IsNullOrWhiteSpace(annotation.FontWeight) ? "normal" : annotation.FontWeight,
                FontStyle = string.IsNullOrWhiteSpace(annotation.FontStyle) ? "normal" : annotation.FontStyle,
                Angle = annotation.Angle,
                TextColor = string.IsNullOrWhiteSpace(annotation.TextColor) ? "#ffffff" : annotation.TextColor
            }).ToList()
        };

        var revision = document.Metadata!.Revision ?? 0;
        return Task.FromResult<StationLayoutRecord?>(
            new StationLayoutRecord(
                new StationSchemeRecord(
                    scopeId,
                    schemeId,
                    string.IsNullOrWhiteSpace(scheme?.Name) ? schemeId : scheme.Name.Trim(),
                    revision,
                    string.Equals(schemeId, ResolveDefaultSchemeId(database, scopeId), StringComparison.OrdinalIgnoreCase)),
                document));
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
                $"SELECT DisplayStyles, GridSettings FROM {Q("stationscheme")} " +
                "WHERE InstanceID = @scopeId AND ID = @sourceSchemeId",
                new { scopeId, sourceSchemeId }) ?? []).FirstOrDefault();
            database.ExecuteNonQuery(
                $"INSERT INTO {Q("stationscheme")} (InstanceID, ID, Name, DisplayStyles, GridSettings) " +
                "VALUES (@scopeId, @targetSchemeId, @Name, @DisplayStyles, @GridSettings)",
                new
                {
                    scopeId, targetSchemeId, targetScheme.Name,
                    source?.DisplayStyles,
                    GridSettings = source?.GridSettings ?? DefaultGridSettings
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
        var nodes = BuildNodes(request.Document);
        var links = BuildLinks(request.Document, nodes);
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
            PersistSchemeMetadata(database, request);
            foreach (var table in LayoutTables)
            {
                database.ExecuteNonQuery(
                    $"DELETE FROM {Q(table)} WHERE InstanceID = @ScopeId AND StationSchemeID = @SchemeId",
                    new { request.ScopeId, request.SchemeId });
            }

            InsertNodes(database, request, nodes);
            InsertLinks(database, request, links);
            InsertPlatforms(database, request);
            InsertAnnotations(database, request);
            InsertCurves(database, request, nodes, links);
            InsertSignals(database, request, nodes);
            InsertJoints(database, request, nodes);
            InsertBufferStops(database, request, nodes);
            InsertCells(database, request, links);
            InsertSwitches(database, request, nodes, links);

            var nextRevision = checked(currentRevision + 1);
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
            return Task.FromResult(new StationLayoutWriteResult(request.SchemeId, nextRevision));
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

    private static void PersistSchemeMetadata(
        DBConnector database,
        StationLayoutWriteRequest request)
    {
        var displayStyles = RawJson(request.Document.Metadata?.DisplayStyles);
        var gridSettings = RawJson(request.Document.Metadata?.GridSettings);
        if (displayStyles is not null)
        {
            database.ExecuteNonQuery(
                $"UPDATE {Q("stationscheme")} SET DisplayStyles = @displayStyles " +
                "WHERE InstanceID = @ScopeId AND ID = @SchemeId",
                new { request.ScopeId, request.SchemeId, displayStyles });
        }

        if (gridSettings is not null)
        {
            database.ExecuteNonQuery(
                $"UPDATE {Q("stationscheme")} SET GridSettings = @gridSettings " +
                "WHERE InstanceID = @ScopeId AND ID = @SchemeId",
                new { request.ScopeId, request.SchemeId, gridSettings });
        }
    }

    private static string? RawJson(JsonElement? element) =>
        element.HasValue && element.Value.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined
            ? element.Value.GetRawText()
            : null;

    private static JsonElement? ParseJson(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(value);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static StationLayoutSignal? MapSignal(
        StationSignalRow signal,
        IReadOnlyDictionary<int, StationNodeRow> nodeById)
    {
        if (!TryNode(signal.BindingNodeID, nodeById, out var node))
        {
            return null;
        }

        return new StationLayoutSignal
        {
            ID = signal.ID ?? string.Empty,
            Name = EquipmentName(signal.Name, signal.ID),
            Type = signal.Type ?? string.Empty,
            Direction = string.IsNullOrWhiteSpace(signal.Direction) ? "e" : signal.Direction,
            BindingNodeID = ToText(node.ID),
            Position = new StationLayoutPosition { X = node.X, Y = node.Y }
        };
    }

    private static StationLayoutInsulationJoint? MapJoint(
        StationInsulationJointRow joint,
        IReadOnlyDictionary<int, StationNodeRow> nodeById)
    {
        if (!TryNode(joint.BindingNodeID, nodeById, out var node))
        {
            return null;
        }

        return new StationLayoutInsulationJoint
        {
            ID = joint.ID ?? string.Empty,
            Type = joint.Type ?? string.Empty,
            BindingNodeID = ToText(node.ID),
            Position = new StationLayoutPosition { X = node.X, Y = node.Y }
        };
    }

    private static StationLayoutBufferStop? MapBufferStop(
        StationBufferStopRow stop,
        IReadOnlyDictionary<int, StationNodeRow> nodeById)
    {
        if (!TryNode(stop.BindingNodeID, nodeById, out var node))
        {
            return null;
        }

        return new StationLayoutBufferStop
        {
            ID = stop.ID ?? string.Empty,
            Type = NormalizeBufferStopType(stop.Type),
            Direction = string.IsNullOrWhiteSpace(stop.Direction) ? "right" : stop.Direction,
            BindingNodeID = ToText(node.ID),
            Position = new StationLayoutPosition { X = node.X, Y = node.Y }
        };
    }

    private static StationLayoutSwitch? MapSwitch(
        StationSwitchRow item,
        IReadOnlyDictionary<int, StationNodeRow> nodeById,
        IReadOnlyDictionary<string, List<SwitchBranchVectorRow>> branches)
    {
        if (!TryNode(item.BindingNodeID, nodeById, out var node))
        {
            return null;
        }

        branches.TryGetValue(item.ID ?? string.Empty, out var vectors);
        return new StationLayoutSwitch
        {
            ID = item.ID ?? string.Empty,
            Name = EquipmentName(item.Name, item.ID),
            Type = string.IsNullOrWhiteSpace(item.Type) ? "unknown" : item.Type,
            BindingNodeID = ToText(node.ID),
            Position = new StationLayoutPosition { X = node.X, Y = node.Y },
            BranchVectorList = (vectors ?? [])
                .OrderBy(vector => vector.Sequence)
                .Select(vector => new StationLayoutSwitchBranch
                {
                    X = vector.X,
                    Y = vector.Y,
                    LineID = vector.BindingLinkID ?? string.Empty
                }).ToList()
        };
    }

    private static bool TryNode(
        string? id,
        IReadOnlyDictionary<int, StationNodeRow> nodeById,
        out StationNodeRow node)
    {
        if (int.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) &&
            nodeById.TryGetValue(parsed, out var found))
        {
            node = found;
            return true;
        }

        node = null!;
        return false;
    }

    private static StationLayoutNodeSaveContext BuildNodes(StationLayoutDocument document)
    {
        var context = new StationLayoutNodeSaveContext
        {
            Allocator = new IntegerIdAllocator(),
            Transform = StationLayoutPersistenceTransform.Identity
        };
        foreach (var item in document.Nodes ?? [])
        {
            var sourceId = string.IsNullOrWhiteSpace(item.ID)
                ? $"node_{context.Nodes.Count}"
                : item.ID.Trim();
            var id = context.Allocator.Allocate(sourceId);
            var entry = new StationLayoutNodeSaveEntry
            {
                SourceID = sourceId,
                ID = id,
                DisplayX = item.X,
                DisplayY = item.Y,
                DatabaseX = Math.Round(item.X, 3),
                DatabaseY = Math.Round(item.Y, 3)
            };
            context.Nodes.Add(entry);
            context.NodeIDBySourceID.TryAdd(sourceId, id);
            context.NodeIDByPointKey.TryAdd(PointKey(item.X, item.Y), id);
        }

        return context;
    }

    private static StationLayoutLinkSaveContext BuildLinks(
        StationLayoutDocument document,
        StationLayoutNodeSaveContext nodes)
    {
        var allocator = new IntegerIdAllocator();
        var context = new StationLayoutLinkSaveContext();
        foreach (var item in document.Tracks ?? [])
        {
            var sourceId = string.IsNullOrWhiteSpace(item.ID)
                ? $"track_{context.Links.Count}"
                : item.ID.Trim();
            var id = allocator.Allocate(sourceId);
            var entry = new StationLayoutLinkSaveEntry
            {
                SourceID = sourceId,
                ID = id,
                Name = item.Name ?? string.Empty,
                ArrowDirection = Code(item.ArrowDirection),
                ArrowType = Code(item.ArrowType),
                FromNodeID = ResolveTrackNode(nodes, item.FromNodeID, item.X1, item.Y1),
                ToNodeID = ResolveTrackNode(nodes, item.ToNodeID, item.X2, item.Y2)
            };
            context.Links.Add(entry);
            context.LinkIDBySourceID.TryAdd(sourceId, id);
        }

        return context;
    }

    private static int ResolveTrackNode(
        StationLayoutNodeSaveContext nodes,
        string? sourceId,
        double x,
        double y)
    {
        if (!string.IsNullOrWhiteSpace(sourceId) &&
            nodes.NodeIDBySourceID.TryGetValue(sourceId.Trim(), out var found))
        {
            return found;
        }

        var key = PointKey(x, y);
        if (nodes.NodeIDByPointKey.TryGetValue(key, out found))
        {
            return found;
        }

        var generatedSourceId = $"__generated_node_{nodes.Nodes.Count}";
        var id = nodes.Allocator.Allocate(generatedSourceId);
        nodes.Nodes.Add(new StationLayoutNodeSaveEntry
        {
            SourceID = generatedSourceId,
            ID = id,
            DisplayX = x,
            DisplayY = y,
            DatabaseX = Math.Round(x, 3),
            DatabaseY = Math.Round(y, 3)
        });
        nodes.NodeIDBySourceID[generatedSourceId] = id;
        nodes.NodeIDByPointKey[key] = id;
        return id;
    }

    private static void InsertNodes(
        DBConnector database,
        StationLayoutWriteRequest request,
        StationLayoutNodeSaveContext nodes)
    {
        foreach (var node in nodes.Nodes)
        {
            Insert(database,
                $"INSERT INTO {Q("node")} (InstanceID, StationSchemeID, ID, X, Y) " +
                "VALUES (@ScopeId, @SchemeId, @ID, @DatabaseX, @DatabaseY)",
                new { request.ScopeId, request.SchemeId, node.ID, node.DatabaseX, node.DatabaseY },
                "node");
        }
    }

    private static void InsertLinks(
        DBConnector database,
        StationLayoutWriteRequest request,
        StationLayoutLinkSaveContext links)
    {
        foreach (var link in links.Links)
        {
            Insert(database,
                $"INSERT INTO {Q("link")} " +
                "(InstanceID, StationSchemeID, ID, Name, FromNodeID, ToNodeID, ArrowDirection, ArrowType) " +
                "VALUES (@ScopeId, @SchemeId, @ID, @Name, @FromNodeID, @ToNodeID, @ArrowDirection, @ArrowType)",
                new
                {
                    request.ScopeId,
                    request.SchemeId,
                    link.ID,
                    link.Name,
                    link.FromNodeID,
                    link.ToNodeID,
                    link.ArrowDirection,
                    link.ArrowType
                },
                "link");
        }
    }

    private static void InsertPlatforms(
        DBConnector database,
        StationLayoutWriteRequest request)
    {
        var index = 0;
        foreach (var item in request.Document.Platforms ?? [])
        {
            var id = StringId(item.ID, "platform", index++);
            Insert(database,
                $"INSERT INTO {Q("platform")} " +
                "(InstanceID, StationSchemeID, ID, Name, X, Y, Width, Height) " +
                "VALUES (@ScopeId, @SchemeId, @id, @name, @X, @Y, @Width, @Height)",
                new
                {
                    request.ScopeId,
                    request.SchemeId,
                    id,
                    name = EquipmentName(item.Name, id),
                    item.X,
                    item.Y,
                    item.Width,
                    item.Height
                },
                "platform");
        }
    }

    private static void InsertAnnotations(
        DBConnector database,
        StationLayoutWriteRequest request)
    {
        var index = 0;
        foreach (var item in request.Document.Annotations ?? [])
        {
            var position = item.Position ?? new StationLayoutPosition();
            Insert(database,
                $"INSERT INTO {Q("annotation")} " +
                "(InstanceID, StationSchemeID, ID, Text, X, Y, FontFamily, FontSize, " +
                "FontWeight, FontStyle, Angle, TextColor) " +
                "VALUES (@ScopeId, @SchemeId, @id, @text, @X, @Y, @fontFamily, @fontSize, " +
                "@fontWeight, @fontStyle, @Angle, @textColor)",
                new
                {
                    request.ScopeId,
                    request.SchemeId,
                    id = StringId(item.ID, "annotation", index++),
                    text = item.Text ?? string.Empty,
                    position.X,
                    position.Y,
                    fontFamily = string.IsNullOrWhiteSpace(item.FontFamily) ? "Arial" : item.FontFamily,
                    fontSize = item.FontSize <= 0 ? 16 : item.FontSize,
                    fontWeight = string.IsNullOrWhiteSpace(item.FontWeight) ? "normal" : item.FontWeight,
                    fontStyle = string.IsNullOrWhiteSpace(item.FontStyle) ? "normal" : item.FontStyle,
                    item.Angle,
                    textColor = string.IsNullOrWhiteSpace(item.TextColor) ? "#ffffff" : item.TextColor
                },
                "annotation");
        }
    }

    private static void InsertCurves(
        DBConnector database,
        StationLayoutWriteRequest request,
        StationLayoutNodeSaveContext nodes,
        StationLayoutLinkSaveContext links)
    {
        var index = 0;
        foreach (var item in request.Document.Curves ?? [])
        {
            var start = item.Start ?? new StationLayoutPosition();
            var end = item.End ?? new StationLayoutPosition();
            var center = item.Center ?? new StationLayoutPosition();
            Insert(database,
                $"INSERT INTO {Q("curve")} " +
                "(InstanceID, StationSchemeID, ID, BindingNodeID, BindingLink1ID, BindingLink2ID, " +
                "Radius, Angle, TangentDistance, StartX, StartY, EndX, EndY, CenterX, CenterY, LargeArcFlag, SweepFlag) " +
                "VALUES (@ScopeId, @SchemeId, @id, @bindingNodeId, @bindingLink1Id, @bindingLink2Id, " +
                "@radius, @Angle, @TangentDistance, @StartX, @StartY, @EndX, @EndY, @CenterX, @CenterY, " +
                "@largeArcFlag, @sweepFlag)",
                new
                {
                    request.ScopeId,
                    request.SchemeId,
                    id = StringId(item.ID, "curve", index++),
                    bindingNodeId = ResolveNodeText(nodes, item.NodeID),
                    bindingLink1Id = ResolveLinkText(links, item.TangentLinkID1),
                    bindingLink2Id = ResolveLinkText(links, item.TangentLinkID2),
                    radius = Convert.ToInt32(Math.Round(
                        item.Radius <= 0 ? 100 : item.Radius,
                        MidpointRounding.AwayFromZero)),
                    item.Angle,
                    item.TangentDistance,
                    StartX = start.X,
                    StartY = start.Y,
                    EndX = end.X,
                    EndY = end.Y,
                    CenterX = center.X,
                    CenterY = center.Y,
                    largeArcFlag = item.LargeArcFlag == 1 ? 1 : 0,
                    sweepFlag = item.SweepFlag == 1 ? 1 : 0
                },
                "curve");
        }
    }

    private static void InsertSignals(
        DBConnector database,
        StationLayoutWriteRequest request,
        StationLayoutNodeSaveContext nodes)
    {
        var index = 0;
        foreach (var item in request.Document.Signals ?? [])
        {
            var binding = ResolveEquipmentNode(nodes, item.BindingNodeID, item.Position);
            if (!binding.HasValue)
            {
                continue;
            }

            var id = StringId(item.ID, "signal", index++);
            Insert(database,
                $"INSERT INTO {Q("signal")} " +
                "(InstanceID, StationSchemeID, ID, Name, Type, Direction, BindingNodeID) " +
                "VALUES (@ScopeId, @SchemeId, @id, @name, @type, @direction, @bindingNodeId)",
                new
                {
                    request.ScopeId,
                    request.SchemeId,
                    id,
                    name = EquipmentName(item.Name, id),
                    type = string.IsNullOrWhiteSpace(item.Type) ? "departure" : item.Type,
                    direction = string.IsNullOrWhiteSpace(item.Direction) ? "e" : item.Direction,
                    bindingNodeId = ToText(binding.Value)
                },
                "signal");
        }
    }

    private static void InsertJoints(
        DBConnector database,
        StationLayoutWriteRequest request,
        StationLayoutNodeSaveContext nodes)
    {
        var index = 0;
        foreach (var item in request.Document.InsulationJoints ?? [])
        {
            var binding = ResolveEquipmentNode(nodes, item.BindingNodeID, item.Position);
            if (!binding.HasValue)
            {
                continue;
            }

            Insert(database,
                $"INSERT INTO {Q("insulationjoint")} " +
                "(InstanceID, StationSchemeID, ID, Type, BindingNodeID) " +
                "VALUES (@ScopeId, @SchemeId, @id, @type, @bindingNodeId)",
                new
                {
                    request.ScopeId,
                    request.SchemeId,
                    id = StringId(item.ID, "insulationjoint", index++),
                    type = string.IsNullOrWhiteSpace(item.Type) ? "normal" : item.Type,
                    bindingNodeId = ToText(binding.Value)
                },
                "insulationjoint");
        }
    }

    private static void InsertBufferStops(
        DBConnector database,
        StationLayoutWriteRequest request,
        StationLayoutNodeSaveContext nodes)
    {
        var index = 0;
        foreach (var item in request.Document.BufferStops ?? [])
        {
            var binding = ResolveEquipmentNode(nodes, item.BindingNodeID, item.Position);
            if (!binding.HasValue)
            {
                continue;
            }

            Insert(database,
                $"INSERT INTO {Q("bufferstop")} " +
                $"(InstanceID, StationSchemeID, ID, {Q("Type")}, Direction, BindingNodeID) " +
                "VALUES (@ScopeId, @SchemeId, @id, @type, @direction, @bindingNodeId)",
                new
                {
                    request.ScopeId,
                    request.SchemeId,
                    id = StringId(item.ID, "bufferstop", index++),
                    type = NormalizeBufferStopType(item.Type),
                    direction = NormalizeBufferStopDirection(item.Direction),
                    bindingNodeId = ToText(binding.Value)
                },
                "bufferstop");
        }
    }

    private void InsertCells(
        DBConnector database,
        StationLayoutWriteRequest request,
        StationLayoutLinkSaveContext links)
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in request.Document.Cells ?? [])
        {
            var id = item.ID?.Trim();
            if (string.IsNullOrWhiteSpace(id) || !used.Add(id))
            {
                do
                {
                    id = _idGenerator.NextId();
                } while (!used.Add(id));
            }

            var linkIds = ParseIds(item.LinkIDList)
                .Select(linkId => ResolveLinkText(links, linkId))
                .Where(linkId => !string.IsNullOrWhiteSpace(linkId))
                .Distinct(StringComparer.OrdinalIgnoreCase);
            Insert(database,
                $"INSERT INTO {Q("cell")} " +
                "(InstanceID, StationSchemeID, ID, LinkIDList, Name) " +
                "VALUES (@ScopeId, @SchemeId, @id, @linkIdList, @name)",
                new
                {
                    request.ScopeId,
                    request.SchemeId,
                    id,
                    linkIdList = string.Join(',', linkIds),
                    name = EquipmentName(item.Name, id)
                },
                "cell");
        }
    }

    private static void InsertSwitches(
        DBConnector database,
        StationLayoutWriteRequest request,
        StationLayoutNodeSaveContext nodes,
        StationLayoutLinkSaveContext links)
    {
        var index = 0;
        foreach (var item in request.Document.Switches ?? [])
        {
            var binding = ResolveEquipmentNode(nodes, item.BindingNodeID, item.Position);
            if (!binding.HasValue)
            {
                continue;
            }

            var id = StringId(item.ID, "switch", index++);
            Insert(database,
                $"INSERT INTO {Q("switch")} " +
                "(InstanceID, StationSchemeID, ID, Name, Type, BindingNodeID) " +
                "VALUES (@ScopeId, @SchemeId, @id, @name, @type, @bindingNodeId)",
                new
                {
                    request.ScopeId,
                    request.SchemeId,
                    id,
                    name = EquipmentName(item.Name, id),
                    type = string.IsNullOrWhiteSpace(item.Type) ? "unknown" : item.Type,
                    bindingNodeId = ToText(binding.Value)
                },
                "switch");

            var sequence = 0;
            foreach (var vector in item.BranchVectorList ?? [])
            {
                Insert(database,
                    $"INSERT INTO {Q("switchbranchvector")} " +
                    "(InstanceID, StationSchemeID, SwitchID, Sequence, X, Y, BindingLinkID) " +
                    "VALUES (@ScopeId, @SchemeId, @id, @sequence, @X, @Y, @bindingLinkId)",
                    new
                    {
                        request.ScopeId,
                        request.SchemeId,
                        id,
                        sequence = sequence++,
                        vector.X,
                        vector.Y,
                        bindingLinkId = ResolveLinkText(links, vector.LineID)
                    },
                    "switchbranchvector");
            }
        }
    }

    private static int? ResolveEquipmentNode(
        StationLayoutNodeSaveContext nodes,
        string? sourceId,
        StationLayoutPosition? position)
    {
        if (!string.IsNullOrWhiteSpace(sourceId) &&
            nodes.NodeIDBySourceID.TryGetValue(sourceId.Trim(), out var found))
        {
            return found;
        }

        if (position is null)
        {
            return null;
        }

        if (nodes.NodeIDByPointKey.TryGetValue(PointKey(position.X, position.Y), out found))
        {
            return found;
        }

        var nearest = nodes.Nodes
            .Select(node => new
            {
                node.ID,
                Distance = Math.Sqrt(
                    Math.Pow(node.DisplayX - position.X, 2) +
                    Math.Pow(node.DisplayY - position.Y, 2))
            })
            .OrderBy(item => item.Distance)
            .FirstOrDefault();
        return nearest?.Distance <= 2 ? nearest.ID : null;
    }

    private static string ResolveNodeText(
        StationLayoutNodeSaveContext nodes,
        string? sourceId) =>
        !string.IsNullOrWhiteSpace(sourceId) &&
        nodes.NodeIDBySourceID.TryGetValue(sourceId.Trim(), out var found)
            ? ToText(found)
            : sourceId?.Trim() ?? string.Empty;

    private static string ResolveLinkText(
        StationLayoutLinkSaveContext links,
        string? sourceId) =>
        !string.IsNullOrWhiteSpace(sourceId) &&
        links.LinkIDBySourceID.TryGetValue(sourceId.Trim(), out var found)
            ? ToText(found)
            : sourceId?.Trim() ?? string.Empty;

    private static void Insert(
        DBConnector database,
        string sql,
        object parameters,
        string table)
    {
        if (database.ExecuteNonQuery(sql, parameters) <= 0)
        {
            throw new InvalidOperationException($"Failed to insert {table} row.");
        }
    }

    private static string PointKey(double x, double y) =>
        $"{Math.Round(x, 3).ToString(CultureInfo.InvariantCulture)}|" +
        Math.Round(y, 3).ToString(CultureInfo.InvariantCulture);

    private static string? Code(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

    private static string StringId(string? value, string prefix, int index) =>
        string.IsNullOrWhiteSpace(value) ? $"{prefix}_{index}" : value.Trim();

    private static string EquipmentName(string? name, string? id) =>
        string.IsNullOrWhiteSpace(name) ? id?.Trim() ?? string.Empty : name.Trim();

    private static string NormalizeBufferStopType(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "ext" or "e" or "extend" or "extended" or "extension" or "延伸" or "延申" => "ext",
            _ => "normal"
        };

    private static string NormalizeBufferStopDirection(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "left" or "l" or "左" or "向左" => "left",
            "right" or "r" or "右" or "向右" => "right",
            _ => "right"
        };

    private static IEnumerable<string> ParseIds(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : Regex.Split(value.Trim(), @"[\s,，;；]+")
                .Select(item => item.Trim())
                .Where(item => item.Length > 0);

    private static string? First(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private static string ToText(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static double ParseDouble(object? value)
    {
        if (value is null || value is DBNull)
        {
            return 0;
        }

        if (value is IConvertible convertible)
        {
            try
            {
                return convertible.ToDouble(CultureInfo.InvariantCulture);
            }
            catch (Exception) when (value is string)
            {
            }
        }

        return double.TryParse(
            Convert.ToString(value, CultureInfo.InvariantCulture),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : 0;
    }

    private static int CalculateLatestElementId(IEnumerable<string?> ids)
    {
        var maximum = -1;
        foreach (var value in ids)
        {
            if (int.TryParse(
                    value,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var parsed))
            {
                maximum = Math.Max(maximum, parsed);
            }
        }

        return maximum + 1;
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
