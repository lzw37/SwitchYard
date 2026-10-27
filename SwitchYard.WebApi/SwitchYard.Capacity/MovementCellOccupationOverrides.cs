using System.Text.Json;
using System.Text.Json.Serialization;

namespace SwitchYard.Capacity;

public sealed class MovementCellOccupationOverride
{
    public string RouteID { get; set; } = string.Empty;
    public int StartOccupationShift { get; set; }
    public int EndOccupationShift { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StationPlanCellID { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? StationPlanArriveMinutes { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? StationPlanDepartMinutes { get; set; }
}

public static class MovementCellOccupationOverrides
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static Dictionary<string, MovementCellOccupationOverride> Read(string? json) =>
        string.IsNullOrWhiteSpace(json) ? new() :
        JsonSerializer.Deserialize<Dictionary<string, MovementCellOccupationOverride>>(json, JsonOptions)
            ?? throw new ArgumentException("Cell occupation overrides must be an object.");

    public static string? Normalize(string? json, bool limitOccupationOffsets = true)
    {
        // Omitted values preserve existing overrides when older clients edit a movement.
        if (json is null) return null;
        if (json.Length > 131072) throw new ArgumentException("Too many cell occupation overrides.");
        var entries = Read(json);
        if (entries.Count > 2000) throw new ArgumentException("Too many cell occupation overrides.");
        foreach (var (cellID, entry) in entries)
        {
            if (string.IsNullOrWhiteSpace(cellID) || cellID.Length > 50 || entry is null ||
                string.IsNullOrWhiteSpace(entry.RouteID) || entry.RouteID.Length > 50 ||
                (limitOccupationOffsets && (Math.Abs((long)entry.StartOccupationShift) > 604800 || Math.Abs((long)entry.EndOccupationShift) > 604800)) ||
                entry.StationPlanCellID?.Length > 50 ||
                (entry.StationPlanArriveMinutes.HasValue && !double.IsFinite(entry.StationPlanArriveMinutes.Value)) ||
                (entry.StationPlanDepartMinutes.HasValue && !double.IsFinite(entry.StationPlanDepartMinutes.Value)))
                throw new ArgumentException("Invalid cell occupation override.");
        }
        return JsonSerializer.Serialize(entries.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ToDictionary(pair => pair.Key, pair => pair.Value), JsonOptions);
    }
}
