<template>
    <div class="capacity-instance-manager">
        <div class="toolbar">
            <el-button type="primary" @click="openCreate">
                {{ t('capacityInstance.buttons.create') }}
            </el-button>
            <el-button type="primary" :loading="loading" @click="loadInstances">
                {{ t('capacityInstance.buttons.refresh') }}
            </el-button>
        </div>

        <el-table :data="instances" style="width: 100%" v-loading="loading">
            <el-table-column prop="id" label="ID" width="200" />
            <el-table-column prop="name" :label="t('capacityInstance.columns.name')" min-width="200" />
            <el-table-column prop="owner" :label="t('capacityInstance.columns.owner')" width="150" />
            <el-table-column prop="createdDate" :label="t('capacityInstance.columns.createdDate')" width="180">
                <template #default="{ row }">
                    {{ formatDate(row.createdDate) }}
                </template>
            </el-table-column>
            <el-table-column :label="t('capacityInstance.columns.isActive')" width="100">
                <template #default="{ row }">
                    <el-tag :type="row.isActive === 1 ? 'success' : 'info'">
                        {{
                            row.isActive === 1
                                ? t('capacityInstance.status.active')
                                : t('capacityInstance.status.inactive')
                        }}
                    </el-tag>
                </template>
            </el-table-column>
            <el-table-column :label="t('capacityInstance.columns.actions')" width="280" fixed="right">
                <template #default="{ row }">
                    <el-button size="small" type="success" @click="openCopy(row)">
                        {{ t('capacityInstance.buttons.copy') }}
                    </el-button>
                    <el-button size="small" @click="openEdit(row)">
                        {{ t('capacityInstance.buttons.edit') }}
                    </el-button>
                    <el-button size="small" type="danger" @click="confirmDelete(row)">
                        {{ t('capacityInstance.buttons.delete') }}
                    </el-button>
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

        <el-dialog
            :title="
                dialogMode === 'create'
                    ? t('capacityInstance.dialogs.createTitle')
                    : t('capacityInstance.dialogs.editTitle')
            "
            v-model="dialogVisible"
            width="500px"
            :close-on-click-modal="false"
        >
            <el-form ref="formRef" :model="formData" :rules="rules" label-width="120px">
                <el-form-item :label="t('capacityInstance.columns.name')" prop="name">
                    <el-input v-model="formData.name" :placeholder="t('capacityInstance.placeholder.name')" />
                </el-form-item>
                <el-form-item :label="t('capacityInstance.columns.isActive')" prop="isActive">
                    <el-switch
                        v-model="formData.isActive"
                        :active-value="1"
                        :inactive-value="0"
                        :active-text="t('capacityInstance.status.active')"
                        :inactive-text="t('capacityInstance.status.inactive')"
                    />
                </el-form-item>
            </el-form>
            <template #footer>
                <el-button @click="dialogVisible = false">
                    {{ t('capacityInstance.buttons.cancel') }}
                </el-button>
                <el-button type="primary" :loading="saving" @click="handleSubmit">
                    {{ t('capacityInstance.buttons.save') }}
                </el-button>
            </template>
        </el-dialog>

        <el-dialog
            :title="t('capacityInstanceManagement.dialogs.copyTitle')"
            v-model="copyDialogVisible"
            width="500px"
            :close-on-click-modal="false"
        >
            <el-form ref="copyFormRef" :model="copyForm" :rules="copyRules" label-width="120px">
                <el-form-item :label="t('capacityInstanceManagement.copy.source')">
                    <el-input :model-value="copySourceLabel" disabled />
                </el-form-item>
                <el-form-item :label="t('capacityInstanceManagement.copy.newName')" prop="newInstanceName">
                    <el-input
                        v-model="copyForm.newInstanceName"
                        :placeholder="t('capacityInstanceManagement.placeholder.newName')"
                    />
                </el-form-item>
                <el-alert
                    class="copy-hint"
                    :title="t('capacityInstanceManagement.copy.generatedIdHint')"
                    type="info"
                    :closable="false"
                    show-icon
                />
            </el-form>
            <template #footer>
                <el-button @click="copyDialogVisible = false">
                    {{ t('capacityInstance.buttons.cancel') }}
                </el-button>
                <el-button type="primary" :loading="copying" @click="handleCopy">
                    {{ t('capacityInstance.buttons.copy') }}
                </el-button>
            </template>
        </el-dialog>
    </div>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import axios from '@/utils/axios'
