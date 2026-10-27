<script setup lang="ts">
import { computed } from "vue";
import { useI18n } from "vue-i18n";
import { crossingTraffic } from "./crossingAnalysis";
import { crossingLoadRatio, loadColor, probabilityColor } from "./analysisOverlay";
import { resolveAnalysisSettings } from "./types";
import type { CrossingQueueMetrics, CrossingRisk, SchemeDocument } from "./types";

const props = defineProps<{
  document: SchemeDocument;
  results?: CrossingRisk[];
}>();
const emit = defineEmits<{ change: [] }>();
const { locale } = useI18n();
const tr = (zh: string, en: string) => (locale.value === "zh" ? zh : en);
const settings = computed(() => resolveAnalysisSettings(props.document));
const models = computed(() => [
  {
    value: "poisson-fixed",
    label: tr("泊松到达 · 固定占用时间", "Poisson arrivals · Fixed occupancy"),
  },
  {
    value: "uniform-fixed",
    label: tr("均匀到达 · 固定占用时间", "Uniform arrivals · Fixed occupancy"),
  },
  {
    value: "poisson-exponential",
    label: tr(
      "泊松到达 · 指数占用时间",
      "Poisson arrivals · Exponential occupancy",
    ),
  },
]);
const traffic = computed(() => crossingTraffic(props.document));
const rows = computed(() => {
  const links = new Map(props.document.links.map((l) => [l.id, l]));
  const risks = new Map(props.results?.map((r) => [r.crossingId, r]));
  return (props.document.crossings ?? []).map((c) => {
    const risk = risks.get(c.id);
    const routes = (c.sourceLinks ?? []).map((s) => {
      const flow = traffic.value.get(s.linkId);
      const passages = flow?.passages ?? 0;
      const automatic = passages ? flow!.weightedSeconds / passages : null;
      return {
        ...s,
        name: links.get(s.linkId)?.name ?? s.name,
        missingLink: !links.has(s.linkId),
        passages,
        automatic,
        seconds: s.occupancySeconds ?? automatic,
        ratePerHour: (passages * 3600) / settings.value.horizonSeconds,
        probability: risk?.routes.find((r) => r.linkId === s.linkId)
          ?.conflictProbability,
      };
    });
    const passages = routes.reduce((sum, r) => sum + r.passages, 0);
    const status =
      risk?.status ??
      (routes.length < 2
        ? "missing-sources"
        : routes.some((r) => r.missingLink)
          ? "missing-links"
          : passages === 0
            ? "no-traffic"
            : "pending");
    return { ...c, routes, passages, status, risk };
  });
});
function updateModel(value: unknown) {
  if (
    value !== "poisson-fixed" &&
    value !== "uniform-fixed" &&
    value !== "poisson-exponential"
  )
    return;
  if (value === settings.value.crossingModel) return;
  props.document.analysisSettings = { ...settings.value, crossingModel: value };
  emit("change");
}
function updateOccupancy(crossingId: string, linkId: string, value: unknown) {
  const source = props.document.crossings
    ?.find((c) => c.id === crossingId)
    ?.sourceLinks?.find((s) => s.linkId === linkId);
  if (!source) return;
  const seconds = value == null ? null : value;
  if (
    seconds !== null &&
    (typeof seconds !== "number" ||
      !Number.isFinite(seconds) ||
      seconds < 0.01 ||
      seconds > 31536000)
  )
    return;
  if ((source.occupancySeconds ?? null) === seconds) return;
  source.occupancySeconds = seconds;
  emit("change");
}
const percent = (p: number | null | undefined) =>
  p == null
    ? "—"
    : (p * 100).toLocaleString(undefined, { maximumFractionDigits: 4 }) + "%";
const number = (value: number | null | undefined) =>
  value == null ? "—" : value.toLocaleString(undefined, { maximumFractionDigits: 3 });
