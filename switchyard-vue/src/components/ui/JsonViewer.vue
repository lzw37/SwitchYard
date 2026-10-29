<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import { useI18n } from 'vue-i18n'
import JsonTreeNode from './JsonTreeNode.vue'

const props = withDefaults(defineProps<{ source: string; emptyText?: string; loading?: boolean }>(), { emptyText: '', loading: false })
const { t } = useI18n()
const expandDepth = ref(2)
const treeVersion = ref(0)
watch(() => props.source, () => { treeVersion.value++ })
const parsed = computed(() => {
    if (!props.source.trim()) return { empty: true, value: null, error: '' }
    try { return { empty: false, value: JSON.parse(props.source), error: '' } }
    catch (error) { return { empty: false, value: null, error: error instanceof Error ? error.message : String(error) } }
})
const size = computed(() => {
    const bytes = new TextEncoder().encode(props.source).length
    return bytes >= 1024 ? (bytes / 1024).toFixed(1) + ' KB' : bytes + ' B'
})
function expand(depth: number) { expandDepth.value = depth; treeVersion.value++ }
async function copy() {
    try { await navigator.clipboard.writeText(props.source); ElMessage.success(t('modelSolving.viewer.copied')) }
    catch { ElMessage.error(t('modelSolving.viewer.copyFailed')) }
}
</script>

<template>
    <div v-loading="loading" class="json-viewer">
        <div v-if="!parsed.empty" class="json-viewer-toolbar">
            <span class="json-format">JSON</span><span class="json-size">{{ size }}</span>
            <div class="json-tools">
                <el-button text size="small" :disabled="!!parsed.error" @click="expand(Infinity)">{{ t('modelSolving.viewer.expand') }}</el-button>
                <el-button text size="small" :disabled="!!parsed.error" @click="expand(0)">{{ t('modelSolving.viewer.collapse') }}</el-button>
                <el-button text size="small" @click="copy">{{ t('modelSolving.viewer.copy') }}</el-button>
            </div>
        </div>
        <div class="json-viewer-content">
            <el-empty v-if="parsed.empty" :description="emptyText" :image-size="60" />
            <el-alert v-else-if="parsed.error" :title="t('modelSolving.messages.invalidJson')" :description="parsed.error" type="error" :closable="false" show-icon />
            <JsonTreeNode v-else :key="treeVersion" :value="parsed.value" :depth="0" :expand-depth="expandDepth" />
        </div>
    </div>
</template>

<style scoped>
.json-viewer { display: flex; flex-direction: column; flex: 1; min-width: 0; min-height: 160px; height: 100%; border: 1px solid #e5eaf1; border-radius: 7px; background: #f8fafc; overflow: hidden; }
.json-viewer-toolbar { display: flex; flex-wrap: wrap; align-items: center; gap: 10px; min-height: 36px; padding: 4px 12px; border-bottom: 1px solid #e5eaf1; background: #fff; }
.json-format { color: #44688d; font: 600 11px ui-monospace, Consolas, monospace; letter-spacing: .5px; }
.json-size { color: #94a3b8; font-size: 11px; }
.json-tools { display: flex; margin-left: auto; }
.json-tools :deep(.el-button + .el-button) { margin-left: 0; }
.json-viewer-content { flex: 1; min-height: 0; overflow: auto; padding: 16px 20px; font: 12px/25px ui-monospace, 'Cascadia Code', Consolas, monospace; tab-size: 2; }
</style>
