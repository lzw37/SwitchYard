# 牵引计算关系存储

牵引计算使用通过能力数据库（`CapacityDatabase`），兼容 SQLite 和 MySQL。服务启动时 `TractionSchemaMigration.Ensure` 幂等建立以下 24 张现行表（另保留空的旧表用于升级兼容），不创建 JSON、JSONB、BLOB 或承载整块参数文档的字段。文本列仅保存名称、标识、类型、时间戳和单条警告；数值、边界是否已知等分别保存为数值列。

## 表与关系

计算方案以 `InstanceID`、`StationSchemeID`、`CalculationSetID` 为组合主键。方案下的进路记录增加 `RouteCaseID`，通过四列作用域独立保存参数与结果；同一方案内 `(RouteID, ContinuationID)` 唯一，所以主进路单独计算与主进路接续计算分别保留。有序子表增加 `OrderIndex`，预设增加 `PresetID`。子表通过组合外键引用父表，配置级删除采用事务内显式清理，并定义 `ON DELETE CASCADE`。

| 表 | 内容 | 父表 |
| --- | --- | --- |
| `tractioncalculationset` | 计算方案名称、管理版本、创建和更新时间 | 应用校验实例及站场存在 |
| `tractionsetselection` | 每个实例、站场当前选中的 CalculationSetID；主键为 InstanceID、StationSchemeID | `tractioncalculationset` |
| `tractionroutecase` | 主进路、接续进路、记录版本、是否为方案当前选择 | `tractioncalculationset` |
| `tractionrouteworkspace` | 数据版本、选中预设及进路、距离标尺、缺省限速、信号参数、作业模式、边界速度、计算精度 | `tractionroutecase` |
| `tractionroutetrainpreset` | 每组列车名称、长度、质量、最高速度、惯性质量倍率（1+γ）、常用制动力及 Davis A/B/C 系数 | `tractionrouteworkspace` |
| `tractionroutepresetpoint` | 每个速度—牵引力节点 | `tractionroutetrainpreset` |
| `tractionroutepresetbrakingpoint` | 每个速度—制动力节点 | `tractionroutetrainpreset` |
| `tractionroutepresetresistancepoint` | 每个速度—基本阻力节点 | `tractionroutetrainpreset` |
| `tractionrouteworkspacestop` | 每个停车位置及停留时间 | `tractionrouteworkspace` |
| `tractionrouteworkspacelimitpoint` | 每个允许速度锚点的顺序、位置（m）、限速（km/h）；首点为 0 m，每点控制到下一点前或进路终点的区间 | `tractionrouteworkspace` |
| `tractionrouteworkspaceoverride` | 进路、接续进路、起终节点、线路分段编号及标识，限速、坡度、附加阻力覆盖值 | `tractionrouteworkspace` |
| `tractionroutecalculation` | 最近计算对应的数据版本、保存时间、进路标识、输入标量快照和结果摘要 | `tractionrouteworkspace` |
| `tractionroutecalculationpoint` | 此次计算使用的牵引节点快照 | `tractionroutecalculation` |
| `tractionroutecalculationbrakingpoint` | 此次计算使用的制动节点快照 | `tractionroutecalculation` |
| `tractionroutecalculationresistancepoint` | 此次计算使用的基本阻力节点快照 | `tractionroutecalculation` |
| `tractionroutecalculationsegment` | 完整展开线路各段的起终里程、限速、坡度、附加阻力 | `tractionroutecalculation` |
| `tractionroutecalculationdevice` | 设备标识、名称、类别及位置 | `tractionroutecalculation` |
| `tractionroutecalculationjoint` | 输入绝缘标识、名称及位置 | `tractionroutecalculation` |
| `tractionroutecalculationcell` | 输入轨道电路范围及边界是否已知 | `tractionroutecalculation` |
| `tractionroutecalculationstop` | 此次计算使用的停车点快照 | `tractionroutecalculation` |
| `tractionroutecalculationsample` | 每个位置、速度、限速、车头时刻采样点 | `tractionroutecalculation` |
| `tractionroutecalculationjointtime` | 每个绝缘的车头、车尾及释放时刻 | `tractionroutecalculation` |
| `tractionroutecalculationcelltime` | 每个轨道电路的进入、出清、释放时刻 | `tractionroutecalculation` |
| `tractionroutecalculationwarning` | 每条计算警告代码 | `tractionroutecalculation` |

