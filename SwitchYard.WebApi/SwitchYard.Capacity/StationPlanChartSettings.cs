using System.Text.Json.Serialization;

namespace SwitchYard.Capacity;

public sealed class StationPlanChartSettings
{
    public bool IsConfigured { get; set; }
    public List<StationPlanChart> Charts { get; set; } = new();
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public StationPlanViewSettings? LegacySettings { get; set; }
}

public sealed class StationPlanChart
{
    public string ChartID { get; set; } = "";
    public string ChartName { get; set; } = "";
    public List<string> EndpointNodeIDs { get; set; } = new();
}

public sealed class StationPlanChartSettingsRequest
{
    public string? InstanceID { get; set; }
    public string? StationSchemeID { get; set; }
    public string? OperationPlanID { get; set; }
    public List<StationPlanChart>? Charts { get; set; }
}
