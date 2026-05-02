<template>
    <div class="chart-wrapper" :class="{
        'chart-fullscreen': fullscreenChart === 'time',
        'chart-hidden': fullscreenChart === 'velocity'
    }">
        <div class="chart-header">
            <span>{{ t('humpChart.timeTitle') }}</span>
            <div class="chart-tags">
                <el-tag v-for="tag in timeTabs" :key="tag.name" closable @close="handleRemoveTag(tag.name)">
                    {{ tag.label }}
                </el-tag>
            </div>
            <el-button size="small" @click="handleToggleFullscreen" class="fullscreen-btn">
                {{ fullscreenChart === 'time' ? t('humpChart.fullscreenExit') : t('humpChart.fullscreenEnter') }}
            </el-button>
        </div>
        <div class="chart-content" id="time-distance-chart" ref="chartContainer">
            <svg :style="{ width: '100%', height: chartHeight + 'px' }" class="curve-chart">
                <defs>
                    <linearGradient id="timeGradient" x1="0%" y1="0%" x2="0%" y2="100%">
                        <stop offset="0%" style="stop-color: #FDF2F8; stop-opacity: 0.8" />
                        <stop offset="100%" style="stop-color: #FEF7FF; stop-opacity: 1" />
                    </linearGradient>
                </defs>
                <g class="background">
                    <rect :width="effectiveChartWidth" :height="chartHeight" fill="url(#timeGradient)" />
                </g>
                <g class="axis">
                    <line class="x-axis" :x1="plotLeft" :x2="plotRight" :y1="chartHeight - marginBottom"
                        :y2="chartHeight - marginBottom" />
                    <line class="y-axis" :x1="marginLeft" :x2="marginLeft" :y1="marginTop"
                        :y2="chartHeight - marginBottom" />
                    <text class="axis-label" :x="plotLeft + plotWidth / 2" :y="chartHeight - 5" text-anchor="middle">
                        {{ t('humpChart.axis.distance') }}
                    </text>
                    <text class="axis-label" :x="15" :y="marginTop + (chartHeight - marginBottom - marginTop) / 2"
                        text-anchor="middle" transform="rotate(-90, 15, 100)">{{ t('humpChart.axis.time') }}</text>
                </g>
                <g class="time-curves">
                    <polyline v-for="curve in timeCurveData" :key="curve.seriesName"
                        :points="getTimePolylinePoints(curve.data)" :stroke="curve.color" stroke-width="2"
                        fill="none" />
                    <g v-for="curve in timeCurveData" :key="curve.seriesName">
                        <circle v-for="(point, pointIndex) in curve.data" :key="`${curve.seriesName}-${pointIndex}`"
                            :cx="getTimeX(point.x)" :cy="getTimeY(point.time)" r="3" :fill="curve.color" />
                    </g>
                </g>
                <g class="grid-lines">
                    <line v-for="i in 5" :key="i" class="grid-line-h" :x1="plotLeft" :x2="plotRight"
                        :y1="marginTop + (chartHeight - marginBottom - marginTop) * i / 5"
                        :y2="marginTop + (chartHeight - marginBottom - marginTop) * i / 5" />
                    <line v-for="i in 6" :key="i" class="grid-line-v" :x1="plotLeft + plotWidth * i / 6"
                        :x2="plotLeft + plotWidth * i / 6" :y1="marginTop" :y2="chartHeight - marginBottom" />
                </g>
                <g class="headway-annotations">
                    <g v-for="annotation in renderedHeadwayAnnotations" :key="annotation.id" class="headway-annotation"
                        :class="{ 'headway-annotation--conflict': annotation.isConflict }">
                        <polyline :points="annotation.pathPoints" fill="none" />
                        <text :x="annotation.labelX" :y="annotation.labelY" text-anchor="middle">{{ annotation.label
                        }}</text>
                        <title>{{ annotation.tooltip }}</title>
                    </g>
                </g>
            </svg>
            <hump-slope-sketch-block v-model:slope-layout="slopeLayout" v-if="fullscreenChart === 'time'"
                :global-scale-x="sharedScaleX" :global-min-x="sharedXExtent.min" :global-left-margin="plotLeft"
                :global-domain-span="sharedXExtent.span" />
            <hump-layout-ctrl v-model:flat-layout="flatLayout" v-if="fullscreenChart === 'time'"
                :global-scale-x="sharedScaleX" :global-min-x="sharedXExtent.min" :global-left-margin="plotLeft"
                :global-domain-span="sharedXExtent.span" />
        </div>
    </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onBeforeUnmount, nextTick, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import HumpSlopeSketchBlock from './HumpSlopeSketchBlock.vue'
