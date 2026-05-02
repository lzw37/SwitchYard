<template>
    <div class="chart-wrapper" :class="{
        'chart-fullscreen': fullscreenChart === 'velocity',
        'chart-hidden': fullscreenChart === 'time'
    }">
        <div class="chart-header">
            <span>{{ t('humpChart.velocityTitle') }}</span>
            <div class="chart-tags">
                <el-tag v-for="tag in velocityTabs" :key="tag.name" closable @close="handleRemoveTag(tag.name)">
                    {{ tag.label }}
                </el-tag>
            </div>
            <el-button size="small" @click="handleToggleFullscreen" class="fullscreen-btn">
                {{ fullscreenChart === 'velocity' ? t('humpChart.fullscreenExit') : t('humpChart.fullscreenEnter') }}
            </el-button>
        </div>
        <div class="chart-content" id="velocity-distance-chart" ref="chartContainer">
            <svg :style="{ width: '100%', height: chartHeight + 'px' }" class="curve-chart">
                <defs>
                    <linearGradient id="velocityGradient" x1="0%" y1="0%" x2="0%" y2="100%">
                        <stop offset="0%" style="stop-color: #E8F4FD; stop-opacity: 0.8" />
                        <stop offset="100%" style="stop-color: #F0F9FF; stop-opacity: 1" />
                    </linearGradient>
                </defs>
                <g class="background">
                    <rect :width="effectiveChartWidth" :height="chartHeight" fill="url(#velocityGradient)" />
                </g>
                <g v-if="retarderRects.length > 0" class="retarder-overlays">
                    <g v-for="retarder in retarderRects" :key="retarder.key">
                        <rect class="retarder-range" :x="retarder.x" :y="retarder.y" :width="retarder.width"
                            :height="retarder.height" />
                        <text v-if="retarder.label" class="retarder-label" :x="retarder.labelX" :y="retarder.labelY">
                            {{ retarder.label }}
                        </text>
                    </g>
                </g>
                <g class="grid-lines">
                    <line v-for="(tick, index) in yTicks" :key="`grid-y-${index}`" class="grid-line-h" :x1="plotLeft"
                        :x2="plotRight" :y1="tick.y" :y2="tick.y" />
                    <line v-for="(tick, index) in xTicks" :key="`grid-x-${index}`" class="grid-line-v" :x1="tick.x"
                        :x2="tick.x" :y1="marginTop" :y2="chartHeight - marginBottom" />
                </g>
                <g class="axis">
                    <line class="x-axis" :x1="plotLeft" :x2="plotRight" :y1="chartHeight - marginBottom"
                        :y2="chartHeight - marginBottom" />
                    <line class="y-axis" :x1="marginLeft" :x2="marginLeft" :y1="marginTop"
                        :y2="chartHeight - marginBottom" />
                    <g v-for="(tick, index) in xTicks" :key="`x-tick-${index}`">
                        <line class="axis-tick" :x1="tick.x" :x2="tick.x" :y1="chartHeight - marginBottom"
                            :y2="chartHeight - marginBottom + 5" />
                        <text class="axis-tick-label" :x="tick.x" :y="chartHeight - marginBottom + 20"
                            text-anchor="middle">
                            {{ tick.label }}
                        </text>
                    </g>
                    <g v-for="(tick, index) in yTicks" :key="`y-tick-${index}`">
                        <line class="axis-tick" :x1="plotLeft - 5" :x2="plotLeft" :y1="tick.y" :y2="tick.y" />
                        <text class="axis-tick-label" :x="plotLeft - 10" :y="tick.y + 4" text-anchor="end">
                            {{ tick.label }}
                        </text>
                    </g>
                    <text class="axis-label" :x="plotLeft + plotWidth / 2" :y="chartHeight - 5" text-anchor="middle">
                        {{ t('humpChart.axis.distance') }}
                    </text>
                    <text class="axis-label" :x="verticalAxisLabelX" :y="verticalAxisLabelY" text-anchor="middle"
                        dominant-baseline="middle"
                        :transform="`rotate(-90, ${verticalAxisLabelX}, ${verticalAxisLabelY})`">
                        {{ t('humpChart.axis.velocity') }}
                    </text>
                </g>
                <g class="velocity-curves">
                    <polyline v-for="curve in velocityCurveData" :key="curve.seriesName"
                        :points="getVelocityPolylinePoints(curve.data)" :stroke="curve.color" stroke-width="2"
                        fill="none" />
                    <g v-for="curve in velocityCurveData" :key="curve.seriesName">
                        <circle v-for="(point, pointIndex) in curve.data" :key="`${curve.seriesName}-${pointIndex}`"
                            :cx="getVelocityX(point.x)" :cy="getVelocityY(point.velocity)" r="3" :fill="curve.color"
                            @mouseenter="showPointTooltip(curve, point, pointIndex)" @mouseleave="clearPointTooltip">
                            <title>{{ formatNodeTooltip(point) }}</title>
                        </circle>
                    </g>
                </g>
                <g v-if="hoveredVelocityPoint" class="velocity-tooltip">
                    <circle class="velocity-tooltip-point" :cx="hoveredVelocityPoint.pointX"
                        :cy="hoveredVelocityPoint.pointY" r="5" :fill="hoveredVelocityPoint.color" />
                    <circle class="velocity-tooltip-halo" :cx="hoveredVelocityPoint.pointX"
                        :cy="hoveredVelocityPoint.pointY" r="9" />
                    <line class="velocity-tooltip-connector" :x1="hoveredVelocityPoint.pointX"
                        :y1="hoveredVelocityPoint.pointY" :x2="hoveredVelocityPoint.connectorX"
                        :y2="hoveredVelocityPoint.connectorY" />
                    <rect class="velocity-tooltip-shadow" :x="hoveredVelocityPoint.tooltipX + 2"
                        :y="hoveredVelocityPoint.tooltipY + 3" :width="hoveredVelocityPoint.labelWidth"
                        :height="hoveredVelocityPoint.labelHeight" rx="6" ry="6" />
                    <rect class="velocity-tooltip-box" :x="hoveredVelocityPoint.tooltipX"
                        :y="hoveredVelocityPoint.tooltipY" :width="hoveredVelocityPoint.labelWidth"
                        :height="hoveredVelocityPoint.labelHeight" rx="6" ry="6" />
                    <text class="velocity-tooltip-text" :x="hoveredVelocityPoint.tooltipX + 10"
                        :y="hoveredVelocityPoint.tooltipY + 17">
                        X {{ hoveredVelocityPoint.distanceText }} m
                    </text>
                    <text class="velocity-tooltip-text velocity-tooltip-text-strong"
                        :x="hoveredVelocityPoint.tooltipX + 10" :y="hoveredVelocityPoint.tooltipY + 33">
                        V {{ hoveredVelocityPoint.velocityText }} m/s
                    </text>
                </g>
            </svg>
            <div v-if="showFullscreenAuxLayouts" class="fullscreen-aux-layouts">
                <div v-if="showFullscreenSlopeSketch" class="fullscreen-slope-sketch">
                    <HumpSlopeSketchBlock :slope-layout="slopeLayout" :global-scale-x="sharedScaleX"
                        :global-min-x="sharedXExtent.min" :global-left-margin="plotLeft"
                        :global-domain-span="sharedXExtent.span" />
                </div>
                <div v-if="showFullscreenFlatLayout" class="fullscreen-flat-layout">
                    <HumpLayoutCtrl :flat-layout="flatLayout" :is-toolbar-display="false" style="height: auto"
                        :global-scale-x="sharedScaleX" :global-min-x="sharedXExtent.min" :global-left-margin="plotLeft"
                        :global-domain-span="sharedXExtent.span" />
                </div>
            </div>
        </div>
    </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onBeforeUnmount, nextTick, watch } from 'vue'
