import * as THREE from 'three'

/** A centreline in scene coordinates. Its height is supplied by this model. */
export interface RailwayPath { id: string; points: THREE.Vector3[] }
export interface RailwaySwitch { id: string; position: THREE.Vector3 }

export function getRailwayDimensions(gauge: number) {
    const g = Math.max(0.001, gauge)
    return {
        gauge: g,
        ballastHeight: g * 0.2,
        sleeperHeight: g * 0.13,
        sleeperTop: g * 0.27,
        railBase: g * 0.275,
        railHeight: g * 0.12,
        railTop: g * 0.395,
        sleeperSpacing: g * 0.43,
        sleeperLength: g * 1.83,
    }
}

type Dimensions = ReturnType<typeof getRailwayDimensions>
interface Path extends RailwayPath { length: number; distances: number[] }
interface Turnout {
    id: string
    origin: THREE.Vector3
    direction: THREE.Vector3
    normal: THREE.Vector3
    main: Path
    branch: Path
    stem: Path
    fanLength: number
}
interface Rail {
    points: THREE.Vector3[]
    path: Path
    side: number
    widths: number[]
    cuts: { start: number; end: number }[]
    distances: number[]
}
interface Instance { position: THREE.Vector3; angle: number; scale: THREE.Vector3; shade?: number }
interface TurnoutModel {
    id: string
    origin: THREE.Vector3
    handedness: number
    bladeLength: number
    blades: { points: THREE.Vector3[]; widths: number[]; closed: boolean }[]
    frog: THREE.Vector3 | null
    nose: THREE.Vector3 | null
    wings: THREE.Vector3[][]
    guards: THREE.Vector3[][]
    flangeway: number
}

const UP = new THREE.Vector3(0, 1, 0)
const clamp = THREE.MathUtils.clamp

function flat(point: THREE.Vector3) { return new THREE.Vector3(point.x, 0, point.z) }
function normal(direction: THREE.Vector3) { return new THREE.Vector3(-direction.z, 0, direction.x) }
function cross(a: THREE.Vector3, b: THREE.Vector3) { return a.x * b.z - a.z * b.x }
function hash(a: number, b = 0) {
    const value = Math.sin(a * 127.1 + b * 311.7) * 43758.5453123
    return value - Math.floor(value)
}

function makePath(id: string, source: THREE.Vector3[]): Path {
    const points: THREE.Vector3[] = []
    for (const point of source) {
        if (!Number.isFinite(point.x) || !Number.isFinite(point.z)) continue
        if (!points.length || points[points.length - 1]!.distanceToSquared(flat(point)) > 1e-12) points.push(flat(point))
    }
    const distances = [0]
    for (let i = 1; i < points.length; i++) distances.push(distances[i - 1]! + points[i]!.distanceTo(points[i - 1]!))
    return { id, points, distances, length: distances[distances.length - 1] || 0 }
}

function sample(path: Pick<Path, 'points' | 'distances' | 'length'>, distance: number) {
    const d = clamp(distance, 0, path.length)
    let lo = 0
    let hi = path.distances.length - 1
    while (lo + 1 < hi) {
        const middle = (lo + hi) >> 1
        if (path.distances[middle]! <= d) lo = middle
        else hi = middle
    }
    const start = path.points[lo]!
    const end = path.points[Math.min(lo + 1, path.points.length - 1)]!
    const span = path.distances[Math.min(lo + 1, path.distances.length - 1)]! - path.distances[lo]!
    const direction = end.clone().sub(start).normalize()
    return { point: start.clone().lerp(end, span > 0 ? (d - path.distances[lo]!) / span : 0), direction }
}

function nearest(path: Path, position: THREE.Vector3) {
    let best = { distance: Infinity, along: 0, point: path.points[0]!.clone() }
    for (let i = 0; i < path.points.length - 1; i++) {
        const a = path.points[i]!
        const delta = path.points[i + 1]!.clone().sub(a)
        const t = clamp(position.clone().sub(a).dot(delta) / delta.lengthSq(), 0, 1)
        const point = a.clone().addScaledVector(delta, t)
        const distance = point.distanceTo(position)
        if (distance < best.distance) best = { distance, along: path.distances[i]! + t * delta.length(), point }
    }
    return best
}

function slice(path: Path, from: number, to: number) {
    const points = [sample(path, from).point]
    for (let i = 1; i < path.points.length - 1; i++) {
        if (path.distances[i]! > from && path.distances[i]! < to) points.push(path.points[i]!.clone())
    }
    points.push(sample(path, to).point)
    return points
}

/** Split only where a real centreline touches a turnout, never at a visual crossing. */
function preparePaths(source: RailwayPath[], switches: RailwaySwitch[], gauge: number) {
    const paths: Path[] = []
    const seen = new Set<string>()
    for (const item of source) {
        const path = makePath(item.id, item.points)
        if (path.points.length < 2 || path.length < gauge * 0.015) continue
        const cuts = [0, path.length]
        for (const sw of switches) {
            const hit = nearest(path, flat(sw.position))
            if (hit.distance < gauge * 0.035 && hit.along > gauge * 0.04 && hit.along < path.length - gauge * 0.04) cuts.push(hit.along)
        }
        cuts.sort((a, b) => a - b)
        for (let i = 0; i < cuts.length - 1; i++) {
            if (cuts[i + 1]! - cuts[i]! < gauge * 0.015) continue
            const part = makePath(path.id, slice(path, cuts[i]!, cuts[i + 1]!))
            const keyPoints = part.points.map((p) => `${Math.round(p.x / gauge * 1000)},${Math.round(p.z / gauge * 1000)}`)
            const forward = keyPoints.join(';')
            const reverse = keyPoints.reverse().join(';')
            const key = forward < reverse ? forward : reverse
            if (seen.has(key)) continue
            seen.add(key)
            paths.push(part)
        }
    }
    return paths
}

function orientFrom(path: Path, position: THREE.Vector3) {
    if (path.points[0]!.distanceToSquared(position) > path.points[path.points.length - 1]!.distanceToSquared(position)) {
        Object.assign(path, makePath(path.id, [...path.points].reverse()))
    }
}

