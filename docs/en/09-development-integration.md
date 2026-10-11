# Frontend development and station-layout integration

[Documentation index](README.md) · [简体中文](../zh-CN/09-development-integration.md)

This guide describes development, reuse and validation from project files, gateway contracts and actual routes as of 2026-10-11.
Use the index for environment setup, databases and deployment; this chapter does not require access to a business database.
Earlier documentation is retained and linked at the end. Outdated method counts and missing test entry points are identified below.

## 1. Directories and runtime boundaries

| Path | Responsibility |
| --- | --- |
| `switchyard-vue/src/capacity` | Capacity pages, plans, solving and simulation host |
| `switchyard-vue/src/hump` | Hump pages, drawing, checking and simulation |
| `switchyard-vue/src/course` | Courses, videos, PDFs and exercises |
| `switchyard-vue/src/views` | Account and other routed pages |
| `switchyard-vue/src/components/ui` | Shared buttons, dividers and icons |
| `switchyard-vue/src/locales` | Chinese and English messages |
| `SwitchYard.StationLayout/frontend` | Reusable Vue station-layout component |
| `SwitchYard.StationLayout/backend` | Host-independent layout contracts and application logic |
| `SwitchYard.WebApi/SwitchYard.Service/StationLayout` | SwitchYard database, authorization, migration and ID adapters |

Keep the main frontend and `SwitchYard.StationLayout` as adjacent directories.
`package.json` uses `file:../SwitchYard.StationLayout/frontend`, and Vite resolves the package entry directly to shared source.
The shared package has no built-in host HTTP address, Axios singleton, user table or database connection.
Another host supplies a Gateway and Repository, uses its own authentication and storage, and can operate independently offline.

## 2. Starting and building the main frontend

`switchyard-vue/package.json` declares Node.js `^20.19.0 || >=22.12.0`.
Node tests that directly import TypeScript source require Node `22.18+` or `24`; distinguish the build minimum from test requirements.
Run in `switchyard-vue`:

```powershell
npm ci
npm run dev -- --host 127.0.0.1
```

The development server uses port `5173` with `strictPort=true`; it fails if the port is occupied rather than incrementing it.
Plain `npm run dev` listens on `0.0.0.0`; the example limits access to the local machine.
The development configuration substitutes the browser's current hostname for a configured local hostname and uses API port `7297`.
Opening the frontend using a LAN IP therefore connects to the API port on that same host by default.

To override the API, create `.env.development.local`:

```dotenv
VITE_API_BASE_URL=http://localhost:7297
```

Use a service root without an extra `/api`; restart Vite after changing it.
Authentication uses `/api/Auth`; business routes also include `/Hump`, `/Capacity`, `/StationLayout` and `/OperationPlan`.
Axios normally uses the absolute API root; Vite's `/api` proxy does not proxy every business endpoint.
Before production builds, set the real address in `.env.production.local` or use the production JSON configuration.

```powershell
npm run type-check
node node_modules/vite/bin/vite.js build --mode production
npm run preview -- --host 127.0.0.1
```

Output goes to `dist/`; preview defaults to port `4173` and does not start the backend.
`build` / `build:prod` combine type checking and building; separate commands help identify Windows/npm script issues.
`build-only` skips type checking, `preview:prod` does not rebuild, and `dev:prod` runs the development server with production configuration.
`config.ts` selects production JSON when `import.meta.env.DEV=false`, so `build --mode development` may still use the production API.
`VITE_API_BASE_URL` has highest precedence and is compiled into the output; changing server environment variables after deployment does not update existing JavaScript.

## 3. Shared UI and bilingual maintenance

Prefer the `workspace.css` variables `--sy-surface`, `--sy-surface-muted`, `--sy-border`, `--sy-text`, `--sy-text-muted` and `--sy-radius`.
Load Element Plus base CSS before workspace CSS.
Scope shared selectors carefully; hump and the reusable station-layout toolbar retain their own layouts.

