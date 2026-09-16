import * as THREE from 'three'
import { createEmuMarking } from './emuMarkings.ts'
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
export function getEmuBogieOffsets(role: EmuCarRole): [number, number] {
    if (role === 'head') return [-8.25, 5.1]
    if (role === 'tail') return [-5.1, 8.25]
    return [-8.25, 8.25]
}

type Finish = 'pearl' | 'roof' | 'red' | 'glass' | 'reflection' | 'graphite' | 'rubber' | 'steel' | 'seam' | 'lamp' | 'marker'
type Point = [number, number, number]
type RibbonSection = [x: number, lower: number, upper: number]

/**
 * A procedural CR400AF-inspired carriage, using the standard silver/red livery.
 * Reference: CRRC Sifang's standard CR400AF product photographs:
 * https://www.crrcgc.cc/sfgf/2015-11/04/article_E7691BD1DADD4C799E29B682C86DADD3.html
 * Lettering is local vector geometry, with no image or font downloads. X is longitudinal, Z is across the
 * track; wheels touch Y=0. Head points +X, tail points -X. Its normalized bounds
 * fit X/Z ±0.5 and Y 0..1 (including a raised pantograph on selected middle cars).
 * Scale by (length, height, width). Each call owns its materials and geometries,
 * so normal scene traversal/disposal is safe. Static details merge by material.
 */
