<template>
    <section class="process-editor" v-loading="loading">
        <div class="process-header">
            <div class="process-template-actions">
                <el-select :model-value="current?.id" size="small" :aria-label="ui('作业过程模板', 'Operation process template')" :title="current?.name" :placeholder="ui('选择作业模板', 'Select a template')" filterable :disabled="editingLocked" @change="selectTemplate">
                    <el-option v-for="item in templates" :key="item.id" :value="item.id" :label="`${item.name || ui('未命名模板', 'Untitled template')}${isUnsaved(item) ? ui(' · 草稿', ' · Draft') : ''}`" />
                </el-select>
                <span v-if="current" :class="['process-save-state', { dirty }]">{{ dirty ? ui('有未保存的修改', 'Unsaved changes') : ui('已保存', 'Saved') }}</span>
                <ActionButton :icon="Plus" :disabled="editingLocked || !hasScope" @click="newTemplate" :label="ui('新建', 'New template')" />
                <ActionButton :icon="DocumentCopy" :disabled="editingLocked || !hasScope" @click="loadExample" :label="ui('加载测试案例', 'Load example')" />
                <ActionButton type="primary" :icon="Check" :loading="saving" :disabled="loading || connecting || !current" @click="saveTemplate" :label="ui('保存模板', 'Save template')" />
                <ActionButton :icon="Delete" :disabled="editingLocked || !current" @click="deleteTemplate" :label="ui('删除模板', 'Delete template')" />
            </div>
            <el-tag v-if="preview" size="small" effect="plain" type="info">{{ ui('演示模式', 'Demo') }}</el-tag>
        </div>

        <el-alert v-if="loadError" :title="loadError" type="error" show-icon :closable="false" class="process-alert">
            <ActionButton variant="icon-text" type="primary" :disabled="editingLocked" @click="loadScope" :icon="Refresh" :label="ui('重新加载', 'Reload')" />
        </el-alert>
        <el-alert v-if="conflictMessage" :title="conflictMessage" type="warning" show-icon :closable="false" class="process-alert">
            <div class="process-recovery-actions">
                <ActionButton variant="text" type="primary" :disabled="editingLocked" @click="saveConflictCopy" :icon="DocumentCopy" :label="ui('保留修改并另存为新模板', 'Save changes as a new template')" />
                <ActionButton variant="text" :disabled="editingLocked" @click="reloadConflictingTemplate" :icon="Refresh" :label="ui('放弃本地修改并重新加载', 'Discard changes and reload')" />
            </div>
        </el-alert>
        <el-alert v-if="preview" :title="ui('这是可交互的测试案例：可拖动活动、点击图形并修改右侧属性；保存仅保留在本次演示中。', 'Interactive demo. Drag activities and select objects to edit. Saves last for this session only.')" type="info" show-icon :closable="false" class="process-alert" />
        <el-alert v-if="trackCleanupNotice" :title="ui('已从草稿中移除未命名或已不存在的备选轨道，请保存需要保留此修正的模板。', 'Removed unnamed or missing tracks from drafts. Save a template to keep these corrections.')" type="info" show-icon :closable="false" class="process-alert" />

        <template v-if="current">
            <div class="process-workspace" :class="{ 'process-list-hidden': !showObjectList }" :style="{ '--inspector-width': `${inspectorWidth}px`, '--object-list-width': `${objectListWidth}px` }">
                <div class="process-main">
                    <div class="process-canvas-toolbar">
                        <div class="process-add-actions">
                            <el-dropdown trigger="click" :disabled="editingLocked" @command="addActivity">
                                <ActionButton type="primary" :icon="Plus" :disabled="editingLocked" :label="ui('添加活动', 'Add activity')" />
                                <template #dropdown><el-dropdown-menu><el-dropdown-item v-for="type in activityTypes" :key="type" :command="type">{{ activityLabel(type) }}</el-dropdown-item></el-dropdown-menu></template>
                            </el-dropdown>
                            <ActionButton :disabled="editingLocked" @click="addEvent" :icon="CirclePlus" :label="ui('添加事件', 'Add event')" />
                            <ActionButton :icon="Connection" :type="connecting ? 'primary' : 'default'" :disabled="busy || (!connecting && !canAddPrecedence)" @click="connecting ? cancelPrecedence() : addPrecedence()" :label="connecting ? ui('取消添加次序', 'Cancel precedence') : ui('添加次序', 'Add precedence')" />
                            <ActionButton :icon="Location" :disabled="editingLocked" @click="addAnchor" :label="ui('添加锚', 'Add anchor')" />
                        </div>
                        <div class="process-view-actions">
                            <div class="process-zoom"><ActionButton :icon="ZoomOut" :disabled="zoom <= 0.6" @click="zoom = Math.max(0.6, zoom - 0.1)" :label="ui('缩小画布', 'Zoom out')" /><span>{{ Math.round(zoom * 100) }}%</span><ActionButton :icon="ZoomIn" :disabled="zoom >= 1.4" @click="zoom = Math.min(1.4, zoom + 0.1)" :label="ui('放大画布', 'Zoom in')" /></div>
                            <ActionButton :icon="List" :type="showObjectList ? 'primary' : 'default'" :aria-expanded="showObjectList" :aria-controls="`${canvasID}-objects`" @click="showObjectList = !showObjectList" :label="showObjectList ? ui('隐藏对象列表', 'Hide objects') : ui('显示对象列表', 'Show objects')" />
                        </div>
                    </div>
                    <div v-if="connecting" class="process-connection-status" role="status" aria-live="polite">
                        <span v-if="!precedenceSource"><b>1 / 2</b> {{ ui('选择亮起的结束事件。', 'Select a highlighted end event.') }}</span>
                        <span v-else><b>2 / 2</b> {{ eventName(precedenceSource.eventID) }} · {{ ui('选择后序活动的开始事件。', 'Select the start event of the following activity.') }}</span>
                        <ActionButton variant="text" @click="cancelPrecedence" :icon="Close" :label="ui('取消（Esc）', 'Cancel (Esc)')" />
                    </div>
                    <div class="process-canvas-scroll" :class="{ 'process-is-busy': busy, 'process-is-connecting': connecting }">
                        <ProcessDiagram ref="canvasRef" :model="current" :catalog="catalog" :canvas-id="canvasID" :zoom="zoom"
                            :selection="selection" :busy="busy" :connecting="connecting" :precedence-source="precedenceSource" :precedence-target="precedenceTarget" :candidate-activities="candidateActivities"
                            @select-object="selectObject" @start-drag="startDrag" @move-activity="moveActivity" @end-drag="endDrag"
                            @activate-event="activateEvent" @preview-precedence="previewPrecedence" @clear-precedence-preview="clearPrecedencePreview" />
                    </div>
                    <div class="process-legend"><span v-for="type in activityTypes" :key="type"><i :style="{ background: typeColors[type].ink }" />{{ activityLabel(type) }}</span><span class="process-legend-note">{{ ui('○ 事件　→ 次序　⚓ 锚', '○ Event · → Precedence · Anchor') }}</span></div>

                </div>

                <PaneDivider v-model="inspectorWidth" reverse :min="250" :max="520" :label="ui('属性', 'Properties')" />
                <aside class="process-inspector">
                    <div class="process-inspector-header"><div><h3>{{ inspectorTitle }}</h3></div><ActionButton v-if="selection?.kind !== 'template'" :icon="Close" @click="selectObject('template', current.id)" :label="ui('关闭对象属性', 'Close object properties')" /></div>
                    <div class="process-inspector-body">
                        <fieldset :disabled="editingLocked" class="process-fieldset">
                            <el-form label-position="top" size="small" :disabled="editingLocked">
                                <template v-if="selectedActivity">
                                    <el-form-item :label="ui('活动名称', 'Activity name')"><el-input :model-value="selectedActivity.name" maxlength="100" @update:model-value="changeActivityName" /></el-form-item>
                                    <el-form-item :label="ui('活动类型', 'Activity type')"><el-select :model-value="selectedActivity.type" @change="changeActivityType"><el-option v-for="type in activityTypes" :key="type" :label="activityLabel(type)" :value="type" /></el-select></el-form-item>

                                    <div class="process-form-pair"><el-form-item :label="ui('最小持续时间（分钟）', 'Minimum duration (min)')"><el-input-number v-model="selectedActivity.minDuration" :min="0" :precision="2" controls-position="right" /></el-form-item><el-form-item :label="ui('最大持续时间（分钟）', 'Maximum duration (min)')"><el-input-number v-model="selectedActivity.maxDuration" :min="0" :precision="2" controls-position="right" /></el-form-item></div>
                                    <el-form-item :label="ui('开始事件', 'Start event')"><el-input :model-value="eventName(selectedActivity.startEvent)" readonly /></el-form-item>
                                    <el-form-item :label="ui('结束事件', 'End event')"><el-input :model-value="eventName(selectedActivity.endEvent)" readonly /></el-form-item>

                                    <template v-if="selectedActivity.type === 'Dwelling'">
                                        <div class="process-section-label">{{ ui('停留轨道', 'Dwelling tracks') }}</div>
                                        <el-form-item :label="ui('备选轨道 TrackList', 'Candidate tracks')"><el-select v-model="selectedActivity.trackList" multiple filterable @change="syncSelectedTrack"><el-option v-for="track in dwellingTracks" :key="track.id" :label="track.name" :value="track.id" /></el-select></el-form-item>

                                    </template>
                                    <template v-else>
                                        <div class="process-section-label">{{ ui('移动进路', 'Movement routes') }}</div>
                                        <el-form-item :label="ui('备选进路 RouteList', 'Candidate routes')"><el-select v-model="selectedActivity.routeList" multiple filterable @change="syncSelectedRoute"><el-option v-for="route in compatibleRoutes" :key="route.id" :label="route.name" :value="route.id" /></el-select></el-form-item>
                                        <template v-if="availableRoutes.length">
                                            <div class="process-section-label">{{ ui('进路端点锚', 'Route endpoint anchors') }} <span>{{ ui('可留空', 'Optional') }}</span></div>
                                            <el-collapse class="process-route-anchors">
                                                <el-collapse-item v-for="route in availableRoutes" :key="route.id" :name="route.id" :title="route.name">
                                                    <p class="process-field-help">{{ nodeName(route.startNodeID) }} → {{ nodeName(route.endNodeID) }}</p>
                                                    <el-form-item :label="ui('起点锚', 'Start anchor')"><el-select :model-value="routeAnchorBinding(route.id)?.startAnchor" filterable clearable :placeholder="ui('不设置锚', 'No anchor')" @update:model-value="setRouteAnchor(route.id, 'startAnchor', $event)"><el-option v-for="anchor in endpointAnchors(route.id, 'startAnchor')" :key="anchor.id" :label="anchor.name" :value="anchor.id" /></el-select></el-form-item>
                                                    <el-form-item :label="ui('终点锚', 'End anchor')"><el-select :model-value="routeAnchorBinding(route.id)?.endAnchor" filterable clearable :placeholder="ui('不设置锚', 'No anchor')" @update:model-value="setRouteAnchor(route.id, 'endAnchor', $event)"><el-option v-for="anchor in endpointAnchors(route.id, 'endAnchor')" :key="anchor.id" :label="anchor.name" :value="anchor.id" /></el-select></el-form-item>
                                                </el-collapse-item>
                                            </el-collapse>
                                        </template>
                                    </template>
                                </template>

                                <template v-else-if="selectedEvent">
                                    <el-form-item :label="ui('事件名称', 'Event name')"><el-input v-model="selectedEvent.name" maxlength="100" /></el-form-item>
                                    <el-form-item :label="ui('备选 Node', 'Candidate nodes')">
                                        <div class="process-node-candidates" role="list" :aria-label="ui('备选 Node', 'Candidate nodes')">
                                            <span v-for="nodeID in eventNodeLists.get(selectedEvent.id) || []" :key="nodeID" role="listitem"><el-tag effect="plain">{{ nodeName(nodeID) }}</el-tag></span>
                                            <span v-if="!eventNodeLists.get(selectedEvent.id)?.length" class="process-node-empty">{{ ui('暂无备选 Node', 'No candidate nodes') }}</span>
                                        </div>
                                    </el-form-item>

                                    <div class="process-section-label">{{ ui('事件锚', 'Event anchors') }}</div>
                                    <el-form-item :label="ui('备选锚 AnchorList', 'Candidate anchors')"><el-select v-model="selectedEvent.anchorList" multiple filterable collapse-tags collapse-tags-tooltip @change="syncSelectedAnchor"><el-option v-for="anchor in compatibleEventAnchors" :key="anchor.id" :label="`${anchor.name} · ${trackName(anchor.trackID)}`" :value="anchor.id" /></el-select></el-form-item>
                                    <el-form-item :label="ui('选定锚 SelectedAnchor', 'Selected anchor')"><el-select :model-value="selectedEvent.selectedAnchor" clearable filterable :placeholder="ui('可暂不选择', 'Optional')" @update:model-value="selectedEvent.selectedAnchor = $event || null"><el-option v-for="anchor in availableAnchors" :key="anchor.id" :label="anchor.name" :value="anchor.id" /></el-select></el-form-item>

                                </template>

                                <template v-else-if="selectedPrecedence">

                                    <el-form-item :label="ui('前序事件 LeadingEvent', 'Preceding event')"><el-select v-model="selectedPrecedence.leadingEvent" filterable><el-option v-for="event in current.events" :key="event.id" :label="event.name" :value="event.id" :disabled="event.id === selectedPrecedence.followingEvent" /></el-select></el-form-item>
                                    <div class="process-flow-divider">↓</div>
                                    <el-form-item :label="ui('后序事件 FollowingEvent', 'Following event')"><el-select v-model="selectedPrecedence.followingEvent" filterable><el-option v-for="event in current.events" :key="event.id" :label="event.name" :value="event.id" :disabled="event.id === selectedPrecedence.leadingEvent" /></el-select></el-form-item>
                                    <el-form-item :label="ui('最小间隔 Interval（分钟）', 'Minimum interval (min)')"><el-input-number v-model="selectedPrecedence.interval" :min="0" :precision="2" controls-position="right" /></el-form-item>
                                </template>

                                <template v-else-if="selectedAnchor">
                                    <el-form-item :label="ui('锚名称', 'Anchor name')"><el-input v-model="selectedAnchor.name" maxlength="100" /></el-form-item>
                                    <el-form-item :label="ui('对应轨道 Track', 'Track')"><el-select v-model="selectedAnchor.trackID" filterable :placeholder="ui('选择锚所对应的轨道', 'Select a track for this anchor')"><el-option v-for="track in catalog.tracks" :key="track.id" :label="trackName(track.id)" :value="track.id" /></el-select></el-form-item>

                                    <div class="process-reference-box"><span>{{ ui('引用此锚的事件', 'Events using this anchor') }}</span><b>{{ anchorEventCount }}</b></div>
                                </template>

                                <template v-else>
                                    <el-form-item :label="ui('模板名称', 'Template name')"><el-input v-model="current.name" maxlength="100" /></el-form-item>
                                    <el-form-item :label="ui('模板说明', 'Description')"><el-input v-model="current.description" type="textarea" :rows="4" maxlength="1000" show-word-limit :placeholder="ui('描述此模板适用的车站作业场景', 'Describe the station operations covered by this template')" /></el-form-item>



                                </template>
                            </el-form>
                            <ActionButton v-if="selection && selection.kind !== 'template'" class="process-delete-object" type="danger" :icon="Delete" :disabled="editingLocked" @click="deleteSelected" :label="ui('删除', 'Delete') + ' ' + inspectorTitle" />
                        </fieldset>
                    </div>
                    <div class="process-validation" :class="{ invalid: validationErrors.length }"><span>{{ validationErrors.length ? ui(`有 ${validationErrors.length} 项配置待完善`, `${validationErrors.length} configuration issues`) : ui('✓ 模板配置校验通过', 'Configuration valid') }}</span><ul v-if="validationErrors.length"><li v-for="message in validationErrors.slice(0, 5)" :key="message">{{ message }}</li><li v-if="validationErrors.length > 5">{{ ui(`其余 ${validationErrors.length - 5} 项将在保存时提示`, `${validationErrors.length - 5} more issues will be shown when saving`) }}</li></ul></div>
                </aside>
                <PaneDivider v-if="showObjectList" v-model="objectListWidth" reverse :min="280" :max="640" :label="ui('对象列表', 'Objects')" />
                <aside v-show="showObjectList" :id="`${canvasID}-objects`" class="process-object-list" :aria-label="ui('作业过程对象列表', 'Operation process objects')">
                    <div class="process-object-list-header"><span class="process-eyebrow">{{ ui('对象列表', 'Objects') }}</span></div>
                    <el-tabs v-model="listTab" class="process-list-tabs">
                        <el-tab-pane v-for="tab in objectTabs" :key="tab.kind" :name="tab.kind" :label="tab.label" />
                    </el-tabs>
                    <div class="process-list-table">
                        <el-table :data="listRows" size="small" height="100%" highlight-current-row row-key="id" :current-row-key="selection?.kind === listTab ? selection.id : ''" :empty-text="ui('暂无对象，使用上方按钮添加', 'No objects. Add one using the toolbar.')" @row-click="selectListRow">
                            <el-table-column :label="ui('名称 / 对象', 'Name / object')" min-width="155" show-overflow-tooltip><template #default="{ row }"><span class="process-list-name">{{ row.name }}</span></template></el-table-column>
                            <el-table-column prop="summary" :label="ui('配置', 'Configuration')" min-width="150" show-overflow-tooltip />
                            <el-table-column prop="detail" :label="ui('关联 / 位置', 'References / location')" min-width="150" show-overflow-tooltip />
                            <el-table-column width="60" :label="ui('操作', 'Actions')" fixed="right"><template #default="{ row }"><ActionButton type="primary" :disabled="editingLocked" @click.stop="selectObject(listTab, row.id)" :icon="Edit" :label="ui('编辑', 'Edit')" /></template></el-table-column>
                        </el-table>
                    </div>
                </aside>
            </div>
        </template>
        <div v-else-if="!loading" class="process-empty"><h3>{{ hasScope ? ui('从一个作业模板开始', 'Create an operation template') : ui('请先选择车站方案', 'Select a station scheme') }}</h3><div v-if="hasScope"><ActionButton variant="icon-text" type="primary" :icon="DocumentCopy" :disabled="busy" @click="loadExample" :label="ui('查看测试案例', 'View example')" /><ActionButton variant="icon-text" :icon="Plus" :disabled="busy" @click="newTemplate" :label="ui('创建空白模板', 'Create blank template')" /></div></div>
    </section>
