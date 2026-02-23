<template>
    <div class="sketch-scroll-container" ref="scrollContainerRef" @scroll.passive="handleHorizontalScroll">
        <svg id="slopesketch" :style="{ width: svgWidth + 'px', height: sketchHeight + 10 + 'px' }">
            <rect class="slopesketch-frame" :height="sketchHeight" :width="sketchWidth" :x="frameStartX" :y="0"></rect>
            <g v-for="sec in sectors">
                <line class="slope-line" :x1="getX(sec.startX || 0)"
                    :y1="getStartY(sec.gradient !== null ? sec.gradient : 0)" :x2="getX(sec.endX)"
                    :y2="getEndY(sec.gradient !== null ? sec.gradient : 0)" />
                <line class="slope-v" :x1="getX(sec.endX || 0)" :x2="getX(sec.endX || 0)" :y1="0" :y2="sketchHeight">
                </line>
                <text class="length-text" :x="getX((sec.startX + sec.endX) / 2)" :y="sketchHeight - 2">{{ sec.length
                }}</text>
                <text class="gradient-text" :x="getX((sec.startX + sec.endX) / 2)" :y="fontSize + 2">{{ sec.gradient
                    }}</text>
            </g>
            <g class="cursor">
                <line class="cursor-vline" :y1="0" :y2="sketchHeight" :x1="getX(cursorX)" :x2="getX(cursorX)"></line>
            </g>
        </svg>
    </div>
</template>

<script setup lang="ts">
import { computed, ref, watch, onMounted, onBeforeUnmount } from 'vue';
import type { SlopeLayout } from './humplayoutctrl';

class Sector {
    startPositionID: string | null = null;
    endPositionID: string | null = null;
    length: number = 0;
    gradient: number | null = null;
    startX: number = 0;
    endX: number = 0;
}

const props = defineProps<{
    slopeLayout?: SlopeLayout | null
    globalScaleX?: number
    globalMinX?: number
    globalLeftMargin?: number
    globalDomainSpan?: number
    globalCursorX?: number
    horizontalScrollLeft?: number
}>()

const emit = defineEmits<{
    updateGlobalCursorX: [value: number]
    'horizontal-scroll': [scrollLeft: number]
}>()

const scaleX = computed(() => props.globalScaleX ?? 3.5);
const marginLeft = ref(50);
const marginRight = ref(20);
const effectiveMarginLeft = computed(() => {
    if (Number.isFinite(Number(props.globalLeftMargin))) {
        return Number(props.globalLeftMargin);
    }
    return marginLeft.value;
});
const sketchHeight = ref(40);
const fontSize = ref(13);
const scrollContainerRef = ref<HTMLDivElement | null>(null);

const localCursorX = ref(0);
const cursorX = computed({
    get() {
        return props.globalCursorX !== undefined ? props.globalCursorX : localCursorX.value;
    },
    set(newVal: number) {
        if (props.globalCursorX === undefined) {
            localCursorX.value = newVal;
        } else {
            emit('updateGlobalCursorX', newVal);
        }
    }
});

function getX(posX: number): number {
    return (posX - xDomainMin.value) * scaleX.value + effectiveMarginLeft.value;
}

function getStartY(gradient: number): number {
    if (gradient > 0) return 0;
    if (gradient === 0) return sketchHeight.value / 2;
    return sketchHeight.value;
}

function getEndY(gradient: number): number {
    if (gradient > 0) return sketchHeight.value;
    if (gradient === 0) return sketchHeight.value / 2;
    return 0;
}

const slopeXStats = computed(() => {
    const positions = props.slopeLayout?.positionList || [];
    const xs = positions
        .map(pos => Number(pos.x))
        .filter(x => Number.isFinite(x));

    if (xs.length === 0) {
        return { minX: 0, spanX: 0 };
    }

    const minX = Math.min(...xs);
    const maxX = Math.max(...xs);
    return { minX, spanX: Math.max(0, maxX - minX) };
});

const xDomainMin = computed(() => {
    if (Number.isFinite(Number(props.globalMinX))) {
        return Number(props.globalMinX);
    }
    return slopeXStats.value.minX;
});

const xDomainSpan = computed(() => {
    const globalSpan = Number(props.globalDomainSpan);
    if (Number.isFinite(globalSpan) && globalSpan > 0) {
        return Math.max(globalSpan, slopeXStats.value.spanX);
    }
    return Math.max(0, slopeXStats.value.spanX);
});

const sketchWidth = computed(() => {
    const width = xDomainSpan.value * scaleX.value;
    return Math.max(300, width);
});

