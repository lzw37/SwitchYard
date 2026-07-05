<script setup>
import { computed, nextTick, onBeforeUnmount, onMounted, ref } from "vue";
import {
    DEFAULT_BUFFER_STOP_DIRECTION,
    DEFAULT_BUFFER_STOP_TYPE,
    getBufferStopStyleAsset,
    normalizeBufferStopDirection,
    normalizeBufferStopType,
} from "@/assets/stationLayoutBufferStopStyles";
import { DEFAULT_SIGNAL_TYPE, getSignalStyleAsset, normalizeSignalType } from "@/assets/stationLayoutSignalStyles";

const props = defineProps({
    width: { type: Number, default: 1920 },
    height: { type: Number, default: 1080 },
    displayScaleX: { type: Number, default: 1 },
    displayScaleY: { type: Number, default: 1 },
    showCurveArc: { type: Boolean, default: true },
    showNodes: { type: Boolean, default: true },
    showGrid: { type: Boolean, default: true },
    objectSnapDistance: { type: Number, default: 10 },
    displayStyles: { type: Object, default: () => ({}) },
});
const emit = defineEmits(["selected-annotation-change", "selected-equipment-change"]);

const svgRef = ref(null);
const defaultEditorDisplayStyles = {
    switchName: { fontSize: 8, fontFamily: "Arial", fontWeight: "normal", fontStyle: "normal", color: "#ffffff" },
    platformName: { fontSize: 10, fontFamily: "Arial", fontWeight: "normal", fontStyle: "normal", color: "#ffffff" },
    signalName: { fontSize: 8, fontFamily: "Arial", fontWeight: "normal", fontStyle: "normal", color: "#ffffff" },
    lineName: { fontSize: 10, fontFamily: "Arial", fontWeight: "normal", fontStyle: "normal", color: "#ffffff" },
    track: { strokeWidth: 2, color: "#fefded" },
    curve: { strokeWidth: 4, color: "#ffb347" },
    platform: { strokeWidth: 1, color: "#87ceeb" },
    signal: { scale: 0.5 },
    switch: { strokeWidth: 5, color: "#00ffff" },
    node: { radius: 5, color: "#ffffff" },
};

const editModeCode = ref(0);
const drawingObject = ref("l");
const mouseGridSnapModeCode = ref(1);
const mouseObjectSnapModeCode = ref(1);
const snapDistance = computed(() => Math.max(0, toFiniteNumber(props.objectSnapDistance)));
const autoSeparateLineTolerance = ref(10);
const autoMergeNodeTolerance = ref(10);
const defaultCurveRadius = 100;
const curveCornerMinAngle = 90;
const curveCornerMaxAngle = 175;
const curveRadiusLineFitRatio = 0.98;
const linkArrowShape = {
    length: 12,
    gap: 4,
    halfWidth: 5,
    minLength: 4,
    tailLineSpacing: 4,
    tailCircleRadius: 4,
    tailCircleGap: 1,
    tailGap: 3,
};

const grid = { visible: true, verticalSpace: 20, horizontalSpace: 20, originX: 0, originY: 0 };
const cursorParam = ref({ size: 10, barVisible: false, barLength: 100, x: 200, y: 200 });
const signalDirectionKeyViews = [
    { key: "w", label: "w", x: -22, y: -18 },
    { key: "e", label: "e", x: 22, y: -18 },
    { key: "s", label: "s", x: -22, y: 22 },
    { key: "d", label: "d", x: 22, y: 22 },
];
const signalNodeExtraGap = 4;
const anchorParam = { size: 10 };
const safeDisplayScaleX = computed(() => normalizeDisplayScale(props.displayScaleX));
const safeDisplayScaleY = computed(() => normalizeDisplayScale(props.displayScaleY));
const canvasAutoExpandPadding = 240;
const canvasEdgeTriggerMargin = 80;
const canvasElementScreenMargin = 80;
const createDefaultCanvasBounds = () => ({
    minX: 0,
    minY: 0,
    maxX: Math.max(1, toFiniteNumber(props.width)),
    maxY: Math.max(1, toFiniteNumber(props.height)),
});
const canvasBounds = ref(createDefaultCanvasBounds());
const canvasWidth = computed(() => Math.max(1, canvasBounds.value.maxX - canvasBounds.value.minX));
const canvasHeight = computed(() => Math.max(1, canvasBounds.value.maxY - canvasBounds.value.minY));
const svgScreenWidth = computed(() => canvasWidth.value * safeDisplayScaleX.value);
const svgScreenHeight = computed(() => canvasHeight.value * safeDisplayScaleY.value);
const svgStyle = computed(() => ({
    width: `${svgScreenWidth.value}px`,
    height: `${svgScreenHeight.value}px`,
}));
const editorDisplayStyles = computed(() => normalizeDisplayStyles(props.displayStyles));

const latestElementID = ref(0);
const layoutMetadata = ref({});

const tracks = ref([]);
const curves = ref([]);
const nodes = ref([]);
const signals = ref([]);
const insulationJoints = ref([]);
const bufferStops = ref([]);
const platforms = ref([]);
const switches = ref([]);
const annotations = ref([]);

const selectedLineIds = ref(new Set());
const selectedNodeIds = ref(new Set());
const selectedSignalIds = ref(new Set());
const selectedInsulationJointIds = ref(new Set());
const selectedBufferStopIds = ref(new Set());
const selectedSwitchIds = ref(new Set());
const selectedPlatformIds = ref(new Set());
const selectedAnnotationIds = ref(new Set());

const crossPoints = ref([]);
const perpendicularPoint = ref(null);

const tempLine = ref(null);
const tempSignal = ref({ visible: false, direction: "w", x: 0, y: 0, type: DEFAULT_SIGNAL_TYPE });
const tempInsulationJoint = ref({ visible: false, x: 0, y: 0 });
const tempBufferStop = ref({ visible: false, direction: DEFAULT_BUFFER_STOP_DIRECTION, type: DEFAULT_BUFFER_STOP_TYPE, x: 0, y: 0 });
const tempNode = ref({ visible: false, x: 0, y: 0 });
const tempPlatformPosition = ref(null);
const selectionBox = ref(null);
const selectionBoxDragThreshold = 4;
const annotationTextAnchorRadius = 5;

const movingAnchor = ref(null);
const nodeInteraction = ref(null);
const annotationInteraction = ref(null);

const finishedCmdList = ref([]);
const revokedCmdList = ref([]);

const selectionBoxView = computed(() => {
    if (!selectionBox.value) {
        return { visible: false, x: 0, y: 0, width: 0, height: 0 };
    }

    const rect = normalizeSelectionBox(selectionBox.value);
    return {
        visible: isSelectionBoxLarge(selectionBox.value),
        x: screenX(rect.minX),
        y: screenY(rect.minY),
        width: screenDeltaX(rect.maxX - rect.minX),
        height: screenDeltaY(rect.maxY - rect.minY),
    };
});

function normalizeDisplayScale(value) {
    const parsed = Number(value);
    if (!Number.isFinite(parsed) || parsed <= 0) return 1;
    return parsed;
}

function clampDisplayNumber(value, fallback, min, max) {
    const parsed = Number(value);
    if (!Number.isFinite(parsed)) return fallback;
    return Math.min(max, Math.max(min, parsed));
}

function normalizeDisplayTextStyle(source, fallback) {
    const style = source && typeof source === "object" ? source : {};
    return {
        fontSize: clampDisplayNumber(style.fontSize, fallback.fontSize, 6, 96),
        fontFamily: String(style.fontFamily || fallback.fontFamily),
        fontWeight: String(style.fontWeight || fallback.fontWeight),
        fontStyle: String(style.fontStyle || fallback.fontStyle),
        color: String(style.color || fallback.color),
    };
}

function normalizeDisplayStyles(source) {
    const styles = source && typeof source === "object" ? source : {};
    return {
        switchName: normalizeDisplayTextStyle(styles.switchName, defaultEditorDisplayStyles.switchName),
        platformName: normalizeDisplayTextStyle(styles.platformName, defaultEditorDisplayStyles.platformName),
        signalName: normalizeDisplayTextStyle(styles.signalName, defaultEditorDisplayStyles.signalName),
        lineName: normalizeDisplayTextStyle(styles.lineName, defaultEditorDisplayStyles.lineName),
        track: {
            strokeWidth: clampDisplayNumber(styles.track?.strokeWidth, defaultEditorDisplayStyles.track.strokeWidth, 0.5, 24),
            color: String(styles.track?.color || defaultEditorDisplayStyles.track.color),
        },
        curve: {
            strokeWidth: clampDisplayNumber(styles.curve?.strokeWidth, defaultEditorDisplayStyles.curve.strokeWidth, 0.5, 24),
            color: String(styles.curve?.color || defaultEditorDisplayStyles.curve.color),
        },
        platform: {
            strokeWidth: clampDisplayNumber(styles.platform?.strokeWidth, defaultEditorDisplayStyles.platform.strokeWidth, 0.5, 24),
            color: String(styles.platform?.color || defaultEditorDisplayStyles.platform.color),
        },
        signal: {
            scale: clampDisplayNumber(styles.signal?.scale, defaultEditorDisplayStyles.signal.scale, 0.2, 3),
        },
        switch: {
            strokeWidth: clampDisplayNumber(styles.switch?.strokeWidth, defaultEditorDisplayStyles.switch.strokeWidth, 1, 32),
            color: String(styles.switch?.color || defaultEditorDisplayStyles.switch.color),
        },
        node: {
            radius: clampDisplayNumber(styles.node?.radius, defaultEditorDisplayStyles.node.radius, 1, 48),
            color: String(styles.node?.color || defaultEditorDisplayStyles.node.color),
        },
    };
}

function toFiniteNumber(value) {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : 0;
}

function screenX(value) {
    return (toFiniteNumber(value) - canvasBounds.value.minX) * safeDisplayScaleX.value;
}

function screenY(value) {
    return (toFiniteNumber(value) - canvasBounds.value.minY) * safeDisplayScaleY.value;
}

function screenDeltaX(value) {
    return toFiniteNumber(value) * safeDisplayScaleX.value;
}

function screenDeltaY(value) {
    return toFiniteNumber(value) * safeDisplayScaleY.value;
}

function screenCenterX(origin, size) {
    return screenX(toFiniteNumber(origin) + toFiniteNumber(size) / 2);
}

function screenCenterY(origin, size) {
    return screenY(toFiniteNumber(origin) + toFiniteNumber(size) / 2);
}

function dataX(value) {
    return toFiniteNumber(value) / safeDisplayScaleX.value + canvasBounds.value.minX;
}

function dataY(value) {
    return toFiniteNumber(value) / safeDisplayScaleY.value + canvasBounds.value.minY;
}

function resetCanvasBounds() {
    const bounds = createDefaultCanvasBounds();
    const stepX = canvasStepX();
    const stepY = canvasStepY();
    canvasBounds.value = {
        minX: alignCanvasMin(bounds.minX, stepX, canvasOriginX()),
        minY: alignCanvasMin(bounds.minY, stepY, canvasOriginY()),
        maxX: alignCanvasMax(bounds.maxX, stepX, canvasOriginX()),
        maxY: alignCanvasMax(bounds.maxY, stepY, canvasOriginY()),
    };
}

function alignCanvasMin(value, step, origin = 0) {
    const safeStep = Math.max(1, toFiniteNumber(step));
    const safeOrigin = toFiniteNumber(origin);
    return Math.floor((toFiniteNumber(value) - safeOrigin) / safeStep) * safeStep + safeOrigin;
}

function alignCanvasMax(value, step, origin = 0) {
    const safeStep = Math.max(1, toFiniteNumber(step));
    const safeOrigin = toFiniteNumber(origin);
    return Math.ceil((toFiniteNumber(value) - safeOrigin) / safeStep) * safeStep + safeOrigin;
}

function canvasStepX() {
    return Math.max(1, toFiniteNumber(grid.verticalSpace) || 1);
}

function canvasStepY() {
    return Math.max(1, toFiniteNumber(grid.horizontalSpace) || 1);
}

function canvasOriginX() {
    return toFiniteNumber(grid.originX);
}

function canvasOriginY() {
    return toFiniteNumber(grid.originY);
}

function normalizeGridOrigin(value, step) {
    const safeStep = Math.max(1, toFiniteNumber(step));
    const remainder = ((toFiniteNumber(value) % safeStep) + safeStep) % safeStep;
    return roundLayoutNumber(remainder);
}

function resetGridOrigin() {
    grid.originX = 0;
    grid.originY = 0;
}

function getCanvasScrollContainer() {
    return svgRef.value?.parentElement || null;
}

function adjustCanvasScrollAfterExpansion(deltaLeft, deltaTop) {
    if (deltaLeft <= 0 && deltaTop <= 0) return;
    const scrollContainer = getCanvasScrollContainer();
    if (!scrollContainer) return;
    nextTick(() => {
        if (deltaLeft > 0) scrollContainer.scrollLeft += deltaLeft;
        if (deltaTop > 0) scrollContainer.scrollTop += deltaTop;
    });
}

function expandCanvasToIncludeRect(rect, options = {}) {
    if (!rect) return false;
    const minX = Math.min(toFiniteNumber(rect.minX), toFiniteNumber(rect.maxX));
    const minY = Math.min(toFiniteNumber(rect.minY), toFiniteNumber(rect.maxY));
    const maxX = Math.max(toFiniteNumber(rect.minX), toFiniteNumber(rect.maxX));
    const maxY = Math.max(toFiniteNumber(rect.minY), toFiniteNumber(rect.maxY));
    const triggerMargin = Math.max(0, toFiniteNumber(options.triggerMargin));
    const padding = Math.max(0, toFiniteNumber(options.padding ?? canvasAutoExpandPadding));
    const bounds = canvasBounds.value;
    const stepX = canvasStepX();
    const stepY = canvasStepY();

    let nextMinX = bounds.minX;
    let nextMinY = bounds.minY;
    let nextMaxX = bounds.maxX;
    let nextMaxY = bounds.maxY;

    if (minX < bounds.minX + triggerMargin) nextMinX = alignCanvasMin(minX - padding, stepX, canvasOriginX());
    if (minY < bounds.minY + triggerMargin) nextMinY = alignCanvasMin(minY - padding, stepY, canvasOriginY());
    if (maxX > bounds.maxX - triggerMargin) nextMaxX = alignCanvasMax(maxX + padding, stepX, canvasOriginX());
    if (maxY > bounds.maxY - triggerMargin) nextMaxY = alignCanvasMax(maxY + padding, stepY, canvasOriginY());

    if (nextMinX === bounds.minX && nextMinY === bounds.minY && nextMaxX === bounds.maxX && nextMaxY === bounds.maxY) {
        return false;
    }

    const deltaLeft = Math.max(0, (bounds.minX - nextMinX) * safeDisplayScaleX.value);
    const deltaTop = Math.max(0, (bounds.minY - nextMinY) * safeDisplayScaleY.value);
    canvasBounds.value = {
        minX: nextMinX,
        minY: nextMinY,
        maxX: nextMaxX,
        maxY: nextMaxY,
    };
    adjustCanvasScrollAfterExpansion(deltaLeft, deltaTop);
    return true;
}

function expandCanvasToIncludePoint(point, options = {}) {
    if (!point) return false;
    const x = toFiniteNumber(point.x);
    const y = toFiniteNumber(point.y);
    return expandCanvasToIncludeRect({ minX: x, minY: y, maxX: x, maxY: y }, options);
}

function anchorScreenX(anchor) {
    return screenX(toFiniteNumber(anchor.x) + anchorParam.size / 2) - anchorParam.size / 2;
}

function anchorScreenY(anchor) {
    return screenY(toFiniteNumber(anchor.y) + anchorParam.size / 2) - anchorParam.size / 2;
}

function normalizePosition(position) {
    return {
        x: toFiniteNumber(position?.x),
        y: toFiniteNumber(position?.y),
    };
}

function normalizeAnnotation(annotation) {
    return {
        id: annotation?.id ?? nextId(),
        text: annotation?.text ?? "Annotation",
        position: normalizePosition(annotation?.position),
        fontFamily: annotation?.fontFamily || "Arial",
        fontSize: toFiniteNumber(annotation?.fontSize) || 16,
        fontWeight: annotation?.fontWeight || "normal",
        fontStyle: annotation?.fontStyle || "normal",
        angle: toFiniteNumber(annotation?.angle),
        textColor: annotation?.textColor || "#ffffff",
    };
}

function buildDefaultAnnotation(x, y) {
    return normalizeAnnotation({
        id: nextId(),
        text: "Annotation",
        position: { x, y },
    });
}

function normalizeCurve(curve) {
    return {
        id: curve?.id ?? nextId(),
        nodeID: curve?.nodeID ?? curve?.vertexNodeID ?? "",
        tangentLinkID1: curve?.tangentLinkID1 ?? curve?.linkID1 ?? "",
        tangentLinkID2: curve?.tangentLinkID2 ?? curve?.linkID2 ?? "",
        radius: toFiniteNumber(curve?.radius) || defaultCurveRadius,
        angle: toFiniteNumber(curve?.angle),
        tangentDistance: toFiniteNumber(curve?.tangentDistance),
        start: normalizePosition(curve?.start ?? { x: curve?.startX, y: curve?.startY }),
        end: normalizePosition(curve?.end ?? { x: curve?.endX, y: curve?.endY }),
        center: normalizePosition(curve?.center ?? { x: curve?.centerX, y: curve?.centerY }),
        largeArcFlag: Number(curve?.largeArcFlag) === 1 ? 1 : 0,
        sweepFlag: Number(curve?.sweepFlag) === 1 ? 1 : 0,
    };
}

function getSelectedAnnotation() {
    if (selectedAnnotationIds.value.size !== 1) return null;
    const [selectedId] = [...selectedAnnotationIds.value];
    return annotations.value.find((annotation) => annotation.id === selectedId) || null;
}

function getAnnotationSnapshot(annotation) {
    if (!annotation) return null;
    return JSON.parse(JSON.stringify(annotation));
}

function emitSelectedAnnotationChange() {
    emit("selected-annotation-change", getAnnotationSnapshot(getSelectedAnnotation()));
}

function getEquipmentCollection(kind) {
    if (kind === "link") return tracks.value;
    if (kind === "signal") return signals.value;
    if (kind === "insulationJoint") return insulationJoints.value;
    if (kind === "bufferStop") return bufferStops.value;
    if (kind === "switch") return switches.value;
    if (kind === "platform") return platforms.value;
    return [];
}

function getEquipmentSelectedSet(kind) {
    if (kind === "link") return selectedLineIds.value;
    if (kind === "signal") return selectedSignalIds.value;
    if (kind === "insulationJoint") return selectedInsulationJointIds.value;
    if (kind === "bufferStop") return selectedBufferStopIds.value;
    if (kind === "switch") return selectedSwitchIds.value;
    if (kind === "platform") return selectedPlatformIds.value;
    return new Set();
}

