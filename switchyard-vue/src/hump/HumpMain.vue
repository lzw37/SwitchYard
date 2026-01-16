<template>
    <section class="hump-main">
        <el-tabs v-model="activeTab" class="hump-tabs">
            <el-tab-pane :label="t('humpMain.tabs.plan')" name="plan">
                <HumpLayout />
            </el-tab-pane>

            <el-tab-pane :label="t('humpMain.tabs.vehicle')" name="vehicle">
                <el-card class="param-card" shadow="hover">
                    <h3>{{ t('humpMain.headings.wagonParams') }}</h3>
                    <Wagon />
                </el-card>
                <el-card class="param-card" shadow="hover">
                    <h3>{{ t('humpMain.headings.calcCondition') }}</h3>
                    <HumpCalculationCondition />
                </el-card>
            </el-tab-pane>
            <el-tab-pane :label="t('humpMain.tabs.profile')" name="profile">
                <HumpSlopeDesigner />
            </el-tab-pane>
            <el-tab-pane :label="t('humpMain.tabs.release')" name="release">
                <HumpHeadwayCheck />
            </el-tab-pane>
            <el-tab-pane :label="t('humpMain.tabs.simulation')" name="simulation">
                <HumpSim />
            </el-tab-pane>
        </el-tabs>
    </section>
</template>

<script lang="ts" setup>
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import HumpLayout from './HumpLayout.vue';
import HumpSlopeDesigner from './HumpSlopeDesigner.vue';
import Wagon from './Wagon.vue';
import HumpCalculationCondition from './HumpCalculationCondition.vue';
import HumpHeadwayCheck from './HumpHeadwayCheck.vue';
import HumpSim from './HumpSim.vue';

const activeTab = ref('plan')
const { t } = useI18n()
const selectedLine = ref<number | null>(null)
const lines = ref([
    { id: 1 },
    { id: 2 },
    { id: 3 }
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