import axios from '@/utils/axios'
import { useI18n } from 'vue-i18n'
import HumpLayoutCtrl from './HumpLayoutCtrl.vue'
import HumpSlopeSketchBlock from './HumpSlopeSketchBlock.vue'
import type { SlopeLayout } from './humplayoutctrl'

interface VelocityPoint {
    x: number
    velocity: number
}

interface VelocityCurveData {
    seriesName: string
    color: string
    data: VelocityPoint[]
}

interface VelocityTab {
    name: string
    label: string
}

interface RetarderRectItem {
    key: string
    x: number
    y: number
    width: number
    height: number
    label: string
    labelX: number
    labelY: number
}

interface HoveredVelocityPoint {
    key: string
    pointX: number
    pointY: number
    tooltipX: number
    tooltipY: number
    connectorX: number
    connectorY: number
    labelWidth: number
    labelHeight: number
    distanceText: string
    velocityText: string
    color: string
}

interface AxisTick {
    x: number
    y: number
    label: string
}

const props = defineProps<{
    velocityCurveData: VelocityCurveData[]
    velocityTabs: VelocityTab[]
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
const flatLayout = ref<any | null>(null)
const slopeLayout = ref<SlopeLayout | null>(null)
const hoveredVelocityPoint = ref<HoveredVelocityPoint | null>(null)
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
const plotHeight = computed(() => Math.max(1, props.chartHeight - props.marginTop - props.marginBottom))
const verticalAxisLabelX = computed(() => Math.max(16, plotLeft.value - 42))
const verticalAxisLabelY = computed(() => props.marginTop + plotHeight.value / 2)

const velocityXValues = computed(() => {
    return props.velocityCurveData
        .flatMap(curve => curve.data.map(p => Number(p.x)))
        .filter(distance => Number.isFinite(distance))
})

const velocityXExtent = computed(() => {
    const allDistances = velocityXValues.value

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
        .map((pos: any) => Number(pos?.x))
        .filter((x: number) => Number.isFinite(x))

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

    if (velocityXValues.value.length > 0) {
        mins.push(velocityXExtent.value.min)
        maxes.push(velocityXExtent.value.min + velocityXExtent.value.span)
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
const showFullscreenAuxLayouts = computed(() => props.fullscreenChart === 'velocity' && (!!slopeLayout.value || !!flatLayout.value))
const showFullscreenSlopeSketch = computed(() => props.fullscreenChart === 'velocity' && !!slopeLayout.value)
const showFullscreenFlatLayout = computed(() => props.fullscreenChart === 'velocity' && !!flatLayout.value)

const velocityMaxVelocity = computed(() => {
    const allVelocities = props.velocityCurveData
        .flatMap(curve => curve.data.map(p => Number(p.velocity)))
        .filter(velocity => Number.isFinite(velocity))

    const maxVelocity = allVelocities.length > 0 ? Math.max(...allVelocities) : 0
    return maxVelocity > 0 ? maxVelocity : 1
})

const getVelocityX = (x: number): number => {
    return plotLeft.value + ((x - sharedXExtent.value.min) / sharedXExtent.value.span) * plotWidth.value
}

const getVelocityY = (velocity: number): number => {
    return props.chartHeight - props.marginBottom - (velocity / velocityMaxVelocity.value) * plotHeight.value
}

const getVelocityPolylinePoints = (data: VelocityPoint[]): string => {
    return data.map(point => `${getVelocityX(point.x)},${getVelocityY(point.velocity)}`).join(' ')
}

const formatAxisTickValue = (value: number, fractionDigits: number) => {
    if (!Number.isFinite(value)) return ''
    const fixedValue = value.toFixed(fractionDigits)
    return fractionDigits > 0 ? fixedValue.replace(/\.?0+$/, '') : fixedValue
}

const xTicks = computed<AxisTick[]>(() => {
    const count = 6
    const ticks: AxisTick[] = []

    for (let i = 0; i <= count; i++) {
        const ratio = i / count
        ticks.push({
            x: plotLeft.value + ratio * plotWidth.value,
            y: 0,
            label: formatAxisTickValue(sharedXExtent.value.min + ratio * sharedXExtent.value.span, 0)
        })
    }

    return ticks
})

const yTicks = computed<AxisTick[]>(() => {
    const count = 5
    const ticks: AxisTick[] = []
    const digits = velocityMaxVelocity.value >= 10 ? 1 : 2

    for (let i = 0; i <= count; i++) {
        const ratio = i / count
        ticks.push({
            x: 0,
            y: props.chartHeight - props.marginBottom - ratio * plotHeight.value,
            label: formatAxisTickValue(ratio * velocityMaxVelocity.value, digits)
        })
    }

    return ticks
})

const retarderRects = computed<RetarderRectItem[]>(() => {
    const retarderList = Array.isArray(flatLayout.value?.retarderList) ? flatLayout.value.retarderList : []
    const positionSegmentList = Array.isArray(flatLayout.value?.positionSegmentList) ? flatLayout.value.positionSegmentList : []
    const positionMap = new Map<string, number>()

    for (const position of flatLayout.value?.positionList || []) {
        const id = String(position?.id ?? '')
        const x = Number(position?.x)
        if (id && Number.isFinite(x)) {
            positionMap.set(id, x)
        }
    }

    return retarderList.map((retarder: any, index: number) => {
        const segmentId = String(retarder?.bindingPositionSegmentID ?? retarder?.bindingPositionSegment?.id ?? '')
        const directSegment = retarder?.bindingPositionSegment
        const segment = positionSegmentList.find((item: any) => String(item?.id ?? '') === segmentId) ?? directSegment
        const startX = positionMap.get(String(segment?.startPositionID ?? ''))
        const endX = positionMap.get(String(segment?.endPositionID ?? ''))

        if (startX === undefined || endX === undefined) {
            return null
        }

        const x1 = getVelocityX(Math.min(startX, endX))
        const x2 = getVelocityX(Math.max(startX, endX))
        const numberText = Array.isArray(retarder?.numberArray) && retarder.numberArray.length > 0
            ? retarder.numberArray.join('+')
            : String(retarder?.numbers ?? retarder?.id ?? '')
        const resolvedKey = String(retarder?.id || segmentId || `retarder-${index}`)

        return {
            key: resolvedKey,
            x: x1,
            y: props.marginTop,
            width: Math.max(0, x2 - x1),
            height: plotHeight.value,
            label: numberText,
            labelX: x1 + Math.max(0, x2 - x1) / 2,
            labelY: Math.max(12, props.marginTop - 4)
        }
    }).filter((item: RetarderRectItem | null): item is RetarderRectItem => item !== null && item.width > 0)
})

const formatMetricValue = (value: number, precision = 3) => {
    if (!Number.isFinite(value)) return '--'
    return value.toFixed(precision).replace(/\.?0+$/, '')
}

const formatNodeTooltip = (point: VelocityPoint) => {
    return `X ${formatMetricValue(point.x)} m, V ${formatMetricValue(point.velocity)} m/s`
}

const showPointTooltip = (curve: VelocityCurveData, point: VelocityPoint, pointIndex: number) => {
    const pointX = getVelocityX(point.x)
    const pointY = getVelocityY(point.velocity)
    const labelWidth = 132
    const labelHeight = 42
    const placeRight = pointX <= plotLeft.value + plotWidth.value * 0.62
    const rawTooltipX = placeRight ? pointX + 14 : pointX - labelWidth - 14
    const tooltipX = Math.min(
        plotRight.value - labelWidth - 6,
        Math.max(plotLeft.value + 6, rawTooltipX)
    )
    const preferredTooltipY = pointY - labelHeight - 12
    const tooltipY = preferredTooltipY >= props.marginTop + 4
        ? preferredTooltipY
        : Math.min(props.chartHeight - props.marginBottom - labelHeight - 4, pointY + 12)
    const connectorX = placeRight ? tooltipX : tooltipX + labelWidth
    const connectorY = tooltipY + labelHeight / 2

    hoveredVelocityPoint.value = {
        key: `${curve.seriesName}-${pointIndex}`,
        pointX,
        pointY,
        tooltipX,
        tooltipY,
        connectorX,
        connectorY,
        labelWidth,
        labelHeight,
        distanceText: formatMetricValue(point.x),
        velocityText: formatMetricValue(point.velocity),
        color: curve.color
    }
}

const clearPointTooltip = () => {
    hoveredVelocityPoint.value = null
}

const handleRemoveTag = (tagName: string) => {
    emit('removeTab', tagName)
}

const handleToggleFullscreen = () => {
    emit('toggleFullscreen')
}

function loadFlatLayout() {
    if (!props.selectedInstanceId || !props.selectedSlopeLineId) {
        flatLayout.value = null
        return
    }

    axios.get('/Hump/GetFlatLayout', {
        params: {
            instanceID: props.selectedInstanceId,
            slopeLineID: props.selectedSlopeLineId
        }
    }).then(response => {
        flatLayout.value = response.data || null
    }).catch(error => {
        flatLayout.value = null
        console.error('Failed to load flat layout data:', error)
    })
}

function loadSlopeLayout() {
    if (!props.selectedInstanceId || !props.selectedHumpSchemeId) {
        slopeLayout.value = null
        return
    }

    axios.get('/Hump/GetSlopeLayout', {
        params: {
            instanceID: props.selectedInstanceId,
            humpSchemeID: props.selectedHumpSchemeId
        }
    }).then(response => {
        slopeLayout.value = response.data || null
    }).catch(error => {
        slopeLayout.value = null
        console.error('Failed to load slope layout data:', error)
    })
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
})

onBeforeUnmount(() => {
    if (chartResizeObserver) {
        chartResizeObserver.disconnect()
        chartResizeObserver = null
    }
    window.removeEventListener('resize', updateLocalChartWidth)
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

watch(
    () => props.velocityCurveData,
    () => {
        clearPointTooltip()
    },
    { deep: true }
)

defineExpose({
    chartContainer
})

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
    display: flex;
    flex-direction: column;
    align-items: stretch;
    gap: 14px;
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
    display: block;
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

.curve-chart .axis-tick {
    stroke: #4b5563;
    stroke-width: 1;
}

.curve-chart .axis-tick-label {
    font-size: 11px;
    fill: #6b7280;
    font-family: system-ui;
    user-select: none;
}

.velocity-curves polyline {
    stroke-linejoin: round;
    stroke-linecap: round;
}

.velocity-curves circle {
    stroke: white;
    stroke-width: 1;
}

.velocity-curves circle:hover {
    r: 4;
    stroke-width: 2;
    cursor: pointer;
}

.retarder-overlays .retarder-range {
    fill: rgba(47, 116, 208, 0.14);
    stroke: #2f74d0;
    stroke-width: 1.5px;
    pointer-events: none;
}

.retarder-overlays .retarder-label {
    fill: #1e3a8a;
    font-size: 11px;
    font-weight: 600;
    text-anchor: middle;
    dominant-baseline: auto;
    user-select: none;
    pointer-events: none;
}

.velocity-tooltip {
    pointer-events: none;
}

.velocity-tooltip-point {
    stroke: #ffffff;
    stroke-width: 1.5px;
}

.velocity-tooltip-halo {
    fill: rgba(37, 99, 235, 0.14);
}

.velocity-tooltip-connector {
    stroke: rgba(59, 130, 246, 0.75);
    stroke-width: 1.5px;
    stroke-linecap: round;
}

.velocity-tooltip-shadow {
    fill: rgba(15, 23, 42, 0.12);
}

.velocity-tooltip-box {
    fill: rgba(255, 255, 255, 0.96);
    stroke: rgba(59, 130, 246, 0.55);
    stroke-width: 1px;
}

.velocity-tooltip-text {
    fill: #334155;
    font-size: 11px;
    font-weight: 600;
    user-select: none;
}

.velocity-tooltip-text-strong {
    fill: #1d4ed8;
}

.fullscreen-aux-layouts {
    display: flex;
    flex-direction: column;
    gap: 8px;
}

.fullscreen-slope-sketch,
.fullscreen-flat-layout {
    padding-top: 8px;
    border-top: 1px solid #e5e7eb;
}

.fullscreen-slope-sketch :deep(.sketch-scroll-container),
.fullscreen-flat-layout :deep(.flatlayout-root) {
    width: 100%;
}
</style>
