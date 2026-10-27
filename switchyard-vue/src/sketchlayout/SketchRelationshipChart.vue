<script setup lang="ts">
import { computed, ref } from "vue";
import { useI18n } from "vue-i18n";
import { gradeColor } from "./geometry";
import type { Facility, FlowRelation } from "./types";

const props = defineProps<{
  facilities: Facility[];
  relations: FlowRelation[];
}>();
const { locale } = useI18n();
const tr = (zh: string, en: string) => (locale.value === "zh" ? zh : en);
const pairKey = (a: string, b: string) => JSON.stringify([a, b].sort());
const rowHeight = 52;
const half = rowHeight / 2;
const labelWidth = 160;
const padding = 8;
const chartWidth = computed(
  () => padding * 2 + labelWidth + Math.max(1, props.facilities.length) * half,
);
const chartHeight = computed(
  () => padding * 2 + props.facilities.length * rowHeight,
);
const relationMap = computed(
  () => new Map(props.relations.map((r) => [pairKey(r.fromId, r.toId), r])),
);
const cells = computed(() =>
  props.facilities.flatMap((first, i) =>
    props.facilities.slice(i + 1).map((second, offset) => {
      const j = i + offset + 1;
      const key = pairKey(first.id, second.id);
      const relation = relationMap.value.get(key);
      // The two diagonal families meet once for each unordered facility pair.
      const x = padding + labelWidth + (j - i) * half;
      const y = padding + (i + j + 1) * half;
      const forward = relation?.fromId === first.id ? relation.forward : relation?.reverse;
      const reverse = relation?.fromId === first.id ? relation.reverse : relation?.forward;
      return {
        key, first, second, i, j, relation, forward, reverse, x, y,
        points: `${x},${y - half} ${x + half},${y} ${x},${y + half} ${x - half},${y}`,
        color: relation ? gradeColor[relation.grade] || "#94a3b8" : "#94a3b8",
      };
    }),
  ),
);
type Cell = (typeof cells.value)[number];
const hoveredKey = ref<string>();
const focusedKey = ref<string>();
const activeCell = computed(
  () => cells.value.find((cell) => cell.key === (hoveredKey.value ?? focusedKey.value)),
);
function focusCell(key: string) {
  focusedKey.value = key;
  hoveredKey.value = undefined;
}
const formatNumber = (value: number) => value.toLocaleString(locale.value === "zh" ? "zh-CN" : "en-US");
const compactNumber = (value: number) => value < 1000
  ? formatNumber(value)
  : new Intl.NumberFormat(locale.value === "zh" ? "zh-CN" : "en-US", {
      notation: "compact", maximumFractionDigits: 1,
    }).format(value);
function description(cell: Cell) {
  const pair = `${cell.i + 1}. ${cell.first.name} ↔ ${cell.j + 1}. ${cell.second.name}`;
  if (!cell.relation) return `${pair} · ${tr("无分析数据", "No analysis data")}`;
  return `${pair} · ${cell.relation.grade} · ${tr("双向总量", "Total flow")} ${formatNumber(cell.relation.total)} · ${tr("相对强度", "Relative intensity")} ${(cell.relation.intensity * 100).toFixed(1)}% · ${tr("正向 / 反向", "Forward / reverse")} ${formatNumber(cell.forward!)} / ${formatNumber(cell.reverse!)}`;
}
const rowY = (index: number) => padding + (index + 0.5) * rowHeight;
const rowPoints = (index: number) => {
  const y = rowY(index), end = padding + labelWidth;
  return `${padding},${y - half} ${end},${y - half} ${end + half},${y} ${end},${y + half} ${padding},${y + half}`;
};
</script>

