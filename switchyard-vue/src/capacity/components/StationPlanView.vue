<template>
    <div ref="viewRoot" class="station-plan-view">
        <div class="station-plan-toolbar" role="toolbar" :aria-label="t('stationPlanView.toolbar')">
            <ActionButton :icon="FullScreen" :label="t('capacityGantt.fit')" :active="autoFit" :disabled="rows.length === 0" @click="enableAutoFit" />
            <label class="station-plan-scale"><span>{{ t('capacityGantt.scaleX') }}</span>
                <ElSlider :model-value="scaleX" :min="Math.min(0.01, scaleX)" :max="Math.max(4, Math.ceil(scaleX))" :step="0.01" :aria-label="t('capacityGantt.scaleX')" @update:model-value="setScaleX" />
                <output>{{ scaleX.toFixed(2) }}×</output>
            </label>
            <label class="station-plan-scale"><span>{{ t('capacityGantt.scaleY') }}</span>
                <ElSlider :model-value="scaleY" :min="Math.min(0.05, scaleY)" :max="Math.max(4, Math.ceil(scaleY))" :step="0.01" :aria-label="t('capacityGantt.scaleY')" @update:model-value="setScaleY" />
                <output>{{ scaleY.toFixed(2) }}×</output>
            </label>
            <ActionButton :icon="Refresh" :label="t('capacityGantt.refresh')" :loading="loading" :disabled="refreshDisabled" @click="emit('refresh')" />
            <ActionButton :icon="Back" variant="icon-text" :label="t('stationPlanView.undo')" :disabled="!canUndo" aria-keyshortcuts="Control+Z" @click="emit('undo')" />
            <ActionButton :icon="Right" variant="icon-text" :label="t('stationPlanView.redo')" :disabled="!canRedo" aria-keyshortcuts="Control+Y" @click="emit('redo')" />
            <output class="station-plan-history-count" :title="t('stationPlanView.historyHint')" :aria-label="t('stationPlanView.historyCount', { undo: undoCount, redo: redoCount })">{{ undoCount + redoCount }} / 20</output>
            <slot name="actions" />
        </div>
        <div v-if="rows.length === 0" class="station-plan-empty">{{ emptyText || t('stationPlanView.emptyRows') }}</div>
        <div v-else class="station-plan-grid">
            <div class="station-plan-corner">{{ t('stationPlanView.axis') }}</div>
            <div ref="timeViewport" class="station-plan-time-viewport" :style="{ marginRight: `${scrollbarGutter.width}px` }">
                <svg :width="contentWidth" height="50" class="station-plan-time-axis">
                    <text v-for="tick in hourTicks" :key="tick.time" :x="timeToX(tick.time)" y="26" class="station-plan-hour-label" :style="{ textAnchor: tick.time === domain.start ? 'start' : tick.time === domain.end ? 'end' : 'middle' }">{{ tick.label }}</text>
                </svg>
            </div>
            <div ref="rowViewport" class="station-plan-row-viewport" :style="{ marginBottom: `${scrollbarGutter.height}px` }">
                <div :style="{ height: `${contentHeight}px` }" class="station-plan-row-axis">
                    <div v-for="(row, index) in rows" :key="row.key" class="station-plan-row-label" :class="`is-${row.kind}`" :style="{ top: `${rowToY(index)}px` }" :title="row.label">
                        <span class="station-plan-row-kind">{{ t(`stationPlanView.${row.kind}`) }}</span><span>{{ row.label }}</span>
                    </div>
                </div>
            </div>
            <div ref="viewport" class="station-plan-canvas-viewport" @scroll="syncScroll" @click="clearTrainSelection">
                <svg :width="contentWidth" :height="contentHeight" class="station-plan-canvas" role="group" :aria-label="t('stationPlanView.chartLabel')">
                    <g class="station-plan-background" aria-hidden="true">
                        <line v-for="tick in ticks" :key="tick.time" class="station-plan-time-line" :class="`is-${tick.type}`" :x1="timeToX(tick.time)" :x2="timeToX(tick.time)" y1="0" :y2="contentHeight" />
                        <line v-for="(row, index) in rows" :key="row.key" class="station-plan-row-line" :x1="chartPadding.x" :x2="contentWidth - chartPadding.x" :y1="rowToY(index)" :y2="rowToY(index)" />
                    </g>
                    <g v-for="line in lines" :key="line.train.id" class="station-plan-train" :class="{ 'is-selected': selectedTrainIDs.has(line.train.id) }" :data-train-id="line.train.id"
                        role="button" tabindex="0" :aria-label="t('stationPlanView.trainLine', { train: line.train.label })" :aria-pressed="selectedTrainIDs.has(line.train.id)"
                        @click.stop="toggleTrainSelection(line.train.id)" @keydown.enter.stop.prevent="toggleTrainSelection(line.train.id)" @keydown.space.stop.prevent="toggleTrainSelection(line.train.id)">
                        <title>{{ trainTitle(line.train) }}</title>
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
                    <g v-for="line in selectedLines" :key="`drag:${line.train.id}`" :data-control-train-id="line.train.id">
                        <line v-for="(segment, index) in line.editableSegments" :key="index" :data-segment-index="index" :data-row-key="segment.rowKey"
                            :x1="segment.x1" :x2="segment.x2" :y1="segment.y" :y2="segment.y" class="station-plan-segment-drag" :class="{ 'is-editable': canEditTrain(line.train.id) }"
                            @click.stop="toggleTrainSelection(line.train.id)" @pointerdown.stop="startSegmentDrag($event, line.train, segment, 'row')"><title>{{ t('stationPlanView.moveRow') }}</title></line>
                    </g>
                    <!-- Every handle is above every train line and drag area, including overlapping trains. -->
                    <g v-for="line in selectedLines" :key="`handles:${line.train.id}`" class="station-plan-segment-controls" :data-control-train-id="line.train.id">
                        <g v-for="(segment, index) in line.editableSegments" :key="index" :data-segment-index="index" :data-row-key="segment.rowKey">
                            <rect v-for="edge in (['start', 'end'] as const)" :key="edge" :x="(edge === 'start' ? segment.x1 : segment.x2) - 5" :y="segment.y - 5" width="10" height="10"
                                class="station-plan-time-handle" :class="{ 'is-disabled': !canEditTrain(line.train.id) }" :stroke="line.train.color" :data-edge="edge" role="button" tabindex="0"
                                :aria-label="t(`stationPlanView.${edge}Handle`, { train: line.train.label })" :aria-disabled="!canEditTrain(line.train.id)"
                                :aria-describedby="timeHandleInfo && activeTimeHandle?.trainID === line.train.id && activeTimeHandle.segmentIndex === index && activeTimeHandle.edge === edge ? timeHandleTooltipID : undefined"
                                @mouseenter="showTimeHandleInfo($event, line.train.id, index, edge)" @mouseleave="hideTimeHandleInfo"
                                @focus="showTimeHandleInfo($event, line.train.id, index, edge)" @blur="hideTimeHandleInfo"
                                @click.stop @pointerdown.stop="startSegmentDrag($event, line.train, segment, edge)"
                                @keydown.left.stop.prevent="nudgeSegment(line.train, segment, edge, -1)" @keydown.right.stop.prevent="nudgeSegment(line.train, segment, edge, 1)" />
                        </g>
                    </g>
                    <text v-if="lines.length === 0" :x="24" y="26" class="station-plan-empty-label">{{ t('stationPlanView.emptyTrains') }}</text>
                </svg>
            </div>
        </div>
        <ElTooltip :visible="!!timeHandleInfo" :virtual-ref="activeTimeHandle?.element" virtual-triggering placement="top" :show-arrow="false" :enterable="false" :persistent="false" :hide-after="0" transition="" popper-class="station-plan-handle-tooltip">
            <template #content>
                <div v-if="timeHandleInfo" :id="timeHandleTooltipID" class="station-plan-handle-info">
                    <span>{{ t('stationPlanView.handleInfo.trainNumber') }}</span><strong>{{ timeHandleInfo.trainNumber }}</strong>
                    <span>{{ t('stationPlanView.handleInfo.cell') }}</span><strong>{{ timeHandleInfo.cell }}</strong>
                    <span>{{ t('stationPlanView.handleInfo.time') }}</span><strong>{{ timeHandleInfo.time }}</strong>
                </div>
            </template>
        </ElTooltip>
        <div v-if="dragPreview" class="station-plan-drag-feedback" role="status">{{ rowLabels.get(dragPreview.rowKey) }} · {{ stationPlanTimeLabel(dragPreview.startMinutes) }} – {{ stationPlanTimeLabel(dragPreview.endMinutes) }}</div>
        <div v-if="lines.length" class="station-plan-legend" :aria-label="t('stationPlanView.trainLegend')">
            <button v-for="line in lines" :key="line.train.id" type="button" :aria-pressed="selectedTrainIDs.has(line.train.id)" @click="toggleTrainSelection(line.train.id)">
                <span :style="{ background: line.train.color }" />{{ line.train.label }}
            </button>
        </div>
    </div>
