<template>
    <section ref="workspaceElement" class="model-solving" :style="{ '--sidebar-width': sidebarWidth + 'px' }">
        <SolveJobManager ref="jobManager" :instance-id="selectedInstanceId" :selected-job-id="selectedJobId" :busy="managementBusy || submitting || creatingTask" :auto-select="!draftMode" @new="startNewTask" @select="selectJob" @cancel="manageJob('cancel', $event)" @delete="manageJob('delete', $event)" />
        <PaneDivider :model-value="sidebarWidth" :min="sidebarMin" :max="sidebarMax" :reset-value="defaultSidebarWidth" :label="t('modelSolving.workspace.resize')" @update:model-value="resizeSidebar" @reset="resetSidebar" />
        <main class="task-workspace">
            <el-card v-loading="pollingJob && !currentJob" class="configuration-card" shadow="never">
                <template #header><div class="section-heading"><div><span class="section-eyebrow">{{ t('modelSolving.workspace.configuration') }}</span><h3>{{ currentJob?.name || t('modelSolving.workspace.newTask') }}</h3></div><ActionButton :icon="Connection" :label="t('modelSolving.sections.connections')" @click="agentManagerVisible = true; loadAgents(false)" /></div></template>
                <div v-if="currentJob" class="task-name-field"><span class="field-label">{{ t('modelSolving.workspace.taskName') }}</span><el-input v-model="taskName" :disabled="formLocked" maxlength="100" :placeholder="currentJob.name" @change="persistDraftNow" /><span v-if="currentJob.status === 'draft'" class="draft-save-state" :class="{ 'has-error': draftSaveState === 'error' }">{{ t('modelSolving.workspace.saveState.' + draftSaveState) }}<el-button v-if="draftSaveState === 'error'" link type="danger" @click="persistDraftNow">{{ t('modelSolving.workspace.retrySave') }}</el-button></span></div>
                <div class="configuration-fields">            <div class="form-field">
                <span class="field-label">{{ t('modelSolving.fields.stationScheme') }}</span>
                <el-select
                    v-model="stationSchemeId" :disabled="formLocked || submitting || !selectedInstanceId" @change="draftMode = true"
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
            <div class="form-field">
                <span class="field-label">{{ t('modelSolving.fields.operationPlan') }}</span>
                <el-select
                    v-model="operationPlanId"
                    :loading="loadingPlans"
                    :disabled="formLocked || submitting || !stationSchemeId" @change="draftMode = true"
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

                    <div class="form-field"><span class="field-label">{{ t('modelSolving.fields.selectedAgent') }}</span><div class="agent-picker">
                        <el-select v-model="selectedAgentId" :disabled="formLocked || submitting" :placeholder="t('modelSolving.placeholders.agent')" @change="handleAgentSelectionChange" filterable>
                            <el-option v-for="agent in agents" :key="agent.agentId" :label="agent.name" :value="agent.agentId" :disabled="!agent.isConnected || !agent.canUse || !agent.isAvailable" />
                            <el-option v-if="selectedAgentId && !agents.some(a => a.agentId === selectedAgentId)" :label="currentJob?.agentName || selectedAgentId" :value="selectedAgentId" />
                        </el-select><el-tooltip :content="selectedAgentStatus"><span class="agent-lamp-box" role="status" :aria-label="selectedAgentStatus"><span class="agent-lamp" :class="selectedAgent ? 'is-' + agentTagType(selectedAgent) : 'is-info'" /></span></el-tooltip>
                    </div></div>
                    <div class="form-field"><span class="field-label">{{ t('modelSolving.fields.model') }}</span><el-select v-model="selectedModelId" :disabled="formLocked || !availableModels.length" :placeholder="t('modelSolving.placeholders.model')"><el-option v-for="model in availableModels" :key="model.id" :label="model.name + ' (' + model.version + ')'" :value="model.id" /><el-option v-if="selectedModelId && !availableModels.some(model => model.id === selectedModelId)" :label="selectedModelId" :value="selectedModelId" /></el-select></div>
                </div>
                <SolverParametersForm v-if="selectedModelId && currentJob" v-model="solverSettings" :solver-name="selectedModelName" :disabled="formLocked" />
                <div class="configuration-footer"><span class="configuration-hint">{{ !currentJob ? t('modelSolving.workspace.createFirst') : formLocked ? t('modelSolving.workspace.historyHint') : t('modelSolving.workspace.draftHint') }}</span><span v-if="selectedModelId" class="model-chip">{{ selectedModelName }}</span>
                    <ActionButton v-if="!formLocked" :icon="MagicStick" :label="t('modelSolving.actions.generateInput')" :loading="generatingInput" :disabled="!canGenerateInput" @click="generateInput" />
                    <ActionButton v-if="!formLocked" type="primary" variant="icon-text" :icon="VideoPlay" :label="t('modelSolving.actions.solve')" :loading="submitting" :disabled="!canSubmit" @click="submitJob" />
                </div>
            </el-card>
            <el-card class="job-information-card" shadow="never">
                <template #header><div class="section-heading"><div class="information-heading"><h3>{{ t('modelSolving.workspace.information') }}</h3><el-tag v-if="currentJob" :type="jobTagType(currentJob.status)" size="small">{{ jobStatusText(currentJob.status) }}</el-tag></div>
                    <div v-if="currentJob" class="job-actions"><ActionButton :icon="Refresh" :label="t('modelSolving.actions.refreshJob')" :loading="pollingJob" :disabled="managementBusy" @click="refreshCurrentJob()" />
                        <el-button v-if="isSolveJobActive(currentJob.status)" type="warning" plain size="small" :disabled="managementBusy || currentJob.status === 'cancelling'" @click="manageJob('cancel')">{{ t('modelSolving.manager.cancel') }}</el-button>
                        <template v-else-if="currentJob.status !== 'draft'"><el-button size="small" :disabled="managementBusy" @click="manageJob('retry')">{{ t('modelSolving.manager.retry') }}</el-button><el-button type="danger" plain size="small" :disabled="managementBusy" @click="manageJob('delete')">{{ t('modelSolving.manager.delete') }}</el-button></template><el-button v-if="currentJob.status === 'draft'" type="danger" plain size="small" :disabled="managementBusy || submitting" @click="manageJob('delete')">{{ t('modelSolving.manager.delete') }}</el-button>
                    </div></div></template>
                <template v-if="currentJob"><el-progress :percentage="currentJob.progress" :stroke-width="7" :status="currentJob.status === 'failed' ? 'exception' : currentJob.status === 'completed' ? 'success' : undefined" /><p class="job-progress-message">{{ currentJob.progressMessage }}</p>
                    <el-alert v-if="currentJob.error" :title="currentJob.error" type="error" :closable="false" show-icon />
                    <div class="job-metadata"><div><span>{{ t('modelSolving.manager.jobId') }}</span><strong class="job-id">{{ currentJob.jobId }}</strong></div><div><span>{{ t(currentJob.status === 'draft' ? 'modelSolving.workspace.createdAt' : 'modelSolving.manager.submittedAt') }}</span><strong>{{ formatDateTime(currentJob.status === 'draft' ? currentJob.createdAt : currentJob.submittedAt) }}</strong></div><div><span>{{ t('modelSolving.manager.completedAt') }}</span><strong>{{ currentJob.completedAt ? formatDateTime(currentJob.completedAt) : '—' }}</strong></div><div v-if="currentJob.resultOperationPlanId"><span>{{ t('modelSolving.manager.resultPlan') }}</span><strong>{{ currentJob.resultOperationPlanName || currentJob.resultOperationPlanId }}</strong></div></div>
                </template>
                <div v-else class="draft-information"><el-icon><Document /></el-icon><span>{{ selectedJobId ? t('modelSolving.workspace.loadingTask') : t('modelSolving.workspace.draftHint') }}</span></div>
            </el-card>
            <el-card class="data-card" shadow="never"><el-tabs v-model="activeDataTab" class="data-tabs">
                <el-tab-pane :label="t('modelSolving.sections.input')" name="request"><div class="data-heading"><span>{{ formLocked ? t('modelSolving.workspace.originalRequest') : t('modelSolving.hints.editableJson') }}</span><el-button v-if="!formLocked" size="small" :icon="Edit" :disabled="!selectedInstanceId || generatingInput || submitting" @click="openInputEditor">{{ t('modelSolving.workspace.editInput') }}</el-button><el-button v-else-if="requestLoadError" size="small" :icon="Refresh" @click="loadJobInput(selectedJobId)">{{ t('modelSolving.workspace.reloadInput') }}</el-button></div><el-alert v-if="requestLoadError" :title="requestLoadError" type="error" :closable="false" show-icon /><JsonViewer :source="formLocked ? jobInputJson : inputJson" :loading="loadingJobInput || generatingInput" :empty-text="t('modelSolving.placeholders.input')" /></el-tab-pane>
                <el-tab-pane :label="t('modelSolving.sections.result')" name="result"><div class="data-heading"><span>{{ resultSummary || t('modelSolving.placeholders.result') }}</span><ActionButton :icon="Download" :label="t('modelSolving.actions.downloadResult')" :disabled="!currentJob?.result" @click="downloadResult" /></div><JsonViewer :source="resultJson" :empty-text="t('modelSolving.placeholders.result')" /></el-tab-pane>
            </el-tabs></el-card>
        </main>
        <el-dialog v-model="agentManagerVisible" :title="t('modelSolving.sections.connections')" width="min(1120px, 94vw)"><div class="agent-dialog-heading"><el-tag :type="connectedAgents.length ? 'success' : 'info'">{{ t('modelSolving.connection.onlineCount', { count: connectedAgents.length }) }}</el-tag><ActionButton :icon="Refresh" :loading="loadingAgents" :label="t('modelSolving.actions.refreshConnections')" @click="loadAgents(false)" /></div>
                <el-table
                    :data="agents"
                    row-key="agentId"
                    size="small"
                    highlight-current-row
                    :empty-text="t('modelSolving.empty.noAgents')"
                    @current-change="selectAgent"
                >
                    <el-table-column :label="t('modelSolving.fields.status')" width="118">
                        <template #default="scope">
                            <el-tag :type="agentTagType(scope.row)" size="small">
                                {{ agentStatusText(scope.row) }}
                            </el-tag>
                        </template>
                    </el-table-column>
                    <el-table-column prop="name" :label="t('modelSolving.fields.agent')" min-width="135" />
                    <el-table-column prop="username" :label="t('modelSolving.fields.account')" min-width="105" />
                    <el-table-column :label="t('modelSolving.fields.workload')" width="105" align="center">
                        <template #default="scope">
                            {{ agentWorkload(scope.row) }}
                        </template>
                    </el-table-column>
                    <el-table-column :label="t('modelSolving.fields.cpu')" min-width="150">
                        <template #default="scope">
                            {{ agentCpuSummary(scope.row) }}
                        </template>
                    </el-table-column>
                    <el-table-column :label="t('modelSolving.fields.memory')" min-width="165">
                        <template #default="scope">
                            {{ agentMemorySummary(scope.row) }}
                        </template>
                    </el-table-column>
                    <el-table-column :label="t('modelSolving.fields.models')" min-width="150">
                        <template #default="scope">
                            {{ agentModelNames(scope.row) }}
                        </template>
                    </el-table-column>
                </el-table>

                <div class="selection-row">
                    <div class="toolbar-field grow">
                        <span class="field-label">{{ t('modelSolving.fields.selectedAgent') }}</span>
                        <el-select
                            v-model="selectedAgentId" :disabled="formLocked"
                            :placeholder="t('modelSolving.placeholders.agent')"
                            @change="handleAgentSelectionChange"
                        >
                            <el-option
                                v-for="agent in connectedAgents"
                                :key="agent.agentId"
                                :label="agentOptionLabel(agent)"
                                :value="agent.agentId"
                                :disabled="!agent.canUse || !agent.isAvailable"
                            />
                        </el-select>
                    </div>
                    <div class="toolbar-field grow">
                        <span class="field-label">{{ t('modelSolving.fields.model') }}</span>
                        <el-select
                            v-model="selectedModelId"
                            :disabled="formLocked || availableModels.length === 0"
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

                <div v-if="selectedAgent" class="agent-resource-grid">
                    <div class="resource-card">
                        <span>{{ t('modelSolving.resources.slots') }}</span>
                        <strong>{{ agentWorkload(selectedAgent) }}</strong>
                        <small>{{ t('modelSolving.resources.availableSlots', { count: selectedAgent.resources.availableJobSlots }) }}</small>
                    </div>
                    <div class="resource-card">
                        <span>{{ t('modelSolving.resources.cpuUsage') }}</span>
                        <strong>{{ formatPercent(selectedAgent.resources.cpuUsagePercent) }}</strong>
                        <el-progress :percentage="clampPercent(selectedAgent.resources.cpuUsagePercent)" :show-text="false" :stroke-width="6" />
                    </div>
                    <div class="resource-card">
                        <span>{{ t('modelSolving.resources.memoryUsage') }}</span>
                        <strong>{{ formatBytes(selectedAgent.resources.processWorkingSetBytes) }}</strong>
                        <el-progress :percentage="clampPercent(selectedAgent.resources.memoryUsagePercent)" :show-text="false" :stroke-width="6" />
                    </div>
                    <div class="resource-card">
                        <span>{{ t('modelSolving.resources.perJob') }}</span>
                        <strong>{{ selectedAgent.resources.cpuCoresPerJob }} CPU · {{ formatBytes(selectedAgent.resources.memoryLimitBytesPerJob) }}</strong>
                        <small>{{ t('modelSolving.resources.allocated', { cpu: selectedAgent.resources.allocatedCpuCores, memory: formatBytes(selectedAgent.resources.allocatedMemoryBytes) }) }}</small>
                    </div>
                </div>
        </el-dialog>
        <el-dialog v-model="inputEditorVisible" :title="t('modelSolving.workspace.editInput')" width="min(900px, 94vw)" :close-on-click-modal="false"><el-input v-model="editingInputJson" type="textarea" :rows="22" spellcheck="false" class="input-editor" /><template #footer><el-button @click="inputEditorVisible = false">{{ t('operationPlan.actions.cancel') }}</el-button><el-button type="primary" @click="saveInputEdit">{{ t('operationPlan.actions.save') }}</el-button></template></el-dialog>
    </section>