</template>

<script setup lang="ts">
import ActionButton from '@/components/ui/ActionButton.vue'
import PaneDivider from '@/components/ui/PaneDivider.vue'
import ProcessDiagram from './ProcessDiagram.vue'
import { processTypeColors as typeColors } from './processDiagram'
import { useI18n } from 'vue-i18n'
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Edit, Refresh, CirclePlus, Check, Close, Connection, Delete, DocumentCopy, List, Location, Plus, ZoomIn, ZoomOut } from '@element-plus/icons-vue'
import { activityLabels, activityTypes, api, createEmptyTemplate, createExampleTemplate, createDemoCatalog, deriveEventNodeLists, dwellingTrackNames, getPrecedenceEndCandidates, getPrecedenceStartCandidates, makeID, namedTracks, reconcileDwellingTracks, removeActivity, removeAnchor, removeEvent, renameActivity, syncEventNodeLists, validateTemplate, type ActivityType, type PrecedenceEndpoint, type ProcessActivity, type ProcessCatalog, type ProcessEvent, type ProcessScope, type ProcessTemplate, type ProcessTranslate } from '../operationProcess'

const inspectorWidth = ref(300)
const objectListWidth = ref(360)
const { locale, t } = useI18n()
const translateProcess: ProcessTranslate = (key, parameters) => parameters ? t(key, parameters) : t(key)
function ui(zh: string, en: string) { return locale.value.startsWith('en') ? en : zh }
function activityLabel(type: ActivityType) { return ui(activityLabels[type], type === 'Locomotive' ? 'Locomotive' : type) }


