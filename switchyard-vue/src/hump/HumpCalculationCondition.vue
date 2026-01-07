<template>
    <div class="calculation-condition-container">
        <el-card class="condition-card">
            <template #header>
                <div class="card-header">
                    <div>
                        <span style="width:100px"></span>
                        <el-select style="width:300px"></el-select>
                        <el-button type="primary" size="small" @click="saveCondition">新建</el-button>
                    </div>

                    <div class="header-buttons">
                        <el-button type="primary" size="small" @click="saveCondition">保存</el-button>
                        <el-button size="small" @click="resetForm">重置</el-button>
                    </div>
                </div>
            </template>

            <el-form :model="formData" label-width="140px" ref="formRef">
                <!-- 条件名称部分 -->
                <el-divider content-position="left">概况</el-divider>
                <el-form-item label="计算条件名称" prop="conditionName">
                    <el-input v-model="formData.conditionName" placeholder="例如：标准计算条件" style="width: 300px" />
                </el-form-item>

                <!-- 车辆信息部分 -->
                <el-divider content-position="left">车辆信息</el-divider>
                <el-form-item label="车辆类型" prop="wagonTypeName">
                    <el-select v-model="formData.wagonTypeName" placeholder="请选择车辆类型" style="width: 300px" filterable
                        allow-create default-first-option>
                        <el-option label="P70H" value="P70H" />
                        <el-option label="C70H" value="C70H" />
                        <el-option label="C80B" value="C80B" />
                        <el-option label="C80H" value="C80H" />
                        <el-option label="C64K" value="C64K" />
                        <el-option label="C62A" value="C62A" />
                        <el-option label="C60" value="C60" />
                        <el-option label="C50" value="C50" />
                        <el-option label="其他" value="其他" />
                    </el-select>
                </el-form-item>

                <!-- 速度信息部分 -->
                <el-divider content-position="left">速度参数</el-divider>
                <el-row :gutter="20">
                    <el-col :span="8">
                        <el-form-item label="推峰速度" prop="wagonVelocityOnTop">
                            <div style="display: flex; align-items: center;">
                                <el-input-number v-model="formData.wagonVelocityOnTop" :min="0" :step="0.1"
                                    :precision="2" style="flex: 1;" />
                                <span style="margin-left: 8px; color: #666; font-size: 14px;">m/s</span>
                            </div>
                        </el-form-item>
                    </el-col>
                    <el-col :span="8">
                        <el-form-item label="溜放部分速度" prop="wagonVelocityOnSlop">
                            <div style="display: flex; align-items: center;">
                                <el-input-number v-model="formData.wagonVelocityOnSlop" :min="0" :step="0.1"
                                    :precision="2" style="flex: 1;" />
                                <span style="margin-left: 8px; color: #666; font-size: 14px;">m/s</span>
                            </div>
                        </el-form-item>
                    </el-col>
                    <el-col :span="8">
                        <el-form-item label="调车场速度" prop="wagonVelocityOnYard">
                            <div style="display: flex; align-items: center;">
                                <el-input-number v-model="formData.wagonVelocityOnYard" :min="0" :step="0.1"
                                    :precision="2" style="flex: 1;" />
                                <span style="margin-left: 8px; color: #666; font-size: 14px;">m/s</span>
                            </div>
                        </el-form-item>
                    </el-col>
                </el-row>

                <!-- 风速信息部分 -->
                <el-divider content-position="left">风速信息</el-divider>
                <el-row :gutter="20">
                    <el-col :span="12">
                        <el-form-item label="风速" prop="windVelocity">
                            <div style="display: flex; align-items: center;">
                                <el-input-number v-model="formData.windVelocity" :min="0" :step="0.1" :precision="2"
                                    style="flex: 1;" />
                                <span style="margin-left: 8px; color: #666; font-size: 14px;">m/s</span>
                            </div>
                        </el-form-item>
                    </el-col>
                    <el-col :span="12">
                        <el-form-item label="风向" prop="isHeadWind">
                            <el-select v-model="formData.isHeadWind" style="width: 100%">
                                <el-option label="逆风" :value="1" />
                                <el-option label="顺风" :value="0" />
                            </el-select>
                        </el-form-item>
                    </el-col>
                </el-row>

                <!-- 环境参数部分 -->
                <el-divider content-position="left">环境参数</el-divider>
                <el-row :gutter="20">
                    <el-col :span="12">
                        <el-form-item label="空气密度" prop="airDensity">
                            <div style="display: flex; align-items: center;">
                                <el-input-number v-model="formData.airDensity" :min="0" :step="0.001" :precision="4"
                                    style="flex: 1;" />
                                <span style="margin-left: 8px; color: #666; font-size: 14px;">kg/m³</span>
                            </div>
                        </el-form-item>
                    </el-col>
                    <el-col :span="12">
                        <el-form-item label="温度" prop="temperature">
                            <div style="display: flex; align-items: center;">
                                <el-input-number v-model="formData.temperature" :step="1" :precision="0"
                                    style="flex: 1;" />
                                <span style="margin-left: 8px; color: #666; font-size: 14px;">°C</span>
                            </div>
                        </el-form-item>
                    </el-col>
                </el-row>

                <!-- 重力加速度部分 -->
                <el-divider content-position="left">物理参数</el-divider>
                <el-form-item label="重力加速度" prop="g">
                    <div style="display: flex; align-items: center;">
                        <el-input-number v-model="formData.g" :min="0" :step="0.01" :precision="2"
                            style="width: 300px;" />
                        <span style="margin-left: 8px; color: #666; font-size: 14px;">m/s²</span>
                    </div>
                </el-form-item>

                <!-- 减速机参数部分 -->
                <el-divider content-position="left">减速机参数</el-divider>
                <el-form-item label="减速机启动阈值" prop="retarderActivation">
                    <el-input v-model="retarderActivationText" placeholder="JSON格式，例如：{}" style="width: 100%" rows="3"
                        type="textarea" @input="updateRetarderActivation" />
                </el-form-item>
                <el-form-item label="减速机输出参数" prop="retarderOutput">
                    <el-input v-model="retarderOutputText" placeholder="JSON格式，例如：{}" style="width: 100%" rows="3"
                        type="textarea" @input="updateRetarderOutput" />
                </el-form-item>
            </el-form>
        </el-card>
    </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue';
