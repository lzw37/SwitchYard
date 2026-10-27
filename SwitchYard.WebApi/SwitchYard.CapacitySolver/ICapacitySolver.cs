using SwitchYard.Capacity;

namespace SwitchYard.CapacitySolver;

/// <summary>
/// 能力求解器的统一接口，提供求解器描述、异步求解、进度报告和取消支持。
/// </summary>
public interface ICapacitySolver
{
    /// <summary>供代理注册及调用方识别求解器的描述，包含协议标识、名称、版本和功能说明。</summary>
    CapacityModelDescriptor Descriptor { get; }

    /// <summary>异步求解已由共享领域层组装的能力问题，并通过回调报告进度。</summary>
    /// <param name="problem">由 SwitchYard.Capacity 组装完成、已建立领域对象引用的求解问题。</param>
    /// <param name="executionContext">任务标识和本次求解可使用的资源配额。</param>
    /// <param name="reportProgress">接收进度百分比及中文进度说明的异步回调。</param>
    /// <param name="cancellationToken">用于请求取消求解的令牌。</param>
    /// <returns>包含求解状态、列车时刻及资源占用的强类型结果。</returns>
    Task<StationCapacitySolveResult> SolveAsync(
        CapacityProblem problem,
        CapacitySolverExecutionContext executionContext,
        Func<int, string, Task> reportProgress,
        CancellationToken cancellationToken);
}

/// <summary>单次求解器执行的上下文，通过位置参数提供任务标识与资源配额属性。</summary>
/// <param name="JobId">用于任务跟踪及进度关联的任务标识。</param>
/// <param name="Resources">本次任务可使用的 CPU 核数及内存字节数上限。</param>
public sealed record CapacitySolverExecutionContext(
    string JobId,
    CapacityTaskResourceLimits Resources);
