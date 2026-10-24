import axios from 'axios'
import { validateSpeedLimitPoints, type SpeedLimitPoint } from './speedLimitPoints.ts'
import { cloneTrainPresetCatalog, validateTrainPresetCatalog, type TrainPresetCatalog } from './trainPresets.ts'
import type { SignalParameters, TractionRunOptions, TractionResult } from './tractionCalculation'
import { buildTractionApiRequest, type TractionApiRequest } from './tractionApi.ts'

export interface TractionScope { instanceID: string; stationSchemeID: string; calculationSetID?: string; routeCaseID?: string }
export interface TractionSegmentOverride {
    routeId: string; routeStartNodeId: string; routeEndNodeId: string; continuationId: string
    segmentIndex: number; segmentId: string
    speedLimitKmh?: number; gradientPermille?: number; additionalResistanceKn?: number
}
export interface TractionWorkspaceContent {
    catalog: TrainPresetCatalog
    settings: { selectedRouteId: string; continuationId: string; signal: SignalParameters; run: TractionRunOptions
        distanceMode: 'auto' | 'manual'; metersPerUnit: number; defaultLimit: number }
    speedLimitPoints?: SpeedLimitPoint[]
    overrides: TractionSegmentOverride[]
}
export interface TractionWorkspace extends TractionWorkspaceContent { revision: number }
export interface StoredTractionCalculation {
    revision: number; currentWorkspaceRevision: number; isCurrent: boolean; savedAtUtc: string
    selectedRouteId: string; continuationId: string
    request: TractionApiRequest; result: TractionResult
}
export interface TractionCalculationSet {
    routeCount: number; calculatedCount: number; currentCount: number
    id: string; name: string; revision: number; createdAtUtc: string; updatedAtUtc: string
    selectedRouteId: string | null; trainName: string | null; savedAtUtc: string | null; durationS: number | null; isCurrent: boolean
}
export interface TractionRouteCase {
    id: string; routeId: string; continuationId: string; revision: number; isSelected: boolean
    trainName: string | null; savedAtUtc: string | null; durationS: number | null; isCurrent: boolean
}
export interface TractionSetCatalog { selectedId: string; sets: TractionCalculationSet[] }
export type TractionSetAction = { kind: 'create'; name: string; sourceId?: string; revision?: number }
    | { kind: 'rename'; id: string; name: string; revision: number }
    | { kind: 'delete'; id: string; revision: number }
const endpoint = '/api/SpeedProfile/workspace'
export async function loadTractionSets(scope: TractionScope): Promise<TractionSetCatalog> {
    const { data } = await axios.get<TractionSetCatalog>(endpoint + '/sets', { params: { instanceID: scope.instanceID, stationSchemeID: scope.stationSchemeID }, timeout: 30_000 })
    if (!data || !Array.isArray(data.sets) || !data.sets.length || !data.sets.some(item => item.id === data.selectedId)
        || data.sets.some(item => !item.id || !item.name || !Number.isSafeInteger(item.revision))) throw new Error('Invalid calculation set list.')
    return data
}
export async function selectTractionSet(scope: TractionScope): Promise<void> {
    await axios.put(endpoint + '/sets/selection', scope, { timeout: 30_000 })
}
export async function manageTractionSet(scope: TractionScope, action: TractionSetAction): Promise<TractionCalculationSet | null> {
    const station = { instanceID: scope.instanceID, stationSchemeID: scope.stationSchemeID }
    if (action.kind === 'create') return (await axios.post<TractionCalculationSet>(endpoint + '/sets', { ...station, name: action.name, sourceId: action.sourceId, revision: action.revision }, { timeout: 60_000 })).data
    const body = { ...station, calculationSetID: action.id, revision: action.revision }
    if (action.kind === 'rename') return (await axios.put<TractionCalculationSet>(endpoint + '/sets/name', { ...body, name: action.name }, { timeout: 30_000 })).data
    await axios.delete(endpoint + '/sets', { data: body, timeout: 30_000 })
    return null
}
export async function loadTractionRoutes(scope: TractionScope): Promise<TractionRouteCase[]> {
    const { data } = await axios.get<TractionRouteCase[]>(endpoint + '/routes', { params: scope, timeout: 30_000 })
    if (!Array.isArray(data) || data.some(row => !row.id || typeof row.routeId !== 'string' || typeof row.continuationId !== 'string' || !Number.isSafeInteger(row.revision))) throw new Error('Invalid route result list.')
    return data
}
export async function openTractionRoute(scope: TractionScope, routeId: string, continuationId: string): Promise<TractionRouteCase> {
    const { data } = await axios.post<TractionRouteCase>(endpoint + '/routes/open', { ...scope, routeId, continuationId }, { timeout: 30_000 })
    if (!data?.id || data.routeId !== routeId || data.continuationId !== continuationId) throw new Error('Invalid route record.')
    return data
}
export async function deleteTractionRoute(scope: TractionScope, row: TractionRouteCase): Promise<void> {
    await axios.delete(endpoint + '/routes', { data: { ...scope, routeCaseID: row.id, revision: row.revision }, timeout: 30_000 })
}
export const overrideKey = (row: Pick<TractionSegmentOverride, 'routeId' | 'routeStartNodeId' | 'routeEndNodeId' | 'continuationId' | 'segmentIndex' | 'segmentId'>) =>
    JSON.stringify([row.routeId, row.routeStartNodeId, row.routeEndNodeId, row.continuationId, row.segmentIndex, row.segmentId])

