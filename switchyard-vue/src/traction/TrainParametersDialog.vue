<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { generateBrakingCurve, generateResistanceCurve, rotatingMassCoefficientFromFactor, rotatingMassFactorFromCoefficient, type ForcePoint, type TrainParameters } from './tractionCalculation'
import { cloneTrainPresetCatalog, cloneTrainParameters, validateTrainPresetCatalog, type TrainPreset, type TrainPresetCatalog } from './trainPresets'

const props = defineProps<{ modelValue: boolean; catalog: TrainPresetCatalog; saving?: boolean }>()
const emit = defineEmits<{
    'update:modelValue': [value: boolean]
    save: [value: TrainPresetCatalog]
}>()
const { locale } = useI18n()
const ui = (zh: string, en: string) => locale.value.startsWith('zh') ? zh : en
const draft = ref<TrainPresetCatalog>(cloneTrainPresetCatalog(props.catalog))
const activeTab = ref('tractionCurve')
watch(() => props.modelValue, open => {
    if (open) {
        draft.value = cloneTrainPresetCatalog(props.catalog)
        activeTab.value = 'tractionCurve'
    }
}, { immediate: true })
const current = computed(() => draft.value.presets.find(preset => preset.id === draft.value.selectedId))
const presetName = (preset: TrainPreset) => !locale.value.startsWith('zh') && preset.nameEn ? preset.nameEn : preset.name

type CurveField = 'tractionCurve' | 'brakingCurve' | 'resistanceCurve'
type NumericField = Exclude<keyof TrainParameters, CurveField>
const curveTabs: { key: CurveField; zh: string; en: string; force: string }[] = [
    { key: 'tractionCurve', zh: '牵引特性', en: 'Traction', force: '牵引力' },
    { key: 'brakingCurve', zh: '制动特性', en: 'Braking', force: '制动力' },
    { key: 'resistanceCurve', zh: '阻力特性', en: 'Resistance', force: '基本阻力' },
]
const basicFields: { key: NumericField; zh: string; en: string; min: number; max: number; step: number; precision: number }[] = [
    { key: 'lengthM', zh: '列车长度 (m)', en: 'Train length (m)', min: 0.001, max: 100000, step: 1, precision: 3 },
    { key: 'massT', zh: '列车质量 (t)', en: 'Train mass (t)', min: 0.001, max: 10000000, step: 1, precision: 3 },
    { key: 'maxSpeedKmh', zh: '最高速度 (km/h)', en: 'Maximum speed (km/h)', min: 0.001, max: 1000, step: 1, precision: 3 },
    { key: 'rotatingMassFactor', zh: '回转质量系数 γ', en: 'Rotating mass coefficient γ', min: 0, max: 2, step: 0.01, precision: 4 },
]
const resistanceRows: { field: 'resistanceA' | 'resistanceB' | 'resistanceC'; coefficient: string; unit: string; step: number; precision: number }[] = [
    { field: 'resistanceA', coefficient: 'A', unit: 'kN', step: 1, precision: 4 },
    { field: 'resistanceB', coefficient: 'B', unit: 'kN / (km/h)', step: 0.01, precision: 6 },
    { field: 'resistanceC', coefficient: 'C', unit: 'kN / (km/h)²', step: 0.001, precision: 8 },
]
const finiteRange = (value: number, min: number, max: number) => Number.isFinite(value) && value >= min && value <= max
const inputValue = (value: number) => Number.isFinite(value) ? value : undefined
const basicFieldValue = (train: TrainParameters, field: NumericField) =>
    field === 'rotatingMassFactor' ? rotatingMassCoefficientFromFactor(train.rotatingMassFactor) : train[field]
