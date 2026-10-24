<template>
    <TrackOccupancyGantt
        class="operation-occupancy-gantt"
        ref="ganttRef"
        :rows="displayRows"
        :ticks="displayTicks"
        :timeline-width="timelineWidth"
        :scale-x="scaleX"
        :auto-fit="autoFit"
        :controls-disabled="dragging"
        :refresh-disabled="refreshDisabled || disabled"
        :refresh-loading="disabled"
        :disabled="disabled"
        :empty-text="emptyText"
        :cell-axis-label="cellAxisLabel"
        :time-axis-label="timeAxisLabel"
        :start-handle-label="startHandleLabel"
        :end-handle-label="endHandleLabel"
        editable
        @drag-start="handleGanttDragStart"
        @update:scale-x="emit('update:scaleX', $event)"
        @update:auto-fit="emit('update:autoFit', $event)"
        @refresh="emit('refresh')"
    />
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import { useI18n } from 'vue-i18n'
import { operationOccupancyWindow, dragOperationOccupancy, isOperationOccupancyOrderValid } from './operationOccupancy'
import TrackOccupancyGantt from './TrackOccupancyGantt.vue'
import { getTrackOccupancyGanttTimeScale, trackOccupancyGanttMetrics, type TrackOccupancyGanttDragStart, type TrackOccupancyGanttRow } from './trackOccupancyGantt'

interface GanttCell {
    id: string
    name: string
    kind?: 'track' | 'cell'
    trackNames?: string[]
}

interface GanttTime {
    cellID: string
    startOccupationShift: number | null
    endOccupationShift: number | null
}

interface GanttRow {
    key: string
    cellID: string
    cellName: string
    kind?: 'track' | 'cell'
    trackNames?: string[]
    timeIndex: number
    start: number | null
    end: number | null
    hasBar: boolean
}

type DragMode = 'start' | 'end'

interface DragState {
    mode: DragMode
    timeIndex: number
    cellID: string
    pointerStartX: number
    scrollStartX: number
    pixelsPerUnit: number
    start: number
    end: number
    duration: number
    cellName: string
    warnedInvalidOrder: boolean
}

const props = withDefaults(defineProps<{
    operationTimeS?: number | null
    cells?: GanttCell[]
    times?: GanttTime[]
    disabled?: boolean
    scaleX?: number
    autoFit?: boolean
    refreshDisabled?: boolean
    emptyText?: string
    cellAxisLabel?: string
    timeAxisLabel?: string
    startHandleLabel?: string
    endHandleLabel?: string
}>(), {
    operationTimeS: null,
    cells: () => [],
    times: () => [],
    disabled: false,
    scaleX: 1,
    autoFit: false,
    refreshDisabled: false,
    emptyText: '',
    cellAxisLabel: '',
    timeAxisLabel: '',
    startHandleLabel: '',
    endHandleLabel: '',
})

const emit = defineEmits<{
    (event: 'change', payload: {
        timeIndex: number
        cellID: string
        startOccupationShift: number
        endOccupationShift: number
    }): void
    (event: 'update:scaleX', value: number): void
    (event: 'update:autoFit', value: boolean): void
    (event: 'refresh'): void
}>()

const { t } = useI18n()
const validDuration = computed(() => props.operationTimeS !== null && Number.isFinite(props.operationTimeS) && props.operationTimeS >= 0 ? props.operationTimeS : null)
const minTimelineWidth = 520
const ganttRef = ref<InstanceType<typeof TrackOccupancyGantt> | null>(null)
const scrollRef = computed(() => ganttRef.value?.viewport || null)
const dragging = ref(false)
const dragDomain = ref<{ start: number; end: number } | null>(null)
let dragState: DragState | null = null
let previousBodyCursor = ''
let previousBodyUserSelect = ''

