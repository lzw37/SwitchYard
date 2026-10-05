<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import { ArrowDown, ArrowUp, Check, Close, Delete, Edit, Plus, Refresh } from '@element-plus/icons-vue'
import { useI18n } from 'vue-i18n'
import ActionButton from '@/components/ui/ActionButton.vue'
import axios from '@/utils/axios'

interface ChartScope { instanceID: string; stationSchemeID: string; operationPlanID: string }
interface Chart { chartID: string; chartName: string; cellIDs: string[] | null }
interface SavedChart { chartID: string; chartName: string; cellIDs: string[] }
interface SaveRequest { charts: SavedChart[]; selectedID: string; closeDialog: boolean }

const props = withDefaults(defineProps<{
    scope: ChartScope | null
    cells: { id: string; name: string }[]
    disabled?: boolean
}>(), { disabled: false })
const emit = defineEmits<{ 'selection-change': [cellIDs: string[] | null] }>()
const { t } = useI18n()
const text = (key: string, values: Record<string, string | number> = {}) => t(`operationPlan.resourceOccupancyCharts.${key}`, values)

const charts = ref<Chart[]>([])
const activeChartID = ref('')
const loaded = ref(false)
const loading = ref(false)
const saving = ref(false)
const loadError = ref('')
const saveError = ref('')
const dialogVisible = ref(false)
const editingChartID = ref('')
const draftName = ref('')
const draftCellIDs = ref<string[]>([])
const draftFollowsAllCells = ref(false)
const nameError = ref('')
let requestVersion = 0
let disposed = false
let pendingSave: SaveRequest | null = null

const normalizedScope = computed<ChartScope | null>(() => {
    const value = props.scope
    if (!value) return null
    const scope = {
        instanceID: value.instanceID.trim(),
        stationSchemeID: value.stationSchemeID.trim(),
        operationPlanID: value.operationPlanID.trim(),
    }
    return Object.values(scope).every(Boolean) ? scope : null
})
const scopeKey = computed(() => normalizedScope.value ? JSON.stringify(normalizedScope.value) : '')
const controlsDisabled = computed(() => props.disabled || loading.value || saving.value || !loaded.value || !normalizedScope.value)
const addDisabled = computed(() => controlsDisabled.value || charts.value.length >= 100)
const activeChart = computed(() => charts.value.find(chart => chart.chartID === activeChartID.value))
const activeSelection = computed(() => activeChart.value?.cellIDs ?? null)
const availableCellIDs = computed(() => uniqueIDs(props.cells.map(cell => cell.id)))
const cellNames = computed(() => new Map(props.cells.map(cell => [cell.id.trim(), cell.name.trim() || cell.id.trim()])))
const draftCellOptions = computed(() => uniqueIDs([...availableCellIDs.value, ...draftCellIDs.value])
    .map(id => ({ id, name: cellNames.value.get(id) || id })))
const currentDialogTitle = computed(() => text(editingChartID.value ? 'editTitle' : 'addTitle'))

function uniqueIDs(values: string[]): string[] {
    return [...new Set(values.map(value => value.trim()).filter(Boolean))]
}

function defaultChart(): Chart {
    return { chartID: crypto.randomUUID(), chartName: text('defaultName'), cellIDs: null }
}

function parseResponse(value: unknown): { isConfigured: boolean; charts: Chart[] } {
    if (!value || typeof value !== 'object') throw new Error('Invalid chart configuration')
    const data = value as Record<string, unknown>
    if (typeof data.isConfigured !== 'boolean' || !Array.isArray(data.charts)) throw new Error('Invalid chart configuration')
    if (!data.isConfigured) return { isConfigured: false, charts: [defaultChart()] }
    const seen = new Set<string>()
    const result = data.charts.map((item: unknown): SavedChart => {
        if (!item || typeof item !== 'object') throw new Error('Invalid chart')
        const chart = item as Record<string, unknown>
        if (typeof chart.chartID !== 'string' || !chart.chartID.trim() || seen.has(chart.chartID.trim())
            || typeof chart.chartName !== 'string' || !chart.chartName.trim() || !Array.isArray(chart.cellIDs)
            || !chart.cellIDs.every(id => typeof id === 'string')) throw new Error('Invalid chart')
        seen.add(chart.chartID.trim())
        return { chartID: chart.chartID.trim(), chartName: chart.chartName.trim(), cellIDs: uniqueIDs(chart.cellIDs as string[]) }
    })
    if (!result.length) throw new Error('A configured list must contain a chart')
    return { isConfigured: true, charts: result }
}

