<template>
    <section class="hump-main">
        <div class="top-bar">
            <div class="left-controls">
                <el-button type="primary" @click="showInstanceManager = true">
                    {{ t('humpMain.buttons.instanceManager') }}
                </el-button>
                <el-select v-model="selectedLine" :placeholder="t('humpMain.placeholders.selectInstance')"
                    style="margin-left: 12px; width: 200px;">
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
        </div>
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

        <!-- 实例管理对话框 -->
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
const planSubTab = ref('ctrl')
const showInstanceManager = ref(false)

// 当前语言
const currentLocale = computed(() => locale.value)

// 切换语言
function switchLanguage(lang: string) {
    locale.value = lang
    localStorage.setItem('locale', lang)
}

// 加载实例列表
const loadInstances = async () => {
    try {
        const response = await axios.get('/Hump/GetInstances')
        lines.value = response.data || []
    } catch (error: any) {
        console.error('Failed to load instances:', error)
        ElMessage.error(t('humpMain.messages.loadInstancesError'))
        lines.value = []
    }
}

// 关闭实例管理对话框
const handleCloseInstanceManager = (done: () => void) => {
    done()
    // 刷新实例列表
    loadInstances()
}

// 组件挂载时加载实例
onMounted(() => {
    loadInstances()
})
</script>

<style scoped lang="css">
.hump-main {
    width: 100dvw;
    height: 100dvh;
    padding: 24px;
    background-color: white;
}

.top-bar {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 20px;
}

.left-controls {
    display: flex;
    align-items: center;
}

.right-controls {
    display: flex;
    align-items: center;
}

.language-switch {
    margin-left: 10px;
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
