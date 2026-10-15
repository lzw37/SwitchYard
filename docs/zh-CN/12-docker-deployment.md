# Docker 部署

[文档目录](README.md) · [English](../en/12-docker-deployment.md) · 核对日期：2026-10-15

本章对应仓库的 Dockerfile、Compose 和运行时配置，采用与 `nstd.ui` 类似的“前端 Nginx + 后端 API + 外部 MySQL”部署方式。浏览器通过 Docker 发布的端口直接访问前端容器，由容器内的 Nginx 代理后端 API；宿主机只需运行 Docker，无需安装 Nginx。源码本机启动见[安装与首次启动](07-installation.md)，systemd 部署及详细数据维护见[部署与运维](08-deployment-operations.md)。

## 1. 部署组成

```text
浏览器 → 宿主机端口 8080（Docker 端口转发）→ frontend:80（Nginx）
                                             ├─ Vue 静态资源与 History 路由回退
                                             └─ api:8080（Docker 内网）
                                                   ├─ 外部 MySQL 三库
                                                   ├─ 只读课程目录
                                                   └─ 日志、迁移备份、数据及密钥卷
```

| 文件 / 服务 | 用途 |
| --- | --- |
| [根 Dockerfile](../../Dockerfile) | .NET 10 SDK 构建指定 Service 项目，发布 `net8.0`，使用 ASP.NET Core 8 Debian bookworm-slim 运行镜像和非 root `app` 用户 |
| [前端 Dockerfile](../../switchyard-vue/Dockerfile) | Node.js 24 多阶段构建 Vue 与本地站场前端依赖，使用 Nginx 提供静态页面和反向代理 |
| [compose.yaml](../../compose.yaml) | 编排 `api`、`frontend`、网络、健康检查和持久化挂载 |
| [.env.docker.example](../../.env.docker.example) | 部署环境变量模板；复制后填写私有配置 |
| [容器 API 基础配置](../../deploy/docker/api.appsettings.json) | 容器专用的干净配置，实际凭据通过环境变量注入 |

