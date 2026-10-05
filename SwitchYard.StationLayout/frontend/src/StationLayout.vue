<script setup>
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from "vue";
import { ElMessage, ElMessageBox } from "element-plus";
import {
    DEFAULT_BUFFER_STOP_DIRECTION,
    DEFAULT_BUFFER_STOP_TYPE,
    bufferStopDirectionOptions as baseBufferStopDirectionOptions,
    bufferStopTypeOptions as baseBufferStopTypeOptions,
} from "./assets/stationLayoutBufferStopStyles";
import { DEFAULT_SIGNAL_TYPE, normalizeSignalType, signalTypeMenuOptions, signalTypeOptions } from "./assets/stationLayoutSignalStyles";
import StationLayoutEditor from "./components/StationLayoutEditor.vue";
import StationLayoutEditToolbar from "./components/StationLayoutEditToolbar.vue";
import { createStationLayoutTranslator } from "./messages";
import { isStationLayoutArchive, parseStationLayoutJson, serializeStationLayoutJson, validateStationLayoutJson, remapStationLayoutIds } from "./layoutJson";
import {
    Download, Upload, Aim, Hide, Connection, Scissor,
    Share, SetUp,
    Pointer, EditPen,
    Minus, Location, Bell, Switch, Filter, Guide, Stopwatch, Platform,
    Grid, Magnet,
    RefreshLeft, RefreshRight,
    CircleClose, Delete, ArrowDown
} from '@element-plus/icons-vue';

const DEFAULT_GRID_SPACING = 20;
const MAX_GRID_SPACING = 500;
const TEMP_CELL_ID_PREFIX = "TEMP_CELL_";
const BOUND_EQUIPMENT_KINDS = ["signal", "switch", "insulationJoint", "bufferStop"];
const props = defineProps({
    selectedInstanceId: {
        type: String,
        default: "",
    },
    stationSchemeId: {
        type: String,
        default: undefined,
    },
    gateway: {
        type: Object,
        required: true,
    },
    translate: {
        type: Function,
        default: null,
    },
    formatError: {
        type: Function,
        default: null,
    },
    readonly: {
        type: Boolean,
        default: false,
    },
});
const emit = defineEmits(['update:stationSchemeId']);
const fallbackTranslate = createStationLayoutTranslator("zh");
const t = (key, parameters) => props.translate?.(key, parameters) ?? fallbackTranslate(key, parameters);

function stringifyErrorValue(value, seen = new Set()) {
    if (typeof value === "string") return value.trim();
    if (value instanceof Error && value.message) return String(value.message).trim();
    if (!value || typeof value !== "object") return "";
    if (seen.has(value)) return "";
    seen.add(value);

    if (Array.isArray(value)) {
        for (const item of value) {
            const message = stringifyErrorValue(item, seen);
            if (message) return message;
        }
        return "";
    }

    for (const key of ["message", "detail", "title", "error", "errors", "data", "body", "payload", "response", "cause"]) {
        const message = stringifyErrorValue(value[key], seen);
        if (message) return message;
    }

    try {
        const serialized = JSON.stringify(value);
        return serialized && serialized !== "{}" ? serialized : "";
    } catch {
        return "";
    }
}

function defaultFormatError(error, fallback) {
    return stringifyErrorValue(error) || fallback;
}

function getHttpErrorMessage(error, fallback) {
    try {
        const customMessage = props.formatError?.(error, fallback);
        if (typeof customMessage === "string" && customMessage.trim()) {
            return customMessage.trim();
        }
    } catch (formatError) {
        console.warn("StationLayout formatError failed:", formatError);
    }
    return defaultFormatError(error, fallback);
}

function ensureWritable(options = {}) {
    if (!props.readonly) return true;
    if (options?.silent !== true) {
        ElMessage.warning(t("stationLayout.messages.readonly"));
    }
    return false;
}
const stationLayoutEditorRef = ref(null);
const stationLayoutEditorFrameRef = ref(null);
const signalDropdownRef = ref(null);
const extractDwgDialogVisible = ref(false);
const dwgFileInputRef = ref(null);
const importJsonFileInputRef = ref(null);
const selectedDwgFile = ref(null);
const dwgLayerName = ref("0");
const extractingDwg = ref(false);
const loadingData = ref(false);
const importingData = ref(false);
let layoutLoadVersion = 0;
let pendingLoadedLayoutFit = false;
let loadedLayoutResizeObserver = null;
const savingData = ref(false);
const editToolbarDensity = ref("compact");
const currentStationSchemeId = ref(props.stationSchemeId || "");
const loadingStationSchemes = ref(false);
const stationSchemeOptions = ref([]);
const stationSchemeManagerVisible = ref(false);
const stationSchemeManagerSaving = ref(false);
const stationSchemeDraft = ref(null);
const stationSchemeDraftInputRef = ref(null);
const stationSchemeManagerRows = computed(() => stationSchemeDraft.value
    ? [stationSchemeDraft.value, ...stationSchemeOptions.value]
    : stationSchemeOptions.value);
const editingStationSchemeOriginalId = ref("");
const editingStationSchemeForm = ref({ name: "" });
const layoutScaleX = ref(1);
const layoutScaleY = ref(1);
const showCurveArc = ref(true);
const showNodes = ref(true);
const showCellNames = ref(true);
const showGrid = ref(true);
const gridSpacing = ref(DEFAULT_GRID_SPACING);
const layoutStyleDialogVisible = ref(false);
const bindingCorrectionDialogVisible = ref(false);
const bindingCorrectionTableRef = ref(null);
const bindingCorrectionPlan = ref(null);
const bindingCorrectionRows = ref([]);
const selectedBindingCorrectionRows = ref([]);
const layoutScaleXDisplay = computed(() => layoutScaleX.value.toFixed(2));
const layoutScaleYDisplay = computed(() => layoutScaleY.value.toFixed(2));

function fitDataRectInLayout(rect, options = {}) {
    if (!rect) return;
    const screenMargin = Math.max(0, Number(options.screenMargin ?? 48));
    const viewport = stationLayoutEditorFrameRef.value;
    if (viewport) {
        const width = Math.max(1, Number(rect.maxX) - Number(rect.minX));
        const height = Math.max(1, Number(rect.maxY) - Number(rect.minY));
        const scale = Math.max(0.25, Math.min(4, Math.min(
            (viewport.clientWidth - screenMargin * 2) / width,
            (viewport.clientHeight - screenMargin * 2) / height
        )));
        layoutScaleX.value = Number(scale.toFixed(2));
        layoutScaleY.value = Number(scale.toFixed(2));
    }
    nextTick(() => stationLayoutEditorRef.value?.scrollDataRectIntoView?.(rect, {
        screenMargin,
        padding: options.padding ?? 160,
    }));
}

function fitFullLayout() {
    const rect = stationLayoutEditorRef.value?.getFullViewRect?.();
    fitDataRectInLayout(rect, { screenMargin: 48, padding: 160 });
}

function cancelLoadedLayoutFit() {
    pendingLoadedLayoutFit = false;
    loadedLayoutResizeObserver?.disconnect();
    loadedLayoutResizeObserver = null;
}

function tryFitLoadedLayout() {
    const viewport = stationLayoutEditorFrameRef.value;
    if (!pendingLoadedLayoutFit || !viewport || viewport.clientWidth <= 0 || viewport.clientHeight <= 0) return;
    cancelLoadedLayoutFit();
    fitFullLayout();
}

function scheduleLoadedLayoutFit() {
    pendingLoadedLayoutFit = true;
    tryFitLoadedLayout();
    const viewport = stationLayoutEditorFrameRef.value;
    // A hidden tab has no usable viewport yet. Fit once when it becomes visible,
    // then disconnect so later resizing does not override the user's zoom.
    if (pendingLoadedLayoutFit && viewport && typeof ResizeObserver !== "undefined") {
        loadedLayoutResizeObserver = new ResizeObserver(tryFitLoadedLayout);
        loadedLayoutResizeObserver.observe(viewport);
    }
}

const selectedAnnotation = ref(null);
const selectedEquipment = ref(null);
const equipmentDrawerVisible = ref(false);
const equipmentForm = ref({});
const equipmentFormBaseline = ref({});
const equipmentSaving = ref(false);
const activeEditMode = ref(0);
const topologyGenerationMode = ref("auto");
const isSelectMode = computed(() => activeEditMode.value === 0);
const isEquipmentBatchMode = computed(() => Boolean(selectedEquipment.value?.batch));
let f4HoldPreviousEditMode = null;
const routeTesterVisible = ref(false);
const routeSearchLoading = ref(false);
const routeNodePickTarget = ref("");
const routeSearchForm = ref({
    startNodeId: "",
    endNodeId: "",
});
const routeSearchRoutes = ref([]);
const selectedRouteIndex = ref(-1);
const cellPanelVisible = ref(false);
const cells = ref([]);
const selectedCellId = ref("");
const selectedCellLinkId = ref("");
const cellLinkHighlightScope = ref("cell");
const cellLinkPickMode = ref(false);
const cellForm = ref(createEmptyCellForm());
const topologyRepairPending = ref(false);
const layoutSnapshot = ref({
    tracks: [],
    nodes: [],
    insulationJoints: [],
});
const currentLayoutNodeById = computed(() => new Map((layoutSnapshot.value.nodes || [])
    .map((node) => [String(node.id ?? "").trim(), node])));
const equipmentBindingNodeOptions = computed(() => [...currentLayoutNodeById.value]
    .filter(([id]) => id)
    .map(([id, node]) => ({ value: id, label: node.name ? `${id} (${node.name})` : id })));
const isEquipmentNodePositionLocked = computed(() =>
    BOUND_EQUIPMENT_KINDS.includes(equipmentForm.value.kind));
watch([() => equipmentForm.value.bindingNodeID, currentLayoutNodeById], () => {
    syncEquipmentFormPositionToBindingNode(equipmentForm.value);
});
const selectedRoute = computed(() => {
    if (selectedRouteIndex.value < 0) return null;
    return routeSearchRoutes.value[selectedRouteIndex.value] || null;
});
const highlightedRouteLinkIds = computed(() => selectedRoute.value?.linkIds || []);
const highlightedRouteNodeIds = computed(() => selectedRoute.value?.nodeIds || []);
const currentLayoutLinks = computed(() => layoutSnapshot.value.tracks || []);
const currentLayoutLinkById = computed(() => {
    const linkById = new Map();
    for (const link of currentLayoutLinks.value) {
        const id = String(link?.id ?? "").trim();
        if (id) linkById.set(id, link);
    }
    return linkById;
});
const stationLayoutEditorState = computed(() => cellPanelVisible.value ? "cell_editing" : "");
const cellFormLinkIds = computed(() => parseLinkIdList(cellForm.value.linkIDList));
const layoutEditorCells = computed(() => cells.value.map((cell) => {
    if (cell.id !== selectedCellId.value) return cell;

    const selectedCellIsNew = isCellPendingBackendId(cellForm.value) || isCellPendingBackendId(cell);
    const selectedCellName = String(cellForm.value.name || "").trim();
    return {
        ...cell,
        id: String(cell.id || cellForm.value.id || "").trim(),
        isNew: selectedCellIsNew,
        name: selectedCellName || (selectedCellIsNew ? "" : String(cell.id || "").trim()),
        linkIDList: normalizeLinkIdListString(cellForm.value.linkIDList) === normalizeLinkIdListString(cell.linkIDList)
            ? cell.linkIDList : normalizeLinkIdListString(cellForm.value.linkIDList),
    };
}));
const cellLinkMembershipCounts = computed(() => {
    const counts = {};
    for (const cell of cells.value) {
        const linkIDList = cell.id === selectedCellId.value
            ? cellForm.value.linkIDList
            : cell.linkIDList;
        for (const linkId of new Set(parseLinkIdList(linkIDList))) {
            counts[linkId] = (counts[linkId] || 0) + 1;
        }
    }
    return counts;
});
const highlightedCellLinkIds = computed(() => {
    if (!cellPanelVisible.value || !selectedCellId.value) return [];
    const selectedLinkId = String(selectedCellLinkId.value || "").trim();
    return cellLinkHighlightScope.value === "link" && selectedLinkId
        ? [selectedLinkId]
        : cellFormLinkIds.value;
});
const highlightedEditorLinkIds = computed(() => {
    const ids = new Set();
    for (const id of highlightedRouteLinkIds.value) {
        const normalizedId = String(id ?? "").trim();
        if (normalizedId) ids.add(normalizedId);
    }
    for (const id of highlightedCellLinkIds.value) {
        const normalizedId = String(id ?? "").trim();
        if (normalizedId) ids.add(normalizedId);
    }
    return [...ids];
});
const bufferStopDirectionOptions = computed(() => baseBufferStopDirectionOptions.map(option => ({
    ...option,
    label: t(`stationLayout.bufferStop.directions.${option.value}`),
})));
const bufferStopTypeOptions = computed(() => baseBufferStopTypeOptions.map(option => ({
    ...option,
    label: t(`stationLayout.bufferStop.types.${option.value}`),
})));
function signalOptionLabel(option) {
    const match = String(option.value || '').match(/^(DepartureSignal|HomeSignal|ShuntingSignal|HumpSignal)(?:(\d+)Aspect)?(High|Low)?$/);
    if (!match) return option.label;
    return [
        t(`stationLayout.signal.types.${match[1]}`),
        match[2] ? t('stationLayout.signal.aspects', { count: match[2] }) : '',
        match[3] ? t(`stationLayout.signal.poles.${match[3]}`) : '',
    ].filter(Boolean).join(' ');
}
const equipmentSignalTypeOptions = computed(() => signalTypeOptions.map((option) => ({
    value: option.value,
    label: signalOptionLabel(option),
})));
const signalDirectionOptions = computed(() => ["w", "e", "s", "d"].map((value) => ({
    value,
    label: `${value} - ${t(`stationLayout.signal.directions.${value}`)}`,
})));
const insulationJointTypeOptions = computed(() => ["normal", "oversize"].map((value) => ({
    value,
    label: `${value} - ${t(`stationLayout.insulationJoint.types.${value}`)}`,
})));
const switchTypeOptions = computed(() => ["single", "slip", "diamond", "symmetric"].map((value) => ({
    value,
    label: `${value} - ${t(`stationLayout.switch.types.${value}`)}`,
})));
const selectedDrawingBufferStopDirection = ref(DEFAULT_BUFFER_STOP_DIRECTION);
const selectedDrawingBufferStopType = ref(DEFAULT_BUFFER_STOP_TYPE);
const selectedDrawingSignalType = ref(DEFAULT_SIGNAL_TYPE);
const activeDrawingObject = ref("l");
const drawingObjectCodes = new Set(["l", "n", "s", "w", "i", "r", "e", "p", "a"]);
const drawingSignalMenuGroups = computed(() => signalTypeMenuOptions.map((option) => {
    const children = Array.isArray(option.children) && option.children.length > 0
        ? option.children
        : [{ label: option.label, value: option.value }];

    return {
        label: signalOptionLabel(option),
        value: option.value,
        showLabel: children.length > 1,
        options: children.map((child) => ({
            label: signalOptionLabel(child),
            value: child.value,
        })),
    };
}));
const selectedDrawingSignalTypeLabel = computed(() => {
    const selectedValue = String(selectedDrawingSignalType.value || "");
    for (const group of drawingSignalMenuGroups.value) {
        const option = group.options.find((item) => item.value === selectedValue);
        if (option) return option.label;
    }
    return signalOptionLabel({ value: selectedValue, label: selectedValue || t("stationLayout.draw.signal") });
});
const selectedDrawingBufferStopTypeLabel = computed(() =>
    getOptionLabel(bufferStopTypeOptions.value, selectedDrawingBufferStopType.value, t("stationLayout.draw.buffer"))
);
const selectedDrawingBufferStopDirectionLabel = computed(() =>
    getOptionLabel(bufferStopDirectionOptions.value, selectedDrawingBufferStopDirection.value, "")
);
const drawingSignalButtonLabel = computed(() =>
    selectedDrawingSignalTypeLabel.value
);
const drawingBufferStopButtonLabel = computed(() =>
    `${t("stationLayout.draw.buffer")} ${selectedDrawingBufferStopTypeLabel.value}/${selectedDrawingBufferStopDirectionLabel.value}`
);

function getOptionLabel(options, value, fallback = "") {
    const selectedValue = String(value || "");
    return options.find((option) => option.value === selectedValue)?.label || selectedValue || fallback;
}

function getDrawingButtonType(drawingObj) {
    return !isSelectMode.value && activeDrawingObject.value === drawingObj ? "primary" : "default";
}

const annotationFontFamilyOptions = ["Arial", "Microsoft YaHei", "SimSun", "SimHei", "Times New Roman", "Consolas"];
const annotationFontWeightOptions = computed(() => [
    { label: t('stationLayout.editor.common.normal'), value: "normal" },
    { label: t('stationLayout.editor.common.bold'), value: "bold" },
]);
const annotationFontStyleOptions = computed(() => [
    { label: t('stationLayout.editor.common.normal'), value: "normal" },
    { label: t('stationLayout.editor.common.italic'), value: "italic" },
]);
const layoutTextStyleRows = computed(() => [
    { key: "switchName", label: t('stationLayout.editor.styles.switchName') },
    { key: "platformName", label: t('stationLayout.editor.styles.platformName') },
    { key: "signalName", label: t('stationLayout.editor.styles.signalName') },
    { key: "lineName", label: t('stationLayout.editor.styles.lineName') },
    { key: "cellName", label: t('stationLayout.editor.styles.cellName') },
]);
const defaultLayoutDisplayStyles = {
    switchName: { fontSize: 8, fontFamily: "Arial", fontWeight: "normal", fontStyle: "normal", color: "#ffffff" },
    platformName: { fontSize: 10, fontFamily: "Arial", fontWeight: "normal", fontStyle: "normal", color: "#ffffff" },
    signalName: { fontSize: 8, fontFamily: "Arial", fontWeight: "normal", fontStyle: "normal", color: "#ffffff" },
    lineName: { fontSize: 10, fontFamily: "Arial", fontWeight: "normal", fontStyle: "normal", color: "#ffffff" },
    cellName: { fontSize: 13, fontFamily: "Arial", fontWeight: "bold", fontStyle: "normal", color: "#ffd600" },
    track: { strokeWidth: 2, color: "#fefded" },
    curve: { strokeWidth: 4, color: "#ffb347" },
    platform: { strokeWidth: 1, color: "#87ceeb" },
    signal: { scale: 0.5 },
    switch: { strokeWidth: 5, color: "#00ffff" },
    node: { radius: 5, color: "#ffffff" },
};
const layoutDisplayStyles = ref(createDefaultLayoutDisplayStyles());
const linkArrowDirectionOptions = computed(() => [
    { label: t('stationLayout.editor.arrows.none'), value: "" },
    { label: t('stationLayout.editor.arrows.left'), value: "L" },
    { label: t('stationLayout.editor.arrows.right'), value: "R" },
    { label: t('stationLayout.editor.arrows.both'), value: "LR" },
]);
const linkArrowTypeOptions = computed(() => [
    { label: t('stationLayout.editor.arrows.none'), value: "" },
    { label: t('stationLayout.editor.arrows.passenger'), value: "P" },
    { label: t('stationLayout.editor.arrows.freight'), value: "F" },
    { label: t('stationLayout.editor.arrows.mixed'), value: "PF" },
    { label: t('stationLayout.editor.arrows.outbound'), value: "LO" },
    { label: t('stationLayout.editor.arrows.inbound'), value: "LI" },
    { label: t('stationLayout.editor.arrows.leftInRightOut'), value: "LIRO" },
    { label: t('stationLayout.editor.arrows.leftOutRightIn'), value: "LORI" },
    { label: t('stationLayout.editor.arrows.oversize'), value: "OF" },
]);
const equipmentKindLabels = computed(() => ({
    link: t('stationLayout.draw.line'),
    signal: t('stationLayout.draw.signal'),
    switch: t('stationLayout.draw.switch'),
    platform: t('stationLayout.draw.platform'),
    insulationJoint: t('stationLayout.draw.insulation'),
    bufferStop: t('stationLayout.draw.buffer'),
}));
const equipmentDrawerTitle = computed(() => {
    if (!selectedEquipment.value) return t('stationLayout.panels.equipment');
    const label = equipmentKindLabels.value[selectedEquipment.value.kind] || t('stationLayout.equipment.generic');
    if (isEquipmentBatchMode.value) {
        return t('stationLayout.equipment.batch', { type: label, count: selectedEquipment.value.count || 0 });
    }
    return `${label} ${selectedEquipment.value.id || ""}`;
});

function setSelectMode() {
    activeEditMode.value = 0;
    stationLayoutEditorRef.value?.setEditMode(0);
}
function setDrawMode() {
    if (!ensureWritable()) {
        setSelectMode();
        return;
    }
    activeEditMode.value = 1;
    stationLayoutEditorRef.value?.setEditMode(1);
    stationLayoutEditorRef.value?.setDrawingObject(activeDrawingObject.value);
}
function handleEditModeChange(mode) {
    if (Number(mode) === 0) {
        setSelectMode();
    } else {
        routeNodePickTarget.value = "";
        setDrawMode();
    }
}

function isStationLayoutVisible() {
    const frame = stationLayoutEditorFrameRef.value;
    return Boolean(frame?.isConnected && frame.getClientRects().length > 0);
}

function isEditableShortcutTarget(target) {
    if (!(target instanceof HTMLElement)) return false;
    return Boolean(target.isContentEditable || target.closest("input, textarea, select, [contenteditable='true'], [contenteditable='']"));
}

function isPlainF4Event(event) {
    return event.key === "F4" && !event.altKey && !event.ctrlKey && !event.metaKey && !event.shiftKey;
}

function handleStationLayoutKeydown(event) {
    if (!isPlainF4Event(event)) return;
    if (!isStationLayoutVisible() || isEditableShortcutTarget(event.target)) return;

    event.preventDefault();
    if (f4HoldPreviousEditMode != null) return;

    f4HoldPreviousEditMode = activeEditMode.value;
    if (activeEditMode.value === 1) {
        setSelectMode();
    }
}

