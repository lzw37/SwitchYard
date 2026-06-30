<template>
    <section class="station-layout-3d-page" v-loading="loadingData">
        <div class="layout3d-toolbar">
            <div class="layout3d-metrics" aria-live="polite">
                <span class="metric-item">
                    <span class="metric-label">{{ t('stationLayout3d.metrics.tracks') }}</span>
                    <strong>{{ layoutStats.tracks }}</strong>
                </span>
                <span class="metric-item">
                    <span class="metric-label">{{ t('stationLayout3d.metrics.signals') }}</span>
                    <strong>{{ layoutStats.signals }}</strong>
                </span>
                <span class="metric-item">
                    <span class="metric-label">{{ t('stationLayout3d.metrics.platforms') }}</span>
                    <strong>{{ layoutStats.platforms }}</strong>
                </span>
            </div>

            <div class="layout3d-actions">
                <el-tooltip :content="t('stationLayout3d.buttons.refresh')">
                    <el-button
                        size="small"
                        :icon="RefreshRight"
                        :disabled="!selectedInstanceId || loadingData"
                        @click="loadLayout"
                    />
                </el-tooltip>
                <el-tooltip :content="t('stationLayout3d.buttons.resetView')">
                    <el-button size="small" :icon="Aim" :disabled="!canRender" @click="resetCamera" />
                </el-tooltip>
                <el-checkbox v-model="showLabels" size="small">
                    {{ t('stationLayout3d.labels.showLabels') }}
                </el-checkbox>
            </div>
        </div>

        <div
            ref="canvasWrapperRef"
            class="layout3d-body"
            :class="{ 'hide-layout-labels': !showLabels, 'is-empty': !canRender }"
        >
            <canvas ref="canvasRef" class="layout3d-canvas" data-testid="station-layout-3d-canvas" />
            <div v-if="!canRender && !loadingData" class="layout3d-empty">
                {{ emptyStateText }}
            </div>
        </div>
    </section>
</template>

<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { ElMessage } from 'element-plus'
import { Aim, RefreshRight } from '@element-plus/icons-vue'
import * as THREE from 'three'
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js'
import { CSS2DObject, CSS2DRenderer } from 'three/examples/jsm/renderers/CSS2DRenderer.js'
import axios from '@/utils/axios'

interface Props {
    selectedInstanceId?: string | null
    activationKey?: number
}

interface Position2D {
    x: number
    y: number
}

interface Track {
    id: string
    name: string
    x1: number
    y1: number
    x2: number
    y2: number
    fromNodeID: string
    toNodeID: string
}

interface TrackSegment {
    id: string
    line: Track
    x1: number
    y1: number
    x2: number
    y2: number
}

interface CurveTrack {
    id: string
    nodeID: string
    tangentLinkID1: string
    tangentLinkID2: string
    radius: number
    start: Position2D
    end: Position2D
    center: Position2D
    largeArcFlag: number
    sweepFlag: number
}

interface NodePoint {
    id: string
    x: number
    y: number
}

interface Signal {
    id: string
    name: string
    type: string
    position: Position2D
    direction: string
    bindingNodeID: string
}

interface Platform {
    id: string
    name: string
    x: number
    y: number
    width: number
    height: number
}

interface SwitchDevice {
    id: string
    name: string
    type: string
    position: Position2D
    bindingNodeID: string
}

interface StationLayoutData {
    tracks: Track[]
    curves: CurveTrack[]
    nodes: NodePoint[]
    signals: Signal[]
    platforms: Platform[]
    switches: SwitchDevice[]
}

interface LayoutBounds {
    minX: number
    minY: number
    maxX: number
    maxY: number
}

interface LayoutMapper {
    centerX: number
    centerY: number
    scale: number
    worldWidth: number
    worldDepth: number
    mapPoint: (point: Position2D, y?: number) => THREE.Vector3
    mapLength: (value: number) => number
}

interface SceneMaterials {
    ground: THREE.MeshStandardMaterial
    ballast: THREE.MeshStandardMaterial
    rail: THREE.MeshStandardMaterial
    sleeper: THREE.MeshStandardMaterial
    platform: THREE.MeshStandardMaterial
    platformLine: THREE.MeshStandardMaterial
    signalPost: THREE.MeshStandardMaterial
    signalHead: THREE.MeshStandardMaterial
    signalRed: THREE.MeshStandardMaterial
    signalGreen: THREE.MeshStandardMaterial
    signalWhite: THREE.MeshStandardMaterial
    switchMarker: THREE.MeshStandardMaterial
}

const props = withDefaults(defineProps<Props>(), {
    selectedInstanceId: null,
    activationKey: 0,
})

const { t } = useI18n()

const TARGET_WORLD_SPAN = 170
const MIN_WORLD_SPAN = 48
const TRACK_GAUGE = 0.95
const BALLAST_HEIGHT = 0.14
const BALLAST_WIDTH = 2.25
const RAIL_HEIGHT = 0.08
const RAIL_WIDTH = 0.08
const RAIL_Y = 0.28
const SLEEPER_Y = 0.19
const SLEEPER_WIDTH = 0.18
const SLEEPER_HEIGHT = 0.08
const SLEEPER_LENGTH = 1.65
const SLEEPER_SPACING = 1.25
const MAX_SLEEPERS_PER_SEGMENT = 72
const PLATFORM_MIN_SIZE = 1.2

const canvasWrapperRef = ref<HTMLElement | null>(null)
const canvasRef = ref<HTMLCanvasElement | null>(null)
const layoutData = ref<StationLayoutData>(createEmptyLayout())
const loadingData = ref(false)
const loadErrorMessage = ref('')
const showLabels = ref(true)

