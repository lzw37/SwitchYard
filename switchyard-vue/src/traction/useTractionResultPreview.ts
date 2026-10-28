import { onScopeDispose, ref, shallowRef, watch } from 'vue'
import {
    loadTractionSets, loadTractionRoutes, loadTractionCalculation,
    type TractionScope, type TractionCalculationSet, type TractionRouteCase, type StoredTractionCalculation,
} from './tractionWorkspace.ts'

/** Read saved snapshots without changing the traction workspace's selected set or route. */
export function useTractionResultPreview(getScope: () => { instanceID: string; stationSchemeID: string; routeID: string }) {
    const sets = ref<TractionCalculationSet[]>([])
    const selectedSetID = ref('')
    const selectedCaseID = ref('')
    const calculation = shallowRef<StoredTractionCalculation | null>(null)
    const loading = ref(false)
    const error = shallowRef<unknown>(null)
    let version = 0

    async function readCalculation(scope: TractionScope, row: TractionRouteCase, requestVersion: number) {
        const saved = await loadTractionCalculation({ ...scope, routeCaseID: row.id })
        if (requestVersion !== version) return null
        if (saved && (saved.selectedRouteId !== row.routeId || saved.continuationId !== row.continuationId)) {
            throw new Error('The saved calculation belongs to another route.')
        }
        return saved
    }

    async function readRoutes(scope: TractionScope, routeID: string, requestVersion: number) {
        const rows = await loadTractionRoutes(scope)
        if (requestVersion !== version) return
        const standalone = rows.find(row => row.routeId === routeID && !row.continuationId)
        const continuations = rows.filter(row => (row.routeId === routeID && !!row.continuationId) || row.continuationId === routeID)
            .sort((a, b) => Number(b.isCurrent) - Number(a.isCurrent)
                || (b.savedAtUtc ?? '').localeCompare(a.savedAtUtc ?? '')
                || Number(b.isSelected) - Number(a.isSelected) || a.id.localeCompare(b.id))
        // An existing route record may still have no calculation snapshot (HTTP 204).
        // Try combined runs only after the standalone result is confirmed absent.
        for (const row of [...(standalone ? [standalone] : []), ...continuations]) {
            const saved = await readCalculation(scope, row, requestVersion)
            if (requestVersion !== version) return
            if (saved) {
                selectedCaseID.value = row.id
                calculation.value = saved
                return
            }
        }
    }

    async function load(action: (requestVersion: number) => Promise<void>) {
        const requestVersion = ++version
        calculation.value = null
        error.value = null
        loading.value = true
        try { await action(requestVersion) }
        catch (failure) { if (requestVersion === version) error.value = failure }
        finally { if (requestVersion === version) loading.value = false }
    }

    async function reload() {
        const { instanceID, stationSchemeID, routeID } = getScope()
        const preferredSetID = selectedSetID.value
        sets.value = []; selectedSetID.value = ''; selectedCaseID.value = ''
        await load(async requestVersion => {
            if (!instanceID || !stationSchemeID || !routeID) return
            const catalog = await loadTractionSets({ instanceID, stationSchemeID })
            if (requestVersion !== version) return
            sets.value = catalog.sets
            selectedSetID.value = catalog.sets.some(set => set.id === preferredSetID) ? preferredSetID : catalog.selectedId
            await readRoutes({ instanceID, stationSchemeID, calculationSetID: selectedSetID.value }, routeID, requestVersion)
        })
    }

    async function selectSet(id: string) {
        if (!sets.value.some(set => set.id === id)) return
        selectedSetID.value = id; selectedCaseID.value = ''
        const { instanceID, stationSchemeID, routeID } = getScope()
        await load(requestVersion => readRoutes({ instanceID, stationSchemeID, calculationSetID: id }, routeID, requestVersion))
    }

    watch(() => [getScope().instanceID, getScope().stationSchemeID, getScope().routeID], () => {
        selectedSetID.value = ''; selectedCaseID.value = ''
        void reload()
    }, { immediate: true, flush: 'sync' })
    onScopeDispose(() => { version++ })
    return { sets, selectedSetID, selectedCaseID, calculation, loading, error, reload, selectSet }
}
