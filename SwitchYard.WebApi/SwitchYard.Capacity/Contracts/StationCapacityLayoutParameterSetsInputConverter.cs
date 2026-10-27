using System.Text.Json;
using System.Text.Json.Serialization;

namespace SwitchYard.Capacity;

/// <summary>Reads saved inputs with the former parameter-set array and writes the current object shape.</summary>
public sealed class StationCapacityLayoutParameterSetsInputConverter : JsonConverter<StationCapacityLayoutParameterSetsInput>
{
    public override StationCapacityLayoutParameterSetsInput Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        if (root.ValueKind == JsonValueKind.Array)
        {
            return new StationCapacityLayoutParameterSetsInput
            {
                Sets = root.Deserialize<List<StationCapacityLayoutParameterSetInput>>(options) ?? new()
            };
        }
        if (root.ValueKind != JsonValueKind.Object)
            throw new JsonException("Calculation parameter sets must be an object or a legacy array.");

        var result = new StationCapacityLayoutParameterSetsInput();
        var comparison = options.PropertyNameCaseInsensitive ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var setsName = PropertyName(nameof(result.Sets), options);
        var occupationsName = PropertyName(nameof(result.RouteOccupations), options);
        foreach (var property in root.EnumerateObject())
        {
            if (string.Equals(property.Name, setsName, comparison))
                result.Sets = property.Value.Deserialize<List<StationCapacityLayoutParameterSetInput>>(options) ?? new();
            else if (string.Equals(property.Name, occupationsName, comparison))
                result.RouteOccupations = property.Value.Deserialize<Dictionary<string, List<StationCapacityLayoutRouteOccupationInput>>>(options) ?? new();
        }
        return result;
    }

    public override void Write(
        Utf8JsonWriter writer, StationCapacityLayoutParameterSetsInput value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WritePropertyName(PropertyName(nameof(value.Sets), options));
        JsonSerializer.Serialize(writer, value.Sets, options);
        writer.WritePropertyName(PropertyName(nameof(value.RouteOccupations), options));
        JsonSerializer.Serialize(writer, value.RouteOccupations, options);
        writer.WriteEndObject();
    }

    private static string PropertyName(string name, JsonSerializerOptions options) =>
        options.PropertyNamingPolicy?.ConvertName(name) ?? name;
}
