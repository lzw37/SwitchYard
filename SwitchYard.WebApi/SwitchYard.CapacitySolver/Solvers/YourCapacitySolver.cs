using SwitchYard.Capacity;
using System;
using System.Collections.Generic;
using System.Text;

namespace SwitchYard.CapacitySolver
{
    /// <summary>
    /// 用户自定义的能力计算求解器
    /// </summary>
    public class YourCapacitySolver : ICapacitySolver
    {
        public CapacityModelDescriptor Descriptor => throw new NotImplementedException();

        public Task<StationCapacitySolveResult> SolveAsync(CapacityProblem problem, CapacitySolverExecutionContext executionContext, Func<int, string, Task> reportProgress, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
