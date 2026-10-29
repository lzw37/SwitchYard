<template>
    <section class="solver-parameters" data-testid="solver-parameters">
        <div class="parameters-heading"><h4>{{ t('modelSolving.parameters.title') }}</h4><span>{{ solverName }}</span></div>
        <el-form label-position="top" :disabled="disabled" class="parameters-grid">
            <el-form-item :label="t('modelSolving.presets.fields.timeLimit')" for="solve-time-limit">
                <el-input-number id="solve-time-limit" :model-value="modelValue.timeLimitSeconds" :min="1" :max="86400" :precision="0" controls-position="right" @update:model-value="update('timeLimitSeconds', $event)" />
            </el-form-item>
            <el-form-item :label="t('modelSolving.presets.fields.threadCount')" for="solve-thread-count">
                <el-input-number id="solve-thread-count" :model-value="modelValue.threadCount" :min="1" :max="128" :precision="0" controls-position="right" @update:model-value="update('threadCount', $event)" />
            </el-form-item>
            <el-form-item :label="t('modelSolving.presets.fields.objective')" for="solve-objective">
                <el-select id="solve-objective" :model-value="modelValue.objective" @update:model-value="update('objective', $event)">
                    <el-option value="min-end-time" :label="t('modelSolving.presets.objectives.minEndTime')" />
                    <el-option value="min-duration" :label="t('modelSolving.presets.objectives.minDuration')" />
                    <el-option value="min-start-and-end" :label="t('modelSolving.presets.objectives.minStartAndEnd')" />
                </el-select>
            </el-form-item>
            <el-form-item :label="t('modelSolving.presets.fields.leftTolerance')" for="solve-left-tolerance">
                <el-input-number id="solve-left-tolerance" :model-value="modelValue.leftShiftToleranceSeconds" :min="0" :max="172800" :precision="0" controls-position="right" @update:model-value="update('leftShiftToleranceSeconds', $event)" />
            </el-form-item>
            <el-form-item :label="t('modelSolving.presets.fields.rightTolerance')" for="solve-right-tolerance">
                <el-input-number id="solve-right-tolerance" :model-value="modelValue.rightShiftToleranceSeconds" :min="0" :max="172800" :precision="0" controls-position="right" @update:model-value="update('rightShiftToleranceSeconds', $event)" />
            </el-form-item>
        </el-form>
        <p class="parameters-hint">{{ t(disabled ? 'modelSolving.parameters.readOnly' : 'modelSolving.parameters.hint') }}</p>
    </section>
</template>

<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { normalizeSolveSettings, type SolveSettings } from '../solveSettings'
const props = defineProps<{ modelValue: SolveSettings; solverName: string; disabled: boolean }>()
const emit = defineEmits<{ 'update:modelValue': [value: SolveSettings] }>()
const { t } = useI18n()
function update(key: keyof SolveSettings, value: number | string | undefined) {
    if (props.disabled || value === undefined) return
    emit('update:modelValue', normalizeSolveSettings({ ...props.modelValue, [key]: value }))
}
</script>

<style scoped>
.solver-parameters { margin-top: 20px; padding-top: 16px; border-top: 1px solid var(--el-border-color-lighter); }
.parameters-heading { display: flex; align-items: baseline; gap: 12px; margin-bottom: 12px; }
.parameters-heading h4 { margin: 0; font-size: 14px; }
.parameters-heading span, .parameters-hint { color: var(--el-text-color-secondary); font-size: 12px; }
.parameters-grid { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 0 16px; }
.parameters-grid :deep(.el-form-item) { margin-bottom: 12px; min-width: 0; }
.parameters-grid :deep(.el-input-number), .parameters-grid :deep(.el-select) { width: 100%; }
.parameters-grid :deep(.el-input__inner) { text-align: left; }
.parameters-hint { margin: 0; line-height: 1.6; }
@media (max-width: 1100px) { .parameters-grid { grid-template-columns: repeat(2, minmax(0, 1fr)); } }
@media (max-width: 760px) { .parameters-grid { grid-template-columns: 1fr; } }
</style>
