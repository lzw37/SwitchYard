<template>
    <div class="flatlayout-root">
        <div v-if="props.isToolbarDisplay" class="flatlayout-toolbar">
            <div class="flatlayout-toolbar__group">
                <label for="leftmargin-slider">{{ t('humpLayoutCtrl.horizontalBaseline') }}</label>
                <span class="flatlayout-toolbar__value">{{ leftMarginSliderValue }}</span>
                <el-slider id="leftmargin-slider" size="small" v-model="leftMarginSliderValue" :min="-50" :max="500"
                    :step="10" />
            </div>
            <div class="flatlayout-toolbar__group">
                <label for="scalex-slider">{{ t('humpLayoutCtrl.horizontalScale') }}</label>
                <span class="flatlayout-toolbar__value">{{ scaleXDisplay }}</span>
                <el-slider id="scalex-slider" size="small" v-model="scaleX" :min="0.1" :max="5" :step="0.01" />
            </div>
            <div class="flatlayout-toolbar__group">
                <label for="baseline-slider">{{ t('humpLayoutCtrl.verticalBaseline') }}</label>
                <span class="flatlayout-toolbar__value">{{ baseLineY }}</span>
                <el-slider id="baseline-slider" size="small" v-model="baseLineY" :min="0" :max="250" :step="1" />
            </div>
        </div>
        <div class="flatlayout-scroll-container" ref="scrollContainerRef" @scroll.passive="handleHorizontalScroll">
            <svg id="hump-layout-ctrl" ref="svgRef" :style="{ width: svgWidth + 'px' }" @mousedown="handleDragStart"
                @mousemove="handleMouseMove" @mouseup="handleDragEnd"
                @mouseleave="handleDragEnd" @click.self="handleSvgClick">
            <g id="baseline-group">
                <g v-for="seg in renderedSegments" :key="seg.id"
                    :class="{ 'flatlayout-segment-selected': selectedSegmentIdSet.has(seg.id) }"
                    @mouseover="handlePositionSegmentMouseOver($event)"
                    @mouseout="handlePositionSegmentMouseOut($event)" @click.stop="toggleSegment(seg.id)">
                    <line class="flatlayout-baseline" :x1="seg.startX" :x2="seg.endX" :y1="seg.y" :y2="seg.y">
                    </line>
                    <line class="flatlayout-baselinecurve" v-if="seg.hasCurve" :x1="seg.startX" :y1="baseLineY"
                        :x2="seg.startX" :y2="seg.y" />
                    <line class="flatlayout-baselinecurve" v-if="seg.hasCurve" :x1="seg.endX" :y1="baseLineY"
                        :x2="seg.endX" :y2="seg.y" />
                    <text v-if="seg.hasCurve" :x="seg.centerX" :y="seg.curveDegreeY"
                        class="flatlayout-curve-degree">{{ seg.curveDegreeLabel }}</text>
                </g>
                <text v-for="seg in renderedSegments" :key="`length-${seg.id}`" :x="seg.centerX" :y="seg.lengthY"
                    class="flatlayout-baseline-length">{{ seg.lengthText }}</text>
                <g v-for="tp in renderedPositionLabels" :key="tp.id"
                    :class="{ 'flatlayout-position-selected': selectedPositionIdSet.has(tp.id) }"
                    @click.stop="togglePosition(tp.id)">
                    <text :x="tp.x" :y="tp.y" class="flatlayout-positionid">{{
                        tp.id }}</text>
                    <line :x1="tp.x" :x2="tp.x" :y1="tp.y" :y2="baseLineY" class="flatlayout-positionid-vline"></line>
                </g>
            </g>
            <g id="switch-group">
                <g v-for="sw in switchRenderItems" :key="sw.key" class="flatlayout-switch"
                    :class="{ 'flatlayout-switch-selected': selectedSwitchIdSet.has(sw.bindingPositionID) }"
                    @mouseover="handleSwitchMouseOver($event)" @mouseout="handleSwitchMouseOut($event)"
                    @click.stop="toggleSwitch(sw.bindingPositionID)">
                    <line :x1="sw.x" :x2="sw.x" :y1="baseLineY - 5" :y2="baseLineY + 5">
                    </line>
                    <line :x1="sw.x" :y1="baseLineY" :x2="sw.tailX" :y2="sw.tailY">
                    </line>
                </g>
            </g>
            <g id="retarder-group">
                <g v-for="re in retarderRenderItems" :key="re.key"
                    class="flatlayout-retarder"
                    :class="{ 'flatlayout-retarder-selected': selectedRetarderIdSet.has(re.bindingPositionSegmentID) }"
                    @mouseover="handleRetarderMouseOver($event)" @mouseout="handleRetarderMouseOut($event)"
                    @click.stop="toggleRetarder(re.bindingPositionSegmentID)">
                    <rect :x="re.x" :y="re.y" :width="re.width" height="20"></rect>
                    <text :x="re.labelX" :y="re.labelY" class="flatlayout-retarder-numbers">{{ re.label }}</text>
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
    </div>
