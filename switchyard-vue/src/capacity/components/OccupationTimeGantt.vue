<template>
    <TrackOccupancyGantt
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
import { computed, onBeforeUnmount, ref } from 'vue'
import TrackOccupancyGantt from './TrackOccupancyGantt.vue'
import { getTrackOccupancyGanttTimeScale, trackOccupancyGanttMetrics, type TrackOccupancyGanttDragStart, type TrackOccupancyGanttRow } from './trackOccupancyGantt'

interface GanttCell {
    id: string
    name: string
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
    timeIndex: number
    start: number | null
    end: number | null
    hasBar: boolean
}

type DragMode = 'start' | 'end' | 'move'

interface DragState {
    mode: DragMode
    timeIndex: number
    cellID: string
    pointerStartX: number
    scrollStartX: number
    pixelsPerUnit: number
    start: number
    end: number
}

const props = withDefaults(defineProps<{
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

const minTimelineWidth = 520
const minBarWidth = trackOccupancyGanttMetrics.minBarWidth
const ganttRef = ref<InstanceType<typeof TrackOccupancyGantt> | null>(null)
const scrollRef = computed(() => ganttRef.value?.viewport || null)
const dragging = ref(false)
const dragDomain = ref<{ start: number; end: number } | null>(null)
let dragState: DragState | null = null
let previousBodyCursor = ''
let previousBodyUserSelect = ''

const rows = computed<GanttRow[]>(() => {
    const usedTimeIndexes = new Set<number>()
    const sourceCells = props.cells.length > 0
        ? props.cells
        : props.times.map((time) => ({ id: time.cellID, name: time.cellID }))

    return sourceCells
        .map((cell, rowIndex) => {
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
                timeIndex,
                start,
                end,
                hasBar: timeIndex >= 0 && start !== null && end !== null,
            }
        })
        .filter((row): row is GanttRow => row !== null)
})

const timeValues = computed(() => rows.value
    .flatMap((row) => [row.start, row.end])
    .filter((value): value is number => value !== null))

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

const displayRows = computed<TrackOccupancyGanttRow[]>(() => rows.value.map((row) => ({
    key: row.key,
    label: row.cellName,
    blocks: row.hasBar && row.start !== null && row.end !== null ? [{
        key: row.key,
        left: timeToX(Math.min(row.start, row.end)),
        width: Math.max(minBarWidth, Math.abs(row.end - row.start) * basePixelsPerUnit.value),
        label: `${row.start} - ${row.end}`,
        title: `${row.cellName} · ${row.start} - ${row.end}`,
    }] : [],
})))
const displayTicks = computed(() => ticks.value.map((tick) => ({
    key: tick,
    left: timeToX(tick),
    label: tick,
    zero: tick === 0,
})))

function handleGanttDragStart(payload: TrackOccupancyGanttDragStart) {
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
    if (props.disabled || row.timeIndex < 0 || row.start === null || row.end === null) return
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
    }
    previousBodyCursor = document.body.style.cursor
    previousBodyUserSelect = document.body.style.userSelect
    document.body.style.cursor = mode === 'move' ? 'grabbing' : 'ew-resize'
    document.body.style.userSelect = 'none'
    window.addEventListener('pointermove', handlePointerMove)
    window.addEventListener('pointerup', stopDrag)
    window.addEventListener('pointercancel', stopDrag)
}

function handlePointerMove(event: PointerEvent) {
    if (!dragState) return
    const delta = Math.round((event.clientX - dragState.pointerStartX + (scrollRef.value?.scrollLeft || 0) - dragState.scrollStartX) / dragState.pixelsPerUnit)
    let nextStart = dragState.start
    let nextEnd = dragState.end

    if (dragState.mode === 'start') {
        nextStart = Math.min(dragState.start + delta, nextEnd)
    } else if (dragState.mode === 'end') {
        nextEnd = Math.max(dragState.end + delta, nextStart)
    } else {
        nextStart = dragState.start + delta
        nextEnd = dragState.end + delta
    }

    emit('change', {
        timeIndex: dragState.timeIndex,
        cellID: dragState.cellID,
        startOccupationShift: nextStart,
        endOccupationShift: nextEnd,
    })
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
