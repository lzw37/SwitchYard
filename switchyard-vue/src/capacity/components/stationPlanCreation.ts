import type { StationPlanEditRoute } from './stationPlanEditing.ts'

export interface StationPlanDraftPoint {
    nodeID: string
    timeMinutes: number
}

export interface StationPlanDraftMovement {
    id: string
    name: string
    start: StationPlanDraftPoint | null
    end: StationPlanDraftPoint | null
    routeID: string
}

export interface StationPlanDraftTrain {
    id: string
    trainNumber: string
    name: string
    trainType: string
}

export const STATION_PLAN_DRAFT_MOVEMENT_LIMIT = 1000

let draftIDSequence = 0
function newDraftID() {
    return globalThis.crypto?.randomUUID?.() || `draft-${Date.now()}-${++draftIDSequence}`
}

/** Match the selected direction exactly; route order from the server does not choose the default. */
export function matchingStationPlanDraftRoutes(row: StationPlanDraftMovement, routes: StationPlanEditRoute[]): StationPlanEditRoute[] {
    if (!row.start?.nodeID || !row.end?.nodeID) return []
    return routes.filter(route => route.id && route.startNodeID === row.start!.nodeID && route.endNodeID === row.end!.nodeID)
        .sort((a, b) => a.id < b.id ? -1 : a.id > b.id ? 1 : 0)
}

export function rematchStationPlanDraftMovement(row: StationPlanDraftMovement, routes: StationPlanEditRoute[]): StationPlanDraftMovement {
    return { ...row, start: row.start ? { ...row.start } : null, end: row.end ? { ...row.end } : null,
        routeID: matchingStationPlanDraftRoutes(row, routes)[0]?.id || '' }
}

/** Two clicks form one Movement. An unfinished last pair remains editable in the draft. */
export function createStationPlanDraftMovements(
    points: StationPlanDraftPoint[], routes: StationPlanEditRoute[], makeID: () => string = newDraftID,
): StationPlanDraftMovement[] {
    const result: StationPlanDraftMovement[] = []
    for (let index = 0; index < points.length; index += 2) {
        result.push(rematchStationPlanDraftMovement({ id: makeID(), name: '', start: points[index]!, end: points[index + 1] || null, routeID: '' }, routes))
    }
    return result
}

/** A manually selected route supplies its nodes, while the user's times stay unchanged. */
export function selectStationPlanDraftRoute(
    row: StationPlanDraftMovement, routeID: string, routes: StationPlanEditRoute[],
): StationPlanDraftMovement {
    const route = routes.find(candidate => candidate.id === routeID)
    if (!routeID || !route) return { ...row, routeID: '' }
    return { ...row, routeID: route.id,
        start: { nodeID: route.startNodeID, timeMinutes: row.start?.timeMinutes ?? NaN },
        end: { nodeID: route.endNodeID, timeMinutes: row.end?.timeMinutes ?? NaN } }
}

/** Accept the chart's D±n HH:mm[:ss.fraction] labels and extended hours such as 25:10. */
export function parseStationPlanDraftTime(value: string): number | null {
    const match = value.trim().match(/^(?:D([+-]\d+)\s+)?(\d+):(\d{1,2})(?::(\d{1,2}(?:\.\d+)?))?$/i)
    if (!match) return null
    const day = Number(match[1] || 0), hour = Number(match[2]), minute = Number(match[3]), second = Number(match[4] || 0)
    if (!Number.isFinite(day) || !Number.isFinite(hour) || minute >= 60 || second >= 60) return null
    const result = day * 1440 + hour * 60 + minute + second / 60
    return Number.isFinite(result) ? result : null
}

export function canConfirmStationPlanDraft(movements: StationPlanDraftMovement[], trainNumber: string): boolean {
    return Boolean(trainNumber.trim()) && movements.length > 0 && movements.length <= STATION_PLAN_DRAFT_MOVEMENT_LIMIT && movements.every(row =>
        [row.start, row.end].every(point => point !== null && Boolean(point.nodeID.trim()) && Number.isFinite(point.timeMinutes)))
}
