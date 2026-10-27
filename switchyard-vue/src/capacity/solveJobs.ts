export type SolveJobStatus = 'queued' | 'running' | 'cancelling' | 'cancelled' | 'completed' | 'failed'

export interface SolveJob {
    jobId: string
    agentId: string
    agentName: string
    modelId: string
    requestedBy: string
    status: SolveJobStatus
    progress: number
    progressMessage: string
    submittedAt: string
    startedAt?: string
    completedAt?: string
    error?: string
    result?: Record<string, unknown>
    instanceId?: string
    stationSchemeId?: string
    sourceOperationPlanId?: string
    resultOperationPlanId?: string
    resultOperationPlanName?: string
    retryOfJobId?: string
}

export function isSolveJobActive(status: SolveJobStatus) {
    return status === 'queued' || status === 'running' || status === 'cancelling'
}
