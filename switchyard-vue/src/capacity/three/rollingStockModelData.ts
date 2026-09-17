import * as THREE from 'three'
import type { RollingStockCarRole, RollingStockModelId } from './rollingStock.ts'

export interface RollingStockTemplateSpec {
    modelId: RollingStockModelId
    role: RollingStockCarRole
    carIndex: number
}

interface AttributeData {
    array: THREE.TypedArray
    itemSize: number
    normalized: boolean
}

interface GeometryData {
    attributes: Record<string, AttributeData>
    index: AttributeData | null
    boundingBox: { min: number[]; max: number[] } | null
    boundingSphere: { center: number[]; radius: number } | null
}

interface ObjectData {
    name: string
    position: number[]
    quaternion: number[]
    scale: number[]
    userData: Record<string, unknown>
    castShadow: boolean
    receiveShadow: boolean
    geometry?: number
    material?: number | number[]
    children: ObjectData[]
}

/** Geometry stays in transferable typed arrays instead of JSON number arrays. */
export interface RollingStockModelData {
    geometries: GeometryData[]
    materials: THREE.MaterialJSON[]
    root: ObjectData
}

export interface RollingStockBuildRequest {
    key: string
    spec: RollingStockTemplateSpec
}

export type RollingStockBuildResponse =
    | { key: string; data: RollingStockModelData }
    | { key: string; error: string }

export function rollingStockTemplateKey(spec: RollingStockTemplateSpec): string {
    // Index controls both the number plaque and whether an EMU has a pantograph.
    return `${spec.modelId}:${spec.role}:${spec.carIndex}`
}

export function serializeRollingStockModel(root: THREE.Group): { data: RollingStockModelData; transfer: ArrayBuffer[] } {
    const geometries: GeometryData[] = []
    const materials: THREE.MaterialJSON[] = []
    const geometryIds = new Map<THREE.BufferGeometry, number>()
    const materialIds = new Map<THREE.Material, number>()
    const buffers = new Set<ArrayBuffer>()

    function attributeData(attribute: THREE.BufferAttribute): AttributeData {
        buffers.add(attribute.array.buffer as ArrayBuffer)
        return { array: attribute.array, itemSize: attribute.itemSize, normalized: attribute.normalized }
    }

    function geometryId(geometry: THREE.BufferGeometry): number {
        const existing = geometryIds.get(geometry)
        if (existing !== undefined) return existing
        const id = geometries.length
        geometryIds.set(geometry, id)
        const attributes: Record<string, AttributeData> = {}
        for (const [name, attribute] of Object.entries(geometry.attributes)) {
            if (!(attribute instanceof THREE.BufferAttribute)) throw new Error('Unsupported rolling stock interleaved geometry')
            attributes[name] = attributeData(attribute)
        }
        geometries.push({
            attributes,
            index: geometry.index ? attributeData(geometry.index) : null,
            boundingBox: geometry.boundingBox ? { min: geometry.boundingBox.min.toArray(), max: geometry.boundingBox.max.toArray() } : null,
            boundingSphere: geometry.boundingSphere ? { center: geometry.boundingSphere.center.toArray(), radius: geometry.boundingSphere.radius } : null,
        })
        return id
    }

    function materialId(material: THREE.Material): number {
        const existing = materialIds.get(material)
        if (existing !== undefined) return existing
        const id = materials.length
        materialIds.set(material, id)
        materials.push(material.toJSON())
        return id
    }

    function objectData(object: THREE.Object3D): ObjectData {
        const result: ObjectData = {
            name: object.name,
            position: object.position.toArray(),
            quaternion: object.quaternion.toArray(),
            scale: object.scale.toArray(),
            userData: object.userData,
            castShadow: object.castShadow,
            receiveShadow: object.receiveShadow,
            children: object.children.map(objectData),
        }
        if (object instanceof THREE.Mesh) {
            result.geometry = geometryId(object.geometry)
            result.material = Array.isArray(object.material) ? object.material.map(materialId) : materialId(object.material)
        }
        return result
    }

    const data = { geometries, materials, root: objectData(root) }
    return { data, transfer: [...buffers] }
}

/** Rehydrate buffers without triangulation, vertex processing or array copies. */
export function deserializeRollingStockModel(data: RollingStockModelData): THREE.Group {
    const geometries = data.geometries.map(source => {
        const geometry = new THREE.BufferGeometry()
        for (const [name, attribute] of Object.entries(source.attributes)) {
            geometry.setAttribute(name, new THREE.BufferAttribute(attribute.array, attribute.itemSize, attribute.normalized))
        }
        if (source.index) geometry.setIndex(new THREE.BufferAttribute(source.index.array, source.index.itemSize, source.index.normalized))
        if (source.boundingBox) geometry.boundingBox = new THREE.Box3(
            new THREE.Vector3().fromArray(source.boundingBox.min), new THREE.Vector3().fromArray(source.boundingBox.max),
        )
        if (source.boundingSphere) geometry.boundingSphere = new THREE.Sphere(
            new THREE.Vector3().fromArray(source.boundingSphere.center), source.boundingSphere.radius,
        )
        return geometry
    })
    const loader = new THREE.MaterialLoader()
    const materials = data.materials.map(material => loader.parse(material))

    function objectFromData(source: ObjectData): THREE.Object3D {
        const material = Array.isArray(source.material) ? source.material.map(id => materials[id]!) : materials[source.material!]
        const object = source.geometry === undefined ? new THREE.Group() : new THREE.Mesh(geometries[source.geometry], material)
        object.name = source.name
        object.position.fromArray(source.position)
        object.quaternion.fromArray(source.quaternion)
        object.scale.fromArray(source.scale)
        object.userData = source.userData
        object.castShadow = source.castShadow
        object.receiveShadow = source.receiveShadow
        for (const child of source.children) object.add(objectFromData(child))
        return object
    }

    return objectFromData(data.root) as THREE.Group
}

/** Only template owners call this; clones deliberately share their GPU resources. */
export function disposeRollingStockModel(root: THREE.Group): void {
    const resources = new Set<THREE.BufferGeometry | THREE.Material>()
    root.traverse(object => {
        if (!(object instanceof THREE.Mesh)) return
        resources.add(object.geometry)
        for (const material of Array.isArray(object.material) ? object.material : [object.material]) resources.add(material)
    })
    for (const resource of resources) resource.dispose()
}
