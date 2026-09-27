<template>
    <div class="user-management">
        <div class="toolbar">
            <el-input
                v-model="keyword"
                :placeholder="t('userManager.searchPlaceholder')"
                clearable
                style="width: 320px; max-width: 100%"
                @clear="handleSearch"
                @keyup.enter="handleSearch"
            />
            <ActionButton type="primary" :loading="loading" @click="loadUsers()" :label="t('common.actions.refresh')" :icon="actionIcons.refresh" />
            <ActionButton type="success" @click="openCreate" :label="t('userManager.actions.create')" :icon="actionIcons.add" />
            <ActionButton type="warning" :loading="importing" @click="triggerImport" :label="t('userManager.actions.import')" :icon="actionIcons.upload" />
            <ActionButton type="info" @click="downloadTemplate" :label="t('userManager.actions.downloadTemplate')" :icon="actionIcons.download" />
            <input
                ref="importFileInput"
                type="file"
                accept=".xlsx"
                style="display: none"
                @change="handleImportFile"
            />
        </div>

        <el-table :data="users" v-loading="loading" stripe style="width: 100%">
            <el-table-column prop="id" label="ID" min-width="170" />
            <el-table-column prop="name" :label="t('userManager.columns.username')" min-width="130" />
            <el-table-column prop="role" :label="t('userManager.columns.role')" min-width="120"><template #default="{ row }">{{ row.role === 'Admin' ? t('createUser.roles.admin') : row.role === 'User' ? t('createUser.roles.user') : row.role }}</template></el-table-column>
            <el-table-column prop="email" :label="t('userManager.columns.email')" min-width="180" />
            <el-table-column :label="t('userManager.columns.createdAt')" min-width="180">
                <template #default="{ row }">
                    {{ formatCreateAt(row.createAt) }}
                </template>
            </el-table-column>
            <el-table-column :label="t('userManager.columns.active')" width="120">
                <template #default="{ row }">
                    <el-tag :type="row.isActive === 1 ? 'success' : 'info'">
                        {{ row.isActive === 1 ? t('userManager.states.active') : t('userManager.states.inactive') }}
                    </el-tag>
                </template>
            </el-table-column>
            <el-table-column :label="t('userManager.columns.actions')" width="136" fixed="right">
                <template #default="{ row }">
                    <ActionButton type="primary" @click="openEdit(row)" :label="t('common.actions.edit')" :icon="actionIcons.edit" />
                    <ActionButton type="warning" @click="openResetPassword(row)" :label="t('userManager.actions.resetPassword')" :icon="actionIcons.lock" />
                    <ActionButton
                        type="danger"
                        :disabled="isCurrentUser(row)"
                        :loading="deletingId === row.id"
                        @click="confirmDelete(row)"
                     :label="t('common.actions.delete')" :icon="actionIcons.delete" />
                </template>
            </el-table-column>
        </el-table>

        <div class="table-footer">
            <el-pagination
                background
                :current-page="pagination.pageNumber"
                :page-size="pagination.pageSize"
                :page-sizes="pageSizes"
                :total="pagination.totalCount"
                layout="total, sizes, prev, pager, next, jumper"
                @current-change="handleCurrentChange"
                @size-change="handleSizeChange"
            />
        </div>

        <el-dialog v-model="createVisible" :title="t('userManager.actions.create')" width="560px">
            <el-form :model="createForm" label-width="150px">
                <el-form-item :label="t('userManager.columns.username')">
                    <el-input v-model="createForm.username" />
                </el-form-item>
                <el-form-item :label="t('createUser.password')">
                    <el-input v-model="createForm.password" type="password" show-password />
                </el-form-item>
                <el-form-item :label="t('createUser.confirmPassword')">
                    <el-input v-model="createForm.confirmPassword" type="password" show-password />
                </el-form-item>
                <el-form-item :label="t('userManager.columns.role')">
                    <el-select v-model="createForm.role" style="width: 100%">
                        <el-option :label="t('createUser.roles.user')" value="User" />
                        <el-option :label="t('createUser.roles.admin')" value="Admin" />
                    </el-select>
                </el-form-item>
                <el-form-item :label="t('userManager.columns.email')">
                    <el-input v-model="createForm.email" />
                </el-form-item>
                <el-form-item :label="t('userManager.columns.active')">
                    <el-select v-model="createForm.isActive" style="width: 100%">
                        <el-option :label="t('userManager.states.activeOption')" :value="1" />
                        <el-option :label="t('userManager.states.inactiveOption')" :value="0" />
                    </el-select>
                </el-form-item>
            </el-form>
            <template #footer>
                <ActionButton variant="text" @click="createVisible = false" :label="t('common.actions.cancel')" :icon="actionIcons.close" />
                <ActionButton variant="text" type="primary" :loading="creating" @click="createUser" :label="t('common.actions.create')" :icon="actionIcons.add" />
            </template>
        </el-dialog>

        <el-dialog v-model="editVisible" :title="t('userManager.dialogs.editTitle')" width="680px">
            <el-form :model="editForm" label-width="150px">
                <el-form-item label="ID">
                    <el-input v-model="editForm.id" />
                </el-form-item>
                <el-form-item :label="t('userManager.columns.username')">
                    <el-input v-model="editForm.name" />
                </el-form-item>
                <el-form-item :label="t('userManager.columns.role')">
                    <el-select v-model="editForm.role" style="width: 100%">
                        <el-option :label="t('createUser.roles.user')" value="User" />
                        <el-option :label="t('createUser.roles.admin')" value="Admin" />
                    </el-select>
                </el-form-item>
                <el-form-item :label="t('userManager.columns.email')">
                    <el-input v-model="editForm.email" />
                </el-form-item>
                <el-form-item :label="t('userManager.columns.createdAt')">
                    <el-date-picker
                        v-model="editForm.createAt"
                        type="datetime"
                        format="YYYY-MM-DD HH:mm:ss"
                        value-format="YYYY-MM-DDTHH:mm:ss"
                        style="width: 100%"
                    />
                </el-form-item>
                <el-form-item :label="t('userManager.columns.active')">
                    <el-select v-model="editForm.isActive" style="width: 100%">
                        <el-option :label="t('userManager.states.activeOption')" :value="1" />
                        <el-option :label="t('userManager.states.inactiveOption')" :value="0" />
                    </el-select>
                </el-form-item>
            </el-form>
            <template #footer>
                <ActionButton variant="text" @click="editVisible = false" :label="t('common.actions.cancel')" :icon="actionIcons.close" />
                <ActionButton variant="text" type="primary" :loading="saving" @click="saveUser" :label="t('common.actions.save')" :icon="actionIcons.save" />
            </template>
        </el-dialog>

        <el-dialog v-model="importResultVisible" :title="t('userManager.dialogs.importResults')" width="700px" :close-on-click-modal="false">
            <div v-if="importResult">
                <el-alert
                    :title="t('userManager.messages.importSummary', { success: importResult.successCount, failed: importResult.failedCount, total: importResult.totalCount })"
                    :type="importResult.failedCount === 0 ? 'success' : 'warning'"
                    :closable="false"
                    style="margin-bottom: 16px"
                />
                <el-table :data="importResult.results" max-height="360" stripe style="width: 100%">
                    <el-table-column prop="row" :label="t('userManager.columns.row')" width="70" />
                    <el-table-column prop="username" :label="t('userManager.columns.username')" min-width="130" />
                    <el-table-column :label="t('userManager.columns.status')" width="80">
                        <template #default="{ row }">
                            <el-tag :type="row.success ? 'success' : 'danger'">
                                {{ row.success ? t('userManager.states.success') : t('userManager.states.failed') }}
                            </el-tag>
                        </template>
                    </el-table-column>
                    <el-table-column prop="error" :label="t('userManager.columns.error')" min-width="200" />
                </el-table>
            </div>
            <template #footer>
                <ActionButton variant="text" type="primary" @click="closeImportResult" :label="t('common.actions.back')" :icon="actionIcons.back" />
            </template>
        </el-dialog>

        <el-dialog v-model="resetPasswordVisible" :title="t('userManager.actions.resetPassword')" width="520px">
            <el-form :model="resetForm" label-width="150px">
                <el-form-item :label="t('userManager.columns.userId')">
                    <el-input v-model="resetForm.id" disabled />
                </el-form-item>
                <el-form-item :label="t('userManager.columns.username')">
                    <el-input v-model="resetForm.name" disabled />
                </el-form-item>
                <el-form-item :label="t('userInfo.changePassword.new')">
                    <el-input v-model="resetForm.newPassword" type="password" show-password />
                </el-form-item>
                <el-form-item :label="t('createUser.confirmPassword')">
                    <el-input v-model="resetForm.confirmPassword" type="password" show-password />
                </el-form-item>
            </el-form>
            <template #footer>
                <ActionButton variant="text" @click="resetPasswordVisible = false" :label="t('common.actions.cancel')" :icon="actionIcons.close" />
                <ActionButton variant="text" type="primary" :loading="resettingPassword" @click="submitResetPassword" :label="t('userManager.actions.confirmReset')" :icon="actionIcons.reset" />
            </template>
        </el-dialog>
    </div>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, reactive, ref, watch } from 'vue'
