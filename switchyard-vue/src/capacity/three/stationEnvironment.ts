import * as THREE from 'three'
import { RoomEnvironment } from 'three/examples/jsm/environments/RoomEnvironment.js'
import { mergeGeometries } from 'three/examples/jsm/utils/BufferGeometryUtils.js'
import { getRailwayDimensions } from './railway'

/** Local, generated lighting: no HDR download or network dependency. */
export function lightStationScene(scene: THREE.Scene, renderer: THREE.WebGLRenderer) {
    scene.background = new THREE.Color(0xdce5e8)
    scene.fog = new THREE.Fog(0xdce5e8, 280, 1100)
    renderer.outputColorSpace = THREE.SRGBColorSpace
    renderer.toneMapping = THREE.ACESFilmicToneMapping
    renderer.toneMappingExposure = 1.12
    renderer.shadowMap.enabled = true
    renderer.shadowMap.type = THREE.PCFShadowMap

    const room = new RoomEnvironment()
    const generator = new THREE.PMREMGenerator(renderer)
    const environment = generator.fromScene(room, 0.04)
    scene.environment = environment.texture
    scene.environmentIntensity = 0.42
    room.dispose()
    generator.dispose()

    const sky = new THREE.HemisphereLight(0xe7f2ff, 0x8e927c, 1.65)
    const sun = new THREE.DirectionalLight(0xfff2db, 3.1)
    sun.name = 'station-sun'
    sun.position.set(-75, 130, 65)
    sun.castShadow = true
    sun.shadow.mapSize.set(2048, 2048)
    Object.assign(sun.shadow.camera, { near: 1, far: 430, left: -110, right: 110, top: 110, bottom: -110 })
    sun.shadow.normalBias = 0.025
    sun.shadow.bias = -0.00015
    sun.shadow.camera.updateProjectionMatrix()
    scene.add(sky, sun)
    return () => {
        scene.environment = null
        environment.dispose()
        sun.shadow.dispose()
        scene.remove(sky, sun)
    }
}

/** Keep stretched station layouts inside the sun's shadow volume and beyond the fog start. */
export function fitStationLighting(scene: THREE.Scene, worldWidth: number, worldDepth: number, viewDistance: number) {
    const positiveSize = (value: number) => Number.isFinite(value) ? Math.max(0, value) : 0
    const radius = Math.hypot(positiveSize(worldWidth), positiveSize(worldDepth)) / 2
    const shadowExtent = Math.max(110, radius + 15)
    const lightScale = shadowExtent / 110
    const sun = scene.getObjectByName('station-sun')

    if (sun instanceof THREE.DirectionalLight) {
        // Moving along the same ray preserves the original lighting direction and intensity.
        sun.position.set(-75 * lightScale, 130 * lightScale, 65 * lightScale)
        Object.assign(sun.shadow.camera, {
            near: 1,
            far: 430 * lightScale,
            left: -shadowExtent,
            right: shadowExtent,
            top: shadowExtent,
            bottom: -shadowExtent,
        })
        sun.shadow.camera.updateProjectionMatrix()
        sun.shadow.needsUpdate = true
    }

    if (scene.fog instanceof THREE.Fog) {
        // Retain the original distances whenever they already cover the visible station.
        scene.fog.near = Math.max(280, positiveSize(viewDistance) + radius * 1.2)
        scene.fog.far = Math.max(1100, scene.fog.near + Math.max(820, radius * 4))
    }
}

function surfaceTexture(kind: 'ground' | 'paving') {
    const canvas = document.createElement('canvas')
    canvas.width = canvas.height = 256
    const context = canvas.getContext('2d')!
    let seed = 73
    const random = () => { seed = (seed * 1664525 + 1013904223) >>> 0; return seed / 4294967296 }
    context.fillStyle = kind === 'ground' ? '#a6aa91' : '#bec2bf'
    context.fillRect(0, 0, 256, 256)
    for (let i = 0; i < 12000; i++) {
        const value = Math.floor(random() * 55 + (kind === 'ground' ? 118 : 158))
        context.fillStyle = `rgba(${value},${value + 4},${value - 7},0.22)`
        context.fillRect(random() * 256, random() * 256, 1 + random() * 2, 1 + random() * 2)
    }
    if (kind === 'paving') {
        context.strokeStyle = '#959e9e'
        context.lineWidth = 1
        for (let row = 0; row < 8; row++) {
            context.beginPath()
            context.moveTo(0, row * 32)
            context.lineTo(256, row * 32)
            for (let col = -1; col < 5; col++) {
                const x = col * 64 + (row % 2) * 32
                context.moveTo(x, row * 32)
                context.lineTo(x, row * 32 + 32)
            }
            context.stroke()
        }
    }
    const texture = new THREE.CanvasTexture(canvas)
    texture.wrapS = texture.wrapT = THREE.RepeatWrapping
    texture.colorSpace = THREE.SRGBColorSpace
    texture.anisotropy = 8
    return texture
}

export function createStationGround(width: number, depth: number) {
    const texture = surfaceTexture('ground')
    texture.repeat.set(width / 12, depth / 12)
    const ground = new THREE.Mesh(new THREE.PlaneGeometry(width, depth),
        new THREE.MeshStandardMaterial({ map: texture, color: 0xc7c9b6, roughness: 1 }))
    ground.rotation.x = -Math.PI / 2
    ground.position.y = -0.012
    ground.receiveShadow = true
    return ground
}

