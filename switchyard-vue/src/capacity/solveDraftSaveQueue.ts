import type { SolveJob } from './solveJobs'
import type { SolveSettings } from './solveSettings'

export interface SolveDraftValues {
    instanceId: string
    name: string
    stationSchemeId: string
    operationPlanId: string
    agentId: string
    modelId: string
    input: unknown
    settings: SolveSettings
}
interface Entry {
    revision: number
    version: number
    saved: number
    value: SolveDraftValues
    error?: unknown
    timer?: ReturnType<typeof setTimeout>
    running?: Promise<void>
}

// Each task writes serially; newer edits made during a request are saved next.
export class SolveDraftSaveQueue {
    private readonly entries = new Map<string, Entry>()
    constructor(
        private readonly save: (id: string, value: SolveDraftValues, revision: number) => Promise<SolveJob>,
        private readonly changed: () => void,
        private readonly saved: (id: string, job: SolveJob) => void,
    ) {}
    schedule(id: string, value: SolveDraftValues, revision: number, delay = 300) {
        let entry = this.entries.get(id)
        if (!entry) {
            entry = { revision, version: 0, saved: 0, value }
            this.entries.set(id, entry)
        }
        entry.value = value; entry.version++; entry.error = undefined
        if (entry.timer) clearTimeout(entry.timer)
        entry.timer = setTimeout(() => { entry!.timer = undefined; void this.flush(id).catch(() => {}) }, delay)
        this.changed()
    }
    state(id: string) {
        const entry = this.entries.get(id)
        return !entry ? 'saved' : entry.error ? 'error' : entry.running ? 'saving' : entry.version > entry.saved ? 'pending' : 'saved'
    }
    error(id: string) { return this.entries.get(id)?.error }
    revision(id: string, fallback: number) { return this.entries.get(id)?.revision ?? fallback }
    flush(id: string): Promise<void> {
        const entry = this.entries.get(id)
        if (!entry) return Promise.resolve()
        if (entry.timer) { clearTimeout(entry.timer); entry.timer = undefined }
        if (entry.running) return entry.running.then(() => this.flush(id))
        if (entry.saved === entry.version) return Promise.resolve()
        entry.error = undefined
        entry.running = this.drain(id, entry).finally(() => { entry.running = undefined; this.changed() })
        this.changed()
        return entry.running
    }
    private async drain(id: string, entry: Entry) {
        while (entry.saved < entry.version && this.entries.get(id) === entry) {
            const version = entry.version
            const value = entry.value
            try {
                const job = await this.save(id, value, entry.revision)
                entry.revision = job.revision
                entry.saved = version; entry.error = undefined
                if (this.entries.get(id) === entry) this.saved(id, job)
            } catch (error) { entry.error = error; throw error }
        }
    }
    async flushAll() { await Promise.all([...this.entries.keys()].map(id => this.flush(id))) }
    forget(id: string) {
        const entry = this.entries.get(id)
        if (entry?.timer) clearTimeout(entry.timer)
        this.entries.delete(id); this.changed()
    }
}