function cloneEquipment(kind, equipment) {
    if (!equipment) return null;
    return {
        kind,
        id: equipment.id,
        data: JSON.parse(JSON.stringify(equipment)),
    };
}

function getSelectedEquipment() {
    const selected = [];
    for (const kind of ["link", "signal", "insulationJoint", "bufferStop", "switch", "platform"]) {
        const selectedIds = [...getEquipmentSelectedSet(kind)];
        if (selectedIds.length !== 1) {
            if (selectedIds.length > 1) return null;
            continue;
        }

        const equipment = getEquipmentCollection(kind).find((item) => item.id === selectedIds[0]);
        if (equipment) selected.push(cloneEquipment(kind, equipment));
    }

    return selected.length === 1 ? selected[0] : null;
}

function emitSelectedEquipmentChange() {
    emit("selected-equipment-change", getSelectedEquipment());
}

function clearSelectedDeviceIds() {
    selectedSignalIds.value = new Set();
    selectedInsulationJointIds.value = new Set();
    selectedBufferStopIds.value = new Set();
    selectedSwitchIds.value = new Set();
    selectedPlatformIds.value = new Set();
}

function setSelectedAnnotationIds(ids) {
    selectedAnnotationIds.value = new Set(ids);
    emitSelectedAnnotationChange();
}

function updateSelectedAnnotation(patch) {
    const selected = getSelectedAnnotation();
    if (!selected) return;

    executeMutation(() => {
        const target = annotations.value.find((annotation) => annotation.id === selected.id);
        if (!target) return;
        if (patch.position) {
            target.position = {
                ...target.position,
                ...patch.position,
            };
        }
        Object.assign(target, { ...patch, position: target.position });
        annotations.value = annotations.value.map((annotation) => annotation.id === target.id ? normalizeAnnotation(target) : annotation);
    });
    emitSelectedAnnotationChange();
}

function annotationTransform(annotation) {
    return `translate(${screenX(annotation.position?.x)},${screenY(annotation.position?.y)}) rotate(${toFiniteNumber(annotation.angle)})`;
}

function roundLayoutNumber(value) {
    return Math.round(toFiniteNumber(value) * 1000) / 1000;
}

function shouldShowAnnotationControls(annotation) {
    return selectedAnnotationIds.value.size === 1 && isAnnotationSelected(annotation.id);
}

function normalizeSelectionBox(box) {
    return {
        minX: Math.min(toFiniteNumber(box.startX), toFiniteNumber(box.endX)),
        minY: Math.min(toFiniteNumber(box.startY), toFiniteNumber(box.endY)),
        maxX: Math.max(toFiniteNumber(box.startX), toFiniteNumber(box.endX)),
        maxY: Math.max(toFiniteNumber(box.startY), toFiniteNumber(box.endY)),
    };
}

function isSelectionBoxLarge(box) {
    return (
        Math.abs(screenDeltaX(toFiniteNumber(box.endX) - toFiniteNumber(box.startX))) >= selectionBoxDragThreshold ||
        Math.abs(screenDeltaY(toFiniteNumber(box.endY) - toFiniteNumber(box.startY))) >= selectionBoxDragThreshold
    );
}

const anchorRects = computed(() => {
    const list = [];
    for (const line of tracks.value) {
        if (!selectedLineIds.value.has(line.id)) continue;
        const half = anchorParam.size / 2;
        const sp = { id: `sp${line.id}`, lineId: line.id, type: "sp", x: Number(line.x1) - half, y: Number(line.y1) - half };
        const ep = { id: `ep${line.id}`, lineId: line.id, type: "ep", x: Number(line.x2) - half, y: Number(line.y2) - half };
        list.push(sp, ep);
    }
    return list;
});

const gridDots = computed(() => {
    const dots = [];
    if (!props.showGrid || !grid.visible) return dots;
    const stepX = canvasStepX();
    const stepY = canvasStepY();
    const bounds = canvasBounds.value;
    const startX = alignCanvasMin(bounds.minX, stepX, canvasOriginX());
    const startY = alignCanvasMin(bounds.minY, stepY, canvasOriginY());
    const endX = alignCanvasMax(bounds.maxX, stepX, canvasOriginX());
    const endY = alignCanvasMax(bounds.maxY, stepY, canvasOriginY());
    for (let x = startX; x <= endX; x += stepX) {
        for (let y = startY; y <= endY; y += stepY) {
            dots.push({ x, y });
        }
    }
    return dots;
});

function createEmptyCanvasContentRect() {
    return {
        minX: Infinity,
        minY: Infinity,
        maxX: -Infinity,
        maxY: -Infinity,
    };
}

function isCanvasContentRectEmpty(rect) {
    return !Number.isFinite(rect.minX) || !Number.isFinite(rect.minY) ||
        !Number.isFinite(rect.maxX) || !Number.isFinite(rect.maxY);
}

function includeDataRectInCanvasContentRect(rect, minX, minY, maxX, maxY, screenMargin = 0) {
    const rectMinX = Number(minX);
    const rectMinY = Number(minY);
    const rectMaxX = Number(maxX);
    const rectMaxY = Number(maxY);
    if (![rectMinX, rectMinY, rectMaxX, rectMaxY].every(Number.isFinite)) return;

    const marginX = Math.max(0, screenMargin) / safeDisplayScaleX.value;
    const marginY = Math.max(0, screenMargin) / safeDisplayScaleY.value;
    rect.minX = Math.min(rect.minX, Math.min(rectMinX, rectMaxX) - marginX);
    rect.minY = Math.min(rect.minY, Math.min(rectMinY, rectMaxY) - marginY);
    rect.maxX = Math.max(rect.maxX, Math.max(rectMinX, rectMaxX) + marginX);
    rect.maxY = Math.max(rect.maxY, Math.max(rectMinY, rectMaxY) + marginY);
}

function includePointInCanvasContentRect(rect, point, screenMargin = canvasElementScreenMargin) {
    if (!point) return;
    includeDataRectInCanvasContentRect(rect, point.x, point.y, point.x, point.y, screenMargin);
}

function includePositionInCanvasContentRect(rect, item, screenMargin = canvasElementScreenMargin) {
    includePointInCanvasContentRect(rect, item?.position, screenMargin);
}

function buildCanvasContentRect(screenMargin = canvasElementScreenMargin) {
    const rect = createEmptyCanvasContentRect();

    for (const line of tracks.value) {
        includeDataRectInCanvasContentRect(rect, line.x1, line.y1, line.x2, line.y2, screenMargin);
    }
    for (const curve of curves.value) {
        includePointInCanvasContentRect(rect, curve.start, screenMargin);
        includePointInCanvasContentRect(rect, curve.end, screenMargin);
    }
    for (const node of nodes.value) {
        includePointInCanvasContentRect(rect, node, screenMargin);
    }
    for (const signal of signals.value) {
        includePositionInCanvasContentRect(rect, signal, screenMargin);
    }
    for (const insulationJoint of insulationJoints.value) {
        includePositionInCanvasContentRect(rect, insulationJoint, screenMargin);
    }
    for (const bufferStop of bufferStops.value) {
        includePositionInCanvasContentRect(rect, bufferStop, screenMargin);
    }
    for (const platform of platforms.value) {
        includeDataRectInCanvasContentRect(
            rect,
            platform.x,
            platform.y,
            toFiniteNumber(platform.x) + toFiniteNumber(platform.width),
            toFiniteNumber(platform.y) + toFiniteNumber(platform.height),
            screenMargin
        );
    }
    for (const sw of switches.value) {
        includePositionInCanvasContentRect(rect, sw, screenMargin);
    }
    for (const annotation of annotations.value) {
        includePositionInCanvasContentRect(rect, annotation, screenMargin);
    }

    return rect;
}

function alignGridOriginToCurrentContent() {
    const rect = buildCanvasContentRect(0);
    if (isCanvasContentRectEmpty(rect)) {
        resetGridOrigin();
        return false;
    }

    grid.originX = normalizeGridOrigin(rect.minX, canvasStepX());
    grid.originY = normalizeGridOrigin(rect.minY, canvasStepY());
    return true;
}

function ensureCanvasForAllElements() {
    const rect = buildCanvasContentRect(canvasElementScreenMargin);
    if (isCanvasContentRectEmpty(rect)) return false;
    return expandCanvasToIncludeRect(rect, { padding: canvasAutoExpandPadding });
}

function getLinePointAtRate(line, rate) {
    const x1 = toFiniteNumber(line.x1);
    const y1 = toFiniteNumber(line.y1);
    const x2 = toFiniteNumber(line.x2);
    const y2 = toFiniteNumber(line.y2);
    return {
        x: x1 + (x2 - x1) * rate,
        y: y1 + (y2 - y1) * rate,
    };
}

function getPointRateOnLine(line, point) {
    const x1 = toFiniteNumber(line.x1);
    const y1 = toFiniteNumber(line.y1);
    const dx = toFiniteNumber(line.x2) - x1;
    const dy = toFiniteNumber(line.y2) - y1;
    const lengthSquared = dx * dx + dy * dy;
    if (lengthSquared <= 0) return null;

    const rawRate = ((toFiniteNumber(point?.x) - x1) * dx + (toFiniteNumber(point?.y) - y1) * dy) / lengthSquared;
    if (!Number.isFinite(rawRate)) return null;
    return Math.max(0, Math.min(1, rawRate));
}

function mergeHiddenRateRanges(ranges) {
    const normalizedRanges = ranges
        .map((range) => ({
            start: Math.max(0, Math.min(1, Math.min(range.start, range.end))),
            end: Math.max(0, Math.min(1, Math.max(range.start, range.end))),
        }))
        .filter((range) => range.end - range.start > 0.000001)
        .sort((a, b) => a.start - b.start);

    const merged = [];
    for (const range of normalizedRanges) {
        const previous = merged[merged.length - 1];
        if (previous && range.start <= previous.end + 0.000001) {
            previous.end = Math.max(previous.end, range.end);
        } else {
            merged.push({ ...range });
        }
    }

    return merged;
}

function buildVisibleRateRanges(hiddenRanges) {
    const mergedHiddenRanges = mergeHiddenRateRanges(hiddenRanges);
    const visibleRanges = [];
    let cursor = 0;

    for (const hiddenRange of mergedHiddenRanges) {
        if (hiddenRange.start > cursor + 0.000001) {
            visibleRanges.push({ start: cursor, end: hiddenRange.start });
        }
        cursor = Math.max(cursor, hiddenRange.end);
    }

    if (cursor < 1 - 0.000001) {
        visibleRanges.push({ start: cursor, end: 1 });
    }

    return visibleRanges;
}

function addCurveHiddenRange(hiddenRangesByLineID, lineByID, nodeByID, curve, tangentLinkIDKey, tangentPointKey) {
    const lineID = curve?.[tangentLinkIDKey];
    const line = lineByID.get(lineID);
    const node = nodeByID.get(curve?.nodeID);
    if (!line || !node) return;

    const nodeRate = getPointRateOnLine(line, node);
    const tangentRate = getPointRateOnLine(line, curve?.[tangentPointKey]);
    if (nodeRate == null || tangentRate == null) return;

    if (!hiddenRangesByLineID.has(line.id)) {
        hiddenRangesByLineID.set(line.id, []);
    }
    hiddenRangesByLineID.get(line.id).push({ start: nodeRate, end: tangentRate });
}

const renderedTrackSegments = computed(() => {
    const lineByID = new Map(tracks.value.map((line) => [line.id, line]));
    const nodeByID = new Map(nodes.value.map((node) => [node.id, node]));
    const hiddenRangesByLineID = new Map();

    if (props.showCurveArc) {
        for (const curve of curves.value) {
            addCurveHiddenRange(hiddenRangesByLineID, lineByID, nodeByID, curve, "tangentLinkID1", "start");
            addCurveHiddenRange(hiddenRangesByLineID, lineByID, nodeByID, curve, "tangentLinkID2", "end");
        }
    }

    const segments = [];
    for (const line of tracks.value) {
        const visibleRanges = buildVisibleRateRanges(hiddenRangesByLineID.get(line.id) || []);
        visibleRanges.forEach((range, index) => {
            const start = getLinePointAtRate(line, range.start);
            const end = getLinePointAtRate(line, range.end);
            segments.push({
                id: `${line.id}-visible-${index}`,
                line,
                x1: start.x,
                y1: start.y,
                x2: end.x,
                y2: end.y,
                rateStart: range.start,
                rateEnd: range.end,
            });
        });
    }

    return segments;
});

const displayedCurves = computed(() => props.showCurveArc ? curves.value : []);

const linkArrowViews = computed(() => {
    return tracks.value.flatMap((line) => buildLinkArrowViews(line));
});

const lineNameViews = computed(() => {
    return tracks.value
        .filter((line) => getLineName(line))
        .map((line) => {
            const visibleSegments = renderedTrackSegments.value.filter((segment) => segment.line.id === line.id);
            const labelSegment = visibleSegments
                .map((segment) => ({
                    segment,
                    length: Math.hypot(
                        toFiniteNumber(segment.x2) - toFiniteNumber(segment.x1),
                        toFiniteNumber(segment.y2) - toFiniteNumber(segment.y1)
                    ),
                }))
                .sort((a, b) => b.length - a.length)[0]?.segment;

            if (!labelSegment) return null;

            return {
                id: line.id,
                line,
                x: (toFiniteNumber(labelSegment.x1) + toFiniteNumber(labelSegment.x2)) / 2,
                y: (toFiniteNumber(labelSegment.y1) + toFiniteNumber(labelSegment.y2)) / 2,
            };
        })
        .filter((lineNameView) => lineNameView != null);
});

function cloneState() {
    return JSON.parse(
        JSON.stringify({
            latestElementID: latestElementID.value,
            tracks: tracks.value,
            curves: curves.value,
            nodes: nodes.value,
            signals: signals.value,
            insulationJoints: insulationJoints.value,
            bufferStops: bufferStops.value,
            platforms: platforms.value,
            switches: switches.value,
            annotations: annotations.value,
            selectedLineIds: [...selectedLineIds.value],
            selectedNodeIds: [...selectedNodeIds.value],
            selectedSignalIds: [...selectedSignalIds.value],
            selectedInsulationJointIds: [...selectedInsulationJointIds.value],
            selectedBufferStopIds: [...selectedBufferStopIds.value],
            selectedSwitchIds: [...selectedSwitchIds.value],
            selectedPlatformIds: [...selectedPlatformIds.value],
            selectedAnnotationIds: [...selectedAnnotationIds.value],
        })
    );
}

function applyState(state) {
    latestElementID.value = state.latestElementID;
    tracks.value = state.tracks || [];
    curves.value = (state.curves || []).map((curve) => normalizeCurve(curve));
    nodes.value = state.nodes || [];
    signals.value = (state.signals || []).map((signal) => normalizeNamedEquipment(signal));
    insulationJoints.value = state.insulationJoints || [];
    bufferStops.value = (state.bufferStops || []).map((bufferStop) => normalizeBufferStop(bufferStop));
    platforms.value = (state.platforms || []).map((platform) => normalizeNamedEquipment(platform));
    switches.value = (state.switches || []).map((sw) => normalizeNamedEquipment(sw));
    annotations.value = state.annotations || [];
    syncSignalsToBindingNodes();
    selectedLineIds.value = new Set(state.selectedLineIds || []);
    selectedNodeIds.value = new Set(state.selectedNodeIds || []);
    selectedSignalIds.value = new Set(state.selectedSignalIds || []);
    selectedInsulationJointIds.value = new Set(state.selectedInsulationJointIds || []);
    selectedBufferStopIds.value = new Set(state.selectedBufferStopIds || []);
    selectedSwitchIds.value = new Set(state.selectedSwitchIds || []);
    selectedPlatformIds.value = new Set(state.selectedPlatformIds || []);
    selectedAnnotationIds.value = new Set(state.selectedAnnotationIds || []);
    ensureCanvasForAllElements();
    emitSelectedAnnotationChange();
    emitSelectedEquipmentChange();
}

function executeMutation(mutator) {
    finishedCmdList.value.push(cloneState());
    if (finishedCmdList.value.length > 30) {
        finishedCmdList.value.shift();
    }
    revokedCmdList.value = [];
    mutator();
    ensureCanvasForAllElements();
}

function revoke() {
    if (finishedCmdList.value.length === 0) return;
    revokedCmdList.value.push(cloneState());
    const prev = finishedCmdList.value.pop();
    applyState(prev);
}

function redo() {
    if (revokedCmdList.value.length === 0) return;
    finishedCmdList.value.push(cloneState());
    const next = revokedCmdList.value.pop();
    applyState(next);
}

function nextId() {
    const id = String(latestElementID.value);
    latestElementID.value += 1;
    return id;
}

function normalizeNamedEquipment(equipment) {
    const normalized = { ...(equipment || {}) };
    const id = normalized.id == null ? "" : String(normalized.id).trim();
    const name = normalized.name == null ? "" : String(normalized.name).trim();
    normalized.name = name || id;
    return normalized;
}

function normalizeBufferStop(bufferStop) {
    const normalized = { ...(bufferStop || {}) };
    normalized.direction = normalizeBufferStopDirection(normalized.direction ?? normalized.Direction);
    normalized.type = normalizeBufferStopType(normalized.type ?? normalized.Type ?? normalized.style ?? normalized.Style);
    return normalized;
}

function syncSignalPositionToBindingNode(signal) {
    const bindingNode = getNodeById(signal?.bindingNodeID);
    if (!bindingNode) return signal;

    signal.position = {
        ...(signal.position || {}),
        x: toFiniteNumber(bindingNode.x),
        y: toFiniteNumber(bindingNode.y),
    };
    return signal;
}

function syncSignalsToBindingNodes() {
    for (const signal of signals.value) {
        syncSignalPositionToBindingNode(signal);
    }
}

function getEquipmentDisplayName(equipment, placeholder) {
    const name = equipment?.name == null ? "" : String(equipment.name).trim();
    if (name) return name;
    const id = equipment?.id == null ? "" : String(equipment.id).trim();
    return id || placeholder;
}

function clearSelectedLines() {
    selectedLineIds.value = new Set();
    finishAnchorInteraction();
    emitSelectedEquipmentChange();
}

function clearSelectedNodes() {
    selectedNodeIds.value = new Set();
    finishNodeInteraction();
}

function clearSelectedEquipment() {
    clearSelectedDeviceIds();
    selectedAnnotationIds.value = new Set();
    finishAnnotationInteraction();
    emitSelectedAnnotationChange();
    emitSelectedEquipmentChange();
}

function setEditMode(code) {
    editModeCode.value = Number(code);
}

function setDrawingSignalType(type) {
    tempSignal.value.type = normalizeSignalType(type);
}

