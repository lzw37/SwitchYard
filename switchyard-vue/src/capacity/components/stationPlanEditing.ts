import { getStationPlanSegmentEditContext, orderStationPlanMovements, resolveStationPlanMovementRouteID, stationPlanTrackVisitIndexes,
    type StationPlanMovement, type StationPlanRoute, type StationPlanSegmentEdit, type StationPlanTrack, type StationPlanTrackEdit,
    type StationPlanTimeAnchor, type StationPlanTrain, type StationPlanVisit } from './stationPlanView.ts'

export interface StationPlanEditRoute extends StationPlanRoute { type: string }
export interface StationPlanEditMovement extends StationPlanMovement { minDuration?: number | null }
export interface StationPlanAlignmentMovement {
    trainID: string
    movementID: string
    sortOrder: number | null
    startMinutes: number | null
    endMinutes: number | null
}
export class StationPlanEditingError extends Error {
    reason = 'stale'
    constructor() { super('stale') }
}

export class StationPlanTrackEditingError extends Error {
    reason: 'missing-route' | 'stale'
    movementNames: string[]
    targetTrackName: string
    constructor(reason: 'missing-route' | 'stale', movementNames: string[], targetTrackName: string) {
        super(reason)
        this.name = 'StationPlanTrackEditingError'
        this.reason = reason
        this.movementNames = movementNames
        this.targetTrackName = targetTrackName
    }
}

/** Reassign the arrival, stay and departure routes for one continuous visit to a named track. */
export function editStationPlanTrack(
    train: StationPlanTrain, edit: StationPlanTrackEdit, movements: StationPlanEditMovement[],
    routes: StationPlanEditRoute[], tracks: StationPlanTrack[],
): StationPlanEditMovement[] {
    const sourceTrack = tracks.find(track => track.id === edit.sourceTrackID)
    const targetTrack = tracks.find(track => track.id === edit.targetTrackID)
    const targetName = targetTrack?.name || edit.targetTrackID
    const sourceName = movements.find(movement => movement.trainID === train.id && movement.movementID === edit.segment.movementID)?.name || edit.segment.movementID
    function fail(reason: 'missing-route' | 'stale', names = [sourceName]): never {
        throw new StationPlanTrackEditingError(reason, names, targetName)
    }
    if (train.id !== edit.trainID || !sourceTrack || !targetTrack || !targetTrack.id.trim() || !targetTrack.name.trim() ||
        !targetTrack.fromNodeID.trim() || !targetTrack.toNodeID.trim() || targetTrack.fromNodeID.trim() === targetTrack.toNodeID.trim()) fail('stale')
    const visitIndexes = stationPlanTrackVisitIndexes(train, edit.segment, sourceTrack)
    if (!visitIndexes.length) fail('stale')
    if (sourceTrack.id === targetTrack.id) return []
    const sourceNodes = new Set([sourceTrack.fromNodeID.trim(), sourceTrack.toNodeID.trim()])
    const targetNodes = new Set([targetTrack.fromNodeID.trim(), targetTrack.toNodeID.trim()])
    const affected = new Map<string, Set<'start' | 'end'>>()
    for (const index of visitIndexes) {
        const visit = train.visits[index]!, anchor = visit.arriveAnchors?.[0]
        if (!anchor || anchor.kind !== 'movement' || visit.movementID !== anchor.movementID || (anchor.edge !== 'start' && anchor.edge !== 'end')) fail('stale')
        const edges = affected.get(anchor.movementID) || new Set<'start' | 'end'>()
        edges.add(anchor.edge)
        affected.set(anchor.movementID, edges)
    }
    const scoped = movements.filter(movement => movement.trainID === train.id)
    const ordered = orderStationPlanMovements(movements, train.id).filter(movement => affected.has(movement.movementID))
    if (ordered.length !== affected.size) fail('stale')
    const routesByID = new Map(routes.map(route => [route.id, route]))
    const visitsByMovement = new Map<string, { visit: StationPlanVisit; index: number }[]>()
    train.visits.forEach((visit, index) => {
        if (!visit.movementID) return
        const entries = visitsByMovement.get(visit.movementID) || []
        entries.push({ visit, index })
        visitsByMovement.set(visit.movementID, entries)
    })
    const normalizeType = (type: string) => type.trim().toLowerCase().replace(/\s+/g, '')
    const plans: StationPlanEditMovement[] = []
    const missingNames: string[] = []
    for (const movement of ordered) {
        const name = movement.name || movement.movementID
        const entries = visitsByMovement.get(movement.movementID) || []
        if (scoped.filter(candidate => candidate.movementID === movement.movementID).length !== 1 || entries.length !== 2 ||
            entries[0]!.index % 2 !== 0 || entries[1]!.index !== entries[0]!.index + 1) fail('stale', [name])
        const routeID = resolveStationPlanMovementRouteID(movement.routeID, movement.routeIDList)
        const sourceRoute = routesByID.get(routeID)
        for (const [index, edge] of (['start', 'end'] as const).entries()) {
            const visit = entries[index]!.visit
            const matches = (anchor: StationPlanTimeAnchor | undefined) =>
                anchor?.kind === 'movement' && anchor.movementID === movement.movementID && anchor.edge === edge
            const expectedTime = edge === 'start' ? movement.startMinutes : movement.endMinutes
            const expectedNode = (edge === 'start' ? sourceRoute?.startNodeID || movement.startNodeID : sourceRoute?.endNodeID || movement.endNodeID)?.trim()
            if (visit.arriveAnchors?.length !== 1 || !matches(visit.arriveAnchors[0]) || visit.departAnchors?.length !== 1 ||
                !matches(visit.departAnchors[0]) || visit.arriveMinutes !== expectedTime || visit.departMinutes !== expectedTime ||
                !visit.rowKey.startsWith('node:') || (expectedNode && visit.rowKey !== `node:${expectedNode}`)) fail('stale', [name])
        }
        const startNodeID = entries[0]!.visit.rowKey.slice(5), endNodeID = entries[1]!.visit.rowKey.slice(5)
        const edges = affected.get(movement.movementID)!
        const both = edges.has('start') && edges.has('end')
        const routeLessDwelling = !routeID && both && sourceNodes.has(startNodeID) && sourceNodes.has(endNodeID)
        if (!sourceRoute && !routeLessDwelling) { missingNames.push(name); continue }
        const type = sourceRoute ? normalizeType(sourceRoute.type) : 'dwelling'
        const sourceCells = new Set(sourceRoute?.cellIDs || [])
        const candidates = routes.filter(route => {
            if (!route.id.trim() || normalizeType(route.type) !== type) return false
            const start = route.startNodeID.trim(), end = route.endNodeID.trim()
            if ((edges.has('start') ? !targetNodes.has(start) : start !== startNodeID) ||
                (edges.has('end') ? !targetNodes.has(end) : end !== endNodeID) || (both && start === end)) return false
            return type !== 'dwelling' || route.linkIDs === undefined ||
                (route.linkIDs.length === 1 && route.linkIDs[0]?.trim() === targetTrack.id)
        }).map(route => ({ route, sharedCells: [...new Set(route.cellIDs)].filter(id => sourceCells.has(id)).length }))
            .sort((a, b) => b.sharedCells - a.sharedCells || (a.route.id < b.route.id ? -1 : a.route.id > b.route.id ? 1 : 0))
        const chosen = candidates[0]?.route
        if (!chosen) { missingNames.push(name); continue }
        plans.push({ ...movement, routeID: chosen.id, routeIDList: chosen.id, cellOccupations: [], cellOccupationOverridesJson: '{}' })
    }
    if (missingNames.length) fail('missing-route', missingNames)
    return plans
}

