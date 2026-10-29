<template>
    <el-card class="solve-job-manager" shadow="never">
        <template #header>
            <div class="manager-heading"><strong>{{ t('modelSolving.manager.title') }}</strong><span class="task-count">{{ total }}</span></div>
            <p class="manager-hint">{{ t('modelSolving.manager.hint') }}</p>
            <el-button class="new-task" type="primary" plain :disabled="!instanceId || busy" @click="emit('new')"><el-icon><Plus /></el-icon>{{ t('modelSolving.workspace.newTask') }}</el-button>
            <div class="manager-toolbar">
                <el-select v-model="status" :disabled="!instanceId" :aria-label="t('modelSolving.fields.status')" :placeholder="t('modelSolving.manager.all')" size="small">
                    <el-option :label="t('modelSolving.manager.all')" value="" />
                    <el-option :label="t('modelSolving.manager.active')" value="active" />
                    <el-option v-for="value in ['draft', 'completed', 'failed', 'cancelled']" :key="value" :label="t('modelSolving.jobs.' + value)" :value="value" />
                </el-select>
                <el-button :icon="Refresh" :loading="loading" :disabled="!instanceId" :aria-label="t('modelSolving.actions.refreshJob')" :title="t('modelSolving.actions.refreshJob')" size="small" @click="refresh(false)" />
            </div>
        </template>
        <el-alert v-if="loadError" :title="loadError" type="error" :closable="false" show-icon />
        <div v-loading="loading && !jobs.length" class="task-list" role="list" :aria-label="t('modelSolving.manager.title')">
            <el-empty v-if="!jobs.length" :description="instanceId ? t('modelSolving.empty.noJob') : t('capacityMain.placeholders.selectInstance')" :image-size="50" />
            <article v-for="row in jobs" :key="row.jobId" class="task-item" :class="{ 'is-selected': row.jobId === selectedJobId }" role="listitem">
                <button class="task-open" type="button" :disabled="busy" :aria-pressed="row.jobId === selectedJobId" :aria-label="t('modelSolving.manager.open') + ' ' + row.jobId" @click="selectRow(row)">
                    <span class="task-title"><span class="task-name" :title="row.name">{{ row.name || row.jobId }}</span><el-tag :type="tagType(row.status)" size="small" effect="light">{{ t('modelSolving.jobs.' + row.status) }}</el-tag></span>
                    <span class="task-id">#{{ row.jobId }}</span><span class="task-agent">{{ row.agentName }}</span>
                    <span class="task-time">{{ formatDate(row.createdAt || row.submittedAt) }}</span>
                    <el-progress v-if="row.status !== 'draft'" :percentage="row.progress" :show-text="false" :stroke-width="4" :status="row.status === 'completed' ? 'success' : row.status === 'failed' ? 'exception' : undefined" />
                    <span class="task-message" :title="row.error || row.progressMessage">{{ row.error || row.progressMessage || row.modelId }}</span>
                </button>
                <div class="row-actions">
                    <span class="task-percent">{{ row.status === 'draft' ? t('modelSolving.jobs.draft') : row.progress + '%' }}</span>
                    <el-button v-if="isSolveJobActive(row.status)" link type="warning" size="small" :disabled="busy || row.status === 'cancelling'" @click="emit('cancel', row)">{{ t('modelSolving.manager.cancel') }}</el-button>
                    <el-tooltip :content="t('modelSolving.manager.deleteActiveHint')" :disabled="!isSolveJobActive(row.status)">
                        <span><el-button link type="danger" size="small" :disabled="busy || isSolveJobActive(row.status)" @click="emit('delete', row)">{{ t('modelSolving.manager.delete') }}</el-button></span>
                    </el-tooltip>
                </div>
            </article>
        </div>
        <el-pagination v-model:current-page="page" :page-size="10" :pager-count="5" :total="total" layout="prev, pager, next" small hide-on-single-page />
    </el-card>
</template>

<script setup lang="ts">
import { onBeforeUnmount, onMounted } from 'vue'
import { useI18n } from 'vue-i18n'
import { Plus, Refresh } from '@element-plus/icons-vue'
import axios from '@/utils/axios'
import { isSolveJobActive, type SolveJob } from '../solveJobs'
import { useSolveJobList } from '../useSolveJobList'

