<template>
    <div class="headway-check-container">
        <!-- 工具栏 -->
        <div class="headway-toolbar">
            <el-button @click="fetchData" type="primary">{{ t('hump.headway.toolbar.debugFetch') }}</el-button>
            <div class="headway-toolbar__group">
                <label>{{ t('hump.headway.labels.verification') }}</label>
                <el-select v-model="selectedVerification"
                    :placeholder="t('hump.headway.placeholders.selectVerification')" size="small" clearable>
                    <el-option :label="t('hump.headway.options.verify1')" value="verify1"></el-option>
                    <el-option :label="t('hump.headway.options.verify2')" value="verify2"></el-option>
                    <el-option :label="t('hump.headway.options.verify3')" value="verify3"></el-option>
                </el-select>
            </div>
            <div class="headway-toolbar__group">
                <label>{{ t('hump.headway.labels.designScheme') }}</label>
                <el-select v-model="selectedDesignScheme" :placeholder="t('hump.headway.placeholders.selectDesign')"
                    size="small" clearable>
                    <el-option :label="t('hump.headway.options.scheme1')" value="scheme1"></el-option>
                    <el-option :label="t('hump.headway.options.scheme2')" value="scheme2"></el-option>
                    <el-option :label="t('hump.headway.options.scheme3')" value="scheme3"></el-option>
                </el-select>
            </div>
            <div class="headway-toolbar__group">
                <el-button type="primary" size="small">{{ t('hump.headway.toolbar.newVerification') }}</el-button>
                <el-button type="danger" size="small">{{ t('hump.headway.toolbar.delete') }}</el-button>
                <el-button type="success" size="small">{{ t('hump.headway.toolbar.save') }}</el-button>
            </div>
            <div class="headway-toolbar__group">
                <label>{{ t('hump.headway.labels.condition') }}</label>
                <el-select v-model="selectedCondition" :placeholder="t('hump.headway.placeholders.selectCondition')"
                    size="small" clearable>
                    <el-option :label="t('hump.headway.options.condA')" value="conditionA"></el-option>
                    <el-option :label="t('hump.headway.options.condB')" value="conditionB"></el-option>
                    <el-option :label="t('hump.headway.options.condC')" value="conditionC"></el-option>
                </el-select>
            </div>
            <div class="headway-toolbar__group">
                <el-button type="primary" size="small">{{ t('hump.headway.toolbar.genVelocityChart') }}</el-button>
            </div>
            <div class="headway-toolbar__group">
                <el-button type="primary" size="small">{{ t('hump.headway.toolbar.genTimeChart') }}</el-button>
            </div>
        </div>

        <!-- 图表容器 -->
        <div class="charts-container" :class="{
            'velocity-fullscreen': fullscreenChart === 'velocity',
            'time-fullscreen': fullscreenChart === 'time'
        }">
            <!-- 速度-距离曲线 -->
            <HumpVelocityCurve ref="velocityCurveRef" :velocity-curve-data="velocityCurveData"
                :velocity-tabs="velocityTabs" :chart-width="chartWidth" :chart-height="chartHeight"
                :margin-left="marginLeft" :margin-right="marginRight" :margin-top="marginTop"
                :margin-bottom="marginBottom" :fullscreen-chart="fullscreenChart" @remove-tab="handleRemoveVelocityTab"
                @toggle-fullscreen="toggleFullscreen('velocity')" />

            <!-- 时间-距离曲线 -->
            <HumpTimeCurve ref="timeCurveRef" :time-curve-data="timeCurveData" :time-tabs="timeTabs"
                :chart-width="chartWidth" :chart-height="chartHeight" :margin-left="marginLeft"
                :margin-right="marginRight" :margin-top="marginTop" :margin-bottom="marginBottom"
                :fullscreen-chart="fullscreenChart" @remove-tab="handleRemoveTimeTab"
                @toggle-fullscreen="toggleFullscreen('time')" />
        </div>
    </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onBeforeUnmount, nextTick } from 'vue'
import axios from '@/utils/axios'
import config from '../config.json'
import HumpVelocityCurve from './HumpVelocityCurve.vue'
import HumpTimeCurve from './HumpTimeCurve.vue'
import { useI18n } from 'vue-i18n'

const { t } = useI18n()

