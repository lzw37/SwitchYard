<template>
    <section class="operation-simulation-page">
        <div class="simulation-toolbar">
            <div class="simulation-toolbar-left">
                <label class="simulation-toolbar-control">
                    <span>车站方案</span>
                    <el-select
                        v-model="currentStationSchemeId"
                        size="small"
                        filterable
                        class="simulation-select"
                        :loading="loadingStationSchemes"
                        :disabled="!selectedInstanceId || loadingStationSchemes"
                        placeholder="请选择车站方案"
                        @change="handleStationSchemeChange"
                    >
                        <el-option
                            v-for="option in stationSchemeOptions"
                            :key="option.id"
                            :label="formatStationSchemeLabel(option)"
                            :value="option.id"
                        />
                    </el-select>
                </label>
                <label class="simulation-toolbar-control">
                    <span>作业计划</span>
                    <el-select
                        v-model="currentOperationPlanId"
                        size="small"
                        filterable
                        class="simulation-select"
                        :loading="loadingOperationPlans"
                        :disabled="!currentStationSchemeId || loadingOperationPlans"
                        placeholder="请选择作业计划"
                        @change="handleOperationPlanChange"
                    >
                        <el-option
                            v-for="option in operationPlanOptions"
                            :key="option.operationPlanID"
                            :label="formatOperationPlanLabel(option)"
                            :value="option.operationPlanID"
                        />
                    </el-select>
                </label>
                <label class="simulation-toolbar-control">
                    <span>范围</span>
                    <el-radio-group
                        v-model="playbackMode"
                        size="small"
                        class="playback-mode-toggle"
                        @change="handlePlaybackModeChange"
                    >
                        <el-radio-button value="single">单列车</el-radio-button>
                        <el-radio-button value="all">全站</el-radio-button>
                    </el-radio-group>
                </label>
                <label class="simulation-toolbar-control">
                    <span>列车</span>
                    <el-select
                        v-model="selectedTrainId"
                        size="small"
                        filterable
                        class="simulation-select train-select"
                        :loading="loadingTrainOperationPlan"
                        :disabled="isAllTrainPlayback || trainOptions.length === 0 || loadingTrainOperationPlan"
                        :placeholder="isAllTrainPlayback ? '全站全部列车' : '请选择列车'"
                        @change="handleTrainChange"
                    >
                        <el-option
                            v-for="option in trainOptions"
                            :key="option.id"
                            :label="formatTrainLabel(option)"
                            :value="option.id"
                        />
                    </el-select>
                </label>
            </div>
            <div class="simulation-toolbar-right">
                <el-button
                    :icon="Refresh"
                    size="small"
                    :loading="loadingAnyData"
                    :disabled="!hasScheme"
                    @click="refreshSimulationData"
                >
                    刷新
                </el-button>
                <el-button
                    :icon="Aim"
                    size="small"
                    :disabled="!layoutData"
                    @click="fitLayoutToFullView"
                >
                    全图
                </el-button>
            </div>
        </div>

        <div class="simulation-body">
            <div ref="simulationLeftPanelRef" class="simulation-left-panel">
                <div
                    ref="layoutViewportRef"
                    class="simulation-layout-view"
                    v-loading="loadingLayout"
                >
                    <div v-if="layoutData" class="simulation-layout-stage" :style="simulationLayoutStageStyle">
                        <StationLayoutEditor
                            ref="layoutEditorRef"
                            readonly
                            :display-scale-x="layoutScaleX"
                            :display-scale-y="layoutScaleY"
                            :display-styles="layoutDisplayStyles"
                            :show-grid="false"
                            :show-nodes="true"
                            :show-curve-arc="true"
                            :grid-spacing="layoutGridSpacing"
                            :cells="layoutCells"
                            :show-cell-names="true"
                            :highlighted-route-node-ids="highlightedRouteNodeIds"
                            :highlighted-route-link-ids="highlightedRouteLinkIds"
                            :highlighted-route-arrow-node-ids="highlightedRouteArrowNodeIds"
                            :highlighted-route-color="highlightedRouteColor"
                            :highlighted-route-arrow-visible="highlightedRouteArrowVisible"
                        />
                        <svg
                            v-if="simulationOverlayVisible"
                            class="simulation-train-overlay"
                            :width="layoutViewportState.width"
                            :height="layoutViewportState.height"
                            :style="simulationOverlayStyle"
                        >
                            <g
                                v-for="(car, index) in simulationTrainCars"
                                :key="car.key"
                                class="simulation-train-car"
                                :transform="simulationTrainCarTransform(car)"
                            >
                                <rect
                                    class="simulation-train-car-body"
                                    :x="-simulationCarScreenLength(car) / 2"
                                    :y="-simulationCarScreenWidth(car) / 2"
                                    :width="simulationCarScreenLength(car)"
                                    :height="simulationCarScreenWidth(car)"
                                    rx="2"
                                    ry="2"
                                    :style="simulationTrainCarRectStyle(car)"
                                />
                                <line
                                    v-if="index === 0"
                                    class="simulation-train-car-head"
                                    :x1="simulationCarScreenLength(car) / 2 - 4"
                                    :y1="-simulationCarScreenWidth(car) / 2 + 2"
                                    :x2="simulationCarScreenLength(car) / 2 - 4"
                                    :y2="simulationCarScreenWidth(car) / 2 - 2"
                                />
                                <text
                                    v-if="car.label"
                                    class="simulation-train-car-label"
                                    text-anchor="middle"
                                    dominant-baseline="middle"
                                    :transform="`rotate(${-Number(car.angle || 0)})`"
                                >
                                    {{ car.label }}
                                </text>
                            </g>
                        </svg>
                    </div>
                    <div v-else class="simulation-layout-empty">
                        {{ layoutEmptyText }}
                    </div>
                </div>

                <div
                    class="simulation-horizontal-resizer"
                    role="separator"
                    aria-orientation="horizontal"
                    @pointerdown="startGanttPanelResize"
                    @dblclick="resetGanttPanelHeight"
                />

                <section class="simulation-gantt-panel" :style="simulationGanttPanelStyle">
                    <div class="simulation-gantt-header">
                        <div class="simulation-gantt-title">
                            <h3>计划甘特图</h3>
                            <span>{{ ganttSummaryText }}</span>
                        </div>
                        <div class="simulation-gantt-subtable-toolbar">
                            <el-tabs
                                v-model="activeGanttSubTableId"
                                type="card"
                                class="simulation-gantt-sub-tabs"
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
                            <div class="simulation-gantt-subtable-actions">
                                <span class="simulation-gantt-subtable-summary">
                                    {{ activeGanttSubTableSummaryText }}
                                </span>
                                <el-button
                                    :icon="Edit"
                                    circle
                                    size="small"
                                    :disabled="!activeGanttSubTable"
                                    title="编辑子表"
                                    @click="openEditGanttSubTableDialog"
                                />
                                <el-button
                                    :icon="Plus"
                                    circle
                                    size="small"
                                    title="新增子表"
                                    @click="openCreateGanttSubTableDialog"
                                />
                            </div>
                        </div>
                    </div>
                    <div
                        v-if="ganttLanes.length > 0"
                        ref="ganttViewportRef"
                        class="simulation-gantt-viewport"
                    >
                        <div class="simulation-gantt-content" :style="ganttContentStyle">
                            <div class="simulation-gantt-axis-row">
                                <div class="simulation-gantt-axis-label">轨道电路区段</div>
                                <div class="simulation-gantt-axis-track" :style="ganttTimelineStyle">
                                    <div
                                        v-for="tick in ganttTicks"
                                        :key="tick.key"
                                        class="simulation-gantt-axis-tick"
                                        :class="{ 'is-major': tick.major }"
                                        :style="getGanttTickStyle(tick)"
                                    >
                                        <span>{{ tick.label }}</span>
                                    </div>
                                    <div class="simulation-gantt-now-line" :style="ganttPlayheadStyle" />
                                </div>
                            </div>
                            <div
                                v-for="lane in ganttLanes"
                                :key="lane.key"
                                class="simulation-gantt-lane-row"
                            >
                                <div class="simulation-gantt-lane-label" :title="lane.label">
                                    {{ lane.label }}
                                </div>
                                <div class="simulation-gantt-lane-track" :style="ganttTimelineStyle">
                                    <div
                                        v-for="tick in ganttTicks"
                                        :key="`${lane.key}-${tick.key}`"
                                        class="simulation-gantt-grid-line"
                                        :class="{ 'is-major': tick.major }"
                                        :style="getGanttTickStyle(tick)"
                                    />
                                    <div
                                        v-for="block in lane.blocks"
                                        :key="block.key"
                                        class="simulation-gantt-block"
                                        :class="getGanttBlockClassName(block)"
                                        :style="getGanttBlockStyle(block)"
                                        :title="block.title"
                                    >
                                        <span>{{ block.label }}</span>
                                    </div>
                                    <div class="simulation-gantt-now-line" :style="ganttPlayheadStyle" />
                                </div>
                            </div>
                        </div>
                    </div>
                    <div v-else class="simulation-gantt-empty">
                        {{ ganttEmptyText }}
                    </div>
                </section>

                <el-dialog
                    v-model="ganttSubTableDialogVisible"
                    :title="ganttSubTableDialogTitle"
                    width="560px"
                    class="simulation-gantt-subtable-dialog"
                >
                    <el-form label-position="top">
                        <el-form-item label="子表名称">
                            <el-input
                                v-model="ganttSubTableDialogForm.name"
                                maxlength="100"
                                show-word-limit
                                placeholder="请输入子表名称"
                            />
                        </el-form-item>
                        <el-form-item label="显示轨道电路区段">
                            <el-select
                                v-model="ganttSubTableDialogForm.cellIds"
                                class="simulation-gantt-subtable-cell-select"
                                multiple
                                filterable
                                clearable
                                collapse-tags
                                collapse-tags-tooltip
                                placeholder="请选择要显示的轨道电路区段"
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
                            取消
                        </el-button>
                        <el-button type="primary" @click="confirmGanttSubTableDialog">
                            确认
                        </el-button>
                    </template>
                </el-dialog>
            </div>

            <aside class="simulation-side-panel">
                <header class="simulation-panel-header">
                    <div>
                        <h2>仿真播放</h2>
                        <span>{{ playbackSummaryText }}</span>
                    </div>
                    <el-tag size="small" :type="playbackStatusTagType">
                        {{ playbackStatusText }}
                    </el-tag>
                </header>

                <div class="simulation-controls">
                    <div class="simulation-control-buttons">
                        <el-button
                            :icon="RefreshLeft"
                            circle
                            size="small"
                            :disabled="!canPlayback"
                            title="重置"
                            @click="resetPlayback"
                        />
                        <el-button
                            :icon="isPlaying ? VideoPause : VideoPlay"
                            type="primary"
                            circle
                            size="small"
                            :disabled="!canPlayback"
                            :title="isPlaying ? '暂停' : '播放'"
                            @click="togglePlayback"
                        />
                        <span class="simulation-clock">{{ playbackClockText }}</span>
                    </div>
                    <el-slider
                        :model-value="playheadSeconds"
                        :min="0"
                        :max="playbackSliderMax"
                        :step="0.1"
                        :disabled="!canPlayback"
                        :format-tooltip="formatPlayheadTooltip"
                        @input="handlePlayheadInput"
                    />
                    <div class="simulation-speed-row">
                        <span>速度</span>
                        <el-select
                            v-model="playbackSpeed"
                            size="small"
                            class="simulation-speed-select"
                            :disabled="!canPlayback"
                        >
                            <el-option :value="1" label="1x" />
                            <el-option :value="10" label="10x" />
                            <el-option :value="60" label="60x" />
                            <el-option :value="180" label="180x" />
                            <el-option :value="300" label="300x" />
                        </el-select>
                    </div>
                </div>

                <section class="simulation-route-status">
                    <div class="simulation-status-grid">
                        <div>
                            <span>当前进路</span>
                            <strong>{{ activeRouteName }}</strong>
                        </div>
                        <div>
                            <span>阶段</span>
                            <strong>{{ activePhaseText }}</strong>
                        </div>
                        <div>
                            <span>进度</span>
                            <strong>{{ activeRouteProgressText }}</strong>
                        </div>
                        <div>
                            <span>编组</span>
                            <strong>8 节</strong>
                        </div>
                    </div>
                </section>

                <section class="simulation-table-section">
                    <div class="simulation-table-header">
                        <h3>作业序列</h3>
                        <span>{{ routeRuns.length }} 条进路</span>
                    </div>
                    <el-table
                        ref="movementTableRef"
                        class="simulation-movement-table"
                        :data="simulationRows"
                        size="small"
                        height="100%"
                        border
                        highlight-current-row
                        :row-class-name="getSimulationRowClassName"
                        :empty-text="movementTableEmptyText"
                    >
                        <el-table-column label="#" width="46" align="center">
                            <template #default="{ row }">
                                {{ row.index + 1 }}
                            </template>
                        </el-table-column>
                        <el-table-column label="列车" width="92" show-overflow-tooltip>
                            <template #default="{ row }">
                                {{ row.trainName }}
                            </template>
                        </el-table-column>
                        <el-table-column label="进路" min-width="150" show-overflow-tooltip>
                            <template #default="{ row }">
                                {{ row.routeName }}
                            </template>
                        </el-table-column>
                        <el-table-column label="时间" width="136" show-overflow-tooltip>
                            <template #default="{ row }">
                                {{ row.timeText }}
                            </template>
                        </el-table-column>
                        <el-table-column label="状态" width="82" align="center">
                            <template #default="{ row }">
                                <el-tag size="small" :type="row.statusType">
                                    {{ row.statusText }}
                                </el-tag>
                            </template>
                        </el-table-column>
                    </el-table>
                </section>
            </aside>
        </div>
    </section>
