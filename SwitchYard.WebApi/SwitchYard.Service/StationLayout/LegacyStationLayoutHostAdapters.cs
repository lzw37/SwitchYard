using System.Security.Claims;
using System.Text;
using System.Text.Json;
using SwitchYard.Capacity;
using SwitchYard.Service.Utils;
using SwitchYard.StationLayout;

namespace SwitchYard.Service.StationLayout;

/// <summary>
/// Preserves the authorization behavior of the original StationLayoutController.
/// Database access intentionally lives in the SwitchYard host, never in the shared module.
/// </summary>
public sealed class LegacyStationLayoutAuthorization : IStationLayoutAuthorization
{
    public ValueTask<StationLayoutAccessDecision> AuthorizeScopeAsync(
        ClaimsPrincipal user,
        string scopeId,
        StationLayoutPermission permission,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (user.Identity?.IsAuthenticated != true)
        {
            return ValueTask.FromResult(
                StationLayoutAccessDecision.Unauthenticated("Invalid user context."));
        }

        var isAdmin = IsAdmin(user);
        if (permission == StationLayoutPermission.SaveLayout && !isAdmin)
        {
            // The original action used [Authorize(Roles = "Admin")], so this
            // denial occurred before instance lookup.
            return ValueTask.FromResult(
                StationLayoutAccessDecision.Forbid("Administrator permission is required."));
        }

        var database = DBConnector.GetDBConnector(DBConnector.CapacityDatabaseSectionName);
        var instance = (database.Query<CapacityInstance>(
                "SELECT ID, Owner FROM capacityinstance WHERE ID = @scopeId LIMIT 1",
                new { scopeId }) ?? [])
            .FirstOrDefault();
        if (instance is null)
        {
            return ValueTask.FromResult(
                StationLayoutAccessDecision.Missing("Instance not found."));
        }

        if (isAdmin || string.Equals(instance.Owner, user.Identity.Name, StringComparison.Ordinal))
        {
            return ValueTask.FromResult(StationLayoutAccessDecision.Allow());
        }

        // The legacy controller returned Unauthorized (401), rather than Forbid
        // (403), for an authenticated non-owner. Preserve that wire contract until
        // the old endpoints are retired.
        return ValueTask.FromResult(
            StationLayoutAccessDecision.Unauthenticated("Instance not owned by user."));
    }

    public ValueTask<StationLayoutAccessDecision> AuthorizeGlobalAsync(
        ClaimsPrincipal user,
        StationLayoutPermission permission,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (user.Identity?.IsAuthenticated != true)
        {
            return ValueTask.FromResult(
                StationLayoutAccessDecision.Unauthenticated("Invalid user context."));
        }

        return ValueTask.FromResult(IsAdmin(user)
            ? StationLayoutAccessDecision.Allow()
            : StationLayoutAccessDecision.Forbid("Administrator permission is required."));
    }

    private static bool IsAdmin(ClaimsPrincipal user) =>
        user.IsInRole("Admin") ||
        user.FindAll(ClaimTypes.Role)
            .Any(claim => string.Equals(claim.Value, "Admin", StringComparison.OrdinalIgnoreCase)) ||
        string.Equals(user.Identity?.Name, "Admin", StringComparison.OrdinalIgnoreCase);
}

public sealed class LegacyStationLayoutIdGenerator : IStationLayoutIdGenerator
{
    private readonly SnowflakeIdGenerator _generator;

    public LegacyStationLayoutIdGenerator(SnowflakeIdGenerator generator)
    {
        _generator = generator;
    }

    public string NextId() => _generator.NextIdString();
}

/// <summary>
/// Keeps the original DWG-import side effect: write StationLayout.json beneath LocalData.
/// The shared module only emits the artifact and remains unaware of the file system.
/// </summary>
public sealed class LegacyStationLayoutArtifactSink : IStationLayoutArtifactSink
{
    private readonly IWebHostEnvironment _environment;

    public LegacyStationLayoutArtifactSink(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public async Task OnDwgExtractedAsync(
        StationLayoutDwgArtifact artifact,
        CancellationToken cancellationToken)
    {
        var contentRoot = Path.GetFullPath(_environment.ContentRootPath);
        var outputDirectory = Path.GetFullPath(
            Path.Combine(contentRoot, "LocalData", "Capacity"));
        if (!outputDirectory.StartsWith(contentRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The StationLayout.json path escapes the content root.");
        }

        Directory.CreateDirectory(outputDirectory);
        var outputPath = Path.Combine(outputDirectory, "StationLayout.json");
        var json = JsonSerializer.Serialize(
            artifact.Layout,
            new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(
            outputPath,
            json,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            cancellationToken);
    }
}
