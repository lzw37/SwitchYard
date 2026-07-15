<template>
    <section class="operation-plan-page">
        <div class="operation-plan-toolbar">
            <div class="operation-plan-scheme-control">
                <span class="operation-plan-control-label">{{ t('stationLayout.menu.stationScheme') }}</span>
                <el-select
                    v-model="currentStationSchemeId"
                    size="small"
                    filterable
                    class="operation-plan-scheme-select"
                    :loading="loadingStationSchemes"
                    :disabled="!selectedInstanceId || loadingStationSchemes || loadingTrainTemplates"
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
            <div class="operation-plan-object-control">
                <span class="operation-plan-control-label">{{ t('operationPlan.planObject.label') }}</span>
                <el-select
                    v-model="currentOperationPlanId"
                    size="small"
                    filterable
                    class="operation-plan-object-select"
                    :loading="loadingOperationPlans"
                    :disabled="!currentStationSchemeId || loadingOperationPlans || operationPlanInlineActive"
                    :placeholder="t('operationPlan.planObject.placeholders.select')"
                    @change="handleOperationPlanChange"
                >
                    <el-option
                        v-for="option in operationPlanOptions"
                        :key="option.operationPlanID"
                        :label="formatOperationPlanLabel(option)"
                        :value="option.operationPlanID"
                    />
                </el-select>
            </div>
            <div class="operation-plan-toolbar-actions">
                <el-button
                    :icon="Setting"
                    size="small"
                    :disabled="!selectedInstanceId || !currentStationSchemeId"
                    @click="openOperationPlanManager"
                >
                    {{ t('operationPlan.planObject.actions.manage') }}
                </el-button>
                <el-button
                    :icon="Refresh"
                    size="small"
                    :disabled="!canLoadTemplates || operationPlanInlineActive"
                    @click="refreshOperationPlanData"
                >
                    {{ t('operationPlan.actions.refresh') }}
                </el-button>
            </div>
        </div>

        <el-dialog
            v-model="operationPlanManagerVisible"
            :title="t('operationPlan.planObject.manager.title')"
            width="760px"
            class="operation-plan-object-dialog"
        >
            <div class="operation-plan-object-manager" v-loading="loadingOperationPlans || savingOperationPlanObject">
                <div class="operation-plan-object-manager-toolbar">
                    <el-button
                        :icon="Plus"
                        type="primary"
                        size="small"
                        :disabled="operationPlanObjectInlineActive || savingOperationPlanObject"
                        @click="startCreateOperationPlanObjectInline"
                    >
                        {{ t('operationPlan.actions.add') }}
                    </el-button>
                    <el-button
                        :icon="Refresh"
                        size="small"
                        :disabled="operationPlanObjectInlineActive || savingOperationPlanObject"
                        @click="loadOperationPlans"
                    >
                        {{ t('operationPlan.actions.refresh') }}
                    </el-button>
                </div>

                <el-table
                    :data="operationPlanOptions"
                    size="small"
                    class="operation-plan-object-table"
                    :empty-text="t('operationPlan.planObject.empty')"
                    :row-key="getOperationPlanObjectRowKey"
                >
                    <el-table-column
                        prop="operationPlanID"
                        :label="t('operationPlan.planObject.fields.operationPlanID')"
                        min-width="150"
                    >
                        <template #default="{ row }">
                            <el-input
                                v-if="isOperationPlanObjectInlineEditing(row)"
                                v-model="operationPlanObjectForm.operationPlanID"
                                size="small"
                                :disabled="operationPlanObjectMode === 'edit'"
                                :placeholder="t('operationPlan.placeholders.autoId')"
                            />
                            <span v-else>{{ row.operationPlanID }}</span>
                        </template>
                    </el-table-column>
                    <el-table-column
                        prop="name"
                        :label="t('operationPlan.planObject.fields.name')"
                        min-width="160"
                    >
                        <template #default="{ row }">
                            <el-input
                                v-if="isOperationPlanObjectInlineEditing(row)"
                                v-model="operationPlanObjectForm.name"
                                size="small"
                                :placeholder="t('operationPlan.planObject.placeholders.name')"
                            />
                            <span v-else>{{ row.name }}</span>
                        </template>
                    </el-table-column>
                    <el-table-column
                        prop="description"
                        :label="t('operationPlan.planObject.fields.description')"
                        min-width="220"
                        show-overflow-tooltip
                    >
                        <template #default="{ row }">
                            <el-input
                                v-if="isOperationPlanObjectInlineEditing(row)"
                                v-model="operationPlanObjectForm.description"
                                size="small"
                                :placeholder="t('operationPlan.planObject.placeholders.description')"
                            />
                            <span v-else>{{ row.description }}</span>
                        </template>
                    </el-table-column>
                    <el-table-column
                        prop="sortOrder"
                        :label="t('operationPlan.planObject.fields.sortOrder')"
                        width="100"
                    >
                        <template #default="{ row }">
                            <el-input-number
                                v-if="isOperationPlanObjectInlineEditing(row)"
                                v-model="operationPlanObjectForm.sortOrder"
                                size="small"
                                :min="0"
                                :step="1"
                                controls-position="right"
                                class="operation-plan-object-sort-input"
                            />
                            <span v-else>{{ row.sortOrder ?? '' }}</span>
                        </template>
                    </el-table-column>
                    <el-table-column
                        :label="t('operationPlan.fields.operation')"
                        width="220"
                        fixed="right"
                    >
                        <template #default="{ row }">
                            <template v-if="isOperationPlanObjectInlineEditing(row)">
                                <el-button
                                    :icon="Check"
                                    link
                                    type="primary"
                                    :loading="savingOperationPlanObject"
                                    @click="saveOperationPlanObject"
                                >
                                    {{ t('operationPlan.actions.save') }}
                                </el-button>
                                <el-button
                                    :icon="Close"
                                    link
                                    :disabled="savingOperationPlanObject"
                                    @click="cancelOperationPlanObjectEdit"
                                >
                                    {{ t('operationPlan.actions.cancel') }}
                                </el-button>
                            </template>
                            <el-button
                                v-else
                                :icon="Edit"
                                link
                                type="primary"
                                :disabled="operationPlanObjectInlineActive"
                                @click="startEditOperationPlanObject(row)"
                            >
                                {{ t('operationPlan.actions.edit') }}
                            </el-button>
                            <el-button
                                v-if="!isOperationPlanObjectInlineEditing(row)"
                                :icon="CopyDocument"
                                link
                                type="success"
                                :disabled="operationPlanObjectInlineActive"
                                @click="copyOperationPlanObject(row)"
                            >
                                {{ t('operationPlan.actions.copy') }}
                            </el-button>
                            <el-button
                                v-if="!isOperationPlanObjectInlineEditing(row)"
                                :icon="Delete"
                                link
                                type="danger"
                                :disabled="operationPlanObjectInlineActive || row.operationPlanID === defaultOperationPlanID"
                                @click="confirmDeleteOperationPlanObject(row)"
                            >
                                {{ t('operationPlan.actions.delete') }}
                            </el-button>
                        </template>
                    </el-table-column>
                </el-table>
            </div>
        </el-dialog>

        <el-tabs v-model="activeOperationPlanTab" class="operation-plan-sub-tabs">
            <el-tab-pane
                :label="t('operationPlan.tabs.trainTemplate')"
                name="trainTemplate"
                class="operation-plan-sub-tab-pane"
            >
                <div
                    class="operation-plan-grid"
                    :class="{ 'is-expanded': selectedTrainTemplate !== null }"
                >
                    <section class="operation-plan-card train-template-card" v-loading="loadingTrainTemplates || savingTrainTemplate">
                <header class="operation-plan-card-header">
                    <div>
                        <h2>{{ t('operationPlan.train.title') }}</h2>
                        <span>{{ trainTemplateCountText }}</span>
                    </div>
                    <div class="operation-plan-card-actions">
                        <el-button
                            :icon="Refresh"
                            circle
                            size="small"
                            :disabled="!canLoadTemplates || operationPlanInlineActive"
                            @click="loadTrainTemplates"
                        />
                        <el-button
                            :icon="Plus"
                            type="primary"
                            size="small"
                            :disabled="!canEditTrainTemplates || operationPlanInlineActive"
                            @click="startCreateTrainTemplateInline"
                        >
                            {{ t('operationPlan.actions.add') }}
                        </el-button>
                    </div>
                </header>

                <el-table
                    :data="visibleTrainTemplates"
                    class="operation-plan-table"
                    size="small"
                    height="100%"
                    :row-key="getTrainTemplateRowKey"
                    highlight-current-row
                    :row-class-name="getTrainTemplateRowClassName"
                    :empty-text="trainTemplateEmptyText"
                    @row-click="toggleTrainTemplateExpansion"
                >
                    <el-table-column width="56" align="center">
                        <template #default="{ row }">
                            <el-tag v-if="row.isDraft" size="small" type="success">
                                {{ t('operationPlan.states.new') }}
                            </el-tag>
                            <el-tag v-else-if="isTrainTemplateEditing(row)" size="small" type="warning">
                                {{ t('operationPlan.states.editing') }}
                            </el-tag>
                            <el-button
                                v-else
                                :icon="isTrainTemplateExpanded(row) ? ArrowDown : ArrowRight"
                                size="small"
                                text
                                type="primary"
                                :title="isTrainTemplateExpanded(row) ? t('operationPlan.actions.collapse') : t('operationPlan.actions.expand')"
                                @click.stop="toggleTrainTemplateExpansion(row)"
                            />
                        </template>
                    </el-table-column>
                    <el-table-column
                        prop="name"
                        :label="t('operationPlan.train.fields.name')"
                        min-width="190"
                    >
                        <template #default="{ row }">
                            <div
                                v-if="isTrainTemplateInlineEditing(row)"
                                class="operation-plan-name-edit-cell"
                                @click.stop
                            >
                                <el-input
                                    v-model="trainTemplateForm.trainTemplateID"
                                    size="small"
                                    clearable
                                    :placeholder="t('operationPlan.placeholders.autoId')"
                                />
                                <el-input
                                    v-model="trainTemplateForm.name"
                                    size="small"
                                    clearable
                                    :placeholder="t('operationPlan.train.placeholders.name')"
                                />
                            </div>
                            <el-tooltip
                                v-else
                                :content="getTrainTemplateNameTooltip(row)"
                                placement="top"
                                :show-after="250"
                            >
                                <span class="operation-plan-hover-name">{{ row.name }}</span>
                            </el-tooltip>
                        </template>
                    </el-table-column>
                    <el-table-column
                        prop="type"
                        :label="t('operationPlan.train.fields.type')"
                        width="110"
                        show-overflow-tooltip
                    >
                        <template #default="{ row }">
                            <el-input
                                v-if="isTrainTemplateInlineEditing(row)"
                                v-model="trainTemplateForm.type"
                                size="small"
                                clearable
                                :placeholder="t('operationPlan.train.placeholders.type')"
                                @click.stop
                            />
                            <span v-else>{{ row.type }}</span>
                        </template>
                    </el-table-column>
                    <el-table-column
                        prop="number"
                        :label="t('operationPlan.train.fields.number')"
                        width="120"
                        align="right"
                    >
                        <template #default="{ row }">
                            <el-input-number
                                v-if="isTrainTemplateInlineEditing(row)"
                                v-model="trainTemplateForm.number"
                                class="operation-plan-table-number-input"
                                controls-position="right"
                                size="small"
                                :min="0"
                                :step="1"
                                @click.stop
                            />
                            <span v-else>{{ row.number ?? '' }}</span>
                        </template>
                    </el-table-column>
                    <el-table-column
                        prop="isFixedOperation"
                        :label="t('operationPlan.train.fields.isFixedOperation')"
                        width="118"
                        align="center"
                    >
                        <template #default="{ row }">
                            <el-checkbox
                                v-if="isTrainTemplateInlineEditing(row)"
                                v-model="trainTemplateForm.isFixedOperation"
                                @click.stop
                            />
                            <el-checkbox
                                v-else
                                :model-value="row.isFixedOperation"
                                :disabled="!canEditTrainTemplates || operationPlanInlineActive || savingTrainTemplate"
                                @click.stop
                                @change="updateTrainTemplateFixedOperation(row, Boolean($event))"
                            />
                        </template>
                    </el-table-column>
                    <el-table-column
                        :label="t('operationPlan.fields.operation')"
                        width="142"
                        fixed="right"
                        align="center"
                    >
                        <template #default="{ row }">
                            <div v-if="isTrainTemplateInlineEditing(row)" class="operation-plan-row-actions">
                                <el-button
                                    :icon="Check"
                                    circle
                                    size="small"
                                    type="primary"
                                    :loading="savingTrainTemplate"
                                    @click.stop="saveTrainTemplate"
                                />
                                <el-button
                                    :icon="Close"
                                    circle
                                    size="small"
                                    @click.stop="cancelTrainTemplateInline(row)"
                                />
                            </div>
                            <div v-else class="operation-plan-row-actions">
                                <el-button
                                    :icon="Edit"
                                    circle
                                    size="small"
                                    :disabled="!canEditTrainTemplates || operationPlanInlineActive"
                                    @click.stop="startEditTrainTemplateInline(row)"
                                />
                                <el-button
                                    :icon="Delete"
                                    circle
                                    size="small"
                                    type="danger"
                                    :disabled="!canEditTrainTemplates || operationPlanInlineActive"
                                    @click.stop="confirmDeleteTrainTemplate(row)"
                                />
                            </div>
                        </template>
                    </el-table-column>
                </el-table>
                    </section>

                    <section
                        v-if="selectedTrainTemplate"
                        class="operation-plan-card movement-template-card"
                        v-loading="loadingMovementTemplates || savingMovementTemplate"
                    >
                <header class="operation-plan-card-header">
                    <div>
                        <h2>{{ t('operationPlan.movement.title') }}</h2>
                        <span>{{ movementTemplateCountText }}</span>
                    </div>
                    <div class="operation-plan-card-actions">
                        <el-button
                            :icon="Refresh"
                            circle
                            size="small"
                            :disabled="!canLoadMovementTemplates || movementTemplateInlineActive"
                            @click="loadMovementTemplates"
                        />
                        <el-button
                            :icon="Plus"
                            type="primary"
                            size="small"
                            :disabled="!canEditMovementTemplates || movementTemplateInlineActive"
                            @click="startCreateMovementTemplateInline"
                        >
                            {{ t('operationPlan.actions.add') }}
                        </el-button>
                    </div>
                </header>

                <div class="movement-template-context">
                    <el-tooltip
                        :content="getTrainTemplateNameTooltip(selectedTrainTemplate)"
                        placement="top"
                        :show-after="250"
                    >
                        <span class="operation-plan-hover-name">{{ selectedTrainTemplate.name }}</span>
                    </el-tooltip>
                </div>

                <el-table
                    :data="visibleMovementTemplates"
                    class="operation-plan-table"
                    size="small"
                    height="100%"
                    :row-key="getMovementTemplateRowKey"
                    :row-class-name="getMovementTemplateRowClassName"
                    :empty-text="movementTemplateEmptyText"
                    @row-dblclick="startEditMovementTemplateInline"
                >
                    <el-table-column
                        prop="name"
                        :label="t('operationPlan.movement.fields.name')"
                        min-width="190"
                    >
                        <template #default="{ row }">
                            <div
                                v-if="isMovementTemplateInlineEditing(row)"
                                class="operation-plan-name-edit-cell"
                                @click.stop
                            >
                                <el-input
                                    v-model="movementTemplateForm.movementID"
                                    size="small"
                                    clearable
                                    :placeholder="t('operationPlan.placeholders.autoId')"
                                />
                                <el-input
                                    v-model="movementTemplateForm.name"
                                    size="small"
                                    clearable
                                    :placeholder="t('operationPlan.movement.placeholders.name')"
                                />
                            </div>
                            <el-tooltip
                                v-else
                                :content="getMovementTemplateNameTooltip(row)"
                                placement="top"
                                :show-after="250"
                            >
                                <span class="operation-plan-hover-name">{{ row.name }}</span>
                            </el-tooltip>
                        </template>
                    </el-table-column>
                    <el-table-column
                        :label="t('operationPlan.movement.fields.routeIDList')"
                        min-width="240"
                    >
                        <template #default="{ row }">
                            <div
                                v-if="isMovementTemplateInlineEditing(row)"
                                class="operation-plan-route-edit-cell"
                                @click.stop
                            >
                                <div class="operation-plan-route-tags operation-plan-route-edit-tags">
                                    <template v-if="movementTemplateRouteIds.length > 0">
                                        <el-tag
                                            v-for="routeID in movementTemplateRouteIds"
                                            :key="routeID"
                                            size="small"
                                            effect="plain"
                                            :title="routeID"
                                        >
                                            {{ getRouteDisplayName(routeID) }}
                                        </el-tag>
                                    </template>
                                    <span v-else class="operation-plan-muted">
                                        {{ t('operationPlan.movement.placeholders.routeIDList') }}
                                    </span>
                                </div>
                                <el-button
                                    :icon="Filter"
                                    size="small"
                                    :loading="loadingStationRoutes"
                                    @click.stop="openRoutePicker"
                                >
                                    {{ t('operationPlan.movement.routePicker.openButton') }}
                                </el-button>
                            </div>
                            <div v-else class="operation-plan-route-tags">
                                <template v-if="parseRouteIDList(row.routeIDList).length > 0">
                                    <el-tag
                                        v-for="routeID in parseRouteIDList(row.routeIDList)"
                                        :key="routeID"
                                        size="small"
                                        effect="plain"
                                        :title="routeID"
                                    >
                                        {{ getRouteDisplayName(routeID) }}
                                    </el-tag>
                                </template>
                                <span v-else class="operation-plan-muted">-</span>
                            </div>
                        </template>
                    </el-table-column>
                    <el-table-column
                        prop="minDuration"
                        :label="t('operationPlan.movement.fields.minDuration')"
                        width="138"
                        align="right"
                    >
                        <template #default="{ row }">
                            <el-input-number
                                v-if="isMovementTemplateInlineEditing(row)"
                                v-model="movementTemplateForm.minDuration"
                                class="operation-plan-table-number-input"
                                controls-position="right"
                                size="small"
                                :min="0"
                                :step="1"
                                @click.stop
                            />
                            <span v-else>{{ row.minDuration ?? '' }}</span>
                        </template>
                    </el-table-column>
                    <el-table-column
                        :label="t('operationPlan.fields.operation')"
                        width="168"
                        fixed="right"
                        align="center"
                    >
                        <template #default="{ row }">
                            <div v-if="isMovementTemplateInlineEditing(row)" class="operation-plan-row-actions">
                                <el-button
                                    :icon="Check"
                                    circle
                                    size="small"
                                    type="primary"
                                    :loading="savingMovementTemplate"
                                    @click.stop="saveMovementTemplate"
                                />
                                <el-button
                                    :icon="Close"
                                    circle
                                    size="small"
                                    @click.stop="cancelMovementTemplateInline(row)"
                                />
                            </div>
                            <div v-else class="operation-plan-row-actions">
                                <el-button
                                    :icon="ArrowUp"
                                    circle
                                    size="small"
                                    :title="t('operationPlan.actions.moveUp')"
                                    :disabled="!canMoveMovementTemplate(row, -1)"
                                    @click.stop="moveMovementTemplate(row, -1)"
                                />
                                <el-button
                                    :icon="ArrowDown"
                                    circle
                                    size="small"
                                    :title="t('operationPlan.actions.moveDown')"
                                    :disabled="!canMoveMovementTemplate(row, 1)"
                                    @click.stop="moveMovementTemplate(row, 1)"
                                />
                                <el-button
                                    :icon="Edit"
                                    circle
                                    size="small"
                                    :disabled="!canEditMovementTemplates || movementTemplateInlineActive"
                                    @click.stop="startEditMovementTemplateInline(row)"
                                />
                                <el-button
                                    :icon="Delete"
                                    circle
                                    size="small"
                                    type="danger"
                                    :disabled="!canEditMovementTemplates || movementTemplateInlineActive"
                                    @click.stop="confirmDeleteMovementTemplate(row)"
                                />
                            </div>
                        </template>
                    </el-table-column>
                </el-table>
                    </section>
                </div>
            </el-tab-pane>
            <el-tab-pane
                :label="t('operationPlan.tabs.trainOperationPlan')"
                name="trainOperationPlan"
                class="operation-plan-sub-tab-pane"
            >
                <div class="train-operation-plan-panel">
                    <div class="train-operation-plan-toolbar">
                        <div class="train-operation-plan-time-range">
                            <span>{{ t('operationPlan.trainOperationPlan.timeRange') }}</span>
                            <el-input
                                v-model="trainOperationPlanStartTime"
                                size="small"
                                class="train-operation-plan-time-input"
                                :placeholder="t('operationPlan.trainOperationPlan.startTime')"
                                :disabled="generatingTrainOperationPlan"
                            />
                            <span>{{ t('operationPlan.trainOperationPlan.to') }}</span>
                            <el-input
                                v-model="trainOperationPlanEndTime"
                                size="small"
                                class="train-operation-plan-time-input"
                                :placeholder="t('operationPlan.trainOperationPlan.endTime')"
                                :disabled="generatingTrainOperationPlan"
                            />
                        </div>
                        <div class="operation-plan-card-actions">
                            <el-button
                                :icon="Refresh"
                                size="small"
                                :disabled="!canLoadTrainOperationPlan || generatingTrainOperationPlan || operationPlanInlineActive"
                                @click="loadTrainOperationPlan"
                            >
                                {{ t('operationPlan.actions.refresh') }}
                            </el-button>
                            <el-button
                                :icon="MagicStick"
                                type="primary"
                                size="small"
                                :loading="generatingTrainOperationPlan"
                                :disabled="!canGenerateTrainOperationPlan"
                                @click="generateTrainOperationPlan"
                            >
                                {{ t('operationPlan.trainOperationPlan.actions.autoGenerate') }}
                            </el-button>
                        </div>
                    </div>

                    <div
                        class="train-operation-plan-grid"
                        :class="{ 'is-expanded': selectedTrainOperationPlanTrain !== null }"
                        v-loading="loadingTrainOperationPlan"
                    >
                        <section class="operation-plan-card train-operation-plan-train-card">
                            <header class="operation-plan-card-header">
                                <div>
                                    <h2>{{ t('operationPlan.trainOperationPlan.train.title') }}</h2>
                                    <span>{{ trainOperationPlanTrainCountText }}</span>
                                </div>
                                <div class="operation-plan-card-actions">
                                    <el-button
                                        :icon="Plus"
                                        type="primary"
                                        size="small"
                                        :disabled="!canEditTrainOperationPlan || operationPlanInlineActive"
                                        @click="startCreateTrainOperationPlanTrainInline"
                                    >
                                        {{ t('operationPlan.actions.add') }}
                                    </el-button>
                                </div>
                            </header>
                            <el-table
                                :data="visibleTrainOperationPlanTrains"
                                class="operation-plan-table"
                                size="small"
                                height="100%"
                                :row-key="getTrainOperationPlanTrainRowKey"
                                :row-class-name="getTrainOperationPlanTrainRowClassName"
                                :empty-text="trainOperationPlanEmptyText"
                                highlight-current-row
                                @row-click="toggleTrainOperationPlanTrainExpansion"
                            >
                                <el-table-column width="56" align="center">
                                    <template #default="{ row }">
                                        <el-tag v-if="row.isDraft" size="small" type="success">
                                            {{ t('operationPlan.states.new') }}
                                        </el-tag>
                                        <el-tag v-else-if="isTrainOperationPlanTrainEditing(row)" size="small" type="warning">
                                            {{ t('operationPlan.states.editing') }}
                                        </el-tag>
                                        <el-button
                                            v-else
                                            :icon="isTrainOperationPlanTrainExpanded(row) ? ArrowDown : ArrowRight"
                                            size="small"
                                            text
                                            type="primary"
                                            :title="isTrainOperationPlanTrainExpanded(row) ? t('operationPlan.actions.collapse') : t('operationPlan.actions.expand')"
                                            @click.stop="toggleTrainOperationPlanTrainExpansion(row)"
                                        />
                                    </template>
                                </el-table-column>
                                <el-table-column prop="trainNumber" :label="t('operationPlan.trainOperationPlan.train.fields.trainNumber')" min-width="110" show-overflow-tooltip>
                                    <template #default="{ row }">
                                        <el-input
                                            v-if="isTrainOperationPlanTrainInlineEditing(row)"
                                            v-model="trainOperationPlanTrainForm.trainNumber"
                                            size="small"
                                            clearable
                                            :placeholder="t('operationPlan.placeholders.autoId')"
                                        />
                                        <span v-else>{{ row.trainNumber }}</span>
                                    </template>
                                </el-table-column>
                                <el-table-column prop="name" :label="t('operationPlan.train.fields.name')" min-width="170">
                                    <template #default="{ row }">
                                        <el-input
                                            v-if="isTrainOperationPlanTrainInlineEditing(row)"
                                            v-model="trainOperationPlanTrainForm.name"
                                            size="small"
                                            clearable
                                        />
                                        <el-tooltip
                                            v-else
                                            :content="getTrainOperationPlanTrainNameTooltip(row)"
                                            placement="top"
                                            :show-after="250"
                                        >
                                            <span class="operation-plan-hover-name">{{ row.name }}</span>
                                        </el-tooltip>
                                    </template>
                                </el-table-column>
                                <el-table-column prop="trainType" :label="t('operationPlan.trainOperationPlan.train.fields.trainType')" min-width="120" show-overflow-tooltip>
                                    <template #default="{ row }">
                                        <el-input
                                            v-if="isTrainOperationPlanTrainInlineEditing(row)"
                                            v-model="trainOperationPlanTrainForm.trainType"
                                            size="small"
                                            clearable
                                        />
                                        <span v-else>{{ row.trainType }}</span>
                                    </template>
                                </el-table-column>
                                <el-table-column
                                    prop="isFixedOperation"
                                    :label="t('operationPlan.trainOperationPlan.train.fields.isFixedOperation')"
                                    width="118"
                                    align="center"
                                >
                                    <template #default="{ row }">
                                        <el-checkbox
                                            v-if="isTrainOperationPlanTrainInlineEditing(row)"
                                            v-model="trainOperationPlanTrainForm.isFixedOperation"
                                            @click.stop
                                        />
                                        <el-checkbox
                                            v-else
                                            :model-value="row.isFixedOperation"
                                            :disabled="!canEditTrainOperationPlan || operationPlanInlineActive || savingTrainOperationPlanTrain"
                                            @click.stop
                                            @change="updateTrainOperationPlanTrainFixedOperation(row, Boolean($event))"
                                        />
                                    </template>
                                </el-table-column>
                                <el-table-column :label="t('operationPlan.fields.operation')" width="116" fixed="right" align="center">
                                    <template #default="{ row }">
                                        <div v-if="isTrainOperationPlanTrainInlineEditing(row)" class="operation-plan-row-actions">
                                            <el-button
                                                :icon="Check"
                                                circle
                                                size="small"
                                                type="primary"
                                                :loading="savingTrainOperationPlanTrain"
                                                @click.stop="saveTrainOperationPlanTrain"
                                            />
                                            <el-button
                                                :icon="Close"
                                                circle
                                                size="small"
                                                @click.stop="cancelTrainOperationPlanTrainInline(row)"
                                            />
                                        </div>
                                        <div v-else class="operation-plan-row-actions">
                                            <el-button
                                                :icon="Edit"
                                                circle
                                                size="small"
                                                :disabled="!canEditTrainOperationPlan || operationPlanInlineActive"
                                                @click.stop="startEditTrainOperationPlanTrainInline(row)"
                                            />
                                            <el-button
                                                :icon="Delete"
                                                circle
                                                size="small"
                                                type="danger"
                                                :disabled="!canEditTrainOperationPlan || operationPlanInlineActive"
                                                @click.stop="confirmDeleteTrainOperationPlanTrain(row)"
                                            />
                                        </div>
                                    </template>
                                </el-table-column>
                            </el-table>
                        </section>

                        <section
                            v-if="selectedTrainOperationPlanTrain"
                            class="operation-plan-card train-operation-plan-movement-card"
                        >
                            <header class="operation-plan-card-header">
                                <div>
                                    <h2>{{ t('operationPlan.trainOperationPlan.movement.title') }}</h2>
                                    <span>{{ trainOperationPlanMovementCountText }}</span>
                                </div>
                                <div class="operation-plan-card-actions">
                                    <el-button
                                        :icon="Plus"
                                        type="primary"
                                        size="small"
                                        :disabled="!canEditTrainOperationPlan || operationPlanInlineActive || !selectedTrainOperationPlanTrain"
                                        @click="startCreateTrainOperationPlanMovementInline"
                                    >
                                        {{ t('operationPlan.actions.add') }}
                                    </el-button>
                                </div>
                            </header>
                            <div class="movement-template-context">
                                <el-tooltip
                                    :content="getTrainOperationPlanTrainNameTooltip(selectedTrainOperationPlanTrain)"
                                    placement="top"
                                    :show-after="250"
                                >
                                    <span class="operation-plan-hover-name">{{ selectedTrainOperationPlanTrain.name || selectedTrainOperationPlanTrain.id }}</span>
                                </el-tooltip>
                            </div>
                            <el-table
                                :data="visibleTrainOperationPlanMovements"
                                class="operation-plan-table"
                                size="small"
                                height="100%"
                                :row-key="getTrainOperationPlanMovementRowKey"
                                :row-class-name="getTrainOperationPlanMovementRowClassName"
                                :empty-text="trainOperationPlanMovementEmptyText"
                            >
                                <el-table-column prop="name" :label="t('operationPlan.movement.fields.name')" min-width="170">
                                    <template #default="{ row }">
                                        <el-input
                                            v-if="isTrainOperationPlanMovementInlineEditing(row)"
                                            v-model="trainOperationPlanMovementForm.name"
                                            size="small"
                                            clearable
                                        />
                                        <el-tooltip
                                            v-else
                                            :content="getTrainOperationPlanMovementNameTooltip(row)"
                                            placement="top"
                                            :show-after="250"
                                        >
                                            <span class="operation-plan-hover-name">{{ row.name }}</span>
                                        </el-tooltip>
                                    </template>
                                </el-table-column>
                                <el-table-column prop="minDuration" :label="t('operationPlan.movement.fields.minDuration')" width="132" align="right">
                                    <template #default="{ row }">
                                        <el-input-number
                                            v-if="isTrainOperationPlanMovementInlineEditing(row)"
                                            v-model="trainOperationPlanMovementForm.minDuration"
                                            class="operation-plan-table-number-input"
                                            controls-position="right"
                                            size="small"
                                            :min="0"
                                            :step="1"
                                        />
                                        <span v-else>{{ row.minDuration ?? '' }}</span>
                                    </template>
                                </el-table-column>
                                <el-table-column prop="earliestStartTime" :label="t('operationPlan.trainOperationPlan.movement.fields.earliestStartTime')" width="150" show-overflow-tooltip>
                                    <template #default="{ row }">
                                        <el-input
                                            v-if="isTrainOperationPlanMovementInlineEditing(row)"
                                            v-model="trainOperationPlanMovementForm.earliestStartTime"
                                            size="small"
                                            clearable
                                        />
                                        <span v-else>{{ row.earliestStartTime }}</span>
                                    </template>
                                </el-table-column>
                                <el-table-column prop="latestEndTime" :label="t('operationPlan.trainOperationPlan.movement.fields.latestEndTime')" width="150" show-overflow-tooltip>
                                    <template #default="{ row }">
                                        <el-input
                                            v-if="isTrainOperationPlanMovementInlineEditing(row)"
                                            v-model="trainOperationPlanMovementForm.latestEndTime"
                                            size="small"
                                            clearable
                                        />
                                        <span v-else>{{ row.latestEndTime }}</span>
                                    </template>
                                </el-table-column>
                                <el-table-column prop="route" :label="t('operationPlan.trainOperationPlan.movement.fields.route')" min-width="140" show-overflow-tooltip>
                                    <template #default="{ row }">
                                        <div
                                            v-if="isTrainOperationPlanMovementInlineEditing(row)"
                                            class="operation-plan-route-edit-cell"
                                        >
                                            <div class="operation-plan-route-tags operation-plan-route-edit-tags">
                                                <el-tag
                                                    v-if="trainOperationPlanMovementForm.route"
                                                    size="small"
                                                    effect="plain"
                                                >
                                                    {{ getRouteDisplayName(trainOperationPlanMovementForm.route) }}
                                                </el-tag>
                                                <span v-else class="operation-plan-muted">
                                                    {{ t('operationPlan.trainOperationPlan.movement.placeholders.route') }}
                                                </span>
                                            </div>
                                            <el-button
                                                size="small"
                                                :loading="loadingStationRoutes"
                                                @click.stop="openRoutePicker('trainOperationPlanMovementRoute')"
                                            >
                                                {{ t('operationPlan.movement.routePicker.openButton') }}
                                            </el-button>
                                        </div>
                                        <span v-else>{{ getRouteDisplayName(row.route) }}</span>
                                    </template>
                                </el-table-column>
                                <el-table-column prop="tag" :label="t('operationPlan.trainOperationPlan.movement.fields.tag')" min-width="140" show-overflow-tooltip>
                                    <template #default="{ row }">
                                        <el-input
                                            v-if="isTrainOperationPlanMovementInlineEditing(row)"
                                            v-model="trainOperationPlanMovementForm.tag"
                                            size="small"
                                            clearable
                                        />
                                        <span v-else>{{ row.tag }}</span>
                                    </template>
                                </el-table-column>
                                <el-table-column :label="t('operationPlan.fields.operation')" width="168" fixed="right" align="center">
                                    <template #default="{ row }">
                                        <div v-if="isTrainOperationPlanMovementInlineEditing(row)" class="operation-plan-row-actions">
                                            <el-button
                                                :icon="Check"
                                                circle
                                                size="small"
                                                type="primary"
                                                :loading="savingTrainOperationPlanMovement"
                                                @click.stop="saveTrainOperationPlanMovement"
                                            />
                                            <el-button
                                                :icon="Close"
                                                circle
                                                size="small"
                                                @click.stop="cancelTrainOperationPlanMovementInline(row)"
                                            />
                                        </div>
                                        <div v-else class="operation-plan-row-actions">
                                            <el-button
                                                :icon="ArrowUp"
                                                circle
                                                size="small"
                                                :title="t('operationPlan.actions.moveUp')"
                                                :disabled="!canMoveTrainOperationPlanMovement(row, -1)"
                                                @click.stop="moveTrainOperationPlanMovement(row, -1)"
                                            />
                                            <el-button
                                                :icon="ArrowDown"
                                                circle
                                                size="small"
                                                :title="t('operationPlan.actions.moveDown')"
                                                :disabled="!canMoveTrainOperationPlanMovement(row, 1)"
                                                @click.stop="moveTrainOperationPlanMovement(row, 1)"
                                            />
                                            <el-button
                                                :icon="Edit"
                                                circle
                                                size="small"
                                                :disabled="!canEditTrainOperationPlan || operationPlanInlineActive"
                                                @click.stop="startEditTrainOperationPlanMovementInline(row)"
                                            />
                                            <el-button
                                                :icon="Delete"
                                                circle
                                                size="small"
                                                type="danger"
                                                :disabled="!canEditTrainOperationPlan || operationPlanInlineActive"
                                                @click.stop="confirmDeleteTrainOperationPlanMovement(row)"
                                            />
                                        </div>
                                    </template>
                                </el-table-column>
                            </el-table>
                        </section>
                    </div>
                </div>
            </el-tab-pane>

            <el-tab-pane
                :label="t('operationPlan.tabs.trainOperationChart')"
                name="trainOperationChart"
                class="operation-plan-sub-tab-pane"
            >
                <section
                    class="operation-plan-card operation-plan-chart-card"
                    v-loading="loadingOperationPlanChart || loadingTrainOperationPlan || loadingStationRoutes || loadingStationRouteEnds"
                >
                    <header class="operation-plan-card-header">
                        <div>
                            <h2>{{ t('operationPlan.trainOperationChart.title') }}</h2>
                            <span>{{ operationPlanChartCountText }}</span>
                        </div>
                        <div class="operation-plan-card-actions">
                            <el-button
                                :icon="Refresh"
                                size="small"
                                :disabled="!canLoadOperationPlanChart || operationPlanInlineActive"
                                @click="loadOperationPlanChartData"
                            >
                                {{ t('operationPlan.actions.refresh') }}
                            </el-button>
                        </div>
                    </header>

                    <div v-if="operationPlanChartBars.length === 0" class="operation-plan-chart-empty">
                        {{ operationPlanChartEmptyText }}
                    </div>
                    <div v-else class="operation-plan-chart-scroll">
                        <div class="operation-plan-chart-grid" :style="operationPlanChartGridStyle">
                            <div class="operation-plan-chart-corner">
                                {{ t('operationPlan.trainOperationChart.cellAxis') }}
                            </div>
                            <div class="operation-plan-chart-time-head">
                                <span class="operation-plan-chart-axis-title">
                                    {{ t('operationPlan.trainOperationChart.timeAxis') }}
                                </span>
                                <span
                                    v-for="tick in operationPlanChartTicks"
                                    :key="`operation-chart-head-${tick}`"
                                    class="operation-plan-chart-tick-label"
                                    :style="{ left: `${operationPlanChartTimeToX(tick)}px` }"
                                >
                                    {{ formatOperationPlanChartTime(tick) }}
                                </span>
                            </div>

                            <template v-for="row in operationPlanChartRows" :key="row.cellID">
                                <div class="operation-plan-chart-cell" :style="getOperationPlanChartRowStyle(row)" :title="row.cellName">
                                    {{ row.cellName }}
                                </div>
                                <div class="operation-plan-chart-track" :style="getOperationPlanChartRowStyle(row)">
                                    <span
                                        v-for="tick in operationPlanChartTicks"
                                        :key="`operation-chart-line-${row.cellID}-${tick}`"
                                        class="operation-plan-chart-grid-line"
                                        :style="{ left: `${operationPlanChartTimeToX(tick)}px` }"
                                    />
                                    <div
                                        v-for="bar in row.bars"
                                        :key="bar.key"
                                        class="operation-plan-chart-bar"
                                        :style="getOperationPlanChartBarStyle(bar)"
                                        :title="bar.title"
                                    >
                                        <span>{{ bar.label }}</span>
                                    </div>
                                </div>
                            </template>
                        </div>
                    </div>
                </section>
            </el-tab-pane>

            <el-tab-pane
                :label="t('operationPlan.tabs.operationOccupationTimeTable')"
                name="operationOccupationTimeTable"
                class="operation-plan-sub-tab-pane"
            >
                <section
                    class="operation-plan-card operation-occupation-time-card"
                    v-loading="loadingOperationPlanChart || loadingTrainOperationPlan || loadingStationRoutes || loadingStationRouteEnds"
                >
                    <header class="operation-plan-card-header">
                        <div>
                            <h2>{{ t('operationPlan.operationOccupationTimeTable.title') }}</h2>
                            <span>{{ operationOccupationTimeTableCountText }}</span>
                        </div>
                        <div class="operation-plan-card-actions">
                            <label class="operation-occupation-time-total-control">
                                <span>{{ t('operationPlan.operationOccupationTimeTable.totalTime') }}</span>
                                <el-input-number
                                    v-model="operationOccupationTotalTimeSeconds"
                                    :min="1"
                                    :step="60"
                                    :precision="0"
                                    size="small"
                                    controls-position="right"
                                    :placeholder="t('operationPlan.operationOccupationTimeTable.totalTimePlaceholder')"
                                />
                            </label>
                            <label class="operation-occupation-time-factor-control">
                                <span>{{ t('operationPlan.operationOccupationTimeTable.emptyWasteFactor') }}</span>
                                <el-input-number
                                    v-model="operationOccupationEmptyWasteFactor"
                                    :min="0"
                                    :max="0.99"
                                    :step="0.01"
                                    :precision="2"
                                    size="small"
                                    controls-position="right"
                                    :placeholder="t('operationPlan.operationOccupationTimeTable.emptyWasteFactorPlaceholder')"
                                />
                            </label>
                            <label class="operation-occupation-time-unit-control">
                                <span>{{ t('operationPlan.operationOccupationTimeTable.unitLabel') }}</span>
                                <el-radio-group v-model="operationOccupationTimeUnit" size="small">
                                    <el-radio-button value="seconds">
                                        {{ t('operationPlan.operationOccupationTimeTable.unitSeconds') }}
                                    </el-radio-button>
                                    <el-radio-button value="minutes">
                                        {{ t('operationPlan.operationOccupationTimeTable.unitMinutes') }}
                                    </el-radio-button>
                                </el-radio-group>
                            </label>
                            <el-button
                                :icon="Refresh"
                                size="small"
                                :disabled="!canLoadOperationPlanChart || operationPlanInlineActive"
                                @click="loadOperationPlanChartData"
                            >
                                {{ t('operationPlan.actions.refresh') }}
                            </el-button>
                        </div>
                    </header>

                    <div class="operation-occupation-time-subtable-panel">
                        <div class="operation-occupation-time-subtable-toolbar">
                            <el-tabs
                                v-model="activeOperationOccupationTimeSubTableId"
                                type="card"
                                class="operation-occupation-time-sub-tabs"
                                @tab-remove="removeOperationOccupationTimeSubTable"
                            >
                                <el-tab-pane
                                    v-for="(subTable, index) in operationOccupationTimeSubTables"
                                    :key="subTable.id"
                                    :name="subTable.id"
                                    :label="formatOperationOccupationTimeSubTableLabel(subTable, index)"
                                    :closable="operationOccupationTimeSubTables.length > 1"
                                />
                            </el-tabs>
                            <el-button
                                :icon="Edit"
                                circle
                                size="small"
                                :disabled="!activeOperationOccupationTimeSubTable"
                                :title="t('operationPlan.operationOccupationTimeTable.subTables.edit')"
                                @click="openEditOperationOccupationTimeSubTableDialog"
                            />
                            <el-button
                                :icon="Plus"
                                circle
                                size="small"
                                :title="t('operationPlan.operationOccupationTimeTable.subTables.add')"
                                @click="openCreateOperationOccupationTimeSubTableDialog"
                            />
                        </div>

                        <div class="operation-occupation-time-subtable-controls">
                            <span class="operation-occupation-time-subtable-summary">
                                {{ activeOperationOccupationTimeSubTableSummaryText }}
                            </span>
                        </div>

                        <el-table
                            class="operation-plan-table operation-occupation-time-table"
                            :data="displayOperationOccupationTimeTableRows"
                            row-key="rowKey"
                            height="100%"
                            border
                            :empty-text="operationOccupationTimeTableEmptyText"
                            :row-class-name="getOperationOccupationTimeTableRowClassName"
                            default-expand-all
                            :tree-props="{ children: 'children' }"
                        >
                            <el-table-column
                                prop="sequence"
                                :label="t('operationPlan.operationOccupationTimeTable.fields.sequence')"
                                width="92"
                                fixed
                            />
                            <el-table-column
                                prop="routeID"
                                :label="t('operationPlan.operationOccupationTimeTable.fields.routeID')"
                                min-width="132"
                                fixed
                                show-overflow-tooltip
                            />
                            <el-table-column
                                prop="routeName"
                                :label="t('operationPlan.operationOccupationTimeTable.fields.routeDescription')"
                                min-width="220"
                                fixed
                                show-overflow-tooltip
                            />
                            <el-table-column
                                prop="operationCount"
                                :label="t('operationPlan.operationOccupationTimeTable.fields.operationCount')"
                                min-width="128"
                                align="right"
                            />
                            <el-table-column
                                v-for="cell in activeOperationOccupationTimeSubTableCells"
                                :key="cell.id"
                                :label="cell.name"
                                min-width="66"
                                align="right"
                                show-overflow-tooltip
                            >
                                <template #default="{ row }">
                                    {{ formatOperationOccupationCellValue(row, cell.id) }}
                                </template>
                            </el-table-column>
                        </el-table>
                    </div>

                    <el-dialog
                        v-model="operationOccupationTimeSubTableDialogVisible"
                        :title="operationOccupationTimeSubTableDialogTitle"
                        width="560px"
                        class="operation-occupation-time-subtable-dialog"
                    >
                        <el-form
                            label-position="top"
                            class="operation-occupation-time-subtable-form"
                        >
                            <el-form-item :label="t('operationPlan.operationOccupationTimeTable.subTables.name')">
                                <el-input
                                    v-model="operationOccupationTimeSubTableDialogForm.name"
                                    maxlength="100"
                                    show-word-limit
                                    :placeholder="t('operationPlan.operationOccupationTimeTable.subTables.namePlaceholder')"
                                />
                            </el-form-item>
                            <el-form-item :label="t('operationPlan.operationOccupationTimeTable.subTables.cells')">
                                <el-select
                                    v-model="operationOccupationTimeSubTableDialogForm.cellIds"
                                    class="operation-occupation-time-subtable-dialog-cell-select"
                                    multiple
                                    filterable
                                    clearable
                                    collapse-tags
                                    collapse-tags-tooltip
                                    :placeholder="t('operationPlan.operationOccupationTimeTable.subTables.cellPlaceholder')"
                                >
                                    <el-option
                                        v-for="cell in displayOperationOccupationTimeTableCells"
                                        :key="cell.id"
                                        :label="cell.name || cell.id"
                                        :value="cell.id"
                                    />
                                </el-select>
                            </el-form-item>
                        </el-form>
                        <template #footer>
                            <el-button @click="operationOccupationTimeSubTableDialogVisible = false">
                                {{ t('operationPlan.actions.cancel') }}
                            </el-button>
                            <el-button
                                type="primary"
                                @click="confirmOperationOccupationTimeSubTableDialog"
                            >
                                {{ t('operationPlan.actions.confirm') }}
                            </el-button>
                        </template>
                    </el-dialog>
                </section>
            </el-tab-pane>

            <el-tab-pane
                :label="t('operationPlan.tabs.operationBottleneckAnalysis')"
                name="operationBottleneckAnalysis"
                class="operation-plan-sub-tab-pane"
            >
                <section
                    class="operation-plan-card operation-bottleneck-analysis-card"
                    v-loading="loadingOperationPlanChart || loadingTrainOperationPlan || loadingStationRoutes || loadingStationRouteEnds"
                >
                    <header class="operation-plan-card-header">
                        <div>
                            <h2>{{ t('operationPlan.operationBottleneckAnalysis.title') }}</h2>
                            <span>{{ operationBottleneckAnalysisCountText }}</span>
                        </div>
                        <div class="operation-plan-card-actions">
                            <label class="operation-occupation-time-total-control">
                                <span>{{ t('operationPlan.operationOccupationTimeTable.totalTime') }}</span>
                                <el-input-number
                                    v-model="operationOccupationTotalTimeSeconds"
                                    :min="1"
                                    :step="60"
                                    :precision="0"
                                    size="small"
                                    controls-position="right"
                                    :placeholder="t('operationPlan.operationOccupationTimeTable.totalTimePlaceholder')"
                                />
                            </label>
                            <label class="operation-occupation-time-factor-control">
                                <span>{{ t('operationPlan.operationOccupationTimeTable.emptyWasteFactor') }}</span>
                                <el-input-number
                                    v-model="operationOccupationEmptyWasteFactor"
                                    :min="0"
                                    :max="0.99"
                                    :step="0.01"
                                    :precision="2"
                                    size="small"
                                    controls-position="right"
                                    :placeholder="t('operationPlan.operationOccupationTimeTable.emptyWasteFactorPlaceholder')"
                                />
                            </label>
                            <el-button
                                :icon="Refresh"
                                size="small"
                                :disabled="!canLoadOperationPlanChart || operationPlanInlineActive"
                                @click="loadOperationPlanChartData"
                            >
                                {{ t('operationPlan.actions.refresh') }}
                            </el-button>
                        </div>
                    </header>

                    <el-table
                        class="operation-plan-table operation-bottleneck-analysis-table operation-bottleneck-analysis-detail-table"
                        :data="displayOperationBottleneckAnalysisRows"
                        height="100%"
                        border
                        :empty-text="operationBottleneckAnalysisEmptyText"
                    >
                        <el-table-column
                            prop="routeID"
                            :label="t('operationPlan.operationBottleneckAnalysis.fields.routeID')"
                            min-width="132"
                            show-overflow-tooltip
                        />
                        <el-table-column
                            prop="routeName"
                            :label="t('operationPlan.operationBottleneckAnalysis.fields.routeDescription')"
                            min-width="260"
                            show-overflow-tooltip
                        />
                        <el-table-column
                            prop="operationCount"
                            :label="t('operationPlan.operationBottleneckAnalysis.fields.operationCount')"
                            min-width="150"
                            align="right"
                        />
                        <el-table-column
                            prop="bottleneckCellName"
                            :label="t('operationPlan.operationBottleneckAnalysis.fields.bottleneckCellName')"
                            min-width="220"
                            show-overflow-tooltip
                        />
                        <el-table-column
                            :label="t('operationPlan.operationBottleneckAnalysis.fields.bottleneckUtilization')"
                            min-width="160"
                            align="right"
                        >
                            <template #default="{ row }">
                                {{ formatOperationOccupationUtilization(row.bottleneckUtilization) }}
                            </template>
                        </el-table-column>
                        <el-table-column
                            :label="t('operationPlan.operationBottleneckAnalysis.fields.throughputCapacity')"
                            min-width="140"
                            align="right"
                        >
                            <template #default="{ row }">
                                {{ formatOperationBottleneckCapacity(row.throughputCapacity) }}
                            </template>
                        </el-table-column>
                    </el-table>
                </section>
            </el-tab-pane>

            <el-tab-pane
                :label="t('operationPlan.tabs.operationThroughputSummary')"
                name="operationThroughputSummary"
                class="operation-plan-sub-tab-pane"
            >
                <section
                    class="operation-plan-card operation-bottleneck-summary-card"
                    v-loading="loadingOperationPlanChart || loadingTrainOperationPlan || loadingStationRoutes || loadingStationRouteEnds || loadingOperationBottleneckSummaryCategories"
                >
                    <header class="operation-plan-card-header">
                        <div>
                            <h2>{{ t('operationPlan.operationBottleneckAnalysis.summary.title') }}</h2>
                            <span>{{ operationBottleneckSummaryCountText }}</span>
                        </div>
                        <div class="operation-bottleneck-summary-actions">
                            <el-button
                                :icon="Refresh"
                                size="small"
                                :loading="savingOperationBottleneckSummaryCategories"
                                :disabled="!hasScope || loadingOperationBottleneckSummaryCategories || savingOperationBottleneckSummaryCategories"
                                @click="calculateOperationBottleneckSummary"
                            >
                                {{ t('operationPlan.operationBottleneckAnalysis.summary.actions.calculate') }}
                            </el-button>
                            <el-button
                                :icon="Plus"
                                size="small"
                                type="primary"
                                :disabled="!hasScope || loadingOperationBottleneckSummaryCategories || savingOperationBottleneckSummaryCategories"
                                @click="addOperationBottleneckSummaryCategory"
                            >
                                {{ t('operationPlan.operationBottleneckAnalysis.summary.actions.addCategory') }}
                            </el-button>
                        </div>
                    </header>
                    <el-table
                        class="operation-plan-table operation-bottleneck-summary-table"
                        :data="displayOperationBottleneckSummaryRows"
                        height="100%"
                        border
                        :empty-text="operationBottleneckSummaryEmptyText"
                    >
                        <el-table-column
                            prop="groupText"
                            :label="t('operationPlan.operationBottleneckAnalysis.summary.fields.groupText')"
                            min-width="260"
                        >
                            <template #default="{ row }">
                                <el-input
                                    :model-value="row.groupText"
                                    size="small"
                                    @input="updateOperationBottleneckSummaryCategoryName(row.categoryID, String($event))"
                                />
                            </template>
                        </el-table-column>
                        <el-table-column
                            :label="t('operationPlan.operationBottleneckAnalysis.summary.fields.selectedRoutes')"
                            min-width="180"
                            show-overflow-tooltip
                        >
                            <template #default="{ row }">
                                <el-button size="small" @click="openOperationBottleneckRoutePicker(row.categoryID)">
                                    {{ getOperationBottleneckSummarySelectionText(row) }}
                                </el-button>
                            </template>
                        </el-table-column>
                        <el-table-column
                            prop="routeCount"
                            :label="t('operationPlan.operationBottleneckAnalysis.summary.fields.routeCount')"
                            min-width="96"
                            align="right"
                        />
                        <el-table-column
                            prop="operationCount"
                            :label="t('operationPlan.operationBottleneckAnalysis.summary.fields.operationCount')"
                            min-width="120"
                            align="right"
                        />
                        <el-table-column
                            :label="t('operationPlan.operationBottleneckAnalysis.summary.fields.capacityTotal')"
                            min-width="128"
                            align="right"
                        >
                            <template #default="{ row }">
                                {{ formatOperationBottleneckCapacity(row.capacityTotal) }}
                            </template>
                        </el-table-column>
                        <el-table-column
                            :label="t('operationPlan.operationBottleneckAnalysis.summary.fields.capacityAverage')"
                            min-width="128"
                            align="right"
                        >
                            <template #default="{ row }">
                                {{ formatOperationBottleneckCapacity(row.capacityAverage) }}
                            </template>
                        </el-table-column>
                        <el-table-column
                            :label="t('operationPlan.fields.operation')"
                            width="88"
                            fixed="right"
                            align="center"
                        >
                            <template #default="{ row }">
                                <el-button
                                    :icon="Delete"
                                    circle
                                    size="small"
                                    type="danger"
                                    @click="deleteOperationBottleneckSummaryCategory(row.categoryID)"
                                />
                            </template>
                        </el-table-column>
                    </el-table>
                </section>
            </el-tab-pane>
        </el-tabs>

        <el-dialog
            v-model="routePickerVisible"
            :title="t('operationPlan.movement.routePicker.title')"
            fullscreen
            class="operation-plan-route-picker-dialog"
            :close-on-click-modal="false"
            @opened="handleRoutePickerOpened"
            @closed="handleRoutePickerClosed"
        >
            <div class="operation-plan-route-picker">
                <div class="operation-plan-route-picker-toolbar">
                    <span>{{ routePickerSummaryText }}</span>
                    <el-popover
                        placement="bottom-start"
                        trigger="click"
                        width="420"
                        popper-class="operation-plan-route-filter-popover"
                    >
                        <template #reference>
                            <el-button
                                :icon="Filter"
                                circle
                                size="small"
                                :type="routePickerFiltersActive ? 'primary' : 'default'"
                                :title="t('routeDesign.stationRoute.actions.filter')"
                            />
                        </template>
                        <div class="operation-plan-route-filter-panel">
                            <el-select
                                v-model="routePickerFilters.types"
                                multiple
                                filterable
                                clearable
                                collapse-tags
                                collapse-tags-tooltip
                                :reserve-keyword="false"
                                size="small"
                                class="operation-plan-route-filter-control"
                                :placeholder="t('routeDesign.stationRoute.filter.type')"
                            >
                                <el-option
                                    v-for="option in routePickerTypeFilterOptions"
                                    :key="`operation-route-filter-type-${option.id}`"
                                    :label="option.name"
                                    :value="option.id"
                                />
                            </el-select>
                            <el-select
                                v-for="filter in routePickerFilterFieldControls"
                                :key="filter.field"
                                v-model="routePickerFilters[filter.field]"
                                multiple
                                filterable
                                clearable
                                collapse-tags
                                collapse-tags-tooltip
                                :reserve-keyword="false"
                                size="small"
                                class="operation-plan-route-filter-control"
                                :placeholder="t(filter.placeholderKey)"
                            >
                                <el-option
                                    v-for="option in getRoutePickerFilterSelectOptions(filter)"
                                    :key="`operation-route-filter-${filter.field}-${option.id}`"
                                    :label="option.name"
                                    :value="option.id"
                                >
                                    <div class="operation-plan-route-option">
                                        <span>{{ option.name }}</span>
                                        <small v-if="option.id !== option.name">{{ option.id }}</small>
                                    </div>
                                </el-option>
                            </el-select>
                            <el-button
                                :icon="Close"
                                size="small"
                                class="operation-plan-route-filter-clear"
                                :disabled="!routePickerFiltersActive"
                                @click="clearRoutePickerFilters"
                            >
                                {{ t('routeDesign.stationRoute.actions.clearFilters') }}
                            </el-button>
                        </div>
                    </el-popover>
                </div>

                <div ref="routePickerSplitRef" class="operation-plan-route-picker-split">
                    <div
                        class="operation-plan-route-picker-table-pane"
                        :style="{ height: `${routePickerTableHeight}px` }"
                    >
                        <el-table
                            :data="filteredRoutePickerRoutes"
                            size="small"
                            height="100%"
                            row-key="id"
                            highlight-current-row
                            :current-row-key="routePickerPreviewRouteId"
                            class="operation-plan-route-picker-table"
                            :empty-text="routePickerEmptyText"
                            @row-click="selectRoutePickerPreviewRoute"
                        >
                            <el-table-column width="44" align="center">
                                <template #header>
                                    <el-checkbox
                                        v-if="!routePickerSingleSelect"
                                        :model-value="routePickerFilteredAllSelected"
                                        :indeterminate="routePickerFilteredPartlySelected"
                                        :disabled="filteredRoutePickerRoutes.length === 0"
                                        @change="toggleRoutePickerFilteredSelection"
                                    />
                                </template>
                                <template #default="{ row }">
                                    <el-checkbox
                                        :model-value="isRoutePickerRouteSelected(row.id)"
                                        @change="toggleRoutePickerRoute(row.id, $event)"
                                        @click.stop
                                    />
                                </template>
                            </el-table-column>
                            <el-table-column prop="id" :label="t('routeDesign.stationRoute.fields.id')" min-width="112" show-overflow-tooltip />
                            <el-table-column prop="type" :label="t('routeDesign.stationRoute.fields.type')" min-width="92" show-overflow-tooltip />
                            <el-table-column prop="name" :label="t('routeDesign.stationRoute.fields.description')" min-width="220" show-overflow-tooltip />
                            <el-table-column prop="startNodeID" :label="t('routeDesign.stationRoute.fields.startNodeID')" width="92" show-overflow-tooltip />
                            <el-table-column prop="endNodeID" :label="t('routeDesign.stationRoute.fields.endNodeID')" width="92" show-overflow-tooltip />
                        </el-table>
                    </div>

                    <div
                        class="operation-plan-route-picker-splitter"
                        role="separator"
                        tabindex="0"
                        aria-orientation="horizontal"
                        :aria-valuenow="routePickerTableHeight"
                        @pointerdown="startRoutePickerTableResize"
                        @keydown="handleRoutePickerTableResizeKeydown"
                    >
                        <span />
                    </div>

                    <div class="operation-plan-route-picker-layout">
                        <div class="operation-plan-route-picker-layout-header">
                            <div class="operation-plan-route-picker-layout-title">
                                <span>{{ t('operationPlan.movement.routePicker.previewTitle') }}</span>
                                <strong v-if="selectedRoutePickerPreviewRoute">{{ selectedRoutePickerPreviewRoute.name }}</strong>
                            </div>
                            <div class="operation-plan-route-picker-node-filter">
                                <el-tag
                                    size="small"
                                    :type="routePickerNodeFilterStage === 'start' ? 'warning' : 'info'"
                                >
                                    {{ routePickerStartNodeFilterText }}
                                </el-tag>
                                <el-tag
                                    size="small"
                                    :type="routePickerNodeFilterStage === 'end' ? 'warning' : 'info'"
                                >
                                    {{ routePickerEndNodeFilterText }}
                                </el-tag>
                                <el-button
                                    v-if="routePickerNodeFiltersActive"
                                    :icon="Close"
                                    circle
                                    text
                                    size="small"
                                    :title="t('routeDesign.stationRoute.actions.clearFilters')"
                                    @click="clearRoutePickerNodeFilters"
                                />
                            </div>
                        </div>
                        <div
                            ref="routePickerLayoutViewportRef"
                            class="operation-plan-route-picker-layout-view"
                            v-loading="loadingRoutePickerLayout"
                        >
                            <StationLayoutEditor
                                v-if="routePickerLayoutData"
                                ref="routePickerLayoutEditorRef"
                                readonly
                                :display-scale-x="routePickerLayoutScaleX"
                                :display-scale-y="routePickerLayoutScaleY"
                                :display-styles="routePickerLayoutDisplayStyles"
                                :show-grid="false"
                                :show-nodes="true"
                                :show-curve-arc="true"
                                :grid-spacing="routePickerLayoutGridSpacing"
                                :cells="routePickerLayoutCells"
                                :show-cell-names="false"
                                :route-pick-target="routePickerNodePickTarget"
                                :highlighted-route-node-ids="routePickerHighlightedRouteNodeIds"
                                :highlighted-route-link-ids="routePickerHighlightedRouteLinkIds"
                                :highlighted-route-arrow-node-ids="routePickerHighlightedRouteArrowNodeIds"
                                :highlighted-route-color="routePickerHighlightedRouteColor"
                                :highlighted-route-arrow-visible="routePickerHighlightedRouteArrowVisible"
                                @route-node-pick="handleRoutePickerNodePick"
                            />
                            <div v-else class="operation-plan-route-picker-layout-empty">
                                {{ t('operationPlan.movement.routePicker.previewEmpty') }}
                            </div>
                        </div>
                    </div>
                </div>
            </div>

            <template #footer>
                <el-button :icon="Close" @click="closeRoutePicker">
                    {{ t('operationPlan.actions.cancel') }}
                </el-button>
                <el-button :icon="Check" type="primary" @click="confirmRoutePicker">
                    {{ t('operationPlan.actions.confirm') }}
                </el-button>
            </template>
        </el-dialog>

        <el-dialog
            v-model="operationBottleneckRoutePickerVisible"
            :title="t('operationPlan.operationBottleneckAnalysis.summary.routePicker.title')"
            width="960px"
            top="6vh"
            class="operation-bottleneck-route-picker-dialog"
            :close-on-click-modal="false"
        >
            <div class="operation-bottleneck-route-picker">
                <div class="operation-bottleneck-route-picker-toolbar">
                    <span>{{ operationBottleneckRoutePickerSummaryText }}</span>
                    <div class="operation-bottleneck-route-picker-filters">
                        <el-input
                            v-model="operationBottleneckRoutePickerFilters.keyword"
                            size="small"
                            clearable
                            :placeholder="t('operationPlan.operationBottleneckAnalysis.summary.routePicker.filters.keyword')"
                        />
                        <el-select
                            v-model="operationBottleneckRoutePickerFilters.startRouteEndIds"
                            size="small"
                            multiple
                            filterable
                            clearable
                            collapse-tags
                            :placeholder="t('operationPlan.operationBottleneckAnalysis.summary.routePicker.filters.startRouteEnd')"
                        >
                            <el-option
                                v-for="option in operationBottleneckRouteEndFilterOptions"
                                :key="`start-${option.id}`"
                                :label="option.name"
                                :value="option.id"
                            />
                        </el-select>
                        <el-select
                            v-model="operationBottleneckRoutePickerFilters.endRouteEndIds"
                            size="small"
                            multiple
                            filterable
                            clearable
                            collapse-tags
                            :placeholder="t('operationPlan.operationBottleneckAnalysis.summary.routePicker.filters.endRouteEnd')"
                        >
                            <el-option
                                v-for="option in operationBottleneckRouteEndFilterOptions"
                                :key="`end-${option.id}`"
                                :label="option.name"
                                :value="option.id"
                            />
                        </el-select>
                        <el-button size="small" :disabled="!operationBottleneckRoutePickerFiltersActive" @click="clearOperationBottleneckRoutePickerFilters">
                            {{ t('routeDesign.stationRoute.actions.clearFilters') }}
                        </el-button>
                    </div>
                </div>

                <el-table
                    class="operation-bottleneck-route-picker-table"
                    :data="filteredOperationBottleneckRoutePickerRows"
                    height="420"
                    border
                    :empty-text="operationBottleneckRoutePickerEmptyText"
                >
                    <el-table-column width="48" align="center">
                        <template #header>
                            <el-checkbox
                                :model-value="operationBottleneckRoutePickerFilteredAllSelected"
                                :indeterminate="operationBottleneckRoutePickerFilteredPartlySelected"
                                :disabled="filteredOperationBottleneckRoutePickerRows.length === 0"
                                @change="toggleOperationBottleneckRoutePickerFilteredSelection"
                            />
                        </template>
                        <template #default="{ row }">
                            <el-checkbox
                                :model-value="isOperationBottleneckRoutePickerRouteSelected(row.routeID)"
                                @change="toggleOperationBottleneckRoutePickerRoute(row.routeID, $event)"
                                @click.stop
                            />
                        </template>
                    </el-table-column>
                    <el-table-column prop="routeID" :label="t('operationPlan.operationBottleneckAnalysis.fields.routeID')" min-width="120" show-overflow-tooltip />
                    <el-table-column prop="routeName" :label="t('operationPlan.operationBottleneckAnalysis.fields.routeDescription')" min-width="220" show-overflow-tooltip />
                    <el-table-column prop="startRouteEndName" :label="t('operationPlan.operationBottleneckAnalysis.summary.groupFields.startRouteEnd')" min-width="160" show-overflow-tooltip />
                    <el-table-column prop="endRouteEndName" :label="t('operationPlan.operationBottleneckAnalysis.summary.groupFields.endRouteEnd')" min-width="160" show-overflow-tooltip />
                    <el-table-column prop="operationCount" :label="t('operationPlan.operationBottleneckAnalysis.fields.operationCount')" width="128" align="right" />
                    <el-table-column :label="t('operationPlan.operationBottleneckAnalysis.fields.throughputCapacity')" width="120" align="right">
                        <template #default="{ row }">
                            {{ formatOperationBottleneckCapacity(row.throughputCapacity) }}
                        </template>
                    </el-table-column>
                </el-table>
            </div>

            <template #footer>
                <el-button :icon="Close" @click="closeOperationBottleneckRoutePicker">
                    {{ t('operationPlan.actions.cancel') }}
                </el-button>
                <el-button :icon="Check" type="primary" @click="confirmOperationBottleneckRoutePicker">
                    {{ t('operationPlan.actions.confirm') }}
                </el-button>
            </template>
        </el-dialog>

    </section>
