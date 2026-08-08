<template>
    <section class="model-solving">
        <div class="toolbar">
            <div class="toolbar-field">
                <span class="field-label">{{ t('modelSolving.fields.stationScheme') }}</span>
                <el-select
                    v-model="stationSchemeId"
                    :loading="loadingSchemes"
                    :placeholder="t('modelSolving.placeholders.stationScheme')"
                    filterable
                >
                    <el-option
                        v-for="scheme in stationSchemes"
                        :key="scheme.id"
                        :label="scheme.name || scheme.id"
                        :value="scheme.id"
                    />
                </el-select>
            </div>
            <div class="toolbar-field">
                <span class="field-label">{{ t('modelSolving.fields.operationPlan') }}</span>
                <el-select
                    v-model="operationPlanId"
                    :loading="loadingPlans"
                    :disabled="!stationSchemeId"
                    :placeholder="t('modelSolving.placeholders.operationPlan')"
                    filterable
                >
                    <el-option
                        v-for="plan in operationPlans"
                        :key="plan.operationPlanID"
                        :label="plan.name || plan.operationPlanID"
                        :value="plan.operationPlanID"
                    />
                </el-select>
            </div>
            <div class="toolbar-actions">
                <el-button
                    :loading="generatingInput"
                    :disabled="!canGenerateInput"
                    @click="generateInput"
                >
                    {{ t('modelSolving.actions.generateInput') }}
                </el-button>
                <el-button :loading="loadingAgents" @click="loadAgents(false)">
                    {{ t('modelSolving.actions.refreshConnections') }}
                </el-button>
            </div>
        </div>

        <div class="workspace-grid">
            <el-card class="panel connection-panel" shadow="never">
                <template #header>
                    <div class="panel-header">
                        <span>{{ t('modelSolving.sections.connections') }}</span>
                        <el-tag :type="connectedAgents.length > 0 ? 'success' : 'info'" size="small">
                            {{ t('modelSolving.connection.onlineCount', { count: connectedAgents.length }) }}
                        </el-tag>
                    </div>
                </template>

                <el-table
                    :data="agents"
                    row-key="agentId"
                    size="small"
                    highlight-current-row
                    :empty-text="t('modelSolving.empty.noAgents')"
                    @current-change="selectAgent"
                >
                    <el-table-column :label="t('modelSolving.fields.status')" width="88">
                        <template #default="scope">
                            <el-tag :type="agentTagType(scope.row)" size="small">
                                {{ agentStatusText(scope.row) }}
                            </el-tag>
                        </template>
                    </el-table-column>
                    <el-table-column prop="name" :label="t('modelSolving.fields.agent')" min-width="135" />
                    <el-table-column prop="username" :label="t('modelSolving.fields.account')" min-width="105" />
                    <el-table-column :label="t('modelSolving.fields.models')" min-width="160">
                        <template #default="scope">
                            {{ agentModelNames(scope.row) }}
                        </template>
                    </el-table-column>
                </el-table>

                <div class="selection-row">
                    <div class="toolbar-field grow">
                        <span class="field-label">{{ t('modelSolving.fields.selectedAgent') }}</span>
                        <el-select
                            v-model="selectedAgentId"
                            :placeholder="t('modelSolving.placeholders.agent')"
                            @change="syncSelectedModel"
                        >
                            <el-option
                                v-for="agent in connectedAgents"
                                :key="agent.agentId"
                                :label="agentOptionLabel(agent)"
                                :value="agent.agentId"
                                :disabled="agent.isBusy"
                            />
                        </el-select>
                    </div>
                    <div class="toolbar-field grow">
                        <span class="field-label">{{ t('modelSolving.fields.model') }}</span>
                        <el-select
                            v-model="selectedModelId"
                            :disabled="availableModels.length === 0"
                            :placeholder="t('modelSolving.placeholders.model')"
                        >
                            <el-option
                                v-for="model in availableModels"
                                :key="model.id"
                                :label="`${model.name} (${model.version})`"
                                :value="model.id"
                            />
                        </el-select>
                    </div>
                </div>
            </el-card>

            <el-card class="panel job-panel" shadow="never">
                <template #header>
                    <div class="panel-header">
                        <span>{{ t('modelSolving.sections.job') }}</span>
                        <el-tag v-if="currentJob" :type="jobTagType(currentJob.status)" size="small">
                            {{ jobStatusText(currentJob.status) }}
                        </el-tag>
                    </div>
                </template>

                <div v-if="currentJob" class="job-summary">
                    <div class="job-id">{{ currentJob.jobId }}</div>
                    <el-progress
                        :percentage="currentJob.progress"
                        :status="currentJob.status === 'failed' ? 'exception' : currentJob.status === 'completed' ? 'success' : undefined"
                    />
                    <div class="job-message">{{ currentJob.error || currentJob.progressMessage }}</div>
                    <div class="job-meta">
                        <span>{{ currentJob.agentName }}</span>
                        <span>{{ formatDateTime(currentJob.submittedAt) }}</span>
                    </div>
                </div>
                <el-empty v-else :description="t('modelSolving.empty.noJob')" :image-size="62" />

                <div class="job-actions">
                    <el-button
                        type="primary"
                        :loading="submitting"
                        :disabled="!canSubmit"
                        @click="submitJob"
                    >
                        {{ t('modelSolving.actions.solve') }}
                    </el-button>
                    <el-button
                        :disabled="!currentJob"
                        :loading="pollingJob"
                        @click="refreshCurrentJob"
                    >
                        {{ t('modelSolving.actions.refreshJob') }}
                    </el-button>
                    <el-button
                        type="success"
                        plain
                        :disabled="!currentJob?.result"
                        @click="downloadResult"
                    >
                        {{ t('modelSolving.actions.downloadResult') }}
                    </el-button>
                </div>
            </el-card>

            <el-card class="panel json-panel" shadow="never">
                <template #header>
                    <div class="panel-header">
                        <span>{{ t('modelSolving.sections.input') }}</span>
                        <span class="panel-hint">{{ t('modelSolving.hints.editableJson') }}</span>
                    </div>
                </template>
                <el-input
                    v-model="inputJson"
                    type="textarea"
                    resize="none"
                    spellcheck="false"
                    :placeholder="t('modelSolving.placeholders.input')"
                />
            </el-card>

            <el-card class="panel json-panel result-panel" shadow="never">
                <template #header>
                    <div class="panel-header">
                        <span>{{ t('modelSolving.sections.result') }}</span>
                        <span v-if="resultSummary" class="panel-hint">{{ resultSummary }}</span>
                    </div>
                </template>
                <el-input
                    :model-value="resultJson"
                    type="textarea"
                    resize="none"
                    readonly
                    spellcheck="false"
                    :placeholder="t('modelSolving.placeholders.result')"
                />
            </el-card>
        </div>
    </section>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import { useI18n } from 'vue-i18n'
