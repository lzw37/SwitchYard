<template>
    <div class="login-container">
        <div class="login-box">
            <h2 class="login-title">用户登录</h2>
            <el-form :model="loginForm" :rules="rules" ref="loginFormRef" class="login-form">
                <el-form-item prop="username">
                    <el-input v-model="loginForm.username" placeholder="请输入用户名" prefix-icon="User" size="large"
                        clearable />
                </el-form-item>

                <el-form-item prop="password">
                    <el-input v-model="loginForm.password" type="password" placeholder="请输入密码" prefix-icon="Lock"
                        size="large" show-password @keyup.enter="handleLogin" />
                </el-form-item>

                <el-form-item>
                    <el-button type="primary" size="large" :loading="loading" @click="handleLogin" class="login-button">
                        登录
                    </el-button>
                </el-form-item>
            </el-form>
        </div>
    </div>
</template>

<script setup lang="ts">
import { ref, reactive } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import type { FormInstance, FormRules } from 'element-plus'
import axios from 'axios'

const router = useRouter()
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
        { required: true, message: '请输入用户名', trigger: 'blur' },
        { min: 3, max: 20, message: '用户名长度应在 3-20 个字符之间', trigger: 'blur' }
    ],
    password: [
        { required: true, message: '请输入密码', trigger: 'blur' },
        { min: 6, max: 30, message: '密码长度应在 6-30 个字符之间', trigger: 'blur' }
    ]
})

// SHA-256 哈希函数
const hashPassword = async (password: string): Promise<string> => {
    // 将密码转换为 Uint8Array
    const encoder = new TextEncoder()
    const data = encoder.encode(password)

    // 使用 Web Crypto API 进行 SHA-256 哈希
    const hashBuffer = await crypto.subtle.digest('SHA-256', data)

    // 将 ArrayBuffer 转换为十六进制字符串
    const hashArray = Array.from(new Uint8Array(hashBuffer))
    const hashHex = hashArray.map(b => b.toString(16).padStart(2, '0')).join('')

    return hashHex
}

// 处理登录
const handleLogin = async () => {
    if (!loginFormRef.value) return

    await loginFormRef.value.validate(async (valid) => {
        if (valid) {
            loading.value = true
            try {
                // 对密码进行 SHA-256 哈希
                const hashedPassword = await hashPassword(loginForm.password)

                const response = await axios.post('/api/Auth/login', {
                    username: loginForm.username,
                    password: hashedPassword
                })

                // 登录成功，保存 token 和用户信息
                const data = response.data
                localStorage.setItem('token', data.token)
                localStorage.setItem('tokenType', data.tokenType)
                localStorage.setItem('username', data.username)
                localStorage.setItem('role', data.role)

                ElMessage.success('登录成功')

                // 跳转到首页
                router.push('/')
            } catch (error: any) {
                console.error('登录错误:', error)
                // 错误已由axios拦截器处理，这里只需记录日志
                if (error.response?.data?.message) {
                    ElMessage.error(error.response.data.message)
                } else if (!error.response) {
                    ElMessage.error('网络错误，请稍后重试')
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

:deep(.el-input__wrapper) {
    padding: 8px 15px;
}
</style>
