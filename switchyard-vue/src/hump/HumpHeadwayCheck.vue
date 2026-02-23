<template>
    <div class="headway-check-container">
        <!-- 工具栏 -->
        <div class="headway-toolbar">
            <div class="headway-toolbar__group">
                <label>{{ t('hump.headway.labels.verification') }}</label>
                <el-select v-model="selectedHeadwayCheckSchemeID"
                    :placeholder="t('hump.headway.placeholders.selectVerification')" size="small" clearable>
                    <el-option v-for="scheme in headwayCheckSchemes" :key="scheme.value" :label="scheme.label"
                        :value="scheme.value"></el-option>
                </el-select>
                <el-button type="primary" size="small" @click="openHeadwaySchemeManager">...</el-button>
            </div>
            <div class="headway-toolbar__group">
                <label>{{ t('hump.headway.labels.designScheme') }}</label>
                <el-select v-model="selectedHumpSchemeID" :placeholder="t('hump.headway.placeholders.selectDesign')"
                    size="small" clearable>
                    <el-option v-for="scheme in humpSchemes" :key="scheme.value" :label="scheme.label"
                        :value="scheme.value"></el-option>
                </el-select>
            </div>
            <div class="headway-toolbar__group">
                <label>{{ t('hump.headway.labels.slopeLine') }}</label>
                <el-select v-model="selectedSlopeLineID" :placeholder="t('hump.headway.placeholders.selectDesign')"
                    size="small" clearable>
                    <el-option v-for="slopeLine in slopeLines" :key="slopeLine.value" :label="slopeLine.label"
                        :value="slopeLine.value"></el-option>
                </el-select>
            </div>
            <div class="headway-toolbar__group">
                <label>{{ t('hump.headway.labels.wagonOrder') }}</label>
                <el-select v-model="selectedHeadwayCheckWagonTokens"
                    :placeholder="t('hump.headway.placeholders.selectDesign')" size="small" multiple filterable
                    collapse-tags-tooltip class="headway-order-select">
                    <el-option v-for="hc in headwayOrderOptions" :key="hc.value" :label="hc.label"
                        :value="hc.value"></el-option>
                </el-select>
            </div>
            <div class="headway-toolbar__group">
                <label>{{ t('hump.headway.labels.pushVelocity') }}</label>
                <el-input-number v-model="selectedWagonVelocityOnTop" size="small" :min="0" :step="0.1" :precision="3"
                    controls-position="right" />
            </div>
            <div class="headway-toolbar__group">
                <el-button type="primary" size="small" :loading="headwayCheckExecuting"
                    :disabled="headwayCheckExecuting" @click="handleExecuteHeadwayCheck">{{
                        t('hump.headway.toolbar.execute') }}</el-button>
            </div>
        </div>

        <!-- 图表容器 -->
        <div class="charts-container" :class="{
            'velocity-fullscreen': fullscreenChart === 'velocity',
            'time-fullscreen': fullscreenChart === 'time'
        }">
            <!-- 速度-距离曲线 -->
            <HumpVelocityCurve ref="velocityCurveRef" :velocity-curve-data="velocityCurveData"
                :velocity-tabs="velocityTabs" :chart-width="chartWidth" :chart-height="chartHeight"
                :margin-left="marginLeft" :margin-right="marginRight" :margin-top="marginTop"
                :margin-bottom="marginBottom" :fullscreen-chart="fullscreenChart" @remove-tab="handleRemoveVelocityTab"
                @toggle-fullscreen="toggleFullscreen('velocity')" />

            <!-- 时间-距离曲线 -->
            <HumpTimeCurve ref="timeCurveRef" :time-curve-data="timeCurveData" :time-tabs="timeTabs"
                :chart-width="chartWidth" :chart-height="chartHeight" :margin-left="marginLeft"
                :margin-right="marginRight" :margin-top="marginTop" :margin-bottom="marginBottom"
                :selected-instance-id="props.selectedInstanceId" :selected-slope-line-id="selectedSlopeLineID"
                :fullscreen-chart="fullscreenChart" @remove-tab="handleRemoveTimeTab"
                @toggle-fullscreen="toggleFullscreen('time')" />
        </div>

        <el-dialog v-model="showHeadwaySchemeManager" :title="t('hump.headway.manager.title')" width="90%"
            :close-on-click-modal="false">
            <div class="manager-toolbar">
                <el-button type="primary" @click="handleAddHeadwayScheme">{{ t('hump.headway.buttons.add')
                    }}</el-button>
            </div>
            <el-table :data="headwaySchemeManagerRows" style="width: 100%" v-loading="headwaySchemeManagerLoading">
                <el-table-column prop="id" :label="t('hump.headway.labels.id')" width="190" />
                <el-table-column prop="name" :label="t('hump.headway.labels.name')" min-width="160">
                    <template #default="{ row }">
                        <el-input v-if="editingHeadwaySchemeID === row.id" v-model="row.name" size="small" />
                        <span v-else>{{ row.name }}</span>
                    </template>
                </el-table-column>
                <el-table-column prop="humpSchemeID" :label="t('hump.headway.labels.designScheme')" width="180">
                    <template #default="{ row }">
                        <el-select v-if="editingHeadwaySchemeID === row.id" v-model="row.humpSchemeID" size="small"
                            @change="handleManagerHumpSchemeChange(row)">
                            <el-option v-for="scheme in humpSchemes" :key="scheme.value" :label="scheme.label"
                                :value="scheme.value" />
                        </el-select>
                        <span v-else>{{ getOptionLabel(humpSchemes, row.humpSchemeID) }}</span>
                    </template>
                </el-table-column>
                <el-table-column prop="slopeLineID" :label="t('hump.headway.labels.slopeLine')" width="180">
                    <template #default="{ row }">
                        <el-select v-if="editingHeadwaySchemeID === row.id" v-model="row.slopeLineID" size="small">
                            <el-option v-for="slopeLine in slopeLines" :key="slopeLine.value" :label="slopeLine.label"
                                :value="slopeLine.value" />
                        </el-select>
                        <span v-else>{{ getOptionLabel(slopeLines, row.slopeLineID) }}</span>
                    </template>
                </el-table-column>
                <el-table-column prop="wagonVelocityOnTop" :label="t('hump.headway.labels.pushVelocityUnit')"
                    width="170">
                    <template #default="{ row }">
                        <el-input-number v-if="editingHeadwaySchemeID === row.id" v-model="row.wagonVelocityOnTop"
                            size="small" :min="0" :step="0.1" :precision="3" controls-position="right" />
                        <span v-else>{{ row.wagonVelocityOnTop }}</span>
                    </template>
                </el-table-column>
                <el-table-column prop="wagonIDs" :label="t('hump.headway.labels.wagonSequence')" min-width="260">
                    <template #default="{ row }">
                        <el-select v-if="editingHeadwaySchemeID === row.id" v-model="row.wagonTokens" multiple
                            filterable @change="handleManagerWagonTokensChange(row)" collapse-tags collapse-tags-tooltip
                            size="small" style="width: 100%">
                            <el-option v-for="hc in getManagerHeadwayOrderOptions(row)" :key="hc.value"
                                :label="hc.label" :value="hc.value" />
                        </el-select>
                        <span v-else>{{ formatWagonSummary(row) }}</span>
                    </template>
                </el-table-column>
                <el-table-column :label="t('hump.headway.labels.operation')" width="200" fixed="right">
                    <template #default="{ row }">
                        <div v-if="editingHeadwaySchemeID === row.id">
                            <el-button type="success" size="small" @click="handleSaveHeadwayScheme(row)">{{
                                t('hump.headway.buttons.save') }}</el-button>
                            <el-button size="small" @click="handleCancelHeadwaySchemeEdit">{{
                                t('hump.headway.buttons.cancel') }}</el-button>
                        </div>
                        <div v-else>
                            <el-button type="primary" size="small" @click="handleEditHeadwayScheme(row)">{{
                                t('hump.headway.buttons.edit') }}</el-button>
                            <el-button type="danger" size="small" @click="handleDeleteHeadwayScheme(row)">{{
                                t('hump.headway.buttons.delete') }}</el-button>
                        </div>
                    </template>
                </el-table-column>
            </el-table>
            <template #footer>
                <el-button @click="showHeadwaySchemeManager = false">{{ t('hump.headway.buttons.close') }}</el-button>
            </template>
        </el-dialog>
    </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted, onBeforeUnmount, nextTick } from 'vue'
