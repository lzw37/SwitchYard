# 牵引计算关系存储

牵引计算使用通过能力数据库（`CapacityDatabase`），兼容 SQLite 和 MySQL。服务启动时 `TractionSchemaMigration.Ensure` 幂等建立以下现行关系表（另保留空的旧表用于升级兼容），不创建 JSON、JSONB、BLOB 或承载整块参数文档的字段。文本列仅保存名称、标识、类型、时间戳和单条警告；数值、边界是否已知等分别保存为数值列。

## 表与关系

计算方案以 `InstanceID`、`StationSchemeID`、`CalculationSetID` 为组合主键。方案下的进路记录增加 `RouteCaseID`，通过四列作用域独立保存参数与结果；同一方案内 `(RouteID, ContinuationID)` 唯一，所以主进路单独计算与主进路接续计算分别保留。有序子表增加 `OrderIndex`，预设增加 `PresetID`。子表通过组合外键引用父表，配置级删除采用事务内显式清理，并定义 `ON DELETE CASCADE`。

| 表 | 内容 | 父表 |
| --- | --- | --- |
| `tractioncalculationset` | 计算方案名称、管理版本、创建和更新时间 | 应用校验实例及站场存在 |
| `tractionsetselection` | 每个实例、站场当前选中的 CalculationSetID；主键为 InstanceID、StationSchemeID | `tractioncalculationset` |
| `tractionroutecase` | 主进路、接续进路、记录版本、是否为方案当前选择 | `tractioncalculationset` |
| `tractionrouteworkspace` | 数据版本、选中预设及进路、距离标尺、缺省限速、信号参数、作业模式、边界速度、计算精度 | `tractionroutecase` |
| `tractionprojecttrains` | 每个项目（InstanceID）唯一的列车参数库版本、默认预设选择 | 应用校验实例存在 |
| `tractionprojecttrainpreset` | 项目内每组列车的名称与物理标量参数 | `tractionprojecttrains` |
| `tractionprojecttrainpoint` | 项目列车的速度—牵引力节点 | `tractionprojecttrainpreset` |
| `tractionprojecttrainbrakingpoint` | 项目列车的速度—制动力节点 | `tractionprojecttrainpreset` |
| `tractionprojecttrainresistancepoint` | 项目列车的速度—基本阻力节点 | `tractionprojecttrainpreset` |
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

## 项目共用列车参数

列车定义只按 `InstanceID` 存一份，跨车站、牵引计算方案和进路共用。进路工作区仅持有所选 `SelectedPresetID`，API 读取时组合共享定义及进路选择；`catalogRevision` 校验项目库版本，工作区保存不能修改共享定义。项目库最多 1000 个预设。历史计算继续保存当时实际使用的列车输入快照，避免参数修改改变历史曲线。

首次访问时，在项目锁和事务内收集该项目所有旧进路/方案预设，合并名称、参数相同的定义；同名或同 ID 但参数不同的记录保留并自动追加名称后缀及分配新 ID，重映射原进路选择。迁移成功后清空旧可编辑预设子表，保留历史计算输入和结果；重复执行不新增记录。修改某个列车的物理参数只递增使用该列车的工作区版本，使相关旧结果待计算；仅重命名不使结果过期。删除正在使用的列车时，相关进路改选项目默认列车并递增版本。不同项目隔离，删除车站保留项目参数库，删除项目清理参数库。

