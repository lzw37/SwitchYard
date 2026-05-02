# SwitchYard 运维建议

## 1. 当前程序的运行画像

结合当前代码与部署脚本，这套程序的生产运行方式大致如下：

- 应用主体是 .NET 8 Web API，使用 `systemd` 托管，服务名为 `switchyard-api`。
- API 默认通过 Kestrel 监听 `http://127.0.0.1:7297`，设计上期望前面再放 Nginx/Caddy/Apache 做公网入口和 TLS 终止。
- 生产敏感配置通过 `/etc/switchyard/api.env` 注入，部署脚本会创建独立用户 `switchyard` 并下发 `systemd` 单元。
- 日志使用 Serilog，同时输出到控制台和 `/opt/switchyard/api/logs`。
- 默认数据库是 MySQL，也兼容 SQLite；应用启动时会自动执行 SQL 脚本确保表存在。
- 鉴权使用 JWT + Refresh Token；Refresh Token 会持久化到数据库。
- 课程视频/文档通过本地目录直接对外提供下载与流式访问，默认目录是 `/data/switchyardvid`。

## 2. 优先级最高的运维事项

### P0：立即处理

#### 2.1 立刻轮换已经出现在仓库中的密钥和数据库口令

当前开发配置里仍然存在明文密钥和数据库账号：

- `Jwt:SecretKey` 明文存在于 [appsettings.json](/d:/SwitchYard/SwitchYard.WebApi/SwitchYard.Service/appsettings.json:29)
- MySQL 用户名和密码明文存在于 [appsettings.json](/d:/SwitchYard/SwitchYard.WebApi/SwitchYard.Service/appsettings.json:36)

建议：

- 立即更换 JWT 签名密钥。
- 立即更换数据库密码。
- 如果生产库还在使用 `root`，改为专用业务账号，只授予当前库的最小权限。
- 后续仅通过 `/etc/switchyard/api.env` 或密钥管理系统注入，不再在任何 `appsettings*.json` 中保留真实密钥。

#### 2.2 生产环境只暴露反向代理，不直接暴露 Kestrel

当前部署设计本身是合理的，生产配置也默认只监听回环地址：

- 生产默认监听 `127.0.0.1:7297`，见 [appsettings.Production.json](/d:/SwitchYard/SwitchYard.WebApi/SwitchYard.Service/appsettings.Production.json:27)
- 安装脚本默认写入 `WebApi__Hosts__0=http://127.0.0.1:7297`，见 [install-secrets.sh](/d:/SwitchYard/scripts/deploy/install-secrets.sh:125)

建议：

- 服务器安全组、防火墙仅开放 `80/443` 给公网。
- `7297` 仅本机访问，不对公网开放。
- 反向代理必须传递 `X-Forwarded-For` 和 `X-Forwarded-Proto`，否则 `UseHttpsRedirection()` 和真实 IP 识别会失真。

#### 2.3 收紧受信任代理范围，不要直接信任整个内网段

当前程序在生产环境会信任 RFC1918 私网段：

- 见 [Program.cs](/d:/SwitchYard/SwitchYard.WebApi/SwitchYard.Service/Program.cs:167)

这意味着只要请求来源落在这些网段内，转发头就可能被接受。对单机反向代理场景来说，这个范围偏大，容易影响：

- 登录限流的真实 IP 识别
- 审计日志中的客户端 IP
- 未来接入其他内网代理时的边界控制

建议：

- 明确写死反向代理所在主机 IP，优先使用 `KnownProxies`。
- 如果必须信任网段，也尽量缩小到实际网段，而不是整个 `10/8`、`172.16/12`、`192.168/16`。

#### 2.4 建立数据库备份与恢复演练

当前应用会在启动时“补齐表”，但这不是正式迁移体系：

- 启动时自动执行 schema 脚本，见 [Program.cs](/d:/SwitchYard/SwitchYard.WebApi/SwitchYard.Service/Program.cs:436)
- 实现方式见 [DatabaseSchemaInitializer.cs](/d:/SwitchYard/SwitchYard.WebApi/SwitchYard.Service/Services/DatabaseSchemaInitializer.cs:18)

