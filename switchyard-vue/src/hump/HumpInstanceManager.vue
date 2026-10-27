<template>
    <div class="hump-instance-manager">
        <div class="toolbar">
            <el-button type="primary" @click="openCreate">
                {{ t('humpInstance.buttons.create') }}
            </el-button>
            <el-button type="primary" :loading="loading" @click="loadInstances">
                {{ t('humpInstance.buttons.refresh') }}
            </el-button>
        </div>

        <el-table :data="instances" style="width: 100%" v-loading="loading">
            <el-table-column prop="id" label="ID" width="200" />
            <el-table-column prop="name" :label="t('humpInstance.columns.name')" min-width="200" />
            <el-table-column prop="owner" :label="t('humpInstance.columns.owner')" width="150" />
            <el-table-column prop="createdDate" :label="t('humpInstance.columns.createdDate')" width="180">
                <template #default="{ row }">
                    {{ formatDate(row.createdDate) }}
                </template>
            </el-table-column>
            <el-table-column :label="t('humpInstance.columns.isActive')" width="100">
                <template #default="{ row }">
                    <el-tag :type="row.isActive === 1 ? 'success' : 'info'">
                        {{ row.isActive === 1 ? t('humpInstance.status.active') : t('humpInstance.status.inactive') }}
                    </el-tag>
                </template>
            </el-table-column>
            <el-table-column :label="t('humpInstance.columns.actions')" width="280" fixed="right">
                <template #default="{ row }">
                    <el-button size="small" type="success" @click="openCopy(row)">
                        {{ tr('humpInstance.buttons.copy', '复制', 'Copy') }}
                    </el-button>
                    <el-button size="small" @click="openEdit(row)">
                        {{ t('humpInstance.buttons.edit') }}
                    </el-button>
                    <el-button size="small" type="danger" @click="confirmDelete(row)">
                        {{ t('humpInstance.buttons.delete') }}
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
            :title="dialogMode === 'create' ? t('humpInstance.dialogs.createTitle') : t('humpInstance.dialogs.editTitle')"
            v-model="dialogVisible"
            width="500px"
            :close-on-click-modal="false"
        >
            <el-form ref="formRef" :model="formData" :rules="rules" label-width="120px">
                <el-form-item :label="t('humpInstance.columns.name')" prop="name">
                    <el-input v-model="formData.name" :placeholder="t('humpInstance.placeholder.name')" />
                </el-form-item>
                <el-form-item :label="t('humpInstance.columns.isActive')" prop="isActive">
                    <el-switch
                        v-model="formData.isActive"
                        :active-value="1"
                        :inactive-value="0"
                        :active-text="t('humpInstance.status.active')"
                        :inactive-text="t('humpInstance.status.inactive')"
                    />
                </el-form-item>
            </el-form>
            <template #footer>
                <el-button @click="dialogVisible = false">
                    {{ t('humpInstance.buttons.cancel') }}
                </el-button>
                <el-button type="primary" :loading="saving" @click="handleSubmit">
                    {{ t('humpInstance.buttons.save') }}
                </el-button>
            </template>
        </el-dialog>

        <el-dialog
            :title="tr('humpInstanceManagement.dialogs.copyTitle', '复制驼峰实例', 'Copy Hump Instance')"
            v-model="copyDialogVisible"
            width="500px"
            :close-on-click-modal="false"
        >
            <el-form ref="copyFormRef" :model="copyForm" :rules="copyRules" label-width="120px">
                <el-form-item :label="tr('humpInstanceManagement.copy.source', '原实例', 'Source Instance')">
                    <el-input :model-value="copySourceLabel" disabled />
                </el-form-item>
                <el-form-item
                    :label="tr('humpInstanceManagement.copy.newName', '新实例名称', 'New Instance Name')"
                    prop="newInstanceName"
                >
                    <el-input
                        v-model="copyForm.newInstanceName"
                        :placeholder="
                            tr(
                                'humpInstanceManagement.placeholder.newName',
                                '请输入新实例名称',
                                'Please enter a new instance name',
                            )
                        "
                    />
                </el-form-item>
                <el-alert
                    class="copy-hint"
                    :title="
                        tr(
                            'humpInstanceManagement.copy.generatedIdHint',
                            '新实例编号将由系统自动生成',
                            'The new instance ID will be generated automatically',
                        )
                    "
                    type="info"
                    :closable="false"
                    show-icon
                />
            </el-form>
            <template #footer>
                <el-button @click="copyDialogVisible = false">
                    {{ t('humpInstance.buttons.cancel') }}
                </el-button>
                <el-button type="primary" :loading="copying" @click="handleCopy">
                    {{ tr('humpInstance.buttons.copy', '复制', 'Copy') }}
                </el-button>
            </template>
        </el-dialog>
    </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import axios from '@/utils/axios'
import { ElMessage, ElMessageBox } from 'element-plus'
import { useI18n } from 'vue-i18n'
import type { FormInstance, FormRules } from 'element-plus'
import { useAuthStore } from '@/stores/auth'
import { DEFAULT_PAGE_SIZES, type PagedResult } from '@/types/pagination'
import { getHumpMissingReferenceMessage } from '@/utils/humpMissingReference'

