<template>
    <div class="container">
        <div class="side-menu-top">
            <div class="left-section">
                <el-button @click="toggleLeft" size="small" type="primary">{{ t('humpSlopeDesigner.tool') }}</el-button>
            </div>
            <div class="center-section">
                <div class="control-group">
                    <span>{{ t('humpSlopeDesigner.longitudinalSectionScheme') }}</span>
                    <el-select v-model="selectedCondition"
                        :placeholder="t('humpSlopeDesigner.selectCalculationCondition')" size="small"
                        style="width: 150px;">
                        <el-option :label="t('humpSlopeDesigner.condition1')" value="condition1"></el-option>
                        <el-option :label="t('humpSlopeDesigner.condition2')" value="condition2"></el-option>
                        <el-option :label="t('humpSlopeDesigner.condition3')" value="condition3"></el-option>
                    </el-select>
                </div>
                <div class="control-group">
                    <span>{{ t('humpSlopeDesigner.calculationCondition') }}</span>
                    <el-select v-model="selectedCondition"
                        :placeholder="t('humpSlopeDesigner.selectCalculationCondition')" size="small"
                        style="width: 150px;">
                        <el-option :label="t('humpSlopeDesigner.condition1')" value="condition1"></el-option>
                        <el-option :label="t('humpSlopeDesigner.condition2')" value="condition2"></el-option>
                        <el-option :label="t('humpSlopeDesigner.condition3')" value="condition3"></el-option>
                    </el-select>
                </div>
                <div class="control-group">
                    <span>{{ t('humpSlopeDesigner.initialKineticEnergyLine') }}</span>
                    <el-switch v-model="showInitialKinetic" size="small"></el-switch>
                </div>
                <div class="control-group">
                    <span>{{ t('humpSlopeDesigner.resistanceEnergyLine') }}</span>
                    <el-switch v-model="showResistance" size="small"></el-switch>
                </div>
                <div class="control-group">
                    <span>{{ t('humpSlopeDesigner.kineticEnergyLine') }}</span>
                    <el-switch v-model="showKinetic" size="small"></el-switch>
                </div>
                <div class="control-group">
                    <span>{{ t('humpSlopeDesigner.brakingEnergyLine') }}</span>
                    <el-switch v-model="showBreaking" size="small"></el-switch>
                </div>
                <div class="control-group" style="width: 100px;">
                    <span>{{ t('humpSlopeDesigner.xScale') }}</span>
                    <el-slider v-model="globalScaleX" :min="0.1" :max="5" :step="0.01"
                        style="display:inline; width: 150px;"></el-slider>
                </div>
                <div class="control-group" style="width: 100px;">
                    <span>{{ t('humpSlopeDesigner.yScale') }}</span>
                    <el-slider v-model="globalScaleY" :min="5" :max="100" :step="0.1"
                        style="display:inline; width: 150px;"></el-slider>
                </div>
            </div>
            <div class="right-section">
                <el-button @click="toggleRight" size="small" type="primary">{{ t('humpSlopeDesigner.data')
                    }}</el-button>
            </div>
        </div>
        <div class="condition-info">
            <span>{{ t('humpSlopeDesigner.humpVelocity') }}{{ currentCalculateCondition.wagonVelocityOnTop }}m/s</span>
            <span>{{ t('humpSlopeDesigner.slopeVelocity') }}{{ currentCalculateCondition.wagonVelocityOnSlop
                }}m/s</span>
            <span>{{ t('humpSlopeDesigner.yardVelocity') }}{{ currentCalculateCondition.wagonVelocityOnYard }}m/s</span>
            <span>{{ t('humpSlopeDesigner.windSpeed') }}{{ currentCalculateCondition.windVelocity }}m/s（{{
                currentCalculateCondition.isHeadWind ? t('humpSlopeDesigner.headWind') :
                    t('humpSlopeDesigner.tailWind') }}）</span>
            <span>{{ t('humpSlopeDesigner.airDensity') }}{{ currentCalculateCondition.airDensity }}kg/m³</span>
            <span>{{ t('humpSlopeDesigner.temperature') }}{{ currentCalculateCondition.temperature }}°C</span>
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
                    <el-tab-pane :label="t('humpSlopeDesigner.positionPoints')" name="vposition">
                        <el-table :data="slopeLayout?.positionList || []" style="width: 100%">
                            <el-table-column prop="id" :label="t('humpSlopeDesigner.id')" width="100"></el-table-column>
                            <el-table-column prop="x" :label="t('humpSlopeDesigner.positionX')"
                                width="100"></el-table-column>
                            <el-table-column prop="height" :label="t('humpSlopeDesigner.height')"
                                width="120"></el-table-column>
                        </el-table>
                    </el-tab-pane>
                    <el-tab-pane :label="t('humpSlopeDesigner.positionSegments')" name="vpositionsegment">
                        <el-table :data="slopeLayout?.positionSegmentList || []" style="width: 100%">
                            <el-table-column prop="id" :label="t('humpSlopeDesigner.id')" width="100"></el-table-column>
                            <el-table-column prop="startPositionID" :label="t('humpSlopeDesigner.startPositionID')"
                                width="120"></el-table-column>
                            <el-table-column prop="endPositionID" :label="t('humpSlopeDesigner.endPositionID')"
                                width="120"></el-table-column>
                            <el-table-column prop="length" :label="t('humpSlopeDesigner.length')"
                                width="100"></el-table-column>
                            <el-table-column prop="gradient" :label="t('humpSlopeDesigner.gradient')"
                                width="120"></el-table-column>
                            <el-table-column prop="height" :label="t('humpSlopeDesigner.height')"
                                width="120"></el-table-column>
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
import { useI18n } from 'vue-i18n'
import HumpLayoutCtrl from './HumpLayoutCtrl.vue';
import axios from '@/utils/axios';
import config from '../config.json';
import { FlatLayout, SlopeLayout, CurveDirections } from './humplayoutctrl';

const slopeLayout = ref<SlopeLayout | null>(null);
const flatLayout = ref<FlatLayout | null>(null);
const activeTab = ref('vposition');
const leftVisible = ref(false);
const rightVisible = ref(false);

const { t } = useI18n()

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
    return `${t('humpSlopeDesigner.wagonType')} ${c.wagonTypeName}\t${t('humpSlopeDesigner.humpVelocityLabel')} ${c.wagonVelocityOnTop} m/s\t${t('humpSlopeDesigner.slopeVelocityLabel')}【${t('humpSlopeDesigner.slopePart')}${c.wagonVelocityOnSlop} m/s, ${t('humpSlopeDesigner.yard')}${c.wagonVelocityOnYard} m/s】\t${t('humpSlopeDesigner.windSpeedLabel')} ${c.windVelocity} m/s（${c.isHeadWind ? t('humpSlopeDesigner.headWind') : t('humpSlopeDesigner.tailWind')}）\t${t('humpSlopeDesigner.airDensityLabel')} ${c.airDensity} kg/m³\t${t('humpSlopeDesigner.temperatureLabel')} ${c.temperature} °C`;
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
    font-weight: 600;
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