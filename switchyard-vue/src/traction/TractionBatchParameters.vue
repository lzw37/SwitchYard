<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { defaultRunOptions, type TractionMode } from './tractionCalculation'
import type { TractionWorkspaceContent } from './tractionWorkspace'
import { tractionBatchProfile, setBatchLineParameter, setBatchLineLimit, type TractionBatchContext, type TractionBatchField } from './tractionBatch'
import { cloneTrainPresetCatalog, type TrainPresetCatalog } from './trainPresets'
import { useProjectTrains, type ProjectTrains } from './projectTrains'
import TrainParametersDialog from './TrainParametersDialog.vue'
import LineParametersDialog from './LineParametersDialog.vue'

const workspace = defineModel<TractionWorkspaceContent>({ required: true })
const props = defineProps<{ context: TractionBatchContext; disabled?: boolean; common?: boolean; fields?: TractionBatchField[] }>()
const emit = defineEmits<{ 'trains-updated': [ProjectTrains] }>()
const { locale } = useI18n()
const ui = (zh: string, en: string) => locale.value.startsWith('zh') ? zh : en
const expanded = ref(props.common ? ['train', 'run', 'signal', 'line'] : ['train', 'run', 'signal'])
const show = (field: TractionBatchField) => !props.common || !props.fields || props.fields.includes(field)
const trainVisible = ref(false), lineVisible = ref(false)
const savingTrains = ref(false), trainError = ref('')
const settings = computed(() => workspace.value.settings)
const run = computed(() => settings.value.run)
const selectedTrain = computed(() => workspace.value.catalog.presets.find(p => p.id === workspace.value.catalog.selectedId))
const geometry = computed(() => {
    if (props.common) return null
    try { return tractionBatchProfile(props.context, workspace.value) } catch { return null }
})
function setMode(mode: TractionMode) {
    const defaults = defaultRunOptions(mode)
    Object.assign(run.value, { mode, initialSpeedKmh: defaults.initialSpeedKmh, finalSpeedKmh: defaults.finalSpeedKmh })
}
async function saveCatalog(catalog: TrainPresetCatalog) {
    if (!props.context.saveTrains || savingTrains.value) return
    savingTrains.value = true; trainError.value = ''
    try {
        const project = await props.context.saveTrains(cloneTrainPresetCatalog(catalog))
        emit('trains-updated', project)
        workspace.value = useProjectTrains(workspace.value, project)
        workspace.value.catalog.selectedId = project.catalog.selectedId
        trainVisible.value = false
    } catch (error) { trainError.value = (error as { response?: { data?: { message?: string } }; message?: string }).response?.data?.message || (error as Error).message }
    finally { savingTrains.value = false }
}
function addStop() {
    if (!geometry.value || run.value.stops.length >= 100) return
    run.value.stops.push({ positionM: Math.round(geometry.value.profile.lengthM / 2 * 100) / 100, dwellS: 30 })
}
</script>
<template>
    <el-form :disabled="disabled" class="batch-parameter-form" label-position="top">
        <el-collapse v-model="expanded">
            <el-collapse-item v-if="show('train')" name="train" :title="ui('列车', 'Train')">
                <label class="batch-field">{{ ui('列车预设', 'Train preset') }}
                    <el-select v-model="workspace.catalog.selectedId" :aria-label="ui('批量计算列车预设', 'Batch train preset')">
                        <el-option v-for="preset in workspace.catalog.presets" :key="preset.id" :value="preset.id" :label="locale.startsWith('zh') ? preset.name : preset.nameEn || preset.name" />
                    </el-select>
                </label>
                <div v-if="selectedTrain" class="batch-train-summary">{{ selectedTrain.parameters.lengthM }} m · {{ selectedTrain.parameters.massT }} t · {{ selectedTrain.parameters.maxSpeedKmh }} km/h</div>
                <el-alert v-if="trainError" :title="trainError" type="error" :closable="false" />
                <el-button v-if="context.saveTrains" size="small" @click="trainVisible = true">{{ ui('维护项目列车参数', 'Manage project train parameters') }}</el-button>
            </el-collapse-item>
            <el-collapse-item v-if="show('mode') || show('run')" name="run" :title="show('mode') && show('run') ? ui('作业与运行参数', 'Movement and run') : show('mode') ? ui('作业类型', 'Movement') : ui('运行参数', 'Run parameters')">
                <label v-if="show('mode')" class="batch-field">{{ ui('作业类型', 'Movement') }}
                    <el-select :model-value="run.mode" :aria-label="ui('批量作业类型', 'Batch movement')" @update:model-value="setMode">
                        <el-option value="arrival" :label="ui('接车', 'Arrival')" /><el-option value="departure" :label="ui('发车', 'Departure')" /><el-option value="through" :label="ui('通过', 'Through')" /><el-option value="shunting" :label="ui('调车', 'Shunting')" />
                    </el-select>
                </label>
                <template v-if="show('run')">
                <div class="batch-grid">
                    <label class="batch-field">{{ ui('期望起点速度 (km/h)', 'Desired entry speed (km/h)') }}<el-input-number v-model="run.initialSpeedKmh" :min="0" :max="500" :disabled="disabled || run.mode === 'departure'" controls-position="right" /></label>
                    <label class="batch-field">{{ ui('期望终点速度 (km/h)', 'Desired exit speed (km/h)') }}<el-input-number v-model="run.finalSpeedKmh" :min="0" :max="500" :disabled="disabled || run.mode === 'arrival'" controls-position="right" /></label>
                </div>
                <div v-if="run.mode === 'arrival' || run.mode === 'through'" class="batch-grid">
                    <label class="batch-field">{{ ui('制动距离 (m)', 'Braking distance (m)') }}<el-input-number v-model="run.brakingDistanceM" :min="0" :max="10000000" :precision="2" :step="100" :value-on-clear="null" :placeholder="ui('留空自动计算', 'Automatic when blank')" controls-position="right" /></label>
                    <label class="batch-field">{{ ui('制动段最高速度 (km/h)', 'Braking range max speed (km/h)') }}<el-input-number v-model="run.brakingMaxSpeedKmh" :min=".1" :max="1000" :precision="1" :step="10" :value-on-clear="null" :placeholder="ui('列车最高速度', 'Train maximum')" controls-position="right" /></label>
                </div>
                <div v-if="run.mode === 'departure' || run.mode === 'through'" class="batch-grid">
                    <label class="batch-field">{{ ui('一离去长度 (m)', 'First departure length (m)') }}<el-input-number v-model="run.departureSectionLengthM" :min="0" :max="100000" :precision="1" :step="100" controls-position="right" /></label>
                    <label class="batch-field">{{ ui('一离去最高速度 (km/h)', 'First departure max speed (km/h)') }}<el-input-number v-model="run.departureSectionMaxSpeedKmh" :min=".1" :max="1000" :precision="1" :step="10" :value-on-clear="null" :placeholder="ui('列车最高速度', 'Train maximum')" controls-position="right" /></label>
                </div>
                <div class="batch-grid">
                    <label class="batch-field">{{ ui('计算步长 (m)', 'Position step (m)') }}<el-input-number v-model="run.stepM" :min=".1" :max="100" :step=".5" controls-position="right" /></label>
                    <label class="batch-field">{{ ui('最大计算点数', 'Maximum points') }}<el-input-number v-model="run.maxPointCount" :min="2" :max="100001" :precision="0" :step="1000" controls-position="right" /></label>
                </div>
                </template>
            </el-collapse-item>
            <el-collapse-item v-if="show('signal')" name="signal" :title="ui('信号时间', 'Signal timing')">
                <div class="batch-grid">
                    <label class="batch-field">{{ ui('进路办理 (s)', 'Route setting (s)') }}<el-input-number v-model="settings.signal.routeSettingS" :min="0" :max="86400" :precision="1" controls-position="right" /></label>
                    <label class="batch-field">{{ ui('反应时间 (s)', 'Reaction (s)') }}<el-input-number v-model="settings.signal.reactionS" :min="0" :max="86400" :precision="1" controls-position="right" /></label>
                    <label class="batch-field">{{ ui('释放延迟 (s)', 'Release delay (s)') }}<el-input-number v-model="settings.signal.clearanceS" :min="0" :max="86400" :precision="1" controls-position="right" /></label>
                </div>
            </el-collapse-item>
            <el-collapse-item v-if="show('line')" name="line" :title="ui('线路与距离', 'Line and distance')">
                <label class="batch-field">{{ ui('距离标尺', 'Distance scale') }}<el-radio-group v-model="settings.distanceMode"><el-radio value="auto">{{ ui('自动', 'Automatic') }}</el-radio><el-radio value="manual">{{ ui('人工', 'Manual') }}</el-radio></el-radio-group></label>
                <div class="batch-grid">
                    <label class="batch-field">{{ ui('米 / 画板单位', 'Metres / drawing unit') }}<el-input-number v-model="settings.metersPerUnit" :min=".0001" :max="10000" :precision="4" :step=".1" :disabled="disabled || settings.distanceMode === 'auto'" controls-position="right" /></label>
                    <label class="batch-field">{{ ui('缺省限速 (km/h)', 'Default speed limit (km/h)') }}<el-input-number v-model="settings.defaultLimit" :min=".001" :max="1000" :precision="1" controls-position="right" /></label>
                </div>
                <div v-if="!common" class="batch-line-actions"><span>{{ ui('进路长度', 'Route length') }} {{ geometry?.profile.lengthM.toFixed(1) ?? '—' }} m</span><el-button size="small" :disabled="disabled || !geometry || !!geometry.raw.errors.length" @click="lineVisible = true">{{ ui('坡度、限速与附加阻力', 'Edit line sections') }}</el-button></div>
            </el-collapse-item>
            <el-collapse-item v-if="!common" name="stops" :title="ui('停车设置', 'Stops') + ` (${run.stops.length})`">
                <div v-for="(stop, index) in run.stops" :key="index" class="batch-stop">
                    <div class="batch-grid">
                        <label class="batch-field">{{ ui('位置 (m)', 'Position (m)') }}<el-input-number v-model="stop.positionM" :min="0" :max="geometry?.profile.lengthM ?? 10000000" :precision="2" :controls="false" /></label>
                        <label class="batch-field">{{ ui('停留 (s)', 'Dwell (s)') }}<el-input-number v-model="stop.dwellS" :min="0" :max="86400" :precision="1" :controls="false" /></label>
                    </div>
                    <el-button link type="danger" :aria-label="ui(`删除停车 ${index + 1}`, `Delete stop ${index + 1}`)" @click="run.stops.splice(index, 1)">{{ ui('删除', 'Delete') }}</el-button>
                </div>
                <el-button size="small" :disabled="disabled || !geometry || !!geometry.raw.errors.length || run.stops.length >= 100" @click="addStop">{{ ui('添加停车点', 'Add stop') }}</el-button>
            </el-collapse-item>
        </el-collapse>
    </el-form>
    <TrainParametersDialog v-model="trainVisible" :catalog="workspace.catalog" :saving="savingTrains" :error="trainError" @save="saveCatalog" />
    <LineParametersDialog v-model="lineVisible" :segments="geometry?.rows ?? []" v-model:default-limit="settings.defaultLimit"
        :route-label="[settings.selectedRouteId, settings.continuationId].filter(Boolean).join(' → ')"
        @change="(id, index, field, value) => setBatchLineParameter(context, workspace, id, index, field, value)"
        @limit-change="(start, end, value) => setBatchLineLimit(context, workspace, start, end, value)" />
</template>
<style scoped>
.batch-field { display:flex; flex-direction:column; gap:6px; min-width:0; color:var(--el-text-color-regular); font-size:12px; }
.batch-field :deep(.el-input-number),.batch-field :deep(.el-select) { width:100%; }
.batch-grid { display:grid; grid-template-columns:minmax(0,1fr) minmax(0,1fr); gap:12px; margin-top:12px; }
.batch-train-summary { margin:8px 0; color:var(--el-text-color-secondary); font-size:12px; }
.batch-parameter-form :deep(.el-collapse-item__content) { padding:4px 0 16px; }
.batch-parameter-form :deep(.el-collapse-item__header) { font-size:13px; font-weight:600; }
.batch-stop { display:flex; gap:8px; align-items:flex-end; margin-bottom:12px; }.batch-stop>.batch-grid { flex:1; }.batch-stop>.el-button { margin-bottom:6px; }
.batch-line-actions { display:flex; flex-wrap:wrap; align-items:center; gap:10px; margin-top:12px; font-size:12px; color:var(--el-text-color-secondary); }
</style>
