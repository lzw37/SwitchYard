<template>
    <div style="width:100%;height:100%">
        <div v-if="props.isToolbarDisplay" class="flatlayout-toolbar">
            <div class="flatlayout-toolbar__group">
                <label for="leftmargin-slider">{{ t('humpLayoutCtrl.horizontalBaseline') }}</label>
                <el-slider id="leftmargin-slider" size="small" v-model="leftMarginSliderValue" :min="-50" :max="500"
                    :step="10" />
            </div>
            <div class="flatlayout-toolbar__group">
                <label for="scalex-slider">{{ t('humpLayoutCtrl.horizontalScale') }}</label>
                <el-slider id="scalex-slider" size="small" v-model="scaleX" :min="0.1" :max="5" :step="0.01" />
            </div>
            <div class="flatlayout-toolbar__group">
                <label for="baseline-slider">{{ t('humpLayoutCtrl.verticalBaseline') }}</label>
                <el-slider id="baseline-slider" size="small" v-model="baseLineY" :min="0" :max="250" :step="1" />
            </div>
        </div>
        <svg id="hump-layout-ctrl" ref="svgRef" @mousedown="handleDragStart"
            @mousemove="(event) => { handleDragMove(event); updateCursorX(event); }" @mouseup="handleDragEnd"
            @mouseleave="handleDragEnd" @click.self="handleSvgClick">
            <g id="baseline-group">
                <g v-for="seg in props.flatLayout?.positionSegmentList" :key="seg.id"
                    :class="{ 'flatlayout-segment-selected': selectedElements.segments.includes(seg.id) }"
                    @mouseover="handlePositionSegmentMouseOver($event)"
                    @mouseout="handlePositionSegmentMouseOut($event)" @click.stop="toggleSegment(seg.id)">
                    <line class="flatlayout-baseline" :x1="getX(getPositionBySegmentID(seg.id)?.startPosition?.x)"
                        :x2="getX(getPositionBySegmentID(seg.id)?.endPosition?.x)"
                        :y1="baseLineY + getPositionSegmentDeltaY(seg)" :y2="baseLineY + getPositionSegmentDeltaY(seg)">
                    </line>
                    <line class="flatlayout-baselinecurve" v-if="seg.curveDegree > 0"
                        :x1="getX(getPositionBySegmentID(seg.id)?.startPosition?.x)" :y1="baseLineY"
                        :x2="getX(getPositionBySegmentID(seg.id)?.startPosition?.x)"
                        :y2="baseLineY + getPositionSegmentDeltaY(seg)" />
                    <line class="flatlayout-baselinecurve" v-if="seg.curveDegree > 0"
                        :x1="getX(getPositionBySegmentID(seg.id)?.endPosition?.x)" :y1="baseLineY"
                        :x2="getX(getPositionBySegmentID(seg.id)?.endPosition?.x)"
                        :y2="baseLineY + getPositionSegmentDeltaY(seg)" />
                    <text v-if="seg.curveDegree > 0"
                        :x="(getX(getPositionBySegmentID(seg.id)?.startPosition?.x) + getX(getPositionBySegmentID(seg.id)?.endPosition.x)) / 2"
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
            <g id="cursor-group">
                <line class="cursor-vline" :y1="0" :x1="getX(cursorX)" :x2="getX(cursorX)" :y2="500"></line>
            </g>
        </svg>
    </div>
</template>

