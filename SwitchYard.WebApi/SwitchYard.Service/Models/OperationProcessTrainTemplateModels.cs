using SwitchYard.Capacity;

namespace SwitchYard.Service.Models;

public sealed class GenerateTrainTemplateFromProcessRequest
{
    public string? InstanceID { get; set; }
    public string? StationSchemeID { get; set; }
    public string? OperationPlanID { get; set; }
    public string? ProcessTemplateID { get; set; }
    public int? Revision { get; set; }
}

public sealed class GenerateTrainTemplateFromProcessResponse
{
    public TrainTemplateRow TrainTemplate { get; set; } = new();
    public int MovementCount { get; set; }
    public List<string> Warnings { get; set; } = new();
}
