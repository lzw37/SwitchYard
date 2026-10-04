<template>
    <div class="track-occupancy-gantt" :class="{ 'is-disabled': disabled }">
        <div class="track-occupancy-gantt-toolbar" role="toolbar" :aria-label="t('capacityGantt.toolbar')">
            <ActionButton :icon="FullScreen" :label="t('capacityGantt.fit')" :title="t('capacityGantt.fitHint')" :active="autoFit" :disabled="controlsDisabled || rows.length === 0" @click="enableAutoFit" />
            <label class="track-occupancy-gantt-scale">
                <span>{{ t('capacityGantt.scaleX') }}</span>
                <ElSlider :model-value="scaleX" :min="sliderMinX" :max="sliderMaxX" :step="0.01" size="small" :disabled="controlsDisabled || rows.length === 0" :aria-label="t('capacityGantt.scaleX')" @update:model-value="setScaleX" />
                <output>{{ scaleX.toFixed(2) }}×</output>
            </label>
            <label class="track-occupancy-gantt-scale">
                <span>{{ t('capacityGantt.scaleY') }}</span>
                <ElSlider :model-value="scaleY" :min="sliderMinY" :max="sliderMaxY" :step="0.01" size="small" :disabled="controlsDisabled || rows.length === 0" :aria-label="t('capacityGantt.scaleY')" @update:model-value="setScaleY" />
                <output>{{ scaleY.toFixed(2) }}×</output>
            </label>
            <ActionButton :icon="Refresh" :label="t('capacityGantt.refresh')" :loading="refreshLoading" :disabled="refreshDisabled || controlsDisabled" @click="emit('refresh')" />
            <div v-if="rows.length" class="track-occupancy-gantt-row-legend" :aria-label="t('capacityGantt.rowLegend')">
                <span class="is-track"><i aria-hidden="true" />{{ t('capacityGantt.rowKinds.track') }}</span>
                <span class="is-cell"><i aria-hidden="true" />{{ t('capacityGantt.rowKinds.cell') }}</span>
            </div>
        </div>
        <div ref="viewport" class="track-occupancy-gantt-viewport" @scroll="emit('viewport-scroll', $event)">
            <div v-if="rows.length === 0" class="track-occupancy-gantt-empty">{{ emptyText }}</div>
            <div v-else class="track-occupancy-gantt-content" :style="contentStyle">
                <div class="track-occupancy-gantt-axis-row">
                    <div class="track-occupancy-gantt-axis-label">{{ cellAxisLabel }}</div>
                    <div class="track-occupancy-gantt-axis-track">
                        <span v-if="timeAxisLabel" class="track-occupancy-gantt-axis-title">{{ timeAxisLabel }}</span>
                        <div
                            v-for="tick in ticks"
                            :key="tick.key"
                            class="track-occupancy-gantt-axis-tick"
                            :class="{ 'is-major': tick.major, 'is-zero': tick.zero }"
                            :style="{ left: `${tick.left * scaleX}px` }"
                        >
                            <span :class="{ 'is-first': tick.left <= 0, 'is-last': tick.left >= timelineWidth }">{{ tick.label }}</span>
                        </div>
                        <div v-if="playheadLeft != null" class="track-occupancy-gantt-now-line" :style="playheadStyle" />
                    </div>
                </div>
                <div
                    v-for="row in rows"
                    :key="row.key"
                    class="track-occupancy-gantt-lane-row"
                    :class="[`is-${row.kind || 'cell'}`, { 'is-drop-target': row.key === highlightedRowKey }]"
                    :data-row-key="row.key"
                    :style="{ height: `${(row.height ?? metrics.rowHeight) * scaleY}px` }"
                >
                    <div class="track-occupancy-gantt-lane-label" :title="rowKindTitle(row)">
                        <i class="track-occupancy-gantt-row-marker" aria-hidden="true" /><span>{{ row.label }}</span>
                    </div>
                    <div class="track-occupancy-gantt-lane-track">
                        <span
                            v-for="tick in ticks"
                            :key="tick.key"
                            class="track-occupancy-gantt-grid-line"
                            :class="{ 'is-major': tick.major, 'is-zero': tick.zero }"
                            :style="{ left: `${tick.left * scaleX}px` }"
                        />
                        <div
                            v-for="block in row.blocks"
                            :key="block.key"
                            class="track-occupancy-gantt-block"
                            :class="[block.className, { 'is-editable': editable && block.editable !== false }]"
                            :style="getBlockStyle(block)"
                            :title="block.title"
                            @pointerdown="startDrag($event, row.key, block, 'move')"
                        >
                            <span
                                v-if="editable && block.editable !== false"
                                class="track-occupancy-gantt-handle is-start"
                                role="separator"
                                aria-orientation="vertical"
                                :aria-label="startHandleLabel"
                                @pointerdown.stop.prevent="startDrag($event, row.key, block, 'start')"
                            />
                            <span class="track-occupancy-gantt-block-label">{{ block.label }}</span>
                            <span
                                v-if="editable && block.editable !== false"
                                class="track-occupancy-gantt-handle is-end"
                                role="separator"
                                aria-orientation="vertical"
                                :aria-label="endHandleLabel"
                                @pointerdown.stop.prevent="startDrag($event, row.key, block, 'end')"
                            />
                        </div>
                        <div v-if="playheadLeft != null" class="track-occupancy-gantt-now-line" :style="playheadStyle" />
                    </div>
                </div>
            </div>
        </div>
    </div>