function restoreF4HoldEditMode() {
    const previousEditMode = f4HoldPreviousEditMode;
    f4HoldPreviousEditMode = null;
    if (previousEditMode === 1) {
        setDrawMode();
    }
}

function handleStationLayoutKeyup(event) {
    if (event.key !== "F4" || f4HoldPreviousEditMode == null) return;

    event.preventDefault();
    restoreF4HoldEditMode();
}

function toggleRouteTester() {
    routeTesterVisible.value = !routeTesterVisible.value;
    if (!routeTesterVisible.value) {
        routeNodePickTarget.value = "";
        clearSelectedRoute();
    } else {
        setSelectMode();
    }
}

function setRouteNodePickTarget(target) {
    routeNodePickTarget.value = routeNodePickTarget.value === target ? "" : target;
    if (routeNodePickTarget.value) {
        setSelectMode();
    }
}

function handleRouteNodePick(payload) {
    const nodeId = String(payload?.nodeId ?? "").trim();
    const target = payload?.target || routeNodePickTarget.value;
    if (!nodeId) return;

    if (target === "start") {
        routeSearchForm.value.startNodeId = nodeId;
    } else if (target === "end") {
        routeSearchForm.value.endNodeId = nodeId;
    }

    routeNodePickTarget.value = "";
}

function clearSelectedRoute() {
    selectedRouteIndex.value = -1;
}

function selectStationRoute(row) {
    selectedRouteIndex.value = Number(row?.index ?? -1);
}

function clearRouteSearchResult() {
    routeSearchRoutes.value = [];
    clearSelectedRoute();
}

function handleCellLinksChange(updates) {
    const byID = new Map(updates.map((update) => [update.id, update.fields]));
    cells.value = cells.value.map((cell) => {
        const fields = byID.get(String(cell.id));
        if (!fields) return cell;
        const updated = { ...cell };
        delete updated.linkIDList;
        delete updated.LinkIDList;
        return { ...updated, ...fields };
    });
    const selected = cells.value.find((cell) => cell.id === selectedCellId.value);
    if (selected && byID.has(String(selected.id))) {
        cellForm.value.linkIDList = normalizeLinkIdListString(selected.linkIDList ?? selected.LinkIDList);
        if (!parseLinkIdList(cellForm.value.linkIDList).includes(selectedCellLinkId.value)) selectedCellLinkId.value = "";
    }
}

function handleTopologyRebuilt(impact) {
    if (!impact?.requiresRepair) return;

    topologyRepairPending.value = true;
    refreshLayoutSnapshot();
    clearRouteSearchResult();
}

function showTopologyRepairReminderAfterSave() {
    if (!topologyRepairPending.value) return;

    topologyRepairPending.value = false;
    ElMessage.warning({
        message: t('stationLayout.editor.topology.recheck'),
        duration: 8000,
        showClose: true,
    });
}

function createEmptyCellForm() {
    return {
        instanceID: props.selectedInstanceId || "",
        stationSchemeID: currentStationSchemeId.value || "",
        id: "",
        isNew: false,
        name: "",
        linkIDList: "",
    };
}

function readStringField(source, ...keys) {
    for (const key of keys) {
        const value = source?.[key];
        if (value != null) return String(value);
    }
    return "";
}

function parseLinkIdList(value) {
    if (Array.isArray(value)) {
        return value
            .map((id) => String(id ?? "").trim())
            .filter((id) => id !== "");
    }

    return String(value ?? "")
        .split(/[\s,，;；]+/)
        .map((id) => id.trim())
        .filter((id) => id !== "");
}

function normalizeLinkIdListString(value) {
    return [...new Set(parseLinkIdList(value))].join(",");
}

function compareNaturalId(a, b) {
    return String(a ?? "").localeCompare(String(b ?? ""), undefined, {
        numeric: true,
        sensitivity: "base",
    });
}

function isTemporaryCellId(id) {
    return String(id ?? "").trim().startsWith(TEMP_CELL_ID_PREFIX);
}

function isCellPendingBackendId(cell) {
    return Boolean(cell?.isNew || cell?.IsNew) || isTemporaryCellId(cell?.id ?? cell?.ID);
}

function generateTemporaryCellId(reservedIds = new Set()) {
    const existingIds = new Set(cells.value.map((cell) => cell.id));
    for (const id of reservedIds) {
        existingIds.add(id);
    }

    let index = 1;
    let candidate = `${TEMP_CELL_ID_PREFIX}${index}`;
    while (existingIds.has(candidate)) {
        index++;
        candidate = `${TEMP_CELL_ID_PREFIX}${index}`;
    }
    return candidate;
}

function getCellDisplayId(cell) {
    const id = String(cell?.id ?? cell?.ID ?? "").trim();
    return !id || isCellPendingBackendId(cell) ? t('stationLayout.editor.cells.idPending') : id;
}

function normalizeCell(cell, reservedIds = new Set()) {
    let id = readStringField(cell, "id", "ID").trim();
    const name = readStringField(cell, "name", "Name").trim();
    const isNew = Boolean(cell?.isNew || cell?.IsNew) || isTemporaryCellId(id) || !id;
    if (!id) {
        id = generateTemporaryCellId(reservedIds);
    }

    return {
        ...cell,
        instanceID: readStringField(cell, "instanceID", "InstanceID").trim() || props.selectedInstanceId || "",
        stationSchemeID: readStringField(cell, "stationSchemeID", "StationSchemeID").trim() || currentStationSchemeId.value || "",
        id,
        isNew,
        name: name || (isNew ? "" : id),
        linkIDList: normalizeLinkIdListString(readStringField(cell, "linkIDList", "LinkIDList")),
    };
}

function normalizeCells(cellList) {
    if (!Array.isArray(cellList)) return [];

    const seenIds = new Set();
    const normalizedCells = [];
    for (const sourceCell of cellList) {
        const cell = normalizeCell(sourceCell, seenIds);
        if (!cell.id || seenIds.has(cell.id)) continue;
        seenIds.add(cell.id);
        normalizedCells.push(cell);
    }

    return normalizedCells.sort((left, right) => compareNaturalId(left.id, right.id));
}

function resetCells() {
    cells.value = [];
    selectedCellId.value = "";
    selectedCellLinkId.value = "";
    cellLinkHighlightScope.value = "cell";
    cellLinkPickMode.value = false;
    cellForm.value = createEmptyCellForm();
}

function setCellsFromLayout(jsonObj) {
    cells.value = isStationLayoutArchive(jsonObj)
        ? JSON.parse(JSON.stringify(jsonObj.cells || []))
        : normalizeCells(jsonObj?.cells || []);
    const nextSelectedCell = cells.value.find((cell) => cell.id === selectedCellId.value) || cells.value[0] || null;
    if (nextSelectedCell) {
        selectCell(nextSelectedCell);
    } else {
        selectedCellId.value = "";
        selectedCellLinkId.value = "";
        cellLinkHighlightScope.value = "cell";
        cellForm.value = createEmptyCellForm();
    }
}

function buildCellForm(cell) {
    return {
        instanceID: cell?.instanceID || props.selectedInstanceId || "",
        stationSchemeID: cell?.stationSchemeID || currentStationSchemeId.value || "",
        id: cell?.id || "",
        isNew: isCellPendingBackendId(cell),
        name: cell?.name ?? (isCellPendingBackendId(cell) ? "" : cell?.id) ?? "",
        linkIDList: normalizeLinkIdListString(cell?.linkIDList),
    };
}

function selectCell(cell) {
    if (!cell) return;
    selectedCellId.value = cell.id;
    cellForm.value = buildCellForm(cell);
    const linkIds = parseLinkIdList(cellForm.value.linkIDList);
    selectedCellLinkId.value = linkIds[0] || "";
    cellLinkHighlightScope.value = "cell";
}

function selectCellRow(row) {
    applyCellFormToSelected();
    selectCell(row);
}

function handleCellNameClick(cellName) {
    if (!cellName?.id) return;
    if (!applyCellFormToSelected({ showWarning: true })) return;

    const cell = cells.value.find((item) => item.id === cellName.id);
    if (!cell) return;

    selectCell(cell);
    if (!cellPanelVisible.value) {
        cellPanelVisible.value = true;
        refreshLayoutSnapshot();
    }
}

function handleCellRename(payload) {
    if (!ensureWritable() || loadingData.value || savingData.value) return;
    const id = String(payload?.id ?? "").trim();
    if (!id || typeof payload?.name !== "string" || !cells.value.some((cell) => cell.id === id)) return;
    if (!applyCellFormToSelected({ showWarning: true })) return;

    const index = cells.value.findIndex((cell) => cell.id === id);
    if (index < 0) return;
    const renamedCell = normalizeCell({ ...cells.value[index], name: payload.name });
    cells.value[index] = renamedCell;
    if (selectedCellId.value === id) {
        cellForm.value = buildCellForm(renamedCell);
    }
}

function getCellLinkCount(cell) {
    return parseLinkIdList(cell?.linkIDList).length;
}

function applyCellFormToSelected(options = {}) {
    if (props.readonly) return true;
    const previousId = selectedCellId.value;
    if (!previousId) return true;

    const original = cells.value.find((cell) => cell.id === previousId);
    if (original && JSON.stringify(cellForm.value) === JSON.stringify(buildCellForm(original))) return true;

    const nextCell = normalizeCell({ ...original, ...cellForm.value });
    if (!nextCell.id) {
        if (options.showWarning) ElMessage.warning(t('stationLayout.editor.cells.idRequired'));
        return false;
    }

    const duplicated = cells.value.some((cell) => cell.id === nextCell.id && cell.id !== previousId);
    if (duplicated) {
        if (options.showWarning) ElMessage.warning(t('stationLayout.editor.cells.idExists', { id: nextCell.id }));
        return false;
    }

    const index = cells.value.findIndex((cell) => cell.id === previousId);
    if (index < 0) return false;

    cells.value[index] = nextCell;
    selectedCellId.value = nextCell.id;
    cellForm.value = buildCellForm(nextCell);
    return true;
}

function buildCellsForJson(options = {}) {
    applyCellFormToSelected();
    return cells.value.map((cell) => {
        const result = {
            ...cell,
            instanceID: props.selectedInstanceId || "",
            stationSchemeID: currentStationSchemeId.value || "",
            id: !options.forFile && !options.forSave && isCellPendingBackendId(cell) ? "" : cell.id,
        };
        if (!options.forFile && isCellPendingBackendId(cell)) {
            delete result.isNew;
            delete result.IsNew;
        }
        return result;
    });
}

function createCell() {
    if (!ensureWritable()) return;
    if (!applyCellFormToSelected({ showWarning: true })) return;
    const id = generateTemporaryCellId();
    const cell = {
        instanceID: props.selectedInstanceId || "",
        stationSchemeID: currentStationSchemeId.value || "",
        id,
        isNew: true,
        name: `Cell ${cells.value.length + 1}`,
        linkIDList: "",
    };
    cells.value = [...cells.value, cell];
    selectCell(cell);
}

async function deleteSelectedCell() {
    if (!ensureWritable()) return;
    if (!selectedCellId.value) return;
    const selectedCell = cells.value.find((cell) => cell.id === selectedCellId.value);
    const cellLabel = selectedCell?.name || getCellDisplayId(selectedCell);

    try {
        await ElMessageBox.confirm(
            t('stationLayout.editor.cells.deleteConfirm', { name: cellLabel }),
            t('stationLayout.editor.cells.deleteTitle'),
            {
                confirmButtonText: t('stationLayout.editor.common.delete'),
                cancelButtonText: t('stationLayout.editor.common.cancel'),
                type: "warning",
            }
        );
    } catch (err) {
        return;
    }

    cells.value = cells.value.filter((cell) => cell.id !== selectedCellId.value);
    const nextCell = cells.value[0] || null;
    if (nextCell) {
        selectCell(nextCell);
    } else {
        selectedCellId.value = "";
        selectedCellLinkId.value = "";
        cellLinkHighlightScope.value = "cell";
        cellForm.value = createEmptyCellForm();
    }
}

function setCellFormLinkIds(linkIds) {
    if (!ensureWritable()) return;
    const normalizedLinkIds = [...new Set(linkIds.map((id) => String(id ?? "").trim()).filter((id) => id !== ""))];
    cellForm.value.linkIDList = normalizedLinkIds.join(",");
    cellLinkHighlightScope.value = "cell";
    if (selectedCellLinkId.value && !normalizedLinkIds.includes(selectedCellLinkId.value)) {
        selectedCellLinkId.value = normalizedLinkIds[0] || "";
    }
    if (!selectedCellLinkId.value) {
        selectedCellLinkId.value = normalizedLinkIds[0] || "";
    }
    applyCellFormToSelected();
}

function addLinkToCell(linkId) {
    if (!ensureWritable()) return;
    if (!selectedCellId.value) {
        ElMessage.warning(t('stationLayout.editor.cells.selectFirst'));
        return;
    }

    const normalizedLinkId = String(linkId ?? "").trim();
    if (!normalizedLinkId) return;

    const linkIds = parseLinkIdList(cellForm.value.linkIDList);
    if (linkIds.includes(normalizedLinkId)) {
        selectedCellLinkId.value = normalizedLinkId;
        cellLinkHighlightScope.value = "link";
        ElMessage.info(t('stationLayout.editor.cells.linkExists', { id: normalizedLinkId }));
        return;
    }

    setCellFormLinkIds([...linkIds, normalizedLinkId]);
    selectedCellLinkId.value = normalizedLinkId;
    cellLinkHighlightScope.value = "link";
    ElMessage.success(t('stationLayout.editor.cells.linkAdded', { id: normalizedLinkId }));
}

function removeSelectedCellLink() {
    if (!ensureWritable()) return;
    const removingLinkId = String(selectedCellLinkId.value || "").trim();
    if (!removingLinkId) return;

    setCellFormLinkIds(parseLinkIdList(cellForm.value.linkIDList).filter((id) => id !== removingLinkId));
    cellLinkHighlightScope.value = "cell";
}

function clearCellLinks() {
    if (!ensureWritable()) return;
    setCellFormLinkIds([]);
    cellLinkHighlightScope.value = "cell";
}

function handleCellLinkTabClick() {
    refreshLayoutSnapshot();
    cellLinkHighlightScope.value = "link";
}

function toggleCellLinkPickMode() {
    if (!ensureWritable()) return;
    if (!selectedCellId.value) {
        ElMessage.warning(t('stationLayout.editor.cells.selectFirst'));
        return;
    }

    cellLinkPickMode.value = !cellLinkPickMode.value;
    if (cellLinkPickMode.value) {
        setSelectMode();
    }
}

function toggleCellPanel() {
    cellPanelVisible.value = !cellPanelVisible.value;
    if (cellPanelVisible.value) {
        refreshLayoutSnapshot();
        if (!selectedCellId.value && cells.value.length > 0) {
            selectCell(cells.value[0]);
        }
    } else {
        cellLinkPickMode.value = false;
        selectedCellLinkId.value = "";
        cellLinkHighlightScope.value = "cell";
    }
}

function snapshotText(value) {
    return typeof value === "string" || typeof value === "number" ? String(value).trim() : "";
}

function normalizeSnapshotTrack(track) {
    return {
        ...track,
        id: snapshotText(track?.id ?? track?.ID),
        name: snapshotText(track?.name ?? track?.Name),
        fromNodeID: snapshotText(track?.fromNodeID ?? track?.FromNodeID),
        toNodeID: snapshotText(track?.toNodeID ?? track?.ToNodeID),
    };
}

function normalizeSnapshotInsulationJoint(insulationJoint) {
    return {
        ...insulationJoint,
        id: snapshotText(insulationJoint?.id ?? insulationJoint?.ID),
        bindingNodeID: snapshotText(insulationJoint?.bindingNodeID ?? insulationJoint?.BindingNodeID),
    };
}

function setLayoutSnapshotFromJson(jsonObj) {
    layoutSnapshot.value = {
        tracks: Array.isArray(jsonObj?.tracks) ? jsonObj.tracks.map(normalizeSnapshotTrack) : [],
        nodes: Array.isArray(jsonObj?.nodes) ? jsonObj.nodes.map((node) => ({ ...node })) : [],
        insulationJoints: Array.isArray(jsonObj?.insulationJoints)
            ? jsonObj.insulationJoints.map(normalizeSnapshotInsulationJoint)
            : [],
    };
}

function refreshLayoutSnapshot() {
    const dataStr = stationLayoutEditorRef.value?.buildJsonData?.();
    if (!dataStr) return;

    try {
        setLayoutSnapshotFromJson(JSON.parse(dataStr));
    } catch (err) {
        console.error("Failed to refresh station layout snapshot:", err);
    }
}

function pruneCellLinksToExistingTracks() {
    const existingLinkIds = new Set(currentLayoutLinks.value.map((link) => link.id).filter(Boolean));
    if (existingLinkIds.size === 0) return;

    cells.value = cells.value.map((cell) => ({
        ...cell,
        linkIDList: parseLinkIdList(cell.linkIDList)
            .filter((id) => existingLinkIds.has(id))
            .join(","),
    }));
    if (selectedCellId.value) {
        const selectedCell = cells.value.find((cell) => cell.id === selectedCellId.value);
        if (selectedCell) selectCell(selectedCell);
    }
}

function updateCellLinkReferences(previousId, nextId) {
    const oldId = String(previousId ?? "").trim();
    const newId = String(nextId ?? "").trim();
    if (!oldId || !newId || oldId === newId) return;

    cells.value = cells.value.map((cell) => ({
        ...cell,
        linkIDList: parseLinkIdList(cell.linkIDList)
            .map((id) => id === oldId ? newId : id)
            .join(","),
    }));
    if (selectedCellLinkId.value === oldId) {
        selectedCellLinkId.value = newId;
    }
    if (selectedCellId.value) {
        const selectedCell = cells.value.find((cell) => cell.id === selectedCellId.value);
        if (selectedCell) cellForm.value = buildCellForm(selectedCell);
    }
}

function getLinkLabel(linkId) {
    const normalizedLinkId = String(linkId ?? "").trim();
    const link = currentLayoutLinkById.value.get(normalizedLinkId);
    if (link?.name) return `${link.name} (${normalizedLinkId})`;
    return `Link ${normalizedLinkId}`;
}

function getLinkEndpointSummary(linkId) {
    const link = currentLayoutLinkById.value.get(String(linkId ?? "").trim());
    if (!link) return t('stationLayout.editor.cells.linkNotFound');
    const fromNode = link.fromNodeID || "-";
    const toNode = link.toNodeID || "-";
    return `${fromNode} -> ${toNode}`;
}

function buildCellComponentsFromCurrentLayout() {
    refreshLayoutSnapshot();
    const tracks = currentLayoutLinks.value.filter((link) => link.id);
    const insulatedNodeIds = new Set(
        (layoutSnapshot.value.insulationJoints || [])
            .map((ij) => String(ij.bindingNodeID || "").trim())
            .filter((id) => id !== "")
    );
    const linkIdsByNodeId = new Map();
    const adjacency = new Map(tracks.map((link) => [link.id, new Set()]));

    for (const link of tracks) {
        for (const nodeId of [link.fromNodeID, link.toNodeID]) {
            const normalizedNodeId = String(nodeId || "").trim();
            if (!normalizedNodeId) continue;
            if (!linkIdsByNodeId.has(normalizedNodeId)) {
                linkIdsByNodeId.set(normalizedNodeId, []);
            }
            linkIdsByNodeId.get(normalizedNodeId).push(link.id);
        }
    }

    for (const [nodeId, linkIds] of linkIdsByNodeId.entries()) {
        if (insulatedNodeIds.has(nodeId)) continue;
        for (const linkId of linkIds) {
            const adjacentLinks = adjacency.get(linkId);
            for (const adjacentLinkId of linkIds) {
                if (adjacentLinkId !== linkId) adjacentLinks.add(adjacentLinkId);
            }
        }
    }

    const visited = new Set();
    const components = [];
    for (const link of [...tracks].sort((left, right) => compareNaturalId(left.id, right.id))) {
        if (visited.has(link.id)) continue;

        const queue = [link.id];
        const component = [];
        visited.add(link.id);
        while (queue.length > 0) {
            const currentLinkId = queue.shift();
            component.push(currentLinkId);
            for (const adjacentLinkId of adjacency.get(currentLinkId) || []) {
                if (visited.has(adjacentLinkId)) continue;
                visited.add(adjacentLinkId);
                queue.push(adjacentLinkId);
            }
        }
        components.push(component.sort(compareNaturalId));
    }

    return components;
}

async function autoGenerateCells() {
    if (!ensureWritable()) return;
    const components = buildCellComponentsFromCurrentLayout();
    if (components.length === 0) {
        ElMessage.warning(t('stationLayout.editor.cells.noLinks'));
        return;
    }

    if (cells.value.length > 0) {
        try {
            await ElMessageBox.confirm(
                t('stationLayout.editor.cells.overwriteConfirm'),
                t('stationLayout.editor.cells.generateTitle'),
                {
                    confirmButtonText: t('stationLayout.editor.common.generate'),
                    cancelButtonText: t('stationLayout.editor.common.cancel'),
                    type: "warning",
                }
            );
        } catch (err) {
            return;
        }
    }

    const generatedIds = new Set();
    cells.value = components.map((linkIds, index) => {
        const id = generateTemporaryCellId(generatedIds);
        generatedIds.add(id);
        return {
            instanceID: props.selectedInstanceId || "",
            stationSchemeID: currentStationSchemeId.value || "",
            id,
            isNew: true,
            name: `Cell ${index + 1}`,
            linkIDList: linkIds.join(","),
        };
    });
    selectCell(cells.value[0]);
    cellPanelVisible.value = true;
    ElMessage.success(t('stationLayout.editor.cells.generated', { count: cells.value.length }));
}

