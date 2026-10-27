# Operation Processes and Train Generation

[Documentation index](README.md) · [中文](../zh-CN/05-operation-process.md)

An operation process uses activities, events, precedences, and anchors to express a train's complete operational constraints, including parallel activities, time bounds, and alternative routes. This chapter describes the implementation as of 2026-10-11: editing templates, generating trains, and understanding subsequent editing and solving.

## Scope and objects

Open Capacity → Operation plan → Operation process. Select an instance and station scheme first. Process templates and traditional train templates are shared by the scheme; no operation plan is needed until you generate actual trains.

| Object | Meaning | Main fields |
| --- | --- | --- |
| Activity | Arrival, departure, shunting, locomotive, or dwelling work | `type`, `minDuration`, `maxDuration`, `startEvent`, `endEvent`, `routeList` |
| Event | A time/location boundary, normally an activity endpoint; standalone events are also possible | `time`, `nodeList`, `anchorList` |
| Precedence | A time relationship between two events | `leadingEvent`, `followingEvent`, `interval`, `isExact` |
| Anchor | A dwelling location selected consistently for one train | `routeIDs`, `dwellingActivityID` |
| Route endpoint anchors | Anchors used at the two endpoints of a candidate route | `routeID`, `startAnchor`, `endAnchor` |
| Template | The object collections and their version | `id`, `revision`, `activities`, `events`, `precedences`, `anchors`, `routeAnchors` |

Template times, durations, and intervals use **relative minutes**. Actual plans and solver input use seconds. A traditional Movement stores its minimum duration as integer seconds, rounded upward during conversion; the complete snapshot retains original precision and maximum duration.

All five activity types use actual StationRoutes from the current scheme: `Arrival`, `Departure`, `Shunting`, `Locomotive`, and `Dwelling`. Dwelling also uses a dwelling route; a physical Link is not an execution resource. Selected resources are consistently stored as `selectedRouteIDs`.

## Build a process

1. Choose New or Load example and name the template. Loading an example creates an unsaved template.
2. Add activities. Set type, name, minimum/maximum duration, and alternative routes of that type.
3. Select activity endpoints to inspect candidate Nodes and anchors. Candidate Nodes are derived from route endpoints and cannot be edited directly.
4. Choose Add precedence, select a preceding activity's end event, then another activity's start event. The preview becomes a relationship only when you click the target.
5. Set a minimum or exact interval in minutes. The default is a minimum interval of zero.
6. Review automatic dwelling anchors and per-route endpoint anchors. Add manual anchors for other shared locations where needed.
7. Save, then preview or generate. Incomplete drafts can be saved; saving does not establish that a process is executable.

Exact precedences use solid lines; minimum precedences use dashed lines. Esc cancels an unfinished connection. The current drawing tool creates end-to-start relationships. The model and existing data also support start-to-start, end-to-end, and other event combinations; later processing preserves these instead of replacing them with movement order.

Resize the canvas, properties, and object list with splitters or keyboard controls; the object list can be hidden. Diagram positions only affect presentation. Activities display all alternative route names, and event markers display candidate anchors.

Copy process includes unsaved edits, every object and reference, and diagram positions. The copy has a new ID and independent revision 1, with a copy suffix and sequence number where necessary. Failed saving retains the copy draft. The source and its draft remain unchanged.

## Time semantics

```text
Activity: 60 × minDuration ≤ t(end) − t(start) ≤ 60 × maxDuration
Minimum precedence: t(following) − t(leading) ≥ 60 × interval
Exact precedence: t(following) − t(leading) = 60 × interval
Nonempty event time: t(event) = train origin seconds + 60 × time
```

An exact zero interval means simultaneous events; a minimum zero interval permits a later event. Older records without `isExact` mean a minimum interval. The API accepts the historical spelling `intervel` but returns `interval`.

Activity start/end event associations are fixed after creation and cannot be replaced through saving. Renaming an activity updates its event names while retaining IDs and other constraints. The current event properties do not edit times, but nonempty event times in existing data remain effective and validated.

Generation rejects missing references, invalid durations, negative intervals, dependency cycles, and infeasible timing. Exact intervals propagate later constraints backward and may delay preceding events. The system does not silently relax an equality into a lower bound.

## Location and anchor semantics

A precedence constrains time only. It does not by itself require equal nodes or contiguous routes. Shared events and anchors establish location consistency.

- A nondwelling activity's start event collects candidate route starts, and its end event collects route ends.
- A dwelling route can be used in either orientation. Both events may use either endpoint, including arrival and departure on the same end.
- Events using the same anchor in one train must select the same dwelling route for that anchor. Its two endpoint nodes both belong to that location.
- Multiple anchors are alternatives. Overlapping candidate lists do not merge all anchor groups. Existing `selectedAnchor` values and fixed route choices still restrict selection.
- Activities sharing an event must agree on its physical node and anchor choice as well as its time.

