<template>
    <div>
        <div class="slope-scroll-container" ref="scrollContainerRef" @scroll.passive="handleHorizontalScroll"
            @wheel.prevent="handleScaleXWheel">
            <svg id="slope" :style="{ width: svgWidth + 'px', height: svgHeight + 'px' }">
                <defs>
                    <linearGradient id="backgroundGradient" x1="0%" y1="0%" x2="0%" y2="100%">
                        <stop offset="0%" style="stop-color: #ECF4E8; stop-opacity: 0.8" />
                        <stop offset="100%" style="stop-color: #EFE9E3; stop-opacity: 1" />
                    </linearGradient>
                    <linearGradient id="resistanceShadeGradient" x1="0%" y1="0%" x2="0%" y2="100%">
                        <stop offset="0%" style="stop-color: #C4E1E6; stop-opacity: 0.2" />
                        <stop offset="100%" style="stop-color: #9ECFD4; stop-opacity: 0.3" />
                    </linearGradient>
                </defs>

                <g class="background-fill">
                    <polygon :points="polygonPoints" fill="url(#backgroundGradient)" />
                    <polygon v-if="props.elementVisibility?.resistance" :points="shadePoints"
                        class="resistance-shade-clickable" fill="url(#resistanceShadeGradient)"
                        @click.stop="handleResistanceShadeClick($event)"></polygon>
                </g>
                <g class="axis">
                    <line class="xaxis" :x1="marginLeft" :x2="marginLeft + sketchWidth" :y1="svgHeight - marginBottom"
                        :y2="svgHeight - marginBottom">
                    </line>
                    <line class="yaxis" :x1="marginLeft" :x2="marginLeft" :y1="marginTop"
                        :y2="svgHeight - marginBottom">
                    </line>
                </g>
                <g class="xaxis-addpointbar">
                    <line class="addpointbar" :x1="marginLeft" :x2="marginLeft + sketchWidth"
                        :y1="svgHeight - marginBottom / 2" :y2="svgHeight - marginBottom / 2">
                    </line>
                    <!-- Add point handler that follows current cursor X -->
                    <g class="cursor-addpoint" v-if="cursorX >= 0 && cursorX <= sketchWidth / scaleX"
                        @click="addVPosition(cursorX)">
                        <circle class="addpointhandler" :cx="getX(cursorX)" :cy="svgHeight - marginBottom / 2" r="5" />
                        <text :x="getX(cursorX)" :y="svgHeight - marginBottom / 2" text-anchor="middle"
                            dominant-baseline="middle" font-size="14" fill="white" font-weight="bold"
                            style="cursor:pointer">+</text>
                    </g>
                </g>
                <g v-if="showRetarder" class="retarders">
                    <g v-for="retarder in retarderRects" :key="retarder.key">
                        <rect class="retarder-range" :class="{ 'retarder-range-active': retarder.isActivated }"
                            :x="retarder.x" :y="retarder.y" :width="retarder.width" :height="retarder.height"
                            :style="{ opacity: retarder.opacity }"
                            @dblclick.stop="openRetarderStatusDialog(retarder)" />
                        <text class="retarder-output-text" :x="retarder.x + retarder.width / 2" :y="retarder.y - 4">{{
                            retarder.outputPercentText }}</text>
                    </g>
                </g>
                <g v-if="props.elementVisibility?.breaking" class="breakingenergyheight">
                    <polygon v-if="props.elementVisibility?.resistance && breakingResistanceShadePoints"
                        :points="breakingResistanceShadePoints" class="breaking-resistance-shade" />
                    <polyline v-if="breakingPoints" :points="breakingPoints" class="breaking-line" />
                    <circle v-for="dataPoint in breakingEnergyHeightData?.filter(x => x.display) || []"
                        :key="`breaking-${dataPoint.x}`" class="breaking-circle" :cx="getX(dataPoint.x)"
                        :cy="getY(dataPoint.gravityEnergyHeight + dataPoint.kineticEnergyHeight)" r="4" />
                    <text v-for="label in retarderBreakingHeightLabels" :key="label.key"
                        class="retarder-breaking-height-text" :x="label.x" :y="label.y">
                        {{ label.text }}
                    </text>
                </g>
                <g v-if="props.elementVisibility?.kinetic && temporaryKineticHitAreaPoints" class="temporary-kinetic-hit-layer">
                    <polygon :points="temporaryKineticHitAreaPoints" class="temporary-kinetic-hit-area"
                        @click.stop="handleTemporaryKineticAreaClick($event)" />
                </g>
                <g class="slopelines">
                    <line v-for="seg in slopeLayout?.positionSegmentList || []" class="slope-line"
                        :x1="getX(getPositionX(seg.startPositionID))" :y1="getY(getPositionHeight(seg.startPositionID))"
                        :x2="getX(getPositionX(seg.endPositionID))" :y2="getY(getPositionHeight(seg.endPositionID))" />
                </g>
                <g v-if="props.elementVisibility?.resistance" class="resistance-energy-height">
                    <polyline :points="resistancePoints" class="resistance-line"
                        @click.stop="handleResistanceShadeClick($event)" />
                    <g v-for="dataPoint in resistanceEnergyHeightData || []">
                        <circle class="resistance-circle resistance-shade-clickable" :cx="getX(dataPoint.x)"
                            :cy="getY(orgKineticEnergyY - dataPoint.height)" r="4"
                            @click.stop="handleResistanceShadeClick($event, dataPoint.x)" />
                        <text v-if="showResistanceNumber" class="resistance-text" :x="getX(dataPoint.x)"
                            :y="(getY(orgKineticEnergyY - dataPoint.height) + getY(orgKineticEnergyY)) / 2">{{
                                dataPoint.height
                            }}m</text>
                        <line class="resistance-vline" :x1="getX(dataPoint.x)"
                            :y1="getY(orgKineticEnergyY - dataPoint.height)" :x2="getX(dataPoint.x)"
                            :y2="getY(orgKineticEnergyY)"></line>
                    </g>
                </g>
                <g class="init-kinetic-energy-height"
                    v-if="props.elementVisibility?.initialKinetic && kineticEnergyHeightData && kineticEnergyHeightData.length > 0 && slopeLayout?.positionList && slopeLayout.positionList.length > 0">
                    <line class="init-kinetic-energy-line" :x1="marginLeft" :x2="marginLeft + sketchWidth"
                        :y1="getY(orgKineticEnergyY)" :y2="getY(orgKineticEnergyY)" />
                </g>
                <g class="kinetic-energy-height">
                    <line v-if="props.elementVisibility?.kinetic" class="kinetic-vline" v-for="dataPoint in
                        kineticEnergyHeightData || []" :x1="getX(dataPoint.x)"
                        :y1="getY(dataPoint.result.gravitationHeight)" :x2="getX(dataPoint.x)"
                        :y2="getY(dataPoint.result.gravitationHeight + dataPoint.result.kineticEnergyHeight)"></line>
                    <text v-if="props.elementVisibility?.kinetic && showKineticNumber" class="kinetic-text" v-for="dataPoint in
                        kineticEnergyHeightData || []" :x="getX(dataPoint.x)"
                        :y="kineticTextPositions.get(dataPoint.x) ?? ((getY(dataPoint.result.gravitationHeight) + getY(dataPoint.result.gravitationHeight + dataPoint.result.kineticEnergyHeight)) / 2)">{{
                            formatKineticEnergyHeight(dataPoint.result.kineticEnergyHeight)
                        }}m({{ dataPoint.result.velocity }}m/s)</text>
                    <line v-if="props.elementVisibility?.kinetic && temporaryKineticEnergyPoint" class="kinetic-vline temporary-kinetic-vline"
                        :x1="getX(temporaryKineticEnergyPoint.x)" :y1="getY(temporaryKineticEnergyPoint.gravitationHeight)"
                        :x2="getX(temporaryKineticEnergyPoint.x)"
                        :y2="getY(temporaryKineticEnergyPoint.gravitationHeight + temporaryKineticEnergyPoint.kineticEnergyHeight)"></line>
                    <text v-if="props.elementVisibility?.kinetic && temporaryKineticEnergyPoint" class="kinetic-text temporary-kinetic-text"
                        :x="getX(temporaryKineticEnergyPoint.x)" :y="temporaryKineticTextY">{{
                            formatKineticEnergyHeight(temporaryKineticEnergyPoint.kineticEnergyHeight)
                        }}m({{ formatKineticVelocity(temporaryKineticEnergyPoint.velocity) }}m/s)</text>
                </g>
                <g class="points">
                    <g v-for="pos in slopeLayout?.positionList || []"
                        @contextmenu.prevent="openContextMenu(pos, $event)">
                        <text v-if="showPointHeightNumber" class="point-height-text" :x="getX(pos.x)"
                            :y="(textPositions.get(pos.id)?.y ?? (getY(pos.height) - 10))">{{ pos.height }}m</text>
                        <line
                            v-if="showPointHeightNumber && Math.abs(getY(pos.height) - (textPositions.get(pos.id)?.y ?? (getY(pos.height) - 10))) >= 15"
                            class="point-line" :x1="getX(pos.x)"
                            :y1="textPositions.get(pos.id)?.barStartY ?? (getY(pos.height) - 10)" :x2="getX(pos.x)"
                            :y2="(textPositions.get(pos.id)?.barEndY ?? (getY(pos.height) - 10))"></line>
                        <circle class="point-circle" :cx="getX(pos.x)" :cy="getY(pos.height)" r="4"
                            :class="{ 'point-circle-longpress': longPressActivatedId === pos.id, 'point-circle-dragging': draggingId === pos.id }"
                            @mousedown="startDrag(pos, $event)" @touchstart.prevent="startTouchDrag(pos, $event)">
                        </circle>
                        <circle class="point-hit-area" :cx="getX(pos.x)" :cy="getY(pos.height)" r="4"
                            @mousedown="startDrag(pos, $event)" @touchstart.prevent="startTouchDrag(pos, $event)">
                        </circle>
                    </g>
                </g>
                <g class="guide-lines" v-if="draggingId">
                    <line v-if="dragMode === 'horizontal'" class="guide-line horizontal" :x1="marginLeft"
                        :y1="getY(currentHeight)" :x2="marginLeft + sketchWidth" :y2="getY(currentHeight)" />
                    <line v-if="dragMode === 'vertical'" class="guide-line vertical" :x1="getX(currentX)"
                        :y1="marginTop" :x2="getX(currentX)" :y2="svgHeight - marginBottom" />
                </g>
                <g class="cursor">
                    <line class="cursor-vline" :y1="marginTop" :y2="svgHeight - marginBottom" :x1="getX(cursorX)"
                        :x2="getX(cursorX)"></line>
                    <g v-if="cursorSlopeInfo" class="cursor-slope-info">
                        <circle class="cursor-slope-point-halo" :cx="cursorSlopeInfo.pointX" :cy="cursorSlopeInfo.pointY"
                            r="8"></circle>
                        <!-- <circle class="cursor-slope-point" :cx="cursorSlopeInfo.pointX" :cy="cursorSlopeInfo.pointY"
                            r="3.5"></circle> -->
                        <line class="cursor-slope-connector" :x1="cursorSlopeInfo.pointX" :y1="cursorSlopeInfo.pointY"
                            :x2="cursorSlopeInfo.connectorX" :y2="cursorSlopeInfo.connectorY"></line>
                        <rect class="cursor-slope-label-shadow" :x="cursorSlopeInfo.shadowX" :y="cursorSlopeInfo.shadowY"
                            :width="cursorSlopeInfo.labelWidth" :height="cursorSlopeInfo.labelHeight" rx="4"
                            ry="4"></rect>
                        <rect class="cursor-slope-label-box" :x="cursorSlopeInfo.labelX" :y="cursorSlopeInfo.labelY"
                            :width="cursorSlopeInfo.labelWidth" :height="cursorSlopeInfo.labelHeight" rx="4"
                            ry="4"></rect>
                        <line class="cursor-slope-label-divider" :x1="cursorSlopeInfo.labelX + 12"
                            :x2="cursorSlopeInfo.labelX + cursorSlopeInfo.labelWidth - 12"
                            :y1="cursorSlopeInfo.dividerY" :y2="cursorSlopeInfo.dividerY"></line>
                        <text class="cursor-slope-label-caption" :x="cursorSlopeInfo.textX"
                            :y="cursorSlopeInfo.firstRowY">
                            X
                        </text>
                        <text class="cursor-slope-label-value" :x="cursorSlopeInfo.valueX"
                            :y="cursorSlopeInfo.firstRowY">
                            {{ cursorSlopeInfo.xText }} m
                        </text>
                        <text class="cursor-slope-label-caption" :x="cursorSlopeInfo.textX"
                            :y="cursorSlopeInfo.secondRowY">
                            H
                        </text>
                        <text class="cursor-slope-label-value" :x="cursorSlopeInfo.valueX"
                            :y="cursorSlopeInfo.secondRowY">
                            {{ cursorSlopeInfo.heightText }} m
                        </text>
                    </g>
                </g>
            </svg>
        </div>
        <div v-if="contextMenu.visible" class="context-menu"
            :style="{ left: contextMenu.x + 'px', top: contextMenu.y + 'px' }">
            <div class="context-menu-item" @click.stop="deleteContextPos">{{ t('humpSlopeCtrl.contextMenu.deleteNode') }}</div>
        </div>
        <el-dialog v-model="showRetarderStatusDialog" :title="t('humpSlopeCtrl.dialog.retarderSettings')" width="420px"
            :close-on-click-modal="false" append-to-body>
            <div v-if="editingRetarderStatus">
                <div style="margin-bottom: 10px;">{{ t('humpSlopeCtrl.labels.retarderID') }}: {{
                    editingRetarderStatus.retarderID }}</div>
                <div style="margin-bottom: 12px;">
                    <span style="margin-right: 8px;">{{ t('humpSlopeCtrl.labels.enabled') }}</span>
                    <el-switch v-model="editingRetarderStatus.isActivated" />
                </div>
                <div style="margin-bottom: 12px;">
                    <span style="display:inline-block; width: 80px;">{{ t('humpSlopeCtrl.labels.output') }}</span>
                    <el-input-number v-model="editingRetarderStatus.output" :min="0" :max="1" :step="0.1"
                        :precision="2" />
                </div>
                <div>
                    <span style="display:inline-block; width: 80px;">{{ t('humpSlopeCtrl.labels.totalEnergyHeight')
                    }}</span>
                    <el-input-number v-model="editingRetarderStatus.totalEnergyHeight" :min="0" :step="0.01"
                        :precision="3" />
                </div>
            </div>
            <template #footer>
                <el-button @click="showRetarderStatusDialog = false">{{ t('common.buttons.cancel') }}</el-button>
                <el-button type="primary" @click="saveRetarderStatusDialog">{{ t('common.buttons.confirm')
                }}</el-button>
            </template>
        </el-dialog>
    </div>
