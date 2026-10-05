using System.Text.Json;

namespace SwitchYard.StationLayout;

internal static class StationLayoutJsonValidation
{
    private static readonly string[] Collections =
    [
        "tracks", "curves", "nodes", "signals", "insulationJoints",
        "bufferStops", "platforms", "switches", "cells", "annotations"
    ];

    public static void Validate(string json)
    {
        using var parsed = JsonDocument.Parse(json);
        var root = parsed.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            throw new StationLayoutValidationException("Station-layout JSON must be an object.");

        var hasFormat = root.TryGetProperty("format", out var format);
        var hasVersion = root.TryGetProperty("formatVersion", out var version);
        var archive = hasFormat || hasVersion;
        if (archive && (!hasFormat || format.ValueKind != JsonValueKind.String ||
                        format.GetString() != StationLayoutDocument.ArchiveFormat ||
                        !hasVersion || version.ValueKind != JsonValueKind.Number ||
                        !version.TryGetInt32(out var number) || number != 1))
            throw new StationLayoutValidationException("Unsupported station-layout JSON format or version.");

        foreach (var name in Collections)
        {
            if (!root.TryGetProperty(name, out var collection))
            {
                if (archive)
                    throw new StationLayoutValidationException($"Station-layout JSON is missing '{name}'.");
                continue;
            }
            if (collection.ValueKind != JsonValueKind.Array)
                throw new StationLayoutValidationException($"Station-layout '{name}' must be an array.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in collection.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                    throw new StationLayoutValidationException($"Station-layout '{name}' contains an invalid element.");
                ValidateGeometry(item, name, archive);
                if (name == "nodes" && item.TryGetProperty("adjacentLineIDList", out var adjacency) && adjacency.ValueKind != JsonValueKind.Array)
                    throw new StationLayoutValidationException("Node adjacentLineIDList must be an array.");
                if (name == "switches" && item.TryGetProperty("branchVectorList", out var branches))
                {
                    if (branches.ValueKind != JsonValueKind.Array) throw new StationLayoutValidationException("Switch branchVectorList must be an array.");
                    foreach (var branch in branches.EnumerateArray())
                    {
                        if (branch.ValueKind != JsonValueKind.Object) throw new StationLayoutValidationException("Invalid switch branch.");
                        Number(branch, "x", archive);
                        Number(branch, "y", archive);
                    }
                }
                if (archive && name == "cells" && item.TryGetProperty("id", out var pendingId) &&
                    pendingId.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(pendingId.GetString()))
                    continue; // Legacy hosts allocate IDs for newly created cells on save.
                if (archive && (!item.TryGetProperty("id", out var id) ||
                                id.ValueKind != JsonValueKind.String ||
                                string.IsNullOrWhiteSpace(id.GetString()) || !ids.Add(id.GetString()!)))
                    throw new StationLayoutValidationException($"Station-layout '{name}' requires unique, non-empty string IDs.");
            }
        }
        ValidateNumbers(root);
    }

    private static void ValidateGeometry(JsonElement item, string collection, bool archive)
    {
        if (collection == "tracks")
            foreach (var key in new[] { "x1", "y1", "x2", "y2" }) Number(item, key, archive);
        else if (collection is "nodes" or "platforms")
        {
            Number(item, "x", archive);
            Number(item, "y", archive);
            if (collection == "platforms") { Number(item, "width", archive); Number(item, "height", archive); }
        }
        else if (collection == "curves")
        {
            Number(item, "radius", archive);
            foreach (var key in new[] { "start", "end", "center" }) Position(item, key, archive);
        }
        else if (collection != "cells") Position(item, "position", archive);
    }

    private static void Number(JsonElement item, string key, bool required)
    {
        if (!item.TryGetProperty(key, out var value) && !required) return;
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out var number) || !double.IsFinite(number))
            throw new StationLayoutValidationException($"Station-layout '{key}' must be a finite number.");
    }

    private static void Position(JsonElement item, string key, bool required)
    {
        if (!item.TryGetProperty(key, out var position) && !required) return;
        if (position.ValueKind != JsonValueKind.Object)
            throw new StationLayoutValidationException($"Station-layout '{key}' must be a position object.");
        Number(position, "x", required);
        Number(position, "y", required);
    }

    private static void ValidateNumbers(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number &&
            (!value.TryGetDouble(out var number) || !double.IsFinite(number)))
            throw new StationLayoutValidationException("Station-layout JSON contains a non-finite number.");
        if (value.ValueKind == JsonValueKind.Array)
            foreach (var item in value.EnumerateArray()) ValidateNumbers(item);
        if (value.ValueKind == JsonValueKind.Object)
            foreach (var item in value.EnumerateObject()) ValidateNumbers(item.Value);
    }
}
