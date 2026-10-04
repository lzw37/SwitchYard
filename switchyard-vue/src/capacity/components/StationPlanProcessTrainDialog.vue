<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { Check, Close, Refresh, View } from '@element-plus/icons-vue'
import ActionButton from '@/components/ui/ActionButton.vue'
import type { ProcessCatalog, ProcessTemplate } from '../operationProcess'
import { parseStationPlanDraftTime } from './stationPlanCreation'
import { processTrainActivityRows, updateProcessTrainSelection, validateProcessTrainSelections,
    type ProcessTrainActivitySelection, type ProcessTrainCreationForm, type ProcessTrainPreview } from './processTrainCreation'

const props = defineProps<{
    modelValue: boolean
    sources: ProcessTemplate[]
    sourceID: string
    form: ProcessTrainCreationForm
    catalog: ProcessCatalog
    selections: ProcessTrainActivitySelection[]
    trainTypes: { id: string; name: string }[]
    loading: boolean
    loadError: string
    previewing: boolean
    saving: boolean
    preview: ProcessTrainPreview | null
    previewFresh: boolean
    error: string
}>()
const emit = defineEmits<{
    'update:sourceID': [value: string]
    'update:form': [value: ProcessTrainCreationForm]
    'update:selections': [value: ProcessTrainActivitySelection[]]
    preview: []
    confirm: []
    reload: []
    cancel: []
}>()
const { t } = useI18n()
const source = computed(() => props.sources.find(item => item.id === props.sourceID) || null)
const busy = computed(() => props.previewing || props.saving)
const activityRows = computed(() => source.value ? processTrainActivityRows(source.value, props.catalog, props.selections) : [])
const selectionErrors = computed(() => source.value ? validateProcessTrainSelections(source.value, props.catalog, props.selections) : [])
const invalidActivityIDs = computed(() => new Set(selectionErrors.value.map(error => error.activityID)))
function validTime(text: string, origin = false) {
    const minutes = parseStationPlanDraftTime(text)
    return minutes !== null && minutes >= 0 && (origin ? minutes < 7 * 1440 : minutes <= 7 * 1440)
}
const formValid = computed(() => Boolean(props.form.id.trim() && props.form.trainNumber.trim()) &&
    validTime(props.form.originTime, true) && validTime(props.form.endTime))
const inputsValid = computed(() => !!source.value && activityRows.value.length > 0 && formValid.value && selectionErrors.value.length === 0)
const canPreview = computed(() => props.modelValue && !busy.value && !props.loading && !props.loadError && inputsValid.value)
const canConfirm = computed(() => canPreview.value && !!props.preview && props.previewFresh)
const previewMovements = computed(() => new Map(props.preview?.movements.map(movement => [movement.movementID, movement]) || []))
function previewMovement(activityID: string) {
    const movementID = props.preview?.processConstraint.activityMovementMap[activityID]
    return movementID ? previewMovements.value.get(movementID) : undefined
}
function routeName(id: string) { return props.catalog.routes.find(route => route.id === id)?.name || id }
function trackName(id: string) { return props.catalog.tracks.find(track => track.id === id)?.name || id }
function previewTrack(activityID: string) {
    const trackID = props.preview?.processConstraint.selectedTrackIDs[activityID]
    return trackID ? trackName(trackID) : ''
}
function selectSource(value: string) {
    if (busy.value || props.loading) return
    const id = String(value ?? '')
    if (!id || props.sources.some(item => item.id === id)) emit('update:sourceID', id)
}
function updateForm(field: Exclude<keyof ProcessTrainCreationForm, 'id'>, value: string) {
    if (!busy.value && !props.loading) emit('update:form', { ...props.form, [field]: String(value ?? '') })
}
function updateSelection(activityID: string, field: 'routeID' | 'trackID', value: string) {
    if (busy.value || props.loading || !source.value) return
    const selections = updateProcessTrainSelection(source.value, props.catalog, props.selections, activityID, field, String(value ?? ''))
    if (JSON.stringify(selections) !== JSON.stringify(props.selections)) emit('update:selections', selections)
}
function previewPlan() { if (canPreview.value) emit('preview') }
function confirm() { if (canConfirm.value) emit('confirm') }
function reload() { if (!busy.value && !props.loading) emit('reload') }
function cancel() { if (!props.saving && props.modelValue) emit('cancel') }
function handleDialogClose(done: () => void) {
    if (props.saving || !props.modelValue) return
    cancel()
    done()
}
</script>