</template>

<script lang="ts" setup>
import { ref, onMounted, onBeforeUnmount, watch, computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { Switch, SwitchTypes, SwitchDirections, SwitchSides, PositionSegment, CurveDirections } from './humplayoutctrl'
import axios from '@/utils/axios'
import { ElMessageBox } from 'element-plus'

const emit = defineEmits(['update:flatLayout', 'update:globalCursorX', 'horizontal-scroll'])
const props = defineProps<{
    flatLayout?: any,
    isToolbarDisplay?: boolean,
    isEditable?: boolean,
    globalScaleX?: number,
    globalMinX?: number,
    globalLeftMargin?: number,
    globalDomainSpan?: number,
    globalCursorX?: number
}>()

const { t } = useI18n()

const svgRef = ref<SVGSVGElement | null>(null)
const scrollContainerRef = ref<HTMLDivElement | null>(null)

// 存储原始position列表的JSON字符串，用于检测ID变化
type PositionSnapshot = { id: string; x: number }

const originalPositionMap = ref<Map<string, PositionSnapshot>>(new Map())

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
const rightMargin = ref(20)
const effectiveLeftMargin = computed(() => {
    if (Number.isFinite(Number(props.globalLeftMargin))) {
        return Number(props.globalLeftMargin)
    }
    return leftMargin.value
})

const leftMarginSliderValue = computed({
    get: () => -leftMargin.value,
    set: (val: number) => {
        leftMargin.value = -val
    }
})

const scaleXDisplay = computed(() => Number(scaleX.value).toFixed(2))

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
let pendingCursorX: number | null = null
let cursorFrameId: number | null = null

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

function toFiniteNumber(value: unknown, fallback = 0): number {
    const num = Number(value)
    return Number.isFinite(num) ? num : fallback
}

function toId(value: unknown): string {
    return String(value ?? '')
}

type LayoutPosition = { id?: unknown; x?: unknown; [key: string]: any }
type LayoutSegment = PositionSegment & { id?: unknown; startPositionID?: unknown; endPositionID?: unknown; length?: unknown }
type RenderedSegmentBase = {
    id: string
    raw: LayoutSegment
    startX: number
    endX: number
    centerX: number
    y: number
    deltaY: number
    hasCurve: boolean
    curveDegreeLabel: string
    lengthText: string
}
type RenderedSegment = RenderedSegmentBase & { lengthY: number; curveDegreeY: number }

function createPositionSnapshotMap(positions: any[] | undefined): Map<string, PositionSnapshot> {
    const map = new Map<string, PositionSnapshot>()

    for (const position of positions ?? []) {
        const id = toId(position?.id)
        if (!id) continue

        map.set(id, {
            id,
            x: toFiniteNumber(position?.x),
        })
    }

    return map
}

const positionList = computed<LayoutPosition[]>(() => {
    const list = props.flatLayout?.positionList
    return Array.isArray(list) ? list : []
})

const positionSegmentList = computed<LayoutSegment[]>(() => {
    const list = props.flatLayout?.positionSegmentList
    return Array.isArray(list) ? list : []
})

const switchList = computed<Switch[]>(() => {
    const list = props.flatLayout?.switchList
    return Array.isArray(list) ? list : []
})

const retarderList = computed<any[]>(() => {
    const list = props.flatLayout?.retarderList
    return Array.isArray(list) ? list : []
})

const positionById = computed(() => {
    const map = new Map<string, LayoutPosition>()
    for (const position of positionList.value) {
        const id = toId(position?.id)
        if (id) {
            map.set(id, position)
        }
    }
    return map
})

const segmentById = computed(() => {
    const map = new Map<string, LayoutSegment>()
    for (const segment of positionSegmentList.value) {
        const id = toId(segment?.id)
        if (id) {
            map.set(id, segment)
        }
    }
    return map
})

const positionXStats = computed(() => {
    let hasValue = false
    let minX = 0
    let maxX = 0

    for (const position of positionList.value) {
        const x = toFiniteNumber(position?.x, NaN)
        if (!Number.isFinite(x)) continue

        if (!hasValue) {
            minX = x
            maxX = x
            hasValue = true
        } else {
            minX = Math.min(minX, x)
            maxX = Math.max(maxX, x)
        }
    }

    if (!hasValue) {
        return { minX: 0, maxX: 0, spanX: 0 }
    }

    return { minX, maxX, spanX: Math.max(0, maxX - minX) }
})

const xDomainMin = computed(() => {
    if (Number.isFinite(Number(props.globalMinX))) {
        return Number(props.globalMinX)
    }
    return positionXStats.value.minX
})

const xDomainSpan = computed(() => {
    const globalSpan = Number(props.globalDomainSpan)
    if (Number.isFinite(globalSpan) && globalSpan > 0) {
        return Math.max(globalSpan, positionXStats.value.spanX)
    }
    return Math.max(0, positionXStats.value.spanX)
})

const svgWidth = computed(() => {
    const minWidth = effectiveLeftMargin.value + rightMargin.value + 300
    const layoutWidth = effectiveLeftMargin.value + xDomainSpan.value * scaleX.value + rightMargin.value + Math.abs(switchShift.value.x)
    return Math.max(minWidth, layoutWidth)
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
        const segA = getPositionBySegmentID(a.id);
        const segB = getPositionBySegmentID(b.id);
        if (!segA?.startPosition || !segA?.endPosition) return 1;
        if (!segB?.startPosition || !segB?.endPosition) return -1;
        const ax = (getX(toFiniteNumber(segA.startPosition.x)) + getX(toFiniteNumber(segA.endPosition.x))) / 2;
        const bx = (getX(toFiniteNumber(segB.startPosition.x)) + getX(toFiniteNumber(segB.endPosition.x))) / 2;
        return ax - bx;
    });
    const result: { id: string; x: number; y: number }[] = [];
    let currentY = baseLineY.value - 10;
    let lastX = -Infinity;
    for (const seg of sorted) {
        const positions = getPositionBySegmentID(seg.id);
        if (!positions?.startPosition || !positions?.endPosition) continue;
        const startX = getX(toFiniteNumber(positions.startPosition.x));
        const endX = getX(toFiniteNumber(positions.endPosition.x));
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
        const segA = getPositionBySegmentID(a.id);
        const segB = getPositionBySegmentID(b.id);
        if (!segA?.startPosition || !segA?.endPosition) return 1;
        if (!segB?.startPosition || !segB?.endPosition) return -1;
        const ax = (getX(toFiniteNumber(segA.startPosition.x)) + getX(toFiniteNumber(segA.endPosition.x))) / 2;
        const bx = (getX(toFiniteNumber(segB.startPosition.x)) + getX(toFiniteNumber(segB.endPosition.x))) / 2;
        return ax - bx;
    });
    const result: { id: string; y: number }[] = [];
    let currentY = baseLineY.value + 15;
    let lastX = -Infinity;
    for (const seg of sorted) {
        const positions = getPositionBySegmentID(seg.id);
        if (!positions?.startPosition || !positions?.endPosition) continue;
        const startX = getX(toFiniteNumber(positions.startPosition.x));
        const endX = getX(toFiniteNumber(positions.endPosition.x));
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

// Precompute SVG-ready geometry so rendering stays linear in layout size.
const renderedPositionLabels = computed(() => {
    const textWidth = 20
    const lineHeight = 15
    const sorted = positionList.value
        .map((position) => ({
            id: toId(position?.id),
            x: getX(toFiniteNumber(position?.x)),
        }))
        .filter((position) => position.id)
        .sort((a, b) => a.x - b.x)

    const result: { id: string; x: number; y: number }[] = []
    let currentY = baseLineY.value - 50
    let lastX = -Infinity

    for (const position of sorted) {
        if (position.x - lastX < textWidth + 5) {
            currentY -= lineHeight
        } else {
            currentY = baseLineY.value - 50
        }

        result.push({ id: position.id, x: position.x, y: currentY })
        lastX = position.x
    }

    return result
})

const renderedSegmentBaseList = computed<RenderedSegmentBase[]>(() => {
    const items: RenderedSegmentBase[] = []
    const positions = positionById.value

    for (const segment of positionSegmentList.value) {
        const id = toId(segment?.id)
        if (!id) continue

        const startPosition = positions.get(toId(segment?.startPositionID))
        const endPosition = positions.get(toId(segment?.endPositionID))
        if (!startPosition || !endPosition) continue

        const startX = getX(toFiniteNumber(startPosition.x))
        const endX = getX(toFiniteNumber(endPosition.x))
        const curveDegree = toFiniteNumber(segment?.curveDegree, 0)
        const hasCurve = curveDegree > 0
        const deltaY = getPositionSegmentDeltaY(segment)

        items.push({
            id,
            raw: segment,
            startX,
            endX,
            centerX: (startX + endX) / 2,
            y: baseLineY.value + deltaY,
            deltaY,
            hasCurve,
            curveDegreeLabel: hasCurve ? getDegreeStr(curveDegree) : '',
            lengthText: String(segment?.length ?? ''),
        })
    }

    return items
})

const segmentLabelYMaps = computed(() => {
    const lineHeight = 15
    const sorted = renderedSegmentBaseList.value.slice().sort((a, b) => a.centerX - b.centerX)
    const lengthYById = new Map<string, number>()
    const curveYById = new Map<string, number>()

    let lengthY = baseLineY.value - 10
    let lastLengthX = -Infinity
    for (const segment of sorted) {
        if (segment.centerX - lastLengthX < 35) {
            lengthY -= lineHeight
        } else {
            lengthY = baseLineY.value - 10
        }

        lengthYById.set(segment.id, lengthY)
        lastLengthX = segment.centerX
    }

    let curveY = baseLineY.value + 15
    let lastCurveX = -Infinity
    for (const segment of sorted) {
        if (!segment.hasCurve) continue

        if (segment.centerX - lastCurveX < 55) {
            curveY += lineHeight
        } else {
            curveY = baseLineY.value + 14 + segment.deltaY
        }

        curveYById.set(segment.id, curveY)
        lastCurveX = segment.centerX
    }

    return { lengthYById, curveYById }
})

const renderedSegments = computed<RenderedSegment[]>(() => {
    const { lengthYById, curveYById } = segmentLabelYMaps.value

    return renderedSegmentBaseList.value.map((segment) => ({
        ...segment,
        lengthY: lengthYById.get(segment.id) ?? (baseLineY.value - 10),
        curveDegreeY: curveYById.get(segment.id) ?? (baseLineY.value + segment.deltaY + 15),
    }))
})

const renderedSegmentById = computed(() => {
    const map = new Map<string, RenderedSegment>()
    for (const segment of renderedSegments.value) {
        map.set(segment.id, segment)
    }
    return map
})

const switchRenderItems = computed(() => {
    return switchList.value.flatMap((sw, index) => {
        const bindingPositionID = toId(sw?.bindingPositionID)
        const position = positionById.value.get(bindingPositionID)
        if (!bindingPositionID || !position) return []

        const x = getX(toFiniteNumber(position.x))
        const tail = getSwitchTailPosition(sw)
        const id = toId(sw?.id)

        return [{
            key: `${bindingPositionID}-${id || index}`,
            bindingPositionID,
            x,
            tailX: x + tail.deltaX,
            tailY: baseLineY.value + tail.deltaY,
        }]
    })
})

const retarderRenderItems = computed(() => {
    return retarderList.value.flatMap((retarder, index) => {
        const bindingPositionSegmentID = toId(retarder?.bindingPositionSegmentID ?? retarder?.bindingPositionSegment?.id)
        const segment = renderedSegmentById.value.get(bindingPositionSegmentID)
        if (!bindingPositionSegmentID || !segment) return []

        const x = Math.min(segment.startX, segment.endX)
        const width = Math.abs(segment.endX - segment.startX)
        const label = Array.isArray(retarder?.numberArray) ? retarder.numberArray.join('+') : ''
        const id = toId(retarder?.id)

        return [{
            key: `${bindingPositionSegmentID}-${id || index}`,
            bindingPositionSegmentID,
            x,
            y: baseLineY.value - 10,
            width,
            labelX: segment.centerX,
            labelY: baseLineY.value + 25,
            label,
        }]
    })
})

const selectedSegmentIdSet = computed(() => new Set(selectedElements.value.segments))
const selectedPositionIdSet = computed(() => new Set(selectedElements.value.positions))
const selectedSwitchIdSet = computed(() => new Set(selectedElements.value.switches))
const selectedRetarderIdSet = computed(() => new Set(selectedElements.value.retarders))

/**
 * 将位置坐标转换为SVG的X坐标
 * @param position 位置坐标
 * @returns SVG中的X坐标
 */
function getX(position: number): number {
    const normalizedX = toFiniteNumber(position) - xDomainMin.value
    return normalizedX * scaleX.value + effectiveLeftMargin.value;
}

/**
 * 将SVG的X坐标转换为位置坐标
 * @param x SVG中的X坐标
 * @returns 位置坐标
 */
function getPositionByX(x: number): number {
    return (x - effectiveLeftMargin.value) / scaleX.value + xDomainMin.value;
}

/**
 * 根据位置ID获取位置的X坐标
 * @param positionID 位置ID
 * @returns 位置的X坐标，如果未找到则返回0
 */
function getPositionByPositionID(positionID: string): number {
    const pos = positionById.value.get(toId(positionID))
    return toFiniteNumber(pos?.x, 0);
}

/**
 * 根据区段ID获取区段的起始和结束位置
 * @param positionSegmentID 区段ID
 * @returns 包含startPosition和endPosition的对象，如果未找到则返回null
 */
function getPositionBySegmentID(positionSegmentID: string) {
    const segment = segmentById.value.get(toId(positionSegmentID));
    if (!segment) return null;
    const startPosition = positionById.value.get(toId(segment.startPositionID));
    const endPosition = positionById.value.get(toId(segment.endPositionID));
    return { startPosition, endPosition };
}

/**
 * 根据ID获取区段的长度
 * @param id 区段ID
 * @returns 区段长度，如果未找到则返回空字符串
 */
function getLengthById(id: string) {
    return segmentById.value.get(toId(id))?.length || '';
}

/**
 * 检查position ID是否发生变化，如有变化则弹出确认对话框
 * @param positionId 修改的position的ID
 */
async function checkPositionIdChange(positionId: string) {
    const newPositionList = props.flatLayout?.positionList
    if (!newPositionList) return

    // 如果originalPositionMap为空，初始化它
    if (originalPositionMap.value.size === 0) {
        originalPositionMap.value = createPositionSnapshotMap(newPositionList)
        return
    }

    const newPos = newPositionList.find((p: any) => p.id.toString() === positionId)
    if (!newPos) return

    // 检测ID变化
    const newId = newPos.id.toString()
    let oldId: string | null = null

    // 查找是否有position的其他属性匹配但ID不同
    for (const [origId, oldPos] of originalPositionMap.value.entries()) {
        // 如果x坐标相同但ID不同，认为是ID被修改了
        if (oldPos.x === newPos.x && origId !== newId) {
            // 检查新ID是否在旧列表中存在
            const newIdExistedBefore = originalPositionMap.value.has(newId)
            if (!newIdExistedBefore) {
                oldId = origId
                break
            }
        }
    }

    // 如果检测到ID变化，弹出确认对话框
    if (oldId) {
        try {
            const affectedSegments: string[] = []
            // 查找所有受影响的区段
            const segments = props.flatLayout?.positionSegmentList?.filter((seg: any) =>
                seg.startPositionID === oldId || seg.endPositionID === oldId
            ) || []
            affectedSegments.push(...segments.map((s: any) => s.id))

            const message = affectedSegments.length > 0
                ? `检测到Position ID变化，这将影响 ${affectedSegments.length} 个区段。是否删除受影响的区段？`
                : '检测到Position ID变化，是否继续？'

            await ElMessageBox.confirm(
                message,
                '警告',
                {
                    confirmButtonText: '确定',
                    cancelButtonText: '取消',
                    type: 'warning',
                }
            )

            // 用户确定，删除受影响的区段
            if (affectedSegments.length > 0 && props.flatLayout?.positionSegmentList) {
                const updatedLayout = {
                    ...props.flatLayout,
                    positionSegmentList: props.flatLayout.positionSegmentList.filter(
                        (seg: any) => !affectedSegments.includes(seg.id)
                    )
                }
                emit('update:flatLayout', updatedLayout)
            }

            // 更新原始position映射
            originalPositionMap.value = createPositionSnapshotMap(newPositionList)
        } catch (error) {
            // 用户取消，恢复原来的ID
            const restoredPositionList = newPositionList.map((pos: any) => {
                if (pos.id.toString() === newId && pos.x === newPos.x) {
                    return { ...pos, id: oldId }
                }
                return pos
            })

            const updatedLayout = {
                ...props.flatLayout,
                positionList: restoredPositionList
            }
            emit('update:flatLayout', updatedLayout)
        }
    } else {
        // 没有ID变化，更新原始position映射中的该position
        originalPositionMap.value.set(newId, {
            id: newId,
            x: toFiniteNumber(newPos.x),
        })
    }
}

/**
 * 初始化原始position映射
 */
function initializePositionMap() {
    if (props.flatLayout?.positionList) {
        originalPositionMap.value = createPositionSnapshotMap(props.flatLayout.positionList)
    } else {
        originalPositionMap.value = new Map()
    }
}

onMounted(() => {
    document.addEventListener('keydown', handleKeyDown)
    initializePositionMap()
})

onBeforeUnmount(() => {
    document.removeEventListener('keydown', handleKeyDown)
    if (cursorFrameId !== null) {
        cancelAnimationFrame(cursorFrameId)
    }
})

// 监听flatLayout变化，初始化position映射
watch(
    () => props.flatLayout,
    () => {
        initializePositionMap()
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
    if (toFiniteNumber(ps.curveDegree, 0) === 0) {
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

function getSvgLocalPoint(event: MouseEvent) {
    if (!svgRef.value) return null
    const rect = svgRef.value.getBoundingClientRect()
    return {
        x: event.clientX - rect.left,
        y: event.clientY - rect.top,
    }
}

function updateDragRect(currentX: number, currentY: number) {
    if (!isDragging.value) return

    const x = Math.min(dragStartX, currentX)
    const width = Math.abs(currentX - dragStartX)
    const y = Math.min(dragStartY, currentY)
    const height = Math.abs(currentY - dragStartY)
    dragRect.value = { ...dragRect.value, x, y, width, height }
}

function scheduleCursorXUpdate(mouseX: number) {
    pendingCursorX = (mouseX - effectiveLeftMargin.value) / scaleX.value + xDomainMin.value

    if (cursorFrameId !== null) return

    cursorFrameId = requestAnimationFrame(() => {
        if (pendingCursorX !== null) {
            cursorX.value = pendingCursorX
            pendingCursorX = null
        }
        cursorFrameId = null
    })
}

function handleMouseMove(event: MouseEvent) {
    const point = getSvgLocalPoint(event)
    if (!point) return

    updateDragRect(point.x, point.y)
    scheduleCursorXUpdate(point.x)
}

/**
 * 处理鼠标拖拽开始事件
 * @param event 鼠标事件
 */
function handleDragStart(event: MouseEvent) {
    const point = getSvgLocalPoint(event)
    if (!point) return

    dragStartX = point.x
    dragStartY = point.y
    dragRect.value = { ...dragRect.value, x: dragStartX, y: dragStartY, width: 0, height: 0 }
    isDragging.value = true
}

/**
 * 处理鼠标拖拽移动事件
 * @param event 鼠标事件
 */
function handleDragMove(event: MouseEvent) {
    const point = getSvgLocalPoint(event)
    if (!point) return
    updateDragRect(point.x, point.y)
}

function updateCursorX(event: MouseEvent) {
    const point = getSvgLocalPoint(event)
    if (!point) return
    cursorX.value = (point.x - effectiveLeftMargin.value) / scaleX.value + xDomainMin.value
}

/**
 * 处理鼠标拖拽结束事件
 * @param event 鼠标事件
 */
function handleDragEnd(event: MouseEvent) {
    if (!isDragging.value) return
    handleDragMove(event)
    isDragging.value = false

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
    const positions = getPositionBySegmentID(seg.id);
    if (!positions?.startPosition || !positions?.endPosition) return false;

    const startX = getX(toFiniteNumber(positions.startPosition.x));
    const endX = getX(toFiniteNumber(positions.endPosition.x));
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
    const positions = getPositionBySegmentID(re.bindingPositionSegmentID);
    if (!positions?.startPosition || !positions?.endPosition) return false;

    const startX = getX(toFiniteNumber(positions.startPosition.x));
    const width = (toFiniteNumber(positions.endPosition.x) - toFiniteNumber(positions.startPosition.x)) * scaleX.value;
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
    const rectLeft = rect.x;
    const rectRight = rect.x + rect.width;
    const rectTop = rect.y;
    const rectBottom = rect.y + rect.height;

    const segments = renderedSegments.value
        .filter((seg) => {
            const segLeft = Math.min(seg.startX, seg.endX);
            const segRight = Math.max(seg.startX, seg.endX);
            const xFullyContained = segLeft >= rectLeft && segRight <= rectRight;
            const yIn = seg.y >= rectTop && seg.y <= rectBottom;
            return xFullyContained && yIn;
        })
        .map((seg) => seg.id);

    const switches = switchRenderItems.value
        .filter((sw) => {
            const xIn = sw.x >= rectLeft && sw.x <= rectRight;
            const yOverlap = (baseLineY.value - 5 <= rectBottom && baseLineY.value + 5 >= rectTop);
            return xIn && yOverlap;
        })
        .map((sw) => sw.bindingPositionID);

    const retarders = retarderRenderItems.value
        .filter((re) => {
            const reLeft = re.x;
            const reRight = re.x + re.width;
            const reTop = re.y;
            const reBottom = re.y + 20;
            const xOverlap = reLeft < rectRight && reRight > rectLeft;
            const yOverlap = reTop < rectBottom && reBottom > rectTop;
            return xOverlap && yOverlap;
        })
        .map((re) => re.bindingPositionSegmentID);

    selectedElements.value.segments = segments;
    selectedElements.value.switches = switches;
    selectedElements.value.retarders = retarders;


    // selectedElements.value = {
    //     segments,
    //     positions: [], // 不选中positions，因为查询没提
    //     switches,
    //     retarders,
    // };
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

// expose methods to parent component
defineExpose({
    checkPositionIdChange,
    setScrollLeft
})
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

.flatlayout-root {
    width: 100%;
    height: 100%;
    display: flex;
    flex-direction: column;
    min-height: 0;
}

.flatlayout-scroll-container {
    width: 100%;
    flex: 1 1 auto;
    overflow-x: auto;
    overflow-y: hidden;
    -ms-overflow-style: none;
    scrollbar-width: none;
}

.flatlayout-scroll-container::-webkit-scrollbar {
    width: 0;
    height: 0;
    display: none;
}

.flatlayout-toolbar {
    flex: 0 0 auto;
    display: flex;
    flex-direction: row;
    flex-wrap: wrap;
    gap: 4px;
    padding: 6px 10px;
    margin: 4px 5px 6px;
    border: 1px solid #dbe3f1;
    border-radius: 2px;
    background: linear-gradient(135deg, #f8fafc, #eef3ff);
    box-shadow: 0 3px 8px rgba(15, 23, 42, 0.06);
}

.flatlayout-toolbar__group {
    flex: 1 1 200px;
    display: flex;
    flex-direction: row;
    align-items: center;
    gap: 8px;
    min-width: 0;
    padding: 4px 10px;
    border-radius: 4px;
    border: 1px solid #e3eaf7;
    background: #ffffff;
    transition: border-color 0.2s ease;
}

.flatlayout-toolbar__group:hover {
    border-color: #c3d4f7;
}

.flatlayout-toolbar__group label {
    flex-shrink: 0;
    font-size: 12px;
    font-weight: 600;
    color: #1f2a37;
    white-space: nowrap;
    letter-spacing: 0.02em;
    line-height: 1.2;
}

.flatlayout-toolbar__value {
    flex-shrink: 0;
    min-width: 40px;
    padding: 1px 6px;
    border-radius: 999px;
    background: #eef4ff;
    color: #315ea8;
    font-size: 11px;
    font-weight: 700;
    text-align: center;
    font-variant-numeric: tabular-nums;
}

.flatlayout-toolbar__group .el-slider {
    flex: 1 1 auto;
    min-width: 0;
    margin: 0;
}
</style>
