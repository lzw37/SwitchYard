<template>
    <section class="station-layout-3d-page" v-loading="loadingAnyData">
        <StationLayoutViewToolbar v-model:density="viewToolbarDensity" :show-display-controls="false">
            <template #context>
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
            </template>
            <template #primary>
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
            </template>
            <template #details>
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
            </template>
            <template #actions>
                <ActionButton
                    :label="t('stationLayout3d.buttons.refresh')"
                    :icon="RefreshRight"
                    :loading="loadingAnyData"
                    :disabled="!selectedInstanceId"
                    @click="refresh3DData"
                />
                <ActionButton
                    :label="t('stationLayout3d.buttons.resetView')"
                    :icon="Aim"
                    :disabled="!canRender"
                    @click="resetCamera"
                />
                <ActionButton
                    :label="t('stationLayout3d.buttons.resetPlayback')"
                    :icon="RefreshLeft"
                    :disabled="!canPlayback"
                    @click="resetPlayback"
                />
                <ActionButton
                    :label="isPlaying ? t('stationLayout3d.buttons.pause') : t('stationLayout3d.buttons.play')"
                    type="primary"
                    :icon="isPlaying ? VideoPause : VideoPlay"
                    :disabled="!canPlayback"
                    @click="togglePlayback"
                />
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
                <div class="station-toolbar-switch-control">
                    <span class="station-toolbar-switch-control__label">{{ t('stationLayout3d.labels.showLabels') }}</span>
                    <el-switch v-model="showLabels" size="small" />
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
            </template>
        </StationLayoutViewToolbar>

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

        <div ref="layoutContentRef" class="layout3d-content">
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

            <PaneDivider
                v-model="ganttPanelHeight"
                direction="vertical"
                reverse
                :min="160"
                :max="maxGanttPanelHeight"
                :reset-value="260"
                :label="t('common.resize.vertical')"
            />
            <section class="layout3d-gantt-panel" :style="{ flexBasis: `${ganttPanelHeight}px` }">
                <div class="layout3d-gantt-header">
                    <div class="layout3d-gantt-title">
                        <h3 :title="ganttSummaryText">{{ t('stationLayout3d.gantt.title') }}</h3>
                    </div>
                    <div class="layout3d-gantt-subtable-toolbar">
                        <el-tabs
                            v-model="activeGanttSubTableId"
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
                            <ActionButton
                                :label="t('stationLayout3d.buttons.editSubTable')"
                                :icon="Edit"
                                :disabled="!activeGanttSubTable"
                                @click="openEditGanttSubTableDialog"
                            />
                            <ActionButton
                                :label="t('stationLayout3d.buttons.createSubTable')"
                                :icon="Plus"
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
                <ActionButton
                    variant="text"
                    :label="t('stationLayout3d.dialogs.cancel')"
                    @click="ganttSubTableDialogVisible = false"
                    :icon="Close"
                />
                <ActionButton
                    variant="text"
                    :label="t('stationLayout3d.dialogs.confirm')"
                    type="primary"
                    @click="confirmGanttSubTableDialog"
                    :icon="Check"
                />
            </template>
        </el-dialog>
    </section>
</template>

<script setup lang="ts">
import ActionButton from '@/components/ui/ActionButton.vue'
import PaneDivider from '@/components/ui/PaneDivider.vue'
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { ElMessage } from 'element-plus'
import { Aim, Check, Close, Edit, Plus, RefreshLeft, RefreshRight, VideoPause, VideoPlay } from '@element-plus/icons-vue'
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
import { isDwellingRoute } from './simulationDwelling'
import { getStationRouteHighlightColor } from './routeColors'
import { createTrackOccupancyTimeline } from './trackOccupancy'
import { createTrackOccupancyOverlay } from './three/trackOccupancyOverlay'
import { mapTrackOccupancyPaths } from './three/trackOccupancyPaths'
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js'
import { CSS2DObject, CSS2DRenderer } from 'three/examples/jsm/renderers/CSS2DRenderer.js'
import axios from '@/utils/axios'
import { getSignalStyleAsset } from '@switchyard/station-layout'
import StationLayoutViewToolbar from './components/StationLayoutViewToolbar.vue'

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