/** A short tangent lead-in removes the kink at a schematic node while retaining route endpoints. */
function smoothEntry(path: Path, direction: THREE.Vector3, gauge: number) {
    const reach = Math.min(gauge * 4.5, path.length * 0.32)
    if (reach < gauge * 0.25) return
    const end = sample(path, reach)
    const start = path.points[0]!
    const length = start.distanceTo(end.point)
    const curve = new THREE.CubicBezierCurve3(
        start, start.clone().addScaledVector(direction, length * 0.38),
        end.point.clone().addScaledVector(end.direction, -length * 0.32), end.point,
    )
    Object.assign(path, makePath(path.id, [...curve.getPoints(18), ...slice(path, reach, path.length).slice(1)]))
}

function coordinateAt(path: Path, turnout: Pick<Turnout, 'origin' | 'direction' | 'normal'>, x: number) {
    for (let i = 0; i < path.points.length - 1; i++) {
        const a = path.points[i]!.clone().sub(turnout.origin)
        const b = path.points[i + 1]!.clone().sub(turnout.origin)
        const ax = a.dot(turnout.direction)
        const bx = b.dot(turnout.direction)
        if (x < Math.min(ax, bx) - 1e-6 || x > Math.max(ax, bx) + 1e-6 || Math.abs(ax - bx) < 1e-8) continue
        const t = (x - ax) / (bx - ax)
        return { z: THREE.MathUtils.lerp(a.dot(turnout.normal), b.dot(turnout.normal), t), direction: b.sub(a).normalize() }
    }
    return null
}

function findTurnouts(paths: Path[], switches: RailwaySwitch[], gauge: number) {
    const turnouts: Turnout[] = []
    const handled = new Set<string>()
    for (const sw of switches) {
        const origin = flat(sw.position)
        const key = `${Math.round(origin.x / gauge * 10)},${Math.round(origin.z / gauge * 10)}`
        if (handled.has(key)) continue
        handled.add(key)
        const incident = paths.filter((p) => Math.min(p.points[0]!.distanceTo(origin), p.points[p.points.length - 1]!.distanceTo(origin)) < gauge * 0.12)
        if (incident.length < 3) continue
        const candidates = incident.map((path) => {
            const reversed = path.points[0]!.distanceToSquared(origin) > path.points[path.points.length - 1]!.distanceToSquared(origin)
            const direction = reversed ? sample(path, path.length).direction.negate() : sample(path, 0).direction
            return { path, direction }
        })
        let outgoing: [number, number] = [0, 1]
        let bestDot = -Infinity
        for (let i = 0; i < candidates.length; i++) for (let j = i + 1; j < candidates.length; j++) {
            const dot = candidates[i]!.direction.dot(candidates[j]!.direction)
            if (dot > bestDot) { bestDot = dot; outgoing = [i, j] }
        }
        // A flat diamond crossing does not have a facing-point end.
        if (bestDot < 0.12 || bestDot > 0.99995) continue
        const pair = outgoing.map((index) => candidates[index]!)
        const stem = candidates.filter((_, index) => !outgoing.includes(index)).sort((a, b) => a.direction.dot(pair[0]!.direction) - b.direction.dot(pair[0]!.direction))[0]!
        pair.sort((a, b) => a.direction.dot(stem.direction) - b.direction.dot(stem.direction))
        const main = pair[0]!
        const branch = pair[1]!
        if (main.direction.dot(stem.direction) > -0.65) continue
        orientFrom(main.path, origin)
        orientFrom(branch.path, origin)
        orientFrom(stem.path, origin)
        const direction = main.direction.clone().sub(stem.direction).normalize()
        smoothEntry(main.path, direction, gauge)
        smoothEntry(branch.path, direction, gauge)
        const turnout: Turnout = { id: sw.id, origin, direction, normal: normal(direction), main: main.path, branch: branch.path, stem: stem.path, fanLength: gauge }
        const max = Math.min(gauge * 19, main.path.length * 0.92, branch.path.length * 0.92)
        for (let x = gauge * 0.43; x <= max; x += gauge * 0.43) {
            const a = coordinateAt(main.path, turnout, x)
            const b = coordinateAt(branch.path, turnout, x)
            if (!a || !b || Math.abs(a.z - b.z) > gauge * 2.1) break
            turnout.fanLength = x
        }
        turnouts.push(turnout)
    }
    return turnouts
}

/** Use the same gauge-scaled centreline preparation for rolling-stock route sampling. */
export function prepareRailwayPaths(source: RailwayPath[], switches: RailwaySwitch[], gauge: number): RailwayPath[] {
    const g = getRailwayDimensions(gauge).gauge
    const paths = preparePaths(source, switches, g)
    findTurnouts(paths, switches, g)
    return paths.map((path) => ({ id: path.id, points: path.points.map((point) => point.clone()) }))
}

/** Batched unindexed triangles avoid thousands of Mesh and BufferGeometry objects. */
class Surface {
    positions: number[] = []
    normals: number[] = []
    uvs: number[] = []
    triangle(a: THREE.Vector3, b: THREE.Vector3, c: THREE.Vector3, textureScale = 1) {
        const n = new THREE.Vector3().crossVectors(b.clone().sub(a), c.clone().sub(a)).normalize()
        for (const p of [a, b, c]) {
            this.positions.push(p.x, p.y, p.z)
            this.normals.push(n.x, n.y, n.z)
            this.uvs.push(p.x / textureScale, p.z / textureScale)
        }
    }
    quad(a: THREE.Vector3, b: THREE.Vector3, c: THREE.Vector3, d: THREE.Vector3, textureScale = 1) {
        this.triangle(a, b, d, textureScale)
        this.triangle(b, c, d, textureScale)
    }
    mesh(material: THREE.Material, name: string, shadows = true) {
        const geometry = new THREE.BufferGeometry()
        geometry.setAttribute('position', new THREE.Float32BufferAttribute(this.positions, 3))
        geometry.setAttribute('normal', new THREE.Float32BufferAttribute(this.normals, 3))
        geometry.setAttribute('uv', new THREE.Float32BufferAttribute(this.uvs, 2))
        geometry.computeBoundingSphere()
        const mesh = new THREE.Mesh(geometry, material)
        mesh.name = name
        mesh.castShadow = shadows
        mesh.receiveShadow = true
        return mesh
    }
}

