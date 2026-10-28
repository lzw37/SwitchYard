<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { formatTractionDuration, type summarizeTractionResult } from './tractionCalculation'

const props = defineProps<{
    summary: ReturnType<typeof summarizeTractionResult> | null
    routeLengthM?: number
    pendingRouteLabel?: string
}>()
const { locale } = useI18n()
const ui = (zh: string, en: string) => locale.value.startsWith('zh') ? zh : en
const occupancyTitle = computed(() => {
    const summary = props.summary
    if (!summary) return undefined
    if (!summary.longestOccupancy) return ui('暂无可确定总占用时间的区段', 'No circuit has a known total occupancy time')
    const { cell, durationS } = summary.longestOccupancy
    return `${cell.label} · ${durationS.toFixed(1)} s` + (summary.unknownOccupancyCount
        ? ui('（仅统计时刻完整的区段）', ' (circuits with complete timing only)') : '')
})
</script>

<template>
    <div class="summary-strip" role="region" :aria-label="ui('运行摘要', 'Run summary')" tabindex="0">
        <div v-if="pendingRouteLabel" class="pending-result" role="status" :title="pendingRouteLabel"><span>{{ ui('待计算 · 上次结果', 'Pending · Last result') }} · {{ pendingRouteLabel }}</span></div>
        <div><span>{{ ui('制动距离', 'Braking distance') }}</span><strong>{{ summary?.brakingDistanceM.toFixed(1) ?? '—' }} <small>m</small></strong></div>
        <div><span>{{ ui('进路长度', 'Route length') }}</span><strong>{{ (summary?.routeLengthM ?? routeLengthM)?.toFixed(1) ?? '—' }} <small>m</small></strong></div>
        <div :title="ui('实际计算速度', 'Actual calculated speeds')"><span>{{ ui('入口速度/出口速度', 'Entry/exit speed') }}</span><strong>{{ summary?.entrySpeedKmh?.toFixed(1) ?? '—' }} / {{ summary?.exitSpeedKmh?.toFixed(1) ?? '—' }} <small>km/h</small></strong></div>
        <div><span>{{ ui('全程时间（含接近）', 'Journey time incl. approach') }}</span><strong>{{ formatTractionDuration(summary?.journeyTimeS) }}</strong></div>
        <div :title="occupancyTitle"><span>{{ ui('占用时间（最小间隔）', 'Occupancy time (minimum interval)') }}</span><strong class="cyan">{{ formatTractionDuration(summary?.longestOccupancy?.durationS) }}</strong></div>
    </div>
</template>

<style scoped>
.summary-strip { display:flex; flex:none; align-items:center; justify-content:space-between; gap:18px; height:46px; min-height:0; overflow:auto; overscroll-behavior:contain; border-bottom:1px solid #243046; margin-bottom:10px; padding:0 2px; white-space:nowrap; }
.summary-strip>div { display:flex; flex:none; align-items:baseline; gap:8px; }
.summary-strip span { color:#8e9db4; font-size:11px; }
.summary-strip strong { color:#edf4ff; font-size:17px; font-variant-numeric:tabular-nums; font-weight:550; }
.summary-strip small { font-size:10px; font-weight:400; color:#90a0b9; }
.summary-strip .cyan { color:#4fe3d0; }
.summary-strip .pending-result span { color:#f3c578; }
@media(max-width:1150px) { .summary-strip { gap:14px; }.summary-strip>div { gap:5px; } }
@media(max-width:800px) { .summary-strip { height:42px; }.summary-strip strong { font-size:15px; } }
</style>
