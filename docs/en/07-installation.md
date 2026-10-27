# Installation, configuration, and first run

[Documentation index](README.md) · [中文](../zh-CN/07-installation.md) · Reviewed: 2026-10-11

This chapter covers the environment, configuration, data preparation, and CapacityAgent required by the current source. See [Deployment and operations](08-deployment-operations.md) for production hosting, upgrades, backups, and troubleshooting. The original [getting-started guide](../archive/guides/Getting-Started.md) and [CapacityAgent README](../archive/development/capacity-agent.md) remain available. For database count, startup migrations, and cancellation behavior, use the current-source description below.

## 1. Identify the components

| Component | Purpose | Required for |
| --- | --- | --- |
| `switchyard-vue` | Main Vue interface | Web use |
| `SwitchYard.Service` | Authentication, business APIs, courses, Agent coordination | All API use |
| `HumpDatabase` | Users, Refresh Tokens, hump instances and calculation data | API startup |
| `CapacityDatabase` | Capacity instances, layouts, plans, processes, analysis, solve presets | Capacity features |
| `SketchLayoutDatabase` | Independent SketchLayout schemes | SketchLayout features |
| `SwitchYard.CapacityAgent` | Service communication, job scheduling, Worker cancellation, and resource monitoring | Model solving and saturated-plan generation |
| `SwitchYard.CapacitySolver` | .NET 10 class library for solver models, OR-Tools, and the Worker execution entry point | Published with the Agent and invoked by Workers |
| Course asset directories | Local videos, PDF, and Word materials | Course content |

A complete current deployment has three database sections. References to “two databases” in older documents describe an earlier state. Hump and capacity schemas contain identically named tables with different structures, so they must not share a database or SQLite file. Configure, back up, and restore SketchLayout as an independent database too.

The main frontend consumes `../SwitchYard.StationLayout/frontend` as a local dependency; the API also references the independent station-layout backend. Preserve the repository structure when cloning or copying the project.

## 2. Environment and dependencies

| Purpose | Current project requirement |
| --- | --- |
| Repository builds | .NET 10 SDK to cover Agent and other `net10.0` projects |
| API runtime | ASP.NET Core 8 Runtime; Service targets `net8.0` |
| Agent runtime | .NET 10 Runtime; Agent targets `net10.0` |
| Migration tool runtime | .NET 8 Runtime; `dotnet run` also requires an SDK |
| Frontend builds | Node.js `^20.19.0 || >=22.12.0`, npm |
| Databases | SQLite or MySQL, selected separately for each database |

Do not assume that installing the .NET 10 SDK also installed ASP.NET Core 8 Runtime. Inspect the environment first:

```powershell
dotnet --list-sdks
dotnet --list-runtimes
node --version
npm.cmd --version
```

Key declared dependencies include Vue `^3.5.25`, Vite `^7.2.4`, TypeScript `~5.9.0`, API MySqlConnector `2.6.1`, Microsoft.Data.Sqlite `10.0.1`, and the Solver library's Google.OrTools `9.15.6755`. The Agent's project reference includes the Solver and its runtime dependencies. These describe the current project declarations, not recommended latest upstream versions. `package-lock.json` and `npm ci` determine the installed frontend versions.

The Solver uses OR-Tools native components. Run the tests in section 7.2 on the target operating system and architecture, then execute representative jobs through the published Agent. Output built for another system is not a verified deployment package.

The tracked solution references a missing `SwitchYard.SketchLayout.Tests` project, and other local test projects are ignored by Git. A full solution build may fail before compiling application code. For installation, use the explicit Service and Agent project commands in this chapter rather than assuming every solution entry is present.

## 3. Initial data is part of installation

Startup runs schema initialization and selected upgrade logic. It does not seed demonstration instances, a first administrator, or course assets.

| Goal | Required data |
| --- | --- |
| HTTP/Swagger check | Correct configuration, reachable databases, successful schema initialization |
| Login | A valid user with the correct password hash in the hump database, with activation enabled |
| Registration or administrator-created users | Hump template instance `001` and its copyable related data |
| Capacity analysis | Capacity instance, station layout, routes/occupation parameters, operation plan, and related data |
| Model solving | Valid input, an accessible online Agent, a compatible model version, and a free slot |