</template>

<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { ElSlider } from 'element-plus'
import { FullScreen, Refresh } from '@element-plus/icons-vue'
import ActionButton from '@/components/ui/ActionButton.vue'
import {
    fitTrackOccupancyGantt,
    hitTestTrackOccupancyGanttRow,
    trackOccupancyGanttMetrics as metrics,
    type TrackOccupancyGanttBlock,
    type TrackOccupancyGanttRow,
    type TrackOccupancyGanttTick,
    type TrackOccupancyGanttDragMode,
    type TrackOccupancyGanttDragStart,
} from './trackOccupancyGantt'

const props = withDefaults(defineProps<{
    rows: TrackOccupancyGanttRow[]
    ticks: TrackOccupancyGanttTick[]
    timelineWidth: number
    playheadLeft?: number | null
    editable?: boolean
    disabled?: boolean
    controlsDisabled?: boolean
    refreshDisabled?: boolean
    refreshLoading?: boolean
    emptyText?: string
    cellAxisLabel?: string
    timeAxisLabel?: string
    startHandleLabel?: string
    endHandleLabel?: string
    highlightedRowKey?: string | null
}>(), {
    playheadLeft: null,
    editable: false,
    disabled: false,
    controlsDisabled: false,
    refreshDisabled: false,
    refreshLoading: false,
    emptyText: '',
    cellAxisLabel: '',
    timeAxisLabel: '',
    startHandleLabel: '',
    endHandleLabel: '',
    highlightedRowKey: null,
})

const emit = defineEmits<{
    (event: 'drag-start', payload: TrackOccupancyGanttDragStart): void
    (event: 'refresh'): void
    (event: 'viewport-scroll', payload: Event): void
}>()
const { t } = useI18n()
const scaleX = defineModel<number>('scaleX', { default: 1 })
const scaleY = defineModel<number>('scaleY', { default: 1 })
const autoFit = defineModel<boolean>('autoFit', { default: false })
const sliderMinX = ref(0.01), sliderMaxX = ref(10)
const sliderMinY = ref(0.01), sliderMaxY = ref(4)
// Extend the manual ranges for fitted charts without moving the slider bounds during a drag.
watch([scaleX, scaleY], ([x, y]) => {
    sliderMinX.value = Math.min(sliderMinX.value, x)
    sliderMaxX.value = Math.max(sliderMaxX.value, Math.ceil(x))
    sliderMinY.value = Math.min(sliderMinY.value, y)
    sliderMaxY.value = Math.max(sliderMaxY.value, Math.ceil(y))
}, { immediate: true })
// Parents use this same viewport for auto-fit and following the playback cursor.
const viewport = ref<HTMLElement | null>(null)
let resizeObserver: ResizeObserver | null = null
defineExpose({ viewport, fitToViewport, hitTestRow })