</template>

<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, ref, watch } from 'vue'
import { Aim, Edit, Plus, Refresh, RefreshLeft, VideoPause, VideoPlay } from '@element-plus/icons-vue'
import { ElMessage } from 'element-plus'
import axios from '@/utils/axios'
import StationLayoutEditor from './components/StationLayoutEditor.vue'

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

interface LayoutNode {
    id: string
    x: number
    y: number
}

interface LayoutTrack {
    id: string
    x1: number
    y1: number
    x2: number
    y2: number
    fromNodeID: string
    toNodeID: string
}

interface LayoutCell {
    id: string
    name: string
    linkIDList: string
}

interface Point {
    x: number
    y: number
    nodeId?: string
}

interface PathSegment {
    from: Point
    to: Point
    length: number
    startDistance: number
    angle: number
}

interface PolylinePath {
    points: Point[]
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

interface CanvasViewportState {
    minX: number
    minY: number
    maxX: number
    maxY: number
    width: number
    height: number
    scaleX: number
    scaleY: number
}

type RunPhase = 'waiting' | 'locking' | 'moving' | 'finished'
type PlaybackMode = 'single' | 'all'

interface RouteRunSource {
    train: TrainOperationPlanTrain
    movement: TrainOperationPlanMovement
    sourceIndex: number
}

interface SimulationRow {
    index: number
    key: string
    phase: RunPhase
    trainName: string
    routeName: string
    timeText: string
    statusText: string
    statusType: 'success' | 'warning' | 'info' | 'primary'
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

const props = defineProps<{
    selectedInstanceId: string
}>()

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
const defaultGanttPanelHeight = 260
const minGanttPanelHeight = 160
const minLayoutPanelHeight = 220
const horizontalResizerHeight = 12

function createEmptyCanvasViewportState(): CanvasViewportState {
    return {
        minX: 0,
        minY: 0,
        maxX: 0,
        maxY: 0,
        width: 0,
        height: 0,
        scaleX: 1,
        scaleY: 1,
    }
}

const currentStationSchemeId = ref('')
const currentOperationPlanId = ref('')
const selectedTrainId = ref('')
const stationSchemeOptions = ref<StationSchemeOption[]>([])
const operationPlanOptions = ref<OperationPlanOption[]>([])
const stationRouteOptions = ref<StationRouteOption[]>([])
const stationRouteTimesByKey = ref<Record<string, StationRouteTimeOption[]>>({})
const trainOperationPlanTrains = ref<TrainOperationPlanTrain[]>([])
const trainOperationPlanMovements = ref<TrainOperationPlanMovement[]>([])
const layoutData = ref<Record<string, unknown> | null>(null)
const layoutDisplayStyles = ref<Record<string, unknown>>({})
const layoutCells = ref<LayoutCell[]>([])
const layoutGridSpacing = ref(20)
const layoutScaleX = ref(1)
const layoutScaleY = ref(1)
const layoutEditorRef = ref<any | null>(null)
const simulationLeftPanelRef = ref<HTMLElement | null>(null)
const layoutViewportRef = ref<HTMLElement | null>(null)
const movementTableRef = ref<any | null>(null)
const ganttViewportRef = ref<HTMLElement | null>(null)
const layoutViewportState = ref<CanvasViewportState>(createEmptyCanvasViewportState())
const loadingStationSchemes = ref(false)
const loadingOperationPlans = ref(false)
const loadingStationRoutes = ref(false)
const loadingStationRouteTimes = ref(false)
const loadingTrainOperationPlan = ref(false)
const loadingLayout = ref(false)
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
const ganttPanelHeight = ref(defaultGanttPanelHeight)
const ganttSubTableSequence = ref(ganttDefaultSubTableCount)
const ganttSubTables = ref<GanttSubTable[]>(
    Array.from({ length: ganttDefaultSubTableCount }, (_, index) => createGanttSubTable(index + 1)),
)
const activeGanttSubTableId = ref(ganttSubTables.value[0]?.id || '')
const loadingGanttSubTableSettings = ref(false)
const savingGanttSubTableSettings = ref(false)
const ganttSubTableDialogVisible = ref(false)
const ganttSubTableDialogMode = ref<'create' | 'edit'>('create')
const ganttSubTableDialogTargetId = ref('')
const ganttSubTableDialogTargetSequence = ref(0)
const ganttSubTableDialogForm = ref<GanttSubTableDialogForm>({
    name: '',
    cellIds: [],
})

let stationSchemeLoadVersion = 0
let operationPlanLoadVersion = 0
let stationRouteLoadVersion = 0
let stationRouteTimeLoadVersion = 0
let trainPlanLoadVersion = 0
let layoutLoadVersion = 0
let ganttSubTableLoadVersion = 0
let animationFrameId: number | null = null
let tableScrollFrameId: number | null = null
let ganttScrollFrameId: number | null = null
let ganttResizeState: { pointerStartY: number; startHeight: number } | null = null
let ganttSubTableSaveTimer: ReturnType<typeof window.setTimeout> | null = null
let suppressGanttSubTableSave = false
let ganttSubTableSaveRevision = 0
let previousBodyCursor = ''
let previousBodyUserSelect = ''
let lastAnimationTimestamp = 0
let lastPlaybackRenderTimestamp = 0
let playbackRuntimeSeconds = 0
const trainCarAngleMemory = new Map<string, number>()

const hasScheme = computed(() => Boolean(props.selectedInstanceId && currentStationSchemeId.value.trim()))
const hasScope = computed(() => Boolean(hasScheme.value && currentOperationPlanId.value.trim()))
const loadingAnyData = computed(() => (
    loadingStationSchemes.value ||
    loadingOperationPlans.value ||
    loadingStationRoutes.value ||
    loadingStationRouteTimes.value ||
    loadingTrainOperationPlan.value ||
    loadingGanttSubTableSettings.value ||
    savingGanttSubTableSettings.value ||
    loadingLayout.value
))
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
    const map = new Map<string, LayoutNode>()
    getLayoutNodes(layoutData.value).forEach((node) => map.set(node.id, node))
    return map
})
const layoutTrackMap = computed(() => {
    const map = new Map<string, LayoutTrack>()
    getLayoutTracks(layoutData.value).forEach((track) => map.set(track.id, track))
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
const simulationDurationSeconds = computed(() => {
    return routeRuns.value.reduce((maxSeconds, run) => Math.max(maxSeconds, run.endSeconds), 0)
})
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
    '--simulation-gantt-sidebar-width': `${ganttSidebarWidth}px`,
}))
const simulationGanttPanelStyle = computed(() => ({
    flexBasis: `${ganttPanelHeight.value}px`,
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
    if (routeRuns.value.length === 0) return '暂无可播放作业'
    const blockCount = ganttLanes.value.reduce((count, lane) => count + lane.blocks.length, 0)
    return `${ganttLanes.value.length} 个区段 · ${blockCount} 条占用`
})
const activeGanttSubTableSummaryText = computed(() => (
    `${activeGanttSubTableCells.value.length}/${ganttAvailableCells.value.length} 个轨道电路区段`
))
const ganttSubTableDialogTitle = computed(() => (
    ganttSubTableDialogMode.value === 'create' ? '新建甘特图子表' : '编辑甘特图子表'
))
const ganttEmptyText = computed(() => {
    if (routeRuns.value.length === 0) return movementTableEmptyText.value
    if (ganttAvailableCells.value.length > 0 && activeGanttSubTableCells.value.length === 0) {
        return '当前子表没有选择轨道电路区段'
    }
    return '当前计划没有可显示的轨道电路区段占用'
})
const activeRun = computed(() => {
    const runs = routeRuns.value
    if (runs.length === 0) return null
    return runs[activeRunIndex.value] || runs[0] || null
})
const highlightedRouteRuns = computed(() => activeRunIndices.value
    .map((index) => routeRuns.value[index] || null)
    .filter((run): run is RouteRun => run !== null))
const activePhase = computed(() => activeRunPhase.value)
const activeRouteProgress = computed(() => getActiveRouteProgress(activeRun.value, playheadSeconds.value))
const highlightedRouteNodeIds = computed(() => normalizeUniqueStrings(highlightedRouteRuns.value.flatMap((run) => run.nodeIds)))
const highlightedRouteLinkIds = computed(() => normalizeUniqueStrings(highlightedRouteRuns.value.flatMap((run) => run.linkIds)))
const highlightedRouteArrowNodeIds = computed(() => normalizeUniqueStrings(highlightedRouteRuns.value.flatMap((run) => run.nodeIds)))
const highlightedRouteColor = computed(() => isAllTrainPlayback.value ? '#fbbf24' : activeRun.value?.color || '#ffd600')
const highlightedRouteArrowVisible = computed(() => highlightedRouteArrowNodeIds.value.length >= 2)
const simulationTrainCars = computed<SimulationTrainCar[]>(() => buildSimulationTrainCars())
const simulationOverlayVisible = computed(() => (
    layoutViewportState.value.width > 0 &&
    layoutViewportState.value.height > 0 &&
    simulationTrainCars.value.length > 0
))
const simulationLayoutStageStyle = computed(() => {
    if (layoutViewportState.value.width <= 0 || layoutViewportState.value.height <= 0) return {}
    return {
        width: `${layoutViewportState.value.width}px`,
        height: `${layoutViewportState.value.height}px`,
    }
})
const simulationOverlayStyle = computed(() => ({
    width: `${layoutViewportState.value.width}px`,
    height: `${layoutViewportState.value.height}px`,
}))
const activeRouteName = computed(() => {
    if (isAllTrainPlayback.value) {
        const activeCount = activeRunIndices.value.length
        if (activeCount > 0) return `${activeCount} 条进路进行中`
        return routeRuns.value.length > 0 ? '等待下一项作业' : '-'
    }
    return activeRun.value ? getRouteDisplayName(activeRun.value.route.id) : '-'
})
const activePhaseText = computed(() => {
    if (isAllTrainPlayback.value) {
        const locking = activeLockingRunCount.value
        const moving = activeMovingRunCount.value
        if (locking + moving <= 0) return '等待'
        return `办理 ${locking} / 走行 ${moving}`
    }
    if (!activeRun.value) return '待选择'
    if (activePhase.value === 'locking') return '办理进路'
    if (activePhase.value === 'moving') return '列车走行'
    if (activePhase.value === 'finished') return '已完成'
    return '等待'
})
const finishedRunCount = computed(() => routeRuns.value.filter((run) => runPhaseByKey.value[run.key] === 'finished').length)
const activeRouteProgressText = computed(() => {
    if (isAllTrainPlayback.value) return `${finishedRunCount.value}/${routeRuns.value.length}`
    return `${Math.round(activeRouteProgress.value * 100)}%`
})
const playbackStatusText = computed(() => {
    if (!canPlayback.value) return '未就绪'
    if (isPlaying.value) return '播放中'
    if (playheadSeconds.value >= simulationDurationSeconds.value) return '已结束'
    return '已暂停'
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
        return `${trainOptions.value.length} 列车 · ${routeRuns.value.length} 条有效进路`
    }
    const train = selectedTrain.value
    if (!train) return '请选择一列列车'
    return `${formatTrainLabel(train)} · ${selectedTrainMovements.value.length} 个作业`
})
const layoutEmptyText = computed(() => {
    if (!hasScheme.value) return '请选择车站方案'
    if (loadingLayout.value) return '正在加载布置图'
    return '当前方案没有可显示的布置图'
})
const movementTableEmptyText = computed(() => {
    if (isAllTrainPlayback.value) return '当前作业计划没有包含可解析开始/结束时间的可播放进路'
    if (!selectedTrain.value) return '请选择列车'
    if (selectedTrainMovements.value.length === 0) return '当前列车没有作业计划'
    return '当前列车没有可播放的进路'
})
const simulationRows = computed<SimulationRow[]>(() => routeRuns.value.map((run, index) => {
    const phase = getCachedRunPhase(index)
    return {
        index,
        key: run.key,
        phase,
        trainName: formatTrainLabel(run.train),
        routeName: getRouteDisplayName(run.route.id),
        timeText: getRunTimeText(run),
        statusText: getRunStatusText(phase),
        statusType: getRunStatusType(phase),
    }
}))

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
        '--simulation-gantt-block-color': block.color,
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
    return `子表 ${index}`
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
        ElMessage.warning('请输入子表名称')
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

