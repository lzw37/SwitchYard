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

## Vue 前端部署到 Nginx

`switchyard-vue` 当前使用的是 Vue Router 的 History 模式：

```ts
createWebHistory(import.meta.env.BASE_URL)
```

这意味着如果用户直接访问或刷新 `/about`、`/hump`、`/capacity` 这类前端路由，Nginx 必须把不存在的物理文件回退到 `index.html`，否则会出现 404，看起来像“路由不生效”。

### 部署在站点根路径

如果前端直接部署在域名根路径，例如 `https://example.com/`：

1. 在前端目录执行构建：

```bash
cd switchyard-vue
npm install
npm run build
```

2. 将 `dist/` 内容发布到 Nginx 站点目录，例如 `/var/www/switchyard`。

3. 在 Nginx 中配置 SPA 路由回退：

```nginx
server {
  listen 80;
  server_name example.com;

  root /var/www/switchyard;
  index index.html;

  location / {
    try_files $uri $uri/ /index.html;
  }

  location /api/ {
    proxy_pass http://127.0.0.1:7297/api/;
    proxy_http_version 1.1;
    proxy_set_header Host $host;
    proxy_set_header X-Real-IP $remote_addr;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header X-Forwarded-Proto $scheme;
  }
}
```

其中最关键的是：

```nginx
try_files $uri $uri/ /index.html;
```

### 部署在子路径

如果前端不是放在根路径，而是放在子路径，例如 `https://example.com/switchyard/`，则除了 Nginx 回退配置，还需要同步设置 Vite 的 `base`。

可以在 `switchyard-vue/vite.config.ts` 中增加：

```ts
export default defineConfig(({ mode }) => {
  return {
    base: mode === "production" ? "/switchyard/" : "/",
    // ...其余配置
  };
});
```

对应的 Nginx 配置示例：

```nginx
location /switchyard/ {
  alias /var/www/switchyard/;
  index index.html;
  try_files $uri $uri/ /switchyard/index.html;
}
```

注意：

- 前端构建产物里的静态资源路径会跟随 `base` 变化。
- `createWebHistory(import.meta.env.BASE_URL)` 已经会读取这个 `base`，所以路由前缀要和 Nginx 保持一致。
- 如果 `base` 还是默认的 `/`，但你把站点发布到了 `/switchyard/`，就会出现资源 404 或路由跳转异常。

### 不想配 Nginx 回退时的替代方案

如果不想使用 History 模式，也可以改成 Hash 模式：

```ts
import { createRouter, createWebHashHistory } from "vue-router";

const router = createRouter({
  history: createWebHashHistory(import.meta.env.BASE_URL),
  routes: [
    // ...
  ],
});
```
```

这样 URL 会变成 `/#/about`，Nginx 通常不需要额外做路由回退，但 URL 不如 History 模式干净。

### 排查顺序

1. 直接访问首页是否正常加载静态资源。
2. 刷新 `/about`、`/hump` 等地址时，Nginx 是否返回了 `index.html`。
3. 如果站点部署在子路径，检查 `vite.config.ts` 的 `base` 是否与 Nginx 路径一致。
4. 打开浏览器网络面板，确认 JS/CSS 资源是否请求到了错误路径。

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
