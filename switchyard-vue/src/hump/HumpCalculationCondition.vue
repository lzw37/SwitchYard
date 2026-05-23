<template>
    <div class="calculation-condition-container">
        <div class="condition-toolbar">
            <el-button type="primary" size="small" @click="addCondition"
                :disabled="!currentInstanceId">
                {{ t('hump.buttons.add') }}
            </el-button>
        </div>

        <el-table :data="conditionsList" class="condition-table" row-key="localKey"
            v-loading="loading">
            <el-table-column prop="name" :label="t('hump.calcCondition.labels.conditionName')" width="120"
                show-overflow-tooltip>
                <template #default="{ row }">
                    <el-input v-if="row.isEditing" v-model="row.name" size="small"
                        :placeholder="t('hump.calcCondition.placeholders.conditionName')" />
                    <span v-else>{{ row.name }}</span>
                </template>
            </el-table-column>

            <el-table-column width="112" align="center" header-align="center">
                <template #header>
                    <div class="condition-header">
                        <span>{{ t('hump.calcCondition.labels.peakVelocity') }}</span>
                        <span class="condition-header__unit">({{ t('units.m_s') }})</span>
                    </div>
                </template>
                <template #default="{ row }">
                    <el-input-number v-if="row.isEditing" v-model="row.wagonVelocityOnTop" :min="0" :step="0.1"
                        :precision="2" controls-position="right" size="small" class="table-number-input" />
                    <span v-else>{{ formatNumber(row.wagonVelocityOnTop, 2) }}</span>
                </template>
            </el-table-column>

            <el-table-column width="128" align="center" header-align="center">
                <template #header>
                    <div class="condition-header">
                        <span>{{ t('hump.calcCondition.labels.slopeAvgVelocity') }}</span>
                        <span class="condition-header__unit">({{ t('units.m_s') }})</span>
                    </div>
                </template>
                <template #default="{ row }">
                    <el-input-number v-if="row.isEditing" v-model="row.wagonVelocityOnSlope" :min="0" :step="0.1"
                        :precision="2" controls-position="right" size="small" class="table-number-input" />
                    <span v-else>{{ formatNumber(row.wagonVelocityOnSlope, 2) }}</span>
                </template>
            </el-table-column>

            <el-table-column width="132" align="center" header-align="center">
                <template #header>
                    <div class="condition-header">
                        <span>{{ t('hump.calcCondition.labels.yardAvgVelocity') }}</span>
                        <span class="condition-header__unit">({{ t('units.m_s') }})</span>
                    </div>
                </template>
                <template #default="{ row }">
                    <el-input-number v-if="row.isEditing" v-model="row.wagonVelocityOnYard" :min="0" :step="0.1"
                        :precision="2" controls-position="right" size="small" class="table-number-input" />
                    <span v-else>{{ formatNumber(row.wagonVelocityOnYard, 2) }}</span>
                </template>
            </el-table-column>

            <el-table-column width="96" align="center" header-align="center">
                <template #header>
                    <div class="condition-header">
                        <span>{{ t('hump.calcCondition.labels.windVelocity') }}</span>
                        <span class="condition-header__unit">({{ t('units.m_s') }})</span>
                    </div>
                </template>
                <template #default="{ row }">
                    <el-input-number v-if="row.isEditing" v-model="row.windVelocity" :min="0" :step="0.1"
                        :precision="2" controls-position="right" size="small" class="table-number-input" />
                    <span v-else>{{ formatNumber(row.windVelocity, 2) }}</span>
                </template>
            </el-table-column>

            <el-table-column prop="isHeadWind" :label="t('hump.calcCondition.labels.windDirection')" width="88"
                align="center" header-align="center">
                <template #default="{ row }">
                    <el-select v-if="row.isEditing" v-model="row.isHeadWind" size="small" class="table-select">
                        <el-option :label="t('hump.calcCondition.options.headwind')" :value="1" />
                        <el-option :label="t('hump.calcCondition.options.tailwind')" :value="0" />
                    </el-select>
                    <span v-else>{{ getWindDirectionText(row.isHeadWind) }}</span>
                </template>
            </el-table-column>

            <el-table-column width="124" align="center" header-align="center">
                <template #header>
                    <div class="condition-header">
                        <span>{{ t('hump.calcCondition.labels.airDensity') }}</span>
                        <span class="condition-header__unit">({{ t('units.kg_s2_m4') }})</span>
                    </div>
                </template>
                <template #default="{ row }">
                    <el-input-number v-if="row.isEditing" v-model="row.airDensity" :min="0" :step="0.001"
                        :precision="4" controls-position="right" size="small" class="table-number-input" />
                    <span v-else>{{ formatNumber(row.airDensity, 4) }}</span>
                </template>
            </el-table-column>

            <el-table-column width="96" align="center" header-align="center">
                <template #header>
                    <div class="condition-header">
                        <span>{{ t('hump.calcCondition.labels.temperature') }}</span>
                        <span class="condition-header__unit">({{ t('units.deg_c') }})</span>
                    </div>
                </template>
                <template #default="{ row }">
                    <el-input-number v-if="row.isEditing" v-model="row.temperature" :step="1" :precision="0"
                        controls-position="right" size="small" class="table-number-input" />
                    <span v-else>{{ formatNumber(row.temperature, 0) }}</span>
                </template>
            </el-table-column>

            <el-table-column :label="t('hump.operation')" width="150" fixed="right" align="center"
                header-align="center">
                <template #default="{ row, $index }">
                    <div class="row-actions">
                        <template v-if="row.isEditing">
                            <el-button type="primary" size="small" @click="saveConditionRow($index)"
                                :loading="row.saving" :disabled="row.saving">
                                {{ t('hump.buttons.save') }}
                            </el-button>
                            <el-button size="small" @click="cancelEdit($index)"
                                :disabled="row.saving">
                                {{ t('hump.buttons.cancel') }}
                            </el-button>
                        </template>
                        <template v-else>
                            <el-button type="primary" size="small" @click="editRow($index)">
                                {{ t('hump.buttons.edit') }}
                            </el-button>
                            <el-button type="danger" size="small" @click="deleteConditionRow($index)"
                                :loading="row.deleting" :disabled="row.deleting">
                                {{ t('hump.buttons.delete') }}
                            </el-button>
                        </template>
                    </div>
                </template>
            </el-table-column>
        </el-table>
    </div>
