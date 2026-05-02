<template>
    <div class="hump-instance-manager">
        <div class="toolbar">
            <el-button type="primary" @click="openCreate">
                {{ t('humpInstance.buttons.create') }}
            </el-button>
            <el-button type="primary" @click="loadInstances">
                {{ t('humpInstance.buttons.refresh') }}
            </el-button>
        </div>

        <el-table :data="instances" style="width:100%" v-loading="loading">
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

        <!-- 创建/编辑对话框 -->
        <el-dialog
            :title="dialogMode === 'create' ? t('humpInstance.dialogs.createTitle') : t('humpInstance.dialogs.editTitle')"
            v-model="dialogVisible" width="500px" :close-on-click-modal="false">
            <el-form :model="formData" :rules="rules" ref="formRef" label-width="120px">
                <el-form-item :label="t('humpInstance.columns.name')" prop="name">
                    <el-input v-model="formData.name" :placeholder="t('humpInstance.placeholder.name')" />
                </el-form-item>
                <el-form-item :label="t('humpInstance.columns.isActive')" prop="isActive">
                    <el-switch v-model="formData.isActive" :active-value="1" :inactive-value="0"
                        :active-text="t('humpInstance.status.active')"
                        :inactive-text="t('humpInstance.status.inactive')" />
                </el-form-item>
            </el-form>
            <template #footer>
                <el-button @click="dialogVisible = false">
                    {{ t('humpInstance.buttons.cancel') }}
                </el-button>
                <el-button type="primary" @click="handleSubmit" :loading="saving">
                    {{ t('humpInstance.buttons.save') }}
                </el-button>
            </template>
        </el-dialog>

        <el-dialog
            :title="tr('humpInstanceManagement.dialogs.copyTitle', '复制驼峰实例', 'Copy Hump Instance')"
            v-model="copyDialogVisible" width="500px" :close-on-click-modal="false">
            <el-form :model="copyForm" :rules="copyRules" ref="copyFormRef" label-width="120px">
                <el-form-item :label="tr('humpInstanceManagement.copy.source', '原实例', 'Source Instance')">
                    <el-input :model-value="copySourceLabel" disabled />
                </el-form-item>
                <el-form-item
                    :label="tr('humpInstanceManagement.copy.newName', '新实例名称', 'New Instance Name')"
                    prop="newInstanceName">
                    <el-input
                        v-model="copyForm.newInstanceName"
                        :placeholder="tr('humpInstanceManagement.placeholder.newName', '请输入新实例名称', 'Please enter a new instance name')" />
                </el-form-item>
                <el-alert
                    class="copy-hint"
                    :title="tr('humpInstanceManagement.copy.generatedIdHint', '新实例号将由系统自动生成', 'The new instance ID will be generated automatically')"
                    type="info"
                    :closable="false"
                    show-icon />
            </el-form>
            <template #footer>
                <el-button @click="copyDialogVisible = false">
                    {{ t('humpInstance.buttons.cancel') }}
                </el-button>
                <el-button type="primary" @click="handleCopy" :loading="copying">
                    {{ tr('humpInstance.buttons.copy', '复制', 'Copy') }}
                </el-button>
            </template>
        </el-dialog>
    </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted, computed } from 'vue'
import axios from '@/utils/axios'
import { ElMessage, ElMessageBox } from 'element-plus'
import { useI18n } from 'vue-i18n'
import type { FormInstance, FormRules } from 'element-plus'
import { useAuthStore } from '@/stores/auth'

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

const formData = reactive<HumpInstance>({
    id: '',
    name: '',
    owner: '',
    createdDate: '',
    isActive: 1
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
        { min: 2, max: 100, message: t('humpInstance.validation.nameLength'), trigger: 'blur' }
    ]
})

const copyRules = reactive<FormRules>({
    newInstanceName: [
        { required: true, message: t('humpInstance.validation.nameRequired'), trigger: 'blur' },
        { min: 2, max: 100, message: t('humpInstance.validation.nameLength'), trigger: 'blur' },
    ],
})

// 加载实例列表
const loadInstances = async () => {
    loading.value = true
    try {
        const response = await axios.get('/Hump/GetInstances')
        instances.value = response.data || []
        ElMessage.success(t('humpInstance.messages.loadSuccess'))
    } catch (error: any) {
        console.error('Failed to load instances:', error)
        ElMessage.error(t('humpInstance.messages.loadError'))
    } finally {
        loading.value = false
    }
}