let renderer: THREE.WebGLRenderer | null = null
let labelRenderer: CSS2DRenderer | null = null
let labelRendererRoot: HTMLElement | null = null
let scene: THREE.Scene | null = null
let camera: THREE.PerspectiveCamera | null = null
let controls: OrbitControls | null = null
let layoutGroup: THREE.Group | null = null
let resizeObserver: ResizeObserver | null = null
let rafId: number | null = null
let lastMapper: LayoutMapper | null = null
let layoutLoadVersion = 0
let lastWrapperWidth = 0
let lastWrapperHeight = 0

const selectedInstanceId = computed(() => props.selectedInstanceId || '')
const layoutStats = computed(() => ({
    tracks: layoutData.value.tracks.length,
    signals: layoutData.value.signals.length,
    platforms: layoutData.value.platforms.length,
}))
const canRender = computed(() =>
    layoutStats.value.tracks > 0 ||
    layoutStats.value.signals > 0 ||
    layoutStats.value.platforms > 0 ||
    layoutData.value.switches.length > 0
)
const emptyStateText = computed(() => {
    if (!selectedInstanceId.value) return t('capacityMain.placeholders.selectInstance')
    if (loadErrorMessage.value) return loadErrorMessage.value
    return t('stationLayout3d.messages.empty')
})

function createEmptyLayout(): StationLayoutData {
    return {
        tracks: [],
        curves: [],
        nodes: [],
        signals: [],
        platforms: [],
        switches: [],
    }
}

function toFiniteNumber(value: unknown, fallback = 0): number {
    const parsed = Number(value)
    return Number.isFinite(parsed) ? parsed : fallback
}

function toFiniteNumberOrNull(value: unknown): number | null {
    const parsed = Number(value)
    return Number.isFinite(parsed) ? parsed : null
}

function readString(source: any, ...keys: string[]): string {
    for (const key of keys) {
        const value = source?.[key]
        if (typeof value === 'string') return value
        if (value !== undefined && value !== null) return String(value)
    }
    return ''
}

function normalizePosition(source: any): Position2D | null {
    if (!source) return null
    const x = toFiniteNumberOrNull(source.x ?? source.X)
    const y = toFiniteNumberOrNull(source.y ?? source.Y)
    if (x === null || y === null) return null
    return { x, y }
}

function normalizeNamedValue(id: string, name: string): string {
    const trimmedName = name.trim()
    return trimmedName || id
}

function normalizeTrack(item: any, index: number): Track | null {
    const x1 = toFiniteNumberOrNull(item?.x1 ?? item?.X1)
    const y1 = toFiniteNumberOrNull(item?.y1 ?? item?.Y1)
    const x2 = toFiniteNumberOrNull(item?.x2 ?? item?.X2)
    const y2 = toFiniteNumberOrNull(item?.y2 ?? item?.Y2)
    if (x1 === null || y1 === null || x2 === null || y2 === null) return null

    const id = readString(item, 'id', 'ID') || `track-${index + 1}`
    return {
        id,
        name: readString(item, 'name', 'Name'),
        x1,
        y1,
        x2,
        y2,
        fromNodeID: readString(item, 'fromNodeID', 'FromNodeID'),
        toNodeID: readString(item, 'toNodeID', 'ToNodeID'),
    }
}

function normalizeCurveTrack(item: any, index: number): CurveTrack | null {
    const start = normalizePosition(item?.start ?? item?.Start)
    const end = normalizePosition(item?.end ?? item?.End)
    const center = normalizePosition(item?.center ?? item?.Center)
    if (!start || !end || !center) return null

    const fallbackRadius = Math.hypot(start.x - center.x, start.y - center.y)
    const radius = Math.max(0.001, toFiniteNumber(item?.radius ?? item?.Radius, fallbackRadius))

    return {
        id: readString(item, 'id', 'ID') || `curve-${index + 1}`,
        nodeID: readString(item, 'nodeID', 'NodeID'),
        tangentLinkID1: readString(item, 'tangentLinkID1', 'TangentLinkID1'),
        tangentLinkID2: readString(item, 'tangentLinkID2', 'TangentLinkID2'),
        radius,
        start,
        end,
        center,
        largeArcFlag: Number(item?.largeArcFlag ?? item?.LargeArcFlag) === 1 ? 1 : 0,
        sweepFlag: Number(item?.sweepFlag ?? item?.SweepFlag) === 1 ? 1 : 0,
    }
}

function normalizeNode(item: any, index: number): NodePoint | null {
    const x = toFiniteNumberOrNull(item?.x ?? item?.X)
    const y = toFiniteNumberOrNull(item?.y ?? item?.Y)
    if (x === null || y === null) return null
    return {
        id: readString(item, 'id', 'ID') || `node-${index + 1}`,
        x,
        y,
    }
}

function normalizeSignal(item: any, index: number): Signal | null {
    const position = normalizePosition(item?.position ?? item?.Position) || normalizePosition(item)
    if (!position) return null

    const id = readString(item, 'id', 'ID') || `signal-${index + 1}`
    const name = normalizeNamedValue(id, readString(item, 'name', 'Name'))
    return {
        id,
        name,
        type: readString(item, 'type', 'Type'),
        position,
        direction: readString(item, 'direction', 'Direction') || 'e',
        bindingNodeID: readString(item, 'bindingNodeID', 'BindingNodeID'),
    }
}

