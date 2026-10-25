<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, reactive, ref, shallowRef, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { ElMessage } from 'element-plus'
import type { StationLayoutDocument, StationScheme } from '@switchyard/station-layout'
import axios from '@/utils/axios'
import { useCapacityStore } from '@/stores/capacity'
import { useStationSchemeSelection } from '@/capacity/useStationSchemeSelection'
import { useCalculationParameterSetSelection } from '@/capacity/useCalculationParameterSetSelection'
import { useCalculationParameterSets } from '@/capacity/calculationParameterSets'
import { buildTractionOccupancyUpdates, mergeTractionOccupancyTimes } from './tractionOccupancy'
import { switchyardStationLayoutGateway as gateway } from '@/capacity/switchyardStationLayoutGateway'
import TractionCharts from './TractionCharts.vue'
import { withBrakingApproach, withDepartureSection, extensionSpeedLimits, tractionModeForRoute, rotatingMassCoefficientFromFactor, summarizeTractionResult, formatTractionDuration, cellTerminalStopTime, cellOccupancyEndTime } from './tractionCalculation'
import TrainParametersDialog from './TrainParametersDialog.vue'
import TractionSetsDialog from './TractionSetsDialog.vue'
import TractionRouteResultsDialog from './TractionRouteResultsDialog.vue'
import type { TractionBatchContext, TractionBatchReport } from './tractionBatch'
import { loadProjectTrains, saveProjectTrains, useProjectTrains, type ProjectTrains } from './projectTrains'
import { prepareTractionParameterTransfers, writeTractionParameterTransfers, type TractionParameterTransferReport } from './tractionParameterTransfer'
import { createDefaultTrainPresetCatalog, cloneTrainParameters, cloneTrainPresetCatalog, parseTrainPresetCatalog, validateTrainPresetCatalog, type TrainPreset, type TrainPresetCatalog } from './trainPresets'
import LineParametersDialog from './LineParametersDialog.vue'
import { buildRouteProfile, type TractionStationRoute } from './routeProfile'
import { generateBrakingCurve, generateResistanceCurve, defaultTrainParameters, defaultSignalParameters, defaultRunOptions, type TractionRouteProfile, type TractionRunOptions } from './tractionCalculation'
import { buildTractionApiRequest } from './tractionApi'
import { applySpeedLimitPoints, pointsFromSegments, setIntervalLimit, validateSpeedLimitPoints, type SpeedLimitPoint } from './speedLimitPoints'
import { loadTractionRoutes, openTractionRoute, deleteTractionRoute, type TractionRouteCase, loadTractionSets, selectTractionSet, manageTractionSet, type TractionCalculationSet, type TractionSetAction, loadTractionWorkspace, loadTractionCalculation, TractionWorkspaceSession, calculateStoredTraction, workspaceFingerprint, overrideKey, type StoredTractionCalculation, type TractionWorkspace, type TractionWorkspaceContent, type TractionSegmentOverride } from './tractionWorkspace'
import { tractionMessages } from './tractionMessages'

const props = defineProps<{ selectedInstanceId: string; active?: boolean }>()
const { locale } = useI18n()
const ui = (zh: string, en: string) => locale.value.startsWith('zh') ? zh : en
const capacityStore = useCapacityStore()
const schemeId = useStationSchemeSelection(() => props.selectedInstanceId)
const calculationSets = ref<TractionCalculationSet[]>([])
const selectedSetId = ref('')
const setsVisible = ref(false)
const setBusy = ref(false)
const setError = ref('')
const switchingSet = ref(false)
const routeCases = ref<TractionRouteCase[]>([])
const selectedCaseId = ref('')
const routeResultsVisible = ref(false)
const batchBusy = ref(false)
const batchScopeKey = computed(() => JSON.stringify([props.selectedInstanceId, schemeId.value, selectedSetId.value, props.active]))
const routeBusy = ref(false)
const routeError = ref('')
const setScope = () => ({ ...stationScope(), calculationSetID: selectedSetId.value })
const scopeKey = () => JSON.stringify([props.selectedInstanceId, schemeId.value, selectedSetId.value, selectedCaseId.value])
const stationScope = () => ({ instanceID: props.selectedInstanceId, stationSchemeID: schemeId.value })
const selectedSetName = computed(() => calculationSets.value.find(item => item.id === selectedSetId.value)?.name || '')
const parameterSetId = useCalculationParameterSetSelection(() => props.selectedInstanceId, schemeId)
const { sets: parameterSets, loading: loadingParameterSets, error: parameterSetError, reload: reloadParameterSets } =
    useCalculationParameterSets(() => props.selectedInstanceId, () => schemeId.value)
const updatingOccupancy = ref(false)
watch(parameterSets, sets => {
    if (sets.length && !sets.some(set => set.id === parameterSetId.value)) {
        parameterSetId.value = sets.find(set => set.isDefault)?.id || sets[0]!.id
    }
})
const schemes = ref<StationScheme[]>([])
const routes = ref<(TractionStationRoute & { type: string; description: string })[]>([])
const document = ref<StationLayoutDocument | null>(null)
const selectedRouteId = ref('')
const continuationId = ref('')
const loadingSchemes = ref(false)
const loadingData = ref(false)
const loadError = ref(false)
let schemeVersion = 0
let dataVersion = 0
const train = reactive(defaultTrainParameters())
const trainCatalog = ref<TrainPresetCatalog>(createDefaultTrainPresetCatalog())
const projectTrains = shallowRef<ProjectTrains | null>(null)
const trainSaveError = ref('')
const trainPresetLabel = (preset: TrainPreset) => locale.value.startsWith('zh') ? preset.name : preset.nameEn || preset.name
const signal = reactive(defaultSignalParameters())
const run = reactive<TractionRunOptions>(defaultRunOptions())
const metersPerUnit = ref(1)
const distanceMode = ref<'auto' | 'manual'>('auto')
const defaultLimit = ref(80)
const speedLimitPoints = ref<SpeedLimitPoint[]>([])
type SegmentField = 'speedLimitKmh' | 'gradientPermille' | 'additionalResistanceKn'
const overrides = reactive<Record<string, TractionSegmentOverride>>({})
const expanded = ref(['train', 'signal', 'line', 'stops'])
const trainParametersVisible = ref(false)
const lineParametersVisible = ref(false)
const timingDetailsVisible = ref(false)
const chartSplitRatio = ref(0.5)
const showOutsideStation = ref(true)
const saved = ref(false)
const workspaceReady = ref(false)
const workspaceLoading = ref(false)
const workspaceError = ref('')
const workspaceConflict = ref(false)
const savingWrites = ref(0)
const savingTrain = ref(false)
let workspaceSession: TractionWorkspaceSession | null = null
let workspaceEpoch = 0
let saveTimer: ReturnType<typeof setTimeout> | undefined
let pendingSave: { session: TractionWorkspaceSession; content: TractionWorkspaceContent } | null = null
let legacyOverrides: Record<string, Partial<Record<SegmentField, number>>> = {}
const migrationSources = new Map<TractionWorkspaceSession, string[]>()
const stopsCleared = ref(false)
const brakeMigration = ref<{ forceKn: number; variable: boolean } | null>(null)
let pendingSelection: { routeId: string; continuationId: string; run: TractionRunOptions } | null = null
const selectedRoute = computed(() => routes.value.find(item => item.id === selectedRouteId.value))
const continuations = computed(() => routes.value.filter(item => item.id !== selectedRouteId.value && item.startNodeID === selectedRoute.value?.endNodeID))
const routeLabel = (item: TractionStationRoute & { description?: string }) => item.description ? `${item.id} · ${item.description}` : item.id
const routeProfile = computed(() => {
    if (!document.value || !selectedRoute.value || loadingData.value) return null
    const next = continuations.value.find(item => item.id === continuationId.value)
    return buildRouteProfile(document.value, next ? [selectedRoute.value, next] : selectedRoute.value, distanceMode.value === 'manual' ? { metersPerUnit: metersPerUnit.value } : {})
})
const segmentKey = (id: string, index: number) => overrideKey({ routeId: selectedRouteId.value, routeStartNodeId: selectedRoute.value?.startNodeID || '', routeEndNodeId: selectedRoute.value?.endNodeID || '', continuationId: continuationId.value, segmentIndex: index, segmentId: id })
const baseProfile = computed<TractionRouteProfile | null>(() => {
    const raw = routeProfile.value
    if (!raw || raw.errors.length) return null
    return {
        lengthM: raw.lengthM, devices: raw.devices, joints: raw.joints, cells: raw.cells,
        segments: raw.segments.map((item, index) => ({
            startM: item.startM, endM: item.endM,
            speedLimitKmh: overrides[segmentKey(item.id, index)]?.speedLimitKmh ?? item.speedLimitKmh ?? defaultLimit.value,
            gradientPermille: overrides[segmentKey(item.id, index)]?.gradientPermille ?? item.gradientPermille,
            additionalResistanceKn: overrides[segmentKey(item.id, index)]?.additionalResistanceKn ?? 0,
        })),
    }
})
const effectiveSpeedLimitPoints = computed(() => (speedLimitPoints.value.length ? speedLimitPoints.value : pointsFromSegments(baseProfile.value?.segments ?? []))
    .filter(point => point.positionM < (baseProfile.value?.lengthM ?? 0)))
const profile = computed<TractionRouteProfile | null>(() => baseProfile.value ? { ...baseProfile.value,
    segments: applySpeedLimitPoints(baseProfile.value.segments, speedLimitPoints.value),
} : null)
function updateSpeedLimitPoints(points: SpeedLimitPoint[]) {
    if (!workspaceReady.value || workspaceConflict.value || !validateSpeedLimitPoints(points)) return
    speedLimitPoints.value = points.map(point => ({ ...point }))
}
function setLineSpeedLimit(startM: number, endM: number, value: number | undefined) {
    if (value === undefined || !profile.value) return
    updateSpeedLimitPoints(setIntervalLimit(effectiveSpeedLimitPoints.value, startM, endM, value, profile.value.lengthM))
}
const diagnostic = (code: string) => {
    const message = tractionMessages[code]
    return message ? ui(message[0], message[1]) : ui(`数据需要检查：${code}`, `Check data: ${code}`)
}
interface CalculationFailure { code: string; message: string; details: string[]; network: boolean; status?: number }
const calculation = shallowRef<StoredTractionCalculation | null>(null)
const result = computed(() => calculation.value?.result ?? null)
const reducedSpeedMessages = computed(() => (result.value?.warnings ?? [])
    .filter(code => code === 'initial-speed-reduced').map(diagnostic))
const resultProfile = computed(() => calculation.value?.request.profile ?? null)
const runSummary = computed(() => calculation.value ? summarizeTractionResult(calculation.value.result,
    calculation.value.request.profile, calculation.value.request.signal) : null)
const approachDistanceM = computed(() => runSummary.value?.brakingDistanceM ?? 0)
const occupancySummaryTitle = computed(() => {
    const summary = runSummary.value
    if (!summary) return undefined
    if (!summary.longestOccupancy) return ui('暂无可确定总占用时间的区段', 'No circuit has a known total occupancy time')
    const { cell, durationS } = summary.longestOccupancy
    return `${cell.label} · ${durationS.toFixed(1)} s` + (summary.unknownOccupancyCount
        ? ui('（仅统计时刻完整的区段）', ' (circuits with complete timing only)') : '')
})
const chartExtensionSpeeds = computed(() => extensionSpeedLimits(calculation.value?.request.train ?? train,
    calculation.value?.request.options ?? run))
const chartDepartureDistanceM = computed(() => calculation.value ? result.value?.departureSectionLengthM ?? 0
    : run.mode === 'departure' || run.mode === 'through' ? run.departureSectionLengthM ?? 0 : 0)
