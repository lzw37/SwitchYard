<template>
    <div class="create-user-container">
        <div class="create-user-box">
            <h2 class="create-user-title">创建新用户</h2>
            <el-form :model="createUserForm" :rules="rules" ref="createUserFormRef" class="create-user-form"
                label-width="80px">
                <el-form-item label="用户名" prop="username">
                    <el-input v-model="createUserForm.username" placeholder="请输入用户名（3-50个字符）" clearable />
                </el-form-item>

                <el-form-item label="密码" prop="password">
                    <el-input v-model="createUserForm.password" type="password" placeholder="请输入密码（至少6个字符）"
                        show-password clearable />
                </el-form-item>

                <el-form-item label="确认密码" prop="confirmPassword">
                    <el-input v-model="createUserForm.confirmPassword" type="password" placeholder="请再次输入密码"
                        show-password clearable @keyup.enter="handleCreateUser" />
                </el-form-item>

                <el-form-item label="邮箱" prop="email">
                    <el-input v-model="createUserForm.email" placeholder="请输入邮箱（可选）" clearable />
                </el-form-item>

                <el-form-item label="角色" prop="role">
                    <el-select v-model="createUserForm.role" placeholder="请选择角色" style="width: 100%">
                        <el-option label="普通用户" value="User" />
                        <el-option label="管理员" value="Admin" />
                    </el-select>
                </el-form-item>

                <el-form-item>
                    <el-button type="primary" :loading="loading" @click="handleCreateUser" class="create-button">
                        创建用户
                    </el-button>
                    <el-button @click="handleCancel" class="cancel-button">
                        取消
                    </el-button>
                </el-form-item>

                <div class="back-to-login">
                    <router-link to="/login">返回登录</router-link>
                </div>
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

// 定义创建用户请求接口
interface CreateUserRequest {
    username: string
    password: string
    email?: string
    role: string
}

// 定义创建用户响应接口
interface CreateUserResponse {
    id: string
    name: string
    email?: string
    role: string
    createdAt: string
    message: string
}

const router = useRouter()
const createUserFormRef = ref<FormInstance>()
const loading = ref(false)

// 创建用户表单数据
const createUserForm = reactive({
    username: '',
    password: '',
    confirmPassword: '',
    email: '',
    role: 'User'
})

// 密码确认验证器
const validateConfirmPassword = (rule: any, value: any, callback: any) => {
    if (value === '') {
        callback(new Error('请再次输入密码'))
    } else if (value !== createUserForm.password) {
        callback(new Error('两次输入的密码不一致'))
    } else {
        callback()
    }
}

// 表单验证规则
const rules = reactive<FormRules>({
    username: [
        { required: true, message: '请输入用户名', trigger: 'blur' },
        { min: 3, max: 50, message: '用户名长度应在 3-50 个字符之间', trigger: 'blur' }
    ],
    password: [
        { required: true, message: '请输入密码', trigger: 'blur' },
        { min: 6, max: 30, message: '密码长度应至少为 6 个字符', trigger: 'blur' }
    ],
    confirmPassword: [
        { required: true, validator: validateConfirmPassword, trigger: 'blur' }
    ],
    email: [
        { type: 'email', message: '请输入正确的邮箱地址', trigger: 'blur' }
    ],
    role: [
        { required: true, message: '请选择角色', trigger: 'change' }
    ]
})

// SHA-256 哈希函数
const hashPassword = async (password: string): Promise<string> => {
    const encoder = new TextEncoder()
    const data = encoder.encode(password)
    const hashBuffer = await crypto.subtle.digest('SHA-256', data)
    const hashArray = Array.from(new Uint8Array(hashBuffer))
    const hashHex = hashArray.map(b => b.toString(16).padStart(2, '0')).join('')
    return hashHex
}

// 处理创建用户
const handleCreateUser = async () => {
    if (!createUserFormRef.value) return

    await createUserFormRef.value.validate(async (valid) => {
        if (valid) {
            loading.value = true
            try {
                // 对密码进行 SHA-256 哈希
                const hashedPassword = await hashPassword(createUserForm.password)

                const requestData: CreateUserRequest = {
                    username: createUserForm.username,
                    password: hashedPassword,
                    role: createUserForm.role
                }

                // 如果邮箱不为空，添加到请求中
                if (createUserForm.email) {
                    requestData.email = createUserForm.email
                }

                const response = await axios.post<CreateUserResponse>('/api/Auth/createuser', requestData)

                if (response.data.name) {
                    ElMessage.success(response.data.message || '用户创建成功')
                } else {
                    throw new Error('创建用户响应数据不完整')
                }

                // 创建成功后跳转到登录页
                setTimeout(() => {
                    router.push('/login')
                }, 1500)
            } catch (error: any) {
                console.error('创建用户错误:', error)
                if (error.response?.data?.message) {
                    ElMessage.error(error.response.data.message)
                } else if (!error.response) {
                    ElMessage.error('网络错误，请稍后重试')
                } else {
                    ElMessage.error('创建用户失败，请稍后重试')
                }
            } finally {
                loading.value = false
            }
        }
    })
}

// 处理取消
const handleCancel = () => {
    createUserFormRef.value?.resetFields()
}
</script>

<style scoped>
.create-user-container {
    display: flex;
    justify-content: center;
    align-items: center;
    min-height: 100vh;
    background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
}

.create-user-box {
    width: 450px;
    padding: 40px;
    background: white;
    border-radius: 10px;
    box-shadow: 0 10px 30px rgba(0, 0, 0, 0.2);
}

.create-user-title {
    text-align: center;
    margin-bottom: 30px;
    color: #333;
    font-size: 24px;
    font-weight: 600;
}

.create-user-form {
    width: 100%;
}

.create-button {
    width: 48%;
}

.cancel-button {
    width: 48%;
    margin-left: 4%;
}

.back-to-login {
    text-align: center;
    margin-top: 15px;
}

.back-to-login a {
    color: #667eea;
    text-decoration: none;
    font-size: 14px;
}

.back-to-login a:hover {
    text-decoration: underline;
}

:deep(.el-form-item) {
    margin-bottom: 20px;
}
</style>
