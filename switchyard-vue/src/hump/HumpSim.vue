<template>
    <div class="hump-sim-page">
        <div class="sim-toolbar">
            <div class="toolbar-group">
                <label class="toolbar-label">{{ t('hump.sim.labels.scheme') }}</label>
                <el-select v-model="selectedHeadwayCheckSchemeID" :placeholder="t('hump.sim.select.placeholder')" clearable
                    filterable :no-data-text="t('hump.sim.select.noData')" :no-match-text="t('hump.sim.select.noMatch')"
                    size="small" :loading="loadingSchemes"
                    class="scheme-select" :disabled="!selectedInstanceId">
                    <el-option v-for="scheme in headwayCheckSchemeOptions" :key="scheme.id" :label="scheme.name"
                        :value="scheme.id" />
                </el-select>
            </div>

            <div class="toolbar-group playback-buttons">
                <el-button-group>
                    <el-button size="small" type="primary" :disabled="!hasTrajectoryData || isPlaying"
                        @click="handleStart" :title="t('hump.sim.buttons.start')">
                        &#9654;
                    </el-button>
                    <el-button size="small" type="primary" :disabled="!isPlaying" @click="handlePause"
                        :title="t('hump.sim.buttons.pause')">
                        &#x23F8;
                    </el-button>
                    <el-button size="small" type="primary" :disabled="!hasTrajectoryData" @click="handleEnd"
                        :title="t('hump.sim.buttons.end')">
                        &#x23F9;
                    </el-button>
                    <el-button size="small" type="primary" :disabled="!hasTrajectoryData" @click="stepBySecond(-1)"
                        :title="t('hump.sim.buttons.prevSecond')">
                        &#x23EA;
                    </el-button>
                    <el-button size="small" type="primary" :disabled="!hasTrajectoryData" @click="stepBySecond(1)"
                        :title="t('hump.sim.buttons.nextSecond')">
                        &#x23E9;
                    </el-button>
                    <el-button size="small" type="primary" :disabled="!hasTrajectoryData" @click="handleReset"
                        :title="t('hump.sim.buttons.reset')">
                        &#x21BA;
                    </el-button>
                </el-button-group>
            </div>

            <div class="toolbar-group slider-group speed-slider-group">
                <span class="toolbar-label">{{ t('hump.sim.labels.playbackSpeed') }}</span>
                <el-slider v-model="playbackSpeedRate" :min="0.25" :max="3" :step="0.05" :disabled="!hasTrajectoryData"
                    class="toolbar-slider speed-slider" />
                <span class="slider-value">{{ playbackSpeedRate.toFixed(2) }}x</span>
            </div>

            <div class="toolbar-group slider-group progress-slider-group">
                <span class="toolbar-label">{{ t('hump.sim.labels.progress') }}</span>
                <el-slider v-model="simulationTimeSec" :min="0" :max="progressSliderMax" :step="0.01"
                    :disabled="!hasTrajectoryData" :format-tooltip="formatProgressTooltip"
                    class="toolbar-slider progress-slider" />
                <span class="slider-value">{{ progressPercentText }}</span>
            </div>

            <div class="clock-box">
                <span class="clock-label">{{ t('hump.sim.labels.clock') }}</span>
                <span class="clock-time">{{ formattedSimulationClock }}</span>
            </div>
        </div>

        <div ref="svgWrapperRef" class="sim-body" v-loading="loadingSimulation">
            <svg v-if="canRenderSvg" :width="svgWidth" :height="SVG_HEIGHT" class="sim-svg">
                <defs>
                    <linearGradient id="simSkyGradient" x1="0%" y1="0%" x2="0%" y2="100%">
                        <stop offset="0%" style="stop-color: #f8fbff; stop-opacity: 1" />
                        <stop offset="100%" style="stop-color: #eef4ff; stop-opacity: 1" />
                    </linearGradient>
                    <linearGradient id="simSlopeGradient" x1="0%" y1="0%" x2="0%" y2="100%">
                        <stop offset="0%" style="stop-color: #c8d8ff; stop-opacity: 0.6" />
                        <stop offset="100%" style="stop-color: #dbe7ff; stop-opacity: 0.15" />
                    </linearGradient>
                </defs>

                <rect x="0" y="0" :width="svgWidth" :height="SVG_HEIGHT" fill="url(#simSkyGradient)" />

                <g class="grid-layer">
                    <line v-for="(tick, index) in yTicks" :key="`grid-y-${index}`" :x1="plotLeft" :x2="plotRight"
                        :y1="tick.y" :y2="tick.y" class="grid-line" />
                    <line v-for="(tick, index) in xTicks" :key="`grid-x-${index}`" :x1="tick.x" :x2="tick.x"
                        :y1="plotTop" :y2="plotBottom" class="grid-line" />
                </g>

                <g class="axis-layer">
                    <line :x1="plotLeft" :x2="plotRight" :y1="plotBottom" :y2="plotBottom" class="axis-line" />
                    <line :x1="plotLeft" :x2="plotLeft" :y1="plotTop" :y2="plotBottom" class="axis-line" />

                    <g v-for="(tick, index) in xTicks" :key="`x-tick-${index}`">
                        <line :x1="tick.x" :x2="tick.x" :y1="plotBottom" :y2="plotBottom + 5" class="axis-tick" />
                        <text :x="tick.x" :y="plotBottom + 20" class="axis-label" text-anchor="middle">
                            {{ tick.label }}
                        </text>
                    </g>

                    <g v-for="(tick, index) in yTicks" :key="`y-tick-${index}`">
                        <line :x1="plotLeft - 5" :x2="plotLeft" :y1="tick.y" :y2="tick.y" class="axis-tick" />
                        <text :x="plotLeft - 10" :y="tick.y + 4" class="axis-label" text-anchor="end">
                            {{ tick.label }}
                        </text>
                    </g>

                    <text :x="plotLeft + plotWidth / 2" :y="SVG_HEIGHT - 8" text-anchor="middle" class="axis-title">
                        {{ t('hump.sim.axis.distance') }}
                    </text>
                    <text :x="16" :y="plotTop + plotHeight / 2" text-anchor="middle" class="axis-title"
                        transform="rotate(-90, 16, 180)">
                        {{ t('hump.sim.axis.height') }}
                    </text>
                </g>

                <g class="slope-layer">
                    <polygon v-if="slopeAreaPoints" :points="slopeAreaPoints" class="slope-area" />
                    <polyline :points="slopePolylinePoints" class="slope-line" />
                </g>

                <g class="wagon-layer">
                    <g v-for="wagon in wagonRenderStates" :key="wagon.id">
                        <circle :cx="wagon.cx" :cy="wagon.cy" r="6" :fill="wagon.color" class="wagon-dot" />
                        <text :x="wagon.cx" :y="wagon.cy - 14" class="wagon-speed-label" text-anchor="middle">
                            {{ wagon.speedLabel }}
                        </text>
                        <text :x="wagon.cx" :y="wagon.cy + 18" class="wagon-info-label" text-anchor="middle">
                            {{ wagon.wagonInfoLabel }}
                        </text>
                    </g>
                </g>
            </svg>

            <div v-if="canRenderSvg && !hasTrajectoryData" class="floating-hint">
                {{ t('hump.sim.hints.missingTrajectory') }}
            </div>

            <div v-else-if="emptyStateText" class="empty-hint">
                {{ emptyStateText }}
            </div>
        </div>
    </div>