// 打开创建对话框
const openCreate = () => {
    dialogMode.value = 'create'
    Object.assign(formData, {
        id: '',
        name: '',
        owner: authStore.username.trim(),
        createdDate: '',
        isActive: 1
    })
    dialogVisible.value = true
    // 重置表单验证
    formRef.value?.clearValidate()
}

// 打开编辑对话框
const openEdit = (row: HumpInstance) => {
    dialogMode.value = 'edit'
    Object.assign(formData, {
        id: row.id,
        name: row.name,
        owner: row.owner,
        createdDate: row.createdDate,
        isActive: row.isActive
    })
    dialogVisible.value = true
    // 重置表单验证
    formRef.value?.clearValidate()
}

// 打开复制对话框
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

// 提交表单
const handleSubmit = async () => {
    if (!formRef.value) return

    await formRef.value.validate(async (valid) => {
        if (!valid) return

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
    })
}

// 复制实例
const handleCopy = async () => {
    if (!copyFormRef.value) return

    await copyFormRef.value.validate(async (valid) => {
        if (!valid) return

        copying.value = true
        try {
            const response = await axios.post<HumpInstance>('/Hump/CopyHumpInstance', {
                sourceInstanceID: copyForm.sourceInstanceID,
                newInstanceName: copyForm.newInstanceName.trim(),
                owner: copyForm.owner || authStore.username.trim(),
            })

            ElMessage.success(
                tr(
                    'humpInstanceManagement.messages.copySuccess',
                    '实例复制成功，新实例号：{id}',
                    'Instance copied successfully. New ID: {id}',
                    { id: response.data?.id || '-' },
                ),
            )
            copyDialogVisible.value = false
            await loadInstances()
        } catch (error: any) {
            console.error('Failed to copy instance:', error)
            ElMessage.error(
                tr(
                    'humpInstanceManagement.messages.copyError',
                    '实例复制失败',
                    'Failed to copy instance',
                ),
            )
        } finally {
            copying.value = false
        }
    })
}

// 创建实例
const createInstance = async () => {
    try {
        const response = await axios.post('/Hump/CreateInstance', {
            name: formData.name,
            owner: formData.owner || authStore.username.trim(),
            isActive: formData.isActive,
        })
        ElMessage.success(t('humpInstance.messages.createSuccess'))
        dialogVisible.value = false
        await loadInstances()
    } catch (error: any) {
        console.error('Failed to create instance:', error)
        ElMessage.error(t('humpInstance.messages.createError'))
    }
}

// 编辑实例
const editInstance = async () => {
    try {
        await axios.put('/Hump/EditInstance', {
            id: formData.id,
            name: formData.name,
            owner: formData.owner,
            isActive: formData.isActive
        })
        ElMessage.success(t('humpInstance.messages.editSuccess'))
        dialogVisible.value = false
        await loadInstances()
    } catch (error: any) {
        console.error('Failed to edit instance:', error)
        ElMessage.error(t('humpInstance.messages.editError'))
    }
}

// 确认删除
const confirmDelete = (row: HumpInstance) => {
    ElMessageBox.confirm(
        t('humpInstance.messages.deleteConfirm', { name: row.name }),
        t('humpInstance.dialogs.deleteTitle'),
        {
            confirmButtonText: t('humpInstance.buttons.confirm'),
            cancelButtonText: t('humpInstance.buttons.cancel'),
            type: 'warning'
        }
    ).then(() => {
        deleteInstance(row.id!)
    }).catch(() => {
        // 取消删除
    })
}

// 删除实例
const deleteInstance = async (id: string) => {
    loading.value = true
    try {
        await axios.delete('/Hump/DeleteInstance', {
            params: { id }
        })
        ElMessage.success(t('humpInstance.messages.deleteSuccess'))
        await loadInstances()
    } catch (error: any) {
        console.error('Failed to delete instance:', error)
        ElMessage.error(t('humpInstance.messages.deleteError'))
    } finally {
        loading.value = false
    }
}

// 格式化日期
const formatDate = (dateString: string) => {
    if (!dateString) return ''
    const date = new Date(dateString)
    return date.toLocaleString()
}

// 组件挂载时加载数据
onMounted(() => {
    loadInstances()
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

.copy-hint {
    margin-top: 8px;
}
</style>
