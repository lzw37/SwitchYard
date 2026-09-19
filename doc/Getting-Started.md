# SwitchYard 开发与配置指南

本文给出主程序的本地启动流程。界面与业务操作见 [项目说明](../README.md)，上线部署见 [部署说明](Deploy-Instruction.md)。示例不会使用仓库中已有的数据库连接信息。

## 1. 环境要求

| 用途 | 依赖 |
| --- | --- |
| 主前端 | Node.js `^20.19.0 || >=22.12.0`，npm |
| Web API | .NET 10 SDK 用于仓库构建；API 运行时为 ASP.NET Core 8 |
| CapacityAgent（模型求解） | .NET 10 SDK / Runtime |
| 数据库 | 本地 SQLite 或两个独立 MySQL 数据库 |

开发环境安装 .NET 10 SDK 和 ASP.NET Core 8 Runtime。API 本身目标为 .NET 8，但解决方案及共享组件含 .NET 10 目标，不应只依据 API 的目标版本选择 SDK。保留 `SwitchYard.StationLayout` 与 `switchyard-vue` 的相对位置，前端和 API 均引用其中的独立组件。

## 2. 启动本地 API（PowerShell + SQLite）

从仓库根目录执行以下配置。`LocalData` 已被版本控制忽略，示例给两套业务使用不同文件，并覆盖默认数据库选择。主 API 保持原端口 `7297`，监听所有 IPv4 网卡；`5102` 仅作为本机兼容入口保留。默认开发配置已采用这组监听地址，不依赖某个固定网卡 IP；下面显式写出，便于核对旧终端中可能残留的环境覆盖：

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

# 生成此开发会话自己的随机签名密钥，不输出密钥。
$jwtBytes = New-Object byte[] 64
$jwtRng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$jwtRng.GetBytes($jwtBytes)
$jwtRng.Dispose()
$env:Jwt__SecretKey = [Convert]::ToBase64String($jwtBytes)

dotnet run --project SwitchYard.WebApi/SwitchYard.Service/SwitchYard.Service.csproj --no-launch-profile
```

使用 `--no-launch-profile` 避免开发启动配置混入。监听地址最终来自 `WebApi:Hosts`，不是 `launchSettings.json` 的 `applicationUrl`。成功后检查：

- Swagger：`http://localhost:7297/swagger`
- 版本响应：`http://localhost:7297/api/System/version`
- 控制台中的 `Server bound addresses` 和 `Database schema initialized`

`0.0.0.0` 用于监听，不是浏览器访问地址；本机使用 `localhost`，其他设备使用运行 API 的电脑当前局域网地址。这样重启或切换网络后不需修改 API 的绑定 IP。

API 在开始监听前会创建缺少的表。父目录需要预先存在；MySQL 则需要提前建好数据库。两个库不能合并，因为两套 schema 中有同名但结构不同的表。

这些环境变量只在当前终端及子进程生效。重新生成密钥会使先前访问令牌失效；需要保留登录状态时，应通过本机私有配置保存并重用自己的密钥。不要把签名密钥或真实数据库密码提交到仓库。

### 首次数据与账号

空库可以启动并访问 Swagger、版本接口，但**不包含可直接使用的演示数据或默认管理员**。用户注册时必须复制驼峰模板实例 `001`；模板缺失或复制失败会回滚新建用户。因此完整业务联调还需导入经授权的开发模板和账号数据，或使用团队提供的开发数据库副本。

现有管理员可在用户管理中创建、激活账号；申请 `Admin` 角色的新账号默认未激活，需要既有管理员审核。仓库没有自动创建首个管理员的引导命令。不要根据“建表成功”判断注册、模型求解及示例实例已可用。

## 3. 启动主前端

另开终端，从仓库根目录执行：

```powershell
Set-Location switchyard-vue
npm.cmd ci
npm.cmd run dev
```

打开 `http://localhost:5173`。Vite 已设置 `strictPort`，`5173` 被占用时会报错，不会自动换端口。开发 API 默认跟随浏览器地址中的主机名使用 HTTP `7297`：本机访问会连接 `http://localhost:7297`，从局域网地址打开页面则连接同一主机的 `7297`。

需要显式连接其他 API 时，在启动前设置 `VITE_API_BASE_URL`，或写入 `switchyard-vue/.env.development.local`。例如固定到本机服务：

```powershell
$env:VITE_API_BASE_URL = 'http://localhost:7297'
npm.cmd run dev
```

macOS/Linux 使用相同 npm 命令（去掉 `.cmd`）；显式覆盖地址时可写成：

```bash
VITE_API_BASE_URL=http://localhost:7297 npm run dev
```

`VITE_API_BASE_URL` 是 **API 根 URL**，无需 `/api` 后缀。`config.ts` 优先使用它，设置后不再跟随页面主机名。没有覆盖时，开发服务使用上述同主机默认地址，生产构建使用 production JSON。若旧终端或 `.env` 文件中仍有旧地址，清除该项后重启 Vite 才会恢复默认行为。