MySQL 事务先用 `INSERT ... ON DUPLICATE KEY UPDATE Revision=Revision` 一次取得项目排他锁，再锁定车站；重复访问不会改动项目版本和默认选择。不要先 `INSERT IGNORE` 再 `SELECT ... FOR UPDATE`：重复键共享锁在并发请求中升级为排他锁会死锁（[InnoDB 加锁规则](https://dev.mysql.com/doc/refman/8.4/en/innodb-locks-set.html)）。收到 MySQL 1213 后回滚整个事务，从项目锁开始重新执行并检查版本，最多尝试三次；其他数据库错误及业务版本冲突不重试。

- `GET /api/SpeedProfile/workspace/trains?instanceID=...&stationSchemeID=...`：按项目读取 `{ instanceID, revision, catalog }`，站场用于权限/归属检查，无库时返回 204。
- `PUT /api/SpeedProfile/workspace/trains`：提交 `{ instanceID, stationSchemeID, revision, catalog }`，在项目锁内验证版本并保存共享定义、节点及受影响的进路版本。旧版本返回 409，禁止旧工作区或批量草稿覆盖新库。

## 保存与读取

- `GET /api/SpeedProfile/workspace/sets`：按实例、站场返回方案摘要和数据库中保存的当前选择；没有方案时初始化默认方案。
- `POST /api/SpeedProfile/workspace/sets`：新建方案；提供 `sourceId` 与源方案管理 `revision` 时，在同一事务中复制全部进路记录、参数和已有结果，不计算。
- `PUT /api/SpeedProfile/workspace/sets/name`：按 `calculationSetId` 与管理 `revision` 重命名；名称在站场内不重复。
- `PUT /api/SpeedProfile/workspace/sets/selection`：保存当前选择；不触发计算。
- `DELETE /api/SpeedProfile/workspace/sets`：按 `calculationSetId` 与管理 `revision` 删除参数和结果；至少保留一套，删除当前方案会原子切换到剩余方案。

以上写入均携带 `instanceId`、`stationSchemeId`；管理版本会在参数、结果或名称变更时递增，防止复制、改名、删除覆盖并发修改。后续工作区接口同时携带 `calculationSetId` 和 `routeCaseId`，所有读写以明确的进路记录为范围。

- `GET /api/SpeedProfile/workspace/routes`：返回方案内全部进路记录，包含主进路、接续进路、所选列车、结果状态和计算时间。
- `POST /api/SpeedProfile/workspace/routes/open`：按 routeId、continuationId 打开或新建记录，默认记住当前选择，不触发计算。批量计算传入 `select: false`，创建或取得记录时保留当前所选进路。
- `DELETE /api/SpeedProfile/workspace/routes`：按 routeCaseId 和记录 revision 删除一条进路的参数与结果，不影响兄弟记录或其他方案。
- `GET /api/SpeedProfile/workspace?instanceId=...&stationSchemeId=...&calculationSetId=...&routeCaseId=...`：读取工作区，无记录返回 204。
- `PUT /api/SpeedProfile/workspace`：提交 `{ instanceId, stationSchemeId, calculationSetId, routeCaseId, workspace }`；`workspace` 包含 `revision`、`catalogRevision`、`catalog`、`settings`、`overrides`、`speedLimitPoints`。HTTP 使用 JSON 传输，服务拆分成上述关系行。
- `POST /api/SpeedProfile/workspace/calculate`：提交 `{ instanceId, stationSchemeId, calculationSetId, routeCaseId, revision, request }`。服务验证已保存参数，执行计算后，在一个事务中替换最近一次完整输入快照及结果，落库成功才返回成功。
- `GET /api/SpeedProfile/workspace/calculation`：读取最近计算的完整输入、结果、保存时间及 `isCurrent`。

每个站场允许 1–100 套具名牵引计算方案，每套可包含多条进路记录，每条记录独立保存列车选择、运行及线路参数和最近一次结果，列车定义统一引用项目参数库；版本号用于并发校验。方案管理显示进路总数、已有结果数和待计算数，“方案进路结果”可查看、切换、删除单条记录，也可勾选多条进路批量计算。页面载入、刷新和切换站场时读取数据库中已有的计算快照，即使 `isCurrent=false` 也保留显示。修改参数只保存参数，不触发计算；输入或进路与快照不一致时显示“待计算”。点击“计算”或“批量计算”才提交后端计算，并在成功落库后只替换对应进路记录的旧快照；无结果时显示空态，读取或计算失败均不自动重算。图表、摘要、占用明细和导出使用同一份已保存快照，避免混用新旧输入。结果与当前输入一致时才允许更新计算参数中的占用时间。

批量计算在客户端依次调用已有的进路、工作区保存和计算接口，每条进路独立校验版本并提交。编辑批量参数只产生弹窗内草稿；点击批量计算后固定本次输入，依次保存所选进路的参数及结果。计算失败保留该进路上一次结果，已保存的新参数仍可重试；某条失败不影响后续进路。停止只取消尚未执行的进路，当前进路完成保存后结束。409 冲突要求重载该进路参数后再编辑、重试。批量计算保留工作区当前选择，并刷新方案结果列表；它不会自动写入能力计算参数集中的轨道电路占用偏移。

“已有结果”的“带入计算参数”是独立的显式写入操作：读取所选记录的工作区和结果，确认版本、输入和当前线路一致后，复用占用偏移换算；再读取目标参数集各进路的原占用参数，保留未知与其他已配置行，并通过 `SaveStationRouteTimes?includeOperationTime=true` 同时保存作业时间。每条进路独立提交并确认响应，已完成部分通过能力计算刷新通知同步。多个所选记录涉及同一进路时，相同参数去重，不同参数在首次写入前报错，避免覆盖。

终点实际速度为 0 且区段仍与车身范围重叠时，占用结束采用首次到达终点的停车时刻，不包含同位置的后续停时，也不追加释放延迟。各作业类型均适用，按该时刻减每条进路自身的结束参考生成结束偏移。此规则同时用于占用图、摘要、明细、单条更新和结果批量带入；读取已有快照即可应用，不修改物理出清与释放的空值，不需要增加存储列。

若某条进路所有区段的占用时刻都不完整，仍可带入其已知作业时间。此时请求 `SaveStationRouteTimes?includeOperationTime=true&operationTimeOnly=true`，必须显式提供 `operationTimeS`，只更新目标参数集、进路和列车类型的作业时间，保留全部占用行；无占用行时也不创建虚构行。默认保存接口仍要求非空占用行并同时更新作业时间与占用参数。

项目预设维护和进路列车选择分别保存，其他参数修改后自动保存；“保存到数据库”可主动保存或重试。数据库读写失败保留当前草稿并给出重试入口，不回退到仅本机保存，也不将未落库的计算显示为成功。相同数据不重复写入，写入排队并携带最近确认的版本；409 冲突要求重新载入。参数、子表和结果各自按事务提交，故障不会留下半套数据。采样点按 250 行批量插入。

所有接口要求登录，并验证实例所有者或管理员及站场方案归属。保存时再次锁定方案、验证进路引用和版本。从站场删除进路会清除相关线路覆盖并让旧计算过期，保留历史进路标识及结果用于查看；删除方案或实例时同步清理全部牵引关系表。现有作业计划和仿真数据不会因保存牵引参数自动重算。

## 旧数据迁入与启用

制动段及一离去最高速度分别使用 `RunBrakingMaxSpeedKmh`、`RunDepartureSectionMaxSpeedKmh` 保存工作区设置，计算快照采用相应 `Input` 前缀数值列。四个表中的列均可空，启动时幂等补列，旧值默认 NULL 表示使用列车最高速度；读取、版本匹配和方案复制保留空值与显式速度的区别。

可选制动距离使用独立可空数值列 `RunBrakingDistanceM`、`InputBrakingDistanceM`，分别保存工作区输入和计算输入快照，同时升级 `tractionset`、`tractionroute` 表。NULL 代表未输入、自动计算；0 和正值均为显式输入。服务启动幂等补列，历史记录保持 NULL；保存、读取和复制保持此区别。手动距离直接从输入快照恢复，自动距离继续根据已保存结果范围还原，不重算旧结果。

发车和通过的“一离去”长度以独立数值列保存：工作区 `RunDepartureSectionLengthM`、输入快照 `InputDepartureSectionLengthM`，同时兼容旧版 `tractionset` 和现行 `tractionroute` 表。服务启动幂等补列，历史数据默认 0，不重算既有结果；复制方案逐列保留长度。结果读取时发车的 `approachDistanceM` 固定为 0，`departureSectionLengthM` 根据已保存输入及实际追加的区段恢复，通过模式的制动距离从结果全长扣除原进路和一离去长度后恢复。旧通过快照即使含未生效的正长度，仍按原区段和范围读取；追加电路的占用时刻按结果子表逐行保存。

升级服务后，启动过程自动建表，包括允许速度点子表。旧工作区无锚点行时，沿用原线路分段限速；编辑图中限速后逐点落库。旧版 16 张单套关系表先迁入各站场的“默认计算方案”（ID 为 `default`）；旧版每方案单结果数据再按主进路和接续进路迁入新表，包括所有采样点、输入快照和占用明细。当前草稿进路与上次结果进路不同的，拆为两条进路记录，历史结果恢复其计算时的输入，避免错误归属。每一步数据迁移均采用事务，失败时保留该步的来源数据，重复启动可继续迁移。成功后清空旧表行，保留空表结构作为升级兼容；重复启动不会重复迁移或覆盖新方案。新建、复制、改名、删除均不触发计算，复制保留原结果的计算时间及是否过期状态。

第一次打开没有数据库工作区的默认计算方案时，前端读取旧浏览器列车预设及 v1/v2 参数，保留合法特性节点、信号设置、停站和线路覆盖，写入数据库成功后移除对应旧浏览器记录。数据库已有工作区时优先读取数据库，避免另一台设备的旧数据覆盖它；迁移或保存失败时保留本机旧记录用于重试。无旧数据时建立三组可编辑的示例预设。

仅实现升级不会直接读取其他用户或设备的浏览器数据；旧数据在对应浏览器首次进入牵引页面时迁入。

## 验证

`SwitchYard.Traction.PersistenceTests` 使用独立临时 SQLite 文件，覆盖表结构、主外键、方案内多进路及接续组合隔离、复制全部进路、单记录删除、旧数据拆分迁移、完整参数与结果逐值读回、乐观锁、作用域隔离、非法引用、事务故障回滚、删除生命周期和 HTTP 权限。测试不连接业务数据库。前端 `test:traction` 覆盖多套方案 API 的目标与版本、选择不重算、串行保存、重复保存去重、冲突停止、失败重试和禁止无状态回退。MySQL 使用对应建表分支，实际 MySQL 环境需使用应用原有连接配置启动。

## 回填到计算参数的进路作业时间

`stationrouteoperationtime` 按 `InstanceID`、`StationSchemeID`、`CalculationParameterSetID`、`RouteID`、`TrainTypeID` 建复合主键，`OperationTimeS` 为非负数值秒（MySQL DOUBLE / SQLite REAL）。无记录表示未知，不用 0 代替。`SaveStationRouteTimes` 的 `operationTimeS` 与区段偏移在同一事务中保存；省略该字段保留已有值，显式 null 清空。GET/PUT 加 `includeOperationTime=true` 返回 `{ times, operationTimeS }`，未携带时保留原数组响应。参数集复制、删除以及进路、站场清理均处理此表，不采用 JSON 字段。
