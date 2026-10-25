<script setup lang="ts">
import { computed, onBeforeUnmount, reactive, ref, shallowRef, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { copyWorkspaceContent, workspaceFingerprint, type TractionRouteCase, type TractionWorkspaceContent } from './tractionWorkspace'
import { tractionModeForRoute } from './tractionCalculation'
import { tractionBatchCandidates, loadTractionBatchJob, tractionBatchIssues, applyTractionBatchParameters, runTractionBatch, batchFailureMessage,
    type TractionBatchContext, type TractionBatchCandidate, type TractionBatchJob, type TractionBatchReport, type TractionBatchField } from './tractionBatch'
import TractionBatchParameters from './TractionBatchParameters.vue'
import { useProjectTrains, rebaseProjectTrainSource, type ProjectTrains } from './projectTrains'
import type { CalculationParameterSet } from '@/capacity/calculationParameterSets'
import type { TractionParameterTransferReport } from './tractionParameterTransfer'
const props = defineProps<{ modelValue: boolean; rows: TractionRouteCase[]; selectedId: string; setName: string;
    routes: { id: string; description: string }[]; busy: boolean; error: string; batchScopeKey?: string;
    prepareBatch?: () => Promise<TractionBatchContext>; parameterSetId?: string; parameterSets?: CalculationParameterSet[];
    parameterSetsLoading?: boolean; parameterSetError?: string;
    transferResults?: (selection: TractionRouteCase[]) => Promise<TractionParameterTransferReport> }>()
const emit = defineEmits<{ 'update:modelValue': [boolean]; open: [TractionRouteCase]; remove: [TractionRouteCase]; refresh: [];
    'batch-busy': [boolean]; 'batch-complete': [TractionBatchReport]; 'update:parameterSetId': [string]; 'reload-parameter-sets': [] }>()
const { locale } = useI18n()
const ui = (zh: string, en: string) => locale.value.startsWith('zh') ? zh : en
const label = (id: string) => { const route = props.routes.find(r => r.id === id); return route?.description ? id + ' · ' + route.description : id || '—' }
const dateLabel = (date: string | null) => date ? new Date(date).toLocaleString(locale.value.startsWith('zh') ? 'zh-CN' : 'en-GB', { hour12: false }) : '—'
const tab = ref('results'), search = ref(''), filter = ref('all'), batchError = ref('')
const context = shallowRef<TractionBatchContext | null>(null)
const initializing = ref(false), running = ref(false), stopRequested = ref(false)
const jobs = reactive<Record<string, TractionBatchJob>>({})
const selectedKeys = ref<string[]>([]), activeKey = ref(''), report = shallowRef<TractionBatchReport | null>(null)
const commonVisible = ref(false), commonWorkspace = ref<TractionWorkspaceContent | null>(null)
const commonFields = ref<TractionBatchField[]>(['train', 'signal'])
const resultSelectionIds = ref<string[]>([]), transferring = ref(false), transferError = ref('')
const transferTargetName = ref('')
const transferReport = shallowRef<TractionParameterTransferReport | null>(null)
const canTransferRow = (row: TractionRouteCase) => !!row.savedAtUtc && row.isCurrent && props.routes.some(route => route.id === row.routeId)
    && (!row.continuationId || props.routes.some(route => route.id === row.continuationId))
const selectedResults = computed(() => props.rows.filter(row => resultSelectionIds.value.includes(row.id) && canTransferRow(row)))
const transferSummary = computed(() => {
    const report = transferReport.value
    if (!report) return ''
    return ui(`已向“${transferTargetName.value}”带入 ${report.completedRouteIDs.length} 条进路、${report.updatedCellCount} 个轨道电路。`,
        `Applied ${report.completedRouteIDs.length} routes and ${report.updatedCellCount} circuits to “${transferTargetName.value}”.`)
        + (report.completedRouteIDs.length ? ui(`进路：${report.completedRouteIDs.join('、')}。`, ` Routes: ${report.completedRouteIDs.join(', ')}.`) : '')
        + (report.skippedCellCount ? ui(`${report.skippedCellCount} 个区段时刻不完整，保留原值。`, ` ${report.skippedCellCount} incomplete circuits retain their existing values.`) : '')
        + (report.operationTimeOnlyRouteIDs.length ? ui(`仅带入作业时间的进路：${report.operationTimeOnlyRouteIDs.join('、')}。`, ` Routes with operation time only: ${report.operationTimeOnlyRouteIDs.join(', ')}.`) : '')
        + (report.error ? ui(` ${report.completedRouteIDs.length ? '其余进路未完成。' : ''}${report.error}`, ` ${report.completedRouteIDs.length ? 'Remaining routes did not finish. ' : ''}${report.error}`) : '')
})
function selectResults(rows: TractionRouteCase[]) {
    if (transferring.value) return
    resultSelectionIds.value = rows.map(row => row.id); transferReport.value = null; transferError.value = ''
}
async function transferSelectedResults() {
    if (!props.transferResults || !selectedResults.value.length || !props.parameterSetId || props.busy || running.value || transferring.value) return
    const key = props.batchScopeKey
    transferTargetName.value = props.parameterSets?.find(set => set.id === props.parameterSetId)?.name || props.parameterSetId
    transferring.value = true; transferReport.value = null; transferError.value = ''
    try { transferReport.value = await props.transferResults(selectedResults.value.map(row => ({ ...row }))) }
    catch (error) { transferError.value = batchFailureMessage(error) }
    finally {
        transferring.value = false
        if (!props.modelValue || props.batchScopeKey !== key) { reset(); tab.value = 'results' }
    }
}
const table = ref<{ toggleRowSelection(row: TractionBatchCandidate, selected: boolean): void }>()
let generation = 0, loaders = 0
const loadingQueue: Array<{ candidate: TractionBatchCandidate; caseId: string; context: TractionBatchContext; generation: number }> = []
const candidates = computed(() => context.value ? tractionBatchCandidates(context.value.routes, context.value.cases) : [])
const selectedJobs = computed(() => selectedKeys.value.map(key => jobs[key]).filter((job): job is TractionBatchJob => !!job))
const activeJob = computed(() => jobs[activeKey.value])
const loadingCount = computed(() => selectedJobs.value.filter(job => job.status === 'loading').length)
const issueByKey = computed(() => Object.fromEntries(selectedJobs.value.map(job => [job.candidate.key,
    job.workspace && context.value ? tractionBatchIssues(context.value, job.workspace) : []])))
const ready = computed(() => !!context.value && selectedJobs.value.length === selectedKeys.value.length && selectedJobs.value.length > 0
    && selectedJobs.value.every(job => job.workspace && job.status !== 'loading' && job.status !== 'load-error' && !issueByKey.value[job.candidate.key]?.length))
const progress = computed(() => {
    const values = report.value?.jobs ?? selectedJobs.value
    const done = values.filter(job => ['success', 'failed', 'stopped'].includes(job.status)).length
    return { done, total: values.length, percent: values.length ? Math.round(done / values.length * 100) : 0,
        success: values.filter(job => job.status === 'success').length, failed: values.filter(job => job.status === 'failed').length,
        stopped: values.filter(job => job.status === 'stopped').length }
})
const filteredCandidates = computed(() => candidates.value.filter(candidate => {
    const query = search.value.trim().toLowerCase()
    if (query && !`${label(candidate.routeId)} ${label(candidate.continuationId)}`.toLowerCase().includes(query)) return false
    if (filter.value === 'failed') return jobs[candidate.key]?.status === 'failed' || jobs[candidate.key]?.status === 'load-error'
    if (filter.value === 'pending') return jobs[candidate.key]?.status !== 'success' && !candidate.record?.isCurrent
    return true
}))
const movementLabel = (candidate: TractionBatchCandidate) => {
    const mode = jobs[candidate.key]?.workspace?.settings.run.mode ?? tractionModeForRoute(context.value?.routes.find(route => route.id === candidate.routeId)?.type ?? '', !!candidate.continuationId)
    return ({ arrival: ui('接车', 'Arrival'), departure: ui('发车', 'Departure'), through: ui('通过', 'Through'), shunting: ui('调车', 'Shunting') })[mode]
}
function statusLabel(candidate: TractionBatchCandidate) {
    const status = jobs[candidate.key]?.status
    if (!candidate.available) return ui('进路已失效', 'Unavailable')
    if (status) return ({ loading: ui('载入参数', 'Loading'), ready: ui('参数就绪', 'Ready'), 'load-error': ui('载入失败', 'Load failed'),
        queued: ui('等待计算', 'Queued'), running: ui('计算中', 'Calculating'), success: ui('已完成', 'Completed'), failed: ui('计算失败', 'Failed'), stopped: ui('未执行', 'Not run') })[status]
    return !candidate.record?.savedAtUtc ? ui('未计算', 'Not calculated') : candidate.record.isCurrent ? ui('已计算', 'Calculated') : ui('待计算', 'Pending')
}
function statusType(candidate: TractionBatchCandidate): 'success' | 'danger' | 'warning' | 'info' {
    const status = jobs[candidate.key]?.status
    if (status === 'success') return 'success'
    if (status === 'failed' || status === 'load-error' || !candidate.available) return 'danger'
    return status === 'running' || status === 'queued' ? 'warning' : 'info'
}
async function initialize() {
    if (context.value || initializing.value || !props.prepareBatch) return
    initializing.value = true; batchError.value = ''
    const version = generation
    try {
        const next = await props.prepareBatch()
        if (version !== generation || !props.modelValue) return
        context.value = next; commonWorkspace.value = copyWorkspaceContent(next.seed)
    } catch (error) { if (version === generation) batchError.value = batchFailureMessage(error) }
    finally { initializing.value = false }
}
function loadOne(candidate: TractionBatchCandidate, reload = false) {
    if (!context.value || running.value || (jobs[candidate.key] && !reload)) return
    const caseId = jobs[candidate.key]?.caseId ?? candidate.record?.id ?? ''
    jobs[candidate.key] = { candidate, caseId, source: null, workspace: null, status: 'loading', error: '' }
    loadingQueue.push({ candidate, caseId, context: context.value, generation }); drainLoads()
}
function drainLoads() {
    while (loaders < 4 && loadingQueue.length) {
        const task = loadingQueue.shift()!
        if (task.generation !== generation) continue
        loaders++
        void loadTractionBatchJob(task.context, task.candidate, task.caseId).then(job => {
            if (task.generation === generation) jobs[task.candidate.key] = job
        }).catch(error => {
            if (task.generation === generation) Object.assign(jobs[task.candidate.key]!, { status: 'load-error', error: batchFailureMessage(error) })
        }).finally(() => { loaders--; drainLoads() })
    }
}
function selectCandidates(rows: TractionBatchCandidate[]) {
    if (running.value) return
    selectedKeys.value = rows.filter(row => row.available).map(row => row.key)
    for (const row of rows) if (row.available) loadOne(row)
    if (!selectedKeys.value.includes(activeKey.value)) activeKey.value = selectedKeys.value[0] ?? ''
}
function activateRow(row: TractionBatchCandidate, column?: { type?: string } | null) {
    if (!row.available || column?.type === 'selection') return
    activeKey.value = row.key
    if (!running.value && !selectedKeys.value.includes(row.key)) table.value?.toggleRowSelection(row, true)
}
function applyCommon() {
    if (!commonWorkspace.value || !commonFields.value.length || !selectedJobs.value.length) return
    try {
        const values = selectedJobs.value.map(job => ({ job, workspace: applyTractionBatchParameters(job.workspace!, commonWorkspace.value!, commonFields.value) }))
        for (const { job, workspace } of values) { job.workspace = workspace; job.status = 'ready'; job.error = ''; job.conflict = false }
        commonVisible.value = false; batchError.value = ''; report.value = null
    } catch (error) { batchError.value = batchFailureMessage(error) }
}
function projectTrainsUpdated(project: ProjectTrains) {
    if (!context.value || context.value.scope.instanceID !== project.instanceID) return
    context.value = { ...context.value, seed: useProjectTrains(context.value.seed, project), cases: props.rows.map(row => ({ ...row })) }
    const pending = Object.values(jobs).filter(job => job.status === 'loading').map(job => job.candidate)
    generation++; loadingQueue.length = 0
    if (commonWorkspace.value) commonWorkspace.value = useProjectTrains(commonWorkspace.value, project)
    for (const job of Object.values(jobs)) {
        if (job.workspace) job.workspace = useProjectTrains(job.workspace, project)
        if (job.source) job.source = rebaseProjectTrainSource(job.source, project)
        if (job.workspace && !job.conflict) { job.status = 'ready'; job.error = '' }
    }
    for (const candidate of pending) loadOne(candidate, true)
    report.value = null
}
async function start(retry = false) {
    if (!context.value || !ready.value || running.value || props.busy) return
    const targets = selectedJobs.value.filter(job => !retry || job.status === 'failed')
    if (!targets.length) return
    const scopeKey = props.batchScopeKey
    running.value = true; stopRequested.value = false; batchError.value = ''; report.value = { scope: context.value.scope, jobs: targets }
    emit('batch-busy', true)
    try {
        const completed = await runTractionBatch(context.value, targets, () => stopRequested.value || !props.modelValue)
        report.value = completed; emit('batch-complete', completed)
    } catch (error) { batchError.value = batchFailureMessage(error) }
    finally {
        running.value = false; emit('batch-busy', false)
        if (!props.modelValue || props.batchScopeKey !== scopeKey) { reset(); tab.value = 'results' }
    }
}
function reset() {
    generation++; context.value = null; selectedKeys.value = []; activeKey.value = ''; report.value = null; commonWorkspace.value = null
    batchError.value = ''; search.value = ''; filter.value = 'all'; commonVisible.value = false; loadingQueue.length = 0
    resultSelectionIds.value = []; transferReport.value = null; transferError.value = ''
    for (const key of Object.keys(jobs)) delete jobs[key]
}
watch(tab, value => { if (value === 'batch') void initialize() })
watch(() => props.parameterSetId, () => { if (!transferring.value) { transferReport.value = null; transferError.value = '' } })
watch(() => ({ key: activeKey.value, fingerprint: activeJob.value?.workspace ? workspaceFingerprint(activeJob.value.workspace) : '' }), (next, previous) => {
    const job = activeJob.value
    if (!running.value && job?.workspace && !job.conflict && next.key === previous.key && next.fingerprint !== previous.fingerprint && previous.fingerprint) {
        job.status = 'ready'; job.error = ''; report.value = null
    }
})
watch(() => props.modelValue, open => { if (!open) { stopRequested.value = true; if (!running.value && !transferring.value) { reset(); tab.value = 'results' } } })
watch(() => props.batchScopeKey, () => { stopRequested.value = true; if (!running.value && !transferring.value) { reset(); tab.value = 'results' } })
onBeforeUnmount(() => { generation++; stopRequested.value = true; loadingQueue.length = 0 })
</script>
<template>
    <el-dialog :model-value="modelValue" :title="ui('方案进路结果', 'Route results') + ' · ' + setName" :width="tab === 'batch' ? 'min(1380px, 97vw)' : 'min(1180px, 95vw)'" top="4vh" append-to-body class="traction-route-results-dialog" :close-on-click-modal="!busy && !running && !transferring" :show-close="!busy && !running && !transferring" :close-on-press-escape="!busy && !running && !transferring" @update:model-value="emit('update:modelValue', $event)">
        <el-tabs v-model="tab">
        <el-tab-pane :label="ui('已有结果', 'Saved results')" name="results" :disabled="running">
        <div class="route-results-toolbar"><span>{{ ui('进路记录', 'Routes') }} {{ rows.length }} · {{ ui('已有结果', 'Results') }} {{ rows.filter(r => r.savedAtUtc).length }}</span><div><el-button :disabled="busy || transferring" @click="emit('refresh')">{{ ui('刷新列表', 'Refresh list') }}</el-button><el-button type="primary" :disabled="busy || transferring || !prepareBatch" @click="tab = 'batch'">{{ ui('批量计算', 'Batch calculate') }}</el-button></div></div>
        <div v-if="transferResults" class="results-transfer-toolbar">
            <label>{{ ui('计算参数集', 'Calculation parameter set') }}<el-select :model-value="parameterSetId" :loading="parameterSetsLoading" :disabled="busy || transferring || parameterSetsLoading" :aria-label="ui('结果带入目标参数集', 'Target parameter set for saved results')" @update:model-value="(value: string) => emit('update:parameterSetId', value)"><el-option v-for="set in parameterSets" :key="set.id" :value="set.id" :label="set.name" /></el-select></label>
            <span>{{ ui('已选', 'Selected') }} {{ selectedResults.length }} · {{ ui('带入作业时间和轨道电路占用时间', 'Apply operation and track-circuit occupancy times') }}</span>
            <el-button type="primary" :loading="transferring" :disabled="busy || transferring || !selectedResults.length || !parameterSetId || parameterSetsLoading || !!parameterSetError" @click="transferSelectedResults">{{ ui('带入计算参数', 'Apply to calculation parameters') }} ({{ selectedResults.length }})</el-button>
        </div>
        <el-alert v-if="parameterSetError && transferResults" :title="ui('计算参数集加载失败', 'Could not load calculation parameter sets')" type="error" :closable="false"><el-button link @click="emit('reload-parameter-sets')">{{ ui('重新加载', 'Reload') }}</el-button></el-alert>
        <el-alert v-if="transferError || transferReport" :title="transferError || transferSummary" :type="transferError || transferReport?.error ? 'error' : transferReport?.skippedCellCount ? 'warning' : 'success'" :closable="false" show-icon />
        <p v-if="transferResults" class="batch-caption">{{ ui('勾选已有且与当前参数一致的结果后，一键带入目标参数集。待计算的进路请先重新计算。', 'Select saved results matching current inputs, then apply them to the target set. Recalculate pending routes first.') }}</p>
        <el-alert v-if="error" :title="error" type="error" :closable="false" show-icon />
        <el-table :data="rows" row-key="id" v-loading="busy" stripe max-height="52vh" :empty-text="ui('选择进路并点击计算，即可保存到当前方案', 'Select a route and click Calculate to save its result')" @selection-change="selectResults">
            <el-table-column v-if="transferResults" type="selection" width="42" :selectable="(row: TractionRouteCase) => !busy && !transferring && canTransferRow(row)" />
            <el-table-column :label="ui('进路', 'Route')" min-width="190"><template #default="{ row }">{{ label(row.routeId) }} <el-tag v-if="row.id === selectedId" size="small">{{ ui('当前', 'Current') }}</el-tag></template></el-table-column>
            <el-table-column :label="ui('接续进路', 'Continuation')" min-width="150"><template #default="{ row }">{{ label(row.continuationId) }}</template></el-table-column>
            <el-table-column prop="trainName" :label="ui('列车', 'Train')" min-width="130" />
            <el-table-column :label="ui('状态', 'Status')" width="100"><template #default="{ row }">{{ !row.savedAtUtc ? ui('未计算', 'Not calculated') : row.isCurrent ? ui('已计算', 'Calculated') : ui('待计算', 'Pending') }}</template></el-table-column>
            <el-table-column :label="ui('运行时间 (s)', 'Duration (s)')" width="120"><template #default="{ row }">{{ row.durationS?.toFixed(1) ?? '—' }}</template></el-table-column>
            <el-table-column :label="ui('计算时间', 'Calculated at')" min-width="170"><template #default="{ row }">{{ dateLabel(row.savedAtUtc) }}</template></el-table-column>
            <el-table-column :label="ui('操作', 'Actions')" width="130" fixed="right"><template #default="{ row }">
                <el-button link type="primary" :disabled="busy || transferring || !row.routeId" @click="row.id === selectedId ? emit('update:modelValue', false) : emit('open', row)">{{ ui('查看', 'View') }}</el-button>
                <el-popconfirm :title="ui('删除此进路记录的参数与结果？', 'Delete parameters and results for this route?')" :confirm-button-text="ui('删除', 'Delete')" :cancel-button-text="ui('取消', 'Cancel')" width="260" @confirm="emit('remove', row)"><template #reference><el-button link type="danger" :disabled="busy || transferring">{{ ui('删除', 'Delete') }}</el-button></template></el-popconfirm>
            </template></el-table-column>
        </el-table>
        </el-tab-pane>
        <el-tab-pane :label="ui('批量计算', 'Batch calculate')" name="batch" :disabled="!prepareBatch || transferring">
            <el-alert v-if="batchError || error" :title="batchError || error" type="error" :closable="false" show-icon />
            <div v-if="initializing" class="batch-empty" v-loading="true">{{ ui('正在准备进路与参数…', 'Preparing routes and parameters…') }}</div>
            <template v-else-if="context">
                <div class="batch-search">
                    <el-input v-model="search" clearable :disabled="running" :placeholder="ui('搜索进路编号或名称', 'Search route ID or name')" :aria-label="ui('搜索批量进路', 'Search batch routes')" />
                    <el-select v-model="filter" :disabled="running" :aria-label="ui('筛选进路状态', 'Filter route status')"><el-option value="all" :label="ui('全部进路', 'All routes')" /><el-option value="pending" :label="ui('未计算 / 待计算', 'Not calculated / pending')" /><el-option value="failed" :label="ui('失败项', 'Failed items')" /></el-select>
                    <span>{{ ui('已选', 'Selected') }} <strong>{{ selectedKeys.length }}</strong> / {{ candidates.filter(row => row.available).length }}<template v-if="loadingCount"> · {{ ui('载入参数', 'Loading') }} {{ loadingCount }}</template></span>
                    <el-button :disabled="running || busy || !selectedJobs.length || !!loadingCount || selectedJobs.some(job => !job.workspace)" @click="commonVisible = true">{{ ui('统一设置已选进路', 'Set selected parameters') }}</el-button>
                </div>
                <div class="batch-layout">
                    <div class="batch-route-list">
                        <p class="batch-caption">{{ ui('勾选需要计算的进路，点击行可逐条调整参数。包含全部站场进路及已有接续组合。', 'Select routes to calculate; click a row to edit its parameters. Includes station routes and saved continuations.') }}</p>
                        <el-table ref="table" :data="filteredCandidates" row-key="key" stripe highlight-current-row max-height="54vh" :current-row-key="activeKey" @selection-change="selectCandidates" @row-click="activateRow">
                            <el-table-column type="selection" width="42" reserve-selection :selectable="(row: TractionBatchCandidate) => row.available && !running" />
                            <el-table-column :label="ui('进路 / 接续', 'Route / continuation')" min-width="200"><template #default="{ row }"><div class="batch-route-name">{{ label(row.routeId) }}</div><small v-if="row.continuationId">→ {{ label(row.continuationId) }}</small></template></el-table-column>
                            <el-table-column :label="ui('类型', 'Movement')" width="68"><template #default="{ row }">{{ movementLabel(row) }}</template></el-table-column>
                            <el-table-column :label="ui('状态', 'Status')" width="105"><template #default="{ row }"><el-tag size="small" :type="statusType(row)">{{ statusLabel(row) }}</el-tag><div v-if="issueByKey[row.key]?.length" class="batch-issue-count">{{ ui('参数需完善', 'Check parameters') }}</div></template></el-table-column>
                            <el-table-column :label="ui('时间 (s)', 'Time (s)')" width="84"><template #default="{ row }">{{ jobs[row.key]?.calculation?.result.durationS.toFixed(1) ?? row.record?.durationS?.toFixed(1) ?? '—' }}</template></el-table-column>
                            <el-table-column :label="ui('操作', 'Actions')" width="84"><template #default="{ row }"><el-button v-if="jobs[row.key]?.status === 'failed' || jobs[row.key]?.status === 'load-error'" link type="primary" :disabled="running" @click.stop="loadOne(row, true)">{{ ui('重载参数', 'Reload') }}</el-button></template></el-table-column>
                        </el-table>
                    </div>
                    <section class="batch-editor">
                        <template v-if="activeJob?.workspace">
                            <header><strong>{{ label(activeJob.candidate.routeId) }}</strong><small v-if="activeJob.candidate.continuationId">→ {{ label(activeJob.candidate.continuationId) }}</small><span>{{ ui('该进路参数', 'Route parameters') }}</span></header>
                            <el-alert v-if="activeJob.error" :title="activeJob.error" type="error" :closable="false" />
                            <ul v-if="issueByKey[activeKey]?.length" class="batch-issues"><li v-for="issue in issueByKey[activeKey]" :key="issue">{{ issue }}</li></ul>
                            <TractionBatchParameters :key="activeKey" v-model="activeJob.workspace" :context="context" :disabled="running || busy" @trains-updated="projectTrainsUpdated" />
                        </template>
                        <div v-else class="batch-empty"><p>{{ activeJob?.status === 'loading' ? ui('正在载入进路参数…', 'Loading route parameters…') : activeJob?.error || ui('选择进路后，在这里设置计算参数', 'Select a route to edit its calculation parameters') }}</p></div>
                    </section>
                </div>
                <div v-if="report" class="batch-progress" role="status"><el-progress :percentage="progress.percent" :status="!running && progress.failed ? 'exception' : !running && !progress.stopped ? 'success' : undefined" /><span>{{ progress.done }} / {{ progress.total }} · {{ ui('成功', 'Succeeded') }} {{ progress.success }} · {{ ui('失败', 'Failed') }} {{ progress.failed }}<template v-if="progress.stopped"> · {{ ui('未执行', 'Not run') }} {{ progress.stopped }}</template></span></div>
            </template>
            <div v-else class="batch-empty"><el-button :disabled="busy || !prepareBatch" @click="initialize">{{ ui('重新准备批量计算', 'Prepare batch calculation') }}</el-button></div>
        </el-tab-pane>
        </el-tabs>
        <template #footer><div class="batch-footer">
            <span v-if="tab === 'batch'">{{ running ? ui('逐条计算并保存到当前方案', 'Calculating and saving each route') : ui('确认参数后点击批量计算，更新选中进路的参数与结果。', 'Batch calculate saves the selected routes with these parameters.') }}</span>
            <div><el-button :disabled="busy || running || transferring" @click="emit('update:modelValue', false)">{{ ui('关闭', 'Close') }}</el-button>
                <template v-if="tab === 'batch'"><el-button v-if="running" :disabled="stopRequested" @click="stopRequested = true">{{ stopRequested ? ui('等待当前进路完成', 'Waiting for current route') : ui('停止后续计算', 'Stop after current route') }}</el-button><template v-else><el-button v-if="selectedJobs.some(job => job.status === 'failed')" :disabled="busy || !ready" @click="start(true)">{{ ui('重试失败项', 'Retry failed') }}</el-button><el-button type="primary" :disabled="busy || !ready" @click="start()">{{ ui('批量计算', 'Batch calculate') }} ({{ selectedKeys.length }})</el-button></template></template>
            </div>
        </div></template>
    </el-dialog>
    <el-dialog v-model="commonVisible" :title="ui('统一设置已选进路', 'Set selected route parameters')" width="min(670px, 94vw)" top="5vh" append-to-body class="traction-batch-common-dialog">
        <template v-if="context && commonWorkspace">
            <el-alert v-if="batchError" :title="batchError" type="error" :closable="false" />
            <p class="batch-caption">{{ ui('仅应用勾选项目；停车点和分段线路参数仍由各进路单独维护。', 'Only checked groups are applied. Stops and section overrides remain specific to each route.') }}</p>
            <el-checkbox-group v-model="commonFields" class="batch-common-fields"><el-checkbox value="train">{{ ui('列车', 'Train') }}</el-checkbox><el-checkbox value="mode">{{ ui('作业类型', 'Movement') }}</el-checkbox><el-checkbox value="run">{{ ui('运行参数', 'Run') }}</el-checkbox><el-checkbox value="signal">{{ ui('信号时间', 'Signal timing') }}</el-checkbox><el-checkbox value="line">{{ ui('距离标尺与缺省限速', 'Distance scale and default limit') }}</el-checkbox></el-checkbox-group>
            <div class="batch-common-form"><TractionBatchParameters v-model="commonWorkspace" :context="context" :fields="commonFields" common @trains-updated="projectTrainsUpdated" /></div>
        </template>
        <template #footer><el-button @click="commonVisible = false">{{ ui('取消', 'Cancel') }}</el-button><el-button type="primary" :disabled="!commonFields.length" @click="applyCommon">{{ ui('应用至已选进路', 'Apply to selected') }} ({{ selectedKeys.length }})</el-button></template>
    </el-dialog>
</template>
<style scoped>
.route-results-toolbar { display:flex; align-items:center; justify-content:space-between; gap:12px; margin-bottom:14px; color:var(--el-text-color-secondary); font-size:13px; }.el-alert { margin-bottom:12px; }
.results-transfer-toolbar { display:flex; flex-wrap:wrap; align-items:center; gap:12px; padding:12px; margin-bottom:10px; border-radius:6px; background:var(--el-fill-color-light); }.results-transfer-toolbar>label { display:flex; align-items:center; gap:8px; font-size:13px; }.results-transfer-toolbar .el-select { width:190px; }.results-transfer-toolbar>span { flex:1; color:var(--el-text-color-secondary); font-size:12px; }.results-transfer-toolbar>.el-button { margin-left:auto; }
.batch-caption { margin:0 0 12px; color:var(--el-text-color-secondary); font-size:12px; line-height:1.6; }
.batch-search { display:flex; flex-wrap:wrap; align-items:center; gap:10px; margin-bottom:14px; }.batch-search>.el-input { width:230px; }.batch-search>.el-select { width:180px; }.batch-search>span { flex:1; white-space:nowrap; color:var(--el-text-color-secondary); font-size:13px; }
.batch-layout { display:grid; grid-template-columns:minmax(0,1.45fr) minmax(350px,1fr); gap:18px; height:59vh; min-height:310px; }
.batch-route-list { min-width:0; overflow:auto; }.batch-route-name { font-weight:550; }.batch-route-list small { color:var(--el-text-color-secondary); }.batch-issue-count { color:var(--el-color-danger); font-size:11px; line-height:1.6; }
.batch-editor { min-width:0; overflow:auto; padding:0 14px 12px; border:1px solid var(--el-border-color-lighter); border-radius:8px; background:var(--el-fill-color-blank); }
.batch-editor>header { position:sticky; top:0; z-index:2; display:flex; flex-wrap:wrap; gap:6px; align-items:center; padding:13px 0; background:var(--el-fill-color-blank); border-bottom:1px solid var(--el-border-color-lighter); font-size:13px; }.batch-editor>header>span { margin-left:auto; color:var(--el-text-color-secondary); font-size:11px; }.batch-editor>header small { color:var(--el-text-color-secondary); }
.batch-empty { min-height:180px; display:flex; align-items:center; justify-content:center; padding:20px; color:var(--el-text-color-secondary); text-align:center; font-size:13px; }
.batch-issues { padding:10px 12px 10px 26px; margin:10px 0; border-radius:5px; background:var(--el-color-danger-light-9); color:var(--el-color-danger); font-size:12px; line-height:1.6; }
.batch-progress { display:flex; flex-wrap:wrap; gap:10px; align-items:center; margin-top:14px; font-size:12px; color:var(--el-text-color-secondary); }.batch-progress>.el-progress { flex:1; min-width:180px; }
.batch-footer { display:flex; flex-wrap:wrap; gap:12px; align-items:center; justify-content:space-between; }.batch-footer>span { flex:1; color:var(--el-text-color-secondary); font-size:12px; text-align:left; }.batch-footer>div { margin-left:auto; }
.batch-common-fields { margin-bottom:10px; padding:8px 12px; background:var(--el-fill-color-light); border-radius:6px; }.batch-common-form { max-height:59vh; overflow:auto; padding:0 6px; }
@media(max-width:900px) { .batch-layout { grid-template-columns:1fr; height:64vh; overflow:auto; }.batch-route-list { min-height:220px; }.batch-editor { min-height:300px; overflow:visible; }.batch-search>.el-input { flex:1; min-width:150px; }.batch-search>.el-select { width:150px; }.batch-footer>span { flex-basis:100%; } }
</style>
