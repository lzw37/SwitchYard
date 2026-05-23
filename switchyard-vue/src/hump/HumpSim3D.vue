<template>
    <div class="hump-sim3d-page">
        <div class="sim-toolbar">
            <div class="toolbar-group">
                <label class="toolbar-label">{{ t('hump.sim.labels.scheme') }}</label>
                <el-select v-model="selectedHeadwayCheckSchemeID" :placeholder="t('hump.sim.select.placeholder')"
                    clearable filterable :no-data-text="t('hump.sim.select.noData')"
                    :no-match-text="t('hump.sim.select.noMatch')" size="small" :loading="loadingSchemes"
                    class="scheme-select" :disabled="!selectedInstanceId">
                    <el-option v-for="scheme in headwayCheckSchemeOptions" :key="scheme.id" :label="scheme.name"
                        :value="scheme.id" />
                </el-select>
            </div>

            <div class="toolbar-group playback-buttons">
                <el-button-group>
                    <el-button size="small" type="primary" :disabled="!hasTrajectoryData || isPlaying"
                        @click="handleStart" :title="t('hump.sim.buttons.start')">&#9654;</el-button>
                    <el-button size="small" type="primary" :disabled="!isPlaying" @click="handlePause"
                        :title="t('hump.sim.buttons.pause')">&#x23F8;</el-button>
                    <el-button size="small" type="primary" :disabled="!hasTrajectoryData" @click="handleEnd"
                        :title="t('hump.sim.buttons.end')">&#x23F9;</el-button>
                    <el-button size="small" type="primary" :disabled="!hasTrajectoryData" @click="stepBySecond(-1)"
                        :title="t('hump.sim.buttons.prevSecond')">&#x23EA;</el-button>
                    <el-button size="small" type="primary" :disabled="!hasTrajectoryData" @click="stepBySecond(1)"
                        :title="t('hump.sim.buttons.nextSecond')">&#x23E9;</el-button>
                    <el-button size="small" type="primary" :disabled="!hasTrajectoryData" @click="handleReset"
                        :title="t('hump.sim.buttons.reset')">&#x21BA;</el-button>
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

            <div class="toolbar-group view-group">
                <el-button size="small" @click="resetCamera"
                    :title="t('hump.sim3d.buttons.resetView')">{{ t('hump.sim3d.buttons.resetView') }}</el-button>
                <el-checkbox v-model="followCamera" size="small">
                    {{ t('hump.sim3d.labels.follow') }}
                </el-checkbox>
                <el-checkbox v-model="showLabels" size="small">
                    {{ t('hump.sim3d.labels.showLabels') }}
                </el-checkbox>
            </div>

            <div class="clock-box">
                <span class="clock-label">{{ t('hump.sim.labels.clock') }}</span>
                <span class="clock-time">{{ formattedSimulationClock }}</span>
            </div>
        </div>

        <div ref="canvasWrapperRef" class="sim-body" v-loading="loadingSimulation">
            <canvas ref="canvasRef" class="sim-canvas" />

            <div v-if="!canRender && !loadingSimulation && emptyStateText" class="empty-hint">
                {{ emptyStateText }}
            </div>

            <div v-if="canRender && !hasTrajectoryData" class="floating-hint">
                {{ t('hump.sim.hints.missingTrajectory') }}
            </div>

            <div v-if="canRender" class="wagon-legend">
                <div v-for="wagon in wagonLegendList" :key="wagon.id" class="legend-item">
                    <span class="legend-swatch" :style="{ background: wagon.color }"></span>
                    <span class="legend-label">{{ wagon.label }}</span>
                    <span class="legend-speed">{{ wagon.speedLabel }}</span>
                </div>
            </div>
        </div>
    </div>
</template>

<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import { useI18n } from 'vue-i18n'
import * as THREE from 'three'
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js'
import { CSS2DRenderer, CSS2DObject } from 'three/examples/jsm/renderers/CSS2DRenderer.js'
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
    slopeLineID: string
    wagonVelocityOnTop: number
}