// The domain validator returns Chinese messages; translate its fixed text without changing object names.
const validationText = new Map<string, string>([
    ['模板名称必须为 1–200 个字符。', 'Template names must contain 1–200 characters.'],
    ['模板说明最多 4000 个字符。', 'Descriptions can contain up to 4,000 characters.'],
    ['对象 ID 不能为空或重复。', 'Object IDs must be nonempty and unique.'],
    ['活动名称不能为空。', 'Activity names cannot be empty.'],
    ['事件名称必须为 1–200 个字符。', 'Event names must contain 1–200 characters.'],
    ['次序需要两个不同且有效的事件。', 'A precedence requires two distinct valid events.'],
    ['次序间隔必须是非负分钟数。', 'Precedence intervals must be nonnegative minutes.'],
    ['锚名称必须为 1–200 个字符。', 'Anchor names must contain 1–200 characters.'],
    ['同一进路的起终点锚配置不能重复。', 'Each route can have only one endpoint anchor configuration.'],
    ['进路锚引用了不存在的进路。', 'A route anchor references a missing route.'],
    ['进路起终点锚必须存在或留空。', 'Route endpoint anchors must exist or be empty.'],
    ['活动和次序关系形成了循环，请检查箭头方向。', 'Activities and precedences form a cycle. Check the arrow directions.'],
    ['锚的轨道不连接任何候选节点。', 'The anchor track does not connect to any candidate node.'],
    ['活动类型无效。', 'Invalid activity type.'],
    ['持续时间应满足 0 ≤ 最小值 ≤ 最大值。', 'Durations must satisfy 0 ≤ minimum ≤ maximum.'],
    ['画布位置无效。', 'Invalid canvas position.'],
    ['请指定两个不同且有效的起止事件。', 'Select two distinct valid start and end events.'],
    ['起止时刻与持续时间范围不一致。', 'Start and end times do not match the duration range.'],
    ['停留活动仅使用轨道。', 'Dwelling activities can only use tracks.'],
    ['备选轨道必须具名，名称为空的轨道不能用于停留活动。', 'Candidate tracks for dwelling activities must have names.'],
    ['移动活动仅使用进路。', 'Movement activities can only use routes.'],
    ['进路类型必须与活动类型一致。', 'Route types must match the activity type.'],
    ['时刻必须是非负分钟数或留空。', 'Time must be nonnegative minutes or empty.'],
    ['节点不属于当前站场方案。', 'This node is not part of the current station scheme.'],
    ['时刻不满足最小间隔。', 'These times do not satisfy the minimum interval.'],
    ['请选择当前站场方案的轨道。', 'Select a track from the current station scheme.'],
    ['包含不存在的对象。', 'contains missing objects.'],
    ['不能重复。', 'must not contain duplicates.'],
    ['的选定值必须属于备选列表。', 'must include the selected value.'],
])
function translateValidation(message: string) {
    if (!locale.value.startsWith('en')) return message
    if (validationText.has(message)) return validationText.get(message)!
    const separator = message.lastIndexOf('：')
    if (separator >= 0) {
        const detail = validationText.get(message.slice(separator + 1))
        const name = message.slice(0, separator)
        const label = name === '进路起点' ? 'Route start' : name === '进路终点' ? 'Route end' : name
        if (detail) return `${label}: ${detail}`
    }
    const listMessage = message.match(/^(.*)(备选进路|备选轨道|备选锚)(包含不存在的对象。|不能重复。|的选定值必须属于备选列表。)$/)
    if (listMessage) {
        const [, name, kind, detail] = listMessage
        const label = kind === '备选进路' ? 'candidate routes' : kind === '备选轨道' ? 'candidate tracks' : 'candidate anchors'
        return `${name}: ${label} ${validationText.get(detail!)}`
    }
    return message
}