const rows = computed<GanttRow[]>(() => {
    const usedTimeIndexes = new Set<number>()
    const sourceCells: GanttCell[] = props.cells.length > 0
        ? props.cells
        : props.times.map((time) => ({ id: time.cellID, name: time.cellID }))

    return sourceCells
        .map((cell, rowIndex): GanttRow | null => {
            const cellID = String(cell.id || '').trim()
            if (!cellID) return null
            const timeIndex = findTimeIndex(cellID, rowIndex, usedTimeIndexes)
            if (timeIndex >= 0) usedTimeIndexes.add(timeIndex)
            const time = timeIndex >= 0 ? props.times[timeIndex] : null
            const start = normalizeShift(time?.startOccupationShift)
            const end = normalizeShift(time?.endOccupationShift)
            return {
                key: `${cellID}-${rowIndex}`,
                cellID,
                cellName: String(cell.name || cellID),
                kind: cell.kind,
                trackNames: cell.trackNames,
                timeIndex,
                start,
                end,
                hasBar: timeIndex >= 0 && start !== null && end !== null,
            }
        })
        .filter((row): row is GanttRow => row !== null)
})

const timeValues = computed(() => validDuration.value === null ? [] : [0, validDuration.value, ...rows.value.flatMap(row => {
    const window = operationOccupancyWindow(row.start, row.end, validDuration.value)
    return window ? [window.start, window.release] : []
})])

const domain = computed(() => {
    if (dragDomain.value) return dragDomain.value
    if (timeValues.value.length === 0) return { start: -2, end: 12 }
    const min = Math.min(0, ...timeValues.value)
    const max = Math.max(0, ...timeValues.value)
    const span = Math.max(1, max - min)
    const padding = Math.max(1, Math.ceil(span * 0.08))
    return { start: min - padding, end: max + padding }
})

const timeSpan = computed(() => Math.max(1, domain.value.end - domain.value.start))

const basePixelsPerUnit = computed(() => {
    const span = timeSpan.value
    const targetScale = span <= 12 ? 52 : span <= 30 ? 36 : span <= 80 ? 24 : 16
    return getTrackOccupancyGanttTimeScale(span, minTimelineWidth, targetScale)
})

const normalizedScaleX = computed(() => clampScaleX(props.scaleX))
const timelineWidth = computed(() => Math.max(minTimelineWidth, timeSpan.value * basePixelsPerUnit.value))

const ticks = computed(() => {
    const step = getTickStep(domain.value.end - domain.value.start)
    const first = Math.ceil(domain.value.start / step) * step
    const values: number[] = []
    for (let value = first; value <= domain.value.end; value += step) {
        values.push(value)
    }
    return values
})