function readString(source: unknown, ...keys: string[]) {
    const record = source && typeof source === 'object' ? source as Record<string, unknown> : {}
    for (const key of keys) {
        const value = record[key]
        if (value !== undefined && value !== null) return String(value)
    }
    return ''
}

function readArray(source: unknown, ...keys: string[]) {
    const record = source && typeof source === 'object' ? source as Record<string, unknown> : {}
    for (const key of keys) {
        const value = record[key]
        if (Array.isArray(value)) return value
    }
    return []
}

function readOptionalInteger(source: unknown, ...keys: string[]): number | null {
    const record = source && typeof source === 'object' ? source as Record<string, unknown> : {}
    for (const key of keys) {
        const value = record[key]
        if (value === undefined || value === null || value === '') continue
        const parsed = Number(value)
        if (Number.isFinite(parsed)) return Math.trunc(parsed)
    }
    return null
}

function readBoolean(source: unknown, defaultValue: boolean, ...keys: string[]) {
    const record = source && typeof source === 'object' ? source as Record<string, unknown> : {}
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

function normalizeStationSchemeOption(item: unknown): StationSchemeOption | null {
    const id = readString(item, 'id', 'ID').trim()
    if (!id) return null
    return {
        id,
        name: readString(item, 'name', 'Name').trim() || id,
    }
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
    const record = data && typeof data === 'object' ? data as Record<string, unknown> : {}
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

function getLayoutDisplayStyles(data: unknown): Record<string, unknown> {
    const metadata = readRecord(readRecord(data).metadata)
    const styles = metadata.displayStyles
    return styles && typeof styles === 'object' && !Array.isArray(styles) ? styles as Record<string, unknown> : {}
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

function getLayoutNodes(data: unknown): LayoutNode[] {
    const nodes = Array.isArray(readRecord(data).nodes) ? readRecord(data).nodes as unknown[] : []
    return nodes
        .map((node) => {
            const id = readString(node, 'id', 'ID').trim()
            const x = Number(readRecord(node).x ?? readRecord(node).X)
            const y = Number(readRecord(node).y ?? readRecord(node).Y)
            return { id, x, y }
        })
        .filter((node) => node.id && Number.isFinite(node.x) && Number.isFinite(node.y))
}

function getLayoutTracks(data: unknown): LayoutTrack[] {
    const tracks = Array.isArray(readRecord(data).tracks) ? readRecord(data).tracks as unknown[] : []
    return tracks
        .map((track) => {
            const record = readRecord(track)
            const id = readString(track, 'id', 'ID').trim()
            return {
                id,
                x1: Number(record.x1 ?? record.X1),
                y1: Number(record.y1 ?? record.Y1),
                x2: Number(record.x2 ?? record.X2),
                y2: Number(record.y2 ?? record.Y2),
                fromNodeID: readString(track, 'fromNodeID', 'FromNodeID').trim(),
                toNodeID: readString(track, 'toNodeID', 'ToNodeID').trim(),
            }
        })
        .filter((track) => (
            track.id &&
            Number.isFinite(track.x1) &&
            Number.isFinite(track.y1) &&
            Number.isFinite(track.x2) &&
            Number.isFinite(track.y2)
        ))
}

function readRecord(value: unknown): Record<string, unknown> {
    return value && typeof value === 'object' && !Array.isArray(value) ? value as Record<string, unknown> : {}
}

function formatStationSchemeLabel(option: StationSchemeOption) {
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
        .filter((point): point is Point => point !== null)

    if (points.length < 2) {
        points = buildPointsFromLinks(route, linkIds)
    }
    if (points.length < 2) {
        points = [pointFromNodeId(route.startNodeID), pointFromNodeId(route.endNodeID)]
            .filter((point): point is Point => point !== null)
    }

    return {
        path: buildPolylinePath(points),
        nodeIds,
        linkIds,
    }
}

function pointFromNodeId(nodeId: string): Point | null {
    const id = String(nodeId || '').trim()
    if (!id) return null
    const node = layoutNodeMap.value.get(id)
    if (!node) return null
    return { x: node.x, y: node.y, nodeId: id }
}

function buildPointsFromLinks(route: StationRouteOption, linkIds: string[]): Point[] {
    const points: Point[] = []
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

function getTrackEndpoints(track: LayoutTrack): [Point, Point] | null {
    const fromNode = pointFromNodeId(track.fromNodeID)
    const toNode = pointFromNodeId(track.toNodeID)
    const fromPoint = fromNode || { x: track.x1, y: track.y1, nodeId: track.fromNodeID || undefined }
    const toPoint = toNode || { x: track.x2, y: track.y2, nodeId: track.toNodeID || undefined }
    if (!Number.isFinite(fromPoint.x) || !Number.isFinite(fromPoint.y) || !Number.isFinite(toPoint.x) || !Number.isFinite(toPoint.y)) {
        return null
    }
    return [fromPoint, toPoint]
}

function appendDistinctPoint(points: Point[], point: Point) {
    const previous = points[points.length - 1]
    if (previous && getPointDistance(previous, point) < 0.001) return
    points.push(point)
}

function buildPolylinePath(points: Point[]): PolylinePath {
    const normalizedPoints: Point[] = []
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

function getPointDistance(left: Point, right: Point) {
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

function getCachedRunPhase(index: number): RunPhase {
    const run = routeRuns.value[index]
    if (!run) return 'waiting'
    return runPhaseByKey.value[run.key] || 'waiting'
}

function getActiveRouteProgress(run: RouteRun | null, currentSeconds: number) {
    if (!run) return 0
    const moveStart = run.startSeconds + run.lockSeconds
    const moveDuration = Math.max(0.1, run.endSeconds - moveStart)
    if (currentSeconds <= moveStart) return 0
    return Math.max(0, Math.min(1, (currentSeconds - moveStart) / moveDuration))
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

function simulationScreenX(value: number) {
    return (Number(value || 0) - layoutViewportState.value.minX) * layoutViewportState.value.scaleX
}

function simulationScreenY(value: number) {
    return (Number(value || 0) - layoutViewportState.value.minY) * layoutViewportState.value.scaleY
}

function formatSvgNumber(value: number) {
    const parsed = Number(value)
    if (!Number.isFinite(parsed)) return '0'
    return Number(parsed.toFixed(3)).toString()
}

function simulationCarScreenLength(car: SimulationTrainCar) {
    const length = Math.max(1, Number(car.length || 0))
    return Math.max(8, length * layoutViewportState.value.scaleX)
}

function simulationCarScreenWidth(car: SimulationTrainCar) {
    const width = Math.max(1, Number(car.width || 0))
    return Math.max(4, width * layoutViewportState.value.scaleY)
}

function simulationTrainCarTransform(car: SimulationTrainCar) {
    const x = simulationScreenX(car.x)
    const y = simulationScreenY(car.y)
    const angle = Number.isFinite(Number(car.angle)) ? Number(car.angle) : 0
    return `translate(${formatSvgNumber(x)},${formatSvgNumber(y)}) rotate(${formatSvgNumber(angle)})`
}

function simulationTrainCarRectStyle(car: SimulationTrainCar) {
    return {
        fill: car.fill || '#2563eb',
        stroke: car.stroke || '#eff6ff',
    }
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

function getRunTimeText(run: RouteRun) {
    if (run.usesPlanTime) {
        return `${formatClockSeconds(run.absoluteStartSeconds)} - ${formatClockSeconds(run.absoluteEndSeconds)}`
    }
    return `${formatDurationSeconds(run.startSeconds)} - ${formatDurationSeconds(run.endSeconds)}`
}

function getRunStatusText(phase: ReturnType<typeof getRunPhase>) {
    if (phase === 'locking') return '办理'
    if (phase === 'moving') return '走行'
    if (phase === 'finished') return '完成'
    return '待办'
}

function getRunStatusType(phase: ReturnType<typeof getRunPhase>): SimulationRow['statusType'] {
    if (phase === 'locking') return 'warning'
    if (phase === 'moving') return 'primary'
    if (phase === 'finished') return 'success'
    return 'info'
}

function getSimulationRowClassName({ row }: { row: SimulationRow }) {
    const classes: string[] = []
    if (row.index === getSimulationTableCurrentRunIndex()) {
        classes.push('simulation-current-row')
    }
    const isActiveRun = activeRunIndices.value.some((index) => routeRuns.value[index]?.key === row.key)
    if (isActiveRun && isRunProcessingPhase(row.phase)) {
        classes.push('simulation-active-row')
    }
    return classes.join(' ')
}

function isRunProcessingPhase(phase: RunPhase) {
    return phase === 'locking' || phase === 'moving'
}

function getSimulationTableCurrentRunIndex() {
    const activeIndex = activeRunIndices.value.find((index) => index >= 0 && isRunProcessingPhase(getCachedRunPhase(index)))
    if (activeIndex !== undefined) return activeIndex
    const nextRunIndex = routeRuns.value.findIndex((run) => playheadSeconds.value < run.endSeconds)
    if (nextRunIndex >= 0) return nextRunIndex
    return routeRuns.value.length - 1
}

function scheduleScrollSimulationTableToCurrentRun() {
    if (tableScrollFrameId !== null) {
        window.cancelAnimationFrame(tableScrollFrameId)
    }
    tableScrollFrameId = window.requestAnimationFrame(() => {
        tableScrollFrameId = null
        void nextTick(() => {
            scrollSimulationTableToCurrentRun()
        })
    })
}

function scrollSimulationTableToCurrentRun() {
    const rowIndex = getSimulationTableCurrentRunIndex()
    const row = simulationRows.value[rowIndex]
    if (rowIndex < 0 || !row) return

    const table = movementTableRef.value
    table?.setCurrentRow?.(row)

    const tableRoot = table?.$el as HTMLElement | undefined
    const scrollContainer = tableRoot?.querySelector('.el-scrollbar__wrap') as HTMLElement | null
    const fallbackTop = rowIndex * 34 - 96
    if (!tableRoot || !scrollContainer) {
        setSimulationTableScrollTop(scrollContainer, fallbackTop)
        return
    }

    const rowElement = tableRoot.querySelector(`.el-table__body tbody tr:nth-child(${rowIndex + 1})`) as HTMLElement | null
    if (!rowElement) {
        setSimulationTableScrollTop(scrollContainer, fallbackTop)
        return
    }

    const containerRect = scrollContainer.getBoundingClientRect()
    const rowRect = rowElement.getBoundingClientRect()
    const rowTop = scrollContainer.scrollTop + rowRect.top - containerRect.top
    const rowBottom = rowTop + rowRect.height
    const viewTop = scrollContainer.scrollTop
    const viewBottom = viewTop + scrollContainer.clientHeight
    const margin = Math.min(96, Math.max(40, scrollContainer.clientHeight * 0.25))

    if (rowTop < viewTop + margin) {
        setSimulationTableScrollTop(scrollContainer, rowTop - margin)
        return
    }
    if (rowBottom > viewBottom - margin) {
        setSimulationTableScrollTop(scrollContainer, rowBottom - scrollContainer.clientHeight + margin)
    }
}

function setSimulationTableScrollTop(scrollContainer: HTMLElement | null, top: number) {
    const normalizedTop = Math.max(0, top)
    const table = movementTableRef.value
    table?.setScrollTop?.(normalizedTop)
    if (scrollContainer) {
        scrollContainer.scrollTop = normalizedTop
    }
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
    const targetLeft = Math.max(0, playheadContentLeft - viewport.clientWidth * 0.45)
    viewport.scrollLeft = targetLeft
}

function startGanttPanelResize(event: PointerEvent) {
    event.preventDefault()
    ganttResizeState = {
        pointerStartY: event.clientY,
        startHeight: ganttPanelHeight.value,
    }
    previousBodyCursor = document.body.style.cursor
    previousBodyUserSelect = document.body.style.userSelect
    document.body.style.cursor = 'row-resize'
    document.body.style.userSelect = 'none'
    window.addEventListener('pointermove', handleGanttPanelResize)
    window.addEventListener('pointerup', stopGanttPanelResize)
    window.addEventListener('pointercancel', stopGanttPanelResize)
}

function handleGanttPanelResize(event: PointerEvent) {
    if (!ganttResizeState) return
    const deltaY = event.clientY - ganttResizeState.pointerStartY
    const maxHeight = getMaxGanttPanelHeight()
    ganttPanelHeight.value = clampNumber(
        ganttResizeState.startHeight - deltaY,
        minGanttPanelHeight,
        maxHeight,
    )
}

function stopGanttPanelResize() {
    if (!ganttResizeState) return
    ganttResizeState = null
    window.removeEventListener('pointermove', handleGanttPanelResize)
    window.removeEventListener('pointerup', stopGanttPanelResize)
    window.removeEventListener('pointercancel', stopGanttPanelResize)
    document.body.style.cursor = previousBodyCursor
    document.body.style.userSelect = previousBodyUserSelect
}

function resetGanttPanelHeight() {
    ganttPanelHeight.value = clampNumber(defaultGanttPanelHeight, minGanttPanelHeight, getMaxGanttPanelHeight())
}

function getMaxGanttPanelHeight() {
    const panelHeight = simulationLeftPanelRef.value?.clientHeight || 0
    if (panelHeight <= 0) return defaultGanttPanelHeight * 2
    return Math.max(
        minGanttPanelHeight,
        panelHeight - minLayoutPanelHeight - horizontalResizerHeight,
    )
}

function clampNumber(value: number, min: number, max: number) {
    return Math.max(min, Math.min(max, value))
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
    lastAnimationTimestamp = 0
    lastPlaybackRenderTimestamp = 0
    animationFrameId = window.requestAnimationFrame(stepPlayback)
}

function pausePlayback() {
    isPlaying.value = false
    if (animationFrameId !== null) {
        window.cancelAnimationFrame(animationFrameId)
        animationFrameId = null
    }
}

function resetPlayback() {
    pausePlayback()
    clearTrainCarAngleMemory()
    setPlayheadSeconds(0)
}

function stepPlayback(timestamp: number) {
    if (!isPlaying.value) return
    if (!lastAnimationTimestamp) lastAnimationTimestamp = timestamp
    const deltaSeconds = Math.max(0, (timestamp - lastAnimationTimestamp) / 1000)
    lastAnimationTimestamp = timestamp
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
    animationFrameId = window.requestAnimationFrame(stepPlayback)
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
    layoutData.value = null
    layoutDisplayStyles.value = {}
    layoutCells.value = []
    layoutGridSpacing.value = 20
    layoutScaleX.value = 1
    layoutScaleY.value = 1
    layoutViewportState.value = createEmptyCanvasViewportState()
    layoutEditorRef.value?.clearElements?.()
}

async function loadStationSchemes() {
    const instanceID = props.selectedInstanceId
    if (!instanceID) {
        stationSchemeLoadVersion++
        stationSchemeOptions.value = []
        currentStationSchemeId.value = ''
        clearOperationPlans()
        clearStationRoutes()
        clearLayout()
        return
    }

    const loadVersion = ++stationSchemeLoadVersion
    loadingStationSchemes.value = true
    try {
        const response = await axios.get('/StationLayout/GetStationSchemes', { params: { instanceID } })
        if (loadVersion !== stationSchemeLoadVersion || instanceID !== props.selectedInstanceId) return
        const previousId = currentStationSchemeId.value
        stationSchemeOptions.value = (Array.isArray(response.data) ? response.data : [])
            .map(normalizeStationSchemeOption)
            .filter((item): item is StationSchemeOption => item !== null)
        currentStationSchemeId.value = stationSchemeOptions.value.some((item) => item.id === previousId)
            ? previousId
            : stationSchemeOptions.value[0]?.id || ''
        await loadOperationPlans()
        await refreshSimulationData()
    } catch (error) {
        if (loadVersion !== stationSchemeLoadVersion || instanceID !== props.selectedInstanceId) return
        console.error('Failed to load simulation station schemes:', error)
        stationSchemeOptions.value = []
        currentStationSchemeId.value = ''
        clearOperationPlans()
        clearStationRoutes()
        clearLayout()
        ElMessage.error('加载车站方案失败')
    } finally {
        if (loadVersion === stationSchemeLoadVersion) loadingStationSchemes.value = false
    }
}

async function loadOperationPlans() {
    const instanceID = props.selectedInstanceId
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
            instanceID !== props.selectedInstanceId ||
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
        console.error('Failed to load simulation operation plans:', error)
        clearOperationPlans()
        ElMessage.error('加载作业计划失败')
    } finally {
        if (loadVersion === operationPlanLoadVersion) loadingOperationPlans.value = false
    }
}

async function loadStationRoutes() {
    const instanceID = props.selectedInstanceId
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
            instanceID !== props.selectedInstanceId ||
            stationSchemeID !== currentStationSchemeId.value.trim()
        ) {
            return
        }
        stationRouteOptions.value = (Array.isArray(response.data) ? response.data : [])
            .map(normalizeStationRouteOption)
            .filter((item): item is StationRouteOption => item !== null)
    } catch (error) {
        if (loadVersion !== stationRouteLoadVersion) return
        console.error('Failed to load simulation station routes:', error)
        stationRouteOptions.value = []
        ElMessage.error('加载进路失败')
    } finally {
        if (loadVersion === stationRouteLoadVersion) loadingStationRoutes.value = false
    }
}

async function loadTrainOperationPlan() {
    const instanceID = props.selectedInstanceId
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
            instanceID !== props.selectedInstanceId ||
            stationSchemeID !== currentStationSchemeId.value.trim() ||
            operationPlanID !== currentOperationPlanId.value.trim()
        ) {
            return
        }
        normalizeTrainOperationPlanResponse(response.data)
        stopPlaybackForReload()
    } catch (error) {
        if (loadVersion !== trainPlanLoadVersion) return
        console.error('Failed to load simulation train operation plan:', error)
        clearTrainPlan()
        ElMessage.error('加载列车作业计划失败')
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
    const instanceID = props.selectedInstanceId
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
            instanceID !== props.selectedInstanceId ||
            stationSchemeID !== currentStationSchemeId.value.trim()
        ) {
            return
        }
        stationRouteTimesByKey.value = Object.fromEntries(entries)
    } catch (error) {
        if (loadVersion !== stationRouteTimeLoadVersion) return
        console.error('Failed to load simulation station route times:', error)
        stationRouteTimesByKey.value = {}
        ElMessage.error('加载进路占用时间失败')
    } finally {
        if (loadVersion === stationRouteTimeLoadVersion) loadingStationRouteTimes.value = false
    }
}

async function loadGanttSubTableSettings() {
    const instanceID = props.selectedInstanceId
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
            instanceID !== props.selectedInstanceId ||
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
        console.error('Failed to load simulation gantt sub table settings:', error)
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

    const instanceID = props.selectedInstanceId
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
            instanceID !== props.selectedInstanceId ||
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
        console.error('Failed to save simulation gantt sub table settings:', error)
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
    const instanceID = props.selectedInstanceId
    const stationSchemeID = currentStationSchemeId.value.trim()
    if (!instanceID || !stationSchemeID) {
        clearLayout()
        return
    }

    const loadVersion = ++layoutLoadVersion
    loadingLayout.value = true
    try {
        const response = await axios.post('/StationLayout/GetJson', null, {
            params: { instanceID, stationSchemeID },
        })
        if (
            loadVersion !== layoutLoadVersion ||
            instanceID !== props.selectedInstanceId ||
            stationSchemeID !== currentStationSchemeId.value.trim()
        ) {
            return
        }
        layoutData.value = readRecord(response.data)
        layoutDisplayStyles.value = getLayoutDisplayStyles(response.data)
        layoutCells.value = getLayoutCells(response.data)
        layoutGridSpacing.value = getLayoutGridSpacing(response.data)
        await loadLayoutIntoEditor()
    } catch (error) {
        if (loadVersion !== layoutLoadVersion) return
        console.error('Failed to load simulation layout:', error)
        clearLayout()
        ElMessage.error('加载布置图失败')
    } finally {
        if (loadVersion === layoutLoadVersion) loadingLayout.value = false
    }
}

async function loadLayoutIntoEditor() {
    if (!layoutData.value) return
    await nextTick()
    layoutEditorRef.value?.loadDataFromJson?.(layoutData.value)
    await nextTick()
    fitLayoutToFullView()
}

function updateLayoutViewportState() {
    const state = layoutEditorRef.value?.getCanvasViewportState?.()
    if (!state || typeof state !== 'object') return
    const width = Number(state.width)
    const height = Number(state.height)
    const scaleX = Number(state.scaleX)
    const scaleY = Number(state.scaleY)
    if (
        !Number.isFinite(width) ||
        !Number.isFinite(height) ||
        width <= 0 ||
        height <= 0 ||
        !Number.isFinite(scaleX) ||
        !Number.isFinite(scaleY)
    ) {
        return
    }
    layoutViewportState.value = {
        minX: Number(state.minX || 0),
        minY: Number(state.minY || 0),
        maxX: Number(state.maxX || 0),
        maxY: Number(state.maxY || 0),
        width,
        height,
        scaleX,
        scaleY,
    }
}

function fitLayoutDataRect(
    rect: { minX: number; minY: number; maxX: number; maxY: number } | null,
    options: { screenMargin?: number; padding?: number } = {},
) {
    if (!rect) return
    const screenMargin = Math.max(0, Number(options.screenMargin ?? 36))
    const viewport = layoutViewportRef.value
    if (viewport) {
        const width = Math.max(1, rect.maxX - rect.minX)
        const height = Math.max(1, rect.maxY - rect.minY)
        const availableWidth = Math.max(1, viewport.clientWidth - screenMargin * 2)
        const availableHeight = Math.max(1, viewport.clientHeight - screenMargin * 2)
        const scale = Math.max(0.18, Math.min(3, Math.min(availableWidth / width, availableHeight / height)))
        layoutScaleX.value = Number(scale.toFixed(2))
        layoutScaleY.value = Number(scale.toFixed(2))
    }
    nextTick(() => {
        layoutEditorRef.value?.scrollDataRectIntoView?.(rect, {
            screenMargin,
            padding: options.padding ?? 120,
        })
        updateLayoutViewportState()
    })
}

function fitLayoutToFullView() {
    const fullRect = layoutEditorRef.value?.getFullViewRect?.({ screenMargin: 60 }) || null
    fitLayoutDataRect(fullRect, { screenMargin: 36, padding: 140 })
}

async function refreshSimulationData() {
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

async function handleStationSchemeChange() {
    stopPlaybackForReload()
    currentOperationPlanId.value = ''
    clearGanttSubTableState()
    clearStationRoutes()
    clearTrainPlan()
    await loadOperationPlans()
    await refreshSimulationData()
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

watch(
    () => props.selectedInstanceId,
    async () => {
        stopPlaybackForReload()
        await loadStationSchemes()
    },
    { immediate: true },
)

watch(simulationDurationSeconds, () => {
    clampPlayheadToDuration()
})

watch(routeRuns, () => {
    syncActiveRunIndex(playheadSeconds.value)
    scheduleScrollSimulationTableToCurrentRun()
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

watch([activeRunIndex, activeRunIndices], () => {
    scheduleScrollSimulationTableToCurrentRun()
}, {
    flush: 'post',
})

watch(playheadSeconds, () => {
    scheduleScrollGanttToPlayhead()
}, {
    flush: 'post',
})

onBeforeUnmount(() => {
    pausePlayback()
    stopGanttPanelResize()
    if (ganttSubTableSaveTimer) {
        window.clearTimeout(ganttSubTableSaveTimer)
        ganttSubTableSaveTimer = null
    }
    if (tableScrollFrameId !== null) {
        window.cancelAnimationFrame(tableScrollFrameId)
        tableScrollFrameId = null
    }
    if (ganttScrollFrameId !== null) {
        window.cancelAnimationFrame(ganttScrollFrameId)
        ganttScrollFrameId = null
    }
})
</script>

<style scoped lang="css">
.operation-simulation-page {
    display: flex;
    flex-direction: column;
    width: 100%;
    height: 100%;
    min-height: 0;
    gap: 10px;
    overflow: hidden;
}

.simulation-toolbar {
    display: flex;
    flex: 0 0 auto;
    align-items: center;
    justify-content: space-between;
    gap: 12px;
    min-height: 36px;
}

.simulation-toolbar-left,
.simulation-toolbar-right {
    display: flex;
    align-items: center;
    gap: 8px;
    min-width: 0;
}

.simulation-toolbar-left {
    flex: 1;
    flex-wrap: wrap;
}

.simulation-toolbar-control {
    display: flex;
    align-items: center;
    gap: 6px;
    min-width: 0;
}

.simulation-toolbar-control span,
.simulation-speed-row span {
    color: #40546b;
    font-size: 13px;
    font-weight: 600;
    white-space: nowrap;
}

.simulation-select {
    width: min(280px, 32vw);
}

.train-select {
    width: min(320px, 34vw);
}

.playback-mode-toggle :deep(.el-radio-button__inner) {
    padding: 5px 10px;
}

.simulation-body {
    display: grid;
    grid-template-columns: minmax(0, 1fr) 430px;
    flex: 1 1 auto;
    gap: 12px;
    min-height: 0;
    overflow: hidden;
}

.simulation-left-panel {
    display: flex;
    min-width: 0;
    min-height: 0;
    flex-direction: column;
    gap: 0;
}

.simulation-layout-view {
    position: relative;
    flex: 1 1 auto;
    min-width: 0;
    min-height: 0;
    overflow: auto;
    border: 1px solid #d8e3ef;
    border-radius: 8px;
    background: #31363f;
}

.simulation-horizontal-resizer {
    position: relative;
    flex: 0 0 12px;
    cursor: row-resize;
    background: transparent;
    touch-action: none;
}

.simulation-horizontal-resizer::before {
    content: "";
    position: absolute;
    top: 5px;
    right: 0;
    left: 0;
    height: 2px;
    border-radius: 999px;
    background: #a9b8ca;
}

.simulation-horizontal-resizer:hover::before {
    background: #6d89ad;
}

.simulation-layout-stage {
    position: relative;
    display: inline-block;
    min-width: 100%;
    min-height: 100%;
}

.simulation-train-overlay {
    position: absolute;
    top: 0;
    left: 0;
    z-index: 3;
    overflow: visible;
    pointer-events: none;
}

.simulation-train-car {
    filter: drop-shadow(0 1px 2px rgba(15, 23, 42, 0.35));
    transition: transform 90ms linear;
}

.simulation-train-car-body {
    stroke-width: 1.5px;
}

.simulation-train-car-head {
    stroke: rgba(255, 255, 255, 0.88);
    stroke-width: 2px;
    stroke-linecap: round;
}

.simulation-train-car-label {
    fill: #ffffff;
    stroke: rgba(15, 23, 42, 0.72);
    stroke-width: 3px;
    paint-order: stroke fill;
    font-family: Arial, "Microsoft YaHei", sans-serif;
    font-size: 10px;
    font-weight: 700;
}

.simulation-layout-empty {
    display: flex;
    align-items: center;
    justify-content: center;
    height: 100%;
    color: #d7dee9;
    font-size: 13px;
}

.simulation-gantt-panel {
    display: flex;
    flex: 0 0 260px;
    min-height: 188px;
    flex-direction: column;
    overflow: hidden;
    border: 1px solid #d8e3ef;
    border-radius: 8px;
    background: #ffffff;
}

.simulation-gantt-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 10px;
    min-height: 40px;
    padding: 6px 10px;
    border-bottom: 1px solid #edf2f7;
}

.simulation-gantt-title {
    display: flex;
    flex: 0 0 auto;
    align-items: baseline;
    gap: 8px;
    min-width: 0;
    white-space: nowrap;
}

.simulation-gantt-header h3 {
    margin: 0;
    color: #21354f;
    font-size: 14px;
    font-weight: 700;
}

.simulation-gantt-title span {
    color: #65758a;
    font-size: 12px;
}

.simulation-gantt-subtable-toolbar {
    display: flex;
    flex: 1 1 auto;
    align-items: center;
    gap: 8px;
    min-width: 0;
}

.simulation-gantt-sub-tabs {
    flex: 1 1 auto;
    min-width: 0;
    overflow: hidden;
}

.simulation-gantt-sub-tabs :deep(.el-tabs__header) {
    margin: 0;
}

.simulation-gantt-sub-tabs :deep(.el-tabs__nav-wrap::after) {
    display: none;
}

.simulation-gantt-sub-tabs :deep(.el-tabs__item) {
    height: 28px;
    padding: 0 12px;
    font-size: 12px;
    line-height: 28px;
}

.simulation-gantt-subtable-actions {
    display: flex;
    flex: 0 0 auto;
    align-items: center;
    gap: 6px;
}

.simulation-gantt-subtable-actions :deep(.el-button + .el-button) {
    margin-left: 0;
}

.simulation-gantt-subtable-summary {
    flex: 0 0 auto;
    color: #65758a;
    font-size: 12px;
    font-weight: 600;
    white-space: nowrap;
}

.simulation-gantt-subtable-cell-select {
    width: 100%;
}

.simulation-gantt-viewport {
    flex: 1 1 auto;
    min-height: 0;
    overflow: auto;
    scrollbar-gutter: stable;
}

.simulation-gantt-content {
    position: relative;
    width: max-content;
    min-height: 100%;
}

.simulation-gantt-axis-row,
.simulation-gantt-lane-row {
    display: grid;
    grid-template-columns: var(--simulation-gantt-sidebar-width) auto;
}

.simulation-gantt-axis-row {
    position: sticky;
    top: 0;
    z-index: 8;
    height: 36px;
    border-bottom: 1px solid #dfe8f1;
    background: #f8fafc;
}

.simulation-gantt-lane-row {
    height: 38px;
    border-bottom: 1px solid #eef3f8;
}

.simulation-gantt-axis-label,
.simulation-gantt-lane-label {
    position: sticky;
    left: 0;
    z-index: 6;
    box-sizing: border-box;
    width: var(--simulation-gantt-sidebar-width);
    border-right: 1px solid #dfe8f1;
}

.simulation-gantt-axis-label {
    display: flex;
    align-items: center;
    padding: 0 10px;
    background: #f8fafc;
    color: #65758a;
    font-size: 12px;
    font-weight: 700;
}

.simulation-gantt-lane-label {
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

.simulation-gantt-axis-track,
.simulation-gantt-lane-track {
    position: relative;
}

.simulation-gantt-axis-track {
    height: 36px;
    background: #f8fafc;
}

.simulation-gantt-lane-track {
    height: 38px;
    background: #ffffff;
}

.simulation-gantt-axis-tick,
.simulation-gantt-grid-line {
    position: absolute;
    top: 0;
    bottom: 0;
    width: 1px;
    background: #e4ebf3;
}

.simulation-gantt-axis-tick.is-major,
.simulation-gantt-grid-line.is-major {
    background: #cbd8e6;
}

.simulation-gantt-axis-tick span {
    position: absolute;
    bottom: 8px;
    transform: translateX(-50%);
    padding: 0 3px;
    background: #f8fafc;
    color: #65758a;
    font-size: 11px;
    white-space: nowrap;
}

.simulation-gantt-block {
    position: absolute;
    top: 7px;
    z-index: 3;
    box-sizing: border-box;
    height: 24px;
    overflow: hidden;
    padding: 0 6px;
    border: 1px solid color-mix(in srgb, var(--simulation-gantt-block-color) 72%, #0f172a);
    border-radius: 5px;
    background: var(--simulation-gantt-block-color);
    color: #ffffff;
    font-size: 11px;
    line-height: 22px;
    text-overflow: ellipsis;
    white-space: nowrap;
}

.simulation-gantt-block.is-finished {
    border-color: #8792a1;
    background: #a0a8b3;
    color: #ffffff;
}

.simulation-gantt-block.is-active {
    box-shadow: 0 0 0 2px rgba(37, 99, 235, 0.22);
    transform: translateY(-1px);
}

.simulation-gantt-now-line {
    position: absolute;
    top: 0;
    bottom: 0;
    z-index: 5;
    width: 2px;
    transform: translateX(-1px);
    background: #ef4444;
    pointer-events: none;
}

.simulation-gantt-empty {
    display: flex;
    flex: 1 1 auto;
    align-items: center;
    justify-content: center;
    color: #65758a;
    font-size: 13px;
}

.simulation-side-panel {
    display: flex;
    min-width: 0;
    min-height: 0;
    flex-direction: column;
    overflow: hidden;
    border: 1px solid #d8e3ef;
    border-radius: 8px;
    background: #ffffff;
}

.simulation-panel-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 12px;
    padding: 12px 14px;
    border-bottom: 1px solid #e4edf6;
    background: #f8fbff;
}

.simulation-panel-header h2,
.simulation-table-header h3 {
    margin: 0;
    color: #21354f;
    font-size: 16px;
    font-weight: 700;
    line-height: 1.4;
}

.simulation-panel-header span,
.simulation-table-header span {
    color: #65758a;
    font-size: 12px;
}

.simulation-controls {
    display: flex;
    flex: 0 0 auto;
    flex-direction: column;
    gap: 8px;
    padding: 12px 14px;
    border-bottom: 1px solid #edf2f7;
}

.simulation-control-buttons,
.simulation-speed-row {
    display: flex;
    align-items: center;
    gap: 8px;
}

.simulation-clock {
    margin-left: auto;
    color: #1f3a68;
    font-family: Consolas, "Microsoft YaHei", monospace;
    font-size: 13px;
    font-weight: 700;
}

.simulation-speed-row {
    justify-content: space-between;
}

.simulation-speed-select {
    width: 112px;
}

.simulation-route-status {
    flex: 0 0 auto;
    padding: 12px 14px;
    border-bottom: 1px solid #edf2f7;
}

.simulation-status-grid {
    display: grid;
    grid-template-columns: repeat(2, minmax(0, 1fr));
    gap: 10px;
}

.simulation-status-grid div {
    display: flex;
    min-width: 0;
    flex-direction: column;
    gap: 3px;
    padding: 8px 10px;
    border: 1px solid #e4edf6;
    border-radius: 6px;
    background: #fbfdff;
}

.simulation-status-grid span {
    color: #718096;
    font-size: 12px;
}

.simulation-status-grid strong {
    min-width: 0;
    overflow: hidden;
    color: #21354f;
    font-size: 13px;
    font-weight: 700;
    text-overflow: ellipsis;
    white-space: nowrap;
}

.simulation-table-section {
    display: flex;
    flex: 1 1 auto;
    min-height: 0;
    flex-direction: column;
}

.simulation-table-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 12px;
    padding: 12px 14px 8px;
}

.simulation-movement-table {
    flex: 1;
    min-height: 0;
}

.simulation-movement-table :deep(.el-table__cell) {
    font-size: 12px;
}

.simulation-movement-table :deep(.simulation-current-row) {
    --el-table-tr-bg-color: #f8fbff;
}

.simulation-movement-table :deep(.simulation-active-row) {
    --el-table-tr-bg-color: #dbeafe;
    color: #1d4ed8;
    font-weight: 600;
}

.simulation-movement-table :deep(.simulation-active-row .el-table__cell:first-child) {
    box-shadow: inset 3px 0 0 #2563eb;
}

@media (max-width: 1100px) {
    .simulation-body {
        grid-template-columns: minmax(0, 1fr);
        overflow: auto;
    }

    .simulation-left-panel {
        min-height: 650px;
    }

    .simulation-layout-view {
        min-height: 420px;
    }

    .simulation-side-panel {
        min-height: 460px;
    }
}

@media (max-width: 680px) {
    .simulation-toolbar,
    .simulation-toolbar-left,
    .simulation-toolbar-right,
    .simulation-toolbar-control {
        align-items: stretch;
        flex-direction: column;
    }

    .simulation-toolbar-right {
        flex-direction: row;
    }

    .simulation-left-panel {
        min-height: 600px;
    }

    .simulation-layout-view {
        min-height: 380px;
    }

    .simulation-gantt-panel {
        flex-basis: 220px;
    }

    .simulation-select,
    .train-select {
        width: 100%;
    }
}
</style>
