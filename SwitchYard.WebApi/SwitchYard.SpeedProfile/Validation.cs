namespace SwitchYard.SpeedProfile;

internal sealed record ValidatedInput(
    TrainParameters Train, TractionPoint[] Curve, TrackSection[] Sections, TrainStop[] Stops,
    double TotalDistance, double InertialMass);

internal static class Validation
{
    internal static ValidatedInput Validate(SpeedProfileRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Train);
        ArgumentNullException.ThrowIfNull(request.Sections);
        ArgumentNullException.ThrowIfNull(request.Stops);
        var train = request.Train;
        Positive(train.MassKg, nameof(train.MassKg));
        Positive(train.RotatingMassFactor, nameof(train.RotatingMassFactor));
        if (train.RotatingMassFactor < 1) throw new ArgumentException("RotatingMassFactor must be at least one.");
        Positive(train.MaxSpeedMps, nameof(train.MaxSpeedMps));
        Positive(train.MaxBrakeForceNewtons, nameof(train.MaxBrakeForceNewtons));
        Nonnegative(train.ResistanceConstantNewtons, nameof(train.ResistanceConstantNewtons));
        Nonnegative(train.ResistanceLinearNewtonsPerMps, nameof(train.ResistanceLinearNewtonsPerMps));
        Nonnegative(train.ResistanceQuadraticNewtonsPerMpsSquared, nameof(train.ResistanceQuadraticNewtonsPerMpsSquared));
        Positive(request.DistanceStepMeters, nameof(request.DistanceStepMeters));
        Nonnegative(request.InitialSpeedMps, nameof(request.InitialSpeedMps));
        Nonnegative(request.FinalSpeedMps, nameof(request.FinalSpeedMps));
        if (request.MaxPointCount is < 2 or > 1_000_001)
            throw new ArgumentException("MaxPointCount must be between 2 and 1,000,001.");
        if (request.Sections.Count == 0 || request.Sections.Count > request.MaxPointCount ||
            request.Stops.Count > request.MaxPointCount)
            throw new ArgumentException("Provide a nonempty route with section/stop counts within MaxPointCount.");
        ArgumentNullException.ThrowIfNull(train.TractionCurve);
        if (train.TractionCurve.Count == 0 || train.TractionCurve.Count > 1_000_001)
            throw new ArgumentException("Provide between 1 and 1,000,001 traction samples.");
        var curve = train.TractionCurve.ToArray();
        for (int i = 0; i < curve.Length; i++)
        {
            ArgumentNullException.ThrowIfNull(curve[i]);
            Nonnegative(curve[i].SpeedMps, "TractionCurve.SpeedMps");
            Nonnegative(curve[i].ForceNewtons, "TractionCurve.ForceNewtons");
            if ((i == 0 && curve[i].SpeedMps != 0) ||
                (i > 0 && (curve[i].SpeedMps <= curve[i - 1].SpeedMps ||
                           curve[i].ForceNewtons > curve[i - 1].ForceNewtons)))
                throw new ArgumentException("Traction samples must start at zero speed, have increasing speeds and non-increasing forces.");
        }

        var mass = train.MassKg * train.RotatingMassFactor;
        Positive(mass, "Effective inertial mass");
        Positive(train.MaxSpeedMps * train.MaxSpeedMps, "Squared maximum speed");
        var sections = request.Sections.ToArray();
        double distance = 0;
        foreach (var section in sections)
        {
            ArgumentNullException.ThrowIfNull(section);
            Positive(section.LengthMeters, nameof(section.LengthMeters));
            Positive(section.SpeedLimitMps, nameof(section.SpeedLimitMps));
            Finite(section.GradientPermille, nameof(section.GradientPermille));
            Nonnegative(section.AdditionalResistanceNewtons, nameof(section.AdditionalResistanceNewtons));
            var nextDistance = distance + section.LengthMeters;
            Positive(nextDistance, "Route length");
            if (nextDistance <= distance) throw new ArgumentException("Section lengths cannot be represented at this route scale.");
            distance = nextDistance;
            var resistance0 = train.ResistanceConstantNewtons + section.AdditionalResistanceNewtons +
                TrainRunPhysics.ForceFromCoefficient(section.GradientPermille, train.MassKg);
            var resistanceMax = resistance0 + train.ResistanceLinearNewtonsPerMps * train.MaxSpeedMps +
                train.ResistanceQuadraticNewtonsPerMpsSquared * train.MaxSpeedMps * train.MaxSpeedMps;
            Finite(resistance0, "Resistance");
            Finite(resistanceMax, "Resistance at maximum speed");
            Finite((curve[0].ForceNewtons - resistance0) / mass, "Maximum acceleration");
            Finite((curve[^1].ForceNewtons - resistanceMax) / mass, "Minimum acceleration");
            var brake0 = (train.MaxBrakeForceNewtons + resistance0) / mass;
            Finite(brake0, "Effective braking");
            Finite((train.MaxBrakeForceNewtons + resistanceMax) / mass, "Effective braking at maximum speed");
            if (brake0 <= 0)
                throw new InfeasibleProfileException("This conservative model requires positive net braking down to zero speed in every section; the supplied downhill section is unsupported.");
        }
        var stops = request.Stops.ToArray();
        foreach (var stop in stops)
        {
            ArgumentNullException.ThrowIfNull(stop);
            Nonnegative(stop.PositionMeters, nameof(stop.PositionMeters));
            Nonnegative(stop.DwellSeconds, nameof(stop.DwellSeconds));
            if (stop.PositionMeters > distance) throw new ArgumentException("A stop lies outside the route.");
        }
        Array.Sort(stops, (a, b) => a.PositionMeters.CompareTo(b.PositionMeters));
        for (int i = 1; i < stops.Length; i++)
            if (stops[i].PositionMeters == stops[i - 1].PositionMeters)
                throw new ArgumentException("Stop positions must be unique.");
        if ((stops.Length > 0 && stops[0].PositionMeters == 0 && request.InitialSpeedMps != 0) ||
            (stops.Length > 0 && stops[^1].PositionMeters == distance && request.FinalSpeedMps != 0))
            throw new InfeasibleProfileException("A boundary stop requires zero boundary speed.");
        return new(train, curve, sections, stops, distance, mass);
    }

    private static void Positive(double value, string name)
    {
        if (!double.IsFinite(value) || value <= 0) throw new ArgumentException(name + " must be finite and positive.");
    }
    private static void Nonnegative(double value, string name)
    {
        if (!double.IsFinite(value) || value < 0) throw new ArgumentException(name + " must be finite and nonnegative.");
    }
    private static void Finite(double value, string name)
    {
        if (!double.IsFinite(value)) throw new ArgumentException(name + " must be finite.");
    }
}
