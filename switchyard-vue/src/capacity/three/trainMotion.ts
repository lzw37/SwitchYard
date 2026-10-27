import { isDwellingRoute } from '../simulationDwelling.ts'
import { sampleTrainPath, sampleTrainPose } from './trainPath.ts'
import type { TrainPath, TrainPathPoint } from './trainPath.ts'

export interface TrainMotionRun {
    key: string
    train: { id: string }
    route: { type: string }
    path: TrainPath
    startSeconds: number
    endSeconds: number
    lockSeconds: number
}

/** Distances refer to the centre of the complete formation, not its leading car. */
export interface TrainMotionProfile {
    run: TrainMotionRun
    path: TrainPath
    startDistance: number
    endDistance: number
    moveStartSeconds: number
    moveEndSeconds: number
    directionSign: 1 | -1
    dwelling: boolean
}

const EPSILON = 0.000001

function normalizeAngle(angle: number) {
    return ((angle % 360) + 360) % 360
}

function makePath(points: readonly TrainPathPoint[]): TrainPath {
    const clean: TrainPathPoint[] = []
    for (const point of points) {
        const last = clean[clean.length - 1]
        if (!last || Math.hypot(point.x - last.x, point.y - last.y) > EPSILON) clean.push(point)
    }
    const segments: TrainPath['segments'][number][] = []
    let totalLength = 0
    for (let index = 1; index < clean.length; index++) {
        const from = clean[index - 1]!
        const to = clean[index]!
        const length = Math.hypot(to.x - from.x, to.y - from.y)
        segments.push({ from, to, length, startDistance: totalLength,
            angle: normalizeAngle(Math.atan2(to.y - from.y, to.x - from.x) * 180 / Math.PI) })
        totalLength += length
    }
    return { points: clean, segments, totalLength }
}

function projectPoint(path: TrainPath, point: TrainPathPoint) {
    let result = { distance: 0, error: Infinity }
    for (const segment of path.segments) {
        if (segment.length <= EPSILON) continue
        const dx = segment.to.x - segment.from.x
        const dy = segment.to.y - segment.from.y
        const rate = Math.max(0, Math.min(1,
            ((point.x - segment.from.x) * dx + (point.y - segment.from.y) * dy) / (segment.length * segment.length)))
        const error = Math.hypot(point.x - segment.from.x - dx * rate, point.y - segment.from.y - dy * rate)
        if (error < result.error) result = { distance: segment.startDistance + segment.length * rate, error }
    }
    return result
}

function matchingTolerance(first: TrainPath, second: TrainPath) {
    return Math.max(0.0001, Math.max(first.totalLength, second.totalLength) * 0.0000001)
}

/**
 * Some stored arrival/departure routes stop at the platform's boundary. Attach
 * the known neighbouring track at that boundary, retaining its actual bends.
 * Never join unrelated endpoints with an invented straight line.
 */
function extendConnectedPath(path: TrainPath, adjacent: TrainPath, side: 'start' | 'end'): TrainPath {
    if (!path.segments.length || !adjacent.segments.length) return path
    const tolerance = matchingTolerance(path, adjacent)
    if (adjacent.points.every(point => projectPoint(path, point).error <= tolerance)) return path
    const boundaryDistance = side === 'start' ? 0 : path.totalLength
    const boundary = sampleTrainPath(path, boundaryDistance)
    const projection = projectPoint(adjacent, boundary)
    if (projection.error > tolerance) return path
    const adjacentAngle = sampleTrainPath(adjacent, projection.distance).angle
    let aligned = Math.cos((adjacentAngle - boundary.angle) * Math.PI / 180) >= 0
    // At an endpoint, continue into the available track even for a sharp bend.
    // When tracks overlap, a nearby shared point gives a more reliable direction
    // than the tangent exactly at a segment junction.
    if (projection.distance <= EPSILON) aligned = side === 'end'
    else if (projection.distance >= adjacent.totalLength - EPSILON) aligned = side === 'start'
    else {
        const probeLength = Math.min(path.totalLength, Math.max(EPSILON * 10, path.totalLength * 0.00001))
        const probe = projectPoint(adjacent, sampleTrainPath(path,
            side === 'start' ? probeLength : path.totalLength - probeLength))
        const distanceChange = side === 'start'
            ? probe.distance - projection.distance : projection.distance - probe.distance
        if (probe.error <= tolerance && Math.abs(distanceChange) > EPSILON) aligned = distanceChange > 0
    }
    const oriented = aligned ? adjacent : makePath([...adjacent.points].reverse())
    const connection = projectPoint(oriented, boundary).distance
    if (side === 'start') {
        const prefix = oriented.segments
            .filter(segment => segment.startDistance < connection - EPSILON)
            .map(segment => segment.from)
        return prefix.length ? makePath([...prefix, ...path.points]) : path
    }
    const suffix = oriented.segments
        .filter(segment => segment.startDistance + segment.length > connection + EPSILON)
        .map(segment => segment.to)
    return suffix.length ? makePath([...path.points, ...suffix]) : path
}

function groupedRuns<T extends TrainMotionRun>(runs: readonly T[]) {
    const grouped = new Map<string, T[]>()
    for (const run of runs) {
        const trainRuns = grouped.get(run.train.id) || []
        trainRuns.push(run)
        grouped.set(run.train.id, trainRuns)
    }
    return grouped
}

function midpoint(path: TrainPath) {
    return sampleTrainPath(path, path.totalLength / 2)
}

/**
 * Build once when the plan or layout changes. Every pose is subsequently a pure
 * function of the clock, so seeking, pausing and replaying cannot accumulate lag.
 */
