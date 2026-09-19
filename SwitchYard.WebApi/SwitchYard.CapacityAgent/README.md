# SwitchYard.CapacityAgent

`SwitchYard.CapacityAgent` 是独立运行的能力计算控制台程序。它使用现有 SwitchYard 用户账号向
`SwitchYard.Service` 申请专用 Agent 令牌，通过 SignalR 注册并保持长连接，随后接收模型求解任务并回传 JSON 结果。

## 运行

项目目标框架为 **.NET 10 (`net10.0`)**，从源码构建和运行需要 .NET 10 SDK。Web 服务仍以 .NET 8 为目标；同机运行时还需具备对应的 ASP.NET Core 8 运行时。完整环境说明见[项目 README](../../README.md)。

在仓库根目录运行：

```powershell
dotnet run --project SwitchYard.WebApi/SwitchYard.CapacityAgent
```

按提示输入：

1. SwitchYard Web 服务器根 URL，本地开发默认为 `http://localhost:7297`；
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

## 模型与过程约束

当前模型 ID 为 `station-capacity-v1`，注册的模型版本为 **2.0.0**；ID 中的 `v1` 不代表当前功能版本。实现使用 Google OR-Tools 9.15.6755 的 SCIP 混合整数规划求解器。

- 普通列车沿用顺序作业模型：持续时间、相邻作业时间连续、候选进路唯一选择及轨道电路首尾连通。
- 由作业过程生成的列车使用完整事件图：最小／最大时长、相对固定事件时刻、事件次序的最小间隔、共享事件及资源地点／锚约束；不会把并行活动强制改为顺序作业。
- 停留活动选择候选股道，求解内部映射到关联轨道电路，不向站场数据库新增虚构进路。
- 两种列车均计算轨道电路开始／结束占用时刻，并约束不同列车在同一区段的占用互斥。
- 支持最小结束时刻和、最小持续时间和、最小开始与结束时刻和三种目标，以及左右移动容差、求解时限和线程数设置。

服务端根据输入中的过程约束和 `minimumModelVersion` 检查模型兼容性；含过程约束的任务要求模型版本至少为 2.0.0。旧 Agent 不能接收这类任务，避免忽略过程约束。过程模板、快照与生成规则见[作业过程编排](../../docs/operation-process.md)。

## 在界面中求解与保存结果

在 Web 的「模型求解」页选择实例、站场方案和作业计划，点击「生成输入」。输入由服务端根据已保存的站场、占用参数和计划构建，提交前可检查、编辑 JSON。选择在线且有空闲槽位的 Agent 和模型后提交，页面显示进度与结果，并支持下载 JSON。

任务完成不等于找到可行解，应检查结果的 `hasSolution` 和 `status`。当前任务状态与原始结果 JSON 保存在 `SwitchYard.Service` 进程内存中，服务重启后不会恢复。

保存行为取决于入口：

| 入口 | 保存内容 |
| --- | --- |
| 模型求解 → 求解 | 保留内存任务与结果 JSON，可下载；不自动写回作业计划 |
| 作业计划 → 生成饱和计划 | 使用当前用户的求解预设，校验可行解后以事务另存为新的作业计划，源计划保持不变 |

求解预设持久保存 Agent、模型及求解设置。生成饱和计划时，服务端保存新计划的列车、求解作业时刻与资源，并保留过程列车的约束快照、完整事件时刻和停留股道选择；保存失败则回滚。页面成功后切换到新计划，可继续查看计划图、占用表、瓶颈分析或仿真。它优化已有列车的作业，不自动增加列车数搜索最大开行量。

这里的计划与过程快照属于数据库数据，与内存中的任务记录、可下载结果 JSON 不同；不能用“所有求解结果均不持久化”概括。分析页面另外保存的分析快照也不属于 Agent 任务存储。

## 日志

CapacityAgent 使用 Serilog 同时输出控制台日志和本地文件日志。日志保存在程序输出目录的 `logs`
子目录，文件名为 `capacity-agent-yyyyMMdd.log`，按日滚动并保留最近 30 个文件；单个文件达到
50 MB 时会在当天继续分卷。求解期间会开启并捕获 OR-Tools/SCIP 的原生求解输出，以
`SourceContext=OR-Tools` 逐行写入同一个 Serilog 日志文件。日志记录服务器地址、用户名、Agent、
任务和求解摘要，但不会记录密码、访问令牌或完整求解输入。

## 自检

在仓库根目录执行以下命令，并发运行最多两个包含两列车、单进路和共享轨道电路的最小求解案例：

```powershell
dotnet run --project SwitchYard.WebApi/SwitchYard.CapacityAgent -- --self-test
```

自检无需登录 Web 服务，使用内存输入和独立 Worker，不写业务数据库；仍会生成程序日志。此命令检查基本求解与并发运行，完整作业过程约束的接口回归见[过程测试说明](../SwitchYard.OperationProcess.Tests/README.md)。
