<template>
    <el-card class="solve-job-manager" shadow="never">
        <template #header>
            <div class="manager-toolbar">
                <strong>{{ t('modelSolving.manager.title') }}</strong>
                <span class="manager-hint">{{ t('modelSolving.manager.hint') }}</span>
                <el-select v-model="status" :aria-label="t('modelSolving.fields.status')" class="status-filter">
                    <el-option :label="t('modelSolving.manager.all')" value="" />
                    <el-option :label="t('modelSolving.manager.active')" value="active" />
                    <el-option v-for="value in ['completed', 'failed', 'cancelled']" :key="value" :label="t(`modelSolving.jobs.${value}`)" :value="value" />
                </el-select>
                <ActionButton :icon="Refresh" :loading="loading" :label="t('modelSolving.actions.refreshJob')" @click="refresh(false)" />
            </div>
        </template>
        <el-alert v-if="loadError" :title="loadError" type="error" :closable="false" show-icon />
        <el-table :data="jobs" row-key="jobId" :current-row-key="selectedJobId" highlight-current-row
            size="small" max-height="230" :empty-text="t('modelSolving.empty.noJob')" @row-click="selectRow">
            <el-table-column prop="jobId" :label="t('modelSolving.manager.jobId')" min-width="180" show-overflow-tooltip />
            <el-table-column :label="t('modelSolving.fields.status')" width="115">
                <template #default="{ row }">
                    <el-tag :type="row.status === 'failed' ? 'danger' : row.status === 'completed' ? 'success' : isSolveJobActive(row.status) ? 'warning' : 'info'" size="small">
                        {{ t(`modelSolving.jobs.${row.status}`) }}
                    </el-tag>
                </template>
            </el-table-column>
            <el-table-column prop="agentName" :label="t('modelSolving.fields.agent')" min-width="120" />
            <el-table-column prop="requestedBy" :label="t('modelSolving.fields.account')" width="100" />
            <el-table-column :label="t('modelSolving.manager.progress')" width="130">
                <template #default="{ row }"><el-progress :percentage="row.progress" :stroke-width="5" /></template>
            </el-table-column>
            <el-table-column :label="t('modelSolving.manager.submittedAt')" min-width="175">
                <template #default="{ row }">{{ new Date(row.submittedAt).toLocaleString() }}</template>
            </el-table-column>
            <el-table-column :label="t('modelSolving.manager.message')" min-width="200" show-overflow-tooltip>
                <template #default="{ row }">{{ row.error || row.progressMessage }}</template>
            </el-table-column>
            <el-table-column width="95" fixed="right">
                <template #default="{ row }"><el-button link type="primary" @click.stop="selectRow(row)">{{ t('modelSolving.manager.open') }}</el-button></template>
            </el-table-column>
        </el-table>
        <el-pagination v-model:current-page="page" :page-size="10" :total="total" layout="total, prev, pager, next" small />
    </el-card>
</template>

<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { Refresh } from '@element-plus/icons-vue'
import ActionButton from '@/components/ui/ActionButton.vue'
import axios from '@/utils/axios'
import { isSolveJobActive, type SolveJob } from '../solveJobs'

const props = defineProps<{ selectedJobId: string }>()
const emit = defineEmits<{ select: [jobId: string] }>()
const { t } = useI18n()
const jobs = ref<SolveJob[]>([])
const status = ref('')
const page = ref(1)
const total = ref(0)
const loading = ref(false)
const loadError = ref('')
let version = 0
let timer: number | undefined
let disposed = false

function selectRow(job: SolveJob) { emit('select', job.jobId) }

async function refresh(silent = true) {
    if (disposed || (silent && loading.value)) return
    const requestVersion = ++version
    loading.value = true
    try {
        const response = await axios.get<{ items: SolveJob[]; total: number }>('/api/capacity-agents/jobs/page', {
            params: { status: status.value, offset: (page.value - 1) * 10, limit: 10 },
        })
        if (disposed || requestVersion !== version) return
        jobs.value = response.data.items
        total.value = response.data.total
        loadError.value = ''
        if (page.value > Math.max(1, Math.ceil(total.value / 10))) page.value = Math.max(1, Math.ceil(total.value / 10))
        if (!props.selectedJobId && jobs.value[0]) selectRow(jobs.value[0])
    } catch {
        if (!disposed && requestVersion === version) loadError.value = t('modelSolving.manager.loadFailed')
    } finally {
        if (requestVersion === version) loading.value = false
    }
}

watch(status, () => { page.value = 1; void refresh(false) })
watch(page, () => void refresh(false))
onMounted(() => { void refresh(false); timer = window.setInterval(() => void refresh(), 3000) })
onBeforeUnmount(() => { disposed = true; version++; window.clearInterval(timer) })
defineExpose({ refresh })
</script>

<style scoped>
.solve-job-manager { margin-bottom: 12px; }
.manager-toolbar { display: flex; align-items: center; flex-wrap: wrap; gap: 12px; }
.manager-hint { flex: 1; color: #64748b; font-size: 12px; min-width: 220px; }
.status-filter { width: 130px; }
.el-pagination { margin-top: 10px; justify-content: flex-end; }
</style>
