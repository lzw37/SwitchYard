<template>
    <section class="hump-layout">
        <div class="plan-top">
            <el-select v-model="selectedLine" size="small" :placeholder="t('hump.selectLine')" clearable
                style="width:240px;align-items: center;vertical-align: middle;">
                <el-option v-for="line in lines" :key="line.id" :label="line.name" :value="line.id" />
            </el-select>
            <el-button type="primary" size="small" @click="openSlopeLineManager">...</el-button>
            <div>
                <el-button-group>
                    <el-button type="plain" size="small" @click="loadFlatLayout">{{ t('hump.load') }}</el-button>
                    <el-button type="primary" size="small" @click="saveFlatLayout">{{ t('hump.buttons.save')
                        }}</el-button>
                </el-button-group>

            </div>
        </div>

        <el-dialog v-model="slopeLineManagerVisible" :title="t('hump.messages.slopeLineManagerTitle')" width="760px"
            :close-on-click-modal="false">
            <div style="display:flex;justify-content:flex-end;margin-bottom:10px">
                <el-button type="primary" size="small" @click="addSlopeLineInManager">{{ t('hump.new') }}</el-button>
            </div>
            <el-table :data="slopeLineEditList" size="small" :max-height="360" style="width: 100%">
                <el-table-column prop="id" :label="t('hump.id')" width="240" />
                <el-table-column :label="t('hump.messages.slopeLineName')" min-width="280">
                    <template #default="scope">
                        <template v-if="scope.row._isEditing">
                            <el-input v-model="scope.row.name" size="small"
                                :placeholder="t('hump.messages.enterSlopeLineName')" />
                        </template>
                        <template v-else>
                            <span>{{ scope.row.name }}</span>
                        </template>
                    </template>
                </el-table-column>
                <el-table-column :label="t('hump.operation')" width="220">
                    <template #default="scope">
                        <template v-if="scope.row._isEditing">
                            <el-button type="primary" size="small" :loading="scope.row._loading"
                                @click="saveSlopeLineInManager(scope.row)">{{ t('hump.buttons.save') }}</el-button>
                            <el-button size="small" :disabled="scope.row._loading"
                                @click="cancelEditSlopeLine(scope.$index)">{{ t('hump.buttons.cancel') }}</el-button>
                        </template>
                        <template v-else>
                            <el-button size="small" :disabled="scope.row._loading"
                                @click="startEditSlopeLine(scope.row)">{{ t('hump.buttons.edit') }}</el-button>
                            <el-button type="danger" size="small" :loading="scope.row._loading"
                                @click="removeSlopeLineInManager(scope.row, scope.$index)">{{ t('hump.buttons.delete') }}</el-button>
                        </template>
                    </template>
                </el-table-column>
            </el-table>
            <template #footer>
                <el-button @click="slopeLineManagerVisible = false">{{ t('hump.buttons.cancel') }}</el-button>
            </template>
        </el-dialog>

        <div class="plan-graphic">
            <div class="graphic-placeholder">
                <HumpLayoutCtrl ref="ctrlRef" v-model:flatLayout="flatLayout" v-model:globalCursorX="globalCursorX"
                    :isToolbarDisplay="true" />
            </div>
        </div>

        <div class="plan-subtabs">
            <el-tabs v-model="planSubTab" type="border-card">
                <el-tab-pane :label="t('hump.tabs.ctrl')" name="ctrl">
                    <el-card>
                        <div style="display: flex; justify-content: space-between;">
                            <div style="flex: 1; margin-right: 10px;">
                                <div style="display:flex; align-items: center; justify-content:left;">
                                    <h3>{{ t('hump.controlList') }}</h3>
                                    <el-button :disabled="!flatLayout" type="primary" size="small"
                                        style="margin-left:20px;" @click="addPosition">{{ t('hump.buttons.add')
                                        }}</el-button>
                                </div>
                                <el-table :data="flatLayout?.positionList || []" stripe :max-height="250"
                                    style="width: 100%">
                                    <el-table-column :label="t('hump.id')" width="100">
                                        <template #default="scope">
                                            <el-input v-model="scope.row.id" size="small"
                                                @blur="onPositionIdBlur(scope.row)" />
                                        </template>
                                    </el-table-column>
                                    <el-table-column :label="t('hump.xCoord')" width="150">
                                        <template #default="scope">
                                            <el-input v-model="scope.row.x" size="small" type="number"
                                                @input="onPositionXChange(scope.row)" />
                                        </template>
                                    </el-table-column>
                                    <el-table-column :label="t('hump.insert')" width="120">
                                        <template #default="scope">
                                            <el-button size="small" type="primary"
                                                @click="insertPositionAfter(scope.$index)">{{ t('hump.insertAfter')
                                                }}</el-button>
                                            <el-button size="small" type="danger" style="margin-left:8px"
                                                @click="confirmRemovePosition(scope.$index)">×</el-button>
                                        </template>
                                    </el-table-column>
                                </el-table>
                            </div>
                            <div style="flex: 3; margin-left: 10px;">
                                <div style="display: flex; justify-content: left; align-items: center;">
                                    <h3>{{ t('hump.sectionList') }}</h3>
                                    <el-button :disabled="!isPositionListDirty" type="primary" size="small"
                                        @click="updatePositionSegmentList" style="margin-left:20px">{{ t('hump.update')
                                        }}</el-button>
                                </div>
                                <el-table :data="flatLayout?.positionSegmentList || []" stripe :max-height="250"
                                    style="width: 100%">
                                    <el-table-column prop="id" :label="t('hump.id')" width="100"></el-table-column>
                                    <el-table-column prop="startPositionID" :label="t('hump.startID')"
                                        width="120"></el-table-column>
                                    <el-table-column prop="endPositionID" :label="t('hump.endID')"
                                        width="120"></el-table-column>
                                    <el-table-column :label="t('hump.length')" width="100">
                                        <template #default="scope">
                                            <el-input v-model="scope.row.length" size="small" type="number" disabled />
                                        </template>
                                    </el-table-column>
                                    <el-table-column :label="t('hump.curvature')" width="100">
                                        <template #default="scope">
                                            <el-input v-model="scope.row.curveDegree" size="small" type="number"
                                                @input="onCurveDegreeInput(scope.row, $event)" />
                                        </template>
                                    </el-table-column>
                                    <el-table-column :label="t('hump.direction')" width="100">
                                        <template #default="scope">
                                            <el-select v-model="scope.row.curveDirection"
                                                :disabled="scope.row.curveDegree === 0" size="small">
                                                <el-option
                                                    v-for="opt in getCurveDirectionOptions(scope.row.curveDegree)"
                                                    :key="opt.value" :label="opt.label" :value="opt.value" />
                                            </el-select>
                                        </template>
                                    </el-table-column>
                                    <el-table-column :label="t('hump.location')" width="120">
                                        <template #default="scope">
                                            <el-select v-model="scope.row.locationParam" size="small">
                                                <el-option v-for="opt in locationParamOptions" :key="opt.value"
                                                    :label="opt.label" :value="opt.value" />
                                            </el-select>
                                        </template>
                                    </el-table-column>
                                </el-table>
                            </div>
                        </div>
                    </el-card>
                </el-tab-pane>
                <el-tab-pane :label="t('hump.tabs.switch')" name="switch">
                    <el-card>
                        <div style="display:flex; align-items: center; justify-content:left; margin-bottom: 10px;">
                            <h3>{{ t('hump.switchList') }}</h3>
                            <el-button :disabled="!flatLayout" type="primary" size="small" style="margin-left:20px;"
                                @click="addSwitch">{{ t('hump.buttons.add')
                                }}</el-button>
                        </div>
                        <el-table :data="flatLayout?.switchList || []" stripe :max-height="250" style="width: 100%">
                            <el-table-column :label="t('hump.id')" width="100">
                                <template #default="scope">
                                    {{ scope.row.id }}
                                </template>
                            </el-table-column>
                            <el-table-column :label="t('hump.bindingPosition')" width="140">
                                <template #default="scope">
                                    <el-select v-model="scope.row.bindingPositionID"
                                        :placeholder="t('hump.choosePosition')" size="small" clearable
                                        style="width:120px">
                                        <el-option v-for="opt in getPositionOptions()" :key="opt.value"
                                            :label="opt.label" :value="opt.value" />
                                    </el-select>
                                </template>
                            </el-table-column>
                            <el-table-column :label="t('hump.bindingSegment')" width="160">
                                <template #default="scope">
                                    <el-select v-model="scope.row.bindingPositionSegmentID"
                                        :placeholder="t('hump.chooseSegment')" size="small" clearable
                                        style="width:140px">
                                        <el-option v-for="opt in getPositionSegmentOptions()" :key="opt.value"
                                            :label="opt.label" :value="opt.value" />
                                    </el-select>
                                </template>
                            </el-table-column>
                            <el-table-column :label="t('hump.type')" width="120">
                                <template #default="scope">
                                    <el-select v-model="scope.row.type"
                                        :placeholder="t('hump.chooseType') || t('hump.type')" size="small"
                                        style="width:100px">
                                        <el-option v-for="opt in getSwitchTypeOptions()" :key="opt.value"
                                            :label="opt.label" :value="opt.value" />
                                    </el-select>
                                </template>
                            </el-table-column>
                            <el-table-column :label="t('hump.switchDirectionLabel') || t('hump.direction')" width="120">
                                <template #default="scope">
                                    <el-select v-model="scope.row.direction"
                                        :placeholder="t('hump.chooseDirection') || t('hump.direction')" size="small"
                                        style="width:100px">
                                        <el-option v-for="opt in getSwitchDirectionOptions()" :key="opt.value"
                                            :label="opt.label" :value="opt.value" />
                                    </el-select>
                                </template>
                            </el-table-column>
                            <el-table-column :label="t('hump.sideLabel') || t('hump.side')" width="120">
                                <template #default="scope">
                                    <el-select v-model="scope.row.side"
                                        :placeholder="t('hump.chooseSide') || t('hump.side')" size="small"
                                        style="width:100px">
                                        <el-option v-for="opt in getSwitchSideOptions()" :key="opt.value"
                                            :label="opt.label" :value="opt.value" />
                                    </el-select>
                                </template>
                            </el-table-column>
                            <el-table-column :label="t('hump.curveRadius')" width="120">
                                <template #default="scope">
                                    <el-input v-model="scope.row.curveDegree" size="small" type="number" />
                                </template>
                            </el-table-column>
                            <el-table-column :label="t('hump.operation')" width="100">
                                <template #default="scope">
                                    <el-button size="small" type="danger" @click="confirmRemoveSwitch(scope.$index)">{{
                                        t('hump.buttons.delete') }}</el-button>
                                </template>
                            </el-table-column>
                        </el-table>
                    </el-card>
                </el-tab-pane>
                <el-tab-pane :label="t('hump.tabs.retarder')" name="retarder">
                    <el-card>
                        <div style="display:flex; align-items: center; justify-content:left; margin-bottom: 10px;">
                            <h3>{{ t('hump.retarderList') }}</h3>
                            <el-button :disabled="!flatLayout" type="primary" size="small" style="margin-left:20px;"
                                @click="addRetarder">{{ t('hump.buttons.add')
                                }}</el-button>
                        </div>
                        <el-table :data="flatLayout?.retarderList || []" stripe :max-height="250" style="width: 100%">
                            <el-table-column :label="t('hump.retarder.index')" width="80">
                                <template #default="scope">{{ scope.row.id }}</template>
                            </el-table-column>
                            <el-table-column :label="t('hump.bindingSegment')" width="180">
                                <template #default="scope">
                                    <el-select v-model="scope.row.bindingPositionSegmentID"
                                        :placeholder="t('hump.chooseSegment')" size="small" clearable
                                        style="width:160px">
                                        <el-option v-for="opt in getPositionSegmentOptions()" :key="opt.value"
                                            :label="opt.label" :value="opt.value" />
                                    </el-select>
                                </template>
                            </el-table-column>
                            <el-table-column :label="t('hump.retarder.params')" width="400">
                                <template #default="scope">
                                    <div style="display:flex;align-items:center;gap:6px;flex-wrap:wrap">
                                        <el-tag v-for="(num, idx) in scope.row.numberArray || []" :key="idx" closable
                                            @close="removeRetarderParam(scope.row, idx)">{{ num }}</el-tag>
                                        <template v-if="!scope.row._showNewRetarderInput">
                                            <el-button size="small" @click="showNewRetarderInput(scope.row)">{{
                                                t('hump.retarder.add') }}</el-button>
                                        </template>
                                        <template v-else>
                                            <el-input v-model="scope.row._newRetarderParam"
                                                :placeholder="t('hump.retarder.addParam')" size="small"
                                                style="width:120px" @keyup.enter="addRetarderParam(scope.row)" />
                                            <el-button size="small" @click="addRetarderParam(scope.row)">{{
                                                t('hump.retarder.confirm') }}</el-button>
                                            <el-button size="small" @click="cancelNewRetarderInput(scope.row)">{{
                                                t('hump.retarder.cancel') }}</el-button>
                                        </template>
                                    </div>
                                </template>
                            </el-table-column>
                            <el-table-column :label="t('hump.operation')" width="100">
                                <template #default="scope">
                                    <el-button size="small" type="danger"
                                        @click="confirmRemoveRetarder(scope.$index)">{{ t('hump.buttons.delete')
                                        }}</el-button>
                                </template>
                            </el-table-column>
                        </el-table>
                    </el-card>
                </el-tab-pane>
            </el-tabs>
        </div>
    </section>
