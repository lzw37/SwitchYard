import { computed, toValue, type MaybeRefOrGetter } from 'vue'
import { useCapacityStore } from '@/stores/capacity'

/** Share a parameter set within an instance and station scheme across Capacity modules. */
export function useCalculationParameterSetSelection(
    instanceId: MaybeRefOrGetter<string | null | undefined>,
    stationSchemeId: MaybeRefOrGetter<string | null | undefined>,
) {
    const capacityStore = useCapacityStore()
    return computed({
        get: () => capacityStore.calculationParameterSetId(toValue(instanceId), toValue(stationSchemeId)),
        set: (parameterSetId: string) => capacityStore.selectCalculationParameterSet(toValue(instanceId), toValue(stationSchemeId), parameterSetId),
    })
}
