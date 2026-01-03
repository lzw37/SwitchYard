<template>
    <section class="hump-layout">
        <div class="plan-top">
            <el-select v-model="selectedLine" placeholder="请选择线路" clearable style="width:240px">
                <el-option v-for="line in lines" :key="line.id" :label="line.name" :value="line.id" />
            </el-select>
            <div>
                <el-button type="primary" @click="loadFlatLayout">加载</el-button>
                <el-button type="primary" @click="renderLayout">渲染</el-button>
            </div>
        </div>

        <div class="plan-graphic">
            <div class="graphic-placeholder">
                <HumpLayoutCtrl ref="ctrlRef" v-model:flatLayout="flatLayout" />
            </div>
        </div>

        <div class="plan-subtabs">
            <el-tabs v-model="planSubTab" type="border-card">
                <el-tab-pane label="控制点及区段" name="ctrl">
                    <el-card>控制点与区段列表（占位）</el-card>
                </el-tab-pane>
                <el-tab-pane label="道岔" name="switch">
                    <el-card>道岔信息（占位）</el-card>
                </el-tab-pane>
                <el-tab-pane label="减速器" name="retarder">
                    <el-card>减速器参数（占位）</el-card>
                </el-tab-pane>
            </el-tabs>
        </div>
    </section>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import HumpLayoutCtrl from './HumpLayoutCtrl.vue'
import type { FlatLayout } from './humplayoutctrl'
import axios from 'axios'

const selectedLine = ref<number | null>(null)
const lines = ref([
    { id: 1, name: '1 号线' },
    { id: 2, name: '2 号线' },
    { id: 3, name: '3 号线' }
])
const planSubTab = ref('ctrl')

const ctrlRef = ref<InstanceType<typeof HumpLayoutCtrl> | null>(null)
const flatLayout = ref<FlatLayout | null>(null)

function renderLayout() {
    // ctrlRef.value?.renderLayout()
}

function loadFlatLayout() {
    axios.get('https://localhost:7297/hump/getflatlayout').then(response => {
        flatLayout.value = response.data
        console.log('Flat layout data loaded:', flatLayout.value)
        // child will react to flatLayout prop change and load data
    }).catch(error => {
        console.error('Error fetching flat layout data:', error)
    })
}

function saveFlatLayout() {
    if (!flatLayout.value) {
        console.warn('No flat layout data to save.')
        return
    }
    axios.post('https://localhost:7297/hump/saveflatlayout', flatLayout.value).then(response => {
        console.log('Flat layout data saved successfully.')
    }).catch(error => {
        console.error('Error saving flat layout data:', error)
    })
}

</script>

<style scoped>
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
</style>