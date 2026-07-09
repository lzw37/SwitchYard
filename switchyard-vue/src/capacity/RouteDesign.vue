<template>
    <section class="route-design-page" v-loading="loadingData">
        <div class="route-design-toolbar">
            <div class="route-design-toolbar-left">
                <div class="route-design-scheme-control">
                    <span class="route-design-control-label">{{ t('stationLayout.menu.stationScheme') }}</span>
                    <el-select
                        v-model="currentStationSchemeId"
                        size="small"
                        filterable
                        class="route-design-scheme-select"
                        :loading="loadingStationSchemes"
                        :disabled="!selectedInstanceId || loadingStationSchemes || loadingData"
                        :placeholder="t('stationLayout.placeholders.selectStationScheme')"
                        @change="handleStationSchemeChange"
                    >
                        <el-option
                            v-for="option in stationSchemeOptions"
                            :key="option.id"
                            :label="formatStationSchemeLabel(option)"
                            :value="option.id"
                        />
                    </el-select>
                </div>
            </div>
            <div class="route-design-toolbar-right">
                <div class="route-design-switch-control">
                    <span class="route-design-control-label">{{ t('routeDesign.toolbar.stationRoute') }}</span>
                    <el-switch v-model="showStationRouteCard" size="small" />
                </div>
                <div class="route-design-switch-control">
                    <span class="route-design-control-label">{{ t('routeDesign.toolbar.routeEnd') }}</span>
                    <el-switch v-model="showRouteEndCard" size="small" />
                </div>
            </div>
        </div>

        <div
            ref="splitContainerRef"
            class="route-design-body"
            :class="{ 'is-resizing': isResizing }"
        >
            <div class="route-design-editor-pane" :style="leftPaneStyle">
                <div class="route-design-editor-scroll">
                    <StationLayoutEditor
                        ref="stationLayoutEditorRef"
                        readonly
                        :display-styles="layoutDisplayStyles"
                        :show-grid="true"
                        :show-nodes="true"
                        :show-curve-arc="true"
                        :route-pick-target="routePickTarget"
                        :highlighted-route-node-ids="highlightedRouteNodeIds"
                        :highlighted-route-link-ids="highlightedRouteLinkIds"
                        @route-node-pick="handleRouteNodePick"
                    />
                </div>
                <section
                    v-if="routeSearchDialogVisible"
                    class="route-search-result-popover"
                    v-loading="routeSearchLoading"
                >
                    <header class="route-search-result-header">
                        <div>
                            <h3>{{ t('routeDesign.stationRoute.searchDialog.title') }}</h3>
                            <span>{{ routeSearchDialogSubtitle }}</span>
                        </div>
                        <el-button :icon="Close" circle size="small" @click="closeRouteSearchDialog" />
                    </header>
                    <el-table
                        :data="routeSearchCandidates"
                        size="small"
                        height="220"
                        row-key="index"
                        highlight-current-row
                        :current-row-key="selectedRouteCandidateIndex"
                        :empty-text="t('routeDesign.stationRoute.searchDialog.empty')"
                        @row-click="selectRouteSearchCandidate"
                    >
                        <el-table-column label="#" width="46">
                            <template #default="{ row }">
                                {{ row.index + 1 }}
                            </template>
                        </el-table-column>
                        <el-table-column :label="t('routeDesign.stationRoute.searchDialog.direction')" width="76">
                            <template #default="{ row }">
                                {{ getRouteDirectionLabel(row.direction) }}
                            </template>
                        </el-table-column>
                        <el-table-column :label="t('routeDesign.stationRoute.searchDialog.path')" show-overflow-tooltip>
                            <template #default="{ row }">
                                {{ getRouteSummary(row) }}
                            </template>
                        </el-table-column>
                    </el-table>
                    <footer class="route-search-result-actions">
                        <el-button size="small" @click="closeRouteSearchDialog">
                            {{ t('routeDesign.stationRoute.actions.cancel') }}
                        </el-button>
                        <el-button
                            type="primary"
                            size="small"
                            :disabled="!selectedRouteCandidate"
                            @click="applySelectedRouteCandidate"
                        >
                            {{ t('routeDesign.stationRoute.searchDialog.useRoute') }}
                        </el-button>
                    </footer>
                </section>
            </div>
            <div
                class="route-design-resizer"
                role="separator"
                aria-orientation="vertical"
                @mousedown="startResize"
                @dblclick="resetSplit"
            />
            <aside
                class="route-design-data-pane"
                :class="{ 'is-single-card': visibleRoutePanelCount === 1 }"
            >
                <section
                    v-if="showStationRouteCard"
                    class="station-route-card"
                    v-loading="loadingRoutes || savingRoute || routeSearchLoading"
                >
                    <header class="station-route-card-header">
                        <div class="station-route-title-group">
                            <h2>{{ t('routeDesign.stationRoute.title') }}</h2>
                            <span class="station-route-subtitle">
                                {{ stationRouteHeaderText }}
                            </span>
                        </div>
                        <div class="station-route-card-actions">
                            <el-tooltip :content="t('routeDesign.stationRoute.actions.refresh')" placement="top">
                                <el-button
                                    :icon="Refresh"
                                    circle
                                    size="small"
                                    :disabled="!canLoadRoutes"
                                    @click="loadStationRoutes"
                                />
                            </el-tooltip>
                            <el-tooltip :content="t('routeDesign.stationRoute.actions.add')" placement="top">
                                <el-button
                                    :icon="Plus"
                                    circle
                                    size="small"
                                    type="primary"
                                    :disabled="!canEditRoutes"
                                    @click="startCreateStationRoute"
                                />
                            </el-tooltip>
                        </div>
                    </header>

                    <div class="station-route-table-wrap">
                        <el-table
                            :data="stationRoutes"
                            size="small"
                            height="100%"
                            row-key="id"
                            highlight-current-row
                            :current-row-key="selectedRouteId"
                            :empty-text="t('routeDesign.stationRoute.empty')"
                            @row-click="selectStationRoute"
                        >
                            <el-table-column prop="id" :label="t('routeDesign.stationRoute.fields.id')" min-width="116" show-overflow-tooltip />
                            <el-table-column prop="type" :label="t('routeDesign.stationRoute.fields.type')" min-width="88" show-overflow-tooltip />
                            <el-table-column prop="startNodeID" :label="t('routeDesign.stationRoute.fields.startNodeID')" width="86" show-overflow-tooltip />
                            <el-table-column prop="endNodeID" :label="t('routeDesign.stationRoute.fields.endNodeID')" width="86" show-overflow-tooltip />
                        </el-table>
                    </div>

                    <div class="station-route-form-panel">
                        <div class="station-route-form-header">
                            <span class="station-route-form-title">{{ stationRouteFormTitle }}</span>
                            <el-tag v-if="routeEditMode === 'create'" size="small" type="success">
                                {{ t('routeDesign.stationRoute.states.new') }}
                            </el-tag>
                            <el-tag v-else-if="routeNodePickStage !== 'none'" size="small" type="warning">
                                {{ t('routeDesign.stationRoute.states.picking') }}
                            </el-tag>
                        </div>

                        <el-form label-position="top" size="small" class="station-route-form" :model="routeForm">
                            <el-form-item :label="t('routeDesign.stationRoute.fields.instanceID')">
                                <el-input v-model="routeForm.instanceID" disabled />
                            </el-form-item>
                            <el-form-item :label="t('routeDesign.stationRoute.fields.stationSchemeID')">
                                <el-input v-model="routeForm.stationSchemeID" disabled />
                            </el-form-item>
                            <el-form-item :label="t('routeDesign.stationRoute.fields.id')">
                                <el-input
                                    v-model="routeForm.id"
                                    :placeholder="t('routeDesign.stationRoute.placeholders.autoId')"
                                    :disabled="!canEditRoutes || savingRoute"
                                />
                            </el-form-item>
                            <el-form-item :label="t('routeDesign.stationRoute.fields.type')">
                                <el-select
                                    v-model="routeForm.type"
                                    filterable
                                    allow-create
                                    default-first-option
                                    clearable
                                    :disabled="!canEditRoutes || savingRoute"
                                    class="station-route-full-control"
                                >
                                    <el-option
                                        v-for="option in routeTypeOptions"
                                        :key="option"
                                        :label="option"
                                        :value="option"
                                    />
                                </el-select>
                            </el-form-item>
                            <el-form-item :label="t('routeDesign.stationRoute.fields.startNodeID')" required>
                                <div class="station-route-node-control">
                                    <el-input
                                        v-model="routeForm.startNodeID"
                                        :disabled="!canEditRoutes || savingRoute"
                                    />
                                    <el-tooltip :content="t('routeDesign.stationRoute.actions.pickStart')" placement="top">
                                        <el-button
                                            :icon="Aim"
                                            :type="routeNodePickStage === 'start' ? 'primary' : 'default'"
                                            :disabled="!canEditRoutes || savingRoute"
                                            @click="startStationRouteNodePick('start')"
                                        />
                                    </el-tooltip>
                                </div>
                            </el-form-item>
                            <el-form-item :label="t('routeDesign.stationRoute.fields.endNodeID')" required>
                                <div class="station-route-node-control">
                                    <el-input
                                        v-model="routeForm.endNodeID"
                                        :disabled="!canEditRoutes || savingRoute"
                                    />
                                    <el-tooltip :content="t('routeDesign.stationRoute.actions.pickEnd')" placement="top">
                                        <el-button
                                            :icon="Aim"
                                            :type="routeNodePickStage === 'end' ? 'primary' : 'default'"
                                            :disabled="!canEditRoutes || savingRoute"
                                            @click="startStationRouteNodePick('end')"
                                        />
                                    </el-tooltip>
                                </div>
                            </el-form-item>
                            <el-form-item :label="t('routeDesign.stationRoute.fields.nodeList')">
                                <el-input
                                    v-model="routeForm.nodeList"
                                    type="textarea"
                                    :autosize="{ minRows: 2, maxRows: 4 }"
                                    :disabled="!canEditRoutes || savingRoute"
                                />
                            </el-form-item>
                            <el-form-item :label="t('routeDesign.stationRoute.fields.linkList')">
                                <el-input
                                    v-model="routeForm.linkList"
                                    type="textarea"
                                    :autosize="{ minRows: 2, maxRows: 4 }"
                                    :disabled="!canEditRoutes || savingRoute"
                                />
                            </el-form-item>
                            <el-form-item :label="t('routeDesign.stationRoute.fields.switchList')">
                                <el-input v-model="routeForm.switchList" :disabled="!canEditRoutes || savingRoute" />
                            </el-form-item>
                            <el-form-item :label="t('routeDesign.stationRoute.fields.cellList')">
                                <el-input v-model="routeForm.cellList" :disabled="!canEditRoutes || savingRoute" />
                            </el-form-item>
                            <el-form-item :label="t('routeDesign.stationRoute.fields.signalList')">
                                <el-input v-model="routeForm.signalList" :disabled="!canEditRoutes || savingRoute" />
                            </el-form-item>
                            <el-form-item :label="t('routeDesign.stationRoute.fields.allowanceTags')">
                                <el-input v-model="routeForm.allowanceTags" :disabled="!canEditRoutes || savingRoute" />
                            </el-form-item>
                            <el-form-item :label="t('routeDesign.stationRoute.fields.forbiddenTags')">
                                <el-input v-model="routeForm.forbiddenTags" :disabled="!canEditRoutes || savingRoute" />
                            </el-form-item>
                        </el-form>

                        <div class="station-route-form-actions">
                            <el-button
                                :icon="Check"
                                type="primary"
                                size="small"
                                :disabled="!canSaveRoute"
                                @click="saveStationRoute"
                            >
                                {{ t('routeDesign.stationRoute.actions.save') }}
                            </el-button>
                            <el-button
                                :icon="Close"
                                size="small"
                                :disabled="savingRoute"
                                @click="cancelStationRouteEdit"
                            >
                                {{ t('routeDesign.stationRoute.actions.cancel') }}
                            </el-button>
                            <el-button
                                :icon="Delete"
                                type="danger"
                                size="small"
                                plain
                                :disabled="!selectedRouteId || savingRoute"
                                @click="deleteSelectedStationRoute"
                            >
                                {{ t('routeDesign.stationRoute.actions.delete') }}
                            </el-button>
                        </div>
                    </div>
                </section>

                <section
                    v-if="showRouteEndCard"
                    class="route-end-card"
                    v-loading="loadingRouteEnds || savingRouteEnd"
                >
                    <header class="route-end-card-header">
                        <div class="route-end-title-group">
                            <h2>{{ t('routeDesign.routeEnd.title') }}</h2>
                            <span class="route-end-subtitle">
                                {{ routeEndHeaderText }}
                            </span>
                        </div>
                        <div class="route-end-card-actions">
                            <el-tooltip :content="t('routeDesign.routeEnd.actions.refresh')" placement="top">
                                <el-button
                                    :icon="Refresh"
                                    circle
                                    size="small"
                                    :disabled="!canLoadRouteEnds"
                                    @click="loadRouteEnds"
                                />
                            </el-tooltip>
                            <el-tooltip :content="t('routeDesign.routeEnd.actions.add')" placement="top">
                                <el-button
                                    :icon="Plus"
                                    circle
                                    size="small"
                                    type="primary"
                                    :disabled="!canEditRouteEnds"
                                    @click="startCreateRouteEnd"
                                />
                            </el-tooltip>
                        </div>
                    </header>

                    <div class="route-end-table-wrap">
                        <el-table
                            :data="routeEnds"
                            size="small"
                            height="100%"
                            row-key="id"
                            highlight-current-row
                            :current-row-key="selectedRouteEndId"
                            :empty-text="t('routeDesign.routeEnd.empty')"
                            @row-click="selectRouteEnd"
                        >
                            <el-table-column prop="id" :label="t('routeDesign.routeEnd.fields.id')" min-width="120" show-overflow-tooltip />
                            <el-table-column prop="type" :label="t('routeDesign.routeEnd.fields.type')" min-width="116" show-overflow-tooltip />
                            <el-table-column prop="bindingNodeID" :label="t('routeDesign.routeEnd.fields.bindingNodeID')" width="96" show-overflow-tooltip />
                            <el-table-column prop="segmentTag" :label="t('routeDesign.routeEnd.fields.segmentTag')" min-width="100" show-overflow-tooltip />
                            <el-table-column prop="sidingTag" :label="t('routeDesign.routeEnd.fields.sidingTag')" min-width="96" show-overflow-tooltip />
                        </el-table>
                    </div>

                    <div class="route-end-form-panel">
                        <div class="route-end-form-header">
                            <span class="route-end-form-title">{{ routeEndFormTitle }}</span>
                            <el-tag v-if="routeEndEditMode === 'create'" size="small" type="success">
                                {{ t('routeDesign.routeEnd.states.new') }}
                            </el-tag>
                            <el-tag v-else-if="routeEndPickingNode" size="small" type="warning">
                                {{ t('routeDesign.routeEnd.states.picking') }}
                            </el-tag>
                        </div>

                        <el-form label-position="top" size="small" class="route-end-form" :model="routeEndForm">
                            <el-form-item :label="t('routeDesign.routeEnd.fields.instanceID')">
                                <el-input v-model="routeEndForm.instanceID" disabled />
                            </el-form-item>
                            <el-form-item :label="t('routeDesign.routeEnd.fields.stationSchemeID')">
                                <el-input v-model="routeEndForm.stationSchemeID" disabled />
                            </el-form-item>
                            <el-form-item :label="t('routeDesign.routeEnd.fields.id')">
                                <el-input
                                    v-model="routeEndForm.id"
                                    :placeholder="t('routeDesign.routeEnd.placeholders.autoId')"
                                    :disabled="!canEditRouteEnds || savingRouteEnd"
                                />
                            </el-form-item>
                            <el-form-item :label="t('routeDesign.routeEnd.fields.bindingNodeID')" required>
                                <div class="route-end-binding-control">
                                    <el-input
                                        v-model="routeEndForm.bindingNodeID"
                                        :disabled="!canEditRouteEnds || savingRouteEnd"
                                    />
                                    <el-tooltip :content="t('routeDesign.routeEnd.actions.pickNode')" placement="top">
                                        <el-button
                                            :icon="Aim"
                                            :type="routeEndPickingNode ? 'primary' : 'default'"
                                            :disabled="!canEditRouteEnds || savingRouteEnd"
                                            @click="startRouteEndNodePick"
                                        />
                                    </el-tooltip>
                                </div>
                            </el-form-item>
                            <el-form-item :label="t('routeDesign.routeEnd.fields.type')">
                                <el-select
                                    v-model="routeEndForm.type"
                                    filterable
                                    allow-create
                                    default-first-option
                                    clearable
                                    :disabled="!canEditRouteEnds || savingRouteEnd"
                                    class="route-end-full-control"
                                >
                                    <el-option
                                        v-for="option in routeEndTypeOptions"
                                        :key="option"
                                        :label="option"
                                        :value="option"
                                    />
                                </el-select>
                            </el-form-item>
                            <el-form-item :label="t('routeDesign.routeEnd.fields.segmentTag')">
                                <el-input
                                    v-model="routeEndForm.segmentTag"
                                    :disabled="!canEditRouteEnds || savingRouteEnd"
                                />
                            </el-form-item>
                            <el-form-item :label="t('routeDesign.routeEnd.fields.sidingTag')">
                                <el-input
                                    v-model="routeEndForm.sidingTag"
                                    :disabled="!canEditRouteEnds || savingRouteEnd"
                                />
                            </el-form-item>
                        </el-form>

                        <div class="route-end-form-actions">
                            <el-button
                                :icon="Check"
                                type="primary"
                                size="small"
                                :disabled="!canSaveRouteEnd"
                                @click="saveRouteEnd"
                            >
                                {{ t('routeDesign.routeEnd.actions.save') }}
                            </el-button>
                            <el-button
                                :icon="Close"
                                size="small"
                                :disabled="savingRouteEnd"
                                @click="cancelRouteEndEdit"
                            >
                                {{ t('routeDesign.routeEnd.actions.cancel') }}
                            </el-button>
                            <el-button
                                :icon="Delete"
                                type="danger"
                                size="small"
                                plain
                                :disabled="!selectedRouteEndId || savingRouteEnd"
                                @click="deleteSelectedRouteEnd"
                            >
                                {{ t('routeDesign.routeEnd.actions.delete') }}
                            </el-button>
                        </div>
                    </div>
                </section>
            </aside>
        </div>
    </section>
