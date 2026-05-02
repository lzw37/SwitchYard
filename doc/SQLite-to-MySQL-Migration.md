# SwitchYard SQLite 到 MySQL 迁移方案

## 1. 结论摘要

当前项目已经在连接层预留了数据库类型切换能力，`DBConnector.GetDBConnector()` 会按 `HumpDatabase:DatabaseType` 在 SQLite 和 MySQL 间切换，说明迁移方向是正确的；但现状还不满足“只改配置即可切换”的条件。

本仓库当前已落地的修复版本，按你的要求采用了“`MySQL` 不加主键、不加外键”的实现方式，相关脚本位于 `SwitchYard.WebApi/SwitchYard.Service/Database/mysql-schema.sql`。

本次检查后的结论是：

1. 代码层只完成了“连接器切换”，没有完成“MySQL 可落地的建库、建表、初始化、数据约束、数据迁移工具链”。
2. 现有 SQLite 库中存在重复键和孤儿数据，不能直接带严格主外键约束导入 MySQL。
3. 现有部分 SQL 写法依赖 SQLite 的宽松特性，迁移前应先做一轮兼容性改造。
4. 推荐采用“新建 MySQL 库并行验证 + 一次性切换”的迁移方式，不建议原地替换。

## 2. 本次代码与数据检查结果

### 2.1 代码入口

- 数据库切换入口：`SwitchYard.WebApi/SwitchYard.Service/DBConnector.cs`
- 配置入口：`SwitchYard.WebApi/SwitchYard.Service/appsettings.json`
- 启动初始化：`SwitchYard.WebApi/SwitchYard.Service/Program.cs`
- 自动建表逻辑：`SwitchYard.WebApi/SwitchYard.Service/Services/RefreshTokenService.cs`

### 2.2 现有库表

SQLite 当前包含 20 张表：

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

### 2.3 当前数据量

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

### 2.5 当前代码里的 MySQL 迁移风险点

1. 只有 `refreshtoken` 有自动建表逻辑，其他业务表依赖现成 SQLite 文件，MySQL 侧没有完整 schema 初始化能力。
2. `HumpController.ExecuteEnergyHeightCalculation()` 里仍在用字符串拼接批量 `INSERT`，迁移到 MySQL 前应改成参数化写入。
3. `wagonconcept` 的更新/删除逻辑仅按 `TypeName` 查询和删除，没有带 `InstanceID`，在 MySQL 严格建模后这是明显 bug。
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

## 5. 必做代码改造清单

建议在真正迁移数据前先完成下面几项代码修改。

### 5.1 抽出完整 schema 初始化

新增独立的数据库初始化服务，例如：

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

`wagonconcept` 当前有按 `TypeName` 全局更新/删除的逻辑，必须改为：

- `WHERE InstanceID = @InstanceID AND TypeName = @TypeName`

否则多个实例下车型同名时会误删、误改。

### 5.4 统一命名策略

建议在 MySQL 迁移时同步完成一次列名与表名规范化：

- 表名统一小写下划线，或保留当前小写名称但加反引号
- 字段名统一小写下划线
- 代码中用 Dapper 别名做兼容映射

最少也应处理：

- `user`
- `switch`

### 5.5 改善连接串与安全配置

建议把 MySQL 连接配置扩展为：

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

### 步骤 2：完成代码兼容改造

1. 完成 schema initializer
2. 修正 `wagonconcept` 查询范围
3. 移除字符串拼接 SQL
4. 补充 MySQL 初始化脚本
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

1. 从 SQLite 导出为 CSV 或通过中间脚本逐表读取
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

## 9. 建议的交付物

建议把迁移实施拆成以下几个交付件：

1. `mysql-schema.sql`
2. `sqlite-precheck.sql`
3. `sqlite-cleanup.sql`
4. `sqlite-to-mysql-migrator` 小工具或脚本
5. `migration-verification.md`
6. `rollback-runbook.md`

## 10. 对本项目的最终建议

如果目标是“尽快切过去”，最稳妥的顺序是：

1. 先做代码兼容改造
2. 再做 SQLite 数据清洗
3. 再生成 MySQL schema 和迁移脚本
4. 先在测试库完整跑通一次
5. 最后再切生产

如果跳过第 1、2 步，直接把 SQLite 数据导进 MySQL，大概率会在以下地方出问题：

1. 主键/唯一键冲突
2. 外键冲突
3. 同名车型跨实例误更新
4. 批量计算结果写入异常
5. 生产回滚成本升高
