<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Delete, DocumentCopy, Edit, Plus, Refresh, Setting, Star } from '@element-plus/icons-vue'
import axios from 'axios'
import ActionButton from '@/components/ui/ActionButton.vue'
import { useCalculationParameterSets, type CalculationParameterSet } from '../calculationParameterSets'

const props = defineProps<{ instanceID: string; stationSchemeID: string; modelValue: string; disabled?: boolean }>()
const emit = defineEmits<{ 'update:modelValue': [value: string]; change: [] }>()
const { t } = useI18n()
const { sets, loading, error, reload } = useCalculationParameterSets(() => props.instanceID, () => props.stationSchemeID)
const saving = ref(false)
const managerVisible = ref(false)
const editorVisible = ref(false)
const mode = ref<'create' | 'copy' | 'rename'>('create')
const editingID = ref('')
const editingSet = computed(() => sets.value.find(set => set.id === editingID.value))
const form = ref({ name: '', isDefault: false, copyFromID: '' })
const blocked = computed(() => props.disabled || loading.value || saving.value || !props.instanceID || !props.stationSchemeID)
watch(sets, () => {
    const id = sets.value.find(set => set.id === props.modelValue)?.id || sets.value.find(set => set.isDefault)?.id || ''
    emit('update:modelValue', id)
    emit('change')
})
watch([() => props.instanceID, () => props.stationSchemeID], () => {
    managerVisible.value = false
    editorVisible.value = false
})
function openManager() {
    managerVisible.value = true
    void reload()
}
function openEditor(next: typeof mode.value, set?: CalculationParameterSet) {
    mode.value = next
    editingID.value = set?.id || ''
    form.value = {
        name: next === 'create' ? '' : (set?.name || '') + (next === 'copy' ? t('calculationParameterSets.copySuffix') : ''),
        isDefault: next === 'rename' && !!set?.isDefault,
        copyFromID: next === 'copy' ? set?.id || '' : '',
    }
    editorVisible.value = true
}
function fail(reason: any) { ElMessage.error(typeof reason.response?.data === 'string' ? reason.response.data : t('calculationParameterSets.failed')) }
async function save(defaultSet?: CalculationParameterSet) {
    if (blocked.value || (!defaultSet && !form.value.name.trim())) return
    const scope = { instanceID: props.instanceID, stationSchemeID: props.stationSchemeID }
    const creating = !defaultSet && mode.value !== 'rename'
    saving.value = true
    try {
        const response = await axios.post('/StationLayout/SaveCalculationParameterSet', { ...scope,
            id: defaultSet?.id || (creating ? undefined : editingID.value),
            name: defaultSet?.name ?? form.value.name.trim(),
            isDefault: !!defaultSet || form.value.isDefault,
            copyFromID: creating && mode.value === 'copy' ? form.value.copyFromID : undefined })
        if (scope.instanceID !== props.instanceID || scope.stationSchemeID !== props.stationSchemeID) return
        editorVisible.value = false
        if (creating) emit('update:modelValue', response.data.id ?? response.data.ID)
        await reload()
        ElMessage.success(t('calculationParameterSets.saved'))
    } catch (reason) { fail(reason) } finally { saving.value = false }
}
async function remove(set: CalculationParameterSet) {
    const scope = { instanceID: props.instanceID, stationSchemeID: props.stationSchemeID, id: set.id }
    try { await ElMessageBox.confirm(t('calculationParameterSets.deleteConfirm', { name: set.name }), t('calculationParameterSets.delete'), { type: 'warning' }) }
    catch { return }
    if (blocked.value || scope.instanceID !== props.instanceID || scope.stationSchemeID !== props.stationSchemeID) return
    saving.value = true
    try { await axios.delete('/StationLayout/DeleteCalculationParameterSet', { params: scope }); await reload() }
    catch (reason) { fail(reason) } finally { saving.value = false }
}
</script>

