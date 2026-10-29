import { onScopeDispose, ref, watch } from 'vue'
import type { SolveJob } from './solveJobs'

interface JobPageQuery {
    instanceId: string
    status: string
    offset: number
    limit: number
}

interface JobListOptions {
    instanceId: () => string
    selectedJobId: () => string
    autoSelect?: () => boolean
    loadPage: (query: JobPageQuery) => Promise<{ items: SolveJob[]; total: number }>
    onSelect: (job: SolveJob) => void
    loadErrorMessage: () => string
}

export function useSolveJobList(options: JobListOptions) {
    const jobs = ref<SolveJob[]>([])
    const status = ref('')
    const page = ref(1)
    const total = ref(0)
    const loading = ref(false)
    const loadError = ref('')
    let version = 0
    let disposed = false

    async function refresh(silent = true) {
        if (disposed || (silent && loading.value)) return
        const instanceId = options.instanceId().trim()
        if (!instanceId) return
        const requestVersion = ++version
        loading.value = true
        try {
            const response = await options.loadPage({ instanceId, status: status.value, offset: (page.value - 1) * 10, limit: 10 })
            if (disposed || requestVersion !== version || instanceId !== options.instanceId().trim()) return
            jobs.value = response.items
            total.value = response.total
            loadError.value = ''
            if (page.value > Math.max(1, Math.ceil(total.value / 10))) page.value = Math.max(1, Math.ceil(total.value / 10))
            if ((options.autoSelect?.() ?? true) && !options.selectedJobId() && jobs.value[0]) options.onSelect(jobs.value[0])
        } catch {
            if (!disposed && requestVersion === version) loadError.value = options.loadErrorMessage()
        } finally {
            if (requestVersion === version) loading.value = false
        }
    }

    watch([options.instanceId, status], () => {
        version++
        jobs.value = []
        total.value = 0
        loading.value = false
        loadError.value = ''
        if (page.value !== 1) page.value = 1
        else void refresh(false)
    }, { immediate: true, flush: 'sync' })
    watch(page, () => void refresh(false))
    onScopeDispose(() => { disposed = true; version++ })

    return { jobs, status, page, total, loading, loadError, refresh }
}