async function saveCellForm() {
    if (!ensureWritable()) return;
    if (!applyCellFormToSelected({ showWarning: true })) return;

    await saveData({
        silent: true,
        successMessage: t('stationLayout.editor.cells.saved'),
        failurePrefix: t('stationLayout.editor.cells.saveFailed'),
    });
}

function normalizeRouteIdList(route, keys) {
    for (const key of keys) {
        const value = route?.[key];
        if (Array.isArray(value)) {
            return value.map((id) => String(id)).filter((id) => id !== "");
        }
    }
    return [];
}

function normalizeSearchRoute(route, index) {
    const nodeIds = normalizeRouteIdList(route, ["nodeIds", "nodeIDs", "NodeIds", "NodeIDs"]);
    const linkIds = normalizeRouteIdList(route, ["linkIds", "linkIDs", "LinkIds", "LinkIDs"]);
    const cellIds = normalizeRouteIdList(route, ["cellIds", "cellIDs", "CellIds", "CellIDs"]);
    return {
        ...route,
        index,
        direction: String(route?.direction ?? route?.Direction ?? ""),
        nodeIds,
        linkIds,
        cellIds,
    };
}

function getRouteDirectionLabel(direction) {
    if (direction === "LeftToRight") return t('stationLayout.editor.routes.leftToRight');
    if (direction === "RightToLeft") return t('stationLayout.editor.routes.rightToLeft');
    return direction || "-";
}

function getRouteSummary(route) {
    if (!route) return "";
    return route.nodeIds.length > 0
        ? route.nodeIds.join(" -> ")
        : `${route.linkIds.length} links`;
}

async function searchStationRoutes() {
    if (!props.selectedInstanceId) {
        ElMessage.warning(t('stationLayout.placeholders.selectInstance'));
        return;
    }

    const startNodeId = String(routeSearchForm.value.startNodeId || "").trim();
    const endNodeId = String(routeSearchForm.value.endNodeId || "").trim();
    if (!startNodeId || !endNodeId) {
        ElMessage.warning(t('stationLayout.editor.routes.endpointsRequired'));
        return;
    }
    routeSearchLoading.value = true;
    try {
        const response = await props.gateway.searchRoutes({
            instanceId: props.selectedInstanceId,
            stationSchemeId: currentStationSchemeId.value,
            startNodeId,
            endNodeId,
        });
        const routes = Array.isArray(response?.routes)
            ? response.routes
            : Array.isArray(response?.Routes)
                ? response.Routes
                : [];
        routeSearchRoutes.value = routes.map((route, index) => normalizeSearchRoute(route, index));
        selectedRouteIndex.value = routeSearchRoutes.value.length > 0 ? 0 : -1;
        ElMessage.success(t('stationLayout.editor.routes.found', { count: routeSearchRoutes.value.length }));
    } catch (err) {
        routeSearchRoutes.value = [];
        selectedRouteIndex.value = -1;
        ElMessage.error(getHttpErrorMessage(err, t('stationLayout.editor.routes.searchFailed')));
    } finally {
        routeSearchLoading.value = false;
    }
}
function createDefaultLayoutDisplayStyles() {
    return JSON.parse(JSON.stringify(defaultLayoutDisplayStyles));
}
function normalizeLayoutDisplayStyles(styles) {
    const source = styles && typeof styles === "object" && !Array.isArray(styles) ? styles : {};
    const normalized = { ...createDefaultLayoutDisplayStyles(), ...source };
    for (const row of layoutTextStyleRows.value) {
        if (source[row.key] && typeof source[row.key] === "object" && !Array.isArray(source[row.key])) {
            normalized[row.key] = { ...defaultLayoutDisplayStyles[row.key], ...source[row.key] };
        } else {
            normalized[row.key] = { ...defaultLayoutDisplayStyles[row.key] };
        }
    }

    for (const key of ["track", "curve", "platform", "signal", "switch", "node"]) {
        if (source[key] && typeof source[key] === "object" && !Array.isArray(source[key])) {
            normalized[key] = { ...defaultLayoutDisplayStyles[key], ...source[key] };
        } else {
            normalized[key] = { ...defaultLayoutDisplayStyles[key] };
        }
    }

    return normalized;
}
function applyLayoutDisplayStyles(styles) {
    layoutDisplayStyles.value = normalizeLayoutDisplayStyles(styles);
}
function normalizeGridSpacingValue(value) {
    const spacing = Number(value);
    if (!Number.isFinite(spacing) || spacing <= 0) return DEFAULT_GRID_SPACING;
    return Math.min(MAX_GRID_SPACING, Math.max(1, spacing));
}
function normalizeGridOriginValue(value) {
    const origin = Number(value);
    return Number.isFinite(origin) ? origin : 0;
}
function normalizeLayoutGridSettings(settings) {
    const source = settings && typeof settings === "object" && !Array.isArray(settings) ? settings : {};
    const rawShowGrid = source.showGrid ?? source.ShowGrid;
    return {
        showGrid: rawShowGrid == null ? true : rawShowGrid !== false,
        spacing: normalizeGridSpacingValue(source.spacing ?? source.Spacing ?? source.gridSpacing ?? source.GridSpacing),
        originX: normalizeGridOriginValue(source.originX ?? source.OriginX),
        originY: normalizeGridOriginValue(source.originY ?? source.OriginY),
    };
}
function buildCurrentLayoutGridSettings(settings) {
    const normalized = normalizeLayoutGridSettings(settings);
    return {
        ...normalized,
        ...settings,
        showGrid: normalized.showGrid === showGrid.value && settings?.showGrid !== undefined
            ? settings.showGrid : showGrid.value !== false,
        spacing: normalized.spacing === gridSpacing.value && settings?.spacing !== undefined
            ? settings.spacing : normalizeGridSpacingValue(gridSpacing.value),
    };
}
function applyLayoutGridSettings(settings) {
    const normalized = normalizeLayoutGridSettings(settings);
    showGrid.value = normalized.showGrid;
    gridSpacing.value = normalized.spacing;
    return normalized;
}
function resetLayoutGridSettings() {
    applyLayoutGridSettings();
}
function buildLayoutJsonWithDisplayStyles(dataStr, options = {}) {
    const jsonObj = JSON.parse(dataStr);
    jsonObj.cells = buildCellsForJson(options);
    const preserveDocument = isStationLayoutArchive(jsonObj);
    const originalStyles = jsonObj.metadata?.displayStyles;
    const displayStyles = (preserveDocument || originalStyles !== undefined)
        && JSON.stringify(normalizeLayoutDisplayStyles(originalStyles)) === JSON.stringify(layoutDisplayStyles.value)
        ? originalStyles : normalizeLayoutDisplayStyles(layoutDisplayStyles.value);
    jsonObj.metadata = {
        ...(jsonObj.metadata || {}),
        instanceID: props.selectedInstanceId || "",
        stationSchemeID: currentStationSchemeId.value || "",
        revision: getStationSchemeRevision(),
        displayStyles,
        // The editor owns grid precision and detects actual grid-property edits.
        gridSettings: preserveDocument ? jsonObj.metadata?.gridSettings
            : buildCurrentLayoutGridSettings(jsonObj.metadata?.gridSettings),
    };
    if (jsonObj.metadata.revision === undefined) delete jsonObj.metadata.revision;
    if (jsonObj.metadata.displayStyles === undefined) delete jsonObj.metadata.displayStyles;
    if (jsonObj.metadata.gridSettings === undefined) delete jsonObj.metadata.gridSettings;
    return serializeStationLayoutJson(jsonObj, {
        allowUnresolvedReferences: options.forSave === true && Boolean(props.gateway.repairJson),
    });
}

async function prepareLayoutInput(text, instanceId, stationSchemeId) {
    const document = JSON.parse(text.replace(/^\uFEFF/, ""));
    validateStationLayoutJson(document, { allowUnresolvedReferences: Boolean(props.gateway.repairJson) });
    if (!props.gateway.repairJson) return { document, repairCount: 0 };
    const result = await props.gateway.repairJson({ json: JSON.stringify(document), instanceId, stationSchemeId });
    return { document: parseStationLayoutJson(result.json), repairCount: result.repairCount || 0 };
}

function notifyLayoutRepair(repairCount, saved = false) {
    if (repairCount > 0) ElMessage.success(t(saved ? 'stationLayout.editor.topology.repairSaved' : 'stationLayout.editor.topology.repaired', { count: repairCount }));
}
function resetLayoutDisplayStyles() {
    layoutDisplayStyles.value = createDefaultLayoutDisplayStyles();
}
async function saveLayoutDisplayStyles() {
    if (!ensureWritable()) return;
    const saved = await saveData({
        silent: true,
        successMessage: t('stationLayout.editor.styles.saved'),
        failurePrefix: t('stationLayout.editor.styles.saveFailed'),
    });
    if (saved) {
        layoutStyleDialogVisible.value = false;
    }
}
function clearSelection() {
    stationLayoutEditorRef.value?.clearSelectedLines();
    stationLayoutEditorRef.value?.clearSelectedNodes();
    stationLayoutEditorRef.value?.clearSelectedEquipment();
}
const boundNodeEquipmentDeleteLabels = computed(() => ({
    signal: t('stationLayout.draw.signal'),
    insulationJoint: t('stationLayout.draw.insulation'),
    switch: t('stationLayout.draw.switch'),
    bufferStop: t('stationLayout.draw.buffer'),
}));

function formatBoundNodeEquipmentDeleteMessage(plan) {
    const countText = Object.entries(plan?.counts || {})
        .filter(([, count]) => Number(count) > 0)
        .map(([kind, count]) => t('stationLayout.editor.deleteNodes.count', { type: boundNodeEquipmentDeleteLabels.value[kind] || kind, count }))
        .join("、");
    const detailText = (plan?.boundEquipment || [])
        .slice(0, 8)
        .map((equipment) => {
            const label = boundNodeEquipmentDeleteLabels.value[equipment.kind] || equipment.kind;
            const name = equipment.name || equipment.id || "";
            return `${label}${name ? ` ${name}` : ""}`;
        })
        .join("、");
    const moreCount = Math.max(0, (plan?.boundEquipment?.length || 0) - 8);
    const detailSuffix = detailText
        ? t('stationLayout.editor.deleteNodes.details', { details: detailText, more: moreCount > 0 ? t('stationLayout.editor.deleteNodes.more', { count: plan.boundEquipment.length }) : '' })
        : "";

    return t('stationLayout.editor.deleteNodes.confirm', { count: plan?.nodeIds?.length || 0, equipment: countText || t('stationLayout.equipment.generic'), details: detailSuffix });
}

async function confirmDeleteNodesWithBoundEquipment(plan) {
    if (!plan?.requiresConfirmation) return true;

    try {
        await ElMessageBox.confirm(
            formatBoundNodeEquipmentDeleteMessage(plan),
            t('stationLayout.editor.deleteNodes.title'),
            {
                confirmButtonText: t('stationLayout.editor.common.delete'),
                cancelButtonText: t('stationLayout.editor.common.cancel'),
                type: "warning",
            }
        );
        return true;
    } catch (err) {
        return false;
    }
}

async function deleteSelection() {
    if (!ensureWritable()) return;
    const editor = stationLayoutEditorRef.value;
    if (!editor) return;

    const nodeDeletePlan = editor.getSelectedNodeDeletePlan?.();
    const canDeleteNodes = await confirmDeleteNodesWithBoundEquipment(nodeDeletePlan);
    if (!canDeleteNodes) return;

    editor.deleteLine();
    editor.deleteNode({ deleteBoundEquipment: true });
    editor.deleteEquipment();
    refreshLayoutSnapshot();
    pruneCellLinksToExistingTracks();
}
function revoke() {
    if (!ensureWritable()) return;
    stationLayoutEditorRef.value?.revoke();
}
function redo() {
    if (!ensureWritable()) return;
    stationLayoutEditorRef.value?.redo();
}
const mouseSnap = ref(true);
const objectSnap = ref(true);
const objectSnapDistance = ref(10);
function mouseGridSnapChange(e) {
    if (mouseSnap.value === false) {
        stationLayoutEditorRef.value?.setMouseGridSnapModeCode(0);
    } else {
        stationLayoutEditorRef.value?.setMouseGridSnapModeCode(1);
    }
}

function mouseObjectSnapChange() {
    stationLayoutEditorRef.value?.setMouseObjectSnapModeCode(objectSnap.value ? 1 : 0);
}

function normalizeStationSchemeOption(item) {
    const id = String(item?.id ?? item?.ID ?? "").trim();
    if (!id) return null;

    const name = String(item?.name ?? item?.Name ?? id).trim() || id;
    const rawRevision = item?.revision ?? item?.Revision;
    const revision = Number(rawRevision);
    return {
        id,
        name,
        ...(Number.isSafeInteger(revision) && revision >= 0 ? { revision } : {}),
    };
}

function getStationSchemeRevision(stationSchemeId = currentStationSchemeId.value) {
    const option = stationSchemeOptions.value.find((item) => item.id === stationSchemeId);
    const revision = Number(option?.revision);
    return Number.isSafeInteger(revision) && revision >= 0 ? revision : undefined;
}

function updateStationSchemeRevision(stationSchemeId, revisionValue) {
    const revision = Number(revisionValue);
    if (!stationSchemeId || !Number.isSafeInteger(revision) || revision < 0) return;
    stationSchemeOptions.value = stationSchemeOptions.value.map((option) => {
        if (option.id !== stationSchemeId) return option;
        const currentRevision = Number(option.revision);
        const nextRevision = Number.isSafeInteger(currentRevision) && currentRevision >= 0
            ? Math.max(currentRevision, revision)
            : revision;
        return { ...option, revision: nextRevision };
    });
}

function setStationSchemeOptions(options, includeCurrent = true) {
    const previousOptionsById = new Map(
        stationSchemeOptions.value.map((option) => [option.id, option])
    );
    const optionsById = new Map();
    for (const option of options) {
        if (!option?.id || optionsById.has(option.id)) continue;
        const previousOption = previousOptionsById.get(option.id);
        const revisions = [option.revision, previousOption?.revision]
            .map(Number)
            .filter((revision) => Number.isSafeInteger(revision) && revision >= 0);
        const revision = revisions.length > 0 ? Math.max(...revisions) : undefined;
        optionsById.set(option.id, {
            ...option,
            ...(revision !== undefined ? { revision } : {}),
        });
    }

    stationSchemeOptions.value = Array.from(optionsById.values());
    if (includeCurrent) {
        ensureCurrentStationSchemeOption();
    }
}

function ensureCurrentStationSchemeOption(name, revisionValue) {
    const id = currentStationSchemeId.value?.trim();
    if (!id) return;
    if (stationSchemeOptions.value.some((option) => option.id === id)) {
        updateStationSchemeRevision(id, revisionValue);
        return;
    }

    const revision = Number(revisionValue);

    stationSchemeOptions.value = [
        ...stationSchemeOptions.value,
        {
            id,
            name: name || id,
            ...(Number.isSafeInteger(revision) && revision >= 0 ? { revision } : {}),
        },
    ];
}

function formatStationSchemeLabel(option) {
    if (!option?.id) return "";
    return option.name || option.id;
}

function loadStationSchemes(options = {}) {
    const includeCurrent = options?.includeCurrent !== false;
    const instanceId = props.selectedInstanceId;
    if (!instanceId) {
        stationSchemeOptions.value = [];
        loadingStationSchemes.value = false;
        return Promise.resolve([]);
    }

    loadingStationSchemes.value = true;
    return props.gateway
        .getStationSchemes({ instanceId })
        .then((result) => {
            if (props.selectedInstanceId !== instanceId) {
                return [];
            }

            const options = (result || [])
                .map(normalizeStationSchemeOption)
                .filter(Boolean);
            setStationSchemeOptions(options, includeCurrent);
            return options;
        })
        .catch((err) => {
            if (props.selectedInstanceId !== instanceId) {
                return [];
            }

            console.error("Failed to load station schemes:", err);
            ElMessage.error(getHttpErrorMessage(err, t('stationLayout.messages.loadSchemesFailed')));
            return stationSchemeOptions.value;
        })
        .finally(() => {
            if (props.selectedInstanceId === instanceId) {
                loadingStationSchemes.value = false;
            }
        });
}

function resetStationSchemeDraft() {
    stationSchemeDraft.value = null;
}

function cancelStationSchemeEdit() {
    editingStationSchemeOriginalId.value = "";
    editingStationSchemeForm.value = { name: "" };
}

async function openStationSchemeManager() {
    if (!props.selectedInstanceId) {
        ElMessage.warning(t('stationLayout.placeholders.selectInstance'));
        return;
    }

    stationSchemeManagerVisible.value = true;
    resetStationSchemeDraft();
    cancelStationSchemeEdit();
    await loadStationSchemes();
}

async function startNewStationScheme() {
    if (!ensureWritable() || stationSchemeManagerSaving.value || stationSchemeDraft.value) return;
    cancelStationSchemeEdit();
    stationSchemeDraft.value = { id: "", name: "", isDraft: true };
    await nextTick();
    stationSchemeDraftInputRef.value?.focus();
}

async function activateSavedStationScheme(result, instanceId) {
    const option = normalizeStationSchemeOption(result);
    if (!option?.id) throw new Error(t('stationLayout.schemeManager.invalidResponse'));
    if (props.selectedInstanceId !== instanceId) return;
    currentStationSchemeId.value = option.id;
    ensureCurrentStationSchemeOption(option.name, option.revision);
    await loadStationSchemes();
    if (props.selectedInstanceId === instanceId) {
        getData({ stationSchemeId: option.id });
    }
}

async function createStationScheme() {
    if (!ensureWritable() || stationSchemeManagerSaving.value || !stationSchemeDraft.value) return;
    const name = stationSchemeDraft.value.name.trim();
    if (!name) {
        ElMessage.warning(t('stationLayout.schemeManager.nameRequired'));
        return;
    }

    const instanceId = props.selectedInstanceId;
    stationSchemeManagerSaving.value = true;
    try {
        const result = await props.gateway.createStationScheme({
            instanceId,
            name,
        });
        await activateSavedStationScheme(result, instanceId);
        if (props.selectedInstanceId !== instanceId) return;
        resetStationSchemeDraft();
        ElMessage.success(t('stationLayout.schemeManager.createSuccess'));
    } catch (err) {
        ElMessage.error(getHttpErrorMessage(err, t('stationLayout.schemeManager.createFailed')));
    } finally {
        stationSchemeManagerSaving.value = false;
    }
}

async function copyStationScheme(row) {
    if (!ensureWritable() || stationSchemeManagerSaving.value || !row?.id) return;
    const instanceId = props.selectedInstanceId;
    stationSchemeManagerSaving.value = true;
    try {
        const result = await props.gateway.copyStationScheme({
            instanceId,
            sourceStationSchemeId: row.id,
            name: t('stationLayout.schemeManager.copyName', { name: formatStationSchemeLabel(row) }).slice(0, 100),
        });
        await activateSavedStationScheme(result, instanceId);
        if (props.selectedInstanceId === instanceId) {
            ElMessage.success(t('stationLayout.schemeManager.copySuccess'));
        }
    } catch (err) {
        ElMessage.error(getHttpErrorMessage(err, t('stationLayout.schemeManager.copyFailed')));
    } finally {
        stationSchemeManagerSaving.value = false;
    }
}

function startEditStationScheme(row) {
    if (!ensureWritable()) return;
    editingStationSchemeOriginalId.value = row.id;
    editingStationSchemeForm.value = {
        name: row.name || row.id,
    };
}

async function saveStationSchemeEdit() {
    if (!ensureWritable()) return;
    const originalID = editingStationSchemeOriginalId.value;
    const name = editingStationSchemeForm.value.name.trim();
    if (!originalID) {
        ElMessage.warning(t('stationLayout.schemeManager.idRequired'));
        return;
    }
    if (!name) {
        ElMessage.warning(t('stationLayout.schemeManager.nameRequired'));
        return;
    }

    const wasCurrent = currentStationSchemeId.value === originalID;
    stationSchemeManagerSaving.value = true;
    try {
        await props.gateway.editStationScheme({
            instanceId: props.selectedInstanceId,
            originalId: originalID,
            name,
        });
        cancelStationSchemeEdit();
        await loadStationSchemes();
        if (wasCurrent) {
            getData({ stationSchemeId: originalID });
        }
        ElMessage.success(t('stationLayout.schemeManager.updateSuccess'));
    } catch (err) {
        ElMessage.error(getHttpErrorMessage(err, t('stationLayout.schemeManager.updateFailed')));
    } finally {
        stationSchemeManagerSaving.value = false;
    }
}

async function deleteStationScheme(row) {
    if (!ensureWritable()) return;
    const instanceId = props.selectedInstanceId;
    try {
        await ElMessageBox.confirm(
            t('stationLayout.schemeManager.deleteConfirm', { name: formatStationSchemeLabel(row) }),
            t('stationLayout.schemeManager.deleteTitle'),
            {
                confirmButtonText: t('stationLayout.schemeManager.confirm'),
                cancelButtonText: t('stationLayout.schemeManager.cancel'),
                type: 'warning',
            }
        );
    } catch (err) {
        return;
    }

    if (props.selectedInstanceId !== instanceId) return;
    stationSchemeManagerSaving.value = true;
    try {
        await props.gateway.deleteStationScheme({
            instanceId,
            stationSchemeId: row.id,
        });
        if (props.selectedInstanceId !== instanceId) return;

        const deletedCurrent = currentStationSchemeId.value === row.id;
        if (deletedCurrent) {
            currentStationSchemeId.value = "";
        }
        cancelStationSchemeEdit();
        await loadStationSchemes({ includeCurrent: !deletedCurrent });

        if (props.selectedInstanceId !== instanceId) return;
        if (deletedCurrent && !currentStationSchemeId.value) {
            const nextStationSchemeId = stationSchemeOptions.value[0]?.id || "";
            currentStationSchemeId.value = nextStationSchemeId;
            if (nextStationSchemeId) {
                getData({ stationSchemeId: nextStationSchemeId });
            } else {
                stationLayoutEditorRef.value?.clearElements();
            }
        }

        ElMessage.success(t('stationLayout.schemeManager.deleteSuccess'));
    } catch (err) {
        ElMessage.error(getHttpErrorMessage(err, t('stationLayout.schemeManager.deleteFailed')));
    } finally {
        stationSchemeManagerSaving.value = false;
    }
}

