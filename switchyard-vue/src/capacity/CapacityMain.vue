<template>
    <section class="capacity-main">
        <div class="capacity-tabs-wrapper">
            <div class="workspace-topbar">
            <div class="left-controls">
                <ActionButton :label="t('capacityMain.buttons.instanceManager')" :icon="actionIcons.folder" @click="showInstanceManager = true" />
                <el-select
                    v-model="selectedInstance"
                    :placeholder="t('capacityMain.placeholders.selectInstance')"
                    :loading="loadingInstances"
                    :disabled="loadingInstances"
                    class="instance-select"
                >
                    <el-option v-for="inst in activeInstances" :key="inst.id" :label="inst.name" :value="inst.id" />
                </el-select>
            </div>
            <div class="right-controls">
                <ActionButton :label="t(currentLocale === 'zh' ? 'common.language.en' : 'common.language.zh')"
                    @click="switchLanguage(currentLocale === 'zh' ? 'en' : 'zh')">
                    <span class="language-code">{{ currentLocale === 'zh' ? 'EN' : '中' }}</span>
                </ActionButton>
                <el-dropdown class="user-dropdown" @command="handleUserMenuCommand">
                    <button class="user-menu-trigger" type="button" :aria-label="`${userDisplayName} · ${userDisplayRole}`"
                        :title="`${userDisplayName} · ${userDisplayRole}`"><el-icon><User /></el-icon></button>
                    <template #dropdown>
                        <el-dropdown-menu>
                            <el-dropdown-item command="userinfo">
                                {{ t('userInfo.title') }}
                            </el-dropdown-item>
                            <el-dropdown-item v-if="authStore.isAdmin" command="usermanagement">
                                {{ t('common.userMenu.userManagement') }}
                            </el-dropdown-item>
                            <el-dropdown-item divided command="logout">
                                {{ t('common.userMenu.logout') }}
                            </el-dropdown-item>
                        </el-dropdown-menu>
                    </template>
                </el-dropdown>
            </div>
            </div>
            <el-tabs v-model="activeTab" class="capacity-tabs">
                <el-tab-pane :label="t('capacityMain.tabs.stationLayout')" name="stationLayout">
                    <div v-if="hasSelectedInstance" class="station-layout-pane">
                        <StationLayout :selected-instance-id="selectedInstance || ''" />
                    </div>
                    <el-empty v-else :description="t('capacityMain.placeholders.selectInstance')" />
                </el-tab-pane>
                <el-tab-pane :label="t('capacityMain.tabs.routeDesign')" name="routeDesign" lazy>
                    <div v-if="hasSelectedInstance" class="route-design-pane">
                        <RouteDesign :selected-instance-id="selectedInstance || ''" />
                    </div>
                    <el-empty v-else :description="t('capacityMain.placeholders.selectInstance')" />
                </el-tab-pane>
                <el-tab-pane :label="t('capacityMain.tabs.layout3d')" name="layout3d" lazy>
                    <div v-if="activeTab === 'layout3d' && hasSelectedInstance" class="station-layout-3d-pane">
                        <StationLayout3D :selected-instance-id="selectedInstance || ''" />
                    </div>
                    <el-empty v-else-if="activeTab === 'layout3d'" :description="t('capacityMain.placeholders.selectInstance')" />
                </el-tab-pane>
                <el-tab-pane :label="t('capacityMain.tabs.calcParams')" name="calcParams" lazy>
                    <div v-if="hasSelectedInstance" class="calculation-parameters-pane">
                        <CalculationParameters :selected-instance-id="selectedInstance || ''" />
                    </div>
                    <el-empty v-else :description="t('capacityMain.placeholders.selectInstance')" />
                </el-tab-pane>
                <el-tab-pane :label="t('capacityMain.tabs.operationPlan')" name="operationPlan">
                    <div v-if="hasSelectedInstance" class="operation-plan-pane">
                        <OperationPlan :selected-instance-id="selectedInstance || ''" />
                    </div>
                    <el-empty v-else :description="t('capacityMain.placeholders.selectInstance')" />
                </el-tab-pane>
                <el-tab-pane :label="t('capacityMain.tabs.modelSolving')" name="modelSolving" lazy>
                    <div v-if="hasSelectedInstance" class="model-solving-pane">
                        <ModelSolving :selected-instance-id="selectedInstance || ''" />
                    </div>
                    <el-empty v-else :description="t('capacityMain.placeholders.selectInstance')" />
                </el-tab-pane>
                <el-tab-pane :label="t('capacityMain.tabs.resultAnalysis')" name="resultAnalysis">
                    <div class="tab-placeholder">
                        <el-empty :description="t('capacityMain.placeholders.resultAnalysis')" />
                    </div>
                </el-tab-pane>
                <el-tab-pane :label="t('capacityMain.tabs.simulation')" name="simulation">
                    <div v-if="hasSelectedInstance" class="operation-simulation-pane">
                        <OperationSimulation :selected-instance-id="selectedInstance || ''" />
                    </div>
                    <el-empty v-else :description="t('capacityMain.placeholders.selectInstance')" />
                </el-tab-pane>
            </el-tabs>
        </div>

        <el-dialog v-model="showInstanceManager" :title="t('capacityMain.dialogs.instanceManagerTitle')" width="90%"
            :close-on-click-modal="false" :before-close="handleCloseInstanceManager">
            <CapacityInstanceManager @instances-changed="loadInstances" />
        </el-dialog>
        <el-dialog
            v-model="showUserManagement"
            :title="t('common.userMenu.userManagement')"
            width="96%"
            :close-on-click-modal="false"
        >
            <UserManagement />
        </el-dialog>
    </section>
