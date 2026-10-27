using System.Text.Json;
using SwitchYard.Capacity;
using SwitchYard.CapacitySolver;

namespace SwitchYard.CapacityAgent;

/// <summary>Dispatches a solver advertised by the solver library to a monitored worker.</summary>
internal sealed class WorkerCapacityModel(CapacityModelDescriptor descriptor) : ICapacityModel
{
    public CapacityModelDescriptor Descriptor { get; } = descriptor;

    public Task<JsonElement> SolveAsync(
        JsonElement input,
        CapacityModelExecutionContext executionContext,
        Func<int, string, Task> reportProgress,
        CancellationToken cancellationToken) =>
        CapacitySolveWorkerProcess.SolveAsync(Descriptor.Id, input, executionContext, reportProgress, cancellationToken);
}
