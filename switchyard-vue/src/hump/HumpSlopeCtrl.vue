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
                        fill="url(#resistanceShadeGradient)"></polygon>
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
                        <circle class="addpointhandler" :cx="getX(cursorX)" :cy="svgHeight - marginBottom / 2" />
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
                <g class="slopelines">
                    <line v-for="seg in slopeLayout?.positionSegmentList || []" class="slope-line"
                        :x1="getX(getPositionX(seg.startPositionID))" :y1="getY(getPositionHeight(seg.startPositionID))"
                        :x2="getX(getPositionX(seg.endPositionID))" :y2="getY(getPositionHeight(seg.endPositionID))" />
                </g>
                <g class="points">
                    <g v-for="pos in slopeLayout?.positionList || []"
                        @contextmenu.prevent="openContextMenu(pos, $event)">
                        <circle class="point-circle" :cx="getX(pos.x)" :cy="getY(pos.height)" r="4"
                            :class="{ 'point-circle-longpress': longPressActivatedId === pos.id, 'point-circle-dragging': draggingId === pos.id }"
                            @mousedown="startDrag(pos, $event)" @touchstart.prevent="startTouchDrag(pos, $event)">
                        </circle>
                        <text v-if="showPointHeightNumber" class="point-height-text" :x="getX(pos.x)"
                            :y="(textPositions.get(pos.id)?.y ?? (getY(pos.height) - 10))">{{ pos.height }}m</text>
                        <line
                            v-if="showPointHeightNumber && Math.abs(getY(pos.height) - (textPositions.get(pos.id)?.y ?? (getY(pos.height) - 10))) >= 15"
                            class="point-line" :x1="getX(pos.x)"
                            :y1="textPositions.get(pos.id)?.barStartY ?? (getY(pos.height) - 10)" :x2="getX(pos.x)"
                            :y2="(textPositions.get(pos.id)?.barEndY ?? (getY(pos.height) - 10))"></line>
                    </g>
                </g>
                <g class="guide-lines" v-if="draggingId">
                    <line v-if="dragMode === 'horizontal'" class="guide-line horizontal" :x1="marginLeft"
                        :y1="getY(currentHeight)" :x2="marginLeft + sketchWidth" :y2="getY(currentHeight)" />
                    <line v-if="dragMode === 'vertical'" class="guide-line vertical" :x1="getX(currentX)"
                        :y1="marginTop" :x2="getX(currentX)" :y2="svgHeight - marginBottom" />
                </g>
                <g v-if="props.elementVisibility?.resistance" class="resistance-energy-height">
                    <polyline :points="resistancePoints" class="resistance-line" />
                    <g v-for="dataPoint in resistanceEnergyHeightData || []">
                        <circle class="resistance-circle" :cx="getX(dataPoint.x)"
                            :cy="getY(orgKineticEnergyY - dataPoint.height)" r="4" />
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
                            dataPoint.result.kineticEnergyHeight
                        }}m({{ dataPoint.result.velocity }}m/s)</text>
                </g>
                <g class="cursor">
                    <line class="cursor-vline" :y1="marginTop" :y2="svgHeight - marginBottom" :x1="getX(cursorX)"
                        :x2="getX(cursorX)"></line>
                </g>
            </svg>
        </div>
        <div v-if="contextMenu.visible" class="context-menu"
            :style="{ left: contextMenu.x + 'px', top: contextMenu.y + 'px' }">
            <div class="context-menu-item" @click.stop="deleteContextPos">Delete Node</div>
        </div>
        <el-dialog v-model="showRetarderStatusDialog" :title="t('humpSlopeCtrl.dialog.retarderSettings')" width="420px"
            :close-on-click-modal="false" append-to-body>
            <div v-if="editingRetarderStatus">
                <div style="margin-bottom: 10px;">{{ t('humpSlopeCtrl.labels.retarderID') }}: {{ editingRetarderStatus.retarderID }}</div>
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
                    <span style="display:inline-block; width: 80px;">{{ t('humpSlopeCtrl.labels.totalEnergyHeight') }}</span>
                    <el-input-number v-model="editingRetarderStatus.totalEnergyHeight" :min="0" :step="0.01"
                        :precision="3" />
                </div>
            </div>
            <template #footer>
                <el-button @click="showRetarderStatusDialog = false">{{ t('common.buttons.cancel') }}</el-button>
                <el-button type="primary" @click="saveRetarderStatusDialog">{{ t('common.buttons.confirm') }}</el-button>
            </template>
        </el-dialog>
    </div>