import { ElMessage } from 'element-plus';

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
const formData = ref<CalculationCondition>({
    conditionName: '标准计算条件',
    wagonTypeName: 'P70H',
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
    }
});

const retarderOutputText = computed({
    get: () => JSON.stringify(formData.value.retarderOutput, null, 2),
    set: (value: string) => {
        try {
            formData.value.retarderOutput = JSON.parse(value);
        } catch (e) {
            // 如果JSON解析失败，保持原值
        }
    }
});

// 更新函数
function updateRetarderActivation(value: string) {
    try {
        formData.value.retarderActivation = JSON.parse(value);
    } catch (e) {
        // JSON解析失败时显示错误提示
        ElMessage.warning('减速机启动阈值格式错误，请输入有效的JSON');
    }
}

function updateRetarderOutput(value: string) {
    try {
        formData.value.retarderOutput = JSON.parse(value);
    } catch (e) {
        // JSON解析失败时显示错误提示
        ElMessage.warning('减速机输出参数格式错误，请输入有效的JSON');
    }
}

// 保存条件
function saveCondition() {
    const conditions = localStorage.getItem('calculationConditions') || '[]';
    const list: CalculationCondition[] = JSON.parse(conditions);
    list.push({ ...formData.value });
    localStorage.setItem('calculationConditions', JSON.stringify(list));
    conditionsList.value = list;
    ElMessage.success('计算条件已保存');
}

// 重置表单
function resetForm() {
    formData.value = {
        conditionName: '标准计算条件',
        wagonTypeName: 'P70H',
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
    };
    // 重置textarea的值
    retarderActivationText.value = '{}';
    retarderOutputText.value = '{}';
}

// 加载条件
function loadCondition(index: number) {
    const condition = conditionsList.value[index];
    if (condition) {
        formData.value = { ...condition };
        ElMessage.success('计算条件已加载');
    }
}

// 删除条件
function deleteCondition(index: number) {
    conditionsList.value.splice(index, 1);
    localStorage.setItem('calculationConditions', JSON.stringify(conditionsList.value));
    ElMessage.success('计算条件已删除');
}

// 初始化加载保存的条件
function initConditions() {
    const conditions = localStorage.getItem('calculationConditions') || '[]';
    conditionsList.value = JSON.parse(conditions);
}

initConditions();
</script>

<style scoped lang="css">
.calculation-condition-container {
    min-height: 100vh;
}

.condition-card {
    max-width: 1200px;
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

.conditions-list-card {
    box-shadow: 0 2px 12px rgba(0, 0, 0, 0.1);
}

:deep(.el-divider--horizontal) {
    margin-top: 40px;
}
</style>