</template>

<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import { useI18n } from 'vue-i18n'
import axios from '@/utils/axios'
import { getHumpMissingReferenceMessage } from '@/utils/humpMissingReference'

interface Props {
    selectedInstanceId?: string | null
    activationKey?: number
}

interface HeadwayCheckSchemeOption {
    id: string
    name: string
    humpSchemeID: string
}

interface SlopePoint {
    x: number
    height: number
}

interface TrajectoryPoint {
    time: number
    x: number
}

interface SpeedPoint {
    x: number
    speed: number
}

interface WagonTrajectory {
    id: string
    sequence: number
    wagonType: string
    label: string
    color: string
    points: TrajectoryPoint[]
}

interface WagonSpeedProfile {
    sequence: number
    points: SpeedPoint[]
}

interface TickPoint {
    x: number
    y: number
    label: string
}

interface WagonRenderState {
    id: string
    wagonInfoLabel: string
    speedLabel: string
    color: string
    cx: number
    cy: number
}

const props = withDefaults(defineProps<Props>(), {
    selectedInstanceId: null,
    activationKey: 0
})
const { t } = useI18n()

const COLOR_SET = ['#2563eb', '#16a34a', '#dc2626', '#d97706', '#7c3aed', '#0f766e', '#1d4ed8', '#be123c']

const SVG_HEIGHT = 520
const MARGIN_TOP = 24
const MARGIN_RIGHT = 24
const MARGIN_BOTTOM = 50
const MARGIN_LEFT = 70
const SPEED_PROFILE_SPACE_STEP_SIZE = 10

const selectedHeadwayCheckSchemeID = ref('')
const headwayCheckSchemeOptions = ref<HeadwayCheckSchemeOption[]>([])
const slopePoints = ref<SlopePoint[]>([])
const wagonTrajectories = ref<WagonTrajectory[]>([])
const wagonSpeedProfilesBySequence = ref<Record<string, SpeedPoint[]>>({})
const simulationTimeSec = ref(0)
const playbackSpeedRate = ref(1)
const isPlaying = ref(false)
const loadingSchemes = ref(false)
const loadingSimulation = ref(false)
const loadErrorMessage = ref('')

const svgWrapperRef = ref<HTMLElement | null>(null)
const localSvgWidth = ref(0)

let resizeObserver: ResizeObserver | null = null
let rafId: number | null = null
let animationLastTimestamp: number | null = null
let simulationLoadVersion = 0