const chartProfile = computed(() => resultProfile.value && calculation.value
    ? withBrakingApproach(withDepartureSection(resultProfile.value, chartDepartureDistanceM.value, chartExtensionSpeeds.value.departureSectionMaxSpeedKmh),
        approachDistanceM.value, chartExtensionSpeeds.value.brakingMaxSpeedKmh)
    : profile.value ? withDepartureSection(profile.value, chartDepartureDistanceM.value, chartExtensionSpeeds.value.departureSectionMaxSpeedKmh) : null)
const chartLimitPoints = computed(() => {
    if (!canEditLimits.value || !profile.value) return undefined
    const points = effectiveSpeedLimitPoints.value.map(point => ({ ...point, positionM: point.positionM + approachDistanceM.value }))
    if (approachDistanceM.value > 0) points.unshift({ positionM: 0, speedLimitKmh: chartExtensionSpeeds.value.brakingMaxSpeedKmh })
    if (chartDepartureDistanceM.value > 0) points.push({ positionM: approachDistanceM.value + profile.value.lengthM,
        speedLimitKmh: chartExtensionSpeeds.value.departureSectionMaxSpeedKmh })
    return points
})
function updateChartLimitPoints(points: SpeedLimitPoint[]) {
    const offset = approachDistanceM.value
    updateSpeedLimitPoints(points.filter(point => point.positionM >= offset && point.positionM < offset + (profile.value?.lengthM ?? 0))
        .map(point => ({ ...point, positionM: point.positionM - offset })))
}
const currentRequest = computed(() => profile.value ? buildTractionApiRequest(profile.value, train, signal, run) : null)
const resultMatchesInputs = computed(() => !!calculation.value && !!currentRequest.value
    && calculation.value.selectedRouteId === selectedRouteId.value && calculation.value.continuationId === continuationId.value
    && JSON.stringify(calculation.value.request) === JSON.stringify(currentRequest.value))
const resultRouteLabel = computed(() => [calculation.value?.selectedRouteId, calculation.value?.continuationId].filter(Boolean)
    .map(id => { const route = routes.value.find(item => item.id === id); return route ? routeLabel(route) : id }).join(' → '))
// Preview and save consume the same conversion, including rounding and skipped cells.
const occupancyPreview = computed(() => {
    if (!result.value || !routeProfile.value || !selectedRoute.value) return { updates: [], error: '' }
    const currentRoutes = [selectedRoute.value, continuations.value.find(route => route.id === continuationId.value)]
        .filter((route): route is NonNullable<typeof route> => !!route)
    try { return { updates: buildTractionOccupancyUpdates(currentRoutes, routeProfile.value, result.value), error: '' } }
    catch (reason) { return { updates: [], error: reason instanceof Error ? reason.message : String(reason) } }
})
const occupancyOffsetColumns = [
    { key: 'startOccupationShift', zh: '相对开始占用时间 (s)', en: 'Relative occupancy start (s)' },
    { key: 'endOccupationShift', zh: '相对结束占用时间 (s)', en: 'Relative occupancy end (s)' },
] as const
const occupancyDetailRows = computed(() => (result.value?.cellOccupancies ?? []).map(cell => {
    const sourceID = /^(.*)@\d+$/.exec(cell.id)?.[1]
    const offsets = occupancyPreview.value.updates.flatMap(update => {
        const time = update.times.find(item => item.cellID === sourceID)
        if (!time && (!sourceID || !update.skippedCellIDs.includes(sourceID))) return []
        return [{ routeID: update.routeID, startOccupationShift: time?.startOccupationShift ?? null,
            endOccupationShift: time?.endOccupationShift ?? null }]
    })
    return { ...cell, offsets, stoppedAt: cellTerminalStopTime(result.value, cell), occupancyEndTimeS: cellOccupancyEndTime(result.value, cell) }
}))
const occupancyUnavailableTitle = computed(() => resultMatchesInputs.value
    ? ui('无法确定占用参数，更新时保留原值。', 'Occupancy parameters cannot be determined; existing values will be retained.')
    : ui('当前参数与已保存结果不一致，请先计算。', 'Current inputs differ from the saved result. Calculate before updating.'))
const calculationLoadError = ref(false)
let workspaceDataVersion = 0
const canCalculate = computed(() => workspaceReady.value && !!profile.value && !loadError.value && !workspaceConflict.value
    && !batchBusy.value && !setBusy.value && !routeBusy.value && !switchingSet.value && !workspaceLoading.value && !loadingData.value && !loadingSchemes.value && !savingTrain.value && !updatingOccupancy.value && props.active !== false)
const canEditLimits = computed(() => canCalculate.value && (resultProfile.value ?? profile.value)?.lengthM === profile.value?.lengthM)
const calculationPending = ref(false)
const calculationError = shallowRef<CalculationFailure | null>(null)
let calculationVersion = 0
let calculationController: AbortController | undefined
let disposed = false
const errorTitle = computed(() => {
    const error = calculationError.value
    if (error?.network) return ui('无法连接计算服务', 'Calculation service unavailable')
    if (error?.status === 401 || error?.status === 403) return ui('无权调用计算服务', 'Calculation access denied')
    if (error?.code === 'infeasible-profile') return ui('当前参数无法形成可行运行曲线', 'No feasible run for these parameters')
    return ui('计算未完成', 'Calculation could not be completed')
})
const errorMessages = computed(() => {
    const error = calculationError.value
    if (!error) return []
    return [...new Set([diagnostic(error.code), error.message, ...error.details].filter(Boolean))]
})
function cancelCalculation(clearResult = false) {
    calculationVersion++
    calculationController?.abort()
    calculationController = undefined
    if (clearResult) {
        calculation.value = null
        calculationLoadError.value = false
        timingDetailsVisible.value = false
    }
    calculationError.value = null
    calculationPending.value = false
}
// Single-route calculation is explicit; the results dialog has a separate explicit batch action.
async function calculate() {
    if (disposed || !canCalculate.value || calculationPending.value || !currentRequest.value || !workspaceSession) return
    cancelCalculation()
    calculationPending.value = true
    const version = calculationVersion
    const session = workspaceSession
    const request = currentRequest.value
    const controller = new AbortController()
    calculationController = controller
    try {
        flushWorkspaceSave()
        const revision = await persistWorkspace(captureWorkspace(), session)
        if (version !== calculationVersion || session !== workspaceSession || controller.signal.aborted) return
        const response = await calculateStoredTraction(session.scope, revision, request, controller.signal)
        if (version === calculationVersion && !controller.signal.aborted && !disposed) {
            calculation.value = response
            calculationLoadError.value = false
        }
    } catch (error) {
        if (version !== calculationVersion || controller.signal.aborted || disposed) return
        const failure = error as { code?: string; message?: string; response?: { status?: number; data?: { code?: string; message?: string; errors?: unknown } } }
        const payload = failure.response?.data
        const errors = payload?.errors
        const details: string[] = []
        if (Array.isArray(errors)) details.push(...errors.map(item => typeof item === 'string' ? item : JSON.stringify(item)))
        else if (errors && typeof errors === 'object') {
            for (const [field, messages] of Object.entries(errors)) {
                const rows = Array.isArray(messages) ? messages : [messages]
                details.push(...rows.map(message => `${field}: ${typeof message === 'string' ? message : JSON.stringify(message)}`))
            }
        }
        const network = !failure.response && failure.code !== 'invalid-response'
        calculationError.value = {
            code: payload?.code || (failure.code === 'invalid-response' ? 'invalid-response' : network ? 'calculation-network-error' : failure.response?.status === 400 ? 'invalid-input' : 'calculation-service-error'),
            message: payload?.message || failure.message || '', details, network, status: failure.response?.status,
        }
    } finally {
        if (version === calculationVersion && !disposed) {
            calculationPending.value = false
            calculationController = undefined
        }
    }
}
const lineParameterRows = computed(() => applySpeedLimitPoints((routeProfile.value?.segments ?? []).map((segment, index) => ({
    ...segment, index,
    speedLimitKmh: overrides[segmentKey(segment.id, index)]?.speedLimitKmh ?? segment.speedLimitKmh ?? defaultLimit.value,
    gradientPermille: overrides[segmentKey(segment.id, index)]?.gradientPermille ?? segment.gradientPermille,
    additionalResistanceKn: overrides[segmentKey(segment.id, index)]?.additionalResistanceKn ?? 0,
})), speedLimitPoints.value))
const selectedRouteLabel = computed(() => [selectedRoute.value, continuations.value.find(item => item.id === continuationId.value)]
    .filter((item): item is NonNullable<typeof item> => Boolean(item)).map(routeLabel).join(' → '))