</template>

<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { ElMessage, ElMessageBox } from 'element-plus'
import { ArrowDown, ArrowRight, ArrowUp, Check, Close, CopyDocument, Delete, Edit, Filter, MagicStick, Plus, Refresh, Setting } from '@element-plus/icons-vue'
import axios from '@/utils/axios'
import StationLayoutEditor from './components/StationLayoutEditor.vue'

interface StationSchemeOption {
    id: string
    name: string
}

interface StationOperationPlan {
    instanceID: string
    stationSchemeID: string
    operationPlanID: string
    name: string
    description: string
    sortOrder: number | null
    createdDate?: string
    updatedDate?: string
    isDraft?: boolean
}

interface StationRouteOption {
    id: string
    name: string
    type: string
    label: string
    nodeList: string
    linkList: string
    switchList: string
    cellList: string
    interruptCellList: string
    signalList: string
    startNodeID: string
    endNodeID: string
}

interface StationRouteEndOption {
    instanceID: string
    stationSchemeID: string
    id: string
    bindingNodeID: string
    type: string
    segmentTag: string
    sidingTag: string
}

interface TrainTemplate {
    instanceID: string
    stationSchemeID: string
    operationPlanID: string
    trainTemplateID: string
    name: string
    type: string
    number: number | null
    isFixedOperation: boolean
    isDraft?: boolean
}

interface MovementTemplate {
    instanceID: string
    stationSchemeID: string
    operationPlanID: string
    trainTemplateID: string
    movementID: string
    name: string
    routeIDList: string
    minDuration: number | null
    sortOrder: number | null
    isDraft?: boolean
}

interface TrainOperationPlanTrain {
    instanceID: string
    stationSchemeID: string
    operationPlanID: string
    id: string
    trainTemplateID: string
    trainNumber: string
    name: string
    trainType: string
    isFixedOperation: boolean
    isDraft?: boolean
}

interface TrainOperationPlanMovement {
    instanceID: string
    stationSchemeID: string
    operationPlanID: string
    trainID: string
    trainTemplateID: string
    movementID: string
    name: string
    routeIDList: string
    minDuration: number | null
    earliestStartTime: string
    latestEndTime: string
    route: string
    tag: string
    sortOrder: number | null
    isDraft?: boolean
}

type TemplateEditMode = 'create' | 'edit'
type OperationPlanSubTab = 'trainTemplate' | 'trainOperationPlan' | 'trainOperationChart' | 'operationOccupationTimeTable' | 'operationBottleneckAnalysis' | 'operationThroughputSummary'
type OperationOccupationTimeUnit = 'seconds' | 'minutes'
type RoutePickerTarget = 'movementTemplate' | 'trainOperationPlanMovement' | 'trainOperationPlanMovementRoute'
type RoutePickerFilterField = 'types' | 'startNodeIds' | 'endNodeIds' | 'nodeIds' | 'linkIds' | 'cellIds' | 'switchIds' | 'signalIds'
type RoutePickerObjectFilterField = Exclude<RoutePickerFilterField, 'types'>
type RoutePickerFilters = Record<RoutePickerFilterField, string[]>
type RoutePickerNodeFilterStage = 'start' | 'end'

interface RouteNodePickPayload {
    target?: string
    nodeId?: string
    nodeID?: string
}

interface RoutePickerResizeState {
    startY: number
    startTableHeight: number
}

interface RouteListSelectOption {
    id: string
    name: string
}

interface RoutePickerFilterControl {
    field: RoutePickerObjectFilterField
    placeholderKey: string
}

interface StationRouteTimeOption {
    routeID: string
    trainTypeID: string
    cellID: string
    startOccupationShift: number | null
    endOccupationShift: number | null
    isInterruptCell: boolean
}

interface OperationPlanChartCell {
    id: string
    name: string
}

interface OperationPlanChartBar {
    key: string
    cellID: string
    trainID: string
    trainNumber: string
    isFixedOperation: boolean
    movementID: string
    movementName: string
    routeID: string
    routeName: string
    isInterruptCell: boolean
    startMinutes: number
    endMinutes: number
    lane: number
    label: string
    color: string
    title: string
}

interface OperationPlanChartRow {
    cellID: string
    cellName: string
    bars: OperationPlanChartBar[]
    laneCount: number
}

type OperationOccupationTimeTableRowType = 'group' | 'route' | 'fixed-total' | 'total' | 'utilization'

interface OperationOccupationTimeTableRow {
    rowKey: string
    rowType: OperationOccupationTimeTableRowType
    sequence: number | string
    routeID: string
    routeName: string
    operationCount: number | string
    cellDurations: Record<string, number>
    interruptCellDurations: Record<string, number>
    isFixedOperation?: boolean
    children?: OperationOccupationTimeTableRow[]
}

interface OperationOccupationTimeSubTable {
    id: string
    name: string
    cellIds: string[]
    hasCustomSelection: boolean
}

interface OperationOccupationTimeSubTableSettingPayload {
    subTableID: string
    subTableName: string
    cellIDs: string[]
    sortOrder: number
}

interface OperationOccupationTimeSubTableDialogForm {
    name: string
    cellIds: string[]
}

interface OperationOccupationRouteStats {
    routeID: string
    routeName: string
    operationCount: number
    cellDurations: Record<string, number>
    interruptCellDurations: Record<string, number>
    isFixedOperation?: boolean
}

interface OperationBottleneckAnalysisRow {
    routeID: string
    routeName: string
    operationCount: number
    bottleneckCellID: string
    bottleneckCellName: string
    bottleneckUtilization: number | null
    throughputCapacity: number | null
}

interface OperationBottleneckSummaryRow {
    categoryID: string
    groupKey: string
    groupText: string
    routeIDs: string[]
    routeCount: number
    operationCount: number
    capacityTotal: number | null
    capacityAverage: number | null
}

