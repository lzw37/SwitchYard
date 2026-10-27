# 接口与数据参考

[文档目录](README.md) · [English](../en/10-api-data-reference.md)

本章供接入客户端、维护数据和定位接口错误时使用。主服务当前包含 175 个控制器 HTTP 操作，完整方法及路径见[接口清单](appendix-api-routes.md)。请求字段以本章链接的 DTO 和对应控制器签名为准；开发环境还可以打开 `/swagger` 查看运行服务的 OpenAPI。生产环境默认不开启 Swagger。

## 地址与协议

前端 `VITE_API_BASE_URL` 填 API 的根地址，例如 `http://localhost:7297`，不统一追加 `/api`。认证与代理接口使用 `/api/...`，驼峰、站场、作业计划、过程和课程使用各自前缀。接口中的 `instanceID`、`stationSchemeID` 等必须来自当前登录用户能够访问的业务对象。

默认 JSON 序列化使用 camelCase。不同 DTO 的 C# 字段可能是 `InstanceID` 或 `InstanceId`，相应输出可能为 `instanceID` 或 `instanceId`；不要自行把所有返回键改成同一种拼写。部分旧接口直接返回字符串或 JSON 文本，未采用统一 `{ data, code }` 包装。课程文件接口返回二进制流；上传接口使用 `multipart/form-data`。

驼峰控制器采用 `[controller]/[action]` 路由。特性中的 `Name` 只是路由名，例如 `[HttpPost(Name = "ExecuteCalculation")]` 对应的实际路径是 `/Hump/ExecuteEnergyHeightCalculation`。读取站场 JSON 的 `GetJson` 使用 **POST**，不能仅根据动词猜测 HTTP 方法。

## 认证与权限

### 登录和会话

Web 客户端将密码先计算为 SHA-256 十六进制值，再提交给服务端；服务端以 Argon2id 验证保存的密码数据。对接普通登录接口时遵守相同约定。网络传输仍需部署 HTTPS，密码摘要本身也是可以用于登录的凭据。

```http
POST /api/Auth/login
Content-Type: application/json

{
  "username": "<existing-username>",
  "password": "<SHA256_HEX_OF_PASSWORD>"
}
```

成功响应包含 `token`、`tokenType`、`expiresIn`、`refreshToken`、`refreshTokenExpiresIn`、`userID`、`name`、`role` 和 `mustChangePassword`。后续普通业务请求使用：

```http
Authorization: Bearer <token>
```

`POST /api/Auth/refresh` 接收 `{ "refreshToken": "<refresh-token>" }`，返回新访问令牌和轮换后的刷新令牌。客户端需同时替换两者。`POST /api/Auth/logout` 使用相同请求结构撤销刷新令牌。过期、停用或已轮换的令牌不应反复重试。

登录、刷新和代理认证使用 `auth` 限流策略，按远端 IP 每分钟最多 5 次；注册使用 `register` 策略，每 10 分钟最多 3 次。具体操作上是否绑定策略以控制器特性为准；超限返回 429。反向代理配置影响服务识别的客户端 IP。

### 访问范围

| 接口族 | 权限要点 |
| --- | --- |
| `/api/Auth/login`、`refresh`、`logout`、`createuser` | 无需已有 JWT；仍检查凭据、令牌或注册数据 |
| `/api/Auth/userinfo`、`changepassword` | 需要 JWT |
| `/api/Admin/...` | `Admin` 角色 |
| `/Hump/...` | JWT，并按具体操作检查实例所有者或管理员权限 |
| `/Capacity/...`、`/StationLayout/...`、`/OperationPlan/...`、`/OperationProcess/...` | JWT，且校验实例及方案范围；不能跨范围拼接对象 ID |
| `/SketchLayout/schemes...` | 每次请求从 HumpDatabase 重新查询有效用户及当前角色；普通用户访问自己的方案，管理员可管理全部 |
| `/Course/...`、`/api/System/version` | 允许匿名访问 |
| `/api/capacity-agents/...` | 认证入口校验账号，其他接口需要 JWT，并按代理访问规则和任务所有者限制 |
| `/hubs/capacity-agent` | 专门的 `CapacityAgent` 授权策略；普通浏览器登录令牌不等同于代理令牌 |

