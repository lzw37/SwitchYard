# 安装、配置与首次运行

[文档目录](README.md) · [English](../en/07-installation.md) · 核对日期：2026-10-11

本章面向开发人员和首次部署人员，说明当前代码需要的运行环境、配置、数据和 CapacityAgent。生产上线、升级、备份及排错见[部署与运维](08-deployment-operations.md)。旧版[开发与配置指南](../archive/guides/Getting-Started.md)及 [CapacityAgent README](../archive/development/capacity-agent.md)保留；涉及数据库数量、启动迁移和任务取消时，以本章对当前源码的说明为准。

## 1. 安装前先确认组成

| 组件 | 作用 | 是否必需 |
| --- | --- | --- |
| `switchyard-vue` | Vue 主界面 | Web 操作必需 |
| `SwitchYard.Service` | 登录、业务 API、课程文件、Agent 协调 | 必需 |
| `HumpDatabase` | 用户、Refresh Token、驼峰实例和计算数据 | API 启动必需 |
| `CapacityDatabase` | 能力实例、站场、作业计划、过程、分析及求解预设 | 能力模块必需 |
| `SketchLayoutDatabase` | 独立 SketchLayout 方案 | SketchLayout 模块必需 |
| `SwitchYard.CapacityAgent` | Service 通信、任务调度、Worker 取消和资源监控 | 模型求解和生成饱和计划需要 |
| `SwitchYard.CapacitySolver` | 求解模型、OR-Tools 和 Worker 求解入口的 .NET 10 类库 | 随 Agent 发布，由 Worker 调用 |
| 课程文件目录 | 本地视频、PDF、Word 资料 | 课程页面有内容时需要 |

当前完整部署有三组数据库配置。旧文档所述“两库”是较早状态。驼峰库和能力库中存在同名异构表，不能共用同一数据库或 SQLite 文件；SketchLayout 也按独立库配置、备份和恢复。

主前端通过本地依赖使用 `../SwitchYard.StationLayout/frontend`，API 也引用独立站场后端项目。克隆或复制工程时保留仓库目录结构，不要只复制前端或 Service 文件夹。

## 2. 环境和依赖

| 用途 | 当前工程要求 |
| --- | --- |
| 仓库构建 | .NET 10 SDK，覆盖 Agent 等 `net10.0` 项目 |
| API 运行 | ASP.NET Core 8 Runtime；Service 目标为 `net8.0` |
| Agent 运行 | .NET 10 Runtime；目标为 `net10.0` |
| 迁移工具运行 | .NET 8 Runtime；通过 `dotnet run` 时需要 SDK |
| 前端构建 | Node.js `^20.19.0 || >=22.12.0`，npm |
| 数据库 | SQLite 或 MySQL；各业务库可分别选择 |

安装 .NET 10 SDK 不应被当作已经安装 ASP.NET Core 8 Runtime。先检查：

```powershell
dotnet --list-sdks
dotnet --list-runtimes
node --version
npm.cmd --version
```

工程声明的关键依赖包括 Vue `^3.5.25`、Vite `^7.2.4`、TypeScript `~5.9.0`、API MySqlConnector `2.6.1`、Microsoft.Data.Sqlite `10.0.1`，以及 Solver 类库的 Google.OrTools `9.15.6755`。Agent 通过项目引用携带 Solver 及其运行依赖。这些是当前工程声明，不是对上游最新版本的推荐；前端实际安装版本由 `package-lock.json` 与 `npm ci` 决定。

Solver 使用 OR-Tools 的原生组件。应在目标操作系统及架构上运行第 7.2 节的测试，并用发布后的 Agent 执行代表性任务；不要把其他系统的构建输出直接当作已验证的可用包。

已跟踪的解决方案引用了缺失的 `SwitchYard.SketchLayout.Tests` 项目，其他本地测试项目也有 Git 忽略情况。完整构建解决方案可能在编译应用之前失败；安装请优先使用本章明确指定 Service / Agent 的单项目命令，不假定所有解决方案条目都存在。

## 3. 首次数据准备是安装的一部分

启动初始化会执行当前 schema 和部分升级逻辑，但没有导入演示实例、初始管理员或课程资料的种子数据步骤。

| 目标 | 需要的数据 |
| --- | --- |
| HTTP/Swagger 检查 | 正确配置、可连接数据库及完整 schema 初始化 |
| 登录 | 驼峰库中存在有效账号和正确的密码哈希，账号已激活 |
| 注册或管理员创建用户 | 驼峰模板实例 `001` 及其可复制的关联数据 |
| 能力分析 | 能力实例、站场、进路/占用参数、作业计划等业务数据 |
| 模型求解 | 合法输入、可访问的在线 Agent、匹配的模型版本和可用槽位 |

