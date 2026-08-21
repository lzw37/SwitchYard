using System.Text.Json;
using System.Text.Json.Serialization;

namespace SwitchYard.StationLayout;

public sealed class StationLayoutDocument
{
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

public sealed class StationLayoutMetadata
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

public sealed class StationLayoutCoordinateTransform
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

public sealed class StationLayoutTrack
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

public sealed class StationLayoutCurve
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

public sealed class StationLayoutNode
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

public sealed class StationLayoutPosition
{
    [JsonPropertyName("x")]
    public double X { get; set; }

    [JsonPropertyName("y")]
    public double Y { get; set; }
}

public sealed class StationLayoutSignal
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

public sealed class StationLayoutInsulationJoint
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

public sealed class StationLayoutBufferStop
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

public sealed class StationLayoutPlatform
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

public sealed class StationLayoutSwitch
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

public sealed class StationLayoutSwitchBranch
{
    [JsonPropertyName("x")]
    public double X { get; set; }

    [JsonPropertyName("y")]
    public double Y { get; set; }

    [JsonPropertyName("lineID")]
    public string? LineID { get; set; }
}

public sealed class StationLayoutCell
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

public sealed class StationLayoutAnnotation
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