const contentStyle = computed(() => ({
    width: `${metrics.sidebarWidth + props.timelineWidth * scaleX.value}px`,
    '--occupancy-gantt-sidebar-width': `${metrics.sidebarWidth}px`,
    '--occupancy-gantt-timeline-width': `${props.timelineWidth * scaleX.value}px`,
    '--occupancy-gantt-bar-height': `${metrics.barHeight * scaleY.value}px`,
    '--occupancy-gantt-font-scale': String(Math.max(0.75, Math.min(1.5, scaleY.value))),
}))
const playheadStyle = computed(() => ({ left: `${(props.playheadLeft || 0) * scaleX.value}px` }))
const rowsHeight = computed(() => props.rows.reduce((sum, row) => sum + (row.height ?? metrics.rowHeight), 0))

function hitTestRow(clientX: number, clientY: number, allowedRowKeys?: ReadonlySet<string>) {
    if (!viewport.value) return null
    const bounds = viewport.value.getBoundingClientRect()
    return hitTestTrackOccupancyGanttRow(props.rows, {
        left: bounds.left + viewport.value.clientLeft, top: bounds.top + viewport.value.clientTop,
        width: viewport.value.clientWidth, height: viewport.value.clientHeight,
        scrollLeft: viewport.value.scrollLeft, scrollTop: viewport.value.scrollTop,
        scaleX: scaleX.value, scaleY: scaleY.value, timelineWidth: props.timelineWidth,
    }, clientX, clientY, allowedRowKeys)
}

function fitToViewport() {
    if (!viewport.value || props.rows.length === 0 || props.controlsDisabled) return
    const scales = fitTrackOccupancyGantt(viewport.value.clientWidth, viewport.value.clientHeight, props.timelineWidth, rowsHeight.value)
    scaleX.value = scales.scaleX
    scaleY.value = scales.scaleY
    viewport.value.scrollLeft = 0
    viewport.value.scrollTop = 0
}

function enableAutoFit() {
    autoFit.value = true
    fitToViewport()
}

function setScaleX(value: number | number[]) {
    autoFit.value = false
    scaleX.value = Number(value)
}

function setScaleY(value: number | number[]) {
    autoFit.value = false
    scaleY.value = Number(value)
}

function syncAutoFit() {
    if (autoFit.value) fitToViewport()
}

onMounted(() => {
    if (typeof ResizeObserver !== 'undefined') {
        resizeObserver = new ResizeObserver(syncAutoFit)
        if (viewport.value) resizeObserver.observe(viewport.value)
    } else {
        window.addEventListener('resize', syncAutoFit)
    }
    syncAutoFit()
})
watch([autoFit, () => props.timelineWidth, rowsHeight, () => props.controlsDisabled], () => {
    void nextTick(syncAutoFit)
}, { flush: 'post' })
onBeforeUnmount(() => {
    resizeObserver?.disconnect()
    window.removeEventListener('resize', syncAutoFit)
})

function getBlockStyle(block: TrackOccupancyGanttBlock) {
    return {
        left: `${block.left * scaleX.value}px`,
        width: `${Math.max(metrics.minBarWidth, block.width * scaleX.value)}px`,
        top: `${(block.top ?? metrics.barInset) * scaleY.value}px`,
        '--occupancy-gantt-block-color': block.color || '#2563eb',
        '--occupancy-gantt-block-text': block.color === '#facc15' ? '#334155' : '#ffffff',
    }
}

function rowKindTitle(row: TrackOccupancyGanttRow) {
    const kind = t(row.kind === 'track' ? 'capacityGantt.rowKinds.track' : 'capacityGantt.rowKinds.cell')
    const names = row.trackNames?.filter(name => name !== row.label).join(' / ')
    return `${row.label} · ${kind}${names ? ` · ${names}` : ''}`
}

function startDrag(event: PointerEvent, rowKey: string, block: TrackOccupancyGanttBlock, mode: TrackOccupancyGanttDragMode) {
    if (!props.editable || props.disabled || block.editable === false) return
    emit('drag-start', { event, rowKey, blockKey: block.key, mode })
}
</script>

