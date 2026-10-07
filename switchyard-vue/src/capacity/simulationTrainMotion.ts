import { isDwellingRoute } from './simulationDwelling.ts'
import { projectTrainCarToViewport, projectTrainPathToViewport } from './simulationViewport.ts'
import type { SimulationViewport, TrainCarPose } from './simulationViewport.ts'
import { buildTrainMotionProfiles, sampleTrainMotion, selectTrainMotionRuns } from './three/trainMotion.ts'
import type { TrainMotionRun } from './three/trainMotion.ts'

export const planarTrainCarCount = 8
const nominalCarLength = 34
const nominalCarWidth = 14
const nominalCarGap = 4

export interface PlanarTrainCar extends TrainCarPose {
    key: string
    trainID: string
    carIndex: number
}

/** Tween successive screen poses without scaling the body or taking a full turn at 0°. */
export function interpolatePlanarTrainCar<T extends TrainCarPose>(source: T, target: T, progress: number): T {
    const fraction = Math.max(0, Math.min(1, progress))
    return {
        ...target,
        x: source.x + (target.x - source.x) * fraction,
        y: source.y + (target.y - source.y) * fraction,
        angle: source.angle + (((target.angle - source.angle) % 360 + 540) % 360 - 180) * fraction,
    }
}

/** Cache geometry and one formation per train until the plan or viewport changes. */
export function buildPlanarTrainScene(runs: readonly TrainMotionRun[], viewport: SimulationViewport) {
    const projectedRuns = runs.map(run => ({ ...run, path: projectTrainPathToViewport(run.path, viewport) }))
    const body = projectTrainCarToViewport({ x: 0, y: 0, angle: 0,
        length: nominalCarLength, width: nominalCarWidth }, viewport)
    const gap = nominalCarGap * body.length / nominalCarLength
    const nominalLength = planarTrainCarCount * body.length + (planarTrainCarCount - 1) * gap
    const scaleByTrain = new Map<string, number>()
    for (const run of projectedRuns) {
        // Fit the shortest stop once, including arrival and departure. Never
        // shrink only the currently dwelling train or resize individual cars.
        const scale = isDwellingRoute(run.route.type) && run.path.totalLength > 0
            ? Math.min(1, run.path.totalLength * 0.75 / nominalLength) : 1
        scaleByTrain.set(run.train.id, Math.min(scaleByTrain.get(run.train.id) ?? 1, scale))
    }
    const formations = new Map([...scaleByTrain].map(([trainID, scale]) => [trainID, {
        length: body.length * scale,
        width: body.width * scale,
        pitch: (body.length + gap) * scale,
    }]))
    return { runs: projectedRuns, formations, profiles: buildTrainMotionProfiles(projectedRuns) }
}

export function samplePlanarTrainScene(
    scene: ReturnType<typeof buildPlanarTrainScene>, currentSeconds: number, singleTrain: boolean,
): PlanarTrainCar[] {
    // Keep trains visible between operations and select only one formation
    // when route locking overlaps another operation of the same train.
    const visibleRuns = selectTrainMotionRuns(scene.runs, currentSeconds)
    if (singleTrain && visibleRuns.length === 0) {
        const ordered = [...scene.runs].sort((left, right) => left.startSeconds - right.startSeconds)
        const run = currentSeconds < (ordered[0]?.startSeconds ?? 0) ? ordered[0] : ordered[ordered.length - 1]
        if (run) visibleRuns.push(run)
    }
    return visibleRuns.flatMap(run => {
        const formation = scene.formations.get(run.train.id)
        const motion = scene.profiles.get(run.key)
        if (!formation || !motion) return []
        return Array.from({ length: planarTrainCarCount }, (_, carIndex) => {
            const offset = ((planarTrainCarCount - 1) / 2 - carIndex) * formation.pitch
            const pose = sampleTrainMotion(motion, currentSeconds, offset,
                [-formation.length * 0.35, formation.length * 0.35])
            return {
                key: JSON.stringify([run.train.id, carIndex]),
                trainID: run.train.id,
                carIndex,
                x: pose.x,
                y: pose.y,
                angle: pose.angle,
                length: formation.length,
                width: formation.width,
            }
        })
    })
}