</template>
<script setup lang="ts">
import { CurveDirections, FlatLayout, LocationParam, SlopeLayout, VPosition, VPositionSegment } from './humplayoutctrl';
import { ref, computed, onBeforeUnmount, onMounted, watch } from 'vue';
import { useI18n } from 'vue-i18n';

type RetarderStatusItem = {
    retarderID: string
    isActivated: boolean
    output: number
    totalEnergyHeight: number
}

type RetarderRectItem = {
    key: string
    retarderID: string
    x: number
    y: number
    width: number
    height: number
    isActivated: boolean
    output: number
    totalEnergyHeight: number
    outputPercentText: string
    opacity: number
}

type BreakingEnergyHeightPoint = {
    x: number
    breakingEnergyHeight: number
    gravityEnergyHeight: number
    kineticEnergyHeight: number
    display: boolean
}

type TemporaryKineticEnergyPoint = {
    x: number
    gravitationHeight: number
    kineticEnergyHeight: number
    velocity: number
}

const props = defineProps<{
    flatLayout?: FlatLayout | null
    slopeLayout?: SlopeLayout | null
    retarderStatusList?: RetarderStatusItem[] | null
    resistanceEnergyHeightData?: { x: number, height: number }[] | null
    kineticEnergyHeightData?: { x: number, result: any }[] | null
    breakingEnergyHeightData?: BreakingEnergyHeightPoint[] | null
    globalScaleX?: number
    globalScaleY?: number
    elementVisibility?: {
        initialKinetic: boolean
        resistance: boolean
        kinetic: boolean
        breaking: boolean
        retarder: boolean
        resistanceNumber: boolean
        kineticNumber: boolean
        pointHeightNumber: boolean
        cursorPositionLabel: boolean
    }
    g_?: number
    globalCursorX?: number
}>()

const emit = defineEmits<{
    updateGlobalCursorX: [value: number]
    'horizontal-scroll': [scrollLeft: number]
    'wheel-scale-x': [payload: { scaleX: number, scrollLeft: number }]
    'update-retarder-status-list': [value: RetarderStatusItem[]]
    'resistance-click': [payload: { x: number, clientX: number, clientY: number }]
    'control-point-drag-end': []
}>()