<style scoped>
.track-occupancy-gantt {
    --occupancy-gantt-border: var(--sy-border, #d8e3ef);
    display: flex;
    flex-direction: column;
    box-sizing: border-box;
    flex: 1 1 auto;
    width: 100%;
    max-width: 100%;
    min-width: 0;
    min-height: 0;
    overflow: hidden;
    border: 1px solid var(--occupancy-gantt-border);
    border-radius: var(--sy-radius, 6px);
    background: #ffffff;
}

.track-occupancy-gantt.is-disabled { opacity: 0.72; }

.track-occupancy-gantt-toolbar {
    display: flex;
    flex: 0 0 auto;
    flex-wrap: wrap;
    align-items: center;
    gap: 8px 14px;
    min-height: 36px;
    padding: 4px 10px;
    border-bottom: 1px solid var(--occupancy-gantt-border);
    background: #ffffff;
}
.track-occupancy-gantt-scale { display: flex; flex: 1 1 170px; align-items: center; gap: 8px; max-width: 280px; color: #40546b; font-size: 12px; }
.track-occupancy-gantt-scale > span { flex: 0 0 auto; }
.track-occupancy-gantt-scale :deep(.el-slider) { flex: 1 1 auto; min-width: 70px; }
.track-occupancy-gantt-scale output { flex: 0 0 42px; text-align: right; font-variant-numeric: tabular-nums; }
.track-occupancy-gantt-toolbar :deep(.el-button + .el-button) { margin-left: 0; }
.track-occupancy-gantt-row-legend { display: flex; flex-wrap: wrap; gap: 12px; margin-left: auto; font-size: 12px; }
.track-occupancy-gantt-row-legend > span { display: inline-flex; align-items: center; gap: 5px; color: #64748b; white-space: nowrap; }
.track-occupancy-gantt-row-legend > .is-track { color: #0f766e; }
.track-occupancy-gantt-row-legend i, .track-occupancy-gantt-row-marker { display: inline-block; width: 12px; height: 0; border-top: 1px dashed currentColor; }
.track-occupancy-gantt-row-legend .is-track i, .is-track .track-occupancy-gantt-row-marker { height: 3px; border-top: 1px solid currentColor; border-bottom: 1px solid currentColor; }
.track-occupancy-gantt-row-marker { flex: 0 0 12px; margin-right: 6px; }

.track-occupancy-gantt-viewport {
    flex: 1 1 auto;
    min-width: 0;
    min-height: 0;
    overflow: auto;
}

.track-occupancy-gantt-content { min-height: 100%; }

.track-occupancy-gantt-axis-row,
.track-occupancy-gantt-lane-row {
    display: grid;
    grid-template-columns: var(--occupancy-gantt-sidebar-width) var(--occupancy-gantt-timeline-width);
}

.track-occupancy-gantt-axis-row {
    position: sticky;
    top: 0;
    z-index: 8;
    height: 44px;
    background: #f8fafc;
}

.track-occupancy-gantt-axis-label,
.track-occupancy-gantt-lane-label {
    position: sticky;
    left: 0;
    z-index: 6;
    display: flex;
    align-items: center;
    box-sizing: border-box;
    min-width: 0;
    padding: 0 10px;
    border-right: 1px solid var(--occupancy-gantt-border);
    color: #40546b;
    font-size: 12px;
}

.track-occupancy-gantt-axis-label {
    border-bottom: 1px solid var(--occupancy-gantt-border);
    background: #f8fafc;
    font-weight: 600;
}

.track-occupancy-gantt-lane-label {
    border-bottom: 1px solid #edf2f7;
    background: #ffffff;
    font-size: calc(12px * var(--occupancy-gantt-font-scale));
}

.track-occupancy-gantt-lane-label span {
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
}

.track-occupancy-gantt-axis-track,
.track-occupancy-gantt-lane-track {
    position: relative;
    box-sizing: border-box;
    overflow: hidden;
}

.track-occupancy-gantt-axis-track { border-bottom: 1px solid var(--occupancy-gantt-border); }
.track-occupancy-gantt-lane-track { border-bottom: 1px solid #edf2f7; }
.track-occupancy-gantt-lane-row:nth-child(odd) > div { background-color: #fbfdff; }
.track-occupancy-gantt-lane-row.is-cell > .track-occupancy-gantt-lane-label { color: #64748b; }
.track-occupancy-gantt-lane-row.is-track > .track-occupancy-gantt-lane-track { background-color: #f0fdfa; border-bottom-color: #b9e3d9; }
.track-occupancy-gantt-lane-row.is-track > .track-occupancy-gantt-lane-label { background-color: #e4f5f0; color: #0f766e; font-weight: 600; box-shadow: inset 3px 0 #0f766e; }
.track-occupancy-gantt-lane-row.is-drop-target > .track-occupancy-gantt-lane-label,
.track-occupancy-gantt-lane-row.is-drop-target > .track-occupancy-gantt-lane-track {
    background-color: var(--el-color-primary-light-9, #ecf5ff);
    box-shadow: inset 0 1px 0 var(--el-color-primary, #409eff), inset 0 -1px 0 var(--el-color-primary, #409eff);
}
.track-occupancy-gantt-lane-row.is-drop-target > .track-occupancy-gantt-lane-label { color: var(--el-color-primary, #409eff); }

.track-occupancy-gantt-axis-title {
    position: absolute;
    top: 5px;
    left: 10px;
    z-index: 1;
    color: #40546b;
    font-size: 12px;
    font-weight: 600;
}

.track-occupancy-gantt-axis-tick,
.track-occupancy-gantt-grid-line {
    position: absolute;
    top: 0;
    bottom: 0;
    width: 1px;
    background: #e4ebf3;
    pointer-events: none;
}

.track-occupancy-gantt-axis-tick.is-major,
.track-occupancy-gantt-grid-line.is-major { background: #cbd8e6; }
.track-occupancy-gantt-axis-tick.is-zero,
.track-occupancy-gantt-grid-line.is-zero { background: rgba(37, 99, 235, 0.45); }

.track-occupancy-gantt-axis-tick span {
    position: absolute;
    bottom: 6px;
    transform: translateX(-50%);
    padding: 0 3px;
    background: #f8fafc;
    color: #65758a;
    font-size: 11px;
    line-height: 1;
    white-space: nowrap;
}
.track-occupancy-gantt-axis-tick span.is-first { transform: none; }
.track-occupancy-gantt-axis-tick span.is-last { transform: translateX(-100%); }

.track-occupancy-gantt-block {
    position: absolute;
    z-index: 3;
    display: flex;
    align-items: center;
    box-sizing: border-box;
    height: var(--occupancy-gantt-bar-height);
    overflow: hidden;
    border: 1px solid color-mix(in srgb, var(--occupancy-gantt-block-color) 72%, #0f172a);
    border-radius: 5px;
    background: var(--occupancy-gantt-block-color);
    color: var(--occupancy-gantt-block-text);
}

.track-occupancy-gantt-block-label {
    flex: 1 1 auto;
    min-width: 0;
    overflow: hidden;
    padding: 0 6px;
    font-size: calc(11px * var(--occupancy-gantt-font-scale));
    font-weight: 600;
    line-height: 1;
    text-overflow: ellipsis;
    white-space: nowrap;
}

.track-occupancy-gantt-block.is-editable { cursor: grab; touch-action: none; }
.track-occupancy-gantt-block.is-editable:active { cursor: grabbing; }
.track-occupancy-gantt-block.is-editable .track-occupancy-gantt-block-label { text-align: center; }
.is-disabled .track-occupancy-gantt-block.is-editable,
.is-disabled .track-occupancy-gantt-handle { cursor: default; }

.track-occupancy-gantt-handle {
    flex: 0 1 8px;
    min-width: 3px;
    align-self: stretch;
    background: rgba(255, 255, 255, 0.22);
    cursor: ew-resize;
}
.track-occupancy-gantt-handle.is-start { border-right: 1px solid rgba(255, 255, 255, 0.38); }
.track-occupancy-gantt-handle.is-end { border-left: 1px solid rgba(255, 255, 255, 0.38); }

.track-occupancy-gantt-block.is-finished { border-color: #8792a1; background: #a0a8b3; color: #ffffff; }
.track-occupancy-gantt-block.is-active { box-shadow: 0 0 0 2px rgba(37, 99, 235, 0.22); transform: translateY(-1px); }
.track-occupancy-gantt-block.is-track-preview { border-style: dashed; opacity: 0.75; pointer-events: none; }

.track-occupancy-gantt-now-line {
    position: absolute;
    top: 0;
    bottom: 0;
    z-index: 5;
    width: 2px;
    transform: translateX(-1px);
    background: #ef4444;
    pointer-events: none;
}

.track-occupancy-gantt-empty {
    display: flex;
    align-items: center;
    justify-content: center;
    box-sizing: border-box;
    min-height: 100%;
    padding: 16px;
    color: #65758a;
    font-size: 13px;
    text-align: center;
}
</style>
