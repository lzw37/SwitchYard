namespace SwitchYard.Service.Models;

/// <summary>Legacy generators are materialized once; supplied points always remain authoritative.</summary>
public static class TrainCharacteristicTables
{
    public static IReadOnlyList<SpeedProfileForceInput> Generate(SpeedProfileTrainInput train, bool braking)
    {
        if (!double.IsFinite(train.MaxSpeedKmh) || train.MaxSpeedKmh <= 0 || train.MaxSpeedKmh > 1000) throw new ArgumentException("Invalid train maximum speed.");
        var speeds = Enumerable.Range(0, (int)Math.Ceiling(train.MaxSpeedKmh / 10)).Select(i => i * 10d).Append(train.MaxSpeedKmh);
        return speeds.Select(v => new SpeedProfileForceInput { SpeedKmh = v, ForceKn = braking ? train.MaxBrakeForceKn : train.ResistanceA + train.ResistanceB * v + train.ResistanceC * v * v }).ToArray();
    }
    public static SpeedProfileTrainInput Normalize(SpeedProfileTrainInput train) => train with {
        BrakingCurve = train.BrakingCurve ?? Generate(train, true),
        ResistanceCurve = train.ResistanceCurve ?? Generate(train, false),
    };
}
