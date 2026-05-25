<template>
    <section class="hump-main">
        <header class="page-header">
            <span class="page-header-brand">SwitchYard.Hump</span>
            <span class="page-header-sep"></span>
            <span class="page-header-title">驼峰纵断面设计辅助工具</span>
            <a class="page-header-video-link" href="https://www.bilibili.com/video/BV1gMRmBRER9" target="_blank"
                rel="noopener noreferrer">
                教学视频
            </a>
        </header>
        <div ref="tabsHostRef" class="hump-tabs-wrapper">
            <div class="hump-main-toolbar">
                <div class="left-controls">
                    <el-button type="primary" @click="showInstanceManager = true">
                        {{ t('humpMain.buttons.instanceManager') }}
                    </el-button>
                    <el-select v-model="selectedLine" class="line-select"
                        :placeholder="t('humpMain.placeholders.selectInstance')" :loading="loadingInstances"
                        :disabled="loadingInstances">
                        <el-option v-for="line in activeLines" :key="line.id" :label="line.name" :value="line.id" />
                    </el-select>
                </div>
                <div ref="tabSlotRef" class="tab-nav-slot">
                    <div v-show="!tabsInDropdown" class="main-tab-nav" role="tablist">
                        <button v-for="tab in mainTabs" :key="tab.name" type="button" class="main-tab-button"
                            :class="{ 'is-active': activeTab === tab.name }" role="tab"
                            :aria-selected="activeTab === tab.name" @click="activeTab = tab.name">
                            {{ tab.label }}
                        </button>
                    </div>
                    <div v-show="tabsInDropdown" class="tab-dropdown-control">
                        <el-select v-model="activeTab" class="tab-select" :placeholder="activeTabLabel">
                            <el-option v-for="tab in mainTabs" :key="tab.name" :label="tab.label" :value="tab.name" />
                        </el-select>
                    </div>
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
                                <el-dropdown-item v-if="authStore.isAdmin" command="humpInstanceManagement">
                                    {{ t('humpMain.menu.instanceManagement') }}
                                </el-dropdown-item>
                                <el-dropdown-item divided command="logout">
                                    {{ t('common.userMenu.logout') }}
                                </el-dropdown-item>
                            </el-dropdown-menu>
                        </template>
                    </el-dropdown>
                </div>
            </div>
            <div ref="tabMeasureRef" class="tab-measure" aria-hidden="true">
                <span v-for="tab in mainTabs" :key="tab.name" class="tab-measure-item">{{ tab.label }}</span>
            </div>
            <el-tabs v-model="activeTab" class="hump-main-tabs">
                <el-tab-pane :label="getTabLabel('plan')" name="plan" lazy>
                    <HumpLayout v-if="hasSelectedInstance" :selectedInstanceId="selectedLine"
                        :activation-key="tabActivationKeys.plan" />
                    <el-empty v-else :description="t('humpMain.placeholders.selectInstance')" />
                </el-tab-pane>
                <el-tab-pane :label="getTabLabel('vehicle')" name="vehicle" lazy>
                    <template v-if="hasSelectedInstance">
                        <el-card class="param-card" shadow="hover">
                            <h3>{{ t('humpMain.headings.wagonParams') }}</h3>
                            <Wagon :selectedInstanceId="selectedLine" />
                        </el-card>
                        <el-card class="param-card" shadow="hover">
                            <h3>{{ t('humpMain.headings.calcCondition') }}</h3>
                            <HumpCalculationCondition :selectedInstanceId="selectedLine"
                                :activation-key="tabActivationKeys.vehicle" />
                        </el-card>
                    </template>
                    <el-empty v-else :description="t('humpMain.placeholders.selectInstance')" />
                </el-tab-pane>
                <el-tab-pane :label="getTabLabel('profile')" name="profile" lazy>
                    <HumpSlopeDesigner v-if="hasSelectedInstance" :selectedInstanceId="selectedLine"
                        :activation-key="tabActivationKeys.profile" />
                    <el-empty v-else :description="t('humpMain.placeholders.selectInstance')" />
                </el-tab-pane>
                <el-tab-pane :label="getTabLabel('release')" name="release" lazy>
                    <HumpHeadwayCheck v-if="hasSelectedInstance" :selectedInstanceId="selectedLine"
                        :activation-key="tabActivationKeys.release" />
                    <el-empty v-else :description="t('humpMain.placeholders.selectInstance')" />
                </el-tab-pane>
                <el-tab-pane :label="getTabLabel('simulation')" name="simulation" lazy>
                    <HumpSim v-if="hasSelectedInstance" :selectedInstanceId="selectedLine"
                        :activation-key="tabActivationKeys.simulation" />
                    <el-empty v-else :description="t('humpMain.placeholders.selectInstance')" />
                </el-tab-pane>
                <el-tab-pane :label="getTabLabel('simulation3d')" name="simulation3d" lazy>
                    <HumpSim3D v-if="hasSelectedInstance" :selectedInstanceId="selectedLine"
                        :activation-key="tabActivationKeys.simulation3d" />
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
import { ref, onMounted, computed, nextTick, onBeforeUnmount, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import axios from '@/utils/axios'
import { ElMessage } from 'element-plus'
import { useAuthStore } from '@/stores/auth'
import HumpLayout from './HumpLayout.vue';
import HumpSlopeDesigner from './HumpSlopeDesigner.vue';
import Wagon from './Wagon.vue';
import HumpCalculationCondition from './HumpCalculationCondition.vue';
import HumpHeadwayCheck from './HumpHeadwayCheck.vue';
import HumpSim from './HumpSim.vue';
import HumpSim3D from './HumpSim3D.vue';
import HumpInstanceManager from './HumpInstanceManager.vue';

const router = useRouter()
const { t, locale } = useI18n()
const authStore = useAuthStore()

authStore.hydrateFromStorage()

interface HumpInstance {
    id: string
    name: string
    owner: string
    createdDate: string
    isActive: number
}

type MainTabName = 'plan' | 'vehicle' | 'profile' | 'release' | 'simulation' | 'simulation3d'

const activeTab = ref('plan')
const selectedLine = ref<string | null>(null)
const lines = ref<HumpInstance[]>([])
const showInstanceManager = ref(false)
const loadingInstances = ref(false)
const tabsHostRef = ref<HTMLElement | null>(null)
const tabSlotRef = ref<HTMLElement | null>(null)
const tabMeasureRef = ref<HTMLElement | null>(null)
const tabsInDropdown = ref(false)
const tabActivationKeys = ref<Record<MainTabName, number>>({
    plan: 0,
    vehicle: 0,
    profile: 0,
    release: 0,
    simulation: 0,
    simulation3d: 0,
})
const activeLines = computed(() => lines.value.filter((item) => Number(item.isActive) === 1))
const hasSelectedInstance = computed(() => Boolean(selectedLine.value))
const mainTabs = computed(() => [
    { name: 'plan', label: t('humpMain.tabs.plan') },
    { name: 'vehicle', label: t('humpMain.tabs.vehicle') },
    { name: 'profile', label: t('humpMain.tabs.profile') },
    { name: 'release', label: t('humpMain.tabs.release') },
    { name: 'simulation', label: t('humpMain.tabs.simulation') },
    { name: 'simulation3d', label: t('humpMain.tabs.simulation3d') },
])
const activeTabLabel = computed(() => getTabLabel(activeTab.value))
const userDisplayName = computed(() => authStore.username.trim() || t('common.userMenu.guest'))
const userDisplayRole = computed(() => {
    const role = authStore.role.trim()
    if (!role) return t('createUser.roles.user')

    const normalizedRole = role.toLowerCase()
    if (normalizedRole === 'admin') return t('createUser.roles.admin')
    if (normalizedRole === 'user') return t('createUser.roles.user')

    return role
})

// Current language
const currentLocale = computed(() => locale.value)

const getTabLabel = (tabName: string) => {
    return mainTabs.value.find((tab) => tab.name === tabName)?.label || tabName
}

const bumpTabActivationKey = (tabName: MainTabName) => {
    tabActivationKeys.value[tabName] += 1
}

const updateTabDisplayMode = () => {
    nextTick(() => {
        const hostWidth = Math.floor(tabSlotRef.value?.clientWidth || tabsHostRef.value?.clientWidth || 0)
        const measureItems = Array.from(tabMeasureRef.value?.children || []) as HTMLElement[]

        if (hostWidth <= 0 || measureItems.length === 0) {
            tabsInDropdown.value = false
            return
        }

        const availableWidth = Math.max(0, hostWidth - 8)
        let visibleCount = 0
        let usedWidth = 0

        for (const item of measureItems) {
            const itemWidth = Math.ceil(item.getBoundingClientRect().width)
            if (usedWidth + itemWidth > availableWidth) break
            usedWidth += itemWidth
            visibleCount += 1
        }

        tabsInDropdown.value = visibleCount < 2
    })
}

// Switch language
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
        router.push('/usermanagement')
        return
    }

    if (command === 'humpInstanceManagement') {
        router.push('/hump/instancemanagement')
        return
    }

    if (command === 'logout') {
        authStore.clearAuth()
        ElMessage.success(t('common.userMenu.loggedOut'))
        router.replace('/login')
    }
}

