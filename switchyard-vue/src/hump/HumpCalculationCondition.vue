<template>
    <div class="calculation-condition-container">
        <div class="condition-card">
            <div class="card-header">
                <div style="display: flex; align-items: center; gap: 10px;">
                    <span style="font-weight: bold; min-width: 80px;">{{ t('hump.calcCondition.labels.condition')
                        }}:</span>
                    <el-select v-model="currentConditionId" style="width: 250px" @change="onConditionChange"
                        :placeholder="t('hump.calcCondition.placeholders.chooseCondition')" clearable>
                        <el-option v-for="condition in conditionsList" :key="condition.id" :label="condition.name"
                            :value="condition.id" />
                    </el-select>
                </div>

                <div class="header-buttons">
                    <el-button type="primary" size="small" @click="createNewCondition">
                        {{ t("hump.calcCondition.new") }}
                    </el-button>
                    <el-button type="primary" size="small" @click="saveCondition" :disabled="!currentInstanceId">
                        {{ currentConditionId ? t("hump.calcCondition.update") : t("hump.calcCondition.save") }}
                    </el-button>
                    <el-button size="small" @click="resetForm">
                        {{ t("hump.calcCondition.reset") }}
                    </el-button>
                    <el-button type="danger" size="small" @click="deleteCondition" :disabled="!currentConditionId">
                        {{ t("hump.calcCondition.delete") }}
                    </el-button>
                </div>
            </div>

            <el-form :model="formData" label-width="140px" ref="formRef">
                <div class="form-sections">
                    <!-- 第1列: 概况、车辆信息、速度参数 -->
                    <div class="form-column">
                        <el-divider content-position="left">{{
                            t("hump.calcCondition.sections.overview")
                            }}</el-divider>
                        <el-form-item :label="t('hump.calcCondition.labels.conditionName')" prop="name">
                            <el-input v-model="formData.name"
                                :placeholder="t('hump.calcCondition.placeholders.conditionName')"
                                style="width: 300px" />
                        </el-form-item>
                        <el-divider content-position="left">{{
                            t("hump.calcCondition.sections.speed")
                            }}</el-divider>
                        <el-form-item :label="t('hump.calcCondition.labels.peakVelocity')" prop="wagonVelocityOnTop">
                            <div style="display: flex; align-items: center">
                                <el-input-number v-model="formData.wagonVelocityOnTop" :min="0" :step="0.1"
                                    :precision="2" style="width: 300px" />
                                <span style="margin-left: 8px; color: #666; font-size: 14px">{{ t("units.m_s") }}</span>
                            </div>
                        </el-form-item>
                        <el-form-item :label="t('hump.calcCondition.labels.slopeAvgVelocity')"
                            prop="wagonVelocityOnSlope">
                            <div style="display: flex; align-items: center">
                                <el-input-number v-model="formData.wagonVelocityOnSlope" :min="0" :step="0.1"
                                    :precision="2" style="width: 300px" />
                                <span style="margin-left: 8px; color: #666; font-size: 14px">{{ t("units.m_s") }}</span>
                            </div>
                        </el-form-item>
                        <el-form-item :label="t('hump.calcCondition.labels.yardAvgVelocity')"
                            prop="wagonVelocityOnYard">
                            <div style="display: flex; align-items: center">
                                <el-input-number v-model="formData.wagonVelocityOnYard" :min="0" :step="0.1"
                                    :precision="2" style="width: 300px" />
                                <span style="margin-left: 8px; color: #666; font-size: 14px">{{ t("units.m_s") }}</span>
                            </div>
                        </el-form-item>
                    </div>

                    <!-- 第2列: 风速信息、环境参数、物理参数 -->
                    <div class="form-column">
                        <el-divider content-position="left">{{
                            t("hump.calcCondition.sections.wind")
                            }}</el-divider>
                        <el-form-item :label="t('hump.calcCondition.labels.windVelocity')" prop="windVelocity">
                            <div style="display: flex; align-items: center">
                                <el-input-number v-model="formData.windVelocity" :min="0" :step="0.1" :precision="2"
                                    style="width: 300px" />
                                <span style="margin-left: 8px; color: #666; font-size: 14px">{{ t("units.m_s") }}</span>
                            </div>
                        </el-form-item>
                        <el-form-item :label="t('hump.calcCondition.labels.windDirection')" prop="isHeadWind">
                            <el-select v-model="formData.isHeadWind" style="width: 300px">
                                <el-option :label="t('hump.calcCondition.options.headwind')" :value="1" />
                                <el-option :label="t('hump.calcCondition.options.tailwind')" :value="0" />
                            </el-select>
                        </el-form-item>

                        <el-divider content-position="left">{{
                            t("hump.calcCondition.sections.environment")
                            }}</el-divider>
                        <el-form-item :label="t('hump.calcCondition.labels.airDensity')" prop="airDensity">
                            <div style="display: flex; align-items: center">
                                <el-input-number v-model="formData.airDensity" :min="0" :step="0.001" :precision="4"
                                    style="width: 300px" />
                                <span style="margin-left: 8px; color: #666; font-size: 14px">{{ t("units.kg_m3")
                                    }}</span>
                            </div>
                        </el-form-item>
                        <el-form-item :label="t('hump.calcCondition.labels.temperature')" prop="temperature">
                            <div style="display: flex; align-items: center">
                                <el-input-number v-model="formData.temperature" :step="1" :precision="0"
                                    style="width: 300px" />
                                <span style="margin-left: 8px; color: #666; font-size: 14px">{{ t("units.deg_c")
                                    }}</span>
                            </div>
                        </el-form-item>
                    </div>
                </div>
            </el-form>
        </div>
    </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from "vue";