function normalizePlatform(item: any, index: number): Platform | null {
    const x = toFiniteNumberOrNull(item?.x ?? item?.X)
    const y = toFiniteNumberOrNull(item?.y ?? item?.Y)
    if (x === null || y === null) return null

    const id = readString(item, 'id', 'ID') || `platform-${index + 1}`
    const name = normalizeNamedValue(id, readString(item, 'name', 'Name'))
    return {
        id,
        name,
        x,
        y,
        width: Math.abs(toFiniteNumber(item?.width ?? item?.Width, 0)),
        height: Math.abs(toFiniteNumber(item?.height ?? item?.Height, 0)),
    }
}

function normalizeSwitch(item: any, index: number): SwitchDevice | null {
    const position = normalizePosition(item?.position ?? item?.Position) || normalizePosition(item)
    if (!position) return null

    const id = readString(item, 'id', 'ID') || `switch-${index + 1}`
    const name = normalizeNamedValue(id, readString(item, 'name', 'Name'))
    return {
        id,
        name,
        type: readString(item, 'type', 'Type'),
        position,
        bindingNodeID: readString(item, 'bindingNodeID', 'BindingNodeID'),
    }
}

function normalizeLayout(payload: any): StationLayoutData {
    const tracksRaw = Array.isArray(payload?.tracks) ? payload.tracks : []
    const curvesRaw = Array.isArray(payload?.curves) ? payload.curves : []
    const nodesRaw = Array.isArray(payload?.nodes) ? payload.nodes : []
    const signalsRaw = Array.isArray(payload?.signals) ? payload.signals : []
    const platformsRaw = Array.isArray(payload?.platforms) ? payload.platforms : []
    const switchesRaw = Array.isArray(payload?.switches) ? payload.switches : []

    return {
        tracks: tracksRaw
            .map((item: any, index: number) => normalizeTrack(item, index))
            .filter((item: Track | null): item is Track => item !== null),
        curves: curvesRaw
            .map((item: any, index: number) => normalizeCurveTrack(item, index))
            .filter((item: CurveTrack | null): item is CurveTrack => item !== null),
        nodes: nodesRaw
            .map((item: any, index: number) => normalizeNode(item, index))
            .filter((item: NodePoint | null): item is NodePoint => item !== null),
        signals: signalsRaw
            .map((item: any, index: number) => normalizeSignal(item, index))
            .filter((item: Signal | null): item is Signal => item !== null),
        platforms: platformsRaw
            .map((item: any, index: number) => normalizePlatform(item, index))
            .filter((item: Platform | null): item is Platform => item !== null),
        switches: switchesRaw
            .map((item: any, index: number) => normalizeSwitch(item, index))
            .filter((item: SwitchDevice | null): item is SwitchDevice => item !== null),
    }
}

function includeBoundsPoint(bounds: LayoutBounds, point: Position2D) {
    bounds.minX = Math.min(bounds.minX, point.x)
    bounds.minY = Math.min(bounds.minY, point.y)
    bounds.maxX = Math.max(bounds.maxX, point.x)
    bounds.maxY = Math.max(bounds.maxY, point.y)
}

function collectBounds(layout: StationLayoutData): LayoutBounds | null {
    const bounds: LayoutBounds = {
        minX: Infinity,
        minY: Infinity,
        maxX: -Infinity,
        maxY: -Infinity,
    }

    for (const track of layout.tracks) {
        includeBoundsPoint(bounds, { x: track.x1, y: track.y1 })
        includeBoundsPoint(bounds, { x: track.x2, y: track.y2 })
    }
    for (const curve of layout.curves) {
        for (const point of buildCurveSamplePoints(curve, 20)) includeBoundsPoint(bounds, point)
    }
    for (const signal of layout.signals) includeBoundsPoint(bounds, signal.position)
    for (const sw of layout.switches) includeBoundsPoint(bounds, sw.position)
    for (const platform of layout.platforms) {
        includeBoundsPoint(bounds, { x: platform.x, y: platform.y })
        includeBoundsPoint(bounds, { x: platform.x + platform.width, y: platform.y + platform.height })
    }

    if (!Number.isFinite(bounds.minX) || !Number.isFinite(bounds.minY)) return null
    return bounds
}

function createMapper(layout: StationLayoutData): LayoutMapper | null {
    const bounds = collectBounds(layout)
    if (!bounds) return null

    const width = Math.max(1, bounds.maxX - bounds.minX)
    const depth = Math.max(1, bounds.maxY - bounds.minY)
    const sourceSpan = Math.max(width, depth, 1)
    const scale = sourceSpan / TARGET_WORLD_SPAN
    const centerX = (bounds.minX + bounds.maxX) / 2
    const centerY = (bounds.minY + bounds.maxY) / 2

    return {
        centerX,
        centerY,
        scale,
        worldWidth: Math.max(MIN_WORLD_SPAN, width / scale),
        worldDepth: Math.max(MIN_WORLD_SPAN, depth / scale),
        mapPoint: (point: Position2D, y = 0) =>
            new THREE.Vector3((point.x - centerX) / scale, y, -(point.y - centerY) / scale),
        mapLength: (value: number) => Math.abs(value) / scale,
    }
}

function getLinePointAtRate(line: Track, rate: number): Position2D {
    return {
        x: line.x1 + (line.x2 - line.x1) * rate,
        y: line.y1 + (line.y2 - line.y1) * rate,
    }
}

function getPointRateOnLine(line: Track, point: Position2D): number | null {
    const dx = line.x2 - line.x1
    const dy = line.y2 - line.y1
    const lengthSquared = dx * dx + dy * dy
    if (lengthSquared <= 0) return null

    const rawRate = ((point.x - line.x1) * dx + (point.y - line.y1) * dy) / lengthSquared
    if (!Number.isFinite(rawRate)) return null
    return Math.max(0, Math.min(1, rawRate))
}