interface OperationAnalysisSnapshot {
    totalTimeSeconds: number | null
    cells: OperationPlanChartCell[]
    occupationTimeTableRows: OperationOccupationTimeTableRow[]
    bottleneckAnalysisRows: OperationBottleneckAnalysisRow[]
    throughputSummaryRows: OperationBottleneckSummaryRow[]
    updatedDate?: string
}

interface OperationBottleneckSummaryCategory {
    id: string
    name: string
    routeIDs: string[]
    sortOrder: number
}

interface OperationBottleneckRoutePickerFilters {
    keyword: string
    startRouteEndIds: string[]
    endRouteEndIds: string[]
}

interface OperationBottleneckRoutePickerRow extends OperationBottleneckAnalysisRow {
    startRouteEndID: string
    startRouteEndName: string
    endRouteEndID: string
    endRouteEndName: string
}

const routePickerFilterFieldControls: RoutePickerFilterControl[] = [
    { field: 'startNodeIds', placeholderKey: 'routeDesign.stationRoute.filter.startNode' },
    { field: 'endNodeIds', placeholderKey: 'routeDesign.stationRoute.filter.endNode' },
    { field: 'nodeIds', placeholderKey: 'routeDesign.stationRoute.filter.node' },
    { field: 'linkIds', placeholderKey: 'routeDesign.stationRoute.filter.link' },
    { field: 'cellIds', placeholderKey: 'routeDesign.stationRoute.filter.cell' },
    { field: 'switchIds', placeholderKey: 'routeDesign.stationRoute.filter.switch' },
    { field: 'signalIds', placeholderKey: 'routeDesign.stationRoute.filter.signal' },
]
const routePickerNodePickTarget = 'operation-plan-route-picker'
const routePickerDefaultTableHeight = 360
const routePickerMinTableHeight = 160
const routePickerMinLayoutHeight = 220
const routePickerSplitterHeight = 10
const props = defineProps<{
    selectedInstanceId: string
}>()

const { t } = useI18n()

const defaultOperationPlanID = 'default'
const operationOccupationTimeDefaultSubTableCount = 3
const currentStationSchemeId = ref('')
const currentOperationPlanId = ref('')
const activeOperationPlanTab = ref<OperationPlanSubTab>('trainTemplate')
const stationSchemeOptions = ref<StationSchemeOption[]>([])
const operationPlanOptions = ref<StationOperationPlan[]>([])
const stationRouteOptions = ref<StationRouteOption[]>([])
const stationRouteEndOptions = ref<StationRouteEndOption[]>([])
const stationLayoutCells = ref<OperationPlanChartCell[]>([])
const trainTemplates = ref<TrainTemplate[]>([])
const movementTemplates = ref<MovementTemplate[]>([])
const trainOperationPlanTrains = ref<TrainOperationPlanTrain[]>([])
const trainOperationPlanMovements = ref<TrainOperationPlanMovement[]>([])
const stationRouteTimesByKey = ref<Record<string, StationRouteTimeOption[]>>({})
const selectedTrainTemplateId = ref('')
const selectedTrainOperationPlanTrainId = ref('')
const operationOccupationTotalTimeSeconds = ref<number | null>(86400)
const operationOccupationEmptyWasteFactor = ref(0.2)
const operationOccupationTimeUnit = ref<OperationOccupationTimeUnit>('seconds')
const operationOccupationTimeSubTableSequence = ref(operationOccupationTimeDefaultSubTableCount)
const operationOccupationTimeSubTables = ref<OperationOccupationTimeSubTable[]>(
    Array.from({ length: operationOccupationTimeDefaultSubTableCount }, (_, index) => createOperationOccupationTimeSubTable(index + 1)),
)
const activeOperationOccupationTimeSubTableId = ref(operationOccupationTimeSubTables.value[0]?.id || '')
const loadingOperationOccupationTimeSubTableSettings = ref(false)
const savingOperationOccupationTimeSubTableSettings = ref(false)
const operationOccupationTimeSubTableDialogVisible = ref(false)
const operationOccupationTimeSubTableDialogMode = ref<'create' | 'edit'>('create')
const operationOccupationTimeSubTableDialogTargetId = ref('')
const operationOccupationTimeSubTableDialogTargetSequence = ref(0)
const operationOccupationTimeSubTableDialogForm = ref<OperationOccupationTimeSubTableDialogForm>({
    name: '',
    cellIds: [],
})
const operationBottleneckSummaryCategories = ref<OperationBottleneckSummaryCategory[]>([])
const operationAnalysisSnapshot = ref<OperationAnalysisSnapshot | null>(null)
const usingOperationAnalysisSnapshot = ref(false)
const operationBottleneckRoutePickerVisible = ref(false)
const operationBottleneckRoutePickerCategoryId = ref('')
const operationBottleneckRoutePickerSelectedIds = ref<string[]>([])
const operationBottleneckRoutePickerFilters = ref<OperationBottleneckRoutePickerFilters>({
    keyword: '',
    startRouteEndIds: [],
    endRouteEndIds: [],
})

const loadingStationSchemes = ref(false)
const loadingOperationPlans = ref(false)
const loadingStationRoutes = ref(false)
const loadingStationRouteEnds = ref(false)
const loadingTrainTemplates = ref(false)
const loadingMovementTemplates = ref(false)
const loadingTrainOperationPlan = ref(false)
const loadingOperationPlanChart = ref(false)
const loadingOperationBottleneckSummaryCategories = ref(false)
const savingOperationAnalysisSnapshot = ref(false)
const savingOperationPlanObject = ref(false)
const savingTrainTemplate = ref(false)
const savingMovementTemplate = ref(false)
const savingTrainOperationPlanTrain = ref(false)
const savingTrainOperationPlanMovement = ref(false)
const savingOperationBottleneckSummaryCategories = ref(false)
const generatingTrainOperationPlan = ref(false)
const trainTemplateCreating = ref(false)
const movementTemplateCreating = ref(false)
const trainOperationPlanTrainCreating = ref(false)
const trainOperationPlanMovementCreating = ref(false)
const trainTemplateEditingId = ref('')
const movementTemplateEditingId = ref('')
const trainOperationPlanTrainEditingId = ref('')
const trainOperationPlanMovementEditingKey = ref('')

const trainTemplateMode = ref<TemplateEditMode>('create')
const trainTemplateOriginalId = ref('')
const trainTemplateForm = ref(createEmptyTrainTemplate())

const movementTemplateMode = ref<TemplateEditMode>('create')
const movementTemplateOriginalId = ref('')
const movementTemplateForm = ref(createEmptyMovementTemplate())
const movementTemplateRouteIds = ref<string[]>([])
const trainOperationPlanTrainMode = ref<TemplateEditMode>('create')
const trainOperationPlanTrainForm = ref(createEmptyTrainOperationPlanTrain())
const trainOperationPlanMovementMode = ref<TemplateEditMode>('create')
const trainOperationPlanMovementForm = ref(createEmptyTrainOperationPlanMovement())
const trainOperationPlanMovementRouteIds = ref<string[]>([])
const trainOperationPlanStartTime = ref('00:00')
const trainOperationPlanEndTime = ref('24:00')
const routePickerVisible = ref(false)
const routePickerTarget = ref<RoutePickerTarget>('movementTemplate')
const routePickerSelectedIds = ref<string[]>([])
const routePickerFilters = ref<RoutePickerFilters>(createEmptyRoutePickerFilters())
const routePickerPreviewRouteId = ref('')
const routePickerLayoutEditorRef = ref<any | null>(null)
const routePickerLayoutViewportRef = ref<HTMLElement | null>(null)
const routePickerSplitRef = ref<HTMLElement | null>(null)
const routePickerLayoutData = ref<any | null>(null)
const routePickerLayoutKey = ref('')
const routePickerLayoutDisplayStyles = ref<Record<string, unknown>>({})
const routePickerLayoutCells = ref<Array<{ id: string; name: string; linkIDList: string }>>([])
const routePickerLayoutGridSpacing = ref(20)
const routePickerLayoutScaleX = ref(1)
const routePickerLayoutScaleY = ref(1)
const routePickerTableHeight = ref(routePickerDefaultTableHeight)
const routePickerNodeFilterStage = ref<RoutePickerNodeFilterStage>('start')
const loadingRoutePickerLayout = ref(false)
const operationPlanManagerVisible = ref(false)
const operationPlanObjectMode = ref<TemplateEditMode>('create')
const operationPlanObjectOriginalId = ref('')
const operationPlanObjectForm = ref(createEmptyOperationPlanObject())

let stationSchemeLoadVersion = 0
let operationPlanObjectLoadVersion = 0
let stationRouteLoadVersion = 0
let stationRouteEndLoadVersion = 0
let trainTemplateLoadVersion = 0
let movementTemplateLoadVersion = 0
let trainOperationPlanLoadVersion = 0
let operationPlanChartLoadVersion = 0
let routePickerLayoutLoadVersion = 0
let routePickerResizeState: RoutePickerResizeState | null = null
let operationAnalysisSnapshotSaveTimer: ReturnType<typeof window.setTimeout> | null = null
let operationBottleneckSummaryCategorySaveTimer: ReturnType<typeof window.setTimeout> | null = null
let operationOccupationTimeSubTableSaveTimer: ReturnType<typeof window.setTimeout> | null = null
let suppressOperationOccupationTimeSubTableSave = false
let operationOccupationTimeSubTableSaveRevision = 0

const hasScope = computed(() => Boolean(
    props.selectedInstanceId &&
    currentStationSchemeId.value.trim() &&
    currentOperationPlanId.value.trim(),
))
const selectedTrainTemplate = computed(() => {
    const id = selectedTrainTemplateId.value.trim()
    return trainTemplates.value.find((item) => item.trainTemplateID === id) || null
})
const selectedTrainOperationPlanTrain = computed(() => {
    const id = selectedTrainOperationPlanTrainId.value.trim()
    return trainOperationPlanTrains.value.find((item) => item.id === id) || null
})
const trainTemplateInlineActive = computed(() => trainTemplateCreating.value || Boolean(trainTemplateEditingId.value))
const movementTemplateInlineActive = computed(() => movementTemplateCreating.value || Boolean(movementTemplateEditingId.value))
const trainOperationPlanTrainInlineActive = computed(() => trainOperationPlanTrainCreating.value || Boolean(trainOperationPlanTrainEditingId.value))
const trainOperationPlanMovementInlineActive = computed(() => trainOperationPlanMovementCreating.value || Boolean(trainOperationPlanMovementEditingKey.value))
const trainOperationPlanInlineActive = computed(() => trainOperationPlanTrainInlineActive.value || trainOperationPlanMovementInlineActive.value)
const operationPlanInlineActive = computed(() => trainTemplateInlineActive.value || movementTemplateInlineActive.value || trainOperationPlanInlineActive.value)
const operationPlanObjectInlineActive = computed(() => (
    operationPlanOptions.value.some((item) => item.isDraft) ||
    Boolean(operationPlanObjectOriginalId.value)
))
const canLoadTemplates = computed(() => hasScope.value && !loadingTrainTemplates.value)
const canEditTrainTemplates = computed(() => hasScope.value && !savingTrainTemplate.value)
const canLoadMovementTemplates = computed(() => hasScope.value && selectedTrainTemplate.value !== null && !loadingMovementTemplates.value)
const canEditMovementTemplates = computed(() => canLoadMovementTemplates.value && !savingMovementTemplate.value && !trainTemplateInlineActive.value)
const canLoadTrainOperationPlan = computed(() => hasScope.value && !loadingTrainOperationPlan.value)
const canLoadOperationPlanChart = computed(() => (
    hasScope.value &&
    !loadingOperationPlanChart.value &&
    !loadingTrainOperationPlan.value &&
    !loadingStationRoutes.value &&
    !loadingStationRouteEnds.value
))
const canGenerateTrainOperationPlan = computed(() => (
    hasScope.value &&
    !generatingTrainOperationPlan.value &&
    !loadingTrainOperationPlan.value &&
    !operationPlanInlineActive.value
))
const canEditTrainOperationPlan = computed(() => (
    hasScope.value &&
    !loadingTrainOperationPlan.value &&
    !generatingTrainOperationPlan.value &&
    !savingTrainOperationPlanTrain.value &&
    !savingTrainOperationPlanMovement.value
))
const trainTemplateCountText = computed(() => t('operationPlan.train.count', { count: trainTemplates.value.length }))
const movementTemplateCountText = computed(() => {
    const trainName = selectedTrainTemplate.value?.name || ''
    return t('operationPlan.movement.count', { count: movementTemplates.value.length, train: trainName })
})
const trainOperationPlanTrainCountText = computed(() => (
    t('operationPlan.trainOperationPlan.train.count', { count: trainOperationPlanTrains.value.length })
))
const trainOperationPlanMovementCountText = computed(() => (
    t('operationPlan.trainOperationPlan.movement.count', { count: selectedTrainOperationPlanMovements.value.length })
))
const operationPlanChartCountText = computed(() => (
    t('operationPlan.trainOperationChart.count', {
        cellCount: operationPlanChartRows.value.length,
        barCount: operationPlanChartBars.value.length,
    })
))
const operationOccupationTimeTableCountText = computed(() => (
    t('operationPlan.operationOccupationTimeTable.count', {
        routeCount: displayOperationOccupationRouteRows.value.length,
        operationCount: displayOperationOccupationRouteRows.value.reduce((sum, row) => sum + Number(row.operationCount || 0), 0),
    })
))
const operationBottleneckAnalysisCountText = computed(() => (
    t('operationPlan.operationBottleneckAnalysis.count', { count: displayOperationBottleneckAnalysisRows.value.length })
))
const operationBottleneckSummaryCountText = computed(() => (
    t('operationPlan.operationBottleneckAnalysis.summary.count', { count: displayOperationBottleneckSummaryRows.value.length })
))
const trainTemplateEmptyText = computed(() => hasScope.value ? t('operationPlan.train.empty') : t('operationPlan.empty.selectScheme'))
const movementTemplateEmptyText = computed(() => selectedTrainTemplate.value ? t('operationPlan.movement.empty') : t('operationPlan.empty.expandTrain'))
const trainOperationPlanEmptyText = computed(() => (
    hasScope.value
        ? t('operationPlan.trainOperationPlan.empty')
        : t('operationPlan.empty.selectScheme')
))
const trainOperationPlanMovementEmptyText = computed(() => {
    if (!hasScope.value) return t('operationPlan.empty.selectScheme')
    if (!selectedTrainOperationPlanTrain.value) return t('operationPlan.trainOperationPlan.movement.expandTrain')
    return t('operationPlan.trainOperationPlan.empty')
})
const operationPlanChartEmptyText = computed(() => {
    if (!hasScope.value) return t('operationPlan.empty.selectScheme')
    if (trainOperationPlanMovements.value.length === 0) return t('operationPlan.trainOperationChart.emptyPlan')
    if (stationLayoutCells.value.length === 0) return t('operationPlan.trainOperationChart.emptyCells')
    return t('operationPlan.trainOperationChart.emptyOccupations')
})
const operationOccupationTimeTableEmptyText = computed(() => {
    if (!hasScope.value) return t('operationPlan.empty.selectScheme')
    if (trainOperationPlanMovements.value.length === 0) return t('operationPlan.operationOccupationTimeTable.emptyPlan')
    return t('operationPlan.operationOccupationTimeTable.empty')
})
const operationBottleneckAnalysisEmptyText = computed(() => {
    if (!hasScope.value) return t('operationPlan.empty.selectScheme')
    if (trainOperationPlanMovements.value.length === 0) return t('operationPlan.operationBottleneckAnalysis.emptyPlan')
    return t('operationPlan.operationBottleneckAnalysis.empty')
})
const operationBottleneckSummaryEmptyText = computed(() => {
    if (!hasScope.value) return t('operationPlan.empty.selectScheme')
    if (usingOperationAnalysisSnapshot.value) return t('operationPlan.operationBottleneckAnalysis.summary.empty')
    if (operationBottleneckSummaryCategories.value.length === 0) return t('operationPlan.operationBottleneckAnalysis.summary.emptyCategories')
    if (displayOperationBottleneckAnalysisRows.value.length === 0) return t('operationPlan.operationBottleneckAnalysis.empty')
    return t('operationPlan.operationBottleneckAnalysis.summary.empty')
})
const visibleTrainTemplates = computed<TrainTemplate[]>(() => (
    trainTemplateCreating.value
        ? [{ ...trainTemplateForm.value, isDraft: true }, ...trainTemplates.value]
        : trainTemplates.value
))
const visibleMovementTemplates = computed<MovementTemplate[]>(() => (
    movementTemplateCreating.value
        ? [{ ...movementTemplateForm.value, isDraft: true }, ...movementTemplates.value]
        : movementTemplates.value
))
const visibleTrainOperationPlanTrains = computed<TrainOperationPlanTrain[]>(() => (
    trainOperationPlanTrainCreating.value
        ? [{ ...trainOperationPlanTrainForm.value, isDraft: true }, ...trainOperationPlanTrains.value]
        : trainOperationPlanTrains.value
))
const selectedTrainOperationPlanMovements = computed<TrainOperationPlanMovement[]>(() => {
    const trainID = selectedTrainOperationPlanTrain.value?.id || ''
    if (!trainID) return []
    return trainOperationPlanMovements.value.filter((movement) => movement.trainID === trainID)
})
const visibleTrainOperationPlanMovements = computed<TrainOperationPlanMovement[]>(() => {
    const movements = selectedTrainOperationPlanMovements.value
    const trainID = selectedTrainOperationPlanTrain.value?.id || ''
    if (trainOperationPlanMovementCreating.value && trainID && trainOperationPlanMovementForm.value.trainID === trainID) {
        return [{ ...trainOperationPlanMovementForm.value, isDraft: true }, ...movements]
    }
    return movements
})
const stationRouteOptionMap = computed(() => {
    const map = new Map<string, StationRouteOption>()
    stationRouteOptions.value.forEach((route) => map.set(route.id, route))
    return map
})
const stationRouteEndByBindingNodeId = computed(() => {
    const map = new Map<string, StationRouteEndOption>()
    stationRouteEndOptions.value.forEach((routeEnd) => {
        const bindingNodeID = routeEnd.bindingNodeID.trim()
        if (bindingNodeID) map.set(bindingNodeID, routeEnd)
    })
    return map
})
const trainOperationPlanTrainMap = computed(() => {
    const map = new Map<string, TrainOperationPlanTrain>()
    trainOperationPlanTrains.value.forEach((train) => map.set(train.id, train))
    return map
})
const operationPlanChartCells = computed<OperationPlanChartCell[]>(() => {
    if (stationLayoutCells.value.length > 0) return stationLayoutCells.value

    const cellsById = new Map<string, OperationPlanChartCell>()
    stationRouteOptions.value.forEach((route) => {
        normalizeRoutePickerValues([
            ...parseRouteReferenceList(route.cellList),
            ...parseRouteReferenceList(route.interruptCellList),
        ]).forEach((cellID) => {
            if (!cellsById.has(cellID)) cellsById.set(cellID, { id: cellID, name: cellID })
        })
    })
    return Array.from(cellsById.values())
})
const operationPlanChartBars = computed<OperationPlanChartBar[]>(() => {
    const bars: OperationPlanChartBar[] = []
    trainOperationPlanMovements.value.forEach((movement) => {
        const routeID = movement.route.trim()
        if (!routeID) return

        const route = stationRouteOptionMap.value.get(routeID)
        const train = trainOperationPlanTrainMap.value.get(movement.trainID)
        const baseStartMinutes = parseOperationPlanTime(movement.earliestStartTime)
        const baseEndMinutes = parseOperationPlanTime(movement.latestEndTime)
        if (baseStartMinutes === null || baseEndMinutes === null) return

        const routeTimeRows = getOperationPlanChartRouteTimes(routeID, train?.trainType || '')
        const routeTimeByCellID = new Map<string, StationRouteTimeOption>()
        routeTimeRows.forEach((time) => {
            if (time.cellID && !routeTimeByCellID.has(time.cellID)) {
                routeTimeByCellID.set(time.cellID, time)
            }
        })

        const routeCellIDs = normalizeRoutePickerValues(parseRouteReferenceList(route?.cellList || ''))
        const routeCellIDSet = new Set(routeCellIDs.map((cellID) => cellID.toLowerCase()))
        const interruptCellIDs = normalizeRoutePickerValues(parseRouteReferenceList(route?.interruptCellList || ''))
            .filter((cellID) => !routeCellIDSet.has(cellID.toLowerCase()))
        const timeCellIDs = normalizeRoutePickerValues(routeTimeRows.map((time) => time.cellID))
        const routeConfiguredCellIDs = normalizeRoutePickerValues([...routeCellIDs, ...interruptCellIDs])
        const cellIDs = routeConfiguredCellIDs.length > 0 ? routeConfiguredCellIDs : timeCellIDs
        const interruptCellIDSet = new Set([
            ...interruptCellIDs,
            ...routeTimeRows.filter((time) => time.isInterruptCell).map((time) => time.cellID),
        ].map((cellID) => cellID.toLowerCase()))
        const trainLabel = train?.trainNumber || movement.trainID
        const movementLabel = movement.name || movement.movementID
        const routeName = getRouteDisplayName(routeID)
        const chartLabel = `${trainLabel}-${routeName}`
        const color = getOperationPlanChartTrainColor(movement.trainID)

        cellIDs.forEach((cellID) => {
            const time = routeTimeByCellID.get(cellID)
            const isInterruptCell = interruptCellIDSet.has(cellID.toLowerCase())
            const startMinutes = baseStartMinutes + (time?.startOccupationShift ?? 0) / 60
            const endMinutes = Math.max(startMinutes, baseEndMinutes + (time?.endOccupationShift ?? 0) / 60)
            bars.push({
                key: `${movement.trainID}-${movement.movementID}-${routeID}-${cellID}`,
                cellID,
                trainID: movement.trainID,
                trainNumber: train?.trainNumber || '',
                isFixedOperation: Boolean(train?.isFixedOperation),
                movementID: movement.movementID,
                movementName: movement.name,
                routeID,
                routeName,
                isInterruptCell,
                startMinutes,
                endMinutes,
                lane: 0,
                label: chartLabel,
                color,
                title: `${chartLabel}\n${movementLabel}${isInterruptCell ? `\n${t('calculationParameters.manager.directInterrupt')}` : ''}\n${formatOperationPlanChartTime(startMinutes)} - ${formatOperationPlanChartTime(endMinutes)}`,
            })
        })
    })
    return bars
})
const operationPlanChartRows = computed<OperationPlanChartRow[]>(() => {
    const barsByCellID = new Map<string, OperationPlanChartBar[]>()
    operationPlanChartBars.value.forEach((bar) => {
        const bars = barsByCellID.get(bar.cellID) || []
        bars.push(bar)
        barsByCellID.set(bar.cellID, bars)
    })

    const sourceCells = operationPlanChartCells.value.length > 0
        ? operationPlanChartCells.value
        : Array.from(barsByCellID.keys()).map((cellID) => ({ id: cellID, name: cellID }))

    return sourceCells
        .map((cell) => {
            const bars = assignOperationPlanChartBarLanes(barsByCellID.get(cell.id) || [])
            return {
                cellID: cell.id,
                cellName: cell.name || cell.id,
                bars,
                laneCount: Math.max(1, ...bars.map((bar) => bar.lane + 1)),
            }
        })
        .filter((row) => row.cellID)
})
const operationOccupationTimeTableCells = computed<OperationPlanChartCell[]>(() => {
    const cellsByID = new Map<string, OperationPlanChartCell>()
    operationPlanChartCells.value.forEach((cell) => {
        if (cell.id && !cellsByID.has(cell.id)) cellsByID.set(cell.id, cell)
    })
    operationPlanChartBars.value.forEach((bar) => {
        if (bar.cellID && !cellsByID.has(bar.cellID)) {
            cellsByID.set(bar.cellID, { id: bar.cellID, name: bar.cellID })
        }
    })
    return Array.from(cellsByID.values())
})
const operationOccupationRouteRows = computed<OperationOccupationTimeTableRow[]>(() => {
    const routeStats = new Map<string, OperationOccupationRouteStats>()

    trainOperationPlanMovements.value.forEach((movement) => {
        const routeID = movement.route.trim()
        if (!routeID) return
        const stats = ensureOperationOccupationRouteStats(routeStats, routeID, routeID)
        stats.operationCount += 1
    })

    operationPlanChartBars.value.forEach((bar) => {
        const stats = ensureOperationOccupationRouteStats(routeStats, bar.routeID, bar.routeID, bar.routeName)
        const duration = Math.max(0, Math.round((bar.endMinutes - bar.startMinutes) * 60))
        stats.cellDurations[bar.cellID] = (stats.cellDurations[bar.cellID] || 0) + duration
        if (bar.isInterruptCell) {
            stats.interruptCellDurations[bar.cellID] = (stats.interruptCellDurations[bar.cellID] || 0) + duration
        }
    })

    return Array.from(routeStats.values()).map((row, index) => createOperationOccupationRouteRow(row, index, 'route'))
})
const operationOccupationGroupedRouteRows = computed<OperationOccupationTimeTableRow[]>(() => {
    const routeStats = new Map<string, OperationOccupationRouteStats>()

    trainOperationPlanMovements.value.forEach((movement) => {
        const routeID = movement.route.trim()
        if (!routeID) return
        const isFixedOperation = Boolean(trainOperationPlanTrainMap.value.get(movement.trainID)?.isFixedOperation)
        const routeKey = `${isFixedOperation ? 'fixed' : 'nonfixed'}:${routeID}`
        const stats = ensureOperationOccupationRouteStats(routeStats, routeKey, routeID, undefined, isFixedOperation)
        stats.operationCount += 1
    })

    operationPlanChartBars.value.forEach((bar) => {
        const routeKey = `${bar.isFixedOperation ? 'fixed' : 'nonfixed'}:${bar.routeID}`
        const stats = ensureOperationOccupationRouteStats(routeStats, routeKey, bar.routeID, bar.routeName, bar.isFixedOperation)
        const duration = Math.max(0, Math.round((bar.endMinutes - bar.startMinutes) * 60))
        stats.cellDurations[bar.cellID] = (stats.cellDurations[bar.cellID] || 0) + duration
        if (bar.isInterruptCell) {
            stats.interruptCellDurations[bar.cellID] = (stats.interruptCellDurations[bar.cellID] || 0) + duration
        }
    })

    return Array.from(routeStats.values())
        .sort((left, right) => Number(Boolean(right.isFixedOperation)) - Number(Boolean(left.isFixedOperation)))
        .map((row, index) => createOperationOccupationRouteRow(row, index))
})
const operationOccupationCellTotalSeconds = computed<Record<string, number>>(() => {
    const totals: Record<string, number> = {}
    operationOccupationRouteRows.value.forEach((row) => {
        Object.entries(row.cellDurations).forEach(([cellID, seconds]) => {
            totals[cellID] = (totals[cellID] || 0) + seconds
        })
    })
    return totals
})
const operationOccupationInterruptCellTotalSeconds = computed<Record<string, number>>(() => {
    const totals: Record<string, number> = {}
    operationOccupationRouteRows.value.forEach((row) => {
        Object.entries(row.interruptCellDurations || {}).forEach(([cellID, seconds]) => {
            totals[cellID] = (totals[cellID] || 0) + seconds
        })
    })
    return totals
})
const operationOccupationFixedCellTotalSeconds = computed<Record<string, number>>(() => (
    sumOperationOccupationCellDurations(
        operationOccupationGroupedRouteRows.value.filter((row) => row.isFixedOperation),
    )
))
const operationOccupationCellUtilizations = computed<Record<string, number>>(() => {
    const totalTimeSeconds = Number(operationOccupationTotalTimeSeconds.value || 0)
    if (!Number.isFinite(totalTimeSeconds) || totalTimeSeconds <= 0) return {}

    const emptyWasteFactor = Number(operationOccupationEmptyWasteFactor.value || 0)
    const normalizedEmptyWasteFactor = Number.isFinite(emptyWasteFactor)
        ? Math.min(0.99, Math.max(0, emptyWasteFactor))
        : 0.2
    const denominatorFactor = 1 - normalizedEmptyWasteFactor
    const utilizations: Record<string, number> = {}

    Object.entries(operationOccupationCellTotalSeconds.value).forEach(([cellID, seconds]) => {
        const fixedOccupationSeconds = Number(operationOccupationFixedCellTotalSeconds.value[cellID] || 0)
        const nonFixedOccupationSeconds = Math.max(0, Number(seconds || 0) - fixedOccupationSeconds)
        const effectiveTotalTimeSeconds = totalTimeSeconds - fixedOccupationSeconds
        const denominator = denominatorFactor * effectiveTotalTimeSeconds
        if (
            Number.isFinite(nonFixedOccupationSeconds) &&
            Number.isFinite(denominator) &&
            nonFixedOccupationSeconds > 0 &&
            denominator > 0
        ) {
            utilizations[cellID] = nonFixedOccupationSeconds / denominator
        }
    })

    return utilizations
})
const operationOccupationTimeTableRows = computed<OperationOccupationTimeTableRow[]>(() => {
    if (operationOccupationGroupedRouteRows.value.length === 0) return []

    const fixedRows = operationOccupationGroupedRouteRows.value.filter((row) => row.isFixedOperation)
    const nonFixedRows = operationOccupationGroupedRouteRows.value.filter((row) => !row.isFixedOperation)
    const groupedRows: OperationOccupationTimeTableRow[] = []

    if (fixedRows.length > 0) {
        const fixedCellDurations = sumOperationOccupationCellDurations(fixedRows)
        const fixedInterruptCellDurations = sumOperationOccupationInterruptCellDurations(fixedRows)
        const fixedOperationCount = sumOperationOccupationOperationCount(fixedRows)
        const fixedTotalRow: OperationOccupationTimeTableRow = {
            rowKey: 'fixed-total',
            rowType: 'fixed-total',
            sequence: '',
            routeID: '',
            routeName: t('operationPlan.operationOccupationTimeTable.summary.fixedOccupationTime'),
            operationCount: fixedOperationCount,
            cellDurations: fixedCellDurations,
            interruptCellDurations: fixedInterruptCellDurations,
            isFixedOperation: true,
        }
        groupedRows.push(createOperationOccupationGroupRow(
            'group:fixed',
            t('operationPlan.operationOccupationTimeTable.groups.fixedOperation'),
            fixedOperationCount,
            [...fixedRows, fixedTotalRow],
            fixedCellDurations,
            fixedInterruptCellDurations,
        ))
    }

    if (nonFixedRows.length > 0) {
        const nonFixedCellDurations = sumOperationOccupationCellDurations(nonFixedRows)
        const nonFixedInterruptCellDurations = sumOperationOccupationInterruptCellDurations(nonFixedRows)
        groupedRows.push(createOperationOccupationGroupRow(
            'group:nonfixed',
            t('operationPlan.operationOccupationTimeTable.groups.nonFixedOperation'),
            sumOperationOccupationOperationCount(nonFixedRows),
            nonFixedRows,
            nonFixedCellDurations,
            nonFixedInterruptCellDurations,
        ))
    }

    const totalRow: OperationOccupationTimeTableRow = {
        rowKey: 'total',
        rowType: 'total',
        sequence: '',
        routeID: '',
        routeName: t('operationPlan.operationOccupationTimeTable.summary.totalOccupationTime'),
        operationCount: '',
        cellDurations: operationOccupationCellTotalSeconds.value,
        interruptCellDurations: operationOccupationInterruptCellTotalSeconds.value,
    }
    const utilizationRow: OperationOccupationTimeTableRow = {
        rowKey: 'utilization',
        rowType: 'utilization',
        sequence: '',
        routeID: '',
        routeName: t('operationPlan.operationOccupationTimeTable.summary.utilizationK'),
        operationCount: '',
        cellDurations: operationOccupationCellUtilizations.value,
        interruptCellDurations: {},
    }
    return [...groupedRows, totalRow, utilizationRow]
})
const operationOccupationTimeSnapshotRows = computed<OperationOccupationTimeTableRow[]>(() => (
    flattenOperationOccupationTimeTableRows(operationOccupationTimeTableRows.value)
        .filter((row) => row.rowType !== 'group')
))
const operationOccupationCellNameMap = computed(() => {
    const map = new Map<string, string>()
    operationPlanChartCells.value.forEach((cell) => map.set(cell.id, cell.name || cell.id))
    return map
})
const operationBottleneckAnalysisRows = computed<OperationBottleneckAnalysisRow[]>(() => (
    operationOccupationRouteRows.value.map((routeRow) => {
        const route = stationRouteOptionMap.value.get(routeRow.routeID)
        const routeCellIDs = normalizeRoutePickerValues([
            ...parseRouteReferenceList(route?.cellList || ''),
            ...parseRouteReferenceList(route?.interruptCellList || ''),
        ])
        const fallbackCellIDs = normalizeRoutePickerValues(Object.keys(routeRow.cellDurations))
        const cellIDs = routeCellIDs.length > 0 ? routeCellIDs : fallbackCellIDs
        let bottleneckCellID = ''
        let bottleneckUtilization = 0

        cellIDs.forEach((cellID) => {
            const utilization = operationOccupationCellUtilizations.value[cellID] || 0
            if (!bottleneckCellID || utilization > bottleneckUtilization) {
                bottleneckCellID = cellID
                bottleneckUtilization = utilization
            }
        })

        return {
            routeID: routeRow.routeID,
            routeName: routeRow.routeName,
            operationCount: Number(routeRow.operationCount || 0),
            bottleneckCellID,
            bottleneckCellName: bottleneckCellID
                ? (operationOccupationCellNameMap.value.get(bottleneckCellID) || bottleneckCellID)
                : '',
            bottleneckUtilization: bottleneckUtilization > 0 ? bottleneckUtilization : null,
            throughputCapacity: bottleneckUtilization > 0
                ? Number(routeRow.operationCount || 0) / bottleneckUtilization
                : null,
        }
    })
))
const operationBottleneckAnalysisRowMap = computed(() => {
    const map = new Map<string, OperationBottleneckAnalysisRow>()
    operationBottleneckAnalysisRows.value.forEach((row) => map.set(row.routeID, row))
    return map
})
const operationBottleneckSummaryRows = computed<OperationBottleneckSummaryRow[]>(() => (
    operationBottleneckSummaryCategories.value.map((category) => {
        const routeRows = normalizeRoutePickerValues(category.routeIDs)
            .map((routeID) => operationBottleneckAnalysisRowMap.value.get(routeID))
            .filter((row): row is OperationBottleneckAnalysisRow => row !== undefined)
        const capacityRows = routeRows.filter((row) => row.throughputCapacity !== null && Number.isFinite(row.throughputCapacity))
        const capacityTotal = capacityRows.reduce((sum, row) => sum + Number(row.throughputCapacity || 0), 0)
        return {
            categoryID: category.id,
            groupKey: category.id,
            groupText: category.name,
            routeIDs: category.routeIDs,
            routeCount: routeRows.length,
            operationCount: routeRows.reduce((sum, row) => sum + row.operationCount, 0),
            capacityTotal: capacityRows.length > 0 ? capacityTotal : null,
            capacityAverage: capacityRows.length > 0 ? capacityTotal / capacityRows.length : null,
        }
    })
))
const displayOperationOccupationTimeTableCells = computed<OperationPlanChartCell[]>(() => (
    usingOperationAnalysisSnapshot.value
        ? operationAnalysisSnapshot.value?.cells || []
        : operationOccupationTimeTableCells.value
))
const activeOperationOccupationTimeSubTable = computed(() => (
    operationOccupationTimeSubTables.value.find((subTable) => subTable.id === activeOperationOccupationTimeSubTableId.value) ||
    operationOccupationTimeSubTables.value[0] ||
    null
))
const activeOperationOccupationTimeSubTableCellIds = computed(() => (
    normalizeOperationOccupationTimeSubTableCellIds(activeOperationOccupationTimeSubTable.value?.cellIds || [])
))
const activeOperationOccupationTimeSubTableCells = computed<OperationPlanChartCell[]>(() => {
    const selectedCellIds = new Set(activeOperationOccupationTimeSubTableCellIds.value)
    return displayOperationOccupationTimeTableCells.value.filter((cell) => selectedCellIds.has(cell.id))
})
const activeOperationOccupationTimeSubTableSummaryText = computed(() => (
    t('operationPlan.operationOccupationTimeTable.subTables.selectedCount', {
        selected: activeOperationOccupationTimeSubTableCells.value.length,
        total: displayOperationOccupationTimeTableCells.value.length,
    })
))
const operationOccupationTimeSubTableDialogTitle = computed(() => (
    operationOccupationTimeSubTableDialogMode.value === 'create'
        ? t('operationPlan.operationOccupationTimeTable.subTables.createTitle')
        : t('operationPlan.operationOccupationTimeTable.subTables.editTitle')
))
const displayOperationOccupationTimeTableRows = computed<OperationOccupationTimeTableRow[]>(() => (
    usingOperationAnalysisSnapshot.value
        ? buildOperationOccupationSnapshotDisplayRows(operationAnalysisSnapshot.value?.occupationTimeTableRows || [])
        : operationOccupationTimeTableRows.value
))
const displayOperationOccupationRouteRows = computed<OperationOccupationTimeTableRow[]>(() => (
    flattenOperationOccupationTimeTableRows(displayOperationOccupationTimeTableRows.value)
        .filter((row) => row.rowType === 'route')
))
const displayOperationBottleneckAnalysisRows = computed<OperationBottleneckAnalysisRow[]>(() => (
    usingOperationAnalysisSnapshot.value
        ? operationAnalysisSnapshot.value?.bottleneckAnalysisRows || []
        : operationBottleneckAnalysisRows.value
))
const displayOperationBottleneckSummaryRows = computed<OperationBottleneckSummaryRow[]>(() => (
    usingOperationAnalysisSnapshot.value
        ? operationAnalysisSnapshot.value?.throughputSummaryRows || []
        : operationBottleneckSummaryRows.value
))
const operationBottleneckRouteEndFilterOptions = computed<RouteListSelectOption[]>(() => (
    stationRouteEndOptions.value
        .map((routeEnd) => ({
            id: routeEnd.id,
            name: getStationRouteEndDisplayName(routeEnd),
        }))
        .sort((left, right) => left.name.localeCompare(right.name, 'zh-Hans-CN'))
))
const operationBottleneckRoutePickerRows = computed<OperationBottleneckRoutePickerRow[]>(() => (
    operationBottleneckAnalysisRows.value.map((row) => {
        const route = stationRouteOptionMap.value.get(row.routeID)
        const startRouteEnd = route?.startNodeID ? stationRouteEndByBindingNodeId.value.get(route.startNodeID) || null : null
        const endRouteEnd = route?.endNodeID ? stationRouteEndByBindingNodeId.value.get(route.endNodeID) || null : null
        return {
            ...row,
            startRouteEndID: startRouteEnd?.id || '',
            startRouteEndName: getStationRouteEndDisplayName(startRouteEnd),
            endRouteEndID: endRouteEnd?.id || '',
            endRouteEndName: getStationRouteEndDisplayName(endRouteEnd),
        }
    })
))
const operationBottleneckRoutePickerFiltersActive = computed(() => (
    Boolean(operationBottleneckRoutePickerFilters.value.keyword.trim()) ||
    operationBottleneckRoutePickerFilters.value.startRouteEndIds.length > 0 ||
    operationBottleneckRoutePickerFilters.value.endRouteEndIds.length > 0
))
const filteredOperationBottleneckRoutePickerRows = computed(() => {
    const keyword = operationBottleneckRoutePickerFilters.value.keyword.trim().toLowerCase()
    const startRouteEndIds = new Set(operationBottleneckRoutePickerFilters.value.startRouteEndIds)
    const endRouteEndIds = new Set(operationBottleneckRoutePickerFilters.value.endRouteEndIds)
    return operationBottleneckRoutePickerRows.value.filter((row) => {
        if (keyword) {
            const text = `${row.routeID} ${row.routeName} ${row.startRouteEndName} ${row.endRouteEndName}`.toLowerCase()
            if (!text.includes(keyword)) return false
        }
        if (startRouteEndIds.size > 0 && !startRouteEndIds.has(row.startRouteEndID)) return false
        if (endRouteEndIds.size > 0 && !endRouteEndIds.has(row.endRouteEndID)) return false
        return true
    })
})
const operationBottleneckRoutePickerSelectedIdSet = computed(() => new Set(operationBottleneckRoutePickerSelectedIds.value))
const operationBottleneckRoutePickerFilteredAllSelected = computed(() => (
    filteredOperationBottleneckRoutePickerRows.value.length > 0 &&
    filteredOperationBottleneckRoutePickerRows.value.every((row) => operationBottleneckRoutePickerSelectedIdSet.value.has(row.routeID))
))
const operationBottleneckRoutePickerFilteredPartlySelected = computed(() => (
    filteredOperationBottleneckRoutePickerRows.value.some((row) => operationBottleneckRoutePickerSelectedIdSet.value.has(row.routeID)) &&
    !operationBottleneckRoutePickerFilteredAllSelected.value
))
const operationBottleneckRoutePickerSummaryText = computed(() => (
    t('operationPlan.operationBottleneckAnalysis.summary.routePicker.count', {
        selected: operationBottleneckRoutePickerSelectedIds.value.length,
        filtered: filteredOperationBottleneckRoutePickerRows.value.length,
        total: operationBottleneckRoutePickerRows.value.length,
    })
))
const operationBottleneckRoutePickerEmptyText = computed(() => (
    operationBottleneckRoutePickerFiltersActive.value
        ? t('operationPlan.operationBottleneckAnalysis.summary.routePicker.filterEmpty')
        : t('operationPlan.operationBottleneckAnalysis.summary.routePicker.empty')
))
const operationPlanChartDomain = computed(() => {
    const chartValues = operationPlanChartBars.value.flatMap((bar) => [bar.startMinutes, bar.endMinutes])
    const planStart = parseOperationPlanTime(trainOperationPlanStartTime.value)
    const planEnd = parseOperationPlanTime(trainOperationPlanEndTime.value)
    const planValues = [planStart, planEnd].filter((value): value is number => value !== null)
    const values = [...chartValues, ...planValues]
    if (values.length === 0) return { start: 0, end: 24 * 60 }

    const min = Math.min(...values)
    const max = Math.max(...values)
    const span = Math.max(30, max - min)
    const padding = Math.max(15, Math.ceil(span * 0.04))
    return { start: min - padding, end: max + padding }
})
const operationPlanChartTimelineWidth = computed(() => (
    Math.max(860, operationPlanChartTimeSpan.value * operationPlanChartPixelsPerMinute.value)
))
const operationPlanChartTimeSpan = computed(() => (
    Math.max(1, operationPlanChartDomain.value.end - operationPlanChartDomain.value.start)
))
const operationPlanChartPixelsPerMinute = computed(() => {
    const span = operationPlanChartTimeSpan.value
    if (span <= 180) return 4
    if (span <= 480) return 2.4
    if (span <= 960) return 1.5
    return 1
})
const operationPlanChartGridStyle = computed(() => ({
    gridTemplateColumns: `180px ${operationPlanChartTimelineWidth.value}px`,
    minWidth: `${180 + operationPlanChartTimelineWidth.value}px`,
}))
const operationPlanChartTicks = computed(() => {
    const step = getOperationPlanChartTickStep(operationPlanChartTimeSpan.value)
    const first = Math.ceil(operationPlanChartDomain.value.start / step) * step
    const ticks: number[] = []
    for (let value = first; value <= operationPlanChartDomain.value.end; value += step) {
        ticks.push(value)
    }
    return ticks
})
const routePickerRoutes = computed<StationRouteOption[]>(() => {
    const routesById = new Map<string, StationRouteOption>()
    stationRouteOptions.value.forEach((route) => routesById.set(route.id, route))
    normalizeRoutePickerValues([
        ...getActiveRoutePickerSourceIds(),
        ...routePickerSelectedIds.value,
    ]).forEach((id) => {
        if (!routesById.has(id)) routesById.set(id, createFallbackStationRouteOption(id))
    })
    return sortStationRouteOptions(Array.from(routesById.values()))
})
const routePickerSelectedIdSet = computed(() => new Set(routePickerSelectedIds.value))
const routePickerSingleSelect = computed(() => routePickerTarget.value === 'trainOperationPlanMovementRoute')
const routePickerFiltersActive = computed(() => (
    Object.values(routePickerFilters.value).some((values) => values.length > 0)
))
const filteredRoutePickerRoutes = computed(() => (
    routePickerRoutes.value.filter((route) => routeMatchesRoutePickerFilters(route))
))
const routePickerFilteredAllSelected = computed(() => (
    filteredRoutePickerRoutes.value.length > 0 &&
    filteredRoutePickerRoutes.value.every((route) => routePickerSelectedIdSet.value.has(route.id))
))
const routePickerFilteredPartlySelected = computed(() => (
    filteredRoutePickerRoutes.value.some((route) => routePickerSelectedIdSet.value.has(route.id)) &&
    !routePickerFilteredAllSelected.value
))
const routePickerTypeFilterOptions = computed<RouteListSelectOption[]>(() => (
    buildRoutePickerFilterOptions([
        ...routePickerRoutes.value.map((route) => route.type),
        ...routePickerFilters.value.types,
    ])
))
const routePickerSummaryText = computed(() => {
    if (routePickerSelectedIds.value.length > 0) {
        return t('operationPlan.movement.routePicker.selectionCount', {
            selected: routePickerSelectedIds.value.length,
            total: routePickerRoutes.value.length,
        })
    }
    if (routePickerFiltersActive.value) {
        return t('operationPlan.movement.routePicker.filterCount', {
            filtered: filteredRoutePickerRoutes.value.length,
            total: routePickerRoutes.value.length,
        })
    }
    return t('operationPlan.movement.routePicker.count', { count: routePickerRoutes.value.length })
})
const routePickerEmptyText = computed(() => (
    routePickerFiltersActive.value
        ? t('operationPlan.movement.routePicker.filterEmpty')
        : t('operationPlan.movement.routePicker.empty')
))
const routePickerNodeFiltersActive = computed(() => (
    routePickerFilters.value.startNodeIds.length > 0 ||
    routePickerFilters.value.endNodeIds.length > 0
))
const routePickerEndpointFiltersReady = computed(() => (
    routePickerFilters.value.startNodeIds.length > 0 &&
    routePickerFilters.value.endNodeIds.length > 0
))
const routePickerEndpointFilterKey = computed(() => JSON.stringify([
    routePickerFilters.value.startNodeIds,
    routePickerFilters.value.endNodeIds,
]))
const routePickerStartNodeFilterText = computed(() => (
    `${t('routeDesign.stationRoute.fields.startNodeID')}: ${routePickerFilters.value.startNodeIds[0] || '-'}`
))
const routePickerEndNodeFilterText = computed(() => (
    `${t('routeDesign.stationRoute.fields.endNodeID')}: ${routePickerFilters.value.endNodeIds[0] || '-'}`
))
const selectedRoutePickerPreviewRoute = computed(() => {
    if (!routePickerEndpointFiltersReady.value || !routePickerPreviewRouteId.value) return null
    return filteredRoutePickerRoutes.value.find((route) => route.id === routePickerPreviewRouteId.value) || null
})
const routePickerHighlightedRoutePathNodeIds = computed(() => {
    const route = selectedRoutePickerPreviewRoute.value
    if (!route) return []
    const nodeIds = normalizeRoutePickerValues(parseRouteReferenceList(route.nodeList))
    if (nodeIds.length > 0) return nodeIds
    return normalizeRoutePickerValues([route.startNodeID, route.endNodeID])
})
const routePickerHighlightedRouteNodeIds = computed(() => (
    normalizeRoutePickerValues([
        ...routePickerHighlightedRoutePathNodeIds.value,
        selectedRoutePickerPreviewRoute.value?.startNodeID || '',
        selectedRoutePickerPreviewRoute.value?.endNodeID || '',
        ...routePickerFilters.value.startNodeIds,
        ...routePickerFilters.value.endNodeIds,
    ])
))
const routePickerHighlightedRouteLinkIds = computed(() => (
    parseRouteReferenceList(selectedRoutePickerPreviewRoute.value?.linkList || '')
))
const routePickerHighlightedRouteArrowNodeIds = computed(() => routePickerHighlightedRoutePathNodeIds.value)
const routePickerHighlightedRouteColor = computed(() => getStationRouteHighlightColor(selectedRoutePickerPreviewRoute.value?.type || ''))
const routePickerHighlightedRouteArrowVisible = computed(() => routePickerHighlightedRouteArrowNodeIds.value.length >= 2)