function setDrawingBufferStopDirection(direction) {
    tempBufferStop.value.direction = normalizeBufferStopDirection(direction);
}

function setDrawingBufferStopType(type) {
    tempBufferStop.value.type = normalizeBufferStopType(type);
}

function setDrawingObject(obj) {
    drawingObject.value = obj;
    if (obj === "s") {
        startDrawingSignal();
    } else if (obj === "i") {
        startDrawingInsulationJoint();
    } else if (obj === "e") {
        startDrawingBufferStop();
    } else if (obj === "n") {
        startDrawingNode();
    } else if (obj === "p") {
        startDrawingPlatform();
    } else if (obj === "a") {
        cancelSelectionBox();
    }
}

function setMouseGridSnapModeCode(code) {
    mouseGridSnapModeCode.value = Number(code);
}

function setMouseObjectSnapModeCode(code) {
    mouseObjectSnapModeCode.value = Number(code);
}

function clientPointToSvgPoint(clientX, clientY) {
    if (!svgRef.value) return;
    const screenCtm = svgRef.value.getScreenCTM();
    if (!screenCtm) return;

    const point = svgRef.value.createSVGPoint();
    point.x = clientX;
    point.y = clientY;
    return point.matrixTransform(screenCtm.inverse());
}

function clientPointToDataPoint(clientX, clientY) {
    const svgPoint = clientPointToSvgPoint(clientX, clientY);
    if (!svgPoint) return null;
    return {
        x: dataX(svgPoint.x),
        y: dataY(svgPoint.y),
    };
}

function snapPointToGrid(point) {
    const gridX = toFiniteNumber(grid.horizontalSpace) || 1;
    const gridY = toFiniteNumber(grid.verticalSpace) || 1;
    const originX = canvasOriginX();
    const originY = canvasOriginY();
    return {
        x: originX + Math.round((toFiniteNumber(point?.x) - originX) / gridX) * gridX,
        y: originY + Math.round((toFiniteNumber(point?.y) - originY) / gridY) * gridY,
    };
}

function applyGridSnapWhenEnabled(point) {
    if (mouseGridSnapModeCode.value !== 1) return normalizePosition(point);
    return snapPointToGrid(point);
}

function getNodeSnapObjects() {
    return nodes.value
        .map((node) => ({
            x: toFiniteNumber(node.x),
            y: toFiniteNumber(node.y),
            id: node.id,
            kind: "node",
        }))
        .filter((node) => Number.isFinite(node.x) && Number.isFinite(node.y));
}

function findNearestPointSnap(point, candidates, threshold) {
    let nearest = null;
    const sourcePoint = normalizePosition(point);

    for (const candidate of candidates) {
        const candidatePoint = normalizePosition(candidate);
        const dist = Math.hypot(candidatePoint.x - sourcePoint.x, candidatePoint.y - sourcePoint.y);
        if (dist > threshold) continue;
        if (!nearest || dist < nearest.dist) {
            nearest = {
                ...candidate,
                x: candidatePoint.x,
                y: candidatePoint.y,
                dist,
            };
        }
    }

    return nearest;
}

function findNearestNodeSnap(point, threshold) {
    return findNearestPointSnap(point, getNodeSnapObjects(), threshold);
}

function findNearestEdgeSnap(point, threshold) {
    let nearest = null;
    const sourcePoint = normalizePosition(point);

    for (const line of tracks.value) {
        const snapPoint = calculatePointSegmentSnap(line, sourcePoint);
        if (!snapPoint || !Number.isFinite(snapPoint.dist)) continue;
        if (snapPoint.dist > threshold) continue;
        if (!nearest || snapPoint.dist < nearest.dist) {
            nearest = {
                x: roundLayoutNumber(snapPoint.x),
                y: roundLayoutNumber(snapPoint.y),
                dist: snapPoint.dist,
                id: line.id,
                kind: "edge",
            };
        }
    }

    return nearest;
}

function resolveObjectSnapPoint(point) {
    if (mouseObjectSnapModeCode.value !== 1) return null;

    const threshold = snapDistance.value;
    const nodeSnap = findNearestNodeSnap(point, threshold);
    if (nodeSnap) return nodeSnap;

    return findNearestEdgeSnap(point, threshold);
}

function resolveCursorSnapPoint(point) {
    const sourcePoint = normalizePosition(point);
    const objectSnapPoint = resolveObjectSnapPoint(sourcePoint);
    if (objectSnapPoint) return objectSnapPoint;

    const gridPoint = applyGridSnapWhenEnabled(sourcePoint);
    return {
        ...gridPoint,
        kind: mouseGridSnapModeCode.value === 1 ? "grid" : "free",
    };
}

function updateCursorPosition(clientX, clientY) {
    const dataPoint = clientPointToDataPoint(clientX, clientY);
    if (!dataPoint) return;
    const sourcePoint = normalizePosition(dataPoint);
    const snapPoint = resolveCursorSnapPoint(sourcePoint);
    const snapX = snapPoint.x;
    const snapY = snapPoint.y;

    cursorParam.value.x = snapX;
    cursorParam.value.y = snapY;
    if (editModeCode.value !== 0) {
        expandCanvasToIncludePoint({ x: snapX, y: snapY }, { triggerMargin: canvasEdgeTriggerMargin });
    }

    if (editModeCode.value === 1) {
        if (snapPoint.kind === "edge") {
            perpendicularPoint.value = { x: snapX, y: snapY };
        } else {
            snapPointToLine(sourcePoint.x, sourcePoint.y);
        }
    } else {
        perpendicularPoint.value = null;
    }
}

function isPointInRect(point, rect) {
    const x = toFiniteNumber(point.x);
    const y = toFiniteNumber(point.y);
    return x >= rect.minX && x <= rect.maxX && y >= rect.minY && y <= rect.maxY;
}

function crossProduct(a, b, c) {
    return (toFiniteNumber(b.x) - toFiniteNumber(a.x)) * (toFiniteNumber(c.y) - toFiniteNumber(a.y)) -
        (toFiniteNumber(b.y) - toFiniteNumber(a.y)) * (toFiniteNumber(c.x) - toFiniteNumber(a.x));
}

function isPointOnSegment(point, segmentStart, segmentEnd) {
    const tolerance = 1e-6;
    if (Math.abs(crossProduct(segmentStart, segmentEnd, point)) > tolerance) return false;
    return (
        toFiniteNumber(point.x) >= Math.min(toFiniteNumber(segmentStart.x), toFiniteNumber(segmentEnd.x)) - tolerance &&
        toFiniteNumber(point.x) <= Math.max(toFiniteNumber(segmentStart.x), toFiniteNumber(segmentEnd.x)) + tolerance &&
        toFiniteNumber(point.y) >= Math.min(toFiniteNumber(segmentStart.y), toFiniteNumber(segmentEnd.y)) - tolerance &&
        toFiniteNumber(point.y) <= Math.max(toFiniteNumber(segmentStart.y), toFiniteNumber(segmentEnd.y)) + tolerance
    );
}

function doSegmentsIntersect(a, b, c, d) {
    const abC = crossProduct(a, b, c);
    const abD = crossProduct(a, b, d);
    const cdA = crossProduct(c, d, a);
    const cdB = crossProduct(c, d, b);
    const tolerance = 1e-6;

    if (Math.abs(abC) <= tolerance && isPointOnSegment(c, a, b)) return true;
    if (Math.abs(abD) <= tolerance && isPointOnSegment(d, a, b)) return true;
    if (Math.abs(cdA) <= tolerance && isPointOnSegment(a, c, d)) return true;
    if (Math.abs(cdB) <= tolerance && isPointOnSegment(b, c, d)) return true;

    return ((abC > 0 && abD < 0) || (abC < 0 && abD > 0)) && ((cdA > 0 && cdB < 0) || (cdA < 0 && cdB > 0));
}

function doesLineIntersectRect(line, rect) {
    const start = { x: toFiniteNumber(line.x1), y: toFiniteNumber(line.y1) };
    const end = { x: toFiniteNumber(line.x2), y: toFiniteNumber(line.y2) };
    if (isPointInRect(start, rect) || isPointInRect(end, rect)) return true;
    if (Math.max(start.x, end.x) < rect.minX || Math.min(start.x, end.x) > rect.maxX) return false;
    if (Math.max(start.y, end.y) < rect.minY || Math.min(start.y, end.y) > rect.maxY) return false;

    const topLeft = { x: rect.minX, y: rect.minY };
    const topRight = { x: rect.maxX, y: rect.minY };
    const bottomRight = { x: rect.maxX, y: rect.maxY };
    const bottomLeft = { x: rect.minX, y: rect.maxY };

    return (
        doSegmentsIntersect(start, end, topLeft, topRight) ||
        doSegmentsIntersect(start, end, topRight, bottomRight) ||
        doSegmentsIntersect(start, end, bottomRight, bottomLeft) ||
        doSegmentsIntersect(start, end, bottomLeft, topLeft)
    );
}

function normalizeElementRect(x, y, width, height) {
    const left = toFiniteNumber(x);
    const top = toFiniteNumber(y);
    const right = left + toFiniteNumber(width);
    const bottom = top + toFiniteNumber(height);
    return {
        minX: Math.min(left, right),
        minY: Math.min(top, bottom),
        maxX: Math.max(left, right),
        maxY: Math.max(top, bottom),
    };
}

function doRectsIntersect(a, b) {
    return a.minX <= b.maxX && a.maxX >= b.minX && a.minY <= b.maxY && a.maxY >= b.minY;
}

function selectElementsInBox(box) {
    const rect = normalizeSelectionBox(box);
    const nextLineIds = box.additive ? new Set(selectedLineIds.value) : new Set();
    const nextNodeIds = box.additive ? new Set(selectedNodeIds.value) : new Set();
    const nextSignalIds = box.additive ? new Set(selectedSignalIds.value) : new Set();
    const nextInsulationJointIds = box.additive ? new Set(selectedInsulationJointIds.value) : new Set();
    const nextBufferStopIds = box.additive ? new Set(selectedBufferStopIds.value) : new Set();
    const nextSwitchIds = box.additive ? new Set(selectedSwitchIds.value) : new Set();
    const nextPlatformIds = box.additive ? new Set(selectedPlatformIds.value) : new Set();
    const nextAnnotationIds = box.additive ? new Set(selectedAnnotationIds.value) : new Set();

    for (const line of tracks.value) {
        if (doesLineIntersectRect(line, rect)) nextLineIds.add(line.id);
    }
    for (const node of nodes.value) {
        if (isPointInRect(node, rect)) nextNodeIds.add(node.id);
    }
    for (const signal of signals.value) {
        if (isPointInRect(signal.position || {}, rect)) nextSignalIds.add(signal.id);
    }
    for (const insulationJoint of insulationJoints.value) {
        if (isPointInRect(insulationJoint.position || {}, rect)) nextInsulationJointIds.add(insulationJoint.id);
    }
    for (const bufferStop of bufferStops.value) {
        if (isPointInRect(bufferStop.position || {}, rect)) nextBufferStopIds.add(bufferStop.id);
    }
    for (const sw of switches.value) {
        if (isPointInRect(sw.position || {}, rect)) nextSwitchIds.add(sw.id);
    }
    for (const platform of platforms.value) {
        if (doRectsIntersect(normalizeElementRect(platform.x, platform.y, platform.width, platform.height), rect)) {
            nextPlatformIds.add(platform.id);
        }
    }
    for (const annotation of annotations.value) {
        if (isPointInRect(annotation.position || {}, rect)) {
            nextAnnotationIds.add(annotation.id);
        }
    }

    selectedLineIds.value = nextLineIds;
    selectedNodeIds.value = nextNodeIds;
    selectedSignalIds.value = nextSignalIds;
    selectedInsulationJointIds.value = nextInsulationJointIds;
    selectedBufferStopIds.value = nextBufferStopIds;
    selectedSwitchIds.value = nextSwitchIds;
    selectedPlatformIds.value = nextPlatformIds;
    selectedAnnotationIds.value = nextAnnotationIds;
    finishAnchorInteraction();
    emitSelectedAnnotationChange();
    emitSelectedEquipmentChange();
}

function addSelectionBoxWindowListeners() {
    window.addEventListener("mousemove", onSelectionBoxWindowMouseMove);
    window.addEventListener("mouseup", onSelectionBoxWindowMouseUp);
}

function removeSelectionBoxWindowListeners() {
    window.removeEventListener("mousemove", onSelectionBoxWindowMouseMove);
    window.removeEventListener("mouseup", onSelectionBoxWindowMouseUp);
}

function startSelectionBox(event) {
    const dataPoint = clientPointToDataPoint(event.clientX, event.clientY);
    if (!dataPoint) return;
    selectionBox.value = {
        startX: dataPoint.x,
        startY: dataPoint.y,
        endX: dataPoint.x,
        endY: dataPoint.y,
        additive: event.shiftKey,
    };
    addSelectionBoxWindowListeners();
}

function updateSelectionBox(clientX, clientY) {
    if (!selectionBox.value) return;
    const dataPoint = clientPointToDataPoint(clientX, clientY);
    if (!dataPoint) return;
    selectionBox.value.endX = dataPoint.x;
    selectionBox.value.endY = dataPoint.y;
}

function finishSelectionBox() {
    if (!selectionBox.value) return;
    const box = selectionBox.value;
    removeSelectionBoxWindowListeners();
    if (isSelectionBoxLarge(box)) {
        selectElementsInBox(box);
    }
    selectionBox.value = null;
}

function cancelSelectionBox() {
    selectionBox.value = null;
    removeSelectionBoxWindowListeners();
}

function onSelectionBoxWindowMouseMove(event) {
    updateSelectionBox(event.clientX, event.clientY);
}

function onSelectionBoxWindowMouseUp(event) {
    updateSelectionBox(event.clientX, event.clientY);
    finishSelectionBox();
}

function beginDrawLine(x, y) {
    tempLine.value = { x1: x, y1: y, x2: x, y2: y };
}

function drawingLineMouseMove(x, y) {
    if (!tempLine.value) return;
    tempLine.value.x2 = x;
    tempLine.value.y2 = y;
}

function endDrawLine() {
    if (!tempLine.value) return;
    const line = {
        id: nextId(),
        name: "",
        x1: tempLine.value.x1,
        y1: tempLine.value.y1,
        x2: tempLine.value.x2,
        y2: tempLine.value.y2,
        fromNodeID: "",
        toNodeID: "",
    };
    executeMutation(() => {
        tracks.value.push(line);
    });
    tempLine.value = null;
}

function selectLine(lineId) {
    clearSelectedDeviceIds();
    selectedNodeIds.value = new Set();
    selectedLineIds.value = new Set([lineId]);
    emitSelectedEquipmentChange();
}

function deleteLine() {
    if (selectedLineIds.value.size === 0) return;
    executeMutation(() => {
        tracks.value = tracks.value.filter((line) => !selectedLineIds.value.has(line.id));
        selectedLineIds.value = new Set();
        finishAnchorInteraction();
    });
    emitSelectedEquipmentChange();
}

function deleteNode() {
    if (selectedNodeIds.value.size === 0) return;
    executeMutation(() => {
        nodes.value = nodes.value.filter((n) => !selectedNodeIds.value.has(n.id));
        selectedNodeIds.value = new Set();
    });
}

function deleteEquipment() {
    executeMutation(() => {
        signals.value = signals.value.filter((s) => !selectedSignalIds.value.has(s.id));
        insulationJoints.value = insulationJoints.value.filter((i) => !selectedInsulationJointIds.value.has(i.id));
        bufferStops.value = bufferStops.value.filter((b) => !selectedBufferStopIds.value.has(b.id));
        switches.value = switches.value.filter((s) => !selectedSwitchIds.value.has(s.id));
        platforms.value = platforms.value.filter((p) => !selectedPlatformIds.value.has(p.id));
        annotations.value = annotations.value.filter((a) => !selectedAnnotationIds.value.has(a.id));
        clearSelectedEquipment();
    });
}

function getLineList() {
    return tracks.value.map((line) => ({ ...line }));
}

function getNodeList() {
    return nodes.value.map((node) => ({ ...node }));
}

function calculatePointDist(p1, p2) {
    return Math.trunc(Math.hypot(Number(p2.x) - Number(p1.x), Number(p2.y) - Number(p1.y)));
}

function calculatePointSegmentSnap(line, point) {
    const x1 = Number(line.x1);
    const y1 = Number(line.y1);
    const x2 = Number(line.x2);
    const y2 = Number(line.y2);
    const x0 = Number(point.x);
    const y0 = Number(point.y);
    const dx = x2 - x1;
    const dy = y2 - y1;
    const lengthSquared = dx * dx + dy * dy;

    if (lengthSquared === 0) {
        return { x: x1, y: y1, dist: Math.hypot(x0 - x1, y0 - y1) };
    }

    const positionRate = Math.max(0, Math.min(1, ((x0 - x1) * dx + (y0 - y1) * dy) / lengthSquared));
    const x = x1 + positionRate * dx;
    const y = y1 + positionRate * dy;
    return { x, y, dist: Math.hypot(x0 - x, y0 - y) };
}

function calculateCrossPoint(l1, l2) {
    const denominatorX = (l2.x1 - l2.x2) * (l1.y1 - l1.y2) - (l1.x1 - l1.x2) * (l2.y1 - l2.y2);
    const denominatorY = (l2.y1 - l2.y2) * (l1.x1 - l1.x2) - (l1.y1 - l1.y2) * (l2.x1 - l2.x2);
    if (denominatorX === 0 || denominatorY === 0) return null;

    const x = Math.round(((l2.x1 - l2.x2) * (l1.x2 * l1.y1 - l1.x1 * l1.y2) - (l1.x1 - l1.x2) * (l2.x2 * l2.y1 - l2.x1 * l2.y2)) / denominatorX);
    const y = Math.round(((l2.y1 - l2.y2) * (l1.y2 * l1.x1 - l1.y1 * l1.x2) - (l1.y1 - l1.y2) * (l2.y2 * l2.x1 - l2.y1 * l2.x2)) / denominatorY);
    return { x, y };
}

function isSamePoint(p1, p2) {
    return Math.hypot(Number(p2.x) - Number(p1.x), Number(p2.y) - Number(p1.y)) < 1;
}

function findNodeAtPosition(x, y) {
    return nodes.value.find((node) => isSamePoint(node, { x, y })) || null;
}

function isLineEndpoint(line, point) {
    return isSamePoint(point, { x: line.x1, y: line.y1 }) || isSamePoint(point, { x: line.x2, y: line.y2 });
}

