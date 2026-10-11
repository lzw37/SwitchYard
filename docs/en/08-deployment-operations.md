# Deployment, upgrades, and operations

[Documentation index](README.md) · [中文](../zh-CN/08-deployment-operations.md) · Reviewed: 2026-10-11

This chapter follows current source and the repository's Ubuntu/systemd deployment scripts. See [Installation and configuration](07-installation.md) for local setup and initial Agent installation. The [original deployment guide](../archive/guides/Deploy-Instruction.md), [operations guide](../archive/guides/Operations-Recommendations.md), and [SQLite-to-MySQL document](../archive/guides/SQLite-to-MySQL-Migration.md) are preserved. Their historical row counts, two-database architecture, and early migration proposals are not current deployment facts.

## 1. Deployment layout and artifacts

The example places the frontend at `https://app.example.com` and the API at `https://api.example.com`, with TLS terminated by a reverse proxy. Replace example domains and paths for your environment.

| Artifact | Location / requirement |
| --- | --- |
| `switchyard-vue/dist/` | Static Web server with History-route fallback |
| API publish output | `/opt/switchyard/api`; ASP.NET Core 8 Runtime |
| Private API configuration | `/etc/switchyard/api.env`, loaded by systemd |
| Three databases | Hump, capacity, and SketchLayout managed separately |
| API logs | `/opt/switchyard/api/logs` |
| SQLite data | `/opt/switchyard/data`, when SQLite is selected |
| Course assets | Readable by the API service account, e.g. `/data/switchyardvid` |
| Optional Agent | .NET 10 Runtime and HTTPS/SignalR access to the API |

Use .NET 10 SDK and a Node.js version matching frontend `engines` on the build machine. The API's `net8.0` target does not mean the whole repository builds with only .NET 8 SDK. The solution includes missing or untracked test projects; publish explicit application projects for deployment.

The installer does not install .NET, MySQL, or Nginx, create business databases, import templates, or create the first administrator. API output must contain `Database/*.sql` and `scripts/deploy/`.

## 2. Prepare databases and configuration first

The complete system uses three separate sections: `HumpDatabase`, `CapacityDatabase`, and `SketchLayoutDatabase`. Users and Refresh Tokens live in the hump database; plans, processes, and solve presets in capacity; SketchLayout schemes in the third database.

A database administrator must provision and grant access to MySQL databases before startup. This example contains database names and charsets only, with no credentials:

```sql
CREATE DATABASE IF NOT EXISTS hump
  CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;
CREATE DATABASE IF NOT EXISTS capacity
  CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE DATABASE IF NOT EXISTS sketchlayout
  CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
```

The target must support the chosen collations. The hump migration tool specifically uses `utf8mb4_0900_ai_ci`. Grant access to the actual databases, including table/index creation, required alterations, and business reads/writes. API startup performs DDL and historical data migrations, not just a connection check.

For SQLite, configure separate `/opt/switchyard/data/hump.db`, `capacity.db`, and `sketchlayout.db` files. Create the parent directory and allow `switchyard` to write it. Do not point the sections at the same file or place active databases inside the replaceable API publish directory.

