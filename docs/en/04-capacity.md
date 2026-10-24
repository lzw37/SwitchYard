# Capacity Analysis and Operation Plans

[Documentation index](README.md) · [中文](../zh-CN/04-capacity.md)

This chapter covers the workflow from a saved station layout through operation planning, solving, graphical editing, reports, and simulation. It describes the repository implementation as of 2026-10-11. See [Operation processes](05-operation-process.md) for detailed process constraints.

## Data hierarchy and prerequisites

Capacity data follows this hierarchy: calculation instance → station scheme → operation plan → train → movement. Routes, occupation parameters, process templates, and traditional train templates belong to the station scheme. Plans in that scheme share the template library; copying or deleting one plan does not copy or delete the shared templates.

Copy instance currently creates instance metadata only; it does not copy child schemes, drawings, or operation plans and is not a complete backup. Station-scheme copying and plan copying have their own data scopes.

Select an enabled instance that you can access, then select the station scheme on each page. Template editing does not require an operation plan. Train editing, solving, and simulation require a target plan. Check the selected names after changing scope.

| Page | Purpose and required preparation |
| --- | --- |
| Station layout | Create and save geometry, nodes, links, equipment, and track circuits |
| Route design | Define endpoints, find routes, assign route types and interrupting cells |
| Calculation parameters | Set start and end occupation offsets for each route and Cell |
| Operation plan | Edit processes, templates and trains; inspect charts and analysis |
| Model solving | Generate input JSON, choose an online Agent, submit jobs, save presets |
| Simulation | Replay a saved plan on a 2D or 3D layout |
| Results analysis | The standalone tab remains a placeholder; use analysis tabs inside Operation plan |

Hover over or keyboard-focus circular buttons to read their labels. Shared splitters support dragging, arrow keys, larger steps with Shift, Home/End, and Enter/double-click to reset. EN/中 changes interface language; business names are not translated.

## Prepare the station and routes

1. Create or select an instance in Instance management.
2. Create a station scheme in Station layout. Import a DWG or draw manually, repair topology, and save.
3. Check Node, Link, Cell, and equipment bindings. Intersecting drawing lines do not by themselves establish topology. Review the result of automatic splitting, node generation, and nearby-node merging.
4. Configure route endpoints in Route design, optionally deriving them from signals and buffer stops.
5. Find individual candidates or generate routes in batches. Review types, endpoints, nodes/links, switches, signals, and Cells.
6. Check interrupting cells and save. Dwelling activities require actual dwelling routes; a physical Link cannot replace a process route candidate.

Fit all scales and centers the view within its current container without changing coordinates. Use it again after resizing panels. Saved Cell label fonts, sizes, colors, and other scheme styles are reused by other 2D views.

Topology edits may affect equipment, Cells, and existing routes. Review the equipment-deletion prompt when removing a bound node, then recheck routes. Visual proximity alone does not establish connectivity.

Each route has a multiple-selection Train types field. Arrival and departure routes offer Passenger train and Freight train; other routes offer Passenger coach (rolling stock) and Freight wagon (rolling stock). Changing route type within a group preserves the selection; changing groups selects the new group's defaults. An empty selection can also be saved.

New routes, including automatically generated and imported routes, default to both types in their group. On the first backend startup after upgrading, SQLite and MySQL databases gain a `stationroute.TrainTypes` field containing a JSON array, and existing routes receive the corresponding defaults. Repeated migration does not overwrite saved selections.

## Calibrate occupation parameters

Each station scheme can store multiple calculation parameter sets. In Calculation parameters, click the round gear button beside the set selector to open the management dialog. Create, copy, rename, delete, and default actions are available inside the dialog. The Copy parameters icon duplicates all route parameters, including common and train-type-specific offsets, under a new name. Copies are independent: editing or deleting the source does not change the copy. New and copied sets can be made the default; choose another default before deleting the current one.

After selecting a set, select a route, inspect directly traversed and interrupting Cells, set start/end occupation offsets, and save. Operation time (s) records route end minus route start and can be edited manually. Updating calculation parameters from a traction result saves it atomically with the circuit offsets. A separate relational table stores one duration per parameter-set route and train type; parameter-set copies and deletions include it.

