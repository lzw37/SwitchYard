<template>
    <div class="login-container">
        <div class="login-box">
            <h2 class="login-title">{{ t('login.title') }}</h2>
            <el-form :model="loginForm" :rules="rules" ref="loginFormRef" class="login-form">
                <el-form-item prop="username">
                    <el-input v-model="loginForm.username" :placeholder="t('login.usernamePlaceholder')"
                        prefix-icon="User" size="large" clearable />
                </el-form-item>

                <el-form-item prop="password">
                    <el-input v-model="loginForm.password" type="password" :placeholder="t('login.passwordPlaceholder')"
                        prefix-icon="Lock" size="large" show-password @keyup.enter="handleLogin" />
                </el-form-item>

                <el-form-item>
                    <el-button type="primary" size="large" :loading="loading" @click="handleLogin" class="login-button">
                        {{ t('login.login') }}
                    </el-button>
                </el-form-item>

                <div class="register-link">
                    <span>{{ t('login.registerPrompt') }}</span>
                    <router-link to="/createuser">{{ t('login.registerLink') }}</router-link>
                </div>
            </el-form>
        </div>
    </div>
</template>

<script setup lang="ts">
import { ref, reactive } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import type { FormInstance, FormRules } from 'element-plus'
import axios from '@/utils/axios'
import CryptoJS from 'crypto-js'

// 定义登录响应接口
interface LoginResponse {
    token: string
    tokenType: string
    expiresIn: number
    name: string
    role: string
}

const router = useRouter()
const { t } = useI18n()
const loginFormRef = ref<FormInstance>()
const loading = ref(false)

// 登录表单数据
const loginForm = reactive({
    username: '',
    password: ''
})

// 表单验证规则
const rules = reactive<FormRules>({
    username: [
        { required: true, message: t('login.validation.usernameRequired'), trigger: 'blur' },
        { min: 3, max: 20, message: t('login.validation.usernameLength'), trigger: 'blur' }
    ],
    password: [
        { required: true, message: t('login.validation.passwordRequired'), trigger: 'blur' },
        { min: 6, max: 30, message: t('login.validation.passwordLength'), trigger: 'blur' }
    ]
})

// SHA-256 哈希函数
const hashPassword = (password: string): string => {
    // 使用 crypto-js 进行 SHA-256 哈希
    return CryptoJS.SHA256(password).toString()
}

// 处理登录
const handleLogin = async () => {
    if (!loginFormRef.value) return

    await loginFormRef.value.validate(async (valid) => {
        if (valid) {
            loading.value = true
            try {
                // 对密码进行 SHA-256 哈希
                const hashedPassword = hashPassword(loginForm.password)

                const response = await axios.post<LoginResponse>('/api/Auth/login', {
                    username: loginForm.username,
                    password: hashedPassword
                })

                // 登录成功，保存 token 和用户信息
                const data = response.data
                if (data.token && data.name) {
                    localStorage.setItem('token', data.token)
                    localStorage.setItem('tokenType', data.tokenType || 'Bearer')
                    localStorage.setItem('username', data.name)
                    localStorage.setItem('role', data.role || 'User')

                    ElMessage.success(t('login.success'))
                } else {
                    throw new Error(t('login.incompleteResponse'))
                }

                // 跳转到首页
                router.push('/')
            } catch (error: any) {
                console.error('登录错误:', error)
                if (error.response?.data?.message) {
                    ElMessage.error(error.response.data.message)
                } else if (!error.response) {
                    ElMessage.error(t('common.networkError'))
                } else {
                    ElMessage.error(t('login.failed'))
                }
            } finally {
                loading.value = false
            }
        }
    })
}
</script>

<style scoped>
.login-container {
    display: flex;
    justify-content: center;
    align-items: center;
    min-height: 100vh;
    background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
}

.login-box {
    width: 400px;
    padding: 40px;
    background: white;
    border-radius: 10px;
    box-shadow: 0 10px 30px rgba(0, 0, 0, 0.2);
}

.login-title {
    text-align: center;
    margin-bottom: 30px;
    color: #333;
    font-size: 24px;
    font-weight: 600;
}

.login-form {
    width: 100%;
}

.login-button {
    width: 100%;
    margin-top: 10px;
}

:deep(.el-form-item) {
    margin-bottom: 22px;
}

.register-link {
    text-align: center;
    margin-top: 15px;
    font-size: 14px;
    color: #666;
}

.register-link a {
    color: #667eea;
    text-decoration: none;
    margin-left: 5px;
    font-weight: 500;
}

.register-link a:hover {
    text-decoration: underline;
}

:deep(.el-input__wrapper) {
    padding: 8px 15px;
}
</style>
