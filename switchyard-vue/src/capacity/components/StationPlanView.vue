<template>
    <div ref="viewRoot" class="station-plan-view">
        <div class="station-plan-toolbar" role="toolbar" :aria-label="t('stationPlanView.toolbar')">
            <ActionButton :icon="FullScreen" :label="t('capacityGantt.fit')" :active="autoFit" :disabled="displayRows.length === 0" @click="enableAutoFit" />
            <label class="station-plan-scale"><span>{{ t('capacityGantt.scaleX') }}</span>
                <ElSlider :model-value="scaleX" :min="Math.min(0.01, scaleX)" :max="Math.max(10, Math.ceil(scaleX))" :step="0.01" :aria-label="t('capacityGantt.scaleX')" @update:model-value="setScaleX" />
                <output>{{ scaleX.toFixed(2) }}×</output>
            </label>
            <label class="station-plan-scale"><span>{{ t('capacityGantt.scaleY') }}</span>
                <ElSlider :model-value="scaleY" :min="Math.min(0.05, scaleY)" :max="Math.max(4, Math.ceil(scaleY))" :step="0.01" :aria-label="t('capacityGantt.scaleY')" @update:model-value="setScaleY" />
                <output>{{ scaleY.toFixed(2) }}×</output>
            </label>
            <div class="station-plan-line-mode" role="group" :aria-label="t('stationPlanView.lineMode')" :title="t('stationPlanView.lineModeHint')">
                <span>{{ t('stationPlanView.lineMode') }}</span>
                <button v-for="mode in (['straight', 'orthogonal'] as const)" :key="mode" type="button" :data-line-mode="mode"
                    :aria-pressed="lineMode === mode" @click="setLineMode(mode)">{{ t(`stationPlanView.${mode}Mode`) }}</button>
            </div>
            <ActionButton :icon="Refresh" :label="t('capacityGantt.refresh')" :loading="loading" :disabled="refreshDisabled" @click="emit('refresh')" />
            <ActionButton :icon="Back" variant="icon-text" :label="t('stationPlanView.undo')" :disabled="!canUndo" aria-keyshortcuts="Control+Z" @click="emit('undo')" />
            <ActionButton :icon="Right" variant="icon-text" :label="t('stationPlanView.redo')" :disabled="!canRedo" aria-keyshortcuts="Control+Y" @click="emit('redo')" />
            <output class="station-plan-history-count" :title="t('stationPlanView.historyHint')" :aria-label="t('stationPlanView.historyCount', { undo: undoCount, redo: redoCount })">{{ undoCount + redoCount }} / 20</output>
            <slot name="actions" />
        </div>
        <div v-if="displayRows.length === 0" class="station-plan-empty">{{ emptyText || t('stationPlanView.emptyRows') }}</div>
        <div v-else class="station-plan-grid">
            <div class="station-plan-corner" :aria-label="t('stationPlanView.rowLegend')" :title="t('stationPlanView.rowLegend')">
                <span class="station-plan-row-kind is-track-row"><i aria-hidden="true" />{{ t('stationPlanView.rowKinds.track') }}</span>
                <span class="station-plan-row-kind is-node-row"><i aria-hidden="true" />{{ t('stationPlanView.rowKinds.node') }}</span>
            </div>
            <div ref="timeViewport" class="station-plan-time-viewport" :style="{ marginRight: `${scrollbarGutter.width}px` }">
                <svg :width="contentWidth" height="50" class="station-plan-time-axis">
                    <text v-for="tick in hourTicks" :key="tick.time" :x="timeToX(tick.time)" y="26" class="station-plan-hour-label" :style="{ textAnchor: tick.time === domain.start ? 'start' : tick.time === domain.end ? 'end' : 'middle' }">{{ tick.label }}</text>
                </svg>
            </div>
            <div ref="rowViewport" class="station-plan-row-viewport" :style="{ marginBottom: `${scrollbarGutter.height}px` }">
                <div :style="{ height: `${contentHeight}px` }" class="station-plan-row-axis">
                    <div v-for="(row, index) in displayRows" :key="`band:${row.key}`" class="station-plan-axis-row-band" :class="isTrackRow(row) ? 'is-track-row' : 'is-node-row'"
                        :style="{ top: `${rowToY(index) - rowPitch / 2}px`, height: `${rowPitch}px` }" aria-hidden="true" />
                    <div v-for="(row, index) in displayRows" :key="row.key" class="station-plan-row-label" :data-row-key="row.key" :class="[`is-${row.kind}`, isTrackRow(row) ? 'is-track-row' : 'is-node-row', { 'is-track-node': trackChildKeys.has(row.key), 'is-drop-target': trackTargetRowIndexes.has(index) }]" :style="{ top: `${rowToY(index)}px` }" :title="row.label">
                        <button v-if="row.kind === 'track' && row.children?.length" type="button" class="station-plan-track-label" :data-track-key="row.key"
                            :disabled="pickMode"
                            :aria-expanded="false" :title="`${row.label}\n${t('stationPlanView.expandTrack')}`" @dblclick.stop="toggleTrackExpansion(row.key)">
                            <span aria-hidden="true">▸</span><span>{{ row.label }}</span>
                        </button>
                        <span v-else>{{ row.label }}</span>
                    </div>
                    <button v-for="group in expandedTrackHeaders" :key="`track:${group.row.key}`" type="button" class="station-plan-track-label station-plan-expanded-track-label"
                        :disabled="pickMode"
                        :class="{ 'is-compact': rowPitch < 36 }" :data-track-key="group.row.key" :aria-expanded="true" :aria-label="group.row.label" :title="`${group.row.label}\n${t('stationPlanView.collapseTrack')}`"
                        :style="{ top: `${rowToY(group.firstIndex) + (group.childCount - 1) * rowPitch / 2}px` }" @dblclick.stop="toggleTrackExpansion(group.row.key)">
                        <span aria-hidden="true">▾</span><span>{{ group.row.label }}</span>
                    </button>
                </div>
            </div>
            <div ref="viewport" class="station-plan-canvas-viewport" @scroll="syncScroll" @click="clearTrainSelection">
                <svg :width="contentWidth" :height="contentHeight" class="station-plan-canvas" role="group" :aria-label="t('stationPlanView.chartLabel')">
                    <g class="station-plan-background" aria-hidden="true">
                        <rect v-for="(row, index) in displayRows" :key="`band:${row.key}`" class="station-plan-row-band" :class="isTrackRow(row) ? 'is-track-row' : 'is-node-row'"
                            :x="chartPadding.x" :y="rowToY(index) - rowPitch / 2" :width="contentWidth - chartPadding.x * 2" :height="rowPitch" />
                        <line v-for="tick in ticks" :key="tick.time" class="station-plan-time-line" :class="`is-${tick.type}`" :x1="timeToX(tick.time)" :x2="timeToX(tick.time)" y1="0" :y2="contentHeight" />
                        <line v-for="(row, index) in displayRows" :key="row.key" class="station-plan-row-line" :data-row-kind="isTrackRow(row) ? 'track' : 'node'" :x1="chartPadding.x" :x2="contentWidth - chartPadding.x" :y1="rowToY(index)" :y2="rowToY(index)" />
                        <rect v-for="index in [...trackTargetRowIndexes]" :key="`target:${index}`" class="station-plan-track-target" :data-track-target-row="index"
                            :x="chartPadding.x" :y="rowToY(index) - rowPitch / 2" :width="contentWidth - chartPadding.x * 2" :height="rowPitch" />
                    </g>
                    <g v-for="line in lines" :key="line.train.id" class="station-plan-train" :class="{ 'is-selected': selectedTrainIDs.has(line.train.id) }" :data-train-id="line.train.id"
                        role="button" :tabindex="pickMode ? -1 : 0" :aria-disabled="pickMode" :aria-label="t('stationPlanView.trainLine', { train: line.train.label })" :aria-pressed="selectedTrainIDs.has(line.train.id)"
                        @click.stop="toggleTrainSelection(line.train.id)" @keydown.enter.stop.prevent="toggleTrainSelection(line.train.id)" @keydown.space.stop.prevent="toggleTrainSelection(line.train.id)">
                        <title>{{ trainTitle(line.train) }}</title>
                        <path :d="line.connectorPath" class="station-plan-train-hit-area" aria-hidden="true" />
                        <path :d="line.connectorPath" :stroke="line.train.color" class="station-plan-train-connector" aria-hidden="true" />
                        <path :d="line.path" class="station-plan-train-hit-area" aria-hidden="true" />
                        <path :d="line.path" :stroke="line.train.color" class="station-plan-train-line" />
                        <text v-for="(segment, index) in line.horizontalSegments" :key="`horizontal:${index}`" :x="(segment.x1 + segment.x2) / 2" :y="segment.y - 7" :fill="line.train.color" class="station-plan-train-label station-plan-horizontal-label">{{ line.train.label }}</text>
                        <g v-for="terminal in line.terminals" :key="terminal.kind" class="station-plan-terminal" :data-terminal="terminal.kind">
                            <line :x1="terminal.from.x" :y1="terminal.from.y" :x2="terminal.to.x" :y2="terminal.to.y" class="station-plan-train-hit-area" aria-hidden="true" />
                            <line :x1="terminal.from.x" :y1="terminal.from.y" :x2="terminal.to.x" :y2="terminal.to.y" :stroke="line.train.color" class="station-plan-terminal-line" />
                            <path :d="terminal.arrowPath" :stroke="line.train.color" class="station-plan-terminal-line" />
                            <text :x="terminal.label.x" :y="terminal.label.y" :transform="`rotate(${terminal.label.angle} ${terminal.label.x} ${terminal.label.y})`" :fill="line.train.color" class="station-plan-train-label station-plan-terminal-label">{{ line.train.label }}</text>
                        </g>
                    </g>
                    <line v-for="item in trackDragSegments" :key="`track-drag:${item.train.id}:${item.segment.movementID}:${item.segment.isConnector ? 'connector' : 'movement'}`"
                        class="station-plan-track-drag" :class="{ 'is-disabled': !canEditTrain(item.train.id) }" :data-track-train-id="item.train.id" :data-track-movement-id="item.segment.movementID"
                        :data-track-segment-type="item.segment.isConnector ? 'connector' : 'movement'"
                        :x1="item.segment.x1" :y1="item.segment.y1" :x2="item.segment.x2" :y2="item.segment.y2"
                        :aria-label="t('stationPlanView.trackHandleLabel', { movement: item.segment.detail || item.segment.movementID, track: item.track.name })"
                        @click.stop @pointerdown.stop="startTrackDrag($event, item.train, item.segment, item.track)"><title>{{ t('stationPlanView.trackDragHint') }}</title></line>
                    <!-- Endpoint handles stay above every train line and track drag area. -->
                    <g v-for="line in selectedLines" :key="`handles:${line.train.id}`" class="station-plan-segment-controls" :data-control-train-id="line.train.id">
                        <g v-for="(segment, index) in line.editableSegments" :key="segment.movementID" :data-segment-index="index" :data-movement-id="segment.movementID">
                            <g v-for="edge in (['start', 'end'] as const)" :key="edge" :data-handle-edge="edge"
                                :transform="`translate(${edge === 'start' ? segment.x1 : segment.x2} ${edge === 'start' ? segment.y1 : segment.y2})`">
                                <rect x="-5" y="-5" width="10" height="10"
                                    class="station-plan-time-handle" :class="{ 'is-disabled': !canEditTrain(line.train.id) }" :stroke="line.train.color" :data-edge="edge" role="button" tabindex="0"
                                    :style="{ '--station-plan-handle-color': line.train.color }"
                                    :data-row-key="edge === 'start' ? segment.startRowKey : segment.endRowKey"
                                    :aria-label="t(`stationPlanView.${edge}Handle`, { train: line.train.label, movement: segment.detail || segment.movementID })" :aria-disabled="!canEditTrain(line.train.id)"
                                    :title="t(edge === 'start' ? 'stationPlanView.alignStartHint' : 'stationPlanView.alignEndHint')"
                                    :aria-describedby="timeHandleInfo && activeTimeHandle?.trainID === line.train.id && activeTimeHandle.segmentIndex === index && activeTimeHandle.edge === edge ? timeHandleTooltipID : undefined"
                                    @mouseenter="showTimeHandleInfo($event, line.train.id, index, edge)" @mouseleave="hideTimeHandleInfo"
                                    @focus="showTimeHandleInfo($event, line.train.id, index, edge)" @blur="hideTimeHandleInfo"
                                    @click.stop @pointerdown.stop="startSegmentDrag($event, line.train, segment, edge)"
                                    @dblclick.stop.prevent="alignSegment($event, line.train, segment, edge)"
                                    @contextmenu.stop.prevent="openDwellingMenu($event, line.train, segment, edge)"
                                    @keydown.left.stop.prevent="nudgeSegment(line.train, segment, edge, -1, $event.ctrlKey)" @keydown.right.stop.prevent="nudgeSegment(line.train, segment, edge, 1, $event.ctrlKey)" />
                            </g>
                        </g>
                    </g>
                    <text v-if="lines.length === 0" :x="24" y="26" class="station-plan-empty-label">{{ t('stationPlanView.emptyTrains') }}</text>
                    <g v-if="pickMode" class="station-plan-pick-layer">
                        <rect x="0" y="0" :width="contentWidth" :height="contentHeight" class="station-plan-pick-surface" role="application" tabindex="0"
                            :aria-label="t('stationPlanView.creation.pickCanvasLabel')" @click.stop="pickCanvasPoint" @pointerdown.stop @dblclick.stop.prevent />
                        <g class="station-plan-draft-points">
                            <line v-for="pair in draftPointPairs" :key="pair.index" class="station-plan-draft-line" :data-draft-pair="pair.index"
                                :x1="pair.from.x" :y1="pair.from.y" :x2="pair.to.x" :y2="pair.to.y" />
                            <g v-for="point in visibleDraftPoints" :key="point.index" class="station-plan-draft-marker" :data-draft-point="point.index" :transform="`translate(${point.x} ${point.y})`"
                                @click.stop="pickCanvasPoint" @pointerdown.stop @dblclick.stop.prevent>
                                <title>{{ t('stationPlanView.creation.pickedPoint', { index: point.index + 1, node: point.label, time: stationPlanTimeLabel(point.timeMinutes) }) }}</title>
                                <circle r="5" class="station-plan-draft-point" />
                                <text x="0" y="-10" class="station-plan-draft-point-label">{{ point.index + 1 }}</text>
                            </g>
                        </g>
                    </g>
                </svg>
            </div>
        </div>
        <ElTooltip :visible="!!timeHandleInfo" :virtual-ref="activeTimeHandle?.element" virtual-triggering placement="top" :show-arrow="false" :enterable="false" :persistent="false" :hide-after="0" transition="" popper-class="station-plan-handle-tooltip">
            <template #content>
                <div v-if="timeHandleInfo" :id="timeHandleTooltipID" class="station-plan-handle-info">
                    <span>{{ t('stationPlanView.handleInfo.trainNumber') }}</span><strong>{{ timeHandleInfo.trainNumber }}</strong>
                    <span>{{ t('stationPlanView.handleInfo.movement') }}</span><strong>{{ timeHandleInfo.movement }}</strong>
                    <span>{{ t('stationPlanView.handleInfo.node') }}</span><strong>{{ timeHandleInfo.node }}</strong>
                    <span>{{ t(`stationPlanView.handleInfo.${timeHandleInfo.edge}Time`) }}</span><strong>{{ timeHandleInfo.time }}</strong>
                </div>
            </template>
        </ElTooltip>
        <Teleport to="body">
            <div v-if="dwellingMenu" ref="dwellingMenuElement" class="station-plan-dwelling-menu" role="menu" :aria-label="t('stationPlanView.dwelling.menuLabel')"
                :style="{ left: `${dwellingMenu.x}px`, top: `${dwellingMenu.y}px` }" @pointerdown.stop @click.stop @contextmenu.stop.prevent>
                <button type="button" role="menuitem" :title="t(dwellingMenu.target.action === 'passing-to-stop' ? 'stationPlanView.dwelling.passToStopHint' : 'stationPlanView.dwelling.stopToPassHint')"
                    @click.stop="applyDwellingMenu">{{ t(dwellingMenu.target.action === 'passing-to-stop' ? 'stationPlanView.dwelling.passToStop' : 'stationPlanView.dwelling.stopToPass') }}</button>
            </div>
        </Teleport>
        <div v-if="dragPreview" class="station-plan-drag-feedback" role="status">{{ dragPreview.segment.detail || dragPreview.segment.movementID }} · {{ rowLabels.get(dragPreview.segment.startRowKey) }} → {{ rowLabels.get(dragPreview.segment.endRowKey) }} · {{ stationPlanTimeLabel(previewMovementSegment?.startMinutes ?? dragPreview.startMinutes) }} – {{ stationPlanTimeLabel(previewMovementSegment?.endMinutes ?? dragPreview.endMinutes) }} · {{ t(dragPreview.shiftFollowing === false ? 'stationPlanView.singleEndpoint' : 'stationPlanView.shiftFollowing') }}</div>
        <div v-if="trackDrag || trackDropInvalid" class="station-plan-drag-feedback" :class="{ 'is-invalid': trackDropInvalid || !trackDrag?.targetTrackID }" role="status">{{ trackDragMessage }}</div>
    </div>
