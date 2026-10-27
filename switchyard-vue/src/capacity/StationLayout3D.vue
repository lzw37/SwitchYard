<template>
    <section class="station-layout-3d-page" v-loading="preparingTrainModels">
        <div class="layout3d-toolbar">
            <div class="layout3d-scheme-control">
                <el-tooltip :content="t('stationLayout3d.trainModels.help')">
                    <span class="layout3d-control-label">{{ t('stationLayout3d.labels.trainModel') }}</span>
                </el-tooltip>
                <el-select
                    v-model="selectedTrainModel"
                    size="small"
                    filterable
                    class="layout3d-model-select"
                    :aria-label="t('stationLayout3d.labels.trainModel')"
                >
                    <el-option value="auto" :label="t('stationLayout3d.trainModels.auto')" />
                    <el-option
                        v-for="option in ROLLING_STOCK_OPTIONS"
                        :key="option.value"
                        :label="option.label"
                        :value="option.value"
                    />
                </el-select>
            </div>
            <div class="layout3d-ratio-control">
                <span class="layout3d-control-label">{{ t('stationLayout3d.labels.displayRatio') }}</span>
                <el-slider
                    v-model="displayRatio"
                    class="layout3d-ratio-slider"
                    size="small"
                    :min="0.25"
                    :max="20"
                    :step="0.05"
                    :disabled="!canRender"
                    :format-tooltip="formatDisplayRatio"
                    :aria-label="t('stationLayout3d.labels.displayRatio')"
                    @change="handleDisplayRatioChange"
                />
                <output class="layout3d-ratio-value">{{ formatDisplayRatio(displayRatio) }}</output>
                <ActionButton
                    :label="t('stationLayout3d.buttons.resetDisplayRatio')"
                    :disabled="!canRender"
                    @click="resetDisplayRatio"
                    :icon="RefreshLeft"
                />
            </div>
            <ActionButton
                :label="t('stationLayout3d.buttons.resetView')"
                :icon="Aim"
                :disabled="!canRender"
                @click="resetCamera"
            />
            <div class="station-toolbar-switch-control">
                <span class="station-toolbar-switch-control__label">{{ t('stationLayout3d.labels.showLabels') }}</span>
                <el-switch v-model="showLabels" size="small" :aria-label="t('stationLayout3d.labels.showLabels')" />
            </div>
            <div class="station-toolbar-switch-control">
                <span class="station-toolbar-switch-control__label">{{ t('stationLayout3d.labels.showOccupancy') }}</span>
                <el-switch
                    v-model="showTrackOccupancy"
                    size="small"
                    :disabled="!canRender"
                    :aria-label="t('stationLayout3d.labels.showOccupancy')"
                />
            </div>
        </div>
        <div ref="canvasWrapperRef" class="layout3d-body" :class="{ 'hide-layout-labels': !showLabels }">
            <canvas ref="canvasRef" class="layout3d-canvas" data-testid="station-layout-3d-canvas" />
            <div v-if="renderError || !canRender" class="layout3d-empty">
                {{ renderError ? t('operationSimulation.views.renderError') : t('stationLayout3d.messages.empty') }}
            </div>
        </div>
    </section>
</template>

<script setup lang="ts">
import ActionButton from '@/components/ui/ActionButton.vue'
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { ElMessage } from 'element-plus'
import { Aim, RefreshLeft } from '@element-plus/icons-vue'
import * as THREE from 'three'
import {
    getRollingStockBogieOffsets, getRollingStockDimensions, ROLLING_STOCK_OPTIONS,
    type RollingStockCarRole, type RollingStockModelId,
} from './three/rollingStock'
import { RollingStockTemplates, type RollingStockTemplateSpec } from './three/rollingStockTemplates'
import { getRollingStockConsistForRun, type RollingStockModelSelection } from './three/emuModelSelection'
import { getEmuConsistSizing, getLongestRouteLinkLength, getShortestTrainConsistSizing } from './three/trainSizing'
import { createRailway, getRailwayDimensions, prepareRailwayPaths } from './three/railway'
import { createStationGround, createStationPlatform, fitStationLighting, lightStationScene } from './three/stationEnvironment'
import { sampleCurveCoordinates, transformLayoutCoordinates } from './three/layoutCoordinates'
import { insertRouteCurves, followRenderedPaths } from './three/trainPath'
import { buildTrainMotionProfiles, sampleTrainMotion, selectTrainMotionRuns } from './three/trainMotion'
import { createTrackOccupancyOverlay } from './three/trackOccupancyOverlay'
import { mapTrackOccupancyPaths } from './three/trackOccupancyPaths'
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js'
import { CSS2DObject, CSS2DRenderer } from 'three/examples/jsm/renderers/CSS2DRenderer.js'
import { getSignalStyleAsset } from '@switchyard/station-layout'
import type { TrackOccupancyTimeline } from './trackOccupancy'
import type { StationRouteOption, Point as RoutePoint, PolylinePath, RouteGeometry, RouteRun, PathSegment } from './simulationTypes'

interface Position2D {
    x: number
    y: number
}

interface Vector2D {
    x: number
    y: number
}