</template>

<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Aim, Check, Close, Delete, Plus, Refresh } from '@element-plus/icons-vue'
import axios from '@/utils/axios'
import StationLayoutEditor from './components/StationLayoutEditor.vue'

interface StationSchemeOption {
    id: string
    name: string
}

interface StationRouteEnd {
    instanceID: string
    stationSchemeID: string
    id: string
    bindingNodeID: string
    type: string
    segmentTag: string
    sidingTag: string
}

interface StationRoute {
    instanceID: string
    stationSchemeID: string
    id: string
    type: string
    nodeList: string
    linkList: string
    switchList: string
    cellList: string
    signalList: string
    allowanceTags: string
    forbiddenTags: string
    startNodeID: string
    endNodeID: string
}

interface StationRouteSearchCandidate {
    index: number
    direction: string
    nodeIds: string[]
    linkIds: string[]
    switchIds: string[]
    cellIds: string[]
    signalIds: string[]
    nodeList: string
    linkList: string
    switchList: string
    cellList: string
    signalList: string
    startNodeID: string
    endNodeID: string
}

interface RouteNodePickPayload {
    target?: string
    nodeId?: string
    nodeID?: string
}

type RouteEndEditMode = 'none' | 'create' | 'edit'
type StationRouteEditMode = 'none' | 'create' | 'edit'
type StationRouteNodePickStage = 'none' | 'start' | 'end'