Registration creates the user and then copies template `001`. If copying fails, it attempts to delete the new user as compensation. That deletion can also fail and is logged; the entire workflow is not one database transaction. Administrator-created users also depend on template copying.

Ordinary registrations are active by default. Registrations requesting the `Admin` role are inactive until approved by an existing administrator. The repository has no first-administrator bootstrap command or public default username/password.

For a complete first installation, obtain an authorized development database copy or initialization package compatible with the current version. Check its template and administrator before enabling registration. Do not put plaintext passwords into `passwordhash`, or commit production data and credentials.

## 4. Local API with isolated configuration

Run the following PowerShell commands from the repository root. They use a dedicated local data directory and override all three database sections. Place authorized SQLite data at these paths first. Empty paths only trigger structural initialization; they do not satisfy the business prerequisites above.

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
$env:SketchLayoutDatabase__DatabaseType = 'SQLite'
$env:SketchLayoutDatabase__SqlliteConfig__DatabaseFile = Join-Path $localDataDir 'sketchlayout.dev.db'

$jwtBytes = New-Object byte[] 64
$jwtRng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$jwtRng.GetBytes($jwtBytes)
$jwtRng.Dispose()
$env:Jwt__SecretKey = [Convert]::ToBase64String($jwtBytes)

dotnet run --project SwitchYard.WebApi/SwitchYard.Service/SwitchYard.Service.csproj --no-launch-profile
```

These environment variables apply only to this shell and its children. The example does not print the random signing key. Changing the key invalidates existing access tokens; preserve your own key in private configuration if logins must survive restarts.

`SqlliteConfig` is the actual configuration key; do not “correct” it to `SqliteConfig`. Use the exact database-type spellings `SQLite` / `Sqllite` or `MySQL` / `Mysql`. Connector selection is not universally case-insensitive.

`0.0.0.0` is a bind address. Use `localhost` in a local browser, or the API computer's LAN address from another device. For local-only access, set index 0 to `http://127.0.0.1:7297`.

### 4.1 Verify startup

After initialization, look for `Database schema initialized` and `Server bound addresses` in the logs. Then use another terminal:

```powershell
Invoke-RestMethod 'http://localhost:7297/api/System/version'
```

Development Swagger is at `http://localhost:7297/swagger`; production does not enable Swagger. The version endpoint returns version information without checking database content, templates, course assets, or Agents. It is not a complete readiness check.

After checking startup, perform the acceptance checks at the end of this chapter to confirm that accounts, business data, and the required modules are usable.

### 4.2 What startup changes

[`DatabaseSchemaInitializer`](../../SwitchYard.WebApi/SwitchYard.Service/Services/DatabaseSchemaInitializer.cs) performs these steps before listening:

1. Execute hump `sqlite-schema.sql` or `mysql-schema.sql`.
2. Execute the independent SketchLayout schema when its configuration is present and usable.
3. Execute the capacity schema when its configuration is present and usable.
4. Potentially normalize MySQL charset/collation for `stationroute` and `stationroutetime`.
5. Upgrade operation-plan structure/indexes and relational station-layout storage, then backfill historical data.
6. Migrate legacy process track references to real dwelling routes and backfill movement occupation snapshots.

The capacity database records some completed steps in `capacityschemamigration`. Station-layout migration also changes ID types/collations, adds columns, and drops historical `LayoutDocument` after successful conversion. Starting the service can change real data; it is not a read-only probe.

Base SQL uses `CREATE TABLE IF NOT EXISTS`; targeted migrations add selected fields. This does not guarantee that an arbitrary older database will upgrade automatically. Invalid snapshots, ambiguous routes, or duplicate identities can stop migration; retain the original data and investigate the logs.

Create MySQL databases before startup, and ensure SQLite parent directories exist and are writable. Currently the preset service also creates `capacitysolvepreset` on access, so it is inaccurate to say all DDL occurs only at startup.

