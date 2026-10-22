<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'

interface LineParameterRow {
    id: string
    index: number
    linkId: string
    startM: number
    endM: number
    speedLimitKmh: number
    gradientPermille: number
    additionalResistanceKn: number
}
const props = defineProps<{
    modelValue: boolean
    segments: LineParameterRow[]
    defaultLimit: number
    routeLabel: string
}>()
const emit = defineEmits<{
    'update:modelValue': [value: boolean]
    'update:defaultLimit': [value: number]
    change: [id: string, index: number, field: 'speedLimitKmh' | 'gradientPermille' | 'additionalResistanceKn', value: number | undefined]
}>()
const { locale } = useI18n()
const ui = (zh: string, en: string) => locale.value.startsWith('zh') ? zh : en
const defaults = computed(() => [{ name: ui('未设置线路限速时使用', 'Used when a link has no speed limit'), speedLimitKmh: props.defaultLimit }])
const fieldLabel = (row: LineParameterRow, field: 'limit' | 'gradient' | 'resistance') =>
    `${row.linkId} · ${ui('第', 'Row ')}${row.index + 1}${ui('段', '')} · ${field === 'limit' ? ui('限速 (km/h)', 'Speed limit (km/h)') : field === 'gradient' ? ui('坡度 (‰)', 'Gradient (‰)') : ui('附加阻力 (kN)', 'Extra resistance (kN)')}`
function updateDefault(value: number | undefined) {
    if (value !== undefined && Number.isFinite(value)) emit('update:defaultLimit', value)
}
</script>

<template>
    <el-dialog :model-value="modelValue" :title="ui('坡度、限速与附加阻力', 'Gradients, speed limits and extra resistance')" width="min(1080px, 94vw)" top="6vh" append-to-body class="line-parameters-dialog" @update:model-value="emit('update:modelValue', $event)">
        <div class="line-dialog-content">
            <p class="dialog-context">{{ routeLabel || ui('尚未选择进路', 'No route selected') }}</p>
            <p class="dialog-help">{{ ui('直接在表格中输入数值，修改后由后端自动重算。位置按当前进路展开，正坡度为上坡；坡度和附加阻力按车头所在分段计算。', 'Edit values directly in the tables to recalculate on the backend. Positions follow the selected route; positive gradients are uphill. Gradient and extra resistance use the section at the train head.') }}</p>
            <h3>{{ ui('缺省限速', 'Default speed limit') }}</h3>
            <el-table :data="defaults" border class="default-limit-table">
                <el-table-column prop="name" :label="ui('适用范围', 'Applies to')" min-width="220" />
                <el-table-column :label="ui('限速 (km/h)', 'Speed limit (km/h)')" width="180">
                    <template #default="{ row }"><el-input-number :model-value="row.speedLimitKmh" :min="1" :max="500" :controls="false" :aria-label="ui('缺省限速 (km/h)', 'Default speed limit (km/h)')" @update:model-value="updateDefault" /></template>
                </el-table-column>
            </el-table>
            <h3>{{ ui('进路分段参数', 'Route section parameters') }} <span>{{ segments.length }} {{ ui('段', 'sections') }}</span></h3>
            <el-table :data="segments" row-key="id" border stripe max-height="min(48vh, 480px)" class="line-section-table" :empty-text="ui('选择进路并确认距离标尺后，可维护各段坡度与限速。', 'Select a route and confirm the distance scale to edit section parameters.')">
                <el-table-column type="index" label="#" width="48" />
                <el-table-column prop="linkId" :label="ui('线路', 'Link')" min-width="130" show-overflow-tooltip />
                <el-table-column :label="ui('起点位置 (m)', 'Start (m)')" min-width="120"><template #default="{ row }">{{ row.startM.toFixed(1) }}</template></el-table-column>
                <el-table-column :label="ui('终点位置 (m)', 'End (m)')" min-width="120"><template #default="{ row }">{{ row.endM.toFixed(1) }}</template></el-table-column>
                <el-table-column :label="ui('限速 (km/h)', 'Speed limit (km/h)')" min-width="155">
                    <template #default="{ row }"><el-input-number :model-value="row.speedLimitKmh" :min="1" :max="500" :controls="false" :aria-label="fieldLabel(row, 'limit')" @update:model-value="(value: number | undefined) => emit('change', row.id, row.index, 'speedLimitKmh', value)" /></template>
                </el-table-column>
                <el-table-column :label="ui('坡度 (‰)', 'Gradient (‰)')" min-width="155">
                    <template #default="{ row }"><el-input-number :model-value="row.gradientPermille" :min="-100" :max="100" :step="0.1" :controls="false" :aria-label="fieldLabel(row, 'gradient')" @update:model-value="(value: number | undefined) => emit('change', row.id, row.index, 'gradientPermille', value)" /></template>
                </el-table-column>
                <el-table-column :label="ui('附加阻力 (kN)', 'Extra resistance (kN)')" min-width="170">
                    <template #default="{ row }"><el-input-number :model-value="row.additionalResistanceKn" :min="0" :max="10000000" :step="0.1" :precision="3" :controls="false" :aria-label="fieldLabel(row, 'resistance')" @update:model-value="(value: number | undefined) => emit('change', row.id, row.index, 'additionalResistanceKn', value)" /></template>
                </el-table-column>
            </el-table>
            <p class="dialog-help">{{ ui('曲线与道岔限速请按实际资料填写。附加阻力为整列车的额外阻力，默认 0 kN；不要重复填写 Davis 阻力。需要跨次使用时，在主页面点击“保存本机参数”。', 'Use surveyed curve and turnout limits. Extra resistance is additional total train resistance (default 0 kN); exclude Davis resistance already entered. Select Save parameters locally on the main page to retain your edits for later visits.') }}</p>
        </div>
        <template #footer><el-button type="primary" @click="emit('update:modelValue', false)">{{ ui('完成', 'Done') }}</el-button></template>
    </el-dialog>
</template>

<style scoped>
.line-dialog-content { max-height:70vh; overflow:auto; }
.dialog-context { margin:0 0 8px; color:var(--sy-text,#243248); font-weight:600; overflow-wrap:anywhere; }
.dialog-help { margin:8px 0 16px; color:var(--sy-text-muted,#758298); font-size:12px; line-height:1.7; }
h3 { display:flex; justify-content:space-between; align-items:center; margin:20px 0 10px; font-size:13px; color:var(--sy-text,#243248); }
h3 span { font-size:11px; color:var(--sy-text-muted,#758298); font-weight:400; }
:deep(.el-input-number) { width:100%; }
:deep(.el-table th.el-table__cell) { background:var(--sy-surface-muted,#f4f7fb); }
:deep(.el-table .cell) { font-variant-numeric:tabular-nums; }
@media(max-width:600px) { .line-dialog-content { max-height:68vh; }.dialog-help { font-size:11px; } }
</style>