const props = withDefaults(defineProps<{
    selectedInstanceId?: string | null
}>(), {
    selectedInstanceId: '',
})

const { t } = useI18n()

const stationLayoutEditorRef = ref<any>(null)
const splitContainerRef = ref<HTMLElement | null>(null)
const currentStationSchemeId = ref('')
const loadingStationSchemes = ref(false)
const loadingData = ref(false)
const loadingRouteEnds = ref(false)
const savingRouteEnd = ref(false)
const loadingRoutes = ref(false)
const savingRoute = ref(false)
const routeSearchLoading = ref(false)
const stationSchemeOptions = ref<StationSchemeOption[]>([])
const layoutDisplayStyles = ref<Record<string, unknown>>({})
const leftPaneWidth = ref(0)
const isResizing = ref(false)
const showStationRouteCard = ref(true)
const showRouteEndCard = ref(true)
const stationRoutes = ref<StationRoute[]>([])
const selectedRouteId = ref('')
const routeOriginalId = ref('')
const routeEditMode = ref<StationRouteEditMode>('none')
const routeNodePickStage = ref<StationRouteNodePickStage>('none')
const routeSearchDialogVisible = ref(false)
const routeSearchCandidates = ref<StationRouteSearchCandidate[]>([])
const selectedRouteCandidateIndex = ref(-1)
const routeForm = ref<StationRoute>({
    instanceID: props.selectedInstanceId || '',
    stationSchemeID: '',
    id: '',
    type: '',
    nodeList: '',
    linkList: '',
    switchList: '',
    cellList: '',
    signalList: '',
    allowanceTags: '',
    forbiddenTags: '',
    startNodeID: '',
    endNodeID: '',
})
const routeEnds = ref<StationRouteEnd[]>([])
const selectedRouteEndId = ref('')
const routeEndOriginalId = ref('')
const routeEndEditMode = ref<RouteEndEditMode>('none')
const routeEndPickingNode = ref(false)
const routeEndForm = ref<StationRouteEnd>({
    instanceID: props.selectedInstanceId || '',
    stationSchemeID: '',
    id: '',
    bindingNodeID: '',
    type: '',
    segmentTag: '',
    sidingTag: '',
})
const routeEndTypeOptions = [
    'StationGate',
    'DepartureSignal',
    'ShuntingSignal',
]
const routeTypeOptions = [
    'Arrival',
    'Departure',
    'Shunting',
    'Locomotive',
]

const selectedInstanceId = computed(() => props.selectedInstanceId || '')
const leftPaneStyle = computed(() => (
    leftPaneWidth.value > 0
        ? { flexBasis: `${leftPaneWidth.value}px` }
        : { flexBasis: '64%' }
))
const canLoadRoutes = computed(() => Boolean(selectedInstanceId.value && currentStationSchemeId.value.trim()))
const canEditRoutes = computed(() => canLoadRoutes.value && !loadingData.value)
const canLoadRouteEnds = computed(() => Boolean(selectedInstanceId.value && currentStationSchemeId.value.trim()))
const canEditRouteEnds = computed(() => canLoadRouteEnds.value && !loadingData.value)
const selectedStationRoute = computed(() => (
    stationRoutes.value.find((item) => item.id === selectedRouteId.value) || null
))
const routeNodePickTarget = computed(() => {
    if (routeNodePickStage.value === 'start') return 'stationRouteStartNode'
    if (routeNodePickStage.value === 'end') return 'stationRouteEndNode'
    return ''
})
const selectedRouteEnd = computed(() => (
    routeEnds.value.find((item) => item.id === selectedRouteEndId.value) || null
))
const routeEndPickTarget = computed(() => (routeEndPickingNode.value ? 'stationRouteEndBindingNode' : ''))
const routePickTarget = computed(() => routeNodePickTarget.value || routeEndPickTarget.value)
const selectedRouteCandidate = computed(() => (
    routeSearchCandidates.value.find((item) => item.index === selectedRouteCandidateIndex.value) || null
))
const highlightedRouteNodeIds = computed(() => {
    const ids = new Set<string>()
    const candidate = routeSearchDialogVisible.value ? selectedRouteCandidate.value : null
    for (const id of candidate?.nodeIds || parseRouteIdText(routeForm.value.nodeList)) {
        if (id) ids.add(id)
    }

    const startNodeID = routeForm.value.startNodeID || selectedStationRoute.value?.startNodeID || ''
    const endNodeID = routeForm.value.endNodeID || selectedStationRoute.value?.endNodeID || ''
    if (startNodeID) ids.add(startNodeID)
    if (endNodeID) ids.add(endNodeID)

    const routeEndNodeId = routeEndForm.value.bindingNodeID || selectedRouteEnd.value?.bindingNodeID || ''
    if (routeEndNodeId) ids.add(routeEndNodeId)

    return Array.from(ids)
})
const highlightedRouteLinkIds = computed(() => {
    const candidate = routeSearchDialogVisible.value ? selectedRouteCandidate.value : null
    return candidate?.linkIds || parseRouteIdText(routeForm.value.linkList)
})
const stationRouteHeaderText = computed(() => {
    if (routeNodePickStage.value === 'start') return t('routeDesign.stationRoute.messages.pickStart')
    if (routeNodePickStage.value === 'end') return t('routeDesign.stationRoute.messages.pickEnd')
    return t('routeDesign.stationRoute.count', { count: stationRoutes.value.length })
})
const stationRouteFormTitle = computed(() => {
    if (routeEditMode.value === 'create') return t('routeDesign.stationRoute.form.newTitle')
    if (selectedRouteId.value) return t('routeDesign.stationRoute.form.editTitle')
    return t('routeDesign.stationRoute.form.emptyTitle')
})
const canSaveRoute = computed(() => (
    canEditRoutes.value &&
    !savingRoute.value &&
    Boolean(routeForm.value.startNodeID.trim()) &&
    Boolean(routeForm.value.endNodeID.trim()) &&
    (routeEditMode.value === 'create' || Boolean(routeForm.value.id.trim()))
))
const routeSearchDialogSubtitle = computed(() => (
    t('routeDesign.stationRoute.searchDialog.count', { count: routeSearchCandidates.value.length })
))
const visibleRoutePanelCount = computed(() => (
    Number(showStationRouteCard.value) + Number(showRouteEndCard.value)
))
const routeEndHeaderText = computed(() => (
    routeEndPickingNode.value
        ? t('routeDesign.routeEnd.messages.pickNode')
        : t('routeDesign.routeEnd.count', { count: routeEnds.value.length })
))
const routeEndFormTitle = computed(() => {
    if (routeEndEditMode.value === 'create') return t('routeDesign.routeEnd.form.newTitle')
    if (selectedRouteEndId.value) return t('routeDesign.routeEnd.form.editTitle')
    return t('routeDesign.routeEnd.form.emptyTitle')
})
const canSaveRouteEnd = computed(() => (
    canEditRouteEnds.value &&
    !savingRouteEnd.value &&
    Boolean(routeEndForm.value.bindingNodeID.trim()) &&
    (routeEndEditMode.value === 'create' || Boolean(routeEndForm.value.id.trim()))
))

