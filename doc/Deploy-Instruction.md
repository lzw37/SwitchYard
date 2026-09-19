# SwitchYard 部署说明

本文对应当前 Vue 前端、.NET 8 API 和可选的 .NET 10 CapacityAgent。本地启动见 [开发与配置指南](Getting-Started.md)，日常维护见 [运维说明](Operations-Recommendations.md)。

## 1. 部署组成

| 组件 | 产物 / 入口 | 运行要求 |
| --- | --- | --- |
| Web 前端 | `switchyard-vue/dist/` | 静态文件服务器；使用 History 路由 |
| API | `SwitchYard.Service.dll` | ASP.NET Core 8 Runtime；可访问两个业务数据库 |
| CapacityAgent（可选） | `SwitchYard.CapacityAgent.dll` | .NET 10 Runtime；经 HTTP/SignalR 连接 API |
| 课程资料（可选） | 视频、PDF、Word 文件目录 | API 运行用户有读取权限 |

仓库构建使用 .NET 10 SDK，以覆盖 Agent 和共享组件的目标框架；运行 API 仍需 ASP.NET Core 8 Runtime。前端需要 Node.js `^20.19.0 || >=22.12.0`。

API 发布包包含 `Database/*.sql` 和 `scripts/deploy/`。部署脚本提供 Ubuntu 的 `systemd` 单元、环境变量模板和配置安装脚本；不会安装 .NET、MySQL、反向代理，也不会创建 MySQL 数据库或导入业务数据。

## 2. 配置与数据库准备

API 配置依次读取 `appsettings.json`、对应环境的 `appsettings.{Environment}.json`、环境变量和命令行参数，后者优先。嵌套键在环境变量中以 `__` 分隔。

监听地址由 `WebApi:Hosts` 配置后调用 `UseUrls` 设置；不要仅修改 `launchSettings.json` 或 `ASPNETCORE_URLS`。生产配置和安装脚本默认使用 `http://127.0.0.1:7297`。配置数组逐项合并，改变监听数量时也要覆盖不用的索引。

| 环境变量 | 用途 |
| --- | --- |
| `ASPNETCORE_ENVIRONMENT=Production` | 启用生产配置；不开放 Swagger |
| `WebApi__Hosts__0`、`WebApi__Hosts__1` | 监听地址；第二项默认留空 |
| `Jwt__SecretKey` | 自行生成的 JWT 签名密钥；可用 `openssl rand -base64 64` 生成 |
| `HumpDatabase__DatabaseType` | `Mysql` / `MySQL` 或 `SQLite` / `Sqllite` |
| `CapacityDatabase__DatabaseType` | 能力模块独立数据库，同上 |
| `<数据库节>__MysqlConfig__Host` / `Port` / `Database` / `Username` / `Password` | 两个 MySQL 数据库分别配置连接信息 |
| `<数据库节>__SqlliteConfig__DatabaseFile` | SQLite 文件绝对路径；键名保留代码中的 `SqlliteConfig` 拼写 |
| `Course__VideoDir`、`Course__DocDir` | 课程资料目录；生产默认 `/data/switchyardvid` |
| `Cors__AllowedOrigins__0` 等 | 前端跨域访问时允许的完整 origin（协议、域名、端口，不含路径） |

`<数据库节>` 分别为 `HumpDatabase`、`CapacityDatabase`。用户和 Refresh Token 保存在驼峰数据库。两组数据库不要指向同一库/同一 SQLite 文件：两套业务存在名称相同、结构不同的表。

使用 MySQL 时，先建立两个独立数据库，并授权应用账号访问。启动会执行建表脚本；能力库的 `stationroute`、`stationroutetime` 还可能执行 `ALTER TABLE ... CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci`。账号需要相应建表、读写及必要的结构修改权限。此流程不会创建 MySQL 数据库，也不是完整的版本化迁移机制；升级已有库前应备份并核对结构差异。

使用 SQLite 时，将两组 `DatabaseType` 改为 `SQLite`，分别指定 `/opt/switchyard/data/hump.db` 和 `/opt/switchyard/data/capacity.db`。目录必须提前存在且可写。

**业务初始化：** 建表不生成管理员、课程内容或示例实例。注册和管理员创建用户都会复制驼峰模板实例 `001`；缺少模板或模板关联数据不完整会导致创建用户失败。首次生产部署需导入经确认的模板及账号数据，并核实管理员状态；当前没有自动创建首个管理员的命令。

## 3. 发布 API 与安装服务

在仓库根目录发布：

```bash
dotnet publish SwitchYard.WebApi/SwitchYard.Service/SwitchYard.Service.csproj \
  -c Release -o ./publish/api
scp -r ./publish/api user@server:/tmp/switchyard-api-publish
```

在 Ubuntu 服务器上安装：

```bash
sudo mkdir -p /opt/switchyard/api
sudo rsync -av /tmp/switchyard-api-publish/ /opt/switchyard/api/
cd /opt/switchyard/api/scripts/deploy
sudo JWT_AUTOGEN=1 bash install-secrets.sh
```

按提示填写监听地址、MySQL 主机/端口、两个数据库名，以及访问两库的账号密码。设置 `JWT_AUTOGEN=1` 需要服务器已安装 `openssl`；不设置时脚本会提示输入签名密钥。

脚本会创建 `switchyard` 系统用户、权限为 `0600` 的 `/etc/switchyard/api.env`、日志和数据目录，并安装 `/etc/systemd/system/switchyard-api.service`。再次运行会备份并重写环境文件，不会保留自行增加的课程目录、CORS 或其他配置；执行后需核对这些项。

