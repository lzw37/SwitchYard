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
            <div class="toolbar-field preset-toolbar-field">
                <span class="field-label">{{ t('modelSolving.presets.fields.preset') }}</span>
                <el-select
                    v-model="selectedPresetId"
                    :loading="loadingPresets"
                    clearable
                    filterable
                    :placeholder="t('modelSolving.presets.placeholders.select')"
                    @change="applySelectedPreset"
                >
                    <el-option
                        v-for="preset in presets"
                        :key="preset.presetId"
                        :label="preset.name"
                        :value="preset.presetId"
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
                <el-button @click="openPresetManager">
                    {{ t('modelSolving.presets.actions.manage') }}
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
                            v-model="selectedAgentId"
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
                            :disabled="availableModels.length === 0"
                            :placeholder="t('modelSolving.placeholders.model')"
                            @change="selectedPresetId = ''"
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

        <el-dialog
            v-model="presetManagerVisible"
            :title="t('modelSolving.presets.title')"
            width="680px"
            :close-on-click-modal="false"
        >
            <div class="preset-dialog-toolbar">
                <el-select
                    v-model="editingPresetId"
                    clearable
                    filterable
                    :placeholder="t('modelSolving.presets.placeholders.newPreset')"
                    @change="loadPresetIntoForm"
                >
                    <el-option
                        v-for="preset in presets"
                        :key="preset.presetId"
                        :label="preset.name"
                        :value="preset.presetId"
                    />
                </el-select>
                <el-button @click="startNewPreset">{{ t('modelSolving.presets.actions.new') }}</el-button>
            </div>

            <el-form label-width="128px" class="preset-form">
                <el-form-item :label="t('modelSolving.presets.fields.name')" required>
                    <el-input v-model="presetForm.name" maxlength="100" show-word-limit />
                </el-form-item>
                <el-form-item :label="t('modelSolving.presets.fields.description')">
                    <el-input v-model="presetForm.description" maxlength="500" show-word-limit />
                </el-form-item>
                <el-form-item :label="t('modelSolving.fields.agent')" required>
                    <el-select
                        v-model="presetForm.agentId"
                        filterable
                        allow-create
                        default-first-option
                        :placeholder="t('modelSolving.placeholders.agent')"
                    >
                        <el-option
                            v-for="agent in agents"
                            :key="agent.agentId"
                            :label="agentPresetOptionLabel(agent)"
                            :value="agent.agentId"
                            :disabled="!agent.canUse"
                        />
                    </el-select>
                </el-form-item>
                <el-form-item :label="t('modelSolving.fields.model')" required>
                    <el-select
                        v-model="presetForm.modelId"
                        filterable
                        allow-create
                        default-first-option
                        :placeholder="t('modelSolving.placeholders.model')"
                    >
                        <el-option
                            v-for="model in presetModelOptions"
                            :key="model.id"
                            :label="`${model.name} (${model.version})`"
                            :value="model.id"
                        />
                    </el-select>
                </el-form-item>
                <el-divider>{{ t('modelSolving.presets.sections.settings') }}</el-divider>
                <div class="preset-settings-grid">
                    <el-form-item :label="t('modelSolving.presets.fields.leftTolerance')">
                        <el-input-number v-model="presetForm.settings.leftShiftToleranceSeconds" :min="0" :max="172800" :step="60" />
                    </el-form-item>
                    <el-form-item :label="t('modelSolving.presets.fields.rightTolerance')">
                        <el-input-number v-model="presetForm.settings.rightShiftToleranceSeconds" :min="0" :max="172800" :step="60" />
                    </el-form-item>
                    <el-form-item :label="t('modelSolving.presets.fields.timeLimit')">
                        <el-input-number v-model="presetForm.settings.timeLimitSeconds" :min="1" :max="86400" :step="60" />
                    </el-form-item>
                    <el-form-item :label="t('modelSolving.presets.fields.threadCount')">
                        <el-input-number v-model="presetForm.settings.threadCount" :min="1" :max="128" :step="1" />
                    </el-form-item>
                </div>
                <el-form-item :label="t('modelSolving.presets.fields.objective')">
                    <el-select v-model="presetForm.settings.objective">
                        <el-option :label="t('modelSolving.presets.objectives.minEndTime')" value="min-end-time" />
                        <el-option :label="t('modelSolving.presets.objectives.minDuration')" value="min-duration" />
                        <el-option :label="t('modelSolving.presets.objectives.minStartAndEnd')" value="min-start-and-end" />
                    </el-select>
                </el-form-item>
            </el-form>

            <template #footer>
                <div class="preset-dialog-footer">
                    <el-button
                        v-if="editingPresetId"
                        type="danger"
                        plain
                        :loading="savingPreset"
                        @click="deletePreset"
                    >
                        {{ t('modelSolving.presets.actions.delete') }}
                    </el-button>
                    <span class="preset-dialog-footer-spacer" />
                    <el-button @click="presetManagerVisible = false">{{ t('operationPlan.actions.cancel') }}</el-button>
                    <el-button type="primary" :loading="savingPreset" @click="savePreset">
                        {{ t('operationPlan.actions.save') }}
                    </el-button>
                </div>
            </template>
        </el-dialog>
    </section>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
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

