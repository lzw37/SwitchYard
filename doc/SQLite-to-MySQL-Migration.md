# SwitchYard SQLite 到 MySQL 迁移说明与历史评估

## 当前实现与使用入口

当前程序已支持两套业务数据库的 SQLite / MySQL 连接，并由 `DatabaseSchemaInitializer` 执行驼峰和能力库各自的建表脚本。API 启动不会创建 MySQL 数据库，不会生成管理员或模板实例。配置与上线说明以 [开发指南](Getting-Started.md) 和 [部署说明](Deploy-Instruction.md) 为准。

仓库已有可运行的**驼峰数据库**迁移工具：

- 包装脚本：[`scripts/Migrate-SqliteToMySql.ps1`](../scripts/Migrate-SqliteToMySql.ps1)
- .NET 8 工具：[`tools/SQLiteToMySqlMigrator`](../tools/SQLiteToMySqlMigrator/Program.cs)
- 默认建表脚本：[`mysql-schema.sql`](../SwitchYard.WebApi/SwitchYard.Service/Database/mysql-schema.sql)
- 默认预检查：[`sqlite-precheck.sql`](../SwitchYard.WebApi/SwitchYard.Service/Database/sqlite-precheck.sql)

工具固定迁移下文列出的 20 张驼峰表，**不迁移 `CapacityDatabase`**；仅替换 schema 参数不能使其成为能力库迁移工具。默认 MySQL schema 与工具建库语句使用 `utf8mb4_0900_ai_ci`，目标实例需支持该排序规则。

工具只从指定 JSON 的 `HumpDatabase` 节读取默认连接信息，不加载 ASP.NET Core 的生产 JSON、环境变量或 `api.env`。创建自己的私有 JSON（例如被忽略的 `LocalData/migration.json`），填写 `HumpDatabase:SqlliteConfig:DatabaseFile` 与 `HumpDatabase:MysqlConfig` 的 Host、Port、Database、Username、Password，先用计划模式核对：

```powershell
# 从仓库根目录执行；该私有配置文件需预先准备。
./scripts/Migrate-SqliteToMySql.ps1 -ConfigPath ./LocalData/migration.json -DryRun
```

`-DryRun` 仅校验配置与本地文件路径并显示计划，不连接数据库、不执行预检查 SQL，也不证明迁移可成功。正式执行前先备份并停写，确认目标是专门的新库后再移除 `-DryRun`。

正式流程会尝试建库、执行预检查和 schema、清空目标表、事务导入数据、比较逐表行数，并在当前工作目录的 `migration-reports/` 写报告。**默认清空目标 20 张表，且清空发生在导入事务之外；导入失败不能自动恢复被清空的数据。** 预检查发现问题只警告，不会自动清洗或阻止后续迁移。

可用覆盖项为 `-SqlitePath`、`-MySqlHost`、`-MySqlPort`、`-MySqlDatabase`、`-MySqlUsername`、`-MySqlPassword`、`-SchemaPath`、`-PrecheckPath`。密码宜放在受保护的私有配置中，避免写入命令历史。跳过开关为 `-SkipSchemaInitialization`、`-SkipPrecheck`、`-SkipClearTarget`、`-SkipValidation`。`-SkipClearTarget` 是追加导入，不是增量同步；非空目标可能产生重复记录并导致行数校验失败。

迁移后需验证登录、模板 `001`、实例复制与计算数据；行数一致不能替代业务检查。切换 `HumpDatabase` 时不会同时迁移或切换能力库。

> 以下第 1–10 节保留早期迁移设计和当时样本库的检查记录。表行数、重复/孤儿数据数量不是当前运行库的统计；约束、字段改名等是设计建议，不代表现有 schema。已完成的关键改造在相应小节注明。

## 1. 结论摘要

当前 `DBConnector.GetDBConnector()` 会按所选数据库配置节在 SQLite 和 MySQL 间切换。改变配置不会迁移业务数据；需先完成独立目标库的导入和验证。

当前驼峰 MySQL schema 未添加主键、外键，相关脚本位于 `SwitchYard.WebApi/SwitchYard.Service/Database/mysql-schema.sql`；这一描述不适用于全部能力库表。

早期检查及当前对应状态：

1. 当前已有 schema 初始化、SQL 预检查、驼峰迁移工具与行数验证；版本化结构迁移和种子数据引导仍未提供。
2. 早期样本 SQLite 中曾发现重复键和孤儿数据；迁移实际数据前应重新运行检查。
3. 已修复下述车型作用域与计算结果参数化写入问题，其他兼容性仍需在目标实例验证。
4. 推荐采用“新建 MySQL 库并行验证 + 一次性切换”的迁移方式，不建议原地替换。

## 2. 早期代码与样本数据检查记录

### 2.1 代码入口