</template>

<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, shallowRef, useId, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { ElSlider, ElTooltip } from 'element-plus'
import { Back, FullScreen, Refresh, Right } from '@element-plus/icons-vue'
import ActionButton from '@/components/ui/ActionButton.vue'
import { previewStationPlanSegmentEdit, stationPlanChartPadding, stationPlanCoincidentAnchors, stationPlanDomain, stationPlanTimeGrid, stationPlanTimeLabel, stationPlanTrainPath, stationPlanTrackVisitIndexes, type StationPlanAxisRow, type StationPlanTrain, type StationPlanEditableSegment, type StationPlanSegmentEdit, type StationPlanTrack, type StationPlanTrackEdit, type StationPlanTrackSegment } from './stationPlanView'
import { stationPlanHistoryShortcut } from './stationPlanActions'
import type { StationPlanDraftPoint } from './stationPlanCreation'
import type { StationPlanEditMovement, StationPlanEditRoute } from './stationPlanEditing'
import { resolveStationPlanDwellingTarget, type StationPlanDwellingTarget } from './stationPlanDwelling'
import { cloneSvgWithStyles, createSvgElement, hasRenderedLayout, serializeReportSvg, snapshotHtmlText, stripReportInteractionMetadata, type StationPlanReportViewState, type StationPlanReportLabels } from '../report/svgSnapshot'
import type { ReportFigure } from '../report/wordReport'

