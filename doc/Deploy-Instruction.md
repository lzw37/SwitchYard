# SwitchYard.Service 生产部署说明

此目录包含会随 API 发布包一起分发的部署文件：

- `switchyard-api.service`：用于 Ubuntu 的 `systemd` 服务单元
- `switchyard-api.env.example`：环境变量模板
- `install-secrets.sh`：用于安装 `/etc/switchyard/api.env` 和 `systemd` 服务单元的脚本

## 发布

以 Release 模式发布 API。发布产物现在会包含 `scripts/deploy/` 目录。

```bash
dotnet publish SwitchYard.WebApi/SwitchYard.Service/SwitchYard.Service.csproj \
  -c Release \
  -o ./publish
```

## Ubuntu 首次安装

1. 将发布产物复制到服务器。

```bash
scp -r ./publish user@server:/tmp/switchyard-publish
```

2. 将发布包安装到 `/opt/switchyard/api`。

```bash
sudo mkdir -p /opt/switchyard/api
sudo rsync -av --delete /tmp/switchyard-publish/ /opt/switchyard/api/
```

3. 在发布包目录中运行敏感配置安装脚本。

```bash
cd /opt/switchyard/api/scripts/deploy
sudo bash install-secrets.sh
```

如果希望自动生成 JWT 签名密钥：

```bash
cd /opt/switchyard/api/scripts/deploy
sudo JWT_AUTOGEN=1 bash install-secrets.sh
```

4. 启用并启动服务。

```bash
sudo systemctl enable --now switchyard-api
sudo systemctl status switchyard-api
sudo journalctl -u switchyard-api -f
```

## 安装脚本会执行的操作

- 确保 `switchyard` 系统用户存在
- 创建权限为 `0600` 的 `/etc/switchyard/api.env`
- 创建 `/opt/switchyard/api/logs`
- 创建 `/opt/switchyard/data`
- 安装 `/etc/systemd/system/switchyard-api.service`

## 默认运行方式

- Kestrel 默认绑定到 `http://127.0.0.1:7297`
- 建议在 API 前面放置 Nginx、Caddy 或 Apache 以提供 TLS 和公网访问
- 除非显式覆盖，否则课程资源默认位于 `/data/switchyardvid`

## 手动调试启动

如果你想不通过 `systemd`，直接在工作目录中手动启动服务，可以这样做：

1. 进入发布目录。

```bash
cd /opt/switchyard/api
```

2. 如果正式服务正在运行，先停掉它，避免占用默认端口。

```bash
sudo systemctl stop switchyard-api
```

3. 加载生产环境变量，然后手动启动应用。

```bash
set -a
source /etc/switchyard/api.env
set +a
dotnet SwitchYard.Service.dll
```

如果你只是想临时调试，但不想停掉正式服务，可以改用其他端口：

```bash
cd /opt/switchyard/api
set -a
source /etc/switchyard/api.env
set +a
export WebApi__Hosts__0=http://127.0.0.1:5033
export WebApi__Hosts__1=
dotnet SwitchYard.Service.dll
```

调试结束后，如果需要恢复托管方式，可以重新启动服务：

```bash
sudo systemctl start switchyard-api
```

## 更新已有部署

```bash
dotnet publish SwitchYard.WebApi/SwitchYard.Service/SwitchYard.Service.csproj \
  -c Release \
  -o ./publish

scp -r ./publish user@server:/tmp/switchyard-publish
sudo rsync -av --delete /tmp/switchyard-publish/ /opt/switchyard/api/
sudo systemctl restart switchyard-api
```

如果敏感配置发生变化，请在重启前重新运行安装脚本：

```bash
cd /opt/switchyard/api/scripts/deploy
sudo bash install-secrets.sh
sudo systemctl restart switchyard-api
```

## 卸载服务单元和环境变量文件

```bash
cd /opt/switchyard/api/scripts/deploy
sudo bash install-secrets.sh --uninstall
```

## 运维检查清单

1. 使用专用的 MySQL 账号，不要使用 `root`。
2. 如果需要课程文件，请确认 `/data/switchyardvid` 已存在。
3. 对所有曾经提交到 Git 历史中的数据库密码或 JWT 密钥进行轮换。
4. 确保 `/etc/switchyard/api.env` 不进入版本控制，并保持 `0600` 权限。
