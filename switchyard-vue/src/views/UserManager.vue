<template>
    <div class="user-manager">
        <div class="toolbar">
            <el-input v-model="filter" :placeholder="t('userManager.searchPlaceholder')" clearable @clear="loadUsers"
                @keyup.enter="loadUsers" style="width:300px" />
            <el-button type="primary" @click="loadUsers">{{ t('userManager.refresh') }}</el-button>
        </div>

        <el-table :data="users" style="width:100%" v-loading="loading">
            <el-table-column prop="id" label="ID" width="180" />
            <el-table-column prop="username" :label="t('userManager.columns.username')" />
            <el-table-column prop="email" :label="t('userManager.columns.email')" />
            <el-table-column prop="role" :label="t('userManager.columns.role')" width="120" />
            <el-table-column :label="t('userManager.columns.active')" width="100">
                <template #default="{ row }">
                    <el-switch v-model="row.isActive" @change="toggleActive(row)" active-color="#13ce66"
                        inactive-color="#ff4949" />
                </template>
            </el-table-column>
            <el-table-column :label="t('userManager.columns.actions')" width="260">
                <template #default="{ row }">
                    <el-button size="mini" @click="openEdit(row)">{{ t('userManager.actions.edit') }}</el-button>
                    <el-button size="mini" type="warning" @click="openRole(row)">{{ t('userManager.actions.setRole')
                        }}</el-button>
                    <el-button size="mini" type="danger" @click="confirmDelete(row)">{{ t('userManager.actions.delete')
                        }}</el-button>
                </template>
            </el-table-column>
        </el-table>

        <el-dialog :title="t('userManager.dialogs.editTitle')" :visible.sync="editDialogVisible">
            <el-form :model="editUser" label-width="100px">
                <el-form-item :label="t('userManager.columns.username')">
                    <el-input v-model="editUser.username" disabled />
                </el-form-item>
                <el-form-item :label="t('userManager.columns.email')">
                    <el-input v-model="editUser.email" />
                </el-form-item>
            </el-form>
            <template #footer>
                <el-button @click="editDialogVisible = false">{{ t('userManager.buttons.cancel') }}</el-button>
                <el-button type="primary" @click="saveEdit" :loading="saving">{{ t('userManager.buttons.save')
                    }}</el-button>
            </template>
        </el-dialog>

        <el-dialog :title="t('userManager.dialogs.setRoleTitle')" :visible.sync="roleDialogVisible">
            <el-form :model="roleUser" label-width="100px">
                <el-form-item :label="t('userManager.columns.username')">
                    <el-input v-model="roleUser.username" disabled />
                </el-form-item>
                <el-form-item :label="t('userManager.columns.role')">
                    <el-select v-model="roleUser.role" :placeholder="t('userManager.dialogs.rolePlaceholder')">
                        <el-option label="User" value="User" />
                        <el-option label="Admin" value="Admin" />
                    </el-select>
                </el-form-item>
            </el-form>
            <template #footer>
                <el-button @click="roleDialogVisible = false">{{ t('userManager.buttons.cancel') }}</el-button>
                <el-button type="primary" @click="saveRole" :loading="savingRole">{{ t('userManager.buttons.save')
                    }}</el-button>
            </template>
        </el-dialog>
    </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import axios from '@/utils/axios'
import { ElMessage, ElMessageBox } from 'element-plus'
import { useI18n } from 'vue-i18n'

interface UserItem {
    id: string
    username: string
    email?: string
    role?: string
    isActive?: number | boolean
}

const { t } = useI18n()
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
        console.error(t('userManager.messages.loadFailed'), err)
        ElMessage.error(err?.response?.data?.message || t('userManager.messages.loadFailed') as string)
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
        ElMessage.success(t('userManager.messages.saveSuccess') as string)
        editDialogVisible.value = false
        await loadUsers()
    } catch (err: any) {
        console.error(err)
        ElMessage.error(err?.response?.data?.message || t('userManager.messages.saveFailed') as string)
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
        ElMessage.success(t('userManager.messages.roleUpdated') as string)
        roleDialogVisible.value = false
        await loadUsers()
    } catch (err: any) {
        console.error(err)
        ElMessage.error(err?.response?.data?.message || t('userManager.messages.roleUpdateFailed') as string)
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
        ElMessage.success(t('userManager.messages.statusUpdated') as string)
        await loadUsers()
    } catch (err: any) {
        console.error(err)
        ElMessage.error(err?.response?.data?.message || t('userManager.messages.statusUpdateFailed') as string)
    }
}

const confirmDelete = async (row: UserItem) => {
    try {
        await ElMessageBox.confirm(t('userManager.messages.deleteConfirm', { username: row.username }) as string, t('common.confirm') as string, { type: 'warning' })
        // 假设后端提供 DELETE /api/Admin/users/{id}
        await axios.delete(`/api/Admin/users/${row.id}`)
        ElMessage.success(t('userManager.messages.deleted') as string)
        await loadUsers()
    } catch (err: any) {
        if (err === 'cancel' || err === 'close') return
        console.error(err)
        ElMessage.error(err?.response?.data?.message || t('userManager.messages.deleteFailed') as string)
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
