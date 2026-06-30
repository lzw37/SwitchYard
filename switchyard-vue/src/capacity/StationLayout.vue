<script setup>
import { computed, onMounted, ref, watch } from "vue";
import { useI18n } from 'vue-i18n';
import axios from "@/utils/axios";
import { ElMessage } from "element-plus";
import SignalInfoTabVue from "./components/SignalInfoTab.vue";
import StationLayoutEditor from "./components/StationLayoutEditor.vue";
import {
    Download, Upload, Aim, Hide, Connection, Scissor,
    Share, SetUp,
    Pointer, EditPen,
    Minus, Location, Bell, Switch, Filter, Guide, Stopwatch, Platform,
    Grid, Magnet,
    RefreshLeft, RefreshRight,
    CircleClose, Delete
} from '@element-plus/icons-vue';

const { t } = useI18n();
const props = defineProps({
    selectedInstanceId: {
        type: String,
        default: "",
    },
});
const stationLayoutEditorRef = ref(null);
const extractDwgDialogVisible = ref(false);
const dwgFileInputRef = ref(null);
const importJsonFileInputRef = ref(null);
const selectedDwgFile = ref(null);
const dwgLayerName = ref("0");
const extractingDwg = ref(false);
const loadingData = ref(false);
const savingData = ref(false);
const currentStationSchemeId = ref("");
const layoutScaleX = ref(1);
const layoutScaleY = ref(1);
const layoutScaleXDisplay = computed(() => layoutScaleX.value.toFixed(2));
const layoutScaleYDisplay = computed(() => layoutScaleY.value.toFixed(2));
const selectedAnnotation = ref(null);
const selectedEquipment = ref(null);
const equipmentDrawerVisible = ref(false);
const equipmentForm = ref({});
const equipmentSaving = ref(false);

const annotationFontFamilyOptions = ["Arial", "Microsoft YaHei", "SimSun", "SimHei", "Times New Roman", "Consolas"];
const annotationFontWeightOptions = [
    { label: "常规", value: "normal" },
    { label: "加粗", value: "bold" },
];
const annotationFontStyleOptions = [
    { label: "常规", value: "normal" },
    { label: "斜体", value: "italic" },
];
const equipmentKindLabels = {
    link: "Link",
    signal: "信号机",
    switch: "道岔",
    platform: "站台",
    insulationJoint: "钢轨绝缘",
};
const equipmentDrawerTitle = computed(() => {
    if (!selectedEquipment.value) return "设备信息";
    const label = equipmentKindLabels[selectedEquipment.value.kind] || "设备";
    return `${label} ${selectedEquipment.value.id || ""}`;
});