import HumpLayoutCtrl from './HumpLayoutCtrl.vue'
import axios from '@/utils/axios'
import { FlatLayout, SlopeLayout, CurveDirections } from './humplayoutctrl';

const slopeLayout = ref<SlopeLayout | null>(null);
const flatLayout = ref<FlatLayout | null>(null);

interface TimePoint {
    x: number
    time: number
}

interface TimeCurveData {
    seriesName: string
    color: string
    data: TimePoint[]
}

interface HeadwayAnnotation {
    id: string
    frontSeriesName: string
    rearSeriesName: string
    frontSequence: number
    rearSequence: number
    equipmentID: string
    startX: number
    endX: number
    frontExitTime: number
    rearEnterTime: number
    headway: number
}

interface TimeTab {
    name: string
    label: string
}

const props = defineProps<{
    timeCurveData: TimeCurveData[]
    timeTabs: TimeTab[]
    headwayAnnotations: HeadwayAnnotation[]
    chartWidth: number
    chartHeight: number
    marginLeft: number
    marginRight: number
    marginTop: number
    marginBottom: number
    fullscreenChart: 'velocity' | 'time' | null
    selectedInstanceId?: string | null
    selectedSlopeLineId?: string | null
    selectedHumpSchemeId?: string | null
}>()

const emit = defineEmits<{
    removeTab: [tagName: string]
    toggleFullscreen: []
}>()

const chartContainer = ref<HTMLElement>()
const localChartWidth = ref(0)
let chartResizeObserver: ResizeObserver | null = null

function toFiniteNumber(value: unknown, fallback = 0): number {
    const num = Number(value)
    return Number.isFinite(num) ? num : fallback
}

function getChartContentWidth(container: HTMLElement | undefined): number {
    if (!container) return 0
    const styles = window.getComputedStyle(container)
    const paddingLeft = toFiniteNumber(parseFloat(styles.paddingLeft), 0)
    const paddingRight = toFiniteNumber(parseFloat(styles.paddingRight), 0)
    return Math.max(0, container.clientWidth - paddingLeft - paddingRight)
}

function updateLocalChartWidth() {
    localChartWidth.value = getChartContentWidth(chartContainer.value)
}

const effectiveChartWidth = computed(() => {
    const minWidth = props.marginLeft + props.marginRight + 1
    if (localChartWidth.value > 0) {
        return Math.max(minWidth, localChartWidth.value)
    }
    return Math.max(minWidth, toFiniteNumber(props.chartWidth, minWidth))
})

const plotLeft = computed(() => props.marginLeft)
const plotRight = computed(() => Math.max(plotLeft.value + 1, effectiveChartWidth.value - props.marginRight))
const plotWidth = computed(() => Math.max(1, plotRight.value - plotLeft.value))

const timeXValues = computed(() => {
    return props.timeCurveData
        .flatMap(curve => curve.data.map(p => Number(p.x)))
        .filter(distance => Number.isFinite(distance))
})

const timeXExtent = computed(() => {
    const allDistances = timeXValues.value

    if (allDistances.length === 0) {
        return { min: 0, span: 1 }
    }

    const min = Math.min(...allDistances)
    const max = Math.max(...allDistances)
    return {
        min,
        span: Math.max(1e-9, max - min)
    }
})

const slopeXExtent = computed(() => {
    const xs = (slopeLayout.value?.positionList || [])
        .map(pos => Number(pos.x))
        .filter(x => Number.isFinite(x))

    if (xs.length === 0) {
        return { min: 0, span: 0 }
    }

    const min = Math.min(...xs)
    const max = Math.max(...xs)
    return {
        min,
        span: Math.max(0, max - min)
    }
})

const flatXExtent = computed(() => {
    const xs = (flatLayout.value?.positionList || [])
        .map(pos => Number(pos.x))
        .filter(x => Number.isFinite(x))

    if (xs.length === 0) {
        return { min: 0, span: 0 }
    }

    const min = Math.min(...xs)
    const max = Math.max(...xs)
    return {
        min,
        span: Math.max(0, max - min)
    }
})

const sharedXExtent = computed(() => {
    const mins: number[] = []
    const maxes: number[] = []

    if (timeXValues.value.length > 0) {
        mins.push(timeXExtent.value.min)
        maxes.push(timeXExtent.value.min + timeXExtent.value.span)
    }

    if (slopeXExtent.value.span > 0) {
        mins.push(slopeXExtent.value.min)
        maxes.push(slopeXExtent.value.min + slopeXExtent.value.span)
    }

    if (flatXExtent.value.span > 0) {
        mins.push(flatXExtent.value.min)
        maxes.push(flatXExtent.value.min + flatXExtent.value.span)
    }

    if (mins.length === 0 || maxes.length === 0) {
        return { min: 0, span: 1 }
    }

    const min = Math.min(...mins)
    const max = Math.max(...maxes)

    return {
        min,
        span: Math.max(1e-9, max - min)
    }
})

