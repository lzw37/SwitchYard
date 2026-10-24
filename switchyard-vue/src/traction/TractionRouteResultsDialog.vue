<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import type { TractionRouteCase } from './tractionWorkspace'
const props = defineProps<{ modelValue: boolean; rows: TractionRouteCase[]; selectedId: string; setName: string; routes: { id: string; description: string }[]; busy: boolean; error: string }>()
const emit = defineEmits<{ 'update:modelValue': [boolean]; open: [TractionRouteCase]; remove: [TractionRouteCase]; refresh: [] }>()
const { locale } = useI18n()
const ui = (zh: string, en: string) => locale.value.startsWith('zh') ? zh : en
const label = (id: string) => { const route = props.routes.find(r => r.id === id); return route?.description ? id + ' · ' + route.description : id || '—' }
const dateLabel = (date: string | null) => date ? new Date(date).toLocaleString(locale.value.startsWith('zh') ? 'zh-CN' : 'en-GB', { hour12: false }) : '—'
</script>
<template>
    <el-dialog :model-value="modelValue" :title="ui('方案进路结果', 'Route results') + ' · ' + setName" width="min(1100px, 95vw)" top="7vh" append-to-body class="traction-route-results-dialog" :close-on-click-modal="!busy" :show-close="!busy" :close-on-press-escape="!busy" @update:model-value="emit('update:modelValue', $event)">
        <div class="route-results-toolbar"><span>{{ ui('进路记录', 'Routes') }} {{ rows.length }} · {{ ui('已有结果', 'Results') }} {{ rows.filter(r => r.savedAtUtc).length }}</span><el-button :disabled="busy" @click="emit('refresh')">{{ ui('刷新列表', 'Refresh list') }}</el-button></div>
        <el-alert v-if="error" :title="error" type="error" :closable="false" show-icon />
        <el-table :data="rows" row-key="id" v-loading="busy" stripe max-height="58vh" :empty-text="ui('选择进路并点击计算，即可保存到当前方案', 'Select a route and click Calculate to save its result')">
            <el-table-column :label="ui('进路', 'Route')" min-width="190"><template #default="{ row }">{{ label(row.routeId) }} <el-tag v-if="row.id === selectedId" size="small">{{ ui('当前', 'Current') }}</el-tag></template></el-table-column>
            <el-table-column :label="ui('接续进路', 'Continuation')" min-width="150"><template #default="{ row }">{{ label(row.continuationId) }}</template></el-table-column>
            <el-table-column prop="trainName" :label="ui('列车', 'Train')" min-width="130" />
            <el-table-column :label="ui('状态', 'Status')" width="100"><template #default="{ row }">{{ !row.savedAtUtc ? ui('未计算', 'Not calculated') : row.isCurrent ? ui('已计算', 'Calculated') : ui('待计算', 'Pending') }}</template></el-table-column>
            <el-table-column :label="ui('运行时间 (s)', 'Duration (s)')" width="120"><template #default="{ row }">{{ row.durationS?.toFixed(1) ?? '—' }}</template></el-table-column>
            <el-table-column :label="ui('计算时间', 'Calculated at')" min-width="170"><template #default="{ row }">{{ dateLabel(row.savedAtUtc) }}</template></el-table-column>
            <el-table-column :label="ui('操作', 'Actions')" width="130" fixed="right"><template #default="{ row }">
                <el-button link type="primary" :disabled="busy || !row.routeId" @click="row.id === selectedId ? emit('update:modelValue', false) : emit('open', row)">{{ ui('查看', 'View') }}</el-button>
                <el-popconfirm :title="ui('删除此进路记录的参数与结果？', 'Delete parameters and results for this route?')" :confirm-button-text="ui('删除', 'Delete')" :cancel-button-text="ui('取消', 'Cancel')" width="260" @confirm="emit('remove', row)"><template #reference><el-button link type="danger" :disabled="busy">{{ ui('删除', 'Delete') }}</el-button></template></el-popconfirm>
            </template></el-table-column>
        </el-table>
        <template #footer><el-button :disabled="busy" @click="emit('update:modelValue', false)">{{ ui('关闭', 'Close') }}</el-button></template>
    </el-dialog>
</template>
<style scoped>
.route-results-toolbar { display:flex; align-items:center; justify-content:space-between; gap:12px; margin-bottom:14px; color:var(--el-text-color-secondary); font-size:13px; }.el-alert { margin-bottom:12px; }
</style>
