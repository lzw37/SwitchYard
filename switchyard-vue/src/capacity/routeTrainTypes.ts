export type RouteTrainType = 'PassengerTrain' | 'FreightTrain' | 'PassengerCar' | 'FreightCar'

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