function presetIssue(preset: TrainPreset) {
    if (!preset.name.trim() || Array.from(preset.name).length > 80) return ui('名称须为 1–80 个字符。', 'Enter a name with 1–80 characters.')
    for (const field of basicFields) {
        if (!finiteRange(basicFieldValue(preset.parameters, field.key), field.min, field.max)) {
            return ui(`${field.zh}须为 ${field.min}–${field.max} 的数值，不能为空。`, `${field.en} must be a number from ${field.min} to ${field.max}; it cannot be empty.`)
        }
    }
    if (!finiteRange(preset.parameters.maxBrakeForceKn, 0.001, 10000000)) return ui('常用制动力须为 0.001–10000000 kN，不能为空。', 'Service braking force must be 0.001–10000000 kN and cannot be empty.')
    for (const row of resistanceRows) {
        if (!finiteRange(preset.parameters[row.field], 0, 10000000)) return ui(`阻力系数 ${row.coefficient} 须为 0–10000000 的数值，不能为空。`, `Resistance coefficient ${row.coefficient} must be a number from 0 to 10000000 and cannot be empty.`)
    }
    for (const tab of curveTabs) {
        const curve = preset.parameters[tab.key]
        const label = ui(tab.zh, tab.en)
        if (curve.length < 1 || curve.length > 128) return ui(label + '须保留 1–128 个节点。', label + ' requires 1–128 points.')
        for (let index = 0; index < curve.length; index++) {
            const point = curve[index]!
            if (!finiteRange(point.speedKmh, 0, 10000) || !finiteRange(point.forceKn, 0, 10000000))
                return ui(label + '：速度须为 0–10000 km/h，力须为 0–10000000 kN，不能为空。', label + ': invalid speed or force.')
            if ((index === 0 && point.speedKmh !== 0) || (index > 0 && point.speedKmh <= curve[index - 1]!.speedKmh))
                return ui(label + '：首速度须为 0，速度须严格递增。', label + ': speeds must start at zero and strictly increase.')
            if (tab.key === 'tractionCurve' && index > 0 && point.forceKn > curve[index - 1]!.forceKn)
                return ui('牵引力不可增大。', 'Traction force must not increase.')
        }
    }
    return ''
}
const validationMessage = computed(() => {
    for (const preset of draft.value.presets) {
        const issue = presetIssue(preset)
        if (issue) return `${presetName(preset).trim() || ui('未命名', 'Untitled')}：${issue}`
    }
    const code = validateTrainPresetCatalog(draft.value)
    if (!code) return ''
    if (code === 'duplicate-preset-name') return ui('列车参数名称不能重复。', 'Train parameter names must be unique.')
    if (code === 'invalid-preset-name') return ui('列车参数名称须为 1–80 个字符。', 'Train parameter names must contain 1–80 characters.')
    if (code === 'invalid-preset-count') return ui('须保留 1–100 组列车参数。', 'Keep 1–100 train parameter presets.')
    if (code === 'invalid-selected-preset') return ui('请选择一组列车参数。', 'Select a train parameter preset.')
    return ui('列车参数数据无效，请检查预设。', 'Invalid train parameter data. Check the presets.')
})
function setName(value: string) {
    if (!current.value) return
    current.value.name = value
    delete current.value.nameEn
}
function setNumber(field: NumericField, value: number | undefined | null) {
    if (!current.value) return
    const number = value ?? Number.NaN
    current.value.parameters[field] = field === 'rotatingMassFactor' ? rotatingMassFactorFromCoefficient(number) : number
}
function setPoint(key: CurveField, index: number, field: keyof ForcePoint, value: number | undefined | null) {
    const point = current.value?.parameters[key][index]
    if (point) point[field] = value ?? Number.NaN
}
function addPoint(key: CurveField) {
    const curve = current.value?.parameters[key]
    if (!curve || curve.length >= 128) return
    const last = curve[curve.length - 1]
    curve.push({ speedKmh: last ? Math.min(10000, last.speedKmh + 20) : 0, forceKn: last?.forceKn ?? 100 })
}
function removePoint(key: CurveField, index: number) {
    const curve = current.value?.parameters[key]
    if (curve && curve.length > 1) curve.splice(index, 1)
}
function generatePoints(key: CurveField) {
    const train = current.value?.parameters
    if (!train) return
    if (key === 'brakingCurve') train.brakingCurve = generateBrakingCurve(train)
    if (key === 'resistanceCurve') train.resistanceCurve = generateResistanceCurve(train)
}
const canGenerate = computed(() => {
    const train = current.value?.parameters
    if (!train || !finiteRange(train.maxSpeedKmh, .001, 1000)) return false
    if (activeTab.value === 'brakingCurve') return finiteRange(train.maxBrakeForceKn, .001, 10000000)
    return resistanceRows.every(row => finiteRange(train[row.field], 0, 10000000))
        && finiteRange(train.resistanceA + train.resistanceB * train.maxSpeedKmh + train.resistanceC * train.maxSpeedKmh ** 2, 0, 10000000)
})
function addPreset() {
    if (!current.value || draft.value.presets.length >= 100) return
    let suffix = 1
    let name = ui('新建列车参数', 'New train parameters')
    while (draft.value.presets.some(preset => preset.name.trim().toLowerCase() === name.toLowerCase() || preset.nameEn?.trim().toLowerCase() === name.toLowerCase())) {
        suffix++
        name = ui(`新建列车参数 ${suffix}`, `New train parameters ${suffix}`)
    }
    let id: string
    do { id = globalThis.crypto?.randomUUID?.() ?? `train-${Date.now()}-${Math.random().toString(36).slice(2)}` }
    while (draft.value.presets.some(preset => preset.id === id))
    draft.value.presets.push({ id, name, parameters: cloneTrainParameters(current.value.parameters) })
    draft.value.selectedId = id
}
function removePreset() {
    if (draft.value.presets.length <= 1) return
    const index = draft.value.presets.findIndex(preset => preset.id === draft.value.selectedId)
    if (index < 0) return
    draft.value.presets.splice(index, 1)
    draft.value.selectedId = draft.value.presets[Math.min(index, draft.value.presets.length - 1)]!.id
}
function save() {
    if (props.saving || validationMessage.value || validateTrainPresetCatalog(draft.value)) return
    emit('save', cloneTrainPresetCatalog(draft.value))
}
</script>