</template>
<script setup lang="ts">
import { CurveDirections, FlatLayout, LocationParam, SlopeLayout, VPosition, VPositionSegment } from './humplayoutctrl';
import { ref, computed, onBeforeUnmount, onMounted } from 'vue';
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

const props = defineProps<{
    flatLayout?: FlatLayout | null
    slopeLayout?: SlopeLayout | null
    retarderStatusList?: RetarderStatusItem[] | null
    resistanceEnergyHeightData?: { x: number, height: number }[] | null
    kineticEnergyHeightData?: { x: number, result: any }[] | null
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
    }
    g_?: number
    globalCursorX?: number
}>()

const emit = defineEmits<{
    updateGlobalCursorX: [value: number]
    'horizontal-scroll': [scrollLeft: number]
    'wheel-scale-x': [payload: { scaleX: number, scrollLeft: number }]
    'update-retarder-status-list': [value: RetarderStatusItem[]]
}>()
const { t } = useI18n();
const scrollContainerRef = ref<HTMLDivElement | null>(null);
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
const touchLongPressDelay = 550;
const touchMoveThreshold = 8;
const longPressActivatedId = ref<string | null>(null);
const touchLongPressTriggered = ref(false);
const contextMenu = ref<{ visible: boolean; x: number; y: number; posId: string }>({ visible: false, x: 0, y: 0, posId: '' });

function clearTouchLongPressTimer() {
    if (touchLongPressTimer.value) {
        clearTimeout(touchLongPressTimer.value);
        touchLongPressTimer.value = null;
    }
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
    longPressActivatedId.value = null;
    touchLongPressTriggered.value = false;
    touchStartClientX.value = touch.clientX;
    touchStartClientY.value = touch.clientY;
    touchCurrentClientX.value = touch.clientX;
    touchCurrentClientY.value = touch.clientY;
    clearTouchLongPressTimer();
    touchLongPressTimer.value = setTimeout(() => {
        if (!draggingId.value || dragMode.value !== 'vertical') return;
        const target = props.slopeLayout?.positionList?.find(p => p.id === draggingId.value);
        if (!target) return;
        dragMode.value = 'horizontal';
        longPressActivatedId.value = target.id;
        touchLongPressTriggered.value = true;
        startMouseX.value = touchCurrentClientX.value;
        startX.value = target.x;
        clearTouchLongPressTimer();
    }, touchLongPressDelay);
    window.addEventListener('touchmove', onTouchMove, { passive: false });
    window.addEventListener('touchend', endTouchDrag);
    window.addEventListener('touchcancel', endTouchDrag);
}

function beginDrag(pos: { id: string; height: number; x: number }, clientX: number, clientY: number, isHorizontal: boolean) {
    draggingId.value = pos.id;
    currentX.value = pos.x;
    currentHeight.value = pos.height;
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
        const newHeight = startHeight.value - deltaY / scaleY.value;
        target.height = Math.round(Math.max(0, newHeight) * 1000) / 1000;
        currentHeight.value = target.height;
    } else if (dragMode.value === 'horizontal') {
        const deltaX = event.clientX - startMouseX.value;
        const newX = startX.value + deltaX / scaleX.value;
        target.x = Math.round(Math.max(0, newX) * 1000) / 1000;
        currentX.value = target.x;
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

    if (dragMode.value === 'vertical' && touchLongPressTimer.value) {
        const movedX = Math.abs(touch.clientX - touchStartClientX.value);
        const movedY = Math.abs(touch.clientY - touchStartClientY.value);
        if (movedX > touchMoveThreshold || movedY > touchMoveThreshold) {
            clearTouchLongPressTimer();
        }
    }

    if (dragMode.value === 'vertical') {
        const deltaY = touch.clientY - startMouseY.value;
        const newHeight = startHeight.value - deltaY / scaleY.value;
        const target = props.slopeLayout?.positionList?.find(p => p.id === draggingId.value);
        if (!target) return;
        target.height = Math.round(Math.max(0, newHeight) * 1000) / 1000;
        currentHeight.value = target.height;
    } else {
        const deltaX = touch.clientX - startMouseX.value;
        const newX = startX.value + deltaX / scaleX.value;
        const target = props.slopeLayout?.positionList?.find(p => p.id === draggingId.value);
        if (!target) return;
        target.x = Math.round(Math.max(0, newX) * 1000) / 1000;
        currentX.value = target.x;
    }

    updateKineticEnergyHeights(draggingId.value);
    const target = props.slopeLayout?.positionList?.find(p => p.id === draggingId.value);
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
    longPressActivatedId.value = null;
    window.removeEventListener('mousemove', onMouseMove);
    window.removeEventListener('mouseup', endDrag);
    window.removeEventListener('touchmove', onTouchMove);
    window.removeEventListener('touchend', endTouchDrag);
    window.removeEventListener('touchcancel', endTouchDrag);
    // updateKineticEnergyHeights(finishedId);
    draggingId.value = null;
}

