# Sketch Layout Design and Operation Analysis

[Documentation index](README.md) · [中文](../zh-CN/06-sketch-layout.md)

Sketch Layout Planning draws facilities and logistics links, defines operations, and analyzes flow relationships, occupation conflicts, and crossing queues within one time range. Its schemes are independent of railway capacity Nodes, Cells, StationRoutes, and solver Agents; there is no automatic exchange between these models. This chapter describes the implementation as of 2026-10-11.

## Complete workflow

1. Sign in, open Sketch Layout Planning, and create a named scheme in scheme management.
2. In Layout design, draw polygon facilities and circular entrances/exits, then connect them with logistics links.
3. Configure link names, directions, and anchors, and inspect crossings to analyze.
4. Choose Route crossings to generate independent markers, check their source links, and save.
5. In Process design, create process plans, set each plan's quantity, and add operations and steps.
6. In Layout analysis, set the layout-wide departure horizon, SLP thresholds, crossing probability model, and occupation durations, then Save and analyze.
7. Read operation-pair conflicts, crossing probabilities, queue metrics, and flow relationships separately; they are not one interchangeable probability.

Regular users manage their own schemes; administrators can view and manage all schemes. Scheme management supports searching by name, description, or creator, and creating, opening, editing details, and deleting schemes.

## Drawing and editing

In normal mode, click to select, drag empty space to pan, and use the wheel to zoom. Select a drawing tool to enter that mode; Esc abandons unfinished drawing.

| Operation | Method |
| --- | --- |
| Draw a facility | Add polygon vertices, then click the first point or press Enter to close |
| Draw an entrance/exit | Create a circle and edit its center and radius |
| Draw a logistics link | Start at a circle center, polygon vertex, or edge midpoint; click empty space for anchors; end on another facility |
| Adjust geometry | Drag facilities, vertices, or anchors; finite coordinates can also be entered in properties |
| Rename | Double-click a name or renamable object in normal mode; Enter saves and Esc cancels |
| Add a vertex or anchor | Double-click a polygon edge; Shift+double-click a link or use its properties to add an anchor |
| Delete an anchor | Right-click it or remove it in properties |
| Resize panels | Drag splitters, fine-adjust with arrow keys, or double-click to reset |

Polygons require nonzero area, no self-intersection, and at least three valid vertices. A logistics link connects two different facilities and binds its endpoints to actual facility positions. Invalid coordinates or geometry produce an error and are restored.

Properties support facility fill/border colors and link line/arrow colors in `#RRGGBB` format; clearing a color restores its default. Link direction is forward, reverse, or both. Older documents without direction use both. Step selection and backend validation enforce direction; adjust any existing process using the opposite direction before changing a link.

### Grid and view settings

Grid display and snapping are enabled by default, with spacing 20 and a permitted range of 1–500 canvas units. Display and snapping are independent. Zooming out thins displayed dots without changing snap spacing. New vertices, centers, anchors, and dragged objects follow snapping; dragging a whole facility preserves its shape.

The canvas supports negative coordinates and positions outside the original 2400×1600 area. Fit all includes facilities, link anchors, and visible crossing markers without clipping or moving objects. Show link names is off by default; the selected link's name remains visible.

Curve display is on by default and replaces polyline corners with tangent circular arcs of default radius 100, reduced for short segments. Polygon fillets are independently enabled by default with radius 150 and apply only to eligible right/obtuse corners; acute, straight, and concave corners remain unchanged. These switches preserve original vertices, anchors, and endpoint bindings, but the currently displayed link shape affects geometric conflict analysis.

Grid, colors, display options, markers, and document edits support undo/redo and scheme saving. Panel widths are view state only.

## Crossing markers are generation snapshots

Route crossings computes intersections of different links using their current straight/rounded shapes. Common endpoints and coincident segments do not produce markers, and intersections at the same position merge. Each marker stores source link IDs and their names at generation time.

**Every generation replaces the entire previous marker collection**, renumbers it, restores radius 24, and clears per-crossing occupation overrides. No intersections also clears old markers. Replacement is one undoable action. More than 1000 resulting markers rejects replacement and preserves the original collection.

