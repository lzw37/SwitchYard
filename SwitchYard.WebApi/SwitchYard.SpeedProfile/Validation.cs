namespace SwitchYard.SpeedProfile;

internal sealed record ValidatedInput(
    TrainParameters Train, TractionPoint[] Curve, TractionPoint[] Resistance, TractionPoint[] NetTraction, TractionPoint[] NetBraking, TrackSection[] Sections, TrainStop[] Stops,
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

        // Only legacy callers without tables use the generators. Supplied tables always take precedence.
        TractionPoint[] Generate(Func<double, double> force) {
            const double spacing = 10 / 3.6;
            if (train.MaxSpeedMps / spacing > 1_000_000) throw new ArgumentException("Maximum speed exceeds the legacy table generation range.");
            var points = new List<TractionPoint>();
            for (int i = 0; i * spacing < train.MaxSpeedMps; i++) points.Add(new(i * spacing, force(i * spacing)));
            points.Add(new(train.MaxSpeedMps, force(train.MaxSpeedMps))); return points.ToArray();
        }
        var braking = train.BrakingCurve?.ToArray() ?? Generate(_ => train.MaxBrakeForceNewtons);
        var resistance = train.ResistanceCurve?.ToArray() ?? Generate(v => train.ResistanceConstantNewtons + train.ResistanceLinearNewtonsPerMps * v + train.ResistanceQuadraticNewtonsPerMpsSquared * v * v);
        ValidateTable(braking, "BrakingCurve"); ValidateTable(resistance, "ResistanceCurve");
        TractionPoint[] Combine(TractionPoint[] first, double sign) => first.Concat(resistance).Select(p => p.SpeedMps).Distinct().Order().Select(v =>
            new TractionPoint(v, TrainRunPhysics.TractiveEffort(v, first) + sign * TrainRunPhysics.TractiveEffort(v, resistance))).ToArray();
        var netTraction = Combine(curve, -1); var netBraking = Combine(braking, 1);
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
            var extra = section.AdditionalResistanceNewtons + TrainRunPhysics.ForceFromCoefficient(section.GradientPermille, train.MassKg);
            foreach (var point in netTraction) Finite((point.ForceNewtons - extra) / mass, "Traction acceleration");
            foreach (var point in netBraking) Finite((point.ForceNewtons + extra) / mass, "Effective braking");
            var minimumBrake = (TrainRunPhysics.MinimumEffort(0, train.MaxSpeedMps, netBraking) + extra) / mass;
            if (minimumBrake <= 0)
                throw new InfeasibleProfileException("This conservative model requires positive net braking throughout the operating speed range in every section.");
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
        return new(train, curve, resistance, netTraction, netBraking, sections, stops, distance, mass);
    }

    private static void ValidateTable(TractionPoint[] curve, string name)
    {
        if (curve.Length is < 1 or > 1_000_001) throw new ArgumentException(name + " must contain 1–1,000,001 points.");
        for (int i = 0; i < curve.Length; i++) {
            ArgumentNullException.ThrowIfNull(curve[i]);
            Nonnegative(curve[i].SpeedMps, name + ".SpeedMps"); Nonnegative(curve[i].ForceNewtons, name + ".ForceNewtons");
            if ((i == 0 && curve[i].SpeedMps != 0) || (i > 0 && curve[i].SpeedMps <= curve[i - 1].SpeedMps))
                throw new ArgumentException(name + " speeds must start at zero and strictly increase.");
        }
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
