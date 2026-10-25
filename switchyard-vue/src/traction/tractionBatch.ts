import type { StationLayoutDocument } from '@switchyard/station-layout'
import { buildRouteProfile, type TractionStationRoute } from './routeProfile.ts'
import { defaultRunOptions, tractionModeForRoute, type TractionRouteProfile } from './tractionCalculation.ts'
import { buildTractionApiRequest } from './tractionApi.ts'
import { applySpeedLimitPoints, pointsFromSegments, setIntervalLimit, validateSpeedLimitPoints } from './speedLimitPoints.ts'
import { validateTrainPresetCatalog, type TrainPresetCatalog } from './trainPresets.ts'
import type { ProjectTrains } from './projectTrains.ts'
import { tractionMessages } from './tractionMessages.ts'
import { copyWorkspaceContent, overrideKey, workspaceFingerprint, loadTractionWorkspace, openTractionRoute, calculateStoredTraction, TractionWorkspaceSession,
    type TractionScope, type TractionRouteCase, type TractionWorkspaceContent, type TractionWorkspace, type StoredTractionCalculation } from './tractionWorkspace.ts'

export interface BatchStationRoute extends TractionStationRoute { type: string; description: string }
export interface TractionBatchContext {
    scope: TractionScope
    document: StationLayoutDocument
    routes: BatchStationRoute[]
    cases: TractionRouteCase[]
    seed: TractionWorkspaceContent
    saveTrains?: (catalog: TrainPresetCatalog) => Promise<ProjectTrains>
}
export interface TractionBatchCandidate {
    key: string; routeId: string; continuationId: string; available: boolean; record?: TractionRouteCase
}
export type TractionBatchStatus = 'loading' | 'ready' | 'load-error' | 'queued' | 'running' | 'success' | 'failed' | 'stopped'
export interface TractionBatchJob {
    candidate: TractionBatchCandidate
    caseId: string
    source: TractionWorkspace | null
    workspace: TractionWorkspaceContent | null
    status: TractionBatchStatus
    error: string
    conflict?: boolean
    calculation?: StoredTractionCalculation
}
export interface TractionBatchReport { scope: TractionScope; jobs: TractionBatchJob[] }
export type TractionBatchField = 'train' | 'mode' | 'run' | 'signal' | 'line'
const pairKey = (routeId: string, continuationId: string) => JSON.stringify([routeId, continuationId])
const fail = (message: string): never => { throw new Error(message) }
const inRange = (value: unknown, min: number, max: number): value is number => typeof value === 'number' && Number.isFinite(value) && value >= min && value <= max

/** Include uncalculated station routes and existing continuation combinations, once each. */
export function tractionBatchCandidates(routes: BatchStationRoute[], cases: TractionRouteCase[]): TractionBatchCandidate[] {
    const ids = new Set(routes.map(route => route.id)), values = new Map<string, TractionBatchCandidate>()
    for (const route of routes) values.set(pairKey(route.id, ''), { key: pairKey(route.id, ''), routeId: route.id, continuationId: '', available: true })
    for (const record of cases) {
        if (!record.routeId) continue
        const key = pairKey(record.routeId, record.continuationId)
        values.set(key, { key, routeId: record.routeId, continuationId: record.continuationId, record,
            available: ids.has(record.routeId) && (!record.continuationId || ids.has(record.continuationId)) })
    }
    return [...values.values()]
}

export async function loadTractionBatchJob(context: TractionBatchContext, candidate: TractionBatchCandidate,
    caseId = candidate.record?.id ?? ''): Promise<TractionBatchJob> {
    if (!candidate.available) fail('进路或接续进路已不存在，请刷新站场数据。')
    const source = caseId ? await loadTractionWorkspace({ ...context.scope, routeCaseID: caseId }) : null
    if (source && (source.settings.selectedRouteId !== candidate.routeId || source.settings.continuationId !== candidate.continuationId)) fail('进路参数归属不一致，请刷新列表。')
    const workspace = copyWorkspaceContent(source ?? context.seed)
    if (!source) {
        const route = context.routes.find(route => route.id === candidate.routeId)!
        const defaults = defaultRunOptions(tractionModeForRoute(route.type, !!candidate.continuationId))
        workspace.settings.selectedRouteId = candidate.routeId
        workspace.settings.continuationId = candidate.continuationId
        workspace.settings.run = { ...workspace.settings.run, mode: defaults.mode,
            initialSpeedKmh: defaults.initialSpeedKmh, finalSpeedKmh: defaults.finalSpeedKmh, stops: [] }
        workspace.speedLimitPoints = []
        workspace.overrides = workspace.overrides.filter(row => row.routeId === candidate.routeId && row.continuationId === candidate.continuationId)
    }
    return { candidate, caseId, source, workspace, status: 'ready', error: '' }
}