const props = withDefaults(defineProps<{
    rows: StationPlanAxisRow[]
    trains: StationPlanTrain[]
    tracks?: StationPlanTrack[]
    movements?: StationPlanEditMovement[]
    routes?: StationPlanEditRoute[]
    dwellingMovementIDs?: Record<string, string[]>
    pickMode?: boolean
    draftPoints?: StationPlanDraftPoint[]
    startMinutes?: number | null
    endMinutes?: number | null
    loading?: boolean
    refreshDisabled?: boolean
    emptyText?: string
    selectionScope?: string
    editable?: boolean
    readOnlyTrainIDs?: string[]
    undoCount?: number
    redoCount?: number
}>(), { tracks: () => [], movements: () => [], routes: () => [], dwellingMovementIDs: () => ({}), pickMode: false, draftPoints: () => [], startMinutes: null, endMinutes: null, loading: false, refreshDisabled: false, emptyText: '', selectionScope: '', editable: true, readOnlyTrainIDs: () => [], undoCount: 0, redoCount: 0 })
const emit = defineEmits<{ (event: 'refresh'): void; (event: 'edit' | 'align', edit: StationPlanSegmentEdit): void; (event: 'track-edit', edit: StationPlanTrackEdit): void; (event: 'dwelling-edit', target: StationPlanDwellingTarget): void; (event: 'point-pick', point: StationPlanDraftPoint): void; (event: 'pick-cancel'): void; (event: 'undo'): void; (event: 'redo'): void }>()
const { t } = useI18n()
const scaleX = defineModel<number>('scaleX', { default: 1 })
const scaleY = defineModel<number>('scaleY', { default: 1 })
const autoFit = ref(true)
const lineMode = ref<'straight' | 'orthogonal'>('straight')
const selectedTrainIDs = ref(new Set<string>())
const expandedTrackKeys = ref(new Set<string>())
const displayRows = computed(() => props.rows.flatMap(row =>
    row.kind === 'track' && row.children?.length && (props.pickMode || expandedTrackKeys.value.has(row.key)) ? row.children.map(child => ({ ...child, isTrackNode: true })) : [row]))
const expandedTrackHeaders = computed(() => {
    const groups: { row: StationPlanAxisRow; firstIndex: number; childCount: number }[] = []
    let index = 0
    for (const row of props.rows) {
        if (row.kind === 'track' && row.children?.length && (props.pickMode || expandedTrackKeys.value.has(row.key))) {
            groups.push({ row, firstIndex: index, childCount: row.children.length })
            index += row.children.length
        } else index++
    }
    return groups
})
const trackChildKeys = computed(() => new Set(props.rows.flatMap(row => row.children?.map(child => child.key) || [])))
const isTrackRow = (row: StationPlanAxisRow) => row.kind === 'track' || row.isTrackNode === true || trackChildKeys.value.has(row.key)
const groupedTracks = computed(() => {
    const rows = new Map<string, StationPlanTrack>()
    const nodes = new Map<string, StationPlanTrack>()
    for (const row of props.rows) {
        if (row.kind !== 'track') continue
        const track = props.tracks.find(item => item.id === row.sourceID && item.name.trim() && item.fromNodeID && item.toNodeID && item.fromNodeID !== item.toNodeID)
        const nodeIDs = row.nodeIDs || row.children?.map(child => child.sourceID) || []
        if (!track || !nodeIDs.includes(track.fromNodeID) || !nodeIDs.includes(track.toNodeID)) continue
        rows.set(row.key, track)
        for (const child of row.children || []) rows.set(child.key, track)
        for (const nodeID of nodeIDs) nodes.set(`node:${nodeID}`, track)
    }
    return { rows, nodes }
})
const viewRoot = ref<HTMLElement | null>(null)
const viewport = ref<HTMLElement | null>(null)
const timeViewport = ref<HTMLElement | null>(null)
const rowViewport = ref<HTMLElement | null>(null)
const scrollbarGutter = ref({ width: 0, height: 0 })
interface DragState {
    train: StationPlanTrain
    edit: StationPlanSegmentEdit
    pointerID: number
    x: number
    lastClientX: number
    scrollX: number
    domain: { start: number; end: number }
    pixelsPerMinute: number
    moved: boolean
    cursor: string
    userSelect: string
}
const drag = ref<DragState | null>(null)
const dragPreview = ref<StationPlanSegmentEdit | null>(null)
interface TrackDragState {
    trainID: string
    segment: StationPlanTrackSegment
    source: StationPlanTrack
    visitIndexes: number[]
    targetTrackID: string | null
    pointerID: number
    y: number
    lastClientY: number
    scrollY: number
    viewportTop: number
    moved: boolean
    cursor: string
    userSelect: string
}
const trackDrag = ref<TrackDragState | null>(null)
const trackDropInvalid = ref(false)
let suppressClick = false
const domain = computed(() => drag.value?.domain || stationPlanDomain(props.trains, props.startMinutes, props.endMinutes))
const ticks = computed(() => stationPlanTimeGrid(domain.value.start, domain.value.end))
const hourTicks = computed(() => ticks.value.filter(tick => tick.type === 'hour'))
const pixelsPerMinute = computed(() => 6 * scaleX.value)
const rowPitch = computed(() => 52 * scaleY.value)
const chartPadding = computed(() => stationPlanChartPadding(props.trains))
const contentWidth = computed(() => chartPadding.value.x * 2 + (domain.value.end - domain.value.start) * pixelsPerMinute.value)
const contentHeight = computed(() => chartPadding.value.y * 2 + displayRows.value.length * rowPitch.value)
const lines = computed(() => props.trains.map(source => {
    const train = trackDrag.value?.trainID === source.id ? previewTrackDrag(source) : dragPreview.value?.trainID === source.id ? previewStationPlanSegmentEdit(source, dragPreview.value) : source
    return { train, ...stationPlanTrainPath(train, displayRows.value, domain.value.start, pixelsPerMinute.value, rowPitch.value, chartPadding.value, lineMode.value) }
}).filter(line => line.path))
const selectedLines = computed(() => props.pickMode ? [] : lines.value.filter(line => selectedTrainIDs.value.has(line.train.id)))
const trackDragSegments = computed(() => selectedLines.value.flatMap(line => {
    const segments: StationPlanTrackSegment[] = [...line.editableSegments]
    for (let index = 1; index < line.editableSegments.length; index++) {
        const previous = line.editableSegments[index - 1]!, next = line.editableSegments[index]!
        const fromIndex = previous.visitIndexes[1], toIndex = next.visitIndexes[0]
        if (fromIndex === undefined || toIndex === undefined || (previous.x2 === next.x1 && previous.y2 === next.y1)) continue
        segments.push({ ...previous, isConnector: true, detail: `${previous.detail} → ${next.detail}`,
            startMinutes: previous.endMinutes, endMinutes: next.startMinutes, startRowKey: previous.endRowKey, endRowKey: next.startRowKey,
            rowKey: previous.endRowKey, visitIndexes: [fromIndex, toIndex], startAnchors: previous.endAnchors, endAnchors: next.startAnchors,
            x1: previous.x2, y1: previous.y2, x2: next.x1, y2: next.y1, y: previous.y2 })
    }
    return segments.flatMap(segment => {
        const track = groupedTracks.value.nodes.get(segment.startRowKey)
        return track && groupedTracks.value.nodes.get(segment.endRowKey)?.id === track.id && stationPlanTrackVisitIndexes(line.train, segment, track).length
            ? [{ train: line.train, segment, track }] : []
    })
}))
const trackTargetRowIndexes = computed(() => new Set(trackDrag.value?.targetTrackID
    ? displayRows.value.flatMap((row, index) => groupedTracks.value.rows.get(row.key)?.id === trackDrag.value?.targetTrackID ? [index] : []) : []))
