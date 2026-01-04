<template>
    <div class="container">
        <div class="side-menu-top">
            <div class="left-section">
                <el-button @click="toggleLeft" size="small" type="primary">工具</el-button>
            </div>
            <div class="center-section">
                TOP MENU
            </div>
            <div class="right-section">
                <el-button @click="toggleRight" size="small" type="primary">数据</el-button>
            </div>
        </div>
        <div class="main-ctrl">
            <HumpSlopeCtrl v-model:slope-layout="slopeLayout" />
            <HumpSlopeSketchBlock v-model:slope-layout="slopeLayout" style="height:auto" />
            <HumpLayoutCtrl v-model:flat-layout="flatLayout" :is-toolbar-display="false" style="height:auto" />
        </div>
        <div class="side-menu-left" v-show="leftVisible">
            LEFT SIDE MENU
        </div>
        <div class="side-menu-right" v-show="rightVisible">
            <div class="side-menu-container">
                <el-tabs v-model="activeTab" @tab-click="handleTabClick">
                    <el-tab-pane label="位置点" name="vposition">
                        <el-table :data="slopeLayout?.positionList || []" style="width: 100%">
                            <el-table-column prop="id" label="ID" width="100"></el-table-column>
                            <el-table-column prop="x" label="位置X/m" width="100"></el-table-column>
                            <el-table-column prop="height" label="高度/m" width="120"></el-table-column>
                        </el-table>
                    </el-tab-pane>
                    <el-tab-pane label="位置区间" name="vpositionsegment">
                        <el-table :data="slopeLayout?.positionSegmentList || []" style="width: 100%">
                            <el-table-column prop="id" label="ID" width="100"></el-table-column>
                            <el-table-column prop="startPositionID" label="起始位置ID" width="120"></el-table-column>
                            <el-table-column prop="endPositionID" label="结束位置ID" width="120"></el-table-column>
                            <el-table-column prop="length" label="长度/m" width="100"></el-table-column>
                            <el-table-column prop="gradient" label="坡度/‰" width="120"></el-table-column>
                            <el-table-column prop="height" label="高度/m" width="120"></el-table-column>
                        </el-table>
                    </el-tab-pane>
                </el-tabs>
            </div>
        </div>
        <div class="side-menu-bottom">BOTTOM MENU</div>
    </div>
</template>
<script setup lang="ts">
import HumpSlopeCtrl from './HumpSlopeCtrl.vue';
import HumpSlopeSketchBlock from './HumpSlopeSketchBlock.vue';
import { onMounted, ref } from 'vue';
import HumpLayoutCtrl from './HumpLayoutCtrl.vue';
import axios from 'axios';
import config from '../config.json';
import { FlatLayout, SlopeLayout, CurveDirections } from './humplayoutctrl';

const slopeLayout = ref<SlopeLayout | null>(null);
const flatLayout = ref<FlatLayout | null>(null);
const activeTab = ref('vposition');
const leftVisible = ref(false);
const rightVisible = ref(false);

const globalLeftMargin = ref(0);
const globalScaleX = ref(1);

// 加载纵断面设计数据
function loadSlopeLayout() {
    axios.get(`${config.serverurl}/hump/getslopelayout`).then(response => {
        if (response.data) {
            slopeLayout.value = response.data as SlopeLayout;
        }
    }).catch(error => {
        console.error("加载纵断面设计数据失败:", error);
    });
}

function loadFlatLayout() {
    axios.get(`${config.serverurl}/hump/getflatlayout`).then(response => {
        if (response.data) {
            flatLayout.value = response.data
            if (flatLayout.value?.positionSegmentList) {
                flatLayout.value.positionSegmentList.forEach(seg => {
                    if (seg.curveDegree === 0) {
                        seg.curveDirection = CurveDirections.None
                    }
                })
            }
            console.log('Flat layout data loaded:', flatLayout.value)
        }
    }).catch(error => {
        console.error("加载平面展开图数据失败:", error);
    });
}

function handleTabClick(tab: any) {
    // 可以添加切换tab的逻辑
}

function toggleLeft() {
    leftVisible.value = !leftVisible.value;
}

function toggleRight() {
    rightVisible.value = !rightVisible.value;
}

onMounted(() => {
    loadSlopeLayout();
    loadFlatLayout();
});

</script>
<style scoped lang="css">
.container {
    position: relative;
    height: 100vh;
    display: flex;
    flex-direction: column;
}

.side-menu-top {
    height: 50px;
    background-color: #f0f0f0;
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 0 10px;
}

.left-section,
.right-section {
    flex: 0 0 auto;
}

.center-section {
    flex: 1;
    text-align: center;
}

.main-ctrl {
    flex: 1;
    background-color: #ffffff;
    position: relative;
    /* display: flex; */
}

.side-menu-left {
    position: absolute;
    top: 50px;
    left: 0;
    width: 200px;
    height: calc(100vh - 100px);
    background-color: #e0e0e0;
    z-index: 10;
}

.side-menu-right {
    position: absolute;
    top: 50px;
    right: 0;
    width: 500px;
    height: calc(100vh - 100px);
    background-color: white;
    z-index: 10;
}

.side-menu-bottom {
    height: 50px;
    background-color: white;
}

.side-menu-container {
    margin: 5px;
    padding: 10px;
    height: 100%;
    box-sizing: border-box;
    overflow-y: auto;
}
</style>