- 数据库切换入口：`SwitchYard.WebApi/SwitchYard.Service/DBConnector.cs`
- 配置入口：`SwitchYard.WebApi/SwitchYard.Service/appsettings.json`
- 启动初始化：`SwitchYard.WebApi/SwitchYard.Service/Program.cs`
- 当前自动建表入口：`SwitchYard.WebApi/SwitchYard.Service/Services/DatabaseSchemaInitializer.cs`

### 2.2 现有库表

当时的驼峰 SQLite 样本包含以下 20 张表（能力库另有 schema）：

- `user`
- `refreshtoken`
- `humpinstance`
- `slopeline`
- `position`
- `positionsegment`
- `switch`
- `retarder`
- `wagonconcept`
- `operationcondition`
- `humpscheme`
- `vposition`
- `vpositionsegment`
- `humpcalculation`
- `humpcalculationdata`
- `retarderstatus`
- `headwaycheckscheme`
- `headwaycheckwagon`
- `headwaycheckdata`
- `headwaycheckresult`

### 2.3 历史样本数据量

主要表记录数如下：

| 表 | 行数 |
| --- | ---: |
| `user` | 7 |
| `humpinstance` | 6 |
| `slopeline` | 26 |
| `position` | 209 |
| `positionsegment` | 184 |
| `switch` | 51 |
| `retarder` | 36 |
| `wagonconcept` | 25 |
| `operationcondition` | 21 |
| `humpscheme` | 20 |
| `vposition` | 133 |
| `vpositionsegment` | 114 |
| `humpcalculation` | 53 |
| `humpcalculationdata` | 363 |
| `retarderstatus` | 73 |
| `headwaycheckscheme` | 19 |
| `headwaycheckwagon` | 45 |
| `headwaycheckdata` | 0 |
| `headwaycheckresult` | 0 |
| `refreshtoken` | 16 |

### 2.4 数据质量问题

发现的关键问题如下：

1. 重复键
   - `position.ID` 存在重复
   - `positionsegment.ID` 存在重复
   - `switch.ID` 存在重复
   - `retarder.ID` 存在重复

2. 孤儿数据
   - `slopeline -> humpinstance` 存在 11 条孤儿
   - `operationcondition -> humpinstance` 存在 7 条孤儿
   - `humpscheme -> humpinstance` 存在 6 条孤儿
   - `position -> slopeline` 存在 1 条孤儿
   - `switch -> positionsegment` 存在 42 条孤儿
   - `headwaycheckscheme -> humpscheme` 存在 1 条孤儿

3. 现有表约束非常弱
   - 除 `refreshtoken.token` 外，绝大多数表没有明确主键
   - 基本没有外键
   - 基本没有索引

4. 现有业务逻辑已暴露出“应为复合键”的迹象
   - `position.ID`、`positionsegment.ID` 这样的值明显在不同实例/不同线路下重复出现
   - 说明这些表在业务上更接近“父级作用域内唯一”，而不是“全局唯一”

### 2.5 早期风险与当前状态

1. 已增加 `DatabaseSchemaInitializer` 和四份业务 schema；旧表字段变更仍需单独核对。
2. `HumpController` 的计算结果写入已改用参数化 `INSERT`。
3. `wagonconcept` 的更新、删除及读取已按 `InstanceID` 与 `TypeName` 限定作用域。
4. 表名 `user`、`switch` 建议统一做转义或重命名，避免与数据库关键字/系统对象语义冲突。
5. `RefreshTokenService` 现在把时间字段以字符串方式存库，MySQL 目标模型应统一成 `DATETIME(6)` 或 `TIMESTAMP`。

## 3. 推荐迁移策略

推荐采用“三阶段迁移”：

### 阶段 A：先做兼容性改造

目标：让程序同时兼容 SQLite 与 MySQL，并且把未来会卡住的数据问题提前暴露。

### 阶段 B：建立 MySQL 新库并导数验证

目标：不影响现网 SQLite，先把 MySQL 跑通并验证数据、接口和前端功能。

### 阶段 C：择机切换生产配置

目标：在短暂停写窗口内完成最终增量迁移与配置切换，保留 SQLite 回滚路径。

## 4. 目标库设计建议

### 4.1 MySQL 版本建议

- 推荐：MySQL 8.0.x
- 字符集：`utf8mb4`
- 排序规则：`utf8mb4_0900_ai_ci`
- 存储引擎：`InnoDB`
- 时区：统一 UTC 存储，应用层负责展示时区转换

### 4.2 主键设计建议

建议优先按“现有业务作用域”设计键，而不是机械照搬 SQLite 列定义。

