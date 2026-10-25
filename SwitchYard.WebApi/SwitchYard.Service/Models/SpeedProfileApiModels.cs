using System.Text.Json.Serialization;

namespace SwitchYard.Service.Models;

// Public HTTP units are metres, seconds, tonnes, kN and km/h. The adapter owns SI conversion.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SpeedProfileCalculationRequest
{
    [JsonRequired] public SpeedProfileRouteInput Profile { get; init; } = null!;
    [JsonRequired] public SpeedProfileTrainInput Train { get; init; } = null!;
    [JsonRequired] public SpeedProfileSignalInput Signal { get; init; } = null!;
    [JsonRequired] public SpeedProfileOptionsInput Options { get; init; } = null!;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SpeedProfileTrainInput
{
    [JsonRequired] public double LengthM { get; init; }
    [JsonRequired] public double MassT { get; init; }
    [JsonRequired] public double RotatingMassFactor { get; init; }
    [JsonRequired] public double MaxSpeedKmh { get; init; }
    [JsonRequired] public double MaxBrakeForceKn { get; init; }
    [JsonRequired] public double ResistanceA { get; init; }
    [JsonRequired] public double ResistanceB { get; init; }
    [JsonRequired] public double ResistanceC { get; init; }
    public IReadOnlyList<SpeedProfileForceInput>? BrakingCurve { get; init; }
    public IReadOnlyList<SpeedProfileForceInput>? ResistanceCurve { get; init; }
    [JsonRequired] public IReadOnlyList<SpeedProfileForceInput> TractionCurve { get; init; } = null!;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SpeedProfileForceInput
{
    [JsonRequired] public double SpeedKmh { get; init; }
    [JsonRequired] public double ForceKn { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SpeedProfileSignalInput
{
    [JsonRequired] public double RouteSettingS { get; init; }
    [JsonRequired] public double ReactionS { get; init; }
    [JsonRequired] public double ClearanceS { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SpeedProfileOptionsInput
{
    /// <summary>arrival, departure, through or shunting. Shunting uses the original route with editable desired boundary speeds.</summary>
    [JsonRequired] public string Mode { get; init; } = null!;
    /// <summary>Desired entry-speed upper bound; the calculated speed may be lower.</summary>
    [JsonRequired] public double InitialSpeedKmh { get; init; }
    /// <summary>Desired exit-speed upper bound; the calculated speed may be lower.</summary>
    [JsonRequired] public double FinalSpeedKmh { get; init; }
    /// <summary>User-entered first departure circuit after a departure or through route; 0 keeps legacy scope.</summary>
    public double DepartureSectionLengthM { get; init; }
    /// <summary>Optional entry braking reserve for arrival/through runs. Null means automatic; an entered zero is used as zero.</summary>
    public double? BrakingDistanceM { get; init; }
    /// <summary>Optional speed limit in the entry braking range; null uses the train maximum.</summary>
    public double? BrakingMaxSpeedKmh { get; init; }
    /// <summary>Optional speed limit in the first departure circuit; null uses the train maximum.</summary>
    public double? DepartureSectionMaxSpeedKmh { get; init; }
    public double StepM { get; init; } = 5;
    public int MaxPointCount { get; init; } = 100_001;
    public IReadOnlyList<SpeedProfileStopInput> Stops { get; init; } = [];
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SpeedProfileStopInput
{
    [JsonRequired] public double PositionM { get; init; }
    public double DwellS { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SpeedProfileRouteInput
{
    [JsonRequired] public double LengthM { get; init; }
    [JsonRequired] public IReadOnlyList<SpeedProfileSegmentInput> Segments { get; init; } = null!;
    public IReadOnlyList<SpeedProfileDeviceInput> Devices { get; init; } = [];
    public IReadOnlyList<SpeedProfileJointInput> Joints { get; init; } = [];
    public IReadOnlyList<SpeedProfileCellInput> Cells { get; init; } = [];
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SpeedProfileSegmentInput
{
    [JsonRequired] public double StartM { get; init; }
    [JsonRequired] public double EndM { get; init; }
    [JsonRequired] public double SpeedLimitKmh { get; init; }
    public double GradientPermille { get; init; }
    public double AdditionalResistanceKn { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SpeedProfileDeviceInput
{
    [JsonRequired] public string Id { get; init; } = null!;
    [JsonRequired] public string Label { get; init; } = null!;
    [JsonRequired] public string Type { get; init; } = null!;
    [JsonRequired] public double PositionM { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record SpeedProfileJointInput
{
    [JsonRequired] public string Id { get; init; } = null!;
    [JsonRequired] public string Label { get; init; } = null!;
    [JsonRequired] public double PositionM { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record SpeedProfileCellInput
{
    [JsonRequired] public string Id { get; init; } = null!;
    [JsonRequired] public string Label { get; init; } = null!;
    [JsonRequired] public double StartM { get; init; }
    [JsonRequired] public double EndM { get; init; }
    public bool EntryBoundaryKnown { get; init; } = true;
    public bool ExitBoundaryKnown { get; init; } = true;
}

public sealed record SpeedProfileSample(double PositionM, double SpeedKmh, double LimitKmh, double TimeS);
public sealed record SpeedProfileJointTime : SpeedProfileJointInput
{
    public double HeadTimeS { get; init; }
    public double? TailTimeS { get; init; }
    public double? ReleaseTimeS { get; init; }
}
public sealed record SpeedProfileCellOccupancy : SpeedProfileCellInput
{
    public double? EntryTimeS { get; init; }
    public double? ExitTimeS { get; init; }
    public double? ReleaseTimeS { get; init; }
}
public sealed record SpeedProfileCalculationResponse
{
    /// <summary>Effective braking reserve before an arrival or through route; all result positions include this offset.</summary>
    public double ApproachDistanceM { get; init; }
    public double DepartureSectionLengthM { get; init; }
    public required IReadOnlyList<SpeedProfileSample> Samples { get; init; }
    public required IReadOnlyList<SpeedProfileJointTime> JointTimes { get; init; }
    public required IReadOnlyList<SpeedProfileCellOccupancy> CellOccupancies { get; init; }
    public double LengthM { get; init; }
    public double TrainLengthM { get; init; }
    public required string Mode { get; init; }
    public double DurationS { get; init; }
    public double MotionStartS { get; init; }
    public double TotalTimeS { get; init; }
    public double RunningTimeS { get; init; }
    public double DwellTimeS { get; init; }
    public double MaxSpeedKmh { get; init; }
    public required IReadOnlyList<string> Warnings { get; init; }
    public string Engine { get; init; } = "SwitchYard.SpeedProfile";
}
public sealed record SpeedProfileApiError(string Code, string Message,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyDictionary<string, string[]>? Errors = null);