import axios from '@/utils/axios'
import { ElMessage, ElMessageBox } from 'element-plus'
import HumpVelocityCurve from './HumpVelocityCurve.vue'
import HumpTimeCurve from './HumpTimeCurve.vue'
import { useI18n } from 'vue-i18n'

const { t } = useI18n()

type OptionItem = { value: string; label: string }

interface HumpCalculation {
    id: string
    wagonType: string
    operationConditionID?: string
    operationCondition?: {
        name?: string
        Name?: string
    }
    OperationCondition?: {
        name?: string
        Name?: string
    }
    slopeLineID: string
}

interface HeadwayCheckWagon {
    sequence: number
    humpCalculationID: string
    name?: string
}

interface HeadwayCheckScheme {
    id: string
    instanceID: string
    humpSchemeID: string
    name: string
    wagonList?: HeadwayCheckWagon[]
    wagonVelocityOnTop: number
    slopeLineID: string
}

interface HeadwayCheckSchemeManagerRow {
    id: string
    instanceID: string
    humpSchemeID: string
    name: string
    wagonIDs: string[]
    wagonTokens: string[]
    wagonVelocityOnTop: number
    slopeLineID: string
}

interface SpeedProfileWagon {
    sequence?: number
    humpCalculationID?: string
}

interface SpeedProfileResponseItem {
    wagon?: SpeedProfileWagon
    positionList?: Array<number | string>
    speedList?: Array<number | string>
}

interface RunningTimeResponseItem {
    wagon?: SpeedProfileWagon
    positionList?: Array<number | string>
    runningTimeList?: Array<number | string>
}

interface OperationCondition {
    id: string
    name: string
}

const props = withDefaults(defineProps<{
    selectedInstanceId?: string | null
}>(), {
    selectedInstanceId: null
})

// 全屏状态管理
const fullscreenChart = ref<'velocity' | 'time' | null>(null)

// 切换全屏状态
const toggleFullscreen = (chartType: 'velocity' | 'time') => {
    if (fullscreenChart.value === chartType) {
        fullscreenChart.value = null
    } else {
        fullscreenChart.value = chartType
    }
    // 更新容器宽度
    setTimeout(() => {
        updateContainerWidth()
    }, 100)
}

const selectedHeadwayCheckWagons = ref<string[]>([])
const selectedHeadwayCheckWagonTokens = ref<string[]>([])
const selectedHeadwayCheckSchemeID = ref('')
const selectedHumpSchemeID = ref('')
const selectedSlopeLineID = ref('')
const selectedWagonVelocityOnTop = ref<number>(1.4)

// API数据
const headwayCheckSchemes = ref<OptionItem[]>([])
const humpSchemes = ref<OptionItem[]>([])
const slopeLines = ref<OptionItem[]>([])
const operationConditions = ref<OperationCondition[]>([])
const humpCalculationsRaw = ref<HumpCalculation[]>([])
const humpCalculationsByScheme = ref<Record<string, HumpCalculation[]>>({})

const showHeadwaySchemeManager = ref(false)
const headwaySchemeManagerLoading = ref(false)
const headwaySchemeManagerRows = ref<HeadwayCheckSchemeManagerRow[]>([])
const editingHeadwaySchemeID = ref('')
const headwayCheckExecuting = ref(false)