function createEmptyTrainTemplate(): TrainTemplate {
    return {
        instanceID: '',
        stationSchemeID: '',
        operationPlanID: '',
        trainTemplateID: '',
        name: '',
        type: '',
        number: null,
        isFixedOperation: false,
    }
}

function createEmptyMovementTemplate(): MovementTemplate {
    return {
        instanceID: '',
        stationSchemeID: '',
        operationPlanID: '',
        trainTemplateID: '',
        movementID: '',
        name: '',
        routeIDList: '',
        minDuration: null,
        sortOrder: null,
    }
}

function createEmptyTrainOperationPlanTrain(): TrainOperationPlanTrain {
    return {
        instanceID: '',
        stationSchemeID: '',
        operationPlanID: '',
        id: '',
        trainTemplateID: '',
        trainNumber: '',
        name: '',
        trainType: '',
        isFixedOperation: false,
    }
}

function createEmptyTrainOperationPlanMovement(): TrainOperationPlanMovement {
    return {
        instanceID: '',
        stationSchemeID: '',
        operationPlanID: '',
        trainID: '',
        trainTemplateID: '',
        movementID: '',
        name: '',
        routeIDList: '',
        minDuration: null,
        earliestStartTime: '',
        latestEndTime: '',
        route: '',
        tag: '',
        sortOrder: null,
    }
}

function createEmptyOperationPlanObject(): StationOperationPlan {
    return {
        instanceID: '',
        stationSchemeID: '',
        operationPlanID: '',
        name: '',
        description: '',
        sortOrder: null,
    }
}

function createEmptyRoutePickerFilters(): RoutePickerFilters {
    return {
        types: [],
        startNodeIds: [],
        endNodeIds: [],
        nodeIds: [],
        linkIds: [],
        cellIds: [],
        switchIds: [],
        signalIds: [],
    }
}

function createFallbackStationRouteOption(id: string): StationRouteOption {
    return {
        id,
        name: id,
        type: '',
        label: id,
        nodeList: '',
        linkList: '',
        switchList: '',
        cellList: '',
        interruptCellList: '',
        signalList: '',
        startNodeID: '',
        endNodeID: '',
    }
}

function readString(source: any, ...keys: string[]) {
    for (const key of keys) {
        const value = source?.[key]
        if (value !== undefined && value !== null) return String(value)
    }
    return ''
}

function readOptionalInteger(source: any, ...keys: string[]): number | null {
    for (const key of keys) {
        const value = source?.[key]
        if (value === undefined || value === null || value === '') continue
        const parsed = Number(value)
        if (Number.isFinite(parsed)) return Math.trunc(parsed)
    }
    return null
}

function readOptionalNumber(source: any, ...keys: string[]): number | null {
    for (const key of keys) {
        const value = source?.[key]
        if (value === undefined || value === null || value === '') continue
        const parsed = Number(value)
        if (Number.isFinite(parsed)) return parsed
    }
    return null
}

function readBoolean(source: any, defaultValue: boolean, ...keys: string[]) {
    for (const key of keys) {
        const value = source?.[key]
        if (value === undefined || value === null || value === '') continue
        if (typeof value === 'boolean') return value
        if (typeof value === 'number') return value === 1
        const text = String(value).trim().toLowerCase()
        if (['1', 'true', 'yes', 'y'].includes(text)) return true
        if (['0', 'false', 'no', 'n'].includes(text)) return false
    }
    return defaultValue
}

function readArray(source: any, ...keys: string[]): any[] {
    for (const key of keys) {
        const value = source?.[key]
        if (Array.isArray(value)) return value
    }
    return []
}

function normalizeStationSchemeOption(item: any): StationSchemeOption | null {
    const id = readString(item, 'id', 'ID').trim()
    if (!id) return null
    return {
        id,
        name: readString(item, 'name', 'Name').trim() || id,
    }
}

function normalizeOperationPlanObject(item: any): StationOperationPlan | null {
    const operationPlanID = readString(item, 'operationPlanID', 'OperationPlanID').trim()
    if (!operationPlanID) return null
    return {
        instanceID: readString(item, 'instanceID', 'InstanceID').trim(),
        stationSchemeID: readString(item, 'stationSchemeID', 'StationSchemeID').trim(),
        operationPlanID,
        name: readString(item, 'name', 'Name').trim() || operationPlanID,
        description: readString(item, 'description', 'Description').trim(),
        sortOrder: readOptionalInteger(item, 'sortOrder', 'SortOrder'),
        createdDate: readString(item, 'createdDate', 'CreatedDate').trim(),
        updatedDate: readString(item, 'updatedDate', 'UpdatedDate').trim(),
    }
}

function normalizeStationRouteOption(item: any): StationRouteOption | null {
    const id = readString(item, 'id', 'ID').trim()
    if (!id) return null
    const description = readString(item, 'description', 'Description').trim()
    const type = readString(item, 'type', 'Type').trim()
    const name = description || id
    return {
        id,
        name,
        type,
        label: name,
        nodeList: readString(item, 'nodeList', 'NodeList').trim(),
        linkList: readString(item, 'linkList', 'LinkList').trim(),
        switchList: readString(item, 'switchList', 'SwitchList').trim(),
        cellList: readString(item, 'cellList', 'CellList').trim(),
        interruptCellList: readString(item, 'interruptCellList', 'InterruptCellList').trim(),
        signalList: readString(item, 'signalList', 'SignalList').trim(),
        startNodeID: readString(item, 'startNodeID', 'StartNodeID').trim(),
        endNodeID: readString(item, 'endNodeID', 'EndNodeID').trim(),
    }
}

function normalizeStationRouteEndOption(item: any): StationRouteEndOption | null {
    const id = readString(item, 'id', 'ID').trim()
    const bindingNodeID = readString(item, 'bindingNodeID', 'BindingNodeID').trim()
    if (!id || !bindingNodeID) return null
    return {
        instanceID: readString(item, 'instanceID', 'InstanceID').trim(),
        stationSchemeID: readString(item, 'stationSchemeID', 'StationSchemeID').trim(),
        id,
        bindingNodeID,
        type: readString(item, 'type', 'Type').trim(),
        segmentTag: readString(item, 'segmentTag', 'SegmentTag').trim(),
        sidingTag: readString(item, 'sidingTag', 'SidingTag').trim(),
    }
}

function getLayoutDisplayStyles(layoutData: any): Record<string, unknown> {
    const styles = layoutData?.metadata?.displayStyles
    return styles && typeof styles === 'object' && !Array.isArray(styles) ? styles : {}
}

function getLayoutCells(layoutData: any) {
    const cells = Array.isArray(layoutData?.cells) ? layoutData.cells : []
    return cells
        .map((cell: any) => {
            const id = readString(cell, 'id', 'ID').trim()
            const name = readString(cell, 'name', 'Name').trim() || id
            const linkIDList = readString(cell, 'linkIDList', 'LinkIDList').trim()
            return { id, name, linkIDList }
        })
        .filter((cell: { id: string; name: string; linkIDList: string }) => cell.id || cell.name || cell.linkIDList)
}

function getLayoutGridSettings(layoutData: any): Record<string, unknown> {
    const gridSettings = layoutData?.metadata?.gridSettings
    return gridSettings && typeof gridSettings === 'object' && !Array.isArray(gridSettings)
        ? gridSettings
        : {}
}

function getLayoutGridSpacing(layoutData: any) {
    const gridSettings = getLayoutGridSettings(layoutData)
    const parsedSpacing = Number(gridSettings.spacing ?? gridSettings.Spacing ?? 20)
    return Number.isFinite(parsedSpacing) && parsedSpacing > 0 ? parsedSpacing : 20
}

function normalizeTrainTemplate(item: any): TrainTemplate | null {
    const trainTemplateID = readString(item, 'trainTemplateID', 'TrainTemplateID').trim()
    if (!trainTemplateID) return null
    return {
        instanceID: readString(item, 'instanceID', 'InstanceID').trim(),
        stationSchemeID: readString(item, 'stationSchemeID', 'StationSchemeID').trim(),
        operationPlanID: readString(item, 'operationPlanID', 'OperationPlanID').trim(),
        trainTemplateID,
        name: readString(item, 'name', 'Name').trim() || trainTemplateID,
        type: readString(item, 'type', 'Type').trim(),
        number: readOptionalInteger(item, 'number', 'Number'),
        isFixedOperation: readBoolean(item, false, 'isFixedOperation', 'IsFixedOperation'),
    }
}

function normalizeMovementTemplate(item: any): MovementTemplate | null {
    const movementID = readString(item, 'movementID', 'MovementID').trim()
    if (!movementID) return null
    return {
        instanceID: readString(item, 'instanceID', 'InstanceID').trim(),
        stationSchemeID: readString(item, 'stationSchemeID', 'StationSchemeID').trim(),
        operationPlanID: readString(item, 'operationPlanID', 'OperationPlanID').trim(),
        trainTemplateID: readString(item, 'trainTemplateID', 'TrainTemplateID').trim(),
        movementID,
        name: readString(item, 'name', 'Name').trim() || movementID,
        routeIDList: readString(item, 'routeIDList', 'RouteIDList').trim(),
        minDuration: readOptionalInteger(item, 'minDuration', 'MinDuration'),
        sortOrder: readOptionalInteger(item, 'sortOrder', 'SortOrder'),
    }
}

function normalizeTrainOperationPlanTrain(item: any): TrainOperationPlanTrain | null {
    const id = readString(item, 'id', 'ID').trim()
    if (!id) return null
    return {
        instanceID: readString(item, 'instanceID', 'InstanceID').trim(),
        stationSchemeID: readString(item, 'stationSchemeID', 'StationSchemeID').trim(),
        operationPlanID: readString(item, 'operationPlanID', 'OperationPlanID').trim(),
        id,
        trainTemplateID: readString(item, 'trainTemplateID', 'TrainTemplateID').trim(),
        trainNumber: readString(item, 'trainNumber', 'TrainNumber').trim(),
        name: readString(item, 'name', 'Name').trim(),
        trainType: readString(item, 'trainType', 'TrainType').trim(),
        isFixedOperation: readBoolean(item, false, 'isFixedOperation', 'IsFixedOperation'),
    }
}

function normalizeTrainOperationPlanMovement(item: any): TrainOperationPlanMovement | null {
    const trainID = readString(item, 'trainID', 'TrainID').trim()
    const movementID = readString(item, 'movementID', 'MovementID').trim()
    if (!trainID || !movementID) return null
    return {
        instanceID: readString(item, 'instanceID', 'InstanceID').trim(),
        stationSchemeID: readString(item, 'stationSchemeID', 'StationSchemeID').trim(),
        operationPlanID: readString(item, 'operationPlanID', 'OperationPlanID').trim(),
        trainID,
        trainTemplateID: readString(item, 'trainTemplateID', 'TrainTemplateID').trim(),
        movementID,
        name: readString(item, 'name', 'Name').trim(),
        routeIDList: readString(item, 'routeIDList', 'RouteIDList').trim(),
        minDuration: readOptionalInteger(item, 'minDuration', 'MinDuration'),
        earliestStartTime: readString(item, 'earliestStartTime', 'EarliestStartTime').trim(),
        latestEndTime: readString(item, 'latestEndTime', 'LatestEndTime').trim(),
        route: readString(item, 'route', 'Route').trim(),
        tag: readString(item, 'tag', 'Tag').trim(),
        sortOrder: readOptionalInteger(item, 'sortOrder', 'SortOrder'),
    }
}

function normalizeStationRouteTimeOption(item: any): StationRouteTimeOption | null {
    const cellID = readString(item, 'cellID', 'CellID').trim()
    if (!cellID) return null
    return {
        routeID: readString(item, 'routeID', 'RouteID').trim(),
        trainTypeID: readString(item, 'trainTypeID', 'TrainTypeID').trim(),
        cellID,
        startOccupationShift: readOptionalInteger(item, 'startOccupationShift', 'StartOccupationShift'),
        endOccupationShift: readOptionalInteger(item, 'endOccupationShift', 'EndOccupationShift'),
        isInterruptCell: readBoolean(item, false, 'isInterruptCell', 'IsInterruptCell'),
    }
}

function normalizeOperationBottleneckSummaryCategory(item: any, index = 0): OperationBottleneckSummaryCategory | null {
    const id = readString(item, 'id', 'ID', 'categoryID', 'CategoryID').trim()
    if (!id) return null
    return {
        id,
        name: readString(item, 'name', 'Name').trim() || t('operationPlan.operationBottleneckAnalysis.summary.defaultCategoryName', { index: index + 1 }),
        routeIDs: normalizeRoutePickerValues(parseRouteReferenceList(readString(item, 'routeIDList', 'RouteIDList'))),
        sortOrder: readOptionalInteger(item, 'sortOrder', 'SortOrder') ?? index,
    }
}

function normalizeOperationAnalysisCell(item: any): OperationPlanChartCell | null {
    const id = readString(item, 'id', 'ID').trim()
    if (!id) return null
    return {
        id,
        name: readString(item, 'name', 'Name').trim() || id,
    }
}

function normalizeOperationAnalysisCellDurations(source: any): Record<string, number> {
    const durations: Record<string, number> = {}
    const raw = source && typeof source === 'object' && !Array.isArray(source) ? source : {}
    Object.entries(raw).forEach(([cellID, value]) => {
        const id = cellID.trim()
        const seconds = Number(value)
        if (id && Number.isFinite(seconds)) durations[id] = seconds
    })
    return durations
}

function buildOperationOccupationSnapshotRowKey(rowType: string, routeID: string, index: number) {
    const normalizedRowType = rowType.trim() || 'row'
    const normalizedRouteID = routeID.trim()
    if (normalizedRowType === 'route' && normalizedRouteID) {
        return `snapshot:route:${index}:${normalizedRouteID}`
    }
    return `snapshot:${normalizedRowType}:${index}`
}

function normalizeOperationOccupationTimeTableSnapshotRow(item: any, index = 0): OperationOccupationTimeTableRow | null {
    const rowType = readString(item, 'rowType', 'RowType').trim() as OperationOccupationTimeTableRowType
    if (!['group', 'route', 'fixed-total', 'total', 'utilization'].includes(rowType)) return null
    const routeID = readString(item, 'routeID', 'RouteID').trim()
    return {
        rowKey: readString(item, 'rowKey', 'RowKey').trim() || buildOperationOccupationSnapshotRowKey(rowType, routeID, index),
        rowType,
        sequence: readString(item, 'sequence', 'Sequence').trim(),
        routeID,
        routeName: readString(item, 'routeName', 'RouteName').trim(),
        operationCount: readString(item, 'operationCount', 'OperationCount').trim(),
        cellDurations: normalizeOperationAnalysisCellDurations(item?.cellDurations ?? item?.CellDurations),
        interruptCellDurations: normalizeOperationAnalysisCellDurations(item?.interruptCellDurations ?? item?.InterruptCellDurations),
    }
}

function normalizeOperationBottleneckAnalysisSnapshotRow(item: any): OperationBottleneckAnalysisRow | null {
    const routeID = readString(item, 'routeID', 'RouteID').trim()
    if (!routeID) return null
    return {
        routeID,
        routeName: readString(item, 'routeName', 'RouteName').trim() || getRouteDisplayName(routeID),
        operationCount: readOptionalInteger(item, 'operationCount', 'OperationCount') ?? 0,
        bottleneckCellID: readString(item, 'bottleneckCellID', 'BottleneckCellID').trim(),
        bottleneckCellName: readString(item, 'bottleneckCellName', 'BottleneckCellName').trim(),
        bottleneckUtilization: readOptionalNumber(item, 'bottleneckUtilization', 'BottleneckUtilization'),
        throughputCapacity: readOptionalNumber(item, 'throughputCapacity', 'ThroughputCapacity'),
    }
}

function normalizeOperationBottleneckSummarySnapshotRow(item: any): OperationBottleneckSummaryRow | null {
    const categoryID = readString(item, 'categoryID', 'CategoryID').trim()
    const groupKey = readString(item, 'groupKey', 'GroupKey').trim() || categoryID
    if (!categoryID && !groupKey) return null
    const routeIDs = readArray(item, 'routeIDs', 'RouteIDs')
        .map((routeID) => String(routeID).trim())
        .filter((routeID) => routeID)
    return {
        categoryID,
        groupKey,
        groupText: readString(item, 'groupText', 'GroupText').trim() || groupKey,
        routeIDs,
        routeCount: readOptionalInteger(item, 'routeCount', 'RouteCount') ?? routeIDs.length,
        operationCount: readOptionalInteger(item, 'operationCount', 'OperationCount') ?? 0,
        capacityTotal: readOptionalNumber(item, 'capacityTotal', 'CapacityTotal'),
        capacityAverage: readOptionalNumber(item, 'capacityAverage', 'CapacityAverage'),
    }
}

function normalizeOperationAnalysisSnapshot(data: any): OperationAnalysisSnapshot | null {
    if (!data || typeof data !== 'object') return null
    const snapshot: OperationAnalysisSnapshot = {
        totalTimeSeconds: readOptionalInteger(data, 'totalTimeSeconds', 'TotalTimeSeconds'),
        cells: readArray(data, 'cells', 'Cells')
            .map(normalizeOperationAnalysisCell)
            .filter((item): item is OperationPlanChartCell => item !== null),
        occupationTimeTableRows: readArray(data, 'occupationTimeTableRows', 'OccupationTimeTableRows')
            .map((item, index) => normalizeOperationOccupationTimeTableSnapshotRow(item, index))
            .filter((item): item is OperationOccupationTimeTableRow => item !== null),
        bottleneckAnalysisRows: readArray(data, 'bottleneckAnalysisRows', 'BottleneckAnalysisRows')
            .map(normalizeOperationBottleneckAnalysisSnapshotRow)
            .filter((item): item is OperationBottleneckAnalysisRow => item !== null),
        throughputSummaryRows: readArray(data, 'throughputSummaryRows', 'ThroughputSummaryRows')
            .map(normalizeOperationBottleneckSummarySnapshotRow)
            .filter((item): item is OperationBottleneckSummaryRow => item !== null),
        updatedDate: readString(data, 'updatedDate', 'UpdatedDate').trim(),
    }
    return snapshot.occupationTimeTableRows.length > 0 ||
        snapshot.bottleneckAnalysisRows.length > 0 ||
        snapshot.throughputSummaryRows.length > 0
        ? snapshot
        : null
}

function normalizeTrainOperationPlanResponse(data: any) {
    const trainRows: any[] = Array.isArray(data?.trains) ? data.trains : Array.isArray(data?.Trains) ? data.Trains : []
    const movementRows: any[] = Array.isArray(data?.movements) ? data.movements : Array.isArray(data?.Movements) ? data.Movements : []
    const previousSelectedTrainId = selectedTrainOperationPlanTrainId.value
    trainOperationPlanTrains.value = trainRows
        .map(normalizeTrainOperationPlanTrain)
        .filter((item): item is TrainOperationPlanTrain => item !== null)
    trainOperationPlanMovements.value = movementRows
        .map(normalizeTrainOperationPlanMovement)
        .filter((item): item is TrainOperationPlanMovement => item !== null)
    selectedTrainOperationPlanTrainId.value = trainOperationPlanTrains.value.some((train) => train.id === previousSelectedTrainId)
        ? previousSelectedTrainId
        : ''
}

function formatStationSchemeLabel(option: StationSchemeOption) {
    return option.name && option.name !== option.id ? `${option.name} (${option.id})` : option.id
}

function formatOperationPlanLabel(option: StationOperationPlan) {
    return option.name && option.name !== option.operationPlanID
        ? `${option.name} (${option.operationPlanID})`
        : option.operationPlanID
}

function formatIdentifierValue(value: string | null | undefined, fallback = '-') {
    const trimmed = value?.trim()
    return trimmed || fallback
}

function formatAutoIdentifierValue(value: string | null | undefined) {
    return formatIdentifierValue(value, t('operationPlan.placeholders.autoId'))
}

function formatIdentifierTooltip(entries: Array<[string, string]>) {
    return entries.map(([label, value]) => `${label}: ${value}`).join(' / ')
}

function getTrainTemplateNameTooltip(row: TrainTemplate) {
    return formatIdentifierTooltip([
        [t('operationPlan.train.fields.trainTemplateID'), formatAutoIdentifierValue(row.trainTemplateID)],
    ])
}

function getMovementTemplateNameTooltip(row: MovementTemplate) {
    return formatIdentifierTooltip([
        [t('operationPlan.train.fields.trainTemplateID'), formatIdentifierValue(row.trainTemplateID || selectedTrainTemplate.value?.trainTemplateID)],
        [t('operationPlan.movement.fields.movementID'), formatAutoIdentifierValue(row.movementID)],
    ])
}

function getTrainOperationPlanTrainNameTooltip(row: TrainOperationPlanTrain) {
    return formatIdentifierTooltip([
        [t('operationPlan.trainOperationPlan.train.fields.id'), formatAutoIdentifierValue(row.id)],
        [t('operationPlan.train.fields.trainTemplateID'), formatIdentifierValue(row.trainTemplateID)],
    ])
}

function getTrainOperationPlanMovementNameTooltip(row: TrainOperationPlanMovement) {
    return formatIdentifierTooltip([
        [t('operationPlan.trainOperationPlan.train.fields.id'), formatIdentifierValue(row.trainID || selectedTrainOperationPlanTrain.value?.id)],
        [t('operationPlan.train.fields.trainTemplateID'), formatIdentifierValue(row.trainTemplateID || selectedTrainOperationPlanTrain.value?.trainTemplateID)],
        [t('operationPlan.movement.fields.movementID'), formatAutoIdentifierValue(row.movementID)],
    ])
}

function getCurrentOperationPlanID() {
    return currentOperationPlanId.value.trim()
}

function getOperationPlanScope() {
    const instanceID = props.selectedInstanceId
    const stationSchemeID = currentStationSchemeId.value.trim()
    const operationPlanID = getCurrentOperationPlanID()
    return { instanceID, stationSchemeID, operationPlanID }
}

function parseRouteIDList(value: string) {
    return value
        .split(/[,，;；\n\r]+/)
        .map((item) => item.trim())
        .filter(Boolean)
}

function parseRouteReferenceList(value: string) {
    const text = String(value || '').trim()
    if (!text) return []

    try {
        const parsed = JSON.parse(text)
        if (Array.isArray(parsed)) {
            return parsed.map((item) => String(item).trim()).filter(Boolean)
        }
    } catch {
        // Station route lists may be saved as plain text.
    }

    return text
        .split(/(?:\s*->\s*)|(?:\s*[,，;；\n\r]\s*)|\s+/)
        .map((item) => item.trim())
        .filter(Boolean)
}

function serializeRouteIDList(routeIDs: string[]) {
    return routeIDs
        .map((item) => item.trim())
        .filter(Boolean)
        .filter((item, index, list) => list.indexOf(item) === index)
        .join(',')
}

function parseOperationPlanTime(value: string) {
    const text = String(value || '').trim()
    if (!text) return null

    let dayOffset = 0
    let timeText = text
    const dayMatch = text.match(/^D\+(\d+)\s+(.+)$/i)
    if (dayMatch) {
        dayOffset = Number(dayMatch[1])
        timeText = (dayMatch[2] || '').trim()
    }

    const parts = timeText.split(':')
    if (parts.length < 2) return null
    const hours = Number(parts[0])
    const minutes = Number(parts[1])
    const seconds = parts.length > 2 ? Number(parts[2]) : 0
    if (
        !Number.isFinite(hours) ||
        !Number.isFinite(minutes) ||
        !Number.isFinite(seconds) ||
        hours < 0 ||
        minutes < 0 ||
        minutes >= 60 ||
        seconds < 0 ||
        seconds >= 60
    ) {
        return null
    }
    return dayOffset * 24 * 60 + hours * 60 + minutes + seconds / 60
}