const props = withDefaults(defineProps<{ instanceID: string; stationSchemeID: string; preview?: boolean }>(), { preview: false })
type ObjectKind = 'activity' | 'event' | 'precedence' | 'anchor'
type Selection = { kind: ObjectKind | 'template'; id: string }
type ListRow = { id: string; name: string; summary: string; detail: string }
type ScopeState = { templates: ProcessTemplate[]; saved: Map<string, string>; selectedID: string | null; catalog: ProcessCatalog }
type EventSide = 'start' | 'end' | 'standalone'
const scope = computed<ProcessScope>(() => ({ instanceID: props.instanceID, stationSchemeID: props.stationSchemeID }))
const scopeKey = computed(() => JSON.stringify([props.preview, props.instanceID, props.stationSchemeID]))
const hasScope = computed(() => props.preview || !!(props.instanceID && props.stationSchemeID))
const templates = ref<ProcessTemplate[]>([])
const current = ref<ProcessTemplate | null>(null)
const catalog = ref<ProcessCatalog>({ nodes: [], tracks: [], routes: [] })
const loading = ref(false)
const saving = ref(false)
const loadError = ref('')
const conflictMessage = ref('')
const trackCleanupNotice = ref(false)
const busy = computed(() => loading.value || saving.value)
const connecting = ref(false)
const precedenceSource = ref<PrecedenceEndpoint | null>(null)
const precedenceTarget = ref<PrecedenceEndpoint | null>(null)
const editingLocked = computed(() => busy.value || connecting.value)
const precedenceCandidates = computed(() => current.value ? (precedenceSource.value
    ? getPrecedenceStartCandidates(current.value, precedenceSource.value.activityID)
    : getPrecedenceEndCandidates(current.value)) : [])
const candidateActivities = computed(() => new Set(precedenceCandidates.value.map(endpoint => endpoint.activityID)))
const canAddPrecedence = computed(() => {
    const model = current.value
    return !!model && getPrecedenceEndCandidates(model).some(source => getPrecedenceStartCandidates(model, source.activityID).length > 0)
})
const saved = ref(new Map<string, string>())
const drafts = new Map<string, ScopeState>()
let activeScopeKey = ''
let requestEpoch = 0
const selection = ref<Selection | null>(null)
const listTab = ref<ObjectKind>('activity')
const showObjectList = ref(true)
const zoom = ref(1)
const canvasRef = ref<InstanceType<typeof ProcessDiagram> | null>(null)
const canvasID = makeID('process-canvas')
const objectTabs = computed<{ kind: ObjectKind; label: string }[]>(() => [{ kind: 'activity', label: ui('活动', 'Activity') }, { kind: 'event', label: ui('事件', 'Event') }, { kind: 'precedence', label: ui('次序', 'Precedence') }, { kind: 'anchor', label: ui('锚', 'Anchor') }])
const clone = <T,>(value: T): T => JSON.parse(JSON.stringify(value)) as T
const dirty = computed(() => !!current.value && saved.value.get(current.value.id) !== JSON.stringify(current.value))
const validationErrors = computed(() => current.value ? validateTemplate(current.value, catalog.value).map(translateValidation) : [])
const selectedActivity = computed(() => selection.value?.kind === 'activity' ? current.value?.activities.find(item => item.id === selection.value?.id) : undefined)
const selectedEvent = computed(() => selection.value?.kind === 'event' ? current.value?.events.find(item => item.id === selection.value?.id) : undefined)
const eventNodeLists = computed(() => current.value ? deriveEventNodeLists(current.value, catalog.value) : new Map<string, string[]>())
const selectedPrecedence = computed(() => selection.value?.kind === 'precedence' ? current.value?.precedences.find(item => item.id === selection.value?.id) : undefined)
const selectedAnchor = computed(() => selection.value?.kind === 'anchor' ? current.value?.anchors.find(item => item.id === selection.value?.id) : undefined)
const inspectorTitle = computed(() => ({ template: ui('模板信息', 'Template'), activity: ui('活动', 'Activity'), event: ui('事件', 'Event'), precedence: ui('次序', 'Precedence'), anchor: ui('锚', 'Anchor') })[selection.value?.kind || 'template'])
const compatibleRoutes = computed(() => catalog.value.routes.filter(route => route.type.toLowerCase() === selectedActivity.value?.type.toLowerCase()))
const availableRoutes = computed(() => compatibleRoutes.value.filter(route => selectedActivity.value?.routeList.includes(route.id)))
const dwellingTracks = computed(() => namedTracks(catalog.value))
function candidateNames(activity: ProcessActivity) {
    return activity.type === 'Dwelling' ? dwellingTrackNames(activity, catalog.value) : activity.routeList.map(routeName)
}
const availableAnchors = computed(() => current.value?.anchors.filter(anchor => selectedEvent.value?.anchorList.includes(anchor.id)) || [])
const compatibleEventAnchors = computed(() => current.value?.anchors.filter(anchor => anchorFitsEvent(anchor.id, selectedEvent.value?.id || '')) || [])
function routeAnchorBinding(routeID: string) { return current.value?.routeAnchors.find(item => item.routeID === routeID) }
const anchorEventCount = computed(() => current.value?.events.filter(event => event.anchorList.includes(selectedAnchor.value?.id || '') || event.selectedAnchor === selectedAnchor.value?.id).length || 0)
const listRows = computed<ListRow[]>(() => {
    const model = current.value
    if (!model) return []
    if (listTab.value === 'activity') return model.activities.map(item => ({ id: item.id, name: item.name, summary: `${activityLabel(item.type)} · ${item.minDuration}–${item.maxDuration} ${ui('分钟', 'min')}`, detail: candidateNames(item).join('、') || (item.type === 'Dwelling' ? ui('未设置备选轨道', 'No candidate tracks') : ui('未设置备选进路', 'No candidate routes')) }))
    if (listTab.value === 'event') return model.events.map(item => ({ id: item.id, name: item.name, summary: eventNodeSummary(item.id), detail: item.selectedAnchor ? anchorName(item.selectedAnchor) : ui('未选定锚', 'No selected anchor') }))
    if (listTab.value === 'precedence') return model.precedences.map(item => ({ id: item.id, name: `${eventName(item.leadingEvent)} → ${eventName(item.followingEvent)}`, summary: `${ui('最小间隔', 'Minimum interval')} ${item.interval} ${ui('分钟', 'min')}`, detail: ui('前序事件 → 后序事件', 'Preceding event → following event') }))
    return model.anchors.map(item => ({ id: item.id, name: item.name, summary: trackName(item.trackID), detail: `${model.events.filter(event => event.anchorList.includes(item.id) || event.selectedAnchor === item.id).length} ${ui('个事件引用', 'event references')}` }))
})
function eventName(id: string | null) { return current.value?.events.find(item => item.id === id)?.name || ui('未选择事件', 'No event selected') }
function anchorName(id: string | null) { return current.value?.anchors.find(item => item.id === id)?.name || ui('未选择锚', 'No anchor selected') }
function trackName(id: string | null) { const track = catalog.value.tracks.find(item => item.id === id); return track ? track.name.trim() || `${ui('轨道', 'Track')} ${track.id}` : (id ? ui('轨道不可用', 'Track unavailable') : ui('未选择轨道', 'No track selected')) }
function nodeName(id: string | null) { return catalog.value.nodes.find(item => item.id === id)?.name || (id ? ui('节点不可用', 'Node unavailable') : ui('地点待定', 'Location pending')) }
function routeName(id: string | null) { return catalog.value.routes.find(item => item.id === id)?.name || (id ? ui('进路不可用', 'Route unavailable') : ui('未选择进路', 'No route selected')) }
function eventNodeSummary(eventID: string) { return `${ui('备选节点', 'Candidate nodes')}: ${(eventNodeLists.value.get(eventID) || []).map(nodeName).join('、') || ui('暂无', 'None')}` }
function isUnsaved(template: ProcessTemplate) { return saved.value.get(template.id) !== JSON.stringify(template) }
function selectObject(kind: ObjectKind | 'template', id: string) { if (connecting.value) return; selection.value = { kind, id }; if (kind !== 'template') listTab.value = kind }
function selectListRow(row: ListRow) { selectObject(listTab.value, row.id) }
function selectTemplate(id: string) { if (busy.value) return; current.value = templates.value.find(item => item.id === id) || null; selection.value = current.value ? { kind: 'template', id } : null; conflictMessage.value = '' }
function stashScope() { if (activeScopeKey) drafts.set(activeScopeKey, { templates: clone(templates.value), saved: new Map(saved.value), selectedID: current.value?.id || null, catalog: clone(catalog.value) }) }