牵引、制动、基本阻力均按速度节点成行，计算统一使用分段线性插值。恒定制动力、Davis A/B/C 保留为生成表格的数值列，修改这些标量不会覆盖已有节点，须在弹框点击“生成并替换表格”。旧记录缺少制动或阻力表时，以原标量每 10 km/h 生成到最高速度并落库；迁移幂等，不覆盖已维护的表格和已有结果。缺失的进入、出清、释放时刻以及未设置的线路覆盖值存 SQL `NULL`，不补成零。线路覆盖的进路和分段标识分别存列，不将浏览器旧复合键当作数据文档保存。

## 保存与读取

- `GET /api/SpeedProfile/workspace/sets`：按实例、站场返回方案摘要和数据库中保存的当前选择；没有方案时初始化默认方案。
- `POST /api/SpeedProfile/workspace/sets`：新建方案；提供 `sourceId` 与源方案管理 `revision` 时，在同一事务中复制全部进路记录、参数和已有结果，不计算。
- `PUT /api/SpeedProfile/workspace/sets/name`：按 `calculationSetId` 与管理 `revision` 重命名；名称在站场内不重复。
- `PUT /api/SpeedProfile/workspace/sets/selection`：保存当前选择；不触发计算。
- `DELETE /api/SpeedProfile/workspace/sets`：按 `calculationSetId` 与管理 `revision` 删除参数和结果；至少保留一套，删除当前方案会原子切换到剩余方案。

以上写入均携带 `instanceId`、`stationSchemeId`；管理版本会在参数、结果或名称变更时递增，防止复制、改名、删除覆盖并发修改。后续工作区接口同时携带 `calculationSetId` 和 `routeCaseId`，所有读写以明确的进路记录为范围。

- `GET /api/SpeedProfile/workspace/routes`：返回方案内全部进路记录，包含主进路、接续进路、所选列车、结果状态和计算时间。
- `POST /api/SpeedProfile/workspace/routes/open`：按 routeId、continuationId 打开或新建记录，记住当前选择，不触发计算。
- `DELETE /api/SpeedProfile/workspace/routes`：按 routeCaseId 和记录 revision 删除一条进路的参数与结果，不影响兄弟记录或其他方案。
- `GET /api/SpeedProfile/workspace?instanceId=...&stationSchemeId=...&calculationSetId=...&routeCaseId=...`：读取工作区，无记录返回 204。
- `PUT /api/SpeedProfile/workspace`：提交 `{ instanceId, stationSchemeId, calculationSetId, routeCaseId, workspace }`；`workspace` 包含 `revision`、`catalog`、`settings`、`overrides`、`speedLimitPoints`。HTTP 使用 JSON 传输，服务拆分成上述关系行。
- `POST /api/SpeedProfile/workspace/calculate`：提交 `{ instanceId, stationSchemeId, calculationSetId, routeCaseId, revision, request }`。服务验证已保存参数，执行计算后，在一个事务中替换最近一次完整输入快照及结果，落库成功才返回成功。
- `GET /api/SpeedProfile/workspace/calculation`：读取最近计算的完整输入、结果、保存时间及 `isCurrent`。