const toHeadwayWagonIDs = (wagonList?: HeadwayCheckWagon[]) => {
    if (!Array.isArray(wagonList)) return []
    return [...wagonList]
        .sort((a, b) => a.sequence - b.sequence)
        .map(w => w.humpCalculationID)
        .filter(Boolean)
}

const toHeadwayWagonList = (wagonIDs: string[]): HeadwayCheckWagon[] => {
    return wagonIDs.map((id, index) => ({
        sequence: index + 1,
        humpCalculationID: id
    }))
}

const getHeadwayToken = (wagonID: string, sequence: number) => `${wagonID}::${sequence}`

const parseHeadwayToken = (token: string) => {
    const [wagonID, sequenceText] = token.split('::')
    const sequence = Number(sequenceText)
    return {
        wagonID: wagonID || '',
        sequence: Number.isFinite(sequence) && sequence > 0 ? sequence : 1
    }
}

const toHeadwayTokens = (wagonIDs: string[]) => {
    const perWagonCount = new Map<string, number>()
    return wagonIDs.map((wagonID) => {
        const current = (perWagonCount.get(wagonID) || 0) + 1
        perWagonCount.set(wagonID, current)
        return getHeadwayToken(wagonID, current)
    })
}

const toHeadwayWagonIDsFromTokens = (tokens: string[]) => {
    return tokens
        .map(token => parseHeadwayToken(token).wagonID)
        .filter((id): id is string => !!id)
}

const getDefaultHumpSchemeID = () => selectedHumpSchemeID.value || humpSchemes.value[0]?.value || ''
const getDefaultSlopeLineID = () => selectedSlopeLineID.value || slopeLines.value[0]?.value || ''

const getOptionLabel = (options: OptionItem[], value: string) => {
    return options.find(o => o.value === value)?.label || value || '--'
}

const getOperationConditionName = (row: HumpCalculation) => {
    const nested = row.operationCondition?.name
        || row.operationCondition?.Name
        || row.OperationCondition?.name
        || row.OperationCondition?.Name
    if (nested) return nested
    const conditionID = row.operationConditionID || ''
    return operationConditions.value.find(c => c.id === conditionID)?.name || conditionID
}

const getHumpCalculationName = (row: HumpCalculation) => {
    const operationConditionName = getOperationConditionName(row)
    return operationConditionName ? `${row.wagonType}-${operationConditionName}` : row.wagonType
}

const getManagerHumpCalculationOptions = (humpSchemeID: string): OptionItem[] => {
    const rows = humpCalculationsByScheme.value[humpSchemeID] || []
    return rows.map(r => ({ value: r.id, label: getHumpCalculationName(r) }))
}

const buildHeadwayOrderOptions = (tokens: string[], availableOptions: OptionItem[]) => {
    const optionMap = new Map<string, string>()

    tokens.forEach((token) => {
        const { wagonID } = parseHeadwayToken(token)
        const label = availableOptions.find(c => c.value === wagonID)?.label || wagonID
        optionMap.set(token, label)
    })

    const maxSequenceByWagon = new Map<string, number>()
    tokens.forEach((token) => {
        const { wagonID, sequence } = parseHeadwayToken(token)
        maxSequenceByWagon.set(wagonID, Math.max(maxSequenceByWagon.get(wagonID) || 0, sequence))
    })

    availableOptions.forEach((wagon) => {
        const nextSequence = (maxSequenceByWagon.get(wagon.value) || 0) + 1
        const nextToken = getHeadwayToken(wagon.value, nextSequence)
        if (!optionMap.has(nextToken)) {
            optionMap.set(nextToken, wagon.label)
        }
    })

    return Array.from(optionMap.entries()).map(([value, label]) => ({ value, label }))
}

const getManagerHeadwayOrderOptions = (row: HeadwayCheckSchemeManagerRow) => {
    return buildHeadwayOrderOptions(row.wagonTokens || [], getManagerHumpCalculationOptions(row.humpSchemeID))
}

const formatWagonSummary = (row: HeadwayCheckSchemeManagerRow) => {
    const options = getManagerHumpCalculationOptions(row.humpSchemeID)
    if (!row.wagonIDs.length) return '--'
    return row.wagonIDs.map(id => options.find(o => o.value === id)?.label || id).join(' -> ')
}

const buildHeadwaySchemePayload = (row: HeadwayCheckSchemeManagerRow) => {
    return {
        id: row.id,
        instanceID: row.instanceID,
        humpSchemeID: row.humpSchemeID,
        name: row.name,
        wagonVelocityOnTop: row.wagonVelocityOnTop,
        slopeLineID: row.slopeLineID,
        wagonList: toHeadwayWagonList(row.wagonIDs)
    }
}

// 根据selectedSlopeLineID过滤钩车溜放顺序
const humpCalculations = computed(() => {
    const filtered = selectedSlopeLineID.value
        ? humpCalculationsRaw.value.filter(c => c.slopeLineID === selectedSlopeLineID.value)
        : humpCalculationsRaw.value
    return filtered.map(c => ({ value: c.id, label: getHumpCalculationName(c) }))
})

const headwayOrderOptions = computed<OptionItem[]>(() => {
    return buildHeadwayOrderOptions(selectedHeadwayCheckWagonTokens.value, humpCalculations.value)
})

const loadHumpCalculationsForScheme = async (humpSchemeID: string, force = false): Promise<HumpCalculation[]> => {
    if (!props.selectedInstanceId || !humpSchemeID) {
        return [] as HumpCalculation[]
    }

    if (!force && humpCalculationsByScheme.value[humpSchemeID]) {
        return humpCalculationsByScheme.value[humpSchemeID]
    }

    const response = await axios.get('/Hump/GetHumpCalculations', {
        params: {
            instanceID: props.selectedInstanceId,
            humpSchemeID
        }
    })
    const rows = (response.data || []).map((item: any) => ({
        id: item.id,
        wagonType: item.wagonType,
        operationConditionID: item.operationConditionID || item.OperationConditionID,
        operationCondition: item.operationCondition || item.OperationCondition,
        OperationCondition: item.OperationCondition || item.operationCondition,
        slopeLineID: item.slopeLineID
    }))
    humpCalculationsByScheme.value = {
        ...humpCalculationsByScheme.value,
        [humpSchemeID]: rows
    }
    return rows
}

