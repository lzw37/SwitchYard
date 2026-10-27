/** Both the imported CSV abbreviation and the stored route type mean a stop. */
export function isDwellingRoute(type: string | undefined): boolean {
    return ['dw', 'dwelling', '停留', '停留进路'].includes(String(type ?? '').trim().toLowerCase())
}

/** Cars are sampled by their centres, with each following car one pitch behind. */
export function getDwellingHeadDistance(pathLength: number, carCount: number, carPitch: number): number {
    return pathLength / 2 + Math.max(0, carCount - 1) * carPitch / 2
}

interface TimedTrainRun {
    train: { id: string }
    route: { type: string }
    startSeconds: number
    endSeconds: number
}

/** A departure can lock its route before dwelling ends; render one parked train. */
export function preferDwellingRuns<T extends TimedTrainRun>(runs: readonly T[], currentSeconds: number): T[] {
    const dwellingByTrain = new Map<string, T>()
    for (const run of runs) {
        if (isDwellingRoute(run.route.type) && currentSeconds >= run.startSeconds && currentSeconds < run.endSeconds) {
            dwellingByTrain.set(run.train.id, run)
        }
    }
    return runs.filter(run => !dwellingByTrain.has(run.train.id) || dwellingByTrain.get(run.train.id) === run)
}
