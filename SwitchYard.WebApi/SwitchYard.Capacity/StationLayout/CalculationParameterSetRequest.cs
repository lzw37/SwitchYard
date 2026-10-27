namespace SwitchYard.Capacity;

public sealed class CalculationParameterSetRequest
{
    public string InstanceID { get; set; } = "";
    public string StationSchemeID { get; set; } = "";
    public string? ID { get; set; }
    public string Name { get; set; } = "";
    public bool IsDefault { get; set; }
    public string? CopyFromID { get; set; }
}
