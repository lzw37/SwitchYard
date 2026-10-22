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
        // Extension: use section-specific force/gradient/Davis braking rather than constant net deceleration.
        // Since resistance increases with speed, braking at the lower (exit) speed is conservative.
        for (int i = speed.Length - 2; i >= 0; i--)
        {
            if ((i & 1023) == 0) cancellationToken.ThrowIfCancellationRequested();
            double distance = grid.Positions[i + 1] - grid.Positions[i];
            var section = input.Sections[grid.CellSections[i]];
            double deceleration = BrakeDeceleration(speed[i + 1], section, input);
            double entrySquared = TrainRunPhysics.NextSpeedSquared(speed[i + 1], deceleration, distance);
            speed[i] = Math.Min(speed[i], Math.Sqrt(entrySquared));
        }
    }

    private static double ReachableSpeed(
        double start, double cap, double distance, TrackSection section, ValidatedInput input)
    {
        double acceleration = TractionAcceleration(start, section, input);
        double nextSquared = TrainRunPhysics.NextSpeedSquared(start, acceleration, distance);
        if (nextSquared < 0)
            throw new InfeasibleProfileException("The train stalls within a distance cell. Check traction/gradient or reduce DistanceStepMeters.");
        double upper = Math.Min(cap, Math.Sqrt(nextSquared));
        if (upper <= start || acceleration <= 0) return upper;

        // Use the smallest available traction acceleration across the cell. For the supported
        // non-increasing effort and nonnegative Davis coefficients this is at its highest speed.
        // The resulting scalar residual is monotone; fixed-count bisection cannot diverge.
        double startSquared = start * start;
        bool Reachable(double end) =>
            (end * end - startSquared) / (2 * distance) <= TractionAcceleration(end, section, input);
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
            double availableTraction = TractionAcceleration(Math.Max(start, end), section, input);
            double availableBraking = BrakeDeceleration(Math.Min(start, end), section, input);
            const double forceTolerance = 1e-7; // acceleration tolerance, m/s²
            if (!double.IsFinite(acceleration) ||
                acceleration > availableTraction + forceTolerance ||
                acceleration < -availableBraking - forceTolerance)
                throw new InfeasibleProfileException("A distance cell cannot satisfy both force bounds. Reduce DistanceStepMeters.");
            time += dt;
            runningTime += dt;
            EnsureFiniteTime(time);
            double effort = acceleration * input.InertialMass + Resistance((start + end) / 2, section, input.Train);
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

    private static double Resistance(double speed, TrackSection section, TrainParameters train) =>
        train.ResistanceConstantNewtons +
        train.ResistanceLinearNewtonsPerMps * speed +
        train.ResistanceQuadraticNewtonsPerMpsSquared * speed * speed +
        section.AdditionalResistanceNewtons +
        TrainRunPhysics.ForceFromCoefficient(section.GradientPermille, train.MassKg);

    private static double TractionAcceleration(double speed, TrackSection section, ValidatedInput input) =>
        TrainRunPhysics.Acceleration(
            TrainRunPhysics.TractiveEffort(speed, input.Curve),
            Resistance(speed, section, input.Train), input.InertialMass);

    private static double BrakeDeceleration(double speed, TrackSection section, ValidatedInput input) =>
        (input.Train.MaxBrakeForceNewtons + Resistance(speed, section, input.Train)) / input.InertialMass;

    private static double SpeedTolerance(double speed) => 1e-8 * Math.Max(1, speed);

    private static void EnsureFiniteTime(double time)
    {
        if (!double.IsFinite(time))
            throw new InfeasibleProfileException("Travel or dwell time exceeds the numerical range.");
    }
}
