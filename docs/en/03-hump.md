# Hump design, rolling checks and simulation

[Documentation index](README.md) · [简体中文](../zh-CN/03-hump.md)

This guide covers the current `/hump` features, checked against the repository on 2026-10-11.
Start with the data relationships, then follow the complete workflow; see the [user guide](02-user-guide.md) for accounts and shared UI behavior.
The calculation section describes the implementation. Drawings and results do not imply that all engineering-code checks have been implemented.

## 1. Instances and dependencies

On the hump page, select an enabled instance at the top left.
Ordinary users access their own instances; administrators can manage a wider set of instances and their owners.
Disabled instances are omitted from the workspace selector.
Changing instance changes the plan, wagons, operating conditions, profiles, calculation cases and checking schemes together.

| Object | Contents | Location |
| --- | --- | --- |
| Hump instance | Container for one design dataset | Instance management at top left |
| Rolling line | Plan control points, segments, switches and retarders | Rolling-line management in the plan page |
| Wagon parameters | Length, mass, frontal area, axles and rolling class | Upper section of calculation parameters |
| Operating condition | Speeds, wind, density and temperature | Lower section of calculation parameters, titled “Calculation conditions” |
| Profile scheme | Positions, elevations and profile segments | Profile scheme management |
| Calculation case | Wagon, operating condition, line, profile and retarder states | Calculation-condition management in profile design |
| Checking scheme | Profile, rolling line, pushing speed and ordered cuts/wagons | Scheme management in rolling checks |

An operating condition and a calculation case are different records.
Create wagons and conditions first, then bind them to a profile's calculation cases before selecting cuts in the checking page.
Each cut record references a calculation case and uses a sequence number to define its order.

The workspace's instance manager supports creation, copying, renaming, enabling/disabling and deletion.
Supply a new name when copying; the system generates the ID. Copying preserves the original for comparison.
A new empty instance does not automatically contain all required design objects.
“Instance management page” in the user menu opens the administrator page `/hump/instancemanagement`, a different entry from the workspace dialog.

## 2. Complete workflow and outputs

| Stage | Required input | Main output |
| --- | --- | --- |
| Unfolded plan | Rolling line, control points and devices | Saved plan and device references |
| Calculation parameters | Wagons and operating conditions | Reusable input parameters |
| Profile design | Control points and complete calculation cases | Energy-height curves, speed and resistance details |
| Rolling checks | Profile, line and cut order | Speed–distance, time–distance and adjacent headways |
| 2D / 3D simulation | Valid checking scheme and trajectories | Time-based vehicle visualization |

Before a comparison, copy the instance or profile and use a name that identifies the experiment.
Changing one factor at a time makes curve differences easier to explain.
Names are not relationship keys; renaming an object does not create or repair its references.
Save each stage before proceeding to the next.

## 3. Unfolded plan

1. Open the plan page and select a rolling line.
2. If no line exists, create and save one in line management; existing lines can also be copied, renamed or deleted.
3. Add or insert control points in the lower table and enter their IDs and `X` positions.
4. After changing points, use Update beside the segment table and inspect endpoints, lengths, curvature, direction and location parameters.
5. Add switches in the switch tab, binding them to control points on the current line and supplying device parameters.
6. Add retarders in their tab and bind them to segments on the current line.
7. Compare the drawing with the tables, then use Save at the top.
8. Reload to verify when needed; Load reads server data, so save changes you intend to keep first.

Drawing selection and table selection are linked to help locate devices and points.
Point order, segment endpoints and device bindings should describe a continuous rolling path.
The location parameter distinguishes the rolling section `0` from the classification yard `1` in calculations.
Device positions participate in resistance and timing calculations; they are more than drawing decorations.
After plan changes, recalculate cases and checking schemes rather than continuing to use the previous results.

## 4. Wagon and condition inputs

In Calculation parameters, create and save wagon types and operating conditions row by row.
An editing row and a saved row are different states; inspect each row before leaving the page.

| Wagon field | Unit or meaning |
| --- | --- |
| Type name | Name referenced by calculation cases |
| Length | m; affects release timing in checks |
| Tare / payload | t; total mass `Q = tare + payload` |
| Frontal area | m² |
| Axle count | Used to adjust effective gravitational acceleration |
| Rolling-class label | `难行车`, `中行车`, `易行车`, mapped to `1`, `0`, `-1` |
| Gravity parameter `g` | m/s²; model default is `9.8` |

The Chinese rolling-class labels are recognized business values; do not replace them with arbitrary English strings.
Total mass must be meaningful; zero total mass makes divisions invalid.

| Operating-condition field | Unit or meaning |
| --- | --- |
| Crest speed | m/s; supplies the condition's initial kinetic energy |
| Rolling-section / yard speed | m/s; representative speed used by each region's resistance formula |
| Wind speed | m/s |
| Wind flag | `1` is headwind; `0` is labeled tailwind in the UI |
| Air-density parameter | Documented in the model as kg·s²/m⁴; do not directly treat the value as kg/m³ |
| Temperature | °C |

