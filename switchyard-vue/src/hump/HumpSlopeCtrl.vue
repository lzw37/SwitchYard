<template>
    <div>
        <svg id="slope" :style="{ height: svgHeight + 'px' }">
            <defs>
                <linearGradient id="backgroundGradient" x1="0%" y1="0%" x2="0%" y2="100%">
                    <stop offset="0%" style="stop-color: #ECF4E8; stop-opacity: 0.8" />
                    <stop offset="100%" style="stop-color: #EFE9E3; stop-opacity: 1" />
                </linearGradient>
            </defs>
            <g class="background-fill">
                <polygon :points="polygonPoints" fill="url(#backgroundGradient)" />
            </g>
            <g class="axis">
                <line class="xaxis" :x1="marginLeft" :x2="marginLeft + sketchWidth" :y1="svgHeight - marginBottom"
                    :y2="svgHeight - marginBottom">
                </line>
                <line class="yaxis" :x1="marginLeft" :x2="marginLeft" :y1="marginTop" :y2="svgHeight - marginBottom">
                </line>
            </g>
            <g class="slopelines">
                <line v-for="seg in slopeLayout?.positionSegmentList || []" class="slope-line"
                    :x1="getX(getPositionX(seg.startPositionID))" :y1="getY(getPositionHeight(seg.startPositionID))"
                    :x2="getX(getPositionX(seg.endPositionID))" :y2="getY(getPositionHeight(seg.endPositionID))" />
            </g>
            <g class="points">
                <g v-for="pos in slopeLayout?.positionList || []">
                    <circle class="point-circle" :cx="getX(pos.x)" :cy="getY(pos.height)" r="4"
                        @mousedown="startDrag(pos, $event)"></circle>
                    <text class="point-height-text" :x="getX(pos.x)"
                        :y="(textPositions.get(pos.id)?.y ?? (getY(pos.height) - 10))">{{ pos.height }}m</text>
                    <line
                        v-if="Math.abs(getY(pos.height) - (textPositions.get(pos.id)?.y ?? (getY(pos.height) - 10))) >= 15"
                        class="point-line" :x1="getX(pos.x)"
                        :y1="textPositions.get(pos.id)?.barStartY ?? (getY(pos.height) - 10)" :x2="getX(pos.x)"
                        :y2="(textPositions.get(pos.id)?.barEndY ?? (getY(pos.height) - 10))"></line>
                </g>
            </g>
            <g class="guide-lines" v-if="draggingId">
                <line v-if="dragMode === 'horizontal'" class="guide-line horizontal" :x1="marginLeft"
                    :y1="getY(currentHeight)" :x2="marginLeft + sketchWidth" :y2="getY(currentHeight)" />
                <line v-if="dragMode === 'vertical'" class="guide-line vertical" :x1="getX(currentX)" :y1="marginTop"
                    :x2="getX(currentX)" :y2="svgHeight - marginBottom" />
            </g>
        </svg>
    </div>
</template>
<script setup lang="ts">
import type { SlopeLayout } from './humplayoutctrl';
import { ref, computed, onBeforeUnmount } from 'vue';

const props = defineProps<{
    slopeLayout?: SlopeLayout | null
}>()

const svgHeight = ref(400);

const scaleX = ref(3.5);
const marginLeft = ref(50);
const scaleY = ref(80);
const marginBottom = ref(20);
const marginTop = ref(20);
const draggingId = ref<string | null>(null);
const startMouseY = ref(0);
const startHeight = ref(0);
const startMouseX = ref(0);
const startX = ref(0);
const dragMode = ref<'vertical' | 'horizontal'>('vertical');
const currentX = ref(0);
const currentHeight = ref(0);

function getX(posX: number): number {
    return posX * 3.5 + 50;
}

function getY(height: number): number {
    return svgHeight.value - height * scaleY.value - marginBottom.value;
}

function getPositionX(positionID: string): number {
    const position = props.slopeLayout?.positionList?.find(pos => pos.id === positionID);
    return position?.x ?? 0;
}

function getPositionHeight(positionID: string): number {
    const position = props.slopeLayout?.positionList?.find(pos => pos.id === positionID);
    return position?.height ?? 0;
}

function startDrag(pos: { id: string; height: number; x: number }, event: MouseEvent) {
    event.preventDefault();
    draggingId.value = pos.id;
    currentX.value = pos.x;
    currentHeight.value = pos.height;
    if (event.altKey) {
        dragMode.value = 'horizontal';
        startMouseX.value = event.clientX;
        startX.value = pos.x;
    } else {
        dragMode.value = 'vertical';
        startMouseY.value = event.clientY;
        startHeight.value = pos.height;
    }
    window.addEventListener('mousemove', onMouseMove);
    window.addEventListener('mouseup', endDrag);
}

function onMouseMove(event: MouseEvent) {
    if (!draggingId.value || !props.slopeLayout?.positionList) return;
    const target = props.slopeLayout.positionList.find(p => p.id === draggingId.value);
    if (!target) return;
    if (dragMode.value === 'vertical') {
        const deltaY = event.clientY - startMouseY.value;
        const newHeight = startHeight.value - deltaY / scaleY.value;
        target.height = Math.round(Math.max(0, newHeight) * 1000) / 1000;
        currentHeight.value = target.height;
    } else if (dragMode.value === 'horizontal') {
        const deltaX = event.clientX - startMouseX.value;
        const newX = startX.value + deltaX / scaleX.value;
        target.x = Math.round(Math.max(0, newX) * 1000) / 1000;
        currentX.value = target.x;
    }
}