这说明数据库结构管理目前更偏“初始化”而不是“变更迁移”。因此备份与回滚的重要性更高。

建议：

- 至少每日一次 `mysqldump --single-transaction` 逻辑备份。
- 备份保留建议采用 `7 天日备 + 4 周周备 + 3 个月月备`。
- 每月至少做一次恢复演练，验证能否在新实例成功恢复并启动服务。
- 如果课程目录 `/data/switchyardvid` 也是业务资产，也要纳入单独备份策略。

### P1：一周内补齐

#### 2.5 增加健康检查与可观测性入口

目前程序有较完整的启动日志，但没有标准健康检查端点：

- 代码里有 `MapControllers()`，但未见 `AddHealthChecks/MapHealthChecks`，见 [Program.cs](/d:/SwitchYard/SwitchYard.WebApi/SwitchYard.Service/Program.cs:431)

建议：

- 增加 `/health/live` 和 `/health/ready`。
- `ready` 至少检查：
  - 数据库连通性
  - `/data/switchyardvid` 是否存在且可读
  - JWT 配置是否已注入
- 反向代理和外部监控统一探测 `/health/ready`。

#### 2.6 为 MySQL 表补齐主键、唯一约束和索引

当前建表脚本大多只有字段，没有主键和索引：

- `user` 表没有主键和唯一用户名约束，见 [mysql-schema.sql](/d:/SwitchYard/SwitchYard.WebApi/SwitchYard.Service/Database/mysql-schema.sql:1)
- `refreshtoken` 表没有主键或索引，见 [mysql-schema.sql](/d:/SwitchYard/SwitchYard.WebApi/SwitchYard.Service/Database/mysql-schema.sql:12)

而实际查询又大量依赖这些字段：

- 按 `user.name` 登录查询，见 [UserService.cs](/d:/SwitchYard/SwitchYard.WebApi/SwitchYard.Service/Services/UserService.cs:80)
- 按 `user.id` 查询用户，见 [UserService.cs](/d:/SwitchYard/SwitchYard.WebApi/SwitchYard.Service/Services/UserService.cs:117)
- 按 `refreshtoken.token` 查询 token，见 [RefreshTokenService.cs](/d:/SwitchYard/SwitchYard.WebApi/SwitchYard.Service/Services/RefreshTokenService.cs:91)

建议最低补齐：

- `user(id)` 主键
- `user(name)` 唯一索引
- `refreshtoken(token)` 主键或唯一索引
- `refreshtoken(userid, isrevoked)` 组合索引
- `humpinstance(ID)` 索引
- 所有高频查询表上的 `InstanceID`、`SlopeLineID`、`HumpSchemeID`、`HeadwayCheckID` 组合索引

否则随着实例数据增长，查询延迟和锁竞争会越来越明显。

#### 2.7 建立正式的数据库变更流程

当前 schema 通过 SQL 文件在启动时执行，适合初始化，不适合长期变更。

建议：

- 从现在开始给数据库变更编号，例如 `V001__init.sql`、`V002__add_indexes.sql`。
- 发布前先执行变更，再重启应用。
- 不建议依赖应用启动时自动“顺手改库”作为正式变更机制。

#### 2.8 为 Refresh Token 建立清理任务

代码里已经提供了清理过期 token 的方法：

- 见 [RefreshTokenService.cs](/d:/SwitchYard/SwitchYard.WebApi/SwitchYard.Service/Services/RefreshTokenService.cs:167)

但目前没有发现定时调用逻辑。长时间运行后，`refreshtoken` 表会持续增长。

建议：

- 每天执行一次清理任务。
- 如果暂时不想改代码，可以先通过数据库计划任务或运维脚本清理过期数据。
- 清理前先给 `expires` 和 `userid` 建索引，否则删除效率会变差。

#### 2.9 课程文件服务要单独考虑带宽、缓存和磁盘

课程接口当前是匿名开放的，并支持范围请求：

- 匿名访问见 [CourseController.cs](/d:/SwitchYard/SwitchYard.WebApi/SwitchYard.Service/Controllers/CourseController.cs:41)
- 流式文件下载见 [CourseController.cs](/d:/SwitchYard/SwitchYard.WebApi/SwitchYard.Service/Controllers/CourseController.cs:107)

