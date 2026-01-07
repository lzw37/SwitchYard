<template>
    <section class="hump-layout">
        <div class="plan-top">
            <el-select v-model="selectedLine" placeholder="请选择线路" clearable style="width:240px">
                <el-option v-for="line in lines" :key="line.id" :label="line.name" :value="line.id" />
            </el-select>
            <div>
                <el-button type="primary" @click="loadFlatLayout">加载</el-button>
            </div>
        </div>

        <div class="plan-graphic">
            <div class="graphic-placeholder">
                <HumpLayoutCtrl ref="ctrlRef" v-model:flatLayout="flatLayout" v-model:globalCursorX="globalCursorX"
                    :isToolbarDisplay="true" />
            </div>
        </div>

        <div class="plan-subtabs">
            <el-tabs v-model="planSubTab" type="border-card">
                <el-tab-pane label="控制点及区段" name="ctrl">
                    <el-card>
                        <div style="display: flex; justify-content: space-between;">
                            <div style="flex: 1; margin-right: 10px;">
                                <h3>控制点列表</h3>
                                <el-table :data="flatLayout?.positionList || []" stripe :max-height="250"
                                    style="width: 100%">
                                    <el-table-column label="ID" width="100">
                                        <template #default="scope">
                                            <el-input v-model="scope.row.id" size="small" />
                                        </template>
                                    </el-table-column>
                                    <el-table-column label="X 坐标" width="150">
                                        <template #default="scope">
                                            <el-input v-model="scope.row.x" size="small" type="number"
                                                @input="onPositionXChange(scope.row)" />
                                        </template>
                                    </el-table-column>
                                    <el-table-column label="插入" width="120">
                                        <template #default="scope">
                                            <el-button size="small" type="primary"
                                                @click="insertPositionAfter(scope.$index)">插入下方</el-button>
                                        </template>
                                    </el-table-column>
                                </el-table>
                            </div>
                            <div style="flex: 3; margin-left: 10px;">
                                <h3>区段列表</h3>
                                <el-table :data="flatLayout?.positionSegmentList || []" stripe :max-height="250"
                                    style="width: 100%">
                                    <el-table-column prop="id" label="ID" width="100"></el-table-column>
                                    <el-table-column prop="startPositionID" label="起始位置ID"
                                        width="120"></el-table-column>
                                    <el-table-column prop="endPositionID" label="结束位置ID" width="120"></el-table-column>
                                    <el-table-column label="长度" width="100">
                                        <template #default="scope">
                                            <el-input v-model="scope.row.length" size="small" type="number" disabled />
                                        </template>
                                    </el-table-column>
                                    <el-table-column label="曲线度" width="100">
                                        <template #default="scope">
                                            <el-input v-model="scope.row.curveDegree" size="small" type="number"
                                                @input="onCurveDegreeInput(scope.row, $event)" />
                                        </template>
                                    </el-table-column>
                                    <el-table-column label="曲线方向" width="100">
                                        <template #default="scope">
                                            <el-select v-model="scope.row.curveDirection"
                                                :disabled="scope.row.curveDegree === 0" size="small">
                                                <el-option
                                                    v-for="opt in getCurveDirectionOptions(scope.row.curveDegree)"
                                                    :key="opt.value" :label="opt.label" :value="opt.value" />
                                            </el-select>
                                        </template>
                                    </el-table-column>
                                </el-table>
                            </div>
                        </div>
                    </el-card>
                </el-tab-pane>
                <el-tab-pane label="道岔" name="switch">
                    <el-card>
                        <el-table :data="flatLayout?.switchList || []" stripe :max-height="250" style="width: 100%">
                            <el-table-column label="绑定位置ID" width="140">
                                <template #default="scope">
                                    <el-select v-model="scope.row.bindingPositionID" placeholder="请选择位置" size="small"
                                        clearable style="width:120px">
                                        <el-option v-for="opt in getPositionOptions()" :key="opt.value"
                                            :label="opt.label" :value="opt.value" />
                                    </el-select>
                                </template>
                            </el-table-column>
                            <el-table-column label="绑定区段ID" width="160">
                                <template #default="scope">
                                    <el-select v-model="scope.row.bindingPositionSegmentID" placeholder="请选择区段"
                                        size="small" clearable style="width:140px">
                                        <el-option v-for="opt in getPositionSegmentOptions()" :key="opt.value"
                                            :label="opt.label" :value="opt.value" />
                                    </el-select>
                                </template>
                            </el-table-column>
                            <el-table-column label="类型" width="120">
                                <template #default="scope">
                                    <el-select v-model="scope.row.type" placeholder="请选择类型" size="small"
                                        style="width:100px">
                                        <el-option v-for="opt in getSwitchTypeOptions()" :key="opt.value"
                                            :label="opt.label" :value="opt.value" />
                                    </el-select>
                                </template>
                            </el-table-column>
                            <el-table-column label="方向" width="120">
                                <template #default="scope">
                                    <el-select v-model="scope.row.direction" placeholder="请选择方向" size="small"
                                        style="width:100px">
                                        <el-option v-for="opt in getSwitchDirectionOptions()" :key="opt.value"
                                            :label="opt.label" :value="opt.value" />
                                    </el-select>
                                </template>
                            </el-table-column>
                            <el-table-column label="侧边" width="120">
                                <template #default="scope">
                                    <el-select v-model="scope.row.side" placeholder="请选择侧边" size="small"
                                        style="width:100px">
                                        <el-option v-for="opt in getSwitchSideOptions()" :key="opt.value"
                                            :label="opt.label" :value="opt.value" />
                                    </el-select>
                                </template>
                            </el-table-column>
                        </el-table>
                    </el-card>
                </el-tab-pane>
                <el-tab-pane label="减速器" name="retarder">
                    <el-card>
                        <el-table :data="flatLayout?.retarderList || []" stripe :max-height="250" style="width: 100%">
                            <el-table-column label="序号" width="80">
                                <template #default="scope">{{ scope.$index + 1 }}</template>
                            </el-table-column>
                            <el-table-column label="绑定区段ID" width="180">
                                <template #default="scope">
                                    <el-select v-model="scope.row.bindingPositionSegmentID" placeholder="请选择区段"
                                        size="small" clearable style="width:160px">
                                        <el-option v-for="opt in getPositionSegmentOptions()" :key="opt.value"
                                            :label="opt.label" :value="opt.value" />
                                    </el-select>
                                </template>
                            </el-table-column>
                            <el-table-column label="参数">
                                <template #default="scope">
                                    <div style="display:flex;align-items:center;gap:6px;flex-wrap:wrap">
                                        <el-tag v-for="(num, idx) in scope.row.numberArray || []" :key="idx" closable
                                            @close="removeRetarderParam(scope.row, idx)">{{ num }}</el-tag>
                                        <template v-if="!scope.row._showNewRetarderInput">
                                            <el-button size="small"
                                                @click="showNewRetarderInput(scope.row)">+</el-button>
                                        </template>
                                        <template v-else>
                                            <el-input v-model="scope.row._newRetarderParam" placeholder="添加参数"
                                                size="small" style="width:120px"
                                                @keyup.enter="addRetarderParam(scope.row)" />
                                            <el-button size="small" @click="addRetarderParam(scope.row)">确定</el-button>
                                            <el-button size="small"
                                                @click="cancelNewRetarderInput(scope.row)">取消</el-button>
                                        </template>
                                    </div>
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
import { ref } from 'vue'
import HumpLayoutCtrl from './HumpLayoutCtrl.vue'
import type { FlatLayout } from './humplayoutctrl'
import { CurveDirections, SwitchTypes, SwitchDirections, SwitchSides } from './humplayoutctrl'
import axios from 'axios'
import config from '../config.json'