权限逻辑并非所有模块都相同，例如 Sketch 每次查当前账号状态，而旧接口还使用 JWT 声明。前端隐藏按钮不能替代服务端授权。

## 接口族与数据契约

| 接口族 | 主要用途 | 字段定义与实现 |
| --- | --- | --- |
| Auth / Admin | 登录、刷新、注册、资料、用户导入及管理 | [认证模型](../../SwitchYard.WebApi/SwitchYard.Service/Models)、[AuthController](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/AuthController.cs)、[AdminController](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/AdminController.cs) |
| Hump | 实例、溜放线、平纵断面、车辆、条件、计算、间隔检验 | [驼峰核心模型](../../SwitchYard.WebApi/SwitchYard.Hump)、[HumpController](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/HumpController.cs) |
| Capacity | 能力实例增删改查和复制 | [CapacityInstance](../../SwitchYard.WebApi/SwitchYard.Capacity/Database/CapacityInstance.cs) |
| StationLayout | 方案、JSON、修复、DWG、进路、停留进路与时间参数 | [布局请求](../../SwitchYard.WebApi/SwitchYard.Capacity/StationLayout/StationLayoutRequests.cs)、[模块契约](../../SwitchYard.StationLayout/backend) |
| OperationPlan | 计划、列车、移动、批量修改、占用导入和重算、图表与分析保存 | [控制器](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/OperationPlanController.cs)、[占用导入模型](../../SwitchYard.WebApi/SwitchYard.Service/Models/CellOccupancyImportModels.cs) |
| OperationProcess | 模板、目录、活动和事件等子对象、版本控制 | [过程模型](../../SwitchYard.WebApi/SwitchYard.Service/Models/OperationProcessModels.cs)、[过程控制器](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/OperationProcessController.cs) |
| CapacityAgents | 在线代理、预设、求解输入、任务与饱和计划 | [代理共享契约](../../SwitchYard.WebApi/SwitchYard.Capacity/Contracts/CapacityAgentContracts.cs) |
| SketchLayout | 方案文档增删改查与保存版本分析 | [布置图模型](../../SwitchYard.WebApi/SwitchYard.SketchLayout/Models.cs)、[校验规则](../../SwitchYard.WebApi/SwitchYard.SketchLayout/Validation.cs) |
| Course | 资源清单及支持范围读取的文件流 | [CourseController](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/CourseController.cs) |

分页接口使用 `pageNumber` 和 `pageSize`，默认分别为 1 和 10，单页上限 100。用户分页另外支持 `keyword`。以 `GetInstancePage` 或 `/api/Admin/users/paged` 返回的分页结构读取总数，不要把普通列表接口当作分页接口。

## 常用请求示例

以下 ID、令牌与版本均为占位值。先读列表获得真实 ID 和当前 revision，再提交修改。

### 读取能力实例与站场文档

```http
GET /Capacity/GetInstancePage?pageNumber=1&pageSize=10
Authorization: Bearer <token>
```

```http
GET /StationLayout/GetStationSchemes?instanceID=<instance-id>
Authorization: Bearer <token>
```

```http
POST /StationLayout/GetJson?instanceID=<instance-id>&stationSchemeID=<scheme-id>
Authorization: Bearer <token>
```

`GetJson` 返回站场文档。`SaveJson` 接收 `json` 字符串、范围信息及 `expectedRevision`；`RepairJson` 供修复流程使用。使用独立模块的 Gateway 可统一处理这些契约，详见[开发与接入](09-development-integration.md)。生产业务读取始终显式传实例和方案，不依赖旧的无实例文件回退。

### 从多个过程生成初始计划

```http
POST /OperationPlan/GenerateTrainOperationPlanFromProcess
Authorization: Bearer <token>
Content-Type: application/json

{
  "instanceID": "<instance-id>",
  "stationSchemeID": "<scheme-id>",
  "operationPlanID": "<plan-id>",
  "processes": [
    { "processTemplateID": "<process-a>", "revision": 3, "trainCount": 2 },
    { "processTemplateID": "<process-b>", "revision": 1, "trainCount": 3 }
  ],
  "startTime": "08:00",
  "endTime": "09:00"
}
```