Vite 当前只为 `/api` 配置了指向 `http://127.0.0.1:7297` 的开发代理，无法覆盖全部业务路径。前端通常直接使用 API 根地址，不依赖该代理。开发 API 的 CORS 策略允许所有 origin；生产需要显式设置允许的前端来源。

### 构建和预览

```powershell
# 在 switchyard-vue 目录执行
$env:VITE_API_BASE_URL = 'http://localhost:7297'
npm.cmd run type-check
if ($LASTEXITCODE -ne 0) { throw 'Type check failed' }
npm.cmd run build-only -- --mode production
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
npm.cmd run preview
```

预览默认端口为 `4173`，产物为 `dist/`。`npm run build` 可合并类型检查与构建；上面的分步命令也适用于 Windows 并行脚本无法启动的情况。

API 地址在构建时写入产物，预览时再设置环境变量不会替换它。当前 `config.ts` 在非开发服务器环境下选择生产配置，因此 `build:dev` / `--mode development` 也不能保证使用开发 JSON 中的 API；应显式设置 `VITE_API_BASE_URL`。

## 4. 使用 MySQL

将上述两组 `DatabaseType` 改为 `Mysql`，分别设置：

```text
HumpDatabase__MysqlConfig__Host=127.0.0.1
HumpDatabase__MysqlConfig__Port=3306
HumpDatabase__MysqlConfig__Database=<驼峰数据库名>
HumpDatabase__MysqlConfig__Username=<本机应用账号>
HumpDatabase__MysqlConfig__Password=<自行配置的密码>

CapacityDatabase__MysqlConfig__Host=127.0.0.1
CapacityDatabase__MysqlConfig__Port=3306
CapacityDatabase__MysqlConfig__Database=<能力数据库名>
CapacityDatabase__MysqlConfig__Username=<本机应用账号>
CapacityDatabase__MysqlConfig__Password=<自行配置的密码>
```

这是配置键说明，不是可直接执行的 shell 脚本。PowerShell 通过 `$env:键名 = '值'` 设置。其他可选键有 `CharSet`、`SslMode`、`ConnectionTimeout`、`AllowPublicKeyRetrieval`；端口默认 3306，字符集默认 `utf8mb4`，连接超时默认 15 秒。

账号需有初始化 schema 所需权限。启动对能力库的部分表还会核对并转换字符集/排序规则。已有数据库升级需检查列结构；`CREATE TABLE IF NOT EXISTS` 不会自动更新已存在的表。

## 5. 可选的课程资料

在启动 API 的终端覆盖：

```powershell
$env:Course__VideoDir = Join-Path $repoDir 'switchyardvid'
$env:Course__DocDir = Join-Path $repoDir 'switchyardvid'
```

目录下若包含 `站场视频` 或 `教学文档` 子目录，对应接口优先扫描这些子目录；否则扫描配置根目录。视频支持 `.mp4`、`.webm`、`.m4v`、`.mov`、`.avi`、`.mkv`，教学资料支持 `.pdf`、`.doc`、`.docx`。浏览器能否播放视频还取决于编码格式。没有资料目录时清单为空，不妨碍 API 启动。

课程清单与文件接口目前允许匿名访问。目录配置用于公开展示资料，避免把其他本地文件放入这个目录。

## 6. 可选的能力求解 Agent

安装 .NET 10 并准备可正常登录的已激活账号后，在仓库根目录新开终端：

```powershell
dotnet run --project SwitchYard.WebApi/SwitchYard.CapacityAgent/SwitchYard.CapacityAgent.csproj
```

按提示输入 `http://localhost:7297`、用户名和密码。如果账号要求首次修改密码，先在前端完成。Agent 经专用认证接口和 `/hubs/capacity-agent` 长连接注册；普通用户仅能使用自己的 Agent。

程序从输出目录的 `capacity-agent.json` 读取资源配置，也可传入 `-- --config <绝对路径>`。离线求解自检不需要连接 API：

```powershell
dotnet run --project SwitchYard.WebApi/SwitchYard.CapacityAgent/SwitchYard.CapacityAgent.csproj -- --self-test
```

更多参数、权限与日志说明见 [CapacityAgent README](../SwitchYard.WebApi/SwitchYard.CapacityAgent/README.md)。

## 7. 配置核对顺序

1. 以控制台最终监听地址为准，不根据 launch profile 推断地址。
2. 前端确认请求发往设定的 API 根 URL，并核对开发端口。
3. 确认两个数据库配置节均已覆盖；默认选择 MySQL，不能只改 SQLite 文件路径。
4. 注册失败检查模板 `001`；能力页面失败检查能力库；求解不可用检查 Agent 和共享权限。
5. 查看 API 输出目录下的 `logs/`。生产不会开放 Swagger，可用版本端点与真实业务请求验证。