type RunPhase = 'waiting' | 'locking' | 'moving' | 'dwelling' | 'finished'
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
const PLATFORM_MIN_SIZE = 1.2
const SIGNAL_SIDE_OFFSET = 2.25
const SIGNAL_LABEL_Y = 1.92
const SIGNAL_HEAD_DEPTH = 0.11
const SIGNAL_LIGHT_RADIUS = 0.07
const SIGNAL_LIGHT_ROW_SPACING = 0.17
const SIGNAL_LIGHT_COLUMN_SPACING = 0.18
const SIGNAL_LIGHT_PADDING = 0.16
const defaultOperationPlanID = 'default'
const syntheticRouteGapSeconds = 1.2
const routeLockMinSeconds = 1.2
const routeLockMaxSeconds = 8
const playbackRenderIntervalMs = 33
const ganttSidebarWidth = 168
const ganttMinTimelineWidth = 860
const ganttMaxTimelineWidth = 6400
const ganttTargetPixelsPerSecond = 0.08
const ganttDefaultSubTableCount = 3

const canvasWrapperRef = ref<HTMLElement | null>(null)
const layoutContentRef = ref<HTMLElement | null>(null)
const ganttPanelHeight = ref(260)
const maxGanttPanelHeight = ref(520)
let panelResizeObserver: ResizeObserver | null = null
const canvasRef = ref<HTMLCanvasElement | null>(null)
const layoutData = ref<StationLayoutData>(createEmptyLayout())
const layoutCells = ref<LayoutCell[]>([])
const layoutGridSpacing = ref(20)
const loadingData = ref(false)
const loadErrorMessage = ref('')
const showLabels = ref(true)
const showTrackOccupancy = ref(false)
const displayRatio = ref(1)
const appliedDisplayRatio = ref(1)
// Keep loaded coordinates intact. Rebuild geometry from transformed coordinates
// when the slider is released, without distorting meshes or the physical gauge.
const displayLayoutData = computed(() => transformLayoutCoordinates(layoutData.value, appliedDisplayRatio.value))
const viewToolbarDensity = ref('compact')
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
const playbackSpeed = ref(10)
const playbackMode = ref<PlaybackMode>('single')
const selectedTrainModel = ref<RollingStockModelSelection>('auto')
const isPlaying = ref(false)
const activeRunIndex = ref(-1)
const activeRunIndices = ref<number[]>([])
const activeRunPhase = ref<RunPhase>('waiting')
const activeLockingRunCount = ref(0)
const activeMovingRunCount = ref(0)
const activeDwellingRunCount = ref(0)
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
let isDisposed = false
const dataRequests = new AbortController()
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
let playbackFrameId: number | null = null
let ganttScrollFrameId: number | null = null
let ganttSubTableSaveTimer: ReturnType<typeof window.setTimeout> | null = null
let suppressGanttSubTableSave = false
let ganttSubTableSaveRevision = 0
let ganttSubTableSavingRevision = 0
let ganttSubTableSaveRequest: Promise<unknown> | null = null
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
const trainModelTemplates = new RollingStockTemplates()
const preparingTrainModels = ref(false)
let trainModelPreparationVersion = 0

