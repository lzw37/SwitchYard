<template>
    <div class="user-manager">
        <div class="toolbar">
            <el-input v-model="filter" placeholder="搜索用户名或邮箱" clearable @clear="loadUsers" @keyup.enter="loadUsers"
                style="width:300px" />
            <el-button type="primary" @click="loadUsers">刷新</el-button>
        </div>

        <el-table :data="users" style="width:100%" v-loading="loading">
            <el-table-column prop="id" label="ID" width="180" />
            <el-table-column prop="username" label="用户名" />
            <el-table-column prop="email" label="邮箱" />
            <el-table-column prop="role" label="角色" width="120" />
            <el-table-column label="可用" width="100">
                <template #default="{ row }">
                    <el-switch v-model="row.isActive" @change="toggleActive(row)" active-color="#13ce66"
                        inactive-color="#ff4949" />
                </template>
            </el-table-column>
            <el-table-column label="操作" width="260">
                <template #default="{ row }">
                    <el-button size="mini" @click="openEdit(row)">编辑</el-button>
                    <el-button size="mini" type="warning" @click="openRole(row)">设置角色</el-button>
                    <el-button size="mini" type="danger" @click="confirmDelete(row)">删除</el-button>
                </template>
            </el-table-column>
        </el-table>

        <el-dialog title="编辑用户" :visible.sync="editDialogVisible">
            <el-form :model="editUser" label-width="100px">
                <el-form-item label="用户名">
                    <el-input v-model="editUser.username" disabled />
                </el-form-item>
                <el-form-item label="邮箱">
                    <el-input v-model="editUser.email" />
                </el-form-item>
            </el-form>
            <template #footer>
                <el-button @click="editDialogVisible = false">取消</el-button>
                <el-button type="primary" @click="saveEdit" :loading="saving">保存</el-button>
            </template>
        </el-dialog>

        <el-dialog title="设置角色" :visible.sync="roleDialogVisible">
            <el-form :model="roleUser" label-width="100px">
                <el-form-item label="用户名">
                    <el-input v-model="roleUser.username" disabled />
                </el-form-item>
                <el-form-item label="角色">
                    <el-select v-model="roleUser.role" placeholder="选择角色">
                        <el-option label="User" value="User" />
                        <el-option label="Admin" value="Admin" />
                    </el-select>
                </el-form-item>
            </el-form>
            <template #footer>
                <el-button @click="roleDialogVisible = false">取消</el-button>
                <el-button type="primary" @click="saveRole" :loading="savingRole">保存</el-button>
            </template>
        </el-dialog>
    </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import axios from '@/utils/axios'
import { ElMessage, ElMessageBox } from 'element-plus'

interface UserItem {
    id: string
    username: string
    email?: string
    role?: string
    isActive?: number | boolean
}

const users = ref<UserItem[]>([])
const loading = ref(false)
const saving = ref(false)
const savingRole = ref(false)
const filter = ref('')

const editDialogVisible = ref(false)
const roleDialogVisible = ref(false)
const editUser = reactive<UserItem>({ id: '', username: '', email: '', role: 'User', isActive: 1 })
const roleUser = reactive<UserItem>({ id: '', username: '', email: '', role: 'User', isActive: 1 })

const loadUsers = async () => {
    loading.value = true
    try {
        // 假设后端提供 GET /api/Admin/users?filter=...
        const resp = await axios.get('/api/Admin/users', { params: { filter: filter.value } })
        users.value = (resp.data || []) as UserItem[]
    } catch (err: any) {
        console.error('加载用户失败', err)
        ElMessage.error(err?.response?.data?.message || '加载用户失败')
    } finally {
        loading.value = false
    }
}

const openEdit = (row: UserItem) => {
    editUser.id = row.id
    editUser.username = row.username
    editUser.email = row.email
    editUser.role = row.role
    editDialogVisible.value = true
}

const saveEdit = async () => {
    saving.value = true
    try {
        // 假设后端提供 PUT /api/Admin/users/{id} 接收 { email }
        await axios.put(`/api/Admin/users/${editUser.id}`, { email: editUser.email })
        ElMessage.success('用户信息已保存')
        editDialogVisible.value = false
        await loadUsers()
    } catch (err: any) {
        console.error(err)
        ElMessage.error(err?.response?.data?.message || '保存失败')
    } finally {
        saving.value = false
    }
}

const openRole = (row: UserItem) => {
    roleUser.id = row.id
    roleUser.username = row.username
    roleUser.role = row.role || 'User'
    roleDialogVisible.value = true
}

const saveRole = async () => {
    savingRole.value = true
    try {
        // 假设后端提供 PATCH /api/Admin/users/{id}/role 接收 { role }
        await axios.patch(`/api/Admin/users/${roleUser.id}/role`, { role: roleUser.role })
        ElMessage.success('角色已更新')
        roleDialogVisible.value = false
        await loadUsers()
    } catch (err: any) {
        console.error(err)
        ElMessage.error(err?.response?.data?.message || '设置角色失败')
    } finally {
        savingRole.value = false
    }
}

const toggleActive = async (row: UserItem) => {
    try {
        // 兼容 boolean 或 数字
        const isActive = row.isActive ? 1 : 0
        // 假设后端提供 PATCH /api/Admin/users/{id}/active 接收 { isActive }
        await axios.patch(`/api/Admin/users/${row.id}/active`, { isActive })
        ElMessage.success('状态已更新')
        await loadUsers()
    } catch (err: any) {
        console.error(err)
        ElMessage.error(err?.response?.data?.message || '更新状态失败')
    }
}

const confirmDelete = async (row: UserItem) => {
    try {
        await ElMessageBox.confirm(`确定删除用户 ${row.username} 吗？此操作不可恢复。`, '请确认', { type: 'warning' })
        // 假设后端提供 DELETE /api/Admin/users/{id}
        await axios.delete(`/api/Admin/users/${row.id}`)
        ElMessage.success('用户已删除')
        await loadUsers()
    } catch (err: any) {
        if (err === 'cancel' || err === 'close') return
        console.error(err)
        ElMessage.error(err?.response?.data?.message || '删除失败')
    }
}

onMounted(() => {
    loadUsers()
})
</script>

<style scoped>
.user-manager {
    padding: 20px
}

.toolbar {
    display: flex;
    gap: 10px;
    margin-bottom: 12px
}
</style>
