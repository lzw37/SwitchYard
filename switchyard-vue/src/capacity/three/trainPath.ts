/** Source-layout coordinates; intentionally independent of Three.js and playback time. */
export interface TrainPathPoint {
    x: number
    y: number
    nodeId?: string
}

interface TrainPathSegment {
    from: TrainPathPoint
    to: TrainPathPoint
    length: number
    startDistance: number
    angle: number
}

/** Structurally compatible with StationLayout3D's existing PolylinePath. */
export interface TrainPath {
    points: readonly TrainPathPoint[]
    segments: readonly TrainPathSegment[]
    totalLength: number
}

export interface TrainRouteCurve {
    nodeID: string
    tangentLinkID1: string
    tangentLinkID2: string
    /** Ordered from the tangent point on link 1 to the tangent point on link 2. */
    points: readonly TrainPathPoint[]
}

export interface TrainRouteLink {
    id: string
    fromNodeID: string
    toNodeID: string
}

export interface RenderedTrainPath {
    originalStart: TrainPathPoint
    originalEnd: TrainPathPoint
    points: readonly TrainPathPoint[]
}

const EPSILON = 0.000001

function normalizeAngle(angle: number) {
    return ((angle % 360) + 360) % 360
}

function liesOnSegment(point: TrainPathPoint, start: TrainPathPoint, end: TrainPathPoint) {
    const dx = end.x - start.x
    const dy = end.y - start.y
    const lengthSquared = dx * dx + dy * dy
    if (lengthSquared < EPSILON) return false
    const rate = ((point.x - start.x) * dx + (point.y - start.y) * dy) / lengthSquared
    if (rate < -EPSILON || rate > 1 + EPSILON) return false
    const distance = Math.hypot(point.x - start.x - dx * rate, point.y - start.y - dy * rate)
    return distance <= Math.max(0.001, Math.sqrt(lengthSquared) * 0.00001)
}

function connects(link: TrainRouteLink, firstNodeID: string, secondNodeID: string) {
    return (link.fromNodeID === firstNodeID && link.toNodeID === secondNodeID)
        || (link.toNodeID === firstNodeID && link.fromNodeID === secondNodeID)
}

/** Replace complete straight spans with the exact samples used by the rails. */
export function followRenderedPaths(
    routePoints: readonly TrainPathPoint[],
    paths: readonly RenderedTrainPath[],
): TrainPathPoint[] {
    const result: TrainPathPoint[] = []
    const append = (point: TrainPathPoint) => {
        const previous = result[result.length - 1]
        if (!previous || Math.hypot(point.x - previous.x, point.y - previous.y) > EPSILON) {
            result.push({ ...point })
        }
    }
    routePoints.forEach((start, index) => {
        append(start)
        const end = routePoints[index + 1]
        if (!end) return
        const dx = end.x - start.x
        const dy = end.y - start.y
        const squaredLength = dx * dx + dy * dy
        if (squaredLength <= EPSILON) return
        const rate = (point: TrainPathPoint) => ((point.x - start.x) * dx + (point.y - start.y) * dy) / squaredLength
        const candidates = paths.flatMap((path) => {
            if (path.points.length < 2
                || !liesOnSegment(path.originalStart, start, end)
                || !liesOnSegment(path.originalEnd, start, end)) return []
            const firstRate = rate(path.originalStart)
            const lastRate = rate(path.originalEnd)
            if (Math.abs(firstRate - lastRate) <= EPSILON) return []
            return [{
                startRate: Math.min(firstRate, lastRate),
                endRate: Math.max(firstRate, lastRate),
                points: firstRate < lastRate ? path.points : [...path.points].reverse(),
            }]
        }).sort((a, b) => a.startRate - b.startRate || b.endRate - a.endRate)
        let cursor = 0
        for (const candidate of candidates) {
            // Coincident/overlapping drawing elements must not send a train
            // backwards over a span already supplied by another rail path.
            if (candidate.startRate < cursor - EPSILON) continue
            candidate.points.forEach(append)
            cursor = candidate.endRate
        }
    })
    return result
}

/**
 * Follow the same explicit fillets as the rendered rails. A curve is used only
 * when this route traverses both of its linked legs; unrelated switch routes
 * must retain their original topology. Neither input nor node/link metadata is
 * mutated. Call before constructing cumulative path segment lengths.
 */
export function insertRouteCurves(
    routePoints: readonly TrainPathPoint[],
    curves: readonly TrainRouteCurve[],
    links: readonly TrainRouteLink[],
): TrainPathPoint[] {
    const linksByID = new Map(links.map((link) => [link.id, link]))
    const curvesByNode = new Map<string, TrainRouteCurve[]>()
    curves.forEach((curve) => {
        if (curve.points.length < 2) return
        const nodeCurves = curvesByNode.get(curve.nodeID) || []
        nodeCurves.push(curve)
        curvesByNode.set(curve.nodeID, nodeCurves)
    })

    const result: TrainPathPoint[] = []
    const append = (point: TrainPathPoint) => {
        const previous = result[result.length - 1]
        if (!previous || Math.hypot(point.x - previous.x, point.y - previous.y) > EPSILON) {
            result.push({ ...point })
        }
    }

    routePoints.forEach((corner, index) => {
        const previous = routePoints[index - 1]
        const next = routePoints[index + 1]
        const candidates = corner.nodeId ? curvesByNode.get(corner.nodeId) || [] : []
        let replacement: readonly TrainPathPoint[] | undefined

        if (previous?.nodeId && next?.nodeId && corner.nodeId) {
            for (const curve of candidates) {
                const link1 = linksByID.get(curve.tangentLinkID1)
                const link2 = linksByID.get(curve.tangentLinkID2)
                if (!link1 || !link2) continue
                const forward = connects(link1, previous.nodeId, corner.nodeId)
                    && connects(link2, corner.nodeId, next.nodeId)
                const reverse = connects(link2, previous.nodeId, corner.nodeId)
                    && connects(link1, corner.nodeId, next.nodeId)
                if (!forward && !reverse) continue
                const samples = forward ? curve.points : [...curve.points].reverse()
                const first = samples[0]
                const last = samples[samples.length - 1]
                if (!first || !last || !liesOnSegment(first, previous, corner) || !liesOnSegment(last, corner, next)) continue
                replacement = samples
                break
            }
        }

        if (replacement) replacement.forEach(append)
        else append(corner)
    })
    return result
}