const formatSeconds = (value: number) => Number(value.toFixed(3)).toString()
const displayRows = computed<TrackOccupancyGanttRow[]>(() => rows.value.map(row => {
    const window = row.hasBar ? operationOccupancyWindow(row.start, row.end, validDuration.value) : null
    const anchorsOutside = window !== null && (window.release - window.start) * basePixelsPerUnit.value * normalizedScaleX.value < 16
    const activeDrag = dragging.value && dragState?.timeIndex === row.timeIndex && dragState.cellID === row.cellID ? dragState : null
    const guide = window && activeDrag ? {
        anchor: activeDrag.mode === 'start' ? window.start : window.release,
        reference: activeDrag.mode === 'start' ? window.operationStart : window.operationEnd,
        mode: activeDrag.mode,
    } : null
    return { key: row.key, label: row.cellName, kind: row.kind || 'cell', trackNames: row.trackNames,
        blocks: window && window.release > window.start ? [{
            key: row.key + ':occupancy', left: timeToX(window.start),
            width: (window.release - window.start) * basePixelsPerUnit.value, minWidth: 0,
            label: '',
            title: row.cellName + ' · ' + props.startHandleLabel + ': ' + formatSeconds(row.start!) + ' s · '
                + props.endHandleLabel + ': ' + formatSeconds(row.end!) + ' s',
            color: 'var(--occupancy-window-color)', className: 'is-occupancy-window', editable: false,
        }] : [],
        connectors: guide ? [{ key: row.key + ':offset-guide',
            left: timeToX(Math.min(guide.anchor, guide.reference)),
            width: Math.abs(guide.anchor - guide.reference) * basePixelsPerUnit.value,
            top: trackOccupancyGanttMetrics.barInset + trackOccupancyGanttMetrics.barHeight / 2,
            color: guide.mode === 'start' ? 'var(--occupancy-start-color)' : 'var(--occupancy-end-color)', className: 'is-offset-guide is-' + guide.mode,
        }] : [],
        anchors: window ? [
            { key: row.key + ':start', left: timeToX(window.start), mode: 'start', outside: anchorsOutside, label: props.startHandleLabel, offsetLabel: formatSeconds(row.start!),
                title: props.startHandleLabel + ': ' + formatSeconds(row.start!) + ' s' },
            { key: row.key + ':end', left: timeToX(window.release), mode: 'end', outside: anchorsOutside, label: props.endHandleLabel, offsetLabel: formatSeconds(row.end!),
                title: props.endHandleLabel + ': ' + formatSeconds(row.end!) + ' s' },
        ] : [],
    }
}))
const displayTicks = computed(() => {
    const duration = validDuration.value
    const values = duration === null ? ticks.value : [...ticks.value.filter(tick => Math.abs(tick) * basePixelsPerUnit.value * normalizedScaleX.value > 75 && Math.abs(tick - duration) * basePixelsPerUnit.value * normalizedScaleX.value > 90), 0, ...(duration > 0 ? [duration] : [])].sort((a, b) => a - b)
    return values.map(tick => ({ key: tick, left: timeToX(tick), zero: tick === 0, major: tick === duration,
        reference: duration === null ? undefined : tick === 0 ? (duration === 0 ? 'both' as const : 'start' as const) : tick === duration ? 'end' as const : undefined,
        align: tick === duration && duration > 0 ? 'end' as const : undefined,
        label: duration === 0 && tick === 0 ? '0 · ' + t('calculationParameters.manager.operationStart') + ' / ' + t('calculationParameters.manager.operationEnd')
            : duration !== null && tick === 0 ? '0 · ' + t('calculationParameters.manager.operationStart')
            : tick === duration ? formatSeconds(tick) + ' · ' + t('calculationParameters.manager.operationEnd') : formatSeconds(tick) }))
})

function warnInvalidOrder(cellName: string) {
    ElMessage.warning({ message: t('calculationParameters.manager.invalidOccupancyOrder', { cell: cellName }), grouping: true })
}
const invalidRowKeys = computed(() => rows.value.filter(row => row.hasBar && validDuration.value !== null
    && !isOperationOccupancyOrderValid(row.start, row.end, validDuration.value)).map(row => row.key).join(','))
watch([invalidRowKeys, () => props.disabled], ([keys, disabled]) => {
    if (keys && !disabled) {
        const row = rows.value.find(row => row.hasBar && !isOperationOccupancyOrderValid(row.start, row.end, validDuration.value))
        if (row) warnInvalidOrder(row.cellName)
    }
}, { immediate: true })

function handleGanttDragStart(payload: TrackOccupancyGanttDragStart) {
    if (payload.mode === 'move') return
    const row = rows.value.find((item) => item.key === payload.rowKey)
    if (row) startDrag(payload.event, row, payload.mode)
}

function findTimeIndex(cellID: string, rowIndex: number, usedTimeIndexes: Set<number>) {
    const sameIndexTime = props.times[rowIndex]
    if (sameIndexTime && !usedTimeIndexes.has(rowIndex) && sameIndexTime.cellID === cellID) {
        return rowIndex
    }
    return props.times.findIndex((time, index) => !usedTimeIndexes.has(index) && time.cellID === cellID)
}

function normalizeShift(value: unknown) {
    if (value === null || value === undefined || value === '') return null
    const number = Number(value)
    return Number.isFinite(number) ? Math.trunc(number) : null
}

function clampScaleX(value: unknown) {
    const number = Number(value)
    if (!Number.isFinite(number)) return 1
    return number > 0 ? number : 1
}

function getTickStep(span: number) {
    if (span <= 12) return 1
    if (span <= 30) return 2
    if (span <= 80) return 5
    if (span <= 160) return 10
    return 20
}

function timeToX(value: number) {
    return (value - domain.value.start) * basePixelsPerUnit.value
}