const sharedScaleX = computed(() => plotWidth.value / sharedXExtent.value.span)

const timeMaxTime = computed(() => {
    const allTimes = props.timeCurveData
        .flatMap(curve => curve.data.map(p => Number(p.time)))
        .filter(time => Number.isFinite(time))

    const maxTime = allTimes.length > 0 ? Math.max(...allTimes) : 0
    return maxTime > 0 ? maxTime : 1
})

const getTimeX = (x: number): number => {
    return plotLeft.value + ((x - sharedXExtent.value.min) / sharedXExtent.value.span) * plotWidth.value
}

const getTimeY = (time: number): number => {
    const chartAreaHeight = props.chartHeight - props.marginTop - props.marginBottom
    return props.chartHeight - props.marginBottom - (time / timeMaxTime.value) * chartAreaHeight
}

const getTimePolylinePoints = (data: TimePoint[]): string => {
    return data.map(point => `${getTimeX(point.x)},${getTimeY(point.time)}`).join(' ')
}

const formatHeadwayLabel = (headway: number): string => {
    if (!Number.isFinite(headway)) return ''
    const precision = Math.abs(headway) >= 10 ? 1 : 2
    return `${headway.toFixed(precision)}s`
}

const renderedHeadwayAnnotations = computed(() => {
    const minLabelY = props.marginTop + 12
    const maxLabelY = props.chartHeight - props.marginBottom - 6

    return props.headwayAnnotations
        .filter(annotation =>
            Number.isFinite(annotation.startX) &&
            Number.isFinite(annotation.endX) &&
            Number.isFinite(annotation.frontExitTime) &&
            Number.isFinite(annotation.rearEnterTime) &&
            Number.isFinite(annotation.headway)
        )
        .map((annotation, index) => {
            const exitX = getTimeX(annotation.endX)
            const exitY = getTimeY(annotation.frontExitTime)
            const enterX = getTimeX(annotation.startX)
            const enterY = getTimeY(annotation.rearEnterTime)
            const offsetY = index % 2 === 0 ? -8 : 12
            const rawLabelY = exitY + offsetY
            const labelY = Math.min(maxLabelY, Math.max(minLabelY, rawLabelY))

            return {
                ...annotation,
                exitX,
                exitY,
                enterX,
                enterY,
                pathPoints: `${exitX},${exitY} ${enterX},${exitY} ${enterX},${enterY}`,
                labelX: (exitX + enterX) / 2,
                labelY,
                label: formatHeadwayLabel(annotation.headway),
                tooltip: `#${annotation.frontSequence} -> #${annotation.rearSequence}, ${annotation.equipmentID || 'checkpoint'}, ${annotation.headway.toFixed(2)} s`,
                isConflict: annotation.headway < 0
            }
        })
})

const handleRemoveTag = (tagName: string) => {
    emit('removeTab', tagName)
}

const handleToggleFullscreen = () => {
    emit('toggleFullscreen')
}

function loadSlopeLayout() {
    if (!props.selectedInstanceId || !props.selectedHumpSchemeId) {
        slopeLayout.value = null
        return
    }
    axios.get(`/Hump/GetSlopeLayout`, {
        params: {
            instanceID: props.selectedInstanceId,
            humpSchemeID: props.selectedHumpSchemeId
        }
    }).then(response => {
        if (response.data) {
            slopeLayout.value = response.data as SlopeLayout;
        }
    }).catch(error => {
        console.error('Failed to load slope layout data:', error);
    });
}

function loadFlatLayout() {
    if (!props.selectedInstanceId || !props.selectedSlopeLineId) {
        flatLayout.value = null
        return
    }

    axios.get(`/hump/getflatlayout`, {
        params: {
            instanceID: props.selectedInstanceId,
            slopeLineID: props.selectedSlopeLineId
        }
    }).then(response => {
        if (response.data) {
            flatLayout.value = response.data
            if (flatLayout.value?.positionSegmentList) {
                flatLayout.value.positionSegmentList.forEach(seg => {
                    if (seg.curveDegree === 0) {
                        seg.curveDirection = CurveDirections.None
                    }
                })
            }
            console.log('Flat layout data loaded:', flatLayout.value)
        }
    }).catch(error => {
        console.error('Failed to load flat layout data:', error);
    });
}