</template>

<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, shallowRef, useId, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { ElSlider, ElTooltip } from 'element-plus'
import { Back, FullScreen, Refresh, Right } from '@element-plus/icons-vue'
import ActionButton from '@/components/ui/ActionButton.vue'
import { previewStationPlanSegmentEdit, stationPlanChartPadding, stationPlanDomain, stationPlanTimeGrid, stationPlanTimeLabel, stationPlanTrainPath, type StationPlanAxisRow, type StationPlanTrain, type StationPlanEditableSegment, type StationPlanSegmentEdit } from './stationPlanView'
import { stationPlanHistoryShortcut } from './stationPlanActions'

const props = withDefaults(defineProps<{
    rows: StationPlanAxisRow[]
    trains: StationPlanTrain[]
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
}>(), { startMinutes: null, endMinutes: null, loading: false, refreshDisabled: false, emptyText: '', selectionScope: '', editable: true, readOnlyTrainIDs: () => [], undoCount: 0, redoCount: 0 })
const emit = defineEmits<{ (event: 'refresh'): void; (event: 'edit', edit: StationPlanSegmentEdit): void; (event: 'undo'): void; (event: 'redo'): void }>()
const { t } = useI18n()
const scaleX = defineModel<number>('scaleX', { default: 1 })
const scaleY = defineModel<number>('scaleY', { default: 1 })
const autoFit = ref(true)
const selectedTrainIDs = ref(new Set<string>())
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
    y: number
    scrollX: number
    scrollY: number
    domain: { start: number; end: number }
    pixelsPerMinute: number
    rowPitch: number
    moved: boolean
    cursor: string
    userSelect: string
}
const drag = ref<DragState | null>(null)
const dragPreview = ref<StationPlanSegmentEdit | null>(null)
let suppressClick = false
const domain = computed(() => drag.value?.domain || stationPlanDomain(props.trains, props.startMinutes, props.endMinutes))
const ticks = computed(() => stationPlanTimeGrid(domain.value.start, domain.value.end))
const hourTicks = computed(() => ticks.value.filter(tick => tick.type === 'hour'))
const pixelsPerMinute = computed(() => 6 * scaleX.value)
const rowPitch = computed(() => 52 * scaleY.value)
const chartPadding = computed(() => stationPlanChartPadding(props.trains))
const contentWidth = computed(() => chartPadding.value.x * 2 + (domain.value.end - domain.value.start) * pixelsPerMinute.value)
const contentHeight = computed(() => chartPadding.value.y * 2 + props.rows.length * rowPitch.value)
const lines = computed(() => props.trains.map(source => {
    const train = dragPreview.value?.trainID === source.id ? previewStationPlanSegmentEdit(source, dragPreview.value) : source
    return { train, ...stationPlanTrainPath(train, props.rows, domain.value.start, pixelsPerMinute.value, rowPitch.value, chartPadding.value) }
}).filter(line => line.path))
const selectedLines = computed(() => lines.value.filter(line => selectedTrainIDs.value.has(line.train.id)))
const canUndo = computed(() => props.editable && !props.loading && !drag.value && props.undoCount > 0)
const canRedo = computed(() => props.editable && !props.loading && !drag.value && props.redoCount > 0)
const rowLabels = computed(() => new Map(props.rows.map(row => [row.key, row.label])))
const timeHandleTooltipID = `station-plan-time-handle-${useId()}`
const activeTimeHandle = shallowRef<{ element: SVGRectElement; trainID: string; segmentIndex: number; edge: 'start' | 'end' } | null>(null)
const timeHandleInfo = computed(() => {
    const handle = activeTimeHandle.value
    if (!handle || drag.value) return null
    const line = selectedLines.value.find(item => item.train.id === handle.trainID)
    const segment = line?.editableSegments[handle.segmentIndex]
    if (!line || !segment) return null
    return {
        trainNumber: line.train.label,
        cell: rowLabels.value.get(segment.rowKey) || segment.rowKey,
        time: stationPlanTimeLabel(handle.edge === 'start' ? segment.startMinutes : segment.endMinutes),
    }
})
const timeToX = (time: number) => chartPadding.value.x + (time - domain.value.start) * pixelsPerMinute.value
const rowToY = (index: number) => chartPadding.value.y + (index + 0.5) * rowPitch.value

