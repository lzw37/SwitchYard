<template>
    <section class="hump-main">
        <el-tabs v-model="activeTab" type="card" class="hump-tabs">
            <el-tab-pane label="平面展开图" name="plan">
                <HumpLayout />
            </el-tab-pane>

            <el-tab-pane label="计算参数" name="vehicle">
                <el-card class="param-card" shadow="hover">
                    <h3>车辆参数</h3>
                    <Wagon />
                </el-card>
                <el-card class="param-card" shadow="hover">
                    <h3>计算条件</h3>
                    <HumpCalculationCondition />
                </el-card>
            </el-tab-pane>

            <el-tab-pane label="纵断面设计" name="profile">
                <HumpSlopeDesigner />
            </el-tab-pane>

            <el-tab-pane label="车辆溜放检算" name="release">
                <el-card shadow="hover">溜放检算结果（占位）</el-card>
            </el-tab-pane>
        </el-tabs>
    </section>
</template>

<script lang="ts" setup>
import { ref } from 'vue'
import HumpLayout from './HumpLayout.vue';
import HumpSlopeDesigner from './HumpSlopeDesigner.vue';
import Wagon from './Wagon.vue';
import HumpCalculationCondition from './HumpCalculationCondition.vue';

const activeTab = ref('plan')
const selectedLine = ref<number | null>(null)
const lines = ref([
    { id: 1, name: '1 号线' },
    { id: 2, name: '2 号线' },
    { id: 3, name: '3 号线' }
])
const planSubTab = ref('ctrl')
</script>

<style scoped lang="css">
.hump-main {
    width: 100dvw;
    height: 100dvh;
    padding: 24px;
    background-color: white;
}

.hump-tabs {
    margin: 0 auto;
}

el-card {
    padding: 18px;
}

.plan-top {
    display: flex;
    justify-content: flex-start;
    gap: 12px;
    margin-bottom: 12px;
}

.plan-graphic {
    margin-bottom: 14px;
}

.graphic-placeholder {
    height: 300px;
    border: 2px dashed #d6e5ef;
    border-radius: 8px;
    display: flex;
    align-items: center;
    justify-content: center;
    color: #5d6d7a;
    background: linear-gradient(180deg, #ffffff 0%, #fbfdff 100%);
}

.plan-subtabs {
    margin-top: 12px;
}

.param-card {
    margin-bottom: 24px;
}
</style>
