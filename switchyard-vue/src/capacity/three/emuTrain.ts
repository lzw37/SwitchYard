import * as THREE from 'three'
import { createEmuMarking, type EmuMarking } from './emuMarkings.ts'
import { EMU_MODEL_PROFILES, type EmuModelId } from './emuProfiles.ts'
import { mergeGeometries } from 'three/examples/jsm/utils/BufferGeometryUtils.js'

export type EmuCarRole = 'head' | 'middle' | 'tail'

/** Meter proportions used before normalizing the model for the schematic layout. */
export const EMU_DIMENSIONS = {
    length: 25,
    width: 3.4,
    height: 4.6,
    railGauge: 1.435,
    roofHeight: 3.88,
} as const

/** Rear/front bogie pivot positions along the carriage, in physical meters. */
export function getEmuBogieOffsets(role: EmuCarRole, modelId: EmuModelId = 'CR400AF'): [number, number] {
    const front = modelId === 'CR200J' ? 8.0
        : modelId === 'CR400AF' ? 5.1
        : Math.min(8.25, Math.max(4.3, EMU_MODEL_PROFILES[modelId].nose[0]![0] + 2.6))
    if (role === 'head') return [-8.25, front]
    if (role === 'tail') return [-front, 8.25]
    return [-8.25, 8.25]
}

type Finish = 'pearl' | 'roof' | 'red' | 'accent' | 'glass' | 'reflection' | 'graphite' | 'rubber' | 'steel' | 'seam' | 'lamp' | 'marker'
type Point = [number, number, number]
type RibbonSection = [x: number, lower: number, upper: number]

/**
 * Procedural EMU fleet, with CR400AF as the backwards-compatible default.
 * Model-specific lofts, livery and equipment come from emuProfiles.ts, which
 * records the photographic references and the representative family variants.
 * Lettering is local vector geometry, with no image or font downloads. X is longitudinal, Z is across the
 * track; wheels touch Y=0. Head points +X, tail points -X. Its normalized bounds
 * fit X/Z ±0.5 and Y 0..1 (including a raised pantograph on selected middle cars).
 * Scale by (length, height, width). Each call owns its materials and geometries,
 * so normal scene traversal/disposal is safe. Static details merge by material.
 */