<template>
    <el-dialog :model-value="modelValue" :title="ui('维护列车参数', 'Maintain train parameters')"
        class="train-parameters-dialog" width="min(1080px, 96vw)" top="4vh" append-to-body destroy-on-close :close-on-click-modal="!saving" :close-on-press-escape="!saving" :show-close="!saving"
        @update:model-value="emit('update:modelValue', $event)">
        <div class="train-parameters-content" :inert="saving">
            <aside class="preset-sidebar">
                <div class="preset-toolbar">
                    <span>{{ ui('预设', 'Presets') }} {{ draft.presets.length }}/100</span>
                    <div><el-button size="small" :disabled="draft.presets.length >= 100 || !current" @click="addPreset">{{ ui('新增', 'Add') }}</el-button><el-button size="small" type="danger" plain :disabled="draft.presets.length <= 1" @click="removePreset">{{ ui('删除', 'Delete') }}</el-button></div>
                </div>
                <div class="preset-list" :aria-label="ui('列车参数预设', 'Train parameter presets')">
                    <button v-for="preset in draft.presets" :key="preset.id" type="button" class="preset-item"
                        :class="{ 'is-selected': preset.id === draft.selectedId }" :aria-pressed="preset.id === draft.selectedId"
                        @click="draft.selectedId = preset.id"><span>{{ presetName(preset) || ui('未命名', 'Untitled') }}</span><small v-if="presetIssue(preset)">{{ ui('待完善', 'Incomplete') }}</small></button>
                </div>
            </aside>
            <section v-if="current" class="preset-editor">
                <label class="parameter-field preset-name-field"><span>{{ ui('名称', 'Name') }}</span><el-input :model-value="presetName(current)" maxlength="80" :aria-label="ui('列车参数名称', 'Train parameter name')" @update:model-value="setName" /></label>
                <div class="basic-fields">
                    <label v-for="field in basicFields" :key="field.key" class="parameter-field"><span>{{ ui(field.zh, field.en) }}</span>
                        <el-input-number :model-value="inputValue(basicFieldValue(current.parameters, field.key))" :min="field.min" :max="field.max" :step="field.step" :precision="field.precision" :controls="false" :aria-label="ui(field.zh, field.en)"
                            @update:model-value="(value: number | undefined) => setNumber(field.key, value)" />
                        <small v-if="field.key === 'rotatingMassFactor'" class="parameter-hint">{{ ui('有效惯性质量 = 列车质量 × (1 + γ)', 'Effective inertial mass = train mass × (1 + γ)') }}</small>
                    </label>
                </div>
                <el-tabs v-model="activeTab" class="characteristics-tabs">
                    <el-tab-pane v-for="tab in curveTabs" :key="tab.key" :name="tab.key" :label="ui(tab.zh, tab.en)">
                        <div v-if="tab.key !== 'tractionCurve'" class="curve-generator">
                            <template v-if="tab.key === 'brakingCurve'">
                                <label class="generator-field"><span>{{ ui('恒定制动力 (kN)', 'Constant braking force (kN)') }}</span>
                                    <el-input-number :model-value="inputValue(current.parameters.maxBrakeForceKn)" :min="0.001" :max="10000000" :precision="3" :controls="false" @update:model-value="(value: number | undefined) => setNumber('maxBrakeForceKn', value)" />
                                </label>
                            </template>
                            <template v-else>
                                <span class="generator-formula">R = A + Bv + Cv² · kN · km/h</span>
                                <label v-for="row in resistanceRows" :key="row.field" class="generator-field"><span>{{ row.coefficient }}</span>
                                    <el-input-number :model-value="inputValue(current.parameters[row.field])" :min="0" :max="10000000" :step="row.step" :precision="row.precision" :controls="false" :aria-label="ui('阻力系数 ', 'Resistance coefficient ') + row.coefficient" @update:model-value="(value: number | undefined) => setNumber(row.field, value)" />
                                </label>
                            </template>
                            <el-button size="small" :disabled="!canGenerate" @click="generatePoints(tab.key)">{{ ui('生成并替换表格', 'Generate and replace table') }}</el-button>
                            <small>{{ ui('每 10 km/h 生成至最高速度；计算使用下方表格插值。', 'Generate every 10 km/h up to maximum speed; calculations interpolate the table below.') }}</small>
                        </div>
                        <div class="table-toolbar"><span>{{ ui('首速度为 0，速度严格递增。', 'Start at zero; speeds strictly increase.') }}</span><el-button size="small" :disabled="current.parameters[tab.key].length >= 128" @click="addPoint(tab.key)">{{ ui('新增节点', 'Add point') }}</el-button></div>
                        <el-table :data="current.parameters[tab.key]" border stripe :max-height="290" class="characteristic-table" :aria-label="ui(tab.zh + '表', tab.en + ' characteristics')">
                            <el-table-column type="index" :label="ui('行', 'Row')" width="54" align="center" />
                            <el-table-column :label="ui('速度 (km/h)', 'Speed (km/h)')" min-width="150"><template #default="{ row, $index }">
                                <el-input-number :model-value="inputValue(row.speedKmh)" :min="0" :max="10000" :step="10" :precision="3" :controls="false" :aria-label="ui(tab.zh + '第 ' + ($index + 1) + ' 行速度 (km/h)', tab.en + ' row ' + ($index + 1) + ' speed (km/h)')" @update:model-value="(value: number | undefined) => setPoint(tab.key, $index, 'speedKmh', value)" />
                            </template></el-table-column>
                            <el-table-column :label="ui(tab.force + ' (kN)', tab.en + ' force (kN)')" min-width="160"><template #default="{ row, $index }">
                                <el-input-number :model-value="inputValue(row.forceKn)" :min="0" :max="10000000" :step="10" :precision="3" :controls="false" :aria-label="ui(tab.zh + '第 ' + ($index + 1) + ' 行力 (kN)', tab.en + ' row ' + ($index + 1) + ' force (kN)')" @update:model-value="(value: number | undefined) => setPoint(tab.key, $index, 'forceKn', value)" />
                            </template></el-table-column>
                            <el-table-column :label="ui('操作', 'Action')" width="80" align="center"><template #default="{ $index }"><el-button type="danger" link :disabled="current.parameters[tab.key].length <= 1" :aria-label="ui('删除' + tab.zh + '第 ' + ($index + 1) + ' 行', 'Remove ' + tab.en + ' row ' + ($index + 1))" @click="removePoint(tab.key, $index)">{{ ui('删除', 'Delete') }}</el-button></template></el-table-column>
                        </el-table>
                    </el-tab-pane>
                </el-tabs>
            </section>
        </div>
        <el-alert v-if="validationMessage" :title="validationMessage" type="error" show-icon :closable="false" class="preset-validation" role="alert" />
        <template #footer><el-button :disabled="saving" @click="emit('update:modelValue', false)">{{ ui('取消', 'Cancel') }}</el-button><el-button type="primary" :disabled="!!validationMessage" :loading="saving" @click="save">{{ ui('保存', 'Save') }}</el-button></template>
    </el-dialog>