// 全屏状态管理
const fullscreenChart = ref<'velocity' | 'time' | null>(null)

// 切换全屏状态
const toggleFullscreen = (chartType: 'velocity' | 'time') => {
    if (fullscreenChart.value === chartType) {
        fullscreenChart.value = null
    } else {
        fullscreenChart.value = chartType
    }
    // 更新容器宽度
    setTimeout(() => {
        updateContainerWidth()
    }, 100)
}

const selectedDesignScheme = ref('')
const selectedCondition = ref('')
const selectedVerification = ref('')

// 速度-距离曲线tags数据
const velocityTabs = ref([
    { name: 'series1', label: t('hump.headway.series.series1') },
    { name: 'series2', label: t('hump.headway.series.series2') }
])

// 时间-距离曲线tags数据
const timeTabs = ref([
    { name: 'series1', label: t('hump.headway.series.series1') },
    { name: 'series2', label: t('hump.headway.series.series2') }
])

// 删除速度-距离曲线的tag
const handleRemoveVelocityTab = (tagName: string) => {
    const index = velocityTabs.value.findIndex(tab => tab.name === tagName)
    if (index > -1) {
        velocityTabs.value.splice(index, 1)
    }
}

// 删除时间-距离曲线的tag
const handleRemoveTimeTab = (tagName: string) => {
    const index = timeTabs.value.findIndex(tab => tab.name === tagName)
    if (index > -1) {
        timeTabs.value.splice(index, 1)
    }
}

// 图表容器引用
const velocityCurveRef = ref<InstanceType<typeof HumpVelocityCurve>>()
const timeCurveRef = ref<InstanceType<typeof HumpTimeCurve>>()

// 通过子组件ref访问chartContainer
const velocityChartContainer = computed(() => velocityCurveRef.value?.chartContainer)
const timeChartContainer = computed(() => timeCurveRef.value?.chartContainer)

// 图表尺寸和边距
const chartHeight = ref(300)
const containerWidth = ref(800)
const marginLeft = ref(60)
const marginRight = ref(30)
const marginTop = ref(30)
const marginBottom = ref(50)

// 响应式的图表宽度
const chartWidth = computed(() => {
    return Math.max(400, containerWidth.value - 32) // 减去padding
})

// 曲线数据
interface VelocityPoint {
    x: number
    velocity: number
}

interface TimePoint {
    x: number
    time: number
}

interface VelocityCurveData {
    seriesName: string
    color: string
    data: VelocityPoint[]
}

interface TimeCurveData {
    seriesName: string
    color: string
    data: TimePoint[]
}

const velocityCurveData = ref<VelocityCurveData[]>([])
const timeCurveData = ref<TimeCurveData[]>([])

// 从后端获取数据
const fetchVelocityCurve = async () => {
    try {
        const response = await axios.post(`${config.serverurl}/hump/GetVelocityCurve`, {
            wagonTypeName: "P70H",
            wagonVelocityOnTop: 1.4,
            wagonVelocityOnSlop: 5.2,
            wagonVelocityOnYard: 2.2,
            windVelocity: 5,
            isHeadWind: 1,
            airDensity: 0.063,
            temperature: -10,
            g: 9.8,
            retarderActivation: {},
            retarderOutput: {}
        })
        const colors = ['#3b82f6', '#ef4444', '#22c55e', '#f59e0b', '#8b5cf6']
        velocityCurveData.value = [
            {
                seriesName: t('hump.headway.series.series1'),
                color: '#3b82f6',
                data: response.data
            }
        ]
        // 数据载入后更新容器宽度
        updateContainerWidth()
    } catch (error) {
        console.error(t('hump.headway.messages.fetchVelocityFailed'), error)
        // 使用模拟数据
        velocityCurveData.value = [
            {
                seriesName: t('hump.headway.series.series1'),
                color: '#3b82f6',
                data: [
                    { x: 0, velocity: 0 },
                    { x: 100, velocity: 20 },
                    { x: 200, velocity: 35 },
                    { x: 300, velocity: 45 },
                    { x: 400, velocity: 50 },
                    { x: 500, velocity: 40 }
                ]
            }
        ]
        // 模拟数据载入后更新容器宽度
        updateContainerWidth()
    }
}

