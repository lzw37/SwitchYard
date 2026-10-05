<template>
<svg ref="canvasRef" class="process-canvas" :class="{ 'process-is-busy': busy, 'process-is-connecting': connecting }" :width="canvasWidth * zoom" :height="canvasHeight * zoom" :viewBox="`0 0 ${canvasWidth} ${canvasHeight}`" role="group" :aria-label="ui('作业过程编排图，点击活动、事件、次序或锚以编辑属性', 'Operation process diagram. Select an activity, event, precedence or anchor to edit.')" @pointermove="emit('moveActivity', $event)" @pointerup="emit('endDrag')" @pointercancel="emit('endDrag')">
    <defs>
        <pattern :id="`${canvasID}-grid`" width="24" height="24" patternUnits="userSpaceOnUse"><circle cx="1" cy="1" r="1" fill="#dce4ef" /></pattern>
        <marker :id="`${canvasID}-arrow`" markerWidth="9" markerHeight="9" refX="8" refY="4.5" orient="auto" markerUnits="strokeWidth"><path d="M 0 0 L 9 4.5 L 0 9 z" fill="#8496af" /></marker>
        <marker :id="`${canvasID}-arrow-selected`" markerWidth="9" markerHeight="9" refX="8" refY="4.5" orient="auto" markerUnits="strokeWidth"><path d="M 0 0 L 9 4.5 L 0 9 z" fill="#3265db" /></marker>
    </defs>
    <rect :width="canvasWidth" :height="canvasHeight" fill="#fbfcfe" />
    <rect :width="canvasWidth" :height="canvasHeight" :fill="`url(#${canvasID}-grid)`" @click="emit('selectObject', 'template', model.id)" />


    <g v-for="edge in graphEdges" :key="edge.item.id" class="process-edge" :class="{ selected: isSelected('precedence', edge.item.id) }" role="button" :tabindex="connecting ? -1 : 0" :aria-disabled="connecting" :aria-label="`${ui('编辑次序', 'Edit precedence')} ${eventName(edge.item.leadingEvent)} → ${eventName(edge.item.followingEvent)}`" @click.stop="emit('selectObject', 'precedence', edge.item.id)" @keydown.enter="emit('selectObject', 'precedence', edge.item.id)">
        <path :d="edge.path" class="process-edge-hit" />
        <path :d="edge.path" class="process-edge-line" :marker-end="`url(#${canvasID}-arrow${isSelected('precedence', edge.item.id) ? '-selected' : ''})`" />
        <rect :x="edge.labelX - 27" :y="edge.labelY - 11" width="54" height="22" rx="11" class="process-edge-label-bg" />
        <text :x="edge.labelX" :y="edge.labelY + 4" text-anchor="middle" class="process-edge-label">{{ edge.item.interval }} {{ ui('分钟', 'min') }}</text>
    </g>

    <g v-for="activity in model.activities" :key="activity.id" class="process-activity" :class="{ selected: isSelected('activity', activity.id) }" :transform="`translate(${activity.x}, ${activity.y})`" role="button" :tabindex="connecting ? -1 : 0" :aria-disabled="connecting" :aria-label="`${ui('编辑活动', 'Edit activity')}: ${activity.name}`" @pointerdown="emit('startDrag', $event, activity)" @click.stop="emit('selectObject', 'activity', activity.id)" @keydown.enter="emit('selectObject', 'activity', activity.id)">
        <rect x="0" y="0" :width="CARD_WIDTH" :height="activityHeight(activity)" rx="14" class="process-card-shadow" />
        <rect x="0" y="0" :width="CARD_WIDTH" :height="activityHeight(activity)" rx="14" :fill="typeColors[activity.type].fill" :stroke="isSelected('activity', activity.id) ? '#3265db' : typeColors[activity.type].border" :stroke-width="isSelected('activity', activity.id) ? 2.5 : 1.3" />
        <rect x="15" y="15" width="5" height="17" rx="2.5" :fill="typeColors[activity.type].ink" />
        <text x="29" y="28" class="process-card-type" :fill="typeColors[activity.type].ink">{{ activityLabel(activity.type) }}</text>
        <text x="15" y="51" class="process-card-name">{{ shortText(activity.name, 12) }}</text>
        <text x="15" y="71" class="process-card-duration">{{ activity.minDuration }}–{{ activity.maxDuration }} {{ ui('分钟', 'min') }}</text>
        <text class="process-card-candidates"><tspan v-for="(line, index) in activityCardLabels.get(activity.id)?.lines" :key="index" x="15" :y="91 + index * CANDIDATE_LINE_HEIGHT">{{ line }}</tspan></text>
        <title>{{ activity.name }} · {{ activityLabel(activity.type) }} · {{ activity.minDuration }}–{{ activity.maxDuration }} {{ ui('分钟', 'min') }} · {{ activityCardLabels.get(activity.id)?.text }}</title>
    </g>

    <path v-if="precedencePreview" :d="precedencePreview.path" class="process-precedence-preview" :marker-end="`url(#${canvasID}-arrow-selected)`" :aria-label="ui('次序虚线预览', 'Precedence preview')" />

    <g v-for="{ event, x, y, top, bottom, key, activityID, side } in renderedEvents" :key="key" class="process-event" :class="{ 'process-precedence-candidate': isPrecedenceCandidate(activityID, side), 'process-end-source': connecting && side === 'end' && precedenceSource?.eventID === event.id }" role="button" :tabindex="connecting && !isPrecedenceCandidate(activityID, side) ? -1 : 0" :aria-disabled="connecting && !isPrecedenceCandidate(activityID, side)" :aria-label="`${isPrecedenceCandidate(activityID, side) ? (precedenceSource ? ui('选择后序开始事件', 'Select the following start event') : ui('选择前序结束事件', 'Select the preceding end event')) : ui('编辑事件', 'Edit event')}：${event.name}`" @click.stop="emit('activateEvent', event.id, activityID, side)" @keydown.enter.prevent="emit('activateEvent', event.id, activityID, side)" @keydown.space.prevent="emit('activateEvent', event.id, activityID, side)" @pointerenter="emit('previewPrecedence', event.id, activityID, side)" @pointerleave="emit('clearPrecedencePreview', activityID)" @focus="emit('previewPrecedence', event.id, activityID, side)" @blur="emit('clearPrecedencePreview', activityID)">
        <circle v-if="isPrecedenceCandidate(activityID, side)" :cx="x" :cy="y" r="15" class="process-precedence-halo" />
        <circle :cx="x" :cy="y" :r="isSelected('event', event.id) ? 10 : 7" :class="['process-event-dot', { selected: isSelected('event', event.id) }]" />
        <circle :cx="x" :cy="y" r="17" fill="transparent" />
        <text :x="x" :y="bottom + 19" text-anchor="middle" class="process-event-label">{{ shortText(event.name, 9) }}</text>
        <text v-if="event.time !== null" :x="x" :y="bottom + 36" text-anchor="middle" class="process-event-time">{{ formatTime(event.time) }}</text>
        <g v-if="event.selectedAnchor" class="process-anchor-badge" @click.stop="emit('selectObject', 'anchor', event.selectedAnchor)">
            <rect :x="x - 47" :y="top - 34" width="94" height="23" rx="6" :class="{ selected: isSelected('anchor', event.selectedAnchor) }" />
            <text :x="x" :y="top - 19" text-anchor="middle">{{ shortText(anchorName(event.selectedAnchor), 6) }}</text>
        </g>
        <title>{{ [event.name, formatTime(event.time), eventNodeSummary(event.id)].filter(Boolean).join(' · ') }}</title>
    </g>