function saveData(options = {}) {
    const silent = options?.silent === true;
    if (loadingData.value || savingData.value || importingData.value) return Promise.resolve(false);
    if (!ensureWritable({ silent })) {
        return Promise.resolve(false);
    }
    if (!props.selectedInstanceId) {
        ElMessage.warning(t('stationLayout.placeholders.selectInstance'));
        return Promise.resolve(false);
    }

    var dataStr = stationLayoutEditorRef.value?.buildJsonData();
    if (!dataStr) {
        ElMessage.warning(t('stationLayout.editor.equipment.noLayout'));
        return Promise.resolve(false);
    }

    if (selectedCellId.value && !applyCellFormToSelected({ showWarning: true })) {
        return Promise.resolve(false);
    }

    try {
        dataStr = buildLayoutJsonWithDisplayStyles(dataStr, { forSave: true });
    } catch (err) {
        console.error("Failed to validate station layout before saving:", err);
        ElMessage.error(`${t('stationLayout.editor.styles.invalidLayout')} ${getHttpErrorMessage(err, '')}`);
        return Promise.resolve(false);
    }

    const silentSuccessMessage = options?.successMessage || t('stationLayout.editor.equipment.saved');
    const silentFailurePrefix = options?.failurePrefix || t('stationLayout.editor.equipment.saveFailed');
    const shouldReloadGeneratedCellIds = cells.value.some(isCellPendingBackendId);
    const saveLoadVersion = layoutLoadVersion;
    savingData.value = true;
    const saveRequest = {
        json: dataStr,
        instanceId: props.selectedInstanceId,
        stationSchemeId: currentStationSchemeId.value,
    };
    const expectedRevision = getStationSchemeRevision();
    if (expectedRevision !== undefined) {
        saveRequest.expectedRevision = expectedRevision;
    }

    return props.gateway
        .saveJson(saveRequest)
        .then((result) => {
            const sameScope = props.selectedInstanceId === saveRequest.instanceId
                && currentStationSchemeId.value === saveRequest.stationSchemeId && layoutLoadVersion === saveLoadVersion;
            const repairCount = result?.repairCount || 0;
            if (!sameScope) return true;
            let unchanged = false;
            try {
                unchanged = (!selectedCellId.value || applyCellFormToSelected())
                    && buildLayoutJsonWithDisplayStyles(stationLayoutEditorRef.value.buildJsonData(), { forSave: true }) === dataStr;
            } catch { /* Preserve any new, incomplete edits. */ }
            const mappings = result?.idMappings || {};
            const mapped = (kind, value) => Object.hasOwn(mappings[kind] || {}, String(value)) ? mappings[kind][String(value)] : value;
            // IDs are acknowledged even when the user kept editing during the request.
            cells.value = remapStationLayoutIds({ cells: cells.value }, mappings).cells;
            cellForm.value = remapStationLayoutIds({ cells: [cellForm.value] }, mappings).cells[0];
            selectedCellId.value = mapped("cells", selectedCellId.value);
            selectedCellLinkId.value = mapped("tracks", selectedCellLinkId.value);
            routeSearchForm.value.startNodeId = mapped("nodes", routeSearchForm.value.startNodeId);
            routeSearchForm.value.endNodeId = mapped("nodes", routeSearchForm.value.endNodeId);
            stationLayoutEditorRef.value.applyPersistedIds?.(mappings);
            const savedJson = result?.savedJson || result?.repairedJson;
            let appliedSaved = false;
            if (savedJson && unchanged) {
                const saved = parseStationLayoutJson(savedJson);
                if (repairCount > 0 || !stationLayoutEditorRef.value.applySavedLayout) {
                    stationLayoutEditorRef.value.loadDataFromJson(saved, { preserveDocument: isStationLayoutArchive(saved), resetHistory: true });
                } else {
                    stationLayoutEditorRef.value.applySavedLayout(saved);
                }
                setLayoutSnapshotFromJson(saved);
                setCellsFromLayout(saved);
                appliedSaved = true;
            } else {
                refreshLayoutSnapshot();
                if (repairCount > 0) ElMessage.warning(t('stationLayout.editor.topology.repairNewEdits'));
            }
            clearRouteSearchResult();
            const savedStationSchemeId = result?.stationSchemeId || result?.stationSchemeID || currentStationSchemeId.value;
            currentStationSchemeId.value = savedStationSchemeId;
            ensureCurrentStationSchemeOption(undefined, result?.revision ?? result?.Revision);
            updateStationSchemeRevision(savedStationSchemeId, result?.revision ?? result?.Revision);
            void loadStationSchemes();
            if (repairCount > 0) {
                notifyLayoutRepair(repairCount, true);
            } else if (silent) {
                ElMessage.success(silentSuccessMessage);
            } else {
                ElMessage.success(t('stationLayout.messages.saveSuccess') + (result?.message || result));
            }
            if (shouldReloadGeneratedCellIds && !result?.savedJson && (repairCount === 0 || appliedSaved)) {
                getData({ stationSchemeId: savedStationSchemeId });
            }
            showTopologyRepairReminderAfterSave();
            return true;
        })
        .catch((err) => {
            const serverMsg = getHttpErrorMessage(err, t('stationLayout.messages.saveFailed'));
            if (silent) {
                ElMessage.error(silentFailurePrefix + serverMsg);
            } else {
                ElMessage.error(t('stationLayout.messages.saveFailed') + serverMsg);
            }
            return false;
        })
        .finally(() => {
            savingData.value = false;
        });
}
function getData(options = {}) {
    const loadVersion = ++layoutLoadVersion;
    cancelLoadedLayoutFit();
    if (!props.selectedInstanceId) {
        loadingData.value = false;
        currentStationSchemeId.value = "";
        stationSchemeOptions.value = [];
        resetLayoutDisplayStyles();
        resetLayoutGridSettings();
        stationLayoutEditorRef.value?.clearElements();
        routeNodePickTarget.value = "";
        clearRouteSearchResult();
        resetCells();
        setLayoutSnapshotFromJson({});
        return;
    }

    const instanceId = props.selectedInstanceId;
    const requestedStationSchemeId = options?.stationSchemeId ?? currentStationSchemeId.value;
    let expectedSelection = currentStationSchemeId.value;
    const isCurrentRequest = () => loadVersion === layoutLoadVersion && props.selectedInstanceId === instanceId
        && currentStationSchemeId.value === expectedSelection
        && (props.stationSchemeId === undefined || props.stationSchemeId === expectedSelection);
    loadingData.value = true;
    props.gateway
        .getJson({
            instanceId,
            stationSchemeId: requestedStationSchemeId,
        })
        .then(async (layout) => {
            if (!isCurrentRequest()) {
                return;
            }

            const prepared = props.gateway.repairJson
                ? await prepareLayoutInput(JSON.stringify(layout), instanceId, requestedStationSchemeId)
                : { document: layout, repairCount: 0 };
            if (!isCurrentRequest()) return;
            layout = prepared.document;

            currentStationSchemeId.value = layout?.metadata?.stationSchemeID || requestedStationSchemeId || "";
            expectedSelection = currentStationSchemeId.value;
            applyLayoutDisplayStyles(layout?.metadata?.displayStyles);
            applyLayoutGridSettings(layout?.metadata?.gridSettings);
            ensureCurrentStationSchemeOption(undefined, layout?.metadata?.revision ?? layout?.metadata?.Revision);
            await nextTick();
            if (!isCurrentRequest()) {
                return;
            }

            stationLayoutEditorRef.value?.loadDataFromJson(layout, {
                preserveDocument: isStationLayoutArchive(layout), resetHistory: true,
            });
            setLayoutSnapshotFromJson(layout);
            setCellsFromLayout(layout);
            notifyLayoutRepair(prepared.repairCount);
            routeNodePickTarget.value = "";
            clearRouteSearchResult();
            // Fit only after the loaded geometry and surrounding panels render.
            await nextTick();
            if (isCurrentRequest()) scheduleLoadedLayoutFit();
        })
        .catch((err) => {
            if (!isCurrentRequest()) {
                return;
            }

            const serverMsg = getHttpErrorMessage(err, t('stationLayout.messages.loadFailed'));
            ElMessage.error(t('stationLayout.messages.loadFailed') + serverMsg);
        })
        .finally(() => {
            if (isCurrentRequest()) {
                loadingData.value = false;
            }
        });
}

function handleStationSchemeChange(stationSchemeId) {
    if (!stationSchemeId) return;
    routeNodePickTarget.value = "";
    clearRouteSearchResult();
    topologyRepairPending.value = false;
    getData({ stationSchemeId });
}

function exportJson() {
    if (loadingData.value || savingData.value || importingData.value) {
        throw new Error(t('stationLayout.editor.json.busy'));
    }
    const dataStr = stationLayoutEditorRef.value?.buildJsonData();
    if (!dataStr) throw new Error(t('stationLayout.editor.json.noLayout'));
    if (!applyCellFormToSelected({ showWarning: true })) {
        throw new Error(t('stationLayout.editor.json.invalidCell'));
    }
    return buildLayoutJsonWithDisplayStyles(dataStr, { forFile: true });
}

function exportJsonFile() {
    try {
        const prettyJson = exportJson();
        const jsonObj = JSON.parse(prettyJson);
        const blob = new Blob([prettyJson], { type: "application/json;charset=utf-8" });
        const url = URL.createObjectURL(blob);
        const link = document.createElement("a");
        link.href = url;
        link.download = buildExportJsonFileName(jsonObj);
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        URL.revokeObjectURL(url);
        ElMessage.success(t('stationLayout.editor.json.exported'));
    } catch (err) {
        console.error("Failed to export station layout JSON:", err);
        ElMessage.error(`${t('stationLayout.editor.json.exportFailed')} ${getHttpErrorMessage(err, '')}`);
    }
}

function handleSelectedAnnotationChange(annotation) {
    selectedAnnotation.value = annotation;
}

function handleSelectedEquipmentChange(equipment) {
    refreshLayoutSnapshot();
    if (cellLinkPickMode.value) {
        if (equipment?.kind === "link") {
            addLinkToCell(equipment.id);
        }
        equipmentForm.value = {};
        equipmentFormBaseline.value = {};
        selectedEquipment.value = null;
        return;
    }

    selectedEquipment.value = equipment;
    if (!equipment) {
        equipmentForm.value = {};
        equipmentFormBaseline.value = {};
        return;
    }

    equipmentForm.value = buildEquipmentForm(equipment);
    equipmentFormBaseline.value = clonePlainObject(equipmentForm.value);
}

function clonePlainObject(value) {
    return JSON.parse(JSON.stringify(value || {}));
}

function readSignalType(data) {
    return data?.type || data?.SignalType || data?.signalType || "";
}

function readEquipmentDirection(data) {
    return data?.direction || data?.Direction || "";
}

function readEquipmentType(data) {
    return data?.type || data?.Type || "";
}

function buildEquipmentForm(equipment) {
    if (equipment?.batch) return buildBatchEquipmentForm(equipment);

    return buildSingleEquipmentForm(equipment);
}

function syncEquipmentFormPositionToBindingNode(form) {
    if (!BOUND_EQUIPMENT_KINDS.includes(form.kind)) return;
    const node = currentLayoutNodeById.value.get(normalizedString(form.bindingNodeID));
    if (!node) return;
    form.x = toNumber(node.x);
    form.y = toNumber(node.y);
}

function handleEquipmentBindingDropdownVisible(visible) {
    if (visible) refreshLayoutSnapshot();
}

function buildSingleEquipmentForm(equipment) {
    const data = equipment?.data || {};
    const shouldFallbackNameToId = ["signal", "switch", "platform"].includes(equipment?.kind);
    const equipmentType = equipment?.kind === "signal"
        ? normalizeSignalType(readSignalType(data) || DEFAULT_SIGNAL_TYPE)
        : equipment?.kind === "bufferStop"
            ? readEquipmentType(data) || DEFAULT_BUFFER_STOP_TYPE
            : equipment?.kind === "insulationJoint"
                ? normalizedString(readEquipmentType(data) || "normal").toLowerCase()
                : equipment?.kind === "switch"
                    ? normalizedString(readEquipmentType(data) || "single").toLowerCase()
                    : readEquipmentType(data);
    const form = {
        kind: equipment?.kind || "",
        originalId: data.id || equipment?.id || "",
        id: data.id || "",
        name: data.name ?? (shouldFallbackNameToId ? data.id || "" : ""),
        type: equipmentType,
        direction: equipment?.kind === "signal"
            ? normalizedString(readEquipmentDirection(data) || "e").toLowerCase()
            : readEquipmentDirection(data),
        bindingNodeID: normalizedString(data.bindingNodeID ?? data.BindingNodeID ?? ""),
        x: Number(data.x ?? data.position?.x ?? 0),
        y: Number(data.y ?? data.position?.y ?? 0),
        x1: Number(data.x1 ?? 0),
        y1: Number(data.y1 ?? 0),
        x2: Number(data.x2 ?? 0),
        y2: Number(data.y2 ?? 0),
        width: Number(data.width ?? 0),
        height: Number(data.height ?? 0),
        fromNodeID: data.fromNodeID || "",
        toNodeID: data.toNodeID || "",
        arrowDirection: String(data.arrowDirection ?? data.ArrowDirection ?? "").trim().toUpperCase(),
        arrowType: String(data.arrowType ?? data.ArrowType ?? "").trim().toUpperCase(),
        branchVectorListText: "",
    };

    if (equipment?.kind === "switch") {
        form.branchVectorListText = JSON.stringify(data.branchVectorList || [], null, 2);
    }

    syncEquipmentFormPositionToBindingNode(form);
    return form;
}

function getEmptyBatchEquipmentValue(field) {
    return ["x", "y", "x1", "y1", "x2", "y2", "width", "height"].includes(field) ? null : "";
}

function areEquipmentFormValuesEqual(a, b) {
    return Object.is(a, b);
}

function getCommonEquipmentFormValue(forms, field) {
    if (!Array.isArray(forms) || forms.length === 0) return getEmptyBatchEquipmentValue(field);

    const firstValue = forms[0]?.[field];
    return forms.every((form) => areEquipmentFormValuesEqual(form?.[field], firstValue))
        ? firstValue
        : getEmptyBatchEquipmentValue(field);
}

function buildBatchEquipmentForm(equipment) {
    const itemForms = (Array.isArray(equipment?.items) ? equipment.items : [])
        .map((item) => buildSingleEquipmentForm({
            kind: equipment.kind,
            id: item?.id,
            data: item?.data || {},
        }));

    const representativeForm = buildSingleEquipmentForm(equipment);
    if (itemForms.length === 0) {
        return {
            ...representativeForm,
            originalId: "",
            id: "",
        };
    }

    const batchForm = {
        ...representativeForm,
        originalId: "",
    };

    for (const field of Object.keys(batchForm)) {
        if (field === "kind" || field === "originalId") continue;
        batchForm[field] = getCommonEquipmentFormValue(itemForms, field);
    }

    return batchForm;
}

function toNumber(value) {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : 0;
}

function normalizedString(value) {
    return String(value ?? "").trim();
}

function normalizedUpperString(value) {
    return normalizedString(value).toUpperCase();
}

function shouldApplyEquipmentField(field, currentValue, options = {}) {
    if (options.changedOnly !== true) return true;

    const baselineValue = options.baseline?.[field];
    if ((baselineValue === null || baselineValue === undefined) && currentValue !== baselineValue) return true;

    const normalize = options.normalize || ((value) => value);
    return normalize(currentValue) !== normalize(baselineValue);
}

function assignEquipmentPatchField(patch, patchKey, formField, value, options = {}) {
    if (shouldApplyEquipmentField(formField, value, options)) {
        patch[patchKey] = options.normalize ? options.normalize(value) : value;
    }
}

function assignEquipmentPositionPatch(patch, form, options = {}) {
    const positionPatch = {};
    assignEquipmentPatchField(positionPatch, "x", "x", form.x, { ...options, normalize: toNumber });
    assignEquipmentPatchField(positionPatch, "y", "y", form.y, { ...options, normalize: toNumber });
    if (Object.keys(positionPatch).length > 0) {
        patch.position = positionPatch;
    }
}

function buildEquipmentPatchFromForm() {
    const form = equipmentForm.value;
    const patch = {
        id: normalizedString(BOUND_EQUIPMENT_KINDS.includes(form.kind) ? form.originalId : form.id),
    };

    if (form.kind === "signal") {
        patch.name = String(form.name || "").trim();
        patch.type = String(form.type || "").trim();
        patch.direction = String(form.direction || "").trim();
        patch.bindingNodeID = String(form.bindingNodeID || "").trim();
    } else if (form.kind === "switch") {
        patch.name = String(form.name || "").trim();
        patch.type = String(form.type || "").trim();
        patch.bindingNodeID = String(form.bindingNodeID || "").trim();
        try {
            const branchVectorList = form.branchVectorListText?.trim()
                ? JSON.parse(form.branchVectorListText)
                : [];
            if (!Array.isArray(branchVectorList)) {
                throw new Error("branchVectorList must be an array.");
            }
            patch.branchVectorList = branchVectorList;
        } catch (err) {
            ElMessage.error(t('stationLayout.editor.equipment.branchJsonInvalid'));
            return null;
        }
    } else if (form.kind === "platform") {
        patch.name = String(form.name || "").trim();
        patch.x = toNumber(form.x);
        patch.y = toNumber(form.y);
        patch.width = toNumber(form.width);
        patch.height = toNumber(form.height);
    } else if (form.kind === "insulationJoint") {
        patch.type = String(form.type || "").trim();
        patch.bindingNodeID = String(form.bindingNodeID || "").trim();
    } else if (form.kind === "bufferStop") {
        patch.type = String(form.type || DEFAULT_BUFFER_STOP_TYPE).trim();
        patch.direction = String(form.direction || DEFAULT_BUFFER_STOP_DIRECTION).trim();
        patch.bindingNodeID = String(form.bindingNodeID || "").trim();
    } else if (form.kind === "link") {
        patch.name = String(form.name || "").trim();
        patch.x1 = toNumber(form.x1);
        patch.y1 = toNumber(form.y1);
        patch.x2 = toNumber(form.x2);
        patch.y2 = toNumber(form.y2);
        patch.fromNodeID = String(form.fromNodeID || "").trim();
        patch.toNodeID = String(form.toNodeID || "").trim();
        patch.arrowDirection = String(form.arrowDirection || "").trim().toUpperCase();
        patch.arrowType = String(form.arrowType || "").trim().toUpperCase();
    }

    return patch;
}

function buildEquipmentPatchFromFormForSave(options = {}) {
    const form = equipmentForm.value;
    const patch = {};
    const includeId = options.includeId !== false;
    if (includeId) {
        patch.id = normalizedString(BOUND_EQUIPMENT_KINDS.includes(form.kind) ? form.originalId : form.id);
    }

    if (form.kind === "signal") {
        assignEquipmentPatchField(patch, "name", "name", form.name, { ...options, normalize: normalizedString });
        assignEquipmentPatchField(patch, "type", "type", form.type, { ...options, normalize: normalizedString });
        assignEquipmentPatchField(patch, "direction", "direction", form.direction, { ...options, normalize: normalizedString });
        assignEquipmentPatchField(patch, "bindingNodeID", "bindingNodeID", form.bindingNodeID, { ...options, normalize: normalizedString });
    } else if (form.kind === "switch") {
        assignEquipmentPatchField(patch, "name", "name", form.name, { ...options, normalize: normalizedString });
        assignEquipmentPatchField(patch, "type", "type", form.type, { ...options, normalize: normalizedString });
        assignEquipmentPatchField(patch, "bindingNodeID", "bindingNodeID", form.bindingNodeID, { ...options, normalize: normalizedString });
        if (shouldApplyEquipmentField("branchVectorListText", form.branchVectorListText, {
            ...options,
            normalize: (value) => String(value ?? "").trim(),
        })) {
            try {
                const branchVectorList = form.branchVectorListText?.trim()
                    ? JSON.parse(form.branchVectorListText)
                    : [];
                if (!Array.isArray(branchVectorList)) {
                    throw new Error("branchVectorList must be an array.");
                }
                patch.branchVectorList = branchVectorList;
            } catch (err) {
                ElMessage.error(t('stationLayout.editor.equipment.branchJsonInvalid'));
                return null;
            }
        }
    } else if (form.kind === "platform") {
        assignEquipmentPatchField(patch, "name", "name", form.name, { ...options, normalize: normalizedString });
        assignEquipmentPatchField(patch, "x", "x", form.x, { ...options, normalize: toNumber });
        assignEquipmentPatchField(patch, "y", "y", form.y, { ...options, normalize: toNumber });
        assignEquipmentPatchField(patch, "width", "width", form.width, { ...options, normalize: toNumber });
        assignEquipmentPatchField(patch, "height", "height", form.height, { ...options, normalize: toNumber });
    } else if (form.kind === "insulationJoint") {
        assignEquipmentPatchField(patch, "type", "type", form.type, { ...options, normalize: normalizedString });
        assignEquipmentPatchField(patch, "bindingNodeID", "bindingNodeID", form.bindingNodeID, { ...options, normalize: normalizedString });
    } else if (form.kind === "bufferStop") {
        assignEquipmentPatchField(patch, "type", "type", form.type || DEFAULT_BUFFER_STOP_TYPE, { ...options, normalize: normalizedString });
        assignEquipmentPatchField(patch, "direction", "direction", form.direction || DEFAULT_BUFFER_STOP_DIRECTION, { ...options, normalize: normalizedString });
        assignEquipmentPatchField(patch, "bindingNodeID", "bindingNodeID", form.bindingNodeID, { ...options, normalize: normalizedString });
    } else if (form.kind === "link") {
        assignEquipmentPatchField(patch, "name", "name", form.name, { ...options, normalize: normalizedString });
        assignEquipmentPatchField(patch, "x1", "x1", form.x1, { ...options, normalize: toNumber });
        assignEquipmentPatchField(patch, "y1", "y1", form.y1, { ...options, normalize: toNumber });
        assignEquipmentPatchField(patch, "x2", "x2", form.x2, { ...options, normalize: toNumber });
        assignEquipmentPatchField(patch, "y2", "y2", form.y2, { ...options, normalize: toNumber });
        assignEquipmentPatchField(patch, "fromNodeID", "fromNodeID", form.fromNodeID, { ...options, normalize: normalizedString });
        assignEquipmentPatchField(patch, "toNodeID", "toNodeID", form.toNodeID, { ...options, normalize: normalizedString });
        assignEquipmentPatchField(patch, "arrowDirection", "arrowDirection", form.arrowDirection, { ...options, normalize: normalizedUpperString });
        assignEquipmentPatchField(patch, "arrowType", "arrowType", form.arrowType, { ...options, normalize: normalizedUpperString });
    }

    return patch;
}