Showing/hiding markers does not recalculate them. Moving a marker, changing its radius, editing a link, or switching curve display does not change its recorded sources. Deleted links retain their historical source names and a deleted indication; renamed links show current and recorded names. After geometry changes, review markers and regenerate when necessary, then reapply occupation durations.

Markers are not process steps. Crossing probabilities and queues use only saved `sourceLinks`; analysis does not generate missing intersections. Geometric conflicts between complete operations directly inspect current paths instead, so these two analyses have different sources.

## Process plans and operations

A layout can have multiple process plans, each containing independent operations. A plan groups operations and supplies their common quantity; it does not put them in sequence. All operations independently start within the same layout-wide departure horizon.

1. Create a process plan and set its common operation quantity.
2. Add and select an operation, then click facilities in order on the canvas to append steps.
3. Edit facility dwell seconds and the separate travel seconds for entering the next facility. Choose among links that permit the required direction.
4. Review per-run duration, total duration, and draft status in the vertical list. Copy/delete operations or copy the entire plan as needed.
5. Save scheme at the top. Drafts can be saved, but analysis requires every operation in every plan to contain at least two facilities.

The step UI alternates facility dwell → link travel → facility dwell. Storage remains facility-based: `dwellSeconds` is the current facility's stay; `incomingLinkId/travelSeconds` describe independent travel before reaching that facility. The first step has no incoming link and zero travel; later steps require a valid connecting link and positive travel time.

Truncating a facility step removes its incoming travel and subsequent steps. Deleting facilities/links referenced by processes is blocked; update the steps first. Repeated visits to a link accumulate additional flow and crossing passage counts.

The plan's quantity applies to every operation and step. For quantity 10 and two operations lasting 60/90 seconds, total durations are 600/900 seconds and the layout counts 20 executions. This does not require two operations to run sequentially for 1500 seconds.

## Layout-wide analysis settings

Analysis combines all process plans; no individual plan selection is required. Settings are saved at root `analysisSettings`:

| Parameter | Default and range |
| --- | --- |
| `horizonSeconds` | 3600 seconds; 1 second to 365 days; denoted H |
| `aThreshold/eThreshold/iThreshold` | 0.8/0.5/0.2; require 1 ≥ A > E > I > 0 |
| `crossingModel` | `poisson-fixed`; alternatives are `uniform-fixed` and `poisson-exponential` |
| Per-crossing, per-link duration override | 0.01 seconds to 365 days; empty means automatic |

Automatic duration is the passage-count-weighted average travel time for that link across all operations. Expanded crossing rows configure independent crossing-zone occupation times. Restore automatic clears the override. Facility dwell times are not part of this service time.

Editing parameters or the document invalidates old results. Save and analyze again. The analysis API reads the saved server document; it does not accept an unsaved draft as temporary analysis input.

## SLP flow relationships

Every transition between adjacent facilities adds the plan quantity to flow. Forward and reverse counts are summed for each facility pair and normalized by the greatest pair total in the layout:

```text
Relative flow intensity = pair's bidirectional total / greatest pair total
Positive flow is graded by the A/E/I lower bounds; remaining positive flow is O
Zero flow is U
```

These are configurable flow-based rules for this module, not an evaluation of non-flow relationships or X relationships. Multiple links connecting the same pair share its grade.

Flow relationships shows a table above a triangular diamond activity relationship chart. Each facility pair appears once, including U relationships. A diamond's upper cell is its grade and the lower cell its bidirectional total. Hover or keyboard-focus for directional counts and intensity. Large matrices scroll, and the horizontal splitter adjusts table/chart height.

## Conflict probability between operations

This calculation fixes step durations and assumes each execution starts independently and uniformly in `[0,H]`, with completion allowed after H. Facilities and links are single-capacity resources. Same-direction, opposite-direction, and geometrically intersecting/overlapping links can conflict.

For start-time difference `Δ = startB − startA` and relative occupation intervals `[a,b)` and `[c,d)`, overlap occurs when:

```text
a − d < Δ < b − c
```

