namespace SwitchYard.Capacity
{
    public sealed class StationSchemeLookupRow
    {
        public string? ID { get; set; }

        public string? Name { get; set; }

        public string? DisplayStyles { get; set; }
    }

    public sealed class StationNodeRow
    {
        public int ID { get; set; }

        public double X { get; set; }

        public double Y { get; set; }
    }

    public sealed class StationLinkRow
    {
        public int ID { get; set; }

        public string? Name { get; set; }

        public string? ArrowDirection { get; set; }

        public string? ArrowType { get; set; }

        public int FromNodeID { get; set; }

        public int ToNodeID { get; set; }
    }

    public sealed class StationCurveRow
    {
        public string? ID { get; set; }

        public string? BindingNodeID { get; set; }

        public string? BindingLink1ID { get; set; }

        public string? BindingLink2ID { get; set; }

        public string? VertexNodeID { get; set; }

        public string? TangentLinkID1 { get; set; }

        public string? TangentLinkID2 { get; set; }

        public object? Radius { get; set; }

        public double Angle { get; set; }

        public double TangentDistance { get; set; }

        public double StartX { get; set; }

        public double StartY { get; set; }

        public double EndX { get; set; }

        public double EndY { get; set; }

        public double CenterX { get; set; }

        public double CenterY { get; set; }

        public int LargeArcFlag { get; set; }

        public int SweepFlag { get; set; }
    }

    public sealed class StationSignalRow
    {
        public string? ID { get; set; }

        public string? Name { get; set; }

        public string? Type { get; set; }

        public string? Direction { get; set; }

        public string? BindingNodeID { get; set; }
    }

    public sealed class StationInsulationJointRow
    {
        public string? ID { get; set; }

        public string? Type { get; set; }

        public string? BindingNodeID { get; set; }
    }

    public sealed class StationBufferStopRow
    {
        public string? ID { get; set; }

        public string? Type { get; set; }

        public string? Direction { get; set; }

        public string? BindingNodeID { get; set; }
    }

    public sealed class StationPlatformRow
    {
        public string? ID { get; set; }

        public string? Name { get; set; }

        public double X { get; set; }

        public double Y { get; set; }

        public double Width { get; set; }

        public double Height { get; set; }
    }

    public sealed class StationSwitchRow
    {
        public string? ID { get; set; }

        public string? Name { get; set; }

        public string? Type { get; set; }

        public string? BindingNodeID { get; set; }
    }

    public sealed class DatabaseNameLookupRow
    {
        public string? Name { get; set; }
    }

    public sealed class SwitchBranchVectorRow
    {
        public string? SwitchID { get; set; }

        public int Sequence { get; set; }

        public double X { get; set; }

        public double Y { get; set; }

        public string? BindingLinkID { get; set; }
    }

    public sealed class StationAnnotationRow
    {
        public string? ID { get; set; }

        public string? Text { get; set; }

        public double X { get; set; }

        public double Y { get; set; }

        public string? FontFamily { get; set; }

        public double FontSize { get; set; }

        public string? FontWeight { get; set; }

        public string? FontStyle { get; set; }

        public double Angle { get; set; }

        public string? TextColor { get; set; }
    }
}
