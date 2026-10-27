<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'

const props = withDefaults(defineProps<{
    modelValue: number
    direction?: 'horizontal' | 'vertical'
    min?: number
    max?: number
    reverse?: boolean
    label?: string
    resetValue?: number
}>(), { direction: 'horizontal', min: 160, max: 1200, reverse: false })
const emit = defineEmits<{ 'update:modelValue': [value: number]; reset: [] }>()
const { t } = useI18n()
const dragging = ref(false)
const isWidth = computed(() => props.direction === 'horizontal')
const upper = computed(() => Math.max(0, props.max))
const lower = computed(() => Math.min(Math.max(0, props.min), upper.value))
const initialSize = props.modelValue
let target: HTMLElement | null = null
let pointerId: number | null = null
let startPosition = 0
let startSize = 0
let previousCursor = ''
let previousSelection = ''

function update(value: number) {
    const next = Math.round(Math.min(upper.value, Math.max(lower.value, value)))
    if (next !== props.modelValue) emit('update:modelValue', next)
}
function reset() {
    update(props.resetValue ?? initialSize)
    emit('reset')
}
function start(event: PointerEvent) {
    if (event.button !== 0 || dragging.value) return
    event.preventDefault()
    target = event.currentTarget as HTMLElement
    pointerId = event.pointerId
    startPosition = isWidth.value ? event.clientX : event.clientY
    startSize = props.modelValue
    previousCursor = document.body.style.cursor
    previousSelection = document.body.style.userSelect
    document.body.style.cursor = isWidth.value ? 'col-resize' : 'row-resize'
    document.body.style.userSelect = 'none'
    dragging.value = true
    target.setPointerCapture(event.pointerId)
    target.focus({ preventScroll: true })
}
function move(event: PointerEvent) {
    if (!dragging.value || event.pointerId !== pointerId) return
    const delta = (isWidth.value ? event.clientX : event.clientY) - startPosition
    update(startSize + delta * (props.reverse ? -1 : 1))
}
function stop() {
    if (!dragging.value) return
    dragging.value = false
    document.body.style.cursor = previousCursor
    document.body.style.userSelect = previousSelection
    if (target && pointerId !== null && target.hasPointerCapture(pointerId)) target.releasePointerCapture(pointerId)
    target = null
    pointerId = null
}
function keydown(event: KeyboardEvent) {
    const decrease = isWidth.value ? 'ArrowLeft' : 'ArrowUp'
    const increase = isWidth.value ? 'ArrowRight' : 'ArrowDown'
    if (![decrease, increase, 'Home', 'End', 'Enter'].includes(event.key)) return
    event.preventDefault()
    if (event.key === 'Home') update(lower.value)
    else if (event.key === 'End') update(upper.value)
    else if (event.key === 'Enter') reset()
    else update(props.modelValue + (event.key === increase ? 1 : -1) * (props.reverse ? -1 : 1) * (event.shiftKey ? 40 : 10))
}
watch([lower, upper], () => {
    if (props.modelValue < lower.value || props.modelValue > upper.value) update(props.modelValue)
})
onBeforeUnmount(stop)
</script>

<template>
    <div class="sy-pane-divider" :class="{ 'is-horizontal': isWidth, 'is-vertical': !isWidth, 'is-dragging': dragging }"
        role="separator" tabindex="0" :aria-orientation="isWidth ? 'vertical' : 'horizontal'"
        :aria-label="label || t(isWidth ? 'common.resize.horizontal' : 'common.resize.vertical')"
        :aria-valuenow="Math.round(modelValue)" :aria-valuemin="lower" :aria-valuemax="upper"
        :title="label || t(isWidth ? 'common.resize.horizontal' : 'common.resize.vertical')"
        @pointerdown="start" @pointermove="move" @pointerup="stop" @pointercancel="stop"
        @lostpointercapture="stop" @keydown="keydown" @dblclick="reset" />
</template>

<style scoped>
.sy-pane-divider { position: relative; flex: 0 0 8px; align-self: stretch; touch-action: none; user-select: none; outline: none; z-index: 5; }
.is-horizontal { width: 8px; cursor: col-resize; }
.is-vertical { height: 8px; width: 100%; cursor: row-resize; }
.sy-pane-divider::after { content: ''; position: absolute; border-radius: 2px; background: var(--sy-border, #dfe4ea); transition: background .15s; }
.is-horizontal::after { top: 0; bottom: 0; left: 3px; width: 2px; }
.is-vertical::after { left: 0; right: 0; top: 3px; height: 2px; }
.sy-pane-divider:hover::after, .sy-pane-divider:focus-visible::after, .is-dragging::after { background: var(--el-color-primary, #4776a8); }
</style>
