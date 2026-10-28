<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import TractionCharts from './TractionCharts.vue'
import TractionRunSummary from './TractionRunSummary.vue'
import { calculatedTractionProfile, summarizeTractionResult } from './tractionCalculation'
import { useTractionResultPreview } from './useTractionResultPreview'

const props = defineProps<{ instanceID: string; stationSchemeID: string; routeID: string; routeLabel: string }>()
const visible = defineModel<boolean>({ default: false })
const { locale } = useI18n()
const ui = (zh: string, en: string) => locale.value.startsWith('zh') ? zh : en
const { sets, selectedSetID, selectedCaseID, calculation, loading, error, reload, selectSet } =
    useTractionResultPreview(() => props)
const splitRatio = ref(0.56)
const showOutsideStation = ref(true)
const profile = computed(() => calculation.value ? calculatedTractionProfile(calculation.value) : null)
const summary = computed(() => calculation.value ? summarizeTractionResult(calculation.value.result,
    calculation.value.request.profile, calculation.value.request.signal) : null)
const resultRouteLabel = computed(() => [calculation.value?.selectedRouteId, calculation.value?.continuationId].filter(Boolean).join(' → '))
</script>

<template>
    <el-dialog v-model="visible" :title="ui('牵引计算', 'Traction calculation')" width="min(1440px, 96vw)" top="3vh" destroy-on-close class="calc-traction-dialog">
        <div class="traction-preview" v-loading="loading" :aria-busy="loading">
            <header class="preview-toolbar">
                <span class="current-route" :title="routeLabel">{{ ui('当前进路', 'Current route') }} · {{ routeLabel }}</span>
                <span v-if="calculation?.continuationId" class="continuation-route" :title="resultRouteLabel">{{ ui('接续计算', 'Combined run') }} · {{ resultRouteLabel }}</span>
                <label>{{ ui('计算方案', 'Calculation set') }}
                    <el-select :model-value="selectedSetID" size="small" :aria-label="ui('牵引计算方案', 'Traction calculation set')" :disabled="loading || !sets.length" @update:model-value="selectSet">
                        <el-option v-for="set in sets" :key="set.id" :value="set.id" :label="set.name" />
                    </el-select>
                </label>
                <el-checkbox v-model="showOutsideStation" size="small">{{ ui('显示站外内容', 'Show outside station') }}</el-checkbox>
                <el-button size="small" :disabled="loading" @click="reload">{{ ui('刷新', 'Refresh') }}</el-button>
            </header>
            <main class="preview-plots">
                <template v-if="calculation && profile">
                    <TractionRunSummary :summary="summary" :pending-route-label="calculation.isCurrent ? undefined : resultRouteLabel" />
                    <TractionCharts compact :key="selectedSetID + '/' + selectedCaseID" v-model:split-ratio="splitRatio" :profile="profile" :result="calculation.result"
                        :calculated-signal="calculation.request.signal" :train-length-m="calculation.result.trainLengthM"
                        :visible-start-m="showOutsideStation ? 0 : summary?.brakingDistanceM" :limits-editable="false" />
                </template>
                <div v-else-if="error" class="preview-empty" role="alert">
                    <strong>{{ ui('牵引计算结果加载失败', 'Could not load traction results') }}</strong>
                    <p>{{ ui('请检查网络和登录状态后重试。', 'Check your connection and sign-in, then retry.') }}</p>
                    <el-button size="small" @click="reload">{{ ui('重试', 'Retry') }}</el-button>
                </div>
                <div v-else-if="!loading" class="preview-empty" role="status">
                    <strong>{{ ui('未计算', 'Not calculated') }}</strong>
                </div>
            </main>
        </div>
    </el-dialog>
</template>

<style scoped>
.traction-preview { display:flex; flex-direction:column; height:min(80vh, 860px); min-height:280px; overflow:hidden; }
.preview-toolbar { display:flex; flex:none; flex-wrap:wrap; align-items:center; gap:8px 16px; padding:0 0 8px; font-size:12px; }
.current-route { max-width:280px; overflow:hidden; text-overflow:ellipsis; white-space:nowrap; }
.continuation-route { max-width:240px; overflow:hidden; text-overflow:ellipsis; white-space:nowrap; color:var(--el-text-color-secondary); }
.preview-toolbar label { display:flex; align-items:center; gap:8px; }
.preview-toolbar .el-select { width:180px; }
.preview-plots { display:flex; flex-direction:column; flex:1; min-width:0; min-height:0; overflow:hidden; padding:0 10px 8px; background:#0d1422; color:#dce5f5; border-radius:6px; }
.preview-plots :deep(.summary-strip) { height:38px; margin-bottom:6px; gap:12px; }
.preview-plots :deep(.summary-strip strong) { font-size:15px; }
.preview-empty { display:flex; flex:1; flex-direction:column; align-items:center; justify-content:center; gap:12px; padding:24px; text-align:center; }
.preview-empty p { margin:0; color:#91a2bd; font-size:13px; line-height:1.8; }
@media(max-width:800px) { .preview-toolbar label { flex:1 1 200px; }.preview-toolbar .el-select { flex:1; width:auto; min-width:120px; } }
</style>