Adding a dwelling activity creates an owned anchor whose candidate routes follow the activity. Its start/end events share the anchor. Direct precedence connections to a dwelling event add that anchor to the connected event. Disconnecting, deleting the dwelling activity, or changing its type cleans up derived references while preserving other manual anchors.

Do not independently change or delete an automatic anchor through the anchor list; change its owning dwelling activity. The backend derives candidate Nodes and automatic relations on save/read, so submitted fields cannot override them. The old single `routeID` is read as a one-element `routeIDs` array.

## Three generation workflows

### Convert to a traditional train template

In Train templates, choose Generate from process and select a saved process and its current revision in the same scheme. This creates a shared train template without overwriting templates, modifying the source, or requiring a target plan.

Each activity becomes one traditional movement template, retaining alternative routes and converting minimum duration to integer seconds. Ordering uses a stable topological order of the event graph, not canvas positions. Maximum durations, relative fixed times, precedence intervals, parallelism, and anchors cannot all fit in the traditional sequential format; the conversion returns warnings.

Initial quantity is 1, train type is empty, and fixed operation is off. Generate process trains directly when complete constraints must be retained.

### Append process trains in a batch

1. Select the target in Train operation plan and choose Generate from processes.
2. Enter a quantity for each saved process; zero skips that row.
3. Set the shared start/end range, check source revisions, and submit.
4. Inspect new train numbers, movements, selected routes, occupations, and the Process constraints details.

Each row allows 0–1000 trains; the batch must contain 1–1000 trains and at most 20,000 activities. Time formats are `HH:mm`, `HH:mm:ss`, and `D+days HH:mm:ss`. If the end is not later than the start, whole days are added until it is later; equal clock times therefore cross a day boundary. The absolute end may not exceed 604,800 seconds from day-zero midnight.

All selected processes append in one generation transaction. Any validation, revision, or write failure rolls back the batch. Legacy template migration occurs independently before generation and is not undone by a later generation failure. A stale source normally returns 409; refresh the process and revision before retrying.

Each process distributes its own trains independently within the same range, so first trains from multiple processes can start together. Generation does not optimize Cell conflicts among new trains or with existing trains. The batch endpoint has no batch idempotency key. If a response is lost, refresh the target plan before retrying to avoid a duplicate batch.

### Add one process train in the station plan chart

Choose Add train from process, select a saved process, enter train number/name/type, origin time, and latest end, and select an allowed route for every activity. A unique candidate is filled automatically. Source choices that are already fixed remain locked.

Preview validates the complete graph, resources, anchors, and time constraints without saving a train. Preview again after changing any input. Confirmation repeats revision and constraint checks, then saves the train, all movements, and its independent snapshot in one transaction. This workflow cannot move the explicitly requested origin to another time; change the inputs if it is infeasible.

User selections narrow only this train's snapshot, leaving the shared process unchanged. A duplicate train ID is rejected. Failed creation retains the draft for correction.

## Initial scheduling algorithm

For one process, let `A` be activity count, `N` train count, `[S,E]` the time range in seconds, and `D` the greatest minimum activity duration in seconds:

```text
available = max(0, E − S − ceil(D/60) × 60)
slot = available/60/(N×A−1) when N×A > 1; otherwise 0
target origin for train i = S + round(i×A×slot) × 60, starting with i = 0
```

This is a whole-minute placement target. Batch scheduling moves it to the nearest origin permitted by the event graph. With the origin fixed, difference constraints find the earliest feasible times for all events together. Minimum/maximum durations, fixed times, and exact/minimum precedences apply simultaneously, preserving parallelism.

For example, assume fixed arrival/dwelling/departure durations of 2, 8, and 1 minutes, linked by zero intervals with feasible resources. Generating two trains between 08:00 and 09:00 gives target origins 08:00 and 08:31, with each train lasting 11 minutes. This illustrates placement, not freedom from conflicts between trains.

Resource selection rotates its initial candidate by train index, then backtracks to satisfy shared events and anchors. It does not optimize load or crossing conflicts. Limits are 100,000 resource attempts per train and 100,000,000 difference-graph relaxations per process scheduling call. Reduce quantities or candidate complexity if a limit is reached.

## What an actual plan stores

| Record | Contents |
| --- | --- |
| `train` | Train identity, number, name, and type within an instance/scheme/plan |
| `movement` | Activity name, route, minimum duration, times, order, actual nodes, and Cell occupations |
| `trainprocesssnapshot` | Complete process copy, source ID/revision, origin and event seconds, activity-to-movement map, selected routes, and explicit precedences |