Prominent vertical lines mark operation start (zero) and operation end (the saved duration). Each track circuit has two draggable anchors: start equals the start offset, and end equals duration plus end offset. One blue rounded rectangle spans these anchors. The raw start offset is shown to the left of the start anchor in green, and the raw end offset to the right of the end anchor in red. While dragging, a matching dashed guide joins the active anchor to its operation start or end reference; releasing or cancelling the drag hides it. If the absolute end precedes the start, a warning appears and dragging retains the last valid position; invalid saved values still show both anchors for correction. An unknown duration remains empty, and segments appear once a duration is provided. Dragging chart endpoints, shifting offsets together, and applying settings by route type affect only the selected set. Train-type-specific offsets take priority over common offsets within that set. Generate or enter parameters for an empty set, or copy an existing set instead.

Plan generation supports a parameter set selection per train template or process. Individual train creation and editing also support selection. Unassigned sets remain empty in storage and in the operation plan list. Occupation generation and calculation use the current scheme default. Deleted or other-scheme references also fall back to the current scheme default. Changing a train's set recalculates occupations while preserving manually edited times. Changing the default, editing parameters, or deleting a set does not overwrite saved plan occupations; use Recalculate occupations when needed.

Backend startup upgrades SQLite and MySQL automatically: a scheme-scoped `calculationparameterset` table is added, `stationroutetime` and `train` gain `CalculationParameterSetID`, and legacy parameters join the initial default set while existing trains remain unassigned and use the default at calculation time. Copying a station scheme includes its parameter sets, default selection, and all parameter rows.

Update and restart CapacityAgent as well (model version 2.4.0 or later) so solving uses each train's parameter set. The backend rejects older models for these inputs.

```text
Initial Cell start seconds = movement start seconds + start occupation offset
Initial Cell end seconds   = movement end seconds   + end occupation offset
```

Offsets express early locking and delayed release. Signs, seconds versus minutes, and day offsets affect conflict results. The separate Traction calculation page can convert complete circuit occupation times from the backend into these offsets. Choose the parameter set in Track circuit occupancy details and click Update calculation parameters.

## Create and maintain operation plans

Under Operation plan, use plan management in Train operation plan to create, select, copy, or delete a plan. Expand a train and edit movement names, types, candidate/selected routes, minimum durations, and start/end times. Save before analysis.

| Entry point | Source | Effect on the target plan |
| --- | --- | --- |
| Train template: automatic generation | Shared traditional sequential templates | Rebuilds the plan using the traditional rules; check the target first |
| Train template: generate from process | A saved process | Adds a shared train template, without creating trains |
| Train operation plan: generate from processes | Several saved processes and their quantities | Appends complete trains and independent process snapshots |
| Station plan chart: add train from process | A saved process and per-activity route selections | Previews and appends one complete process train |
| Station plan chart: point-and-click train creation | Chart endpoints and times | Saves a train and all movements after confirmation |
| Import CellOccupancy | UTF-8 CSV | Creates a new plan by default, or appends to the current plan |

Search the train list by number, name, or type. Space-separated terms are matched individually. Select all includes only saved trains in the current filter; changing the filter clears selection. Batch deletion removes selected trains, their movements, and snapshots. Review the selected count first.

### Import CellOccupancy CSV

Click Import CellOccupancy in the train-plan toolbar. Select a UTF-8 CSV of at most 5 MB and 50,000 occupation records, preview it, then confirm import.

```csv
TrainID,MovementID,MovementType,CellID,OccupancyStartTime,OccupancyEndTime
C2551,0,Arr,1G,02:09:00,02:14:00
C2551,1,Dw,1G,02:14:00,02:30:00
C2551,2,Dep,1G,02:30:00,02:33:00
```

Rows with the same train and MovementID describe different Cells of one movement. Include every occupied Cell. In the preview, check Cell mappings, train/movement counts, and routes to reuse or create. New routes must form valid paths in the existing station topology. Incompatible occupation parameters cause a separate route to be created rather than altering an existing route's common parameters.

Duplicate train numbers reject the entire append. Re-preview if the file or related station/plan data changes. Final import saves the new plan, routes, and movements in one transaction; any failure rolls back the import.

## Occupation times and persistence

Actual occupations are stored in `movement.CellOccupationsJson` and returned as `cellOccupations`. Each item contains `cellID`, `routeID`, `startSeconds`, `endSeconds`, and `isInterruptCell`. Times are seconds from midnight on plan day zero, with day offsets and fractional seconds supported.