import axios from '@/utils/axios'

interface StationScheme {
    id: string
    name: string
}

interface OperationPlan {
    operationPlanID: string
    name: string
}

interface CapacityModel {
    id: string
    name: string
    version: string
    description: string
}

interface CapacityAgent {
    agentId: string
    name: string
    username: string
    isConnected: boolean
    isBusy: boolean
    connectedAt: string
    lastSeenAt: string
    models: CapacityModel[]
}

interface SolveJob {
    jobId: string
    agentId: string
    agentName: string
    modelId: string
    requestedBy: string
    status: 'queued' | 'running' | 'completed' | 'failed'
    progress: number
    progressMessage: string
    submittedAt: string
    startedAt?: string
    completedAt?: string
    error?: string
    result?: Record<string, unknown>
}

const props = defineProps<{ selectedInstanceId: string }>()
const { t } = useI18n()

const stationSchemes = ref<StationScheme[]>([])
const operationPlans = ref<OperationPlan[]>([])
const agents = ref<CapacityAgent[]>([])
const stationSchemeId = ref('')
const operationPlanId = ref('')
const selectedAgentId = ref('')
const selectedModelId = ref('')
const inputJson = ref('')
const currentJob = ref<SolveJob | null>(null)
const loadingSchemes = ref(false)
const loadingPlans = ref(false)
const loadingAgents = ref(false)
const generatingInput = ref(false)
const submitting = ref(false)
const pollingJob = ref(false)
let pollingTimer: number | undefined