另外，清单接口每次都会递归扫描目录：

- 见 [CourseController.cs](/d:/SwitchYard/SwitchYard.WebApi/SwitchYard.Service/Controllers/CourseController.cs:132)

建议：

- 用 Nginx 代理课程内容，并开启静态缓存、带宽限制和访问日志。
- 如果文件量会上千，建议对 manifest 做缓存，避免每次请求都递归扫盘。
- 为 `/data/switchyardvid` 设置独立磁盘告警阈值。
- 如果课程资料不适合匿名公开，后续应增加鉴权或防盗链。

## 3. 生产部署建议

### 3.1 推荐拓扑

建议采用：

- `Internet -> Nginx/Caddy -> Kestrel(127.0.0.1:7297) -> MySQL`

这样可以把以下职责放到反向代理层：

- TLS 证书管理
- HTTP 到 HTTPS 跳转
- 静态资源缓存
- 请求体大小限制
- WAF / 黑白名单 / 基础限流

### 3.2 保持现有 systemd 最小权限设计，同时补资源限制

当前 `systemd` 已经做了不少加固，方向是对的：

- 独立用户运行，见 [switchyard-api.service](/d:/SwitchYard/scripts/deploy/switchyard-api.service:11)
- `NoNewPrivileges=true`，见 [switchyard-api.service](/d:/SwitchYard/scripts/deploy/switchyard-api.service:22)
- `ProtectSystem=strict`，见 [switchyard-api.service](/d:/SwitchYard/scripts/deploy/switchyard-api.service:25)

建议继续补：

- `LimitNOFILE=65535`
- `MemoryMax=` 根据服务器规格设置上限
- `TasksMax=` 防止异常线程膨胀
- `StartLimitIntervalSec` 与 `StartLimitBurst`，避免异常反复重启

### 3.3 发布流程从“直接覆盖”逐步演进到“可回滚”

当前 README 描述的流程是 `rsync` 覆盖发布后直接重启，适合早期，但回滚成本偏高。

建议：

- 发布目录改为版本化，例如 `/opt/switchyard/releases/20260502-01/`
- `current` 软链接指向当前版本
- 启动脚本始终指向 `current`
- 新版本发布后先做：
  - 配置文件检查
  - 数据库连通性检查
  - 本地 smoke test
- 验证通过再切换软链接并重启
- 保留最近 3 到 5 个版本，支持快速回滚

## 4. 安全建议

### 4.1 数据库连接安全

当前安装脚本默认写入：

- `HumpDatabase__MysqlConfig__SslMode=Preferred`，见 [install-secrets.sh](/d:/SwitchYard/scripts/deploy/install-secrets.sh:135)

`Preferred` 表示“能加密就加密，不能加密也照连”。如果数据库是远程实例，这个级别偏弱。

建议：

- 单机本地 MySQL 可以接受本地回环连接。
- 如果数据库跨主机，改成 `Required`，更理想是启用服务端证书校验。
- 数据库只允许应用服务器访问，禁止公网暴露 `3306`。

### 4.2 保持 Swagger 只在开发环境开启

这点当前实现是好的：

- Swagger 仅在开发环境启用，见 [Program.cs](/d:/SwitchYard/SwitchYard.WebApi/SwitchYard.Service/Program.cs:408)

建议继续保持，不要在生产直接开放调试文档。

### 4.3 注册与登录限流还需要外围补充

应用层已经对登录和注册做了基础限流：

- `auth` 每分钟 5 次，见 [Program.cs](/d:/SwitchYard/SwitchYard.WebApi/SwitchYard.Service/Program.cs:280)
- `register` 每 10 分钟 3 次，见 [Program.cs](/d:/SwitchYard/SwitchYard.WebApi/SwitchYard.Service/Program.cs:292)

建议：

- 在 Nginx/Caddy 再做一层限流。
- 对异常 IP 增加封禁策略，例如连续失败后暂时拉黑。
- 对管理接口额外做访问源限制。

## 5. 日志、监控与告警

### 5.1 日志现状

当前日志会同时写控制台和文件：