<template>
    <el-dialog :model-value="modelValue" :before-close="handleDialogClose" :title="t('stationPlanView.fromProcess.title')"
        width="min(1240px, 96vw)" :close-on-click-modal="!saving" :close-on-press-escape="!saving" :show-close="!saving">
        <div class="process-train-creation">
            <p class="process-train-help">{{ t('stationPlanView.fromProcess.description') }}</p>
            <div class="process-train-source">
                <label for="station-plan-process-source">{{ t('stationPlanView.fromProcess.source') }}</label>
                <el-select id="station-plan-process-source" :model-value="sourceID" :loading="loading" :disabled="busy || loading"
                    filterable clearable :placeholder="t('stationPlanView.fromProcess.selectSource')" :aria-label="t('stationPlanView.fromProcess.source')"
                    data-field="sourceID" @update:model-value="selectSource">
                    <el-option v-for="item in sources" :key="item.id" :value="item.id" :label="item.name" />
                </el-select>
                <ActionButton variant="icon-text" :icon="Refresh" :label="t('stationPlanView.fromProcess.reload')"
                    :disabled="busy || loading" :loading="loading" @click="reload" />
            </div>
            <p v-if="loading" class="process-train-help" role="status">{{ t('stationPlanView.fromProcess.loadingSources') }}</p>
            <el-alert v-if="loadError" :title="loadError" class="process-train-error" type="error" :closable="false" show-icon />
            <el-empty v-else-if="!loading && sources.length === 0" :image-size="64" :description="t('stationPlanView.fromProcess.emptySources')" />
            <template v-if="source">
                <p v-if="source.description" class="process-train-help">{{ source.description }}</p>
                <el-form label-position="top" :disabled="busy || loading" class="process-train-fields" @submit.prevent>
                    <el-form-item :label="t('stationPlanView.creation.trainNumber')" required>
                        <el-input :model-value="form.trainNumber" data-field="trainNumber" :aria-label="t('stationPlanView.creation.trainNumber')"
                            @update:model-value="updateForm('trainNumber', $event)" />
                    </el-form-item>
                    <el-form-item :label="t('stationPlanView.creation.trainName')">
                        <el-input :model-value="form.name" data-field="name" :aria-label="t('stationPlanView.creation.trainName')" @update:model-value="updateForm('name', $event)" />
                    </el-form-item>
                    <el-form-item :label="t('stationPlanView.creation.trainType')">
                        <el-select :model-value="form.trainType" filterable allow-create default-first-option clearable data-field="trainType"
                            :aria-label="t('stationPlanView.creation.trainType')" @update:model-value="updateForm('trainType', $event)">
                            <el-option v-for="type in trainTypes" :key="type.id" :label="type.name" :value="type.id" />
                        </el-select>
                    </el-form-item>
                    <el-form-item :label="t('stationPlanView.fromProcess.originTime')" required>
                        <el-input :model-value="form.originTime" data-field="originTime" :aria-invalid="!validTime(form.originTime, true)"
                            :aria-label="t('stationPlanView.fromProcess.originTime')" :placeholder="t('stationPlanView.creation.timePlaceholder')"
                            @update:model-value="updateForm('originTime', $event)" />
                    </el-form-item>
                    <el-form-item :label="t('stationPlanView.fromProcess.endTime')" required>
                        <el-input :model-value="form.endTime" data-field="endTime" :aria-invalid="!validTime(form.endTime)"
                            :aria-label="t('stationPlanView.fromProcess.endTime')" :placeholder="t('stationPlanView.creation.timePlaceholder')"
                            @update:model-value="updateForm('endTime', $event)" />
                    </el-form-item>
                </el-form>
                <p class="process-train-help">{{ t('stationPlanView.fromProcess.timeRangeHint') }}</p>
                <strong>{{ t('stationPlanView.fromProcess.activities') }}</strong>
                <el-table :data="activityRows" row-key="activity.id" size="small" max-height="460" class="process-train-activities">
                    <el-table-column type="index" width="44" />
                    <el-table-column :label="t('stationPlanView.fromProcess.activities')" min-width="170">
                        <template #default="{ row }">
                            <div class="process-train-activity-name">{{ row.activity.name }}</div>
                            <el-tag size="small" effect="plain" :class="`process-type-${row.activity.type.toLowerCase()}`">
                                {{ t(`operationPlan.trainOperationPlan.fromProcess.types.${row.activity.type}`) }}
                            </el-tag>
                        </template>
                    </el-table-column>
                    <el-table-column :label="t('stationPlanView.fromProcess.duration')" width="116">
                        <template #default="{ row }">{{ row.activity.minDuration }} – {{ row.activity.maxDuration }}</template>
                    </el-table-column>
                    <el-table-column :label="t('stationPlanView.fromProcess.route')" min-width="300">
                        <template #default="{ row }">
                            <div class="process-train-choices" :class="{ 'is-invalid': invalidActivityIDs.has(row.activity.id) }">
                                <template v-if="row.activity.type === 'Dwelling'">
                                    <div class="process-train-choice-label">{{ t('stationPlanView.fromProcess.track') }}
                                        <span v-if="row.trackLocked" class="process-train-fixed">{{ t('stationPlanView.fromProcess.fixedSelection') }}</span>
                                    </div>
                                    <el-select :model-value="row.selection.trackID" filterable clearable :disabled="busy || loading || row.trackLocked"
                                        :data-activity-id="row.activity.id" data-choice="trackID" :aria-invalid="invalidActivityIDs.has(row.activity.id)"
                                        :aria-label="`${row.activity.name} ${t('stationPlanView.fromProcess.track')}`" :placeholder="t('stationPlanView.fromProcess.selectTrack')"
                                        @update:model-value="updateSelection(row.activity.id, 'trackID', $event)">
                                        <el-option v-for="track in row.trackOptions" :key="track.id" :value="track.id" :label="track.name || track.id" />
                                    </el-select>
                                    <div class="process-train-choice-label">{{ t('stationPlanView.fromProcess.dwellingRoute') }}</div>
                                    <el-select :model-value="row.selection.routeID" filterable clearable :disabled="busy || loading || !row.selection.trackID"
                                        :data-activity-id="row.activity.id" data-choice="routeID" :aria-label="`${row.activity.name} ${t('stationPlanView.fromProcess.dwellingRoute')}`"
                                        :placeholder="t('stationPlanView.fromProcess.noDwellingRoute')" @update:model-value="updateSelection(row.activity.id, 'routeID', $event)">
                                        <el-option v-for="route in row.routeOptions" :key="route.id" :value="route.id" :label="route.name || route.id" />
                                    </el-select>
                                </template>
                                <template v-else>
                                    <span v-if="row.routeLocked" class="process-train-fixed">{{ t('stationPlanView.fromProcess.fixedSelection') }}</span>
                                    <el-select :model-value="row.selection.routeID" filterable clearable :disabled="busy || loading || row.routeLocked"
                                        :data-activity-id="row.activity.id" data-choice="routeID" :aria-invalid="invalidActivityIDs.has(row.activity.id)"
                                        :aria-label="`${row.activity.name} ${t('stationPlanView.fromProcess.route')}`" :placeholder="t('stationPlanView.fromProcess.selectRoute')"
                                        @update:model-value="updateSelection(row.activity.id, 'routeID', $event)">
                                        <el-option v-for="route in row.routeOptions" :key="route.id" :value="route.id" :label="route.name || route.id" />
                                    </el-select>
                                </template>
                                <span v-if="invalidActivityIDs.has(row.activity.id)" class="process-train-invalid">{{ t('stationPlanView.fromProcess.missingChoices') }}</span>
                                <template v-if="previewMovement(row.activity.id)">
                                    <span class="process-train-help">{{ t('stationPlanView.fromProcess.previewRoute') }}:
                                        {{ routeName(previewMovement(row.activity.id)?.route || '') || t('stationPlanView.fromProcess.noDwellingRoute') }}</span>
                                    <span v-if="previewTrack(row.activity.id)" class="process-train-help">{{ t('stationPlanView.fromProcess.previewTrack') }}: {{ previewTrack(row.activity.id) }}</span>
                                </template>
                            </div>
                        </template>
                    </el-table-column>
                    <el-table-column :label="t('stationPlanView.fromProcess.previewStart')" min-width="132">
                        <template #default="{ row }"><span class="process-train-preview-time" :class="{ 'is-stale': !previewFresh }">{{ previewMovement(row.activity.id)?.earliestStartTime || '—' }}</span></template>
                    </el-table-column>
                    <el-table-column :label="t('stationPlanView.fromProcess.previewEnd')" min-width="132">
                        <template #default="{ row }"><span class="process-train-preview-time" :class="{ 'is-stale': !previewFresh }">{{ previewMovement(row.activity.id)?.latestEndTime || '—' }}</span></template>
                    </el-table-column>
                </el-table>
                <p v-if="!inputsValid" class="process-train-invalid" role="status">{{ t('stationPlanView.fromProcess.validation') }}</p>
                <p v-if="preview" class="process-train-preview-status" :class="{ 'is-stale': !previewFresh }" role="status">
                    {{ t(previewFresh ? 'stationPlanView.fromProcess.previewReady' : 'stationPlanView.fromProcess.previewStale') }}
                </p>
                <p v-else class="process-train-help" role="status">{{ t('stationPlanView.fromProcess.unpreviewed') }}</p>
            </template>
            <el-alert v-if="error" :title="t('stationPlanView.fromProcess.constraintErrors')" type="error" :closable="false" show-icon>
                <p class="process-train-error">{{ error }}</p>
            </el-alert>
            <el-alert v-if="preview?.warnings.length" :title="t('stationPlanView.fromProcess.warnings')" type="warning" :closable="false" show-icon>
                <ul class="process-train-warnings"><li v-for="(warning, index) in preview.warnings" :key="index">{{ warning }}</li></ul>
            </el-alert>
        </div>
        <template #footer>
            <div class="process-train-actions">
                <ActionButton variant="text" :icon="Close" :label="t('stationPlanView.creation.cancel')" :disabled="saving" @click="cancel" />
                <ActionButton variant="icon-text" :icon="View" :label="t('stationPlanView.fromProcess.preview')" :disabled="!canPreview" :loading="previewing" @click="previewPlan" />
                <ActionButton variant="text" :icon="Check" type="primary" :label="t('stationPlanView.creation.confirm')" :disabled="!canConfirm" :loading="saving" @click="confirm" />
            </div>
        </template>
    </el-dialog>