注册先创建用户，再复制模板 `001`；复制失败会尝试删除新用户以补偿。该补偿失败也会记录错误，因此不能将它描述成覆盖全部步骤的单个数据库事务。管理员创建用户也依赖模板复制。

普通角色的注册账号默认激活；申请 `Admin` 角色的账号默认未激活，须由已有管理员审批。当前仓库没有创建首个管理员的引导命令或可公开使用的默认账号密码。

首次完整业务安装应取得经授权、与当前版本兼容的开发数据副本或初始化数据包，核实模板及管理员状态后再开放注册。不要直接把明文密码写入 `passwordhash`，也不要把生产数据和凭据提交到仓库。

## 4. 本地 API：隔离配置示例

以下 PowerShell 命令从仓库根目录运行，使用专门的本地数据目录，覆盖三组数据库配置。先把经授权的 SQLite 数据放到对应路径；若用空文件路径，启动仅尝试初始化结构，不会自动满足上一节的业务前提。

```powershell
$repoDir = (Get-Location).Path
$localDataDir = Join-Path $repoDir 'LocalData'
New-Item -ItemType Directory -Force -Path $localDataDir | Out-Null

$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:WebApi__Hosts__0 = 'http://0.0.0.0:7297'
$env:WebApi__Hosts__1 = 'http://localhost:5102'

$env:HumpDatabase__DatabaseType = 'SQLite'
$env:HumpDatabase__SqlliteConfig__DatabaseFile = Join-Path $localDataDir 'hump.dev.db'
$env:CapacityDatabase__DatabaseType = 'SQLite'
$env:CapacityDatabase__SqlliteConfig__DatabaseFile = Join-Path $localDataDir 'capacity.dev.db'
$env:SketchLayoutDatabase__DatabaseType = 'SQLite'
$env:SketchLayoutDatabase__SqlliteConfig__DatabaseFile = Join-Path $localDataDir 'sketchlayout.dev.db'

$jwtBytes = New-Object byte[] 64
$jwtRng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$jwtRng.GetBytes($jwtBytes)
$jwtRng.Dispose()
$env:Jwt__SecretKey = [Convert]::ToBase64String($jwtBytes)

dotnet run --project SwitchYard.WebApi/SwitchYard.Service/SwitchYard.Service.csproj --no-launch-profile
```

这些环境变量仅影响当前终端和它启动的子进程。示例不输出随机签名密钥；每次更换密钥会使既有访问令牌失效。需要跨重启保留登录时，使用自己的私有配置保存并重用密钥。

`SqlliteConfig` 是代码中的实际键名，拼写不能自行改成 `SqliteConfig`。数据库类型接受精确形式 `SQLite` / `Sqllite`、`MySQL` / `Mysql`；连接器的选择并非任意大小写都兼容。

`0.0.0.0` 是监听地址，本机浏览器使用 `localhost`，其他设备使用运行 API 的电脑局域网地址。只需本机访问时，可把第 0 项设为 `http://127.0.0.1:7297`。

### 4.1 核对启动结果

初始化成功后，检查启动日志中的 `Database schema initialized` 和 `Server bound addresses`。随后在另一终端执行：

```powershell
Invoke-RestMethod 'http://localhost:7297/api/System/version'
```

开发环境 Swagger 地址为 `http://localhost:7297/swagger`。生产环境不启用 Swagger。版本接口只返回版本，不检查数据库内容、模板、课程或 Agent，不能作为完整就绪检测。

完成启动检查后，还应执行本章末尾的验收，确认账号、业务数据和所需模块可用。

### 4.2 启动到底修改什么

[`DatabaseSchemaInitializer`](../../SwitchYard.WebApi/SwitchYard.Service/Services/DatabaseSchemaInitializer.cs)在开始监听前执行以下流程：

1. 为驼峰库执行 `sqlite-schema.sql` 或 `mysql-schema.sql`。
2. 若 SketchLayout 配置有效，执行对应独立 schema。
3. 若能力库配置有效，执行能力 schema。
4. MySQL 能力库可能统一 `stationroute`、`stationroutetime` 的字符集/排序规则。
5. 执行作业计划结构/索引升级、站场关系表升级及历史数据回填。
6. 将旧过程股道引用迁移到真实停留进路，并回填作业占用快照。

