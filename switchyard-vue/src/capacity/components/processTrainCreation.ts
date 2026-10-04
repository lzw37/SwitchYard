import type { ProcessActivity, ProcessCatalog, ProcessTemplate } from '../operationProcess.ts'

export interface ProcessTrainActivitySelection {
    activityID: string
    routeID: string
    trackID: string
}

export interface ProcessTrainCreationForm {
    id: string
    trainNumber: string
    name: string
    trainType: string
    originTime: string
    endTime: string
}

export interface ProcessTrainPreview {
    train: { id: string; trainNumber: string; name: string; trainType: string }
    movements: { movementID: string; name: string; route: string; startNodeID?: string; endNodeID?: string;
        earliestStartTime: string; latestEndTime: string; sortOrder: number | null }[]
    processConstraint: { activityMovementMap: Record<string, string>; selectedTrackIDs: Record<string, string> }
    warnings: string[]
}

export interface ProcessTrainActivityRow {
    activity: ProcessActivity
    selection: ProcessTrainActivitySelection
    routeOptions: ProcessCatalog['routes']
    trackOptions: ProcessCatalog['tracks']
    routeLocked: boolean
    trackLocked: boolean
}

export interface ProcessTrainSelectionIssue {
    activityID: string
    reason: 'unsaved-process' | 'empty-process' | 'unknown-activity' | 'missing-selection' | 'duplicate-selection' |
        'missing-route' | 'invalid-route' | 'fixed-route' | 'unexpected-track' | 'missing-track' | 'invalid-track' | 'fixed-track'
}

/** These are resource choices only. Event locations, anchors and scheduling are checked by backend preview. */
export function processTrainTrackOptions(activity: ProcessActivity, catalog: ProcessCatalog): ProcessCatalog['tracks'] {
    if (activity.type !== 'Dwelling') return []
    const tracks = new Map(catalog.tracks.map(track => [track.id, track]))
    return [...new Set(activity.trackList)].flatMap(id => {
        const track = tracks.get(id)
        return track && track.name.trim() && (activity.selectedTrack == null || activity.selectedTrack === id) ? [track] : []
    })
}

export function processTrainRouteOptions(
    activity: ProcessActivity, catalog: ProcessCatalog, trackID = '',
): ProcessCatalog['routes'] {
    if (activity.type === 'Dwelling') {
        const track = processTrainTrackOptions(activity, catalog).find(track => track.id === trackID)
        if (!track) return []
        return catalog.routes.filter(route => route.type === 'Dwelling' && route.trackIDs.length === 1 && route.trackIDs[0] === track.id &&
            Boolean(route.startNodeID && route.endNodeID) && route.startNodeID !== route.endNodeID &&
            ((route.startNodeID === track.fromNodeID && route.endNodeID === track.toNodeID) ||
                (route.startNodeID === track.toNodeID && route.endNodeID === track.fromNodeID)))
    }
    const routes = new Map(catalog.routes.map(route => [route.id, route]))
    return [...new Set(activity.routeList)].flatMap(id => {
        const route = routes.get(id)
        return route && route.type === activity.type && (activity.selectedRoute == null || activity.selectedRoute === id) ? [route] : []
    })
}

export function createProcessTrainSelections(source: ProcessTemplate, catalog: ProcessCatalog): ProcessTrainActivitySelection[] {
    return source.activities.map(activity => {
        const selection = { activityID: activity.id, routeID: '', trackID: '' }
        if (activity.type === 'Dwelling') {
            const candidates = processTrainTrackOptions(activity, catalog)
            selection.trackID = activity.selectedTrack ?? (candidates.length === 1 ? candidates[0]!.id : '')
        } else {
            const candidates = processTrainRouteOptions(activity, catalog)
            selection.routeID = activity.selectedRoute ?? (candidates.length === 1 ? candidates[0]!.id : '')
        }
        return selection
    })
}

