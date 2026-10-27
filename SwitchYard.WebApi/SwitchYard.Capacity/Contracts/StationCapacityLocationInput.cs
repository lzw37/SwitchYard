using System.Text.Json;
using System.Text.Json.Serialization;

namespace SwitchYard.Capacity;

/// <summary>A physical event location and its optional dwelling anchor.</summary>
[JsonConverter(typeof(StationCapacityLocationInputConverter))]
public sealed class StationCapacityLocationInput
{
    public string? NodeId { get; set; }
    public string? AnchorId { get; set; }
    public string? DwellingRouteId { get; set; }

    [JsonIgnore]
    internal string? LegacySolverKey { get; set; }

    public static StationCapacityLocationInput FromLegacyToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) throw new JsonException("候选地点不能为空。");
        if (!token.TrimStart().StartsWith('['))
            return new() { NodeId = token, LegacySolverKey = token };

        using var document = JsonDocument.Parse(token);
        var values = document.RootElement;
        if (values.ValueKind != JsonValueKind.Array || values.GetArrayLength() != 3 ||
            values.EnumerateArray().Any(value => value.ValueKind is not (JsonValueKind.String or JsonValueKind.Null)))
            throw new JsonException("旧候选地点必须是包含三个字符串或 null 的数组。");
        return new()
        {
            NodeId = values[0].GetString(),
            AnchorId = values[1].GetString(),
            DwellingRouteId = values[2].GetString()
        };
    }

    public string ToSolverKey() => LegacySolverKey is not null && NodeId == LegacySolverKey &&
        AnchorId is null && DwellingRouteId is null
            ? LegacySolverKey
            : JsonSerializer.Serialize(new[] { NodeId, AnchorId, DwellingRouteId });
}

/// <summary>Writes named location fields while accepting earlier string encodings.</summary>
public sealed class StationCapacityLocationInputConverter : JsonConverter<StationCapacityLocationInput>
{
    public override StationCapacityLocationInput Read(ref Utf8JsonReader reader, Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
            return StationCapacityLocationInput.FromLegacyToken(reader.GetString()!);
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("候选地点必须是对象或旧格式字符串。");

        using var document = JsonDocument.ParseValue(ref reader);
        var result = new StationCapacityLocationInput();
        foreach (var property in document.RootElement.EnumerateObject())
        {
            var isNode = string.Equals(property.Name, "nodeId", StringComparison.OrdinalIgnoreCase);
            var isAnchor = string.Equals(property.Name, "anchorId", StringComparison.OrdinalIgnoreCase);
            var isRoute = string.Equals(property.Name, "dwellingRouteId", StringComparison.OrdinalIgnoreCase);
            if (!isNode && !isAnchor && !isRoute) continue;
            if (property.Value.ValueKind is not (JsonValueKind.String or JsonValueKind.Null))
                throw new JsonException($"候选地点的 {property.Name} 必须是字符串或 null。");
            var value = property.Value.GetString();
            if (isNode) result.NodeId = value;
            else if (isAnchor) result.AnchorId = value;
            else result.DwellingRouteId = value;
        }
        return result;
    }

    public override void Write(Utf8JsonWriter writer, StationCapacityLocationInput value,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("nodeId", value.NodeId);
        writer.WriteString("anchorId", value.AnchorId);
        writer.WriteString("dwellingRouteId", value.DwellingRouteId);
        writer.WriteEndObject();
    }
}