function queueValue(queue: CrossingQueueMetrics | undefined, key: "meanDelaySeconds" | "meanQueueLength") {
  return queue?.status === "unstable" ? tr("无有限稳态", "No finite steady state") : number(queue?.[key]);
}
function queueStatus(queue: CrossingQueueMetrics | undefined) {
  if (!queue) return tr("待生成分析", "Run analysis");
  if (queue.status === "ready") return queue.model === "mg1-fcfs"
    ? tr("稳态 · 先到先服务", "Steady state · FCFS")
    : queue.simulationRuns
      ? tr(`仿真估计 · ${queue.simulationRuns} 次`, `Simulation · ${queue.simulationRuns} runs`)
      : tr("有限时段 · 先到先服务", "Finite window · FCFS");
  if (queue.status === "unstable") return tr("负荷达到或超过 100%", "Load is at least 100%");
  if (queue.status === "calculation-limit") return tr("排队仿真规模超限", "Queue simulation limit reached");
  return statusText(queue.status);
}
function statusText(status: string) {
  switch (status) {
    case "missing-sources":
      return tr(
        "缺少来源：请在布置图中重新生成“进路交叉”",
        "Missing sources: generate route crossings in Layout design",
      );
    case "missing-links":
      return tr(
        "来源进路已删除，请核对标记",
        "A source route was deleted; review this marker",
      );
    case "no-traffic":
      return tr("无通行作业", "No passages");
    case "calculation-limit":
      return tr(
        "计算未收敛，请减少参与进路或调整参数",
        "Calculation limit reached; reduce routes or adjust parameters",
      );
    case "ready":
      return "";
    default:
      return tr("待生成分析", "Run analysis");
  }
}
</script>