const fetchTimeCurve = async () => {
    try {
        const response = await axios.post(`${config.serverurl}/hump/GetTimeCurve`, {
            wagonTypeName: "P70H",
            wagonVelocityOnTop: 1.4,
            wagonVelocityOnSlop: 5.2,
            wagonVelocityOnYard: 2.2,
            windVelocity: 5,
            isHeadWind: 1,
            airDensity: 0.063,
            temperature: -10,
            g: 9.8,
            retarderActivation: {},
            retarderOutput: {}
        })
        // 假设后端返回的数据格式：{ seriesName: string, data: TimePoint[] }
        const colors = ['#e11d48', '#059669', '#dc2626', '#7c3aed', '#ea580c']
        timeCurveData.value = [
            {
                seriesName: t('hump.headway.series.series1'),
                color: '#e11d48',
                data: response.data
            }
        ]
        // 数据载入后更新容器宽度
        updateContainerWidth()
    } catch (error) {
        console.error(t('hump.headway.messages.fetchTimeFailed'), error)
        // 使用模拟数据
        timeCurveData.value = [
            {
                seriesName: t('hump.headway.series.series1'),
                color: '#e11d48',
                data: [
                    { x: 0, time: 0 },
                    { x: 100, time: 10 },
                    { x: 200, time: 18 },
                    { x: 300, time: 25 },
                    { x: 400, time: 30 },
                    { x: 500, time: 38 }
                ]
            }
        ]
        // 模拟数据载入后更新容器宽度
        updateContainerWidth()
    }
}

// 更新容器宽度
const updateContainerWidth = () => {
    nextTick(() => {
        let targetContainer = null
        if (fullscreenChart.value === 'velocity' && velocityChartContainer.value) {
            targetContainer = velocityChartContainer.value
        } else if (fullscreenChart.value === 'time' && timeChartContainer.value) {
            targetContainer = timeChartContainer.value
        } else if (!fullscreenChart.value && velocityChartContainer.value) {
            // 默认使用速度图表容器获取宽度
            targetContainer = velocityChartContainer.value
        }

        if (targetContainer) {
            containerWidth.value = targetContainer.clientWidth
        }
    })
}

// 窗口大小变化监听
const handleResize = () => {
    updateContainerWidth()
}

// 组件挂载时获取数据
onMounted(() => {
    window.addEventListener('resize', handleResize)
})

// 组件卸载时移除监听器
onBeforeUnmount(() => {
    window.removeEventListener('resize', handleResize)
})

function fetchData() {
    fetchVelocityCurve()
    fetchTimeCurve()
}
</script>

<style lang="css" scoped>
.headway-check-container {
    display: flex;
    flex-direction: column;
    width: 100%;
    height: 100%;
    background-color: #ffffff;
}

/* 工具栏样式 */
.headway-toolbar {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: 12px;
    padding: 14px 20px;
    margin: 5px 5px 16px 5px;
    border: 1px solid #dbe3f1;
    border-radius: 2px;
    background: linear-gradient(135deg, #f8fafc, #eef3ff);
    box-shadow: 0 5px 15px rgba(15, 23, 42, 0.08);
    min-width: 1400px;
}

.headway-toolbar__group {
    display: flex;
    align-items: center;
    gap: 10px;
    padding: 4px 8px;
    border-radius: 5px;
    /* border: 1px solid #e3eaf7; */
    /* background: #ffffff; */
    /* box-shadow: inset 0 1px 0 rgba(255, 255, 255, 0.9); */
    transition: box-shadow 0.2s ease, border-color 0.2s ease;
}

.headway-toolbar__group label {
    font-size: 13px;
    font-weight: 600;
    color: #1f2a37;
    min-width: 70px;
    text-align: right;
    letter-spacing: 0.02em;
    white-space: nowrap;
}

.headway-toolbar__group :deep(.el-select) {
    min-width: 150px;
}

.headway-toolbar__group :deep(.el-button) {
    position: relative;
}

.chart-header .fullscreen-btn {
    right: 12px;
    margin: 0;
}

/* 图表容器样式 */
.charts-container {
    display: flex;
    flex: 1;
    gap: 16px;
    padding: 16px 20px;
    overflow: auto;
    min-width: 800px;
    transition: all 0.3s ease-in-out;
}
</style>