let stationSchemeLoadVersion = 0
let layoutLoadVersion = 0
let routeLoadVersion = 0
let routeEndLoadVersion = 0
let resizeObserver: ResizeObserver | null = null
let previousBodyCursor = ''
let previousBodyUserSelect = ''

function readString(source: any, ...keys: string[]): string {
    if (!source || typeof source !== 'object') return ''

    for (const key of keys) {
        const value = source[key]
        if (value !== undefined && value !== null) return String(value)
    }

    return ''
}

function normalizeStationSchemeOption(item: any): StationSchemeOption | null {
    const id = readString(item, 'id', 'ID').trim()
    if (!id) return null

    const name = readString(item, 'name', 'Name').trim() || id
    return { id, name }
}

function setStationSchemeOptions(options: StationSchemeOption[], includeCurrent = true) {
    const optionsById = new Map<string, StationSchemeOption>()
    for (const option of options) {
        if (!option.id || optionsById.has(option.id)) continue
        optionsById.set(option.id, option)
    }

    stationSchemeOptions.value = Array.from(optionsById.values())
    if (includeCurrent) ensureCurrentStationSchemeOption()
}

function ensureCurrentStationSchemeOption(name?: string) {
    const id = currentStationSchemeId.value.trim()
    if (!id) return
    if (stationSchemeOptions.value.some((option) => option.id === id)) return

    stationSchemeOptions.value = [
        ...stationSchemeOptions.value,
        {
            id,
            name: name || id,
        },
    ]
}

function formatStationSchemeLabel(option: StationSchemeOption): string {
    return option.name || option.id
}

function parseRouteIdText(value: string): string[] {
    const text = String(value || '').trim()
    if (!text) return []

    try {
        const parsed = JSON.parse(text)
        if (Array.isArray(parsed)) {
            return parsed.map((id) => String(id).trim()).filter(Boolean)
        }
    } catch {
        // Plain text lists are allowed in the form.
    }

    return text
        .split(/(?:\s*->\s*)|(?:\s*[,\n;]\s*)|\s+/)
        .map((id) => id.trim())
        .filter(Boolean)
}

function serializeRouteIdList(ids: string[]): string {
    return JSON.stringify(ids.map((id) => String(id).trim()).filter(Boolean))
}

function normalizeRouteIdList(route: any, keys: string[]): string[] {
    for (const key of keys) {
        const value = route?.[key]
        if (Array.isArray(value)) {
            return value.map((id) => String(id).trim()).filter(Boolean)
        }
    }

    return []
}

function normalizeStationRouteSearchCandidate(
    route: any,
    index: number,
    fallbackStartNodeID: string,
    fallbackEndNodeID: string
): StationRouteSearchCandidate {
    const nodeIds = normalizeRouteIdList(route, ['nodeIds', 'nodeIDs', 'NodeIds', 'NodeIDs'])
    const linkIds = normalizeRouteIdList(route, ['linkIds', 'linkIDs', 'LinkIds', 'LinkIDs'])
    const switchIds = normalizeRouteIdList(route, ['switchIds', 'switchIDs', 'SwitchIds', 'SwitchIDs'])
    const cellIds = normalizeRouteIdList(route, ['cellIds', 'cellIDs', 'CellIds', 'CellIDs'])
    const signalIds = normalizeRouteIdList(route, ['signalIds', 'signalIDs', 'SignalIds', 'SignalIDs'])
    const startNodeID = nodeIds[0] || fallbackStartNodeID
    const endNodeID = nodeIds[nodeIds.length - 1] || fallbackEndNodeID

    return {
        index,
        direction: readString(route, 'direction', 'Direction').trim(),
        nodeIds,
        linkIds,
        switchIds,
        cellIds,
        signalIds,
        nodeList: serializeRouteIdList(nodeIds),
        linkList: serializeRouteIdList(linkIds),
        switchList: serializeRouteIdList(switchIds),
        cellList: serializeRouteIdList(cellIds),
        signalList: serializeRouteIdList(signalIds),
        startNodeID,
        endNodeID,
    }
}

function getRouteDirectionLabel(direction: string): string {
    if (direction === 'LeftToRight') return t('routeDesign.stationRoute.directions.leftToRight')
    if (direction === 'RightToLeft') return t('routeDesign.stationRoute.directions.rightToLeft')
    return direction || '-'
}

function getRouteSummary(route: StationRouteSearchCandidate | null): string {
    if (!route) return ''
    if (route.nodeIds.length > 0) return route.nodeIds.join(' -> ')
    return t('routeDesign.stationRoute.searchDialog.linkSummary', { count: route.linkIds.length })
}

function createEmptyStationRouteForm(): StationRoute {
    return {
        instanceID: selectedInstanceId.value,
        stationSchemeID: currentStationSchemeId.value.trim(),
        id: '',
        type: '',
        nodeList: '',
        linkList: '',
        switchList: '',
        cellList: '',
        signalList: '',
        allowanceTags: '',
        forbiddenTags: '',
        startNodeID: '',
        endNodeID: '',
    }
}

function normalizeStationRoute(item: any): StationRoute | null {
    const id = readString(item, 'id', 'ID').trim()
    if (!id) return null

    return {
        instanceID: readString(item, 'instanceID', 'InstanceID').trim(),
        stationSchemeID: readString(item, 'stationSchemeID', 'StationSchemeID').trim(),
        id,
        type: readString(item, 'type', 'Type').trim(),
        nodeList: readString(item, 'nodeList', 'NodeList').trim(),
        linkList: readString(item, 'linkList', 'LinkList').trim(),
        switchList: readString(item, 'switchList', 'SwitchList').trim(),
        cellList: readString(item, 'cellList', 'CellList').trim(),
        signalList: readString(item, 'signalList', 'SignalList').trim(),
        allowanceTags: readString(item, 'allowanceTags', 'AllowanceTags').trim(),
        forbiddenTags: readString(item, 'forbiddenTags', 'ForbiddenTags').trim(),
        startNodeID: readString(item, 'startNodeID', 'StartNodeID').trim(),
        endNodeID: readString(item, 'endNodeID', 'EndNodeID').trim(),
    }
}

function cloneStationRoute(route: StationRoute): StationRoute {
    return { ...route }
}

function syncStationRouteFormScope() {
    routeForm.value.instanceID = selectedInstanceId.value
    routeForm.value.stationSchemeID = currentStationSchemeId.value.trim()
}

function clearRouteSearchCandidates() {
    routeSearchCandidates.value = []
    selectedRouteCandidateIndex.value = -1
    routeSearchDialogVisible.value = false
}

function clearStationRoutes() {
    routeLoadVersion++
    stationRoutes.value = []
    selectedRouteId.value = ''
    routeOriginalId.value = ''
    routeEditMode.value = 'none'
    routeNodePickStage.value = 'none'
    routeSearchLoading.value = false
    routeForm.value = createEmptyStationRouteForm()
    clearRouteSearchCandidates()
}

function selectStationRoute(row: StationRoute) {
    selectedRouteId.value = row.id
    routeOriginalId.value = row.id
    routeEditMode.value = 'edit'
    routeNodePickStage.value = 'none'
    clearRouteSearchCandidates()
    routeForm.value = cloneStationRoute(row)
    syncStationRouteFormScope()
}

function selectStationRouteById(id: string) {
    const row = stationRoutes.value.find((item) => item.id === id)
    if (row) {
        selectStationRoute(row)
        return
    }

    const firstRoute = stationRoutes.value[0]
    if (firstRoute) {
        selectStationRoute(firstRoute)
        return
    }

    selectedRouteId.value = ''
    routeOriginalId.value = ''
    routeEditMode.value = 'none'
    routeNodePickStage.value = 'none'
    routeForm.value = createEmptyStationRouteForm()
}

function createEmptyRouteEndForm(bindingNodeID = ''): StationRouteEnd {
    return {
        instanceID: selectedInstanceId.value,
        stationSchemeID: currentStationSchemeId.value.trim(),
        id: '',
        bindingNodeID,
        type: '',
        segmentTag: '',
        sidingTag: '',
    }
}

function normalizeStationRouteEnd(item: any): StationRouteEnd | null {
    const id = readString(item, 'id', 'ID').trim()
    if (!id) return null

    return {
        instanceID: readString(item, 'instanceID', 'InstanceID').trim(),
        stationSchemeID: readString(item, 'stationSchemeID', 'StationSchemeID').trim(),
        id,
        bindingNodeID: readString(item, 'bindingNodeID', 'BindingNodeID').trim(),
        type: readString(item, 'type', 'Type').trim(),
        segmentTag: readString(item, 'segmentTag', 'SegmentTag').trim(),
        sidingTag: readString(item, 'sidingTag', 'SidingTag').trim(),
    }
}

function cloneRouteEnd(routeEnd: StationRouteEnd): StationRouteEnd {
    return { ...routeEnd }
}

