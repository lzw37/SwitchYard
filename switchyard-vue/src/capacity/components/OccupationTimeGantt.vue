<template>
    <div class="occupation-gantt" :class="{ 'is-disabled': disabled }">
        <div v-if="rows.length === 0" class="occupation-gantt-empty">
            {{ emptyText }}
        </div>
        <div v-else ref="scrollRef" class="occupation-gantt-scroll">
            <div class="occupation-gantt-grid" :style="gridStyle">
                <div class="occupation-gantt-corner">
                    {{ cellAxisLabel }}
                </div>
                <div class="occupation-gantt-time-head">
                    <span class="occupation-gantt-axis-title">{{ timeAxisLabel }}</span>
                    <span
                        v-for="tick in ticks"
                        :key="`head-${tick}`"
                        class="occupation-gantt-tick-label"
                        :style="{ left: `${timeToX(tick)}px` }"
                    >
                        {{ tick }}
                    </span>
                </div>

                <template v-for="row in rows" :key="row.key">
                    <div class="occupation-gantt-cell" :title="row.cellName">
                        {{ row.cellName }}
                    </div>
                    <div class="occupation-gantt-track">
                        <span
                            v-for="tick in ticks"
                            :key="`${row.key}-${tick}`"
                            :class="['occupation-gantt-grid-line', { 'is-zero': tick === 0 }]"
                            :style="{ left: `${timeToX(tick)}px` }"
                        />
                        <div
                            v-if="row.hasBar"
                            class="occupation-gantt-bar"
                            :style="getBarStyle(row)"
                            @pointerdown="startDrag($event, row, 'move')"
                        >
                            <span
                                class="occupation-gantt-handle is-start"
                                role="separator"
                                :aria-label="startHandleLabel"
                                @pointerdown.stop.prevent="startDrag($event, row, 'start')"
                            />
                            <span class="occupation-gantt-bar-label">
                                {{ row.start }} - {{ row.end }}
                            </span>
                            <span
                                class="occupation-gantt-handle is-end"
                                role="separator"
                                :aria-label="endHandleLabel"
                                @pointerdown.stop.prevent="startDrag($event, row, 'end')"
                            />
                        </div>
                    </div>
                </template>
            </div>
        </div>
    </div>
</template>

<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'

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
    start: number
    end: number
}

const props = withDefaults(defineProps<{
    cells?: GanttCell[]
    times?: GanttTime[]
    disabled?: boolean
    scaleX?: number
    autoFit?: boolean
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
}>()

const labelWidth = 168
const minTimelineWidth = 520
const minBarWidth = 8
const minScaleX = 0.01
const maxScaleX = 4
const scaleChangeThreshold = 0.001
const scrollRef = ref<HTMLElement | null>(null)
const viewportWidth = ref(0)
let resizeObserver: ResizeObserver | null = null
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
    if (span <= 12) return 52
    if (span <= 30) return 36
    if (span <= 80) return 24
    return 16
})

const normalizedScaleX = computed(() => clampScaleX(props.scaleX))
const pixelsPerUnit = computed(() => basePixelsPerUnit.value * normalizedScaleX.value)
const timelineWidth = computed(() => {
    const minWidth = props.autoFit ? 1 : minTimelineWidth
    return Math.max(minWidth, timeSpan.value * pixelsPerUnit.value)
})
const autoFitScaleX = computed(() => {
    const availableWidth = viewportWidth.value - labelWidth - 2
    if (availableWidth <= 0) return normalizedScaleX.value
    return roundScale(clampScaleX(availableWidth / (timeSpan.value * basePixelsPerUnit.value)))
})

const gridStyle = computed(() => ({
    gridTemplateColumns: `${labelWidth}px ${timelineWidth.value}px`,
    minWidth: `${labelWidth + timelineWidth.value}px`,
    '--gantt-unit-width': `${pixelsPerUnit.value}px`,
}))

const ticks = computed(() => {
    const step = getTickStep(domain.value.end - domain.value.start)
    const first = Math.ceil(domain.value.start / step) * step
    const values: number[] = []
    for (let value = first; value <= domain.value.end; value += step) {
        values.push(value)
    }
    return values
})

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
    return Math.max(minScaleX, Math.min(maxScaleX, number))
}

function roundScale(value: number) {
    return Math.round(value * 100) / 100
}

function getTickStep(span: number) {
    if (span <= 12) return 1
    if (span <= 30) return 2
    if (span <= 80) return 5
    if (span <= 160) return 10
    return 20
}

function timeToX(value: number) {
    return (value - domain.value.start) * pixelsPerUnit.value
}

function getBarStyle(row: GanttRow) {
    if (row.start === null || row.end === null) return {}
    const start = Math.min(row.start, row.end)
    const end = Math.max(row.start, row.end)
    return {
        left: `${timeToX(start)}px`,
        width: `${Math.max(minBarWidth, timeToX(end) - timeToX(start))}px`,
    }
}

