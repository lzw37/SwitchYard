<template>
    <div class="report-render-host" aria-hidden="true" inert :style="{ width: `${width}px`, height: `${height}px` }">
        <StationPlanView v-if="station" :key="renderKey" ref="stationRef" :rows="station.rows" :trains="station.trains"
            :tracks="station.tracks" :start-minutes="station.startMinutes" :end-minutes="station.endMinutes" :editable="false" />
        <TrackOccupancyGantt v-if="occupancy" :key="renderKey" ref="occupancyRef" :rows="occupancy.rows" :ticks="occupancy.ticks"
            :timeline-width="occupancy.timelineWidth" :disabled="occupancy.disabled" :cell-axis-label="job.cellAxisLabel" :time-axis-label="job.timeAxisLabel" editable />
        <ProcessDiagram v-if="process" :key="renderKey" ref="processRef" :model="process" :catalog="processCatalog" :zoom="processZoom" />
    </div>
</template>

<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, ref, shallowRef } from 'vue'
import StationPlanView from '../components/StationPlanView.vue'
import TrackOccupancyGantt from '../components/TrackOccupancyGantt.vue'
import ProcessDiagram from '../components/ProcessDiagram.vue'
import { processReportFigureKey, type RenderedReportFigures } from './stationCapacityReport'
import type { ReportRenderJob } from './reportRenderTypes'
import type { ProcessTemplate } from '../operationProcess'

const props = defineProps<{ job: ReportRenderJob }>()
const station = shallowRef<ReportRenderJob['stationPlans'][number] | null>(null)
const occupancy = shallowRef<ReportRenderJob['occupancies'][number] | null>(null)
const process = shallowRef<ProcessTemplate | null>(null)
const stationRef = ref<InstanceType<typeof StationPlanView> | null>(null)
const occupancyRef = ref<InstanceType<typeof TrackOccupancyGantt> | null>(null)
const processRef = ref<InstanceType<typeof ProcessDiagram> | null>(null)
const renderKey = ref(0)
const width = ref(1000), height = ref(640)
let disposed = false
onBeforeUnmount(() => { disposed = true })
const isCurrentProcess = computed(() => !!process.value && !!props.job.currentProcess &&
    processReportFigureKey(process.value) === processReportFigureKey(props.job.currentProcess.model))
const processCatalog = computed(() => isCurrentProcess.value ? props.job.currentProcess!.catalog : props.job.catalog)
const processZoom = computed(() => isCurrentProcess.value ? props.job.currentProcess!.zoom : 1)

async function ready() {
    await nextTick()
    if (document.fonts) await document.fonts.ready
    if (disposed) throw new Error('Report rendering was cancelled')
}

async function render(): Promise<RenderedReportFigures> {
    const figures: RenderedReportFigures = { processes: {}, stationPlans: [], occupancies: [] }
    for (const item of props.job.processes) {
        const key = processReportFigureKey(item)
        const current = props.job.currentProcess
        if (current && key === processReportFigureKey(current.model) && current.figure) {
            figures.processes[key] = current.figure
            continue
        }
        if (!item.activities.length && !item.events.length) continue
        process.value = item
        renderKey.value++
        await ready()
        const figure = processRef.value?.exportReportFigure(item.name, true)
        if (!figure) throw new Error(`Cannot render process figure: ${item.name}`)
        figures.processes[key] = figure
    }
    process.value = null
    for (const item of props.job.stationPlans) {
        if (item.figure) { figures.stationPlans.push(item.figure); continue }
        if (!item.rows.length) continue
        width.value = item.view.width
        height.value = item.view.height
        station.value = item
        renderKey.value++
        await ready()
        await stationRef.value?.applyReportViewState(item.view)
        const figure = stationRef.value?.exportReportFigure(item.name, item.labels)
        if (!figure) throw new Error(`Cannot render station plan figure: ${item.name}`)
        figures.stationPlans.push(figure)
    }
    station.value = null
    for (const item of props.job.occupancies) {
        if (item.figure) { figures.occupancies.push(item.figure); continue }
        if (!item.rows.length) continue
        width.value = item.view.width
        height.value = item.view.height
        occupancy.value = item
        renderKey.value++
        await ready()
        await occupancyRef.value?.applyReportViewState(item.view)
        const figure = occupancyRef.value?.exportReportFigure(item.name, item.labels)
        if (!figure) throw new Error(`Cannot render resource occupancy figure: ${item.name}`)
        figures.occupancies.push(figure)
    }
    occupancy.value = null
    return figures
}
defineExpose({ render })
</script>

<style scoped>
.report-render-host {
    position: fixed;
    left: -100000px;
    top: 0;
    display: flex;
    flex-direction: column;
    pointer-events: none;
    background: #fff;
}
</style>
