<script lang="ts">
export interface CellOccupancyImportScope {
    instanceID: string
    stationSchemeID: string
    operationPlanID: string
}

interface CellMatch {
    sourceCell: string
    cellID: string | null
    cellName: string | null
    method: string
    needsSelection: boolean
}

interface ImportPreview {
    valid: boolean
    trainCount: number
    movementCount: number
    occupancyCount: number
    newRouteCount: number
    reusedRouteCount: number
    warnings: string[]
    errors: string[]
    cellMatches: CellMatch[]
    availableCells: Array<{ id: string; name: string }>
    routeMatches: Array<{ movementType: string; routeID: string; description: string; isNew: boolean; movementCount: number }>
    previewToken: string
}

export interface CellOccupancyImportResult extends ImportPreview {
    operationPlanID: string
    operationPlanName: string
}
</script>

<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { ElMessage } from 'element-plus'
import { Check, Close, Refresh, UploadFilled } from '@element-plus/icons-vue'
import ActionButton from '@/components/ui/ActionButton.vue'
import axios from '@/utils/axios'

const props = defineProps<{
    modelValue: boolean
    instanceId: string
    stationSchemeId: string
    operationPlanId: string
    stationSchemeName: string
    operationPlanName: string
    disabled: boolean
}>()
const emit = defineEmits<{
    'update:modelValue': [value: boolean]
    'busy-change': [value: boolean]
    imported: [result: CellOccupancyImportResult, scope: CellOccupancyImportScope]
}>()
const { t } = useI18n()
const fileInput = ref<HTMLInputElement>()
const fileName = ref('')
const csvText = ref('')
const createNewPlan = ref(true)
const planName = ref('')
const cellMappings = ref<Record<string, string>>({})
const preview = ref<ImportPreview | null>(null)
const previewKey = ref('')
const readingFile = ref(false)
const previewing = ref(false)
const importing = ref(false)
const errorMessage = ref('')
let previewVersion = 0
let fileVersion = 0
let previewTimer: ReturnType<typeof setTimeout> | undefined
let previewController: AbortController | undefined

const scope = computed<CellOccupancyImportScope>(() => ({
    instanceID: props.instanceId,
    stationSchemeID: props.stationSchemeId,
    operationPlanID: props.operationPlanId,
}))
const scopeKey = computed(() => JSON.stringify(scope.value))
const payload = computed(() => ({
    ...scope.value,
    csvText: csvText.value,
    fileName: fileName.value,
    createNewPlan: createNewPlan.value,
    planName: createNewPlan.value ? planName.value.trim() : '',
    cellMappings: { ...cellMappings.value },
}))
const requestKey = computed(() => JSON.stringify(payload.value))
const canPreview = computed(() => Boolean(
    props.modelValue && !props.disabled && !readingFile.value && !importing.value &&
    props.instanceId && props.stationSchemeId && props.operationPlanId && csvText.value.trim() &&
    (!createNewPlan.value || (planName.value.trim().length > 0 && planName.value.trim().length <= 100)),
))
const previewIsCurrent = computed(() => !!preview.value && previewKey.value === requestKey.value)
const canImport = computed(() => canPreview.value && !previewing.value && previewIsCurrent.value &&
    preview.value?.valid && !!preview.value.previewToken && !preview.value.errors.length)
const visible = computed({
    get: () => props.modelValue,
    set: (value: boolean) => { if (!importing.value) emit('update:modelValue', value) },
})

function cancelPreview() {
    ++previewVersion
    clearTimeout(previewTimer)
    previewController?.abort()
    previewController = undefined
    previewing.value = false
    previewKey.value = ''
}

function reset() {
    ++fileVersion
    cancelPreview()
    csvText.value = ''
    fileName.value = ''
    createNewPlan.value = true
    planName.value = t('operationPlan.cellOccupancyImport.defaultPlanName')
    cellMappings.value = {}
    preview.value = null
    readingFile.value = false
    errorMessage.value = ''
    if (fileInput.value) fileInput.value.value = ''
}

function schedulePreview() {
    cancelPreview()
    errorMessage.value = ''
    if (canPreview.value) previewTimer = setTimeout(() => { void previewImport() }, 350)
}

function getErrorMessage(error: unknown, fallback: string) {
    const data = (error as { response?: { data?: { message?: string; detail?: string } | string } })?.response?.data
    if (typeof data === 'string' && data.trim()) return data
    if (data && typeof data === 'object') return [data.message, data.detail].filter(Boolean).join(' ') || fallback
    return fallback
}

