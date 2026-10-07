import { getStationPlanSegmentEditContext, orderStationPlanMovements, resolveStationPlanMovementRouteID, stationPlanTrackVisitIndexes,
    type StationPlanMovement, type StationPlanRoute, type StationPlanSegmentEdit, type StationPlanTrack, type StationPlanTrackEdit,
    type StationPlanTimeAnchor, type StationPlanTrain, type StationPlanVisit } from './stationPlanView.ts'

export interface StationPlanEditRoute extends StationPlanRoute { type: string }
export interface StationPlanEditMovement extends StationPlanMovement { minDuration?: number | null }
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

/** Resolve the dwelling's own anchors without merging other alternative anchor groups transitively. */
function dwellingAnchorEdges(train: StationPlanTrain, edit: StationPlanTrackEdit, movements: StationPlanEditMovement[],
    routes: StationPlanEditRoute[], sourceTrack: StationPlanTrack): Map<string, Set<'start' | 'end'>> | null {
    const snapshot = train.processConstraints, process = snapshot?.process
    if (!snapshot || !process?.events || !process.anchors) return null
    if (snapshot.trainID !== train.id) throw new StationPlanEditingError()
    const selected = process.activities.find(activity => snapshot.activityMovementMap[activity.id] === edit.segment.movementID)
    if (!selected || selected.type?.toLowerCase() !== 'dwelling' || edit.segment.isConnector) return null
    const events = new Map(process.events.map(event => [event.id, event]))
    const byMovement = new Map(movements.filter(movement => movement.trainID === train.id).map(movement => [movement.movementID, movement]))
    const byRoute = new Map(routes.map(route => [route.id, route]))
    const bindings = new Map(process.routeAnchors?.map(binding => [binding.routeID, binding]))
    function anchorsAt(activity: { id: string; startEvent: string; endEvent: string }, edge: 'start' | 'end') {
        const event = events.get(edge === 'start' ? activity.startEvent : activity.endEvent)
        const movement = byMovement.get(snapshot!.activityMovementMap[activity.id] || '')
        const routeID = movement && resolveStationPlanMovementRouteID(movement.routeID, movement.routeIDList)
        const binding = routeID ? bindings.get(routeID) : undefined
        const bound = edge === 'start' ? binding?.startAnchor : binding?.endAnchor
        return bound ? [bound] : event?.selectedAnchor ? [event.selectedAnchor] : event?.anchorList || []
    }
    const owned = process.anchors.filter(anchor => anchor.dwellingActivityID === selected.id).map(anchor => anchor.id)
    const anchorIDs = new Set(owned.length ? owned : [...anchorsAt(selected, 'start'), ...anchorsAt(selected, 'end')])
    const eventIDs = new Set([selected.startEvent, selected.endEvent])
    const sourceNodes = new Set([sourceTrack.fromNodeID.trim(), sourceTrack.toNodeID.trim()])
    for (const activity of process.activities) {
        const movement = byMovement.get(snapshot.activityMovementMap[activity.id] || '')
        const route = movement && byRoute.get(resolveStationPlanMovementRouteID(movement.routeID, movement.routeIDList))
        for (const edge of ['start', 'end'] as const) {
            const ids = anchorsAt(activity, edge)
            if (!ids.some(id => anchorIDs.has(id))) continue
            const node = (edge === 'start' ? route?.startNodeID || movement?.startNodeID : route?.endNodeID || movement?.endNodeID)?.trim()
            // With alternative anchors, the current track identifies which group this endpoint uses.
            if (ids.length > 1 && node && !sourceNodes.has(node)) continue
            eventIDs.add(edge === 'start' ? activity.startEvent : activity.endEvent)
        }
    }
    const affected = new Map<string, Set<'start' | 'end'>>()
    for (const activity of process.activities) {
        const edges = new Set<'start' | 'end'>()
        if (eventIDs.has(activity.startEvent)) edges.add('start')
        if (eventIDs.has(activity.endEvent)) edges.add('end')
        if (!edges.size) continue
        const movementID = snapshot.activityMovementMap[activity.id]
        if (!movementID) throw new StationPlanEditingError()
        if (activity.type?.toLowerCase() === 'dwelling') { edges.add('start'); edges.add('end') }
        affected.set(movementID, edges)
    }
    return affected
}

/** Reassign every route attached to the dwelling's anchors; legacy plans use their continuous visit. */
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
    const anchored = dwellingAnchorEdges(train, edit, movements, routes, sourceTrack)
    const affected = anchored || new Map<string, Set<'start' | 'end'>>()
    for (const index of anchored ? [] : visitIndexes) {
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
        if (scoped.filter(candidate => candidate.movementID === movement.movementID).length !== 1 ||
            (entries.length === 0 ? !anchored : entries.length !== 2 ||
                entries[0]!.index % 2 !== 0 || entries[1]!.index !== entries[0]!.index + 1)) fail('stale', [name])
        const routeID = resolveStationPlanMovementRouteID(movement.routeID, movement.routeIDList)
        const sourceRoute = routesByID.get(routeID)
        for (const [index, edge] of (entries.length ? ['start', 'end'] as const : []).entries()) {
            const visit = entries[index]!.visit
            const matches = (anchor: StationPlanTimeAnchor | undefined) =>
                anchor?.kind === 'movement' && anchor.movementID === movement.movementID && anchor.edge === edge
            const expectedTime = edge === 'start' ? movement.startMinutes : movement.endMinutes
            const expectedNode = (edge === 'start' ? sourceRoute?.startNodeID || movement.startNodeID : sourceRoute?.endNodeID || movement.endNodeID)?.trim()
            if (visit.arriveAnchors?.length !== 1 || !matches(visit.arriveAnchors[0]) || visit.departAnchors?.length !== 1 ||
                !matches(visit.departAnchors[0]) || visit.arriveMinutes !== expectedTime || visit.departMinutes !== expectedTime ||
                !visit.rowKey.startsWith('node:') || (expectedNode && visit.rowKey !== `node:${expectedNode}`)) fail('stale', [name])
        }
        const startNodeID = sourceRoute?.startNodeID.trim() || movement.startNodeID?.trim() || entries[0]?.visit.rowKey.slice(5) || ''
        const endNodeID = sourceRoute?.endNodeID.trim() || movement.endNodeID?.trim() || entries[1]?.visit.rowKey.slice(5) || ''
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
        plans.push({ ...movement, routeID: chosen.id, routeIDList: chosen.id,
            startNodeID: chosen.startNodeID, endNodeID: chosen.endNodeID, cellOccupations: [], cellOccupationOverridesJson: '{}' })
    }
    if (missingNames.length) fail('missing-route', missingNames)
    return plans
}

/** Preview the same route choices that will be saved, including both ends of each anchor-linked route. */
export function previewStationPlanTrackEdit(train: StationPlanTrain, edit: StationPlanTrackEdit,
    movements: StationPlanEditMovement[], routes: StationPlanEditRoute[], tracks: StationPlanTrack[]): StationPlanTrain {
    const changed = new Map(editStationPlanTrack(train, edit, movements, routes, tracks).map(movement => [movement.movementID, movement]))
    return { ...train, visits: train.visits.map(visit => {
        const ref = visit.arriveAnchors?.[0], movement = ref && changed.get(ref.movementID)
        if (!movement || !ref) return visit
        return { ...visit, rowKey: `node:${ref.edge === 'start' ? movement.startNodeID : movement.endNodeID}` }
    }) }
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