能力库使用 `capacityschemamigration` 记录部分已完成步骤。站场迁移还会调整 ID 类型/排序规则、补列，并在成功转换历史 `LayoutDocument` 后移除该列。这是实际数据升级，启动不是只读探测。

基础 SQL 通过 `CREATE TABLE IF NOT EXISTS` 创建表；特定迁移补齐部分字段，不意味着任意旧库都能自动升级。遇到损坏快照、歧义进路或重复标识会报错，应保留数据现场并依据日志处理。

MySQL 数据库本身须提前创建；SQLite 的父目录须存在且可写。当前 `capacitysolvepreset` 还由预设服务在访问时执行建表，不属于“启动后所有接口都完全不再执行 DDL”的范围。

## 5. 配置优先级和常用键

API 使用 ASP.NET Core 默认配置链：基础 JSON、当前环境 JSON、适用的开发用户密钥、环境变量、命令行配置。后者覆盖前者；环境变量用 `__` 表示嵌套层级。

`Program.cs` 读取 `WebApi:Hosts` 后调用 `UseUrls`，因此仅修改 `ASPNETCORE_URLS`、`--urls` 或 `launchSettings.json` 的 `applicationUrl` 不能替代本程序的监听配置。无有效 `WebApi:Hosts` 会直接终止启动。

| 键 / 环境变量 | 含义及注意点 |
| --- | --- |
| `ASPNETCORE_ENVIRONMENT` | 选择 Development / Production 等环境 |
| `WebApi__Hosts__0`、`__1` | 监听地址数组；生产常用回环地址及空的第二项 |
| `App__Version` | 版本接口与启动日志使用的应用版本文本 |
| `Jwt__SecretKey` | JWT 签名密钥，应私有注入 |
| `Jwt__Issuer` / `Jwt__Audience` | 签发者 / 受众，须与验证端一致 |
| `Jwt__ExpirationMinutes` | Web 访问令牌分钟数，代码默认 60 |
| `Jwt__RefreshTokenExpirationDays` | Refresh Token 天数，默认 7 |
| `Jwt__CapacityAgentExpirationMinutes` | Agent 专用令牌分钟数，默认 1440 |
| `Cors__AllowedOrigins__0` 等 | 生产允许的前端 origin，须包括协议和端口 |
| `<库节>__DatabaseType` | 三库分别选择 SQLite / MySQL |
| `<库节>__SqlliteConfig__DatabaseFile` | SQLite 文件，推荐绝对路径 |
| `<库节>__MysqlConfig__Host` / `Port` / `Database` | MySQL 主机、端口、库名 |
| `<库节>__MysqlConfig__Username` / `Password` | MySQL 账号、密码 |
| `<库节>__MysqlConfig__CharSet` / `SslMode` | 字符集、连接 TLS 模式 |
| `<库节>__MysqlConfig__AllowPublicKeyRetrieval` | 可选 MySQL 公钥取回设置 |
| `<库节>__MysqlConfig__ConnectionTimeout` | 连接超时秒数，默认 15 |
| `Course__VideoDir` / `Course__DocDir` | 课程目录，详见运维章 |

`<库节>` 为 `HumpDatabase`、`CapacityDatabase` 或 `SketchLayoutDatabase`。配置数组按索引合并，不会因仅设置第 0 项而自动删除其余项。监听数组过滤空项；CORS 列表没有同样的过滤步骤，部署时应维护完整的最终 origin 列表。

生产启动校验检查合并后的 JWT、所选 MySQL 驼峰库及 SketchLayout 库凭据，不验证所有连接参数，也没有同等检查能力库凭据。错误信息虽称“环境变量”，实际检查的是合并配置值；仍应自行以私有环境文件或密钥管理注入敏感值。

## 6. 启动和构建主前端

另开 PowerShell，从仓库根目录执行：

```powershell
Set-Location switchyard-vue
npm.cmd ci
npm.cmd run dev
```

默认页面为 `http://localhost:5173`。Vite 监听所有 IPv4 网卡，`strictPort` 已启用，端口占用会报错，不会自动换号。开发 API 默认使用页面同主机名的 HTTP `7297`。

需要固定 API 时，在启动前指定根 URL：

```powershell
$env:VITE_API_BASE_URL = 'http://localhost:7297'
npm.cmd run dev
```

