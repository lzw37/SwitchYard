<template>
    <div style="width:100%;height:100%">
        <div class="flatlayout-toolbar">
            <label for="leftmargin-slider" style="width:auto;margin-right:10px">横向基线</label>
            <el-slider id="leftmargin-slider" size="small" v-model="leftMarginSliderValue" :min="-50" :max="500"
                :step="10" style="width:200px" />
            <label for="scalex-slider" style="width:auto;margin-right:10px">横向缩放</label>
            <el-slider id="scalex-slider" size="small" v-model="scaleX" :min="0.1" :max="5" :step="0.01"
                style="width:200px" />
            <label for="baseline-slider" style="width:auto;margin-right:10px">纵向基线</label>
            <el-slider id="baseline-slider" size="small" v-model="baseLineY" :min="0" :max="250" :step="1"
                style="width:200px" />
        </div>
        <svg id="hump-layout-ctrl" ref="svgRef" @mousedown="handleDragStart" @mousemove="handleDragMove"
            @mouseup="handleDragEnd" @mouseleave="handleDragEnd" @click.self="handleSvgClick">
            <g id="baseline-group">
                <g v-for="seg in props.flatLayout?.positionSegmentList" :key="seg.id"
                    :class="{ 'flatlayout-segment-selected': selectedElements.segments.includes(seg.id) }"
                    @mouseover="handlePositionSegmentMouseOver($event)"
                    @mouseout="handlePositionSegmentMouseOut($event)" @click.stop="toggleSegment(seg.id)">
                    <line class="flatlayout-baseline" :x1="getX(getPositionBySegmentID(seg.id)?.startPosition.x)"
                        :x2="getX(getPositionBySegmentID(seg.id)?.endPosition.x)"
                        :y1="baseLineY + getPositionSegmentDeltaY(seg)" :y2="baseLineY + getPositionSegmentDeltaY(seg)">
                    </line>
                    <line class="flatlayout-baselinecurve" v-if="seg.curveDegree > 0"
                        :x1="getX(getPositionBySegmentID(seg.id)?.startPosition.x)" :y1="baseLineY"
                        :x2="getX(getPositionBySegmentID(seg.id)?.startPosition.x)"
                        :y2="baseLineY + getPositionSegmentDeltaY(seg)" />
                    <line class="flatlayout-baselinecurve" v-if="seg.curveDegree > 0"
                        :x1="getX(getPositionBySegmentID(seg.id)?.endPosition.x)" :y1="baseLineY"
                        :x2="getX(getPositionBySegmentID(seg.id)?.endPosition.x)"
                        :y2="baseLineY + getPositionSegmentDeltaY(seg)" />
                    <text v-if="seg.curveDegree > 0"
                        :x="(getX(getPositionBySegmentID(seg.id)?.startPosition.x) + getX(getPositionBySegmentID(seg.id)?.endPosition.x)) / 2"
                        :y="curveDegreePositions.find(p => p.id === seg.id)?.y || (baseLineY + getPositionSegmentDeltaY(seg) + 15)"
                        class="flatlayout-curve-degree">{{
                            getDegreeStr(seg.curveDegree) }}</text>
                </g>
                <text v-for="ltp in lengthTextPositions" :key="ltp.id" :x="ltp.x" :y="ltp.y"
                    class="flatlayout-baseline-length">{{ getLengthById(ltp.id) }}</text>
                <g v-for="tp in textPositions" :key="tp.id"
                    :class="{ 'flatlayout-position-selected': selectedElements.positions.includes(tp.id) }"
                    @click.stop="togglePosition(tp.id)">
                    <text :x="tp.x" :y="tp.y" class="flatlayout-positionid">{{
                        tp.id }}</text>
                    <line :x1="tp.x" :x2="tp.x" :y1="tp.y" :y2="baseLineY" class="flatlayout-positionid-vline"></line>
                </g>
            </g>
            <g id="switch-group">
                <g v-for="sw in props.flatLayout?.switchList" :key="sw.bindingPositionID" class="flatlayout-switch"
                    :class="{ 'flatlayout-switch-selected': selectedElements.switches.includes(sw.bindingPositionID) }"
                    @mouseover="handleSwitchMouseOver($event)" @mouseout="handleSwitchMouseOut($event)"
                    @click.stop="toggleSwitch(sw.bindingPositionID)">
                    <line :x1="getX(getPositionByPositionID(sw.bindingPositionID))"
                        :x2="getX(getPositionByPositionID(sw.bindingPositionID))" :y1="baseLineY - 5"
                        :y2="baseLineY + 5">
                    </line>
                    <line :x1="getX(getPositionByPositionID(sw.bindingPositionID))" :y1="baseLineY"
                        :x2="getX(getPositionByPositionID(sw.bindingPositionID)) + getSwitchTailPosition(sw).deltaX"
                        :y2="baseLineY + getSwitchTailPosition(sw).deltaY">
                    </line>
                </g>
            </g>
            <g id="retarder-group">
                <g v-for="re in props.flatLayout?.retarderList" :key="re.bindingPositionSegmentID"
                    class="flatlayout-retarder"
                    :class="{ 'flatlayout-retarder-selected': selectedElements.retarders.includes(re.bindingPositionSegmentID) }"
                    @mouseover="handleRetarderMouseOver($event)" @mouseout="handleRetarderMouseOut($event)"
                    @click.stop="toggleRetarder(re.bindingPositionSegmentID)">
                    <rect :x="getX(getPositionBySegmentID(re.bindingPositionSegmentID)?.startPosition.x)"
                        :y="baseLineY - 10"
                        :width="(getPositionBySegmentID(re.bindingPositionSegmentID)?.endPosition.x - getPositionBySegmentID(re.bindingPositionSegmentID)?.startPosition.x) * scaleX"
                        height="20"></rect>
                    <text
                        :x="getX(getPositionBySegmentID(re.bindingPositionSegmentID)?.startPosition.x) + ((getPositionBySegmentID(re.bindingPositionSegmentID)?.endPosition.x - getPositionBySegmentID(re.bindingPositionSegmentID)?.startPosition.x) * scaleX) / 2"
                        :y="baseLineY + 25" class="flatlayout-retarder-numbers">{{ re.numberArray ?
                            re.numberArray.join('+') :
                            '' }}</text>
                </g>
            </g>
            <g id="interaction-group">
                <rect id="drag-rect" :x="dragRect.x" :y="dragRect.y" :width="dragRect.width" :height="dragRect.height">
                </rect>
            </g>
        </svg>
    </div>