const svgWidth = computed(() => {
    return Math.max(360, localSvgWidth.value || 960)
})

const plotLeft = computed(() => MARGIN_LEFT)
const plotRight = computed(() => Math.max(plotLeft.value + 1, svgWidth.value - MARGIN_RIGHT))
const plotTop = computed(() => MARGIN_TOP)
const plotBottom = computed(() => SVG_HEIGHT - MARGIN_BOTTOM)
const plotWidth = computed(() => Math.max(1, plotRight.value - plotLeft.value))
const plotHeight = computed(() => Math.max(1, plotBottom.value - plotTop.value))

function toFiniteNumber(value: unknown): number | null {
    const parsed = Number(value)
    return Number.isFinite(parsed) ? parsed : null
}

function readString(source: any, ...keys: string[]): string {
    for (const key of keys) {
        const value = source?.[key]
        if (typeof value === 'string') {
            return value
        }
        if (value !== undefined && value !== null) {
            return String(value)
        }
    }
    return ''
}

function normalizeHeadwayCheckScheme(item: any): HeadwayCheckSchemeOption | null {
    const id = readString(item, 'id', 'ID')
    if (!id) return null

    const name = readString(item, 'name', 'Name') || id
    const humpSchemeID = readString(item, 'humpSchemeID', 'HumpSchemeID')

    return {
        id,
        name,
        humpSchemeID
    }
}

function normalizeSlopePoints(payload: any): SlopePoint[] {
    const positionList = Array.isArray(payload?.positionList)
        ? payload.positionList
        : Array.isArray(payload?.PositionList)
            ? payload.PositionList
            : []

    const points: SlopePoint[] = []

    for (const item of positionList as any[]) {
        const x = toFiniteNumber(item?.x ?? item?.X)
        const height = toFiniteNumber(item?.height ?? item?.Height)
        if (x === null || height === null) continue
        points.push({ x, height })
    }

    points.sort((a: SlopePoint, b: SlopePoint) => a.x - b.x)

    return points
}

function extractRunningTimeItems(payload: any): any[] {
    if (Array.isArray(payload)) return payload
    if (Array.isArray(payload?.runningTimes)) return payload.runningTimes
    if (Array.isArray(payload?.RunningTimes)) return payload.RunningTimes
    return []
}

function extractSpeedProfileItems(payload: any): any[] {
    if (Array.isArray(payload)) return payload
    if (Array.isArray(payload?.speedProfiles)) return payload.speedProfiles
    if (Array.isArray(payload?.SpeedProfiles)) return payload.SpeedProfiles
    return []
}

function normalizeSpeedProfiles(payload: any): WagonSpeedProfile[] {
    const speedProfileItems = extractSpeedProfileItems(payload)

    return speedProfileItems
        .map((item: any, index: number) => {
            const wagon = item?.wagon ?? item?.Wagon ?? {}
            const sequenceValue = toFiniteNumber(wagon?.sequence ?? wagon?.Sequence)
            const sequence = sequenceValue === null ? index + 1 : Math.max(1, Math.floor(sequenceValue))

            const positionListRaw = Array.isArray(item?.positionList)
                ? item.positionList
                : Array.isArray(item?.PositionList)
                    ? item.PositionList
                    : []

            const speedListRaw = Array.isArray(item?.speedList)
                ? item.speedList
                : Array.isArray(item?.SpeedList)
                    ? item.SpeedList
                    : []

            const pointCount = Math.min(positionListRaw.length, speedListRaw.length)
            if (pointCount <= 0) return null

            const points: SpeedPoint[] = []
            for (let i = 0; i < pointCount; i++) {
                const x = toFiniteNumber(positionListRaw[i])
                const speed = toFiniteNumber(speedListRaw[i])
                if (x === null || speed === null) continue
                points.push({ x, speed })
            }

            if (points.length <= 0) return null

            points.sort((a, b) => a.x - b.x)
            return { sequence, points } as WagonSpeedProfile
        })
        .filter((item: WagonSpeedProfile | null): item is WagonSpeedProfile => item !== null)
}

function normalizeWagonTypeMap(payload: any): Record<string, string> {
    const rows = Array.isArray(payload) ? payload : []
    const wagonTypeMap: Record<string, string> = {}

    rows.forEach((row: any) => {
        const id = readString(row, 'id', 'ID')
        if (!id) return

        const wagonType = readString(row, 'wagonType', 'WagonType')
        if (!wagonType) return

        wagonTypeMap[id] = wagonType
    })

    return wagonTypeMap
}

