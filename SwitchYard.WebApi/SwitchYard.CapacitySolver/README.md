# SwitchYard CapacitySolver

此 .NET 10 类库集中管理求解模型、模型注册、OR-Tools 集成和 Worker 求解入口。它使用 [共享领域模型](../SwitchYard.Capacity/Domain)，由 [CapacityAgent](../SwitchYard.CapacityAgent/README.md) 在隔离 Worker 子进程中调用；认证、Service 通信、调度和资源监控仍由 Agent 负责。类库随 Agent 一起发布，无需单独启动。

This .NET 10 class library contains solver models, model registration, OR-Tools integration, and the Worker execution entry point. [CapacityAgent](../SwitchYard.CapacityAgent/README.md) invokes it in isolated Worker subprocesses using the [shared domain model](../SwitchYard.Capacity/Domain). The Agent owns authentication, Service communication, scheduling, and resource monitoring. The library ships with the Agent and does not start separately.

业务回归在仓库根目录执行 / Run business regression tests from the repository root:

```powershell
dotnet run --project SwitchYard.WebApi/SwitchYard.CapacitySolver.Tests
```

[安装与验收](../../docs/zh-CN/07-installation.md) · [Installation and validation](../../docs/en/07-installation.md) · [通过能力分析](../../docs/zh-CN/04-capacity.md) · [Capacity analysis](../../docs/en/04-capacity.md)
