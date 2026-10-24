import type { TractionRouteSegment } from './tractionCalculation'
export interface SpeedLimitPoint { positionM: number; speedLimitKmh: number }
export const maximumSpeedLimitPoints = 1000
export function pointsFromSegments(segments: readonly TractionRouteSegment[]): SpeedLimitPoint[] {
    const points: SpeedLimitPoint[] = []
    for (const segment of segments) {
        if (points[points.length - 1]?.speedLimitKmh !== segment.speedLimitKmh) points.push({ positionM: segment.startM, speedLimitKmh: segment.speedLimitKmh })
    }
    return points
}
export function validateSpeedLimitPoints(points: readonly SpeedLimitPoint[]): boolean {
    return points.length <= maximumSpeedLimitPoints && points.every((point, index) => point != null && Number.isFinite(point.positionM)
        && point.positionM >= 0 && point.positionM <= 10_000_000 && (index === 0 ? point.positionM === 0 : point.positionM > points[index - 1]!.positionM)
        && Number.isFinite(point.speedLimitKmh) && point.speedLimitKmh >= .001 && point.speedLimitKmh <= 1000)
}
export function limitAtPosition(points: readonly SpeedLimitPoint[], positionM: number): number {
    let low = 0, high = points.length
    while (low < high) { const middle = Math.floor((low + high) / 2); if (points[middle]!.positionM <= positionM) low = middle + 1; else high = middle }
    return points[Math.max(0, low - 1)]?.speedLimitKmh ?? 0
}
/** Split only at new limit boundaries; keep every original gradient/resistance boundary. */
export function applySpeedLimitPoints<T extends TractionRouteSegment>(segments: readonly T[], points: readonly SpeedLimitPoint[]): T[] {
    if (!points.length) return segments.map(segment => ({ ...segment }))
    const output: T[] = []
    let index = 0
    for (const segment of segments) {
        let cursor = segment.startM
        while (index + 1 < points.length && points[index + 1]!.positionM <= cursor) index++
        while (cursor < segment.endM) {
            const endM = Math.min(segment.endM, points[index + 1]?.positionM ?? segment.endM)
            output.push({ ...segment, startM: cursor, endM, speedLimitKmh: points[index]!.speedLimitKmh })
            cursor = endM
            if (index + 1 < points.length && points[index + 1]!.positionM <= cursor) index++
        }
    }
    return output
}
/** Editing a table interval changes only that interval and restores the following limit. */
export function setIntervalLimit(points: readonly SpeedLimitPoint[], startM: number, endM: number, speedLimitKmh: number, lengthM: number): SpeedLimitPoint[] {
    const after = limitAtPosition(points, endM)
    const result = points.filter(point => point.positionM < startM || point.positionM > endM).map(point => ({ ...point }))
    result.push({ positionM: startM, speedLimitKmh })
    if (endM < lengthM) result.push({ positionM: endM, speedLimitKmh: after })
    return result.sort((a, b) => a.positionM - b.positionM)
}