function currentRequest(version: number, key: string) {
    return !disposed && version === requestVersion && key === scopeKey.value
}

async function loadCharts() {
    const scope = normalizedScope.value
    if (!scope || saving.value || props.disabled) return
    const key = scopeKey.value
    const version = ++requestVersion
    loading.value = true
    loadError.value = ''
    try {
        const response = await axios.get('/OperationPlan/GetResourceOccupancyCharts', { params: scope })
        if (!currentRequest(version, key)) return
        const result = parseResponse(response.data)
        charts.value = result.charts
        activeChartID.value = result.charts[0]!.chartID
        loaded.value = true
    } catch {
        if (!currentRequest(version, key)) return
        loadError.value = text('loadFailed')
        loaded.value = false
    } finally {
        if (currentRequest(version, key)) loading.value = false
    }
}

function selectChart(id: string | number) {
    if (controlsDisabled.value || !charts.value.some(chart => chart.chartID === id)) return
    activeChartID.value = String(id)
}

function openEditor(create = false) {
    if (controlsDisabled.value || (create && addDisabled.value)) return
    const chart = create ? undefined : activeChart.value
    if (!create && !chart) return
    editingChartID.value = chart?.chartID || ''
    draftName.value = chart?.chartName || text('newName', { number: charts.value.length + 1 })
    draftFollowsAllCells.value = chart?.cellIDs === null
    draftCellIDs.value = chart ? [...(chart.cellIDs ?? availableCellIDs.value)] : []
    nameError.value = ''
    saveError.value = ''
    pendingSave = null
    dialogVisible.value = true
}

function setDraftCells(ids: string[]) {
    if (controlsDisabled.value) return
    draftFollowsAllCells.value = false
    draftCellIDs.value = uniqueIDs(ids)
}

function moveDraftCell(index: number, offset: number) {
    if (controlsDisabled.value) return
    const target = index + offset
    if (index < 0 || target < 0 || index >= draftCellIDs.value.length || target >= draftCellIDs.value.length) return
    const ids = [...draftCellIDs.value]
    const [id] = ids.splice(index, 1)
    ids.splice(target, 0, id!)
    setDraftCells(ids)
}

async function persistCharts(request: SaveRequest) {
    const scope = normalizedScope.value
    if (!scope || controlsDisabled.value) return
    const key = scopeKey.value
    const version = ++requestVersion
    saving.value = true
    saveError.value = ''
    pendingSave = request
    try {
        const response = await axios.put('/OperationPlan/SaveResourceOccupancyCharts', { ...scope, charts: request.charts })
        if (!currentRequest(version, key)) return
        const result = parseResponse(response.data)
        if (!result.isConfigured) throw new Error('Chart configuration was not saved')
        charts.value = result.charts
        activeChartID.value = result.charts.some(chart => chart.chartID === request.selectedID)
            ? request.selectedID : result.charts[0]!.chartID
        pendingSave = null
        if (request.closeDialog) dialogVisible.value = false
    } catch {
        if (currentRequest(version, key)) saveError.value = text('saveFailed')
    } finally {
        if (currentRequest(version, key)) saving.value = false
    }
}

function savedCharts(): SavedChart[] {
    return charts.value.map(chart => ({ ...chart, cellIDs: [...(chart.cellIDs ?? availableCellIDs.value)] }))
}

async function saveDraft() {
    if (controlsDisabled.value) return
    if (!editingChartID.value && charts.value.length >= 100) return
    const chartName = draftName.value.trim()
    nameError.value = !chartName ? text('nameRequired') : chartName.length > 100 ? text('nameTooLong') : ''
    if (nameError.value) return
    const chartID = editingChartID.value || pendingSave?.selectedID || crypto.randomUUID()
    const updated: SavedChart = { chartID, chartName, cellIDs: [...(draftFollowsAllCells.value ? availableCellIDs.value : draftCellIDs.value)] }
    const next = savedCharts()
    const index = next.findIndex(chart => chart.chartID === chartID)
    if (index >= 0) next[index] = updated
    else next.push(updated)
    await persistCharts({ charts: next, selectedID: chartID, closeDialog: true })
}