Divide source speeds in km/h by `3.6` before entering m/s.
The condition's representative speed differs from the instantaneous speed calculated at each position and shown in the result table.
See section 7 for the current treatment of wind flag `0`; the UI label does not imply arbitrary wind-direction dynamics.

## 5. Profile design and calculation cases

1. Open Profile design, create or copy a scheme and select it.
2. Move along the add-point line beneath the drawing and click the following “+”; an empty scheme initializes control points.
3. Drag a point vertically to change elevation; hold `Alt` when starting a drag to change its horizontal position.
4. Use the right-hand Data panel for exact positions and elevations; the segment table shows length, gradient and elevation difference.
5. Right-click a point to delete it. Scheme deletion is disabled in the UI when only one scheme remains.
6. Open calculation-case management and bind a saved wagon, condition, rolling line and current profile.
7. Edit Retarder states: select actual devices and set activation, total braking energy height and output ratio; save the case.
8. Select the case and verify the wagon, line and condition summary at the top.
9. Save manual table edits, then execute the calculation.

Retarder output uses `0–1`: `0.5` applies 50% of the configured total braking energy height; do not enter `50`.
A disabled retarder contributes no braking energy height to that case.
The same physical retarder can use different states in different cases for speed-control comparisons.

Adding, deleting or completing a point drag triggers saving; complete conditions may also trigger recalculation.
Unsubmitted table values do not mean an automatic drawing save has completed; check save and calculation messages.
On failure, inspect references and use the top Save/Calculate controls again; a moved drawing is insufficient evidence.

Side arrows expand or collapse tool and data panels.
Display switches control initial kinetic energy, resistance, kinetic and braking energy-height curves, devices and labels.
Horizontal and vertical zoom change the view, not the design data.
Use the right-hand energy table to inspect positions, energy heights and instantaneous speed; click the resistance area for the current position's breakdown.

## 6. Reading energy-height results

Elevation, initial kinetic energy height, cumulative resistance and cumulative braking determine the available kinetic energy height.
Energy height is measured in m: energy per unit weight. Not every curve represents a physical elevation.
The resistance breakdown contains rolling, wind, switch and curve terms, plus the associated input values.
Check the current position, wagon, condition and rolling line when inspecting a breakdown.
If a curve stops or speed becomes zero, first inspect energy exhaustion and the common extent of the plan and profile.
Screenshots can document comparisons; the current workflow does not automatically issue a complete engineering-review report.

## 7. Current calculation model

These formulas are transcribed from the implementation to make behavior reviewable; their applicability has not been independently validated here.
Let `Q` be total mass, `n` axle count, `z(x)` profile elevation and `v₀` the operating condition's crest speed:

```text
g' = g / (1 + 0.42 n / Q)
h₀ = v₀² / (2 g')
hK(x) = max(0, h₀ + z(0) - z(x) - hR(x) - hB(x))
v(x) = sqrt(2 g' hK(x))
```

Result elevation, resistance, braking and kinetic energy heights are rounded to 3 decimals; speed is rounded to 2 decimals.
The initial potential-energy reference is the elevation at `x=0`, so use a consistent distance origin.
Negative available kinetic energy is clamped to zero; it does not produce a negative speed.

Rolling resistance uses the following implementation, where `t` is temperature, `k=0/1` denotes rolling section/yard, and `c` is the wagon-class parameter:

```text
σ = 0.602 - (0.0012 + 0.00002 Q)t - 0.003 Q + 0.00002 t²
r = 2.439 + (0.00008 Q - 0.01743)t - 0.015 Q + 0.00017 t²
    + 0.1 v + 1.65 c σ + (1-k)0.39                     [N/kN]
```

The wind-resistance code fixes `β=0` and the drag-coefficient ratio at `1`, effectively using `ρ F (v + I vWind)² / (2Q)`.
`I=1` adds headwind speed; `I=0` removes the wind-speed term. It does not implement a full tailwind relationship with a negative relative wind contribution.
Record this current limitation when comparing tailwind cases.

Curve-resistance work is the total angle, including switch curvature, multiplied by `8 N·m/kN`.
Switch-resistance work is `24×facing count + 12×(trailing count+diamond count) + 48×slip/three-way count`.
Rolling and wind resistance are multiplied by each region's accumulated length, added to switch and curve work, then multiplied by `0.001` for energy height.
The calculation uses the two configured representative speeds; it does not iteratively recompute resistance from every instantaneous sample speed.

Each enabled retarder accumulates braking energy linearly across its bound segment:
`total braking energy height × output ratio × fraction traversed`, reaching the full value at the exit.
This assigns configured braking energy; it does not solve a physical feedback controller or vehicle collisions.

