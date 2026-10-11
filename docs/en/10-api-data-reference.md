# API and data reference

[Documentation](README.md) · [中文](../zh-CN/10-api-data-reference.md)

Use this chapter when integrating a client, maintaining data, or diagnosing API failures. The main service currently contains 175 controller HTTP operations; the [API inventory](appendix-api-routes.md) lists every method and path. Request fields are defined by the linked DTOs and controller signatures. In Development, `/swagger` exposes the running service's OpenAPI description. Swagger is not enabled by default in Production.

## Addresses and protocols

Set frontend `VITE_API_BASE_URL` to the API root, such as `http://localhost:7297`, without adding a universal `/api` suffix. Authentication and agent endpoints use `/api/...`; hump, station layout, operation plan, process, and course endpoints use their own prefixes. Scope identifiers such as `instanceID` and `stationSchemeID` must refer to business objects accessible to the authenticated user.

Default JSON serialization uses camelCase. Depending on whether a DTO declares `InstanceID` or `InstanceId`, an output key may be `instanceID` or `instanceId`; do not rewrite all returned keys into a single spelling. Some legacy endpoints return strings or raw JSON rather than a uniform `{ data, code }` envelope. Course file endpoints stream binary data. Uploads use `multipart/form-data`.

The hump controller uses `[controller]/[action]` routing. An attribute's `Name` is only its route name: `[HttpPost(Name = "ExecuteCalculation")]` actually maps to `/Hump/ExecuteEnergyHeightCalculation`. Station layout `GetJson` is a **POST**, so do not infer the HTTP method from an action's name.

## Authentication and authorization

### Login and sessions

The web client hashes a password with SHA-256 and sends its hexadecimal representation. The server verifies stored password data using Argon2id. Follow the same convention when integrating the ordinary login endpoint. Deploy HTTPS for transport: the password digest itself is a reusable login credential.

```http
POST /api/Auth/login
Content-Type: application/json

{
  "username": "<existing-username>",
  "password": "<SHA256_HEX_OF_PASSWORD>"
}
```

A successful response contains `token`, `tokenType`, `expiresIn`, `refreshToken`, `refreshTokenExpiresIn`, `userID`, `name`, `role`, and `mustChangePassword`. Subsequent business requests use:

```http
Authorization: Bearer <token>
```

`POST /api/Auth/refresh` accepts `{ "refreshToken": "<refresh-token>" }` and returns a new access token and a rotated refresh token. Replace both in the client. `POST /api/Auth/logout` uses the same request shape to revoke the refresh token. Do not repeatedly retry expired, disabled, or already rotated credentials.

Login, refresh, and agent authentication use the `auth` policy, allowing five requests per minute per remote IP. Registration uses `register`, allowing three requests per ten minutes. Controller attributes determine which actions use each policy; exceeding a limit returns 429. Reverse proxy settings affect the client IP observed by the service.

### Access scope

| API family | Authorization |
| --- | --- |
| `/api/Auth/login`, `refresh`, `logout`, `createuser` | No existing JWT required; credentials, tokens, or registration data are still validated |
| `/api/Auth/userinfo`, `changepassword` | JWT required |
| `/api/Admin/...` | `Admin` role required |
| `/Hump/...` | JWT and action-specific instance owner or administrator checks |
| `/Capacity/...`, `/StationLayout/...`, `/OperationPlan/...`, `/OperationProcess/...` | JWT and instance/scheme scope validation; identifiers cannot be combined across scopes |
| `/SketchLayout/schemes...` | Each request checks the active user and current role in HumpDatabase; ordinary users access their own schemes, administrators all schemes |
| `/Course/...`, `/api/System/version` | Anonymous access allowed |
| `/api/capacity-agents/...` | Authentication validates an account; other endpoints require JWT and apply agent access rules or job ownership checks |
| `/hubs/capacity-agent` | Dedicated `CapacityAgent` authorization policy; a browser login token is not an agent token |

Authorization is not identical across modules. Sketch checks current account status on every request, while older endpoints also rely on JWT claims. Hiding frontend controls does not replace server authorization.

## API families and contracts