const selectedLine = ref<number | null>(null)
const lines = ref([
    { id: 1, name: '1 号线' },
    { id: 2, name: '2 号线' },
    { id: 3, name: '3 号线' }
])
const planSubTab = ref('ctrl')

const ctrlRef = ref<InstanceType<typeof HumpLayoutCtrl> | null>(null)
const flatLayout = ref<FlatLayout | null>(null)
const globalCursorX = ref<number | undefined>(undefined)

const curveDirectionOptions = [
    { label: '左转', value: CurveDirections.Left },
    { label: '右转', value: CurveDirections.Right },
    { label: '无', value: CurveDirections.None }
]

function updateGlobalCursorX(value: number) {
    globalCursorX.value = value
}

function getCurveDirectionOptions(degree: number) {
    return curveDirectionOptions.filter(opt => opt.value !== CurveDirections.None || degree <= 0)
}

function getSwitchTypeLabel(type: SwitchTypes): string {
    switch (type) {
        case SwitchTypes.Single: return '单开道岔';
        case SwitchTypes.Slip: return '交分道岔';
        case SwitchTypes.Diamond: return '菱形交叉';
        case SwitchTypes.None: return '无';
        default: return '未知';
    }
}

function getSwitchDirectionLabel(direction: SwitchDirections): string {
    switch (direction) {
        case SwitchDirections.Reverse: return '逆向';
        case SwitchDirections.Forward: return '顺向';
        case SwitchDirections.None: return '无';
        default: return '未知';
    }
}

function getSwitchSideLabel(side: SwitchSides): string {
    switch (side) {
        case SwitchSides.Left: return '左开';
        case SwitchSides.Right: return '右开';
        case SwitchSides.None: return '无';
        default: return '未知';
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

function insertPositionAfter(index: number) {
    if (!flatLayout.value) return
    const list = flatLayout.value.positionList
    const current = list[index]
    const next = list[index + 1]
    const newId = list.length > 0 ? (Math.max(...list.map(p => parseInt(p.id))) + 1).toString() : "1"
    const currentX = Number(current?.x ?? 0)
    const nextX = next !== undefined ? Number(next.x) : currentX + 1
    const newX = next !== undefined ? (currentX + nextX) / 2 : nextX
    list.splice(index + 1, 0, { id: newId, x: newX, height: 0 })
}

function loadFlatLayout() {
    axios.get(`${config.serverurl}/hump/getflatlayout`).then(response => {
        flatLayout.value = response.data
        if (flatLayout.value?.positionSegmentList) {
            flatLayout.value.positionSegmentList.forEach(seg => {
                if (seg.curveDegree === 0) {
                    seg.curveDirection = CurveDirections.None
                }
            })
        }
        console.log('Flat layout data loaded:', flatLayout.value)
        // child will react to flatLayout prop change and load data
    }).catch(error => {
        console.error('Error fetching flat layout data:', error)
    })
}

function saveFlatLayout() {
    if (!flatLayout.value) {
        console.warn('No flat layout data to save.')
        return
    }
    axios.post('https://localhost:7297/hump/saveflatlayout', flatLayout.value).then(response => {
        console.log('Flat layout data saved successfully.')
    }).catch(error => {
        console.error('Error saving flat layout data:', error)
    })
}

</script>

<style scoped>
.plan-top {
    display: flex;
    justify-content: flex-start;
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