function endDrag() {
    if (!draggingId.value) return;
    draggingId.value = null;
    window.removeEventListener('mousemove', onMouseMove);
    window.removeEventListener('mouseup', endDrag);
}

const sketchWidth = computed(() => {
    if (!props.slopeLayout || !props.slopeLayout.positionList || props.slopeLayout.positionList.length === 0) {
        return 300;
    }
    const positions = props.slopeLayout.positionList;
    const minX = Math.min(...positions.map(pos => pos.x));
    const maxX = Math.max(...positions.map(pos => pos.x));
    return (maxX - minX) * scaleX.value;
});

const polygonPoints = computed(() => {
    if (!props.slopeLayout?.positionSegmentList || !props.slopeLayout.positionList) return '';

    // 收集 slopelines 的所有点，按 x 排序
    const points: { x: number; y: number }[] = [];
    props.slopeLayout.positionSegmentList.forEach(seg => {
        const startX = getPositionX(seg.startPositionID);
        const startY = getPositionHeight(seg.startPositionID);
        const endX = getPositionX(seg.endPositionID);
        const endY = getPositionHeight(seg.endPositionID);
        points.push({ x: startX, y: startY });
        points.push({ x: endX, y: endY });
    });
    // 去重并排序
    const uniquePoints = points.filter((point, index, self) =>
        index === self.findIndex(p => p.x === point.x && p.y === point.y)
    ).sort((a, b) => a.x - b.x);

    // 多边形点：yaxis 顶部 -> slopelines 点 -> xaxis 右端 -> xaxis 左端 -> yaxis 底部
    const polyPoints: string[] = [];
    polyPoints.push(`${marginLeft.value},${marginTop.value}`);
    uniquePoints.forEach(point => {
        polyPoints.push(`${getX(point.x)},${getY(point.y)}`);
    });
    polyPoints.push(`${marginLeft.value + sketchWidth.value},${svgHeight.value - marginBottom.value}`);
    polyPoints.push(`${marginLeft.value},${svgHeight.value - marginBottom.value}`);
    polyPoints.push(`${marginLeft.value},${svgHeight.value - marginTop.value}`);

    return polyPoints.join(' ');
});

// 计算文字的垂直偏移，避免重叠
const fontSize = ref(12);

// 计算标签位置，避免任意两个标签的矩形（宽/高）重叠
const textPositions = computed(() => {
    const map = new Map<string, { y: number; barStartY: number; barEndY: number }>();
    if (!props.slopeLayout?.positionList) return map;

    const placed: { id: string; x1: number; x2: number; y1: number; y2: number }[] = [];

    const charWidth = fontSize.value * 0.6; // 近似每字符宽度
    const textHeight = fontSize.value; // 近似文本高度

    for (const pos of props.slopeLayout.positionList) {
        const text = String(pos.height);
        const width = Math.max(10, text.length * charWidth);
        const cx = getX(pos.x);
        // initial top position (y increases downwards in SVG)
        let ty = (getY(pos.height) + svgHeight.value - marginBottom.value) / 2;
        var anchor = 0;
        var barStartY = getY(pos.height) + 5;
        var barEndY = svgHeight.value - marginBottom.value;

        if (ty > svgHeight.value - marginBottom.value - 10) {  // 太低不好看，调整到上方
            ty = getY(pos.height) - 10;
            anchor = 1;
        }

        // compute rect for text with anchor middle
        const getRect = (y: number) => ({
            x1: cx - width / 2,
            x2: cx + width / 2,
            y1: y - textHeight,
            y2: y
        });

        let rect = getRect(ty);
        let iter = 0;
        while (placed.some(p => !(p.x2 < rect.x1 || p.x1 > rect.x2 || p.y2 < rect.y1 || p.y1 > rect.y2))) {
            // 如果重叠，向上移动一个步长
            ty -= (textHeight + 4);
            rect = getRect(ty);
            if (++iter > 20) break;
        }

        placed.push({ id: pos.id, ...rect });

        var textY = Math.round(ty * 1000) / 1000;
        if (anchor === 1) {
            barStartY = getY(pos.height) - 5;
            barEndY = textY + 2;
        }

        map.set(pos.id, { y: textY, barStartY: barStartY, barEndY: barEndY });
    }

    return map;
});

onBeforeUnmount(() => {
    window.removeEventListener('mousemove', onMouseMove);
    window.removeEventListener('mouseup', endDrag);
});
</script>
<style scoped lang="css">
.te {
    color: blue;
}

#slope {
    width: 100%;
    height: 200px;
    /* border: 1px solid #ccc; */
    background-color: whitesmoke;
}

.xaxis,
.yaxis {
    stroke: #161E54;
    stroke-width: 2px;
    stroke-linecap: round;
}

.points .point-circle {
    fill: white;
    stroke: darkred;
    stroke-width: 2px;
}

.slope-line {
    stroke: #C2A68C;
    stroke-width: 3px;
}

.guide-line {
    stroke: gray;
    stroke-width: 1px;
    stroke-dasharray: 5, 5;
}

.point-height-text {
    font-size: 12px;
    fill: darkred;
    text-anchor: middle;
}

.point-line {
    stroke: darkred;
    stroke-width: 1px;
    opacity: 0.5;
}
</style>