function setSelectMode() {
    stationLayoutEditorRef.value?.setEditMode(0);
}
function setDrawMode() {
    stationLayoutEditorRef.value?.setEditMode(1);
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
function mouseGridSnapChange(e) {
    if (mouseSnap.value === false) {
        stationLayoutEditorRef.value?.setMouseGridSnapModeCode(0);
    } else {
        stationLayoutEditorRef.value?.setMouseGridSnapModeCode(1);
    }
}
function saveData(options = {}) {
    const silent = options?.silent === true;
    if (!props.selectedInstanceId) {
        ElMessage.warning(t('capacityMain.placeholders.selectInstance'));
        return Promise.resolve(false);
    }

    var dataStr = stationLayoutEditorRef.value?.buildJsonData();
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
            if (silent) {
                ElMessage.success("设备信息已保存");
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
                ElMessage.error("设备信息保存失败：" + (serverMsg || err));
            } else {
                alert(t('stationLayout.messages.saveFailed') + err + "\r\n" + serverMsg);
            }
            return false;
        })
        .finally(() => {
            savingData.value = false;
        });
}
function getData() {
    if (!props.selectedInstanceId) {
        currentStationSchemeId.value = "";
        stationLayoutEditorRef.value?.clearElements();
        return;
    }

    loadingData.value = true;
    axios
        .post("/StationLayout/GetJson", null, {
            params: {
                instanceID: props.selectedInstanceId,
            },
        })
        .then((res) => {
            currentStationSchemeId.value = res.data?.metadata?.stationSchemeID || "";
            stationLayoutEditorRef.value?.loadDataFromJson(res.data);
        })
        .catch((err) => {
            var serverMsg = "";
            if (err.response != undefined) {
                serverMsg = err.response.data;
            }
            alert(t('stationLayout.messages.loadFailed') + err + "\r\n" + serverMsg);
        })
        .finally(() => {
            loadingData.value = false;
        });
}
function exportJsonFile() {
    const dataStr = stationLayoutEditorRef.value?.buildJsonData();
    if (!dataStr) {
        ElMessage.warning("当前没有可导出的车站布置图数据");
        return;
    }

    try {
        const jsonObj = JSON.parse(dataStr);
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

function buildEquipmentForm(equipment) {
    const data = equipment?.data || {};
    const shouldFallbackNameToId = ["signal", "switch", "platform"].includes(equipment?.kind);
    const form = {
        kind: equipment?.kind || "",
        originalId: data.id || equipment?.id || "",
        id: data.id || "",
        name: data.name ?? (shouldFallbackNameToId ? data.id || "" : ""),
        type: data.type || "",
        direction: data.direction || "",
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
    } else if (form.kind === "link") {
        patch.name = String(form.name || "").trim();
        patch.x1 = toNumber(form.x1);
        patch.y1 = toNumber(form.y1);
        patch.x2 = toNumber(form.x2);
        patch.y2 = toNumber(form.y2);
        patch.fromNodeID = String(form.fromNodeID || "").trim();
        patch.toNodeID = String(form.toNodeID || "").trim();
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

    const arrayFields = ["tracks", "curves", "nodes", "signals", "insulationJoints", "platforms", "switches", "annotations"];
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
    stationLayoutEditorRef.value?.setDrawingObject(drawingObj);
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
    getData();
});

watch(
    () => props.selectedInstanceId,
    () => {
        getData();
    }
);
</script>

<template>
    <div v-loading="loadingData || savingData" style="max-width: 100%; overflow: hidden;">
        <el-menu mode="horizontal" class="station-toolbar" :ellipsis="false">
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
                <el-checkbox :label="t('stationLayout.menu.objectSnap')" />
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

        <!-- 第二行：模式 / 绘图对象 / 工具 -->
        <div class="toolbar-row">
            <div class="toolbar-group">
                <span class="toolbar-group-label">{{ t('stationLayout.group.mode') }}</span>
                <el-button-group>
                    <el-button :icon="Pointer" @click="setSelectMode">{{ t('stationLayout.mode.select') }}</el-button>
                    <el-button :icon="EditPen" @click="setDrawMode">{{ t('stationLayout.mode.draw') }}</el-button>
                </el-button-group>
            </div>
            <el-divider direction="vertical" />
            <div class="toolbar-group">
                <span class="toolbar-group-label">{{ t('stationLayout.group.drawingObject') }}</span>
                <el-button-group>
                    <el-button :icon="Minus" @click="setDrawingObject('l')">{{ t('stationLayout.draw.line')
                        }}</el-button>
                    <el-button :icon="Location" @click="setDrawingObject('n')">{{ t('stationLayout.draw.node')
                        }}</el-button>
                    <el-button :icon="Bell" @click="setDrawingObject('s')">{{ t('stationLayout.draw.signal')
                        }}</el-button>
                    <el-button :icon="Switch" @click="setDrawingObject('w')">{{ t('stationLayout.draw.switch')
                        }}</el-button>
                    <el-button :icon="Filter" @click="setDrawingObject('i')">{{ t('stationLayout.draw.insulation')
                        }}</el-button>
                    <el-button :icon="Guide" @click="setDrawingObject('r')">{{ t('stationLayout.draw.route')
                        }}</el-button>
                    <el-button :icon="Stopwatch" @click="setDrawingObject('e')">{{ t('stationLayout.draw.buffer')
                        }}</el-button>
                    <el-button :icon="Platform" @click="setDrawingObject('p')">{{ t('stationLayout.draw.platform')
                        }}</el-button>
                    <el-button :icon="EditPen" @click="setDrawingObject('a')">{{ t('stationLayout.draw.annotation')
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
                    :display-scale-y="layoutScaleY" @selected-annotation-change="handleSelectedAnnotationChange"
                    @selected-equipment-change="handleSelectedEquipmentChange" />
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
                        <el-form-item v-if="['signal', 'switch', 'insulationJoint'].includes(equipmentForm.kind)"
                            label="Type">
                            <el-input v-model="equipmentForm.type" />
                        </el-form-item>
                        <el-form-item v-if="equipmentForm.kind === 'signal'" label="Direction">
                            <el-select v-model="equipmentForm.direction">
                                <el-option label="e" value="e" />
                                <el-option label="w" value="w" />
                                <el-option label="s" value="s" />
                                <el-option label="d" value="d" />
                            </el-select>
                        </el-form-item>
                        <el-form-item v-if="['signal', 'switch', 'insulationJoint'].includes(equipmentForm.kind)"
                            label="BindingNodeID">
                            <el-input v-model="equipmentForm.bindingNodeID" />
                        </el-form-item>
                        <div v-if="['signal', 'switch', 'insulationJoint'].includes(equipmentForm.kind)"
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

.toolbar-checkbox-item:hover {
    background-color: transparent !important;
}

.toolbar-checkbox-item {
    cursor: default;
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

.toolbar-group-label {
    font-size: 12px;
    color: #909399;
    white-space: nowrap;
    font-weight: 500;
}

.scale-toolbar-group {
    flex-wrap: wrap;
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

@media (max-width: 960px) {
    .equipment-side-panel {
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