## 5. Configuration precedence and keys

The API uses the standard ASP.NET Core configuration chain: base JSON, environment-specific JSON, applicable development user secrets, environment variables, and command-line configuration. Later sources override earlier ones. Environment variables represent nested keys with `__`.

`Program.cs` reads `WebApi:Hosts` and calls `UseUrls`. Changing only `ASPNETCORE_URLS`, `--urls`, or `launchSettings.json` `applicationUrl` does not replace this application's bind configuration. With no effective `WebApi:Hosts`, startup fails.

| Key / environment variable | Meaning and notes |
| --- | --- |
| `ASPNETCORE_ENVIRONMENT` | Select Development, Production, or another environment |
| `WebApi__Hosts__0`, `__1` | Bind array; production commonly uses loopback and an empty second entry |
| `App__Version` | Application version text returned by the version endpoint and logged at startup |
| `Jwt__SecretKey` | JWT signing key, supplied privately |
| `Jwt__Issuer` / `Jwt__Audience` | Issuer/audience, consistent with token validation |
| `Jwt__ExpirationMinutes` | Web access-token minutes; code default 60 |
| `Jwt__RefreshTokenExpirationDays` | Refresh Token days; default 7 |
| `Jwt__CapacityAgentExpirationMinutes` | Agent token minutes; default 1440 |
| `Cors__AllowedOrigins__0`, etc. | Production frontend origins, including scheme and port |
| `<section>__DatabaseType` | Independently select SQLite/MySQL for each database |
| `<section>__SqlliteConfig__DatabaseFile` | SQLite path; an absolute path is recommended |
| `<section>__MysqlConfig__Host` / `Port` / `Database` | MySQL server, port, and database |
| `<section>__MysqlConfig__Username` / `Password` | MySQL credentials |
| `<section>__MysqlConfig__CharSet` / `SslMode` | Charset and connection TLS mode |
| `<section>__MysqlConfig__AllowPublicKeyRetrieval` | Optional MySQL public-key retrieval setting |
| `<section>__MysqlConfig__ConnectionTimeout` | Connection timeout in seconds; default 15 |
| `Course__VideoDir` / `Course__DocDir` | Course directories; see operations chapter |

`<section>` is `HumpDatabase`, `CapacityDatabase`, or `SketchLayoutDatabase`. Arrays merge by index: setting index 0 does not remove later indexes. Bind processing filters empty entries; CORS does not apply the same filtering. Maintain a complete effective list of allowed origins.

Production startup validates the merged JWT setting and credentials for selected MySQL hump/SketchLayout databases. It does not validate every connection parameter and lacks an equivalent capacity-credential check. Although the error names “environment variables,” the code examines merged configuration values; still supply secrets through private environment files or secret management.

## 6. Start and build the main frontend

Open another PowerShell at the repository root:

```powershell
Set-Location switchyard-vue
npm.cmd ci
npm.cmd run dev
```

The default page is `http://localhost:5173`. Vite binds all IPv4 interfaces and enables `strictPort`; an occupied port causes an error rather than automatic renumbering. Development API requests default to HTTP port `7297` on the page's hostname.

To pin a particular API, set its root URL before starting:

```powershell
$env:VITE_API_BASE_URL = 'http://localhost:7297'
npm.cmd run dev
```