import axios from '@/utils/axios'
import { useAuthStore } from '@/stores/auth'
import { ElMessage, ElMessageBox } from 'element-plus'
import { useI18n } from 'vue-i18n'
import ActionButton from '@/components/ui/ActionButton.vue'
import { actionIcons } from '@/components/ui/actionIcons'
import CryptoJS from 'crypto-js'
import { DEFAULT_PAGE_SIZES, type PagedResult } from '@/types/pagination'

interface ImportRowResult {
    row: number
    username: string
    success: boolean
    error?: string | null
}

interface ImportResult {
    totalCount: number
    successCount: number
    failedCount: number
    results: ImportRowResult[]
}

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

const { t } = useI18n()

const users = ref<UserRecord[]>([])
const loading = ref(false)
const saving = ref(false)
const creating = ref(false)
const resettingPassword = ref(false)
const deletingId = ref('')
const keyword = ref('')
const pageSizes = [...DEFAULT_PAGE_SIZES]
const pagination = reactive({
    pageNumber: 1,
    pageSize: 10,
    totalCount: 0,
})

const createVisible = ref(false)
const editVisible = ref(false)
const resetPasswordVisible = ref(false)
const importResultVisible = ref(false)
const editingSourceId = ref('')

const importing = ref(false)
const importResult = ref<ImportResult | null>(null)
const importFileInput = ref<HTMLInputElement | null>(null)

