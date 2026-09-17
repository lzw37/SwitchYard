import { createRollingStockCar } from './rollingStock.ts'
import {
    disposeRollingStockModel, serializeRollingStockModel,
    type RollingStockBuildRequest, type RollingStockBuildResponse,
} from './rollingStockModelData.ts'

// Use a narrow worker scope so this module can share the application's DOM tsconfig.
const workerScope = globalThis as unknown as {
    onmessage: (event: MessageEvent<RollingStockBuildRequest>) => void
    postMessage(message: RollingStockBuildResponse, transfer?: Transferable[]): void
}

workerScope.onmessage = ({ data: { key, spec } }) => {
    // Worker messages execute serially. Heavy geometry construction never runs
    // on the UI thread, including a model's first arrival in the station.
    let model: ReturnType<typeof createRollingStockCar> | undefined
    try {
        model = createRollingStockCar(spec.role, spec.carIndex, spec.modelId)
        const { data, transfer } = serializeRollingStockModel(model)
        workerScope.postMessage({ key, data }, transfer)
    } catch (error) {
        workerScope.postMessage({ key, error: error instanceof Error ? error.message : String(error) })
    } finally {
        if (model) disposeRollingStockModel(model)
    }
}
