<script setup lang="ts">
import { Refresh, Setting } from '@element-plus/icons-vue'
import { useI18n } from 'vue-i18n'
import ActionButton from '@/components/ui/ActionButton.vue'

defineProps<{
    modelValue: string
    options: Array<{ value: string; label: string }>
    loading: boolean
    disabled: boolean
    manageDisabled: boolean
    refreshDisabled: boolean
}>()

const emit = defineEmits<{
    'update:modelValue': [value: string]
    change: []
    manage: []
    refresh: []
}>()

const { t } = useI18n()
</script>

<template>
    <div class="operation-plan-selector" role="group" :aria-label="t('operationPlan.planObject.label')">
        <span class="operation-plan-selector__label">{{ t('operationPlan.planObject.label') }}</span>
        <el-select
            :model-value="modelValue"
            size="small"
            filterable
            class="operation-plan-selector__select"
            :loading="loading"
            :disabled="disabled"
            :aria-label="t('operationPlan.planObject.label')"
            :placeholder="t('operationPlan.planObject.placeholders.select')"
            @update:model-value="emit('update:modelValue', $event)"
            @change="emit('change')"
        >
            <el-option
                v-for="option in options"
                :key="option.value"
                :label="option.label"
                :value="option.value"
            />
        </el-select>
        <ActionButton
            :icon="Setting"
            :label="t('operationPlan.planObject.actions.manage')"
            :disabled="manageDisabled"
            @click="emit('manage')"
        />
        <ActionButton
            :icon="Refresh"
            :label="t('operationPlan.actions.refresh')"
            :disabled="refreshDisabled"
            @click="emit('refresh')"
        />
    </div>
</template>

<style scoped>
.operation-plan-selector {
    display: flex;
    align-items: center;
    gap: 8px;
    flex: 0 0 auto;
    min-width: 0;
    max-width: 100%;
}

.operation-plan-selector__label {
    flex: 0 0 auto;
    color: var(--el-text-color-regular);
    font-size: 12px;
    white-space: nowrap;
}

.operation-plan-selector__select {
    flex: 0 1 300px;
    width: min(300px, 100%);
    min-width: 0;
}
</style>
