<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { CalculationParameterSet } from '../calculationParameterSets'
const props = defineProps<{ modelValue?: string; sets: CalculationParameterSet[]; disabled?: boolean }>()
const emit = defineEmits<{ 'update:modelValue': [value: string] }>()
const { t } = useI18n()
const defaultName = computed(() => props.sets.find(set => set.isDefault)?.name || t('calculationParameterSets.default'))
</script>

<template>
    <el-select :model-value="modelValue || ''" :disabled="disabled" filterable :aria-label="t('calculationParameterSets.title')"
        style="min-width: 180px; width: 100%" @update:model-value="emit('update:modelValue', $event || '')">
        <el-option value="" :label="t('calculationParameterSets.useDefault', { name: defaultName })" />
        <el-option v-if="modelValue && !sets.some(set => set.id === modelValue)" :value="modelValue" :label="t('calculationParameterSets.missing')" />
        <el-option v-for="set in sets" :key="set.id" :value="set.id" :label="set.name + (set.isDefault ? ` (${t('calculationParameterSets.default')})` : '')" />
    </el-select>
</template>