async function loadScope() {
    const epoch = ++requestEpoch
    stashScope()
    activeScopeKey = scopeKey.value
    const capturedKey = activeScopeKey
    const capturedScope = { ...scope.value }
    const cached = drafts.get(capturedKey)
    loadError.value = ''
    conflictMessage.value = ''
    trackCleanupNotice.value = false
    if (cached) {
        templates.value = clone(cached.templates)
        saved.value = new Map(cached.saved)
        catalog.value = clone(cached.catalog)
        current.value = templates.value.find(item => item.id === cached.selectedID) || templates.value[0] || null
    } else { templates.value = []; current.value = null; saved.value = new Map(); catalog.value = { nodes: [], tracks: [], routes: [] } }
    selection.value = current.value ? { kind: 'template', id: current.value.id } : null
    if (!hasScope.value) { loading.value = false; return }
    if (props.preview) {
        catalog.value = createDemoCatalog(translateProcess)
        if (!current.value) { const example = createExampleTemplate(capturedScope, catalog.value, translateProcess); templates.value = [example]; current.value = templates.value[0] || null; selection.value = { kind: 'template', id: example.id } }
        loading.value = false
        return
    }
    loading.value = true
    try {
        const [loadedCatalog, loadedTemplates] = await Promise.all([api.catalog(capturedScope), api.list(capturedScope)])
        if (epoch !== requestEpoch || capturedKey !== activeScopeKey) return
        catalog.value = loadedCatalog
        const localDrafts = templates.value.filter(item => isUnsaved(item))
        const desiredID = current.value?.id
        saved.value = new Map(loadedTemplates.map(item => [item.id, JSON.stringify(item)]))
        const draftIDs = new Set(localDrafts.map(item => item.id))
        templates.value = [...loadedTemplates.filter(item => !draftIDs.has(item.id)), ...localDrafts]
        for (const template of templates.value) {
            if (reconcileDwellingTracks(template, loadedCatalog)) trackCleanupNotice.value = true
        }
        current.value = templates.value.find(item => item.id === desiredID) || templates.value[0] || null
        selection.value = current.value ? { kind: 'template', id: current.value.id } : null
    } catch (error) { if (epoch === requestEpoch) loadError.value = errorMessage(error, ui('加载作业编排模板失败，请稍后重试。', 'Could not load operation templates. Try again.')) }
    finally { if (epoch === requestEpoch) loading.value = false }
}
function newTemplate() { if (busy.value) return; const item = createEmptyTemplate(scope.value, translateProcess); templates.value.push(item); selectTemplate(item.id) }
function loadExample() { if (busy.value) return; const item = createExampleTemplate(scope.value, catalog.value, translateProcess); item.name = `${item.name} ${templates.value.filter(template => template.name.startsWith(item.name)).length + 1}`; templates.value.push(item); selectTemplate(item.id); ElMessage.success(ui('测试案例已载入为新草稿，可点击图形查看或修改。', 'Example loaded as a new draft.')) }
function errorMessage(error: unknown, fallback: string) {
    const value = error as { response?: { data?: { message?: string; errors?: string[] | Record<string, string[]> } }; message?: string }
    const errors = value?.response?.data?.errors
    if (errors) return Array.isArray(errors) ? errors.join('；') : Object.values(errors).flat().join('；')
    return value?.response?.data?.message || value?.message || fallback
}
async function saveTemplate() {
    if (!current.value || editingLocked.value) return
    const errors = validateTemplate(current.value, catalog.value)
    if (errors.length) { await ElMessageBox.alert(errors.slice(0, 12).map(translateValidation).join('\n'), ui('请先完善模板配置', 'Complete the template configuration'), { type: 'warning', confirmButtonText: ui('继续编辑', 'Continue editing'), customClass: 'process-validation-dialog' }); return }
    const snapshot = clone(current.value)
    syncEventNodeLists(snapshot, catalog.value)
    const capturedKey = activeScopeKey
    const existed = saved.value.has(snapshot.id)
    saving.value = true
    try {
        const result = props.preview ? { ...snapshot, revision: snapshot.revision + 1 } : await (existed ? api.update(snapshot) : api.create(snapshot))
        if (capturedKey === activeScopeKey) {
            const index = templates.value.findIndex(item => item.id === snapshot.id)
            if (index >= 0) templates.value.splice(index, 1, result)
            else templates.value.push(result)
            saved.value.set(result.id, JSON.stringify(result))
            current.value = templates.value.find(item => item.id === result.id) || null
        } else {
            const state = drafts.get(capturedKey)
            if (state) { const index = state.templates.findIndex(item => item.id === snapshot.id); if (index >= 0) state.templates.splice(index, 1, result); state.saved.set(result.id, JSON.stringify(result)); state.selectedID = result.id }
        }
        conflictMessage.value = ''
        if (capturedKey === activeScopeKey && templates.value.every(item => !isUnsaved(item))) trackCleanupNotice.value = false
        ElMessage.success(props.preview ? ui('模板已保存到本次演示。', 'Template saved for this demo session.') : ui('作业过程模板已保存。', 'Operation process template saved.'))
    } catch (error) {
        if ((error as { response?: { status?: number } }).response?.status === 409 && capturedKey === activeScopeKey) conflictMessage.value = ui('此模板已被其他操作修改。本地草稿已保留，请另存副本或重新加载最新版本。', 'This template changed elsewhere. Your draft is safe; save a copy or reload the latest version.')
        ElMessage.error(errorMessage(error, ui('保存失败，草稿已保留。', 'Save failed. Your draft has been kept.')))
    }
    finally { saving.value = false }
}
async function saveConflictCopy() {
    if (!current.value || editingLocked.value) return
    const copy = clone(current.value)
    copy.id = makeID('process')
    copy.revision = 0
    copy.name = `${copy.name.slice(0, 190)} (${ui('副本', 'Copy')})`
    templates.value.push(copy)
    selectTemplate(copy.id)
    await saveTemplate()
}
async function reloadConflictingTemplate() {
    if (!current.value || editingLocked.value) return
    const id = current.value.id, capturedKey = activeScopeKey
    try { await ElMessageBox.confirm(ui('重新加载将放弃当前模板的本地修改。其他模板的草稿会保留。', 'Reloading discards local changes to this template. Other drafts will be kept.'), ui('重新加载模板', 'Reload template'), { type: 'warning', confirmButtonText: ui('放弃修改并加载', 'Discard and reload'), cancelButtonText: ui('取消', 'Cancel') }) } catch { return }
    if (capturedKey !== activeScopeKey || current.value?.id !== id || busy.value) return
    const baseline = saved.value.get(id)
    const index = templates.value.findIndex(item => item.id === id)
    if (baseline && index >= 0) { templates.value.splice(index, 1, JSON.parse(baseline) as ProcessTemplate); current.value = templates.value[index] || null }
    else { templates.value = templates.value.filter(item => item.id !== id); current.value = null }
    await loadScope()
}
async function deleteTemplate() {
    if (!current.value || busy.value) return
    const item = current.value
    const capturedKey = activeScopeKey
    const capturedScope = { ...scope.value }
    try { await ElMessageBox.confirm(ui(`删除模板“${item.name}”及其活动、事件、次序和锚？`, `Delete template “${item.name}” and its activities, events, precedences and anchors?`), ui('删除作业模板', 'Delete operation template'), { type: 'warning', confirmButtonText: ui('删除', 'Delete'), cancelButtonText: ui('取消', 'Cancel') }) } catch { return }
    if (capturedKey !== activeScopeKey || current.value?.id !== item.id) return
    saving.value = true
    try {
        if (!props.preview && saved.value.has(item.id)) await api.remove(capturedScope, item.id, item.revision)
        if (capturedKey === activeScopeKey) { templates.value = templates.value.filter(template => template.id !== item.id); saved.value.delete(item.id); current.value = templates.value[0] || null; selection.value = current.value ? { kind: 'template', id: current.value.id } : null }
        else { const state = drafts.get(capturedKey); if (state) { state.templates = state.templates.filter(template => template.id !== item.id); state.saved.delete(item.id); state.selectedID = state.templates[0]?.id || null } }
        ElMessage.success(ui('模板已删除。', 'Template deleted.'))
    } catch (error) { ElMessage.error(errorMessage(error, ui('删除模板失败。', 'Could not delete the template.'))) }
    finally { saving.value = false }
}

