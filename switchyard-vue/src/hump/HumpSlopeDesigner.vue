<template>
    <div class="container">
        <div class="side-menu-top">
            <div class="left-section">
                <el-button @click="toggleLeft" size="small" type="primary">工具</el-button>
            </div>
            <div class="center-section">
                <div class="control-group">
                    <span>纵断面方案</span>
                    <el-select v-model="selectedCondition" placeholder="选择计算条件" size="small" style="width: 150px;">
                        <el-option label="条件1" value="condition1"></el-option>
                        <el-option label="条件2" value="condition2"></el-option>
                        <el-option label="条件3" value="condition3"></el-option>
                    </el-select>
                </div>
                <div class="control-group">
                    <span>计算条件</span>
                    <el-select v-model="selectedCondition" placeholder="选择计算条件" size="small" style="width: 150px;">
                        <el-option label="条件1" value="condition1"></el-option>
                        <el-option label="条件2" value="condition2"></el-option>
                        <el-option label="条件3" value="condition3"></el-option>
                    </el-select>
                </div>
                <div class="control-group">
                    <span>初始动能高线</span>
                    <el-switch v-model="showInitialKinetic" size="small"></el-switch>
                </div>
                <div class="control-group">
                    <span>阻力能高线</span>
                    <el-switch v-model="showResistance" size="small"></el-switch>
                </div>
                <div class="control-group">
                    <span>动能高线</span>
                    <el-switch v-model="showKinetic" size="small"></el-switch>
                </div>
                <div class="control-group">
                    <span>制动能高线</span>
                    <el-switch v-model="showBreaking" size="small"></el-switch>
                </div>
                <div class="control-group" style="width: 100px;">
                    <span>X缩放</span>
                    <el-slider v-model="globalScaleX" :min="0.1" :max="5" :step="0.01"
                        style="display:inline; width: 150px;"></el-slider>
                </div>
                <div class="control-group" style="width: 100px;">
                    <span>Y缩放</span>
                    <el-slider v-model="globalScaleY" :min="5" :max="100" :step="0.1"
                        style="display:inline; width: 150px;"></el-slider>
                </div>
            </div>
            <div class="right-section">
                <el-button @click="toggleRight" size="small" type="primary">数据</el-button>
            </div>
        </div>
        <div class="condition-info">
            <span>推峰速度：{{ currentCalculateCondition.wagonVelocityOnTop }}m/s</span>
            <span>溜放部分溜放速度：{{ currentCalculateCondition.wagonVelocityOnSlop }}m/s</span>
            <span>调车场溜放速度：{{ currentCalculateCondition.wagonVelocityOnYard }}m/s</span>
            <span>风速：{{ currentCalculateCondition.windVelocity }}m/s（{{ currentCalculateCondition.isHeadWind ? '逆风' :
                '顺风' }}）</span>
            <span>空气密度：{{ currentCalculateCondition.airDensity }}kg/m³</span>
            <span>温度：{{ currentCalculateCondition.temperature }}°C</span>
        </div>
        <div class="main-ctrl">
            <HumpSlopeCtrl v-model:slope-layout="slopeLayout"
                :resistance-energy-height-data="resistanceEnergyHeightData"
                :kinetic-energy-height-data="kineticEnergyHeightData" :global-scale-x="globalScaleX"
                :global-scale-y="globalScaleY" :element-visibility="elementVisibility" :global-cursor-x="globalCursorX"
                @updateGlobalCursorX="updateGlobalCursorX" />
            <HumpSlopeSketchBlock v-model:slope-layout="slopeLayout" style="height:auto" :global-scale-x="globalScaleX"
                :global-cursor-x="globalCursorX" @updateGlobalCursorX="updateGlobalCursorX" />
            <HumpLayoutCtrl v-model:flat-layout="flatLayout" :is-toolbar-display="false" style="height:auto"
                :global-scale-x="globalScaleX" :global-cursor-x="globalCursorX"
                @update:global-cursor-x="updateGlobalCursorX" />
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
import { computed, onMounted, ref, watch } from 'vue';
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

function updateGlobalCursorX(value: number) {
    globalCursorX.value = value;
}

const resistanceEnergyHeightData = ref<{ x: number, height: number }[] | null>(null);
const kineticEnergyHeightData = ref<{ x: number, result: any }[] | null>(null);