每个站场允许 1–100 套具名牵引计算方案，每套可包含多条进路记录，每条记录独立保存工作区参数和最近一次结果；版本号用于并发校验。方案管理显示进路总数、已有结果数和待计算数，“方案进路结果”可查看、切换、删除单条记录。页面载入、刷新和切换站场时读取数据库中已有的计算快照，即使 `isCurrent=false` 也保留显示。修改参数只保存参数，不触发计算；输入或进路与快照不一致时显示“待计算”。只有点击“计算”才提交后端计算，并在成功落库后只替换当前进路记录的旧快照；无结果时显示空态，读取或计算失败均不自动重算。图表、摘要、占用明细和导出使用同一份已保存快照，避免混用新旧输入。结果与当前输入一致时才允许更新计算参数中的占用时间。

预设保存和选择直接调用数据库，其他参数修改后自动保存；“保存到数据库”可主动保存或重试。数据库读写失败保留当前草稿并给出重试入口，不回退到仅本机保存，也不将未落库的计算显示为成功。相同数据不重复写入，写入排队并携带最近确认的版本；409 冲突要求重新载入。参数、子表和结果各自按事务提交，故障不会留下半套数据。采样点按 250 行批量插入。

所有接口要求登录，并验证实例所有者或管理员及站场方案归属。保存时再次锁定方案、验证进路引用和版本。从站场删除进路会清除相关线路覆盖并让旧计算过期，保留历史进路标识及结果用于查看；删除方案或实例时同步清理全部牵引关系表。现有作业计划和仿真数据不会因保存牵引参数自动重算。

## 旧数据迁入与启用

升级服务后，启动过程自动建表，包括允许速度点子表。旧工作区无锚点行时，沿用原线路分段限速；编辑图中限速后逐点落库。旧版 16 张单套关系表先迁入各站场的“默认计算方案”（ID 为 `default`）；旧版每方案单结果数据再按主进路和接续进路迁入新表，包括所有采样点、输入快照和占用明细。当前草稿进路与上次结果进路不同的，拆为两条进路记录，历史结果恢复其计算时的输入，避免错误归属。每一步数据迁移均采用事务，失败时保留该步的来源数据，重复启动可继续迁移。成功后清空旧表行，保留空表结构作为升级兼容；重复启动不会重复迁移或覆盖新方案。新建、复制、改名、删除均不触发计算，复制保留原结果的计算时间及是否过期状态。

第一次打开没有数据库工作区的默认计算方案时，前端读取旧浏览器列车预设及 v1/v2 参数，保留合法特性节点、信号设置、停站和线路覆盖，写入数据库成功后移除对应旧浏览器记录。数据库已有工作区时优先读取数据库，避免另一台设备的旧数据覆盖它；迁移或保存失败时保留本机旧记录用于重试。无旧数据时建立三组可编辑的示例预设。

仅实现升级不会直接读取其他用户或设备的浏览器数据；旧数据在对应浏览器首次进入牵引页面时迁入。

## 验证

`SwitchYard.Traction.PersistenceTests` 使用独立临时 SQLite 文件，覆盖表结构、主外键、方案内多进路及接续组合隔离、复制全部进路、单记录删除、旧数据拆分迁移、完整参数与结果逐值读回、乐观锁、作用域隔离、非法引用、事务故障回滚、删除生命周期和 HTTP 权限。测试不连接业务数据库。前端 `test:traction` 覆盖多套方案 API 的目标与版本、选择不重算、串行保存、重复保存去重、冲突停止、失败重试和禁止无状态回退。MySQL 使用对应建表分支，实际 MySQL 环境需使用应用原有连接配置启动。

## 回填到计算参数的进路作业时间

`stationrouteoperationtime` 按 `InstanceID`、`StationSchemeID`、`CalculationParameterSetID`、`RouteID`、`TrainTypeID` 建复合主键，`OperationTimeS` 为非负数值秒（MySQL DOUBLE / SQLite REAL）。无记录表示未知，不用 0 代替。`SaveStationRouteTimes` 的 `operationTimeS` 与区段偏移在同一事务中保存；省略该字段保留已有值，显式 null 清空。GET/PUT 加 `includeOperationTime=true` 返回 `{ times, operationTimeS }`，未携带时保留原数组响应。参数集复制、删除以及进路、站场清理均处理此表，不采用 JSON 字段。
