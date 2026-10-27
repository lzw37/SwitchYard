using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace SwitchYard.StationLayout;

public sealed class StationLayoutDocument : StationLayoutJsonObject
{
    public const string ArchiveFormat = "switchyard.station-layout";

    [JsonPropertyName("format")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Format { get; set; }

    [JsonPropertyName("formatVersion")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? FormatVersion { get; set; }

    [JsonIgnore]
    public bool IsArchive => Format == ArchiveFormat && FormatVersion == 1;

    // Keep the JSON shape as well as its values: a missing optional field must
    // not turn into a DTO default (for example fontSize: 0) during persistence.
    [JsonIgnore]
    public JsonObject? ArchiveJson { get; set; }

    public string ToJson() => ArchiveJson?.ToJsonString() ?? JsonSerializer.Serialize(this);

    public static StationLayoutDocument FromJson(string json, JsonSerializerOptions? legacyOptions = null)
    {
        var source = JsonNode.Parse(json) as JsonObject;
        var archive = source?["format"] is JsonValue format && format.TryGetValue<string>(out var name) && name == ArchiveFormat;
        var document = JsonSerializer.Deserialize<StationLayoutDocument>(json, archive ? null : legacyOptions)
            ?? throw new JsonException("Station-layout document is empty.");
        if (document.IsArchive) document.ArchiveJson = source;
        return document;
    }

    public void SetArchiveScope(string scopeId, string schemeId, long? revision = null)
    {
        Metadata ??= new StationLayoutMetadata();
        Metadata.InstanceID = scopeId;
        Metadata.StationSchemeID = schemeId;
        if (revision.HasValue) Metadata.Revision = revision;
        foreach (var cell in Cells)
        {
            cell.InstanceID = scopeId;
            cell.StationSchemeID = schemeId;
        }
        if (ArchiveJson is null) return;
        var metadata = ArchiveJson["metadata"] as JsonObject;
        if (metadata is null) ArchiveJson["metadata"] = metadata = new JsonObject();
        metadata["instanceID"] = scopeId;
        metadata["stationSchemeID"] = schemeId;
        if (revision.HasValue) metadata["revision"] = revision.Value;
        if (ArchiveJson["cells"] is not JsonArray cells) return;
        for (var index = 0; index < cells.Count; index++)
        {
            if (cells[index] is not JsonObject cell) continue;
            cell["instanceID"] = scopeId;
            cell["stationSchemeID"] = schemeId;
            if (index < Cells.Count && string.IsNullOrWhiteSpace(cell["id"]?.GetValue<string>()))
            {
                cell["id"] = Cells[index].ID;
                cell.Remove("isNew");
                cell.Remove("IsNew");
            }
        }
    }

    [JsonPropertyName("metadata")]
    public StationLayoutMetadata? Metadata { get; set; }

    [JsonPropertyName("tracks")]
    public List<StationLayoutTrack> Tracks { get; set; } = [];

    [JsonPropertyName("curves")]
    public List<StationLayoutCurve> Curves { get; set; } = [];

    [JsonPropertyName("nodes")]
    public List<StationLayoutNode> Nodes { get; set; } = [];

    [JsonPropertyName("signals")]
    public List<StationLayoutSignal> Signals { get; set; } = [];

    [JsonPropertyName("insulationJoints")]
    public List<StationLayoutInsulationJoint> InsulationJoints { get; set; } = [];

    [JsonPropertyName("bufferStops")]
    public List<StationLayoutBufferStop> BufferStops { get; set; } = [];

    [JsonPropertyName("platforms")]
    public List<StationLayoutPlatform> Platforms { get; set; } = [];

    [JsonPropertyName("switches")]
    public List<StationLayoutSwitch> Switches { get; set; } = [];

    [JsonPropertyName("cells")]
    public List<StationLayoutCell> Cells { get; set; } = [];

    [JsonPropertyName("annotations")]
    public List<StationLayoutAnnotation> Annotations { get; set; } = [];
}

public sealed class StationLayoutMetadata : StationLayoutJsonObject
{
    [JsonPropertyName("revision")]
    public long? Revision { get; set; }

    [JsonPropertyName("latestElementID")]
    public int LatestElementID { get; set; }

    [JsonPropertyName("instanceID")]
    public string? InstanceID { get; set; }

    [JsonPropertyName("stationSchemeID")]
    public string? StationSchemeID { get; set; }

    [JsonPropertyName("coordinateTransform")]
    public StationLayoutCoordinateTransform? CoordinateTransform { get; set; }

    [JsonPropertyName("displayStyles")]
    public JsonElement? DisplayStyles { get; set; }

    [JsonPropertyName("gridSettings")]
    public JsonElement? GridSettings { get; set; }
}

public sealed class StationLayoutCoordinateTransform : StationLayoutJsonObject
{
    [JsonPropertyName("applied")]
    public bool Applied { get; set; }

    [JsonPropertyName("minX")]
    public double MinX { get; set; }

    [JsonPropertyName("minY")]
    public double MinY { get; set; }

    [JsonPropertyName("scale")]
    public double Scale { get; set; } = 1;

    [JsonPropertyName("padding")]
    public double Padding { get; set; }
}

public sealed class StationLayoutTrack : StationLayoutJsonObject
{
    [JsonPropertyName("id")]
    public string? ID { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("arrowDirection")]
    public string? ArrowDirection { get; set; }

    [JsonPropertyName("arrowType")]
    public string? ArrowType { get; set; }

    [JsonPropertyName("x1")]
    public double X1 { get; set; }

    [JsonPropertyName("y1")]
    public double Y1 { get; set; }

    [JsonPropertyName("x2")]
    public double X2 { get; set; }

    [JsonPropertyName("y2")]
    public double Y2 { get; set; }

    [JsonPropertyName("fromNodeID")]
    public string? FromNodeID { get; set; }

    [JsonPropertyName("toNodeID")]
    public string? ToNodeID { get; set; }
}

public sealed class StationLayoutCurve : StationLayoutJsonObject
{
    [JsonPropertyName("id")]
    public string? ID { get; set; }

    [JsonPropertyName("nodeID")]
    public string? NodeID { get; set; }

    [JsonPropertyName("tangentLinkID1")]
    public string? TangentLinkID1 { get; set; }

    [JsonPropertyName("tangentLinkID2")]
    public string? TangentLinkID2 { get; set; }

    [JsonPropertyName("radius")]
    public double Radius { get; set; }

    [JsonPropertyName("angle")]
    public double Angle { get; set; }

    [JsonPropertyName("tangentDistance")]
    public double TangentDistance { get; set; }

    [JsonPropertyName("start")]
    public StationLayoutPosition? Start { get; set; }

    [JsonPropertyName("end")]
    public StationLayoutPosition? End { get; set; }

    [JsonPropertyName("center")]
    public StationLayoutPosition? Center { get; set; }

    [JsonPropertyName("largeArcFlag")]
    public int LargeArcFlag { get; set; }

    [JsonPropertyName("sweepFlag")]
    public int SweepFlag { get; set; }
}

public sealed class StationLayoutNode : StationLayoutJsonObject
{
    [JsonPropertyName("id")]
    public string? ID { get; set; }

    [JsonPropertyName("x")]
    public double X { get; set; }

    [JsonPropertyName("y")]
    public double Y { get; set; }

    [JsonPropertyName("adjacentLineIDList")]
    public List<string> AdjacentLineIDList { get; set; } = [];
}

public sealed class StationLayoutPosition : StationLayoutJsonObject
{
    [JsonPropertyName("x")]
    public double X { get; set; }

    [JsonPropertyName("y")]
    public double Y { get; set; }
}

public sealed class StationLayoutSignal : StationLayoutJsonObject
{
    [JsonPropertyName("id")]
    public string? ID { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("position")]
    public StationLayoutPosition? Position { get; set; }

    [JsonPropertyName("direction")]
    public string? Direction { get; set; }

    [JsonPropertyName("bindingNodeID")]
    public string? BindingNodeID { get; set; }
}

public sealed class StationLayoutInsulationJoint : StationLayoutJsonObject
{
    [JsonPropertyName("id")]
    public string? ID { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("position")]
    public StationLayoutPosition? Position { get; set; }

    [JsonPropertyName("bindingNodeID")]
    public string? BindingNodeID { get; set; }
}

public sealed class StationLayoutBufferStop : StationLayoutJsonObject
{
    [JsonPropertyName("id")]
    public string? ID { get; set; }

    [JsonPropertyName("position")]
    public StationLayoutPosition? Position { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("direction")]
    public string? Direction { get; set; }

    [JsonPropertyName("bindingNodeID")]
    public string? BindingNodeID { get; set; }
}

public sealed class StationLayoutPlatform : StationLayoutJsonObject
{
    [JsonPropertyName("id")]
    public string? ID { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("x")]
    public double X { get; set; }

    [JsonPropertyName("y")]
    public double Y { get; set; }

    [JsonPropertyName("width")]
    public double Width { get; set; }

    [JsonPropertyName("height")]
    public double Height { get; set; }
}

public sealed class StationLayoutSwitch : StationLayoutJsonObject
{
    [JsonPropertyName("id")]
    public string? ID { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("position")]
    public StationLayoutPosition? Position { get; set; }

    [JsonPropertyName("bindingNodeID")]
    public string? BindingNodeID { get; set; }

    [JsonPropertyName("branchVectorList")]
    public List<StationLayoutSwitchBranch> BranchVectorList { get; set; } = [];
}

public sealed class StationLayoutSwitchBranch : StationLayoutJsonObject
{
    [JsonPropertyName("x")]
    public double X { get; set; }

    [JsonPropertyName("y")]
    public double Y { get; set; }

    [JsonPropertyName("lineID")]
    public string? LineID { get; set; }
}

public sealed class StationLayoutCell : StationLayoutJsonObject
{
    [JsonPropertyName("instanceID")]
    public string? InstanceID { get; set; }

    [JsonPropertyName("stationSchemeID")]
    public string? StationSchemeID { get; set; }

    [JsonPropertyName("id")]
    public string? ID { get; set; }

    [JsonPropertyName("linkIDList")]
    public string? LinkIDList { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

public sealed class StationLayoutAnnotation : StationLayoutJsonObject
{
    [JsonPropertyName("id")]
    public string? ID { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("position")]
    public StationLayoutPosition? Position { get; set; }

    [JsonPropertyName("fontFamily")]
    public string? FontFamily { get; set; }

    [JsonPropertyName("fontSize")]
    public double FontSize { get; set; }

    [JsonPropertyName("fontWeight")]
    public string? FontWeight { get; set; }

    [JsonPropertyName("fontStyle")]
    public string? FontStyle { get; set; }

    [JsonPropertyName("angle")]
    public double Angle { get; set; }

    [JsonPropertyName("textColor")]
    public string? TextColor { get; set; }
}

/// <summary>Preserves host and future-version extensions through JSON round trips.</summary>
public abstract class StationLayoutJsonObject
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }
}
