<script setup>
import { computed, onBeforeUnmount, onMounted, ref } from "vue";

const props = defineProps({
    width: { type: Number, default: 1920 },
    height: { type: Number, default: 1080 },
    displayScaleX: { type: Number, default: 1 },
    displayScaleY: { type: Number, default: 1 },
});
const emit = defineEmits(["selected-annotation-change", "selected-equipment-change"]);

const svgRef = ref(null);

const editModeCode = ref(0);
const drawingObject = ref("l");
const mouseGridSnapModeCode = ref(1);
const mouseObjectSnapModeCode = ref(1);
const snapDistance = ref(10);
const autoSeparateLineTolerance = ref(10);
const autoMergeNodeTolerance = ref(10);
const defaultCurveRadius = 100;
const curveCornerMinAngle = 90;
const curveCornerMaxAngle = 160;

const grid = { visible: true, verticalSpace: 20, horizontalSpace: 20 };
const cursorParam = ref({ size: 10, barVisible: false, barLength: 100, x: 200, y: 200 });
const anchorParam = { size: 10 };
const safeDisplayScaleX = computed(() => normalizeDisplayScale(props.displayScaleX));
const safeDisplayScaleY = computed(() => normalizeDisplayScale(props.displayScaleY));
const svgScreenWidth = computed(() => props.width * safeDisplayScaleX.value);
const svgScreenHeight = computed(() => props.height * safeDisplayScaleY.value);
const svgStyle = computed(() => ({
    width: `${svgScreenWidth.value}px`,
    height: `${svgScreenHeight.value}px`,
}));

const latestElementID = ref(0);
const layoutMetadata = ref({});

const tracks = ref([]);
const curves = ref([]);
const nodes = ref([]);
const signals = ref([]);
const insulationJoints = ref([]);
const platforms = ref([]);
const switches = ref([]);
const annotations = ref([]);

const selectedLineIds = ref(new Set());
const selectedNodeIds = ref(new Set());
const selectedSignalIds = ref(new Set());
const selectedInsulationJointIds = ref(new Set());
const selectedSwitchIds = ref(new Set());
const selectedPlatformIds = ref(new Set());
const selectedAnnotationIds = ref(new Set());

const crossPoints = ref([]);
const perpendicularPoint = ref(null);

const tempLine = ref(null);
const tempSignal = ref({ visible: false, direction: "w", x: 0, y: 0 });
const tempInsulationJoint = ref({ visible: false, x: 0, y: 0 });
const tempNode = ref({ visible: false, x: 0, y: 0 });
const tempPlatformPosition = ref(null);
const selectionBox = ref(null);
const selectionBoxDragThreshold = 4;
const annotationTextAnchorRadius = 5;

const movingAnchor = ref(null);
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

function toFiniteNumber(value) {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : 0;
}

function screenX(value) {
    return toFiniteNumber(value) * safeDisplayScaleX.value;
}

function screenY(value) {
    return toFiniteNumber(value) * safeDisplayScaleY.value;
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
    return toFiniteNumber(value) / safeDisplayScaleX.value;
}

function dataY(value) {
    return toFiniteNumber(value) / safeDisplayScaleY.value;
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
    if (kind === "switch") return switches.value;
    if (kind === "platform") return platforms.value;
    return [];
}

function getEquipmentSelectedSet(kind) {
    if (kind === "link") return selectedLineIds.value;
    if (kind === "signal") return selectedSignalIds.value;
    if (kind === "insulationJoint") return selectedInsulationJointIds.value;
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
    for (const kind of ["link", "signal", "insulationJoint", "switch", "platform"]) {
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
        const cp = {
            id: `cp${line.id}`,
            lineId: line.id,
            type: "cp",
            x: (Number(line.x1) + Number(line.x2)) / 2 - half,
            y: (Number(line.y1) + Number(line.y2)) / 2 - half,
        };
        list.push(sp, ep, cp);
    }
    return list;
});