function showTimeHandleInfo(event: Event, trainID: string, segmentIndex: number, edge: 'start' | 'end') {
    if (event.currentTarget instanceof SVGRectElement && !drag.value) activeTimeHandle.value = { element: event.currentTarget, trainID, segmentIndex, edge }
}
function hideTimeHandleInfo(event: Event) {
    if (activeTimeHandle.value?.element === event.currentTarget) activeTimeHandle.value = null
}
function trainTitle(train: StationPlanTrain) {
    return [train.label, ...train.visits.filter(visit => rowLabels.value.has(visit.rowKey)).map(visit => `${rowLabels.value.get(visit.rowKey)}: ${stationPlanTimeLabel(visit.arriveMinutes)} - ${stationPlanTimeLabel(visit.departMinutes)} (${visit.detail})`)].join('\n')
}
function toggleTrainSelection(trainID: string) {
    if (suppressClick || drag.value) return
    activeTimeHandle.value = null
    if (selectedTrainIDs.value.has(trainID)) selectedTrainIDs.value.delete(trainID)
    else selectedTrainIDs.value.add(trainID)
}
function clearTrainSelection() { if (!suppressClick) { cancelSegmentDrag(); activeTimeHandle.value = null; selectedTrainIDs.value.clear() } }
function handleSelectionKeydown(event: KeyboardEvent) {
    if (!viewRoot.value?.getClientRects().length) return
    if (event.key === 'Escape') { clearTrainSelection(); return }
    const target = event.target instanceof Element ? event.target : null
    const editingText = !!target?.closest('input, textarea, select, [contenteditable]:not([contenteditable="false"]), [role="textbox"], [role="combobox"], [role="dialog"]')
    const operation = stationPlanHistoryShortcut(event, editingText)
    if (!operation) return
    event.preventDefault()
    if (operation === 'undo' && canUndo.value) emit('undo')
    if (operation === 'redo' && canRedo.value) emit('redo')
}
const canEditTrain = (id: string) => props.editable && !props.loading && !props.readOnlyTrainIDs.includes(id)
function makeEdit(train: StationPlanTrain, segment: StationPlanEditableSegment, mode: StationPlanSegmentEdit['mode']): StationPlanSegmentEdit {
    return { trainID: train.id, segment, mode, startMinutes: segment.startMinutes, endMinutes: segment.endMinutes, rowKey: segment.rowKey }
}
function nudgeSegment(train: StationPlanTrain, segment: StationPlanEditableSegment, edge: 'start' | 'end', seconds: number) {
    if (!canEditTrain(train.id) || drag.value) return
    const edit = makeEdit(train, segment, edge)
    if (edge === 'start') edit.startMinutes += seconds / 60
    else edit.endMinutes += seconds / 60
    emit('edit', edit)
}
function startSegmentDrag(event: PointerEvent, train: StationPlanTrain, segment: StationPlanEditableSegment, mode: StationPlanSegmentEdit['mode']) {
    if (event.button !== 0 || !canEditTrain(train.id) || drag.value) return
    event.preventDefault()
    activeTimeHandle.value = null
    autoFit.value = false
    drag.value = { train, edit: makeEdit(train, segment, mode), pointerID: event.pointerId, x: event.clientX, y: event.clientY,
        scrollX: viewport.value?.scrollLeft || 0, scrollY: viewport.value?.scrollTop || 0,
        domain: { ...domain.value }, pixelsPerMinute: pixelsPerMinute.value, rowPitch: rowPitch.value, moved: false,
        cursor: document.body.style.cursor, userSelect: document.body.style.userSelect }
    dragPreview.value = { ...drag.value.edit }
    document.body.style.cursor = mode === 'row' ? 'ns-resize' : 'grabbing'
    document.body.style.userSelect = 'none'
    window.addEventListener('pointermove', moveSegmentDrag)
    window.addEventListener('pointerup', finishSegmentDrag)
    window.addEventListener('pointercancel', cancelSegmentDrag)
}
function moveSegmentDrag(event: PointerEvent) {
    const current = drag.value
    if (!current || event.pointerId !== current.pointerID) return
    const dx = event.clientX - current.x + (viewport.value?.scrollLeft || 0) - current.scrollX
    const dy = event.clientY - current.y + (viewport.value?.scrollTop || 0) - current.scrollY
    current.moved ||= Math.abs(dx) > 3 || Math.abs(dy) > 3
    const edit = { ...current.edit }
    if (edit.mode === 'row') {
        const index = props.rows.findIndex(row => row.key === edit.segment.rowKey) + Math.round(dy / current.rowPitch)
        const target = props.rows[index]
        if (target?.kind === 'cell') edit.rowKey = target.key
    } else {
        const delta = Math.round(dx / current.pixelsPerMinute * 60) / 60
        if (edit.mode === 'start') edit.startMinutes += delta
        else edit.endMinutes += delta
    }
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
    if (current.moved) { suppressClick = true; window.setTimeout(() => { suppressClick = false }, 0) }
}
function cancelSegmentDrag() { stopSegmentDrag() }
function finishSegmentDrag(event: PointerEvent) {
    if (!drag.value || drag.value.pointerID !== event.pointerId) return
    moveSegmentDrag(event)
    const current = drag.value, edit = dragPreview.value
    stopSegmentDrag()
    if (current.moved && edit && (edit.startMinutes !== current.edit.startMinutes || edit.endMinutes !== current.edit.endMinutes || edit.rowKey !== current.edit.rowKey)) emit('edit', edit)
}
function syncScroll() {
    if (!viewport.value) return
    const width = viewport.value.offsetWidth - viewport.value.clientWidth
    const height = viewport.value.offsetHeight - viewport.value.clientHeight
    if (width !== scrollbarGutter.value.width || height !== scrollbarGutter.value.height) {
        scrollbarGutter.value = { width, height }
        void nextTick(syncScroll)
    }
    if (timeViewport.value) timeViewport.value.scrollLeft = viewport.value.scrollLeft
    if (rowViewport.value) rowViewport.value.scrollTop = viewport.value.scrollTop
}
function fitToViewport() {
    if (drag.value) return
    if (!viewport.value || !props.rows.length) return
    scaleX.value = Math.max(0.001, (viewport.value.clientWidth - chartPadding.value.x * 2 - 1) / ((domain.value.end - domain.value.start) * 6))
    scaleY.value = Math.max(0.01, (viewport.value.clientHeight - chartPadding.value.y * 2 - 1) / (props.rows.length * 52))
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
    observer = new ResizeObserver(() => { syncAutoFit(); syncScroll() })
    if (viewport.value) observer.observe(viewport.value)
    syncAutoFit()
})
watch([() => props.rows, domain, chartPadding, () => props.loading], () => { void nextTick(() => {
    if (viewport.value) observer?.observe(viewport.value)
    syncAutoFit()
    syncScroll()
}) }, { flush: 'post' })
watch(() => props.trains.map(train => train.id), ids => {
    const available = new Set(ids)
    for (const id of selectedTrainIDs.value) if (!available.has(id)) selectedTrainIDs.value.delete(id)
})
watch(() => props.selectionScope, () => { cancelSegmentDrag(); activeTimeHandle.value = null; selectedTrainIDs.value.clear() })
watch([() => props.rows, () => props.trains, () => props.editable], cancelSegmentDrag)
watch([contentWidth, contentHeight], () => { void nextTick(syncScroll) }, { flush: 'post' })
onBeforeUnmount(() => {
    cancelSegmentDrag()
    observer?.disconnect()
    window.removeEventListener('keydown', handleSelectionKeydown)
})
defineExpose({ viewport, fitToViewport })
</script>

