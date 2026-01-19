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
            <el-table-column :label="t('humpInstance.columns.actions')" width="200" fixed="right">
                <template #default="{ row }">
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
    </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import axios from '@/utils/axios'
import { ElMessage, ElMessageBox } from 'element-plus'
import { useI18n } from 'vue-i18n'
import type { FormInstance, FormRules } from 'element-plus'

interface HumpInstance {
    id?: string
    name: string
    owner?: string
    createdDate?: string
    isActive: number
}

const { t } = useI18n()
const instances = ref<HumpInstance[]>([])
const loading = ref(false)
const saving = ref(false)
const dialogVisible = ref(false)
const dialogMode = ref<'create' | 'edit'>('create')
const formRef = ref<FormInstance>()

const formData = reactive<HumpInstance>({
    id: '',
    name: '',
    owner: '',
    createdDate: '',
    isActive: 1
})

const rules = reactive<FormRules>({
    name: [
        { required: true, message: t('humpInstance.validation.nameRequired'), trigger: 'blur' },
        { min: 2, max: 100, message: t('humpInstance.validation.nameLength'), trigger: 'blur' }
    ]
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
        owner: '',
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

// 创建实例
const createInstance = async () => {
    try {
        const response = await axios.post('/Hump/CreateInstance', {
            name: formData.name,
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
</style>