const connectedAgents = computed(() => agents.value.filter((agent) => agent.isConnected))
const selectedAgent = computed(() =>
    agents.value.find((agent) => agent.agentId === selectedAgentId.value) || null,
)
const availableModels = computed(() => selectedAgent.value?.models || [])
const canGenerateInput = computed(() =>
    Boolean(props.selectedInstanceId && stationSchemeId.value && operationPlanId.value),
)
const canSubmit = computed(() => {
    const jobActive = currentJob.value?.status === 'queued' || currentJob.value?.status === 'running'
    return Boolean(
        selectedAgent.value?.isConnected &&
        !selectedAgent.value?.isBusy &&
        selectedModelId.value &&
        inputJson.value.trim() &&
        !jobActive,
    )
})
const resultJson = computed(() =>
    currentJob.value?.result ? JSON.stringify(currentJob.value.result, null, 2) : '',
)
const resultSummary = computed(() => {
    const result = currentJob.value?.result as any
    if (!result) return ''
    const status = result.status || '-'
    const trainCount = result.trainCount ?? '-'
    const seconds = Number(result.solveTimeSeconds)
    const solveTime = Number.isFinite(seconds) ? `${seconds.toFixed(2)}s` : '-'
    return `${status} · ${trainCount} ${t('modelSolving.units.trains')} · ${solveTime}`
})

async function loadStationSchemes() {
    const instanceID = props.selectedInstanceId
    stationSchemes.value = []
    stationSchemeId.value = ''
    clearPlansAndInput()
    if (!instanceID) return

    loadingSchemes.value = true
    try {
        const response = await axios.get('/StationLayout/GetStationSchemes', { params: { instanceID } })
        stationSchemes.value = (Array.isArray(response.data) ? response.data : [])
            .map((item: any) => ({ id: String(item.id || '').trim(), name: String(item.name || '').trim() }))
            .filter((item: StationScheme) => item.id)
        stationSchemeId.value = stationSchemes.value[0]?.id || ''
    } catch (error) {
        console.error(error)
        ElMessage.error(t('modelSolving.messages.loadSchemesFailed'))
    } finally {
        loadingSchemes.value = false
    }
}

async function loadOperationPlans() {
    operationPlans.value = []
    operationPlanId.value = ''
    inputJson.value = ''
    if (!props.selectedInstanceId || !stationSchemeId.value) return

    loadingPlans.value = true
    try {
        const response = await axios.get('/OperationPlan/GetOperationPlans', {
            params: {
                instanceID: props.selectedInstanceId,
                stationSchemeID: stationSchemeId.value,
            },
        })
        operationPlans.value = (Array.isArray(response.data) ? response.data : [])
            .map((item: any) => ({
                operationPlanID: String(item.operationPlanID || '').trim(),
                name: String(item.name || '').trim(),
            }))
            .filter((item: OperationPlan) => item.operationPlanID)
        operationPlanId.value = operationPlans.value.find((plan) => plan.operationPlanID === 'default')?.operationPlanID
            || operationPlans.value[0]?.operationPlanID
            || ''
    } catch (error) {
        console.error(error)
        ElMessage.error(t('modelSolving.messages.loadPlansFailed'))
    } finally {
        loadingPlans.value = false
    }
}

async function loadAgents(silent = true) {
    if (!silent) loadingAgents.value = true
    try {
        const response = await axios.get<CapacityAgent[]>('/api/capacity-agents')
        agents.value = Array.isArray(response.data) ? response.data : []
        if (!connectedAgents.value.some((agent) => agent.agentId === selectedAgentId.value && !agent.isBusy)) {
            selectedAgentId.value = connectedAgents.value.find((agent) => !agent.isBusy)?.agentId || ''
        }
        syncSelectedModel()
    } catch (error) {
        console.error(error)
        if (!silent) ElMessage.error(t('modelSolving.messages.loadAgentsFailed'))
    } finally {
        if (!silent) loadingAgents.value = false
    }
}

async function generateInput() {
    if (!canGenerateInput.value) return
    generatingInput.value = true
    try {
        const response = await axios.post('/api/capacity-agents/preview-input', {
            instanceId: props.selectedInstanceId,
            stationSchemeId: stationSchemeId.value,
            operationPlanId: operationPlanId.value,
        })
        inputJson.value = JSON.stringify(response.data, null, 2)
        ElMessage.success(t('modelSolving.messages.inputGenerated'))
    } catch (error: any) {
        console.error(error)
        ElMessage.error(apiErrorMessage(error, t('modelSolving.messages.generateInputFailed')))
    } finally {
        generatingInput.value = false
    }
}