</svg>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { activityLabels, deriveEventNodeLists, makeID, type ActivityType, type PrecedenceEndpoint, type ProcessActivity, type ProcessCatalog, type ProcessTemplate } from '../operationProcess'
import { CARD_WIDTH, CANDIDATE_LINE_HEIGHT, createProcessDiagram, processTypeColors as typeColors, type ProcessEventSide, type ProcessObjectKind, type ProcessSelection } from './processDiagram'
import { cloneSvgWithStyles, serializeReportSvg, stripReportInteractionMetadata } from '../report/svgSnapshot'
import { reportAnchorName, reportObjectName } from '../report/reportNames'
import type { ReportFigure } from '../report/wordReport'

const props = withDefaults(defineProps<{
    model: ProcessTemplate
    catalog: ProcessCatalog
    zoom?: number
    canvasID?: string
    selection?: ProcessSelection | null
    busy?: boolean
    connecting?: boolean
    precedenceSource?: PrecedenceEndpoint | null
    precedenceTarget?: PrecedenceEndpoint | null
    candidateActivities?: Set<string>
}>(), { zoom: 1, canvasID: () => makeID('process-canvas'), selection: null, busy: false, connecting: false, precedenceSource: null, precedenceTarget: null, candidateActivities: () => new Set<string>() })
const emit = defineEmits<{
    selectObject: [kind: ProcessObjectKind | 'template', id: string]
    startDrag: [event: PointerEvent, activity: ProcessActivity]
    moveActivity: [event: PointerEvent]
    endDrag: []
    activateEvent: [eventID: string, activityID: string | null, side: ProcessEventSide]
    previewPrecedence: [eventID: string, activityID: string | null, side: ProcessEventSide]
    clearPrecedencePreview: [activityID: string | null]
}>()
const { locale } = useI18n()
const canvasRef = ref<SVGSVGElement | null>(null)
const diagram = computed(() => createProcessDiagram(props.model, props.catalog, locale.value.startsWith('en')))
const canvasWidth = computed(() => diagram.value.canvasWidth)
const canvasHeight = computed(() => diagram.value.canvasHeight)
const activityCardLabels = computed(() => diagram.value.activityCardLabels)
const renderedEvents = computed(() => diagram.value.renderedEvents)
const graphEdges = computed(() => diagram.value.graphEdges)
const precedencePreview = computed(() => props.connecting ? diagram.value.precedencePreview(props.precedenceSource, props.precedenceTarget) : null)
const eventNodeLists = computed(() => deriveEventNodeLists(props.model, props.catalog))
function ui(zh: string, en: string) { return locale.value.startsWith('en') ? en : zh }
function activityLabel(type: ActivityType) { return ui(activityLabels[type], type === 'Locomotive' ? 'Locomotive' : type) }
function activityHeight(activity: ProcessActivity) { return diagram.value.activityHeight(activity) }
function shortText(text: string, length: number) { return text.length > length ? `${text.slice(0, length)}…` : text }
function eventName(id: string | null) { return props.model.events.find(item => item.id === id)?.name || ui('未选择事件', 'No event selected') }
function anchorName(id: string | null) { return props.model.anchors.find(item => item.id === id)?.name || ui('未选择锚', 'No anchor selected') }
function nodeName(id: string | null) { return props.catalog.nodes.find(item => item.id === id)?.name || (id ? ui('节点不可用', 'Node unavailable') : ui('地点待定', 'Location pending')) }
function formatTime(time: number | null) { return time === null ? '' : `T${time >= 0 ? '+' : ''}${time} ${ui('分钟', 'min')}` }
function eventNodeSummary(eventID: string) { return `${ui('备选节点', 'Candidate nodes')}: ${(eventNodeLists.value.get(eventID) || []).map(nodeName).join('、') || ui('暂无', 'None')}` }
function isSelected(kind: ProcessObjectKind, id: string) { return props.selection?.kind === kind && props.selection.id === id }
function isPrecedenceCandidate(activityID: string | null, side: ProcessEventSide) { return props.connecting && side === (props.precedenceSource ? 'start' : 'end') && !!activityID && props.candidateActivities.has(activityID) }
function exportReportFigure(caption: string, namesOnly = false): ReportFigure | null {
    if (!canvasRef.value) return null
    const svg = cloneSvgWithStyles(canvasRef.value) as SVGSVGElement
    if (namesOnly) {
        const catalog: ProcessCatalog = {
            nodes: props.catalog.nodes.map(item => ({ ...item, name: reportObjectName(item.name, item.id, ui('节点名称未提供', 'Unnamed node')) })),
            tracks: props.catalog.tracks.map(item => ({ ...item, name: reportObjectName(item.name, item.id, ui('股道名称未提供', 'Unnamed track')) })),
            routes: props.catalog.routes.map(item => ({ ...item, name: reportObjectName(item.name, item.id, ui('进路名称未提供', 'Unnamed route')) })),
        }
        const labels = createProcessDiagram(props.model, catalog, locale.value.startsWith('en')).activityCardLabels
        Array.from(svg.querySelectorAll('.process-activity')).forEach((group, index) => {
            const activity = props.model.activities[index]
            if (!activity) return
            const name = group.querySelector('.process-card-name')
            if (name) name.textContent = shortText(reportObjectName(activity.name, activity.id, ui('未命名活动', 'Unnamed activity')), 12)
            const spans = Array.from(group.querySelectorAll('.process-card-candidates tspan'))
            const lines = labels.get(activity.id)?.lines || []
            spans.forEach((span, line) => {
                // Reuse the original text slots so deleting IDs cannot move cards or connectors.
                span.textContent = line === spans.length - 1 && lines.length > spans.length
                    ? `${(lines[line] || '').slice(0, 12)}…` : lines[line] || ''
            })
        })
        Array.from(svg.querySelectorAll('.process-event')).forEach((group, index) => {
            const event = renderedEvents.value[index]?.event
            if (!event) return
            const name = group.querySelector('.process-event-label')
            if (name) name.textContent = shortText(reportObjectName(event.name, event.id, ui('未命名事件', 'Unnamed event')), 9)
            const anchor = group.querySelector('.process-anchor-badge text')
            if (anchor) anchor.textContent = shortText(reportAnchorName(props.model.anchors.find(item => item.id === event.selectedAnchor), catalog), 6)
        })
        caption = reportObjectName(caption, props.model.id, '未命名作业过程')
        stripReportInteractionMetadata(svg)
    }
    return serializeReportSvg(svg, caption, canvasWidth.value * props.zoom, canvasHeight.value * props.zoom)
}
defineExpose({
    exportReportFigure,
    setPointerCapture: (pointerID: number) => canvasRef.value?.setPointerCapture(pointerID),
    hasPointerCapture: (pointerID: number) => canvasRef.value?.hasPointerCapture(pointerID) || false,
    releasePointerCapture: (pointerID: number) => canvasRef.value?.releasePointerCapture(pointerID),
})
</script>