function mergeHiddenRateRanges(ranges: Array<{ start: number; end: number }>) {
    const normalizedRanges = ranges
        .map((range) => ({
            start: Math.max(0, Math.min(1, Math.min(range.start, range.end))),
            end: Math.max(0, Math.min(1, Math.max(range.start, range.end))),
        }))
        .filter((range) => range.end - range.start > 0.000001)
        .sort((a, b) => a.start - b.start)

    const merged: Array<{ start: number; end: number }> = []
    for (const range of normalizedRanges) {
        const previous = merged[merged.length - 1]
        if (previous && range.start <= previous.end + 0.000001) {
            previous.end = Math.max(previous.end, range.end)
        } else {
            merged.push({ ...range })
        }
    }

    return merged
}

function buildVisibleRateRanges(hiddenRanges: Array<{ start: number; end: number }>) {
    const mergedHiddenRanges = mergeHiddenRateRanges(hiddenRanges)
    const visibleRanges: Array<{ start: number; end: number }> = []
    let cursor = 0

    for (const hiddenRange of mergedHiddenRanges) {
        if (hiddenRange.start > cursor + 0.000001) {
            visibleRanges.push({ start: cursor, end: hiddenRange.start })
        }
        cursor = Math.max(cursor, hiddenRange.end)
    }

    if (cursor < 1 - 0.000001) {
        visibleRanges.push({ start: cursor, end: 1 })
    }

    return visibleRanges
}

function addCurveHiddenRange(
    hiddenRangesByLineID: Map<string, Array<{ start: number; end: number }>>,
    lineByID: Map<string, Track>,
    nodeByID: Map<string, NodePoint>,
    curve: CurveTrack,
    tangentLinkIDKey: 'tangentLinkID1' | 'tangentLinkID2',
    tangentPointKey: 'start' | 'end',
) {
    const lineID = curve[tangentLinkIDKey]
    const line = lineByID.get(lineID)
    const node = nodeByID.get(curve.nodeID)
    if (!line || !node) return

    const nodeRate = getPointRateOnLine(line, node)
    const tangentRate = getPointRateOnLine(line, curve[tangentPointKey])
    if (nodeRate === null || tangentRate === null) return

    if (!hiddenRangesByLineID.has(line.id)) {
        hiddenRangesByLineID.set(line.id, [])
    }
    hiddenRangesByLineID.get(line.id)?.push({ start: nodeRate, end: tangentRate })
}

function buildVisibleTrackSegments(layout: StationLayoutData): TrackSegment[] {
    const lineByID = new Map(layout.tracks.map((line) => [line.id, line]))
    const nodeByID = new Map(layout.nodes.map((node) => [node.id, node]))
    const hiddenRangesByLineID = new Map<string, Array<{ start: number; end: number }>>()

    for (const curve of layout.curves) {
        addCurveHiddenRange(hiddenRangesByLineID, lineByID, nodeByID, curve, 'tangentLinkID1', 'start')
        addCurveHiddenRange(hiddenRangesByLineID, lineByID, nodeByID, curve, 'tangentLinkID2', 'end')
    }

    const segments: TrackSegment[] = []
    for (const line of layout.tracks) {
        const visibleRanges = buildVisibleRateRanges(hiddenRangesByLineID.get(line.id) || [])
        visibleRanges.forEach((range, index) => {
            const start = getLinePointAtRate(line, range.start)
            const end = getLinePointAtRate(line, range.end)
            segments.push({
                id: `${line.id}-visible-${index}`,
                line,
                x1: start.x,
                y1: start.y,
                x2: end.x,
                y2: end.y,
            })
        })
    }

    return segments
}

function buildCurveSamplePoints(curve: CurveTrack, preferredSegments = 24): Position2D[] {
    const startAngle = Math.atan2(curve.start.y - curve.center.y, curve.start.x - curve.center.x)
    const endAngle = Math.atan2(curve.end.y - curve.center.y, curve.end.x - curve.center.x)
    let delta = endAngle - startAngle

    if (curve.sweepFlag === 1 && delta < 0) delta += Math.PI * 2
    if (curve.sweepFlag === 0 && delta > 0) delta -= Math.PI * 2

    const absoluteDelta = Math.abs(delta)
    if (curve.largeArcFlag === 1 && absoluteDelta < Math.PI) {
        delta += delta >= 0 ? Math.PI * 2 : -Math.PI * 2
    } else if (curve.largeArcFlag === 0 && absoluteDelta > Math.PI) {
        delta += delta >= 0 ? -Math.PI * 2 : Math.PI * 2
    }

    const segmentCount = Math.max(8, Math.min(56, Math.ceil(Math.abs(delta) / (Math.PI / preferredSegments))))
    const points: Position2D[] = []
    for (let i = 0; i <= segmentCount; i++) {
        const rate = i / segmentCount
        const angle = startAngle + delta * rate
        points.push({
            x: curve.center.x + Math.cos(angle) * curve.radius,
            y: curve.center.y + Math.sin(angle) * curve.radius,
        })
    }

    points[0] = curve.start
    points[points.length - 1] = curve.end
    return points
}