interface SolveSettings {
    leftShiftToleranceSeconds: number
    rightShiftToleranceSeconds: number
    timeLimitSeconds: number
    threadCount: number
    objective: string
}

interface SolvePreset {
    presetId: string
    name: string
    description: string
    agentId: string
    modelId: string
    settings: SolveSettings
    createdAt?: string
    updatedAt?: string
}

interface SolvePresetForm {
    name: string
    description: string
    agentId: string
    modelId: string
    settings: SolveSettings
}

const props = defineProps<{ selectedInstanceId: string }>()
const { t } = useI18n()

const stationSchemes = ref<StationScheme[]>([])
const operationPlans = ref<OperationPlan[]>([])
const agents = ref<CapacityAgent[]>([])
const presets = ref<SolvePreset[]>([])
const stationSchemeId = ref('')
const operationPlanId = ref('')
const selectedAgentId = ref('')
const selectedModelId = ref('')
const selectedPresetId = ref('')
const editingPresetId = ref('')
const presetManagerVisible = ref(false)
const presetForm = ref<SolvePresetForm>(createEmptyPresetForm())
const inputJson = ref('')
const currentJob = ref<SolveJob | null>(null)
const loadingSchemes = ref(false)
const loadingPlans = ref(false)
const loadingAgents = ref(false)
const loadingPresets = ref(false)
const savingPreset = ref(false)
const generatingInput = ref(false)
const submitting = ref(false)
const pollingJob = ref(false)
let pollingTimer: number | undefined