First deployment also requires template `001`, its related records, and a valid administrator. Schema initialization does not supply them; see [initial data preparation](07-installation.md#3-initial-data-is-part-of-installation).

Configuration merges base JSON, environment JSON, environment variables/command-line values, and other applicable providers. `WebApi:Hosts` controls actual binding; production normally uses loopback port `7297` and an empty second entry. Maintain the complete CORS origin list: overriding array index 0 does not remove later production entries.

## 3. Publish and install the API service

From the repository root on the build machine:

```bash
dotnet publish SwitchYard.WebApi/SwitchYard.Service/SwitchYard.Service.csproj \
  -c Release -o ./publish/api
scp -r ./publish/api deploy-user@server:/tmp/switchyard-api-publish
```

Before delivery, check that output contains no local private settings, business databases, or unintended materials. Supply your own private environment configuration rather than reusing checked-in development connection values.

On the Ubuntu server:

```bash
sudo mkdir -p /opt/switchyard/api
sudo rsync -av /tmp/switchyard-api-publish/ /opt/switchyard/api/
cd /opt/switchyard/api/scripts/deploy
sudo JWT_AUTOGEN=1 bash install-secrets.sh
```

The script creates the `switchyard` system account, log/data directories, an environment file with mode `0600`, and the systemd unit. `JWT_AUTOGEN=1` requires `openssl`; otherwise enter your own long random signing key when prompted.

**Actual installer behavior:** it prompts for hump and capacity database names but also writes a third `SketchLayoutDatabase` section with the hard-coded database name `sketchlayout`. All three sections initially use the supplied MySQL account. A prompt referring to “both databases” does not remove the third database requirement. Edit the complete configuration before startup if database names or accounts differ.

```bash
sudoedit /etc/switchyard/api.env
sudo systemctl enable --now switchyard-api
sudo systemctl status switchyard-api
sudo journalctl -u switchyard-api -n 100 --no-pager
curl -f http://127.0.0.1:7297/api/System/version
```

Check all database sections, JWT, course directories, CORS, `WebApi__Hosts__0=http://127.0.0.1:7297`, and `WebApi__Hosts__1=`. Rerunning the installer backs up and rewrites the environment file; manually added origins, course paths, and other settings are not automatically retained. Edit the file and restart for configuration-only changes.

The unit uses `ProtectSystem=strict` and `ProtectHome=true`. Its writable paths are API `logs` and `/opt/switchyard/data` only. Keep runtime assets outside home directories. Filesystem permissions alone do not override systemd's write restrictions.

### 3.1 Backup directory for legacy process migration

When automatic process migration has changes to apply, it writes backups under `migration-backups/` in the API output directory. The supplied systemd unit does not make that directory writable. Before upgrading data containing legacy process track references, create it and add a narrowly scoped writable path:

```bash
sudo install -d -o switchyard -g switchyard -m 0750 /opt/switchyard/api/migration-backups
sudo systemctl edit switchyard-api
```

Enter this drop-in:

```ini
[Service]
ReadWritePaths=/opt/switchyard/api/migration-backups
```

Then run `sudo systemctl daemon-reload` and restart during the upgrade window. This adds to existing paths; do not clear the original `logs` and `data` permissions. Include migration backups in retention and backup management. `chmod` alone cannot bypass `ProtectSystem=strict`.

## 4. Build the frontend and configure HTTPS proxying

On the build machine:

```bash
cd switchyard-vue
npm ci
npm run type-check
VITE_API_BASE_URL=https://api.example.com npm run build-only -- --mode production
```

Stop publication if type checking fails. In PowerShell, use `$env:VITE_API_BASE_URL = 'https://api.example.com'` and `npm.cmd`. The API URL is embedded at build time; changing the server environment does not update existing output.

Use the API server root without `/api`. Deploy the contents of `dist/` to the static frontend directory, and preserve the complete source structure needed by the local station-layout frontend dependency during builds.

Nginx example: put `map` in the `http` block and replace certificate paths with the actual certificate files.

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

Keep original paths intact, including `/api/*`, `/Hump/*`, `/Capacity/*`, `/StationLayout/*`, `/OperationPlan/*`, `/OperationProcess/*`, `/SketchLayout/*`, `/Course/*`, and `/hubs/capacity-agent`. Proxying only `/api/` can allow login while breaking business pages.

Add the complete frontend origin, such as `https://app.example.com`, to production CORS. The API enables HTTPS redirection and HSTS outside development. Forward the original protocol correctly to avoid redirection or mixed-content problems. Current production source trusts loopback and RFC1918 private forwarding sources with a two-hop limit; inspect the fixed trust scope in `Program.cs` when changing proxy topology.

Agent long connections require WebSocket upgrade headers and suitable timeouts. Preserve Range requests/responses for video and PDF. History routes must fall back to `index.html` on refresh. Hosting under a prefix such as `/switchyard/` requires aligning Vite `base`, router base, and static fallback, then rebuilding.

For uploads, check the proxy request-body limit. The application currently limits SketchLayout requests to 8 MiB and DWG imports to 20 MiB. Proxy limits apply independently, so changing only the application limit is insufficient. Multipart requests also include form overhead.

## 5. Course assets

`Course__VideoDir` selects the video root. `Course__DocDir` selects the document root and falls back to the video directory when unset. Production JSON defaults both to `/data/switchyardvid`.

If the configured video directory contains `站场视频`, that child becomes the video root. If the document directory contains `教学文档`, that child becomes the document root. Otherwise the configured directory is scanned recursively.

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

| Type | Accepted extensions |
| --- | --- |
| Video | `.mp4`, `.webm`, `.m4v`, `.mov`, `.avi`, `.mkv` |
| Documents | `.pdf`, `.doc`, `.docx` |

Manifests omit temporary files whose names start with `~$` and sort by relative path. Accepted extensions do not guarantee browser codec support. For playback failures, check encoding compatibility and file integrity.

The frontend also expects part/chapter/section hierarchy and video names beginning with chapter.section[.clip] numbers. A file appearing in an API manifest does not guarantee inclusion in the course tree. Keep Chinese recognition markers such as `学习指导`, `文字教材`, and `习题测试` even in English UI mode. Word extraction uses byte-decoding heuristics rather than a complete Word renderer and cannot reliably parse arbitrary `.docx` files. Validate a representative chapter before deploying a collection; see the [user guide](02-user-guide.md) for naming, directory, and content markers.

All four course endpoints allow anonymous access: `GetVideoManifest`, `StreamVideo`, `GetTeachingManifest`, and `StreamTeachingFile`, under `/Course/`. There is currently no course upload API, account authorization, or administrative manifest manager. Maintain assets through server-side file deployment and keep confidential material out of these public directories.

Missing or unconfigured directories produce empty manifests. Streaming a missing directory/file returns 404. File responses support Range processing. Directory enumeration is not a content cache; watch manifest latency and disk access as collections grow.

## 6. Upgrades, rollback, and recovery

Record the source revision, API `App:Version`, frontend API URL, database types, and maintenance window. Allow running solves to finish. Exiting an Agent can stop its Workers when necessary, but there is currently no per-job cancellation endpoint.

Suggested upgrade sequence:

1. Back up all three databases, environment settings, course assets, previous publish output, and downloaded solve results.
2. Check schemas, migrations, and representative workflows against isolated database copies; make the migration backup directory writable.
3. Stop the API and deploy the new package, preserving `logs/` and `migration-backups/`.
4. Start the API, review migration/binding logs and the version response, then verify accounts and business data.
5. Deploy the matching frontend; upgrade and reauthenticate Agents, then verify model compatibility.
6. Check layouts, plans, processes, SketchLayout, courses, and a representative solve.

Use `sudo systemctl stop switchyard-api` and `sudo systemctl start switchyard-api`. For environment-file changes use `sudo systemctl restart switchyard-api`. A separate debugging process must also use separate database configuration because startup writes to its configured databases.

Rollback requires databases compatible with the old application. Copying old DLLs cannot reverse ID type changes, historical layout conversion, or process upgrades. If users have written new data since deployment, preserve the failure state and decide how to reconcile those writes before restoring an older snapshot.

### 6.1 Backup scope and recovery rehearsal

| Object | Backup scope |
| --- | --- |
| Hump database | Users, Refresh Tokens, template `001`, all hump business tables |
| Capacity database | Layouts, plans, process templates/snapshots, analysis, presets, migration markers |
| SketchLayout database | Independent scheme records |
| Course directories | Preserve subdirectories and relative paths |
| Configuration | Private environment file, proxy certificate configuration, systemd drop-ins; restrict access |
| Migration backups/reports | Keep alongside pre-upgrade database backups; not a substitute for them |
| Required raw Agent results | Download before API restart |

For SQLite, use the database backup mechanism or stop every writer before making a consistent copy. Do not copy only an actively written main file while ignoring WAL state. Use a deployment-appropriate consistent MySQL backup process and record the matching application version. Restore databases to a consistent business point to keep users and ownership aligned.

Rehearse restoration into separate directories/databases: startup, existing login, template copying, instance/scheme loading, route consistency, movement occupations and process snapshots, SketchLayout, courses, and Agent reconnection. A successful version request is not a recovery test.

## 7. SQLite to MySQL: exact scope of the supplied tool

The wrapper is [`scripts/Migrate-SqliteToMySql.ps1`](../../scripts/Migrate-SqliteToMySql.ps1), implemented by the [.NET 8 migrator](../../tools/SQLiteToMySqlMigrator/Program.cs). It migrates a fixed set of 20 hump tables. It does not migrate capacity or SketchLayout; replacing `SchemaPath` does not change that scope.

The tables are `user`, `refreshtoken`, `humpinstance`, `slopeline`, `position`, `positionsegment`, `switch`, `retarder`, `wagonconcept`, `operationcondition`, `humpscheme`, `vposition`, `vpositionsegment`, `humpcalculation`, `humpcalculationdata`, `retarderstatus`, `headwaycheckscheme`, `headwaycheckwagon`, `headwaycheckdata`, and `headwaycheckresult`.

The tool reads only the `HumpDatabase` section of the specified JSON. It does not merge the API's production JSON, environment variables, or `api.env`. Prepare a protected private file such as `LocalData/migration.json`:

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

Inspect the plan from the repository root:

```powershell
./scripts/Migrate-SqliteToMySql.ps1 -ConfigPath ./LocalData/migration.json -DryRun
```

`-DryRun` only validates configuration/local paths and prints the plan. It does not connect to databases, execute precheck SQL, or prove import success. Before removing `-DryRun`, stop writes, take backups, and confirm the target is a dedicated new database.

The real run attempts database creation, opens the source, runs prechecks and schema SQL, clears destination tables, imports within a transaction, compares per-table row counts, and saves reports under `migration-reports/` in the current directory. Reports can contain data-quality records and must be protected as business data.

**By default, all data in the 20 destination tables is deleted outside the import transaction.** A failed import cannot automatically restore cleared data. Precheck findings such as duplicates or orphaned records produce warnings; they are neither cleaned automatically nor guaranteed to stop import. Review issues and resolve them on a source copy first.

| Parameter | Meaning |
| --- | --- |
| `-ConfigPath` | Private JSON configuration |
| `-SqlitePath` | Override source SQLite path |
| `-MySqlHost` / `-MySqlPort` / `-MySqlDatabase` | Override destination |
| `-MySqlUsername` / `-MySqlPassword` | Override credentials; prefer a private file to putting passwords in shell history |
| `-SchemaPath` / `-PrecheckPath` | SQL paths; defaults are repository hump schema/precheck files |
| `-SkipSchemaInitialization` / `-SkipPrecheck` | Skip the corresponding step; do not use without validating consequences |
| `-SkipClearTarget` | Append import, not incremental synchronization/deduplication; can duplicate records and fail row-count checks |
| `-SkipValidation` | Skip row-count validation |

Matching counts still require login, template-copying, instance, calculation, and result-saving checks. Switching `HumpDatabase` does not migrate the other two databases. Keep the original SQLite copy read-only and retain reports until business acceptance. Historical row counts, cleanup suggestions, and proposed keys/constraints in the older migration document are not a description of the current target schema.

The capacity database has a separate `--migrate-process-routes` maintenance entry point. It converts legacy process resources and **is not a SQLite-to-MySQL data-transfer tool**. Without `--apply`, it reads existing capacity data and reports planned conversion; with `--apply`, it writes a backup and applies changes. Relevant tables must already exist. Normal API startup also applies this conversion automatically.

## 8. Monitoring and troubleshooting

Routine checks:

```bash
sudo systemctl status switchyard-api
sudo journalctl -u switchyard-api -n 100 --no-pager
curl -f http://127.0.0.1:7297/api/System/version
```

API file logs are `logs/switchyard-*.log`, rolling daily and at 10 MB, retaining 30 files. Agent logs are its own `logs/capacity-agent-*.log`, rolling daily and at 50 MB, also retaining 30 files; native solver lines have `OR-Tools` source context. File count is not a number of retained days.

Logging is configured directly in code; do not assume changing the JSON `Serilog` section changes actual sinks or levels. No dedicated liveness/readiness endpoints exist. Also monitor processes, disk, databases, representative business operations, Agent availability, and failure rates.

Login, Refresh Token refresh, and Agent authentication share the `auth` limit of 5 requests per client IP per minute. Registration allows 3 per IP per 10 minutes; exceeding a limit returns 429. Incorrect proxy forwarding can make many users share one apparent IP.

| Symptom | Check first |
| --- | --- |
| API cannot bind | Stale IPs, duplicate ports, or inherited indexes in `WebApi__Hosts__*`; effective bind logs |
| Database failure at startup | All three sections, existing databases, published SQL files, DDL permissions |
| Historical upgrade stops | Migration context, duplicate IDs, invalid snapshots, ambiguous routes, writable backup directory |
| SQLite read-only error | Parent directory permissions, systemd writable paths, database location |
| Version responds but login fails | Hump data, activation, password hash, JWT, rate limits; version checks none of these |
| User creation fails | Template `001` and related records; whether compensating deletion also failed |
| Only capacity/SketchLayout fails | Its own database settings/schema, not just hump |
| Home works but route refresh gives 404 | Static History fallback |
| Login works but business routes give 404 | Proxy limited to `/api/` or incorrect path rewriting |
| Browser uses old API | Build-time `VITE_API_BASE_URL`, stale static cache; rebuild/redeploy |
| CORS/redirect problems | Complete origin, array merging, forwarded protocol, HTTPS settings |
| Upload returns 413 | Application and proxy body limits, form overhead, actual file size |
| Agent authentication fails | Activation, required password change, API root, authentication limits |
| Agent online but unavailable | Sharing, slots, CPU/memory, minimum model version, name/username ID collision |
| Completed job has no usable solution | Solver `hasSolution` / `status`; completion does not imply feasibility |
| Jobs/raw results disappear | API restart clears memory; distinguish persisted plans |
| Empty courses or playback failure | Directories, preferred subdirectories, extensions/codecs, permissions, Range forwarding |

Database connector exceptions include operation and SQL summaries. Some callers return generic errors, so use service logs for the cause. Remove credentials, tokens, and private business records before sharing logs.

Further operational work includes full health checks, unified migration management across modules, initial-data bootstrap, durable job queuing/recovery, scheduled expired Refresh Token cleanup, and course authorization/caching. These are future improvements, not delivered features.

Uninstall the supplied unit with `sudo bash install-secrets.sh --uninstall`. It stops/disables the service, removes the unit, and preserves the environment file under a renamed path. It does not remove databases or runtime directories.

## 9. Source references and retained material

- [systemd unit](../../scripts/deploy/switchyard-api.service), [environment template](../../scripts/deploy/switchyard-api.env.example), [installer](../../scripts/deploy/install-secrets.sh).
- [Course controller](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/CourseController.cs), [process-resource migration](../../SwitchYard.WebApi/SwitchYard.Service/Services/OperationProcessRouteMigration.cs).
- [Original deployment guide](../archive/guides/Deploy-Instruction.md), [original operations guide](../archive/guides/Operations-Recommendations.md), [full historical migration material](../archive/guides/SQLite-to-MySQL-Migration.md).