function endTouchDrag(event: TouchEvent) {
    const shouldOpenContextMenu = touchLongPressTriggered.value;
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

            kineticResultPos.result.kineticEnergyHeight = Math.round((orgKineticEnergyHeight + (humpHeight - gravitationHeight) - resistanceHeight - breakingHeight) * 1000) / 1000;
            kineticResultPos.result.velocity = Math.round(Math.sqrt(2 * (props.g_ ?? 9.81) * kineticResultPos.result.kineticEnergyHeight) * 1000) / 1000;
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
        const text = `${dataPoint.result.kineticEnergyHeight}m(${dataPoint.result.velocity}m/s)`;
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
    const sl = props.slopeLayout;
    if (!sl || !Array.isArray(sl.positionList)) return;
    sl.positionList = sl.positionList.filter(p => p.id !== posId).sort((a, b) => a.x - b.x);
    sl.positionSegmentList = buildSegments(sl.positionList);
}

function deleteContextPos() {
    if (contextMenu.value.posId) {
        removePosition(contextMenu.value.posId);
    }
    closeContextMenu();
}

function addVPosition(posX: number) {
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
        return;
    }
    else if (positions.length === 1) {
        const newPos1 = new VPosition(createTempVPositionId(), posX, 2);
        const updated = [...positions, newPos1].sort((a, b) => a.x - b.x);
        sl.positionList = updated;
        sl.positionSegmentList = buildSegments(updated);
        return;
    }

    const left = positions.filter((p) => p.x <= posX).pop();
    const right = positions.find((p) => p.x >= posX);

    let height = 0;
    if (left && right && left !== right) {
        height = (left.height + right.height) / 2;
    } else if (left) {
        height = left.height;
    } else if (right) {
        height = right.height;
    }

    const newPos = new VPosition(createTempVPositionId(), posX, Math.round(height * 1000) / 1000);
    const updated = [...positions, newPos].sort((a, b) => a.x - b.x);
    sl.positionList = updated;
    sl.positionSegmentList = buildSegments(updated);
}

onMounted(() => {
    addCursorXListener();
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
}

.points .point-circle.point-circle-longpress,
.points .point-circle.point-circle-dragging {
    transform: scale(1.8);
}

.slope-line {
    stroke: #C2A68C;
    stroke-width: 3px;
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
}

.point-height-text {
    font-size: 12px;
    fill: darkred;
    text-anchor: middle;
    user-select: none;
}

.point-line {
    stroke: darkred;
    stroke-width: 1px;
    opacity: 0.5;
}

.resistance-circle {
    stroke: #4988C4;
    stroke-width: 1.5px;
    fill: white;
}

.resistance-text {
    font-size: 12px;
    fill: #4988C4;
    text-anchor: middle;
    dominant-baseline: middle;
    user-select: none;
}

.resistance-line {
    stroke: #4988C4;
    stroke-width: 2px;
    fill: none;
}

.resistance-shade {
    /* fill: rgba(0, 0, 255, 0.2); */
    stroke: none;
}

.init-kinetic-energy-line {
    stroke: #016B61;
    stroke-width: 1px;
    stroke-dasharray: 4 2;
    fill: none;
}

.resistance-vline {
    stroke: #4988C4;
    stroke-width: 1px;
    opacity: 0.2;
}

.kinetic-vline {
    stroke: #016B61;
    stroke-width: 1px;
    opacity: 0.6;
}

.kinetic-text {
    font-size: 12px;
    fill: #016B61;
    text-anchor: middle;
    dominant-baseline: middle;
    user-select: none;
}

.addpointbar {
    stroke: #888;
    stroke-width: 1px;
    stroke-dasharray: 2 2;
}

.addpointhandler {
    r: 5;
    fill: #aeb8c2;
    stroke: #98a4b0;
    stroke-width: 2px;
    cursor: pointer;
    transition: r 120ms ease, fill 120ms ease, stroke 120ms ease;
}

.cursor-addpoint {
    cursor: pointer;
}

.cursor-addpoint:hover .addpointhandler {
    r: 8;
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
