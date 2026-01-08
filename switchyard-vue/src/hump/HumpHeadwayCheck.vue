<template>
    <div class="headway-check-container">
        <!-- 工具栏 -->
        <div class="headway-toolbar">

            <div class="headway-toolbar__group">
                <label>检算实例</label>
                <el-select v-model="selectedVerification" placeholder="请选择检算数据" size="small" clearable>
                    <el-option label="验证集1" value="verify1"></el-option>
                    <el-option label="验证集2" value="verify2"></el-option>
                    <el-option label="验证集3" value="verify3"></el-option>
                </el-select>
            </div>
            <div class="headway-toolbar__group">
                <el-button type="primary" size="small">新建检算实例</el-button>
                <el-button type="danger" size="small">删除</el-button>
                <el-button type="success" size="small">保存</el-button>
            </div>
            <div class="headway-toolbar__group">
                <label>纵断面方案</label>
                <el-select v-model="selectedDesignScheme" placeholder="请选择设计方案" size="small" clearable>
                    <el-option label="方案1" value="scheme1"></el-option>
                    <el-option label="方案2" value="scheme2"></el-option>
                    <el-option label="方案3" value="scheme3"></el-option>
                </el-select>
            </div>
            <div class="headway-toolbar__group">
                <label>计算条件</label>
                <el-select v-model="selectedCondition" placeholder="请选择计算条件" size="small" clearable>
                    <el-option label="条件A" value="conditionA"></el-option>
                    <el-option label="条件B" value="conditionB"></el-option>
                    <el-option label="条件C" value="conditionC"></el-option>
                </el-select>
            </div>
            <div class="headway-toolbar__group">
                <el-button type="primary" size="small">生成速度-距离曲线</el-button>
            </div>
            <div class="headway-toolbar__group">
                <el-button type="primary" size="small">生成时间-距离曲线</el-button>
            </div>
        </div>

        <!-- 图表容器 -->
        <div class="charts-container">
            <!-- 左侧：速度-距离曲线 -->
            <div class="chart-wrapper">
                <div class="chart-header">
                    <span>速度-距离曲线</span>
                    <div class="chart-tags">
                        <el-tag v-for="tag in velocityTabs" :key="tag.name" closable
                            @close="handleRemoveVelocityTab(tag.name)">
                            {{ tag.label }}
                        </el-tag>
                    </div>
                </div>
                <div class="chart-content" id="velocity-distance-chart">
                    <!-- 速度-距离曲线图表内容 -->
                </div>
            </div>

            <!-- 右侧：时间-距离曲线 -->
            <div class="chart-wrapper">
                <div class="chart-header">
                    <span>时间-距离曲线</span>
                    <div class="chart-tags">
                        <el-tag v-for="tag in timeTabs" :key="tag.name" closable @close="handleRemoveTimeTab(tag.name)">
                            {{ tag.label }}
                        </el-tag>
                    </div>
                </div>
                <div class="chart-content" id="time-distance-chart">
                    <!-- 时间-距离曲线图表内容 -->
                </div>
            </div>
        </div>
    </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'

const selectedDesignScheme = ref('')
const selectedCondition = ref('')
const selectedVerification = ref('')

// 速度-距离曲线tags数据
const velocityTabs = ref([
    { name: 'series1', label: '系列1' },
    { name: 'series2', label: '系列2' }
])

// 时间-距离曲线tags数据
const timeTabs = ref([
    { name: 'series1', label: '系列1' },
    { name: 'series2', label: '系列2' }
])

// 删除速度-距离曲线的tag
const handleRemoveVelocityTab = (tagName: string) => {
    const index = velocityTabs.value.findIndex(tab => tab.name === tagName)
    if (index > -1) {
        velocityTabs.value.splice(index, 1)
    }
}

// 删除时间-距离曲线的tag
const handleRemoveTimeTab = (tagName: string) => {
    const index = timeTabs.value.findIndex(tab => tab.name === tagName)
    if (index > -1) {
        timeTabs.value.splice(index, 1)
    }
}
</script>

<style lang="css" scoped>
.headway-check-container {
    display: flex;
    flex-direction: column;
    width: 100%;
    height: 100%;
    background-color: #ffffff;
}

/* 工具栏样式 */
.headway-toolbar {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: 12px;
    padding: 14px 20px;
    margin: 5px 5px 16px 5px;
    border: 1px solid #dbe3f1;
    border-radius: 2px;
    background: linear-gradient(135deg, #f8fafc, #eef3ff);
    box-shadow: 0 5px 15px rgba(15, 23, 42, 0.08);
    min-width: 1400px;
}

.headway-toolbar__group {
    display: flex;
    align-items: center;
    gap: 10px;
    padding: 4px 8px;
    border-radius: 5px;
    /* border: 1px solid #e3eaf7; */
    /* background: #ffffff; */
    /* box-shadow: inset 0 1px 0 rgba(255, 255, 255, 0.9); */
    transition: box-shadow 0.2s ease, border-color 0.2s ease;
}

.headway-toolbar__group label {
    font-size: 13px;
    font-weight: 600;
    color: #1f2a37;
    min-width: 70px;
    text-align: right;
    letter-spacing: 0.02em;
    white-space: nowrap;
}

.headway-toolbar__group :deep(.el-select) {
    min-width: 150px;
}

.headway-toolbar__group :deep(.el-button) {
    margin: 0;
}

/* 图表容器样式 */
.charts-container {
    display: flex;
    flex: 1;
    gap: 16px;
    padding: 16px 20px;
    overflow: auto;
    min-width: 800px;
}

.chart-wrapper {
    flex: 1;
    display: flex;
    flex-direction: column;
    border: 1px solid #dbe3f1;
    border-radius: 2px;
    background: #ffffff;
    box-shadow: 0 2px 8px rgba(15, 23, 42, 0.08);
    overflow: hidden;
}

.chart-header {
    display: flex;
    padding: 12px 16px;
    background: linear-gradient(135deg, #f8fafc, #eef3ff);
    border-bottom: 1px solid #dbe3f1;
    color: #1f2a37;
    letter-spacing: 0.02em;
    align-items: center;
}

.chart-header span {
    font-size: 16px;
    font-weight: 600;
    letter-spacing: 0.02em;
    flex-shrink: 0;
}

.chart-tags {
    display: flex;
    gap: 8px;
    flex: 1;
    margin-left: 16px;
    align-items: center;
    flex-wrap: wrap;
}

.chart-content {
    flex: 1;
    padding: 16px;
    overflow: auto;
    display: flex;
    align-items: center;
    justify-content: center;
    color: #9ca3af;
    font-size: 12px;
}

/* Tag样式 */
.chart-tags :deep(.el-tag) {
    margin: 0;
    font-size: small;
}
</style>