function normalizeWagonTrajectories(payload: any, wagonTypeMap: Record<string, string>): WagonTrajectory[] {
    const runningTimeItems = extractRunningTimeItems(payload)

    return runningTimeItems
        .map((item: any, index: number) => {
            const wagon = item?.wagon ?? item?.Wagon ?? {}
            const sequenceValue = toFiniteNumber(wagon?.sequence ?? wagon?.Sequence)
            const sequence = sequenceValue === null ? index + 1 : Math.max(1, Math.floor(sequenceValue))
            const humpCalculationID = readString(wagon, 'humpCalculationID', 'HumpCalculationID')
            const wagonType = wagonTypeMap[humpCalculationID] || t('hump.sim.labels.unknown')

            const positionListRaw = Array.isArray(item?.positionList)
                ? item.positionList
                : Array.isArray(item?.PositionList)
                    ? item.PositionList
                    : []

            const runningTimeListRaw = Array.isArray(item?.runningTimeList)
                ? item.runningTimeList
                : Array.isArray(item?.RunningTimeList)
                    ? item.RunningTimeList
                    : []

            const pointCount = Math.min(positionListRaw.length, runningTimeListRaw.length)
            if (pointCount <= 0) return null

            const rawPoints: TrajectoryPoint[] = []
            for (let i = 0; i < pointCount; i++) {
                const x = toFiniteNumber(positionListRaw[i])
                const time = toFiniteNumber(runningTimeListRaw[i])
                if (x === null || time === null) continue
                rawPoints.push({ x, time })
            }

            if (rawPoints.length <= 0) return null

            rawPoints.sort((a, b) => a.time - b.time)
            const points: TrajectoryPoint[] = []
            rawPoints.forEach(point => {
                const lastPoint = points[points.length - 1]
                if (!lastPoint || Math.abs(lastPoint.time - point.time) > 1e-9) {
                    points.push(point)
                    return
                }
                points[points.length - 1] = point
            })

            if (points.length <= 0) return null

            return {
                id: `${sequence}-${humpCalculationID || index + 1}`,
                sequence,
                wagonType,
                label: humpCalculationID ? `${sequence}. ${humpCalculationID}` : `${sequence}. Wagon`,
                color: COLOR_SET[index % COLOR_SET.length] || '#2563eb',
                points
            } as WagonTrajectory
        })
        .filter((item: WagonTrajectory | null): item is WagonTrajectory => item !== null)
}

const hasTrajectoryData = computed(() => wagonTrajectories.value.length > 0)

const maxSimulationTime = computed(() => {
    let maxTime = 0
    wagonTrajectories.value.forEach(trajectory => {
        const lastPoint = trajectory.points[trajectory.points.length - 1]
        if (lastPoint && lastPoint.time > maxTime) {
            maxTime = lastPoint.time
        }
    })
    return maxTime
})

const slopeMinX = computed(() => {
    const firstPoint = slopePoints.value[0]
    return firstPoint ? firstPoint.x : 0
})

const xDomain = computed(() => {
    const xValues: number[] = []
    slopePoints.value.forEach(point => xValues.push(point.x))
    wagonTrajectories.value.forEach(trajectory => {
        trajectory.points.forEach(point => xValues.push(point.x))
    })

    if (xValues.length <= 0) {
        return { min: 0, span: 1 }
    }

    const min = Math.min(...xValues)
    const max = Math.max(...xValues)
    return { min, span: Math.max(1e-9, max - min) }
})

const yDomain = computed(() => {
    const yValues = slopePoints.value.map(point => point.height)
    if (yValues.length <= 0) {
        return { min: 0, span: 1 }
    }

    const minY = Math.min(...yValues)
    const maxY = Math.max(...yValues)
    const rawSpan = Math.max(1e-9, maxY - minY)
    const padding = rawSpan * 0.1

    return {
        min: minY - padding,
        span: Math.max(1e-9, rawSpan + padding * 2)
    }
})

function toSvgX(x: number): number {
    return plotLeft.value + ((x - xDomain.value.min) / xDomain.value.span) * plotWidth.value
}

function toSvgY(height: number): number {
    return plotBottom.value - ((height - yDomain.value.min) / yDomain.value.span) * plotHeight.value
}

const slopePolylinePoints = computed(() => {
    return slopePoints.value
        .map(point => `${toSvgX(point.x)},${toSvgY(point.height)}`)
        .join(' ')
})

const slopeAreaPoints = computed(() => {
    if (slopePoints.value.length <= 0) return ''

    const first = slopePoints.value[0]
    const last = slopePoints.value[slopePoints.value.length - 1]
    if (!first || !last) return ''

    const middle = slopePoints.value
        .map(point => `${toSvgX(point.x)},${toSvgY(point.height)}`)
        .join(' ')

    return `${toSvgX(first.x)},${plotBottom.value} ${middle} ${toSvgX(last.x)},${plotBottom.value}`
})