Do not append `/api`: business routes also use `/Hump`, `/Capacity`, `/StationLayout`, `/OperationPlan`, and other roots. For normal source startup, `VITE_API_BASE_URL` overrides frontend JSON configuration and can be kept in your private `.env.development.local`. Docker frontend runtime configuration has higher precedence; see [Docker environment variables](12-docker-deployment.md#3-environment-variables).

Vite's development proxy covers only `/api`, not every business route. The current frontend generally requests the configured API root directly. Development API CORS permits any origin; production uses the configured list.

Build and preview:

```powershell
$env:VITE_API_BASE_URL = 'http://localhost:7297'
npm.cmd run type-check
if ($LASTEXITCODE -ne 0) { throw 'Type check failed' }
npm.cmd run build-only -- --mode production
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
npm.cmd run preview
```

Output is `dist/`, with preview defaulting to port `4173`. `npm run build` combines type checking and building; separate commands simplify failure diagnosis. On Linux/macOS use `npm` instead of `npm.cmd`, for example `VITE_API_BASE_URL=https://api.example.com npm run build-only -- --mode production`.

For direct source builds, the API URL is read when the development server starts or the frontend is built; changing a deployment server variable does not update existing output. Docker deployment generates higher-priority runtime configuration at container startup, so changing the API address only requires recreating the container. Current `config.ts` selects production JSON outside the development server, so `build:dev` does not necessarily select the development API; set the URL explicitly. These runtime configuration notes were reviewed on 2026-10-15.

## 7. Install CapacityAgent

The API dispatches jobs; the Agent handles communication, scheduling, and resource monitoring, while independent Worker subprocesses invoke the `SwitchYard.CapacitySolver` class library. Publishing the Agent includes the Solver and OR-Tools runtime dependencies; there is no separate Solver executable to deploy. Editing layouts and other independent features remain usable without an Agent, but model-dependent solve actions cannot complete. This description of project responsibilities and test entry points was updated on 2026-10-27.

```powershell
dotnet publish SwitchYard.WebApi/SwitchYard.CapacityAgent/SwitchYard.CapacityAgent.csproj -c Release -o ./publish/capacity-agent
Set-Location publish/capacity-agent
dotnet SwitchYard.CapacityAgent.dll
```

Enter the API root URL, username, and password. The account must be active. If it requires a password change, complete that in the Web UI first. The current entry point is interactive; no unattended Agent service installer or password environment-variable protocol is supplied.

The Agent obtains a dedicated JWT with a client-type claim from `/api/capacity-agents/authenticate`, then connects to `/hubs/capacity-agent`. It does not rotate Web Refresh Tokens; it authenticates again with the account credentials when it needs a fresh token.

Example `capacity-agent.json` in the publish directory:

```json
{
  "agentName": "workstation-a",
  "maxConcurrentJobs": 2,
  "cpuCoresPerJob": 2,
  "memoryLimitMbPerJob": 2048,
  "statusReportIntervalSeconds": 2,
  "allowAllUsers": false,
  "allowedUsers": []
}
```

Select another file with `dotnet SwitchYard.CapacityAgent.dll --config <private-config-path>`. The configuration does not contain account credentials.

| Field | Range / behavior |
| --- | --- |
| `agentName` | Uses the OS username when empty; use different names on different machines |
| `maxConcurrentJobs` | 1–128; default 2 |
| `cpuCoresPerJob` | 1–128; default 2; a per-job thread cap, not CPU affinity |
| `memoryLimitMbPerJob` | 256–1048576; default 2048; SCIP limit plus Worker working-set monitoring |
| `statusReportIntervalSeconds` | 1–60; default 2 seconds |
| `allowAllUsers` / `allowedUsers` | Sharing is restricted to administrator Agents; at most 500 usernames, each at most 100 characters |

An ordinary user's Agent is always private to its owner. An administrator may allow all signed-in users or only the owner and a username allowlist; absent sharing settings it remains owner-only. The service enforces these rules regardless of local JSON edits.

The Agent ID derives from the Agent name and login username. Reusing that pair on different machines produces the same ID; assign different `agentName` values for simultaneous Agents. Available slots also depend on logical CPUs and memory headroom, so a concurrency setting of 2 does not guarantee 2 free slots.

### 7.1 Models, Workers, and job lifecycle

The current model is `station-capacity-v1`, version **2.5.0**, using OR-Tools / SCIP mixed-integer programming in `SwitchYard.CapacitySolver`. The `v1` in the ID is not the current feature version. Ordinary trains retain sequential activities and track-circuit occupation constraints; process-generated trains also retain event graphs, durations, exact/minimum ordering, resource locations, and anchor constraints.

Current ordinary-train inputs require at least model 2.4.0; process references require at least 2.5.0. The service checks `minimumModelVersion` and raises the minimum for parameter sets and process references. An older Agent can appear online yet be unable to accept the input; update the Agent and its bundled Solver, then register again.

Each job runs in an independent Worker process. The Agent launches its own internal `--solve-worker` entry point, which invokes the Solver library's Worker Host; the main process's model adapter forwards jobs without executing solver algorithms. CPU settings limit solver threads. The Agent periodically monitors the Worker working set and terminates a Worker exceeding its memory allowance. These controls are not equivalent to complete operating-system container isolation.

The service reserves an available slot before dispatch. Jobs progress through `queued`, `running`, and finally `completed` or `failed`. `queued` is not a durable queue waiting for future resources. A `completed` job does not guarantee a feasible solution: inspect result `hasSolution` and solver `status`.

There is currently no Web/HTTP per-job cancellation endpoint. `Ctrl+C` exits the Agent, propagates cancellation, and terminates relevant Workers; it may affect every job on that Agent. Connection loss triggers reconnection. Registration reports active jobs, and the service fails abandoned previous jobs. Reconnection is not cross-process solver recovery.

| Data | Persistence |
| --- | --- |
| Agent registration, job state, progress, raw result JSON | Service memory; lost on API restart |
| Solve presets | Capacity database `capacitysolvepreset` |
| Ordinary “Model solving” results | Downloadable JSON; no automatic write-back to the plan |
| Successful “Generate saturated plan” result | Saved transactionally as a new plan after feasible-solution validation, including activities, resources, and process snapshots |
| Separately saved analysis results | Capacity analysis tables, separate from Agent jobs |

Download raw results that must be retained before restarting, and let jobs finish. Saturated-plan generation optimizes activities for existing trains; it does not add trains to search for maximum train count.

### 7.2 Optional self-test and acceptance checks

Run the separate solver test project from the repository root:

```powershell
dotnet run --project SwitchYard.WebApi/SwitchYard.CapacitySolver.Tests
```

Business self-tests have moved from the Agent to this project. The Agent's `--self-test` option reports the new entry point and exits without running solver examples. Tests cover shared domain objects, route continuity, process references, concurrent Worker solving, unknown-model rejection, and cancellation of running Workers. They require no API login and write no business database. They do not validate database, proxy, or business-data integration; after publishing, submit a representative job through the Service to verify Worker startup, progress, and result delivery.

Some `*.Tests` projects and `switchyard-vue/checks` are excluded by ignore rules in the current workspace. Test files referenced by older documents may be absent from a fresh clone; confirm delivery before running them. Missing files are not successful tests.

For an actual installation, record at least:

1. SDK/Runtime/Node versions, source revision, and API/frontend build versions.
2. Correct database sections and effective binds, with no initialization errors.
3. Successful login and an authorized user-creation check using template `001`.
4. Loading and saving a test instance, station layout, operation plan, and sketch layout scheme; course assets readable.
5. An online Agent visible to the current user with a free slot, and a representative solve producing the expected result.
6. Saturated-plan save-as behavior on test data and persisted data surviving a restart.

## 8. Source references and retained documents

- [API startup/configuration](../../SwitchYard.WebApi/SwitchYard.Service/Program.cs), [database connector](../../SwitchYard.WebApi/SwitchYard.Service/DBConnector.cs).
- [Operation-plan migrations](../../SwitchYard.WebApi/SwitchYard.Service/Services/OperationPlanSchemaMigration.cs), [station-layout migrations](../../SwitchYard.WebApi/SwitchYard.Service/StationLayout/StationLayoutSchemaMigration.cs).
- [Agent configuration validation](../../SwitchYard.WebApi/SwitchYard.CapacityAgent/CapacityAgentOptions.cs), [Agent entry point](../../SwitchYard.WebApi/SwitchYard.CapacityAgent/Program.cs).
- [Original getting-started guide](../archive/guides/Getting-Started.md), [original Agent README](../archive/development/capacity-agent.md), [next: deployment and operations](08-deployment-operations.md).
