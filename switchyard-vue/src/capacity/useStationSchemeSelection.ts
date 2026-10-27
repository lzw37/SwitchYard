import { computed, toValue, type MaybeRefOrGetter } from 'vue'
import { useCapacityStore } from '@/stores/capacity'

/** Share a selection across Capacity modules, independently for each instance. */
export function useStationSchemeSelection(instanceId: MaybeRefOrGetter<string | null | undefined>) {
    const capacityStore = useCapacityStore()
    return computed({
        get: () => capacityStore.stationSchemeId(toValue(instanceId)),
        set: (stationSchemeId: string) => capacityStore.selectStationScheme(toValue(instanceId), stationSchemeId),
    })
}
