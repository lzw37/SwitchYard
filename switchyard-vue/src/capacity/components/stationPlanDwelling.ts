import { isDwellingRoute } from '../simulationDwelling.ts'
import { StationPlanEditingError, type StationPlanEditMovement, type StationPlanEditRoute } from './stationPlanEditing.ts'
import { getStationPlanSegmentEditContext, resolveStationPlanMovementRouteID, stationPlanTrainPath,
    type StationPlanAxisRow, type StationPlanEditableSegment, type StationPlanTrack, type StationPlanTrain } from './stationPlanView.ts'

export interface StationPlanDwellingTarget {
    trainID: string
    movementID: string
    segment: StationPlanEditableSegment
    action: 'passing-to-stop' | 'stop-to-passing'
}

function sourceMovement(train: StationPlanTrain, segment: StationPlanEditableSegment,
    movements: StationPlanEditMovement[], routes: StationPlanEditRoute[]) {
    if (!getStationPlanSegmentEditContext(train, { trainID: train.id, segment, mode: 'start', shiftFollowing: false,
        startMinutes: segment.startMinutes, endMinutes: segment.endMinutes, rowKey: segment.rowKey })) return null
    const matches = movements.filter(movement => movement.trainID === train.id && movement.movementID === segment.movementID)
    if (matches.length !== 1) return null
    const movement = matches[0]!
    if (movement.startMinutes !== segment.startMinutes || movement.endMinutes !== segment.endMinutes) return null
    const routeID = resolveStationPlanMovementRouteID(movement.routeID, movement.routeIDList)
    const route = routes.find(route => route.id === routeID)
    const startNodeID = (route?.startNodeID || movement.startNodeID)?.trim()
    const endNodeID = (route?.endNodeID || movement.endNodeID)?.trim()
    if (!startNodeID || !endNodeID || segment.startRowKey !== `node:${startNodeID}` || segment.endRowKey !== `node:${endNodeID}`) return null
    return { movement, routeID, route }
}

function dwellingTracks(segment: StationPlanEditableSegment, tracks: StationPlanTrack[]) {
    return tracks.filter(track => {
        if (!track.id.trim() || !track.name.trim() || !track.fromNodeID.trim() || !track.toNodeID.trim() ||
            track.fromNodeID.trim() === track.toNodeID.trim()) return false
        const nodeKeys = [`node:${track.fromNodeID.trim()}`, `node:${track.toNodeID.trim()}`]
        return nodeKeys.includes(segment.startRowKey) && nodeKeys.includes(segment.endRowKey)
    })
}

function directTarget(train: StationPlanTrain, segment: StationPlanEditableSegment, movements: StationPlanEditMovement[],
    routes: StationPlanEditRoute[], tracks: StationPlanTrack[], dwellingMovementIDs: string[]): StationPlanDwellingTarget | null {
    const source = sourceMovement(train, segment, movements, routes)
    if (!source || !dwellingTracks(segment, tracks).length) return null
    if (source.routeID ? !source.route || !isDwellingRoute(source.route.type) : !dwellingMovementIDs.includes(segment.movementID)) return null
    return { trainID: train.id, movementID: segment.movementID,
        segment: { ...segment, visitIndexes: [...segment.visitIndexes],
            startAnchors: segment.startAnchors.map(anchor => ({ ...anchor })), endAnchors: segment.endAnchors.map(anchor => ({ ...anchor })) },
        action: source.movement.startMinutes === source.movement.endMinutes ? 'passing-to-stop' : 'stop-to-passing' }
}

/** Find a real Dwelling activity under a handle, including the adjacent arrival/departure handle at the same track and time. */
export function resolveStationPlanDwellingTarget(
    train: StationPlanTrain, segment: StationPlanEditableSegment, edge: 'start' | 'end',
    movements: StationPlanEditMovement[], routes: StationPlanEditRoute[], tracks: StationPlanTrack[], dwellingMovementIDs: string[] = [],
): StationPlanDwellingTarget | null {
    if (edge !== 'start' && edge !== 'end') return null
    const own = directTarget(train, segment, movements, routes, tracks, dwellingMovementIDs)
    if (own) return own
    const source = sourceMovement(train, segment, movements, routes)
    if (!source?.route) return null
    const type = source.route.type.trim().toLowerCase()
    const direction = edge === 'end' && ['arrival', 'arr', '接车', '接车进路'].includes(type) ? 1
        : edge === 'start' && ['departure', 'dep', '发车', '发车进路'].includes(type) ? -1 : 0
    if (!direction) return null
    // Retain hidden or untimed Movement IDs as barriers; never search past the immediate neighbor.
    const order = train.movementOrder || movements.map((movement, index) => ({ movement, index }))
        .filter(({ movement }) => movement.trainID === train.id)
        .sort((a, b) => (a.movement.sortOrder ?? a.index) - (b.movement.sortOrder ?? b.index) || a.index - b.index)
        .map(({ movement }) => movement.movementID)
    if (order.filter(id => id === segment.movementID).length !== 1) return null
    const adjacentID = order[order.indexOf(segment.movementID) + direction]
    if (!adjacentID) return null
    const rows: StationPlanAxisRow[] = [...new Set(train.visits.map(visit => visit.rowKey))].map(key =>
        ({ key, sourceID: key.slice(5), label: key, kind: 'node' }))
    const adjacent = stationPlanTrainPath(train, rows, 0, 1, 1, { x: 0, y: 0 }).editableSegments
        .find(candidate => candidate.movementID === adjacentID)
    if (!adjacent || (direction === 1 ? adjacent.visitIndexes[0] !== segment.visitIndexes[1]! + 1
        : adjacent.visitIndexes[1]! + 1 !== segment.visitIndexes[0])) return null
    const time = edge === 'start' ? segment.startMinutes : segment.endMinutes
    const rowKey = edge === 'start' ? segment.startRowKey : segment.endRowKey
    if ((direction === 1 ? adjacent.startMinutes : adjacent.endMinutes) !== time ||
        !dwellingTracks(adjacent, tracks).some(track => [`node:${track.fromNodeID.trim()}`, `node:${track.toNodeID.trim()}`].includes(rowKey))) return null
    return directTarget(train, adjacent, movements, routes, tracks, dwellingMovementIDs)
}

/** This is a single-activity conversion, independent of dragging, coincident handles and following Movements. */
export function editStationPlanDwelling(
    train: StationPlanTrain, target: StationPlanDwellingTarget, movements: StationPlanEditMovement[],
    routes: StationPlanEditRoute[], tracks: StationPlanTrack[], dwellingMovementIDs: string[] = [],
): StationPlanEditMovement {
    if (target.trainID !== train.id || target.movementID !== target.segment.movementID) throw new StationPlanEditingError()
    const current = directTarget(train, target.segment, movements, routes, tracks, dwellingMovementIDs)
    if (!current || current.action !== target.action) throw new StationPlanEditingError()
    const original = movements.find(movement => movement.trainID === train.id && movement.movementID === target.movementID)!
    const startMinutes = target.action === 'passing-to-stop' ? original.startMinutes : Math.min(original.startMinutes, original.endMinutes)
    const endMinutes = target.action === 'passing-to-stop' ? startMinutes + 2 : startMinutes
    if (!Number.isFinite(startMinutes) || !Number.isFinite(endMinutes)) throw new StationPlanEditingError()
    return { ...original, startMinutes, endMinutes }
}
