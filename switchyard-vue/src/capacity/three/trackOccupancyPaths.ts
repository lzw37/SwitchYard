import * as THREE from 'three'
import type { TrackOccupancyPath } from './trackOccupancyOverlay'

export type TrackOccupancyBinding =
    | { linkId: string }
    | { linkIds: readonly [string, string]; points: readonly THREE.Vector3[] }

interface RenderedPath {
    id: string
    points: readonly THREE.Vector3[]
}

interface SourceSegment {
    start: THREE.Vector3
    delta: THREE.Vector3
    length: number
    along: number
}

interface CurveReference {
    segments: SourceSegment[]
    midpoint: number
}

function validPoint(point: THREE.Vector3) {
    return Number.isFinite(point.x) && Number.isFinite(point.y) && Number.isFinite(point.z)
}

function validLinkId(id: string) {
    return typeof id === 'string' && id.trim().length > 0
}

function buildCurveReference(points: readonly THREE.Vector3[]): CurveReference | null {
    if (points.length < 2 || !points.every(validPoint)) return null
    const segments: SourceSegment[] = []
    let length = 0
    for (let index = 1; index < points.length; index++) {
        const start = points[index - 1]!
        const delta = points[index]!.clone().sub(start)
        const segmentLength = delta.length()
        if (segmentLength === 0) continue
        segments.push({ start, delta, length: segmentLength, along: length })
        length += segmentLength
    }
    return length > 0 && Number.isFinite(length) ? { segments, midpoint: length / 2 } : null
}

function projectAlong(point: THREE.Vector3, reference: CurveReference) {
    let closestDistance = Infinity
    let along = 0
    for (const segment of reference.segments) {
        const relative = point.clone().sub(segment.start)
        const t = THREE.MathUtils.clamp(relative.dot(segment.delta) / segment.length ** 2, 0, 1)
        const distance = relative.addScaledVector(segment.delta, -t).lengthSq()
        if (distance < closestDistance) {
            closestDistance = distance
            along = segment.along + t * segment.length
        }
    }
    return along
}

/**
 * Match the final, possibly trimmed/reversed turnout paths to their source Links.
 * A connector changes ownership at the full source curve's arc-length midpoint,
 * regardless of how many rendered fragments share its ID.
 */
export function mapTrackOccupancyPaths(
    paths: readonly RenderedPath[],
    bindings: ReadonlyMap<string, TrackOccupancyBinding>,
): TrackOccupancyPath[] {
    const result: TrackOccupancyPath[] = []
    const references = new Map<string, CurveReference | null>()
    for (const path of paths) {
        const binding = bindings.get(path.id)
        if (!binding || path.points.length < 2 || !path.points.every(validPoint)) continue
        if ('linkId' in binding) {
            if (validLinkId(binding.linkId)) {
                result.push({ id: path.id, linkId: binding.linkId, points: path.points.map(point => point.clone()) })
            }
            continue
        }
        if (binding.linkIds.length !== 2 || !binding.linkIds.every(validLinkId)) continue
        if (!references.has(path.id)) references.set(path.id, buildCurveReference(binding.points))
        const reference = references.get(path.id)
        if (!reference) continue

        const along = path.points.map(point => projectAlong(point, reference))
        let active: TrackOccupancyPath | null = null
        const appendSegment = (start: THREE.Vector3, end: THREE.Vector3, linkId: string) => {
            if (start.distanceToSquared(end) === 0) return
            if (!active || active.linkId !== linkId) {
                active = { id: path.id, linkId, points: [start.clone()] }
                result.push(active)
            }
            active.points.push(end.clone())
        }

        for (let index = 1; index < path.points.length; index++) {
            const start = path.points[index - 1]!
            const end = path.points[index]!
            const from = along[index - 1]!
            const to = along[index]!
            const midpoint = reference.midpoint
            if ((from < midpoint && to > midpoint) || (from > midpoint && to < midpoint)) {
                // Interpolate on the rendered segment, retaining the actual rail
                // alignment even when turnout construction moved it off the source.
                const split = start.clone().lerp(end, (midpoint - from) / (to - from))
                appendSegment(start, split, binding.linkIds[from < midpoint ? 0 : 1])
                appendSegment(split, end, binding.linkIds[to < midpoint ? 0 : 1])
            } else {
                appendSegment(start, end, binding.linkIds[(from + to) / 2 <= midpoint ? 0 : 1])
            }
        }
    }
    return result
}
