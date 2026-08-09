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

## 并发与资源配置

程序从输出目录的 `capacity-agent.json` 读取并发和每任务资源上限：

```json
{
  "agentName": "",
  "maxConcurrentJobs": 2,
  "cpuCoresPerJob": 2,
  "memoryLimitMbPerJob": 2048,
  "statusReportIntervalSeconds": 2,
  "allowAllUsers": false,
  "allowedUsers": []
}
```

- `maxConcurrentJobs`：Agent 同时接受的最大求解任务数；
- `cpuCoresPerJob`：每个任务允许使用的 OR-Tools 线程上限；
- `memoryLimitMbPerJob`：每个任务的 SCIP 内存上限，Worker 工作集超过该值时主 Agent 也会终止该 Worker；
- `statusReportIntervalSeconds`：向 SwitchYard.Service 上报 CPU、内存、活动任务和可用槽位的周期；
- `agentName`：可选的 Agent 显示名称，留空时使用当前操作系统用户名。
- `allowAllUsers`：管理员启动 Agent 时，是否允许所有已登录用户使用；
- `allowedUsers`：管理员启动 Agent 时允许使用该 Agent 的用户名白名单。

访问权限由 SwitchYard.Service 根据 Agent 登录账号的角色强制执行。普通用户启动的 Agent 永远只能由
该用户本人使用，`allowAllUsers` 和 `allowedUsers` 会被忽略。管理员启动的 Agent 可以开放给所有用户，
或者只开放给所有者和 `allowedUsers` 中的用户；当两个字段都未设置时，默认仍然只有管理员本人可用。

可以使用 `--config <文件路径>` 加载其他配置。每个任务由独立 Worker 进程求解，因此多个任务可以真正
并行运行，且 OR-Tools 原生日志、取消和内存限制互不干扰。Service 会根据 Agent 上报的可用槽位分派任务，
“模型求解”页面每 2 秒刷新 Agent 的并发任务数、CPU、内存、每任务资源上限和可用性。

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

## 日志

CapacityAgent 使用 Serilog 同时输出控制台日志和本地文件日志。日志保存在程序输出目录的 `logs`
子目录，文件名为 `capacity-agent-yyyyMMdd.log`，按日滚动并保留最近 30 个文件；单个文件达到
50 MB 时会在当天继续分卷。求解期间会开启并捕获 OR-Tools/SCIP 的原生求解输出，以
`SourceContext=OR-Tools` 逐行写入同一个 Serilog 日志文件。日志记录服务器地址、用户名、Agent、
任务和求解摘要，但不会记录密码、访问令牌或完整求解输入。

## 自检

以下命令并发运行最多两个包含两列车、单进路和共享轨道电路的最小求解案例：

```powershell
dotnet run --project SwitchYard.CapacityAgent -- --self-test
```
