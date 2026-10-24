// The reverse braking-envelope recurrence is adapted from TrainRuns.jl (ISC).
// Copyright 2022 Max Kannenberg, Martin Scheidt
// Source: 529cdd1d2a158077440ce4e3093a8b875a1191fb/src/behavior.jl, secureBrakingBehavior!
// See THIRD-PARTY-NOTICES.txt for the complete license and the scope of this adaptation.
namespace SwitchYard.SpeedProfile;

/// <summary>
/// Conservative maximum-effort profile on a distance grid, with reverse braking and forward traction passes.
/// Stateless and safe for concurrent calls. This is a point-mass planning model, not a signalling/brake safety model.
/// </summary>
public sealed class SpeedProfileCalculator
{
    public SpeedProfileResult Calculate(SpeedProfileRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var input = Validation.Validate(request);
        var grid = DistanceGrid.Build(input, request, cancellationToken);
        var speed = (double[])grid.SpeedLimits.Clone();
        for (int i = 0; i < speed.Length; i++)
            if (grid.StopNodes[i]) speed[i] = 0;

        if (request.FinalSpeedMps > speed[^1])
            throw new InfeasibleProfileException("Final speed exceeds the terminal speed limit.");
        speed[^1] = request.FinalSpeedMps;
        ApplyBrakingEnvelope(speed, grid, input, cancellationToken);
        if (request.InitialSpeedMps > speed[0] + SpeedTolerance(speed[0]))
            throw new InfeasibleProfileException("Initial speed is too high to satisfy downstream limits and stops.");
        speed[0] = request.InitialSpeedMps;

        for (int i = 0; i < speed.Length - 1; i++)
        {
            if ((i & 1023) == 0) cancellationToken.ThrowIfCancellationRequested();
            double distance = grid.Positions[i + 1] - grid.Positions[i];
            var section = input.Sections[grid.CellSections[i]];
            speed[i + 1] = ReachableSpeed(speed[i], speed[i + 1], distance, section, input);
        }
        if (Math.Abs(speed[^1] - request.FinalSpeedMps) > SpeedTolerance(request.FinalSpeedMps))
            throw new InfeasibleProfileException("Requested final speed cannot be reached. Check traction, route length and boundary speeds.");
        speed[^1] = request.FinalSpeedMps;
        return CreateResult(speed, grid, input, cancellationToken);
    }

    private static void ApplyBrakingEnvelope(
        double[] speed, DistanceGrid grid, ValidatedInput input, CancellationToken cancellationToken)
    {
        // Port of secureBrakingBehavior!: propagate each target's reachable entry speed backwards.
        // Use the minimum interpolated net braking across each entire speed interval.
        // Variable braking may rise or fall, so using only the exit speed is insufficient.
        for (int i = speed.Length - 2; i >= 0; i--)
        {
            if ((i & 1023) == 0) cancellationToken.ThrowIfCancellationRequested();
            double distance = grid.Positions[i + 1] - grid.Positions[i];
            var section = input.Sections[grid.CellSections[i]];
            speed[i] = BrakingEntrySpeed(speed[i + 1], speed[i], distance, section, input);
        }
    }