async function saveEquipmentForm() {
    if (!ensureWritable()) return;
    if (!selectedEquipment.value) return;
    const patch = buildEquipmentPatchFromFormForSave(isEquipmentBatchMode.value
        ? { changedOnly: true, includeId: false, baseline: equipmentFormBaseline.value }
        : {}
    );
    if (!patch) return;
    if (!isEquipmentBatchMode.value && !patch.id) {
        ElMessage.warning(t('stationLayout.editor.equipment.idRequired'));
        return;
    }

    if (isEquipmentBatchMode.value && Object.keys(patch).length === 0) {
        ElMessage.warning(t('stationLayout.editor.equipment.batchChangeRequired'));
        return;
    }

    equipmentSaving.value = true;
    try {
        const previousId = equipmentForm.value.originalId;
        if (isEquipmentBatchMode.value) {
            stationLayoutEditorRef.value?.updateSelectedEquipmentBatch(
                selectedEquipment.value.kind,
                selectedEquipment.value.ids || [],
                patch
            );
        } else {
            stationLayoutEditorRef.value?.updateSelectedEquipment(
                selectedEquipment.value.kind,
                previousId,
                patch
            );
        }
        if (!isEquipmentBatchMode.value && selectedEquipment.value.kind === "link") {
            updateCellLinkReferences(previousId, patch.id);
            refreshLayoutSnapshot();
        } else if (selectedEquipment.value.kind === "link") {
            refreshLayoutSnapshot();
        }
        const saved = await saveData({ silent: true });
        if (saved) {
            if (!isEquipmentBatchMode.value) {
                equipmentForm.value.originalId = patch.id;
            }
            equipmentFormBaseline.value = clonePlainObject(equipmentForm.value);
        }
    } finally {
        equipmentSaving.value = false;
    }
}

function updateSelectedAnnotation(patch) {
    if (!ensureWritable()) return;
    if (!selectedAnnotation.value) return;
    stationLayoutEditorRef.value?.updateSelectedAnnotation(patch);
}

function updateSelectedAnnotationPosition() {
    if (!selectedAnnotation.value) return;
    updateSelectedAnnotation({
        position: {
            x: selectedAnnotation.value.position?.x || 0,
            y: selectedAnnotation.value.position?.y || 0,
        },
    });
}