export function copyWorkspaceContent(value: TractionWorkspaceContent): TractionWorkspaceContent {
    const settings = value.settings
    return {
        catalog: cloneTrainPresetCatalog(value.catalog),
        speedLimitPoints: (value.speedLimitPoints ?? []).map(point => ({ positionM: point.positionM, speedLimitKmh: point.speedLimitKmh })),
        settings: { selectedRouteId: settings.selectedRouteId, continuationId: settings.continuationId,
            signal: { routeSettingS: settings.signal.routeSettingS, reactionS: settings.signal.reactionS, clearanceS: settings.signal.clearanceS },
            run: { mode: settings.run.mode, initialSpeedKmh: settings.run.initialSpeedKmh, finalSpeedKmh: settings.run.finalSpeedKmh,
                stepM: settings.run.stepM, maxPointCount: settings.run.maxPointCount, stops: settings.run.stops.map(stop => ({ positionM: stop.positionM, dwellS: stop.dwellS })) },
            distanceMode: settings.distanceMode, metersPerUnit: settings.metersPerUnit, defaultLimit: settings.defaultLimit },
        overrides: value.overrides.map(row => ({ routeId: row.routeId, routeStartNodeId: row.routeStartNodeId, routeEndNodeId: row.routeEndNodeId,
            continuationId: row.continuationId, segmentIndex: row.segmentIndex, segmentId: row.segmentId,
            speedLimitKmh: row.speedLimitKmh ?? undefined, gradientPermille: row.gradientPermille ?? undefined,
            additionalResistanceKn: row.additionalResistanceKn ?? undefined })).sort((a, b) => overrideKey(a).localeCompare(overrideKey(b))),
    }
}
export const workspaceFingerprint = (value: TractionWorkspaceContent) => JSON.stringify(copyWorkspaceContent(value))
function normalizeWorkspace(value: TractionWorkspace): TractionWorkspace {
    if (!value || !Number.isSafeInteger(value.revision) || value.revision < 1 || !value.catalog || !value.settings || !Array.isArray(value.overrides)) throw new Error('Invalid traction workspace response.')
    if (!Array.isArray(value.speedLimitPoints ?? []) || !validateSpeedLimitPoints(value.speedLimitPoints ?? [])) throw new Error('Invalid speed limit points in database response.')
    const catalog = { ...value.catalog, presets: value.catalog.presets?.map(preset => ({ ...preset, nameEn: preset.nameEn ?? undefined })) }
    if (validateTrainPresetCatalog(catalog)) throw new Error('Invalid train catalog in database response.')
    return { revision: value.revision, ...copyWorkspaceContent({ ...value, catalog }) }
}
export async function loadTractionWorkspace(scope: TractionScope): Promise<TractionWorkspace | null> {
    const response = await axios.get<TractionWorkspace>(endpoint, { params: scope, timeout: 30_000 })
    return response.status === 204 || !response.data ? null : normalizeWorkspace(response.data)
}
export async function loadTractionCalculation(scope: TractionScope): Promise<StoredTractionCalculation | null> {
    const response = await axios.get<StoredTractionCalculation>(`${endpoint}/calculation`, { params: scope, timeout: 30_000 })
    return response.status === 204 || !response.data ? null : normalizeCalculation(response.data)
}

