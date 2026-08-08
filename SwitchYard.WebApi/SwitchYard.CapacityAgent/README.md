# SwitchYard.CapacityAgent

`SwitchYard.CapacityAgent` 是独立运行的能力计算控制台程序。它使用现有 SwitchYard 用户账号向
`SwitchYard.Service` 申请专用 Agent 令牌，通过 SignalR 注册并保持长连接，随后接收模型求解任务并回传 JSON 结果。

## 运行

```powershell
dotnet run --project SwitchYard.CapacityAgent
```

按提示输入：

1. SwitchYard Web 服务器根 URL，例如 `https://localhost:7297`；
2. 用户名；
3. 密码（控制台不回显明文）。

账号必须存在、已激活，且不处于“首次登录必须修改密码”状态。程序会把用户输入的原始密码进行与 Web
前端一致的 SHA-256 处理后再发送，并只在内存中保留凭据以便令牌到期后自动续接。

## 模型

当前注册模型为 `station-capacity-v1`。其数学模型与 `PyStationCapacity` 保持同一结构：

- 作业开始/结束时间与持续时间约束；
- 同一列车相邻作业的时间连续性；
- 每个作业唯一选择一条候选进路；
- 相邻作业所选进路的轨道电路首尾连通；
- 进路选中后计算各轨道电路的开始/结束占用时间；
- 不同列车在同一轨道电路上的占用互斥；
- 支持最小结束时刻和、最小持续时间和、最小开始与结束时刻和三种目标。

实现使用 Google OR-Tools 的 SCIP 混合整数规划求解器。模型输入由 Web 前端“模型求解”页根据当前实例、
站场方案和作业计划生成，发送前可直接检查和编辑 JSON。结果保存在服务进程内存中，不写回数据库，
可由浏览器下载为 JSON 文件。

## 自检

以下命令运行一个包含两列车、单进路和共享轨道电路的最小求解案例：

```powershell
dotnet run --project SwitchYard.CapacityAgent -- --self-test
```
