# SwitchYard CapacitySolver

此 .NET 10 类库集中管理能力求解器、求解器注册和 OR-Tools 集成。它接收 [共享领域模型](../SwitchYard.Capacity/Domain) 组装完成的 `CapacityProblem` 对象，返回 `StationCapacitySolveResult` 对象，由 [CapacityAgent](../SwitchYard.CapacityAgent/README.md) 在同一进程内直接调用；认证、Service 通信、JSON 转换、调度和资源监控由 Agent 负责。类库随 Agent 一起发布，无需单独启动。

This .NET 10 class library contains capacity solvers, solver registration, and OR-Tools integration. It accepts an assembled `CapacityProblem` and returns a `StationCapacitySolveResult`. [CapacityAgent](../SwitchYard.CapacityAgent/README.md) invokes it directly in its own process using the [shared domain model](../SwitchYard.Capacity/Domain). The Agent owns authentication, Service communication, JSON conversion, scheduling, and resource monitoring. The library ships with the Agent and does not start separately.

| 文件 / File | 职责 / Responsibility |
| --- | --- |
| `CapacitySolver.cs` | `ICapacitySolver` 求解器接口及 `CapacitySolverExecutionContext` 执行上下文 / Solver contract and execution context |
| `CapacitySolverRegistry.cs` | 默认注册 `MIPCapacitySolver`，提供求解器查找及描述列表 / Default registration, lookup, and descriptions |
| `MIPCapacitySolver.cs` | 混合整数规划能力求解器实现 / Mixed-integer programming capacity solver |

Agent 使用 `new CapacitySolverRegistry()` 创建注册表，通过 `TryGet` 取得求解器，再调用 `solver.SolveAsync(problem, ...)`。领域对象组装始终由 `SwitchYard.Capacity` 负责。

The Agent creates `new CapacitySolverRegistry()`, resolves a solver through `TryGet`, and calls `solver.SolveAsync(problem, ...)`. Domain assembly remains in `SwitchYard.Capacity`.

业务回归在仓库根目录执行 / Run business regression tests from the repository root:

```powershell
dotnet run --project SwitchYard.WebApi/SwitchYard.CapacitySolver.Tests
```

[安装与验收](../../docs/zh-CN/07-installation.md) · [Installation and validation](../../docs/en/07-installation.md) · [通过能力分析](../../docs/zh-CN/04-capacity.md) · [Capacity analysis](../../docs/en/04-capacity.md)