function handleResistanceShadeClick(event: MouseEvent, dataX?: number) {
    const computedX = getDataXFromMouseEvent(event);
    const x = dataX !== undefined ? dataX : computedX;
    if (x === null) return;
    if (!Number.isFinite(x) || x < 0) return;
    emit('resistance-click', { x, clientX: event.clientX, clientY: event.clientY });
}
const { t } = useI18n();
const scrollContainerRef = ref<HTMLDivElement | null>(null);
const temporaryKineticEnergyPoint = ref<TemporaryKineticEnergyPoint | null>(null);
const minScaleX = 0.1;
const maxScaleX = 5;

function handleHorizontalScroll(event: Event) {
    const target = event.target as HTMLDivElement | null;
    if (!target) return;
    emit('horizontal-scroll', target.scrollLeft);
}

function setScrollLeft(scrollLeft: number) {
    if (!scrollContainerRef.value) return;
    scrollContainerRef.value.scrollLeft = scrollLeft;
}

function clampScaleX(scaleX: number) {
    return Math.min(maxScaleX, Math.max(minScaleX, scaleX));
}

function handleScaleXWheel(event: WheelEvent) {
    const container = scrollContainerRef.value;
    if (!container) return;

    const oldScale = scaleX.value;
    const zoomFactor = event.deltaY < 0 ? 1.1 : 0.9;
    const nextScale = clampScaleX(Math.round(oldScale * zoomFactor * 1000) / 1000);
    if (Math.abs(nextScale - oldScale) < 1e-6) return;

    const rect = container.getBoundingClientRect();
    const localX = event.clientX - rect.left;
    const anchorDataX = (container.scrollLeft + localX - marginLeft.value) / oldScale;
    const nextScrollLeft = Math.max(0, marginLeft.value + anchorDataX * nextScale - localX);

    emit('wheel-scale-x', {
        scaleX: nextScale,
        scrollLeft: nextScrollLeft
    });
}

const minSvgHeight = ref(400);

const scaleX = computed(() => props.globalScaleX ?? 3.5);
const scaleY = computed(() => props.globalScaleY ?? 80);

const localCursorX = ref(0);
const cursorX = computed({
    get() {
        return props.globalCursorX !== undefined ? props.globalCursorX : localCursorX.value;
    },
    set(newVal: number) {
        // If parent controls cursor X, emit update instead of mutating local state.
        if (props.globalCursorX === undefined) {
            localCursorX.value = newVal;
        }
        else {
            // Sync cursor X back to parent.
            emit('updateGlobalCursorX', newVal);
        }
    }
});


const marginLeft = ref(50);
const marginRight = ref(20);
const marginBottom = ref(20);
const marginTop = ref(20);
const showRetarder = computed(() => props.elementVisibility?.retarder ?? true);
const showResistanceNumber = computed(() => props.elementVisibility?.resistanceNumber ?? true);
const showKineticNumber = computed(() => props.elementVisibility?.kineticNumber ?? true);
const showPointHeightNumber = computed(() => props.elementVisibility?.pointHeightNumber ?? true);
const showCursorPositionLabel = computed(() => props.elementVisibility?.cursorPositionLabel ?? true);
const draggingId = ref<string | null>(null);
const startMouseY = ref(0);
const startHeight = ref(0);
const startMouseX = ref(0);
const startX = ref(0);
const dragMode = ref<'vertical' | 'horizontal'>('vertical');
const currentX = ref(0);
const currentHeight = ref(0);
const touchLongPressTimer = ref<ReturnType<typeof setTimeout> | null>(null);
const touchStartClientX = ref(0);
const touchStartClientY = ref(0);
const touchCurrentClientX = ref(0);
const touchCurrentClientY = ref(0);
const didDragChange = ref(false);
const touchHorizontalDragActivated = ref(false);

function notifyControlPointChanged() {
    emit('control-point-drag-end');
}
const touchLongPressDelay = 550;
const touchMoveThreshold = 8;
const touchHorizontalActivationThreshold = 12;
const longPressActivatedId = ref<string | null>(null);
const touchLongPressTriggered = ref(false);
const contextMenu = ref<{ visible: boolean; x: number; y: number; posId: string }>({ visible: false, x: 0, y: 0, posId: '' });

function clearTouchLongPressTimer() {
    if (touchLongPressTimer.value) {
        clearTimeout(touchLongPressTimer.value);
        touchLongPressTimer.value = null;
    }
}

function resetTouchLongPressState() {
    longPressActivatedId.value = null;
    touchLongPressTriggered.value = false;
    touchHorizontalDragActivated.value = false;
}

function openContextMenuAt(posId: string, x: number, y: number) {
    contextMenu.value = { visible: true, x, y, posId };
    window.addEventListener('click', closeContextMenu);
}

function openContextMenu(pos: { id: string }, event: MouseEvent) {
    event.preventDefault();
    openContextMenuAt(pos.id, event.clientX, event.clientY);
}

function closeContextMenu() {
    contextMenu.value = { visible: false, x: 0, y: 0, posId: '' };
    window.removeEventListener('click', closeContextMenu);
}

function getX(posX: number): number {
    return posX * scaleX.value + marginLeft.value;
}

