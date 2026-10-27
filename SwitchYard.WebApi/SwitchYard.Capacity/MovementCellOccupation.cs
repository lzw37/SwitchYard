using System.Text.Json;

namespace SwitchYard.Capacity;

/// <summary>Persisted occupation times, in seconds from the plan's day-zero midnight.</summary>
public sealed class MovementCellOccupation
{
    public string CellID { get; set; } = "";
    public string RouteID { get; set; } = "";
    public double StartSeconds { get; set; }
    public double EndSeconds { get; set; }
    public bool IsInterruptCell { get; set; }
    public string? DisplayCellID { get; set; }
    public bool IsEdited { get; set; }
}

public static class MovementCellOccupations
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static List<MovementCellOccupation>? Read(string? json) => json is null ? null :
        Normalize(JsonSerializer.Deserialize<List<MovementCellOccupation>>(json, Options));

    public static string Write(List<MovementCellOccupation> rows) => JsonSerializer.Serialize(rows, Options);

    public static List<MovementCellOccupation>? Normalize(List<MovementCellOccupation>? rows)
    {
        if (rows is null) return null; // Older clients omit the occupation data.
        if (rows.Count > 2000 || rows.Any(row => row is null || string.IsNullOrWhiteSpace(row.CellID) ||
            row.CellID.Length > 50 || row.RouteID is null || row.RouteID.Length > 256 || row.DisplayCellID?.Length > 50 ||
            !double.IsFinite(row.StartSeconds) || !double.IsFinite(row.EndSeconds)) ||
            rows.Select(row => row.CellID).Distinct(StringComparer.OrdinalIgnoreCase).Count() != rows.Count)
            throw new ArgumentException("Invalid cell occupation times.");
        return rows.Select(row => new MovementCellOccupation {
            CellID = row.CellID, RouteID = row.RouteID, StartSeconds = row.StartSeconds, EndSeconds = row.EndSeconds,
            IsInterruptCell = row.IsInterruptCell, DisplayCellID = row.DisplayCellID, IsEdited = row.IsEdited
        }).ToList();
    }
}
