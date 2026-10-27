<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from "vue";
import { useI18n } from "vue-i18n";
import PaneDivider from "@/components/ui/PaneDivider.vue";
import { gradeColor } from "./geometry";
import SketchCanvas from "./SketchCanvas.vue";
import { loadGradient, LOAD_COLOR_LIMIT, probabilityColor, UNKNOWN_ANALYSIS_COLOR } from "./analysisOverlay";
import SketchCrossingAnalysis from "./SketchCrossingAnalysis.vue";
import SketchRelationshipChart from "./SketchRelationshipChart.vue";
import { resolveAnalysisSettings } from "./types";
import type {
  AnalysisResult,
  AnalysisSettings,
  SchemeDocument,
} from "./types";
const props = defineProps<{
  document: SchemeDocument;
  result?: AnalysisResult;
  busy: boolean;
  dirty: boolean;
}>();
const emit = defineEmits<{
  analyze: [];
  change: [];
}>();
const { locale } = useI18n();
const tr = (zh: string, en: string) => (locale.value === "zh" ? zh : en);
const analysisHost = ref<HTMLElement>();
const analysisPanelWidth = ref(520);
const availableWidth = ref(1200);
const availableHeight = ref(600);
const flowTableHeight = ref(220);
const flowAreaHeight = computed(() => Math.max(480, availableHeight.value));
const flowTableMax = computed(() => flowAreaHeight.value - 228);
const analysisPanelMax = computed(() => Math.max(360, availableWidth.value - 368));
let resizeObserver: ResizeObserver | undefined;
onMounted(() => {
  resizeObserver = new ResizeObserver(() => {
    const width = analysisHost.value?.clientWidth;
    const height = analysisHost.value?.clientHeight;
    if (width) availableWidth.value = width;
    if (height) availableHeight.value = height;
  });
  if (analysisHost.value) resizeObserver.observe(analysisHost.value);
});
onBeforeUnmount(() => resizeObserver?.disconnect());
const names = computed(() =>
  Object.fromEntries(props.document.facilities.map((f) => [f.id, f.name])),
);
const chainName = (planId: string, id: string) => {
  const plan = props.document.plans.find((p) => p.id === planId);
  return `${plan?.name || planId} / ${plan?.chains.find((c) => c.id === id)?.name || id}`;
};
const chains = computed(() => props.document.plans.flatMap((p) => p.chains));
const draftCount = computed(
  () => chains.value.filter((c) => c.steps.length < 2).length,
);
const ready = computed(() => !!chains.value.length && !draftCount.value);
const totalQuantity = computed(() =>
  props.document.plans.reduce((total, p) => total + p.quantity * p.chains.length, 0),
);
const settings = computed(() => resolveAnalysisSettings(props.document));
function updateSettings(key: keyof AnalysisSettings, value: unknown) {
  if (typeof value !== "number" || !Number.isFinite(value)) return;
  props.document.analysisSettings = { ...settings.value, [key]: value };
  emit("change");
}
const analysisTab = ref("conflicts");
const activeRelations = computed(
  () => props.result?.relations.filter((r) => r.total > 0) || [],
);
const englishWarnings = [
  "Each job starts independently and uniformly within [0, analysis horizon]. Dwell and travel times are fixed. Jobs may finish beyond the horizon. Facilities and links are exclusive, single-capacity resources in either direction.",
  "Analysis follows the current polyline or rounded shape, using 24 subdivisions per circular fillet. Crossing or overlapping links are mutually exclusive for their entire traversal. This is a conservative occupancy-conflict estimate, not a collision probability.",
  "Pair probability is the chance that two randomly selected jobs conflict at least once. Expected conflicting pairs can be added; this is not the probability of any conflict across the whole system.",
  "SLP grades use total bidirectional flow divided by the largest facility-pair flow. A/E/I thresholds are project settings; lower positive flow is O and zero flow is U. Non-flow relationships and X (undesirable closeness) are not evaluated.",
];
</script>
<template>
  <section ref="analysisHost" class="analysis" :style="{ '--analysis-panel-width': `${analysisPanelWidth}px` }">
    <div class="analysis-sidebar" :aria-label="tr('分析参数与结果', 'Analysis settings and results')">
    <div class="analysis-toolbar">
      <el-button type="primary" :loading="busy" :disabled="!ready || busy" @click="emit('analyze')">
        {{ tr(dirty ? "保存并分析" : "生成分析", dirty ? "Save and analyze" : "Run analysis") }}
      </el-button>
      <label class="horizon-setting">
        {{ tr("统一分析时段（秒）", "Analysis window (seconds)") }}
        <el-input-number :model-value="settings.horizonSeconds"
          :aria-label="tr('统一分析时段（秒）', 'Shared analysis window (seconds)')"
          :min="1" :max="31536000" @change="updateSettings('horizonSeconds', $event)" />
      </label>
      <span class="scope-summary">{{ tr(
        `全部 ${document.plans.length} 套流程 · ${chains.length} 项作业内容 · ${totalQuantity} 次作业`,
        `All ${document.plans.length} plans · ${chains.length} chains · ${totalQuantity} jobs`) }}</span>
    </div>
    <p v-if="!ready" class="draft-warning">{{ tr(
      chains.length ? `还有 ${draftCount} 项草稿作业内容，请补全至至少两处设施。` : "请先在作业流程设计中添加完整作业内容。",
      chains.length ? `${draftCount} draft chains remain. Add at least two facilities to each.` : "Add a complete chain in Process design first.") }}</p>
    <el-tabs v-model="analysisTab" class="analysis-tabs">
      <el-tab-pane :label="tr('冲突分析', 'Conflict analysis')" name="conflicts">
        <div class="analysis-tab-content">
          <SketchCrossingAnalysis :document="document" :results="result?.crossingRisks" @change="emit('change')" />
          <template v-if="result">
            <div class="conflict-summary">{{ tr(
              `作业内容冲突组合 ${result.conflicts.length} 组 · 期望冲突作业对数 ${result.expectedConflictingPairs.toLocaleString(undefined, { maximumFractionDigits: 2 })}`,
              `${result.conflicts.length} conflicting chain combinations · ${result.expectedConflictingPairs.toLocaleString(undefined, { maximumFractionDigits: 2 })} expected conflicting job pairs`) }}</div>
      <details class="panel chain-details">
        <summary>
          {{
            tr(
              "作业内容占用冲突 · 均匀出发模型",
              "Chain occupancy conflicts · Uniform starts",
            )
          }}
        </summary>
        <el-table
          :data="result.conflicts"
          size="small"
          :empty-text="
            tr(
              '当前假设下未发现作业占用冲突',
              'No occupancy conflicts under the current assumptions',
            )
          "
          ><el-table-column
            :label="tr('作业内容组合', 'Process chains')"
            min-width="200"
            ><template #default="{ row }"
              >{{ chainName(row.firstPlanId, row.firstChainId) }} ↔
              {{ chainName(row.secondPlanId, row.secondChainId)
              }}<span
                v-if="
                  row.firstPlanId === row.secondPlanId &&
                  row.firstChainId === row.secondChainId
                "
                >{{ tr("（不同批次）", " (different jobs)") }}</span
              ></template
            ></el-table-column
          ><el-table-column
            :label="tr('单对冲突概率', 'Pair probability')"
            width="140"
            ><template #default="{ row }"
              ><b :style="{ color: probabilityColor(row.pairProbability) }">{{ (row.pairProbability * 100).toFixed(2) }}%</b></template
            ></el-table-column
          ><el-table-column
            prop="jobPairs"
            :label="tr('作业对数', 'Job pairs')"
            width="120"
          /><el-table-column
            :label="tr('期望冲突对数', 'Expected conflicts')"
            width="150"
            ><template #default="{ row }">{{
              row.expectedConflictingPairs.toFixed(2)
            }}</template></el-table-column
          ><el-table-column
            :label="tr('冲突资源', 'Conflict resources')"
            min-width="240"
            ><template #default="{ row }">{{
              row.resources.map((r: any) => r.name).join("；")
            }}</template></el-table-column
          ></el-table
        >
      </details>

          </template>
        </div>
      </el-tab-pane>
      <el-tab-pane :label="tr('作业关系', 'Flow relationships')" name="flow">
        <div class="analysis-tab-content">
          <div class="analysis-settings">
            <label v-for="key in ['aThreshold', 'eThreshold', 'iThreshold'] as const" :key="key">
              {{ key[0]?.toUpperCase() }} {{ tr("阈值", "threshold") }}
              <el-input-number :model-value="settings[key]"
                :aria-label="key[0]?.toUpperCase() + tr(' 阈值', ' threshold')"
                :min="0.01" :max="1" :step="0.05" :precision="2" @change="updateSettings(key, $event)" />
            </label>
            <p>{{ tr("按全部流程汇总的设施对双向作业量分级：1 ≥ A > E > I > 0，低于 I 的正流量为 O，零流量为 U。", "Grades use combined bidirectional facility-pair flow across all plans: 1 ≥ A > E > I > 0. Positive flow below I is O; zero flow is U.") }}</p>
          </div>
          <div v-if="result" class="flow-relationships"
            :style="{ '--flow-table-height': `${flowTableHeight}px`, '--flow-area-height': `${flowAreaHeight}px` }">
          <div class="panel flow-table">
            <h3>{{ tr("设施关系与流量", "Facility relationships and flow") }} · {{ activeRelations.length }} {{ tr("对有作业", "pairs with flow") }}</h3>
          <div class="flow-table-body">
          <el-table :data="result.relations" height="100%" size="small"
            ><el-table-column
              :label="tr('设施对', 'Facility pair')"
              min-width="160"
              ><template #default="{ row }"
                >{{ names[row.fromId] }} ↔ {{ names[row.toId] }}</template
              ></el-table-column
            ><el-table-column
              :label="tr('正向 / 反向', 'Forward / reverse')"
              width="130"
              ><template #default="{ row }"
                >{{ row.forward }} / {{ row.reverse }}</template
              ></el-table-column
            ><el-table-column
              prop="total"
              :label="tr('总量', 'Total')"
              width="90"
            /><el-table-column label="SLP" width="60"
              ><template #default="{ row }"
                ><b :style="{ color: gradeColor[row.grade] }">{{
                  row.grade
                }}</b></template
              ></el-table-column
            ></el-table
          >
          </div>
          </div>
          <PaneDivider v-model="flowTableHeight" direction="vertical" :min="120" :max="flowTableMax" :reset-value="220"
            :label="tr('调整设施关系表与活动关系图高度', 'Resize flow table and relationship chart heights')" />
          <SketchRelationshipChart class="flow-chart" :facilities="document.facilities" :relations="result.relations" />
          </div>
          <el-empty v-else :image-size="52" :description="tr('生成分析后显示设施作业关系。', 'Run analysis to show facility flow relationships.')" />
        </div>
      </el-tab-pane>
    </el-tabs>
    <template v-if="result">
      <details class="assumptions">
        <summary>
          {{
            tr("SLP 与作业内容计算口径", "SLP and chain analysis assumptions")
          }}
        </summary>
        <ul>
          <li v-for="(warning, i) in result.warnings" :key="i">
            {{
              locale === "zh"
                ? warning
                : englishWarnings[i] ||
                  "Some processes last longer than the start window. Check units and horizon."
            }}
          </li>
        </ul>
        <a
          href="https://richardmuther.com/systematic-planning-methods/"
          target="_blank"
          rel="noopener noreferrer"
          >{{
            tr(
              "SLP 方法参考：Richard Muther & Associates",
              "SLP reference: Richard Muther & Associates",
            )
          }}</a
        >
      </details>

    </template>
    </div>
    <PaneDivider v-model="analysisPanelWidth" :min="360" :max="analysisPanelMax" :reset-value="520"
      :label="tr('调整分析卡片与布置图宽度', 'Resize analysis and layout panes')" />
    <section class="analysis-layout" :aria-label="tr('布置图分析视图', 'Layout analysis view')">
      <div class="layout-heading">
        <h3>{{ tr("布置图分析", "Layout analysis") }}</h3>
        <span>{{ tr("连接：设施对 SLP 等级 · 圆圈：负荷率", "Links: facility-pair SLP grade · Circles: load ratio") }}</span>
      </div>
      <div class="analysis-legends">
        <div class="flow-legend" :aria-label="tr('作业关系色谱', 'Flow grade spectrum')">
          <b>{{ tr("作业关系", "Flow") }}</b>
          <span v-for="grade in ['U', 'O', 'I', 'E', 'A']" :key="grade"><i :style="{ background: gradeColor[grade] }" />{{ grade }}</span>
          <small>{{ tr("低 → 高", "Low → High") }}</small>
        </div>
        <div class="load-legend" :aria-label="tr('负荷率色谱', 'Load ratio spectrum')">
          <b>{{ tr("负荷率", "Load ratio") }}</b>
          <span>0%</span><i :style="{ background: loadGradient }" /><span>≥{{ LOAD_COLOR_LIMIT * 100 }}%</span>
          <span class="unknown"><i :style="{ background: UNKNOWN_ANALYSIS_COLOR }" />{{ tr("未计算 / 无数据", "Pending / no data") }}</span>
        </div>
      </div>
      <p v-if="!result" class="pending-note">{{ tr("生成分析后显示色谱。修改分析参数后，请重新生成分析。", "Run analysis to show result colors. Recalculate after changing parameters.") }}</p>
      <SketchCanvas :document="document" analysis-mode :analysis-result="result" />
    </section>
  </section>