</template>

<script lang="ts" setup>
import { ref, onMounted, onBeforeUnmount, watch, computed } from 'vue'
import { Switch, SwitchTypes, SwitchDirections, SwitchSides, PositionSegment, CurveDirections } from './humplayoutctrl'
import axios from 'axios'

const emit = defineEmits(['update:flatLayout'])
const props = defineProps<{ flatLayout?: any }>()

const svgRef = ref<SVGSVGElement | null>(null)

// x轴横向缩放比例
const scaleX = ref(3.5)

// 左侧边界距离
const leftMargin = ref(50)

const leftMarginSliderValue = computed({
    get: () => -leftMargin.value,
    set: (val: number) => {
        leftMargin.value = -val
    }
})

// 基线y坐标
const baseLineY = ref(100)

// 拖拽矩形状态
const dragRect = ref<{ x: number; y: number; width: number; height: number }>(
    { x: 0, y: 0, width: 0, height: 0 }
)
const isDragging = ref(false)
const suppressClickClear = ref(false)
let dragStartX = 0
let dragStartY = 0

// 选中状态
const selectedElements = ref({
    segments: [] as string[],
    positions: [] as string[],
    switches: [] as string[],
    retarders: [] as string[],
})

// 曲线表示的Y偏移量
const curveShiftY = ref(5);

// 道岔的偏移量
const switchShift = computed(() => {
    return { x: 50, y: 15 };
})

const textPositions = computed(() => {
    const positions = props.flatLayout?.positionList || [];
    const textWidth = 20; // 假设文本宽度为20px
    const lineHeight = 15; // 行高
    const sorted = positions.slice().sort((a: any, b: any) => getX(getPositionByPositionID(a.id)) - getX(getPositionByPositionID(b.id)));
    const result: { id: string; x: number; y: number }[] = [];
    let currentY = baseLineY.value - 50;
    let lastX = -Infinity;
    for (const p of sorted) {
        const x = getX(getPositionByPositionID(p.id));
        if (x - lastX < textWidth + 5) {
            currentY -= lineHeight; // 往上移动
        } else {
            currentY = baseLineY.value - 50;
        }
        result.push({ id: p.id, x: x, y: currentY });
        lastX = x;
    }
    return result;
})