正数量过程必须存在且版本一致。整批生成在事务中写入列车、移动与过程快照；失败时回滚本批业务写入。接口不提供批次幂等键，成功后再次提交相同请求会再生成一批。若响应丢失，先重新读取计划确认结果，避免盲目重试。约束与数量上限见[作业过程](05-operation-process.md)。

### 预览求解输入

```http
POST /api/capacity-agents/preview-input
Authorization: Bearer <token>
Content-Type: application/json

{
  "instanceId": "<instance-id>",
  "stationSchemeId": "<scheme-id>",
  "operationPlanId": "<plan-id>"
}
```

返回的输入包含 `stationLayout` 基础数据快照，覆盖当前实例、当前站场方案的全部记录，不按作业计划裁剪：

| 字段 | 数据内容 |
| --- | --- |
| `stationSchemeId` | 布局所属站场方案，布局内各集合的记录不再重复携带此字段 |
| `cells` | Cell 数据，省略 `instanceId`、`stationSchemeId` 和 `extraProperties` |
| `routes` | Route 数据，包括车种、节点与线路列表、干扰 Cell、标签和起终点；省略 `instanceId`、`stationSchemeId` |
| `calculationParameterSets.sets` | 参数集标识、名称、默认标记；省略 `instanceId`、`stationSchemeId` |
| `calculationParameterSets.routeOccupations` | 以 `calculationParameterSetId` 为 key、占用行数组为 value 的字典；占用行省略 `instanceId`、`stationSchemeId` 和重复的 `calculationParameterSetId` |
| `routeOperationTimes` | 各参数集、进路和车种的作业时长 `operationTimeS`；省略 `instanceId`、`stationSchemeId` |
| `links`、`nodes` | 拓扑 Link、Node 数据，省略 `instanceId`、`stationSchemeId` 和 `extraProperties` |

占用参数的读取路径为 `stationLayout.calculationParameterSets.routeOccupations[参数集ID]`，不再使用布局层的平铺 `routeOccupations`。参数集 ID 仅由字典 key 表达，行内不重复传递。没有占用行的参数集对应空数组。旧占用行的参数集 ID 为 `null` 时归入 `default` key。若方案尚未保存任何参数集，`sets` 包含系统提供的有效默认集 `default`。

快照字段名使用 camelCase；保留字段的数据库值不变，包括 `null` 和原始列表字符串。输入顶层原有 `routes`、`routeOccupations` 仍是供算法使用的投影，求解占用参数的参数集 ID 从字典 key 获取；快照只传递基础数据，不增加求解约束。Agent 也能读取旧版参数集数组，兼容旧任务输入。旧任务按原始输入重试时不会补入当前布局。

输入顶层还包含与 `stationLayout` 同级的 `operationProcesses` 数组，传递当前实例、站场方案的全部已保存作业过程模板（模板库的 `OperationPlanID = ''`），包括未用于当前计划的模板和尚未编排完整的草稿；浏览器中未保存的修改不包含在内。模板内不重复传递实例、方案和计划标识。

| `operationProcesses` 中的字段 | 数据内容 |
| --- | --- |
| `id`、`name`、`description`、`revision` | 模板标识、名称、说明和版本 |
| `activities`、`events` | 完整活动与事件，包括候选进路、选定资源、持续时间及编排坐标 |
| `precedences`、`anchors`、`routeAnchors` | 事件次序、锚及进路端点与锚的关联 |

子字段统一使用 camelCase，例如 `nodeId`、`automaticAnchorIds`、`dwellingActivityId`、`routeIds`、`routeId`。模板中的时间、持续时间和次序间隔保留分钟单位，活动 `x`、`y` 保留编排图坐标；事件 `nodeList` 与编排页面 API 一样，在读取时按当前进路刷新。旧输入缺少 `operationProcesses` 时按空数组读取。

由作业过程生成的列车通过 `trains[].operationProcessId` 引用该数组中的当前已保存模板；持续时间、固定时刻、次序和锚约束使用模板的最新版本。列车附带执行基准 `processOriginSeconds`（秒），各移动通过 `movements[].activityId` 对应模板活动，原计划事件时刻从移动的 `originalStartSeconds`、`originalEndSeconds` 及起止事件 ID 恢复，不再传递 `processEventTimes` 字典。独立固定事件使用执行基准加模板时刻；独立且无固定时刻的事件由过程约束确定。原计划时刻不必满足后来编辑的约束，由求解器重新调整。普通列车不附带过程引用；模板被删除或活动与已有移动不再一一对应时，需重新生成作业计划。