const trackDragMessage = computed(() => {
    const target = props.tracks.find(track => track.id === trackDrag.value?.targetTrackID)
    return target ? t('stationPlanView.trackDragPreview', { track: target.name }) : t('stationPlanView.trackDropInvalid')
})
const previewMovementSegment = computed(() => {
    const edit = dragPreview.value
    return edit ? lines.value.find(line => line.train.id === edit.trainID)?.editableSegments.find(segment => segment.movementID === edit.segment.movementID) : undefined
})
const canUndo = computed(() => !props.pickMode && props.editable && !props.loading && !drag.value && !trackDrag.value && props.undoCount > 0)
const canRedo = computed(() => !props.pickMode && props.editable && !props.loading && !drag.value && !trackDrag.value && props.redoCount > 0)
const rowLabels = computed(() => new Map(props.rows.flatMap(row => [row, ...(row.children || [])]).map(row => [row.key, row.label])))
const timeHandleTooltipID = `station-plan-time-handle-${useId()}`
const activeTimeHandle = shallowRef<{ element: SVGRectElement; trainID: string; segmentIndex: number; edge: 'start' | 'end' } | null>(null)
interface DwellingMenu {
    target: StationPlanDwellingTarget
    segment: StationPlanEditableSegment
    edge: 'start' | 'end'
    trigger: SVGRectElement | null
    x: number
    y: number
}
const dwellingMenu = shallowRef<DwellingMenu | null>(null)
const dwellingMenuElement = ref<HTMLElement | null>(null)
let stopDwellingMenuWatch: (() => void) | null = null
const timeHandleInfo = computed(() => {
    const handle = activeTimeHandle.value
    if (!handle || drag.value || trackDrag.value || dwellingMenu.value) return null
    const line = selectedLines.value.find(item => item.train.id === handle.trainID)
    const segment = line?.editableSegments[handle.segmentIndex]
    if (!line || !segment) return null
    return {
        trainNumber: line.train.label,
        movement: segment.detail || segment.movementID,
        node: rowLabels.value.get(handle.edge === 'start' ? segment.startRowKey : segment.endRowKey) || (handle.edge === 'start' ? segment.startRowKey : segment.endRowKey),
        edge: handle.edge,
        time: stationPlanTimeLabel(handle.edge === 'start' ? segment.startMinutes : segment.endMinutes),
    }
})
const timeToX = (time: number) => chartPadding.value.x + (time - domain.value.start) * pixelsPerMinute.value
const rowToY = (index: number) => chartPadding.value.y + (index + 0.5) * rowPitch.value
const draftPlotPoints = computed(() => props.draftPoints.map((point, index) => {
    const rowIndex = displayRows.value.findIndex(row => row.kind === 'node' && row.sourceID === point.nodeID)
    return rowIndex < 0 || !Number.isFinite(point.timeMinutes) ? null : { ...point, index,
        x: timeToX(point.timeMinutes), y: rowToY(rowIndex), label: displayRows.value[rowIndex]!.label }
}))
const visibleDraftPoints = computed(() => draftPlotPoints.value.filter(point => point !== null))
const draftPointPairs = computed(() => draftPlotPoints.value.flatMap((point, index, points) => {
    const next = points[index + 1]
    return index % 2 === 0 && point && next ? [{ from: point, to: next, index }] : []
}))

function pickCanvasPoint(event: MouseEvent) {
    if (!props.pickMode || props.loading || event.button !== 0 || !viewport.value) return
    const bounds = viewport.value.getBoundingClientRect()
    if (event.clientX < bounds.left || event.clientX > bounds.right || event.clientY < bounds.top || event.clientY > bounds.bottom) return
    const x = event.clientX - bounds.left + viewport.value.scrollLeft
    const y = event.clientY - bounds.top + viewport.value.scrollTop
    const row = displayRows.value[Math.floor((y - chartPadding.value.y) / rowPitch.value)]
    const time = domain.value.start + (x - chartPadding.value.x) / pixelsPerMinute.value
    if (row?.kind !== 'node' || !row.sourceID || !Number.isFinite(time) || time < domain.value.start || time > domain.value.end) return
    emit('point-pick', { nodeID: row.sourceID, timeMinutes: Math.round(time * 60) / 60 })
}

