<script setup lang="ts">
import { ElSwitch } from 'element-plus'
import { useI18n } from 'vue-i18n'
const visible = defineModel<boolean>({ required: true })
const { t } = useI18n()
</script>

<template>
    <ElSwitch v-model="visible" class="resource-conflict-toggle" :active-text="t('resourceConflicts.show')"
        :aria-label="t('resourceConflicts.show')" :title="t('resourceConflicts.hint')" />
</template>

<style scoped>
.resource-conflict-toggle { flex-shrink: 0; }
</style>