import { ElMessage, ElMessageBox } from 'element-plus'
import { useI18n } from 'vue-i18n'
import type { FormInstance, FormRules } from 'element-plus'
import { useAuthStore } from '@/stores/auth'
import { DEFAULT_PAGE_SIZES, type PagedResult } from '@/types/pagination'

interface CapacityInstance {
    id?: string
    name: string
    owner?: string
    createdDate?: string
    isActive: number
}

const emit = defineEmits<{
    instancesChanged: []
}>()

const { t } = useI18n()
const authStore = useAuthStore()
authStore.hydrateFromStorage()

const instances = ref<CapacityInstance[]>([])
const loading = ref(false)
const saving = ref(false)
const copying = ref(false)
const dialogVisible = ref(false)
const copyDialogVisible = ref(false)
const dialogMode = ref<'create' | 'edit'>('create')
const formRef = ref<FormInstance>()
const copyFormRef = ref<FormInstance>()
const copySourceLabel = ref('')
const pageSizes = [...DEFAULT_PAGE_SIZES]
const pagination = reactive({
    pageNumber: 1,
    pageSize: 10,
    totalCount: 0,
})

const formData = reactive<CapacityInstance>({
    id: '',
    name: '',
    owner: '',
    createdDate: '',
    isActive: 1,
})

const copyForm = reactive({
    sourceInstanceID: '',
    newInstanceName: '',
    owner: '',
})

const rules = reactive<FormRules>({
    name: [
        { required: true, message: t('capacityInstance.validation.nameRequired'), trigger: 'blur' },
        { min: 2, max: 100, message: t('capacityInstance.validation.nameLength'), trigger: 'blur' },
    ],
})

const copyRules = reactive<FormRules>({
    newInstanceName: [
        { required: true, message: t('capacityInstance.validation.nameRequired'), trigger: 'blur' },
        { min: 2, max: 100, message: t('capacityInstance.validation.nameLength'), trigger: 'blur' },
    ],
})

const loadInstances = async () => {
    loading.value = true
    try {
        const response = await axios.get<PagedResult<CapacityInstance>>('/Capacity/GetInstancePage', {
            params: {
                pageNumber: pagination.pageNumber,
                pageSize: pagination.pageSize,
            },
        })

        const result = response.data
        const totalPages = Math.max(1, Math.ceil((result.totalCount || 0) / (result.pageSize || pagination.pageSize)))
        if (result.totalCount > 0 && pagination.pageNumber > totalPages) {
            pagination.pageNumber = totalPages
            await loadInstances()
            return
        }

        instances.value = (result.items || []).map((item) => ({
            ...item,
            isActive: Number(item.isActive ?? 1),
        }))
        pagination.pageNumber = result.pageNumber || pagination.pageNumber
        pagination.pageSize = result.pageSize || pagination.pageSize
        pagination.totalCount = result.totalCount || 0
    } catch (error: any) {
        console.error('Failed to load capacity instances:', error)
        instances.value = []
        pagination.totalCount = 0
        ElMessage.error(t('capacityInstance.messages.loadError'))
    } finally {
        loading.value = false
    }
}

const handleCurrentChange = (pageNumber: number) => {
    pagination.pageNumber = pageNumber
    void loadInstances()
}

const handleSizeChange = (pageSize: number) => {
    pagination.pageSize = pageSize
    pagination.pageNumber = 1
    void loadInstances()
}

const openCreate = () => {
    dialogMode.value = 'create'
    Object.assign(formData, {
        id: '',
        name: '',
        owner: authStore.username.trim(),
        createdDate: '',
        isActive: 1,
    })
    dialogVisible.value = true
    formRef.value?.clearValidate()
}

const openEdit = (row: CapacityInstance) => {
    dialogMode.value = 'edit'
    Object.assign(formData, {
        id: row.id,
        name: row.name,
        owner: row.owner,
        createdDate: row.createdDate,
        isActive: row.isActive,
    })
    dialogVisible.value = true
    formRef.value?.clearValidate()
}