const props = withDefaults(defineProps<{ instanceId: string; selectedJobId: string; busy: boolean; autoSelect?: boolean }>(), { autoSelect: true })
const emit = defineEmits<{ select: [jobId: string]; cancel: [job: SolveJob]; delete: [job: SolveJob]; new: [] }>()
const { t } = useI18n()
const { jobs, status, page, total, loading, loadError, refresh } = useSolveJobList({
    instanceId: () => props.instanceId,
    selectedJobId: () => props.selectedJobId,
    autoSelect: () => props.autoSelect,
    loadPage: async params => (await axios.get<{ items: SolveJob[]; total: number }>('/api/capacity-agents/jobs/page', { params })).data,
    onSelect: selectRow,
    loadErrorMessage: () => t('modelSolving.manager.loadFailed'),
})
let timer: number | undefined
function selectRow(job: SolveJob) { if (!props.busy && job.instanceId === props.instanceId) emit('select', job.jobId) }
function formatDate(value: string) { return new Date(value).toLocaleString() }
function tagType(status: SolveJob['status']) { return status === 'failed' ? 'danger' : status === 'completed' ? 'success' : isSolveJobActive(status) ? 'warning' : 'info' }
onMounted(() => { timer = window.setInterval(() => void refresh(), 3000) })
onBeforeUnmount(() => window.clearInterval(timer))
async function revealJob() { status.value = ''; page.value = 1; await refresh(false) }
defineExpose({ refresh, revealJob })
</script>

<style scoped>
.solve-job-manager { height: 100%; display: flex; flex-direction: column; min-width: 0; border-radius: 8px; }
.solve-job-manager :deep(.el-card__header) { padding: 16px 14px 12px; }
.solve-job-manager :deep(.el-card__body) { display: flex; flex-direction: column; flex: 1; min-height: 0; padding: 0; }
.manager-heading { display: flex; gap: 8px; align-items: center; color: #263d55; font-size: 14px; }
.task-count { border-radius: 12px; min-width: 23px; padding: 1px 6px; text-align: center; color: #6b7f94; background: #eef2f6; font-size: 11px; }
.manager-hint { color: #8492a4; font-size: 11px; line-height: 1.6; margin: 8px 0 12px; }
.new-task { width: 100%; gap: 5px; margin-bottom: 12px; }
.manager-toolbar { display: flex; gap: 8px; }
.manager-toolbar .el-select { flex: 1; min-width: 0; }
.task-list { overflow: auto; flex: 1; min-height: 0; padding: 10px; }
.task-item { border: 1px solid #e8edf3; border-radius: 7px; margin-bottom: 9px; background: #fff; transition: border-color .15s, background .15s; overflow: hidden; }
.task-item:hover { border-color: #a9bfdc; }
.task-item.is-selected { border-color: #6892c2; background: #f0f6ff; box-shadow: inset 3px 0 #477baa; }
.task-open { display: flex; flex-direction: column; gap: 7px; border: 0; background: transparent; width: 100%; padding: 12px 12px 6px; text-align: left; cursor: pointer; color: inherit; font: inherit; }
.task-open:focus-visible { outline: 2px solid #477baa; outline-offset: -2px; }
.task-open:disabled { cursor: wait; }
.task-title { display: flex; justify-content: space-between; align-items: center; gap: 8px; width: 100%; }
.task-name { min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; font-size: 12px; font-weight: 600; color: #3b526a; }
.task-id { color: #3b526a; font: 600 12px ui-monospace, Consolas, monospace; }
.task-agent { color: #435a73; font-size: 12px; }
.task-time { color: #93a0b1; font-size: 11px; }
.task-message { color: #7c8a9c; font-size: 11px; width: 100%; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.task-open .el-progress { width: 100%; margin: 2px 0; }
.row-actions { display: flex; justify-content: flex-end; align-items: center; gap: 10px; padding: 0 12px 8px; }
.task-percent { margin-right: auto; font-size: 11px; color: #93a0b1; }
.row-actions :deep(.el-button + .el-button) { margin-left: 0; }
.el-pagination { justify-content: center; padding: 10px 4px; border-top: 1px solid #e8edf3; }
</style>