根 URL 不带 `/api` 后缀；业务还使用 `/Hump`、`/Capacity`、`/StationLayout`、`/OperationPlan` 等根路径。常规源码启动时 `VITE_API_BASE_URL` 优先于前端 JSON 配置，可保存到自己的 `.env.development.local`。Docker 前端的运行时配置优先级更高，见 [Docker 环境变量](12-docker-deployment.md#3-环境变量)。

Vite 开发代理只代理 `/api`，并不涵盖全部业务路径。当前前端主要直接请求配置的 API 根地址。开发 API 的动态 CORS 策略允许任意来源；生产只接受配置列表中的来源。

构建和本地预览：

```powershell
$env:VITE_API_BASE_URL = 'http://localhost:7297'
npm.cmd run type-check
if ($LASTEXITCODE -ne 0) { throw 'Type check failed' }
npm.cmd run build-only -- --mode production
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
npm.cmd run preview
```

产物为 `dist/`，预览默认端口 `4173`。`npm run build` 组合类型检查与构建；分步命令方便定位失败。Linux/macOS 将 `npm.cmd` 换为 `npm`，环境变量可写成 `VITE_API_BASE_URL=https://api.example.com npm run build-only -- --mode production`。

这种直接源码构建的 API 地址在开发服务器启动或构建时读取，修改部署服务器环境变量不会更新已构建页面。Docker 部署会在容器启动时生成更高优先级的运行时配置，改变 API 地址后重建容器即可。当前 `config.ts` 在非开发服务器环境选择生产 JSON，因此 `build:dev` 不等于使用开发 API，需显式指定 URL。以上运行时配置说明核对至 2026-10-15。

## 7. 安装 CapacityAgent

API 负责分派任务，Agent 负责通信、调度和资源监控，独立 Worker 子进程调用 `SwitchYard.CapacitySolver` 类库求解。发布 Agent 时会自动带入 Solver 和 OR-Tools 运行依赖，无需另行部署 Solver 可执行程序。没有可用 Agent 时，站场编辑等功能仍可使用，依赖模型的求解入口不能完成任务。此项目职责和测试入口说明更新于 2026-10-27。

```powershell
dotnet publish SwitchYard.WebApi/SwitchYard.CapacityAgent/SwitchYard.CapacityAgent.csproj -c Release -o ./publish/capacity-agent
Set-Location publish/capacity-agent
dotnet SwitchYard.CapacityAgent.dll
```

按提示输入 API 根 URL、用户名和密码。账号须已激活；如要求首次改密，先在 Web 页面完成。当前主程序采用交互输入，没有附带无人值守 Agent 服务安装脚本或密码环境变量协议。

Agent 向 `/api/capacity-agents/authenticate` 获取带客户端类型声明的专用 JWT，再连接 `/hubs/capacity-agent`。它不会用 Web Refresh Token 轮换流程，令牌需要更新时重新使用账号凭据认证。

发布目录中的 `capacity-agent.json` 可替换为：

```json
{
  "agentName": "workstation-a",
  "maxConcurrentJobs": 2,
  "cpuCoresPerJob": 2,
  "memoryLimitMbPerJob": 2048,
  "statusReportIntervalSeconds": 2,
  "allowAllUsers": false,
  "allowedUsers": []
}
```

也可通过 `dotnet SwitchYard.CapacityAgent.dll --config <私有配置路径>` 选择文件。配置内容不包含账号密码。

| 字段 | 范围 / 行为 |
| --- | --- |
| `agentName` | 空时使用操作系统用户名；建议多机器使用不同名称 |
| `maxConcurrentJobs` | 1–128，默认 2 |
| `cpuCoresPerJob` | 1–128，默认 2；每任务线程上限，不是 CPU 绑核 |
| `memoryLimitMbPerJob` | 256–1048576，默认 2048；SCIP 设置与 Worker 工作集监测 |
| `statusReportIntervalSeconds` | 1–60，默认 2 秒 |
| `allowAllUsers` / `allowedUsers` | 仅管理员 Agent 可开放共享；白名单最多 500 个用户名，每项最多 100 字符 |

普通用户 Agent 永远只允许本人使用。管理员可开放全部已登录用户，或只开放所有者与白名单用户；未配置共享时仍只供本人。服务端强制执行权限，不能通过修改本地 JSON 越权共享。

Agent ID 由 Agent 名称和登录用户名计算；多台机器用同一组合会产生相同 ID。需要同时在线时设置不同 `agentName`。可用槽位还受逻辑 CPU 和内存余量约束，配置并发为 2 不代表必定有 2 个空闲槽位。

### 7.1 模型、Worker 和任务生命周期

当前模型 ID 为 `station-capacity-v1`，版本 **2.5.0**，在 `SwitchYard.CapacitySolver` 中使用 OR-Tools / SCIP 混合整数规划。ID 中的 `v1` 不表示版本 1。普通列车约束顺序作业与轨道电路占用；过程列车还保留事件图、时长、严格/最小次序、资源地点及锚约束。

当前普通列车输入要求模型至少 2.4.0，含作业过程引用时至少 2.5.0；服务端检查输入的 `minimumModelVersion`，并根据参数集与过程引用提高最低要求。旧 Agent 可能在线但无法接收当前任务，应更新 Agent 及随包 Solver 后重新注册。

每个任务运行在独立 Worker 进程。Agent 启动自身的 `--solve-worker` 内部入口，由该入口调用 Solver 类库的 Worker Host；主进程中的模型适配器负责转发任务，不执行求解算法。CPU 资源约束控制求解线程；Agent 定期检查 Worker 工作集并在超过上限时终止它。资源限制不能等同于完整操作系统容器隔离。

服务端先保留空闲槽位再派发，状态经过 `queued`、`running`，最后为 `completed` 或 `failed`。`queued` 不是等待未来资源的持久队列。任务 `completed` 也不保证有可行解，须查看结果的 `hasSolution` 和求解器 `status`。

当前没有 Web/HTTP 的单任务取消接口。按 `Ctrl+C` 退出 Agent 会传播取消并终止相应 Worker，可能影响这台 Agent 的所有任务。网络断开会触发重连；重新注册时报告活动任务，服务端把已丢弃的旧任务标记失败。不要把重连视为跨进程恢复求解。

| 数据 | 是否持久化 |
| --- | --- |
| Agent 在线注册、任务状态、进度、原始结果 JSON | Service 内存；API 重启后丢失 |
| 求解预设 | 能力库 `capacitysolvepreset` |
| “模型求解”页面的普通结果 | 可下载 JSON，不自动写回作业计划 |
| “生成饱和计划”的成功结果 | 验证可行解后事务另存为新计划，含作业、资源和过程快照 |
| 单独保存的分析结果 | 对应能力分析数据表，与 Agent 内存任务不同 |

重启前下载需要保留的原始结果，并安排任务完成。生成饱和计划优化已有列车的作业，不自动增加列车数搜索最大开行量。

### 7.2 可选自检与安装验收

在仓库根目录执行独立求解测试项目：

```powershell
dotnet run --project SwitchYard.WebApi/SwitchYard.CapacitySolver.Tests
```

业务自检已从 Agent 移至此测试项目；Agent 的 `--self-test` 参数会提示新入口并退出，不再运行求解案例。测试覆盖共享领域对象、进路连通性、过程引用、并发 Worker 求解、未知模型拒绝和运行中取消，无需登录 API，不写业务数据库。通过该检查仍不等于三库、代理或业务数据通过联调；发布后还应通过 Service 向 Agent 提交代表性任务，验证 Worker 启动、进度和结果回传。

当前工作区部分 `*.Tests` 项目及 `switchyard-vue/checks` 由忽略规则排除。旧文档提到的本地测试文件不保证在新克隆中存在；执行前先确认文件确已交付，不把缺少文件当作测试通过。

实际安装验收至少记录：

1. SDK/Runtime/Node 版本、代码提交和 API/前端产物版本。
2. 三组数据库及最终监听地址正确，初始化没有异常。
3. 有效账号登录，模板 `001` 可支持经授权的用户创建验证。
4. 打开并保存测试实例、站场、作业计划及布置图方案；课程清单/文件可读。
5. Agent 上线、当前账号可见且有空闲槽位；代表性输入返回预期结果。
6. 在测试数据上验证饱和计划另存、重启后已保存数据仍可打开。

## 8. 依据与保留资料

- [API 启动与配置](../../SwitchYard.WebApi/SwitchYard.Service/Program.cs)、[数据库连接器](../../SwitchYard.WebApi/SwitchYard.Service/DBConnector.cs)。
- [作业计划迁移](../../SwitchYard.WebApi/SwitchYard.Service/Services/OperationPlanSchemaMigration.cs)、[站场迁移](../../SwitchYard.WebApi/SwitchYard.Service/StationLayout/StationLayoutSchemaMigration.cs)。
- [Agent 配置校验](../../SwitchYard.WebApi/SwitchYard.CapacityAgent/CapacityAgentOptions.cs)、[Agent 主程序](../../SwitchYard.WebApi/SwitchYard.CapacityAgent/Program.cs)。
- [原开发指南](../archive/guides/Getting-Started.md)、[原 Agent README](../archive/development/capacity-agent.md)、[下一章：部署与运维](08-deployment-operations.md)。