| Family | Main purpose | Field definitions and implementation |
| --- | --- | --- |
| Auth / Admin | Login, refresh, registration, profile, user import, and administration | [Authentication models](../../SwitchYard.WebApi/SwitchYard.Service/Models), [AuthController](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/AuthController.cs), [AdminController](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/AdminController.cs) |
| Hump | Instances, rolling lines, planar and vertical layouts, wagons, conditions, calculations, and headway checks | [Hump models](../../SwitchYard.WebApi/SwitchYard.Hump), [HumpController](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/HumpController.cs) |
| Capacity | Create, read, update, delete, and copy capacity instances | [CapacityInstance](../../SwitchYard.WebApi/SwitchYard.Capacity/CapacityInstance.cs) |
| StationLayout | Schemes, JSON, repair, DWG, routes, dwelling routes, and timing | [Layout requests](../../SwitchYard.WebApi/SwitchYard.Capacity/StationLayoutRequests.cs), [module contracts](../../SwitchYard.StationLayout/backend) |
| OperationPlan | Plans, trains, movements, batch edits, occupancy import/recalculation, charts, and analysis persistence | [Controller](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/OperationPlanController.cs), [occupancy import models](../../SwitchYard.WebApi/SwitchYard.Service/Models/CellOccupancyImportModels.cs) |
| OperationProcess | Templates, catalog, activity/event entities, and revision control | [Process models](../../SwitchYard.WebApi/SwitchYard.Service/Models/OperationProcessModels.cs), [process controller](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/OperationProcessController.cs) |
| CapacityAgents | Available agents, presets, solve inputs, jobs, and saturated plans | [Shared agent contracts](../../SwitchYard.WebApi/SwitchYard.Capacity/CapacityAgentContracts.cs) |
| SketchLayout | Scheme document CRUD and analysis of the saved revision | [Sketch models](../../SwitchYard.WebApi/SwitchYard.SketchLayout/Models.cs), [validation rules](../../SwitchYard.WebApi/SwitchYard.SketchLayout/Validation.cs) |
| Course | Resource manifests and file streaming with range support | [CourseController](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/CourseController.cs) |

Paged endpoints accept `pageNumber` and `pageSize`, defaulting to 1 and 10; the maximum page size is 100. User pagination also accepts `keyword`. Read totals from the paged responses of `GetInstancePage` or `/api/Admin/users/paged`; ordinary list endpoints do not implicitly paginate.

## Common request examples

Identifiers, tokens, and revisions below are placeholders. Read the relevant lists to obtain actual IDs and current revisions before making changes.

### Read capacity instances and a station document

```http
GET /Capacity/GetInstancePage?pageNumber=1&pageSize=10
Authorization: Bearer <token>
```

```http
GET /StationLayout/GetStationSchemes?instanceID=<instance-id>
Authorization: Bearer <token>
```

```http
POST /StationLayout/GetJson?instanceID=<instance-id>&stationSchemeID=<scheme-id>
Authorization: Bearer <token>
```

`GetJson` returns the station document. `SaveJson` accepts a `json` string, scope fields, and `expectedRevision`; `RepairJson` serves the repair workflow. The standalone module's Gateway helps handle these contracts consistently; see [development and integration](09-development-integration.md). Always pass an explicit instance and scheme for production business reads instead of relying on the legacy file fallback without an instance.

### Generate an initial plan from multiple processes

```http
POST /OperationPlan/GenerateTrainOperationPlanFromProcess
Authorization: Bearer <token>
Content-Type: application/json

{
  "instanceID": "<instance-id>",
  "stationSchemeID": "<scheme-id>",
  "operationPlanID": "<plan-id>",
  "processes": [
    { "processTemplateID": "<process-a>", "revision": 3, "trainCount": 2 },
    { "processTemplateID": "<process-b>", "revision": 1, "trainCount": 3 }
  ],
  "startTime": "08:00",
  "endTime": "09:00"
}
```

Processes with positive counts must exist and match the supplied revisions. A transaction writes trains, movements, and process snapshots for the batch; failure rolls back these business writes. There is no batch idempotency key: submitting the same successful request again creates another batch. If a response is lost, reload the plan before retrying. See [operation processes](05-operation-process.md) for constraints and count limits.