All resource conflict intervals are merged and integrated against the triangular distribution of the difference of two independent uniform starts. This avoids counting the same pair repeatedly. Different operations have `n×m` execution pairs; one operation paired with itself has `n×(n−1)/2`.

The results are the probability that one pair conflicts at least once and the expected number of conflicting pairs. Expectations can be added, but the total is not the probability of any conflict anywhere in the system. Rounded arcs use 24 subdivisions per arc; intersecting links are mutually exclusive for their full travel durations. This is a conservative occupation estimate, not a collision probability.

## Crossing passage conflict probability

For each saved marker, count source-link passages `Nᵢ`, including repeated visits, define `λᵢ=Nᵢ/H`, and use occupation duration `mᵢ`. Different source links in one marker are treated as pairwise mutually conflicting.

The aggregate `ΣNᵢPᵢ/ΣNᵢ` is the probability that a randomly selected passage overlaps at least one occupation on another participating link. It is neither the whole-horizon probability nor a queueing delay.

Let `Bᵢ=Σⱼ≠ᵢλⱼmⱼ` and `Rᵢ=Σⱼ≠ᵢλⱼ`:

| Model | Conditional conflict probability and scope |
| --- | --- |
| Poisson arrivals, fixed occupation | `Pᵢ=1−exp(−Bᵢ−Rᵢmᵢ)` |
| Poisson arrivals, exponential occupation | Durations are means; `Pᵢ=1−exp(−Bᵢ)/(1+Rᵢmᵢ)` |
| Uniform arrivals, fixed occupation | Fixed counts start independently in `[0,H]`; integrate conditional non-conflict probability below |

```text
ℓᵢⱼ(u) = min(H,u+mᵢ) − max(0,u−mⱼ)
Pᵢ = 1 − (1/H) ∫₀ᴴ ∏ⱼ≠ᵢ (1−ℓᵢⱼ(u)/H)^Nⱼ du
```

Poisson models use stationary arrivals, including unfinished occupations that began before the horizon; exponential durations are independent marks per arrival. The uniform model uses fixed counts beginning within the horizon, without pre-zero arrivals. It combines conditional probabilities before integrating, correctly accounting for boundaries and durations exceeding H.

Repeated visits or use of several source links by one operation produce a correlation-approximation warning. This probability covers cross-link overlap only, excluding same-link headways, dispatch avoidance, and queues. If only one source link has traffic, cross-link probability is zero while queueing may still exist.

Uniform integration uses absolute tolerance `1e-9`, depth 30, at most one million factor evaluations per marker and five million across the layout. Nonconvergence or exhausted budgets return `calculation-limit` rather than an unfinished probability.

## Crossing queue metrics

Queueing is a separate exclusive single-server, first-come-first-served model with unlimited waiting space, including same-link waiting. `Wq` is mean waiting seconds excluding service; `Lq` is waiting queue length excluding the job being served; `ρ=ΣNᵢmᵢ/H` is load.

Poisson models use M/G/1:

```text
Wq = ΣλᵢE[Sᵢ²] / [2(1−ρ)]
Lq = (Σλᵢ)Wq
Fixed occupation: E[Sᵢ²]=mᵢ²; exponential occupation: E[Sᵢ²]=2mᵢ²
```

For `ρ≥1`, the result is `unstable` with no finite steady state. Delay and queue length are null; the actual load is retained and may exceed 100%. Null does not mean zero.

The uniform model starts with an empty queue, reproducibly samples fixed counts, and simulates FCFS service. Wq includes each job's full waiting time after H as necessary. Lq is waiting-queue area only over `[0,H]`, divided by H, so the steady-state Little formula is not substituted. Loads above 100% can still produce finite-batch estimates.

Limits are 20,000 passages per marker, 64–2048 repetitions, two million simulation events per marker, and eight million across the layout. Insufficient budget returns `calculation-limit`, retaining load without delay/length estimates. Expanded rows show repetitions and standard errors for both estimates. Fixed seeds and source ordering make identical inputs reproducible.

Each crossing is assessed independently, without propagating upstream/downstream delay or simulating priorities. Probability and queue calculations report status separately; a limit in one does not hide the other's available result.