function getSlopeHeightAtX(x: number): number {
    const points = slopePoints.value
    if (points.length <= 0) return 0
    const firstPoint = points[0]
    if (!firstPoint) return 0
    if (points.length === 1) return firstPoint.height

    if (x <= firstPoint.x) return firstPoint.height

    for (let i = 0; i < points.length - 1; i++) {
        const current = points[i]
        const next = points[i + 1]
        if (!current || !next) continue
        if (x > next.x) continue

        const deltaX = next.x - current.x
        if (Math.abs(deltaX) < 1e-9) return next.height

        const ratio = (x - current.x) / deltaX
        return current.height + ratio * (next.height - current.height)
    }

    const lastPoint = points[points.length - 1]
    return lastPoint ? lastPoint.height : firstPoint.height
}

function getWagonXAtTime(trajectory: WagonTrajectory, currentTime: number): number {
    const points = trajectory.points
    if (points.length <= 0) return slopeMinX.value

    const first = points[0]
    const last = points[points.length - 1]
    if (!first || !last) return slopeMinX.value
    const safeTime = Math.max(0, currentTime)

    if (safeTime <= first.time) {
        if (first.time <= 1e-9) return first.x
        const ratio = Math.max(0, Math.min(1, safeTime / first.time))
        return slopeMinX.value + (first.x - slopeMinX.value) * ratio
    }

    for (let i = 0; i < points.length - 1; i++) {
        const current = points[i]
        const next = points[i + 1]
        if (!current || !next) continue
        if (safeTime > next.time) continue

        const deltaT = next.time - current.time
        if (Math.abs(deltaT) < 1e-9) return next.x

        const ratio = (safeTime - current.time) / deltaT
        return current.x + ratio * (next.x - current.x)
    }

    return last.x
}

function getWagonSpeedAtX(sequence: number, x: number): number | null {
    const points = wagonSpeedProfilesBySequence.value[String(sequence)] || []
    if (points.length <= 0) return null

    const first = points[0]
    const last = points[points.length - 1]
    if (!first || !last) return null

    if (x <= first.x) return first.speed

    for (let i = 0; i < points.length - 1; i++) {
        const current = points[i]
        const next = points[i + 1]
        if (!current || !next) continue
        if (x > next.x) continue

        const deltaX = next.x - current.x
        if (Math.abs(deltaX) < 1e-9) return next.speed

        const ratio = (x - current.x) / deltaX
        return current.speed + ratio * (next.speed - current.speed)
    }

    return last.speed
}

const wagonRenderStates = computed(() => {
    return wagonTrajectories.value.map((trajectory): WagonRenderState => {
        const x = getWagonXAtTime(trajectory, simulationTimeSec.value)
        const height = getSlopeHeightAtX(x)
        const speed = getWagonSpeedAtX(trajectory.sequence, x)

        return {
            id: trajectory.id,
            wagonInfoLabel: `${trajectory.sequence} | ${trajectory.wagonType}`,
            speedLabel: speed === null
                ? `${t('hump.sim.labels.unknown')} ${t('hump.sim.units.speed')}`
                : `${speed.toFixed(2)} ${t('hump.sim.units.speed')}`,
            color: trajectory.color,
            cx: toSvgX(x),
            cy: toSvgY(height)
        }
    })
})

const wagonTrajectoryPolylines = computed(() => {
    return wagonTrajectories.value.map(trajectory => {
        const points = [
            `${toSvgX(slopeMinX.value)},${toSvgY(getSlopeHeightAtX(slopeMinX.value))}`,
            ...trajectory.points.map(point => `${toSvgX(point.x)},${toSvgY(getSlopeHeightAtX(point.x))}`)
        ].join(' ')

        return {
            id: trajectory.id,
            points,
            color: trajectory.color
        }
    })
})

const xTicks = computed<TickPoint[]>(() => {
    const count = 6
    const ticks: TickPoint[] = []

    for (let i = 0; i <= count; i++) {
        const ratio = i / count
        const x = plotLeft.value + ratio * plotWidth.value
        const value = xDomain.value.min + ratio * xDomain.value.span
        ticks.push({ x, y: 0, label: value.toFixed(0) })
    }

    return ticks
})

const yTicks = computed<TickPoint[]>(() => {
    const count = 5
    const ticks: TickPoint[] = []

    for (let i = 0; i <= count; i++) {
        const ratio = i / count
        const y = plotBottom.value - ratio * plotHeight.value
        const value = yDomain.value.min + ratio * yDomain.value.span
        ticks.push({ x: 0, y, label: value.toFixed(2) })
    }

    return ticks
})

function formatClock(seconds: number): string {
    const safeSeconds = Math.max(0, Math.floor(seconds))
    const hh = Math.floor(safeSeconds / 3600)
    const mm = Math.floor((safeSeconds % 3600) / 60)
    const ss = safeSeconds % 60

    return `${String(hh).padStart(2, '0')}:${String(mm).padStart(2, '0')}:${String(ss).padStart(2, '0')}`
}

function formatProgressTooltip(value: number): string {
    return formatClock(value)
}

const progressSliderMax = computed(() => {
    return maxSimulationTime.value > 0 ? maxSimulationTime.value : 1
})

