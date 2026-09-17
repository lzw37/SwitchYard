<template>
    <div class="simulation-movement-table">
        <ElAutoResizer @resize="handleResize">
            <template #default="{ width, height }">
                <ElTableV2
                    v-if="width > 0 && height > 0"
                    ref="tableRef"
                    :columns="columns"
                    :data="rows"
                    :width="width"
                    :height="height"
                    :row-height="34"
                    :header-height="32"
                    :cache="3"
                    :v-scrollbar-size="scrollbarSize"
                    :h-scrollbar-size="scrollbarSize"
                    row-key="key"
                    fixed
                    :row-class="getRowClass"
                    :row-props="getRowProps"
                    @scroll="handleTableScroll"
                >
                    <template #empty>
                        <div class="simulation-movement-empty">{{ emptyText }}</div>
                    </template>
                </ElTableV2>
            </template>
        </ElAutoResizer>
    </div>
</template>

<script setup lang="ts">
import { computed, h, ref, watch } from 'vue'
import { ElAutoResizer, ElTableV2, ElTag, ElTooltip, type Column, type TableV2Instance } from 'element-plus'

interface MovementRow {
    index: number
    key: string
    trainName: string
    routeName: string
    timeText: string
}

type MovementPhase = 'waiting' | 'locking' | 'moving' | 'finished'
type MovementStatusType = 'success' | 'warning' | 'info' | 'primary'
type ScrollStrategy = 'auto' | 'smart' | 'center' | 'start' | 'end'

const props = defineProps<{
    rows: MovementRow[]
    getPhase: (index: number) => MovementPhase
    statusText: (phase: MovementPhase) => string
    statusType: (phase: MovementPhase) => MovementStatusType
    rowClassName: (context: { row: MovementRow }) => string
    emptyText: string
}>()

const scrollbarSize = 8
const tableRef = ref<TableV2Instance | null>(null)
const viewportWidth = ref(0)
let requestedRowIndex = -1
let requestedScrollStrategy: ScrollStrategy = 'smart'
let horizontalScrollOffset = 0

function textCell(text: string) {
    return h(ElTooltip, { content: text, placement: 'top', showAfter: 300 }, {
        default: () => h('span', { class: 'simulation-movement-cell-text' }, text),
    })
}

// Columns and row metadata remain stable throughout playback. Only the visible
// rows read live status, so a new train does not rebuild the entire sequence.
const columns = computed<Column[]>(() => [
    {
        key: 'index', title: '#', width: 46, align: 'center',
        cellRenderer: ({ rowData }: { rowData: MovementRow }) => h('span', rowData.index + 1),
    },
    {
        key: 'trainName', dataKey: 'trainName', title: '列车', width: 92,
        cellRenderer: ({ rowData }: { rowData: MovementRow }) => textCell(rowData.trainName),
    },
    {
        key: 'routeName', dataKey: 'routeName', title: '进路',
        width: Math.max(150, viewportWidth.value - scrollbarSize - 46 - 92 - 136 - 82),
        cellRenderer: ({ rowData }: { rowData: MovementRow }) => textCell(rowData.routeName),
    },
    {
        key: 'timeText', dataKey: 'timeText', title: '时间', width: 136,
        cellRenderer: ({ rowData }: { rowData: MovementRow }) => textCell(rowData.timeText),
    },
    {
        key: 'status', title: '状态', width: 82, align: 'center',
        cellRenderer: ({ rowData }: { rowData: MovementRow }) => {
            const phase = props.getPhase(rowData.index)
            return h(ElTag, { size: 'small', type: props.statusType(phase) }, {
                default: () => props.statusText(phase),
            })
        },
    },
])

function handleResize({ width }: { width: number; height: number }) {
    viewportWidth.value = width
}

function getRowClass({ rowData }: { rowData: MovementRow }) {
    return props.rowClassName({ row: rowData })
}

function getRowProps({ rowData }: { rowData: MovementRow }) {
    return { 'data-run-key': rowData.key, 'data-run-index': rowData.index }
}

function handleTableScroll({ scrollLeft }: { scrollLeft: number }) {
    horizontalScrollOffset = scrollLeft
}

function applyRequestedScroll() {
    if (requestedRowIndex < 0 || props.rows.length === 0) return
    const table = tableRef.value
    if (!table) return
    const previousScrollLeft = horizontalScrollOffset
    table.scrollToRow(Math.min(requestedRowIndex, props.rows.length - 1), requestedScrollStrategy)
    // ElTableV2's row navigation also aligns its single wide grid column, and
    // restores horizontal position only when it was nonzero. Preserve zero too.
    table.scrollToLeft(previousScrollLeft)
}

function scrollToRow(index: number, strategy: ScrollStrategy = 'smart') {
    requestedRowIndex = index
    requestedScrollStrategy = strategy
    applyRequestedScroll()
}

// A first scroll can arrive before AutoResizer mounts the table. Retain it and
// reapply it when the table becomes available or the operation plan is replaced.
watch([tableRef, () => props.rows], applyRequestedScroll, { flush: 'post' })

defineExpose({ scrollToRow })
</script>

<style scoped>
.simulation-movement-table {
    flex: 1 1 0;
    min-width: 0;
    min-height: 0;
    overflow: hidden;
    border-top: 1px solid #ebeef5;
}

.simulation-movement-table :deep(.el-table-v2__header-cell),
.simulation-movement-table :deep(.el-table-v2__row-cell) {
    box-sizing: border-box;
    padding: 0 8px;
    border-right: 1px solid #ebeef5;
    font-size: 12px;
}

.simulation-movement-table :deep(.el-table-v2__header-cell) {
    color: #606266;
    font-weight: 600;
}

.simulation-movement-table :deep(.simulation-movement-cell-text) {
    display: block;
    min-width: 0;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
}

.simulation-movement-table :deep(.simulation-current-row) {
    background-color: #f8fbff;
}

.simulation-movement-table :deep(.simulation-active-row) {
    background-color: #dbeafe;
    color: #1d4ed8;
    font-weight: 600;
}

.simulation-movement-table :deep(.simulation-active-row .el-table-v2__row-cell:first-child) {
    box-shadow: inset 3px 0 0 #2563eb;
}

.simulation-movement-empty {
    display: flex;
    height: 100%;
    align-items: center;
    justify-content: center;
    padding: 16px;
    color: #909399;
    font-size: 12px;
    text-align: center;
}
</style>