<script lang="ts" setup>
import { ref, onMounted, onBeforeUnmount, watch, computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { Switch, SwitchTypes, SwitchDirections, SwitchSides, PositionSegment, CurveDirections } from './humplayoutctrl'
import axios from '@/utils/axios'

const emit = defineEmits(['update:flatLayout', 'update:globalCursorX'])
const props = defineProps<{ flatLayout?: any, isToolbarDisplay?: boolean, isEditable?: boolean, globalScaleX?: number, globalCursorX?: number }>()

const { t } = useI18n()

const svgRef = ref<SVGSVGElement | null>(null)

const localScaleX = ref(3.5) // 本地的x轴横向缩放比例，当全局scaleX不设置时使用这个
// x轴横向缩放比例
const scaleX = computed({
    get() {
        return props.globalScaleX ?? localScaleX.value;
    },
    set(newVal) {
        localScaleX.value = newVal;
    }
});

const localCursorX = ref(0);
const cursorX = computed({
    get() {
        return props.globalCursorX !== undefined ? props.globalCursorX : localCursorX.value;
    },
    set(newVal) {
        if (props.globalCursorX === undefined) {
            localCursorX.value = newVal;
        } else {
            emit('update:globalCursorX', newVal);
        }
    }
});

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

/**
 * 将位置坐标转换为SVG的X坐标
 * @param position 位置坐标
 * @returns SVG中的X坐标
 */
function getX(position: number): number {
    return (position * scaleX.value + leftMargin.value);
}

/**
 * 将SVG的X坐标转换为位置坐标
 * @param x SVG中的X坐标
 * @returns 位置坐标
 */
function getPositionByX(x: number): number {
    return (x - leftMargin.value) / scaleX.value;
}

/**
 * 根据位置ID获取位置的X坐标
 * @param positionID 位置ID
 * @returns 位置的X坐标，如果未找到则返回0
 */
function getPositionByPositionID(positionID: string): number {
    const pos = props.flatLayout?.positionList.find((p: { id: { toString: () => string; }; }) => p.id.toString() === positionID)
    return pos ? pos.x : 0;
}

/**
 * 根据区段ID获取区段的起始和结束位置
 * @param positionSegmentID 区段ID
 * @returns 包含startPosition和endPosition的对象，如果未找到则返回null
 */
function getPositionBySegmentID(positionSegmentID: string) {
    const segment = props.flatLayout?.positionSegmentList.find((seg: any) => seg.id === positionSegmentID);
    if (!segment) return null;
    const startPosition = props.flatLayout?.positionList.find((pos: any) => pos.id.toString() === segment.startPositionID);
    const endPosition = props.flatLayout?.positionList.find((pos: any) => pos.id.toString() === segment.endPositionID);
    return { startPosition, endPosition };
}

/**
 * 根据ID获取区段的长度
 * @param id 区段ID
 * @returns 区段长度，如果未找到则返回空字符串
 */
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

/**
 * 处理位置区段鼠标悬停事件，添加激活样式
 * @param event 鼠标事件
 */
function handlePositionSegmentMouseOver(event: Event) {
    const target = event.currentTarget as SVGGElement;
    target.classList.add('flatlayout-baseline-active');
}

/**
 * 处理位置区段鼠标离开事件，移除激活样式
 * @param event 鼠标事件
 */
function handlePositionSegmentMouseOut(event: Event) {
    const target = event.currentTarget as SVGGElement;
    target.classList.remove('flatlayout-baseline-active');
}

/**
 * 处理道岔鼠标悬停事件，添加激活样式
 * @param event 鼠标事件
 */
function handleSwitchMouseOver(event: Event) {
    const target = event.currentTarget as SVGGElement;
    target.classList.add('flatlayout-switch-active');
}

/**
 * 处理道岔鼠标离开事件，移除激活样式
 * @param event 鼠标事件
 */
function handleSwitchMouseOut(event: Event) {
    const target = event.currentTarget as SVGGElement;
    target.classList.remove('flatlayout-switch-active');
}

/**
 * 处理减速器鼠标悬停事件，添加激活样式
 * @param event 鼠标事件
 */
function handleRetarderMouseOver(event: Event) {
    const target = event.currentTarget as SVGGElement;
    target.classList.add('flatlayout-retarder-active');
}

/**
 * 处理减速器鼠标离开事件，移除激活样式
 * @param event 鼠标事件
 */
function handleRetarderMouseOut(event: Event) {
    const target = event.currentTarget as SVGGElement;
    target.classList.remove('flatlayout-retarder-active');
}

/**
 * 切换区段的选中状态
 * @param id 区段ID
 */
function toggleSegment(id: string) {
    const list = selectedElements.value.segments
    if (list.includes(id)) {
        selectedElements.value.segments = list.filter(item => item !== id)
    } else {
        selectedElements.value.segments = [...list, id]
    }
}

/**
 * 切换位置的选中状态
 * @param id 位置ID
 */
function togglePosition(id: string) {
    const list = selectedElements.value.positions
    if (list.includes(id)) {
        selectedElements.value.positions = list.filter(item => item !== id)
    } else {
        selectedElements.value.positions = [...list, id]
    }
}

/**
 * 切换道岔的选中状态
 * @param id 道岔绑定位置ID
 */
function toggleSwitch(id: string) {
    const list = selectedElements.value.switches
    if (list.includes(id)) {
        selectedElements.value.switches = list.filter(item => item !== id)
    } else {
        selectedElements.value.switches = [...list, id]
    }
}

/**
 * 切换减速器的选中状态
 * @param id 减速器绑定区段ID
 */
function toggleRetarder(id: string) {
    const list = selectedElements.value.retarders
    if (list.includes(id)) {
        selectedElements.value.retarders = list.filter(item => item !== id)
    } else {
        selectedElements.value.retarders = [...list, id]
    }
}

/**
 * 检查区段是否被选中
 * @param id 区段ID
 * @returns 是否选中
 */
function isSegmentSelected(id: string) {
    return selectedElements.value.segments.includes(id)
}

/**
 * 检查位置是否被选中
 * @param id 位置ID
 * @returns 是否选中
 */
function isPositionSelected(id: string) {
    return selectedElements.value.positions.includes(id)
}

/**
 * 检查道岔是否被选中
 * @param id 道岔绑定位置ID
 * @returns 是否选中
 */
function isSwitchSelected(id: string) {
    return selectedElements.value.switches.includes(id)
}

/**
 * 检查减速器是否被选中
 * @param id 减速器绑定区段ID
 * @returns 是否选中
 */
function isRetarderSelected(id: string) {
    return selectedElements.value.retarders.includes(id)
}

/**
 * 获取道岔尾部的偏移位置
 * @param sw 道岔对象
 * @returns 包含deltaX和deltaY的对象，表示偏移量
 */
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

/**
 * 获取位置区段的Y轴偏移量（用于曲线表示）
 * @param ps 位置区段对象
 * @returns Y轴偏移量
 */
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

/**
 * 将十进制度数转换为度分秒字符串
 * @param degreeDecimal 十进制度数
 * @returns 度分秒字符串
 */
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

/**
 * 清空所有选中元素
 */
function clearSelections() {
    selectedElements.value = {
        segments: [],
        positions: [],
        switches: [],
        retarders: [],
    }
}

/**
 * 处理键盘按键事件，按Esc键清空选择
 * @param event 键盘事件
 */
function handleKeyDown(event: KeyboardEvent) {
    if (event.key === 'Escape') {
        clearSelections()
    }
}

/**
 * 处理鼠标拖拽开始事件
 * @param event 鼠标事件
 */
function handleDragStart(event: MouseEvent) {
    if (!svgRef.value) return
    const rect = svgRef.value.getBoundingClientRect()
    dragStartX = event.clientX - rect.left
    dragStartY = event.clientY - rect.top
    dragRect.value = { ...dragRect.value, x: dragStartX, y: dragStartY, width: 0, height: 0 }
    isDragging.value = true
}

/**
 * 处理鼠标拖拽移动事件
 * @param event 鼠标事件
 */
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

function updateCursorX(event: MouseEvent) {
    if (!svgRef.value) return
    const rect = svgRef.value.getBoundingClientRect()
    const mouseX = event.clientX - rect.left
    const posX = (mouseX - leftMargin.value) / scaleX.value
    cursorX.value = posX
}

/**
 * 处理鼠标拖拽结束事件
 * @param event 鼠标事件
 */
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

/**
 * 处理SVG点击事件，清空选择（如果未被抑制）
 */
function handleSvgClick() {
    if (suppressClickClear.value) return
    clearSelections()
}

/**
 * 检查位置区段是否在矩形内
 * @param seg 区段对象
 * @param rect 矩形对象
 * @returns 是否在矩形内
 */
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

/**
 * 检查道岔是否在矩形内
 * @param sw 道岔对象
 * @param rect 矩形对象
 * @returns 是否在矩形内
 */
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

/**
 * 检查减速器是否在矩形内
 * @param re 减速器对象
 * @param rect 矩形对象
 * @returns 是否在矩形内
 */
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

/**
 * 选择矩形内的对象
 * @param rect 矩形对象
 */
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

.cursor-vline {
    stroke: orange;
    stroke-width: 1px;
    pointer-events: none;
    opacity: 0.4;
}

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
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: 12px;
    padding: 14px 20px;
    margin-top: 5px;
    margin-left: 5px;
    margin-right: 5px;
    margin-bottom: 16px;
    border: 1px solid #dbe3f1;
    border-radius: 2px;
    background: linear-gradient(135deg, #f8fafc, #eef3ff);
    box-shadow: 0 5px 15px rgba(15, 23, 42, 0.08);
}

.flatlayout-toolbar__group {
    display: flex;
    align-items: center;
    gap: 10px;
    padding: 4px 4px;
    border-radius: 5px;
    border: 1px solid #e3eaf7;
    background: #ffffff;
    box-shadow: inset 0 1px 0 rgba(255, 255, 255, 0.9);
    transition: box-shadow 0.2s ease, border-color 0.2s ease;
}

.flatlayout-toolbar__group:hover {
    border-color: #c3d4f7;
    box-shadow: 0 4px 10px rgba(15, 23, 42, 0.12);
}

.flatlayout-toolbar__group label {
    font-size: 13px;
    font-weight: 600;
    color: #1f2a37;
    min-width: 70px;
    text-align: right;
    letter-spacing: 0.02em;
}

.flatlayout-toolbar__group .el-slider {
    flex: 1;
    min-width: 180px;
    margin-right: 4px;
}
</style>