### Preview solver input

```http
POST /api/capacity-agents/preview-input
Authorization: Bearer <token>
Content-Type: application/json

{
  "instanceId": "<instance-id>",
  "stationSchemeId": "<scheme-id>",
  "operationPlanId": "<plan-id>"
}
```

Submit a generic job to `POST /api/capacity-agents/jobs` with `{ "agentId": "...", "modelId": "...", "input": { ... } }`. Successful acceptance returns 202 and a job object. Read status and results from `GET /api/capacity-agents/jobs/{jobId}`. The dedicated `saturated-plan-jobs` endpoint builds input from the scope and `presetId` and associates it with the new-plan persistence workflow; a generic job does not automatically perform that workflow.

### Save and analyze a sketch scheme

1. Read the document and its `revision` with `GET /SketchLayout/schemes/{id}`.
2. Submit `{ "revision": <current-revision>, "document": { ...complete scheme... } }` to `PUT /SketchLayout/schemes/{id}`.
3. Update the client with the returned revision after a successful save.
4. Call `POST /SketchLayout/schemes/{id}/analysis` to analyze the server's saved document. It does not read unsaved browser edits.
5. Delete using `DELETE /SketchLayout/schemes/{id}?revision=<current-revision>`.

The Sketch controller request limit is 8 MiB; DWG import allows up to 20 MiB. A reverse proxy must also permit the corresponding request size; both limits apply.

## Concurrent edits and errors

| Status | Common cause | Client handling |
| --- | --- | --- |
| 400 | Missing fields, invalid scope, infeasible graph constraints, invalid file or JSON | Show validation details and correct the input |
| 401 | Missing or invalid credentials, unavailable account | Refresh once when appropriate, otherwise log in again |
| 403 | Authenticated user lacks scope access | Check the user, instance, and role |
| 404 | Object missing or unavailable within the scope | Reload the list and check selection and IDs |
| 409 | Revision conflict, missing dependency, unavailable agent allocation, or similar conflict | Reload or restore dependencies according to the response; do not force an overwrite |
| 413 | Application or proxy rejects the request size | Check the file size and both limits |
| 429 | Authentication or registration rate limit | Wait for the window to expire; avoid login retry loops |
| 500 | Schema, storage, or calculation failure | Record operation scope, time, and error, then inspect service logs |
| 503 | Job dispatch failure or another unavailable service | Inspect the returned job's status before retrying |

Process template updates and entity operations use the template `revision`. New templates are submitted at 0 and created at 1. Entity `collection` must be a collection supported by the controller, and a mutation returns the complete template. Station documents use `expectedRevision`; Sketch uses the scheme revision. These counters belong to different objects and are not interchangeable.

Missing hump dependencies can return 409 with `code: HUMP_REFERENCE_MISSING`, compatibility field `legacyCode: HUMP_CALCULATION_DEPENDENCY_MISSING`, and `missingDependencies`. This is not a concurrent editing conflict; reselect or restore the listed dependencies.

Not every GET is free of side effects. A legacy operation-plan list read can create a default plan, and some legacy data paths prepare or migrate structures. Validate integrations against a prepared test database rather than automatically traversing production endpoints.

## Database responsibilities and hierarchy

| Configuration section | Main objects | Relationships and identity |
| --- | --- | --- |
| `HumpDatabase` | `user`, `refreshtoken`, `humpinstance`, `slopeline`, planar/vertical geometry, wagons, conditions, calculations, and headway results | Account source for all modules; instance Owner mainly stores the username |
| `CapacityDatabase` | `capacityinstance`, `stationscheme`, Node/Link/Cell layout, `stationroute`, `stationroutetime`, plans, trains, movements, processes, charts, and analysis | Instance → station scheme → operation plan → train → movement |
| `SketchLayoutDatabase` | `sketchlayoutscheme` | Ownership by `OwnerId`; `DocumentJson` stores the scheme, alongside name, description, and revision |