const frameStartX = computed(() => getX(xDomainMin.value));

const svgWidth = computed(() => {
    return effectiveMarginLeft.value + sketchWidth.value + marginRight.value;
});

const sectors = computed(() => {
    const sectors = [] as Sector[];
    const length = props.slopeLayout?.positionSegmentList?.length || 0;
    let cumulativeLength = 0;
    let startPositionID = props.slopeLayout?.positionSegmentList?.[0]?.startPositionID || null;

    for (let i = 0; i < length; i++) {
        const seg = props.slopeLayout?.positionSegmentList?.[i];
        const nextSeg = props.slopeLayout?.positionSegmentList?.[i + 1];
        if (seg && nextSeg && seg.gradient !== undefined && nextSeg.gradient !== undefined && seg.gradient === nextSeg.gradient) {
            cumulativeLength += seg.length;
        } else {
            cumulativeLength += seg ? seg.length : 0;
            const mergedSeg = seg ? ({
                startPositionID,
                endPositionID: seg.endPositionID,
                length: cumulativeLength,
                gradient: seg.gradient,
                startX: -1,
                endX: -1
            } as Sector) : null;
            if (mergedSeg) sectors.push(mergedSeg);
            cumulativeLength = 0;
            startPositionID = seg?.endPositionID || null;
        }
    }

    for (const sector of sectors) {
        sector.startX = props.slopeLayout?.positionList?.find(pos => pos.id === sector.startPositionID)?.x || 0;
        sector.endX = props.slopeLayout?.positionList?.find(pos => pos.id === sector.endPositionID)?.x || 0;
    }

    return sectors;
});

function addCursorXListener() {
    const svgElement = document.getElementById('slopesketch');
    if (!svgElement) return;
    svgElement.addEventListener('mousemove', (event) => {
        const rect = svgElement.getBoundingClientRect();
        const mouseX = event.clientX - rect.left;
        const posX = (mouseX - effectiveMarginLeft.value) / scaleX.value + xDomainMin.value;
        cursorX.value = posX;
    });
}

function setScrollLeft(scrollLeft: number) {
    if (!scrollContainerRef.value) return;
    scrollContainerRef.value.scrollLeft = scrollLeft;
}

function handleHorizontalScroll(event: Event) {
    const target = event.target as HTMLDivElement | null;
    if (!target) return;
    emit('horizontal-scroll', target.scrollLeft);
}

defineExpose({
    setScrollLeft
});

onMounted(() => {
    addCursorXListener();
});

onBeforeUnmount(() => {
    const svgElement = document.getElementById('slopesketch');
    if (svgElement) {
        svgElement.removeEventListener('mousemove', () => { });
    }
});

watch(() => props.slopeLayout, (newVal) => {
    if (newVal && newVal.positionSegmentList && newVal.positionList) {
        newVal.positionSegmentList.forEach(seg => {
            const startPos = newVal.positionList.find(p => p.id === seg.startPositionID);
            const endPos = newVal.positionList.find(p => p.id === seg.endPositionID);
            if (startPos && endPos && seg.length > 0) {
                seg.length = Math.round(Math.abs(endPos.x - startPos.x) * 1000) / 1000;
                const gradientValue = (startPos.height - endPos.height) / seg.length * 1000;
                seg.gradient = Math.round(gradientValue * 10) / 10;
            }
        });
    }
}, { deep: true });

watch(() => props.horizontalScrollLeft, (newVal) => {
    if (typeof newVal !== 'number') return;
    setScrollLeft(newVal);
});
</script>

<style scoped lang="css">
.sketch-scroll-container {
    width: 100%;
    overflow-x: auto;
    overflow-y: hidden;
    -ms-overflow-style: none;
    scrollbar-width: none;
}

.sketch-scroll-container::-webkit-scrollbar {
    width: 0;
    height: 0;
    display: none;
}

#slopesketch {
    min-width: 100%;
    display: block;
}

.cursor-vline {
    stroke: orange;
    stroke-width: 1px;
    pointer-events: none;
    opacity: 0.4;
}

.slope-line {
    stroke: black;
    stroke-width: 1.5px;
}

.slope-v {
    stroke: gray;
    stroke-width: 1px;
}

.slopesketch-frame {
    stroke-width: 1px;
    stroke: gray;
    fill: rgb(251, 251, 251);
}

.length-text {
    fill: black;
    font-size: v-bind('fontSize + "px"');
    text-anchor: middle;
    user-select: none;
}

.gradient-text {
    fill: black;
    font-size: v-bind('fontSize + "px"');
    text-anchor: middle;
    user-select: none;
}
</style>