/**
 * Continue the terminal tangent beyond either route endpoint. Clamping every
 * trailing car to distance zero stacks the entire consist at the entry signal.
 */
export function sampleTrainPath(path: TrainPath, distance: number) {
    const firstSegment = path.segments[0]
    const lastSegment = path.segments[path.segments.length - 1]
    if (!firstSegment || !lastSegment) {
        const point = path.points[0] || { x: 0, y: 0 }
        return { x: point.x, y: point.y, angle: 0 }
    }

    const safeDistance = Number.isFinite(distance) ? distance : 0
    let segment = firstSegment
    if (safeDistance >= path.totalLength) segment = lastSegment
    else if (safeDistance > 0) {
        let lower = 0
        let upper = path.segments.length - 1
        while (lower < upper) {
            const middle = Math.floor((lower + upper) / 2)
            const candidate = path.segments[middle]!
            if (safeDistance <= candidate.startDistance + candidate.length) upper = middle
            else lower = middle + 1
        }
        segment = path.segments[lower] || lastSegment
    }
    const rate = segment.length > EPSILON ? (safeDistance - segment.startDistance) / segment.length : 0
    return {
        x: segment.from.x + (segment.to.x - segment.from.x) * rate,
        y: segment.from.y + (segment.to.y - segment.from.y) * rate,
        angle: normalizeAngle(segment.angle),
    }
}

/**
 * A rigid carriage spans its bogies. Expand arc sampling until the chord has
 * the actual wheelbase, then derive the body's origin and yaw from that chord.
 * A numeric wheelbase uses symmetric mounts; a tuple gives signed rear/front
 * offsets from the body origin, allowing a cab's asymmetric bogie positions.
 */
export function sampleTrainPose(
    path: TrainPath,
    centerDistance: number,
    bogieSpacing: number | readonly [number, number],
) {
    const halfSpacing = typeof bogieSpacing === 'number' && Number.isFinite(bogieSpacing)
        ? Math.max(0, bogieSpacing) / 2 : 0
    const offsets = typeof bogieSpacing === 'number'
        ? [-halfSpacing, halfSpacing]
        : bogieSpacing.map((offset) => Number.isFinite(offset) ? offset : 0).sort((a, b) => a - b)
    const rearOffset = offsets[0] || 0
    const frontOffset = offsets[1] || 0
    const wheelbase = frontOffset - rearOffset
    const sampleBogies = (stretch: number) => {
        const rearBogie = sampleTrainPath(path, centerDistance + rearOffset * stretch)
        const frontBogie = sampleTrainPath(path, centerDistance + frontOffset * stretch)
        return { rearBogie, frontBogie, span: Math.hypot(frontBogie.x - rearBogie.x, frontBogie.y - rearBogie.y) }
    }
    let bogies = sampleBogies(1)

    // A 25 m rigid body requires a slightly longer arc than its wheelbase on a
    // bend. Straight paths incur no search. Bound the work for malformed paths
    // (e.g. a route doubling back onto itself with an impossible turning radius).
    if (wheelbase > EPSILON && bogies.span < wheelbase * (1 - 0.000001)) {
        let lower = 1
        let lowerBogies = bogies
        let upper = 1.25
        let upperBogies = sampleBogies(upper)
        while (upperBogies.span < wheelbase && upper < 16) {
            lower = upper
            lowerBogies = upperBogies
            upper = Math.min(16, upper * 1.5)
            upperBogies = sampleBogies(upper)
        }
        if (upperBogies.span >= wheelbase) {
            for (let iteration = 0; iteration < 12; iteration++) {
                const middle = (lower + upper) / 2
                const middleBogies = sampleBogies(middle)
                if (middleBogies.span < wheelbase) {
                    lower = middle
                    lowerBogies = middleBogies
                }
                else {
                    upper = middle
                    upperBogies = middleBogies
                }
            }
            const remainingSpan = upperBogies.span - lowerBogies.span
            bogies = remainingSpan > EPSILON
                ? sampleBogies(lower + (upper - lower) * (wheelbase - lowerBogies.span) / remainingSpan)
                : upperBogies
        }
    }

    const { rearBogie, frontBogie } = bogies
    const dx = frontBogie.x - rearBogie.x
    const dy = frontBogie.y - rearBogie.y
    const angle = Math.hypot(dx, dy) > EPSILON
        ? normalizeAngle(Math.atan2(dy, dx) * 180 / Math.PI)
        : sampleTrainPath(path, centerDistance).angle
    const originRate = wheelbase > EPSILON ? -rearOffset / wheelbase : 0.5
    return {
        x: rearBogie.x + dx * originRate,
        y: rearBogie.y + dy * originRate,
        angle,
        frontBogie,
        rearBogie,
    }
}