function setSegment(id: string, index: number, field: SegmentField, value: number | undefined) {
    if (value === undefined || !Number.isFinite(value)) return
    const key = segmentKey(id, index)
    overrides[key] = { routeId: selectedRouteId.value, routeStartNodeId: selectedRoute.value!.startNodeID,
        routeEndNodeId: selectedRoute.value!.endNodeID, continuationId: continuationId.value, segmentIndex: index, segmentId: id,
        ...overrides[key], [field]: value }
}
function setMode(mode: TractionRunOptions['mode']) {
    run.mode = mode
    const defaults = defaultRunOptions(mode)
    run.initialSpeedKmh = defaults.initialSpeedKmh
    run.finalSpeedKmh = defaults.finalSpeedKmh
}
function addStop() {
    if (!profile.value || run.stops.length >= 100) return
    run.stops.push({ positionM: Math.round(profile.value.lengthM / 2 * 100) / 100, dwellS: 30 })
    stopsCleared.value = false
}
const storageKey = (version = 2) => `switchyard.traction.v${version}:${props.selectedInstanceId}:${schemeId.value}`
const trainStorageKey = () => `switchyard.traction.trains.v1:${props.selectedInstanceId}:${schemeId.value}`
function applyTrainCatalog(catalog: TrainPresetCatalog) {
    const next = cloneTrainPresetCatalog(catalog)
    trainCatalog.value = next
    Object.assign(train, cloneTrainParameters(next.presets.find(item => item.id === next.selectedId)!.parameters))
    brakeMigration.value = null
}
function captureWorkspace(catalog = trainCatalog.value): TractionWorkspaceContent {
    return { catalogRevision: projectTrains.value?.revision ?? 0, speedLimitPoints: speedLimitPoints.value.map(point => ({ ...point })), catalog: cloneTrainPresetCatalog(catalog), settings: { selectedRouteId: selectedRouteId.value, continuationId: continuationId.value,
        signal: { ...signal }, run: { ...run, stops: run.stops.map(stop => ({ ...stop })) },
        distanceMode: distanceMode.value, metersPerUnit: metersPerUnit.value, defaultLimit: defaultLimit.value },
        overrides: Object.values(overrides).map(row => ({ ...row })) }
}
function applyWorkspace(workspace: TractionWorkspace) {
    if (workspace.catalogRevision && workspace.catalogRevision > (projectTrains.value?.revision ?? 0)) projectTrains.value = {
        instanceID: props.selectedInstanceId, revision: workspace.catalogRevision, catalog: cloneTrainPresetCatalog(workspace.catalog),
    }
    speedLimitPoints.value = (workspace.speedLimitPoints ?? []).map(point => ({ ...point }))
    applyTrainCatalog(workspace.catalog)
    Object.assign(signal, workspace.settings.signal)
    Object.assign(run, workspace.settings.run, { stops: workspace.settings.run.stops.map(stop => ({ ...stop })) })
    distanceMode.value = workspace.settings.distanceMode
    metersPerUnit.value = workspace.settings.metersPerUnit
    defaultLimit.value = workspace.settings.defaultLimit
    Object.keys(overrides).forEach(key => delete overrides[key])
    for (const row of workspace.overrides) overrides[overrideKey(row)] = { ...row }
    selectedRouteId.value = workspace.settings.selectedRouteId
    continuationId.value = workspace.settings.continuationId
    pendingSelection = null
}
function reportWorkspaceError(error: unknown) {
    const failure = error as { message?: string; response?: { status?: number; data?: { message?: string } } }
    workspaceConflict.value = failure.response?.status === 409
    workspaceError.value = workspaceConflict.value
        ? ui('数据库参数已被其他页面修改，请重新载入后再编辑。', 'Database parameters changed in another page. Reload before editing.')
        : ui('数据库读写失败，当前修改尚未保存。', 'Database access failed. Current changes have not been saved.') + ' ' + (failure.response?.data?.message || failure.message || '')
}
async function persistWorkspace(content: TractionWorkspaceContent, session = workspaceSession): Promise<number> {
    if (!session) throw new Error('Traction workspace is not loaded.')
    savingWrites.value++
    try {
        const revision = await session.save(content)
        const sources = migrationSources.get(session)
        if (sources) {
            try { for (const key of sources) localStorage.removeItem(key) } catch { /* Database is authoritative even if old browser data cannot be removed. */ }
            migrationSources.delete(session)
        }
        if (session === workspaceSession) {
            workspaceError.value = ''
            workspaceConflict.value = false
            saved.value = workspaceFingerprint(captureWorkspace()) === session.fingerprint
        }
        return revision
    } catch (error) {
        if (session === workspaceSession) reportWorkspaceError(error)
        else ElMessage.error(ui('上一站场的牵引参数未能保存，请返回重试。', 'Traction parameters for the previous scheme could not be saved. Return to retry.'))
        throw error
    } finally { savingWrites.value-- }
}
function flushWorkspaceSave() {
    if (saveTimer !== undefined) clearTimeout(saveTimer)
    saveTimer = undefined
    const pending = pendingSave
    pendingSave = null
    if (pending) void persistWorkspace(pending.content, pending.session).catch(() => undefined)
}
function scheduleWorkspaceSave() {
    if (!workspaceReady.value || !workspaceSession || workspaceConflict.value || workspaceLoading.value) return
    if (saveTimer !== undefined) clearTimeout(saveTimer)
    pendingSave = { session: workspaceSession, content: captureWorkspace() }
    saveTimer = setTimeout(flushWorkspaceSave, 450)
}
async function persistTrainCatalog(catalog: TrainPresetCatalog) {
    if (!workspaceReady.value || !workspaceSession || validateTrainPresetCatalog(catalog) || savingTrain.value) return false
    const session = workspaceSession
    savingTrain.value = true
    try {
        flushWorkspaceSave()
        await persistWorkspace(captureWorkspace(catalog), session)
        if (session !== workspaceSession) return false
        applyTrainCatalog(catalog)
        return true
    } catch { return false }
    finally { savingTrain.value = false }
}
async function selectTrainPreset(id: string) {
    if (id === trainCatalog.value.selectedId) return true
    if (!trainCatalog.value.presets.some(item => item.id === id)) return false
    return await persistTrainCatalog({ ...trainCatalog.value, selectedId: id })
}
async function saveTrainCatalog(catalog: TrainPresetCatalog) {
    trainSaveError.value = ''
    const key = batchScopeKey.value
    try {
        await saveProjectTrainLibrary(catalog)
        if (disposed || batchScopeKey.value !== key) return
        if (!await selectTrainPreset(catalog.selectedId)) {
            trainSaveError.value = ui('项目列车参数已保存，当前进路的列车选择未保存，请重新载入后重试。', 'Project trains were saved, but the route selection was not saved. Reload and retry.')
            return
        }
        trainParametersVisible.value = false
        ElMessage.success(ui('项目列车参数已保存，各车站和进路共用。', 'Project train parameters saved and shared by all stations and routes.'))
    } catch (error) {
        if ((error as { code?: string }).code === 'project-trains-saved-refresh-failed') workspaceError.value = (error as Error).message
        else reportWorkspaceError(error)
        trainSaveError.value = workspaceError.value
    }
}
async function saveProjectTrainLibrary(catalog: TrainPresetCatalog): Promise<ProjectTrains> {
    if (!projectTrains.value || savingTrain.value || batchBusy.value || updatingOccupancy.value) throw new Error(ui('请等待当前操作完成。', 'Wait for the current operation to finish.'))
    const scope = stationScope(), key = batchScopeKey.value, selected = selectedRouteId.value, continuation = continuationId.value
    const catalogRevision = projectTrains.value.revision
    let committed = false
    savingTrain.value = true; routeBusy.value = true
    try {
        await flushCurrentWorkspace()
        if (disposed || batchScopeKey.value !== key) throw new Error(ui('项目或方案已改变，请重新打开参数。', 'The project or set changed. Reopen parameters.'))
        const savedProject = await saveProjectTrains(scope, catalogRevision, catalog)
        committed = true
        if (disposed || batchScopeKey.value !== key) return savedProject
        projectTrains.value = savedProject
        workspaceReady.value = false
        applyTrainCatalog(useProjectTrains(captureWorkspace(), savedProject).catalog)
        const [rows, sets] = await Promise.all([loadTractionRoutes(setScope()), loadTractionSets(scope)])
        if (disposed || batchScopeKey.value !== key) return savedProject
        routeCases.value = rows; calculationSets.value = sets.sets
        if (selected) await activateRoute(selected, continuation, undefined, true)
        else applyTrainCatalog(savedProject.catalog)
        return savedProject
    } catch (error) {
        if (committed) throw Object.assign(new Error(ui('项目列车参数已保存，但工作区刷新失败，请重新载入。', 'Project train parameters were saved, but the workspace could not refresh. Reload it.')), { code: 'project-trains-saved-refresh-failed' })
        throw error
    } finally { savingTrain.value = false; routeBusy.value = false }
}
function restoreLegacyTrainCatalog(hasLegacyTrain: boolean) {
    const defaults = createDefaultTrainPresetCatalog()
    try {
        const stored = selectedSetId.value === 'default' ? parseTrainPresetCatalog(localStorage.getItem(trainStorageKey())) : null
        if (stored) { applyTrainCatalog(stored); return }
        if (hasLegacyTrain) {
            const previous = cloneTrainParameters(train)
            const matching = defaults.presets.find(item => JSON.stringify(cloneTrainParameters(item.parameters)) === JSON.stringify(previous))
            if (matching) defaults.selectedId = matching.id
            else {
                const migrated: TrainPresetCatalog = { version: 1, selectedId: 'previous-train', presets: [...defaults.presets,
                    { id: 'previous-train', name: '原有列车参数', nameEn: 'Previous train parameters', parameters: previous }] }
                if (!validateTrainPresetCatalog(migrated)) { defaults.presets = migrated.presets; defaults.selectedId = migrated.selectedId }
                else ElMessage.warning(ui('原有列车参数无效，已恢复示例预设。', 'Previous train parameters were invalid; example presets were restored.'))
            }
        }
    } catch { ElMessage.warning(ui('无法读取旧浏览器列车参数。', 'Old browser train parameters could not be read.')) }
    applyTrainCatalog(defaults)
}
function importLegacyOverrides() {
    for (const [key, values] of Object.entries(legacyOverrides)) {
        let imported = false
        for (const route of routes.value) {
            for (const continuation of ['', ...routes.value.filter(item => item.id !== route.id && item.startNodeID === route.endNodeID).map(item => item.id)]) {
                const prefix = `${route.id}:${route.startNodeID}>${route.endNodeID}:${continuation}:`
                if (!key.startsWith(prefix)) continue
                const rest = key.slice(prefix.length), separator = rest.indexOf(':')
                if (separator < 1) continue
                const segmentIndex = Number(rest.slice(0, separator)), segmentId = rest.slice(separator + 1)
                if (!Number.isSafeInteger(segmentIndex) || segmentIndex < 0 || !segmentId) continue
                const row: TractionSegmentOverride = { routeId: route.id, routeStartNodeId: route.startNodeID, routeEndNodeId: route.endNodeID,
                    continuationId: continuation, segmentIndex, segmentId, ...values }
                overrides[overrideKey(row)] = row
                imported = true
                break
            }
            if (imported) break
        }
        if (!imported) ElMessage.warning(ui('部分旧线路参数对应的进路已不存在，请重新核对线路参数。', 'Some old line parameters reference unavailable routes. Review the line parameters.'))
    }
    legacyOverrides = {}
}
async function saveParameters() {
    if (!workspaceReady.value) return
    flushWorkspaceSave()
    try {
        await persistWorkspace(captureWorkspace())
        ElMessage.success(ui('牵引参数已保存到数据库。', 'Traction parameters saved to the database.'))
    } catch { /* Keep the current inputs available for retry. */ }
}
// Opening a route only reads its saved snapshot; calculation is always explicit.
async function activateRoute(routeId: string, continuation: string, seed?: TractionWorkspaceContent, preserveTrainEditor = false) {
    const scope = setScope(), epoch = ++workspaceEpoch
    const record = await openTractionRoute(scope, routeId, continuation)
    const target = { ...scope, routeCaseID: record.id }
    const [workspace, snapshot] = await Promise.all([loadTractionWorkspace(target), loadTractionCalculation(target)])
    const library = workspace ? null : await loadProjectTrains(scope)
    if (epoch !== workspaceEpoch || scope.instanceID !== props.selectedInstanceId || scope.stationSchemeID !== schemeId.value || scope.calculationSetID !== selectedSetId.value || disposed) return
    workspaceReady.value = false
    if (library) projectTrains.value = library
    cancelCalculation(true)
    selectedCaseId.value = record.id
    workspaceSession = new TractionWorkspaceSession(target, workspace)
    workspaceError.value = ''; workspaceConflict.value = false
    if (!preserveTrainEditor) trainParametersVisible.value = false
    lineParametersVisible.value = false
    if (workspace) applyWorkspace(workspace)
    else {
        if (seed) applyWorkspace({ ...seed, revision: 0 })
        else {
            restoreLegacyParameters()
            if (selectedSetId.value === 'default') migrationSources.set(workspaceSession, [trainStorageKey(), storageKey(), storageKey(1)])
        }
        const legacyRun = pendingSelection?.routeId === routeId && pendingSelection?.continuationId === continuation ? pendingSelection.run : null
        speedLimitPoints.value = []
        selectedRouteId.value = routeId; continuationId.value = continuation
        const type = selectedRoute.value?.type.trim().toLowerCase() || ''
        setMode(tractionModeForRoute(type, !!continuation))
        run.stops = []
        if (legacyRun) Object.assign(run, legacyRun)
        if (projectTrains.value) applyTrainCatalog(useProjectTrains(captureWorkspace(), projectTrains.value).catalog)
        pendingSelection = null
        importLegacyOverrides()
    }
    stopsCleared.value = false
    calculation.value = snapshot
    calculationLoadError.value = false
    const existing = routeCases.value.findIndex(row => row.id === record.id)
    routeCases.value = routeCases.value.map(row => ({ ...row, isSelected: row.id === record.id }))
    if (existing < 0) routeCases.value.push(record)
    else routeCases.value[existing] = record
    await nextTick()
    if (epoch !== workspaceEpoch || disposed) return
    workspaceReady.value = true
    saved.value = workspaceFingerprint(captureWorkspace()) === workspaceSession.fingerprint
    if (!workspace && !loadError.value) await persistWorkspace(captureWorkspace())
}
async function switchRoute(routeId: string, continuation = '') {
    if (!routeId || routeBusy.value || switchingSet.value || (routeId === selectedRouteId.value && continuation === continuationId.value && selectedCaseId.value)) return
    routeBusy.value = true; workspaceLoading.value = true; routeError.value = ''
    cancelCalculation()
    const key = scopeKey()
    try {
        await flushCurrentWorkspace()
        if (key !== scopeKey() || disposed) return
        const seed = workspaceReady.value ? captureWorkspace() : undefined
        await activateRoute(routeId, continuation, seed)
        routeResultsVisible.value = false
    } catch (error) { reportWorkspaceError(error); if (routeResultsVisible.value) routeError.value = setFailure(error) }
    finally { routeBusy.value = false; workspaceLoading.value = false }
}
async function loadWorkspaceData() {
    const version = ++workspaceDataVersion, station = stationScope()
    if (!station.instanceID || !station.stationSchemeID) return
    workspaceLoading.value = true
    cancelCalculation()
    try {
        if (workspaceReady.value && workspaceSession?.scope.instanceID === station.instanceID && workspaceSession.scope.stationSchemeID === station.stationSchemeID && workspaceSession.scope.calculationSetID === selectedSetId.value) await flushCurrentWorkspace()
        if (version !== workspaceDataVersion || disposed) return
        workspaceReady.value = false; cancelCalculation(true)
        const [catalog] = await Promise.all([loadTractionSets(station), loadData()])
        if (version !== workspaceDataVersion || station.instanceID !== props.selectedInstanceId || station.stationSchemeID !== schemeId.value || disposed) return
        calculationSets.value = catalog.sets
        if (!catalog.sets.some(item => item.id === selectedSetId.value)) selectedSetId.value = catalog.selectedId
        let library = await loadProjectTrains(station)
        if (version !== workspaceDataVersion || disposed) return
        if (!library) {
            restoreLegacyParameters()
            try { library = await saveProjectTrains(station, 0, trainCatalog.value) }
            catch (error) {
                if ((error as { response?: { status?: number } }).response?.status !== 409) throw error
                library = await loadProjectTrains(station)
                if (!library) throw error
            }
        }
        if (version !== workspaceDataVersion || disposed) return
        projectTrains.value = library
        applyTrainCatalog(library.catalog)
        const list = await loadTractionRoutes(setScope())
        if (version !== workspaceDataVersion || disposed) return
        routeCases.value = list
        const selected = list.find(row => row.isSelected && row.routeId) || list.find(row => row.id === selectedCaseId.value && row.routeId) || list.find(row => row.routeId)
        const routeId = selected?.routeId || routes.value[0]?.id
        if (routeId) {
            const legacyDraft = !selected && list[0] ? await loadTractionWorkspace({ ...setScope(), routeCaseID: list[0].id }) : null
            if (version !== workspaceDataVersion || disposed) return
            await activateRoute(routeId, selected?.continuationId || '', legacyDraft ?? undefined)
        }
        else { workspaceSession = null; selectedCaseId.value = ''; selectedRouteId.value = ''; continuationId.value = '' }
    } catch (error) { if (version === workspaceDataVersion) reportWorkspaceError(error) }
    finally { if (version === workspaceDataVersion) workspaceLoading.value = false }
}
async function openRouteResults() {
    routeResultsVisible.value = true; routeBusy.value = true; routeError.value = ''
    const key = scopeKey(), scope = setScope()
    cancelCalculation()
    try {
        await flushCurrentWorkspace()
        const rows = await loadTractionRoutes(scope)
        if (key === scopeKey() && !disposed) routeCases.value = rows
    } catch (error) { if (key === scopeKey()) routeError.value = setFailure(error) }
    finally { routeBusy.value = false }
}
async function prepareBatch(): Promise<TractionBatchContext> {
    if (!document.value || !routes.value.length || !selectedSetId.value || workspaceConflict.value || routeBusy.value || setBusy.value
        || switchingSet.value || workspaceLoading.value || calculationPending.value || batchBusy.value || props.active === false) throw new Error(ui('请等待站场和参数载入完成。', 'Wait for station data and parameters to load.'))
    routeBusy.value = true
    const key = scopeKey()
    try {
        await flushCurrentWorkspace()
        if (key !== scopeKey() || disposed || !document.value) throw new Error(ui('当前方案已改变，请重新打开批量计算。', 'The current set changed. Reopen batch calculation.'))
        const batchKey = batchScopeKey.value
        return { scope: { ...setScope() }, document: JSON.parse(JSON.stringify(document.value)), routes: routes.value.map(route => ({ ...route })),
            cases: routeCases.value.map(row => ({ ...row })), seed: captureWorkspace(), saveTrains: catalog => {
                if (batchScopeKey.value !== batchKey) throw new Error(ui('项目或方案已改变，请重新打开参数。', 'The project or set changed. Reopen parameters.'))
                return saveProjectTrainLibrary(catalog)
            } }
    } finally { routeBusy.value = false }
}
async function completeBatch(report: TractionBatchReport) {
    const sameScope = () => !disposed && props.active !== false && report.scope.instanceID === props.selectedInstanceId
        && report.scope.stationSchemeID === schemeId.value && report.scope.calculationSetID === selectedSetId.value
    if (!sameScope()) return
    routeBusy.value = true; routeError.value = ''
    try {
        const [rows, catalog] = await Promise.all([loadTractionRoutes(report.scope), loadTractionSets(stationScope())])
        if (!sameScope()) return
        routeCases.value = rows; calculationSets.value = catalog.sets
        if (selectedRouteId.value && report.jobs.some(job => job.caseId === selectedCaseId.value)) {
            await activateRoute(selectedRouteId.value, continuationId.value)
        }
    } catch (error) { if (sameScope()) routeError.value = setFailure(error) }
    finally { routeBusy.value = false }
}
async function transferRouteResults(selection: TractionRouteCase[]): Promise<TractionParameterTransferReport> {
    if (updatingOccupancy.value || batchBusy.value || calculationPending.value || loadingParameterSets.value || parameterSetError.value || !parameterSetId.value)
        throw new Error(ui('请等待载入完成并选择计算参数集。', 'Wait for loading and select a calculation parameter set.'))
    const destination = parameterSetId.value
    const context = await prepareBatch()
    const target = { instanceID: context.scope.instanceID, stationSchemeID: context.scope.stationSchemeID, calculationParameterSetID: destination, trainTypeID: '' as const }
    const report: TractionParameterTransferReport = { completedRouteIDs: [], updatedCellCount: 0, skippedCellCount: 0, operationTimeOnlyRouteIDs: [], error: '' }
    const scopeIsCurrent = () => !disposed && props.active !== false && props.selectedInstanceId === target.instanceID
        && schemeId.value === target.stationSchemeID && selectedSetId.value === context.scope.calculationSetID && parameterSetId.value === destination
    updatingOccupancy.value = true; routeBusy.value = true
    try {
        const updates = await prepareTractionParameterTransfers(context, selection)
        report.skippedCellCount = updates.reduce((count, update) => count + update.skippedCellIDs.length, 0)
        await writeTractionParameterTransfers(target, updates, scopeIsCurrent, update => {
            report.completedRouteIDs.push(update.routeID); report.updatedCellCount += update.times.length
            if (!update.times.length) report.operationTimeOnlyRouteIDs.push(update.routeID)
        })
    } catch (reason) {
        const failure = reason as { message?: string; response?: { data?: unknown } }
        report.error = typeof failure.response?.data === 'string' ? failure.response.data : failure.message || ui('带入计算参数失败。', 'Could not apply calculation parameters.')
    } finally {
        if (report.completedRouteIDs.length) capacityStore.notifyRouteOccupancyUpdated({ ...target, routeIDs: report.completedRouteIDs })
        updatingOccupancy.value = false; routeBusy.value = false
    }
    return report
}
async function removeRouteResult(row: TractionRouteCase) {
    if (routeBusy.value) return
    routeBusy.value = true; routeError.value = ''
    const key = scopeKey(), scope = setScope()
    try {
        await deleteTractionRoute(scope, row)
        if (key !== scopeKey() || disposed) return
        if (row.id === selectedCaseId.value) {
            workspaceReady.value = false; workspaceSession = null; selectedCaseId.value = ''
            selectedRouteId.value = ''; continuationId.value = ''; cancelCalculation(true)
        }
        const rows = await loadTractionRoutes(scope)
        if (scope.instanceID === props.selectedInstanceId && scope.stationSchemeID === schemeId.value && scope.calculationSetID === selectedSetId.value && !disposed) routeCases.value = rows
    } catch (error) { routeError.value = setFailure(error) }
    finally { routeBusy.value = false }
}
async function flushCurrentWorkspace() {
    if (!workspaceReady.value || !workspaceSession) return
    flushWorkspaceSave()
    await persistWorkspace(captureWorkspace())
}
async function switchCalculationSet(id: string, persistSelection = true) {
    if (id === selectedSetId.value || switchingSet.value) return
    const station = stationScope()
    switchingSet.value = true
    cancelCalculation()
    try {
        await flushCurrentWorkspace()
        if (persistSelection) await selectTractionSet({ ...station, calculationSetID: id })
        if (station.instanceID !== props.selectedInstanceId || station.stationSchemeID !== schemeId.value || disposed) return
        workspaceReady.value = false
        workspaceEpoch++; workspaceDataVersion++
        selectedSetId.value = id
        selectedCaseId.value = ''; routeCases.value = []; selectedRouteId.value = ''; continuationId.value = ''
        cancelCalculation(true)
        await loadWorkspaceData()
    } catch (error) { reportWorkspaceError(error) }
    finally { switchingSet.value = false }
}
async function openSetManager() {
    setsVisible.value = true; setBusy.value = true; setError.value = ''
    const key = scopeKey(), station = stationScope()
    cancelCalculation()
    try {
        await flushCurrentWorkspace()
        const catalog = await loadTractionSets(station)
        if (key === scopeKey() && !disposed) calculationSets.value = catalog.sets
    } catch (error) { if (key === scopeKey()) setError.value = setFailure(error) }
    finally { setBusy.value = false }
}
function setFailure(error: unknown) {
    const failure = error as { message?: string; response?: { status?: number; data?: { message?: string } } }
    return failure.response?.status === 409 ? ui('方案已被其他页面更新，请刷新列表后重试。', 'This set changed in another page. Refresh the list and retry.')
        : failure.response?.data?.message || failure.message || ui('操作失败，请重试。', 'Operation failed. Please retry.')
}
async function applySetAction(action: TractionSetAction) {
    if (setBusy.value || switchingSet.value) return
    setBusy.value = true; setError.value = ''
    const station = stationScope(), key = scopeKey()
    try {
        const created = await manageTractionSet(station, action)
        if (key === scopeKey() && action.kind === 'delete' && action.id === selectedSetId.value) {
            workspaceReady.value = false; workspaceSession = null
            cancelCalculation(true)
        }
        const catalog = await loadTractionSets(station)
        if (key !== scopeKey() || disposed) return
        calculationSets.value = catalog.sets
        if (action.kind === 'create' || (action.kind === 'delete' && action.id === selectedSetId.value)) {
            setsVisible.value = false
            // Deleted scopes cannot be saved again while switching to their replacement.
            if (action.kind === 'delete') { workspaceReady.value = false; workspaceSession = null }
            await switchCalculationSet(action.kind === 'create' ? created!.id : catalog.selectedId, false)
        }
    } catch (error) { if (key === scopeKey()) setError.value = setFailure(error) }
    finally { setBusy.value = false }
}
function reloadWorkspace() {
    if (saveTimer !== undefined) clearTimeout(saveTimer)
    saveTimer = undefined; pendingSave = null
    workspaceReady.value = false
    void loadSchemes()
}
function restoreLegacyParameters() {
    trainParametersVisible.value = false
    let hasLegacyTrain = false
    pendingSelection = null
    Object.assign(train, defaultTrainParameters())
    Object.assign(signal, defaultSignalParameters())
    Object.assign(run, defaultRunOptions())
    brakeMigration.value = null
    stopsCleared.value = false
    metersPerUnit.value = 1
    distanceMode.value = 'auto'
    defaultLimit.value = 80
    speedLimitPoints.value = []
    Object.keys(overrides).forEach(key => delete overrides[key])
    saved.value = false
    try {
        const raw = selectedSetId.value === 'default' ? localStorage.getItem(storageKey()) ?? localStorage.getItem(storageKey(1)) : null
        if (!raw) return
        const value = JSON.parse(raw)
        hasLegacyTrain = Boolean(value.train && typeof value.train === 'object')
        // The backend validates restored parameters before returning any result.
        const copyNumbers = (target: object, candidate: unknown) => {
            if (!candidate || typeof candidate !== 'object') return
            const fields = target as Record<string, unknown>
            const input = candidate as Record<string, unknown>
            for (const key of Object.keys(fields)) if (typeof fields[key] === 'number' && typeof input[key] === 'number' && Number.isFinite(input[key])) fields[key] = input[key]
        }
        copyNumbers(train, value.train)
        copyNumbers(signal, value.signal)
        if (['arrival', 'departure', 'through', 'shunting'].includes(value.run?.mode)) setMode(value.run.mode)
        copyNumbers(run, value.run)
        const curve = value.train?.tractionCurve
        if (Array.isArray(curve) && curve.length >= 1 && curve.length <= 128 && curve.every(p => p && Number.isFinite(p.speedKmh) && Number.isFinite(p.forceKn))) {
            train.tractionCurve = curve.map(p => ({ speedKmh: p.speedKmh, forceKn: p.forceKn }))
        }
        const braking = value.train?.brakingCurve
        if (!Number.isFinite(value.train?.maxBrakeForceKn) && Array.isArray(braking) && braking.length && braking.every(p => p && Number.isFinite(p.forceKn))) {
            train.maxBrakeForceKn = Math.min(...braking.map(p => p.forceKn))
            brakeMigration.value = { forceKn: train.maxBrakeForceKn, variable: braking.some(p => p.forceKn !== braking[0].forceKn) }
        } else if (value.brakeMigration && Number.isFinite(value.brakeMigration.forceKn)) {
            brakeMigration.value = { forceKn: value.brakeMigration.forceKn, variable: value.brakeMigration.variable === true }
        }
        for (const key of ['brakingCurve', 'resistanceCurve'] as const) {
            const points = value.train?.[key]
            train[key] = Array.isArray(points) && points.length > 0 && points.length <= 128
                ? points.map(p => ({ speedKmh: p.speedKmh, forceKn: p.forceKn }))
                : key === 'brakingCurve' ? generateBrakingCurve(train) : generateResistanceCurve(train)
        }
        if (run.mode === 'departure') run.initialSpeedKmh = 0
        if (run.mode === 'arrival') run.finalSpeedKmh = 0
        if (Array.isArray(value.run?.stops) && value.run.stops.length <= 100 && value.run.stops.every((stop: { positionM?: number; dwellS?: number } | null) => stop && Number.isFinite(stop.positionM) && Number.isFinite(stop.dwellS))) {
            run.stops = value.run.stops.map((stop: { positionM: number; dwellS: number }) => ({ positionM: stop.positionM, dwellS: stop.dwellS }))
        }
        if (value.distanceMode === 'manual') distanceMode.value = 'manual'
        if (Number.isFinite(value.metersPerUnit)) metersPerUnit.value = value.metersPerUnit
        if (Number.isFinite(value.defaultLimit)) defaultLimit.value = value.defaultLimit
        if (value.overrides && typeof value.overrides === 'object') {
            for (const [key, candidate] of Object.entries(value.overrides)) {
                if (!candidate || typeof candidate !== 'object' || ['__proto__', 'constructor', 'prototype'].includes(key)) continue
                const row = candidate as Record<string, unknown>
                legacyOverrides[key] = {}
                for (const field of ['speedLimitKmh', 'gradientPermille', 'additionalResistanceKn'] as const) {
                    if (typeof row[field] === 'number' && Number.isFinite(row[field])) legacyOverrides[key]![field] = row[field]
                }
            }
        }
        pendingSelection = { routeId: String(value.selectedRouteId ?? ''), continuationId: String(value.continuationId ?? ''), run: { ...run, stops: run.stops.map(stop => ({ ...stop })) } }
        saved.value = true
    } catch { ElMessage.warning(ui('已忽略无效的本机参数。', 'Invalid local parameters were ignored.')) }
    finally { restoreLegacyTrainCatalog(hasLegacyTrain) }
}
function download(name: string, contents: string, type: string) {
    const url = URL.createObjectURL(new Blob([contents], { type }))
    const anchor = window.document.createElement('a')
    anchor.href = url; anchor.download = name; anchor.click()
    setTimeout(() => URL.revokeObjectURL(url), 1000)
}
function exportRecord() {
    const snapshot = calculation.value
    if (!snapshot) return
    download('traction-calculation.json', JSON.stringify({ format: 'switchyard.traction', version: 2,
        routeCaseId: selectedCaseId.value, instanceId: props.selectedInstanceId, stationSchemeId: schemeId.value, calculationSetId: selectedSetId.value, calculationSetName: selectedSetName.value,
        routeIds: [snapshot.selectedRouteId, snapshot.continuationId].filter(Boolean),
        train: snapshot.request.train, signal: snapshot.request.signal, run: snapshot.request.options,
        profile: snapshot.request.profile, result: snapshot.result, assumptions: snapshot.result.warnings.map(diagnostic),
        revision: snapshot.revision, createdAt: snapshot.savedAtUtc,
    }, null, 2), 'application/json')
}
function exportCsv() {
    if (!result.value) return
    const rows = ['position_m,speed_kmh,limit_kmh,head_time_s', ...result.value.samples.map(p => [p.positionM, p.speedKmh, p.limitKmh, p.timeS].map(n => n.toFixed(4)).join(','))]
    download('traction-speed-position.csv', '\uFEFF' + rows.join('\r\n'), 'text/csv;charset=utf-8')
}
async function updateCalculationParameters() {
    if (!workspaceReady.value || workspaceConflict.value || routeBusy.value || updatingOccupancy.value || calculationPending.value || !resultMatchesInputs.value || !result.value || !routeProfile.value || !selectedRoute.value || !parameterSetId.value) return
    const snapshot = result.value
    const scope = { instanceID: props.selectedInstanceId, stationSchemeID: schemeId.value, calculationParameterSetID: parameterSetId.value, trainTypeID: '' }
    const preview = occupancyPreview.value
    const completedRouteIDs: string[] = []
    let updatedCellCount = 0
    const scopeIsCurrent = () => !disposed && resultMatchesInputs.value && result.value === snapshot && props.selectedInstanceId === scope.instanceID
        && schemeId.value === scope.stationSchemeID && parameterSetId.value === scope.calculationParameterSetID
    updatingOccupancy.value = true
    try {
        if (preview.error) throw new Error(preview.error)
        const updates = preview.updates
        const writable = updates.filter(update => update.times.length > 0)
        const skippedCount = updates.reduce((count, update) => count + update.skippedCellIDs.length, 0)
        if (!writable.length) {
            ElMessage.info(ui('没有可更新的占用参数；无法确定占用状态的区段保留原值。', 'No occupancy parameters are available to update. Circuits with an undetermined occupancy state retain their existing values.'))
            return
        }
        // Read and validate every route before any replacement save; retain other configured cells.
        const prepared = await Promise.all(writable.map(async update => {
            const { data } = await axios.get('/StationLayout/GetStationRouteTimes', { params: { ...scope, routeID: update.routeID }, timeout: 30_000 })
            return { update, times: mergeTractionOccupancyTimes(data, update) }
        }))
        for (const { update, times } of prepared) {
            if (!scopeIsCurrent()) throw new Error(ui('计算结果或目标已改变，请重新更新。', 'The result or destination changed. Run the update again.'))
            const { data } = await axios.put('/StationLayout/SaveStationRouteTimes', { ...scope, routeID: update.routeID, times,
                operationTimeS: update.endTimeS - update.startTimeS }, { params: { includeOperationTime: true }, timeout: 30_000 })
            const confirmed = data?.operationTimeS === update.endTimeS - update.startTimeS
                && Array.isArray(data?.times) && update.times.every(expected => data.times.some((row: Record<string, unknown>) =>
                String(row.cellID ?? row.CellID ?? '') === expected.cellID
                && (row.startOccupationShift ?? row.StartOccupationShift) === expected.startOccupationShift
                && (row.endOccupationShift ?? row.EndOccupationShift) === expected.endOccupationShift))
            if (!confirmed) throw new Error(ui('保存响应未能确认占用时刻，请刷新计算参数核对后重试。', 'The save response did not confirm the occupancy times. Refresh calculation parameters before retrying.'))
            completedRouteIDs.push(update.routeID)
            updatedCellCount += update.times.length
        }
        ElMessage.success(ui(
            `已更新 ${completedRouteIDs.length} 条进路、${updatedCellCount} 个轨道电路。${skippedCount ? ` ${skippedCount} 个区段时刻不完整，保留原值。` : ''}`,
            `Updated ${updatedCellCount} circuits across ${completedRouteIDs.length} routes.${skippedCount ? ` ${skippedCount} incomplete circuits retain their existing values.` : ''}`,
        ))
    } catch (reason) {
        const error = reason as { message?: string; response?: { data?: unknown } }
        const detail = typeof error.response?.data === 'string' ? error.response.data : error.message || ''
        ElMessage.error(ui(
            `${completedRouteIDs.length ? `进路 ${completedRouteIDs.join('、')} 已更新；其余更新未完成。` : '更新计算参数失败。'} ${detail}`,
            `${completedRouteIDs.length ? `Routes ${completedRouteIDs.join(', ')} were updated; remaining updates did not finish.` : 'Could not update calculation parameters.'} ${detail}`,
        ))
    } finally {
        if (completedRouteIDs.length) capacityStore.notifyRouteOccupancyUpdated({ ...scope, routeIDs: completedRouteIDs })
        updatingOccupancy.value = false
    }
}
async function loadSchemes() {
    const version = ++schemeVersion
    const instanceId = props.selectedInstanceId
    dataVersion++
    loadingData.value = false
    document.value = null; routes.value = []; schemes.value = []; loadError.value = false
    loadingSchemes.value = true
    try {
        const list = await gateway.getStationSchemes({ instanceId })
        if (version !== schemeVersion || instanceId !== props.selectedInstanceId) return
        schemes.value = list
        const chosen = list.some(item => item.id === schemeId.value) ? schemeId.value : list[0]?.id || ''
        if (chosen) {
            if (chosen === schemeId.value) await loadWorkspaceData()
            else schemeId.value = chosen
        } else {
            capacityStore.clearStationScheme(instanceId)
            loadingData.value = false
        }
    } catch { if (version === schemeVersion) loadError.value = true }
    finally { if (version === schemeVersion) loadingSchemes.value = false }
}
async function loadData() {
    const version = ++dataVersion
    const instanceId = props.selectedInstanceId
    const stationSchemeId = schemeId.value
    document.value = null; routes.value = []; loadError.value = false
    if (!instanceId || !stationSchemeId) { loadingData.value = false; return }
    loadingData.value = true
    try {
        const [layout, response] = await Promise.all([
            gateway.getJson({ instanceId, stationSchemeId }),
            axios.get('/StationLayout/GetStationRoutes', { params: { instanceID: instanceId, stationSchemeID: stationSchemeId } }),
        ])
        if (version !== dataVersion || instanceId !== props.selectedInstanceId || stationSchemeId !== schemeId.value) return
        document.value = layout
        const read = (row: Record<string, unknown>, camel: string, pascal: string) => String(row[camel] ?? row[pascal] ?? '')
        routes.value = (Array.isArray(response.data) ? response.data : []).map((row: Record<string, unknown>) => ({
            id: read(row, 'id', 'ID'), type: read(row, 'type', 'Type'), description: read(row, 'description', 'Description'),
            startNodeID: read(row, 'startNodeID', 'StartNodeID'), endNodeID: read(row, 'endNodeID', 'EndNodeID'),
            nodeList: read(row, 'nodeList', 'NodeList'), linkList: read(row, 'linkList', 'LinkList'),
            cellList: read(row, 'cellList', 'CellList'), signalList: read(row, 'signalList', 'SignalList'), switchList: read(row, 'switchList', 'SwitchList'),
        })).filter(item => item.id && !['dw', 'dwelling', '停留', '停留进路'].includes(item.type.trim().toLowerCase()))
    } catch { if (version === dataVersion) loadError.value = true }
    finally { if (version === dataVersion) loadingData.value = false }
}
watch(() => props.selectedInstanceId, () => { flushWorkspaceSave(); cancelCalculation(true); workspaceDataVersion++; workspaceEpoch++; workspaceReady.value = false; projectTrains.value = null; selectedSetId.value = ''; selectedCaseId.value = ''; routeCases.value = []; routeResultsVisible.value = false; calculationSets.value = []; setsVisible.value = false; selectedRouteId.value = ''; continuationId.value = ''; void loadSchemes() }, { immediate: true })
watch(schemeId, () => { flushWorkspaceSave(); cancelCalculation(true); workspaceDataVersion++; workspaceEpoch++; workspaceReady.value = false; selectedSetId.value = ''; selectedCaseId.value = ''; routeCases.value = []; routeResultsVisible.value = false; calculationSets.value = []; setsVisible.value = false; selectedRouteId.value = ''; continuationId.value = ''; void loadWorkspaceData() })
watch(() => props.active, value => { if (value && schemes.value.length) void loadWorkspaceData(); else if (!value) flushWorkspaceSave() })
watch([trainCatalog, signal, run, metersPerUnit, distanceMode, defaultLimit, speedLimitPoints, overrides, selectedRouteId, continuationId], () => { saved.value = false; scheduleWorkspaceSave() }, { deep: true, flush: 'post' })
// Edits cancel in-flight work but retain the last committed result; they never initiate calculation.
watch([profile, train, signal, run, selectedRouteId, continuationId, workspaceReady, () => props.active], () => cancelCalculation(), { deep: true, flush: 'sync' })
onBeforeUnmount(() => { flushWorkspaceSave(); disposed = true; workspaceDataVersion++; workspaceEpoch++; schemeVersion++; dataVersion++; cancelCalculation() })
</script>