</template>

<script setup lang="ts">
import { ref, watch } from "vue";
import { ElMessage, ElMessageBox } from "element-plus";
import { useI18n } from "vue-i18n";
import axios from "../utils/axios";

interface Props {
    selectedInstanceId?: string | null
    activationKey?: number
}

interface OperationCondition {
    instanceID?: string;
    id?: string;
    name: string;
    wagonVelocityOnTop: number;
    wagonVelocityOnSlope: number;
    wagonVelocityOnYard: number;
    windVelocity: number;
    isHeadWind: number;
    airDensity: number;
    temperature: number;
}

interface ConditionTableRow extends OperationCondition {
    localKey: string;
    isEditing: boolean;
    isNew: boolean;
    saving: boolean;
    deleting: boolean;
    originalData?: OperationCondition;
}

const props = withDefaults(defineProps<Props>(), {
    selectedInstanceId: null,
    activationKey: 0
})

const { t } = useI18n();

const conditionsList = ref<ConditionTableRow[]>([]);
const currentInstanceId = ref<string>("");
const loading = ref(false);
let localKeySeed = 0;

function nextLocalKey(prefix = "condition") {
    localKeySeed += 1;
    return `${prefix}-${localKeySeed}`;
}

function createDefaultCondition(): ConditionTableRow {
    return {
        localKey: nextLocalKey("new-condition"),
        instanceID: currentInstanceId.value,
        name: t("hump.calcCondition.defaults.conditionName"),
        wagonVelocityOnTop: 1.4,
        wagonVelocityOnSlope: 5.2,
        wagonVelocityOnYard: 2.2,
        windVelocity: 5,
        isHeadWind: 1,
        airDensity: 0.125,
        temperature: -10,
        isEditing: true,
        isNew: true,
        saving: false,
        deleting: false
    };
}

