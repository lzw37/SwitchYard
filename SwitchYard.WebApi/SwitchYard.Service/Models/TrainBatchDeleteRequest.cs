namespace SwitchYard.Service.Models;

public sealed class TrainBatchDeleteRequest
{
    public string? InstanceID { get; set; }
    public string? StationSchemeID { get; set; }
    public string? OperationPlanID { get; set; }
    public List<string>? TrainIDs { get; set; }
}