const progressPercentText = computed(() => {
    if (maxSimulationTime.value <= 0) return '0.0%'
    const percent = (simulationTimeSec.value / maxSimulationTime.value) * 100
    return `${Math.max(0, Math.min(100, percent)).toFixed(1)}%`
})

const formattedSimulationClock = computed(() => formatClock(simulationTimeSec.value))

const canRenderSvg = computed(() => {
    return !loadingSimulation.value && slopePoints.value.length > 0
})

const emptyStateText = computed(() => {
    if (!props.selectedInstanceId) return t('hump.sim.states.selectInstance')
    if (loadingSchemes.value || loadingSimulation.value) return t('hump.sim.states.loading')
    if (!headwayCheckSchemeOptions.value.length) return t('hump.sim.states.noSchemes')
    if (!selectedHeadwayCheckSchemeID.value) return t('hump.sim.states.selectScheme')
    if (loadErrorMessage.value) return loadErrorMessage.value
    if (slopePoints.value.length <= 0) return t('hump.sim.states.emptySlope')
    if (!hasTrajectoryData.value) return t('hump.sim.states.emptyRunningTime')
    return ''
})

function cancelAnimationFrameLoop() {
    if (rafId !== null) {
        window.cancelAnimationFrame(rafId)
        rafId = null
    }
    animationLastTimestamp = null
}

function runAnimationFrame(timestamp: number) {
    if (!isPlaying.value) return

    if (animationLastTimestamp === null) {
        animationLastTimestamp = timestamp
        rafId = window.requestAnimationFrame(runAnimationFrame)
        return
    }

    const deltaSeconds = ((timestamp - animationLastTimestamp) / 1000) * playbackSpeedRate.value
    animationLastTimestamp = timestamp
    const nextTime = simulationTimeSec.value + deltaSeconds

    if (nextTime >= maxSimulationTime.value) {
        simulationTimeSec.value = maxSimulationTime.value
        isPlaying.value = false
        cancelAnimationFrameLoop()
        return
    }

    simulationTimeSec.value = nextTime
    rafId = window.requestAnimationFrame(runAnimationFrame)
}

function handleStart() {
    if (!hasTrajectoryData.value) return

    if (simulationTimeSec.value >= maxSimulationTime.value) {
        simulationTimeSec.value = 0
    }

    isPlaying.value = true
    cancelAnimationFrameLoop()
    rafId = window.requestAnimationFrame(runAnimationFrame)
}

function handlePause() {
    isPlaying.value = false
    cancelAnimationFrameLoop()
}

function handleEnd() {
    if (!hasTrajectoryData.value) return
    handlePause()
    simulationTimeSec.value = maxSimulationTime.value
}

function stepBySecond(offset: number) {
    if (!hasTrajectoryData.value) return
    handlePause()

    const nextTime = simulationTimeSec.value + offset
    simulationTimeSec.value = Math.max(0, Math.min(maxSimulationTime.value, nextTime))
}

function handleReset() {
    if (!hasTrajectoryData.value) return
    handlePause()
    simulationTimeSec.value = 0
}

function resetSimulationViewState() {
    handlePause()
    simulationTimeSec.value = 0
}

function updateSvgWidth() {
    if (!svgWrapperRef.value) return
    localSvgWidth.value = Math.max(0, svgWrapperRef.value.clientWidth)
}

async function ensureHumpSchemeID(scheme: HeadwayCheckSchemeOption): Promise<string> {
    if (!props.selectedInstanceId) return ''
    if (scheme.humpSchemeID) return scheme.humpSchemeID

    const response = await axios.get('/Hump/GetHeadwayCheckSchemeById', {
        params: {
            instanceID: props.selectedInstanceId,
            id: scheme.id
        }
    })

    const normalized = normalizeHeadwayCheckScheme(response.data)
    scheme.humpSchemeID = normalized?.humpSchemeID || ''

    return scheme.humpSchemeID
}

async function loadHeadwayCheckSchemes(options: { preserveSelection?: boolean, resetState?: boolean } = {}) {
    const { preserveSelection = false, resetState = true } = options
    const previousSchemeID = preserveSelection ? selectedHeadwayCheckSchemeID.value : ''
    loadErrorMessage.value = ''

    if (resetState) {
        headwayCheckSchemeOptions.value = []
        selectedHeadwayCheckSchemeID.value = ''
        slopePoints.value = []
        wagonTrajectories.value = []
        wagonSpeedProfilesBySequence.value = {}
        resetSimulationViewState()
    }

    if (!props.selectedInstanceId) return

    loadingSchemes.value = true
    try {
        const response = await axios.get('/Hump/GetHeadwayCheckSchemes', {
            params: {
                instanceID: props.selectedInstanceId
            }
        })

        const options = (Array.isArray(response.data) ? response.data : [])
            .map(item => normalizeHeadwayCheckScheme(item))
            .filter((item: HeadwayCheckSchemeOption | null): item is HeadwayCheckSchemeOption => item !== null)

        headwayCheckSchemeOptions.value = options
        const matchedScheme = previousSchemeID
            ? options.find(option => option.id === previousSchemeID)
            : undefined
        selectedHeadwayCheckSchemeID.value = matchedScheme?.id || options[0]?.id || ''
    } catch (error) {
        console.error('Failed to load headway check schemes:', error)
        ElMessage.error(t('hump.sim.messages.loadSchemesFailed'))
        loadErrorMessage.value = t('hump.sim.messages.loadSchemesFailed')
    } finally {
        loadingSchemes.value = false
    }
}

