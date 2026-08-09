using System.Text.Json;
using SwitchYard.Capacity;

namespace SwitchYard.CapacityAgent;

internal interface ICapacityModel
{
    CapacityModelDescriptor Descriptor { get; }

    Task<JsonElement> SolveAsync(
        JsonElement input,
        CapacityModelExecutionContext executionContext,
        Func<int, string, Task> reportProgress,
        CancellationToken cancellationToken);
}

internal sealed record CapacityModelExecutionContext(
    string JobId,
    CapacityTaskResourceLimits Resources);

internal sealed class CapacityModelRegistry
{
    private readonly IReadOnlyDictionary<string, ICapacityModel> _models;

    public CapacityModelRegistry(IEnumerable<ICapacityModel> models)
    {
        _models = models.ToDictionary(model => model.Descriptor.Id, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<CapacityModelDescriptor> Descriptors =>
        _models.Values.Select(model => model.Descriptor).ToList();

    public bool TryGet(string modelId, out ICapacityModel model) =>
        _models.TryGetValue(modelId, out model!);
}
