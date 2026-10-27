using System.Text.Json;
using SwitchYard.Capacity;

namespace SwitchYard.CapacitySolver;

public sealed class CapacitySolveWorkerRequest
{
    public string JobId { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public JsonElement Input { get; set; }
    public CapacityTaskResourceLimits Resources { get; set; } = new();
}

public sealed class CapacitySolveWorkerResponse
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public JsonElement? Result { get; set; }
}