function syncRouteEndFormScope() {
    routeEndForm.value.instanceID = selectedInstanceId.value
    routeEndForm.value.stationSchemeID = currentStationSchemeId.value.trim()
}

function clearRouteEnds() {
    routeEndLoadVersion++
    routeEnds.value = []
    selectedRouteEndId.value = ''
    routeEndOriginalId.value = ''
    routeEndEditMode.value = 'none'
    routeEndPickingNode.value = false
    routeEndForm.value = createEmptyRouteEndForm()
}

function selectRouteEnd(row: StationRouteEnd) {
    selectedRouteEndId.value = row.id
    routeEndOriginalId.value = row.id
    routeEndEditMode.value = 'edit'
    routeEndPickingNode.value = false
    routeEndForm.value = cloneRouteEnd(row)
    syncRouteEndFormScope()
}

function selectRouteEndById(id: string) {
    const row = routeEnds.value.find((item) => item.id === id)
    if (row) {
        selectRouteEnd(row)
        return
    }

    const firstRouteEnd = routeEnds.value[0]
    if (firstRouteEnd) {
        selectRouteEnd(firstRouteEnd)
        return
    }

    selectedRouteEndId.value = ''
    routeEndOriginalId.value = ''
    routeEndEditMode.value = 'none'
    routeEndPickingNode.value = false
    routeEndForm.value = createEmptyRouteEndForm()
}

function getHttpErrorMessage(error: any, fallback: string): string {
    const data = error?.response?.data
    if (typeof data === 'string' && data.trim()) return data
    return error?.message || fallback
}

async function loadStationSchemes(options: { includeCurrent?: boolean } = {}) {
    const includeCurrent = options.includeCurrent !== false
    const instanceID = selectedInstanceId.value
    if (!instanceID) {
        stationSchemeLoadVersion++
        currentStationSchemeId.value = ''
        stationSchemeOptions.value = []
        loadingStationSchemes.value = false
        return []
    }

    const loadVersion = ++stationSchemeLoadVersion
    loadingStationSchemes.value = true
    try {
        const response = await axios.get('/StationLayout/GetStationSchemes', {
            params: { instanceID },
        })
        if (loadVersion !== stationSchemeLoadVersion || instanceID !== selectedInstanceId.value) return []

        const options = (Array.isArray(response.data) ? response.data : [])
            .map((item: any) => normalizeStationSchemeOption(item))
            .filter((item: StationSchemeOption | null): item is StationSchemeOption => item !== null)
        setStationSchemeOptions(options, includeCurrent)
        return options
    } catch (error) {
        if (loadVersion !== stationSchemeLoadVersion || instanceID !== selectedInstanceId.value) return []

        console.error('Failed to load route design station schemes:', error)
        stationSchemeOptions.value = []
        ElMessage.error(t('stationLayout.messages.loadSchemesFailed'))
        return []
    } finally {
        if (loadVersion === stationSchemeLoadVersion && instanceID === selectedInstanceId.value) {
            loadingStationSchemes.value = false
        }
    }
}

async function loadStationRoutes() {
    const instanceID = selectedInstanceId.value
    const stationSchemeID = currentStationSchemeId.value.trim()
    if (!instanceID || !stationSchemeID) {
        clearStationRoutes()
        return []
    }

    const loadVersion = ++routeLoadVersion
    loadingRoutes.value = true
    const previousSelectedId = selectedRouteId.value || routeForm.value.id.trim()
    try {
        const response = await axios.get('/StationLayout/GetStationRoutes', {
            params: { instanceID, stationSchemeID },
        })
        if (
            loadVersion !== routeLoadVersion ||
            instanceID !== selectedInstanceId.value ||
            stationSchemeID !== currentStationSchemeId.value.trim()
        ) {
            return []
        }

        stationRoutes.value = (Array.isArray(response.data) ? response.data : [])
            .map((item: any) => normalizeStationRoute(item))
            .filter((item: StationRoute | null): item is StationRoute => item !== null)
        selectStationRouteById(previousSelectedId)
        return stationRoutes.value
    } catch (error) {
        if (
            loadVersion !== routeLoadVersion ||
            instanceID !== selectedInstanceId.value ||
            stationSchemeID !== currentStationSchemeId.value.trim()
        ) {
            return []
        }

        console.error('Failed to load station routes:', error)
        stationRoutes.value = []
        selectStationRouteById('')
        ElMessage.error(t('routeDesign.stationRoute.messages.loadFailed'))
        return []
    } finally {
        if (
            loadVersion === routeLoadVersion &&
            instanceID === selectedInstanceId.value &&
            stationSchemeID === currentStationSchemeId.value.trim()
        ) {
            loadingRoutes.value = false
        }
    }
}

async function loadRouteEnds() {
    const instanceID = selectedInstanceId.value
    const stationSchemeID = currentStationSchemeId.value.trim()
    if (!instanceID || !stationSchemeID) {
        clearRouteEnds()
        return []
    }

    const loadVersion = ++routeEndLoadVersion
    loadingRouteEnds.value = true
    const previousSelectedId = selectedRouteEndId.value || routeEndForm.value.id.trim()
    try {
        const response = await axios.get('/StationLayout/GetStationRouteEnds', {
            params: { instanceID, stationSchemeID },
        })
        if (
            loadVersion !== routeEndLoadVersion ||
            instanceID !== selectedInstanceId.value ||
            stationSchemeID !== currentStationSchemeId.value.trim()
        ) {
            return []
        }

        routeEnds.value = (Array.isArray(response.data) ? response.data : [])
            .map((item: any) => normalizeStationRouteEnd(item))
            .filter((item: StationRouteEnd | null): item is StationRouteEnd => item !== null)
        selectRouteEndById(previousSelectedId)
        return routeEnds.value
    } catch (error) {
        if (
            loadVersion !== routeEndLoadVersion ||
            instanceID !== selectedInstanceId.value ||
            stationSchemeID !== currentStationSchemeId.value.trim()
        ) {
            return []
        }

        console.error('Failed to load station route ends:', error)
        routeEnds.value = []
        selectRouteEndById('')
        ElMessage.error(t('routeDesign.routeEnd.messages.loadFailed'))
        return []
    } finally {
        if (
            loadVersion === routeEndLoadVersion &&
            instanceID === selectedInstanceId.value &&
            stationSchemeID === currentStationSchemeId.value.trim()
        ) {
            loadingRouteEnds.value = false
        }
    }
}

function startCreateStationRoute() {
    if (!canEditRoutes.value) {
        ElMessage.warning(t('routeDesign.stationRoute.messages.selectScheme'))
        return
    }

    selectedRouteId.value = ''
    routeOriginalId.value = ''
    routeEditMode.value = 'create'
    routeNodePickStage.value = 'start'
    routeEndPickingNode.value = false
    routeForm.value = createEmptyStationRouteForm()
    clearRouteSearchCandidates()
    ElMessage.info(t('routeDesign.stationRoute.messages.pickStart'))
}

function startStationRouteNodePick(stage: Exclude<StationRouteNodePickStage, 'none'>) {
    if (!canEditRoutes.value) {
        ElMessage.warning(t('routeDesign.stationRoute.messages.selectScheme'))
        return
    }

    if (routeEditMode.value === 'none') {
        routeEditMode.value = 'create'
        routeForm.value = createEmptyStationRouteForm()
    } else {
        syncStationRouteFormScope()
    }

    routeEndPickingNode.value = false
    routeNodePickStage.value = stage
    ElMessage.info(t(stage === 'start'
        ? 'routeDesign.stationRoute.messages.pickStart'
        : 'routeDesign.stationRoute.messages.pickEnd'))
}

function setRouteSearchCandidates(candidates: StationRouteSearchCandidate[]) {
    routeSearchCandidates.value = candidates
    const firstCandidate = candidates[0]
    selectedRouteCandidateIndex.value = firstCandidate ? firstCandidate.index : -1
    routeSearchDialogVisible.value = true
}

async function searchStationRoutesForCreate() {
    const instanceID = selectedInstanceId.value
    const stationSchemeID = currentStationSchemeId.value.trim()
    const startNodeID = routeForm.value.startNodeID.trim()
    const endNodeID = routeForm.value.endNodeID.trim()
    if (!instanceID || !stationSchemeID) {
        ElMessage.warning(t('routeDesign.stationRoute.messages.selectScheme'))
        return
    }

    const startNodeNumber = Number(startNodeID)
    const endNodeNumber = Number(endNodeID)
    if (!Number.isInteger(startNodeNumber) || !Number.isInteger(endNodeNumber)) {
        ElMessage.warning(t('routeDesign.stationRoute.messages.nodeIdMustBeInteger'))
        return
    }

    routeSearchLoading.value = true
    clearRouteSearchCandidates()
    try {
        const response = await axios.post('/StationLayout/SearchRoutes', {
            instanceID,
            stationSchemeID,
            startNodeId: startNodeNumber,
            endNodeId: endNodeNumber,
        }, {
            params: { instanceID, stationSchemeID },
        })
        const routes = Array.isArray(response.data?.routes)
            ? response.data.routes
            : Array.isArray(response.data?.Routes)
                ? response.data.Routes
                : []
        const candidates = routes.map((route: any, index: number) => (
            normalizeStationRouteSearchCandidate(route, index, startNodeID, endNodeID)
        ))
        setRouteSearchCandidates(candidates)
        if (candidates.length > 0) {
            ElMessage.success(t('routeDesign.stationRoute.messages.searchSuccess', { count: candidates.length }))
        } else {
            ElMessage.warning(t('routeDesign.stationRoute.messages.searchEmpty'))
        }
    } catch (error) {
        console.error('Failed to search station routes:', error)
        clearRouteSearchCandidates()
        ElMessage.error(getHttpErrorMessage(error, t('routeDesign.stationRoute.messages.searchFailed')))
    } finally {
        routeSearchLoading.value = false
    }
}