export function createEmuCar(role: EmuCarRole, carIndex: number): THREE.Group {
    const group = new THREE.Group()
    group.name = `emu-${role}-${carIndex}`
    const isCab = role !== 'middle'
    const parts = new Map<Finish, THREE.BufferGeometry[]>()
    const bogieParts: { center: number; parts: Map<Finish, THREE.BufferGeometry[]> }[] = []
    let activeParts = parts
    const materials: Record<Finish, THREE.MeshStandardMaterial> = {
        pearl: new THREE.MeshStandardMaterial({ color: 0xcbd0d1, metalness: 0.42, roughness: 0.31 }),
        roof: new THREE.MeshStandardMaterial({ color: 0x8c979d, metalness: 0.55, roughness: 0.36 }),
        red: new THREE.MeshStandardMaterial({ color: 0xce132d, metalness: 0.22, roughness: 0.26 }),
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
    const noseSections = [
        [2.0, 1.000, 0.96, 3.88], [3.8, 0.997, 0.95, 3.84],
        [5.2, 0.983, 0.95, 3.67], [6.6, 0.935, 0.96, 3.35],
        [8.0, 0.840, 0.99, 2.99], [9.4, 0.715, 1.04, 2.66],
        [10.5, 0.565, 1.12, 2.43], [11.3, 0.435, 1.21, 2.24],
        [11.9, 0.300, 1.34, 2.07], [12.25, 0.150, 1.52, 1.90],
        [12.42, 0.006, 1.71, 1.73],
    ] as const

    function sampleSection(x: number) {
        if (!isCab || x <= noseSections[0][0]) return { width: 1, bottom: 0.96, top: 3.88, cove: 0 }
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
        return { width: interpolate(1), bottom: interpolate(2), top: interpolate(3),
            cove: 0.08 * Math.sin(THREE.MathUtils.clamp((x - 2) / 10.42, 0, 1) * Math.PI) ** 2 }
    }

    function surface(x: number, u: number, offset = 0): THREE.Vector3 {
        const index = THREE.MathUtils.clamp(u, 0, 1) * (profile.length - 1)
        const i = Math.floor(index)
        const a = profile[i]!
        const b = profile[Math.min(i + 1, profile.length - 1)]!
        const z = THREE.MathUtils.lerp(a.x, b.x, index - i)
        const y = THREE.MathUtils.lerp(a.y, b.y, index - i)
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
            const a = profile[index - 1]!
            const b = profile[index]!
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
    }

    function ribbon(finish: Finish, rows: RibbonSection[], project: (x: number, transverse: number) => THREE.Vector3, flip: boolean) {
        const positions: number[] = []
        const indices: number[] = []
        const across = 8
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

    function lettering(text: '复兴号' | 'CR400AF', x: number, y: number, height: number, side: number) {
        const geometry = createEmuMarking(text, height)
        if (side < 0) geometry.rotateY(Math.PI)
        geometry.translate(x, y, 0)
        const positions = geometry.getAttribute('position')
        for (let i = 0; i < positions.count; i++)
            positions.setZ(i, side * (sideSurfaceZ(positions.getX(i), positions.getY(i)) + 0.025))
        geometry.computeVertexNormals()
        add('graphite', geometry)
    }

    patch('pearl', -12.13, isCab ? 12.42 : 12.13, 0, 1, isCab ? 128 : 4, 96)
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
    patch('roof', -11.75, isCab ? 3.0 : 11.75, 0.43, 0.57, 2, 16, 0.009)
    patch('graphite', -11.9, isCab ? 4.5 : 11.9, 0.02, 0.14, 2, 10, 0.012)
    patch('graphite', -11.9, isCab ? 4.5 : 11.9, 0.86, 0.98, 2, 10, 0.012)
    patch('steel', -11.98, isCab ? 4.5 : 11.98, 0.145, 0.149, 2, 1, 0.02)
    patch('steel', -11.98, isCab ? 4.5 : 11.98, 0.851, 0.855, 2, 1, 0.02)
    for (const side of [-1, 1]) {
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

    const windowXs = isCab
        ? [-8.45, -6.95, -5.45, -3.95, -2.45, -0.95, 0.55]
        : [-8.45, -6.91, -5.37, -3.83, -2.29, -0.75, 0.79, 2.33, 3.87, 5.41, 6.95, 8.49]
    for (const side of [-1, 1]) {
        for (const x of windowXs) {
            panel('rubber', 1.18, 0.81, 0.145, [x, 2.63, side * 1.718])
            panel('glass', 1.11, 0.74, 0.12, [x, 2.63, side * 1.73])
            // Sky reflection occupies only the top rim, preserving deep glazing.
            panel('reflection', 0.92, 0.055, 0.025, [x - 0.035, 2.91, side * 1.734])
        }
        for (const x of isCab ? [-10.5, 1.9] : [-10.5, 10.5]) {
            panel('seam', 1.08, 2.15, 0.09, [x, 2.18, side * 1.674])
            panel('pearl', 1.01, 2.08, 0.075, [x, 2.18, side * 1.681])
            panel('rubber', 1.01, 0.85, 0.025, [x, 2.63, side * 1.703])
            panel('rubber', 0.59, 0.82, 0.1, [x, 2.68, side * 1.711])
            panel('glass', 0.53, 0.75, 0.075, [x, 2.68, side * 1.722])
            panel('red', 1.01, 0.13, 0.005, [x, 2.09, side * 1.691])
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
        lettering('复兴号', isCab ? -5.8 : -4.4, 3.38, 0.29, side)
        lettering('CR400AF', isCab ? -4.5 : -3.7, 1.66, 0.13, side)
    }

    if (isCab) {
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
        const capVertices: number[] = []
        const capCenter = new THREE.Vector3(12.423, 1.72, 0)
        for (let i = 0; i < 96; i++) {
            capVertices.push(...capCenter.toArray(), ...surface(12.42, i / 96).toArray(), ...surface(12.42, (i + 1) / 96).toArray())
        }
        const noseCap = new THREE.BufferGeometry()
        noseCap.setAttribute('position', new THREE.Float32BufferAttribute(capVertices, 3))
        noseCap.computeVertexNormals()
        add('pearl', noseCap)
        // Geometric gold railway crest on the graphite mask.
        patch('marker', 7.67, 7.82, 0.487, 0.513, 3, 8, 0.074, 0.005)
        for (const side of [-1, 1]) {
            const a = surface(7.93, 0.5 + side * 0.011, 0.075)
            const b = surface(8.18, 0.5 + side * 0.022, 0.075)
            rod('marker', a.toArray() as Point, b.toArray() as Point, 0.016)
        }
    }

    // Two two-axle bogies, with steel running surfaces centered on standard gauge.
    // Parts stay in physical proportions before group normalization.
    for (const bogieX of isCab ? [-8.25, 5.1] : [-8.25, 8.25]) {
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
    for (const roofX of isCab ? [-7.3, -2.3] : [-6.8, 6.8]) {
        box('roof', [3.35, 0.18, 1.56], [roofX, 3.88, 0])
        box('graphite', [2.55, 0.035, 1.25], [roofX, 3.984, 0])
        for (let slat = 0; slat < 18; slat++) {
            box('roof', [0.052, 0.028, 1.23], [roofX - 1.19 + slat * 0.14, 4.012, 0])
        }
        box('pearl', [0.31, 0.1, 1.58], [roofX - 1.55, 3.85, 0])
        box('pearl', [0.31, 0.1, 1.58], [roofX + 1.55, 3.85, 0])
    }
    if (role === 'middle' && carIndex % 3 === 1) {
        // A low-profile single-arm pantograph, ceramic insulators and contact shoe.
        const x = -0.9
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
        box('roof', [0.6, 0.075, 0.42], [isCab ? 3.1 : 0, 3.92, 0])
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
    group.userData.emuType = 'CR400AF'
    group.userData.emuRole = role
    const [rear, front] = getEmuBogieOffsets(role)
    group.userData.bogieRearX = rear / EMU_DIMENSIONS.length
    group.userData.bogieFrontX = front / EMU_DIMENSIONS.length
    return group
}