Generation, import, and saving solved results write these records. The backend materializes missing records for older plans. Saved records are not recomputed from common parameters on every read. When movement times or train type change, the backend updates occupations that were not manually edited and retains explicitly edited absolute Cell times.

Recalculate Cell occupation times uses the database's latest movement times, train types, routes, and parameters to rebuild every occupation in the current plan. It overwrites manual Cell times, clears legacy movement-level offsets and undo history, and leaves movement start/end times and source process snapshots unchanged. Calculation or write failure rolls back the whole operation.

Editing a process train's Movement or Cell does not rewrite its complete process snapshot. The next solve still restores event references, duration bounds, and candidate routes from that snapshot. Cell overrides apply only to matching routes and Cells. To change process definitions, edit the source and regenerate the required trains.

## The two plan charts

### Occupation plan chart

The Gantt chart has Cell rows and a time axis and reads backend occupation times directly. Cells associated with named tracks use teal markings and a pale green background; other cells use gray-blue markings. Classification uses actual Link references and nonempty track names.

- Drag a block horizontally or drag an endpoint to edit only that Cell's absolute occupation times, without moving the entire movement or other Cells.
- Drag a dwelling block vertically to change track. The target Cell must identify one unambiguous named track with a real dwelling route and all necessary associated routes.
- Saving uses a batch request containing original records. A concurrent edit or write failure restores the entire change. Esc cancels the current drag.
- Fit, X/Y scaling, and refresh follow the same interaction used by calculation parameters and simulation occupation charts.

### Station plan chart

The station plan chart uses nodes/named tracks on the vertical axis and shows each Movement's actual endpoints and times. Named tracks can collapse into one row; double-click the track name to expand its two endpoints. Display elements and their order are saved per plan. Nodes currently used by movements remain available in the chart.

Thin lines for process trains follow saved event precedences: solid for exact intervals, dashed for minimum intervals. Ordinary trains use movement order. Choose straight or orthogonal lines, and color by source process, route type, or train type. The operation-time toggle shows the units digit of event minutes; seconds are not rounded up.

1. Click lines to select multiple trains. Esc or empty space clears selection.
2. Dragging a start handle normally shifts both movement endpoints and subsequent movements. Dragging an end handle shifts that end and subsequent movements. For this action, subsequent means movement record order.
3. Exactly coincident handles of the same train at the start of the action move together. Hold Ctrl to move only the current endpoint. Arrow keys allow fine adjustment.
4. Double-click a handle to align related events through exact precedences and propagate changes through directed event relationships. Contradictory changes cancel the action.
5. Right-click a dwelling handle to switch between passing and stopping. Converting a pass to a stop adds two minutes by default; the reverse aligns both ends to the earlier time.
6. Drag a dwelling segment vertically to change track. Process trains propagate changes through their shared anchors. Missing any required matching route cancels the whole action; movement times are retained.

Graphical editing is not a complete process solver. Check minimum durations, precedences, and resource conflicts before using the plan for conclusions. Hovering over an activity shows conflicts derived from actual overlapping Cell intervals even when conflict highlighting is disabled.

A batch adjustment is one save and one undo action. Ctrl+Z/Ctrl+Y retain up to 20 actions; switching plans, reopening the page, or external data changes clears history. Text inputs handle their own shortcuts. Track changes regenerate occupations; undo restores the original routes and manual times.

## Solving and saturated plans

Start the separate CapacityAgent and connect it to the current service. The browser submits and displays jobs; the Agent runs the mixed-integer model with OR-Tools, preferring SCIP and falling back to CBC if unavailable.

1. Select a scheme and plan in Model solving, generate input, and check its scope, trains, route candidates, and offsets.
2. Select an online authorized Agent with available job slots and model `station-capacity-v1`.
3. Set left/right shift tolerances, time limit, threads, and objective. Submit and watch the job state.
4. Check both `hasSolution` and `status`; download result JSON that you need to retain. A completed job does not guarantee a feasible solution.
5. Save a preset. Select it at the top of Operation plan and choose Generate saturated plan to save a validated feasible result as a new plan.

The current model version is **2.3.0**. Process location/anchor semantics require 2.2.0, exact precedences require 2.3.0, and fractional occupation offsets require at least 2.1.0. Use the current 2.3.0 Agent when combining these features. Input JSON may show `minimumModelVersion` as 2.1.0 when fractional offsets are present, but before dispatch the service also inspects the complete process and exact precedences and enforces the higher 2.2.0/2.3.0 requirement. That field alone does not establish compatibility.