function selectRouteSearchCandidate(row: StationRouteSearchCandidate) {
    selectedRouteCandidateIndex.value = row.index
}

function closeRouteSearchDialog() {
    routeSearchDialogVisible.value = false
}

function applySelectedRouteCandidate() {
    const candidate = selectedRouteCandidate.value
    if (!candidate) {
        ElMessage.warning(t('routeDesign.stationRoute.messages.selectCandidate'))
        return
    }

    routeEditMode.value = 'create'
    selectedRouteId.value = ''
    routeOriginalId.value = ''
    routeNodePickStage.value = 'none'
    routeForm.value = {
        ...createEmptyStationRouteForm(),
        id: routeForm.value.id,
        type: routeForm.value.type,
        startNodeID: candidate.startNodeID,
        endNodeID: candidate.endNodeID,
        nodeList: candidate.nodeList,
        linkList: candidate.linkList,
        switchList: candidate.switchList || routeForm.value.switchList,
        cellList: candidate.cellList || routeForm.value.cellList,
        signalList: candidate.signalList || routeForm.value.signalList,
        allowanceTags: routeForm.value.allowanceTags,
        forbiddenTags: routeForm.value.forbiddenTags,
    }
    syncStationRouteFormScope()
    routeSearchDialogVisible.value = false
    ElMessage.success(t('routeDesign.stationRoute.messages.candidateApplied'))
}

function buildStationRoutePayload() {
    syncStationRouteFormScope()
    return {
        instanceID: routeForm.value.instanceID.trim(),
        stationSchemeID: routeForm.value.stationSchemeID.trim(),
        originalID: routeOriginalId.value.trim(),
        id: routeForm.value.id.trim(),
        type: routeForm.value.type.trim(),
        nodeList: routeForm.value.nodeList.trim(),
        linkList: routeForm.value.linkList.trim(),
        switchList: routeForm.value.switchList.trim(),
        cellList: routeForm.value.cellList.trim(),
        signalList: routeForm.value.signalList.trim(),
        allowanceTags: routeForm.value.allowanceTags.trim(),
        forbiddenTags: routeForm.value.forbiddenTags.trim(),
        startNodeID: routeForm.value.startNodeID.trim(),
        endNodeID: routeForm.value.endNodeID.trim(),
    }
}

async function saveStationRoute() {
    if (!canEditRoutes.value) {
        ElMessage.warning(t('routeDesign.stationRoute.messages.selectScheme'))
        return
    }

    const payload = buildStationRoutePayload()
    if (!payload.startNodeID || !payload.endNodeID) {
        ElMessage.warning(t('routeDesign.stationRoute.messages.startEndRequired'))
        return
    }

    const isCreate = routeEditMode.value === 'create' || !routeOriginalId.value
    if (!isCreate && !payload.id) {
        ElMessage.warning(t('routeDesign.stationRoute.messages.idRequired'))
        return
    }

    savingRoute.value = true
    try {
        const response = isCreate
            ? await axios.post('/StationLayout/CreateStationRoute', payload)
            : await axios.put('/StationLayout/EditStationRoute', payload)
        const saved = normalizeStationRoute(response.data)
        ElMessage.success(t(isCreate
            ? 'routeDesign.stationRoute.messages.createSuccess'
            : 'routeDesign.stationRoute.messages.updateSuccess'))
        await loadStationRoutes()
        if (saved?.id) selectStationRouteById(saved.id)
    } catch (error) {
        console.error('Failed to save station route:', error)
        ElMessage.error(getHttpErrorMessage(
            error,
            t(routeEditMode.value === 'create'
                ? 'routeDesign.stationRoute.messages.createFailed'
                : 'routeDesign.stationRoute.messages.updateFailed')
        ))
    } finally {
        savingRoute.value = false
    }
}

function cancelStationRouteEdit() {
    routeNodePickStage.value = 'none'
    routeSearchDialogVisible.value = false
    if (selectedStationRoute.value) {
        selectStationRoute(selectedStationRoute.value)
        return
    }

    selectStationRouteById('')
}

async function deleteSelectedStationRoute() {
    const id = selectedRouteId.value
    if (!id || !canEditRoutes.value) return

    try {
        await ElMessageBox.confirm(
            t('routeDesign.stationRoute.messages.deleteConfirm', { id }),
            t('routeDesign.stationRoute.messages.deleteTitle'),
            {
                confirmButtonText: t('routeDesign.stationRoute.actions.delete'),
                cancelButtonText: t('routeDesign.stationRoute.actions.cancel'),
                type: 'warning',
            }
        )
    } catch {
        return
    }

    savingRoute.value = true
    try {
        await axios.delete('/StationLayout/DeleteStationRoute', {
            params: {
                instanceID: selectedInstanceId.value,
                stationSchemeID: currentStationSchemeId.value.trim(),
                id,
            },
        })
        ElMessage.success(t('routeDesign.stationRoute.messages.deleteSuccess'))
        selectedRouteId.value = ''
        routeOriginalId.value = ''
        await loadStationRoutes()
    } catch (error) {
        console.error('Failed to delete station route:', error)
        ElMessage.error(getHttpErrorMessage(error, t('routeDesign.stationRoute.messages.deleteFailed')))
    } finally {
        savingRoute.value = false
    }
}

function startCreateRouteEnd() {
    if (!canEditRouteEnds.value) {
        ElMessage.warning(t('routeDesign.routeEnd.messages.selectScheme'))
        return
    }

    selectedRouteEndId.value = ''
    routeEndOriginalId.value = ''
    routeEndEditMode.value = 'create'
    routeEndPickingNode.value = true
    routeNodePickStage.value = 'none'
    routeSearchDialogVisible.value = false
    routeEndForm.value = createEmptyRouteEndForm()
    ElMessage.info(t('routeDesign.routeEnd.messages.pickNode'))
}

function startRouteEndNodePick() {
    if (!canEditRouteEnds.value) {
        ElMessage.warning(t('routeDesign.routeEnd.messages.selectScheme'))
        return
    }

    if (routeEndEditMode.value === 'none') {
        routeEndEditMode.value = 'create'
        routeEndForm.value = createEmptyRouteEndForm()
    } else {
        syncRouteEndFormScope()
    }

    routeEndPickingNode.value = true
    routeNodePickStage.value = 'none'
    routeSearchDialogVisible.value = false
    ElMessage.info(t('routeDesign.routeEnd.messages.pickNode'))
}

async function handleRouteNodePick(payload: RouteNodePickPayload) {
    const nodeId = readString(payload, 'nodeId', 'nodeID').trim()
    if (!nodeId) return

    if (routeNodePickStage.value !== 'none' && payload?.target === routeNodePickTarget.value) {
        if (routeNodePickStage.value === 'start') {
            routeForm.value.startNodeID = nodeId
            routeForm.value.nodeList = ''
            routeForm.value.linkList = ''
            syncStationRouteFormScope()
            routeNodePickStage.value = 'end'
            ElMessage.success(t('routeDesign.stationRoute.messages.startPicked', { nodeId }))
            ElMessage.info(t('routeDesign.stationRoute.messages.pickEnd'))
            return
        }

        routeForm.value.endNodeID = nodeId
        syncStationRouteFormScope()
        routeNodePickStage.value = 'none'
        ElMessage.success(t('routeDesign.stationRoute.messages.endPicked', { nodeId }))
        await searchStationRoutesForCreate()
        return
    }

    if (!routeEndPickingNode.value || payload?.target !== routeEndPickTarget.value) return

    routeEndForm.value.bindingNodeID = nodeId
    syncRouteEndFormScope()
    routeEndPickingNode.value = false
    ElMessage.success(t('routeDesign.routeEnd.messages.nodePicked', { nodeId }))
}

function buildRouteEndPayload() {
    syncRouteEndFormScope()
    return {
        instanceID: routeEndForm.value.instanceID.trim(),
        stationSchemeID: routeEndForm.value.stationSchemeID.trim(),
        originalID: routeEndOriginalId.value.trim(),
        id: routeEndForm.value.id.trim(),
        bindingNodeID: routeEndForm.value.bindingNodeID.trim(),
        type: routeEndForm.value.type.trim(),
        segmentTag: routeEndForm.value.segmentTag.trim(),
        sidingTag: routeEndForm.value.sidingTag.trim(),
    }
}

