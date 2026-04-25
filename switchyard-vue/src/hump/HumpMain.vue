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
                    <el-option v-for="line in activeLines" :key="line.id" :label="line.name" :value="line.id" />
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
                <el-tab-pane :label="t('humpMain.tabs.simulation3d')" name="simulation3d" lazy>
                    <HumpSim3D v-if="hasSelectedInstance" :selectedInstanceId="selectedLine" />
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

const activeTab = ref('plan')
const selectedLine = ref<string | null>(null)
const lines = ref<HumpInstance[]>([])
const showInstanceManager = ref(false)
const loadingInstances = ref(false)
const activeLines = computed(() => lines.value.filter((item) => Number(item.isActive) === 1))
const hasSelectedInstance = computed(() => Boolean(selectedLine.value))
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
onMounted(() => {
    void loadInstances()
})
</script>

<style scoped lang="css">
.hump-main {
    width: 100%;
    min-height: 1000px;
    padding: 24px;
    background-color: white;
    box-sizing: border-box;
    overflow-x: auto;
    overflow-y: auto;
}

.hump-tabs-wrapper {
    position: relative;
    min-width: 1000px;
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
    max-width: 120px;
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
}

.hump-main-tabs> :deep(.el-tabs__header) {
    padding-left: 450px;
    padding-right: 380px;
}

.hump-main-tabs> :deep(.el-tabs__header .el-tabs__nav-wrap) {
    overflow-x: auto;
}

.hump-main-tabs> :deep(.el-tabs__header .el-tabs__nav-scroll) {
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
</style>
