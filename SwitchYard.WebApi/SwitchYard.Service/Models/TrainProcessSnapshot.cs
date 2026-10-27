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
    public Dictionary<string, string> SelectedRouteIDs { get; set; } = new(StringComparer.Ordinal);
    /// <summary>Execution-owned timing constraints. Null identifies snapshots written before this field existed.</summary>
    public List<TrainProcessPrecedence>? Precedences { get; set; }
}

public sealed class TrainProcessPrecedence
{
    public string Id { get; set; } = "";
    public string LeadingEvent { get; set; } = "";
    public string FollowingEvent { get; set; } = "";
    /// <summary>Lower bound in seconds; also the upper bound when IsExact is true.</summary>
    public double MinIntervalSeconds { get; set; }
    public bool IsExact { get; set; }
}

public sealed class GenerateTrainOperationPlanFromProcessRequest
{
    public string? CalculationParameterSetID { get; set; }

    public string? InstanceID { get; set; }
    public string? StationSchemeID { get; set; }
    public string? OperationPlanID { get; set; }
    public string? ProcessTemplateID { get; set; }
    public int? Revision { get; set; }
    public int TrainCount { get; set; }
    public List<ProcessPlanGenerationItem>? Processes { get; set; }
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
}

public sealed class ProcessPlanGenerationItem
{
    public string? CalculationParameterSetID { get; set; }

    public string? ProcessTemplateID { get; set; }
    public int? Revision { get; set; }
    public int TrainCount { get; set; }
}
