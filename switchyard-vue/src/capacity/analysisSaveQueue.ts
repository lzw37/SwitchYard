/** Coalesce derived analysis snapshots per plan and never send overlapping writes. */
export class AnalysisSaveQueue<T> {
    private pending = new Map<string, T>()
    private timer: ReturnType<typeof setTimeout> | undefined
    private running: Promise<void> | undefined
    constructor(private readonly save: (value: T) => Promise<void>, private readonly onError: (error: unknown) => void) {}

    schedule(key: string, value: T, delay = 800) {
        this.pending.set(key, value)
        if (this.timer !== undefined) clearTimeout(this.timer)
        this.timer = setTimeout(() => { this.timer = undefined; void this.flush() }, delay)
    }
    cancelPending(key: string) { this.pending.delete(key) }
    flush(): Promise<void> {
        if (this.timer !== undefined) { clearTimeout(this.timer); this.timer = undefined }
        if (this.running) return this.running.then(() => this.flush())
        if (this.pending.size === 0) return Promise.resolve()
        this.running = this.drain().finally(() => { this.running = undefined })
        return this.running
    }
    private async drain() {
        while (this.pending.size > 0) {
            const [key, value] = this.pending.entries().next().value!
            this.pending.delete(key)
            try { await this.save(value) } catch (error) { this.onError(error) }
        }
    }
}