// 加载基础数据
const loadBaseData = async () => {
    if (!props.selectedInstanceId) {
        headwayCheckSchemes.value = []
        humpSchemes.value = []
        slopeLines.value = []
        operationConditions.value = []
        humpCalculationsByScheme.value = {}
        return
    }

    try {
        const [hcsRes, hsRes, slRes, ocRes] = await Promise.all([
            axios.get('/Hump/GetHeadwayCheckSchemes', { params: { instanceID: props.selectedInstanceId } }),
            axios.get('/Hump/GetHumpSchemes', { params: { instanceID: props.selectedInstanceId } }),
            axios.get('/Hump/GetSlopeLines', { params: { instanceID: props.selectedInstanceId } }),
            axios.get('/Hump/GetOperationConditions', { params: { instanceID: props.selectedInstanceId } }),
        ])

        headwayCheckSchemes.value = (hcsRes.data || []).map((s: any) => ({ value: s.id, label: s.name }))
        humpSchemes.value = (hsRes.data || []).map((s: any) => ({ value: s.id, label: s.name }))
        slopeLines.value = (slRes.data || []).map((s: any) => ({ value: s.id, label: s.name }))
        operationConditions.value = (ocRes.data || []).map((s: any) => ({ id: s.id, name: s.name }))

        if (selectedHeadwayCheckSchemeID.value && !headwayCheckSchemes.value.some(s => s.value === selectedHeadwayCheckSchemeID.value)) {
            selectedHeadwayCheckSchemeID.value = ''
        }
        if (selectedHumpSchemeID.value && !humpSchemes.value.some(s => s.value === selectedHumpSchemeID.value)) {
            selectedHumpSchemeID.value = ''
        }
        if (selectedSlopeLineID.value && !slopeLines.value.some(s => s.value === selectedSlopeLineID.value)) {
            selectedSlopeLineID.value = ''
        }
    } catch (error) {
        console.error('加载基础数据失败:', error)
        ElMessage.error(t('hump.headway.messages.loadSchemesFailed'))
    }
}

const loadHeadwayCheckSchemeByID = async (id: string) => {
    if (!props.selectedInstanceId || !id) return null
    const response = await axios.get('/Hump/GetHeadwayCheckSchemeById', {
        params: {
            instanceID: props.selectedInstanceId,
            id
        }
    })
    return response.data as HeadwayCheckScheme
}

const applySelectedHeadwayScheme = async () => {
    velocityCurveData.value = []
    velocityTabs.value = []

    if (!selectedHeadwayCheckSchemeID.value) {
        selectedHeadwayCheckWagons.value = []
        selectedHeadwayCheckWagonTokens.value = []
        selectedWagonVelocityOnTop.value = 1.4
        return
    }

    try {
        const scheme = await loadHeadwayCheckSchemeByID(selectedHeadwayCheckSchemeID.value)
        if (!scheme) return

        selectedHumpSchemeID.value = scheme.humpSchemeID || ''
        selectedSlopeLineID.value = scheme.slopeLineID || ''
        selectedWagonVelocityOnTop.value = Number(scheme.wagonVelocityOnTop ?? 1.4)

        await loadHumpCalculations()
        const availableWagons = new Set(humpCalculationsRaw.value.map(c => c.id))
        selectedHeadwayCheckWagons.value = toHeadwayWagonIDs(scheme.wagonList).filter(id => availableWagons.has(id))
        selectedHeadwayCheckWagonTokens.value = toHeadwayTokens(selectedHeadwayCheckWagons.value)
    } catch (error) {
        console.error('加载检算实例失败:', error)
        ElMessage.error(t('hump.headway.messages.loadSchemeFailed'))
    }
}

// 加载钩车计算数据
const loadHumpCalculations = async () => {
    if (!props.selectedInstanceId || !selectedHumpSchemeID.value) {
        humpCalculationsRaw.value = []
        selectedHeadwayCheckWagons.value = []
        selectedHeadwayCheckWagonTokens.value = []
        return
    }

    try {
        const rows = await loadHumpCalculationsForScheme(selectedHumpSchemeID.value)
        humpCalculationsRaw.value = rows

        const availableWagons = new Set(rows.map(c => c.id))
        selectedHeadwayCheckWagons.value = selectedHeadwayCheckWagons.value.filter(id => availableWagons.has(id))
        selectedHeadwayCheckWagonTokens.value = toHeadwayTokens(selectedHeadwayCheckWagons.value)
    } catch (error) {
        console.error('加载钩车计算数据失败:', error)
        humpCalculationsRaw.value = []
        selectedHeadwayCheckWagons.value = []
        selectedHeadwayCheckWagonTokens.value = []
    }
}

const toManagerRow = (scheme: HeadwayCheckScheme): HeadwayCheckSchemeManagerRow => ({
    id: scheme.id,
    instanceID: scheme.instanceID,
    humpSchemeID: scheme.humpSchemeID || '',
    name: scheme.name || '',
    wagonVelocityOnTop: Number(scheme.wagonVelocityOnTop ?? 1.4),
    slopeLineID: scheme.slopeLineID || '',
    wagonIDs: toHeadwayWagonIDs(scheme.wagonList),
    wagonTokens: toHeadwayTokens(toHeadwayWagonIDs(scheme.wagonList))
})