Snapshots are identified by instance, scheme, plan, and train. Later source edits or deletion do not modify generated trains. Names exceeding a legacy 50-UTF-16-unit field are shortened at complete Unicode text-element boundaries with a warning; full names remain in the snapshot.

Explicit plan precedences retain `id`, `leadingEvent`, `followingEvent`, `minIntervalSeconds`, and `isExact`, including fractional seconds. Missing lists in older plans are recovered from their own snapshot, not the current shared template. An explicit list inconsistent with the saved process is rejected.

Deleting a train, copying/changing the ID/deleting a plan, and traditional automatic regeneration maintain snapshot lifecycle. Individual activities of a process train cannot be added, deleted, or reordered independently. Edit the source and regenerate instead.

## What edits mean for the next solve

Existing Movement times, durations, and routes can be edited in the table. Cells can be adjusted in occupation charts, and events or shared-anchor tracks can be adjusted in the station plan chart. These save the actual plan without rewriting original process constraints.

| Action | Stored change | Meaning for a subsequent solve |
| --- | --- | --- |
| Adjust one Cell | Absolute Cell start/end times | Input derives offsets for the matching route and Cell |
| Edit Movement times, route, or duration | Movement fields | The process snapshot restores original event references, bounds, and candidates |
| Edit the shared process | A new template revision | Existing trains remain unchanged |
| Save a solved saturated plan | New Movement, Cell, and snapshot data | Uses solved event times and routes |

A visually plausible edit therefore does not prove complete process feasibility. Distinguish snapshot event times from the current actual plan when inspecting Process constraints. To change the process used by the next solve, edit it and regenerate.

The current model is `station-capacity-v1` 2.3.0, supporting exact precedences and dwelling-route anchors. See [Capacity solving](04-capacity.md#solving-and-saturated-plans) for compatibility and actual version checks.

## Storage validation and limits

Storage permits incomplete business configuration but requires structurally valid arrays, IDs, and finite values. Full preview/generation additionally requires executable names, types, resources, and constraints.

| Item | Current limit |
| --- | --- |
| Template and object IDs | 100 characters without leading/trailing whitespace |
| Names / description | 200 / 4000 characters |
| Activities | 2000 |
| Events, anchors, route-anchor configurations | 4000 each |
| Precedences | 10000 |
| Batch trains / total activities | 1000 / 20000 |
| Actual process time range | Within seven days of day-zero midnight |

Templates use revisions; stale revisions cannot overwrite newer versions. Reload and reconcile your edits after a conflict. Templates previously stored per plan migrate into scheme libraries. Legacy track references are backed up before conversion to actual dwelling routes; ambiguity aborts the transaction instead of guessing resources.

## Troubleshooting

| Symptom | Cause and action |
| --- | --- |
| Saved successfully but generation fails | Draft saving is allowed; complete names, routes, durations, and constraints |
| No dwelling resource | Check actual dwelling routes and their type; naming a physical Link is insufficient |
| Connected precedences use different tracks | Precedences constrain time; review automatic or manual anchors |
| Many anchor candidates but no feasible plan | One dwelling route must satisfy all related events of the train |
| Trains overlap after batch generation | Processes place trains independently; run capacity solving afterward |
| Exact precedence rejects generation | Check equality intervals, fixed events, and maximum durations |
| Editing the source does not update trains | Execution snapshots are independent; regenerate the required trains |
| A network failure leaves creation uncertain | Refresh the target plan before retrying the batch endpoint |

## Further reading

- [Preserved operation-process guide](../archive/guides/operation-process.md): detailed graphical operations and APIs; some old track-field descriptions are compatibility background.
- [Preserved process-generation review](../archive/reviews/process-generation-review.zh-CN.md): a historical review. Its Link-based dwelling, `selectedTrackIDs`, empty Route, and temporary solver resource descriptions have been superseded by real dwelling routes; old test totals are not current verification results.
- [Preserved capacity guide](../archive/guides/Capacity.md).
- Implementation: [models](../../SwitchYard.WebApi/SwitchYard.Service/Models/OperationProcessModels.cs), [scheduler](../../SwitchYard.WebApi/SwitchYard.Service/Services/OperationProcessPlanScheduler.cs), [batch generation](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/OperationPlanFromProcessController.cs), [single-train generation](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/OperationPlanProcessTrainCreationController.cs), and [process input](../../SwitchYard.WebApi/SwitchYard.Service/Services/StationCapacityProcessInputBuilder.cs).