Defaults are 50,400 seconds for each shift tolerance, a 3,600-second limit, 12 threads, and `min-end-time`. Agent CPU allocation can further limit actual threads.

| Objective | Quantity minimized |
| --- | --- |
| `min-end-time` | Sum of movement end times |
| `min-duration` | Sum of movement durations |
| `min-start-and-end` | Sum of movement start and end times |

Ordinary trains use adjacent movement sequencing and route connections, with movement variables limited to one day. Process trains use event graphs, duration bounds, fixed events, exact/minimum precedences, shared locations, and anchors, within a maximum seven-day horizon. Different trains using the same Cell must have nonoverlapping occupations. This conflict logic skips movements of the same train; it is not a complete detector for all resource conflicts within one train.

Ordinary solve jobs and result JSON are held in service memory and do not update plans automatically. Do not rely on them surviving a service restart. Saving a saturated plan creates a new plan with solved movement times, selected routes, Cell occupations, and process snapshots, preserving the source plan. It solves the existing trains; it does not add trains to search for the maximum possible train count.

`totalOccupationTimeSeconds` is the greatest occupation end time. `capacityValue = trainCount × 64800 / greatest occupation end seconds`, or 0 when that denominator is nonpositive. This measure depends on the plan's time origin and is neither a maximum station capacity nor the bottleneck measure below.

## Capacity analysis and reports

Inside Operation plan, use the occupation table, bottleneck analysis, and category summary. Set a consistent statistical period `T` and allowance for unused time `α`, then refresh or calculate.

```text
K(Cell) = nonfixed occupation seconds / [(1 − α) × (T − fixed occupation seconds)]
Route bottleneck = the related Cell with the greatest K
Estimated route capacity = route operation count / bottleneck K
```

This sums individual occupation durations; it is not the busy fraction obtained by merging overlapping time intervals. Missing valid denominators or utilization values do not imply unlimited capacity. Categories aggregate their assigned routes; routes assigned to multiple categories prevent simple addition into a station total.

Analysis snapshots and display settings can be saved. Recalculate after changing the plan and distinguish historical snapshots from current data. The station plan toolbar exports a Word capacity report covering processes, plan charts, occupations, bottlenecks, and categories, retaining current chart settings. It uses all supplied data regardless of search filters or train selection and identifies historical snapshots. Report prose is currently Chinese; selecting the English interface does not generate an English report.

## 2D and 3D simulation

In Simulation, select a scheme, plan, and trains. Switching between 2D and 3D preserves progress, speed, movement sequence, Gantt settings, and single-train/all-train scope. Leaving the tab pauses playback. Default speed is 60×; play, pause, reset, or scrub the time slider.

Concurrent activities highlight separately: arrival red, departure blue, locomotive green, and shunting yellow. Real dwelling routes use the plan's dwelling times; the train remains stationary at the track center while an early-locking departure route can appear simultaneously. The 3D view offers vehicle, display-scale, label, and occupation options.

Replay displays existing timing and paths; it does not solve or repair conflicts. Check the plan, real routes, Cell times, topology, and nodes before refreshing an incomplete display.

## Traction calculation

This section describes the implementation as of 2026-10-23. Open Traction in the capacity workspace, retain the current instance and station scheme, and select a saved route. Arrival stops at the endpoint; departure starts at rest. Through runs can join a second route whose start node matches the first route's end node. Entry and exit speeds are required boundary conditions.

The toolbar uses small, labeled station and route selectors, followed by a single-line run summary. The summary, speed-position chart and occupancy chart fill the workspace. Drag the line between the charts to change their heights, or focus it and use the up/down arrow keys; double-click to restore equal heights. Each area scrolls independently. Use the Track circuit occupancy details toolbar button to open the circuit timing table in a dialog. The Relative occupancy start and Relative occupancy end columns preview the exact integer-second values that will be saved. An uncleared arrival circuit shows end offset zero; undetermined values show an em dash. Route IDs distinguish offsets for consecutive routes. The speed chart status bar always keeps a fixed height, showing placeholders when no position is selected.