/** Align only this endpoint to the true adjacent movement, including movements omitted from the diagram. */
export function alignStationPlanSegmentToAdjacent(
    train: StationPlanTrain, edit: StationPlanSegmentEdit, movements: StationPlanAlignmentMovement[],
): StationPlanSegmentEdit | null {
    const context = getStationPlanSegmentEditContext(train, { ...edit, shiftFollowing: false })
    if (!context) return null
    // Preserve the normal movement ordering, but retain missing timestamps so an invalid neighbor is never skipped.
    const ordered = movements.map((movement, index) => ({ movement, index }))
        .filter(({ movement }) => movement.trainID === train.id)
        .sort((a, b) => (a.movement.sortOrder ?? a.index) - (b.movement.sortOrder ?? b.index) || a.index - b.index)
        .map(item => item.movement)
    const selectedIndex = ordered.findIndex(movement => movement.movementID === context.movementID)
    const original = ordered[selectedIndex]
    if (!original || original.startMinutes !== edit.segment.startMinutes || original.endMinutes !== edit.segment.endMinutes) return null
    const neighbor = ordered[selectedIndex + (edit.mode === 'start' ? -1 : 1)]
    const referenceMinutes = edit.mode === 'start' ? neighbor?.endMinutes : neighbor?.startMinutes
    const currentMinutes = edit.mode === 'start' ? original.startMinutes : original.endMinutes
    if (typeof referenceMinutes !== 'number' || !Number.isFinite(referenceMinutes) || referenceMinutes === currentMinutes) return null
    return { ...edit, shiftFollowing: false, coincidentAnchors: undefined,
        startMinutes: edit.mode === 'start' ? referenceMinutes : edit.segment.startMinutes,
        endMinutes: edit.mode === 'end' ? referenceMinutes : edit.segment.endMinutes }
}

/** Shift movement timestamps in operation order. The backend owns all Cell occupation calculations. */
export function editStationPlanSegment(
    train: StationPlanTrain, edit: StationPlanSegmentEdit, movements: StationPlanEditMovement[], _routes: StationPlanEditRoute[],
): StationPlanEditMovement[] {
    const context = getStationPlanSegmentEditContext(train, edit)
    if (!context) throw new StationPlanEditingError()
    const ordered = orderStationPlanMovements(movements, train.id)
    const selectedIndex = ordered.findIndex(movement => movement.movementID === context.movementID)
    const original = ordered[selectedIndex]
    if (!original || original.startMinutes !== edit.segment.startMinutes || original.endMinutes !== edit.segment.endMinutes)
        throw new StationPlanEditingError()
    const originalsByID = new Map(ordered.map(movement => [movement.movementID, movement]))
    for (const anchor of context.coincidentAnchors) {
        const source = originalsByID.get(anchor.movementID)
        const time = anchor.edge === 'start' ? source?.startMinutes : source?.endMinutes
        if (time !== context.originalMinutes) throw new StationPlanEditingError()
    }
    return ordered.flatMap((movement, index) => {
        const startMinutes = context.shiftTime(movement.startMinutes, 'start', index - selectedIndex, movement.movementID)
        const endMinutes = context.shiftTime(movement.endMinutes, 'end', index - selectedIndex, movement.movementID)
        if (!Number.isFinite(startMinutes) || !Number.isFinite(endMinutes)) throw new StationPlanEditingError()
        return startMinutes === movement.startMinutes && endMinutes === movement.endMinutes
            ? [] : [{ ...movement, startMinutes, endMinutes }]
    })
}