function makeEvent(name: string): ProcessEvent { return { id: makeID('event'), name, time: null, nodeID: null, nodeList: [], anchorList: [], selectedAnchor: null } }
function changeActivityName(name: string) {
    if (current.value && selectedActivity.value && !busy.value) renameActivity(current.value, selectedActivity.value.id, name, translateProcess)
}
function addActivity(type: ActivityType) {
    const model = current.value
    if (!model || busy.value) return
    const number = model.activities.length + 1
    const name = `${activityLabel(type)} ${number}`
    const start = makeEvent(`${name} ${ui('开始', 'Start')}`)
    const end = makeEvent(`${name} ${ui('结束', 'End')}`)
    model.events.push(start, end)
    const item: ProcessActivity = { id: makeID('activity'), name, type, minDuration: 5, maxDuration: 15, startEvent: start.id, endEvent: end.id, routeList: [], selectedRoute: null, trackList: [], selectedTrack: null, x: 85 + (number - 1) % 3 * 310, y: 140 + Math.floor((number - 1) / 3) * 240 }
    model.activities.push(item)
    selectObject('activity', item.id)
}
function addEvent() { if (!current.value || busy.value) return; const item = makeEvent(`${ui('独立事件', 'Standalone event')} ${current.value.events.length + 1}`); current.value.events.push(item); selectObject('event', item.id) }
function addPrecedence() {
    if (!current.value || busy.value || !canAddPrecedence.value) return
    endDrag()
    precedenceSource.value = null
    precedenceTarget.value = null
    connecting.value = true
}
function cancelPrecedence() { connecting.value = false; precedenceSource.value = null; precedenceTarget.value = null }
function isPrecedenceCandidate(activityID: string | null, side: EventSide) { return connecting.value && side === (precedenceSource.value ? 'start' : 'end') && !!activityID && candidateActivities.value.has(activityID) }
function previewPrecedence(eventID: string, activityID: string | null, side: EventSide) {
    precedenceTarget.value = precedenceSource.value && isPrecedenceCandidate(activityID, side) ? { activityID: activityID!, eventID } : null
}
function clearPrecedencePreview(activityID: string | null) { if (precedenceTarget.value?.activityID === activityID) precedenceTarget.value = null }
function activateEvent(eventID: string, activityID: string | null, side: EventSide) {
    if (!connecting.value) { selectObject('event', eventID); return }
    if (busy.value || !current.value || !isPrecedenceCandidate(activityID, side)) return
    if (precedenceSource.value && !getPrecedenceEndCandidates(current.value).some(item => item.activityID === precedenceSource.value?.activityID && item.eventID === precedenceSource.value.eventID)) { cancelPrecedence(); return }
    const endpoint = precedenceCandidates.value.find(item => item.activityID === activityID && item.eventID === eventID)
    if (!endpoint) return
    if (!precedenceSource.value) { precedenceSource.value = endpoint; precedenceTarget.value = null; return }
    const item = { id: makeID('precedence'), leadingEvent: precedenceSource.value.eventID, followingEvent: endpoint.eventID, interval: 0 }
    current.value.precedences.push(item)
    cancelPrecedence()
    selectObject('precedence', item.id)
}
function handleConnectionEscape(event: KeyboardEvent) { if (connecting.value && event.key === 'Escape') { event.preventDefault(); cancelPrecedence() } }
function addAnchor() { if (!current.value || busy.value) return; const item = { id: makeID('anchor'), name: `${ui('轨道锚', 'Track anchor')} ${current.value.anchors.length + 1}`, trackID: catalog.value.tracks[0]?.id || '' }; current.value.anchors.push(item); selectObject('anchor', item.id) }
function changeActivityType(type: ActivityType) {
    const item = selectedActivity.value
    if (!item) return
    item.type = type
    if (type === 'Dwelling') { item.routeList = []; item.selectedRoute = null; reconcileActivityEventNodes(item) }
    else { item.trackList = []; item.selectedTrack = null; item.routeList = item.routeList.filter(id => catalog.value.routes.some(route => route.id === id && route.type.toLowerCase() === type.toLowerCase())); syncSelectedRoute() }
}
function syncSelectedTrack() { const item = selectedActivity.value; if (item?.selectedTrack && !item.trackList.includes(item.selectedTrack)) item.selectedTrack = null }
function syncSelectedRoute() {
    const item = selectedActivity.value
    if (!item) return
    if (item.selectedRoute && !item.routeList.includes(item.selectedRoute)) item.selectedRoute = null
    reconcileActivityEventNodes(item)
}
function syncSelectedAnchor() { const item = selectedEvent.value; if (item?.selectedAnchor && !item.anchorList.includes(item.selectedAnchor)) item.selectedAnchor = null }
function anchorFitsEvent(anchorID: string, eventID: string) {
    const nodeIDs = eventNodeLists.value.get(eventID) || []
    if (!nodeIDs.length) return true
    const anchor = current.value?.anchors.find(item => item.id === anchorID)
    const track = catalog.value.tracks.find(item => item.id === anchor?.trackID)
    return !!track && nodeIDs.some(nodeID => track.fromNodeID === nodeID || track.toNodeID === nodeID)
}
function reconcileActivityEventNodes(activity: ProcessActivity) {
    const model = current.value
    if (!model) return
    syncEventNodeLists(model, catalog.value)
    let removed = false
    for (const event of model.events.filter(item => item.id === activity.startEvent || item.id === activity.endEvent)) {
        const anchors = event.anchorList.filter(id => anchorFitsEvent(id, event.id))
        removed ||= anchors.length !== event.anchorList.length
        event.anchorList = anchors
        if (event.selectedAnchor && !anchors.includes(event.selectedAnchor)) event.selectedAnchor = null
    }
    if (removed) ElMessage.info(ui('已清理与更新后的备选节点不相连的事件锚。', 'Removed event anchors that do not connect to the updated candidate nodes.'))
}
function endpointAnchors(routeID: string, endpoint: 'startAnchor' | 'endAnchor') {
    const route = catalog.value.routes.find(item => item.id === routeID)
    const nodeID = endpoint === 'startAnchor' ? route?.startNodeID : route?.endNodeID
    return current.value?.anchors.filter(anchor => { const track = catalog.value.tracks.find(item => item.id === anchor.trackID); return !nodeID || track?.fromNodeID === nodeID || track?.toNodeID === nodeID }) || []
}
function setRouteAnchor(routeID: string, endpoint: 'startAnchor' | 'endAnchor', value: string | undefined) {
    const model = current.value
    if (!model || !selectedActivity.value?.routeList.includes(routeID)) return
    let binding = model.routeAnchors.find(item => item.routeID === routeID)
    if (!binding) { binding = { routeID, startAnchor: null, endAnchor: null }; model.routeAnchors.push(binding) }
    binding[endpoint] = value || null
}
async function deleteSelected() {
    const model = current.value
    const target = selection.value
    if (!model || !target || target.kind === 'template' || busy.value) return
    if (target.kind === 'event' && model.activities.some(activity => activity.startEvent === target.id || activity.endEvent === target.id)) { ElMessage.warning(ui('活动的起止事件不能单独删除，请删除对应活动。', 'Start and end events belong to an activity. Delete the activity to remove them.')); return }
    const detail = target.kind === 'activity' ? ui('删除活动后，将同时清理其专属事件及相关次序。', 'Deleting this activity also removes its events and related precedence constraints.') : target.kind === 'anchor' ? ui('删除锚后，将同步清理事件和进路端点中的引用。', 'Deleting this anchor also removes its references from events and route endpoints.') : target.kind === 'event' ? ui('删除事件后，将同步清理与其关联的次序。', 'Deleting this event also removes its precedence constraints.') : ui('删除此次序约束。', 'Delete this precedence constraint.')
    try { await ElMessageBox.confirm(detail, `${ui('删除', 'Delete')} ${inspectorTitle.value}`, { type: 'warning', confirmButtonText: ui('删除', 'Delete'), cancelButtonText: ui('取消', 'Cancel') }) } catch { return }
    if (current.value?.id !== model.id || busy.value) return
    if (target.kind === 'activity') removeActivity(model, target.id)
    else if (target.kind === 'event') { if (!removeEvent(model, target.id)) return }
    else if (target.kind === 'anchor') removeAnchor(model, target.id)
    else model.precedences = model.precedences.filter(item => item.id !== target.id)
    selectObject('template', model.id)
}
let drag: { activityID: string; pointerID: number; x: number; y: number; originX: number; originY: number } | null = null
function startDrag(event: PointerEvent, activity: ProcessActivity) {
    if (editingLocked.value || event.button !== 0) return
    selectObject('activity', activity.id)
    drag = { activityID: activity.id, pointerID: event.pointerId, x: event.clientX, y: event.clientY, originX: activity.x, originY: activity.y }
    canvasRef.value?.setPointerCapture(event.pointerId)
    event.preventDefault()
}
function moveActivity(event: PointerEvent) { if (!drag || busy.value || event.pointerId !== drag.pointerID) return; const item = current.value?.activities.find(activity => activity.id === drag?.activityID); if (item) { item.x = Math.round(Math.max(60, drag.originX + (event.clientX - drag.x) / zoom.value)); item.y = Math.round(Math.max(85, drag.originY + (event.clientY - drag.y) / zoom.value)) } }
function endDrag() { if (drag && canvasRef.value?.hasPointerCapture(drag.pointerID)) canvasRef.value.releasePointerCapture(drag.pointerID); drag = null }
function protectDrafts(event: BeforeUnloadEvent) {
    const hasDrafts = templates.value.some(item => isUnsaved(item)) || [...drafts.entries()].some(([key, state]) => key !== activeScopeKey && state.templates.some(item => state.saved.get(item.id) !== JSON.stringify(item)))
    if (hasDrafts) { event.preventDefault(); event.returnValue = '' }
}
window.addEventListener('beforeunload', protectDrafts)
window.addEventListener('keydown', handleConnectionEscape)
watch([scopeKey, () => current.value?.id], cancelPrecedence)
watch(busy, value => { if (value) cancelPrecedence() })
watch([() => current.value?.activities.map(activity => `${activity.id}:${activity.startEvent}:${activity.endEvent}`).join('|'), () => current.value?.events.map(event => event.id).join('|')], () => { if (connecting.value) cancelPrecedence() })
watch(scopeKey, loadScope, { immediate: true })
function getReportSnapshot(caption = current.value?.name || '', namesOnly = false) {
    if (!current.value) return null
    return { model: clone(current.value), catalog: clone(catalog.value), figure: canvasRef.value?.exportReportFigure(caption, namesOnly) || null, zoom: zoom.value }
}
defineExpose({ getReportSnapshot, getReportViewState: () => ({ zoom: zoom.value }) })
onBeforeUnmount(() => { requestEpoch++; cancelPrecedence(); endDrag(); window.removeEventListener('beforeunload', protectDrafts); window.removeEventListener('keydown', handleConnectionEscape) })
</script>

