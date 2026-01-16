<template>
    <div class="create-user-container">
        <div class="create-user-box">
            <h2 class="create-user-title">{{ t('createUser.title') }}</h2>
            <el-form :model="createUserForm" :rules="rules" ref="createUserFormRef" class="create-user-form"
                label-width="80px">
                <el-form-item :label="t('createUser.username')" prop="username">
                    <el-input v-model="createUserForm.username" :placeholder="t('createUser.placeholder.username')"
                        clearable />
                </el-form-item>

                <el-form-item :label="t('createUser.password')" prop="password">
                    <el-input v-model="createUserForm.password" type="password"
                        :placeholder="t('createUser.placeholder.password')" show-password clearable />
                </el-form-item>

                <el-form-item :label="t('createUser.confirmPassword')" prop="confirmPassword">
                    <el-input v-model="createUserForm.confirmPassword" type="password"
                        :placeholder="t('createUser.placeholder.confirmPassword')" show-password clearable
                        @keyup.enter="handleCreateUser" />
                </el-form-item>

                <el-form-item :label="t('createUser.email')" prop="email">
                    <el-input v-model="createUserForm.email" :placeholder="t('createUser.placeholder.email')"
                        clearable />
                </el-form-item>

                <el-form-item :label="t('createUser.role')" prop="role">
                    <el-select v-model="createUserForm.role" :placeholder="t('createUser.placeholder.selectRole')"
                        style="width: 100%">
                        <el-option :label="t('createUser.roles.user')" value="User" />
                        <el-option :label="t('createUser.roles.admin')" value="Admin" />
                    </el-select>
                </el-form-item>

                <el-form-item>
                    <el-button type="primary" :loading="loading" @click="handleCreateUser" class="create-button">
                        {{ t('createUser.buttons.create') }}
                    </el-button>
                    <el-button @click="handleCancel" class="cancel-button">
                        {{ t('createUser.buttons.cancel') }}
                    </el-button>
                </el-form-item>

                <div class="back-to-login">
                    <router-link to="/login">{{ t('createUser.buttons.returnLogin') }}</router-link>
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
const { t } = useI18n()
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
        callback(new Error(t('createUser.validation.confirmRequired')))
    } else if (value !== createUserForm.password) {
        callback(new Error(t('createUser.validation.confirmMismatch')))
    } else {
        callback()
    }
}

// 表单验证规则
const rules = reactive<FormRules>({
    username: [
        { required: true, message: t('createUser.validation.usernameRequired'), trigger: 'blur' },
        { min: 3, max: 50, message: t('createUser.validation.usernameLength'), trigger: 'blur' }
    ],
    password: [
        { required: true, message: t('createUser.validation.passwordRequired'), trigger: 'blur' },
        { min: 6, max: 30, message: t('createUser.validation.passwordLength'), trigger: 'blur' }
    ],
    confirmPassword: [
        { required: true, validator: validateConfirmPassword, trigger: 'blur' }
    ],
    email: [
        { type: 'email', message: t('createUser.validation.emailInvalid'), trigger: 'blur' }
    ],
    role: [
        { required: true, message: t('createUser.validation.roleRequired'), trigger: 'change' }
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
                    ElMessage.success(response.data.message || t('createUser.success'))
                } else {
                    throw new Error(t('createUser.failed'))
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
                    ElMessage.error(t('createUser.networkError'))
                } else {
                    ElMessage.error(t('createUser.failed'))
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