</template>

<script setup lang="ts">
import ActionButton from '@/components/ui/ActionButton.vue'
import PaneDivider from '@/components/ui/PaneDivider.vue'
import SolveJobManager from './components/SolveJobManager.vue'
import SolverParametersForm from './components/SolverParametersForm.vue'
import { applySolveSettings, normalizeSolveSettings } from './solveSettings'
import JsonViewer from '@/components/ui/JsonViewer.vue'
import { isSolveJobActive, type SolveJob } from './solveJobs'
import { SolveDraftSaveQueue } from './solveDraftSaveQueue'
import { Connection, Document, Download, Edit, MagicStick, Refresh, VideoPlay } from '@element-plus/icons-vue'
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useCapacityStore } from '@/stores/capacity'
import { useStationSchemeSelection } from './useStationSchemeSelection'
import { ElMessage, ElMessageBox } from 'element-plus'
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
    isAvailable: boolean
    canUse: boolean
    connectedAt: string
    lastSeenAt: string
    models: CapacityModel[]
    resources: CapacityAgentResources
}

interface CapacityAgentResources {
    maxConcurrentJobs: number
    activeJobCount: number
    availableJobSlots: number
    logicalCpuCores: number
    cpuCoresPerJob: number
    allocatedCpuCores: number
    cpuUsagePercent: number
    memoryLimitBytesPerJob: number
    allocatedMemoryBytes: number
    totalMemoryBytes: number
    memoryLoadBytes: number
    processWorkingSetBytes: number
    memoryUsagePercent: number
    collectedAt: string
}