function startDrag(event: PointerEvent, row: GanttRow, mode: DragMode) {
    if (props.disabled || validDuration.value === null || row.timeIndex < 0 || row.start === null || row.end === null) return
    event.preventDefault()
    dragDomain.value = { ...domain.value }
    dragging.value = true
    dragState = {
        mode,
        timeIndex: row.timeIndex,
        cellID: row.cellID,
        pointerStartX: event.clientX,
        scrollStartX: scrollRef.value?.scrollLeft || 0,
        pixelsPerUnit: basePixelsPerUnit.value * normalizedScaleX.value,
        start: row.start,
        end: row.end,
        duration: validDuration.value,
        cellName: row.cellName,
        warnedInvalidOrder: false,
    }
    previousBodyCursor = document.body.style.cursor
    previousBodyUserSelect = document.body.style.userSelect
    document.body.style.cursor = 'ew-resize'
    document.body.style.userSelect = 'none'
    window.addEventListener('pointermove', handlePointerMove)
    window.addEventListener('pointerup', stopDrag)
    window.addEventListener('pointercancel', stopDrag)
}

function handlePointerMove(event: PointerEvent) {
    if (!dragState) return
    const delta = Math.round((event.clientX - dragState.pointerStartX + (scrollRef.value?.scrollLeft || 0) - dragState.scrollStartX) / dragState.pixelsPerUnit)
    const next = dragOperationOccupancy(dragState.start, dragState.end, dragState.mode, delta)
    if (!isOperationOccupancyOrderValid(next.startOccupationShift, next.endOccupationShift, dragState.duration)) {
        if (!dragState.warnedInvalidOrder) warnInvalidOrder(dragState.cellName)
        dragState.warnedInvalidOrder = true
        return
    }
    emit('change', { timeIndex: dragState.timeIndex, cellID: dragState.cellID, ...next })
}

function stopDrag() {
    if (!dragState) return
    dragState = null
    dragging.value = false
    dragDomain.value = null
    document.body.style.cursor = previousBodyCursor
    document.body.style.userSelect = previousBodyUserSelect
    window.removeEventListener('pointermove', handlePointerMove)
    window.removeEventListener('pointerup', stopDrag)
    window.removeEventListener('pointercancel', stopDrag)
}

onBeforeUnmount(stopDrag)
</script>

