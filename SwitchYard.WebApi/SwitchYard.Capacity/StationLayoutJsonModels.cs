using System.Text.Json;
using System.Text.Json.Serialization;

namespace SwitchYard.Capacity
{
    public sealed class StationLayoutJson
    {
        [JsonPropertyName("metadata")]
        public StationLayoutJsonMetadata? Metadata { get; set; }

        [JsonPropertyName("tracks")]
        public List<StationLayoutTrackJson> Tracks { get; set; } = new();

        [JsonPropertyName("curves")]
        public List<StationLayoutCurveJson> Curves { get; set; } = new();

        [JsonPropertyName("nodes")]
        public List<StationLayoutNodeJson> Nodes { get; set; } = new();

        [JsonPropertyName("signals")]
        public List<StationLayoutSignalJson> Signals { get; set; } = new();

        [JsonPropertyName("insulationJoints")]
        public List<StationLayoutInsulationJointJson> InsulationJoints { get; set; } = new();

        [JsonPropertyName("bufferStops")]
        public List<StationLayoutBufferStopJson> BufferStops { get; set; } = new();

        [JsonPropertyName("platforms")]
        public List<StationLayoutPlatformJson> Platforms { get; set; } = new();

        [JsonPropertyName("switches")]
        public List<StationLayoutSwitchJson> Switches { get; set; } = new();

        [JsonPropertyName("cells")]
        public List<StationLayoutCellJson> Cells { get; set; } = new();

        [JsonPropertyName("annotations")]
        public List<StationLayoutAnnotationJson> Annotations { get; set; } = new();
    }

    public sealed class StationLayoutJsonMetadata
    {
        [JsonPropertyName("latestElementID")]
        public int LatestElementID { get; set; }

        [JsonPropertyName("instanceID")]
        public string? InstanceID { get; set; }

        [JsonPropertyName("stationSchemeID")]
        public string? StationSchemeID { get; set; }

        [JsonPropertyName("coordinateTransform")]
        public StationLayoutJsonCoordinateTransform? CoordinateTransform { get; set; }

        [JsonPropertyName("displayStyles")]
        public JsonElement? DisplayStyles { get; set; }

        [JsonPropertyName("gridSettings")]
        public JsonElement? GridSettings { get; set; }
    }

    public sealed class StationLayoutJsonCoordinateTransform
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

    public sealed class StationLayoutTrackJson
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

    public sealed class StationLayoutCurveJson
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
        public StationLayoutPositionJson? Start { get; set; }

        [JsonPropertyName("end")]
        public StationLayoutPositionJson? End { get; set; }

        [JsonPropertyName("center")]
        public StationLayoutPositionJson? Center { get; set; }

        [JsonPropertyName("largeArcFlag")]
        public int LargeArcFlag { get; set; }

        [JsonPropertyName("sweepFlag")]
        public int SweepFlag { get; set; }
    }

    public sealed class StationLayoutNodeJson
    {
        [JsonPropertyName("id")]
        public string? ID { get; set; }

        [JsonPropertyName("x")]
        public double X { get; set; }

        [JsonPropertyName("y")]
        public double Y { get; set; }
    }

    public sealed class StationLayoutPositionJson
    {
        [JsonPropertyName("x")]
        public double X { get; set; }

        [JsonPropertyName("y")]
        public double Y { get; set; }
    }

    public sealed class StationLayoutSignalJson
    {
        [JsonPropertyName("id")]
        public string? ID { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("position")]
        public StationLayoutPositionJson? Position { get; set; }

        [JsonPropertyName("direction")]
        public string? Direction { get; set; }

        [JsonPropertyName("bindingNodeID")]
        public string? BindingNodeID { get; set; }
    }

    public sealed class StationLayoutInsulationJointJson
    {
        [JsonPropertyName("id")]
        public string? ID { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("position")]
        public StationLayoutPositionJson? Position { get; set; }

        [JsonPropertyName("bindingNodeID")]
        public string? BindingNodeID { get; set; }
    }

    public sealed class StationLayoutBufferStopJson
    {
        [JsonPropertyName("id")]
        public string? ID { get; set; }

        [JsonPropertyName("position")]
        public StationLayoutPositionJson? Position { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("direction")]
        public string? Direction { get; set; }

        [JsonPropertyName("bindingNodeID")]
        public string? BindingNodeID { get; set; }
    }

    public sealed class StationLayoutPlatformJson
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

    public sealed class StationLayoutSwitchJson
    {
        [JsonPropertyName("id")]
        public string? ID { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("position")]
        public StationLayoutPositionJson? Position { get; set; }

        [JsonPropertyName("bindingNodeID")]
        public string? BindingNodeID { get; set; }

        [JsonPropertyName("branchVectorList")]
        public List<StationLayoutSwitchBranchVectorJson> BranchVectorList { get; set; } = new();
    }

    public sealed class StationLayoutSwitchBranchVectorJson
    {
        [JsonPropertyName("x")]
        public double X { get; set; }

        [JsonPropertyName("y")]
        public double Y { get; set; }

        [JsonPropertyName("lineID")]
        public string? LineID { get; set; }
    }

    public sealed class StationLayoutCellJson
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

    public sealed class StationLayoutAnnotationJson
    {
        [JsonPropertyName("id")]
        public string? ID { get; set; }

        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("position")]
        public StationLayoutPositionJson? Position { get; set; }

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
}