const openCopy = (row: CapacityInstance) => {
    copyForm.sourceInstanceID = row.id || ''
    copyForm.newInstanceName = t('capacityInstanceManagement.copy.defaultName', { name: row.name })
    copyForm.owner = row.owner || authStore.username.trim()
    copySourceLabel.value = `${row.name} (${row.id})`
    copyDialogVisible.value = true
    copyFormRef.value?.clearValidate()
}

const handleSubmit = async () => {
    if (!formRef.value) return

    try {
        await formRef.value.validate()
    } catch {
        return
    }

    saving.value = true
    try {
        if (dialogMode.value === 'create') {
            await createInstance()
        } else {
            await editInstance()
        }
    } finally {
        saving.value = false
    }
}

const handleCopy = async () => {
    if (!copyFormRef.value) return

    try {
        await copyFormRef.value.validate()
    } catch {
        return
    }

    copying.value = true
    try {
        const response = await axios.post<CapacityInstance>('/Capacity/CopyCapacityInstance', {
            sourceInstanceID: copyForm.sourceInstanceID,
            newInstanceName: copyForm.newInstanceName.trim(),
            owner: copyForm.owner || authStore.username.trim(),
        })

        pagination.pageNumber = 1
        ElMessage.success(t('capacityInstanceManagement.messages.copySuccess', { id: response.data?.id || '-' }))
        copyDialogVisible.value = false
        await loadInstances()
        emit('instancesChanged')
    } catch (error: any) {
        console.error('Failed to copy capacity instance:', error)
        ElMessage.error(t('capacityInstanceManagement.messages.copyError'))
    } finally {
        copying.value = false
    }
}

const createInstance = async () => {
    try {
        await axios.post('/Capacity/CreateInstance', {
            name: formData.name.trim(),
            owner: formData.owner || authStore.username.trim(),
            isActive: formData.isActive,
        })
        pagination.pageNumber = 1
        ElMessage.success(t('capacityInstance.messages.createSuccess'))
        dialogVisible.value = false
        await loadInstances()
        emit('instancesChanged')
    } catch (error: any) {
        console.error('Failed to create capacity instance:', error)
        ElMessage.error(t('capacityInstance.messages.createError'))
    }
}

const editInstance = async () => {
    try {
        await axios.put('/Capacity/EditInstance', {
            id: formData.id,
            name: formData.name.trim(),
            owner: formData.owner,
            isActive: formData.isActive,
        })
        ElMessage.success(t('capacityInstance.messages.editSuccess'))
        dialogVisible.value = false
        await loadInstances()
        emit('instancesChanged')
    } catch (error: any) {
        console.error('Failed to edit capacity instance:', error)
        ElMessage.error(t('capacityInstance.messages.editError'))
    }
}

const confirmDelete = (row: CapacityInstance) => {
    ElMessageBox.confirm(
        t('capacityInstance.messages.deleteConfirm', { name: row.name }),
        t('capacityInstance.dialogs.deleteTitle'),
        {
            confirmButtonText: t('capacityInstance.buttons.confirm'),
            cancelButtonText: t('capacityInstance.buttons.cancel'),
            type: 'warning',
        },
    )
        .then(() => {
            if (!row.id) return
            void deleteInstance(row.id)
        })
        .catch(() => {
            return
        })
}

const deleteInstance = async (id: string) => {
    loading.value = true
    try {
        await axios.delete('/Capacity/DeleteInstance', {
            params: { id },
        })
        ElMessage.success(t('capacityInstance.messages.deleteSuccess'))
        if (instances.value.length === 1 && pagination.pageNumber > 1) {
            pagination.pageNumber -= 1
        }
        await loadInstances()
        emit('instancesChanged')
    } catch (error: any) {
        console.error('Failed to delete capacity instance:', error)
        ElMessage.error(t('capacityInstance.messages.deleteError'))
    } finally {
        loading.value = false
    }
}

const formatDate = (dateString?: string) => {
    if (!dateString) return ''
    const date = new Date(dateString)
    return Number.isNaN(date.getTime()) ? dateString : date.toLocaleString()
}

onMounted(() => {
    void loadInstances()
})
</script>

<style scoped>
.capacity-instance-manager {
    padding: 20px;
}

.toolbar {
    margin-bottom: 20px;
    display: flex;
    gap: 10px;
}

.el-table {
    margin-top: 10px;
}

.table-footer {
    display: flex;
    justify-content: flex-end;
    margin-top: 12px;
}

.copy-hint {
    margin-top: 8px;
}
</style>