</template>

<style scoped>
.process-train-creation { display: flex; flex-direction: column; gap: 12px; }
.process-train-help { margin: 0; color: var(--el-text-color-secondary); font-size: 13px; }
.process-train-source { display: flex; align-items: center; gap: 12px; }
.process-train-source > label { white-space: nowrap; }
.process-train-source > .el-select { flex: 1; min-width: 120px; }
.process-train-fields { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 12px 16px; }
.process-train-fields :deep(.el-form-item) { margin-bottom: 0; }
.process-train-activity-name { margin-bottom: 5px; }
.process-train-choices { display: flex; flex-direction: column; gap: 5px; padding: 5px 0; }
.process-train-choice-label, .process-train-fixed { font-size: 12px; color: var(--el-text-color-secondary); }
.process-train-fixed { margin-left: 4px; color: var(--el-color-primary); }
.process-train-invalid, .process-train-preview-status.is-stale { margin: 0; color: var(--el-color-warning); font-size: 13px; }
.process-train-choices.is-invalid :deep(.el-select__wrapper) { box-shadow: 0 0 0 1px var(--el-color-warning) inset; }
.process-train-preview-time { font-variant-numeric: tabular-nums; white-space: nowrap; }
.process-train-preview-time.is-stale { color: var(--el-text-color-secondary); }
.process-train-preview-status { margin: 0; color: var(--el-color-success); font-size: 13px; }
.process-train-error, .process-train-warnings { margin: 0; white-space: pre-line; overflow-wrap: anywhere; }
.process-train-warnings { padding-left: 18px; }
.process-train-actions { display: flex; justify-content: flex-end; gap: 8px; }
.process-type-arrival { --el-tag-text-color: #3374c8; --el-tag-border-color: #a4c5ee; --el-tag-bg-color: #edf5ff; }
.process-type-departure { --el-tag-text-color: #268263; --el-tag-border-color: #a5d6bf; --el-tag-bg-color: #eef9f4; }
.process-type-shunting { --el-tag-text-color: #ad7b24; --el-tag-border-color: #e7c992; --el-tag-bg-color: #fff7e9; }
.process-type-locomotive { --el-tag-text-color: #8360c1; --el-tag-border-color: #c6b6eb; --el-tag-bg-color: #f4f0ff; }
.process-type-dwelling { --el-tag-text-color: #657e98; --el-tag-border-color: #b9c8d8; --el-tag-bg-color: #f0f5fa; }
@media (max-width: 650px) {
    .process-train-fields { grid-template-columns: 1fr; }
    .process-train-source { flex-wrap: wrap; gap: 8px; }
}
</style>