const props = defineProps<{ selectedInstanceId: string }>()
const { t } = useI18n()

const stationSchemes = ref<StationScheme[]>([])
const operationPlans = ref<OperationPlan[]>([])
const agents = ref<CapacityAgent[]>([])
const capacityStore = useCapacityStore()
const stationSchemeId = useStationSchemeSelection(() => props.selectedInstanceId)
const operationPlanId = ref('')
const selectedAgentId = ref('')
const selectedModelId = ref('')
const solverSettings = ref(normalizeSolveSettings())
const inputJson = ref('')
const currentJob = ref<SolveJob | null>(null)
const selectedJobId = ref('')
const jobManager = ref<InstanceType<typeof SolveJobManager>>()
const managementBusy = ref(false)
const jobInputJson = ref('')
const requestLoadError = ref('')
const loadingJobInput = ref(false)
const inputEditorVisible = ref(false)
const editingInputJson = ref('')
const agentManagerVisible = ref(false)
const activeDataTab = ref('request')
const draftMode = ref(false)
const creatingTask = ref(false)
const draftReady = ref(false)
const taskName = ref('')
const saveTick = ref(0)
const formLocked = computed(() => !currentJob.value || currentJob.value.status !== 'draft' || !draftReady.value || creatingTask.value || submitting.value)
const workspaceElement = ref<HTMLElement>()
const workspaceWidth = ref(1280)
const sidebarWidth = ref(318)
const sidebarMin = computed(() => Math.min(240, Math.max(180, workspaceWidth.value / 4)))
const sidebarMax = computed(() => Math.max(sidebarMin.value, workspaceWidth.value - 440))
const defaultSidebarWidth = computed(() => Math.round(Math.min(sidebarMax.value, Math.max(sidebarMin.value, (workspaceWidth.value - 8) / 4))))
let sidebarWasResized = false
let resizeObserver: ResizeObserver | undefined
let inputLoadVersion = 0
let jobRequestVersion = 0
let instanceVersion = 0
let disposed = false
const loadingSchemes = ref(false)
const loadingPlans = ref(false)
const loadingAgents = ref(false)
const generatingInput = ref(false)
const submitting = ref(false)
const pollingJob = ref(false)
let pollingTimer: number | undefined
let schemeLoadVersion = 0
let planLoadVersion = 0