function buildExportJsonFileName(jsonObj) {
    const instanceID = jsonObj?.metadata?.instanceID || props.selectedInstanceId || "station-layout";
    const stationSchemeID = jsonObj?.metadata?.stationSchemeID || currentStationSchemeId.value || "scheme";
    const timestamp = new Date()
        .toISOString()
        .replace(/[-:]/g, "")
        .replace(/\.\d{3}Z$/, "");
    const safeName = `${instanceID}-${stationSchemeID}-${timestamp}`
        .replace(/[\\/:*?"<>|]/g, "_");
    return `${safeName}.json`;
}

function openImportJsonFile() {
    if (!ensureWritable()) return;
    if (loadingData.value || savingData.value || importingData.value) return;
    if (importJsonFileInputRef.value) {
        importJsonFileInputRef.value.value = "";
        importJsonFileInputRef.value.click();
    }
}

async function handleImportJsonFileChange(event) {
    if (!ensureWritable()) {
        event.target.value = "";
        return;
    }
    const file = event.target.files?.[0];
    if (!file) {
        return;
    }

    if (!file.name.toLowerCase().endsWith(".json")) {
        event.target.value = "";
        ElMessage.error(t('stationLayout.editor.json.fileRequired'));
        return;
    }

    const instanceId = props.selectedInstanceId;
    const stationSchemeId = currentStationSchemeId.value;
    const loadVersion = layoutLoadVersion;
    try {
        const text = await file.text();
        if (instanceId !== props.selectedInstanceId || stationSchemeId !== currentStationSchemeId.value
            || loadVersion !== layoutLoadVersion) throw new Error(t('stationLayout.editor.json.scopeChanged'));
        await importJson(text);
        ElMessage.success(t('stationLayout.editor.json.importReady'));
    } catch (err) {
        console.error("Failed to import station layout JSON:", err);
        ElMessage.error(`${t('stationLayout.editor.json.importFailed')} ${getHttpErrorMessage(err, '')}`);
    } finally {
        event.target.value = "";
    }
}

async function importJson(text) {
    if (props.readonly) throw new Error(t('stationLayout.messages.readonly'));
    if (loadingData.value || savingData.value || importingData.value) throw new Error(t('stationLayout.editor.json.busy'));
    const editor = stationLayoutEditorRef.value;
    if (!editor) throw new Error(t('stationLayout.editor.json.noLayout'));
    const instanceId = props.selectedInstanceId;
    const stationSchemeId = currentStationSchemeId.value;
    const loadVersion = ++layoutLoadVersion;
    const previousStyles = layoutDisplayStyles.value;
    const previousGrid = { showGrid: showGrid.value, spacing: gridSpacing.value };
    const isCurrent = () => loadVersion === layoutLoadVersion && instanceId === props.selectedInstanceId
        && stationSchemeId === currentStationSchemeId.value;
    importingData.value = true;
    cancelLoadedLayoutFit();
    let importedStyles;
    let importedGrid;
    try {
        // Repair and revalidate the entire file before changing the canvas or panels.
        const prepared = props.gateway.repairJson
            ? await prepareLayoutInput(text, instanceId, stationSchemeId)
            : { document: parseStationLayoutJson(text), repairCount: 0 };
        if (!isCurrent() || props.readonly) throw new Error(t('stationLayout.editor.json.scopeChanged'));
        const jsonObj = prepared.document;
        const preserveDocument = isStationLayoutArchive(jsonObj);
        jsonObj.metadata = { ...jsonObj.metadata, instanceID: instanceId, stationSchemeID: stationSchemeId, revision: getStationSchemeRevision() };
        jsonObj.cells = (jsonObj.cells || []).map(cell => ({ ...cell, instanceID: instanceId, stationSchemeID: stationSchemeId }));
        applyLayoutDisplayStyles(jsonObj.metadata.displayStyles);
        applyLayoutGridSettings(jsonObj.metadata.gridSettings);
        importedStyles = layoutDisplayStyles.value;
        importedGrid = { showGrid: showGrid.value, spacing: gridSpacing.value };
        await nextTick();
        if (!isCurrent() || props.readonly) throw new Error(t('stationLayout.editor.json.scopeChanged'));
        editor.loadDataFromJson(jsonObj, { preserveDocument, resetHistory: true });
        setLayoutSnapshotFromJson(jsonObj);
        setCellsFromLayout(jsonObj);
        routeNodePickTarget.value = "";
        cellLinkPickMode.value = false;
        clearRouteSearchResult();
        topologyRepairPending.value = false;
        equipmentDrawerVisible.value = false;
        notifyLayoutRepair(prepared.repairCount);
        await nextTick();
        if (isCurrent()) scheduleLoadedLayoutFit();
    } catch (err) {
        // A scheme switch may cancel us before its own request succeeds. Roll
        // back our staged controls only if no newer layout has replaced them.
        if (layoutDisplayStyles.value === importedStyles) {
            layoutDisplayStyles.value = previousStyles;
            if (showGrid.value === importedGrid.showGrid && gridSpacing.value === importedGrid.spacing) {
                applyLayoutGridSettings(previousGrid);
            }
        }
        throw err;
    } finally {
        importingData.value = false;
    }
}

function handleFileToolbarCommand(command) {
    if (command === "load") {
        getData();
    } else if (command === "save") {
        saveData();
    } else if (command === "importJson") {
        openImportJsonFile();
    } else if (command === "exportJson") {
        exportJsonFile();
    } else if (command === "extractDwg") {
        openExtractDwgDialog();
    }
}

function autoSeparateLine() {
    if (!ensureWritable()) return;
    stationLayoutEditorRef.value?.autoSeparateLine();
}
function showCrossPoint() {
    if (!ensureWritable()) return;
    stationLayoutEditorRef.value?.markCrossPoint();
}
function hideCrossPoint() {
    if (!ensureWritable()) return;
    stationLayoutEditorRef.value?.removeCrossPoint();
}
function snapLine() {
    if (!ensureWritable()) return;
    stationLayoutEditorRef.value?.snapLine();
}
function setDrawingObject(drawingObj) {
    if (!ensureWritable()) return;
    if (isSelectMode.value) return;
    const nextDrawingObject = String(drawingObj || "");
    if (!drawingObjectCodes.has(nextDrawingObject)) return;

    activeDrawingObject.value = nextDrawingObject;
    stationLayoutEditorRef.value?.setDrawingObject(nextDrawingObject);
}

function setDrawingSignalType(signalType) {
    if (!ensureWritable()) return;
    if (isSelectMode.value) return;
    const nextSignalType = Array.isArray(signalType) ? signalType[signalType.length - 1] : signalType;
    selectedDrawingSignalType.value = nextSignalType;
    stationLayoutEditorRef.value?.setDrawingSignalType(nextSignalType);
    setDrawingObject("s");
    signalDropdownRef.value?.handleClose?.();
}

function setDrawingBufferStopOption(option) {
    if (!ensureWritable()) return;
    if (isSelectMode.value) return;
    const type = String(option?.type || selectedDrawingBufferStopType.value || DEFAULT_BUFFER_STOP_TYPE);
    const direction = String(option?.direction || selectedDrawingBufferStopDirection.value || DEFAULT_BUFFER_STOP_DIRECTION);
    selectedDrawingBufferStopType.value = type;
    selectedDrawingBufferStopDirection.value = direction;
    stationLayoutEditorRef.value?.setDrawingBufferStopType(type);
    stationLayoutEditorRef.value?.setDrawingBufferStopDirection(direction);
    setDrawingObject("e");
}

function setDrawingBufferStopDirection(direction) {
    setDrawingBufferStopOption({
        type: selectedDrawingBufferStopType.value,
        direction,
    });
}

function autoGenerateNode() {
    if (!ensureWritable()) return;
    stationLayoutEditorRef.value?.autoGenerateNodes();
}

function autoMergeNode() {
    if (!ensureWritable()) return;
    stationLayoutEditorRef.value?.autoMergeNode();
}   

function correctEquipmentBindingNodes() {
    if (!ensureWritable()) return;
    const result = stationLayoutEditorRef.value?.correctEquipmentBindingNodesByPosition?.();
    if (!result) return;

    if (result.totalCount === 0) {
        ElMessage.info(t('stationLayout.editor.binding.none'));
        return;
    }

    const unmatchedText = result.unmatchedCount > 0 ? t('stationLayout.editor.binding.unmatchedSuffix', { count: result.unmatchedCount }) : "";
    if (result.fixedCount > 0) {
        ElMessage.success(t('stationLayout.editor.binding.fixed', { count: result.fixedCount, details: unmatchedText }));
    } else if (result.unmatchedCount > 0 && result.alreadyCorrectCount === 0) {
        ElMessage.warning(t('stationLayout.editor.binding.unmatched', { count: result.unmatchedCount }));
    } else {
        ElMessage.info(t('stationLayout.editor.binding.correct', { details: unmatchedText }));
    }
}

function openEquipmentBindingCorrectionDialog() {
    if (!ensureWritable()) return;
    const plan = stationLayoutEditorRef.value?.getEquipmentBindingNodeCorrectionPlan?.();
    if (!plan) return;

    if (plan.totalCount === 0) {
        ElMessage.info(t('stationLayout.editor.binding.none'));
        return;
    }

    if (!Array.isArray(plan.items) || plan.items.length === 0) {
        const unmatchedText = plan.unmatchedCount > 0 ? t('stationLayout.editor.binding.unmatchedSuffix', { count: plan.unmatchedCount }) : "";
        ElMessage.info(t('stationLayout.editor.binding.correct', { details: unmatchedText }));
        return;
    }

    bindingCorrectionPlan.value = plan;
    bindingCorrectionRows.value = plan.items;
    selectedBindingCorrectionRows.value = [];
    bindingCorrectionDialogVisible.value = true;
    nextTick(() => {
        bindingCorrectionTableRef.value?.clearSelection?.();
        for (const row of bindingCorrectionRows.value) {
            bindingCorrectionTableRef.value?.toggleRowSelection?.(row, true);
        }
    });
}

function handleBindingCorrectionSelectionChange(selection) {
    selectedBindingCorrectionRows.value = Array.isArray(selection) ? selection : [];
}

function closeBindingCorrectionDialog() {
    bindingCorrectionDialogVisible.value = false;
}

function handleBindingCorrectionDialogClosed() {
    bindingCorrectionPlan.value = null;
    bindingCorrectionRows.value = [];
    selectedBindingCorrectionRows.value = [];
}

function applySelectedBindingCorrections() {
    if (!ensureWritable()) return;
    if (selectedBindingCorrectionRows.value.length === 0) {
        ElMessage.warning(t('stationLayout.editor.binding.selectFirst'));
        return;
    }

    const result = stationLayoutEditorRef.value?.applyEquipmentBindingNodeCorrections?.(selectedBindingCorrectionRows.value);
    if (!result) return;

    bindingCorrectionDialogVisible.value = false;

    const skippedText = result.unmatchedCount > 0 ? t('stationLayout.editor.binding.skippedSuffix', { count: result.unmatchedCount }) : "";
    if (result.fixedCount > 0) {
        ElMessage.success(t('stationLayout.editor.binding.fixed', { count: result.fixedCount, details: skippedText }));
    } else if (result.alreadyCorrectCount > 0) {
        ElMessage.info(t('stationLayout.editor.binding.selectedCorrect', { details: skippedText }));
    } else {
        ElMessage.warning(t('stationLayout.editor.binding.notFixed', { details: skippedText }));
    }
}

function formatSwitchLineIds(lineIds) {
    return Array.isArray(lineIds) && lineIds.length > 0 ? lineIds.join(", ") : t('stationLayout.editor.common.none');
}

function formatSwitchRebuildMessage(plan) {
    const items = plan?.reconstructItems || [];
    const details = items
        .slice(0, 8)
        .map((item) => {
            const switchLabel = item.switchName || item.switchId || t('stationLayout.editor.switches.untitled');
            return t('stationLayout.editor.switches.change', { name: switchLabel, node: item.nodeName || item.nodeId, from: formatSwitchLineIds(item.previousLineIds), to: formatSwitchLineIds(item.nextLineIds) });
        })
        .join("\n");
    const moreText = items.length > 8 ? t('stationLayout.editor.switches.more', { count: items.length }) : "";
    const createText = plan?.createCount > 0 ? t('stationLayout.editor.switches.alsoCreate', { count: plan.createCount }) : "";

    return t('stationLayout.editor.switches.confirm', { count: items.length, details: details ? `\n\n${details}${moreText}` : "", create: createText });
}

async function confirmSwitchRebuild(plan) {
    if (!plan?.requiresConfirmation) return true;

    try {
        await ElMessageBox.confirm(
            formatSwitchRebuildMessage(plan),
            t('stationLayout.editor.switches.reconstructTitle'),
            {
                confirmButtonText: t('stationLayout.editor.switches.reconstruct'),
                cancelButtonText: t('stationLayout.editor.common.cancel'),
                type: "warning",
            }
        );
        return true;
    } catch (err) {
        return false;
    }
}

async function autoGenerateSwitch() {
    if (!ensureWritable()) return;
    const editor = stationLayoutEditorRef.value;
    if (!editor) return;

    const plan = editor.getAutoGenerateSwitchPlan?.();
    if (!plan) return;

    const canRebuild = await confirmSwitchRebuild(plan);
    if (!canRebuild) return;

    const result = editor.autoGenerateSwitches({ plan, confirmed: true });
    if (!result?.applied) {
        ElMessage.info(t('stationLayout.editor.switches.noChanges'));
        return;
    }

    const messageParts = [];
    if (result.reconstructCount > 0) messageParts.push(t('stationLayout.editor.switches.reconstructed', { count: result.reconstructCount }));
    if (result.createCount > 0) messageParts.push(t('stationLayout.editor.switches.created', { count: result.createCount }));
    ElMessage.success(t('stationLayout.editor.switches.generated', { details: messageParts.join(", ") }));
}

function autoGenerateCurve() {
    if (!ensureWritable()) return;
    const count = stationLayoutEditorRef.value?.autoGenerateCurves?.() ?? 0;
    ElMessage.success(t('stationLayout.editor.topology.curvesGenerated', { count }));
}

function openExtractDwgDialog() {
    if (!ensureWritable()) return;
    selectedDwgFile.value = null;
    dwgLayerName.value = "0";
    extractDwgDialogVisible.value = true;
    if (dwgFileInputRef.value) {
        dwgFileInputRef.value.value = "";
    }
}

function handleDwgFileChange(event) {
    if (!ensureWritable()) {
        selectedDwgFile.value = null;
        event.target.value = "";
        return;
    }
    const file = event.target.files?.[0];
    if (!file) {
        selectedDwgFile.value = null;
        return;
    }

    if (!file.name.toLowerCase().endsWith(".dwg")) {
        selectedDwgFile.value = null;
        event.target.value = "";
        ElMessage.error(t('stationLayout.editor.dwg.invalidFile'));
        return;
    }

    selectedDwgFile.value = file;
}

function extractDwgFile() {
    if (!ensureWritable()) return;
    if (!selectedDwgFile.value) {
        ElMessage.warning(t('stationLayout.editor.dwg.fileRequired'));
        return;
    }

    extractingDwg.value = true;
    props.gateway
        .extractDwgFile({
            file: selectedDwgFile.value,
            layerName: dwgLayerName.value || "0",
        })
        .then(async (result) => {
            extractDwgDialogVisible.value = false;
            const layout = result?.layout;
            if (!layout) {
                ElMessage.error(t('stationLayout.editor.dwg.noData'));
                return;
            }

            validateStationLayoutJson(layout);
            stationLayoutEditorRef.value?.clearElements();
            currentStationSchemeId.value = layout?.metadata?.stationSchemeID || currentStationSchemeId.value;
            applyLayoutGridSettings(layout?.metadata?.gridSettings);
            ensureCurrentStationSchemeOption();
            await nextTick();
            stationLayoutEditorRef.value?.loadDataFromJson(layout);
            setLayoutSnapshotFromJson(layout);
            setCellsFromLayout(layout);
            ElMessage.success(t('stationLayout.editor.dwg.completed', { count: result?.segmentCount || 0 }));
        })
        .catch((err) => {
            ElMessage.error(t('stationLayout.editor.dwg.failed') + getHttpErrorMessage(err, t('stationLayout.editor.common.unknownError')));
        })
        .finally(() => {
            extractingDwg.value = false;
        });
}

onMounted(() => {
    document.addEventListener("keydown", handleStationLayoutKeydown);
    document.addEventListener("keyup", handleStationLayoutKeyup);
    window.addEventListener("blur", restoreF4HoldEditMode);
    loadStationSchemes();
    getData();
});

onBeforeUnmount(() => {
    layoutLoadVersion += 1;
    cancelLoadedLayoutFit();
    document.removeEventListener("keydown", handleStationLayoutKeydown);
    document.removeEventListener("keyup", handleStationLayoutKeyup);
    window.removeEventListener("blur", restoreF4HoldEditMode);
});

watch(
    () => props.selectedInstanceId,
    () => {
        stationSchemeManagerVisible.value = false;
        resetStationSchemeDraft();
        cancelStationSchemeEdit();
        currentStationSchemeId.value = props.stationSchemeId || "";
        stationSchemeOptions.value = [];
        routeNodePickTarget.value = "";
        clearRouteSearchResult();
        resetCells();
        setLayoutSnapshotFromJson({});
        loadStationSchemes();
        getData();
    }
);

watch(currentStationSchemeId, (stationSchemeId) => {
    emit('update:stationSchemeId', stationSchemeId);
}, { flush: 'sync' });

watch(() => props.stationSchemeId, (stationSchemeId) => {
    if (stationSchemeId === undefined || stationSchemeId === currentStationSchemeId.value) return;
    currentStationSchemeId.value = stationSchemeId;
    if (stationSchemeId) {
        ensureCurrentStationSchemeOption();
        handleStationSchemeChange(stationSchemeId);
        void loadStationSchemes();
    } else {
        layoutLoadVersion += 1;
        loadingData.value = false;
        cancelLoadedLayoutFit();
        stationLayoutEditorRef.value?.clearElements();
        routeNodePickTarget.value = "";
        clearRouteSearchResult();
        resetCells();
        setLayoutSnapshotFromJson({});
    }
});

watch(
    () => props.readonly,
    (readonly) => {
        if (!readonly) return;
        f4HoldPreviousEditMode = null;
        setSelectMode();
        cellLinkPickMode.value = false;
        bindingCorrectionDialogVisible.value = false;
        extractDwgDialogVisible.value = false;
        resetStationSchemeDraft();
        cancelStationSchemeEdit();
    },
    { immediate: true }
);
defineExpose({ exportJson, importJson });
</script>

<template>
    <div v-loading="loadingData || savingData || importingData" class="station-layout-page">
        <StationLayoutEditToolbar v-model:density="editToolbarDensity" :translate="t">
            <template #context>
                <div class="station-scheme-control-row">
                    <span class="station-toolbar-group__label">{{ t('stationLayout.menu.stationScheme') }}</span>
                    <el-select v-model="currentStationSchemeId" size="small" filterable
                        class="station-scheme-select" :loading="loadingStationSchemes"
                        :disabled="!props.selectedInstanceId || loadingStationSchemes || loadingData || savingData"
                        :placeholder="t('stationLayout.placeholders.selectStationScheme')"
                        @change="handleStationSchemeChange">
                        <el-option v-for="option in stationSchemeOptions" :key="option.id"
                            :label="formatStationSchemeLabel(option)" :value="option.id" />
                    </el-select>
                    <el-button size="small" :icon="Grid"
                        :disabled="!props.selectedInstanceId || loadingStationSchemes || loadingData || savingData"
                        @click.stop="openStationSchemeManager">
                        {{ t('stationLayout.schemeManager.manage') }}
                    </el-button>
                </div>
            </template>

            <template #primary>
                <el-radio-group v-model="activeEditMode" class="mode-toggle" size="small" :disabled="props.readonly"
                    @change="handleEditModeChange">
                    <el-radio-button :value="0">
                        <el-icon><Pointer /></el-icon>
                        <span>{{ t('stationLayout.mode.select') }}</span>
                    </el-radio-button>
                    <el-radio-button :value="1">
                        <el-icon><EditPen /></el-icon>
                        <span>{{ t('stationLayout.mode.draw') }}</span>
                    </el-radio-button>
                </el-radio-group>
            </template>

            <template #actions>
                <el-button-group>
                    <el-button size="small" :icon="RefreshLeft" :disabled="props.readonly" @click="revoke">
                        {{ t('stationLayout.menu.undo') }}
                    </el-button>
                    <el-button size="small" :icon="RefreshRight" :disabled="props.readonly" @click="redo">
                        {{ t('stationLayout.menu.redo') }}
                    </el-button>
                </el-button-group>
                <el-button size="small" :icon="Aim" @click="fitFullLayout">
                    {{ t('stationLayout.tools.fitFullView') }}
                </el-button>
                <el-dropdown v-if="editToolbarDensity === 'compact'" trigger="click" @command="handleFileToolbarCommand">
                    <el-button size="small" :icon="Download">
                        {{ t('stationLayout.menu.file') }}
                        <el-icon class="el-icon--right"><ArrowDown /></el-icon>
                    </el-button>
                    <template #dropdown>
                        <el-dropdown-menu>
                            <el-dropdown-item command="load">{{ t('stationLayout.menu.loadData') }}</el-dropdown-item>
                            <el-dropdown-item command="importJson" :disabled="props.readonly">{{ t('stationLayout.menu.importJson') }}</el-dropdown-item>
                            <el-dropdown-item command="exportJson">{{ t('stationLayout.menu.exportJson') }}</el-dropdown-item>
                            <el-dropdown-item command="extractDwg" :disabled="props.readonly">{{ t('stationLayout.menu.extractDwg') }}</el-dropdown-item>
                        </el-dropdown-menu>
                    </template>
                </el-dropdown>
                <el-button type="primary" size="small" :icon="Upload" :loading="savingData"
                    :disabled="props.readonly" @click="saveData">
                    {{ t('stationLayout.menu.saveData') }}
                </el-button>
            </template>

            <template #essential>
                <div class="station-toolbar-group">
                    <span class="station-toolbar-group__label">{{ t('stationLayout.group.drawingObject') }}</span>
                    <el-button-group>
                        <el-button size="small" :icon="Minus" :disabled="props.readonly || isSelectMode" :type="getDrawingButtonType('l')"
                            @click="setDrawingObject('l')">{{ t('stationLayout.draw.line') }}</el-button>
                        <el-button size="small" :icon="Location" :disabled="props.readonly || isSelectMode" :type="getDrawingButtonType('n')"
                            @click="setDrawingObject('n')">{{ t('stationLayout.draw.node') }}</el-button>
                        <el-dropdown ref="signalDropdownRef" trigger="click" :disabled="props.readonly || isSelectMode"
                            @command="setDrawingSignalType">
                            <el-button size="small" :icon="Bell" :disabled="props.readonly || isSelectMode" :type="getDrawingButtonType('s')"
                                @click="setDrawingObject('s')">
                                <span class="drawing-object-button-label">{{ drawingSignalButtonLabel }}</span>
                                <el-icon class="el-icon--right"><ArrowDown /></el-icon>
                            </el-button>
                            <template #dropdown>
                                <el-dropdown-menu class="drawing-object-dropdown-menu">
                                    <template v-for="(group, groupIndex) in drawingSignalMenuGroups" :key="group.value">
                                        <el-dropdown-item v-if="group.showLabel" disabled class="drawing-option-group-label"
                                            :divided="groupIndex > 0">{{ group.label }}</el-dropdown-item>
                                        <el-dropdown-item v-for="option in group.options" :key="option.value"
                                            :command="option.value">{{ option.label }}</el-dropdown-item>
                                    </template>
                                </el-dropdown-menu>
                            </template>
                        </el-dropdown>
                        <el-button size="small" :icon="Switch" :disabled="props.readonly || isSelectMode" :type="getDrawingButtonType('w')"
                            @click="setDrawingObject('w')">{{ t('stationLayout.draw.switch') }}</el-button>
                        <el-button size="small" :icon="Filter" :disabled="props.readonly || isSelectMode" :type="getDrawingButtonType('i')"
                            @click="setDrawingObject('i')">{{ t('stationLayout.draw.insulation') }}</el-button>
                        <el-button size="small" :icon="Guide" :disabled="props.readonly || isSelectMode" :type="getDrawingButtonType('r')"
                            @click="setDrawingObject('r')">{{ t('stationLayout.draw.route') }}</el-button>
                        <el-dropdown trigger="click" :disabled="props.readonly || isSelectMode" @command="setDrawingBufferStopOption">
                            <el-button size="small" :icon="Stopwatch" :disabled="props.readonly || isSelectMode" :type="getDrawingButtonType('e')"
                                @click="setDrawingObject('e')">
                                <span class="drawing-object-button-label">{{ drawingBufferStopButtonLabel }}</span>
                                <el-icon class="el-icon--right"><ArrowDown /></el-icon>
                            </el-button>
                            <template #dropdown>
                                <el-dropdown-menu>
                                    <template v-for="(typeOption, typeIndex) in bufferStopTypeOptions" :key="typeOption.value">
                                        <el-dropdown-item disabled class="drawing-option-group-label" :divided="typeIndex > 0">
                                            {{ typeOption.label }}
                                        </el-dropdown-item>
                                        <el-dropdown-item v-for="directionOption in bufferStopDirectionOptions"
                                            :key="`${typeOption.value}-${directionOption.value}`"
                                            :command="{ type: typeOption.value, direction: directionOption.value }">
                                            {{ directionOption.label }}
                                        </el-dropdown-item>
                                    </template>
                                </el-dropdown-menu>
                            </template>
                        </el-dropdown>
                        <el-button size="small" :icon="Platform" :disabled="props.readonly || isSelectMode" :type="getDrawingButtonType('p')"
                            @click="setDrawingObject('p')">{{ t('stationLayout.draw.platform') }}</el-button>
                        <el-button size="small" :icon="EditPen" :disabled="props.readonly || isSelectMode" :type="getDrawingButtonType('a')"
                            @click="setDrawingObject('a')">{{ t('stationLayout.draw.annotation') }}</el-button>
                    </el-button-group>
                </div>
                <div class="station-toolbar-group">
                    <span class="station-toolbar-group__label">{{ t('stationLayout.group.selection') }}</span>
                    <el-button-group>
                        <el-button size="small" :icon="CircleClose" @click="clearSelection">
                            {{ t('stationLayout.menu.clearSelection') }}
                        </el-button>
                        <el-button size="small" :icon="Delete" type="danger" plain :disabled="props.readonly"
                            @click="deleteSelection">
                            {{ t('stationLayout.menu.deleteSelection') }}
                        </el-button>
                    </el-button-group>
                </div>
                <div class="station-toolbar-group">
                    <span class="station-toolbar-group__label">{{ t('stationLayout.group.display') }}</span>
                    <div class="station-toolbar-switch-control">
                        <span class="station-toolbar-switch-control__label">{{ t('stationLayout.menu.showGrid') }}</span>
                        <el-switch v-model="showGrid" size="small" />
                    </div>
                    <div class="station-toolbar-switch-control">
                        <span class="station-toolbar-switch-control__label">{{ t('stationLayout.draw.node') }}</span>
                        <el-switch v-model="showNodes" size="small" />
                    </div>
                </div>
                <div class="station-toolbar-group">
                    <span class="station-toolbar-group__label">{{ t('stationLayout.group.panels') }}</span>
                    <el-button size="small" :icon="Magnet" :type="cellPanelVisible ? 'primary' : 'default'"
                        :aria-pressed="cellPanelVisible" @click="toggleCellPanel">
                        {{ t('stationLayout.panels.trackCircuits') }}
                    </el-button>
                    <el-button size="small" :icon="SetUp" :type="equipmentDrawerVisible ? 'primary' : 'default'"
                        :aria-pressed="equipmentDrawerVisible" @click="equipmentDrawerVisible = !equipmentDrawerVisible">
                        {{ t('stationLayout.panels.equipment') }}
                    </el-button>
                </div>
                <div class="station-toolbar-group scale-toolbar-group">
                    <span class="station-toolbar-group__label">{{ t('stationLayout.group.displayScale') }}</span>
                    <div class="scale-slider">
                        <span class="scale-slider-label">{{ t('stationLayout.scale.x') }}</span>
                        <el-slider v-model="layoutScaleX" :min="0.25" :max="4" :step="0.05" size="small" />
                        <span class="scale-slider-value">{{ layoutScaleXDisplay }}</span>
                    </div>
                    <div class="scale-slider">
                        <span class="scale-slider-label">{{ t('stationLayout.scale.y') }}</span>
                        <el-slider v-model="layoutScaleY" :min="0.25" :max="4" :step="0.05" size="small" />
                        <span class="scale-slider-value">{{ layoutScaleYDisplay }}</span>
                    </div>
                </div>
            </template>

            <template #advanced>
                <div class="station-toolbar-group">
                    <span class="station-toolbar-group__label">{{ t('stationLayout.menu.file') }}</span>
                    <el-button-group>
                        <el-button size="small" :icon="Download" @click="getData">{{ t('stationLayout.menu.loadData') }}</el-button>
                        <el-button size="small" :icon="Upload" :disabled="props.readonly"
                            @click="openImportJsonFile">{{ t('stationLayout.menu.importJson') }}</el-button>
                        <el-button size="small" :icon="Download" @click="exportJsonFile">{{ t('stationLayout.menu.exportJson') }}</el-button>
                        <el-button size="small" :icon="Download" :disabled="props.readonly"
                            @click="openExtractDwgDialog">{{ t('stationLayout.menu.extractDwg') }}</el-button>
                    </el-button-group>
                </div>
                <div class="station-toolbar-group">
                    <span class="station-toolbar-group__label">{{ t('stationLayout.group.snapping') }}</span>
                    <div class="station-toolbar-switch-control">
                        <span class="station-toolbar-switch-control__label">{{ t('stationLayout.menu.gridSnap') }}</span>
                        <el-switch v-model="mouseSnap" size="small" @change="mouseGridSnapChange" />
                    </div>
                    <div class="station-toolbar-switch-control">
                        <span class="station-toolbar-switch-control__label">{{ t('stationLayout.menu.objectSnap') }}</span>
                        <el-switch v-model="objectSnap" size="small" @change="mouseObjectSnapChange" />
                    </div>
                    <div class="toolbar-field-item">
                        <span class="toolbar-group-label">{{ t('stationLayout.menu.snapDistance') }}</span>
                        <el-input-number v-model="objectSnapDistance" size="small" :min="0" :max="200" :step="1"
                            controls-position="right" :disabled="!objectSnap" />
                    </div>
                    <div class="toolbar-field-item">
                        <span class="toolbar-group-label">{{ t('stationLayout.menu.gridSpacing') }}</span>
                        <el-input-number v-model="gridSpacing" size="small" :min="1" :max="500" :step="1"
                            controls-position="right" />
                    </div>
                </div>
                <div class="station-toolbar-group">
                    <span class="station-toolbar-group__label">{{ t('stationLayout.group.topology') }}</span>
                    <el-radio-group v-model="topologyGenerationMode" size="small" :disabled="props.readonly">
                        <el-radio-button value="auto">{{ t('stationLayout.topology.auto') }}</el-radio-button>
                        <el-radio-button value="manual">{{ t('stationLayout.topology.manual') }}</el-radio-button>
                    </el-radio-group>
                    <el-button-group>
                        <el-button size="small" :icon="Aim" :disabled="props.readonly" @click="showCrossPoint">{{ t('stationLayout.tools.showCrossPoint') }}</el-button>
                        <el-button size="small" :icon="Hide" :disabled="props.readonly" @click="hideCrossPoint">{{ t('stationLayout.tools.hideCrossPoint') }}</el-button>
                        <el-button size="small" :icon="Connection" :disabled="props.readonly" @click="snapLine">{{ t('stationLayout.tools.snapLine') }}</el-button>
                        <el-button size="small" :icon="Scissor" :disabled="props.readonly" @click="autoSeparateLine">{{ t('stationLayout.tools.separateLine') }}</el-button>
                        <el-button size="small" :icon="Share" :disabled="props.readonly" @click="autoGenerateNode">{{ t('stationLayout.tools.generateNode') }}</el-button>
                        <el-button size="small" :icon="Share" :disabled="props.readonly" @click="autoMergeNode">{{ t('stationLayout.tools.mergeNodes') }}</el-button>
                        <el-button size="small" :icon="SetUp" :disabled="props.readonly" @click="autoGenerateSwitch">{{ t('stationLayout.tools.generateSwitch') }}</el-button>
                        <el-button size="small" :icon="Connection" :disabled="props.readonly" @click="autoGenerateCurve">{{ t('stationLayout.tools.generateCurve') }}</el-button>
                        <el-button size="small" :icon="Connection" :disabled="props.readonly"
                            @click="openEquipmentBindingCorrectionDialog">{{ t('stationLayout.tools.correctBindings') }}</el-button>
                    </el-button-group>
                </div>
                <div class="station-toolbar-group">
                    <span class="station-toolbar-group__label">{{ t('stationLayout.group.pathTest') }}</span>
                    <el-button size="small" :icon="Guide" :type="routeTesterVisible ? 'primary' : 'default'" @click="toggleRouteTester">{{ t('stationLayout.tools.searchPath') }}</el-button>
                </div>
                <div class="station-toolbar-group">
                    <span class="station-toolbar-group__label">{{ t('stationLayout.group.display') }}</span>
                    <div class="station-toolbar-switch-control">
                        <span class="station-toolbar-switch-control__label">{{ t('stationLayout.group.curveDisplay') }}</span>
                        <el-switch v-model="showCurveArc" size="small" />
                        <span class="station-toolbar-switch-control__state">
                            {{ showCurveArc ? t('stationLayout.curveDisplay.arc') : t('stationLayout.curveDisplay.tangent') }}
                        </span>
                    </div>
                    <div class="station-toolbar-switch-control">
                        <span class="station-toolbar-switch-control__label">{{ t('stationLayout.menu.showCellNames') }}</span>
                        <el-switch v-model="showCellNames" size="small" />
                    </div>
                    <el-button size="small" :icon="SetUp" @click="layoutStyleDialogVisible = true">{{ t('stationLayout.tools.displayStyle') }}</el-button>
                </div>
            </template>
        </StationLayoutEditToolbar>
        <input ref="importJsonFileInputRef" type="file" accept=".json,application/json" class="hidden-file-input"
            :disabled="props.readonly"
            @change="handleImportJsonFileChange" />

        <el-dialog v-model="stationSchemeManagerVisible" :title="t('stationLayout.schemeManager.title')" width="760px"
            :close-on-click-modal="false" :close-on-press-escape="!stationSchemeManagerSaving"
            :show-close="!stationSchemeManagerSaving" @closed="resetStationSchemeDraft(); cancelStationSchemeEdit()">
            <div class="station-scheme-manager">
                <div class="station-scheme-create-row">
                    <el-button type="primary" size="small"
                        :disabled="props.readonly || loadingStationSchemes || stationSchemeManagerSaving || !!stationSchemeDraft || !!editingStationSchemeOriginalId"
                        @click="startNewStationScheme">
                        {{ t('stationLayout.schemeManager.add') }}
                    </el-button>
                </div>
                <el-table :data="stationSchemeManagerRows" v-loading="loadingStationSchemes || stationSchemeManagerSaving"
                    :row-key="row => row.isDraft ? 'draft' : row.id" height="360" class="station-scheme-table">
                    <el-table-column prop="id" :label="t('stationLayout.schemeManager.id')" width="220">
                        <template #default="{ row }">
                            <el-tag v-if="row.isDraft" type="info" size="small">{{ t('stationLayout.schemeManager.pendingSave') }}</el-tag>
                            <span v-else>{{ row.id }}</span>
                        </template>
                    </el-table-column>
                    <el-table-column prop="name" :label="t('stationLayout.schemeManager.name')">
                        <template #default="{ row }">
                            <el-input v-if="row.isDraft" ref="stationSchemeDraftInputRef"
                                v-model="row.name" size="small" maxlength="100"
                                :disabled="props.readonly || stationSchemeManagerSaving"
                                :placeholder="t('stationLayout.schemeManager.namePlaceholder')" @keyup.enter="createStationScheme" />
                            <el-input v-else-if="editingStationSchemeOriginalId === row.id"
                                v-model="editingStationSchemeForm.name" size="small" maxlength="100"
                                :disabled="props.readonly || stationSchemeManagerSaving" @keyup.enter="saveStationSchemeEdit" />
                            <span v-else>{{ row.name || row.id }}</span>
                        </template>
                    </el-table-column>
                    <el-table-column :label="t('stationLayout.schemeManager.operation')" width="240">
                        <template #default="{ row }">
                            <div v-if="row.isDraft" class="station-scheme-actions">
                                <el-button type="success" size="small" :disabled="props.readonly || stationSchemeManagerSaving"
                                    @click="createStationScheme">{{ t('stationLayout.schemeManager.save') }}</el-button>
                                <el-button size="small" :disabled="stationSchemeManagerSaving"
                                    @click="resetStationSchemeDraft">{{ t('stationLayout.schemeManager.cancel') }}</el-button>
                            </div>
                            <div v-else-if="editingStationSchemeOriginalId === row.id" class="station-scheme-actions">
                                <el-button type="success" size="small" :disabled="props.readonly || stationSchemeManagerSaving"
                                    @click="saveStationSchemeEdit">
                                    {{ t('stationLayout.schemeManager.save') }}
                                </el-button>
                                <el-button size="small" :disabled="stationSchemeManagerSaving" @click="cancelStationSchemeEdit">
                                    {{ t('stationLayout.schemeManager.cancel') }}
                                </el-button>
                            </div>
                            <div v-else class="station-scheme-actions">
                                <el-button type="primary" size="small" :disabled="props.readonly || stationSchemeManagerSaving || !!stationSchemeDraft || !!editingStationSchemeOriginalId"
                                    @click="startEditStationScheme(row)">
                                    {{ t('stationLayout.schemeManager.edit') }}
                                </el-button>
                                <el-button size="small" :disabled="props.readonly || stationSchemeManagerSaving || !!stationSchemeDraft || !!editingStationSchemeOriginalId"
                                    @click="copyStationScheme(row)">{{ t('stationLayout.schemeManager.copy') }}</el-button>
                                <el-button type="danger" size="small" :disabled="props.readonly || stationSchemeManagerSaving || !!stationSchemeDraft || !!editingStationSchemeOriginalId"
                                    @click="deleteStationScheme(row)">
                                    {{ t('stationLayout.schemeManager.delete') }}
                                </el-button>
                            </div>
                        </template>
                    </el-table-column>
                </el-table>
            </div>
            <template #footer>
                <el-button :disabled="stationSchemeManagerSaving" @click="stationSchemeManagerVisible = false">
                    {{ t('stationLayout.schemeManager.close') }}
                </el-button>
            </template>
        </el-dialog>

        <el-dialog v-model="bindingCorrectionDialogVisible" :title="t('stationLayout.editor.binding.title')" width="860px"
            :close-on-click-modal="false" @closed="handleBindingCorrectionDialogClosed">
            <div class="binding-correction-dialog">
                <div class="binding-correction-summary">
                    {{ t('stationLayout.editor.binding.summary', { count: bindingCorrectionRows.length }) }}
                    <span v-if="bindingCorrectionPlan?.unmatchedCount > 0">
                        {{ t('stationLayout.editor.binding.skipped', { count: bindingCorrectionPlan.unmatchedCount }) }}
                    </span>
                </div>
                <el-table ref="bindingCorrectionTableRef" :data="bindingCorrectionRows" row-key="key" size="small"
                    height="360" @selection-change="handleBindingCorrectionSelectionChange">
                    <el-table-column type="selection" width="48" />
                    <el-table-column :label="t('stationLayout.equipment.type')" width="110">
                        <template #default="{ row }">{{ equipmentKindLabels[row.kind] || row.kindLabel }}</template>
                    </el-table-column>
                    <el-table-column :label="t('stationLayout.equipment.generic')" min-width="160" show-overflow-tooltip>
                        <template #default="{ row }">
                            <span>{{ row.equipmentName || row.equipmentId }}</span>
                        </template>
                    </el-table-column>
                    <el-table-column prop="previousBindingNodeID" :label="t('stationLayout.editor.binding.previous')" width="150" />
                    <el-table-column prop="nextBindingNodeID" :label="t('stationLayout.editor.binding.next')" width="140" />
                    <el-table-column :label="t('stationLayout.editor.binding.position')" width="150">
                        <template #default="{ row }">
                            <span>({{ row.position?.x }}, {{ row.position?.y }})</span>
                        </template>
                    </el-table-column>
                </el-table>
            </div>
            <template #footer>
                <div class="binding-correction-footer">
                    <span>{{ t('stationLayout.editor.binding.selected', { selected: selectedBindingCorrectionRows.length, total: bindingCorrectionRows.length }) }}</span>
                    <div class="binding-correction-actions">
                        <el-button @click="closeBindingCorrectionDialog">{{ t('stationLayout.editor.common.cancel') }}</el-button>
                        <el-button type="primary" :disabled="props.readonly || selectedBindingCorrectionRows.length === 0"
                            @click="applySelectedBindingCorrections">
                            {{ t('stationLayout.editor.binding.confirm') }}
                        </el-button>
                    </div>
                </div>
            </template>
        </el-dialog>

        <el-dialog v-model="layoutStyleDialogVisible" :title="t('stationLayout.editor.styles.title')" width="920px" class="layout-style-dialog"
            :close-on-click-modal="false">
            <el-form :disabled="props.readonly">
            <el-tabs>
                <el-tab-pane :label="t('stationLayout.editor.styles.text')">
                    <div class="layout-style-table">
                        <div class="layout-style-table-header">{{ t('stationLayout.editor.styles.object') }}</div>
                        <div class="layout-style-table-header">{{ t('stationLayout.editor.styles.size') }}</div>
                        <div class="layout-style-table-header">{{ t('stationLayout.editor.styles.font') }}</div>
                        <div class="layout-style-table-header">{{ t('stationLayout.editor.styles.weight') }}</div>
                        <div class="layout-style-table-header">{{ t('stationLayout.editor.styles.style') }}</div>
                        <div class="layout-style-table-header">{{ t('stationLayout.editor.styles.color') }}</div>
                        <template v-for="row in layoutTextStyleRows" :key="row.key">
                            <div class="layout-style-label">{{ row.label }}</div>
                            <el-input-number v-model="layoutDisplayStyles[row.key].fontSize" size="small" :min="6"
                                :max="48" :step="1" controls-position="right" />
                            <el-select v-model="layoutDisplayStyles[row.key].fontFamily" size="small">
                                <el-option v-for="fontFamily in annotationFontFamilyOptions" :key="fontFamily"
                                    :label="fontFamily" :value="fontFamily" />
                            </el-select>
                            <el-select v-model="layoutDisplayStyles[row.key].fontWeight" size="small">
                                <el-option v-for="item in annotationFontWeightOptions" :key="item.value"
                                    :label="item.label" :value="item.value" />
                            </el-select>
                            <el-select v-model="layoutDisplayStyles[row.key].fontStyle" size="small">
                                <el-option v-for="item in annotationFontStyleOptions" :key="item.value"
                                    :label="item.label" :value="item.value" />
                            </el-select>
                            <el-color-picker v-model="layoutDisplayStyles[row.key].color" size="small"
                                show-alpha />
                        </template>
                    </div>
                </el-tab-pane>
                <el-tab-pane :label="t('stationLayout.editor.styles.equipment')">
                    <div class="layout-style-grid">
                        <section class="layout-style-section">
                            <h4>{{ t('stationLayout.editor.styles.track') }}</h4>
                            <div class="layout-style-field">
                                <span>{{ t('stationLayout.editor.styles.weight') }}</span>
                                <el-input-number v-model="layoutDisplayStyles.track.strokeWidth" size="small" :min="0.5"
                                    :max="12" :step="0.5" controls-position="right" />
                            </div>
                            <div class="layout-style-field">
                                <span>{{ t('stationLayout.editor.styles.color') }}</span>
                                <el-color-picker v-model="layoutDisplayStyles.track.color" size="small" show-alpha />
                            </div>
                        </section>
                        <section class="layout-style-section">
                            <h4>{{ t('stationLayout.editor.styles.curve') }}</h4>
                            <div class="layout-style-field">
                                <span>{{ t('stationLayout.editor.styles.weight') }}</span>
                                <el-input-number v-model="layoutDisplayStyles.curve.strokeWidth" size="small" :min="0.5"
                                    :max="12" :step="0.5" controls-position="right" />
                            </div>
                            <div class="layout-style-field">
                                <span>{{ t('stationLayout.editor.styles.color') }}</span>
                                <el-color-picker v-model="layoutDisplayStyles.curve.color" size="small" show-alpha />
                            </div>
                        </section>
                        <section class="layout-style-section">
                            <h4>{{ t('stationLayout.editor.styles.platform') }}</h4>
                            <div class="layout-style-field">
                                <span>{{ t('stationLayout.editor.styles.weight') }}</span>
                                <el-input-number v-model="layoutDisplayStyles.platform.strokeWidth" size="small"
                                    :min="0.5" :max="12" :step="0.5" controls-position="right" />
                            </div>
                            <div class="layout-style-field">
                                <span>{{ t('stationLayout.editor.styles.color') }}</span>
                                <el-color-picker v-model="layoutDisplayStyles.platform.color" size="small" show-alpha />
                            </div>
                        </section>
                        <section class="layout-style-section">
                            <h4>{{ t('stationLayout.draw.signal') }}</h4>
                            <div class="layout-style-field">
                                <span>{{ t('stationLayout.editor.styles.size') }}</span>
                                <el-input-number v-model="layoutDisplayStyles.signal.scale" size="small" :min="0.2"
                                    :max="2" :step="0.05" controls-position="right" />
                            </div>
                        </section>
                        <section class="layout-style-section">
                            <h4>{{ t('stationLayout.draw.switch') }}</h4>
                            <div class="layout-style-field">
                                <span>{{ t('stationLayout.editor.styles.strokeWidth') }}</span>
                                <el-input-number v-model="layoutDisplayStyles.switch.strokeWidth" size="small" :min="1"
                                    :max="16" :step="0.5" controls-position="right" />
                            </div>
                            <div class="layout-style-field">
                                <span>{{ t('stationLayout.editor.styles.color') }}</span>
                                <el-color-picker v-model="layoutDisplayStyles.switch.color" size="small" show-alpha />
                            </div>
                        </section>
                        <section class="layout-style-section">
                            <h4>{{ t('stationLayout.draw.node') }}</h4>
                            <div class="layout-style-field">
                                <span>{{ t('stationLayout.editor.styles.size') }}</span>
                                <el-input-number v-model="layoutDisplayStyles.node.radius" size="small" :min="1"
                                    :max="24" :step="1" controls-position="right" />
                            </div>
                            <div class="layout-style-field">
                                <span>{{ t('stationLayout.editor.styles.color') }}</span>
                                <el-color-picker v-model="layoutDisplayStyles.node.color" size="small" show-alpha />
                            </div>
                        </section>
                    </div>
                </el-tab-pane>
            </el-tabs>
            </el-form>
            <template #footer>
                <el-button :disabled="props.readonly"
                    @click="ensureWritable() && resetLayoutDisplayStyles()">{{ t('stationLayout.editor.styles.reset') }}</el-button>
                <el-button type="primary" :loading="savingData" :disabled="props.readonly"
                    @click="saveLayoutDisplayStyles">{{ t('stationLayout.editor.common.save') }}</el-button>
                <el-button @click="layoutStyleDialogVisible = false">{{ t('stationLayout.schemeManager.close') }}</el-button>
            </template>
        </el-dialog>

        <div v-if="selectedAnnotation" class="annotation-editor-row">
            <span class="toolbar-group-label">{{ t('stationLayout.editor.annotation.title') }}</span>
            <el-input v-model="selectedAnnotation.text" size="small" class="annotation-text-input"
                :disabled="props.readonly"
                @input="updateSelectedAnnotation({ text: selectedAnnotation.text })" />
            <el-select v-model="selectedAnnotation.fontFamily" size="small" class="annotation-font-select"
                :disabled="props.readonly"
                @change="updateSelectedAnnotation({ fontFamily: selectedAnnotation.fontFamily })">
                <el-option v-for="fontFamily in annotationFontFamilyOptions" :key="fontFamily" :label="fontFamily"
                    :value="fontFamily" />
            </el-select>
            <el-input-number v-model="selectedAnnotation.fontSize" size="small" :min="8" :max="96" :step="1"
                controls-position="right" :disabled="props.readonly"
                @change="updateSelectedAnnotation({ fontSize: selectedAnnotation.fontSize })" />
            <el-select v-model="selectedAnnotation.fontWeight" size="small" class="annotation-small-select"
                :disabled="props.readonly"
                @change="updateSelectedAnnotation({ fontWeight: selectedAnnotation.fontWeight })">
                <el-option v-for="item in annotationFontWeightOptions" :key="item.value" :label="item.label"
                    :value="item.value" />
            </el-select>
            <el-select v-model="selectedAnnotation.fontStyle" size="small" class="annotation-small-select"
                :disabled="props.readonly"
                @change="updateSelectedAnnotation({ fontStyle: selectedAnnotation.fontStyle })">
                <el-option v-for="item in annotationFontStyleOptions" :key="item.value" :label="item.label"
                    :value="item.value" />
            </el-select>
            <span class="annotation-field-label">{{ t('stationLayout.editor.annotation.angle') }}</span>
            <el-input-number v-model="selectedAnnotation.angle" size="small" :min="-180" :max="180" :step="5"
                controls-position="right" :disabled="props.readonly"
                @change="updateSelectedAnnotation({ angle: selectedAnnotation.angle })" />
            <span class="annotation-field-label">X</span>
            <el-input-number v-model="selectedAnnotation.position.x" size="small" :step="10" controls-position="right"
                :disabled="props.readonly"
                @change="updateSelectedAnnotationPosition" />
            <span class="annotation-field-label">Y</span>
            <el-input-number v-model="selectedAnnotation.position.y" size="small" :step="10" controls-position="right"
                :disabled="props.readonly"
                @change="updateSelectedAnnotationPosition" />
        </div>
        <div class="station-layout-workspace">
            <div ref="stationLayoutEditorFrameRef" class="station-layout-editor-frame">
                <StationLayoutEditor :translate="t" ref="stationLayoutEditorRef" :display-scale-x="layoutScaleX"
                    :display-scale-y="layoutScaleY" :show-curve-arc="showCurveArc" :show-nodes="showNodes"
                    :show-grid="showGrid" :object-snap-distance="objectSnapDistance"
                    :readonly="props.readonly"
                    :auto-generate-topology="topologyGenerationMode === 'auto'"
                    :grid-spacing="gridSpacing"
                    :display-styles="layoutDisplayStyles"
                    :editor-state="stationLayoutEditorState"
                    :cell-link-membership-counts="cellLinkMembershipCounts"
                    :cells="layoutEditorCells"
                    :show-cell-names="showCellNames"
                    :route-pick-target="routeNodePickTarget"
                    :highlighted-route-link-ids="highlightedEditorLinkIds"
                    :highlighted-route-node-ids="highlightedRouteNodeIds"
                    @selected-annotation-change="handleSelectedAnnotationChange"
                    @selected-equipment-change="handleSelectedEquipmentChange"
                    @route-node-pick="handleRouteNodePick"
                    @cell-name-click="handleCellNameClick"
                    @cell-rename="handleCellRename"
                    @topology-rebuilt="handleTopologyRebuilt"
                    @cell-links-change="handleCellLinksChange"
                    @delete-selection-request="deleteSelection" />
            </div>
            <aside v-if="cellPanelVisible" class="cell-side-panel">
                <div class="cell-side-panel-header">
                    <div>
                        <div class="cell-side-panel-title">{{ t('stationLayout.editor.cells.title') }}</div>
                        <div class="cell-side-panel-subtitle">{{ currentStationSchemeId || t('stationLayout.editor.common.currentScheme') }}</div>
                    </div>
                    <el-button text size="small" @click="toggleCellPanel">{{ t('stationLayout.schemeManager.close') }}</el-button>
                </div>
                <div class="cell-side-panel-body">
                    <section class="cell-panel-section">
                        <div class="cell-panel-section-header">
                            <span>{{ t('stationLayout.editor.cells.list') }}</span>
                            <div class="cell-panel-actions">
                                <el-button size="small" :icon="Magnet" :disabled="props.readonly"
                                    @click="autoGenerateCells">{{ t('stationLayout.editor.cells.autoGenerate') }}</el-button>
                                <el-button size="small" type="primary" :disabled="props.readonly"
                                    @click="createCell">{{ t('stationLayout.editor.common.add') }}</el-button>
                                <el-button size="small" type="danger" :disabled="props.readonly || !selectedCellId"
                                    @click="deleteSelectedCell">
                                    {{ t('stationLayout.editor.common.delete') }}
                                </el-button>
                            </div>
                        </div>
                        <el-table :data="cells" class="cell-table" size="small" height="190" row-key="id"
                            highlight-current-row :current-row-key="selectedCellId" @row-click="selectCellRow">
                            <el-table-column label="ID" width="112">
                                <template #default="{ row }">
                                    {{ getCellDisplayId(row) }}
                                </template>
                            </el-table-column>
                            <el-table-column prop="name" :label="t('stationLayout.editor.cells.name')" min-width="120" show-overflow-tooltip />
                            <el-table-column :label="t('stationLayout.editor.cells.links')" width="72">
                                <template #default="{ row }">
                                    {{ getCellLinkCount(row) }}
                                </template>
                            </el-table-column>
                        </el-table>
                    </section>

                    <section class="cell-panel-section cell-detail-section">
                        <div class="cell-panel-section-header">
                            <span>{{ t('stationLayout.editor.cells.info') }}</span>
                            <el-button size="small" type="primary" :disabled="props.readonly || !selectedCellId"
                                :loading="savingData"
                                @click="saveCellForm">
                                {{ t('stationLayout.editor.common.save') }}
                            </el-button>
                        </div>
                        <div v-if="selectedCellId" class="cell-detail-content">
                            <el-form class="cell-form" label-width="116px" size="small" :disabled="props.readonly">
                                <el-form-item :label="t('stationLayout.editor.cells.instance')">
                                    <el-input v-model="cellForm.instanceID" disabled />
                                </el-form-item>
                                <el-form-item :label="t('stationLayout.editor.cells.scheme')">
                                    <el-input v-model="cellForm.stationSchemeID" disabled />
                                </el-form-item>
                                <el-form-item label="ID">
                                    <el-input :model-value="getCellDisplayId(cellForm)" disabled :placeholder="t('stationLayout.editor.cells.idPlaceholder')" />
                                </el-form-item>
                                <el-form-item :label="t('stationLayout.editor.cells.name')">
                                    <el-input v-model="cellForm.name" />
                                </el-form-item>
                                <el-form-item :label="t('stationLayout.editor.cells.links')">
                                    <el-input v-model="cellForm.linkIDList" type="textarea" :rows="2"
                                        @change="setCellFormLinkIds(parseLinkIdList(cellForm.linkIDList))" />
                                </el-form-item>
                            </el-form>

                            <div class="cell-link-toolbar">
                                <span class="cell-link-toolbar-title">{{ t('stationLayout.editor.cells.links') }}</span>
                                <div class="cell-link-toolbar-actions">
                                    <el-button size="small" :type="cellLinkPickMode ? 'primary' : 'default'"
                                        :disabled="props.readonly"
                                        @click="toggleCellLinkPickMode">
                                        {{ t('stationLayout.editor.cells.pickAdd') }}
                                    </el-button>
                                    <el-button size="small" :disabled="props.readonly || !selectedCellLinkId"
                                        @click="removeSelectedCellLink">
                                        {{ t('stationLayout.editor.cells.removeCurrent') }}
                                    </el-button>
                                    <el-button size="small" :disabled="props.readonly || cellFormLinkIds.length === 0"
                                        @click="clearCellLinks">
                                        {{ t('stationLayout.editor.common.clear') }}
                                    </el-button>
                                </div>
                            </div>

                            <el-tabs v-if="cellFormLinkIds.length > 0" v-model="selectedCellLinkId"
                                class="cell-link-tabs" type="card" @tab-click="handleCellLinkTabClick">
                                <el-tab-pane v-for="linkId in cellFormLinkIds" :key="linkId" :name="linkId"
                                    :label="getLinkLabel(linkId)">
                                    <div class="cell-link-detail">
                                        <div>
                                            <span class="cell-link-detail-label">ID</span>
                                            <span class="cell-link-detail-value">{{ linkId }}</span>
                                        </div>
                                        <div>
                                            <span class="cell-link-detail-label">{{ t('stationLayout.editor.cells.endpoints') }}</span>
                                            <span class="cell-link-detail-value">{{ getLinkEndpointSummary(linkId) }}</span>
                                        </div>
                                    </div>
                                </el-tab-pane>
                            </el-tabs>
                            <el-empty v-else class="cell-link-empty" :description="t('stationLayout.editor.cells.emptyLinks')" />
                        </div>
                        <el-empty v-else class="cell-empty" :description="t('stationLayout.editor.cells.empty')" />
                    </section>
                </div>
            </aside>
            <aside v-if="equipmentDrawerVisible" class="equipment-side-panel">
                <div class="equipment-side-panel-header">
                    <div>
                        <div class="equipment-side-panel-title">{{ equipmentDrawerTitle }}</div>
                        <div class="equipment-side-panel-subtitle">{{ t('stationLayout.panels.equipment') }}</div>
                    </div>
                    <el-button text size="small" @click="equipmentDrawerVisible = false">{{ t('stationLayout.schemeManager.close') }}</el-button>
                </div>
                <div class="equipment-side-panel-body">
                    <el-form v-if="selectedEquipment" label-position="top" class="equipment-form"
                        :disabled="props.readonly">
                        <el-form-item :label="t('stationLayout.equipment.type')">
                            <el-tag type="info">{{ equipmentKindLabels[equipmentForm.kind] || t('stationLayout.equipment.generic') }}</el-tag>
                        </el-form-item>
                        <el-form-item :label="t('stationLayout.equipment.fields.id')">
                            <el-input v-model="equipmentForm.id" readonly />
                        </el-form-item>
                        <el-form-item v-if="['link', 'signal', 'switch', 'platform'].includes(equipmentForm.kind)"
                            :label="t('stationLayout.equipment.fields.name')">
                            <el-input v-model="equipmentForm.name" />
                        </el-form-item>
                        <el-form-item v-if="equipmentForm.kind === 'link'" :label="t('stationLayout.equipment.fields.arrowDirection')">
                            <el-select v-model="equipmentForm.arrowDirection">
                                <el-option v-for="option in linkArrowDirectionOptions" :key="option.value"
                                    :label="option.label" :value="option.value" />
                            </el-select>
                        </el-form-item>
                        <el-form-item v-if="equipmentForm.kind === 'link'" :label="t('stationLayout.equipment.fields.arrowType')">
                            <el-select v-model="equipmentForm.arrowType">
                                <el-option v-for="option in linkArrowTypeOptions" :key="option.value"
                                    :label="option.label" :value="option.value" />
                            </el-select>
                        </el-form-item>
                        <el-form-item v-if="equipmentForm.kind === 'switch'" :label="t('stationLayout.equipment.fields.type')">
                            <el-select v-model="equipmentForm.type">
                                <el-option v-for="option in switchTypeOptions" :key="option.value"
                                    :label="option.label" :value="option.value" />
                            </el-select>
                        </el-form-item>
                        <el-form-item v-if="equipmentForm.kind === 'signal'" :label="t('stationLayout.equipment.fields.type')">
                            <el-select v-model="equipmentForm.type">
                                <el-option v-for="option in equipmentSignalTypeOptions" :key="option.value"
                                    :label="option.label" :value="option.value" />
                            </el-select>
                        </el-form-item>
                        <el-form-item v-if="equipmentForm.kind === 'insulationJoint'" :label="t('stationLayout.equipment.fields.type')">
                            <el-select v-model="equipmentForm.type">
                                <el-option v-for="option in insulationJointTypeOptions" :key="option.value"
                                    :label="option.label" :value="option.value" />
                            </el-select>
                        </el-form-item>
                        <el-form-item v-if="equipmentForm.kind === 'bufferStop'" :label="t('stationLayout.equipment.fields.type')">
                            <el-select v-model="equipmentForm.type">
                                <el-option v-for="option in bufferStopTypeOptions" :key="option.value"
                                    :label="option.label" :value="option.value" />
                            </el-select>
                        </el-form-item>
                        <el-form-item v-if="equipmentForm.kind === 'signal'" :label="t('stationLayout.equipment.fields.direction')">
                            <el-select v-model="equipmentForm.direction">
                                <el-option v-for="option in signalDirectionOptions" :key="option.value"
                                    :label="option.label" :value="option.value" />
                            </el-select>
                        </el-form-item>
                        <el-form-item v-if="equipmentForm.kind === 'bufferStop'" :label="t('stationLayout.equipment.fields.direction')">
                            <el-select v-model="equipmentForm.direction">
                                <el-option v-for="option in bufferStopDirectionOptions" :key="option.value"
                                    :label="option.label" :value="option.value" />
                            </el-select>
                        </el-form-item>
                        <el-form-item v-if="['signal', 'switch', 'insulationJoint', 'bufferStop'].includes(equipmentForm.kind)"
                            :label="t('stationLayout.equipment.fields.bindingNodeID')">
                            <el-select v-if="isEquipmentNodePositionLocked" v-model="equipmentForm.bindingNodeID"
                                filterable @change="syncEquipmentFormPositionToBindingNode(equipmentForm)"
                                @visible-change="handleEquipmentBindingDropdownVisible">
                                <el-option v-for="option in equipmentBindingNodeOptions" :key="option.value"
                                    :label="option.label" :value="option.value" />
                            </el-select>
                            <el-input v-else v-model="equipmentForm.bindingNodeID" />
                        </el-form-item>
                        <div v-if="['signal', 'switch', 'insulationJoint', 'bufferStop'].includes(equipmentForm.kind)"
                            class="equipment-form-grid">
                            <el-form-item :label="t('stationLayout.equipment.fields.x')">
                                <el-input-number v-model="equipmentForm.x" controls-position="right" :step="10"
                                    :disabled="isEquipmentNodePositionLocked" :controls="!isEquipmentNodePositionLocked" />
                            </el-form-item>
                            <el-form-item :label="t('stationLayout.equipment.fields.y')">
                                <el-input-number v-model="equipmentForm.y" controls-position="right" :step="10"
                                    :disabled="isEquipmentNodePositionLocked" :controls="!isEquipmentNodePositionLocked" />
                            </el-form-item>
                        </div>
                        <div v-if="equipmentForm.kind === 'platform'" class="equipment-form-grid">
                            <el-form-item :label="t('stationLayout.equipment.fields.x')">
                                <el-input-number v-model="equipmentForm.x" controls-position="right" :step="10" />
                            </el-form-item>
                            <el-form-item :label="t('stationLayout.equipment.fields.y')">
                                <el-input-number v-model="equipmentForm.y" controls-position="right" :step="10" />
                            </el-form-item>
                            <el-form-item :label="t('stationLayout.equipment.fields.width')">
                                <el-input-number v-model="equipmentForm.width" controls-position="right" :min="0"
                                    :step="10" />
                            </el-form-item>
                            <el-form-item :label="t('stationLayout.equipment.fields.height')">
                                <el-input-number v-model="equipmentForm.height" controls-position="right" :min="0"
                                    :step="10" />
                            </el-form-item>
                        </div>
                        <div v-if="equipmentForm.kind === 'link'" class="equipment-form-grid">
                            <el-form-item :label="t('stationLayout.equipment.fields.x1')">
                                <el-input-number v-model="equipmentForm.x1" controls-position="right" :step="10" />
                            </el-form-item>
                            <el-form-item :label="t('stationLayout.equipment.fields.y1')">
                                <el-input-number v-model="equipmentForm.y1" controls-position="right" :step="10" />
                            </el-form-item>
                            <el-form-item :label="t('stationLayout.equipment.fields.x2')">
                                <el-input-number v-model="equipmentForm.x2" controls-position="right" :step="10" />
                            </el-form-item>
                            <el-form-item :label="t('stationLayout.equipment.fields.y2')">
                                <el-input-number v-model="equipmentForm.y2" controls-position="right" :step="10" />
                            </el-form-item>
                        </div>
                        <el-form-item v-if="equipmentForm.kind === 'link'" :label="t('stationLayout.equipment.fields.fromNodeID')">
                            <el-input v-model="equipmentForm.fromNodeID" />
                        </el-form-item>
                        <el-form-item v-if="equipmentForm.kind === 'link'" :label="t('stationLayout.equipment.fields.toNodeID')">
                            <el-input v-model="equipmentForm.toNodeID" />
                        </el-form-item>
                        <el-collapse v-if="equipmentForm.kind === 'switch'"
                            :key="selectedEquipment.ids?.join(',') || selectedEquipment.id">
                            <el-collapse-item :title="t('stationLayout.equipment.fields.branchVectorList')" name="branchVectorList">
                                <el-input v-model="equipmentForm.branchVectorListText" type="textarea" :rows="8"
                                    :aria-label="t('stationLayout.equipment.fields.branchVectorList')" />
                            </el-collapse-item>
                        </el-collapse>
                    </el-form>
                    <el-empty v-else class="equipment-empty" :description="t('stationLayout.equipment.selectOne')" />
                </div>
                <div class="equipment-side-panel-footer">
                    <el-button @click="equipmentDrawerVisible = false">{{ t('stationLayout.schemeManager.close') }}</el-button>
                    <el-button type="primary" :disabled="props.readonly || !selectedEquipment"
                        :loading="equipmentSaving || savingData"
                        @click="saveEquipmentForm">
                        {{ t('stationLayout.schemeManager.save') }}
                    </el-button>
                </div>
            </aside>
            <aside v-if="routeTesterVisible" class="route-search-panel">
                <div class="route-search-panel-header">
                    <div>
                        <div class="route-search-panel-title">{{ t('stationLayout.editor.routes.title') }}</div>
                        <div class="route-search-panel-subtitle">{{ currentStationSchemeId || t('stationLayout.editor.common.currentScheme') }}</div>
                    </div>
                    <el-button text size="small" @click="toggleRouteTester">{{ t('stationLayout.schemeManager.close') }}</el-button>
                </div>
                <div class="route-search-panel-body">
                    <div class="route-search-form">
                        <label class="route-search-label">{{ t('stationLayout.editor.routes.start') }}</label>
                        <div class="route-search-input-row">
                            <el-input v-model="routeSearchForm.startNodeId" size="small" clearable />
                            <el-button size="small" :type="routeNodePickTarget === 'start' ? 'primary' : 'default'"
                                @click="setRouteNodePickTarget('start')">
                                {{ t('stationLayout.editor.routes.pick') }}
                            </el-button>
                        </div>
                        <label class="route-search-label">{{ t('stationLayout.editor.routes.end') }}</label>
                        <div class="route-search-input-row">
                            <el-input v-model="routeSearchForm.endNodeId" size="small" clearable />
                            <el-button size="small" :type="routeNodePickTarget === 'end' ? 'primary' : 'default'"
                                @click="setRouteNodePickTarget('end')">
                                {{ t('stationLayout.editor.routes.pick') }}
                            </el-button>
                        </div>
                        <div class="route-search-actions">
                            <el-button type="primary" size="small" :loading="routeSearchLoading"
                                @click="searchStationRoutes">
                                {{ t('stationLayout.editor.routes.search') }}
                            </el-button>
                            <el-button size="small" @click="clearRouteSearchResult">{{ t('stationLayout.editor.common.clear') }}</el-button>
                        </div>
                    </div>
                    <el-table :data="routeSearchRoutes" v-loading="routeSearchLoading" size="small"
                        class="route-search-table" height="100%" highlight-current-row
                        @row-click="selectStationRoute">
                        <el-table-column label="#" width="48">
                            <template #default="{ row }">
                                {{ row.index + 1 }}
                            </template>
                        </el-table-column>
                        <el-table-column :label="t('stationLayout.editor.routes.direction')" width="72">
                            <template #default="{ row }">
                                {{ getRouteDirectionLabel(row.direction) }}
                            </template>
                        </el-table-column>
                        <el-table-column :label="t('stationLayout.editor.routes.path')">
                            <template #default="{ row }">
                                <div class="route-search-summary" :class="{ 'is-active': row.index === selectedRouteIndex }">
                                    {{ getRouteSummary(row) }}
                                </div>
                            </template>
                        </el-table-column>
                    </el-table>
                </div>
            </aside>
        </div>
        <el-dialog v-model="extractDwgDialogVisible" :title="t('stationLayout.editor.dwg.title')" width="420px" :close-on-click-modal="false">
            <div class="dwg-extract-form">
                <label class="dwg-extract-label">{{ t('stationLayout.editor.dwg.file') }}</label>
                <input ref="dwgFileInputRef" type="file" accept=".dwg" :disabled="props.readonly"
                    @change="handleDwgFileChange" />
                <label class="dwg-extract-label">{{ t('stationLayout.editor.dwg.layer') }}</label>
                <el-input v-model="dwgLayerName" :disabled="props.readonly" :placeholder="t('stationLayout.editor.dwg.layerPlaceholder')" />
            </div>
            <template #footer>
                <el-button @click="extractDwgDialogVisible = false">{{ t('stationLayout.editor.common.cancel') }}</el-button>
                <el-button type="primary" :loading="extractingDwg" :disabled="props.readonly"
                    @click="extractDwgFile">
                    {{ t('stationLayout.editor.dwg.upload') }}
                </el-button>
            </template>
        </el-dialog>
        <div id="equipmentinfolist"></div>
    </div>
</template>

<style scoped>
.station-layout-page {
    position: relative;
    display: flex;
    flex-direction: column;
    width: 100%;
    max-width: 100%;
    height: 100%;
    min-width: 0;
    min-height: 0;
    overflow: hidden;
}

.station-toolbar {
    row-gap: 4px;
}

.station-scheme-toolbar-group {
    flex: 1 1 360px;
    max-width: 100%;
}

.station-scheme-control-row {
    display: inline-flex;
    align-items: center;
    flex: 1 1 300px;
    flex-wrap: nowrap;
    gap: 6px;
    min-width: 0;
    max-width: 100%;
}

.station-scheme-select {
    flex: 1 1 220px;
    min-width: 0;
    max-width: 240px;
}

.station-scheme-control-row :deep(.el-button) {
    flex: 0 0 auto;
}

.station-scheme-manager {
    display: flex;
    flex-direction: column;
    gap: 12px;
}

.station-scheme-create-row {
    display: flex;
    align-items: center;
    gap: 8px;
}

.station-scheme-table {
    width: 100%;
}

.station-scheme-actions {
    display: flex;
    align-items: center;
    gap: 6px;
}

.binding-correction-dialog {
    display: flex;
    flex-direction: column;
    gap: 10px;
}

.binding-correction-summary {
    font-size: 13px;
    line-height: 1.5;
    color: #606266;
}

.binding-correction-footer {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 12px;
    width: 100%;
}

.binding-correction-actions {
    display: flex;
    align-items: center;
    gap: 8px;
}

.layout-style-table {
    display: grid;
    grid-template-columns: 100px 110px minmax(150px, 1fr) 110px 110px 72px;
    gap: 10px 12px;
    align-items: center;
}

.layout-style-table-header {
    font-size: 12px;
    font-weight: 600;
    color: #606266;
}

.layout-style-label {
    font-size: 13px;
    color: #303133;
    white-space: nowrap;
}

.layout-style-table :deep(.el-input-number) {
    width: 100%;
}

.layout-style-grid {
    display: grid;
    grid-template-columns: repeat(2, minmax(0, 1fr));
    gap: 12px;
}

.layout-style-section {
    padding: 12px;
    border: 1px solid var(--el-border-color-lighter);
    border-radius: 6px;
    background-color: #fff;
}

.layout-style-section h4 {
    margin: 0 0 10px;
    font-size: 14px;
    font-weight: 600;
    color: #303133;
}

.layout-style-field {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 12px;
    min-height: 32px;
    font-size: 13px;
    color: #606266;
}

.layout-style-field + .layout-style-field {
    margin-top: 8px;
}

.layout-style-field :deep(.el-input-number) {
    width: 140px;
}

.toolbar-checkbox-item:hover {
    background-color: transparent !important;
}

.toolbar-checkbox-item {
    cursor: default;
}

.toolbar-field-item {
    display: flex;
    align-items: center;
    flex-wrap: nowrap;
    gap: 6px;
    cursor: default;
    white-space: nowrap;
}

.toolbar-field-item:hover {
    background-color: transparent !important;
}

.toolbar-field-item :deep(.el-input-number) {
    width: 92px;
}

.toolbar-row {
    flex: 0 0 auto;
    display: flex;
    align-items: center;
    flex-wrap: wrap;
    gap: 8px;
    max-width: 100%;
    padding: 6px 12px;
    overflow-x: hidden;
    background-color: #fafafa;
    border-bottom: 1px solid var(--el-border-color-light);
}

.toolbar-group {
    display: flex;
    align-items: center;
    flex-wrap: wrap;
    gap: 6px;
    min-width: 0;
}

.toolbar-group :deep(.el-button-group) {
    display: inline-flex;
    flex-wrap: wrap;
    max-width: 100%;
    row-gap: 4px;
}

.toolbar-group :deep(.el-button-group .el-dropdown) {
    display: inline-flex;
}

.toolbar-group :deep(.el-button-group .el-dropdown .el-button) {
    border-radius: 0;
}

.drawing-object-button-label {
    display: inline-block;
    max-width: 160px;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    vertical-align: bottom;
}

:global(.drawing-object-dropdown-menu) {
    max-height: 360px;
    overflow-y: auto;
}

:global(.drawing-option-group-label) {
    color: var(--el-text-color-secondary);
    cursor: default;
    font-size: 12px;
    font-weight: 600;
}

:global(.drawing-option-group-label.is-disabled) {
    opacity: 1;
}

.mode-toggle :deep(.el-radio-button__inner) {
    display: inline-flex;
    align-items: center;
    gap: 4px;
}

.toolbar-group-label {
    display: inline-flex;
    align-items: center;
    min-height: 24px;
    font-size: 12px;
    line-height: 1;
    color: #909399;
    white-space: nowrap;
    font-weight: 500;
}

.scale-toolbar-group {
    flex-wrap: wrap;
}

.curve-display-toolbar-group {
    min-height: 24px;
}

.node-display-toolbar-group,
.cell-name-display-toolbar-group,
.equipment-panel-toolbar-group {
    min-height: 24px;
}

.curve-display-switch {
    --el-switch-on-color: #409eff;
    --el-switch-off-color: #909399;
}

.node-display-switch,
.cell-name-display-switch,
.equipment-panel-switch {
    --el-switch-on-color: #409eff;
    --el-switch-off-color: #909399;
}

.curve-display-switch :deep(.el-switch__core) {
    min-width: 56px;
}

.node-display-switch :deep(.el-switch__core),
.cell-name-display-switch :deep(.el-switch__core) {
    min-width: 52px;
}

.scale-slider {
    display: flex;
    align-items: center;
    gap: 6px;
    width: 190px;
}

.scale-slider-label {
    display: inline-flex;
    align-items: center;
    width: 12px;
    min-height: 24px;
    font-size: 12px;
    line-height: 1;
    color: #606266;
}

.scale-slider-value {
    width: 34px;
    text-align: right;
    font-size: 12px;
    color: #606266;
    font-variant-numeric: tabular-nums;
}

.scale-slider :deep(.el-slider) {
    flex: 1;
    min-width: 100px;
}

.annotation-editor-row {
    /* Keep the canvas origin fixed between the two clicks that start renaming. */
    order: 2;
    flex: 0 0 auto;
    display: flex;
    align-items: center;
    flex-wrap: wrap;
    gap: 8px;
    padding: 6px 12px;
    background-color: #f5f7fa;
    border-bottom: 1px solid var(--el-border-color-light);
}

.annotation-text-input {
    width: 220px;
}

.annotation-font-select {
    width: 150px;
}

.annotation-small-select {
    width: 96px;
}

.annotation-field-label {
    display: inline-flex;
    align-items: center;
    min-height: 24px;
    font-size: 12px;
    line-height: 1;
    color: #606266;
    white-space: nowrap;
}

.annotation-editor-row :deep(.el-input-number) {
    width: 96px;
}

.station-layout-workspace {
    order: 1;
    display: flex;
    flex: 1 1 auto;
    align-items: stretch;
    width: 100%;
    min-width: 0;
    min-height: 0;
    overflow: hidden;
    background-color: #31363f;
}

.station-layout-editor-frame {
    flex: 1 1 auto;
    min-width: 0;
    min-height: 0;
    height: 100%;
    overflow: auto;
    background-color: #31363f;
}

.equipment-side-panel {
    flex: 0 0 360px;
    width: 360px;
    height: 100%;
    min-height: 0;
    display: flex;
    flex-direction: column;
    background-color: #fff;
    border-left: 1px solid var(--el-border-color-light);
    box-shadow: -4px 0 12px rgba(0, 0, 0, 0.08);
}

.equipment-side-panel-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 12px;
    padding: 14px 16px 12px;
    border-bottom: 1px solid var(--el-border-color-lighter);
}

.equipment-side-panel-title {
    font-size: 16px;
    font-weight: 600;
    color: #303133;
    line-height: 1.3;
}

.equipment-side-panel-subtitle {
    margin-top: 2px;
    font-size: 12px;
    color: #909399;
}

.equipment-side-panel-body {
    flex: 1 1 auto;
    overflow: auto;
    padding: 12px 16px 4px;
}

.equipment-side-panel-footer {
    display: flex;
    justify-content: flex-end;
    gap: 8px;
    padding: 12px 16px;
    border-top: 1px solid var(--el-border-color-lighter);
    background-color: #fff;
}

.equipment-form {
    padding-bottom: 8px;
}

.equipment-form :deep(.el-form-item) {
    margin-bottom: 10px;
}

.equipment-form :deep(.el-form-item__label) {
    display: inline-flex;
    align-items: center;
    min-height: 18px;
    padding: 0;
    margin-bottom: 4px;
    line-height: 18px;
}

.equipment-form :deep(.el-form-item__content) {
    min-width: 0;
    line-height: normal;
}

.equipment-form :deep(.el-input),
.equipment-form :deep(.el-select),
.equipment-form :deep(.el-input-number) {
    width: 100%;
}

.equipment-form-grid {
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: 0 10px;
}

.equipment-form-grid :deep(.el-form-item) {
    min-width: 0;
}

.equipment-form-grid :deep(.el-input-number) {
    width: 100%;
}

.cell-side-panel {
    flex: 0 0 460px;
    width: 460px;
    height: 100%;
    min-height: 0;
    display: flex;
    flex-direction: column;
    background-color: #fff;
    border-left: 1px solid var(--el-border-color-light);
    box-shadow: -4px 0 12px rgba(0, 0, 0, 0.08);
}

.cell-side-panel-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 12px;
    padding: 14px 16px 12px;
    border-bottom: 1px solid var(--el-border-color-lighter);
}

