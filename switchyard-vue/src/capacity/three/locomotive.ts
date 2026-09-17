import * as THREE from 'three'
import { mergeGeometries } from 'three/examples/jsm/utils/BufferGeometryUtils.js'
import { createEmuMarking, type EmuMarking } from './emuMarkings.ts'
import { LOCOMOTIVE_PROFILES, type LocomotiveModelId } from './locomotiveProfiles.ts'

type Point = [number, number, number]
type Point2 = [number, number]
type Role = 'head' | 'middle' | 'tail'
type Finish = 'body' | 'roof' | 'accent' | 'secondary' | 'glass' | 'reflection'
    | 'graphite' | 'rubber' | 'steel' | 'seam' | 'lamp' | 'tailLamp' | 'ceramic' | 'copper' | 'warning'
type Batches = Map<Finish, THREE.BufferGeometry[]>

/** Physical bogie centers in meters. The two Co-Co bogies steer independently. */
export function getLocomotiveBogieOffsets(modelId: LocomotiveModelId): [number, number] {
    const [rear, front] = LOCOMOTIVE_PROFILES[modelId].bogieCenters
    return [rear, front]
}

/**
 * Double-cab, six-axle HXD electric locomotives. Coordinates are first built in
 * meters, with X along the track, Z across it, and wheel contact at Y=0. The
 * finished group fits X/Z ±0.5 and Y 0..1; scale by the model's dimensions.
 * The long, flat steel bodyside, cabs, six driving axles and exposed roof circuit
 * are locomotive geometry, independent from the streamlined EMU shell.
 * Static parts merge by finish, while each bogie retains its own steering pivot.
 * Every returned mesh owns its geometry and material for safe scene disposal.
 */