function normalizeConditionRow(condition: any): ConditionTableRow {
    const id = condition.id ?? condition.ID;

    return {
        localKey: id || nextLocalKey(),
        instanceID: condition.instanceID ?? condition.InstanceID ?? currentInstanceId.value,
        id,
        name: condition.name ?? condition.Name ?? "",
        wagonVelocityOnTop: Number(condition.wagonVelocityOnTop ?? condition.WagonVelocityOnTop ?? 0),
        wagonVelocityOnSlope: Number(condition.wagonVelocityOnSlope ?? condition.WagonVelocityOnSlope ?? 0),
        wagonVelocityOnYard: Number(condition.wagonVelocityOnYard ?? condition.WagonVelocityOnYard ?? 0),
        windVelocity: Number(condition.windVelocity ?? condition.WindVelocity ?? 0),
        isHeadWind: Number(condition.isHeadWind ?? condition.IsHeadWind ?? 1),
        airDensity: Number(condition.airDensity ?? condition.AirDensity ?? 0),
        temperature: Number(condition.temperature ?? condition.Temperature ?? 0),
        isEditing: false,
        isNew: false,
        saving: false,
        deleting: false
    };
}

function cloneConditionData(row: OperationCondition): OperationCondition {
    return {
        instanceID: row.instanceID,
        id: row.id,
        name: row.name,
        wagonVelocityOnTop: row.wagonVelocityOnTop,
        wagonVelocityOnSlope: row.wagonVelocityOnSlope,
        wagonVelocityOnYard: row.wagonVelocityOnYard,
        windVelocity: row.windVelocity,
        isHeadWind: row.isHeadWind,
        airDensity: row.airDensity,
        temperature: row.temperature
    };
}

function buildConditionPayload(row: ConditionTableRow): OperationCondition {
    return {
        instanceID: currentInstanceId.value,
        id: row.id || "",
        name: row.name.trim(),
        wagonVelocityOnTop: Number(row.wagonVelocityOnTop),
        wagonVelocityOnSlope: Number(row.wagonVelocityOnSlope),
        wagonVelocityOnYard: Number(row.wagonVelocityOnYard),
        windVelocity: Number(row.windVelocity),
        isHeadWind: Number(row.isHeadWind),
        airDensity: Number(row.airDensity),
        temperature: Number(row.temperature)
    };
}

function formatNumber(value: number, precision: number) {
    return Number(value ?? 0).toFixed(precision);
}

function getWindDirectionText(isHeadWind: number) {
    return Number(isHeadWind) === 1
        ? t("hump.calcCondition.options.headwind")
        : t("hump.calcCondition.options.tailwind");
}

async function fetchConditions() {
    if (!currentInstanceId.value) {
        conditionsList.value = [];
        return;
    }

    try {
        loading.value = true;
        const response = await axios.get("/Hump/GetOperationConditions", {
            params: { instanceID: currentInstanceId.value }
        });
        conditionsList.value = (response.data || []).map(normalizeConditionRow);
    } catch (error) {
        console.error("Failed to fetch conditions:", error);
        conditionsList.value = [];
        ElMessage.error(t("hump.calcCondition.errors.loadConditions"));
    } finally {
        loading.value = false;
    }
}

function addCondition() {
    if (!currentInstanceId.value) {
        ElMessage.warning(t("hump.calcCondition.errors.noInstance"));
        return;
    }

    conditionsList.value.unshift(createDefaultCondition());
}

function editRow(index: number) {
    const row = conditionsList.value[index];
    if (!row) return;

    row.originalData = cloneConditionData(row);
    row.isEditing = true;
}