const loadHeadwaySchemeManagerData = async () => {
    if (!props.selectedInstanceId) {
        headwaySchemeManagerRows.value = []
        return
    }

    try {
        headwaySchemeManagerLoading.value = true
        const listResponse = await axios.get('/Hump/GetHeadwayCheckSchemes', {
            params: { instanceID: props.selectedInstanceId }
        })
        const list = listResponse.data || []

        const detailRows = await Promise.all(
            list.map(async (item: any) => {
                const detail = await loadHeadwayCheckSchemeByID(item.id)
                return detail || item
            })
        )

        const humpSchemeIDs = [...new Set(detailRows.map((x: any) => x.humpSchemeID).filter(Boolean))]
        await Promise.all(humpSchemeIDs.map(id => loadHumpCalculationsForScheme(id)))

        headwaySchemeManagerRows.value = detailRows.map((row: HeadwayCheckScheme) => toManagerRow(row))
        editingHeadwaySchemeID.value = ''
    } catch (error) {
        console.error('加载检算实例管理列表失败:', error)
        ElMessage.error(t('hump.headway.messages.loadManagerFailed'))
    } finally {
        headwaySchemeManagerLoading.value = false
    }
}

const openHeadwaySchemeManager = async () => {
    if (!props.selectedInstanceId) {
        ElMessage.warning(t('hump.headway.messages.selectInstance'))
        return
    }

    showHeadwaySchemeManager.value = true
    await loadHeadwaySchemeManagerData()
}

const handleAddHeadwayScheme = async () => {
    if (!props.selectedInstanceId) {
        ElMessage.warning(t('hump.headway.messages.selectInstance'))
        return
    }

    let humpSchemeID = getDefaultHumpSchemeID()
    let slopeLineID = getDefaultSlopeLineID()

    if (!humpSchemeID || !slopeLineID) {
        await loadBaseData()
        humpSchemeID = humpSchemeID || getDefaultHumpSchemeID()
        slopeLineID = slopeLineID || getDefaultSlopeLineID()
    }

    if (!selectedHumpSchemeID.value && humpSchemeID) {
        selectedHumpSchemeID.value = humpSchemeID
    }
    if (!selectedSlopeLineID.value && slopeLineID) {
        selectedSlopeLineID.value = slopeLineID
    }

    const newScheme: HeadwayCheckSchemeManagerRow = {
        id: '',
        instanceID: props.selectedInstanceId,
        humpSchemeID,
        name: t('hump.headway.manager.defaultName', { number: headwaySchemeManagerRows.value.length + 1 }),
        wagonVelocityOnTop: 1.4,
        slopeLineID,
        wagonIDs: [],
        wagonTokens: []
    }

    try {
        headwaySchemeManagerLoading.value = true
        await loadHumpCalculationsForScheme(newScheme.humpSchemeID)
        const response = await axios.post('/Hump/CreateHeadwayCheckScheme', buildHeadwaySchemePayload(newScheme))
        await Promise.all([loadBaseData(), loadHeadwaySchemeManagerData()])

        const createdID = response.data?.id || response.data?.ID
        if (createdID) {
            selectedHeadwayCheckSchemeID.value = createdID
        }

        ElMessage.success(t('hump.headway.messages.createSuccess'))
    } catch (error) {
        console.error('新建检算实例失败:', error)
        ElMessage.error(t('hump.headway.messages.createFailed'))
    } finally {
        headwaySchemeManagerLoading.value = false
    }
}

const handleEditHeadwayScheme = async (row: HeadwayCheckSchemeManagerRow) => {
    editingHeadwaySchemeID.value = row.id
    row.wagonTokens = toHeadwayTokens(row.wagonIDs)
    if (row.humpSchemeID) {
        try {
            await loadHumpCalculationsForScheme(row.humpSchemeID)
        } catch (error) {
            console.error('加载钩车条件失败:', error)
        }
    }
}

const handleManagerHumpSchemeChange = async (row: HeadwayCheckSchemeManagerRow) => {
    if (!row.humpSchemeID) {
        row.wagonIDs = []
        row.wagonTokens = []
        return
    }

    try {
        const rows = await loadHumpCalculationsForScheme(row.humpSchemeID)
        const availableWagons = new Set(rows.map(c => c.id))
        row.wagonIDs = row.wagonIDs.filter(id => availableWagons.has(id))
        row.wagonTokens = toHeadwayTokens(row.wagonIDs)
    } catch (error) {
        console.error('加载钩车条件失败:', error)
        row.wagonIDs = []
        row.wagonTokens = []
    }
}

const handleManagerWagonTokensChange = (row: HeadwayCheckSchemeManagerRow) => {
    row.wagonIDs = toHeadwayWagonIDsFromTokens(row.wagonTokens || [])
}

const handleSaveHeadwayScheme = async (row: HeadwayCheckSchemeManagerRow) => {
    row.wagonIDs = toHeadwayWagonIDsFromTokens(row.wagonTokens || [])
    if (!row.name.trim()) {
        ElMessage.warning(t('hump.headway.messages.nameRequired'))
        return
    }
    if (!row.humpSchemeID) {
        ElMessage.warning(t('hump.headway.messages.humpSchemeRequired'))
        return
    }
    if (!row.slopeLineID) {
        ElMessage.warning(t('hump.headway.messages.slopeLineRequired'))
        return
    }

    try {
        headwaySchemeManagerLoading.value = true
        await axios.put('/Hump/EditHeadwayCheckScheme', buildHeadwaySchemePayload(row))
        await Promise.all([loadBaseData(), loadHeadwaySchemeManagerData()])
        if (selectedHeadwayCheckSchemeID.value === row.id) {
            await applySelectedHeadwayScheme()
        }
        ElMessage.success(t('hump.headway.messages.updateSuccess'))
    } catch (error) {
        console.error('更新检算实例失败:', error)
        ElMessage.error(t('hump.headway.messages.updateFailed'))
    } finally {
        headwaySchemeManagerLoading.value = false
    }
}