function formatOperationPlanChartTime(totalMinutes: number) {
    const normalizedSeconds = Math.max(0, Math.round(totalMinutes * 60))
    const days = Math.floor(normalizedSeconds / (24 * 60 * 60))
    const secondsInDay = normalizedSeconds % (24 * 60 * 60)
    const hours = Math.floor(secondsInDay / (60 * 60))
    const minutes = Math.floor((secondsInDay % (60 * 60)) / 60)
    const seconds = secondsInDay % 60
    const timeText = seconds > 0
        ? `${String(hours).padStart(2, '0')}:${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}`
        : `${String(hours).padStart(2, '0')}:${String(minutes).padStart(2, '0')}`
    return days > 0 ? `D+${days} ${timeText}` : timeText
}

function getOperationOccupationTimeSubTableFallbackName(index: number) {
    return t('operationPlan.operationOccupationTimeTable.subTables.label', { index })
}

function createOperationOccupationTimeSubTable(index: number, name?: string): OperationOccupationTimeSubTable {
    return {
        id: `occupation-time-sub-table-${index}`,
        name: name?.trim() || getOperationOccupationTimeSubTableFallbackName(index),
        cellIds: [],
        hasCustomSelection: false,
    }
}

function formatOperationOccupationTimeSubTableLabel(subTable: OperationOccupationTimeSubTable, index: number) {
    return subTable.name?.trim() || getOperationOccupationTimeSubTableFallbackName(index + 1)
}

function normalizeOperationOccupationTimeSubTableCellIds(cellIds: string[]) {
    const availableCellIds = new Set(displayOperationOccupationTimeTableCells.value.map((cell) => cell.id))
    return normalizeRoutePickerValues(cellIds).filter((cellID) => availableCellIds.has(cellID))
}

function normalizeOperationOccupationTimeSubTableStoredCellIds(cellIds: string[]) {
    return normalizeRoutePickerValues(cellIds)
}

function runWithoutOperationOccupationTimeSubTableSave(action: () => void) {
    suppressOperationOccupationTimeSubTableSave = true
    try {
        action()
    } finally {
        void nextTick(() => {
            suppressOperationOccupationTimeSubTableSave = false
        })
    }
}

function resetOperationOccupationTimeSubTables() {
    operationOccupationTimeSubTableSequence.value = operationOccupationTimeDefaultSubTableCount
    operationOccupationTimeSubTables.value = Array.from(
        { length: operationOccupationTimeDefaultSubTableCount },
        (_, index) => createOperationOccupationTimeSubTable(index + 1),
    )
    activeOperationOccupationTimeSubTableId.value = operationOccupationTimeSubTables.value[0]?.id || ''
}

function syncOperationOccupationTimeSubTables(cells: OperationPlanChartCell[]) {
    if (operationOccupationTimeSubTables.value.length === 0) {
        operationOccupationTimeSubTables.value = [createOperationOccupationTimeSubTable(1)]
        operationOccupationTimeSubTableSequence.value = 1
    }

    const cellIds = cells.map((cell) => cell.id).filter(Boolean)
    const availableCellIds = new Set(cellIds)
    if (cellIds.length === 0) {
        return
    }

    operationOccupationTimeSubTables.value = operationOccupationTimeSubTables.value.map((subTable) => ({
        ...subTable,
        cellIds: subTable.cellIds.filter((cellID) => availableCellIds.has(cellID)),
    }))

    if (!operationOccupationTimeSubTables.value.some((subTable) => subTable.id === activeOperationOccupationTimeSubTableId.value)) {
        activeOperationOccupationTimeSubTableId.value = operationOccupationTimeSubTables.value[0]?.id || ''
    }

    const hasCustomSelection = operationOccupationTimeSubTables.value.some((subTable) => subTable.hasCustomSelection)
    if (hasCustomSelection) return

    const tableCount = Math.max(1, operationOccupationTimeSubTables.value.length)
    const chunkSize = Math.max(1, Math.ceil(cellIds.length / tableCount))
    operationOccupationTimeSubTables.value = operationOccupationTimeSubTables.value.map((subTable, index) => ({
        ...subTable,
        cellIds: cellIds.slice(index * chunkSize, (index + 1) * chunkSize),
        hasCustomSelection: false,
    }))
}

function getNextOperationOccupationTimeSubTableDraft() {
    const usedIds = new Set(operationOccupationTimeSubTables.value.map((item) => item.id))
    let sequence = operationOccupationTimeSubTableSequence.value
    let subTable: OperationOccupationTimeSubTable
    do {
        sequence += 1
        subTable = createOperationOccupationTimeSubTable(sequence)
    } while (usedIds.has(subTable.id))

    return { sequence, subTable }
}

function openCreateOperationOccupationTimeSubTableDialog() {
    const { sequence, subTable } = getNextOperationOccupationTimeSubTableDraft()
    const selectedCellIds = new Set(operationOccupationTimeSubTables.value.flatMap((item) => item.cellIds))
    const remainingCellIds = displayOperationOccupationTimeTableCells.value
        .map((cell) => cell.id)
        .filter((cellID) => !selectedCellIds.has(cellID))

    operationOccupationTimeSubTableDialogMode.value = 'create'
    operationOccupationTimeSubTableDialogTargetId.value = subTable.id
    operationOccupationTimeSubTableDialogTargetSequence.value = sequence
    operationOccupationTimeSubTableDialogForm.value = {
        name: subTable.name,
        cellIds: remainingCellIds,
    }
    operationOccupationTimeSubTableDialogVisible.value = true
}

function openEditOperationOccupationTimeSubTableDialog() {
    const activeSubTable = activeOperationOccupationTimeSubTable.value
    if (!activeSubTable) return

    const activeIndex = operationOccupationTimeSubTables.value.findIndex((subTable) => subTable.id === activeSubTable.id)
    operationOccupationTimeSubTableDialogMode.value = 'edit'
    operationOccupationTimeSubTableDialogTargetId.value = activeSubTable.id
    operationOccupationTimeSubTableDialogTargetSequence.value = 0
    operationOccupationTimeSubTableDialogForm.value = {
        name: activeSubTable.name?.trim() || getOperationOccupationTimeSubTableFallbackName(activeIndex + 1),
        cellIds: [...activeSubTable.cellIds],
    }
    operationOccupationTimeSubTableDialogVisible.value = true
}

function confirmOperationOccupationTimeSubTableDialog() {
    const name = operationOccupationTimeSubTableDialogForm.value.name.trim()
    if (!name) {
        ElMessage.warning(t('operationPlan.operationOccupationTimeTable.subTables.nameRequired'))
        return
    }

    const cellIds = normalizeOperationOccupationTimeSubTableCellIds(operationOccupationTimeSubTableDialogForm.value.cellIds)
    if (operationOccupationTimeSubTableDialogMode.value === 'create') {
        let subTableId = operationOccupationTimeSubTableDialogTargetId.value
        let sequence = operationOccupationTimeSubTableDialogTargetSequence.value
        if (!subTableId || operationOccupationTimeSubTables.value.some((subTable) => subTable.id === subTableId)) {
            const draft = getNextOperationOccupationTimeSubTableDraft()
            subTableId = draft.subTable.id
            sequence = draft.sequence
        }

        operationOccupationTimeSubTableSequence.value = Math.max(operationOccupationTimeSubTableSequence.value, sequence)
        operationOccupationTimeSubTables.value = [
            ...operationOccupationTimeSubTables.value,
            {
                id: subTableId,
                name,
                cellIds,
                hasCustomSelection: true,
            },
        ]
        activeOperationOccupationTimeSubTableId.value = subTableId
    } else {
        const subTableId = operationOccupationTimeSubTableDialogTargetId.value
        operationOccupationTimeSubTables.value = operationOccupationTimeSubTables.value.map((subTable) => (
            subTable.id === subTableId
                ? {
                    ...subTable,
                    name,
                    cellIds,
                    hasCustomSelection: true,
                }
                : subTable
        ))
    }

    operationOccupationTimeSubTableDialogVisible.value = false
}

function removeOperationOccupationTimeSubTable(name: string | number) {
    if (operationOccupationTimeSubTables.value.length <= 1) return

    const subTableId = String(name)
    const removedIndex = operationOccupationTimeSubTables.value.findIndex((subTable) => subTable.id === subTableId)
    if (removedIndex < 0) return

    const nextSubTables = operationOccupationTimeSubTables.value.filter((subTable) => subTable.id !== subTableId)
    operationOccupationTimeSubTables.value = nextSubTables
    if (activeOperationOccupationTimeSubTableId.value === subTableId) {
        activeOperationOccupationTimeSubTableId.value = nextSubTables[Math.min(removedIndex, nextSubTables.length - 1)]?.id || ''
    }
}

function normalizeOperationOccupationTimeSubTableSetting(item: any): OperationOccupationTimeSubTable | null {
    const id = readString(item, 'subTableID', 'SubTableID', 'id', 'ID').trim()
    if (!id) return null

    const name = readString(item, 'subTableName', 'SubTableName', 'name', 'Name').trim()
    const cellIDs = normalizeOperationOccupationTimeSubTableStoredCellIds(
        readArray(item, 'cellIDs', 'CellIDs', 'cellIds')
            .map((cellID) => String(cellID ?? '')),
    )
    const fallbackCellIDList = readString(item, 'cellIDList', 'CellIDList').trim()
    return {
        id,
        name,
        cellIds: cellIDs.length > 0
            ? cellIDs
            : normalizeOperationOccupationTimeSubTableStoredCellIds(parseRouteReferenceList(fallbackCellIDList)),
        hasCustomSelection: true,
    }
}

function applyOperationOccupationTimeSubTableSettings(settings: OperationOccupationTimeSubTable[]) {
    const nextSettings = settings.length > 0
        ? settings
        : Array.from(
            { length: operationOccupationTimeDefaultSubTableCount },
            (_, index) => createOperationOccupationTimeSubTable(index + 1),
        )

    runWithoutOperationOccupationTimeSubTableSave(() => {
        operationOccupationTimeSubTables.value = nextSettings.map((setting, index) => ({
            ...setting,
            name: setting.name?.trim() || getOperationOccupationTimeSubTableFallbackName(index + 1),
            cellIds: normalizeOperationOccupationTimeSubTableStoredCellIds(setting.cellIds),
            hasCustomSelection: true,
        }))
        operationOccupationTimeSubTableSequence.value = Math.max(
            operationOccupationTimeDefaultSubTableCount,
            operationOccupationTimeSubTables.value.length,
        )
        activeOperationOccupationTimeSubTableId.value = operationOccupationTimeSubTables.value[0]?.id || ''
        syncOperationOccupationTimeSubTables(displayOperationOccupationTimeTableCells.value)
    })
}

function buildOperationOccupationTimeSubTableSettingsPayload(): OperationOccupationTimeSubTableSettingPayload[] {
    return operationOccupationTimeSubTables.value.map((subTable, index) => ({
        subTableID: subTable.id,
        subTableName: subTable.name?.trim() || getOperationOccupationTimeSubTableFallbackName(index + 1),
        cellIDs: normalizeRoutePickerValues(subTable.cellIds),
        sortOrder: index,
    }))
}

function clearOperationOccupationTimeSubTableState() {
    if (operationOccupationTimeSubTableSaveTimer) {
        window.clearTimeout(operationOccupationTimeSubTableSaveTimer)
        operationOccupationTimeSubTableSaveTimer = null
    }
    loadingOperationOccupationTimeSubTableSettings.value = false
    savingOperationOccupationTimeSubTableSettings.value = false
    operationOccupationTimeSubTableDialogVisible.value = false
    operationOccupationTimeSubTableDialogTargetId.value = ''
    operationOccupationTimeSubTableDialogTargetSequence.value = 0
    operationOccupationTimeSubTableDialogForm.value = {
        name: '',
        cellIds: [],
    }
    runWithoutOperationOccupationTimeSubTableSave(resetOperationOccupationTimeSubTables)
}

function formatOperationOccupationMinuteValue(totalSeconds: number) {
    const minutes = totalSeconds / 60
    if (Number.isInteger(minutes)) return String(minutes)
    return minutes.toFixed(2).replace(/\.?0+$/, '')
}

function formatOperationOccupationDuration(totalSeconds: number | null | undefined) {
    const seconds = Math.round(Number(totalSeconds || 0))
    if (!Number.isFinite(seconds) || seconds <= 0) return ''
    if (operationOccupationTimeUnit.value === 'minutes') {
        return t('operationPlan.operationOccupationTimeTable.minutes', {
            value: formatOperationOccupationMinuteValue(seconds),
        })
    }
    return t('operationPlan.operationOccupationTimeTable.seconds', { value: seconds })
}

function formatOperationOccupationUtilization(value: number | null | undefined) {
    const utilization = Number(value || 0)
    if (!Number.isFinite(utilization) || utilization <= 0) return ''
    return utilization.toFixed(4)
}

function ensureOperationOccupationRouteStats(
    routeStats: Map<string, OperationOccupationRouteStats>,
    key: string,
    routeID: string,
    routeName?: string,
    isFixedOperation?: boolean,
) {
    const normalizedRouteID = routeID.trim()
    const normalizedKey = key.trim() || normalizedRouteID
    const existing = routeStats.get(normalizedKey)
    if (existing) {
        if (!existing.routeName && routeName) existing.routeName = routeName
        if (typeof isFixedOperation === 'boolean') existing.isFixedOperation = isFixedOperation
        return existing
    }

    const created: OperationOccupationRouteStats = {
        routeID: normalizedRouteID,
        routeName: routeName || getRouteDisplayName(normalizedRouteID),
        operationCount: 0,
        cellDurations: {},
        interruptCellDurations: {},
        isFixedOperation,
    }
    routeStats.set(normalizedKey, created)
    return created
}

function createOperationOccupationRouteRow(
    stats: OperationOccupationRouteStats,
    index: number,
    rowKeyPrefix?: string,
): OperationOccupationTimeTableRow {
    const isFixedOperation = Boolean(stats.isFixedOperation)
    const prefix = rowKeyPrefix || (isFixedOperation ? 'fixed-route' : 'nonfixed-route')
    return {
        rowKey: `${prefix}:${stats.routeID}`,
        rowType: 'route',
        sequence: index + 1,
        routeID: stats.routeID,
        routeName: stats.routeName,
        operationCount: stats.operationCount,
        cellDurations: stats.cellDurations,
        interruptCellDurations: stats.interruptCellDurations,
        isFixedOperation,
    }
}

function createOperationOccupationGroupRow(
    rowKey: string,
    routeName: string,
    operationCount: number,
    children: OperationOccupationTimeTableRow[],
    cellDurations = sumOperationOccupationCellDurations(children.filter((row) => row.rowType === 'route')),
    interruptCellDurations = sumOperationOccupationInterruptCellDurations(children.filter((row) => row.rowType === 'route')),
): OperationOccupationTimeTableRow {
    return {
        rowKey,
        rowType: 'group',
        sequence: '',
        routeID: '',
        routeName,
        operationCount,
        cellDurations,
        interruptCellDurations,
        children,
    }
}

function sumOperationOccupationCellDurations(rows: OperationOccupationTimeTableRow[]) {
    const totals: Record<string, number> = {}
    rows.forEach((row) => {
        Object.entries(row.cellDurations).forEach(([cellID, seconds]) => {
            const duration = Number(seconds || 0)
            if (cellID && Number.isFinite(duration)) totals[cellID] = (totals[cellID] || 0) + duration
        })
    })
    return totals
}

function sumOperationOccupationInterruptCellDurations(rows: OperationOccupationTimeTableRow[]) {
    const totals: Record<string, number> = {}
    rows.forEach((row) => {
        Object.entries(row.interruptCellDurations || {}).forEach(([cellID, seconds]) => {
            const duration = Number(seconds || 0)
            if (cellID && Number.isFinite(duration)) totals[cellID] = (totals[cellID] || 0) + duration
        })
    })
    return totals
}

function sumOperationOccupationOperationCount(rows: OperationOccupationTimeTableRow[]) {
    return rows.reduce((sum, row) => {
        const count = Number(row.operationCount || 0)
        return Number.isFinite(count) ? sum + count : sum
    }, 0)
}

function flattenOperationOccupationTimeTableRows(rows: OperationOccupationTimeTableRow[]) {
    const flattenedRows: OperationOccupationTimeTableRow[] = []
    rows.forEach((row) => {
        flattenedRows.push(row)
        if (row.children?.length) {
            flattenedRows.push(...flattenOperationOccupationTimeTableRows(row.children))
        }
    })
    return flattenedRows
}

function buildOperationOccupationSnapshotDisplayRows(rows: OperationOccupationTimeTableRow[]) {
    if (rows.some((row) => row.rowType === 'group' && row.children?.length)) return rows

    const fixedTotalIndex = rows.findIndex((row) => row.rowType === 'fixed-total')
    if (fixedTotalIndex < 0) return rows

    const fixedRows: OperationOccupationTimeTableRow[] = []
    const nonFixedRows: OperationOccupationTimeTableRow[] = []
    const summaryRows: OperationOccupationTimeTableRow[] = []
    let fixedTotalRow: OperationOccupationTimeTableRow | null = null

    rows.forEach((row, index) => {
        if (row.rowType === 'total' || row.rowType === 'utilization') {
            summaryRows.push(row)
            return
        }
        if (row.rowType === 'fixed-total') {
            fixedTotalRow = { ...row, isFixedOperation: true }
            return
        }
        if (row.rowType !== 'route') return

        const routeRow = {
            ...row,
            isFixedOperation: index < fixedTotalIndex,
        }
        if (index < fixedTotalIndex) {
            fixedRows.push(routeRow)
        } else {
            nonFixedRows.push(routeRow)
        }
    })

    const displayRows: OperationOccupationTimeTableRow[] = []
    if (fixedRows.length > 0 || fixedTotalRow) {
        const snapshotFixedTotalRow = fixedTotalRow as OperationOccupationTimeTableRow | null
        const fixedCellDurations = snapshotFixedTotalRow?.cellDurations || sumOperationOccupationCellDurations(fixedRows)
        const fixedInterruptCellDurations = snapshotFixedTotalRow?.interruptCellDurations || sumOperationOccupationInterruptCellDurations(fixedRows)
        displayRows.push(createOperationOccupationGroupRow(
            'snapshot:group:fixed',
            t('operationPlan.operationOccupationTimeTable.groups.fixedOperation'),
            sumOperationOccupationOperationCount(fixedRows),
            snapshotFixedTotalRow ? [...fixedRows, snapshotFixedTotalRow] : fixedRows,
            fixedCellDurations,
            fixedInterruptCellDurations,
        ))
    }
    if (nonFixedRows.length > 0) {
        const nonFixedCellDurations = sumOperationOccupationCellDurations(nonFixedRows)
        const nonFixedInterruptCellDurations = sumOperationOccupationInterruptCellDurations(nonFixedRows)
        displayRows.push(createOperationOccupationGroupRow(
            'snapshot:group:nonfixed',
            t('operationPlan.operationOccupationTimeTable.groups.nonFixedOperation'),
            sumOperationOccupationOperationCount(nonFixedRows),
            nonFixedRows,
            nonFixedCellDurations,
            nonFixedInterruptCellDurations,
        ))
    }

    return [...displayRows, ...summaryRows]
}

function formatOperationBottleneckCapacity(value: number | null | undefined) {
    const capacity = Number(value || 0)
    if (!Number.isFinite(capacity) || capacity <= 0) return ''
    return capacity.toFixed(2)
}

function getStationRouteEndDisplayName(routeEnd: StationRouteEndOption | null) {
    if (!routeEnd) return t('operationPlan.operationBottleneckAnalysis.summary.unboundRouteEnd')
    const tag = `${routeEnd.segmentTag || ''}${routeEnd.sidingTag || ''}`.trim()
    return tag ? `${tag} (${routeEnd.id})` : routeEnd.id
}

function normalizeOperationBottleneckSummaryCategoriesResponse(data: any) {
    return (Array.isArray(data) ? data : [])
        .map((item, index) => normalizeOperationBottleneckSummaryCategory(item, index))
        .filter((item): item is OperationBottleneckSummaryCategory => item !== null)
        .sort((left, right) => left.sortOrder - right.sortOrder)
}

function buildOperationBottleneckSummaryCategoryPayload() {
    const { instanceID, stationSchemeID, operationPlanID } = getOperationPlanScope()
    if (!instanceID || !stationSchemeID || !operationPlanID) return null

    return {
        instanceID,
        stationSchemeID,
        operationPlanID,
        categories: operationBottleneckSummaryCategories.value.map((category, index) => ({
            instanceID,
            stationSchemeID,
            operationPlanID,
            categoryID: category.id,
            name: category.name,
            routeIDList: serializeRouteIDList(category.routeIDs),
            sortOrder: index,
        })),
    }
}

async function saveOperationBottleneckSummaryCategoriesNow() {
    const payload = buildOperationBottleneckSummaryCategoryPayload()
    if (!payload) return false

    try {
        const response = await axios.put('/OperationPlan/SaveBottleneckSummaryCategories', payload)
        if (
            payload.instanceID !== props.selectedInstanceId ||
            payload.stationSchemeID !== currentStationSchemeId.value.trim() ||
            payload.operationPlanID !== getCurrentOperationPlanID()
        ) {
            return false
        }

        operationBottleneckSummaryCategories.value = normalizeOperationBottleneckSummaryCategoriesResponse(response.data)
        return true
    } catch (error) {
        console.error('Failed to save operation bottleneck summary categories:', error)
        return false
    }
}

function scheduleSaveOperationBottleneckSummaryCategories(delay = 500) {
    if (operationBottleneckSummaryCategorySaveTimer) {
        window.clearTimeout(operationBottleneckSummaryCategorySaveTimer)
    }
    operationBottleneckSummaryCategorySaveTimer = window.setTimeout(() => {
        operationBottleneckSummaryCategorySaveTimer = null
        void saveOperationBottleneckSummaryCategoriesNow()
    }, delay)
}

function addOperationBottleneckSummaryCategory() {
    const index = operationBottleneckSummaryCategories.value.length + 1
    const category: OperationBottleneckSummaryCategory = {
        id: `summary_${Date.now()}_${index}`,
        name: t('operationPlan.operationBottleneckAnalysis.summary.defaultCategoryName', { index }),
        routeIDs: [],
        sortOrder: index - 1,
    }
    operationBottleneckSummaryCategories.value.push(category)
    if (usingOperationAnalysisSnapshot.value && operationAnalysisSnapshot.value) {
        operationAnalysisSnapshot.value.throughputSummaryRows.push({
            categoryID: category.id,
            groupKey: category.id,
            groupText: category.name,
            routeIDs: [],
            routeCount: 0,
            operationCount: 0,
            capacityTotal: null,
            capacityAverage: null,
        })
    }
    scheduleSaveOperationBottleneckSummaryCategories()
}

function updateOperationBottleneckSummaryCategoryName(categoryID: string, name: string) {
    const category = operationBottleneckSummaryCategories.value.find((item) => item.id === categoryID)
    if (category) {
        category.name = name
        const snapshotRow = operationAnalysisSnapshot.value?.throughputSummaryRows.find((row) => row.categoryID === categoryID)
        if (snapshotRow) snapshotRow.groupText = name
        scheduleSaveOperationBottleneckSummaryCategories()
    }
}

function deleteOperationBottleneckSummaryCategory(categoryID: string) {
    operationBottleneckSummaryCategories.value = operationBottleneckSummaryCategories.value.filter((item) => item.id !== categoryID)
    if (operationAnalysisSnapshot.value) {
        operationAnalysisSnapshot.value.throughputSummaryRows = operationAnalysisSnapshot.value.throughputSummaryRows.filter((row) => row.categoryID !== categoryID)
    }
    if (operationBottleneckRoutePickerCategoryId.value === categoryID) {
        closeOperationBottleneckRoutePicker()
    }
    scheduleSaveOperationBottleneckSummaryCategories()
}

function getOperationBottleneckSummarySelectionText(row: OperationBottleneckSummaryRow) {
    return t('operationPlan.operationBottleneckAnalysis.summary.selectedRoutes', { count: row.routeIDs.length })
}

function openOperationBottleneckRoutePicker(categoryID: string) {
    const category = operationBottleneckSummaryCategories.value.find((item) => item.id === categoryID)
    if (!category) return
    operationBottleneckRoutePickerCategoryId.value = categoryID
    operationBottleneckRoutePickerSelectedIds.value = normalizeRoutePickerValues(category.routeIDs)
    operationBottleneckRoutePickerVisible.value = true
}

function closeOperationBottleneckRoutePicker() {
    operationBottleneckRoutePickerVisible.value = false
    operationBottleneckRoutePickerCategoryId.value = ''
    operationBottleneckRoutePickerSelectedIds.value = []
}

function confirmOperationBottleneckRoutePicker() {
    const category = operationBottleneckSummaryCategories.value.find((item) => item.id === operationBottleneckRoutePickerCategoryId.value)
    if (category) {
        category.routeIDs = normalizeRoutePickerValues(operationBottleneckRoutePickerSelectedIds.value)
        const snapshotRow = operationAnalysisSnapshot.value?.throughputSummaryRows.find((row) => row.categoryID === category.id)
        if (snapshotRow) {
            snapshotRow.routeIDs = category.routeIDs
            snapshotRow.routeCount = category.routeIDs.length
        }
        scheduleSaveOperationBottleneckSummaryCategories()
    }
    closeOperationBottleneckRoutePicker()
}

async function calculateOperationBottleneckSummary() {
    const instanceID = props.selectedInstanceId
    const stationSchemeID = currentStationSchemeId.value.trim()
    if (!instanceID || !stationSchemeID) {
        ElMessage.warning(t('operationPlan.empty.selectScheme'))
        return
    }

    savingOperationBottleneckSummaryCategories.value = true
    try {
        if (operationBottleneckSummaryCategorySaveTimer) {
            window.clearTimeout(operationBottleneckSummaryCategorySaveTimer)
            operationBottleneckSummaryCategorySaveTimer = null
        }
        const saved = await saveOperationBottleneckSummaryCategoriesNow()
        if (!saved) throw new Error('Failed to save operation bottleneck summary categories.')
        await loadOperationPlanChartData()
        ElMessage.success(t('operationPlan.operationBottleneckAnalysis.summary.messages.calculateSuccess'))
    } catch (error) {
        console.error('Failed to calculate operation bottleneck summary:', error)
        ElMessage.error(t('operationPlan.operationBottleneckAnalysis.summary.messages.calculateFailed'))
    } finally {
        savingOperationBottleneckSummaryCategories.value = false
    }
}

function isOperationBottleneckRoutePickerRouteSelected(routeID: string) {
    return operationBottleneckRoutePickerSelectedIdSet.value.has(routeID)
}

function toggleOperationBottleneckRoutePickerRoute(routeID: string, selected: unknown) {
    const routeIDs = new Set(operationBottleneckRoutePickerSelectedIds.value)
    if (Boolean(selected)) {
        routeIDs.add(routeID)
    } else {
        routeIDs.delete(routeID)
    }
    operationBottleneckRoutePickerSelectedIds.value = Array.from(routeIDs)
}

function toggleOperationBottleneckRoutePickerFilteredSelection(selected: unknown) {
    const routeIDs = new Set(operationBottleneckRoutePickerSelectedIds.value)
    filteredOperationBottleneckRoutePickerRows.value.forEach((row) => {
        if (Boolean(selected)) {
            routeIDs.add(row.routeID)
        } else {
            routeIDs.delete(row.routeID)
        }
    })
    operationBottleneckRoutePickerSelectedIds.value = Array.from(routeIDs)
}

function clearOperationBottleneckRoutePickerFilters() {
    operationBottleneckRoutePickerFilters.value = {
        keyword: '',
        startRouteEndIds: [],
        endRouteEndIds: [],
    }
}

function formatOperationOccupationCellValue(row: OperationOccupationTimeTableRow, cellID: string) {
    const value = row.cellDurations[cellID]
    if (row.rowType === 'utilization') {
        return formatOperationOccupationUtilization(value)
    }
    const text = formatOperationOccupationDuration(value)
    const interruptSeconds = Number((row.interruptCellDurations || {})[cellID] || 0)
    if (!Number.isFinite(interruptSeconds) || interruptSeconds <= 0) return text

    const interruptText = `(${formatOperationOccupationDuration(interruptSeconds)})`
    const totalSeconds = Number(value || 0)
    if (row.rowType === 'route' && Math.round(totalSeconds) === Math.round(interruptSeconds)) {
        return interruptText
    }
    if (!text) return interruptText
    return `${text} ${interruptText}`
}

function getOperationOccupationTimeTableRowClassName({ row }: { row: OperationOccupationTimeTableRow }) {
    if (row.rowType === 'group') return 'operation-occupation-time-group-row'
    if (row.rowType === 'fixed-total') return 'operation-occupation-time-fixed-total-row'
    if (row.rowType === 'total') return 'operation-occupation-time-total-row'
    if (row.rowType === 'utilization') return 'operation-occupation-time-utilization-row'
    return ''
}

function getRouteDisplayName(routeID: string) {
    return stationRouteOptionMap.value.get(routeID)?.name || routeID
}

function isOperationPlanChartDataTab(tab: OperationPlanSubTab) {
    return tab === 'trainOperationChart' ||
        tab === 'operationOccupationTimeTable' ||
        tab === 'operationBottleneckAnalysis' ||
        tab === 'operationThroughputSummary'
}

function normalizeSnapshotNumber(value: number | null | undefined) {
    const parsed = Number(value)
    return Number.isFinite(parsed) ? parsed : null
}

function normalizeSnapshotCellDurations(cellDurations: Record<string, number>) {
    const durations: Record<string, number> = {}
    Object.entries(cellDurations || {}).forEach(([cellID, seconds]) => {
        const id = cellID.trim()
        const duration = Number(seconds)
        if (id && Number.isFinite(duration)) durations[id] = duration
    })
    return durations
}

function buildOperationAnalysisSnapshotPayload() {
    const { instanceID, stationSchemeID, operationPlanID } = getOperationPlanScope()
    if (!instanceID || !stationSchemeID || !operationPlanID || usingOperationAnalysisSnapshot.value) return null
    if (operationPlanChartBars.value.length === 0) return null
    if (
        operationOccupationTimeSnapshotRows.value.length === 0 &&
        operationBottleneckAnalysisRows.value.length === 0 &&
        operationBottleneckSummaryRows.value.length === 0
    ) {
        return null
    }

    const totalTimeSeconds = readOptionalInteger({ value: operationOccupationTotalTimeSeconds.value }, 'value')
    return {
        instanceID,
        stationSchemeID,
        operationPlanID,
        totalTimeSeconds,
        cells: operationOccupationTimeTableCells.value.map((cell) => ({
            id: cell.id,
            name: cell.name || cell.id,
        })),
        occupationTimeTableRows: operationOccupationTimeSnapshotRows.value.map((row) => ({
            rowType: row.rowType,
            sequence: String(row.sequence ?? ''),
            routeID: row.routeID,
            routeName: row.routeName,
            operationCount: String(row.operationCount ?? ''),
            cellDurations: normalizeSnapshotCellDurations(row.cellDurations),
            interruptCellDurations: normalizeSnapshotCellDurations(row.interruptCellDurations || {}),
        })),
        bottleneckAnalysisRows: operationBottleneckAnalysisRows.value.map((row) => ({
            routeID: row.routeID,
            routeName: row.routeName,
            operationCount: row.operationCount,
            bottleneckCellID: row.bottleneckCellID,
            bottleneckCellName: row.bottleneckCellName,
            bottleneckUtilization: normalizeSnapshotNumber(row.bottleneckUtilization),
            throughputCapacity: normalizeSnapshotNumber(row.throughputCapacity),
        })),
        throughputSummaryRows: operationBottleneckSummaryRows.value.map((row) => ({
            categoryID: row.categoryID,
            groupKey: row.groupKey,
            groupText: row.groupText,
            routeIDs: normalizeRoutePickerValues(row.routeIDs),
            routeCount: row.routeCount,
            operationCount: row.operationCount,
            capacityTotal: normalizeSnapshotNumber(row.capacityTotal),
            capacityAverage: normalizeSnapshotNumber(row.capacityAverage),
        })),
    }
}

function clearOperationAnalysisSnapshotState() {
    if (operationAnalysisSnapshotSaveTimer) {
        window.clearTimeout(operationAnalysisSnapshotSaveTimer)
        operationAnalysisSnapshotSaveTimer = null
    }
    operationAnalysisSnapshot.value = null
    usingOperationAnalysisSnapshot.value = false
    savingOperationAnalysisSnapshot.value = false
}

async function saveOperationAnalysisSnapshotNow() {
    const payload = buildOperationAnalysisSnapshotPayload()
    if (!payload || savingOperationAnalysisSnapshot.value) return

    savingOperationAnalysisSnapshot.value = true
    try {
        const response = await axios.put('/OperationPlan/SaveOperationAnalysisResult', payload)
        if (
            payload.instanceID !== props.selectedInstanceId ||
            payload.stationSchemeID !== currentStationSchemeId.value.trim() ||
            payload.operationPlanID !== getCurrentOperationPlanID() ||
            usingOperationAnalysisSnapshot.value
        ) {
            return
        }

        operationAnalysisSnapshot.value = normalizeOperationAnalysisSnapshot(response.data) || {
            totalTimeSeconds: payload.totalTimeSeconds,
            cells: payload.cells,
            occupationTimeTableRows: payload.occupationTimeTableRows
                .map((row, index) => normalizeOperationOccupationTimeTableSnapshotRow(row, index))
                .filter((row): row is OperationOccupationTimeTableRow => row !== null),
            bottleneckAnalysisRows: payload.bottleneckAnalysisRows,
            throughputSummaryRows: payload.throughputSummaryRows,
        }
    } catch (error) {
        console.error('Failed to save operation analysis result:', error)
    } finally {
        savingOperationAnalysisSnapshot.value = false
    }
}

function scheduleSaveOperationAnalysisSnapshot(delay = 400) {
    if (usingOperationAnalysisSnapshot.value) return
    if (operationPlanChartBars.value.length === 0) return
    if (operationAnalysisSnapshotSaveTimer) {
        window.clearTimeout(operationAnalysisSnapshotSaveTimer)
    }
    operationAnalysisSnapshotSaveTimer = window.setTimeout(() => {
        operationAnalysisSnapshotSaveTimer = null
        void saveOperationAnalysisSnapshotNow()
    }, delay)
}

async function loadOperationAnalysisSnapshotFallback(
    instanceID: string,
    stationSchemeID: string,
    loadVersion = operationPlanChartLoadVersion,
) {
    const operationPlanID = getCurrentOperationPlanID()
    if (!operationPlanID) return false
    try {
        const response = await axios.get('/OperationPlan/GetOperationAnalysisResult', {
            params: { instanceID, stationSchemeID, operationPlanID },
        })
        if (
            loadVersion !== operationPlanChartLoadVersion ||
            instanceID !== props.selectedInstanceId ||
            stationSchemeID !== currentStationSchemeId.value.trim() ||
            operationPlanID !== getCurrentOperationPlanID()
        ) {
            return false
        }

        const snapshot = normalizeOperationAnalysisSnapshot(response.data)
        if (!snapshot) return false
        operationAnalysisSnapshot.value = snapshot
        usingOperationAnalysisSnapshot.value = true
        operationBottleneckSummaryCategories.value = snapshot.throughputSummaryRows.map((row, index) => ({
            id: row.categoryID || row.groupKey || `summary_${index + 1}`,
            name: row.groupText || row.groupKey || t('operationPlan.operationBottleneckAnalysis.summary.defaultCategoryName', { index: index + 1 }),
            routeIDs: normalizeRoutePickerValues(row.routeIDs),
            sortOrder: index,
        }))
        if (snapshot.totalTimeSeconds && snapshot.totalTimeSeconds > 0) {
            operationOccupationTotalTimeSeconds.value = snapshot.totalTimeSeconds
        }
        return true
    } catch (error) {
        console.error('Failed to load operation analysis result snapshot:', error)
        return false
    }
}