const lengthTextPositions = computed(() => {
    const segments = props.flatLayout?.positionSegmentList || [];
    const textWidth = 30; // 假设文本宽度为30px
    const lineHeight = 15; // 行高
    const sorted = segments.slice().sort((a: any, b: any) => {
        const ax = (getX(getPositionBySegmentID(a.id)?.startPosition.x) + getX(getPositionBySegmentID(a.id)?.endPosition.x)) / 2;
        const bx = (getX(getPositionBySegmentID(b.id)?.startPosition.x) + getX(getPositionBySegmentID(b.id)?.endPosition.x)) / 2;
        return ax - bx;
    });
    const result: { id: string; x: number; y: number }[] = [];
    let currentY = baseLineY.value - 10;
    let lastX = -Infinity;
    for (const seg of sorted) {
        const startX = getX(getPositionBySegmentID(seg.id)?.startPosition.x);
        const endX = getX(getPositionBySegmentID(seg.id)?.endPosition.x);
        const x = (startX + endX) / 2;
        if (x - lastX < textWidth + 5) {
            currentY -= lineHeight; // 往上移动
        } else {
            currentY = baseLineY.value - 10;
        }
        result.push({ id: seg.id, x: x, y: currentY });
        lastX = x;
    }
    return result;
})

const curveDegreePositions = computed(() => {
    const segments = props.flatLayout?.positionSegmentList.filter((seg: any) => seg.curveDegree > 0) || [];
    const textWidth = 50; // 假设文本宽度为30px
    const lineHeight = 15; // 行高
    const sorted = segments.slice().sort((a: any, b: any) => {
        const ax = (getX(getPositionBySegmentID(a.id)?.startPosition.x) + getX(getPositionBySegmentID(a.id)?.endPosition.x)) / 2;
        const bx = (getX(getPositionBySegmentID(b.id)?.startPosition.x) + getX(getPositionBySegmentID(b.id)?.endPosition.x)) / 2;
        return ax - bx;
    });
    const result: { id: string; y: number }[] = [];
    let currentY = baseLineY.value + 15;
    let lastX = -Infinity;
    for (const seg of sorted) {
        const startX = getX(getPositionBySegmentID(seg.id)?.startPosition.x);
        const endX = getX(getPositionBySegmentID(seg.id)?.endPosition.x);
        const x = (startX + endX) / 2;
        if (x - lastX < textWidth + 5) {
            currentY += lineHeight; // 往下移动
        } else {
            currentY = baseLineY.value + 14 + getPositionSegmentDeltaY(seg);
        }
        result.push({ id: seg.id, y: currentY });
        lastX = x;
    }
    return result;
})

function getX(position: number): number {
    return (position * scaleX.value + leftMargin.value);
}

function getPositionByX(x: number): number {
    return (x - leftMargin.value) / scaleX.value;
}

function getPositionByPositionID(positionID: string): number {
    const pos = props.flatLayout?.positionList.find((p: { id: { toString: () => string; }; }) => p.id.toString() === positionID)
    return pos ? pos.x : 0;
}

function getPositionBySegmentID(positionSegmentID: string) {
    const segment = props.flatLayout?.positionSegmentList.find((seg: any) => seg.id === positionSegmentID);
    if (!segment) return null;
    const startPosition = props.flatLayout?.positionList.find((pos: any) => pos.id.toString() === segment.startPositionID);
    const endPosition = props.flatLayout?.positionList.find((pos: any) => pos.id.toString() === segment.endPositionID);
    return { startPosition, endPosition };
}

function getLengthById(id: string) {
    return props.flatLayout?.positionSegmentList.find((s: any) => s.id === id)?.length || '';
}

onMounted(() => {
    document.addEventListener('keydown', handleKeyDown)
})

onBeforeUnmount(() => {
    document.removeEventListener('keydown', handleKeyDown)
})

watch(
    () => props.flatLayout,
    newVal => {

    }
)

function handlePositionSegmentMouseOver(event: Event) {
    const target = event.currentTarget as SVGGElement;
    target.classList.add('flatlayout-baseline-active');
}

function handlePositionSegmentMouseOut(event: Event) {
    const target = event.currentTarget as SVGGElement;
    target.classList.remove('flatlayout-baseline-active');
}

function handleSwitchMouseOver(event: Event) {
    const target = event.currentTarget as SVGGElement;
    target.classList.add('flatlayout-switch-active');
}

function handleSwitchMouseOut(event: Event) {
    const target = event.currentTarget as SVGGElement;
    target.classList.remove('flatlayout-switch-active');
}

function handleRetarderMouseOver(event: Event) {
    const target = event.currentTarget as SVGGElement;
    target.classList.add('flatlayout-retarder-active');
}

