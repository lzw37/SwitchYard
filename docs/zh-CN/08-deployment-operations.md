# 部署、升级与运维

[文档目录](README.md) · [English](../en/08-deployment-operations.md) · 核对日期：2026-10-11

本文以当前代码和仓库自带的 Ubuntu/systemd 部署脚本为依据。开发环境与 Agent 首次安装见[安装与配置](07-installation.md)。[原部署说明](../archive/guides/Deploy-Instruction.md)、[原运维说明](../archive/guides/Operations-Recommendations.md)和[原 SQLite→MySQL 迁移文档](../archive/guides/SQLite-to-MySQL-Migration.md)完整保留，其中历史数据量、两库架构与早期迁移建议不能直接作为当前运行事实。

## 1. 部署结构与交付物

推荐把前端放在 `https://app.example.com`，API 放在 `https://api.example.com`，由反向代理终止 TLS。以下域名和路径都是示例，应替换为本环境的值。

| 交付物 | 运行位置 / 要求 |
| --- | --- |
| `switchyard-vue/dist/` | 静态 Web 服务器，History 路由回退 |
| API 发布包 | `/opt/switchyard/api`；ASP.NET Core 8 Runtime |
| 私有 API 配置 | `/etc/switchyard/api.env`，systemd 注入 |
| 三组数据库 | 驼峰、能力、SketchLayout 分开管理 |
| API 日志 | `/opt/switchyard/api/logs` |
| SQLite 数据 | 如采用 SQLite，放 `/opt/switchyard/data` |
| 课程资料 | API 服务账号可读的目录，示例 `/data/switchyardvid` |
| 可选 Agent | .NET 10 Runtime，能经 HTTPS/SignalR 访问 API |

构建机使用 .NET 10 SDK 和符合前端 `engines` 的 Node.js。不要因 API 目标为 `net8.0` 而只安装 .NET 8 SDK 构建所有组件。完整解决方案含缺失或未跟踪的测试项目，部署优先发布明确的单项目。

仓库的安装脚本不安装 .NET、MySQL、Nginx，也不创建业务数据库、导入模板或生成首个管理员。API 发布包须包含 `Database/*.sql` 与 `scripts/deploy/`。

## 2. 先准备数据库和运行配置

完整系统使用 `HumpDatabase`、`CapacityDatabase`、`SketchLayoutDatabase` 三个独立配置节。用户和 Refresh Token 在驼峰库；能力计划、过程和求解预设在能力库；SketchLayout 方案在第三库。

MySQL 数据库由管理员提前创建并授权。以下只演示库名和字符集，不包含账号密码：

```sql
CREATE DATABASE IF NOT EXISTS hump
  CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;
CREATE DATABASE IF NOT EXISTS capacity
  CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE DATABASE IF NOT EXISTS sketchlayout
  CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
```

目标 MySQL 必须支持使用的排序规则；驼峰迁移工具固定使用 `utf8mb4_0900_ai_ci`。授权范围应覆盖实际采用的三库及建表、索引、必要的改表和业务读写。API 启动会执行 DDL 和历史数据迁移，不是仅检查数据库连接。

SQLite 部署则分别配置 `/opt/switchyard/data/hump.db`、`capacity.db`、`sketchlayout.db`；先建立父目录并授予 `switchyard` 写权限。不要把三个配置指向同一文件，也不要把活动数据库放在会被发布覆盖的 API 目录。