按实际部署调整环境文件并启动：

```bash
sudoedit /etc/switchyard/api.env
sudo systemctl enable --now switchyard-api
sudo systemctl status switchyard-api
sudo journalctl -u switchyard-api -n 100 --no-pager
curl -f http://127.0.0.1:7297/api/System/version
```

`version` 响应只证明 HTTP 服务能响应，不检查数据库。生产启动会检查 JWT，以及选择 MySQL 时的驼峰库账号密码；能力库配置也必须正确，不能把这项校验当作全部配置都通过的证明。

服务只允许写 `/opt/switchyard/api/logs` 与 `/opt/switchyard/data`。SQLite 应放在可写目录；课程文件只需可读。服务设置了 `ProtectHome=true`，不要把运行资料放在用户家目录下。

## 4. 构建前端

构建时显式设置 API 的**根 URL**，不要追加 `/api`：业务还使用 `/Hump`、`/Capacity`、`/Course` 等路径。

```bash
cd switchyard-vue
npm ci
npm run type-check
VITE_API_BASE_URL=https://api.example.com npm run build-only -- --mode production
```

Windows PowerShell 等价命令：

```powershell
Set-Location switchyard-vue
npm.cmd ci
$env:VITE_API_BASE_URL = 'https://api.example.com'
npm.cmd run type-check
if ($LASTEXITCODE -ne 0) { throw 'Type check failed' }
npm.cmd run build-only -- --mode production
```

`npm run build` 同时进行类型检查和构建；如 Windows 上并行脚本启动失败，可使用上述顺序命令。`VITE_API_BASE_URL` 在开发服务器启动/构建时读取，修改服务器环境后不会改变已生成的 `dist/`。需重新构建并替换静态文件。当前配置逻辑下，`build:dev` 也会选择生产 JSON 配置，因此环境名不能代替显式 URL。

保留仓库目录结构：主前端通过本地依赖引用 `../SwitchYard.StationLayout/frontend`。

## 5. 反向代理与前端路由

推荐将前端部署在站点根路径，将 API 放在独立子域名。例如前端 `https://app.example.com`，API `https://api.example.com`。下面是 Nginx 配置示例，需替换域名、证书路径和静态文件目录；`map` 放在 `http` 块中。若由其他入口终止 TLS，应按实际代理链传递原始协议。

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

API 代理保留整个原始路径，覆盖 `/api/*`、`/Hump/*`、`/Capacity/*`、`/StationLayout/*`、`/OperationPlan/*`、`/OperationProcess/*`、`/Course/*` 和 `/hubs/capacity-agent`。**只代理 `/api/` 会使业务功能失效。** WebSocket 升级头用于 Agent 长连接。

生产 API 的 `Cors:AllowedOrigins` 应包含 `https://app.example.com`。配置数组会合并现有生产列表；覆盖时核对全部索引，不能假定只设置第 0 项就删除其余项。课程视频与文档也经 API 返回，保留 Range 请求/响应以支持分段读取。

前端使用 History 模式，刷新 `/hump`、`/capacity` 等路径必须回退 `index.html`。若部署到 `/switchyard/` 子路径，应同时配置 Vite 的 `base` 和静态站点回退路径，并重新构建；API 根 URL 仍是单独的服务器地址。

## 6. 可选 CapacityAgent

模型求解需要至少一个当前用户可使用的在线 Agent。API 服务本身不运行 OR-Tools 求解器。

```bash
dotnet publish SwitchYard.WebApi/SwitchYard.CapacityAgent/SwitchYard.CapacityAgent.csproj \
  -c Release -o ./publish/capacity-agent
cd publish/capacity-agent
dotnet SwitchYard.CapacityAgent.dll
```

按提示输入 API 根 URL、已激活账号和密码；账号如要求首次改密，应先在 Web 页面完成。Agent 使用专用令牌连接 `/hubs/capacity-agent`。默认同时运行 2 个任务，每任务 2 核、2048 MB；通过发布目录中的 `capacity-agent.json` 或 `--config <路径>` 调整。

管理员可配置共享范围，普通用户 Agent 只允许本人使用。当前任务和求解结果保存在 Service 进程内存中；重启前下载需要保留的结果。完整说明见 [CapacityAgent README](../SwitchYard.WebApi/SwitchYard.CapacityAgent/README.md)。

## 7. 更新与回滚

1. 备份两个业务数据库、课程资料、环境配置和旧发布包；安排当前求解任务结束或取消。
2. 生成新 API 发布包及前端 `dist/`，检查数据库脚本与所需运行时。
3. 在服务器停止服务，再同步发布包。同步时保留日志：

   ```bash
   sudo systemctl stop switchyard-api
   sudo rsync -av --delete --exclude logs/ /tmp/switchyard-api-publish/ /opt/switchyard/api/
   sudo systemctl start switchyard-api
   ```

4. 核对启动日志、版本响应、登录、实例读取/保存、课程及 Agent 连接，再发布前端静态资源。
5. 若回滚，恢复旧程序和与其兼容的数据。仅恢复 DLL 不能撤销已执行的数据库结构变更。

只修改配置时编辑 `/etc/switchyard/api.env` 并重启即可，无需反复运行安装脚本。另一端口的调试进程也应使用独立配置和数据；启动即会连接数据库并执行初始化，不能当作只读检查。

卸载服务单元：

```bash
cd /opt/switchyard/api/scripts/deploy
sudo bash install-secrets.sh --uninstall
```

脚本停止/禁用服务并移除单元，将环境文件改名保留；运行目录和数据库不会自动删除。