Capacity train, movement, and process templates belong to the **station scheme's shared library**. The compatibility schema still includes `OperationPlanID`, but the shared key is `''`. Deleting an operation plan does not delete the scheme's shared templates.

A process train's `trainprocesssnapshot` is keyed by instance, scheme, plan, and train. It stores the source template ID and revision, complete process copy, origin in seconds, activity-to-movement mapping, event times, `SelectedRouteIDs`, and execution precedence. Dwelling activities now reference real StationRoute records. `SelectedTrackIDs` and temporary dwelling resources in older reviews describe an earlier implementation.

Base schemas are in [Database](../../SwitchYard.WebApi/SwitchYard.Service/Database). Further schema and legacy data changes are handled by [OperationPlanSchemaMigration](../../SwitchYard.WebApi/SwitchYard.Service/Services/OperationPlanSchemaMigration.cs), [StationLayoutSchemaMigration](../../SwitchYard.WebApi/SwitchYard.Service/StationLayout/StationLayoutSchemaMigration.cs), and [OperationProcessRouteMigration](../../SwitchYard.WebApi/SwitchYard.Service/Services/OperationProcessRouteMigration.cs). Running only the base SQL does not complete every upgrade. See [deployment and operations](08-deployment-operations.md) for migration and rollback.

## Deletion and related data

The application primarily maintains relationships using transactions and scoped conditions, rather than a single cross-database foreign-key cascade. Maintenance scripts must not delete by display name or an isolated local identifier alone.

| Operation | Current effects |
| --- | --- |
| Delete a capacity instance | Removes its schemes, layouts, routes, timing, templates, plans, trains, movements, snapshots, charts, and analysis before deleting the instance |
| Delete a station scheme | Removes its layout, routes, shared templates, and all plan-related data |
| Delete an operation plan | Removes its trains, movements, snapshots, chart settings, and analysis; retains scheme-shared templates |
| Delete a train | Removes its movements and snapshot and invalidates associated plan analysis |
| Delete a route | Checks references from actual movements, templates, processes, and snapshots; refuses while referenced, then removes timing and related analysis once references are resolved |
| Change hump geometry | May clear affected calculation curves, retarder states, and related headway results while retaining editable calculation settings for recalculation |
| Delete a sketch scheme | Removes the scheme and facilities, routes, workflows, and settings within its JSON; requires the matching revision |
| Delete a user | The current user deletion service deletes the user record; it is not a cascade across business data in all three databases |

Capacity instance “copy” currently copies instance metadata, not all child schemes and plans. Use the appropriate scheme or plan copy function when duplicating those objects. Hump instances have a dedicated copy service; the scope of “copy” differs across modules.

Check ownership before deletion and back up or copy usable schemes when needed. If an analysis becomes empty after editing or deleting data, check whether related results were invalidated and need recalculation. [hump-orphan-cleanup.sql](../../SwitchYard.WebApi/SwitchYard.Service/Database/hump-orphan-cleanup.sql) is an administrator maintenance tool for historical orphan data, not an ordinary user workflow.

Implementation: [CapacityDataLifecycle](../../SwitchYard.WebApi/SwitchYard.Service/Services/CapacityDataLifecycle.cs), [HumpDataLifecycle](../../SwitchYard.WebApi/SwitchYard.Service/Services/HumpDataLifecycle.cs), [SketchLayoutStore](../../SwitchYard.WebApi/SwitchYard.Service/Services/SketchLayoutStore.cs), and [UserService](../../SwitchYard.WebApi/SwitchYard.Service/Services/UserService.cs).

## Agent communication

Agents connect to `/hubs/capacity-agent` and call `RegisterAgent`, `ReportStatus`, `ReportProgress`, and `CompleteJob`. The server dispatches work with `Solve`. Registration includes models, access policy, resource capacity, and IDs of still-running jobs. The current HTTP API and Hub do not provide a standard browser cancellation operation. Stopping an agent affects its active Workers.

See [CapacityAgentContracts.cs](../../SwitchYard.WebApi/SwitchYard.Capacity/CapacityAgentContracts.cs) for registration, job, result, and model input structures, and [installation](07-installation.md) for execution. API in-memory job state is not a database queue that can be backed up and resumed.