async function saveRouteEnd() {
    if (!canEditRouteEnds.value) {
        ElMessage.warning(t('routeDesign.routeEnd.messages.selectScheme'))
        return
    }

    const payload = buildRouteEndPayload()
    if (!payload.bindingNodeID) {
        ElMessage.warning(t('routeDesign.routeEnd.messages.bindingNodeRequired'))
        return
    }

    const isCreate = routeEndEditMode.value === 'create' || !routeEndOriginalId.value
    if (!isCreate && !payload.id) {
        ElMessage.warning(t('routeDesign.routeEnd.messages.idRequired'))
        return
    }

    savingRouteEnd.value = true
    try {
        const response = isCreate
            ? await axios.post('/StationLayout/CreateStationRouteEnd', payload)
            : await axios.put('/StationLayout/EditStationRouteEnd', payload)
        const saved = normalizeStationRouteEnd(response.data)
        ElMessage.success(t(isCreate
            ? 'routeDesign.routeEnd.messages.createSuccess'
            : 'routeDesign.routeEnd.messages.updateSuccess'))
        await loadRouteEnds()
        if (saved?.id) selectRouteEndById(saved.id)
    } catch (error) {
        console.error('Failed to save station route end:', error)
        ElMessage.error(getHttpErrorMessage(
            error,
            t(routeEndEditMode.value === 'create'
                ? 'routeDesign.routeEnd.messages.createFailed'
                : 'routeDesign.routeEnd.messages.updateFailed')
        ))
    } finally {
        savingRouteEnd.value = false
    }
}

function cancelRouteEndEdit() {
    routeEndPickingNode.value = false
    if (selectedRouteEnd.value) {
        selectRouteEnd(selectedRouteEnd.value)
        return
    }

    selectRouteEndById('')
}

async function deleteSelectedRouteEnd() {
    const id = selectedRouteEndId.value
    if (!id || !canEditRouteEnds.value) return

    try {
        await ElMessageBox.confirm(
            t('routeDesign.routeEnd.messages.deleteConfirm', { id }),
            t('routeDesign.routeEnd.messages.deleteTitle'),
            {
                confirmButtonText: t('routeDesign.routeEnd.actions.delete'),
                cancelButtonText: t('routeDesign.routeEnd.actions.cancel'),
                type: 'warning',
            }
        )
    } catch {
        return
    }

    savingRouteEnd.value = true
    try {
        await axios.delete('/StationLayout/DeleteStationRouteEnd', {
            params: {
                instanceID: selectedInstanceId.value,
                stationSchemeID: currentStationSchemeId.value.trim(),
                id,
            },
        })
        ElMessage.success(t('routeDesign.routeEnd.messages.deleteSuccess'))
        selectedRouteEndId.value = ''
        routeEndOriginalId.value = ''
        await loadRouteEnds()
    } catch (error) {
        console.error('Failed to delete station route end:', error)
        ElMessage.error(getHttpErrorMessage(error, t('routeDesign.routeEnd.messages.deleteFailed')))
    } finally {
        savingRouteEnd.value = false
    }
}

function getLayoutDisplayStyles(layoutData: any): Record<string, unknown> {
    const styles = layoutData?.metadata?.displayStyles
    return styles && typeof styles === 'object' && !Array.isArray(styles) ? styles : {}
}

function clearLayout() {
    layoutDisplayStyles.value = {}
    stationLayoutEditorRef.value?.clearElements()
}

async function loadLayout() {
    const instanceID = selectedInstanceId.value
    const stationSchemeID = currentStationSchemeId.value.trim()

    if (!instanceID) {
        layoutLoadVersion++
        clearLayout()
        return
    }

    const loadVersion = ++layoutLoadVersion
    loadingData.value = true
    try {
        const params: Record<string, string> = { instanceID }
        if (stationSchemeID) params.stationSchemeID = stationSchemeID

        const response = await axios.post('/StationLayout/GetJson', null, {
            params,
        })
        if (loadVersion !== layoutLoadVersion || instanceID !== selectedInstanceId.value) return

        const resolvedStationSchemeId = readString(response.data?.metadata, 'stationSchemeID', 'StationSchemeID').trim()
        if (resolvedStationSchemeId) {
            currentStationSchemeId.value = resolvedStationSchemeId
            ensureCurrentStationSchemeOption()
        }

        layoutDisplayStyles.value = getLayoutDisplayStyles(response.data)
        await nextTick()
        stationLayoutEditorRef.value?.loadDataFromJson(response.data)
    } catch (error) {
        if (loadVersion !== layoutLoadVersion || instanceID !== selectedInstanceId.value) return

        console.error('Failed to load route design layout:', error)
        clearLayout()
        ElMessage.error(t('routeDesign.messages.loadFailed'))
    } finally {
        if (loadVersion === layoutLoadVersion && instanceID === selectedInstanceId.value) {
            loadingData.value = false
        }
    }
}

async function refreshForInstance() {
    await loadStationSchemes()
    await loadLayout()
    await loadStationRoutes()
    await loadRouteEnds()
}

async function handleStationSchemeChange() {
    routeNodePickStage.value = 'none'
    routeSearchDialogVisible.value = false
    routeEndPickingNode.value = false
    await loadLayout()
    await loadStationRoutes()
    await loadRouteEnds()
}

function getPaneLimits(containerWidth: number) {
    const compact = containerWidth < 700
    return {
        minLeft: compact ? 240 : 360,
        minRight: compact ? 220 : 300,
        resizerWidth: 8,
    }
}

function clampLeftPaneWidth(width: number) {
    const containerWidth = splitContainerRef.value?.clientWidth || 0
    if (containerWidth <= 0) return Math.max(240, width)

    const { minLeft, minRight, resizerWidth } = getPaneLimits(containerWidth)
    const maxLeft = Math.max(minLeft, containerWidth - minRight - resizerWidth)
    return Math.min(maxLeft, Math.max(minLeft, width))
}

function ensureSplitWidth() {
    const containerWidth = splitContainerRef.value?.clientWidth || 0
    if (containerWidth <= 0) return

    const targetWidth = leftPaneWidth.value > 0
        ? leftPaneWidth.value
        : Math.round(containerWidth * 0.64)
    leftPaneWidth.value = clampLeftPaneWidth(targetWidth)
}

function onResizeMouseMove(event: MouseEvent) {
    if (!isResizing.value) return

    const rect = splitContainerRef.value?.getBoundingClientRect()
    if (!rect) return

    leftPaneWidth.value = clampLeftPaneWidth(event.clientX - rect.left)
}

function finishResize() {
    if (!isResizing.value) return

    isResizing.value = false
    window.removeEventListener('mousemove', onResizeMouseMove)
    window.removeEventListener('mouseup', finishResize)
    document.body.style.cursor = previousBodyCursor
    document.body.style.userSelect = previousBodyUserSelect
}

function startResize(event: MouseEvent) {
    event.preventDefault()
    isResizing.value = true
    previousBodyCursor = document.body.style.cursor
    previousBodyUserSelect = document.body.style.userSelect
    document.body.style.cursor = 'col-resize'
    document.body.style.userSelect = 'none'
    window.addEventListener('mousemove', onResizeMouseMove)
    window.addEventListener('mouseup', finishResize)
}

function resetSplit() {
    const containerWidth = splitContainerRef.value?.clientWidth || 0
    if (containerWidth <= 0) return

    leftPaneWidth.value = clampLeftPaneWidth(Math.round(containerWidth * 0.64))
}

watch(() => props.selectedInstanceId, () => {
    currentStationSchemeId.value = ''
    stationSchemeOptions.value = []
    clearStationRoutes()
    clearRouteEnds()
    void refreshForInstance()
}, { immediate: true })

watch(showStationRouteCard, (visible) => {
    if (visible) return

    routeNodePickStage.value = 'none'
    routeSearchDialogVisible.value = false
})

watch(showRouteEndCard, (visible) => {
    if (visible) return

    routeEndPickingNode.value = false
})

onMounted(() => {
    nextTick(() => {
        ensureSplitWidth()
        if (typeof ResizeObserver !== 'undefined' && splitContainerRef.value) {
            resizeObserver = new ResizeObserver(() => ensureSplitWidth())
            resizeObserver.observe(splitContainerRef.value)
        } else {
            window.addEventListener('resize', ensureSplitWidth)
        }
    })
})

onBeforeUnmount(() => {
    finishResize()
    resizeObserver?.disconnect()
    window.removeEventListener('resize', ensureSplitWidth)
})
</script>

<style scoped>
.route-design-page {
    display: flex;
    flex-direction: column;
    width: 100%;
    height: calc(100dvh - 118px);
    min-height: 420px;
    border: 1px solid #d8e2ef;
    background: #ffffff;
    overflow: hidden;
}

.route-design-toolbar {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 12px;
    flex: 0 0 auto;
    min-height: 40px;
    padding: 6px 10px;
    border-bottom: 1px solid #d8e2ef;
    background: #f7fafc;
}

