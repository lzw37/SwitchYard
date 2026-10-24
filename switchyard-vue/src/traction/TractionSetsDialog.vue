<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import type { TractionCalculationSet, TractionSetAction } from './tractionWorkspace'
const props = defineProps<{ modelValue: boolean; sets: TractionCalculationSet[]; selectedId: string; busy: boolean; error: string }>()
const emit = defineEmits<{ 'update:modelValue': [boolean]; action: [TractionSetAction]; refresh: [] }>()
const { locale } = useI18n()
const ui = (zh: string, en: string) => locale.value.startsWith('zh') ? zh : en
const editing = ref<'create' | 'copy' | 'rename' | null>(null)
const source = ref<TractionCalculationSet | null>(null)
const name = ref('')
const nameError = computed(() => !name.value.trim() ? ui('请输入方案名称', 'Enter a set name')
    : props.sets.some(item => item.id !== (editing.value === 'rename' ? source.value?.id : '') && item.name.trim().toLocaleLowerCase() === name.value.trim().toLocaleLowerCase())
        ? ui('方案名称已存在', 'This name already exists') : '')
watch(() => props.modelValue, () => { editing.value = null })
watch(() => props.sets, () => { if (!props.error) editing.value = null })
function begin(mode: 'create' | 'copy' | 'rename', row: TractionCalculationSet | null = null) {
    editing.value = mode; source.value = row
    name.value = mode === 'rename' ? row!.name : mode === 'copy' ? `${row!.name.slice(0, 65)} ${ui('副本', 'copy')}` : ''
    if (mode === 'copy') {
        const base = name.value; let n = 2
        while (props.sets.some(item => item.name === name.value)) name.value = `${base} ${n++}`
    }
}
function submit() {
    if (props.busy || nameError.value || !editing.value) return
    emit('action', editing.value === 'rename'
        ? { kind: 'rename', id: source.value!.id, revision: source.value!.revision, name: name.value.trim() }
        : { kind: 'create', name: name.value.trim(), ...(editing.value === 'copy' ? { sourceId: source.value!.id, revision: source.value!.revision } : {}) })
}
const dateLabel = (value: string | null) => value ? new Date(value).toLocaleString(locale.value.startsWith('zh') ? 'zh-CN' : 'en-GB', { hour12: false }) : '—'
</script>

<template>
    <el-dialog :model-value="modelValue" :title="ui('管理牵引计算方案', 'Manage traction calculation sets')" width="min(1100px, 95vw)" top="7vh" append-to-body class="traction-sets-dialog"
        :close-on-click-modal="!busy" :close-on-press-escape="!busy" :show-close="!busy" @update:model-value="emit('update:modelValue', $event)">
        <div class="sets-toolbar"><el-button type="primary" class="new-calculation-set" :disabled="busy || sets.length >= 100" @click="begin('create')">{{ ui('新建方案', 'New set') }}</el-button><el-button :disabled="busy" @click="emit('refresh')">{{ ui('刷新列表', 'Refresh list') }}</el-button><span>{{ sets.length }} / 100</span></div>
        <el-alert v-if="error" :title="error" type="error" :closable="false" show-icon />
        <form v-if="editing" class="set-name-form" @submit.prevent="submit">
            <label>{{ editing === 'rename' ? ui('重命名', 'Rename') : editing === 'copy' ? ui('复制为', 'Copy as') : ui('方案名称', 'Set name') }}</label>
            <el-input v-model="name" :maxlength="80" :disabled="busy" :aria-label="ui('计算方案名称', 'Calculation set name')" autofocus />
            <el-button native-type="submit" type="primary" :loading="busy" :disabled="!!nameError">{{ ui('保存', 'Save') }}</el-button>
            <el-button :disabled="busy" @click="editing = null">{{ ui('取消', 'Cancel') }}</el-button>
            <small v-if="name && nameError" role="alert">{{ nameError }}</small>
        </form>
        <el-table :data="sets" row-key="id" v-loading="busy" stripe max-height="58vh">
            <el-table-column :label="ui('名称', 'Name')" min-width="190"><template #default="{ row }"><strong>{{ row.name }}</strong><el-tag v-if="row.id === selectedId" size="small" class="selected-set-tag">{{ ui('当前', 'Current') }}</el-tag></template></el-table-column>
            <el-table-column prop="routeCount" :label="ui('进路记录', 'Routes')" width="100" />
            <el-table-column prop="calculatedCount" :label="ui('已有结果', 'Results')" width="100" />
            <el-table-column :label="ui('待计算', 'Pending')" width="100"><template #default="{ row }">{{ row.routeCount - row.currentCount }}</template></el-table-column>
            <el-table-column :label="ui('更新时间', 'Updated at')" min-width="170"><template #default="{ row }">{{ dateLabel(row.updatedAtUtc) }}</template></el-table-column>
            <el-table-column :label="ui('操作', 'Actions')" width="205" fixed="right"><template #default="{ row }">
                <el-button link type="primary" :disabled="busy || sets.length >= 100" @click="begin('copy', row)">{{ ui('复制', 'Copy') }}</el-button>
                <el-button link type="primary" :disabled="busy" @click="begin('rename', row)">{{ ui('重命名', 'Rename') }}</el-button>
                <el-popconfirm :title="ui(`删除“${row.name}”及其全部进路参数和结果？`, `Delete “${row.name}”, including all its route parameters and results?`)" :confirm-button-text="ui('删除', 'Delete')" :cancel-button-text="ui('取消', 'Cancel')" width="260" @confirm="emit('action', { kind: 'delete', id: row.id, revision: row.revision })">
                    <template #reference><el-button link type="danger" :disabled="busy || sets.length <= 1">{{ ui('删除', 'Delete') }}</el-button></template>
                </el-popconfirm>
            </template></el-table-column>
        </el-table>
        <template #footer><el-button :disabled="busy" @click="emit('update:modelValue', false)">{{ ui('关闭', 'Close') }}</el-button></template>
    </el-dialog>
</template>

<style scoped>
.sets-toolbar { display:flex; align-items:center; gap:10px; margin-bottom:16px; }.sets-toolbar span { margin-left:auto; color:var(--el-text-color-secondary); font-size:12px; }
.set-name-form { display:flex; flex-wrap:wrap; gap:10px; align-items:center; padding:14px 0; }.set-name-form label { font-size:13px; }.set-name-form .el-input { width:300px; max-width:100%; }.set-name-form small { color:var(--el-color-danger); }
.selected-set-tag { margin-left:8px; }.el-alert { margin-bottom:12px; }
</style>