function showTimeHandleInfo(event: Event, trainID: string, segmentIndex: number, edge: 'start' | 'end') {
    if (event.currentTarget instanceof SVGRectElement && !drag.value && !trackDrag.value && !dwellingMenu.value) activeTimeHandle.value = { element: event.currentTarget, trainID, segmentIndex, edge }
}
function hideTimeHandleInfo(event: Event) {
    if (activeTimeHandle.value?.element === event.currentTarget) activeTimeHandle.value = null
}
function closeDwellingMenu() {
    if (!dwellingMenu.value && !stopDwellingMenuWatch) return
    dwellingMenu.value = null
    stopDwellingMenuWatch?.()
    stopDwellingMenuWatch = null
    window.removeEventListener('pointerdown', handleDwellingMenuOutside, true)
    window.removeEventListener('keydown', handleDwellingMenuKey, true)
    window.removeEventListener('scroll', closeDwellingMenu, true)
    window.removeEventListener('resize', closeDwellingMenu)
    window.removeEventListener('blur', closeDwellingMenu)
}
function handleDwellingMenuOutside(event: PointerEvent) {
    if (!dwellingMenuElement.value?.contains(event.target as Node)) closeDwellingMenu()
}
function handleDwellingMenuKey(event: KeyboardEvent) {
    if (!dwellingMenu.value) return
    if (event.key === 'Escape') {
        event.preventDefault()
        event.stopPropagation()
        const trigger = dwellingMenu.value.trigger
        closeDwellingMenu()
        trigger?.focus()
    } else if (event.key === 'Tab') closeDwellingMenu()
    else if (['ArrowDown', 'ArrowUp', 'Home', 'End'].includes(event.key)) {
        event.preventDefault()
        event.stopPropagation()
        dwellingMenuElement.value?.querySelector<HTMLButtonElement>('button')?.focus()
    }
}
function positionDwellingMenu(menu: DwellingMenu, width = 220, height = 52) {
    if (dwellingMenu.value !== menu) return
    menu.x = Math.max(8, Math.min(menu.x, window.innerWidth - width - 8))
    menu.y = Math.max(8, Math.min(menu.y, window.innerHeight - height - 8))
    dwellingMenu.value = { ...menu }
}
function openDwellingMenu(event: MouseEvent, train: StationPlanTrain, segment: StationPlanEditableSegment, edge: 'start' | 'end') {
    closeDwellingMenu()
    if (!canEditTrain(train.id) || drag.value || trackDrag.value || suppressClick || (event.button !== 0 && event.button !== 2)) return
    const target = resolveStationPlanDwellingTarget(train, segment, edge, props.movements, props.routes, props.tracks, props.dwellingMovementIDs[train.id] || [])
    if (!target) return
    activeTimeHandle.value = null
    const trigger = event.currentTarget instanceof SVGRectElement ? event.currentTarget : null
    const bounds = event.clientX === 0 && event.clientY === 0 ? trigger?.getBoundingClientRect() : null
    dwellingMenu.value = { target, segment, edge, trigger, x: bounds?.right ?? event.clientX, y: bounds?.bottom ?? event.clientY }
    positionDwellingMenu(dwellingMenu.value)
    const menu = dwellingMenu.value!
    window.addEventListener('pointerdown', handleDwellingMenuOutside, true)
    window.addEventListener('keydown', handleDwellingMenuKey, true)
    window.addEventListener('scroll', closeDwellingMenu, true)
    window.addEventListener('resize', closeDwellingMenu)
    window.addEventListener('blur', closeDwellingMenu)
    stopDwellingMenuWatch = watch([() => props.movements, () => props.routes, () => props.dwellingMovementIDs, () => props.trains, () => props.tracks, () => props.rows], closeDwellingMenu, { deep: true })
    void nextTick(() => {
        if (dwellingMenu.value !== menu) return
        const rect = dwellingMenuElement.value?.getBoundingClientRect()
        if (rect) positionDwellingMenu(menu, rect.width, rect.height)
        dwellingMenuElement.value?.querySelector<HTMLButtonElement>('button')?.focus()
    })
}
function applyDwellingMenu() {
    const menu = dwellingMenu.value
    if (!menu) return
    closeDwellingMenu()
    if (!canEditTrain(menu.target.trainID) || drag.value || trackDrag.value) return
    const train = props.trains.find(item => item.id === menu.target.trainID)
    const target = train && resolveStationPlanDwellingTarget(train, menu.segment, menu.edge, props.movements, props.routes, props.tracks, props.dwellingMovementIDs[train.id] || [])
    if (!target || target.movementID !== menu.target.movementID || target.action !== menu.target.action) return
    menu.trigger?.focus()
    emit('dwelling-edit', menu.target)
}
function trainTitle(train: StationPlanTrain) {
    return [train.label, ...train.visits.filter(visit => rowLabels.value.has(visit.rowKey)).map(visit => `${visit.detail}: ${rowLabels.value.get(visit.rowKey)} · ${stationPlanTimeLabel(visit.arriveMinutes)}`)].join('\n')
}
function setLineMode(mode: 'straight' | 'orthogonal') {
    if (mode === lineMode.value) return
    cancelSegmentDrag()
    activeTimeHandle.value = null
    lineMode.value = mode
}
function toggleTrackExpansion(key: string) {
    if (props.pickMode) return
    const row = props.rows.find(item => item.key === key)
    if (row?.kind !== 'track' || !row.children?.length) return
    cancelSegmentDrag()
    activeTimeHandle.value = null
    if (expandedTrackKeys.value.has(key)) expandedTrackKeys.value.delete(key)
    else expandedTrackKeys.value.add(key)
}
function toggleTrainSelection(trainID: string) {
    if (props.pickMode || suppressClick || drag.value || trackDrag.value) return
    closeDwellingMenu()
    activeTimeHandle.value = null
    if (selectedTrainIDs.value.has(trainID)) selectedTrainIDs.value.delete(trainID)
    else selectedTrainIDs.value.add(trainID)
}
function clearTrainSelection() { if (!props.pickMode && !suppressClick) { cancelSegmentDrag(); activeTimeHandle.value = null; selectedTrainIDs.value.clear() } }
function handleSelectionKeydown(event: KeyboardEvent) {
    if (!viewRoot.value?.getClientRects().length) return
    if (dwellingMenu.value) return
    if (event.key === 'Escape') {
        if (props.pickMode) { event.preventDefault(); emit('pick-cancel') }
        else clearTrainSelection()
        return
    }
    const target = event.target instanceof Element ? event.target : null
    const editingText = !!target?.closest('input, textarea, select, [contenteditable]:not([contenteditable="false"]), [role="textbox"], [role="combobox"], [role="dialog"]')
    const operation = stationPlanHistoryShortcut(event, editingText)
    if (!operation) return
    event.preventDefault()
    if (operation === 'undo' && canUndo.value) emit('undo')
    if (operation === 'redo' && canRedo.value) emit('redo')
}
const canEditTrain = (id: string) => !props.pickMode && props.editable && !props.loading && !props.readOnlyTrainIDs.includes(id)
function makeEdit(train: StationPlanTrain, segment: StationPlanEditableSegment, mode: StationPlanSegmentEdit['mode'], ctrlKey = false): StationPlanSegmentEdit {
    const segments = lines.value.find(line => line.train.id === train.id)?.editableSegments || [segment]
    return { trainID: train.id, segment, mode, startMinutes: segment.startMinutes, endMinutes: segment.endMinutes, rowKey: segment.rowKey, shiftFollowing: !ctrlKey,
        coincidentAnchors: stationPlanCoincidentAnchors(segments, segment, mode) }
}
function alignSegment(event: MouseEvent, train: StationPlanTrain, segment: StationPlanEditableSegment, edge: 'start' | 'end') {
    if (event.button !== 0 || !canEditTrain(train.id) || suppressClick || drag.value?.moved || trackDrag.value) return
    cancelSegmentDrag()
    activeTimeHandle.value = null
    const edit = makeEdit(train, segment, edge, true)
    delete edit.coincidentAnchors
    emit('align', edit)
}
function nudgeSegment(train: StationPlanTrain, segment: StationPlanEditableSegment, edge: 'start' | 'end', seconds: number, ctrlKey = false) {
    if (!canEditTrain(train.id) || drag.value || trackDrag.value) return
    closeDwellingMenu()
    const edit = makeEdit(train, segment, edge, ctrlKey)
    if (edge === 'start') edit.startMinutes += seconds / 60
    else edit.endMinutes += seconds / 60
    emit('edit', edit)
}
function startSegmentDrag(event: PointerEvent, train: StationPlanTrain, segment: StationPlanEditableSegment, mode: StationPlanSegmentEdit['mode']) {
    if (event.button !== 0 || !canEditTrain(train.id) || drag.value || trackDrag.value) return
    closeDwellingMenu()
    event.preventDefault()
    activeTimeHandle.value = null
    trackDropInvalid.value = false
    autoFit.value = false
    drag.value = { train, edit: makeEdit(train, segment, mode, event.ctrlKey), pointerID: event.pointerId, x: event.clientX, lastClientX: event.clientX,
        scrollX: viewport.value?.scrollLeft || 0,
        domain: { ...domain.value }, pixelsPerMinute: pixelsPerMinute.value, moved: false,
        cursor: document.body.style.cursor, userSelect: document.body.style.userSelect }
    dragPreview.value = { ...drag.value.edit }
    document.body.style.cursor = 'ew-resize'
    document.body.style.userSelect = 'none'
    window.addEventListener('pointermove', moveSegmentDrag)
    window.addEventListener('pointerup', finishSegmentDrag)
    window.addEventListener('pointercancel', cancelSegmentDrag)
    window.addEventListener('keydown', updateDragModifier)
    window.addEventListener('keyup', updateDragModifier)
    window.addEventListener('blur', cancelSegmentDrag)
}
function moveSegmentDrag(event: PointerEvent) {
    const current = drag.value
    if (!current || event.pointerId !== current.pointerID) return
    current.lastClientX = event.clientX
    updateDragPreview(event.ctrlKey)
}
function updateDragModifier(event: KeyboardEvent) {
    if (event.key === 'Control') updateDragPreview(event.ctrlKey)
}
function updateDragPreview(ctrlKey: boolean) {
    const current = drag.value
    if (!current) return
    const dx = current.lastClientX - current.x + (viewport.value?.scrollLeft || 0) - current.scrollX
    current.moved ||= Math.abs(dx) > 3
    const edit = { ...current.edit, shiftFollowing: !ctrlKey }
    const delta = Math.round(dx / current.pixelsPerMinute * 60) / 60
    if (edit.mode === 'start') edit.startMinutes += delta
    else edit.endMinutes += delta
    dragPreview.value = edit
}
function stopSegmentDrag() {
    const current = drag.value
    if (!current) return
    drag.value = null
    dragPreview.value = null
    document.body.style.cursor = current.cursor
    document.body.style.userSelect = current.userSelect
    window.removeEventListener('pointermove', moveSegmentDrag)
    window.removeEventListener('pointerup', finishSegmentDrag)
    window.removeEventListener('pointercancel', cancelSegmentDrag)
    window.removeEventListener('keydown', updateDragModifier)
    window.removeEventListener('keyup', updateDragModifier)
    window.removeEventListener('blur', cancelSegmentDrag)
    if (current.moved) { suppressClick = true; window.setTimeout(() => { suppressClick = false }, 0) }
}
function cancelSegmentDrag() { closeDwellingMenu(); stopSegmentDrag(); stopTrackDrag(); trackDropInvalid.value = false }
function finishSegmentDrag(event: PointerEvent) {
    if (!drag.value || drag.value.pointerID !== event.pointerId) return
    moveSegmentDrag(event)
    const current = drag.value, edit = dragPreview.value
    stopSegmentDrag()
    if (current.moved && edit && (edit.startMinutes !== current.edit.startMinutes || edit.endMinutes !== current.edit.endMinutes)) emit('edit', edit)
}
function previewTrackDrag(train: StationPlanTrain): StationPlanTrain {
    const current = trackDrag.value
    const target = props.tracks.find(track => track.id === current?.targetTrackID)
    if (!current || !target || target.id === current.source.id) return train
    const indexes = new Set(current.visitIndexes)
    const nodeKeys = new Map([[`node:${current.source.fromNodeID}`, `node:${target.fromNodeID}`], [`node:${current.source.toNodeID}`, `node:${target.toNodeID}`]])
    return { ...train, visits: train.visits.map((visit, index) => indexes.has(index) ? { ...visit, rowKey: nodeKeys.get(visit.rowKey) || visit.rowKey } : visit) }
}
function startTrackDrag(event: PointerEvent, train: StationPlanTrain, segment: StationPlanTrackSegment, source: StationPlanTrack) {
    if (event.button !== 0 || !canEditTrain(train.id) || !selectedTrainIDs.value.has(train.id) || drag.value || trackDrag.value || !viewport.value) return
    if (groupedTracks.value.nodes.get(segment.startRowKey)?.id !== source.id || groupedTracks.value.nodes.get(segment.endRowKey)?.id !== source.id) return
    const indexes = stationPlanTrackVisitIndexes(train, segment, source)
    if (!indexes.length) return
    closeDwellingMenu()
    event.preventDefault()
    activeTimeHandle.value = null
    trackDropInvalid.value = false
    autoFit.value = false
    trackDrag.value = { trainID: train.id, segment, source: { ...source }, visitIndexes: indexes, targetTrackID: source.id,
        pointerID: event.pointerId, y: event.clientY, lastClientY: event.clientY, scrollY: viewport.value.scrollTop, viewportTop: viewport.value.getBoundingClientRect().top, moved: false,
        cursor: document.body.style.cursor, userSelect: document.body.style.userSelect }
    document.body.style.cursor = 'ns-resize'
    document.body.style.userSelect = 'none'
    window.addEventListener('pointermove', moveTrackDrag)
    window.addEventListener('pointerup', finishTrackDrag)
    window.addEventListener('pointercancel', cancelSegmentDrag)
    window.addEventListener('blur', cancelSegmentDrag)
    window.addEventListener('keydown', handleTrackDragKeydown)
    window.addEventListener('scroll', updateTrackDragTarget, true)
}
function trackAtClientY(clientY: number): StationPlanTrack | null {
    if (!viewport.value) return null
    const bounds = viewport.value.getBoundingClientRect()
    if (clientY < bounds.top || clientY >= bounds.bottom) return null
    const index = Math.floor((clientY - bounds.top + viewport.value.scrollTop - chartPadding.value.y) / rowPitch.value)
    const row = displayRows.value[index]
    return row ? groupedTracks.value.rows.get(row.key) || null : null
}
function updateTrackDragTarget() {
    const current = trackDrag.value
    if (!current) return
    current.moved ||= Math.abs(current.lastClientY - current.y + (viewport.value?.scrollTop || 0) - current.scrollY - ((viewport.value?.getBoundingClientRect().top ?? current.viewportTop) - current.viewportTop)) > 3
    current.targetTrackID = trackAtClientY(current.lastClientY)?.id || null
}
function moveTrackDrag(event: PointerEvent) {
    if (!trackDrag.value || event.pointerId !== trackDrag.value.pointerID) return
    trackDrag.value.lastClientY = event.clientY
    updateTrackDragTarget()
}
function handleTrackDragKeydown(event: KeyboardEvent) {
    if (event.key === 'Escape') cancelSegmentDrag()
}
function stopTrackDrag() {
    const current = trackDrag.value
    if (!current) return
    trackDrag.value = null
    document.body.style.cursor = current.cursor
    document.body.style.userSelect = current.userSelect
    window.removeEventListener('pointermove', moveTrackDrag)
    window.removeEventListener('pointerup', finishTrackDrag)
    window.removeEventListener('pointercancel', cancelSegmentDrag)
    window.removeEventListener('blur', cancelSegmentDrag)
    window.removeEventListener('keydown', handleTrackDragKeydown)
    window.removeEventListener('scroll', updateTrackDragTarget, true)
    if (current.moved) { suppressClick = true; window.setTimeout(() => { suppressClick = false }, 0) }
}
function finishTrackDrag(event: PointerEvent) {
    if (!trackDrag.value || event.pointerId !== trackDrag.value.pointerID) return
    moveTrackDrag(event)
    const current = trackDrag.value
    stopTrackDrag()
    if (!current.moved || !canEditTrain(current.trainID)) return
    if (!current.targetTrackID) { trackDropInvalid.value = true; return }
    if (current.targetTrackID !== current.source.id) emit('track-edit', {
        trainID: current.trainID, segment: current.segment, sourceTrackID: current.source.id, targetTrackID: current.targetTrackID,
    })
}
function syncScroll() {
    closeDwellingMenu()
    if (!viewport.value) return
    const width = viewport.value.offsetWidth - viewport.value.clientWidth
    const height = viewport.value.offsetHeight - viewport.value.clientHeight
    if (width !== scrollbarGutter.value.width || height !== scrollbarGutter.value.height) {
        scrollbarGutter.value = { width, height }
        void nextTick(syncScroll)
    }
    if (timeViewport.value) timeViewport.value.scrollLeft = viewport.value.scrollLeft
    if (rowViewport.value) rowViewport.value.scrollTop = viewport.value.scrollTop
    updateTrackDragTarget()
}
function fitScale(width: number, height: number) {
    if (!displayRows.value.length) return
    scaleX.value = Math.max(0.001, (width - chartPadding.value.x * 2 - 1) / ((domain.value.end - domain.value.start) * 6))
    scaleY.value = Math.max(0.01, (height - chartPadding.value.y * 2 - 1) / (displayRows.value.length * 52))
}
function fitToViewport() {
    if (drag.value || trackDrag.value) return
    if (!viewport.value || !displayRows.value.length) return
    if (viewport.value.clientWidth <= 0 || viewport.value.clientHeight <= 0) return
    fitScale(viewport.value.clientWidth, viewport.value.clientHeight)
    viewport.value.scrollLeft = 0
    viewport.value.scrollTop = 0
    syncScroll()
}
function enableAutoFit() { cancelSegmentDrag(); activeTimeHandle.value = null; autoFit.value = true; fitToViewport() }
function setScaleX(value: number | number[]) { cancelSegmentDrag(); activeTimeHandle.value = null; autoFit.value = false; scaleX.value = Number(value) }
function setScaleY(value: number | number[]) { cancelSegmentDrag(); activeTimeHandle.value = null; autoFit.value = false; scaleY.value = Number(value) }
function syncAutoFit() { if (autoFit.value) fitToViewport() }
let observer: ResizeObserver | null = null
onMounted(() => {
    window.addEventListener('keydown', handleSelectionKeydown)
    observer = new ResizeObserver(() => { rememberReportSize(); syncAutoFit(); syncScroll() })
    if (viewport.value) observer.observe(viewport.value)
    syncAutoFit()
})
watch([displayRows, domain, chartPadding, () => props.loading], () => { void nextTick(() => {
    if (viewport.value) observer?.observe(viewport.value)
    syncAutoFit()
    syncScroll()
}) }, { flush: 'post' })
watch(() => props.trains.map(train => train.id), ids => {
    const available = new Set(ids)
    for (const id of selectedTrainIDs.value) if (!available.has(id)) selectedTrainIDs.value.delete(id)
})
watch(() => props.selectionScope, () => { cancelSegmentDrag(); activeTimeHandle.value = null; selectedTrainIDs.value.clear(); expandedTrackKeys.value.clear() })
watch(() => props.pickMode, () => { cancelSegmentDrag(); activeTimeHandle.value = null })
watch([displayRows, () => props.trains, () => props.tracks, () => props.editable, () => props.loading, () => props.readOnlyTrainIDs, scaleX, scaleY], cancelSegmentDrag)
watch([contentWidth, contentHeight], () => { void nextTick(syncScroll) }, { flush: 'post' })
onBeforeUnmount(() => {
    cancelSegmentDrag()
    observer?.disconnect()
    window.removeEventListener('keydown', handleSelectionKeydown)
})
let lastReportSize = { width: 0, height: 0, viewportWidth: 0, viewportHeight: 0 }
function rememberReportSize() {
    const bounds = viewRoot.value?.getBoundingClientRect()
    if (bounds && bounds.width > 0 && bounds.height > 0 && viewport.value?.clientWidth && viewport.value.clientHeight) {
        lastReportSize = { width: bounds.width, height: bounds.height,
            viewportWidth: viewport.value.clientWidth, viewportHeight: viewport.value.clientHeight }
    }
}
function getReportViewState(): StationPlanReportViewState {
    rememberReportSize()
    return { scaleX: scaleX.value, scaleY: scaleY.value, lineMode: lineMode.value,
        expandedTrackKeys: [...expandedTrackKeys.value], autoFit: autoFit.value,
        ...lastReportSize }
}