构建上下文是**仓库根目录**，必须保留 `SwitchYard.StationLayout` 等被引用的项目。站场后端包含多目标框架，构建阶段采用 .NET 10 SDK；运行 API 的镜像仍是 ASP.NET Core 8。Compose 不启动 MySQL，也不启动交互式 CapacityAgent。需要模型求解时，按[Agent 安装说明](07-installation.md#7-安装-capacityagent)在可访问本站的计算机单独运行 Agent，API 根地址填写浏览器访问本站所用的地址。

## 2. 部署前准备

### 2.1 Docker 与目录

构建机和部署机需要能运行 Linux 容器的 Docker Engine / Docker Desktop，以及支持 `docker compose up --wait` 的 Compose v2。先检查守护进程是否已启动：

```bash
docker version
docker compose version
```

在线构建需要访问镜像源、NuGet 和 npm。默认架构是 `amd64`，ARM64 主机在环境文件设置 `SWITCHYARD_ARCH=arm64`；为目标架构构建或导入对应镜像，跨架构构建要求构建器具备相应执行支持。

以下命令除特别说明外都在仓库根目录运行。Linux/macOS 首次准备：

```bash
cp .env.docker.example .env.docker
chmod 600 .env.docker
mkdir -p switchyardvid
```

PowerShell 首次准备：

```powershell
Copy-Item .env.docker.example .env.docker
New-Item -ItemType Directory -Force -Path switchyardvid | Out-Null
```

已有 `.env.docker` 时直接编辑，避免覆盖已保存的密钥。Windows 上限制此文件的访问权限。课程 bind mount 的源目录必须预先存在；目录为空时可以启动，课程清单也为空。将实际教学资料复制到目录后，核对容器用户有读取权限，组织规则见[课程资料管理](08-deployment-operations.md#5-课程资料管理)。

### 2.2 外部 MySQL 与初始数据

使用支持 `utf8mb4_0900_ai_ci` 排序规则的 MySQL 8.x。由数据库管理员预建互相独立的 `hump`、`capacity`、`sketchlayout` 三库，并向部署账号授予这三库所需的建表、索引、改表和业务读写权限。建库示例和排序规则要求见[数据库准备](08-deployment-operations.md#2-先准备数据库和运行配置)。不要用同一个库承载三组配置。

API 启动会建表和执行部分历史数据迁移，**不会创建数据库、导入模板 `001`、创建初始管理员或提供默认密码**。首次完整部署须取得经授权、兼容当前版本的初始化数据或数据库副本，准备有效管理员、模板 `001` 及其关联数据，再开放登录和注册。具体前提见[首次数据准备](07-installation.md#3-首次数据准备是安装的一部分)。

数据库在 Docker 宿主机时可用默认 `host.docker.internal`；Compose 为 Linux 增加了 `host-gateway` 映射。容器里的 `localhost` 指向容器自身。MySQL 只监听 `127.0.0.1` 时通常无法从 Linux 容器连接，须配置容器可达的监听地址、主机防火墙和账号来源授权。远程数据库则填实际可达的主机名/IP，并按数据库要求设置 TLS；不应为了容器访问直接把 MySQL 暴露到公网。

## 3. 环境变量

编辑 `.env.docker`。下表变量由 Compose 读取并映射为应用配置；文件不会自动作为完整环境传入所有服务。

| 变量 | 默认值 / 配置说明 |
| --- | --- |
| `COMPOSE_PROJECT_NAME` | `switchyard`；影响网络和卷名，已有部署应保持稳定 |
| `SWITCHYARD_ARCH` | `amd64`；ARM64 使用 `arm64` |
| `SWITCHYARD_IMAGE_TAG` | `local`；仅用于镜像命名，生产建议填版本号或提交号 |
| `SWITCHYARD_HTTP_BIND` | `0.0.0.0`；Docker 在宿主机所有网络接口发布前端端口 |
| `SWITCHYARD_HTTP_PORT` | `8080`；对外发布的前端 HTTP 端口 |
| `SWITCHYARD_API_BASE_URL` | 空；浏览器同源 API，运行时生效；独立 API 时填写不带 `/api` 的绝对根 URL |
| `SWITCHYARD_CORS_ORIGIN` | 空；同源无需配置；跨域时填写前端完整 origin，例如 `https://app.example.com` |
| `SWITCHYARD_DB_HOST` | `host.docker.internal`；三库所在 MySQL 主机 |
| `SWITCHYARD_DB_PORT` | `3306` |
| `SWITCHYARD_DB_USER` | `switchyard` |
| `SWITCHYARD_DB_PASSWORD` | 必填，无默认密码 |
| `SWITCHYARD_HUMP_DB` | `hump` |
| `SWITCHYARD_CAPACITY_DB` | `capacity` |
| `SWITCHYARD_SKETCHLAYOUT_DB` | `sketchlayout` |
| `SWITCHYARD_DB_SSL_MODE` | `Preferred`；按数据库 TLS 策略调整 |
| `SWITCHYARD_JWT_KEY` | 必填；使用至少 64 个随机字节生成并持久保存的签名密钥 |
| `SWITCHYARD_COURSE_PATH` | `./switchyardvid`；宿主机目录，只读挂载至 `/data/course` |

Linux/macOS 可运行 `openssl rand -base64 64` 生成 JWT 密钥，再保存到私有环境文件。PowerShell 可生成同等随机值：

```powershell
$jwtBytes = New-Object byte[] 64
$jwtRng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$jwtRng.GetBytes($jwtBytes)
$jwtRng.Dispose()
[Convert]::ToBase64String($jwtBytes)
```

不要把命令输出和真实配置提交到仓库或粘贴进公开日志。密钥应跨重启保持稳定，更换密钥会使现有访问令牌失效。环境文件中用单引号包围包含 `$` 或 `#` 等特殊字符的值，例如 `SWITCHYARD_DB_PASSWORD='实际私有密码'`；单引号内的 `$` 不参与 Compose 插值。已有 shell 环境变量可覆盖 `--env-file` 中同名变量，发现配置不符时检查终端环境。语法和优先级见 [Docker 官方变量说明](https://docs.docker.com/compose/how-tos/environment-variables/variable-interpolation/)。

前端容器启动时生成浏览器运行时 API 配置，所以修改 `SWITCHYARD_API_BASE_URL` 后重建容器即可，无需重新打包前端。空值使用当前页面 origin；不要填 Docker 内部的 `http://api:8080`，浏览器无法解析该服务名。前端配置中只能放可公开的地址，数据库密码和 JWT 密钥仅传给 API。

## 4. 构建、启动与验收

填写必填变量、准备数据库和课程目录后执行：

```bash
docker compose --env-file .env.docker config --quiet
docker compose --env-file .env.docker build
docker compose --env-file .env.docker up -d --wait
docker compose --env-file .env.docker ps
docker compose --env-file .env.docker logs --tail=100 api frontend
```

每一步成功后再执行下一步。`config --quiet` 仅校验 Compose，不连接数据库、不验证业务初始化；省略 `--quiet` 可能将已解析的密码写到终端。`up --wait` 会等待服务健康，首次数据库升级较慢或失败时，检查 API 日志后处理。

### 4.1 查看单个容器的运行状态

优先按 Compose 服务名查看，这样无需依赖自动生成的容器名。后端服务名是 `api`，前端服务名是 `frontend`：

```bash
# 查看某个服务对应容器的状态、健康状态和端口
docker compose --env-file .env.docker ps api
docker compose --env-file .env.docker ps frontend

# 包括已经退出的容器
docker compose --env-file .env.docker ps --all api

# 查看最近 100 行日志；加入 -f 可持续跟踪
docker compose --env-file .env.docker logs --tail=100 api
docker compose --env-file .env.docker logs --tail=100 -f frontend

# 查看某个服务当前的 CPU、内存和网络占用，然后自动退出
docker stats --no-stream $(docker compose --env-file .env.docker ps -q api)
```

PowerShell 查看资源占用时使用变量，避免依赖 Bash 的命令替换语法：

```powershell
$containerId = docker compose --env-file .env.docker ps -q api
docker stats --no-stream $containerId
```

`STATUS` 中常见的 `Up` / `running` 表示进程正在运行，`Exited` 表示已经退出，`Restarting` 表示反复重启。带健康检查的容器还会显示 `starting`、`healthy` 或 `unhealthy`。`Ctrl+C` 只会结束 `logs -f` 的日志跟踪，不会停止容器。

需要按 Docker 容器名排查时，先列出名称，再使用该名称查看详细状态：

```bash
docker ps --all --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"
docker inspect switchyard-api-1
docker logs --tail=100 switchyard-api-1
```

默认 `COMPOSE_PROJECT_NAME=switchyard` 时容器名通常是 `switchyard-api-1` 和 `switchyard-frontend-1`；如果修改了项目名或副本数，以 `docker compose ... ps` 的实际输出为准。

默认在部署机打开 `http://localhost:8080`，其他电脑使用宿主机地址。版本检查：

```bash
curl -f http://localhost:8080/api/System/version
```

PowerShell 可使用 `Invoke-RestMethod 'http://localhost:8080/api/System/version'`。API 仅监听容器内部 `8080`，Compose 不发布 API 的宿主机端口；所有访问经过前端 Nginx。镜像分别命名为 `switchyard-api:<标记>-<架构>` 和 `switchyard-web:<标记>-<架构>`，默认是 `local-amd64`。版本接口沿用 [原始 appsettings.json](../../SwitchYard.WebApi/SwitchYard.Service/appsettings.json) 中的 `App:Version`，与镜像标记无关。构建时只提取该版本字段写入容器配置；修改原始版本后重新构建 API 镜像即可生效，无需维护另一份版本值。

健康检查基于现有版本接口，只证明进程及代理可响应，不证明模板、管理员、课程、Agent 或所有业务数据正确。上线前还需用测试数据验证登录、模板复制、驼峰实例、站场和作业计划、SketchLayout、课程流式读取及所需 Agent 求解，并确认重建容器后保存的数据仍可读取。生产环境不提供 Swagger。

## 5. 前端容器直接代理后端

API 路径匹配不区分大小写，例如 `/Hump/GetFlatLayout` 与 `/hump/getflatlayout` 都转发到后端。共享此前缀的前端页面 `/hump`、`/hump/instancemanagement`、`/capacity`、`/sketchlayout` 会优先返回 Vue 首页，兼容尾部斜杠。新增此类前端路由时，同步更新 [Nginx 页面例外](../../switchyard-vue/docker/default.conf.template)，避免页面刷新被转发到 API。

服务启动后，可用 Python 3.10+ 运行 [代理回归检查](../../scripts/check-docker-proxy.py)：`python scripts/check-docker-proxy.py --base-url http://localhost:8080`。脚本仅执行匿名 GET，检查大小写 API、页面回退和未知 API 的 404；受保护 API 预期返回带 Bearer 认证头的 401，不读取账号或业务数据。

默认配置已经由前端容器的 Nginx 直接访问后端容器。在 `.env.docker` 中保留以下值，并填写第 3 节所需的数据库和密钥配置：

```dotenv
SWITCHYARD_HTTP_BIND=0.0.0.0
SWITCHYARD_HTTP_PORT=8080
SWITCHYARD_API_BASE_URL=
SWITCHYARD_CORS_ORIGIN=
```

已有部署更新源码和配置后，重新构建并启动：

```bash
docker compose --env-file .env.docker up -d --build --wait
```

随后访问 `http://服务器地址:8080`。浏览器以当前页面 origin 请求 API，例如 `http://服务器地址:8080/api/System/version`；Docker 把宿主机 `8080` 转发到前端容器的 `80`，Nginx 再通过 Compose 网络将业务请求转发到 `http://api:8080`。后端无需发布宿主机端口。只需改变对外端口时，修改 `SWITCHYARD_HTTP_PORT` 并执行 `docker compose --env-file .env.docker up -d --wait`，前端容器内的 `80` 和后端的 `8080` 保持不变。

Compose 将前端容器内部变量 `SWITCHYARD_BACKEND_URL` 固定为 `http://api:8080`，供 Nginx 使用；服务名 `api` 由 Docker 内部 DNS 解析。该地址不能填入浏览器的 `SWITCHYARD_API_BASE_URL` 或 Agent 配置，客户端应使用 `http://服务器地址:8080`。保持 `SWITCHYARD_API_BASE_URL` 和 `SWITCHYARD_CORS_ORIGIN` 为空即可使用同源请求。

容器 Nginx 提供前端 History 路由回退，并保留以下 API 路径：`/api/`、`/Hump/`、`/Capacity/`、`/StationLayout/`、`/OperationPlan/`、`/OperationProcess/`、`/SketchLayout/`、`/Course/` 和 `/hubs/`。代理支持 WebSocket、课程 Range 请求和 `25m` 请求体上限；应用自身的上传限制仍然生效。Nginx 使用自身接收请求的 `$scheme` 设置 `X-Forwarded-Proto`。`proxy_pass` 不附带 URI，保留原始业务路径，参见 [Nginx 官方说明](https://nginx.org/en/docs/http/ngx_http_proxy_module.html#proxy_pass)。部署入口位于站点根路径，子路径部署还需调整前端构建与路由设置。

当前配置提供 HTTP。如需 HTTPS，需要另外为前端容器配置 `443` 监听、证书挂载和端口发布；本配置尚未包含这些内容。

`SWITCHYARD_COURSE_PATH` 挂载为只读，课程在宿主机维护。允许匿名访问的课程接口会公开该目录下符合规则的内容；只放可供站点访客读取的教学资料。课程组织与扩展名见[用户指南](02-user-guide.md#8-教学资源的组织要求)。

## 6. 持久化、备份、更新与回滚

| 对象 | 默认位置 / 处理方式 |
| --- | --- |
| 三组业务数据库 | 外部 MySQL；使用数据库一致性备份并记录对应应用版本 |
| 课程文件 | 宿主机 `SWITCHYARD_COURSE_PATH`；备份完整目录树 |
| API 日志 | `api-logs` 卷 → `/app/logs` |
| DWG 导入等本地产物 | `api-local-data` 卷 → `/app/LocalData` |
| 自动迁移备份 | `migration-backups` 卷 → `/app/migration-backups`；不能替代三库完整备份 |
| 应用数据目录 | `api-data` 卷 → `/data`；默认 MySQL 数据不存放在此卷 |
| ASP.NET Data Protection 密钥 | `data-protection-keys` 卷 → `/home/app/.aspnet/DataProtection-Keys` |
| 私有部署配置 | `.env.docker`、Compose 自定义覆盖文件及自定义 Nginx 配置 |

实际卷名通常包含 Compose 项目前缀，例如 `switchyard_api-logs`。更改 `COMPOSE_PROJECT_NAME` 会选择另一组卷，不能据此认为旧数据已丢失。默认方案使用外部 MySQL；若自行改用 SQLite，必须另外覆盖三组数据库类型与独立文件路径、准备有效数据并验证 `/data` 写权限，不能只修改库名变量。SQLite 一致性备份要求见[备份与恢复](08-deployment-operations.md#61-备份范围与恢复演练)。

更新前让计算任务结束，下载需要保留的 Agent 原始结果，备份三库、配置、课程、持久卷和旧镜像，并在隔离数据库副本上试运行迁移。随后更新源码，将 `SWITCHYARD_IMAGE_TAG` 改为新的版本或提交号：

```bash
docker compose --env-file .env.docker build
docker compose --env-file .env.docker up -d --wait
docker compose --env-file .env.docker logs --tail=100 api frontend
```

修改运行环境后使用 `up -d --wait` 重建受影响容器；仅 `restart` 不会重新读取 Compose 环境配置。检查版本和业务后保留回滚所需的旧镜像及备份。回滚时恢复旧标记对应镜像、配置和与其兼容的数据库快照，再执行 `up -d --no-build --wait`。只切换镜像不能撤销数据库迁移。

停止服务可执行 `docker compose --env-file .env.docker stop`。移除容器和默认网络可执行 `docker compose --env-file .env.docker down`，命名卷仍保留。**`down -v` 会删除 Compose 管理的命名卷**；日常更新和停机不要加此参数。外部 MySQL 与课程 bind mount 不因普通 `down` 而删除。

## 7. 离线交付

在有网络的构建机上，按 `.env.docker` 中的镜像标签和目标架构构建，再查看 Compose 实际使用的镜像名：

```bash
docker compose --env-file .env.docker build
docker compose --env-file .env.docker config --images
```

默认 `SWITCHYARD_IMAGE_TAG=local`、`SWITCHYARD_ARCH=amd64` 对应 `switchyard-api:local-amd64` 和 `switchyard-web:local-amd64`。`docker image save` 只导出已有镜像，不会创建新标签；不要把其他日期或应用版本直接替换进镜像名。以下命令自动读取已解析的 Compose 镜像名，避免导出名称与构建配置不一致。

**PowerShell：**

```powershell
$dockerImages = @(docker compose --env-file .env.docker config --images)
if ($LASTEXITCODE -ne 0) { throw 'Failed to resolve Compose image names.' }
docker image save -o switchyard-images.tar @dockerImages
```

**Bash：**

```bash
if docker_images=$(docker compose --env-file .env.docker config --images); then
    docker image save -o switchyard-images.tar $docker_images
fi
```

把镜像归档、`compose.yaml`、环境模板及本章说明交付到服务器；通过受控方式准备实际 `.env.docker`、课程目录和外部数据库。服务器已有 Docker 和 Compose 后，在交付目录执行：

```bash
docker image load -i switchyard-images.tar
docker compose --env-file .env.docker config --quiet
docker compose --env-file .env.docker up -d --no-build --pull never --wait
```

部署机环境文件中的镜像标签与架构须与构建机一致，匹配导入镜像。镜像标签独立于 API 的 `App:Version`，应用版本仍取自原始配置文件。API URL 可在运行时配置。镜像归档不包含数据库、课程资料、环境文件或卷内容，恢复这些数据需要单独备份和导入。离线 `--no-build` 运行不需要源码；重新构建需要完整源码和依赖获取条件。

## 8. 常见问题与验收范围

| 现象 | 核对内容 |
| --- | --- |
| `docker version` 无法连接服务端 | 启动 Docker Desktop / Engine，确认当前 context 和 Linux 容器模式 |
| Compose 提示必填变量为空 | 填写数据库密码和 JWT 密钥，所有命令使用同一 `--env-file` |
| `No such image` | 用 `docker compose --env-file .env.docker config --images` 核对实际标签；先构建或导入对应镜像，再按本章自动读取名称导出 |
| 课程 bind mount 不存在 | 预先创建 `SWITCHYARD_COURSE_PATH` 对应宿主机目录并检查路径 |
| API 不健康或启动失败 | 查看三库地址、权限、排序规则、数据迁移异常和目录写权限 |
| 能打开页面但登录/注册失败 | 检查管理员、账号激活、模板 `001` 及关联数据，不能只看健康状态 |
| 页面请求旧 API 地址 | 检查运行时变量与浏览器缓存，执行 `up` 重建前端容器 |
| 通过服务器 IP 的 HTTP 地址打开“布置图设计”但画布为空 | 查看浏览器 Console；旧前端镜像可能报 `crypto.randomUUID is not a function`。重新构建并加载当前前端镜像，再用 `up -d --no-build --pull never --force-recreate frontend` 重建前端容器 |
| Agent 连接异常 | Agent 根 URL 使用 `http://服务器地址:对外端口`；检查前端容器日志、WebSocket 升级和宿主机端口可达性 |
| 更新后找不到旧数据 | 检查是否改变 Compose 项目名、外部数据库名、卷和课程源路径 |

本章描述代码和配置约定，不构成某台服务器的部署验收记录。实际交付需记录镜像版本、架构、Compose 校验结果、容器健康、三库初始化和代表性业务结果；无法启动 Docker 守护进程或连接业务数据库时，不应声称完成容器端到端验证。
