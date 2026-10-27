import * as THREE from 'three'
import { getRailwayDimensions } from './railway.ts'

export interface TrackOccupancyPath {
    id: string
    points: THREE.Vector3[]
    linkId: string
}

const BAND_COUNT = 4

interface LinkMeshes {
    meshes: THREE.Mesh<THREE.BufferGeometry, THREE.MeshBasicMaterial>[]
    colorKey: string
}

function createRibbon(points: readonly THREE.Vector3[], gauge: number) {
    const clean: THREE.Vector3[] = []
    const minimumDistanceSquared = (gauge * 1e-8) ** 2
    for (const point of points) {
        if (!Number.isFinite(point.x) || !Number.isFinite(point.z)) continue
        const flat = new THREE.Vector3(point.x, 0, point.z)
        if (!clean.length || clean[clean.length - 1]!.distanceToSquared(flat) > minimumDistanceSquared) clean.push(flat)
    }
    if (clean.length < 2) return null

    const normals = clean.slice(1).map((point, index) => {
        const tangent = point.clone().sub(clean[index]!).normalize()
        return new THREE.Vector3(-tangent.z, 0, tangent.x)
    })
    const offsets = clean.map((_point, index) => {
        const before = normals[Math.max(0, index - 1)]!
        const after = normals[Math.min(index, normals.length - 1)]!
        const miter = before.clone().add(after)
        if (miter.lengthSq() < 1e-10) return after.clone()
        miter.normalize()
        // Bound joins at sharp schematic corners instead of creating long spikes.
        return miter.multiplyScalar(Math.min(2.5, 1 / Math.max(0.001, miter.dot(after))))
    })
    const width = gauge * 2.05
    const height = getRailwayDimensions(gauge).railTop + gauge * 0.025
    const positions: number[] = []
    const indices: number[] = []
    for (let band = 0; band < BAND_COUNT; band++) {
        const startVertex = positions.length / 3
        for (let index = 0; index < clean.length; index++) {
            const point = clean[index]!
            const offset = offsets[index]!
            for (const edge of [band, band + 1]) {
                const distance = (edge / BAND_COUNT - 0.5) * width
                positions.push(point.x + offset.x * distance, height, point.z + offset.z * distance)
            }
        }
        for (let index = 0; index < clean.length - 1; index++) {
            const vertex = startVertex + index * 2
            indices.push(vertex, vertex + 1, vertex + 2, vertex + 1, vertex + 3, vertex + 2)
        }
    }

    const geometry = new THREE.BufferGeometry()
    geometry.setAttribute('position', new THREE.Float32BufferAttribute(positions, 3))
    geometry.setAttribute('color', new THREE.Float32BufferAttribute(new Float32Array(positions.length).fill(1), 3)
        .setUsage(THREE.DynamicDrawUsage))
    geometry.setIndex(indices)
    geometry.computeBoundingBox()
    geometry.computeBoundingSphere()
    return geometry
}

/** The scene owner disposes this group's geometries and its shared material. */
export function createTrackOccupancyOverlay(paths: readonly TrackOccupancyPath[], gauge: number) {
    const safeGauge = Number.isFinite(gauge) && gauge > 0 ? gauge : 0.001
    const group = new THREE.Group()
    group.name = 'track-circuit-occupancy'
    const links = new Map<string, LinkMeshes>()
    let material: THREE.MeshBasicMaterial | null = null

    for (const path of paths) {
        if (!path.linkId) continue
        const geometry = createRibbon(path.points, safeGauge)
        if (!geometry) continue
        material ||= new THREE.MeshBasicMaterial({
            vertexColors: true,
            transparent: true,
            opacity: 0.35,
            depthTest: true,
            depthWrite: false,
            toneMapped: false,
            side: THREE.DoubleSide,
            polygonOffset: true,
            polygonOffsetFactor: -1,
            polygonOffsetUnits: -1,
        })
        const mesh = new THREE.Mesh(geometry, material)
        mesh.name = `track-occupancy-${path.id}`
        mesh.userData.linkId = path.linkId
        mesh.userData.pathId = path.id
        mesh.visible = false
        mesh.renderOrder = 2
        group.add(mesh)
        let entry = links.get(path.linkId)
        if (!entry) {
            entry = { meshes: [], colorKey: '' }
            links.set(path.linkId, entry)
        }
        entry.meshes.push(mesh)
    }

    function update(colorsByLink: ReadonlyMap<string, readonly string[]>) {
        for (const [linkId, entry] of links) {
            const colors = Array.from(new Set((colorsByLink.get(linkId) || [])
                .map(color => color.trim()).filter(Boolean))).slice(0, BAND_COUNT)
            const visible = colors.length > 0
            for (const mesh of entry.meshes) mesh.visible = visible
            if (!visible) continue

            const colorKey = JSON.stringify(colors)
            if (entry.colorKey === colorKey) continue
            entry.colorKey = colorKey
            const bandColors = Array.from({ length: BAND_COUNT }, (_, band) =>
                new THREE.Color(colors[Math.floor(band * colors.length / BAND_COUNT)]!))
            for (const mesh of entry.meshes) {
                const attribute = mesh.geometry.getAttribute('color') as THREE.BufferAttribute
                const verticesPerBand = attribute.count / BAND_COUNT
                for (let band = 0; band < BAND_COUNT; band++) {
                    const color = bandColors[band]!
                    const end = (band + 1) * verticesPerBand
                    for (let vertex = band * verticesPerBand; vertex < end; vertex++) {
                        attribute.setXYZ(vertex, color.r, color.g, color.b)
                    }
                }
                attribute.needsUpdate = true
            }
        }
    }

    return { group, update }
}