function stoneTextures(gauge: number) {
    const size = 128
    const cells = 20
    const cellSize = size / cells
    const colors = new Uint8Array(size * size * 4)
    const heights = new Float32Array(size * size)
    for (let y = 0; y < size; y++) for (let x = 0; x < size; x++) {
        const cellX = Math.floor(x / cellSize)
        const cellY = Math.floor(y / cellSize)
        let distance = Infinity
        let value = 0
        for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++) {
            const cx = cellX + dx
            const cy = cellY + dy
            const wrappedX = (cx + cells) % cells
            const wrappedY = (cy + cells) % cells
            const sx = (cx + hash(wrappedX, wrappedY)) * cellSize
            const sy = (cy + hash(wrappedX + 19, wrappedY + 47)) * cellSize
            const d = Math.abs(sx - x) * 0.65 + Math.abs(sy - y) * 0.85
            if (d < distance) { distance = d; value = hash(wrappedX + 83, wrappedY - 63) }
        }
        const i = y * size + x
        const grain = hash(x + 201, y + 48)
        const brightness = 75 + value * 69 + grain * 19 - distance * 6
        colors[i * 4] = brightness * 1.03
        colors[i * 4 + 1] = brightness
        colors[i * 4 + 2] = brightness * 0.91
        colors[i * 4 + 3] = 255
        heights[i] = clamp(0.8 - distance * 0.1 + value * 0.1 + grain * 0.06, 0, 1)
    }
    const normals = new Uint8Array(size * size * 4)
    for (let y = 0; y < size; y++) for (let x = 0; x < size; x++) {
        const dx = heights[y * size + (x + 1) % size]! - heights[y * size + (x + size - 1) % size]!
        const dy = heights[((y + 1) % size) * size + x]! - heights[((y + size - 1) % size) * size + x]!
        const n = new THREE.Vector3(-dx * 2.8, -dy * 2.8, 1).normalize()
        const index = (y * size + x) * 4
        normals[index] = (n.x * 0.5 + 0.5) * 255
        normals[index + 1] = (n.y * 0.5 + 0.5) * 255
        normals[index + 2] = (n.z * 0.5 + 0.5) * 255
        normals[index + 3] = 255
    }
    const map = new THREE.DataTexture(colors, size, size)
    const normalMap = new THREE.DataTexture(normals, size, size)
    for (const texture of [map, normalMap]) {
        texture.wrapS = texture.wrapT = THREE.RepeatWrapping
        texture.magFilter = THREE.LinearFilter
        texture.minFilter = THREE.LinearMipmapLinearFilter
        texture.generateMipmaps = true
        texture.anisotropy = 8
        texture.needsUpdate = true
    }
    map.colorSpace = THREE.SRGBColorSpace
    map.name = `ballast-aggregate-${gauge}`
    return { map, normalMap }
}

function tangentAt(points: THREE.Vector3[], i: number) {
    const previous = points[Math.max(0, i - 1)]!
    const next = points[Math.min(points.length - 1, i + 1)]!
    return next.clone().sub(previous).normalize()
}

function addBed(surface: Surface, points: THREE.Vector3[], widths: number[], dimensions: Dimensions) {
    const g = dimensions.gauge
    for (let i = 0; i < points.length - 1; i++) {
        const rings = [i, i + 1].map((index) => {
            const n = normal(tangentAt(points, index))
            const width = widths[index]!
            const p = points[index]!
            const irregular = (hash(Math.round(p.x / g * 10), Math.round(p.z / g * 10)) - 0.5) * g * 0.065
            return [
                p.clone().addScaledVector(n, -width / 2 - g * 0.3 + irregular).setY(g * 0.012),
                p.clone().addScaledVector(n, -width / 2).setY(dimensions.ballastHeight),
                p.clone().addScaledVector(n, width / 2).setY(dimensions.ballastHeight),
                p.clone().addScaledVector(n, width / 2 + g * 0.3 + irregular).setY(g * 0.012),
            ]
        })
        for (let edge = 0; edge < 3; edge++) surface.quad(rings[0]![edge]!, rings[0]![edge + 1]!, rings[1]![edge + 1]!, rings[1]![edge]!, g * 0.85)
    }
}

function insideFan(point: THREE.Vector3, turnout: Turnout, gauge: number) {
    const delta = point.clone().sub(turnout.origin)
    const x = delta.dot(turnout.direction)
    if (x < -gauge * 0.22 || x > turnout.fanLength + gauge * 0.22) return false
    const a = coordinateAt(turnout.main, turnout, Math.max(0, Math.min(x, turnout.fanLength)))
    const b = coordinateAt(turnout.branch, turnout, Math.max(0, Math.min(x, turnout.fanLength)))
    if (!a || !b) return false
    const z = delta.dot(turnout.normal)
    return z > Math.min(a.z, b.z) - gauge * 0.65 && z < Math.max(a.z, b.z) + gauge * 0.65
}