async function applyReportViewState(state: StationPlanReportViewState): Promise<void> {
    cancelSegmentDrag()
    // Keep captured scales even if a ResizeObserver runs during an offscreen mount.
    autoFit.value = false
    expandedTrackKeys.value = new Set(state.expandedTrackKeys)
    lineMode.value = state.lineMode
    scaleX.value = state.scaleX
    scaleY.value = state.scaleY
    const width = state.viewportWidth || viewport.value?.clientWidth || 0
    const height = state.viewportHeight || viewport.value?.clientHeight || 0
    if (state.autoFit && width > 0 && height > 0) fitScale(width, height)
    await nextTick()
    syncScroll()
}

/** Snapshot actual chart vectors and HTML axes at the current scale. */
function exportReportFigure(caption: string, labels: StationPlanReportLabels = {}): ReportFigure | null {
    const root = viewRoot.value
    const canvas = root?.querySelector<SVGSVGElement>('.station-plan-canvas')
    const timeAxis = root?.querySelector<SVGSVGElement>('.station-plan-time-axis')
    const corner = root?.querySelector<HTMLElement>('.station-plan-corner')
    const rowAxis = root?.querySelector<HTMLElement>('.station-plan-row-axis')
    const grid = root?.querySelector<HTMLElement>('.station-plan-grid')
    if (!root || !canvas || !timeAxis || !corner || !rowAxis || !grid || !rowViewport.value || !timeViewport.value) return null
    if (!hasRenderedLayout(grid)) return null
    // A drag preview is not a saved plan; the caller may retry once it is committed.
    if (dragPreview.value || trackDrag.value) return null
    const document = root.ownerDocument
    const view = document.defaultView!
    const gridStyle = view.getComputedStyle(grid)
    const border = Number.parseFloat(gridStyle.borderLeftWidth) || 0
    const left = corner.getBoundingClientRect().width
    const top = corner.getBoundingClientRect().height
    const width = left + contentWidth.value + border * 2
    const height = top + contentHeight.value + border * 2
    const svg = createSvgElement(document, 'svg')
    const defs = createSvgElement(document, 'defs')
    svg.appendChild(defs)
    const clip = createSvgElement(document, 'clipPath', { id: 'station-plan-report-grid' })
    clip.appendChild(createSvgElement(document, 'rect', { x: border, y: border, width: width - border * 2, height: height - border * 2,
        rx: Math.max(0, (Number.parseFloat(gridStyle.borderTopLeftRadius) || 0) - border) }))
    defs.appendChild(clip)
    const chart = createSvgElement(document, 'g', { 'clip-path': 'url(#station-plan-report-grid)' })
    svg.appendChild(chart)
    chart.appendChild(createSvgElement(document, 'rect', { x: border, y: border, width: width - border * 2, height: height - border * 2, fill: '#fff' }))

    let gradientID = 0
    const number = (value: string) => Number.parseFloat(value) || 0
    const transparent = (value: string) => !value || value === 'transparent' || /^rgba\([^)]*,\s*0\)$/.test(value)
    const addBox = (element: HTMLElement, x: number, y: number, boxWidth: number, boxHeight: number) => {
        const style = view.getComputedStyle(element)
        if (style.display === 'none' || style.visibility === 'hidden' || boxWidth <= 0 || boxHeight <= 0) return
        let fill = style.backgroundColor
        const colors = style.backgroundImage.match(/rgba?\([^)]+\)|#[\da-f]+/gi)
        if (style.backgroundImage.startsWith('linear-gradient(') && colors && colors.length >= 2) {
            const id = `station-plan-report-gradient-${gradientID++}`
            const horizontal = style.backgroundImage.startsWith('linear-gradient(90deg')
            const gradient = createSvgElement(document, 'linearGradient', { id, x1: '0%', y1: '0%', x2: horizontal ? '100%' : '0%', y2: horizontal ? '0%' : '100%' })
            colors.forEach((color, index) => gradient.appendChild(createSvgElement(document, 'stop', { offset: `${index / (colors.length - 1) * 100}%`, 'stop-color': color })))
            defs.appendChild(gradient)
            fill = `url(#${id})`
        }
        if (!transparent(fill)) chart.appendChild(createSvgElement(document, 'rect', { x, y, width: boxWidth, height: boxHeight, fill }))
        for (const edge of ['Top', 'Right', 'Bottom', 'Left'] as const) {
            const thickness = number(style[`border${edge}Width`])
            const color = style[`border${edge}Color`]
            if (!thickness || transparent(color) || style[`border${edge}Style`] === 'none') continue
            const vertical = edge === 'Left' || edge === 'Right'
            const a = edge === 'Right' ? x + boxWidth - thickness / 2 : vertical ? x + thickness / 2 : x
            const b = edge === 'Bottom' ? y + boxHeight - thickness / 2 : vertical ? y : y + thickness / 2
            const line = createSvgElement(document, 'line', { x1: a, y1: b, x2: a + (vertical ? 0 : boxWidth), y2: b + (vertical ? boxHeight : 0), stroke: color, 'stroke-width': thickness })
            if (style[`border${edge}Style`] === 'dashed') line.setAttribute('stroke-dasharray', `${thickness * 3} ${thickness * 2}`)
            chart.appendChild(line)
        }
    }
    const measure = document.createElement('canvas').getContext('2d')
    const addHtmlContents = (element: HTMLElement, originX: number, originY: number) => {
        const style = view.getComputedStyle(element)
        if (style.display === 'none' || style.visibility === 'hidden') return
        const bounds = element.getBoundingClientRect()
        addBox(element, bounds.left + originX, bounds.top + originY, bounds.width, bounds.height)
        for (const node of Array.from(element.childNodes)) {
            if (node.nodeType === 1) { addHtmlContents(node as HTMLElement, originX, originY); continue }
            if (node.nodeType !== 3 || !node.textContent?.trim()) continue
            const range = document.createRange()
            range.selectNodeContents(node)
            const bounds = range.getBoundingClientRect()
            if (!bounds.width || !bounds.height) continue
            let label = node.textContent.replace(/\s+/g, ' ')
            let reportLabel: string | undefined
            let reportWidth: number | undefined
            if (element.matches('.station-plan-row-label > span:last-child, .station-plan-track-label > span:last-child')) {
                const owner = element.closest<HTMLElement>('[data-track-key], [data-row-key]')
                const key = owner?.dataset.trackKey || owner?.dataset.rowKey
                if (key && labels.rows && Object.prototype.hasOwnProperty.call(labels.rows, key)) reportLabel = labels.rows[key]
            }
            if (reportLabel !== undefined) {
                label = reportLabel
                // An original short ID gives its text span an intrinsic width of
                // just a few pixels. Report names can use the remaining row-axis
                // space without moving the label or changing any chart geometry.
                const row = element.closest<HTMLElement>('.station-plan-row-label, .station-plan-expanded-track-label')
                if (row) {
                    const rowStyle = view.getComputedStyle(row)
                    reportWidth = Math.max(0, row.getBoundingClientRect().right - number(rowStyle.paddingRight) - number(rowStyle.borderRightWidth) - bounds.left)
                }
            }
            let baseline = number(style.fontSize) * 0.8
            if (measure) {
                measure.font = `${style.fontStyle} ${style.fontWeight} ${style.fontSize} ${style.fontFamily}`
                const metrics = measure.measureText(label)
                const ascent = metrics.fontBoundingBoxAscent
                const descent = metrics.fontBoundingBoxDescent
                baseline = Number.isFinite(ascent + descent) && ascent + descent > 0 ? bounds.height * ascent / (ascent + descent) : baseline
                label = snapshotHtmlText(label, element, value => measure.measureText(value).width, reportLabel !== undefined, reportWidth)
            }
            const text = createSvgElement(document, 'text', { x: bounds.left + originX, y: bounds.top + originY + baseline,
                fill: style.color, 'font-family': style.fontFamily, 'font-size': style.fontSize,
                'font-weight': style.fontWeight, 'font-style': style.fontStyle,
                'letter-spacing': style.letterSpacing, 'word-spacing': style.wordSpacing })
            text.textContent = label
            chart.appendChild(text)
        }
    }
    // Origins are relative to the axis itself, so scrolling cannot crop the report.
    const cornerBounds = corner.getBoundingClientRect()
    addHtmlContents(corner, border - cornerBounds.left, border - cornerBounds.top)
    addBox(timeViewport.value, border + left, border, contentWidth.value, top)
    addBox(rowViewport.value, border, border + top, left, contentHeight.value)
    const rowBounds = rowAxis.getBoundingClientRect()
    addHtmlContents(rowAxis, border - rowBounds.left, border + top - rowBounds.top)
    const header = cloneSvgWithStyles(timeAxis)
    header.setAttribute('x', String(border + left))
    header.setAttribute('y', String(border))
    header.style.setProperty('overflow', 'hidden')
    chart.appendChild(header)
    const body = cloneSvgWithStyles(canvas, { exclude: '.station-plan-train-hit-area, .station-plan-track-drag, .station-plan-segment-controls, .station-plan-track-target, .station-plan-pick-layer' })
    if (labels.trains) {
        for (const train of Array.from(body.querySelectorAll<SVGGElement>('.station-plan-train[data-train-id]'))) {
            const id = train.dataset.trainId
            if (!id || !Object.prototype.hasOwnProperty.call(labels.trains, id)) continue
            for (const text of Array.from(train.querySelectorAll('text'))) text.textContent = labels.trains[id]!
        }
    }
    body.setAttribute('x', String(border + left))
    body.setAttribute('y', String(border + top))
    chart.appendChild(body)
    if (border) svg.appendChild(createSvgElement(document, 'rect', { x: border / 2, y: border / 2,
        width: width - border, height: height - border, rx: number(gridStyle.borderTopLeftRadius),
        fill: 'none', stroke: gridStyle.borderLeftColor, 'stroke-width': border }))
    stripReportInteractionMetadata(svg)
    return serializeReportSvg(svg, caption, width, height)
}

