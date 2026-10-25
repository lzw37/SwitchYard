export type RouteTrainType = 'PassengerTrain' | 'FreightTrain' | 'PassengerCar' | 'FreightCar'

export interface RouteTrainTypeTrack {
    id: string
    name: string
    arrowType: string
    fromNodeID: string
    toNodeID: string
}

export function allowedRouteTrainTypes(routeType: string): RouteTrainType[] {
    const type = routeType.replace(/\s+/g, '').toLowerCase()
    return ['arrival', 'arr', '接车', '接车进路', 'departure', 'dep', '发车', '发车进路'].includes(type)
        ? ['PassengerTrain', 'FreightTrain']
        : ['PassengerCar', 'FreightCar']
}

export function readRouteTrainTypes(value: unknown, routeType: string): RouteTrainType[] {
    const allowed = allowedRouteTrainTypes(routeType)
    if (value == null || value === '') return allowed
    try {
        const selected = typeof value === 'string' ? JSON.parse(value) : value
        return Array.isArray(selected) ? allowed.filter(type => selected.includes(type)) : []
    } catch { return [] }
}

export function changeRouteTrainTypes(selected: RouteTrainType[], previousType: string, nextType: string): RouteTrainType[] {
    const allowed = allowedRouteTrainTypes(nextType)
    // Changing within the same category keeps even an explicitly empty selection.
    return allowed.join() === allowedRouteTrainTypes(previousType).join()
        ? selected.filter(type => allowed.includes(type))
        : allowed
}

function trackTrainTypes(track: RouteTrainTypeTrack): RouteTrainType[] | null {
    switch (track.arrowType.trim().toUpperCase()) {
        case 'P': return ['PassengerTrain']
        case 'F':
        case 'OF': return ['FreightTrain']
        case 'PF': return ['PassengerTrain', 'FreightTrain']
        default: return null
    }
}

/** Return null when the layout cannot identify the route's train categories. */
export function inferRouteTrainTypes(
    routeType: string,
    linkIds: readonly string[],
    startNodeID: string,
    tracks: readonly RouteTrainTypeTrack[],
): RouteTrainType[] | null {
    const type = routeType.replace(/\s+/g, '').toLowerCase()
    const routeLinks = new Set(linkIds.map(id => id.trim()).filter(Boolean))
    if (!routeLinks.size) return null

    if (['arrival', 'arr', '接车', '接车进路'].includes(type)) {
        const categories = tracks
            .filter(track => track.name.trim() && routeLinks.has(track.id))
            .map(trackTrainTypes)
            .filter((types): types is RouteTrainType[] => types !== null)
        if (!categories.length) return null
        // A train must be permitted on every classified named track in its path.
        return allowedRouteTrainTypes(routeType).filter(trainType => categories.every(types => types.includes(trainType)))
    }

    if (['departure', 'dep', '发车', '发车进路'].includes(type)) {
        const start = startNodeID.trim()
        const atStart = (track: RouteTrainTypeTrack) => track.fromNodeID === start || track.toNodeID === start
        if (!start || !tracks.some(track => routeLinks.has(track.id) && atStart(track))) return null
        // The departure track precedes the route's first Link, across its start node.
        const adjacent = tracks.filter(track => track.name.trim() && !routeLinks.has(track.id) && atStart(track))
        return adjacent.length === 1 ? trackTrainTypes(adjacent[0]!) : null
    }

    return null
}
