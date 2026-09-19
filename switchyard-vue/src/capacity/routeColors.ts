export const routeHighlightColors = {
    arrival: '#ef4444',
    departure: '#2563eb',
    locomotive: '#16a34a',
    shunting: '#facc15',
} as const

export function getStationRouteHighlightColor(type: string): string {
    const normalizedType = String(type || '').trim().replace(/\s+/g, '').toLowerCase()
    if (['arrival', 'arr', '接车', '接车进路'].includes(normalizedType)) {
        return routeHighlightColors.arrival
    }
    if (['departure', 'dep', '发车', '发车进路'].includes(normalizedType)) {
        return routeHighlightColors.departure
    }
    if (['locomotive', '机车出入段', '机车出入段进路', '机车走行'].includes(normalizedType)) {
        return routeHighlightColors.locomotive
    }
    return routeHighlightColors.shunting
}