<template>
  <section class="relationship-chart" :aria-label="tr('Richard Muther 活动关系图', 'Richard Muther Activity Relationship Chart')">
    <h3>{{ tr("活动关系图", "Activity Relationship Chart") }}</h3>
    <p class="chart-subtitle">Richard Muther · Activity Relationship Chart</p>
    <div class="chart-legend">
      <span v-for="grade in ['A', 'E', 'I', 'O', 'U']" :key="grade">
        <i :style="{ background: gradeColor[grade] }" />{{ grade }}
      </span>
      <small>{{ tr("上格：等级 · 下格：双向作业量", "Upper: grade · Lower: total flow") }}</small>
    </div>
    <div v-if="facilities.length >= 2" class="chart-scroll">
      <svg :width="chartWidth" :height="chartHeight" :viewBox="`0 0 ${chartWidth} ${chartHeight}`"
        role="group" :aria-label="tr('设施活动关系三角矩阵', 'Triangular facility relationship matrix')">
        <g v-for="(facility, i) in facilities" :key="facility.id" class="facility-row"
          :class="{ active: activeCell?.i === i || activeCell?.j === i }">
          <title>{{ i + 1 }}. {{ facility.name }}</title>
          <polygon :points="rowPoints(i)" />
          <text class="facility-index" :x="padding + 12" :y="rowY(i) + 4">{{ i + 1 }}</text>
          <svg :x="padding + 34" :y="rowY(i) - 12" :width="labelWidth - 42" height="24">
            <text x="0" y="16">{{ facility.name }}</text>
          </svg>
        </g>
        <g v-for="cell in cells" :key="cell.key" class="relationship-cell"
          :class="{ active: activeCell?.key === cell.key }"
          :data-first-id="cell.first.id" :data-second-id="cell.second.id"
          :style="{ '--grade-color': cell.color }" tabindex="0" role="img"
          :aria-label="description(cell)"
          @mouseenter="hoveredKey = cell.key" @mouseleave="hoveredKey = undefined"
          @focus="focusCell(cell.key)" @blur="focusedKey = undefined">
          <title>{{ description(cell) }}</title>
          <polygon class="cell-background" :points="cell.points" />
          <path class="cell-divider" :d="`M ${cell.x - half} ${cell.y} H ${cell.x + half}`" />
          <text class="cell-grade" :x="cell.x" :y="cell.y - 5">{{ cell.relation?.grade || '—' }}</text>
          <text class="cell-flow" :x="cell.x" :y="cell.y + 14">{{ cell.relation ? compactNumber(cell.relation.total) : '—' }}</text>
        </g>
      </svg>
    </div>
    <el-empty v-else :image-size="48" :description="tr('至少两处设施才能绘制关系图。', 'Add at least two facilities to draw a relationship chart.')" />
    <div class="chart-detail" :class="{ 'has-value': activeCell }">
      {{ activeCell ? description(activeCell) : tr("悬停或聚焦菱形查看设施对与流量明细；本图仅反映作业关系。", "Hover or focus a diamond for facility-pair details. This chart represents flow relationships only.") }}
    </div>
  </section>
</template>

<style scoped>
.relationship-chart { min-width: 0; }
h3 { font-size: 14px; font-weight: 600; margin: 0 0 4px; }
.chart-subtitle { margin: 0 0 10px; font-size: 11px; color: var(--sy-text-muted); }
.chart-legend { display: flex; flex-wrap: wrap; align-items: center; gap: 7px 12px; font-size: 11px; margin-bottom: 8px; }
.chart-legend span { display: inline-flex; align-items: center; gap: 4px; }
.chart-legend i { width: 8px; height: 8px; border-radius: 2px; }
.chart-legend small { font-size: 11px; color: var(--sy-text-muted); }
.chart-scroll { max-height: 280px; overflow: auto; overscroll-behavior: contain; border: 1px solid var(--sy-border); border-radius: 4px; background: #fbfcfe; }
.chart-scroll > svg { display: block; margin: 8px auto; }
.facility-row polygon { fill: #f1f4f8; stroke: #cbd3de; stroke-width: 1; }
.facility-row text { fill: var(--sy-text); font-size: 12px; }
.facility-row .facility-index { fill: #778497; font-size: 11px; }
.facility-row.active polygon { fill: #e7effa; }
.facility-row.active text { font-weight: 600; }
.relationship-cell { outline: none; cursor: help; }
.cell-background { fill: var(--grade-color); fill-opacity: 0.15; stroke: #aeb9c8; stroke-width: 1; }
.cell-divider { stroke: #aeb9c8; stroke-width: 0.6; pointer-events: none; }
.relationship-cell text { text-anchor: middle; pointer-events: none; }
.cell-grade { fill: var(--grade-color); font-size: 14px; font-weight: 700; }
.cell-flow { fill: #41516a; font-size: 10px; }
.relationship-cell.active .cell-background { fill-opacity: 0.35; stroke: #334b6a; stroke-width: 2; }
.chart-detail { min-height: 34px; margin-top: 8px; font-size: 11px; line-height: 17px; color: var(--sy-text-muted); overflow-wrap: anywhere; }
.chart-detail.has-value { color: var(--sy-text); }
</style>
