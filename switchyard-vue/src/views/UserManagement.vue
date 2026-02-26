<template>
    <div class="user-management">
        <div class="toolbar">
            <el-input
                v-model="keyword"
                placeholder="搜索 ID/用户名/邮箱/角色"
                clearable
                style="width: 320px"
            />
            <el-button type="primary" :loading="loading" @click="loadUsers">刷新</el-button>
            <el-button type="success" @click="openCreate">新增用户</el-button>
        </div>

        <el-table :data="filteredUsers" v-loading="loading" stripe style="width: 100%">
            <el-table-column prop="id" label="ID" min-width="170" />
            <el-table-column prop="name" label="用户名" min-width="130" />
            <el-table-column prop="role" label="角色" width="100" />
            <el-table-column prop="email" label="邮箱" min-width="180" />
            <el-table-column label="创建时间" min-width="180">
                <template #default="{ row }">
                    {{ formatCreateAt(row.createAt) }}
                </template>
            </el-table-column>
            <el-table-column label="激活状态" width="120">
                <template #default="{ row }">
                    <el-tag :type="row.isActive === 1 ? 'success' : 'info'">
                        {{ row.isActive === 1 ? '已激活' : '未激活' }}
                    </el-tag>
                </template>
            </el-table-column>
            <el-table-column label="操作" width="240" fixed="right">
                <template #default="{ row }">
                    <el-button type="primary" link @click="openEdit(row)">编辑</el-button>
                    <el-button type="warning" link @click="openResetPassword(row)">重置密码</el-button>
                    <el-button
                        type="danger"
                        link
                        :disabled="isCurrentUser(row)"
                        :loading="deletingId === row.id"
                        @click="confirmDelete(row)"
                    >
                        删除
                    </el-button>
                </template>
            </el-table-column>
        </el-table>

        <el-dialog v-model="createVisible" title="新增用户" width="560px">
            <el-form :model="createForm" label-width="120px">
                <el-form-item label="用户名">
                    <el-input v-model="createForm.username" />
                </el-form-item>
                <el-form-item label="密码">
                    <el-input v-model="createForm.password" type="password" show-password />
                </el-form-item>
                <el-form-item label="确认密码">
                    <el-input v-model="createForm.confirmPassword" type="password" show-password />
                </el-form-item>
                <el-form-item label="角色">
                    <el-select v-model="createForm.role" style="width: 100%">
                        <el-option label="User" value="User" />
                        <el-option label="Admin" value="Admin" />
                    </el-select>
                </el-form-item>
                <el-form-item label="邮箱">
                    <el-input v-model="createForm.email" />
                </el-form-item>
                <el-form-item label="激活状态">
                    <el-select v-model="createForm.isActive" style="width: 100%">
                        <el-option label="1 - 已激活" :value="1" />
                        <el-option label="0 - 未激活" :value="0" />
                    </el-select>
                </el-form-item>
            </el-form>
            <template #footer>
                <el-button @click="createVisible = false">取消</el-button>
                <el-button type="primary" :loading="creating" @click="createUser">创建</el-button>
            </template>
        </el-dialog>

        <el-dialog v-model="editVisible" title="编辑用户" width="680px">
            <el-form :model="editForm" label-width="120px">
                <el-form-item label="ID">
                    <el-input v-model="editForm.id" />
                </el-form-item>
                <el-form-item label="用户名">
                    <el-input v-model="editForm.name" />
                </el-form-item>
                <el-form-item label="角色">
                    <el-select v-model="editForm.role" style="width: 100%">
                        <el-option label="User" value="User" />
                        <el-option label="Admin" value="Admin" />
                    </el-select>
                </el-form-item>
                <el-form-item label="邮箱">
                    <el-input v-model="editForm.email" />
                </el-form-item>
                <el-form-item label="创建时间">
                    <el-date-picker
                        v-model="editForm.createAt"
                        type="datetime"
                        format="YYYY-MM-DD HH:mm:ss"
                        value-format="YYYY-MM-DDTHH:mm:ss"
                        style="width: 100%"
                    />
                </el-form-item>
                <el-form-item label="激活状态">
                    <el-select v-model="editForm.isActive" style="width: 100%">
                        <el-option label="1 - 已激活" :value="1" />
                        <el-option label="0 - 未激活" :value="0" />
                    </el-select>
                </el-form-item>
            </el-form>
            <template #footer>
                <el-button @click="editVisible = false">取消</el-button>
                <el-button type="primary" :loading="saving" @click="saveUser">保存</el-button>
            </template>
        </el-dialog>

        <el-dialog v-model="resetPasswordVisible" title="重置密码" width="520px">
            <el-form :model="resetForm" label-width="120px">
                <el-form-item label="用户ID">
                    <el-input v-model="resetForm.id" disabled />
                </el-form-item>
                <el-form-item label="用户名">
                    <el-input v-model="resetForm.name" disabled />
                </el-form-item>
                <el-form-item label="新密码">
                    <el-input v-model="resetForm.newPassword" type="password" show-password />
                </el-form-item>
                <el-form-item label="确认密码">
                    <el-input v-model="resetForm.confirmPassword" type="password" show-password />
                </el-form-item>
            </el-form>
            <template #footer>
                <el-button @click="resetPasswordVisible = false">取消</el-button>
                <el-button type="primary" :loading="resettingPassword" @click="submitResetPassword">确认重置</el-button>
            </template>
        </el-dialog>
    </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import axios from '@/utils/axios'
