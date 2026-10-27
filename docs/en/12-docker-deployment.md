# Docker deployment

[Documentation index](README.md) · [中文](../zh-CN/12-docker-deployment.md) · Reviewed: 2026-10-15

This chapter covers the repository's Dockerfiles, Compose definition, and runtime configuration. It follows the same general arrangement as `nstd.ui`: a frontend Nginx container, an API container, and external MySQL. Browsers reach the frontend container through a Docker-published port, and Nginx inside that container proxies API requests. The host only needs Docker; installing Nginx on the host is unnecessary. See [Installation and first run](07-installation.md) for local source setup and [Deployment and operations](08-deployment-operations.md) for systemd deployment and detailed data maintenance.

## 1. Deployment components

```text
Browser → host port 8080 (Docker port forwarding) → frontend:80 (Nginx)
                                                     ├─ Vue assets and History routing fallback
                                                     └─ api:8080 (internal Docker network)
                                                           ├─ external MySQL, three databases
                                                           ├─ read-only course directory
                                                           └─ log, migration backup, data, and key volumes
```

| File / service | Purpose |
| --- | --- |
| [Root Dockerfile](../../Dockerfile) | Builds the specific Service project with .NET 10 SDK, publishes `net8.0`, and runs ASP.NET Core 8 Debian bookworm-slim as the non-root `app` user |
| [Frontend Dockerfile](../../switchyard-vue/Dockerfile) | Builds Vue and the local station frontend dependency using Node.js 24 stages, then serves assets and proxies requests with Nginx |
| [compose.yaml](../../compose.yaml) | Defines `api`, `frontend`, networking, health checks, and persistent mounts |
| [.env.docker.example](../../.env.docker.example) | Template to copy and populate with private deployment values |
| [Container API base configuration](../../deploy/docker/api.appsettings.json) | Clean container-specific configuration; real credentials are injected through the environment |

