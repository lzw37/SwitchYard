<script setup>
import { computed, onMounted, ref, watch } from "vue";
import { useI18n } from 'vue-i18n';
import axios from "@/utils/axios";
import { ElMessage, ElMessageBox } from "element-plus";
import {
    DEFAULT_BUFFER_STOP_DIRECTION,
    DEFAULT_BUFFER_STOP_TYPE,
    bufferStopDirectionOptions,
    bufferStopTypeOptions,
} from "@/assets/stationLayoutBufferStopStyles";
import { DEFAULT_SIGNAL_TYPE, signalTypeMenuOptions } from "@/assets/stationLayoutSignalStyles";
import SignalInfoTabVue from "./components/SignalInfoTab.vue";
import StationLayoutEditor from "./components/StationLayoutEditor.vue";
import {
    Download, Upload, Aim, Hide, Connection, Scissor,
    Share, SetUp,
    Pointer, EditPen,
    Minus, Location, Bell, Switch, Filter, Guide, Stopwatch, Platform,
    Grid, Magnet,
    RefreshLeft, RefreshRight,
    CircleClose, Delete, ArrowDown
} from '@element-plus/icons-vue';

const { t } = useI18n();
const props = defineProps({
    selectedInstanceId: {
        type: String,
        default: "",
    },
});
const stationLayoutEditorRef = ref(null);
const signalDropdownRef = ref(null);
const extractDwgDialogVisible = ref(false);
const dwgFileInputRef = ref(null);
const importJsonFileInputRef = ref(null);
const selectedDwgFile = ref(null);
const dwgLayerName = ref("0");
const extractingDwg = ref(false);
const loadingData = ref(false);
const savingData = ref(false);
const currentStationSchemeId = ref("");
const loadingStationSchemes = ref(false);
const stationSchemeOptions = ref([]);
const stationSchemeManagerVisible = ref(false);
const stationSchemeManagerSaving = ref(false);
const stationSchemeDraft = ref({ name: "" });
const editingStationSchemeOriginalId = ref("");
const editingStationSchemeForm = ref({ name: "" });
const layoutScaleX = ref(1);
const layoutScaleY = ref(1);
const showCurveArc = ref(true);
const showNodes = ref(true);
const showGrid = ref(true);
const gridSpacing = ref(20);
const layoutStyleDialogVisible = ref(false);
const layoutScaleXDisplay = computed(() => layoutScaleX.value.toFixed(2));
const layoutScaleYDisplay = computed(() => layoutScaleY.value.toFixed(2));
const selectedAnnotation = ref(null);
const selectedEquipment = ref(null);
const equipmentDrawerVisible = ref(false);
const equipmentForm = ref({});
const equipmentSaving = ref(false);
const activeEditMode = ref(0);
const isSelectMode = computed(() => activeEditMode.value === 0);
const routeTesterVisible = ref(false);
const routeSearchLoading = ref(false);
const routeNodePickTarget = ref("");
const routeSearchForm = ref({
    startNodeId: "",
    endNodeId: "",
});
const routeSearchRoutes = ref([]);
const selectedRouteIndex = ref(-1);
const selectedRoute = computed(() => {
    if (selectedRouteIndex.value < 0) return null;
    return routeSearchRoutes.value[selectedRouteIndex.value] || null;
});
const highlightedRouteLinkIds = computed(() => selectedRoute.value?.linkIds || []);
const highlightedRouteNodeIds = computed(() => selectedRoute.value?.nodeIds || []);
const selectedDrawingBufferStopDirection = ref(DEFAULT_BUFFER_STOP_DIRECTION);
const selectedDrawingBufferStopType = ref(DEFAULT_BUFFER_STOP_TYPE);
const selectedDrawingSignalType = ref(DEFAULT_SIGNAL_TYPE);
const signalTypeCascaderProps = { emitPath: false };