interface HumpInstance {
    id?: string
    name: string
    owner?: string
    createdDate?: string
    isActive: number
}

const { t, te, locale } = useI18n()
const authStore = useAuthStore()
authStore.hydrateFromStorage()

const instances = ref<HumpInstance[]>([])
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

const formData = reactive<HumpInstance>({
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

const isZhLocale = computed(() => locale.value.toLowerCase().startsWith('zh'))

const interpolateText = (text: string, params?: Record<string, string | number>) => {
    if (!params) return text

    return Object.entries(params).reduce((result, [key, value]) => {
        return result.split(`{${key}}`).join(String(value))
    }, text)
}

const tr = (
    key: string,
    zhFallback: string,
    enFallback: string,
    params?: Record<string, string | number>,
) => {
    const currentLocale = locale.value
    if (te(key, currentLocale)) {
        return params ? t(key, params) : t(key)
    }

    return interpolateText(isZhLocale.value ? zhFallback : enFallback, params)
}

const rules = reactive<FormRules>({
    name: [
        { required: true, message: t('humpInstance.validation.nameRequired'), trigger: 'blur' },
        { min: 2, max: 100, message: t('humpInstance.validation.nameLength'), trigger: 'blur' },
    ],
})

const copyRules = reactive<FormRules>({
    newInstanceName: [
        { required: true, message: t('humpInstance.validation.nameRequired'), trigger: 'blur' },
        { min: 2, max: 100, message: t('humpInstance.validation.nameLength'), trigger: 'blur' },
    ],
})

const loadInstances = async () => {
    loading.value = true
    try {
        const response = await axios.get<PagedResult<HumpInstance>>('/Hump/GetInstancePage', {
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
        console.error('Failed to load instances:', error)
        instances.value = []
        pagination.totalCount = 0
        ElMessage.error(t('humpInstance.messages.loadError'))
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

const openEdit = (row: HumpInstance) => {
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

const openCopy = (row: HumpInstance) => {
    copyForm.sourceInstanceID = row.id || ''
    copyForm.newInstanceName = tr(
        'humpInstanceManagement.copy.defaultName',
        '{name}副本',
        '{name} Copy',
        { name: row.name },
    )
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
        const response = await axios.post<HumpInstance>('/Hump/CopyHumpInstance', {
            sourceInstanceID: copyForm.sourceInstanceID,
            newInstanceName: copyForm.newInstanceName.trim(),
            owner: copyForm.owner || authStore.username.trim(),
        })

        pagination.pageNumber = 1
        ElMessage.success(
            tr(
                'humpInstanceManagement.messages.copySuccess',
                '实例复制成功，新实例编号：{id}',
                'Instance copied successfully. New ID: {id}',
                { id: response.data?.id || '-' },
            ),
        )
        copyDialogVisible.value = false
        await loadInstances()
    } catch (error: any) {
        console.error('Failed to copy instance:', error)
        ElMessage.error(getHumpMissingReferenceMessage(error, 'humpInstanceManagement.messages.copyError'))
    } finally {
        copying.value = false
    }
}

const createInstance = async () => {
    try {
        await axios.post('/Hump/CreateInstance', {
            name: formData.name.trim(),
            owner: formData.owner || authStore.username.trim(),
            isActive: formData.isActive,
        })
        pagination.pageNumber = 1
        ElMessage.success(t('humpInstance.messages.createSuccess'))
        dialogVisible.value = false
        await loadInstances()
    } catch (error: any) {
        console.error('Failed to create instance:', error)
        ElMessage.error(t('humpInstance.messages.createError'))
    }
}

const editInstance = async () => {
    try {
        await axios.put('/Hump/EditInstance', {
            id: formData.id,
            name: formData.name.trim(),
            owner: formData.owner,
            isActive: formData.isActive,
        })
        ElMessage.success(t('humpInstance.messages.editSuccess'))
        dialogVisible.value = false
        await loadInstances()
    } catch (error: any) {
        console.error('Failed to edit instance:', error)
        ElMessage.error(t('humpInstance.messages.editError'))
    }
}

const confirmDelete = (row: HumpInstance) => {
    ElMessageBox.confirm(
        t('humpInstance.messages.deleteConfirm', { name: row.name }),
        t('humpInstance.dialogs.deleteTitle'),
        {
            confirmButtonText: t('humpInstance.buttons.confirm'),
            cancelButtonText: t('humpInstance.buttons.cancel'),
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
        await axios.delete('/Hump/DeleteInstance', {
            params: { id },
        })
        ElMessage.success(t('humpInstance.messages.deleteSuccess'))
        if (instances.value.length === 1 && pagination.pageNumber > 1) {
            pagination.pageNumber -= 1
        }
        await loadInstances()
    } catch (error: any) {
        console.error('Failed to delete instance:', error)
        ElMessage.error(t('humpInstance.messages.deleteError'))
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
.hump-instance-manager {
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
