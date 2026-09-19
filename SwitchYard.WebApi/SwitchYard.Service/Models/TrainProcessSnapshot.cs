namespace SwitchYard.Service.Models;

/// <summary>A generated train owns an independent copy of its complete process constraints.</summary>
public sealed class TrainProcessSnapshot : ProcessScope
{
    public string TrainID { get; set; } = "";
    public string SourceTemplateID { get; set; } = "";
    public int SourceRevision { get; set; }
    public string SourceName { get; set; } = "";
    public double OriginSeconds { get; set; }
    public OperationProcessTemplate Process { get; set; } = new();
    public Dictionary<string, string> ActivityMovementMap { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, double> EventTimes { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> SelectedTrackIDs { get; set; } = new(StringComparer.Ordinal);
}

public sealed class GenerateTrainOperationPlanFromProcessRequest
{
    public string? InstanceID { get; set; }
    public string? StationSchemeID { get; set; }
    public string? OperationPlanID { get; set; }
    public string? ProcessTemplateID { get; set; }
    public int? Revision { get; set; }
    public int TrainCount { get; set; }
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
}