## Result canvas and states

Analysis uses a split layout: Conflict analysis/Flow relationships on the left and a shared canvas on the right. Links use their facility-pair AEIOU color. Crossing circles use a continuous load scale from 0–75%, with the highest red color at and above 75%, and 32% fill opacity. Probability figures use a separate 0–100% scale.

Missing data is gray rather than zero-load color. A known load still colors a marker when queue simulation exceeds limits. Analysis displays all saved markers, including those hidden on the design page, without changing design colors or visibility.

| Status | Meaning |
| --- | --- |
| `ready` | This metric was computed |
| `no-traffic` | No passages; all three queue metrics are zero and probability shows no-traffic state |
| `missing-sources` | Fewer than two source links, so no valid crossing analysis |
| `missing-links` | A recorded source link was deleted; review or regenerate markers |
| `calculation-limit` | Integration/simulation budget exhausted; reduce scale or select another model |
| `unstable` | Poisson queue load is at least one, with no finite steady state |

## Persistence and limits

A scheme is one JSON document stored atomically in `sketchlayoutscheme`. Root collections include `facilities`, `links`, `crossings`, `plans`, and `analysisSettings`. Process hierarchy is `plans[] → chains[] → steps[]`; every plan has `executionMode="parallel"` and one `quantity`.

PUT sends `{ revision, document }`; DELETE uses `?revision=N`. A stale revision returns 409. Reload and merge edits rather than overwriting another window's changes. Development defaults to a separate SQLite `sketchlayout.db`; production defaults to a separate MySQL `sketchlayout` database. Deployments must configure it in addition to the capacity database.

For older documents without root analysis settings, identical old per-plan settings are retained; otherwise defaults are used. Missing plan quantity takes the first old operation's quantity, or 1 for an empty plan. Missing direction means both; missing execution mode means parallel. Saving persists these normalized values without adding business tables.

| Object or calculation | Limit |
| --- | --- |
| Request body / scheme name / description | 8 MiB / 120 characters / 2000 characters |
| Facilities / links / plans / markers | 200 / 400 / 50 / 1000 |
| Polygon vertices / anchors per link | 100 / 24 |
| Operations per plan / total steps per plan / steps per operation | 100 / 1000 / 100 |
| Plan quantity | 1–1,000,000 |
| Approximation segments used in analysis | 5000 across the layout |
| Operation combinations / occupation comparisons | 50,000 / 5,000,000 |

Oversized whole-layout operation analysis fails explicitly rather than returning partial results that omit plans. Drawing geometry does not create railway topology or infer travel times; durations remain business inputs.

## Troubleshooting and further reading

| Symptom | Action |
| --- | --- |
| Cannot append the next facility | Check a connecting link exists and permits that direction |
| Saved but analysis fails | Complete every draft in every plan to at least two facilities |
| A marker no longer matches an intersection | Markers are snapshots; review and regenerate, then reapply duration overrides |
| Regeneration removed settings | Whole-collection replacement resets properties; undo restores old markers |
| Low conflict probability but large delay | The measures differ; queueing includes same-link jobs and grows near full load |
| Load exceeds 100% | Poisson queues have no finite steady state; finite uniform batches may still yield full-wait estimates |
| Cannot delete a facility/link | Remove its process references first |
| Save reports a revision conflict | Reload the current revision and reconcile local edits |

The preserved sketch-layout description is in the [historical project README](../archive/project/README.en.md). Implementation references: [models](../../SwitchYard.WebApi/SwitchYard.SketchLayout/Models.cs), [validation](../../SwitchYard.WebApi/SwitchYard.SketchLayout/Validation.cs), [operation analysis](../../SwitchYard.WebApi/SwitchYard.SketchLayout/SketchLayoutAnalyzer.cs), [crossing probability](../../SwitchYard.WebApi/SwitchYard.SketchLayout/CrossingConflictAnalyzer.cs), [queueing](../../SwitchYard.WebApi/SwitchYard.SketchLayout/CrossingQueueAnalyzer.cs), and [canvas](../../switchyard-vue/src/sketchlayout/SketchCanvas.vue).