`ActionButton` requires `label` for its accessible name. Its default `variant="icon"` is a 30 px circular button with a tooltip.
It also supports `variant="text"` and `variant="icon-text"` for visible form and dialog action names.
`active` represents toggle state and supplies `aria-pressed`; retain business `disabled` and `loading` conditions.
Click events carry a `MouseEvent`; use `@click="refreshData()"` when the handler accepts an optional options object, avoiding accidental event-as-options input.
Use your own container for outer layout rather than depending on tooltip wrapper structure.

`PaneDivider` only changes the bound value; the host must bind it to panel width, height or `flex-basis`:

```vue
<PaneDivider v-model="sidebarWidth" direction="horizontal"
  :min="240" :max="maxSidebarWidth" :reset-value="320"
  reverse :label="t('common.resize.horizontal')" />
```

`horizontal` changes width, `vertical` changes height, and `reverse` handles right-hand or bottom panels.
Its interactive width is 8 px. Arrows step by 10 px, Shift by 40 px, Home/End select bounds, and Enter/double-click reset.
Calculate valid limits in the parent and mount after asynchronous sizing is ready; scrollable children need `min-width:0` and `min-height:0`.
Sizes are not persisted; a host can use the reset event to restore proportional layout.

Keep matching keys and interpolation parameters in `zh.json` and `en.json`, including tooltips, validation, empty states and accessible names.
Some process-editor text still uses `ui(zh, en)`; maintain both strings together.
Do not rewrite user names or business enums when switching languages. Shared layout fallback translations are in `messages.ts`.

## 4. Shared package and frontend mounting

The current npm package `@switchyard/station-layout` and .NET package `SwitchYard.StationLayout` both use version `0.1.0-alpha.2`.
Frontend peer dependencies are Vue `>=3.5.0 <4`, Element Plus `>=2.10.0 <3` and icons `>=2.3.0 <3`.
Register Vue and Element Plus in the host and configure Vite `resolve.dedupe: ['vue', 'element-plus', '@element-plus/icons-vue']`.
Across repositories, install a fixed `.tgz` package rather than treating arbitrary `.vue` source paths as a release interface.

This host skeleton assumes that its own `stationLayoutGateway` is implemented; `scopeId` comes from the host's current resource selection.

```vue
<script setup lang="ts">
import { ref } from 'vue'
import { StationLayout, createStationLayoutTranslator } from '@switchyard/station-layout'
import '@switchyard/station-layout/style.css'
import { stationLayoutGateway } from './stationLayoutGateway'
const scopeId = ref('')
const schemeId = ref<string | undefined>()
const readonly = ref(false) // Derive from actual host permissions.
const translate = createStationLayoutTranslator('en')
</script>
<template>
  <div class="layout-host">
    <StationLayout :key="scopeId" :selected-instance-id="scopeId"
      v-model:station-scheme-id="schemeId" :gateway="stationLayoutGateway"
      :translate="translate" :readonly="readonly" />
  </div>
</template>
<style scoped>
.layout-host { height: 720px; min-height: 0; overflow: hidden; }
</style>
```

`gateway` is required; `translate`, `formatError` and `readonly` are optional. The default language is Chinese.
`stationSchemeId` / `update:stationSchemeId` can synchronize scheme selection with the host.
An empty `selectedInstanceId` displays the unselected state. Supply an explicit container height and import the packaged CSS.
If project and scope both define isolation, use `${projectId}:${scopeId}` as the component key.
`readonly=true` permits viewing, selection, zoom, loading and export, while disabling drawing, editing, import, save, scheme management and DWG extraction.
Readonly is a UI constraint; enforce backend authorization independently.

## 5. Complete Gateway contract

The current contract has **9 required methods and 1 optional method**; earlier references to “8 methods” are outdated.
Gateway arguments use `instanceId` and `stationSchemeId`; compatible HTTP parameters often use `instanceID` and `stationSchemeID`.