defineExpose({ viewport, fitToViewport, getReportViewState, applyReportViewState, exportReportFigure })
</script>

<style scoped>
.station-plan-view { position: relative; flex: 1; display: flex; flex-direction: column; min-width: 0; min-height: 0; }
.station-plan-toolbar { display: flex; align-items: center; flex-wrap: wrap; gap: 12px; padding: 8px 12px; background: #f8fafc; border-bottom: 1px solid var(--el-border-color-lighter); }
.station-plan-scale { display: flex; align-items: center; gap: 8px; font-size: 12px; white-space: nowrap; }
.station-plan-scale :deep(.el-slider) { width: 150px; margin: 0 6px; }
.station-plan-scale output { width: 44px; font-variant-numeric: tabular-nums; }
.station-plan-line-mode { display: flex; align-items: center; gap: 4px; font-size: 12px; white-space: nowrap; }
.station-plan-line-mode > span { margin-right: 4px; }
.station-plan-line-mode button { padding: 4px 8px; border: 1px solid var(--el-border-color); border-radius: 4px; color: var(--el-text-color-regular); background: #fff; font: inherit; cursor: pointer; }
.station-plan-line-mode button[aria-pressed='true'] { color: var(--el-color-primary); border-color: var(--el-color-primary); background: var(--el-color-primary-light-9); }
.station-plan-line-mode button:focus-visible { outline: 2px solid var(--el-color-primary); outline-offset: 2px; }
.station-plan-history-count { color: var(--el-text-color-secondary); font-size: 12px; font-variant-numeric: tabular-nums; white-space: nowrap; }
.station-plan-grid { flex: 1; min-height: 0; display: grid; grid-template-columns: 180px minmax(0, 1fr); grid-template-rows: 50px minmax(0, 1fr); overflow: hidden; border: 1px solid var(--el-border-color); border-radius: 8px; }
.station-plan-corner { display: flex; align-items: center; gap: 14px; padding: 0 12px; font-size: 12px; font-weight: 600; background: #f8fafc; border-right: 1px solid var(--el-border-color); border-bottom: 1px solid var(--el-border-color); }
.station-plan-row-kind { display: inline-flex; align-items: center; gap: 5px; white-space: nowrap; }
.station-plan-row-kind.is-track-row { color: #0f766e; }
.station-plan-row-kind.is-node-row { color: #64748b; }
.station-plan-row-kind i { width: 12px; border-top: 2px solid currentColor; }
.station-plan-row-kind.is-node-row i { border-top-style: dashed; }
.station-plan-time-viewport { overflow: hidden; border-bottom: 1px solid var(--el-border-color); background: linear-gradient(180deg, #fff, #f6f9fe); }
.station-plan-time-axis { display: block; }
.station-plan-hour-label { fill: #606266; font-size: 11px; font-weight: 600; text-anchor: middle; }
.station-plan-row-viewport { overflow: hidden; border-right: 1px solid var(--el-border-color); background: linear-gradient(90deg, #fff, #f9fbff); }
.station-plan-row-axis { position: relative; }
.station-plan-axis-row-band { position: absolute; left: 0; right: 0; box-sizing: border-box; pointer-events: none; }
.station-plan-axis-row-band.is-track-row { background: #f0fdfa; border-left: 3px solid #0f766e; }
.station-plan-row-label { position: absolute; width: 100%; box-sizing: border-box; padding: 0 12px; display: flex; transform: translateY(-50%); color: #64748b; font-size: 12px; }
.station-plan-row-label.is-track-row { color: #0f766e; }
.station-plan-row-label > span:last-child { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.station-plan-row-label.is-track-node { padding-left: 26px; }
.station-plan-row-label.is-drop-target { color: var(--el-color-primary); }
.station-plan-track-label { display: flex; align-items: center; gap: 5px; min-width: 0; padding: 0; border: 0; color: inherit; background: transparent; font: inherit; font-weight: 600; cursor: pointer; text-align: left; }
.station-plan-track-label > span:last-child { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.station-plan-track-label:focus-visible { outline: 2px solid var(--el-color-primary); outline-offset: 2px; }
.station-plan-expanded-track-label { position: absolute; left: 12px; right: 12px; transform: translateY(-50%); color: #0f766e; font-size: 10px; }
.station-plan-expanded-track-label.is-compact { left: 8px; right: auto; }
.station-plan-expanded-track-label.is-compact > span:last-child { display: none; }
.station-plan-canvas-viewport { overflow: auto; min-width: 0; min-height: 0; background: #fff; overscroll-behavior: contain; }
.station-plan-canvas { display: block; user-select: none; }
.station-plan-row-band { fill: transparent; pointer-events: none; }
.station-plan-row-band.is-track-row { fill: #f0fdfa; }
.station-plan-time-line, .station-plan-row-line { stroke-width: 0.75; vector-effect: non-scaling-stroke; }
.station-plan-time-line { stroke: #cbd5e1; }
.station-plan-time-line.is-hour { stroke: #94a3b8; stroke-width: 1.5; }
.station-plan-time-line.is-half-hour { stroke-width: 1; stroke-dasharray: 4 4; }
.station-plan-row-line[data-row-kind='track'] { stroke: #0f766e; stroke-width: 1.1; }
.station-plan-row-line[data-row-kind='node'] { stroke: #64748b; stroke-opacity: 0.65; stroke-dasharray: 5 4; }
.station-plan-train-line, .station-plan-terminal-line { fill: none; stroke-width: 2; stroke-linejoin: round; vector-effect: non-scaling-stroke; }
.station-plan-train-connector { fill: none; stroke-width: 1; stroke-dasharray: 3 3; opacity: 0.55; vector-effect: non-scaling-stroke; }
.station-plan-train { cursor: pointer; outline: none; }
.station-plan-train:focus-visible .station-plan-train-line { filter: drop-shadow(0 0 2px var(--el-color-primary)); }
.station-plan-train-hit-area { fill: none; stroke: transparent; stroke-width: 12; pointer-events: stroke; vector-effect: non-scaling-stroke; }
.station-plan-track-drag { stroke: transparent; stroke-width: 18; stroke-linecap: round; pointer-events: stroke; cursor: ns-resize; touch-action: none; vector-effect: non-scaling-stroke; }
.station-plan-track-drag.is-disabled { pointer-events: none; cursor: default; }
.station-plan-track-target { fill: var(--el-color-primary); opacity: 0.12; pointer-events: none; }
.station-plan-train.is-selected .station-plan-train-line, .station-plan-train.is-selected .station-plan-terminal-line,
.station-plan-train:hover .station-plan-train-line, .station-plan-train:hover .station-plan-terminal-line { stroke-width: 4; }
.station-plan-train-label { font-size: 11px; font-weight: 600; text-anchor: middle; paint-order: stroke; stroke: #fff; stroke-width: 3px; stroke-linejoin: round; pointer-events: none; }
.station-plan-time-handle { fill: white; stroke-width: 2; cursor: ew-resize; touch-action: none; }
.station-plan-time-handle:hover, .station-plan-time-handle:focus-visible { fill: var(--station-plan-handle-color, #409eff); }
.station-plan-time-handle.is-disabled { cursor: default; opacity: 0.5; }
.station-plan-time-handle:focus-visible { outline: 2px solid var(--el-color-primary); }
.station-plan-dwelling-menu { position: fixed; z-index: 2400; box-sizing: border-box; min-width: min(180px, calc(100vw - 16px)); max-width: calc(100vw - 16px); padding: 5px; border: 1px solid var(--el-border-color, #dcdfe6); border-radius: 6px; background: #fff; box-shadow: 0 6px 20px rgba(15, 23, 42, 0.18); }
.station-plan-dwelling-menu button { display: block; width: 100%; padding: 8px 12px; border: 0; border-radius: 3px; color: var(--el-text-color-primary, #303133); background: transparent; font: inherit; font-size: 13px; text-align: left; cursor: pointer; }
.station-plan-dwelling-menu button:hover, .station-plan-dwelling-menu button:focus-visible { color: var(--el-color-primary, #409eff); background: var(--el-color-primary-light-9, #ecf5ff); outline: none; }
:global(.el-popper.station-plan-handle-tooltip) { max-width: min(320px, calc(100vw - 24px)); padding: 10px 12px; color: #fff; background: rgba(30, 41, 59, 0.82); border: 1px solid rgba(148, 163, 184, 0.4); border-radius: 6px; box-shadow: 0 4px 12px rgba(15, 23, 42, 0.18); pointer-events: none; }
.station-plan-handle-info { display: grid; grid-template-columns: auto minmax(0, 1fr); gap: 4px 12px; line-height: 1.6; font-size: 12px; }
.station-plan-handle-info span { color: rgba(255, 255, 255, 0.75); }
.station-plan-handle-info strong { font-weight: 600; font-variant-numeric: tabular-nums; overflow-wrap: anywhere; }
.station-plan-drag-feedback { position: absolute; right: 16px; top: 58px; z-index: 2; padding: 5px 10px; font-size: 12px; background: #fff; border: 1px solid var(--el-color-primary); border-radius: 4px; pointer-events: none; }
.station-plan-drag-feedback.is-invalid { color: var(--el-color-danger); border-color: var(--el-color-danger); }
.station-plan-pick-surface { fill: transparent; pointer-events: all; cursor: crosshair; }
.station-plan-draft-points { pointer-events: none; }
.station-plan-draft-marker { pointer-events: all; cursor: crosshair; }
.station-plan-draft-line { stroke: var(--el-color-primary); stroke-width: 2; stroke-dasharray: 6 4; fill: none; }
.station-plan-draft-point { fill: #fff; stroke: var(--el-color-primary); stroke-width: 2; }
.station-plan-draft-point-label { fill: var(--el-color-primary); font-size: 12px; font-weight: 600; text-anchor: middle; paint-order: stroke; stroke: #fff; stroke-width: 3px; }
.station-plan-empty { flex: 1; display: grid; place-items: center; color: var(--el-text-color-secondary); font-size: 13px; }
.station-plan-empty-label { fill: #909399; font-size: 12px; }
@media (max-width: 760px) { .station-plan-grid { grid-template-columns: 132px minmax(0, 1fr); } .station-plan-scale :deep(.el-slider) { width: 100px; } }
</style>