const connectedAgents = computed(() => agents.value.filter((agent) => agent.isConnected))
const selectedAgent = computed(() =>
    agents.value.find((agent) => agent.agentId === selectedAgentId.value) || null,
)
const availableModels = computed(() => selectedAgent.value?.models || [])
const selectedModelName = computed(() => availableModels.value.find(model => model.id === selectedModelId.value)?.name || selectedModelId.value)
const selectedAgentStatus = computed(() => selectedAgent.value ? agentStatusText(selectedAgent.value) : selectedAgentId.value ? t('modelSolving.connection.offline') : t('modelSolving.placeholders.agent'))
const draftSaveState = computed(() => { saveTick.value; return draftQueue.state(selectedJobId.value) })
const canGenerateInput = computed(() =>
    Boolean(!formLocked.value && !submitting.value && props.selectedInstanceId && stationSchemeId.value && operationPlanId.value),
)
const canSubmit = computed(() => {
    return Boolean(
        !formLocked.value && props.selectedInstanceId &&
        selectedAgent.value?.isConnected &&
        selectedAgent.value?.canUse &&
        selectedAgent.value?.isAvailable &&
        selectedModelId.value &&
        inputJson.value.trim() &&
        !submitting.value,
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

const draftQueue = new SolveDraftSaveQueue(
    async (id, value, revision) => (await axios.put<SolveJob>('/api/capacity-agents/jobs/' + encodeURIComponent(id) + '/draft', { ...value, revision })).data,
    () => { saveTick.value++ },
    (id, job) => {
        if (!disposed && selectedJobId.value === id && currentJob.value?.status === 'draft') {
            currentJob.value = job
            void jobManager.value?.refresh(false)
        }
    },
)
function scheduleDraftSave() {
    const job = currentJob.value
    if (!draftReady.value || job?.status !== 'draft' || job.instanceId !== props.selectedInstanceId) return
    draftQueue.schedule(job.jobId, {
        instanceId: job.instanceId, name: taskName.value,
        stationSchemeId: stationSchemeId.value, operationPlanId: operationPlanId.value,
        agentId: selectedAgentId.value, modelId: selectedModelId.value,
        settings: { ...solverSettings.value },
        input: inputJson.value.trim() ? JSON.parse(inputJson.value) : null,
    }, job.revision)
}
async function flushDraft(id = selectedJobId.value) { await draftQueue.flush(id) }
async function persistDraftNow() {
    try { await flushDraft() }
    catch (error) { ElMessage.error(apiErrorMessage(error, t('modelSolving.workspace.saveFailed'))) }
}
watch([taskName, stationSchemeId, operationPlanId, selectedAgentId, selectedModelId, inputJson, solverSettings], scheduleDraftSave, { flush: 'sync' })
watch(solverSettings, () => {
    if (draftReady.value && currentJob.value?.status === 'draft' && inputJson.value.trim()) {
        inputJson.value = JSON.stringify(applySolveSettings(JSON.parse(inputJson.value), solverSettings.value), null, 2)
    }
}, { flush: 'sync' })
watch(stationSchemeId, () => {
    if (draftReady.value && currentJob.value?.status === 'draft' && currentJob.value.instanceId === props.selectedInstanceId) {
        operationPlanId.value = ''; inputJson.value = ''
    }
}, { flush: 'sync' })

async function loadStationSchemes() {
    const instanceID = props.selectedInstanceId
    const requestedSchemeId = stationSchemeId.value
    const loadVersion = ++schemeLoadVersion
    stationSchemes.value = []
    loadingSchemes.value = false
    if (!instanceID) return

    loadingSchemes.value = true
    try {
        const response = await axios.get('/StationLayout/GetStationSchemes', { params: { instanceID } })
        if (loadVersion !== schemeLoadVersion || instanceID !== props.selectedInstanceId) return
        stationSchemes.value = (Array.isArray(response.data) ? response.data : [])
            .map((item: any) => ({ id: String(item.id || '').trim(), name: String(item.name || '').trim() }))
            .filter((item: StationScheme) => item.id)
        if (!formLocked.value && requestedSchemeId === stationSchemeId.value && !stationSchemes.value.some(scheme => scheme.id === stationSchemeId.value)) {
            const firstScheme = stationSchemes.value[0]?.id
            if (firstScheme) stationSchemeId.value = firstScheme
            else capacityStore.clearStationScheme(instanceID)
        }
    } catch (error) {
        if (loadVersion !== schemeLoadVersion || instanceID !== props.selectedInstanceId) return
        console.error(error)
        ElMessage.error(t('modelSolving.messages.loadSchemesFailed'))
    } finally {
        if (loadVersion === schemeLoadVersion) loadingSchemes.value = false
    }
}

async function loadOperationPlans() {
    const instanceID = props.selectedInstanceId
    const stationSchemeID = stationSchemeId.value
    const loadVersion = ++planLoadVersion
    clearPlansAndInput()
    loadingPlans.value = false
    if (!instanceID || !stationSchemeID) return

    loadingPlans.value = true
    try {
        const response = await axios.get('/OperationPlan/GetOperationPlans', {
            params: {
                instanceID,
                stationSchemeID,
            },
        })
        if (loadVersion !== planLoadVersion || instanceID !== props.selectedInstanceId || stationSchemeID !== stationSchemeId.value) return
        operationPlans.value = (Array.isArray(response.data) ? response.data : [])
            .map((item: any) => ({
                operationPlanID: String(item.operationPlanID || '').trim(),
                name: String(item.name || '').trim(),
            }))
            .filter((item: OperationPlan) => item.operationPlanID)
        operationPlanId.value = operationPlanId.value || operationPlans.value.find((plan) => plan.operationPlanID === 'default')?.operationPlanID
            || operationPlans.value[0]?.operationPlanID
            || ''
    } catch (error) {
        if (loadVersion !== planLoadVersion || instanceID !== props.selectedInstanceId || stationSchemeID !== stationSchemeId.value) return
        console.error(error)
        ElMessage.error(t('modelSolving.messages.loadPlansFailed'))
    } finally {
        if (loadVersion === planLoadVersion) loadingPlans.value = false
    }
}

async function loadAgents(silent = true) {
    if (!silent) loadingAgents.value = true
    try {
        const response = await axios.get<CapacityAgent[]>('/api/capacity-agents')
        agents.value = (Array.isArray(response.data) ? response.data : []).map(normalizeAgent)
        if (!selectedAgentId.value && !formLocked.value) {
            selectedAgentId.value = connectedAgents.value.find(agent => agent.canUse && agent.isAvailable)?.agentId || ''
        }
        syncSelectedModel()
    } catch (error) {
        console.error(error)
        if (!silent) ElMessage.error(t('modelSolving.messages.loadAgentsFailed'))
    } finally {
        if (!silent) loadingAgents.value = false
    }
}

function resizeSidebar(value: number) { sidebarWidth.value = value; sidebarWasResized = true }
function resetSidebar() { sidebarWasResized = false; sidebarWidth.value = defaultSidebarWidth.value }
function clearTaskSelection() {
    draftReady.value = false; taskName.value = ''; solverSettings.value = normalizeSolveSettings()
    jobRequestVersion++; inputLoadVersion++
    currentJob.value = null; selectedJobId.value = ''
    jobInputJson.value = ''; requestLoadError.value = ''
    pollingJob.value = false; loadingJobInput.value = false
}
async function startNewTask() {
    if (managementBusy.value || submitting.value || creatingTask.value || !props.selectedInstanceId) return
    const version = instanceVersion
    creatingTask.value = true
    try {
        await flushDraft()
        if (!isCurrentInstance(version)) return
        const { data } = await axios.post<SolveJob>('/api/capacity-agents/jobs/drafts', {
            instanceId: props.selectedInstanceId, stationSchemeId: stationSchemeId.value,
            operationPlanId: operationPlanId.value, agentId: selectedAgentId.value, modelId: selectedModelId.value,
        })
        if (!isCurrentInstance(version)) return
        clearTaskSelection(); inputJson.value = ''; currentJob.value = data; selectedJobId.value = data.jobId
        syncJobFields(data); draftReady.value = true; draftMode.value = true; activeDataTab.value = 'request'
        await jobManager.value?.revealJob()
    } catch (error) { if (isCurrentInstance(version)) ElMessage.error(apiErrorMessage(error, t('modelSolving.workspace.createFailed'))) }
    finally { creatingTask.value = false }
}
function syncJobFields(job: SolveJob) {
    solverSettings.value = normalizeSolveSettings(job.settings)
    taskName.value = job.name
    if (job.stationSchemeId) stationSchemeId.value = job.stationSchemeId
    operationPlanId.value = job.sourceOperationPlanId || ''
    selectedAgentId.value = job.agentId; selectedModelId.value = job.modelId
}
function openInputEditor() { editingInputJson.value = inputJson.value; inputEditorVisible.value = true; draftMode.value = true }
async function saveInputEdit() {
    let formatted: string
    try { formatted = JSON.stringify(applySolveSettings(JSON.parse(editingInputJson.value), solverSettings.value), null, 2) }
    catch { ElMessage.error(t('modelSolving.messages.invalidJson')); return }
    inputJson.value = formatted
    try { await flushDraft(); inputEditorVisible.value = false; activeDataTab.value = 'request' }
    catch (error) { ElMessage.error(apiErrorMessage(error, t('modelSolving.workspace.saveFailed'))) }
}
function isCurrentInstance(version: number) {
    return !disposed && version === instanceVersion
}

async function generateInput() {
    if (!canGenerateInput.value) return
    const instanceRequestVersion = instanceVersion
    const draftId = selectedJobId.value
    const scheme = stationSchemeId.value, plan = operationPlanId.value
    draftMode.value = true
    generatingInput.value = true
    try {
        const response = await axios.post('/api/capacity-agents/preview-input', {
            instanceId: props.selectedInstanceId,
            stationSchemeId: stationSchemeId.value,
            operationPlanId: operationPlanId.value,
        })
        if (!isCurrentInstance(instanceRequestVersion)) return
        const generatedInput = response.data as Record<string, unknown>
        if (formLocked.value || selectedJobId.value !== draftId || stationSchemeId.value !== scheme || operationPlanId.value !== plan) return
        inputJson.value = JSON.stringify(applySolveSettings(generatedInput, solverSettings.value), null, 2)
        activeDataTab.value = 'request'
        await flushDraft(draftId)
        if (!isCurrentInstance(instanceRequestVersion) || selectedJobId.value !== draftId) return
        ElMessage.success(t('modelSolving.messages.inputGenerated'))
    } catch (error: any) {
        if (!isCurrentInstance(instanceRequestVersion)) return
        console.error(error)
        ElMessage.error(apiErrorMessage(error, t('modelSolving.messages.generateInputFailed')))
    } finally {
        if (isCurrentInstance(instanceRequestVersion)) generatingInput.value = false
    }
}

async function submitJob() {
    if (!canSubmit.value || !currentJob.value || currentJob.value.status !== 'draft') return
    const id = selectedJobId.value
    const version = instanceVersion
    submitting.value = true
    try {
        await flushDraft(id)
        if (!isCurrentInstance(version) || selectedJobId.value !== id) return
        const { data } = await axios.post<SolveJob>('/api/capacity-agents/jobs/' + encodeURIComponent(id) + '/start', {
            revision: draftQueue.revision(id, currentJob.value.revision),
        })
        if (!isCurrentInstance(version) || selectedJobId.value !== id) return
        jobRequestVersion++
        draftReady.value = false; draftQueue.forget(id)
        currentJob.value = data; jobInputJson.value = inputJson.value
        syncJobFields(data); draftMode.value = false
        void jobManager.value?.revealJob(); void loadAgents(true)
        ElMessage.success(t('modelSolving.messages.jobSubmitted'))
    } catch (error) { if (isCurrentInstance(version)) ElMessage.error(apiErrorMessage(error, t('modelSolving.messages.submitFailed'))) }
    finally { submitting.value = false }
}

async function refreshCurrentJob(silent = false) {
    if (!selectedJobId.value || managementBusy.value || (silent && pollingJob.value)) return
    const jobId = selectedJobId.value
    const requestVersion = ++jobRequestVersion
    pollingJob.value = true
    try {
        const response = await axios.get<SolveJob>(`/api/capacity-agents/jobs/${encodeURIComponent(jobId)}`)
        if (disposed || requestVersion !== jobRequestVersion || selectedJobId.value !== jobId) return
        if (response.data.instanceId !== props.selectedInstanceId) return
        if (response.data.revision < (currentJob.value?.revision ?? 0)) return
        const previousStatus = currentJob.value?.status
        currentJob.value = response.data
        if (!previousStatus) syncJobFields(response.data)
        if (previousStatus && previousStatus !== response.data.status && response.data.status === 'completed') {
            ElMessage.success(t('modelSolving.messages.jobCompleted'))
        } else if (previousStatus && previousStatus !== response.data.status && response.data.status === 'failed') {
            ElMessage.error(response.data.error || t('modelSolving.messages.jobFailed'))
        }
    } catch (error: any) {
        if (disposed || requestVersion !== jobRequestVersion) return
        if (error?.response?.status === 404) {
            currentJob.value = null
            selectedJobId.value = ''
            void jobManager.value?.refresh(false)
        }
        console.error(error)
        if (!silent) ElMessage.error(t('modelSolving.messages.refreshJobFailed'))
    } finally {
        if (requestVersion === jobRequestVersion) pollingJob.value = false
    }
}

async function selectJob(jobId: string) {
    if (!props.selectedInstanceId || managementBusy.value || creatingTask.value || submitting.value) return
    const instance = instanceVersion
    try { await flushDraft() }
    catch (error) { ElMessage.error(apiErrorMessage(error, t('modelSolving.workspace.saveFailed'))); return }
    if (!isCurrentInstance(instance) || creatingTask.value || submitting.value) return
    clearTaskSelection(); draftQueue.forget(jobId)
    selectedJobId.value = jobId; draftMode.value = false; activeDataTab.value = 'request'
    await Promise.all([refreshCurrentJob(), loadJobInput(jobId)])
    if (!isCurrentInstance(instance) || selectedJobId.value !== jobId || requestLoadError.value || !currentJob.value) return
    if (currentJob.value.status === 'draft') {
        inputJson.value = jobInputJson.value === 'null' ? '' : jobInputJson.value
        draftReady.value = true; draftMode.value = true
    }
}

async function loadJobInput(jobId: string) {
    const version = ++inputLoadVersion
    const instanceRequestVersion = instanceVersion
    loadingJobInput.value = true; requestLoadError.value = ''
    try {
        const response = await axios.get('/api/capacity-agents/jobs/' + encodeURIComponent(jobId) + '/input')
        if (!isCurrentInstance(instanceRequestVersion) || version !== inputLoadVersion || selectedJobId.value !== jobId) return
        jobInputJson.value = JSON.stringify(response.data, null, 2)
    } catch (error) {
        if (isCurrentInstance(instanceRequestVersion) && version === inputLoadVersion && selectedJobId.value === jobId)
            requestLoadError.value = apiErrorMessage(error, t('modelSolving.messages.refreshJobFailed'))
    } finally { if (version === inputLoadVersion) loadingJobInput.value = false }
}

async function manageJob(action: 'cancel' | 'retry' | 'delete', job = currentJob.value) {
    if (!job || managementBusy.value || job.instanceId !== props.selectedInstanceId) return
    const instanceRequestVersion = instanceVersion
    managementBusy.value = true
    try {
        if (action !== 'retry') {
            try {
                await ElMessageBox.confirm(t(`modelSolving.manager.${action}Confirm`, { id: job.jobId }), t(`modelSolving.manager.${action}`), { type: 'warning' })
            } catch { return }
        }
        if (!isCurrentInstance(instanceRequestVersion)) return
        jobRequestVersion++
        pollingJob.value = false
        const url = `/api/capacity-agents/jobs/${encodeURIComponent(job.jobId)}`
        if (action === 'delete') {
            if (job.status === 'draft') await flushDraft(job.jobId).catch(() => {})
            await axios.delete(url)
            draftQueue.forget(job.jobId)
            if (!isCurrentInstance(instanceRequestVersion)) return
            if (selectedJobId.value === job.jobId) {
                clearTaskSelection()
            }
        } else {
            const response = await axios.post<SolveJob>(`${url}/${action}`)
            if (!isCurrentInstance(instanceRequestVersion)) return
            if (selectedJobId.value === job.jobId) {
                jobRequestVersion++
                selectedJobId.value = response.data.jobId
                currentJob.value = response.data
                syncJobFields(response.data)
                if (action === 'retry') void loadJobInput(response.data.jobId)
            }
        }
        ElMessage.success(t(`modelSolving.manager.${action}Success`))
    } catch (error) {
        if (!isCurrentInstance(instanceRequestVersion)) return
        ElMessage.error(apiErrorMessage(error, t('modelSolving.manager.actionFailed')))
    } finally {
        managementBusy.value = false
        if (!disposed) {
            void jobManager.value?.refresh(false)
            void loadAgents(true)
        }
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
    if (formLocked.value || !agent?.isConnected || !agent.canUse || !agent.isAvailable) return
    selectedAgentId.value = agent.agentId
    syncSelectedModel()
}

function handleAgentSelectionChange() {
    draftMode.value = true
    syncSelectedModel()
}

function syncSelectedModel() {
    if (!formLocked.value && !availableModels.value.some((model) => model.id === selectedModelId.value)) {
        selectedModelId.value = availableModels.value[0]?.id || ''
    }
}

function clearPlansAndInput() {
    operationPlans.value = []
}

function agentStatusText(agent: CapacityAgent) {
    if (!agent.isConnected) return t('modelSolving.connection.offline')
    if (!agent.canUse) return t('modelSolving.connection.accessDenied')
    if (!agent.isAvailable) return t('modelSolving.connection.full')
    if (agent.isBusy) {
        return t('modelSolving.connection.running', {
            active: agent.resources.activeJobCount,
            max: agent.resources.maxConcurrentJobs,
        })
    }
    return t('modelSolving.connection.online')
}

function agentTagType(agent: CapacityAgent) {
    if (!agent.isConnected) return 'info'
    if (!agent.canUse) return 'danger'
    if (!agent.isAvailable) return 'danger'
    return agent.isBusy ? 'warning' : 'success'
}

function agentOptionLabel(agent: CapacityAgent) {
    if (!agent.canUse) return `${agent.name} · ${t('modelSolving.connection.accessDenied')}`
    return `${agent.name} · ${t('modelSolving.resources.availableSlots', { count: agent.resources.availableJobSlots })}`
}

function agentModelNames(agent: CapacityAgent) {
    return agent.models.map((model) => model.name).join(', ')
}

function agentWorkload(agent: CapacityAgent) {
    return `${agent.resources.activeJobCount}/${agent.resources.maxConcurrentJobs}`
}

function agentCpuSummary(agent: CapacityAgent) {
    return `${formatPercent(agent.resources.cpuUsagePercent)} · ${agent.resources.allocatedCpuCores}/${agent.resources.logicalCpuCores} CPU`
}

function agentMemorySummary(agent: CapacityAgent) {
    return `${formatBytes(agent.resources.processWorkingSetBytes)} · ${formatPercent(agent.resources.memoryUsagePercent)}`
}

function normalizeAgent(value: CapacityAgent): CapacityAgent {
    const resources = value?.resources || {} as CapacityAgentResources
    const maxConcurrentJobs = Math.max(1, Number(resources.maxConcurrentJobs || 1))
    const activeJobCount = Math.max(0, Number(resources.activeJobCount || 0))
    return {
        ...value,
        canUse: Boolean(value?.canUse ?? true),
        isAvailable: Boolean(value?.isConnected && (value?.isAvailable ?? activeJobCount < maxConcurrentJobs)),
        resources: {
            maxConcurrentJobs,
            activeJobCount,
            availableJobSlots: Math.max(0, Number(resources.availableJobSlots ?? maxConcurrentJobs - activeJobCount)),
            logicalCpuCores: Math.max(1, Number(resources.logicalCpuCores || 1)),
            cpuCoresPerJob: Math.max(1, Number(resources.cpuCoresPerJob || 1)),
            allocatedCpuCores: Math.max(0, Number(resources.allocatedCpuCores || 0)),
            cpuUsagePercent: clampPercent(resources.cpuUsagePercent),
            memoryLimitBytesPerJob: Math.max(0, Number(resources.memoryLimitBytesPerJob || 0)),
            allocatedMemoryBytes: Math.max(0, Number(resources.allocatedMemoryBytes || 0)),
            totalMemoryBytes: Math.max(0, Number(resources.totalMemoryBytes || 0)),
            memoryLoadBytes: Math.max(0, Number(resources.memoryLoadBytes || 0)),
            processWorkingSetBytes: Math.max(0, Number(resources.processWorkingSetBytes || 0)),
            memoryUsagePercent: clampPercent(resources.memoryUsagePercent),
            collectedAt: String(resources.collectedAt || value.lastSeenAt || ''),
        },
    }
}

function clampPercent(value: unknown) {
    const number = Number(value)
    return Number.isFinite(number) ? Math.min(100, Math.max(0, Number(number.toFixed(1)))) : 0
}

function formatPercent(value: unknown) {
    return `${clampPercent(value).toFixed(1)}%`
}

function formatBytes(value: unknown) {
    const bytes = Math.max(0, Number(value || 0))
    if (!Number.isFinite(bytes) || bytes <= 0) return '0 MB'
    if (bytes >= 1024 ** 3) return `${(bytes / 1024 ** 3).toFixed(1)} GB`
    return `${(bytes / 1024 ** 2).toFixed(0)} MB`
}

function jobStatusText(status: SolveJob['status']) {
    return t(`modelSolving.jobs.${status}`)
}

function jobTagType(status: SolveJob['status']) {
    if (status === 'completed') return 'success'
    if (status === 'failed') return 'danger'
    if (isSolveJobActive(status)) return 'warning'
    return 'info'
}

function formatDateTime(value: string) {
    const date = new Date(value)
    return Number.isNaN(date.getTime()) ? value : date.toLocaleString()
}

function apiErrorMessage(error: any, fallback: string) {
    const data = error?.response?.data
    if (typeof data === 'string' && data.trim()) return data
    const message = typeof data?.message === 'string' ? data.message.trim() : ''
    const detail = typeof data?.detail === 'string' ? data.detail.trim() : ''
    if (typeof data?.error === 'string' && data.error.trim()) return data.error
    if (message && detail) return `${message} ${detail}`
    if (message) return message
    if (detail) return detail
    return fallback
}

watch(() => props.selectedInstanceId, () => {
    instanceVersion++
    jobRequestVersion++
    selectedJobId.value = ''
    currentJob.value = null
    inputJson.value = ''
    inputEditorVisible.value = false
    inputLoadVersion++; loadingJobInput.value = false; requestLoadError.value = ''
    draftMode.value = false; activeDataTab.value = 'request'
    jobInputJson.value = ''
    pollingJob.value = false
    generatingInput.value = false
    submitting.value = false
}, { flush: 'sync' })
watch(() => props.selectedInstanceId, () => void loadStationSchemes(), { immediate: true })
watch([() => props.selectedInstanceId, stationSchemeId], () => void loadOperationPlans(), { immediate: true })
watch(operationPlanId, () => { if (draftReady.value && currentJob.value?.status === 'draft' && currentJob.value.instanceId === props.selectedInstanceId) inputJson.value = '' }, { flush: 'sync' })

onMounted(() => {
    resizeObserver = new ResizeObserver(entries => {
        const width = entries[0]?.contentRect.width || 0
        if (!width) return
        workspaceWidth.value = width
        sidebarWidth.value = sidebarWasResized ? Math.min(sidebarMax.value, Math.max(sidebarMin.value, sidebarWidth.value)) : defaultSidebarWidth.value
    })
    if (workspaceElement.value) resizeObserver.observe(workspaceElement.value)
    void loadAgents(false)
    pollingTimer = window.setInterval(() => {
        void loadAgents(true)
        if (selectedJobId.value && (!currentJob.value || isSolveJobActive(currentJob.value.status))) {
            void refreshCurrentJob(true)
        }
    }, 2000)
})

onBeforeUnmount(() => {
    void draftQueue.flushAll().catch(() => {})
    disposed = true
    resizeObserver?.disconnect()
    inputLoadVersion++
    jobRequestVersion++
    schemeLoadVersion++
    planLoadVersion++
    if (pollingTimer !== undefined) window.clearInterval(pollingTimer)
})
</script>
<style scoped>
.model-solving { height: 100%; width: 100%; min-height: 0; display: grid; grid-template-columns: var(--sidebar-width) 8px minmax(0, 1fr); padding: 12px; box-sizing: border-box; overflow: hidden; background: #f5f7fa; }
.task-workspace { display: flex; flex-direction: column; gap: 12px; min-width: 0; min-height: 0; overflow: auto; padding-left: 10px; }
.configuration-card, .job-information-card { flex-shrink: 0; border-radius: 8px; }
.configuration-card :deep(.el-card__header), .job-information-card :deep(.el-card__header) { padding: 13px 18px; }
.configuration-card :deep(.el-card__body), .job-information-card :deep(.el-card__body) { padding: 16px 18px; }
.section-heading, .information-heading { display: flex; align-items: center; justify-content: space-between; gap: 12px; }
.section-heading { flex-wrap: wrap; }
h3 { font-size: 14px; font-weight: 600; color: #2b435b; margin: 0; }
.section-eyebrow { display: block; color: #97a4b4; font-size: 10px; letter-spacing: 1px; margin-bottom: 5px; }
.configuration-fields { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 16px; }
.form-field, .toolbar-field { display: flex; flex-direction: column; gap: 8px; min-width: 0; }
.field-label { font-size: 12px; color: #5a6e84; }
.agent-picker { display: flex; align-items: center; gap: 8px; }
.agent-picker .el-select { flex: 1; min-width: 0; }
.agent-lamp-box { display: flex; align-items: center; justify-content: center; flex: 0 0 30px; height: 30px; border: 1px solid #e6ebf1; border-radius: 6px; background: #fafbfd; }
.agent-lamp { width: 9px; height: 9px; border-radius: 50%; background: #a7b2c0; box-shadow: 0 0 0 3px #a7b2c017; }
.agent-lamp.is-success { background: #38a675; box-shadow: 0 0 0 3px #38a67520; }
.agent-lamp.is-warning { background: #e4a332; box-shadow: 0 0 0 3px #e4a33220; }
.agent-lamp.is-danger { background: #d86666; box-shadow: 0 0 0 3px #d8666620; }
.task-name-field { display: flex; align-items: center; gap: 12px; margin-bottom: 16px; flex-wrap: wrap; }
.task-name-field .el-input { max-width: 380px; }
.draft-save-state { color: #7f91a5; font-size: 11px; }
.draft-save-state.has-error { color: #df6464; }
.configuration-footer { display: flex; flex-wrap: wrap; align-items: center; gap: 10px; margin-top: 16px; }
.configuration-hint { flex: 1; min-width: 180px; color: #8a98aa; font-size: 11px; line-height: 1.7; }
.model-chip { color: #70839b; background: #f1f5f9; border-radius: 4px; padding: 4px 8px; font-size: 11px; max-width: 220px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.job-actions { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
.job-actions :deep(.el-button + .el-button) { margin-left: 0; }
.job-progress-message { color: #718298; font-size: 12px; margin: 8px 0 12px; }
.job-metadata { display: grid; grid-template-columns: 2fr 1fr 1fr; gap: 14px 22px; margin-top: 14px; }
.job-metadata > div { display: flex; flex-direction: column; min-width: 0; gap: 6px; }
.job-metadata span { color: #94a1b1; font-size: 11px; }
.job-metadata strong { color: #4d637a; font-size: 12px; font-weight: 400; overflow-wrap: anywhere; }
.job-metadata .job-id { font-family: ui-monospace, Consolas, monospace; }
.draft-information { display: flex; align-items: center; gap: 10px; color: #8b9aac; font-size: 12px; line-height: 1.8; }
.draft-information .el-icon { font-size: 22px; color: #a4b7cd; }
.data-card { display: flex; flex-direction: column; flex: 1 0 320px; min-height: 320px; border-radius: 8px; }
.data-card :deep(.el-card__body) { display: flex; flex-direction: column; flex: 1; min-height: 0; padding: 0; }
.data-tabs { display: flex; flex-direction: column; flex: 1; min-height: 0; }
.data-tabs :deep(.el-tabs__header) { margin: 0; flex-shrink: 0; padding: 0 18px; }
.data-tabs :deep(.el-tabs__item) { height: 46px; font-size: 13px; }
.data-tabs :deep(.el-tabs__nav-wrap::after) { height: 1px; }
.data-tabs :deep(.el-tabs__content) { flex: 1; min-height: 0; }
.data-tabs :deep(.el-tab-pane) { height: 100%; min-height: 0; display: flex; flex-direction: column; padding: 12px 16px 16px; box-sizing: border-box; }
.data-heading { display: flex; justify-content: space-between; align-items: center; gap: 12px; min-height: 28px; margin-bottom: 10px; color: #8b99ab; font-size: 11px; }
.agent-dialog-heading { display: flex; justify-content: space-between; gap: 12px; align-items: center; margin-bottom: 16px; }
.selection-row { display: flex; gap: 16px; margin-top: 18px; }
.toolbar-field.grow { flex: 1; }
.agent-resource-grid { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 12px; margin-top: 18px; }
.resource-card { display: flex; flex-direction: column; gap: 8px; border: 1px solid #e6edf5; border-radius: 7px; padding: 14px; background: #f8fafc; }
.resource-card span, .resource-card small { color: #8b9bad; font-size: 11px; }
.resource-card strong { color: #415c78; font-size: 13px; font-weight: 500; }
.input-editor :deep(textarea) { font: 12px/1.7 ui-monospace, Consolas, monospace; }
@media (max-width: 1100px) { .configuration-fields { grid-template-columns: 1fr 1fr; gap: 10px; } .job-metadata { grid-template-columns: 1fr 1fr; } .agent-resource-grid { grid-template-columns: 1fr 1fr; } }
@media (max-width: 760px) { .model-solving { display: flex; flex-direction: column; gap: 12px; overflow: auto; } .model-solving > .solve-job-manager { height: 320px; flex-shrink: 0; } .model-solving > .sy-pane-divider { display: none; } .task-workspace { flex-shrink: 0; overflow: visible; padding-left: 0; } .configuration-fields { grid-template-columns: 1fr; } .data-card { height: 420px; flex: none; } }
</style>