function createMaterials(): SceneMaterials {
    return {
        ground: new THREE.MeshStandardMaterial({ color: 0xb8c7b2, roughness: 1, metalness: 0 }),
        ballast: new THREE.MeshStandardMaterial({ color: 0x6c7375, roughness: 0.95, metalness: 0 }),
        rail: new THREE.MeshStandardMaterial({ color: 0x43484d, roughness: 0.42, metalness: 0.75 }),
        sleeper: new THREE.MeshStandardMaterial({ color: 0x5c4532, roughness: 0.9, metalness: 0.05 }),
        platform: new THREE.MeshStandardMaterial({ color: 0x8fb3c8, roughness: 0.85, metalness: 0.05 }),
        platformLine: new THREE.MeshStandardMaterial({ color: 0xf4d35e, roughness: 0.8, metalness: 0 }),
        signalPost: new THREE.MeshStandardMaterial({ color: 0x2e343b, roughness: 0.65, metalness: 0.45 }),
        signalHead: new THREE.MeshStandardMaterial({ color: 0x111827, roughness: 0.7, metalness: 0.2 }),
        signalRed: new THREE.MeshStandardMaterial({ color: 0xe11d48, emissive: 0x7f0018, emissiveIntensity: 0.45 }),
        signalGreen: new THREE.MeshStandardMaterial({ color: 0x22c55e, emissive: 0x0a5c2b, emissiveIntensity: 0.35 }),
        signalWhite: new THREE.MeshStandardMaterial({ color: 0xf8fafc, emissive: 0x64748b, emissiveIntensity: 0.18 }),
        switchMarker: new THREE.MeshStandardMaterial({ color: 0xf59e0b, roughness: 0.55, metalness: 0.15 }),
    }
}

function initThree() {
    if (!canvasRef.value || !canvasWrapperRef.value || renderer) return

    const width = Math.max(1, canvasWrapperRef.value.clientWidth)
    const height = Math.max(1, canvasWrapperRef.value.clientHeight)

    scene = new THREE.Scene()
    scene.background = new THREE.Color(0xe7edf5)
    scene.fog = new THREE.Fog(0xe7edf5, 200, 950)

    camera = new THREE.PerspectiveCamera(52, width / height, 0.1, 3000)
    camera.position.set(90, 62, 92)

    renderer = new THREE.WebGLRenderer({ canvas: canvasRef.value, antialias: true })
    renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2))
    renderer.setSize(width, height, false)
    renderer.shadowMap.enabled = true
    renderer.shadowMap.type = THREE.PCFSoftShadowMap

    labelRenderer = new CSS2DRenderer()
    labelRenderer.setSize(width, height)
    labelRenderer.domElement.style.position = 'absolute'
    labelRenderer.domElement.style.left = '0'
    labelRenderer.domElement.style.top = '0'
    labelRenderer.domElement.style.pointerEvents = 'none'
    canvasWrapperRef.value.appendChild(labelRenderer.domElement)
    labelRendererRoot = labelRenderer.domElement

    controls = new OrbitControls(camera, renderer.domElement)
    controls.enableDamping = true
    controls.dampingFactor = 0.08
    controls.minDistance = 8
    controls.maxDistance = 900
    controls.maxPolarAngle = Math.PI * 0.49

    scene.add(new THREE.HemisphereLight(0xffffff, 0x6b7280, 0.7))

    const sun = new THREE.DirectionalLight(0xffffff, 1.05)
    sun.position.set(140, 190, 110)
    sun.castShadow = true
    sun.shadow.mapSize.set(2048, 2048)
    sun.shadow.camera.near = 20
    sun.shadow.camera.far = 620
    sun.shadow.camera.left = -210
    sun.shadow.camera.right = 210
    sun.shadow.camera.top = 210
    sun.shadow.camera.bottom = -210
    scene.add(sun)

    layoutGroup = new THREE.Group()
    scene.add(layoutGroup)

    rebuildScene()
    ensureRafLoop()
}

function disposeObject3D(obj: THREE.Object3D) {
    obj.traverse((child) => {
        const css = child as unknown as { isCSS2DObject?: boolean; element?: HTMLElement }
        if (css.isCSS2DObject && css.element?.parentNode) {
            css.element.parentNode.removeChild(css.element)
        }

        const mesh = child as THREE.Mesh
        if (mesh.geometry) mesh.geometry.dispose()
        const material = mesh.material
        if (Array.isArray(material)) {
            material.forEach((item) => item.dispose())
        } else if (material) {
            ;(material as THREE.Material).dispose()
        }
    })
}

function clearGroup(group: THREE.Group | null) {
    if (!group) return
    while (group.children.length) {
        const child = group.children[0]
        if (!child) return
        group.remove(child)
        disposeObject3D(child)
    }
}

function setShadow(mesh: THREE.Object3D, cast = true, receive = true) {
    mesh.castShadow = cast
    mesh.receiveShadow = receive
}

function addGround(mapper: LayoutMapper, materials: SceneMaterials) {
    if (!layoutGroup) return
    const size = Math.max(mapper.worldWidth, mapper.worldDepth, MIN_WORLD_SPAN) + 48
    const ground = new THREE.Mesh(new THREE.PlaneGeometry(size, size), materials.ground)
    ground.rotation.x = -Math.PI / 2
    ground.position.y = -0.012
    ground.receiveShadow = true
    layoutGroup.add(ground)

    const grid = new THREE.GridHelper(size, Math.min(80, Math.max(20, Math.round(size / 4))), 0x78909c, 0xc5d0d8)
    grid.position.y = 0.004
    const gridMaterial = grid.material as THREE.Material
    gridMaterial.transparent = true
    gridMaterial.opacity = 0.28
    layoutGroup.add(grid)
}

