<script setup>
import { ref } from "vue";
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
const stationLayoutEditorRef = ref(null);
const extractDwgDialogVisible = ref(false);
const dwgFileInputRef = ref(null);
const selectedDwgFile = ref(null);
const dwgLayerName = ref("0");
const extractingDwg = ref(false);

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
function saveData() {
    var dataStr = stationLayoutEditorRef.value?.buildJsonData();
    axios
        .post("/StationLayout/SaveJson", {
            json: dataStr,
        })
        .then((res) => {
            console.log(res);
            alert(t('stationLayout.messages.saveSuccess') + res.data);
        })
        .catch((err) => {
            // alert(err);
            var serverMsg = "";
            if (err.response != undefined) {
                serverMsg = err.response.data;
            }
            alert(t('stationLayout.messages.saveFailed') + err + "\r\n" + serverMsg);
        });
}
function getData() {
    axios
        .post("/StationLayout/GetJson")
        .then((res) => {
            stationLayoutEditorRef.value?.loadDataFromJson(res.data);
        })
        .catch((err) => {
            var serverMsg = "";
            if (err.response != undefined) {
                serverMsg = err.response.data;
            }
            alert(t('stationLayout.messages.loadFailed') + err + "\r\n" + serverMsg);
        });
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
            ElMessage.success(`DWG 提取完成，共生成 ${res.data?.segmentCount || 0} 条线段`);
            getData();
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
</script>

<template>
    <div style="max-width: 100%; overflow: hidden;">
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
                </el-button-group>
            </div>
        </div>
        <StationLayoutEditor ref="stationLayoutEditorRef" />
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

.dwg-extract-form {
    display: grid;
    gap: 10px;
}

.dwg-extract-label {
    font-size: 13px;
    color: #606266;
    font-weight: 500;
}
</style>
