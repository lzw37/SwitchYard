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
                <g class="axis">
                    <line class="x-axis" :x1="plotLeft" :x2="plotRight" :y1="chartHeight - marginBottom"
                        :y2="chartHeight - marginBottom" />
                    <line class="y-axis" :x1="marginLeft" :x2="marginLeft" :y1="marginTop"
                        :y2="chartHeight - marginBottom" />
                    <text class="axis-label" :x="plotLeft + plotWidth / 2" :y="chartHeight - 5" text-anchor="middle">
                        {{ t('humpChart.axis.distance') }}
                    </text>
                    <text class="axis-label" :x="15" :y="marginTop + (chartHeight - marginBottom - marginTop) / 2"
                        text-anchor="middle" transform="rotate(-90, 15, 100)">{{ t('humpChart.axis.velocity') }}</text>
                </g>
                <g class="velocity-curves">
                    <polyline v-for="curve in velocityCurveData" :key="curve.seriesName"
                        :points="getVelocityPolylinePoints(curve.data)" :stroke="curve.color" stroke-width="2"
                        fill="none" />
                    <g v-for="curve in velocityCurveData" :key="curve.seriesName">
                        <circle v-for="(point, pointIndex) in curve.data" :key="`${curve.seriesName}-${pointIndex}`"
                            :cx="getVelocityX(point.x)" :cy="getVelocityY(point.velocity)" r="3" :fill="curve.color" />
                    </g>
                </g>
                <g class="grid-lines">
                    <line v-for="i in 5" :key="i" class="grid-line-h" :x1="plotLeft" :x2="plotRight"
                        :y1="marginTop + (chartHeight - marginBottom - marginTop) * i / 5"
                        :y2="marginTop + (chartHeight - marginBottom - marginTop) * i / 5" />
                    <line v-for="i in 6" :key="i" class="grid-line-v" :x1="plotLeft + plotWidth * i / 6"
                        :x2="plotLeft + plotWidth * i / 6" :y1="marginTop" :y2="chartHeight - marginBottom" />
                </g>
            </svg>
        </div>
    </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onBeforeUnmount, nextTick } from 'vue'
import { useI18n } from 'vue-i18n'

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

const velocityXExtent = computed(() => {
    const allDistances = props.velocityCurveData
        .flatMap(curve => curve.data.map(p => Number(p.x)))
        .filter(distance => Number.isFinite(distance))

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

const velocityMaxVelocity = computed(() => {
    const allVelocities = props.velocityCurveData
        .flatMap(curve => curve.data.map(p => Number(p.velocity)))
        .filter(velocity => Number.isFinite(velocity))

    const maxVelocity = allVelocities.length > 0 ? Math.max(...allVelocities) : 0
    return maxVelocity > 0 ? maxVelocity : 1
})

const getVelocityX = (x: number): number => {
    return plotLeft.value + ((x - velocityXExtent.value.min) / velocityXExtent.value.span) * plotWidth.value
}

const getVelocityY = (velocity: number): number => {
    const chartAreaHeight = props.chartHeight - props.marginTop - props.marginBottom
    return props.chartHeight - props.marginBottom - (velocity / velocityMaxVelocity.value) * chartAreaHeight
}

const getVelocityPolylinePoints = (data: VelocityPoint[]): string => {
    return data.map(point => `${getVelocityX(point.x)},${getVelocityY(point.velocity)}`).join(' ')
}

const handleRemoveTag = (tagName: string) => {
    emit('removeTab', tagName)
}

const handleToggleFullscreen = () => {
    emit('toggleFullscreen')
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
    margin-left: 16px;
    align-items: center;
    flex-wrap: wrap;
}

.chart-content {
    flex: 1;
    padding: 16px;
    overflow: auto;
    display: flex;
    justify-content: center;
    color: #9ca3af;
    font-size: 12px;
}

.chart-tags :deep(.el-tag) {
    margin: 0;
    font-size: small;
}

.curve-chart {
    border: 1px solid #e5e7eb;
    border-radius: 4px;
}

.chart-fullscreen {
    flex: 1 !important;
    width: 100% !important;
}

.chart-hidden {
    flex: 0 !important;
    width: 0 !important;
    min-width: 0 !important;
    opacity: 0;
    overflow: hidden;
    padding: 0;
    margin: 0;
    border: none;
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
</style>
