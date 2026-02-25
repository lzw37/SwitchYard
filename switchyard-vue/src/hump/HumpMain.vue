<template>
    <section class="hump-main">
        <div class="hump-tabs-wrapper">
            <div class="left-controls">
                <el-button type="primary" @click="showInstanceManager = true">
                    {{ t('humpMain.buttons.instanceManager') }}
                </el-button>
                <el-select v-model="selectedLine" class="line-select"
                    :placeholder="t('humpMain.placeholders.selectInstance')" :loading="loadingInstances"
                    :disabled="loadingInstances">
                    <el-option v-for="line in lines" :key="line.id" :label="line.name" :value="line.id" />
                </el-select>
            </div>
            <div class="right-controls">
                <el-button-group class="language-switch">
                    <el-button size="small" :type="currentLocale === 'zh' ? 'primary' : 'default'"
                        @click="switchLanguage('zh')">
                        中文
                    </el-button>
                    <el-button size="small" :type="currentLocale === 'en' ? 'primary' : 'default'"
                        @click="switchLanguage('en')">
                        EN
                    </el-button>
                </el-button-group>
            </div>
            <el-tabs v-model="activeTab" class="hump-main-tabs">
                <el-tab-pane :label="t('humpMain.tabs.plan')" name="plan" lazy>
                    <HumpLayout v-if="hasSelectedInstance" :selectedInstanceId="selectedLine" />
                    <el-empty v-else :description="t('humpMain.placeholders.selectInstance')" />
                </el-tab-pane>
                <el-tab-pane :label="t('humpMain.tabs.vehicle')" name="vehicle" lazy>
                    <template v-if="hasSelectedInstance">
                        <el-card class="param-card" shadow="hover">
                            <h3>{{ t('humpMain.headings.wagonParams') }}</h3>
                            <Wagon :selectedInstanceId="selectedLine" />
                        </el-card>
                        <el-card class="param-card" shadow="hover">
                            <h3>{{ t('humpMain.headings.calcCondition') }}</h3>
                            <HumpCalculationCondition :selectedInstanceId="selectedLine" />
                        </el-card>
                    </template>
                    <el-empty v-else :description="t('humpMain.placeholders.selectInstance')" />
                </el-tab-pane>
                <el-tab-pane :label="t('humpMain.tabs.profile')" name="profile" lazy>
                    <HumpSlopeDesigner v-if="hasSelectedInstance" :selectedInstanceId="selectedLine" />
                    <el-empty v-else :description="t('humpMain.placeholders.selectInstance')" />
                </el-tab-pane>
                <el-tab-pane :label="t('humpMain.tabs.release')" name="release" lazy>
                    <HumpHeadwayCheck v-if="hasSelectedInstance" :selectedInstanceId="selectedLine" />
                    <el-empty v-else :description="t('humpMain.placeholders.selectInstance')" />
                </el-tab-pane>
                <el-tab-pane :label="t('humpMain.tabs.simulation')" name="simulation" lazy>
                    <HumpSim v-if="hasSelectedInstance" :selectedInstanceId="selectedLine" />
                    <el-empty v-else :description="t('humpMain.placeholders.selectInstance')" />
                </el-tab-pane>
            </el-tabs>
        </div>
        <el-dialog v-model="showInstanceManager" :title="t('humpMain.dialogs.instanceManagerTitle')" width="90%"
            :close-on-click-modal="false" :before-close="handleCloseInstanceManager">
            <HumpInstanceManager />
        </el-dialog>
    </section>
</template>

<script lang="ts" setup>
import { ref, onMounted, computed } from 'vue'
import { useI18n } from 'vue-i18n'
import axios from '@/utils/axios'
import { ElMessage } from 'element-plus'
import HumpLayout from './HumpLayout.vue';
import HumpSlopeDesigner from './HumpSlopeDesigner.vue';
import Wagon from './Wagon.vue';
import HumpCalculationCondition from './HumpCalculationCondition.vue';
import HumpHeadwayCheck from './HumpHeadwayCheck.vue';
import HumpSim from './HumpSim.vue';
import HumpInstanceManager from './HumpInstanceManager.vue';

const { t, locale } = useI18n()

interface HumpInstance {
    id: string
    name: string
    owner: string
    createdDate: string
    isActive: number
}

const activeTab = ref('plan')
const selectedLine = ref<string | null>(null)
const lines = ref<HumpInstance[]>([])
const showInstanceManager = ref(false)
const loadingInstances = ref(false)
const hasSelectedInstance = computed(() => Boolean(selectedLine.value))

// Current language
const currentLocale = computed(() => locale.value)

// Switch language
function switchLanguage(lang: string) {
    locale.value = lang
    localStorage.setItem('locale', lang)
}

// Load instance list
const loadInstances = async () => {
    loadingInstances.value = true
    try {
        const response = await axios.get<HumpInstance[]>('/Hump/GetInstances')
        lines.value = response.data || []

        const currentSelected = selectedLine.value
        const hasCurrent = currentSelected !== null && lines.value.some(item => item.id === currentSelected)
        if (!hasCurrent) {
            selectedLine.value = lines.value[0]?.id || null
        }
    } catch (error: any) {
        console.error('Failed to load instances:', error)
        lines.value = []
        selectedLine.value = null
        if (error?.response?.status !== 401) {
            ElMessage.error(t('humpMain.messages.loadInstancesError'))
        }
    } finally {
        loadingInstances.value = false
    }
}

// Close instance manager dialog
const handleCloseInstanceManager = (done: () => void) => {
    done()
    // Refresh instance list
    void loadInstances()
}

// Load instances when component is mounted
onMounted(() => {
    void loadInstances()
})
</script>

<style scoped lang="css">
.hump-main {
    width: 100%;
    min-height: 100dvh;
    padding: 24px;
    background-color: white;
    box-sizing: border-box;
    overflow: auto;
}

.hump-tabs-wrapper {
    position: relative;
}

.left-controls {
    position: absolute;
    left: 0;
    top: 0;
    z-index: 1;
    display: flex;
    align-items: center;
    height: 40px;
}

.line-select {
    margin-left: 12px;
    width: 200px;
}

.right-controls {
    position: absolute;
    right: 0;
    top: 0;
    z-index: 1;
    display: flex;
    align-items: center;
    height: 40px;
}

.language-switch {
    margin-left: 10px;
}

.hump-main-tabs {
    margin: 0 auto;
}

.hump-main-tabs > :deep(.el-tabs__header) {
    padding-left: 450px;
    padding-right: 120px;
}

.hump-main-tabs > :deep(.el-tabs__header .el-tabs__nav-wrap) {
    overflow-x: auto;
}

.hump-main-tabs > :deep(.el-tabs__header .el-tabs__nav-scroll) {
    min-width: max-content;
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

@media (max-width: 768px) and (orientation: portrait) {
    .hump-main {
        padding: 12px;
    }

    .hump-tabs-wrapper {
        display: flex;
        flex-direction: column;
        gap: 8px;
    }

    .left-controls,
    .right-controls {
        position: static;
        height: auto;
    }

    .left-controls {
        flex-wrap: wrap;
        gap: 8px;
        align-items: stretch;
    }

    .line-select {
        margin-left: 0;
        width: 100%;
        min-width: 0;
    }

    .right-controls {
        justify-content: flex-end;
    }

    .language-switch {
        margin-left: 0;
    }

    .hump-main-tabs > :deep(.el-tabs__header) {
        padding-left: 0;
        padding-right: 0;
    }
}
</style>