</template>

<style scoped>
.analysis { display: grid; grid-template-columns: var(--analysis-panel-width) 8px minmax(360px, 1fr); min-width: 0; height: clamp(480px, calc(100dvh - 144px), 1200px); overflow-x: auto; }
.analysis-sidebar { min-width: 0; min-height: 0; overflow: auto; overscroll-behavior: contain; padding-right: 8px; }
.analysis-toolbar { display: flex; align-items: center; flex-wrap: wrap; gap: 12px 20px; margin-bottom: 12px; font-size: 12px; }
.horizon-setting { display: flex; align-items: center; gap: 10px; }
.horizon-setting .el-input-number { width: 170px; }
.scope-summary { color: var(--sy-text-muted); }
.analysis-tabs { background: white; border: 1px solid var(--sy-border); border-radius: 6px; padding: 0 16px; margin-bottom: 16px; }
.analysis-tabs :deep(.el-tabs__header) { position: sticky; top: 0; z-index: 2; background: white; margin-bottom: 0; }
.analysis-tab-content { padding: 12px 0; }
.analysis-tab-content :deep(.crossing-analysis) { margin: 0; border: 0; padding: 0; }
.analysis-settings { display: flex; gap: 12px; align-items: center; flex-wrap: wrap; padding-bottom: 12px; }
.analysis-settings label { display: flex; align-items: center; gap: 6px; font-size: 12px; }
.analysis-settings .el-input-number { width: 104px; }
.analysis-settings p { flex-basis: 100%; font-size: 12px; color: var(--sy-text-muted); margin: 0; line-height: 1.7; }
.flow-relationships { display: grid; grid-template-rows: var(--flow-table-height) 8px minmax(0, 1fr); height: var(--flow-area-height); min-width: 0; }
.flow-table { display: flex; flex-direction: column; min-height: 0; padding-bottom: 8px; }
.flow-table h3 { flex-shrink: 0; }
.flow-table-body { flex: 1; min-height: 0; min-width: 0; }
.flow-chart { display: flex; flex-direction: column; min-height: 0; overflow: auto; padding-top: 8px; }
.flow-chart :deep(.chart-scroll) { flex: 1; min-height: 0; max-height: none; }
.flow-chart :deep(h3), .flow-chart :deep(.chart-subtitle), .flow-chart :deep(.chart-legend), .flow-chart :deep(.chart-detail) { flex-shrink: 0; }
.panel { min-width: 0; }
h3 { font-size: 14px; font-weight: 600; margin: 0 0 10px; }
.conflict-summary { font-size: 12px; margin: 12px 0; color: var(--sy-text-muted); }
.chain-details summary, .assumptions summary { cursor: pointer; font-weight: 600; }
.chain-details { font-size: 13px; padding: 12px 0; }
.analysis-layout { display: flex; flex-direction: column; min-width: 0; min-height: 0; padding-left: 8px; }
.analysis-layout :deep(.analysis-workspace) { flex: 1; min-height: 200px; overflow: hidden; }
.analysis-layout :deep(.canvas-shell) { display: flex; flex-direction: column; min-height: 0; }
.analysis-layout :deep(.canvas-toolbar), .analysis-layout :deep(.canvas-status) { flex-shrink: 0; }
.analysis-layout :deep(.canvas-stage) { flex: 1; min-height: 0; }
.analysis-layout :deep(.analysis-workspace .sketch-canvas) { height: 100%; }
.layout-heading { display: flex; align-items: baseline; flex-wrap: wrap; gap: 8px 16px; }
.layout-heading span, .pending-note { font-size: 12px; color: var(--sy-text-muted); }
.analysis-legends { display: flex; flex-wrap: wrap; gap: 12px 28px; padding: 10px 0 14px; font-size: 12px; }
.flow-legend, .load-legend { display: flex; align-items: center; flex-wrap: wrap; gap: 10px; }
.flow-legend span, .unknown { display: inline-flex; align-items: center; gap: 5px; }
.flow-legend i { width: 22px; height: 4px; border-radius: 2px; }
.flow-legend small { color: var(--sy-text-muted); }
.load-legend > i { width: 150px; height: 10px; border-radius: 5px; }
.unknown { color: var(--sy-text-muted); }
.unknown i { width: 10px; height: 10px; border-radius: 50%; opacity: 0.6; }
.draft-warning { color: #a27839; font-size: 12px; }
.assumptions { font-size: 12px; line-height: 1.8; color: var(--sy-text-muted); padding: 12px 16px; background: #eef2f7; border-radius: 6px; }
.assumptions ul { padding-left: 18px; }
</style>