function addRailSurface(sideSurface: Surface, topSurface: Surface, rail: Rail, dimensions: Dimensions) {
    const g = dimensions.gauge
    // UIC-style foot, narrow web, broad head and bevelled running surface.
    const base = dimensions.railBase
    const top = dimensions.railTop
    const profile: [number, number][] = [
        [-0.055, base], [0.055, base], [0.055, base + g * 0.018],
        [0.012, base + g * 0.031], [0.009, top - g * 0.042],
        [0.0245, top - g * 0.035], [0.026, top - g * 0.008], [0.020, top],
        [-0.020, top], [-0.026, top - g * 0.008], [-0.0245, top - g * 0.035],
        [-0.009, top - g * 0.042], [-0.012, base + g * 0.031], [-0.055, base + g * 0.018],
    ]
    const caps = THREE.ShapeUtils.triangulateShape(profile.map(([x, y]) => new THREE.Vector2(x, y)), [])
    const isCut = (distance: number) => rail.cuts.some(cut => distance > cut.start && distance < cut.end)
    for (let i = 0; i < rail.points.length - 1; i++) {
        const start = rail.distances[i]!
        const end = rail.distances[i + 1]!
        if (end - start < 1e-10) continue
        // Insert exact cut boundaries so frog/point joints do not depend on tessellation.
        const boundaries = [start, end, ...rail.cuts.flatMap(cut => [cut.start, cut.end])
            .filter(distance => distance > start && distance < end)].sort((a, b) => a - b)
        for (let part = 1; part < boundaries.length; part++) {
            const a = boundaries[part - 1]!
            const b = boundaries[part]!
            if (b - a < 1e-10 || isCut((a + b) / 2)) continue
            const rings = [a, b].map(distance => {
                const t = (distance - start) / (end - start)
                const n = normal(tangentAt(rail.points, i).lerp(tangentAt(rail.points, i + 1), t).normalize())
                const width = THREE.MathUtils.lerp(rail.widths[i]!, rail.widths[i + 1]!, t)
                const center = rail.points[i]!.clone().lerp(rail.points[i + 1]!, t)
                return profile.map(([lateral, height]) => center.clone().addScaledVector(n, lateral * g * width).setY(height))
            })
            for (let edge = 0; edge < profile.length; edge++) {
                const next = (edge + 1) % profile.length
                const target = edge >= 6 && edge <= 8 ? topSurface : sideSurface
                target.quad(rings[0]![edge]!, rings[1]![edge]!, rings[1]![next]!, rings[0]![next]!)
            }
            for (const [index, close] of [[0, a === 0 || isCut(a - g * 1e-7)],
                [1, b === rail.distances[rail.distances.length - 1] || isCut(b + g * 1e-7)]] as const) {
                if (!close) continue
                for (const [u, v, w] of caps) {
                    const ring = rings[index]!
                    if (index === 0) sideSurface.triangle(ring[u!]!, ring[v!]!, ring[w!]!)
                    else sideSurface.triangle(ring[w!]!, ring[v!]!, ring[u!]!)
                }
            }
        }
    }
}

function offsetRail(path: Path, side: number, dimensions: Dimensions) {
    const spacing = dimensions.gauge * 0.25
    const count = Math.max(1, Math.ceil(path.length / spacing))
    const centers = Array.from({ length: count + 1 }, (_, i) => sample(path, i / count * path.length).point)
    const points: THREE.Vector3[] = []
    const widths: number[] = []
    for (let i = 0; i <= count; i++) {
        const n = normal(tangentAt(centers, i))
        points.push(centers[i]!.clone().addScaledVector(n, side * dimensions.gauge / 2))
        widths.push(1)
    }
    const sampled = makePath(path.id, points)
    return { points, side, path, widths, cuts: [], distances: sampled.distances } as Rail
}

function crossing(first: Rail, second: Rail, origin: THREE.Vector3, gauge: number) {
    for (let i = 0; i < first.points.length - 1; i++) {
        const a = first.points[i]!
        if (a.distanceTo(origin) > gauge * 24) continue
        const r = first.points[i + 1]!.clone().sub(a)
        for (let j = 0; j < second.points.length - 1; j++) {
            const b = second.points[j]!
            if (b.distanceToSquared(a) > gauge * gauge) continue
            const s = second.points[j + 1]!.clone().sub(b)
            const denominator = cross(r, s)
            if (Math.abs(denominator) < 1e-9) continue
            const delta = b.clone().sub(a)
            const t = cross(delta, s) / denominator
            const u = cross(delta, r) / denominator
            if (t < 0 || t > 1 || u < 0 || u > 1) continue
            const point = a.clone().addScaledVector(r, t)
            if (point.distanceTo(origin) < gauge * 1.3) continue
            return { point, firstDistance: first.distances[i]! + r.length() * t, secondDistance: second.distances[j]! + s.length() * u, aDirection: r.normalize(), bDirection: s.normalize() }
        }
    }
    return null
}

function detailRail(points: THREE.Vector3[], widths: number[] = points.map(() => 0.78)): Rail {
    const path = makePath('turnout-detail', points)
    return { path, points: path.points, distances: path.distances, side: 0, widths, cuts: [] }
}

function railPath(rail: Rail): Path {
    return { id: rail.path.id, points: rail.points, distances: rail.distances, length: rail.distances[rail.distances.length - 1]! }
}

function outwardSign(rail: Rail, turnout: Turnout) {
    return sample(rail.path, nearest(rail.path, turnout.origin).along).direction.dot(turnout.direction) >= 0 ? 1 : -1
}

function cutRail(rail: Rail, a: number, b: number) {
    rail.cuts.push({ start: Math.min(a, b), end: Math.max(a, b) })
}

function intersectLines(a: THREE.Vector3, directionA: THREE.Vector3, b: THREE.Vector3, directionB: THREE.Vector3) {
    const denominator = cross(directionA, directionB)
    if (Math.abs(denominator) < 1e-9) return a.clone().lerp(b, 0.5)
    return a.clone().addScaledVector(directionA, cross(b.clone().sub(a), directionB) / denominator)
}