const selectedInstanceId = computed(() => props.selectedInstanceId || '')
const hasScheme = computed(() => Boolean(selectedInstanceId.value && currentStationSchemeId.value.trim()))
const hasScope = computed(() => Boolean(hasScheme.value && currentOperationPlanId.value.trim()))
const loadingAnyData = computed(() => (
    preparingTrainModels.value ||
    loadingData.value ||
    loadingStationSchemes.value ||
    loadingOperationPlans.value ||
    loadingStationRoutes.value ||
    loadingStationRouteTimes.value ||
    loadingTrainOperationPlan.value ||
    loadingGanttSubTableSettings.value ||
    savingGanttSubTableSettings.value
))
// Share the exact prepared centreline with the playback path. Compute once per layout.
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
const selectedTrainMovements = computed(() => {
    const trainID = selectedTrainId.value
    if (!trainID) return []
    return trainOperationPlanMovements.value
        .filter((movement) => movement.trainID === trainID)
        .sort(compareMovements)
})
const sourceRouteRuns = computed<RouteRun[]>(() => buildRouteRuns())
// The timetable uses source distances; only the path shown in 3D changes.
const routeRuns = computed<RouteRun[]>(() => sourceRouteRuns.value.map(run => ({
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
// Use every cell's actual occupation window, independently of the visible
// Gantt sub-table. The timeline caches states between occupation boundaries.
const trackOccupancyTimeline = computed(() => createTrackOccupancyTimeline(
    routeRuns.value.flatMap(run => buildGanttBlocksForRun(run).map(({ cellID, block }) => ({
        cellID, startSeconds: block.startSeconds, endSeconds: block.endSeconds, color: block.color,
    }))),
    layoutCells.value,
))
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
        const dwelling = activeDwellingRunCount.value
        if (locking + moving + dwelling <= 0) return t('stationLayout3d.phase.waiting')
        if (dwelling > 0) return String(t('stationLayout3d.phase.allActiveWithDwelling', { locking, moving, dwelling }))
        return String(t('stationLayout3d.phase.allActive', { locking, moving }))
    }
    if (!activeRun.value) return t('stationLayout3d.phase.selecting')
    if (activePhase.value === 'locking') return t('stationLayout3d.phase.locking')
    if (activePhase.value === 'dwelling') return t('stationLayout3d.phase.dwelling')
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
    if (isDisposed) return []
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
            signal: dataRequests.signal,
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
            lockSeconds: isDwellingRoute(item.route.type) ? 0 : Math.min(lockSeconds, duration * 0.65),
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
    const fill = getTrainColor(run.train.id)
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
    let dwellingCount = 0
    const visibleRuns = selectTrainMotionRuns(routeRuns.value, currentSeconds)
    visibleRuns.forEach((run) => {
        const phase = getRunPhase(run, currentSeconds)
        if (phase === 'locking') lockingCount++
        if (phase === 'moving') movingCount++
        if (phase === 'dwelling') dwellingCount++
    })
    if (activeLockingRunCount.value !== lockingCount) activeLockingRunCount.value = lockingCount
    if (activeMovingRunCount.value !== movingCount) activeMovingRunCount.value = movingCount
    if (activeDwellingRunCount.value !== dwellingCount) activeDwellingRunCount.value = dwellingCount
}

function getRunPhase(run: RouteRun | null, currentSeconds: number): RunPhase {
    if (!run) return 'waiting'
    if (currentSeconds < run.startSeconds) return 'waiting'
    if (currentSeconds >= run.endSeconds) return 'finished'
    if (isDwellingRoute(run.route.type)) return 'dwelling'
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
    const moveStart = run.startSeconds + (isDwellingRoute(run.route.type) ? 0 : run.lockSeconds)
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
    if (isDisposed || isPlaying.value || !canPlayback.value) return
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
    if (isPlaying.value) setPlayheadSeconds(playbackRuntimeSeconds)
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
}

function stepPlayback(timestamp: number) {
    playbackFrameId = null
    if (isDisposed || !isPlaying.value) return
    // Model changes may prepare a new fleet. Keep the clock still while the
    // worker runs, so preparation time cannot jump playback past an arrival.
    if (preparingTrainModels.value) {
        lastPlaybackTimestamp = 0
        playbackFrameId = window.requestAnimationFrame(stepPlayback)
        return
    }
    if (!lastPlaybackTimestamp) lastPlaybackTimestamp = timestamp
    // A suspended tab must not skip across a station on its first resumed frame.
    const deltaSeconds = Math.min(0.05, Math.max(0, (timestamp - lastPlaybackTimestamp) / 1000))
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
}

function scheduleScrollGanttToPlayhead() {
    if (isDisposed) return
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
        pausePlayback()
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
    rebuildScene()
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
    if (isPlaying.value) updateTrainObjects(playbackRuntimeSeconds)
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
    if (isDisposed) return
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
            signal: dataRequests.signal,
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
    if (isDisposed) return
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
            signal: dataRequests.signal,
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
    if (isDisposed) return
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
            signal: dataRequests.signal,
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
    if (isDisposed) return
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
                signal: dataRequests.signal,
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
    if (isDisposed) return
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
            signal: dataRequests.signal,
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
        isDisposed ||
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
    ganttSubTableSavingRevision = savingRevision
    savingGanttSubTableSettings.value = true
    const request = axios.put('/OperationPlan/SaveOperationOccupationTimeSubTables', {
        instanceID,
        stationSchemeID,
        operationPlanID,
        subTables,
    })
    ganttSubTableSaveRequest = request
    try {
        const response = await request
        if (
            isDisposed ||
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
        if (!isDisposed) console.error('Failed to save 3D gantt sub table settings:', error)
    } finally {
        // An older scheme's save may finish after a new scheme started saving.
        if (ganttSubTableSaveRequest === request) {
            ganttSubTableSaveRequest = null
            savingGanttSubTableSettings.value = false
            if (!isDisposed && savingRevision !== ganttSubTableSaveRevision) {
                scheduleSaveGanttSubTableSettings(0)
            }
        }
    }
}

function flushPendingGanttSubTableSettings() {
    const hasPendingChanges = ganttSubTableSaveTimer !== null ||
        (savingGanttSubTableSettings.value && ganttSubTableSavingRevision !== ganttSubTableSaveRevision)
    if (!hasPendingChanges || loadingGanttSubTableSettings.value || !hasScope.value) return
    // Finish a user's pending edit before discarding this page. Only the small
    // request payload survives; its result never updates the disposed component.
    const payload = {
        instanceID: selectedInstanceId.value,
        stationSchemeID: currentStationSchemeId.value.trim(),
        operationPlanID: currentOperationPlanId.value.trim(),
        subTables: buildGanttSubTableSettingsPayload(),
    }
    if (!payload.subTables.length) return
    void (ganttSubTableSaveRequest || Promise.resolve())
        .catch(() => undefined)
        .then(() => axios.put('/OperationPlan/SaveOperationOccupationTimeSubTables', payload))
        .catch(error => console.error('Failed to save pending 3D gantt sub table settings:', error))
}

function scheduleSaveGanttSubTableSettings(delay = 500) {
    if (isDisposed || suppressGanttSubTableSave || loadingGanttSubTableSettings.value) return

    if (ganttSubTableSaveTimer) {
        window.clearTimeout(ganttSubTableSaveTimer)
    }
    ganttSubTableSaveTimer = window.setTimeout(() => {
        ganttSubTableSaveTimer = null
        void saveGanttSubTableSettingsNow()
    }, delay)
}

async function loadLayout() {
    if (isDisposed) return
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
            signal: dataRequests.signal,
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
        if (isDisposed || loadVersion !== layoutLoadVersion) return
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
    if (isDisposed) return
    if (!hasScope.value) {
        clearStationRoutes()
        clearTrainPlan()
        clearGanttSubTableState()
        await loadLayout()
        return
    }
    stopPlaybackForReload()
    await Promise.all([loadStationRoutes(), loadTrainOperationPlan(), loadLayout()])
    if (isDisposed) return
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

watch(trainConsistByRun, () => {
    updateTrainObjects()
    void prepareTrainModels()
}, { immediate: true })

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
    if (!isPlaying.value) updateTrainObjects()
    updateTrackOccupancy()
    scheduleScrollGanttToPlayhead()
}, {
    flush: 'post',
})

watch([showTrackOccupancy, trackOccupancyTimeline], updateTrackOccupancy, { flush: 'post' })

onMounted(() => {
    panelResizeObserver = new ResizeObserver(() => {
        maxGanttPanelHeight.value = Math.max(160, (layoutContentRef.value?.clientHeight || 500) - 168)
        ganttPanelHeight.value = Math.min(ganttPanelHeight.value, maxGanttPanelHeight.value)
    })
    if (layoutContentRef.value) panelResizeObserver.observe(layoutContentRef.value)
    nextTick(() => {
        if (isDisposed) return
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
    panelResizeObserver?.disconnect()
    flushPendingGanttSubTableSettings()
    isDisposed = true
    trainModelPreparationVersion++
    // Invalidate every in-flight result before aborting; custom adapters may
    // still complete, and chained loaders must not recreate an unmounted scene.
    layoutLoadVersion++
    stationSchemeLoadVersion++
    operationPlanLoadVersion++
    stationRouteLoadVersion++
    stationRouteTimeLoadVersion++
    trainPlanLoadVersion++
    ganttSubTableLoadVersion++
    dataRequests.abort()
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

.layout3d-ratio-control {
    display: inline-flex;
    align-items: center;
    gap: 10px;
    min-width: 0;
    max-width: 100%;
}

.layout3d-ratio-slider {
    width: 130px;
    min-width: 70px;
}

.layout3d-ratio-value {
    flex: 0 0 48px;
    color: #4c5968;
    font-size: 12px;
    font-variant-numeric: tabular-nums;
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
    font-size: 13px;
    color: #1452a3;
}

.layout3d-plan-select {
    width: 210px;
}

.layout3d-train-select {
    width: 230px;
}

.layout3d-model-select {
    width: 180px;
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

.layout3d-gantt-panel {
    display: flex;
    flex: 0 0 260px;
    min-height: 160px;
    flex-direction: column;
    overflow: hidden;
    border-top: 1px solid var(--sy-border);
    background: #ffffff;
}

.layout3d-gantt-header {
    display: flex;
    flex-wrap: wrap;
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
    font-size: 13px;
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
    .layout3d-model-select,
    .layout3d-speed-select {
        width: 100%;
    }

    .layout3d-gantt-panel {
        flex-basis: 230px;
    }
}
</style>