/** Keep source activity order in the form; the backend determines the resulting Movement order. */
export function processTrainActivityRows(
    source: ProcessTemplate, catalog: ProcessCatalog, selections: ProcessTrainActivitySelection[],
): ProcessTrainActivityRow[] {
    return source.activities.map(activity => {
        const selection = { ...(selections.find(item => item.activityID === activity.id) || { activityID: activity.id, routeID: '', trackID: '' }) }
        return { activity, selection, routeOptions: processTrainRouteOptions(activity, catalog, selection.trackID),
            trackOptions: processTrainTrackOptions(activity, catalog),
            routeLocked: activity.type === 'Dwelling' ? !selection.trackID : activity.selectedRoute != null,
            trackLocked: activity.type !== 'Dwelling' || activity.selectedTrack != null }
    })
}

export function updateProcessTrainSelection(
    source: ProcessTemplate, catalog: ProcessCatalog, selections: ProcessTrainActivitySelection[],
    activityID: string, field: 'routeID' | 'trackID', value: string,
): ProcessTrainActivitySelection[] {
    const activity = source.activities.find(activity => activity.id === activityID)
    const selection = selections.find(selection => selection.activityID === activityID)
    if (!activity || !selection) return selections
    const next = { ...selection }
    if (activity.type === 'Dwelling') {
        if (field === 'trackID') {
            if (activity.selectedTrack != null || (value && !processTrainTrackOptions(activity, catalog).some(track => track.id === value))) return selections
            next.trackID = value
            if (!processTrainRouteOptions(activity, catalog, value).some(route => route.id === next.routeID)) next.routeID = ''
        } else {
            if (value && !processTrainRouteOptions(activity, catalog, selection.trackID).some(route => route.id === value)) return selections
            next.routeID = value
        }
    } else {
        if (field !== 'routeID' || activity.selectedRoute != null || (value && !processTrainRouteOptions(activity, catalog).some(route => route.id === value))) return selections
        next.routeID = value
        next.trackID = ''
    }
    return selections.map(item => item.activityID === activityID ? { ...next } : item)
}

/** Validate the request's shape and local candidates, without claiming schedule feasibility. */
export function validateProcessTrainSelections(
    source: ProcessTemplate, catalog: ProcessCatalog, selections: ProcessTrainActivitySelection[],
): ProcessTrainSelectionIssue[] {
    const issues: ProcessTrainSelectionIssue[] = []
    const add = (activityID: string, reason: ProcessTrainSelectionIssue['reason']) => issues.push({ activityID, reason })
    if (!source.id.trim() || !Number.isSafeInteger(source.revision) || source.revision <= 0) add('', 'unsaved-process')
    if (!source.activities.length) add('', 'empty-process')
    const activityIDs = new Set(source.activities.map(activity => activity.id))
    for (const selection of selections) if (!activityIDs.has(selection.activityID)) add(selection.activityID, 'unknown-activity')
    for (const activity of source.activities) {
        const matches = selections.filter(selection => selection.activityID === activity.id)
        if (!matches.length) { add(activity.id, 'missing-selection'); continue }
        if (matches.length !== 1) { add(activity.id, 'duplicate-selection'); continue }
        const selection = matches[0]!
        if (activity.type === 'Dwelling') {
            if (activity.selectedTrack != null && selection.trackID !== activity.selectedTrack) add(activity.id, 'fixed-track')
            if (!selection.trackID) add(activity.id, 'missing-track')
            else if (!processTrainTrackOptions(activity, catalog).some(track => track.id === selection.trackID)) add(activity.id, 'invalid-track')
            if (selection.routeID && !processTrainRouteOptions(activity, catalog, selection.trackID).some(route => route.id === selection.routeID)) add(activity.id, 'invalid-route')
        } else {
            if (activity.selectedRoute != null && selection.routeID !== activity.selectedRoute) add(activity.id, 'fixed-route')
            if (!selection.routeID) add(activity.id, 'missing-route')
            else if (!processTrainRouteOptions(activity, catalog).some(route => route.id === selection.routeID)) add(activity.id, 'invalid-route')
            if (selection.trackID) add(activity.id, 'unexpected-track')
        }
    }
    return issues
}