const connectedAgents = computed(() => agents.value.filter((agent) => agent.isConnected))
const selectedAgent = computed(() =>
    agents.value.find((agent) => agent.agentId === selectedAgentId.value) || null,
)
const availableModels = computed(() => selectedAgent.value?.models || [])
const selectedPreset = computed(() =>
    presets.value.find((preset) => preset.presetId === selectedPresetId.value) || null,
)
const presetModelOptions = computed(() =>
    agents.value.find((agent) => agent.agentId === presetForm.value.agentId)?.models || [],
)
const canGenerateInput = computed(() =>
    Boolean(props.selectedInstanceId && stationSchemeId.value && operationPlanId.value),
)
const canSubmit = computed(() => {
    const jobActive = currentJob.value?.status === 'queued' || currentJob.value?.status === 'running'
    return Boolean(
        selectedAgent.value?.isConnected &&
        selectedAgent.value?.canUse &&
        selectedAgent.value?.isAvailable &&
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

function createDefaultSettings(): SolveSettings {
    return {
        leftShiftToleranceSeconds: 50400,
        rightShiftToleranceSeconds: 50400,
        timeLimitSeconds: 3600,
        threadCount: 12,
        objective: 'min-end-time',
    }
}

function createEmptyPresetForm(): SolvePresetForm {
    return {
        name: '',
        description: '',
        agentId: '',
        modelId: 'station-capacity-v1',
        settings: createDefaultSettings(),
    }
}

function normalizeSettings(value: any): SolveSettings {
    const defaults = createDefaultSettings()
    return {
        leftShiftToleranceSeconds: Number(value?.leftShiftToleranceSeconds ?? defaults.leftShiftToleranceSeconds),
        rightShiftToleranceSeconds: Number(value?.rightShiftToleranceSeconds ?? defaults.rightShiftToleranceSeconds),
        timeLimitSeconds: Number(value?.timeLimitSeconds ?? defaults.timeLimitSeconds),
        threadCount: Number(value?.threadCount ?? defaults.threadCount),
        objective: String(value?.objective || defaults.objective),
    }
}

function normalizePreset(value: any): SolvePreset | null {
    const presetId = String(value?.presetId || '').trim()
    if (!presetId) return null
    return {
        presetId,
        name: String(value?.name || '').trim(),
        description: String(value?.description || '').trim(),
        agentId: String(value?.agentId || '').trim(),
        modelId: String(value?.modelId || 'station-capacity-v1').trim(),
        settings: normalizeSettings(value?.settings),
        createdAt: value?.createdAt,
        updatedAt: value?.updatedAt,
    }
}

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
        agents.value = (Array.isArray(response.data) ? response.data : []).map(normalizeAgent)
        if (selectedPreset.value) {
            selectedAgentId.value = selectedPreset.value.agentId
            selectedModelId.value = selectedPreset.value.modelId
        } else if (!connectedAgents.value.some((agent) =>
            agent.agentId === selectedAgentId.value && agent.canUse && agent.isAvailable)) {
            selectedAgentId.value = connectedAgents.value.find((agent) => agent.canUse && agent.isAvailable)?.agentId || ''
            syncSelectedModel()
        } else {
            syncSelectedModel()
        }
    } catch (error) {
        console.error(error)
        if (!silent) ElMessage.error(t('modelSolving.messages.loadAgentsFailed'))
    } finally {
        if (!silent) loadingAgents.value = false
    }
}

async function loadPresets(selectPresetId = selectedPresetId.value) {
    loadingPresets.value = true
    try {
        const response = await axios.get<SolvePreset[]>('/api/capacity-agents/presets')
        presets.value = (Array.isArray(response.data) ? response.data : [])
            .map(normalizePreset)
            .filter((preset): preset is SolvePreset => preset !== null)
        selectedPresetId.value = presets.value.some((preset) => preset.presetId === selectPresetId)
            ? selectPresetId
            : (presets.value[0]?.presetId || '')
        applySelectedPreset()
    } catch (error) {
        console.error(error)
        ElMessage.error(apiErrorMessage(error, t('modelSolving.presets.messages.loadFailed')))
    } finally {
        loadingPresets.value = false
    }
}

function applySelectedPreset() {
    const preset = selectedPreset.value
    if (!preset) return
    selectedAgentId.value = preset.agentId
    selectedModelId.value = preset.modelId
    if (inputJson.value.trim()) {
        try {
            const input = JSON.parse(inputJson.value)
            input.settings = { ...preset.settings }
            inputJson.value = JSON.stringify(input, null, 2)
        } catch {
            // The editable JSON may temporarily be invalid; preserve the user's text.
        }
    }
}

function openPresetManager() {
    presetManagerVisible.value = true
    editingPresetId.value = selectedPresetId.value
    loadPresetIntoForm()
}

function startNewPreset() {
    editingPresetId.value = ''
    presetForm.value = {
        ...createEmptyPresetForm(),
        agentId: selectedAgentId.value,
        modelId: selectedModelId.value || 'station-capacity-v1',
    }
}

function loadPresetIntoForm() {
    const preset = presets.value.find((item) => item.presetId === editingPresetId.value)
    if (!preset) {
        startNewPreset()
        return
    }
    presetForm.value = {
        name: preset.name,
        description: preset.description,
        agentId: preset.agentId,
        modelId: preset.modelId,
        settings: { ...preset.settings },
    }
}

async function savePreset() {
    const form = presetForm.value
    if (!form.name.trim() || !form.agentId.trim() || !form.modelId.trim()) {
        ElMessage.warning(t('modelSolving.presets.messages.required'))
        return
    }

    savingPreset.value = true
    try {
        const payload = {
            presetId: editingPresetId.value || undefined,
            name: form.name.trim(),
            description: form.description.trim(),
            agentId: form.agentId.trim(),
            modelId: form.modelId.trim(),
            settings: { ...form.settings },
        }
        const response = editingPresetId.value
            ? await axios.put(`/api/capacity-agents/presets/${editingPresetId.value}`, payload)
            : await axios.post('/api/capacity-agents/presets', payload)
        const saved = normalizePreset(response.data)
        if (!saved) throw new Error('Invalid solve preset response')
        await loadPresets(saved.presetId)
        editingPresetId.value = saved.presetId
        loadPresetIntoForm()
        ElMessage.success(t('modelSolving.presets.messages.saved'))
    } catch (error) {
        console.error(error)
        ElMessage.error(apiErrorMessage(error, t('modelSolving.presets.messages.saveFailed')))
    } finally {
        savingPreset.value = false
    }
}

async function deletePreset() {
    const preset = presets.value.find((item) => item.presetId === editingPresetId.value)
    if (!preset) return
    try {
        await ElMessageBox.confirm(
            t('modelSolving.presets.messages.deleteConfirm', { name: preset.name }),
            t('modelSolving.presets.actions.delete'),
            { type: 'warning' },
        )
    } catch {
        return
    }

    savingPreset.value = true
    try {
        await axios.delete(`/api/capacity-agents/presets/${preset.presetId}`)
        const wasSelected = selectedPresetId.value === preset.presetId
        await loadPresets(wasSelected ? '' : selectedPresetId.value)
        startNewPreset()
        ElMessage.success(t('modelSolving.presets.messages.deleted'))
    } catch (error) {
        console.error(error)
        ElMessage.error(apiErrorMessage(error, t('modelSolving.presets.messages.deleteFailed')))
    } finally {
        savingPreset.value = false
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
        const generatedInput = response.data as Record<string, unknown>
        if (selectedPreset.value) {
            generatedInput.settings = { ...selectedPreset.value.settings }
        }
        inputJson.value = JSON.stringify(generatedInput, null, 2)
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
    if (!agent?.isConnected || !agent.canUse || !agent.isAvailable) return
    selectedPresetId.value = ''
    selectedAgentId.value = agent.agentId
    syncSelectedModel()
}

function handleAgentSelectionChange() {
    selectedPresetId.value = ''
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

function agentPresetOptionLabel(agent: CapacityAgent) {
    return agent.canUse
        ? `${agent.name} (${agent.agentId})`
        : `${agent.name} (${agent.agentId}) · ${t('modelSolving.connection.accessDenied')}`
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
    const message = typeof data?.message === 'string' ? data.message.trim() : ''
    const detail = typeof data?.detail === 'string' ? data.detail.trim() : ''
    if (message && detail) return `${message} ${detail}`
    if (message) return message
    if (detail) return detail
    return fallback
}

watch(() => props.selectedInstanceId, () => void loadStationSchemes(), { immediate: true })
watch(stationSchemeId, () => void loadOperationPlans())
watch(operationPlanId, () => { inputJson.value = '' })

onMounted(() => {
    void loadAgents(false)
    void loadPresets()
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

.preset-toolbar-field {
    min-width: 240px;
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

.preset-dialog-toolbar,
.preset-dialog-footer {
    display: flex;
    align-items: center;
    gap: 10px;
}

.preset-dialog-toolbar {
    margin-bottom: 18px;
}

.preset-dialog-toolbar .el-select {
    flex: 1;
}

.preset-form :deep(.el-select),
.preset-form :deep(.el-input-number) {
    width: 100%;
}

.preset-settings-grid {
    display: grid;
    grid-template-columns: 1fr 1fr;
    column-gap: 14px;
}

.preset-dialog-footer-spacer {
    flex: 1;
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

.agent-resource-grid {
    display: grid;
    grid-template-columns: repeat(4, minmax(0, 1fr));
    gap: 8px;
    margin-top: 12px;
}

.resource-card {
    display: flex;
    min-width: 0;
    padding: 9px 10px;
    border: 1px solid #e1e9f2;
    border-radius: 7px;
    background: #f8fafc;
    flex-direction: column;
    gap: 5px;
}

.resource-card > span,
.resource-card > small {
    overflow: hidden;
    color: #7a8ca0;
    font-size: 11px;
    text-overflow: ellipsis;
    white-space: nowrap;
}

.resource-card > strong {
    overflow: hidden;
    color: #29445f;
    font-size: 13px;
    text-overflow: ellipsis;
    white-space: nowrap;
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

    .preset-settings-grid {
        grid-template-columns: 1fr;
    }

    .agent-resource-grid {
        grid-template-columns: 1fr 1fr;
    }
}
</style>
