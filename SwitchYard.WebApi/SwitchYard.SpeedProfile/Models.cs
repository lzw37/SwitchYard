namespace SwitchYard.SpeedProfile;

/// <summary>A speed/tractive effort sample in SI units. Samples must start at zero speed.</summary>
public sealed record TractionPoint(double SpeedMps, double ForceNewtons);

/// <summary>Point-mass train model. Davis resistance is A + B*v + C*v², with v in m/s.</summary>
public sealed record TrainParameters
{
    public double MassKg { get; init; }
    /// <summary>Total inertial mass / static mass, including rotating parts; at least one.</summary>
    public double RotatingMassFactor { get; init; } = 1.05;
    public double MaxSpeedMps { get; init; }
    /// <summary>Constant available service brake force before adding resistance/gradient.</summary>
    public double MaxBrakeForceNewtons { get; init; }
    public double ResistanceConstantNewtons { get; init; }
    public double ResistanceLinearNewtonsPerMps { get; init; }
    public double ResistanceQuadraticNewtonsPerMpsSquared { get; init; }
    /// <summary>Strictly increasing speeds, non-increasing nonnegative forces. Last force is held above the table.</summary>
    /// <summary>Discrete force tables; null upgrades legacy scalar generators before solving.</summary>
    public IReadOnlyList<TractionPoint>? BrakingCurve { get; init; }
    public IReadOnlyList<TractionPoint>? ResistanceCurve { get; init; }
    public IReadOnlyList<TractionPoint> TractionCurve { get; init; } = [];
}

/// <summary>Contiguous section in travel order. Positive gradient is uphill; units are metres, m/s, and per mille.</summary>
public sealed record TrackSection
{
    public double LengthMeters { get; init; }
    public double SpeedLimitMps { get; init; }
    public double GradientPermille { get; init; }
    public double AdditionalResistanceNewtons { get; init; }
}

/// <summary>Required stop measured from the route start; dwell time is included in total time.</summary>
public sealed record TrainStop(double PositionMeters, double DwellSeconds = 0);

public sealed record SpeedProfileRequest
{
    public TrainParameters Train { get; init; } = new();
    public IReadOnlyList<TrackSection> Sections { get; init; } = [];
    public IReadOnlyList<TrainStop> Stops { get; init; } = [];
    public double InitialSpeedMps { get; init; }
    public double FinalSpeedMps { get; init; }
    public double DistanceStepMeters { get; init; } = 5;
    /// <summary>Upper bound on all output points, including dwell points (2..1,000,001).</summary>
    public int MaxPointCount { get; init; } = 200_001;
}

public enum OperatingMode { Initial, Traction, Braking, Coasting, Cruising, Dwell }

/// <summary>Acceleration and mode describe the interval ending at this point, not the next interval.</summary>
public sealed record SpeedProfilePoint(
    double PositionMeters,
    double TimeSeconds,
    double SpeedMps,
    double SpeedLimitMps,
    double AccelerationMps2,
    OperatingMode Mode);

public sealed record SpeedProfileResult
{
    public required IReadOnlyList<SpeedProfilePoint> Points { get; init; }
    public double TotalDistanceMeters { get; init; }
    public double TotalTimeSeconds { get; init; }
    public double RunningTimeSeconds { get; init; }
    public double MaxSpeedMps { get; init; }
}

/// <summary>The requested boundary conditions cannot be satisfied by this model and distance grid.</summary>
public sealed class InfeasibleProfileException(string message) : InvalidOperationException(message);