const handleDeleteHeadwayScheme = async (row: HeadwayCheckSchemeManagerRow) => {
    try {
        await ElMessageBox.confirm(t('hump.headway.messages.deleteConfirm', { name: row.name }), t('common.titles.tip'), {
            confirmButtonText: t('common.buttons.confirm'),
            cancelButtonText: t('common.buttons.cancel'),
            type: 'warning'
        })

        headwaySchemeManagerLoading.value = true
        await axios.delete('/Hump/DeleteHeadwayCheckScheme', {
            params: { id: row.id }
        })

        if (selectedHeadwayCheckSchemeID.value === row.id) {
            selectedHeadwayCheckSchemeID.value = ''
        }

        await Promise.all([loadBaseData(), loadHeadwaySchemeManagerData()])
        ElMessage.success(t('hump.headway.messages.deleteSuccess'))
    } catch (error) {
        if (error !== 'cancel') {
            console.error('删除检算实例失败:', error)
            ElMessage.error(t('hump.headway.messages.deleteFailed'))
        }
    } finally {
        headwaySchemeManagerLoading.value = false
    }
}

const handleCancelHeadwaySchemeEdit = () => {
    editingHeadwaySchemeID.value = ''
    loadHeadwaySchemeManagerData()
}

const SPEED_PROFILE_SPACE_STEP_SIZE = 10
const CURVE_COLORS = ['#2563EB', '#16A34A', '#DC2626', '#D97706', '#7C3AED', '#0F766E', '#BE123C', '#4F46E5']

const toNumericList = (list: unknown): number[] => {
    if (!Array.isArray(list)) return []
    return list
        .map(item => Number(item))
        .filter(item => Number.isFinite(item))
}

const normalizeSpeedProfileWagon = (wagon: any): SpeedProfileWagon | undefined => {
    if (!wagon || typeof wagon !== 'object') return undefined

    const sequenceRaw = wagon.sequence ?? wagon.Sequence
    const sequence = Number.isFinite(Number(sequenceRaw)) ? Number(sequenceRaw) : undefined
    const humpCalculationIDRaw = wagon.humpCalculationID ?? wagon.HumpCalculationID
    const humpCalculationID = typeof humpCalculationIDRaw === 'string' ? humpCalculationIDRaw : undefined

    return {
        sequence,
        humpCalculationID
    }
}

const normalizeSpeedProfileItem = (item: any): SpeedProfileResponseItem => {
    const positionList = Array.isArray(item?.positionList) ? item.positionList : item?.PositionList
    const speedList = Array.isArray(item?.speedList) ? item.speedList : item?.SpeedList

    return {
        wagon: normalizeSpeedProfileWagon(item?.wagon ?? item?.Wagon),
        positionList: Array.isArray(positionList) ? positionList : [],
        speedList: Array.isArray(speedList) ? speedList : []
    }
}

const normalizeRunningTimeItem = (item: any): RunningTimeResponseItem => {
    const positionList = Array.isArray(item?.positionList) ? item.positionList : item?.PositionList
    const runningTimeList = Array.isArray(item?.runningTimeList) ? item.runningTimeList : item?.RunningTimeList

    return {
        wagon: normalizeSpeedProfileWagon(item?.wagon ?? item?.Wagon),
        positionList: Array.isArray(positionList) ? positionList : [],
        runningTimeList: Array.isArray(runningTimeList) ? runningTimeList : []
    }
}

const extractRunningTimeItems = (payload: any): RunningTimeResponseItem[] => {
    if (Array.isArray(payload)) {
        return payload.map(item => normalizeRunningTimeItem(item))
    }

    const runningTimes = Array.isArray(payload?.runningTimes) ? payload.runningTimes : payload?.RunningTimes
    if (!Array.isArray(runningTimes)) return []

    return runningTimes.map(item => normalizeRunningTimeItem(item))
}

const getSpeedProfileLabel = (wagon: SpeedProfileWagon | undefined, index: number): string => {
    const sequence = Number.isFinite(Number(wagon?.sequence)) ? Number(wagon?.sequence) : index + 1
    const humpCalculationID = wagon?.humpCalculationID || ''
    const humpCalculation = humpCalculationsRaw.value.find(hc => hc.id === humpCalculationID)
    if (humpCalculation) {
        return `${sequence}. ${getHumpCalculationName(humpCalculation)}`
    }
    if (humpCalculationID) {
        return `${sequence}. ${humpCalculationID}`
    }
    return `#${sequence}`
}

const renderSpeedProfiles = (rawProfiles: SpeedProfileResponseItem[]) => {
    const nextVelocityCurveData: VelocityCurveData[] = []
    const nextVelocityTabs: CurveTab[] = []

    rawProfiles.forEach((rawProfile, index) => {
        const positionList = toNumericList(rawProfile.positionList)
        const speedList = toNumericList(rawProfile.speedList)
        const pointCount = Math.min(positionList.length, speedList.length)

        if (pointCount <= 0) return

        const data: VelocityPoint[] = []
        for (let i = 0; i < pointCount; i++) {
            const x = positionList[i]
            const velocity = speedList[i]
            if (x === undefined || velocity === undefined) continue
            data.push({ x, velocity })
        }
        if (data.length <= 0) return

        const seriesName = `series-${index + 1}`

        nextVelocityCurveData.push({
            seriesName,
            color: CURVE_COLORS[index % CURVE_COLORS.length] || '#2563EB',
            data
        })
        nextVelocityTabs.push({
            name: seriesName,
            label: getSpeedProfileLabel(rawProfile.wagon, index)
        })
    })

    velocityCurveData.value = nextVelocityCurveData
    velocityTabs.value = nextVelocityTabs
}