<style scoped>
.operation-occupancy-gantt {
    --occupancy-window-color: #7896b8;
    --occupancy-start-color: #3c806e;
    --occupancy-end-color: #b45f65;
    --occupancy-reference-both: #64748b;
    --occupancy-axis-surface: #f3f6fa;
    --occupancy-grid-color: #e9eef4;
    --sy-border: #dce3eb;
    --sy-surface: #ffffff;
    --el-color-primary: #6887ab;
    --el-color-primary-light-3: #95abc5;
    --el-color-primary-light-5: #b6c6d9;
    --el-color-primary-light-7: #d5e0eb;
    --el-color-primary-light-8: #e6edf5;
    --el-color-primary-light-9: #f2f6fa;
    --el-color-primary-dark-2: #526e90;
    box-shadow: 0 2px 8px rgba(44, 62, 84, 0.035);
}
.operation-occupancy-gantt :deep(.track-occupancy-gantt-toolbar) { background: #fafbfd; }
.operation-occupancy-gantt :deep(.track-occupancy-gantt-scale) { color: #607086; }
.operation-occupancy-gantt :deep(.track-occupancy-gantt-scale output) { color: #7b8899; }
.operation-occupancy-gantt :deep(.el-slider__runway) { background: #e4eaf1; }
.operation-occupancy-gantt :deep(.el-slider__button) { box-shadow: 0 1px 3px rgba(59, 78, 102, 0.1); }
.operation-occupancy-gantt :deep(.track-occupancy-gantt-axis-row),
.operation-occupancy-gantt :deep(.track-occupancy-gantt-axis-label),
.operation-occupancy-gantt :deep(.track-occupancy-gantt-axis-tick span) { background: var(--occupancy-axis-surface); }
.operation-occupancy-gantt :deep(.track-occupancy-gantt-axis-label),
.operation-occupancy-gantt :deep(.track-occupancy-gantt-axis-title) { color: #44566c; }
.operation-occupancy-gantt :deep(.track-occupancy-gantt-axis-tick span) { color: #7b8899; }
.operation-occupancy-gantt :deep(.track-occupancy-gantt-lane-row) { --occupancy-lane-background: #ffffff; }
.operation-occupancy-gantt :deep(.track-occupancy-gantt-lane-row:nth-child(odd)) { --occupancy-lane-background: #fafcfe; }
.operation-occupancy-gantt :deep(.track-occupancy-gantt-lane-label),
.operation-occupancy-gantt :deep(.track-occupancy-gantt-lane-track) { background: var(--occupancy-lane-background); border-bottom-color: #edf1f5; }
.operation-occupancy-gantt :deep(.track-occupancy-gantt-lane-row:nth-child(odd) > .track-occupancy-gantt-lane-label),
.operation-occupancy-gantt :deep(.track-occupancy-gantt-lane-row:nth-child(odd) > .track-occupancy-gantt-lane-track) { background: var(--occupancy-lane-background); }
.operation-occupancy-gantt :deep(.track-occupancy-gantt-lane-row.is-track > .track-occupancy-gantt-lane-track) {
    background: var(--occupancy-lane-background); border-bottom-color: #e2ebe6;
}
.operation-occupancy-gantt :deep(.track-occupancy-gantt-lane-row.is-cell .track-occupancy-gantt-lane-label) { color: #64748b; }
.operation-occupancy-gantt :deep(.track-occupancy-gantt-lane-row.is-track) { --occupancy-lane-background: #f3f8f6; }
.operation-occupancy-gantt :deep(.track-occupancy-gantt-lane-row.is-track .track-occupancy-gantt-lane-label) {
    background: #edf4f1; color: #527c6f; box-shadow: inset 3px 0 #8eafa2;
}
.operation-occupancy-gantt :deep(.track-occupancy-gantt-row-legend > span) { color: #7b8899; }
.operation-occupancy-gantt :deep(.track-occupancy-gantt-row-legend > .is-track) { color: #527c6f; }
.operation-occupancy-gantt :deep(.track-occupancy-gantt-grid-line),
.operation-occupancy-gantt :deep(.track-occupancy-gantt-axis-tick) { background: var(--occupancy-grid-color); }
.operation-occupancy-gantt :deep(.track-occupancy-gantt-grid-line.is-reference-start),
.operation-occupancy-gantt :deep(.track-occupancy-gantt-axis-tick.is-reference-start) { background: var(--occupancy-start-color); }
.operation-occupancy-gantt :deep(.track-occupancy-gantt-grid-line.is-reference-end),
.operation-occupancy-gantt :deep(.track-occupancy-gantt-axis-tick.is-reference-end) { background: var(--occupancy-end-color); }
.operation-occupancy-gantt :deep(.track-occupancy-gantt-grid-line.is-reference-both),
.operation-occupancy-gantt :deep(.track-occupancy-gantt-axis-tick.is-reference-both) { background: var(--occupancy-reference-both); }
.operation-occupancy-gantt :deep(.is-reference-start),
.operation-occupancy-gantt :deep(.is-reference-end),
.operation-occupancy-gantt :deep(.is-reference-both) { width: 3px; z-index: 4; }
.operation-occupancy-gantt :deep(.is-reference-start span) { color: var(--occupancy-start-color); font-weight: 700; }
.operation-occupancy-gantt :deep(.is-reference-end span) { color: var(--occupancy-end-color); font-weight: 700; }
.operation-occupancy-gantt :deep(.is-reference-both span) { color: var(--occupancy-reference-both); font-weight: 700; }
.operation-occupancy-gantt :deep(.is-occupancy-window) {
    border: none; border-radius: 5px;
    box-shadow: inset 0 1px 0 rgba(255, 255, 255, 0.16), inset 0 -1px 0 rgba(50, 71, 99, 0.08);
}
.operation-occupancy-gantt :deep(.is-occupancy-window .track-occupancy-gantt-block-label) { text-align: center; }
</style>