interface RetarderSegment {
    id: string
    startX: number
    endX: number
    numberLabel: string
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

interface WagonLegendEntry {
    id: string
    label: string
    color: string
    speedLabel: string
}

const props = withDefaults(defineProps<Props>(), {
    selectedInstanceId: null,
    activationKey: 0
})
const { t } = useI18n()

const COLOR_SET = ['#2563eb', '#16a34a', '#dc2626', '#d97706', '#7c3aed', '#0f766e', '#1d4ed8', '#be123c']
const SPEED_PROFILE_SPACE_STEP_SIZE = 10

// 3D constants (metres)
const TRACK_WIDTH = 2.4             // visual gauge (wider than real for visibility)
const BED_WIDTH = 4.0               // slope/rail bed half-width -> full width 4m on each side
const WAGON_LENGTH = 14.0
const WAGON_WIDTH = 3.0
const WAGON_HEIGHT = 3.2
const WAGON_BODY_LIFT = 1.0         // above rail top
const RAIL_HEIGHT = 0.15
const SLEEPER_SPACING = 2.0
const PRE_SLOPE_LENGTH = 120        // flat lead-in track length before crest
const BALLAST_TOP_HALF = BED_WIDTH / 2 + 0.4
const BALLAST_BOTTOM_HALF = BALLAST_TOP_HALF + 0.6
const BALLAST_THICKNESS = 0.45
const EMBANKMENT_TOP_HALF = BALLAST_BOTTOM_HALF
// Embankment slopes 1:1.5 down from rail height to ground.
const EMBANKMENT_SIDE_SLOPE = 1.5
const GROUND_Y = -0.001             // base ground level (3D world y)

const selectedHeadwayCheckSchemeID = ref('')
const headwayCheckSchemeOptions = ref<HeadwayCheckSchemeOption[]>([])
const slopePoints = ref<SlopePoint[]>([])
const retarderSegments = ref<RetarderSegment[]>([])
const wagonTrajectories = ref<WagonTrajectory[]>([])
const wagonSpeedProfilesBySequence = ref<Record<string, SpeedPoint[]>>({})
const simulationTimeSec = ref(0)
const playbackSpeedRate = ref(1)
const isPlaying = ref(false)
const loadingSchemes = ref(false)
const loadingSimulation = ref(false)
const loadErrorMessage = ref('')
const followCamera = ref(false)
const showLabels = ref(true)
const wagonVelocityOnTop = ref(0)

const canvasWrapperRef = ref<HTMLElement | null>(null)
const canvasRef = ref<HTMLCanvasElement | null>(null)
const label2dRootRef = ref<HTMLElement | null>(null)

let renderer: THREE.WebGLRenderer | null = null
let labelRenderer: CSS2DRenderer | null = null
let scene: THREE.Scene | null = null
let camera: THREE.PerspectiveCamera | null = null
let controls: OrbitControls | null = null
let slopeGroup: THREE.Group | null = null
let retarderGroup: THREE.Group | null = null
let wagonsGroup: THREE.Group | null = null
let wagonMeshes: Map<string, THREE.Group> = new Map()
let wagonLabels: Map<string, HTMLDivElement> = new Map()
let resizeObserver: ResizeObserver | null = null
let rafId: number | null = null
let animationLastTimestamp: number | null = null
let simulationLoadVersion = 0

// ---------- normalization helpers (shared logic with HumpSim.vue) ----------
function toFiniteNumber(value: unknown): number | null {
    const parsed = Number(value)
    return Number.isFinite(parsed) ? parsed : null
}

function readString(source: any, ...keys: string[]): string {
    for (const key of keys) {
        const value = source?.[key]
        if (typeof value === 'string') return value
        if (value !== undefined && value !== null) return String(value)
    }
    return ''
}

function normalizeHeadwayCheckScheme(item: any): HeadwayCheckSchemeOption | null {
    const id = readString(item, 'id', 'ID')
    if (!id) return null
    const name = readString(item, 'name', 'Name') || id
    const humpSchemeID = readString(item, 'humpSchemeID', 'HumpSchemeID')
    const slopeLineID = readString(item, 'slopeLineID', 'SlopeLineID')
    const velocityRaw = item?.wagonVelocityOnTop ?? item?.WagonVelocityOnTop
    const velocity = toFiniteNumber(velocityRaw) ?? 0
    return { id, name, humpSchemeID, slopeLineID, wagonVelocityOnTop: velocity }
}

function normalizeFlatLayoutRetarders(payload: any): RetarderSegment[] {
    if (!payload) return []
    const positionListRaw = Array.isArray(payload?.positionList) ? payload.positionList
        : Array.isArray(payload?.PositionList) ? payload.PositionList : []
    const segmentListRaw = Array.isArray(payload?.positionSegmentList) ? payload.positionSegmentList
        : Array.isArray(payload?.PositionSegmentList) ? payload.PositionSegmentList : []
    const retarderListRaw = Array.isArray(payload?.retarderList) ? payload.retarderList
        : Array.isArray(payload?.RetarderList) ? payload.RetarderList : []

    const positionXById: Record<string, number> = {}
    for (const p of positionListRaw as any[]) {
        const pid = readString(p, 'id', 'ID')
        const x = toFiniteNumber(p?.x ?? p?.X)
        if (pid && x !== null) positionXById[pid] = x
    }
    const segmentById: Record<string, { startX: number; endX: number }> = {}
    for (const s of segmentListRaw as any[]) {
        const sid = readString(s, 'id', 'ID')
        if (!sid) continue
        let startX: number | null = null
        let endX: number | null = null
        const startPos = s?.startPosition ?? s?.StartPosition
        const endPos = s?.endPosition ?? s?.EndPosition
        if (startPos) startX = toFiniteNumber(startPos?.x ?? startPos?.X)
        if (endPos) endX = toFiniteNumber(endPos?.x ?? endPos?.X)
        if (startX === null) {
            const startPosID = readString(s, 'startPositionID', 'StartPositionID')
            if (startPosID && startPosID in positionXById) startX = positionXById[startPosID]!
        }
        if (endX === null) {
            const endPosID = readString(s, 'endPositionID', 'EndPositionID')
            if (endPosID && endPosID in positionXById) endX = positionXById[endPosID]!
        }
        if (startX === null || endX === null) continue
        segmentById[sid] = { startX, endX }
    }
    const result: RetarderSegment[] = []
    for (const r of retarderListRaw as any[]) {
        const segId = readString(r, 'bindingPositionSegmentID', 'BindingPositionSegmentID')
        const seg = segId ? segmentById[segId] : undefined
        if (!seg) continue
        const id = readString(r, 'id', 'ID') || segId
        const numberArr = Array.isArray(r?.numberArray) ? r.numberArray
            : Array.isArray(r?.NumberArray) ? r.NumberArray : []
        const numberLabel = numberArr.length > 0 ? numberArr.join('+') : ''
        const startX = Math.min(seg.startX, seg.endX)
        const endX = Math.max(seg.startX, seg.endX)
        result.push({ id, startX, endX, numberLabel })
    }
    return result
}

function normalizeSlopePoints(payload: any): SlopePoint[] {
    const positionList = Array.isArray(payload?.positionList)
        ? payload.positionList
        : Array.isArray(payload?.PositionList) ? payload.PositionList : []
    const points: SlopePoint[] = []
    for (const item of positionList as any[]) {
        const x = toFiniteNumber(item?.x ?? item?.X)
        const height = toFiniteNumber(item?.height ?? item?.Height)
        if (x === null || height === null) continue
        points.push({ x, height })
    }
    points.sort((a, b) => a.x - b.x)
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
    return extractSpeedProfileItems(payload)
        .map((item: any, index: number) => {
            const wagon = item?.wagon ?? item?.Wagon ?? {}
            const sequenceValue = toFiniteNumber(wagon?.sequence ?? wagon?.Sequence)
            const sequence = sequenceValue === null ? index + 1 : Math.max(1, Math.floor(sequenceValue))
            const positionListRaw = Array.isArray(item?.positionList) ? item.positionList
                : Array.isArray(item?.PositionList) ? item.PositionList : []
            const speedListRaw = Array.isArray(item?.speedList) ? item.speedList
                : Array.isArray(item?.SpeedList) ? item.SpeedList : []
            const n = Math.min(positionListRaw.length, speedListRaw.length)
            if (n <= 0) return null
            const points: SpeedPoint[] = []
            for (let i = 0; i < n; i++) {
                const x = toFiniteNumber(positionListRaw[i])
                const speed = toFiniteNumber(speedListRaw[i])
                if (x === null || speed === null) continue
                points.push({ x, speed })
            }
            if (points.length <= 0) return null
            points.sort((a, b) => a.x - b.x)
            return { sequence, points } as WagonSpeedProfile
        })
        .filter((x): x is WagonSpeedProfile => x !== null)
}

function normalizeWagonTypeMap(payload: any): Record<string, string> {
    const rows = Array.isArray(payload) ? payload : []
    const map: Record<string, string> = {}
    rows.forEach((row: any) => {
        const id = readString(row, 'id', 'ID')
        if (!id) return
        const wagonType = readString(row, 'wagonType', 'WagonType')
        if (!wagonType) return
        map[id] = wagonType
    })
    return map
}

function normalizeWagonTrajectories(payload: any, wagonTypeMap: Record<string, string>): WagonTrajectory[] {
    return extractRunningTimeItems(payload)
        .map((item: any, index: number) => {
            const wagon = item?.wagon ?? item?.Wagon ?? {}
            const sequenceValue = toFiniteNumber(wagon?.sequence ?? wagon?.Sequence)
            const sequence = sequenceValue === null ? index + 1 : Math.max(1, Math.floor(sequenceValue))
            const humpCalculationID = readString(wagon, 'humpCalculationID', 'HumpCalculationID')
            const wagonType = wagonTypeMap[humpCalculationID] || t('hump.sim.labels.unknown')

            const positionListRaw = Array.isArray(item?.positionList) ? item.positionList
                : Array.isArray(item?.PositionList) ? item.PositionList : []
            const runningTimeListRaw = Array.isArray(item?.runningTimeList) ? item.runningTimeList
                : Array.isArray(item?.RunningTimeList) ? item.RunningTimeList : []

            const n = Math.min(positionListRaw.length, runningTimeListRaw.length)
            if (n <= 0) return null
            const rawPoints: TrajectoryPoint[] = []
            for (let i = 0; i < n; i++) {
                const x = toFiniteNumber(positionListRaw[i])
                const time = toFiniteNumber(runningTimeListRaw[i])
                if (x === null || time === null) continue
                rawPoints.push({ x, time })
            }
            if (rawPoints.length <= 0) return null
            rawPoints.sort((a, b) => a.time - b.time)
            const points: TrajectoryPoint[] = []
            rawPoints.forEach(p => {
                const last = points[points.length - 1]
                if (!last || Math.abs(last.time - p.time) > 1e-9) { points.push(p); return }
                points[points.length - 1] = p
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
        .filter((x): x is WagonTrajectory => x !== null)
}

// ---------- derived state ----------
const hasTrajectoryData = computed(() => wagonTrajectories.value.length > 0)

const maxSimulationTime = computed(() => {
    let maxTime = 0
    wagonTrajectories.value.forEach(t => {
        const last = t.points[t.points.length - 1]
        if (last && last.time > maxTime) maxTime = last.time
    })
    return maxTime
})

const slopeMinX = computed(() => slopePoints.value[0]?.x ?? 0)

function getSlopeHeightAtX(x: number): number {
    const pts = slopePoints.value
    if (pts.length === 0) return 0
    const first = pts[0]!
    if (pts.length === 1) return first.height
    if (x <= first.x) return first.height
    for (let i = 0; i < pts.length - 1; i++) {
        const c = pts[i]!, n = pts[i + 1]!
        if (x > n.x) continue
        const dx = n.x - c.x
        if (Math.abs(dx) < 1e-9) return n.height
        const r = (x - c.x) / dx
        return c.height + r * (n.height - c.height)
    }
    return pts[pts.length - 1]!.height
}

function getWagonXAtTime(traj: WagonTrajectory, currentTime: number): number {
    const pts = traj.points
    if (pts.length === 0) return slopeMinX.value
    const first = pts[0]!, last = pts[pts.length - 1]!
    const safe = Math.max(0, currentTime)
    if (safe <= first.time) {
        // Pre-release: wagon is moving at constant push-peak speed toward the crest.
        const v = wagonVelocityOnTop.value
        if (v > 1e-6) {
            const dt = first.time - safe
            const x = first.x - v * dt
            const minX = slopeMinX.value - PRE_SLOPE_LENGTH
            return Math.max(minX, x)
        }
        // Fallback (no push speed available): linear interpolation.
        if (first.time <= 1e-9) return first.x
        const r = Math.max(0, Math.min(1, safe / first.time))
        return slopeMinX.value + (first.x - slopeMinX.value) * r
    }
    for (let i = 0; i < pts.length - 1; i++) {
        const c = pts[i]!, n = pts[i + 1]!
        if (safe > n.time) continue
        const dt = n.time - c.time
        if (Math.abs(dt) < 1e-9) return n.x
        const r = (safe - c.time) / dt
        return c.x + r * (n.x - c.x)
    }
    return last.x
}

function getWagonSpeedAtX(sequence: number, x: number): number | null {
    const pts = wagonSpeedProfilesBySequence.value[String(sequence)] || []
    if (pts.length === 0) {
        // No speed profile yet; fall back to push-peak speed if before crest.
        if (x < slopeMinX.value && wagonVelocityOnTop.value > 0) return wagonVelocityOnTop.value
        return null
    }
    const first = pts[0]!, last = pts[pts.length - 1]!
    // Pre-release flat track: wagon moves at push-peak speed.
    if (x < first.x && wagonVelocityOnTop.value > 0) return wagonVelocityOnTop.value
    if (x <= first.x) return first.speed
    for (let i = 0; i < pts.length - 1; i++) {
        const c = pts[i]!, n = pts[i + 1]!
        if (x > n.x) continue
        const dx = n.x - c.x
        if (Math.abs(dx) < 1e-9) return n.speed
        const r = (x - c.x) / dx
        return c.speed + r * (n.speed - c.speed)
    }
    return last.speed
}

function getSlopeSlopeAtX(x: number): number {
    const pts = slopePoints.value
    if (pts.length < 2) return 0
    // Pre-slope flat lead-in: zero gradient before the first slope point.
    if (x < pts[0]!.x) return 0
    for (let i = 0; i < pts.length - 1; i++) {
        const c = pts[i]!, n = pts[i + 1]!
        if (x >= c.x && x <= n.x) {
            const dx = n.x - c.x
            if (Math.abs(dx) < 1e-9) return 0
            return (n.height - c.height) / dx
        }
    }
    const last = pts[pts.length - 1]!, prev = pts[pts.length - 2]!
    const dx = last.x - prev.x
    return dx < 1e-9 ? 0 : (last.height - prev.height) / dx
}

const wagonLegendList = computed<WagonLegendEntry[]>(() => {
    return wagonTrajectories.value.map(traj => {
        const x = getWagonXAtTime(traj, simulationTimeSec.value)
        const speed = getWagonSpeedAtX(traj.sequence, x)
        return {
            id: traj.id,
            label: `${traj.sequence} | ${traj.wagonType}`,
            color: traj.color,
            speedLabel: speed === null
                ? `${t('hump.sim.labels.unknown')} ${t('hump.sim.units.speed')}`
                : `${speed.toFixed(2)} ${t('hump.sim.units.speed')}`
        }
    })
})

function formatClock(seconds: number): string {
    const s = Math.max(0, Math.floor(seconds))
    const hh = Math.floor(s / 3600), mm = Math.floor((s % 3600) / 60), ss = s % 60
    return `${String(hh).padStart(2, '0')}:${String(mm).padStart(2, '0')}:${String(ss).padStart(2, '0')}`
}

function formatProgressTooltip(value: number): string { return formatClock(value) }

const progressSliderMax = computed(() => maxSimulationTime.value > 0 ? maxSimulationTime.value : 1)
const progressPercentText = computed(() => {
    if (maxSimulationTime.value <= 0) return '0.0%'
    const p = (simulationTimeSec.value / maxSimulationTime.value) * 100
    return `${Math.max(0, Math.min(100, p)).toFixed(1)}%`
})
const formattedSimulationClock = computed(() => formatClock(simulationTimeSec.value))
const canRender = computed(() => !loadingSimulation.value && slopePoints.value.length > 0)

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

// ---------- playback controls ----------
function cancelRafLoop() {
    if (rafId !== null) { window.cancelAnimationFrame(rafId); rafId = null }
    animationLastTimestamp = null
}

function rafTick(timestamp: number) {
    // Advance simulation time when playing
    if (isPlaying.value) {
        if (animationLastTimestamp === null) {
            animationLastTimestamp = timestamp
        } else {
            const ds = ((timestamp - animationLastTimestamp) / 1000) * playbackSpeedRate.value
            animationLastTimestamp = timestamp
            const next = simulationTimeSec.value + ds
            if (next >= maxSimulationTime.value) {
                simulationTimeSec.value = maxSimulationTime.value
                isPlaying.value = false
                animationLastTimestamp = null
            } else {
                simulationTimeSec.value = next
            }
        }
    } else {
        animationLastTimestamp = null
    }

    // Always render (so slider drag updates, camera orbits, etc.)
    updateWagonTransforms()
    if (controls) controls.update()
    if (renderer && scene && camera) renderer.render(scene, camera)
    if (labelRenderer && scene && camera) labelRenderer.render(scene, camera)

    rafId = window.requestAnimationFrame(rafTick)
}

function ensureRafLoop() {
    if (rafId === null) {
        rafId = window.requestAnimationFrame(rafTick)
    }
}

function handleStart() {
    if (!hasTrajectoryData.value) return
    if (simulationTimeSec.value >= maxSimulationTime.value) simulationTimeSec.value = 0
    isPlaying.value = true
    animationLastTimestamp = null
    ensureRafLoop()
}
function handlePause() { isPlaying.value = false; animationLastTimestamp = null }
function handleEnd() {
    if (!hasTrajectoryData.value) return
    handlePause()
    simulationTimeSec.value = maxSimulationTime.value
}
function stepBySecond(offset: number) {
    if (!hasTrajectoryData.value) return
    handlePause()
    const next = simulationTimeSec.value + offset
    simulationTimeSec.value = Math.max(0, Math.min(maxSimulationTime.value, next))
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

// ---------- three.js setup ----------
function initThree() {
    if (!canvasRef.value || !canvasWrapperRef.value) return
    if (renderer) return

    const width = Math.max(1, canvasWrapperRef.value.clientWidth)
    const height = Math.max(1, canvasWrapperRef.value.clientHeight)

    scene = new THREE.Scene()
    scene.background = new THREE.Color(0xeaf2ff)
    scene.fog = new THREE.Fog(0xeaf2ff, 200, 1200)

    camera = new THREE.PerspectiveCamera(55, width / height, 0.5, 4000)
    camera.position.set(60, 40, 80)

    renderer = new THREE.WebGLRenderer({ canvas: canvasRef.value, antialias: true })
    renderer.setPixelRatio(window.devicePixelRatio)
    renderer.setSize(width, height, false)
    renderer.shadowMap.enabled = true
    renderer.shadowMap.type = THREE.PCFSoftShadowMap

    // CSS2D label renderer for HTML overlays attached to 3D positions
    labelRenderer = new CSS2DRenderer()
    labelRenderer.setSize(width, height)
    const labelDom = labelRenderer.domElement
    labelDom.style.position = 'absolute'
    labelDom.style.left = '0'
    labelDom.style.top = '0'
    labelDom.style.pointerEvents = 'none'
    canvasWrapperRef.value.appendChild(labelDom)
    label2dRootRef.value = labelDom

    controls = new OrbitControls(camera, renderer.domElement)
    controls.enableDamping = true
    controls.dampingFactor = 0.08
    controls.minDistance = 5
    controls.maxDistance = 1500
    // Keep the camera above the ground plane so the user can't tumble below it.
    controls.maxPolarAngle = Math.PI * 0.495

    // Lights
    const ambient = new THREE.AmbientLight(0xffffff, 0.55)
    scene.add(ambient)
    const sun = new THREE.DirectionalLight(0xffffff, 0.9)
    sun.position.set(120, 200, 80)
    sun.castShadow = true
    sun.shadow.mapSize.set(2048, 2048)
    sun.shadow.camera.near = 10
    sun.shadow.camera.far = 600
    sun.shadow.camera.left = -200
    sun.shadow.camera.right = 200
    sun.shadow.camera.top = 200
    sun.shadow.camera.bottom = -200
    scene.add(sun)

    // Ground (reference plane)
    const groundGeo = new THREE.PlaneGeometry(8000, 8000)
    const groundMat = new THREE.MeshStandardMaterial({ color: 0x9bb285, roughness: 1, metalness: 0 })
    const ground = new THREE.Mesh(groundGeo, groundMat)
    ground.rotation.x = -Math.PI / 2
    ground.position.y = GROUND_Y
    ground.receiveShadow = true
    scene.add(ground)

    // Groups
    slopeGroup = new THREE.Group()
    scene.add(slopeGroup)
    retarderGroup = new THREE.Group()
    scene.add(retarderGroup)
    wagonsGroup = new THREE.Group()
    scene.add(wagonsGroup)

    ensureRafLoop()
}

function disposeObject3D(obj: THREE.Object3D) {
    obj.traverse(child => {
        // CSS2DObject children attach their HTML element to the labelRenderer's DOM
        // root on first render; THREE never auto-removes them when the object is
        // detached from the scene. We must remove the element ourselves to avoid
        // leftover labels (e.g. when the user switches the headway-check scheme).
        const css = child as unknown as { isCSS2DObject?: boolean; element?: HTMLElement }
        if (css && css.isCSS2DObject && css.element && css.element.parentNode) {
            css.element.parentNode.removeChild(css.element)
        }
        const mesh = child as THREE.Mesh
        if (mesh.geometry) mesh.geometry.dispose()
        const mat = mesh.material
        if (Array.isArray(mat)) mat.forEach(m => m.dispose())
        else if (mat) (mat as THREE.Material).dispose()
    })
}

function clearGroup(group: THREE.Group | null) {
    if (!group) return
    while (group.children.length) {
        const child = group.children[0]!
        group.remove(child)
        disposeObject3D(child)
    }
}

function buildSlopeGeometry() {
    if (!slopeGroup) return
    clearGroup(slopeGroup)
    clearGroup(retarderGroup)
    wagonMeshes.clear()
    wagonLabels.clear()
    clearGroup(wagonsGroup)

    const pts = slopePoints.value
    if (pts.length < 2) return

    // Build a sample list that covers a flat lead-in area at the slope's first height.
    const first = pts[0]!
    const last = pts[pts.length - 1]!
    const xCenter = (first.x + last.x) / 2

    type Sample = { x: number; height: number }
    const samples: Sample[] = []
    // Insert lead-in samples (flat) before the original first point.
    const leadInStart = first.x - PRE_SLOPE_LENGTH
    samples.push({ x: leadInStart, height: first.height })
    samples.push({ x: first.x - 0.001, height: first.height })
    for (const p of pts) samples.push({ x: p.x, height: p.height })

    const halfBed = BED_WIDTH / 2

    // ---- Bed (concrete-ish strip) ----
    const bedThickness = 0.25
    const bedPositions: number[] = []
    const bedIndices: number[] = []
    for (const s of samples) {
        bedPositions.push(s.x - xCenter, s.height, -halfBed)
        bedPositions.push(s.x - xCenter, s.height, halfBed)
    }
    for (let i = 0; i < samples.length - 1; i++) {
        const a = i * 2, b = a + 1, c = a + 2, d = a + 3
        bedIndices.push(a, c, b, b, c, d)
    }
    const bedBottomStart = samples.length * 2
    for (const s of samples) {
        bedPositions.push(s.x - xCenter, s.height - bedThickness, -halfBed)
        bedPositions.push(s.x - xCenter, s.height - bedThickness, halfBed)
    }
    for (let i = 0; i < samples.length - 1; i++) {
        const a = bedBottomStart + i * 2, b = a + 1, c = a + 2, d = a + 3
        bedIndices.push(a, b, c, b, d, c)
    }
    // Bed side walls
    for (let i = 0; i < samples.length - 1; i++) {
        const tA = i * 2, tB = tA + 2
        const bA = bedBottomStart + i * 2, bB = bA + 2
        bedIndices.push(tA, bA, tB, bA, bB, tB)
        bedIndices.push(tA + 1, tB + 1, bA + 1, bA + 1, tB + 1, bB + 1)
    }
    const bedGeom = new THREE.BufferGeometry()
    bedGeom.setAttribute('position', new THREE.Float32BufferAttribute(bedPositions, 3))
    bedGeom.setIndex(bedIndices)
    bedGeom.computeVertexNormals()
    const bedMat = new THREE.MeshStandardMaterial({ color: 0x8a8a86, roughness: 0.95, metalness: 0 })
    const bedMesh = new THREE.Mesh(bedGeom, bedMat)
    bedMesh.receiveShadow = true
    bedMesh.castShadow = true
    slopeGroup.add(bedMesh)

    // ---- Ballast layer (gravel) - trapezoidal cross-section under the bed ----
    const ballastTopY = (h: number) => h - bedThickness
    const ballastBottomY = (h: number) => h - bedThickness - BALLAST_THICKNESS
    const ballastPositions: number[] = []
    const ballastIndices: number[] = []
    // 4 vertices per sample: top-left, top-right, bottom-left, bottom-right
    for (const s of samples) {
        ballastPositions.push(s.x - xCenter, ballastTopY(s.height), -BALLAST_TOP_HALF)
        ballastPositions.push(s.x - xCenter, ballastTopY(s.height), BALLAST_TOP_HALF)
        ballastPositions.push(s.x - xCenter, ballastBottomY(s.height), -BALLAST_BOTTOM_HALF)
        ballastPositions.push(s.x - xCenter, ballastBottomY(s.height), BALLAST_BOTTOM_HALF)
    }
    for (let i = 0; i < samples.length - 1; i++) {
        const v0 = i * 4
        const v1 = v0 + 4
        const tl0 = v0, tr0 = v0 + 1, bl0 = v0 + 2, br0 = v0 + 3
        const tl1 = v1, tr1 = v1 + 1, bl1 = v1 + 2, br1 = v1 + 3
        // top
        ballastIndices.push(tl0, tr0, tl1, tr0, tr1, tl1)
        // bottom (face down)
        ballastIndices.push(bl0, bl1, br0, br0, bl1, br1)
        // -z slanted side (top-left -> bottom-left)
        ballastIndices.push(tl0, bl0, tl1, bl0, bl1, tl1)
        // +z slanted side
        ballastIndices.push(tr0, tr1, br0, br0, tr1, br1)
    }
    const ballastGeom = new THREE.BufferGeometry()
    ballastGeom.setAttribute('position', new THREE.Float32BufferAttribute(ballastPositions, 3))
    ballastGeom.setIndex(ballastIndices)
    ballastGeom.computeVertexNormals()
    const ballastMat = new THREE.MeshStandardMaterial({
        color: 0x6f6a60,
        roughness: 1.0,
        metalness: 0,
        flatShading: true
    })
    const ballastMesh = new THREE.Mesh(ballastGeom, ballastMat)
    ballastMesh.receiveShadow = true
    ballastMesh.castShadow = true
    slopeGroup.add(ballastMesh)

    // ---- Earth embankment - wider trapezoid that goes from ballast bottom to ground ----
    const earthPositions: number[] = []
    const earthIndices: number[] = []
    for (const s of samples) {
        const topY = ballastBottomY(s.height)
        const heightAboveGround = Math.max(0, topY - GROUND_Y)
        // Side slope 1 : EMBANKMENT_SIDE_SLOPE => widen by slope * height per side.
        const widen = heightAboveGround * EMBANKMENT_SIDE_SLOPE
        const bottomHalf = EMBANKMENT_TOP_HALF + widen
        // 4 verts per sample: top -EMB, top +EMB, bot -bottomHalf, bot +bottomHalf
        earthPositions.push(s.x - xCenter, topY, -EMBANKMENT_TOP_HALF)
        earthPositions.push(s.x - xCenter, topY, EMBANKMENT_TOP_HALF)
        earthPositions.push(s.x - xCenter, GROUND_Y, -bottomHalf)
        earthPositions.push(s.x - xCenter, GROUND_Y, bottomHalf)
    }
    for (let i = 0; i < samples.length - 1; i++) {
        const v0 = i * 4
        const v1 = v0 + 4
        const tl0 = v0, tr0 = v0 + 1, bl0 = v0 + 2, br0 = v0 + 3
        const tl1 = v1, tr1 = v1 + 1, bl1 = v1 + 2, br1 = v1 + 3
        // -z slanted side
        earthIndices.push(tl0, bl0, tl1, bl0, bl1, tl1)
        // +z slanted side
        earthIndices.push(tr0, tr1, br0, br0, tr1, br1)
        // No top face (covered by ballast). No bottom face needed (ground hides it).
    }
    // End caps
    if (samples.length >= 2) {
        const v0 = 0
        earthIndices.push(v0, v0 + 1, v0 + 2, v0 + 1, v0 + 3, v0 + 2)
        const vL = (samples.length - 1) * 4
        earthIndices.push(vL, vL + 2, vL + 1, vL + 1, vL + 2, vL + 3)
    }
    const earthGeom = new THREE.BufferGeometry()
    earthGeom.setAttribute('position', new THREE.Float32BufferAttribute(earthPositions, 3))
    earthGeom.setIndex(earthIndices)
    earthGeom.computeVertexNormals()
    const earthMat = new THREE.MeshStandardMaterial({
        color: 0x8a6a45,
        roughness: 1.0,
        metalness: 0
    })
    const earthMesh = new THREE.Mesh(earthGeom, earthMat)
    earthMesh.receiveShadow = true
    earthMesh.castShadow = true
    slopeGroup.add(earthMesh)

    // ---- Rails: continuous tubes along the entire (flat lead-in + slope) path ----
    const railMat = new THREE.MeshStandardMaterial({ color: 0x4a4a4a, roughness: 0.5, metalness: 0.8 })
    const trackHalf = TRACK_WIDTH / 2
    const railOffsets = [-trackHalf, trackHalf]
    for (const zOffset of railOffsets) {
        const railPoints: THREE.Vector3[] = []
        for (const s of samples) {
            railPoints.push(new THREE.Vector3(s.x - xCenter, s.height + RAIL_HEIGHT / 2, zOffset))
        }
        const curve = new THREE.CatmullRomCurve3(railPoints, false, 'catmullrom', 0)
        const tubeGeo = new THREE.TubeGeometry(curve, Math.max(64, samples.length * 2), RAIL_HEIGHT / 2, 8, false)
        const railMesh = new THREE.Mesh(tubeGeo, railMat)
        railMesh.castShadow = true
        railMesh.receiveShadow = true
        slopeGroup.add(railMesh)
    }

    // ---- Sleepers along the entire path ----
    const sleeperMat = new THREE.MeshStandardMaterial({ color: 0x5a3a20, roughness: 0.95, metalness: 0 })
    const sleeperGeo = new THREE.BoxGeometry(0.25, 0.18, TRACK_WIDTH + 0.6)
    const xStart = leadInStart
    const xEnd = last.x
    for (let sx = xStart; sx <= xEnd; sx += SLEEPER_SPACING) {
        const sy = (sx < first.x ? first.height : getSlopeHeightAtX(sx)) - 0.02
        const m = new THREE.Mesh(sleeperGeo, sleeperMat)
        m.position.set(sx - xCenter, sy, 0)
        const slope = sx < first.x ? 0 : getSlopeSlopeAtX(sx)
        m.rotation.z = Math.atan(slope)
        m.castShadow = true
        m.receiveShadow = true
        slopeGroup.add(m)
    }

    // ---- Hump crest marker line (optional vertical guide at first slope point) ----
    const crestMat = new THREE.LineBasicMaterial({ color: 0xff6b6b, transparent: true, opacity: 0.5 })
    const crestGeo = new THREE.BufferGeometry().setFromPoints([
        new THREE.Vector3(first.x - xCenter, first.height, -BED_WIDTH),
        new THREE.Vector3(first.x - xCenter, first.height + 6, -BED_WIDTH)
    ])
    slopeGroup.add(new THREE.Line(crestGeo, crestMat))

    // ---- Retarders mounted on the rails along the slope ----
    buildRetarders(xCenter)

    // ---- Build wagons with CSS2D labels ----
    for (const traj of wagonTrajectories.value) {
        const wagonGroup = buildWagonMesh(traj.color, traj.id)
        wagonsGroup!.add(wagonGroup)
        wagonMeshes.set(traj.id, wagonGroup)
    }

    fitCameraToSlope()
}

// Retarder visual constants (metres). Matches the typical "夹片式减速器" appearance:
// each rail carries a pair of beam-like clip jaws hugging the rail on both sides,
// joined by a few cross-ties, with a small control housing between the two rails.
const RETARDER_CLIP_HEIGHT = 0.22
const RETARDER_CLIP_THICKNESS = 0.06
const RETARDER_CLIP_GAP_HALF = 0.10           // half-gap between inner/outer clip and rail centre
const RETARDER_CROSSTIE_COUNT_PER_M = 0.4     // ~one cross tie every 2.5m
const RETARDER_CROSSTIE_LENGTH = TRACK_WIDTH + 0.4
const RETARDER_CROSSTIE_THICKNESS = 0.06
const RETARDER_CROSSTIE_HEIGHT = 0.10
const RETARDER_HOUSING_LENGTH = 1.6
const RETARDER_HOUSING_WIDTH = TRACK_WIDTH * 0.55
const RETARDER_HOUSING_HEIGHT = 0.55
const RETARDER_END_CAP_LENGTH = 0.35

// High-visibility palette so retarders stand out against the dark rails / grey ballast.
// Clips: vivid orange ("safety orange"); ties: deep red; housing: bright yellow with emissive glow.
const retarderClipMat = new THREE.MeshStandardMaterial({
    color: 0xff6a00,
    roughness: 0.45,
    metalness: 0.35,
    emissive: 0x803000,
    emissiveIntensity: 0.35
})
const retarderTieMat = new THREE.MeshStandardMaterial({
    color: 0xd62828,
    roughness: 0.7,
    metalness: 0.25,
    emissive: 0x4a0d0d,
    emissiveIntensity: 0.3
})
const retarderHousingMat = new THREE.MeshStandardMaterial({
    color: 0xffd60a,
    roughness: 0.55,
    metalness: 0.2,
    emissive: 0x6a5500,
    emissiveIntensity: 0.4
})
const retarderEndCapMat = new THREE.MeshStandardMaterial({
    color: 0x111418,
    roughness: 0.5,
    metalness: 0.6
})

function buildRetarders(xCenter: number) {
    if (!retarderGroup) return
    clearGroup(retarderGroup)
    if (retarderSegments.value.length === 0 || slopePoints.value.length < 2) return

    const trackHalf = TRACK_WIDTH / 2
    const railTopY = RAIL_HEIGHT
    const clipBaseY = railTopY - RETARDER_CLIP_HEIGHT / 2 + 0.04
    const tieY = clipBaseY - RETARDER_CLIP_HEIGHT / 2 - RETARDER_CROSSTIE_HEIGHT / 2 + 0.02

    for (const ret of retarderSegments.value) {
        const length = ret.endX - ret.startX
        if (!(length > 0.2)) continue
        const xMid = (ret.startX + ret.endX) / 2
        const yMid = getSlopeHeightAtX(xMid)
        const angle = Math.atan(getSlopeSlopeAtX(xMid))

        const group = new THREE.Group()
        group.position.set(xMid - xCenter, yMid, 0)
        group.rotation.set(0, 0, angle)

        // Two clip beams per rail (inner + outer). Slightly shorter than full segment
        // and given small end caps for a more realistic silhouette.
        const beamLength = Math.max(0.4, length - RETARDER_END_CAP_LENGTH * 2)
        const beamGeo = new THREE.BoxGeometry(beamLength, RETARDER_CLIP_HEIGHT, RETARDER_CLIP_THICKNESS)
        const railZs = [-trackHalf, trackHalf]
        for (const railZ of railZs) {
            for (const offsetSign of [-1, 1]) {
                const clip = new THREE.Mesh(beamGeo, retarderClipMat)
                clip.position.set(0, clipBaseY, railZ + offsetSign * RETARDER_CLIP_GAP_HALF)
                clip.castShadow = true
                clip.receiveShadow = true
                group.add(clip)
            }
        }
        // End caps (rounded look) at both ends of the retarder body.
        const capGeo = new THREE.BoxGeometry(
            RETARDER_END_CAP_LENGTH,
            RETARDER_CLIP_HEIGHT * 0.85,
            TRACK_WIDTH + RETARDER_CLIP_GAP_HALF * 2 + RETARDER_CLIP_THICKNESS
        )
        for (const sign of [-1, 1]) {
            const cap = new THREE.Mesh(capGeo, retarderEndCapMat)
            cap.position.set(sign * (length / 2 - RETARDER_END_CAP_LENGTH / 2), clipBaseY, 0)
            cap.castShadow = true
            cap.receiveShadow = true
            group.add(cap)
        }

        // Cross ties spanning the gauge (visible between the rails in the photo).
        const tieCount = Math.max(2, Math.round(length * RETARDER_CROSSTIE_COUNT_PER_M))
        const tieGeo = new THREE.BoxGeometry(
            RETARDER_CROSSTIE_THICKNESS,
            RETARDER_CROSSTIE_HEIGHT,
            RETARDER_CROSSTIE_LENGTH
        )
        const tieSpan = beamLength
        for (let i = 0; i < tieCount; i++) {
            const t = tieCount === 1 ? 0.5 : i / (tieCount - 1)
            const tx = -tieSpan / 2 + t * tieSpan
            const tie = new THREE.Mesh(tieGeo, retarderTieMat)
            tie.position.set(tx, tieY, 0)
            tie.castShadow = true
            tie.receiveShadow = true
            group.add(tie)
        }

        // Central control housing between the rails (the white-painted box in the photo).
        const housingLen = Math.min(RETARDER_HOUSING_LENGTH, length * 0.5)
        if (housingLen > 0.3) {
            const housingGeo = new THREE.BoxGeometry(housingLen, RETARDER_HOUSING_HEIGHT, RETARDER_HOUSING_WIDTH)
            const housing = new THREE.Mesh(housingGeo, retarderHousingMat)
            housing.position.set(0, clipBaseY + RETARDER_HOUSING_HEIGHT / 2 - RETARDER_CLIP_HEIGHT / 2 + 0.02, 0)
            housing.castShadow = true
            housing.receiveShadow = true
            group.add(housing)
        }

        retarderGroup.add(group)
    }
}

function buildWagonMesh(colorHex: string, wagonId: string): THREE.Group {
    const group = new THREE.Group()
    const color = new THREE.Color(colorHex)

    // Chassis (darker)
    const chassisMat = new THREE.MeshStandardMaterial({ color: 0x2a2f38, roughness: 0.85, metalness: 0.3 })
    const chassis = new THREE.Mesh(new THREE.BoxGeometry(WAGON_LENGTH, 0.35, WAGON_WIDTH), chassisMat)
    chassis.position.y = WAGON_BODY_LIFT
    chassis.castShadow = true
    group.add(chassis)

    // Body
    const bodyMat = new THREE.MeshStandardMaterial({ color, roughness: 0.65, metalness: 0.15 })
    const body = new THREE.Mesh(new THREE.BoxGeometry(WAGON_LENGTH - 1.0, WAGON_HEIGHT, WAGON_WIDTH - 0.2), bodyMat)
    body.position.y = WAGON_BODY_LIFT + 0.18 + WAGON_HEIGHT / 2
    body.castShadow = true
    group.add(body)

    // Roof stripe for visibility
    const topMat = new THREE.MeshStandardMaterial({ color: 0xffffff, roughness: 0.8, metalness: 0.1 })
    const top = new THREE.Mesh(new THREE.BoxGeometry(WAGON_LENGTH - 2.0, 0.05, WAGON_WIDTH - 0.6), topMat)
    top.position.y = WAGON_BODY_LIFT + 0.18 + WAGON_HEIGHT + 0.03
    group.add(top)

    // Wheels: 4 pairs
    const wheelMat = new THREE.MeshStandardMaterial({ color: 0x1a1a1a, roughness: 0.4, metalness: 0.7 })
    const wheelGeo = new THREE.CylinderGeometry(0.45, 0.45, 0.2, 20)
    const wheelOffsetsX = [-WAGON_LENGTH / 2 + 1.8, -WAGON_LENGTH / 2 + 3.8, WAGON_LENGTH / 2 - 3.8, WAGON_LENGTH / 2 - 1.8]
    const wheelOffsetsZ = [-TRACK_WIDTH / 2, TRACK_WIDTH / 2]
    for (const wx of wheelOffsetsX) {
        for (const wz of wheelOffsetsZ) {
            const w = new THREE.Mesh(wheelGeo, wheelMat)
            w.rotation.x = Math.PI / 2
            w.position.set(wx, 0.45, wz)
            w.castShadow = true
            group.add(w)
        }
    }

    // CSS2D info label floating above the wagon.
    // Note: CSS2DRenderer overwrites the root element's `transform` every frame
    // (it sets translate(-50%,-50%) translate(Xpx,Ypx) for screen positioning).
    // To offset the visible badge upward in screen space without being clobbered,
    // we wrap it: an outer (anchor) element holds the CSS2DObject, and an inner
    // element carries our screen-space translate to lift the badge above the wagon.
    const labelEl = document.createElement('div')
    labelEl.className = 'wagon-info-label'
    labelEl.style.cssText = 'position:absolute;pointer-events:none;'
    // Inner offset wrapper: shifts the visible content upward by ~80px in screen space.
    const offsetEl = document.createElement('div')
    offsetEl.style.cssText = 'position:absolute;left:0;top:-80px;transform:translate(-50%,-100%);'
    // Vertical leader line from the offset badge down to the wagon anchor point.
    const leaderEl = document.createElement('div')
    leaderEl.style.cssText = 'position:absolute;left:50%;top:100%;width:1px;height:80px;background:rgba(31,42,55,0.55);transform:translateX(-0.5px);'
    offsetEl.appendChild(leaderEl)
    // Visible badge.
    const badgeEl = document.createElement('div')
    badgeEl.style.cssText = [
        'background:rgba(255,255,255,0.85)',
        `border:1px solid ${colorHex}`,
        'border-radius:3px',
        'padding:2px 6px',
        'font-size:10px',
        'line-height:1.25',
        'color:#1f2a37',
        'font-family:Consolas,"Courier New",monospace',
        'white-space:nowrap',
        'box-shadow:0 1px 3px rgba(15,23,42,0.18)'
    ].join(';')
    offsetEl.appendChild(badgeEl)
    labelEl.appendChild(offsetEl)
    const labelObj = new CSS2DObject(labelEl)
    labelObj.position.set(0, WAGON_BODY_LIFT + 0.18 + WAGON_HEIGHT + 0.4, 0)
    group.add(labelObj)
    wagonLabels.set(wagonId, badgeEl)

    return group
}

function updateWagonTransforms() {
    if (!wagonsGroup || slopePoints.value.length < 2) return
    const pts = slopePoints.value
    const xCenter = (pts[0]!.x + pts[pts.length - 1]!.x) / 2
    const speedUnit = t('hump.sim.units.speed')
    const unknownText = t('hump.sim.labels.unknown')

    let leadSequence = -1
    let leadPos: { x: number; y: number } | null = null

    for (const traj of wagonTrajectories.value) {
        const mesh = wagonMeshes.get(traj.id)
        if (!mesh) continue
        const x = getWagonXAtTime(traj, simulationTimeSec.value)
        const y = getSlopeHeightAtX(x)
        const slope = getSlopeSlopeAtX(x)
        const angle = Math.atan(slope)
        mesh.position.set(x - xCenter, y, 0)
        mesh.rotation.set(0, 0, angle)

        // Update floating label
        const labelEl = wagonLabels.get(traj.id)
        if (labelEl) {
            const speed = getWagonSpeedAtX(traj.sequence, x)
            const speedText = speed === null ? `${unknownText} ${speedUnit}` : `${speed.toFixed(2)} ${speedUnit}`
            // Compact single-line content keeps the label out of the wagon's silhouette.
            labelEl.innerHTML =
                `<b>#${traj.sequence}</b> ${escapeHtml(traj.wagonType)} | x=${x.toFixed(1)} h=${y.toFixed(2)} v=${speedText}`
            const parent = labelEl.parentElement as HTMLElement | null
            if (parent) parent.style.display = showLabels.value ? '' : 'none'
        }

        if (traj.sequence > leadSequence) {
            leadSequence = traj.sequence
            leadPos = { x: x - xCenter, y }
        }
    }

    if (followCamera.value && leadPos && camera && controls) {
        const target = new THREE.Vector3(leadPos.x, leadPos.y + 2, 0)
        controls.target.lerp(target, 0.15)
    }
}

function escapeHtml(s: string): string {
    return s.replace(/[&<>"']/g, c => ({
        '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
    }[c] as string))
}

function fitCameraToSlope() {
    const pts = slopePoints.value
    if (!camera || !controls || pts.length < 2) return
    const xMin = pts[0]!.x - PRE_SLOPE_LENGTH, xMax = pts[pts.length - 1]!.x
    const length = xMax - xMin
    let hMin = Infinity, hMax = -Infinity
    for (const p of pts) { if (p.height < hMin) hMin = p.height; if (p.height > hMax) hMax = p.height }
    const height = Math.max(1, hMax - hMin)

    const midY = (hMin + hMax) / 2
    // Target the centre of the rendered span (which already includes lead-in to the left of x=0).
    const xTargetWorld = -(PRE_SLOPE_LENGTH / 2) // slight bias toward lead-in side
    controls.target.set(xTargetWorld, midY, 0)
    const dist = Math.max(length * 0.6, height * 8, 60)
    camera.position.set(xTargetWorld + dist * 0.6, midY + Math.max(15, height * 2), dist * 0.9)
    camera.near = 0.5
    camera.far = Math.max(4000, dist * 6)
    camera.updateProjectionMatrix()
    controls.update()
}

function resetCamera() { fitCameraToSlope() }

// Track the wrapper's last laid-out size so we can detect the hidden -> visible
// transition that happens when the user switches tabs. el-tabs keeps the panel
// in the DOM with display:none, which collapses the wrapper to 0x0 and makes
// any user pan/zoom that happened before look "off" once the panel is shown
// again. When we return from a collapsed state we refit the camera so the
// slope is fully visible inside the canvas.
let lastWrapperWidth = 0
let lastWrapperHeight = 0

function onResize() {
    if (!renderer || !camera || !canvasWrapperRef.value) return
    const rawW = canvasWrapperRef.value.clientWidth
    const rawH = canvasWrapperRef.value.clientHeight
    const w = Math.max(1, rawW)
    const h = Math.max(1, rawH)
    renderer.setSize(w, h, false)
    if (labelRenderer) labelRenderer.setSize(w, h)
    camera.aspect = w / h
    camera.updateProjectionMatrix()
    const wasCollapsed = lastWrapperWidth < 2 || lastWrapperHeight < 2
    const nowVisible = rawW > 1 && rawH > 1
    if (wasCollapsed && nowVisible && slopePoints.value.length >= 2) {
        // Refit so the slope stays inside the visible canvas after tab activation.
        fitCameraToSlope()
    }
    lastWrapperWidth = rawW
    lastWrapperHeight = rawH
}

// ---------- data loading ----------
async function ensureHumpSchemeID(scheme: HeadwayCheckSchemeOption): Promise<string> {
    if (!props.selectedInstanceId) return ''
    if (scheme.humpSchemeID) return scheme.humpSchemeID
    const response = await axios.get('/Hump/GetHeadwayCheckSchemeById', {
        params: { instanceID: props.selectedInstanceId, id: scheme.id }
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
        retarderSegments.value = []
        wagonTrajectories.value = []
        wagonSpeedProfilesBySequence.value = {}
        resetSimulationViewState()
    }
    if (!props.selectedInstanceId) return

    loadingSchemes.value = true
    try {
        const response = await axios.get('/Hump/GetHeadwayCheckSchemes', {
            params: { instanceID: props.selectedInstanceId }
        })
        const options = (Array.isArray(response.data) ? response.data : [])
            .map(item => normalizeHeadwayCheckScheme(item))
            .filter((x): x is HeadwayCheckSchemeOption => x !== null)
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
    retarderSegments.value = []
    wagonTrajectories.value = []
    wagonSpeedProfilesBySequence.value = {}
    loadErrorMessage.value = ''

    if (!props.selectedInstanceId || !selectedHeadwayCheckSchemeID.value) return

    const scheme = headwayCheckSchemeOptions.value.find(s => s.id === selectedHeadwayCheckSchemeID.value)
    if (!scheme) { loadErrorMessage.value = t('hump.sim.messages.schemeNotFound'); return }

    const loadVersion = ++simulationLoadVersion
    loadingSimulation.value = true

    try {
        const humpSchemeID = await ensureHumpSchemeID(scheme)
        if (!humpSchemeID) { loadErrorMessage.value = t('hump.sim.messages.missingHumpScheme'); return }

        const [slopeResult, runningTimeResult, speedProfileResult, humpCalcResult, flatLayoutResult] = await Promise.allSettled([
            axios.get('/Hump/GetSlopeLayout', {
                params: { instanceID: props.selectedInstanceId, humpSchemeID }
            }),
            axios.get('/Hump/CalculateRunningTime', {
                params: { instanceID: props.selectedInstanceId, headwayCheckSchemeID: selectedHeadwayCheckSchemeID.value }
            }),
            axios.get('/Hump/CalculateSpeedProfile', {
                params: {
                    instanceID: props.selectedInstanceId,
                    headwayCheckSchemeID: selectedHeadwayCheckSchemeID.value,
                    spaceStepSize: SPEED_PROFILE_SPACE_STEP_SIZE
                }
            }),
            axios.get('/Hump/GetHumpCalculations', {
                params: { instanceID: props.selectedInstanceId, humpSchemeID }
            }),
            scheme.slopeLineID
                ? axios.get('/Hump/GetFlatLayout', {
                    params: { instanceID: props.selectedInstanceId, slopeLineID: scheme.slopeLineID }
                })
                : Promise.reject(new Error('Missing slopeLineID'))
        ])

        if (loadVersion !== simulationLoadVersion) return

        if (slopeResult.status !== 'fulfilled') {
            throw slopeResult.reason
        }
        if (runningTimeResult.status !== 'fulfilled') {
            throw runningTimeResult.reason
        }

        slopePoints.value = normalizeSlopePoints(slopeResult.value.data)
        wagonVelocityOnTop.value = scheme.wagonVelocityOnTop || 0
        const wagonTypeMap = humpCalcResult.status === 'fulfilled'
            ? normalizeWagonTypeMap(humpCalcResult.value.data) : {}
        wagonTrajectories.value = normalizeWagonTrajectories(runningTimeResult.value.data, wagonTypeMap)

        if (speedProfileResult.status === 'fulfilled') {
            const profiles = normalizeSpeedProfiles(speedProfileResult.value.data)
            const m: Record<string, SpeedPoint[]> = {}
            profiles.forEach(p => { m[String(p.sequence)] = p.points })
            wagonSpeedProfilesBySequence.value = m
        } else {
            wagonSpeedProfilesBySequence.value = {}
            ElMessage.warning(t('hump.sim.messages.loadSpeedFailed'))
        }

        if (humpCalcResult.status !== 'fulfilled') {
            ElMessage.warning(t('hump.sim.messages.loadWagonTypeFailed'))
        }

        if (flatLayoutResult.status === 'fulfilled') {
            retarderSegments.value = normalizeFlatLayoutRetarders(flatLayoutResult.value.data)
        } else {
            retarderSegments.value = []
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
        if (loadVersion === simulationLoadVersion) loadingSimulation.value = false
    }
}

// Rebuild scene when slope/trajectories change
watch([slopePoints, wagonTrajectories, retarderSegments], () => {
    if (!scene) return
    buildSlopeGeometry()
}, { deep: false })

watch(() => props.selectedInstanceId, () => {
    simulationLoadVersion++
    void loadHeadwayCheckSchemes()
}, { immediate: true })

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
        initThree()
        if (typeof ResizeObserver !== 'undefined' && canvasWrapperRef.value) {
            resizeObserver = new ResizeObserver(() => onResize())
            resizeObserver.observe(canvasWrapperRef.value)
        } else {
            window.addEventListener('resize', onResize)
        }
    })
})

onBeforeUnmount(() => {
    cancelRafLoop()
    if (resizeObserver) { resizeObserver.disconnect(); resizeObserver = null }
    window.removeEventListener('resize', onResize)
    if (slopeGroup) clearGroup(slopeGroup)
    if (retarderGroup) clearGroup(retarderGroup)
    if (wagonsGroup) clearGroup(wagonsGroup)
    if (controls) { controls.dispose(); controls = null }
    if (renderer) { renderer.dispose(); renderer = null }
    if (labelRenderer && label2dRootRef.value && label2dRootRef.value.parentElement) {
        label2dRootRef.value.parentElement.removeChild(label2dRootRef.value)
    }
    labelRenderer = null
    label2dRootRef.value = null
    scene = null
    camera = null
    wagonMeshes.clear()
    wagonLabels.clear()
})
</script>

<style scoped lang="css">
.hump-sim3d-page {
    display: flex;
    flex-direction: column;
    width: 100%;
    height: 100%;
    max-height: calc(100dvh - 104px);
    min-height: 0;
    background: #ffffff;
    overflow: hidden;
}

.sim-toolbar {
    flex: 0 0 auto;
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

.view-group {
    padding-left: 8px;
    border-left: 1px dashed #c7d2fe;
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
    flex: 1 1 auto;
    margin: 0 6px 8px 6px;
    border: 1px solid #dbe3f1;
    border-radius: 2px;
    background: #eaf2ff;
    overflow: hidden;
    contain: layout paint;
    min-height: 0;
}

.sim-canvas {
    display: block;
    width: 100%;
    height: 100%;
    outline: none;
}

.empty-hint,
.floating-hint {
    position: absolute;
    left: 50%;
    top: 50%;
    transform: translate(-50%, -50%);
    padding: 10px 18px;
    background: rgba(255, 255, 255, 0.85);
    border: 1px solid #c8d4f0;
    border-radius: 4px;
    font-size: 13px;
    color: #334155;
    pointer-events: none;
}

.floating-hint {
    top: 16px;
    left: 16px;
    transform: none;
}

.wagon-legend {
    position: absolute;
    right: 12px;
    top: 12px;
    display: flex;
    flex-direction: column;
    gap: 4px;
    padding: 8px 10px;
    background: rgba(255, 255, 255, 0.88);
    border: 1px solid #c8d4f0;
    border-radius: 4px;
    font-size: 12px;
    color: #1f2a37;
    max-height: calc(100% - 24px);
    overflow-y: auto;
    pointer-events: none;
}

.legend-item {
    display: inline-flex;
    align-items: center;
    gap: 6px;
}

.legend-swatch {
    display: inline-block;
    width: 12px;
    height: 12px;
    border-radius: 2px;
    border: 1px solid rgba(0, 0, 0, 0.1);
}

.legend-label {
    min-width: 90px;
}

.legend-speed {
    font-family: "Consolas", "Courier New", monospace;
    color: #1d4ed8;
}
</style>
