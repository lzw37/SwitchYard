import { getMovementCellOccupationShifts, readMovementCellOccupationOverrides, type MovementCellOccupationOverride } from '../movementCellOccupation.ts'
import { resolveStationPlanMovementRouteID, type StationPlanCellTime, type StationPlanMovement, type StationPlanRoute, type StationPlanSegmentEdit, type StationPlanTrain } from './stationPlanView.ts'

export interface StationPlanEditRoute extends StationPlanRoute { type: string }
export interface StationPlanEditMovement extends StationPlanMovement { minDuration?: number | null }
export class StationPlanEditingError extends Error {
    reason = 'stale'
    constructor() { super('stale') }
}
interface CellWindow { start: number; end: number; override?: MovementCellOccupationOverride }

/** Store the user's edit verbatim. Route matching assists the edit; it never rejects it. */
export function editStationPlanSegment(
    train: StationPlanTrain, edit: StationPlanSegmentEdit, movements: StationPlanEditMovement[], routes: StationPlanEditRoute[],
    times: (movement: StationPlanMovement) => StationPlanCellTime[],
): StationPlanEditMovement[] {
    const routeMap = new Map(routes.map(route => [route.id, route]))
    const originals = new Map(movements.filter(movement => movement.trainID === train.id).map(movement => [movement.movementID, movement]))
    const changed = new Map<string, StationPlanEditMovement>()
    const windows = new Map<string, Map<string, CellWindow>>()
    const cellMappings = new Map<string, Map<string, string>>()
    const load = (id: string) => {
        if (changed.has(id)) return changed.get(id)!
        const original = originals.get(id)
        if (!original) throw new StationPlanEditingError()
        const movement = { ...original, routeID: resolveStationPlanMovementRouteID(original.routeID, original.routeIDList) }
        const timingMap = new Map(times(movement).map(time => [time.cellID, time]))
        const overrides = readMovementCellOccupationOverrides(movement.cellOccupationOverridesJson)
        const active = Object.entries(overrides).filter(([, value]) => value.routeID === movement.routeID).map(([id]) => id)
        const ids = new Set([...(routeMap.get(movement.routeID)?.cellIDs || []), ...timingMap.keys(), ...active])
        windows.set(id, new Map([...ids].map(cellID => {
            const shift = getMovementCellOccupationShifts(movement.cellOccupationOverridesJson, movement.routeID, cellID, timingMap.get(cellID) || {})
            return [cellID, { start: movement.startMinutes + shift.startOccupationShift / 60,
                end: movement.endMinutes + shift.endOccupationShift / 60, override: overrides[cellID]?.routeID === movement.routeID ? overrides[cellID] : undefined }]
        })))
        changed.set(id, movement)
        return movement
    }
    const visits = edit.segment.visitIndexes.map(index => train.visits[index]).filter(visit => !!visit)
    if (!visits.length) throw new StationPlanEditingError()
    for (const visit of visits) {
        if (!visit.movementID || !visit.sourceCellID) throw new StationPlanEditingError()
        load(visit.movementID)
    }
    if (edit.mode === 'row') {
        const targetCell = edit.rowKey.slice(5)
        for (const [id, movement] of changed) {
            const sourceCells = [...new Set(visits.filter(visit => visit.movementID === id).map(visit => visit.sourceCellID!))]
            const source = routeMap.get(movement.routeID)
            const oldCell = sourceCells[0]!
            const index = source?.cellIDs.indexOf(oldCell) ?? -1
            const normalizeType = (type: string) => type.trim().toLowerCase().replace(/\s+/g, '')
            const candidates = source && sourceCells.length === 1 ? routes.filter(route => {
                const target = route.cellIDs.indexOf(targetCell)
                return target >= 0 && normalizeType(route.type) === normalizeType(source.type) &&
                    (target === 0) === (index === 0) && (target === route.cellIDs.length - 1) === (index === source.cellIDs.length - 1) &&
                    (index === 0 || route.startNodeID === source.startNodeID) &&
                    (index === source.cellIDs.length - 1 || route.endNodeID === source.endNodeID)
            }).map(route => ({ route, score: route.cellIDs.filter(cellID => cellID !== targetCell && source.cellIDs.includes(cellID)).length }))
                .sort((a, b) => b.score - a.score || a.route.id.localeCompare(b.route.id)) : []
            const best = candidates[0]
            if (!best) continue
            const cellWindows = windows.get(id)!, stop = cellWindows.get(oldCell)!
            movement.routeID = best.route.id
            movement.routeIDList = best.route.id
            const timingMap = new Map(times(movement).map(time => [time.cellID, time]))
            const targetWindows = new Map<string, CellWindow>()
            for (const cellID of new Set([...best.route.cellIDs, ...timingMap.keys()])) {
                const timing = timingMap.get(cellID)
                targetWindows.set(cellID, cellID === targetCell ? { ...stop } : cellWindows.get(cellID) || {
                    start: movement.startMinutes + (timing?.startOccupationShift ?? 0) / 60,
                    end: movement.endMinutes + (timing?.endOccupationShift ?? 0) / 60,
                })
            }
            windows.set(id, targetWindows)
            cellMappings.set(id, new Map([[oldCell, targetCell]]))
        }
    } else {
        const anchors = edit.mode === 'start' ? edit.segment.startAnchors : edit.segment.endAnchors
        const time = edit.mode === 'start' ? edit.startMinutes : edit.endMinutes
        for (const anchor of anchors) load(anchor.movementID)
        for (const anchor of anchors) {
            const movement = load(anchor.movementID)
            if (anchor.kind !== 'cell' || anchor.edge !== 'start') continue
            const cells = routeMap.get(movement.routeID)?.cellIDs || []
            const previousCellID = cells[cells.indexOf(anchor.cellID || '') - 1]
            if (!previousCellID || anchors.some(other => other.movementID === anchor.movementID && other.cellID === previousCellID && other.edge === 'end')) continue
            const cell = windows.get(anchor.movementID)!.get(anchor.cellID!)!
            const previous = windows.get(anchor.movementID)!.get(previousCellID)!
            previous.end += time - cell.start
        }
        for (const anchor of anchors) {
            const movement = load(anchor.movementID)
            if (anchor.kind === 'movement') movement[anchor.edge === 'start' ? 'startMinutes' : 'endMinutes'] = time
            else {
                const cell = windows.get(anchor.movementID)?.get(anchor.cellID || '')
                if (!cell) throw new StationPlanEditingError()
                cell[anchor.edge] = time
                if (cell.override) cell.override = { ...cell.override,
                    ...(anchor.edge === 'start' && cell.override.stationPlanArriveMinutes !== undefined ? { stationPlanArriveMinutes: time } : {}),
                    ...(anchor.edge === 'end' && cell.override.stationPlanDepartMinutes !== undefined ? { stationPlanDepartMinutes: time } : {}),
                }
            }
        }
    }
    // Explicit visit values prevent the diagram builder from correcting reversed or overlapping times.
    visits.forEach((visit, index) => {
        const cellID = cellMappings.get(visit.movementID!)?.get(visit.sourceCellID!) || visit.sourceCellID!
        const cell = windows.get(visit.movementID!)?.get(cellID)
        if (!cell) throw new StationPlanEditingError()
        cell.override = { routeID: changed.get(visit.movementID!)!.routeID, startOccupationShift: 0, endOccupationShift: 0,
            ...cell.override, stationPlanCellID: edit.rowKey.slice(5),
            stationPlanArriveMinutes: index === 0 ? edit.startMinutes : visit.arriveMinutes,
            stationPlanDepartMinutes: index === visits.length - 1 ? edit.endMinutes : visit.departMinutes,
        }
    })
    for (const movement of changed.values()) {
        const retained = edit.mode === 'row' && cellMappings.has(movement.movementID) ? {} : readMovementCellOccupationOverrides(originals.get(movement.movementID)!.cellOccupationOverridesJson)
        movement.cellOccupationOverridesJson = JSON.stringify({ ...retained, ...Object.fromEntries([...windows.get(movement.movementID)!].map(([cellID, window]) => [cellID, {
            ...window.override, routeID: movement.routeID,
            startOccupationShift: Math.round((window.start - movement.startMinutes) * 60),
            endOccupationShift: Math.round((window.end - movement.endMinutes) * 60),
        }])) })
    }
    return [...changed.values()]
}