新输入不再输出 `trains[].processConstraints`，Solver 调用共享领域构图逻辑从引用重建求解约束；旧任务的该字段仍可读取，旧输入中的 `processEventTimes` 会被忽略。求解结果中的 `eventTimes` 保持不变。含过程引用的输入要求模型版本至少为 `2.5.0`，使用前需更新 CapacityAgent 及随包 Solver；普通列车输入仍要求 `2.4.0`。

移动的 `startLocationsByRoute`、`endLocationsByRoute` 以候选进路 ID 为 key，列表中的每个地点是对象：`nodeId` 是节点，`anchorId` 是锚，`dwellingRouteId` 是该锚对应的停留进路；不存在的字段显式为 `null`。例如：

```json
{
  "startLocationsByRoute": {
    "R1": [{ "nodeId": "N1", "anchorId": null, "dwellingRouteId": null }]
  },
  "endLocationsByRoute": {
    "R1": [{ "nodeId": "N2", "anchorId": "A1", "dwellingRouteId": "D1" }]
  }
}
```

旧字段名 `startLocationIdsByRoute`、`endLocationIdsByRoute` 仍可读取，输出只使用新字段名。Agent 仍接受旧版列表中的编码数组字符串（如 `"[\"N1\",null,null]"`）和纯节点字符串；重新输出时统一使用对象，畸形编码数组会被拒绝。包含地点对象的输入也要求模型版本至少为 `2.5.0`，即使没有作业过程引用。

通用任务由 `POST /api/capacity-agents/jobs` 提交 `{ "agentId": "...", "modelId": "...", "input": { ... } }`，成功接收返回 202 和任务对象。通过 `GET /api/capacity-agents/jobs/{jobId}` 查询状态及结果。专用 `saturated-plan-jobs` 接口使用范围和 `presetId` 构建输入并关联新计划保存流程；通用任务不自动等同于这个流程。

### 共享能力领域模型

当前领域模型位于 `SwitchYard.Capacity/Domain`，命名空间为 `SwitchYard.Capacity`，类型为公开 API。`SwitchYard.CapacitySolver` 引用共享库，通过 `CapacityProblem.Build(input)` 将传输 DTO 装配为当前任务的对象图。Agent 负责通信和调度，在 Worker 子进程中调用 Solver 类库。进路、Cell、Link、Node 和参数集复用同一个实体实例；参数占用行直接引用其 `Route`、`Cell`、`ParameterSet`。原始字段保留在实体的 `Input`／`Layout` 中，构图不会改变传输 JSON。

`Train.OperationProcess` 指向共享编排模板，`Train.Process` 保存本列车的执行图。`Movement.Activity`、`StartEvent`、`EndEvent`、`CandidateRoutes` 与 `RouteOptions` 均为对象引用；地点通过 `Node`、`Anchor`、`DwellingRoute` 关联实际实体。模板内活动、事件和锚按模板作用域解析；执行事件、锚和地点按列车作用域隔离，候选地点进一步按事件隔离。求解约束使用这些对象关系，ID 仅在输入解析、求解变量键和结果输出边界使用。

未引用的草稿及缺失布局元数据记录在 `problem.Issues`，包含来源路径、目标类型和 ID。旧输入可从求解投影建立基础实体；缺失实际需要的活动、事件、锚或占用参数仍报错。当前 JSON 没有道岔、信号机实体定义，其 ID 保留并记为未解析引用；车种代码和标签保留为值。

与 CapacityAgent 通信的类型集中在 `SwitchYard.Capacity/Contracts`，包括认证、注册、资源状态、求解命令、进度、结果，以及布置图、参数集、作业过程和地点输入 DTO。配套 JSON 转换器也位于该目录。类型名、命名空间 `SwitchYard.Capacity` 和 JSON 格式保持不变。

直接映射数据库查询结果的类型集中在 `SwitchYard.Capacity/Database`，包括能力实例、计算参数集、布置图、作业计划及分析结果行。Service 先查询数据库行，再转换为 `Contracts` 中的 Agent 输入 DTO；领域对象及构图逻辑位于 `Domain`。