.route-design-toolbar-left,
.route-design-toolbar-right,
.route-design-scheme-control,
.route-design-switch-control {
    display: inline-flex;
    align-items: center;
    min-width: 0;
}

.route-design-toolbar-left {
    flex: 1 1 auto;
}

.route-design-toolbar-right {
    justify-content: flex-end;
    gap: 14px;
    flex: 0 0 auto;
}

.route-design-scheme-control {
    gap: 6px;
}

.route-design-switch-control {
    gap: 6px;
    white-space: nowrap;
}

.route-design-control-label {
    flex: 0 0 auto;
    color: #4c5968;
    font-size: 12px;
    line-height: 1;
    white-space: nowrap;
}

.route-design-scheme-select {
    width: 220px;
}

.route-design-body {
    display: flex;
    align-items: stretch;
    flex: 1 1 auto;
    min-height: 0;
    min-width: 0;
    overflow: hidden;
    background: #ffffff;
}

.route-design-editor-pane {
    position: relative;
    flex: 0 0 auto;
    min-width: 240px;
    height: 100%;
    min-height: 0;
    overflow: hidden;
    background: #31363f;
}

.route-design-editor-scroll {
    width: 100%;
    height: 100%;
    overflow: auto;
    background: #31363f;
}

.route-design-resizer {
    position: relative;
    flex: 0 0 8px;
    width: 8px;
    background: #dbe5f0;
    cursor: col-resize;
}

.route-design-resizer::before {
    content: "";
    position: absolute;
    top: 0;
    bottom: 0;
    left: 3px;
    width: 2px;
    background: #a9b8ca;
}

.route-design-resizer:hover,
.route-design-body.is-resizing .route-design-resizer {
    background: #c7d8ea;
}

.route-design-data-pane {
    display: flex;
    flex-direction: row;
    gap: 10px;
    flex: 1 1 auto;
    min-width: 220px;
    min-height: 0;
    padding: 10px;
    background: #f6f9fc;
    overflow: hidden;
}

.route-design-data-pane.is-single-card {
    gap: 0;
}

.route-search-result-popover {
    position: absolute;
    left: 14px;
    bottom: 14px;
    z-index: 10;
    display: flex;
    flex-direction: column;
    width: min(520px, calc(100% - 28px));
    max-height: min(380px, calc(100% - 28px));
    border: 1px solid #cfdbea;
    border-radius: 8px;
    background: #ffffff;
    box-shadow: 0 10px 28px rgba(15, 23, 42, 0.22);
    overflow: hidden;
}

.route-search-result-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 10px;
    flex: 0 0 auto;
    padding: 9px 10px;
    border-bottom: 1px solid #e1e8f0;
    background: #fbfdff;
}

.route-search-result-header h3 {
    margin: 0;
    color: #1f2d3d;
    font-size: 14px;
    font-weight: 650;
    line-height: 1.2;
}

.route-search-result-header span {
    display: block;
    margin-top: 2px;
    color: #718096;
    font-size: 12px;
    line-height: 1.2;
}

.route-search-result-actions {
    display: flex;
    justify-content: flex-end;
    gap: 8px;
    flex: 0 0 auto;
    padding: 8px 10px;
    border-top: 1px solid #e1e8f0;
    background: #ffffff;
}

.station-route-card {
    display: flex;
    flex-direction: column;
    flex: 1 1 0;
    min-height: 0;
    min-width: 0;
    border: 1px solid #d7e2ee;
    border-radius: 8px;
    background: #ffffff;
    overflow: hidden;
}

.station-route-card-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 8px;
    flex: 0 0 auto;
    min-height: 48px;
    padding: 8px 10px;
    border-bottom: 1px solid #e1e8f0;
    background: #fbfdff;
}

.station-route-title-group {
    display: flex;
    flex-direction: column;
    gap: 3px;
    min-width: 0;
}

.station-route-title-group h2 {
    margin: 0;
    color: #1f2d3d;
    font-size: 15px;
    font-weight: 650;
    line-height: 1.2;
}

.station-route-subtitle {
    color: #718096;
    font-size: 12px;
    line-height: 1.2;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
}

.station-route-card-actions {
    display: inline-flex;
    align-items: center;
    gap: 6px;
    flex: 0 0 auto;
}

.station-route-table-wrap {
    flex: 0 0 32%;
    min-height: 118px;
    border-bottom: 1px solid #e1e8f0;
    overflow: hidden;
}

.station-route-table-wrap :deep(.el-table) {
    font-size: 12px;
}

.station-route-form-panel {
    display: flex;
    flex-direction: column;
    flex: 1 1 auto;
    min-height: 0;
    padding: 10px;
    overflow: auto;
}

.station-route-form-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 8px;
    flex: 0 0 auto;
    margin-bottom: 8px;
}

.station-route-form-title {
    color: #35465a;
    font-size: 13px;
    font-weight: 650;
    line-height: 1.2;
}

.station-route-form {
    display: grid;
    grid-template-columns: minmax(0, 1fr);
    gap: 0;
}

.station-route-form :deep(.el-form-item) {
    margin-bottom: 9px;
}

.station-route-form :deep(.el-form-item__label) {
    margin-bottom: 3px;
    color: #536273;
    font-size: 12px;
    line-height: 1.2;
}

.station-route-node-control {
    display: grid;
    grid-template-columns: minmax(0, 1fr) 32px;
    gap: 6px;
    width: 100%;
}

.station-route-full-control {
    width: 100%;
}

.station-route-form-actions {
    display: flex;
    align-items: center;
    gap: 8px;
    flex: 0 0 auto;
    padding-top: 4px;
    border-top: 1px solid #edf2f7;
    flex-wrap: wrap;
}

.route-end-card {
    display: flex;
    flex-direction: column;
    flex: 1 1 0;
    height: auto;
    min-height: 0;
    min-width: 0;
    border: 1px solid #d7e2ee;
    border-radius: 8px;
    background: #ffffff;
    overflow: hidden;
}

.route-end-card-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 8px;
    flex: 0 0 auto;
    min-height: 48px;
    padding: 8px 10px;
    border-bottom: 1px solid #e1e8f0;
    background: #fbfdff;
}

.route-end-title-group {
    display: flex;
    flex-direction: column;
    gap: 3px;
    min-width: 0;
}

.route-end-title-group h2 {
    margin: 0;
    color: #1f2d3d;
    font-size: 15px;
    font-weight: 650;
    line-height: 1.2;
}

.route-end-subtitle {
    color: #718096;
    font-size: 12px;
    line-height: 1.2;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
}

.route-end-card-actions {
    display: inline-flex;
    align-items: center;
    gap: 6px;
    flex: 0 0 auto;
}

.route-end-table-wrap {
    flex: 0 0 32%;
    min-height: 118px;
    border-bottom: 1px solid #e1e8f0;
    overflow: hidden;
}

.route-end-table-wrap :deep(.el-table) {
    font-size: 12px;
}

.route-end-form-panel {
    display: flex;
    flex-direction: column;
    flex: 1 1 auto;
    min-height: 0;
    padding: 10px;
    overflow: auto;
}

.route-end-form-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 8px;
    flex: 0 0 auto;
    margin-bottom: 8px;
}

.route-end-form-title {
    color: #35465a;
    font-size: 13px;
    font-weight: 650;
    line-height: 1.2;
}

.route-end-form {
    display: grid;
    grid-template-columns: minmax(0, 1fr);
    gap: 0;
}

.route-end-form :deep(.el-form-item) {
    margin-bottom: 9px;
}

.route-end-form :deep(.el-form-item__label) {
    margin-bottom: 3px;
    color: #536273;
    font-size: 12px;
    line-height: 1.2;
}

.route-end-binding-control {
    display: grid;
    grid-template-columns: minmax(0, 1fr) 32px;
    gap: 6px;
    width: 100%;
}

.route-end-full-control {
    width: 100%;
}

.route-end-form-actions {
    display: flex;
    align-items: center;
    gap: 8px;
    flex: 0 0 auto;
    padding-top: 4px;
    border-top: 1px solid #edf2f7;
    flex-wrap: wrap;
}

@media (max-width: 768px) {
    .route-design-page {
        height: calc(100dvh - 188px);
    }

    .route-design-toolbar {
        align-items: flex-start;
        flex-direction: column;
    }

    .route-design-toolbar-left,
    .route-design-toolbar-right {
        width: 100%;
    }

    .route-design-toolbar-right {
        justify-content: flex-start;
        flex-wrap: wrap;
    }

    .route-design-scheme-select {
        width: min(220px, 56vw);
    }

    .route-design-data-pane {
        padding: 8px;
        gap: 8px;
        overflow-x: auto;
    }

    .station-route-card,
    .route-end-card {
        min-width: 260px;
    }

    .station-route-table-wrap,
    .route-end-table-wrap {
        flex-basis: 30%;
        min-height: 104px;
    }

    .route-search-result-popover {
        left: 8px;
        bottom: 8px;
        width: calc(100% - 16px);
    }
}
</style>