async function removeChart(id: string | number) {
    if (controlsDisabled.value || charts.value.length <= 1) return
    const index = charts.value.findIndex(chart => chart.chartID === id)
    if (index < 0) return
    const next = savedCharts().filter(chart => chart.chartID !== id)
    const selectedID = activeChartID.value === id ? next[Math.max(0, index - 1)]!.chartID : activeChartID.value
    await persistCharts({ charts: next, selectedID, closeDialog: false })
}

function retrySave() {
    if (dialogVisible.value) void saveDraft()
    else if (pendingSave) void persistCharts(pendingSave)
}

watch(activeSelection, ids => emit('selection-change', ids === null ? null : [...ids]), { immediate: true })
watch(availableCellIDs, ids => {
    if (dialogVisible.value && draftFollowsAllCells.value) draftCellIDs.value = [...ids]
})
watch(scopeKey, () => {
    requestVersion++
    charts.value = [defaultChart()]
    activeChartID.value = charts.value[0]!.chartID
    loaded.value = false
    loading.value = false
    saving.value = false
    loadError.value = ''
    saveError.value = ''
    dialogVisible.value = false
    pendingSave = null
    void loadCharts()
}, { immediate: true })
watch(() => props.disabled, disabled => {
    if (!disabled && !loaded.value && !loading.value && !loadError.value) void loadCharts()
})
onBeforeUnmount(() => { disposed = true; requestVersion++ })
</script>

<template>
    <div class="resource-occupancy-charts" :aria-busy="loading || saving">
        <el-tabs :model-value="activeChartID" type="card" class="resource-occupancy-charts__tabs"
            @tab-change="selectChart" @tab-remove="removeChart">
            <el-tab-pane v-for="chart in charts" :key="chart.chartID" :name="chart.chartID" :label="chart.chartName"
                :disabled="controlsDisabled" :closable="charts.length > 1 && !controlsDisabled" />
        </el-tabs>
        <div class="resource-occupancy-charts__actions">
            <ActionButton :icon="Edit" :label="text('edit')" :disabled="controlsDisabled" @click="openEditor()" />
            <ActionButton :icon="Plus" :label="text('add')" :disabled="addDisabled" @click="openEditor(true)" />
        </div>
        <span v-if="loading" class="resource-occupancy-charts__status" role="status">{{ text('loading') }}</span>
        <span v-if="loadError || (saveError && !dialogVisible)" class="resource-occupancy-charts__error" role="alert"
            :title="loadError || saveError">{{ loadError || saveError }}</span>
        <ActionButton v-if="loadError || (saveError && !dialogVisible)" :icon="Refresh" :label="text('retry')"
            :disabled="disabled || loading || saving" @click="loadError ? loadCharts() : retrySave()" />

        <el-dialog v-model="dialogVisible" :title="currentDialogTitle" width="min(640px, 94vw)" append-to-body
            :close-on-click-modal="false" :close-on-press-escape="!saving" :show-close="!saving">
            <el-form label-position="top" @submit.prevent="saveDraft">
                <el-form-item :label="text('name')" required :error="nameError">
                    <el-input v-model="draftName" :disabled="controlsDisabled" :placeholder="text('namePlaceholder')" maxlength="100"
                        @input="nameError = ''" />
                </el-form-item>
                <el-form-item :label="text('cells')">
                    <el-select :model-value="draftCellIDs" multiple filterable collapse-tags collapse-tags-tooltip
                        :disabled="controlsDisabled" :placeholder="text('selectCells')" class="resource-occupancy-charts__cell-select"
                        @update:model-value="setDraftCells">
                        <el-option v-for="cell in draftCellOptions" :key="cell.id" :value="cell.id" :label="cell.name" />
                    </el-select>
                </el-form-item>
                <div class="resource-occupancy-charts__selection-actions">
                    <span>{{ text('order') }}</span>
                    <el-button link type="primary" :disabled="controlsDisabled" @click="setDraftCells(availableCellIDs)">{{ text('selectAll') }}</el-button>
                    <el-button link :disabled="controlsDisabled" @click="setDraftCells([])">{{ text('clear') }}</el-button>
                </div>
                <ol v-if="draftCellIDs.length" class="resource-occupancy-charts__cell-order">
                    <li v-for="(id, index) in draftCellIDs" :key="id" class="resource-occupancy-charts__cell-row">
                        <span class="resource-occupancy-charts__cell-index">{{ index + 1 }}</span>
                        <span class="resource-occupancy-charts__cell-label" :title="id">{{ cellNames.get(id) || id }}</span>
                        <ActionButton :icon="ArrowUp" :label="text('moveUp')" :disabled="controlsDisabled || index === 0"
                            @click="moveDraftCell(index, -1)" />
                        <ActionButton :icon="ArrowDown" :label="text('moveDown')" :disabled="controlsDisabled || index === draftCellIDs.length - 1"
                            @click="moveDraftCell(index, 1)" />
                        <ActionButton :icon="Delete" :label="text('removeCell')" :disabled="controlsDisabled"
                            @click="setDraftCells(draftCellIDs.filter(cellID => cellID !== id))" />
                    </li>
                </ol>
                <p v-else class="resource-occupancy-charts__empty">{{ text('emptySelection') }}</p>
                <p class="resource-occupancy-charts__hint">{{ text('orderHint') }}</p>
                <el-alert v-if="saveError" :title="saveError" type="error" :closable="false" show-icon />
            </el-form>
            <template #footer>
                <div class="resource-occupancy-charts__dialog-actions">
                <ActionButton :icon="Close" :label="text('cancel')" variant="icon-text" :disabled="saving" @click="dialogVisible = false" />
                <ActionButton :icon="Check" :label="text(saveError ? 'retry' : 'save')" variant="icon-text" type="primary"
                    :disabled="controlsDisabled" :loading="saving" @click="saveDraft" />
                </div>
            </template>
        </el-dialog>
    </div>