/** One closed V casting replaces the two crossing railheads, with a bevel, web and foot. */
function addCrossingNose(
    hit: THREE.Vector3, nose: THREE.Vector3, directionA: THREE.Vector3, directionB: THREE.Vector3,
    endA: THREE.Vector3, endB: THREE.Vector3, handedness: number,
    dimensions: Dimensions, steel: Surface, tops: Surface,
) {
    const g = dimensions.gauge
    const outsideA = normal(directionA).multiplyScalar(-handedness)
    const outsideB = normal(directionB).multiplyScalar(handedness)
    const bisector = directionA.clone().add(directionB).normalize()
    const reach = Math.min(endA.distanceTo(hit), endB.distanceTo(hit))
    const outline = (width: number, height: number) => {
        const halfWidth = width * g
        const notch = intersectLines(hit.clone().addScaledVector(outsideA, -halfWidth), directionA,
            hit.clone().addScaledVector(outsideB, -halfWidth), directionB)
        const along = clamp(notch.clone().sub(hit).dot(bisector), nose.distanceTo(hit) + g * 0.04, reach * 0.86)
        return [nose.clone(), endA.clone().addScaledVector(outsideA, halfWidth),
            endA.clone().addScaledVector(outsideA, -halfWidth), hit.clone().addScaledVector(bisector, along),
            endB.clone().addScaledVector(outsideB, -halfWidth), endB.clone().addScaledVector(outsideB, halfWidth)]
            .map(point => point.setY(height))
    }
    const levels: [number, number][] = [
        [0.055, dimensions.railBase], [0.055, dimensions.railBase + g * 0.018],
        [0.012, dimensions.railBase + g * 0.031], [0.009, dimensions.railTop - g * 0.042],
        [0.0245, dimensions.railTop - g * 0.035], [0.026, dimensions.railTop - g * 0.008],
        [0.020, dimensions.railTop],
    ]
    const rings = levels.map(([width, height]) => outline(width, height))
    const clockwise = THREE.ShapeUtils.isClockWise(rings[0]!.map(point => new THREE.Vector2(point.x, point.z)))
    for (let i = 1; i < rings.length; i++) for (let edge = 0; edge < 6; edge++) {
        const next = (edge + 1) % 6
        const surface = i === rings.length - 1 ? tops : steel
        const a = rings[i - 1]![edge]!, b = rings[i - 1]![next]!, c = rings[i]![next]!, d = rings[i]![edge]!
        if (clockwise) surface.quad(a, b, c, d)
        else surface.quad(a, d, c, b)
    }
    for (const [ring, surface, up] of [[rings[0]!, steel, false], [rings[rings.length - 1]!, tops, true]] as const) {
        const faces = THREE.ShapeUtils.triangulateShape(ring.map(point => new THREE.Vector2(point.x, point.z)), [])
        for (const [a, b, c] of faces) {
            const vertices = [ring[a!]!, ring[b!]!, ring[c!]!]
            const upward = new THREE.Vector3().crossVectors(vertices[1]!.clone().sub(vertices[0]!), vertices[2]!.clone().sub(vertices[0]!)).y > 0
            if (upward === up) surface.triangle(vertices[0]!, vertices[1]!, vertices[2]!)
            else surface.triangle(vertices[2]!, vertices[1]!, vertices[0]!)
        }
    }
}

function buildTurnoutRails(turnout: Turnout, rails: Rail[], dimensions: Dimensions, steel: Surface, tops: Surface): TurnoutModel {
    const g = dimensions.gauge
    const at = Math.min(g * 2, turnout.main.length * 0.35, turnout.branch.length * 0.35)
    const a = coordinateAt(turnout.main, turnout, at)
    const b = coordinateAt(turnout.branch, turnout, at)
    const handedness = Math.sign((b?.z ?? 0) - (a?.z ?? 0)) || 1
    const model: TurnoutModel = { id: turnout.id, origin: turnout.origin.clone(), handedness,
        bladeLength: 0, blades: [], frog: null, nose: null, wings: [], guards: [], flangeway: g * 0.035 }
    const pick = (path: Path, side: number) => rails.find(rail => rail.path === path && rail.side * outwardSign(rail, turnout) === side)!
    // The two stock rails are on opposite routes; the two inner rails become the points.
    const mainStock = pick(turnout.main, -handedness)
    const branchStock = pick(turnout.branch, handedness)
    const mainPoint = pick(turnout.main, handedness)
    const branchPoint = pick(turnout.branch, -handedness)
    if (!mainStock || !branchStock || !mainPoint || !branchPoint) return model
    const hit = crossing(mainPoint, branchPoint, turnout.origin, g)
    const frogDistance = hit ? hit.point.clone().sub(turnout.origin).dot(turnout.direction) : turnout.fanLength
    model.bladeLength = Math.min(g * 4.2, frogDistance * 0.42, turnout.main.length * 0.36, turnout.branch.length * 0.36)
    const toe = Math.min(g * 0.28, model.bladeLength * 0.14)
    for (const [rail, stock, side, closed] of [[mainPoint, branchStock, handedness, true],
        [branchPoint, mainStock, -handedness, false]] as const) {
        const line = railPath(rail)
        const sign = outwardSign(rail, turnout)
        const originDistance = sign > 0 ? 0 : line.length
        const heelDistance = originDistance + sign * model.bladeLength
        cutRail(rail, originDistance, heelDistance)
        const start = sample(line, originDistance + sign * toe).point
        const stockLine = railPath(stock)
        const stockContact = nearest(stockLine, start)
        const stockDirection = sample(stockLine, stockContact.along).direction.multiplyScalar(outwardSign(stock, turnout))
        const inside = normal(stockDirection).multiplyScalar(-side)
        // One fine tip bears against its stock rail; the other has a visible opening.
        const tip = stockContact.point.clone().addScaledVector(inside, g * (0.02665 + (closed ? 0 : 0.09)))
        const tipShift = tip.sub(start)
        const points: THREE.Vector3[] = []
        const widths: number[] = []
        const count = Math.max(12, Math.ceil(model.bladeLength / (g * 0.09)))
        for (let index = 0; index <= count; index++) {
            const t = index / count
            const along = originDistance + sign * THREE.MathUtils.lerp(toe, model.bladeLength, t)
            points.push(sample(line, along).point.addScaledVector(tipShift, (1 - t) ** 2))
            widths.push(THREE.MathUtils.lerp(0.025, 1, Math.min(1, t * 1.18)))
        }
        addRailSurface(steel, tops, detailRail(points, widths), dimensions)
        model.blades.push({ points, widths, closed })
    }
    if (!hit) return model
    const signA = outwardSign(mainPoint, turnout), signB = outwardSign(branchPoint, turnout)
    const directionA = hit.aDirection.clone().multiplyScalar(signA)
    const directionB = hit.bDirection.clone().multiplyScalar(signB)
    const lineA = railPath(mainPoint), lineB = railPath(branchPoint)
    const beforeA = signA > 0 ? hit.firstDistance : lineA.length - hit.firstDistance
    const beforeB = signB > 0 ? hit.secondDistance : lineB.length - hit.secondDistance
    const afterA = signA > 0 ? lineA.length - hit.firstDistance : hit.firstDistance
    const afterB = signB > 0 ? lineB.length - hit.secondDistance : hit.secondDistance
    const sine = Math.abs(cross(directionA, directionB))
    const approach = Math.min(Math.max(g * 1.6, g * 0.18 / Math.max(0.035, sine)),
        beforeA - model.bladeLength - g * 0.1, beforeB - model.bladeLength - g * 0.1)
    const exit = Math.min(g * 2, afterA * 0.8, afterB * 0.8)
    if (approach < g * 0.3 || exit < g * 0.3) return model
    const startA = hit.firstDistance - signA * approach, startB = hit.secondDistance - signB * approach
    const endA = hit.firstDistance + signA * exit, endB = hit.secondDistance + signB * exit
    cutRail(mainPoint, startA, endA)
    cutRail(branchPoint, startB, endB)
    model.frog = hit.point.clone()
    model.nose = hit.point.clone().addScaledVector(directionA.clone().add(directionB).normalize(), g * 0.065)
    addCrossingNose(hit.point, model.nose, directionA, directionB, sample(lineA, endA).point,
        sample(lineB, endB).point, handedness, dimensions, steel, tops)

    // A wing stays connected to its approach rail, then bends along the OTHER outgoing arm.
    const clearance = g * 0.052 + model.flangeway
    for (const [incoming, startDistance, incomingDirection, outgoing, endDistance, outgoingDirection, outsideSign] of [
        [lineA, startA, directionA, lineB, endB, directionB, handedness],
        [lineB, startB, directionB, lineA, endA, directionA, -handedness],
    ] as const) {
        const outside = normal(outgoingDirection).multiplyScalar(outsideSign)
        const outgoingOffset = hit.point.clone().addScaledVector(outside, clearance)
        const corner = intersectLines(hit.point, incomingDirection, outgoingOffset, outgoingDirection)
        const start = sample(incoming, startDistance).point
        const end = sample(outgoing, endDistance).point.addScaledVector(outside, clearance)
        const radius = Math.min(g * 0.18, start.distanceTo(corner) * 0.3, end.distanceTo(corner) * 0.3)
        const pre = corner.clone().addScaledVector(incomingDirection, -radius)
        const post = corner.clone().addScaledVector(outgoingDirection, radius)
        const bend = new THREE.QuadraticBezierCurve3(pre, corner, post).getPoints(8)
        const flareStart = end.clone().addScaledVector(outgoingDirection, -Math.min(g * 0.35, exit * 0.2))
        const wing = [start, ...bend, flareStart, end.addScaledVector(outside, g * 0.045)]
        addRailSurface(steel, tops, detailRail(wing, wing.map(() => 1)), dimensions)
        model.wings.push(wing)
    }
    return model
}

