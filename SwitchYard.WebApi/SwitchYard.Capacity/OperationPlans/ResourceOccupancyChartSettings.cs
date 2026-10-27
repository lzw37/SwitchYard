namespace SwitchYard.Capacity;

public sealed class ResourceOccupancyChartSettings
{
    public bool IsConfigured { get; set; }
    public List<ResourceOccupancyChart> Charts { get; set; } = new();
}

public sealed class ResourceOccupancyChart
{
    public string ChartID { get; set; } = "";
    public string ChartName { get; set; } = "";
    public List<string> CellIDs { get; set; } = new();
}

public sealed class ResourceOccupancyChartSettingsRequest
{
    public string? InstanceID { get; set; }
    public string? StationSchemeID { get; set; }
    public string? OperationPlanID { get; set; }
    public List<ResourceOccupancyChart>? Charts { get; set; }
}
