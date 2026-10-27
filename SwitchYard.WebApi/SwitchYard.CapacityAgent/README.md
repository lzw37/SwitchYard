# SwitchYard CapacityAgent

独立的 .NET 计算代理通过 SignalR 连接 SwitchYard API，负责认证、任务调度、进度与结果回传，以及任务取消和本进程资源监控。求解实现位于 [SwitchYard.CapacitySolver](../SwitchYard.CapacitySolver/README.md)，由 Agent 直接调用。运行配置、模型版本和使用限制统一在 `docs/` 中维护。

This independent .NET agent connects to the API over SignalR and handles authentication, job scheduling, progress and result delivery, job cancellation, and process resource monitoring. The Agent directly invokes the implementation in [SwitchYard.CapacitySolver](../SwitchYard.CapacitySolver/README.md). Runtime configuration, model versions, and limitations are documented centrally.

共享领域模型位于 [SwitchYard.Capacity/Domain](../SwitchYard.Capacity/Domain)，对象组装集中在该共享项目的 `SwitchYard.Capacity.CapacityProblem.Build(input)`。Agent 收到网络输入后调用它，将完整的 `CapacityProblem` 对象直接传给 Solver，并接收 `StationCapacitySolveResult` 对象。JSON 仅用于 Service 网络通信；求解数据不通过文件或子进程传递。Agent 引用 Solver 类库，发布时自动携带求解库和 OR-Tools 运行依赖；所有任务在 Agent 进程内求解，各自持有独立的领域对象图和 OR-Tools 求解器。

The shared domain model lives in [SwitchYard.Capacity/Domain](../SwitchYard.Capacity/Domain), which owns object assembly through `SwitchYard.Capacity.CapacityProblem.Build(input)`. The Agent calls that builder after receiving network input, passes the assembled `CapacityProblem` directly to the Solver, and receives a `StationCapacitySolveResult`. JSON is used only for Service network communication; solver input and results stay in memory. The Agent references the Solver class library, so publishing includes the solver and OR-Tools runtime dependencies. Jobs run in the Agent process, each with its own domain object graph and OR-Tools solver.

通信 DTO 和 JSON 转换器集中在 [SwitchYard.Capacity/Contracts](../SwitchYard.Capacity/Contracts)，数据库行映射类型位于 [SwitchYard.Capacity/Database](../SwitchYard.Capacity/Database)。Service 将数据库行转换为传输 DTO，再由领域层装配对象图。

Communication DTOs and JSON converters live in [SwitchYard.Capacity/Contracts](../SwitchYard.Capacity/Contracts), with database row mappings in [SwitchYard.Capacity/Database](../SwitchYard.Capacity/Database). The Service maps database rows to transport DTOs before the domain layer assembles the object graph.

代码分为四个文件。阅读求解流程时从 `CapacityAgentClient.ProcessJobAsync` 开始：反序列化网络输入 → `CapacityProblem.Build(input)` 组装对象 → `solver.SolveAsync(problem, ...)` 直接求解 → 回传结果。取消令牌直接传给模型；Agent 退出前等待所有模型完成取消清理。

The Agent has four source files. `CapacityAgentClient.ProcessJobAsync` deserializes network input, assembles it through `CapacityProblem.Build(input)`, calls `solver.SolveAsync(problem, ...)` directly, and delivers the result. Cancellation tokens go directly to the model; Agent shutdown waits for active models to finish cancellation and cleanup.

| 文件 / File | 职责 / Responsibility |
| --- | --- |
| `Program.cs` | 启动、登录输入及退出信号 / Startup, login input, and shutdown |
| `CapacityAgentOptions.cs` | 配置读取与校验 / Configuration and validation |
| `CapacityAgentClient.cs` | 连接循环、对象组装调用、直接求解及结果回传 / Connection loop, domain assembly, direct solving, and result delivery |
| `CapacityAgentResources.cs` | CPU、内存与任务配额采样 / CPU, memory, and allocation sampling |

内存配置传给 SCIP 作为求解器内部限制，不再执行独立进程工作集限制；CBC 后端没有同等的单任务内存硬限制。

The memory allowance is passed to SCIP as its internal limit. There is no separate process working-set limit, and CBC has no equivalent per-job hard memory limit.

| 内容 / Topic | 中文 | English |
| --- | --- | --- |
| 安装与启动 / Installation and startup | [安装与代理配置](../../docs/zh-CN/07-installation.md) | [Installation and agent setup](../../docs/en/07-installation.md) |
| 求解与结果 / Solving and results | [通过能力分析](../../docs/zh-CN/04-capacity.md) | [Capacity analysis](../../docs/en/04-capacity.md) |
| 接口与任务 / APIs and jobs | [接口参考](../../docs/zh-CN/10-api-data-reference.md) | [API reference](../../docs/en/10-api-data-reference.md) |

[文档中心 / Documentation](../../docs/README.md) · [历史代理说明 / Previous agent guide](../../docs/archive/development/capacity-agent.md)
