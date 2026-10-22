// Portions adapted from railtoolkit/TrainRuns.jl v1.0.4 (ISC).
// Copyright 2022 Max Kannenberg, Martin Scheidt
// Source: 529cdd1d2a158077440ce4e3093a8b875a1191fb/src/{calc,formulary}.jl
// See THIRD-PARTY-NOTICES.txt for provenance, modifications and the full ISC license.
namespace SwitchYard.SpeedProfile;

internal static class TrainRunPhysics
{
    internal const double Gravity = 9.80665;

    // Port of forceFromCoefficient and acceleration (formulary.jl).
    internal static double ForceFromCoefficient(double perMille, double massKg) =>
        perMille / 1000 * massKg * Gravity;

    internal static double Acceleration(double effort, double resistance, double inertialMass) =>
        (effort - resistance) / inertialMass;

    // Port of calculateTractiveEffort (calc.jl); binary search replaces its linear scan.
    // Preserve linear interpolation and the constant last-force extrapolation.
    internal static double TractiveEffort(double speed, TractionPoint[] curve)
    {
        if (speed <= curve[0].SpeedMps) return curve[0].ForceNewtons;
        if (speed >= curve[^1].SpeedMps) return curve[^1].ForceNewtons;
        int low = 0, high = curve.Length - 1;
        while (high - low > 1)
        {
            int middle = low + (high - low) / 2;
            if (curve[middle].SpeedMps <= speed) low = middle;
            else high = middle;
        }
        var fraction = (speed - curve[low].SpeedMps) / (curve[high].SpeedMps - curve[low].SpeedMps);
        return curve[low].ForceNewtons + fraction * (curve[high].ForceNewtons - curve[low].ForceNewtons);
    }

    // Port of the spatial constant-acceleration step; callers handle infeasible negative radicands.
    internal static double NextSpeedSquared(double speed, double acceleration, double distance) =>
        speed * speed + 2 * acceleration * distance;

    // Algebraically equivalent to the upstream time step, stable also when acceleration is zero.
    internal static double TravelTime(double distance, double initialSpeed, double finalSpeed) =>
        2 * distance / (initialSpeed + finalSpeed);
}