function getOperationPlanChartRouteTimeKey(routeID: string, trainTypeID: string) {
    return `${routeID.trim()}::${trainTypeID.trim()}`
}

function getOperationPlanChartRouteTimes(routeID: string, trainTypeID: string) {
    const specificKey = getOperationPlanChartRouteTimeKey(routeID, trainTypeID)
    const defaultKey = getOperationPlanChartRouteTimeKey(routeID, '')
    const specificRows = stationRouteTimesByKey.value[specificKey] || []
    if (specificRows.length > 0) return specificRows
    return stationRouteTimesByKey.value[defaultKey] || []
}

function getOperationPlanChartTrainColor(trainID: string) {
    const colors = ['#2563eb', '#16a34a', '#dc2626', '#9333ea', '#0891b2', '#ea580c', '#4f46e5', '#0f766e']
    let hash = 0
    for (let index = 0; index < trainID.length; index++) {
        hash = (hash * 31 + trainID.charCodeAt(index)) >>> 0
    }
    return colors[hash % colors.length] || '#2563eb'
}

function assignOperationPlanChartBarLanes(bars: OperationPlanChartBar[]) {
    const laneEndMinutes: number[] = []
    return [...bars]
        .sort((left, right) => left.startMinutes - right.startMinutes || left.endMinutes - right.endMinutes)
        .map((bar) => {
            let lane = laneEndMinutes.findIndex((endMinutes) => bar.startMinutes >= endMinutes)
            if (lane < 0) {
                lane = laneEndMinutes.length
                laneEndMinutes.push(bar.endMinutes)
            } else {
                laneEndMinutes[lane] = bar.endMinutes
            }
            return { ...bar, lane }
        })
}

function getOperationPlanChartTickStep(span: number) {
    if (span <= 120) return 15
    if (span <= 360) return 30
    if (span <= 720) return 60
    if (span <= 1440) return 120
    return 240
}

function operationPlanChartTimeToX(minutes: number) {
    return (minutes - operationPlanChartDomain.value.start) * operationPlanChartPixelsPerMinute.value
}

function getOperationPlanChartRowHeight(row: OperationPlanChartRow) {
    return Math.max(36, 12 + row.laneCount * 24)
}

function getOperationPlanChartRowStyle(row: OperationPlanChartRow) {
    return { height: `${getOperationPlanChartRowHeight(row)}px` }
}

function getOperationPlanChartBarStyle(bar: OperationPlanChartBar) {
    return {
        left: `${operationPlanChartTimeToX(bar.startMinutes)}px`,
        width: `${Math.max(8, (bar.endMinutes - bar.startMinutes) * operationPlanChartPixelsPerMinute.value)}px`,
        top: `${6 + bar.lane * 24}px`,
        backgroundColor: bar.color,
    }
}

function normalizeStationRouteType(type: string) {
    return String(type || '').trim().replace(/\s+/g, '').toLowerCase()
}

function getStationRouteHighlightColor(type: string) {
    const normalizedType = normalizeStationRouteType(type)
    if (normalizedType === 'arrival' || normalizedType === '接车' || normalizedType === '接车进路') return '#ef4444'
    if (normalizedType === 'departure' || normalizedType === '发车' || normalizedType === '发车进路') return '#2563eb'
    if (
        normalizedType === 'locomotive' ||
        normalizedType === '机车出入段' ||
        normalizedType === '机车出入段进路' ||
        normalizedType === '机车走行'
    ) {
        return '#16a34a'
    }
    return '#facc15'
}

function normalizeRoutePickerValues(values: unknown): string[] {
    const result: string[] = []
    const seen = new Set<string>()
    const source = Array.isArray(values) ? values : []
    source.forEach((value) => {
        const id = String(value ?? '').trim()
        if (!id || seen.has(id)) return
        seen.add(id)
        result.push(id)
    })
    return result
}

function getActiveRoutePickerSourceIds() {
    if (routePickerTarget.value === 'trainOperationPlanMovementRoute') {
        return trainOperationPlanMovementForm.value.route ? [trainOperationPlanMovementForm.value.route] : []
    }
    if (routePickerTarget.value === 'trainOperationPlanMovement') {
        return trainOperationPlanMovementRouteIds.value
    }
    return movementTemplateRouteIds.value
}

function sortRouteListOptions(options: RouteListSelectOption[]) {
    return [...options].sort((left, right) => (
        left.name.localeCompare(right.name, undefined, { numeric: true, sensitivity: 'base' }) ||
        left.id.localeCompare(right.id, undefined, { numeric: true, sensitivity: 'base' })
    ))
}

function sortStationRouteOptions(options: StationRouteOption[]) {
    return [...options].sort((left, right) => (
        left.name.localeCompare(right.name, undefined, { numeric: true, sensitivity: 'base' }) ||
        left.id.localeCompare(right.id, undefined, { numeric: true, sensitivity: 'base' })
    ))
}

function buildRoutePickerFilterOptions(values: string[]) {
    return sortRouteListOptions(
        normalizeRoutePickerValues(values).map((id) => ({ id, name: id })),
    )
}

function getRoutePickerFilterReferencedIds(field: RoutePickerObjectFilterField) {
    if (field === 'startNodeIds') return routePickerRoutes.value.map((route) => route.startNodeID)
    if (field === 'endNodeIds') return routePickerRoutes.value.map((route) => route.endNodeID)
    if (field === 'nodeIds') {
        return routePickerRoutes.value.flatMap((route) => [
            route.startNodeID,
            route.endNodeID,
            ...parseRouteReferenceList(route.nodeList),
        ])
    }
    if (field === 'linkIds') return routePickerRoutes.value.flatMap((route) => parseRouteReferenceList(route.linkList))
    if (field === 'cellIds') return routePickerRoutes.value.flatMap((route) => parseRouteReferenceList(route.cellList))
    if (field === 'switchIds') return routePickerRoutes.value.flatMap((route) => parseRouteReferenceList(route.switchList))
    return routePickerRoutes.value.flatMap((route) => parseRouteReferenceList(route.signalList))
}

function getRoutePickerFilterSelectOptions(control: RoutePickerFilterControl) {
    return buildRoutePickerFilterOptions([
        ...getRoutePickerFilterReferencedIds(control.field),
        ...routePickerFilters.value[control.field],
    ])
}

function clearRoutePickerFilters() {
    routePickerFilters.value = createEmptyRoutePickerFilters()
    routePickerNodeFilterStage.value = 'start'
    syncRoutePickerPreviewWithFilteredRoutes()
}

function clearRoutePickerNodeFilters() {
    routePickerFilters.value = {
        ...routePickerFilters.value,
        startNodeIds: [],
        endNodeIds: [],
    }
    routePickerNodeFilterStage.value = 'start'
    syncRoutePickerPreviewWithFilteredRoutes()
}

function syncRoutePickerPreviewWithFilteredRoutes() {
    if (!routePickerEndpointFiltersReady.value) {
        routePickerPreviewRouteId.value = ''
        return
    }
    if (!routePickerPreviewRouteId.value) return
    if (filteredRoutePickerRoutes.value.some((route) => route.id === routePickerPreviewRouteId.value)) return
    routePickerPreviewRouteId.value = ''
}

function handleRoutePickerNodePick(payload: RouteNodePickPayload) {
    if (payload?.target !== routePickerNodePickTarget) return

    const nodeId = readString(payload, 'nodeId', 'nodeID').trim()
    if (!nodeId) return

    if (routePickerNodeFilterStage.value === 'start') {
        routePickerFilters.value = {
            ...routePickerFilters.value,
            startNodeIds: [nodeId],
            endNodeIds: [],
        }
        routePickerPreviewRouteId.value = ''
        routePickerNodeFilterStage.value = 'end'
        syncRoutePickerPreviewWithFilteredRoutes()
        ElMessage.success(t('routeDesign.stationRoute.messages.startPicked', { nodeId }))
        ElMessage.info(t('routeDesign.stationRoute.messages.pickEnd'))
        return
    }

    routePickerFilters.value = {
        ...routePickerFilters.value,
        endNodeIds: [nodeId],
    }
    routePickerPreviewRouteId.value = ''
    routePickerNodeFilterStage.value = 'start'
    syncRoutePickerPreviewWithFilteredRoutes()
    ElMessage.success(t('routeDesign.stationRoute.messages.endPicked', { nodeId }))
}

function routeMatchesScalarFilter(selectedIds: string[], value: string) {
    if (selectedIds.length === 0) return true
    return selectedIds.includes(String(value || '').trim())
}

function routeMatchesListFilter(selectedIds: string[], routeIds: string[]) {
    if (selectedIds.length === 0) return true
    const routeIdSet = new Set(normalizeRoutePickerValues(routeIds))
    return selectedIds.some((id) => routeIdSet.has(id))
}

function routeMatchesRoutePickerFilters(route: StationRouteOption) {
    const filters = routePickerFilters.value
    if (!routeMatchesScalarFilter(filters.types, route.type)) return false
    if (!routeMatchesScalarFilter(filters.startNodeIds, route.startNodeID)) return false
    if (!routeMatchesScalarFilter(filters.endNodeIds, route.endNodeID)) return false
    if (!routeMatchesListFilter(filters.nodeIds, [
        route.startNodeID,
        route.endNodeID,
        ...parseRouteReferenceList(route.nodeList),
    ])) {
        return false
    }
    if (!routeMatchesListFilter(filters.linkIds, parseRouteReferenceList(route.linkList))) return false
    if (!routeMatchesListFilter(filters.cellIds, parseRouteReferenceList(route.cellList))) return false
    if (!routeMatchesListFilter(filters.switchIds, parseRouteReferenceList(route.switchList))) return false
    return routeMatchesListFilter(filters.signalIds, parseRouteReferenceList(route.signalList))
}

function isRoutePickerRouteSelected(routeID: string) {
    return routePickerSelectedIdSet.value.has(routeID)
}

function toggleRoutePickerRoute(routeID: string, checked: unknown) {
    if (routePickerSingleSelect.value) {
        routePickerSelectedIds.value = checked ? [routeID] : []
        routePickerPreviewRouteId.value = checked && routePickerEndpointFiltersReady.value ? routeID : ''
        syncRoutePickerPreviewWithFilteredRoutes()
        return
    }

    const selected = new Set(routePickerSelectedIds.value)
    if (checked) {
        selected.add(routeID)
        routePickerPreviewRouteId.value = routePickerEndpointFiltersReady.value ? routeID : ''
    } else {
        selected.delete(routeID)
    }
    routePickerSelectedIds.value = normalizeRoutePickerValues(Array.from(selected))
}

function toggleRoutePickerFilteredSelection(checked: unknown) {
    if (routePickerSingleSelect.value) return

    const selected = new Set(routePickerSelectedIds.value)
    filteredRoutePickerRoutes.value.forEach((route) => {
        if (checked) {
            selected.add(route.id)
        } else {
            selected.delete(route.id)
        }
    })
    routePickerSelectedIds.value = normalizeRoutePickerValues(Array.from(selected))
}

function clampRoutePickerTableHeight(height: number) {
    const splitHeight = routePickerSplitRef.value?.clientHeight || 0
    const availableHeight = splitHeight > 0
        ? splitHeight
        : Math.max(0, window.innerHeight - 160)
    const maxHeight = Math.max(
        routePickerMinTableHeight,
        availableHeight - routePickerMinLayoutHeight - routePickerSplitterHeight,
    )
    const normalizedHeight = Number.isFinite(height) ? height : routePickerDefaultTableHeight
    return Math.round(Math.min(Math.max(normalizedHeight, routePickerMinTableHeight), maxHeight))
}

function setRoutePickerTableHeight(height: number) {
    routePickerTableHeight.value = clampRoutePickerTableHeight(height)
}

function resetRoutePickerTableHeight() {
    const splitHeight = routePickerSplitRef.value?.clientHeight || 0
    if (!splitHeight) {
        setRoutePickerTableHeight(routePickerDefaultTableHeight)
        return
    }

    setRoutePickerTableHeight(Math.round(splitHeight * 0.42))
}

function startRoutePickerTableResize(event: PointerEvent) {
    if (event.button !== 0) return

    event.preventDefault()
    routePickerResizeState = {
        startY: event.clientY,
        startTableHeight: routePickerTableHeight.value,
    }
    window.addEventListener('pointermove', handleRoutePickerTableResize, { passive: false })
    window.addEventListener('pointerup', stopRoutePickerTableResize, { once: true })
    window.addEventListener('pointercancel', stopRoutePickerTableResize, { once: true })
}

function handleRoutePickerTableResize(event: PointerEvent) {
    if (!routePickerResizeState) return

    event.preventDefault()
    setRoutePickerTableHeight(routePickerResizeState.startTableHeight + event.clientY - routePickerResizeState.startY)
}

function stopRoutePickerTableResize() {
    if (!routePickerResizeState) return

    routePickerResizeState = null
    window.removeEventListener('pointermove', handleRoutePickerTableResize)
    window.removeEventListener('pointerup', stopRoutePickerTableResize)
    window.removeEventListener('pointercancel', stopRoutePickerTableResize)
    void nextTick(() => fitRoutePickerLayoutToFullView())
}

function handleRoutePickerTableResizeKeydown(event: KeyboardEvent) {
    const smallStep = event.shiftKey ? 40 : 20
    const largeStep = event.shiftKey ? 120 : 80
    if (event.key === 'ArrowUp') {
        event.preventDefault()
        setRoutePickerTableHeight(routePickerTableHeight.value - smallStep)
    } else if (event.key === 'ArrowDown') {
        event.preventDefault()
        setRoutePickerTableHeight(routePickerTableHeight.value + smallStep)
    } else if (event.key === 'PageUp') {
        event.preventDefault()
        setRoutePickerTableHeight(routePickerTableHeight.value - largeStep)
    } else if (event.key === 'PageDown') {
        event.preventDefault()
        setRoutePickerTableHeight(routePickerTableHeight.value + largeStep)
    } else if (event.key === 'Home') {
        event.preventDefault()
        setRoutePickerTableHeight(routePickerMinTableHeight)
    } else if (event.key === 'End') {
        event.preventDefault()
        setRoutePickerTableHeight(Number.MAX_SAFE_INTEGER)
    } else {
        return
    }
    void nextTick(() => fitRoutePickerLayoutToFullView())
}

async function openRoutePicker(target: RoutePickerTarget = 'movementTemplate') {
    routePickerTarget.value = target
    routePickerSelectedIds.value = normalizeRoutePickerValues(getActiveRoutePickerSourceIds())
    routePickerPreviewRouteId.value = ''
    routePickerVisible.value = true
    await nextTick()
    resetRoutePickerTableHeight()
    await loadRoutePickerLayoutPreview()
}

function closeRoutePicker() {
    routePickerVisible.value = false
}

async function handleRoutePickerOpened() {
    await nextTick()
    setRoutePickerTableHeight(routePickerTableHeight.value)
    fitRoutePickerLayoutToFullView()
}

function handleRoutePickerClosed() {
    stopRoutePickerTableResize()
}

function confirmRoutePicker() {
    const selectedIds = normalizeRoutePickerValues(routePickerSelectedIds.value)
    if (routePickerTarget.value === 'trainOperationPlanMovementRoute') {
        trainOperationPlanMovementForm.value.route = selectedIds[0] || ''
    } else if (routePickerTarget.value === 'trainOperationPlanMovement') {
        trainOperationPlanMovementRouteIds.value = selectedIds
    } else {
        movementTemplateRouteIds.value = selectedIds
    }
    routePickerVisible.value = false
}

function getRoutePickerLayoutKey() {
    return `${props.selectedInstanceId || ''}::${currentStationSchemeId.value.trim()}`
}

function clearRoutePickerLayoutPreview() {
    routePickerLayoutData.value = null
    routePickerLayoutKey.value = ''
    routePickerLayoutDisplayStyles.value = {}
    routePickerLayoutCells.value = []
    routePickerLayoutGridSpacing.value = 20
    routePickerLayoutScaleX.value = 1
    routePickerLayoutScaleY.value = 1
    loadingRoutePickerLayout.value = false
    routePickerLayoutEditorRef.value?.clearElements?.()
}

function fitRoutePickerLayoutDataRect(
    rect: { minX: number; minY: number; maxX: number; maxY: number } | null,
    options: { screenMargin?: number; padding?: number } = {},
) {
    if (!rect) return
    const screenMargin = Math.max(0, Number(options.screenMargin ?? 24))
    const viewport = routePickerLayoutViewportRef.value
    if (viewport) {
        const width = Math.max(1, rect.maxX - rect.minX)
        const height = Math.max(1, rect.maxY - rect.minY)
        const availableWidth = Math.max(1, viewport.clientWidth - screenMargin * 2)
        const availableHeight = Math.max(1, viewport.clientHeight - screenMargin * 2)
        const scale = Math.max(0.18, Math.min(3, Math.min(availableWidth / width, availableHeight / height)))
        routePickerLayoutScaleX.value = Number(scale.toFixed(2))
        routePickerLayoutScaleY.value = Number(scale.toFixed(2))
    }
    nextTick(() => routePickerLayoutEditorRef.value?.scrollDataRectIntoView?.(rect, {
        screenMargin,
        padding: options.padding ?? 80,
    }))
}

function fitRoutePickerLayoutToFullView() {
    const fullRect = routePickerLayoutEditorRef.value?.getFullViewRect?.({ screenMargin: 40 }) || null
    fitRoutePickerLayoutDataRect(fullRect, { screenMargin: 24, padding: 96 })
}

async function loadRoutePickerLayoutIntoEditor() {
    if (!routePickerLayoutData.value) return
    await nextTick()
    routePickerLayoutEditorRef.value?.loadDataFromJson?.(routePickerLayoutData.value)
    await nextTick()
    fitRoutePickerLayoutToFullView()
}

async function loadRoutePickerLayoutPreview() {
    const instanceID = props.selectedInstanceId
    const stationSchemeID = currentStationSchemeId.value.trim()
    if (!instanceID || !stationSchemeID) {
        clearRoutePickerLayoutPreview()
        return
    }

    const layoutKey = getRoutePickerLayoutKey()
    if (routePickerLayoutData.value && routePickerLayoutKey.value === layoutKey) {
        await loadRoutePickerLayoutIntoEditor()
        return
    }

    const loadVersion = ++routePickerLayoutLoadVersion
    loadingRoutePickerLayout.value = true
    try {
        const response = await axios.post('/StationLayout/GetJson', null, {
            params: { instanceID, stationSchemeID },
        })
        if (loadVersion !== routePickerLayoutLoadVersion || layoutKey !== getRoutePickerLayoutKey()) return

        routePickerLayoutData.value = response.data
        routePickerLayoutKey.value = layoutKey
        routePickerLayoutDisplayStyles.value = getLayoutDisplayStyles(response.data)
        routePickerLayoutCells.value = getLayoutCells(response.data)
        routePickerLayoutGridSpacing.value = getLayoutGridSpacing(response.data)
        await loadRoutePickerLayoutIntoEditor()
    } catch (error) {
        if (loadVersion !== routePickerLayoutLoadVersion) return
        console.error('Failed to load operation plan route preview layout:', error)
        clearRoutePickerLayoutPreview()
        ElMessage.error(t('routeDesign.messages.loadFailed'))
    } finally {
        if (loadVersion === routePickerLayoutLoadVersion) {
            loadingRoutePickerLayout.value = false
        }
    }
}

function selectRoutePickerPreviewRoute(row: StationRouteOption) {
    if (!routePickerEndpointFiltersReady.value) {
        routePickerPreviewRouteId.value = ''
        return
    }
    routePickerPreviewRouteId.value = row.id
}

function isTrainTemplateEditing(row: TrainTemplate) {
    return !row.isDraft && trainTemplateEditingId.value === row.trainTemplateID
}

function isTrainTemplateInlineEditing(row: TrainTemplate) {
    return Boolean(row.isDraft) || isTrainTemplateEditing(row)
}

function isMovementTemplateEditing(row: MovementTemplate) {
    return !row.isDraft && movementTemplateEditingId.value === row.movementID
}

function isMovementTemplateInlineEditing(row: MovementTemplate) {
    return Boolean(row.isDraft) || isMovementTemplateEditing(row)
}

function isTrainTemplateExpanded(row: TrainTemplate) {
    if (isTrainTemplateInlineEditing(row)) return false
    return selectedTrainTemplateId.value === row.trainTemplateID
}

function getTrainTemplateRowClassName({ row }: { row: TrainTemplate }) {
    if (row.isDraft) return 'is-draft-row'
    if (isTrainTemplateEditing(row)) return 'is-edit-row'
    return isTrainTemplateExpanded(row) ? 'is-expanded-row' : ''
}

function getMovementTemplateRowClassName({ row }: { row: MovementTemplate }) {
    if (row.isDraft) return 'is-draft-row'
    return isMovementTemplateEditing(row) ? 'is-edit-row' : ''
}

function getTrainOperationPlanTrainRowClassName({ row }: { row: TrainOperationPlanTrain }) {
    if (row.isDraft) return 'is-draft-row'
    if (isTrainOperationPlanTrainEditing(row)) return 'is-edit-row'
    return isTrainOperationPlanTrainExpanded(row) ? 'is-expanded-row' : ''
}

function getTrainOperationPlanMovementRowClassName({ row }: { row: TrainOperationPlanMovement }) {
    if (row.isDraft) return 'is-draft-row'
    return isTrainOperationPlanMovementEditing(row) ? 'is-edit-row' : ''
}

function getTrainTemplateRowKey(row: TrainTemplate) {
    return row.isDraft ? '__train_template_draft__' : row.trainTemplateID
}

function getMovementTemplateRowKey(row: MovementTemplate) {
    return row.isDraft ? '__movement_template_draft__' : row.movementID
}

function getTrainOperationPlanTrainRowKey(row: TrainOperationPlanTrain) {
    return row.isDraft ? '__train_operation_plan_train_draft__' : row.id
}

function getTrainOperationPlanMovementRowKey(row: TrainOperationPlanMovement) {
    return `${row.trainID}::${row.movementID}::${row.earliestStartTime}`
}

function getTrainOperationPlanMovementIdentityKey(row: TrainOperationPlanMovement) {
    return `${row.trainID}::${row.movementID}`
}

function assignMovementTemplateSortOrders(items: MovementTemplate[]) {
    items.forEach((item, index) => {
        item.sortOrder = index
    })
    return items
}

function assignTrainOperationPlanMovementSortOrders(items: TrainOperationPlanMovement[]) {
    items.forEach((item, index) => {
        item.sortOrder = index
    })
    return items
}

function getMovementTemplateOrderIndex(row: MovementTemplate) {
    return movementTemplates.value.findIndex((item) => item.movementID === row.movementID)
}

function canMoveMovementTemplate(row: MovementTemplate, direction: -1 | 1) {
    if (row.isDraft || !canEditMovementTemplates.value || operationPlanInlineActive.value || savingMovementTemplate.value) return false
    const index = getMovementTemplateOrderIndex(row)
    return index >= 0 && index + direction >= 0 && index + direction < movementTemplates.value.length
}

async function moveMovementTemplate(row: MovementTemplate, direction: -1 | 1) {
    if (!canMoveMovementTemplate(row, direction)) return

    const previousItems = movementTemplates.value.map((item) => ({ ...item }))
    const nextItems = movementTemplates.value.map((item) => ({ ...item }))
    const index = nextItems.findIndex((item) => item.movementID === row.movementID)
    const targetIndex = index + direction
    const currentItem = nextItems[index]
    const targetItem = nextItems[targetIndex]
    if (!currentItem || !targetItem) return
    nextItems[index] = targetItem
    nextItems[targetIndex] = currentItem
    movementTemplates.value = assignMovementTemplateSortOrders(nextItems)

    savingMovementTemplate.value = true
    try {
        const instanceID = props.selectedInstanceId
        const stationSchemeID = currentStationSchemeId.value.trim()
        const operationPlanID = getCurrentOperationPlanID()
        const trainTemplateID = selectedTrainTemplate.value?.trainTemplateID || row.trainTemplateID
        const response = await axios.put('/OperationPlan/UpdateMovementTemplateOrder', {
            instanceID,
            stationSchemeID,
            operationPlanID,
            trainTemplateID,
            items: movementTemplates.value.map((item, sortOrder) => ({
                movementID: item.movementID,
                sortOrder,
            })),
        })
        const savedItems = (Array.isArray(response.data) ? response.data : [])
            .map(normalizeMovementTemplate)
            .filter((item): item is MovementTemplate => item !== null)
        if (savedItems.length > 0) {
            movementTemplates.value = savedItems
        }
    } catch (error) {
        movementTemplates.value = previousItems
        console.error('Failed to update movement template order:', error)
        ElMessage.error(t('operationPlan.movement.messages.updateFailed'))
    } finally {
        savingMovementTemplate.value = false
    }
}

function getTrainOperationPlanMovementOrderIndex(row: TrainOperationPlanMovement) {
    const key = getTrainOperationPlanMovementIdentityKey(row)
    return selectedTrainOperationPlanMovements.value.findIndex((item) => getTrainOperationPlanMovementIdentityKey(item) === key)
}

function canMoveTrainOperationPlanMovement(row: TrainOperationPlanMovement, direction: -1 | 1) {
    if (row.isDraft || !canEditTrainOperationPlan.value || operationPlanInlineActive.value || savingTrainOperationPlanMovement.value) return false
    const index = getTrainOperationPlanMovementOrderIndex(row)
    return index >= 0 && index + direction >= 0 && index + direction < selectedTrainOperationPlanMovements.value.length
}

function replaceTrainOperationPlanMovementsForTrain(trainID: string, nextItems: TrainOperationPlanMovement[]) {
    let inserted = false
    const result: TrainOperationPlanMovement[] = []
    trainOperationPlanMovements.value.forEach((item) => {
        if (item.trainID !== trainID) {
            result.push(item)
            return
        }

        if (!inserted) {
            result.push(...nextItems)
            inserted = true
        }
    })
    if (!inserted) result.push(...nextItems)
    trainOperationPlanMovements.value = result
}

async function moveTrainOperationPlanMovement(row: TrainOperationPlanMovement, direction: -1 | 1) {
    if (!canMoveTrainOperationPlanMovement(row, direction)) return

    const previousItems = trainOperationPlanMovements.value.map((item) => ({ ...item }))
    const nextItems = selectedTrainOperationPlanMovements.value.map((item) => ({ ...item }))
    const index = nextItems.findIndex((item) => getTrainOperationPlanMovementIdentityKey(item) === getTrainOperationPlanMovementIdentityKey(row))
    const targetIndex = index + direction
    const currentItem = nextItems[index]
    const targetItem = nextItems[targetIndex]
    if (!currentItem || !targetItem) return
    nextItems[index] = targetItem
    nextItems[targetIndex] = currentItem
    replaceTrainOperationPlanMovementsForTrain(row.trainID, assignTrainOperationPlanMovementSortOrders(nextItems))

    savingTrainOperationPlanMovement.value = true
    try {
        const response = await axios.put('/OperationPlan/UpdateMovementOrder', {
            instanceID: props.selectedInstanceId,
            stationSchemeID: currentStationSchemeId.value.trim(),
            operationPlanID: getCurrentOperationPlanID(),
            trainID: row.trainID,
            items: nextItems.map((item, sortOrder) => ({
                movementID: item.movementID,
                sortOrder,
            })),
        })
        normalizeTrainOperationPlanResponse(response.data)
    } catch (error) {
        trainOperationPlanMovements.value = previousItems
        console.error('Failed to update movement order:', error)
        ElMessage.error(t('operationPlan.trainOperationPlan.movement.messages.updateFailed'))
    } finally {
        savingTrainOperationPlanMovement.value = false
    }
}

function isTrainOperationPlanTrainEditing(row: TrainOperationPlanTrain) {
    return !row.isDraft && trainOperationPlanTrainEditingId.value === row.id
}

function isTrainOperationPlanTrainInlineEditing(row: TrainOperationPlanTrain) {
    return Boolean(row.isDraft) || isTrainOperationPlanTrainEditing(row)
}

function isTrainOperationPlanTrainExpanded(row: TrainOperationPlanTrain) {
    if (isTrainOperationPlanTrainInlineEditing(row)) return false
    return selectedTrainOperationPlanTrainId.value === row.id
}

function isTrainOperationPlanMovementEditing(row: TrainOperationPlanMovement) {
    return !row.isDraft && trainOperationPlanMovementEditingKey.value === getTrainOperationPlanMovementIdentityKey(row)
}

function isTrainOperationPlanMovementInlineEditing(row: TrainOperationPlanMovement) {
    return Boolean(row.isDraft) || isTrainOperationPlanMovementEditing(row)
}

function syncTemplateScope() {
    trainTemplateForm.value.instanceID = props.selectedInstanceId
    trainTemplateForm.value.stationSchemeID = currentStationSchemeId.value.trim()
    trainTemplateForm.value.operationPlanID = getCurrentOperationPlanID()
    movementTemplateForm.value.instanceID = props.selectedInstanceId
    movementTemplateForm.value.stationSchemeID = currentStationSchemeId.value.trim()
    movementTemplateForm.value.operationPlanID = getCurrentOperationPlanID()
    movementTemplateForm.value.trainTemplateID = selectedTrainTemplate.value?.trainTemplateID || ''
}

function syncTrainOperationPlanScope() {
    trainOperationPlanTrainForm.value.instanceID = props.selectedInstanceId
    trainOperationPlanTrainForm.value.stationSchemeID = currentStationSchemeId.value.trim()
    trainOperationPlanTrainForm.value.operationPlanID = getCurrentOperationPlanID()
    trainOperationPlanMovementForm.value.instanceID = props.selectedInstanceId
    trainOperationPlanMovementForm.value.stationSchemeID = currentStationSchemeId.value.trim()
    trainOperationPlanMovementForm.value.operationPlanID = getCurrentOperationPlanID()
}

function clearOperationPlans() {
    operationPlanObjectLoadVersion++
    currentOperationPlanId.value = ''
    operationPlanOptions.value = []
    operationPlanObjectMode.value = 'create'
    operationPlanObjectOriginalId.value = ''
    operationPlanObjectForm.value = createEmptyOperationPlanObject()
    loadingOperationPlans.value = false
    savingOperationPlanObject.value = false
}

function clearTrainTemplates() {
    trainTemplateLoadVersion++
    movementTemplateLoadVersion++
    trainOperationPlanLoadVersion++
    trainTemplateCreating.value = false
    movementTemplateCreating.value = false
    trainTemplateEditingId.value = ''
    movementTemplateEditingId.value = ''
    trainTemplates.value = []
    selectedTrainTemplateId.value = ''
    movementTemplates.value = []
    routePickerVisible.value = false
    routePickerTarget.value = 'movementTemplate'
    routePickerSelectedIds.value = []
    routePickerPreviewRouteId.value = ''
    trainOperationPlanTrainCreating.value = false
    trainOperationPlanMovementCreating.value = false
    trainOperationPlanTrainEditingId.value = ''
    trainOperationPlanMovementEditingKey.value = ''
    trainOperationPlanTrainForm.value = createEmptyTrainOperationPlanTrain()
    trainOperationPlanMovementForm.value = createEmptyTrainOperationPlanMovement()
    trainOperationPlanMovementRouteIds.value = []
    trainOperationPlanTrains.value = []
    trainOperationPlanMovements.value = []
    selectedTrainOperationPlanTrainId.value = ''
    loadingTrainOperationPlan.value = false
    loadingOperationPlanChart.value = false
    generatingTrainOperationPlan.value = false
    clearOperationPlanChart()
    clearRoutePickerLayoutPreview()
}

async function loadOperationPlans() {
    const instanceID = props.selectedInstanceId
    const stationSchemeID = currentStationSchemeId.value.trim()
    if (!instanceID || !stationSchemeID) {
        clearOperationPlans()
        clearTrainTemplates()
        return
    }

    const loadVersion = ++operationPlanObjectLoadVersion
    const previousOperationPlanID = currentOperationPlanId.value
    loadingOperationPlans.value = true
    try {
        const response = await axios.get('/OperationPlan/GetOperationPlans', {
            params: { instanceID, stationSchemeID },
        })
        if (
            loadVersion !== operationPlanObjectLoadVersion ||
            instanceID !== props.selectedInstanceId ||
            stationSchemeID !== currentStationSchemeId.value.trim()
        ) {
            return
        }

        operationPlanOptions.value = (Array.isArray(response.data) ? response.data : [])
            .map(normalizeOperationPlanObject)
            .filter((item): item is StationOperationPlan => item !== null)
        if (operationPlanOptions.value.some((item) => item.operationPlanID === previousOperationPlanID)) {
            currentOperationPlanId.value = previousOperationPlanID
        } else {
            currentOperationPlanId.value = operationPlanOptions.value[0]?.operationPlanID || ''
        }
    } catch (error) {
        if (
            loadVersion !== operationPlanObjectLoadVersion ||
            instanceID !== props.selectedInstanceId ||
            stationSchemeID !== currentStationSchemeId.value.trim()
        ) {
            return
        }

        console.error('Failed to load operation plan objects:', error)
        clearOperationPlans()
        ElMessage.error(t('operationPlan.planObject.messages.loadFailed'))
    } finally {
        if (loadVersion === operationPlanObjectLoadVersion) {
            loadingOperationPlans.value = false
        }
    }
}

function openOperationPlanManager() {
    operationPlanManagerVisible.value = true
    operationPlanObjectMode.value = 'create'
    operationPlanObjectOriginalId.value = ''
    operationPlanObjectForm.value = createEmptyOperationPlanObject()
    void loadOperationPlans()
}

function getOperationPlanObjectRowKey(row: StationOperationPlan) {
    return row.isDraft ? '__operation_plan_draft__' : row.operationPlanID
}

function isOperationPlanObjectEditing(row: StationOperationPlan) {
    return !row.isDraft && operationPlanObjectOriginalId.value === row.operationPlanID
}

function isOperationPlanObjectInlineEditing(row: StationOperationPlan) {
    return Boolean(row.isDraft) || isOperationPlanObjectEditing(row)
}

function startCreateOperationPlanObjectInline() {
    const instanceID = props.selectedInstanceId
    const stationSchemeID = currentStationSchemeId.value.trim()
    if (!instanceID || !stationSchemeID || operationPlanObjectInlineActive.value) return

    const sortOrders = operationPlanOptions.value
        .map((item) => Number(item.sortOrder))
        .filter((value) => Number.isFinite(value))
    const nextSortOrder = sortOrders.length > 0 ? Math.max(...sortOrders) + 1 : operationPlanOptions.value.length
    const draft: StationOperationPlan = {
        ...createEmptyOperationPlanObject(),
        instanceID,
        stationSchemeID,
        sortOrder: nextSortOrder,
        isDraft: true,
    }
    operationPlanObjectMode.value = 'create'
    operationPlanObjectOriginalId.value = ''
    operationPlanObjectForm.value = { ...draft }
    operationPlanOptions.value = [draft, ...operationPlanOptions.value.filter((item) => !item.isDraft)]
}

function cancelOperationPlanObjectEdit() {
    operationPlanOptions.value = operationPlanOptions.value.filter((item) => !item.isDraft)
    operationPlanObjectMode.value = 'create'
    operationPlanObjectOriginalId.value = ''
    operationPlanObjectForm.value = createEmptyOperationPlanObject()
}

function startEditOperationPlanObject(row: StationOperationPlan) {
    if (row.isDraft || operationPlanObjectInlineActive.value) return
    operationPlanObjectMode.value = 'edit'
    operationPlanObjectOriginalId.value = row.operationPlanID
    operationPlanObjectForm.value = { ...row }
}