- 见 [Program.cs](/d:/SwitchYard/SwitchYard.WebApi/SwitchYard.Service/Program.cs:90)

优点是方便排查，缺点是如果不做监控，日志只是“能看”，还不是“可运维”。

建议：

- 统一监控 `systemd` 服务状态、重启次数和最近错误日志。
- 监控 `/opt/switchyard/api/logs` 的磁盘占用。
- 采集关键指标：
  - 5xx 数量
  - 登录失败数量
  - 请求耗时 P95/P99
  - MySQL 连接失败数
  - 课程目录可用空间

### 5.2 数据库错误要避免“只打印控制台”

当前数据库基础层在异常时直接 `Console.WriteLine` 并返回 `null/0`：

- 查询见 [DBConnector.cs](/d:/SwitchYard/SwitchYard.WebApi/SwitchYard.Service/DBConnector.cs:47)
- 写入见 [DBConnector.cs](/d:/SwitchYard/SwitchYard.WebApi/SwitchYard.Service/DBConnector.cs:82)

这会带来两个问题：

- 上层可能只看到“操作失败”，但缺少结构化错误上下文
- 监控系统难以稳定提取数据库异常指标

建议：

- 后续把数据库层错误接入 `ILogger`。
- 对关键写操作失败增加告警条件。

## 6. 业务数据与初始化检查

### 6.1 新用户注册依赖模板实例 `001`

当前注册新用户时会自动复制模板实例：

- 模板实例常量见 [AuthController.cs](/d:/SwitchYard/SwitchYard.WebApi/SwitchYard.Service/Controllers/AuthController.cs:22)
- 注册逻辑见 [AuthController.cs](/d:/SwitchYard/SwitchYard.WebApi/SwitchYard.Service/Controllers/AuthController.cs:237)

这意味着：

- 新库初始化后如果缺少模板实例 `001`，注册流程会失败。
- 数据库迁移、恢复、测试环境初始化都要把这份种子数据纳入检查项。

建议：

- 把“模板实例存在性检查”加入上线前 smoke test。
- 最好提供一份标准种子数据初始化脚本。

## 7. 建议的日常运维检查清单

建议每天至少检查一次：

- `systemctl status switchyard-api`
- `journalctl -u switchyard-api -n 200 --no-pager`
- `/opt/switchyard/api/logs` 磁盘占用
- `/data/switchyardvid` 是否可读、容量是否接近阈值
- MySQL 主从状态或实例健康状态
- 当日备份是否成功生成

建议每周至少检查一次：

- 访问日志中的高频来源 IP
- 登录失败和 429 次数
- 慢查询日志
- `refreshtoken` 表记录数
- TLS 证书剩余有效期

建议每月至少执行一次：

- 备份恢复演练
- 漏洞修复和系统补丁升级
- JWT 密钥与数据库账户使用情况复核

## 8. 分阶段落地建议

### 第一阶段：现在就做

- 轮换仓库中暴露过的 JWT 和数据库密码
- 改用最小权限数据库账号
- 确认公网只开放反向代理端口
- 收紧受信任代理 IP
- 建立数据库与课程目录备份

### 第二阶段：本周完成

- 增加健康检查端点
- 给 MySQL 补主键、唯一约束和索引
- 补齐 Refresh Token 清理机制
- 给课程资源访问加缓存与带宽控制

### 第三阶段：后续优化

- 引入正式数据库迁移机制
- 发布目录版本化，支持快速回滚
- 接入统一监控与告警平台
- 评估课程文件是否需要对象存储或 CDN

## 9. 总结

这套程序已经具备了不错的生产雏形：有 `systemd` 托管、独立服务用户、环境变量注入、JWT、限流、日志和反向代理意识。当前真正需要优先补的，不是“能不能跑”，而是三件事：

- 把密钥与数据库账号彻底从代码仓库中剥离并轮换
- 把数据库从“能用”提升到“可维护”，也就是索引、备份、恢复、迁移
- 把服务从“可启动”提升到“可观测”，也就是健康检查、告警、日常巡检

如果这三块补齐，这套系统就会从开发可用状态，比较稳地迈到可持续运维状态。
