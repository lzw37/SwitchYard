namespace SwitchYard.Service.Models;

public sealed record TrainCharacteristicsImport(
    IReadOnlyList<SpeedProfileForceInput> TractionCurve,
    IReadOnlyList<SpeedProfileForceInput> BrakingCurve,
    IReadOnlyList<SpeedProfileForceInput> ResistanceCurve);