/** Place check rails only after every turnout has cut its points and crossing from shared rails. */
function buildTurnoutGuards(turnout: Turnout, rails: Rail[], model: TurnoutModel,
    dimensions: Dimensions, steel: Surface, tops: Surface) {
    if (!model.frog) return
    const g = dimensions.gauge
    const pick = (path: Path, side: number) => rails.find(rail => rail.path === path && rail.side * outwardSign(rail, turnout) === side)
    for (const [path, side] of [[turnout.main, -model.handedness], [turnout.branch, model.handedness]] as const) {
        const stock = pick(path, side)
        if (!stock) continue
        const line = railPath(stock)
        const sign = outwardSign(stock, turnout)
        const center = nearest(line, model.frog).along
        const span = g * 1.75
        // A short crossover can put the other frog or a point blade inside this span.
        // Stay within the continuous stock-rail interval surrounding this crossing.
        const margin = g * 0.22
        let availableStart = 0
        let availableEnd = line.length
        let interrupted = false
        for (const cut of stock.cuts) {
            if (center >= cut.start - margin && center <= cut.end + margin) {
                interrupted = true
                break
            }
            if (cut.end < center) availableStart = Math.max(availableStart, cut.end + margin)
            if (cut.start > center) availableEnd = Math.min(availableEnd, cut.start - margin)
        }
        const start = Math.max(center - span, availableStart)
        const end = Math.min(center + span, availableEnd)
        // Keep a useful straight checking section on both sides of the crossing.
        if (interrupted || end - start < g * 0.9 || center - start < g * 0.35 || end - center < g * 0.35) continue
        const guard: THREE.Vector3[] = []
        for (let index = 0; index <= 24; index++) {
            const t = index / 24
            const distance = THREE.MathUtils.lerp(sign > 0 ? start : end, sign > 0 ? end : start, t)
            const at = sample(line, distance)
            const inward = normal(at.direction.multiplyScalar(sign)).multiplyScalar(-side)
            const flare = Math.max(0, (Math.abs(t - 0.5) - 0.34) / 0.16) ** 2 * g * 0.065
            guard.push(at.point.addScaledVector(inward, g * (0.026 + 0.026 * 0.78) + model.flangeway + flare))
        }
        const guardRail = detailRail(guard)
        addRailSurface(steel, tops, guardRail, dimensions)
        model.guards.push(guardRail.points)
    }
}

function instances(group: THREE.Group, geometry: THREE.BufferGeometry, material: THREE.Material, items: Instance[], name: string) {
    if (!items.length) { geometry.dispose(); return }
    const mesh = new THREE.InstancedMesh(geometry, material, items.length)
    const matrix = new THREE.Matrix4()
    const rotation = new THREE.Quaternion()
    const color = new THREE.Color()
    items.forEach((item, index) => {
        rotation.setFromAxisAngle(UP, -item.angle)
        matrix.compose(item.position, rotation, item.scale)
        mesh.setMatrixAt(index, matrix)
        if (item.shade !== undefined) mesh.setColorAt(index, color.setRGB(item.shade, item.shade * 0.99, item.shade * 0.96))
    })
    mesh.name = name
    mesh.castShadow = mesh.receiveShadow = true
    mesh.instanceMatrix.needsUpdate = true
    mesh.computeBoundingSphere()
    group.add(mesh)
}