export function createLocomotive(modelId: LocomotiveModelId, role: Role = 'head', carIndex = 0): THREE.Group {
    const model = LOCOMOTIVE_PROFILES[modelId]
    const dimensions = model.dimensions
    const halfLength = dimensions.length / 2
    const halfWidth = dimensions.width / 2
    const roofY = dimensions.roofHeight
    const wallZ = halfWidth - 0.055
    const bodyBottom = 1.37
    const bodyEnd = halfLength - 0.68
    const cabBack = bodyEnd - 2.5
    const is1D = modelId === 'HXD1D'
    const is3D = modelId === 'HXD3D'
    const group = new THREE.Group()
    group.name = `locomotive-${modelId}-${role}-${carIndex}`
    const bodyParts: Batches = new Map()
    const bogies: { center: number; parts: Batches }[] = []
    let activeParts = bodyParts

    const materials: Record<Finish, THREE.MeshStandardMaterial> = {
        body: new THREE.MeshStandardMaterial({ color: model.bodyColor, metalness: 0.43, roughness: 0.3 }),
        roof: new THREE.MeshStandardMaterial({ color: model.roofColor, metalness: 0.47, roughness: 0.42 }),
        accent: new THREE.MeshStandardMaterial({ color: model.accentColor, metalness: 0.28, roughness: 0.32 }),
        secondary: new THREE.MeshStandardMaterial({ color: model.secondaryColor, metalness: 0.32, roughness: 0.34 }),
        glass: new THREE.MeshStandardMaterial({ color: 0x102935, metalness: 0.58, roughness: 0.16, side: THREE.DoubleSide }),
        reflection: new THREE.MeshStandardMaterial({ color: 0x6f94a1, metalness: 0.54, roughness: 0.2, side: THREE.DoubleSide }),
        graphite: new THREE.MeshStandardMaterial({ color: 0x293238, metalness: 0.48, roughness: 0.53 }),
        rubber: new THREE.MeshStandardMaterial({ color: 0x171e23, metalness: 0.02, roughness: 0.88 }),
        steel: new THREE.MeshStandardMaterial({ color: 0xaab5bb, metalness: 0.85, roughness: 0.27 }),
        seam: new THREE.MeshStandardMaterial({ color: 0x697783, metalness: 0.42, roughness: 0.6 }),
        lamp: new THREE.MeshStandardMaterial({ color: 0xfff4d8, emissive: 0xffdc9b, emissiveIntensity: 1.4, roughness: 0.18 }),
        tailLamp: new THREE.MeshStandardMaterial({ color: 0xf0453c, emissive: 0xe6261b, emissiveIntensity: 1.1, roughness: 0.22 }),
        ceramic: new THREE.MeshStandardMaterial({ color: 0x72553e, metalness: 0.09, roughness: 0.35 }),
        copper: new THREE.MeshStandardMaterial({ color: 0xa67149, metalness: 0.76, roughness: 0.3 }),
        warning: new THREE.MeshStandardMaterial({ color: 0xe8b93d, metalness: 0.2, roughness: 0.43 }),
    }

    function add(finish: Finish, geometry: THREE.BufferGeometry) {
        geometry.deleteAttribute('uv')
        const batch = activeParts.get(finish) ?? []
        batch.push(geometry)
        activeParts.set(finish, batch)
    }

    function box(finish: Finish, size: Point, center: Point, rotationZ = 0, rotationY = 0) {
        const geometry = new THREE.BoxGeometry(...size)
        if (rotationZ) geometry.rotateZ(rotationZ)
        if (rotationY) geometry.rotateY(rotationY)
        geometry.translate(...center)
        add(finish, geometry)
    }

    function cylinder(finish: Finish, radius: number, length: number, center: Point,
        axis: 'x' | 'y' | 'z' = 'z', segments = 20) {
        const geometry = new THREE.CylinderGeometry(radius, radius, length, segments)
        if (axis === 'x') geometry.rotateZ(-Math.PI / 2)
        if (axis === 'z') geometry.rotateX(Math.PI / 2)
        geometry.translate(...center)
        add(finish, geometry)
    }

    function rod(finish: Finish, from: Point, to: Point, radius: number, segments = 8) {
        const a = new THREE.Vector3(...from)
        const b = new THREE.Vector3(...to)
        const delta = b.clone().sub(a)
        if (delta.lengthSq() < 1e-10) return
        const geometry = new THREE.CylinderGeometry(radius, radius, delta.length(), segments)
        geometry.applyQuaternion(new THREE.Quaternion().setFromUnitVectors(new THREE.Vector3(0, 1, 0), delta.normalize()))
        geometry.translate(...a.add(b).multiplyScalar(0.5).toArray())
        add(finish, geometry)
    }

    function tube(finish: Finish, points: Point[], radius: number, tubularSegments = 20) {
        const curve = new THREE.CatmullRomCurve3(points.map(point => new THREE.Vector3(...point)))
        add(finish, new THREE.TubeGeometry(curve, tubularSegments, radius, 6, false))
        // TubeGeometry has no end faces; small caps avoid open-ended hoses.
        for (const endpoint of [points[0]!, points[points.length - 1]!]) {
            const cap = new THREE.SphereGeometry(radius, 6, 4)
            cap.translate(...endpoint)
            add(finish, cap)
        }
    }

    function roundedRectangle(width: number, height: number, radius = 0.035) {
        const r = Math.min(radius, width / 2, height / 2)
        const shape = new THREE.Shape()
        const l = -width / 2, b = -height / 2, t = height / 2, right = width / 2
        shape.moveTo(l + r, b)
        shape.lineTo(right - r, b)
        shape.quadraticCurveTo(right, b, right, b + r)
        shape.lineTo(right, t - r)
        shape.quadraticCurveTo(right, t, right - r, t)
        shape.lineTo(l + r, t)
        shape.quadraticCurveTo(l, t, l, t - r)
        shape.lineTo(l, b + r)
        shape.quadraticCurveTo(l, b, l + r, b)
        return shape
    }

    function polygon(points: Point2[]) {
        const shape = new THREE.Shape()
        shape.moveTo(...points[0]!)
        for (const point of points.slice(1)) shape.lineTo(...point)
        shape.closePath()
        return shape
    }

    // Cab slopes are model-specific, but the engine room retains straight sides.
    // Front X is evaluated per height so glass, trim and lamps sit on the body.
    function frontX(y: number, z = 0) {
        const relativeY = y - bodyBottom
        let inset: number
        if (is1D) {
            inset = relativeY < 0.48 ? 0.1 - relativeY * 0.1
                : relativeY < 1.11 ? 0.05
                : 0.05 + (relativeY - 1.11) * 0.47
        } else if (is3D) {
            inset = relativeY < 0.6 ? 0.13 - relativeY * 0.16
                : relativeY < 1.14 ? 0.034 + (relativeY - 0.6) * 0.12
                : 0.1 + (relativeY - 1.14) * 0.31
            inset += 0.17 * Math.pow(Math.abs(z) / wallZ, 2.6)
        } else {
            inset = relativeY < 0.65 ? 0.11 : 0.11 + (relativeY - 0.65) * 0.17
        }
        return bodyEnd - inset
    }

    function sectionWidth(y: number) {
        if (y < bodyBottom + 0.22) return wallZ - 0.15 + (y - bodyBottom) * 0.15 / 0.22
        if (y <= roofY - 0.49) return wallZ
        if (y <= roofY - 0.18) return wallZ - (y - (roofY - 0.49)) * 0.17 / 0.31
        return wallZ - 0.17 - (y - (roofY - 0.18)) * 0.31 / 0.18
    }
    const faceLevels = [...new Set([
        bodyBottom, bodyBottom + 0.22, bodyBottom + 0.48, bodyBottom + 0.6,
        bodyBottom + 0.65, bodyBottom + 1.11, bodyBottom + 1.14,
        roofY - 0.49, roofY - 0.18, roofY,
        ...Array.from({ length: 20 }, (_, i) => bodyBottom + (roofY - bodyBottom) * (i + 1) / 21),
    ])].sort((a, b) => a - b)
    const crossSection: Point2[] = [
        [-sectionWidth(bodyBottom), bodyBottom],
        ...faceLevels.map(y => [sectionWidth(y), y] as Point2),
        ...faceLevels.slice(1).reverse().map(y => [-sectionWidth(y), y] as Point2),
    ]

    function sideZ(x: number, y: number) {
        let width = wallZ
        if (y < bodyBottom + 0.22) width -= 0.15 * (1 - (y - bodyBottom) / 0.22)
        if (y > roofY - 0.49) width -= (y - (roofY - 0.49)) * 0.8
        const alongCab = THREE.MathUtils.clamp((Math.abs(x) - cabBack) / (bodyEnd - cabBack), 0, 1)
        width -= (is3D ? 0.17 : is1D ? 0.085 : 0.035) * alongCab ** 2
        return width
    }

    // A closed loft uses additional sections around the cab corners. It gives
    // the Dalian cab rounded cheeks and leaves the Zhuzhou cab visibly angular.
    const rings = [-1, -0.92, -0.77, -0.55, 0.55, 0.77, 0.92, 1]
    const shellPositions: number[] = []
    const shellIndices: number[] = []
    function shellPoint(t: number, z: number, y: number): Point {
        const alongCab = THREE.MathUtils.clamp((Math.abs(t) - 0.55) / 0.45, 0, 1)
        let actualZ = z
        let x = cabBack
        // The same frontX surface is used by the cap, windshield and painted trim.
        for (let iteration = 0; iteration < 3; iteration++) {
            x = THREE.MathUtils.lerp(cabBack, frontX(y, actualZ), alongCab)
            actualZ = z * sideZ(x, y) / Math.max(0.1, sideZ(0, y))
        }
        x = THREE.MathUtils.lerp(cabBack, frontX(y, actualZ), alongCab)
        return [Math.sign(t) * x, y, actualZ]
    }
    for (const t of rings) {
        for (const [z, y] of crossSection) {
            shellPositions.push(...shellPoint(t, z, y))
        }
    }
    const ringSize = crossSection.length
    for (let i = 0; i < rings.length - 1; i++) {
        for (let j = 0; j < ringSize; j++) {
            const a = i * ringSize + j, b = i * ringSize + (j + 1) % ringSize
            const c = (i + 1) * ringSize + j, d = (i + 1) * ringSize + (j + 1) % ringSize
            shellIndices.push(a, c, b, b, c, d)
        }
    }
    // Subdivided, outward-facing cab caps follow every horizontal crease. A
    // single fan across the face would cut through the sloped windscreen plane.
    for (const sign of [-1, 1]) {
        const base = shellPositions.length / 3
        const columns = 20
        for (const y of faceLevels) {
            const width = shellPoint(sign, sectionWidth(y), y)[2]
            for (let iz = 0; iz <= columns; iz++) {
                const z = (iz / columns * 2 - 1) * width
                shellPositions.push(sign * frontX(y, z), y, z)
            }
        }
        for (let iy = 0; iy < faceLevels.length - 1; iy++) {
            for (let iz = 0; iz < columns; iz++) {
                const a = base + iy * (columns + 1) + iz, b = a + 1
                const c = a + columns + 1, d = c + 1
                if (sign > 0) shellIndices.push(a, c, b, b, c, d)
                else shellIndices.push(a, b, c, b, d, c)
            }
        }
    }
    const shell = new THREE.BufferGeometry()
    shell.setAttribute('position', new THREE.Float32BufferAttribute(shellPositions, 3))
    shell.setIndex(shellIndices)
    shell.computeVertexNormals()
    add('body', shell)

    function sidePanel(finish: Finish, shape: THREE.Shape, center: Point, offset = 0.012) {
        const geometry = new THREE.ShapeGeometry(shape, 5)
        const position = geometry.getAttribute('position')
        const side = Math.sign(center[2]) || 1
        // Rotate the far side around Y so lettering and panels face outward.
        if (side < 0) geometry.rotateY(Math.PI)
        for (let i = 0; i < position.count; i++) {
            const y = position.getY(i) + center[1]
            const limit = frontX(y, wallZ) - 0.025
            const x = THREE.MathUtils.clamp(position.getX(i) + center[0], -limit, limit)
            position.setXYZ(i, x, y, side * (sideZ(x, y) + offset))
        }
        geometry.computeVertexNormals()
        add(finish, geometry)
    }

    function reverseFaces(geometry: THREE.BufferGeometry) {
        const index = geometry.getIndex()
        if (!index) return
        for (let i = 0; i < index.count; i += 3) {
            const a = index.getX(i)
            index.setX(i, index.getX(i + 2))
            index.setX(i + 2, a)
        }
    }

    function sideRibbon(finish: Finish, y: number, height: number, side: number, offset = 0.018) {
        const vertices: number[] = []
        const indices: number[] = []
        const steps = 100
        for (let row = 0; row <= 4; row++) {
            const actualY = y - height / 2 + height * row / 4
            const limit = frontX(actualY, wallZ) - 0.025
            for (let column = 0; column <= steps; column++) {
                const x = (column / steps * 2 - 1) * limit
                vertices.push(x, actualY, side * (sideZ(x, actualY) + offset))
            }
        }
        for (let row = 0; row < 4; row++) {
            for (let column = 0; column < steps; column++) {
                const a = row * (steps + 1) + column, b = a + 1, c = a + steps + 1, d = c + 1
                if (side > 0) indices.push(a, b, c, b, d, c)
                else indices.push(a, c, b, b, c, d)
            }
        }
        const geometry = new THREE.BufferGeometry()
        geometry.setAttribute('position', new THREE.Float32BufferAttribute(vertices, 3))
        geometry.setIndex(indices)
        geometry.computeVertexNormals()
        add(finish, geometry)
    }

    function frontPanel(finish: Finish, shape: THREE.Shape, sign: number, centerY: number, centerZ = 0, offset = 0.018) {
        // Large ShapeGeometry triangles form chords through the convex cab. Split
        // before projecting so paint and window seals follow the actual surface.
        const source = new THREE.ShapeGeometry(shape, 6)
        const sourcePositions = source.getAttribute('position')
        const sourceIndex = source.getIndex()!
        const vertices: number[] = []
        const edgeLengthSq = (a: Point2, b: Point2) => (a[0] - b[0]) ** 2 + (a[1] - b[1]) ** 2
        function subdivide(a: Point2, b: Point2, c: Point2, depth = 0) {
            const ab = edgeLengthSq(a, b), bc = edgeLengthSq(b, c), ca = edgeLengthSq(c, a)
            if (Math.max(ab, bc, ca) <= 0.14 ** 2 || depth >= 14) {
                for (const point of [a, b, c]) vertices.push(point[0], point[1], 0)
                return
            }
            if (ab >= bc && ab >= ca) {
                const middle: Point2 = [(a[0] + b[0]) / 2, (a[1] + b[1]) / 2]
                subdivide(a, middle, c, depth + 1)
                subdivide(middle, b, c, depth + 1)
            } else if (bc >= ca) {
                const middle: Point2 = [(b[0] + c[0]) / 2, (b[1] + c[1]) / 2]
                subdivide(a, b, middle, depth + 1)
                subdivide(a, middle, c, depth + 1)
            } else {
                const middle: Point2 = [(c[0] + a[0]) / 2, (c[1] + a[1]) / 2]
                subdivide(a, b, middle, depth + 1)
                subdivide(middle, b, c, depth + 1)
            }
        }
        for (let i = 0; i < sourceIndex.count; i += 3) {
            const a = sourceIndex.getX(i), b = sourceIndex.getX(i + 1), c = sourceIndex.getX(i + 2)
            subdivide([sourcePositions.getX(a), sourcePositions.getY(a)],
                [sourcePositions.getX(b), sourcePositions.getY(b)], [sourcePositions.getX(c), sourcePositions.getY(c)])
        }
        source.dispose()
        const geometry = new THREE.BufferGeometry()
        geometry.setAttribute('position', new THREE.Float32BufferAttribute(vertices, 3))
        geometry.setIndex(Array.from({ length: vertices.length / 3 }, (_, index) => index))
        const position = geometry.getAttribute('position')
        for (let i = 0; i < position.count; i++) {
            const z = position.getX(i) + centerZ
            const y = position.getY(i) + centerY
            position.setXYZ(i, sign * (frontX(y, z) + offset), y, sign * z)
        }
        reverseFaces(geometry)
        geometry.computeVertexNormals()
        add(finish, geometry)
    }

    function doorPaint(x: number, side: number, width: number, bottom: number, top: number, offset: number) {
        const centerY = is1D ? 1.81 : is3D ? 2.18 : 2.67
        const height = is1D ? 0.76 : is3D ? 0.3 : 0.62
        const lower = Math.max(bottom, centerY - height / 2)
        const upper = Math.min(top, centerY + height / 2)
        if (upper <= lower) return
        // Leave the dark perimeter seam and metal latch above the paint layer.
        sidePanel('accent', roundedRectangle(width, upper - lower, 0.006),
            [x, (lower + upper) / 2, side * wallZ], offset)
    }

    function sideText(text: EmuMarking, height: number, center: Point, finish: Finish = 'accent') {
        const geometry = createEmuMarking(text, height)
        if (center[2] < 0) geometry.rotateY(Math.PI)
        const positions = geometry.getAttribute('position')
        for (let i = 0; i < positions.count; i++) {
            const x = positions.getX(i) + center[0]
            const y = positions.getY(i) + center[1]
            positions.setXYZ(i, x, y, Math.sign(center[2]) * (sideZ(x, y) + 0.03))
        }
        geometry.computeVertexNormals()
        add(finish, geometry)
    }

    function frontText(text: EmuMarking, height: number, y: number, sign: number, finish: Finish = 'accent') {
        const geometry = createEmuMarking(text, height)
        const positions = geometry.getAttribute('position')
        for (let i = 0; i < positions.count; i++) {
            const z = positions.getX(i)
            const worldY = positions.getY(i) + y
            positions.setXYZ(i, sign * (frontX(worldY, z) + 0.048), worldY, -sign * z)
        }
        geometry.computeVertexNormals()
        add(finish, geometry)
    }

    // Longitudinal painted bands, lower silvery skirts and roof drip rails.
    for (const side of [-1, 1]) {
        const z = side * wallZ
        const bodyBandY = is1D ? 1.81 : is3D ? 2.18 : 2.67
        const bandHeight = is1D ? 0.76 : is3D ? 0.3 : 0.62
        sideRibbon('accent', bodyBandY, bandHeight, side)
        if (is3D) {
            sideRibbon('secondary', 1.54, 0.27, side, 0.019)
        } else if (!is1D) {
            sideRibbon('secondary', 1.55, 0.28, side, 0.019)
        }
        box('roof', [cabBack * 2 + 0.15, 0.10, 0.08], [0, roofY - 0.31, side * (wallZ - 0.08)])
        box('graphite', [bodyEnd * 2 - 0.24, 0.14, 0.11], [0, bodyBottom + 0.04, side * (wallZ - 0.11)])
        box('steel', [bodyEnd * 2 - 0.33, 0.026, 0.045], [0, bodyBottom + 0.15, side * (wallZ - 0.11)])

        // Engine-room louvres have individual tilted blades, frames and drainage
        // lips. There are no repeated passenger windows on these locomotives.
        const grilleXs = is1D ? [-4.7, -2.75, 2.75, 4.7] : is3D ? [-4.05, -2.5, 2.5, 4.05] : [-3.9, -2.35, 2.35, 3.9]
        const grilleWidth = is1D ? 1.54 : 1.18
        const grilleY = is1D ? 2.87 : is3D ? 3.06 : 3.21
        const grilleHeight = is1D ? 1.05 : is3D ? 0.79 : 0.55
        for (const x of grilleXs) {
            sidePanel('seam', roundedRectangle(grilleWidth + 0.105, grilleHeight + 0.11, 0.026), [x, grilleY, z], 0.022)
            sidePanel('graphite', roundedRectangle(grilleWidth, grilleHeight, 0.018), [x, grilleY, z], 0.028)
            if (model.grilleStyle === 'vertical') {
                for (let blade = 0; blade < 14; blade++) {
                    box('roof', [0.044, grilleHeight - 0.055, 0.035],
                        [x - grilleWidth / 2 + 0.075 + blade * (grilleWidth - 0.15) / 13, grilleY, side * (wallZ + 0.035)])
                }
            } else {
                const count = is1D ? 12 : 9
                for (let blade = 0; blade < count; blade++) {
                    box('roof', [grilleWidth - 0.07, 0.025, 0.042],
                        [x, grilleY - grilleHeight / 2 + 0.055 + blade * (grilleHeight - 0.11) / (count - 1), side * (wallZ + 0.034)])
                }
            }
            for (const edge of [-1, 1]) {
                for (const upper of [-1, 1]) cylinder('steel', 0.018, 0.016,
                    [x + edge * (grilleWidth / 2 + 0.025), grilleY + upper * (grilleHeight / 2 + 0.028), side * (wallZ + 0.043)], 'z', 7)
            }
        }

        // Equipment access doors, latches, hinges and gently recessed seams.
        for (const x of [-1.03, 1.03]) {
            sidePanel('seam', roundedRectangle(1.55, 1.48, 0.035), [x, 2.68, z], 0.018)
            sidePanel('body', roundedRectangle(1.515, 1.44, 0.026), [x, 2.68, z], 0.023)
            doorPaint(x, side, 1.495, 1.972, 3.388, 0.028)
            for (const y of [2.2, 3.15]) {
                box('steel', [0.06, 0.13, 0.027], [x - 0.72, y, side * (wallZ + 0.039)])
            }
            cylinder('steel', 0.027, 0.029, [x + 0.61, 2.58, side * (wallZ + 0.037)], 'z', 10)
            box('steel', [0.035, 0.135, 0.025], [x + 0.61, 2.55, side * (wallZ + 0.038)])
        }
        sideText('和谐', 0.29, [0, 3.08, z])
        sideText(modelId, 0.2, [0, is1D ? 1.79 : 1.86, z], is1D ? 'graphite' : 'accent')
        for (const x of [-cabBack + 0.18, cabBack - 0.18]) {
            for (const y of [1.85, 2.25, 2.65, 3.05, 3.43]) {
                box('seam', [0.014, 0.25, 0.011], [x, y, side * (sideZ(x, y) + 0.02)])
            }
        }
    }

    // Closed roof panels and raised transverse seams above the flat walls.
    box('roof', [cabBack * 2 + 0.48, 0.055, (wallZ - 0.48) * 2], [0, roofY + 0.018, 0])
    for (const x of [-cabBack + 0.2, -3.6, -1.65, 1.65, 3.6, cabBack - 0.2]) {
        rod('seam', [x, roofY + 0.05, -wallZ + 0.49], [x, roofY + 0.05, wallZ - 0.49], 0.018)
    }

    for (const sign of [-1, 1]) {
        const cabDoorX = sign * (cabBack + 0.22)
        for (const side of [-1, 1]) {
            const z = side * wallZ
            sidePanel('rubber', roundedRectangle(0.81, 1.91, 0.075), [cabDoorX, 2.57, z], 0.026)
            sidePanel('body', roundedRectangle(0.76, 1.85, 0.062), [cabDoorX, 2.57, z], 0.035)
            doorPaint(cabDoorX, side, 0.728, 1.675, 3.465, 0.041)
            sidePanel('steel', roundedRectangle(0.58, 0.69, 0.06), [cabDoorX, 3.04, z], 0.04)
            sidePanel('glass', roundedRectangle(0.52, 0.63, 0.045), [cabDoorX, 3.04, z], 0.049)
            sidePanel('reflection', polygon([[-0.22, 0.26], [-0.22, 0.17], [0.2, -0.18], [0.2, -0.08]]), [cabDoorX, 3.04, z], 0.052)
            box('steel', [0.12, 0.035, 0.025], [cabDoorX - sign * 0.26, 2.42, side * (halfWidth - 0.021)])
            for (const hingeY of [1.95, 2.9]) box('steel', [0.039, 0.13, 0.035], [cabDoorX + sign * 0.35, hingeY, side * (halfWidth - 0.02)])
            // Three open-grated steps and continuous grab rails at both cabs.
            for (let step = 0; step < 3; step++) {
                const y = 0.53 + step * 0.31
                box('graphite', [0.7, 0.065, 0.25], [cabDoorX, y, side * (wallZ - 0.09)])
                box('steel', [0.68, 0.022, 0.047], [cabDoorX, y + 0.037, side * (wallZ + 0.018)])
                for (let slot = 0; slot < 7; slot++) {
                    box('steel', [0.022, 0.018, 0.19], [cabDoorX - 0.27 + slot * 0.09, y + 0.045, side * (wallZ - 0.1)])
                }
            }
            for (const sideOfDoor of [-1, 1]) {
                const x = cabDoorX + sideOfDoor * 0.46
                const grabZ = side * Math.min(halfWidth - 0.022, sideZ(x, 2.3) + 0.09)
                rod('steel', [x, 1.25, grabZ], [x, 2.72, grabZ], 0.021)
                rod('steel', [x, 1.25, grabZ], [x, 1.25, side * (wallZ - 0.08)], 0.021)
                rod('steel', [x, 2.72, grabZ], [x, 2.72, side * (sideZ(x, 2.72) + 0.015)], 0.021)
            }
            const cabWindowX = sign * (cabBack + 1.36)
            const cabWindowShape = polygon([[-0.49, -0.42], [0.49, -0.35], [0.43, 0.36], [-0.49, 0.41]])
            sidePanel('rubber', cabWindowShape, [cabWindowX, 3.12, z], 0.028)
            sidePanel('glass', polygon([[-0.44, -0.37], [0.43, -0.3], [0.37, 0.31], [-0.44, 0.35]]), [cabWindowX, 3.12, z], 0.039)
            sidePanel('reflection', polygon([[-0.38, 0.29], [-0.37, 0.24], [0.33, 0.07], [0.33, 0.12]]), [cabWindowX, 3.12, z], 0.042)
            sideText(sign > 0 ? 'Ⅰ端' : 'Ⅱ端', 0.13, [sign * (cabBack + 1.15), 2.43, z])
        }

        const frontBandY = is1D ? 1.82 : is3D ? 2.19 : 2.71
        const frontBandHeight = is1D ? 0.73 : is3D ? 0.32 : 0.58
        frontPanel('accent', roundedRectangle((wallZ - 0.15) * 2, frontBandHeight, 0.025), sign, frontBandY, 0, 0.024)
        if (is1D) {
            // The sharp V-shaped silver nose stripe identifies the HXD1D cab.
            for (const side of [-1, 1]) {
                frontPanel('accent', polygon([[side * 0.07, 0.04], [side * 1.3, 0.5], [side * 1.3, 0.65], [side * 0.07, 0.2]]), sign, 2.14, 0, 0.034)
            }
        } else if (is3D) {
            frontPanel('accent', polygon([[-1.31, 0.24], [0, -0.02], [1.31, 0.24], [1.31, 0.34], [0, 0.1], [-1.31, 0.34]]), sign, 2.26, 0, 0.035)
        }

        // The window silhouettes and visors differ between the angular, rounded
        // and nearly vertical cab faces. Rubber gaskets remain separate geometry.
        const windowY = is1D ? 3.24 : is3D ? 3.22 : 3.3
        const windscreenWidth = is3D ? 2.48 : 2.30
        const windscreenHeight = is1D ? 0.84 : is3D ? 0.82 : 0.79
        frontPanel('rubber', roundedRectangle(windscreenWidth, windscreenHeight, is3D ? 0.14 : 0.055), sign, windowY, 0, 0.025)
        for (const side of [-1, 1]) {
            const w = windscreenWidth / 2 - 0.085
            frontPanel('glass', polygon([[-w / 2, -windscreenHeight / 2 + 0.057], [w / 2, -windscreenHeight / 2 + 0.079],
                [w / 2 - (is1D ? 0.075 : 0.027), windscreenHeight / 2 - 0.065], [-w / 2 + 0.025, windscreenHeight / 2 - 0.055]]),
            sign, windowY, side * (windscreenWidth / 4), 0.044)
            frontPanel('reflection', polygon([[-w / 2 + 0.06, 0.29], [w / 2 - 0.11, 0.23], [w / 2 - 0.11, 0.18], [-w / 2 + 0.06, 0.24]]),
                sign, windowY, side * windscreenWidth / 4, 0.049)
            const wiperFrom: Point = [sign * (frontX(windowY - 0.35, side * 0.75) + 0.068), windowY - 0.35, sign * side * 0.75]
            const wiperTo: Point = [sign * (frontX(windowY + 0.17, side * 0.4) + 0.072), windowY + 0.17, sign * side * 0.4]
            rod('graphite', wiperFrom, wiperTo, 0.018)
            rod('rubber', [wiperTo[0], wiperTo[1] - 0.11, wiperTo[2] - 0.17], [wiperTo[0], wiperTo[1] + 0.04, wiperTo[2] + 0.17], 0.025)
        }
        rod('steel', [sign * (frontX(windowY - 0.39) + 0.055), windowY - 0.39, -1.2], [sign * (frontX(windowY - 0.39) + 0.055), windowY - 0.39, 1.2], 0.021)

        function lamp(y: number, z: number, radius: number, main = true) {
            const x = sign * (frontX(y, z) + 0.05)
            cylinder('steel', radius + 0.029, 0.055, [x, y, sign * z], 'x', 24)
            cylinder('rubber', radius + 0.012, 0.063, [x + sign * 0.014, y, sign * z], 'x', 24)
            const finish: Finish = main ? sign > 0 ? 'lamp' : 'glass' : sign < 0 ? 'tailLamp' : 'glass'
            cylinder(finish, radius, 0.067, [x + sign * 0.03, y, sign * z], 'x', 24)
            if (main && sign > 0) {
                cylinder('lamp', radius * 0.43, 0.076, [x + sign * 0.034, y, sign * z], 'x', 16)
            }
        }

        const upperLampY = roofY - 0.24
        if (model.headlampStyle === 'twin-upper') {
            frontPanel('graphite', roundedRectangle(0.61, 0.28, 0.055), sign, upperLampY, 0, 0.026)
            lamp(upperLampY, -0.16, 0.088)
            lamp(upperLampY, 0.16, 0.088)
        } else if (model.headlampStyle === 'triple') {
            frontPanel('graphite', roundedRectangle(0.44, 0.26, 0.075), sign, upperLampY, 0, 0.026)
            lamp(upperLampY, 0, 0.104)
        } else {
            frontPanel('accent', roundedRectangle(0.63, 0.27, 0.025), sign, upperLampY, 0, 0.026)
            lamp(upperLampY, -0.15, 0.087)
            lamp(upperLampY, 0.15, 0.087)
        }
        for (const side of [-1, 1]) {
            const lampY = is1D ? 1.89 : is3D ? 1.85 : 1.96
            frontPanel('graphite', roundedRectangle(is1D ? 0.63 : 0.58, 0.3, is3D ? 0.095 : 0.04), sign, lampY, side * 0.98, 0.03)
            lamp(lampY, side * 0.85, 0.096)
            lamp(lampY, side * 1.12, 0.069, false)
        }
        frontText(modelId, 0.145, is1D ? 2.5 : is3D ? 2.56 : 2.25, sign)

        // Nose handrails, hoses, buffers, coupler pocket and knuckle coupler.
        const bufferX = sign * (bodyEnd + 0.12)
        box('graphite', [0.21, 0.28, 2.7], [bufferX, 1.24, 0])
        for (const side of [-1, 1]) {
            cylinder('steel', 0.105, 0.30, [sign * (bodyEnd + 0.17), 1.12, side * 0.93], 'x', 16)
            box('graphite', [0.10, 0.28, 0.35], [sign * (bodyEnd + 0.35), 1.12, side * 0.93])
            tube('rubber', [[sign * (bodyEnd + 0.12), 1.29, side * 0.52], [sign * (bodyEnd + 0.3), 1.04, side * 0.57],
                [sign * (bodyEnd + 0.37), 0.73, side * 0.47], [sign * (bodyEnd + 0.38), 0.8, side * 0.35]], 0.034)
            cylinder('steel', 0.055, 0.09, [sign * (bodyEnd + 0.38), 0.8, side * 0.35], 'x', 10)
            rod('steel', [sign * (frontX(2.61, side * 1.24) + 0.075), 2.61, side * 1.24],
                [sign * (frontX(2.08, side * 1.24) + 0.075), 2.08, side * 1.24], 0.021)
        }
        box('rubber', [0.19, 0.41, 0.55], [sign * (bodyEnd + 0.12), 1.03, 0])
        box('steel', [0.46, 0.21, 0.22], [sign * (bodyEnd + 0.32), 1.03, 0])
        box('graphite', [0.20, 0.32, 0.43], [sign * (halfLength - 0.12), 1.04, 0])
        box('steel', [0.13, 0.27, 0.17], [sign * (halfLength - 0.09), 1.04, 0.18])
        cylinder('steel', 0.047, 0.34, [sign * (halfLength - 0.14), 1.04, 0.085], 'y', 10)

        // A broad steel obstacle deflector, not an EMU's sealed nose fairing.
        const plowShape = polygon([[-1.28, -0.2], [1.28, -0.2], [1.36, 0.18], [0.48, 0.28],
            [0.39, 0.11], [-0.39, 0.11], [-0.48, 0.28], [-1.36, 0.18]])
        const plow = new THREE.ExtrudeGeometry(plowShape, { depth: 0.10, bevelEnabled: false })
        plow.rotateY(sign * Math.PI / 2)
        plow.translate(sign * (bodyEnd + 0.18), 0.5, 0)
        add('graphite', plow)
        if (!is1D && !is3D) {
            // Yellow/black warning bars on the standard HXD3C obstacle deflector.
            for (let stripe = -4; stripe <= 4; stripe++) {
                const z = stripe * 0.27
                const stripeShape = polygon([[z - 0.08, -0.17], [z + 0.06, -0.17], [z + 0.18, 0.10], [z + 0.04, 0.10]])
                const stripeGeometry = new THREE.ShapeGeometry(stripeShape)
                stripeGeometry.rotateY(sign * Math.PI / 2)
                stripeGeometry.translate(sign * (bodyEnd + 0.29), 0.5, 0)
                add('warning', stripeGeometry)
            }
        }
        rod('steel', [sign * (bodyEnd + 0.25), 0.31, -1.26], [sign * (bodyEnd + 0.25), 0.31, 1.26], 0.028)
        for (const side of [-1, 1]) rod('steel', [sign * (bodyEnd - 0.2), 1.28, side * 1.12], [sign * (bodyEnd + 0.18), 0.64, side * 1.08], 0.05)
    }

    // Center underframe: transformer case, battery boxes, air reservoirs and
    // steel piping. These remain separate from the two steering bogies.
    box('graphite', [bodyEnd * 2 - 0.9, 0.27, 2.49], [0, 1.33, 0])
    box('roof', [3.45, 0.53, 1.45], [0, 0.93, 0])
    for (const side of [-1, 1]) {
        box('graphite', [2.86, 0.44, 0.49], [0, 1.02, side * 1.17])
        for (const x of [-0.82, 0.82]) {
            box('seam', [1.18, 0.36, 0.026], [x, 1.02, side * 1.43])
            box('roof', [1.13, 0.31, 0.028], [x, 1.02, side * 1.45])
            cylinder('steel', 0.027, 0.035, [x + 0.4, 1.03, side * 1.48], 'z', 8)
        }
        cylinder('graphite', 0.19, 2.64, [0, 0.72, side * 0.96], 'x', 18)
        for (const x of [-0.91, 0.91]) {
            box('steel', [0.064, 0.39, 0.04], [x, 0.73, side * 1.14])
        }
        rod('copper', [-1.7, 0.92, side * 1.39], [1.7, 0.92, side * 1.39], 0.018)
    }

    for (const bogieX of getLocomotiveBogieOffsets(modelId)) {
        activeParts = new Map()
        bogies.push({ center: bogieX, parts: activeParts })
        const wheelRadius = 0.625
        const axleOffsets = [...model.axleOffsets].map(x => bogieX < 0 ? -x : x).sort((a, b) => a - b)
        const bogieLength = axleOffsets[2]! - axleOffsets[0]! + 0.9
        box('graphite', [bogieLength, 0.25, 1.68], [bogieX, 0.94, 0])
        box('rubber', [1.52, 0.18, 1.63], [bogieX, 1.16, 0])
        cylinder('steel', 0.22, 0.15, [bogieX, 1.22, 0], 'y', 16)
        for (const side of [-1, 1]) {
            box('graphite', [bogieLength + 0.12, 0.24, 0.20], [bogieX, 0.8, side * 1.04])
            box('steel', [bogieLength - 0.24, 0.055, 0.21], [bogieX, 0.955, side * 1.04])
            for (const axleOffset of axleOffsets) {
                const x = bogieX + axleOffset
                // Twelve driven wheels; running surfaces lie on standard gauge.
                cylinder('steel', wheelRadius, 0.155, [x, wheelRadius, side * dimensions.railGauge / 2], 'z', 32)
                cylinder('graphite', wheelRadius - 0.082, 0.172, [x, wheelRadius, side * 0.725], 'z', 28)
                cylinder('steel', 0.22, 0.205, [x, wheelRadius, side * 0.76], 'z', 20)
                cylinder('graphite', 0.14, 0.232, [x, wheelRadius, side * 0.80], 'z', 18)
                // Axle-box covers, bearing bolts and twin primary coil springs.
                box('graphite', [0.44, 0.41, 0.26], [x, 0.65, side * 1.04])
                cylinder('steel', 0.12, 0.038, [x, 0.65, side * 1.19], 'z', 16)
                cylinder('graphite', 0.079, 0.043, [x, 0.65, side * 1.207], 'z', 12)
                for (const boltOffset of [-1, 1]) {
                    for (const boltY of [-1, 1]) cylinder('steel', 0.019, 0.025, [x + boltOffset * 0.159, 0.65 + boltY * 0.13, side * 1.178], 'z', 6)
                }
                for (const springOffset of [-0.34, 0.34]) {
                    cylinder('rubber', 0.12, 0.34, [x + springOffset, 0.97, side * 1.035], 'y', 12)
                    for (let coil = 0; coil < 6; coil++) {
                        cylinder('steel', 0.135, 0.024, [x + springOffset, 0.815 + coil * 0.058, side * 1.035], 'y', 12)
                    }
                    cylinder('graphite', 0.156, 0.04, [x + springOffset, 1.155, side * 1.035], 'y', 12)
                }
                box('graphite', [0.15, 0.28, 0.12], [x + 0.53, 0.61, side * 0.92])
                rod('steel', [x - 0.37, 0.46, side * 1.05], [x + 0.35, 0.76, side * 1.05], 0.03)
            }
            // Secondary spring packs, traction linkage and sand dispensers.
            for (const x of [-0.68, 0.68]) {
                cylinder('rubber', 0.185, 0.25, [bogieX + x, 1.17, side * 0.89], 'y', 16)
                for (let ring = 0; ring < 4; ring++) cylinder('steel', 0.20, 0.025, [bogieX + x, 1.07 + ring * 0.058, side * 0.89], 'y', 14)
            }
            rod('graphite', [bogieX - 1.38, 0.97, side * 1.16], [bogieX + 1.31, 1.25, side * 1.16], 0.063)
            for (const direction of [-1, 1]) {
                const x = bogieX + direction * (bogieLength / 2 - 0.22)
                box('graphite', [0.37, 0.44, 0.33], [x, 0.97, side * 0.68])
                tube('steel', [[x, 0.77, side * 0.7], [x + direction * 0.14, 0.49, side * 0.7],
                    [x + direction * 0.22, 0.2, side * dimensions.railGauge / 2]], 0.023, 10)
            }
        }
        for (const axleOffset of axleOffsets) {
            const x = bogieX + axleOffset
            cylinder('graphite', 0.105, 2.01, [x, wheelRadius, 0], 'z', 14)
            box('graphite', [0.58, 0.39, 0.83], [x + 0.25, 0.68, 0])
            for (const z of [-0.37, 0.37]) {
                cylinder('steel', 0.38, 0.055, [x, wheelRadius, z], 'z', 24)
                cylinder('graphite', 0.21, 0.065, [x, wheelRadius, z], 'z', 16)
            }
        }
    }
    activeParts = bodyParts

    // Roof-mounted cooling plant: impellers sit under fine radial steel guards.
    const fanXs = is1D ? [-2.38, -0.77, 0.77, 2.38] : [-1.86, 0, 1.86]
    for (const fanX of fanXs) {
        box('roof', [1.46, 0.19, 1.67], [fanX, roofY + 0.12, 0])
        cylinder('graphite', 0.61, 0.046, [fanX, roofY + 0.235, 0], 'y', 32)
        cylinder('roof', 0.19, 0.067, [fanX, roofY + 0.251, 0], 'y', 18)
        for (let blade = 0; blade < 9; blade++) {
            const angle = blade * Math.PI * 2 / 9
            rod('seam', [fanX + Math.cos(angle) * 0.17, roofY + 0.263, Math.sin(angle) * 0.17],
                [fanX + Math.cos(angle + 0.27) * 0.54, roofY + 0.263, Math.sin(angle + 0.27) * 0.54], 0.045)
        }
        for (let ring = 1; ring <= 3; ring++) {
            const guard = new THREE.TorusGeometry(ring * 0.18, 0.011, 5, 32)
            guard.rotateX(Math.PI / 2)
            guard.translate(fanX, roofY + 0.302, 0)
            add('steel', guard)
        }
        for (let spoke = 0; spoke < 12; spoke++) {
            const angle = spoke * Math.PI / 6
            rod('steel', [fanX, roofY + 0.306, 0], [fanX + Math.cos(angle) * 0.6, roofY + 0.306, Math.sin(angle) * 0.6], 0.009)
        }
        for (const side of [-1, 1]) box('steel', [1.3, 0.025, 0.032], [fanX, roofY + 0.24, side * 0.78])
    }

    function insulator(x: number, z: number, height = 0.29, base = roofY + 0.10) {
        cylinder('steel', 0.14, 0.055, [x, base, z], 'y', 12)
        cylinder('ceramic', 0.081, height, [x, base + height / 2, z], 'y', 12)
        const count = Math.max(4, Math.round(height / 0.063))
        for (let ring = 0; ring < count; ring++) cylinder('ceramic', 0.135, 0.036, [x, base + 0.045 + ring * (height - 0.08) / (count - 1), z], 'y', 12)
        cylinder('steel', 0.067, 0.07, [x, base + height + 0.015, z], 'y', 10)
    }

    // Ceramic bushings support a continuous copper high-voltage bus. The central
    // vacuum breaker and arrester sit clear of the fans and pantograph sweep.
    const pantoX = Math.min(cabBack - 0.87, is1D ? 5.52 : 4.82)
    for (const x of [-pantoX, -2.9, 0, 2.9, pantoX]) insulator(x, 0.94)
    rod('copper', [-pantoX, roofY + 0.43, 0.94], [pantoX, roofY + 0.43, 0.94], 0.028)
    box('graphite', [0.77, 0.15, 0.34], [0, roofY + 0.13, -0.94])
    insulator(-0.23, -0.94, 0.42)
    insulator(0.23, -0.94, 0.42)
    rod('steel', [-0.23, roofY + 0.55, -0.94], [0.23, roofY + 0.55, -0.94], 0.046)
    rod('copper', [0.23, roofY + 0.55, -0.94], [0.44, roofY + 0.43, 0.94], 0.025)
    insulator(1.17, -0.89, 0.46)
    rod('copper', [1.17, roofY + 0.6, -0.89], [1.48, roofY + 0.43, 0.94], 0.023)

    // Two single-arm pantographs, one raised to running height and one folded.
    // Their frame is fully three-dimensional: paired arms, crossbars, springs,
    // contact strips and curved horns remain legible in the overhead station view.
    for (const sign of [-1, 1]) {
        const x = sign * pantoX
        const raised = sign < 0
        const baseY = roofY + 0.35
        const elbowY = raised ? roofY + 1.0 : roofY + 0.47
        const shoeY = raised ? dimensions.height - 0.045 : roofY + 0.69
        const baseX = x + sign * 0.9
        const elbowX = x - sign * 0.81
        const shoeX = x + sign * 0.29
        box('graphite', [2.45, 0.08, 1.48], [x, roofY + 0.1, 0])
        for (const dx of [-0.85, 0.85]) {
            for (const side of [-1, 1]) insulator(x + dx, side * 0.53, 0.21, roofY + 0.11)
        }
        for (const side of [-1, 1]) {
            rod('graphite', [baseX, baseY, side * 0.49], [elbowX, elbowY, side * 0.38], 0.063)
            rod('steel', [elbowX, elbowY, side * 0.38], [shoeX, shoeY - 0.12, side * 0.16], 0.045)
            rod('graphite', [baseX - sign * 0.2, baseY + 0.05, side * 0.42], [elbowX + sign * 0.13, elbowY + 0.04, side * 0.3], 0.027)
            rod('steel', [elbowX + sign * 0.13, elbowY + 0.04, side * 0.3], [shoeX - sign * 0.17, shoeY - 0.15, side * 0.12], 0.024)
            cylinder('steel', 0.105, 0.085, [elbowX, elbowY, side * 0.385], 'z', 14)
            const springFrom: Point = [x + sign * 0.56, baseY, side * 0.5]
            const springTo: Point = [x - sign * 0.22, baseY + (raised ? 0.23 : 0.07), side * 0.47]
            rod('rubber', springFrom, springTo, 0.066)
            for (let turn = 0; turn < 9; turn++) {
                const t = turn / 8
                cylinder('steel', 0.072, 0.025, [THREE.MathUtils.lerp(springFrom[0], springTo[0], t), THREE.MathUtils.lerp(springFrom[1], springTo[1], t), side * 0.49], 'x', 10)
            }
        }
        rod('graphite', [elbowX, elbowY, -0.41], [elbowX, elbowY, 0.41], 0.063)
        rod('steel', [baseX, baseY, -0.58], [baseX, baseY, 0.58], 0.061)
        box('graphite', [0.25, 0.085, 1.83], [shoeX, shoeY - 0.075, 0])
        for (const dx of [-0.085, 0.085]) {
            box('copper', [0.054, 0.031, 1.88], [shoeX + dx, shoeY - 0.013, 0])
            for (const side of [-1, 1]) tube('steel', [[shoeX + dx, shoeY - 0.012, side * 0.89],
                [shoeX + dx, shoeY - 0.08, side * 1.015], [shoeX + dx, shoeY - 0.2, side * 1.11]], 0.023, 8)
        }
        rod('copper', [x, roofY + 0.43, 0.94], [baseX, baseY, 0.49], 0.027)
    }

    // Air horns and antennas above each cab complete the visible roof silhouette.
    for (const sign of [-1, 1]) {
        const x = sign * (cabBack + 0.64)
        for (const side of [-1, 1]) {
            cylinder('graphite', 0.06, 0.13, [x, roofY - 0.04, side * 0.3], 'y', 10)
            const horn = new THREE.CylinderGeometry(0.095, 0.045, 0.35, 12)
            horn.rotateZ(-sign * Math.PI / 2)
            horn.translate(x + sign * 0.13, roofY + 0.035, side * 0.3)
            add('steel', horn)
            cylinder('graphite', 0.08, 0.012, [x + sign * 0.312, roofY + 0.035, side * 0.3], 'x', 12)
        }
        cylinder('rubber', 0.092, 0.072, [x - sign * 0.56, roofY + 0.068, 0], 'y', 12)
        rod('graphite', [x - sign * 0.56, roofY + 0.1, 0], [x - sign * 0.56, roofY + 0.42, 0], 0.021)
    }

    const usedFinishes = new Set<Finish>()
    function buildMeshes(target: THREE.Group, batches: Batches, center = 0) {
        for (const [finish, geometries] of batches) {
            const flat = geometries.map(geometry => geometry.index ? geometry.toNonIndexed() : geometry)
            const geometry = mergeGeometries(flat, false)
            for (const source of new Set([...geometries, ...flat])) source.dispose()
            if (!geometry) continue
            geometry.translate(-center, 0, 0)
            geometry.scale(1 / dimensions.length, 1 / dimensions.height, 1 / dimensions.width)
            if (role === 'tail') geometry.rotateY(Math.PI)
            geometry.computeBoundingBox()
            geometry.computeBoundingSphere()
            const material = usedFinishes.has(finish) ? materials[finish].clone() : materials[finish]
            usedFinishes.add(finish)
            const mesh = new THREE.Mesh(geometry, material)
            mesh.name = `loco-${finish}`
            mesh.castShadow = true
            mesh.receiveShadow = true
            target.add(mesh)
        }
    }
    buildMeshes(group, bodyParts)
    for (const bogie of bogies) {
        const pivot = new THREE.Group()
        pivot.position.x = bogie.center / dimensions.length * (role === 'tail' ? -1 : 1)
        pivot.name = pivot.position.x > 0 ? 'bogie-front' : 'bogie-rear'
        pivot.userData.axleCount = 3
        pivot.userData.wheelCount = 6
        buildMeshes(pivot, bogie.parts, bogie.center)
        group.add(pivot)
    }
    for (const finish of Object.keys(materials) as Finish[]) {
        if (!usedFinishes.has(finish)) materials[finish].dispose()
    }
    const [rear, front] = getLocomotiveBogieOffsets(modelId)
    Object.assign(group.userData, {
        emuType: modelId,
        emuVariant: model.variant,
        emuRole: role,
        kind: 'locomotive',
        axleCount: 6,
        wheelCount: 12,
        bogieCount: 2,
        bogieRearX: rear / dimensions.length,
        bogieFrontX: front / dimensions.length,
        passengerWindowCount: 0,
        doorCount: 4,
        pantograph: true,
        pantographCount: 2,
        raisedPantographCount: 1,
        powerCar: true,
    })
    return group
}