</template>

<script setup lang="ts">
import { ref, watch, onMounted, computed } from 'vue'
import { useI18n } from 'vue-i18n'
import HumpLayoutCtrl from './HumpLayoutCtrl.vue'
import type { FlatLayout } from './humplayoutctrl'
import { CurveDirections, SwitchTypes, SwitchDirections, SwitchSides, LocationParam } from './humplayoutctrl'
import axios from '@/utils/axios'
import { ElMessage, ElMessageBox } from 'element-plus'

interface SlopeLine {
    id: string
    instanceID: string
    name: string
}

interface EditableSlopeLine extends SlopeLine {
    _isEditing: boolean
    _isNew: boolean
    _loading: boolean
    _originalName: string
}

interface Props {
    selectedInstanceId?: string | null
}

const props = withDefaults(defineProps<Props>(), {
    selectedInstanceId: null
})

const { t } = useI18n()
const selectedLine = ref<string | null>(null)
const lines = ref<SlopeLine[]>([])
const planSubTab = ref('ctrl')
const slopeLineManagerVisible = ref(false)
const slopeLineEditList = ref<EditableSlopeLine[]>([])

const ctrlRef = ref<InstanceType<typeof HumpLayoutCtrl> | null>(null)
const flatLayout = ref<FlatLayout | null>(null)
const globalCursorX = ref<number | undefined>(undefined)
const originalPositionListJson = ref<string>('')
const isPositionListDirty = computed(() => {
    if (!flatLayout.value) return false
    try {
        return JSON.stringify(flatLayout.value.positionList || []) !== originalPositionListJson.value
    } catch (e) {
        return false
    }
})