export function buildTrainMotionProfiles(runs: readonly TrainMotionRun[]): Map<string, TrainMotionProfile> {
    const profiles = new Map<string, TrainMotionProfile>()
    for (const trainRuns of groupedRuns(runs).values()) {
        // An outgoing route can lock before dwelling ends. Its completion still
        // follows that stop, which makes end order the physical operation order.
        const ordered = [...trainRuns].sort((left, right) => left.endSeconds - right.endSeconds
            || left.startSeconds - right.startSeconds)
        let previous: TrainMotionProfile | undefined
        for (let index = 0; index < ordered.length; index++) {
            const run = ordered[index]!
            const next = ordered[index + 1]
            const dwelling = isDwellingRoute(run.route.type)
            let path = run.path
            if (!dwelling && previous) path = extendConnectedPath(path, previous.path, 'start')
            if (!dwelling && next) {
                path = extendConnectedPath(path, next.path, 'end')
            }
            let startDistance = dwelling ? path.totalLength / 2 : 0
            // Adjacent track supports carriages spanning the boundary; the
            // formation centre still stops at this operation's own endpoint.
            let endDistance = dwelling ? startDistance
                : projectPoint(path, sampleTrainPath(run.path, run.path.totalLength)).distance
            let connectedToPrevious = false
            if (previous) {
                const previousPoint = sampleTrainPath(previous.path, previous.endDistance)
                const projected = projectPoint(path, previousPoint)
                connectedToPrevious = projected.error <= matchingTolerance(path, previous.path)
                if (!dwelling && connectedToPrevious) startDistance = projected.distance
            }
            if (!dwelling && next && isDwellingRoute(next.route.type)) {
                const projected = projectPoint(path, midpoint(next.path))
                if (projected.error <= matchingTolerance(path, next.path)) endDistance = projected.distance
            }
            // A route's node list may be reversed relative to the preceding
            // operation. Keep physical carriage identity and cab orientation.
            let directionSign: 1 | -1 = 1
            if (previous && connectedToPrevious) {
                const previousAngle = sampleTrainPath(previous.path, previous.endDistance).angle
                    + (previous.directionSign < 0 ? 180 : 0)
                const nextAngle = sampleTrainPath(path, startDistance).angle
                directionSign = Math.cos((nextAngle - previousAngle) * Math.PI / 180) >= 0 ? 1 : -1
            }
            let moveStartSeconds = run.startSeconds + Math.max(0, run.lockSeconds)
            let moveEndSeconds = run.endSeconds
            if (!dwelling && previous && connectedToPrevious) {
                moveStartSeconds = Math.max(moveStartSeconds, previous.run.endSeconds)
            }
            if (!dwelling && next && isDwellingRoute(next.route.type)) {
                moveEndSeconds = Math.min(moveEndSeconds, next.startSeconds)
            }
            if (dwelling) {
                moveStartSeconds = run.startSeconds
                moveEndSeconds = run.endSeconds
            } else {
                moveStartSeconds = Math.min(moveStartSeconds, moveEndSeconds)
            }
            const profile: TrainMotionProfile = { run, path, startDistance, endDistance,
                moveStartSeconds, moveEndSeconds, directionSign, dwelling }
            profiles.set(run.key, profile)
            previous = profile
        }
    }
    return profiles
}

/** Keep one continuous visible formation through overlaps and gaps in its work. */
export function selectTrainMotionRuns<T extends TrainMotionRun>(runs: readonly T[], currentSeconds: number): T[] {
    const visible: T[] = []
    for (const trainRuns of groupedRuns(runs).values()) {
        const firstStart = Math.min(...trainRuns.map(run => run.startSeconds))
        const lastEnd = Math.max(...trainRuns.map(run => run.endSeconds))
        if (currentSeconds < firstStart || currentSeconds >= lastEnd) continue
        const active = trainRuns.filter(run => currentSeconds >= run.startSeconds && currentSeconds < run.endSeconds)
        const dwelling = active.filter(run => isDwellingRoute(run.route.type))
        const candidates = dwelling.length ? dwelling : active
        const selected = [...candidates].sort((left, right) => left.endSeconds - right.endSeconds
            || left.startSeconds - right.startSeconds)[0]
            || [...trainRuns].filter(run => run.endSeconds <= currentSeconds)
                .sort((left, right) => right.endSeconds - left.endSeconds)[0]
        if (selected) visible.push(selected)
    }
    return visible
}

export function sampleTrainMotion(
    profile: TrainMotionProfile,
    currentSeconds: number,
    carOffset: number,
    bogieOffsets: readonly [number, number],
) {
    const duration = profile.moveEndSeconds - profile.moveStartSeconds
    const linear = duration > EPSILON
        ? Math.max(0, Math.min(1, (currentSeconds - profile.moveStartSeconds) / duration))
        : currentSeconds <= profile.moveStartSeconds ? 0 : 1
    // Zero velocity at both boundaries prevents the train snapping into motion
    // after a dwell, and brings it smoothly to rest at the common parking point.
    const progress = linear * linear * (3 - 2 * linear)
    const centreDistance = profile.dwelling ? profile.startDistance
        : profile.startDistance + (profile.endDistance - profile.startDistance) * progress
    const sign = profile.directionSign
    const pose = sampleTrainPose(profile.path, centreDistance + sign * carOffset,
        [sign * bogieOffsets[0], sign * bogieOffsets[1]])
    if (sign > 0) return pose
    return { ...pose, angle: normalizeAngle(pose.angle + 180),
        frontBogie: { ...pose.rearBogie, angle: normalizeAngle(pose.rearBogie.angle + 180) },
        rearBogie: { ...pose.frontBogie, angle: normalizeAngle(pose.frontBogie.angle + 180) } }
}