import { ElMessage, ElMessageBox } from 'element-plus'
import CryptoJS from 'crypto-js'

interface UserRecord {
    id: string
    name: string
    role: string
    email?: string | null
    createAt: string
    isActive: number
}

interface CreateUserPayload {
    username: string
    password: string
    role: string
    email?: string | null
    isActive: number
}

const users = ref<UserRecord[]>([])
const loading = ref(false)
const saving = ref(false)
const creating = ref(false)
const resettingPassword = ref(false)
const deletingId = ref('')
const keyword = ref('')

const createVisible = ref(false)
const editVisible = ref(false)
const resetPasswordVisible = ref(false)
const editingSourceId = ref('')

const createForm = reactive({
    username: '',
    password: '',
    confirmPassword: '',
    role: 'User',
    email: '',
    isActive: 1
})

const editForm = reactive<UserRecord>({
    id: '',
    name: '',
    role: 'User',
    email: '',
    createAt: '',
    isActive: 1
})

const resetForm = reactive({
    id: '',
    name: '',
    newPassword: '',
    confirmPassword: ''
})

const currentUsername = (localStorage.getItem('username') || '').trim()

const isCurrentUser = (user: UserRecord): boolean => {
    return !!currentUsername && user.name.toLowerCase() === currentUsername.toLowerCase()
}

const hashPassword = (password: string): string => {
    return CryptoJS.SHA256(password).toString()
}

const normalizeCreateAt = (value: string): string => {
    if (!value) return ''
    return value.includes('T') ? value.slice(0, 19) : value.replace(' ', 'T').slice(0, 19)
}

const formatCreateAt = (value: string): string => {
    return normalizeCreateAt(value).replace('T', ' ')
}

const filteredUsers = computed(() => {
    const key = keyword.value.trim().toLowerCase()
    if (!key) return users.value

    return users.value.filter((user) =>
        user.id.toLowerCase().includes(key) ||
        user.name.toLowerCase().includes(key) ||
        (user.email || '').toLowerCase().includes(key) ||
        user.role.toLowerCase().includes(key)
    )
})

const resetCreateForm = () => {
    createForm.username = ''
    createForm.password = ''
    createForm.confirmPassword = ''
    createForm.role = 'User'
    createForm.email = ''
    createForm.isActive = 1
}

const resetPasswordForm = () => {
    resetForm.id = ''
    resetForm.name = ''
    resetForm.newPassword = ''
    resetForm.confirmPassword = ''
}

const loadUsers = async () => {
    loading.value = true
    try {
        const resp = await axios.get<UserRecord[]>('/api/Admin/users')
        users.value = (resp.data || []).map((user) => ({
            ...user,
            createAt: normalizeCreateAt(user.createAt),
            isActive: Number(user.isActive)
        }))
    } catch (error: any) {
        ElMessage.error(error?.response?.data?.message || '加载用户列表失败')
    } finally {
        loading.value = false
    }
}

const openCreate = () => {
    resetCreateForm()
    createVisible.value = true
}

