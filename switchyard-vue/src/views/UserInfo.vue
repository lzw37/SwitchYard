<template>
    <div class="user-info-page" v-loading="loadingUserInfo">
        <div class="page-head">
            <div class="page-head-main">
                <h1>{{ t('userInfo.title') }}</h1>
                <el-button type="danger" plain @click="logoutCurrentAccount">
                    {{ t('common.userMenu.logout') }}
                </el-button>
            </div>
            <p class="sub-text">{{ profileForm.username || '-' }}</p>
        </div>

        <el-alert
            v-if="isForcedPasswordChange"
            title="首次登录或管理员重置密码后，必须先修改密码才能继续使用系统。"
            type="warning"
            :closable="false"
            show-icon
            class="force-password-alert"
        />

        <div class="cards">
            <section class="card">
                <h2>{{ t('userInfo.title') }}</h2>
                <el-form
                    ref="profileFormRef"
                    :model="profileForm"
                    :rules="profileRules"
                    label-position="top"
                >
                    <el-form-item :label="t('userInfo.labels.username')">
                        <el-input v-model="profileForm.username" disabled />
                    </el-form-item>

                    <el-form-item :label="t('userInfo.labels.email')" prop="email">
                        <el-input
                            v-model="profileForm.email"
                            :disabled="isForcedPasswordChange"
                            clearable
                            placeholder="name@example.com"
                        />
                    </el-form-item>

                    <el-form-item :label="t('userInfo.labels.role')">
                        <el-input v-model="profileForm.role" disabled />
                    </el-form-item>

                    <el-form-item label="Created At">
                        <el-input :model-value="createdAtText" disabled />
                    </el-form-item>

                    <el-form-item>
                        <el-button
                            type="primary"
                            :loading="savingProfile"
                            :disabled="isForcedPasswordChange"
                            @click="saveProfile"
                        >
                            {{ t('userInfo.save') }}
                        </el-button>
                    </el-form-item>
                </el-form>
            </section>

            <section class="card">
                <h2>{{ t('userInfo.changePassword.title') }}</h2>
                <el-form
                    ref="passwordFormRef"
                    :model="passwordForm"
                    :rules="passwordRules"
                    label-position="top"
                >
                    <el-form-item :label="t('userInfo.changePassword.current')" prop="currentPassword">
                        <el-input
                            v-model="passwordForm.currentPassword"
                            type="password"
                            show-password
                            autocomplete="current-password"
                        />
                    </el-form-item>

                    <el-form-item :label="t('userInfo.changePassword.new')" prop="newPassword">
                        <el-input
                            v-model="passwordForm.newPassword"
                            type="password"
                            show-password
                            autocomplete="new-password"
                        />
                    </el-form-item>

                    <el-form-item :label="t('userInfo.changePassword.confirm')" prop="confirmPassword">
                        <el-input
                            v-model="passwordForm.confirmPassword"
                            type="password"
                            show-password
                            autocomplete="new-password"
                        />
                    </el-form-item>

                    <el-form-item>
                        <el-button
                            type="warning"
                            :loading="changingPassword"
                            @click="changePassword"
                        >
                            {{ t('userInfo.changePassword.submit') }}
                        </el-button>
                    </el-form-item>
                </el-form>
            </section>
        </div>
    </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute, useRouter } from 'vue-router'
import axios from '@/utils/axios'
import { useAuthStore } from '@/stores/auth'
import { ElMessage } from 'element-plus'
import type { FormInstance, FormRules } from 'element-plus'
import CryptoJS from 'crypto-js'

interface ProfileFormModel {
    id: string
    username: string
    email: string
    role: string
    createdAt: string
}

interface PasswordFormModel {
    currentPassword: string
    newPassword: string
    confirmPassword: string
}

const router = useRouter()
const route = useRoute()
const { t } = useI18n()
const authStore = useAuthStore()

const profileFormRef = ref<FormInstance>()
const passwordFormRef = ref<FormInstance>()

const loadingUserInfo = ref(false)
const savingProfile = ref(false)
const changingPassword = ref(false)

const profileForm = reactive<ProfileFormModel>({
    id: '',
    username: '',
    email: '',
    role: '',
    createdAt: ''
})

const passwordForm = reactive<PasswordFormModel>({
    currentPassword: '',
    newPassword: '',
    confirmPassword: ''
})

const isEmailValid = (value: string): boolean => {
    if (!value) {
        return true
    }

    return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value)
}

const profileRules = reactive<FormRules<ProfileFormModel>>({
    email: [
        {
            validator: (_rule, value, callback) => {
                const email = (value || '').trim()
                if (!isEmailValid(email)) {
                    callback(new Error(t('userInfo.messages.saveFailed') as string))
                    return
                }

                callback()
            },
            trigger: ['blur', 'change']
        }
    ]
})