function isPreview(value: unknown): value is ImportPreview {
    const data = value as ImportPreview
    return !!data && typeof data.valid === 'boolean' &&
        ['trainCount', 'movementCount', 'occupancyCount', 'newRouteCount', 'reusedRouteCount']
            .every(key => Number.isFinite((data as unknown as Record<string, unknown>)[key])) &&
        Array.isArray(data.cellMatches) && Array.isArray(data.availableCells) && Array.isArray(data.routeMatches) &&
        Array.isArray(data.errors) && Array.isArray(data.warnings)
}

async function selectFile(event: Event) {
    if (importing.value) return
    const file = (event.target as HTMLInputElement).files?.[0]
    if (!file) return
    const version = ++fileVersion
    cancelPreview()
    csvText.value = ''
    fileName.value = file.name
    cellMappings.value = {}
    preview.value = null
    errorMessage.value = ''
    readingFile.value = false
    if (!/\.csv$/i.test(file.name)) {
        errorMessage.value = t('operationPlan.cellOccupancyImport.csvRequired')
        return
    }
    if (file.size > 5 * 1024 * 1024) {
        errorMessage.value = t('operationPlan.cellOccupancyImport.fileTooLarge')
        return
    }
    readingFile.value = true
    try {
        const bytes = await file.arrayBuffer()
        if (version !== fileVersion || !props.modelValue) return
        const text = new TextDecoder('utf-8', { fatal: true }).decode(bytes).replace(/^\uFEFF/, '')
        if (!text.trim()) {
            errorMessage.value = t('operationPlan.cellOccupancyImport.emptyFile')
            return
        }
        csvText.value = text
    } catch {
        if (version === fileVersion) errorMessage.value = t('operationPlan.cellOccupancyImport.readFailed')
    } finally {
        if (version === fileVersion) {
            readingFile.value = false
            if (csvText.value) schedulePreview()
        }
    }
}

async function previewImport() {
    if (!canPreview.value) return
    cancelPreview()
    const version = previewVersion
    const key = requestKey.value
    const controller = new AbortController()
    previewController = controller
    previewing.value = true
    errorMessage.value = ''
    try {
        const response = await axios.post<ImportPreview>('/OperationPlan/PreviewCellOccupancyImport', payload.value, {
            signal: controller.signal,
            timeout: 120000,
        })
        if (version !== previewVersion || key !== requestKey.value || !props.modelValue) return
        if (!isPreview(response.data)) throw new Error('Invalid preview response')
        preview.value = response.data
        previewKey.value = key
    } catch (error) {
        if (version !== previewVersion || controller.signal.aborted || key !== requestKey.value) return
        errorMessage.value = getErrorMessage(error, t('operationPlan.cellOccupancyImport.previewFailed'))
    } finally {
        if (version === previewVersion) previewing.value = false
    }
}

function updateCellMapping(sourceCell: string, value: string) {
    const next = { ...cellMappings.value }
    if (value) next[sourceCell] = value
    else delete next[sourceCell]
    cellMappings.value = next
}

async function importFile() {
    if (!canImport.value || importing.value || !preview.value) return
    const submittedScope = { ...scope.value }
    const request = { ...payload.value, previewToken: preview.value.previewToken }
    importing.value = true
    emit('busy-change', true)
    errorMessage.value = ''
    try {
        const response = await axios.post<CellOccupancyImportResult>('/OperationPlan/ImportCellOccupancy', request, { timeout: 120000 })
        if (!isPreview(response.data) || !response.data.valid || !response.data.operationPlanID) {
            if (isPreview(response.data)) {
                preview.value = response.data
                previewKey.value = ''
                errorMessage.value = response.data.errors.join('；') || t('operationPlan.cellOccupancyImport.importFailed')
                return
            }
            throw new Error('Invalid import response')
        }
        ElMessage.success(t('operationPlan.cellOccupancyImport.success', {
            plan: response.data.operationPlanName,
            trains: response.data.trainCount,
            movements: response.data.movementCount,
            routes: response.data.newRouteCount,
        }))
        emit('imported', response.data, submittedScope)
        emit('update:modelValue', false)
    } catch (error) {
        previewKey.value = ''
        errorMessage.value = getErrorMessage(error, t('operationPlan.cellOccupancyImport.importFailed'))
    } finally {
        importing.value = false
        emit('busy-change', false)
    }
}