const renderRunningTimes = (rawRunningTimes: RunningTimeResponseItem[]) => {
    const nextTimeCurveData: TimeCurveData[] = []
    const nextTimeTabs: CurveTab[] = []

    rawRunningTimes.forEach((rawRunningTime, index) => {
        const positionList = toNumericList(rawRunningTime.positionList)
        const runningTimeList = toNumericList(rawRunningTime.runningTimeList)
        const pointCount = Math.min(positionList.length, runningTimeList.length)

        if (pointCount <= 0) return

        const data: TimePoint[] = []
        for (let i = 0; i < pointCount; i++) {
            const x = positionList[i]
            const time = runningTimeList[i]
            if (x === undefined || time === undefined) continue
            data.push({ x, time })
        }

        if (data.length <= 0) return

        const seriesName = `series-${index + 1}`
        nextTimeCurveData.push({
            seriesName,
            color: CURVE_COLORS[index % CURVE_COLORS.length] || '#2563EB',
            data
        })
        nextTimeTabs.push({
            name: seriesName,
            label: getSpeedProfileLabel(rawRunningTime.wagon, index)
        })
    })

    timeCurveData.value = nextTimeCurveData
    timeTabs.value = nextTimeTabs
}

const calculateAndRenderSpeedProfiles = async () => {
    if (!props.selectedInstanceId || !selectedHeadwayCheckSchemeID.value) {
        velocityCurveData.value = []
        velocityTabs.value = []
        return
    }

    const response = await axios.get('/Hump/CalculateSpeedProfile', {
        params: {
            instanceID: props.selectedInstanceId,
            headwayCheckSchemeID: selectedHeadwayCheckSchemeID.value,
            spaceStepSize: SPEED_PROFILE_SPACE_STEP_SIZE
        }
    })

    const rawProfiles = Array.isArray(response.data) ? response.data : []
    const profiles = rawProfiles.map(item => normalizeSpeedProfileItem(item))
    renderSpeedProfiles(profiles)
}

const calculateAndRenderRunningTimes = async () => {
    if (!props.selectedInstanceId || !selectedHeadwayCheckSchemeID.value) {
        timeCurveData.value = []
        timeTabs.value = []
        return
    }

    const response = await axios.get('/Hump/CalculateRunningTime', {
        params: {
            instanceID: props.selectedInstanceId,
            headwayCheckSchemeID: selectedHeadwayCheckSchemeID.value
        }
    })

    const runningTimeItems = extractRunningTimeItems(response.data)
    renderRunningTimes(runningTimeItems)
}

const handleExecuteHeadwayCheck = async () => {
    if (!props.selectedInstanceId) {
        ElMessage.warning(t('hump.headway.messages.selectInstance'))
        return
    }
    if (!selectedHeadwayCheckSchemeID.value) {
        ElMessage.warning(t('hump.headway.messages.selectVerification'))
        return
    }
    if (!selectedHumpSchemeID.value) {
        ElMessage.warning(t('hump.headway.messages.selectHumpScheme'))
        return
    }
    if (!selectedSlopeLineID.value) {
        ElMessage.warning(t('hump.headway.messages.selectSlopeLine'))
        return
    }

    let configurationSaved = false
    try {
        headwayCheckExecuting.value = true

        const existingScheme = await loadHeadwayCheckSchemeByID(selectedHeadwayCheckSchemeID.value)
        if (!existingScheme) {
            ElMessage.error(t('hump.headway.messages.loadSchemeFailed'))
            return
        }

        const payload = {
            id: existingScheme.id,
            instanceID: props.selectedInstanceId,
            humpSchemeID: selectedHumpSchemeID.value,
            name: existingScheme.name || getOptionLabel(headwayCheckSchemes.value, selectedHeadwayCheckSchemeID.value),
            wagonVelocityOnTop: Number(selectedWagonVelocityOnTop.value ?? 1.4),
            slopeLineID: selectedSlopeLineID.value,
            wagonList: toHeadwayWagonList(selectedHeadwayCheckWagons.value)
        }

        await axios.put('/Hump/EditHeadwayCheckScheme', payload)
        configurationSaved = true
        await loadBaseData()
        await applySelectedHeadwayScheme()
        if (showHeadwaySchemeManager.value) {
            await loadHeadwaySchemeManagerData()
        }
        const [speedProfileResult, runningTimeResult] = await Promise.allSettled([
            calculateAndRenderSpeedProfiles(),
            calculateAndRenderRunningTimes()
        ])

        const speedProfileFailed = speedProfileResult.status === 'rejected'
        const runningTimeFailed = runningTimeResult.status === 'rejected'

        if (speedProfileFailed) {
            console.error('速度-距离曲线计算失败:', speedProfileResult.reason)
        }
        if (runningTimeFailed) {
            console.error('时间-距离曲线计算失败:', runningTimeResult.reason)
        }

        if (speedProfileFailed || runningTimeFailed) {
            if (speedProfileFailed && runningTimeFailed) {
                ElMessage.error('检算配置已保存，但速度-距离与时间-距离曲线计算失败')
            } else if (speedProfileFailed) {
                ElMessage.error('检算配置已保存，但速度-距离曲线计算失败')
            } else {
                ElMessage.error('检算配置已保存，但时间-距离曲线计算失败')
            }
            return
        }

        ElMessage.success(t('hump.headway.messages.saveConfigSuccess'))
    } catch (error) {
        if (configurationSaved) {
            console.error('检算配置保存成功，但刷新结果失败:', error)
            ElMessage.error('检算配置已保存，但结果刷新失败')
        } else {
            console.error('保存检算配置失败:', error)
            ElMessage.error(t('hump.headway.messages.saveConfigFailed'))
        }
    } finally {
        headwayCheckExecuting.value = false
    }
}

// 监听instanceId变化加载基础数据
watch(() => props.selectedInstanceId, (val) => {
    if (!val) {
        selectedHeadwayCheckSchemeID.value = ''
        selectedHumpSchemeID.value = ''
        selectedSlopeLineID.value = ''
        selectedWagonVelocityOnTop.value = 1.4
        selectedHeadwayCheckWagons.value = []
        selectedHeadwayCheckWagonTokens.value = []
        headwayCheckSchemes.value = []
        humpSchemes.value = []
        slopeLines.value = []
        operationConditions.value = []
        humpCalculationsRaw.value = []
        humpCalculationsByScheme.value = {}
        headwaySchemeManagerRows.value = []
        showHeadwaySchemeManager.value = false
        return
    }
    loadBaseData()
}, { immediate: true })