async function loadSimulationData() {
    resetSimulationViewState()
    slopePoints.value = []
    wagonTrajectories.value = []
    wagonSpeedProfilesBySequence.value = {}
    loadErrorMessage.value = ''

    if (!props.selectedInstanceId || !selectedHeadwayCheckSchemeID.value) {
        return
    }

    const selectedScheme = headwayCheckSchemeOptions.value.find(item => item.id === selectedHeadwayCheckSchemeID.value)
    if (!selectedScheme) {
        loadErrorMessage.value = t('hump.sim.messages.schemeNotFound')
        return
    }

    const loadVersion = ++simulationLoadVersion
    loadingSimulation.value = true

    try {
        const humpSchemeID = await ensureHumpSchemeID(selectedScheme)
        if (!humpSchemeID) {
            loadErrorMessage.value = t('hump.sim.messages.missingHumpScheme')
            return
        }

        const [slopeResult, runningTimeResult, speedProfileResult, humpCalculationsResult] = await Promise.allSettled([
            axios.get('/Hump/GetSlopeLayout', {
                params: {
                    instanceID: props.selectedInstanceId,
                    humpSchemeID: humpSchemeID
                }
            }),
            axios.get('/Hump/CalculateRunningTime', {
                params: {
                    instanceID: props.selectedInstanceId,
                    headwayCheckSchemeID: selectedHeadwayCheckSchemeID.value
                }
            }),
            axios.get('/Hump/CalculateSpeedProfile', {
                params: {
                    instanceID: props.selectedInstanceId,
                    headwayCheckSchemeID: selectedHeadwayCheckSchemeID.value,
                    spaceStepSize: SPEED_PROFILE_SPACE_STEP_SIZE
                }
            }),
            axios.get('/Hump/GetHumpCalculations', {
                params: {
                    instanceID: props.selectedInstanceId,
                    humpSchemeID: humpSchemeID
                }
            })
        ])

        if (loadVersion !== simulationLoadVersion) return

        if (slopeResult.status !== 'fulfilled') {
            throw slopeResult.reason
        }
        if (runningTimeResult.status !== 'fulfilled') {
            throw runningTimeResult.reason
        }

        slopePoints.value = normalizeSlopePoints(slopeResult.value.data)
        const wagonTypeMap = humpCalculationsResult.status === 'fulfilled'
            ? normalizeWagonTypeMap(humpCalculationsResult.value.data)
            : {}
        wagonTrajectories.value = normalizeWagonTrajectories(runningTimeResult.value.data, wagonTypeMap)

        if (speedProfileResult.status === 'fulfilled') {
            const normalizedSpeedProfiles = normalizeSpeedProfiles(speedProfileResult.value.data)
            const speedProfileMap: Record<string, SpeedPoint[]> = {}
            normalizedSpeedProfiles.forEach(profile => {
                speedProfileMap[String(profile.sequence)] = profile.points
            })
            wagonSpeedProfilesBySequence.value = speedProfileMap
        } else {
            console.error('Failed to load speed profiles:', speedProfileResult.reason)
            wagonSpeedProfilesBySequence.value = {}
            ElMessage.warning(t('hump.sim.messages.loadSpeedFailed'))
        }

        if (humpCalculationsResult.status !== 'fulfilled') {
            console.error('Failed to load hump calculations:', humpCalculationsResult.reason)
            ElMessage.warning(t('hump.sim.messages.loadWagonTypeFailed'))
        }

        if (slopePoints.value.length <= 0) {
            loadErrorMessage.value = t('hump.sim.messages.emptySlopeResponse')
        } else if (wagonTrajectories.value.length <= 0) {
            loadErrorMessage.value = t('hump.sim.messages.emptyRunningTimeResponse')
        }
    } catch (error) {
        console.error('Failed to load simulation data:', error)
        const message = getHumpMissingReferenceMessage(error, 'hump.sim.messages.loadSimulationFailed')
        ElMessage.error(message)
        loadErrorMessage.value = message
    } finally {
        if (loadVersion === simulationLoadVersion) {
            loadingSimulation.value = false
        }
    }
}

watch(
    () => props.selectedInstanceId,
    () => {
        simulationLoadVersion++
        void loadHeadwayCheckSchemes()
    },
    { immediate: true }
)

watch(() => props.activationKey, () => {
    if (!props.selectedInstanceId) {
        return
    }

    void loadHeadwayCheckSchemes({
        preserveSelection: true,
        resetState: false
    })
})