watch(requestKey, schedulePreview, { flush: 'sync' })
watch(scopeKey, () => {
    cancelPreview()
    preview.value = null
    cellMappings.value = {}
    if (!importing.value) emit('update:modelValue', false)
})
watch(() => props.modelValue, value => {
    if (value) reset()
    else { ++fileVersion; cancelPreview() }
}, { immediate: true })
onBeforeUnmount(() => { ++fileVersion; cancelPreview() })
</script>

<template>
    <el-dialog v-model="visible" :title="t('operationPlan.cellOccupancyImport.title')" width="min(960px, 94vw)"
        :close-on-click-modal="false" :close-on-press-escape="!importing" :show-close="!importing">
        <div class="cell-occupancy-import">
            <el-descriptions :column="2" border size="small">
                <el-descriptions-item :label="t('stationLayout.menu.stationScheme')">{{ stationSchemeName || stationSchemeId }}</el-descriptions-item>
                <el-descriptions-item :label="t('operationPlan.planObject.label')">{{ operationPlanName || operationPlanId }}</el-descriptions-item>
            </el-descriptions>
            <p class="cell-occupancy-help">{{ t('operationPlan.cellOccupancyImport.description') }}</p>
            <el-form label-position="top" :disabled="importing">
                <el-form-item :label="t('operationPlan.cellOccupancyImport.file')">
                    <div class="cell-occupancy-file">
                        <input ref="fileInput" type="file" accept=".csv,text/csv" :disabled="importing" class="cell-occupancy-file-input" :aria-label="t('operationPlan.cellOccupancyImport.selectFile')" @change="selectFile" />
                        <ActionButton :icon="UploadFilled" :loading="readingFile" :disabled="importing || readingFile" :label="t('operationPlan.cellOccupancyImport.selectFile')" @click="fileInput?.click()" />
                        <span>{{ fileName || t('operationPlan.cellOccupancyImport.noFile') }}</span>
                        <span class="cell-occupancy-help">{{ t('operationPlan.cellOccupancyImport.fileHint') }}</span>
                    </div>
                </el-form-item>
                <el-form-item :label="t('operationPlan.cellOccupancyImport.destination')">
                    <el-radio-group v-model="createNewPlan" :disabled="importing">
                        <el-radio-button :value="true">{{ t('operationPlan.cellOccupancyImport.newPlan') }}</el-radio-button>
                        <el-radio-button :value="false">{{ t('operationPlan.cellOccupancyImport.appendPlan') }}</el-radio-button>
                    </el-radio-group>
                </el-form-item>
                <el-form-item v-if="createNewPlan" :label="t('operationPlan.planObject.fields.name')" required>
                    <el-input v-model="planName" maxlength="100" :disabled="importing" :placeholder="t('operationPlan.cellOccupancyImport.defaultPlanName')" />
                </el-form-item>
            </el-form>
            <el-alert v-if="!createNewPlan" :title="t('operationPlan.cellOccupancyImport.appendHint', { plan: operationPlanName || operationPlanId })" type="info" show-icon :closable="false" />
            <el-alert v-if="errorMessage" :title="errorMessage" type="error" show-icon :closable="false" />
            <div v-if="previewing" class="cell-occupancy-help" role="status">{{ t('operationPlan.cellOccupancyImport.previewing') }}</div>
            <template v-if="preview">
                <el-alert v-if="!previewIsCurrent" :title="t('operationPlan.cellOccupancyImport.previewStale')" type="info" :closable="false" />
                <el-descriptions v-else :column="3" border size="small">
                    <el-descriptions-item :label="t('operationPlan.cellOccupancyImport.trains')">{{ preview.trainCount }}</el-descriptions-item>
                    <el-descriptions-item :label="t('operationPlan.cellOccupancyImport.movements')">{{ preview.movementCount }}</el-descriptions-item>
                    <el-descriptions-item :label="t('operationPlan.cellOccupancyImport.occupancies')">{{ preview.occupancyCount }}</el-descriptions-item>
                    <el-descriptions-item :label="t('operationPlan.cellOccupancyImport.reusedRoutes')">{{ preview.reusedRouteCount }}</el-descriptions-item>
                    <el-descriptions-item :label="t('operationPlan.cellOccupancyImport.newRoutes')">{{ preview.newRouteCount }}</el-descriptions-item>
                </el-descriptions>
                <div v-if="previewIsCurrent && preview.errors.length" class="cell-occupancy-messages">
                    <el-alert :title="t('operationPlan.cellOccupancyImport.errors')" type="error" show-icon :closable="false">
                        <ul><li v-for="(message, index) in preview.errors" :key="index">{{ message }}</li></ul>
                    </el-alert>
                </div>
                <div v-if="previewIsCurrent && preview.warnings.length" class="cell-occupancy-messages">
                    <el-alert :title="t('operationPlan.cellOccupancyImport.warnings')" type="warning" show-icon :closable="false">
                        <ul><li v-for="(message, index) in preview.warnings" :key="index">{{ message }}</li></ul>
                    </el-alert>
                </div>
                <p class="cell-occupancy-section-title">{{ t('operationPlan.cellOccupancyImport.cellMatches') }}</p>
                <el-table :data="preview.cellMatches" max-height="250" size="small" row-key="sourceCell">
                    <el-table-column prop="sourceCell" :label="t('operationPlan.cellOccupancyImport.sourceCell')" min-width="150" />
                    <el-table-column :label="t('operationPlan.cellOccupancyImport.matchedCell')" min-width="280">
                        <template #default="{ row }">
                            <el-select v-if="row.needsSelection || cellMappings[row.sourceCell]" :model-value="cellMappings[row.sourceCell] || ''" filterable clearable :disabled="importing"
                                :placeholder="t('operationPlan.cellOccupancyImport.selectCell')" class="cell-occupancy-cell-select" @update:model-value="updateCellMapping(row.sourceCell, $event)">
                                <el-option v-for="cell in preview.availableCells" :key="cell.id" :value="cell.id" :label="cell.name ? `${cell.name} (${cell.id})` : cell.id" />
                            </el-select>
                            <span v-else>{{ row.cellName || row.cellID || '—' }}</span>
                        </template>
                    </el-table-column>
                    <el-table-column :label="t('operationPlan.cellOccupancyImport.matchStatus')" width="150">
                        <template #default="{ row }">
                            <el-tooltip :content="row.method" :disabled="!row.method" placement="top">
                                <el-tag :type="row.needsSelection ? 'warning' : 'success'">{{ t(row.needsSelection ? 'operationPlan.cellOccupancyImport.needsSelection' : 'operationPlan.cellOccupancyImport.matched') }}</el-tag>
                            </el-tooltip>
                        </template>
                    </el-table-column>
                </el-table>
                <p class="cell-occupancy-section-title">{{ t('operationPlan.cellOccupancyImport.routeMatches') }}</p>
                <el-table :data="previewIsCurrent ? preview.routeMatches : []" max-height="230" size="small">
                    <el-table-column prop="movementType" :label="t('operationPlan.cellOccupancyImport.movementType')" width="105" />
                    <el-table-column prop="description" :label="t('operationPlan.cellOccupancyImport.route')" min-width="220" />
                    <el-table-column prop="movementCount" :label="t('operationPlan.cellOccupancyImport.movements')" width="100" />
                    <el-table-column :label="t('operationPlan.cellOccupancyImport.routeAction')" width="110">
                        <template #default="{ row }"><el-tag :type="row.isNew ? 'warning' : 'success'">{{ t(row.isNew ? 'operationPlan.cellOccupancyImport.createRoute' : 'operationPlan.cellOccupancyImport.reuseRoute') }}</el-tag></template>
                    </el-table-column>
                </el-table>
            </template>
        </div>
        <template #footer>
            <ActionButton :icon="Refresh" :loading="previewing" :disabled="!canPreview || previewing" :label="t('operationPlan.cellOccupancyImport.preview')" @click="previewImport" />
            <ActionButton :icon="Close" :disabled="importing" :label="t('operationPlan.actions.cancel')" @click="visible = false" />
            <ActionButton :icon="Check" type="primary" :loading="importing" :disabled="!canImport" :label="t('operationPlan.cellOccupancyImport.confirmImport')" @click="importFile" />
        </template>
    </el-dialog>
</template>

<style scoped>
.cell-occupancy-import { display: flex; flex-direction: column; gap: 12px; }
.cell-occupancy-help { color: var(--el-text-color-secondary); font-size: 13px; margin: 0; }
.cell-occupancy-file { display: flex; align-items: center; gap: 10px; flex-wrap: wrap; }
.cell-occupancy-file-input { display: none; }
.cell-occupancy-cell-select { width: 100%; }
.cell-occupancy-section-title { margin: 4px 0 0; font-weight: 600; }
.cell-occupancy-messages ul { margin: 4px 0; padding-left: 20px; max-height: 150px; overflow-y: auto; }
.cell-occupancy-import :deep(.el-form-item:last-child) { margin-bottom: 0; }
</style>
