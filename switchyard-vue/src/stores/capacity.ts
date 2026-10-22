import { defineStore } from 'pinia'

export interface RouteOccupancyUpdate {
    instanceID: string
    stationSchemeID: string
    calculationParameterSetID: string
    routeIDs: string[]
    revision: number
}

export const useCapacityStore = defineStore('capacity', {
    state: () => ({
        stationSchemeIdsByInstance: {} as Record<string, string>,
        calculationParameterSetIdsByScope: {} as Record<string, Record<string, string>>,
        routeOccupancyUpdate: null as RouteOccupancyUpdate | null,
    }),
    getters: {
        stationSchemeId: (state) => (instanceId: string | null | undefined): string => {
            const id = instanceId?.trim() || ''
            return id ? state.stationSchemeIdsByInstance[id] || '' : ''
        },
        calculationParameterSetId: (state) => (instanceId: string | null | undefined, stationSchemeId: string | null | undefined): string => {
            const instance = instanceId?.trim() || ''
            const scheme = stationSchemeId?.trim() || ''
            return instance && scheme ? state.calculationParameterSetIdsByScope[instance]?.[scheme] || '' : ''
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
        selectCalculationParameterSet(instanceId: string | null | undefined, stationSchemeId: string | null | undefined, calculationParameterSetId: string) {
            const instance = instanceId?.trim()
            const scheme = stationSchemeId?.trim()
            const parameterSet = calculationParameterSetId.trim()
            // Empty values emitted during loading must not erase another module's selection.
            if (!instance || !scheme || !parameterSet) return
            const selections = this.calculationParameterSetIdsByScope[instance] ??= {}
            selections[scheme] = parameterSet
        },
        clearCalculationParameterSet(instanceId: string | null | undefined, stationSchemeId: string | null | undefined) {
            const instance = instanceId?.trim()
            const scheme = stationSchemeId?.trim()
            if (instance && scheme && this.calculationParameterSetIdsByScope[instance]) {
                delete this.calculationParameterSetIdsByScope[instance][scheme]
            }
        },
        notifyRouteOccupancyUpdated(update: Omit<RouteOccupancyUpdate, 'revision'>) {
            const instanceID = update.instanceID.trim()
            const stationSchemeID = update.stationSchemeID.trim()
            const calculationParameterSetID = update.calculationParameterSetID.trim()
            const routeIDs = [...new Set(update.routeIDs.map(id => id.trim()).filter(Boolean))]
            if (!instanceID || !stationSchemeID || !calculationParameterSetID || !routeIDs.length) return
            this.selectCalculationParameterSet(instanceID, stationSchemeID, calculationParameterSetID)
            this.routeOccupancyUpdate = { instanceID, stationSchemeID, calculationParameterSetID, routeIDs,
                revision: (this.routeOccupancyUpdate?.revision ?? 0) + 1 }
        },
    },
})
