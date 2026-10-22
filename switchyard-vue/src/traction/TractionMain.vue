<script setup lang="ts">
import { computed, onBeforeUnmount, reactive, ref, shallowRef, watch } from 'vue'
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
import TrainParametersDialog from './TrainParametersDialog.vue'
import { createDefaultTrainPresetCatalog, cloneTrainParameters, cloneTrainPresetCatalog, parseTrainPresetCatalog, validateTrainPresetCatalog, type TrainPreset, type TrainPresetCatalog } from './trainPresets'
import LineParametersDialog from './LineParametersDialog.vue'
import { buildRouteProfile, type TractionStationRoute } from './routeProfile'
import { defaultTrainParameters, defaultSignalParameters, defaultRunOptions, type TractionRouteProfile, type TractionRunOptions, type TractionResult } from './tractionCalculation'
import { calculateTractionRemote } from './tractionApi'
import { tractionMessages } from './tractionMessages'

const props = defineProps<{ selectedInstanceId: string; active?: boolean }>()
const { locale } = useI18n()
const ui = (zh: string, en: string) => locale.value.startsWith('zh') ? zh : en
const capacityStore = useCapacityStore()
const schemeId = useStationSchemeSelection(() => props.selectedInstanceId)
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
const selectedTrainPreset = computed(() => trainCatalog.value.presets.find(item => item.id === trainCatalog.value.selectedId)!)
const trainPresetLabel = (preset: TrainPreset) => locale.value.startsWith('zh') ? preset.name : preset.nameEn || preset.name
const signal = reactive(defaultSignalParameters())
const run = reactive<TractionRunOptions>(defaultRunOptions())
const metersPerUnit = ref(1)
const distanceMode = ref<'auto' | 'manual'>('auto')
const defaultLimit = ref(80)
type SegmentField = 'speedLimitKmh' | 'gradientPermille' | 'additionalResistanceKn'
const overrides = reactive<Record<string, Partial<Record<SegmentField, number>>>>({})
const expanded = ref(['train', 'signal', 'line', 'stops'])
const trainParametersVisible = ref(false)
const lineParametersVisible = ref(false)
const timingDetailsVisible = ref(false)
const chartSplitRatio = ref(0.5)
const saved = ref(false)
const stopsCleared = ref(false)
const brakeMigration = ref<{ forceKn: number; variable: boolean } | null>(null)
let restoredScope = ''
let pendingSelection: { routeId: string; continuationId: string; run: TractionRunOptions } | null = null
const selectedRoute = computed(() => routes.value.find(item => item.id === selectedRouteId.value))
const continuations = computed(() => routes.value.filter(item => item.id !== selectedRouteId.value && item.startNodeID === selectedRoute.value?.endNodeID))
const routeLabel = (item: TractionStationRoute & { description?: string }) => item.description ? `${item.id} · ${item.description}` : item.id
const routeProfile = computed(() => {
    if (!document.value || !selectedRoute.value || loadingData.value) return null
    const next = continuations.value.find(item => item.id === continuationId.value)
    return buildRouteProfile(document.value, next ? [selectedRoute.value, next] : selectedRoute.value, distanceMode.value === 'manual' ? { metersPerUnit: metersPerUnit.value } : {})
})
const segmentKey = (id: string, index: number) => `${selectedRouteId.value}:${selectedRoute.value?.startNodeID}>${selectedRoute.value?.endNodeID}:${continuationId.value}:${index}:${id}`
const profile = computed<TractionRouteProfile | null>(() => {
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
const diagnostic = (code: string) => {
    const message = tractionMessages[code]
    return message ? ui(message[0], message[1]) : ui(`数据需要检查：${code}`, `Check data: ${code}`)
}
interface CalculationFailure { code: string; message: string; details: string[]; network: boolean; status?: number }
const result = shallowRef<TractionResult | null>(null)
const calculationPending = ref(false)
const calculationError = shallowRef<CalculationFailure | null>(null)
let calculationVersion = 0
let calculationTimer: ReturnType<typeof setTimeout> | undefined
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
function cancelCalculation() {
    calculationVersion++
    if (calculationTimer !== undefined) clearTimeout(calculationTimer)
    calculationTimer = undefined
    calculationController?.abort()
    calculationController = undefined
    result.value = null
    timingDetailsVisible.value = false
    calculationError.value = null
    calculationPending.value = false
}
function scheduleCalculation(delay = 350) {
    cancelCalculation()
    if (disposed || props.active === false || !profile.value) return
    calculationPending.value = true
    const version = calculationVersion
    calculationTimer = setTimeout(() => { void requestCalculation(version) }, delay)
}
async function requestCalculation(version: number) {
    calculationTimer = undefined
    if (version !== calculationVersion || !profile.value || props.active === false || disposed) return
    const controller = new AbortController()
    calculationController = controller
    try {
        const response = await calculateTractionRemote(profile.value, train, signal, run, controller.signal)
        if (version === calculationVersion && !controller.signal.aborted && !disposed) result.value = response
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
const notes = computed(() => [...new Set([
    ...(routeProfile.value?.warnings.map(issue => diagnostic(issue.code)) || []),
    ...(result.value?.warnings.map(diagnostic) || []),
])])
const migrationMessage = computed(() => {
    if (!brakeMigration.value) return ''
    const force = brakeMigration.value.forceKn
    return brakeMigration.value.variable
        ? ui(`旧版变制动力曲线已按最小值 ${force} kN 保守转换为恒定常用制动力。请核对车辆制动资料后调整。`, `The legacy variable braking curve was conservatively converted to its minimum, ${force} kN, as constant service braking. Review the vehicle data and adjust.`)
        : ui(`旧版恒定制动力曲线已转换为常用制动力 ${force} kN。`, `The legacy constant braking curve was converted to ${force} kN service braking.`)
})
const lineParameterRows = computed(() => (routeProfile.value?.segments ?? []).map((segment, index) => ({
    ...segment, index,
    speedLimitKmh: overrides[segmentKey(segment.id, index)]?.speedLimitKmh ?? segment.speedLimitKmh ?? defaultLimit.value,
    gradientPermille: overrides[segmentKey(segment.id, index)]?.gradientPermille ?? segment.gradientPermille,
    additionalResistanceKn: overrides[segmentKey(segment.id, index)]?.additionalResistanceKn ?? 0,
})))
const selectedRouteLabel = computed(() => [selectedRoute.value, continuations.value.find(item => item.id === continuationId.value)]
    .filter((item): item is NonNullable<typeof item> => Boolean(item)).map(routeLabel).join(' → '))
function setSegment(id: string, index: number, field: SegmentField, value: number | undefined) {
    if (value === undefined || !Number.isFinite(value)) return
    const key = segmentKey(id, index)
    overrides[key] = { ...overrides[key], [field]: value }
}
function setMode(mode: TractionRunOptions['mode']) {
    run.mode = mode
    run.initialSpeedKmh = mode === 'departure' ? 0 : 40
    run.finalSpeedKmh = mode === 'arrival' ? 0 : 40
}
function clearRouteStops() {
    if (run.stops.length) {
        run.stops = []
        stopsCleared.value = true
    }
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
function persistTrainCatalog(catalog: TrainPresetCatalog) {
    if (!props.selectedInstanceId || !schemeId.value || validateTrainPresetCatalog(catalog)) return false
    try {
        localStorage.setItem(trainStorageKey(), JSON.stringify(catalog))
        applyTrainCatalog(catalog)
        return true
    } catch {
        ElMessage.error(ui('列车参数未保存：本机存储不可用，请重试。', 'Train parameters were not saved: local storage is unavailable. Please retry.'))
        return false
    }
}
function selectTrainPreset(id: string) {
    if (id === trainCatalog.value.selectedId || !trainCatalog.value.presets.some(item => item.id === id)) return
    persistTrainCatalog({ ...trainCatalog.value, selectedId: id })
}
function saveTrainCatalog(catalog: TrainPresetCatalog) {
    if (!persistTrainCatalog(catalog)) return
    trainParametersVisible.value = false
    ElMessage.success(ui('列车参数已保存并应用。', 'Train parameters saved and applied.'))
}
function restoreTrainCatalog(hasLegacyTrain: boolean) {
    const defaults = createDefaultTrainPresetCatalog()
    if (!props.selectedInstanceId || !schemeId.value) { applyTrainCatalog(defaults); return }
    try {
        const raw = localStorage.getItem(trainStorageKey())
        const stored = parseTrainPresetCatalog(raw)
        if (stored) { applyTrainCatalog(stored); return }
        if (raw) ElMessage.warning(ui('已忽略无效的列车预设，恢复可用参数。', 'Invalid train presets were ignored; available parameters were restored.'))
        if (hasLegacyTrain) {
            const previous = cloneTrainParameters(train)
            const matching = defaults.presets.find(item => JSON.stringify(cloneTrainParameters(item.parameters)) === JSON.stringify(previous))
            if (matching) defaults.selectedId = matching.id
            else {
                const migrated: TrainPresetCatalog = { version: 1, selectedId: 'previous-train', presets: [...defaults.presets,
                    { id: 'previous-train', name: '原有列车参数', nameEn: 'Previous train parameters', parameters: previous }] }
                if (!validateTrainPresetCatalog(migrated)) {
                    defaults.presets = migrated.presets
                    defaults.selectedId = migrated.selectedId
                } else ElMessage.warning(ui('原有列车参数不完整或无效，已恢复示例预设。', 'Previous train parameters were invalid or incomplete; example presets were restored.'))
            }
        }
        applyTrainCatalog(defaults)
        localStorage.setItem(trainStorageKey(), JSON.stringify(defaults))
    } catch {
        applyTrainCatalog(defaults)
        ElMessage.warning(ui('本机列车预设暂时无法读取或保存。', 'Train presets could not be read or saved locally.'))
    }
}
function saveParameters() {
    try {
        localStorage.setItem(storageKey(), JSON.stringify({ version: 2, train, trainPresetId: trainCatalog.value.selectedId, signal, run, brakeMigration: brakeMigration.value, selectedRouteId: selectedRouteId.value, continuationId: continuationId.value, distanceMode: distanceMode.value, metersPerUnit: metersPerUnit.value, defaultLimit: defaultLimit.value, overrides }))
        saved.value = true
        ElMessage.success(ui('参数已保存到本机浏览器。', 'Parameters saved in this browser.'))
    } catch { ElMessage.error(ui('本机存储不可用，请导出计算记录。', 'Local storage is unavailable. Export the calculation record.')) }
}
function restoreParameters() {
    if (restoredScope === storageKey()) return
    restoredScope = storageKey()
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
    Object.keys(overrides).forEach(key => delete overrides[key])
    saved.value = false
    try {
        const raw = localStorage.getItem(storageKey()) ?? localStorage.getItem(storageKey(1))
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
        if (['arrival', 'departure', 'through'].includes(value.run?.mode)) run.mode = value.run.mode
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
                overrides[key] = {}
                for (const field of ['speedLimitKmh', 'gradientPermille', 'additionalResistanceKn'] as const) {
                    if (typeof row[field] === 'number' && Number.isFinite(row[field])) overrides[key]![field] = row[field]
                }
            }
        }
        pendingSelection = { routeId: String(value.selectedRouteId ?? ''), continuationId: String(value.continuationId ?? ''), run: { ...run, stops: run.stops.map(stop => ({ ...stop })) } }
        saved.value = true
    } catch { ElMessage.warning(ui('已忽略无效的本机参数。', 'Invalid local parameters were ignored.')) }
    finally { restoreTrainCatalog(hasLegacyTrain) }
}
function download(name: string, contents: string, type: string) {
    const url = URL.createObjectURL(new Blob([contents], { type }))
    const anchor = window.document.createElement('a')
    anchor.href = url; anchor.download = name; anchor.click()
    setTimeout(() => URL.revokeObjectURL(url), 1000)
}
function exportRecord() {
    if (!result.value || !profile.value || calculationPending.value) return
    download('traction-calculation.json', JSON.stringify({ format: 'switchyard.traction', version: 2,
        instanceId: props.selectedInstanceId, stationSchemeId: schemeId.value, routeIds: routeProfile.value?.routeIds,
        distanceMode: distanceMode.value, metersPerUnit: distanceMode.value === 'manual' ? metersPerUnit.value : null, train, trainPreset: { id: selectedTrainPreset.value.id, name: trainPresetLabel(selectedTrainPreset.value) }, signal, run, profile: profile.value, result: result.value,
        assumptions: [...notes.value, migrationMessage.value].filter(Boolean), createdAt: new Date().toISOString(),
    }, null, 2), 'application/json')
}
function exportCsv() {
    if (!result.value || calculationPending.value) return
    const rows = ['position_m,speed_kmh,limit_kmh,head_time_s', ...result.value.samples.map(p => [p.positionM, p.speedKmh, p.limitKmh, p.timeS].map(n => n.toFixed(4)).join(','))]
    download('traction-speed-position.csv', '\uFEFF' + rows.join('\r\n'), 'text/csv;charset=utf-8')
}
async function updateCalculationParameters() {
    if (updatingOccupancy.value || !result.value || !routeProfile.value || !selectedRoute.value || !parameterSetId.value) return
    const snapshot = result.value
    const scope = { instanceID: props.selectedInstanceId, stationSchemeID: schemeId.value, calculationParameterSetID: parameterSetId.value, trainTypeID: '' }
    const currentRoutes = [selectedRoute.value, continuations.value.find(route => route.id === continuationId.value)]
        .filter((route): route is NonNullable<typeof route> => !!route)
    const completedRouteIDs: string[] = []
    let updatedCellCount = 0
    const scopeIsCurrent = () => !disposed && result.value === snapshot && props.selectedInstanceId === scope.instanceID
        && schemeId.value === scope.stationSchemeID && parameterSetId.value === scope.calculationParameterSetID
    updatingOccupancy.value = true
    try {
        const updates = buildTractionOccupancyUpdates(currentRoutes, routeProfile.value, snapshot)
        const writable = updates.filter(update => update.times.length > 0)
        const skippedCount = updates.reduce((count, update) => count + update.skippedCellIDs.length, 0)
        if (!writable.length) {
            ElMessage.info(ui('没有可更新的完整占用时刻；未知或未出清的区段保留原值。', 'No complete occupancy times are available. Unknown or uncleared circuits retain their existing values.'))
            return
        }
        // Read and validate every route before any replacement save; retain other configured cells.
        const prepared = await Promise.all(writable.map(async update => {
            const { data } = await axios.get('/StationLayout/GetStationRouteTimes', { params: { ...scope, routeID: update.routeID }, timeout: 30_000 })
            return { update, times: mergeTractionOccupancyTimes(data, update) }
        }))
        for (const { update, times } of prepared) {
            if (!scopeIsCurrent()) throw new Error(ui('计算结果或目标已改变，请重新更新。', 'The result or destination changed. Run the update again.'))
            const { data } = await axios.put('/StationLayout/SaveStationRouteTimes', { ...scope, routeID: update.routeID, times }, { timeout: 30_000 })
            const confirmed = Array.isArray(data) && update.times.every(expected => data.some(row =>
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
            if (chosen === schemeId.value) { restoreParameters(); await loadData() }
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
    const oldRouteId = selectedRouteId.value
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
        const preferred = pendingSelection?.routeId || oldRouteId
        selectedRouteId.value = routes.value.some(item => item.id === preferred) ? preferred : routes.value[0]?.id || ''
        if (!continuations.value.some(item => item.id === continuationId.value)) continuationId.value = ''
        if (pendingSelection?.routeId === selectedRouteId.value) {
            const validContinuation = !pendingSelection.continuationId || continuations.value.some(item => item.id === pendingSelection!.continuationId)
            continuationId.value = validContinuation ? pendingSelection.continuationId : ''
            Object.assign(run, pendingSelection.run)
            if (!validContinuation) clearRouteStops()
            else stopsCleared.value = false
            saved.value = validContinuation
        } else if (pendingSelection) clearRouteStops()
        pendingSelection = null
    } catch { if (version === dataVersion) loadError.value = true }
    finally { if (version === dataVersion) loadingData.value = false }
}
watch(() => props.selectedInstanceId, () => { selectedRouteId.value = ''; continuationId.value = ''; void loadSchemes() }, { immediate: true })
watch(schemeId, () => { selectedRouteId.value = ''; continuationId.value = ''; restoreParameters(); void loadData() })
watch(selectedRouteId, () => {
    saved.value = false
    clearRouteStops()
    continuationId.value = ''
    const type = selectedRoute.value?.type.trim().toLowerCase() || ''
    if (['dep', 'departure', '发车', '发车进路'].includes(type)) setMode('departure')
    else if (['arr', 'arrival', '接车', '接车进路'].includes(type)) setMode('arrival')
    else setMode('through')
}, { flush: 'sync' })
watch(continuationId, value => { saved.value = false; clearRouteStops(); if (value) setMode('through') }, { flush: 'sync' })
watch(() => props.active, value => { if (value && schemes.value.length) void loadData() })
watch([train, signal, run, metersPerUnit, distanceMode, defaultLimit, overrides], () => { saved.value = false }, { deep: true, flush: 'sync' })
// Immediately invalidate charts/exports on any input change; debounce only the HTTP request.
watch([profile, train, signal, run, () => props.active, () => props.selectedInstanceId, schemeId], () => scheduleCalculation(), { deep: true, immediate: true, flush: 'sync' })
onBeforeUnmount(() => { disposed = true; schemeVersion++; dataVersion++; cancelCalculation() })
</script>

<template>
    <section class="traction-workspace" v-loading="loadingSchemes || loadingData">
        <header class="traction-toolbar">
            <div class="route-controls">
                <label class="route-control"><span>{{ ui('车站', 'Station') }}</span><el-select v-model="schemeId" size="small" class="scheme-select" :aria-label="ui('站场方案', 'Station scheme')" :placeholder="ui('选择站场方案', 'Select scheme')" :disabled="loadingSchemes">
                    <el-option v-for="scheme in schemes" :key="scheme.id" :value="scheme.id" :label="scheme.name" />
                </el-select></label>
                <label class="route-control"><span>{{ ui('进路', 'Route') }}</span><el-select v-model="selectedRouteId" size="small" filterable class="route-select" :aria-label="ui('计算进路', 'Calculation route')" :placeholder="ui('选择进路', 'Select route')">
                    <el-option v-for="route in routes" :key="route.id" :value="route.id" :label="routeLabel(route)" />
                </el-select></label>
                <label class="route-control"><span>{{ ui('接续进路', 'Continuation') }}</span><el-select v-model="continuationId" size="small" clearable filterable class="route-select continuation-select" :aria-label="ui('接续进路', 'Continuation route')" :placeholder="ui('接续进路（可选）', 'Continuation (optional)')" :disabled="!continuations.length">
                    <el-option v-for="route in continuations" :key="route.id" :value="route.id" :label="routeLabel(route)" />
                </el-select></label>
            </div>
            <el-button size="small" class="timing-details-button" :disabled="!result" @click="timingDetailsVisible = true">{{ ui('轨道电路占用明细', 'Track circuit occupancy details') }}</el-button>
            <el-button size="small" @click="loadSchemes">{{ ui('刷新', 'Refresh') }}</el-button>
            <el-dropdown :disabled="!result" @command="(command: string) => command === 'csv' ? exportCsv() : exportRecord()">
                <el-button size="small" :disabled="!result">{{ ui('导出', 'Export') }} ▾</el-button>
                <template #dropdown><el-dropdown-menu><el-dropdown-item command="json">{{ ui('完整计算记录 · JSON', 'Full calculation · JSON') }}</el-dropdown-item><el-dropdown-item command="csv">{{ ui('速度曲线数据 · CSV', 'Speed profile · CSV') }}</el-dropdown-item></el-dropdown-menu></template>
            </el-dropdown>
        </header>
        <div class="traction-body">
            <main class="plot-workspace" :aria-busy="calculationPending">
                <div class="summary-strip" role="region" :aria-label="ui('运行摘要', 'Run summary')" tabindex="0">
                    <div><span>{{ ui('展开长度', 'Route length') }}</span><strong>{{ profile?.lengthM.toFixed(1) ?? '—' }} <small>m</small></strong></div>
                    <div><span>{{ ui('全程时间（含停时）', 'Journey time incl. stops') }}</span><strong>{{ result?.durationS.toFixed(1) ?? '—' }} <small>s</small></strong></div>
                    <div><span>{{ ui('最高运行速度', 'Peak speed') }}</span><strong class="cyan">{{ result?.maxSpeedKmh.toFixed(1) ?? '—' }} <small>km/h</small></strong></div>
                    <div><span>{{ ui('轨道电路区段', 'Track circuits') }}</span><strong>{{ profile?.cells?.length ?? '—' }}</strong></div>
                </div>
                <div v-if="loadError" class="empty-panel" role="alert"><strong>{{ ui('站场数据加载失败', 'Could not load station data') }}</strong><p>{{ ui('请检查网络和登录状态，点击刷新重试。', 'Check your connection and sign-in, then refresh.') }}</p><el-button @click="loadSchemes">{{ ui('重试', 'Retry') }}</el-button></div>
                <div v-else-if="!selectedRoute" class="empty-panel"><div class="empty-graphic">⌁</div><strong>{{ ui('选择一条进路，查看全程运行曲线', 'Select a route to calculate the complete run') }}</strong><p>{{ ui('请先在站场布局和进路设计中保存方案与进路。通过作业可选择首尾相连的接续进路。', 'Save a layout and routes in Station layout and Route design. A through run can join consecutive routes.') }}</p></div>
                <div v-else-if="routeProfile?.errors.length" class="empty-panel" role="alert"><strong>{{ ui('请完善进路数据', 'Complete the route data') }}</strong><p v-for="(issue, index) in routeProfile.errors" :key="index">{{ diagnostic(issue.code) }} {{ issue.entityId }}</p><el-button v-if="routeProfile.errors.some(issue => issue.code === 'missingDistanceScale')" @click="distanceMode = 'manual'; expanded = [...new Set([...expanded, 'line'])]">{{ ui('启用人工距离标尺', 'Enable manual distance scale') }}</el-button></div>
                <div v-else-if="calculationPending" class="empty-panel" role="status" aria-live="polite"><div class="empty-graphic">↗</div><strong>{{ ui('正在计算运行曲线…', 'Calculating the run profile…') }}</strong><p>{{ ui('由 SwitchYard.SpeedProfile 计算速度与占用时刻。', 'SwitchYard.SpeedProfile is calculating speed and occupancy times.') }}</p></div>
                <div v-else-if="calculationError" class="empty-panel calculation-error" role="alert"><strong>{{ errorTitle }}</strong><p v-for="(message, index) in errorMessages" :key="index">{{ message }}</p><small>{{ calculationError.network ? ui('请检查网络、后端服务与登录状态，然后重试。', 'Check your network, backend service and sign-in, then retry.') : ui('请核对边界速度、停车位置、列车性能和线路参数；字段错误见上方详情。', 'Check boundary speeds, stop positions, train performance and line inputs. Field validation details appear above.') }}</small><el-button @click="scheduleCalculation(0)">{{ ui('重新计算', 'Retry calculation') }}</el-button></div>
                <TractionCharts v-else-if="result && profile" v-model:split-ratio="chartSplitRatio" :profile="profile" :result="result" :train-length-m="train.lengthM" />
            </main>
            <aside class="parameter-panel">
                <header class="parameter-heading"><strong>{{ ui('计算参数', 'Parameters') }}</strong><span>{{ ui('后端计算', 'Backend calculation') }}</span></header>
                <section class="run-parameters">
                    <label class="field-label">{{ ui('作业类型', 'Movement') }}</label>
                    <el-radio-group :model-value="run.mode" @update:model-value="(value: string | number | boolean | undefined) => setMode(value as TractionRunOptions['mode'])" size="small">
                        <el-radio-button value="arrival">{{ ui('接车', 'Arrival') }}</el-radio-button><el-radio-button value="departure">{{ ui('发车', 'Departure') }}</el-radio-button><el-radio-button value="through">{{ ui('通过', 'Through') }}</el-radio-button>
                    </el-radio-group>
                    <div class="field-grid"><label>{{ ui('起点速度 (km/h)', 'Entry speed (km/h)') }}<el-input-number v-model="run.initialSpeedKmh" :min="0" :max="500" :disabled="run.mode === 'departure'" controls-position="right" /></label><label>{{ ui('终点速度 (km/h)', 'Exit speed (km/h)') }}<el-input-number v-model="run.finalSpeedKmh" :min="0" :max="500" :disabled="run.mode === 'arrival'" controls-position="right" /></label></div>
                </section>
                <el-collapse v-model="expanded">
                    <el-collapse-item name="train" :title="ui('01  列车参数', '01  Train')">
                        <label class="field-label">{{ ui('列车预设', 'Train preset') }}</label>
                        <el-select :model-value="trainCatalog.selectedId" class="train-preset-select" :aria-label="ui('列车预设', 'Train preset')" :disabled="!schemeId" @update:model-value="selectTrainPreset">
                            <el-option v-for="preset in trainCatalog.presets" :key="preset.id" :value="preset.id" :label="trainPresetLabel(preset)" />
                        </el-select>
                        <dl class="train-preset-summary">
                            <div><dt>{{ ui('列车长度', 'Length') }}</dt><dd>{{ train.lengthM }} m</dd></div>
                            <div><dt>{{ ui('列车质量', 'Mass') }}</dt><dd>{{ train.massT }} t</dd></div>
                            <div><dt>{{ ui('最高速度', 'Max speed') }}</dt><dd>{{ train.maxSpeedKmh }} km/h</dd></div>
                            <div><dt>{{ ui('回转质量系数', 'Rotating mass factor') }}</dt><dd>{{ train.rotatingMassFactor }}</dd></div>
                        </dl>
                        <p class="parameter-summary">{{ ui('牵引', 'Traction') }} {{ train.tractionCurve.length }} {{ ui('点', 'points') }} · {{ ui('制动', 'Braking') }} {{ train.maxBrakeForceKn }} kN · R = A + Bv + Cv²</p>
                        <el-button class="parameter-dialog-button" :disabled="!schemeId" @click="trainParametersVisible = true">{{ ui('维护列车参数', 'Manage train parameters') }}</el-button>
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
                        <p class="help-text">{{ ui('位置以当前完整进路起点为 0 m，可按任意顺序填写；停车点不可重复。起点或终点停车要求对应边界速度为 0。更换进路或接续进路时清空停车设置。', 'Positions start at 0 m along the complete selected route and may be entered in any order; duplicates are invalid. Stops at route endpoints require zero boundary speed. Changing a route or continuation clears its stops.') }}</p>
                        <el-alert v-if="stopsCleared" :title="ui('进路已改变，原停车设置已清空，请重新核对。', 'The route changed and previous stops were cleared. Review stop settings.')" type="info" show-icon :closable="true" @close="stopsCleared = false" />
                        <div v-for="(stop, index) in run.stops" :key="index" class="stop-row">
                            <div class="field-grid"><label>{{ ui(`停车 ${index + 1} 位置 (m)`, `Stop ${index + 1} position (m)`) }}<el-input-number v-model="stop.positionM" :min="0" :max="profile?.lengthM" :precision="2" :step="10" :controls="false" /></label><label>{{ ui('停留时间 (s)', 'Dwell time (s)') }}<el-input-number v-model="stop.dwellS" :min="0" :max="86400" :precision="1" :step="5" :controls="false" /></label></div>
                            <el-button type="danger" link :aria-label="ui(`删除停车 ${index + 1}`, `Delete stop ${index + 1}`)" @click="run.stops.splice(index, 1)">{{ ui('删除', 'Delete') }}</el-button>
                        </div>
                        <el-button class="parameter-dialog-button add-stop" :disabled="!profile || run.stops.length >= 100" @click="addStop">{{ ui('添加停车点', 'Add stop') }} ({{ run.stops.length }}/100)</el-button>
                    </el-collapse-item>
                </el-collapse>
                <footer class="parameter-footer"><el-button type="primary" :disabled="!schemeId || !result" @click="saveParameters">{{ saved ? ui('已保存到本机', 'Saved locally') : ui('保存本机参数', 'Save parameters locally') }}</el-button><small>{{ ui('参数按实例与站场方案保存于此浏览器。', 'Saved per instance and scheme in this browser.') }}</small></footer>
            </aside>
        </div>
        <el-dialog v-model="timingDetailsVisible" :title="ui('轨道电路占用明细', 'Track circuit occupancy details')"
            width="min(960px, calc(100vw - 32px))" top="8vh" append-to-body destroy-on-close class="timing-dialog" @open="reloadParameterSets"
            :close-on-click-modal="!updatingOccupancy" :close-on-press-escape="!updatingOccupancy" :show-close="!updatingOccupancy">
            <div class="occupancy-target">
                <label>{{ ui('计算参数集', 'Calculation parameter set') }}
                    <el-select v-model="parameterSetId" size="small" :aria-label="ui('计算参数集', 'Calculation parameter set')"
                        :loading="loadingParameterSets" :disabled="updatingOccupancy || loadingParameterSets">
                        <el-option v-for="set in parameterSets" :key="set.id" :value="set.id" :label="set.name" />
                    </el-select>
                </label>
                <span class="occupancy-route-label" :title="selectedRouteLabel">{{ selectedRouteLabel }}</span>
                <span v-if="parameterSetError" role="alert">{{ ui('参数集加载失败', 'Could not load parameter sets') }} <el-button link @click="reloadParameterSets">{{ ui('重试', 'Retry') }}</el-button></span>
            </div>
            <div v-if="result" class="timing-scroll" tabindex="0"><table><thead><tr><th>{{ ui('轨道电路', 'Track circuit') }}</th><th>{{ ui('位置 (m)', 'Position (m)') }}</th><th>{{ ui('车头进入 (s)', 'Head enters (s)') }}</th><th>{{ ui('车尾出清 (s)', 'Tail clears (s)') }}</th><th>{{ ui('释放 (s)', 'Release (s)') }}</th></tr></thead><tbody>
            <tr v-for="cell in result.cellOccupancies" :key="`c-${cell.id}`"><td>{{ cell.label }}</td><td>{{ cell.startM.toFixed(1) }}–{{ cell.endM.toFixed(1) }}</td><td>{{ cell.entryTimeS?.toFixed(1) ?? ui('起始时刻未知', 'Entry time unknown') }}</td><td>{{ cell.exitTimeS?.toFixed(1) ?? (cell.exitBoundaryKnown === false ? ui('边界未覆盖', 'Boundary outside route') : ui('未出清', 'Not cleared')) }}</td><td>{{ cell.releaseTimeS?.toFixed(1) ?? (cell.exitBoundaryKnown === false ? ui('未知', 'Unknown') : '—') }}</td></tr>
        </tbody></table></div>
            <template #footer>
                <el-button @click="timingDetailsVisible = false" :disabled="updatingOccupancy">{{ ui('关闭', 'Close') }}</el-button>
                <el-button type="primary" class="update-occupancy-button" :loading="updatingOccupancy"
                    :disabled="!result || !parameterSetId || loadingParameterSets || !!parameterSetError" @click="updateCalculationParameters">{{ ui('更新计算参数', 'Update calculation parameters') }}</el-button>
            </template>
        </el-dialog>
        <TrainParametersDialog v-model="trainParametersVisible" :catalog="trainCatalog" @save="saveTrainCatalog" />
        <LineParametersDialog v-model="lineParametersVisible" :segments="lineParameterRows" v-model:default-limit="defaultLimit" :route-label="selectedRouteLabel" @change="setSegment" />
    </section>
</template>

<style scoped>
.train-preset-select { width:100%; }
.train-preset-summary { display:grid; grid-template-columns:1fr 1fr; gap:10px 14px; margin:14px 0 10px; }
.train-preset-summary div { min-width:0; }
.train-preset-summary dt { color:var(--el-text-color-secondary); font-size:11px; }
.train-preset-summary dd { margin:3px 0 0; font-size:13px; font-weight:600; font-variant-numeric:tabular-nums; }
.traction-workspace { height:100%; min-height:0; display:flex; flex-direction:column; color:var(--sy-text,#243248); background:var(--sy-surface-muted,#f3f6fa); border:1px solid var(--sy-border,#dce3ed); border-radius:10px; overflow:hidden; }
.traction-toolbar { display:flex; align-items:center; flex-wrap:wrap; flex-shrink:0; gap:8px; padding:9px 14px; background:var(--sy-surface,#fff); border-bottom:1px solid var(--sy-border,#dce3ed); }
.route-controls { display:flex; align-items:center; gap:14px; flex:1; min-width:0; }.route-control { display:flex; align-items:center; gap:6px; min-width:0; }.route-control>span { flex:none; color:var(--sy-text-secondary,#65748b); font-size:12px; white-space:nowrap; }.scheme-select { width:140px; }.route-select { width:200px; }.continuation-select { width:180px; }
.traction-body { display:flex; flex:1; min-height:0; overflow:hidden; }.plot-workspace { display:flex; flex-direction:column; background:#0d1422; color:#dce5f5; flex:1; min-width:0; min-height:0; overflow:hidden; padding:0 14px 12px; }
.summary-strip { display:flex; flex:none; align-items:center; justify-content:space-between; gap:18px; height:46px; min-height:0; overflow:auto; overscroll-behavior:contain; border-bottom:1px solid #243046; margin-bottom:10px; padding:0 2px; white-space:nowrap; }.summary-strip>div { display:flex; flex:none; align-items:baseline; gap:8px; }.summary-strip span { color:#8e9db4; font-size:11px; }.summary-strip strong { color:#edf4ff; font-size:17px; font-variant-numeric:tabular-nums; font-weight:550; }.summary-strip small { font-size:10px; font-weight:400; color:#90a0b9; }.summary-strip .cyan { color:#4fe3d0; }
.parameter-panel { width:350px; flex:none; overflow:auto; background:var(--sy-surface,#fff); border-left:1px solid var(--sy-border,#dce3ed); }.parameter-heading { padding:18px 18px 12px; display:flex; justify-content:space-between; align-items:center; }.parameter-heading span { font-size:10px; padding:3px 7px; color:#087b6b; background:#e7f7f2; border-radius:10px; }.run-parameters { padding:0 18px 16px; }.field-label { display:block; font-size:11px; color:#65748b; margin:8px 0; }.field-grid { display:grid; grid-template-columns:1fr 1fr; gap:12px 10px; margin-top:14px; }.field-grid label { min-width:0; display:flex; flex-direction:column; gap:6px; font-size:11px; color:#65748b; }.field-grid :deep(.el-input-number) { width:100%; }.parameter-panel :deep(.el-collapse) { border-top-color:#e8edf4; }.parameter-panel :deep(.el-collapse-item__header) { padding:0 18px; font-size:12px; font-weight:600; letter-spacing:.03em; }.parameter-panel :deep(.el-collapse-item__content) { padding:0 18px 18px; }.help-text { font-size:11px; color:#7b879a; line-height:1.7; margin:12px 0 0; }.parameter-summary { margin:8px 0 12px; color:#7b879a; font-size:11px; line-height:1.7; }.parameter-dialog-button { width:100%; white-space:normal; height:auto; min-height:34px; padding:8px 10px; }.parameter-footer { display:flex; flex-direction:column; gap:10px; padding:18px; }.parameter-footer small { font-size:10px; text-align:center; color:#8995a8; }
.empty-panel { display:flex; flex:1; min-height:0; overflow:auto; flex-direction:column; justify-content:safe center; align-items:center; text-align:center; gap:12px; padding:40px; color:#c5d1e4; }.empty-panel p { max-width:540px; font-size:13px; line-height:1.8; color:#91a2bd; }.empty-panel small { color:#91a2bd; }.empty-graphic { color:#4cdacb; font-size:70px; }
.occupancy-target { display:flex; align-items:center; flex-wrap:wrap; gap:10px 18px; margin-bottom:14px; font-size:12px; }.occupancy-target label { display:flex; align-items:center; gap:8px; }.occupancy-target .el-select { width:200px; }.occupancy-route-label { min-width:0; max-width:100%; overflow:hidden; white-space:nowrap; text-overflow:ellipsis; color:var(--el-text-color-secondary); }.occupancy-target [role="alert"] { color:var(--el-color-danger); }
.timing-scroll { max-height:calc(84vh - 210px); overflow:auto; color:var(--el-text-color-regular); font-size:13px; }.timing-scroll table { width:100%; border-collapse:separate; border-spacing:0; font-variant-numeric:tabular-nums; }.timing-scroll th,.timing-scroll td { padding:10px 12px; border-bottom:1px solid var(--el-border-color-lighter); text-align:left; white-space:nowrap; }.timing-scroll th { position:sticky; top:0; background:var(--el-fill-color-light); color:var(--el-text-color-primary); font-weight:600; }.timing-scroll tbody tr:hover { background:var(--el-fill-color-lighter); }
@media(min-width:1600px) { .parameter-panel { width:380px; }.plot-workspace { padding:0 20px 16px; } }
@media(max-width:1150px) { .route-controls { gap:10px; flex-basis:100%; }.scheme-select { width:125px; }.route-select { width:175px; }.continuation-select { width:155px; }.parameter-panel { width:310px; }.plot-workspace { padding:0 10px 10px; }.summary-strip { gap:14px; }.summary-strip>div { gap:5px; } }
@media(max-width:800px) { .traction-toolbar { padding:8px 10px; }.route-controls { flex-wrap:wrap; flex-basis:100%; gap:8px 12px; }.route-control { flex:1 1 190px; }.route-select,.scheme-select { flex:1; min-width:100px; width:auto; }.traction-body { flex-direction:column; }.plot-workspace { flex:1; }.parameter-panel { width:100%; flex:0 0 28%; min-height:0; border-left:0; border-top:1px solid var(--sy-border,#dce3ed); }.summary-strip { height:42px; }.summary-strip strong { font-size:15px; }.empty-panel { padding:20px; } }
.calculation-error p { margin:0; overflow-wrap:anywhere; white-space:pre-wrap; }.stop-row { display:flex; gap:8px; align-items:flex-end; }.stop-row .field-grid { flex:1; min-width:0; }.stop-row>.el-button { margin-bottom:6px; }.add-stop { margin-top:14px; }
</style>