/** Serializes writes and retains the acknowledged revision; a conflict requires a fresh load. */
export class TractionWorkspaceSession {
    readonly scope: TractionScope
    revision: number
    fingerprint: string
    private queue: Promise<unknown> = Promise.resolve()
    private conflict: unknown = null
    constructor(scope: TractionScope, initial: TractionWorkspace | null) {
        this.scope = { ...scope }
        this.revision = initial?.revision ?? 0
        this.fingerprint = initial ? workspaceFingerprint(initial) : ''
    }
    save(content: TractionWorkspaceContent): Promise<number> {
        const snapshot = copyWorkspaceContent(content)
        const fingerprint = workspaceFingerprint(snapshot)
        const operation = this.queue.then(async () => {
            if (this.conflict) throw this.conflict
            if (fingerprint === this.fingerprint) return this.revision
            try {
                const response = await axios.put<TractionWorkspace>(endpoint, { ...this.scope, workspace: { revision: this.revision, ...snapshot } }, { timeout: 30_000 })
                const saved = normalizeWorkspace(response.data)
                if (saved.revision !== this.revision + 1 || workspaceFingerprint(saved) !== fingerprint) throw new Error('The database did not acknowledge the complete traction workspace.')
                this.revision = saved.revision
                this.fingerprint = fingerprint
                return this.revision
            } catch (error) {
                if (axios.isAxiosError(error) && error.response?.status === 409) this.conflict = error
                throw error
            }
        })
        this.queue = operation.catch(() => undefined)
        return operation
    }
}
export async function calculateStoredTraction(scope: TractionScope, revision: number, request: TractionApiRequest, signal?: AbortSignal): Promise<StoredTractionCalculation> {
    const { data } = await axios.post<StoredTractionCalculation>(`${endpoint}/calculate`, { ...scope, revision, request }, { signal, timeout: 60_000 })
    const saved = normalizeCalculation(data)
    if (!saved.isCurrent || saved.revision !== revision
        || JSON.stringify(saved.request) !== JSON.stringify(buildTractionApiRequest(request.profile, request.train, request.signal, request.options))) {
        throw Object.assign(new Error('The database did not acknowledge the requested calculation.'), { code: 'invalid-response' })
    }
    return saved
}
function normalizeCalculation(data: StoredTractionCalculation): StoredTractionCalculation {
    const result = data?.result
    if (!Number.isSafeInteger(data?.revision) || data.revision < 1 || !data.request
        || typeof data.selectedRouteId !== 'string' || typeof data.continuationId !== 'string' || !result || result.engine !== 'SwitchYard.SpeedProfile'
        || !Array.isArray(result.samples) || result.samples.length < 2 || !Array.isArray(result.jointTimes)
        || !Array.isArray(result.cellOccupancies) || !Array.isArray(result.warnings)
        || ![result.durationS, result.runningTimeS, result.dwellTimeS, result.motionStartS, result.totalTimeS, result.lengthM, result.trainLengthM, result.maxSpeedKmh].every(Number.isFinite)) {
        throw Object.assign(new Error('Invalid persisted speed profile response.'), { code: 'invalid-response' })
    }
    try {
        const { profile, train, signal, options } = data.request
        return { ...data, request: buildTractionApiRequest(profile, train, signal, options) }
    } catch {
        throw Object.assign(new Error('Invalid persisted calculation inputs.'), { code: 'invalid-response' })
    }
}