function addTrackSection(
    segment: TrackSegment,
    mapper: LayoutMapper,
    materials: SceneMaterials,
    options: { sleepers?: boolean; ballast?: boolean } = {},
) {
    if (!layoutGroup) return
    const start = mapper.mapPoint({ x: segment.x1, y: segment.y1 })
    const end = mapper.mapPoint({ x: segment.x2, y: segment.y2 })
    const dx = end.x - start.x
    const dz = end.z - start.z
    const length = Math.hypot(dx, dz)
    if (length < 0.03) return

    const group = new THREE.Group()
    group.position.set((start.x + end.x) / 2, 0, (start.z + end.z) / 2)
    group.rotation.y = -Math.atan2(dz, dx)

    if (options.ballast !== false) {
        const ballast = new THREE.Mesh(
            new THREE.BoxGeometry(length + 0.1, BALLAST_HEIGHT, BALLAST_WIDTH),
            materials.ballast,
        )
        ballast.position.y = BALLAST_HEIGHT / 2
        setShadow(ballast, true, true)
        group.add(ballast)
    }

    const railGeo = new THREE.BoxGeometry(length + 0.04, RAIL_HEIGHT, RAIL_WIDTH)
    for (const railZ of [-TRACK_GAUGE / 2, TRACK_GAUGE / 2]) {
        const rail = new THREE.Mesh(railGeo, materials.rail)
        rail.position.set(0, RAIL_Y, railZ)
        setShadow(rail, true, true)
        group.add(rail)
    }

    if (options.sleepers !== false) {
        const sleeperCount = Math.max(1, Math.min(MAX_SLEEPERS_PER_SEGMENT, Math.floor(length / SLEEPER_SPACING)))
        const sleeperGeo = new THREE.BoxGeometry(SLEEPER_WIDTH, SLEEPER_HEIGHT, SLEEPER_LENGTH)
        const sleepers = new THREE.InstancedMesh(sleeperGeo, materials.sleeper, sleeperCount)
        const matrix = new THREE.Matrix4()
        for (let i = 0; i < sleeperCount; i++) {
            const x = -length / 2 + ((i + 0.5) / sleeperCount) * length
            matrix.makeTranslation(x, SLEEPER_Y, 0)
            sleepers.setMatrixAt(i, matrix)
        }
        sleepers.instanceMatrix.needsUpdate = true
        setShadow(sleepers, true, true)
        group.add(sleepers)
    }

    layoutGroup.add(group)
}

function addTrackLabels(layout: StationLayoutData, mapper: LayoutMapper) {
    const labelledTrackIds = new Set<string>()
    for (const track of layout.tracks) {
        const label = track.name.trim()
        if (!label || labelledTrackIds.has(track.id)) continue
        labelledTrackIds.add(track.id)
        const mid = mapper.mapPoint({ x: (track.x1 + track.x2) / 2, y: (track.y1 + track.y2) / 2 }, 0.78)
        addLabel(label, mid, 'layout3d-label-track')
    }
}

function addCurveTracks(layout: StationLayoutData, mapper: LayoutMapper, materials: SceneMaterials) {
    for (const curve of layout.curves) {
        const points = buildCurveSamplePoints(curve, 18)
        for (let i = 0; i < points.length - 1; i++) {
            const start = points[i]
            const end = points[i + 1]
            if (!start || !end) continue
            addTrackSection(
                {
                    id: `${curve.id}-section-${i}`,
                    line: {
                        id: curve.id,
                        name: '',
                        x1: start.x,
                        y1: start.y,
                        x2: end.x,
                        y2: end.y,
                        fromNodeID: '',
                        toNodeID: '',
                    },
                    x1: start.x,
                    y1: start.y,
                    x2: end.x,
                    y2: end.y,
                },
                mapper,
                materials,
                { sleepers: false, ballast: true },
            )
        }
    }
}

function addSignal(signal: Signal, mapper: LayoutMapper, materials: SceneMaterials) {
    if (!layoutGroup) return

    const position = mapper.mapPoint(signal.position)
    const group = new THREE.Group()
    group.position.set(position.x, 0, position.z)
    group.rotation.y = getSignalDirectionAngle(signal.direction)

    const base = new THREE.Mesh(new THREE.CylinderGeometry(0.16, 0.22, 0.12, 18), materials.signalPost)
    base.position.y = 0.06
    setShadow(base, true, true)
    group.add(base)

    const post = new THREE.Mesh(new THREE.CylinderGeometry(0.045, 0.055, 1.24, 14), materials.signalPost)
    post.position.y = 0.72
    setShadow(post, true, true)
    group.add(post)

    const arm = new THREE.Mesh(new THREE.BoxGeometry(0.52, 0.045, 0.045), materials.signalPost)
    arm.position.set(0.24, 1.3, 0)
    setShadow(arm, true, true)
    group.add(arm)

    const head = new THREE.Mesh(new THREE.BoxGeometry(0.22, 0.62, 0.11), materials.signalHead)
    head.position.set(0.52, 1.38, 0)
    setShadow(head, true, true)
    group.add(head)

    const lightGeometry = new THREE.SphereGeometry(0.07, 18, 12)
    const lights = [
        { y: 1.55, material: materials.signalWhite },
        { y: 1.38, material: materials.signalGreen },
        { y: 1.21, material: materials.signalRed },
    ]
    lights.forEach((light) => {
        const mesh = new THREE.Mesh(lightGeometry, light.material)
        mesh.position.set(0.52, light.y, -0.065)
        setShadow(mesh, true, false)
        group.add(mesh)
    })

    layoutGroup.add(group)
    addLabel(signal.name || signal.id, new THREE.Vector3(position.x, 1.92, position.z), 'layout3d-label-signal')
}

function getSignalDirectionAngle(direction: string): number {
    const normalized = direction.toLowerCase()
    if (normalized === 'w') return Math.PI
    if (normalized === 's') return Math.PI / 2
    if (normalized === 'd') return -Math.PI / 2
    return 0
}