const createUser = async () => {
    if (!createForm.username.trim() || !createForm.password.trim()) {
        ElMessage.warning('用户名和密码不能为空')
        return
    }

    if (createForm.password.length < 6) {
        ElMessage.warning('密码长度至少为 6 位')
        return
    }

    if (createForm.password !== createForm.confirmPassword) {
        ElMessage.warning('两次输入的密码不一致')
        return
    }

    creating.value = true
    try {
        const payload: CreateUserPayload = {
            username: createForm.username.trim(),
            password: hashPassword(createForm.password),
            role: createForm.role,
            isActive: Number(createForm.isActive)
        }

        const email = createForm.email.trim()
        if (email) {
            payload.email = email
        }

        await axios.post('/api/Admin/users', payload)
        ElMessage.success('用户创建成功')
        createVisible.value = false
        await loadUsers()
    } catch (error: any) {
        ElMessage.error(error?.response?.data?.message || '创建用户失败')
    } finally {
        creating.value = false
    }
}

const openEdit = (row: UserRecord) => {
    editingSourceId.value = row.id
    editForm.id = row.id
    editForm.name = row.name
    editForm.role = row.role
    editForm.email = row.email || ''
    editForm.createAt = normalizeCreateAt(row.createAt)
    editForm.isActive = Number(row.isActive)
    editVisible.value = true
}

const saveUser = async () => {
    if (!editingSourceId.value) return

    if (!editForm.id.trim() || !editForm.name.trim() || !editForm.role.trim()) {
        ElMessage.warning('ID、用户名、角色不能为空')
        return
    }

    saving.value = true
    try {
        await axios.put(`/api/Admin/users/${editingSourceId.value}`, {
            id: editForm.id.trim(),
            name: editForm.name.trim(),
            role: editForm.role.trim(),
            email: editForm.email?.trim() || null,
            createAt: editForm.createAt,
            isActive: Number(editForm.isActive)
        })

        ElMessage.success('用户更新成功')
        editVisible.value = false
        await loadUsers()
    } catch (error: any) {
        ElMessage.error(error?.response?.data?.message || '更新用户失败')
    } finally {
        saving.value = false
    }
}

const openResetPassword = (row: UserRecord) => {
    resetPasswordForm()
    resetForm.id = row.id
    resetForm.name = row.name
    resetPasswordVisible.value = true
}

const confirmDelete = async (row: UserRecord) => {
    if (isCurrentUser(row)) {
        ElMessage.warning('不能删除当前登录账号')
        return
    }

    try {
        await ElMessageBox.confirm(
            `确定要删除用户 "${row.name}" 吗？此操作不可恢复。`,
            '确认删除',
            {
                type: 'warning',
                confirmButtonText: '删除',
                cancelButtonText: '取消'
            }
        )

        deletingId.value = row.id
        await axios.delete(`/api/Admin/users/${row.id}`)
        ElMessage.success('用户已删除')
        await loadUsers()
    } catch (error: any) {
        if (error === 'cancel' || error === 'close') {
            return
        }

        ElMessage.error(error?.response?.data?.message || '删除用户失败')
    } finally {
        deletingId.value = ''
    }
}

const submitResetPassword = async () => {
    if (!resetForm.newPassword.trim()) {
        ElMessage.warning('新密码不能为空')
        return
    }

    if (resetForm.newPassword.length < 6) {
        ElMessage.warning('新密码长度至少为 6 位')
        return
    }

    if (resetForm.newPassword !== resetForm.confirmPassword) {
        ElMessage.warning('两次输入的密码不一致')
        return
    }

    resettingPassword.value = true
    try {
        await axios.post(`/api/Admin/users/${resetForm.id}/reset-password`, {
            newPassword: hashPassword(resetForm.newPassword)
        })

        ElMessage.success('密码重置成功')
        resetPasswordVisible.value = false
        resetPasswordForm()
    } catch (error: any) {
        ElMessage.error(error?.response?.data?.message || '重置密码失败')
    } finally {
        resettingPassword.value = false
    }
}

onMounted(() => {
    loadUsers()
})
</script>

<style scoped>
.user-management {
    padding: 16px;
}

.toolbar {
    display: flex;
    align-items: center;
    gap: 10px;
    margin-bottom: 12px;
}
</style>