import { ElMessage, ElMessageBox } from "element-plus";
import { useI18n } from "vue-i18n";
import axios from "../utils/axios";

interface Props {
    selectedInstanceId?: string | null
}

const props = withDefaults(defineProps<Props>(), {
    selectedInstanceId: null
})

// API模型接口
interface OperationCondition {
    instanceID?: string;
    id?: string;
    name: string;
    wagonVelocityOnTop: number;
    wagonVelocityOnSlope: number;
    wagonVelocityOnYard: number;
    windVelocity: number;
    isHeadWind: number;
    airDensity: number;
    temperature: number;
}

// 扩展的本地表单数据接口
interface CalculationCondition extends OperationCondition {
    wagonTypeName: string;
    g: number;
    retarderActivation: Record<string, any>;
    retarderOutput: Record<string, any>;
}

interface HumpInstance {
    ID: string;
    Name: string;
    Owner: string;
    CreatedDate: string;
    IsActive: number;
}

const formRef = ref();
const { t } = useI18n();

// 状态管理
// const instanceList = ref<HumpInstance[]>([]);
const conditionsList = ref<OperationCondition[]>([]);
const currentInstanceId = ref<string>("");
const currentConditionId = ref<string>("");
const loading = ref(false);

// 表单数据
const formData = ref<CalculationCondition>({
    name: t("hump.calcCondition.defaults.conditionName"),
    wagonTypeName: "P70H",
    wagonVelocityOnTop: 1.4,
    wagonVelocityOnSlope: 5.2,
    wagonVelocityOnYard: 2.2,
    windVelocity: 5,
    isHeadWind: 1,
    airDensity: 0.125,
    temperature: -10,
    g: 9.8,
    retarderActivation: {},
    retarderOutput: {},
});

// 计算属性：将对象转换为JSON字符串用于textarea显示
const retarderActivationText = computed({
    get: () => JSON.stringify(formData.value.retarderActivation, null, 2),
    set: (value: string) => {
        try {
            formData.value.retarderActivation = JSON.parse(value);
        } catch (e) {
            // 如果JSON解析失败，保持原值
        }
    },
});

const retarderOutputText = computed({
    get: () => JSON.stringify(formData.value.retarderOutput, null, 2),
    set: (value: string) => {
        try {
            formData.value.retarderOutput = JSON.parse(value);
        } catch (e) {
            // 如果JSON解析失败，保持原值
        }
    },
});

// 更新函数
function updateRetarderActivation(value: string) {
    try {
        formData.value.retarderActivation = JSON.parse(value);
    } catch (e) {
        // JSON解析失败时显示错误提示
        ElMessage.warning(t("hump.calcCondition.messages.retarderActivationFormatError"));
    }
}

function updateRetarderOutput(value: string) {
    try {
        formData.value.retarderOutput = JSON.parse(value);
    } catch (e) {
        // JSON解析失败时显示错误提示
        ElMessage.warning(t("hump.calcCondition.messages.retarderOutputFormatError"));
    }
}

async function fetchConditions(instanceID: string) {
    if (!instanceID) return;

    try {
        loading.value = true;
        const response = await axios.get(`/Hump/GetOperationConditions?instanceID=${instanceID}`);
        conditionsList.value = response.data || [];
    } catch (error) {
        console.error('Failed to fetch conditions:', error);
        ElMessage.error(t('hump.calcCondition.errors.loadConditions'));
    } finally {
        loading.value = false;
    }
}

async function saveCondition() {
    if (!currentInstanceId.value) {
        ElMessage.warning(t('hump.calcCondition.errors.noInstance'));
        return;
    }

    try {
        loading.value = true;

        // 准备API数据
        const conditionData: OperationCondition = {
            instanceID: currentInstanceId.value,
            id: currentConditionId.value,
            name: formData.value.name,
            wagonVelocityOnTop: formData.value.wagonVelocityOnTop,
            wagonVelocityOnSlope: formData.value.wagonVelocityOnSlope,
            wagonVelocityOnYard: formData.value.wagonVelocityOnYard,
            windVelocity: formData.value.windVelocity,
            isHeadWind: formData.value.isHeadWind,
            airDensity: formData.value.airDensity,
            temperature: formData.value.temperature,
        };

        if (currentConditionId.value) {
            // 更新现有条件
            await axios.put('/Hump/EditOperationCondition', conditionData);
            ElMessage.success(t('hump.calcCondition.messages.updated'));
        } else {
            // 创建新条件
            const response = await axios.post('/Hump/CreateOperationCondition', conditionData);
            currentConditionId.value = response.data.id;
            ElMessage.success(t('hump.calcCondition.messages.created'));
        }

        // 重新加载条件列表
        await fetchConditions(currentInstanceId.value);
    } catch (error) {
        console.error('Failed to save condition:', error);
        ElMessage.error(t('hump.calcCondition.errors.save'));
    } finally {
        loading.value = false;
    }
}

