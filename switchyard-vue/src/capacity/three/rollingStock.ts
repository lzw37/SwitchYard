import { createEmuCar, EMU_DIMENSIONS, getEmuBogieOffsets, type EmuCarRole } from './emuTrain.ts'
import { EMU_MODEL_OPTIONS, EMU_MODEL_PROFILES, resolveEmuModel, type EmuModelId } from './emuProfiles.ts'
import { createLocomotive, getLocomotiveBogieOffsets } from './locomotive.ts'
import { LOCOMOTIVE_MODEL_OPTIONS, LOCOMOTIVE_PROFILES, resolveLocomotiveModel, type LocomotiveModelId } from './locomotiveProfiles.ts'

export type RollingStockModelId = EmuModelId | LocomotiveModelId
export type RollingStockCarRole = EmuCarRole

export const ROLLING_STOCK_OPTIONS = [...EMU_MODEL_OPTIONS, ...LOCOMOTIVE_MODEL_OPTIONS]

export function isLocomotiveModelId(modelId: RollingStockModelId): modelId is LocomotiveModelId {
    return Object.prototype.hasOwnProperty.call(LOCOMOTIVE_PROFILES, modelId)
}

/** Imported business train types stay unchanged; resolution affects the 3D model only. */
export function resolveRollingStockModel(value: unknown): RollingStockModelId {
    return resolveLocomotiveModel(value) ?? resolveEmuModel(value)
}

export function getRollingStockProfile(modelId: RollingStockModelId) {
    return isLocomotiveModelId(modelId)
        ? { ...LOCOMOTIVE_PROFILES[modelId], kind: 'locomotive' as const }
        : { ...EMU_MODEL_PROFILES[modelId], kind: 'emu' as const }
}

export function getRollingStockDimensions(modelId: RollingStockModelId) {
    return isLocomotiveModelId(modelId) ? LOCOMOTIVE_PROFILES[modelId].dimensions : EMU_DIMENSIONS
}

/** Both builders return normalized local X/Y/Z geometry with steerable bogie pivots. */
export function createRollingStockCar(role: RollingStockCarRole, carIndex: number, modelId: RollingStockModelId) {
    return isLocomotiveModelId(modelId)
        ? createLocomotive(modelId, role, carIndex)
        : createEmuCar(role, carIndex, modelId)
}

export function getRollingStockBogieOffsets(role: RollingStockCarRole, modelId: RollingStockModelId): [number, number] {
    if (!isLocomotiveModelId(modelId)) return getEmuBogieOffsets(role, modelId)
    const [rear, front] = getLocomotiveBogieOffsets(modelId)
    return role === 'tail' ? [-front, -rear] : [rear, front]
}
