<template>
    <section class="station-layout-3d-page" v-loading="loadingAnyData">
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

                <div class="layout3d-scheme-control">
                    <span class="layout3d-control-label">{{ t('stationLayout3d.labels.operationPlan') }}</span>
                    <el-select
                        v-model="currentOperationPlanId"
                        size="small"
                        filterable
                        class="layout3d-plan-select"
                        :loading="loadingOperationPlans"
                        :disabled="!currentStationSchemeId || loadingOperationPlans"
                        :placeholder="t('stationLayout3d.placeholders.selectOperationPlan')"
                        @change="handleOperationPlanChange"
                    >
                        <el-option
                            v-for="option in operationPlanOptions"
                            :key="option.operationPlanID"
                            :label="formatOperationPlanLabel(option)"
                            :value="option.operationPlanID"
                        />
                    </el-select>
                </div>

                <div class="layout3d-scheme-control">
                    <span class="layout3d-control-label">{{ t('stationLayout3d.labels.playbackScope') }}</span>
                    <el-radio-group
                        v-model="playbackMode"
                        size="small"
                        class="layout3d-playback-mode"
                        @change="handlePlaybackModeChange"
                    >
                        <el-radio-button value="single">{{ t('stationLayout3d.playbackModes.single') }}</el-radio-button>
                        <el-radio-button value="all">{{ t('stationLayout3d.playbackModes.all') }}</el-radio-button>
                    </el-radio-group>
                </div>

                <div class="layout3d-scheme-control">
                    <span class="layout3d-control-label">{{ t('stationLayout3d.labels.train') }}</span>
                    <el-select
                        v-model="selectedTrainId"
                        size="small"
                        filterable
                        class="layout3d-train-select"
                        :loading="loadingTrainOperationPlan"
                        :disabled="isAllTrainPlayback || trainOptions.length === 0 || loadingTrainOperationPlan"
                        :placeholder="isAllTrainPlayback ? t('stationLayout3d.placeholders.allTrains') : t('stationLayout3d.placeholders.selectTrain')"
                        @change="handleTrainChange"
                    >
                        <el-option
                            v-for="option in trainOptions"
                            :key="option.id"
                            :label="formatTrainLabel(option)"
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
                        :loading="loadingAnyData"
                        :disabled="!selectedInstanceId"
                        @click="refresh3DData"
                    />
                </el-tooltip>
                <el-tooltip :content="t('stationLayout3d.buttons.resetView')">
                    <el-button size="small" :icon="Aim" :disabled="!canRender" @click="resetCamera" />
                </el-tooltip>
                <el-tooltip :content="t('stationLayout3d.buttons.resetPlayback')">
                    <el-button
                        size="small"
                        :icon="RefreshLeft"
                        :disabled="!canPlayback"
                        @click="resetPlayback"
                    />
                </el-tooltip>
                <el-tooltip :content="isPlaying ? t('stationLayout3d.buttons.pause') : t('stationLayout3d.buttons.play')">
                    <el-button
                        size="small"
                        type="primary"
                        :icon="isPlaying ? VideoPause : VideoPlay"
                        :disabled="!canPlayback"
                        @click="togglePlayback"
                    />
                </el-tooltip>
                <span class="layout3d-playback-clock">{{ playbackClockText }}</span>
                <el-select
                    v-model="playbackSpeed"
                    size="small"
                    class="layout3d-speed-select"
                    :disabled="!canPlayback"
                    :aria-label="t('stationLayout3d.labels.speed')"
                >
                    <el-option :value="1" label="1x" />
                    <el-option :value="10" label="10x" />
                    <el-option :value="60" label="60x" />
                    <el-option :value="180" label="180x" />
                    <el-option :value="300" label="300x" />
                </el-select>
                <el-checkbox v-model="showLabels" size="small">
                    {{ t('stationLayout3d.labels.showLabels') }}
                </el-checkbox>
            </div>
        </div>

        <div class="layout3d-playback-bar">
            <div class="layout3d-playback-summary">
                <el-tag size="small" :type="playbackStatusTagType">
                    {{ playbackStatusText }}
                </el-tag>
                <span>{{ playbackSummaryText }}</span>
                <span>{{ activePhaseText }}</span>
            </div>
            <el-slider
                class="layout3d-playhead-slider"
                :model-value="playheadSeconds"
                :min="0"
                :max="playbackSliderMax"
                :step="0.1"
                :disabled="!canPlayback"
                :format-tooltip="formatPlayheadTooltip"
                @input="handlePlayheadInput"
            />
        </div>

        <div class="layout3d-content">
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

            <section class="layout3d-gantt-panel">
                <div class="layout3d-gantt-header">
                    <div class="layout3d-gantt-title">
                        <h3>{{ t('stationLayout3d.gantt.title') }}</h3>
                        <span>{{ ganttSummaryText }}</span>
                    </div>
                    <div class="layout3d-gantt-subtable-toolbar">
                        <el-tabs
                            v-model="activeGanttSubTableId"
                            type="card"
                            class="layout3d-gantt-sub-tabs"
                            @tab-remove="removeGanttSubTable"
                        >
                            <el-tab-pane
                                v-for="(subTable, index) in ganttSubTables"
                                :key="subTable.id"
                                :name="subTable.id"
                                :label="formatGanttSubTableLabel(subTable, index)"
                                :closable="ganttSubTables.length > 1"
                            />
                        </el-tabs>
                        <div class="layout3d-gantt-subtable-actions">
                            <span class="layout3d-gantt-subtable-summary">
                                {{ activeGanttSubTableSummaryText }}
                            </span>
                            <el-button
                                :icon="Edit"
                                circle
                                size="small"
                                :disabled="!activeGanttSubTable"
                                :title="t('stationLayout3d.buttons.editSubTable')"
                                @click="openEditGanttSubTableDialog"
                            />
                            <el-button
                                :icon="Plus"
                                circle
                                size="small"
                                :title="t('stationLayout3d.buttons.createSubTable')"
                                @click="openCreateGanttSubTableDialog"
                            />
                        </div>
                    </div>
                </div>
                <div v-if="ganttLanes.length > 0" ref="ganttViewportRef" class="layout3d-gantt-viewport">
                    <div class="layout3d-gantt-content" :style="ganttContentStyle">
                        <div class="layout3d-gantt-axis-row">
                            <div class="layout3d-gantt-axis-label">{{ t('stationLayout3d.gantt.cellAxis') }}</div>
                            <div class="layout3d-gantt-axis-track" :style="ganttTimelineStyle">
                                <div
                                    v-for="tick in ganttTicks"
                                    :key="tick.key"
                                    class="layout3d-gantt-axis-tick"
                                    :class="{ 'is-major': tick.major }"
                                    :style="getGanttTickStyle(tick)"
                                >
                                    <span>{{ tick.label }}</span>
                                </div>
                                <div class="layout3d-gantt-now-line" :style="ganttPlayheadStyle" />
                            </div>
                        </div>
                        <div v-for="lane in ganttLanes" :key="lane.key" class="layout3d-gantt-lane-row">
                            <div class="layout3d-gantt-lane-label" :title="lane.label">
                                {{ lane.label }}
                            </div>
                            <div class="layout3d-gantt-lane-track" :style="ganttTimelineStyle">
                                <div
                                    v-for="tick in ganttTicks"
                                    :key="`${lane.key}-${tick.key}`"
                                    class="layout3d-gantt-grid-line"
                                    :class="{ 'is-major': tick.major }"
                                    :style="getGanttTickStyle(tick)"
                                />
                                <div
                                    v-for="block in lane.blocks"
                                    :key="block.key"
                                    class="layout3d-gantt-block"
                                    :class="getGanttBlockClassName(block)"
                                    :style="getGanttBlockStyle(block)"
                                    :title="block.title"
                                >
                                    <span>{{ block.label }}</span>
                                </div>
                                <div class="layout3d-gantt-now-line" :style="ganttPlayheadStyle" />
                            </div>
                        </div>
                    </div>
                </div>
                <div v-else class="layout3d-gantt-empty">
                    {{ ganttEmptyText }}
                </div>
            </section>
        </div>

        <el-dialog
            v-model="ganttSubTableDialogVisible"
            :title="ganttSubTableDialogTitle"
            width="560px"
            class="layout3d-gantt-subtable-dialog"
        >
            <el-form label-position="top">
                <el-form-item :label="t('stationLayout3d.labels.subTableName')">
                    <el-input
                        v-model="ganttSubTableDialogForm.name"
                        maxlength="100"
                        show-word-limit
                        :placeholder="t('stationLayout3d.placeholders.subTableName')"
                    />
                </el-form-item>
                <el-form-item :label="t('stationLayout3d.labels.subTableCells')">
                    <el-select
                        v-model="ganttSubTableDialogForm.cellIds"
                        class="layout3d-gantt-subtable-cell-select"
                        multiple
                        filterable
                        clearable
                        collapse-tags
                        collapse-tags-tooltip
                        :placeholder="t('stationLayout3d.placeholders.selectSubTableCells')"
                    >
                        <el-option
                            v-for="cell in ganttAvailableCells"
                            :key="cell.id"
                            :label="cell.name || cell.id"
                            :value="cell.id"
                        />
                    </el-select>
                </el-form-item>
            </el-form>
            <template #footer>
                <el-button @click="ganttSubTableDialogVisible = false">
                    {{ t('stationLayout3d.dialogs.cancel') }}
                </el-button>
                <el-button type="primary" @click="confirmGanttSubTableDialog">
                    {{ t('stationLayout3d.dialogs.confirm') }}
                </el-button>
            </template>
        </el-dialog>
    </section>
</template>

<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { ElMessage } from 'element-plus'
import { Aim, Edit, Plus, RefreshLeft, RefreshRight, VideoPause, VideoPlay } from '@element-plus/icons-vue'
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

interface OperationPlanOption {
    instanceID: string
    stationSchemeID: string
    operationPlanID: string
    name: string
    description: string
    sortOrder: number | null
}

interface StationRouteOption {
    id: string
    name: string
    type: string
    nodeList: string
    linkList: string
    startNodeID: string
    endNodeID: string
}

interface StationRouteTimeOption {
    routeID: string
    trainTypeID: string
    cellID: string
    startOccupationShift: number | null
    endOccupationShift: number | null
    isInterruptCell: boolean
}

interface TrainOperationPlanTrain {
    id: string
    trainTemplateID: string
    trainNumber: string
    name: string
    trainType: string
    isFixedOperation: boolean
}

interface TrainOperationPlanMovement {
    trainID: string
    trainTemplateID: string
    movementID: string
    name: string
    routeIDList: string
    minDuration: number | null
    earliestStartTime: string
    latestEndTime: string
    route: string
    tag: string
    sortOrder: number | null
}

interface LayoutCell {
    id: string
    name: string
    linkIDList: string
}

interface RoutePoint {
    x: number
    y: number
    nodeId?: string
}

interface PathSegment {
    from: RoutePoint
    to: RoutePoint
    length: number
    startDistance: number
    angle: number
}

interface PolylinePath {
    points: RoutePoint[]
    segments: PathSegment[]
    totalLength: number
}

interface RouteGeometry {
    path: PolylinePath
    nodeIds: string[]
    linkIds: string[]
}

interface RouteRun {
    key: string
    train: TrainOperationPlanTrain
    movement: TrainOperationPlanMovement
    route: StationRouteOption
    path: PolylinePath
    nodeIds: string[]
    linkIds: string[]
    startSeconds: number
    endSeconds: number
    lockSeconds: number
    usesPlanTime: boolean
    absoluteStartSeconds: number
    absoluteEndSeconds: number
    color: string
}

interface SimulationTrainCar {
    key: string
    x: number
    y: number
    angle: number
    length: number
    width: number
    fill: string
    stroke: string
    label?: string
}

interface RouteRunSource {
    train: TrainOperationPlanTrain
    movement: TrainOperationPlanMovement
    sourceIndex: number
}

interface GanttTick {
    key: string
    seconds: number
    left: number
    label: string
    major: boolean
}

interface GanttBlock {
    key: string
    label: string
    title: string
    startSeconds: number
    endSeconds: number
    left: number
    width: number
    color: string
}

interface GanttLane {
    key: string
    label: string
    sortIndex: number
    blocks: GanttBlock[]
}

interface GanttSubTable {
    id: string
    name: string
    cellIds: string[]
    hasCustomSelection: boolean
}

interface GanttSubTableSettingPayload {
    subTableID: string
    subTableName: string
    cellIDs: string[]
    sortOrder: number
}

