using SwitchYard.Capacity;

namespace SwitchYard.CapacitySolver;

/// <summary>能力求解器注册表，默认注册 MIP 求解器，并提供描述信息及求解器实例查找。</summary>
public sealed class CapacitySolverRegistry
{
    /// <summary>按不区分大小写的协议标识索引求解器实例，供描述枚举和实例查找使用。</summary>
    private readonly IReadOnlyDictionary<string, ICapacitySolver> _solvers;

    /// <summary>创建默认注册表，包含基于混合整数规划的能力求解器。</summary>
    public CapacitySolverRegistry() : this([new MIPCapacitySolver()])
    {
    }

    /// <summary>注册给定的求解器集合，重复协议标识会导致构造失败。</summary>
    /// <param name="solvers">需要注册的求解器实例集合。</param>
    public CapacitySolverRegistry(IEnumerable<ICapacitySolver> solvers)
    {
        _solvers = solvers.ToDictionary(solver => solver.Descriptor.Id, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>所有已注册求解器的描述列表，每次访问生成新列表，供代理展示及向服务端注册。</summary>
    public IReadOnlyList<CapacityModelDescriptor> Descriptors =>
        _solvers.Values.Select(solver => solver.Descriptor).ToList();

    /// <summary>按不区分大小写的协议标识查找已注册求解器。</summary>
    /// <param name="solverId">要查找的求解器协议标识。</param>
    /// <param name="solver">查找成功时返回对应的求解器实例。</param>
    /// <returns>找到求解器时返回 true，否则返回 false。</returns>
    public bool TryGet(string solverId, out ICapacitySolver solver) =>
        _solvers.TryGetValue(solverId, out solver!);
}