| Gateway method | HTTP operation | Important result |
| --- | --- | --- |
| `getStationSchemes` | GET `GetStationSchemes` | Scheme array |
| `createStationScheme` | POST `CreateStationScheme` | New scheme |
| `copyStationScheme` | POST `CopyStationScheme` | Independent copy |
| `editStationScheme` | PUT `EditStationScheme` | Renamed scheme |
| `deleteStationScheme` | DELETE `DeleteStationScheme` | No response-body dependency |
| `getJson` | POST `GetJson` | Complete layout and revision |
| `saveJson` | POST `SaveJson` | New revision, persisted document and ID mappings |
| `searchRoutes` | POST `SearchRoutes` | Routes with string node/track IDs |
| `extractDwgFile` | POST `ExtractDwgFile` | Extracted layout |
| `repairJson` (optional) | POST `RepairJson` | `{ json, repairCount, repairs }` |

The default HTTP prefix is `/StationLayout`; the shared Endpoint mapper currently exposes **10 operations**.
Reuse the host HTTP client for authentication, token refresh, project context and error handling.
`getJson` should copy `ETag` / `X-Station-Layout-Revision` into `metadata.revision`.
Save with `expectedRevision` or a strong `If-Match: "<revision>"` ETag, then accept the new revision returned after saving.
Revision conflicts return HTTP `409`; reload and reconcile instead of silently disabling the check and overwriting.
For cross-origin deployment, expose `ETag`, `X-Station-Layout-Revision` and `X-Station-Layout-Exists` response headers.
Normalize PascalCase/camelCase responses and handle both plain-text and problem-details errors.
See the complete [host Gateway implementation](../../switchyard-vue/src/capacity/switchyardStationLayoutGateway.ts).

## 6. JSON imports, identities and repairs

Exports use top-level `format: "switchyard.station-layout"` and `formatVersion: 1`; recognition does not rely only on `schemaVersion`.
All ten collections are preserved: `tracks`, `curves`, `nodes`, `signals`, `insulationJoints`, `bufferStops`, `platforms`, `switches`, `cells` and `annotations`.
The document also preserves geometry, topology, coordinate transforms, display/grid settings, numeric precision, array order and extensions.
Older layouts without format markers remain importable; unsupported formats or versions are rejected.
Import replaces the canvas but binds to the current instance/destination scheme and retains that scheme's revision; it does not select the source scheme recorded in the file.
Validation precedes application. Failure retains the existing canvas; successful import/load clears old undo history, and persistence requires Save.
Exports exclude selection, drawing drafts, scroll position and the undo stack.

The complete component exposes `exportJson()` and asynchronous `importJson(json)` through its ref.
The pure `parseStationLayoutJson` / `serializeStationLayoutJson` functions use the same codec as file actions.
For the lower-level Editor, parse and validate before `loadDataFromJson(document, { preserveDocument: true, resetHistory: true })`; obtain all elements with `buildJsonData()`.
The full component also synchronizes the track-circuit sidebar, display settings and current scope; a lower-level host must handle these itself.

Keep every object ID, endpoint ID, search input ID and returned route ID as a string.
Do not pass leading-zero or beyond-safe-integer IDs through `Number`, `parseInt` or numeric database columns.
On save, SwitchYard assigns Snowflake string IDs to new objects in the destination scheme, retains persisted identities and rewrites references within the same transaction.
Apply response `savedJson` and per-collection `idMappings` to the canvas, selection, Cells and undo/redo history.
Preserve edits made while saving; do not replace newer edits with a snapshot of the older request.

Before saving, dangling references may be repaired using positions; the default `TopologyRepairTolerance` is `1` layout coordinate unit.
Only unique matches are accepted. Valid references and geometry remain; ambiguous or unresolved candidates reject the complete save.
`RepairJson` validates/previews repairs without writing. Implement the optional Gateway method to use it during imports and loads too.
Responses report changes through `repairCount` and `repairs`, with `repairedJson` when repairs occurred.
A repair displayed during readonly loading or a local import does not mean the database has been saved.

