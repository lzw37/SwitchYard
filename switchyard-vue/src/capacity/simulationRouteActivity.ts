import { isDwellingRoute } from './simulationDwelling.ts'

interface TimedRouteRun {
    route: { type: string }
    startSeconds: number
    endSeconds: number
    lockSeconds: number
}

export type RouteRunPhase = 'waiting' | 'locking' | 'moving' | 'dwelling' | 'finished'

export function getRouteRunPhase(run: TimedRouteRun | null, currentSeconds: number): RouteRunPhase {
    if (!run || currentSeconds < run.startSeconds) return 'waiting'
    if (currentSeconds >= run.endSeconds) return 'finished'
    if (isDwellingRoute(run.route.type)) return 'dwelling'
    if (currentSeconds <= run.startSeconds + run.lockSeconds) return 'locking'
    return 'moving'
}

/** Every concurrent activity owns a route, even when the train body is parked. */
export function getHighlightedRouteRunIndices(runs: readonly TimedRouteRun[], currentSeconds: number, previewIndex = -1): number[] {
    const active = runs.flatMap((run, index) => currentSeconds >= run.startSeconds && currentSeconds < run.endSeconds ? [index] : [])
    if (active.length) return active
    // Single-train playback retains its route preview while no activity is running.
    return Number.isInteger(previewIndex) && previewIndex >= 0 && previewIndex < runs.length ? [previewIndex] : []
}

/** Count activities, not train formations; parking cannot suppress another route. */
export function countActiveRoutePhases(runs: readonly TimedRouteRun[], currentSeconds: number) {
    const counts = { locking: 0, moving: 0, dwelling: 0 }
    for (const run of runs) {
        const phase = getRouteRunPhase(run, currentSeconds)
        if (phase === 'locking' || phase === 'moving' || phase === 'dwelling') counts[phase]++
    }
    return counts
}
