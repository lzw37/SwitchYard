<template>
    <section class="instance-management-page">
        <header class="page-header">
            <div>
                <h2>{{ t('humpInstanceManagement.title') }}</h2>
                <p class="subtitle">{{ t('humpInstanceManagement.subtitle') }}</p>
            </div>
            <div class="header-actions">
                <el-button size="small" icon="el-icon-refresh" @click="loadInstances" :loading="loading">
                    {{ t('humpInstanceManagement.toolbar.refresh') }}
                </el-button>
                <el-button size="small" type="primary" icon="el-icon-plus" @click="openCreate">
                    {{ t('humpInstanceManagement.toolbar.create') }}
                </el-button>
            </div>
        </header>

        <el-table
            :data="instances"
            stripe
            style="width: 100%"
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
            <el-table-column :label="t('humpInstance.columns.actions')" width="260" fixed="right">
                <template #default="{ row }">
                    <el-button size="small" type="success" @click="openCopy(row)">
                        {{ tr('humpInstance.buttons.copy', '复制', 'Copy') }}
                    </el-button>
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

        <el-dialog
            :title="tr('humpInstanceManagement.dialogs.copyTitle', '复制驼峰实例', 'Copy Hump Instance')"
            v-model="copyDialogVisible"
            width="560px"
            :close-on-click-modal="false"
        >
            <el-form ref="copyFormRef" :model="copyForm" :rules="copyFormRules" label-width="120px">
                <el-form-item :label="tr('humpInstanceManagement.copy.source', '原实例', 'Source Instance')">
                    <el-input :model-value="copySourceLabel" disabled />
                </el-form-item>

                <el-form-item
                    prop="newInstanceName"
                    :label="tr('humpInstanceManagement.copy.newName', '新实例名称', 'New Instance Name')"
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

                <el-form-item prop="owner" :label="t('humpInstance.columns.owner')">
                    <el-select
                        v-model="copyForm.owner"
                        :placeholder="t('humpInstanceManagement.placeholder.owner')"
                        :disabled="users.length === 0"
                        filterable
                    >
                        <el-option v-for="user in users" :key="user.id" :label="user.name" :value="user.id" />
                    </el-select>
                </el-form-item>

                <el-alert
                    class="copy-hint"
                    :title="
                        tr(
                            'humpInstanceManagement.copy.generatedIdHint',
                            '新实例号将由系统自动生成',
                            'The new instance ID will be generated automatically',
                        )
                    "
                    type="info"
                    :closable="false"
                    show-icon
                />
            </el-form>

            <template #footer>
                <el-button @click="copyDialogVisible = false">{{ t('humpInstance.buttons.cancel') }}</el-button>
                <el-button type="primary" :loading="copying" @click="handleCopy">
                    {{ tr('humpInstance.buttons.copy', '复制', 'Copy') }}
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

const { t, te, locale } = useI18n()
const instances = ref<HumpInstance[]>([])
const users = ref<UserRecord[]>([])
const loading = ref(false)
const saving = ref(false)
const copying = ref(false)
const dialogVisible = ref(false)
const copyDialogVisible = ref(false)
const dialogMode = ref<'create' | 'edit'>('create')
const formRef = ref<FormInstance>()
const copyFormRef = ref<FormInstance>()
const copySourceLabel = ref('')

const formData = reactive({
    id: '',
    name: '',
    owner: '',
    isActive: 1,
})

const copyForm = reactive({
    sourceInstanceID: '',
    newInstanceName: '',
    owner: '',
})

const isZhLocale = computed(() => locale.value.toLowerCase().startsWith('zh'))

const interpolateText = (text: string, params?: Record<string, string | number>) => {
    if (!params) {
        return text
    }

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
        const translated = params ? t(key, params) : t(key)
        return translated
    }

    return interpolateText(isZhLocale.value ? zhFallback : enFallback, params)
}

const formRules = reactive<FormRules>({
    name: [
        { required: true, message: t('humpInstance.validation.nameRequired'), trigger: 'blur' },
        { min: 2, max: 100, message: t('humpInstance.validation.nameLength'), trigger: 'blur' },
    ],
    owner: [{ required: true, message: t('humpInstanceManagement.validation.ownerRequired'), trigger: 'change' }],
})

const copyFormRules = reactive<FormRules>({
    newInstanceName: [
        { required: true, message: t('humpInstance.validation.nameRequired'), trigger: 'blur' },
        { min: 2, max: 100, message: t('humpInstance.validation.nameLength'), trigger: 'blur' },
    ],
    owner: [{ required: true, message: t('humpInstanceManagement.validation.ownerRequired'), trigger: 'change' }],
})

const ownerLookup = computed(() => {
    const map = new Map<string, string>()
    users.value.forEach((user) => {
        map.set(user.id, user.name)
        map.set(user.name, user.name)
    })
    return map
})

const resolveOwnerLabel = (ownerId?: string) => {
    if (!ownerId) {
        return t('humpInstanceManagement.labels.ownerUnknown')
    }

    return ownerLookup.value.get(ownerId) || ownerId
}

const resolveOwnerValue = (ownerId?: string) => {
    if (!ownerId) {
        return users.value[0]?.id || ''
    }

    return users.value.find((user) => user.id === ownerId || user.name === ownerId)?.id || users.value[0]?.id || ''
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

        if (!copyForm.owner && users.value.length > 0) {
            copyForm.owner = users.value[0]?.id || ''
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
    formData.owner = resolveOwnerValue(instance.owner)
    formData.isActive = Number(instance.isActive)
    dialogVisible.value = true
    formRef.value?.clearValidate()
}

const openCopy = (instance: HumpInstance) => {
    copyForm.sourceInstanceID = instance.id
    copyForm.newInstanceName = tr(
        'humpInstanceManagement.copy.defaultName',
        '{name}副本',
        '{name} Copy',
        { name: instance.name },
    )
    copyForm.owner = resolveOwnerValue(instance.owner)
    copySourceLabel.value = `${instance.name} (${instance.id})`
    copyDialogVisible.value = true
    copyFormRef.value?.clearValidate()
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

const handleCopy = async () => {
    if (!copyFormRef.value) return

    await copyFormRef.value.validate(async (valid) => {
        if (!valid) return

        copying.value = true
        try {
            const response = await axios.post<HumpInstance>('/Hump/CopyHumpInstance', {
                sourceInstanceId: copyForm.sourceInstanceID,
                newInstanceName: copyForm.newInstanceName.trim(),
                owner: copyForm.owner,
            })

            copyDialogVisible.value = false
            ElMessage.success(
                tr(
                    'humpInstanceManagement.messages.copySuccess',
                    '实例复制成功，新实例号：{id}',
                    'Instance copied successfully. New ID: {id}',
                    { id: response.data?.id || '-' },
                ),
            )
            await loadInstances()
        } catch (error: any) {
            console.error('Failed to copy instance', error)
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

.copy-hint {
    margin-top: 8px;
}
</style>
