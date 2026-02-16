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
                    <rect :width="chartWidth" :height="chartHeight" fill="url(#timeGradient)" />
                </g>
                <g class="axis">
                    <line class="x-axis" :x1="marginLeft" :x2="marginLeft + chartWidth - marginRight"
                        :y1="chartHeight - marginBottom" :y2="chartHeight - marginBottom" />
                    <line class="y-axis" :x1="marginLeft" :x2="marginLeft" :y1="marginTop"
                        :y2="chartHeight - marginBottom" />
                    <text class="axis-label" :x="marginLeft + (chartWidth - marginRight - marginLeft) / 2"
                        :y="chartHeight - 5" text-anchor="middle">{{ t('humpChart.axis.distance') }}</text>
                    <text class="axis-label" :x="15" :y="marginTop + (chartHeight - marginBottom - marginTop) / 2"
                        text-anchor="middle" transform="rotate(-90, 15, 100)">{{ t('humpChart.axis.time') }}</text>
                </g>
                <g class="time-curves">
                    <polyline v-for="curve in timeCurveData" :key="curve.seriesName"
                        :points="getTimePolylinePoints(curve.data)" :stroke="curve.color" stroke-width="2"
                        fill="none" />
                    <g v-for="curve in timeCurveData" :key="curve.seriesName">
                        <circle v-for="point in curve.data" :key="point.x" :cx="getTimeX(point.x)"
                            :cy="getTimeY(point.time)" r="3" :fill="curve.color" />
                    </g>
                </g>
                <g class="grid-lines">
                    <line v-for="i in 5" :key="i" class="grid-line-h" :x1="marginLeft"
                        :x2="marginLeft + chartWidth - marginRight"
                        :y1="marginTop + (chartHeight - marginBottom - marginTop) * i / 5"
                        :y2="marginTop + (chartHeight - marginBottom - marginTop) * i / 5" />
                    <line v-for="i in 6" :key="i" class="grid-line-v"
                        :x1="marginLeft + (chartWidth - marginRight - marginLeft) * i / 6"
                        :x2="marginLeft + (chartWidth - marginRight - marginLeft) * i / 6" :y1="marginTop"
                        :y2="chartHeight - marginBottom" />
                </g>
            </svg>
            <div>{{ t('humpChart.placeholderInterval') }}</div>
            <hump-slope-sketch-block v-model:slope-layout="slopeLayout" v-if="fullscreenChart === 'time'"
                :global-scale-x="(chartWidth - marginLeft - marginRight) / 300" />
            <hump-layout-ctrl v-model:flat-layout="flatLayout" v-if="fullscreenChart === 'time'"
                :global-scale-x="(chartWidth - marginLeft - marginRight) / 300" />
        </div>
    </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
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

interface TimeTab {
    name: string
    label: string
}

const props = defineProps<{
    timeCurveData: TimeCurveData[]
    timeTabs: TimeTab[]
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

// 时间-距离曲线坐标转换
const timeMaxDistance = computed(() => {
    const allDistances = props.timeCurveData.flatMap(curve =>
        curve.data.map(p => p.x))
    return allDistances.length > 0 ? Math.max(...allDistances) : 1000
})

const timeMaxTime = computed(() => {
    const allTimes = props.timeCurveData.flatMap(curve =>
        curve.data.map(p => p.time))
    return allTimes.length > 0 ? Math.max(...allTimes) : 100
})

const getTimeX = (x: number): number => {
    const chartAreaWidth = props.chartWidth - props.marginLeft - props.marginRight
    return props.marginLeft + (x / timeMaxDistance.value) * chartAreaWidth
}

const getTimeY = (time: number): number => {
    const chartAreaHeight = props.chartHeight - props.marginTop - props.marginBottom
    return props.chartHeight - props.marginBottom - (time / timeMaxTime.value) * chartAreaHeight
}

const getTimePolylinePoints = (data: TimePoint[]): string => {
    return data.map(point => `${getTimeX(point.x)},${getTimeY(point.time)}`).join(' ')
}

const handleRemoveTag = (tagName: string) => {
    emit('removeTab', tagName)
}

const handleToggleFullscreen = () => {
    emit('toggleFullscreen')
}

function loadSlopeLayout() {
    axios.get(`/hump/getslopelayout`).then(response => {
        if (response.data) {
            slopeLayout.value = response.data as SlopeLayout;
        }
    }).catch(error => {
        console.error("加载纵断面设计数据失败:", error);
    });
}

function loadFlatLayout() {
    axios.get(`/hump/getflatlayout`).then(response => {
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
        console.error("加载平面展开图数据失败:", error);
    });
}

onMounted(() => {
    loadFlatLayout();
    loadSlopeLayout();
});

// 暴露chartContainer给父组件
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
    /* display: flex;
    flex-direction: column; */
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
</style>