function isPointOnLineSegment(line, point) {
    const x1 = Number(line.x1);
    const y1 = Number(line.y1);
    const x2 = Number(line.x2);
    const y2 = Number(line.y2);
    const x = Number(point.x);
    const y = Number(point.y);
    const dx = x2 - x1;
    const dy = y2 - y1;
    const length = Math.hypot(dx, dy);

    if (length === 0) return isSamePoint(point, { x: x1, y: y1 });
    if (x < Math.min(x1, x2) - 1 || x > Math.max(x1, x2) + 1) return false;
    if (y < Math.min(y1, y2) - 1 || y > Math.max(y1, y2) + 1) return false;

    const distanceToLine = Math.abs(dy * x - dx * y + x2 * y1 - y2 * x1) / length;
    return distanceToLine < 1;
}

function getLinePointPositionRate(line, point) {
    const dx = Number(line.x2) - Number(line.x1);
    const dy = Number(line.y2) - Number(line.y1);
    const lengthSquared = dx * dx + dy * dy;
    if (lengthSquared === 0) return 0;
    return ((Number(point.x) - Number(line.x1)) * dx + (Number(point.y) - Number(line.y1)) * dy) / lengthSquared;
}

function getCrossPointRelation(l1, l2, crossPoint) {
    let onl1 = false;
    let onl2 = false;

    const crossPointOnl1Start = calculatePointDist(crossPoint, { x: l1.x1, y: l1.y1 }) === 0;
    const crossPointOnl1End = calculatePointDist(crossPoint, { x: l1.x2, y: l1.y2 }) === 0;
    const crossPointOnl2Start = calculatePointDist(crossPoint, { x: l2.x1, y: l2.y1 }) === 0;
    const crossPointOnl2End = calculatePointDist(crossPoint, { x: l2.x2, y: l2.y2 }) === 0;

    const distl21 = calculatePointDist(crossPoint, { x: l2.x1, y: l2.y1 });
    const distl22 = calculatePointDist(crossPoint, { x: l2.x2, y: l2.y2 });
    const distl11 = calculatePointDist(crossPoint, { x: l1.x1, y: l1.y1 });
    const distl12 = calculatePointDist(crossPoint, { x: l1.x2, y: l1.y2 });

    const snapPointList = [];
    if (distl11 < autoSeparateLineTolerance.value) snapPointList.push({ line: l1, point: 1 });
    if (distl12 < autoSeparateLineTolerance.value) snapPointList.push({ line: l1, point: 2 });
    if (distl21 < autoSeparateLineTolerance.value) snapPointList.push({ line: l2, point: 1 });
    if (distl22 < autoSeparateLineTolerance.value) snapPointList.push({ line: l2, point: 2 });

    if (Math.min(l1.x1, l1.x2) <= crossPoint.x && crossPoint.x <= Math.max(l1.x1, l1.x2) && Math.min(l1.y1, l1.y2) <= crossPoint.y && crossPoint.y <= Math.max(l1.y1, l1.y2)) {
        onl1 = true;
    }
    if (Math.min(l2.x1, l2.x2) <= crossPoint.x && crossPoint.x <= Math.max(l2.x1, l2.x2) && Math.min(l2.y1, l2.y2) <= crossPoint.y && crossPoint.y <= Math.max(l2.y1, l2.y2)) {
        onl2 = true;
    }

    if (onl1 && onl2) {
        if ((crossPointOnl1Start || crossPointOnl1End) && (crossPointOnl2Start || crossPointOnl2End)) {
            return { code: 0 };
        }
        if (crossPointOnl1Start || crossPointOnl1End || crossPointOnl2Start || crossPointOnl2End) {
            const breakingLine = !crossPointOnl1Start && !crossPointOnl1End ? l1 : l2;
            return { code: 1, breakingLineList: [breakingLine] };
        }
        if (!crossPointOnl1Start && !crossPointOnl1End && !crossPointOnl2Start && !crossPointOnl2End) {
            if (Math.min(distl11, distl12, distl21, distl22) < autoSeparateLineTolerance.value) {
                return { code: 3, snapPointList };
            }
            return { code: 2, breakingLineList: [l1, l2] };
        }
    }

    if (!onl2 && !onl1 && Math.min(distl11, distl12) <= autoSeparateLineTolerance.value && Math.min(distl21, distl22) <= autoSeparateLineTolerance.value) {
        return { code: 4, snapPointList };
    }

    return { code: 6 };
}

function markCrossPoint() {
    const lineSet = getLineList();
    const points = [];
    for (let i = 0; i < lineSet.length; i += 1) {
        for (let j = i + 1; j < lineSet.length; j += 1) {
            const l1 = lineSet[i];
            const l2 = lineSet[j];
            const crossPoint = calculateCrossPoint(l1, l2);
            if (!crossPoint) continue;
            const relation = getCrossPointRelation(l1, l2, crossPoint);
            crossPoint.relation = relation;
            if (relation.code <= 4) {
                points.push({ ...crossPoint, code: relation.code, relation });
            }
        }
    }
    crossPoints.value = points;
    return points;
}

function removeCrossPoint() {
    crossPoints.value = [];
}

function snapLine() {
    executeMutation(() => {
        const lineSet = getLineList();
        const snapOperations = [];

        for (const sourceLine of lineSet) {
            const endpoints = [
                { pointid: 1, x: Number(sourceLine.x1), y: Number(sourceLine.y1) },
                { pointid: 2, x: Number(sourceLine.x2), y: Number(sourceLine.y2) },
            ];

            for (const endpoint of endpoints) {
                let closestSnap = null;

                for (const targetLine of lineSet) {
                    if (targetLine.id === sourceLine.id) continue;

                    const snapPoint = calculatePointSegmentSnap(targetLine, endpoint);
                    if (snapPoint.dist > autoSeparateLineTolerance.value) continue;
                    if (!closestSnap || snapPoint.dist < closestSnap.dist) {
                        closestSnap = snapPoint;
                    }
                }

                if (closestSnap) {
                    snapOperations.push({
                        lineId: sourceLine.id,
                        pointid: endpoint.pointid,
                        x: Math.round(closestSnap.x),
                        y: Math.round(closestSnap.y),
                    });
                }
            }
        }

        for (const op of snapOperations) {
            const target = tracks.value.find((item) => item.id === op.lineId);
            if (!target) continue;
            target[`x${op.pointid}`] = op.x;
            target[`y${op.pointid}`] = op.y;
        }

        refreshCurvesForChangedGeometry({
            lineIds: new Set(snapOperations.map((op) => op.lineId)),
        });
        markCrossPoint();
    });
}

function autoMergeNode() {
    const tolerance = Number(autoMergeNodeTolerance.value);
    if (!Number.isFinite(tolerance) || tolerance < 0 || nodes.value.length < 2) return;

    const sourceNodes = getNodeList();
    const visitedNodeIDs = new Set();
    const mergeGroups = [];

    for (const node of sourceNodes) {
        if (visitedNodeIDs.has(node.id)) continue;

        const group = [];
        const pendingNodes = [node];
        visitedNodeIDs.add(node.id);

        while (pendingNodes.length > 0) {
            const currentNode = pendingNodes.pop();
            group.push(currentNode);

            for (const candidateNode of sourceNodes) {
                if (visitedNodeIDs.has(candidateNode.id)) continue;
                const distance = Math.hypot(Number(candidateNode.x) - Number(currentNode.x), Number(candidateNode.y) - Number(currentNode.y));
                if (distance <= tolerance) {
                    visitedNodeIDs.add(candidateNode.id);
                    pendingNodes.push(candidateNode);
                }
            }
        }

        if (group.length > 1) {
            mergeGroups.push(group);
        }
    }

    if (mergeGroups.length === 0) return;

    executeMutation(() => {
        const nodeIDMergeMap = new Map();
        const removedNodeIDs = new Set();

        for (const group of mergeGroups) {
            const targetNodeID = group[0].id;
            for (const node of group) {
                nodeIDMergeMap.set(node.id, targetNodeID);
                if (node.id !== targetNodeID) {
                    removedNodeIDs.add(node.id);
                }
            }
        }

        const resolveNodeID = (nodeID) => nodeIDMergeMap.get(nodeID) || nodeID;

        nodes.value = nodes.value.filter((node) => !removedNodeIDs.has(node.id));
        const nodeByID = new Map(nodes.value.map((node) => [node.id, node]));

        for (const line of tracks.value) {
            line.fromNodeID = resolveNodeID(line.fromNodeID);
            line.toNodeID = resolveNodeID(line.toNodeID);

            const fromNode = nodeByID.get(line.fromNodeID);
            const toNode = nodeByID.get(line.toNodeID);
            if (fromNode) {
                line.x1 = fromNode.x;
                line.y1 = fromNode.y;
            }
            if (toNode) {
                line.x2 = toNode.x;
                line.y2 = toNode.y;
            }
        }

        const removedLineIDs = new Set();
        tracks.value = tracks.value.filter((line) => {
            const isSameNodeLine = line.fromNodeID && line.fromNodeID === line.toNodeID && isSamePoint({ x: line.x1, y: line.y1 }, { x: line.x2, y: line.y2 });
            if (isSameNodeLine) {
                removedLineIDs.add(line.id);
                return false;
            }
            return true;
        });

        const adjacentLineIDSetByNodeID = new Map(nodes.value.map((node) => [node.id, new Set()]));
        const addAdjacentLine = (nodeID, lineID) => {
            const lineIDSet = adjacentLineIDSetByNodeID.get(nodeID);
            if (lineIDSet) {
                lineIDSet.add(lineID);
            }
        };

        for (const line of tracks.value) {
            addAdjacentLine(line.fromNodeID, line.id);
            addAdjacentLine(line.toNodeID, line.id);
        }

        for (const node of nodes.value) {
            node.adjacentLineIDList = [...(adjacentLineIDSetByNodeID.get(node.id) || [])];
        }

        const rebindToMergedNode = (equipment, afterRebind) => {
            const targetNodeID = resolveNodeID(equipment.bindingNodeID);
            const targetNode = nodeByID.get(targetNodeID);
            if (!targetNode) return;
            equipment.bindingNodeID = targetNodeID;
            if (equipment.position) {
                equipment.position.x = targetNode.x;
                equipment.position.y = targetNode.y;
            }
            if (afterRebind) {
                afterRebind(equipment, targetNode);
            }
        };

        for (const signal of signals.value) {
            rebindToMergedNode(signal);
        }
        for (const insulationJoint of insulationJoints.value) {
            rebindToMergedNode(insulationJoint);
        }
        for (const sw of switches.value) {
            rebindToMergedNode(sw, (switchItem, targetNode) => {
                switchItem.branchVectorList = buildSwitchBranchVectorList(targetNode);
            });
        }

        selectedNodeIds.value = new Set([...selectedNodeIds.value].map((nodeID) => resolveNodeID(nodeID)).filter((nodeID) => nodeByID.has(nodeID)));
        selectedLineIds.value = new Set([...selectedLineIds.value].filter((lineID) => !removedLineIDs.has(lineID)));
        finishAnchorInteraction();
        for (const curve of curves.value) {
            curve.nodeID = resolveNodeID(curve.nodeID);
        }
        refreshCurvesForChangedGeometry({
            lineIds: new Set(tracks.value.map((line) => line.id)),
            nodeIds: new Set(nodes.value.map((node) => node.id)),
        });
        markCrossPoint();
    });
}

function autoSeparateLine() {
    executeMutation(() => {
        const lineSet = getLineList();
        const candidateLineDict = {};

        const addSplitPoint = (line, point) => {
            if (!Number.isFinite(Number(point.x)) || !Number.isFinite(Number(point.y))) return;
            if (!isPointOnLineSegment(line, point)) return;
            if (isLineEndpoint(line, point)) return;
            if (!candidateLineDict[line.id]) {
                candidateLineDict[line.id] = { line, pointList: [] };
            }
            if (!candidateLineDict[line.id].pointList.some((p) => isSamePoint(p, point))) {
                candidateLineDict[line.id].pointList.push({ x: point.x, y: point.y });
            }
        };

        const addEndpointBreakPoint = (endpoint, targetLine) => {
            if (isPointOnLineSegment(targetLine, endpoint)) {
                addSplitPoint(targetLine, endpoint);
                return;
            }

            const snapPoint = calculatePointSegmentSnap(targetLine, endpoint);
            if (snapPoint.dist < autoSeparateLineTolerance.value) {
                addSplitPoint(targetLine, { x: snapPoint.x, y: snapPoint.y });
            }
        };

        for (let i = 0; i < lineSet.length; i += 1) {
            for (let j = i + 1; j < lineSet.length; j += 1) {
                const l1 = lineSet[i];
                const l2 = lineSet[j];
                const crossPoint = calculateCrossPoint(l1, l2);

                if (crossPoint && isPointOnLineSegment(l1, crossPoint) && isPointOnLineSegment(l2, crossPoint)) {
                    addSplitPoint(l1, crossPoint);
                    addSplitPoint(l2, crossPoint);
                }

                addEndpointBreakPoint({ x: Number(l1.x1), y: Number(l1.y1) }, l2);
                addEndpointBreakPoint({ x: Number(l1.x2), y: Number(l1.y2) }, l2);
                addEndpointBreakPoint({ x: Number(l2.x1), y: Number(l2.y1) }, l1);
                addEndpointBreakPoint({ x: Number(l2.x2), y: Number(l2.y2) }, l1);
            }
        }

        const nextTracks = [...tracks.value.filter((line) => !candidateLineDict[line.id])];

        for (const lineID of Object.keys(candidateLineDict)) {
            const line = candidateLineDict[lineID].line;
            const pList = candidateLineDict[lineID].pointList;
            pList.push({ x: line.x1, y: line.y1 });
            pList.push({ x: line.x2, y: line.y2 });

            for (const p of pList) {
                p.positionRate = getLinePointPositionRate(line, p);
            }
            pList.sort((a, b) => (a.positionRate < b.positionRate ? -1 : 1));

            for (let idx = 0; idx < pList.length - 1; idx += 1) {
                const p1 = pList[idx];
                const p2 = pList[idx + 1];
                if (isSamePoint(p1, p2)) continue;
                nextTracks.push({
                    id: `${line.id}s${idx}`,
                    x1: p1.x,
                    y1: p1.y,
                    x2: p2.x,
                    y2: p2.y,
                    fromNodeID: "",
                    toNodeID: "",
                });
            }
        }

        tracks.value = nextTracks;
        markCrossPoint();
    });
}

function startDrawingSignal() {
    tempSignal.value.visible = true;
}

function drawingSignalMouseMove(x, y) {
    if (!tempSignal.value.visible) return;
    tempSignal.value.x = x;
    tempSignal.value.y = y;
}

function drawingSignalMouseDown(x, y) {
    const bindingNode = findNodeAtPosition(x, y);
    if (!bindingNode) return;
    executeMutation(() => {
        const id = nextId();
        signals.value.push({
            id,
            name: id,
            type: normalizeSignalType(tempSignal.value.type),
            position: { x: toFiniteNumber(bindingNode.x), y: toFiniteNumber(bindingNode.y) },
            direction: tempSignal.value.direction,
            bindingNodeID: bindingNode.id,
        });
    });
}

function startDrawingInsulationJoint() {
    tempInsulationJoint.value.visible = true;
}

function drawingInsulationJointMouseMove(x, y) {
    if (!tempInsulationJoint.value.visible) return;
    tempInsulationJoint.value.x = x;
    tempInsulationJoint.value.y = y;
}

function drawingInsulationJointMouseDown(x, y) {
    const bindingNode = findNodeAtPosition(x, y);
    if (!bindingNode) return;
    executeMutation(() => {
        insulationJoints.value.push({
            id: nextId(),
            type: "normal",
            position: { x, y },
            bindingNodeID: bindingNode.id,
        });
    });
}

function startDrawingBufferStop() {
    tempBufferStop.value.visible = true;
}

function drawingBufferStopMouseMove(x, y) {
    if (!tempBufferStop.value.visible) return;
    tempBufferStop.value.x = x;
    tempBufferStop.value.y = y;
}

function drawingBufferStopMouseDown(x, y) {
    const bindingNode = findNodeAtPosition(x, y);
    if (!bindingNode) return;
    executeMutation(() => {
        bufferStops.value.push({
            id: nextId(),
            direction: normalizeBufferStopDirection(tempBufferStop.value.direction),
            type: normalizeBufferStopType(tempBufferStop.value.type),
            position: { x, y },
            bindingNodeID: bindingNode.id,
        });
    });
}

function drawingSwitchMouseDown(x, y) {
    const bindingNode = findNodeAtPosition(x, y);
    if (!bindingNode) return;
    generateSwitchAtNode(bindingNode);
}

function startDrawingNode() {
    tempNode.value.visible = true;
}

function getPerpendicular(x1, y1, x2, y2, x0, y0) {
    if (y1 === y2) return { x: x0, y: y1 };
    if (x1 === x2) return { x: x1, y: y0 };
    const a = y1 - y2;
    const b = -(x1 - x2);
    const c = -x2 * (y1 - y2) + y2 * (x1 - x2);
    const x = (b * b * x0 - a * b * y0 - a * c) / (a * a + b * b);
    const y = (a * a * y0 - a * b * x0 - b * c) / (a * a + b * b);
    return { x, y };
}

function calculatePointLineSnap(x1, y1, x2, y2, x0, y0) {
    const perpendicular = getPerpendicular(x1, y1, x2, y2, x0, y0);
    if (perpendicular.x < Math.min(x1, x2) || perpendicular.x > Math.max(x1, x2)) return null;
    if (perpendicular.y < Math.min(y1, y2) || perpendicular.y > Math.max(y1, y2)) return null;
    const dist = calculatePointDist(perpendicular, { x: x0, y: y0 });
    if (dist > 30) return null;
    return perpendicular;
}

function snapPointToLine(x, y) {
    let found = null;
    for (const line of tracks.value) {
        const p = calculatePointLineSnap(Number(line.x1), Number(line.y1), Number(line.x2), Number(line.y2), x, y);
        if (p) {
            found = p;
            break;
        }
    }
    perpendicularPoint.value = found;
    return found;
}

function drawingNodeMouseMove(x, y) {
    if (!tempNode.value.visible) return;
    tempNode.value.x = x;
    tempNode.value.y = y;
}