function startDrag(event: PointerEvent, row: GanttRow, mode: DragMode) {
    if (props.disabled || row.timeIndex < 0 || row.start === null || row.end === null) return
    event.preventDefault()
    dragState = {
        mode,
        timeIndex: row.timeIndex,
        cellID: row.cellID,
        pointerStartX: event.clientX,
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
    const delta = Math.round((event.clientX - dragState.pointerStartX) / pixelsPerUnit.value)
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
    document.body.style.cursor = previousBodyCursor
    document.body.style.userSelect = previousBodyUserSelect
    window.removeEventListener('pointermove', handlePointerMove)
    window.removeEventListener('pointerup', stopDrag)
    window.removeEventListener('pointercancel', stopDrag)
}

function updateViewportWidth() {
    viewportWidth.value = scrollRef.value?.clientWidth || 0
}

function syncAutoFitScale() {
    if (!props.autoFit) return
    updateViewportWidth()
    const nextScale = autoFitScaleX.value
    if (Math.abs(nextScale - normalizedScaleX.value) > scaleChangeThreshold) {
        emit('update:scaleX', nextScale)
    }
    void nextTick(() => {
        if (props.autoFit && scrollRef.value) scrollRef.value.scrollLeft = 0
    })
}

onMounted(() => {
    updateViewportWidth()
    if (typeof ResizeObserver !== 'undefined' && scrollRef.value) {
        resizeObserver = new ResizeObserver(syncAutoFitScale)
        resizeObserver.observe(scrollRef.value)
    } else {
        window.addEventListener('resize', syncAutoFitScale)
    }
    syncAutoFitScale()
})

watch([() => props.autoFit, autoFitScaleX], syncAutoFitScale, { flush: 'post' })

onBeforeUnmount(() => {
    stopDrag()
    resizeObserver?.disconnect()
    resizeObserver = null
    window.removeEventListener('resize', syncAutoFitScale)
})
</script>

<style scoped>
.occupation-gantt {
    display: flex;
    width: 100%;
    max-width: 100%;
    min-width: 0;
    min-height: 0;
    height: 100%;
    border: 1px solid #d8e2ef;
    border-radius: 6px;
    background: #fff;
    overflow: hidden;
}

.occupation-gantt.is-disabled {
    opacity: 0.72;
}

.occupation-gantt-empty {
    display: flex;
    flex: 1 1 auto;
    align-items: center;
    justify-content: center;
    color: #7c8794;
    font-size: 13px;
}

.occupation-gantt-scroll {
    flex: 1 1 auto;
    width: 100%;
    max-width: 100%;
    min-width: 0;
    min-height: 0;
    overflow: auto;
}

.occupation-gantt-grid {
    display: grid;
    grid-auto-rows: 38px;
}

.occupation-gantt-corner,
.occupation-gantt-time-head {
    position: sticky;
    top: 0;
    z-index: 3;
    height: 36px;
    border-bottom: 1px solid #d8e2ef;
    background: #f6f9fc;
}

.occupation-gantt-corner {
    left: 0;
    z-index: 4;
    display: flex;
    align-items: center;
    padding: 0 10px;
    color: #44515f;
    font-size: 12px;
    font-weight: 600;
}

.occupation-gantt-time-head {
    position: sticky;
    overflow: hidden;
}

.occupation-gantt-tick-label {
    position: absolute;
    bottom: 8px;
    transform: translateX(-50%);
    color: #6b7785;
    font-size: 11px;
    line-height: 1;
    white-space: nowrap;
}

.occupation-gantt-axis-title {
    position: sticky;
    left: 10px;
    z-index: 1;
    display: inline-flex;
    align-items: center;
    height: 100%;
    color: #44515f;
    font-size: 12px;
    font-weight: 600;
}

.occupation-gantt-cell {
    position: sticky;
    left: 0;
    z-index: 2;
    display: flex;
    align-items: center;
    min-width: 0;
    padding: 0 10px;
    border-right: 1px solid #d8e2ef;
    border-bottom: 1px solid #edf2f7;
    background: #fff;
    color: #25313d;
    font-size: 12px;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
}

.occupation-gantt-track {
    position: relative;
    min-width: 0;
    border-bottom: 1px solid #edf2f7;
    background:
        linear-gradient(90deg, rgba(216, 226, 239, 0.55) 1px, transparent 1px) 0 0 / var(--gantt-unit-width) 100%,
        #fff;
}

.occupation-gantt-grid-line {
    position: absolute;
    top: 0;
    bottom: 0;
    width: 1px;
    background: rgba(216, 226, 239, 0.9);
}

.occupation-gantt-grid-line.is-zero {
    background: rgba(37, 99, 235, 0.45);
}

.occupation-gantt-bar {
    position: absolute;
    top: 7px;
    bottom: 7px;
    display: flex;
    align-items: center;
    min-width: 8px;
    border: 1px solid #1d4ed8;
    border-radius: 5px;
    background: #2563eb;
    box-shadow: 0 3px 10px rgba(37, 99, 235, 0.18);
    color: #fff;
    cursor: grab;
    overflow: hidden;
}

.occupation-gantt-bar:active {
    cursor: grabbing;
}

.occupation-gantt-handle {
    flex: 0 0 8px;
    align-self: stretch;
    background: rgba(255, 255, 255, 0.22);
    cursor: ew-resize;
}

.occupation-gantt-handle.is-start {
    border-right: 1px solid rgba(255, 255, 255, 0.38);
}

.occupation-gantt-handle.is-end {
    border-left: 1px solid rgba(255, 255, 255, 0.38);
}

.occupation-gantt-bar-label {
    flex: 1 1 auto;
    min-width: 0;
    padding: 0 6px;
    font-size: 11px;
    font-weight: 600;
    line-height: 1;
    text-align: center;
    white-space: nowrap;
}
</style>