<template>
    <section class="traction-workspace" v-loading="loadingSchemes || loadingData || workspaceLoading || switchingSet">
        <header class="traction-toolbar" :inert="batchBusy">
            <div class="route-controls">
                <label class="route-control"><span>{{ ui('车站', 'Station') }}</span><el-select v-model="schemeId" size="small" class="scheme-select" :aria-label="ui('站场方案', 'Station scheme')" :placeholder="ui('选择站场方案', 'Select scheme')" :disabled="loadingSchemes">
                    <el-option v-for="scheme in schemes" :key="scheme.id" :value="scheme.id" :label="scheme.name" />
                </el-select></label>
                <label class="route-control calculation-set-control"><span>{{ ui('计算方案', 'Calculation set') }}</span><el-select :model-value="selectedSetId" size="small" class="calculation-set-select" :aria-label="ui('牵引计算方案', 'Traction calculation set')" :disabled="!calculationSets.length || setBusy || routeBusy || switchingSet || workspaceLoading || updatingOccupancy" @update:model-value="switchCalculationSet">
                    <el-option v-for="set in calculationSets" :key="set.id" :value="set.id" :label="set.name" />
                </el-select><el-button size="small" class="manage-calculation-sets" :disabled="!calculationSets.length || setBusy || switchingSet || updatingOccupancy" @click="openSetManager">{{ ui('管理', 'Manage') }}</el-button></label>
                <label class="route-control"><span>{{ ui('进路', 'Route') }}</span><el-select :model-value="selectedRouteId" @update:model-value="(id: string) => switchRoute(id)" :disabled="routeBusy || switchingSet || workspaceLoading || updatingOccupancy || setBusy" size="small" filterable class="route-select" :aria-label="ui('计算进路', 'Calculation route')" :placeholder="ui('选择进路', 'Select route')">
                    <el-option v-for="route in routes" :key="route.id" :value="route.id" :label="routeLabel(route)" />
                </el-select></label>
                <label class="route-control"><span>{{ ui('接续进路', 'Continuation') }}</span><el-select :model-value="continuationId" @update:model-value="(id: string) => switchRoute(selectedRouteId, id || '')" size="small" clearable filterable class="route-select continuation-select" :aria-label="ui('接续进路', 'Continuation route')" :placeholder="ui('接续进路（可选）', 'Continuation (optional)')" :disabled="!continuations.length || routeBusy || switchingSet || workspaceLoading || updatingOccupancy || setBusy">
                    <el-option v-for="route in continuations" :key="route.id" :value="route.id" :label="routeLabel(route)" />
                </el-select></label>
            </div>
            <div class="outside-station-toggle">
                <span>{{ ui('显示站外内容', 'Show outside station') }}</span>
                <el-switch v-model="showOutsideStation" size="small" :aria-label="ui('显示站外内容', 'Show outside station')" />
            </div>
            <el-button size="small" class="route-results-button" :disabled="!selectedSetId || routeBusy || switchingSet || workspaceLoading" @click="openRouteResults">{{ ui('方案进路结果', 'Route results') }}</el-button>
            <el-button type="primary" size="small" class="calculate-button" :loading="calculationPending" :disabled="!canCalculate" @click="calculate">{{ ui('计算', 'Calculate') }}</el-button>
            <el-button size="small" class="timing-details-button" :disabled="!result" @click="timingDetailsVisible = true">{{ ui('轨道电路占用明细', 'Track circuit occupancy details') }}</el-button>
            <el-button size="small" @click="reloadWorkspace">{{ ui('刷新', 'Refresh') }}</el-button>
            <el-dropdown :disabled="!result" @command="(command: string) => command === 'csv' ? exportCsv() : exportRecord()">
                <el-button size="small" :disabled="!result">{{ ui('导出', 'Export') }} ▾</el-button>
                <template #dropdown><el-dropdown-menu><el-dropdown-item command="json">{{ ui('完整计算记录 · JSON', 'Full calculation · JSON') }}</el-dropdown-item><el-dropdown-item command="csv">{{ ui('速度曲线数据 · CSV', 'Speed profile · CSV') }}</el-dropdown-item></el-dropdown-menu></template>
            </el-dropdown>
        </header>
        <div v-if="workspaceError" class="workspace-save-error" role="alert"><span>{{ workspaceError }}</span><el-button size="small" @click="workspaceConflict || !workspaceReady ? reloadWorkspace() : saveParameters()">{{ workspaceConflict || !workspaceReady ? ui('重新载入', 'Reload') : ui('重试保存', 'Retry save') }}</el-button></div>
        <div class="traction-body">
            <main class="plot-workspace" :aria-busy="calculationPending">
                <div class="summary-strip" role="region" :aria-label="ui('运行摘要', 'Run summary')" tabindex="0">
                    <div v-if="result && !resultMatchesInputs" class="pending-result" role="status" :title="resultRouteLabel"><span>{{ ui('待计算 · 上次结果', 'Pending · Last result') }} · {{ resultRouteLabel }}</span></div>
                    <div><span>{{ ui('制动距离', 'Braking distance') }}</span><strong>{{ runSummary?.brakingDistanceM.toFixed(1) ?? '—' }} <small>m</small></strong></div>
                    <div><span>{{ ui('进路长度', 'Route length') }}</span><strong>{{ (runSummary?.routeLengthM ?? profile?.lengthM)?.toFixed(1) ?? '—' }} <small>m</small></strong></div>
                    <div :title="ui('实际计算速度', 'Actual calculated speeds')"><span>{{ ui('入口速度/出口速度', 'Entry/exit speed') }}</span><strong>{{ runSummary?.entrySpeedKmh?.toFixed(1) ?? '—' }} / {{ runSummary?.exitSpeedKmh?.toFixed(1) ?? '—' }} <small>km/h</small></strong></div>
                    <div><span>{{ ui('全程时间（含接近）', 'Journey time incl. approach') }}</span><strong>{{ formatTractionDuration(runSummary?.journeyTimeS) }}</strong></div>
                    <div :title="occupancySummaryTitle"><span>{{ ui('占用时间（最小间隔）', 'Occupancy time (minimum interval)') }}</span><strong class="cyan">{{ formatTractionDuration(runSummary?.longestOccupancy?.durationS) }}</strong></div>
                </div>
                <div v-if="calculationLoadError" class="result-notice" role="alert"><span>{{ ui('已保存的计算结果加载失败。', 'Could not load the saved calculation.') }}</span><el-button size="small" @click="loadWorkspaceData">{{ ui('重新载入', 'Reload') }}</el-button></div>
                <div v-if="calculationError" class="result-notice calculation-error" role="alert"><strong>{{ errorTitle }}</strong><span>{{ errorMessages.join(' · ') }}</span></div>
                <div v-if="reducedSpeedMessages.length" class="result-notice" role="status"><span v-for="message in reducedSpeedMessages" :key="message">{{ message }}</span></div>
                <TractionCharts v-if="chartProfile" :key="scopeKey()" v-model:split-ratio="chartSplitRatio" :profile="chartProfile" :result="result" :calculated-signal="calculation?.request.signal ?? null" :train-length-m="result?.trainLengthM ?? train.lengthM"
                    :visible-start-m="showOutsideStation ? 0 : approachDistanceM" :limit-points="chartLimitPoints" :editable-start-m="approachDistanceM" :editable-end-m="approachDistanceM + (profile?.lengthM ?? 0)" :limits-editable="canEditLimits" @update:limit-points="updateChartLimitPoints" />
                <div v-else-if="loadError" class="empty-panel" role="alert"><strong>{{ ui('站场数据加载失败', 'Could not load station data') }}</strong><p>{{ ui('请检查网络和登录状态，点击刷新重试。', 'Check your connection and sign-in, then refresh.') }}</p><el-button @click="loadSchemes">{{ ui('重试', 'Retry') }}</el-button></div>
                <div v-else-if="!selectedRoute" class="empty-panel"><div class="empty-graphic">⌁</div><strong>{{ ui('选择一条进路，查看全程运行曲线', 'Select a route to calculate the complete run') }}</strong><p>{{ ui('请先在站场布局和进路设计中保存方案与进路。通过作业可选择首尾相连的接续进路。', 'Save a layout and routes in Station layout and Route design. A through run can join consecutive routes.') }}</p></div>
                <div v-else-if="routeProfile?.errors.length" class="empty-panel" role="alert"><strong>{{ ui('请完善进路数据', 'Complete the route data') }}</strong><p v-for="(issue, index) in routeProfile.errors" :key="index">{{ diagnostic(issue.code) }} {{ issue.entityId }}</p><el-button v-if="routeProfile.errors.some(issue => issue.code === 'missingDistanceScale')" @click="distanceMode = 'manual'; expanded = [...new Set([...expanded, 'line'])]">{{ ui('启用人工距离标尺', 'Enable manual distance scale') }}</el-button></div>
                <div v-else class="empty-panel" role="status"><strong>{{ calculationPending ? ui('正在计算运行曲线…', 'Calculating the run profile…') : ui('暂无计算数据，点击“计算”生成曲线', 'No saved calculation. Click Calculate to generate the profile.') }}</strong></div>
            </main>
            <aside class="parameter-panel" :inert="batchBusy || !workspaceReady || workspaceConflict || workspaceLoading || routeBusy || setBusy || switchingSet">
                <header class="parameter-heading"><strong>{{ ui('计算参数', 'Parameters') }}</strong><span>{{ ui('后端计算', 'Backend calculation') }}</span></header>
                <section class="run-parameters">
                    <label class="field-label">{{ ui('作业类型', 'Movement') }}</label>
                    <el-radio-group :model-value="run.mode" @update:model-value="(value: string | number | boolean | undefined) => setMode(value as TractionRunOptions['mode'])" size="small">
                        <el-radio-button value="arrival">{{ ui('接车', 'Arrival') }}</el-radio-button><el-radio-button value="departure">{{ ui('发车', 'Departure') }}</el-radio-button><el-radio-button value="through">{{ ui('通过', 'Through') }}</el-radio-button><el-radio-button value="shunting">{{ ui('调车', 'Shunting') }}</el-radio-button>
                    </el-radio-group>
                    <div class="field-grid"><label>{{ ui('期望起点速度 (km/h)', 'Desired entry speed (km/h)') }}<el-input-number v-model="run.initialSpeedKmh" :min="0" :max="500" :disabled="run.mode === 'departure'" controls-position="right" /></label><label>{{ ui('期望终点速度 (km/h)', 'Desired exit speed (km/h)') }}<el-input-number v-model="run.finalSpeedKmh" :min="0" :max="500" :disabled="run.mode === 'arrival'" controls-position="right" /></label></div>
                    <div v-if="run.mode === 'arrival' || run.mode === 'through'" class="field-grid">
                        <label>{{ ui('制动距离 (m)', 'Braking distance (m)') }}<el-input-number v-model="run.brakingDistanceM" :min="0" :max="10000000" :precision="2" :step="100" :value-on-clear="null" :placeholder="ui('留空自动计算', 'Automatic when blank')" controls-position="right" /></label>
                        <label>{{ ui('制动段最高速度 (km/h)', 'Braking range max speed (km/h)') }}<el-input-number v-model="run.brakingMaxSpeedKmh" :min="0.1" :max="1000" :precision="1" :step="10" :value-on-clear="null" :placeholder="ui('列车最高速度', 'Train maximum')" :title="ui('留空时使用列车最高速度', 'Use the train maximum when blank')" controls-position="right" /></label>
                    </div>
                    <div v-if="run.mode === 'departure' || run.mode === 'through'" class="field-grid">
                        <label>{{ ui('一离去长度 (m)', 'First departure circuit length (m)') }}<el-input-number v-model="run.departureSectionLengthM" :min="0" :max="100000" :precision="1" :step="100" controls-position="right" /></label>
                        <label>{{ ui('一离去最高速度 (km/h)', 'First departure max speed (km/h)') }}<el-input-number v-model="run.departureSectionMaxSpeedKmh" :min="0.1" :max="1000" :precision="1" :step="10" :value-on-clear="null" :placeholder="ui('列车最高速度', 'Train maximum')" :title="ui('留空时使用列车最高速度', 'Use the train maximum when blank')" controls-position="right" /></label>
                    </div>
                    <p class="help-text">{{ ui('期望速度作为上限，实际起终点速度可因限速、停车和动力条件降低。', 'Desired speeds are upper bounds. Limits, stops and train performance may lower the actual entry and exit speeds.') }}</p>
                </section>
                <el-collapse v-model="expanded">
                    <el-collapse-item name="train" :title="ui('01  列车参数', '01  Train')">
                        <label class="field-label">{{ ui('列车预设', 'Train preset') }}</label>
                        <el-select :model-value="trainCatalog.selectedId" class="train-preset-select" :aria-label="ui('列车预设', 'Train preset')" :disabled="!workspaceReady || savingTrain" :loading="savingTrain" @update:model-value="selectTrainPreset">
                            <el-option v-for="preset in trainCatalog.presets" :key="preset.id" :value="preset.id" :label="trainPresetLabel(preset)" />
                        </el-select>
                        <dl class="train-preset-summary">
                            <div><dt>{{ ui('列车长度', 'Length') }}</dt><dd>{{ train.lengthM }} m</dd></div>
                            <div><dt>{{ ui('列车质量', 'Mass') }}</dt><dd>{{ train.massT }} t</dd></div>
                            <div><dt>{{ ui('最高速度', 'Max speed') }}</dt><dd>{{ train.maxSpeedKmh }} km/h</dd></div>
                            <div><dt>{{ ui('回转质量系数 γ', 'Rotating mass coefficient γ') }}</dt><dd>{{ rotatingMassCoefficientFromFactor(train.rotatingMassFactor) }}</dd></div>
                        </dl>
                        <p class="parameter-summary">{{ ui('牵引', 'Traction') }} {{ train.tractionCurve.length }} {{ ui('点', 'points') }} · {{ ui('制动', 'Braking') }} {{ train.brakingCurve.length }} {{ ui('点', 'points') }} · {{ ui('阻力', 'Resistance') }} {{ train.resistanceCurve.length }} {{ ui('点', 'points') }}</p>
                        <el-button class="parameter-dialog-button" :disabled="!workspaceReady || savingTrain" @click="trainSaveError = ''; trainParametersVisible = true">{{ ui('维护项目列车参数', 'Manage project train parameters') }}</el-button>
                    </el-collapse-item>
                    <el-collapse-item name="signal" :title="ui('02  信号系统', '02  Signalling')">
                        <div class="field-grid"><label>{{ ui('进路办理 (s)', 'Route setting (s)') }}<el-input-number v-model="signal.routeSettingS" :min="0" :max="3600" controls-position="right" /></label><label>{{ ui('反应时间 (s)', 'Reaction (s)') }}<el-input-number v-model="signal.reactionS" :min="0" :max="600" controls-position="right" /></label><label>{{ ui('出清延时 (s)', 'Release delay (s)') }}<el-input-number v-model="signal.clearanceS" :min="0" :max="600" controls-position="right" /></label></div>
                        <p class="help-text">{{ ui('t=0 为开始办理进路；车头进入起点前计办理与反应时间。出清延时加在车尾通过之后。', 't=0 is the route request. Setting and reaction precede head entry; release delay follows tail clearance.') }}</p>
                    </el-collapse-item>
                    <el-collapse-item name="line" :title="ui('03  线路与分段限速', '03  Line & section limits')">
                        <p class="parameter-summary">{{ ui('当前进路', 'Current route') }} · {{ lineParameterRows.length }} {{ ui('段', 'sections') }} · {{ ui('缺省限速', 'Default limit') }} {{ defaultLimit }} km/h</p>
                        <el-button class="parameter-dialog-button" @click="lineParametersVisible = true">{{ ui('维护坡度、限速与附加阻力', 'Edit gradients, limits and extra resistance') }}</el-button>
                        <label class="field-label">{{ ui('距离依据', 'Distance source') }}</label><el-radio-group v-model="distanceMode" size="small"><el-radio-button value="auto">{{ ui('实测长度 / 文件单位', 'Measured / file units') }}</el-radio-button><el-radio-button value="manual">{{ ui('人工标尺', 'Manual scale') }}</el-radio-button></el-radio-group>
                        <div class="field-grid"><label>{{ ui('米 / 画板单位', 'Metres / drawing unit') }}<el-input-number v-model="metersPerUnit" :disabled="distanceMode === 'auto'" :min="0.0001" :max="10000" :precision="4" :step="0.1" controls-position="right" /></label><label>{{ ui('计算步长 (m)', 'Position step (m)') }}<el-input-number v-model="run.stepM" :min="0.1" :max="100" :step="0.5" controls-position="right" /></label></div>
                        <div class="field-grid"><label>{{ ui('最大计算点数', 'Maximum calculation points') }}<el-input-number v-model="run.maxPointCount" :min="2" :max="100001" :step="1000" :precision="0" controls-position="right" /></label></div>
                        <p class="help-text">{{ ui('步长越小，曲线越精细；线路边界、限速解除与停车点会额外加入计算网格。超过点数预算时请增大步长或提高预算（最多 100001 点）。', 'Smaller steps refine the profile. Section boundaries, speed-limit clearances and stops add grid points. If the point budget is exceeded, increase the step or budget (up to 100001 points).') }}</p>
                        <p class="help-text">{{ ui('优先使用实测长度与文件单位；缺失时启用人工标尺并校准（初值 1 仅为假设）。车长用于限速解除和区段占用；坡度、附加阻力按车头所在分段计算。', 'Use measured lengths and file units first; otherwise enable and calibrate the manual scale (1 is an assumption). Train length controls speed-limit release and occupancy. Gradient and extra resistance use the section at the train head.') }}</p>
                    </el-collapse-item>
                    <el-collapse-item name="stops" :title="ui('04  停车与停时', '04  Stops and dwell times')">
                        <p class="help-text">{{ ui('位置以当前完整进路起点为 0 m，可按任意顺序填写；停车点不可重复。起点或终点停车要求对应边界速度为 0。每条进路及接续组合分别保存停车设置。', 'Positions start at 0 m along the complete selected route and may be entered in any order; duplicates are invalid. Stops at route endpoints require zero boundary speed. Stops are saved separately for each route and continuation.') }}</p>
                        <el-alert v-if="stopsCleared" :title="ui('进路已改变，原停车设置已清空，请重新核对。', 'The route changed and previous stops were cleared. Review stop settings.')" type="info" show-icon :closable="true" @close="stopsCleared = false" />
                        <div v-for="(stop, index) in run.stops" :key="index" class="stop-row">
                            <div class="field-grid"><label>{{ ui(`停车 ${index + 1} 位置 (m)`, `Stop ${index + 1} position (m)`) }}<el-input-number v-model="stop.positionM" :min="0" :max="profile?.lengthM" :precision="2" :step="10" :controls="false" /></label><label>{{ ui('停留时间 (s)', 'Dwell time (s)') }}<el-input-number v-model="stop.dwellS" :min="0" :max="86400" :precision="1" :step="5" :controls="false" /></label></div>
                            <el-button type="danger" link :aria-label="ui(`删除停车 ${index + 1}`, `Delete stop ${index + 1}`)" @click="run.stops.splice(index, 1)">{{ ui('删除', 'Delete') }}</el-button>
                        </div>
                        <el-button class="parameter-dialog-button add-stop" :disabled="!profile || run.stops.length >= 100" @click="addStop">{{ ui('添加停车点', 'Add stop') }} ({{ run.stops.length }}/100)</el-button>
                    </el-collapse-item>
                </el-collapse>
                <footer class="parameter-footer"><el-button type="primary" :loading="savingWrites > 0" :disabled="!workspaceReady || workspaceConflict" @click="saveParameters">{{ saved ? ui('已保存到数据库', 'Saved to database') : ui('保存到数据库', 'Save to database') }}</el-button><small>{{ ui('参数自动保存到当前方案的进路记录。', 'Parameters save automatically to this route in the selected set.') }}</small></footer>
            </aside>
        </div>
        <el-dialog v-model="timingDetailsVisible" :title="ui('轨道电路占用明细', 'Track circuit occupancy details')"
            width="min(1180px, calc(100vw - 32px))" top="8vh" append-to-body destroy-on-close class="timing-dialog" @open="reloadParameterSets"
            :close-on-click-modal="!updatingOccupancy" :close-on-press-escape="!updatingOccupancy" :show-close="!updatingOccupancy">
            <div class="occupancy-target">
                <label>{{ ui('计算参数集', 'Calculation parameter set') }}
                    <el-select v-model="parameterSetId" size="small" :aria-label="ui('计算参数集', 'Calculation parameter set')"
                        :loading="loadingParameterSets" :disabled="updatingOccupancy || loadingParameterSets">
                        <el-option v-for="set in parameterSets" :key="set.id" :value="set.id" :label="set.name" />
                    </el-select>
                </label>
                <span class="occupancy-route-label" :title="resultRouteLabel">{{ resultRouteLabel }}</span>
                <span v-if="parameterSetError" role="alert">{{ ui('参数集加载失败', 'Could not load parameter sets') }} <el-button link @click="reloadParameterSets">{{ ui('重试', 'Retry') }}</el-button></span>
            </div>
            <div v-if="result" class="timing-scroll" tabindex="0">
                <table><thead><tr>
                    <th>{{ ui('轨道电路', 'Track circuit') }}</th><th>{{ ui('位置 (m)', 'Position (m)') }}</th>
                    <th>{{ ui('车头进入 (s)', 'Head enters (s)') }}</th><th>{{ ui('车尾出清 (s)', 'Tail clears (s)') }}</th><th>{{ ui('占用结束 (s)', 'Occupancy ends (s)') }}</th>
                    <th v-for="column in occupancyOffsetColumns" :key="column.key">{{ ui(column.zh, column.en) }}</th>
                </tr></thead><tbody>
                    <tr v-for="cell in occupancyDetailRows" :key="cell.id" :data-cell-id="cell.id">
                        <td>{{ cell.label }}</td><td>{{ cell.startM.toFixed(1) }}–{{ cell.endM.toFixed(1) }}</td>
                        <td>{{ cell.entryTimeS?.toFixed(1) ?? ui('起始时刻未知', 'Entry time unknown') }}</td>
                        <td>{{ cell.exitTimeS?.toFixed(1) ?? (cell.exitBoundaryKnown === false ? ui('边界未覆盖', 'Boundary outside route') : ui('未出清', 'Not cleared')) }}</td>
                        <td :title="cell.stoppedAt !== null ? ui('以终点首次停车时刻结束占用', 'Occupancy ends at first terminal arrival') : undefined">{{ cell.occupancyEndTimeS?.toFixed(1) ?? (cell.exitBoundaryKnown === false ? ui('未知', 'Unknown') : '—') }}<small v-if="cell.stoppedAt !== null"> · {{ ui('停车', 'Stop') }}</small></td>
                        <td v-for="column in occupancyOffsetColumns" :key="column.key" :data-offset="column.key">
                            <div v-for="offset in cell.offsets" :key="offset.routeID" class="occupancy-offset" :data-route-id="offset.routeID">
                                <span v-if="occupancyPreview.updates.length > 1" class="occupancy-offset-route">{{ offset.routeID }}</span>
                                <span class="occupancy-offset-value" :title="offset[column.key] === null ? occupancyUnavailableTitle : undefined">{{ offset[column.key] ?? '—' }}</span>
                            </div>
                            <span v-if="!cell.offsets.length" :title="occupancyUnavailableTitle">—</span>
                        </td>
                    </tr>
                </tbody></table>
            </div>
            <template #footer>
                <el-button @click="timingDetailsVisible = false" :disabled="updatingOccupancy">{{ ui('关闭', 'Close') }}</el-button>
                <el-button type="primary" class="update-occupancy-button" :loading="updatingOccupancy"
                    :disabled="!workspaceReady || workspaceConflict || routeBusy || !resultMatchesInputs || calculationPending || !parameterSetId || loadingParameterSets || !!parameterSetError" @click="updateCalculationParameters">{{ ui('更新计算参数', 'Update calculation parameters') }}</el-button>
            </template>
        </el-dialog>
        <TractionRouteResultsDialog v-model="routeResultsVisible" :rows="routeCases" :selected-id="selectedCaseId" :set-name="selectedSetName" :routes="routes" :busy="routeBusy" :error="routeError" :batch-scope-key="batchScopeKey" :prepare-batch="prepareBatch"
            v-model:parameter-set-id="parameterSetId" :parameter-sets="parameterSets" :parameter-sets-loading="loadingParameterSets" :parameter-set-error="parameterSetError" :transfer-results="transferRouteResults" @reload-parameter-sets="reloadParameterSets"
            @open="row => switchRoute(row.routeId, row.continuationId)" @remove="removeRouteResult" @refresh="openRouteResults" @batch-busy="batchBusy = $event" @batch-complete="completeBatch" />
        <TractionSetsDialog v-model="setsVisible" :sets="calculationSets" :selected-id="selectedSetId" :busy="setBusy" :error="setError" @action="applySetAction" @refresh="openSetManager" />
        <TrainParametersDialog v-model="trainParametersVisible" :catalog="trainCatalog" :saving="savingTrain" :error="trainSaveError" @save="saveTrainCatalog" />
        <LineParametersDialog v-model="lineParametersVisible" :segments="lineParameterRows" v-model:default-limit="defaultLimit" :route-label="selectedRouteLabel" @change="setSegment" @limit-change="setLineSpeedLimit" />
    </section>
