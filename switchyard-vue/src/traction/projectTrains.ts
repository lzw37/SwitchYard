import axios from 'axios'
import { cloneTrainPresetCatalog, cloneTrainParameters, validateTrainPresetCatalog, type TrainPresetCatalog } from './trainPresets.ts'
import { copyWorkspaceContent, type TractionScope, type TractionWorkspaceContent, type TractionWorkspace } from './tractionWorkspace.ts'

export interface ProjectTrains { instanceID: string; revision: number; catalog: TrainPresetCatalog }
const endpoint = '/api/SpeedProfile/workspace/trains'
function normalize(value: ProjectTrains, instanceID: string): ProjectTrains {
    if (!value || value.instanceID !== instanceID || !Number.isSafeInteger(value.revision) || value.revision < 1 || !value.catalog) throw new Error('Invalid project train library.')
    const catalog = { ...value.catalog, presets: value.catalog.presets?.map(p => ({ ...p, nameEn: p.nameEn ?? undefined })) }
    if (validateTrainPresetCatalog(catalog)) throw new Error('Invalid project train parameters.')
    return { instanceID, revision: value.revision, catalog: cloneTrainPresetCatalog(catalog) }
}
export async function loadProjectTrains(scope: TractionScope): Promise<ProjectTrains | null> {
    const response = await axios.get<ProjectTrains>(endpoint, { params: { instanceID: scope.instanceID, stationSchemeID: scope.stationSchemeID }, timeout: 30_000 })
    return response.status === 204 ? null : normalize(response.data, scope.instanceID)
}
export async function saveProjectTrains(scope: TractionScope, revision: number, catalog: TrainPresetCatalog): Promise<ProjectTrains> {
    const snapshot = cloneTrainPresetCatalog(catalog)
    const { data } = await axios.put<ProjectTrains>(endpoint, { instanceID: scope.instanceID, stationSchemeID: scope.stationSchemeID, revision, catalog: snapshot }, { timeout: 60_000 })
    const saved = normalize(data, scope.instanceID)
    if (saved.revision !== revision + 1 || JSON.stringify(saved.catalog) !== JSON.stringify(snapshot)) throw new Error('Project train library save was not acknowledged.')
    return saved
}
/** A route owns its selection; the parameter definitions always come from its project. */
export function useProjectTrains<T extends TractionWorkspaceContent>(workspace: T, project: ProjectTrains): T {
    const catalog = cloneTrainPresetCatalog(project.catalog)
    if (catalog.presets.some(p => p.id === workspace.catalog.selectedId)) catalog.selectedId = workspace.catalog.selectedId
    return { ...workspace, ...copyWorkspaceContent(workspace), catalogRevision: project.revision, catalog }
}
/** Mirror the global save's selective invalidation without masking unrelated route edits. */
export function rebaseProjectTrainSource(workspace: TractionWorkspace, project: ProjectTrains): TractionWorkspace {
    const next = useProjectTrains(workspace, project)
    const before = workspace.catalog.presets.find(p => p.id === workspace.catalog.selectedId)!
    const after = next.catalog.presets.find(p => p.id === next.catalog.selectedId)!
    const changed = before.id !== after.id || JSON.stringify(cloneTrainParameters(before.parameters)) !== JSON.stringify(cloneTrainParameters(after.parameters))
    return { ...next, revision: workspace.revision + (changed ? 1 : 0) }
}
