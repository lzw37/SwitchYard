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
| Capacity | Create, read, update, delete, and copy capacity instances | [CapacityInstance](../../SwitchYard.WebApi/SwitchYard.Capacity/Database/CapacityInstance.cs) |
| StationLayout | Schemes, JSON, repair, DWG, routes, dwelling routes, and timing | [Layout requests](../../SwitchYard.WebApi/SwitchYard.Capacity/StationLayout/StationLayoutRequests.cs), [module contracts](../../SwitchYard.StationLayout/backend) |
| OperationPlan | Plans, trains, movements, batch edits, occupancy import/recalculation, charts, and analysis persistence | [Controller](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/OperationPlanController.cs), [occupancy import models](../../SwitchYard.WebApi/SwitchYard.Service/Models/CellOccupancyImportModels.cs) |
| OperationProcess | Templates, catalog, activity/event entities, and revision control | [Process models](../../SwitchYard.WebApi/SwitchYard.Service/Models/OperationProcessModels.cs), [process controller](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/OperationProcessController.cs) |
| CapacityAgents | Available agents, presets, solve inputs, jobs, and saturated plans | [Shared agent contracts](../../SwitchYard.WebApi/SwitchYard.Capacity/Contracts/CapacityAgentContracts.cs) |
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

The returned input includes a `stationLayout` base-data snapshot containing every record in the current instance and station scheme, without filtering by operation plan:

| Field | Contents |
| --- | --- |
| `stationSchemeId` | The layout's station scheme; records in the layout collections no longer repeat this field |
| `cells` | Cell data, excluding `instanceId`, `stationSchemeId`, and `extraProperties` |
| `routes` | Route data, including train types, node and link lists, interrupt Cells, tags, and endpoints; excludes `instanceId` and `stationSchemeId` |
| `calculationParameterSets.sets` | Parameter set IDs, names, and default flags; excludes `instanceId` and `stationSchemeId` |
| `calculationParameterSets.routeOccupations` | A dictionary keyed by `calculationParameterSetId`, with occupation-row arrays as values; rows exclude `instanceId`, `stationSchemeId`, and the repeated `calculationParameterSetId` |
| `routeOperationTimes` | Operation duration `operationTimeS` for each parameter set, route, and train type; excludes `instanceId` and `stationSchemeId` |
| `links`, `nodes` | Topology Link and Node data, excluding `instanceId`, `stationSchemeId`, and `extraProperties` |

Read occupation parameters from `stationLayout.calculationParameterSets.routeOccupations[parameterSetId]`; the layout no longer has a flat `routeOccupations` array. The dictionary key supplies the parameter set ID, which is not repeated in each row. Sets with no occupation rows have empty arrays. Legacy rows whose parameter set ID is `null` are grouped under the `default` key. If the scheme has no saved parameter sets yet, `sets` includes the effective `default` set supplied by the system.

Snapshot field names use camelCase. Retained fields preserve their database values, including `null` and original list strings. The existing top-level `routes` and `routeOccupations` remain the projections used by the solver, with occupation parameter set IDs taken from the dictionary keys. The snapshot supplies base data without adding solver constraints. The Agent also accepts the former parameter-set array for compatibility with saved job inputs. Retrying an older job with its original input does not add the current layout.

The top-level input also includes an `operationProcesses` array alongside `stationLayout`. It contains every saved operation process template in the current instance and station scheme (the template library uses `OperationPlanID = ''`), including templates unused by the current plan and incomplete drafts. Unsaved browser edits are not included. Templates do not repeat the instance, scheme, or plan identifiers.

| Fields within `operationProcesses` | Contents |
| --- | --- |
| `id`, `name`, `description`, `revision` | Template identity, name, description, and revision |
| `activities`, `events` | Complete activities and events, including candidate routes, selected resources, durations, and diagram positions |
| `precedences`, `anchors`, `routeAnchors` | Event ordering, anchors, and route-endpoint anchor associations |

Nested fields use camelCase, including `nodeId`, `automaticAnchorIds`, `dwellingActivityId`, `routeIds`, and `routeId`. Template times, durations, and precedence intervals remain in minutes; activity `x` and `y` retain their diagram coordinates. Event `nodeList` values are refreshed from current routes when read, matching the editor API. Older inputs without `operationProcesses` are read with an empty array.

Generated trains reference the current saved template in that array through `trains[].operationProcessId`. Duration bounds, fixed times, precedences, and anchor constraints use the latest template version. Each train carries the execution origin `processOriginSeconds` in seconds; `movements[].activityId` maps each movement to its template activity. Original event times are recovered from each movement's `originalStartSeconds`, `originalEndSeconds`, and start/end event IDs, without a `processEventTimes` dictionary. Standalone fixed events use the execution origin plus the template time; standalone events without a fixed time are determined by process constraints. Original times may violate subsequently edited constraints and are adjusted by the solver. Ordinary trains have no process reference. A deleted template or activities that no longer correspond one-to-one with existing movements require regenerating the operation plan.

