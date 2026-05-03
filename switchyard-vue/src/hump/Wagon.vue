<template>
    <div>
        <div class="wagon-toolbar">
            <el-button @click="addNewWagon" type="primary" size="small">{{ t('wagon.new') }}</el-button>
        </div>
        <el-table :data="wagonData" style="width: 100%;">
            <el-table-column prop="typeName" :label="t('wagon.typeName')" width="120">
                <template #default="scope">
                    <span v-if="!scope.row.isEditing || !scope.row.isNew">{{ scope.row.typeName }}</span>
                    <el-input v-else v-model="scope.row.typeName" />
                </template>
            </el-table-column>
            <el-table-column prop="length" :label="t('wagon.length')" width="100">
                <template #default="scope">
                    <span v-if="!scope.row.isEditing">{{ scope.row.length }}</span>
                    <el-input v-else v-model.number="scope.row.length" />
                </template>
            </el-table-column>
            <el-table-column prop="netMass" :label="t('wagon.netMass')" width="100">
                <template #default="scope">
                    <span v-if="!scope.row.isEditing">{{ scope.row.netMass }}</span>
                    <el-input v-else v-model.number="scope.row.netMass" />
                </template>
            </el-table-column>
            <el-table-column prop="loadingMass" :label="t('wagon.loadingMass')" width="100">
                <template #default="scope">
                    <span v-if="!scope.row.isEditing">{{ scope.row.loadingMass }}</span>
                    <el-input v-else v-model.number="scope.row.loadingMass" />
                </template>
            </el-table-column>
            <el-table-column prop="grossMass" :label="t('wagon.grossMass')" width="120">
                <template #default="scope">
                    {{ scope.row.netMass + scope.row.loadingMass }}
                </template>
            </el-table-column>
            <el-table-column prop="windwardArea" :label="t('wagon.windwardArea')" width="120">
                <template #default="scope">
                    <span v-if="!scope.row.isEditing">{{ scope.row.windwardArea }}</span>
                    <el-input v-else v-model.number="scope.row.windwardArea" />
                </template>
            </el-table-column>
            <el-table-column prop="axleNumber" :label="t('wagon.axleNumber')" width="80">
                <template #default="scope">
                    <span v-if="!scope.row.isEditing">{{ scope.row.axleNumber }}</span>
                    <el-input v-else v-model.number="scope.row.axleNumber" />
                </template>
            </el-table-column>
            <el-table-column prop="label" :label="t('wagon.label')" width="100">
                <template #default="scope">
                    <span v-if="!scope.row.isEditing">{{ getLabelText(scope.row.label) }}</span>
                    <el-select v-else v-model="scope.row.label" :placeholder="t('wagon.chooseLabel')"
                        style="width: 100%;">
                        <el-option :label="t('wagon.labelHard')" value="难行车" />
                        <el-option :label="t('wagon.labelMedium')" value="中行车" />
                        <el-option :label="t('wagon.labelEasy')" value="易行车" />
                    </el-select>
                </template>
            </el-table-column>
            <el-table-column prop="g" :label="t('wagon.gravity')" width="120">
                <template #default="scope">
                    <span v-if="!scope.row.isEditing">{{ scope.row.g }}</span>
                    <el-input v-else v-model.number="scope.row.g" />
                </template>
            </el-table-column>
            <el-table-column :label="t('wagon.actionsLabel')" width="200">
                <template #default="scope">
                    <el-button v-if="!scope.row.isEditing" @click="editRow(scope.$index)" size="small">{{
                        t('wagon.actions.edit') }}</el-button>
                    <el-button v-if="scope.row.isEditing" @click="saveRow(scope.$index)" size="small" type="primary"
                        :loading="scope.row.saving" :disabled="scope.row.saving">{{
                            t('wagon.actions.save') }}</el-button>
                    <el-button v-if="scope.row.isEditing" @click="cancelEdit(scope.$index)" size="small">{{
                        t('wagon.actions.cancel') }}</el-button>
                    <el-button @click="deleteRow(scope.$index)" size="small" type="danger" :loading="scope.row.deleting"
                        :disabled="scope.row.deleting">{{ t('wagon.actions.delete')
                        }}</el-button>
                </template>
            </el-table-column>
        </el-table>
    </div>
