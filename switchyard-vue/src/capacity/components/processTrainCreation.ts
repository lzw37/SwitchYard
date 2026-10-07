import { type ProcessActivity, type ProcessCatalog, type ProcessTemplate } from '../operationProcess.ts'

export interface ProcessTrainActivitySelection {
    activityID: string
    routeID: string
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
    processConstraint: { activityMovementMap: Record<string, string>; selectedRouteIDs: Record<string, string> }
    warnings: string[]
}

export interface ProcessTrainActivityRow {
    activity: ProcessActivity
    selection: ProcessTrainActivitySelection
    routeOptions: ProcessCatalog['routes']
    routeLocked: boolean
}

export interface ProcessTrainSelectionIssue {
    activityID: string
    reason: 'unsaved-process' | 'empty-process' | 'unknown-activity' | 'missing-selection' | 'duplicate-selection' |
        'missing-route' | 'invalid-route' | 'fixed-route'
}

/** These are resource choices only. Event locations, anchors and scheduling are checked by backend preview. */
export function processTrainRouteOptions(activity: ProcessActivity, catalog: ProcessCatalog): ProcessCatalog['routes'] {
    const routes = new Map(catalog.routes.map(route => [route.id, route]))
    return [...new Set(activity.routeList)].flatMap(id => {
        const route = routes.get(id)
        return route && route.type === activity.type && (activity.selectedRoute == null || activity.selectedRoute === id) ? [route] : []
    })
}

export function createProcessTrainSelections(source: ProcessTemplate, catalog: ProcessCatalog): ProcessTrainActivitySelection[] {
    return source.activities.map(activity => {
        const selection = { activityID: activity.id, routeID: '' }
        const candidates = processTrainRouteOptions(activity, catalog)
        selection.routeID = activity.selectedRoute ?? (candidates.length === 1 ? candidates[0]!.id : '')
        return selection
    })
}

/** Keep source activity order in the form; the backend determines the resulting Movement order. */
export function processTrainActivityRows(
    source: ProcessTemplate, catalog: ProcessCatalog, selections: ProcessTrainActivitySelection[],
): ProcessTrainActivityRow[] {
    return source.activities.map(activity => {
        const selection = { ...(selections.find(item => item.activityID === activity.id) || { activityID: activity.id, routeID: '' }) }
        return { activity, selection, routeOptions: processTrainRouteOptions(activity, catalog),
            routeLocked: activity.selectedRoute != null }
    })
}

export function updateProcessTrainSelection(
    source: ProcessTemplate, catalog: ProcessCatalog, selections: ProcessTrainActivitySelection[],
    activityID: string, field: 'routeID', value: string,
): ProcessTrainActivitySelection[] {
    const activity = source.activities.find(activity => activity.id === activityID)
    const selection = selections.find(selection => selection.activityID === activityID)
    if (!activity || !selection) return selections
    if (field !== 'routeID' || activity.selectedRoute != null) return selections
    const candidates = processTrainRouteOptions(activity, catalog)
    if (value && !candidates.some(route => route.id === value)) return selections
    const next = { ...selection, routeID: value }
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
        if (activity.selectedRoute != null && selection.routeID !== activity.selectedRoute) add(activity.id, 'fixed-route')
        const route = processTrainRouteOptions(activity, catalog).find(route => route.id === selection.routeID)
        if (!selection.routeID) add(activity.id, 'missing-route')
        else if (!route) add(activity.id, 'invalid-route')

    }
    return issues
}