interface GanttSubTableDialogForm {
    name: string
    cellIds: string[]
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

interface TrainCarObjectEntry {
    group: THREE.Group
    body: THREE.Mesh
    sidePanels: THREE.Mesh[]
    endPanels: THREE.Mesh[]
    ribs: THREE.Mesh[]
    doorPanels: THREE.Mesh[]
    underframe: THREE.Mesh
    centerBeam: THREE.Mesh
    bogieFrames: THREE.Mesh[]
    axles: THREE.Mesh[]
    wheels: THREE.Mesh[]
    couplers: THREE.Mesh[]
    bodyMaterial: THREE.MeshStandardMaterial
    sidePanelMaterial: THREE.MeshStandardMaterial
    detailMaterial: THREE.MeshStandardMaterial
    label: CSS2DObject
    labelElement: HTMLElement
}

type RunPhase = 'waiting' | 'locking' | 'moving' | 'finished'
type PlaybackMode = 'single' | 'all'

const props = withDefaults(defineProps<Props>(), {
    selectedInstanceId: null,
    activationKey: 0,
})

const { t } = useI18n()

const TARGET_WORLD_SPAN = 170
const MIN_WORLD_SPAN = 48
const STANDARD_TRACK_GAUGE_MM = 1435
const TRACK_CENTERLINE_SPACING_MM = 5000
const TRACK_CENTERLINE_GRID_COUNT = 2
const BALLAST_HEIGHT = 0.14
const RAIL_HEIGHT = 0.08
const RAIL_Y = 0.28
const SLEEPER_Y = 0.19
const SLEEPER_HEIGHT = 0.08
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
const defaultOperationPlanID = 'default'
const trainCarCount = 8
const trainCarLength = 34
const trainCarWidth = 12
const trainCarGap = 4
const trainCarTurnSmoothingDistance = trainCarLength * 0.9
const syntheticRouteGapSeconds = 1.2
const routeLockMinSeconds = 1.2
const routeLockMaxSeconds = 8
const playbackRenderIntervalMs = 33
const ganttSidebarWidth = 168
const ganttMinTimelineWidth = 860
const ganttMaxTimelineWidth = 6400
const ganttTargetPixelsPerSecond = 0.08
const ganttDefaultSubTableCount = 3
const trainCarBaseHeight = 0.78

const canvasWrapperRef = ref<HTMLElement | null>(null)
const canvasRef = ref<HTMLCanvasElement | null>(null)
const layoutData = ref<StationLayoutData>(createEmptyLayout())
const layoutCells = ref<LayoutCell[]>([])
const layoutGridSpacing = ref(20)
const loadingData = ref(false)
const loadErrorMessage = ref('')
const showLabels = ref(true)
const currentStationSchemeId = ref('')
const currentOperationPlanId = ref('')
const selectedTrainId = ref('')
const loadingStationSchemes = ref(false)
const loadingOperationPlans = ref(false)
const loadingStationRoutes = ref(false)
const loadingStationRouteTimes = ref(false)
const loadingTrainOperationPlan = ref(false)
const loadingGanttSubTableSettings = ref(false)
const savingGanttSubTableSettings = ref(false)
const stationSchemeOptions = ref<StationSchemeOption[]>([])
const operationPlanOptions = ref<OperationPlanOption[]>([])
const stationRouteOptions = ref<StationRouteOption[]>([])
const stationRouteTimesByKey = ref<Record<string, StationRouteTimeOption[]>>({})
const trainOperationPlanTrains = ref<TrainOperationPlanTrain[]>([])
const trainOperationPlanMovements = ref<TrainOperationPlanMovement[]>([])
const playheadSeconds = ref(0)
const playbackSpeed = ref(60)
const playbackMode = ref<PlaybackMode>('single')
const isPlaying = ref(false)
const activeRunIndex = ref(-1)
const activeRunIndices = ref<number[]>([])
const activeRunPhase = ref<RunPhase>('waiting')
const activeLockingRunCount = ref(0)
const activeMovingRunCount = ref(0)
const runPhaseByKey = ref<Record<string, RunPhase>>({})
const ganttViewportRef = ref<HTMLElement | null>(null)
const ganttSubTableSequence = ref(ganttDefaultSubTableCount)
const ganttSubTables = ref<GanttSubTable[]>(
    Array.from({ length: ganttDefaultSubTableCount }, (_, index) => createGanttSubTable(index + 1)),
)
const activeGanttSubTableId = ref(ganttSubTables.value[0]?.id || '')
const ganttSubTableDialogVisible = ref(false)
const ganttSubTableDialogMode = ref<'create' | 'edit'>('create')
const ganttSubTableDialogTargetId = ref('')
const ganttSubTableDialogTargetSequence = ref(0)
const ganttSubTableDialogForm = ref<GanttSubTableDialogForm>({
    name: '',
    cellIds: [],
})

let renderer: THREE.WebGLRenderer | null = null
let labelRenderer: CSS2DRenderer | null = null
let labelRendererRoot: HTMLElement | null = null
let scene: THREE.Scene | null = null
let camera: THREE.PerspectiveCamera | null = null
let controls: OrbitControls | null = null
let layoutGroup: THREE.Group | null = null
let trainGroup: THREE.Group | null = null
let resizeObserver: ResizeObserver | null = null
let rafId: number | null = null
let playbackFrameId: number | null = null
let ganttScrollFrameId: number | null = null
let ganttSubTableSaveTimer: ReturnType<typeof window.setTimeout> | null = null
let suppressGanttSubTableSave = false
let ganttSubTableSaveRevision = 0
let lastMapper: LayoutMapper | null = null
let layoutLoadVersion = 0
let stationSchemeLoadVersion = 0
let operationPlanLoadVersion = 0
let stationRouteLoadVersion = 0
let stationRouteTimeLoadVersion = 0
let trainPlanLoadVersion = 0
let ganttSubTableLoadVersion = 0
let lastWrapperWidth = 0
let lastWrapperHeight = 0
let lastPlaybackTimestamp = 0
let lastPlaybackRenderTimestamp = 0
let playbackRuntimeSeconds = 0
const trainCarAngleMemory = new Map<string, number>()
const trainCarObjectMap = new Map<string, TrainCarObjectEntry>()

const selectedInstanceId = computed(() => props.selectedInstanceId || '')
const hasScheme = computed(() => Boolean(selectedInstanceId.value && currentStationSchemeId.value.trim()))
const hasScope = computed(() => Boolean(hasScheme.value && currentOperationPlanId.value.trim()))
const loadingAnyData = computed(() => (
    loadingData.value ||
    loadingStationSchemes.value ||
    loadingOperationPlans.value ||
    loadingStationRoutes.value ||
    loadingStationRouteTimes.value ||
    loadingTrainOperationPlan.value ||
    loadingGanttSubTableSettings.value ||
    savingGanttSubTableSettings.value
))
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
const trainOptions = computed(() => trainOperationPlanTrains.value)
const isAllTrainPlayback = computed(() => playbackMode.value === 'all')
const trainMap = computed(() => {
    const map = new Map<string, TrainOperationPlanTrain>()
    trainOperationPlanTrains.value.forEach((train) => map.set(train.id, train))
    return map
})
const selectedTrain = computed(() => trainOptions.value.find((train) => train.id === selectedTrainId.value) || null)
const stationRouteMap = computed(() => {
    const map = new Map<string, StationRouteOption>()
    stationRouteOptions.value.forEach((route) => map.set(route.id, route))
    return map
})
const layoutNodeMap = computed(() => {
    const map = new Map<string, NodePoint>()
    layoutData.value.nodes.forEach((node) => map.set(node.id, node))
    return map
})
const layoutTrackMap = computed(() => {
    const map = new Map<string, Track>()
    layoutData.value.tracks.forEach((track) => map.set(track.id, track))
    return map
})
const selectedTrainMovements = computed(() => {
    const trainID = selectedTrainId.value
    if (!trainID) return []
    return trainOperationPlanMovements.value
        .filter((movement) => movement.trainID === trainID)
        .sort(compareMovements)
})
const routeRuns = computed<RouteRun[]>(() => buildRouteRuns())
const canPlayback = computed(() => routeRuns.value.length > 0 && simulationDurationSeconds.value > 0)
const simulationDurationSeconds = computed(() => (
    routeRuns.value.reduce((maxSeconds, run) => Math.max(maxSeconds, run.endSeconds), 0)
))
const playbackSliderMax = computed(() => Math.max(1, Number(simulationDurationSeconds.value.toFixed(1))))
const usesPlanTime = computed(() => routeRuns.value.some((run) => run.usesPlanTime))
const timelineOriginSeconds = computed(() => {
    const timedRuns = routeRuns.value.filter((run) => run.usesPlanTime)
    if (timedRuns.length === 0) return 0
    return Math.min(...timedRuns.map((run) => run.absoluteStartSeconds))
})
const ganttTimelineWidth = computed(() => {
    const duration = Math.max(1, simulationDurationSeconds.value)
    return Math.round(Math.max(
        ganttMinTimelineWidth,
        Math.min(ganttMaxTimelineWidth, duration * ganttTargetPixelsPerSecond),
    ))
})
const ganttTimeScale = computed(() => ganttTimelineWidth.value / Math.max(1, simulationDurationSeconds.value))
const ganttPlayheadLeft = computed(() => secondsToGanttLeft(playheadSeconds.value))
const ganttTimelineStyle = computed(() => ({
    width: `${ganttTimelineWidth.value}px`,
}))
const ganttContentStyle = computed(() => ({
    minWidth: `${ganttSidebarWidth + ganttTimelineWidth.value}px`,
    '--layout3d-gantt-sidebar-width': `${ganttSidebarWidth}px`,
}))
const ganttPlayheadStyle = computed(() => ({
    left: `${ganttPlayheadLeft.value}px`,
}))
const ganttTicks = computed<GanttTick[]>(() => buildGanttTicks())
const ganttAvailableCells = computed<LayoutCell[]>(() => getGanttAvailableCells())
const activeGanttSubTable = computed(() => (
    ganttSubTables.value.find((subTable) => subTable.id === activeGanttSubTableId.value) ||
    ganttSubTables.value[0] ||
    null
))
const activeGanttSubTableCellIds = computed(() => normalizeGanttSubTableCellIds(activeGanttSubTable.value?.cellIds || []))
const activeGanttSubTableCells = computed<LayoutCell[]>(() => {
    const selectedCellIds = new Set(activeGanttSubTableCellIds.value)
    return ganttAvailableCells.value.filter((cell) => selectedCellIds.has(cell.id))
})
const ganttLanes = computed<GanttLane[]>(() => buildGanttLanes())
const ganttSummaryText = computed(() => {
    if (routeRuns.value.length === 0) return String(t('stationLayout3d.gantt.noPlayableWork'))
    const blockCount = ganttLanes.value.reduce((count, lane) => count + lane.blocks.length, 0)
    return String(t('stationLayout3d.gantt.summary', {
        laneCount: ganttLanes.value.length,
        blockCount,
    }))
})
const activeGanttSubTableSummaryText = computed(() => String(t('stationLayout3d.gantt.subTableSummary', {
    selected: activeGanttSubTableCells.value.length,
    total: ganttAvailableCells.value.length,
})))
const ganttSubTableDialogTitle = computed(() => (
    ganttSubTableDialogMode.value === 'create'
        ? t('stationLayout3d.dialogs.createGanttSubTable')
        : t('stationLayout3d.dialogs.editGanttSubTable')
))
const ganttEmptyText = computed(() => {
    if (routeRuns.value.length === 0) return movementEmptyText.value
    if (ganttAvailableCells.value.length > 0 && activeGanttSubTableCells.value.length === 0) {
        return t('stationLayout3d.gantt.emptySubTable')
    }
    return t('stationLayout3d.gantt.emptyOccupation')
})
const activeRun = computed(() => {
    const runs = routeRuns.value
    if (runs.length === 0) return null
    return runs[activeRunIndex.value] || runs[0] || null
})
const activePhase = computed(() => activeRunPhase.value)
const activeRouteProgress = computed(() => getActiveRouteProgress(activeRun.value, playheadSeconds.value))
const simulationTrainCars = computed<SimulationTrainCar[]>(() => buildSimulationTrainCars())
const finishedRunCount = computed(() => routeRuns.value.filter((run) => runPhaseByKey.value[run.key] === 'finished').length)
const playbackStatusText = computed(() => {
    if (!canPlayback.value) return t('stationLayout3d.status.notReady')
    if (isPlaying.value) return t('stationLayout3d.status.playing')
    if (playheadSeconds.value >= simulationDurationSeconds.value) return t('stationLayout3d.status.finished')
    return t('stationLayout3d.status.paused')
})
const playbackStatusTagType = computed<'success' | 'warning' | 'info'>(() => {
    if (isPlaying.value) return 'success'
    if (!canPlayback.value) return 'info'
    return 'warning'
})
const playbackClockText = computed(() => (
    usesPlanTime.value
        ? formatClockSeconds(timelineOriginSeconds.value + playheadSeconds.value)
        : formatDurationSeconds(playheadSeconds.value)
))
const playbackSummaryText = computed(() => {
    if (isAllTrainPlayback.value) {
        return String(t('stationLayout3d.playback.allSummary', {
            trainCount: trainOptions.value.length,
            routeCount: routeRuns.value.length,
        }))
    }
    const train = selectedTrain.value
    if (!train) return t('stationLayout3d.playback.selectTrain')
    return String(t('stationLayout3d.playback.singleSummary', {
        train: formatTrainLabel(train),
        movementCount: selectedTrainMovements.value.length,
    }))
})
const activePhaseText = computed(() => {
    if (isAllTrainPlayback.value) {
        const locking = activeLockingRunCount.value
        const moving = activeMovingRunCount.value
        if (locking + moving <= 0) return t('stationLayout3d.phase.waiting')
        return String(t('stationLayout3d.phase.allActive', { locking, moving }))
    }
    if (!activeRun.value) return t('stationLayout3d.phase.selecting')
    if (activePhase.value === 'locking') return t('stationLayout3d.phase.locking')
    if (activePhase.value === 'moving') {
        return String(t('stationLayout3d.phase.movingWithProgress', {
            progress: Math.round(activeRouteProgress.value * 100),
        }))
    }
    if (activePhase.value === 'finished') {
        return String(t('stationLayout3d.phase.finishedWithCount', {
            finished: finishedRunCount.value,
            total: routeRuns.value.length,
        }))
    }
    return t('stationLayout3d.phase.waiting')
})
const movementEmptyText = computed(() => {
    if (isAllTrainPlayback.value) return t('stationLayout3d.gantt.emptyAllPlan')
    if (!selectedTrain.value) return t('stationLayout3d.playback.selectTrain')
    if (selectedTrainMovements.value.length === 0) return t('stationLayout3d.gantt.emptyTrainPlan')
    return t('stationLayout3d.gantt.emptyRoute')
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

function readArray(source: unknown, ...keys: string[]) {
    const record = readRecord(source)
    for (const key of keys) {
        const value = record[key]
        if (Array.isArray(value)) return value
    }
    return []
}

function readRecord(value: unknown): Record<string, unknown> {
    return value && typeof value === 'object' && !Array.isArray(value) ? value as Record<string, unknown> : {}
}

function readOptionalInteger(source: unknown, ...keys: string[]): number | null {
    const record = readRecord(source)
    for (const key of keys) {
        const value = record[key]
        if (value === undefined || value === null || value === '') continue
        const parsed = Number(value)
        if (Number.isFinite(parsed)) return Math.trunc(parsed)
    }
    return null
}

function readBoolean(source: unknown, defaultValue: boolean, ...keys: string[]) {
    const record = readRecord(source)
    for (const key of keys) {
        const value = record[key]
        if (value === undefined || value === null || value === '') continue
        if (typeof value === 'boolean') return value
        if (typeof value === 'number') return value === 1
        const text = String(value).trim().toLowerCase()
        if (['1', 'true', 'yes', 'y'].includes(text)) return true
        if (['0', 'false', 'no', 'n'].includes(text)) return false
    }
    return defaultValue
}

function normalizeStationSchemeOption(item: any): StationSchemeOption | null {
    const id = readString(item, 'id', 'ID').trim()
    if (!id) return null

    const name = readString(item, 'name', 'Name').trim() || id
    return { id, name }
}

function normalizeOperationPlanOption(item: unknown): OperationPlanOption | null {
    const operationPlanID = readString(item, 'operationPlanID', 'OperationPlanID').trim()
    if (!operationPlanID) return null
    return {
        instanceID: readString(item, 'instanceID', 'InstanceID').trim(),
        stationSchemeID: readString(item, 'stationSchemeID', 'StationSchemeID').trim(),
        operationPlanID,
        name: readString(item, 'name', 'Name').trim() || operationPlanID,
        description: readString(item, 'description', 'Description').trim(),
        sortOrder: readOptionalInteger(item, 'sortOrder', 'SortOrder'),
    }
}

function normalizeStationRouteOption(item: unknown): StationRouteOption | null {
    const id = readString(item, 'id', 'ID').trim()
    if (!id) return null
    const description = readString(item, 'description', 'Description').trim()
    return {
        id,
        name: description || id,
        type: readString(item, 'type', 'Type').trim(),
        nodeList: readString(item, 'nodeList', 'NodeList').trim(),
        linkList: readString(item, 'linkList', 'LinkList').trim(),
        startNodeID: readString(item, 'startNodeID', 'StartNodeID').trim(),
        endNodeID: readString(item, 'endNodeID', 'EndNodeID').trim(),
    }
}

function normalizeStationRouteTimeOption(item: unknown): StationRouteTimeOption | null {
    const cellID = readString(item, 'cellID', 'CellID').trim()
    if (!cellID) return null
    return {
        routeID: readString(item, 'routeID', 'RouteID').trim(),
        trainTypeID: readString(item, 'trainTypeID', 'TrainTypeID').trim(),
        cellID,
        startOccupationShift: readOptionalInteger(item, 'startOccupationShift', 'StartOccupationShift'),
        endOccupationShift: readOptionalInteger(item, 'endOccupationShift', 'EndOccupationShift'),
        isInterruptCell: readBoolean(item, false, 'isInterruptCell', 'IsInterruptCell'),
    }
}

function normalizeTrain(item: unknown): TrainOperationPlanTrain | null {
    const id = readString(item, 'id', 'ID').trim()
    if (!id) return null
    return {
        id,
        trainTemplateID: readString(item, 'trainTemplateID', 'TrainTemplateID').trim(),
        trainNumber: readString(item, 'trainNumber', 'TrainNumber').trim(),
        name: readString(item, 'name', 'Name').trim(),
        trainType: readString(item, 'trainType', 'TrainType').trim(),
        isFixedOperation: readBoolean(item, false, 'isFixedOperation', 'IsFixedOperation'),
    }
}

function normalizeMovement(item: unknown): TrainOperationPlanMovement | null {
    const trainID = readString(item, 'trainID', 'TrainID').trim()
    const movementID = readString(item, 'movementID', 'MovementID').trim()
    if (!trainID || !movementID) return null
    return {
        trainID,
        trainTemplateID: readString(item, 'trainTemplateID', 'TrainTemplateID').trim(),
        movementID,
        name: readString(item, 'name', 'Name').trim(),
        routeIDList: readString(item, 'routeIDList', 'RouteIDList').trim(),
        minDuration: readOptionalInteger(item, 'minDuration', 'MinDuration'),
        earliestStartTime: readString(item, 'earliestStartTime', 'EarliestStartTime').trim(),
        latestEndTime: readString(item, 'latestEndTime', 'LatestEndTime').trim(),
        route: readString(item, 'route', 'Route').trim(),
        tag: readString(item, 'tag', 'Tag').trim(),
        sortOrder: readOptionalInteger(item, 'sortOrder', 'SortOrder'),
    }
}

function normalizeTrainOperationPlanResponse(data: unknown) {
    const record = readRecord(data)
    const rawTrains = Array.isArray(record.trains)
        ? record.trains
        : Array.isArray(record.Trains)
            ? record.Trains
            : []
    const rawMovements = Array.isArray(record.movements)
        ? record.movements
        : Array.isArray(record.Movements)
            ? record.Movements
            : []
    const previousTrainId = selectedTrainId.value
    trainOperationPlanTrains.value = rawTrains
        .map(normalizeTrain)
        .filter((item): item is TrainOperationPlanTrain => item !== null)
    trainOperationPlanMovements.value = rawMovements
        .map(normalizeMovement)
        .filter((item): item is TrainOperationPlanMovement => item !== null)
    selectedTrainId.value = trainOperationPlanTrains.value.some((train) => train.id === previousTrainId)
        ? previousTrainId
        : trainOperationPlanTrains.value[0]?.id || ''
}

function getLayoutGridSpacing(data: unknown) {
    const metadata = readRecord(readRecord(data).metadata)
    const gridSettings = readRecord(metadata.gridSettings)
    const parsed = Number(gridSettings.spacing ?? gridSettings.Spacing ?? 20)
    return Number.isFinite(parsed) && parsed > 0 ? parsed : 20
}

function getLayoutCells(data: unknown): LayoutCell[] {
    const cells = Array.isArray(readRecord(data).cells) ? readRecord(data).cells as unknown[] : []
    return cells
        .map((cell) => ({
            id: readString(cell, 'id', 'ID').trim(),
            name: readString(cell, 'name', 'Name').trim(),
            linkIDList: readString(cell, 'linkIDList', 'LinkIDList').trim(),
        }))
        .map((cell) => ({ ...cell, name: cell.name || cell.id }))
        .filter((cell) => cell.id || cell.name || cell.linkIDList)
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
    return option.name && option.name !== option.id ? `${option.name} (${option.id})` : option.id
}

function formatOperationPlanLabel(option: OperationPlanOption) {
    return option.name && option.name !== option.operationPlanID
        ? `${option.name} (${option.operationPlanID})`
        : option.operationPlanID
}

function formatTrainLabel(train: TrainOperationPlanTrain) {
    const number = train.trainNumber || train.id
    const name = train.name ? ` ${train.name}` : ''
    return `${number}${name}`
}

async function loadStationSchemes(options: { includeCurrent?: boolean } = {}) {
    const includeCurrent = options.includeCurrent !== false
    const instanceID = selectedInstanceId.value
    if (!instanceID) {
        stationSchemeLoadVersion++
        currentStationSchemeId.value = ''
        stationSchemeOptions.value = []
        clearOperationPlans()
        clearStationRoutes()
        clearLayout()
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
        const previousId = currentStationSchemeId.value
        setStationSchemeOptions(options, includeCurrent)
        currentStationSchemeId.value = stationSchemeOptions.value.some((item) => item.id === previousId)
            ? previousId
            : stationSchemeOptions.value[0]?.id || ''
        await loadOperationPlans()
        await refresh3DData()
        return options
    } catch (error) {
        if (loadVersion !== stationSchemeLoadVersion || instanceID !== selectedInstanceId.value) return []

        console.error('Failed to load station schemes:', error)
        stationSchemeOptions.value = []
        currentStationSchemeId.value = ''
        clearOperationPlans()
        clearStationRoutes()
        clearLayout()
        ElMessage.error(t('stationLayout.messages.loadSchemesFailed'))
        return []
    } finally {
        if (loadVersion === stationSchemeLoadVersion && instanceID === selectedInstanceId.value) {
            loadingStationSchemes.value = false
        }
    }
}

async function handleStationSchemeChange() {
    stopPlaybackForReload()
    currentOperationPlanId.value = ''
    clearGanttSubTableState()
    clearStationRoutes()
    clearTrainPlan()
    await loadOperationPlans()
    await refresh3DData()
}

async function handleOperationPlanChange() {
    stopPlaybackForReload()
    clearTrainPlan()
    clearGanttSubTableState()
    await loadTrainOperationPlan()
    await Promise.all([loadStationRouteTimes(), loadGanttSubTableSettings()])
}

function handleTrainChange() {
    resetPlayback()
}

function handlePlaybackModeChange() {
    resetPlayback()
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

function compareMovements(left: TrainOperationPlanMovement, right: TrainOperationPlanMovement) {
    const leftOrder = Number(left.sortOrder)
    const rightOrder = Number(right.sortOrder)
    if (Number.isFinite(leftOrder) && Number.isFinite(rightOrder) && leftOrder !== rightOrder) {
        return leftOrder - rightOrder
    }
    const leftStart = parseOperationPlanTime(left.earliestStartTime)
    const rightStart = parseOperationPlanTime(right.earliestStartTime)
    if (leftStart !== null && rightStart !== null && leftStart !== rightStart) return leftStart - rightStart
    return left.movementID.localeCompare(right.movementID, undefined, { numeric: true, sensitivity: 'base' })
}

function parseOperationPlanTime(value: string) {
    const text = String(value || '').trim()
    if (!text) return null
    let dayOffset = 0
    let timeText = text
    const dayMatch = text.match(/^D\+(\d+)\s+(.+)$/i)
    if (dayMatch) {
        dayOffset = Number(dayMatch[1])
        timeText = (dayMatch[2] || '').trim()
    }
    const parts = timeText.split(':')
    if (parts.length < 2) return null
    const hours = Number(parts[0])
    const minutes = Number(parts[1])
    const seconds = parts.length > 2 ? Number(parts[2]) : 0
    if (
        !Number.isFinite(hours) ||
        !Number.isFinite(minutes) ||
        !Number.isFinite(seconds) ||
        hours < 0 ||
        minutes < 0 ||
        minutes >= 60 ||
        seconds < 0 ||
        seconds >= 60
    ) {
        return null
    }
    return dayOffset * 24 * 60 + hours * 60 + minutes + seconds / 60
}

function formatClockSeconds(totalSeconds: number) {
    const normalizedSeconds = Math.max(0, Math.round(totalSeconds))
    const days = Math.floor(normalizedSeconds / 86400)
    const secondsInDay = normalizedSeconds % 86400
    const hours = Math.floor(secondsInDay / 3600)
    const minutes = Math.floor((secondsInDay % 3600) / 60)
    const seconds = secondsInDay % 60
    const timeText = `${String(hours).padStart(2, '0')}:${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}`
    return days > 0 ? `D+${days} ${timeText}` : timeText
}

function formatDurationSeconds(totalSeconds: number) {
    const normalizedSeconds = Math.max(0, Math.round(totalSeconds))
    const minutes = Math.floor(normalizedSeconds / 60)
    const seconds = normalizedSeconds % 60
    return `${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}`
}

function getRouteDisplayName(routeID: string) {
    return stationRouteMap.value.get(routeID)?.name || routeID || '-'
}

function getStationRouteHighlightColor(type: string) {
    const normalized = type.trim().toLowerCase()
    if (normalized.includes('arrival') || normalized.includes('接车')) return '#22c55e'
    if (normalized.includes('departure') || normalized.includes('发车')) return '#38bdf8'
    if (normalized.includes('locomotive') || normalized.includes('机车')) return '#f59e0b'
    if (normalized.includes('shunting') || normalized.includes('调车')) return '#a855f7'
    return '#ffd600'
}

function getTrainColor(trainID: string) {
    const colors = ['#2563eb', '#dc2626', '#059669', '#7c3aed', '#ea580c', '#0891b2']
    const hash = trainID.split('').reduce((sum, char) => sum + char.charCodeAt(0), 0)
    return colors[hash % colors.length] || colors[0] || '#2563eb'
}

function getPlaybackRunSources(): RouteRunSource[] {
    if (!isAllTrainPlayback.value) {
        const train = selectedTrain.value
        if (!train) return []
        return selectedTrainMovements.value.map((movement, sourceIndex) => ({ train, movement, sourceIndex }))
    }

    return trainOperationPlanMovements.value
        .map((movement, sourceIndex) => {
            const train = trainMap.value.get(movement.trainID)
            return train ? { train, movement, sourceIndex } : null
        })
        .filter((item): item is RouteRunSource => item !== null)
        .sort(compareRouteRunSources)
}

function compareRouteRunSources(left: RouteRunSource, right: RouteRunSource) {
    const leftStart = parseOperationPlanTime(left.movement.earliestStartTime)
    const rightStart = parseOperationPlanTime(right.movement.earliestStartTime)
    if (leftStart !== null && rightStart !== null && leftStart !== rightStart) return leftStart - rightStart
    if (leftStart !== null && rightStart === null) return -1
    if (leftStart === null && rightStart !== null) return 1
    const trainCompare = formatTrainLabel(left.train).localeCompare(
        formatTrainLabel(right.train),
        undefined,
        { numeric: true, sensitivity: 'base' },
    )
    if (trainCompare !== 0) return trainCompare
    const movementCompare = compareMovements(left.movement, right.movement)
    return movementCompare !== 0 ? movementCompare : left.sourceIndex - right.sourceIndex
}

function buildRouteRuns(): RouteRun[] {
    const rawRuns = getPlaybackRunSources()
        .map((source) => {
            const { movement, train, sourceIndex } = source
            const routeID = getMovementRouteID(movement)
            const route = stationRouteMap.value.get(routeID)
            if (!route) return null
            const geometry = buildRouteGeometry(route)
            if (geometry.path.totalLength <= 0 || geometry.path.segments.length === 0) return null
            const startMinutes = parseOperationPlanTime(movement.earliestStartTime)
            const endMinutes = parseOperationPlanTime(movement.latestEndTime)
            return { movement, train, route, geometry, startMinutes, endMinutes, sourceIndex }
        })
        .filter((item): item is {
            movement: TrainOperationPlanMovement
            train: TrainOperationPlanTrain
            route: StationRouteOption
            geometry: RouteGeometry
            startMinutes: number | null
            endMinutes: number | null
            sourceIndex: number
        } => item !== null)

    const hasValidPlanTime = (item: (typeof rawRuns)[number]) => (
        item.startMinutes !== null &&
        item.endMinutes !== null
    )
    const playableRuns = isAllTrainPlayback.value
        ? rawRuns.filter(hasValidPlanTime)
        : rawRuns
    const timedRuns = playableRuns.filter(hasValidPlanTime)
    const usesTimedPlan = isAllTrainPlayback.value
        ? playableRuns.length > 0
        : playableRuns.length > 0 && timedRuns.length === playableRuns.length
    const originSeconds = usesTimedPlan
        ? Math.min(...timedRuns.map((item) => getRouteOccupationWindowSeconds(item).startSeconds))
        : 0
    let syntheticCursor = 0

    return playableRuns.map((item, index) => {
        const fallbackDuration = getFallbackRouteDurationSeconds(item.movement, item.geometry.path.totalLength)
        let startSeconds = syntheticCursor
        let endSeconds = syntheticCursor + fallbackDuration
        let absoluteStartSeconds = startSeconds
        let absoluteEndSeconds = endSeconds
        let runUsesPlanTime = false

        if (usesTimedPlan && hasValidPlanTime(item)) {
            const occupationWindow = getRouteOccupationWindowSeconds(item)
            absoluteStartSeconds = occupationWindow.startSeconds
            absoluteEndSeconds = Math.max(occupationWindow.endSeconds, absoluteStartSeconds + fallbackDuration)
            startSeconds = absoluteStartSeconds - originSeconds
            endSeconds = absoluteEndSeconds - originSeconds
            runUsesPlanTime = true
        } else {
            syntheticCursor = endSeconds + syntheticRouteGapSeconds
        }

        const duration = Math.max(0.1, endSeconds - startSeconds)
        const lockSeconds = Math.min(routeLockMaxSeconds, Math.max(routeLockMinSeconds, duration * 0.16))
        return {
            key: `${item.train.id}-${item.movement.movementID}-${item.route.id}-${index}`,
            train: item.train,
            movement: item.movement,
            route: item.route,
            path: item.geometry.path,
            nodeIds: item.geometry.nodeIds,
            linkIds: item.geometry.linkIds,
            startSeconds,
            endSeconds,
            lockSeconds: Math.min(lockSeconds, duration * 0.65),
            usesPlanTime: runUsesPlanTime,
            absoluteStartSeconds,
            absoluteEndSeconds,
            color: getStationRouteHighlightColor(item.route.type),
        }
    }).sort((left, right) => (
        left.startSeconds - right.startSeconds ||
        left.endSeconds - right.endSeconds ||
        formatTrainLabel(left.train).localeCompare(formatTrainLabel(right.train), undefined, { numeric: true, sensitivity: 'base' })
    ))
}

function getRouteOccupationWindowSeconds(item: {
    movement: TrainOperationPlanMovement
    train: TrainOperationPlanTrain
    route: StationRouteOption
    startMinutes: number | null
    endMinutes: number | null
}) {
    const baseStartSeconds = Number(item.startMinutes || 0) * 60
    const baseEndSeconds = Number(item.endMinutes || item.startMinutes || 0) * 60
    const routeTimeRows = getStationRouteTimes(item.route.id, item.train.trainType)
    if (routeTimeRows.length === 0) {
        return {
            startSeconds: baseStartSeconds,
            endSeconds: baseEndSeconds,
        }
    }

    let startSeconds = baseStartSeconds
    let endSeconds = baseEndSeconds
    routeTimeRows.forEach((time) => {
        const cellStartSeconds = baseStartSeconds + Number(time.startOccupationShift ?? 0)
        const rawCellEndSeconds = baseEndSeconds + Number(time.endOccupationShift ?? 0)
        startSeconds = Math.min(startSeconds, cellStartSeconds)
        endSeconds = Math.max(endSeconds, Math.max(cellStartSeconds, rawCellEndSeconds))
    })
    return { startSeconds, endSeconds }
}

function getMovementRouteID(movement: TrainOperationPlanMovement) {
    const selectedRouteID = movement.route.trim()
    if (selectedRouteID) return selectedRouteID
    return parseRouteReferenceList(movement.routeIDList)[0] || ''
}

function getStationRouteTimeKey(routeID: string, trainTypeID: string) {
    return `${routeID.trim()}::${trainTypeID.trim()}`
}

function getStationRouteTimes(routeID: string, trainTypeID: string) {
    const specificKey = getStationRouteTimeKey(routeID, trainTypeID)
    const defaultKey = getStationRouteTimeKey(routeID, '')
    const specificRows = stationRouteTimesByKey.value[specificKey] || []
    if (specificRows.length > 0) return specificRows
    return stationRouteTimesByKey.value[defaultKey] || []
}

function getFallbackRouteDurationSeconds(movement: TrainOperationPlanMovement, pathLength: number) {
    const minDuration = Number(movement.minDuration)
    if (Number.isFinite(minDuration) && minDuration > 0) return Math.max(4, minDuration)
    return Math.max(8, Math.min(36, pathLength / 55))
}

function buildRouteGeometry(route: StationRouteOption): RouteGeometry {
    const linkIds = parseRouteReferenceList(route.linkList)
    const nodeIds = normalizeUniqueStrings([
        ...parseRouteReferenceList(route.nodeList),
        route.startNodeID,
        route.endNodeID,
    ])
    let points = parseRouteReferenceList(route.nodeList)
        .map((nodeId) => pointFromNodeId(nodeId))
        .filter((point): point is RoutePoint => point !== null)

    if (points.length < 2) {
        points = buildPointsFromLinks(route, linkIds)
    }
    if (points.length < 2) {
        points = [pointFromNodeId(route.startNodeID), pointFromNodeId(route.endNodeID)]
            .filter((point): point is RoutePoint => point !== null)
    }

    return {
        path: buildPolylinePath(points),
        nodeIds,
        linkIds,
    }
}

function pointFromNodeId(nodeId: string): RoutePoint | null {
    const id = String(nodeId || '').trim()
    if (!id) return null
    const node = layoutNodeMap.value.get(id)
    if (!node) return null
    return { x: node.x, y: node.y, nodeId: id }
}

function buildPointsFromLinks(route: StationRouteOption, linkIds: string[]): RoutePoint[] {
    const points: RoutePoint[] = []
    let currentNodeId = route.startNodeID.trim()
    const startPoint = pointFromNodeId(currentNodeId)
    if (startPoint) points.push(startPoint)

    linkIds.forEach((linkId) => {
        const track = layoutTrackMap.value.get(linkId)
        if (!track) return
        const endpoints = getTrackEndpoints(track)
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

function getTrackEndpoints(track: Track): [RoutePoint, RoutePoint] | null {
    const fromNode = pointFromNodeId(track.fromNodeID)
    const toNode = pointFromNodeId(track.toNodeID)
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

function getPointOnPath(path: PolylinePath, distance: number) {
    if (path.segments.length === 0) {
        const first = path.points[0] || { x: 0, y: 0 }
        return { x: first.x, y: first.y, angle: 0, distance: 0 }
    }
    const clampedDistance = Math.max(0, Math.min(path.totalLength, distance))
    const segment = path.segments.find((item) => clampedDistance <= item.startDistance + item.length) ||
        path.segments[path.segments.length - 1]
    if (!segment) {
        const first = path.points[0] || { x: 0, y: 0 }
        return { x: first.x, y: first.y, angle: 0, distance: clampedDistance }
    }
    const localDistance = Math.max(0, Math.min(segment.length, clampedDistance - segment.startDistance))
    const ratio = segment.length > 0 ? localDistance / segment.length : 0
    return {
        x: segment.from.x + (segment.to.x - segment.from.x) * ratio,
        y: segment.from.y + (segment.to.y - segment.from.y) * ratio,
        angle: segment.angle,
        distance: clampedDistance,
    }
}

function getPositionOnPath(
    path: PolylinePath,
    distance: number,
    options: { smoothAngle?: boolean; smoothingDistance?: number } = {},
) {
    const point = getPointOnPath(path, distance)
    const angle = options.smoothAngle
        ? getSmoothedPathAngle(path, point.distance, point.angle, options.smoothingDistance ?? trainCarTurnSmoothingDistance)
        : point.angle
    return { x: point.x, y: point.y, angle: normalizePathAngle(angle) }
}

function getSmoothedPathAngle(
    path: PolylinePath,
    distance: number,
    fallbackAngle: number,
    smoothingDistance: number,
) {
    if (path.segments.length === 0 || path.totalLength <= 0) return fallbackAngle
    const sampleDistance = Math.max(1, Math.min(path.totalLength / 2, Number(smoothingDistance || 0)))
    if (!Number.isFinite(sampleDistance) || sampleDistance <= 0) return fallbackAngle

    const beforeDistance = Math.max(0, distance - sampleDistance)
    const afterDistance = Math.min(path.totalLength, distance + sampleDistance)
    if (afterDistance - beforeDistance < 0.001) return fallbackAngle

    const before = getPointOnPath(path, beforeDistance)
    const after = getPointOnPath(path, afterDistance)
    const deltaX = after.x - before.x
    const deltaY = after.y - before.y
    if (Math.hypot(deltaX, deltaY) < 0.001) return fallbackAngle
    return normalizePathAngle(Math.atan2(deltaY, deltaX) * 180 / Math.PI)
}

function buildSimulationTrainCars(): SimulationTrainCar[] {
    const currentSeconds = playheadSeconds.value
    const visibleRuns = isAllTrainPlayback.value
        ? activeRunIndices.value
            .map((index) => routeRuns.value[index] || null)
            .filter((run): run is RouteRun => run !== null)
        : activeRun.value
            ? [activeRun.value]
            : []
    const cars = visibleRuns.flatMap((run) => buildSimulationTrainCarsForRun(run, currentSeconds))
    pruneTrainCarAngleMemory(cars)
    return cars
}

function buildSimulationTrainCarsForRun(run: RouteRun, currentSeconds: number): SimulationTrainCar[] {
    const progress = getActiveRouteProgress(run, currentSeconds)
    const headDistance = run.path.totalLength * progress
    const fill = getTrainColor(run.train.id)
    const cars: SimulationTrainCar[] = []

    for (let index = 0; index < trainCarCount; index++) {
        const offset = index * (trainCarLength + trainCarGap)
        const key = `${run.key}-${index}`
        const position = getPositionOnPath(run.path, headDistance - offset, {
            smoothAngle: true,
            smoothingDistance: trainCarTurnSmoothingDistance,
        })
        cars.push({
            key,
            x: position.x,
            y: position.y,
            angle: getContinuousTrainCarAngle(key, position.angle),
            length: trainCarLength,
            width: trainCarWidth,
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

function secondsToGanttLeft(seconds: number) {
    const duration = Math.max(1, simulationDurationSeconds.value)
    return Math.max(0, Math.min(duration, Number(seconds || 0))) * ganttTimeScale.value
}

function buildGanttTicks(): GanttTick[] {
    const duration = simulationDurationSeconds.value
    if (duration <= 0) return []

    const targetTickCount = Math.max(4, Math.min(12, Math.floor(ganttTimelineWidth.value / 120)))
    const stepSeconds = getNiceGanttTickStepSeconds(duration / targetTickCount)
    const ticks: GanttTick[] = []
    const seen = new Set<number>()
    const originSeconds = usesPlanTime.value ? timelineOriginSeconds.value : 0
    const firstAlignedSeconds = usesPlanTime.value
        ? Math.max(0, Math.ceil(originSeconds / stepSeconds) * stepSeconds - originSeconds)
        : 0

    const addTick = (seconds: number, major: boolean) => {
        const normalizedSeconds = Math.max(0, Math.min(duration, seconds))
        const tickKey = Math.round(normalizedSeconds * 10)
        if (seen.has(tickKey)) return
        seen.add(tickKey)
        ticks.push({
            key: String(tickKey),
            seconds: normalizedSeconds,
            left: secondsToGanttLeft(normalizedSeconds),
            label: formatGanttTickLabel(normalizedSeconds),
            major,
        })
    }

    addTick(0, true)
    let tickIndex = 0
    for (let seconds = firstAlignedSeconds; seconds <= duration; seconds += stepSeconds) {
        addTick(seconds, tickIndex % 2 === 0)
        tickIndex++
    }
    addTick(duration, true)
    return ticks.sort((left, right) => left.seconds - right.seconds)
}

function getNiceGanttTickStepSeconds(rawStepSeconds: number) {
    const steps = [5, 10, 15, 30, 60, 120, 300, 600, 900, 1800, 3600, 7200, 10800, 14400, 21600, 43200]
    return steps.find((step) => step >= rawStepSeconds) || steps[steps.length - 1] || 3600
}

function formatGanttTickLabel(seconds: number) {
    return usesPlanTime.value
        ? formatClockSeconds(timelineOriginSeconds.value + seconds)
        : formatDurationSeconds(seconds)
}

function buildGanttLanes(): GanttLane[] {
    if (routeRuns.value.length === 0) return []
    const lanesByCell = buildGanttBaseLanes()

    routeRuns.value.forEach((run) => {
        const trainLabel = formatTrainLabel(run.train)
        const routeName = getRouteDisplayName(run.route.id)
        buildGanttBlocksForRun(run).forEach(({ cellID, block }) => {
            const lane = lanesByCell.get(cellID)
            if (!lane) return
            lane.blocks.push({
                ...block,
                label: trainLabel,
                title: `${lane.label} · ${trainLabel} · ${routeName} · ${formatGanttTickLabel(block.startSeconds)} - ${formatGanttTickLabel(block.endSeconds)}`,
            })
        })
    })

    return Array.from(lanesByCell.values())
        .map((lane) => ({
            ...lane,
            blocks: lane.blocks.sort((left, right) => left.left - right.left),
        }))
        .sort((left, right) => (
            left.sortIndex - right.sortIndex ||
            left.label.localeCompare(right.label, undefined, { numeric: true, sensitivity: 'base' })
        ))
}

function buildGanttBaseLanes() {
    const lanesByCell = new Map<string, GanttLane>()
    activeGanttSubTableCells.value.forEach((cell, index) => {
        const cellID = String(cell.id || cell.name || '').trim()
        if (!cellID || lanesByCell.has(cellID)) return
        lanesByCell.set(cellID, {
            key: cellID,
            label: cell.name || cellID,
            sortIndex: index,
            blocks: [],
        })
    })
    return lanesByCell
}

function getGanttAvailableCells() {
    return layoutCells.value.length > 0
        ? layoutCells.value
        : getFallbackGanttCellsFromRouteTimes()
}

function getFallbackGanttCellsFromRouteTimes() {
    const cellsById = new Map<string, LayoutCell>()
    Object.values(stationRouteTimesByKey.value).flat().forEach((time) => {
        const cellID = time.cellID.trim()
        if (!cellID || cellsById.has(cellID)) return
        cellsById.set(cellID, {
            id: cellID,
            name: cellID,
            linkIDList: '',
        })
    })
    return Array.from(cellsById.values())
}

function buildGanttBlocksForRun(run: RouteRun) {
    const routeTimeRows = getStationRouteTimes(run.route.id, run.train.trainType)
    if (routeTimeRows.length > 0) {
        return routeTimeRows
            .map((time, timeIndex) => buildTimedGanttBlock(run, time, timeIndex))
            .filter((item): item is { cellID: string; block: GanttBlock } => item !== null)
    }

    return getRouteLayoutCellIds(run).map((cellID, cellIndex) => {
        const startSeconds = run.startSeconds
        const endSeconds = run.endSeconds
        return {
            cellID,
            block: createGanttBlock(run, cellID, cellIndex, startSeconds, endSeconds),
        }
    })
}

function buildTimedGanttBlock(run: RouteRun, time: StationRouteTimeOption, timeIndex: number) {
    const cellID = time.cellID.trim()
    if (!cellID) return null
    const baseWindow = getRunGanttBaseWindow(run)
    const startSeconds = baseWindow.startSeconds + Number(time.startOccupationShift ?? 0)
    const endSeconds = baseWindow.endSeconds + Number(time.endOccupationShift ?? 0)
    return {
        cellID,
        block: createGanttBlock(run, cellID, timeIndex, startSeconds, endSeconds),
    }
}

function getRunGanttBaseWindow(run: RouteRun) {
    if (run.usesPlanTime) {
        const startMinutes = parseOperationPlanTime(run.movement.earliestStartTime)
        const endMinutes = parseOperationPlanTime(run.movement.latestEndTime)
        if (startMinutes !== null) {
            const baseStartSeconds = startMinutes * 60 - timelineOriginSeconds.value
            const baseEndSeconds = Number(endMinutes ?? startMinutes) * 60 - timelineOriginSeconds.value
            return {
                startSeconds: baseStartSeconds,
                endSeconds: Math.max(baseStartSeconds, baseEndSeconds),
            }
        }
    }
    return {
        startSeconds: run.startSeconds,
        endSeconds: run.endSeconds,
    }
}

function createGanttBlock(
    run: RouteRun,
    cellID: string,
    blockIndex: number,
    rawStartSeconds: number,
    rawEndSeconds: number,
): GanttBlock {
    const duration = Math.max(0.1, simulationDurationSeconds.value)
    const orderedStartSeconds = Math.min(rawStartSeconds, rawEndSeconds)
    const orderedEndSeconds = Math.max(rawStartSeconds, rawEndSeconds)
    const startSeconds = Math.max(0, Math.min(duration, orderedStartSeconds))
    const endSeconds = Math.max(startSeconds + 0.1, Math.max(0, Math.min(duration, orderedEndSeconds)))
    return {
        key: `${run.key}-${cellID}-${blockIndex}`,
        label: '',
        title: '',
        startSeconds,
        endSeconds,
        left: secondsToGanttLeft(startSeconds),
        width: Math.max(8, (endSeconds - startSeconds) * ganttTimeScale.value),
        color: run.color,
    }
}

function getRouteLayoutCellIds(run: RouteRun) {
    if (run.linkIds.length === 0) return []
    const routeLinkIds = new Set(run.linkIds)
    return layoutCells.value
        .filter((cell) => parseRouteReferenceList(cell.linkIDList).some((linkID) => routeLinkIds.has(linkID)))
        .map((cell) => cell.id || cell.name)
        .filter((cellID) => Boolean(cellID))
}

function getGanttTickStyle(tick: GanttTick) {
    return {
        left: `${tick.left}px`,
    }
}

function getGanttBlockStyle(block: GanttBlock) {
    return {
        left: `${block.left}px`,
        width: `${block.width}px`,
        '--layout3d-gantt-block-color': block.color,
    }
}

function getGanttBlockClassName(block: GanttBlock) {
    const classes: string[] = []
    if (playheadSeconds.value >= block.endSeconds) {
        classes.push('is-finished')
    } else if (playheadSeconds.value >= block.startSeconds) {
        classes.push('is-active')
    } else {
        classes.push('is-waiting')
    }
    return classes.join(' ')
}

function getGanttSubTableFallbackName(index: number) {
    return String(t('stationLayout3d.gantt.subTableFallbackName', { index }))
}

function createGanttSubTable(index: number, name?: string): GanttSubTable {
    return {
        id: `occupation-time-sub-table-${index}`,
        name: name?.trim() || getGanttSubTableFallbackName(index),
        cellIds: [],
        hasCustomSelection: false,
    }
}

function formatGanttSubTableLabel(subTable: GanttSubTable, index: number) {
    return subTable.name?.trim() || getGanttSubTableFallbackName(index + 1)
}

function normalizeGanttSubTableCellIds(cellIds: string[]) {
    const availableCellIds = new Set(ganttAvailableCells.value.map((cell) => cell.id))
    return normalizeUniqueStrings(cellIds).filter((cellID) => availableCellIds.has(cellID))
}

function normalizeStoredGanttSubTableCellIds(cellIds: string[]) {
    return normalizeUniqueStrings(cellIds)
}

function runWithoutGanttSubTableSave(action: () => void) {
    suppressGanttSubTableSave = true
    try {
        action()
    } finally {
        void nextTick(() => {
            suppressGanttSubTableSave = false
        })
    }
}

function resetGanttSubTables() {
    ganttSubTableSequence.value = ganttDefaultSubTableCount
    ganttSubTables.value = Array.from(
        { length: ganttDefaultSubTableCount },
        (_, index) => createGanttSubTable(index + 1),
    )
    activeGanttSubTableId.value = ganttSubTables.value[0]?.id || ''
}

function syncGanttSubTables(cells: LayoutCell[]) {
    if (ganttSubTables.value.length === 0) {
        ganttSubTables.value = [createGanttSubTable(1)]
        ganttSubTableSequence.value = 1
    }

    const cellIds = cells.map((cell) => cell.id).filter(Boolean)
    const availableCellIds = new Set(cellIds)
    if (cellIds.length === 0) return

    ganttSubTables.value = ganttSubTables.value.map((subTable) => ({
        ...subTable,
        cellIds: subTable.cellIds.filter((cellID) => availableCellIds.has(cellID)),
    }))

    if (!ganttSubTables.value.some((subTable) => subTable.id === activeGanttSubTableId.value)) {
        activeGanttSubTableId.value = ganttSubTables.value[0]?.id || ''
    }

    const hasCustomSelection = ganttSubTables.value.some((subTable) => subTable.hasCustomSelection)
    if (hasCustomSelection) return

    const tableCount = Math.max(1, ganttSubTables.value.length)
    const chunkSize = Math.max(1, Math.ceil(cellIds.length / tableCount))
    ganttSubTables.value = ganttSubTables.value.map((subTable, index) => ({
        ...subTable,
        cellIds: cellIds.slice(index * chunkSize, (index + 1) * chunkSize),
        hasCustomSelection: false,
    }))
}

function getNextGanttSubTableDraft() {
    const usedIds = new Set(ganttSubTables.value.map((item) => item.id))
    let sequence = ganttSubTableSequence.value
    let subTable: GanttSubTable
    do {
        sequence += 1
        subTable = createGanttSubTable(sequence)
    } while (usedIds.has(subTable.id))

    return { sequence, subTable }
}

function openCreateGanttSubTableDialog() {
    const { sequence, subTable } = getNextGanttSubTableDraft()
    const selectedCellIds = new Set(ganttSubTables.value.flatMap((item) => item.cellIds))
    const remainingCellIds = ganttAvailableCells.value
        .map((cell) => cell.id)
        .filter((cellID) => !selectedCellIds.has(cellID))

    ganttSubTableDialogMode.value = 'create'
    ganttSubTableDialogTargetId.value = subTable.id
    ganttSubTableDialogTargetSequence.value = sequence
    ganttSubTableDialogForm.value = {
        name: subTable.name,
        cellIds: remainingCellIds,
    }
    ganttSubTableDialogVisible.value = true
}

function openEditGanttSubTableDialog() {
    const activeSubTable = activeGanttSubTable.value
    if (!activeSubTable) return

    const activeIndex = ganttSubTables.value.findIndex((subTable) => subTable.id === activeSubTable.id)
    ganttSubTableDialogMode.value = 'edit'
    ganttSubTableDialogTargetId.value = activeSubTable.id
    ganttSubTableDialogTargetSequence.value = 0
    ganttSubTableDialogForm.value = {
        name: activeSubTable.name?.trim() || getGanttSubTableFallbackName(activeIndex + 1),
        cellIds: [...activeSubTable.cellIds],
    }
    ganttSubTableDialogVisible.value = true
}

function confirmGanttSubTableDialog() {
    const name = ganttSubTableDialogForm.value.name.trim()
    if (!name) {
        ElMessage.warning(t('stationLayout3d.messages.subTableNameRequired'))
        return
    }

    const cellIds = normalizeGanttSubTableCellIds(ganttSubTableDialogForm.value.cellIds)
    if (ganttSubTableDialogMode.value === 'create') {
        let subTableId = ganttSubTableDialogTargetId.value
        let sequence = ganttSubTableDialogTargetSequence.value
        if (!subTableId || ganttSubTables.value.some((subTable) => subTable.id === subTableId)) {
            const draft = getNextGanttSubTableDraft()
            subTableId = draft.subTable.id
            sequence = draft.sequence
        }

        ganttSubTableSequence.value = Math.max(ganttSubTableSequence.value, sequence)
        ganttSubTables.value = [
            ...ganttSubTables.value,
            {
                id: subTableId,
                name,
                cellIds,
                hasCustomSelection: true,
            },
        ]
        activeGanttSubTableId.value = subTableId
    } else {
        const subTableId = ganttSubTableDialogTargetId.value
        ganttSubTables.value = ganttSubTables.value.map((subTable) => (
            subTable.id === subTableId
                ? {
                    ...subTable,
                    name,
                    cellIds,
                    hasCustomSelection: true,
                }
                : subTable
        ))
    }

    ganttSubTableDialogVisible.value = false
}

function removeGanttSubTable(name: string | number) {
    if (ganttSubTables.value.length <= 1) return

    const subTableId = String(name)
    const removedIndex = ganttSubTables.value.findIndex((subTable) => subTable.id === subTableId)
    if (removedIndex < 0) return

    const nextSubTables = ganttSubTables.value.filter((subTable) => subTable.id !== subTableId)
    ganttSubTables.value = nextSubTables
    if (activeGanttSubTableId.value === subTableId) {
        activeGanttSubTableId.value = nextSubTables[Math.min(removedIndex, nextSubTables.length - 1)]?.id || ''
    }
}

function normalizeGanttSubTableSetting(item: unknown): GanttSubTable | null {
    const id = readString(item, 'subTableID', 'SubTableID', 'id', 'ID').trim()
    if (!id) return null

    const cellIDs = normalizeStoredGanttSubTableCellIds(
        readArray(item, 'cellIDs', 'CellIDs', 'cellIds').map((cellID) => String(cellID ?? '')),
    )
    const fallbackCellIDList = readString(item, 'cellIDList', 'CellIDList').trim()
    return {
        id,
        name: readString(item, 'subTableName', 'SubTableName', 'name', 'Name').trim(),
        cellIds: cellIDs.length > 0
            ? cellIDs
            : normalizeStoredGanttSubTableCellIds(parseRouteReferenceList(fallbackCellIDList)),
        hasCustomSelection: true,
    }
}

function applyGanttSubTableSettings(settings: GanttSubTable[]) {
    const nextSettings = settings.length > 0
        ? settings
        : Array.from({ length: ganttDefaultSubTableCount }, (_, index) => createGanttSubTable(index + 1))

    runWithoutGanttSubTableSave(() => {
        ganttSubTables.value = nextSettings.map((setting, index) => ({
            ...setting,
            name: setting.name?.trim() || getGanttSubTableFallbackName(index + 1),
            cellIds: normalizeStoredGanttSubTableCellIds(setting.cellIds),
            hasCustomSelection: true,
        }))
        ganttSubTableSequence.value = Math.max(ganttDefaultSubTableCount, ganttSubTables.value.length)
        activeGanttSubTableId.value = ganttSubTables.value[0]?.id || ''
        syncGanttSubTables(ganttAvailableCells.value)
    })
}

function buildGanttSubTableSettingsPayload(): GanttSubTableSettingPayload[] {
    return ganttSubTables.value.map((subTable, index) => ({
        subTableID: subTable.id,
        subTableName: subTable.name?.trim() || getGanttSubTableFallbackName(index + 1),
        cellIDs: normalizeStoredGanttSubTableCellIds(subTable.cellIds),
        sortOrder: index,
    }))
}

function findActiveRunIndex(currentSeconds: number) {
    const runs = routeRuns.value
    if (runs.length === 0) return -1
    const inProgress = runs.find((run) => currentSeconds >= run.startSeconds && currentSeconds <= run.endSeconds)
    if (inProgress) return runs.indexOf(inProgress)
    for (let index = runs.length - 1; index >= 0; index--) {
        const run = runs[index]
        if (run && currentSeconds >= run.endSeconds) return index
    }
    return 0
}

function findHighlightedRunIndices(currentSeconds: number) {
    if (!isAllTrainPlayback.value) {
        const index = findActiveRunIndex(currentSeconds)
        return index >= 0 ? [index] : []
    }
    return routeRuns.value
        .map((run, index) => (currentSeconds >= run.startSeconds && currentSeconds < run.endSeconds ? index : -1))
        .filter((index) => index >= 0)
}

function syncActiveRunIndex(currentSeconds = playheadSeconds.value) {
    const nextIndex = findActiveRunIndex(currentSeconds)
    if (activeRunIndex.value !== nextIndex) activeRunIndex.value = nextIndex
    const nextActiveIndices = findHighlightedRunIndices(currentSeconds)
    if (!areNumberArraysEqual(activeRunIndices.value, nextActiveIndices)) activeRunIndices.value = nextActiveIndices
    const nextPhaseByKey = buildRunPhaseMap(currentSeconds)
    if (!areRunPhaseMapsEqual(runPhaseByKey.value, nextPhaseByKey)) runPhaseByKey.value = nextPhaseByKey
    const nextRun = nextIndex >= 0 ? routeRuns.value[nextIndex] || null : null
    const nextPhase = getRunPhase(nextRun, currentSeconds)
    if (activeRunPhase.value !== nextPhase) activeRunPhase.value = nextPhase
    let lockingCount = 0
    let movingCount = 0
    nextActiveIndices.forEach((index) => {
        const phase = getRunPhase(routeRuns.value[index] || null, currentSeconds)
        if (phase === 'locking') lockingCount++
        if (phase === 'moving') movingCount++
    })
    if (activeLockingRunCount.value !== lockingCount) activeLockingRunCount.value = lockingCount
    if (activeMovingRunCount.value !== movingCount) activeMovingRunCount.value = movingCount
}

function getRunPhase(run: RouteRun | null, currentSeconds: number): RunPhase {
    if (!run) return 'waiting'
    if (currentSeconds < run.startSeconds) return 'waiting'
    if (currentSeconds >= run.endSeconds) return 'finished'
    if (currentSeconds <= run.startSeconds + run.lockSeconds) return 'locking'
    return 'moving'
}

function buildRunPhaseMap(currentSeconds: number) {
    const phaseMap: Record<string, RunPhase> = {}
    routeRuns.value.forEach((run) => {
        phaseMap[run.key] = getRunPhase(run, currentSeconds)
    })
    return phaseMap
}

function areNumberArraysEqual(left: number[], right: number[]) {
    if (left.length !== right.length) return false
    return left.every((value, index) => value === right[index])
}

function areRunPhaseMapsEqual(left: Record<string, RunPhase>, right: Record<string, RunPhase>) {
    const leftKeys = Object.keys(left)
    const rightKeys = Object.keys(right)
    if (leftKeys.length !== rightKeys.length) return false
    return rightKeys.every((key) => left[key] === right[key])
}

function getActiveRouteProgress(run: RouteRun | null, currentSeconds: number) {
    if (!run) return 0
    const moveStart = run.startSeconds + run.lockSeconds
    const moveDuration = Math.max(0.1, run.endSeconds - moveStart)
    if (currentSeconds <= moveStart) return 0
    return Math.max(0, Math.min(1, (currentSeconds - moveStart) / moveDuration))
}

function formatPlayheadTooltip(value: number) {
    return usesPlanTime.value
        ? formatClockSeconds(timelineOriginSeconds.value + Number(value || 0))
        : formatDurationSeconds(Number(value || 0))
}

function handlePlayheadInput(value: number | number[]) {
    const nextValue = Array.isArray(value) ? Number(value[0] || 0) : Number(value || 0)
    setPlayheadSeconds(nextValue)
}

function togglePlayback() {
    if (!canPlayback.value) return
    if (isPlaying.value) {
        pausePlayback()
    } else {
        startPlayback()
    }
}

function startPlayback() {
    if (!canPlayback.value) return
    if (playheadSeconds.value >= simulationDurationSeconds.value) {
        setPlayheadSeconds(0)
    }
    playbackRuntimeSeconds = playheadSeconds.value
    isPlaying.value = true
    lastPlaybackTimestamp = 0
    lastPlaybackRenderTimestamp = 0
    playbackFrameId = window.requestAnimationFrame(stepPlayback)
}

function pausePlayback() {
    isPlaying.value = false
    if (playbackFrameId !== null) {
        window.cancelAnimationFrame(playbackFrameId)
        playbackFrameId = null
    }
}

function resetPlayback() {
    pausePlayback()
    clearTrainCarAngleMemory()
    setPlayheadSeconds(0)
    updateTrainObjects()
}

function stepPlayback(timestamp: number) {
    if (!isPlaying.value) return
    if (!lastPlaybackTimestamp) lastPlaybackTimestamp = timestamp
    const deltaSeconds = Math.max(0, (timestamp - lastPlaybackTimestamp) / 1000)
    lastPlaybackTimestamp = timestamp
    playbackRuntimeSeconds = Math.min(
        simulationDurationSeconds.value,
        playbackRuntimeSeconds + deltaSeconds * playbackSpeed.value,
    )
    const shouldRender = timestamp - lastPlaybackRenderTimestamp >= playbackRenderIntervalMs ||
        playbackRuntimeSeconds >= simulationDurationSeconds.value
    if (shouldRender) {
        playheadSeconds.value = playbackRuntimeSeconds
        syncActiveRunIndex(playbackRuntimeSeconds)
        updateTrainObjects()
        lastPlaybackRenderTimestamp = timestamp
    }
    if (playbackRuntimeSeconds >= simulationDurationSeconds.value) {
        setPlayheadSeconds(simulationDurationSeconds.value)
        pausePlayback()
        return
    }
    playbackFrameId = window.requestAnimationFrame(stepPlayback)
}

function stopPlaybackForReload() {
    pausePlayback()
    clearTrainCarAngleMemory()
    setPlayheadSeconds(0)
}

function clampPlayheadToDuration() {
    if (playheadSeconds.value > simulationDurationSeconds.value) {
        setPlayheadSeconds(simulationDurationSeconds.value)
        return
    }
    syncActiveRunIndex(playheadSeconds.value)
}

function setPlayheadSeconds(value: number) {
    const clamped = Math.max(0, Math.min(simulationDurationSeconds.value, Number(value || 0)))
    playbackRuntimeSeconds = clamped
    playheadSeconds.value = clamped
    syncActiveRunIndex(clamped)
    updateTrainObjects()
}

function scheduleScrollGanttToPlayhead() {
    if (ganttScrollFrameId !== null) {
        window.cancelAnimationFrame(ganttScrollFrameId)
    }
    ganttScrollFrameId = window.requestAnimationFrame(() => {
        ganttScrollFrameId = null
        scrollGanttToPlayhead()
    })
}

function scrollGanttToPlayhead() {
    const viewport = ganttViewportRef.value
    if (!viewport || routeRuns.value.length === 0) return
    const playheadContentLeft = ganttSidebarWidth + ganttPlayheadLeft.value
    viewport.scrollLeft = Math.max(0, playheadContentLeft - viewport.clientWidth * 0.45)
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

    trainGroup = new THREE.Group()
    scene.add(trainGroup)

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

function getLayoutTrackGaugeUnits() {
    const centerSpacingUnits = layoutGridSpacing.value * TRACK_CENTERLINE_GRID_COUNT
    return centerSpacingUnits * STANDARD_TRACK_GAUGE_MM / TRACK_CENTERLINE_SPACING_MM
}

function getWorldTrackGauge(mapper: LayoutMapper) {
    return Math.max(0.001, mapper.mapLength(getLayoutTrackGaugeUnits()))
}

function getRailWidth(trackGauge: number) {
    return Math.max(0.028, trackGauge * 0.052)
}

function getBallastWidth(trackGauge: number) {
    return Math.max(trackGauge * 2.35, trackGauge + 0.42)
}

function getSleeperLength(trackGauge: number) {
    return Math.max(trackGauge * 1.72, trackGauge + 0.32)
}

function getSleeperWidth(trackGauge: number) {
    return Math.max(0.075, trackGauge * 0.095)
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
    const trackGauge = getWorldTrackGauge(mapper)
    const railWidth = getRailWidth(trackGauge)
    const sleeperLength = getSleeperLength(trackGauge)

    if (options.ballast !== false) {
        const ballast = new THREE.Mesh(
            new THREE.BoxGeometry(length + 0.1, BALLAST_HEIGHT, getBallastWidth(trackGauge)),
            materials.ballast,
        )
        ballast.position.y = BALLAST_HEIGHT / 2
        setShadow(ballast, true, true)
        group.add(ballast)
    }

    const railGeo = new THREE.BoxGeometry(length + 0.04, RAIL_HEIGHT, railWidth)
    for (const railZ of [-trackGauge / 2, trackGauge / 2]) {
        const rail = new THREE.Mesh(railGeo, materials.rail)
        rail.position.set(0, RAIL_Y, railZ)
        setShadow(rail, true, true)
        group.add(rail)
    }

    if (options.sleepers !== false) {
        const sleeperCount = Math.max(1, Math.min(MAX_SLEEPERS_PER_SEGMENT, Math.floor(length / SLEEPER_SPACING)))
        const sleeperGeo = new THREE.BoxGeometry(getSleeperWidth(trackGauge), SLEEPER_HEIGHT, sleeperLength)
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

function addSwitchRouteRails(
    group: THREE.Group,
    origin: THREE.Vector3,
    branch: SwitchRenderBranch,
    materials: SceneMaterials,
    trackGauge: number,
) {
    const normal = getWorldLeftNormal(branch.direction)
    const routeStartDistance = 0.08
    const routeEndDistance = branch.renderLength
    const railWidth = getRailWidth(trackGauge)
    for (const railOffset of [-trackGauge / 2, trackGauge / 2]) {
        const start = origin
            .clone()
            .addScaledVector(branch.direction, routeStartDistance)
            .addScaledVector(normal, railOffset)
        const end = origin
            .clone()
            .addScaledVector(branch.direction, routeEndDistance)
            .addScaledVector(normal, railOffset)
        addBeamBetweenWorld(group, start, end, railWidth * 1.2, RAIL_HEIGHT * 1.18, materials.switchGuard, RAIL_Y + 0.045)
    }
}

function addSwitchTieFan(
    group: THREE.Group,
    origin: THREE.Vector3,
    branches: SwitchRenderBranch[],
    materials: SceneMaterials,
    trackGauge: number,
) {
    const sleeperLength = getSleeperLength(trackGauge)
    const sleeperWidth = getSleeperWidth(trackGauge)
    for (const branch of branches) {
        const maxDistance = Math.min(branch.renderLength - 0.25, 3.35)
        for (let distance = 0.72; distance <= maxDistance; distance += 0.72) {
            const center = origin.clone().addScaledVector(branch.direction, distance)
            const normal = getWorldLeftNormal(branch.direction)
            const start = center.clone().addScaledVector(normal, -sleeperLength * 0.58)
            const end = center.clone().addScaledVector(normal, sleeperLength * 0.58)
            addBeamBetweenWorld(group, start, end, sleeperWidth * 0.92, SLEEPER_HEIGHT * 0.9, materials.switchTie, SLEEPER_Y + 0.02)
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
    const trackGauge = getWorldTrackGauge(mapper)
    branches.forEach((branch) => addSwitchRouteRails(group, position, branch, materials, trackGauge))
    addSwitchTieFan(group, position, branches, materials, trackGauge)
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

function createBoxMesh(material: THREE.Material) {
    const mesh = new THREE.Mesh(new THREE.BoxGeometry(1, 1, 1), material)
    setShadow(mesh, true, true)
    return mesh
}

function createWheelMesh(material: THREE.Material) {
    const wheelGeometry = new THREE.CylinderGeometry(0.5, 0.5, 1, 24)
    wheelGeometry.rotateX(Math.PI / 2)
    const wheel = new THREE.Mesh(wheelGeometry, material)
    setShadow(wheel, true, true)
    return wheel
}

function createAxleMesh(material: THREE.Material) {
    const axleGeometry = new THREE.CylinderGeometry(0.5, 0.5, 1, 12)
    axleGeometry.rotateX(Math.PI / 2)
    const axle = new THREE.Mesh(axleGeometry, material)
    setShadow(axle, true, true)
    return axle
}

function createTrainCarObject(): TrainCarObjectEntry {
    const group = new THREE.Group()
    const bodyMaterial = new THREE.MeshStandardMaterial({
        color: 0x8a3b2b,
        roughness: 0.72,
        metalness: 0.08,
    })
    const sidePanelMaterial = new THREE.MeshStandardMaterial({
        color: 0x713124,
        roughness: 0.78,
        metalness: 0.06,
    })
    const detailMaterial = new THREE.MeshStandardMaterial({
        color: 0x1f2933,
        roughness: 0.62,
        metalness: 0.32,
    })
    const wheelMaterial = new THREE.MeshStandardMaterial({
        color: 0x111827,
        roughness: 0.48,
        metalness: 0.55,
    })

    const body = createBoxMesh(bodyMaterial)
    group.add(body)

    const sidePanels = Array.from({ length: 2 }, () => {
        const panel = createBoxMesh(sidePanelMaterial)
        group.add(panel)
        return panel
    })
    const endPanels = Array.from({ length: 2 }, () => {
        const panel = createBoxMesh(sidePanelMaterial)
        group.add(panel)
        return panel
    })
    const ribs = Array.from({ length: 16 }, () => {
        const rib = createBoxMesh(detailMaterial)
        group.add(rib)
        return rib
    })
    const doorPanels = Array.from({ length: 4 }, () => {
        const door = createBoxMesh(sidePanelMaterial)
        group.add(door)
        return door
    })
    const underframe = createBoxMesh(detailMaterial)
    const centerBeam = createBoxMesh(detailMaterial)
    group.add(underframe, centerBeam)

    const bogieFrames = Array.from({ length: 4 }, () => {
        const frame = createBoxMesh(detailMaterial)
        group.add(frame)
        return frame
    })
    const axles = Array.from({ length: 4 }, () => {
        const axle = createAxleMesh(wheelMaterial)
        group.add(axle)
        return axle
    })
    const wheels = Array.from({ length: 8 }, () => {
        const wheel = createWheelMesh(wheelMaterial)
        group.add(wheel)
        return wheel
    })
    const couplers = Array.from({ length: 2 }, () => {
        const coupler = createBoxMesh(detailMaterial)
        group.add(coupler)
        return coupler
    })

    const labelElement = document.createElement('div')
    labelElement.className = 'layout3d-label layout3d-label-train'
    const label = new CSS2DObject(labelElement)
    group.add(label)

    return {
        group,
        body,
        sidePanels,
        endPanels,
        ribs,
        doorPanels,
        underframe,
        centerBeam,
        bogieFrames,
        axles,
        wheels,
        couplers,
        bodyMaterial,
        sidePanelMaterial,
        detailMaterial,
        label,
        labelElement,
    }
}

function updateTrainCarObject(entry: TrainCarObjectEntry, car: SimulationTrainCar, mapper: LayoutMapper) {
    const position = mapper.mapPoint({ x: car.x, y: car.y })
    const trackGauge = getWorldTrackGauge(mapper)
    const length = Math.max(mapper.mapLength(car.length), trackGauge * 2.25, 1.05)
    const width = Math.max(mapper.mapLength(car.width), trackGauge * 1.45, 0.38)
    const height = Math.max(trainCarBaseHeight, trackGauge * 0.68)
    const bodyColor = getFreightCarBodyColor(car)
    entry.bodyMaterial.color.copy(bodyColor)
    entry.bodyMaterial.emissive.copy(bodyColor).multiplyScalar(0.035)
    entry.bodyMaterial.needsUpdate = true
    entry.sidePanelMaterial.color.copy(bodyColor.clone().multiplyScalar(0.78))
    entry.sidePanelMaterial.needsUpdate = true
    entry.detailMaterial.color.set(0x202833)
    entry.detailMaterial.needsUpdate = true

    entry.group.position.set(position.x, 0, position.z)
    entry.group.rotation.y = -normalizePathAngle(car.angle) * Math.PI / 180
    const bodyLength = length * 0.84
    const bodyWidth = width * 0.92
    const bodyHeight = height * 0.88
    const wheelRadius = Math.max(0.08, trackGauge * 0.2)
    const wheelThickness = Math.max(0.035, trackGauge * 0.075)
    const wheelCenterY = RAIL_Y + wheelRadius + 0.02
    const bodyBottomY = wheelCenterY + wheelRadius + height * 0.16
    const bodyCenterY = bodyBottomY + bodyHeight / 2

    entry.body.scale.set(bodyLength, bodyHeight, bodyWidth)
    entry.body.position.set(0, bodyCenterY, 0)

    entry.sidePanels.forEach((panel, index) => {
        const side = index === 0 ? -1 : 1
        panel.scale.set(bodyLength * 0.96, bodyHeight * 0.82, Math.max(0.018, bodyWidth * 0.035))
        panel.position.set(0, bodyCenterY, side * bodyWidth * 0.515)
    })
    entry.endPanels.forEach((panel, index) => {
        const side = index === 0 ? -1 : 1
        panel.scale.set(Math.max(0.035, bodyLength * 0.025), bodyHeight * 0.9, bodyWidth * 0.94)
        panel.position.set(side * bodyLength * 0.505, bodyCenterY, 0)
    })

    const ribCountPerSide = entry.ribs.length / 2
    entry.ribs.forEach((rib, index) => {
        const side = index < ribCountPerSide ? -1 : 1
        const ribIndex = index % ribCountPerSide
        const rate = ribCountPerSide <= 1 ? 0.5 : ribIndex / (ribCountPerSide - 1)
        rib.scale.set(Math.max(0.018, bodyLength * 0.018), bodyHeight * 0.9, Math.max(0.022, bodyWidth * 0.045))
        rib.position.set(-bodyLength * 0.43 + rate * bodyLength * 0.86, bodyCenterY, side * bodyWidth * 0.545)
    })

    entry.doorPanels.forEach((door, index) => {
        const side = index < 2 ? -1 : 1
        const doorIndex = index % 2
        door.scale.set(bodyLength * 0.18, bodyHeight * 0.58, Math.max(0.024, bodyWidth * 0.05))
        door.position.set((doorIndex === 0 ? -1 : 1) * bodyLength * 0.14, bodyCenterY + bodyHeight * 0.02, side * bodyWidth * 0.565)
    })

    entry.underframe.scale.set(bodyLength * 0.96, Math.max(0.06, height * 0.11), bodyWidth * 0.78)
    entry.underframe.position.set(0, bodyBottomY - height * 0.08, 0)
    entry.centerBeam.scale.set(bodyLength * 1.02, Math.max(0.035, height * 0.07), Math.max(0.04, bodyWidth * 0.12))
    entry.centerBeam.position.set(0, wheelCenterY + wheelRadius * 0.7, 0)

    const bogieCenters = [-bodyLength * 0.32, bodyLength * 0.32]
    const bogieLength = Math.max(bodyLength * 0.18, trackGauge * 0.9)
    const bogieFrameHeight = Math.max(0.055, wheelRadius * 0.46)
    entry.bogieFrames.forEach((frame, index) => {
        const bogieIndex = Math.floor(index / 2)
        const side = index % 2 === 0 ? -1 : 1
        frame.scale.set(bogieLength, bogieFrameHeight, Math.max(0.035, bodyWidth * 0.055))
        frame.position.set(bogieCenters[bogieIndex] || 0, wheelCenterY + wheelRadius * 0.25, side * bodyWidth * 0.39)
    })

    const wheelTrack = bodyWidth * 0.34
    const axleHalfSpacing = bogieLength * 0.23
    entry.axles.forEach((axle, index) => {
        const bogieIndex = Math.floor(index / 2)
        const axleSide = index % 2 === 0 ? -1 : 1
        axle.scale.set(Math.max(0.025, wheelRadius * 0.18), Math.max(0.025, wheelRadius * 0.18), wheelTrack * 2.2)
        axle.position.set((bogieCenters[bogieIndex] || 0) + axleSide * axleHalfSpacing, wheelCenterY, 0)
    })
    entry.wheels.forEach((wheel, index) => {
        const axleIndex = Math.floor(index / 2)
        const side = index % 2 === 0 ? -1 : 1
        const bogieIndex = Math.floor(axleIndex / 2)
        const axleSide = axleIndex % 2 === 0 ? -1 : 1
        wheel.scale.set(wheelRadius, wheelRadius, wheelThickness)
        wheel.position.set((bogieCenters[bogieIndex] || 0) + axleSide * axleHalfSpacing, wheelCenterY, side * wheelTrack)
    })

    entry.couplers.forEach((coupler, index) => {
        const side = index === 0 ? -1 : 1
        coupler.scale.set(Math.max(0.08, length * 0.06), Math.max(0.035, height * 0.08), Math.max(0.08, width * 0.18))
        coupler.position.set(side * (bodyLength / 2 + length * 0.055), wheelCenterY + wheelRadius * 0.58, 0)
    })

    entry.label.position.set(0, RAIL_Y + height + 0.62, 0)
    entry.labelElement.textContent = car.label || ''
    entry.labelElement.style.display = car.label ? '' : 'none'
}

function getFreightCarBodyColor(car: SimulationTrainCar) {
    const palette = [0x8a3b2b, 0x566f42, 0x6b7280, 0x9a4f2f, 0x475569, 0x7a4a2f]
    const hash = car.key.split('').reduce((sum, char) => sum + char.charCodeAt(0), 0)
    return new THREE.Color(palette[hash % palette.length] || 0x8a3b2b)
}

function removeTrainCarObject(key: string) {
    const entry = trainCarObjectMap.get(key)
    if (!entry) return
    trainGroup?.remove(entry.group)
    disposeObject3D(entry.group)
    trainCarObjectMap.delete(key)
}

function clearTrainObjects() {
    Array.from(trainCarObjectMap.keys()).forEach(removeTrainCarObject)
}

function updateTrainObjects() {
    if (!trainGroup || !lastMapper) {
        clearTrainObjects()
        return
    }

    const cars = simulationTrainCars.value
    const visibleKeys = new Set(cars.map((car) => car.key))
    Array.from(trainCarObjectMap.keys()).forEach((key) => {
        if (!visibleKeys.has(key)) removeTrainCarObject(key)
    })

    cars.forEach((car) => {
        let entry = trainCarObjectMap.get(car.key)
        if (!entry) {
            entry = createTrainCarObject()
            trainCarObjectMap.set(car.key, entry)
            trainGroup?.add(entry.group)
        }
        updateTrainCarObject(entry, car, lastMapper as LayoutMapper)
    })
    renderOnce()
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
    updateTrainObjects()
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

function clearOperationPlans() {
    operationPlanLoadVersion++
    operationPlanOptions.value = []
    currentOperationPlanId.value = ''
    clearGanttSubTableState()
    clearTrainPlan()
}

function clearTrainPlan() {
    trainPlanLoadVersion++
    trainOperationPlanTrains.value = []
    trainOperationPlanMovements.value = []
    selectedTrainId.value = ''
    clearStationRouteTimes()
    stopPlaybackForReload()
}

function clearStationRoutes() {
    stationRouteLoadVersion++
    stationRouteOptions.value = []
    clearStationRouteTimes()
}

function clearStationRouteTimes() {
    stationRouteTimeLoadVersion++
    stationRouteTimesByKey.value = {}
    loadingStationRouteTimes.value = false
}

function clearGanttSubTableState() {
    ganttSubTableLoadVersion++
    if (ganttSubTableSaveTimer) {
        window.clearTimeout(ganttSubTableSaveTimer)
        ganttSubTableSaveTimer = null
    }
    loadingGanttSubTableSettings.value = false
    savingGanttSubTableSettings.value = false
    ganttSubTableDialogVisible.value = false
    ganttSubTableDialogTargetId.value = ''
    ganttSubTableDialogTargetSequence.value = 0
    ganttSubTableDialogForm.value = {
        name: '',
        cellIds: [],
    }
    runWithoutGanttSubTableSave(resetGanttSubTables)
}

function clearLayout() {
    layoutLoadVersion++
    layoutData.value = createEmptyLayout()
    layoutCells.value = []
    layoutGridSpacing.value = 20
    loadErrorMessage.value = ''
    rebuildScene()
}

async function loadOperationPlans() {
    const instanceID = selectedInstanceId.value
    const stationSchemeID = currentStationSchemeId.value.trim()
    if (!instanceID || !stationSchemeID) {
        clearOperationPlans()
        return
    }

    const loadVersion = ++operationPlanLoadVersion
    const previousId = currentOperationPlanId.value
    loadingOperationPlans.value = true
    try {
        const response = await axios.get('/OperationPlan/GetOperationPlans', {
            params: { instanceID, stationSchemeID },
        })
        if (
            loadVersion !== operationPlanLoadVersion ||
            instanceID !== selectedInstanceId.value ||
            stationSchemeID !== currentStationSchemeId.value.trim()
        ) {
            return
        }
        operationPlanOptions.value = (Array.isArray(response.data) ? response.data : [])
            .map(normalizeOperationPlanOption)
            .filter((item): item is OperationPlanOption => item !== null)
        currentOperationPlanId.value = operationPlanOptions.value.some((item) => item.operationPlanID === previousId)
            ? previousId
            : operationPlanOptions.value.find((item) => item.operationPlanID === defaultOperationPlanID)?.operationPlanID ||
                operationPlanOptions.value[0]?.operationPlanID ||
                ''
    } catch (error) {
        if (loadVersion !== operationPlanLoadVersion) return
        console.error('Failed to load 3D operation plans:', error)
        clearOperationPlans()
        ElMessage.error(t('stationLayout3d.messages.loadOperationPlansFailed'))
    } finally {
        if (loadVersion === operationPlanLoadVersion) loadingOperationPlans.value = false
    }
}

async function loadStationRoutes() {
    const instanceID = selectedInstanceId.value
    const stationSchemeID = currentStationSchemeId.value.trim()
    if (!instanceID || !stationSchemeID) {
        clearStationRoutes()
        return
    }

    const loadVersion = ++stationRouteLoadVersion
    loadingStationRoutes.value = true
    try {
        const response = await axios.get('/StationLayout/GetStationRoutes', {
            params: { instanceID, stationSchemeID },
        })
        if (
            loadVersion !== stationRouteLoadVersion ||
            instanceID !== selectedInstanceId.value ||
            stationSchemeID !== currentStationSchemeId.value.trim()
        ) {
            return
        }
        stationRouteOptions.value = (Array.isArray(response.data) ? response.data : [])
            .map(normalizeStationRouteOption)
            .filter((item): item is StationRouteOption => item !== null)
    } catch (error) {
        if (loadVersion !== stationRouteLoadVersion) return
        console.error('Failed to load 3D station routes:', error)
        stationRouteOptions.value = []
        ElMessage.error(t('stationLayout3d.messages.loadStationRoutesFailed'))
    } finally {
        if (loadVersion === stationRouteLoadVersion) loadingStationRoutes.value = false
    }
}

async function loadTrainOperationPlan() {
    const instanceID = selectedInstanceId.value
    const stationSchemeID = currentStationSchemeId.value.trim()
    const operationPlanID = currentOperationPlanId.value.trim()
    if (!instanceID || !stationSchemeID || !operationPlanID) {
        clearTrainPlan()
        return
    }

    const loadVersion = ++trainPlanLoadVersion
    loadingTrainOperationPlan.value = true
    try {
        const response = await axios.get('/OperationPlan/GetTrainOperationPlan', {
            params: { instanceID, stationSchemeID, operationPlanID },
        })
        if (
            loadVersion !== trainPlanLoadVersion ||
            instanceID !== selectedInstanceId.value ||
            stationSchemeID !== currentStationSchemeId.value.trim() ||
            operationPlanID !== currentOperationPlanId.value.trim()
        ) {
            return
        }
        normalizeTrainOperationPlanResponse(response.data)
        stopPlaybackForReload()
    } catch (error) {
        if (loadVersion !== trainPlanLoadVersion) return
        console.error('Failed to load 3D train operation plan:', error)
        clearTrainPlan()
        ElMessage.error(t('stationLayout3d.messages.loadTrainOperationPlanFailed'))
    } finally {
        if (loadVersion === trainPlanLoadVersion) loadingTrainOperationPlan.value = false
    }
}

function getStationRouteTimePairs() {
    const pairs = new Map<string, { routeID: string; trainTypeID: string }>()
    trainOperationPlanMovements.value.forEach((movement) => {
        const routeID = getMovementRouteID(movement)
        if (!routeID) return
        const trainTypeID = trainMap.value.get(movement.trainID)?.trainType?.trim() || ''
        const defaultKey = getStationRouteTimeKey(routeID, '')
        pairs.set(defaultKey, { routeID, trainTypeID: '' })
        if (trainTypeID) {
            const specificKey = getStationRouteTimeKey(routeID, trainTypeID)
            pairs.set(specificKey, { routeID, trainTypeID })
        }
    })
    return Array.from(pairs.values())
}

async function loadStationRouteTimes() {
    const instanceID = selectedInstanceId.value
    const stationSchemeID = currentStationSchemeId.value.trim()
    if (!instanceID || !stationSchemeID || trainOperationPlanMovements.value.length === 0) {
        clearStationRouteTimes()
        return
    }

    const loadVersion = ++stationRouteTimeLoadVersion
    loadingStationRouteTimes.value = true
    try {
        const pairs = getStationRouteTimePairs()
        if (pairs.length === 0) {
            stationRouteTimesByKey.value = {}
            return
        }

        const entries = await Promise.all(pairs.map(async (pair) => {
            const response = await axios.get('/StationLayout/GetStationRouteTimes', {
                params: {
                    instanceID,
                    stationSchemeID,
                    routeID: pair.routeID,
                    trainTypeID: pair.trainTypeID,
                },
            })
            const rows = (Array.isArray(response.data) ? response.data : [])
                .map(normalizeStationRouteTimeOption)
                .filter((item): item is StationRouteTimeOption => item !== null)
                .map((time) => ({
                    ...time,
                    routeID: time.routeID || pair.routeID,
                    trainTypeID: time.trainTypeID || pair.trainTypeID,
                }))
            return [getStationRouteTimeKey(pair.routeID, pair.trainTypeID), rows] as const
        }))

        if (
            loadVersion !== stationRouteTimeLoadVersion ||
            instanceID !== selectedInstanceId.value ||
            stationSchemeID !== currentStationSchemeId.value.trim()
        ) {
            return
        }
        stationRouteTimesByKey.value = Object.fromEntries(entries)
    } catch (error) {
        if (loadVersion !== stationRouteTimeLoadVersion) return
        console.error('Failed to load 3D station route times:', error)
        stationRouteTimesByKey.value = {}
        ElMessage.error(t('stationLayout3d.messages.loadRouteTimesFailed'))
    } finally {
        if (loadVersion === stationRouteTimeLoadVersion) loadingStationRouteTimes.value = false
    }
}

async function loadGanttSubTableSettings() {
    const instanceID = selectedInstanceId.value
    const stationSchemeID = currentStationSchemeId.value.trim()
    const operationPlanID = currentOperationPlanId.value.trim()
    if (!instanceID || !stationSchemeID || !operationPlanID) {
        runWithoutGanttSubTableSave(resetGanttSubTables)
        return
    }

    const loadVersion = ++ganttSubTableLoadVersion
    loadingGanttSubTableSettings.value = true
    try {
        const response = await axios.get('/OperationPlan/GetOperationOccupationTimeSubTables', {
            params: {
                instanceID,
                stationSchemeID,
                operationPlanID,
            },
        })
        if (
            loadVersion !== ganttSubTableLoadVersion ||
            instanceID !== selectedInstanceId.value ||
            stationSchemeID !== currentStationSchemeId.value.trim() ||
            operationPlanID !== currentOperationPlanId.value.trim()
        ) {
            return
        }

        const settings = (Array.isArray(response.data) ? response.data : [])
            .map(normalizeGanttSubTableSetting)
            .filter((item): item is GanttSubTable => item !== null)
        if (settings.length > 0) {
            applyGanttSubTableSettings(settings)
            return
        }

        runWithoutGanttSubTableSave(() => {
            resetGanttSubTables()
            syncGanttSubTables(ganttAvailableCells.value)
        })
        void nextTick(() => {
            scheduleSaveGanttSubTableSettings(0)
        })
    } catch (error) {
        if (loadVersion !== ganttSubTableLoadVersion) return
        console.error('Failed to load 3D gantt sub table settings:', error)
        runWithoutGanttSubTableSave(() => {
            resetGanttSubTables()
            syncGanttSubTables(ganttAvailableCells.value)
        })
    } finally {
        if (loadVersion === ganttSubTableLoadVersion) {
            loadingGanttSubTableSettings.value = false
        }
    }
}

async function saveGanttSubTableSettingsNow() {
    if (
        suppressGanttSubTableSave ||
        loadingGanttSubTableSettings.value ||
        savingGanttSubTableSettings.value
    ) {
        return
    }

    const instanceID = selectedInstanceId.value
    const stationSchemeID = currentStationSchemeId.value.trim()
    const operationPlanID = currentOperationPlanId.value.trim()
    if (!instanceID || !stationSchemeID || !operationPlanID) return

    const subTables = buildGanttSubTableSettingsPayload()
    if (subTables.length === 0) return

    const savingRevision = ganttSubTableSaveRevision
    savingGanttSubTableSettings.value = true
    try {
        const response = await axios.put('/OperationPlan/SaveOperationOccupationTimeSubTables', {
            instanceID,
            stationSchemeID,
            operationPlanID,
            subTables,
        })
        if (
            instanceID !== selectedInstanceId.value ||
            stationSchemeID !== currentStationSchemeId.value.trim() ||
            operationPlanID !== currentOperationPlanId.value.trim()
        ) {
            return
        }

        const settings = (Array.isArray(response.data) ? response.data : [])
            .map(normalizeGanttSubTableSetting)
            .filter((item): item is GanttSubTable => item !== null)
        if (settings.length > 0 && savingRevision === ganttSubTableSaveRevision) {
            applyGanttSubTableSettings(settings)
        }
    } catch (error) {
        console.error('Failed to save 3D gantt sub table settings:', error)
    } finally {
        savingGanttSubTableSettings.value = false
        if (savingRevision !== ganttSubTableSaveRevision) {
            scheduleSaveGanttSubTableSettings(0)
        }
    }
}

function scheduleSaveGanttSubTableSettings(delay = 500) {
    if (suppressGanttSubTableSave || loadingGanttSubTableSettings.value) return

    if (ganttSubTableSaveTimer) {
        window.clearTimeout(ganttSubTableSaveTimer)
    }
    ganttSubTableSaveTimer = window.setTimeout(() => {
        ganttSubTableSaveTimer = null
        void saveGanttSubTableSettingsNow()
    }, delay)
}

async function loadLayout() {
    const instanceID = selectedInstanceId.value
    const stationSchemeID = currentStationSchemeId.value.trim()
    loadErrorMessage.value = ''

    if (!instanceID) {
        clearLayout()
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
        layoutCells.value = getLayoutCells(response.data)
        layoutGridSpacing.value = getLayoutGridSpacing(response.data)
        await nextTick()
        rebuildScene()
    } catch (error) {
        if (loadVersion !== layoutLoadVersion) return
        console.error('Failed to load station layout 3D data:', error)
        loadErrorMessage.value = String(t('stationLayout3d.messages.loadFailed'))
        layoutData.value = createEmptyLayout()
        layoutCells.value = []
        layoutGridSpacing.value = 20
        rebuildScene()
        ElMessage.error(loadErrorMessage.value)
    } finally {
        if (loadVersion === layoutLoadVersion) loadingData.value = false
    }
}

async function refresh3DData() {
    if (!hasScope.value) {
        clearStationRoutes()
        clearTrainPlan()
        clearGanttSubTableState()
        await loadLayout()
        return
    }
    stopPlaybackForReload()
    await Promise.all([loadStationRoutes(), loadTrainOperationPlan(), loadLayout()])
    await Promise.all([loadStationRouteTimes(), loadGanttSubTableSettings()])
}

watch(() => props.selectedInstanceId, () => {
    stopPlaybackForReload()
    currentStationSchemeId.value = ''
    stationSchemeOptions.value = []
    void loadStationSchemes()
}, { immediate: true })

watch(() => props.activationKey, () => {
    if (selectedInstanceId.value) {
        void loadStationSchemes()
    }
})

watch(simulationDurationSeconds, () => {
    clampPlayheadToDuration()
})

watch(routeRuns, () => {
    syncActiveRunIndex(playheadSeconds.value)
    updateTrainObjects()
    scheduleScrollGanttToPlayhead()
})

watch(
    ganttAvailableCells,
    (cells) => {
        syncGanttSubTables(cells)
    },
    { immediate: true },
)

watch(
    ganttSubTables,
    () => {
        if (suppressGanttSubTableSave || loadingGanttSubTableSettings.value) return
        ganttSubTableSaveRevision += 1
        scheduleSaveGanttSubTableSettings()
    },
    { deep: true },
)

watch(playheadSeconds, () => {
    updateTrainObjects()
    scheduleScrollGanttToPlayhead()
}, {
    flush: 'post',
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
    pausePlayback()
    cancelRafLoop()
    if (ganttSubTableSaveTimer) {
        window.clearTimeout(ganttSubTableSaveTimer)
        ganttSubTableSaveTimer = null
    }
    if (ganttScrollFrameId !== null) {
        window.cancelAnimationFrame(ganttScrollFrameId)
        ganttScrollFrameId = null
    }
    if (resizeObserver) {
        resizeObserver.disconnect()
        resizeObserver = null
    }
    window.removeEventListener('resize', onResize)
    clearTrainObjects()
    if (trainGroup && scene) scene.remove(trainGroup)
    trainGroup = null
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

.layout3d-plan-select {
    width: 210px;
}

.layout3d-train-select {
    width: 230px;
}

.layout3d-playback-mode :deep(.el-radio-button__inner) {
    padding: 5px 10px;
}

.layout3d-playback-clock {
    min-width: 78px;
    color: #1f3a68;
    font-family: Consolas, "Microsoft YaHei", monospace;
    font-size: 13px;
    font-weight: 700;
    text-align: center;
}

.layout3d-speed-select {
    width: 92px;
}

.layout3d-playback-bar {
    display: flex;
    flex: 0 0 auto;
    align-items: center;
    gap: 12px;
    min-height: 38px;
    padding: 4px 10px;
    border-bottom: 1px solid #d8e2ef;
    background: #ffffff;
}

.layout3d-playback-summary {
    display: inline-flex;
    flex: 0 0 auto;
    align-items: center;
    gap: 8px;
    min-width: 0;
    color: #40546b;
    font-size: 12px;
    white-space: nowrap;
}

.layout3d-playhead-slider {
    flex: 1 1 auto;
    min-width: 180px;
}

.layout3d-content {
    display: flex;
    flex: 1 1 auto;
    min-height: 0;
    flex-direction: column;
    overflow: hidden;
    background: #f5f8fb;
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

:deep(.layout3d-label-train) {
    border-color: rgba(37, 99, 235, 0.35);
    background: rgba(37, 99, 235, 0.9);
    color: #ffffff;
    font-weight: 700;
}

.layout3d-gantt-panel {
    display: flex;
    flex: 0 0 260px;
    min-height: 188px;
    flex-direction: column;
    overflow: hidden;
    border-top: 1px solid #d8e3ef;
    background: #ffffff;
}

.layout3d-gantt-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 10px;
    min-height: 40px;
    padding: 6px 10px;
    border-bottom: 1px solid #edf2f7;
}

.layout3d-gantt-title {
    display: flex;
    flex: 0 0 auto;
    align-items: baseline;
    gap: 8px;
    min-width: 0;
    white-space: nowrap;
}

.layout3d-gantt-header h3 {
    margin: 0;
    color: #21354f;
    font-size: 14px;
    font-weight: 700;
}

.layout3d-gantt-title span {
    color: #65758a;
    font-size: 12px;
}

.layout3d-gantt-subtable-toolbar {
    display: flex;
    flex: 1 1 auto;
    align-items: center;
    gap: 8px;
    min-width: 0;
}

.layout3d-gantt-sub-tabs {
    flex: 1 1 auto;
    min-width: 0;
    overflow: hidden;
}

.layout3d-gantt-sub-tabs :deep(.el-tabs__header) {
    margin: 0;
}

.layout3d-gantt-sub-tabs :deep(.el-tabs__nav-wrap::after) {
    display: none;
}

.layout3d-gantt-sub-tabs :deep(.el-tabs__item) {
    height: 28px;
    padding: 0 12px;
    font-size: 12px;
    line-height: 28px;
}

.layout3d-gantt-subtable-actions {
    display: flex;
    flex: 0 0 auto;
    align-items: center;
    gap: 6px;
}

.layout3d-gantt-subtable-actions :deep(.el-button + .el-button) {
    margin-left: 0;
}

.layout3d-gantt-subtable-summary {
    flex: 0 0 auto;
    color: #65758a;
    font-size: 12px;
    font-weight: 600;
    white-space: nowrap;
}

.layout3d-gantt-subtable-cell-select {
    width: 100%;
}

.layout3d-gantt-viewport {
    flex: 1 1 auto;
    min-height: 0;
    overflow: auto;
    scrollbar-gutter: stable;
}

.layout3d-gantt-content {
    position: relative;
    width: max-content;
    min-height: 100%;
}

.layout3d-gantt-axis-row,
.layout3d-gantt-lane-row {
    display: grid;
    grid-template-columns: var(--layout3d-gantt-sidebar-width) auto;
}

.layout3d-gantt-axis-row {
    position: sticky;
    top: 0;
    z-index: 8;
    height: 36px;
    border-bottom: 1px solid #dfe8f1;
    background: #f8fafc;
}

.layout3d-gantt-lane-row {
    height: 38px;
    border-bottom: 1px solid #eef3f8;
}

.layout3d-gantt-axis-label,
.layout3d-gantt-lane-label {
    position: sticky;
    left: 0;
    z-index: 6;
    box-sizing: border-box;
    width: var(--layout3d-gantt-sidebar-width);
    border-right: 1px solid #dfe8f1;
}

.layout3d-gantt-axis-label {
    display: flex;
    align-items: center;
    padding: 0 10px;
    background: #f8fafc;
    color: #65758a;
    font-size: 12px;
    font-weight: 700;
}

.layout3d-gantt-lane-label {
    display: flex;
    align-items: center;
    overflow: hidden;
    padding: 0 10px;
    background: #ffffff;
    color: #40546b;
    font-size: 12px;
    text-overflow: ellipsis;
    white-space: nowrap;
}

.layout3d-gantt-axis-track,
.layout3d-gantt-lane-track {
    position: relative;
}

.layout3d-gantt-axis-track {
    height: 36px;
    background: #f8fafc;
}

.layout3d-gantt-lane-track {
    height: 38px;
    background: #ffffff;
}

.layout3d-gantt-axis-tick,
.layout3d-gantt-grid-line {
    position: absolute;
    top: 0;
    bottom: 0;
    width: 1px;
    background: #e4ebf3;
}

.layout3d-gantt-axis-tick.is-major,
.layout3d-gantt-grid-line.is-major {
    background: #cbd8e6;
}

.layout3d-gantt-axis-tick span {
    position: absolute;
    bottom: 8px;
    transform: translateX(-50%);
    padding: 0 3px;
    background: #f8fafc;
    color: #65758a;
    font-size: 11px;
    white-space: nowrap;
}

.layout3d-gantt-block {
    position: absolute;
    top: 7px;
    z-index: 3;
    box-sizing: border-box;
    height: 24px;
    overflow: hidden;
    padding: 0 6px;
    border: 1px solid color-mix(in srgb, var(--layout3d-gantt-block-color) 72%, #0f172a);
    border-radius: 5px;
    background: var(--layout3d-gantt-block-color);
    color: #ffffff;
    font-size: 11px;
    line-height: 22px;
    text-overflow: ellipsis;
    white-space: nowrap;
}

.layout3d-gantt-block.is-finished {
    border-color: #8792a1;
    background: #a0a8b3;
    color: #ffffff;
}

.layout3d-gantt-block.is-active {
    box-shadow: 0 0 0 2px rgba(37, 99, 235, 0.22);
    transform: translateY(-1px);
}

.layout3d-gantt-now-line {
    position: absolute;
    top: 0;
    bottom: 0;
    z-index: 5;
    width: 2px;
    transform: translateX(-1px);
    background: #ef4444;
    pointer-events: none;
}

.layout3d-gantt-empty {
    display: flex;
    flex: 1 1 auto;
    align-items: center;
    justify-content: center;
    color: #65758a;
    font-size: 13px;
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
        flex-wrap: wrap;
    }

    .layout3d-playback-bar,
    .layout3d-playback-summary,
    .layout3d-gantt-header,
    .layout3d-gantt-subtable-toolbar {
        align-items: stretch;
        flex-direction: column;
    }

    .layout3d-scheme-select,
    .layout3d-plan-select,
    .layout3d-train-select,
    .layout3d-speed-select {
        width: 100%;
    }

    .layout3d-gantt-panel {
        flex-basis: 230px;
    }
}
</style>