function drawingNodeMouseDown(x, y) {
    if (findNodeAtPosition(x, y)) return;

    const lineList = getLineList();
    let minDistLine = null;
    let minSnapPoint = null;
    let minDist = Number.MAX_SAFE_INTEGER;

    for (const line of lineList) {
        const snapPoint = calculatePointLineSnap(Number(line.x1), Number(line.y1), Number(line.x2), Number(line.y2), x, y);
        if (!snapPoint) continue;
        const dist = calculatePointDist(snapPoint, { x, y });
        if (dist < minDist) {
            minDist = dist;
            minDistLine = line;
            minSnapPoint = snapPoint;
        }
    }

    if (!minSnapPoint || !minDistLine) return;

    executeMutation(() => {
        const nodeX = roundLayoutNumber(minSnapPoint.x);
        const nodeY = roundLayoutNumber(minSnapPoint.y);
        const n = { id: nextId(), x: nodeX, y: nodeY, adjacentLineIDList: [`${minDistLine.id}s1`, `${minDistLine.id}s2`] };

        const l1 = {
            id: `${minDistLine.id}s1`,
            x1: minDistLine.x1,
            y1: minDistLine.y1,
            x2: nodeX,
            y2: nodeY,
            fromNodeID: minDistLine.fromNodeID,
            toNodeID: n.id,
        };
        const l2 = {
            id: `${minDistLine.id}s2`,
            x1: nodeX,
            y1: nodeY,
            x2: minDistLine.x2,
            y2: minDistLine.y2,
            fromNodeID: n.id,
            toNodeID: minDistLine.toNodeID,
        };

        tracks.value = tracks.value.filter((line) => line.id !== minDistLine.id);
        tracks.value.push(l1, l2);
        nodes.value.push(n);
    });
}

function autoGenerateNodes() {
    executeMutation(() => {
        const previousNodeByID = new Map(nodes.value.map((node) => [node.id, { ...node }]));
        nodes.value = [];
        const nodeList = [];
        const getOrCreateNode = (x, y) => {
            const found = nodeList.find((n) => Number(n.x) === Number(x) && Number(n.y) === Number(y));
            if (found) return found;
            const n = { id: nextId(), x: Number(x), y: Number(y), adjacentLineIDList: [] };
            nodeList.push(n);
            return n;
        };

        for (const l of tracks.value) {
            const n1 = getOrCreateNode(l.x1, l.y1);
            const n2 = getOrCreateNode(l.x2, l.y2);
            l.fromNodeID = n1.id;
            l.toNodeID = n2.id;
            n1.adjacentLineIDList.push(l.id);
            n2.adjacentLineIDList.push(l.id);
        }

        nodes.value = nodeList;
        for (const curve of curves.value) {
            const previousNode = previousNodeByID.get(curve.nodeID);
            if (!previousNode) continue;
            const nextNode = nodeList.find((node) => isSamePoint(node, previousNode));
            if (nextNode) {
                curve.nodeID = nextNode.id;
            }
        }
        refreshCurvesForChangedGeometry({
            lineIds: new Set(tracks.value.map((line) => line.id)),
            nodeIds: new Set(nodes.value.map((node) => node.id)),
        });
    });
}

function buildSwitchBranchVectorList(node) {
    const adjacentLineIDSet = new Set(getNodeAdjacentLineIds(node));
    const adjacentLines = tracks.value.filter((line) => adjacentLineIDSet.has(String(line.id)));
    const vectorList = [];
    const nodeID = String(node?.id ?? "");

    for (const line of adjacentLines) {
        if (String(line.fromNodeID ?? "") === nodeID) {
            vectorList.push({ x: Number(line.x2) - Number(line.x1), y: Number(line.y2) - Number(line.y1), lineID: line.id });
        } else if (String(line.toNodeID ?? "") === nodeID) {
            vectorList.push({ x: Number(line.x1) - Number(line.x2), y: Number(line.y1) - Number(line.y2), lineID: line.id });
        }
    }

    return vectorList;
}

function getNodeAdjacentLineIds(node) {
    if (!Array.isArray(node?.adjacentLineIDList)) return [];
    return node.adjacentLineIDList.map((lineID) => String(lineID)).filter((lineID) => lineID !== "");
}

function canAutoGenerateSwitchForNode(node) {
    const adjacentLineCount = getNodeAdjacentLineIds(node).length;
    return adjacentLineCount === 3 || adjacentLineCount === 4;
}

function resolveSwitchType(vectorList) {
    let acuteAngleNum = 0;
    let obtuseAngleNum = 0;
    let isSingleSwitch = false;

    for (let i = 0; i < vectorList.length; i += 1) {
        for (let j = i + 1; j < vectorList.length; j += 1) {
            const innerProduct = vectorList[i].x * vectorList[j].x + vectorList[i].y * vectorList[j].y;
            const absI = Math.hypot(vectorList[i].x, vectorList[i].y);
            const absJ = Math.hypot(vectorList[j].x, vectorList[j].y);
            if (absI === 0 || absJ === 0) continue;
            const cos = clampCos(innerProduct / (absI * absJ));
            if (Math.abs(cos + 1) < 0.01) isSingleSwitch = true;
            if (cos > 0 && cos < 1) acuteAngleNum += 1;
            else if (cos < 0 && cos >= -1) obtuseAngleNum += 1;
        }
    }

    let switchType = "unknown";
    if (acuteAngleNum === 1 && obtuseAngleNum === 2) {
        switchType = isSingleSwitch ? "single" : "symmetrical";
    } else if (acuteAngleNum === 2) {
        switchType = "slip";
    }

    return switchType;
}

function buildSwitchCandidate(node) {
    if (!canAutoGenerateSwitchForNode(node)) return null;
    const branchVectorList = buildSwitchBranchVectorList(node);
    return {
        type: resolveSwitchType(branchVectorList),
        branchVectorList,
    };
}

function buildSwitch(node, candidate = buildSwitchCandidate(node)) {
    if (!candidate) return null;
    const id = nextId();
    return {
        id,
        name: id,
        type: candidate.type,
        position: { x: node.x, y: node.y },
        bindingNodeID: node.id,
        branchVectorList: candidate.branchVectorList,
    };
}

function normalizeSwitchTypeValue(type) {
    return String(type ?? "").trim().toLowerCase();
}

function isSwitchBoundToNode(sw, node) {
    return String(sw?.bindingNodeID ?? "") === String(node?.id ?? "");
}

function isSwitchBranchListMatchedWithNode(sw, node) {
    const expectedLineIds = getNodeAdjacentLineIds(node);
    const expectedLineIdSet = new Set(expectedLineIds);
    const branchVectorList = Array.isArray(sw?.branchVectorList) ? sw.branchVectorList : [];
    const actualLineIds = branchVectorList.map((vector) => String(vector?.lineID ?? "")).filter((lineID) => lineID !== "");
    const actualLineIdSet = new Set(actualLineIds);

    if (expectedLineIdSet.size !== expectedLineIds.length) return false;
    if (actualLineIdSet.size !== expectedLineIdSet.size) return false;
    if (actualLineIds.length !== expectedLineIds.length) return false;

    for (const lineID of expectedLineIdSet) {
        if (!actualLineIdSet.has(lineID)) return false;
    }

    return true;
}

function isSwitchMatchedWithNode(sw, node, candidate) {
    if (!candidate) return false;
    return isSwitchBoundToNode(sw, node) &&
        normalizeSwitchTypeValue(sw?.type) === normalizeSwitchTypeValue(candidate.type) &&
        isSwitchBranchListMatchedWithNode(sw, node);
}

function findMatchedSwitchForNode(node, candidate) {
    return switches.value.find((sw) => isSwitchMatchedWithNode(sw, node, candidate)) || null;
}

function trimSelectedSwitchesToExisting() {
    const switchIdSet = new Set(switches.value.map((sw) => sw.id));
    selectedSwitchIds.value = new Set([...selectedSwitchIds.value].filter((id) => switchIdSet.has(id)));
}

function generateSwitchAtNode(node) {
    const candidate = buildSwitchCandidate(node);
    if (!candidate) return;

    const matchedSwitch = findMatchedSwitchForNode(node, candidate);
    const existingSwitchesAtNode = switches.value.filter((sw) => isSwitchBoundToNode(sw, node));

    if (matchedSwitch && existingSwitchesAtNode.length === 1) return;

    executeMutation(() => {
        switches.value = switches.value.filter((sw) => !isSwitchBoundToNode(sw, node));
        switches.value.push(matchedSwitch || buildSwitch(node, candidate));
        trimSelectedSwitchesToExisting();
    });
    emitSelectedEquipmentChange();
}

function autoGenerateSwitches() {
    executeMutation(() => {
        const generated = [];

        for (const n of nodes.value) {
            const candidate = buildSwitchCandidate(n);
            if (!candidate) continue;

            const matchedSwitch = findMatchedSwitchForNode(n, candidate);
            generated.push(matchedSwitch || buildSwitch(n, candidate));
        }

        switches.value = generated;
        trimSelectedSwitchesToExisting();
    });
    emitSelectedEquipmentChange();
}

function clampCos(value) {
    return Math.max(-1, Math.min(1, value));
}

function roundCurveNumber(value) {
    return Math.round(toFiniteNumber(value) * 1000) / 1000;
}

function getOutgoingLineVector(line, node) {
    if (line.fromNodeID === node.id) {
        return {
            x: toFiniteNumber(line.x2) - toFiniteNumber(line.x1),
            y: toFiniteNumber(line.y2) - toFiniteNumber(line.y1),
        };
    }

    if (line.toNodeID === node.id) {
        return {
            x: toFiniteNumber(line.x1) - toFiniteNumber(line.x2),
            y: toFiniteNumber(line.y1) - toFiniteNumber(line.y2),
        };
    }

    if (isSamePoint(node, { x: line.x1, y: line.y1 })) {
        return {
            x: toFiniteNumber(line.x2) - toFiniteNumber(line.x1),
            y: toFiniteNumber(line.y2) - toFiniteNumber(line.y1),
        };
    }

    if (isSamePoint(node, { x: line.x2, y: line.y2 })) {
        return {
            x: toFiniteNumber(line.x1) - toFiniteNumber(line.x2),
            y: toFiniteNumber(line.y1) - toFiniteNumber(line.y2),
        };
    }

    return null;
}

function buildCurveForCorner(node, line1, line2, radius = defaultCurveRadius, id = nextId()) {
    const vec1 = getOutgoingLineVector(line1, node);
    const vec2 = getOutgoingLineVector(line2, node);
    if (!vec1 || !vec2) return null;

    const len1 = Math.hypot(vec1.x, vec1.y);
    const len2 = Math.hypot(vec2.x, vec2.y);
    if (len1 <= 0 || len2 <= 0) return null;

    const unit1 = { x: vec1.x / len1, y: vec1.y / len1 };
    const unit2 = { x: vec2.x / len2, y: vec2.y / len2 };
    const cos = clampCos(unit1.x * unit2.x + unit1.y * unit2.y);
    const angle = Math.acos(cos);
    const angleDeg = angle * 180 / Math.PI;
    if (angleDeg <= curveCornerMinAngle || angleDeg >= curveCornerMaxAngle) return null;

    const halfAngleTan = Math.tan(angle / 2);
    if (!Number.isFinite(halfAngleTan) || halfAngleTan <= 0) return null;

    let fittedRadius = toFiniteNumber(radius) || defaultCurveRadius;
    if (fittedRadius <= 0) return null;

    let tangentDistance = fittedRadius / halfAngleTan;
    if (!Number.isFinite(tangentDistance) || tangentDistance <= 0) return null;

    const shortestLineLength = Math.min(len1, len2);
    if (tangentDistance > shortestLineLength) {
        fittedRadius = shortestLineLength * halfAngleTan * curveRadiusLineFitRatio;
        tangentDistance = fittedRadius / halfAngleTan;
        if (!Number.isFinite(fittedRadius) || fittedRadius <= 0 || !Number.isFinite(tangentDistance) || tangentDistance <= 0) {
            return null;
        }
    }
    if (tangentDistance > len1 || tangentDistance > len2) return null;

    const bisector = {
        x: unit1.x + unit2.x,
        y: unit1.y + unit2.y,
    };
    const bisectorLength = Math.hypot(bisector.x, bisector.y);
    if (bisectorLength <= 0) return null;

    const centerDistance = fittedRadius / Math.sin(angle / 2);
    const vertex = { x: toFiniteNumber(node.x), y: toFiniteNumber(node.y) };
    const start = {
        x: vertex.x + unit1.x * tangentDistance,
        y: vertex.y + unit1.y * tangentDistance,
    };
    const end = {
        x: vertex.x + unit2.x * tangentDistance,
        y: vertex.y + unit2.y * tangentDistance,
    };
    const center = {
        x: vertex.x + (bisector.x / bisectorLength) * centerDistance,
        y: vertex.y + (bisector.y / bisectorLength) * centerDistance,
    };
    const startRadius = { x: start.x - center.x, y: start.y - center.y };
    const endRadius = { x: end.x - center.x, y: end.y - center.y };
    const sweepFlag = startRadius.x * endRadius.y - startRadius.y * endRadius.x >= 0 ? 1 : 0;

    return normalizeCurve({
        id,
        nodeID: node.id,
        tangentLinkID1: line1.id,
        tangentLinkID2: line2.id,
        radius: roundCurveNumber(fittedRadius),
        angle: roundCurveNumber(angleDeg),
        tangentDistance: roundCurveNumber(tangentDistance),
        start: {
            x: roundCurveNumber(start.x),
            y: roundCurveNumber(start.y),
        },
        end: {
            x: roundCurveNumber(end.x),
            y: roundCurveNumber(end.y),
        },
        center: {
            x: roundCurveNumber(center.x),
            y: roundCurveNumber(center.y),
        },
        largeArcFlag: 0,
        sweepFlag,
    });
}

function autoGenerateCurves() {
    executeMutation(() => {
        rebuildNodeAdjacentLineIds();

        const lineByID = new Map(tracks.value.map((line) => [line.id, line]));
        const generated = [];
        for (const node of nodes.value) {
            const adjacentLineIDList = Array.isArray(node.adjacentLineIDList)
                ? node.adjacentLineIDList
                : [];
            if (adjacentLineIDList.length !== 2) continue;

            const line1 = lineByID.get(adjacentLineIDList[0]);
            const line2 = lineByID.get(adjacentLineIDList[1]);
            if (!line1 || !line2) continue;

            const curve = buildCurveForCorner(node, line1, line2, defaultCurveRadius);
            if (curve) {
                generated.push(curve);
            }
        }

        curves.value = generated;
    });

    return curves.value.length;
}

function startDrawingPlatform() {
    tempPlatformPosition.value = null;
}

function drawingPlatformMouseMove(x, y) {
    if (!tempPlatformPosition.value) return;
    tempPlatformPosition.value.endX = x;
    tempPlatformPosition.value.endY = y;
}

function drawingPlatformMouseDown(x, y) {
    if (!tempPlatformPosition.value) {
        tempPlatformPosition.value = { startX: x, startY: y, endX: x, endY: y };
        return;
    }

    executeMutation(() => {
        const id = nextId();
        const startX = tempPlatformPosition.value.startX;
        const endX = tempPlatformPosition.value.endX;
        const startY = tempPlatformPosition.value.startY;
        const endY = tempPlatformPosition.value.endY;

        platforms.value.push({
            id,
            name: id,
            x: Math.min(startX, endX),
            y: Math.min(startY, endY),
            width: Math.abs(endX - startX),
            height: Math.abs(endY - startY),
        });
    });

    tempPlatformPosition.value = null;
}

function drawingAnnotationMouseDown(x, y) {
    executeMutation(() => {
        const annotation = buildDefaultAnnotation(x, y);
        annotations.value.push(annotation);
        setSelectedAnnotationIds([annotation.id]);
    });
}

const tempPlatformView = computed(() => {
    if (!tempPlatformPosition.value) {
        return { x: 0, y: 0, width: 0, height: 0 };
    }
    const startX = tempPlatformPosition.value.startX;
    const endX = tempPlatformPosition.value.endX;
    const startY = tempPlatformPosition.value.startY;
    const endY = tempPlatformPosition.value.endY;
    return {
        x: Math.min(startX, endX),
        y: Math.min(startY, endY),
        width: Math.abs(endX - startX),
        height: Math.abs(endY - startY),
    };
});

function buildJsonData() {
    return JSON.stringify({
        metadata: { ...layoutMetadata.value, latestElementID: latestElementID.value },
        tracks: tracks.value,
        curves: curves.value,
        nodes: nodes.value,
        signals: signals.value,
        insulationJoints: insulationJoints.value,
        bufferStops: bufferStops.value,
        platforms: platforms.value,
        switches: switches.value,
        annotations: annotations.value,
    });
}

function clearElements() {
    layoutMetadata.value = {};
    tracks.value = [];
    curves.value = [];
    nodes.value = [];
    signals.value = [];
    insulationJoints.value = [];
    bufferStops.value = [];
    platforms.value = [];
    switches.value = [];
    annotations.value = [];
    clearSelectedLines();
    clearSelectedNodes();
    clearSelectedEquipment();
    resetGridOrigin();
    resetCanvasBounds();
}

function loadDataFromJson(jsonObj) {
    executeMutation(() => {
        clearElements();
        layoutMetadata.value = { ...(jsonObj?.metadata || {}) };
        latestElementID.value = Number(jsonObj?.metadata?.latestElementID || 0);
        tracks.value = (jsonObj?.tracks || []).map((track) => ({ name: "", ...track }));
        curves.value = (jsonObj?.curves || []).map((curve) => normalizeCurve(curve));
        nodes.value = (jsonObj?.nodes || []).map((node) => ({ ...node }));
        signals.value = (jsonObj?.signals || []).map((signal) => normalizeNamedEquipment(signal));
        insulationJoints.value = (jsonObj?.insulationJoints || []).map((ij) => ({ ...ij }));
        bufferStops.value = (jsonObj?.bufferStops || []).map((bufferStop) => normalizeBufferStop(bufferStop));
        platforms.value = (jsonObj?.platforms || []).map((platform) => normalizeNamedEquipment(platform));
        switches.value = (jsonObj?.switches || []).map((sw) => normalizeNamedEquipment(sw));
        annotations.value = (jsonObj?.annotations || []).map((annotation) => normalizeAnnotation(annotation));
        syncSignalsToBindingNodes();
        alignGridOriginToCurrentContent();
        resetCanvasBounds();
    });
    emitSelectedAnnotationChange();
}

function handleLineClick(lineId) {
    if (editModeCode.value !== 0) return;
    setSelectedAnnotationIds([]);
    selectLine(lineId);
}

function shouldHandleElementMouseDown(event) {
    if (editModeCode.value !== 0) return false;
    event?.stopPropagation();
    return true;
}

function handleNodeClick(event, nodeId) {
    if (editModeCode.value === 1 && drawingObject.value === "w") {
        event.preventDefault();
        event.stopPropagation();
        const node = getNodeById(nodeId);
        if (node) generateSwitchAtNode(node);
        return;
    }

    if (!shouldHandleElementMouseDown(event)) return;
    event.preventDefault();
    cancelSelectionBox();
    finishAnnotationInteraction();
    clearSelectedDeviceIds();
    setSelectedAnnotationIds([]);
    emitSelectedEquipmentChange();
    selectedNodeIds.value = new Set([...selectedNodeIds.value, nodeId]);
    beginNodeMove(event, nodeId);
}