The upper dark chart shows the current line speed limits, the last calculated running speed, and route-bound turnouts, curves and signals. Turnout numbers have a prefix; signals show only their own names. Insulation markers are omitted from this chart. Track circuit extents and names appear on the position axis. Show outside station is enabled by default. Turning it off hides the approach before the station entrance in both charts and expands the station route across the position axis, retaining original position and time coordinates. The switch changes only the view and does not calculate. The lower chart uses the same position coordinates for head/tail passage and track-circuit occupation. Occupancy runs from head entry to tail exit; a separate red rectangle spans the time from tail exit to release, with the same circuit width. No red rectangle is drawn for zero release delay or unknown timing. Move the pointer or use arrow keys to inspect both charts; use the timing details button to open exact values in a dialog. Joints and circuits still beneath a stopped train remain uncleared, without invented release times. If a route endpoint remains inside a circuit, the actual boundary lies beyond the calculation range and its entry or clearance time remains unknown; shading covers only the known spatial range. Continuations that immediately reverse over the same link are rejected.

Click the permitted-speed line to enter editing, then double-click the line to insert an anchor at the existing limit. Drag an anchor to adjust its position and speed. Right-click an anchor for the delete menu, or press Delete on a selected anchor. Escape or clicking elsewhere in the charts exits editing and hides the handles and menu. No editing buttons or numeric input toolbar are shown. The 0 m origin remains fixed in position, with an editable limit. Each anchor applies from its position up to the next anchor, or the route end. Edits synchronize with the section table and save as individual database rows for the current set and route. They never trigger calculation: running-speed and occupancy results remain unchanged until Calculate is clicked. The limit curve can also be edited before the first calculation.

The Calculation set selector switches between independent inputs and results within the same station; the selection is saved in the database. Manage supports new sets, copies including saved results, renaming and deletion. New sets have no result until Calculate is clicked. Each set can contain many route results. Every primary route and optional continuation pair owns its train catalog, signalling, stops and line inputs. Calculate replaces only that route record. Switching routes restores their own inputs and results. Route results lists all records in the current set and supports viewing, switching and deleting individual records. Set management shows route/result counts; copying a set includes every route. Keep 1–100 uniquely named sets; existing single-set data migrates into Default calculation set.

The Train section selects a short passenger, long passenger or freight example preset and summarizes its basic parameters. Manage train parameters opens one dialog to add, edit or delete presets, including name, length, mass, maximum speed, rotating mass coefficient γ, and the traction, service-braking and resistance tables. Enter γ directly from 0 to 2: for example, 0.1 means an effective inertial mass of 1.1 times the train mass. Existing multipliers are displayed as coefficients, so a stored 1.08 appears as 0.08 without changing its physical meaning. Keep at least one preset. All three characteristics use editable speed–force points. Constant service force and Davis A/B/C inputs remain as table generators: Generate and replace table samples every 10 km/h through maximum speed. Calculations interpolate the stored points; editing generator inputs does not replace existing table data. Edits remain drafts until saved; Cancel leaves the calculation unchanged. Saving applies the selected preset; click Calculate to recalculate. The catalog and selection are saved immediately to the database for the current instance and station scheme. Other calculation inputs save automatically; Save to database retries or saves explicitly. When no database workspace exists, existing browser presets and parameters migrate once after a successful database write. Database data takes precedence on later visits. All three initial presets are editable examples, not real vehicle specifications. Edit gradients, limits and extra resistance opens the separate line table. Line edits apply immediately and save automatically to the database. Force is in kN, mass in t and speed in km/h; resistance is R=A+Bv+Cv². Traction speeds start at zero and strictly increase; forces are nonnegative and nonincreasing, interpolate linearly, and remain at the final force beyond the last point. Route setting, reaction and release delay are in seconds: time zero is the route request, head entry is setting plus reaction, and release delay follows tail passage.

Distance uses supplied link lengthM extensions first, then declared file units and display transforms. If units are absent, explicitly enable and calibrate the manual metres-per-drawing-unit scale. Geometric lengths replace curve tangents with arc lengths. The initial scale of 1 does not establish surveyed metres. Missing speed limits use an editable default and missing gradients assume level track. Each link can override speed and gradient; positive gradient is uphill in the travel direction. Equipment types do not automatically determine turnout or curve speed limits; enter limits from line data.

The calculation uses spatial discretization, a backward braking envelope and forward traction integration. The default step is 2 m, with speed-limit, device and tail-clearance boundaries inserted. Speed-dependent force points interpolate linearly; force balance includes quadratic resistance, gradient and rotating mass. Gradient is weighted over a uniformly distributed train mass. Acceleration beyond a low-speed section waits for the complete train to clear it. Tail passage at x is head passage at x plus train length, with interval time inverted from acceleration. Departure/through clearance beyond the endpoint assumes constant final speed and is identified in the assumptions; the first gradient extends upstream. Adhesion, variable wind drag, brake propagation/build-up, signal enforcement curves and actual off-route motion are outside this model.