// 加载溜放线列�?
async function loadSlopeLines() {
    if (!props.selectedInstanceId) {
        lines.value = []
        return
    }

    try {
        const response = await axios.get('/Hump/GetSlopeLines', {
            params: { instanceID: props.selectedInstanceId }
        })
        lines.value = response.data || []
    } catch (error: any) {
        console.error('Failed to load slope lines:', error)
        ElMessage.error(t('hump.messages.loadSlopeLinesError'))
        lines.value = []
    }
}

// Manage slope line names.
function buildSlopeLineEditRows(source: SlopeLine[]): EditableSlopeLine[] {
    return source.map(line => ({
        ...line,
        _isEditing: false,
        _isNew: false,
        _loading: false,
        _originalName: line.name
    }))
}

async function openSlopeLineManager() {
    if (!props.selectedInstanceId) {
        ElMessage.warning(t('hump.messages.selectInstanceFirst'))
        return
    }
    await loadSlopeLines()
    slopeLineEditList.value = buildSlopeLineEditRows(lines.value)
    slopeLineManagerVisible.value = true
}

function addSlopeLineInManager() {
    if (!props.selectedInstanceId) {
        ElMessage.warning(t('hump.messages.selectInstanceFirst'))
        return
    }
    const tempId = `new-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`
    slopeLineEditList.value.push({
        id: tempId,
        instanceID: props.selectedInstanceId,
        name: '',
        _isEditing: true,
        _isNew: true,
        _loading: false,
        _originalName: ''
    })
}