async function submitJob() {
    if (!canSubmit.value) return
    let input: unknown
    try {
        input = JSON.parse(inputJson.value)
    } catch {
        ElMessage.error(t('modelSolving.messages.invalidJson'))
        return
    }

    submitting.value = true
    try {
        const response = await axios.post<SolveJob>('/api/capacity-agents/jobs', {
            agentId: selectedAgentId.value,
            modelId: selectedModelId.value,
            input,
        })
        currentJob.value = response.data
        ElMessage.success(t('modelSolving.messages.jobSubmitted'))
        await loadAgents(true)
    } catch (error: any) {
        console.error(error)
        ElMessage.error(apiErrorMessage(error, t('modelSolving.messages.submitFailed')))
    } finally {
        submitting.value = false
    }
}

async function refreshCurrentJob(silent = false) {
    if (!currentJob.value) return
    if (!silent) pollingJob.value = true
    try {
        const response = await axios.get<SolveJob>(`/api/capacity-agents/jobs/${currentJob.value.jobId}`)
        const previousStatus = currentJob.value.status
        currentJob.value = response.data
        if (previousStatus !== response.data.status && response.data.status === 'completed') {
            ElMessage.success(t('modelSolving.messages.jobCompleted'))
        } else if (previousStatus !== response.data.status && response.data.status === 'failed') {
            ElMessage.error(response.data.error || t('modelSolving.messages.jobFailed'))
        }
    } catch (error) {
        console.error(error)
        if (!silent) ElMessage.error(t('modelSolving.messages.refreshJobFailed'))
    } finally {
        if (!silent) pollingJob.value = false
    }
}

function downloadResult() {
    if (!currentJob.value?.result) return
    const content = JSON.stringify(currentJob.value.result, null, 2)
    const blob = new Blob([content], { type: 'application/json;charset=utf-8' })
    const url = URL.createObjectURL(blob)
    const anchor = document.createElement('a')
    anchor.href = url
    anchor.download = `capacity-result-${currentJob.value.jobId}.json`
    document.body.appendChild(anchor)
    anchor.click()
    anchor.remove()
    URL.revokeObjectURL(url)
}

function selectAgent(agent: CapacityAgent | undefined) {
    if (!agent?.isConnected || agent.isBusy) return
    selectedAgentId.value = agent.agentId
    syncSelectedModel()
}

function syncSelectedModel() {
    if (!availableModels.value.some((model) => model.id === selectedModelId.value)) {
        selectedModelId.value = availableModels.value[0]?.id || ''
    }
}

function clearPlansAndInput() {
    operationPlans.value = []
    operationPlanId.value = ''
    inputJson.value = ''
    currentJob.value = null
}

function agentStatusText(agent: CapacityAgent) {
    if (!agent.isConnected) return t('modelSolving.connection.offline')
    if (agent.isBusy) return t('modelSolving.connection.busy')
    return t('modelSolving.connection.online')
}

function agentTagType(agent: CapacityAgent) {
    if (!agent.isConnected) return 'info'
    return agent.isBusy ? 'warning' : 'success'
}

function agentOptionLabel(agent: CapacityAgent) {
    return agent.isBusy
        ? `${agent.name} · ${t('modelSolving.connection.busy')}`
        : agent.name
}

function agentModelNames(agent: CapacityAgent) {
    return agent.models.map((model) => model.name).join(', ')
}

function jobStatusText(status: SolveJob['status']) {
    return t(`modelSolving.jobs.${status}`)
}

function jobTagType(status: SolveJob['status']) {
    if (status === 'completed') return 'success'
    if (status === 'failed') return 'danger'
    if (status === 'running') return 'warning'
    return 'info'
}

function formatDateTime(value: string) {
    const date = new Date(value)
    return Number.isNaN(date.getTime()) ? value : date.toLocaleString()
}

function apiErrorMessage(error: any, fallback: string) {
    const data = error?.response?.data
    if (typeof data === 'string' && data.trim()) return data
    if (typeof data?.message === 'string' && data.message.trim()) return data.message
    return fallback
}

