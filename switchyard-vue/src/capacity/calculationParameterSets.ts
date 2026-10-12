import { onScopeDispose, ref, watch } from 'vue'
import axios from 'axios'

export interface CalculationParameterSet { id: string; name: string; isDefault: boolean }

export function normalizeCalculationParameterSet(value: any): CalculationParameterSet {
    return { id: String(value.id ?? value.ID ?? ''), name: String(value.name ?? value.Name ?? ''),
        isDefault: Boolean(value.isDefault ?? value.IsDefault) }
}

export function useCalculationParameterSets(instanceID: () => string, stationSchemeID: () => string) {
    const sets = ref<CalculationParameterSet[]>([])
    const loading = ref(false)
    const error = ref('')
    let version = 0
    onScopeDispose(() => { version++ })
    async function reload() {
        const scope = { instanceID: instanceID(), stationSchemeID: stationSchemeID() }
        const current = ++version
        if (!scope.instanceID || !scope.stationSchemeID) { sets.value = []; loading.value = false; return }
        loading.value = true
        error.value = ''
        try {
            const response = await axios.get('/StationLayout/GetCalculationParameterSets', { params: scope })
            if (current !== version || scope.instanceID !== instanceID() || scope.stationSchemeID !== stationSchemeID()) return
            sets.value = response.data.map(normalizeCalculationParameterSet)
        } catch (reason: any) {
            if (current === version) { sets.value = []; error.value = String(reason.response?.data ?? reason.message) }
        } finally { if (current === version) loading.value = false }
    }
    watch([instanceID, stationSchemeID], () => { sets.value = []; void reload() }, { immediate: true })
    return { sets, loading, error, reload }
}
