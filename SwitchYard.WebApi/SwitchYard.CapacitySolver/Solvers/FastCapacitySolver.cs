using SwitchYard.Capacity;
using System;
using System.Collections.Generic;
using System.Text;

namespace SwitchYard.CapacitySolver
{
    /// <summary>
    /// 快速启发式求解器
    /// </summary>
    public class FastCapacitySolver : ICapacitySolver
    {
        public CapacityModelDescriptor Descriptor => throw new NotImplementedException();

        public Task<StationCapacitySolveResult> SolveAsync(CapacityProblem problem, CapacitySolverExecutionContext executionContext, Func<int, string, Task> reportProgress, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