<style scoped>
.process-editor { --process-blue: #3265db; display: flex; flex-direction: column; width: 100%; height: 100%; min-height: 0; box-sizing: border-box; overflow: hidden; color: #24334c; background: #fff; padding: 0; min-width: 0; }
.process-editor > :not(.process-workspace):not(.process-empty) { flex-shrink: 0; }
.process-header { display: flex; justify-content: space-between; align-items: center; gap: 8px; flex-wrap: wrap; margin-bottom: 10px; }
.process-empty-icon { display: flex; align-items: center; justify-content: center; background: #e9effc; color: var(--process-blue); }
.process-template-actions { display: flex; align-items: center; gap: 8px; flex-wrap: wrap; min-width: 0; max-width: 100%; }
.process-template-actions > .el-select { width: 400px; max-width: 100%; }
.process-template-actions .el-button + .el-button { margin-left: 0; }
.process-alert { margin-bottom: 12px; }
.process-recovery-actions { display: flex; flex-wrap: wrap; gap: 8px; }
.process-recovery-actions :deep(.el-button + .el-button) { margin-left: 0; }
.process-save-state { font-size: 11px; color: #658378; border: 1px solid #d6e6de; background: #eff7f3; padding: 2px 7px; border-radius: 4px; white-space: nowrap; }
.process-save-state.dirty { color: #9b7b39; background: #fff8e8; border-color: #ebdcb8; }
.process-workspace { display: grid; flex: 1; min-width: 0; min-height: 0; grid-template-columns: minmax(320px, 1fr) 8px var(--inspector-width) 8px var(--object-list-width); grid-template-rows: minmax(0, 1fr); gap: 0; align-items: stretch; overflow: auto; }
.process-workspace.process-list-hidden { grid-template-columns: minmax(320px, 1fr) 8px var(--inspector-width); }
.process-main { display: flex; flex-direction: column; min-width: 0; min-height: 0; border: 1px solid #dde5ef; border-radius: 6px; background: #fff; overflow: hidden; }
.process-canvas-toolbar, .process-connection-status, .process-legend { flex-shrink: 0; }
.process-canvas-toolbar { display: flex; align-items: center; justify-content: space-between; min-height: 52px; gap: 8px; padding: 8px 13px; box-sizing: border-box; border-bottom: 1px solid #e6ebf3; flex-wrap: wrap; }
.process-connection-status { display: flex; align-items: center; justify-content: space-between; gap: 12px; padding: 10px 16px; background: #edf4ff; border-bottom: 1px solid #cbdcf8; color: #3766ac; font-size: 12px; line-height: 1.6; }
.process-connection-status b { margin-right: 10px; color: #3265db; }
.process-add-actions { display: flex; gap: 8px; flex-wrap: wrap; }
.process-add-actions .el-button + .el-button { margin-left: 0; }
.process-inline-icon { width: 12px; margin-left: 7px; }
.process-view-actions { display: flex; align-items: center; gap: 10px; margin-left: auto; }
.process-zoom { display: flex; align-items: center; color: #8a97a8; font-size: 11px; }
.process-zoom > span:not(.action-button-anchor) { width: 38px; text-align: center; }
.process-canvas-scroll { flex: 1; min-height: 0; overflow: auto; background: #fbfcfe; }
.process-route-anchors :deep(.el-collapse-item__header) { height: auto; min-height: 38px; line-height: 1.5; padding: 8px 0; text-align: left; }
.process-legend { border-top: 1px solid #e8edf5; display: flex; align-items: center; flex-wrap: wrap; gap: 16px; padding: 10px 16px; font-size: 11px; color: #8d98a9; }
.process-legend span { display: flex; gap: 5px; align-items: center; }
.process-legend i { width: 7px; height: 7px; border-radius: 2px; }
.process-legend-note { margin-left: auto; }
.process-object-list { display: flex; flex-direction: column; min-width: 0; min-height: 0; overflow: hidden; background: #fff; border: 1px solid #dde5ef; border-radius: 6px; }
.process-object-list-header { flex-shrink: 0; padding: 8px 12px 0; }
.process-object-list-header p { color: #6b7b91; font-size: 12px; margin: 7px 0 0; }
.process-list-tabs { flex: 0 0 auto; min-width: 0; padding: 0 12px; }
.process-list-tabs :deep(.el-tabs__header) { margin: 0; }
.process-list-tabs :deep(.el-tabs__content) { display: none; }
.process-list-tabs :deep(.el-tabs__item) { font-size: 12px; height: 42px; padding: 0 12px; }
.process-list-tabs :deep(.el-tabs__nav-wrap::after) { height: 1px; }
.process-list-table { flex: 1; min-height: 0; min-width: 0; padding: 0 8px 8px; }
.process-object-list :deep(.el-table) { --el-table-header-bg-color: #f8fafd; --el-table-row-hover-bg-color: #f5f8fe; --el-table-current-row-bg-color: #edf3ff; --el-table-border-color: #edf1f6; font-size: 11px; }
.process-object-list :deep(.el-table__row) { cursor: pointer; }
.process-object-list :deep(.el-table .cell) { line-height: 24px; }
.process-list-name { color: #425671; }
.process-inspector { background: #fff; border: 1px solid #dde5ef; border-radius: 6px; display: flex; flex-direction: column; min-width: 0; min-height: 0; overflow: hidden; }
.process-inspector-header { display: flex; flex-shrink: 0; justify-content: space-between; align-items: center; padding: 8px 12px; border-bottom: 1px solid #e9edf4; }
.process-eyebrow { color: #9aa6b7; font-size: 10px; letter-spacing: 1px; }
.process-inspector-header h3 { margin: 0; font-size: 13px; font-weight: 650; }
.process-inspector-body { padding: 12px; flex: 1; min-height: 0; overflow: auto; }
.process-fieldset { padding: 0; margin: 0; border: 0; min-width: 0; }
.process-inspector :deep(.el-form-item) { margin-bottom: 17px; }
.process-inspector :deep(.el-form-item__label) { color: #6b7b91; font-size: 11px; margin-bottom: 7px; line-height: 16px; }
.process-inspector :deep(.el-select), .process-inspector :deep(.el-input-number) { width: 100%; }
.process-inspector :deep(.el-input-number .el-input__inner) { text-align: left; }
.process-form-pair { display: grid; grid-template-columns: 1fr 1fr; gap: 10px; }
.process-class-note { font-size: 10px; color: #8c9aaf; padding: 0 0 18px; margin-top: -8px; }
.process-section-label { font-size: 12px; font-weight: 650; color: #526780; border-top: 1px solid #edf1f6; padding-top: 18px; margin: 21px 0 15px; }
.process-section-label span { font-size: 10px; font-weight: 400; color: #9aa7b9; margin-left: 8px; }
.process-field-help { font-size: 11px; line-height: 1.8; color: #95a1b2; margin: 0 0 16px; }
.process-flow-divider { text-align: center; color: #afbad0; margin: -8px 0 8px; font-size: 22px; }
.process-node-candidates { display: flex; flex-wrap: wrap; gap: 6px; width: 100%; min-height: 32px; padding: 7px 9px; border: 1px solid #e2e8f0; border-radius: 4px; background: #f8fafd; }
.process-node-candidates :deep(.el-tag) { height: auto; white-space: normal; overflow-wrap: anywhere; line-height: 1.6; }
.process-node-empty { color: #95a1b2; font-size: 11px; }
.process-reference-box { display: flex; justify-content: space-between; background: #f6f8fc; border: 1px solid #e6edf5; padding: 13px; border-radius: 6px; color: #8796aa; font-size: 11px; }
.process-reference-box b { color: #536b8e; font-weight: 500; }
.process-guide { padding-left: 17px; color: #8493a8; font-size: 11px; line-height: 2; margin: 0 0 18px; }
.process-guide li { padding-left: 3px; margin-bottom: 7px; }
.process-delete-object { margin-top: 8px; }
.process-validation { flex-shrink: 0; max-height: 30%; overflow: auto; border-top: 1px solid #e7edf3; padding: 13px 18px; color: #59917d; background: #f6fbf8; font-size: 11px; line-height: 1.7; }
.process-validation.invalid { background: #fffbf2; color: #a88a4b; }
.process-validation ul { padding-left: 16px; margin: 7px 0 0; font-size: 10px; }
.process-empty { display: flex; flex: 1; min-height: 0; overflow: auto; align-items: center; flex-direction: column; justify-content: center; text-align: center; }
.process-empty > div { display: flex; flex-wrap: wrap; justify-content: center; gap: 8px; }
.process-empty :deep(.el-button + .el-button) { margin-left: 0; }
.process-empty-icon { width: 70px; height: 70px; border-radius: 20px; }
.process-empty-icon svg { width: 38px; height: 38px; }
.process-empty h3 { color: #465a75; font-size: 18px; margin: 22px 0 7px; }
.process-empty p { color: #8c9bb0; font-size: 12px; margin: 0 0 26px; }
</style>

<style>
.process-validation-dialog .el-message-box__message { white-space: pre-line; font-size: 13px; }
</style>