## 7. Backend host integration

The shared backend targets `net8.0;net10.0` and depends on ASP.NET Core and ACadSharp `3.4.29`.
It does not read connection strings, execute SQL or prescribe host table names.
Implement these interfaces, using [StationLayoutAbstractions.cs](../../SwitchYard.StationLayout/backend/StationLayoutAbstractions.cs) for exact signatures:

| Interface | Host responsibility |
| --- | --- |
| `IStationLayoutRepository` | List, load, create, copy, rename, delete and atomically replace layouts |
| `IStationLayoutAuthorization` | Scope and global authorization |
| `IStationLayoutIdGenerator` | Generate unique, host-owned scheme IDs |
| `IStationLayoutArtifactSink` (optional) | Host side effects after DWG extraction; defaults to a no-op |

Permissions are `View`, `ManageSchemes`, `SaveLayout` and `ImportDwg`, returning Allowed, Unauthenticated, Forbidden or NotFound.
`scopeId` is an opaque host resource ID, called `selectedInstanceId` in the frontend and `instanceID` in compatible HTTP parameters.
For project/tenant isolation, combine it with trusted server-side context instead of trusting only a browser-supplied project ID.

This new-host registration skeleton assumes host implementations for `App...` types and authentication:

```csharp
builder.Services.AddScoped<IStationLayoutRepository, AppLayoutRepository>();
builder.Services.AddScoped<IStationLayoutAuthorization, AppLayoutAuthorization>();
builder.Services.AddSingleton<IStationLayoutIdGenerator, AppLayoutIdGenerator>();
builder.Services.AddSwitchYardStationLayout(options =>
{
    options.LegacyV1Compatibility = true;
    options.TopologyRepairTolerance = 1;
});
// Configure host authentication, AddAuthorization and other services first.
var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapSwitchYardStationLayout("/StationLayout");
```

The shared Endpoint group requires authentication but does not select a JWT or Cookie scheme for the host.
**Current SwitchYard uses `AddSwitchYardStationLayoutLegacyHost()` and `MapControllers()`; its compatible Controller still owns the public routes.**
Do not add duplicate Endpoint mappings in this host. A new host should select one public route implementation.
DWG uses multipart `file` and `layerName`, with a default 20 MiB limit. Extraction is separate from saving the final layout.

## 8. Storage, concurrency and releases

Repository reads return detached snapshots; related tables and revisions must be read in a consistent transaction.
The complete save and `ExpectedRevision` comparison belong in one transaction. Throw `StationLayoutConflictException` on mismatch and increment the revision once on success.
Copy atomically and start at revision `0`; delete the scheme and its elements atomically. Do not swallow storage exceptions and report success.
An independent host may store a complete JSON aggregate; relational reconstruction must retain all attributes, precision and extensions.
SwitchYard currently uses relational tables as the source of truth and assembles JSON on read; `LayoutMetadata`/`LayoutExtensions` contain only metadata and root extensions.
Migration converts integer object IDs to text, uses historical archives to restore identities and update route/process references, then removes the old `LayoutDocument` after success.
Conflicting historical topology stops migration while retaining original data; do not treat migration failure as an ignorable log message.
Each host owns its DDL and versioned migrations. Exchange JSON between programs rather than sharing business tables or writing to each other's databases.

For offline releases, pin npm and NuGet versions together, record SHA-256 values and commit the host lockfile.
In the shared frontend directory, run `npm ci`, `npm run test:contract`, `npm run type-check`, `npm run build` and `npm pack`.
From the repository root, use `dotnet pack SwitchYard.StationLayout/backend/SwitchYard.StationLayout.csproj -c Release -o artifacts/nuget` for the backend package.
Upgrade both package versions together, never overwrite an existing released version, and build/test the host with the actual artifacts.
Restart Vite after local-package updates and inspect prebundling caches if needed; refreshing business data does not replace loaded component code.

