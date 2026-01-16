<template>
    <div class="user-info-container">
        <div class="card">
            <h2>{{ t('userInfo.title') }}</h2>
            <el-form :model="user" ref="userFormRef" label-width="100px">
                <el-form-item :label="t('userInfo.labels.username')">
                    <el-input v-model="user.username" disabled />
                </el-form-item>
                <el-form-item :label="t('userInfo.labels.email')">
                    <el-input v-model="user.email" />
                </el-form-item>
                <el-form-item :label="t('userInfo.labels.role')">
                    <el-input v-model="user.role" disabled />
                </el-form-item>
                <el-form-item>
                    <el-button type="primary" @click="saveProfile" :loading="saving">{{ t('userInfo.save')
                        }}</el-button>
                </el-form-item>
            </el-form>
        </div>

        <div class="card">
            <h2>{{ t('userInfo.changePassword.title') }}</h2>
            <el-form :model="pwdForm" ref="pwdFormRef" label-width="120px">
                <el-form-item :label="t('userInfo.changePassword.current')">
                    <el-input v-model="pwdForm.currentPassword" type="password" show-password />
                </el-form-item>
                <el-form-item :label="t('userInfo.changePassword.new')">
                    <el-input v-model="pwdForm.newPassword" type="password" show-password />
                </el-form-item>
                <el-form-item :label="t('userInfo.changePassword.confirm')">
                    <el-input v-model="pwdForm.confirmPassword" type="password" show-password />
                </el-form-item>
                <el-form-item>
                    <el-button type="warning" @click="changePassword" :loading="changing">{{
                        t('userInfo.changePassword.submit') }}</el-button>
                </el-form-item>
            </el-form>
        </div>
    </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import axios from '@/utils/axios'
import { ElMessage } from 'element-plus'

interface UserInfo {
    id?: string
    username: string
    email?: string
    role?: string
    createdAt?: string
}

const router = useRouter()
const { t } = useI18n()
const userFormRef = ref()
const pwdFormRef = ref()
const saving = ref(false)
const changing = ref(false)

const user = reactive<UserInfo>({ username: '', email: '', role: '' })
const pwdForm = reactive({ currentPassword: '', newPassword: '', confirmPassword: '' })

const hashPassword = async (password: string): Promise<string> => {
    const encoder = new TextEncoder()
    const data = encoder.encode(password)
    const hashBuffer = await crypto.subtle.digest('SHA-256', data)
    const hashArray = Array.from(new Uint8Array(hashBuffer))
    return hashArray.map(b => b.toString(16).padStart(2, '0')).join('')
}

const loadUserInfo = async () => {
    try {
        const resp = await axios.get('/api/Auth/userinfo')
        const d = resp.data
        user.id = d.id
        user.username = d.username
        user.email = d.email
        user.role = d.role
        user.createdAt = d.createdAt
    } catch (err) {
        console.error(t('userInfo.messages.saveFailed'), err)
    }
}

const saveProfile = async () => {
    saving.value = true
    try {
        // 假设后端支持 PUT /api/Auth/userinfo 接收 { email }
        await axios.put('/api/Auth/userinfo', { email: user.email })
        ElMessage.success(t('userInfo.messages.saveSuccess') as string)
        await loadUserInfo()
    } catch (err: any) {
        console.error(err)
        ElMessage.error(err?.response?.data?.message || t('userInfo.messages.saveFailed') as string)
    } finally {
        saving.value = false
    }
}

const changePassword = async () => {
    if (!pwdForm.currentPassword || !pwdForm.newPassword) {
        ElMessage.error(t('userInfo.messages.passwordIncomplete') as string)
        return
    }
    if (pwdForm.newPassword !== pwdForm.confirmPassword) {
        ElMessage.error(t('userInfo.messages.passwordMismatch') as string)
        return
    }

    changing.value = true
    try {
        const currentHash = await hashPassword(pwdForm.currentPassword)
        const newHash = await hashPassword(pwdForm.newPassword)
        // 假设后端提供 POST /api/Auth/changepassword 接口
        await axios.post('/api/Auth/changepassword', {
            currentPassword: currentHash,
            newPassword: newHash,
        })
        ElMessage.success(t('userInfo.changePassword.success') as string)
        // 清理本地认证并返回登录页
        localStorage.removeItem('token')
        localStorage.removeItem('tokenType')
        localStorage.removeItem('username')
        localStorage.removeItem('role')
        setTimeout(() => router.replace('/login'), 1000)
    } catch (err: any) {
        console.error(err)
        ElMessage.error(err?.response?.data?.message || t('userInfo.changePassword.failed') as string)
    } finally {
        changing.value = false
        pwdForm.currentPassword = ''
        pwdForm.newPassword = ''
        pwdForm.confirmPassword = ''
    }
}

onMounted(() => {
    loadUserInfo()
})
</script>

<style scoped>
.user-info-container {
    display: flex;
    gap: 20px;
    padding: 20px;
}

.card {
    flex: 1;
    background: #fff;
    padding: 20px;
    border-radius: 8px;
    box-shadow: 0 6px 18px rgba(0, 0, 0, 0.06);
}

h2 {
    margin-bottom: 16px
}
</style>