function handleRetarderMouseOut(event: Event) {
    const target = event.currentTarget as SVGGElement;
    target.classList.remove('flatlayout-retarder-active');
}

function toggleSegment(id: string) {
    const list = selectedElements.value.segments
    if (list.includes(id)) {
        selectedElements.value.segments = list.filter(item => item !== id)
    } else {
        selectedElements.value.segments = [...list, id]
    }
}

function togglePosition(id: string) {
    const list = selectedElements.value.positions
    if (list.includes(id)) {
        selectedElements.value.positions = list.filter(item => item !== id)
    } else {
        selectedElements.value.positions = [...list, id]
    }
}

function toggleSwitch(id: string) {
    const list = selectedElements.value.switches
    if (list.includes(id)) {
        selectedElements.value.switches = list.filter(item => item !== id)
    } else {
        selectedElements.value.switches = [...list, id]
    }
}

function toggleRetarder(id: string) {
    const list = selectedElements.value.retarders
    if (list.includes(id)) {
        selectedElements.value.retarders = list.filter(item => item !== id)
    } else {
        selectedElements.value.retarders = [...list, id]
    }
}

function isSegmentSelected(id: string) {
    return selectedElements.value.segments.includes(id)
}

function isPositionSelected(id: string) {
    return selectedElements.value.positions.includes(id)
}

function isSwitchSelected(id: string) {
    return selectedElements.value.switches.includes(id)
}

function isRetarderSelected(id: string) {
    return selectedElements.value.retarders.includes(id)
}

function getSwitchTailPosition(sw: Switch) {
    if (sw.type === SwitchTypes.Single || sw.type === SwitchTypes.Slip) {
        if (sw.direction === SwitchDirections.Forward) {
            if (sw.side === SwitchSides.Left) {
                return { deltaX: -switchShift.value.x, deltaY: switchShift.value.y }
            } else {
                return { deltaX: -switchShift.value.x, deltaY: -switchShift.value.y }
            }
        } else if (sw.direction === SwitchDirections.Reverse) {
            if (sw.side === SwitchSides.Left) {
                return { deltaX: switchShift.value.x, deltaY: -switchShift.value.y }
            } else {
                return { deltaX: switchShift.value.x, deltaY: switchShift.value.y }
            }
        }
        return { deltaX: 0, deltaY: 0 }
    }
    else {
        return { deltaX: 0, deltaY: 0 }
    }
}

function getPositionSegmentDeltaY(ps: PositionSegment) {
    if (ps.curveDegree === 0) {
        return 0;
    }
    if (ps.curveDirection === CurveDirections.Left) {
        return -curveShiftY.value;
    }
    else if (ps.curveDirection === CurveDirections.Right) {
        return curveShiftY.value;
    }
    return 0;
}

function getDegreeStr(degreeDecimal: number): string {
    const absDegree = Math.abs(degreeDecimal);
    let degrees = Math.floor(absDegree);
    let minutes = Math.floor((absDegree - degrees) * 60);
    let seconds = Math.round(((absDegree - degrees) * 60 - minutes) * 60);
    if (seconds === 60) {
        seconds = 0;
        minutes += 1;
    }
    if (minutes === 60) {
        minutes = 0;
        degrees += 1;
    }
    const sign = degreeDecimal < 0 ? '-' : '';
    let str = `${sign}${degrees}°`;
    if (minutes > 0) {
        str += `${minutes}'`;
    }
    if (seconds > 0) {
        str += `${seconds}''`;
    }
    return str;
}

function clearSelections() {
    selectedElements.value = {
        segments: [],
        positions: [],
        switches: [],
        retarders: [],
    }
}

function handleKeyDown(event: KeyboardEvent) {
    if (event.key === 'Escape') {
        clearSelections()
    }
}

function handleDragStart(event: MouseEvent) {
    if (!svgRef.value) return
    const rect = svgRef.value.getBoundingClientRect()
    dragStartX = event.clientX - rect.left
    dragStartY = event.clientY - rect.top
    dragRect.value = { ...dragRect.value, x: dragStartX, y: dragStartY, width: 0, height: 0 }
    isDragging.value = true
}

function handleDragMove(event: MouseEvent) {
    if (!isDragging.value || !svgRef.value) return
    const rect = svgRef.value.getBoundingClientRect()
    const currentX = event.clientX - rect.left
    const currentY = event.clientY - rect.top
    const x = Math.min(dragStartX, currentX)
    const width = Math.abs(currentX - dragStartX)
    const y = Math.min(dragStartY, currentY)
    const height = Math.abs(currentY - dragStartY)
    dragRect.value = { ...dragRect.value, x, y, width, height }
}