const selectedCondition = ref('condition1');
const globalScaleX = ref(3.5);
const globalScaleY = ref(80);
const globalCursorX = ref(0);

const showInitialKinetic = ref(false);
const showResistance = ref(false);
const showKinetic = ref(false);
const showBreaking = ref(false);

const currentCalculateCondition = ref({
    wagonTypeName: "P70H",
    wagonVelocityOnTop: 1.4,
    wagonVelocityOnSlop: 5.2,
    wagonVelocityOnYard: 2.2,
    windVelocity: 5,
    isHeadWind: 1,
    airDensity: 0.063,
    temperature: -10,
    g: 9.8,
    retarderActivation: {},
    retarderOutput: {}
})

const currentCalculateConditionText = computed(() => {
    const c = currentCalculateCondition.value;
    return `车辆类型: ${c.wagonTypeName}\t推峰速度: ${c.wagonVelocityOnTop} m/s\t溜放速度:【溜放部分${c.wagonVelocityOnSlop} m/s, 调车场: ${c.wagonVelocityOnYard} m/s】\t风速: ${c.windVelocity} m/s（${c.isHeadWind ? '逆风' : '顺风'}）\t空气密度: ${c.airDensity} kg/m³\t温度: ${c.temperature} °C`;
});

watch(showInitialKinetic, (newVal) => {
    if (newVal === true) {
        loadKineticEnergyHeight();
    }
});

watch(showResistance, (newVal) => {
    if (newVal === true) {
        loadResistanceEnergyHeight();
    }
});

watch(showKinetic, (newVal) => {
    if (newVal === true) {
        loadKineticEnergyHeight();
    }
});

watch(showBreaking, (newVal) => {
    if (newVal === true) {
        loadBreakingEnergyHeight();
    }
});

const elementVisibility = computed(() => {
    return {
        initialKinetic: showInitialKinetic.value,
        resistance: showResistance.value,
        kinetic: showKinetic.value,
        breaking: showBreaking.value
    };
});

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

// 加载阻力能高线数据
function loadResistanceEnergyHeight() {
    axios.post(`${config.serverurl}/hump/getresistanceenergyheight`, {
        wagonTypeName: "P70H",
        wagonVelocityOnTop: 1.4,
        wagonVelocityOnSlop: 5.2,
        wagonVelocityOnYard: 2.2,
        windVelocity: 5,
        isHeadWind: 1,
        airDensity: 0.063,
        temperature: -10,
        g: 9.8,
        retarderActivation: {},
        retarderOutput: {}
    }).then(response => {
        if (response.data) {
            console.log('kinetic energy height data loaded:', response.data);
            resistanceEnergyHeightData.value = response.data as { x: number, height: number }[];
        }
    }).catch(error => {
        console.error("加载动能高度数据失败:", error);
    });
}

// 加载动能高线
function loadKineticEnergyHeight() {
    axios.post(`${config.serverurl}/hump/getkineticenergyheight`, {
        wagonTypeName: "P70H",
        wagonVelocityOnTop: 1.4,
        wagonVelocityOnSlop: 5.2,
        wagonVelocityOnYard: 2.2,
        windVelocity: 5,
        isHeadWind: 1,
        airDensity: 0.063,
        temperature: -10,
        g: 9.8,
        retarderActivation: {},
        retarderOutput: {}
    }).then(response => {
        if (response.data) {
            console.log('Resistance energy height data loaded:', response.data);
            kineticEnergyHeightData.value = response.data as { x: number, result: any }[];
        }
    }).catch(error => {
        console.error("加载阻力能高度数据失败:", error);
    });
}

function loadBreakingEnergyHeight() {

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
    height: 40px;
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
    display: flex;
    align-items: center;
    justify-content: center;
    gap: 12px;
    padding: 8px 16px;
    margin: 0 10px;
    border: 1px solid #dbe3f1;
    border-radius: 2px;
    background: linear-gradient(135deg, #f8fafc, #eef3ff);
    box-shadow: 0 2px 8px rgba(15, 23, 42, 0.08);
    height: 100%;
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
    opacity: 0.9;

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

.control-group {
    margin-right: 10px;
}

.control-group span {
    font-size: small;
    margin-right: 5px;
}

.condition-info {
    justify-content: center;
    margin-top: 5px;
    display: flex;
}

.condition-info span {
    font-weight: bold;
    color: #666;
    margin-right: 20px;
}
</style>