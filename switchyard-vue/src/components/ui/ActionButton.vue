<script setup lang="ts">
import type { Component } from 'vue'

defineOptions({ inheritAttrs: false })
withDefaults(defineProps<{
    label: string
    icon?: Component
    type?: '' | 'default' | 'primary' | 'success' | 'warning' | 'danger' | 'info'
    loading?: boolean
    disabled?: boolean
    active?: boolean
}>(), { type: 'default', loading: false, disabled: false, active: undefined })
defineEmits<{ click: [event: MouseEvent] }>()
</script>

<template>
    <span class="action-button-anchor">
    <el-tooltip :content="label" placement="top" :show-after="350">
        <span class="action-button-trigger">
            <el-button v-bind="$attrs" class="sy-action-button" :class="{ 'is-active': active }"
                :type="type === 'default' ? '' : type" :icon="icon" :loading="loading" :disabled="disabled"
                :aria-label="label" :aria-pressed="active" circle size="small" native-type="button"
                @click="$emit('click', $event)">
                <slot />
            </el-button>
        </span>
    </el-tooltip>
    </span>
</template>

<style scoped>
.action-button-anchor, .action-button-trigger { display: inline-flex; flex: 0 0 auto; vertical-align: middle; }
.sy-action-button.el-button {
    width: 30px;
    height: 30px;
    min-width: 30px;
    padding: 0;
    margin: 0;
    border-radius: 50%;
    font-size: 14px;
    box-shadow: none;
}
.sy-action-button.el-button:not(.is-disabled) {
    color: var(--el-color-primary);
    background: var(--sy-surface, #fff);
    border-color: var(--sy-border, #dfe4ea);
}
.sy-action-button.el-button--danger:not(.is-disabled) { color: var(--el-color-danger); }
.sy-action-button.el-button:not(.is-disabled):hover,
.sy-action-button.el-button.is-active:not(.is-disabled) {
    background: var(--el-color-primary-light-9);
    border-color: var(--el-color-primary-light-5);
}
.sy-action-button.el-button--danger:not(.is-disabled):hover {
    background: var(--el-color-danger-light-9);
    border-color: var(--el-color-danger-light-5);
}
.sy-action-button.el-button.is-disabled {
    color: var(--sy-text-muted, #758195);
    background: var(--sy-surface-muted, #f6f8fa);
    border-color: var(--sy-border, #dfe4ea);
    opacity: .55;
}
.sy-action-button:focus-visible { outline: 2px solid var(--el-color-primary); outline-offset: 2px; }
</style>