function startEditSlopeLine(row: EditableSlopeLine) {
    row._originalName = row.name
    row._isEditing = true
}

function cancelEditSlopeLine(index: number) {
    const row = slopeLineEditList.value[index]
    if (!row) return
    if (row._isNew) {
        slopeLineEditList.value.splice(index, 1)
        return
    }
    row.name = row._originalName
    row._isEditing = false
}

async function saveSlopeLineInManager(row: EditableSlopeLine) {
    if (!props.selectedInstanceId) return

    const name = (row.name ?? '').trim()
    if (!name) {
        ElMessage.warning(t('hump.messages.nameRequired'))
        return
    }

    row._loading = true
    try {
        if (row._isNew) {
            await axios.post('/Hump/CreateSlopeLine', {
                InstanceID: props.selectedInstanceId,
                Name: name
            })
            ElMessage.success(t('hump.messages.slopeLineCreated'))
        } else {
            await axios.put('/Hump/EditSlopeLine', {
                ID: row.id,
                InstanceID: row.instanceID || props.selectedInstanceId,
                Name: name
            })
            ElMessage.success(t('hump.messages.slopeLineUpdated'))
        }

        await loadSlopeLines()
        slopeLineEditList.value = buildSlopeLineEditRows(lines.value)
    } catch (error: any) {
        console.error('Failed to save slope line name:', error)
        ElMessage.error(row._isNew ? t('hump.messages.createSlopeLineError') : t('hump.messages.updateSlopeLineError'))
    } finally {
        row._loading = false
    }
}