.cell-side-panel-title {
    font-size: 16px;
    font-weight: 600;
    color: #303133;
    line-height: 1.3;
}

.cell-side-panel-subtitle {
    margin-top: 2px;
    font-size: 12px;
    color: #909399;
}

.cell-side-panel-body {
    flex: 1 1 auto;
    min-height: 0;
    display: flex;
    flex-direction: column;
    gap: 12px;
    overflow: auto;
    padding: 12px 16px;
}

.cell-panel-section {
    display: flex;
    min-height: 0;
    flex-direction: column;
    gap: 10px;
}

.cell-detail-section {
    flex: 1 1 auto;
}

.cell-panel-section-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 10px;
    min-height: 28px;
    font-size: 13px;
    font-weight: 600;
    color: #303133;
}

.cell-panel-actions,
.cell-link-toolbar-actions {
    display: flex;
    align-items: center;
    gap: 6px;
    flex-wrap: wrap;
    justify-content: flex-end;
}

.cell-table {
    width: 100%;
}

.cell-detail-content {
    display: flex;
    flex-direction: column;
    min-height: 0;
    gap: 10px;
}

.cell-form {
    padding-right: 4px;
}

.cell-form :deep(.el-form-item) {
    align-items: center;
    margin-bottom: 8px;
}

.cell-form :deep(.el-form-item__label) {
    display: inline-flex;
    align-items: center;
    justify-content: flex-end;
    min-height: 24px;
    padding-right: 8px;
    line-height: 16px;
}