<template>
    <div class="parameter-set-manager" role="group" :aria-label="t('calculationParameterSets.title')">
        <span class="parameter-set-manager__label">{{ t('calculationParameterSets.title') }}</span>
        <el-select :model-value="modelValue" :disabled="blocked" :loading="loading" size="small" class="parameter-set-manager__select" :aria-label="t('calculationParameterSets.title')"
            @update:model-value="emit('update:modelValue', $event); emit('change')">
            <el-option v-for="set in sets" :key="set.id" :value="set.id" :label="set.name + (set.isDefault ? ` (${t('calculationParameterSets.default')})` : '')" />
        </el-select>
        <ActionButton :icon="Setting" :label="t('calculationParameterSets.manage')" :disabled="blocked" @click="openManager" />
        <span v-if="error" role="alert" class="parameter-set-manager__error">{{ t('calculationParameterSets.failed') }}</span>
    </div>
    <el-dialog v-model="managerVisible" :title="t('calculationParameterSets.manage')" width="720px" class="parameter-set-dialog"
        :close-on-click-modal="!saving" :show-close="!saving" :close-on-press-escape="!saving">
        <div class="parameter-set-dialog__toolbar">
            <ActionButton :icon="Plus" :label="t('calculationParameterSets.create')" :disabled="blocked" @click="openEditor('create')" />
            <ActionButton :icon="Refresh" :label="t('calculationParameterSets.refresh')" :disabled="blocked" @click="reload" />
        </div>
        <el-alert v-if="error" :title="t('calculationParameterSets.failed')" type="error" :closable="false" class="parameter-set-dialog__error" />
        <el-table v-loading="loading" :data="sets" row-key="id" max-height="400">
            <el-table-column prop="name" :label="t('calculationParameterSets.name')" min-width="220" show-overflow-tooltip>
                <template #default="{ row }">
                    <span>{{ row.name }}</span>
                    <el-tag v-if="row.isDefault" size="small" class="parameter-set-dialog__default">{{ t('calculationParameterSets.default') }}</el-tag>
                </template>
            </el-table-column>
            <el-table-column :label="t('calculationParameterSets.actions')" width="170" align="center">
                <template #default="{ row }">
                    <div class="parameter-set-dialog__actions">
                        <ActionButton :icon="DocumentCopy" :label="t('calculationParameterSets.copy')" :disabled="blocked" @click="openEditor('copy', row)" />
                        <ActionButton :icon="Edit" :label="t('calculationParameterSets.rename')" :disabled="blocked" @click="openEditor('rename', row)" />
                        <ActionButton :icon="Star" :label="t('calculationParameterSets.makeDefault')" :disabled="blocked || row.isDefault" @click="save(row)" />
                        <ActionButton :icon="Delete" :label="t('calculationParameterSets.delete')" type="danger" :disabled="blocked || row.isDefault" @click="remove(row)" />
                    </div>
                </template>
            </el-table-column>
        </el-table>
        <p class="parameter-set-dialog__help">{{ t('calculationParameterSets.fallbackHelp') }}</p>
    </el-dialog>
    <el-dialog v-model="editorVisible" :title="t(`calculationParameterSets.${mode}`)" width="460px" append-to-body
        :close-on-click-modal="!saving" :show-close="!saving" :close-on-press-escape="!saving">
        <el-form label-position="top" :disabled="saving" @submit.prevent="save()">
            <el-form-item :label="t('calculationParameterSets.name')" required><el-input v-model="form.name" maxlength="200" /></el-form-item>
            <el-form-item v-if="mode === 'copy'" :label="t('calculationParameterSets.copyFrom')">
                <el-select v-model="form.copyFromID"><el-option v-for="set in sets" :key="set.id" :value="set.id" :label="set.name" /></el-select>
            </el-form-item>
            <el-checkbox v-model="form.isDefault" :disabled="mode === 'rename' && editingSet?.isDefault">{{ t('calculationParameterSets.makeDefault') }}</el-checkbox>
            <p class="parameter-set-dialog__help">{{ t('calculationParameterSets.fallbackHelp') }}</p>
        </el-form>
        <template #footer>
            <ActionButton variant="text" :label="t('calculationParameterSets.cancel')" :disabled="saving" @click="editorVisible = false" />
            <ActionButton variant="text" type="primary" :label="t('calculationParameterSets.save')" :loading="saving" :disabled="blocked || !form.name.trim()" @click="save()" />
        </template>
    </el-dialog>
</template>

<style scoped>
.parameter-set-manager { display: flex; align-items: center; flex-wrap: wrap; gap: 8px; padding: 8px 12px; border-bottom: 1px solid var(--el-border-color); }
.parameter-set-manager__label { color: var(--el-text-color-regular); font-size: 12px; white-space: nowrap; }
.parameter-set-manager__select { width: 220px; max-width: 100%; }
.parameter-set-manager__error { color: var(--el-color-danger); font-size: 12px; }
.parameter-set-dialog__toolbar, .parameter-set-dialog__actions { display: flex; align-items: center; gap: 8px; }
.parameter-set-dialog__toolbar { margin-bottom: 12px; }
.parameter-set-dialog__actions { justify-content: center; }
.parameter-set-dialog__default { margin-left: 8px; }
.parameter-set-dialog__error { margin-bottom: 12px; }
.parameter-set-dialog__help { color: var(--el-text-color-secondary); font-size: 12px; line-height: 1.6; }
</style>