function handleDragEnd(event: MouseEvent) {
    if (!isDragging.value) return
    handleDragMove(event)
    isDragging.value = false
    console.log('drag-rect', { positionStart: getPositionByX(dragRect.value.x), positionEnd: getPositionByX(dragRect.value.x + dragRect.value.width) })

    // 只有当拖拽矩形有面积时，才选中框住的对象
    if (dragRect.value.width > 0 || dragRect.value.height > 0) {
        selectObjectsInRect(dragRect.value)
        suppressClickClear.value = true // 防止随后触发的click事件清空选择
        requestAnimationFrame(() => {
            suppressClickClear.value = false
        })
    }
}

function handleSvgClick() {
    if (suppressClickClear.value) return
    clearSelections()
}

function isPositionSegmentInRect(seg: any, rect: { x: number; y: number; width: number; height: number }): boolean {
    const startX = getX(getPositionBySegmentID(seg.id)?.startPosition.x);
    const endX = getX(getPositionBySegmentID(seg.id)?.endPosition.x);
    const y = baseLineY.value + getPositionSegmentDeltaY(seg);
    const rectLeft = rect.x;
    const rectRight = rect.x + rect.width;
    const rectTop = rect.y;
    const rectBottom = rect.y + rect.height;
    const segLeft = Math.min(startX, endX);
    const segRight = Math.max(startX, endX);
    const xFullyContained = segLeft >= rectLeft && segRight <= rectRight;
    const yIn = y >= rectTop && y <= rectBottom;
    return xFullyContained && yIn;
}

function isSwitchInRect(sw: any, rect: { x: number; y: number; width: number; height: number }): boolean {
    const posX = getX(getPositionByPositionID(sw.bindingPositionID));
    const rectLeft = rect.x;
    const rectRight = rect.x + rect.width;
    const rectTop = rect.y;
    const rectBottom = rect.y + rect.height;
    const xIn = posX >= rectLeft && posX <= rectRight;
    const yOverlap = (baseLineY.value - 5 <= rectBottom && baseLineY.value + 5 >= rectTop);
    return xIn && yOverlap;
}

function isRetarderInRect(re: any, rect: { x: number; y: number; width: number; height: number }): boolean {
    const startX = getX(getPositionBySegmentID(re.bindingPositionSegmentID)?.startPosition.x);
    const width = (getPositionBySegmentID(re.bindingPositionSegmentID)?.endPosition.x - getPositionBySegmentID(re.bindingPositionSegmentID)?.startPosition.x) * scaleX.value;
    const y = baseLineY.value - 10;
    const height = 20;
    const rectLeft = rect.x;
    const rectRight = rect.x + rect.width;
    const rectTop = rect.y;
    const rectBottom = rect.y + rect.height;
    const reLeft = startX;
    const reRight = startX + width;
    const reTop = y;
    const reBottom = y + height;
    const xOverlap = reLeft < rectRight && reRight > rectLeft;
    const yOverlap = reTop < rectBottom && reBottom > rectTop;
    return xOverlap && yOverlap;
}

function selectObjectsInRect(rect: { x: number; y: number; width: number; height: number }) {
    const segments = props.flatLayout?.positionSegmentList.filter((seg: any) => isPositionSegmentInRect(seg, rect)).map((seg: any) => seg.id) || [];
    const switches = props.flatLayout?.switchList.filter((sw: any) => isSwitchInRect(sw, rect)).map((sw: any) => sw.bindingPositionID) || [];
    const retarders = props.flatLayout?.retarderList.filter((re: any) => isRetarderInRect(re, rect)).map((re: any) => re.bindingPositionSegmentID) || [];

    selectedElements.value.segments = segments;
    selectedElements.value.switches = switches;
    selectedElements.value.retarders = retarders;


    // selectedElements.value = {
    //     segments,
    //     positions: [], // 不选中positions，因为查询没提
    //     switches,
    //     retarders,
    // };
    console.log('Selected elements after drag:', selectedElements.value);
}

// expose methods to parent component
defineExpose({})
</script>

<style lang="css">
@import './humplayoutctrl.css';

.ctrl-elements {
    position: absolute;
    top: 10px;
    right: 10px;
    z-index: 10;
    background-color: rgba(157, 32, 32, 0.8);
    padding: 5px;
    border-radius: 5px;
    box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
}

.flatlayout-toolbar {
    display: flex
}
</style>