async function removeSlopeLineInManager(row: EditableSlopeLine, index: number) {
    if (row._isNew) {
        slopeLineEditList.value.splice(index, 1)
        return
    }

    try {
        await ElMessageBox.confirm(
            t('hump.messages.deleteSlopeLineConfirm', { name: row.name || row.id }),
            t('hump.messages.deleteSlopeLineTitle'),
            {
                confirmButtonText: t('hump.buttons.confirm'),
                cancelButtonText: t('hump.buttons.cancel'),
                type: 'warning'
            }
        )

        row._loading = true
        await axios.delete('/Hump/DeleteSlopeLine', {
            params: { id: row.id }
        })

        ElMessage.success(t('hump.messages.slopeLineDeleted'))

        if (selectedLine.value === row.id) {
            selectedLine.value = null
            flatLayout.value = null
        }

        await loadSlopeLines()
        slopeLineEditList.value = buildSlopeLineEditRows(lines.value)
    } catch (error: any) {
        if (error !== 'cancel' && error !== 'close') {
            console.error('Failed to delete slope line:', error)
            ElMessage.error(t('hump.messages.deleteSlopeLineError'))
        }
    } finally {
        row._loading = false
    }
}
// Reload slope lines when selected instance changes.
watch(() => props.selectedInstanceId, (newValue) => {
    if (newValue) {
        loadSlopeLines()
        selectedLine.value = null
        flatLayout.value = null
    } else {
        lines.value = []
        selectedLine.value = null
    }
}, { immediate: true })

// 选中溜放线变化时，清空平面布局数据
watch(selectedLine, (newValue) => {
    flatLayout.value = null
    if (newValue) {
        loadFlatLayout()
    }
}, { immediate: true })

function updateGlobalCursorX(value: number) {
    globalCursorX.value = value
}

function getCurveDirectionOptions(degree: number) {
    const opts = [
        { label: t('hump.curveDirections.Left'), value: CurveDirections.Left },
        { label: t('hump.curveDirections.Right'), value: CurveDirections.Right },
        { label: t('hump.curveDirections.None'), value: CurveDirections.None }
    ]
    return opts.filter(opt => opt.value !== CurveDirections.None || degree <= 0)
}

const locationParamOptions = computed(() => [
    { label: t('hump.locationParams.HumpSection'), value: LocationParam.HumpSection },
    { label: t('hump.locationParams.YardSection'), value: LocationParam.YardSection }
])

function getSwitchTypeLabel(type: SwitchTypes): string {
    switch (type) {
        case SwitchTypes.Single: return t('hump.switchTypes.Single') as string;
        case SwitchTypes.Slip: return t('hump.switchTypes.Slip') as string;
        case SwitchTypes.Diamond: return t('hump.switchTypes.Diamond') as string;
        case SwitchTypes.None: return t('hump.switchTypes.None') as string;
        default: return t('hump.switchTypes.Unknown') as string;
    }
}

function getSwitchDirectionLabel(direction: SwitchDirections): string {
    switch (direction) {
        case SwitchDirections.Reverse: return t('hump.switchDirections.Reverse') as string;
        case SwitchDirections.Forward: return t('hump.switchDirections.Forward') as string;
        case SwitchDirections.None: return t('hump.switchDirections.None') as string;
        default: return t('hump.switchDirections.Unknown') as string;
    }
}

function getSwitchSideLabel(side: SwitchSides): string {
    switch (side) {
        case SwitchSides.Left: return t('hump.switchSides.Left') as string;
        case SwitchSides.Right: return t('hump.switchSides.Right') as string;
        case SwitchSides.None: return t('hump.switchSides.None') as string;
        default: return t('hump.switchSides.Unknown') as string;
    }
}

function getPositionOptions() {
    return (flatLayout.value?.positionList ?? []).map(p => ({ label: p.id, value: p.id }))
}