首次部署还须准备模板 `001`、关联数据和有效管理员。schema 不提供这些数据；详细注册前提见[首次数据准备](07-installation.md#3-首次数据准备是安装的一部分)。

配置按基础 JSON、环境 JSON、环境变量/命令行等来源合并。`WebApi:Hosts` 是应用实际监听入口，生产通常设置回环 `7297`，第二项为空。CORS 来源应维护完整有效列表，不能假定覆盖数组第 0 项会删除其余生产配置。

## 3. 发布和安装 API 服务

在构建机仓库根目录：

```bash
dotnet publish SwitchYard.WebApi/SwitchYard.Service/SwitchYard.Service.csproj \
  -c Release -o ./publish/api
scp -r ./publish/api deploy-user@server:/tmp/switchyard-api-publish
```

交付前检查发布包不带本机私密配置、业务数据库或不应公开的资料。环境值使用自己的私有配置，不复用仓库中的开发连接值。

Ubuntu 服务器安装：

```bash
sudo mkdir -p /opt/switchyard/api
sudo rsync -av /tmp/switchyard-api-publish/ /opt/switchyard/api/
cd /opt/switchyard/api/scripts/deploy
sudo JWT_AUTOGEN=1 bash install-secrets.sh
```

脚本创建系统用户 `switchyard`、日志/数据目录、权限 `0600` 的环境文件，并安装 systemd 单元。`JWT_AUTOGEN=1` 需要 `openssl`，否则按提示输入自行生成的长随机签名密钥。

**脚本的实际行为：** 它询问驼峰库和能力库的库名，却同时写入第三组 `SketchLayoutDatabase`，库名固定为 `sketchlayout`，三组默认共用输入的 MySQL 账号。界面中“两库”的提示不能据此省略第三库。数据库已存在但名称/账号不同的，应在启动前编辑完整配置。

```bash
sudoedit /etc/switchyard/api.env
sudo systemctl enable --now switchyard-api
sudo systemctl status switchyard-api
sudo journalctl -u switchyard-api -n 100 --no-pager
curl -f http://127.0.0.1:7297/api/System/version
```

核对三组数据库、JWT、课程目录、CORS，以及 `WebApi__Hosts__0=http://127.0.0.1:7297`、`WebApi__Hosts__1=`。安装脚本再次运行会备份并重写环境文件，手工增加的 CORS、课程路径及其他设置不会自动保留；只调整配置时直接编辑文件并重启。

systemd 单元使用 `ProtectSystem=strict`、`ProtectHome=true`，仅允许写 API 的 `logs` 和 `/opt/switchyard/data`。服务资料不要放在家目录；能通过文件权限读取并不意味着 systemd 沙箱允许写入。

### 3.1 历史过程迁移的备份目录

当前自动过程迁移在有变更时将备份写入 API 输出目录下的 `migration-backups/`。仓库现有 systemd 单元没有把该目录加入可写列表。升级含旧过程股道引用的数据前，应建立目录并追加精确写权限：

```bash
sudo install -d -o switchyard -g switchyard -m 0750 /opt/switchyard/api/migration-backups
sudo systemctl edit switchyard-api
```

在 drop-in 中填写：

```ini
[Service]
ReadWritePaths=/opt/switchyard/api/migration-backups
```

然后执行 `sudo systemctl daemon-reload` 并按升级窗口重启服务。该设置追加到原有写路径，不要清空原 `logs` 和 `data` 的权限。迁移备份须纳入备份/清理策略；仅对目录 `chmod` 无法绕过 `ProtectSystem=strict`。

## 4. 构建前端并配置 HTTPS 反向代理

在构建机执行：

```bash
cd switchyard-vue
npm ci
npm run type-check
VITE_API_BASE_URL=https://api.example.com npm run build-only -- --mode production
```

类型检查失败时应停止发布。PowerShell 使用 `$env:VITE_API_BASE_URL = 'https://api.example.com'` 和 `npm.cmd`。构建产物中的 API URL 不会因运行服务器环境变化而更新，地址改变须重新构建。

API URL 是服务器根地址，不追加 `/api`。把 `dist/` 内容部署到前端静态目录，保留独立站场前端本地依赖的完整构建来源。

Nginx 示例：`map` 位于 `http` 块，证书路径须对应真实证书。

```nginx
map $http_upgrade $connection_upgrade {
    default upgrade;
    ''      close;
}

server {
    listen 443 ssl;
    server_name app.example.com;
    ssl_certificate     /etc/ssl/switchyard/fullchain.pem;
    ssl_certificate_key /etc/ssl/switchyard/privkey.pem;
    root /var/www/switchyard;
    index index.html;
    location / {
        try_files $uri $uri/ /index.html;
    }
}

server {
    listen 443 ssl;
    server_name api.example.com;
    ssl_certificate     /etc/ssl/switchyard/fullchain.pem;
    ssl_certificate_key /etc/ssl/switchyard/privkey.pem;
    location / {
        proxy_pass http://127.0.0.1:7297;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection $connection_upgrade;
        proxy_read_timeout 3600s;
    }
}
```

代理保留完整原始路径，涵盖 `/api/*`、`/Hump/*`、`/Capacity/*`、`/StationLayout/*`、`/OperationPlan/*`、`/OperationProcess/*`、`/SketchLayout/*`、`/Course/*` 和 `/hubs/capacity-agent`。仅代理 `/api/` 会造成部分页面能登录、业务却不可用。

生产 CORS 加入前端完整 origin，例如 `https://app.example.com`。API 启用 HTTPS 重定向及非开发环境 HSTS；代理应正确转发原始协议，以免重定向或混合内容问题。当前生产代码信任回环和 RFC1918 私网转发来源，最多两层；改变代理拓扑时核对 `Program.cs` 中固定的信任范围。

Agent 需要长连接的 WebSocket 升级头及合适超时；课程视频/PDF 需要保留 Range 请求与响应。前端 History 路由刷新须回退 `index.html`。部署到 `/switchyard/` 等子路径需同时调整 Vite `base`、路由基地址和静态回退，再重新构建。

有上传需求时核对代理的请求体上限。应用当前限制 SketchLayout 请求为 8 MiB、DWG 导入为 20 MiB；代理限制也会独立生效，不能只提高应用侧参数。multipart 请求还包含表单开销。

## 5. 课程资料管理

`Course__VideoDir` 指定视频根目录；`Course__DocDir` 指定文档目录，未配置时回退视频目录。生产 JSON 的默认值为 `/data/switchyardvid`。

若配置目录下有 `站场视频` 子目录，视频接口优先将它作为根目录；若有 `教学文档` 子目录，文档接口优先使用它。否则直接递归扫描配置目录。

```text
/data/switchyardvid/
  站场视频/
    第一篇/第一章/1.1.1.mp4
  教学文档/
    第一篇/学习指导.doc
    第一篇/第一章/学习指导.doc
    第一篇/第一章/第一节/课件.pdf
    第一篇/第一章/第一节/文字教材.doc
    第一篇/第一章/第一节/习题测试.doc
```

| 类型 | 当前接受的扩展名 |
| --- | --- |
| 视频 | `.mp4`、`.webm`、`.m4v`、`.mov`、`.avi`、`.mkv` |
| 文档 | `.pdf`、`.doc`、`.docx` |

清单排除文件名以 `~$` 开头的临时文件，按相对路径排序。扩展名被接受不代表浏览器支持其中的视频编码；无法播放时先核对浏览器兼容的编码与文件完整性。

课程前端还需要“篇/章/节”层级，以及“章号.节号[.片段号]”视频文件名；文件出现在 API 清单中不保证出现在课程树中。`学习指导`、`文字教材`、`习题测试` 等中文识别标记在英文界面下也须保留。Word 提取使用字节解码启发式，不是完整 Word 渲染器，不能保证任意 `.docx` 可解析。投放前用代表性章节验证；详细命名、目录和内容标记见[用户指南的教学资源要求](02-user-guide.md#8-教学资源的组织要求)。

四个课程接口允许匿名访问：`GetVideoManifest`、`StreamVideo`、`GetTeachingManifest`、`StreamTeachingFile`，均位于 `/Course/` 下。当前没有课程上传 API、账号授权或后台清单管理；通过服务器文件部署维护资料，不应把需要保密的文件放到这些目录。

目录不存在/未配置时清单返回空数组；流式请求找不到根目录或文件时返回 404。文件接口支持 Range 分段读取。目录遍历不等于文件缓存，资料较多时留意清单响应时间和磁盘访问。

## 6. 更新、回滚与恢复

上线前记录代码提交、API `App:Version`、前端构建 API URL、三库类型及变更窗口。让正在求解的任务完成；必要时退出 Agent 停止其 Worker，但当前没有单任务取消接口。

建议升级顺序：

1. 备份三库、环境文件、课程资料、旧发布包和已下载求解结果。
2. 在隔离数据库副本上核对新 schema、迁移与代表性业务；确认迁移备份目录可写。
3. 停止 API，部署新 API 发布包；保留 `logs/` 和 `migration-backups/`。
4. 启动并检查迁移、绑定日志及版本响应，再验证账号和业务数据。
5. 发布配套前端；升级 Agent 后重新登录注册，验证模型兼容性。
6. 验证站场、计划、过程、SketchLayout、课程和代表性求解。

停止/启动命令为 `sudo systemctl stop switchyard-api` 与 `sudo systemctl start switchyard-api`。仅调整环境文件使用 `sudo systemctl restart switchyard-api`。另起调试进程也必须使用独立数据库配置，因为启动会写入数据库。

回滚须恢复与旧程序兼容的数据库版本，不能仅覆盖旧 DLL 撤销 ID 类型调整、历史布局转换或过程数据升级。切换后若已产生新业务数据，先保留故障现场并决定补录/合并方案，再恢复旧快照。

### 6.1 备份范围与恢复演练

| 对象 | 备份要求 |
| --- | --- |
| 驼峰库 | 用户、Refresh Token、模板 `001` 和全部驼峰业务表 |
| 能力库 | 站场、计划、过程模板/快照、分析、求解预设、迁移标记 |
| SketchLayout 库 | 独立方案数据 |
| 课程目录 | 保留子目录和相对路径 |
| 配置 | 私有环境文件、代理证书配置、systemd drop-in；限制访问 |
| 迁移备份/报告 | 与升级前数据库备份一起保留；不能替代完整备份 |
| 需要留存的 Agent 原始结果 | 重启 API 前从页面下载 |

SQLite 使用数据库备份机制，或停止全部写入后进行一致性复制；不要只复制正在写入的主数据库文件而忽略 WAL 状态。MySQL 使用适合本部署的一致性备份流程，并记录对应应用版本。跨库恢复应保持同一业务时点，避免账号与所有者关系错位。

恢复到隔离目录或新库验证：启动、原账号登录、模板复制、实例与方案打开、站场进路一致、作业占用和过程快照、SketchLayout 读取、课程及 Agent 连接。版本接口成功不能代替恢复验证。

## 7. SQLite → MySQL：现有工具的准确范围

包装脚本为 [`scripts/Migrate-SqliteToMySql.ps1`](../../scripts/Migrate-SqliteToMySql.ps1)，实现为 [.NET 8 Migrator](../../tools/SQLiteToMySqlMigrator/Program.cs)。它固定迁移驼峰库的 20 张表，不迁移能力库或 SketchLayout 库，更换 `SchemaPath` 不能改变这一范围。

表集合为 `user`、`refreshtoken`、`humpinstance`、`slopeline`、`position`、`positionsegment`、`switch`、`retarder`、`wagonconcept`、`operationcondition`、`humpscheme`、`vposition`、`vpositionsegment`、`humpcalculation`、`humpcalculationdata`、`retarderstatus`、`headwaycheckscheme`、`headwaycheckwagon`、`headwaycheckdata`、`headwaycheckresult`。

工具只读取指定 JSON 的 `HumpDatabase` 节；它不合并 API 的生产 JSON、环境变量或 `api.env`。创建受保护的私有文件，例如 `LocalData/migration.json`：

```json
{
  "HumpDatabase": {
    "SqlliteConfig": { "DatabaseFile": "D:/private-backups/hump-copy.db" },
    "MysqlConfig": {
      "Host": "127.0.0.1",
      "Port": 3306,
      "Database": "hump_migration_test",
      "Username": "REPLACE_WITH_MIGRATION_USER",
      "Password": "REPLACE_WITH_PRIVATE_PASSWORD"
    }
  }
}
```

先从仓库根目录查看计划：

```powershell
./scripts/Migrate-SqliteToMySql.ps1 -ConfigPath ./LocalData/migration.json -DryRun
```

`-DryRun` 只验证配置和本地路径并显示计划，不连接数据库、不执行预检查 SQL，也不证明导入会成功。正式执行前完成停写和备份，确认目标是专门的新库，然后移除 `-DryRun`。

正式流程会尝试建库、读取源库、执行预检查及 schema、清空目标表、在事务中导入、逐表比较行数，并在当前目录的 `migration-reports/` 写报告。报告可能含数据质量检查的记录，须按业务数据保护。

**默认删除目标 20 张表中的全部数据，删除发生在导入事务之外。** 导入失败无法自动恢复被清空的目标。预检查发现重复/孤儿等问题只告警，既不自动清洗，也不一定阻止继续导入。先审阅问题并在源库副本上处理。

| 参数 | 含义 |
| --- | --- |
| `-ConfigPath` | 私有配置 JSON |
| `-SqlitePath` | 覆盖 SQLite 源路径 |
| `-MySqlHost` / `-MySqlPort` / `-MySqlDatabase` | 覆盖目标位置 |
| `-MySqlUsername` / `-MySqlPassword` | 覆盖凭据；密码优先放私有文件，避免命令历史 |
| `-SchemaPath` / `-PrecheckPath` | 指定 SQL；默认仓库驼峰 schema/预检查 |
| `-SkipSchemaInitialization` / `-SkipPrecheck` | 跳过对应步骤，不建议未经验证使用 |
| `-SkipClearTarget` | 追加导入；不是增量同步/去重，可能重复并导致行数校验失败 |
| `-SkipValidation` | 跳过行数验证 |

行数一致后仍须检查账号登录、模板复制、实例数据、计算与保存结果。切换 `HumpDatabase` 不会迁移另两库。保留原 SQLite 只读备份及迁移报告，确认业务验收后再完成正式切换。旧迁移文档中的历史样本行数、清洗建议和未来主外键方案不代表当前目标数据库结构。

能力库另有 `--migrate-process-routes` 维护入口，属于旧过程资源转换，**不是 SQLite→MySQL 导数工具**。不带 `--apply` 时读取现有能力数据并输出转换计划；带 `--apply` 时写备份并应用转换。它需要已有相关表，正常 API 启动也会自动应用该转换。

## 8. 监控与故障定位

日常检查：

```bash
sudo systemctl status switchyard-api
sudo journalctl -u switchyard-api -n 100 --no-pager
curl -f http://127.0.0.1:7297/api/System/version
```

API 文件日志为 `logs/switchyard-*.log`，按日及 10 MB 分卷，保留最近 30 个文件。Agent 为自身 `logs/capacity-agent-*.log`，按日及 50 MB 分卷，也保留 30 个文件；求解原生日志以 `OR-Tools` 来源写入。文件数不是天数。

日志配置在程序代码中直接建立，不能假定修改 JSON 的 `Serilog` 节就改变实际输出。当前没有专门的存活/就绪健康端点，建议额外监测进程、磁盘、数据库、代表性业务操作以及 Agent 状态和失败率。

登录、Refresh Token 刷新和 Agent 认证共用按客户端 IP 每分钟 5 次的 `auth` 限流；注册每 IP 每 10 分钟 3 次，超限为 429。代理不正确传递 IP 时，多个用户可能被算作同一来源。

| 症状 | 优先检查 |
| --- | --- |
| API 无法绑定 | `WebApi__Hosts__*` 中旧 IP、重复端口或遗留索引；最终绑定日志 |
| 启动数据库异常 | 三组独立配置、库是否存在、SQL 发布文件、DDL 权限 |
| 旧数据升级中断 | 迁移错误上下文、重复 ID/无效快照/进路歧义，备份目录是否可写 |
| SQLite 只读错误 | 父目录权限、systemd 可写路径、数据文件所在目录 |
| 版本正常但登录失败 | 驼峰数据、账号激活、密码哈希、JWT、限流；版本不检查这些 |
| 新用户创建失败 | 模板 `001` 及关联记录；查看补偿删除是否也失败 |
| 能力/SketchLayout 独立失败 | 对应数据库配置与表结构，不只看驼峰库 |
| 首页正常、刷新路由 404 | 静态服务器 History 回退 |
| 登录正常、业务 404 | 代理是否只覆盖 `/api/` 或错误重写了路径 |
| 浏览器访问旧 API | 构建期 `VITE_API_BASE_URL`、旧静态缓存，重新构建部署 |
| CORS/重定向问题 | 完整前端 origin、数组合并、转发协议和 HTTPS 配置 |
| 上传返回 413 | 应用与代理两层请求体上限，表单开销及实际文件大小 |
| Agent 认证失败 | 激活、强制改密、正确 API 根 URL、认证限流 |
| Agent 在线却不可用 | 共享权限、空槽、CPU/内存、模型最低版本、名称/用户名 ID 冲突 |
| 任务完成却无结果可用 | 求解器 `hasSolution` / `status`，任务完成不等于可行 |
| 任务/原始结果消失 | API 重启导致内存任务丢失；区分数据库持久计划 |
| 课程为空或播放失败 | 目录、子目录优先级、扩展名/编码、读取权限、Range 转发 |

数据库连接层会抛出含操作和 SQL 摘要的异常；部分上层服务仍返回通用错误，需结合服务日志定位。分享日志时删除凭据、令牌和不应公开的业务记录。

尚待完善的运维能力包括：完整健康检查、全模块统一迁移管理、首次数据引导、任务持久队列/恢复、自动清理过期 Refresh Token、课程授权与缓存。它们是后续建设项，不是当前已交付功能。

卸载自带服务单元可执行 `sudo bash install-secrets.sh --uninstall`。脚本停止/禁用服务、移除单元并改名保留环境文件；数据库和运行目录不会自动删除。

## 9. 依据与保留资料

- [systemd 单元](../../scripts/deploy/switchyard-api.service)、[环境模板](../../scripts/deploy/switchyard-api.env.example)、[安装脚本](../../scripts/deploy/install-secrets.sh)。
- [课程控制器](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/CourseController.cs)、[过程资源迁移](../../SwitchYard.WebApi/SwitchYard.Service/Services/OperationProcessRouteMigration.cs)。
- [原部署说明](../archive/guides/Deploy-Instruction.md)、[原运维说明](../archive/guides/Operations-Recommendations.md)、[完整历史迁移资料](../archive/guides/SQLite-to-MySQL-Migration.md)。
