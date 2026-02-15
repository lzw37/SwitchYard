<template>
    <div class="container">
        <div class="side-menu-top">
            <div class="left-section">
                <el-button @click="toggleLeft" size="small" type="primary">{{ t('humpSlopeDesigner.tool') }}</el-button>
            </div>
            <div class="center-section">
                <div class="control-group">
                    <span>{{ t('humpSlopeDesigner.longitudinalSectionScheme') }}</span>
                    <el-select v-model="currentHumpSchemeID" :placeholder="t('humpSlopeDesigner.selectHumpScheme')"
                        size="small" style="width: 150px;">
                        <el-option v-for="scheme in humpSchemes" :key="scheme.id" :label="scheme.name"
                            :value="scheme.id" />
                    </el-select>
                    <el-button type="primary" size="small" @click="showSchemeManager = true">...</el-button>
                </div>
                <div class="control-group">
                    <span>{{ t('humpSlopeDesigner.calculationCondition') }}</span>
                    <el-select v-model="currentHumpCalculationID"
                        :placeholder="t('humpSlopeDesigner.selectCalculationCondition')" size="small"
                        style="width: 150px;">
                        <el-option v-for="calculation in humpCalculations" :key="calculation.id"
                            :label="getCalculationDisplayLabel(calculation)" :value="calculation.id" />
                    </el-select>
                    <el-button type="primary" size="small" @click="showConditionManager = true">...</el-button>
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

        <!-- 驼峰方案管理对话框 -->
        <el-dialog v-model="showSchemeManager" :title="t('humpSlopeDesigner.dialog.schemeManagement')" width="80%" :close-on-click-modal="false">
            <div style="margin-bottom: 16px;">
                <el-button type="primary" @click="handleAddScheme">{{ t('humpSlopeDesigner.buttons.addScheme') }}</el-button>
            </div>
            <el-table :data="humpSchemes" style="width: 100%" v-loading="tableLoading">
                <el-table-column prop="id" :label="t('humpSlopeDesigner.table.schemeId')" width="200"></el-table-column>
                <el-table-column prop="name" :label="t('humpSlopeDesigner.table.schemeName')">
                    <template #default="{ row, $index }">
                        <el-input v-if="editingIndex === $index" v-model="editingScheme.name" size="small" />
                        <span v-else>{{ row.name }}</span>
                    </template>
                </el-table-column>
                <el-table-column prop="instanceID" :label="t('humpSlopeDesigner.table.instanceId')" width="200"></el-table-column>
                <el-table-column :label="t('humpSlopeDesigner.table.operation')" width="200">
                    <template #default="{ row, $index }">
                        <div v-if="editingIndex === $index">
                            <el-button type="success" size="small" @click="handleSaveScheme">{{ t('humpSlopeDesigner.buttons.save') }}</el-button>
                            <el-button size="small" @click="handleCancelEdit">{{ t('humpSlopeDesigner.buttons.cancel') }}</el-button>
                        </div>
                        <div v-else>
                            <el-button type="primary" size="small" @click="handleEditScheme(row, $index)">{{ t('humpSlopeDesigner.buttons.edit') }}</el-button>
                            <el-button type="danger" size="small" @click="handleDeleteScheme(row)"
                                :disabled="humpSchemes.length <= 1">{{ t('humpSlopeDesigner.buttons.delete') }}</el-button>
                        </div>
                    </template>
                </el-table-column>
            </el-table>

            <template #footer>
                <el-button @click="showSchemeManager = false">{{ t('humpSlopeDesigner.dialog.close') }}</el-button>
            </template>
        </el-dialog>

        <!-- 计算条件管理对话框 -->
        <el-dialog v-model="showConditionManager" :title="t('humpSlopeDesigner.dialog.conditionManagement')" width="90%" :close-on-click-modal="false"
            @open="loadDropdownData">
            <div style="margin-bottom: 16px;">
                <el-button type="primary" @click="handleAddCalculation">{{ t('humpSlopeDesigner.buttons.addCondition') }}</el-button>
            </div>
            <el-table :data="humpCalculations" style="width: 100%" v-loading="calculationTableLoading">
                <el-table-column prop="id" :label="t('humpSlopeDesigner.table.id')" width="180"></el-table-column>
                <el-table-column prop="wagonType" :label="t('humpSlopeDesigner.table.wagonType')" width="120">
                    <template #default="{ row, $index }">
                        <el-select v-if="editingCalculationIndex === $index" v-model="editingCalculation.wagonType"
                            size="small" :placeholder="t('humpSlopeDesigner.placeholder.selectWagonType')">
                            <el-option v-for="wagon in wagonConcepts" :key="wagon.id || wagon.typeName"
                                :label="wagon.typeName" :value="wagon.typeName" />
                        </el-select>
                        <span v-else>{{ row.wagonType }}</span>
                    </template>
                </el-table-column>
                <el-table-column prop="operationConditionID" :label="t('humpSlopeDesigner.table.operationCondition')" width="150">
                    <template #default="{ row, $index }">
                        <el-select v-if="editingCalculationIndex === $index"
                            v-model="editingCalculation.operationConditionID" size="small" :placeholder="t('humpSlopeDesigner.placeholder.selectOperationCondition')">
                            <el-option v-for="condition in operationConditions" :key="condition.id"
                                :label="condition.name || condition.id" :value="condition.id" />
                        </el-select>
                        <span v-else>{{operationConditions.find(c => c.id === row.operationConditionID)?.name ||
                            row.operationConditionID}}</span>
                    </template>
                </el-table-column>
                <el-table-column prop="slopeLineID" :label="t('humpSlopeDesigner.table.slopeLine')" width="150">
                    <template #default="{ row, $index }">
                        <el-select v-if="editingCalculationIndex === $index" v-model="editingCalculation.slopeLineID"
                            size="small" :placeholder="t('humpSlopeDesigner.placeholder.selectSlopeLine')">
                            <el-option v-for="slopeLine in slopeLines" :key="slopeLine.id"
                                :label="slopeLine.name || slopeLine.id" :value="slopeLine.id" />
                        </el-select>
                        <span v-else>{{slopeLines.find(s => s.id === row.slopeLineID)?.name || row.slopeLineID
                        }}</span>
                    </template>
                </el-table-column>
                <el-table-column prop="humpSchemeID" :label="t('humpSlopeDesigner.table.humpScheme')" width="180">
                    <template #default="{ row, $index }">
                        <el-select v-if="editingCalculationIndex === $index" v-model="editingCalculation.humpSchemeID"
                            size="small" :placeholder="t('humpSlopeDesigner.placeholder.selectHumpScheme')">
                            <el-option v-for="scheme in humpSchemes" :key="scheme.id" :label="scheme.name"
                                :value="scheme.id" />
                        </el-select>
                        <span v-else>{{humpSchemes.find(s => s.id === row.humpSchemeID)?.name || row.humpSchemeID
                            }}</span>
                    </template>
                </el-table-column>
                <el-table-column :label="t('humpSlopeDesigner.table.operation')" width="200">
                    <template #default="{ row, $index }">
                        <div v-if="editingCalculationIndex === $index">
                            <el-button type="success" size="small" @click="handleSaveCalculation">{{ t('humpSlopeDesigner.buttons.save') }}</el-button>
                            <el-button size="small" @click="handleCancelCalculationEdit">{{ t('humpSlopeDesigner.buttons.cancel') }}</el-button>
                        </div>
                        <div v-else>
                            <el-button type="primary" size="small"
                                @click="handleEditCalculation(row, $index)">{{ t('humpSlopeDesigner.buttons.edit') }}</el-button>
                            <el-button type="danger" size="small" @click="handleDeleteCalculation(row)">{{ t('humpSlopeDesigner.buttons.delete') }}</el-button>
                        </div>
                    </template>
                </el-table-column>
            </el-table>

            <template #footer>
                <el-button @click="showConditionManager = false">{{ t('humpSlopeDesigner.dialog.close') }}</el-button>
            </template>
        </el-dialog>
    </div>
