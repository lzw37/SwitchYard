using System.Text.Json;

namespace SwitchYard.Capacity;

/// <summary>Train categories allowed on a route. Stored as a JSON array, like the other route lists.</summary>
public static class StationRouteTrainTypes
{
    public const string PassengerTrain = "PassengerTrain";
    public const string FreightTrain = "FreightTrain";
    public const string PassengerCar = "PassengerCar";
    public const string FreightCar = "FreightCar";

    public static string[] AllowedFor(string? routeType)
    {
        var type = string.Concat((routeType ?? "").Where(c => !char.IsWhiteSpace(c))).ToLowerInvariant();
        return type is "arrival" or "arr" or "接车" or "接车进路" or "departure" or "dep" or "发车" or "发车进路"
            ? [PassengerTrain, FreightTrain]
            : [PassengerCar, FreightCar];
    }

    public static string DefaultJson(string? routeType) => JsonSerializer.Serialize(AllowedFor(routeType));

    public static string Normalize(string? value, string? routeType)
    {
        if (string.IsNullOrWhiteSpace(value)) return DefaultJson(routeType);
        string[]? selected;
        try { selected = JsonSerializer.Deserialize<string[]>(value); }
        catch (JsonException ex) { throw new ArgumentException("TrainTypes must be a JSON array of train type identifiers.", ex); }
        if (selected is null) throw new ArgumentException("TrainTypes must be a JSON array of train type identifiers.");
        var allowed = AllowedFor(routeType);
        if (selected.Any(type => !allowed.Contains(type, StringComparer.Ordinal)))
            throw new ArgumentException($"TrainTypes for this route may only contain: {string.Join(", ", allowed)}.");
        // Stable ordering and no duplicates, including explicit empty selections.
        return JsonSerializer.Serialize(allowed.Where(type => selected.Contains(type, StringComparer.Ordinal)));
    }
}