function selectEquipment(kind, id, additive = false) {
    if (!additive) {
        clearSelectedDeviceIds();
    }

    const targetSet = new Set(additive ? [...getEquipmentSelectedSet(kind)] : []);
    targetSet.add(id);
    if (kind === "signal") selectedSignalIds.value = targetSet;
    if (kind === "insulationJoint") selectedInsulationJointIds.value = targetSet;
    if (kind === "bufferStop") selectedBufferStopIds.value = targetSet;
    if (kind === "switch") selectedSwitchIds.value = targetSet;
    if (kind === "platform") selectedPlatformIds.value = targetSet;
    emitSelectedEquipmentChange();
}

function handleSignalClick(event, signalId) {
    if (!shouldHandleElementMouseDown(event)) return;
    setSelectedAnnotationIds([]);
    selectEquipment("signal", signalId, event.shiftKey);
}

function handleInsulationJointClick(event, id) {
    if (!shouldHandleElementMouseDown(event)) return;
    setSelectedAnnotationIds([]);
    selectEquipment("insulationJoint", id, event.shiftKey);
}

function handleBufferStopClick(event, id) {
    if (!shouldHandleElementMouseDown(event)) return;
    setSelectedAnnotationIds([]);
    selectEquipment("bufferStop", id, event.shiftKey);
}

function handleSwitchClick(event, id) {
    if (!shouldHandleElementMouseDown(event)) return;
    setSelectedAnnotationIds([]);
    selectEquipment("switch", id, event.shiftKey);
}

function handlePlatformClick(event, id) {
    if (!shouldHandleElementMouseDown(event)) return;
    setSelectedAnnotationIds([]);
    selectEquipment("platform", id, event.shiftKey);
}

function handleAnnotationClick(event, id) {
    if (!shouldHandleElementMouseDown(event)) return;
    clearSelectedDeviceIds();
    emitSelectedEquipmentChange();
    if (event.shiftKey) {
        setSelectedAnnotationIds([...selectedAnnotationIds.value, id]);
        return;
    }
    setSelectedAnnotationIds([id]);
}

function getAnnotationById(id) {
    return annotations.value.find((annotation) => annotation.id === id) || null;
}

function updateSelectedEquipment(kind, id, patch) {
    const collection = getEquipmentCollection(kind);
    const target = collection.find((item) => item.id === id);
    if (!target) return;

    executeMutation(() => {
        const previousId = target.id;
        const previousLinkState = kind === "link"
            ? {
                fromNodeID: target.fromNodeID,
                toNodeID: target.toNodeID,
                x1: target.x1,
                y1: target.y1,
                x2: target.x2,
                y2: target.y2,
            }
            : null;
        const normalizedPatch = { ...patch };
        if (patch.position) {
            target.position = {
                ...(target.position || {}),
                ...patch.position,
            };
            normalizedPatch.position = target.position;
        }
        Object.assign(target, normalizedPatch);

        if (kind === "signal" || kind === "switch" || kind === "platform") {
            Object.assign(target, normalizeNamedEquipment(target));
        }
        if (kind === "signal") {
            syncSignalPositionToBindingNode(target);
        }
        if (kind === "bufferStop") {
            Object.assign(target, normalizeBufferStop(target));
        }

        if (kind === "link" && patch.id != null && patch.id !== previousId) {
            updateLinkReferences(previousId, patch.id);
            selectedLineIds.value = new Set([patch.id]);
        } else if (patch.id != null && patch.id !== id) {
            selectEquipment(kind, patch.id, false);
        }

        if (kind === "link" && previousLinkState) {
            syncLineEndpointMoveEffects(target, previousLinkState);
        }
    });

    emitSelectedEquipmentChange();
}

function hasOwnProperty(object, key) {
    return Object.prototype.hasOwnProperty.call(object, key);
}

function updateLinkReferences(previousId, nextId) {
    for (const node of nodes.value) {
        if (!Array.isArray(node.adjacentLineIDList)) continue;
        node.adjacentLineIDList = node.adjacentLineIDList.map((lineID) => lineID === previousId ? nextId : lineID);
    }

    for (const sw of switches.value) {
        for (const vector of sw.branchVectorList || []) {
            if (vector.lineID === previousId) {
                vector.lineID = nextId;
            }
        }
    }

    for (const curve of curves.value) {
        if (curve.tangentLinkID1 === previousId) {
            curve.tangentLinkID1 = nextId;
        }
        if (curve.tangentLinkID2 === previousId) {
            curve.tangentLinkID2 = nextId;
        }
    }
}

function hasCoordinateChanged(values, key, previousValue) {
    if (!hasOwnProperty(values, key)) return false;
    return Math.abs(toFiniteNumber(values[key]) - toFiniteNumber(previousValue)) > 0.000001;
}

function updateLinesForNode(node) {
    for (const line of tracks.value) {
        if (line.fromNodeID === node.id) {
            line.x1 = node.x;
            line.y1 = node.y;
        }
        if (line.toNodeID === node.id) {
            line.x2 = node.x;
            line.y2 = node.y;
        }
    }
}

function syncBoundEquipmentForNode(node) {
    const isBoundToNode = (equipment) => String(equipment?.bindingNodeID || "") === String(node.id);
    const moveEquipmentToNode = (equipment) => {
        equipment.position = {
            ...(equipment.position || {}),
            x: toFiniteNumber(node.x),
            y: toFiniteNumber(node.y),
        };
    };

    for (const signal of signals.value) {
        if (isBoundToNode(signal)) moveEquipmentToNode(signal);
    }
    for (const insulationJoint of insulationJoints.value) {
        if (isBoundToNode(insulationJoint)) moveEquipmentToNode(insulationJoint);
    }
    for (const bufferStop of bufferStops.value) {
        if (isBoundToNode(bufferStop)) moveEquipmentToNode(bufferStop);
    }
    for (const sw of switches.value) {
        if (!isBoundToNode(sw)) continue;
        moveEquipmentToNode(sw);
        sw.branchVectorList = buildSwitchBranchVectorList(node);
    }
}

function refreshCurvesForChangedGeometry({ lineIds = new Set(), nodeIds = new Set() } = {}) {
    if (curves.value.length === 0) return;
    if (lineIds.size === 0 && nodeIds.size === 0) return;

    const nodeByID = new Map(nodes.value.map((item) => [item.id, item]));
    const lineByID = new Map(tracks.value.map((line) => [line.id, line]));
    curves.value = curves.value.map((curve) => {
        const shouldUpdate =
            nodeIds.has(curve.nodeID) ||
            lineIds.has(curve.tangentLinkID1) ||
            lineIds.has(curve.tangentLinkID2);
        if (!shouldUpdate) return curve;

        const curveNode = nodeByID.get(curve.nodeID);
        const line1 = lineByID.get(curve.tangentLinkID1);
        const line2 = lineByID.get(curve.tangentLinkID2);
        if (!curveNode || !line1 || !line2) return curve;

        return buildCurveForCorner(
            curveNode,
            line1,
            line2,
            toFiniteNumber(curve.radius) || defaultCurveRadius,
            curve.id
        ) || curve;
    });
}

function updateCurvesForNodeMove(node) {
    const changedLineIds = new Set();
    for (const line of tracks.value) {
        if (line.fromNodeID === node.id || line.toNodeID === node.id) {
            changedLineIds.add(line.id);
        }
    }

    refreshCurvesForChangedGeometry({
        lineIds: changedLineIds,
        nodeIds: new Set([node.id]),
    });
}

function getNodeById(nodeId) {
    const id = String(nodeId ?? "");
    return nodes.value.find((node) => String(node.id ?? "") === id) || null;
}

function pushNodeInteractionUndoSnapshot() {
    const interaction = nodeInteraction.value;
    if (!interaction || interaction.undoCaptured) return;
    finishedCmdList.value.push(cloneState());
    if (finishedCmdList.value.length > 30) {
        finishedCmdList.value.shift();
    }
    revokedCmdList.value = [];
    interaction.undoCaptured = true;
}

function addNodeInteractionWindowListeners() {
    window.addEventListener("mousemove", onNodeInteractionWindowMouseMove);
    window.addEventListener("mouseup", onNodeInteractionWindowMouseUp);
}

function removeNodeInteractionWindowListeners() {
    window.removeEventListener("mousemove", onNodeInteractionWindowMouseMove);
    window.removeEventListener("mouseup", onNodeInteractionWindowMouseUp);
}

function beginNodeMove(event, nodeId) {
    finishNodeInteraction();

    const node = getNodeById(nodeId);
    if (!node) return;

    const startPointer = clientPointToDataPoint(event.clientX, event.clientY);
    if (!startPointer) return;

    nodeInteraction.value = {
        nodeId,
        startNode: {
            x: toFiniteNumber(node.x),
            y: toFiniteNumber(node.y),
        },
        startPointer,
        undoCaptured: false,
    };
    addNodeInteractionWindowListeners();
}

function updateNodeInteraction(event) {
    const interaction = nodeInteraction.value;
    if (!interaction) return;

    const node = getNodeById(interaction.nodeId);
    if (!node) {
        finishNodeInteraction();
        return;
    }

    const currentPointer = clientPointToDataPoint(event.clientX, event.clientY);
    if (!currentPointer) return;

    const dx = currentPointer.x - interaction.startPointer.x;
    const dy = currentPointer.y - interaction.startPointer.y;
    const nextPosition = applyGridSnapWhenEnabled({
        x: interaction.startNode.x + dx,
        y: interaction.startNode.y + dy,
    });
    const nextX = roundLayoutNumber(nextPosition.x);
    const nextY = roundLayoutNumber(nextPosition.y);
    if (nextX === toFiniteNumber(node.x) && nextY === toFiniteNumber(node.y)) return;

    pushNodeInteractionUndoSnapshot();
    node.x = nextX;
    node.y = nextY;
    updateLinesForNode(node);
    syncBoundEquipmentForNode(node);
    updateCurvesForNodeMove(node);
    expandCanvasToIncludePoint(node, { triggerMargin: canvasEdgeTriggerMargin });
}

function finishNodeInteraction() {
    nodeInteraction.value = null;
    removeNodeInteractionWindowListeners();
}

function onNodeInteractionWindowMouseMove(event) {
    event.preventDefault();
    updateNodeInteraction(event);
}

function onNodeInteractionWindowMouseUp(event) {
    event.preventDefault();
    updateNodeInteraction(event);
    finishNodeInteraction();
}

function rebuildNodeAdjacentLineIds() {
    const adjacentLineIDSetByNodeID = new Map(nodes.value.map((node) => [node.id, new Set()]));
    for (const line of tracks.value) {
        const fromLineIDSet = adjacentLineIDSetByNodeID.get(line.fromNodeID);
        const toLineIDSet = adjacentLineIDSetByNodeID.get(line.toNodeID);
        if (fromLineIDSet) fromLineIDSet.add(line.id);
        if (toLineIDSet) toLineIDSet.add(line.id);
    }

    for (const node of nodes.value) {
        node.adjacentLineIDList = [...(adjacentLineIDSetByNodeID.get(node.id) || [])];
    }
}

function syncLinkEndpointNode(line, previousState, endpointConfig) {
    const { nodeIDKey, xKey, yKey } = endpointConfig;
    const node = nodes.value.find((item) => item.id === line[nodeIDKey]);
    if (!node) return;

    const nodeChanged = line[nodeIDKey] !== previousState[nodeIDKey];
    const coordinateChanged =
        hasCoordinateChanged(line, xKey, previousState[xKey]) ||
        hasCoordinateChanged(line, yKey, previousState[yKey]);

    if (coordinateChanged) {
        node.x = toFiniteNumber(line[xKey]);
        node.y = toFiniteNumber(line[yKey]);
        updateLinesForNode(node);
        return;
    }

    if (nodeChanged) {
        line[xKey] = toFiniteNumber(node.x);
        line[yKey] = toFiniteNumber(node.y);
    }
}

function syncLinkEndpointNodes(line, previousState) {
    const fromNodeChanged = line.fromNodeID !== previousState.fromNodeID;
    const toNodeChanged = line.toNodeID !== previousState.toNodeID;

    syncLinkEndpointNode(line, previousState, {
        nodeIDKey: "fromNodeID",
        xKey: "x1",
        yKey: "y1",
    });
    syncLinkEndpointNode(line, previousState, {
        nodeIDKey: "toNodeID",
        xKey: "x2",
        yKey: "y2",
    });

    if (fromNodeChanged || toNodeChanged) {
        rebuildNodeAdjacentLineIds();
    }
}

function getAnnotationInteractionStartState(annotation) {
    return {
        position: {
            x: toFiniteNumber(annotation.position?.x),
            y: toFiniteNumber(annotation.position?.y),
        },
    };
}

function pushAnnotationInteractionUndoSnapshot() {
    const interaction = annotationInteraction.value;
    if (!interaction || interaction.undoCaptured) return;
    finishedCmdList.value.push(cloneState());
    if (finishedCmdList.value.length > 30) {
        finishedCmdList.value.shift();
    }
    revokedCmdList.value = [];
    interaction.undoCaptured = true;
}

function addAnnotationInteractionWindowListeners() {
    window.addEventListener("mousemove", onAnnotationInteractionWindowMouseMove);
    window.addEventListener("mouseup", onAnnotationInteractionWindowMouseUp);
}

function removeAnnotationInteractionWindowListeners() {
    window.removeEventListener("mousemove", onAnnotationInteractionWindowMouseMove);
    window.removeEventListener("mouseup", onAnnotationInteractionWindowMouseUp);
}

function beginAnnotationTextMove(event, annotationId) {
    if (!shouldHandleElementMouseDown(event)) return;
    event.preventDefault();
    cancelSelectionBox();

    const annotation = getAnnotationById(annotationId);
    if (!annotation) return;
    if (!isAnnotationSelected(annotationId) || selectedAnnotationIds.value.size !== 1) {
        setSelectedAnnotationIds([annotationId]);
    }

    const startState = getAnnotationInteractionStartState(annotation);
    const startPointer = clientPointToDataPoint(event.clientX, event.clientY);
    if (!startPointer) return;

    annotationInteraction.value = {
        annotationId,
        startState,
        startPointer,
        undoCaptured: false,
    };
    addAnnotationInteractionWindowListeners();
}

function updateAnnotationTextInteraction(annotation, interaction, currentPointer) {
    const dx = currentPointer.x - interaction.startPointer.x;
    const dy = currentPointer.y - interaction.startPointer.y;
    pushAnnotationInteractionUndoSnapshot();
    annotation.position = {
        x: roundLayoutNumber(interaction.startState.position.x + dx),
        y: roundLayoutNumber(interaction.startState.position.y + dy),
    };
    expandCanvasToIncludePoint(annotation.position, { triggerMargin: canvasEdgeTriggerMargin });
    emitSelectedAnnotationChange();
}

function updateAnnotationInteraction(event) {
    const interaction = annotationInteraction.value;
    if (!interaction) return;
    const annotation = getAnnotationById(interaction.annotationId);
    if (!annotation) {
        finishAnnotationInteraction();
        return;
    }

    const currentPointer = clientPointToDataPoint(event.clientX, event.clientY);
    if (!currentPointer) return;
    updateAnnotationTextInteraction(annotation, interaction, currentPointer);
}

function finishAnnotationInteraction() {
    annotationInteraction.value = null;
    removeAnnotationInteractionWindowListeners();
}

function onAnnotationInteractionWindowMouseMove(event) {
    event.preventDefault();
    updateAnnotationInteraction(event);
}

function onAnnotationInteractionWindowMouseUp(event) {
    event.preventDefault();
    updateAnnotationInteraction(event);
    finishAnnotationInteraction();
}

function getLinkEndpointState(line) {
    return {
        fromNodeID: line.fromNodeID,
        toNodeID: line.toNodeID,
        x1: line.x1,
        y1: line.y1,
        x2: line.x2,
        y2: line.y2,
    };
}

function getLineById(lineId) {
    return tracks.value.find((line) => line.id === lineId) || null;
}

function pushAnchorInteractionUndoSnapshot() {
    const interaction = movingAnchor.value;
    if (!interaction || interaction.undoCaptured) return;
    finishedCmdList.value.push(cloneState());
    if (finishedCmdList.value.length > 30) {
        finishedCmdList.value.shift();
    }
    revokedCmdList.value = [];
    interaction.undoCaptured = true;
}

function addAnchorInteractionWindowListeners() {
    window.addEventListener("mousemove", onAnchorInteractionWindowMouseMove);
    window.addEventListener("mouseup", onAnchorInteractionWindowMouseUp);
}

function removeAnchorInteractionWindowListeners() {
    window.removeEventListener("mousemove", onAnchorInteractionWindowMouseMove);
    window.removeEventListener("mouseup", onAnchorInteractionWindowMouseUp);
}

function handleAnchorDown(event, anchor) {
    if (!shouldHandleElementMouseDown(event)) return;
    event.preventDefault();
    finishAnchorInteraction();
    cancelSelectionBox();
    finishAnnotationInteraction();

    const line = getLineById(anchor.lineId);
    const startPointer = clientPointToDataPoint(event.clientX, event.clientY);
    if (!line || !startPointer) return;

    movingAnchor.value = {
        lineId: anchor.lineId,
        type: anchor.type,
        startPointer,
        startLine: getLinkEndpointState(line),
        undoCaptured: false,
    };
    addAnchorInteractionWindowListeners();
}

function getAnchorInteractionTargetPosition(interaction, currentPointer) {
    const dx = currentPointer.x - interaction.startPointer.x;
    const dy = currentPointer.y - interaction.startPointer.y;
    const startX = interaction.type === "sp"
        ? interaction.startLine.x1
        : interaction.startLine.x2;
    const startY = interaction.type === "sp"
        ? interaction.startLine.y1
        : interaction.startLine.y2;
    const nextPosition = applyGridSnapWhenEnabled({
        x: toFiniteNumber(startX) + dx,
        y: toFiniteNumber(startY) + dy,
    });
    return {
        x: roundLayoutNumber(nextPosition.x),
        y: roundLayoutNumber(nextPosition.y),
    };
}

function syncLineEndpointMoveEffects(line, previousState) {
    syncLinkEndpointNodes(line, previousState);

    const affectedNodeIds = new Set([
        previousState.fromNodeID,
        previousState.toNodeID,
        line.fromNodeID,
        line.toNodeID,
    ].filter((nodeId) => nodeId != null && nodeId !== ""));

    for (const nodeId of affectedNodeIds) {
        const node = getNodeById(nodeId);
        if (!node) continue;
        syncBoundEquipmentForNode(node);
    }

    refreshCurvesForChangedGeometry({
        lineIds: new Set([line.id]),
        nodeIds: affectedNodeIds,
    });
}