The build context is the **repository root**. Keep referenced projects such as `SwitchYard.StationLayout` in place. The station backend targets multiple frameworks, so the build uses .NET 10 SDK while the API runtime remains ASP.NET Core 8. Compose does not start MySQL or the interactive CapacityAgent. For model solving, follow the [Agent installation instructions](07-installation.md#7-install-capacityagent) on a computer that can reach this site, using the same API root address that browsers use to access the site.

## 2. Prerequisites

### 2.1 Docker and directories

Build and deployment hosts need Docker Engine / Docker Desktop capable of running Linux containers and Compose v2 with `docker compose up --wait` support. Check that the daemon is running:

```bash
docker version
docker compose version
```

Online builds need access to image registries, NuGet, and npm. The default architecture is `amd64`; set `SWITCHYARD_ARCH=arm64` for ARM64 hosts. Build or import images for the target architecture. Cross-architecture builds require a builder with suitable execution support.

Run commands from the repository root unless stated otherwise. First-time Linux/macOS preparation:

```bash
cp .env.docker.example .env.docker
chmod 600 .env.docker
mkdir -p switchyardvid
```

First-time PowerShell preparation:

```powershell
Copy-Item .env.docker.example .env.docker
New-Item -ItemType Directory -Force -Path switchyardvid | Out-Null
```

Edit an existing `.env.docker` directly to preserve saved keys. Restrict file access on Windows. The course bind mount source directory must already exist. An empty directory permits startup but produces an empty course listing. Copy teaching material into this directory, ensure the container user can read it, and follow [Course resource management](08-deployment-operations.md#5-course-assets).

### 2.2 External MySQL and initial data

Use MySQL 8.x with support for the `utf8mb4_0900_ai_ci` collation. Have the database administrator create three separate databases, `hump`, `capacity`, and `sketchlayout`, and grant the deployment account the required table creation, index, schema alteration, and business read/write permissions on them. See [Database preparation](08-deployment-operations.md#2-prepare-databases-and-configuration-first) for creation examples and collation requirements. Do not point all three configurations at one database.

API startup creates tables and performs some historical migrations. It **does not create databases, import template `001`, create an initial administrator, or provide a default password**. A complete first deployment requires authorized initialization data or compatible database copies containing a valid administrator, template `001`, and its related records before opening login and registration. See [Initial data preparation](07-installation.md#3-initial-data-is-part-of-installation).

For MySQL on the Docker host, use the default `host.docker.internal`; Compose adds a Linux `host-gateway` mapping. Inside a container, `localhost` means that container. MySQL listening only on `127.0.0.1` is generally unreachable from Linux containers: configure a reachable listening address, host firewall, and account host grants. For a remote database, supply a reachable hostname/IP and configure TLS as required. Container access does not require exposing MySQL to the public Internet.

## 3. Environment variables

Edit `.env.docker`. Compose reads these variables and maps them to application settings; it does not automatically pass the entire file to every service.

| Variable | Default / configuration |
| --- | --- |
| `COMPOSE_PROJECT_NAME` | `switchyard`; affects network and volume names, so keep it stable for existing deployments |
| `SWITCHYARD_ARCH` | `amd64`; use `arm64` for ARM64 |
| `SWITCHYARD_IMAGE_TAG` | `local`; used only for image names; use a version or commit identifier in production |
| `SWITCHYARD_HTTP_BIND` | `0.0.0.0`; Docker publishes the frontend port on all host network interfaces |
| `SWITCHYARD_HTTP_PORT` | `8080`; published frontend HTTP port |
| `SWITCHYARD_API_BASE_URL` | Empty; same-origin browser API at runtime; for a separate API, use an absolute root URL without `/api` |
| `SWITCHYARD_CORS_ORIGIN` | Empty; unnecessary for same-origin requests; for cross-origin requests, use the complete frontend origin such as `https://app.example.com` |
| `SWITCHYARD_DB_HOST` | `host.docker.internal`; MySQL host for all three databases |
| `SWITCHYARD_DB_PORT` | `3306` |
| `SWITCHYARD_DB_USER` | `switchyard` |
| `SWITCHYARD_DB_PASSWORD` | Required; no default password |
| `SWITCHYARD_HUMP_DB` | `hump` |
| `SWITCHYARD_CAPACITY_DB` | `capacity` |
| `SWITCHYARD_SKETCHLAYOUT_DB` | `sketchlayout` |
| `SWITCHYARD_DB_SSL_MODE` | `Preferred`; adjust to the database TLS policy |
| `SWITCHYARD_JWT_KEY` | Required; generate from at least 64 random bytes and save persistently |
| `SWITCHYARD_COURSE_PATH` | `./switchyardvid`; host directory mounted read-only at `/data/course` |

On Linux/macOS, run `openssl rand -base64 64`, then save the generated JWT key in the private environment file. PowerShell can generate an equivalent value:

```powershell
$jwtBytes = New-Object byte[] 64
$jwtRng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$jwtRng.GetBytes($jwtBytes)
$jwtRng.Dispose()
[Convert]::ToBase64String($jwtBytes)
```

Do not commit the output or real configuration or paste it into public logs. Keep the key stable across restarts; changing it invalidates existing access tokens. Single-quote environment values containing characters such as `$` or `#`, for example `SWITCHYARD_DB_PASSWORD='your-private-password'`; `$` inside single quotes is literal. Existing shell variables can override values from `--env-file`, so inspect the terminal environment when values differ unexpectedly. See [Docker's variable documentation](https://docs.docker.com/compose/how-tos/environment-variables/variable-interpolation/) for syntax and precedence.

The frontend container generates browser runtime API configuration at startup. After changing `SWITCHYARD_API_BASE_URL`, recreate the container; rebuilding the frontend bundle is unnecessary. An empty value uses the page origin. Do not use the internal service address `http://api:8080`, which browsers cannot resolve. Frontend configuration may contain public addresses only; database credentials and the JWT key are passed to the API.

## 4. Build, start, and verify

After completing required variables and preparing databases and the course directory, run:

```bash
docker compose --env-file .env.docker config --quiet
docker compose --env-file .env.docker build
docker compose --env-file .env.docker up -d --wait
docker compose --env-file .env.docker ps
docker compose --env-file .env.docker logs --tail=100 api frontend
```

Proceed only after each command succeeds. `config --quiet` validates Compose but does not connect to databases or verify business initialization. Omitting `--quiet` can print resolved passwords. `up --wait` waits for service health; inspect API logs if initial database upgrades are slow or fail.

### 4.1 Inspect one container

Prefer the Compose service name so commands do not depend on generated container names. The backend service is `api`, and the frontend service is `frontend`:

```bash
# Show one service container, including its status, health, and ports
docker compose --env-file .env.docker ps api
docker compose --env-file .env.docker ps frontend

# Include containers that have exited
docker compose --env-file .env.docker ps --all api

# Show the latest 100 log lines; add -f to follow new output
docker compose --env-file .env.docker logs --tail=100 api
docker compose --env-file .env.docker logs --tail=100 -f frontend

# Print current CPU, memory, and network usage once, then exit
docker stats --no-stream $(docker compose --env-file .env.docker ps -q api)
```

In PowerShell, use a variable for the resource check instead of Bash command substitution:

```powershell
$containerId = docker compose --env-file .env.docker ps -q api
docker stats --no-stream $containerId
```

Common `STATUS` values are `Up` / `running` for a running process, `Exited` for a stopped process, and `Restarting` for a restart loop. Containers with health checks also report `starting`, `healthy`, or `unhealthy`. `Ctrl+C` stops only the `logs -f` stream; it does not stop the container.

To work with a Docker container name directly, list the names first and then inspect the selected container:

```bash
docker ps --all --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"
docker inspect switchyard-api-1
docker logs --tail=100 switchyard-api-1
```

With the default `COMPOSE_PROJECT_NAME=switchyard`, the generated names are normally `switchyard-api-1` and `switchyard-frontend-1`. If the project name or replica count changes, use the actual name shown by `docker compose ... ps`.

Open `http://localhost:8080` on the deployment host, or its host address from another computer. Check the version:

```bash
curl -f http://localhost:8080/api/System/version
```

PowerShell can use `Invoke-RestMethod 'http://localhost:8080/api/System/version'`. The API listens on internal port `8080`; Compose publishes no API host port, so requests pass through frontend Nginx. Image names are `switchyard-api:<tag>-<architecture>` and `switchyard-web:<tag>-<architecture>`, defaulting to `local-amd64`. The version endpoint retains `App:Version` from the [original appsettings.json](../../SwitchYard.WebApi/SwitchYard.Service/appsettings.json), independently of the image tag. The build extracts only this version field into the container configuration. Rebuild the API image after changing the original version; no second version value needs maintenance.

Health checks use the existing version endpoint. They establish that processes and proxy respond, not that templates, administrators, courses, Agents, or all business data are valid. Before release, use test data to verify login, template copying, hump instances, station layouts and operation plans, SketchLayout, course streaming, and required Agent solving. Confirm saved data survives container recreation. Swagger is unavailable in production.

## 5. Proxy directly from the frontend container

API matching is case-insensitive: both `/Hump/GetFlatLayout` and `/hump/getflatlayout` are forwarded to the backend. Frontend pages sharing these prefixes (`/hump`, `/hump/instancemanagement`, `/capacity`, and `/sketchlayout`) take precedence and serve the Vue entry page, including optional trailing slashes. When adding similar frontend routes, update the [Nginx page exceptions](../../switchyard-vue/docker/default.conf.template) so page refreshes do not reach the API.

Once services are running, use Python 3.10+ for the [proxy regression check](../../scripts/check-docker-proxy.py): `python scripts/check-docker-proxy.py --base-url http://localhost:8080`. It makes only anonymous GET requests to check API casing, page fallback, and unknown API 404 responses. Protected endpoints should return 401 with a Bearer authentication header; the script does not read account or business data.

The default configuration already connects Nginx in the frontend container directly to the API container. Keep these values in `.env.docker`, and complete the database and key settings described in section 3:

```dotenv
SWITCHYARD_HTTP_BIND=0.0.0.0
SWITCHYARD_HTTP_PORT=8080
SWITCHYARD_API_BASE_URL=
SWITCHYARD_CORS_ORIGIN=
```

For an existing deployment, update the source and configuration, then rebuild and start:

```bash
docker compose --env-file .env.docker up -d --build --wait
```

Open `http://server-address:8080`. The browser requests the API from the page origin, for example `http://server-address:8080/api/System/version`. Docker forwards host port `8080` to port `80` in the frontend container; Nginx forwards business requests to `http://api:8080` through the Compose network. The backend needs no published host port. To change only the public port, edit `SWITCHYARD_HTTP_PORT` and run `docker compose --env-file .env.docker up -d --wait`; frontend container port `80` and backend port `8080` remain unchanged.

Compose fixes the frontend container's internal `SWITCHYARD_BACKEND_URL` variable to `http://api:8080` for Nginx; Docker's internal DNS resolves the `api` service name. Do not put this address in browser `SWITCHYARD_API_BASE_URL` or Agent configuration. Clients should use `http://server-address:8080`. Leave `SWITCHYARD_API_BASE_URL` and `SWITCHYARD_CORS_ORIGIN` empty for same-origin requests.

Container Nginx provides frontend History routing fallback and preserves these API paths: `/api/`, `/Hump/`, `/Capacity/`, `/StationLayout/`, `/OperationPlan/`, `/OperationProcess/`, `/SketchLayout/`, `/Course/`, and `/hubs/`. It supports WebSockets, course Range requests, and a `25m` request body limit. Application upload limits still apply. Nginx sets `X-Forwarded-Proto` from the `$scheme` of the request it receives. `proxy_pass` has no URI component and preserves business paths; see the [Nginx documentation](https://nginx.org/en/docs/http/ngx_http_proxy_module.html#proxy_pass). The site is served at the root path; subpath hosting also requires frontend build and routing changes.

The current configuration provides HTTP. HTTPS would require a separate port `443` listener, certificate mounts, and a published port in the frontend container; these settings are not included yet.

`SWITCHYARD_COURSE_PATH` is mounted read-only and maintained on the host. Anonymous course endpoints expose matching content in that directory, so include teaching material intended for site visitors. See the [user guide](02-user-guide.md#8-organizing-teaching-resources) for directory organization and supported extensions.

## 6. Persistence, backups, updates, and rollback

| Object | Default location / handling |
| --- | --- |
| Three business databases | External MySQL; use consistent database backups and record the application version |
| Course files | Host `SWITCHYARD_COURSE_PATH`; back up the entire directory tree |
| API logs | `api-logs` volume → `/app/logs` |
| Local artifacts including DWG imports | `api-local-data` volume → `/app/LocalData` |
| Automatic migration backups | `migration-backups` volume → `/app/migration-backups`; do not replace full database backups |
| Application data directory | `api-data` volume → `/data`; default MySQL data is outside this volume |
| ASP.NET Data Protection keys | `data-protection-keys` volume → `/home/app/.aspnet/DataProtection-Keys` |
| Private deployment configuration | `.env.docker`, custom Compose overrides, and custom Nginx configuration |

Actual volume names generally include the Compose project prefix, for example `switchyard_api-logs`. Changing `COMPOSE_PROJECT_NAME` selects another set of volumes; it does not establish that previous data has disappeared. The default setup uses external MySQL. A custom SQLite setup requires separate overrides for all three database types and distinct file paths, valid data, and verified `/data` write access; changing database name variables is insufficient. See [Backup and recovery](08-deployment-operations.md#61-backup-scope-and-recovery-rehearsal) for SQLite consistency requirements.

Before an update, let calculation tasks finish, download Agent results that must be retained, back up databases, configuration, courses, persistent volumes, and old images, and trial migrations on isolated database copies. Update source and set `SWITCHYARD_IMAGE_TAG` to the new version or commit:

```bash
docker compose --env-file .env.docker build
docker compose --env-file .env.docker up -d --wait
docker compose --env-file .env.docker logs --tail=100 api frontend
```

Use `up -d --wait` to recreate affected containers after environment changes; `restart` alone does not reload Compose environment configuration. Verify the version and business behavior, and retain old images and backups for rollback. To roll back, restore the old tagged images, configuration, and a database snapshot compatible with that release, then run `up -d --no-build --wait`. Switching images alone cannot undo migrations.

Use `docker compose --env-file .env.docker stop` to stop services. `docker compose --env-file .env.docker down` removes containers and the default network while retaining named volumes. **`down -v` deletes Compose-managed named volumes**; do not add it for routine updates or shutdown. External MySQL and course bind mount data are not removed by ordinary `down`.

## 7. Offline delivery

Build for the target architecture on an online machine using the tag and architecture configured in `.env.docker`, then check the resolved image names:

```bash
docker compose --env-file .env.docker build
docker compose --env-file .env.docker config --images
```

Proceed only after the build succeeds. With the default `SWITCHYARD_IMAGE_TAG=local` and `SWITCHYARD_ARCH=amd64`, the image names are `switchyard-api:local-amd64` and `switchyard-web:local-amd64`. Export the names resolved by Compose instead of manually substituting a date or version:

**PowerShell**

```powershell
$dockerImages = @(docker compose --env-file .env.docker config --images)
if ($LASTEXITCODE -ne 0) { throw 'Failed to resolve Compose image names.' }
docker image save -o switchyard-images.tar @dockerImages
```

**Bash**

```bash
if docker_images=$(docker compose --env-file .env.docker config --images); then
    docker image save -o switchyard-images.tar $docker_images
fi
```

These commands automatically use the configured image names. If you only have source code, build first: `docker image save` exports existing local images and does not build images or create new tags. Confirm the export succeeds before delivering the archive.

Deliver the image archive, `compose.yaml`, environment template, and this guide to the server. Provision the real `.env.docker`, course directory, and external databases through controlled channels. With Docker and Compose already installed, run from the delivery directory:

```bash
docker image load -i switchyard-images.tar
docker compose --env-file .env.docker config --quiet
docker compose --env-file .env.docker up -d --no-build --pull never --wait
```

Set the deployment server's `SWITCHYARD_IMAGE_TAG` and `SWITCHYARD_ARCH` to the same values used on the build machine so that Compose selects the imported images. Image tags are independent of API `App:Version`; the application retains the version in the original `appsettings.json`. The API address is configurable at runtime. Image archives exclude databases, course files, environment files, and volume contents; back up and restore those separately. Running offline with `--no-build` does not require source code. Rebuilding requires the complete source tree and access to dependencies.

## 8. Troubleshooting and acceptance scope

| Symptom | Checks |
| --- | --- |
| `docker version` cannot reach the server | Start Docker Desktop / Engine; check the Docker context and Linux container mode |
| Compose reports an empty required variable | Populate the database password and JWT key, using the same `--env-file` for all commands |
| `No such image: switchyard-api:...` or `switchyard-web:...` | Check names with `docker compose --env-file .env.docker config --images`; build successfully with the same environment file, then export using section 7. Changing the name in a save command does not create that image tag |
| Course bind mount is missing | Create the host directory referenced by `SWITCHYARD_COURSE_PATH` and check its path |
| API is unhealthy or fails to start | Inspect three database addresses, permissions, collations, migration errors, and directory write access |
| Page loads but login/registration fails | Check the administrator, account activation, template `001`, and related records, beyond health status |
| Browser uses an old API address | Check runtime values and browser cache, then recreate the frontend with `up` |
| Layout Design has no canvas when opened over HTTP through a server IP | Check the browser console; an old frontend image may report `crypto.randomUUID is not a function`. Build and load the current frontend image, then recreate it with `up -d --no-build --pull never --force-recreate frontend` |
| Agent connection issues | Use `http://server-address:public-port` as the Agent root URL; check frontend container logs, WebSocket upgrade, and host port reachability |
| Previous data appears missing after an update | Check Compose project name, external database names, volumes, and course source paths |

This chapter documents code and configuration conventions, not acceptance results for a particular server. Record image versions, architecture, Compose validation, container health, database initialization, and representative business results for each deployment. If the Docker daemon or business databases are unavailable, do not report successful container end-to-end validation.