onMounted(() => {
    nextTick(() => {
        updateLocalChartWidth()
        if (typeof ResizeObserver !== 'undefined' && chartContainer.value) {
            chartResizeObserver = new ResizeObserver(() => {
                updateLocalChartWidth()
            })
            chartResizeObserver.observe(chartContainer.value)
        } else {
            window.addEventListener('resize', updateLocalChartWidth)
        }
    })
});

onBeforeUnmount(() => {
    if (chartResizeObserver) {
        chartResizeObserver.disconnect()
        chartResizeObserver = null
    }
    window.removeEventListener('resize', updateLocalChartWidth)
})

defineExpose({
    chartContainer
})

watch(
    () => [props.selectedInstanceId, props.selectedSlopeLineId],
    () => {
        loadFlatLayout()
    },
    { immediate: true }
)

watch(
    () => [props.selectedInstanceId, props.selectedHumpSchemeId],
    () => {
        loadSlopeLayout()
    },
    { immediate: true }
)

const { t } = useI18n()
</script>

<style scoped>
.chart-wrapper {
    flex: 1;
    display: flex;
    flex-direction: column;
    min-width: 0;
    border: 1px solid #dbe3f1;
    border-radius: 2px;
    background: #ffffff;
    box-shadow: 0 2px 8px rgba(15, 23, 42, 0.08);
    overflow: hidden;
    transition: all 0.3s ease-in-out;
}

.chart-header {
    display: flex;
    padding: 12px 16px;
    background: linear-gradient(135deg, #f8fafc, #eef3ff);
    border-bottom: 1px solid #dbe3f1;
    color: #1f2a37;
    letter-spacing: 0.02em;
    align-items: center;
    gap: 10px;
    min-width: 0;
}

.chart-header span {
    font-size: 16px;
    font-weight: 600;
    letter-spacing: 0.02em;
    flex-shrink: 0;
}

.chart-tags {
    display: flex;
    gap: 8px;
    flex: 1;
    min-width: 0;
    align-items: center;
    flex-wrap: wrap;
}

.chart-content {
    flex: 1;
    min-width: 0;
    padding: 16px;
    overflow-x: hidden;
    overflow-y: auto;
    align-items: center;
    justify-content: center;
    color: #9ca3af;
    font-size: 12px;
}

.chart-tags :deep(.el-tag) {
    margin: 0;
    font-size: small;
}

.curve-chart {
    max-width: 100%;
    border: 1px solid #e5e7eb;
    border-radius: 4px;
}

.chart-fullscreen {
    flex: 1 !important;
    width: 100% !important;
    max-width: 100% !important;
    min-width: 0 !important;
}

.chart-hidden {
    flex: 0 !important;
    width: 0 !important;
    min-width: 0 !important;
    opacity: 0;
    overflow: hidden;
    display: none !important;
    padding: 0;
    margin: 0;
    border: none;
}

@media (max-width: 560px) {
    .chart-header {
        align-items: flex-start;
        flex-wrap: wrap;
        padding: 10px 12px;
    }

    .chart-header span {
        font-size: 14px;
    }

    .chart-tags {
        order: 3;
        flex: 1 1 100%;
    }

    .fullscreen-btn {
        margin-left: auto;
    }

    .chart-content {
        padding: 10px;
    }
}

.curve-chart .x-axis,
.curve-chart .y-axis {
    stroke: #374151;
    stroke-width: 1;
}

.curve-chart .grid-line-h,
.curve-chart .grid-line-v {
    stroke: #e5e7eb;
    stroke-width: 0.5;
    stroke-dasharray: 2, 2;
}

.curve-chart .axis-label {
    font-size: 12px;
    fill: #6b7280;
    font-family: system-ui;
}

.time-curves polyline {
    stroke-linejoin: round;
    stroke-linecap: round;
}

.time-curves circle {
    stroke: white;
    stroke-width: 1;
}

.time-curves circle:hover {
    r: 4;
    stroke-width: 2;
    cursor: pointer;
}

.headway-annotation polyline {
    stroke: #0f766e;
    stroke-width: 1.5;
    stroke-dasharray: 4 3;
    opacity: 0.9;
}

.headway-annotation text {
    fill: #115e59;
    font-size: 11px;
    font-weight: 600;
    paint-order: stroke;
    stroke: rgba(255, 255, 255, 0.95);
    stroke-width: 3px;
    stroke-linejoin: round;
}

.headway-annotation--conflict polyline {
    stroke: #dc2626;
}

.headway-annotation--conflict text {
    fill: #b91c1c;
}
</style>