watch(() => props.selectedInstanceId, () => void loadStationSchemes(), { immediate: true })
watch(stationSchemeId, () => void loadOperationPlans())
watch(operationPlanId, () => { inputJson.value = '' })

onMounted(() => {
    void loadAgents(false)
    pollingTimer = window.setInterval(() => {
        void loadAgents(true)
        if (currentJob.value?.status === 'queued' || currentJob.value?.status === 'running') {
            void refreshCurrentJob(true)
        }
    }, 2000)
})

onBeforeUnmount(() => {
    if (pollingTimer !== undefined) window.clearInterval(pollingTimer)
})
</script>

<style scoped>
.model-solving {
    width: 100%;
    height: 100%;
    min-height: 0;
    padding: 12px;
    overflow: auto;
    border: 1px solid #d9e4ef;
    border-radius: 10px;
    background: #f5f8fc;
}

.toolbar,
.selection-row,
.panel-header,
.job-meta,
.job-actions {
    display: flex;
    align-items: center;
}

.toolbar {
    flex-wrap: wrap;
    gap: 12px;
    margin-bottom: 12px;
    padding: 12px;
    border: 1px solid #dbe6f1;
    border-radius: 8px;
    background: #fff;
}

.toolbar-field {
    display: flex;
    flex-direction: column;
    gap: 5px;
    min-width: 220px;
}

.toolbar-field.grow {
    flex: 1;
    min-width: 180px;
}

.field-label {
    color: #52667d;
    font-size: 12px;
    font-weight: 600;
}

.toolbar-actions {
    display: flex;
    align-items: flex-end;
    gap: 8px;
    min-height: 54px;
}

.workspace-grid {
    display: grid;
    grid-template-columns: minmax(0, 1.45fr) minmax(320px, 0.75fr);
    grid-template-rows: minmax(250px, auto) minmax(420px, 1fr);
    gap: 12px;
    min-height: calc(100% - 92px);
}

.panel {
    min-width: 0;
}

.panel :deep(.el-card__header) {
    padding: 12px 14px;
}

.panel :deep(.el-card__body) {
    padding: 12px 14px;
}

.panel-header {
    justify-content: space-between;
    gap: 12px;
    color: #1f3a56;
    font-weight: 700;
}

.panel-hint {
    overflow: hidden;
    color: #7a8ca0;
    font-size: 12px;
    font-weight: 400;
    text-overflow: ellipsis;
    white-space: nowrap;
}

.selection-row {
    gap: 12px;
    margin-top: 14px;
}

.job-panel :deep(.el-card__body) {
    display: flex;
    min-height: 190px;
    flex-direction: column;
}

.job-summary {
    display: flex;
    flex: 1;
    flex-direction: column;
    gap: 10px;
}

.job-id {
    overflow: hidden;
    color: #49627c;
    font-family: Consolas, monospace;
    font-size: 12px;
    text-overflow: ellipsis;
}

.job-message {
    min-height: 22px;
    color: #52667d;
    font-size: 13px;
}

.job-meta {
    justify-content: space-between;
    gap: 8px;
    color: #8291a3;
    font-size: 12px;
}

.job-actions {
    flex-wrap: wrap;
    gap: 8px;
    margin-top: 14px;
}

.job-actions :deep(.el-button + .el-button) {
    margin-left: 0;
}

.json-panel :deep(.el-card__body),
.json-panel :deep(.el-textarea),
.json-panel :deep(.el-textarea__inner) {
    height: 100%;
    min-height: 0;
}

.json-panel :deep(.el-card__body) {
    height: calc(100% - 53px);
}

.json-panel :deep(.el-textarea__inner) {
    padding: 12px;
    border: 0;
    box-shadow: none;
    background: #101827;
    color: #d7e3f4;
    font-family: Consolas, 'Courier New', monospace;
    font-size: 12px;
    line-height: 1.55;
}

.result-panel :deep(.el-textarea__inner) {
    background: #12201c;
    color: #d8efe6;
}

@media (max-width: 1100px) {
    .workspace-grid {
        grid-template-columns: 1fr;
        grid-template-rows: auto auto 420px 420px;
    }
}

@media (max-width: 700px) {
    .toolbar-field,
    .toolbar-actions {
        width: 100%;
    }

    .selection-row {
        align-items: stretch;
        flex-direction: column;
    }
}
</style>
