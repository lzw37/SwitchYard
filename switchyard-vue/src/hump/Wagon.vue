<template>
    <div>
        <el-button @click="addNewWagon" type="primary">{{ t('wagon.new') }}</el-button>
        <el-table :data="wagonData" style="width: 100%; margin-top: 20px;">
            <el-table-column prop="typeName" label="车型" width="120">
                <template #default="scope">
                    <span v-if="!scope.row.isEditing">{{ scope.row.typeName }}</span>
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
                    <span v-if="!scope.row.isEditing">{{ scope.row.label }}</span>
                    <el-select v-else v-model="scope.row.label" :placeholder="t('wagon.chooseLabel')"
                        style="width: 100%;">
                        <el-option :label="t('wagon.labelHard')" value="难行车" />
                        <el-option :label="t('wagon.labelMedium')" value="中行车" />
                        <el-option :label="t('wagon.labelEasy')" value="易行车" />
                    </el-select>
                </template>
            </el-table-column>
            <el-table-column :label="t('wagon.actionsLabel')" width="150">
                <template #default="scope">
                    <el-button v-if="!scope.row.isEditing" @click="editRow(scope.$index)" size="small">{{
                        t('wagon.actions.edit') }}</el-button>
                    <el-button v-if="scope.row.isEditing" @click="saveRow(scope.$index)" size="small" type="primary">{{
                        t('wagon.actions.save') }}</el-button>
                    <el-button @click="deleteRow(scope.$index)" size="small" type="danger">{{ t('wagon.actions.delete')
                        }}</el-button>
                </template>
            </el-table-column>
        </el-table>
    </div>
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue';
import { useI18n } from 'vue-i18n'
import axios from '@/utils/axios';
import config from '../config.json';

// 定义车辆数据结构接口
interface WagonConcept {
    typeName: string;
    length: number;
    netMass: number;
    loadingMass: number;
    grossMass: number;
    windwardArea: number;
    axleNumber: number;
    label: string;
    isEditing: boolean;
}

// 模拟车辆数据
const wagonData = ref<WagonConcept[]>([]);

// 新建车型
const addNewWagon = () => {
    wagonData.value.push({
        typeName: '',
        length: 0,
        netMass: 0,
        loadingMass: 0,
        grossMass: 0,
        windwardArea: 0,
        axleNumber: 0,
        label: '易行车',
        isEditing: true
    });
};

// 编辑行
const editRow = (index: number) => {
    if (wagonData.value[index]) {
        wagonData.value[index].isEditing = true;
    }
};

// 保存行
const saveRow = (index: number) => {
    if (wagonData.value[index]) {
        wagonData.value[index].isEditing = false;
    }
};

// 删除行
const deleteRow = (index: number) => {
    wagonData.value.splice(index, 1);
};

// 加载车辆概念数据
function loadWagonConcept() {
    axios.get(`${config.serverurl}/hump/getwagonconcept`, { params: { instanceID: '001' } }).then(response => {
        wagonData.value = response.data.map((item: any) => ({
            ...item,
            isEditing: false
        }));
    }).catch(error => {
        console.error("加载车辆概念数据失败:", error);
    });
}

onMounted(() => {
    loadWagonConcept();
})
const { t } = useI18n()
</script>

<style scoped lang="css"></style>