</template>

<style scoped>
.result-notice { display:flex; flex:none; align-items:center; flex-wrap:wrap; gap:6px 16px; padding:8px 2px; color:#f3c578; font-size:12px; max-height:96px; overflow:auto; }
.result-notice.calculation-error { color:#ff9c9c; }
.summary-strip .pending-result span { color:#f3c578; }
.workspace-save-error { display:flex; gap:12px; align-items:center; flex-shrink:0; padding:8px 14px; color:var(--el-color-danger); background:var(--el-color-danger-light-9); font-size:12px; }
.workspace-save-error span { flex:1; min-width:0; overflow-wrap:anywhere; }
.train-preset-select { width:100%; }
.train-preset-summary { display:grid; grid-template-columns:1fr 1fr; gap:10px 14px; margin:14px 0 10px; }
.train-preset-summary div { min-width:0; }
.train-preset-summary dt { color:var(--el-text-color-secondary); font-size:11px; }
.train-preset-summary dd { margin:3px 0 0; font-size:13px; font-weight:600; font-variant-numeric:tabular-nums; }
.traction-workspace { height:100%; min-height:0; display:flex; flex-direction:column; color:var(--sy-text,#243248); background:var(--sy-surface-muted,#f3f6fa); border:1px solid var(--sy-border,#dce3ed); border-radius:10px; overflow:hidden; }
.traction-toolbar { display:flex; align-items:center; flex-wrap:wrap; flex-shrink:0; gap:8px; padding:9px 14px; background:var(--sy-surface,#fff); border-bottom:1px solid var(--sy-border,#dce3ed); }
.outside-station-toggle { display:flex; align-items:center; flex:none; gap:6px; color:var(--sy-text-secondary,#65748b); font-size:12px; white-space:nowrap; }
.calculation-set-select { width:175px; }
.route-controls { display:flex; flex-wrap:wrap; align-items:center; gap:14px; flex:1; min-width:0; }.route-control { display:flex; align-items:center; gap:6px; min-width:0; }.route-control>span { flex:none; color:var(--sy-text-secondary,#65748b); font-size:12px; white-space:nowrap; }.scheme-select { width:140px; }.route-select { width:200px; }.continuation-select { width:180px; }
.traction-body { display:flex; flex:1; min-height:0; overflow:hidden; }.plot-workspace { display:flex; flex-direction:column; background:#0d1422; color:#dce5f5; flex:1; min-width:0; min-height:0; overflow:hidden; padding:0 14px 12px; }
.summary-strip { display:flex; flex:none; align-items:center; justify-content:space-between; gap:18px; height:46px; min-height:0; overflow:auto; overscroll-behavior:contain; border-bottom:1px solid #243046; margin-bottom:10px; padding:0 2px; white-space:nowrap; }.summary-strip>div { display:flex; flex:none; align-items:baseline; gap:8px; }.summary-strip span { color:#8e9db4; font-size:11px; }.summary-strip strong { color:#edf4ff; font-size:17px; font-variant-numeric:tabular-nums; font-weight:550; }.summary-strip small { font-size:10px; font-weight:400; color:#90a0b9; }.summary-strip .cyan { color:#4fe3d0; }
.parameter-panel { width:350px; flex:none; overflow:auto; background:var(--sy-surface,#fff); border-left:1px solid var(--sy-border,#dce3ed); }.parameter-heading { padding:18px 18px 12px; display:flex; justify-content:space-between; align-items:center; }.parameter-heading span { font-size:10px; padding:3px 7px; color:#087b6b; background:#e7f7f2; border-radius:10px; }.run-parameters { padding:0 18px 16px; }.field-label { display:block; font-size:11px; color:#65748b; margin:8px 0; }.field-grid { display:grid; grid-template-columns:1fr 1fr; gap:12px 10px; margin-top:14px; }.field-grid label { min-width:0; display:flex; flex-direction:column; gap:6px; font-size:11px; color:#65748b; }.field-grid :deep(.el-input-number) { width:100%; }.parameter-panel :deep(.el-collapse) { border-top-color:#e8edf4; }.parameter-panel :deep(.el-collapse-item__header) { padding:0 18px; font-size:12px; font-weight:600; letter-spacing:.03em; }.parameter-panel :deep(.el-collapse-item__content) { padding:0 18px 18px; }.help-text { font-size:11px; color:#7b879a; line-height:1.7; margin:12px 0 0; }.parameter-summary { margin:8px 0 12px; color:#7b879a; font-size:11px; line-height:1.7; }.parameter-dialog-button { width:100%; white-space:normal; height:auto; min-height:34px; padding:8px 10px; }.parameter-footer { display:flex; flex-direction:column; gap:10px; padding:18px; }.parameter-footer small { font-size:10px; text-align:center; color:#8995a8; }
.empty-panel { display:flex; flex:1; min-height:0; overflow:auto; flex-direction:column; justify-content:safe center; align-items:center; text-align:center; gap:12px; padding:40px; color:#c5d1e4; }.empty-panel p { max-width:540px; font-size:13px; line-height:1.8; color:#91a2bd; }.empty-panel small { color:#91a2bd; }.empty-graphic { color:#4cdacb; font-size:70px; }
.occupancy-target { display:flex; align-items:center; flex-wrap:wrap; gap:10px 18px; margin-bottom:14px; font-size:12px; }.occupancy-target label { display:flex; align-items:center; gap:8px; }.occupancy-target .el-select { width:200px; }.occupancy-route-label { min-width:0; max-width:100%; overflow:hidden; white-space:nowrap; text-overflow:ellipsis; color:var(--el-text-color-secondary); }.occupancy-target [role="alert"] { color:var(--el-color-danger); }
.timing-scroll { max-height:calc(84vh - 210px); overflow:auto; color:var(--el-text-color-regular); font-size:13px; }.timing-scroll table { width:100%; border-collapse:separate; border-spacing:0; font-variant-numeric:tabular-nums; }.timing-scroll th,.timing-scroll td { padding:10px 12px; border-bottom:1px solid var(--el-border-color-lighter); text-align:left; white-space:nowrap; }.timing-scroll th { position:sticky; top:0; background:var(--el-fill-color-light); color:var(--el-text-color-primary); font-weight:600; }.timing-scroll tbody tr:hover { background:var(--el-fill-color-lighter); }
.occupancy-offset { display:flex; align-items:baseline; gap:8px; line-height:1.7; }
.occupancy-offset-route { color:var(--el-text-color-secondary); font-size:12px; }
.occupancy-offset-value { font-weight:600; }
@media(min-width:1600px) { .parameter-panel { width:380px; }.plot-workspace { padding:0 20px 16px; } }
@media(max-width:1150px) { .route-controls { gap:10px; flex-basis:100%; }.scheme-select { width:125px; }.route-select { width:175px; }.continuation-select { width:155px; }.parameter-panel { width:310px; }.plot-workspace { padding:0 10px 10px; }.summary-strip { gap:14px; }.summary-strip>div { gap:5px; } }
@media(max-width:800px) { .traction-toolbar { padding:8px 10px; }.route-controls { flex-wrap:wrap; flex-basis:100%; gap:8px 12px; }.route-control { flex:1 1 190px; }.route-select,.scheme-select { flex:1; min-width:100px; width:auto; }.traction-body { flex-direction:column; }.plot-workspace { flex:1; }.parameter-panel { width:100%; flex:0 0 28%; min-height:0; border-left:0; border-top:1px solid var(--sy-border,#dce3ed); }.summary-strip { height:42px; }.summary-strip strong { font-size:15px; }.empty-panel { padding:20px; } }
.calculation-error p { margin:0; overflow-wrap:anywhere; white-space:pre-wrap; }.stop-row { display:flex; gap:8px; align-items:flex-end; }.stop-row .field-grid { flex:1; min-width:0; }.stop-row>.el-button { margin-bottom:6px; }.add-stop { margin-top:14px; }
</style>
