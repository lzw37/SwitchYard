<template>
    <div>
        <svg id="slopesketch" :style="{ height: sketchHeight + 10 + 'px' }">
            <rect class="slopesketch-frame" :height="sketchHeight" :width="sketchWidth" :x="getX(0)" :y="0"></rect>
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
        </svg>
    </div>

</template>
<script setup lang="ts">
import { computed, ref, watch } from 'vue';
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
}>()

const scaleX = ref(3.5);
const marginLeft = ref(50);
const sketchHeight = ref(40)
const fontSize = ref(12);

function getX(posX: number): number {
    return posX * scaleX.value + marginLeft.value;
}

function getStartY(gradient: number): number {
    if (gradient > 0) {  // 下坡
        return 0;
    }
    else if (gradient === 0) {
        return sketchHeight.value / 2;
    }
    else {  // 上坡
        return sketchHeight.value;
    }
}

function getEndY(gradient: number): number {
    if (gradient > 0) {  // 下坡
        return sketchHeight.value;
    } else if (gradient === 0) {
        return sketchHeight.value / 2;
    } else {  // 上坡
        return 0;
    }
}

const sketchWidth = computed(() => {
    if (!props.slopeLayout || !props.slopeLayout.positionList || props.slopeLayout.positionList.length === 0) {
        return 300;
    }
    const positions = props.slopeLayout.positionList;
    const minX = Math.min(...positions.map(pos => pos.x));
    const maxX = Math.max(...positions.map(pos => pos.x));
    return (maxX - minX) * scaleX.value; // 添加一些边距
});

const sectors = computed(() => {
    const sectors = [] as Sector[];
    const length = props.slopeLayout?.positionSegmentList?.length || 0;
    var cumulativeLength = 0;
    var startPositionID = props.slopeLayout?.positionSegmentList[0]?.startPositionID || null;

    for (var i = 0; i < length; i++) {
        const seg = props.slopeLayout?.positionSegmentList[i]
        const nextSeg = props.slopeLayout?.positionSegmentList[i + 1]
        if (seg && nextSeg
            && seg.gradient !== undefined && nextSeg.gradient !== undefined
            && seg.gradient === nextSeg.gradient) {
            // 合并坡度相同的段
            cumulativeLength += seg.length;
        }
        else {
            cumulativeLength += seg ? seg.length : 0;
            const mergedSeg = seg ? ({
                startPositionID: startPositionID,
                endPositionID: seg?.endPositionID,
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
        if (sector == null) continue;
        sector.startX = props.slopeLayout?.positionList?.find(pos => pos.id === sector.startPositionID)?.x || 0;
        sector.endX = props.slopeLayout?.positionList?.find(pos => pos.id === sector.endPositionID)?.x || 0;
    }

    return sectors || []
}
);

// 监听 props.slopeLayout 的变化，强制更新 sectors
watch(() => props.slopeLayout, (newVal) => {
    if (newVal && newVal.positionSegmentList && newVal.positionList) {
        // 重新计算各个 positionSegment 的 gradient
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
</script>
<style scoped lang="css">
#slopesketch {
    width: 100%;
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
    font-size: v-bind(fontSize)px;
    text-anchor: middle;
}

.gradient-text {
    fill: black;
    font-size: v-bind(fontSize)px;
    text-anchor: middle;
}
</style>