## 8. Rolling checks

1. Open Rolling checks and create a checking scheme.
2. Select the profile and rolling line, and enter a positive pushing speed.
3. Select cuts in release order; choices come from calculation cases under the selected profile.
4. Execute the check; the page saves its configuration before requesting refreshed results.
5. Inspect speed–distance and time–distance curves and equipment headway labels.
6. Toggle curve labels or expand an individual chart; rerun after input changes.

The checking scheme's pushing speed determines release times from wagon lengths; each cut's initial kinetic energy still uses its referenced operating condition's crest speed.
Check both values explicitly: changing one does not automatically replace the other.

Speed calculation samples the intersection of plan and profile `X` ranges and stops further sampling when speed reaches zero.
Running time uses a `1 m` spatial step, converts each interval using adjacent mean speed, and adds the release time.
Switch checking intervals currently extend `10 m` before and after the bound position; retarder intervals use their bound segment.
If a wagon cannot reach a checking interval, its later checking data is not plotted.

Adjacent headway is `following entry time - preceding exit time`, in seconds.
A negative value indicates modeled equipment-occupancy overlap. A positive value only indicates a gap and still needs comparison with actual requirements.
`GetHeadwayCheckResult` currently returns an empty result object; available outputs are curves and headway data, not a complete automatic engineering-code pass/fail assessment.

“Configuration saved, but results failed to refresh” means configuration and calculation are in different states.
Repair references or parameters and rerun; a save-success message is not a successful check.

## 9. Two- and three-dimensional simulation

Both simulations use a checking scheme, load its profile, request time and speed results, and build playback trajectories.

- Select a scheme and wait for trajectories before starting playback.
- Use Start, Pause, End, Previous second, Next second and Reset controls.
- Playback rate ranges from `0.25×` to `3×`; the progress slider seeks through simulation time.
- The 2D view shows wagon position, order, type and speed.
- The 3D view adds rotation, pan, zoom, camera reset, following and label controls.

Playback is disabled without trajectories; return to the checking scheme and inspect cases and references.
Playback rate changes presentation speed, not operating conditions or calculated running times.
Vehicle models, equipment and some dimensions are schematic; do not measure engineering dimensions from the scene.

## 10. Troubleshooting and data maintenance

| Symptom | Action |
| --- | --- |
| Instance list is empty | Check authentication, ownership and enabled status |
| No cuts are available | Create and save calculation cases for the selected profile first |
| Energy curves are absent | Check points, selected case, references and display switches |
| `HUMP_REFERENCE_MISSING` | Recreate or rebind objects identified by type, label and ID |
| `HUMP_CALCULATION_DEPENDENCY_MISSING` | Legacy dependency code; inspect the missing objects in the same way |
| Speed curve stops early | Inspect energy exhaustion, braking, common coordinate range and missing data |
| Simulation has no trajectory | Check for valid cuts and successful time/speed results |
| Deletion returns a conflict | Remove dependencies first; for example, a referenced calculation case cannot be deleted directly |

Base-data changes can invalidate old results; calculate and check again before using them.
Deleting a profile, rolling line or instance may clean up related records; copy first when a comparison must be retained.
When reporting a problem, record instance/scheme names and IDs, operation sequence, inputs, messages and save status.

## 11. Historical material and source references

The [original hump guide](../archive/guides/Hump.md) remains intact, including historical plan-design notes citing TB10062-99.
They mention curve radii of 200 m/individual 180 m in constrained conditions, intervening straight lengths of 15 m/10 m, a 1.8 m/0.5 m straight before the first switch, 30–40 m from crest to first switch, and braking-position, 42 m and 14 m placement notes.
Its old profile section lists only gradient/segment and grade-change-point requirements as topics to be completed.
These are retained historical notes. This documentation has not checked whether that edition is current or claimed that the software implements every listed rule.
The wind-resistance source separately cites TB 10062-2018; that comment also does not replace a clause-by-clause applicability review.

The [original promotional article](../archive/materials/SwitchYard-Hump-WeChat-Push.md) and [H5 promotional page](../archive/materials/SwitchYard-Hump-WeChat-Push-H5.html) retain demonstrations, the trial address and contact information for product introductions.
Their GIFs, trial website and QR-code placeholder are historical presentation material; availability and the deployed version were not verified during this documentation review.
Use this guide and the current code for technical behavior and limitations.

Source: [energy heights](../../SwitchYard.WebApi/SwitchYard.Hump/EnergyHeightCalculator.cs), [resistance](../../SwitchYard.WebApi/SwitchYard.Hump/HumpResistanceCalculator.cs), [speed sampling](../../SwitchYard.WebApi/SwitchYard.Hump/SpeedProfileGenerator.cs), [time and headways](../../SwitchYard.WebApi/SwitchYard.Hump/HeadwayCheck.cs), and [hump controller](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/HumpController.cs).
