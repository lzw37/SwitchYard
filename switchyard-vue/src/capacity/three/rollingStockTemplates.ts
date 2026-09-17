import type * as THREE from 'three'
import {
    deserializeRollingStockModel, disposeRollingStockModel, rollingStockTemplateKey,
    type RollingStockBuildRequest, type RollingStockBuildResponse, type RollingStockTemplateSpec,
} from './rollingStockModelData.ts'

export type { RollingStockTemplateSpec } from './rollingStockModelData.ts'

export interface RollingStockWorker {
    onmessage: ((event: MessageEvent<RollingStockBuildResponse>) => void) | null
    onerror: ((event: ErrorEvent) => void) | null
    onmessageerror: ((event: MessageEvent) => void) | null
    postMessage(message: RollingStockBuildRequest): void
    terminate(): void
}

interface PendingTemplate {
    promise: Promise<void>
    resolve(): void
    reject(error: Error): void
}

/** One scene owns these templates; vehicle instances only own their transforms. */
export class RollingStockTemplates {
    private readonly workerFactory: () => RollingStockWorker
    private worker: RollingStockWorker | undefined
    private readonly templates = new Map<string, THREE.Group>()
    private readonly pending = new Map<string, PendingTemplate>()
    private disposed = false
    private failure: Error | undefined

    constructor(workerFactory: () => RollingStockWorker = () => new Worker(new URL('./rollingStock.worker.ts', import.meta.url), { type: 'module' })) {
        this.workerFactory = workerFactory
    }

    prepare(specs: readonly RollingStockTemplateSpec[]): Promise<void> {
        if (this.disposed) return Promise.reject(new Error('Rolling stock templates have been disposed'))
        if (this.failure) return Promise.reject(this.failure)
        return Promise.all(specs.map(spec => this.prepareOne(spec))).then(() => undefined)
    }

    instantiate(spec: RollingStockTemplateSpec): THREE.Group | undefined {
        if (this.disposed) return undefined
        // Three clones the hierarchy/userData and shares geometry/materials.
        // Bogie steering and train placement remain independent per instance.
        return this.templates.get(rollingStockTemplateKey(spec))?.clone(true)
    }

    dispose(): void {
        if (this.disposed) return
        this.disposed = true
        this.fail(new Error('Rolling stock template preparation was cancelled'))
        for (const template of this.templates.values()) disposeRollingStockModel(template)
        this.templates.clear()
    }

    private prepareOne(spec: RollingStockTemplateSpec): Promise<void> {
        if (this.failure) return Promise.reject(this.failure)
        const key = rollingStockTemplateKey(spec)
        if (this.templates.has(key)) return Promise.resolve()
        const existing = this.pending.get(key)
        if (existing) return existing.promise
        let resolve!: () => void
        let reject!: (error: Error) => void
        const promise = new Promise<void>((onResolve, onReject) => { resolve = onResolve; reject = onReject })
        this.pending.set(key, { promise, resolve, reject })
        try {
            if (!this.worker) {
                this.worker = this.workerFactory()
                this.worker.onmessage = event => this.receive(event.data)
                this.worker.onerror = event => this.fail(new Error(event.message || 'Rolling stock model worker failed'))
                this.worker.onmessageerror = () => this.fail(new Error('Rolling stock model worker response could not be decoded'))
            }
            this.worker.postMessage({ key, spec })
        } catch (error) {
            this.fail(error instanceof Error ? error : new Error(String(error)))
        }
        return promise
    }

    private receive(response: RollingStockBuildResponse): void {
        if (this.disposed || this.failure) return
        const pending = this.pending.get(response.key)
        if (!pending) return
        this.pending.delete(response.key)
        if ('error' in response) {
            pending.reject(new Error(response.error))
            return
        }
        try {
            this.templates.set(response.key, deserializeRollingStockModel(response.data))
            pending.resolve()
        } catch (error) {
            pending.reject(error instanceof Error ? error : new Error(String(error)))
        }
    }

    private fail(error: Error): void {
        this.failure = error
        if (this.worker) {
            this.worker.onmessage = null
            this.worker.onerror = null
            this.worker.onmessageerror = null
            this.worker.terminate()
            this.worker = undefined
        }
        for (const pending of this.pending.values()) pending.reject(error)
        this.pending.clear()
    }
}
