<template>
    <section class="instance-management-page">
        <header class="page-header">
            <div>
                <h2>{{ t("humpInstanceManagement.title") }}</h2>
                <p class="subtitle">{{ t("humpInstanceManagement.subtitle") }}</p>
            </div>
            <div class="header-actions">
                <el-button size="small" icon="el-icon-refresh" @click="loadInstances" :loading="loading">
                    {{ t("humpInstanceManagement.toolbar.refresh") }}
                </el-button>
                <el-button size="small" type="primary" icon="el-icon-plus" @click="openCreate">
                    {{ t("humpInstanceManagement.toolbar.create") }}
                </el-button>
            </div>
        </header>

        <el-table
            :data="instances"
            stripe
            style="width:100%"
            v-loading="loading"
            :empty-text="t('humpInstanceManagement.empty')"
        >
            <el-table-column prop="id" :label="t('humpInstanceManagement.columns.id')" width="220" />
            <el-table-column prop="name" :label="t('humpInstance.columns.name')" min-width="220" />
            <el-table-column :label="t('humpInstance.columns.owner')" min-width="200">
                <template #default="{ row }">
                    {{ resolveOwnerLabel(row.owner) }}
                </template>
            </el-table-column>
            <el-table-column prop="createdDate" :label="t('humpInstance.columns.createdDate')" width="200">
                <template #default="{ row }">
                    {{ formatDate(row.createdDate) }}
                </template>
            </el-table-column>
            <el-table-column :label="t('humpInstance.columns.isActive')" width="140">
                <template #default="{ row }">
                    <el-tag :type="row.isActive === 1 ? 'success' : 'info'">
                        {{ row.isActive === 1 ? t('humpInstance.status.active') : t('humpInstance.status.inactive') }}
                    </el-tag>
                </template>
            </el-table-column>
            <el-table-column :label="t('humpInstance.columns.actions')" width="180" fixed="right">
                <template #default="{ row }">
                    <el-button size="small" type="primary" @click="openEdit(row)">
                        {{ t('humpInstance.buttons.edit') }}
                    </el-button>
                </template>
            </el-table-column>
        </el-table>

        <el-dialog
            :title="dialogMode === 'create' ? t('humpInstance.dialogs.createTitle') : t('humpInstance.dialogs.editTitle')"
            v-model="dialogVisible"
            width="560px"
            :close-on-click-modal="false"
        >
            <el-form ref="formRef" :model="formData" :rules="formRules" label-width="120px">
                <el-form-item prop="name" :label="t('humpInstance.columns.name')">
                    <el-input v-model="formData.name" :placeholder="t('humpInstance.placeholder.name')" />
                </el-form-item>

                <el-form-item prop="owner" :label="t('humpInstance.columns.owner')">
                    <el-select
                        v-model="formData.owner"
                        :placeholder="t('humpInstanceManagement.placeholder.owner')"
                        :disabled="users.length === 0"
                        filterable
                    >
                        <el-option v-for="user in users" :key="user.id" :label="user.name" :value="user.id" />
                    </el-select>
                </el-form-item>

                <el-form-item prop="isActive" :label="t('humpInstance.columns.isActive')">
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
                <el-button @click="dialogVisible = false">{{ t('humpInstance.buttons.cancel') }}</el-button>
                <el-button type="primary" :loading="saving" @click="handleSubmit">
                    {{ t('humpInstance.buttons.save') }}
                </el-button>
            </template>
        </el-dialog>
    </section>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import axios from '@/utils/axios'
import { ElMessage } from 'element-plus'
import type { FormInstance, FormRules } from 'element-plus'

interface HumpInstance {
    id: string
    name: string
    owner?: string
    createdDate?: string
    isActive: number
}

interface UserRecord {
    id: string
    name: string
    role: string
}

const { t } = useI18n()
const instances = ref<HumpInstance[]>([])
const users = ref<UserRecord[]>([])
const loading = ref(false)
const saving = ref(false)
const dialogVisible = ref(false)
const dialogMode = ref<'create' | 'edit'>('create')
const formRef = ref<FormInstance>()
const formData = reactive({
    id: '',
    name: '',
    owner: '',
    isActive: 1,
})