interface TrackVectorCandidate {
    vector: Vector2D
    length: number
    lineID: string
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
    displayPoints?: Position2D[]
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

interface SignalStyleElement {
    tag: string
    attrs?: Record<string, unknown>
}

interface SignalStyleAsset {
    elements?: SignalStyleElement[]
}

interface SignalLightSource {
    x: number
    y: number
    radius: number
    color: number
}

interface SignalLightSpec {
    x: number
    y: number
    color: number
}

interface SignalLightLayout {
    width: number
    height: number
    radius: number
    lights: SignalLightSpec[]
}

interface Platform {
    id: string
    name: string
    x: number
    y: number
    width: number
    height: number
}

interface SwitchBranchVector {
    x: number
    y: number
    lineID: string
}

interface SwitchDevice {
    id: string
    name: string
    type: string
    position: Position2D
    bindingNodeID: string
    branchVectorList: SwitchBranchVector[]
}

interface StationLayoutData {
    tracks: Track[]
    curves: CurveTrack[]
    nodes: NodePoint[]
    signals: Signal[]
    platforms: Platform[]
    switches: SwitchDevice[]
}

interface SimulationTrainCar {
    key: string
    runKey: string
    modelId: RollingStockModelId
    role: RollingStockCarRole
    carIndex: number
    frontBogie: { x: number; y: number; angle: number }
    rearBogie: { x: number; y: number; angle: number }
    x: number
    y: number
    angle: number
    length: number
    width: number
    height: number
    fill: string
    stroke: string
    label?: string
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
    signalPost: THREE.MeshStandardMaterial
    signalHead: THREE.MeshStandardMaterial
}

interface TrainCarObjectEntry {
    group: THREE.Group
    model: THREE.Group
    frontBogie: THREE.Object3D | undefined
    rearBogie: THREE.Object3D | undefined
    label: CSS2DObject
    labelElement: HTMLElement
}

const props = defineProps<{
    layout: Record<string, unknown> | null
    gridSpacing: number
    runs: RouteRun[]
    playheadSeconds: number
    getPlaybackSeconds: () => number
    isPlaying: boolean
    isAllTrainPlayback: boolean
    trackOccupancyTimeline: TrackOccupancyTimeline
}>()
const emit = defineEmits<{ renderError: [] }>()
const selectedTrainModel = defineModel<RollingStockModelSelection>('trainModel', { default: 'auto' })
const displayRatio = defineModel<number>('displayRatio', { default: 1 })
const showLabels = defineModel<boolean>('showLabels', { default: true })
const showTrackOccupancy = defineModel<boolean>('showTrackOccupancy', { default: false })
const { t } = useI18n()

const TARGET_WORLD_SPAN = 170
const MIN_WORLD_SPAN = 48
const STANDARD_TRACK_GAUGE_MM = 1435
const TRACK_CENTERLINE_SPACING_MM = 5000
const TRACK_CENTERLINE_GRID_COUNT = 2
const PLATFORM_MIN_SIZE = 1.2
const SIGNAL_SIDE_OFFSET = 2.25
const SIGNAL_LABEL_Y = 1.92
const SIGNAL_HEAD_DEPTH = 0.11
const SIGNAL_LIGHT_RADIUS = 0.07
const SIGNAL_LIGHT_ROW_SPACING = 0.17
const SIGNAL_LIGHT_COLUMN_SPACING = 0.18
const SIGNAL_LIGHT_PADDING = 0.16
const canvasWrapperRef = ref<HTMLElement | null>(null)
const canvasRef = ref<HTMLCanvasElement | null>(null)
const layoutData = computed(() => normalizeLayout(props.layout))
const layoutGridSpacing = computed(() => props.gridSpacing)
const playheadSeconds = computed(() => props.playheadSeconds)
const isPlaying = computed(() => props.isPlaying)
const isAllTrainPlayback = computed(() => props.isAllTrainPlayback)
const trackOccupancyTimeline = computed(() => props.trackOccupancyTimeline)
const appliedDisplayRatio = ref(displayRatio.value)
const displayLayoutData = computed(() => transformLayoutCoordinates(layoutData.value, appliedDisplayRatio.value))
const renderError = ref(false)
let renderer: THREE.WebGLRenderer | null = null
let isDisposed = false
let labelRenderer: CSS2DRenderer | null = null
let labelRendererRoot: HTMLElement | null = null
let scene: THREE.Scene | null = null
let disposeSceneLighting: (() => void) | null = null
let camera: THREE.PerspectiveCamera | null = null
let controls: OrbitControls | null = null
let layoutGroup: THREE.Group | null = null
let trainGroup: THREE.Group | null = null
let trackOccupancyOverlay: ReturnType<typeof createTrackOccupancyOverlay> | null = null
let lastTrackOccupancyState: ReadonlyMap<string, readonly string[]> | null = null
let resizeObserver: ResizeObserver | null = null
let rafId: number | null = null
let lastMapper: LayoutMapper | null = null
let lastWrapperWidth = 0
let lastWrapperHeight = 0
const trainCarAngleMemory = new Map<string, number>()
const trainCarObjectMap = new Map<string, TrainCarObjectEntry>()
const trainModelTemplates = new RollingStockTemplates()
const preparingTrainModels = ref(false)
let trainModelPreparationVersion = 0

function buildRailwaySourcePaths(layout: StationLayoutData) {
    const paths = buildVisibleTrackSegments(layout).map(segment => ({
        id: segment.id,
        points: [new THREE.Vector3(segment.x1, 0, segment.y1), new THREE.Vector3(segment.x2, 0, segment.y2)],
    }))
    for (const curve of layout.curves) paths.push({
        id: `curve-${curve.id}`,
        points: buildCurveSamplePoints(curve, 48).map(point => new THREE.Vector3(point.x, 0, point.y)),
    })
    return paths
}
const railwaySourcePaths = computed(() => buildRailwaySourcePaths(layoutData.value))
const displayRailwayPaths = computed(() => buildRailwaySourcePaths(displayLayoutData.value))
function buildRenderedTurnoutPaths(layout: StationLayoutData, paths: ReturnType<typeof buildRailwaySourcePaths>) {
    return prepareRailwayPaths(paths,
        layout.switches.map(sw => ({ id: sw.id, position: new THREE.Vector3(sw.position.x, 0, sw.position.y) })),
        getLayoutTrackGaugeUnits(),
    ).filter(path => !path.id.startsWith('curve-') && path.points.length > 2).map(path => ({
        originalStart: { x: path.points[0]!.x, y: path.points[0]!.z },
        originalEnd: { x: path.points[path.points.length - 1]!.x, y: path.points[path.points.length - 1]!.z },
        points: path.points.map(point => ({ x: point.x, y: point.z })),
    }))
}
const renderedTurnoutPaths = computed(() => buildRenderedTurnoutPaths(layoutData.value, railwaySourcePaths.value))
const displayTurnoutPaths = computed(() => buildRenderedTurnoutPaths(displayLayoutData.value, displayRailwayPaths.value))

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
// The parent owns schedule times; only the displayed path is transformed here.
const routeRuns = computed<RouteRun[]>(() => props.runs.map(run => ({
    ...run,
    path: buildRouteGeometry(run.route, displayLayoutData.value, displayTurnoutPaths.value).path,
})))
// Select the shortest of each train's route-based formations once, so route
// changes never resize its physical cars, couplers or wheelbases.
const trainConsistByRun = computed(() => {
    const gauge = getLayoutTrackGaugeUnits()
    const links = displayLayoutData.value.tracks
    const candidates = routeRuns.value.map(run => {
        const consist = getRollingStockConsistForRun(run.train.trainType, selectedTrainModel.value)
        return {
            trainID: run.train.id,
            runKey: run.key,
            ...consist,
            sizing: getEmuConsistSizing(gauge, getLongestRouteLinkLength(links, run), consist.carCount, getRollingStockDimensions(consist.modelId)),
        }
    })
    const shortest = getShortestTrainConsistSizing(candidates)
    return new Map(candidates.map(candidate => [candidate.runKey, {
        modelId: candidate.modelId,
        carCount: candidate.carCount,
        sizing: shortest.get(candidate.runKey) ?? null,
    }]))
})
const trainMotionByRun = computed(() => buildTrainMotionProfiles(routeRuns.value))

const simulationTrainCars = computed<SimulationTrainCar[]>(() => buildSimulationTrainCars())
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

function normalizeSwitchBranchVector(item: any): SwitchBranchVector | null {
    const x = toFiniteNumberOrNull(item?.x ?? item?.X)
    const y = toFiniteNumberOrNull(item?.y ?? item?.Y)
    if (x === null || y === null) return null

    return {
        x,
        y,
        lineID: readString(item, 'lineID', 'LineID', 'bindingLinkID', 'BindingLinkID'),
    }
}

function normalizeSwitch(item: any, index: number): SwitchDevice | null {
    const position = normalizePosition(item?.position ?? item?.Position) || normalizePosition(item)
    if (!position) return null

    const id = readString(item, 'id', 'ID') || `switch-${index + 1}`
    const name = normalizeNamedValue(id, readString(item, 'name', 'Name'))
    const branchVectorRaw = Array.isArray(item?.branchVectorList)
        ? item.branchVectorList
        : Array.isArray(item?.BranchVectorList)
            ? item.BranchVectorList
            : []

    return {
        id,
        name,
        type: readString(item, 'type', 'Type'),
        position,
        bindingNodeID: readString(item, 'bindingNodeID', 'BindingNodeID'),
        branchVectorList: branchVectorRaw
            .map((vector: any) => normalizeSwitchBranchVector(vector))
            .filter((vector: SwitchBranchVector | null): vector is SwitchBranchVector => vector !== null),
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

function parseRouteReferenceList(value: string) {
    const text = String(value || '').trim()
    if (!text) return []
    try {
        const parsed = JSON.parse(text)
        if (Array.isArray(parsed)) return normalizeUniqueStrings(parsed)
    } catch {
        // Route lists may be stored as plain text.
    }
    return normalizeUniqueStrings(text.split(/(?:\s*->\s*)|(?:\s*[,，、\n\r]\s*)|\s+/))
}

function normalizeUniqueStrings(values: unknown[]) {
    const result: string[] = []
    const seen = new Set<string>()
    values.forEach((value) => {
        const text = String(value ?? '').trim()
        if (!text || seen.has(text)) return
        seen.add(text)
        result.push(text)
    })
    return result
}

function buildRouteGeometry(
    route: StationRouteOption,
    layout = layoutData.value,
    turnoutPaths = renderedTurnoutPaths.value,
): RouteGeometry {
    const nodes = new Map(layout.nodes.map(node => [node.id, node]))
    const tracks = new Map(layout.tracks.map(track => [track.id, track]))
    const linkIds = parseRouteReferenceList(route.linkList)
    let points = parseRouteReferenceList(route.nodeList)
        .map((nodeId) => pointFromNodeId(nodeId, nodes))
        .filter((point): point is RoutePoint => point !== null)

    if (points.length < 2) {
        points = buildPointsFromLinks(route, linkIds, nodes, tracks)
    }
    if (points.length < 2) {
        points = [pointFromNodeId(route.startNodeID, nodes), pointFromNodeId(route.endNodeID, nodes)]
            .filter((point): point is RoutePoint => point !== null)
    }

    return {
        path: buildPolylinePath(followRenderedPaths(insertRouteCurves(points, layout.curves.map(curve => ({
            nodeID: curve.nodeID,
            tangentLinkID1: curve.tangentLinkID1,
            tangentLinkID2: curve.tangentLinkID2,
            points: buildCurveSamplePoints(curve, 48),
        })), layout.tracks), turnoutPaths)),
        // Preserve the traversed order for routes that only specify nodes.
        nodeIds: points.map(point => point.nodeId).filter((id): id is string => Boolean(id)),
        linkIds,
    }
}

function pointFromNodeId(nodeId: string, nodes: Map<string, NodePoint>): RoutePoint | null {
    const id = String(nodeId || '').trim()
    if (!id) return null
    const node = nodes.get(id)
    if (!node) return null
    return { x: node.x, y: node.y, nodeId: id }
}

function buildPointsFromLinks(
    route: StationRouteOption, linkIds: string[], nodes: Map<string, NodePoint>, tracks: Map<string, Track>,
): RoutePoint[] {
    const points: RoutePoint[] = []
    let currentNodeId = route.startNodeID.trim()
    const startPoint = pointFromNodeId(currentNodeId, nodes)
    if (startPoint) points.push(startPoint)

    linkIds.forEach((linkId) => {
        const track = tracks.get(linkId)
        if (!track) return
        const endpoints = getTrackEndpoints(track, nodes)
        if (!endpoints) return
        const [fromPoint, toPoint] = endpoints
        if (points.length === 0) {
            if (currentNodeId && currentNodeId === track.toNodeID) {
                points.push(toPoint, fromPoint)
                currentNodeId = track.fromNodeID
            } else {
                points.push(fromPoint, toPoint)
                currentNodeId = track.toNodeID
            }
            return
        }

        if (currentNodeId && currentNodeId === track.fromNodeID) {
            appendDistinctPoint(points, toPoint)
            currentNodeId = track.toNodeID
        } else if (currentNodeId && currentNodeId === track.toNodeID) {
            appendDistinctPoint(points, fromPoint)
            currentNodeId = track.fromNodeID
        } else {
            const last = points[points.length - 1]
            if (!last) return
            const fromDistance = getPointDistance(last, fromPoint)
            const toDistance = getPointDistance(last, toPoint)
            if (fromDistance <= toDistance) {
                appendDistinctPoint(points, fromPoint)
                appendDistinctPoint(points, toPoint)
                currentNodeId = track.toNodeID
            } else {
                appendDistinctPoint(points, toPoint)
                appendDistinctPoint(points, fromPoint)
                currentNodeId = track.fromNodeID
            }
        }
    })

    return points
}

function getTrackEndpoints(track: Track, nodes: Map<string, NodePoint>): [RoutePoint, RoutePoint] | null {
    const fromNode = pointFromNodeId(track.fromNodeID, nodes)
    const toNode = pointFromNodeId(track.toNodeID, nodes)
    const fromPoint = fromNode || { x: track.x1, y: track.y1, nodeId: track.fromNodeID || undefined }
    const toPoint = toNode || { x: track.x2, y: track.y2, nodeId: track.toNodeID || undefined }
    if (!Number.isFinite(fromPoint.x) || !Number.isFinite(fromPoint.y) || !Number.isFinite(toPoint.x) || !Number.isFinite(toPoint.y)) {
        return null
    }
    return [fromPoint, toPoint]
}

function appendDistinctPoint(points: RoutePoint[], point: RoutePoint) {
    const previous = points[points.length - 1]
    if (previous && getPointDistance(previous, point) < 0.001) return
    points.push(point)
}

function buildPolylinePath(points: RoutePoint[]): PolylinePath {
    const normalizedPoints: RoutePoint[] = []
    points.forEach((point) => appendDistinctPoint(normalizedPoints, point))

    const segments: PathSegment[] = []
    let cursor = 0
    for (let index = 0; index < normalizedPoints.length - 1; index++) {
        const from = normalizedPoints[index]
        const to = normalizedPoints[index + 1]
        if (!from || !to) continue
        const length = getPointDistance(from, to)
        if (length <= 0.001) continue
        segments.push({
            from,
            to,
            length,
            startDistance: cursor,
            angle: normalizePathAngle(Math.atan2(to.y - from.y, to.x - from.x) * 180 / Math.PI),
        })
        cursor += length
    }

    return {
        points: normalizedPoints,
        segments,
        totalLength: cursor,
    }
}

function getPointDistance(left: RoutePoint, right: RoutePoint) {
    return Math.hypot(right.x - left.x, right.y - left.y)
}

function normalizePathAngle(angle: number) {
    const normalized = Number(angle) % 360
    return normalized < 0 ? normalized + 360 : normalized
}

function getNearestEquivalentPathAngle(angle: number, referenceAngle: number) {
    const normalized = normalizePathAngle(angle)
    if (!Number.isFinite(referenceAngle)) return normalized
    return normalized + Math.round((referenceAngle - normalized) / 360) * 360
}

function getContinuousTrainCarAngle(key: string, angle: number) {
    const previousAngle = trainCarAngleMemory.get(key)
    const continuousAngle = previousAngle === undefined
        ? normalizePathAngle(angle)
        : getNearestEquivalentPathAngle(angle, previousAngle)
    trainCarAngleMemory.set(key, continuousAngle)
    return continuousAngle
}

function pruneTrainCarAngleMemory(cars: SimulationTrainCar[]) {
    const visibleKeys = new Set(cars.map((car) => car.key))
    Array.from(trainCarAngleMemory.keys()).forEach((key) => {
        if (!visibleKeys.has(key)) trainCarAngleMemory.delete(key)
    })
}

function clearTrainCarAngleMemory() {
    trainCarAngleMemory.clear()
}

function buildSimulationTrainCars(currentSeconds = playheadSeconds.value): SimulationTrainCar[] {
    const visibleRuns = selectTrainMotionRuns(routeRuns.value, currentSeconds)
    // Single-train mode also shows its initial/final pose when seeking outside
    // its active window. During playback choose runs from the exact frame time.
    if (!isAllTrainPlayback.value && visibleRuns.length === 0) {
        const run = routeRuns.value[findActiveRunIndex(currentSeconds)]
        if (run) visibleRuns.push(run)
    }
    const cars = visibleRuns.flatMap((run) => buildSimulationTrainCarsForRun(run, currentSeconds))
    pruneTrainCarAngleMemory(cars)
    return cars
}

function buildSimulationTrainCarsForRun(run: RouteRun, currentSeconds: number): SimulationTrainCar[] {
    const consist = trainConsistByRun.value.get(run.key)
    if (!consist?.sizing) return []
    const { modelId, carCount, sizing } = consist
    const motion = trainMotionByRun.value.get(run.key) ?? buildTrainMotionProfiles([run]).get(run.key)
    if (!motion) return []
    const fill = run.color
    const cars: SimulationTrainCar[] = []

    for (let index = 0; index < carCount; index++) {
        const offset = ((carCount - 1) / 2 - index) * sizing.carPitch
        // The same physical car survives route changes, including reversals.
        const key = JSON.stringify([run.train.id, modelId, index])
        const role: RollingStockCarRole = index === 0 ? 'head' : index === carCount - 1 ? 'tail' : 'middle'
        const bogieOffsets = getRollingStockBogieOffsets(role, modelId).map(value => value * sizing.longitudinalUnitsPerMeter) as [number, number]
        const position = sampleTrainMotion(motion, currentSeconds, offset, bogieOffsets)
        cars.push({
            key,
            runKey: run.key,
            modelId,
            role,
            carIndex: index,
            frontBogie: position.frontBogie,
            rearBogie: position.rearBogie,
            x: position.x,
            y: position.y,
            angle: getContinuousTrainCarAngle(key, position.angle),
            length: sizing.carLength,
            width: sizing.carWidth,
            height: sizing.carHeight,
            fill: index === 0 ? fill : lightenTrainColor(fill, index),
            stroke: '#f8fafc',
            label: index === 0 ? run.train.trainNumber || run.train.id : '',
        })
    }

    return cars
}

function lightenTrainColor(color: string, index: number) {
    if (index % 2 === 0) return color
    const hex = color.replace('#', '')
    if (hex.length !== 6) return color
    const r = Math.min(255, parseInt(hex.slice(0, 2), 16) + 28)
    const g = Math.min(255, parseInt(hex.slice(2, 4), 16) + 28)
    const b = Math.min(255, parseInt(hex.slice(4, 6), 16) + 28)
    return `#${toHex(r)}${toHex(g)}${toHex(b)}`
}

function toHex(value: number) {
    return Math.max(0, Math.min(255, value)).toString(16).padStart(2, '0')
}

function findActiveRunIndex(currentSeconds: number) {
    const runs = routeRuns.value
    if (runs.length === 0) return -1
    if (!isAllTrainPlayback.value) {
        const visibleRun = selectTrainMotionRuns(runs, currentSeconds)[0]
        if (visibleRun) return runs.indexOf(visibleRun)
    }
    const inProgress = runs.find((run) => currentSeconds >= run.startSeconds && currentSeconds < run.endSeconds)
    if (inProgress) return runs.indexOf(inProgress)
    for (let index = runs.length - 1; index >= 0; index--) {
        const run = runs[index]
        if (run && currentSeconds >= run.endSeconds) return index
    }
    return 0
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
    // Preserve the source's world-unit conversion so coordinate changes do not
    // resize rail profiles, wheels, signals or model heights.
    const sourceBounds = collectBounds(layoutData.value) || bounds
    const sourceSpan = Math.max(sourceBounds.maxX - sourceBounds.minX, sourceBounds.maxY - sourceBounds.minY, 1)
    const scale = sourceSpan / TARGET_WORLD_SPAN
    const centerX = (bounds.minX + bounds.maxX) / 2
    const centerY = (bounds.minY + bounds.maxY) / 2

    return {
        centerX,
        centerY,
        scale,
        worldWidth: Math.max(MIN_WORLD_SPAN, width / scale),
        worldDepth: Math.max(MIN_WORLD_SPAN, depth / scale),
        // Keep the 3D plan view aligned with the 2D editor: +x is right, +y is down.
        mapPoint: (point: Position2D, y = 0) =>
            new THREE.Vector3((point.x - centerX) / scale, y, (point.y - centerY) / scale),
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

function getVectorLength2D(vector: Vector2D): number {
    return Math.hypot(vector.x, vector.y)
}

function normalizeVector2D(vector: Vector2D): Vector2D | null {
    const length = getVectorLength2D(vector)
    if (!Number.isFinite(length) || length <= 0.000001) return null
    return { x: vector.x / length, y: vector.y / length }
}

function dotVector2D(a: Vector2D, b: Vector2D): number {
    return a.x * b.x + a.y * b.y
}

function canonicalizeTrackTangent(vector: Vector2D): Vector2D {
    const unit = normalizeVector2D(vector) || { x: 1, y: 0 }
    if (unit.x < -0.000001 || (Math.abs(unit.x) <= 0.000001 && unit.y < 0)) {
        return { x: -unit.x, y: -unit.y }
    }
    return unit
}

function getTrackLeftNormal(tangent: Vector2D): Vector2D {
    return { x: tangent.y, y: -tangent.x }
}

function mapDirectionVectorToWorld(vector: Vector2D): THREE.Vector3 {
    const world = new THREE.Vector3(vector.x, 0, vector.y)
    if (world.lengthSq() <= 0.000001) return new THREE.Vector3(1, 0, 0)
    return world.normalize()
}

function getTrackVectorCandidateFromLine(line: Track, nodeID: string): TrackVectorCandidate | null {
    if (String(line.fromNodeID) === nodeID) {
        const vector = { x: line.x2 - line.x1, y: line.y2 - line.y1 }
        const length = getVectorLength2D(vector)
        return length > 0.000001 ? { vector, length, lineID: line.id } : null
    }

    if (String(line.toNodeID) === nodeID) {
        const vector = { x: line.x1 - line.x2, y: line.y1 - line.y2 }
        const length = getVectorLength2D(vector)
        return length > 0.000001 ? { vector, length, lineID: line.id } : null
    }

    return null
}

function getAdjacentTrackVectorCandidates(layout: StationLayoutData, bindingNodeID: string): TrackVectorCandidate[] {
    const nodeID = String(bindingNodeID || '').trim()
    if (!nodeID) return []

    return layout.tracks
        .map((line) => getTrackVectorCandidateFromLine(line, nodeID))
        .filter((candidate: TrackVectorCandidate | null): candidate is TrackVectorCandidate => candidate !== null)
}

function getNearestTrackVectorCandidate(layout: StationLayoutData, point: Position2D): TrackVectorCandidate | null {
    let best: TrackVectorCandidate | null = null
    let bestDistanceSquared = Infinity

    for (const line of layout.tracks) {
        const rate = getPointRateOnLine(line, point)
        if (rate === null) continue

        const projection = getLinePointAtRate(line, rate)
        const distanceSquared = (projection.x - point.x) ** 2 + (projection.y - point.y) ** 2
        const vector = { x: line.x2 - line.x1, y: line.y2 - line.y1 }
        const length = getVectorLength2D(vector)
        if (length <= 0.000001 || distanceSquared >= bestDistanceSquared) continue

        bestDistanceSquared = distanceSquared
        best = { vector, length, lineID: line.id }
    }

    return best
}

function selectCanonicalTrackTangent(candidates: TrackVectorCandidate[]): Vector2D {
    const normalized = candidates
        .map((candidate) => ({
            ...candidate,
            unit: normalizeVector2D(candidate.vector),
        }))
        .filter((candidate): candidate is TrackVectorCandidate & { unit: Vector2D } => candidate.unit !== null)

    if (normalized.length === 0) return { x: 1, y: 0 }
    const firstCandidate = normalized[0]
    if (!firstCandidate) return { x: 1, y: 0 }
    if (normalized.length === 1) return canonicalizeTrackTangent(firstCandidate.unit)

    let best = firstCandidate
    let bestScore = -Infinity
    for (let i = 0; i < normalized.length; i++) {
        for (let j = i + 1; j < normalized.length; j++) {
            const first = normalized[i]
            const second = normalized[j]
            if (!first || !second) continue

            const alignmentScore = Math.abs(dotVector2D(first.unit, second.unit)) * 10000
            const lengthScore = Math.max(first.length, second.length)
            const score = alignmentScore + lengthScore
            if (score > bestScore) {
                bestScore = score
                best = first.length >= second.length ? first : second
            }
        }
    }

    return canonicalizeTrackTangent(best.unit)
}

function getSignalTrackTangent(layout: StationLayoutData, signal: Signal): Vector2D {
    const adjacentCandidates = getAdjacentTrackVectorCandidates(layout, signal.bindingNodeID)
    if (adjacentCandidates.length > 0) return selectCanonicalTrackTangent(adjacentCandidates)

    const nearestCandidate = getNearestTrackVectorCandidate(layout, signal.position)
    return selectCanonicalTrackTangent(nearestCandidate ? [nearestCandidate] : [])
}

function getSignalDirectionProfile(direction: string) {
    const normalized = direction.trim().toLowerCase()
    return {
        sideSign: normalized === 's' || normalized === 'd' ? -1 : 1,
        faceSign: normalized === 'w' || normalized === 's' ? -1 : 1,
    }
}

function setObjectBasis(object: THREE.Object3D, xAxis: THREE.Vector3, zAxis: THREE.Vector3) {
    const yAxis = new THREE.Vector3(0, 1, 0)
    const matrix = new THREE.Matrix4().makeBasis(xAxis, yAxis, zAxis)
    object.quaternion.setFromRotationMatrix(matrix)
}

function buildCurveSamplePoints(curve: CurveTrack, preferredSegments = 24): Position2D[] {
    return sampleCurveCoordinates(curve, preferredSegments)
}

function createMaterials(): SceneMaterials {
    return {
        signalPost: new THREE.MeshStandardMaterial({ color: 0x738184, roughness: 0.52, metalness: 0.65 }),
        signalHead: new THREE.MeshStandardMaterial({ color: 0x19252a, roughness: 0.55, metalness: 0.28 }),
    }
}

function initThree() {
    if (isDisposed || !canvasRef.value || !canvasWrapperRef.value || renderer) return

    const width = Math.max(1, canvasWrapperRef.value.clientWidth)
    const height = Math.max(1, canvasWrapperRef.value.clientHeight)

    scene = new THREE.Scene()

    camera = new THREE.PerspectiveCamera(52, width / height, 0.1, 3000)
    camera.position.set(0, 62, 112)

    renderer = new THREE.WebGLRenderer({ canvas: canvasRef.value, antialias: true })
    renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2))
    renderer.setSize(width, height, false)
    disposeSceneLighting = lightStationScene(scene, renderer)

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
    controls.minDistance = 1.2
    controls.maxDistance = 900
    controls.maxPolarAngle = Math.PI * 0.49

    layoutGroup = new THREE.Group()
    scene.add(layoutGroup)

    trainGroup = new THREE.Group()
    scene.add(trainGroup)

    rebuildScene()
    ensureRafLoop()
}

function disposeObject3D(obj: THREE.Object3D) {
    const geometries = new Set<THREE.BufferGeometry>()
    const materials = new Set<THREE.Material>()
    const textures = new Set<THREE.Texture>()
    obj.traverse((child) => {
        const css = child as unknown as { isCSS2DObject?: boolean; element?: HTMLElement }
        if (css.isCSS2DObject) css.element?.remove()
        const mesh = child as THREE.Mesh
        if (mesh.geometry) geometries.add(mesh.geometry)
        const owned = Array.isArray(mesh.material) ? mesh.material : mesh.material ? [mesh.material] : []
        owned.forEach(material => {
            materials.add(material)
            Object.values(material).forEach(value => {
                if (value instanceof THREE.Texture) textures.add(value)
            })
        })
        if (child instanceof THREE.InstancedMesh) child.dispose()
    })
    geometries.forEach(geometry => geometry.dispose())
    materials.forEach(material => material.dispose())
    textures.forEach(texture => texture.dispose())
}

function clearGroup(group: THREE.Group | null) {
    if (!group) return
    disposeObject3D(group)
    group.clear()
}

function setShadow(mesh: THREE.Object3D, cast = true, receive = true) {
    mesh.castShadow = cast
    mesh.receiveShadow = receive
}

function addGround(mapper: LayoutMapper) {
    if (!layoutGroup) return
    const size = Math.max(mapper.worldWidth, mapper.worldDepth, MIN_WORLD_SPAN) + 80
    layoutGroup.add(createStationGround(size, size))
}

function getLayoutTrackGaugeUnits() {
    const centerSpacingUnits = layoutGridSpacing.value * TRACK_CENTERLINE_GRID_COUNT
    return centerSpacingUnits * STANDARD_TRACK_GAUGE_MM / TRACK_CENTERLINE_SPACING_MM
}

function getWorldTrackGauge(mapper: LayoutMapper) {
    return Math.max(0.001, mapper.mapLength(getLayoutTrackGaugeUnits()))
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

function readSignalElementNumber(attrs: Record<string, unknown> | undefined, key: string): number | null {
    if (!attrs) return null
    return toFiniteNumberOrNull(attrs[key])
}

function parseSignalLightColor(value: unknown): number | null {
    const raw = String(value ?? '').trim()
    if (!raw || raw.toLowerCase() === 'none' || raw.toLowerCase() === 'transparent') return null

    try {
        return new THREE.Color().setStyle(raw).getHex()
    } catch {
        return null
    }
}

function isWhiteSignalColor(color: number): boolean {
    const threeColor = new THREE.Color(color)
    return threeColor.r >= 0.82 && threeColor.g >= 0.82 && threeColor.b >= 0.82
}

function createSignalLightMaterial(color: number): THREE.MeshStandardMaterial {
    const baseColor = new THREE.Color(color)
    const emissive = baseColor.clone()
    const isWhite = isWhiteSignalColor(color)

    if (isWhite) {
        emissive.set(0x94a3b8)
    } else {
        emissive.multiplyScalar(0.55)
    }

    return new THREE.MeshStandardMaterial({
        color,
        emissive,
        emissiveIntensity: isWhite ? 0.2 : 0.48,
        roughness: 0.38,
        metalness: 0.02,
    })
}

function getSignalLightSources(signal: Signal): SignalLightSource[] {
    const asset = getSignalStyleAsset(signal.type) as SignalStyleAsset
    const elements = Array.isArray(asset?.elements) ? asset.elements : []

    return elements
        .filter((element) => String(element?.tag || '').toLowerCase() === 'circle')
        .map((element) => {
            const attrs = element.attrs
            const x = readSignalElementNumber(attrs, 'cx')
            const y = readSignalElementNumber(attrs, 'cy')
            const radius = readSignalElementNumber(attrs, 'r')
            const color = parseSignalLightColor(attrs?.fill)
            if (x === null || y === null || radius === null || color === null || radius <= 0) return null

            return { x, y, radius, color }
        })
        .filter((source: SignalLightSource | null): source is SignalLightSource => source !== null)
}

function chooseSignalLightGroupColor(sources: SignalLightSource[]): number {
    const sortedByRadius = [...sources].sort((a, b) => a.radius - b.radius)
    const innerColoredSource = sortedByRadius.find((source) => !isWhiteSignalColor(source.color))
    return innerColoredSource?.color || sortedByRadius[sortedByRadius.length - 1]?.color || 0xf8fafc
}

function groupSignalLightSources(sources: SignalLightSource[]): SignalLightSpec[] {
    const groups: SignalLightSource[][] = []
    for (const source of sources) {
        const matchingGroup = groups.find((group) => {
            const first = group[0]
            return first ? Math.hypot(first.x - source.x, first.y - source.y) < 1 : false
        })

        if (matchingGroup) {
            matchingGroup.push(source)
        } else {
            groups.push([source])
        }
    }

    return groups
        .map((group) => {
            const largestSource = [...group].sort((a, b) => b.radius - a.radius)[0]
            if (!largestSource) return null

            return {
                x: largestSource.x,
                y: largestSource.y,
                color: chooseSignalLightGroupColor(group),
            }
        })
        .filter((spec: SignalLightSpec | null): spec is SignalLightSpec => spec !== null)
        .sort((a, b) => a.y - b.y || a.x - b.x)
}

function countDistinctSignalAxisValues(lights: SignalLightSpec[], axis: 'x' | 'y'): number {
    const sortedValues = lights
        .map((light) => light[axis])
        .sort((a, b) => a - b)
    const groups: number[] = []
    for (const value of sortedValues) {
        const previous = groups[groups.length - 1]
        if (previous === undefined || Math.abs(value - previous) >= 1) {
            groups.push(value)
        }
    }

    return Math.max(1, groups.length)
}

function buildSignalLightLayout(signal: Signal): SignalLightLayout {
    const lights = groupSignalLightSources(getSignalLightSources(signal))
    const fallbackLights = lights.length > 0
        ? lights
        : [
            { x: 0, y: 0, color: 0xf8fafc },
            { x: 0, y: 1, color: 0x22c55e },
            { x: 0, y: 2, color: 0xe11d48 },
        ]

    const minX = Math.min(...fallbackLights.map((light) => light.x))
    const maxX = Math.max(...fallbackLights.map((light) => light.x))
    const minY = Math.min(...fallbackLights.map((light) => light.y))
    const maxY = Math.max(...fallbackLights.map((light) => light.y))
    const sourceWidth = Math.max(0.001, maxX - minX)
    const sourceHeight = Math.max(0.001, maxY - minY)
    const columnCount = countDistinctSignalAxisValues(fallbackLights, 'x')
    const rowCount = countDistinctSignalAxisValues(fallbackLights, 'y')
    const width = Math.max(0.22, columnCount * SIGNAL_LIGHT_COLUMN_SPACING + SIGNAL_LIGHT_PADDING)
    const height = Math.max(0.38, rowCount * SIGNAL_LIGHT_ROW_SPACING + SIGNAL_LIGHT_PADDING)
    const usableWidth = Math.max(0.001, width - SIGNAL_LIGHT_RADIUS * 2.15)
    const usableHeight = Math.max(0.001, height - SIGNAL_LIGHT_RADIUS * 2.15)

    return {
        width,
        height,
        radius: SIGNAL_LIGHT_RADIUS,
        lights: fallbackLights.map((light) => ({
            color: light.color,
            x: sourceWidth <= 0.001 ? 0 : ((light.x - minX) / sourceWidth - 0.5) * usableWidth,
            y: sourceHeight <= 0.001 ? 0 : (0.5 - (light.y - minY) / sourceHeight) * usableHeight,
        })),
    }
}

function addSignal(signal: Signal, layout: StationLayoutData, mapper: LayoutMapper, materials: SceneMaterials) {
    if (!layoutGroup) return

    const trackPosition = mapper.mapPoint(signal.position)
    const trackTangent = getSignalTrackTangent(layout, signal)
    const directionProfile = getSignalDirectionProfile(signal.direction)
    const sideNormal = getTrackLeftNormal(trackTangent)
    const sideVector = mapDirectionVectorToWorld(sideNormal).multiplyScalar(directionProfile.sideSign)
    const faceVector = mapDirectionVectorToWorld(trackTangent).multiplyScalar(directionProfile.faceSign)
    const gauge = getWorldTrackGauge(mapper)
    const position = trackPosition.clone().addScaledVector(sideVector, SIGNAL_SIDE_OFFSET * gauge)

    const group = new THREE.Group()
    group.position.set(position.x, 0, position.z)

    const localZAxis = faceVector.clone().multiplyScalar(-1).normalize()
    const localXAxis = new THREE.Vector3().crossVectors(new THREE.Vector3(0, 1, 0), localZAxis).normalize()
    const armSign = localXAxis.dot(sideVector.clone().multiplyScalar(-1)) >= 0 ? 1 : -1
    setObjectBasis(group, localXAxis, localZAxis)
    group.scale.setScalar(gauge)

    const base = new THREE.Mesh(new THREE.CylinderGeometry(0.16, 0.22, 0.12, 18), materials.signalPost)
    base.position.y = 0.06
    setShadow(base, true, true)
    group.add(base)

    const post = new THREE.Mesh(new THREE.CylinderGeometry(0.045, 0.055, 1.24, 14), materials.signalPost)
    post.position.y = 0.72
    setShadow(post, true, true)
    group.add(post)

    const lightLayout = buildSignalLightLayout(signal)
    const headCenterY = 1.08 + lightLayout.height / 2
    const headCenterX = 0.52 * armSign

    const arm = new THREE.Mesh(new THREE.BoxGeometry(0.52, 0.045, 0.045), materials.signalPost)
    arm.position.set(0.24 * armSign, 1.3, 0)
    setShadow(arm, true, true)
    group.add(arm)

    const head = new THREE.Mesh(
        new THREE.BoxGeometry(lightLayout.width, lightLayout.height, SIGNAL_HEAD_DEPTH),
        materials.signalHead,
    )
    head.position.set(headCenterX, headCenterY, 0)
    setShadow(head, true, true)
    group.add(head)

    lightLayout.lights.forEach((light) => {
        const mesh = new THREE.Mesh(
            new THREE.SphereGeometry(lightLayout.radius, 18, 12),
            createSignalLightMaterial(light.color),
        )
        mesh.position.set(headCenterX + light.x, headCenterY + light.y, SIGNAL_HEAD_DEPTH / 2 + 0.012)
        setShadow(mesh, true, false)
        group.add(mesh)
    })

    layoutGroup.add(group)
    addLabel(
        signal.name || signal.id,
        new THREE.Vector3(position.x, gauge * Math.max(SIGNAL_LABEL_Y, headCenterY + lightLayout.height / 2 + 0.2), position.z),
        'layout3d-label-signal',
    )
}

function addPlatform(platform: Platform, mapper: LayoutMapper) {
    if (!layoutGroup) return
    const gauge = getWorldTrackGauge(mapper)
    const center = mapper.mapPoint({ x: platform.x + platform.width / 2, y: platform.y + platform.height / 2 })
    const width = Math.max(PLATFORM_MIN_SIZE, mapper.mapLength(platform.width))
    const depth = Math.max(PLATFORM_MIN_SIZE, mapper.mapLength(platform.height))
    const group = createStationPlatform(width, depth, gauge)
    group.position.set(center.x, 0, center.z)
    layoutGroup.add(group)
    addLabel(platform.name || platform.id, new THREE.Vector3(center.x, gauge * 5.1, center.z), 'layout3d-label-platform')
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

async function prepareTrainModels() {
    const version = ++trainModelPreparationVersion
    const specs = new Map<string, RollingStockTemplateSpec>()
    for (const { modelId, carCount } of trainConsistByRun.value.values()) {
        for (let carIndex = 0; carIndex < carCount; carIndex++) {
            const role = carIndex === 0 ? 'head' : carIndex === carCount - 1 ? 'tail' : 'middle'
            specs.set(`${modelId}-${role}-${carIndex}`, { modelId, role, carIndex })
        }
    }
    preparingTrainModels.value = specs.size > 0
    try {
        await trainModelTemplates.prepare([...specs.values()])
        if (isDisposed || version !== trainModelPreparationVersion) return
        // Use the station's lights/environment when warming shaders, before a
        // model's first scheduled arrival. The temporary objects share assets.
        if (renderer && camera && scene) {
            const warmup = new THREE.Group()
            for (const spec of specs.values()) {
                const model = trainModelTemplates.instantiate(spec)
                if (model) warmup.add(model)
            }
            // compileAsync starts uncancellable polling that can outlive the
            // renderer on tab exit; start compilation without a polling loop.
            renderer.compile(warmup, camera, scene)
            warmup.clear()
        }
        updateTrainObjects()
    } catch (error) {
        if (isDisposed || version !== trainModelPreparationVersion) return
        emit('renderError')
        console.error('Failed to prepare train models', error)
        ElMessage.error(t('stationLayout3d.messages.trainModelLoadFailed'))
    } finally {
        if (!isDisposed && version === trainModelPreparationVersion) {
            preparingTrainModels.value = false
        }
    }
}

function createTrainCarObject(car: SimulationTrainCar): TrainCarObjectEntry | undefined {
    const model = trainModelTemplates.instantiate(car)
    if (!model) return undefined
    const group = new THREE.Group()
    group.add(model)
    const labelElement = document.createElement('div')
    labelElement.className = 'layout3d-label layout3d-label-train'
    const label = new CSS2DObject(labelElement)
    group.add(label)
    return { group, model, label, labelElement,
        frontBogie: model.getObjectByName('bogie-front'), rearBogie: model.getObjectByName('bogie-rear') }
}

// S^-1 R S steers a pivot in physical space despite the model's normalized axes.
function steerBogie(pivot: THREE.Object3D | undefined, angle: number, bodyAngle: number, scale: THREE.Vector3) {
    if (!pivot) return
    const yaw = -(angle - bodyAngle) * Math.PI / 180
    const cosine = Math.cos(yaw)
    const sine = Math.sin(yaw)
    pivot.matrixAutoUpdate = false
    pivot.matrix.set(
        cosine, 0, sine * scale.z / scale.x, pivot.position.x,
        0, 1, 0, pivot.position.y,
        -sine * scale.x / scale.z, 0, cosine, pivot.position.z,
        0, 0, 0, 1,
    )
    pivot.matrixWorldNeedsUpdate = true
}

function updateTrainCarObject(entry: TrainCarObjectEntry, car: SimulationTrainCar, mapper: LayoutMapper) {
    const gauge = getWorldTrackGauge(mapper)
    const length = mapper.mapLength(car.length)
    const width = mapper.mapLength(car.width)
    const height = mapper.mapLength(car.height)
    const position = mapper.mapPoint(car)
    entry.group.position.set(position.x, getRailwayDimensions(gauge).railTop, position.z)
    entry.group.rotation.y = -normalizePathAngle(car.angle) * Math.PI / 180
    entry.model.scale.set(length, height, width)
    steerBogie(entry.frontBogie, car.frontBogie.angle, car.angle, entry.model.scale)
    steerBogie(entry.rearBogie, car.rearBogie.angle, car.angle, entry.model.scale)
    entry.label.position.set(0, height + gauge * 0.45, 0)
    if (entry.labelElement.textContent !== (car.label || '')) entry.labelElement.textContent = car.label || ''
    entry.label.visible = Boolean(car.label)
    entry.labelElement.style.borderColor = car.fill
}

function removeTrainCarObject(key: string) {
    const entry = trainCarObjectMap.get(key)
    if (!entry) return
    trainGroup?.remove(entry.group)
    // Instances own transforms and labels; geometries/materials belong to the
    // templates and must survive departures, seeking and later arrivals.
    entry.labelElement.remove()
    entry.group.clear()
    trainCarObjectMap.delete(key)
}

function clearTrainObjects() {
    Array.from(trainCarObjectMap.keys()).forEach(removeTrainCarObject)
}

function updateTrainObjects(currentSeconds?: number) {
    if (isDisposed) return
    if (!trainGroup || !lastMapper) {
        clearTrainObjects()
        return
    }

    const cars = currentSeconds === undefined ? simulationTrainCars.value : buildSimulationTrainCars(currentSeconds)
    const visibleKeys = new Set(cars.map((car) => car.key))
    Array.from(trainCarObjectMap.keys()).forEach((key) => {
        if (!visibleKeys.has(key)) removeTrainCarObject(key)
    })

    cars.forEach((car) => {
        let entry = trainCarObjectMap.get(car.key)
        if (!entry) {
            entry = createTrainCarObject(car)
            if (!entry) return
            trainCarObjectMap.set(car.key, entry)
            trainGroup?.add(entry.group)
        }
        updateTrainCarObject(entry, car, lastMapper as LayoutMapper)
    })
}

function updateTrackOccupancy() {
    if (isDisposed || !trackOccupancyOverlay) return
    trackOccupancyOverlay.group.visible = showTrackOccupancy.value
    if (!showTrackOccupancy.value) return
    const state = trackOccupancyTimeline.value.sample(playheadSeconds.value)
    if (state !== lastTrackOccupancyState) {
        trackOccupancyOverlay.update(state)
        lastTrackOccupancyState = state
    }
}

function addTrackOccupancyOverlay(
    paths: Array<{ id: string; points: THREE.Vector3[] }>,
    layout: StationLayoutData,
    mapper: LayoutMapper,
) {
    if (!layoutGroup) return
    // Bind IDs explicitly; Link IDs may themselves contain "-visible-" or "curve-".
    const bindings = new Map<string, { linkId: string } | { linkIds: readonly [string, string]; points: THREE.Vector3[] }>()
    for (const segment of buildVisibleTrackSegments(layout)) {
        bindings.set(segment.id, { linkId: segment.line.id })
    }
    for (const curve of layout.curves) {
        bindings.set(`curve-${curve.id}`, {
            linkIds: [curve.tangentLinkID1, curve.tangentLinkID2],
            points: buildCurveSamplePoints(curve, 48).map(point => mapper.mapPoint(point)),
        })
    }
    trackOccupancyOverlay = createTrackOccupancyOverlay(
        mapTrackOccupancyPaths(paths, bindings), getWorldTrackGauge(mapper),
    )
    layoutGroup.add(trackOccupancyOverlay.group)
    updateTrackOccupancy()
}

function rebuildScene() {
    if (isDisposed || !layoutGroup) return
    trackOccupancyOverlay = null
    lastTrackOccupancyState = null
    clearGroup(layoutGroup)
    lastMapper = null

    const layout = displayLayoutData.value
    const mapper = createMapper(layout)
    if (!mapper) {
        clearTrainObjects()
        renderOnce()
        return
    }

    lastMapper = mapper
    const materials = createMaterials()
    addGround(mapper)

    const paths = displayRailwayPaths.value.map(path => ({
        id: path.id,
        points: path.points.map(point => mapper.mapPoint({ x: point.x, y: point.z })),
    }))
    const railway = createRailway(paths, layout.switches.map(sw => ({
        id: sw.id, position: mapper.mapPoint(sw.position),
    })), getWorldTrackGauge(mapper))
    layoutGroup.add(railway)
    addTrackOccupancyOverlay(railway.userData.railway.paths, layout, mapper)
    for (const platform of layout.platforms) addPlatform(platform, mapper)
    for (const signal of layout.signals) addSignal(signal, layout, mapper, materials)
    if (!layout.signals.length) Object.values(materials).forEach(material => material.dispose())
    for (const sw of layout.switches) addLabel(sw.name || sw.id,
        mapper.mapPoint(sw.position, getWorldTrackGauge(mapper) * 0.9), 'layout3d-label-switch')

    addTrackLabels(layout, mapper)

    fitCameraToLayout()
    updateTrainObjects()
    renderOnce()
}

function formatDisplayRatio(value: number) {
    return `${value.toFixed(2)}:1`
}

function handleDisplayRatioChange() {
    appliedDisplayRatio.value = displayRatio.value
}

function resetDisplayRatio() {
    displayRatio.value = 1
    handleDisplayRatioChange()
}

function fitCameraToLayout() {
    if (!camera || !controls || !lastMapper) return
    const worldWidth = lastMapper.worldWidth
    const span = Math.max(worldWidth, lastMapper.worldDepth, MIN_WORLD_SPAN)
    // Fit all corners in the tilted camera's view, including the nearer edges.
    // A width-only distance clips wide layouts in a shallow viewport.
    const halfFov = THREE.MathUtils.degToRad(camera.fov / 2)
    const tanVertical = Math.tan(halfFov) * 0.88
    const tanHorizontal = tanVertical * camera.aspect
    const viewDirection = new THREE.Vector3(0.08, 0.54, 0.84).normalize()
    const viewRight = new THREE.Vector3().crossVectors(camera.up, viewDirection).normalize()
    const viewUp = new THREE.Vector3().crossVectors(viewDirection, viewRight)
    const gauge = getWorldTrackGauge(lastMapper)
    controls.target.set(0, 0.22, 0)
    let distance = 40
    for (const x of [-1, 1]) for (const y of [0, gauge * 5]) for (const z of [-1, 1]) {
        const corner = new THREE.Vector3(
            x * (worldWidth / 2 + gauge), y,
            z * (lastMapper.worldDepth / 2 + gauge),
        ).sub(controls.target)
        const depthOffset = corner.dot(viewDirection)
        distance = Math.max(distance,
            depthOffset + Math.abs(corner.dot(viewRight)) / tanHorizontal,
            depthOffset + Math.abs(corner.dot(viewUp)) / tanVertical)
    }
    controls.maxDistance = Math.max(900, distance * 2)
    camera.position.copy(controls.target).addScaledVector(viewDirection, distance)
    camera.near = 0.1
    camera.far = Math.max(1000, span * 14, distance * 3)
    camera.updateProjectionMatrix()
    controls.update()
    if (scene) fitStationLighting(scene, worldWidth, lastMapper.worldDepth,
        camera.position.distanceTo(controls.target))
}

function resetCamera() {
    fitCameraToLayout()
}

function renderOnce() {
    if (isDisposed) return
    if (controls) controls.update()
    if (renderer && scene && camera) renderer.render(scene, camera)
    if (labelRenderer && scene && camera) labelRenderer.render(scene, camera)
}

function rafTick() {
    rafId = null
    if (isDisposed || !renderer) return
    if (isPlaying.value) updateTrainObjects(props.getPlaybackSeconds())
    renderOnce()
    ensureRafLoop()
}

function ensureRafLoop() {
    if (!isDisposed && renderer && rafId === null) {
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

watch([displayLayoutData, layoutGridSpacing], rebuildScene, { flush: 'post' })
watch(trainConsistByRun, () => {
    clearTrainCarAngleMemory()
    void prepareTrainModels()
}, { flush: 'post' })
watch(playheadSeconds, () => {
    updateTrainObjects()
    updateTrackOccupancy()
    if (!isPlaying.value) renderOnce()
}, { flush: 'post' })
watch([showTrackOccupancy, trackOccupancyTimeline], updateTrackOccupancy, { flush: 'post' })

onMounted(() => {
    nextTick(() => {
        if (isDisposed) return
        try {
            initThree()
            resizeObserver = new ResizeObserver(onResize)
            if (canvasWrapperRef.value) resizeObserver.observe(canvasWrapperRef.value)
            onResize()
            void prepareTrainModels()
        } catch (error) {
            renderError.value = true
            emit('renderError')
            console.error('Failed to initialize 3D simulation', error)
        }
    })
})

onBeforeUnmount(() => {
    isDisposed = true
    trainModelPreparationVersion++
    cancelRafLoop()
    if (resizeObserver) {
        resizeObserver.disconnect()
        resizeObserver = null
    }
    clearTrainObjects()
    trainModelTemplates.dispose()
    clearTrainCarAngleMemory()
    if (trainGroup && scene) scene.remove(trainGroup)
    trainGroup = null
    trackOccupancyOverlay = null
    lastTrackOccupancyState = null
    clearGroup(layoutGroup)
    if (layoutGroup && scene) scene.remove(layoutGroup)
    layoutGroup = null
    if (controls) {
        controls.dispose()
        controls = null
    }
    disposeSceneLighting?.()
    disposeSceneLighting = null
    if (renderer) {
        renderer.dispose()
        renderer.forceContextLoss()
        renderer = null
    }
    if (labelRendererRoot?.parentElement) {
        labelRendererRoot.parentElement.removeChild(labelRendererRoot)
    }
    labelRenderer = null
    labelRendererRoot = null
    scene?.clear()
    scene = null
    camera = null
    lastMapper = null
})
</script>

<style scoped>
.station-layout-3d-page { display:flex; flex:1 1 0; flex-direction:column; min-width:0; min-height:220px; overflow:hidden; }
.layout3d-toolbar { display:flex; flex-wrap:wrap; align-items:center; gap:8px 14px; padding:6px 10px; border-bottom:1px solid var(--sy-border); }
.layout3d-scheme-control, .layout3d-ratio-control, .station-toolbar-switch-control { display:flex; align-items:center; gap:6px; min-width:0; font-size:12px; }
.layout3d-control-label, .station-toolbar-switch-control__label { white-space:nowrap; color:var(--sy-text-muted); }
.layout3d-model-select { width:170px; }
.layout3d-ratio-slider { width:100px; }
.layout3d-ratio-value { min-width:46px; font-variant-numeric:tabular-nums; }
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
    border-radius: var(--sy-radius, 6px);
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

:deep(.layout3d-label-train) {
    border-color: rgba(37, 99, 235, 0.35);
    background: rgba(37, 99, 235, 0.9);
    color: #ffffff;
    font-weight: 700;
}

</style>
