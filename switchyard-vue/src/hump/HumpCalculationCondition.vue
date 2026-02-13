<template>
    <div class="calculation-condition-container">
        <el-card class="condition-card">
            <template #header>
                <div class="card-header">
                    <div>
                        <span style="width: 100px"></span>
                        <el-select style="width: 300px"></el-select>
                        <el-button type="primary" size="small" @click="saveCondition">{{
                            t("hump.calcCondition.new")
                            }}</el-button>
                    </div>

                    <div class="header-buttons">
                        <el-button type="primary" size="small" @click="saveCondition">{{
                            t("hump.calcCondition.save")
                            }}</el-button>
                        <el-button size="small" @click="resetForm">{{
                            t("hump.calcCondition.reset")
                            }}</el-button>
                    </div>
                </div>
            </template>

            <el-form :model="formData" label-width="140px" ref="formRef">
                <div class="form-sections">
                    <!-- 第1列: 概况、车辆信息、速度参数 -->
                    <div class="form-column">
                        <el-divider content-position="left">{{
                            t("hump.calcCondition.sections.overview")
                            }}</el-divider>
                        <el-form-item :label="t('hump.calcCondition.labels.conditionName')" prop="conditionName">
                            <el-input v-model="formData.conditionName" :placeholder="t('hump.calcCondition.placeholders.conditionName')
                                " style="width: 300px" />
                        </el-form-item>

                        <el-divider content-position="left">{{
                            t("hump.calcCondition.sections.wagonInfo")
                            }}</el-divider>
                        <el-form-item :label="t('hump.calcCondition.labels.wagonType')" prop="wagonTypeName">
                            <el-select v-model="formData.wagonTypeName" :placeholder="t('hump.calcCondition.placeholders.chooseWagon')
                                " style="width: 300px" filterable allow-create default-first-option>
                                <el-option label="P70H" value="P70H" />
                                <el-option label="C70H" value="C70H" />
                                <el-option label="C80B" value="C80B" />
                                <el-option label="C80H" value="C80H" />
                                <el-option label="C64K" value="C64K" />
                                <el-option label="C62A" value="C62A" />
                                <el-option label="C60" value="C60" />
                                <el-option label="C50" value="C50" />
                                <el-option :label="t('hump.calcCondition.options.other')" value="其他" />
                            </el-select>
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
                            prop="wagonVelocityOnSlop">
                            <div style="display: flex; align-items: center">
                                <el-input-number v-model="formData.wagonVelocityOnSlop" :min="0" :step="0.1"
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

                        <el-divider content-position="left">{{
                            t("hump.calcCondition.sections.physical")
                            }}</el-divider>
                        <el-form-item :label="t('hump.calcCondition.labels.gravity')" prop="g">
                            <div style="display: flex; align-items: center">
                                <el-input-number v-model="formData.g" :min="0" :step="0.01" :precision="2"
                                    style="width: 300px" />
                                <span style="margin-left: 8px; color: #666; font-size: 14px">{{ t("units.m_s2")
                                    }}</span>
                            </div>
                        </el-form-item>
                    </div>

                    <!-- 第3列: 减速机参数 -->
                    <div class="form-column">
                        <el-divider content-position="left">{{
                            t("hump.calcCondition.sections.retarder")
                            }}</el-divider>
                        <el-form-item :label="t('hump.calcCondition.labels.retarderActivation')"
                            prop="retarderActivation">
                            <el-input v-model="retarderActivationText" :placeholder="t('hump.calcCondition.placeholders.jsonExample')
                                " style="width: 100%" rows="3" type="textarea" @input="updateRetarderActivation" />
                        </el-form-item>
                        <el-form-item :label="t('hump.calcCondition.labels.retarderOutput')" prop="retarderOutput">
                            <el-input v-model="retarderOutputText" :placeholder="t('hump.calcCondition.placeholders.jsonExample')
                                " style="width: 100%" rows="3" type="textarea" @input="updateRetarderOutput" />
                        </el-form-item>
                    </div>
                </div>
            </el-form>
        </el-card>
    </div>
</template>

<script setup lang="ts">
import { ref, computed } from "vue";
import { ElMessage } from "element-plus";
import { useI18n } from "vue-i18n";

interface CalculationCondition {
    conditionName?: string;
    wagonTypeName: string;
    wagonVelocityOnTop: number;
    wagonVelocityOnSlop: number;
    wagonVelocityOnYard: number;
    windVelocity: number;
    isHeadWind: number;
    airDensity: number;
    temperature: number;
    g: number;
    retarderActivation: Record<string, any>;
    retarderOutput: Record<string, any>;
}

const formRef = ref();
const { t } = useI18n();
const formData = ref<CalculationCondition>({
    conditionName: t("hump.calcCondition.defaults.conditionName"),
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
    retarderOutput: {},
});

const conditionsList = ref<CalculationCondition[]>([]);

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

// 保存条件
function saveCondition() {
    const conditions = localStorage.getItem("calculationConditions") || "[]";
    const list: CalculationCondition[] = JSON.parse(conditions);
    list.push({ ...formData.value });
    localStorage.setItem("calculationConditions", JSON.stringify(list));
    conditionsList.value = list;
    ElMessage.success(t("hump.calcCondition.messages.saved"));
}

// 重置表单
function resetForm() {
    formData.value = {
        conditionName: "标准计算条件",
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
        retarderOutput: {},
    };
    // 重置textarea的值
    retarderActivationText.value = "{}";
    retarderOutputText.value = "{}";
}

// 加载条件
function loadCondition(index: number) {
    const condition = conditionsList.value[index];
    if (condition) {
        formData.value = { ...condition };
        ElMessage.success(t("hump.calcCondition.messages.loaded"));
    }
}

// 删除条件
function deleteCondition(index: number) {
    conditionsList.value.splice(index, 1);
    localStorage.setItem("calculationConditions", JSON.stringify(conditionsList.value));
    ElMessage.success(t("hump.calcCondition.messages.deleted"));
}

// 初始化加载保存的条件
function initConditions() {
    const conditions = localStorage.getItem("calculationConditions") || "[]";
    conditionsList.value = JSON.parse(conditions);
}

initConditions();
</script>

<style scoped lang="css">
.calculation-condition-container {
    min-height: 100vh;
}

.condition-card {
    margin-bottom: 20px;
    box-shadow: 0 2px 12px rgba(0, 0, 0, 0.1);
}

.card-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    width: 100%;
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