</template>
<script setup lang="ts">
import HumpSlopeCtrl from './HumpSlopeCtrl.vue';
import HumpSlopeSketchBlock from './HumpSlopeSketchBlock.vue';
import { computed, onMounted, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n'
import { ElMessageBox } from 'element-plus'
import HumpLayoutCtrl from './HumpLayoutCtrl.vue';
import axios from '@/utils/axios';
import config from '../config.json';
import { FlatLayout, SlopeLayout, CurveDirections } from './humplayoutctrl';

// 定义 props
interface Props {
    selectedInstanceId?: string | null
}
const props = withDefaults(defineProps<Props>(), {
    selectedInstanceId: null
})

// HumpScheme 接口
interface HumpScheme {
    id: string
    instanceID: string
    name: string
}

// HumpCalculation 接口
interface HumpCalculation {
    id: string
    instanceID: string
    humpSchemeID: string
    wagonType: string
    operationConditionID: string
    slopeLineID: string
    data: any // 对应后端的 HumpCalculationData
}

const humpSchemes = ref<HumpScheme[]>([])
const currentHumpSchemeID = ref("");

const humpCalculations = ref<HumpCalculation[]>([])
const currentHumpCalculationID = ref("")

// 下拉菜单选项数据
const wagonConcepts = ref<any[]>([])
const operationConditions = ref<any[]>([])
const slopeLines = ref<any[]>([])

// 生成计算条件显示标签
const getCalculationDisplayLabel = (calculation: HumpCalculation) => {
    const wagonType = wagonConcepts.value.find(w => w.typeName === calculation.wagonType)?.typeName || calculation.wagonType
    const operationCondition = operationConditions.value.find(c => c.id === calculation.operationConditionID)?.name || calculation.operationConditionID
    const slopeLine = slopeLines.value.find(s => s.id === calculation.slopeLineID)?.name || calculation.slopeLineID
    return `${wagonType}-${operationCondition}-${slopeLine}`
}

// 方案管理相关状态
const showSchemeManager = ref(false)
const showConditionManager = ref(false)
const tableLoading = ref(false)
const editingIndex = ref(-1)
const editingScheme = ref<HumpScheme>({ id: '', instanceID: '', name: '' })

// 计算条件管理相关状态
const calculationTableLoading = ref(false)
const editingCalculationIndex = ref(-1)
const editingCalculation = ref<HumpCalculation>({
    id: '',
    instanceID: '',
    humpSchemeID: '',
    wagonType: '',
    operationConditionID: '',
    slopeLineID: '',
    data: {}
})

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
const loadSlopeLayout = async () => {
    if (!props.selectedInstanceId || !currentHumpSchemeID.value) {
        slopeLayout.value = null
        return
    }

    try {
        const response = await axios.get('/Hump/GetSlopeLayout', {
            params: {
                instanceID: props.selectedInstanceId,
                humpSchemeID: currentHumpSchemeID.value
            }
        })
        if (response.data) {
            slopeLayout.value = response.data as SlopeLayout
            console.log('Slope layout loaded:', slopeLayout.value)
        }
    } catch (error) {
        console.error('加载纵断面设计数据失败:', error)
        slopeLayout.value = null
    }
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

// 加载下拉菜单选项数据
const loadWagonConcepts = async () => {
    if (!props.selectedInstanceId) {
        wagonConcepts.value = []
        return
    }

    try {
        const response = await axios.get('/Hump/GetWagonConcept', {
            params: { instanceID: props.selectedInstanceId }
        })
        wagonConcepts.value = response.data || []
        console.log('Wagon concepts loaded:', wagonConcepts.value)
    } catch (error) {
        console.error('加载车辆概念失败:', error)
        wagonConcepts.value = []
    }
}

const loadOperationConditions = async () => {
    if (!props.selectedInstanceId) {
        operationConditions.value = []
        return
    }

    try {
        const response = await axios.get('/Hump/GetOperationConditions', {
            params: { instanceID: props.selectedInstanceId }
        })
        operationConditions.value = response.data || []
        console.log('Operation conditions loaded:', operationConditions.value)
    } catch (error) {
        console.error('加载运行条件失败:', error)
        operationConditions.value = []
    }
}

const loadSlopeLines = async () => {
    if (!props.selectedInstanceId) {
        slopeLines.value = []
        return
    }

    try {
        const response = await axios.get('/Hump/GetSlopeLines', {
            params: { instanceID: props.selectedInstanceId }
        })
        slopeLines.value = response.data || []
        console.log('Slope lines loaded:', slopeLines.value)
    } catch (error) {
        console.error('加载溜放线失败:', error)
        slopeLines.value = []
    }
}

// 加载驼峰计算条件数据
const loadHumpCalculations = async () => {
    if (!props.selectedInstanceId || !currentHumpSchemeID.value) {
        humpCalculations.value = []
        currentHumpCalculationID.value = ""
        return
    }

    try {
        // 同时加载下拉菜单数据以支持显示标签
        await Promise.all([
            loadWagonConcepts(),
            loadOperationConditions(),
            loadSlopeLines()
        ])

        const response = await axios.get('/Hump/GetHumpCalculations', {
            params: {
                instanceID: props.selectedInstanceId,
                humpSchemeID: currentHumpSchemeID.value
            }
        })
        humpCalculations.value = response.data || []

        // 如果有计算条件数据，默认选择第一个
        if (humpCalculations.value.length > 0 && humpCalculations.value[0]) {
            currentHumpCalculationID.value = humpCalculations.value[0].id
        } else {
            currentHumpCalculationID.value = ""
        }

        console.log('Hump calculations loaded:', humpCalculations.value)
    } catch (error) {
        console.error('加载驼峰计算条件失败:', error)
        humpCalculations.value = []
        currentHumpCalculationID.value = ""
    }
}

// 加载驼峰方案数据
const loadHumpSchemes = async () => {
    if (!props.selectedInstanceId) {
        humpSchemes.value = []
        currentHumpSchemeID.value = ""
        return
    }

    try {
        const response = await axios.get('/Hump/GetHumpSchemes', {
            params: { instanceID: props.selectedInstanceId }
        })
        humpSchemes.value = response.data || []

        // 如果有方案数据，默认选择第一个
        if (humpSchemes.value.length > 0 && humpSchemes.value[0]) {
            currentHumpSchemeID.value = humpSchemes.value[0].id
        } else {
            currentHumpSchemeID.value = ""
        }

        console.log('Hump schemes loaded:', humpSchemes.value)
    } catch (error) {
        console.error('加载驼峰方案失败:', error)
        humpSchemes.value = []
        currentHumpSchemeID.value = ""
    }
}

// 监听 selectedInstanceId 变化
watch(() => props.selectedInstanceId, (newInstanceId) => {
    console.log('Selected instance changed:', newInstanceId)
    loadHumpSchemes()
}, { immediate: true })

// 监听 currentHumpSchemeID 变化
watch(currentHumpSchemeID, (newSchemeId, oldSchemeId) => {
    console.log('Current hump scheme changed from', oldSchemeId, 'to', newSchemeId)
    if (newSchemeId && props.selectedInstanceId) {
        loadSlopeLayout()
        loadFlatLayout()
        loadHumpCalculations()
    }
})

// 加载平面布置图数据 
const loadFlatLayout = async () => {
    if (!props.selectedInstanceId) {
        flatLayout.value = null
        return
    }

    try {
        // 首先获取该实例的所有溜放线
        const slopeLinesResponse = await axios.get('/Hump/GetSlopeLines', {
            params: { instanceID: props.selectedInstanceId }
        })
        const slopeLines = slopeLinesResponse.data || []

        // 如果有溜放线，使用第一条线获取平面图
        if (slopeLines.length > 0) {
            const slopeLineID = slopeLines[0].id
            const response = await axios.get('/Hump/GetFlatLayout', {
                params: {
                    instanceID: props.selectedInstanceId,
                    slopeLineID: slopeLineID
                }
            })

            if (response.data) {
                flatLayout.value = response.data
                if (flatLayout.value?.positionSegmentList) {
                    flatLayout.value.positionSegmentList.forEach(seg => {
                        if (seg.curveDegree === 0) {
                            seg.curveDirection = CurveDirections.None
                        }
                    })
                }
                console.log('Flat layout loaded:', flatLayout.value)
            }
        } else {
            console.warn('No slope lines found for instance:', props.selectedInstanceId)
            flatLayout.value = null
        }
    } catch (error) {
        console.error('加载平面展开图数据失败:', error)
        flatLayout.value = null
    }
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
    loadHumpSchemes();
});

// 方案管理相关方法
const handleAddScheme = async () => {
    if (!props.selectedInstanceId) {
        console.error('No selected instance')
        return
    }

    const newScheme: HumpScheme = {
        id: '', // 后端会生成
        instanceID: props.selectedInstanceId,
        name: '新方案'
    }

    try {
        tableLoading.value = true
        const response = await axios.post('/Hump/CreateHumpScheme', newScheme)
        if (response.data) {
            await loadHumpSchemes() // 重新加载列表
            console.log('方案创建成功')
        }
    } catch (error) {
        console.error('创建方案失败:', error)
    } finally {
        tableLoading.value = false
    }
}

const handleEditScheme = (scheme: HumpScheme, index: number) => {
    editingIndex.value = index
    editingScheme.value = { ...scheme }
}

const handleSaveScheme = async () => {
    if (!editingScheme.value.name.trim()) {
        console.error('方案名称不能为空')
        return
    }

    try {
        tableLoading.value = true
        const response = await axios.put('/Hump/EditHumpScheme', editingScheme.value)
        if (response.status === 200) {
            await loadHumpSchemes() // 重新加载列表
            handleCancelEdit()
            console.log('方案更新成功')
        }
    } catch (error) {
        console.error('更新方案失败:', error)
    } finally {
        tableLoading.value = false
    }
}

const handleDeleteScheme = async (scheme: HumpScheme) => {
    if (humpSchemes.value.length <= 1) {
        console.error('至少需要保留一个方案')
        return
    }

    try {
        await ElMessageBox.confirm(
            t('humpSlopeDesigner.messages.deleteSchemeConfirm', { name: scheme.name }),
            t('humpSlopeDesigner.buttons.deleteConfirm'),
            {
                confirmButtonText: t('humpSlopeDesigner.buttons.confirm'),
                cancelButtonText: t('humpSlopeDesigner.buttons.cancel'),
                type: 'warning',
            }
        )

        tableLoading.value = true
        const response = await axios.delete(`/Hump/DeleteHumpScheme?id=${scheme.id}`)
        if (response.status === 200) {
            await loadHumpSchemes() // 重新加载列表
            console.log('方案删除成功')
        }
    } catch (error) {
        if (error === 'cancel') {
            console.log('取消删除')
        } else {
            console.error('删除方案失败:', error)
        }
    } finally {
        tableLoading.value = false
    }
}

const handleCancelEdit = () => {
    editingIndex.value = -1
    editingScheme.value = { id: '', instanceID: '', name: '' }
}

// 计算条件管理相关方法
const handleAddCalculation = async () => {
    if (!props.selectedInstanceId || !currentHumpSchemeID.value) {
        console.error('No selected instance or hump scheme')
        return
    }

    // 加载下拉菜单选项数据
    await Promise.all([
        loadWagonConcepts(),
        loadOperationConditions(),
        loadSlopeLines()
    ])

    const newCalculation: HumpCalculation = {
        id: '', // 后端会生成
        instanceID: props.selectedInstanceId,
        humpSchemeID: currentHumpSchemeID.value,
        wagonType: wagonConcepts.value.length > 0 ? (wagonConcepts.value[0].typeName || 'P70H') : 'P70H',
        operationConditionID: operationConditions.value.length > 0 ? operationConditions.value[0].id : 'default',
        slopeLineID: slopeLines.value.length > 0 ? slopeLines.value[0].id : 'default',
        data: {} // 提供必需的 Data 字段
    }

    try {
        calculationTableLoading.value = true
        const response = await axios.post('/Hump/CreateHumpCalculation', newCalculation)
        if (response.data) {
            await loadHumpCalculations() // 重新加载列表
            console.log('计算条件创建成功')
        }
    } catch (error) {
        console.error('创建计算条件失败:', error)
    } finally {
        calculationTableLoading.value = false
    }
}

const handleEditCalculation = async (calculation: HumpCalculation, index: number) => {
    // 加载下拉菜单选项数据
    await Promise.all([
        loadWagonConcepts(),
        loadOperationConditions(),
        loadSlopeLines()
    ])

    editingCalculationIndex.value = index
    editingCalculation.value = { ...calculation }
}

const handleSaveCalculation = async () => {
    if (!editingCalculation.value.wagonType.trim()) {
        console.error('车辆类型不能为空')
        return
    }

    try {
        calculationTableLoading.value = true
        const response = await axios.put('/Hump/EditHumpCalculation', editingCalculation.value)
        if (response.status === 200) {
            await loadHumpCalculations() // 重新加载列表
            handleCancelCalculationEdit()
            console.log('计算条件更新成功')
        }
    } catch (error) {
        console.error('更新计算条件失败:', error)
    } finally {
        calculationTableLoading.value = false
    }
}

const handleDeleteCalculation = async (calculation: HumpCalculation) => {
    try {
        await ElMessageBox.confirm(
            t('humpSlopeDesigner.messages.deleteConditionConfirm', { name: `${calculation.wagonType} - ${calculation.operationConditionID}` }),
            t('humpSlopeDesigner.buttons.deleteConfirm'),
            {
                confirmButtonText: t('humpSlopeDesigner.buttons.confirm'),
                cancelButtonText: t('humpSlopeDesigner.buttons.cancel'),
                type: 'warning',
            }
        )

        calculationTableLoading.value = true
        const response = await axios.delete('/Hump/DeleteHumpCalculation', {
            params: {
                instanceID: calculation.instanceID,
                humpSchemeID: calculation.humpSchemeID,
                id: calculation.id
            }
        })
        if (response.status === 200) {
            await loadHumpCalculations() // 重新加载列表
            console.log('计算条件删除成功')
        }
    } catch (error) {
        if (error === 'cancel') {
            console.log('取消删除')
        } else {
            console.error('删除计算条件失败:', error)
        }
    } finally {
        calculationTableLoading.value = false
    }
}

// 加载下拉菜单数据的统一函数
const loadDropdownData = async () => {
    await Promise.all([
        loadWagonConcepts(),
        loadOperationConditions(),
        loadSlopeLines()
    ])
}

const handleCancelCalculationEdit = () => {
    editingCalculationIndex.value = -1
    editingCalculation.value = {
        id: '',
        instanceID: '',
        humpSchemeID: '',
        wagonType: '',
        operationConditionID: '',
        slopeLineID: '',
        data: {}
    }
}

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