function sleeperGeometry() {
    const geometry = new THREE.BoxGeometry(1, 1, 1)
    const positions = geometry.getAttribute('position')
    for (let i = 0; i < positions.count; i++) {
        if (positions.getY(i) > 0) positions.setXYZ(i, positions.getX(i) * 0.78, positions.getY(i), positions.getZ(i) * 0.96)
    }
    geometry.computeVertexNormals()
    return geometry
}

/** Creates a self-contained track model; the owner disposes its geometries, materials and maps. */
export function createRailway(source: RailwayPath[], switches: RailwaySwitch[], gauge: number): THREE.Group {
    const dimensions = getRailwayDimensions(gauge)
    const g = dimensions.gauge
    const group = new THREE.Group()
    group.name = 'railway-infrastructure'
    const paths = preparePaths(source, switches, g)
    if (!paths.length) return group
    const turnouts = findTurnouts(paths, switches, g)
    const ballast = new Surface()
    const steel = new Surface()
    const polished = new Surface()
    const rails = paths.flatMap(path => [-1, 1].map(side => offsetRail(path, side, dimensions)))
    const turnoutModels = turnouts.map(turnout => buildTurnoutRails(turnout, rails, dimensions, steel, polished))
    turnouts.forEach((turnout, index) => buildTurnoutGuards(turnout, rails, turnoutModels[index]!, dimensions, steel, polished))
    for (const rail of rails) addRailSurface(steel, polished, rail, dimensions)
    const sleepers: Instance[] = []
    const pads: Instance[] = []
    const clips: Instance[] = []
    const rods: Instance[] = []
    const motors: Instance[] = []
    const motorLids: Instance[] = []
    const bolts: Instance[] = []
    const slideChairs: Instance[] = []
    const foundations: Instance[] = []
    const fixings = new Set<string>()
    const tiePositions = new Set<string>()

    function addFixing(point: THREE.Vector3, direction: THREE.Vector3) {
        const key = `${Math.round(point.x / g * 30)},${Math.round(point.z / g * 30)}`
        if (fixings.has(key)) return
        fixings.add(key)
        const angle = Math.atan2(direction.z, direction.x)
        const pointArea = turnouts.some((turnout, index) => {
            const delta = point.clone().sub(turnout.origin)
            const x = delta.dot(turnout.direction)
            return x >= 0 && x <= turnoutModels[index]!.bladeLength && insideFan(point, turnout, g)
        })
        if (pointArea) {
            // Points move across flat slide chairs, without clips obstructing their travel.
            slideChairs.push({ position: point.clone().setY(dimensions.sleeperTop + g * 0.012), angle,
                scale: new THREE.Vector3(g * 0.21, g * 0.024, g * 0.3) })
            return
        }
        pads.push({ position: point.clone().setY(dimensions.sleeperTop + g * 0.012), angle, scale: new THREE.Vector3(g * 0.19, g * 0.024, g * 0.16) })
        const n = normal(direction)
        for (const side of [-1, 1]) {
            clips.push({ position: point.clone().addScaledVector(n, side * g * 0.069).setY(dimensions.railBase + g * 0.029), angle, scale: new THREE.Vector3(g * 0.04, g * 0.04, g * 0.04) })
        }
    }
    function addSleeper(center: THREE.Vector3, direction: THREE.Vector3, length: number, railPoints: { point: THREE.Vector3; direction: THREE.Vector3 }[]) {
        const key = `${Math.round(center.x / g * 6)},${Math.round(center.z / g * 6)}`
        if (tiePositions.has(key)) return
        tiePositions.add(key)
        sleepers.push({ position: center.clone().setY(dimensions.sleeperTop - dimensions.sleeperHeight / 2), angle: Math.atan2(direction.z, direction.x), scale: new THREE.Vector3(g * 0.18, dimensions.sleeperHeight, length), shade: 0.80 + hash(center.x, center.z) * 0.19 })
        railPoints.forEach((point) => addFixing(point.point, point.direction))
    }

    for (const path of paths) {
        const count = Math.max(1, Math.ceil(path.length / (g * 0.55)))
        let run: THREE.Vector3[] = []
        const flush = () => {
            if (run.length > 1) addBed(ballast, run, run.map(() => g * 2.3), dimensions)
            run = []
        }
        for (let i = 0; i <= count; i++) {
            const point = sample(path, i / count * path.length).point
            if (turnouts.some((turnout) => insideFan(point, turnout, g))) { flush(); continue }
            run.push(point)
        }
        flush()
        const tieCount = Math.max(1, Math.floor(path.length / dimensions.sleeperSpacing))
        for (let i = 0; i < tieCount; i++) {
            const at = sample(path, (i + 0.5) / tieCount * path.length)
            if (turnouts.some((turnout) => insideFan(at.point, turnout, g))) continue
            const n = normal(at.direction)
            addSleeper(at.point, at.direction, dimensions.sleeperLength, [-1, 1].map((side) => ({ point: at.point.clone().addScaledVector(n, side * g / 2), direction: at.direction })))
        }
    }

    for (const turnout of turnouts) {
        const bedPoints: THREE.Vector3[] = []
        const bedWidths: number[] = []
        for (let x = -g * 0.7; x <= turnout.fanLength + g * 0.9; x += g * 0.2) {
            const at = clamp(x, 0, turnout.fanLength)
            const a = coordinateAt(turnout.main, turnout, at)
            const b = coordinateAt(turnout.branch, turnout, at)
            if (!a || !b) continue
            bedPoints.push(turnout.origin.clone().addScaledVector(turnout.direction, x).addScaledVector(turnout.normal, (a.z + b.z) / 2))
            bedWidths.push(g * 2.3 + Math.abs(a.z - b.z))
        }
        addBed(ballast, bedPoints, bedWidths, dimensions)
        for (let x = 0; x <= turnout.fanLength + g * 0.05; x += dimensions.sleeperSpacing) {
            const a = coordinateAt(turnout.main, turnout, x)
            const b = coordinateAt(turnout.branch, turnout, x)
            if (!a || !b) continue
            const center = turnout.origin.clone().addScaledVector(turnout.direction, x).addScaledVector(turnout.normal, (a.z + b.z) / 2)
            const railPoints: { point: THREE.Vector3; direction: THREE.Vector3 }[] = []
            for (const route of [a, b]) for (const side of [-1, 1]) {
                // Intersect each offset rail with the common sleeper plane.
                // A shared path can be stored backwards after preparing the other turnout.
                // Its transverse rail spacing depends on the absolute crossing angle.
                const divisor = Math.max(0.3, Math.abs(route.direction.dot(turnout.direction)))
                const z = route.z + side * g / 2 / divisor
                railPoints.push({ point: turnout.origin.clone().addScaledVector(turnout.direction, x).addScaledVector(turnout.normal, z), direction: route.direction })
            }
            addSleeper(center, turnout.direction, dimensions.sleeperLength + Math.abs(a.z - b.z), railPoints)
        }
        // Point rodding and a weatherproof electric point machine beside the toe.
        const angle = Math.atan2(turnout.direction.z, turnout.direction.x)
        for (const x of [g * 0.67, g * 1.35]) {
            rods.push({ position: turnout.origin.clone().addScaledVector(turnout.direction, x).setY(dimensions.railBase + g * 0.025), angle, scale: new THREE.Vector3(g * 0.045, g * 0.038, g * 1.37) })
        }
        const motor = turnout.origin.clone().addScaledVector(turnout.direction, g * 0.67).addScaledVector(turnout.normal, -g * 1.27)
        foundations.push({ position: motor.clone().setY(dimensions.sleeperTop / 2), angle,
            scale: new THREE.Vector3(g * 0.82, dimensions.sleeperTop, g * 0.58) })
        motors.push({ position: motor.clone().setY(dimensions.sleeperTop + g * 0.12), angle, scale: new THREE.Vector3(g * 0.61, g * 0.25, g * 0.38) })
        motorLids.push({ position: motor.clone().setY(dimensions.sleeperTop + g * 0.253), angle, scale: new THREE.Vector3(g * 0.65, g * 0.026, g * 0.42) })
        rods.push({ position: motor.clone().addScaledVector(turnout.normal, g * 0.42).setY(dimensions.sleeperTop + g * 0.05), angle, scale: new THREE.Vector3(g * 0.045, g * 0.055, g * 0.9) })
        for (const x of [-0.23, 0.23]) for (const z of [-0.13, 0.13]) {
            bolts.push({ position: motor.clone().addScaledVector(turnout.direction, x * g).addScaledVector(turnout.normal, z * g).setY(dimensions.sleeperTop + g * 0.275), angle, scale: new THREE.Vector3(g * 0.038, g * 0.02, g * 0.038) })
        }
    }

    const textures = stoneTextures(g)
    const ballastMaterial = new THREE.MeshStandardMaterial({ color: 0xb5b1a5, roughness: 1, metalness: 0, ...textures, normalScale: new THREE.Vector2(0.55, 0.55) })
    const sideMaterial = new THREE.MeshStandardMaterial({ color: 0x65584a, roughness: 0.72, metalness: 0.55 })
    const topMaterial = new THREE.MeshStandardMaterial({ color: 0xc6cdd0, roughness: 0.24, metalness: 0.83 })
    const concreteMaterial = new THREE.MeshStandardMaterial({ color: 0xb9bbb2, roughness: 0.95 })
    const padMaterial = new THREE.MeshStandardMaterial({ color: 0x373b36, roughness: 0.9 })
    const clipMaterial = new THREE.MeshStandardMaterial({ color: 0x64554b, roughness: 0.56, metalness: 0.62 })
    const motorMaterial = new THREE.MeshStandardMaterial({ color: 0x596967, roughness: 0.65, metalness: 0.35 })
    group.add(ballast.mesh(ballastMaterial, 'graded-stone-ballast', false), steel.mesh(sideMaterial, 'rail-feet-webs-and-turnout-castings'), polished.mesh(topMaterial, 'polished-rail-running-surfaces'))
    instances(group, sleeperGeometry(), concreteMaterial, sleepers, 'prestressed-concrete-sleepers')
    instances(group, new THREE.BoxGeometry(1, 1, 1), padMaterial, pads, 'rail-seat-pads')
    instances(group, new THREE.BoxGeometry(1, 1, 1), sideMaterial, slideChairs, 'switch-slide-chairs')
    const clipGeometry = new THREE.TorusGeometry(1, 0.24, 4, 9, Math.PI * 1.65)
    clipGeometry.rotateX(Math.PI / 2)
    instances(group, clipGeometry, clipMaterial, clips, 'elastic-rail-clips')
    instances(group, new THREE.BoxGeometry(1, 1, 1), sideMaterial, rods, 'turnout-stretcher-bars-and-drive-rods')
    instances(group, new THREE.BoxGeometry(1, 1, 1), motorMaterial, motors, 'electric-point-machines')
    instances(group, new THREE.BoxGeometry(1, 1, 1), motorMaterial, motorLids, 'point-machine-lids')
    instances(group, new THREE.BoxGeometry(1, 1, 1), concreteMaterial, foundations, 'point-machine-foundations')
    instances(group, new THREE.CylinderGeometry(0.5, 0.5, 1, 6), topMaterial, bolts, 'point-machine-lid-bolts')
    group.userData.railway = {
        pathCount: paths.length, turnoutCount: turnouts.length, frogCount: turnoutModels.filter(turnout => turnout.frog).length,
        sleeperCount: sleepers.length, railTop: dimensions.railTop, turnouts: turnoutModels,
        paths: paths.map((path) => ({ id: path.id, points: path.points.map((point) => point.clone()) })),
    }
    return group
}