| 表 | 建议主键/唯一键 |
| --- | --- |
| `user` | 主键 `id`，唯一键 `name` |
| `refreshtoken` | 主键 `token`，索引 `userid, expires` |
| `humpinstance` | 主键 `id` |
| `slopeline` | 主键 `id`，索引 `instance_id` |
| `position` | 复合主键 `(instance_id, slope_line_id, id)` |
| `positionsegment` | 复合主键 `(instance_id, slope_line_id, id)` |
| `switch` | 复合主键 `(instance_id, slope_line_id, id)`，建议表名改为 `switch_device` |
| `retarder` | 复合主键 `(instance_id, slope_line_id, id)` |
| `wagonconcept` | 复合主键 `(instance_id, type_name)` |
| `operationcondition` | 主键 `id`，索引 `instance_id` |
| `humpscheme` | 主键 `id`，索引 `instance_id` |
| `vposition` | 复合主键 `(instance_id, hump_scheme_id, id)` |
| `vpositionsegment` | 复合主键 `(instance_id, hump_scheme_id, id)` |
| `humpcalculation` | 主键 `id`，索引 `(instance_id, hump_scheme_id)` |
| `humpcalculationdata` | 复合主键 `(instance_id, hump_scheme_id, hump_calculation_id, x)` |
| `retarderstatus` | 复合主键 `(instance_id, hump_calculation_id, retarder_id)` |
| `headwaycheckscheme` | 主键 `id`，索引 `instance_id` |
| `headwaycheckwagon` | 复合主键 `(instance_id, headway_check_id, sequence)` |
| `headwaycheckdata` | 建议复合主键 `(instance_id, headway_check_id, sequence, x)` |
| `headwaycheckresult` | 建议复合主键 `(instance_id, headway_check_id, equipment_type, equipment_id)` |

### 4.3 字段类型建议

| 现状 | MySQL 建议 |
| --- | --- |
| `VARCHAR(50)` | 保留 `VARCHAR(50)`，确有超长风险的字段再放宽 |
| `REAL` | 改 `DOUBLE` |
| `TINYINT/INTEGER` 表示布尔 | 改 `TINYINT(1)` |
| `DATETIME`/文本时间混用 | 统一 `DATETIME(6)` |
| `TEXT` token | 保留 `VARCHAR(128)` 或 `TEXT`，优先 `VARCHAR(128)` |

## 5. 早期代码改造清单及状态

以下保留设计方向；已实现项目标注状态，剩余建议不代表当前必需的上线前置条件。

### 5.1 抽出完整 schema 初始化

已实现独立的 `DatabaseSchemaInitializer` 并在启动调用。以下迁移管理、约束与种子数据职责属于后续设计：

- `DatabaseSchemaInitializer`
- `DatabaseMigrationRunner`

职责：

1. 按数据库类型初始化全部表结构
2. 创建索引
3. 创建外键
4. 初始化默认管理员或种子数据

不要继续把建表逻辑分散在各个业务服务中。

### 5.2 修正 SQL 兼容性

1. 把所有字符串拼接 SQL 改成参数化 SQL。
2. 批量插入改成循环参数写入，或显式 bulk 导入。
3. 避免依赖 SQLite 的弱类型和宽松比较行为。

重点文件：

- `SwitchYard.WebApi/SwitchYard.Service/Controllers/HumpController.cs`
- `SwitchYard.WebApi/SwitchYard.Service/Services/RefreshTokenService.cs`
- `SwitchYard.WebApi/SwitchYard.Service/Services/UserService.cs`

### 5.3 修正作用域查询 bug

此项已完成，`wagonconcept` 的作用域条件为：

- `WHERE InstanceID = @InstanceID AND TypeName = @TypeName`

该条件避免多个实例下车型同名时误删、误改。

### 5.4 统一命名策略

建议在 MySQL 迁移时同步完成一次列名与表名规范化：

- 表名统一小写下划线，或保留当前小写名称但加反引号
- 字段名统一小写下划线
- 代码中用 Dapper 别名做兼容映射

最少也应处理：

- `user`
- `switch`

### 5.5 改善连接串与安全配置

当前连接器已支持以下配置键：

- `Host`
- `Port`
- `Database`
- `Username`
- `Password`
- `SslMode`
- `CharSet`
- `AllowPublicKeyRetrieval`
- `ConnectionTimeout`

同时把生产密码移出 `appsettings.json`，改为环境变量或密钥管理。

## 6. 数据清洗方案

迁移前必须先清洗 SQLite 数据，否则 MySQL 约束一上就会失败。

### 6.1 清洗原则

1. 先保留业务正确性，再补约束。
2. 对于“应在父级范围内唯一”的表，按复合键迁移，不强制改全局 ID。
3. 对于孤儿数据，优先判断是补父记录还是删除子记录。
4. 所有清洗动作必须可审计，输出清洗日志和映射表。

### 6.2 建议处理方式