async function saveOperationPlanObject() {
    const instanceID = props.selectedInstanceId
    const stationSchemeID = currentStationSchemeId.value.trim()
    if (!instanceID || !stationSchemeID) {
        ElMessage.warning(t('operationPlan.empty.selectScheme'))
        return
    }

    const form = operationPlanObjectForm.value
    const name = form.name.trim()
    if (!name) {
        ElMessage.warning(t('operationPlan.planObject.messages.nameRequired'))
        return
    }

    savingOperationPlanObject.value = true
    try {
        const payload = {
            instanceID,
            stationSchemeID,
            originalOperationPlanID: operationPlanObjectOriginalId.value,
            operationPlanID: form.operationPlanID.trim(),
            name,
            description: form.description.trim(),
            sortOrder: form.sortOrder,
        }
        const response = operationPlanObjectMode.value === 'create'
            ? await axios.post('/OperationPlan/CreateOperationPlan', payload)
            : await axios.put('/OperationPlan/EditOperationPlan', payload)
        const saved = normalizeOperationPlanObject(response.data)
        ElMessage.success(t(operationPlanObjectMode.value === 'create'
            ? 'operationPlan.planObject.messages.createSuccess'
            : 'operationPlan.planObject.messages.updateSuccess'))
        operationPlanObjectMode.value = 'create'
        operationPlanObjectOriginalId.value = ''
        operationPlanObjectForm.value = createEmptyOperationPlanObject()
        await loadOperationPlans()
        if (saved?.operationPlanID) {
            currentOperationPlanId.value = saved.operationPlanID
            await refreshOperationPlanData()
        }
    } catch (error) {
        console.error('Failed to save operation plan object:', error)
        ElMessage.error(t(operationPlanObjectMode.value === 'create'
            ? 'operationPlan.planObject.messages.createFailed'
            : 'operationPlan.planObject.messages.updateFailed'))
    } finally {
        savingOperationPlanObject.value = false
    }
}

function confirmDeleteOperationPlanObject(row: StationOperationPlan) {
    if (row.operationPlanID === defaultOperationPlanID) return
    ElMessageBox.confirm(
        t('operationPlan.planObject.messages.deleteConfirm', { name: row.name }),
        t('operationPlan.planObject.dialogs.deleteTitle'),
        {
            confirmButtonText: t('operationPlan.actions.delete'),
            cancelButtonText: t('operationPlan.actions.cancel'),
            type: 'warning',
        },
    )
        .then(() => deleteOperationPlanObject(row))
        .catch(() => {
            return
        })
}

async function copyOperationPlanObject(row: StationOperationPlan) {
    const instanceID = props.selectedInstanceId
    const stationSchemeID = currentStationSchemeId.value.trim()
    if (!instanceID || !stationSchemeID || row.isDraft || operationPlanObjectInlineActive.value) return

    savingOperationPlanObject.value = true
    try {
        const response = await axios.post('/OperationPlan/CopyOperationPlan', {
            instanceID,
            stationSchemeID,
            sourceOperationPlanID: row.operationPlanID,
        })
        const copied = normalizeOperationPlanObject(response.data)
        ElMessage.success(t('operationPlan.planObject.messages.copySuccess'))
        await loadOperationPlans()
        if (copied?.operationPlanID) {
            currentOperationPlanId.value = copied.operationPlanID
            await refreshOperationPlanData()
        }
    } catch (error) {
        console.error('Failed to copy operation plan object:', error)
        ElMessage.error(t('operationPlan.planObject.messages.copyFailed'))
    } finally {
        savingOperationPlanObject.value = false
    }
}

async function deleteOperationPlanObject(row: StationOperationPlan) {
    const instanceID = props.selectedInstanceId
    const stationSchemeID = currentStationSchemeId.value.trim()
    if (!instanceID || !stationSchemeID) return

    savingOperationPlanObject.value = true
    try {
        await axios.delete('/OperationPlan/DeleteOperationPlan', {
            params: {
                instanceID,
                stationSchemeID,
                operationPlanID: row.operationPlanID,
            },
        })
        ElMessage.success(t('operationPlan.planObject.messages.deleteSuccess'))
        const deletedCurrent = currentOperationPlanId.value === row.operationPlanID
        await loadOperationPlans()
        if (deletedCurrent) {
            await refreshOperationPlanData()
        }
        cancelOperationPlanObjectEdit()
    } catch (error) {
        console.error('Failed to delete operation plan object:', error)
        ElMessage.error(t('operationPlan.planObject.messages.deleteFailed'))
    } finally {
        savingOperationPlanObject.value = false
    }
}

function clearMovementTemplates() {
    movementTemplateLoadVersion++
    movementTemplateCreating.value = false
    movementTemplateEditingId.value = ''
    movementTemplates.value = []
    routePickerVisible.value = false
    routePickerTarget.value = 'movementTemplate'
    routePickerSelectedIds.value = []
    routePickerPreviewRouteId.value = ''
}

function clearTrainOperationPlan() {
    trainOperationPlanLoadVersion++
    operationPlanChartLoadVersion++
    trainOperationPlanTrainCreating.value = false
    trainOperationPlanMovementCreating.value = false
    trainOperationPlanTrainEditingId.value = ''
    trainOperationPlanMovementEditingKey.value = ''
    trainOperationPlanTrainForm.value = createEmptyTrainOperationPlanTrain()
    trainOperationPlanMovementForm.value = createEmptyTrainOperationPlanMovement()
    trainOperationPlanMovementRouteIds.value = []
    trainOperationPlanTrains.value = []
    trainOperationPlanMovements.value = []
    selectedTrainOperationPlanTrainId.value = ''
    loadingTrainOperationPlan.value = false
    loadingOperationPlanChart.value = false
    generatingTrainOperationPlan.value = false
    clearOperationPlanChart()
}

function clearOperationPlanChart() {
    operationPlanChartLoadVersion++
    stationRouteEndLoadVersion++
    if (operationBottleneckSummaryCategorySaveTimer) {
        window.clearTimeout(operationBottleneckSummaryCategorySaveTimer)
        operationBottleneckSummaryCategorySaveTimer = null
    }
    stationLayoutCells.value = []
    stationRouteTimesByKey.value = {}
    stationRouteEndOptions.value = []
    operationBottleneckSummaryCategories.value = []
    clearOperationOccupationTimeSubTableState()
    clearOperationAnalysisSnapshotState()
    closeOperationBottleneckRoutePicker()
    clearOperationBottleneckRoutePickerFilters()
    loadingOperationPlanChart.value = false
    loadingStationRouteEnds.value = false
    loadingOperationBottleneckSummaryCategories.value = false
    savingOperationBottleneckSummaryCategories.value = false
}

async function loadStationSchemes() {
    const instanceID = props.selectedInstanceId
    if (!instanceID) {
        stationSchemeLoadVersion++
        currentStationSchemeId.value = ''
        stationSchemeOptions.value = []
        clearOperationPlans()
        clearTrainTemplates()
        clearTrainOperationPlan()
        return
    }

    const loadVersion = ++stationSchemeLoadVersion
    loadingStationSchemes.value = true
    try {
        const response = await axios.get('/StationLayout/GetStationSchemes', { params: { instanceID } })
        if (loadVersion !== stationSchemeLoadVersion || instanceID !== props.selectedInstanceId) return

        stationSchemeOptions.value = (Array.isArray(response.data) ? response.data : [])
            .map(normalizeStationSchemeOption)
            .filter((item): item is StationSchemeOption => item !== null)
        if (!stationSchemeOptions.value.some((item) => item.id === currentStationSchemeId.value)) {
            currentStationSchemeId.value = stationSchemeOptions.value[0]?.id || ''
        }
        await loadOperationPlans()
    } catch (error) {
        if (loadVersion !== stationSchemeLoadVersion || instanceID !== props.selectedInstanceId) return
        console.error('Failed to load operation plan station schemes:', error)
        stationSchemeOptions.value = []
        currentStationSchemeId.value = ''
        clearOperationPlans()
        ElMessage.error(t('stationLayout.messages.loadSchemesFailed'))
    } finally {
        if (loadVersion === stationSchemeLoadVersion && instanceID === props.selectedInstanceId) {
            loadingStationSchemes.value = false
        }
    }
}

async function loadStationRoutes() {
    const instanceID = props.selectedInstanceId
    const stationSchemeID = currentStationSchemeId.value.trim()
    if (!instanceID || !stationSchemeID) {
        stationRouteLoadVersion++
        stationRouteOptions.value = []
        return
    }

    const loadVersion = ++stationRouteLoadVersion
    loadingStationRoutes.value = true
    try {
        const response = await axios.get('/StationLayout/GetStationRoutes', {
            params: { instanceID, stationSchemeID },
        })
        if (
            loadVersion !== stationRouteLoadVersion ||
            instanceID !== props.selectedInstanceId ||
            stationSchemeID !== currentStationSchemeId.value.trim()
        ) {
            return
        }

        stationRouteOptions.value = (Array.isArray(response.data) ? response.data : [])
            .map(normalizeStationRouteOption)
            .filter((item): item is StationRouteOption => item !== null)
    } catch (error) {
        if (
            loadVersion !== stationRouteLoadVersion ||
            instanceID !== props.selectedInstanceId ||
            stationSchemeID !== currentStationSchemeId.value.trim()
        ) {
            return
        }

        console.error('Failed to load operation plan station routes:', error)
        stationRouteOptions.value = []
    } finally {
        if (loadVersion === stationRouteLoadVersion) {
            loadingStationRoutes.value = false
        }
    }
}

async function loadStationRouteEnds() {
    const instanceID = props.selectedInstanceId
    const stationSchemeID = currentStationSchemeId.value.trim()
    if (!instanceID || !stationSchemeID) {
        stationRouteEndLoadVersion++
        stationRouteEndOptions.value = []
        return
    }

    const loadVersion = ++stationRouteEndLoadVersion
    loadingStationRouteEnds.value = true
    try {
        const response = await axios.get('/StationLayout/GetStationRouteEnds', {
            params: { instanceID, stationSchemeID },
        })
        if (
            loadVersion !== stationRouteEndLoadVersion ||
            instanceID !== props.selectedInstanceId ||
            stationSchemeID !== currentStationSchemeId.value.trim()
        ) {
            return
        }

        stationRouteEndOptions.value = (Array.isArray(response.data) ? response.data : [])
            .map(normalizeStationRouteEndOption)
            .filter((item): item is StationRouteEndOption => item !== null)
    } catch (error) {
        if (
            loadVersion !== stationRouteEndLoadVersion ||
            instanceID !== props.selectedInstanceId ||
            stationSchemeID !== currentStationSchemeId.value.trim()
        ) {
            return
        }

        console.error('Failed to load operation plan station route ends:', error)
        stationRouteEndOptions.value = []
    } finally {
        if (loadVersion === stationRouteEndLoadVersion) {
            loadingStationRouteEnds.value = false
        }
    }
}

async function loadTrainTemplates() {
    const { instanceID, stationSchemeID, operationPlanID } = getOperationPlanScope()
    if (!instanceID || !stationSchemeID || !operationPlanID) {
        clearTrainTemplates()
        return
    }

    const loadVersion = ++trainTemplateLoadVersion
    const previousSelectedId = selectedTrainTemplateId.value
    loadingTrainTemplates.value = true
    try {
        const response = await axios.get('/OperationPlan/GetTrainTemplates', {
            params: { instanceID, stationSchemeID, operationPlanID },
        })
        if (
            loadVersion !== trainTemplateLoadVersion ||
            instanceID !== props.selectedInstanceId ||
            stationSchemeID !== currentStationSchemeId.value.trim() ||
            operationPlanID !== getCurrentOperationPlanID()
        ) {
            return
        }

        trainTemplates.value = (Array.isArray(response.data) ? response.data : [])
            .map(normalizeTrainTemplate)
            .filter((item): item is TrainTemplate => item !== null)
        if (trainTemplates.value.some((item) => item.trainTemplateID === previousSelectedId)) {
            selectedTrainTemplateId.value = previousSelectedId
            await loadMovementTemplates()
        } else {
            selectedTrainTemplateId.value = ''
            clearMovementTemplates()
        }
    } catch (error) {
        if (
            loadVersion !== trainTemplateLoadVersion ||
            instanceID !== props.selectedInstanceId ||
            stationSchemeID !== currentStationSchemeId.value.trim() ||
            operationPlanID !== getCurrentOperationPlanID()
        ) {
            return
        }

        console.error('Failed to load train templates:', error)
        clearTrainTemplates()
        ElMessage.error(t('operationPlan.train.messages.loadFailed'))
    } finally {
        if (loadVersion === trainTemplateLoadVersion) {
            loadingTrainTemplates.value = false
        }
    }
}

async function loadMovementTemplates() {
    const { instanceID, stationSchemeID, operationPlanID } = getOperationPlanScope()
    const trainTemplateID = selectedTrainTemplate.value?.trainTemplateID || ''
    if (!instanceID || !stationSchemeID || !operationPlanID || !trainTemplateID) {
        clearMovementTemplates()
        return
    }

    const loadVersion = ++movementTemplateLoadVersion
    loadingMovementTemplates.value = true
    try {
        const response = await axios.get('/OperationPlan/GetMovementTemplates', {
            params: { instanceID, stationSchemeID, operationPlanID, trainTemplateID },
        })
        if (
            loadVersion !== movementTemplateLoadVersion ||
            instanceID !== props.selectedInstanceId ||
            stationSchemeID !== currentStationSchemeId.value.trim() ||
            operationPlanID !== getCurrentOperationPlanID() ||
            trainTemplateID !== selectedTrainTemplate.value?.trainTemplateID
        ) {
            return
        }

        movementTemplates.value = (Array.isArray(response.data) ? response.data : [])
            .map(normalizeMovementTemplate)
            .filter((item): item is MovementTemplate => item !== null)
    } catch (error) {
        if (
            loadVersion !== movementTemplateLoadVersion ||
            instanceID !== props.selectedInstanceId ||
            stationSchemeID !== currentStationSchemeId.value.trim() ||
            operationPlanID !== getCurrentOperationPlanID() ||
            trainTemplateID !== selectedTrainTemplate.value?.trainTemplateID
        ) {
            return
        }

        console.error('Failed to load movement templates:', error)
        movementTemplates.value = []
        ElMessage.error(t('operationPlan.movement.messages.loadFailed'))
    } finally {
        if (loadVersion === movementTemplateLoadVersion) {
            loadingMovementTemplates.value = false
        }
    }
}

async function loadTrainOperationPlan() {
    const { instanceID, stationSchemeID, operationPlanID } = getOperationPlanScope()
    if (!instanceID || !stationSchemeID || !operationPlanID) {
        clearTrainOperationPlan()
        return
    }

    const loadVersion = ++trainOperationPlanLoadVersion
    loadingTrainOperationPlan.value = true
    try {
        const response = await axios.get('/OperationPlan/GetTrainOperationPlan', {
            params: { instanceID, stationSchemeID, operationPlanID },
        })
        if (
            loadVersion !== trainOperationPlanLoadVersion ||
            instanceID !== props.selectedInstanceId ||
            stationSchemeID !== currentStationSchemeId.value.trim() ||
            operationPlanID !== getCurrentOperationPlanID()
        ) {
            return
        }

        normalizeTrainOperationPlanResponse(response.data)
        if (isOperationPlanChartDataTab(activeOperationPlanTab.value)) {
            await loadOperationPlanChartData()
        }
    } catch (error) {
        if (
            loadVersion !== trainOperationPlanLoadVersion ||
            instanceID !== props.selectedInstanceId ||
            stationSchemeID !== currentStationSchemeId.value.trim() ||
            operationPlanID !== getCurrentOperationPlanID()
        ) {
            return
        }

        console.error('Failed to load train operation plan:', error)
        trainOperationPlanTrains.value = []
        trainOperationPlanMovements.value = []
        if (isOperationPlanChartDataTab(activeOperationPlanTab.value)) {
            const fallbackVersion = ++operationPlanChartLoadVersion
            loadingOperationPlanChart.value = true
            clearOperationAnalysisSnapshotState()
            const fallbackLoaded = await loadOperationAnalysisSnapshotFallback(instanceID, stationSchemeID, fallbackVersion)
            if (!fallbackLoaded) {
                stationLayoutCells.value = []
                stationRouteTimesByKey.value = {}
                stationRouteEndOptions.value = []
                operationBottleneckSummaryCategories.value = []
            }
            loadingOperationPlanChart.value = false
        }
        ElMessage.error(t('operationPlan.trainOperationPlan.messages.loadFailed'))
    } finally {
        if (loadVersion === trainOperationPlanLoadVersion) {
            loadingTrainOperationPlan.value = false
        }
    }
}

function getOperationPlanChartRouteTimePairs() {
    const pairs = new Map<string, { routeID: string; trainTypeID: string }>()
    trainOperationPlanMovements.value.forEach((movement) => {
        const routeID = movement.route.trim()
        if (!routeID) return

        const trainTypeID = trainOperationPlanTrainMap.value.get(movement.trainID)?.trainType?.trim() || ''
        const defaultKey = getOperationPlanChartRouteTimeKey(routeID, '')
        pairs.set(defaultKey, { routeID, trainTypeID: '' })
        if (trainTypeID) {
            const specificKey = getOperationPlanChartRouteTimeKey(routeID, trainTypeID)
            pairs.set(specificKey, { routeID, trainTypeID })
        }
    })
    return Array.from(pairs.values())
}

async function loadOperationPlanChartCells(
    instanceID: string,
    stationSchemeID: string,
    loadVersion: number,
) {
    const response = await axios.post('/StationLayout/GetJson', null, {
        params: { instanceID, stationSchemeID },
    })
    if (
        loadVersion !== operationPlanChartLoadVersion ||
        instanceID !== props.selectedInstanceId ||
        stationSchemeID !== currentStationSchemeId.value.trim()
    ) {
        return
    }

    stationLayoutCells.value = getLayoutCells(response.data)
        .map((cell: OperationPlanChartCell) => ({ id: cell.id, name: cell.name || cell.id }))
        .filter((cell: OperationPlanChartCell) => cell.id)
}

async function loadOperationPlanChartRouteTimes(
    instanceID: string,
    stationSchemeID: string,
    loadVersion: number,
) {
    const pairs = getOperationPlanChartRouteTimePairs()
    if (pairs.length === 0) {
        if (loadVersion === operationPlanChartLoadVersion) {
            stationRouteTimesByKey.value = {}
        }
        return
    }

    const entries = await Promise.all(pairs.map(async (pair) => {
        const response = await axios.get('/StationLayout/GetStationRouteTimes', {
            params: {
                instanceID,
                stationSchemeID,
                routeID: pair.routeID,
                trainTypeID: pair.trainTypeID,
            },
        })
        const rows = (Array.isArray(response.data) ? response.data : [])
            .map(normalizeStationRouteTimeOption)
            .filter((item): item is StationRouteTimeOption => item !== null)
            .map((time) => ({
                ...time,
                routeID: time.routeID || pair.routeID,
                trainTypeID: time.trainTypeID || pair.trainTypeID,
            }))
        return [getOperationPlanChartRouteTimeKey(pair.routeID, pair.trainTypeID), rows] as const
    }))

    if (
        loadVersion !== operationPlanChartLoadVersion ||
        instanceID !== props.selectedInstanceId ||
        stationSchemeID !== currentStationSchemeId.value.trim()
    ) {
        return
    }

    stationRouteTimesByKey.value = Object.fromEntries(entries)
}

async function loadOperationBottleneckSummaryCategories(instanceID: string, stationSchemeID: string, loadVersion = operationPlanChartLoadVersion) {
    const operationPlanID = getCurrentOperationPlanID()
    if (!operationPlanID) {
        operationBottleneckSummaryCategories.value = []
        return
    }

    loadingOperationBottleneckSummaryCategories.value = true
    try {
        const response = await axios.get('/OperationPlan/GetBottleneckSummaryCategories', {
            params: {
                instanceID,
                stationSchemeID,
                operationPlanID,
            },
        })
        if (
            loadVersion !== operationPlanChartLoadVersion ||
            instanceID !== props.selectedInstanceId ||
            stationSchemeID !== currentStationSchemeId.value.trim() ||
            operationPlanID !== getCurrentOperationPlanID()
        ) {
            return
        }
        operationBottleneckSummaryCategories.value = normalizeOperationBottleneckSummaryCategoriesResponse(response.data)
    } finally {
        if (loadVersion === operationPlanChartLoadVersion) {
            loadingOperationBottleneckSummaryCategories.value = false
        }
    }
}

async function loadOperationOccupationTimeSubTableSettings(
    instanceID: string,
    stationSchemeID: string,
    loadVersion = operationPlanChartLoadVersion,
) {
    const operationPlanID = getCurrentOperationPlanID()
    if (!operationPlanID) {
        runWithoutOperationOccupationTimeSubTableSave(resetOperationOccupationTimeSubTables)
        return
    }

    loadingOperationOccupationTimeSubTableSettings.value = true
    try {
        const response = await axios.get('/OperationPlan/GetOperationOccupationTimeSubTables', {
            params: {
                instanceID,
                stationSchemeID,
                operationPlanID,
            },
        })
        if (
            loadVersion !== operationPlanChartLoadVersion ||
            instanceID !== props.selectedInstanceId ||
            stationSchemeID !== currentStationSchemeId.value.trim() ||
            operationPlanID !== getCurrentOperationPlanID()
        ) {
            return
        }

        const settings = (Array.isArray(response.data) ? response.data : [])
            .map(normalizeOperationOccupationTimeSubTableSetting)
            .filter((item): item is OperationOccupationTimeSubTable => item !== null)
        if (settings.length > 0) {
            applyOperationOccupationTimeSubTableSettings(settings)
            return
        }

        runWithoutOperationOccupationTimeSubTableSave(() => {
            resetOperationOccupationTimeSubTables()
            syncOperationOccupationTimeSubTables(displayOperationOccupationTimeTableCells.value)
        })
        void nextTick(() => {
            scheduleSaveOperationOccupationTimeSubTableSettings(0)
        })
    } catch (error) {
        if (loadVersion !== operationPlanChartLoadVersion) return
        console.error('Failed to load operation occupation time sub table settings:', error)
        runWithoutOperationOccupationTimeSubTableSave(() => {
            resetOperationOccupationTimeSubTables()
            syncOperationOccupationTimeSubTables(displayOperationOccupationTimeTableCells.value)
        })
    } finally {
        if (loadVersion === operationPlanChartLoadVersion) {
            loadingOperationOccupationTimeSubTableSettings.value = false
        }
    }
}

async function saveOperationOccupationTimeSubTableSettingsNow() {
    if (
        suppressOperationOccupationTimeSubTableSave ||
        loadingOperationOccupationTimeSubTableSettings.value ||
        savingOperationOccupationTimeSubTableSettings.value
    ) {
        return
    }

    const { instanceID, stationSchemeID, operationPlanID } = getOperationPlanScope()
    if (!instanceID || !stationSchemeID || !operationPlanID) return

    const subTables = buildOperationOccupationTimeSubTableSettingsPayload()
    if (subTables.length === 0) return

    const savingRevision = operationOccupationTimeSubTableSaveRevision
    savingOperationOccupationTimeSubTableSettings.value = true
    try {
        const response = await axios.put('/OperationPlan/SaveOperationOccupationTimeSubTables', {
            instanceID,
            stationSchemeID,
            operationPlanID,
            subTables,
        })
        if (
            instanceID !== props.selectedInstanceId ||
            stationSchemeID !== currentStationSchemeId.value.trim() ||
            operationPlanID !== getCurrentOperationPlanID()
        ) {
            return
        }

        const settings = (Array.isArray(response.data) ? response.data : [])
            .map(normalizeOperationOccupationTimeSubTableSetting)
            .filter((item): item is OperationOccupationTimeSubTable => item !== null)
        if (settings.length > 0 && savingRevision === operationOccupationTimeSubTableSaveRevision) {
            applyOperationOccupationTimeSubTableSettings(settings)
        }
    } catch (error) {
        console.error('Failed to save operation occupation time sub table settings:', error)
    } finally {
        savingOperationOccupationTimeSubTableSettings.value = false
        if (savingRevision !== operationOccupationTimeSubTableSaveRevision) {
            scheduleSaveOperationOccupationTimeSubTableSettings(0)
        }
    }
}

function scheduleSaveOperationOccupationTimeSubTableSettings(delay = 500) {
    if (suppressOperationOccupationTimeSubTableSave || loadingOperationOccupationTimeSubTableSettings.value) return

    if (operationOccupationTimeSubTableSaveTimer) {
        window.clearTimeout(operationOccupationTimeSubTableSaveTimer)
    }
    operationOccupationTimeSubTableSaveTimer = window.setTimeout(() => {
        operationOccupationTimeSubTableSaveTimer = null
        void saveOperationOccupationTimeSubTableSettingsNow()
    }, delay)
}

async function loadOperationPlanChartData() {
    const { instanceID, stationSchemeID, operationPlanID } = getOperationPlanScope()
    if (!instanceID || !stationSchemeID || !operationPlanID) {
        clearOperationPlanChart()
        return
    }

    const loadVersion = ++operationPlanChartLoadVersion
    loadingOperationPlanChart.value = true
    clearOperationAnalysisSnapshotState()
    try {
        if (stationRouteOptions.value.length === 0 && !loadingStationRoutes.value) {
            await loadStationRoutes()
        }
        await Promise.all([
            loadOperationPlanChartCells(instanceID, stationSchemeID, loadVersion),
            loadOperationPlanChartRouteTimes(instanceID, stationSchemeID, loadVersion),
            loadStationRouteEnds(),
            loadOperationBottleneckSummaryCategories(instanceID, stationSchemeID, loadVersion),
        ])
        await loadOperationOccupationTimeSubTableSettings(instanceID, stationSchemeID, loadVersion)
        if (
            loadVersion !== operationPlanChartLoadVersion ||
            instanceID !== props.selectedInstanceId ||
            stationSchemeID !== currentStationSchemeId.value.trim() ||
            operationPlanID !== getCurrentOperationPlanID()
        ) {
            return
        }

        await nextTick()
        if (operationPlanChartBars.value.length === 0) {
            await loadOperationAnalysisSnapshotFallback(instanceID, stationSchemeID, loadVersion)
            return
        }
        scheduleSaveOperationAnalysisSnapshot(0)
    } catch (error) {
        if (loadVersion !== operationPlanChartLoadVersion) return
        console.error('Failed to load operation plan chart:', error)
        const fallbackLoaded = await loadOperationAnalysisSnapshotFallback(instanceID, stationSchemeID, loadVersion)
        if (!fallbackLoaded) {
            stationLayoutCells.value = []
            stationRouteTimesByKey.value = {}
            stationRouteEndOptions.value = []
            operationBottleneckSummaryCategories.value = []
            ElMessage.error(t('operationPlan.trainOperationChart.messages.loadFailed'))
        }
    } finally {
        if (loadVersion === operationPlanChartLoadVersion) {
            loadingOperationPlanChart.value = false
        }
    }
}

async function generateTrainOperationPlan() {
    const { instanceID, stationSchemeID, operationPlanID } = getOperationPlanScope()
    if (!instanceID || !stationSchemeID || !operationPlanID) {
        ElMessage.warning(t('operationPlan.empty.selectScheme'))
        return
    }
    if (operationPlanInlineActive.value) return

    generatingTrainOperationPlan.value = true
    loadingTrainOperationPlan.value = true
    try {
        const response = await axios.post('/OperationPlan/GenerateTrainOperationPlan', {
            instanceID,
            stationSchemeID,
            operationPlanID,
            startTime: trainOperationPlanStartTime.value.trim(),
            endTime: trainOperationPlanEndTime.value.trim(),
        })
        if (
            instanceID !== props.selectedInstanceId ||
            stationSchemeID !== currentStationSchemeId.value.trim() ||
            operationPlanID !== getCurrentOperationPlanID()
        ) {
            return
        }

        normalizeTrainOperationPlanResponse(response.data)
        if (isOperationPlanChartDataTab(activeOperationPlanTab.value)) {
            await loadOperationPlanChartData()
        }
        ElMessage.success(t('operationPlan.trainOperationPlan.messages.generateSuccess'))
    } catch (error) {
        console.error('Failed to generate train operation plan:', error)
        ElMessage.error(t('operationPlan.trainOperationPlan.messages.generateFailed'))
    } finally {
        generatingTrainOperationPlan.value = false
        loadingTrainOperationPlan.value = false
    }
}

function startCreateTrainOperationPlanTrainInline() {
    if (!hasScope.value) {
        ElMessage.warning(t('operationPlan.empty.selectScheme'))
        return
    }
    if (operationPlanInlineActive.value) return

    trainOperationPlanTrainMode.value = 'create'
    trainOperationPlanTrainForm.value = createEmptyTrainOperationPlanTrain()
    syncTrainOperationPlanScope()
    trainOperationPlanTrainCreating.value = true
}

function startEditTrainOperationPlanTrainInline(row: TrainOperationPlanTrain) {
    if (row.isDraft || !canEditTrainOperationPlan.value || operationPlanInlineActive.value) return

    trainOperationPlanTrainMode.value = 'edit'
    trainOperationPlanTrainEditingId.value = row.id
    trainOperationPlanTrainForm.value = { ...row }
    syncTrainOperationPlanScope()
}

function cancelTrainOperationPlanTrainInline(row: TrainOperationPlanTrain) {
    if (row.isDraft) {
        trainOperationPlanTrainCreating.value = false
    } else {
        trainOperationPlanTrainEditingId.value = ''
    }
    trainOperationPlanTrainForm.value = createEmptyTrainOperationPlanTrain()
}

async function saveTrainOperationPlanTrain() {
    syncTrainOperationPlanScope()
    savingTrainOperationPlanTrain.value = true
    try {
        const payload = {
            ...trainOperationPlanTrainForm.value,
            isFixedOperation: trainOperationPlanTrainForm.value.isFixedOperation ? 1 : 0,
        }
        if (trainOperationPlanTrainMode.value === 'create') {
            await axios.post('/OperationPlan/CreateTrain', payload)
        } else {
            await axios.put('/OperationPlan/EditTrain', payload)
        }
        ElMessage.success(t(trainOperationPlanTrainMode.value === 'create'
            ? 'operationPlan.trainOperationPlan.train.messages.createSuccess'
            : 'operationPlan.trainOperationPlan.train.messages.updateSuccess'))
        trainOperationPlanTrainCreating.value = false
        trainOperationPlanTrainEditingId.value = ''
        trainOperationPlanTrainForm.value = createEmptyTrainOperationPlanTrain()
        await loadTrainOperationPlan()
    } catch (error) {
        console.error('Failed to save train:', error)
        ElMessage.error(t(trainOperationPlanTrainMode.value === 'create'
            ? 'operationPlan.trainOperationPlan.train.messages.createFailed'
            : 'operationPlan.trainOperationPlan.train.messages.updateFailed'))
    } finally {
        savingTrainOperationPlanTrain.value = false
    }
}

async function updateTrainOperationPlanTrainFixedOperation(row: TrainOperationPlanTrain, checked: boolean) {
    if (!canEditTrainOperationPlan.value || operationPlanInlineActive.value || row.isDraft) return
    const previousValue = row.isFixedOperation
    row.isFixedOperation = checked
    savingTrainOperationPlanTrain.value = true
    try {
        const response = await axios.put('/OperationPlan/EditTrain', {
            ...row,
            operationPlanID: row.operationPlanID || getCurrentOperationPlanID(),
            isFixedOperation: checked ? 1 : 0,
        })
        const saved = normalizeTrainOperationPlanTrain(response.data)
        if (saved) {
            const index = trainOperationPlanTrains.value.findIndex((item) => item.id === row.id)
            if (index >= 0) trainOperationPlanTrains.value[index] = saved
        }
    } catch (error) {
        row.isFixedOperation = previousValue
        console.error('Failed to update train fixed operation:', error)
        ElMessage.error(t('operationPlan.trainOperationPlan.train.messages.updateFailed'))
    } finally {
        savingTrainOperationPlanTrain.value = false
    }
}

function confirmDeleteTrainOperationPlanTrain(row: TrainOperationPlanTrain) {
    ElMessageBox.confirm(
        t('operationPlan.trainOperationPlan.train.messages.deleteConfirm', { id: row.id }),
        t('operationPlan.trainOperationPlan.train.dialogs.deleteTitle'),
        {
            confirmButtonText: t('operationPlan.actions.delete'),
            cancelButtonText: t('operationPlan.actions.cancel'),
            type: 'warning',
        },
    )
        .then(() => deleteTrainOperationPlanTrain(row))
        .catch(() => {
            return
        })
}

async function deleteTrainOperationPlanTrain(row: TrainOperationPlanTrain) {
    loadingTrainOperationPlan.value = true
    try {
        await axios.delete('/OperationPlan/DeleteTrain', {
            params: {
                instanceID: props.selectedInstanceId,
                stationSchemeID: currentStationSchemeId.value.trim(),
                operationPlanID: getCurrentOperationPlanID(),
                id: row.id,
            },
        })
        ElMessage.success(t('operationPlan.trainOperationPlan.train.messages.deleteSuccess'))
        await loadTrainOperationPlan()
    } catch (error) {
        console.error('Failed to delete train:', error)
        ElMessage.error(t('operationPlan.trainOperationPlan.train.messages.deleteFailed'))
    } finally {
        loadingTrainOperationPlan.value = false
    }
}

function startCreateTrainOperationPlanMovementInline() {
    if (!hasScope.value) {
        ElMessage.warning(t('operationPlan.empty.selectScheme'))
        return
    }
    if (!selectedTrainOperationPlanTrain.value) {
        ElMessage.warning(t('operationPlan.trainOperationPlan.movement.messages.trainRequired'))
        return
    }
    if (operationPlanInlineActive.value) return

    trainOperationPlanMovementMode.value = 'create'
    trainOperationPlanMovementForm.value = createEmptyTrainOperationPlanMovement()
    trainOperationPlanMovementForm.value.trainID = selectedTrainOperationPlanTrain.value.id
    trainOperationPlanMovementForm.value.trainTemplateID = selectedTrainOperationPlanTrain.value.trainTemplateID
    trainOperationPlanMovementForm.value.sortOrder = selectedTrainOperationPlanMovements.value.length
    trainOperationPlanMovementRouteIds.value = []
    syncTrainOperationPlanScope()
    trainOperationPlanMovementCreating.value = true
}

function startEditTrainOperationPlanMovementInline(row: TrainOperationPlanMovement) {
    if (row.isDraft || !canEditTrainOperationPlan.value || operationPlanInlineActive.value) return

    trainOperationPlanMovementMode.value = 'edit'
    trainOperationPlanMovementEditingKey.value = getTrainOperationPlanMovementIdentityKey(row)
    trainOperationPlanMovementForm.value = { ...row }
    trainOperationPlanMovementRouteIds.value = parseRouteIDList(row.routeIDList)
    syncTrainOperationPlanScope()
}

function cancelTrainOperationPlanMovementInline(row: TrainOperationPlanMovement) {
    if (row.isDraft) {
        trainOperationPlanMovementCreating.value = false
    } else {
        trainOperationPlanMovementEditingKey.value = ''
    }
    trainOperationPlanMovementForm.value = createEmptyTrainOperationPlanMovement()
    trainOperationPlanMovementRouteIds.value = []
}

async function saveTrainOperationPlanMovement() {
    syncTrainOperationPlanScope()
    if (!trainOperationPlanMovementForm.value.trainID.trim()) {
        ElMessage.warning(t('operationPlan.trainOperationPlan.movement.messages.trainRequired'))
        return
    }

    savingTrainOperationPlanMovement.value = true
    try {
        trainOperationPlanMovementForm.value.routeIDList = serializeRouteIDList(trainOperationPlanMovementRouteIds.value)
        const payload = { ...trainOperationPlanMovementForm.value }
        if (trainOperationPlanMovementMode.value === 'create') {
            await axios.post('/OperationPlan/CreateMovement', payload)
        } else {
            await axios.put('/OperationPlan/EditMovement', payload)
        }
        ElMessage.success(t(trainOperationPlanMovementMode.value === 'create'
            ? 'operationPlan.trainOperationPlan.movement.messages.createSuccess'
            : 'operationPlan.trainOperationPlan.movement.messages.updateSuccess'))
        trainOperationPlanMovementCreating.value = false
        trainOperationPlanMovementEditingKey.value = ''
        trainOperationPlanMovementForm.value = createEmptyTrainOperationPlanMovement()
        trainOperationPlanMovementRouteIds.value = []
        await loadTrainOperationPlan()
    } catch (error) {
        console.error('Failed to save movement:', error)
        ElMessage.error(t(trainOperationPlanMovementMode.value === 'create'
            ? 'operationPlan.trainOperationPlan.movement.messages.createFailed'
            : 'operationPlan.trainOperationPlan.movement.messages.updateFailed'))
    } finally {
        savingTrainOperationPlanMovement.value = false
    }
}

