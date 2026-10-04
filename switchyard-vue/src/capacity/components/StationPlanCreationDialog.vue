<script setup lang="ts">
import { computed, reactive, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { ArrowDown, ArrowUp, Check, Close, Delete, Plus } from '@element-plus/icons-vue'
import ActionButton from '@/components/ui/ActionButton.vue'
import type { StationPlanEditRoute } from './stationPlanEditing.ts'
import { stationPlanTimeLabel } from './stationPlanView.ts'
import { canConfirmStationPlanDraft, createStationPlanDraftMovements, matchingStationPlanDraftRoutes,
    parseStationPlanDraftTime, rematchStationPlanDraftMovement, selectStationPlanDraftRoute,
    STATION_PLAN_DRAFT_MOVEMENT_LIMIT, type StationPlanDraftMovement, type StationPlanDraftTrain } from './stationPlanCreation.ts'

const props = withDefaults(defineProps<{
    modelValue: boolean
    movements: StationPlanDraftMovement[]
    train: StationPlanDraftTrain
    nodes: { id: string; label: string }[]
    routes: StationPlanEditRoute[]
    trainTypes: { id: string; name: string }[]
    saving?: boolean
}>(), { saving: false })
const emit = defineEmits<{
    'update:modelValue': [value: boolean]
    'update:movements': [value: StationPlanDraftMovement[]]
    'update:train': [value: StationPlanDraftTrain]
    repick: [value: { movementID: string; edge: 'start' | 'end' }]
    confirm: []
    cancel: []
}>()
const { t } = useI18n()
type Edge = 'start' | 'end'
const edges: Edge[] = ['start', 'end']
const timeInputs = reactive<Record<string, { text: string; minutes: number | undefined }>>({})
const timeKey = (movementID: string, edge: Edge) => JSON.stringify([movementID, edge])
const formatTime = (minutes: number | undefined) => minutes !== undefined && Number.isFinite(minutes) ? stationPlanTimeLabel(minutes) : ''

// Keep incomplete text while typing; replace it when a graph repick supplies a new time.
watch(() => props.movements, movements => {
    const keys = new Set<string>()
    for (const row of movements) for (const edge of edges) {
        const key = timeKey(row.id, edge), minutes = row[edge]?.timeMinutes
        keys.add(key)
        if (!timeInputs[key] || !Object.is(timeInputs[key].minutes, minutes)) timeInputs[key] = { text: formatTime(minutes), minutes }
    }
    for (const key of Object.keys(timeInputs)) if (!keys.has(key)) delete timeInputs[key]
}, { immediate: true, deep: true })

const canConfirm = computed(() => !props.saving && canConfirmStationPlanDraft(props.movements, props.train.trainNumber))

function handleDialogClose(done: () => void) {
    if (props.saving || !props.modelValue) return
    cancel()
    done()
}
function cancel() {
    if (props.saving) return
    emit('update:modelValue', false)
    emit('cancel')
}
function updateTrain(field: 'trainNumber' | 'name' | 'trainType', value: string) {
    if (!props.saving) emit('update:train', { ...props.train, [field]: String(value ?? '') })
}
function updateMovement(id: string, update: (row: StationPlanDraftMovement) => StationPlanDraftMovement) {
    if (!props.saving) emit('update:movements', props.movements.map(row => row.id === id ? update(row) : row))
}
function updateNode(id: string, edge: Edge, nodeID: string) {
    updateMovement(id, row => rematchStationPlanDraftMovement({ ...row,
        [edge]: { nodeID: String(nodeID ?? ''), timeMinutes: row[edge]?.timeMinutes ?? NaN } }, props.routes))
}
function updateTime(id: string, edge: Edge, text: string) {
    if (props.saving) return
    const minutes = parseStationPlanDraftTime(text) ?? NaN
    timeInputs[timeKey(id, edge)] = { text, minutes }
    updateMovement(id, row => ({ ...row, [edge]: { nodeID: row[edge]?.nodeID || '', timeMinutes: minutes } }))
}
function timeValue(row: StationPlanDraftMovement, edge: Edge) {
    return timeInputs[timeKey(row.id, edge)]?.text ?? formatTime(row[edge]?.timeMinutes)
}
function timeInvalid(row: StationPlanDraftMovement, edge: Edge) {
    const text = timeValue(row, edge)
    return Boolean(text.trim()) && parseStationPlanDraftTime(text) === null
}
function chooseRoute(id: string, routeID: string) {
    updateMovement(id, row => selectStationPlanDraftRoute(row, routeID, props.routes))
}
function routeGroups(row: StationPlanDraftMovement) {
    const matches = matchingStationPlanDraftRoutes(row, props.routes)
    const matchedIDs = new Set(matches.map(route => route.id))
    return [
        { key: 'matchedRoutes', routes: matches },
        { key: 'otherRoutes', routes: props.routes.filter(route => !matchedIDs.has(route.id)) },
    ].filter(group => group.routes.length > 0)
}
function addMovement() {
    if (props.saving || props.movements.length >= STATION_PLAN_DRAFT_MOVEMENT_LIMIT) return
    const row = createStationPlanDraftMovements([{ nodeID: '', timeMinutes: NaN }], props.routes)[0]!
    row.start = null
    row.name = t('stationPlanView.creation.defaultMovement', { index: props.movements.length + 1 })
    emit('update:movements', [...props.movements, row])
}
function deleteMovement(id: string) {
    if (!props.saving) emit('update:movements', props.movements.filter(row => row.id !== id))
}
function moveMovement(index: number, offset: -1 | 1) {
    const destination = index + offset
    if (props.saving || destination < 0 || destination >= props.movements.length) return
    const movements = [...props.movements]
    ;[movements[index], movements[destination]] = [movements[destination]!, movements[index]!]
    emit('update:movements', movements)
}
function repick(movementID: string, edge: Edge) {
    if (!props.saving) emit('repick', { movementID, edge })
}
function confirm() {
    if (canConfirm.value) emit('confirm')
}
</script>

<template>
    <el-dialog :model-value="modelValue" :before-close="handleDialogClose" :title="t('stationPlanView.creation.title')" width="min(1240px, 96vw)"
        :close-on-click-modal="!saving" :close-on-press-escape="!saving" :show-close="!saving">
        <div class="station-plan-creation">
            <p class="creation-help">{{ t('stationPlanView.creation.description') }}</p>
            <el-form label-position="top" :disabled="saving" class="creation-train-fields" @submit.prevent>
                <el-form-item :label="t('stationPlanView.creation.trainNumber')" required>
                    <el-input :model-value="train.trainNumber" :aria-label="t('stationPlanView.creation.trainNumber')"
                        @update:model-value="updateTrain('trainNumber', $event)" />
                </el-form-item>
                <el-form-item :label="t('stationPlanView.creation.trainName')">
                    <el-input :model-value="train.name" :aria-label="t('stationPlanView.creation.trainName')"
                        @update:model-value="updateTrain('name', $event)" />
                </el-form-item>
                <el-form-item :label="t('stationPlanView.creation.trainType')">
                    <el-select :model-value="train.trainType" filterable allow-create default-first-option clearable
                        :aria-label="t('stationPlanView.creation.trainType')" @update:model-value="updateTrain('trainType', $event)">
                        <el-option v-for="type in trainTypes" :key="type.id" :label="type.name" :value="type.id" />
                    </el-select>
                </el-form-item>
            </el-form>
            <div class="creation-movements-header">
                <strong>{{ t('stationPlanView.creation.movements') }}</strong>
                <ActionButton variant="icon-text" :icon="Plus" :label="t('stationPlanView.creation.addMovement')"
                    :disabled="saving || movements.length >= STATION_PLAN_DRAFT_MOVEMENT_LIMIT" @click="addMovement" />
            </div>
            <el-table :data="movements" row-key="id" size="small" max-height="440" class="creation-movements">
                <el-table-column type="index" width="45" />
                <el-table-column :label="t('stationPlanView.creation.movementName')" min-width="155">
                    <template #default="{ row }">
                        <el-input :model-value="row.name" :disabled="saving" :aria-label="t('stationPlanView.creation.movementName')"
                            @update:model-value="updateMovement(row.id, value => ({ ...value, name: $event }))" />
                    </template>
                </el-table-column>
                <el-table-column v-for="edge in edges" :key="edge" :label="t(`stationPlanView.creation.${edge}`)" min-width="240">
                    <template #default="{ row }">
                        <div class="creation-endpoint">
                            <el-select :model-value="row[edge]?.nodeID || ''" filterable clearable :disabled="saving"
                                :placeholder="t('stationPlanView.creation.node')" :aria-label="`${t(`stationPlanView.creation.${edge}`)} ${t('stationPlanView.creation.node')}`"
                                @update:model-value="updateNode(row.id, edge, $event)">
                                <el-option v-for="node in nodes" :key="node.id" :label="node.label" :value="node.id" />
                            </el-select>
                            <div class="creation-time">
                                <el-input :model-value="timeValue(row, edge)" :disabled="saving" :class="{ 'is-invalid': timeInvalid(row, edge) }"
                                    :placeholder="t('stationPlanView.creation.timePlaceholder')" :aria-invalid="timeInvalid(row, edge)"
                                    :aria-label="`${t(`stationPlanView.creation.${edge}`)} ${t('stationPlanView.creation.time')}`"
                                    @update:model-value="updateTime(row.id, edge, $event)" />
                                <ActionButton variant="text" :label="t('stationPlanView.creation.repick')" :disabled="saving" @click="repick(row.id, edge)" />
                            </div>
                        </div>
                    </template>
                </el-table-column>
                <el-table-column :label="t('stationPlanView.creation.route')" min-width="220">
                    <template #default="{ row }">
                        <el-select :model-value="row.routeID" filterable clearable :disabled="saving"
                            :placeholder="t('stationPlanView.creation.selectRoute')" :aria-label="t('stationPlanView.creation.route')"
                            @update:model-value="chooseRoute(row.id, $event)">
                            <el-option-group v-for="group in routeGroups(row)" :key="group.key" :label="t(`stationPlanView.creation.${group.key}`)">
                                <el-option v-for="route in group.routes" :key="route.id" :value="route.id" :label="route.name ? `${route.name} (${route.id})` : route.id" />
                            </el-option-group>
                        </el-select>
                        <span v-if="!row.routeID" class="creation-unmatched">{{ t('stationPlanView.creation.unmatched') }}</span>
                    </template>
                </el-table-column>
                <el-table-column width="118" fixed="right">
                    <template #default="{ row, $index }">
                        <div class="creation-row-actions">
                            <ActionButton :icon="ArrowUp" :label="t('stationPlanView.creation.moveUp')" :disabled="saving || $index === 0" @click="moveMovement($index, -1)" />
                            <ActionButton :icon="ArrowDown" :label="t('stationPlanView.creation.moveDown')" :disabled="saving || $index === movements.length - 1" @click="moveMovement($index, 1)" />
                            <ActionButton :icon="Delete" type="danger" :label="t('stationPlanView.creation.deleteMovement')" :disabled="saving" @click="deleteMovement(row.id)" />
                        </div>
                    </template>
                </el-table-column>
            </el-table>
            <p class="creation-help">{{ t('stationPlanView.creation.timeHint') }}</p>
            <p v-if="movements.length > STATION_PLAN_DRAFT_MOVEMENT_LIMIT" class="creation-validation" role="status">
                {{ t('stationPlanView.creation.tooManyMovements', { limit: STATION_PLAN_DRAFT_MOVEMENT_LIMIT }) }}
            </p>
            <p v-else-if="!canConfirm && !saving" class="creation-validation" role="status">{{ t('stationPlanView.creation.validation') }}</p>
        </div>
        <template #footer>
            <div class="dialog-actions">
                <ActionButton variant="text" :icon="Close" :label="t('stationPlanView.creation.cancel')" :disabled="saving" @click="cancel" />
                <ActionButton variant="text" :icon="Check" type="primary" :label="t('stationPlanView.creation.confirm')" :disabled="!canConfirm" :loading="saving" @click="confirm" />
            </div>
        </template>
    </el-dialog>
</template>

<style scoped>
.station-plan-creation { display: flex; flex-direction: column; gap: 12px; }
.creation-help { margin: 0; color: var(--el-text-color-secondary); font-size: 13px; }
.creation-train-fields { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 16px; }
.creation-train-fields :deep(.el-form-item) { margin-bottom: 0; }
.creation-movements-header { display: flex; align-items: center; justify-content: space-between; gap: 12px; }
.creation-endpoint { display: flex; flex-direction: column; gap: 6px; padding: 5px 0; }
.creation-time { display: flex; align-items: center; gap: 6px; }
.creation-time > .el-input { min-width: 0; }
.creation-time :deep(.el-input__inner) { font-variant-numeric: tabular-nums; }
.creation-time :deep(.is-invalid .el-input__wrapper) { box-shadow: 0 0 0 1px var(--el-color-danger) inset; }
.creation-unmatched { display: block; color: var(--el-color-warning); font-size: 12px; margin-top: 4px; }
.creation-validation { margin: 0; color: var(--el-color-warning); font-size: 13px; }
.creation-row-actions, .dialog-actions { display: flex; align-items: center; gap: 5px; }
.dialog-actions { justify-content: flex-end; gap: 8px; }
@media (max-width: 650px) { .creation-train-fields { grid-template-columns: 1fr; gap: 10px; } }
</style>
