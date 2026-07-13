<template>
    <section class="station-layout-3d-page" v-loading="loadingData">
        <div class="layout3d-toolbar">
            <div class="layout3d-toolbar-left">
                <div class="layout3d-scheme-control">
                    <span class="layout3d-control-label">{{ t('stationLayout.menu.stationScheme') }}</span>
                    <el-select
                        v-model="currentStationSchemeId"
                        size="small"
                        filterable
                        class="layout3d-scheme-select"
                        :loading="loadingStationSchemes"
                        :disabled="!selectedInstanceId || loadingStationSchemes || loadingData"
                        :placeholder="t('stationLayout.placeholders.selectStationScheme')"
                        @change="handleStationSchemeChange"
                    >
                        <el-option
                            v-for="option in stationSchemeOptions"
                            :key="option.id"
                            :label="formatStationSchemeLabel(option)"
                            :value="option.id"
                        />
                    </el-select>
                </div>

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
import { getSignalStyleAsset } from '@/assets/stationLayoutSignalStyles'

interface Props {
    selectedInstanceId?: string | null
    activationKey?: number
}

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

interface SwitchRenderBranch {
    direction: THREE.Vector3
    sourceLength: number
    renderLength: number
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

interface StationSchemeOption {
    id: string
    name: string
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
    switchMarker: THREE.MeshStandardMaterial
    switchPoint: THREE.MeshStandardMaterial
    switchGuard: THREE.MeshStandardMaterial
    switchTie: THREE.MeshStandardMaterial
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
const SIGNAL_SIDE_OFFSET = 1.45
const SIGNAL_LABEL_Y = 1.92
const SIGNAL_HEAD_DEPTH = 0.11
const SIGNAL_LIGHT_RADIUS = 0.07
const SIGNAL_LIGHT_ROW_SPACING = 0.17
const SIGNAL_LIGHT_COLUMN_SPACING = 0.18
const SIGNAL_LIGHT_PADDING = 0.16
const SWITCH_BRANCH_LENGTH = 4.8
const SWITCH_MIN_BRANCH_LENGTH = 2.6
const SWITCH_POINT_BLADE_LENGTH = 2.15
const SWITCH_FROG_DISTANCE = 3.15
const SWITCH_BRANCH_DUPLICATE_DOT = 0.996

const canvasWrapperRef = ref<HTMLElement | null>(null)
const canvasRef = ref<HTMLCanvasElement | null>(null)
const layoutData = ref<StationLayoutData>(createEmptyLayout())
const loadingData = ref(false)
const loadErrorMessage = ref('')
const showLabels = ref(true)
const currentStationSchemeId = ref('')
const loadingStationSchemes = ref(false)
const stationSchemeOptions = ref<StationSchemeOption[]>([])

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
let stationSchemeLoadVersion = 0
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

function normalizeStationSchemeOption(item: any): StationSchemeOption | null {
    const id = readString(item, 'id', 'ID').trim()
    if (!id) return null

    const name = readString(item, 'name', 'Name').trim() || id
    return { id, name }
}

function setStationSchemeOptions(options: StationSchemeOption[], includeCurrent = true) {
    const optionsById = new Map<string, StationSchemeOption>()
    for (const option of options) {
        if (!option.id || optionsById.has(option.id)) continue
        optionsById.set(option.id, option)
    }

    stationSchemeOptions.value = Array.from(optionsById.values())
    if (includeCurrent) ensureCurrentStationSchemeOption()
}

function ensureCurrentStationSchemeOption(name?: string) {
    const id = currentStationSchemeId.value.trim()
    if (!id) return
    if (stationSchemeOptions.value.some((option) => option.id === id)) return

    stationSchemeOptions.value = [
        ...stationSchemeOptions.value,
        {
            id,
            name: name || id,
        },
    ]
}

function formatStationSchemeLabel(option: StationSchemeOption): string {
    return option.name || option.id
}

async function loadStationSchemes(options: { includeCurrent?: boolean } = {}) {
    const includeCurrent = options.includeCurrent !== false
    const instanceID = selectedInstanceId.value
    if (!instanceID) {
        stationSchemeLoadVersion++
        currentStationSchemeId.value = ''
        stationSchemeOptions.value = []
        loadingStationSchemes.value = false
        return []
    }

    const loadVersion = ++stationSchemeLoadVersion
    loadingStationSchemes.value = true
    try {
        const response = await axios.get('/StationLayout/GetStationSchemes', {
            params: { instanceID },
        })
        if (loadVersion !== stationSchemeLoadVersion || instanceID !== selectedInstanceId.value) return []

        const options = (Array.isArray(response.data) ? response.data : [])
            .map((item: any) => normalizeStationSchemeOption(item))
            .filter((item: StationSchemeOption | null): item is StationSchemeOption => item !== null)
        setStationSchemeOptions(options, includeCurrent)
        return options
    } catch (error) {
        if (loadVersion !== stationSchemeLoadVersion || instanceID !== selectedInstanceId.value) return []

        console.error('Failed to load station schemes:', error)
        stationSchemeOptions.value = []
        ElMessage.error(t('stationLayout.messages.loadSchemesFailed'))
        return []
    } finally {
        if (loadVersion === stationSchemeLoadVersion && instanceID === selectedInstanceId.value) {
            loadingStationSchemes.value = false
        }
    }
}

function handleStationSchemeChange() {
    void loadLayout()
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
        switchMarker: new THREE.MeshStandardMaterial({ color: 0xf59e0b, roughness: 0.55, metalness: 0.15 }),
        switchPoint: new THREE.MeshStandardMaterial({ color: 0xd97706, roughness: 0.42, metalness: 0.35 }),
        switchGuard: new THREE.MeshStandardMaterial({ color: 0x232b34, roughness: 0.38, metalness: 0.7 }),
        switchTie: new THREE.MeshStandardMaterial({ color: 0x513a27, roughness: 0.88, metalness: 0.05 }),
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
    camera.position.set(0, 62, 112)

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
    const position = trackPosition.clone().addScaledVector(sideVector, SIGNAL_SIDE_OFFSET)

    const group = new THREE.Group()
    group.position.set(position.x, 0, position.z)

    const localZAxis = faceVector.clone().multiplyScalar(-1).normalize()
    const localXAxis = new THREE.Vector3().crossVectors(new THREE.Vector3(0, 1, 0), localZAxis).normalize()
    const armSign = localXAxis.dot(sideVector.clone().multiplyScalar(-1)) >= 0 ? 1 : -1
    setObjectBasis(group, localXAxis, localZAxis)

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
        new THREE.Vector3(position.x, Math.max(SIGNAL_LABEL_Y, headCenterY + lightLayout.height / 2 + 0.2), position.z),
        'layout3d-label-signal',
    )
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

function getSwitchBranchVectors(sw: SwitchDevice, layout: StationLayoutData): SwitchBranchVector[] {
    if (sw.branchVectorList.length > 0) return sw.branchVectorList

    return getAdjacentTrackVectorCandidates(layout, sw.bindingNodeID).map((candidate) => ({
        x: candidate.vector.x,
        y: candidate.vector.y,
        lineID: candidate.lineID,
    }))
}

function buildSwitchRenderBranches(sw: SwitchDevice, layout: StationLayoutData, mapper: LayoutMapper): SwitchRenderBranch[] {
    const branches: SwitchRenderBranch[] = []
    for (const branchVector of getSwitchBranchVectors(sw, layout)) {
        const unit = normalizeVector2D(branchVector)
        if (!unit) continue

        const direction = mapDirectionVectorToWorld(unit)
        if (branches.some((branch) => branch.direction.dot(direction) > SWITCH_BRANCH_DUPLICATE_DOT)) continue

        const sourceLength = getVectorLength2D(branchVector)
        const renderLength = Math.max(
            SWITCH_MIN_BRANCH_LENGTH,
            Math.min(SWITCH_BRANCH_LENGTH, mapper.mapLength(sourceLength) * 0.22),
        )
        branches.push({
            direction,
            sourceLength,
            renderLength,
            lineID: branchVector.lineID,
        })
    }

    return branches
}

function addBeamBetweenWorld(
    group: THREE.Group,
    start: THREE.Vector3,
    end: THREE.Vector3,
    width: number,
    height: number,
    material: THREE.Material,
    centerY: number,
) {
    const dx = end.x - start.x
    const dz = end.z - start.z
    const length = Math.hypot(dx, dz)
    if (length <= 0.000001) return null

    const beam = new THREE.Mesh(new THREE.BoxGeometry(length, height, width), material)
    beam.position.set((start.x + end.x) / 2, centerY, (start.z + end.z) / 2)
    beam.rotation.y = -Math.atan2(dz, dx)
    setShadow(beam, true, true)
    group.add(beam)
    return beam
}

function getWorldLeftNormal(direction: THREE.Vector3): THREE.Vector3 {
    return new THREE.Vector3(direction.z, 0, -direction.x).normalize()
}

function addSwitchRouteRails(group: THREE.Group, origin: THREE.Vector3, branch: SwitchRenderBranch, materials: SceneMaterials) {
    const normal = getWorldLeftNormal(branch.direction)
    const routeStartDistance = 0.08
    const routeEndDistance = branch.renderLength
    for (const railOffset of [-TRACK_GAUGE / 2, TRACK_GAUGE / 2]) {
        const start = origin
            .clone()
            .addScaledVector(branch.direction, routeStartDistance)
            .addScaledVector(normal, railOffset)
        const end = origin
            .clone()
            .addScaledVector(branch.direction, routeEndDistance)
            .addScaledVector(normal, railOffset)
        addBeamBetweenWorld(group, start, end, RAIL_WIDTH * 1.2, RAIL_HEIGHT * 1.18, materials.switchGuard, RAIL_Y + 0.045)
    }
}

function addSwitchTieFan(group: THREE.Group, origin: THREE.Vector3, branches: SwitchRenderBranch[], materials: SceneMaterials) {
    for (const branch of branches) {
        const maxDistance = Math.min(branch.renderLength - 0.25, 3.35)
        for (let distance = 0.72; distance <= maxDistance; distance += 0.72) {
            const center = origin.clone().addScaledVector(branch.direction, distance)
            const normal = getWorldLeftNormal(branch.direction)
            const start = center.clone().addScaledVector(normal, -SLEEPER_LENGTH * 0.58)
            const end = center.clone().addScaledVector(normal, SLEEPER_LENGTH * 0.58)
            addBeamBetweenWorld(group, start, end, SLEEPER_WIDTH * 0.92, SLEEPER_HEIGHT * 0.9, materials.switchTie, SLEEPER_Y + 0.02)
        }
    }
}

function findSwitchRoutePair(branches: SwitchRenderBranch[]) {
    if (branches.length < 3) return null

    let routeA = branches[0]
    let routeB = branches[1]
    let bestDot = -Infinity
    for (let i = 0; i < branches.length; i++) {
        for (let j = i + 1; j < branches.length; j++) {
            const first = branches[i]
            const second = branches[j]
            if (!first || !second) continue

            const dot = first.direction.dot(second.direction)
            if (dot > bestDot) {
                bestDot = dot
                routeA = first
                routeB = second
            }
        }
    }

    if (!routeA || !routeB) return null

    const routeAverage = routeA.direction.clone().add(routeB.direction)
    if (routeAverage.lengthSq() <= 0.000001) return null
    routeAverage.normalize()

    let stem: SwitchRenderBranch | null = null
    let stemScore = Infinity
    for (const branch of branches) {
        if (branch === routeA || branch === routeB) continue
        const score = branch.direction.dot(routeAverage)
        if (score < stemScore) {
            stemScore = score
            stem = branch
        }
    }

    return { routeA, routeB, routeAverage, stem }
}

function addSwitchPointWork(group: THREE.Group, origin: THREE.Vector3, branches: SwitchRenderBranch[], materials: SceneMaterials) {
    const routePair = findSwitchRoutePair(branches)
    if (!routePair) {
        const plate = new THREE.Mesh(new THREE.BoxGeometry(0.62, 0.07, 0.42), materials.switchPoint)
        plate.position.set(origin.x, RAIL_Y + 0.1, origin.z)
        setShadow(plate, true, true)
        group.add(plate)
        return
    }

    const stemDirection = routePair.stem?.direction || routePair.routeAverage.clone().multiplyScalar(-1)
    const toe = origin.clone().addScaledVector(stemDirection, 0.34)
    const routeTipA = origin.clone().addScaledVector(routePair.routeA.direction, SWITCH_POINT_BLADE_LENGTH)
    const routeTipB = origin.clone().addScaledVector(routePair.routeB.direction, SWITCH_POINT_BLADE_LENGTH)
    addBeamBetweenWorld(group, toe, routeTipA, 0.06, 0.055, materials.switchPoint, RAIL_Y + 0.115)
    addBeamBetweenWorld(group, toe, routeTipB, 0.06, 0.055, materials.switchPoint, RAIL_Y + 0.115)

    const throwA = origin.clone().addScaledVector(routePair.routeA.direction, 0.82)
    const throwB = origin.clone().addScaledVector(routePair.routeB.direction, 0.82)
    if (throwA.distanceTo(throwB) > 0.18) {
        addBeamBetweenWorld(group, throwA, throwB, 0.06, 0.045, materials.switchPoint, RAIL_Y + 0.16)
    }

    const frogNose = origin.clone().addScaledVector(routePair.routeAverage, SWITCH_FROG_DISTANCE)
    const frogA = origin.clone().addScaledVector(routePair.routeA.direction, SWITCH_FROG_DISTANCE + 0.75)
    const frogB = origin.clone().addScaledVector(routePair.routeB.direction, SWITCH_FROG_DISTANCE + 0.75)
    addBeamBetweenWorld(group, frogNose, frogA, 0.052, 0.052, materials.switchPoint, RAIL_Y + 0.13)
    addBeamBetweenWorld(group, frogNose, frogB, 0.052, 0.052, materials.switchPoint, RAIL_Y + 0.13)
}

function addFallbackSwitchMarker(position: THREE.Vector3, materials: SceneMaterials) {
    if (!layoutGroup) return
    const marker = new THREE.Mesh(new THREE.OctahedronGeometry(0.3, 0), materials.switchMarker)
    marker.position.set(position.x, 0.42, position.z)
    setShadow(marker, true, true)
    layoutGroup.add(marker)
}

function addSwitchDevice(sw: SwitchDevice, layout: StationLayoutData, mapper: LayoutMapper, materials: SceneMaterials) {
    if (!layoutGroup) return
    const position = mapper.mapPoint(sw.position)
    const branches = buildSwitchRenderBranches(sw, layout, mapper)
    if (branches.length < 2) {
        addFallbackSwitchMarker(position, materials)
        addLabel(sw.name || sw.id, new THREE.Vector3(position.x, 0.98, position.z), 'layout3d-label-switch')
        return
    }

    const group = new THREE.Group()
    branches.forEach((branch) => addSwitchRouteRails(group, position, branch, materials))
    addSwitchTieFan(group, position, branches, materials)
    addSwitchPointWork(group, position, branches, materials)
    layoutGroup.add(group)
    addLabel(sw.name || sw.id, new THREE.Vector3(position.x, 1.05, position.z), 'layout3d-label-switch')
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
    for (const signal of layoutData.value.signals) addSignal(signal, layoutData.value, mapper, materials)
    for (const sw of layoutData.value.switches) addSwitchDevice(sw, layoutData.value, mapper, materials)
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
    camera.position.set(0, height, distance * 1.08)
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
    const stationSchemeID = currentStationSchemeId.value.trim()
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
        const params: Record<string, string> = { instanceID }
        if (stationSchemeID) params.stationSchemeID = stationSchemeID

        const response = await axios.post('/StationLayout/GetJson', null, {
            params,
        })
        if (loadVersion !== layoutLoadVersion) return
        const resolvedStationSchemeId = readString(response.data?.metadata, 'stationSchemeID', 'StationSchemeID').trim()
        if (resolvedStationSchemeId) {
            currentStationSchemeId.value = resolvedStationSchemeId
            ensureCurrentStationSchemeOption()
        }
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
    currentStationSchemeId.value = ''
    stationSchemeOptions.value = []
    void loadStationSchemes()
    void loadLayout()
}, { immediate: true })

watch(() => props.activationKey, () => {
    if (selectedInstanceId.value) {
        void loadStationSchemes()
        void loadLayout()
    }
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

.layout3d-toolbar-left {
    display: inline-flex;
    align-items: center;
    flex-wrap: wrap;
    gap: 10px;
    min-width: 0;
}

.layout3d-scheme-control {
    display: inline-flex;
    align-items: center;
    gap: 6px;
    min-width: 0;
}

.layout3d-control-label {
    flex: 0 0 auto;
    color: #4c5968;
    font-size: 12px;
    line-height: 1;
    white-space: nowrap;
}

.layout3d-scheme-select {
    width: 180px;
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
    flex: 0 0 auto;
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