function addPlatform(platform: Platform, mapper: LayoutMapper, materials: SceneMaterials) {
    if (!layoutGroup) return

    const center = mapper.mapPoint({
        x: platform.x + platform.width / 2,
        y: platform.y + platform.height / 2,
    })
    const width = Math.max(PLATFORM_MIN_SIZE, mapper.mapLength(platform.width))
    const depth = Math.max(PLATFORM_MIN_SIZE, mapper.mapLength(platform.height))

    const group = new THREE.Group()
    group.position.set(center.x, 0, center.z)

    const platformMesh = new THREE.Mesh(new THREE.BoxGeometry(width, 0.34, depth), materials.platform)
    platformMesh.position.y = 0.17
    setShadow(platformMesh, true, true)
    group.add(platformMesh)

    const edgeGeometry = new THREE.EdgesGeometry(platformMesh.geometry)
    const edgeMaterial = new THREE.LineBasicMaterial({ color: 0x3f6f89, transparent: true, opacity: 0.72 })
    const edges = new THREE.LineSegments(edgeGeometry, edgeMaterial)
    platformMesh.add(edges)

    const longSafetyLineGeo = new THREE.BoxGeometry(Math.max(0.4, width - 0.35), 0.025, 0.045)
    for (const z of [-depth / 2 + 0.14, depth / 2 - 0.14]) {
        const line = new THREE.Mesh(longSafetyLineGeo, materials.platformLine)
        line.position.set(0, 0.365, z)
        setShadow(line, false, false)
        group.add(line)
    }

    layoutGroup.add(group)
    addLabel(platform.name || platform.id, new THREE.Vector3(center.x, 0.84, center.z), 'layout3d-label-platform')
}

function addSwitchMarker(sw: SwitchDevice, mapper: LayoutMapper, materials: SceneMaterials) {
    if (!layoutGroup) return
    const position = mapper.mapPoint(sw.position)
    const marker = new THREE.Mesh(new THREE.ConeGeometry(0.24, 0.54, 4), materials.switchMarker)
    marker.position.set(position.x, 0.42, position.z)
    marker.rotation.y = Math.PI / 4
    setShadow(marker, true, true)
    layoutGroup.add(marker)
    addLabel(sw.name || sw.id, new THREE.Vector3(position.x, 0.98, position.z), 'layout3d-label-switch')
}

function addLabel(text: string, position: THREE.Vector3, className: string) {
    if (!layoutGroup || !text.trim()) return
    const element = document.createElement('div')
    element.className = `layout3d-label ${className}`
    element.textContent = text
    const label = new CSS2DObject(element)
    label.position.copy(position)
    layoutGroup.add(label)
}

function rebuildScene() {
    if (!layoutGroup) return
    clearGroup(layoutGroup)
    lastMapper = null

    const mapper = createMapper(layoutData.value)
    if (!mapper) {
        renderOnce()
        return
    }

    lastMapper = mapper
    const materials = createMaterials()
    addGround(mapper, materials)

    const trackSegments = buildVisibleTrackSegments(layoutData.value)
    for (const segment of trackSegments) {
        addTrackSection(segment, mapper, materials)
    }

    addCurveTracks(layoutData.value, mapper, materials)
    for (const platform of layoutData.value.platforms) addPlatform(platform, mapper, materials)
    for (const signal of layoutData.value.signals) addSignal(signal, mapper, materials)
    for (const sw of layoutData.value.switches) addSwitchMarker(sw, mapper, materials)
    addTrackLabels(layoutData.value, mapper)

    fitCameraToLayout()
    renderOnce()
}

function fitCameraToLayout() {
    if (!camera || !controls || !lastMapper) return
    const span = Math.max(lastMapper.worldWidth, lastMapper.worldDepth, MIN_WORLD_SPAN)
    const height = Math.max(28, span * 0.48)
    const distance = Math.max(56, span * 0.82)

    controls.target.set(0, 0.22, 0)
    camera.position.set(distance * 0.74, height, distance * 0.86)
    camera.near = 0.1
    camera.far = Math.max(1000, span * 14)
    camera.updateProjectionMatrix()
    controls.update()
}

function resetCamera() {
    fitCameraToLayout()
}

function renderOnce() {
    if (controls) controls.update()
    if (renderer && scene && camera) renderer.render(scene, camera)
    if (labelRenderer && scene && camera) labelRenderer.render(scene, camera)
}

function rafTick() {
    renderOnce()
    rafId = window.requestAnimationFrame(rafTick)
}

function ensureRafLoop() {
    if (rafId === null) {
        rafId = window.requestAnimationFrame(rafTick)
    }
}

function cancelRafLoop() {
    if (rafId !== null) {
        window.cancelAnimationFrame(rafId)
        rafId = null
    }
}

function onResize() {
    if (!renderer || !camera || !canvasWrapperRef.value) return
    const rawWidth = canvasWrapperRef.value.clientWidth
    const rawHeight = canvasWrapperRef.value.clientHeight
    const width = Math.max(1, rawWidth)
    const height = Math.max(1, rawHeight)

    renderer.setSize(width, height, false)
    if (labelRenderer) labelRenderer.setSize(width, height)
    camera.aspect = width / height
    camera.updateProjectionMatrix()

    const wasCollapsed = lastWrapperWidth < 2 || lastWrapperHeight < 2
    const nowVisible = rawWidth > 1 && rawHeight > 1
    if (wasCollapsed && nowVisible && lastMapper) fitCameraToLayout()
    lastWrapperWidth = rawWidth
    lastWrapperHeight = rawHeight
    renderOnce()
}