其他共享类型按功能分类：`SwitchYard.Capacity/StationLayout` 包含布置图 JSON、读写请求、坐标转换、DWG 导入、进路搜索及计算参数集请求；`SwitchYard.Capacity/OperationPlans` 包含作业计划接口、占用数据读写与校验、车站作业计划图和资源占用图设置。所有类型仍使用 `SwitchYard.Capacity` 命名空间，项目根目录只保留项目文件。

### 保存并分析布置图

1. `GET /SketchLayout/schemes/{id}` 读取文档及 `revision`。
2. `PUT /SketchLayout/schemes/{id}` 提交 `{ "revision": <current-revision>, "document": { ...完整方案... } }`。
3. 保存成功后使用返回的新 revision 更新客户端。
4. `POST /SketchLayout/schemes/{id}/analysis` 分析服务端已保存的文档，不会自动读取尚未保存的浏览器编辑。
5. 删除使用 `DELETE /SketchLayout/schemes/{id}?revision=<current-revision>`。

Sketch 控制器请求大小上限为 8 MiB；DWG 导入上限为 20 MiB。代理服务器也需允许相应请求大小，应用限制和代理限制同时生效。

## 并发修改与错误处理

| 状态 | 常见原因 | 客户端处理 |
| --- | --- | --- |
| 400 | 字段缺失、非法范围、图约束不成立、文件或 JSON 不符合要求 | 显示服务端校验信息，修改输入后重试 |
| 401 | 缺少或失效的凭据，账号不可用 | 必要时刷新一次令牌，否则重新登录 |
| 403 | 已登录但没有相应范围权限 | 核对当前用户、实例和角色 |
| 404 | 对象不存在或当前范围不可见 | 重新读列表，确认选择和 ID |
| 409 | revision 冲突、依赖对象丢失、代理不可分配等 | 根据响应内容重新加载或补齐依赖；不要强制覆盖 |
| 413 | 应用或代理拒绝过大的请求 | 核对文件大小和两层限制 |
| 429 | 认证或注册限流 | 等待限流窗口结束，避免自动循环登录 |
| 500 | 数据结构、存储或计算异常 | 记录操作范围、时间和错误，再查服务日志 |
| 503 | 求解任务发送失败等服务不可用情况 | 先查询返回任务的状态，再决定是否重试 |

过程模板更新和子对象操作使用模板 `revision`；新模板以 0 提交，服务端创建后为 1。子对象 `collection` 使用控制器支持的集合名，更新后的响应包含完整模板。站场文档使用 `expectedRevision`，Sketch 使用其方案 revision。它们是不同对象的版本号，不能混用。

驼峰依赖缺失可返回 409、`code: HUMP_REFERENCE_MISSING`、兼容字段 `legacyCode: HUMP_CALCULATION_DEPENDENCY_MISSING` 和 `missingDependencies`。这类 409 不是多人同时编辑，应按清单重新选择或补齐依赖。

不要把所有 GET 都当成无副作用操作：旧作业计划列表读取可能创建默认计划，部分旧数据处理会准备或迁移结构。使用已准备好的测试库验证接入，不对生产库自动遍历调用接口。

## 数据库分工与层级

| 配置节 | 主要对象 | 关系与标识 |
| --- | --- | --- |
| `HumpDatabase` | `user`、`refreshtoken`、`humpinstance`、`slopeline`、平纵断面、车辆、条件、计算及间隔结果 | 用户是所有模块的账号来源；实例 Owner 主要保存用户名 |
| `CapacityDatabase` | `capacityinstance`、`stationscheme`、Node/Link/Cell 等布局、`stationroute`、`stationroutetime`、计划、列车、移动、过程、图表和分析 | 实例 → 站场方案 → 作业计划 → 列车 → 移动 |
| `SketchLayoutDatabase` | `sketchlayoutscheme` | 按 `OwnerId` 归属，文档保存在 `DocumentJson`，另存名称、说明和 revision |

能力模块的列车模板、移动模板和过程模板属于**站场方案共享库**。兼容存储仍有 `OperationPlanID` 列，但共享键为 `''`；删除一个作业计划不应理解为删除方案模板。

