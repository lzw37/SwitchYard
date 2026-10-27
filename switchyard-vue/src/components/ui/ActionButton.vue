<script setup lang="ts">
import type { Component } from 'vue'

defineOptions({ inheritAttrs: false })
withDefaults(defineProps<{
    label: string
    icon?: Component
    // Existing controls stay compact; form and dialog actions opt into visible labels.
    variant?: 'icon' | 'text' | 'icon-text'
    type?: '' | 'default' | 'primary' | 'success' | 'warning' | 'danger' | 'info'
    loading?: boolean
    disabled?: boolean
    active?: boolean
}>(), { variant: 'icon', type: 'default', loading: false, disabled: false, active: undefined })
defineEmits<{ click: [event: MouseEvent] }>()
</script>

<template>
    <span class="action-button-anchor">
    <el-tooltip :content="label" placement="top" :show-after="350" :disabled="variant !== 'icon'">
        <span class="action-button-trigger">
            <el-button v-bind="$attrs" class="sy-action-button" :class="[`sy-action-button--${variant}`, { 'is-active': active }]"
                :type="type === 'default' ? '' : type" :icon="variant === 'text' ? undefined : icon" :loading="loading" :disabled="disabled"
                :aria-label="label" :aria-pressed="active" :circle="variant === 'icon'" size="small" native-type="button"
                @click="$emit('click', $event)">
                <slot v-if="variant !== 'text'" />
                <span v-if="variant !== 'icon'">{{ label }}</span>
            </el-button>
        </span>
    </el-tooltip>
    </span>
</template>

<style scoped>
.action-button-anchor, .action-button-trigger { display: inline-flex; flex: 0 0 auto; vertical-align: middle; }
.sy-action-button.el-button {
    height: 30px;
    margin: 0;
    box-shadow: none;
}
.sy-action-button--icon.el-button {
    width: 30px;
    min-width: 30px;
    padding: 0;
    border-radius: 50%;
    font-size: 14px;
}
.sy-action-button.el-button:not(.sy-action-button--icon) {
    width: auto;
    min-width: 64px;
    padding: 0 12px;
    border-radius: var(--sy-radius, 6px);
    font-size: 13px;
}
.sy-action-button--icon.el-button:not(.is-disabled) {
    color: var(--el-color-primary);
    background: var(--sy-surface, #fff);
    border-color: var(--sy-border, #dfe4ea);
}
.sy-action-button--icon.el-button--danger:not(.is-disabled) { color: var(--el-color-danger); }
.sy-action-button--icon.el-button:not(.is-disabled):hover,
.sy-action-button--icon.el-button.is-active:not(.is-disabled) {
    background: var(--el-color-primary-light-9);
    border-color: var(--el-color-primary-light-5);
}
.sy-action-button--icon.el-button--danger:not(.is-disabled):hover {
    background: var(--el-color-danger-light-9);
    border-color: var(--el-color-danger-light-5);
}
.sy-action-button--icon.el-button.is-disabled {
    color: var(--sy-text-muted, #758195);
    background: var(--sy-surface-muted, #f6f8fa);
    border-color: var(--sy-border, #dfe4ea);
    opacity: .55;
}
.sy-action-button:focus-visible { outline: 2px solid var(--el-color-primary); outline-offset: 2px; }
</style>