async function deleteCondition() {
    if (!currentConditionId.value) {
        ElMessage.warning(t('hump.calcCondition.errors.noCondition'));
        return;
    }

    try {
        await ElMessageBox.confirm(
            t('hump.calcCondition.confirmDelete'),
            t('hump.calcCondition.warning'),
            {
                confirmButtonText: t('common.confirm'),
                cancelButtonText: t('common.cancel'),
                type: 'warning',
            }
        );

        loading.value = true;
        await axios.delete(`/Hump/DeleteOperationCondition?id=${currentConditionId.value}`);
        ElMessage.success(t('hump.calcCondition.messages.deleted'));

        // 重置表单和当前选择
        resetForm();
        currentConditionId.value = "";

        // 重新加载条件列表
        await fetchConditions(currentInstanceId.value);
    } catch (error: any) {
        if (error !== 'cancel') {
            console.error('Failed to delete condition:', error);
            ElMessage.error(t('hump.calcCondition.errors.delete'));
        }
    } finally {
        loading.value = false;
    }
}

function resetForm() {
    formData.value = {
        name: t("hump.calcCondition.defaults.conditionName"),
        wagonTypeName: "P70H",
        wagonVelocityOnTop: 1.4,
        wagonVelocityOnSlope: 5.2,
        wagonVelocityOnYard: 2.2,
        windVelocity: 5,
        isHeadWind: 1,
        airDensity: 0.125,
        temperature: -10,
        g: 9.8,
        retarderActivation: {},
        retarderOutput: {},
    };
    currentConditionId.value = "";
}

function createNewCondition() {
    resetForm();
    currentConditionId.value = "";
    ElMessage.info(t('hump.calcCondition.messages.newCondition'));
}

async function onInstanceChange() {
    conditionsList.value = [];
    currentConditionId.value = "";
    resetForm();

    if (currentInstanceId.value) {
        await fetchConditions(currentInstanceId.value);
    }
}

function onConditionChange() {
    if (!currentConditionId.value) {
        resetForm();
        return;
    }

    const condition = conditionsList.value.find(c => c.id === currentConditionId.value);
    if (condition) {
        // 加载选中的条件到表单
        formData.value = {
            ...formData.value, // 保留本地字段 (wagonTypeName, g, retarders)
            name: condition.name,
            wagonVelocityOnTop: condition.wagonVelocityOnTop,
            wagonVelocityOnSlope: condition.wagonVelocityOnSlope,
            wagonVelocityOnYard: condition.wagonVelocityOnYard,
            windVelocity: condition.windVelocity,
            isHeadWind: condition.isHeadWind,
            airDensity: condition.airDensity,
            temperature: condition.temperature,
        };
        ElMessage.success(t('hump.calcCondition.messages.loaded'));
    }
}

// 组件初始化
onMounted(() => {
    // fetchInstances();
    // 初始化时同步 selectedInstanceId 到 currentInstanceId
    if (props.selectedInstanceId) {
        currentInstanceId.value = props.selectedInstanceId;
    }
});

// 监听 props.selectedInstanceId 的变化，同步到 currentInstanceId
watch(() => props.selectedInstanceId, (newInstanceId) => {
    // 只要 newInstanceId 与当前 currentInstanceId 不同就更新
    if (newInstanceId !== currentInstanceId.value) {
        currentInstanceId.value = newInstanceId || "";
    }
}, { immediate: true });

// 监听 currentInstanceId 的变化，重新执行 fetchConditions  
watch(currentInstanceId, async (newInstanceId) => {
    if (newInstanceId) {
        await onInstanceChange();
    } else {
        // 清空条件列表和当前选择
        conditionsList.value = [];
        currentConditionId.value = "";
        resetForm();
    }
}, { immediate: true });
</script>

<style scoped lang="css">
.calculation-condition-container {
    width: 100%;
}

.condition-card {
    margin-bottom: 0;
}

.card-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    width: 100%;
    margin-bottom: 20px;
    flex-wrap: wrap;
    gap: 10px;
}

.header-buttons {
    display: flex;
    gap: 10px;
}

.form-sections {
    display: flex;
    /* flex-direction: column; */
    gap: 30px;
}

.form-column {
    min-width: 450px;
    display: flex;
    flex-direction: column;
}

.conditions-list-card {
    box-shadow: 0 2px 12px rgba(0, 0, 0, 0.1);
}

:deep(.el-divider--horizontal) {
    margin-top: 40px;
}
</style>