function updateAnchorInteraction(event) {
    const interaction = movingAnchor.value;
    if (!interaction) return;

    const line = getLineById(interaction.lineId);
    if (!line) {
        finishAnchorInteraction();
        return;
    }

    const currentPointer = clientPointToDataPoint(event.clientX, event.clientY);
    if (!currentPointer) return;

    const nextPosition = getAnchorInteractionTargetPosition(interaction, currentPointer);
    const xKey = interaction.type === "sp" ? "x1" : "x2";
    const yKey = interaction.type === "sp" ? "y1" : "y2";
    if (nextPosition.x === toFiniteNumber(line[xKey]) && nextPosition.y === toFiniteNumber(line[yKey])) return;

    pushAnchorInteractionUndoSnapshot();
    const previousState = getLinkEndpointState(line);
    line[xKey] = nextPosition.x;
    line[yKey] = nextPosition.y;
    syncLineEndpointMoveEffects(line, previousState);
    expandCanvasToIncludePoint(nextPosition, { triggerMargin: canvasEdgeTriggerMargin });
    emitSelectedEquipmentChange();
}

function finishAnchorInteraction() {
    movingAnchor.value = null;
    removeAnchorInteractionWindowListeners();
}

function onAnchorInteractionWindowMouseMove(event) {
    event.preventDefault();
    updateAnchorInteraction(event);
}

function onAnchorInteractionWindowMouseUp(event) {
    event.preventDefault();
    updateAnchorInteraction(event);
    finishAnchorInteraction();
}

function onMouseMove(event) {
    updateCursorPosition(event.clientX, event.clientY);
    svgRef.value?.focus({ preventScroll: true });
    const x = cursorParam.value.x;
    const y = cursorParam.value.y;

    if (selectionBox.value) {
        updateSelectionBox(event.clientX, event.clientY);
        return;
    }

    if (editModeCode.value === 0) {
        return;
    }

    if (drawingObject.value === "l") {
        drawingLineMouseMove(x, y);
    } else if (drawingObject.value === "s") {
        drawingSignalMouseMove(x, y);
    } else if (drawingObject.value === "i") {
        drawingInsulationJointMouseMove(x, y);
    } else if (drawingObject.value === "e") {
        drawingBufferStopMouseMove(x, y);
    } else if (drawingObject.value === "n") {
        drawingNodeMouseMove(x, y);
    } else if (drawingObject.value === "p") {
        drawingPlatformMouseMove(x, y);
    }
}

function onMouseDown(event) {
    updateCursorPosition(event.clientX, event.clientY);
    const x = cursorParam.value.x;
    const y = cursorParam.value.y;

    if (editModeCode.value === 0) {
        if (event.button === 0) {
            event.preventDefault();
            startSelectionBox(event);
        }
        return;
    }

    if (drawingObject.value === "l") {
        if (!tempLine.value) beginDrawLine(x, y);
        else endDrawLine();
    } else if (drawingObject.value === "s") {
        drawingSignalMouseDown(x, y);
    } else if (drawingObject.value === "i") {
        drawingInsulationJointMouseDown(x, y);
    } else if (drawingObject.value === "e") {
        drawingBufferStopMouseDown(x, y);
    } else if (drawingObject.value === "w") {
        drawingSwitchMouseDown(x, y);
    } else if (drawingObject.value === "n") {
        drawingNodeMouseDown(x, y);
    } else if (drawingObject.value === "p") {
        drawingPlatformMouseDown(x, y);
    } else if (drawingObject.value === "a") {
        drawingAnnotationMouseDown(x, y);
    }
}

function onMouseUp(event) {
    if (!selectionBox.value) return;
    updateSelectionBox(event.clientX, event.clientY);
    finishSelectionBox();
}

function onKeydown(event) {
    if (event.key === "Escape") {
        cancelSelectionBox();
        finishNodeInteraction();
        finishAnnotationInteraction();
        clearSelectedEquipment();
        clearSelectedLines();
        clearSelectedNodes();
    }
    if (event.ctrlKey && event.key === "z") {
        event.preventDefault();
        revoke();
    }
    if (event.ctrlKey && event.key === "y") {
        event.preventDefault();
        redo();
    }
    if (editModeCode.value === 1 && drawingObject.value === "s") {
        const candidateDirection = ["w", "e", "s", "d"];
        if (candidateDirection.includes(event.key)) {
            tempSignal.value.direction = event.key;
            drawingSignalMouseMove(cursorParam.value.x, cursorParam.value.y);
        }
    }
    if (editModeCode.value === 1 && drawingObject.value === "e") {
        const bufferStopDirectionByKey = {
            ArrowLeft: "left",
            ArrowRight: "right",
            l: "left",
            r: "right",
        };
        const nextDirection = bufferStopDirectionByKey[event.key];
        if (nextDirection) {
            event.preventDefault();
            tempBufferStop.value.direction = nextDirection;
            drawingBufferStopMouseMove(cursorParam.value.x, cursorParam.value.y);
        }
    }
}

function isSignalSelected(id) {
    return selectedSignalIds.value.has(id);
}

function isLineSelected(id) {
    return selectedLineIds.value.has(id);
}

function isNodeSelected(id) {
    return selectedNodeIds.value.has(id);
}

function isInsulationJointSelected(id) {
    return selectedInsulationJointIds.value.has(id);
}

function isBufferStopSelected(id) {
    return selectedBufferStopIds.value.has(id);
}

function isSwitchSelected(id) {
    return selectedSwitchIds.value.has(id);
}

function isPlatformSelected(id) {
    return selectedPlatformIds.value.has(id);
}

function isAnnotationSelected(id) {
    return selectedAnnotationIds.value.has(id);
}

function getSignalDirectionView(signal) {
    const directionViews = {
        e: { coefScaleX: 1, coefShiftY: 1, horizontalSide: "right", verticalSide: "top" },
        w: { coefScaleX: -1, coefShiftY: 1, horizontalSide: "left", verticalSide: "top" },
        s: { coefScaleX: -1, coefShiftY: 0, horizontalSide: "left", verticalSide: "bottom" },
        d: { coefScaleX: 1, coefShiftY: 0, horizontalSide: "right", verticalSide: "bottom" },
    };
    return directionViews[signal.direction || "e"] || directionViews.e;
}

function signalAssetDimension(asset, key) {
    const value = Number(asset?.[key]);
    return Number.isFinite(value) && value > 0 ? value : 0;
}

function signalAssetBounds(asset) {
    const width = signalAssetDimension(asset, "width");
    const height = signalAssetDimension(asset, "height");
    const bounds = asset?.bounds || {};
    const minX = Number(bounds.minX);
    const minY = Number(bounds.minY);
    const maxX = Number(bounds.maxX);
    const maxY = Number(bounds.maxY);
    if ([minX, minY, maxX, maxY].every(Number.isFinite)) {
        return {
            minX,
            minY,
            maxX,
            maxY,
            width: Math.abs(maxX - minX),
            height: Math.abs(maxY - minY),
        };
    }

    return {
        minX: 0,
        minY: 0,
        maxX: width,
        maxY: height,
        width,
        height,
    };
}

function signalNodeGap() {
    return editorDisplayStyles.value.node.radius + signalNodeExtraGap;
}

function signalHorizontalGap() {
    return 0;
}

function signalVerticalGap(directionView) {
    return directionView.verticalSide === "top" ? -signalNodeGap() : signalNodeGap();
}

function signalTransform(signal) {
    const d = getSignalDirectionView(signal);
    const scale = editorDisplayStyles.value.signal.scale;
    const asset = signalStyleAsset(signal);
    if (asset.placement === "quadrant") {
        const bounds = signalAssetBounds(asset);
        const anchorX = signalAssetDimension(asset, "width");
        const svgCoefScaleX = d.horizontalSide === "right" ? -1 : 1;
        const x = screenX(signal.position.x) - svgCoefScaleX * anchorX * scale + signalHorizontalGap(d);
        const y = d.verticalSide === "top"
            ? screenY(signal.position.y) - bounds.maxY * scale + signalVerticalGap(d)
            : screenY(signal.position.y) - bounds.minY * scale + signalVerticalGap(d);
        return `translate(${x},${y})scale(${scale * svgCoefScaleX},${scale})`;
    }

    const x = screenX(signal.position.x) - scale * d.coefScaleX + signalHorizontalGap(d);
    return `translate(${x},${screenY(signal.position.y) - 40 * scale * d.coefShiftY + signalVerticalGap(d)})scale(${scale * d.coefScaleX},${scale})`;
}

function getSignalTypeValue(signal) {
    return signal?.type ?? signal?.SignalType ?? signal?.signalType ?? DEFAULT_SIGNAL_TYPE;
}

function signalStyleAsset(signal) {
    return getSignalStyleAsset(getSignalTypeValue(signal));
}

function signalStyleClass(signal) {
    return signalStyleAsset(signal).className;
}

function signalStyleElements(signal) {
    return signalStyleAsset(signal).elements;
}

function signalNameX(signal) {
    return screenX(signal.position.x) + signalHorizontalGap(getSignalDirectionView(signal));
}

function signalNameY(signal) {
    const d = getSignalDirectionView(signal);
    const scale = editorDisplayStyles.value.signal.scale;
    const asset = signalStyleAsset(signal);
    if (asset.placement === "quadrant") {
        const height = signalAssetBounds(asset).height * scale;
        return screenY(signal.position.y) + signalVerticalGap(d) + (d.verticalSide === "top" ? -height - 4 : height + 12);
    }

    return screenY(signal.position.y) - 40 * scale * d.coefShiftY + 45 * scale + signalVerticalGap(d);
}

function getBufferStopDirectionValue(bufferStop) {
    return bufferStop?.direction ?? bufferStop?.Direction ?? DEFAULT_BUFFER_STOP_DIRECTION;
}

function getBufferStopTypeValue(bufferStop) {
    return bufferStop?.type ?? bufferStop?.Type ?? bufferStop?.style ?? bufferStop?.Style ?? DEFAULT_BUFFER_STOP_TYPE;
}

function bufferStopStyleAsset(bufferStop) {
    return getBufferStopStyleAsset(getBufferStopTypeValue(bufferStop));
}

function bufferStopStyleClass(bufferStop) {
    return bufferStopStyleAsset(bufferStop).className;
}

function bufferStopStyleElements(bufferStop) {
    return bufferStopStyleAsset(bufferStop).elements;
}

function bufferStopAssetWidth(bufferStop) {
    return bufferStopStyleAsset(bufferStop).width;
}

function bufferStopAssetHeight(bufferStop) {
    return bufferStopStyleAsset(bufferStop).height;
}

function bufferStopAssetY(bufferStop) {
    return -bufferStopAssetHeight(bufferStop) / 2;
}

function bufferStopShapeTransform(bufferStop) {
    return `translate(0,${bufferStopAssetY(bufferStop)})`;
}

function bufferStopLineStyle() {
    const style = editorDisplayStyles.value.track;
    return {
        fill: "none",
        stroke: style.color,
        strokeWidth: style.strokeWidth,
    };
}

function bufferStopTransform(bufferStop) {
    const direction = normalizeBufferStopDirection(getBufferStopDirectionValue(bufferStop));
    const coefScaleX = direction === "left" ? -1 : 1;
    return `translate(${screenX(bufferStop.position.x)},${screenY(bufferStop.position.y)})scale(${coefScaleX},1)`;
}

function textDisplayStyle(styleKey, selected = false) {
    const style = editorDisplayStyles.value[styleKey];
    return {
        fill: selected ? "yellow" : style.color,
        fontFamily: style.fontFamily,
        fontSize: `${style.fontSize}px`,
        fontWeight: selected ? "700" : style.fontWeight,
        fontStyle: style.fontStyle,
    };
}

function trackDisplayStyle(lineId) {
    const style = editorDisplayStyles.value.track;
    const selected = isLineSelected(lineId);
    return {
        stroke: selected ? "yellow" : style.color,
        strokeWidth: selected ? Math.max(style.strokeWidth + 2, style.strokeWidth * 2) : style.strokeWidth,
    };
}

function curveDisplayStyle() {
    const style = editorDisplayStyles.value.curve;
    return {
        stroke: style.color,
        strokeWidth: style.strokeWidth,
    };
}

function nodeDisplayStyle(nodeId) {
    if (nodeId != null && isNodeSelected(nodeId)) {
        return {
            fill: "yellow",
            stroke: "red",
            strokeWidth: 2,
        };
    }
    return {
        fill: editorDisplayStyles.value.node.color,
    };
}

function platformLineDisplayStyle(platformId) {
    const style = editorDisplayStyles.value.platform;
    const selected = isPlatformSelected(platformId);
    return {
        stroke: style.color,
        strokeWidth: selected ? style.strokeWidth + 2 : style.strokeWidth,
    };
}

function switchBranchDisplayStyle(switchId) {
    const style = editorDisplayStyles.value.switch;
    if (isSwitchSelected(switchId)) {
        return {
            stroke: "yellow",
            strokeWidth: style.strokeWidth,
        };
    }
    return {
        stroke: style.color,
        strokeWidth: style.strokeWidth,
    };
}

function lineMidpointX(line) {
    return screenX((toFiniteNumber(line.x1) + toFiniteNumber(line.x2)) / 2);
}

function lineMidpointY(line) {
    return screenY((toFiniteNumber(line.y1) + toFiniteNumber(line.y2)) / 2);
}

function getLineName(line) {
    return String(line?.name || "").trim();
}

function getLinkArrowDirection(line) {
    return String(line?.arrowDirection ?? line?.ArrowDirection ?? "").trim().toUpperCase();
}

function getLinkArrowType(line) {
    return String(line?.arrowType ?? line?.ArrowType ?? "").trim().toUpperCase();
}

function getLinkArrowCount(line) {
    const arrowType = getLinkArrowType(line);
    if (arrowType === "F") return 1;
    if (arrowType === "P") return 2;
    if (arrowType === "PF") return 3;
    if (["LO", "LI", "LIRO", "LORI", "OF"].includes(arrowType)) return 1;
    return 0;
}

function getLinkArrowSides(line) {
    const arrowDirection = getLinkArrowDirection(line);
    const arrowType = getLinkArrowType(line);
    if ((arrowType === "LIRO" || arrowType === "LORI") && arrowDirection) return ["L", "R"];
    if (arrowDirection === "L") return ["L"];
    if (arrowDirection === "R") return ["R"];
    if (arrowDirection === "LR") return ["L", "R"];
    return [];
}

function formatSvgNumber(value) {
    return Number.isFinite(value) ? Number(value.toFixed(3)) : 0;
}

function getLinkArrowTailMarkerType(arrowType, side) {
    if (arrowType === "LO") return "out";
    if (arrowType === "LI") return "in";
    if (arrowType === "LIRO") return side === "L" ? "in" : "out";
    if (arrowType === "LORI") return side === "L" ? "out" : "in";
    if (arrowType === "OF") return "oversize";
    return "";
}

function getLinkArrowTailDepth(arrowType) {
    const tailGap = linkArrowShape.tailGap;
    if (arrowType === "LI" || arrowType === "LIRO" || arrowType === "LORI") {
        return tailGap + linkArrowShape.tailLineSpacing;
    }
    if (arrowType === "OF") {
        return tailGap + linkArrowShape.tailCircleRadius * 2 + linkArrowShape.tailCircleGap;
    }
    if (arrowType === "LO") return tailGap;
    return 0;
}

function buildLinkArrowGeometry(tip, direction, arrowLength, halfWidth) {
    const base = {
        x: tip.x - direction.x * arrowLength,
        y: tip.y - direction.y * arrowLength,
    };
    const perpendicular = { x: -direction.y, y: direction.x };
    const baseA = {
        x: base.x + perpendicular.x * halfWidth,
        y: base.y + perpendicular.y * halfWidth,
    };
    const baseB = {
        x: base.x - perpendicular.x * halfWidth,
        y: base.y - perpendicular.y * halfWidth,
    };

    return {
        base,
        perpendicular,
        path: [
            "M", formatSvgNumber(tip.x), formatSvgNumber(tip.y),
            "L", formatSvgNumber(baseA.x), formatSvgNumber(baseA.y),
            "L", formatSvgNumber(baseB.x), formatSvgNumber(baseB.y),
            "Z",
        ].join(" "),
    };
}

function buildTailLine(center, perpendicular, length) {
    const halfLength = length / 2;
    return {
        x1: formatSvgNumber(center.x + perpendicular.x * halfLength),
        y1: formatSvgNumber(center.y + perpendicular.y * halfLength),
        x2: formatSvgNumber(center.x - perpendicular.x * halfLength),
        y2: formatSvgNumber(center.y - perpendicular.y * halfLength),
    };
}

function buildLinkArrowTailMarkers(markerType, base, direction, perpendicular, tailLineLength) {
    if (!markerType) {
        return { lines: [], circles: [] };
    }

    const firstMarkerCenter = {
        x: base.x - direction.x * linkArrowShape.tailGap,
        y: base.y - direction.y * linkArrowShape.tailGap,
    };
    if (markerType === "out") {
        return {
            lines: [buildTailLine(firstMarkerCenter, perpendicular, tailLineLength)],
            circles: [],
        };
    }

    if (markerType === "in") {
        const secondLineCenter = {
            x: firstMarkerCenter.x - direction.x * linkArrowShape.tailLineSpacing,
            y: firstMarkerCenter.y - direction.y * linkArrowShape.tailLineSpacing,
        };
        return {
            lines: [
                buildTailLine(firstMarkerCenter, perpendicular, tailLineLength),
                buildTailLine(secondLineCenter, perpendicular, tailLineLength),
            ],
            circles: [],
        };
    }

    if (markerType === "oversize") {
        const radius = linkArrowShape.tailCircleRadius;
        return {
            lines: [],
            circles: [{
                cx: formatSvgNumber(base.x - direction.x * (radius + linkArrowShape.tailCircleGap + linkArrowShape.tailGap)),
                cy: formatSvgNumber(base.y - direction.y * (radius + linkArrowShape.tailCircleGap + linkArrowShape.tailGap)),
                r: radius,
            }],
        };
    }

    return { lines: [], circles: [] };
}

function getLinkScreenVector(line) {
    const x1 = screenX(line?.x1);
    const y1 = screenY(line?.y1);
    const x2 = screenX(line?.x2);
    const y2 = screenY(line?.y2);
    const dx = x2 - x1;
    const dy = y2 - y1;
    const length = Math.hypot(dx, dy);
    if (!Number.isFinite(length) || length <= 0) return null;

    const visualSign = x1 < x2 || (x1 === x2 && y1 <= y2) ? 1 : -1;
    return {
        center: {
            x: (x1 + x2) / 2,
            y: (y1 + y2) / 2,
        },
        ux: (dx / length) * visualSign,
        uy: (dy / length) * visualSign,
        length,
    };
}