<template>
  <section
    class="crossing-analysis"
    :aria-label="
      tr('进路交叉点冲突与排队分析', 'Route crossing conflict and queue analysis')
    "
  >
    <div class="heading">
      <div>
        <h3>
          {{ tr("进路交叉点冲突与排队分析", "Route crossing conflict and queue analysis") }}
        </h3>
        <p>
          {{
            tr(
              "汇总全部流程方案，逐个分析已保存的交叉点。",
              "Combines all process plans for each saved crossing.",
            )
          }}
        </p>
      </div>
      <label
        >{{ tr("交叉点概率模型", "Crossing probability model") }}
        <el-select
          :model-value="settings.crossingModel"
          :aria-label="tr('交叉点概率模型', 'Crossing probability model')"
          @change="updateModel"
        >
          <el-option
            v-for="m in models"
            :key="m.value"
            :value="m.value"
            :label="m.label"
          />
        </el-select>
      </label>
    </div>
    <el-empty
      v-if="!rows.length"
      :image-size="60"
      :description="
        tr(
          '暂无交叉点，请先在布置图设计中点击“进路交叉”生成。',
          'No crossings. Generate route crossings in Layout design first.',
        )
      "
    />
    <el-table
      v-else
      :data="rows"
      row-key="id"
      size="small"
      max-height="640"
    >
      <el-table-column type="expand">
        <template #default="{ row }">
          <div class="route-details">
            <p>
              {{
                tr(
                  "占用时间默认使用全部流程中该通道按通行次数加权的平均通行时间。可改为该交叉区的实际占用时间；指数模型下输入平均值（秒）。",
                  "Occupancy defaults to mean route travel time weighted by passages across all plans. Override with time spent in this crossing area; enter the mean for exponential occupancy.",
                )
              }}
            </p>
            <el-table :data="row.routes" size="small">
              <el-table-column
                :label="tr('参与进路', 'Source route')"
                min-width="140"
                ><template #default="{ row: route }"
                  >{{ route.name }}
                  <span v-if="route.missingLink" class="warning">{{
                    tr("（已删除）", " (deleted)")
                  }}</span></template
                ></el-table-column
              >
              <el-table-column
                prop="passages"
                :label="tr('通行次数', 'Passages')"
                width="100"
              />
              <el-table-column
                :label="tr('到达率（次/小时）', 'Arrivals / hour')"
                width="150"
                ><template #default="{ row: route }">{{
                  route.ratePerHour.toLocaleString(undefined, {
                    maximumFractionDigits: 2,
                  })
                }}</template></el-table-column
              >
              <el-table-column
                :label="tr('占用时间（秒）', 'Occupancy (seconds)')"
                min-width="240"
                ><template #default="{ row: route }">
                  <div class="time-input">
                    <el-input-number
                      :model-value="route.seconds"
                      :aria-label="`${row.name} / ${route.name} ${tr('占用时间（秒）', 'occupancy (seconds)')}`"
                      :min="0.01"
                      :max="31536000"
                      :precision="2"
                      :controls="false"
                      :disabled="route.missingLink"
                      @change="updateOccupancy(row.id, route.linkId, $event)"
                    />
                    <el-button
                      v-if="route.occupancySeconds != null"
                      link
                      type="primary"
                      :aria-label="`${row.name} / ${route.name} ${tr('恢复自动', 'Reset to automatic')}`"
                      @click="updateOccupancy(row.id, route.linkId, null)"
                      >{{ tr("恢复自动", "Reset") }}</el-button
                    >
                    <span v-else class="automatic">{{
                      tr("自动", "Auto")
                    }}</span>
                  </div>
                </template></el-table-column
              >
              <el-table-column
                :label="tr('该进路遇到冲突', 'Route conflict probability')"
                min-width="150"
                ><template #default="{ row: route }"
                  ><b :style="{ color: probabilityColor(route.probability) }">{{ percent(route.probability) }}</b></template
                ></el-table-column
              >
            </el-table>
            <p v-if="row.risk?.correlatedRoutes" class="warning">
              {{
                tr(
                  "同一作业内容多次通过此处或使用多条参与进路，实际到达可能相关。当前结果采用各次到达独立的近似。",
                  "A chain revisits this crossing or uses multiple source routes. Actual arrivals may be correlated; this result assumes independent arrivals.",
                )
              }}
            </p>
            <p v-if="row.risk?.queue?.simulationRuns">
              {{ tr(
                `均匀到达采用 ${row.risk.queue.simulationRuns} 次可复现仿真。平均延误标准误为 ${number(row.risk.queue.delayStandardErrorSeconds)} 秒，平均队长标准误为 ${number(row.risk.queue.queueLengthStandardError)} 项。延误跟踪到作业开始服务；队长仅在分析时段内取时间平均。`,
                `${row.risk.queue.simulationRuns} reproducible uniform-arrival simulations. Standard errors: ${number(row.risk.queue.delayStandardErrorSeconds)} s for delay and ${number(row.risk.queue.queueLengthStandardError)} jobs for queue length. Delay follows jobs until service starts; queue length is averaged only over the analysis window.`,
              ) }}
            </p>
          </div>
        </template>
      </el-table-column>
      <el-table-column
        prop="name"
        :label="tr('交叉点', 'Crossing')"
        min-width="130"
      />
      <el-table-column :label="tr('参与进路', 'Source routes')" min-width="170"
        ><template #default="{ row }">{{
          row.routes.map((r: { name: string }) => r.name).join(" / ") || "—"
        }}</template></el-table-column
      >
      <el-table-column
        prop="passages"
        :label="tr('总通行次数', 'Total passages')"
        width="115"
      />
      <el-table-column
        :label="tr('单次通行冲突概率', 'Conflict per passage')"
        min-width="160"
        ><template #default="{ row }">
          <strong v-if="row.status === 'ready'" class="probability" :style="{ color: probabilityColor(row.risk?.conflictProbability) }">{{
            percent(row.risk?.conflictProbability)
          }}</strong>
          <span
            v-else
            :class="{
              warning:
                row.status.startsWith('missing') ||
                row.status === 'calculation-limit',
            }"
            >{{ statusText(row.status) }}</span
          >
        </template></el-table-column
      >
      <el-table-column :label="tr('平均延误（秒）', 'Mean delay (s)')" min-width="175">
        <template #default="{ row }">
          <strong :class="{ warning: row.risk?.queue?.status === 'unstable' }">{{ queueValue(row.risk?.queue, 'meanDelaySeconds') }}</strong>
          <div class="queue-note">{{ queueStatus(row.risk?.queue) }}</div>
        </template>
      </el-table-column>
      <el-table-column :label="tr('平均排队长度（项）', 'Mean queue (jobs)')" min-width="160">
        <template #default="{ row }">{{ queueValue(row.risk?.queue, 'meanQueueLength') }}</template>
      </el-table-column>
      <el-table-column :label="tr('负荷率 ρ', 'Load ρ')" min-width="110">
        <template #default="{ row }">
          <strong class="load-ratio" :style="{ color: loadColor(crossingLoadRatio(row.risk)) }">{{ percent(crossingLoadRatio(row.risk)) }}</strong>
          <div v-if="row.risk?.queue?.model === 'finite-uniform-fcfs' && row.risk.queue.loadRatio >= 1" class="queue-note warning">{{ tr("时段负荷超限", "Window overloaded") }}</div>
        </template>
      </el-table-column>
    </el-table>
    <p class="metric-color-note">{{ tr(
      '数值颜色由蓝到红：冲突概率按 0%–100% 着色；负荷率按 0%–75% 着色，75% 及以上为最深红色。',
      'Values run from blue to red: probability uses 0%–100%; load uses 0%–75%, with the deepest red at 75% and above.',
    ) }}</p>
    <p class="explanation">
      {{
        tr(
          "展开交叉点可设置各进路的占用时间。修改模型或时间后，请点击“保存并分析”重新计算。此处仅使用标记中记录的进路，不自动重算几何交点；仅有一条进路有作业时，跨进路冲突概率为 0。",
          "Expand a crossing to set route occupancy. After edits, Save and analyze again. Only recorded source routes are used; geometry is not regenerated. If only one route has passages, its cross-route conflict probability is zero.",
        )
      }}
    </p>
    <details class="method">
      <summary>
        {{ tr("模型公式与适用范围", "Model formulas and assumptions") }}
      </summary>
    <p class="explanation">
      {{
        tr(
          "概率口径：随机选取一次通过该交叉点的作业，与其他参与进路的至少一次占用重叠的概率。总体值按各进路通行次数加权，不是整个时段至少发生一次冲突的概率。",
          "Probability that a randomly selected passage overlaps at least one planned occupation on another participating route. The crossing total is weighted by passage count, not the chance of any conflict over the entire window.",
        )
      }}
    </p>
    <p class="explanation">
      {{ tr(
        "排队口径：每个交叉点作为一个独占资源，先到先服务。平均延误为等待进入交叉区的时间，不含占用时间；平均队长不含正在通过的作业。负荷率为所需占用时间与分析时段之比，可超过 100%。",
        "Each crossing is one exclusive FCFS resource. Mean delay is waiting before entering, excluding occupancy; mean queue length excludes the job in service. Load is required occupancy divided by the analysis window and may exceed 100%.",
      ) }}
    </p>
      <p>
        {{
          tr(
            "Nᵢ 为全部流程中进路 i 的通行次数，H 为统一分析时段，λᵢ = Nᵢ/H，mᵢ 为占用时间。对所选进路 i，Rᵢ = Σⱼ≠ᵢ λⱼ，Bᵢ = Σⱼ≠ᵢ λⱼmⱼ。",
            "Nᵢ counts all passages on route i, H is the shared window, λᵢ = Nᵢ/H and mᵢ is occupancy. For selected route i, Rᵢ = Σⱼ≠ᵢ λⱼ and Bᵢ = Σⱼ≠ᵢ λⱼmⱼ.",
          )
        }}
      </p>
      <p>
        <b>{{ tr("泊松到达 · 固定占用：", "Poisson · Fixed: ") }}</b> Pᵢ = 1 −
        exp(−Bᵢ − Rᵢmᵢ)
      </p>
      <p>
        <b>{{ tr("泊松到达 · 指数占用：", "Poisson · Exponential: ") }}</b> Pᵢ =
        1 − exp(−Bᵢ) / (1 + Rᵢmᵢ)
      </p>
      <p>
        <b>{{ tr("均匀到达 · 固定占用：", "Uniform · Fixed: ") }}</b> Pᵢ = 1 −
        ∫₀ᴴ ∏ⱼ≠ᵢ [1 − ℓᵢⱼ(u)/H]<sup>Nⱼ</sup> du/H；ℓᵢⱼ(u) = min(H, u + mᵢ) −
        max(0, u − mⱼ)
      </p>
      <p>
        {{
          tr(
            "泊松模型按稳定到达流计算，包含开始时刻之前尚未结束的占用；次数用于估计到达率，不固定实际发生次数。指数模型假设每次作业的占用时间独立，开始时刻无既有占用的概率为 exp(−Bᵢ)，再对本次占用期间的新到达积分。均匀模型固定次数，各次开始时刻独立分布在 [0,H]，允许作业在 H 之后结束，并通过数值积分处理时段边界。",
            "Poisson models assume stationary streams, including occupations still active from earlier arrivals; counts estimate rates. Exponential service times are independent for every job: exp(−Bᵢ) is the probability of no earlier active occupation, then future starts are averaged over this job's duration. Uniform arrivals fix counts, start independently in [0,H], may finish after H, and use numerical integration for window boundaries.",
          )
        }}
      </p>
      <p>
        {{
          tr(
            "冲突概率仍表示调度前、不同进路之间的计划占用重叠。排队指标单独按所有到达共用一个服务台计算，包含同一进路作业之间的等待，因此即使跨进路冲突概率为 0，也可能产生排队。各交叉点独立评估，不传播上游延误，不计优先权、额外追踪间隔或调度避让。下方作业内容冲突分析仍按完整步骤时间计算。",
            "Conflict probabilities describe planned cross-route overlap before dispatch. Queue metrics separately share one server across all arrivals, including same-route waiting, so queues can exist even with zero cross-route conflict probability. Crossings are evaluated independently without upstream delay propagation, priorities, extra headways or dispatch avoidance. Chain conflicts below still use full step timing.",
          )
        }}
      </p>
      <p>
        <b>{{ tr("泊松排队（M/G/1）：", "Poisson queue (M/G/1): ") }}</b>
        ρ = Σλᵢmᵢ；Wq = ΣλᵢE[Sᵢ²] / [2(1 − ρ)]；Lq = (Σλᵢ)Wq。
        {{ tr("固定占用 E[Sᵢ²] = mᵢ²，指数占用 E[Sᵢ²] = 2mᵢ²。仅 ρ < 1 时存在有限稳态；不将冲突概率直接乘以占用时间作为延误。", "Fixed occupancy uses E[Sᵢ²] = mᵢ²; exponential occupancy uses 2mᵢ². A finite steady state requires ρ < 1. Delay is not conflict probability multiplied by occupancy.") }}
      </p>
      <p>
        <b>{{ tr("均匀到达排队：", "Uniform-arrival queue: ") }}</b>
        {{ tr("固定各进路次数，在 [0,H] 独立均匀抽样到达时刻，从空队列开始按先到先服务仿真。Wq 为全部作业等待时间的平均值（含 H 之后的等待）；Lq = ∫₀ᴴ Q(t)dt/H，不包含正在服务的作业。有限时段不套用稳态 Little 公式。负荷超过 100% 仍显示本批次的有限估计。每点最多 2 万次通行、64–2048 次重复、200 万个事件，全图最多 800 万个事件；超限仅报告负荷并提示原因。", "With fixed route counts, independently sample arrivals in [0,H] and simulate FCFS from an empty queue. Wq averages all waits, including beyond H; Lq = ∫₀ᴴ Q(t)dt/H excludes service. Steady-state Little's law is not applied to this finite window. Loads above 100% still have finite batch estimates. Limits: 20,000 passages, 64–2048 runs and 2 million events per crossing; 8 million events per layout. Beyond these limits, only load is reported with a reason.") }}
      </p>
      <a href="https://web.mit.edu/urban_or_book/www/book/chapter4/4.7.html" target="_blank" rel="noopener noreferrer">{{ tr("排队公式参考：MIT M/G/1", "Queue formula reference: MIT M/G/1") }}</a>
      <br />
      <a
        href="https://ocw.mit.edu/courses/15-763j-manufacturing-system-and-supply-chain-design-spring-2005/22f4805864deb06eee85acc1601463ba_queueing_note.pdf"
        target="_blank"
        rel="noopener noreferrer"
        >{{
          tr(
            "泊松占用模型参考：MIT Queueing Theory（M/G/∞）",
            "Poisson occupancy reference: MIT Queueing Theory (M/G/∞)",
          )
        }}</a
      >
    </details>
  </section>