</template>

<style scoped>
:global(.train-parameters-dialog) { display: flex; flex-direction: column; max-height: 92dvh; }
:global(.train-parameters-dialog .el-dialog__body) { min-height: 0; overflow: auto; }
.train-parameters-content { display: grid; grid-template-columns: 225px minmax(0, 1fr); gap: 22px; max-height: calc(92dvh - 170px); overflow: auto; }
.preset-sidebar { min-width: 0; border-right: 1px solid var(--el-border-color-light); padding-right: 18px; }
.preset-toolbar { display: flex; flex-direction: column; gap: 10px; margin-bottom: 12px; }
.preset-toolbar > span { color: var(--el-text-color-secondary); font-size: 12px; }
.preset-list { display: flex; flex-direction: column; gap: 5px; max-height: 520px; overflow: auto; }
.preset-item { display: flex; flex-direction: column; align-items: flex-start; gap: 4px; padding: 10px 12px; width: 100%; text-align: left; font: inherit; font-size: 13px; color: var(--el-text-color-primary); border: 1px solid transparent; border-radius: 6px; background: transparent; cursor: pointer; overflow-wrap: anywhere; }
.preset-item:hover { background: var(--el-fill-color-light); }
.preset-item.is-selected { color: var(--el-color-primary); background: var(--el-color-primary-light-9); border-color: var(--el-color-primary-light-5); }
.preset-item:focus-visible { outline: 2px solid var(--el-color-primary); outline-offset: -2px; }
.preset-item small { color: var(--el-color-danger); font-size: 11px; }
.preset-editor { min-width: 0; }
.parameter-field { display: flex; flex-direction: column; gap: 7px; min-width: 0; color: var(--el-text-color-regular); font-size: 12px; }
.parameter-hint { color:var(--el-text-color-secondary); font-size:11px; line-height:1.5; }
.basic-fields { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 13px 16px; margin-top: 16px; }
.parameter-field :deep(.el-input-number), .characteristic-table :deep(.el-input-number) { width: 100%; }
.parameter-field :deep(.el-input__inner), .characteristic-table :deep(.el-input__inner) { text-align: left; }
.curve-generator { display: flex; flex-wrap: wrap; align-items: end; gap: 10px; margin-bottom: 14px; padding: 12px; background: var(--el-fill-color-light); border-radius: 6px; }
.generator-field { display: flex; flex-direction: column; gap: 4px; font-size: 12px; }
.generator-field :deep(.el-input-number) { width: 125px; }
.generator-formula, .curve-generator small { flex-basis: 100%; color: var(--el-text-color-secondary); font-size: 12px; }
.characteristics-tabs { margin-top: 14px; }
.table-toolbar { display: flex; justify-content: space-between; align-items: center; gap: 10px; flex-wrap: wrap; margin-bottom: 10px; }
.table-toolbar > span { font-size: 12px; color: var(--el-text-color-secondary); }
.characteristic-table { width: 100%; }
.resistance-formula { margin: 0 0 12px; color: var(--el-text-color-regular); font-size: 14px; }
.resistance-formula > span { margin-left: 12px; color: var(--el-text-color-secondary); font-size: 12px; }
.preset-validation { margin-top: 14px; }
@media (max-width: 700px) {
    .train-parameters-content { grid-template-columns: minmax(0, 1fr); gap: 14px; max-height: calc(92dvh - 200px); }
    .preset-sidebar { padding: 0 0 12px; border-right: 0; border-bottom: 1px solid var(--el-border-color-light); }
    .preset-toolbar { flex-direction: row; align-items: center; justify-content: space-between; }
    .preset-list { max-height: 130px; }
    .preset-item { padding: 7px 10px; }
    .basic-fields { gap: 12px; }
}
</style>