function cancelEdit(index: number) {
    const row = conditionsList.value[index];
    if (!row) return;

    if (row.isNew) {
        conditionsList.value.splice(index, 1);
        return;
    }

    if (row.originalData) {
        Object.assign(row, row.originalData);
    }

    row.isEditing = false;
    row.originalData = undefined;
}

async function saveConditionRow(index: number) {
    const row = conditionsList.value[index];
    if (!row) return;

    if (!currentInstanceId.value) {
        ElMessage.warning(t("hump.calcCondition.errors.noInstance"));
        return;
    }

    const payload = buildConditionPayload(row);
    if (!payload.name) {
        ElMessage.warning(t("hump.calcCondition.placeholders.conditionName"));
        return;
    }

    if (!row.isNew && !payload.id) {
        ElMessage.error(t("hump.calcCondition.errors.save"));
        return;
    }

    try {
        row.saving = true;

        if (row.isNew) {
            await axios.post("/Hump/CreateOperationCondition", payload);
            ElMessage.success(t("hump.calcCondition.messages.created"));
        } else {
            await axios.put("/Hump/EditOperationCondition", payload);
            ElMessage.success(t("hump.calcCondition.messages.updated"));
        }

        await fetchConditions();
    } catch (error) {
        console.error("Failed to save condition:", error);
        ElMessage.error(t("hump.calcCondition.errors.save"));
    } finally {
        row.saving = false;
    }
}

async function deleteConditionRow(index: number) {
    const row = conditionsList.value[index];
    if (!row) return;

    if (row.isNew) {
        conditionsList.value.splice(index, 1);
        return;
    }

    if (!row.id) {
        ElMessage.warning(t("hump.calcCondition.errors.noCondition"));
        return;
    }

    try {
        await ElMessageBox.confirm(
            t("hump.calcCondition.confirmDelete"),
            t("hump.calcCondition.warning"),
            {
                confirmButtonText: t("common.confirm"),
                cancelButtonText: t("common.cancel"),
                type: "warning"
            }
        );

        row.deleting = true;
        await axios.delete("/Hump/DeleteOperationCondition", {
            params: { id: row.id }
        });

        conditionsList.value.splice(index, 1);
        ElMessage.success(t("hump.calcCondition.messages.deleted"));
    } catch (error: any) {
        if (error !== "cancel" && error !== "close") {
            console.error("Failed to delete condition:", error);
            ElMessage.error(t("hump.calcCondition.errors.delete"));
        }
    } finally {
        row.deleting = false;
    }
}

watch(() => props.selectedInstanceId, async (newInstanceId) => {
    currentInstanceId.value = newInstanceId || "";
    await fetchConditions();
}, { immediate: true });

watch(() => props.activationKey, () => {
    void fetchConditions();
});
</script>

<style scoped lang="css">
.calculation-condition-container {
    width: 100%;
}

.condition-toolbar {
    display: flex;
    justify-content: flex-start;
    margin-bottom: 12px;
}

.condition-table {
    width: min(1046px, 100%);
}

.table-number-input,
.table-select {
    width: 100%;
}

.condition-header {
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    gap: 2px;
    width: 100%;
    line-height: 1.18;
    white-space: normal;
    word-break: keep-all;
}

.condition-header__unit {
    color: #6b7280;
    font-size: 12px;
    font-weight: 500;
}

.row-actions {
    display: flex;
    gap: 6px;
    align-items: center;
    justify-content: center;
    flex-wrap: nowrap;
}

:deep(.condition-table .el-table__header .cell) {
    white-space: normal;
    line-height: 1.18;
    padding: 0 6px;
}

:deep(.condition-table .el-table__body .cell) {
    white-space: nowrap;
    padding: 0 6px;
}

:deep(.condition-table .el-input-number .el-input__inner) {
    padding-left: 6px;
    padding-right: 30px;
}
</style>