</template>

<style scoped>
.resource-occupancy-charts { display: flex; align-items: center; flex: 1; min-width: 0; gap: 6px; }
.resource-occupancy-charts__tabs { flex: 0 1 auto; min-width: 0; max-width: 100%; }
.resource-occupancy-charts__tabs :deep(.el-tabs__header) { margin: 0; border: 0; }
.resource-occupancy-charts__tabs :deep(.el-tabs__content) { display: none; }
.resource-occupancy-charts__tabs :deep(.el-tabs__nav) { border-radius: 4px; border-bottom: 1px solid var(--el-border-color-light); }
.resource-occupancy-charts__tabs :deep(.el-tabs__item) { height: 30px; padding: 0 12px; font-size: 12px; }
.resource-occupancy-charts__actions { display: flex; flex: 0 0 auto; gap: 4px; }
.resource-occupancy-charts__status, .resource-occupancy-charts__error { font-size: 12px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.resource-occupancy-charts__status { color: var(--el-text-color-secondary); }
.resource-occupancy-charts__error { color: var(--el-color-danger); }
.resource-occupancy-charts__cell-select { width: 100%; }
.resource-occupancy-charts__selection-actions { display: flex; align-items: center; gap: 12px; font-size: 13px; }
.resource-occupancy-charts__selection-actions > span { margin-right: auto; font-weight: 600; }
.resource-occupancy-charts__cell-order { padding: 0; margin: 10px 0; max-height: 310px; overflow-y: auto; border: 1px solid var(--el-border-color-light); border-radius: 4px; list-style: none; }
.resource-occupancy-charts__cell-row { display: flex; align-items: center; gap: 6px; padding: 5px 8px; }
.resource-occupancy-charts__cell-row + .resource-occupancy-charts__cell-row { border-top: 1px solid var(--el-border-color-lighter); }
.resource-occupancy-charts__cell-index { min-width: 22px; color: var(--el-text-color-secondary); font-size: 12px; }
.resource-occupancy-charts__cell-label { flex: 1; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.resource-occupancy-charts__hint, .resource-occupancy-charts__empty { color: var(--el-text-color-secondary); font-size: 12px; }
.resource-occupancy-charts__dialog-actions { display: flex; justify-content: flex-end; gap: 8px; }
</style>