const formRules = reactive<FormRules>({
    name: [
        { required: true, message: t('humpInstance.validation.nameRequired'), trigger: 'blur' },
        { min: 2, max: 100, message: t('humpInstance.validation.nameLength'), trigger: 'blur' },
    ],
    owner: [{ required: true, message: t('humpInstanceManagement.validation.ownerRequired'), trigger: 'change' }],
})

const ownerLookup = computed(() => {
    const map = new Map<string, string>()
    users.value.forEach((user) => {
        map.set(user.id, user.name)
    })
    return map
})

const resolveOwnerLabel = (ownerId?: string) => {
    if (!ownerId) {
        return t('humpInstanceManagement.labels.ownerUnknown')
    }

    return ownerLookup.value.get(ownerId) || ownerId
}

const formatDate = (value?: string) => {
    if (!value) return '-'

    const date = new Date(value)
    if (Number.isNaN(date.getTime())) return value

    return date.toLocaleString()
}

const loadInstances = async () => {
    loading.value = true
    try {
        const response = await axios.get<HumpInstance[]>('/Hump/GetInstances')
        instances.value = (response.data || []).map((item) => ({
            ...item,
            isActive: Number(item.isActive ?? 1),
        }))
    } catch (error: any) {
        console.error('Failed to load instances', error)
        ElMessage.error(t('humpInstance.messages.loadError'))
        instances.value = []
    } finally {
        loading.value = false
    }
}

const loadUsers = async () => {
    try {
        const response = await axios.get<UserRecord[]>('/api/Admin/users')
        users.value = (response.data || []).map((item) => ({
            id: item.id,
            name: item.name,
            role: item.role,
        }))

        if (!formData.owner && users.value.length > 0) {
            formData.owner = users.value[0]?.id || ''
        }
    } catch (error: any) {
        console.error('Failed to load users', error)
        ElMessage.error(t('humpInstanceManagement.messages.userLoadError'))
    }
}

const resetForm = () => {
    formData.id = ''
    formData.name = ''
    formData.isActive = 1
    formData.owner = users.value[0]?.id || ''
}

const openCreate = () => {
    dialogMode.value = 'create'
    resetForm()
    dialogVisible.value = true
    formRef.value?.clearValidate()
}

const openEdit = (instance: HumpInstance) => {
    dialogMode.value = 'edit'
    formData.id = instance.id
    formData.name = instance.name
    formData.owner = instance.owner || users.value[0]?.id || ''
    formData.isActive = Number(instance.isActive)
    dialogVisible.value = true
    formRef.value?.clearValidate()
}

const handleSubmit = async () => {
    if (!formRef.value) return

    await formRef.value.validate(async (valid) => {
        if (!valid) return

        saving.value = true
        try {
            if (dialogMode.value === 'create') {
                await axios.post('/Hump/CreateInstance', {
                    name: formData.name.trim(),
                    owner: formData.owner,
                    isActive: formData.isActive,
                })
                ElMessage.success(t('humpInstance.messages.createSuccess'))
            } else {
                await axios.put('/Hump/EditInstance', {
                    id: formData.id,
                    name: formData.name.trim(),
                    owner: formData.owner,
                    isActive: formData.isActive,
                })
                ElMessage.success(t('humpInstance.messages.editSuccess'))
            }

            dialogVisible.value = false
            await loadInstances()
        } catch (error: any) {
            console.error('Failed to save instance', error)
            ElMessage.error(
                dialogMode.value === 'create'
                    ? t('humpInstance.messages.createError')
                    : t('humpInstance.messages.editError'),
            )
        } finally {
            saving.value = false
        }
    })
}

onMounted(() => {
    void loadUsers()
    void loadInstances()
})
</script>

<style scoped>
.instance-management-page {
    padding: 24px;
    background: #fff;
    min-height: 100%;
    display: flex;
    flex-direction: column;
    gap: 12px;
}

.page-header {
    display: flex;
    justify-content: space-between;
    align-items: flex-end;
    gap: 20px;
    flex-wrap: wrap;
}

.subtitle {
    color: #6b7280;
    margin: 4px 0 0;
    font-size: 14px;
}

.header-actions {
    display: flex;
    gap: 10px;
}
</style>