export function createEmuCar(role: EmuCarRole, carIndex: number, modelId: EmuModelId = 'CR400AF'): THREE.Group {
    const model = EMU_MODEL_PROFILES[modelId]
    const isOriginalAf = modelId === 'CR400AF'
    const isPowerCar = modelId === 'CR200J' && role === 'head'
    const group = new THREE.Group()
    group.name = `emu-${role}-${carIndex}`
    const isCab = role !== 'middle'
    const parts = new Map<Finish, THREE.BufferGeometry[]>()
    const bogieParts: { center: number; parts: Map<Finish, THREE.BufferGeometry[]> }[] = []
    let activeParts = parts
    const materials: Record<Finish, THREE.MeshStandardMaterial> = {
        pearl: new THREE.MeshStandardMaterial({ color: model.bodyColor, metalness: modelId === 'CR200J' ? 0.24 : 0.42, roughness: modelId === 'CR200J' ? 0.42 : 0.31 }),
        roof: new THREE.MeshStandardMaterial({ color: model.roofColor, metalness: 0.55, roughness: 0.36 }),
        red: new THREE.MeshStandardMaterial({ color: model.stripeColor, metalness: 0.22, roughness: 0.26 }),
        accent: new THREE.MeshStandardMaterial({ color: model.secondaryColor, metalness: 0.28, roughness: 0.3 }),
        glass: new THREE.MeshStandardMaterial({ color: 0x182a35, metalness: 0.56, roughness: 0.19, side: THREE.DoubleSide }),
        reflection: new THREE.MeshStandardMaterial({ color: 0x597581, metalness: 0.55, roughness: 0.2, side: THREE.DoubleSide }),
        graphite: new THREE.MeshStandardMaterial({ color: 0x2b3339, metalness: 0.48, roughness: 0.55 }),
        rubber: new THREE.MeshStandardMaterial({ color: 0x202629, metalness: 0.05, roughness: 0.91 }),
        steel: new THREE.MeshStandardMaterial({ color: 0x909da3, metalness: 0.84, roughness: 0.27 }),
        seam: new THREE.MeshStandardMaterial({ color: 0x778c98, metalness: 0.35, roughness: 0.56 }),
        lamp: new THREE.MeshStandardMaterial({ color: role === 'tail' ? 0xf74c48 : 0xfff4d3, emissive: role === 'tail' ? 0xd91b16 : 0xffdb95, emissiveIntensity: 1.8, roughness: 0.2 }),
        marker: new THREE.MeshStandardMaterial({ color: 0xecc388, emissive: 0xe8aa43, emissiveIntensity: 0.35, roughness: 0.5 }),
    }

    function add(finish: Finish, geometry: THREE.BufferGeometry) {
        const geometries = activeParts.get(finish) || []
        // All parts use a consistent attribute layout, including handmade lofts.
        geometry.deleteAttribute('uv')
        geometries.push(geometry)
        activeParts.set(finish, geometries)
    }

    function box(finish: Finish, size: Point, center: Point, rotationZ = 0) {
        const geometry = new THREE.BoxGeometry(...size)
        if (rotationZ) geometry.rotateZ(rotationZ)
        geometry.translate(...center)
        add(finish, geometry)
    }

    function cylinder(finish: Finish, radius: number, length: number, center: Point, axis: 'x' | 'y' | 'z' = 'z', segments = 18) {
        const geometry = new THREE.CylinderGeometry(radius, radius, length, segments)
        if (axis === 'z') geometry.rotateX(Math.PI / 2)
        if (axis === 'x') geometry.rotateZ(Math.PI / 2)
        geometry.translate(...center)
        add(finish, geometry)
    }

    function rod(finish: Finish, from: Point, to: Point, radius: number) {
        const start = new THREE.Vector3(...from)
        const end = new THREE.Vector3(...to)
        const direction = end.clone().sub(start)
        const geometry = new THREE.CylinderGeometry(radius, radius, direction.length(), 8)
        geometry.applyQuaternion(new THREE.Quaternion().setFromUnitVectors(new THREE.Vector3(0, 1, 0), direction.normalize()))
        geometry.translate(...start.add(end).multiplyScalar(0.5).toArray())
        add(finish, geometry)
    }

    function roundedShape(width: number, height: number, radius: number) {
        const shape = new THREE.Shape()
        const left = -width / 2
        const right = width / 2
        const bottom = -height / 2
        const top = height / 2
        shape.moveTo(left + radius, bottom)
        shape.lineTo(right - radius, bottom)
        shape.quadraticCurveTo(right, bottom, right, bottom + radius)
        shape.lineTo(right, top - radius)
        shape.quadraticCurveTo(right, top, right - radius, top)
        shape.lineTo(left + radius, top)
        shape.quadraticCurveTo(left, top, left, top - radius)
        shape.lineTo(left, bottom + radius)
        shape.quadraticCurveTo(left, bottom, left + radius, bottom)
        return shape
    }

    function panel(finish: Finish, width: number, height: number, radius: number, center: Point) {
        const geometry = new THREE.ShapeGeometry(roundedShape(width, height, radius), 5)
        // ShapeGeometry faces +Z. Flip the back face to keep sunlight consistent.
        if (center[2] < 0) geometry.rotateY(Math.PI)
        geometry.translate(...center)
        // Follow the inward-curving shoulder and skirt, including door corners.
        // This avoids glass/door planes visibly floating above the curved shell.
        const positions = geometry.getAttribute('position')
        const side = Math.sign(center[2])
        const offset = Math.max(0.006, (Math.abs(center[2]) - 1.66) * 0.7)
        for (let index = 0; index < positions.count; index++) {
            const x = positions.getX(index)
            const y = positions.getY(index)
            positions.setZ(index, side * (sideSurfaceZ(x, y) + offset))
        }
        geometry.computeVertexNormals()
        add(finish, geometry)
    }

    // Continuous rounded shell profile; straight flanks turn into arched roof
    // shoulders and inward-curving lower skirts instead of a scaled box.
    const section = new THREE.Shape()
    section.moveTo(0, 0.96)
    section.bezierCurveTo(-0.6, 0.96, -1.43, 0.92, -1.56, 1.17)
    section.bezierCurveTo(-1.68, 1.4, -1.68, 2.45, -1.65, 2.83)
    section.bezierCurveTo(-1.62, 3.65, -0.97, 3.88, 0, 3.88)
    section.bezierCurveTo(0.97, 3.88, 1.62, 3.65, 1.65, 2.83)
    section.bezierCurveTo(1.68, 2.45, 1.68, 1.4, 1.56, 1.17)
    section.bezierCurveTo(1.43, 0.92, 0.6, 0.96, 0, 0.96)
    const profile = section.getSpacedPoints(96).map(point => new THREE.Vector2(point.x * 0.985, point.y))

    // Monotone loft sections give the AF its full shoulders, concave cheeks and
    // rounded coupling hood. The nose closes above the rail instead of becoming
    // the needle-like wedge of the previous generic EMU.
    // The CR200J control trailer has a lower, more tapered cab than its powered end.
    const noseSections = modelId === 'CR200J' && role === 'tail'
        ? [[6.8, 1, 0.96, 3.88], [8.4, 0.97, 0.96, 3.86], [9.25, 0.915, 0.96, 3.71],
            [10.0, 0.83, 0.96, 3.30], [10.7, 0.715, 0.97, 2.72], [11.3, 0.605, 0.98, 2.25],
            [11.8, 0.525, 0.985, 1.92], [12.12, 0.465, 0.99, 1.78]] as const
        : model.nose
    const noseStart = noseSections[0]![0]
    const noseEnd = noseSections[noseSections.length - 1]![0]
    const noseTip = 12.42
    const cabRoofEnd = isOriginalAf ? 3 : Math.max(-0.5, noseStart - 0.4)
    const noseOval = isCab && model.noseOval
        ? { ...model.noseOval, startX: model.noseOval.startX - (role === 'tail' ? 0.5 : 0) }
        : undefined

    function ovalEnvelope(x: number) {
        if (!noseOval || x <= noseOval.startX) return { bottom: 0.96, top: 3.88 }
        if (x >= noseTip) return { bottom: noseOval.tipY, top: noseOval.tipY }
        // A sheared ellipse starts tangent to the forehead and bends continuously
        // downward to the tip. Its second derivative is strictly negative: no
        // flattened shelf followed by a separate, upward-looking spherical hood.
        const q0 = noseOval.skew
        const a = (noseTip - noseOval.startX) / (1 - q0)
        const cx = noseTip - a
        const root = Math.sqrt(1 - q0 * q0)
        const b = (3.88 - noseOval.tipY) * root / (1 - q0)
        const tilt = b * q0 / (a * root)
        const q = (x - cx) / a
        const top = noseOval.tipY + tilt * (x - noseTip) + b * Math.sqrt(Math.max(0, 1 - q * q))
        const progress = (x - noseOval.startX) / (noseTip - noseOval.startX)
        const bottom = 0.96 + (noseOval.tipY - 0.96) * (1 - Math.sqrt(Math.max(0, 1 - progress * progress)))
        return { bottom, top }
    }

    function sampleSection(x: number) {
        if (!isCab || x <= noseStart) return { width: 1, bottom: 0.96, top: 3.88, cove: 0 }
        const nextSection = noseSections.findIndex((_, i) => i < noseSections.length - 1 && x <= noseSections[i + 1]![0])
        const index = nextSection < 0 ? noseSections.length - 2 : nextSection
        const a = noseSections[index]!
        const b = noseSections[Math.min(index + 1, noseSections.length - 1)]!
        const t = THREE.MathUtils.clamp((x - a[0]) / (b[0] - a[0]), 0, 1)
        // Cubic Hermite interpolation with monotone slopes preserves shoulder
        // continuity without overshoot at the low, blunt nose tip.
        const interpolate = (column: 1 | 2 | 3) => {
            const previous = noseSections[Math.max(0, index - 1)]!
            const next = noseSections[Math.min(noseSections.length - 1, index + 2)]!
            const span = b[0] - a[0]
            const secant = (b[column] - a[column]) / span
            const slopeA = index === 0 ? 0 : (b[column] - previous[column]) / (b[0] - previous[0])
            const slopeB = (next[column] - a[column]) / (next[0] - a[0])
            const limit = (slope: number) => secant * slope <= 0 ? 0 : Math.sign(secant) * Math.min(Math.abs(slope), 3 * Math.abs(secant))
            return (2 * t ** 3 - 3 * t ** 2 + 1) * a[column]
                + (t ** 3 - 2 * t ** 2 + t) * span * limit(slopeA)
                + (-2 * t ** 3 + 3 * t ** 2) * b[column]
                + (t ** 3 - t ** 2) * span * limit(slopeB)
        }
        let width = interpolate(1)
        if (noseOval && x > noseEnd) {
            // The end-cap topology continues the same envelope. Match the
            // transverse tangent too, rather than starting a new half-ellipse.
            const last = noseSections[noseSections.length - 1]!
            const previous = noseSections[noseSections.length - 2]!
            const span = noseTip - noseEnd
            const progress = THREE.MathUtils.clamp((x - noseEnd) / span, 0, 1)
            const slope = (last[1] - previous[1]) / (last[0] - previous[0])
            const alpha = span * slope / last[1]
            width *= Math.sqrt(Math.max(0, 1 - progress * progress)) * (1 + alpha * progress * (1 - progress))
        }
        const envelope = noseOval ? ovalEnvelope(x) : { bottom: interpolate(2), top: interpolate(3) }
        return { width, ...envelope,
            cove: model.noseCove * Math.sin(THREE.MathUtils.clamp((x - noseStart) / (noseEnd - noseStart), 0, 1) * Math.PI) ** 2 }
    }

    function roundedNoseProfile(x: number, z: number, y: number) {
        // The passenger body has upright flanks, but its coupling hood is oval.
        // Blend the corners away before the end cap instead of extruding a tiny
        // rounded rectangle at the front of every train.
        const t = isCab ? THREE.MathUtils.smoothstep(x, noseEnd - 2.7, noseEnd) : 0
        const blend = t * (modelId === 'CRH1' ? 0.45 : modelId === 'CR200J' ? 0.6 : modelId === 'CRH6' ? 0.7 : 1)
        const ny = (y - 2.42) / 1.46
        const nz = z / (1.68 * 0.985)
        const radius = Math.hypot(ny, nz)
        const scale = radius > 1e-6 ? THREE.MathUtils.lerp(1, 1 / radius, blend) : 1
        // The sampled Bézier roof centre can retain ~1e-15 m of transverse
        // error. Keep the symmetry line exact so triangles on either side meet
        // on the same edge all the way into the cap's shared pole.
        const transverse = z * scale
        return new THREE.Vector2(Math.abs(transverse) < 1e-12 ? 0 : transverse, 2.42 + (y - 2.42) * scale)
    }

    function surface(x: number, u: number, offset = 0): THREE.Vector3 {
        const index = THREE.MathUtils.clamp(u, 0, 1) * (profile.length - 1)
        const i = Math.floor(index)
        const a = profile[i]!
        const b = profile[Math.min(i + 1, profile.length - 1)]!
        const p = roundedNoseProfile(x, THREE.MathUtils.lerp(a.x, b.x, index - i), THREE.MathUtils.lerp(a.y, b.y, index - i))
        const z = p.x, y = p.y
        const ring = sampleSection(x)
        const normalizedY = (y - 0.96) / (3.88 - 0.96)
        const outward = new THREE.Vector2(z, (y - 2.42) * 0.7).normalize()
        return new THREE.Vector3(x, ring.bottom + normalizedY * (ring.top - ring.bottom) + outward.y * offset, Math.sign(z) * Math.max(0, Math.abs(z) * ring.width - ring.cove * Math.exp(-1 * ((normalizedY - 0.56) / 0.12) ** 2)) + outward.x * offset)
    }

    function sideSurfaceZ(x: number, y: number) {
        const ring = sampleSection(x)
        const profileY = 0.96 + (y - ring.bottom) * (3.88 - 0.96) / (ring.top - ring.bottom)
        let width = 0
        for (let index = 1; index < profile.length; index++) {
            const a = roundedNoseProfile(x, profile[index - 1]!.x, profile[index - 1]!.y)
            const b = roundedNoseProfile(x, profile[index]!.x, profile[index]!.y)
            if (profileY < Math.min(a.y, b.y) || profileY > Math.max(a.y, b.y)) continue
            const t = Math.abs(b.y - a.y) < 1e-6 ? 0 : (profileY - a.y) / (b.y - a.y)
            width = Math.max(width, Math.abs(THREE.MathUtils.lerp(a.x, b.x, t)))
        }
        const normalizedY = (y - ring.bottom) / (ring.top - ring.bottom)
        return Math.max(0, width * ring.width - ring.cove * Math.exp(-1 * ((normalizedY - 0.56) / 0.12) ** 2))
    }

    function patch(finish: Finish, xStart: number, xEnd: number, uStart: number, uEnd: number, xSegments: number, uSegments: number, offset = 0, taper = 0) {
        const vertices: number[] = []
        const indices: number[] = []
        for (let ix = 0; ix <= xSegments; ix++) {
            const t = ix / xSegments
            const x = THREE.MathUtils.lerp(xStart, xEnd, t)
            const inset = Math.pow(Math.abs(t - 0.5) * 2, 8) * taper
            for (let iu = 0; iu <= uSegments; iu++) {
                const u = THREE.MathUtils.lerp(uStart + inset, uEnd - inset, iu / uSegments)
                vertices.push(...surface(x, u, offset).toArray())
                if (ix < xSegments && iu < uSegments) {
                    const a = ix * (uSegments + 1) + iu
                    const b = a + uSegments + 1
                    indices.push(a, a + 1, b, a + 1, b + 1, b)
                }
            }
        }
        const geometry = new THREE.BufferGeometry()
        geometry.setAttribute('position', new THREE.Float32BufferAttribute(vertices, 3))
        geometry.setIndex(indices)
        geometry.computeVertexNormals()
        add(finish, geometry)
        return geometry
    }

    function ribbon(finish: Finish, rows: RibbonSection[], project: (x: number, transverse: number) => THREE.Vector3, flip: boolean) {
        const positions: number[] = []
        const indices: number[] = []
        // CR200J's steep, broad front needs enough transverse samples for its
        // paint and glazing to stay outside the curved shell between vertices.
        const across = modelId === 'CR200J' && isCab ? 24 : 8
        for (let segment = 0; segment < rows.length - 1; segment++) {
            const first = rows[segment]!
            const last = rows[segment + 1]!
            const count = Math.max(1, Math.ceil((last[0] - first[0]) / 0.22))
            const base = positions.length / 3
            for (let step = 0; step <= count; step++) {
                const t = step / count
                const x = THREE.MathUtils.lerp(first[0], last[0], t)
                const lower = THREE.MathUtils.lerp(first[1], last[1], t)
                const upper = THREE.MathUtils.lerp(first[2], last[2], t)
                for (let j = 0; j <= across; j++) {
                    positions.push(...project(x, THREE.MathUtils.lerp(lower, upper, j / across)).toArray())
                    if (step === count || j === across) continue
                    const a = base + step * (across + 1) + j
                    const b = a + across + 1
                    if (flip) indices.push(a, a + 1, b, a + 1, b + 1, b)
                    else indices.push(a, b, a + 1, a + 1, b, b + 1)
                }
            }
        }
        const geometry = new THREE.BufferGeometry()
        geometry.setAttribute('position', new THREE.Float32BufferAttribute(positions, 3))
        geometry.setIndex(indices)
        geometry.computeVertexNormals()
        add(finish, geometry)
    }

    function sideRibbon(finish: Finish, rows: RibbonSection[], side: number, offset: number) {
        ribbon(finish, rows, (x, y) => new THREE.Vector3(x, y, side * (sideSurfaceZ(x, y) + offset)), side < 0)
    }

    function canopyRibbon(finish: Finish, rows: RibbonSection[], offset: number) {
        ribbon(finish, rows, (x, u) => surface(x, u, offset), true)
    }

    function lettering(text: EmuMarking, x: number, y: number, height: number, side: number) {
        const geometry = createEmuMarking(text, height)
        if (side < 0) geometry.rotateY(Math.PI)
        geometry.translate(x, y, 0)
        const positions = geometry.getAttribute('position')
        for (let i = 0; i < positions.count; i++)
            positions.setZ(i, side * (sideSurfaceZ(positions.getX(i), positions.getY(i)) + 0.025))
        geometry.computeVertexNormals()
        add('graphite', geometry)
    }

    const shell = patch('pearl', -12.13, isCab ? noseEnd : 12.13, 0, 1, isCab ? 144 : 4, 96)
    if (isCab) {
        // The actual trains terminate in a substantial curved coupling cover.
        // Keep the broad final section and close it with a rounded dome; reducing
        // the whole section to a point produces an unrealistically needle-like cab.
        const endSection = sampleSection(noseEnd)
        const centerY = (endSection.bottom + endSection.top) / 2
        const rows = 16, columns = 96
        const vertices: number[] = []
        const indices: number[] = []
        const capPoint = (angle: number, u: number, offset = 0) => {
            if (angle >= Math.PI / 2 - 1e-10) return new THREE.Vector3(noseTip + offset, noseOval?.tipY ?? centerY, 0)
            if (noseOval) {
                const point = surface(noseEnd + (noseTip - noseEnd) * Math.sin(angle), u)
                point.x += offset
                return point
            }
            const rim = surface(noseEnd, u)
            const radius = Math.cos(angle)
            return new THREE.Vector3(noseEnd + (noseTip - noseEnd) * Math.sin(angle) + offset,
                centerY + (rim.y - centerY) * radius, rim.z * radius)
        }
        for (let row = 0; row <= rows; row++) {
            for (let column = 0; column <= columns; column++) {
                vertices.push(...capPoint(row / rows * Math.PI / 2, column / columns).toArray())
                if (row < rows && column < columns) {
                    const a = row * (columns + 1) + column, b = a + columns + 1
                    indices.push(a, a + 1, b)
                    if (row < rows - 1) indices.push(a + 1, b + 1, b)
                }
            }
        }
        const cap = new THREE.BufferGeometry()
        cap.setAttribute('position', new THREE.Float32BufferAttribute(vertices, 3))
        cap.setIndex(indices)
        cap.computeVertexNormals()
        // Share the edge normals so the separately built dome has no bright
        // crease where it joins the loft. The pole has one outward normal.
        const shellNormals = shell.getAttribute('normal')
        const capNormals = cap.getAttribute('normal')
        for (let column = 0; column <= columns; column++) {
            const shellIndex = 144 * (columns + 1) + column
            const normal = new THREE.Vector3().fromBufferAttribute(shellNormals, shellIndex)
                .add(new THREE.Vector3().fromBufferAttribute(capNormals, column)).normalize()
            shellNormals.setXYZ(shellIndex, normal.x, normal.y, normal.z)
            capNormals.setXYZ(column, normal.x, normal.y, normal.z)
            capNormals.setXYZ(rows * (columns + 1) + column, 1, 0, 0)
        }
        add('pearl', cap)
        // Split line of the two coupling-cover leaves follows the curved cap.
        for (const u of [0, 0.5]) for (let step = 0; step < 16; step++) {
            rod('seam', capPoint(step / 16 * Math.PI / 2, u, 0.009).toArray() as Point,
                capPoint((step + 1) / 16 * Math.PI / 2, u, 0.009).toArray() as Point, 0.009)
        }
    }
    // End cap has the same curved profile as the shell.
    for (const x of isCab ? [-12.13] : [-12.13, 12.13]) {
        const geometry = new THREE.ShapeGeometry(section, 8)
        const positions = geometry.getAttribute('position')
        for (let index = 0; index < positions.count; index++) {
            const z = positions.getX(index) * 0.985
            const y = positions.getY(index)
            positions.setXYZ(index, x, y, x < 0 ? z : -z)
        }
        geometry.computeVertexNormals()
        add('pearl', geometry)
    }

    // Silver body, graphite roof/skirt and the standard AF black window ribbon.
    patch('roof', -11.75, isCab ? cabRoofEnd : 11.75, 0.43, 0.57, 8, 16, 0.009)
    const skirtEnd = isOriginalAf ? 4.5 : Math.min(10.9, noseStart + 1.5)
    patch('graphite', -11.9, isCab ? skirtEnd : 11.9, 0.02, 0.14, 8, 10, 0.012)
    patch('graphite', -11.9, isCab ? skirtEnd : 11.9, 0.86, 0.98, 8, 10, 0.012)
    patch('steel', -11.98, isCab ? skirtEnd : 11.98, 0.145, 0.149, 8, 1, 0.02)
    patch('steel', -11.98, isCab ? skirtEnd : 11.98, 0.851, 0.855, 8, 1, 0.02)
    for (const side of isOriginalAf ? [-1, 1] : []) {
        const redBelt: RibbonSection[] = isCab
            ? [[-12.14, 2.025, 2.155], [0.7, 2.025, 2.155], [2.4, 2.01, 3.10],
                [4.1, 1.94, 2.94], [6.3, 1.83, 2.53], [8.75, 1.80, 1.81]]
            : [[-12.14, 2.025, 2.155], [12.14, 2.025, 2.155]]
        sideRibbon('red', redBelt, side, 0.017)
        const windowBelt: RibbonSection[] = isCab
            ? [[-12.14, 2.205, 3.055], [2.1, 2.205, 3.055], [3.7, 2.19, 2.95],
                [5.35, 2.14, 2.63], [6.60, 2.11, 2.12]]
            : [[-12.14, 2.205, 3.055], [12.14, 2.205, 3.055]]
        sideRibbon('pearl', windowBelt.map(([x, low, high]) => [x, low - 0.045, high + 0.035]), side, 0.026)
        sideRibbon('rubber', windowBelt, side, 0.032)
    }

    if (!isOriginalAf) {
        for (const side of [-1, 1]) {
            const end = isCab ? Math.min(noseStart + 0.25, 9.5) : 12.14
            // Waist stripes follow the model-specific shell; two-line blue, gold,
            // jade and arrow liveries remain recognisable from elevated views.
            const low = model.livery === 'metro' ? 3.16 : model.livery === 'jade' ? 2.0 : 2.04
            const high = model.livery === 'metro' ? 3.23 : model.livery === 'jade' ? 2.14 : model.livery === 'bf' ? 2.28 : 2.18
            sideRibbon('red', [[-12.14, low, high], [end, low, high]], side, 0.021)
            if (model.livery !== 'metro' && model.livery !== 'jade') sideRibbon('accent', [[-12.14, 1.89, 1.94], [end, 1.89, 1.94]], side, 0.022)
            if (model.windowBand) {
                sideRibbon('rubber', [[-12.14, 2.23, 3.07], [end, 2.23, 3.07]], side, 0.028)
            }
            if (isCab) {
                const stripeEnd = modelId === 'CR450' ? 5.2 : modelId === 'CR400BF' ? 8.9
                    : modelId === 'CRH2' || modelId === 'CRH380A' ? end + 1.65
                    : modelId === 'CRH6' ? 10.4 : 10.9
                const mid = (end + stripeEnd) / 2
                const at = (x: number, fraction: number) => {
                    const ring = sampleSection(x)
                    return ring.bottom + (ring.top - ring.bottom) * fraction
                }
                if (modelId === 'CR200J') {
                    // The yellow belt turns upward alongside the cab window;
                    // it does not run down onto the coupling cover.
                    const shift = role === 'tail' ? -0.4 : 0
                    sideRibbon('rubber', [[end, 2.23, 3.07], [9.15 + shift, 2.20, 2.86],
                        [10.18 + shift, 1.96, 2.01]], side, 0.030)
                    sideRibbon('red', [[end, low, high], [9.12 + shift, 1.82, 2.02],
                        [10.3 + shift, 2.57, 2.59]], side, 0.040)
                } else if (model.livery === 'metro') {
                    sideRibbon('red', [[end, low, high], [mid, 3.10, 3.17], [stripeEnd, 2.52, 2.55]], side, 0.031)
                } else if (modelId === 'CR450') {
                    sideRibbon('red', [[end, low, high], [end + 0.85, 2.0, 3.10],
                        [3.5, 1.90, 2.48], [stripeEnd, 1.81, 1.83]], side, 0.031)
                } else if (modelId === 'CR400BF') {
                    // Gold frame around the low teardrop side window.
                    sideRibbon('red', [[end, low, 3.16], [end + 1.0, 2.04, 3.20],
                        [6.35, 1.98, 2.94], [stripeEnd, 1.91, 1.93]], side, 0.032)
                } else {
                    sideRibbon('red', [[end, low, high], [mid, at(mid, 0.39), at(mid, 0.44)],
                        [stripeEnd, at(stripeEnd, 0.43), at(stripeEnd, 0.445)]], side, 0.027)
                    if (modelId === 'CRH3C' || modelId === 'CRH380B') {
                        sideRibbon('red', [[mid - 0.45, at(mid - 0.45, 0.65), at(mid - 0.45, 0.665)],
                            [stripeEnd, at(stripeEnd, 0.43), at(stripeEnd, 0.445)]], side, 0.029)
                    }
                }
            }
            if (model.roofStyle === 'ribbed') {
                for (let row = 0; row < 4; row++) {
                    sideRibbon('steel', [[-11.95, 1.30 + row * 0.095, 1.314 + row * 0.095],
                        [isCab ? noseStart : 11.95, 1.30 + row * 0.095, 1.314 + row * 0.095]], side, 0.015)
                }
            }
        }
    }

    const doorXs = isOriginalAf ? isCab ? [-10.5, 1.9] : [-10.5, 10.5]
        : isPowerCar ? [-9.7, 7.0] : isCab ? model.cabDoorXs : model.doorXs
    const doorWidth = model.livery === 'metro' || modelId === 'CRH1' ? 1.45 : 1.01
    const genericWindowXs: number[] = []
    if (!isOriginalAf && !isPowerCar) {
        const windowEnd = isCab ? noseStart - 0.8 : 9.2
        for (let x = -8.6; x <= windowEnd; x += model.windowPitch) {
            if (doorXs.every(door => Math.abs(x - door) > (model.windowWidth + doorWidth) / 2 + 0.16)) genericWindowXs.push(x)
        }
    }
    const windowXs = !isOriginalAf ? genericWindowXs : isCab
        ? [-8.45, -6.95, -5.45, -3.95, -2.45, -0.95, 0.55]
        : [-8.45, -6.91, -5.37, -3.83, -2.29, -0.75, 0.79, 2.33, 3.87, 5.41, 6.95, 8.49]
    for (const side of [-1, 1]) {
        for (const x of windowXs) {
            panel('rubber', model.windowWidth + 0.07, model.windowHeight + 0.07, 0.145, [x, 2.63, side * 1.718])
            panel('glass', model.windowWidth, model.windowHeight, 0.12, [x, 2.63, side * 1.73])
            // Sky reflection occupies only the top rim, preserving deep glazing.
            panel('reflection', isOriginalAf ? 0.92 : model.windowWidth * 0.83, 0.055, 0.025,
                [x - 0.035, 2.63 + model.windowHeight / 2 - 0.09, side * 1.734])
        }
        for (const x of doorXs) {
            panel('seam', doorWidth + 0.07, 2.15, 0.09, [x, 2.18, side * 1.674])
            panel('pearl', doorWidth, 2.08, 0.075, [x, 2.18, side * 1.681])
            if (model.windowBand) panel('rubber', doorWidth, 0.85, 0.025, [x, 2.63, side * 1.703])
            panel('rubber', 0.59, 0.82, 0.1, [x, 2.68, side * 1.711])
            panel('glass', 0.53, 0.75, 0.075, [x, 2.68, side * 1.722])
            if (model.livery !== 'metro') panel('red', doorWidth, 0.13, 0.005, [x, 2.09, side * 1.691])
            if (doorWidth > 1.3) panel('seam', 0.022, 1.92, 0.008, [x, 2.18, side * 1.744])
            box('steel', [0.16, 0.035, 0.028], [x + 0.31, 2.13, side * 1.68])
            cylinder('marker', 0.039, 0.022, [x - 0.35, 2.02, side * 1.687], 'z', 10)
            box('graphite', [1.16, 0.07, 0.18], [x, 1.1, side * 1.57])
            box('steel', [1.08, 0.028, 0.18], [x, 1.145, side * 1.57])
        }
        // Small destination displays and car-number plaques are geometric details;
        // no raster text, network fonts or extra canvas textures are needed.
        panel('glass', 0.86, 0.15, 0.025, [-9.0, 3.13, side * 1.653])
        panel('marker', 0.53, 0.018, 0.007, [-9.0, 3.13, side * 1.668])
        panel('graphite', 0.24, 0.18, 0.025, [-10.5, 3.36, side * 1.564])
        for (let mark = 0; mark < (Math.abs(carIndex) % 4) + 1; mark++) {
            panel('pearl', 0.018, 0.095, 0.004, [-10.57 + mark * 0.047, 3.36, side * 1.571])
        }
    }

    for (const side of [-1, 1]) {
        lettering(model.brand, isCab ? -5.8 : -4.4, 3.38, 0.29, side)
        lettering(model.variant as EmuMarking, isCab ? -4.5 : -3.7, 1.66, 0.13, side)
    }

    if (isCab && isOriginalAf) {
        // Independent high driver windows; passenger glazing stays one level
        // lower. Both are projected onto the loft instead of floating planes.
        for (const side of [-1, 1]) {
            sideRibbon('pearl', [[2.75, 3.26, 3.66], [3.25, 3.14, 3.64], [5.55, 3.18, 3.23]], side, 0.024)
            sideRibbon('glass', [[2.87, 3.29, 3.60], [3.30, 3.20, 3.58], [5.35, 3.20, 3.23]], side, 0.040)
        }
        // Graphite central mask with the distinctive red lower U-shaped rim.
        canopyRibbon('red', [[2.9, 0.49, 0.51], [3.4, 0.411, 0.589], [4.2, 0.351, 0.649], [6.5, 0.362, 0.638],
            [8.5, 0.399, 0.601], [9.65, 0.459, 0.541]], 0.028)
        canopyRibbon('graphite', [[2.8, 0.493, 0.507], [3.4, 0.421, 0.579], [4.2, 0.36, 0.64], [6.5, 0.372, 0.628],
            [8.4, 0.413, 0.587], [9.38, 0.467, 0.533]], 0.045)
        // One-piece curved front glass, gently narrowed at its four corners.
        patch('rubber', 4.45, 7.25, 0.379, 0.621, 34, 32, 0.052, 0.035)
        patch('glass', 4.55, 7.12, 0.389, 0.611, 34, 32, 0.066, 0.033)
        patch('reflection', 4.68, 4.78, 0.409, 0.584, 2, 22, 0.073, 0.013)
        const wiperBase = surface(7.00, 0.545, 0.08)
        const wiperElbow = surface(6.62, 0.510, 0.082)
        const wiperTip = surface(5.95, 0.477, 0.084)
        rod('graphite', wiperBase.toArray() as Point, wiperElbow.toArray() as Point, 0.023)
        rod('graphite', wiperElbow.toArray() as Point, wiperTip.toArray() as Point, 0.021)
        // Narrow swept lamp housings sit high on the cheeks, behind the nose cap.
        for (const side of [-1, 1]) {
            const mirror = (rows: RibbonSection[]) => rows.map(([x, a, b]) =>
                [x, side < 0 ? a : 1 - b, side < 0 ? b : 1 - a] as RibbonSection)
            canopyRibbon('steel', mirror([[6.12, 0.302, 0.306], [6.48, 0.286, 0.332],
                [7.56, 0.315, 0.351], [7.97, 0.347, 0.349]]), 0.057)
            canopyRibbon('rubber', mirror([[6.27, 0.302, 0.307], [6.58, 0.296, 0.326],
                [7.49, 0.323, 0.345], [7.82, 0.344, 0.346]]), 0.071)
            for (let lens = 0; lens < 3; lens++) {
                const x = 6.56 + lens * 0.36
                const u = 0.308 + lens * 0.009
                canopyRibbon('lamp', mirror([[x, u - 0.008, u + 0.009],
                    [x + 0.25, u - 0.002, u + 0.015]]), 0.083)
            }
        }
        // Fine, closed coupling-cover seam, with a silver hood inside it.
        patch('seam', 11.20, 11.226, 0, 1, 1, 96, 0.012)
        // Geometric gold railway crest on the graphite mask.
        patch('marker', 7.67, 7.82, 0.487, 0.513, 3, 8, 0.074, 0.005)
        for (const side of [-1, 1]) {
            const a = surface(7.93, 0.5 + side * 0.011, 0.075)
            const b = surface(8.18, 0.5 + side * 0.022, 0.075)
            rod('marker', a.toArray() as Point, b.toArray() as Point, 0.016)
        }
    }

    if (isCab && !isOriginalAf) {
        const [glassStart, glassEnd, uLow, uHigh] = modelId === 'CR200J' && role === 'tail'
            ? [9.2, 10.68, 0.38, 0.62] : model.cabGlass
        // Each cab's glazing is fitted to its own roof slope. No floating box
        // windscreen: the rubber gasket, glass and reflections share the loft.
        if (modelId === 'CR450') {
            canopyRibbon('red', [[noseStart + 0.2, 0.47, 0.53], [glassStart - 0.5, 0.35, 0.65],
                [glassEnd + 1.4, 0.355, 0.645], [noseEnd - 0.9, 0.43, 0.57]], 0.027)
            canopyRibbon('graphite', [[noseStart + 0.2, 0.48, 0.52], [glassStart - 0.45, 0.36, 0.64],
                [glassEnd + 1.3, 0.365, 0.635], [noseEnd - 1.05, 0.435, 0.565]], 0.041)
        } else if (modelId === 'CR400BF') {
            canopyRibbon('red', [[noseStart + 0.15, 0.46, 0.54], [glassStart - 0.6, 0.337, 0.663],
                [glassEnd + 0.8, 0.362, 0.638], [10.45, 0.456, 0.544]], 0.027)
            canopyRibbon('graphite', [[noseStart + 0.15, 0.47, 0.53], [glassStart - 0.55, 0.35, 0.65],
                [glassEnd + 0.75, 0.375, 0.625], [10.25, 0.46, 0.54]], 0.041)
        } else if (modelId === 'CRH1' || modelId === 'CRH6') {
            patch('graphite', glassStart - 0.5, modelId === 'CRH1' ? 12.03 : 11.65,
                uLow - 0.04, uHigh + 0.04, 46, 40, 0.027, 0.045)
        } else if (modelId === 'CR200J') {
            // Continuous dark forehead, steep face and lower V, as on the
            // first-generation green power car in the supplied photograph.
            canopyRibbon('graphite', [[noseStart + 0.15, 0.38, 0.62], [glassStart - 0.5, 0.325, 0.675],
                [glassStart + 0.2, 0.327, 0.673], [glassEnd + 0.10, 0.34, 0.66],
                [11.60, 0.395, 0.605], [11.96, 0.457, 0.543]], 0.032)
        } else if (modelId === 'CRH5') {
            patch('graphite', noseStart + 0.28, glassEnd + 0.75, uLow - 0.045, uHigh + 0.045, 46, 40, 0.027, 0.072)
        } else {
            patch('graphite', glassStart - 0.3, glassEnd + 0.48, uLow - 0.035, uHigh + 0.035, 28, 32, 0.027, 0.035)
        }
        if (modelId === 'CR200J') {
            canopyRibbon('rubber', [[glassStart - 0.06, uLow + 0.025, uHigh - 0.025],
                [glassStart + 0.12, uLow - 0.012, uHigh + 0.012],
                [glassEnd - 0.12, uLow, uHigh], [glassEnd + 0.06, uLow + 0.045, uHigh - 0.045]], 0.048)
            canopyRibbon('glass', [[glassStart, uLow + 0.025, uHigh - 0.025],
                [glassStart + 0.15, uLow, uHigh], [glassEnd - 0.15, uLow + 0.013, uHigh - 0.013],
                [glassEnd, uLow + 0.045, uHigh - 0.045]], 0.064)
            patch('reflection', glassStart + 0.12, glassStart + 0.17, uLow + 0.038, uHigh - 0.038, 2, 24, 0.074)
        } else {
            patch('rubber', glassStart - 0.065, glassEnd + 0.065, uLow - 0.012, uHigh + 0.012, 26, 28, 0.041, 0.026)
            patch('glass', glassStart, glassEnd, uLow, uHigh, 26, 28, 0.058, 0.028)
            patch('reflection', glassStart + 0.08, glassStart + 0.17, uLow + 0.035, uHigh - 0.035, 2, 20, 0.067)
        }
        if (['CRH1', 'CRH2'].includes(modelId)) {
            patch('rubber', glassStart, glassEnd, 0.496, 0.504, 24, 2, 0.074)
        }
        if (modelId === 'CRH380A') patch('pearl', glassStart, glassEnd, 0.494, 0.506, 24, 2, 0.074)
        for (const side of [-1, 1]) {
            const base = surface(glassEnd - 0.13, 0.5 + side * 0.065, 0.085)
            const elbow = surface(glassEnd - (glassEnd - glassStart) * 0.4, 0.5 + side * 0.035, 0.086)
            const tip = surface(glassStart + 0.28, 0.5 + side * 0.03, 0.086)
            rod('graphite', base.toArray() as Point, elbow.toArray() as Point, 0.021)
            rod('graphite', elbow.toArray() as Point, tip.toArray() as Point, 0.018)
            const [sideStart, sideEnd] = model.cabSideWindow
            const windowRows: RibbonSection[] = modelId === 'CR200J'
                ? [[sideStart, 2.79, 3.43], [sideStart + 0.32, 2.75, 3.44],
                    [sideEnd - 0.22, 2.65, 3.10], [sideEnd, 2.62, 2.72]]
                : [sideStart, (sideStart + sideEnd) / 2, sideEnd].map((x, index) => {
                const ring = sampleSection(x)
                const lowCabWindow = ['CR450', 'CR400BF', 'CRH3C', 'CRH380B'].includes(modelId)
                const top = ring.bottom + (ring.top - ring.bottom) * (lowCabWindow ? 0.8 : 0.86)
                return [x, top - (index === 2 ? 0.09 : lowCabWindow ? 0.66 : 0.56), top]
            })
            sideRibbon('rubber', windowRows, side, 0.036)
            sideRibbon('glass', windowRows.map(([x, a, b]) => [x, a + 0.025, b - 0.022]), side, 0.046)
        }

        function oval(finish: Finish, x: number, u: number, rx: number, ru: number, offset: number) {
            const vertices = [...surface(x, u, offset).toArray()]
            const indices: number[] = []
            for (let i = 0; i <= 32; i++) {
                const angle = i / 32 * Math.PI * 2
                vertices.push(...surface(x + Math.cos(angle) * rx, u + Math.sin(angle) * ru, offset).toArray())
                if (i < 32) indices.push(0, i + 2, i + 1)
            }
            const geometry = new THREE.BufferGeometry()
            geometry.setAttribute('position', new THREE.Float32BufferAttribute(vertices, 3))
            geometry.setIndex(indices)
            geometry.computeVertexNormals()
            add(finish, geometry)
        }

        for (const side of modelId === 'CRH2' || modelId === 'CRH6' ? [] : [-1, 1]) {
            const u = side < 0 ? model.lampU : 1 - model.lampU
            const x = modelId === 'CR200J' && role === 'tail' ? 11.05 : model.lampX
            if (modelId === 'CR200J') {
                const mirror = (rows: RibbonSection[]) => rows.map(([px, a, b]) =>
                    [px, side < 0 ? a : 1 - b, side < 0 ? b : 1 - a] as RibbonSection)
                // Slender paired cheek lights follow the steep face, with the
                // upper lens outboard and the lower lens closer to the centre.
                const lampStart = glassEnd - 0.12
                canopyRibbon('steel', mirror([[lampStart - 0.04, 0.335, 0.338], [lampStart + 0.08, 0.312, 0.354],
                    [lampStart + 0.67, 0.325, 0.367], [lampStart + 0.78, 0.347, 0.350]]), 0.054)
                canopyRibbon('rubber', mirror([[lampStart + 0.01, 0.335, 0.338], [lampStart + 0.10, 0.318, 0.350],
                    [lampStart + 0.63, 0.331, 0.361], [lampStart + 0.72, 0.346, 0.349]]), 0.068)
                for (const [dx, pu] of [[0.15, 0.335], [0.49, 0.343]] as const) {
                    oval('lamp', lampStart + dx, side < 0 ? pu : 1 - pu, 0.13, 0.010, 0.085)
                }
            } else if (modelId === 'CRH1') {
                // The Regina light stacks share its steep black front face.
                // Project all lenses onto that face so they never float ahead
                // of the broad lower coupler door.
                const frontU = side < 0 ? 0.37 : 0.63
                oval('steel', 11.78, frontU, 0.245, 0.032, 0.051)
                oval('rubber', 11.78, frontU, 0.22, 0.027, 0.064)
                for (const dx of [-0.135, 0, 0.135]) oval('lamp', 11.78 + dx, frontU, 0.055, 0.018, 0.079)
            } else if (model.lampStyle === 'round') {
                oval('steel', x, u, 0.38, 0.042, 0.051)
                oval('rubber', x, u, 0.31, 0.036, 0.064)
                for (const offset of [-0.115, 0.115]) oval('lamp', x + offset, u, 0.082, 0.020, 0.079)
            } else if (model.lampStyle === 'vertical') {
                oval('rubber', x, u, 0.48, 0.029, 0.056)
                for (const offset of modelId === 'CRH5' ? [-0.18, 0.18] : [-0.24, 0, 0.24]) oval('lamp', x + offset, u, 0.086, 0.017, 0.079)
            } else {
                const mirror = (rows: RibbonSection[]) => rows.map(([px, a, b]) =>
                    [px, side < 0 ? a : 1 - b, side < 0 ? b : 1 - a] as RibbonSection)
                const sweep = model.lampStyle === 'swept' ? 0.031 : 0
                canopyRibbon('steel', mirror([[x - 0.68, model.lampU - 0.006, model.lampU - 0.004],
                    [x - 0.38, model.lampU - 0.025, model.lampU + 0.022],
                    [x + 0.38, model.lampU + sweep - 0.015, model.lampU + sweep + 0.025],
                    [x + 0.72, model.lampU + sweep + 0.019, model.lampU + sweep + 0.021]]), 0.052)
                canopyRibbon('rubber', mirror([[x - 0.57, model.lampU - 0.005, model.lampU - 0.003],
                    [x - 0.34, model.lampU - 0.018, model.lampU + 0.016],
                    [x + 0.34, model.lampU + sweep - 0.009, model.lampU + sweep + 0.019],
                    [x + 0.60, model.lampU + sweep + 0.015, model.lampU + sweep + 0.017]]), 0.065)
                for (let lens = 0; lens < 3; lens++) {
                    const px = x - 0.4 + lens * 0.29
                    const pu = model.lampU + lens * sweep / 2
                    canopyRibbon('lamp', mirror([[px, pu - 0.006, pu + 0.008],
                        [px + 0.20, pu - 0.003, pu + 0.011]]), 0.078)
                }
            }
        }
        // Coupler door, panel joints and a closed nose tip on every profile.
        const coverSeamX = modelId === 'CR200J' ? 11.94 : 11.58
        patch('seam', coverSeamX, coverSeamX + 0.022, 0, 1, 1, 96, 0.013)
        if (modelId === 'CRH2' || modelId === 'CRH6') {
            // CRH2 has a roof-centre light bar above its windshield; the early
            // CRH6A uses a row below the glass, both clearly visible in photos.
            const lightX = modelId === 'CRH2' ? glassStart - 0.13 : glassEnd + 0.26
            patch('rubber', lightX - 0.15, lightX + 0.15, 0.415, 0.585, 8, 28, 0.076, 0.015)
            for (let lens = 0; lens < 5; lens++) oval('lamp', lightX, 0.44 + lens * 0.03, 0.075, 0.010, 0.092)
        }
        if (modelId === 'CRH5') {
            oval('rubber', glassStart - 0.15, 0.5, 0.17, 0.038, 0.052)
            oval('lamp', glassStart - 0.15, 0.5, 0.10, 0.025, 0.075)
        }
        if (modelId === 'CRH3C' || modelId === 'CRH380B') {
            oval('rubber', glassEnd + 0.3, 0.5, 0.14, 0.031, 0.052)
            oval('lamp', glassEnd + 0.3, 0.5, 0.10, 0.022, 0.074)
        }
        if (modelId === 'CR200J') {
            // Rectangular central headlight above the windscreen.
            patch('rubber', glassStart - 0.48, glassStart - 0.05, 0.470, 0.530, 10, 16, 0.048, 0.009)
            patch('lamp', glassStart - 0.42, glassStart - 0.12, 0.478, 0.522, 10, 16, 0.067, 0.007)
            // Small outlined crest below the windscreen, not a metallic hatch.
            for (let i = 0; i < 20; i++) {
                const point = (angle: number) => surface(11.36 + Math.sin(angle) * 0.14,
                    0.5 + Math.cos(angle) * 0.017, 0.066).toArray() as Point
                rod('marker', point(i / 20 * Math.PI * 2), point((i + 1) / 20 * Math.PI * 2), 0.012)
            }
            rod('marker', surface(11.38, 0.5, 0.069).toArray() as Point, surface(11.57, 0.5, 0.069).toArray() as Point, 0.012)
            // Dark lower apron with a slim metal deflector, kept behind the
            // coupling cover and clear of the rails.
            const apronWidth = EMU_DIMENSIONS.width * sampleSection(noseEnd).width * 0.94
            const apronY = sampleSection(12.08).bottom - 0.075
            const chin = new THREE.ExtrudeGeometry(roundedShape(apronWidth, 0.17, 0.06),
                { depth: 0.19, bevelEnabled: true, bevelSegments: 2, steps: 1, bevelSize: 0.025, bevelThickness: 0.025, curveSegments: 8 })
            chin.rotateY(Math.PI / 2)
            chin.translate(11.96, apronY, 0)
            add('graphite', chin)
            const deflector = new THREE.ShapeGeometry(roundedShape(apronWidth * 0.9, 0.065, 0.025), 8)
            deflector.rotateY(Math.PI / 2)
            deflector.translate(12.202, apronY - 0.068, 0)
            add('steel', deflector)
        }
        if (model.brand === '复兴号' && modelId !== 'CR200J') patch('marker', Math.min(11.0, glassEnd + 0.7), Math.min(11.0, glassEnd + 0.7) + 0.15,
            0.488, 0.512, 3, 8, 0.069, 0.005)
    }

    if (isPowerCar) {
        // CR200J's motor coach has equipment rooms, not passenger glazing.
        for (const side of [-1, 1]) {
            for (const x of [-6.5, -2.4, 1.7, 5.0]) {
                panel('seam', 2.85, 1.40, 0.035, [x, 2.55, side * 1.689])
                panel('graphite', 2.72, 1.29, 0.025, [x, 2.55, side * 1.705])
                for (let slat = 0; slat < 13; slat++) {
                    panel('roof', 2.59, 0.045, 0.01, [x, 1.99 + slat * 0.094, side * 1.719])
                }
            }
            for (const x of [-9.7, 7.0]) {
                for (const dx of [-0.69, 0.69]) rod('steel', [x + dx, 1.5, side * 1.65], [x + dx, 2.57, side * 1.65], 0.023)
            }
        }
    }

    // Two two-axle bogies, with steel running surfaces centered on standard gauge.
    // Parts stay in physical proportions before group normalization.
    for (const bogieX of getEmuBogieOffsets(isCab ? 'head' : 'middle', modelId)) {
        activeParts = new Map<Finish, THREE.BufferGeometry[]>()
        bogieParts.push({ center: bogieX, parts: activeParts })
        box('graphite', [2.95, 0.3, 1.46], [bogieX, 0.73, 0])
        box('rubber', [2.0, 0.17, 1.66], [bogieX, 0.98, 0])
        for (const side of [-1, 1]) {
            box('graphite', [2.98, 0.18, 0.14], [bogieX, 0.55, side * 0.91])
            for (const axleOffset of [-1.05, 1.05]) {
                const axleX = bogieX + axleOffset
                cylinder('steel', 0.46, 0.12, [axleX, 0.46, side * EMU_DIMENSIONS.railGauge / 2])
                cylinder('graphite', 0.35, 0.125, [axleX, 0.46, side * 0.745])
                cylinder('steel', 0.17, 0.16, [axleX, 0.46, side * 0.805])
                box('graphite', [0.37, 0.32, 0.21], [axleX, 0.45, side * 0.985])
                cylinder('steel', 0.085, 0.023, [axleX, 0.46, side * 1.103], 'z', 12)
                for (const springOffset of [-0.3, 0.3]) {
                    cylinder('rubber', 0.105, 0.25, [axleX + springOffset, 0.78, side * 0.92], 'y', 10)
                    for (const springY of [0.71, 0.775, 0.84]) {
                        cylinder('steel', 0.113, 0.021, [axleX + springOffset, springY, side * 0.92], 'y', 10)
                    }
                }
            }
            cylinder('rubber', 0.25, 0.17, [bogieX, 0.91, side * 0.68], 'y', 16)
            rod('steel', [bogieX - 0.53, 0.47, side * 0.91], [bogieX + 0.58, 0.81, side * 0.91], 0.037)
        }
        for (const axleOffset of [-1.05, 1.05]) {
            cylinder('graphite', 0.09, 1.95, [bogieX + axleOffset, 0.46, 0])
            for (const z of [-0.36, 0.36]) cylinder('steel', 0.3, 0.035, [bogieX + axleOffset, 0.46, z])
        }
    }
    activeParts = parts
    if (modelId === 'CR450') {
        // The CR450AF demonstrator encloses the bogie shoulders. Rounded lower
        // fairings retain rail contact and the separately steerable wheelsets.
        for (const bogieX of getEmuBogieOffsets(isCab ? 'head' : 'middle', modelId)) {
            for (const side of [-1, 1]) {
                const fairing = new THREE.ShapeGeometry(roundedShape(3.65, 0.72, 0.24), 8)
                if (side < 0) fairing.rotateY(Math.PI)
                fairing.translate(bogieX, 0.88, side * 1.45)
                add('pearl', fairing)
                box('seam', [2.8, 0.024, 0.018], [bogieX, 0.68, side * 1.461])
            }
        }
    }
    box('graphite', [9.9, 0.45, 2.36], [-0.7, 0.96, 0])
    for (const x of [-4.1, -1.3, 1.5]) {
        box('roof', [2.36, 0.43, 1.55], [x, 0.82, 0])
        for (const side of [-1, 1]) {
            for (let slot = 0; slot < 8; slot++) {
                box('graphite', [0.043, 0.26, 0.014], [x - 0.9 + slot * 0.25, 0.82, side * 0.785])
            }
        }
    }

    // Flexible inner connections only: cab noses never receive exposed bellows.
    for (const sign of isCab ? [-1] : [-1, 1]) {
        box('graphite', [0.33, 2.35, 2.46], [sign * 12.24, 2.21, 0])
        for (let fold = 0; fold < 5; fold++) {
            const x = sign * (12.14 + fold * 0.065)
            box('rubber', [0.041, 2.49, 0.12], [x, 2.22, -1.25])
            box('rubber', [0.041, 2.49, 0.12], [x, 2.22, 1.25])
            box('rubber', [0.041, 0.13, 2.51], [x, 3.45, 0])
            box('rubber', [0.041, 0.13, 2.51], [x, 0.99, 0])
        }
        box('steel', [0.34, 0.07, 1.2], [sign * 12.27, 1.08, 0])
        box('graphite', [0.38, 0.19, 0.26], [sign * 12.26, 0.78, 0])
    }

    // Roof-mounted cooling housings with inset slatted intakes.
    const roofXs = isPowerCar ? [-8.0, 3.5]
        : isOriginalAf ? isCab ? [-7.3, -2.3] : [-6.8, 6.8]
        : isCab ? [-7.1, Math.min(noseStart - 3, 1.8)] : [-6.8, 6.8]
    const roofPodHeight = model.roofStyle === 'pods' || model.roofStyle === 'ribbed' ? 0.28 : 0.18
    for (const roofX of roofXs) {
        box('roof', [3.35, roofPodHeight, 1.56], [roofX, 3.79 + roofPodHeight / 2, 0])
        box('graphite', [2.55, 0.035, 1.25], [roofX, 3.804 + roofPodHeight, 0])
        for (let slat = 0; slat < 18; slat++) {
            box('roof', [0.052, 0.028, 1.23], [roofX - 1.19 + slat * 0.14, 3.832 + roofPodHeight, 0])
        }
        box('pearl', [0.31, 0.1, 1.58], [roofX - 1.55, 3.85, 0])
        box('pearl', [0.31, 0.1, 1.58], [roofX + 1.55, 3.85, 0])
        if (!isOriginalAf && model.roofStyle === 'faired') {
            for (const side of [-1, 1]) {
                box('pearl', [3.5, 0.2, 0.12], [roofX, 3.89, side * 0.86])
            }
        }
    }
    if (isPowerCar) {
        for (const fanX of [2.7, 4.3]) {
            cylinder('graphite', 0.51, 0.045, [fanX, 4.045, 0], 'y', 32)
            cylinder('roof', 0.16, 0.06, [fanX, 4.066, 0], 'y', 16)
            for (let blade = 0; blade < 8; blade++) {
                const angle = blade * Math.PI / 4
                rod('steel', [fanX + Math.cos(angle) * 0.16, 4.078, Math.sin(angle) * 0.16],
                    [fanX + Math.cos(angle + 0.25) * 0.47, 4.078, Math.sin(angle + 0.25) * 0.47], 0.023)
            }
        }
        for (const x of [-3.8, 0.9]) {
            cylinder('rubber', 0.15, 0.23, [x, 4.01, 0.5], 'y', 12)
            for (let ring = 0; ring < 4; ring++) cylinder('roof', 0.2, 0.023, [x, 3.92 + ring * 0.06, 0.5], 'y', 14)
        }
        rod('red', [-3.8, 4.14, 0.5], [0.9, 4.14, 0.5], 0.035)
    }
    const hasPantograph = model.pantographCars.includes(carIndex) && (role === 'middle' || isPowerCar)
    if (hasPantograph) {
        // A low-profile single-arm pantograph, ceramic insulators and contact shoe.
        const x = isPowerCar ? -2.0 : -0.9
        box('graphite', [2.55, 0.07, 1.43], [x, 3.92, 0])
        for (const offset of [-0.88, 0.88]) {
            for (const side of [-1, 1]) {
                cylinder('pearl', 0.095, 0.22, [x + offset, 4.055, side * 0.47], 'y', 10)
                for (const y of [4.015, 4.065, 4.115]) cylinder('roof', 0.13, 0.025, [x + offset, y, side * 0.47], 'y', 10)
            }
        }
        for (const side of [-1, 1]) {
            rod('graphite', [x - 0.9, 4.17, side * 0.44], [x + 0.84, 4.36, side * 0.32], 0.045)
            rod('steel', [x + 0.84, 4.36, side * 0.32], [x - 0.05, 4.54, side * 0.16], 0.032)
            rod('graphite', [x - 0.72, 4.18, side * 0.40], [x + 0.87, 4.32, side * 0.27], 0.022)
        }
        box('graphite', [0.2, 0.047, 1.72], [x - 0.05, 4.565, 0])
        box('steel', [0.065, 0.027, 1.83], [x - 0.1, 4.575, 0])
        rod('steel', [x - 0.1, 4.575, -0.915], [x - 0.1, 4.48, -1.03], 0.018)
        rod('steel', [x - 0.1, 4.575, 0.915], [x - 0.1, 4.48, 1.03], 0.018)
        rod('graphite', [x + 1, 3.93, 0.52], [3.9, 3.93, 0.52], 0.03)
    } else {
        box('roof', [0.6, 0.075, 0.42], [isCab ? Math.min(3.1, cabRoofEnd - 0.5) : 0, 3.92, 0])
    }

    // Merge the shell and each steerable bogie separately, retaining only around
    // twenty draw calls per car. Each mesh owns its disposable material, too.
    const usedFinishes = new Set<Finish>()
    function buildMeshes(target: THREE.Group, batches: Map<Finish, THREE.BufferGeometry[]>, center = 0) {
        for (const [finish, geometries] of batches) {
            const normalized = geometries.map((geometry) => geometry.index ? geometry.toNonIndexed() : geometry)
            const merged = mergeGeometries(normalized, false)
            for (const geometry of new Set([...geometries, ...normalized])) geometry.dispose()
            if (!merged) continue
            merged.translate(-center, 0, 0)
            merged.scale(1 / EMU_DIMENSIONS.length, 1 / EMU_DIMENSIONS.height, 1 / EMU_DIMENSIONS.width)
            // Parent rotation remains dedicated to the direction of the track.
            if (role === 'tail') merged.rotateY(Math.PI)
            merged.computeBoundingBox()
            merged.computeBoundingSphere()
            const material = usedFinishes.has(finish) ? materials[finish].clone() : materials[finish]
            usedFinishes.add(finish)
            const mesh = new THREE.Mesh(merged, material)
            mesh.name = `emu-${finish}`
            mesh.castShadow = true
            mesh.receiveShadow = true
            target.add(mesh)
        }
    }
    buildMeshes(group, parts)
    for (const bogie of bogieParts) {
        const pivot = new THREE.Group()
        pivot.position.x = bogie.center / EMU_DIMENSIONS.length * (role === 'tail' ? -1 : 1)
        pivot.name = pivot.position.x > 0 ? 'bogie-front' : 'bogie-rear'
        buildMeshes(pivot, bogie.parts, bogie.center)
        group.add(pivot)
    }
    for (const finish of Object.keys(materials) as Finish[]) {
        if (!usedFinishes.has(finish)) materials[finish].dispose()
    }
    group.userData.emuType = modelId
    group.userData.emuVariant = model.variant
    group.userData.emuRole = role
    group.userData.powerCar = isPowerCar
    group.userData.pantograph = hasPantograph
    group.userData.passengerWindowCount = windowXs.length * 2
    group.userData.doorCount = doorXs.length * 2
    const [rear, front] = getEmuBogieOffsets(role, modelId)
    group.userData.bogieRearX = rear / EMU_DIMENSIONS.length
    group.userData.bogieFrontX = front / EMU_DIMENSIONS.length
    return group
}