function getPositionSegmentOptions() {
    return (flatLayout.value?.positionSegmentList ?? []).map(s => ({ label: s.id || `${s.startPositionID}-${s.endPositionID}`, value: s.id }))
}

function getSwitchTypeOptions() {
    return [
        { label: getSwitchTypeLabel(SwitchTypes.Single), value: SwitchTypes.Single },
        { label: getSwitchTypeLabel(SwitchTypes.Slip), value: SwitchTypes.Slip },
        { label: getSwitchTypeLabel(SwitchTypes.Diamond), value: SwitchTypes.Diamond },
        { label: getSwitchTypeLabel(SwitchTypes.None), value: SwitchTypes.None }
    ]
}

function getSwitchDirectionOptions() {
    return [
        { label: getSwitchDirectionLabel(SwitchDirections.Reverse), value: SwitchDirections.Reverse },
        { label: getSwitchDirectionLabel(SwitchDirections.Forward), value: SwitchDirections.Forward },
        { label: getSwitchDirectionLabel(SwitchDirections.None), value: SwitchDirections.None }
    ]
}

function getSwitchSideOptions() {
    return [
        { label: getSwitchSideLabel(SwitchSides.Left), value: SwitchSides.Left },
        { label: getSwitchSideLabel(SwitchSides.Right), value: SwitchSides.Right },
        { label: getSwitchSideLabel(SwitchSides.None), value: SwitchSides.None }
    ]
}

function onRetarderParamsInput(row: any, value: string) {
    if (!value) {
        row.numberArray = []
        return
    }
    row.numberArray = value.split(',').map(s => {
        const n = parseFloat(s.trim())
        return isNaN(n) ? 0 : n
    })
}

function addRetarderParam(row: any) {
    if (!row) return
    const raw = (row._newRetarderParam ?? '').toString().trim()
    if (!raw) {
        row._newRetarderParam = ''
        row._showNewRetarderInput = false
        return
    }
    const n = parseFloat(raw)
    const val = isNaN(n) ? raw : n
    if (!Array.isArray(row.numberArray)) row.numberArray = []
    row.numberArray.push(val)
    row._newRetarderParam = ''
    row._showNewRetarderInput = false
}

function removeRetarderParam(row: any, idx: number) {
    if (!row || !Array.isArray(row.numberArray)) return
    row.numberArray.splice(idx, 1)
}

function showNewRetarderInput(row: any) {
    if (!row) return
    row._newRetarderParam = ''
    row._showNewRetarderInput = true
}

function cancelNewRetarderInput(row: any) {
    if (!row) return
    row._newRetarderParam = ''
    row._showNewRetarderInput = false
}

function onCurveDegreeInput(row: any, value: string) {
    const degree = parseFloat(value);
    row.curveDegree = degree;
    if (degree === 0) {
        row.curveDirection = CurveDirections.None;
    }
}

function onPositionXChange(position: any) {
    if (!flatLayout.value?.positionSegmentList || !flatLayout.value?.positionList) return
    flatLayout.value.positionSegmentList.forEach(seg => {
        if (seg.startPositionID === position.id.toString() || seg.endPositionID === position.id.toString()) {
            const startPos = flatLayout?.value?.positionList.find(p => p.id.toString() === seg.startPositionID)
            const endPos = flatLayout?.value?.positionList.find(p => p.id.toString() === seg.endPositionID)
            if (startPos && endPos) {
                seg.length = Math.abs(endPos.x - startPos.x).toFixed(3) as unknown as number
            }
        }
    })
}

function onPositionIdBlur(position: any) {
    // 当position ID输入框失去焦点时，检查ID是否发生变化
    if (ctrlRef.value) {
        ctrlRef.value.checkPositionIdChange(position.id.toString())
    }
}

async function insertPositionAfter(index: number) {
    try {
        await ElMessageBox.confirm(
            t('hump.messages.insertPositionConfirm'),
            t('hump.messages.insertPositionConfirmTitle'),
            {
                confirmButtonText: t('hump.buttons.confirm'),
                cancelButtonText: t('hump.buttons.cancel'),
                type: 'warning'
            }
        )

        if (!flatLayout.value) return
        const list = flatLayout.value.positionList
        const current = list[index]
        const next = list[index + 1]
        const newId = list.length > 0 ? current?.id + "_" : "P1"
        const currentX = Number(current?.x ?? 0)
        const nextX = next !== undefined ? Number(next.x) : currentX + 1
        const newX = next !== undefined ? Math.round(((currentX + nextX) / 2) * 1000) / 1000 : Math.round(nextX * 1000) / 1000
        list.splice(index + 1, 0, { id: newId, x: newX, height: 0, instanceID: props.selectedInstanceId ?? '' })
    } catch (error) {
        // 用户取消操作，不执行任何操作
    }
}

