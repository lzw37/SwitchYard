<template>
    <section class="capacity-main">
        <div class="capacity-tabs-wrapper">
            <div class="left-controls">
                <el-button type="primary" @click="showInstanceManager = true">
                    {{ t('capacityMain.buttons.instanceManager') }}
                </el-button>
                <el-select
                    v-model="selectedInstance"
                    :placeholder="t('capacityMain.placeholders.selectInstance')"
                    :loading="loadingInstances"
                    :disabled="loadingInstances"
                    style="margin-left: 12px; width: 220px;"
                >
                    <el-option v-for="inst in activeInstances" :key="inst.id" :label="inst.name" :value="inst.id" />
                </el-select>
            </div>
            <div class="right-controls">
                <el-button-group class="language-switch">
                    <el-button size="small" :type="currentLocale === 'zh' ? 'primary' : 'default'"
                        @click="switchLanguage('zh')">
                        {{ t('common.language.zh') }}
                    </el-button>
                    <el-button size="small" :type="currentLocale === 'en' ? 'primary' : 'default'"
                        @click="switchLanguage('en')">
                        {{ t('common.language.en') }}
                    </el-button>
                </el-button-group>
                <el-dropdown class="user-dropdown" @command="handleUserMenuCommand">
                    <span class="user-menu-trigger">
                        <span class="user-menu-name">{{ userDisplayName }}</span>
                        <span class="user-menu-role">{{ userDisplayRole }}</span>
                    </span>
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
                    <div v-if="hasSelectedInstance" class="station-layout-3d-pane">
                        <StationLayout3D
                            :selected-instance-id="selectedInstance || ''"
                            :activation-key="layout3DActivationKey"
                        />
                    </div>
                    <el-empty v-else :description="t('capacityMain.placeholders.selectInstance')" />
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
import { ref, onMounted, computed, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import axios from '@/utils/axios'
import { ElMessage } from 'element-plus'
import { useAuthStore } from '@/stores/auth'
import StationLayout from './StationLayout.vue'
import RouteDesign from './RouteDesign.vue'
import StationLayout3D from './StationLayout3D.vue'
import CapacityInstanceManager from './CapacityInstanceManager.vue'
import UserManagement from '@/views/UserManagement.vue'

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
const layout3DActivationKey = ref(0)
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

watch(activeTab, (tab) => {
    if (tab === 'layout3d') {
        layout3DActivationKey.value += 1
    }
})

watch(selectedInstance, () => {
    if (activeTab.value === 'layout3d') {
        layout3DActivationKey.value += 1
    }
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
    justify-content: flex-end;
    gap: 10px;
    height: 40px;
}

.language-switch {
    margin-left: 0;
}

.user-dropdown {
    display: inline-flex;
}

.user-menu-trigger {
    display: inline-flex;
    align-items: center;
    gap: 8px;
    height: 30px;
    padding: 0 10px;
    border-radius: 6px;
    border: 1px solid #c9d8ea;
    background: linear-gradient(180deg, #f9fbff 0%, #eef4fb 100%);
    color: #1f3a68;
    font-size: 13px;
    cursor: pointer;
    transition: all 0.2s ease;
}

.user-menu-trigger:hover {
    border-color: #8eb0d8;
    background: linear-gradient(180deg, #ffffff 0%, #e8f1fb 100%);
}

.user-menu-name {
    max-width: clamp(64px, 14vw, 120px);
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    font-weight: 600;
}

.user-menu-role {
    padding: 1px 6px;
    border-radius: 999px;
    border: 1px solid #adc4e3;
    color: #24528a;
    background: #f0f6ff;
    font-size: 12px;
}

.capacity-tabs {
    margin: 0 auto;
    width: 100%;
    min-width: 0;
}

.capacity-tabs :deep(.el-tabs__header) {
    padding-left: 450px;
    padding-right: 300px;
}

.capacity-tabs :deep(.el-tabs__content),
.capacity-tabs :deep(.el-tab-pane) {
    width: 100%;
    min-width: 0;
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

.station-layout-pane {
    min-height: 400px;
}

.route-design-pane {
    width: 100%;
    max-width: 100%;
    height: calc(100dvh - 118px);
    min-height: 420px;
    overflow: hidden;
}

.station-layout-3d-pane {
    width: 100%;
    max-width: 100%;
    height: calc(100dvh - 118px);
    min-height: 420px;
    overflow: hidden;
}

@media (max-width: 768px) {
    .capacity-main {
        padding: 16px;
    }

    .capacity-tabs :deep(.el-tabs__header) {
        padding-left: 0;
        padding-right: 0;
        padding-top: 48px;
    }

    .left-controls,
    .right-controls {
        position: static;
        margin-bottom: 8px;
    }

    .capacity-tabs-wrapper {
        display: flex;
        flex-direction: column;
    }

    .station-layout-3d-pane {
        height: calc(100dvh - 188px);
        min-height: 420px;
    }

    .route-design-pane {
        height: calc(100dvh - 188px);
        min-height: 420px;
    }
}

@media (max-width: 560px) {
    .right-controls {
        flex-wrap: wrap;
        justify-content: flex-start;
    }

    .user-menu-role {
        display: none;
    }
}
</style>