过程列车的 `trainprocesssnapshot` 使用实例、方案、计划和列车联合定位，保存源模板 ID 与版本、完整过程副本、基准秒数、活动到移动映射、事件时刻、`SelectedRouteIDs` 及执行次序。当前停留活动同样引用真实 StationRoute，旧审阅记录中的 `SelectedTrackIDs` 和临时停留资源是历史实现。

数据库基础结构在 [Database](../../SwitchYard.WebApi/SwitchYard.Service/Database) 下；后续结构和旧数据转换还由 [OperationPlanSchemaMigration](../../SwitchYard.WebApi/SwitchYard.Service/Services/OperationPlanSchemaMigration.cs)、[StationLayoutSchemaMigration](../../SwitchYard.WebApi/SwitchYard.Service/StationLayout/StationLayoutSchemaMigration.cs) 和 [OperationProcessRouteMigration](../../SwitchYard.WebApi/SwitchYard.Service/Services/OperationProcessRouteMigration.cs) 管理。只执行基础 SQL 不等同于完成全部升级。迁移与回滚见[部署与运维](08-deployment-operations.md)。

## 删除与关联数据维护

程序主要通过应用层事务和范围条件维护关联，而不是依靠一个跨数据库外键级联。维护脚本不能只按显示名称或单个局部 ID 删除数据。

| 操作 | 当前影响范围 |
| --- | --- |
| 删除能力实例 | 清理其站场方案、布局、进路、时间参数、模板、计划、列车、移动、快照、图表和分析，再删除实例 |
| 删除站场方案 | 清理该方案下的布局、进路、共享模板和全部计划相关数据 |
| 删除作业计划 | 清理该计划的列车、移动、过程快照、图表设置和分析；保留方案共享模板 |
| 删除列车 | 清理关联移动和过程快照，并使该计划相关分析结果失效 |
| 删除进路 | 检查实际移动、模板、过程与快照的引用；仍被引用则拒绝，解除引用后清理进路参数和相关分析 |
| 修改驼峰几何 | 相应计算曲线、减速器状态及关联间隔结果可能被清理，保留可编辑计算设置以便重算 |
| 删除布置图方案 | 删除该方案及其 JSON 内部设施、进路、流程和设置；需匹配 revision |
| 删除用户 | 当前用户删除服务只删除用户记录，不代表跨三库业务资料已级联删除 |

能力实例的“复制”当前复制的是实例元信息，并不会复制其全部子方案与计划。需要复制具体布局或计划时使用对应的方案、计划复制功能。驼峰实例复制有专门的复制服务，不能假设各模块“复制”的范围完全一致。

删除前应确认对象归属，必要时先备份或复制可用方案。删除或修改后出现空分析结果，先检查是否已按关联规则失效并需要重新计算。历史遗留孤立数据的清理 SQL 位于 [hump-orphan-cleanup.sql](../../SwitchYard.WebApi/SwitchYard.Service/Database/hump-orphan-cleanup.sql)；它是管理员维护工具，不是普通用户操作步骤。

实现依据：[CapacityDataLifecycle](../../SwitchYard.WebApi/SwitchYard.Service/Services/CapacityDataLifecycle.cs)、[HumpDataLifecycle](../../SwitchYard.WebApi/SwitchYard.Service/Services/HumpDataLifecycle.cs)、[SketchLayoutStore](../../SwitchYard.WebApi/SwitchYard.Service/Services/SketchLayoutStore.cs)、[UserService](../../SwitchYard.WebApi/SwitchYard.Service/Services/UserService.cs)。

## 代理通讯

代理连接 `/hubs/capacity-agent`，调用 `RegisterAgent`、`ReportStatus`、`ReportProgress` 和 `CompleteJob`；服务端通过 `Solve` 分发任务。注册内容含模型、访问策略、资源容量和仍在执行的任务 ID。接口及 Hub 当前没有浏览器取消任务的标准操作；停止代理会影响其正在执行的 Worker。

代理注册、任务、结果和各模型输入结构见 [CapacityAgentContracts.cs](../../SwitchYard.WebApi/SwitchYard.Capacity/Contracts/CapacityAgentContracts.cs)，运行方式见[安装与首次启动](07-installation.md)。不要把 API 进程内存中的任务状态作为可备份、可恢复的数据库任务队列。