/** Dimensioned from rail gauge, with platforms kept inside their original footprint. */
export function createStationPlatform(width: number, depth: number, gauge: number) {
    const group = new THREE.Group()
    group.name = 'Passenger platform'
    const long = Math.max(width, depth)
    const short = Math.min(width, depth)
    const top = getRailwayDimensions(gauge).railTop + gauge * 0.87
    const batches = new Map<THREE.Material, THREE.BufferGeometry[]>()
    const concrete = new THREE.MeshStandardMaterial({ color: 0xadb3b0, roughness: 0.9 })
    const coping = new THREE.MeshStandardMaterial({ color: 0xe3e4dc, roughness: 0.8 })
    const yellow = new THREE.MeshStandardMaterial({ color: 0xdcb85d, roughness: 0.88 })
    const steel = new THREE.MeshStandardMaterial({ color: 0x586a72, metalness: 0.65, roughness: 0.45 })
    const roof = new THREE.MeshStandardMaterial({ color: 0xc7d0d0, metalness: 0.35, roughness: 0.5 })
    const trim = new THREE.MeshStandardMaterial({ color: 0x365468, metalness: 0.4, roughness: 0.5 })
    const box = (x: number, y: number, z: number, w: number, h: number, d: number, material: THREE.Material) => {
        const geometry = new THREE.BoxGeometry(w, h, d).translate(x, y, z)
        const bucket = batches.get(material) || []
        bucket.push(geometry)
        batches.set(material, bucket)
    }
    box(0, top / 2, 0, long, top, short, concrete)
    const pavingMap = surfaceTexture('paving')
    pavingMap.repeat.set(long / (gauge * 4), short / (gauge * 4))
    const surface = new THREE.Mesh(new THREE.PlaneGeometry(long, short),
        new THREE.MeshStandardMaterial({ map: pavingMap, roughness: 0.94 }))
    surface.rotation.x = -Math.PI / 2
    surface.position.y = top + gauge * 0.004
    surface.receiveShadow = true
    group.add(surface)
    const edgeWidth = Math.min(gauge * 0.22, short * 0.1)
    for (const side of [-1, 1]) {
        box(0, top + gauge * 0.017, side * (short / 2 - edgeWidth / 2), long, gauge * 0.034, edgeWidth, coping)
        box(0, top + gauge * 0.013, side * (short / 2 - edgeWidth * 1.7), long - gauge * 0.1,
            gauge * 0.026, edgeWidth * 0.55, yellow)
        // Coping joints and tactile ribs are batched, not individual draw calls.
        const tiles = Math.min(700, Math.ceil(long / (gauge * 0.65)))
        for (let i = 0; i < tiles; i++) {
            const x = (i + 0.5) / tiles * long - long / 2
            box(x, top + gauge * 0.04, side * (short / 2 - edgeWidth * 1.7), gauge * 0.018,
                gauge * 0.015, edgeWidth * 0.42, yellow)
        }
    }
    if (short > gauge * 1.3 && long > gauge * 8) {
        const canopyLength = long * 0.68
        const canopyWidth = short * 0.7
        const canopyY = top + gauge * 3.25
        box(0, canopyY, 0, canopyLength, gauge * 0.12, canopyWidth, roof)
        for (const side of [-1, 1]) {
            box(0, canopyY - gauge * 0.09, side * canopyWidth * 0.5,
                canopyLength, gauge * 0.19, gauge * 0.07, trim)
            box(0, canopyY + gauge * 0.074, side * canopyWidth * 0.16,
                canopyLength * 0.92, gauge * 0.025, gauge * 0.2, coping)
        }
        const bays = Math.max(2, Math.ceil(canopyLength / (gauge * 6)))
        for (let i = 0; i < bays; i++) {
            const x = (i + 0.5) / bays * canopyLength - canopyLength / 2
            box(x, (top + canopyY) / 2, 0, gauge * 0.13, canopyY - top, gauge * 0.13, steel)
            box(x, top + gauge * 0.08, 0, gauge * 0.32, gauge * 0.16, gauge * 0.32, coping)
            box(x, canopyY - gauge * 0.17, 0, gauge * 0.13, gauge * 0.19, canopyWidth * 0.94, steel)
            if (i % 2 === 0) {
                box(x + gauge * 0.8, top + gauge * 0.38, 0, gauge * 1.2, gauge * 0.09, gauge * 0.42, trim)
                for (const sign of [-1, 1])
                    box(x + gauge * (0.8 + sign * 0.43), top + gauge * 0.17, 0, gauge * 0.08, gauge * 0.34, gauge * 0.32, steel)
            }
        }
    }
    for (const [material, geometries] of batches) {
        const merged = mergeGeometries(geometries)
        geometries.forEach(geometry => geometry.dispose())
        if (!merged) continue
        const mesh = new THREE.Mesh(merged, material)
        mesh.castShadow = mesh.receiveShadow = true
        group.add(mesh)
    }
    // Materials for omitted furniture are not owned by a mesh.
    for (const material of [concrete, coping, yellow, steel, roof, trim])
        if (!batches.has(material)) material.dispose()
    if (depth > width) group.rotation.y = Math.PI / 2
    return group
}