New inputs omit `trains[].processConstraints`; the Solver invokes shared domain graph construction to reconstruct constraints from the reference. Legacy jobs containing that field remain readable, and older inputs' `processEventTimes` fields are ignored. Solver results retain `eventTimes`. Inputs with process references require model version `2.5.0` or later, so update CapacityAgent and its bundled Solver before using them. Inputs containing only ordinary trains continue to require `2.4.0`.

Movement fields `startLocationsByRoute` and `endLocationsByRoute` are keyed by candidate route ID, with each location in their lists represented as an object. `nodeId` identifies the node, `anchorId` the anchor, and `dwellingRouteId` the dwelling route selected for that anchor. Absent values are explicitly `null`. For example:

```json
{
  "startLocationsByRoute": {
    "R1": [{ "nodeId": "N1", "anchorId": null, "dwellingRouteId": null }]
  },
  "endLocationsByRoute": {
    "R1": [{ "nodeId": "N2", "anchorId": "A1", "dwellingRouteId": "D1" }]
  }
}
```

The former names `startLocationIdsByRoute` and `endLocationIdsByRoute` remain readable; output uses only the new names. Agent still accepts legacy encoded-array strings (such as `"[\"N1\",null,null]"`) and plain node strings in these lists. Serialization always writes objects; malformed encoded arrays are rejected. Inputs containing location objects also require model version `2.5.0` or later, even without a process reference.

Submit a generic job to `POST /api/capacity-agents/jobs` with `{ "agentId": "...", "modelId": "...", "input": { ... } }`. Successful acceptance returns 202 and a job object. Read status and results from `GET /api/capacity-agents/jobs/{jobId}`. The dedicated `saturated-plan-jobs` endpoint builds input from the scope and `presetId` and associates it with the new-plan persistence workflow; a generic job does not automatically perform that workflow.

### Shared capacity domain model

The current domain model lives in `SwitchYard.Capacity/Domain`, with public types in the `SwitchYard.Capacity` namespace. `SwitchYard.CapacitySolver` references that shared library and builds a per-job object graph with `CapacityProblem.Build(input)`. The Agent handles communication and scheduling, invoking the Solver library in Worker subprocesses. Routes, cells, links, nodes, and parameter sets share canonical entity instances; occupation records directly reference their `Route`, `Cell`, and `ParameterSet`. Original fields remain available through each entity's `Input` or `Layout`, and graph construction does not change the transport JSON.

`Train.OperationProcess` references the shared template, while `Train.Process` contains that train's execution graph. `Movement.Activity`, `StartEvent`, `EndEvent`, `CandidateRoutes`, and `RouteOptions` are object references. Locations link their `Node`, `Anchor`, and `DwellingRoute`. Template activities, events, and anchors resolve within their template; execution events, anchors, and locations remain isolated per train, with candidate locations additionally scoped by event. Solver constraints use these relationships; IDs remain at input resolution, solver variable keys, and result output boundaries.

Unreferenced drafts and missing layout metadata produce `problem.Issues` entries containing the source path, target type, and ID. Legacy inputs can supply basic entities through their solver projections. Missing required activities, events, anchors, or occupation parameters still fail explicitly. The payload contains no switch or signal definitions, so their IDs remain unresolved and are reported; train-type codes and tags remain values.

Types used to communicate with CapacityAgent are grouped in `SwitchYard.Capacity/Contracts`, including authentication, registration, resource status, solve commands, progress, results, and layout, parameter-set, process, and location input DTOs. Their JSON converters live in the same directory. Public names, the `SwitchYard.Capacity` namespace, and JSON formats remain unchanged.

Types mapped directly from database queries are grouped in `SwitchYard.Capacity/Database`, covering capacity instances, calculation parameter sets, layout rows, operation plans, and analysis results. The Service queries database rows and maps them to Agent input DTOs in `Contracts`; domain objects and graph-building logic live in `Domain`.

Other shared types are grouped by feature: `SwitchYard.Capacity/StationLayout` contains layout JSON, read/write requests, coordinate transforms, DWG import, route search, and calculation parameter-set requests. `SwitchYard.Capacity/OperationPlans` contains operation-plan API types, occupation data serialization and validation, and station-plan and resource-occupancy chart settings. All types retain the `SwitchYard.Capacity` namespace; the project file is the only file at the project root.

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

See [CapacityAgentContracts.cs](../../SwitchYard.WebApi/SwitchYard.Capacity/Contracts/CapacityAgentContracts.cs) for registration, job, result, and model input structures, and [installation](07-installation.md) for execution. API in-memory job state is not a database queue that can be backed up and resumed.
