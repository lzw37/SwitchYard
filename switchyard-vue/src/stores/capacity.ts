import { defineStore } from 'pinia'

export const useCapacityStore = defineStore('capacity', {
    state: () => ({
        stationSchemeIdsByInstance: {} as Record<string, string>,
    }),
    getters: {
        stationSchemeId: (state) => (instanceId: string | null | undefined): string => {
            const id = instanceId?.trim() || ''
            return id ? state.stationSchemeIdsByInstance[id] || '' : ''
        },
    },
    actions: {
        selectStationScheme(instanceId: string | null | undefined, stationSchemeId: string) {
            const id = instanceId?.trim()
            const schemeId = stationSchemeId.trim()
            // Loading and component resets must not erase another module's selection.
            if (id && schemeId) this.stationSchemeIdsByInstance[id] = schemeId
        },
        clearStationScheme(instanceId: string | null | undefined) {
            const id = instanceId?.trim()
            if (id) delete this.stationSchemeIdsByInstance[id]
        },
    },
})
