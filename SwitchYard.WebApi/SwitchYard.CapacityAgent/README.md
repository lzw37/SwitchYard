# SwitchYard CapacityAgent

独立的 .NET 计算代理通过 SignalR 连接 SwitchYard API，负责认证、任务调度、进度与结果回传，以及 Worker 子进程的取消和资源监控。求解实现位于 [SwitchYard.CapacitySolver](../SwitchYard.CapacitySolver/README.md)，由 Worker 调用。运行配置、模型版本和使用限制统一在 `docs/` 中维护。

This independent .NET agent connects to the API over SignalR and handles authentication, job scheduling, progress and result delivery, Worker cancellation, and resource monitoring. Workers invoke the implementation in [SwitchYard.CapacitySolver](../SwitchYard.CapacitySolver/README.md). Runtime configuration, model versions, and limitations are documented centrally.

共享领域模型位于 [SwitchYard.Capacity/Domain](../SwitchYard.Capacity/Domain)，由 Solver 通过 `SwitchYard.Capacity.CapacityProblem.Build(input)` 装配。Agent 引用 Solver 类库，发布时自动携带求解库和 OR-Tools 运行依赖；每个任务仍在独立 Worker 进程中求解。

The shared domain model lives in [SwitchYard.Capacity/Domain](../SwitchYard.Capacity/Domain), where the Solver builds it through `SwitchYard.Capacity.CapacityProblem.Build(input)`. The Agent references the Solver class library, so publishing includes the solver and OR-Tools runtime dependencies. Each job still solves in its own Worker process.

通信 DTO 和 JSON 转换器集中在 [SwitchYard.Capacity/Contracts](../SwitchYard.Capacity/Contracts)，数据库行映射类型位于 [SwitchYard.Capacity/Database](../SwitchYard.Capacity/Database)。Service 将数据库行转换为传输 DTO，再由领域层装配对象图。

Communication DTOs and JSON converters live in [SwitchYard.Capacity/Contracts](../SwitchYard.Capacity/Contracts), with database row mappings in [SwitchYard.Capacity/Database](../SwitchYard.Capacity/Database). The Service maps database rows to transport DTOs before the domain layer assembles the object graph.

| 内容 / Topic | 中文 | English |
| --- | --- | --- |
| 安装与启动 / Installation and startup | [安装与代理配置](../../docs/zh-CN/07-installation.md) | [Installation and agent setup](../../docs/en/07-installation.md) |
| 求解与结果 / Solving and results | [通过能力分析](../../docs/zh-CN/04-capacity.md) | [Capacity analysis](../../docs/en/04-capacity.md) |
| 接口与任务 / APIs and jobs | [接口参考](../../docs/zh-CN/10-api-data-reference.md) | [API reference](../../docs/en/10-api-data-reference.md) |

[文档中心 / Documentation](../../docs/README.md) · [历史代理说明 / Previous agent guide](../../docs/archive/development/capacity-agent.md)