const createForm = reactive({
    username: '',
    password: '',
    confirmPassword: '',
    role: 'User',
    email: '',
    isActive: 1,
})

const editForm = reactive<UserRecord>({
    id: '',
    name: '',
    role: 'User',
    email: '',
    createAt: '',
    isActive: 1,
})

const resetForm = reactive({
    id: '',
    name: '',
    newPassword: '',
    confirmPassword: '',
})

const authStore = useAuthStore()
authStore.hydrateFromStorage()
const currentUsername = computed(() => authStore.username.trim())
let searchTimer: ReturnType<typeof window.setTimeout> | null = null

const isCurrentUser = (user: UserRecord): boolean => {
    return !!currentUsername.value && user.name.toLowerCase() === currentUsername.value.toLowerCase()
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

const loadUsers = async (options: { resetPage?: boolean } = {}) => {
    if (options.resetPage) {
        pagination.pageNumber = 1
    }

    loading.value = true
    try {
        const resp = await axios.get<PagedResult<UserRecord>>('/api/Admin/users/paged', {
            params: {
                pageNumber: pagination.pageNumber,
                pageSize: pagination.pageSize,
                keyword: keyword.value.trim() || undefined,
            },
        })

        const result = resp.data
        const totalPages = Math.max(1, Math.ceil((result.totalCount || 0) / (result.pageSize || pagination.pageSize)))
        if (result.totalCount > 0 && pagination.pageNumber > totalPages) {
            pagination.pageNumber = totalPages
            await loadUsers()
            return
        }

        users.value = (result.items || []).map((user) => ({
            ...user,
            createAt: normalizeCreateAt(user.createAt),
            isActive: Number(user.isActive),
        }))
        pagination.pageNumber = result.pageNumber || pagination.pageNumber
        pagination.pageSize = result.pageSize || pagination.pageSize
        pagination.totalCount = result.totalCount || 0
    } catch (error: any) {
        users.value = []
        pagination.totalCount = 0
        ElMessage.error(error?.response?.data?.message || t('userManager.messages.loadFailed'))
    } finally {
        loading.value = false
    }
}

const handleSearch = () => {
    void loadUsers({ resetPage: true })
}

const handleCurrentChange = (pageNumber: number) => {
    pagination.pageNumber = pageNumber
    void loadUsers()
}

const handleSizeChange = (pageSize: number) => {
    pagination.pageSize = pageSize
    pagination.pageNumber = 1
    void loadUsers()
}

const openCreate = () => {
    resetCreateForm()
    createVisible.value = true
}

const createUser = async () => {
    if (!createForm.username.trim() || !createForm.password.trim()) {
        ElMessage.warning(t('userManager.messages.credentialsRequired'))
        return
    }

    if (createForm.password.length < 6) {
        ElMessage.warning(t('createUser.validation.passwordLength'))
        return
    }

    if (createForm.password !== createForm.confirmPassword) {
        ElMessage.warning(t('createUser.validation.confirmMismatch'))
        return
    }

    creating.value = true
    try {
        const payload: CreateUserPayload = {
            username: createForm.username.trim(),
            password: hashPassword(createForm.password),
            role: createForm.role,
            isActive: Number(createForm.isActive),
        }

        const email = createForm.email.trim()
        if (email) {
            payload.email = email
        }

        await axios.post('/api/Admin/users', payload)
        ElMessage.success(t('createUser.success'))
        createVisible.value = false
        await loadUsers({ resetPage: true })
    } catch (error: any) {
        ElMessage.error(error?.response?.data?.message || t('userManager.messages.createFailed'))
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
        ElMessage.warning(t('userManager.messages.requiredFields'))
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
            isActive: Number(editForm.isActive),
        })

        ElMessage.success(t('userManager.messages.saveSuccess'))
        editVisible.value = false
        await loadUsers()
    } catch (error: any) {
        ElMessage.error(error?.response?.data?.message || t('userManager.messages.saveFailed'))
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
        ElMessage.warning(t('userManager.messages.cannotDeleteSelf'))
        return
    }

    try {
        await ElMessageBox.confirm(
            t('userManager.messages.deleteConfirm', { username: row.name }),
            t('userManager.dialogs.deleteTitle'),
            {
                type: 'warning',
                confirmButtonText: t('common.actions.delete'),
                cancelButtonText: t('common.actions.cancel'),
            },
        )

        deletingId.value = row.id
        await axios.delete(`/api/Admin/users/${row.id}`)
        ElMessage.success(t('userManager.messages.deleted'))
        if (users.value.length === 1 && pagination.pageNumber > 1) {
            pagination.pageNumber -= 1
        }
        await loadUsers()
    } catch (error: any) {
        if (error === 'cancel' || error === 'close') {
            return
        }

        ElMessage.error(error?.response?.data?.message || t('userManager.messages.deleteFailed'))
    } finally {
        deletingId.value = ''
    }
}

