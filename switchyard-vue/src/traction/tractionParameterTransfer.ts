import axios from 'axios'
import { buildTractionApiRequest } from './tractionApi.ts'
import { tractionBatchProfile, type TractionBatchContext } from './tractionBatch.ts'
import { loadTractionCalculation, loadTractionWorkspace, type TractionRouteCase } from './tractionWorkspace.ts'
import { buildTractionOccupancyUpdates, mergeTractionOccupancyTimes, type RouteOccupancyUpdate } from './tractionOccupancy.ts'

export interface TractionParameterTarget {
    instanceID: string; stationSchemeID: string; calculationParameterSetID: string; trainTypeID: ''
}
export interface TractionParameterTransferReport {
    completedRouteIDs: string[]
    updatedCellCount: number
    skippedCellCount: number
    operationTimeOnlyRouteIDs: string[]
    error: string
}
const fail = (message: string): never => { throw new Error(message) }
const duration = (update: RouteOccupancyUpdate) => update.endTimeS - update.startTimeS
const orderedTimes = (update: RouteOccupancyUpdate) => [...update.times].sort((a, b) => a.cellID.localeCompare(b.cellID))

/** Validate every selected saved snapshot against current inputs before any parameter write. */
export async function prepareTractionParameterTransfers(context: TractionBatchContext, selection: readonly TractionRouteCase[]): Promise<RouteOccupancyUpdate[]> {
    if (!selection.length || new Set(selection.map(row => row.id)).size !== selection.length) fail('请选择需要带入的进路结果。')
    const updates = new Map<string, RouteOccupancyUpdate>()
    // Bound parallel reads; input/result errors leave the destination untouched.
    for (let index = 0; index < selection.length; index += 4) {
        const groups = await Promise.all(selection.slice(index, index + 4).map(async row => {
            const saved = context.cases.find(item => item.id === row.id)
            if (!saved || saved.routeId !== row.routeId || saved.continuationId !== row.continuationId || !row.savedAtUtc || !row.isCurrent)
                fail(`进路 ${row.routeId} 的结果不可用，请刷新列表并重新计算。`)
            const scope = { ...context.scope, routeCaseID: row.id }
            const [workspace, calculation] = await Promise.all([loadTractionWorkspace(scope), loadTractionCalculation(scope)])
            // The list stores ISO round-trip text; DateTime JSON trims trailing fractional zeros.
            const savedAt = calculation ? Date.parse(calculation.savedAtUtc) : NaN
            if (!workspace || !calculation || !calculation.isCurrent || calculation.revision !== workspace.revision
                || !Number.isFinite(savedAt) || savedAt !== Date.parse(row.savedAtUtc!) || calculation.selectedRouteId !== row.routeId || calculation.continuationId !== row.continuationId
                || workspace.settings.selectedRouteId !== row.routeId || workspace.settings.continuationId !== row.continuationId)
                fail(`进路 ${row.routeId} 的结果或参数已改变，请刷新列表并重新计算。`)
            const { raw, profile } = tractionBatchProfile(context, workspace!)
            const preset = workspace!.catalog.presets.find(item => item.id === workspace!.catalog.selectedId)!
            const request = buildTractionApiRequest(profile, preset.parameters, workspace!.settings.signal, workspace!.settings.run)
            if (JSON.stringify(request) !== JSON.stringify(calculation!.request)) fail(`进路 ${row.routeId} 的线路或参数与结果不一致，请重新计算。`)
            const routes = [row.routeId, row.continuationId].filter(Boolean).map(id => context.routes.find(route => route.id === id)!)
            return buildTractionOccupancyUpdates(routes, raw, calculation!.result)
        }))
        for (const update of groups.flat()) {
            const existing = updates.get(update.routeID)
            if (existing && (Math.abs(duration(existing) - duration(update)) > 1e-6
                || JSON.stringify(orderedTimes(existing)) !== JSON.stringify(orderedTimes(update))
                || JSON.stringify([...existing.skippedCellIDs].sort()) !== JSON.stringify([...update.skippedCellIDs].sort())))
                fail(`进路 ${update.routeID} 在所选结果中有不同的时间，请仅选择一个对应的进路组合后带入。`)
            if (!existing) updates.set(update.routeID, update)
        }
    }
    return [...updates.values()]
}

/** Read all replacement rows first, preserving unknown/indirect cells; acknowledge each independent save. */
export async function writeTractionParameterTransfers(target: TractionParameterTarget, updates: readonly RouteOccupancyUpdate[],
    scopeIsCurrent: () => boolean, saved: (update: RouteOccupancyUpdate) => void) {
    if (!target.instanceID || !target.stationSchemeID || !target.calculationParameterSetID || target.trainTypeID !== '') fail('请选择计算参数集。')
    const checkScope = () => { if (!scopeIsCurrent()) fail('当前方案或目标参数集已改变，请重新带入。') }
    const prepared: { update: RouteOccupancyUpdate; times: ReturnType<typeof mergeTractionOccupancyTimes> }[] = []
    for (let index = 0; index < updates.length; index += 4) {
        checkScope()
        prepared.push(...await Promise.all(updates.slice(index, index + 4).map(async update => {
            const { data } = await axios.get('/StationLayout/GetStationRouteTimes', { params: { ...target, routeID: update.routeID }, timeout: 30_000 })
            return { update, times: mergeTractionOccupancyTimes(data, update) }
        })))
    }
    for (const { update, times } of prepared) {
        checkScope()
        const operationTimeOnly = !update.times.length
        const { data } = await axios.put('/StationLayout/SaveStationRouteTimes', { ...target, routeID: update.routeID, times: operationTimeOnly ? [] : times,
            operationTimeS: duration(update) }, { params: { includeOperationTime: true, ...(operationTimeOnly ? { operationTimeOnly: true } : {}) }, timeout: 30_000 })
        const acknowledged = mergeTractionOccupancyTimes(data?.times, { ...update, times: [] })
        if (data?.operationTimeS !== duration(update) || !times.every(expected => acknowledged.some(row => row.cellID === expected.cellID
            && row.startOccupationShift === expected.startOccupationShift && row.endOccupationShift === expected.endOccupationShift)))
            fail(`进路 ${update.routeID} 的保存响应未确认完整参数，请刷新计算参数核对后重试。`)
        saved(update)
    }
}
