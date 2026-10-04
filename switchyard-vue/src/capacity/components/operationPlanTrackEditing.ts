import { editStationPlanTrack, type StationPlanEditMovement, type StationPlanEditRoute } from './stationPlanEditing.ts'
import { resolveStationPlanMovementRouteID, stationPlanTrackVisitIndexes, stationPlanTrainPath,
    type StationPlanAxisRow, type StationPlanEditableSegment, type StationPlanTrack, type StationPlanTrackEdit,
    type StationPlanTrain } from './stationPlanView.ts'

export interface OperationPlanTrackCell {
    id: string
    linkIDs?: string[]
}

export interface OperationPlanTrackSource {
    trainID: string
    movementID: string
    sourceCellID: string
    sourceTrackID: string
    routeID: string
    /** Explicit process Dwelling identity, needed when the Movement has no real route. */
    dwellingTrackID?: string
    segment: StationPlanEditableSegment
}

export interface OperationPlanTrackEditPlan {
    edit: StationPlanTrackEdit
    changed: StationPlanEditMovement[]
    targetTrackID: string
    targetCellID: string
}

export class OperationPlanTrackEditingError extends Error {
    reason: 'unmapped-cell' | 'ambiguous-cell' | 'stale'
    cellID: string
    constructor(reason: OperationPlanTrackEditingError['reason'], cellID: string) {
        super(reason)
        this.name = 'OperationPlanTrackEditingError'
        this.reason = reason
        this.cellID = cellID
    }
}

/** Physical Cell-to-Link references determine the track; labels and Cell occupation times do not. */
export function operationPlanCellTracks(
    cellID: string, cells: OperationPlanTrackCell[], tracks: StationPlanTrack[],
): StationPlanTrack[] {
    const linkIDs = new Set(cells.filter(cell => cell.id === cellID).flatMap(cell => cell.linkIDs || []).map(id => id.trim()).filter(Boolean))
    const seen = new Set<string>()
    return tracks.filter(track => {
        if (!linkIDs.has(track.id) || seen.has(track.id) || !track.id.trim() || !track.name.trim() ||
            !track.fromNodeID.trim() || !track.toNodeID.trim() || track.fromNodeID.trim() === track.toNodeID.trim()) return false
        seen.add(track.id)
        return true
    })
}

/** Capture a real Dwelling Movement's original endpoints before dragging its Gantt block. */
export function resolveOperationPlanTrackSource(
    train: StationPlanTrain, movementID: string, cellID: string, movements: StationPlanEditMovement[],
    routes: StationPlanEditRoute[], tracks: StationPlanTrack[], cells: OperationPlanTrackCell[], dwellingTrackID?: string,
): OperationPlanTrackSource | null {
    const matching = movements.filter(movement => movement.trainID === train.id && movement.movementID === movementID)
    if (matching.length !== 1) return null
    const movement = matching[0]!
    const routeID = resolveStationPlanMovementRouteID(movement.routeID, movement.routeIDList)
    const route = routes.find(route => route.id === routeID)
    if (routeID ? !route || route.type.trim().toLowerCase().replace(/\s+/g, '') !== 'dwelling' : !dwellingTrackID) return null
    if (route?.linkIDs !== undefined && route.linkIDs.length !== 1) return null
    if (route && route.startNodeID.trim() === route.endNodeID.trim()) return null

    const rows: StationPlanAxisRow[] = [...new Set(train.visits.map(visit => visit.rowKey))].map(key =>
        ({ key, sourceID: key.slice(5), label: key, kind: 'node' }))
    // Geometry supplies the same validated Movement anchors as the mesoscopic chart, using Movement times only.
    const segments = stationPlanTrainPath(train, rows, 0, 1, 1, { x: 0, y: 0 }).editableSegments.filter(segment => segment.movementID === movementID)
    if (segments.length !== 1) return null
    const segment = segments[0]!
    if (segment.startMinutes !== movement.startMinutes || segment.endMinutes !== movement.endMinutes) return null
    const startNodeID = (route?.startNodeID || movement.startNodeID)?.trim()
    const endNodeID = (route?.endNodeID || movement.endNodeID)?.trim()
    if ((startNodeID && segment.startRowKey !== `node:${startNodeID}`) || (endNodeID && segment.endRowKey !== `node:${endNodeID}`)) return null

    const candidates = operationPlanCellTracks(cellID, cells, tracks).filter(track => {
        if (route?.linkIDs && route.linkIDs[0]?.trim() !== track.id) return false
        if (!route && dwellingTrackID !== track.id) return false
        return stationPlanTrackVisitIndexes(train, segment, track).length > 0
    })
    if (candidates.length !== 1) return null
    return { trainID: train.id, movementID, sourceCellID: cellID, sourceTrackID: candidates[0]!.id, routeID,
        ...(route ? {} : { dwellingTrackID }), segment }
}

/** Plan the entire related arrival/stay/departure change; the caller persists this as one atomic action. */
export function planOperationPlanTrackEdit(
    train: StationPlanTrain, source: OperationPlanTrackSource, targetCellID: string, movements: StationPlanEditMovement[],
    routes: StationPlanEditRoute[], tracks: StationPlanTrack[], cells: OperationPlanTrackCell[],
): OperationPlanTrackEditPlan {
    const current = train.id === source.trainID ? resolveOperationPlanTrackSource(train, source.movementID, source.sourceCellID,
        movements, routes, tracks, cells, source.dwellingTrackID) : null
    if (!current || current.sourceTrackID !== source.sourceTrackID || current.routeID !== source.routeID)
        throw new OperationPlanTrackEditingError('stale', source.sourceCellID)
    const targets = operationPlanCellTracks(targetCellID, cells, tracks)
    if (targets.length !== 1) throw new OperationPlanTrackEditingError(targets.length ? 'ambiguous-cell' : 'unmapped-cell', targetCellID)
    const edit: StationPlanTrackEdit = { trainID: source.trainID, segment: source.segment,
        sourceTrackID: source.sourceTrackID, targetTrackID: targets[0]!.id }
    // Reuse the captured segment so changing the source timestamp or endpoint during the drag is stale.
    const changed = editStationPlanTrack(train, edit, movements, routes, tracks)
    return { edit, changed, targetTrackID: targets[0]!.id, targetCellID }
}