/** Use the same original geometry, scoped overrides and speed anchors as single-route calculation. */
export function tractionBatchProfile(context: TractionBatchContext, workspace: TractionWorkspaceContent) {
    const { selectedRouteId, continuationId, distanceMode, metersPerUnit, defaultLimit } = workspace.settings
    const route = context.routes.find(route => route.id === selectedRouteId)
    const continuation = continuationId ? context.routes.find(route => route.id === continuationId) : undefined
    if (!route || (continuationId && !continuation)) fail('进路或接续进路已不存在。')
    const raw = buildRouteProfile(context.document, continuation ? [route!, continuation] : route!, distanceMode === 'manual' ? { metersPerUnit } : {})
    const overrides = new Map(workspace.overrides.map(row => [overrideKey(row), row]))
    const rows = raw.segments.map((segment, index) => {
        const id = overrideKey({ routeId: selectedRouteId, routeStartNodeId: route!.startNodeID, routeEndNodeId: route!.endNodeID,
            continuationId, segmentIndex: index, segmentId: segment.id })
        const override = overrides.get(id)
        return { ...segment, index, speedLimitKmh: override?.speedLimitKmh ?? segment.speedLimitKmh ?? defaultLimit,
            gradientPermille: override?.gradientPermille ?? segment.gradientPermille, additionalResistanceKn: override?.additionalResistanceKn ?? 0 }
    })
    const profile: TractionRouteProfile = { lengthM: raw.lengthM, devices: raw.devices, joints: raw.joints, cells: raw.cells,
        segments: applySpeedLimitPoints(rows, workspace.speedLimitPoints ?? []) }
    return { raw, rows: applySpeedLimitPoints(rows, workspace.speedLimitPoints ?? []), profile }
}

export function tractionBatchIssues(context: TractionBatchContext, workspace: TractionWorkspaceContent): string[] {
    const issues: string[] = [], settings = workspace.settings, run = settings.run
    const catalogError = validateTrainPresetCatalog(workspace.catalog)
    if (catalogError) issues.push(catalogError)
    if (!['arrival', 'departure', 'through', 'shunting'].includes(run.mode)) issues.push('请选择作业类型。')
    if (!inRange(run.initialSpeedKmh, 0, 1000) || !inRange(run.finalSpeedKmh, 0, 1000)
        || (run.mode === 'arrival' && run.finalSpeedKmh !== 0) || (run.mode === 'departure' && run.initialSpeedKmh !== 0)) issues.push('请核对起终点期望速度。')
    if (!inRange(run.stepM, .1, 100) || !Number.isInteger(run.maxPointCount) || !inRange(run.maxPointCount, 2, 100001)) issues.push('请核对计算步长和最大计算点数。')
    if (!inRange(run.departureSectionLengthM ?? 0, 0, 100000) || (run.brakingDistanceM != null && !inRange(run.brakingDistanceM, 0, 10000000))
        || (run.brakingMaxSpeedKmh != null && !inRange(run.brakingMaxSpeedKmh, .001, 1000))
        || (run.departureSectionMaxSpeedKmh != null && !inRange(run.departureSectionMaxSpeedKmh, .001, 1000))) issues.push('请核对制动距离、一离去及其最高速度。')
    if (!Object.values(settings.signal).every(value => inRange(value, 0, 86400))) issues.push('请核对办理、反应和释放时间。')
    if (!inRange(settings.defaultLimit, .001, 1000) || !inRange(settings.metersPerUnit, .000001, 1000000)) issues.push('请核对距离标尺和缺省限速。')
    if (!validateSpeedLimitPoints(workspace.speedLimitPoints ?? [])) issues.push('允许速度锚点无效。')
    try {
        const { raw, profile } = tractionBatchProfile(context, workspace)
        issues.push(...raw.errors.map(issue => tractionMessages[issue.code]?.[0] ?? issue.code))
        const positions = new Set<number>()
        for (const stop of run.stops) {
            if (!inRange(stop.positionM, 0, profile.lengthM) || !inRange(stop.dwellS, 0, 86400) || positions.has(stop.positionM)) issues.push('停车位置、停时或重复停车点需要检查。')
            positions.add(stop.positionM)
        }
        if (run.stops.length > 1000) issues.push('停车点最多 1000 个。')
    } catch (error) { issues.push(batchFailureMessage(error)) }
    return [...new Set(issues)]
}

export function setBatchLineParameter(context: TractionBatchContext, workspace: TractionWorkspaceContent, segmentId: string, index: number,
    field: 'speedLimitKmh' | 'gradientPermille' | 'additionalResistanceKn', value: number | undefined) {
    if (value === undefined || !Number.isFinite(value)) return
    const route = context.routes.find(route => route.id === workspace.settings.selectedRouteId)!
    const row = { routeId: route.id, routeStartNodeId: route.startNodeID, routeEndNodeId: route.endNodeID,
        continuationId: workspace.settings.continuationId, segmentId, segmentIndex: index }
    const key = overrideKey(row), found = workspace.overrides.find(item => overrideKey(item) === key)
    if (found) found[field] = value
    else workspace.overrides.push({ ...row, [field]: value })
}
export function setBatchLineLimit(context: TractionBatchContext, workspace: TractionWorkspaceContent, startM: number, endM: number, value: number | undefined) {
    if (value === undefined) return
    const { profile } = tractionBatchProfile(context, workspace)
    const points = workspace.speedLimitPoints?.length ? workspace.speedLimitPoints : pointsFromSegments(profile.segments)
    workspace.speedLimitPoints = setIntervalLimit(points, startM, endM, value, profile.lengthM)
}