</template>

<script lang="ts" setup>
import { ref, onMounted, computed, defineAsyncComponent } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import axios from '@/utils/axios'
import { ElMessage } from 'element-plus'
import { useAuthStore } from '@/stores/auth'
import ActionButton from '@/components/ui/ActionButton.vue'
import { actionIcons } from '@/components/ui/actionIcons'
import { User } from '@element-plus/icons-vue'
import StationLayout from './StationLayout.vue'
import RouteDesign from './RouteDesign.vue'
import CalculationParameters from './CalculationParameters.vue'
import OperationPlan from './OperationPlan.vue'
import OperationSimulation from './OperationSimulation.vue'
import ModelSolving from './ModelSolving.vue'
import CapacityInstanceManager from './CapacityInstanceManager.vue'
import UserManagement from '@/views/UserManagement.vue'

const StationLayout3D = defineAsyncComponent(() => import('./StationLayout3D.vue'))

const router = useRouter()
const { t, locale } = useI18n()
const authStore = useAuthStore()

authStore.hydrateFromStorage()

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
const showUserManagement = ref(false)
const loadingInstances = ref(false)
const activeInstances = computed(() => instances.value.filter((item) => Number(item.isActive) === 1))
const hasSelectedInstance = computed(() => Boolean(selectedInstance.value))
const userDisplayName = computed(() => authStore.username.trim() || t('common.userMenu.guest'))
const userDisplayRole = computed(() => {
    const role = authStore.role.trim()
    if (!role) return t('createUser.roles.user')

    const normalizedRole = role.toLowerCase()
    if (normalizedRole === 'admin') return t('createUser.roles.admin')
    if (normalizedRole === 'user') return t('createUser.roles.user')

    return role
})

// 当前语言
const currentLocale = computed(() => locale.value)

// 切换语言
function switchLanguage(lang: string) {
    locale.value = lang
    localStorage.setItem('locale', lang)
}

const handleUserMenuCommand = (command: string) => {
    if (command === 'userinfo') {
        router.push('/userinfo')
        return
    }

    if (command === 'usermanagement') {
        showUserManagement.value = true
        return
    }

    if (command === 'logout') {
        authStore.clearAuth()
        ElMessage.success(t('common.userMenu.loggedOut'))
        router.replace('/login')
    }
}

// 加载实例列表
const loadInstances = async () => {
    loadingInstances.value = true
    try {
        const response = await axios.get<CapacityInstance[]>('/Capacity/GetInstances')
        instances.value = (response.data || []).map((item) => ({
            ...item,
            isActive: Number(item.isActive ?? 1),
        }))

        if (!activeInstances.value.some((item) => item.id === selectedInstance.value)) {
            selectedInstance.value = activeInstances.value[0]?.id || null
        }
    } catch (error: any) {
        console.error('Failed to load capacity instances:', error)
        ElMessage.error(t('capacityMain.messages.loadInstancesError'))
        instances.value = []
        selectedInstance.value = null
    } finally {
        loadingInstances.value = false
    }
}

// 关闭实例管理对话框
const handleCloseInstanceManager = (done: () => void) => {
    done()
    void loadInstances()
}

// 组件挂载时加载实例
onMounted(() => {
    void loadInstances()
})

</script>

<style scoped>
.capacity-main { display:flex; flex-direction:column; width:100%; height:100dvh; padding:12px 16px; overflow:hidden; background:var(--sy-surface,#fff); }
.capacity-tabs-wrapper { display:flex; flex:1; flex-direction:column; min-height:0; min-width:0; }
.workspace-topbar { display:flex; justify-content:space-between; align-items:center; gap:12px; padding-bottom:8px; flex:0 0 auto; }
.left-controls, .right-controls { display:flex; align-items:center; gap:8px; min-width:0; }
.instance-select { width:220px; max-width:calc(100vw - 156px); }
.language-code { font-size:11px; font-weight:600; }
.user-menu-trigger { display:inline-flex; align-items:center; justify-content:center; width:30px; height:30px; border:1px solid var(--sy-border,#dfe4ea); border-radius:50%; color:var(--el-color-primary); background:#fff; cursor:pointer; }
.user-menu-trigger:hover { background:var(--el-color-primary-light-9); }
.capacity-tabs { display:flex; flex:1; flex-direction:column; width:100%; min-width:0; min-height:0; overflow:hidden; }
.capacity-tabs :deep(> .el-tabs__header) { margin-bottom:10px; }
.capacity-tabs :deep(> .el-tabs__content), .capacity-tabs :deep(> .el-tabs__content > .el-tab-pane) { width:100%; min-width:0; min-height:0; overflow:hidden; }
.capacity-tabs :deep(> .el-tabs__content) { flex:1; }
.capacity-tabs :deep(> .el-tabs__content > .el-tab-pane) { height:100%; }
.station-layout-pane, .route-design-pane, .station-layout-3d-pane, .calculation-parameters-pane, .operation-plan-pane, .operation-simulation-pane, .model-solving-pane { width:100%; height:100%; min-width:0; min-height:0; overflow:hidden; }
.tab-placeholder { display:flex; height:100%; align-items:center; justify-content:center; }
@media (max-width:768px) { .capacity-main { padding:8px; } }
</style>