watch(selectedHeadwayCheckSchemeID, () => {
    simulationLoadVersion++
    void loadSimulationData()
})

onMounted(() => {
    nextTick(() => {
        updateSvgWidth()

        if (typeof ResizeObserver !== 'undefined' && svgWrapperRef.value) {
            resizeObserver = new ResizeObserver(() => {
                updateSvgWidth()
            })
            resizeObserver.observe(svgWrapperRef.value)
        } else {
            window.addEventListener('resize', updateSvgWidth)
        }
    })
})

onBeforeUnmount(() => {
    if (resizeObserver) {
        resizeObserver.disconnect()
        resizeObserver = null
    }
    window.removeEventListener('resize', updateSvgWidth)
    cancelAnimationFrameLoop()
})
</script>

<style scoped lang="css">
.hump-sim-page {
    display: flex;
    flex-direction: column;
    width: 100%;
    height: 100%;
    min-height: 620px;
    background: #ffffff;
}

.sim-toolbar {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: 10px;
    padding: 10px 12px;
    margin: 6px 6px 12px 6px;
    border: 1px solid #dbe3f1;
    border-radius: 2px;
    background: linear-gradient(135deg, #f8fafc, #eef3ff);
    box-shadow: 0 5px 15px rgba(15, 23, 42, 0.08);
}

.toolbar-group {
    display: inline-flex;
    align-items: center;
    gap: 8px;
    min-width: 0;
}

.slider-group {
    min-width: 0;
}

.toolbar-slider {
    width: 160px;
}

.speed-slider {
    width: 120px;
}

.progress-slider {
    width: 120px;
}

.slider-value {
    font-size: 12px;
    color: #374151;
    min-width: 52px;
    text-align: right;
    font-family: "Consolas", "Courier New", monospace;
}

.toolbar-label {
    font-size: 13px;
    font-weight: 600;
    color: #1f2a37;
    white-space: nowrap;
}

.scheme-select {
    width: 260px;
    max-width: 60vw;
}

.clock-box {
    margin-left: auto;
    display: inline-flex;
    align-items: center;
    gap: 8px;
    padding: 6px 10px;
    border: 1px solid #c8d4f0;
    border-radius: 4px;
    background: #ffffff;
    line-height: 1;
}

.clock-label {
    font-size: 12px;
    color: #4b5563;
}

.clock-time {
    font-family: "Consolas", "Courier New", monospace;
    font-size: 18px;
    font-weight: 700;
    color: #1d4ed8;
    letter-spacing: 1px;
}

.sim-body {
    position: relative;
    flex: 1;
    margin: 0 6px 8px 6px;
    border: 1px solid #dbe3f1;
    border-radius: 2px;
    background: #f6f9ff;
    overflow: auto;
}

.sim-svg {
    display: block;
    min-width: 100%;
}

.grid-line {
    stroke: #dbe4f4;
    stroke-width: 1;
    stroke-dasharray: 4 4;
}

.axis-line {
    stroke: #334155;
    stroke-width: 1.2;
}

.axis-tick {
    stroke: #475569;
    stroke-width: 1;
}

.axis-label {
    font-size: 11px;
    fill: #6b7280;
}

.axis-title {
    font-size: 12px;
    fill: #334155;
    font-weight: 600;
}

.slope-area {
    fill: url(#simSlopeGradient);
}

.slope-line {
    fill: none;
    stroke: #1e3a8a;
    stroke-width: 2.5;
    stroke-linejoin: round;
    stroke-linecap: round;
}

.trajectory-line {
    fill: none;
    stroke-width: 1.5;
    stroke-linejoin: round;
    stroke-linecap: round;
    opacity: 0.35;
}

.wagon-dot {
    stroke: #ffffff;
    stroke-width: 2;
}

.wagon-info-label {
    font-size: 12px;
    fill: #1f2937;
    font-weight: 600;
    text-shadow: 0 0 2px rgba(255, 255, 255, 0.9);
}

.wagon-speed-label {
    font-size: 11px;
    fill: #0f172a;
    font-weight: 700;
    text-shadow: 0 0 2px rgba(255, 255, 255, 0.95);
}

.empty-hint {
    min-height: 420px;
    display: flex;
    align-items: center;
    justify-content: center;
    color: #6b7280;
    font-size: 14px;
    padding: 24px;
    text-align: center;
}

.floating-hint {
    position: absolute;
    left: 50%;
    top: 14px;
    transform: translateX(-50%);
    padding: 6px 10px;
    border: 1px solid #dbe3f1;
    border-radius: 4px;
    background: rgba(255, 255, 255, 0.92);
    color: #4b5563;
    font-size: 12px;
}

@media (max-width: 768px) {
    .scheme-select {
        width: 220px;
    }

    .progress-slider {
        width: 180px;
    }

    .clock-box {
        margin-left: 0;
    }

    .clock-time {
        font-size: 16px;
    }
}
</style>
