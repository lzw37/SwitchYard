namespace SwitchYard.Capacity;

public sealed class StationPlanViewSettings
{
    public bool IsConfigured { get; set; }
    public List<string> CellIDs { get; set; } = new();
    public List<string> EndpointNodeIDs { get; set; } = new();
}

public sealed class StationPlanViewSettingsRequest
{
    public string? InstanceID { get; set; }
    public string? StationSchemeID { get; set; }
    public string? OperationPlanID { get; set; }
    public List<string>? CellIDs { get; set; }
    public List<string>? EndpointNodeIDs { get; set; }
}