// 监听纵断面方案变化加载钩车计算
watch(selectedHumpSchemeID, () => {
    loadHumpCalculations()
})

watch(selectedHeadwayCheckSchemeID, () => {
    applySelectedHeadwayScheme()
})

watch(selectedHeadwayCheckWagonTokens, (tokens) => {
    selectedHeadwayCheckWagons.value = toHeadwayWagonIDsFromTokens(tokens)
})

// 速度-距离曲线tags数据
const velocityTabs = ref<CurveTab[]>([
    // 运行检算后按返回结果动态填充
])

// 时间-距离曲线tags数据
const timeTabs = ref<CurveTab[]>([
    // 暂不在此处填充，保留时间曲线扩展能力
])

// 删除速度-距离曲线的tag
const handleRemoveVelocityTab = (tagName: string) => {
    const index = velocityTabs.value.findIndex(tab => tab.name === tagName)
    if (index > -1) {
        velocityTabs.value.splice(index, 1)
        velocityCurveData.value = velocityCurveData.value.filter(curve => curve.seriesName !== tagName)
    }
}

// 删除时间-距离曲线的tag
const handleRemoveTimeTab = (tagName: string) => {
    const index = timeTabs.value.findIndex(tab => tab.name === tagName)
    if (index > -1) {
        timeTabs.value.splice(index, 1)
        timeCurveData.value = timeCurveData.value.filter(curve => curve.seriesName !== tagName)
    }
}

// 图表容器引用
const velocityCurveRef = ref<InstanceType<typeof HumpVelocityCurve>>()
const timeCurveRef = ref<InstanceType<typeof HumpTimeCurve>>()

// 通过子组件ref访问chartContainer
const velocityChartContainer = computed(() => velocityCurveRef.value?.chartContainer)
const timeChartContainer = computed(() => timeCurveRef.value?.chartContainer)

// 图表尺寸和边距
const chartHeight = ref(300)
const containerWidth = ref(800)
const marginLeft = ref(60)
const marginRight = ref(30)
const marginTop = ref(30)
const marginBottom = ref(50)

// 响应式的图表宽度
const chartWidth = computed(() => {
    return Math.max(400, containerWidth.value - 32)
})

// 曲线数据
interface VelocityPoint {
    x: number
    velocity: number
}

interface TimePoint {
    x: number
    time: number
}

interface VelocityCurveData {
    seriesName: string
    color: string
    data: VelocityPoint[]
}

interface TimeCurveData {
    seriesName: string
    color: string
    data: TimePoint[]
}

interface CurveTab {
    name: string
    label: string
}

const velocityCurveData = ref<VelocityCurveData[]>([])
const timeCurveData = ref<TimeCurveData[]>([])

// 更新容器宽度
const updateContainerWidth = () => {
    nextTick(() => {
        let targetContainer = null
        if (fullscreenChart.value === 'velocity' && velocityChartContainer.value) {
            targetContainer = velocityChartContainer.value
        } else if (fullscreenChart.value === 'time' && timeChartContainer.value) {
            targetContainer = timeChartContainer.value
        } else if (!fullscreenChart.value && velocityChartContainer.value) {
            targetContainer = velocityChartContainer.value
        }

        if (targetContainer) {
            containerWidth.value = targetContainer.clientWidth
        }
    })
}

// 窗口大小变化监听
const handleResize = () => {
    updateContainerWidth()
}

// 组件挂载时获取数据
onMounted(() => {
    window.addEventListener('resize', handleResize)
})

// 组件卸载时移除监听器
onBeforeUnmount(() => {
    window.removeEventListener('resize', handleResize)
})

</script>

<style lang="css" scoped>
.headway-check-container {
    display: flex;
    flex-direction: column;
    width: 100%;
    height: 100%;
    background-color: #ffffff;
}

/* 工具栏样式 */
.headway-toolbar {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: 8px;
    padding: 10px 12px;
    margin: 5px 5px 16px 5px;
    border: 1px solid #dbe3f1;
    border-radius: 2px;
    background: linear-gradient(135deg, #f8fafc, #eef3ff);
    box-shadow: 0 5px 15px rgba(15, 23, 42, 0.08);
    min-width: 0;
}

.headway-toolbar__group {
    display: flex;
    align-items: center;
    gap: 6px;
    padding: 2px 4px;
    border-radius: 5px;
    transition: box-shadow 0.2s ease, border-color 0.2s ease;
    min-width: 0;
}

.headway-toolbar__group label {
    font-size: 13px;
    font-weight: 600;
    color: #1f2a37;
    min-width: 72px;
    text-align: right;
    letter-spacing: 0.02em;
    line-height: 1.2;
    white-space: normal;
    word-break: keep-all;
    flex-shrink: 0;
}

.headway-toolbar__group :deep(.el-select) {
    min-width: 126px;
}

.headway-toolbar__group :deep(.headway-order-select) {
    min-width: 170px;
}

.headway-toolbar__group :deep(.el-button) {
    position: relative;
}

.chart-header .fullscreen-btn {
    right: 12px;
    margin: 0;
}

.manager-toolbar {
    margin-bottom: 16px;
}

@media (min-width: 1001px) {
    .headway-toolbar {
        flex-wrap: wrap;
        overflow: visible;
        gap: 6px;
        padding: 8px 10px;
    }

    .headway-toolbar__group {
        flex: 0 1 auto;
        gap: 4px;
        padding: 2px 3px;
    }

    .headway-toolbar__group label {
        min-width: 72px;
    }

    .headway-toolbar__group :deep(.el-select) {
        min-width: 120px;
    }

    .headway-toolbar__group :deep(.headway-order-select) {
        min-width: 170px;
    }
}

/* 图表容器样式 */
.charts-container {
    display: flex;
    flex: 1;
    gap: 16px;
    padding: 16px 20px;
    overflow: auto;
    min-width: 800px;
    transition: all 0.3s ease-in-out;
}
</style>