const passwordRules = reactive<FormRules<PasswordFormModel>>({
    currentPassword: [
        {
            required: true,
            message: t('userInfo.messages.passwordIncomplete'),
            trigger: 'blur'
        }
    ],
    newPassword: [
        {
            required: true,
            message: t('userInfo.messages.passwordIncomplete'),
            trigger: 'blur'
        },
        {
            min: 6,
            max: 30,
            message: t('login.validation.passwordLength'),
            trigger: ['blur', 'change']
        }
    ],
    confirmPassword: [
        {
            required: true,
            message: t('userInfo.messages.passwordIncomplete'),
            trigger: 'blur'
        },
        {
            validator: (_rule, value, callback) => {
                if (value !== passwordForm.newPassword) {
                    callback(new Error(t('userInfo.messages.passwordMismatch') as string))
                    return
                }

                callback()
            },
            trigger: ['blur', 'change']
        }
    ]
})

const createdAtText = computed(() => formatDateTime(profileForm.createdAt))
const isForcedPasswordChange = computed(() => {
    const queryFlag = route.query.forcePasswordChange
    const forcedByQuery = queryFlag === '1' || queryFlag === 'true'
    return authStore.needsPasswordChange || forcedByQuery
})

const formatDateTime = (value: string): string => {
    if (!value) {
        return '-'
    }

    const date = new Date(value)
    if (Number.isNaN(date.getTime())) {
        return value
    }

    return date.toLocaleString()
}

const hashPassword = (password: string): string => {
    return CryptoJS.SHA256(password).toString()
}

const clearPasswordForm = () => {
    passwordForm.currentPassword = ''
    passwordForm.newPassword = ''
    passwordForm.confirmPassword = ''
    passwordFormRef.value?.clearValidate()
}

const clearAuthAndRedirectToLogin = () => {
    authStore.clearAuth()
    router.replace('/login')
}

const logoutCurrentAccount = () => {
    authStore.clearAuth()
    ElMessage.success(t('common.userMenu.loggedOut') as string)
    router.replace('/login')
}

const loadUserInfo = async () => {
    loadingUserInfo.value = true
    try {
        const response = await axios.get('/api/Auth/userinfo')
        const data = response.data || {}

        profileForm.id = data.id || ''
        profileForm.username = data.username || ''
        profileForm.email = data.email || ''
        profileForm.role = data.role || ''
        profileForm.createdAt = data.createdAt || ''
        authStore.setMustChangePassword(data.mustChangePassword === true)

        authStore.updateProfile({
            username: profileForm.username,
            role: profileForm.role
        })
    } catch (error: any) {
        ElMessage.error(error?.response?.data?.message || (t('userInfo.messages.saveFailed') as string))
    } finally {
        loadingUserInfo.value = false
    }
}

const saveProfile = async () => {
    if (isForcedPasswordChange.value) {
        ElMessage.warning('请先完成密码修改')
        return
    }

    if (!profileFormRef.value) {
        return
    }

    const valid = await profileFormRef.value.validate().catch(() => false)
    if (!valid) {
        return
    }

    savingProfile.value = true
    try {
        await axios.put('/api/Auth/userinfo', {
            email: profileForm.email.trim() || null
        })

        ElMessage.success(t('userInfo.messages.saveSuccess') as string)
        await loadUserInfo()
    } catch (error: any) {
        ElMessage.error(error?.response?.data?.message || (t('userInfo.messages.saveFailed') as string))
    } finally {
        savingProfile.value = false
    }
}

const changePassword = async () => {
    if (!passwordFormRef.value) {
        return
    }

    const valid = await passwordFormRef.value.validate().catch(() => false)
    if (!valid) {
        return
    }

    if (passwordForm.currentPassword === passwordForm.newPassword) {
        ElMessage.warning(t('userInfo.changePassword.failed') as string)
        return
    }

    if (passwordForm.newPassword.length < 6 || passwordForm.newPassword.length > 30) {
        ElMessage.warning(t('login.validation.passwordLength') as string)
        return
    }

    changingPassword.value = true
    try {
        await axios.post('/api/Auth/changepassword', {
            currentPassword: hashPassword(passwordForm.currentPassword),
            newPassword: hashPassword(passwordForm.newPassword)
        })

        authStore.setMustChangePassword(false)
        ElMessage.success(t('userInfo.changePassword.success') as string)
        clearPasswordForm()
        setTimeout(() => {
            clearAuthAndRedirectToLogin()
        }, 800)
    } catch (error: any) {
        ElMessage.error(error?.response?.data?.message || (t('userInfo.changePassword.failed') as string))
    } finally {
        changingPassword.value = false
    }
}

onMounted(() => {
    loadUserInfo()
})
</script>

<style scoped>
.user-info-page {
    max-width: 1100px;
    margin: 24px auto;
    padding: 0 16px;
}

.page-head {
    margin-bottom: 18px;
}

.page-head-main {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 12px;
}

.force-password-alert {
    margin-bottom: 16px;
}

.page-head h1 {
    margin: 0;
    font-size: 28px;
    color: #1f2937;
}

.sub-text {
    margin: 6px 0 0;
    color: #6b7280;
}

.cards {
    display: grid;
    grid-template-columns: repeat(2, minmax(0, 1fr));
    gap: 16px;
}

.card {
    background: #fff;
    border: 1px solid #e5e7eb;
    border-radius: 10px;
    padding: 20px;
    box-shadow: 0 8px 24px rgba(15, 23, 42, 0.05);
}

.card h2 {
    margin: 0 0 14px;
    font-size: 20px;
    color: #111827;
}

@media (max-width: 900px) {
    .cards {
        grid-template-columns: 1fr;
    }
}
</style>