.cell-form :deep(.el-form-item__content) {
    min-width: 0;
    line-height: normal;
}

.cell-link-toolbar {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 10px;
}

.cell-link-toolbar-title {
    font-size: 13px;
    font-weight: 600;
    color: #303133;
    white-space: nowrap;
}

.cell-link-tabs {
    min-height: 130px;
}

.cell-link-tabs :deep(.el-tabs__header) {
    margin-bottom: 8px;
}

.cell-link-tabs :deep(.el-tabs__item) {
    max-width: 160px;
    overflow: hidden;
    text-overflow: ellipsis;
}

.cell-link-detail {
    display: grid;
    gap: 8px;
    padding: 6px 2px 2px;
    color: #303133;
    font-size: 12px;
}

.cell-link-detail-label {
    display: inline-block;
    width: 44px;
    color: #909399;
    font-weight: 500;
}

.cell-link-detail-value {
    font-family: Consolas, "Microsoft YaHei", monospace;
}

.cell-empty,
.cell-link-empty {
    flex: 1 1 auto;
    min-height: 120px;
}

.route-search-panel {
    flex: 0 0 380px;
    width: 380px;
    height: 100%;
    min-height: 0;
    display: flex;
    flex-direction: column;
    background-color: #fff;
    border-left: 1px solid var(--el-border-color-light);
    box-shadow: -4px 0 12px rgba(0, 0, 0, 0.08);
}

.route-search-panel-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 12px;
    padding: 14px 16px 12px;
    border-bottom: 1px solid var(--el-border-color-lighter);
}

.route-search-panel-title {
    font-size: 16px;
    font-weight: 600;
    color: #303133;
    line-height: 1.3;
}

.route-search-panel-subtitle {
    margin-top: 2px;
    font-size: 12px;
    color: #909399;
}

.route-search-panel-body {
    flex: 1 1 auto;
    min-height: 0;
    display: flex;
    flex-direction: column;
    gap: 12px;
    padding: 12px 16px;
}

.route-search-form {
    display: grid;
    gap: 6px;
}

.route-search-label {
    display: inline-flex;
    align-items: center;
    font-size: 12px;
    line-height: 16px;
    font-weight: 500;
    color: #606266;
}

.route-search-input-row {
    display: grid;
    grid-template-columns: minmax(0, 1fr) 64px;
    gap: 8px;
    align-items: center;
}

.route-search-actions {
    display: flex;
    justify-content: flex-end;
    gap: 8px;
    padding-top: 4px;
}

.route-search-table {
    flex: 1 1 auto;
    min-height: 160px;
}

.route-search-summary {
    color: #303133;
    font-family: Consolas, "Microsoft YaHei", monospace;
    font-size: 12px;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
}

.route-search-summary.is-active {
    color: #0891b2;
    font-weight: 600;
}

@media (max-width: 960px) {
    .equipment-side-panel {
        flex-basis: 320px;
        width: 320px;
    }

    .cell-side-panel {
        flex-basis: 340px;
        width: 340px;
    }

    .route-search-panel {
        flex-basis: 320px;
        width: 320px;
    }
}

.dwg-extract-form {
    display: grid;
    gap: 6px;
}

.dwg-extract-label {
    display: inline-flex;
    align-items: center;
    font-size: 13px;
    line-height: 18px;
    color: #606266;
    font-weight: 500;
}

.hidden-file-input {
    display: none;
}
</style>
