import type { ProcessScope, ProcessTemplate } from './operationProcess'

export interface ProcessPlanSource extends ProcessTemplate { trainCount: number }
export interface ProcessPlanGenerationItem { processTemplateID: string; revision: number; trainCount: number }

export function createProcessPlanSources(sources: ProcessTemplate[], previous: ProcessPlanSource[], scope: ProcessScope): ProcessPlanSource[] {
    const counts = new Map(previous.map(source => [source.id, source.trainCount]))
    return sources
        .filter(source => source.instanceID === scope.instanceID && source.stationSchemeID === scope.stationSchemeID)
        .map(source => ({ ...source, trainCount: counts.get(source.id) ?? 0 }))
}

export function buildProcessPlanBatch(sources: ProcessPlanSource[]): ProcessPlanGenerationItem[] | null {
    if (sources.some(source => !Number.isInteger(source.trainCount) || source.trainCount < 0 || source.trainCount > 1000)) return null
    const selected = sources.filter(source => source.trainCount > 0)
    const total = selected.reduce((count, source) => count + source.trainCount, 0)
    if (total < 1 || total > 1000) return null
    return selected.map(source => ({ processTemplateID: source.id, revision: source.revision, trainCount: source.trainCount }))
}