Page load and refresh read the saved database result. Editing inputs does not calculate. Only Calculate submits a new run and replaces the stored result after a successful commit; errors retain the previous plot. Selected consecutive routes, train presets and traction nodes, signal parameters, stops, distance settings and section overrides are stored in normalized database tables per instance and scheme. Each route record within a traction calculation set retains its latest complete calculation input and result, including speed samples, devices, joint times, circuit occupancy and warnings. Reload restores the saved result even when its inputs are older. Changed inputs show a pending marker; running-speed and occupancy charts and exports use the saved snapshot while the limit curve shows the current edits, and occupancy updates require matching current inputs. Revisions prevent concurrent pages from silently overwriting one another, and transactions keep prior data intact when a write fails. No traction JSON document is stored in a database column; see [relational storage](../zh-CN/traction-database.md). Export speed samples as CSV or the full parameters, profile, assumptions and timing record as JSON. In the occupancy dialog, select the parameter set shared with Calculation parameters and click Update calculation parameters to save public circuit occupancy offsets for the selected route and its continuation. For arrival calculations, route start is the first head arrival at the station route entrance, and route end is the first head arrival at the route destination. Each traversed circuit's start offset is its earliest reservation time (the green route-setting rectangle begins at zero) minus route start, including setting, reaction and approach time. Its end offset is release time minus route end. Circuits still physically occupied at the final stop receive end offset zero, including occupied circuits clipped by the route end; this parameter convention does not invent a release time in the saved result or chart. Departure and through calculations retain physical circuit entry relative to departure from the route origin, including departure after dwell at a connection. Offsets are stored in integer seconds: start rounds down and a known release offset rounds up. Circuits with an undetermined occupancy state and other existing rows retain their values. Interruption circuits follow the existing parameter default rules. The Calculation parameters page refreshes the matching set and route after saving. Existing plan occupations and simulation timing are not recalculated. Consecutive routes save one at a time; partial failure reports completed routes and remaining failures so the update can be retried.

For arrival results, yellow rectangles in the track circuit occupancy plot show approaching time: all circuits share the instant when the head first enters the arrival route’s braking-distance range, and each interval ends at head entry into its own circuit. The rectangles have aligned top edges and each spans its circuit on the position axis; hover shows both times and duration. Green route-setting and gray reaction-time rectangles precede the yellow interval, using signal settings saved with the calculation. The three intervals are contiguous. Rendering interpolates the saved run using its saved braking distance and does not trigger recalculation.

## Troubleshooting

| Symptom | Action |
| --- | --- |
| Empty lists or disabled actions | Check instance status, permissions, and scheme/plan selection |
| No dwelling candidate or track change fails | Create actual dwelling routes and check types and associated arrival/departure/shunting routes |
| Changed parameters do not change occupations | Saved Cell times are retained; use recalculation after reviewing its overwrite effect |
| The next solve returns to an older reference | Movement editing and process snapshots are separate; check the source and snapshot |
| Agent unavailable | Check connection, authorization, slots, CPU/memory availability, and model version |
| No feasible solution | Check tolerances, durations, exact precedences, route candidates, anchors, and offsets |
| Simulation unchanged after solving | Save a saturated plan and select the new plan |
| Standalone Results tab is unfinished | Use the analysis tabs inside Operation plan |

## Further reading

- [Preserved capacity guide](../archive/guides/Capacity.md): historical instructions; this chapter updates its 2.0.0 model claim and some older chart/read-only descriptions.
- [Preserved operation-process guide](../archive/guides/operation-process.md): detailed graphical operations, Cell persistence, and API behavior.
- [CapacityAgent guide](../archive/development/capacity-agent.md): installation, authentication, resources, and execution.
- [Station layout module](../archive/development/station-layout.md): reusable editor integration.
- Implementation: [Operation plan page](../../switchyard-vue/src/capacity/OperationPlan.vue), [input builder](../../SwitchYard.WebApi/SwitchYard.Service/Services/StationCapacityInputBuilder.cs), and [solver model](../../SwitchYard.WebApi/SwitchYard.CapacityAgent/StationCapacityModel.cs).