const annotationFontFamilyOptions = ["Arial", "Microsoft YaHei", "SimSun", "SimHei", "Times New Roman", "Consolas"];
const annotationFontWeightOptions = [
    { label: "常规", value: "normal" },
    { label: "加粗", value: "bold" },
];
const annotationFontStyleOptions = [
    { label: "常规", value: "normal" },
    { label: "斜体", value: "italic" },
];
const layoutTextStyleRows = [
    { key: "switchName", label: "道岔编号" },
    { key: "platformName", label: "站台名称" },
    { key: "signalName", label: "信号机名称" },
    { key: "lineName", label: "线路名称" },
];
const defaultLayoutDisplayStyles = {
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
const layoutDisplayStyles = ref(createDefaultLayoutDisplayStyles());
const linkArrowDirectionOptions = [
    { label: "不绘制", value: "" },
    { label: "左侧", value: "L" },
    { label: "右侧", value: "R" },
    { label: "左右两侧", value: "LR" },
];
const linkArrowTypeOptions = [
    { label: "不绘制", value: "" },
    { label: "旅客列车进路", value: "P" },
    { label: "货物列车进路", value: "F" },
    { label: "客货列车进路", value: "PF" },
    { label: "机车出段", value: "LO" },
    { label: "机车入段", value: "LI" },
    { label: "机车出入段（左入右出）", value: "LIRO" },
    { label: "机车出入段（左出右入）", value: "LORI" },
    { label: "超限货物列车进路", value: "OF" },
];
const equipmentKindLabels = {
    link: "Link",
    signal: "信号机",
    switch: "道岔",
    platform: "站台",
    insulationJoint: "钢轨绝缘",
    bufferStop: "车挡",
};
const equipmentDrawerTitle = computed(() => {
    if (!selectedEquipment.value) return "设备信息";
    const label = equipmentKindLabels[selectedEquipment.value.kind] || "设备";
    return `${label} ${selectedEquipment.value.id || ""}`;
});

function setSelectMode() {
    activeEditMode.value = 0;
    stationLayoutEditorRef.value?.setEditMode(0);
}
function setDrawMode() {
    activeEditMode.value = 1;
    stationLayoutEditorRef.value?.setEditMode(1);
}
function handleEditModeChange(mode) {
    if (Number(mode) === 0) {
        setSelectMode();
    } else {
        routeNodePickTarget.value = "";
        setDrawMode();
    }
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
    return {
        ...route,
        index,
        direction: String(route?.direction ?? route?.Direction ?? ""),
        nodeIds,
        linkIds,
    };
}

function getRouteDirectionLabel(direction) {
    if (direction === "LeftToRight") return "左向右";
    if (direction === "RightToLeft") return "右向左";
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
        ElMessage.warning(t('capacityMain.placeholders.selectInstance'));
        return;
    }

    const startNodeId = String(routeSearchForm.value.startNodeId || "").trim();
    const endNodeId = String(routeSearchForm.value.endNodeId || "").trim();
    if (!startNodeId || !endNodeId) {
        ElMessage.warning("请输入起点和终点 Node ID");
        return;
    }
    const startNodeNumber = Number(startNodeId);
    const endNodeNumber = Number(endNodeId);
    if (!Number.isInteger(startNodeNumber) || !Number.isInteger(endNodeNumber)) {
        ElMessage.warning("Node ID 必须为整数");
        return;
    }

    const params = {
        instanceID: props.selectedInstanceId,
    };
    if (currentStationSchemeId.value) {
        params.stationSchemeID = currentStationSchemeId.value;
    }

    routeSearchLoading.value = true;
    try {
        const response = await axios.post("/StationLayout/SearchRoutes", {
            instanceID: props.selectedInstanceId,
            stationSchemeID: currentStationSchemeId.value,
            startNodeId: startNodeNumber,
            endNodeId: endNodeNumber,
        }, {
            params,
        });
        const routes = Array.isArray(response.data?.routes)
            ? response.data.routes
            : Array.isArray(response.data?.Routes)
                ? response.data.Routes
                : [];
        routeSearchRoutes.value = routes.map((route, index) => normalizeSearchRoute(route, index));
        selectedRouteIndex.value = routeSearchRoutes.value.length > 0 ? 0 : -1;
        ElMessage.success(`搜索完成，共 ${routeSearchRoutes.value.length} 条路径`);
    } catch (err) {
        routeSearchRoutes.value = [];
        selectedRouteIndex.value = -1;
        ElMessage.error(getHttpErrorMessage(err, "路径搜索失败"));
    } finally {
        routeSearchLoading.value = false;
    }
}
function createDefaultLayoutDisplayStyles() {
    return JSON.parse(JSON.stringify(defaultLayoutDisplayStyles));
}
function normalizeLayoutDisplayStyles(styles) {
    const normalized = createDefaultLayoutDisplayStyles();
    const source = styles && typeof styles === "object" && !Array.isArray(styles) ? styles : {};
    for (const row of layoutTextStyleRows) {
        if (source[row.key] && typeof source[row.key] === "object" && !Array.isArray(source[row.key])) {
            normalized[row.key] = { ...normalized[row.key], ...source[row.key] };
        }
    }

    for (const key of ["track", "curve", "platform", "signal", "switch", "node"]) {
        if (source[key] && typeof source[key] === "object" && !Array.isArray(source[key])) {
            normalized[key] = { ...normalized[key], ...source[key] };
        }
    }

    return normalized;
}
function applyLayoutDisplayStyles(styles) {
    layoutDisplayStyles.value = normalizeLayoutDisplayStyles(styles);
}
function buildLayoutJsonWithDisplayStyles(dataStr) {
    const jsonObj = JSON.parse(dataStr);
    jsonObj.metadata = {
        ...(jsonObj.metadata || {}),
        displayStyles: normalizeLayoutDisplayStyles(layoutDisplayStyles.value),
    };
    return JSON.stringify(jsonObj);
}
function resetLayoutDisplayStyles() {
    layoutDisplayStyles.value = createDefaultLayoutDisplayStyles();
}
async function saveLayoutDisplayStyles() {
    const saved = await saveData({
        silent: true,
        successMessage: "显示样式已保存",
        failurePrefix: "显示样式保存失败：",
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
function deleteSelection() {
    stationLayoutEditorRef.value?.deleteLine();
    stationLayoutEditorRef.value?.deleteNode();
    stationLayoutEditorRef.value?.deleteEquipment();
}
function revoke() {
    stationLayoutEditorRef.value?.revoke();
}
function redo() {
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
    return { id, name };
}

function setStationSchemeOptions(options, includeCurrent = true) {
    const optionsById = new Map();
    for (const option of options) {
        if (!option?.id || optionsById.has(option.id)) continue;
        optionsById.set(option.id, option);
    }

    stationSchemeOptions.value = Array.from(optionsById.values());
    if (includeCurrent) {
        ensureCurrentStationSchemeOption();
    }
}

function ensureCurrentStationSchemeOption(name) {
    const id = currentStationSchemeId.value?.trim();
    if (!id) return;
    if (stationSchemeOptions.value.some((option) => option.id === id)) return;

    stationSchemeOptions.value = [
        ...stationSchemeOptions.value,
        {
            id,
            name: name || id,
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
    return axios
        .get("/StationLayout/GetStationSchemes", {
            params: {
                instanceID: instanceId,
            },
        })
        .then((res) => {
            if (props.selectedInstanceId !== instanceId) {
                return [];
            }

            const options = (res.data || [])
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
            ElMessage.error(t('stationLayout.messages.loadSchemesFailed'));
            stationSchemeOptions.value = [];
            return [];
        })
        .finally(() => {
            if (props.selectedInstanceId === instanceId) {
                loadingStationSchemes.value = false;
            }
        });
}

function getHttpErrorMessage(err, fallback) {
    return err?.response?.data || err?.message || fallback;
}

function resetStationSchemeDraft() {
    stationSchemeDraft.value = { name: "" };
}

function cancelStationSchemeEdit() {
    editingStationSchemeOriginalId.value = "";
    editingStationSchemeForm.value = { name: "" };
}

async function openStationSchemeManager() {
    if (!props.selectedInstanceId) {
        ElMessage.warning(t('capacityMain.placeholders.selectInstance'));
        return;
    }

    stationSchemeManagerVisible.value = true;
    resetStationSchemeDraft();
    cancelStationSchemeEdit();
    await loadStationSchemes();
}

async function createStationScheme() {
    const name = stationSchemeDraft.value.name.trim();
    if (!name) {
        ElMessage.warning(t('stationLayout.schemeManager.nameRequired'));
        return;
    }

    stationSchemeManagerSaving.value = true;
    try {
        const res = await axios.post("/StationLayout/CreateStationScheme", {
            instanceID: props.selectedInstanceId,
            name,
        });
        const createdOption = normalizeStationSchemeOption(res.data);
        const createdStationSchemeId = createdOption?.id || "";
        resetStationSchemeDraft();
        currentStationSchemeId.value = createdStationSchemeId;
        await loadStationSchemes();
        if (createdStationSchemeId) {
            getData({ stationSchemeId: createdStationSchemeId });
        }
        ElMessage.success(t('stationLayout.schemeManager.createSuccess'));
    } catch (err) {
        ElMessage.error(getHttpErrorMessage(err, t('stationLayout.schemeManager.createFailed')));
    } finally {
        stationSchemeManagerSaving.value = false;
    }
}

function startEditStationScheme(row) {
    editingStationSchemeOriginalId.value = row.id;
    editingStationSchemeForm.value = {
        name: row.name || row.id,
    };
}

async function saveStationSchemeEdit() {
    const originalID = editingStationSchemeOriginalId.value;
    const name = editingStationSchemeForm.value.name.trim();
    if (!originalID) {
        ElMessage.warning(t('stationLayout.schemeManager.idRequired'));
        return;
    }

    const wasCurrent = currentStationSchemeId.value === originalID;
    stationSchemeManagerSaving.value = true;
    try {
        await axios.put("/StationLayout/EditStationScheme", {
            instanceID: props.selectedInstanceId,
            originalID,
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

    const deletedCurrent = currentStationSchemeId.value === row.id;
    stationSchemeManagerSaving.value = true;
    try {
        await axios.delete("/StationLayout/DeleteStationScheme", {
            params: {
                instanceID: props.selectedInstanceId,
                stationSchemeID: row.id,
            },
        });

        if (deletedCurrent) {
            currentStationSchemeId.value = "";
        }
        cancelStationSchemeEdit();
        await loadStationSchemes({ includeCurrent: !deletedCurrent });

        if (deletedCurrent) {
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
    if (!props.selectedInstanceId) {
        ElMessage.warning(t('capacityMain.placeholders.selectInstance'));
        return Promise.resolve(false);
    }

    var dataStr = stationLayoutEditorRef.value?.buildJsonData();
    if (!dataStr) {
        ElMessage.warning("当前没有可保存的车站布置图数据");
        return Promise.resolve(false);
    }

    try {
        dataStr = buildLayoutJsonWithDisplayStyles(dataStr);
    } catch (err) {
        console.error("Failed to attach layout display styles:", err);
        ElMessage.error("显示样式保存失败，请检查车站布置图数据");
        return Promise.resolve(false);
    }

    const silentSuccessMessage = options?.successMessage || "设备信息已保存";
    const silentFailurePrefix = options?.failurePrefix || "设备信息保存失败：";
    const params = {
        instanceID: props.selectedInstanceId,
    };
    if (currentStationSchemeId.value) {
        params.stationSchemeID = currentStationSchemeId.value;
    }

    savingData.value = true;
    return axios
        .post("/StationLayout/SaveJson", {
            json: dataStr,
            instanceID: props.selectedInstanceId,
            stationSchemeID: currentStationSchemeId.value,
        }, {
            params,
        })
        .then((res) => {
            console.log(res);
            currentStationSchemeId.value = res.data?.stationSchemeID || currentStationSchemeId.value;
            ensureCurrentStationSchemeOption();
            void loadStationSchemes();
            if (silent) {
                ElMessage.success(silentSuccessMessage);
            } else {
                alert(t('stationLayout.messages.saveSuccess') + (res.data?.message || res.data));
            }
            return true;
        })
        .catch((err) => {
            // alert(err);
            var serverMsg = "";
            if (err.response != undefined) {
                serverMsg = err.response.data;
            }
            if (silent) {
                ElMessage.error(silentFailurePrefix + (serverMsg || err));
            } else {
                alert(t('stationLayout.messages.saveFailed') + err + "\r\n" + serverMsg);
            }
            return false;
        })
        .finally(() => {
            savingData.value = false;
        });
}
function getData(options = {}) {
    if (!props.selectedInstanceId) {
        currentStationSchemeId.value = "";
        stationSchemeOptions.value = [];
        resetLayoutDisplayStyles();
        stationLayoutEditorRef.value?.clearElements();
        routeNodePickTarget.value = "";
        clearRouteSearchResult();
        return;
    }

    const instanceId = props.selectedInstanceId;
    const requestedStationSchemeId = options?.stationSchemeId ?? currentStationSchemeId.value;
    const params = {
        instanceID: instanceId,
    };
    if (requestedStationSchemeId) {
        params.stationSchemeID = requestedStationSchemeId;
    }

    loadingData.value = true;
    axios
        .post("/StationLayout/GetJson", null, {
            params,
        })
        .then((res) => {
            if (props.selectedInstanceId !== instanceId) {
                return;
            }

            currentStationSchemeId.value = res.data?.metadata?.stationSchemeID || requestedStationSchemeId || "";
            applyLayoutDisplayStyles(res.data?.metadata?.displayStyles);
            ensureCurrentStationSchemeOption();
            stationLayoutEditorRef.value?.loadDataFromJson(res.data);
            routeNodePickTarget.value = "";
            clearRouteSearchResult();
        })
        .catch((err) => {
            if (props.selectedInstanceId !== instanceId) {
                return;
            }

            var serverMsg = "";
            if (err.response != undefined) {
                serverMsg = err.response.data;
            }
            alert(t('stationLayout.messages.loadFailed') + err + "\r\n" + serverMsg);
        })
        .finally(() => {
            if (props.selectedInstanceId === instanceId) {
                loadingData.value = false;
            }
        });
}

function handleStationSchemeChange(stationSchemeId) {
    if (!stationSchemeId) return;
    routeNodePickTarget.value = "";
    clearRouteSearchResult();
    getData({ stationSchemeId });
}

function exportJsonFile() {
    const dataStr = stationLayoutEditorRef.value?.buildJsonData();
    if (!dataStr) {
        ElMessage.warning("当前没有可导出的车站布置图数据");
        return;
    }

    try {
        const jsonObj = JSON.parse(buildLayoutJsonWithDisplayStyles(dataStr));
        const prettyJson = JSON.stringify(jsonObj, null, 2);
        const blob = new Blob([prettyJson], { type: "application/json;charset=utf-8" });
        const url = URL.createObjectURL(blob);
        const link = document.createElement("a");
        link.href = url;
        link.download = buildExportJsonFileName(jsonObj);
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        URL.revokeObjectURL(url);
        ElMessage.success("JSON 文件已导出");
    } catch (err) {
        console.error("Failed to export station layout JSON:", err);
        ElMessage.error("导出 JSON 文件失败");
    }
}

function handleSelectedAnnotationChange(annotation) {
    selectedAnnotation.value = annotation;
}

function handleSelectedEquipmentChange(equipment) {
    selectedEquipment.value = equipment;
    if (!equipment) {
        equipmentDrawerVisible.value = false;
        equipmentForm.value = {};
        return;
    }

    equipmentForm.value = buildEquipmentForm(equipment);
    equipmentDrawerVisible.value = true;
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
    const data = equipment?.data || {};
    const shouldFallbackNameToId = ["signal", "switch", "platform"].includes(equipment?.kind);
    const equipmentType = equipment?.kind === "signal"
        ? readSignalType(data)
        : equipment?.kind === "bufferStop"
            ? readEquipmentType(data) || DEFAULT_BUFFER_STOP_TYPE
            : readEquipmentType(data);
    const form = {
        kind: equipment?.kind || "",
        originalId: data.id || equipment?.id || "",
        id: data.id || "",
        name: data.name ?? (shouldFallbackNameToId ? data.id || "" : ""),
        type: equipmentType,
        direction: readEquipmentDirection(data),
        bindingNodeID: data.bindingNodeID || "",
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

    return form;
}

function toNumber(value) {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : 0;
}

function buildEquipmentPatchFromForm() {
    const form = equipmentForm.value;
    const patch = {
        id: String(form.id || "").trim(),
    };

    if (form.kind === "signal") {
        patch.name = String(form.name || "").trim();
        patch.type = String(form.type || "").trim();
        patch.direction = String(form.direction || "").trim();
        patch.bindingNodeID = String(form.bindingNodeID || "").trim();
        patch.position = { x: toNumber(form.x), y: toNumber(form.y) };
    } else if (form.kind === "switch") {
        patch.name = String(form.name || "").trim();
        patch.type = String(form.type || "").trim();
        patch.bindingNodeID = String(form.bindingNodeID || "").trim();
        patch.position = { x: toNumber(form.x), y: toNumber(form.y) };
        try {
            const branchVectorList = form.branchVectorListText?.trim()
                ? JSON.parse(form.branchVectorListText)
                : [];
            if (!Array.isArray(branchVectorList)) {
                throw new Error("branchVectorList must be an array.");
            }
            patch.branchVectorList = branchVectorList;
        } catch (err) {
            ElMessage.error("道岔分支向量 JSON 格式不正确");
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
        patch.position = { x: toNumber(form.x), y: toNumber(form.y) };
    } else if (form.kind === "bufferStop") {
        patch.type = String(form.type || DEFAULT_BUFFER_STOP_TYPE).trim();
        patch.direction = String(form.direction || DEFAULT_BUFFER_STOP_DIRECTION).trim();
        patch.bindingNodeID = String(form.bindingNodeID || "").trim();
        patch.position = { x: toNumber(form.x), y: toNumber(form.y) };
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

async function saveEquipmentForm() {
    if (!selectedEquipment.value) return;
    const patch = buildEquipmentPatchFromForm();
    if (!patch) return;
    if (!patch.id) {
        ElMessage.warning("设备 ID 不能为空");
        return;
    }

    equipmentSaving.value = true;
    try {
        stationLayoutEditorRef.value?.updateSelectedEquipment(
            selectedEquipment.value.kind,
            equipmentForm.value.originalId,
            patch
        );
        const saved = await saveData({ silent: true });
        if (saved) {
            equipmentForm.value.originalId = patch.id;
        }
    } finally {
        equipmentSaving.value = false;
    }
}

function updateSelectedAnnotation(patch) {
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
    if (importJsonFileInputRef.value) {
        importJsonFileInputRef.value.value = "";
        importJsonFileInputRef.value.click();
    }
}

async function handleImportJsonFileChange(event) {
    const file = event.target.files?.[0];
    if (!file) {
        return;
    }

    if (!file.name.toLowerCase().endsWith(".json")) {
        event.target.value = "";
        ElMessage.error("请选择 JSON 格式文件");
        return;
    }

    try {
        const text = await file.text();
        const jsonObj = JSON.parse(text);
        validateStationLayoutJson(jsonObj);
        currentStationSchemeId.value = jsonObj?.metadata?.stationSchemeID || currentStationSchemeId.value;
        applyLayoutDisplayStyles(jsonObj?.metadata?.displayStyles);
        ensureCurrentStationSchemeOption();
        stationLayoutEditorRef.value?.loadDataFromJson(jsonObj);
        ElMessage.success("JSON 文件已导入");
    } catch (err) {
        console.error("Failed to import station layout JSON:", err);
        ElMessage.error("导入 JSON 文件失败，请检查文件格式");
    } finally {
        event.target.value = "";
    }
}

function validateStationLayoutJson(jsonObj) {
    if (!jsonObj || typeof jsonObj !== "object" || Array.isArray(jsonObj)) {
        throw new Error("Invalid station layout JSON root.");
    }

    const arrayFields = ["tracks", "curves", "nodes", "signals", "insulationJoints", "bufferStops", "platforms", "switches", "annotations"];
    for (const field of arrayFields) {
        if (jsonObj[field] !== undefined && !Array.isArray(jsonObj[field])) {
            throw new Error(`Invalid station layout JSON field: ${field}`);
        }
    }
}
function autoSeparateLine() {
    stationLayoutEditorRef.value?.autoSeparateLine();
}
function showCrossPoint() {
    stationLayoutEditorRef.value?.markCrossPoint();
}
function hideCrossPoint() {
    stationLayoutEditorRef.value?.removeCrossPoint();
}
function snapLine() {
    stationLayoutEditorRef.value?.snapLine();
}
function setDrawingObject(drawingObj) {
    if (isSelectMode.value) return;
    stationLayoutEditorRef.value?.setDrawingObject(drawingObj);
}

function setDrawingSignalType(signalType) {
    if (isSelectMode.value) return;
    selectedDrawingSignalType.value = signalType;
    stationLayoutEditorRef.value?.setDrawingSignalType(signalType);
    stationLayoutEditorRef.value?.setDrawingObject("s");
    signalDropdownRef.value?.handleClose?.();
}

function setDrawingBufferStopOption(option) {
    if (isSelectMode.value) return;
    const type = String(option?.type || selectedDrawingBufferStopType.value || DEFAULT_BUFFER_STOP_TYPE);
    const direction = String(option?.direction || selectedDrawingBufferStopDirection.value || DEFAULT_BUFFER_STOP_DIRECTION);
    selectedDrawingBufferStopType.value = type;
    selectedDrawingBufferStopDirection.value = direction;
    stationLayoutEditorRef.value?.setDrawingBufferStopType(type);
    stationLayoutEditorRef.value?.setDrawingBufferStopDirection(direction);
    stationLayoutEditorRef.value?.setDrawingObject("e");
}

function setDrawingBufferStopDirection(direction) {
    setDrawingBufferStopOption({
        type: selectedDrawingBufferStopType.value,
        direction,
    });
}

function isSelectedDrawingBufferStopOption(type, direction) {
    return type === selectedDrawingBufferStopType.value && direction === selectedDrawingBufferStopDirection.value;
}

function autoGenerateNode() {
    stationLayoutEditorRef.value?.autoGenerateNodes();
}

function autoMergeNode() {
    stationLayoutEditorRef.value?.autoMergeNode();
}   

function autoGenerateSwitch() {
    stationLayoutEditorRef.value?.autoGenerateSwitches();
}

function autoGenerateCurve() {
    const count = stationLayoutEditorRef.value?.autoGenerateCurves?.() ?? 0;
    ElMessage.success(`已生成 ${count} 条曲线`);
}

function openExtractDwgDialog() {
    selectedDwgFile.value = null;
    dwgLayerName.value = "0";
    extractDwgDialogVisible.value = true;
    if (dwgFileInputRef.value) {
        dwgFileInputRef.value.value = "";
    }
}

function handleDwgFileChange(event) {
    const file = event.target.files?.[0];
    if (!file) {
        selectedDwgFile.value = null;
        return;
    }

    if (!file.name.toLowerCase().endsWith(".dwg")) {
        selectedDwgFile.value = null;
        event.target.value = "";
        ElMessage.error("请选择 DWG 格式文件");
        return;
    }

    selectedDwgFile.value = file;
}

function extractDwgFile() {
    if (!selectedDwgFile.value) {
        ElMessage.warning("请先选择 DWG 文件");
        return;
    }

    const formData = new FormData();
    formData.append("file", selectedDwgFile.value);
    formData.append("layerName", dwgLayerName.value || "0");

    extractingDwg.value = true;
    axios
        .post("/StationLayout/ExtractDwgFile", formData, {
            headers: {
                "Content-Type": "multipart/form-data",
            },
        })
        .then((res) => {
            extractDwgDialogVisible.value = false;
            const layout = res.data?.layout;
            if (!layout) {
                ElMessage.error("DWG 提取失败：服务器未返回图形数据");
                return;
            }

            validateStationLayoutJson(layout);
            stationLayoutEditorRef.value?.clearElements();
            currentStationSchemeId.value = layout?.metadata?.stationSchemeID || currentStationSchemeId.value;
            ensureCurrentStationSchemeOption();
            stationLayoutEditorRef.value?.loadDataFromJson(layout);
            ElMessage.success(`DWG 提取完成，共生成 ${res.data?.segmentCount || 0} 条线段`);
        })
        .catch((err) => {
            var serverMsg = "";
            if (err.response != undefined) {
                serverMsg = err.response.data;
            }
            ElMessage.error("DWG 提取失败：" + (serverMsg || err));
        })
        .finally(() => {
            extractingDwg.value = false;
        });
}

onMounted(() => {
    loadStationSchemes();
    getData();
});

watch(
    () => props.selectedInstanceId,
    () => {
        currentStationSchemeId.value = "";
        stationSchemeOptions.value = [];
        routeNodePickTarget.value = "";
        clearRouteSearchResult();
        loadStationSchemes();
        getData();
    }
);
</script>

<template>
    <div v-loading="loadingData || savingData" style="max-width: 100%; overflow: hidden;">
        <el-menu mode="horizontal" class="station-toolbar" :ellipsis="false">
            <el-menu-item index="station-scheme" class="station-scheme-menu-item" @click.stop>
                <div class="station-scheme-selector" @click.stop>
                    <span class="toolbar-group-label">{{ t('stationLayout.menu.stationScheme') }}</span>
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
            </el-menu-item>
            <!-- 文件操作 -->
            <el-sub-menu index="file">
                <template #title>
                    <el-icon>
                        <Download />
                    </el-icon>{{ t('stationLayout.menu.file') }}
                </template>
                <el-menu-item index="file-load" @click="getData">
                    <el-icon>
                        <Download />
                    </el-icon>{{ t('stationLayout.menu.loadData') }}
                </el-menu-item>
                <el-menu-item index="file-save" @click="saveData">
                    <el-icon>
                        <Upload />
                    </el-icon>{{ t('stationLayout.menu.saveData') }}
                </el-menu-item>
                <el-menu-item index="file-import-json" @click="openImportJsonFile">
                    <el-icon>
                        <Upload />
                    </el-icon> 导入JSON文件
                </el-menu-item>
                <el-menu-item index="file-export-json" @click="exportJsonFile">
                    <el-icon>
                        <Download />
                    </el-icon> 导出JSON文件
                </el-menu-item>
                <el-menu-item index="extract-dwg-file" @click="openExtractDwgDialog">
                    <el-icon>
                        <Download />
                    </el-icon> 从DWG文件提取
                </el-menu-item>
            </el-sub-menu>

            <!-- 追踪设置 -->
            <el-menu-item index="snap-grid" class="toolbar-checkbox-item">
                <el-checkbox v-model="mouseSnap" :label="t('stationLayout.menu.gridSnap')"
                    @change="mouseGridSnapChange" />
            </el-menu-item>
            <el-menu-item index="snap-obj" class="toolbar-checkbox-item">
                <el-checkbox v-model="objectSnap" :label="t('stationLayout.menu.objectSnap')"
                    @change="mouseObjectSnapChange" />
            </el-menu-item>
            <el-menu-item index="snap-distance" class="toolbar-field-item" @click.stop>
                <span class="toolbar-group-label">{{ t('stationLayout.menu.snapDistance') }}</span>
                <el-input-number v-model="objectSnapDistance" size="small" :min="0" :max="200" :step="1"
                    controls-position="right" :disabled="!objectSnap" />
            </el-menu-item>
            <el-menu-item index="show-grid" class="toolbar-checkbox-item">
                <el-checkbox v-model="showGrid" :label="t('stationLayout.menu.showGrid')" />
            </el-menu-item>
            <el-menu-item index="grid-spacing" class="toolbar-field-item" @click.stop>
                <span class="toolbar-group-label">{{ t('stationLayout.menu.gridSpacing') }}</span>
                <el-input-number v-model="gridSpacing" size="small" :min="1" :max="500" :step="1"
                    controls-position="right" />
            </el-menu-item>

            <!-- 撤销/重做 -->
            <el-menu-item index="undo" @click="revoke">
                <el-icon>
                    <RefreshLeft />
                </el-icon>{{ t('stationLayout.menu.undo') }}
            </el-menu-item>
            <el-menu-item index="redo" @click="redo">
                <el-icon>
                    <RefreshRight />
                </el-icon>{{ t('stationLayout.menu.redo') }}
            </el-menu-item>

            <!-- 选择操作 -->
            <el-menu-item index="clear-sel" @click="clearSelection">
                <el-icon>
                    <CircleClose />
                </el-icon>{{ t('stationLayout.menu.clearSelection') }}
            </el-menu-item>
            <el-menu-item index="delete-sel" @click="deleteSelection">
                <el-icon>
                    <Delete />
                </el-icon>{{ t('stationLayout.menu.deleteSelection') }}
            </el-menu-item>
        </el-menu>
        <input ref="importJsonFileInputRef" type="file" accept=".json,application/json" class="hidden-file-input"
            @change="handleImportJsonFileChange" />

        <el-dialog v-model="stationSchemeManagerVisible" :title="t('stationLayout.schemeManager.title')" width="760px"
            :close-on-click-modal="false">
            <div class="station-scheme-manager">
                <div class="station-scheme-create-row">
                    <el-input v-model="stationSchemeDraft.name" size="small" class="station-scheme-name-input"
                        :placeholder="t('stationLayout.schemeManager.namePlaceholder')" />
                    <el-button type="primary" size="small" :loading="stationSchemeManagerSaving"
                        @click="createStationScheme">
                        {{ t('stationLayout.schemeManager.add') }}
                    </el-button>
                </div>
                <el-table :data="stationSchemeOptions" v-loading="loadingStationSchemes || stationSchemeManagerSaving"
                    height="360" class="station-scheme-table">
                    <el-table-column prop="id" :label="t('stationLayout.schemeManager.id')" width="220">
                        <template #default="{ row }">
                            <span>{{ row.id }}</span>
                        </template>
                    </el-table-column>
                    <el-table-column prop="name" :label="t('stationLayout.schemeManager.name')">
                        <template #default="{ row }">
                            <el-input v-if="editingStationSchemeOriginalId === row.id"
                                v-model="editingStationSchemeForm.name" size="small" />
                            <span v-else>{{ row.name || row.id }}</span>
                        </template>
                    </el-table-column>
                    <el-table-column :label="t('stationLayout.schemeManager.operation')" width="220">
                        <template #default="{ row }">
                            <div v-if="editingStationSchemeOriginalId === row.id" class="station-scheme-actions">
                                <el-button type="success" size="small" @click="saveStationSchemeEdit">
                                    {{ t('stationLayout.schemeManager.save') }}
                                </el-button>
                                <el-button size="small" @click="cancelStationSchemeEdit">
                                    {{ t('stationLayout.schemeManager.cancel') }}
                                </el-button>
                            </div>
                            <div v-else class="station-scheme-actions">
                                <el-button type="primary" size="small" @click="startEditStationScheme(row)">
                                    {{ t('stationLayout.schemeManager.edit') }}
                                </el-button>
                                <el-button type="danger" size="small" @click="deleteStationScheme(row)">
                                    {{ t('stationLayout.schemeManager.delete') }}
                                </el-button>
                            </div>
                        </template>
                    </el-table-column>
                </el-table>
            </div>
            <template #footer>
                <el-button @click="stationSchemeManagerVisible = false">
                    {{ t('stationLayout.schemeManager.close') }}
                </el-button>
            </template>
        </el-dialog>

        <el-dialog v-model="layoutStyleDialogVisible" title="显示样式配置" width="920px" class="layout-style-dialog"
            :close-on-click-modal="false">
            <el-tabs>
                <el-tab-pane label="文字">
                    <div class="layout-style-table">
                        <div class="layout-style-table-header">对象</div>
                        <div class="layout-style-table-header">大小</div>
                        <div class="layout-style-table-header">字体</div>
                        <div class="layout-style-table-header">粗细</div>
                        <div class="layout-style-table-header">样式</div>
                        <div class="layout-style-table-header">颜色</div>
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
                <el-tab-pane label="线条与设备">
                    <div class="layout-style-grid">
                        <section class="layout-style-section">
                            <h4>轨道线条</h4>
                            <div class="layout-style-field">
                                <span>粗细</span>
                                <el-input-number v-model="layoutDisplayStyles.track.strokeWidth" size="small" :min="0.5"
                                    :max="12" :step="0.5" controls-position="right" />
                            </div>
                            <div class="layout-style-field">
                                <span>颜色</span>
                                <el-color-picker v-model="layoutDisplayStyles.track.color" size="small" show-alpha />
                            </div>
                        </section>
                        <section class="layout-style-section">
                            <h4>曲线线条</h4>
                            <div class="layout-style-field">
                                <span>粗细</span>
                                <el-input-number v-model="layoutDisplayStyles.curve.strokeWidth" size="small" :min="0.5"
                                    :max="12" :step="0.5" controls-position="right" />
                            </div>
                            <div class="layout-style-field">
                                <span>颜色</span>
                                <el-color-picker v-model="layoutDisplayStyles.curve.color" size="small" show-alpha />
                            </div>
                        </section>
                        <section class="layout-style-section">
                            <h4>站台线条</h4>
                            <div class="layout-style-field">
                                <span>粗细</span>
                                <el-input-number v-model="layoutDisplayStyles.platform.strokeWidth" size="small"
                                    :min="0.5" :max="12" :step="0.5" controls-position="right" />
                            </div>
                            <div class="layout-style-field">
                                <span>颜色</span>
                                <el-color-picker v-model="layoutDisplayStyles.platform.color" size="small" show-alpha />
                            </div>
                        </section>
                        <section class="layout-style-section">
                            <h4>信号机</h4>
                            <div class="layout-style-field">
                                <span>大小</span>
                                <el-input-number v-model="layoutDisplayStyles.signal.scale" size="small" :min="0.2"
                                    :max="2" :step="0.05" controls-position="right" />
                            </div>
                        </section>
                        <section class="layout-style-section">
                            <h4>道岔</h4>
                            <div class="layout-style-field">
                                <span>线条粗细</span>
                                <el-input-number v-model="layoutDisplayStyles.switch.strokeWidth" size="small" :min="1"
                                    :max="16" :step="0.5" controls-position="right" />
                            </div>
                            <div class="layout-style-field">
                                <span>颜色</span>
                                <el-color-picker v-model="layoutDisplayStyles.switch.color" size="small" show-alpha />
                            </div>
                        </section>
                        <section class="layout-style-section">
                            <h4>节点</h4>
                            <div class="layout-style-field">
                                <span>大小</span>
                                <el-input-number v-model="layoutDisplayStyles.node.radius" size="small" :min="1"
                                    :max="24" :step="1" controls-position="right" />
                            </div>
                            <div class="layout-style-field">
                                <span>颜色</span>
                                <el-color-picker v-model="layoutDisplayStyles.node.color" size="small" show-alpha />
                            </div>
                        </section>
                    </div>
                </el-tab-pane>
            </el-tabs>
            <template #footer>
                <el-button @click="resetLayoutDisplayStyles">恢复默认</el-button>
                <el-button type="primary" :loading="savingData" @click="saveLayoutDisplayStyles">保存</el-button>
                <el-button @click="layoutStyleDialogVisible = false">关闭</el-button>
            </template>
        </el-dialog>

        <!-- 第二行：模式 / 绘图对象 / 工具 -->
        <div class="toolbar-row">
            <div class="toolbar-group">
                <span class="toolbar-group-label">{{ t('stationLayout.group.mode') }}</span>
                <el-radio-group v-model="activeEditMode" class="mode-toggle" size="small" @change="handleEditModeChange">
                    <el-radio-button :value="0">
                        <el-icon>
                            <Pointer />
                        </el-icon>
                        <span>{{ t('stationLayout.mode.select') }}</span>
                    </el-radio-button>
                    <el-radio-button :value="1">
                        <el-icon>
                            <EditPen />
                        </el-icon>
                        <span>{{ t('stationLayout.mode.draw') }}</span>
                    </el-radio-button>
                </el-radio-group>
            </div>
            <el-divider direction="vertical" />
            <div class="toolbar-group">
                <span class="toolbar-group-label">{{ t('stationLayout.group.drawingObject') }}</span>
                <el-button-group>
                    <el-button :icon="Minus" :disabled="isSelectMode" @click="setDrawingObject('l')">{{ t('stationLayout.draw.line')
                        }}</el-button>
                    <el-button :icon="Location" :disabled="isSelectMode" @click="setDrawingObject('n')">{{ t('stationLayout.draw.node')
                        }}</el-button>
                    <el-dropdown ref="signalDropdownRef" trigger="click" :disabled="isSelectMode" :hide-on-click="false">
                        <el-button :icon="Bell" :disabled="isSelectMode">
                            {{ t('stationLayout.draw.signal') }}
                            <el-icon class="el-icon--right">
                                <ArrowDown />
                            </el-icon>
                        </el-button>
                        <template #dropdown>
                            <el-dropdown-menu class="signal-type-dropdown-menu">
                                <li class="signal-type-cascader-item">
                                    <el-cascader-panel class="signal-type-cascader-panel"
                                        :model-value="selectedDrawingSignalType"
                                        :options="signalTypeMenuOptions"
                                        :props="signalTypeCascaderProps"
                                        @change="setDrawingSignalType" />
                                </li>
                            </el-dropdown-menu>
                        </template>
                    </el-dropdown>
                    <el-button :icon="Switch" :disabled="isSelectMode" @click="setDrawingObject('w')">{{ t('stationLayout.draw.switch')
                        }}</el-button>
                    <el-button :icon="Filter" :disabled="isSelectMode" @click="setDrawingObject('i')">{{ t('stationLayout.draw.insulation')
                        }}</el-button>
                    <el-button :icon="Guide" :disabled="isSelectMode" @click="setDrawingObject('r')">{{ t('stationLayout.draw.route')
                        }}</el-button>
                    <el-dropdown trigger="click" :disabled="isSelectMode" @command="setDrawingBufferStopOption">
                        <el-button :icon="Stopwatch" :disabled="isSelectMode">
                            {{ t('stationLayout.draw.buffer') }}
                            <el-icon class="el-icon--right">
                                <ArrowDown />
                            </el-icon>
                        </el-button>
                        <template #dropdown>
                            <el-dropdown-menu>
                                <template v-for="(typeOption, typeIndex) in bufferStopTypeOptions"
                                    :key="typeOption.value">
                                    <el-dropdown-item disabled class="drawing-option-group-label"
                                        :divided="typeIndex > 0">
                                        {{ typeOption.label }}
                                    </el-dropdown-item>
                                    <el-dropdown-item v-for="directionOption in bufferStopDirectionOptions"
                                        :key="`${typeOption.value}-${directionOption.value}`"
                                        :command="{ type: typeOption.value, direction: directionOption.value }"
                                        :class="{ 'drawing-option-selected': isSelectedDrawingBufferStopOption(typeOption.value, directionOption.value) }">
                                        {{ directionOption.label }}
                                    </el-dropdown-item>
                                </template>
                            </el-dropdown-menu>
                        </template>
                    </el-dropdown>
                    <el-button :icon="Platform" :disabled="isSelectMode" @click="setDrawingObject('p')">{{ t('stationLayout.draw.platform')
                        }}</el-button>
                    <el-button :icon="EditPen" :disabled="isSelectMode" @click="setDrawingObject('a')">{{ t('stationLayout.draw.annotation')
                        }}</el-button>
                </el-button-group>
            </div>
            <el-divider direction="vertical" />
            <div class="toolbar-group">
                <span class="toolbar-group-label">{{ t('stationLayout.group.tools') }}</span>
                <el-button-group>
                    <el-button :icon="Aim" @click="showCrossPoint">{{ t('stationLayout.tools.showCrossPoint')
                        }}</el-button>
                    <el-button :icon="Hide" @click="hideCrossPoint">{{ t('stationLayout.tools.hideCrossPoint')
                        }}</el-button>
                    <el-button :icon="Connection" @click="snapLine">{{ t('stationLayout.tools.snapLine') }}</el-button>
                    <el-button :icon="Scissor" @click="autoSeparateLine">{{ t('stationLayout.tools.separateLine')
                        }}</el-button>
                    <el-button :icon="Share" @click="autoGenerateNode">{{ t('stationLayout.tools.generateNode')
                        }}</el-button>
                        <el-button :icon="Share" @click="autoMergeNode">节点合并</el-button>
                    <el-button :icon="SetUp" @click="autoGenerateSwitch">{{ t('stationLayout.tools.generateSwitch')
                        }}</el-button>
                    <el-button :icon="Connection" @click="autoGenerateCurve">{{ t('stationLayout.tools.generateCurve')
                        }}</el-button>
                </el-button-group>
            </div>
            <el-divider direction="vertical" />
            <div class="toolbar-group">
                <span class="toolbar-group-label">路径测试</span>
                <el-button :icon="Guide" :type="routeTesterVisible ? 'primary' : 'default'" @click="toggleRouteTester">
                    路径搜索
                </el-button>
            </div>
            <el-divider direction="vertical" />
            <div class="toolbar-group curve-display-toolbar-group">
                <span class="toolbar-group-label">{{ t('stationLayout.group.curveDisplay') }}</span>
                <el-switch v-model="showCurveArc" class="curve-display-switch" inline-prompt
                    :active-text="t('stationLayout.curveDisplay.arc')"
                    :inactive-text="t('stationLayout.curveDisplay.tangent')" />
            </div>
            <el-divider direction="vertical" />
            <div class="toolbar-group node-display-toolbar-group">
                <span class="toolbar-group-label">节点显示</span>
                <el-switch v-model="showNodes" class="node-display-switch" inline-prompt active-text="显示"
                    inactive-text="隐藏" />
            </div>
            <el-divider direction="vertical" />
            <div class="toolbar-group">
                <span class="toolbar-group-label">显示样式</span>
                <el-button :icon="SetUp" @click="layoutStyleDialogVisible = true">配置</el-button>
            </div>
            <el-divider direction="vertical" />
            <div class="toolbar-group scale-toolbar-group">
                <span class="toolbar-group-label">{{ t('stationLayout.group.displayScale') }}</span>
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
        </div>
        <div v-if="selectedAnnotation" class="annotation-editor-row">
            <span class="toolbar-group-label">注释</span>
            <el-input v-model="selectedAnnotation.text" size="small" class="annotation-text-input"
                @input="updateSelectedAnnotation({ text: selectedAnnotation.text })" />
            <el-select v-model="selectedAnnotation.fontFamily" size="small" class="annotation-font-select"
                @change="updateSelectedAnnotation({ fontFamily: selectedAnnotation.fontFamily })">
                <el-option v-for="fontFamily in annotationFontFamilyOptions" :key="fontFamily" :label="fontFamily"
                    :value="fontFamily" />
            </el-select>
            <el-input-number v-model="selectedAnnotation.fontSize" size="small" :min="8" :max="96" :step="1"
                controls-position="right" @change="updateSelectedAnnotation({ fontSize: selectedAnnotation.fontSize })" />
            <el-select v-model="selectedAnnotation.fontWeight" size="small" class="annotation-small-select"
                @change="updateSelectedAnnotation({ fontWeight: selectedAnnotation.fontWeight })">
                <el-option v-for="item in annotationFontWeightOptions" :key="item.value" :label="item.label"
                    :value="item.value" />
            </el-select>
            <el-select v-model="selectedAnnotation.fontStyle" size="small" class="annotation-small-select"
                @change="updateSelectedAnnotation({ fontStyle: selectedAnnotation.fontStyle })">
                <el-option v-for="item in annotationFontStyleOptions" :key="item.value" :label="item.label"
                    :value="item.value" />
            </el-select>
            <span class="annotation-field-label">角度</span>
            <el-input-number v-model="selectedAnnotation.angle" size="small" :min="-180" :max="180" :step="5"
                controls-position="right" @change="updateSelectedAnnotation({ angle: selectedAnnotation.angle })" />
            <span class="annotation-field-label">X</span>
            <el-input-number v-model="selectedAnnotation.position.x" size="small" :step="10" controls-position="right"
                @change="updateSelectedAnnotationPosition" />
            <span class="annotation-field-label">Y</span>
            <el-input-number v-model="selectedAnnotation.position.y" size="small" :step="10" controls-position="right"
                @change="updateSelectedAnnotationPosition" />
        </div>
        <div class="station-layout-workspace">
            <div class="station-layout-editor-frame">
                <StationLayoutEditor ref="stationLayoutEditorRef" :display-scale-x="layoutScaleX"
                    :display-scale-y="layoutScaleY" :show-curve-arc="showCurveArc" :show-nodes="showNodes"
                    :show-grid="showGrid" :object-snap-distance="objectSnapDistance"
                    :grid-spacing="gridSpacing"
                    :display-styles="layoutDisplayStyles"
                    :route-pick-target="routeNodePickTarget"
                    :highlighted-route-link-ids="highlightedRouteLinkIds"
                    :highlighted-route-node-ids="highlightedRouteNodeIds"
                    @selected-annotation-change="handleSelectedAnnotationChange"
                    @selected-equipment-change="handleSelectedEquipmentChange"
                    @route-node-pick="handleRouteNodePick" />
            </div>
            <aside v-if="equipmentDrawerVisible" class="equipment-side-panel">
                <div class="equipment-side-panel-header">
                    <div>
                        <div class="equipment-side-panel-title">{{ equipmentDrawerTitle }}</div>
                        <div class="equipment-side-panel-subtitle">设备信息</div>
                    </div>
                    <el-button text size="small" @click="equipmentDrawerVisible = false">关闭</el-button>
                </div>
                <div class="equipment-side-panel-body">
                    <el-form v-if="selectedEquipment" label-position="top" class="equipment-form">
                        <el-form-item label="设备类型">
                            <el-tag type="info">{{ equipmentKindLabels[equipmentForm.kind] || "设备" }}</el-tag>
                        </el-form-item>
                        <el-form-item label="ID">
                            <el-input v-model="equipmentForm.id" />
                        </el-form-item>
                        <el-form-item v-if="['link', 'signal', 'switch', 'platform'].includes(equipmentForm.kind)"
                            label="Name">
                            <el-input v-model="equipmentForm.name" />
                        </el-form-item>
                        <el-form-item v-if="equipmentForm.kind === 'link'" label="ArrowDirection">
                            <el-select v-model="equipmentForm.arrowDirection">
                                <el-option v-for="option in linkArrowDirectionOptions" :key="option.value"
                                    :label="option.label" :value="option.value" />
                            </el-select>
                        </el-form-item>
                        <el-form-item v-if="equipmentForm.kind === 'link'" label="ArrowType">
                            <el-select v-model="equipmentForm.arrowType">
                                <el-option v-for="option in linkArrowTypeOptions" :key="option.value"
                                    :label="option.label" :value="option.value" />
                            </el-select>
                        </el-form-item>
                        <el-form-item v-if="['signal', 'switch', 'insulationJoint'].includes(equipmentForm.kind)"
                            label="Type">
                            <el-input v-model="equipmentForm.type" />
                        </el-form-item>
                        <el-form-item v-if="equipmentForm.kind === 'bufferStop'" label="Type">
                            <el-select v-model="equipmentForm.type">
                                <el-option v-for="option in bufferStopTypeOptions" :key="option.value"
                                    :label="option.label" :value="option.value" />
                            </el-select>
                        </el-form-item>
                        <el-form-item v-if="equipmentForm.kind === 'signal'" label="Direction">
                            <el-select v-model="equipmentForm.direction">
                                <el-option label="e" value="e" />
                                <el-option label="w" value="w" />
                                <el-option label="s" value="s" />
                                <el-option label="d" value="d" />
                            </el-select>
                        </el-form-item>
                        <el-form-item v-if="equipmentForm.kind === 'bufferStop'" label="Direction">
                            <el-select v-model="equipmentForm.direction">
                                <el-option v-for="option in bufferStopDirectionOptions" :key="option.value"
                                    :label="option.label" :value="option.value" />
                            </el-select>
                        </el-form-item>
                        <el-form-item v-if="['signal', 'switch', 'insulationJoint', 'bufferStop'].includes(equipmentForm.kind)"
                            label="BindingNodeID">
                            <el-input v-model="equipmentForm.bindingNodeID" />
                        </el-form-item>
                        <div v-if="['signal', 'switch', 'insulationJoint', 'bufferStop'].includes(equipmentForm.kind)"
                            class="equipment-form-grid">
                            <el-form-item label="X">
                                <el-input-number v-model="equipmentForm.x" controls-position="right" :step="10" />
                            </el-form-item>
                            <el-form-item label="Y">
                                <el-input-number v-model="equipmentForm.y" controls-position="right" :step="10" />
                            </el-form-item>
                        </div>
                        <div v-if="equipmentForm.kind === 'platform'" class="equipment-form-grid">
                            <el-form-item label="X">
                                <el-input-number v-model="equipmentForm.x" controls-position="right" :step="10" />
                            </el-form-item>
                            <el-form-item label="Y">
                                <el-input-number v-model="equipmentForm.y" controls-position="right" :step="10" />
                            </el-form-item>
                            <el-form-item label="Width">
                                <el-input-number v-model="equipmentForm.width" controls-position="right" :min="0"
                                    :step="10" />
                            </el-form-item>
                            <el-form-item label="Height">
                                <el-input-number v-model="equipmentForm.height" controls-position="right" :min="0"
                                    :step="10" />
                            </el-form-item>
                        </div>
                        <div v-if="equipmentForm.kind === 'link'" class="equipment-form-grid">
                            <el-form-item label="X1">
                                <el-input-number v-model="equipmentForm.x1" controls-position="right" :step="10" />
                            </el-form-item>
                            <el-form-item label="Y1">
                                <el-input-number v-model="equipmentForm.y1" controls-position="right" :step="10" />
                            </el-form-item>
                            <el-form-item label="X2">
                                <el-input-number v-model="equipmentForm.x2" controls-position="right" :step="10" />
                            </el-form-item>
                            <el-form-item label="Y2">
                                <el-input-number v-model="equipmentForm.y2" controls-position="right" :step="10" />
                            </el-form-item>
                        </div>
                        <el-form-item v-if="equipmentForm.kind === 'link'" label="FromNodeID">
                            <el-input v-model="equipmentForm.fromNodeID" />
                        </el-form-item>
                        <el-form-item v-if="equipmentForm.kind === 'link'" label="ToNodeID">
                            <el-input v-model="equipmentForm.toNodeID" />
                        </el-form-item>
                        <el-form-item v-if="equipmentForm.kind === 'switch'" label="BranchVectorList">
                            <el-input v-model="equipmentForm.branchVectorListText" type="textarea" :rows="8" />
                        </el-form-item>
                    </el-form>
                </div>
                <div class="equipment-side-panel-footer">
                    <el-button @click="equipmentDrawerVisible = false">关闭</el-button>
                    <el-button type="primary" :loading="equipmentSaving || savingData" @click="saveEquipmentForm">
                        保存
                    </el-button>
                </div>
            </aside>
            <aside v-if="routeTesterVisible" class="route-search-panel">
                <div class="route-search-panel-header">
                    <div>
                        <div class="route-search-panel-title">路径搜索测试</div>
                        <div class="route-search-panel-subtitle">{{ currentStationSchemeId || "当前方案" }}</div>
                    </div>
                    <el-button text size="small" @click="toggleRouteTester">关闭</el-button>
                </div>
                <div class="route-search-panel-body">
                    <div class="route-search-form">
                        <label class="route-search-label">起点 Node ID</label>
                        <div class="route-search-input-row">
                            <el-input v-model="routeSearchForm.startNodeId" size="small" clearable />
                            <el-button size="small" :type="routeNodePickTarget === 'start' ? 'primary' : 'default'"
                                @click="setRouteNodePickTarget('start')">
                                点选
                            </el-button>
                        </div>
                        <label class="route-search-label">终点 Node ID</label>
                        <div class="route-search-input-row">
                            <el-input v-model="routeSearchForm.endNodeId" size="small" clearable />
                            <el-button size="small" :type="routeNodePickTarget === 'end' ? 'primary' : 'default'"
                                @click="setRouteNodePickTarget('end')">
                                点选
                            </el-button>
                        </div>
                        <div class="route-search-actions">
                            <el-button type="primary" size="small" :loading="routeSearchLoading"
                                @click="searchStationRoutes">
                                搜索
                            </el-button>
                            <el-button size="small" @click="clearRouteSearchResult">清空</el-button>
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
                        <el-table-column label="方向" width="72">
                            <template #default="{ row }">
                                {{ getRouteDirectionLabel(row.direction) }}
                            </template>
                        </el-table-column>
                        <el-table-column label="路径">
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
        <el-dialog v-model="extractDwgDialogVisible" title="从DWG文件提取" width="420px" :close-on-click-modal="false">
            <div class="dwg-extract-form">
                <label class="dwg-extract-label">DWG 文件</label>
                <input ref="dwgFileInputRef" type="file" accept=".dwg" @change="handleDwgFileChange" />
                <label class="dwg-extract-label">图层名称</label>
                <el-input v-model="dwgLayerName" placeholder="请输入要提取的图层名称" />
            </div>
            <template #footer>
                <el-button @click="extractDwgDialogVisible = false">取消</el-button>
                <el-button type="primary" :loading="extractingDwg" @click="extractDwgFile">
                    上传并提取
                </el-button>
            </template>
        </el-dialog>
        <!-- <div id="equipmentlist" style="
                width: 400px;
                position: absolute;
                top: 100px;
                left: 50px;
                opacity: 0.8;
            ">
            <SignalInfoTabVue></SignalInfoTabVue>
        </div> -->
        <div id="equipmentinfolist"></div>
    </div>
</template>

<style scoped>
.station-toolbar {
    border-bottom: 1px solid var(--el-border-color-light);
    background-color: #fff;
    height: 32px;
}

.station-toolbar .el-menu-item,
.station-toolbar :deep(.el-sub-menu__title) {
    height: 32px;
    line-height: 32px;
    font-size: 13px;
}

.station-scheme-menu-item {
    padding: 0 10px;
    cursor: default;
}

.station-scheme-menu-item:hover {
    background-color: transparent !important;
}

.station-scheme-selector {
    display: flex;
    align-items: center;
    gap: 6px;
    height: 32px;
}

.station-scheme-select {
    width: 240px;
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

.station-scheme-name-input {
    flex: 1;
    min-width: 180px;
}

.station-scheme-table {
    width: 100%;
}

.station-scheme-actions {
    display: flex;
    align-items: center;
    gap: 6px;
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
    gap: 6px;
    cursor: default;
}

.toolbar-field-item:hover {
    background-color: transparent !important;
}

.toolbar-field-item :deep(.el-input-number) {
    width: 92px;
}

.toolbar-row {
    display: flex;
    align-items: center;
    flex-wrap: wrap;
    gap: 8px;
    padding: 6px 12px;
    background-color: #fafafa;
    border-bottom: 1px solid var(--el-border-color-light);
}

.toolbar-group {
    display: flex;
    align-items: center;
    gap: 6px;
}

.toolbar-group :deep(.el-button-group .el-dropdown) {
    display: inline-flex;
}

.toolbar-group :deep(.el-button-group .el-dropdown .el-button) {
    border-radius: 0;
}

:global(.drawing-option-selected) {
    color: var(--el-color-primary);
    font-weight: 600;
    background-color: var(--el-color-primary-light-9);
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

:global(.signal-type-dropdown-menu) {
    padding: 0;
}

:global(.signal-type-cascader-item) {
    list-style: none;
    margin: 0;
    padding: 0;
}

:global(.signal-type-cascader-panel) {
    border: 0;
}

.mode-toggle :deep(.el-radio-button__inner) {
    display: inline-flex;
    align-items: center;
    gap: 4px;
}

.toolbar-group-label {
    font-size: 12px;
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

.node-display-toolbar-group {
    min-height: 24px;
}

.curve-display-switch {
    --el-switch-on-color: #409eff;
    --el-switch-off-color: #909399;
}

.node-display-switch {
    --el-switch-on-color: #409eff;
    --el-switch-off-color: #909399;
}

.curve-display-switch :deep(.el-switch__core) {
    min-width: 56px;
}

.node-display-switch :deep(.el-switch__core) {
    min-width: 52px;
}

.scale-slider {
    display: flex;
    align-items: center;
    gap: 6px;
    width: 190px;
}

.scale-slider-label {
    width: 12px;
    font-size: 12px;
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
    font-size: 12px;
    color: #606266;
    white-space: nowrap;
}

.annotation-editor-row :deep(.el-input-number) {
    width: 96px;
}

.station-layout-workspace {
    display: flex;
    align-items: stretch;
    width: 100%;
    height: calc(100vh - 160px);
    min-height: 420px;
    overflow: hidden;
    background-color: #31363f;
}

.station-layout-editor-frame {
    flex: 1 1 auto;
    min-width: 0;
    height: 100%;
    overflow: auto;
    background-color: #31363f;
}

.equipment-side-panel {
    flex: 0 0 360px;
    width: 360px;
    height: 100%;
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

.equipment-form-grid {
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: 0 10px;
}

.equipment-form-grid :deep(.el-input-number) {
    width: 100%;
}

.route-search-panel {
    flex: 0 0 380px;
    width: 380px;
    height: 100%;
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
    gap: 8px;
}

.route-search-label {
    font-size: 12px;
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

    .route-search-panel {
        flex-basis: 320px;
        width: 320px;
    }
}

.dwg-extract-form {
    display: grid;
    gap: 10px;
}

.dwg-extract-label {
    font-size: 13px;
    color: #606266;
    font-weight: 500;
}

.hidden-file-input {
    display: none;
}
</style>
