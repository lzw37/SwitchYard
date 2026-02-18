<template>
    <section class="capacity-main">
        <div class="capacity-tabs-wrapper">
            <div class="left-controls">
                <el-button type="primary" @click="showInstanceManager = true">
                    {{ t('capacityMain.buttons.instanceManager') }}
                </el-button>
                <el-select v-model="selectedInstance" :placeholder="t('capacityMain.placeholders.selectInstance')"
                    style="margin-left: 12px; width: 200px;">
                    <el-option v-for="inst in instances" :key="inst.id" :label="inst.name" :value="inst.id" />
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
            <el-tabs v-model="activeTab" class="capacity-tabs">
                <el-tab-pane :label="t('capacityMain.tabs.stationLayout')" name="stationLayout">
                    <div class="tab-placeholder">
                        <StationLayout />
                    </div>
                </el-tab-pane>
                <el-tab-pane :label="t('capacityMain.tabs.calcParams')" name="calcParams">
                    <div class="tab-placeholder">
                        <el-empty :description="t('capacityMain.placeholders.calcParams')" />
                    </div>
                </el-tab-pane>
                <el-tab-pane :label="t('capacityMain.tabs.operationPlan')" name="operationPlan">
                    <div class="tab-placeholder">
                        <el-empty :description="t('capacityMain.placeholders.operationPlan')" />
                    </div>
                </el-tab-pane>
                <el-tab-pane :label="t('capacityMain.tabs.resultAnalysis')" name="resultAnalysis">
                    <div class="tab-placeholder">
                        <el-empty :description="t('capacityMain.placeholders.resultAnalysis')" />
                    </div>
                </el-tab-pane>
                <el-tab-pane :label="t('capacityMain.tabs.simulation')" name="simulation">
                    <div class="tab-placeholder">
                        <el-empty :description="t('capacityMain.placeholders.simulation')" />
                    </div>
                </el-tab-pane>
            </el-tabs>
        </div>

        <el-dialog v-model="showInstanceManager" :title="t('capacityMain.dialogs.instanceManagerTitle')" width="90%"
            :close-on-click-modal="false" :before-close="handleCloseInstanceManager">
            <div class="instance-manager-placeholder">
                <el-empty :description="t('capacityMain.placeholders.instanceManager')" />
            </div>
        </el-dialog>
    </section>
</template>

<script lang="ts" setup>
import { ref, onMounted, computed } from 'vue'
import { useI18n } from 'vue-i18n'
import axios from '@/utils/axios'
import { ElMessage } from 'element-plus'
import StationLayout from './StationLayout.vue'

const { t, locale } = useI18n()

interface CapacityInstance {
    id: string
    name: string
    owner: string
    createdDate: string
    isActive: number
}

const activeTab = ref('stationLayout')
const selectedInstance = ref<string | null>(null)
const instances = ref<CapacityInstance[]>([])
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
        const response = await axios.get('/Capacity/GetInstances')
        instances.value = response.data || []
    } catch (error: any) {
        console.error('Failed to load capacity instances:', error)
        ElMessage.error(t('capacityMain.messages.loadInstancesError'))
        instances.value = []
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
.capacity-main {
    width: 100dvw;
    height: 100dvh;
    padding: 24px;
    background-color: white;
}

.capacity-tabs-wrapper {
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

.capacity-tabs {
    margin: 0 auto;
}

.capacity-tabs :deep(.el-tabs__header) {
    padding-left: 450px;
    padding-right: 120px;
}

.tab-placeholder {
    display: flex;
    align-items: center;
    justify-content: center;
    min-height: 400px;
    border: 2px dashed #d6e5ef;
    border-radius: 8px;
    background: linear-gradient(180deg, #ffffff 0%, #fbfdff 100%);
}

.instance-manager-placeholder {
    min-height: 300px;
    display: flex;
    align-items: center;
    justify-content: center;
}
</style>