## 9. Reproducible checks and current gaps

This section reports file existence and Git tracking, **not test passes from this documentation task**.

| Check | Current status |
| --- | --- |
| Main frontend `src/capacity/*.test.mjs` and `src/capacity/components/*.test.mjs` | Tracked, covering processes, conflicts, simulation and station charts |
| `src/sketchlayout/sketchlayout.test.mjs` | Tracked |
| Shared frontend `gateway-contract.ts` | Tracked; used by `test:contract` |
| Shared runtime, inline-rename, json-codec, json-editor and json-integration tests | These 5 `.mjs` files are tracked |
| Shared `test:runtime` references to full-view, topology, repair and identity tests | Files are missing; the complete script cannot be assumed runnable |
| Main frontend `checks/i18n.mjs` | Present locally but untracked because `checks/` is ignored |
| `switchyard-vue/tests/README.md`, paneDivider and other old test entries | Currently missing and untracked |
| `SwitchYard.StationLayout/tests/...Tests.csproj`, `SwitchYard.Service.Tests/...Tests.csproj` | Paths referenced by older READMEs do not exist |
| Old SmokeTests / Service.StationLayout.IntegrationTests directories | Present locally but excluded by `.gitignore`, not delivered by Git |

Choose tracked checks relevant to a change, for example in the main frontend directory:

```powershell
node --test src/capacity/operationProcessRoutes.test.mjs
node --test src/capacity/operationProcessPrecedence.test.mjs
npm run type-check
```

In the shared frontend, run existing files such as `node tests/layout-json-codec.mjs` or `node tests/layout-runtime.mjs` individually; do not assume a composite script with missing files has passed.
Tracked development previews include `/dev/workspace-style.html`, `/dev/operation-process.html`, `/dev/operation-process-layout.html` and `/dev/operation-process-links.html`.
Older references to `/tests/fixtures/...`, `/dev/operation-simulation.html` and `/dev/station-layout-rename.html` are not current reproducible entry points.
Previews use local/in-memory examples, do not prove real server persistence and are not production build entry points.
UI changes also need manual checks for both languages, narrow windows, tooltips, resize/reset behavior and cleanup when leaving a page.

## 10. Integration troubleshooting and retained documents

| Problem | Check first |
| --- | --- |
| Blank page or zero height | Container height, scope/scheme selection and delayed visibility |
| Missing styles | Explicit packaged CSS import and host global-selector overrides |
| Every save returns 409 | Read/send/update revision; expose revision headers across origins |
| 401 / 403 / 404 | Authentication middleware, Gateway credentials, permission mapping and resource scope |
| Duplicate Vue or ref failures | Matching peer dependencies and Vite dedupe |
| Lost ID precision or broken bindings after save | String IDs throughout and application of every ID mapping |
| API returns HTML | Reverse proxy accidentally falling back to frontend `index.html` |

History routes require frontend page fallback to `index.html`, while APIs must be forwarded separately. Subpath deployment also needs consistent Vite base and server paths.
Retained documents: [main frontend README](../archive/development/frontend.md), [module README](../archive/development/station-layout.md), [original Chinese integration guide](../archive/development/station-layout-integration.zh-CN.md), and [shared frontend README](../archive/development/station-layout-frontend.md).
Further source references: [Gateway types](../../SwitchYard.StationLayout/frontend/src/gateway.ts), [JSON codec](../../SwitchYard.StationLayout/frontend/src/layoutJson.ts), [Endpoint mappings](../../SwitchYard.StationLayout/backend/StationLayoutEndpointExtensions.cs), and [host registration](../../SwitchYard.WebApi/SwitchYard.Service/StationLayout/LegacyStationLayoutServiceCollectionExtensions.cs).