| 问题 | 处理建议 |
| --- | --- |
| `position.ID` 重复 | 采用复合主键，不做强制改号 |
| `positionsegment.ID` 重复 | 采用复合主键，不做强制改号 |
| `switch.ID` 重复 | 采用复合主键，不做强制改号 |
| `retarder.ID` 重复 | 采用复合主键，不做强制改号 |
| `slopeline` 孤儿 | 若所属 `humpinstance` 已废弃则删除；否则补父记录 |
| `operationcondition`/`humpscheme` 孤儿 | 同上 |
| `switch -> positionsegment` 孤儿 | 需要逐条核查，优先按布局补齐；无法恢复则删除 |
| `headwaycheckscheme` 孤儿 | 删除或映射到正确 `humpscheme` |

### 6.3 清洗产物

建议输出以下文件：

- `precheck-report.json`
- `cleanup-actions.sql`
- `id-remap.csv`，如后续某些对象必须重编号

## 7. 实施步骤

### 步骤 1：冻结窗口准备

1. 备份当前 SQLite：`hump.db`
2. 导出应用版本号、提交号、配置文件
3. 明确切换窗口与回滚负责人

### 步骤 2：核对代码兼容改造

1. 核对已有 schema initializer 与目标表结构
2. 回归已修复的 `wagonconcept` 查询范围
3. 核对参数化写入与数据库兼容性
4. 核对已有 MySQL 初始化脚本
5. 本地同时验证 SQLite 与 MySQL 两种配置

### 步骤 3：准备 MySQL 目标库

1. 创建数据库
2. 执行 DDL
3. 执行索引和外键脚本
4. 建立迁移专用账号，只授予目标库权限

### 步骤 4：执行 SQLite 预检查

执行下列检查：

1. 重复键检查
2. 孤儿数据检查
3. 空值检查
4. 时间字段格式检查
5. 文本长度超限检查

### 步骤 5：清洗数据

1. 先在 SQLite 副本上做清洗
2. 保留清洗前后差异报告
3. 业务方抽样确认关键实例

### 步骤 6：全量迁移

推荐流程：

1. 优先使用文首说明的现有驼峰迁移工具逐表读取 SQLite
2. 按依赖顺序导入 MySQL
3. 导入顺序建议：
   - `user`
   - `humpinstance`
   - `slopeline`
   - `position`
   - `positionsegment`
   - `switch`
   - `retarder`
   - `wagonconcept`
   - `operationcondition`
   - `humpscheme`
   - `vposition`
   - `vpositionsegment`
   - `humpcalculation`
   - `humpcalculationdata`
   - `retarderstatus`
   - `headwaycheckscheme`
   - `headwaycheckwagon`
   - `headwaycheckdata`
   - `headwaycheckresult`
   - `refreshtoken`

### 步骤 7：验证

至少执行以下验证：

1. 每张表行数对比
2. 关键实例的布局数据对比
3. 登录、刷新 token、创建实例、复制实例、编辑溜放线、执行计算、保存计算结果
4. 管理员增删改用户
5. 前端典型路径回归

### 步骤 8：生产切换

1. 进入短暂停写窗口
2. 停止应用写入
3. 从 SQLite 做最终增量补录
4. 修改 `DatabaseType` 为 `MySQL`
5. 切换连接配置
6. 启动应用并执行冒烟测试

### 步骤 9：观察期

1. 监控连接数、慢 SQL、错误日志
2. 对比 API 响应时间
3. 保留 SQLite 只读备份至少 7 到 14 天

## 8. 回滚方案

一旦出现以下情况，应立即回滚：

1. 登录或 token 刷新异常
2. 关键实例加载失败
3. 计算结果保存失败
4. 大面积 5xx 或明显性能退化

回滚步骤：

1. 停止当前应用
2. 将 `DatabaseType` 改回 `Sqllite`
3. 恢复原 SQLite 配置路径
4. 重新启动服务
5. 保留 MySQL 故障现场供排查

## 9. 早期建议的交付物

其中 schema、预检查和迁移工具已在仓库提供；清洗、验证记录与回滚方案需结合实际数据准备：

1. `mysql-schema.sql`
2. `sqlite-precheck.sql`
3. `sqlite-cleanup.sql`
4. `sqlite-to-mysql-migrator` 小工具或脚本
5. `migration-verification.md`
6. `rollback-runbook.md`

## 10. 早期实施顺序建议

如果目标是“尽快切过去”，最稳妥的顺序是：

1. 先做代码兼容改造
2. 再做 SQLite 数据清洗
3. 核对仓库中的 MySQL schema 和迁移工具
4. 先在测试库完整跑通一次
5. 最后再切生产

如果后续增加严格约束或改动模型，而未先验证兼容性和数据质量，应重点检查以下风险：

1. 主键/唯一键冲突
2. 外键冲突
3. 同名车型跨实例误更新
4. 批量计算结果写入异常
5. 生产回滚成本升高