    /// <summary>
    /// Reserve a full stop from the train maximum speed, reusing the same reverse
    /// braking step as Calculate. Conditions are held constant before the route.
    /// The returned distance is rounded up to the requested spatial grid.
    /// </summary>
    public double CalculateBrakingDistance(TrainParameters train, TrackSection conditions,
        double distanceStepMeters = 5, int maxPointCount = 100_001,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(train);
        ArgumentNullException.ThrowIfNull(conditions);
        var section = conditions with { LengthMeters = distanceStepMeters, SpeedLimitMps = train.MaxSpeedMps };
        var input = Validation.Validate(new SpeedProfileRequest {
            Train = train, Sections = [section], InitialSpeedMps = train.MaxSpeedMps,
            FinalSpeedMps = 0, DistanceStepMeters = distanceStepMeters, MaxPointCount = maxPointCount
        });
        double exit = 0;
        for (int cells = 1; cells < maxPointCount; cells++)
        {
            if ((cells & 255) == 0) cancellationToken.ThrowIfCancellationRequested();
            exit = BrakingEntrySpeed(exit, train.MaxSpeedMps, distanceStepMeters, section, input);
            if (exit >= train.MaxSpeedMps - SpeedTolerance(train.MaxSpeedMps))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var distance = cells * distanceStepMeters;
                if (!double.IsFinite(distance)) throw new ArgumentException("Braking distance exceeds the numerical range.");
                return distance;
            }
        }
        throw new ArgumentException("Braking distance exceeds MaxPointCount. Increase the point budget or spatial step.");
    }

    private static double BrakingEntrySpeed(double exit, double cap, double distance,
        TrackSection section, ValidatedInput input)
    {
        if (cap <= exit) return cap;
        bool Feasible(double entry) =>
            (entry * entry - exit * exit) / (2 * distance) <= BrakeDeceleration(exit, entry, section, input);
        if (Feasible(cap)) return cap;
        double lower = exit, upper = cap;
        for (int iteration = 0; iteration < 40; iteration++) {
            var middle = (lower + upper) / 2;
            if (Feasible(middle)) lower = middle; else upper = middle;
        }
        return lower;
    }

    private static double ReachableSpeed(
        double start, double cap, double distance, TrackSection section, ValidatedInput input)
    {
        double acceleration = TractionAcceleration(start, start, section, input);
        double nextSquared = TrainRunPhysics.NextSpeedSquared(start, acceleration, distance);
        if (nextSquared < 0)
            throw new InfeasibleProfileException("The train stalls within a distance cell. Check traction/gradient or reduce DistanceStepMeters.");
        double upper = Math.Min(cap, Math.Sqrt(nextSquared));
        if (upper <= start || acceleration <= 0) return upper;

        // The minimum over an expanding speed interval is non-increasing even when resistance
        // is non-monotonic. Bisection therefore preserves force bounds across every table knot.
        double startSquared = start * start;
        bool Reachable(double end) =>
            (end * end - startSquared) / (2 * distance) <= TractionAcceleration(start, end, section, input);
        if (Reachable(upper)) return upper;
        double lower = start;
        for (int iteration = 0; iteration < 40; iteration++)
        {
            double middle = lower + (upper - lower) / 2;
            if (Reachable(middle)) lower = middle;
            else upper = middle;
        }
        return lower;
    }

    private static SpeedProfileResult CreateResult(
        double[] speed, DistanceGrid grid, ValidatedInput input, CancellationToken cancellationToken)
    {
        var points = new List<SpeedProfilePoint>(speed.Length);
        double time = 0, runningTime = 0, maximumSpeed = speed[0];
        points.Add(new(0, 0, speed[0], grid.SpeedLimits[0], 0, OperatingMode.Initial));
        AddDwell(0);

        for (int i = 1; i < speed.Length; i++)
        {
            if ((i & 1023) == 0) cancellationToken.ThrowIfCancellationRequested();
            double distance = grid.Positions[i] - grid.Positions[i - 1];
            double start = speed[i - 1], end = speed[i];
            if (start + end <= 0)
                throw new InfeasibleProfileException("The train cannot move from rest with the supplied traction and resistance.");
            double dt = TrainRunPhysics.TravelTime(distance, start, end);
            double acceleration = (end - start) * (end + start) / (2 * distance);
            var section = input.Sections[grid.CellSections[i - 1]];
            double availableTraction = TractionAcceleration(start, end, section, input);
            double availableBraking = BrakeDeceleration(start, end, section, input);
            const double forceTolerance = 1e-7; // acceleration tolerance, m/s²
            if (!double.IsFinite(acceleration) ||
                acceleration > availableTraction + forceTolerance ||
                acceleration < -availableBraking - forceTolerance)
                throw new InfeasibleProfileException("A distance cell cannot satisfy both force bounds. Reduce DistanceStepMeters.");
            time += dt;
            runningTime += dt;
            EnsureFiniteTime(time);
            double effort = acceleration * input.InertialMass + Resistance((start + end) / 2, section, input);
            var mode = Math.Abs(acceleration) < 1e-9 ? OperatingMode.Cruising :
                Math.Abs(effort) < 1e-5 ? OperatingMode.Coasting :
                effort > 0 ? OperatingMode.Traction : OperatingMode.Braking;
            maximumSpeed = Math.Max(maximumSpeed, end);
            points.Add(new(grid.Positions[i], time, end, grid.SpeedLimits[i], acceleration, mode));
            AddDwell(i);
        }
        cancellationToken.ThrowIfCancellationRequested();
        return new()
        {
            Points = points.AsReadOnly(),
            TotalDistanceMeters = input.TotalDistance,
            TotalTimeSeconds = time,
            RunningTimeSeconds = runningTime,
            MaxSpeedMps = maximumSpeed
        };

        void AddDwell(int i)
        {
            if (grid.DwellSeconds[i] <= 0) return;
            time += grid.DwellSeconds[i];
            EnsureFiniteTime(time);
            points.Add(new(grid.Positions[i], time, 0, grid.SpeedLimits[i], 0, OperatingMode.Dwell));
        }
    }

    private static double ExtraResistance(TrackSection section, TrainParameters train) =>
        section.AdditionalResistanceNewtons + TrainRunPhysics.ForceFromCoefficient(section.GradientPermille, train.MassKg);

    private static double Resistance(double speed, TrackSection section, ValidatedInput input) =>
        TrainRunPhysics.TractiveEffort(speed, input.Resistance) + ExtraResistance(section, input.Train);

    private static double TractionAcceleration(double from, double to, TrackSection section, ValidatedInput input) =>
        (TrainRunPhysics.MinimumEffort(from, to, input.NetTraction) - ExtraResistance(section, input.Train)) / input.InertialMass;

    private static double BrakeDeceleration(double from, double to, TrackSection section, ValidatedInput input) =>
        (TrainRunPhysics.MinimumEffort(from, to, input.NetBraking) + ExtraResistance(section, input.Train)) / input.InertialMass;

    private static double SpeedTolerance(double speed) => 1e-8 * Math.Max(1, speed);

    private static void EnsureFiniteTime(double time)
    {
        if (!double.IsFinite(time))
            throw new InfeasibleProfileException("Travel or dwell time exceeds the numerical range.");
    }
}