function addPosition() {
    if (!flatLayout.value) {
        ElMessage.warning(t('hump.messages.loadFlatLayoutFirst'))
        return
    }
    const list = flatLayout.value.positionList
    let newX = 10
    if (list.length > 0) {
        const lastPosition = list[list.length - 1]
        const currentX = lastPosition?.x ?? 0
        newX = Number(currentX) + 10
    }
    list.push({ id: 'P', x: newX, height: 0, instanceID: props.selectedInstanceId ?? '' })
}

function addSwitch() {
    if (!flatLayout.value) {
        ElMessage.warning(t('hump.messages.loadFlatLayoutFirst'))
        return
    }
    if (!flatLayout.value.switchList) {
        flatLayout.value.switchList = []
    }

    // 生成自增ID，从1开�?
    const existingIds = flatLayout.value.switchList
        .map(s => parseInt(s.id))
        .filter(id => !isNaN(id))
    const maxId = existingIds.length > 0 ? Math.max(...existingIds) : 0
    const newId = (maxId + 1).toString()

    flatLayout.value.switchList.push({
        id: newId,
        bindingPositionID: '',
        bindingPositionSegmentID: '',
        type: SwitchTypes.Single,
        direction: SwitchDirections.Forward,
        side: SwitchSides.Left,
        curveDegree: 0
    })
}

async function confirmRemoveSwitch(index: number) {
    if (!flatLayout.value) return
    try {
        await ElMessageBox.confirm(
            t('hump.messages.deleteSwitchConfirm'),
            t('hump.messages.deleteSwitchConfirmTitle'),
            {
                confirmButtonText: t('hump.buttons.confirm'),
                cancelButtonText: t('hump.buttons.cancel'),
                type: 'warning'
            }
        )

        flatLayout.value.switchList?.splice(index, 1)
    } catch (error) {
        // 用户取消，不做操�?
    }
}

function addRetarder() {
    if (!flatLayout.value) {
        ElMessage.warning(t('hump.messages.loadFlatLayoutFirst'))
        return
    }
    if (!flatLayout.value.retarderList) {
        flatLayout.value.retarderList = []
    }

    // 生成自增ID，从1开�?
    const existingIds = flatLayout.value.retarderList
        .map(r => parseInt(r.id))
        .filter(id => !isNaN(id))
    const maxId = existingIds.length > 0 ? Math.max(...existingIds) : 0
    const newId = (maxId + 1).toString()

    flatLayout.value.retarderList.push({
        id: newId,
        numberArray: [],
        bindingPositionSegmentID: '',
        _showNewRetarderInput: false,
        _newRetarderParam: ''
    } as any)
}

async function confirmRemoveRetarder(index: number) {
    if (!flatLayout.value) return
    try {
        await ElMessageBox.confirm(
            t('hump.messages.deleteRetarderConfirm'),
            t('hump.messages.deleteRetarderConfirmTitle'),
            {
                confirmButtonText: t('hump.buttons.confirm'),
                cancelButtonText: t('hump.buttons.cancel'),
                type: 'warning'
            }
        )

        flatLayout.value.retarderList?.splice(index, 1)
    } catch (error) {
        // 用户取消，不做操�?
    }
}

async function confirmRemovePosition(index: number) {
    if (!flatLayout.value) return
    try {
        await ElMessageBox.confirm(
            t('hump.messages.deletePositionConfirm'),
            t('hump.messages.deletePositionConfirmTitle'),
            {
                confirmButtonText: t('hump.buttons.confirm'),
                cancelButtonText: t('hump.buttons.cancel'),
                type: 'warning'
            }
        )

        // 获取要删除的position ID
        const deletedPosition = flatLayout.value.positionList[index]
        if (deletedPosition === undefined) return
        const deletedId = deletedPosition.id.toString()

        // 删除控制�?
        flatLayout.value.positionList.splice(index, 1)

        // 删除引用该控制点的区�?
        if (flatLayout.value.positionSegmentList) {
            flatLayout.value.positionSegmentList = flatLayout.value.positionSegmentList.filter(
                seg => seg.startPositionID !== deletedId && seg.endPositionID !== deletedId
            )
        }
    } catch (error) {
        // 用户取消，不做操�?
    }
}