function confirmDeleteTrainOperationPlanMovement(row: TrainOperationPlanMovement) {
    ElMessageBox.confirm(
        t('operationPlan.trainOperationPlan.movement.messages.deleteConfirm', { id: row.movementID }),
        t('operationPlan.trainOperationPlan.movement.dialogs.deleteTitle'),
        {
            confirmButtonText: t('operationPlan.actions.delete'),
            cancelButtonText: t('operationPlan.actions.cancel'),
            type: 'warning',
        },
    )
        .then(() => deleteTrainOperationPlanMovement(row))
        .catch(() => {
            return
        })
}

async function deleteTrainOperationPlanMovement(row: TrainOperationPlanMovement) {
    loadingTrainOperationPlan.value = true
    try {
        await axios.delete('/OperationPlan/DeleteMovement', {
            params: {
                instanceID: props.selectedInstanceId,
                stationSchemeID: currentStationSchemeId.value.trim(),
                operationPlanID: getCurrentOperationPlanID(),
                trainID: row.trainID,
                movementID: row.movementID,
            },
        })
        ElMessage.success(t('operationPlan.trainOperationPlan.movement.messages.deleteSuccess'))
        await loadTrainOperationPlan()
    } catch (error) {
        console.error('Failed to delete movement:', error)
        ElMessage.error(t('operationPlan.trainOperationPlan.movement.messages.deleteFailed'))
    } finally {
        loadingTrainOperationPlan.value = false
    }
}

async function refreshOperationPlanData() {
    if (!hasScope.value) {
        clearTrainTemplates()
        clearTrainOperationPlan()
        return
    }

    await Promise.all([loadTrainTemplates(), loadStationRoutes(), loadTrainOperationPlan()])
}

async function handleStationSchemeChange() {
    currentOperationPlanId.value = ''
    selectedTrainTemplateId.value = ''
    clearMovementTemplates()
    clearTrainOperationPlan()
    await loadOperationPlans()
    await refreshOperationPlanData()
}

async function handleOperationPlanChange() {
    selectedTrainTemplateId.value = ''
    clearMovementTemplates()
    clearTrainOperationPlan()
    clearOperationPlanChart()
    await refreshOperationPlanData()
}

async function toggleTrainTemplateExpansion(row: TrainTemplate) {
    if (operationPlanInlineActive.value || isTrainTemplateInlineEditing(row)) return

    if (selectedTrainTemplateId.value === row.trainTemplateID) {
        selectedTrainTemplateId.value = ''
        clearMovementTemplates()
        return
    }

    selectedTrainTemplateId.value = row.trainTemplateID
    await loadMovementTemplates()
}

function toggleTrainOperationPlanTrainExpansion(row: TrainOperationPlanTrain) {
    if (operationPlanInlineActive.value || isTrainOperationPlanTrainInlineEditing(row)) return

    selectedTrainOperationPlanTrainId.value = selectedTrainOperationPlanTrainId.value === row.id
        ? ''
        : row.id
}

function startCreateTrainTemplateInline() {
    if (!hasScope.value) {
        ElMessage.warning(t('operationPlan.empty.selectScheme'))
        return
    }
    if (operationPlanInlineActive.value) return

    movementTemplateCreating.value = false
    movementTemplateEditingId.value = ''
    trainTemplateEditingId.value = ''
    trainTemplateMode.value = 'create'
    trainTemplateOriginalId.value = ''
    trainTemplateForm.value = createEmptyTrainTemplate()
    syncTemplateScope()
    trainTemplateCreating.value = true
}

function cancelCreateTrainTemplateInline() {
    trainTemplateCreating.value = false
    trainTemplateOriginalId.value = ''
    trainTemplateForm.value = createEmptyTrainTemplate()
}

function cancelEditTrainTemplateInline() {
    trainTemplateEditingId.value = ''
    trainTemplateOriginalId.value = ''
    trainTemplateForm.value = createEmptyTrainTemplate()
}

function cancelTrainTemplateInline(row: TrainTemplate) {
    if (row.isDraft) {
        cancelCreateTrainTemplateInline()
        return
    }

    cancelEditTrainTemplateInline()
}

function startEditTrainTemplateInline(row: TrainTemplate) {
    if (row.isDraft || !canEditTrainTemplates.value || operationPlanInlineActive.value) return

    movementTemplateCreating.value = false
    movementTemplateEditingId.value = ''
    trainTemplateCreating.value = false
    trainTemplateMode.value = 'edit'
    trainTemplateEditingId.value = row.trainTemplateID
    trainTemplateOriginalId.value = row.trainTemplateID
    trainTemplateForm.value = { ...row }
    syncTemplateScope()
}

async function saveTrainTemplate() {
    syncTemplateScope()
    const form = trainTemplateForm.value
    if (!form.name.trim()) {
        ElMessage.warning(t('operationPlan.train.messages.nameRequired'))
        return
    }

    savingTrainTemplate.value = true
    try {
        const payload = {
            instanceID: form.instanceID,
            stationSchemeID: form.stationSchemeID,
            operationPlanID: form.operationPlanID,
            originalTrainTemplateID: trainTemplateOriginalId.value,
            trainTemplateID: form.trainTemplateID.trim(),
            name: form.name.trim(),
            type: form.type.trim(),
            number: form.number,
            isFixedOperation: form.isFixedOperation ? 1 : 0,
        }
        const response = trainTemplateMode.value === 'create'
            ? await axios.post('/OperationPlan/CreateTrainTemplate', payload)
            : await axios.put('/OperationPlan/EditTrainTemplate', payload)
        const saved = normalizeTrainTemplate(response.data)
        ElMessage.success(t(trainTemplateMode.value === 'create'
            ? 'operationPlan.train.messages.createSuccess'
            : 'operationPlan.train.messages.updateSuccess'))
        trainTemplateCreating.value = false
        trainTemplateEditingId.value = ''
        trainTemplateOriginalId.value = ''
        await loadTrainTemplates()
        if (saved?.trainTemplateID) {
            selectedTrainTemplateId.value = saved.trainTemplateID
            await loadMovementTemplates()
        }
    } catch (error) {
        console.error('Failed to save train template:', error)
        ElMessage.error(t(trainTemplateMode.value === 'create'
            ? 'operationPlan.train.messages.createFailed'
            : 'operationPlan.train.messages.updateFailed'))
    } finally {
        savingTrainTemplate.value = false
    }
}

async function updateTrainTemplateFixedOperation(row: TrainTemplate, checked: boolean) {
    if (!canEditTrainTemplates.value || operationPlanInlineActive.value || row.isDraft) return
    const previousValue = row.isFixedOperation
    row.isFixedOperation = checked
    savingTrainTemplate.value = true
    try {
        const response = await axios.put('/OperationPlan/EditTrainTemplate', {
            instanceID: row.instanceID || props.selectedInstanceId,
            stationSchemeID: row.stationSchemeID || currentStationSchemeId.value.trim(),
            operationPlanID: row.operationPlanID || getCurrentOperationPlanID(),
            originalTrainTemplateID: row.trainTemplateID,
            trainTemplateID: row.trainTemplateID,
            name: row.name,
            type: row.type,
            number: row.number,
            isFixedOperation: checked ? 1 : 0,
        })
        const saved = normalizeTrainTemplate(response.data)
        if (saved) {
            const index = trainTemplates.value.findIndex((item) => item.trainTemplateID === row.trainTemplateID)
            if (index >= 0) trainTemplates.value[index] = saved
        }
    } catch (error) {
        row.isFixedOperation = previousValue
        console.error('Failed to update train template fixed operation:', error)
        ElMessage.error(t('operationPlan.train.messages.updateFailed'))
    } finally {
        savingTrainTemplate.value = false
    }
}

function confirmDeleteTrainTemplate(row: TrainTemplate) {
    ElMessageBox.confirm(
        t('operationPlan.train.messages.deleteConfirm', { name: row.name }),
        t('operationPlan.train.dialogs.deleteTitle'),
        {
            confirmButtonText: t('operationPlan.actions.delete'),
            cancelButtonText: t('operationPlan.actions.cancel'),
            type: 'warning',
        },
    )
        .then(() => deleteTrainTemplate(row))
        .catch(() => {
            return
        })
}

async function deleteTrainTemplate(row: TrainTemplate) {
    loadingTrainTemplates.value = true
    try {
        await axios.delete('/OperationPlan/DeleteTrainTemplate', {
            params: {
                instanceID: props.selectedInstanceId,
                stationSchemeID: currentStationSchemeId.value.trim(),
                operationPlanID: getCurrentOperationPlanID(),
                trainTemplateID: row.trainTemplateID,
            },
        })
        if (selectedTrainTemplateId.value === row.trainTemplateID) {
            selectedTrainTemplateId.value = ''
            clearMovementTemplates()
        }
        ElMessage.success(t('operationPlan.train.messages.deleteSuccess'))
        await loadTrainTemplates()
    } catch (error) {
        console.error('Failed to delete train template:', error)
        ElMessage.error(t('operationPlan.train.messages.deleteFailed'))
    } finally {
        loadingTrainTemplates.value = false
    }
}

function startCreateMovementTemplateInline() {
    if (!selectedTrainTemplate.value) {
        ElMessage.warning(t('operationPlan.empty.expandTrain'))
        return
    }
    if (operationPlanInlineActive.value) return

    movementTemplateMode.value = 'create'
    movementTemplateEditingId.value = ''
    movementTemplateOriginalId.value = ''
    movementTemplateForm.value = createEmptyMovementTemplate()
    movementTemplateForm.value.sortOrder = movementTemplates.value.length
    movementTemplateRouteIds.value = []
    syncTemplateScope()
    movementTemplateCreating.value = true
}

function cancelCreateMovementTemplateInline() {
    movementTemplateCreating.value = false
    movementTemplateOriginalId.value = ''
    movementTemplateForm.value = createEmptyMovementTemplate()
    movementTemplateRouteIds.value = []
    routePickerVisible.value = false
    routePickerSelectedIds.value = []
}

function cancelEditMovementTemplateInline() {
    movementTemplateEditingId.value = ''
    movementTemplateOriginalId.value = ''
    movementTemplateForm.value = createEmptyMovementTemplate()
    movementTemplateRouteIds.value = []
    routePickerVisible.value = false
    routePickerSelectedIds.value = []
}

function cancelMovementTemplateInline(row: MovementTemplate) {
    if (row.isDraft) {
        cancelCreateMovementTemplateInline()
        return
    }

    cancelEditMovementTemplateInline()
}

function startEditMovementTemplateInline(row: MovementTemplate) {
    if (row.isDraft || !canEditMovementTemplates.value || operationPlanInlineActive.value) return

    movementTemplateCreating.value = false
    movementTemplateMode.value = 'edit'
    movementTemplateEditingId.value = row.movementID
    movementTemplateOriginalId.value = row.movementID
    movementTemplateForm.value = { ...row }
    movementTemplateRouteIds.value = parseRouteIDList(row.routeIDList)
    syncTemplateScope()
}

async function saveMovementTemplate() {
    syncTemplateScope()
    const form = movementTemplateForm.value
    if (!form.name.trim()) {
        ElMessage.warning(t('operationPlan.movement.messages.nameRequired'))
        return
    }

    savingMovementTemplate.value = true
    try {
        const payload = {
            instanceID: form.instanceID,
            stationSchemeID: form.stationSchemeID,
            operationPlanID: form.operationPlanID,
            trainTemplateID: form.trainTemplateID,
            originalMovementID: movementTemplateOriginalId.value,
            movementID: form.movementID.trim(),
            name: form.name.trim(),
            routeIDList: serializeRouteIDList(movementTemplateRouteIds.value),
            minDuration: form.minDuration,
            sortOrder: form.sortOrder,
        }
        const response = movementTemplateMode.value === 'create'
            ? await axios.post('/OperationPlan/CreateMovementTemplate', payload)
            : await axios.put('/OperationPlan/EditMovementTemplate', payload)
        const saved = normalizeMovementTemplate(response.data)
        ElMessage.success(t(movementTemplateMode.value === 'create'
            ? 'operationPlan.movement.messages.createSuccess'
            : 'operationPlan.movement.messages.updateSuccess'))
        movementTemplateCreating.value = false
        movementTemplateEditingId.value = ''
        movementTemplateOriginalId.value = ''
        routePickerVisible.value = false
        routePickerSelectedIds.value = []
        await loadMovementTemplates()
        if (saved?.movementID) {
            movementTemplateForm.value = saved
        }
    } catch (error) {
        console.error('Failed to save movement template:', error)
        ElMessage.error(t(movementTemplateMode.value === 'create'
            ? 'operationPlan.movement.messages.createFailed'
            : 'operationPlan.movement.messages.updateFailed'))
    } finally {
        savingMovementTemplate.value = false
    }
}

function confirmDeleteMovementTemplate(row: MovementTemplate) {
    ElMessageBox.confirm(
        t('operationPlan.movement.messages.deleteConfirm', { name: row.name }),
        t('operationPlan.movement.dialogs.deleteTitle'),
        {
            confirmButtonText: t('operationPlan.actions.delete'),
            cancelButtonText: t('operationPlan.actions.cancel'),
            type: 'warning',
        },
    )
        .then(() => deleteMovementTemplate(row))
        .catch(() => {
            return
        })
}

async function deleteMovementTemplate(row: MovementTemplate) {
    loadingMovementTemplates.value = true
    try {
        await axios.delete('/OperationPlan/DeleteMovementTemplate', {
            params: {
                instanceID: props.selectedInstanceId,
                stationSchemeID: currentStationSchemeId.value.trim(),
                operationPlanID: getCurrentOperationPlanID(),
                trainTemplateID: row.trainTemplateID,
                movementID: row.movementID,
            },
        })
        ElMessage.success(t('operationPlan.movement.messages.deleteSuccess'))
        await loadMovementTemplates()
    } catch (error) {
        console.error('Failed to delete movement template:', error)
        ElMessage.error(t('operationPlan.movement.messages.deleteFailed'))
    } finally {
        loadingMovementTemplates.value = false
    }
}

watch(filteredRoutePickerRoutes, () => {
    if (routePickerVisible.value) syncRoutePickerPreviewWithFilteredRoutes()
})

watch(routePickerEndpointFilterKey, () => {
    if (routePickerVisible.value) routePickerPreviewRouteId.value = ''
})

watch(activeOperationPlanTab, (tab) => {
    if (isOperationPlanChartDataTab(tab)) {
        void loadOperationPlanChartData()
    }
})

watch([operationOccupationTotalTimeSeconds, operationOccupationEmptyWasteFactor], () => {
    if (
        usingOperationAnalysisSnapshot.value ||
        !isOperationPlanChartDataTab(activeOperationPlanTab.value) ||
        operationPlanChartBars.value.length === 0
    ) {
        return
    }

    scheduleSaveOperationAnalysisSnapshot()
})

watch(
    displayOperationOccupationTimeTableCells,
    (cells) => {
        syncOperationOccupationTimeSubTables(cells)
    },
    { immediate: true },
)

watch(
    operationOccupationTimeSubTables,
    () => {
        if (suppressOperationOccupationTimeSubTableSave || loadingOperationOccupationTimeSubTableSettings.value) return
        operationOccupationTimeSubTableSaveRevision += 1
        scheduleSaveOperationOccupationTimeSubTableSettings()
    },
    { deep: true },
)

watch(
    () => props.selectedInstanceId,
    async () => {
        currentStationSchemeId.value = ''
        clearOperationPlans()
        stationRouteOptions.value = []
        clearTrainTemplates()
        await loadStationSchemes()
        await refreshOperationPlanData()
    },
    { immediate: true },
)

onBeforeUnmount(() => {
    if (operationAnalysisSnapshotSaveTimer) {
        window.clearTimeout(operationAnalysisSnapshotSaveTimer)
        operationAnalysisSnapshotSaveTimer = null
    }
    if (operationBottleneckSummaryCategorySaveTimer) {
        window.clearTimeout(operationBottleneckSummaryCategorySaveTimer)
        operationBottleneckSummaryCategorySaveTimer = null
    }
    if (operationOccupationTimeSubTableSaveTimer) {
        window.clearTimeout(operationOccupationTimeSubTableSaveTimer)
        operationOccupationTimeSubTableSaveTimer = null
    }
    stopRoutePickerTableResize()
})
</script>

<style scoped lang="css">
.operation-plan-page {
    display: flex;
    flex-direction: column;
    width: 100%;
    height: 100%;
    min-height: 0;
    gap: 12px;
    overflow: hidden;
}

.operation-plan-toolbar {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 12px;
    min-height: 36px;
}

.operation-plan-scheme-control,
.operation-plan-object-control,
.operation-plan-toolbar-actions,
.operation-plan-card-actions,
.operation-plan-row-actions {
    display: flex;
    align-items: center;
    gap: 8px;
}

.operation-plan-control-label {
    color: #4b5f77;
    font-size: 13px;
    font-weight: 600;
    white-space: nowrap;
}

.operation-plan-scheme-select {
    width: min(360px, 54vw);
}

.operation-plan-object-select {
    width: min(300px, 38vw);
}

.operation-plan-object-dialog :deep(.el-dialog__body) {
    padding-top: 8px;
}

.operation-plan-object-manager {
    display: flex;
    flex-direction: column;
    gap: 12px;
}

.operation-plan-object-manager-toolbar {
    display: flex;
    align-items: center;
    gap: 8px;
    justify-content: flex-end;
}

.operation-plan-object-table {
    width: 100%;
}

.operation-plan-object-sort-input {
    width: 86px;
}

.operation-plan-sub-tabs {
    display: flex;
    flex: 1;
    flex-direction: column;
    min-height: 0;
    overflow: hidden;
}

.operation-plan-sub-tabs :deep(.el-tabs__header) {
    flex: 0 0 auto;
    margin: 0 0 10px;
}

.operation-plan-sub-tabs :deep(.el-tabs__content) {
    flex: 1;
    min-height: 0;
    overflow: hidden;
}

.operation-plan-sub-tabs :deep(.el-tab-pane) {
    display: flex;
    height: 100%;
    min-height: 0;
}

.operation-plan-sub-tab-pane {
    height: 100%;
    min-height: 0;
}

.operation-plan-grid {
    display: grid;
    grid-template-columns: minmax(420px, 760px);
    align-items: stretch;
    gap: 12px;
    min-height: 0;
    flex: 1;
    overflow: hidden;
}

.operation-plan-sub-tab-pane .operation-plan-grid {
    height: 100%;
}

.train-operation-plan-panel {
    display: flex;
    flex: 1;
    flex-direction: column;
    gap: 12px;
    min-width: 0;
    min-height: 0;
}

.train-operation-plan-toolbar {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 12px;
    min-height: 32px;
}

.train-operation-plan-time-range {
    display: flex;
    align-items: center;
    gap: 8px;
    min-width: 0;
}

.train-operation-plan-time-range span {
    color: #4b5f77;
    font-size: 13px;
    font-weight: 600;
    white-space: nowrap;
}

.train-operation-plan-time-input {
    width: 108px;
}

.train-operation-plan-grid {
    display: grid;
    grid-template-columns: minmax(420px, 760px);
    gap: 12px;
    min-height: 0;
    flex: 1;
    overflow: hidden;
}

.train-operation-plan-grid.is-expanded {
    grid-template-columns: minmax(320px, 0.8fr) minmax(520px, 1.4fr);
}

.operation-plan-grid.is-expanded {
    grid-template-columns: minmax(360px, 0.9fr) minmax(440px, 1.2fr);
}

.operation-plan-card {
    display: flex;
    flex-direction: column;
    min-width: 0;
    min-height: 0;
    border: 1px solid #d8e3ef;
    border-radius: 8px;
    background: #ffffff;
    overflow: hidden;
}

.operation-plan-card-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 12px;
    padding: 12px 14px;
    border-bottom: 1px solid #e4edf6;
    background: #f8fbff;
}

.operation-plan-card-header h2 {
    margin: 0;
    color: #21354f;
    font-size: 16px;
    font-weight: 700;
    line-height: 1.4;
}

.operation-plan-card-header span {
    color: #65758a;
    font-size: 12px;
}

.movement-template-context {
    display: flex;
    align-items: center;
    gap: 8px;
    min-height: 36px;
    padding: 0 14px;
    border-bottom: 1px solid #edf2f7;
    color: #36506d;
    font-size: 13px;
    font-weight: 600;
}

.operation-plan-table {
    flex: 1;
    min-height: 0;
}

.operation-plan-name-edit-cell {
    display: flex;
    flex-direction: column;
    gap: 6px;
    min-width: 0;
}

.operation-plan-hover-name {
    display: inline-block;
    max-width: 100%;
    overflow: hidden;
    text-overflow: ellipsis;
    vertical-align: middle;
    white-space: nowrap;
    cursor: help;
}

.operation-plan-table :deep(.is-expanded-row) {
    --el-table-tr-bg-color: #eef6ff;
}

.operation-plan-table :deep(.is-draft-row) {
    --el-table-tr-bg-color: #f7fbff;
}

.operation-plan-table :deep(.is-edit-row) {
    --el-table-tr-bg-color: #fffaf0;
}

.operation-plan-route-tags {
    display: flex;
    align-items: center;
    flex-wrap: wrap;
    gap: 4px;
    min-width: 0;
}

.operation-plan-route-tags :deep(.el-tag__content) {
    max-width: 180px;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
}

.operation-plan-route-edit-cell {
    display: flex;
    align-items: center;
    gap: 8px;
    min-width: 0;
}

.operation-plan-route-edit-tags {
    flex: 1;
    min-width: 0;
}

.operation-plan-route-option {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 12px;
    min-width: 0;
}

.operation-plan-route-option span {
    min-width: 0;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
}

.operation-plan-route-option small {
    flex: 0 0 auto;
    color: #718096;
    font-size: 12px;
}

:deep(.operation-plan-route-picker-dialog.el-dialog.is-fullscreen) {
    display: flex;
    flex-direction: column;
    width: 100vw;
    max-width: none;
    height: 100vh;
    margin: 0;
    overflow: hidden;
}

:deep(.operation-plan-route-picker-dialog.el-dialog.is-fullscreen .el-dialog__header) {
    flex: 0 0 auto;
    margin-right: 0;
    padding: 14px 18px 10px;
    border-bottom: 1px solid #e4edf6;
}

:deep(.operation-plan-route-picker-dialog.el-dialog.is-fullscreen .el-dialog__body) {
    display: flex;
    flex: 1 1 auto;
    min-height: 0;
    padding: 10px 16px 12px;
    overflow: hidden;
}

:deep(.operation-plan-route-picker-dialog.el-dialog.is-fullscreen .el-dialog__footer) {
    flex: 0 0 auto;
    padding: 10px 16px 14px;
    border-top: 1px solid #e4edf6;
}

.operation-plan-route-picker {
    display: flex;
    flex: 1;
    flex-direction: column;
    width: 100%;
    height: 100%;
    gap: 10px;
    min-height: 0;
    overflow: hidden;
}

.operation-plan-route-picker-toolbar {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 12px;
}

.operation-plan-route-picker-toolbar span {
    color: #4b5f77;
    font-size: 13px;
    font-weight: 600;
}

.operation-plan-route-filter-panel {
    display: grid;
    grid-template-columns: repeat(2, minmax(0, 1fr));
    gap: 8px;
}

.operation-plan-route-filter-control,
.operation-plan-route-filter-clear {
    width: 100%;
}

.operation-plan-route-filter-clear {
    grid-column: 1 / -1;
}

.operation-plan-route-picker-split {
    display: flex;
    flex: 1 1 auto;
    flex-direction: column;
    min-height: 0;
    overflow: hidden;
}

.operation-plan-route-picker-table-pane {
    flex: 0 0 auto;
    min-height: 160px;
    overflow: hidden;
}

.operation-plan-route-picker-table {
    height: 100%;
    border: 1px solid #e4edf6;
    border-radius: 6px;
}

.operation-plan-route-picker-splitter {
    position: relative;
    display: flex;
    flex: 0 0 10px;
    align-items: center;
    justify-content: center;
    height: 10px;
    cursor: row-resize;
    outline: none;
}

.operation-plan-route-picker-splitter::before {
    position: absolute;
    inset: 0;
    content: "";
}

.operation-plan-route-picker-splitter span {
    width: 72px;
    height: 3px;
    border-radius: 999px;
    background: #9fb2c8;
}

.operation-plan-route-picker-splitter:focus-visible span,
.operation-plan-route-picker-splitter:hover span {
    background: #3b82f6;
}

.operation-plan-route-picker-layout {
    display: flex;
    flex: 1 1 auto;
    flex-direction: column;
    gap: 6px;
    min-height: 220px;
    overflow: hidden;
}

.operation-plan-route-picker-layout-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 12px;
    min-height: 22px;
}

.operation-plan-route-picker-layout-title {
    display: flex;
    align-items: center;
    gap: 10px;
    min-width: 0;
}

.operation-plan-route-picker-layout-title span {
    flex: 0 0 auto;
    color: #4b5f77;
    font-size: 13px;
    font-weight: 600;
}

.operation-plan-route-picker-layout-title strong {
    min-width: 0;
    overflow: hidden;
    color: #21354f;
    font-size: 13px;
    font-weight: 600;
    text-overflow: ellipsis;
    white-space: nowrap;
}

.operation-plan-route-picker-node-filter {
    display: flex;
    flex: 0 1 auto;
    flex-wrap: wrap;
    align-items: center;
    justify-content: flex-end;
    gap: 6px;
    min-width: 0;
}

.operation-plan-route-picker-layout-view {
    position: relative;
    flex: 1 1 auto;
    min-height: 0;
    overflow: auto;
    border: 1px solid #d8e3ef;
    border-radius: 6px;
    background: #0f172a;
}

.operation-plan-route-picker-layout-empty {
    display: flex;
    align-items: center;
    justify-content: center;
    height: 100%;
    color: #cbd5e1;
    font-size: 13px;
}

.operation-plan-muted {
    color: #718096;
    font-size: 12px;
}

.operation-plan-table-number-input {
    width: 100%;
    min-width: 0;
}

.operation-plan-chart-card {
    flex: 1;
    min-width: 0;
}

.operation-plan-chart-empty {
    display: flex;
    align-items: center;
    justify-content: center;
    flex: 1;
    min-height: 220px;
    color: #718096;
    font-size: 13px;
}

.operation-plan-chart-scroll {
    flex: 1;
    min-height: 0;
    overflow: auto;
    background: #ffffff;
}

.operation-plan-chart-grid {
    display: grid;
    align-items: stretch;
    min-height: 100%;
}

.operation-plan-chart-corner,
.operation-plan-chart-time-head {
    position: sticky;
    top: 0;
    z-index: 3;
    height: 44px;
    border-bottom: 1px solid #d8e3ef;
    background: #f8fbff;
}

.operation-plan-chart-corner {
    left: 0;
    z-index: 5;
    display: flex;
    align-items: center;
    padding: 0 12px;
    border-right: 1px solid #d8e3ef;
    color: #36506d;
    font-size: 12px;
    font-weight: 700;
}

.operation-plan-chart-time-head {
    position: sticky;
    overflow: hidden;
}

.operation-plan-chart-axis-title {
    position: absolute;
    top: 7px;
    left: 12px;
    color: #36506d;
    font-size: 12px;
    font-weight: 700;
}

.operation-plan-chart-tick-label {
    position: absolute;
    bottom: 6px;
    transform: translateX(-50%);
    color: #65758a;
    font-size: 11px;
    white-space: nowrap;
}

.operation-plan-chart-cell {
    position: sticky;
    left: 0;
    z-index: 2;
    display: flex;
    align-items: center;
    min-width: 0;
    padding: 0 12px;
    overflow: hidden;
    border-right: 1px solid #d8e3ef;
    border-bottom: 1px solid #edf2f7;
    background: #ffffff;
    color: #36506d;
    font-size: 12px;
    font-weight: 600;
    text-overflow: ellipsis;
    white-space: nowrap;
}

.operation-plan-chart-track {
    position: relative;
    overflow: hidden;
    border-bottom: 1px solid #edf2f7;
    background:
        linear-gradient(90deg, rgba(216, 227, 239, 0.42) 1px, transparent 1px) 0 0 / 120px 100%,
        #ffffff;
}

.operation-plan-chart-track:nth-of-type(4n) {
    background-color: #fbfdff;
}

.operation-plan-chart-grid-line {
    position: absolute;
    top: 0;
    bottom: 0;
    width: 1px;
    background: #e4edf6;
    pointer-events: none;
}

.operation-plan-chart-bar {
    position: absolute;
    display: flex;
    align-items: center;
    height: 18px;
    min-width: 8px;
    max-width: none;
    padding: 0 7px;
    overflow: hidden;
    border-radius: 5px;
    color: #ffffff;
    box-shadow: 0 2px 7px rgba(33, 53, 79, 0.16);
}

.operation-plan-chart-bar span {
    min-width: 0;
    overflow: hidden;
    font-size: 11px;
    font-weight: 600;
    line-height: 18px;
    text-overflow: ellipsis;
    white-space: nowrap;
}

.operation-occupation-time-card {
    flex: 1;
    min-width: 0;
}

.operation-occupation-time-subtable-panel {
    display: flex;
    flex: 1;
    flex-direction: column;
    gap: 10px;
    min-height: 0;
    padding: 10px 12px 12px;
}

.operation-occupation-time-subtable-toolbar {
    display: flex;
    align-items: center;
    gap: 8px;
    min-width: 0;
}

.operation-occupation-time-sub-tabs {
    min-width: 0;
    flex: 1;
}

.operation-occupation-time-sub-tabs :deep(.el-tabs__header) {
    margin: 0;
}

.operation-occupation-time-sub-tabs :deep(.el-tabs__nav-wrap::after) {
    display: none;
}

.operation-occupation-time-subtable-controls {
    display: flex;
    align-items: center;
    flex-wrap: wrap;
    gap: 8px;
    min-height: 32px;
}

.operation-occupation-time-subtable-summary {
    color: #65758a;
    font-size: 12px;
    font-weight: 600;
    white-space: nowrap;
}

.operation-occupation-time-subtable-form {
    display: flex;
    flex-direction: column;
    gap: 4px;
}

.operation-occupation-time-subtable-dialog-cell-select {
    width: 100%;
}

.operation-occupation-time-table {
    flex: 1;
    min-height: 0;
}

.operation-occupation-time-table :deep(.el-table__cell) {
    font-size: 12px;
}

.operation-occupation-time-table :deep(.el-table__cell .cell) {
    padding-right: 6px;
    padding-left: 6px;
    white-space: nowrap;
}

.operation-occupation-time-total-control,
.operation-occupation-time-factor-control {
    display: flex;
    align-items: center;
    gap: 8px;
    color: #4b5f77;
    font-size: 12px;
    font-weight: 600;
    white-space: nowrap;
}

.operation-occupation-time-unit-control {
    display: flex;
    align-items: center;
    gap: 8px;
    color: #4b5f77;
    font-size: 12px;
    font-weight: 600;
    white-space: nowrap;
}

.operation-occupation-time-unit-control :deep(.el-radio-button__inner) {
    min-width: 48px;
}

.operation-occupation-time-total-control :deep(.el-input-number),
.operation-occupation-time-factor-control :deep(.el-input-number) {
    width: 132px;
}

.operation-occupation-time-table :deep(.operation-occupation-time-group-row) {
    --el-table-tr-bg-color: #eef6f8;
    color: #1f3a4a;
    font-weight: 700;
}

.operation-occupation-time-table :deep(.operation-occupation-time-fixed-total-row) {
    --el-table-tr-bg-color: #f2f8f4;
    color: #21354f;
    font-weight: 700;
}

.operation-occupation-time-table :deep(.operation-occupation-time-total-row) {
    --el-table-tr-bg-color: #f8fbff;
    color: #21354f;
    font-weight: 700;
}

.operation-occupation-time-table :deep(.operation-occupation-time-utilization-row) {
    --el-table-tr-bg-color: #fffaf0;
    color: #21354f;
    font-weight: 700;
}

.operation-bottleneck-analysis-card {
    flex: 1;
    min-width: 0;
}

.operation-bottleneck-summary-card {
    flex: 1;
    min-width: 0;
}

.operation-bottleneck-analysis-content {
    display: flex;
    flex: 1;
    flex-direction: column;
    gap: 12px;
    min-height: 0;
}

.operation-bottleneck-analysis-detail-table {
    flex: 1;
    min-height: 0;
}

.operation-bottleneck-summary-table {
    flex: 1;
    min-height: 0;
}

.operation-bottleneck-summary-panel {
    display: flex;
    flex: 1 1 48%;
    flex-direction: column;
    min-height: 220px;
    overflow: hidden;
    border: 1px solid #d8e3ef;
    border-radius: 8px;
    background: #ffffff;
}

.operation-bottleneck-summary-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 12px;
    padding: 12px 14px;
    border-bottom: 1px solid #e4edf6;
    background: #f8fbff;
}

.operation-bottleneck-summary-header h3 {
    margin: 0;
    color: #21354f;
    font-size: 14px;
    font-weight: 700;
    line-height: 1.4;
}

.operation-bottleneck-summary-header span {
    color: #65758a;
    font-size: 12px;
}

.operation-bottleneck-summary-actions {
    display: flex;
    flex-wrap: wrap;
    justify-content: flex-end;
    gap: 8px;
}

.operation-bottleneck-summary-actions :deep(.el-button + .el-button) {
    margin-left: 0;
}

.operation-bottleneck-route-picker {
    display: flex;
    flex-direction: column;
    gap: 10px;
}

.operation-bottleneck-route-picker-toolbar {
    display: flex;
    align-items: flex-start;
    justify-content: space-between;
    gap: 12px;
}

.operation-bottleneck-route-picker-toolbar > span {
    flex: 0 0 auto;
    color: #4b5f77;
    font-size: 13px;
    font-weight: 600;
    line-height: 28px;
}

.operation-bottleneck-route-picker-filters {
    display: grid;
    grid-template-columns: minmax(160px, 1fr) minmax(180px, 1fr) minmax(180px, 1fr) auto;
    gap: 8px;
    min-width: 0;
    flex: 1;
}

.operation-bottleneck-route-picker-table {
    border: 1px solid #e4edf6;
    border-radius: 6px;
}

.operation-bottleneck-analysis-table,
.operation-bottleneck-summary-table {
    flex: 1;
    min-height: 0;
}

.operation-bottleneck-analysis-table :deep(.el-table__cell),
.operation-bottleneck-summary-table :deep(.el-table__cell) {
    font-size: 12px;
}

@media (max-width: 960px) {
    .operation-plan-grid,
    .operation-plan-grid.is-expanded,
    .train-operation-plan-grid,
    .train-operation-plan-grid.is-expanded {
        grid-template-columns: minmax(0, 1fr);
        overflow: auto;
    }

    .operation-plan-card {
        min-height: 320px;
    }
}

@media (max-width: 640px) {
    .operation-plan-toolbar {
        align-items: stretch;
        flex-direction: column;
    }

    .operation-plan-scheme-control,
    .operation-plan-object-control,
    .operation-plan-toolbar-actions {
        align-items: stretch;
        flex-direction: column;
    }

    .train-operation-plan-toolbar,
    .train-operation-plan-time-range {
        align-items: stretch;
        flex-direction: column;
    }

    .operation-plan-scheme-select,
    .operation-plan-object-select {
        width: 100%;
    }

    .train-operation-plan-time-input {
        width: 100%;
    }
}
</style>