const submitResetPassword = async () => {
    if (!resetForm.newPassword.trim()) {
        ElMessage.warning(t('userManager.messages.newPasswordRequired'))
        return
    }

    if (resetForm.newPassword.length < 6) {
        ElMessage.warning(t('createUser.validation.passwordLength'))
        return
    }

    if (resetForm.newPassword !== resetForm.confirmPassword) {
        ElMessage.warning(t('createUser.validation.confirmMismatch'))
        return
    }

    resettingPassword.value = true
    try {
        await axios.post(`/api/Admin/users/${resetForm.id}/reset-password`, {
            newPassword: hashPassword(resetForm.newPassword),
        })

        ElMessage.success(t('userManager.messages.passwordReset'))
        resetPasswordVisible.value = false
        resetPasswordForm()
    } catch (error: any) {
        ElMessage.error(error?.response?.data?.message || t('userManager.messages.passwordResetFailed'))
    } finally {
        resettingPassword.value = false
    }
}

const triggerImport = () => {
    if (importFileInput.value) {
        importFileInput.value.value = ''
        importFileInput.value.click()
    }
}

const handleImportFile = async (event: Event) => {
    const input = event.target as HTMLInputElement
    const file = input.files?.[0]
    if (!file) return

    importing.value = true
    try {
        const formData = new FormData()
        formData.append('file', file)
        const resp = await axios.post<ImportResult>('/api/Admin/users/import', formData, {
            timeout: 0,
            headers: { 'Content-Type': 'multipart/form-data' },
        })
        importResult.value = resp.data
        importResultVisible.value = true
    } catch (error: any) {
        ElMessage.error(error?.response?.data?.message || t('userManager.messages.importFailed'))
    } finally {
        importing.value = false
    }
}

const closeImportResult = async () => {
    importResultVisible.value = false
    await loadUsers({ resetPage: true })
}

const downloadTemplate = async () => {
    try {
        const resp = await axios.get('/api/Admin/users/import-template', { responseType: 'blob' })
        const url = URL.createObjectURL(new Blob([resp.data]))
        const a = document.createElement('a')
        a.href = url
        a.download = 'user_info_template.xlsx'
        document.body.appendChild(a)
        a.click()
        document.body.removeChild(a)
        URL.revokeObjectURL(url)
    } catch (error: any) {
        ElMessage.error(error?.response?.data?.message || t('userManager.messages.downloadFailed'))
    }
}

watch(keyword, () => {
    if (searchTimer !== null) {
        window.clearTimeout(searchTimer)
    }

    searchTimer = window.setTimeout(() => {
        void loadUsers({ resetPage: true })
    }, 300)
})

onMounted(() => {
    void loadUsers()
})

onBeforeUnmount(() => {
    if (searchTimer !== null) {
        window.clearTimeout(searchTimer)
        searchTimer = null
    }
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
    flex-wrap: wrap;
}

.table-footer {
    display: flex;
    justify-content: flex-end;
    margin-top: 12px;
}
</style>