function buildLinkArrowViews(line) {
    const sides = getLinkArrowSides(line);
    const count = getLinkArrowCount(line);
    if (sides.length === 0 || count === 0) return [];

    const arrowType = getLinkArrowType(line);
    const vector = getLinkScreenVector(line);
    if (!vector) return [];

    const availableLength = vector.length / 2 - linkArrowShape.gap - getLinkArrowTailDepth(arrowType);
    if (availableLength < linkArrowShape.minLength) return [];

    const arrowLength = Math.min(linkArrowShape.length, availableLength / count);
    if (arrowLength < linkArrowShape.minLength) return [];

    const halfWidth = Math.min(linkArrowShape.halfWidth, arrowLength * 0.45);
    const arrows = [];

    for (const side of sides) {
        const direction = side === "L"
            ? { x: vector.ux, y: vector.uy }
            : { x: -vector.ux, y: -vector.uy };
        const nearestTip = {
            x: vector.center.x - direction.x * linkArrowShape.gap,
            y: vector.center.y - direction.y * linkArrowShape.gap,
        };

        for (let index = 0; index < count; index++) {
            const tip = {
                x: nearestTip.x - direction.x * arrowLength * index,
                y: nearestTip.y - direction.y * arrowLength * index,
            };
            const geometry = buildLinkArrowGeometry(tip, direction, arrowLength, halfWidth);
            const markerType = index === count - 1
                ? getLinkArrowTailMarkerType(arrowType, side)
                : "";
            const tailMarkers = buildLinkArrowTailMarkers(
                markerType,
                geometry.base,
                direction,
                geometry.perpendicular,
                halfWidth * 2
            );
            arrows.push({
                id: `${line.id}-arrow-${side}-${index}`,
                lineId: line.id,
                path: geometry.path,
                tailLines: tailMarkers.lines,
                tailCircles: tailMarkers.circles,
            });
        }
    }

    return arrows;
}

function curvePath(curve) {
    const start = normalizePosition(curve?.start);
    const end = normalizePosition(curve?.end);
    const radius = toFiniteNumber(curve?.radius) || defaultCurveRadius;
    const rx = Math.max(0.001, screenDeltaX(radius));
    const ry = Math.max(0.001, screenDeltaY(radius));
    const largeArcFlag = Number(curve?.largeArcFlag) === 1 ? 1 : 0;
    const sweepFlag = Number(curve?.sweepFlag) === 1 ? 1 : 0;
    return `M ${screenX(start.x)} ${screenY(start.y)} A ${rx} ${ry} 0 ${largeArcFlag} ${sweepFlag} ${screenX(end.x)} ${screenY(end.y)}`;
}

function tempSignalTransform() {
    return signalTransform({
        position: { x: tempSignal.value.x, y: tempSignal.value.y },
        direction: tempSignal.value.direction,
        type: tempSignal.value.type,
    });
}

function tempBufferStopTransform() {
    return bufferStopTransform({
        position: { x: tempBufferStop.value.x, y: tempBufferStop.value.y },
        direction: tempBufferStop.value.direction,
        type: tempBufferStop.value.type,
    });
}

function switchTransform(sw) {
    return `translate(${screenX(sw.position.x)},${screenY(sw.position.y)})`;
}

function switchBranch(sw, lineVec) {
    const len = 10;
    const displayVec = {
        x: toFiniteNumber(lineVec.x) * safeDisplayScaleX.value,
        y: toFiniteNumber(lineVec.y) * safeDisplayScaleY.value,
    };

    if (displayVec.x === 0) {
        return { x1: 0, y1: 0, x2: 0, y2: displayVec.y > 0 ? len : -len };
    }

    const k = displayVec.y / displayVec.x;
    const solx1 = Math.sqrt((len * len) / (1 + k * k));
    const soly1 = solx1 * k;
    const solx2 = -Math.sqrt((len * len) / (1 + k * k));
    const soly2 = solx2 * k;

    const innerProduct = solx1 * displayVec.x + soly1 * displayVec.y;
    const absVec = Math.hypot(displayVec.x, displayVec.y);
    const absSol = Math.hypot(solx1, soly1);
    const cos = innerProduct / (absVec * absSol);

    if (Math.abs(cos - 1) < 0.01) {
        return { x1: 0, y1: 0, x2: solx1, y2: soly1 };
    }
    return { x1: 0, y1: 0, x2: solx2, y2: soly2 };
}

onMounted(() => {
    svgRef.value?.focus({ preventScroll: true });
});

onBeforeUnmount(() => {
    removeSelectionBoxWindowListeners();
    removeAnchorInteractionWindowListeners();
    removeNodeInteractionWindowListeners();
    removeAnnotationInteractionWindowListeners();
});

defineExpose({
    editModeCode,
    drawingObject,
    mouseGridSnapModeCode,
    mouseObjectSnapModeCode,
    setEditMode,
    setDrawingObject,
    setDrawingSignalType,
    setDrawingBufferStopDirection,
    setDrawingBufferStopType,
    setMouseGridSnapModeCode,
    setMouseObjectSnapModeCode,
    clearSelectedLines,
    clearSelectedNodes,
    clearSelectedEquipment,
    deleteLine,
    deleteNode,
    deleteEquipment,
    revoke,
    redo,
    buildJsonData,
    loadDataFromJson,
    autoSeparateLine,
    markCrossPoint,
    removeCrossPoint,
    snapLine,
    autoMergeNode,
    autoGenerateNodes,
    autoGenerateSwitches,
    autoGenerateCurves,
    startDrawingSignal,
    startDrawingInsulationJoint,
    startDrawingBufferStop,
    startDrawingNode,
    startDrawingPlatform,
    updateSelectedAnnotation,
    updateSelectedEquipment,
    clearElements,
});
</script>

<template>
    <svg id="layout-editor-svg" ref="svgRef" tabindex="0" :width="svgScreenWidth" :height="svgScreenHeight"
        :style="svgStyle" @mousemove="onMouseMove" @mousedown="onMouseDown" @mouseup="onMouseUp"
        @keydown="onKeydown" @selectstart.prevent @dragstart.prevent>
        <g id="grid">
            <circle v-for="(dot, idx) in gridDots" :key="`g-${idx}`" class="griddot" :cx="screenX(dot.x)"
                :cy="screenY(dot.y)" r="0.5" />
        </g>

        <g id="linegroup">
            <line v-for="segment in renderedTrackSegments" :id="segment.id" :key="`line-${segment.id}`" class="track"
                :class="{ 'track-selected': isLineSelected(segment.line.id) }" :x1="screenX(segment.x1)"
                :y1="screenY(segment.y1)" :x2="screenX(segment.x2)" :y2="screenY(segment.y2)"
                :style="trackDisplayStyle(segment.line.id)"
                @mousedown.stop @click.stop="handleLineClick(segment.line.id)" />
            <text v-for="lineName in lineNameViews" v-show="getLineName(lineName.line)" :key="`line-name-${lineName.id}`"
                class="trackname" :class="{ 'name-selected': isLineSelected(lineName.line.id) }"
                :style="textDisplayStyle('lineName', isLineSelected(lineName.line.id))" :x="screenX(lineName.x)"
                :y="screenY(lineName.y)" @mousedown.stop @click.stop="handleLineClick(lineName.line.id)">
                {{ getLineName(lineName.line) }}
            </text>

            <path v-for="curve in displayedCurves" :id="String(curve.id)" :key="`curve-${curve.id}`" class="curve"
                :d="curvePath(curve)" :style="curveDisplayStyle()" />

            <g v-for="arrow in linkArrowViews" :key="arrow.id" class="link-arrow"
                :class="{ 'link-arrow-selected': isLineSelected(arrow.lineId) }">
                <path :d="arrow.path" />
                <line v-for="(tailLine, tailLineIndex) in arrow.tailLines" :key="`tail-line-${tailLineIndex}`"
                    class="link-arrow-tail-line" :x1="tailLine.x1" :y1="tailLine.y1" :x2="tailLine.x2"
                    :y2="tailLine.y2" />
                <circle v-for="(tailCircle, tailCircleIndex) in arrow.tailCircles"
                    :key="`tail-circle-${tailCircleIndex}`" class="link-arrow-tail-circle" :cx="tailCircle.cx"
                    :cy="tailCircle.cy" :r="tailCircle.r" />
            </g>

            <line v-if="tempLine" class="track track-temp" :x1="screenX(tempLine.x1)" :y1="screenY(tempLine.y1)"
                :x2="screenX(tempLine.x2)" :y2="screenY(tempLine.y2)"
                :style="{ strokeWidth: editorDisplayStyles.track.strokeWidth }" />

            <rect v-for="anchor in anchorRects" :id="anchor.id" :key="anchor.id" class="anchor snapobj"
                :x="anchorScreenX(anchor)" :y="anchorScreenY(anchor)" :width="anchorParam.size" :height="anchorParam.size"
                @mousedown="handleAnchorDown($event, anchor)" />

            <circle v-for="(cp, idx) in crossPoints" :key="`cp-${idx}`" class="crosspoint" :class="`rela${cp.code}`"
                :cx="screenX(cp.x)" :cy="screenY(cp.y)" r="4" />

            <circle v-if="perpendicularPoint" class="perpendicular" :cx="screenX(perpendicularPoint.x)"
                :cy="screenY(perpendicularPoint.y)" r="4" />
        </g>

        <g v-if="props.showNodes" id="nodegroup">
            <circle v-for="node in nodes" :id="node.id" :key="`node-${node.id}`" class="node snapobj"
                :class="{ 'node-selected': isNodeSelected(node.id) }" :cx="screenX(node.x)" :cy="screenY(node.y)"
                :r="editorDisplayStyles.node.radius" :style="nodeDisplayStyle(node.id)"
                @mousedown="handleNodeClick($event, node.id)" />

            <circle v-if="tempNode.visible" class="node node-temp" :cx="screenX(tempNode.x)" :cy="screenY(tempNode.y)"
                :r="editorDisplayStyles.node.radius" />
        </g>

        <g id="signalgroup">
            <g v-for="signal in signals" :id="String(signal.id)" :key="`signal-${signal.id}`"
                :class="['signal', signalStyleClass(signal), { 'signal-selected': isSignalSelected(signal.id) }]"
                :transform="signalTransform(signal)" @mousedown="handleSignalClick($event, signal.id)">
                <component :is="element.tag" v-for="(element, index) in signalStyleElements(signal)"
                    :key="`signal-element-${signal.id}-${index}`" v-bind="element.attrs" />
            </g>
            <text v-for="signal in signals" v-show="getEquipmentDisplayName(signal, 'SIGNAL')"
                :key="`signal-name-${signal.id}`" class="signalname"
                :class="{ 'name-selected': isSignalSelected(signal.id) }"
                :style="textDisplayStyle('signalName', isSignalSelected(signal.id))"
                :x="signalNameX(signal)" :y="signalNameY(signal)"
                @mousedown="handleSignalClick($event, signal.id)">
                {{ getEquipmentDisplayName(signal, "SIGNAL") }}
            </text>

            <g v-if="tempSignal.visible" id="tempsignal"
                :class="['signal', signalStyleClass(tempSignal), 'signal-temp']"
                :transform="tempSignalTransform()">
                <component :is="element.tag" v-for="(element, index) in signalStyleElements(tempSignal)"
                    :key="`temp-signal-element-${index}`" v-bind="element.attrs" />
            </g>
        </g>

        <g id="insulationjointgroup">
            <g v-for="ij in insulationJoints" :id="String(ij.id)" :key="`ij-${ij.id}`"
                class="insulationjoint insulationjoint-normal"
                :class="{ 'insulationjoint-selected': isInsulationJointSelected(ij.id) }"
                :transform="`translate(${screenX(ij.position.x)},${screenY(ij.position.y)})`"
                @mousedown="handleInsulationJointClick($event, ij.id)">
                <line x1="0" y1="-5" x2="0" y2="5" />
            </g>

            <g v-if="tempInsulationJoint.visible" id="tempinsulationjoint" class="insulationjoint insulationjoint-temp">
                <line :x1="screenX(tempInsulationJoint.x)" :x2="screenX(tempInsulationJoint.x)"
                    :y1="screenY(tempInsulationJoint.y) - 5" :y2="screenY(tempInsulationJoint.y) + 5" />
            </g>
        </g>

        <g id="bufferstopgroup">
            <g v-for="bufferStop in bufferStops" :id="String(bufferStop.id)" :key="`buffer-stop-${bufferStop.id}`"
                :class="['bufferstop', bufferStopStyleClass(bufferStop), { 'bufferstop-selected': isBufferStopSelected(bufferStop.id) }]"
                :transform="bufferStopTransform(bufferStop)" @mousedown="handleBufferStopClick($event, bufferStop.id)">
                <g class="bufferstop-shape" :style="bufferStopLineStyle()"
                    :transform="bufferStopShapeTransform(bufferStop)">
                    <component :is="element.tag" v-for="(element, index) in bufferStopStyleElements(bufferStop)"
                        :key="`buffer-stop-element-${bufferStop.id}-${index}`" v-bind="element.attrs" />
                </g>
                <rect v-if="isBufferStopSelected(bufferStop.id)" class="bufferstop-selection" x="0"
                    :y="bufferStopAssetY(bufferStop)" :width="bufferStopAssetWidth(bufferStop)"
                    :height="bufferStopAssetHeight(bufferStop)" />
            </g>

            <g v-if="tempBufferStop.visible" id="tempbufferstop"
                :class="['bufferstop', bufferStopStyleClass(tempBufferStop), 'bufferstop-temp']"
                :transform="tempBufferStopTransform()">
                <g class="bufferstop-shape" :style="bufferStopLineStyle()"
                    :transform="bufferStopShapeTransform(tempBufferStop)">
                    <component :is="element.tag" v-for="(element, index) in bufferStopStyleElements(tempBufferStop)"
                        :key="`temp-buffer-stop-element-${index}`" v-bind="element.attrs" />
                </g>
            </g>
        </g>

        <g id="switchgroup">
            <g v-for="sw in switches" :id="String(sw.id)" :key="`sw-${sw.id}`" class="switch"
                :class="{ 'switch-selected': isSwitchSelected(sw.id) }" :transform="switchTransform(sw)"
                @mousedown="handleSwitchClick($event, sw.id)">
                <line v-for="(lineVec, idx) in sw.branchVectorList" :key="`sw-line-${sw.id}-${idx}`"
                    class="switchbranch" :x1="switchBranch(sw, lineVec).x1" :y1="switchBranch(sw, lineVec).y1"
                    :x2="switchBranch(sw, lineVec).x2" :y2="switchBranch(sw, lineVec).y2"
                    :style="switchBranchDisplayStyle(sw.id)" />
                <text class="switchname" :class="{ 'name-selected': isSwitchSelected(sw.id) }"
                    :style="textDisplayStyle('switchName', isSwitchSelected(sw.id))" x="4" y="-4">
                    {{ getEquipmentDisplayName(sw, "SWITCH") }}
                </text>
            </g>
        </g>

        <g id="platformgroup">
            <g v-for="platform in platforms" :id="String(platform.id)" :key="`platform-${platform.id}`" class="platform"
                :class="{ 'platform-selected': isPlatformSelected(platform.id) }"
                @mousedown="handlePlatformClick($event, platform.id)">
                <rect :x="screenX(platform.x)" :y="screenY(platform.y)" :width="screenDeltaX(platform.width)"
                    :height="screenDeltaY(platform.height)" :style="platformLineDisplayStyle(platform.id)" />
                <text class="platformname" :x="screenCenterX(platform.x, platform.width)"
                    :y="screenCenterY(platform.y, platform.height)"
                    :class="{ 'name-selected': isPlatformSelected(platform.id) }"
                    :style="textDisplayStyle('platformName', isPlatformSelected(platform.id))">
                    {{ getEquipmentDisplayName(platform, "PLATFORM") }}
                </text>
            </g>

            <rect class="platform platform-temp" :x="screenX(tempPlatformView.x)" :y="screenY(tempPlatformView.y)"
                :width="screenDeltaX(tempPlatformView.width)" :height="screenDeltaY(tempPlatformView.height)"
                :style="platformLineDisplayStyle()" />
        </g>

        <g id="annotationgroup">
            <g v-for="annotation in annotations" :id="String(annotation.id)" :key="`annotation-${annotation.id}`"
                class="annotation" :class="{ 'annotation-selected': isAnnotationSelected(annotation.id) }"
                :transform="annotationTransform(annotation)" @mousedown="handleAnnotationClick($event, annotation.id)">
                <text class="annotation-text" x="0" y="0" :font-family="annotation.fontFamily"
                    :font-size="annotation.fontSize" :font-weight="annotation.fontWeight" :font-style="annotation.fontStyle"
                    :class="{ 'name-selected': isAnnotationSelected(annotation.id) }"
                    :fill="isAnnotationSelected(annotation.id) ? 'yellow' : annotation.textColor">
                    {{ annotation.text }}
                </text>
                <circle v-if="shouldShowAnnotationControls(annotation)" class="annotation-text-anchor" cx="0" cy="0"
                    :r="annotationTextAnchorRadius" @mousedown="beginAnnotationTextMove($event, annotation.id)" />
            </g>
        </g>

        <g id="cursor">
            <rect v-if="selectionBoxView.visible" class="selection-box" :x="selectionBoxView.x"
                :y="selectionBoxView.y" :width="selectionBoxView.width" :height="selectionBoxView.height" />
            <g v-if="editModeCode === 1 && drawingObject === 's'" class="drawing-hint signal-direction-hint">
                <rect x="12" y="12" width="245" height="28" rx="4" />
                <text x="24" y="31">方向: 按 w / e / s / d 设置</text>
            </g>
            <template v-if="editModeCode !== 0">
                <rect class="cursor" :x="screenX(cursorParam.x) - cursorParam.size / 2"
                    :y="screenY(cursorParam.y) - cursorParam.size / 2" :width="cursorParam.size"
                    :height="cursorParam.size" />
                <line id="cursorlineh" class="cursor" :x1="screenX(cursorParam.x) - cursorParam.barLength / 2"
                    :x2="screenX(cursorParam.x) + cursorParam.barLength / 2" :y1="screenY(cursorParam.y)"
                    :y2="screenY(cursorParam.y)" />
                <line id="cursorlinev" class="cursor" :x1="screenX(cursorParam.x)" :x2="screenX(cursorParam.x)"
                    :y1="screenY(cursorParam.y) - cursorParam.barLength / 2"
                    :y2="screenY(cursorParam.y) + cursorParam.barLength / 2" />
                <g v-if="drawingObject === 's'" class="signal-direction-compass"
                    :transform="`translate(${screenX(cursorParam.x)},${screenY(cursorParam.y)})`">
                    <text v-for="keyView in signalDirectionKeyViews" :key="keyView.key" :x="keyView.x"
                        :y="keyView.y">
                        {{ keyView.label }}
                    </text>
                </g>
            </template>
        </g>
    </svg>
</template>

<style scoped>
@import "./StationLayoutEditor.css";
</style>