const gridDots = computed(() => {
    const dots = [];
    if (!grid.visible) return dots;
    for (let x = 0; x < props.width; x += grid.verticalSpace) {
        for (let y = 0; y < props.height; y += grid.horizontalSpace) {
            dots.push({ x, y });
        }
    }
    return dots;
});

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

    for (const curve of curves.value) {
        addCurveHiddenRange(hiddenRangesByLineID, lineByID, nodeByID, curve, "tangentLinkID1", "start");
        addCurveHiddenRange(hiddenRangesByLineID, lineByID, nodeByID, curve, "tangentLinkID2", "end");
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
            platforms: platforms.value,
            switches: switches.value,
            annotations: annotations.value,
            selectedLineIds: [...selectedLineIds.value],
            selectedNodeIds: [...selectedNodeIds.value],
            selectedSignalIds: [...selectedSignalIds.value],
            selectedInsulationJointIds: [...selectedInsulationJointIds.value],
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
    platforms.value = (state.platforms || []).map((platform) => normalizeNamedEquipment(platform));
    switches.value = (state.switches || []).map((sw) => normalizeNamedEquipment(sw));
    annotations.value = state.annotations || [];
    selectedLineIds.value = new Set(state.selectedLineIds || []);
    selectedNodeIds.value = new Set(state.selectedNodeIds || []);
    selectedSignalIds.value = new Set(state.selectedSignalIds || []);
    selectedInsulationJointIds.value = new Set(state.selectedInsulationJointIds || []);
    selectedSwitchIds.value = new Set(state.selectedSwitchIds || []);
    selectedPlatformIds.value = new Set(state.selectedPlatformIds || []);
    selectedAnnotationIds.value = new Set(state.selectedAnnotationIds || []);
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

function getEquipmentDisplayName(equipment, placeholder) {
    const name = equipment?.name == null ? "" : String(equipment.name).trim();
    if (name) return name;
    const id = equipment?.id == null ? "" : String(equipment.id).trim();
    return id || placeholder;
}

function clearSelectedLines() {
    selectedLineIds.value = new Set();
    movingAnchor.value = null;
    emitSelectedEquipmentChange();
}

function clearSelectedNodes() {
    selectedNodeIds.value = new Set();
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

function setDrawingObject(obj) {
    drawingObject.value = obj;
    if (obj === "s") {
        startDrawingSignal();
    } else if (obj === "i") {
        startDrawingInsulationJoint();
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

function getSnapObjects() {
    const list = [];
    for (const n of nodes.value) {
        list.push({ x: Number(n.x), y: Number(n.y) });
    }
    for (const a of anchorRects.value) {
        list.push({ x: Number(a.x) + anchorParam.size / 2, y: Number(a.y) + anchorParam.size / 2 });
    }
    return list;
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

function updateCursorPosition(clientX, clientY) {
    const dataPoint = clientPointToDataPoint(clientX, clientY);
    if (!dataPoint) return;
    const x = dataPoint.x;
    const y = dataPoint.y;

    let snapX = x;
    let snapY = y;

    if (mouseGridSnapModeCode.value === 1) {
        snapX = Math.round(x / grid.horizontalSpace) * grid.horizontalSpace;
        snapY = Math.round(y / grid.verticalSpace) * grid.verticalSpace;
    }

    if (mouseObjectSnapModeCode.value === 1) {
        const snapObjs = getSnapObjects();
        for (const obj of snapObjs) {
            const dist = Math.hypot(obj.x - x, obj.y - y);
            if (dist <= snapDistance.value) {
                snapX = obj.x;
                snapY = obj.y;
                break;
            }
        }
    }

    cursorParam.value.x = snapX;
    cursorParam.value.y = snapY;

    if (editModeCode.value === 1) {
        snapPointToLine(x, y);
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
    selectedSwitchIds.value = nextSwitchIds;
    selectedPlatformIds.value = nextPlatformIds;
    selectedAnnotationIds.value = nextAnnotationIds;
    movingAnchor.value = null;
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
        curves.value = [];
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
        const deletedLineIds = new Set(selectedLineIds.value);
        tracks.value = tracks.value.filter((line) => !selectedLineIds.value.has(line.id));
        curves.value = curves.value.filter((curve) =>
            !deletedLineIds.has(curve.tangentLinkID1) &&
            !deletedLineIds.has(curve.tangentLinkID2));
        selectedLineIds.value = new Set();
        movingAnchor.value = null;
    });
    emitSelectedEquipmentChange();
}

function deleteNode() {
    if (selectedNodeIds.value.size === 0) return;
    executeMutation(() => {
        const deletedNodeIds = new Set(selectedNodeIds.value);
        nodes.value = nodes.value.filter((n) => !selectedNodeIds.value.has(n.id));
        curves.value = curves.value.filter((curve) => !deletedNodeIds.has(curve.nodeID));
        selectedNodeIds.value = new Set();
    });
}

function deleteEquipment() {
    executeMutation(() => {
        signals.value = signals.value.filter((s) => !selectedSignalIds.value.has(s.id));
        insulationJoints.value = insulationJoints.value.filter((i) => !selectedInsulationJointIds.value.has(i.id));
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

        curves.value = [];
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
        movingAnchor.value = null;
        curves.value = [];
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
        curves.value = [];
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
    const bindingNode = nodes.value.find((n) => Number(n.x) === Number(x) && Number(n.y) === Number(y));
    if (!bindingNode) return;
    executeMutation(() => {
        const id = nextId();
        signals.value.push({
            id,
            name: id,
            type: "departure",
            position: { x, y },
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
    const bindingNode = nodes.value.find((n) => Number(n.x) === Number(x) && Number(n.y) === Number(y));
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
        const n = { id: nextId(), x, y, adjacentLineIDList: [`${minDistLine.id}s1`, `${minDistLine.id}s2`] };

        const l1 = {
            id: `${minDistLine.id}s1`,
            x1: minDistLine.x1,
            y1: minDistLine.y1,
            x2: x,
            y2: y,
            fromNodeID: minDistLine.fromNodeID,
            toNodeID: n.id,
        };
        const l2 = {
            id: `${minDistLine.id}s2`,
            x1: x,
            y1: y,
            x2: minDistLine.x2,
            y2: minDistLine.y2,
            fromNodeID: n.id,
            toNodeID: minDistLine.toNodeID,
        };

        tracks.value = tracks.value.filter((line) => line.id !== minDistLine.id);
        tracks.value.push(l1, l2);
        nodes.value.push(n);
        curves.value = [];
    });
}

function autoGenerateNodes() {
    executeMutation(() => {
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
        curves.value = [];
    });
}

function buildSwitchBranchVectorList(node) {
    const adjacentLines = tracks.value.filter((line) => (node.adjacentLineIDList || []).includes(line.id));
    const vectorList = [];

    for (const line of adjacentLines) {
        if (line.fromNodeID === node.id) {
            vectorList.push({ x: Number(line.x2) - Number(line.x1), y: Number(line.y2) - Number(line.y1), lineID: line.id });
        } else {
            vectorList.push({ x: Number(line.x1) - Number(line.x2), y: Number(line.y1) - Number(line.y2), lineID: line.id });
        }
    }

    return vectorList;
}

function buildSwitch(node) {
    const vectorList = buildSwitchBranchVectorList(node);

    let acuteAngleNum = 0;
    let obtuseAngleNum = 0;
    let isSingleSwitch = false;

    for (let i = 0; i < vectorList.length; i += 1) {
        for (let j = i + 1; j < vectorList.length; j += 1) {
            const innerProduct = vectorList[i].x * vectorList[j].x + vectorList[i].y * vectorList[j].y;
            const absI = Math.hypot(vectorList[i].x, vectorList[i].y);
            const absJ = Math.hypot(vectorList[j].x, vectorList[j].y);
            const cos = innerProduct / (absI * absJ);
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

    const id = nextId();
    return {
        id,
        name: id,
        type: switchType,
        position: { x: node.x, y: node.y },
        bindingNodeID: node.id,
        branchVectorList: vectorList,
    };
}

function autoGenerateSwitches() {
    executeMutation(() => {
        const generated = [];
        for (const n of nodes.value) {
            if (n.adjacentLineIDList.length === 3 || n.adjacentLineIDList.length === 4) {
                generated.push(buildSwitch(n));
            }
        }
        switches.value = generated;
    });
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

function buildCurveForCorner(node, line1, line2, radius = defaultCurveRadius) {
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

    const tangentDistance = radius / Math.tan(angle / 2);
    if (!Number.isFinite(tangentDistance) || tangentDistance <= 0) return null;
    if (tangentDistance > len1 || tangentDistance > len2) return null;

    const bisector = {
        x: unit1.x + unit2.x,
        y: unit1.y + unit2.y,
    };
    const bisectorLength = Math.hypot(bisector.x, bisector.y);
    if (bisectorLength <= 0) return null;

    const centerDistance = radius / Math.sin(angle / 2);
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
        id: nextId(),
        nodeID: node.id,
        tangentLinkID1: line1.id,
        tangentLinkID2: line2.id,
        radius,
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
    platforms.value = [];
    switches.value = [];
    annotations.value = [];
    clearSelectedLines();
    clearSelectedNodes();
    clearSelectedEquipment();
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
        platforms.value = (jsonObj?.platforms || []).map((platform) => normalizeNamedEquipment(platform));
        switches.value = (jsonObj?.switches || []).map((sw) => normalizeNamedEquipment(sw));
        annotations.value = (jsonObj?.annotations || []).map((annotation) => normalizeAnnotation(annotation));
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
    if (!shouldHandleElementMouseDown(event)) return;
    clearSelectedDeviceIds();
    setSelectedAnnotationIds([]);
    emitSelectedEquipmentChange();
    selectedNodeIds.value = new Set([...selectedNodeIds.value, nodeId]);
}

function selectEquipment(kind, id, additive = false) {
    if (!additive) {
        clearSelectedDeviceIds();
    }

    const targetSet = new Set(additive ? [...getEquipmentSelectedSet(kind)] : []);
    targetSet.add(id);
    if (kind === "signal") selectedSignalIds.value = targetSet;
    if (kind === "insulationJoint") selectedInsulationJointIds.value = targetSet;
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

        if (kind === "link" && previousLinkState) {
            syncLinkEndpointNodes(target, previousLinkState);
            curves.value = [];
        }

        if (kind === "link" && patch.id != null && patch.id !== previousId) {
            updateLinkReferences(previousId, patch.id);
            selectedLineIds.value = new Set([patch.id]);
        } else if (patch.id != null && patch.id !== id) {
            selectEquipment(kind, patch.id, false);
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

function handleAnchorDown(event, anchor) {
    if (!shouldHandleElementMouseDown(event)) return;
    movingAnchor.value = anchor;
}

function moveSelectedAnchor(x, y) {
    if (!movingAnchor.value) return;
    executeMutation(() => {
        const line = tracks.value.find((item) => item.id === movingAnchor.value.lineId);
        if (!line) return;

        if (movingAnchor.value.type === "sp") {
            line.x1 = x;
            line.y1 = y;
        } else if (movingAnchor.value.type === "ep") {
            line.x2 = x;
            line.y2 = y;
        } else {
            const oldCX = (Number(line.x1) + Number(line.x2)) / 2;
            const oldCY = (Number(line.y1) + Number(line.y2)) / 2;
            const dx = x - oldCX;
            const dy = y - oldCY;
            line.x1 = Number(line.x1) + dx;
            line.y1 = Number(line.y1) + dy;
            line.x2 = Number(line.x2) + dx;
            line.y2 = Number(line.y2) + dy;
        }
    });
    movingAnchor.value = null;
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
        if (movingAnchor.value) {
            moveSelectedAnchor(x, y);
            return;
        }
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

function isSwitchSelected(id) {
    return selectedSwitchIds.value.has(id);
}

function isPlatformSelected(id) {
    return selectedPlatformIds.value.has(id);
}

function isAnnotationSelected(id) {
    return selectedAnnotationIds.value.has(id);
}

function signalTransform(signal) {
    const directionView = {
        e: { coefScaleX: 1, coefShiftY: 1 },
        w: { coefScaleX: -1, coefShiftY: 1 },
        s: { coefScaleX: -1, coefShiftY: 0 },
        d: { coefScaleX: 1, coefShiftY: 0 },
    };
    const d = directionView[signal.direction || "e"];
    return `translate(${screenX(signal.position.x)},${screenY(signal.position.y) - 20 * d.coefShiftY})scale(${0.5 * d.coefScaleX},0.5)`;
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
    return signalTransform({ position: { x: tempSignal.value.x, y: tempSignal.value.y }, direction: tempSignal.value.direction });
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
    removeAnnotationInteractionWindowListeners();
});

defineExpose({
    editModeCode,
    drawingObject,
    mouseGridSnapModeCode,
    mouseObjectSnapModeCode,
    setEditMode,
    setDrawingObject,
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
        @keydown="onKeydown">
        <g id="grid">
            <circle v-for="(dot, idx) in gridDots" :key="`g-${idx}`" class="griddot" :cx="screenX(dot.x)"
                :cy="screenY(dot.y)" r="0.5" />
        </g>

        <g id="linegroup">
            <line v-for="segment in renderedTrackSegments" :id="segment.id" :key="`line-${segment.id}`" class="track"
                :class="{ 'track-selected': isLineSelected(segment.line.id) }" :x1="screenX(segment.x1)"
                :y1="screenY(segment.y1)" :x2="screenX(segment.x2)" :y2="screenY(segment.y2)"
                @mousedown.stop @click.stop="handleLineClick(segment.line.id)" />
            <text v-for="lineName in lineNameViews" v-show="getLineName(lineName.line)" :key="`line-name-${lineName.id}`"
                class="trackname" :x="screenX(lineName.x)" :y="screenY(lineName.y)" @mousedown.stop
                @click.stop="handleLineClick(lineName.line.id)">
                {{ getLineName(lineName.line) }}
            </text>

            <path v-for="curve in curves" :id="String(curve.id)" :key="`curve-${curve.id}`" class="curve"
                :d="curvePath(curve)" />

            <line v-if="tempLine" class="track track-temp" :x1="screenX(tempLine.x1)" :y1="screenY(tempLine.y1)"
                :x2="screenX(tempLine.x2)" :y2="screenY(tempLine.y2)" />

            <rect v-for="anchor in anchorRects" :id="anchor.id" :key="anchor.id" class="anchor snapobj"
                :x="anchorScreenX(anchor)" :y="anchorScreenY(anchor)" :width="anchorParam.size" :height="anchorParam.size"
                @mousedown="handleAnchorDown($event, anchor)" />

            <circle v-for="(cp, idx) in crossPoints" :key="`cp-${idx}`" class="crosspoint" :class="`rela${cp.code}`"
                :cx="screenX(cp.x)" :cy="screenY(cp.y)" r="4" />

            <circle v-if="perpendicularPoint" class="perpendicular" :cx="screenX(perpendicularPoint.x)"
                :cy="screenY(perpendicularPoint.y)" r="4" />
        </g>

        <g id="nodegroup">
            <circle v-for="node in nodes" :id="node.id" :key="`node-${node.id}`" class="node snapobj"
                :class="{ 'node-selected': isNodeSelected(node.id) }" :cx="screenX(node.x)" :cy="screenY(node.y)" r="5"
                @mousedown="handleNodeClick($event, node.id)" />

            <circle v-if="tempNode.visible" class="node node-temp" :cx="screenX(tempNode.x)" :cy="screenY(tempNode.y)"
                r="5" />
        </g>

        <g id="signalgroup">
            <g v-for="signal in signals" :id="String(signal.id)" :key="`signal-${signal.id}`"
                class="signal signal-departure" :class="{ 'signal-selected': isSignalSelected(signal.id) }"
                :transform="signalTransform(signal)" @mousedown="handleSignalClick($event, signal.id)">
                <circle cx="38" cy="17" r="16" style="fill:#fff;" />
                <circle cx="38" cy="17" r="8" style="fill:#fff;" />
                <circle cx="70" cy="17" r="16" style="fill:#009a3e;" />
                <circle cx="103" cy="17" r="16" style="fill:#e60012;" />
                <line x1="22" y1="17" x2="1" y2="17" style="fill:none;" />
                <line x1="1" y1="1" x2="1" y2="33" style="fill:none;" />
                <text class="signalname" x="0" y="45">{{ getEquipmentDisplayName(signal, "SIGNAL") }}</text>
            </g>

            <g v-if="tempSignal.visible" id="tempsignal" class="signal signal-departure signal-temp"
                :transform="tempSignalTransform()">
                <circle cx="38" cy="17" r="16" style="fill:#fff;" />
                <circle cx="38" cy="17" r="8" style="fill:#fff;" />
                <circle cx="70" cy="17" r="16" style="fill:#009a3e;" />
                <circle cx="103" cy="17" r="16" style="fill:#e60012;" />
                <line x1="22" y1="17" x2="1" y2="17" style="fill:none;" />
                <line x1="1" y1="1" x2="1" y2="33" style="fill:none;" />
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

        <g id="switchgroup">
            <g v-for="sw in switches" :id="String(sw.id)" :key="`sw-${sw.id}`" class="switch"
                :class="{ 'switch-selected': isSwitchSelected(sw.id) }" :transform="switchTransform(sw)"
                @mousedown="handleSwitchClick($event, sw.id)">
                <line v-for="(lineVec, idx) in sw.branchVectorList" :key="`sw-line-${sw.id}-${idx}`"
                    class="switchbranch" :x1="switchBranch(sw, lineVec).x1" :y1="switchBranch(sw, lineVec).y1"
                    :x2="switchBranch(sw, lineVec).x2" :y2="switchBranch(sw, lineVec).y2" />
                <text class="switchname" x="4" y="-4">{{ getEquipmentDisplayName(sw, "SWITCH") }}</text>
            </g>
        </g>

        <g id="platformgroup">
            <g v-for="platform in platforms" :id="String(platform.id)" :key="`platform-${platform.id}`" class="platform"
                :class="{ 'platform-selected': isPlatformSelected(platform.id) }"
                @mousedown="handlePlatformClick($event, platform.id)">
                <rect :x="screenX(platform.x)" :y="screenY(platform.y)" :width="screenDeltaX(platform.width)"
                    :height="screenDeltaY(platform.height)" />
                <text class="platformname" :x="screenCenterX(platform.x, platform.width)"
                    :y="screenCenterY(platform.y, platform.height)">
                    {{ getEquipmentDisplayName(platform, "PLATFORM") }}
                </text>
            </g>

            <rect class="platform platform-temp" :x="screenX(tempPlatformView.x)" :y="screenY(tempPlatformView.y)"
                :width="screenDeltaX(tempPlatformView.width)" :height="screenDeltaY(tempPlatformView.height)" />
        </g>

        <g id="annotationgroup">
            <g v-for="annotation in annotations" :id="String(annotation.id)" :key="`annotation-${annotation.id}`"
                class="annotation" :class="{ 'annotation-selected': isAnnotationSelected(annotation.id) }"
                :transform="annotationTransform(annotation)" @mousedown="handleAnnotationClick($event, annotation.id)">
                <text class="annotation-text" x="0" y="0" :font-family="annotation.fontFamily"
                    :font-size="annotation.fontSize" :font-weight="annotation.fontWeight" :font-style="annotation.fontStyle"
                    :fill="annotation.textColor">
                    {{ annotation.text }}
                </text>
                <circle v-if="shouldShowAnnotationControls(annotation)" class="annotation-text-anchor" cx="0" cy="0"
                    :r="annotationTextAnchorRadius" @mousedown="beginAnnotationTextMove($event, annotation.id)" />
            </g>
        </g>

        <g id="cursor">
            <rect v-if="selectionBoxView.visible" class="selection-box" :x="selectionBoxView.x"
                :y="selectionBoxView.y" :width="selectionBoxView.width" :height="selectionBoxView.height" />
            <rect class="cursor" :x="screenX(cursorParam.x) - cursorParam.size / 2"
                :y="screenY(cursorParam.y) - cursorParam.size / 2" :width="cursorParam.size"
                :height="cursorParam.size" />
            <line id="cursorlineh" class="cursor" :x1="screenX(cursorParam.x) - cursorParam.barLength / 2"
                :x2="screenX(cursorParam.x) + cursorParam.barLength / 2" :y1="screenY(cursorParam.y)"
                :y2="screenY(cursorParam.y)" />
            <line id="cursorlinev" class="cursor" :x1="screenX(cursorParam.x)" :x2="screenX(cursorParam.x)"
                :y1="screenY(cursorParam.y) - cursorParam.barLength / 2"
                :y2="screenY(cursorParam.y) + cursorParam.barLength / 2" />
        </g>
    </svg>
</template>

<style scoped>
@import "./StationLayoutEditor.css";
</style>