</template>

<style scoped>
.crossing-analysis {
  background: white;
  border: 1px solid var(--sy-border);
  border-radius: 6px;
  padding: 18px;
  margin: 16px 0;
}
.heading {
  display: flex;
  justify-content: space-between;
  align-items: center;
  flex-wrap: wrap;
  gap: 16px;
}
h3 {
  font-size: 16px;
  margin: 0 0 6px;
  color: #334155;
}
p {
  line-height: 1.7;
  font-size: 12px;
  color: var(--sy-text-muted);
}
.heading p {
  margin: 0;
}
.heading label {
  font-size: 12px;
  display: flex;
  flex-direction: column;
  gap: 6px;
  width: 310px;
}
.explanation {
  margin: 14px 0;
}
.route-details {
  padding: 0 20px 8px 44px;
  background: #f8fafc;
}
.time-input {
  display: flex;
  align-items: center;
  gap: 8px;
}
.time-input .el-input-number {
  width: 115px;
}
.automatic {
  color: #64748b;
  font-size: 11px;
}
.probability {
  font-size: 18px;
  font-variant-numeric: tabular-nums;
}
.warning {
  color: #a27839;
}
.queue-note {
  font-size: 11px;
  color: #758195;
  margin-top: 3px;
}
.queue-note.warning {
  color: #a27839;
}
.method {
  border-top: 1px solid var(--sy-border);
  padding-top: 12px;
  font-size: 12px;
}
.method summary {
  cursor: pointer;
  color: #526477;
}
.method a {
  color: #427bba;
}
</style>