</template>

<script setup lang="ts">
import { onMounted, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n'
import axios from '@/utils/axios';
import { ElMessage, ElMessageBox } from 'element-plus';

const { t } = useI18n()

interface Props {
    selectedInstanceId?: string | null
}

const props = withDefaults(defineProps<Props>(), {
    selectedInstanceId: null
})

// 定义车辆数据结构接口
interface WagonConcept {
    instanceID?: string;
    typeName: string;
    length: number;
    netMass: number;
    loadingMass: number;
    grossMass?: number; // 计算字段
    windwardArea: number;
    axleNumber: number;
    label: string;
    g: number;
    isEditing?: boolean;
    isNew?: boolean;
    saving?: boolean;
    deleting?: boolean;
    originalData?: WagonConcept;
}

const currentInstanceId = ref<string>("");

// 模拟车辆数据
const wagonData = ref<WagonConcept[]>([]);

// 获取标签的显示文本
const getLabelText = (labelValue: string): string => {
    switch (labelValue) {
        case '难行车':
            return t('wagon.labelHard');
        case '中行车':
            return t('wagon.labelMedium');
        case '易行车':
            return t('wagon.labelEasy');
        default:
            return labelValue;
    }
};

// 验证表单数据
const validateWagonData = (wagon: WagonConcept): string | null => {
    if (!wagon.typeName.trim()) {
        return t('wagon.validation.typeNameRequired');
    }
    if (wagon.length <= 0) {
        return t('wagon.validation.lengthRequired');
    }
    if (wagon.netMass <= 0) {
        return t('wagon.validation.netMassRequired');
    }
    if (wagon.windwardArea <= 0) {
        return t('wagon.validation.windwardAreaRequired');
    }
    if (wagon.axleNumber <= 0) {
        return t('wagon.validation.axleNumberRequired');
    }
    if (wagon.g <= 0) {
        return t('wagon.validation.gravityRequired');
    }
    return null;
};

// 新建车型
const addNewWagon = () => {
    if (!currentInstanceId.value) {
        ElMessage.warning(t('wagon.messages.noInstanceSelected'));
        return;
    }

    wagonData.value.push({
        instanceID: currentInstanceId.value,
        typeName: '',
        length: 12.0,
        netMass: 20.0,
        loadingMass: 60.0,
        windwardArea: 10.0,
        axleNumber: 4,
        label: '易行车',
        g: 9.8,
        isEditing: true,
        isNew: true,
        saving: false,
        deleting: false
    });
};

// 编辑行
const editRow = (index: number) => {
    if (wagonData.value[index]) {
        // 保存原始数据用于取消编辑时恢复
        wagonData.value[index].originalData = { ...wagonData.value[index] };
        wagonData.value[index].isEditing = true;
    }
};

// 取消编辑
const cancelEdit = (index: number) => {
    if (wagonData.value[index]) {
        if (wagonData.value[index].isNew) {
            // 如果是新建的行，直接删除
            wagonData.value.splice(index, 1);
        } else {
            // 恢复原始数据
            const originalData = wagonData.value[index].originalData;
            if (originalData) {
                Object.assign(wagonData.value[index], originalData);
            }
            wagonData.value[index].isEditing = false;
            wagonData.value[index].originalData = undefined;
        }
    }
};

// 保存行
const saveRow = async (index: number) => {
    const wagon = wagonData.value[index];
    if (!wagon) return;
    const effectiveTypeName = wagon.isNew
        ? wagon.typeName.trim()
        : wagon.originalData?.typeName?.trim() || wagon.typeName.trim();

    wagon.typeName = effectiveTypeName;

    // 验证数据
    const validationError = validateWagonData(wagon);
    if (validationError) {
        ElMessage.error(validationError);
        return;
    }

    // 检查车型名是否重复
    const duplicateIndex = wagonData.value.findIndex((item, idx) =>
        idx !== index && item.typeName === effectiveTypeName
    );
    if (duplicateIndex !== -1) {
        ElMessage.error(t('wagon.validation.typeNameExists'));
        return;
    }

    wagon.saving = true;

    try {
        const wagonPayload = {
            instanceID: currentInstanceId.value,
            typeName: effectiveTypeName,
            length: wagon.length,
            netMass: wagon.netMass,
            loadingMass: wagon.loadingMass,
            windwardArea: wagon.windwardArea,
            axleNumber: wagon.axleNumber,
            label: wagon.label,
            g: wagon.g
        };

        if (wagon.isNew) {
            // 创建新车辆概念
            const response = await axios.post('/Hump/CreateWagonConcept', wagonPayload);
            wagon.isNew = false;
            ElMessage.success(t('wagon.messages.created'));
        } else {
            // 更新现有车辆概念
            const response = await axios.put('/Hump/EditWagonConcept', wagonPayload);
            ElMessage.success(t('wagon.messages.updated'));
        }

        wagon.isEditing = false;
        wagon.originalData = undefined;
    } catch (error: any) {
        console.error('保存车辆概念失败:', error);
        ElMessage.error(t('wagon.messages.saveFailed'));
    } finally {
        wagon.saving = false;
    }
};

// 删除行
const deleteRow = async (index: number) => {
    const wagon = wagonData.value[index];
    if (!wagon) return;

    if (wagon.isNew) {
        // 如果是新建的行，直接移除
        wagonData.value.splice(index, 1);
        return;
    }

    try {
        await ElMessageBox.confirm(
            t('wagon.messages.confirmDelete', { typeName: wagon.typeName }),
            t('wagon.messages.warning'),
            {
                confirmButtonText: t('common.confirm'),
                cancelButtonText: t('common.cancel'),
                type: 'warning',
            }
        );

        wagon.deleting = true;

        try {
            await axios.delete('/Hump/DeleteWagonConcept', {
                params: {
                    instanceID: currentInstanceId.value,
                    typeName: wagon.typeName
                }
            });

            wagonData.value.splice(index, 1);
            ElMessage.success(t('wagon.messages.deleted'));
        } catch (error: any) {
            console.error('删除车辆概念失败:', error);
            ElMessage.error(t('wagon.messages.deleteFailed'));
        } finally {
            wagon.deleting = false;
        }
    } catch (error: any) {
        if (error !== 'cancel') {
            console.error('删除确认失败:', error);
        }
    }
};

// 加载车辆概念数据
const loadWagonConcept = async () => {
    if (!currentInstanceId.value) {
        wagonData.value = [];
        return;
    }

    try {
        const response = await axios.get('/Hump/GetWagonConcept', {
            params: { instanceID: currentInstanceId.value }
        });

        wagonData.value = (response.data || []).map((item: any) => ({
            ...item,
            isEditing: false,
            isNew: false,
            saving: false,
            deleting: false
        }));

        console.log('加载车辆概念数据成功:', wagonData.value);
    } catch (error: any) {
        console.error('加载车辆概念数据失败:', error);
        ElMessage.error(t('wagon.messages.loadFailed'));
        wagonData.value = [];
    }
};

// 监听 selectedInstanceId 变化
watch(
    () => props.selectedInstanceId,
    (newInstanceId) => {
        if (newInstanceId) {
            currentInstanceId.value = newInstanceId;
            loadWagonConcept();
        } else {
            currentInstanceId.value = "";
            // 清空数据
            wagonData.value = [];
        }
    },
    { immediate: true }
);

onMounted(() => {
    // loadWagonConcept();
})
</script>

<style scoped lang="css">
.wagon-toolbar {
    display: flex;
    justify-content: flex-end;
    margin-bottom: 12px;
}
</style>
