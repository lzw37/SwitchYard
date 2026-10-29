<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'

const props = defineProps<{ value: unknown; label?: string; index?: boolean; depth: number; expandDepth: number }>()
const { t } = useI18n()
const expanded = ref(props.depth < props.expandDepth)
const isArray = computed(() => Array.isArray(props.value))
const isContainer = computed(() => props.value !== null && typeof props.value === 'object')
const entries = computed(() => Object.entries(props.value as Record<string, unknown>))
const count = computed(() => isArray.value ? (props.value as unknown[]).length : Object.keys(props.value as object).length)
const opening = computed(() => isArray.value ? '[' : '{')
const closing = computed(() => isArray.value ? ']' : '}')
const scalar = computed(() => JSON.stringify(props.value))
const kind = computed(() => props.value === null ? 'null' : typeof props.value)
watch(() => props.expandDepth, depth => { expanded.value = props.depth < depth })
function toggle(event: Event) { expanded.value = (event.target as HTMLDetailsElement).open }
</script>

<template>
    <details v-if="isContainer && count" :open="expanded" class="json-branch" @toggle="toggle">
        <summary class="json-line">
            <span v-if="label !== undefined" class="json-key" :class="{ 'json-index': index }">{{ index ? label : JSON.stringify(label) }}<span class="json-punctuation">: </span></span>
            <span class="json-punctuation">{{ opening }}</span>
            <span class="json-count">{{ count }} {{ t(isArray ? 'modelSolving.viewer.items' : 'modelSolving.viewer.keys') }}</span>
            <span v-if="!expanded" class="json-punctuation">… {{ closing }}</span>
        </summary>
        <div v-if="expanded" class="json-children">
            <JsonTreeNode v-for="[key, child] in entries" :key="key" :label="key" :index="isArray" :value="child" :depth="depth + 1" :expand-depth="expandDepth" />
        </div>
        <div v-if="expanded" class="json-line json-closing">{{ closing }}</div>
    </details>
    <div v-else class="json-line json-leaf">
        <span v-if="label !== undefined" class="json-key" :class="{ 'json-index': index }">{{ index ? label : JSON.stringify(label) }}<span class="json-punctuation">: </span></span>
        <span v-if="isContainer" class="json-punctuation">{{ opening }}{{ closing }}</span>
        <span v-else :class="['json-value', 'json-' + kind]">{{ scalar }}</span>
    </div>
</template>

<style scoped>
.json-line { min-height: 25px; line-height: 25px; padding-right: 8px; white-space: pre-wrap; overflow-wrap: anywhere; }
summary { cursor: pointer; border-radius: 3px; }
summary:hover { background: #eaf1fa; }
summary::marker { color: #7d8b9e; font-size: 10px; }
.json-children { margin-left: 5px; padding-left: 19px; border-left: 1px solid #dce5f0; }
.json-leaf { padding-left: 14px; }
.json-closing { padding-left: 14px; color: #64748b; }
.json-key { color: #1d4c8a; }
.json-index, .json-punctuation { color: #718096; }
.json-string { color: #087f5b; }
.json-number { color: #7c3aed; }
.json-boolean { color: #c05621; }
.json-null { color: #8a94a6; font-style: italic; }
.json-count { margin: 0 10px; padding: 1px 6px; border-radius: 4px; background: #eaf0f7; color: #758299; font-size: 11px; }
</style>