export function applyTractionBatchParameters(target: TractionWorkspaceContent, common: TractionWorkspaceContent, fields: TractionBatchField[]): TractionWorkspaceContent {
    const next = copyWorkspaceContent(target), source = copyWorkspaceContent(common)
    if (fields.includes('train')) {
        const error = validateTrainPresetCatalog(source.catalog)
        if (error) fail(error)
        if ((next.catalogRevision ?? 0) !== (source.catalogRevision ?? 0) || JSON.stringify(next.catalog.presets) !== JSON.stringify(source.catalog.presets))
            fail('项目列车参数已更新，请重新载入后选择列车。')
        next.catalog.selectedId = source.catalog.selectedId
    }
    const mode = fields.includes('mode') ? source.settings.run.mode : next.settings.run.mode
    if (fields.includes('mode')) {
        const defaults = defaultRunOptions(mode)
        next.settings.run = { ...next.settings.run, mode, initialSpeedKmh: defaults.initialSpeedKmh, finalSpeedKmh: defaults.finalSpeedKmh }
    }
    if (fields.includes('run')) next.settings.run = { ...source.settings.run, mode, stops: next.settings.run.stops.map(stop => ({ ...stop })) }
    if (mode === 'arrival') next.settings.run.finalSpeedKmh = 0
    if (mode === 'departure') next.settings.run.initialSpeedKmh = 0
    if (fields.includes('signal')) next.settings.signal = { ...source.settings.signal }
    if (fields.includes('line')) Object.assign(next.settings, { distanceMode: source.settings.distanceMode, metersPerUnit: source.settings.metersPerUnit, defaultLimit: source.settings.defaultLimit })
    return next
}

export function batchFailureMessage(error: unknown): string {
    const failure = error as { message?: string; response?: { status?: number; data?: { message?: string } } }
    return failure.response?.status === 409 ? '参数已被其他页面修改，请重新加载该进路参数后重试。'
        : failure.response?.data?.message || failure.message || '计算失败，请重试。'
}

/** Sequential commits give each route an independent result and a bounded server load. Stop finishes the current route. */
export async function runTractionBatch(context: TractionBatchContext, jobs: TractionBatchJob[], shouldStop: () => boolean,
    changed: (job: TractionBatchJob) => void = () => undefined): Promise<TractionBatchReport> {
    const plans = jobs.map(job => ({ job, workspace: job.workspace ? copyWorkspaceContent(job.workspace) : null,
        source: job.source ? { revision: job.source.revision, ...copyWorkspaceContent(job.source) } : null, caseId: job.caseId }))
    for (const { job } of plans) { job.status = 'queued'; job.error = ''; job.conflict = false; changed(job) }
    for (const plan of plans) {
        const { job, workspace, source } = plan
        if (shouldStop()) { job.status = 'stopped'; changed(job); continue }
        job.status = 'running'; changed(job)
        try {
            if (!workspace) fail('请先载入进路参数。')
            const issues = tractionBatchIssues(context, workspace!)
            if (issues.length) fail(issues.join('；'))
            const { profile } = tractionBatchProfile(context, workspace!)
            const preset = workspace!.catalog.presets.find(p => p.id === workspace!.catalog.selectedId)!
            const request = buildTractionApiRequest(profile, preset.parameters, workspace!.settings.signal, workspace!.settings.run)
            const record = await openTractionRoute(context.scope, job.candidate.routeId, job.candidate.continuationId, false)
            job.caseId = record.id
            if (plan.caseId && record.id !== plan.caseId) fail('进路记录已被删除或重新创建，请重新加载该进路参数。')
            const scope = { ...context.scope, routeCaseID: record.id }, current = await loadTractionWorkspace(scope)
            if ((current?.revision ?? 0) !== (source?.revision ?? 0) || (source && current && workspaceFingerprint(source) !== workspaceFingerprint(current)))
                throw Object.assign(new Error('revision conflict'), { response: { status: 409 } })
            const session = new TractionWorkspaceSession(scope, current)
            const revision = await session.save(workspace!)
            job.source = { revision, ...copyWorkspaceContent(workspace!) }
            const result = await calculateStoredTraction(scope, revision, request)
            if (result.selectedRouteId !== job.candidate.routeId || result.continuationId !== job.candidate.continuationId) fail('计算结果归属未确认，请重新载入结果。')
            job.calculation = result; job.status = 'success'
        } catch (error) {
            job.status = 'failed'; job.error = batchFailureMessage(error)
            job.conflict = (error as { response?: { status?: number } }).response?.status === 409
        }
        changed(job)
    }
    return { scope: { ...context.scope }, jobs }
}