function getDataXFromMouseEvent(event: MouseEvent): number | null {
    const svg = document.getElementById('slope');
    if (!svg) return null;
    const rect = svg.getBoundingClientRect();
    const mouseX = event.clientX - rect.left;
    const computedX = (mouseX - marginLeft.value) / scaleX.value;
    return Number.isFinite(computedX) ? computedX : null;
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

function getFlatPositionX(positionID: string): number | null {
    const position = props.flatLayout?.positionList?.find(pos => pos.id?.toString() === positionID?.toString());
    if (!position) return null;
    return Number.isFinite(position.x) ? position.x : null;
}

function normalizeId(value: unknown): string {
    return String(value ?? '').trim().toLowerCase();
}

function formatOutputPercent(output: unknown): string {
    const value = Number(output);
    if (!Number.isFinite(value)) {
        return '0%';
    }
    const percent = Math.max(0, Math.min(1, value)) * 100;
    if (Math.abs(percent - Math.round(percent)) < 1e-9) {
        return `${Math.round(percent)}%`;
    }
    return `${percent.toFixed(1)}%`;
}

function toOpacityFromOutput(output: unknown): number {
    const value = Number(output);
    if (!Number.isFinite(value)) return 0;
    return Math.max(0, Math.min(1, value));
}

const retarderStatusMap = computed(() => {
    const map = new Map<string, RetarderStatusItem>();
    const list = props.retarderStatusList || [];
    for (const status of list) {
        const id = normalizeId(status?.retarderID);
        if (!id) continue;
        map.set(id, {
            retarderID: status?.retarderID ?? '',
            isActivated: Boolean(status?.isActivated),
            output: toOpacityFromOutput(status?.output ?? 1),
            totalEnergyHeight: Math.max(0, Number(status?.totalEnergyHeight ?? 0))
        });
    }
    return map;
});

const retarderRects = computed(() => {
    const list = (props.flatLayout as any)?.retarderList as any[] | undefined;
    const segments = props.flatLayout?.positionSegmentList || [];
    if (!Array.isArray(list) || list.length === 0) return [];

    const y = marginTop.value;
    const height = Math.max(0, svgHeight.value - marginTop.value - marginBottom.value);

    return list.map((retarder, index) => {
        const segmentId = retarder?.bindingPositionSegmentID ?? retarder?.bindingPositionSegment?.id;
        const directSegment = retarder?.bindingPositionSegment;
        const segment = segments.find(seg => seg.id?.toString() === segmentId?.toString()) ?? directSegment;
        const startX = getFlatPositionX(segment?.startPositionID);
        const endX = getFlatPositionX(segment?.endPositionID);
        if (startX === null || endX === null) return null;

        const x1 = getX(Math.min(startX, endX));
        const x2 = getX(Math.max(startX, endX));
        const retarderId = String(retarder?.id ?? '');
        const status = retarderStatusMap.value.get(normalizeId(retarderId));
        const hasStatus = Boolean(status);
        const resolvedStatus = status ?? {
            retarderID: retarderId,
            isActivated: false,
            output: 1,
            totalEnergyHeight: 0
        };
        const clampedOutput = toOpacityFromOutput(resolvedStatus.output);

        return {
            key: retarder?.id ?? segmentId ?? `retarder-${index}`,
            retarderID: retarderId,
            x: x1,
            y,
            width: Math.max(0, x2 - x1),
            height,
            isActivated: Boolean(resolvedStatus.isActivated),
            output: clampedOutput,
            totalEnergyHeight: resolvedStatus.totalEnergyHeight,
            outputPercentText: hasStatus ? formatOutputPercent(clampedOutput) : t('humpSlopeCtrl.labels.notConfigured'),
            opacity: clampedOutput
        };
    }).filter((item): item is RetarderRectItem => item !== null);
});

const showRetarderStatusDialog = ref(false);
const editingRetarderStatus = ref<RetarderStatusItem | null>(null);

function openRetarderStatusDialog(retarder: RetarderRectItem) {
    editingRetarderStatus.value = {
        retarderID: retarder.retarderID,
        isActivated: retarder.isActivated,
        output: retarder.output,
        totalEnergyHeight: retarder.totalEnergyHeight
    };
    showRetarderStatusDialog.value = true;
}

function saveRetarderStatusDialog() {
    if (!editingRetarderStatus.value) return;

    const edited: RetarderStatusItem = {
        retarderID: editingRetarderStatus.value.retarderID,
        isActivated: Boolean(editingRetarderStatus.value.isActivated),
        output: toOpacityFromOutput(editingRetarderStatus.value.output),
        totalEnergyHeight: Math.max(0, Number(editingRetarderStatus.value.totalEnergyHeight ?? 0))
    };

    const nextList: RetarderStatusItem[] = (props.retarderStatusList || []).map(item => ({
        retarderID: item.retarderID,
        isActivated: Boolean(item.isActivated),
        output: toOpacityFromOutput(item.output),
        totalEnergyHeight: Math.max(0, Number(item.totalEnergyHeight ?? 0))
    }));

    const idx = nextList.findIndex(item => normalizeId(item.retarderID) === normalizeId(edited.retarderID));
    if (idx >= 0) {
        nextList[idx] = edited;
    } else {
        nextList.push(edited);
    }

    emit('update-retarder-status-list', nextList);
    showRetarderStatusDialog.value = false;
}

function startDrag(pos: { id: string; height: number; x: number }, event: MouseEvent) {
    event.preventDefault();
    beginDrag(pos, event.clientX, event.clientY, event.altKey);
    window.addEventListener('mousemove', onMouseMove);
    window.addEventListener('mouseup', endDrag);
}

function startTouchDrag(pos: { id: string; height: number; x: number }, event: TouchEvent) {
    event.preventDefault();
    const touch = event.touches[0];
    if (!touch) return;
    beginDrag(pos, touch.clientX, touch.clientY, false);
    resetTouchLongPressState();
    touchStartClientX.value = touch.clientX;
    touchStartClientY.value = touch.clientY;
    touchCurrentClientX.value = touch.clientX;
    touchCurrentClientY.value = touch.clientY;
    clearTouchLongPressTimer();
    touchLongPressTimer.value = setTimeout(() => {
        if (!draggingId.value || dragMode.value !== 'vertical') return;
        const target = props.slopeLayout?.positionList?.find(p => p.id === draggingId.value);
        if (!target) return;
        longPressActivatedId.value = target.id;
        touchLongPressTriggered.value = true;
        clearTouchLongPressTimer();
    }, touchLongPressDelay);
    window.addEventListener('touchmove', onTouchMove, { passive: false });
    window.addEventListener('touchend', endTouchDrag);
    window.addEventListener('touchcancel', endTouchDrag);
}

function beginDrag(pos: { id: string; height: number; x: number }, clientX: number, clientY: number, isHorizontal: boolean) {
    clearTemporaryKineticEnergyPoint();
    closeContextMenu();
    draggingId.value = pos.id;
    currentX.value = pos.x;
    currentHeight.value = pos.height;
    didDragChange.value = false;
    if (isHorizontal) {
        dragMode.value = 'horizontal';
        startMouseX.value = clientX;
        startX.value = pos.x;
    } else {
        dragMode.value = 'vertical';
        startMouseY.value = clientY;
        startHeight.value = pos.height;
    }
}

function onMouseMove(event: MouseEvent) {
    if (!draggingId.value || !props.slopeLayout?.positionList) return;
    const target = props.slopeLayout.positionList.find(p => p.id === draggingId.value);
    if (!target) return;
    if (dragMode.value === 'vertical') {
        const deltaY = event.clientY - startMouseY.value;
        const nextHeight = Math.round(Math.max(0, startHeight.value - deltaY / scaleY.value) * 1000) / 1000;
        didDragChange.value = didDragChange.value || Math.abs(nextHeight - target.height) > 1e-6;
        target.height = nextHeight;
        currentHeight.value = nextHeight;
    } else if (dragMode.value === 'horizontal') {
        const deltaX = event.clientX - startMouseX.value;
        const nextX = Math.round(Math.max(0, startX.value + deltaX / scaleX.value) * 1000) / 1000;
        didDragChange.value = didDragChange.value || Math.abs(nextX - target.x) > 1e-6;
        target.x = nextX;
        currentX.value = nextX;
    }
    updateKineticEnergyHeights(draggingId.value);  // refresh kinetic profile for dragged point

    if (target.x === 0) { // hump crest moved
        // refresh kinetic profile for all other points
        props.slopeLayout.positionList.forEach(p => {
            if (p.id !== draggingId.value) {
                updateKineticEnergyHeights(p.id);
            }
        });
    }
}

function onTouchMove(event: TouchEvent) {
    if (!draggingId.value) return;
    event.preventDefault();
    const touch = event.touches[0];
    if (!touch) return;
    touchCurrentClientX.value = touch.clientX;
    touchCurrentClientY.value = touch.clientY;
    const target = props.slopeLayout?.positionList?.find(p => p.id === draggingId.value);
    if (!target) return;

    if (dragMode.value === 'vertical' && touchLongPressTimer.value) {
        const movedX = Math.abs(touch.clientX - touchStartClientX.value);
        const movedY = Math.abs(touch.clientY - touchStartClientY.value);
        if (movedX > touchMoveThreshold || movedY > touchMoveThreshold) {
            clearTouchLongPressTimer();
        }
    }

    if (touchLongPressTriggered.value && dragMode.value === 'vertical') {
        const movedX = touch.clientX - touchStartClientX.value;
        const movedY = touch.clientY - touchStartClientY.value;
        if (
            Math.abs(movedX) >= touchHorizontalActivationThreshold
            && Math.abs(movedX) > Math.abs(movedY)
        ) {
            dragMode.value = 'horizontal';
            touchHorizontalDragActivated.value = true;
            touchLongPressTriggered.value = false;
            startMouseX.value = touch.clientX;
            startX.value = target.x;
        }
        return;
    }

    if (dragMode.value === 'vertical') {
        const deltaY = touch.clientY - startMouseY.value;
        const nextHeight = Math.round(Math.max(0, startHeight.value - deltaY / scaleY.value) * 1000) / 1000;
        didDragChange.value = didDragChange.value || Math.abs(nextHeight - target.height) > 1e-6;
        target.height = nextHeight;
        currentHeight.value = nextHeight;
    } else {
        const deltaX = touch.clientX - startMouseX.value;
        const nextX = Math.round(Math.max(0, startX.value + deltaX / scaleX.value) * 1000) / 1000;
        didDragChange.value = didDragChange.value || Math.abs(nextX - target.x) > 1e-6;
        target.x = nextX;
        currentX.value = nextX;
    }

    updateKineticEnergyHeights(draggingId.value);
    if (target?.x === 0 && props.slopeLayout?.positionList) {
        props.slopeLayout.positionList.forEach(p => {
            if (p.id !== draggingId.value) {
                updateKineticEnergyHeights(p.id);
            }
        });
    }
}

function endDrag() {
    const finishedId = draggingId.value;
    if (!finishedId) return;

    clearTouchLongPressTimer();
    resetTouchLongPressState();
    window.removeEventListener('mousemove', onMouseMove);
    window.removeEventListener('mouseup', endDrag);
    window.removeEventListener('touchmove', onTouchMove);
    window.removeEventListener('touchend', endTouchDrag);
    window.removeEventListener('touchcancel', endTouchDrag);
    // updateKineticEnergyHeights(finishedId);
    draggingId.value = null;
    if (didDragChange.value) {
        notifyControlPointChanged();
    }
}

function endTouchDrag(event: TouchEvent) {
    const shouldOpenContextMenu = touchLongPressTriggered.value && !touchHorizontalDragActivated.value;
    const menuPosId = draggingId.value;
    const touch = event.changedTouches?.[0];
    const menuX = touch?.clientX ?? touchCurrentClientX.value;
    const menuY = touch?.clientY ?? touchCurrentClientY.value;

    endDrag();

    if (shouldOpenContextMenu && menuPosId) {
        openContextMenuAt(menuPosId, menuX, menuY);
    }

    touchLongPressTriggered.value = false;
}

function updateKineticEnergyHeights(id: string) {
    if (props.slopeLayout && props.slopeLayout.positionList) {
        const pos = props.slopeLayout.positionList.find(p => p.id === id);
        const kineticResultPos = props.kineticEnergyHeightData?.find(ked => ked.x === pos?.x);
        if (pos && kineticResultPos) {
            kineticResultPos.result.gravitationHeight = pos.height;

            const orgKineticEnergyHeight = kineticResultPos.result.orgKineticEnergyHeight;
            const humpHeight = props.slopeLayout.positionList?.find(p => p.x === 0)?.height || 0;

            const gravitationHeight = pos.height;
            const resistanceHeight = kineticResultPos.result.resistanceHeight || 0;
            const breakingHeight = kineticResultPos.result.breakingHeight || 0;

            const kineticEnergyHeight = Math.max(0, orgKineticEnergyHeight + (humpHeight - gravitationHeight) - resistanceHeight - breakingHeight);
            kineticResultPos.result.kineticEnergyHeight = Math.round(kineticEnergyHeight * 1000) / 1000;
            kineticResultPos.result.velocity = Math.round(Math.sqrt(2 * (props.g_ ?? 9.8) * kineticEnergyHeight) * 100) / 100;
        }
    }
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

const maxDisplayHeight = computed(() => {
    const candidates: number[] = [];

    if (props.slopeLayout?.positionList?.length) {
        candidates.push(...props.slopeLayout.positionList.map(pos => pos.height));
    }

    if (props.kineticEnergyHeightData?.length) {
        candidates.push(...props.kineticEnergyHeightData.map(dataPoint => dataPoint.result.gravitationHeight + dataPoint.result.kineticEnergyHeight));
    }

    if (props.breakingEnergyHeightData?.length) {
        candidates.push(...props.breakingEnergyHeightData.map(dataPoint =>
            dataPoint.breakingEnergyHeight + dataPoint.gravityEnergyHeight + dataPoint.kineticEnergyHeight));
    }

    if (orgKineticEnergyY.value) {
        candidates.push(orgKineticEnergyY.value);
    }

    return Math.max(0, ...candidates);
});

const svgHeight = computed(() => {
    const neededHeight = maxDisplayHeight.value * scaleY.value + marginTop.value + marginBottom.value + 20;
    return Math.max(minSvgHeight.value, neededHeight);
});

const svgWidth = computed(() => {
    return marginLeft.value + sketchWidth.value + marginRight.value;
});

const polygonPoints = computed(() => {
    if (!props.slopeLayout?.positionSegmentList || !props.slopeLayout.positionList) return '';

    // Collect all slope polyline points and sort by x.
    const points: { x: number; y: number }[] = [];
    props.slopeLayout.positionSegmentList.forEach(seg => {
        const startX = getPositionX(seg.startPositionID);
        const startY = getPositionHeight(seg.startPositionID);
        const endX = getPositionX(seg.endPositionID);
        const endY = getPositionHeight(seg.endPositionID);
        points.push({ x: startX, y: startY });
        points.push({ x: endX, y: endY });
    });
    // De-duplicate and sort.
    const uniquePoints = points.filter((point, index, self) =>
        index === self.findIndex(p => p.x === point.x && p.y === point.y)
    ).sort((a, b) => a.x - b.x);

    // Polygon points: top-left -> slope points -> bottom-right -> bottom-left.
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

// Compute vertical offsets for labels to avoid overlap.
const fontSize = ref(12);

// Compute label placements and avoid rectangle overlaps.
const textPositions = computed(() => {
    const map = new Map<string, { y: number; barStartY: number; barEndY: number }>();
    if (!props.slopeLayout?.positionList) return map;

    const placed: { id: string; x1: number; x2: number; y1: number; y2: number }[] = [];

    const charWidth = fontSize.value * 0.6; // approximate width per character
    const textHeight = fontSize.value; // approximate text height

    for (const pos of props.slopeLayout.positionList) {
        const text = String(pos.height);
        const width = Math.max(10, text.length * charWidth);
        const cx = getX(pos.x);
        // initial top position (y increases downwards in SVG)
        let ty = (getY(pos.height) + svgHeight.value - marginBottom.value) / 2;
        var anchor = 0;
        var barStartY = getY(pos.height) + 5;
        var barEndY = svgHeight.value - marginBottom.value;

        if (ty > svgHeight.value - marginBottom.value - 10) {  // keep labels readable
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
            // Move upward until it no longer overlaps.
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

// Adjust kinetic text positions to avoid overlap.
const kineticTextPositions = computed(() => {
    const map = new Map<number, number>();
    if (!props.kineticEnergyHeightData) return map;

    const charWidth = fontSize.value * 0.6;
    const textHeight = fontSize.value;
    const placed: { x1: number; x2: number; y1: number; y2: number }[] = [];

    for (const dataPoint of props.kineticEnergyHeightData) {
        const text = `${formatKineticEnergyHeight(dataPoint.result.kineticEnergyHeight)}m(${dataPoint.result.velocity}m/s)`;
        const width = Math.max(12, text.length * charWidth);
        const cx = getX(dataPoint.x);
        const baseY = (getY(dataPoint.result.gravitationHeight) + getY(dataPoint.result.gravitationHeight + dataPoint.result.kineticEnergyHeight)) / 2;
        const step = textHeight + 4;

        const getRect = (y: number) => ({
            x1: cx - width / 2,
            x2: cx + width / 2,
            y1: y - textHeight,
            y2: y
        });

        let ty = baseY;
        let rect = getRect(ty);
        let iter = 0;
        while (placed.some(p => !(p.x2 < rect.x1 || p.x1 > rect.x2 || p.y2 < rect.y1 || p.y1 > rect.y2))) {
            ty -= step;
            rect = getRect(ty);
            if (++iter > 30) break;
        }

        map.set(dataPoint.x, Math.round(ty * 1000) / 1000);
        placed.push(rect);
    }

    return map;
});

const resistancePoints = computed(() => {
    if (!props.resistanceEnergyHeightData) return '';
    return props.resistanceEnergyHeightData.map(dataPoint => {
        const x = getX(dataPoint.x);
        const y = getY(orgKineticEnergyY.value - dataPoint.height);
        return `${x},${y}`;
    }).join(' ');
});

const kineticPoints = computed(() => {
    if (!props.kineticEnergyHeightData) return '';
    return props.kineticEnergyHeightData.map(dataPoint => {
        const x = getX(dataPoint.x);
        const y = getY(dataPoint.result.kineticEnergyHeight);
        return `${x},${y}`;
    }).join(' ');
});

type CurveSamplePoint = {
    x: number
    value: number
}

type RetarderBreakingHeightLabel = {
    key: string
    x: number
    y: number
    text: string
}

function interpolateSeriesValue(points: CurveSamplePoint[], x: number): number | null {
    if (points.length === 0) return null;
    const first = points[0];
    const last = points[points.length - 1];
    if (!first || !last) return null;
    if (x < first.x || x > last.x) return null;

    for (let i = 0; i < points.length - 1; i++) {
        const p1 = points[i];
        const p2 = points[i + 1];
        if (!p1 || !p2) continue;

        if (Math.abs(x - p1.x) < 1e-9) return p1.value;
        if (Math.abs(x - p2.x) < 1e-9) return p2.value;

        if (x > p1.x && x < p2.x) {
            const dx = p2.x - p1.x;
            if (Math.abs(dx) < 1e-9) return p1.value;
            const t = (x - p1.x) / dx;
            return p1.value + (p2.value - p1.value) * t;
        }
    }

    return Math.abs(x - last.x) < 1e-9 ? last.value : null;
}

function formatHeightValue(value: number): string {
    if (!Number.isFinite(value)) return '0';
    return Number(value.toFixed(3)).toString();
}

function formatKineticEnergyHeight(value: unknown): string {
    const numericValue = Number(value);
    if (!Number.isFinite(numericValue)) return '0.000';
    return numericValue.toFixed(3);
}

function formatKineticVelocity(value: unknown): string {
    const numericValue = Number(value);
    if (!Number.isFinite(numericValue)) return '0';
    return Number(numericValue.toFixed(2)).toString();
}

type SlopeSamplePoint = {
    x: number
    height: number
}

const slopeSamplePoints = computed<SlopeSamplePoint[]>(() => {
    if (!Array.isArray(props.slopeLayout?.positionList)) return [];

    return props.slopeLayout.positionList
        .map((pos) => ({
            x: Number(pos.x),
            height: Number(pos.height)
        }))
        .filter((point) => Number.isFinite(point.x) && Number.isFinite(point.height))
        .sort((a, b) => a.x - b.x);
});

function getSlopeHeightAtX(x: number): number | null {
    const points = slopeSamplePoints.value;
    if (!Number.isFinite(x) || points.length === 0) return null;

    const first = points[0];
    const last = points[points.length - 1];
    if (!first || !last || x < first.x || x > last.x) return null;

    for (let i = 0; i < points.length - 1; i++) {
        const current = points[i];
        const next = points[i + 1];
        if (!current || !next) continue;

        if (Math.abs(x - current.x) < 1e-9) return current.height;
        if (Math.abs(x - next.x) < 1e-9) return next.height;
        if (x < current.x || x > next.x) continue;

        const deltaX = next.x - current.x;
        if (Math.abs(deltaX) < 1e-9) return current.height;

        const ratio = (x - current.x) / deltaX;
        return current.height + (next.height - current.height) * ratio;
    }

    return Math.abs(x - last.x) < 1e-9 ? last.height : null;
}

const breakingDisplaySeries = computed<CurveSamplePoint[]>(() => {
    if (!props.breakingEnergyHeightData?.length) return [];

    return [...props.breakingEnergyHeightData]
        .filter(dataPoint => Number.isFinite(dataPoint.x))
        .map(dataPoint => ({
            x: dataPoint.x,
            value: dataPoint.gravityEnergyHeight + dataPoint.kineticEnergyHeight
        }))
        .filter(point => Number.isFinite(point.value))
        .sort((a, b) => a.x - b.x);
});

const temporaryKineticHitAreaPoints = computed(() => {
    if (!props.elementVisibility?.kinetic) return '';

    const breakingSeries = breakingDisplaySeries.value;
    const slopeSeries = slopeSamplePoints.value;
    if (breakingSeries.length < 2 || slopeSeries.length < 2) return '';

    const startX = Math.max(breakingSeries[0]!.x, slopeSeries[0]!.x);
    const endX = Math.min(breakingSeries[breakingSeries.length - 1]!.x, slopeSeries[slopeSeries.length - 1]!.x);
    if (endX <= startX) return '';

    const sampleXRaw = [...breakingSeries.map(point => point.x), ...slopeSeries.map(point => point.x)]
        .filter(x => x >= startX && x <= endX)
        .sort((a, b) => a - b);

    const sampleX: number[] = [];
    for (const x of sampleXRaw) {
        const last = sampleX[sampleX.length - 1];
        if (last === undefined || Math.abs(x - last) > 1e-6) {
            sampleX.push(x);
        }
    }
    if (sampleX.length < 2) return '';

    const upperBoundary: string[] = [];
    const lowerBoundary: string[] = [];
    for (const x of sampleX) {
        const breakingValue = interpolateSeriesValue(breakingSeries, x);
        const slopeValue = getSlopeHeightAtX(x);
        if (breakingValue === null || slopeValue === null || breakingValue <= slopeValue) continue;

        upperBoundary.push(`${getX(x)},${getY(breakingValue)}`);
        lowerBoundary.push(`${getX(x)},${getY(slopeValue)}`);
    }

    if (upperBoundary.length < 2 || lowerBoundary.length < 2) return '';
    return [...upperBoundary, ...lowerBoundary.reverse()].join(' ');
});

const cursorSlopeInfo = computed(() => {
    if (!showCursorPositionLabel.value) return null;

    const height = getSlopeHeightAtX(cursorX.value);
    if (height === null) return null;

    const labelWidth = 80;
    const labelHeight = 34;
    const labelGap = 10;
    const pointX = getX(cursorX.value);
    const pointY = getY(height);
    const xText = formatKineticEnergyHeight(cursorX.value);
    const heightText = formatKineticEnergyHeight(height);
    const maxLabelX = svgWidth.value - marginRight.value - labelWidth;
    const preferredRightX = pointX + labelGap;
    const preferredLeftX = pointX - labelWidth - labelGap;
    const isRightSide = preferredRightX <= maxLabelX;
    let labelX = isRightSide ? preferredRightX : preferredLeftX;
    labelX = Math.max(marginLeft.value, Math.min(labelX, maxLabelX));

    const minLabelY = marginTop.value;
    const maxLabelY = svgHeight.value - marginBottom.value - labelHeight;
    const labelY = Math.max(minLabelY, Math.min(pointY - labelHeight - labelGap, maxLabelY));
    const connectorX = isRightSide ? labelX : labelX + labelWidth;
    const connectorY = Math.max(labelY + 8, Math.min(pointY, labelY + labelHeight - 8));

    return {
        pointX,
        pointY,
        labelX,
        labelY,
        labelWidth,
        labelHeight,
        shadowX: labelX + 1.5,
        shadowY: labelY + 2,
        connectorX,
        connectorY,
        dividerY: labelY + labelHeight / 2,
        textX: labelX + 10,
        valueX: labelX + 24,
        firstRowY: labelY + 11.5,
        secondRowY: labelY + 24.5,
        xText,
        heightText
    };
});

const temporaryKineticTextY = computed(() => {
    const dataPoint = temporaryKineticEnergyPoint.value;
    if (!dataPoint) return 0;

    const charWidth = fontSize.value * 0.6;
    const textHeight = fontSize.value;
    const step = textHeight + 4;
    const text = `${formatKineticEnergyHeight(dataPoint.kineticEnergyHeight)}m(${formatKineticVelocity(dataPoint.velocity)}m/s)`;
    const width = Math.max(12, text.length * charWidth);
    const cx = getX(dataPoint.x);
    const baseY = (getY(dataPoint.gravitationHeight) + getY(dataPoint.gravitationHeight + dataPoint.kineticEnergyHeight)) / 2;

    const getRect = (y: number) => ({
        x1: cx - width / 2,
        x2: cx + width / 2,
        y1: y - textHeight,
        y2: y
    });

    const existingRects = (props.kineticEnergyHeightData || []).map((item) => {
        const itemText = `${formatKineticEnergyHeight(item.result.kineticEnergyHeight)}m(${item.result.velocity}m/s)`;
        const itemWidth = Math.max(12, itemText.length * charWidth);
        const itemX = getX(item.x);
        const itemBaseY = (getY(item.result.gravitationHeight) + getY(item.result.gravitationHeight + item.result.kineticEnergyHeight)) / 2;
        const itemY = kineticTextPositions.value.get(item.x) ?? itemBaseY;
        return {
            x1: itemX - itemWidth / 2,
            x2: itemX + itemWidth / 2,
            y1: itemY - textHeight,
            y2: itemY
        };
    });

    let ty = baseY;
    let rect = getRect(ty);
    let iter = 0;
    while (existingRects.some(p => !(p.x2 < rect.x1 || p.x1 > rect.x2 || p.y2 < rect.y1 || p.y1 > rect.y2))) {
        ty -= step;
        rect = getRect(ty);
        if (++iter > 30) break;
    }

    return Math.round(Math.max(marginTop.value + textHeight, ty) * 1000) / 1000;
});

const retarderBreakingHeightLabels = computed<RetarderBreakingHeightLabel[]>(() => {
    const retarderList = (props.flatLayout as any)?.retarderList as any[] | undefined;
    const segments = props.flatLayout?.positionSegmentList || [];
    if (!Array.isArray(retarderList) || retarderList.length === 0 || !props.breakingEnergyHeightData?.length) {
        return [];
    }

    const breakingEnergySeries: CurveSamplePoint[] = props.breakingEnergyHeightData
        .filter(dataPoint => dataPoint.display && Number.isFinite(dataPoint.x))
        .map(dataPoint => ({
            x: dataPoint.x,
            value: dataPoint.breakingEnergyHeight
        }))
        .filter(point => Number.isFinite(point.value))
        .sort((a, b) => a.x - b.x);
    const breakingDisplaySeries: CurveSamplePoint[] = props.breakingEnergyHeightData
        .filter(dataPoint => dataPoint.display && Number.isFinite(dataPoint.x))
        .map(dataPoint => ({
            x: dataPoint.x,
            value: dataPoint.gravityEnergyHeight + dataPoint.kineticEnergyHeight
        }))
        .filter(point => Number.isFinite(point.value))
        .sort((a, b) => a.x - b.x);

    if (breakingEnergySeries.length === 0) return [];

    return retarderList.map((retarder, index) => {
        const segmentId = retarder?.bindingPositionSegmentID ?? retarder?.bindingPositionSegment?.id;
        const directSegment = retarder?.bindingPositionSegment;
        const segment = segments.find(seg => seg.id?.toString() === segmentId?.toString()) ?? directSegment;
        const startX = getFlatPositionX(segment?.startPositionID);
        const endX = getFlatPositionX(segment?.endPositionID);
        if (startX === null || endX === null) return null;

        const startHeight = interpolateSeriesValue(breakingEnergySeries, startX);
        const endHeight = interpolateSeriesValue(breakingEnergySeries, endX);
        const startDisplayHeight = interpolateSeriesValue(breakingDisplaySeries, startX);
        const endDisplayHeight = interpolateSeriesValue(breakingDisplaySeries, endX);
        if (startHeight === null || endHeight === null || startDisplayHeight === null || endDisplayHeight === null) return null;

        const heightDiff = Math.round(Math.abs(startHeight - endHeight) * 1000) / 1000;
        const midX = (startX + endX) / 2;
        const midY = (startDisplayHeight + endDisplayHeight) / 2;

        return {
            key: `retarder-breaking-${retarder?.id ?? segmentId ?? index}`,
            x: getX(endX),
            y: getY(midY),
            text: `${formatHeightValue(heightDiff)}m`
        };
    }).filter((item): item is RetarderBreakingHeightLabel => item !== null);
});

const breakingPoints = computed(() => {
    if (!props.breakingEnergyHeightData?.length) return '';
    return [...props.breakingEnergyHeightData]
        .sort((a, b) => a.x - b.x)
        .map(dataPoint => {
            const x = getX(dataPoint.x);
            const y = getY(dataPoint.gravityEnergyHeight + dataPoint.kineticEnergyHeight);
            return `${x},${y}`;
        }).join(' ');
});

const breakingResistanceShadePoints = computed(() => {
    if (!props.breakingEnergyHeightData?.length || !props.resistanceEnergyHeightData?.length) return '';

    const breakingSeries: CurveSamplePoint[] = props.breakingEnergyHeightData
        .filter(dataPoint => Number.isFinite(dataPoint.x))
        .map(dataPoint => ({
            x: dataPoint.x,
            value: dataPoint.gravityEnergyHeight + dataPoint.kineticEnergyHeight
        }))
        .filter(point => Number.isFinite(point.value))
        .sort((a, b) => a.x - b.x);

    const resistanceSeries: CurveSamplePoint[] = props.resistanceEnergyHeightData
        .filter(dataPoint => Number.isFinite(dataPoint.x))
        .map(dataPoint => ({
            x: dataPoint.x,
            value: orgKineticEnergyY.value - dataPoint.height
        }))
        .filter(point => Number.isFinite(point.value))
        .sort((a, b) => a.x - b.x);

    if (breakingSeries.length < 2 || resistanceSeries.length < 2) return '';

    const startX = Math.max(breakingSeries[0]!.x, resistanceSeries[0]!.x);
    const endX = Math.min(breakingSeries[breakingSeries.length - 1]!.x, resistanceSeries[resistanceSeries.length - 1]!.x);
    if (endX <= startX) return '';

    const sampleXRaw = [...breakingSeries.map(point => point.x), ...resistanceSeries.map(point => point.x)]
        .filter(x => x >= startX && x <= endX)
        .sort((a, b) => a - b);

    const sampleX: number[] = [];
    for (const x of sampleXRaw) {
        const last = sampleX[sampleX.length - 1];
        if (last === undefined || Math.abs(x - last) > 1e-6) {
            sampleX.push(x);
        }
    }
    if (sampleX.length < 2) return '';

    const breakingBoundary: string[] = [];
    const resistanceBoundary: string[] = [];
    for (const x of sampleX) {
        const breakingValue = interpolateSeriesValue(breakingSeries, x);
        const resistanceValue = interpolateSeriesValue(resistanceSeries, x);
        if (breakingValue === null || resistanceValue === null) continue;

        breakingBoundary.push(`${getX(x)},${getY(breakingValue)}`);
        resistanceBoundary.push(`${getX(x)},${getY(resistanceValue)}`);
    }

    if (breakingBoundary.length < 2 || resistanceBoundary.length < 2) return '';
    return [...breakingBoundary, ...resistanceBoundary.reverse()].join(' ');
});

const shadePoints = computed(() => {
    if (!props.resistanceEnergyHeightData || !props.kineticEnergyHeightData || props.kineticEnergyHeightData.length === 0 || !props.slopeLayout?.positionList || props.slopeLayout.positionList.length === 0) return '';
    const resPoints = props.resistanceEnergyHeightData.map(dataPoint => {
        const x = getX(dataPoint.x);
        const y = getY(orgKineticEnergyY.value - dataPoint.height);
        return `${x},${y}`;
    });
    const kineticY = getY(orgKineticEnergyY.value);
    const leftX = marginLeft.value;
    const rightX = marginLeft.value + sketchWidth.value;
    const points = [...resPoints, `${rightX},${kineticY}`, `${leftX},${kineticY}`];
    return points.join(' ');
});

const orgKineticEnergyY = computed(() => {
    const ked = props.kineticEnergyHeightData;
    const sl = props.slopeLayout;
    if (!ked || ked.length === 0 || !sl?.positionList || sl.positionList.length === 0) return 0;
    const safeKed = ked as { x: number, result: any }[];
    const safeSl = sl as SlopeLayout;
    return safeKed[0]!.result.kineticEnergyHeight + safeSl.positionList[0]!.height;
});

function clearTemporaryKineticEnergyPoint() {
    temporaryKineticEnergyPoint.value = null;
}

function handleTemporaryKineticAreaClick(event: MouseEvent) {
    if (temporaryKineticEnergyPoint.value) {
        clearTemporaryKineticEnergyPoint();
        return;
    }

    const x = getDataXFromMouseEvent(event);
    if (x === null || x < 0) return;

    const gravitationHeight = getSlopeHeightAtX(x);
    const breakingDisplayHeight = interpolateSeriesValue(breakingDisplaySeries.value, x);
    if (gravitationHeight === null || breakingDisplayHeight === null) return;

    const rawKineticEnergyHeight = breakingDisplayHeight - gravitationHeight;
    if (!Number.isFinite(rawKineticEnergyHeight) || rawKineticEnergyHeight <= 0) {
        clearTemporaryKineticEnergyPoint();
        return;
    }

    const g = Number.isFinite(props.g_ ?? NaN) ? Number(props.g_) : 9.8;
    temporaryKineticEnergyPoint.value = {
        x: Math.round(x * 1000) / 1000,
        gravitationHeight: Math.round(gravitationHeight * 1000) / 1000,
        kineticEnergyHeight: Math.round(rawKineticEnergyHeight * 1000) / 1000,
        velocity: Math.round(Math.sqrt(2 * g * rawKineticEnergyHeight) * 100) / 100
    };
}

function addCursorXListener() {
    const svgElement = document.getElementById('slope');
    if (!svgElement) return;
    svgElement.addEventListener('mousemove', (event) => {
        const rect = svgElement.getBoundingClientRect();
        const mouseX = event.clientX - rect.left; // mouse X relative to SVG left edge
        const posX = (mouseX - marginLeft.value) / scaleX.value;
        cursorX.value = posX;
    });
}

let tempVPositionIdSeq = 0;
function createTempVPositionId(): string {
    tempVPositionIdSeq += 1;
    return `tmp-vp-${Date.now()}-${tempVPositionIdSeq}`;
}

function buildSegments(positions: VPosition[]): VPositionSegment[] {
    const sorted = [...positions].sort((a, b) => a.x - b.x);
    const segments: VPositionSegment[] = [];
    for (let i = 0; i < sorted.length - 1; i++) {
        const start = sorted[i];
        const end = sorted[i + 1];
        if (!start || !end) continue;
        const length = Math.max(0, Math.round((end.x - start.x) * 1000) / 1000);
        const gradient = length !== 0 ? Math.round(((end.height - start.height) / length) * 1000) : 0;
        const seg = new VPositionSegment(
            `vseg-${i}-${Date.now()}`,
            start.id,
            end.id,
            length,
            0,
            LocationParam.YardSection,
            CurveDirections.None,
            gradient,
            end.height,
        );
        segments.push(seg);
    }
    return segments;
}

function removePosition(posId: string) {
    clearTemporaryKineticEnergyPoint();
    const sl = props.slopeLayout;
    if (!sl || !Array.isArray(sl.positionList)) return;
    sl.positionList = sl.positionList.filter(p => p.id !== posId).sort((a, b) => a.x - b.x);
    sl.positionSegmentList = buildSegments(sl.positionList);
    notifyControlPointChanged();
}

function deleteContextPos() {
    if (contextMenu.value.posId) {
        removePosition(contextMenu.value.posId);
    }
    closeContextMenu();
}

function addVPosition(posX: number) {
    clearTemporaryKineticEnergyPoint();
    const sl = props.slopeLayout;
    if (!sl) return;
    if (!Array.isArray(sl.positionList)) sl.positionList = [];

    const positions = [...sl.positionList].sort((a, b) => a.x - b.x);

    // Do not add duplicate x positions.
    if (positions.some((p) => Math.abs(p.x - posX) < 1e-6)) return;

    // Initialize with two default points when empty.
    if (positions.length === 0) {
        const newPos1 = new VPosition(createTempVPositionId(), posX, 2);
        const newPos2 = new VPosition(createTempVPositionId(), posX + 5, 2);
        const updated = [...positions, newPos1, newPos2].sort((a, b) => a.x - b.x);
        sl.positionList = updated;
        sl.positionSegmentList = buildSegments(updated);
        notifyControlPointChanged();
        return;
    }
    else if (positions.length === 1) {
        const newPos1 = new VPosition(createTempVPositionId(), posX, 2);
        const updated = [...positions, newPos1].sort((a, b) => a.x - b.x);
        sl.positionList = updated;
        sl.positionSegmentList = buildSegments(updated);
        notifyControlPointChanged();
        return;
    }

    const left = positions.filter((p) => p.x <= posX).pop();
    const right = positions.find((p) => p.x >= posX);

    let height = 0;
    if (left && right && left !== right) {
        const deltaX = right.x - left.x;
        if (Math.abs(deltaX) < 1e-6) {
            height = left.height;
        } else {
            const ratio = (posX - left.x) / deltaX;
            height = left.height + (right.height - left.height) * ratio;
        }
    } else if (left) {
        height = left.height;
    } else if (right) {
        height = right.height;
    }

    const newPos = new VPosition(createTempVPositionId(), posX, Math.round(height * 1000) / 1000);
    const updated = [...positions, newPos].sort((a, b) => a.x - b.x);
    sl.positionList = updated;
    sl.positionSegmentList = buildSegments(updated);
    notifyControlPointChanged();
}

onMounted(() => {
    addCursorXListener();
});

watch(() => props.breakingEnergyHeightData, () => {
    clearTemporaryKineticEnergyPoint();
});

watch(() => props.slopeLayout, () => {
    clearTemporaryKineticEnergyPoint();
});

watch(() => props.elementVisibility?.kinetic, (visible) => {
    if (!visible) {
        clearTemporaryKineticEnergyPoint();
    }
});

onBeforeUnmount(() => {
    clearTouchLongPressTimer();
    window.removeEventListener('mousemove', onMouseMove);
    window.removeEventListener('mouseup', endDrag);
    window.removeEventListener('touchmove', onTouchMove);
    window.removeEventListener('touchend', endTouchDrag);
    window.removeEventListener('touchcancel', endTouchDrag);
    closeContextMenu();
});

defineExpose({
    setScrollLeft
});
</script>
<style scoped lang="css">
.cursor-vline {
    stroke: orange;
    stroke-width: 1px;
    pointer-events: none;
    opacity: 0.4;
}

.cursor-slope-point {
    fill: #fffaf0;
    stroke: #d97706;
    stroke-width: 2px;
    pointer-events: none;
}

.cursor-slope-point-halo {
    fill: rgba(245, 158, 11, 0.16);
    pointer-events: none;
}

.cursor-slope-connector {
    stroke: rgba(217, 119, 6, 0.72);
    stroke-width: 1.5px;
    stroke-linecap: round;
    pointer-events: none;
}

.cursor-slope-label-shadow {
    fill: rgba(15, 23, 42, 0.08);
    opacity: 0.6;
    pointer-events: none;
}

.cursor-slope-label-box {
    fill: rgba(255, 252, 245, 0.6);
    stroke: rgba(217, 119, 6, 0.6);
    stroke-width: 1px;
    pointer-events: none;
}

.cursor-slope-label-divider {
    stroke: rgba(217, 119, 6, 0.24);
    stroke-width: 1px;
    opacity: 0.6;
    pointer-events: none;
}

.cursor-slope-label-caption {
    fill: #b45309;
    font-size: 9px;
    font-family: 'Segoe UI', 'PingFang SC', 'Microsoft YaHei', sans-serif;
    font-weight: 700;
    letter-spacing: 0.04em;
    dominant-baseline: middle;
    opacity: 0.6;
    pointer-events: none;
    user-select: none;
}

.cursor-slope-label-value {
    fill: #4a3410;
    font-size: 10px;
    font-family: 'Consolas', 'Menlo', monospace;
    font-weight: 600;
    dominant-baseline: middle;
    opacity: 0.6;
    pointer-events: none;
    user-select: none;
}

.te {
    color: blue;
}

#slope {
    min-width: 100%;
    background-color: whitesmoke;
}

.slope-scroll-container {
    width: 100%;
    height: 100%;
    overflow-x: auto;
    overflow-y: auto;
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
    touch-action: none;
    transform-box: fill-box;
    transform-origin: center;
    transition: transform 120ms ease;
    pointer-events: none;
}

.points .point-hit-area {
    fill: transparent;
    stroke: none;
    cursor: pointer;
    touch-action: none;
    pointer-events: all;
}

.points .point-circle.point-circle-longpress,
.points .point-circle.point-circle-dragging {
    transform: scale(1.8);
}

.slope-line {
    stroke: #C2A68C;
    stroke-width: 3px;
    pointer-events: none;
}

.retarders .retarder-range {
    fill: none;
    stroke: #2f74d0;
    stroke-width: 1.5px;
    pointer-events: auto;
    cursor: pointer;
}

.retarders .retarder-range.retarder-range-active {
    fill: rgba(47, 116, 208, 0.28);
}

.retarders .retarder-output-text {
    fill: #1e3a8a;
    font-size: 11px;
    font-weight: 600;
    text-anchor: middle;
    dominant-baseline: auto;
    user-select: none;
    pointer-events: none;
}

.guide-line {
    stroke: gray;
    stroke-width: 1px;
    stroke-dasharray: 5, 5;
    pointer-events: none;
}

.point-height-text {
    font-size: 12px;
    fill: darkred;
    text-anchor: middle;
    user-select: none;
    pointer-events: none;
}

.point-line {
    stroke: darkred;
    stroke-width: 1px;
    opacity: 0.5;
    pointer-events: none;
}

.resistance-circle {
    stroke: #4988C4;
    stroke-width: 1.5px;
    fill: white;
    pointer-events: none;
}

.breaking-circle {
    stroke: #ff0000;
    stroke-width: 2px;
    fill: white;
    pointer-events: none;
}

.breaking-line {
    stroke: #ff0000;
    stroke-width: 2px;
    fill: none;
    pointer-events: none;
}

.breaking-resistance-shade {
    fill: red;
    opacity: 0.06;
    stroke: none;
    pointer-events: none;
}

.temporary-kinetic-hit-area {
    fill: transparent;
    stroke: none;
    cursor: default;
    pointer-events: all;
}

.retarder-breaking-height-text {
    fill: #d10000;
    font-size: 11px;
    font-weight: 600;
    text-anchor: middle;
    dominant-baseline: hanging;
    user-select: none;
    pointer-events: none;
}

.resistance-text {
    font-size: 12px;
    fill: #4988C4;
    text-anchor: middle;
    dominant-baseline: middle;
    user-select: none;
    pointer-events: none;
}

.resistance-line {
    stroke: #4988C4;
    stroke-width: 2px;
    fill: none;
    pointer-events: none;
}

.resistance-shade {
    /* fill: rgba(0, 0, 255, 0.2); */
    stroke: none;
}

.resistance-shade-clickable {
    cursor: pointer;
    pointer-events: auto !important;
}

.resistance-shade-clickable:hover {
    filter: brightness(0.95);
}

.init-kinetic-energy-line {
    stroke: #016B61;
    stroke-width: 1px;
    stroke-dasharray: 4 2;
    fill: none;
    pointer-events: none;
}

.resistance-vline {
    stroke: #4988C4;
    stroke-width: 1px;
    opacity: 0.2;
    pointer-events: none;
}

.kinetic-vline {
    stroke: #016B61;
    stroke-width: 1px;
    opacity: 0.6;
    pointer-events: none;
}

.kinetic-text {
    font-size: 12px;
    fill: #016B61;
    text-anchor: middle;
    dominant-baseline: middle;
    user-select: none;
    pointer-events: none;
}

.temporary-kinetic-vline,
.temporary-kinetic-text {
    opacity: 0.9;
}

.temporary-kinetic-vline {
    stroke-dasharray: 4 2;
}

.addpointbar {
    stroke: #888;
    stroke-width: 1px;
    stroke-dasharray: 2 2;
}

.addpointhandler {
    fill: #aeb8c2;
    stroke: #98a4b0;
    stroke-width: 2px;
    cursor: pointer;
    transition: fill 120ms ease, stroke 120ms ease;
}

.cursor-addpoint {
    cursor: pointer;
}

.cursor-addpoint:hover .addpointhandler {
    fill: #5b9ad2;
    stroke: #3f84c2;
    stroke-width: 2px;
}

.context-menu {
    position: fixed;
    background: #fff;
    border: 1px solid #ebeef5;
    border-radius: 6px;
    box-shadow: 0 6px 16px rgba(0, 0, 0, 0.12), 0 3px 6px rgba(0, 0, 0, 0.08);
    z-index: 1000;
    min-width: 120px;
    padding: 4px 0;
}

.context-menu-item {
    padding: 8px 14px;
    font-size: 14px;
    color: #606266;
    cursor: pointer;
}

.context-menu-item:hover {
    background: #f5f7fa;
    color: #409eff;
}
</style>