function updatePositionSegmentList() {
    // 根据最新的positionList更新positionSegmentList
    if (!flatLayout.value?.positionList || !flatLayout.value?.positionSegmentList) return

    // 检查positionList中的元素id是否有重复？如果有，则弹出对话框提示，然后返�?
    const idSet = new Set<string>()
    for (const pos of flatLayout.value.positionList) {
        if (idSet.has(pos.id)) {
            ElMessageBox.alert(
                t('hump.messages.duplicatePositionID', { id: pos.id }),
                t('hump.messages.duplicatePositionIDTitle'),
                {
                    confirmButtonText: t('hump.buttons.confirm'),
                    type: 'warning'
                }
            )
            return
        }
        idSet.add(pos.id)
    }

    const newPositionSegmentList = [] as any[]

    for (var i = 0; i < flatLayout.value.positionList.length - 1; i++) {
        const startPos = flatLayout?.value?.positionList[i]
        const endPos = flatLayout?.value?.positionList[i + 1]

        if (!startPos || !endPos) continue;

        let seg = flatLayout.value.positionSegmentList.find(s => s.startPositionID === startPos.id.toString() && s.endPositionID === endPos.id.toString())
        if (!seg) {
            // 不存在则创建新的区段
            seg = {
                instanceID: props.selectedInstanceId ?? '',
                id: `${startPos.id}${endPos.id}`,
                startPositionID: startPos.id.toString(),
                endPositionID: endPos.id.toString(),
                length: Math.round(Math.abs(endPos.x - startPos.x) * 1000) / 1000,
                curveDegree: 0,
                curveDirection: CurveDirections.None,
                locationParam: LocationParam.HumpSection
            }
            newPositionSegmentList.push(seg)
        }
        else {
            // 存在则保留原有区段对象，但更新长�?
            seg.length = Math.round(Math.abs(endPos.x - startPos.x) * 1000) / 1000
            newPositionSegmentList.push(seg)
        }
    }

    // 更新区段列表并重置加载快照（认为列表已更新）
    flatLayout.value.positionSegmentList = newPositionSegmentList
    originalPositionListJson.value = JSON.stringify(flatLayout.value.positionList || [])
}

function loadFlatLayout() {
    if (!props.selectedInstanceId) {
        ElMessage.warning(t('hump.messages.selectInstanceFirst'))
        return
    }

    if (!selectedLine.value) {
        ElMessage.warning(t('hump.messages.selectSlopeLineFirst'))
        return
    }

    axios.get(`/hump/getflatlayout`, {
        params: {
            instanceID: props.selectedInstanceId,
            slopeLineID: selectedLine.value
        }
    }).then(response => {
        flatLayout.value = response.data
        // 保存加载时的 positionList 快照，用于判断是否发生更�?
        originalPositionListJson.value = JSON.stringify(flatLayout.value?.positionList || [])
        if (flatLayout.value?.positionSegmentList) {
            flatLayout.value.positionSegmentList.forEach(seg => {
                if (seg.curveDegree === 0) {
                    seg.curveDirection = CurveDirections.None
                }
            })
        }
        console.log('Flat layout data loaded:', flatLayout.value)
        ElMessage.success(t('hump.messages.flatLayoutLoaded'))
        // child will react to flatLayout prop change and load data
    }).catch(error => {
        console.error('Error fetching flat layout data:', error)
        ElMessage.error(t('hump.messages.loadFlatLayoutError'))
    })
}


function saveFlatLayout() {
    if (!flatLayout.value) {
        console.warn('No flat layout data to save.')
        return
    }
    axios.put('/hump/editflatlayout', flatLayout.value).then(response => {
        ElMessage.success(t('hump.messages.flatLayoutSaved'))
        console.log('Flat layout data saved successfully.')
    }).catch(error => {
        console.error('Error saving flat layout data:', error)
        ElMessage.error(t('hump.messages.saveFlatLayoutError'))
    })
}

</script>

<style scoped>
.plan-top {
    display: flex;
    justify-content: flex-start;
    align-items: center;
    gap: 12px;
    margin-bottom: 12px;
}

.plan-graphic {
    margin-bottom: 14px;
}

.graphic-placeholder {
    height: 300px;
    border: 2px dashed #d6e5ef;
    border-radius: 8px;
    display: flex;
    align-items: center;
    justify-content: center;
    color: #5d6d7a;
    background: linear-gradient(180deg, #ffffff 0%, #fbfdff 100%);
}

.plan-subtabs {
    margin-top: 12px;
}
</style>