async function loadLayout() {
    const instanceID = selectedInstanceId.value
    loadErrorMessage.value = ''

    if (!instanceID) {
        layoutLoadVersion++
        layoutData.value = createEmptyLayout()
        rebuildScene()
        return
    }

    const loadVersion = ++layoutLoadVersion
    loadingData.value = true
    try {
        const response = await axios.post('/StationLayout/GetJson', null, {
            params: { instanceID },
        })
        if (loadVersion !== layoutLoadVersion) return
        layoutData.value = normalizeLayout(response.data)
        await nextTick()
        rebuildScene()
    } catch (error) {
        if (loadVersion !== layoutLoadVersion) return
        console.error('Failed to load station layout 3D data:', error)
        loadErrorMessage.value = t('stationLayout3d.messages.loadFailed')
        layoutData.value = createEmptyLayout()
        rebuildScene()
        ElMessage.error(loadErrorMessage.value)
    } finally {
        if (loadVersion === layoutLoadVersion) loadingData.value = false
    }
}

watch(() => props.selectedInstanceId, () => {
    void loadLayout()
}, { immediate: true })

watch(() => props.activationKey, () => {
    if (selectedInstanceId.value) void loadLayout()
})

onMounted(() => {
    nextTick(() => {
        initThree()
        if (typeof ResizeObserver !== 'undefined' && canvasWrapperRef.value) {
            resizeObserver = new ResizeObserver(() => onResize())
            resizeObserver.observe(canvasWrapperRef.value)
        } else {
            window.addEventListener('resize', onResize)
        }
        onResize()
    })
})

onBeforeUnmount(() => {
    cancelRafLoop()
    if (resizeObserver) {
        resizeObserver.disconnect()
        resizeObserver = null
    }
    window.removeEventListener('resize', onResize)
    clearGroup(layoutGroup)
    if (layoutGroup && scene) scene.remove(layoutGroup)
    layoutGroup = null
    if (controls) {
        controls.dispose()
        controls = null
    }
    if (renderer) {
        renderer.dispose()
        renderer = null
    }
    if (labelRendererRoot?.parentElement) {
        labelRendererRoot.parentElement.removeChild(labelRendererRoot)
    }
    labelRenderer = null
    labelRendererRoot = null
    scene = null
    camera = null
    lastMapper = null
})
</script>

<style scoped lang="css">
.station-layout-3d-page {
    display: flex;
    flex-direction: column;
    width: 100%;
    height: 100%;
    min-height: 0;
    background: #ffffff;
    overflow: hidden;
}

.layout3d-toolbar {
    flex: 0 0 auto;
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 12px;
    padding: 8px 10px;
    border-bottom: 1px solid #d8e2ef;
    background: #f7fafc;
}

.layout3d-metrics,
.layout3d-actions {
    display: inline-flex;
    align-items: center;
    gap: 8px;
    min-width: 0;
}

.layout3d-actions {
    justify-content: flex-end;
}

.metric-item {
    display: inline-flex;
    align-items: center;
    gap: 5px;
    min-width: 74px;
    padding: 4px 8px;
    border: 1px solid #d6e1ef;
    border-radius: 3px;
    background: #ffffff;
    color: #263342;
    font-size: 12px;
    line-height: 1;
}

.metric-label {
    color: #5b6777;
    white-space: nowrap;
}

.metric-item strong {
    font-family: "Consolas", "Courier New", monospace;
    font-size: 14px;
    color: #1452a3;
}

.layout3d-body {
    position: relative;
    flex: 1 1 auto;
    min-height: 0;
    background: #e7edf5;
    overflow: hidden;
    contain: layout paint;
}

.layout3d-canvas {
    display: block;
    width: 100%;
    height: 100%;
    outline: none;
}

.layout3d-empty {
    position: absolute;
    left: 50%;
    top: 50%;
    transform: translate(-50%, -50%);
    max-width: min(340px, calc(100% - 32px));
    padding: 10px 16px;
    border: 1px solid #c9d8e8;
    border-radius: 4px;
    background: rgba(255, 255, 255, 0.9);
    color: #334155;
    font-size: 13px;
    text-align: center;
    pointer-events: none;
}

.layout3d-body.hide-layout-labels :deep(.layout3d-label) {
    display: none;
}

:deep(.layout3d-label) {
    padding: 2px 6px;
    border-radius: 3px;
    border: 1px solid rgba(89, 103, 118, 0.28);
    background: rgba(255, 255, 255, 0.86);
    color: #172033;
    font-size: 11px;
    line-height: 1.2;
    white-space: nowrap;
    box-shadow: 0 2px 5px rgba(15, 23, 42, 0.16);
    pointer-events: none;
}

:deep(.layout3d-label-track) {
    color: #0f172a;
}

:deep(.layout3d-label-signal) {
    border-color: rgba(225, 29, 72, 0.34);
    color: #991b1b;
}

:deep(.layout3d-label-platform) {
    border-color: rgba(14, 116, 144, 0.32);
    color: #155e75;
}

:deep(.layout3d-label-switch) {
    border-color: rgba(245, 158, 11, 0.34);
    color: #92400e;
}

@media (max-width: 768px) {
    .layout3d-toolbar {
        align-items: stretch;
        flex-direction: column;
    }

    .layout3d-metrics {
        display: grid;
        grid-template-columns: repeat(3, minmax(0, 1fr));
        width: 100%;
    }

    .metric-item {
        min-width: 0;
        justify-content: center;
    }

    .layout3d-actions {
        justify-content: flex-start;
    }
}
</style>