<style scoped>
.process-canvas { display: block; touch-action: none; background: #fbfcfe; }
.process-canvas-caption { fill: #96a3b5; font-size: 11px; pointer-events: none; }
.process-activity { cursor: grab; outline: none; }
.process-activity:active { cursor: grabbing; }
.process-activity:focus-visible > rect:last-of-type { stroke: #3265db; stroke-width: 3; }
.process-card-shadow { fill: #cfdae8; transform: translateY(3px); opacity: .22; }
.process-card-type { font-size: 11px; font-weight: 650; pointer-events: none; }
.process-card-name { fill: #34465e; font-size: 13px; font-weight: 600; pointer-events: none; }
.process-card-duration { fill: #8492a5; font-size: 11px; pointer-events: none; }
.process-card-candidates { fill: #526e88; font-size: 11px; white-space: pre; pointer-events: none; }
.process-event { cursor: pointer; outline: none; }
.process-event-dot { fill: #fff; stroke: #8096b2; stroke-width: 2; transition: r .12s; }
.process-event:hover .process-event-dot, .process-event:focus-visible .process-event-dot, .process-event-dot.selected { stroke: #3265db; fill: #e9efff; stroke-width: 2.5; }
.process-event-label { fill: #77889e; font-size: 10px; }
.process-event-time { fill: #a0aabc; font-size: 10px; }
.process-is-connecting .process-activity, .process-is-connecting .process-edge, .process-is-connecting .process-canvas-caption { opacity: .16; pointer-events: none; }
.process-is-connecting .process-event { opacity: .18; pointer-events: none; }
.process-is-connecting .process-event.process-end-source { opacity: 1; }
.process-is-connecting .process-event.process-end-source .process-event-dot { fill: #3265db; stroke: #3265db; stroke-width: 2.5; }
.process-is-connecting .process-event.process-precedence-candidate { opacity: 1; pointer-events: auto; cursor: crosshair; }
.process-is-connecting .process-precedence-candidate .process-event-dot { fill: #fff; stroke: #397bf0; stroke-width: 2.5; filter: drop-shadow(0 0 5px #397bf0aa); }
.process-is-connecting .process-precedence-candidate .process-event-label { fill: #3265db; font-weight: 650; }
.process-is-connecting .process-anchor-badge { opacity: .16; pointer-events: none; }
.process-precedence-halo { fill: #397bf029; stroke: #397bf066; stroke-width: 1; pointer-events: none; }
.process-precedence-preview { fill: none; stroke: #3265db; stroke-width: 2.3; stroke-dasharray: 8 6; stroke-linejoin: round; pointer-events: none; }
.process-anchor-badge rect { fill: #fff; stroke: #d6e1ef; }
.process-anchor-badge rect.selected { fill: #e9efff; stroke: #3265db; stroke-width: 2; }
.process-anchor-badge text { fill: #7b8ca4; font-size: 10px; }
.process-edge { cursor: pointer; outline: none; }
.process-edge-hit { fill: none; stroke: transparent; stroke-width: 20; }
.process-edge-line { fill: none; stroke: #8496af; stroke-width: 1.6; stroke-linejoin: round; pointer-events: none; }
.process-edge.selected .process-edge-line, .process-edge:hover .process-edge-line, .process-edge:focus-visible .process-edge-line { stroke: #3265db; stroke-width: 2.4; }
.process-edge-label-bg { fill: #fbfcfe; stroke: #e2e8f1; }
.process-edge-label { fill: #8290a5; font-size: 10px; }
.process-edge.selected .process-edge-label { fill: #3265db; }
.process-is-busy .process-activity { cursor: default; }
</style>
