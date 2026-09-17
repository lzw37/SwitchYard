import { getRollingStockProfile, resolveRollingStockModel, type RollingStockModelId } from './rollingStock.ts'

export type RollingStockModelSelection = 'auto' | RollingStockModelId
export type EmuModelSelection = RollingStockModelSelection

/** Visual selection only: never replace the train type used for route timing. */
export function getRollingStockConsistForRun(trainType: unknown, selection: RollingStockModelSelection = 'auto') {
    const modelId = resolveRollingStockModel(selection === 'auto' ? trainType : selection)
    return { modelId, carCount: getRollingStockProfile(modelId).carCount }
}

// Keep the existing import available to saved previews and EMU regression tests.
export const getEmuConsistForRun = getRollingStockConsistForRun