// Load instance list
const loadInstances = async () => {
    loadingInstances.value = true
    try {
        const response = await axios.get<HumpInstance[]>('/Hump/GetInstances')
        lines.value = (response.data || []).map((item) => ({
            ...item,
            isActive: Number(item.isActive),
        }))

        const currentSelected = selectedLine.value
        const hasCurrent = currentSelected !== null && activeLines.value.some(item => item.id === currentSelected)
        if (!hasCurrent) {
            selectedLine.value = activeLines.value[0]?.id || null
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
let tabsResizeObserver: ResizeObserver | null = null

onMounted(() => {
    void loadInstances()
    updateTabDisplayMode()

    nextTick(() => {
        if (typeof ResizeObserver !== 'undefined' && tabsHostRef.value) {
            tabsResizeObserver = new ResizeObserver(() => updateTabDisplayMode())
            tabsResizeObserver.observe(tabsHostRef.value)
            if (tabSlotRef.value) tabsResizeObserver.observe(tabSlotRef.value)
            if (tabMeasureRef.value) tabsResizeObserver.observe(tabMeasureRef.value)
        } else {
            window.addEventListener('resize', updateTabDisplayMode)
        }
    })
})

onBeforeUnmount(() => {
    if (tabsResizeObserver) {
        tabsResizeObserver.disconnect()
        tabsResizeObserver = null
    }
    window.removeEventListener('resize', updateTabDisplayMode)
})

watch(currentLocale, () => {
    updateTabDisplayMode()
})

watch(activeTab, (newTab, oldTab) => {
    if (!newTab || newTab === oldTab) return
    bumpTabActivationKey(newTab as MainTabName)
})
</script>

<style scoped lang="css">
.hump-main {
    width: 100%;
    min-height: 100dvh;
    padding: 0 24px 24px;
    background-color: white;
    box-sizing: border-box;
    overflow-x: hidden;
    overflow-y: auto;
}

.page-header {
    height: 32px;
    line-height: 28px;
    margin: 0 -24px 10px;
    padding: 0 20px;
    background: linear-gradient(90deg, #1a3860 0%, #254e8e 55%, #3568b0 100%);
    display: flex;
    align-items: center;
    /* justify-content: center; */
    gap: 0;
    user-select: none;
    flex-shrink: 0;
}

.page-header-brand {
    font-family: 'Consolas', 'Monaco', monospace;
    font-weight: 700;
    font-size: 15px;
    color: #ffffff;
    letter-spacing: 0.03em;
}

.page-header-sep {
    display: inline-block;
    width: 1px;
    height: 13px;
    background: rgba(255, 255, 255, 0.3);
    margin: 0 10px;
    flex-shrink: 0;
}

.page-header-title {
    color: #aecfe8;
    font-size: 14px;
    font-weight: 600;
    letter-spacing: 0.04em;
}

.page-header-video-link {
    margin-left: auto;
    color: #ffffff;
    font-size: 14px;
    font-weight: 600;
    line-height: 32px;
    text-decoration: none;
    white-space: nowrap;
}

.page-header-video-link:hover {
    color: #d7ebff;
    text-decoration: underline;
}

.hump-tabs-wrapper {
    position: relative;
    width: 100%;
    min-width: 0;
    max-width: 100%;
}

.hump-main-toolbar {
    display: grid;
    grid-template-columns: auto minmax(0, 1fr) auto;
    align-items: center;
    gap: 0 14px;
    width: 100%;
    margin-bottom: 10px;
    border-bottom: 1px solid #e4e7ed;
}

.left-controls {
    display: flex;
    align-items: center;
    flex-wrap: nowrap;
    gap: 10px;
    min-width: 0;
}

.line-select {
    flex: 0 1 200px;
    width: clamp(150px, 18vw, 200px);
    min-width: 0;
    max-width: 200px;
}

.tab-nav-slot {
    display: flex;
    align-self: stretch;
    align-items: flex-end;
    min-width: 0;
    overflow: hidden;
}

.main-tab-nav {
    display: flex;
    align-items: flex-end;
    min-width: 0;
    max-width: 100%;
    height: 40px;
    overflow-x: auto;
    overflow-y: hidden;
    white-space: nowrap;
    scrollbar-width: thin;
}

.main-tab-button {
    flex: 0 0 auto;
    height: 40px;
    padding: 0 20px;
    border: none;
    border-bottom: 2px solid transparent;
    background: transparent;
    color: #303133;
    font: inherit;
    font-size: 14px;
    font-weight: 500;
    line-height: 40px;
    white-space: nowrap;
    cursor: pointer;
}

.main-tab-button:hover {
    color: #409eff;
}

.main-tab-button.is-active {
    color: #409eff;
    border-bottom-color: #409eff;
}

.tab-dropdown-control {
    display: flex;
    justify-content: center;
    align-items: center;
    width: 100%;
    min-width: 0;
}

.tab-select {
    width: min(320px, 100%);
}

.tab-measure {
    position: absolute;
    left: 0;
    top: 0;
    height: 0;
    overflow: hidden;
    visibility: hidden;
    white-space: nowrap;
    pointer-events: none;
}

.tab-measure-item {
    display: inline-flex;
    align-items: center;
    height: 40px;
    padding: 0 20px;
    box-sizing: border-box;
    color: #303133;
    font-size: 14px;
    font-weight: 500;
}

.right-controls {
    display: flex;
    align-items: center;
    justify-content: flex-end;
    flex-wrap: nowrap;
    gap: 10px;
    min-width: 0;
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

.hump-main-tabs {
    margin: 0 auto;
    min-width: 0;
    max-width: 100%;
}

.hump-main-tabs> :deep(.el-tabs__header) {
    display: none;
}

.hump-main-tabs> :deep(.el-tabs__header .el-tabs__nav-wrap) {
    min-width: 0;
}

.hump-main-tabs> :deep(.el-tabs__content) {
    min-width: 0;
}

.hump-main-tabs :deep(#pane-simulation3d) {
    height: calc(100dvh - 104px);
    max-height: calc(100dvh - 104px);
    min-height: 0;
    overflow: hidden;
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
    max-width: 100%;
    overflow-x: auto;
}

@media (max-width: 768px) {
    .hump-main {
        padding: 0 12px 12px;
    }

    .page-header {
        margin: 0 -12px 8px;
    }

    .hump-main-toolbar {
        grid-template-columns: auto minmax(96px, 1fr) auto;
        gap: 0 8px;
    }

    .left-controls {
        gap: 8px;
    }

    .right-controls {
        gap: 8px;
    }

    .line-select {
        width: clamp(128px, 20vw, 170px);
    }

    .main-tab-button,
    .tab-measure-item {
        padding: 0 14px;
    }

    .user-menu-trigger {
        padding: 0 8px;
        gap: 6px;
    }
}

@media (max-width: 560px) {
    .hump-main-toolbar {
        grid-template-columns: minmax(0, 1fr) minmax(92px, 120px) auto;
    }

    .left-controls {
        flex-wrap: wrap;
        gap: 6px;
    }

    .line-select {
        width: 120px;
    }

    .tab-select {
        width: 100%;
    }

    .user-menu-role {
        display: none;
    }
}
</style>