<style scoped>
.station-plan-view { position: relative; flex: 1; display: flex; flex-direction: column; min-width: 0; min-height: 0; }
.station-plan-toolbar { display: flex; align-items: center; flex-wrap: wrap; gap: 12px; padding: 8px 12px; background: #f8fafc; border-bottom: 1px solid var(--el-border-color-lighter); }
.station-plan-scale { display: flex; align-items: center; gap: 8px; font-size: 12px; white-space: nowrap; }
.station-plan-scale :deep(.el-slider) { width: 150px; margin: 0 6px; }
.station-plan-scale output { width: 44px; font-variant-numeric: tabular-nums; }
.station-plan-history-count { color: var(--el-text-color-secondary); font-size: 12px; font-variant-numeric: tabular-nums; white-space: nowrap; }
.station-plan-grid { flex: 1; min-height: 0; display: grid; grid-template-columns: 180px minmax(0, 1fr); grid-template-rows: 50px minmax(0, 1fr); overflow: hidden; border: 1px solid var(--el-border-color); border-radius: 8px; }
.station-plan-corner { display: flex; align-items: center; padding: 0 12px; font-size: 12px; font-weight: 600; background: linear-gradient(180deg, #f9fbff, #eef3fa); border-right: 1px solid var(--el-border-color); border-bottom: 1px solid var(--el-border-color); }
.station-plan-time-viewport { overflow: hidden; border-bottom: 1px solid var(--el-border-color); background: linear-gradient(180deg, #fff, #f6f9fe); }
.station-plan-time-axis { display: block; }
.station-plan-hour-label { fill: #606266; font-size: 11px; font-weight: 600; text-anchor: middle; }
.station-plan-row-viewport { overflow: hidden; border-right: 1px solid var(--el-border-color); background: linear-gradient(90deg, #fff, #f9fbff); }
.station-plan-row-axis { position: relative; }
.station-plan-row-label { position: absolute; width: 100%; box-sizing: border-box; padding: 0 12px; display: flex; flex-direction: column; gap: 3px; transform: translateY(-50%); font-size: 12px; }
.station-plan-row-label > span:last-child { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.station-plan-row-kind { color: #909399; font-size: 10px; }
.station-plan-row-label.is-endpoint .station-plan-row-kind { color: #409eff; }
.station-plan-canvas-viewport { overflow: auto; min-width: 0; min-height: 0; background: #fff; overscroll-behavior: contain; }
.station-plan-canvas { display: block; user-select: none; }
.station-plan-time-line, .station-plan-row-line { stroke: #59b292; stroke-width: 0.75; vector-effect: non-scaling-stroke; }
.station-plan-time-line.is-hour { stroke-width: 1.5; }
.station-plan-time-line.is-half-hour { stroke-width: 1; stroke-dasharray: 4 4; }
.station-plan-train-line, .station-plan-terminal-line { fill: none; stroke-width: 2; stroke-linejoin: round; vector-effect: non-scaling-stroke; }
.station-plan-train { cursor: pointer; outline: none; }
.station-plan-train:focus-visible .station-plan-train-line { filter: drop-shadow(0 0 2px var(--el-color-primary)); }
.station-plan-train-hit-area { fill: none; stroke: transparent; stroke-width: 12; pointer-events: stroke; vector-effect: non-scaling-stroke; }
.station-plan-train.is-selected .station-plan-train-line, .station-plan-train.is-selected .station-plan-terminal-line,
.station-plan-train:hover .station-plan-train-line, .station-plan-train:hover .station-plan-terminal-line { stroke-width: 4; }
.station-plan-train-label { font-size: 11px; font-weight: 600; text-anchor: middle; paint-order: stroke; stroke: #fff; stroke-width: 3px; stroke-linejoin: round; pointer-events: none; }
.station-plan-time-handle { fill: white; stroke-width: 2; cursor: default; touch-action: none; }
.station-plan-time-handle.is-disabled { cursor: default; opacity: 0.5; }
.station-plan-time-handle:focus-visible { outline: 2px solid var(--el-color-primary); }
:global(.el-popper.station-plan-handle-tooltip) { max-width: min(320px, calc(100vw - 24px)); padding: 10px 12px; color: #fff; background: rgba(30, 41, 59, 0.82); border: 1px solid rgba(148, 163, 184, 0.4); border-radius: 6px; box-shadow: 0 4px 12px rgba(15, 23, 42, 0.18); pointer-events: none; }
.station-plan-handle-info { display: grid; grid-template-columns: auto minmax(0, 1fr); gap: 4px 12px; line-height: 1.6; font-size: 12px; }
.station-plan-handle-info span { color: rgba(255, 255, 255, 0.75); }
.station-plan-handle-info strong { font-weight: 600; font-variant-numeric: tabular-nums; overflow-wrap: anywhere; }
.station-plan-segment-drag { stroke: transparent; stroke-width: 12; pointer-events: stroke; }
.station-plan-segment-drag.is-editable { cursor: ns-resize; touch-action: none; }
.station-plan-drag-feedback { position: absolute; right: 16px; top: 58px; z-index: 2; padding: 5px 10px; font-size: 12px; background: #fff; border: 1px solid var(--el-color-primary); border-radius: 4px; pointer-events: none; }
.station-plan-legend { display: flex; flex-wrap: wrap; gap: 6px; max-height: 64px; overflow: auto; padding: 6px 12px; border-top: 1px solid var(--el-border-color-lighter); }
.station-plan-legend button { display: inline-flex; align-items: center; gap: 6px; padding: 3px 8px; color: var(--el-text-color-regular); background: transparent; border: 1px solid var(--el-border-color-lighter); border-radius: 4px; cursor: pointer; font-size: 12px; }
.station-plan-legend button[aria-pressed='true'] { background: var(--el-color-primary-light-9); border-color: var(--el-color-primary); }
.station-plan-legend button[aria-pressed='true'] span { height: 4px; }
.station-plan-legend button span { width: 14px; height: 3px; }
.station-plan-empty { flex: 1; display: grid; place-items: center; color: var(--el-text-color-secondary); font-size: 13px; }
.station-plan-empty-label { fill: #909399; font-size: 12px; }
@media (max-width: 760px) { .station-plan-grid { grid-template-columns: 132px minmax(0, 1fr); } .station-plan-scale :deep(.el-slider) { width: 100px; } }
</style>
