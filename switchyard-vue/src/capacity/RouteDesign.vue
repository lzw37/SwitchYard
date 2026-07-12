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
                <div class="route-design-display-toolbar">
                    <span class="route-design-control-label">{{ t('routeDesign.toolbar.layoutDisplay') }}</span>
                    <div class="route-design-switch-control">
                        <span class="route-design-control-label">{{ t('routeDesign.toolbar.showGrid') }}</span>
                        <el-switch v-model="showLayoutGrid" size="small" />
                    </div>
                    <div class="route-design-switch-control">
                        <span class="route-design-control-label">{{ t('routeDesign.toolbar.showNodes') }}</span>
                        <el-switch v-model="showLayoutNodes" size="small" />
                    </div>
                    <div class="route-design-switch-control">
                        <span class="route-design-control-label">{{ t('routeDesign.toolbar.curveDisplay') }}</span>
                        <el-switch
                            v-model="showLayoutCurveArc"
                            size="small"
                            inline-prompt
                            :active-text="t('stationLayout.curveDisplay.arc')"
                            :inactive-text="t('stationLayout.curveDisplay.tangent')"
                        />
                    </div>
                    <div class="route-design-switch-control">
                        <span class="route-design-control-label">{{ t('routeDesign.toolbar.showCellNames') }}</span>
                        <el-switch v-model="showLayoutCellNames" size="small" />
                    </div>
                    <div class="route-design-scale-control">
                        <span class="route-design-control-label">{{ t('routeDesign.toolbar.displayScale') }}</span>
                        <span class="route-design-scale-label">{{ t('stationLayout.scale.x') }}</span>
                        <el-slider
                            v-model="layoutScaleX"
                            size="small"
                            :min="0.25"
                            :max="4"
                            :step="0.05"
                            class="route-design-scale-slider"
                        />
                        <span class="route-design-scale-value">{{ layoutScaleX.toFixed(2) }}</span>
                        <span class="route-design-scale-label">{{ t('stationLayout.scale.y') }}</span>
                        <el-slider
                            v-model="layoutScaleY"
                            size="small"
                            :min="0.25"
                            :max="4"
                            :step="0.05"
                            class="route-design-scale-slider"
                        />
                        <span class="route-design-scale-value">{{ layoutScaleY.toFixed(2) }}</span>
                    </div>
                </div>
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
                        :display-scale-x="layoutScaleX"
                        :display-scale-y="layoutScaleY"
                        :display-styles="layoutDisplayStyles"
                        :show-grid="showLayoutGrid"
                        :show-nodes="showLayoutNodes"
                        :show-curve-arc="showLayoutCurveArc"
                        :grid-spacing="layoutGridSpacing"
                        :cells="layoutCells"
                        :show-cell-names="showLayoutCellNames"
                        :route-pick-target="routePickTarget"
                        :highlighted-route-node-ids="highlightedRouteNodeIds"
                        :highlighted-route-link-ids="highlightedRouteLinkIds"
                        :highlighted-route-arrow-node-ids="highlightedRouteArrowNodeIds"
                        :highlighted-route-color="highlightedRouteColor"
                        :highlighted-route-arrow-visible="highlightedRouteArrowVisible"
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
                        <el-table-column :label="t('routeDesign.stationRoute.searchDialog.cellCount')" width="112">
                            <template #default="{ row }">
                                <el-tooltip
                                    :content="getRouteIdsTooltip(row.cellIds)"
                                    placement="top"
                                    :disabled="row.cellIds.length === 0"
                                >
                                    <span class="route-search-count">
                                        {{ t('routeDesign.stationRoute.searchDialog.cellSummary', { count: row.cellIds.length }) }}
                                    </span>
                                </el-tooltip>
                            </template>
                        </el-table-column>
                        <el-table-column :label="t('routeDesign.stationRoute.searchDialog.linkCount')" width="112">
                            <template #default="{ row }">
                                <el-tooltip
                                    :content="getRouteIdsTooltip(row.linkIds)"
                                    placement="top"
                                    :disabled="row.linkIds.length === 0"
                                >
                                    <span class="route-search-count">
                                        {{ t('routeDesign.stationRoute.searchDialog.linkSummary', { count: row.linkIds.length }) }}
                                    </span>
                                </el-tooltip>
                            </template>
                        </el-table-column>
                        <el-table-column :label="t('routeDesign.stationRoute.searchDialog.signalCount')" width="112">
                            <template #default="{ row }">
                                <el-tooltip
                                    :content="getRouteIdsTooltip(row.signalIds)"
                                    placement="top"
                                    :disabled="row.signalIds.length === 0"
                                >
                                    <span class="route-search-count">
                                        {{ t('routeDesign.stationRoute.searchDialog.signalSummary', { count: row.signalIds.length }) }}
                                    </span>
                                </el-tooltip>
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
                            <el-tooltip :content="t('routeDesign.stationRoute.actions.autoGenerate')" placement="top">
                                <el-button
                                    :icon="MagicStick"
                                    circle
                                    size="small"
                                    :type="showAutoRouteGenerateCard ? 'primary' : 'default'"
                                    :disabled="!canEditRoutes"
                                    @click="toggleAutoRouteGenerateCard"
                                />
                            </el-tooltip>
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

                    <div
                        ref="stationRouteContentRef"
                        class="station-route-content"
                        :class="{ 'is-stack-resizing': isStationRouteStackResizing }"
                        :style="stationRouteContentStyle"
                    >
                        <div class="station-route-list-panel">
                            <div class="station-route-list-toolbar">
                                <span class="station-route-list-summary">{{ stationRouteListSummary }}</span>
                                <div class="station-route-list-actions">
                                    <el-tooltip :content="t('routeDesign.stationRoute.actions.batchDelete')" placement="top">
                                        <el-button
                                            :icon="Delete"
                                            circle
                                            size="small"
                                            type="danger"
                                            plain
                                            :disabled="!canBatchDeleteRoutes"
                                            @click="deleteSelectedStationRoutes"
                                        />
                                    </el-tooltip>
                                    <el-popover
                                        placement="bottom-start"
                                        trigger="click"
                                        width="420"
                                        popper-class="station-route-filter-popover"
                                    >
                                        <template #reference>
                                            <el-button
                                                :icon="Filter"
                                                circle
                                                size="small"
                                                :type="routeFiltersActive ? 'primary' : 'default'"
                                                :title="t('routeDesign.stationRoute.actions.filter')"
                                            />
                                        </template>
                                        <div class="station-route-filter-panel">
                                            <el-select
                                                v-model="routeFilters.types"
                                                multiple
                                                filterable
                                                clearable
                                                collapse-tags
                                                collapse-tags-tooltip
                                                :reserve-keyword="false"
                                                size="small"
                                                class="station-route-filter-control"
                                                :placeholder="t('routeDesign.stationRoute.filter.type')"
                                            >
                                                <el-option
                                                    v-for="option in routeFilterTypeOptions"
                                                    :key="`route-filter-type-${option.id}`"
                                                    :label="option.name"
                                                    :value="option.id"
                                                />
                                            </el-select>
                                            <el-select
                                                v-for="filter in routeFilterFieldControls"
                                                :key="filter.field"
                                                v-model="routeFilters[filter.field]"
                                                multiple
                                                filterable
                                                clearable
                                                collapse-tags
                                                collapse-tags-tooltip
                                                :reserve-keyword="false"
                                                size="small"
                                                class="station-route-filter-control"
                                                :placeholder="t(filter.placeholderKey)"
                                            >
                                                <el-option
                                                    v-for="option in getRouteFilterSelectOptions(filter)"
                                                    :key="`route-filter-${filter.field}-${option.id}`"
                                                    :label="option.name"
                                                    :value="option.id"
                                                >
                                                    <div class="station-route-select-option">
                                                        <span class="station-route-select-option-name">{{ option.name }}</span>
                                                        <span
                                                            v-if="option.id !== option.name"
                                                            class="station-route-select-option-id"
                                                        >
                                                            {{ option.id }}
                                                        </span>
                                                    </div>
                                                </el-option>
                                            </el-select>
                                            <el-button
                                                :icon="Close"
                                                size="small"
                                                class="station-route-filter-clear"
                                                :disabled="!routeFiltersActive"
                                                @click="clearRouteFilters"
                                            >
                                                {{ t('routeDesign.stationRoute.actions.clearFilters') }}
                                            </el-button>
                                        </div>
                                    </el-popover>
                                </div>
                            </div>

                            <div class="station-route-table-wrap">
                                <el-table
                                    :data="filteredStationRoutes"
                                    size="small"
                                    height="100%"
                                    row-key="id"
                                    highlight-current-row
                                    :current-row-key="selectedRouteId"
                                    :empty-text="stationRouteTableEmptyText"
                                    @row-click="selectStationRoute"
                                    @selection-change="handleStationRouteSelectionChange"
                                >
                                    <el-table-column type="selection" width="42" />
                                    <el-table-column prop="id" :label="t('routeDesign.stationRoute.fields.id')" min-width="116" show-overflow-tooltip />
                                    <el-table-column prop="type" :label="t('routeDesign.stationRoute.fields.type')" min-width="88" show-overflow-tooltip />
                                    <el-table-column prop="description" :label="t('routeDesign.stationRoute.fields.description')" min-width="130" show-overflow-tooltip />
                                    <el-table-column prop="startNodeID" :label="t('routeDesign.stationRoute.fields.startNodeID')" width="86" show-overflow-tooltip />
                                    <el-table-column prop="endNodeID" :label="t('routeDesign.stationRoute.fields.endNodeID')" width="86" show-overflow-tooltip />
                                </el-table>
                            </div>
                        </div>

                        <div
                            class="station-route-stack-resizer"
                            role="separator"
                            aria-orientation="horizontal"
                            @mousedown="startStationRouteStackResize"
                            @dblclick="resetStationRouteStackResize"
                        />

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
                                <el-form-item :label="t('routeDesign.stationRoute.fields.id')">
                                    <el-input
                                        v-model="routeForm.id"
                                        :placeholder="t('routeDesign.stationRoute.placeholders.autoId')"
                                        disabled
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
                                <el-form-item :label="t('routeDesign.stationRoute.fields.description')">
                                    <div class="station-route-description-control">
                                        <el-input
                                            v-model="routeForm.description"
                                            type="textarea"
                                            :autosize="{ minRows: 2, maxRows: 4 }"
                                            :disabled="!canEditRoutes || savingRoute || generatingRouteDescription"
                                        />
                                        <el-tooltip :content="t('routeDesign.stationRoute.actions.generateDescription')" placement="top">
                                            <el-button
                                                :icon="MagicStick"
                                                :loading="generatingRouteDescription"
                                                :disabled="!canEditRoutes || savingRoute"
                                                @click="generateStationRouteDescription"
                                            />
                                        </el-tooltip>
                                    </div>
                                </el-form-item>
                                <el-form-item :label="t('routeDesign.stationRoute.fields.startNodeID')" required>
                                    <div class="station-route-node-control">
                                        <el-input
                                            v-model="routeForm.startNodeID"
                                            disabled
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
                                            disabled
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
                                <el-form-item
                                    v-for="field in routeListFieldControls"
                                    :key="field.field"
                                    :label="t(field.labelKey)"
                                >
                                    <el-select
                                        :model-value="getRouteListValue(field.field)"
                                        multiple
                                        filterable
                                        clearable
                                        :allow-create="field.allowCreate"
                                        default-first-option
                                        :reserve-keyword="false"
                                        :disabled="!canEditRoutes || savingRoute"
                                        class="station-route-input-tag"
                                        :placeholder="t(field.placeholderKey)"
                                        :filter-method="(query: string) => setRouteListFilterQuery(field.field, query)"
                                        @update:model-value="(value: string[]) => setRouteListValue(field.field, value)"
                                        @visible-change="(visible: boolean) => handleRouteListVisibleChange(field.field, visible)"
                                    >
                                        <el-option
                                            v-for="option in getRouteListSelectOptions(field.field)"
                                            :key="`${field.field}-${option.id}`"
                                            :label="option.name"
                                            :value="option.id"
                                        >
                                            <div class="station-route-select-option">
                                                <span class="station-route-select-option-name">{{ option.name }}</span>
                                                <span
                                                    v-if="option.id !== option.name"
                                                    class="station-route-select-option-id"
                                                >
                                                    {{ option.id }}
                                                </span>
                                            </div>
                                        </el-option>
                                    </el-select>
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
                    </div>
                </section>

                <section
                    v-if="showAutoRouteGenerateCard"
                    class="auto-route-card"
                    v-loading="autoRouteGenerationLoading"
                >
                    <header class="auto-route-card-header">
                        <div class="auto-route-title-group">
                            <h2>{{ t('routeDesign.autoRoute.title') }}</h2>
                            <span class="auto-route-subtitle">
                                {{ autoRouteGenerateHeaderText }}
                            </span>
                        </div>
                        <div class="auto-route-card-actions">
                            <el-button
                                :icon="Close"
                                circle
                                size="small"
                                :disabled="autoRouteGenerationLoading"
                                @click="closeAutoRouteGenerateCard"
                            />
                        </div>
                    </header>

                    <div class="auto-route-form-panel">
                        <el-form label-position="top" size="small" class="auto-route-form">
                            <el-form-item :label="t('routeDesign.autoRoute.fields.startNodes')" required>
                                <div class="auto-route-node-control">
                                    <el-select
                                        v-model="autoRouteStartNodeIds"
                                        multiple
                                        filterable
                                        clearable
                                        collapse-tags
                                        collapse-tags-tooltip
                                        :reserve-keyword="false"
                                        :disabled="!canEditRoutes || autoRouteGenerationLoading"
                                        class="auto-route-node-select"
                                        :placeholder="t('routeDesign.autoRoute.placeholders.selectNodes')"
                                    >
                                        <el-option
                                            v-for="option in autoRouteNodeOptions"
                                            :key="`auto-route-start-${option.id}`"
                                            :label="option.name"
                                            :value="option.id"
                                        >
                                            <div class="station-route-select-option">
                                                <span class="station-route-select-option-name">{{ option.name }}</span>
                                                <span
                                                    v-if="option.id !== option.name"
                                                    class="station-route-select-option-id"
                                                >
                                                    {{ option.id }}
                                                </span>
                                            </div>
                                        </el-option>
                                    </el-select>
                                    <el-tooltip :content="t('routeDesign.autoRoute.actions.pickStart')" placement="top">
                                        <el-button
                                            :icon="Aim"
                                            :type="autoRoutePickStage === 'start' ? 'primary' : 'default'"
                                            :disabled="!canEditRoutes || autoRouteGenerationLoading"
                                            @click="startAutoRouteNodePick('start')"
                                        />
                                    </el-tooltip>
                                </div>
                            </el-form-item>
                            <el-form-item :label="t('routeDesign.autoRoute.fields.endNodes')" required>
                                <div class="auto-route-node-control">
                                    <el-select
                                        v-model="autoRouteEndNodeIds"
                                        multiple
                                        filterable
                                        clearable
                                        collapse-tags
                                        collapse-tags-tooltip
                                        :reserve-keyword="false"
                                        :disabled="!canEditRoutes || autoRouteGenerationLoading"
                                        class="auto-route-node-select"
                                        :placeholder="t('routeDesign.autoRoute.placeholders.selectNodes')"
                                    >
                                        <el-option
                                            v-for="option in autoRouteNodeOptions"
                                            :key="`auto-route-end-${option.id}`"
                                            :label="option.name"
                                            :value="option.id"
                                        >
                                            <div class="station-route-select-option">
                                                <span class="station-route-select-option-name">{{ option.name }}</span>
                                                <span
                                                    v-if="option.id !== option.name"
                                                    class="station-route-select-option-id"
                                                >
                                                    {{ option.id }}
                                                </span>
                                            </div>
                                        </el-option>
                                    </el-select>
                                    <el-tooltip :content="t('routeDesign.autoRoute.actions.pickEnd')" placement="top">
                                        <el-button
                                            :icon="Aim"
                                            :type="autoRoutePickStage === 'end' ? 'primary' : 'default'"
                                            :disabled="!canEditRoutes || autoRouteGenerationLoading"
                                            @click="startAutoRouteNodePick('end')"
                                        />
                                    </el-tooltip>
                                </div>
                            </el-form-item>
                        </el-form>

                        <div class="auto-route-summary">
                            {{ t('routeDesign.autoRoute.count', {
                                start: autoRouteStartNodeIds.length,
                                end: autoRouteEndNodeIds.length,
                                pairs: autoRoutePairCount,
                            }) }}
                        </div>
                        <div v-if="autoRouteGenerationStatus" class="auto-route-status">
                            {{ autoRouteGenerationStatus }}
                        </div>

                        <div class="auto-route-form-actions">
                            <el-button
                                :icon="Check"
                                type="primary"
                                size="small"
                                :disabled="!canAutoGenerateRoutes"
                                :loading="autoRouteGenerationLoading"
                                @click="autoGenerateStationRoutes"
                            >
                                {{ t('routeDesign.autoRoute.actions.generate') }}
                            </el-button>
                            <el-button
                                :icon="Close"
                                size="small"
                                :disabled="autoRouteGenerationLoading"
                                @click="clearAutoRouteGenerateForm"
                            >
                                {{ t('routeDesign.autoRoute.actions.clear') }}
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
                            <el-tooltip :content="t('routeDesign.routeEnd.actions.batchDelete')" placement="top">
                                <el-button
                                    :icon="Delete"
                                    circle
                                    size="small"
                                    type="danger"
                                    plain
                                    :disabled="!canBatchDeleteRouteEnds"
                                    @click="deleteSelectedRouteEnds"
                                />
                            </el-tooltip>
                            <el-popover
                                placement="bottom-end"
                                trigger="click"
                                width="260"
                                popper-class="route-end-filter-popover"
                            >
                                <template #reference>
                                    <el-button
                                        :icon="Filter"
                                        circle
                                        size="small"
                                        :type="routeEndFiltersActive ? 'primary' : 'default'"
                                        :title="t('routeDesign.routeEnd.actions.filter')"
                                    />
                                </template>
                                <div class="route-end-filter-panel">
                                    <el-select
                                        v-model="selectedRouteEndTypeFilters"
                                        multiple
                                        filterable
                                        clearable
                                        collapse-tags
                                        collapse-tags-tooltip
                                        :reserve-keyword="false"
                                        size="small"
                                        class="route-end-filter-control"
                                        :placeholder="t('routeDesign.routeEnd.filter.type')"
                                    >
                                        <el-option
                                            v-for="option in routeEndTypeFilterOptions"
                                            :key="`route-end-filter-type-${option.id}`"
                                            :label="option.name"
                                            :value="option.id"
                                        />
                                    </el-select>
                                    <el-button
                                        :icon="Close"
                                        size="small"
                                        class="route-end-filter-clear"
                                        :disabled="!routeEndFiltersActive"
                                        @click="clearRouteEndFilters"
                                    >
                                        {{ t('routeDesign.routeEnd.actions.clearFilters') }}
                                    </el-button>
                                </div>
                            </el-popover>
                            <el-tooltip :content="t('routeDesign.routeEnd.actions.autoConfigure')" placement="top">
                                <el-button
                                    :icon="MagicStick"
                                    circle
                                    size="small"
                                    :disabled="!canEditRouteEnds || loadingRouteEnds || savingRouteEnd"
                                    @click="autoConfigureRouteEnds"
                                />
                            </el-tooltip>
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

                    <div
                        ref="routeEndContentRef"
                        class="route-end-content"
                        :class="{ 'is-stack-resizing': isRouteEndStackResizing }"
                        :style="routeEndContentStyle"
                    >
                        <div class="route-end-table-wrap">
                            <el-table
                                ref="routeEndTableRef"
                                :data="filteredRouteEnds"
                                size="small"
                                height="100%"
                                row-key="id"
                                highlight-current-row
                                :current-row-key="selectedRouteEndId"
                                :empty-text="routeEndTableEmptyText"
                                @row-click="selectRouteEnd"
                                @selection-change="handleRouteEndSelectionChange"
                            >
                                <el-table-column type="selection" width="42" />
                                <el-table-column prop="id" :label="t('routeDesign.routeEnd.fields.id')" min-width="120" show-overflow-tooltip />
                                <el-table-column prop="type" :label="t('routeDesign.routeEnd.fields.type')" min-width="116" show-overflow-tooltip>
                                    <template #default="{ row }">
                                        {{ getRouteEndTypeLabel(row.type) }}
                                    </template>
                                </el-table-column>
                                <el-table-column prop="bindingNodeID" :label="t('routeDesign.routeEnd.fields.bindingNodeID')" width="96" show-overflow-tooltip />
                                <el-table-column prop="segmentTag" :label="t('routeDesign.routeEnd.fields.segmentTag')" min-width="100" show-overflow-tooltip />
                                <el-table-column prop="sidingTag" :label="t('routeDesign.routeEnd.fields.sidingTag')" min-width="96" show-overflow-tooltip />
                            </el-table>
                        </div>

                        <div
                            class="route-end-stack-resizer"
                            role="separator"
                            aria-orientation="horizontal"
                            @mousedown="startRouteEndStackResize"
                            @dblclick="resetRouteEndStackResize"
                        />

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
                                <el-form-item :label="t('routeDesign.routeEnd.fields.id')">
                                    <el-input
                                        v-model="routeEndForm.id"
                                        :placeholder="t('routeDesign.routeEnd.placeholders.autoId')"
                                        disabled
                                    />
                                </el-form-item>
                                <el-form-item :label="t('routeDesign.routeEnd.fields.bindingNodeID')" required>
                                    <div class="route-end-binding-control">
                                        <el-input
                                            v-model="routeEndForm.bindingNodeID"
                                            disabled
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
                                        default-first-option
                                        clearable
                                        :disabled="!canEditRouteEnds || savingRouteEnd"
                                        class="route-end-full-control"
                                    >
                                        <el-option
                                            v-for="option in routeEndTypeOptions"
                                            :key="option.value"
                                            :label="t(option.labelKey)"
                                            :value="option.value"
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
import { Aim, Check, Close, Delete, Filter, MagicStick, Plus, Refresh } from '@element-plus/icons-vue'
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
    description: string
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

type StationRouteObjectListField = 'nodeList' | 'linkList' | 'switchList' | 'cellList' | 'signalList'
type StationRouteTagListField = 'allowanceTags' | 'forbiddenTags'
type StationRouteListField = StationRouteObjectListField | StationRouteTagListField
type StationRouteFilterField = 'types' | 'startNodeIds' | 'endNodeIds' | 'nodeIds' | 'linkIds' | 'cellIds' | 'switchIds' | 'signalIds'
type StationRouteObjectFilterField = Exclude<StationRouteFilterField, 'types'>

interface RouteListFieldControl {
    field: StationRouteListField
    labelKey: string
    placeholderKey: string
    allowCreate: boolean
}

interface RouteListSelectOption {
    id: string
    name: string
}

interface RouteEndTypeOption {
    value: string
    labelKey: string
}

interface RouteFilterControl {
    field: StationRouteObjectFilterField
    placeholderKey: string
    optionField: StationRouteObjectListField
}

interface RouteEndAutoSource {
    id: string
    name: string
    type: string
    bindingNodeID: string
}

interface RouteEndAutoCandidate {
    bindingNodeID: string
    type: string
    segmentTag: string
    sidingTag: string
    sourceId: string
    sourceName: string
    sourceKind: 'signal' | 'bufferStop'
}

interface RouteEndAutoPlan {
    candidates: RouteEndAutoCandidate[]
    scanned: number
    skippedExisting: number
    skippedDuplicate: number
    skippedUnbound: number
}

type StationRouteFilters = Record<StationRouteFilterField, string[]>
type RouteObjectOptionMap = Record<StationRouteObjectListField, RouteListSelectOption[]>
type RouteListFilterQueryMap = Record<StationRouteListField, string>

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
type AutoRouteNodePickStage = 'none' | 'start' | 'end'

const props = withDefaults(defineProps<{
    selectedInstanceId?: string | null
}>(), {
    selectedInstanceId: '',
})

const { t } = useI18n()

const stationLayoutEditorRef = ref<any>(null)
const splitContainerRef = ref<HTMLElement | null>(null)
const stationRouteContentRef = ref<HTMLElement | null>(null)
const routeEndContentRef = ref<HTMLElement | null>(null)
const routeEndTableRef = ref<any>(null)
const currentStationSchemeId = ref('')
const loadingStationSchemes = ref(false)
const loadingData = ref(false)
const loadingRouteEnds = ref(false)
const savingRouteEnd = ref(false)
const loadingRoutes = ref(false)
const savingRoute = ref(false)
const generatingRouteDescription = ref(false)
const routeSearchLoading = ref(false)
const stationSchemeOptions = ref<StationSchemeOption[]>([])
const layoutDisplayStyles = ref<Record<string, unknown>>({})
const layoutCells = ref<any[]>([])
const layoutSignals = ref<RouteEndAutoSource[]>([])
const layoutBufferStops = ref<RouteEndAutoSource[]>([])
const layoutGridSpacing = ref(20)
const layoutScaleX = ref(1)
const layoutScaleY = ref(1)
const showLayoutGrid = ref(true)
const showLayoutNodes = ref(true)
const showLayoutCurveArc = ref(true)
const showLayoutCellNames = ref(false)
const routeObjectOptions = ref<RouteObjectOptionMap>(createEmptyRouteObjectOptions())
const routeListFilterQueries = ref<RouteListFilterQueryMap>(createEmptyRouteListFilterQueries())
const leftPaneWidth = ref(0)
const isResizing = ref(false)
const stationRouteStackListHeight = ref(0)
const isStationRouteStackResizing = ref(false)
const routeEndStackListHeight = ref(0)
const isRouteEndStackResizing = ref(false)
const showStationRouteCard = ref(true)
const showRouteEndCard = ref(true)
const showAutoRouteGenerateCard = ref(false)
const autoRoutePickStage = ref<AutoRouteNodePickStage>('none')
const autoRouteStartNodeIds = ref<string[]>([])
const autoRouteEndNodeIds = ref<string[]>([])
const autoRouteGenerationLoading = ref(false)
const autoRouteGenerationStatus = ref('')
const stationRoutes = ref<StationRoute[]>([])
const selectedRouteId = ref('')
const selectedRouteSelectionIds = ref<string[]>([])
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
    description: '',
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
const routeFilters = ref<StationRouteFilters>(createEmptyRouteFilters())
const routeEnds = ref<StationRouteEnd[]>([])
const selectedRouteEndId = ref('')
const selectedRouteEndSelectionIds = ref<string[]>([])
const selectedRouteEndTypeFilters = ref<string[]>([])
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
const routeEndTypeOptions: RouteEndTypeOption[] = [
    { value: 'StationEntrance', labelKey: 'routeDesign.routeEnd.types.stationEntrance' },
    { value: 'StationExit', labelKey: 'routeDesign.routeEnd.types.stationExit' },
    { value: 'StationEntranceAndExit', labelKey: 'routeDesign.routeEnd.types.stationEntranceAndExit' },
    { value: 'DepartureSignal', labelKey: 'routeDesign.routeEnd.types.departureSignal' },
    { value: 'ShuntingSignal', labelKey: 'routeDesign.routeEnd.types.shuntingSignal' },
    { value: 'LocomotiveDepot', labelKey: 'routeDesign.routeEnd.types.locomotiveDepot' },
    { value: 'LocomotiveWaitingLine', labelKey: 'routeDesign.routeEnd.types.locomotiveWaitingLine' },
    { value: 'bufferStop', labelKey: 'routeDesign.routeEnd.types.bufferStop' },
    { value: 'Others', labelKey: 'routeDesign.routeEnd.types.others' },
]
const routeTypeOptions = [
    'Arrival',
    'Departure',
    'Shunting',
    'Locomotive',
]
const routeHighlightColors = {
    arrival: '#ef4444',
    departure: '#2563eb',
    locomotive: '#16a34a',
    shunting: '#facc15',
}
const routeListFieldControls: RouteListFieldControl[] = [
    {
        field: 'nodeList',
        labelKey: 'routeDesign.stationRoute.fields.nodeList',
        placeholderKey: 'routeDesign.stationRoute.placeholders.selectRouteItems',
        allowCreate: false,
    },
    {
        field: 'linkList',
        labelKey: 'routeDesign.stationRoute.fields.linkList',
        placeholderKey: 'routeDesign.stationRoute.placeholders.selectRouteItems',
        allowCreate: false,
    },
    {
        field: 'switchList',
        labelKey: 'routeDesign.stationRoute.fields.switchList',
        placeholderKey: 'routeDesign.stationRoute.placeholders.selectRouteItems',
        allowCreate: false,
    },
    {
        field: 'cellList',
        labelKey: 'routeDesign.stationRoute.fields.cellList',
        placeholderKey: 'routeDesign.stationRoute.placeholders.selectRouteItems',
        allowCreate: false,
    },
    {
        field: 'signalList',
        labelKey: 'routeDesign.stationRoute.fields.signalList',
        placeholderKey: 'routeDesign.stationRoute.placeholders.selectRouteItems',
        allowCreate: false,
    },
    {
        field: 'allowanceTags',
        labelKey: 'routeDesign.stationRoute.fields.allowanceTags',
        placeholderKey: 'routeDesign.stationRoute.placeholders.selectRouteTags',
        allowCreate: true,
    },
    {
        field: 'forbiddenTags',
        labelKey: 'routeDesign.stationRoute.fields.forbiddenTags',
        placeholderKey: 'routeDesign.stationRoute.placeholders.selectRouteTags',
        allowCreate: true,
    },
]
const routeFilterFieldControls: RouteFilterControl[] = [
    {
        field: 'startNodeIds',
        placeholderKey: 'routeDesign.stationRoute.filter.startNode',
        optionField: 'nodeList',
    },
    {
        field: 'endNodeIds',
        placeholderKey: 'routeDesign.stationRoute.filter.endNode',
        optionField: 'nodeList',
    },
    {
        field: 'nodeIds',
        placeholderKey: 'routeDesign.stationRoute.filter.node',
        optionField: 'nodeList',
    },
    {
        field: 'linkIds',
        placeholderKey: 'routeDesign.stationRoute.filter.link',
        optionField: 'linkList',
    },
    {
        field: 'cellIds',
        placeholderKey: 'routeDesign.stationRoute.filter.cell',
        optionField: 'cellList',
    },
    {
        field: 'switchIds',
        placeholderKey: 'routeDesign.stationRoute.filter.switch',
        optionField: 'switchList',
    },
    {
        field: 'signalIds',
        placeholderKey: 'routeDesign.stationRoute.filter.signal',
        optionField: 'signalList',
    },
]

const selectedInstanceId = computed(() => props.selectedInstanceId || '')
const leftPaneStyle = computed(() => (
    leftPaneWidth.value > 0
        ? { flexBasis: `${leftPaneWidth.value}px` }
        : { flexBasis: '64%' }
))
const stationRouteContentStyle = computed((): Record<string, string> => {
    if (stationRouteStackListHeight.value <= 0) return {}

    return { '--station-route-list-height': `${stationRouteStackListHeight.value}px` }
})
const routeEndContentStyle = computed((): Record<string, string> => {
    if (routeEndStackListHeight.value <= 0) return {}

    return { '--route-end-list-height': `${routeEndStackListHeight.value}px` }
})
const canLoadRoutes = computed(() => Boolean(selectedInstanceId.value && currentStationSchemeId.value.trim()))
const canEditRoutes = computed(() => canLoadRoutes.value && !loadingData.value)
const canLoadRouteEnds = computed(() => Boolean(selectedInstanceId.value && currentStationSchemeId.value.trim()))
const canEditRouteEnds = computed(() => canLoadRouteEnds.value && !loadingData.value)
const autoRoutePairCount = computed(() => autoRouteStartNodeIds.value.length * autoRouteEndNodeIds.value.length)
const canAutoGenerateRoutes = computed(() => (
    canEditRoutes.value &&
    !autoRouteGenerationLoading.value &&
    autoRouteStartNodeIds.value.length > 0 &&
    autoRouteEndNodeIds.value.length > 0
))
const canBatchDeleteRoutes = computed(() => (
    canEditRoutes.value &&
    !savingRoute.value &&
    selectedRouteSelectionIds.value.length > 0
))
const canBatchDeleteRouteEnds = computed(() => (
    canEditRouteEnds.value &&
    !savingRouteEnd.value &&
    selectedRouteEndSelectionIds.value.length > 0
))
const selectedStationRoute = computed(() => (
    stationRoutes.value.find((item) => item.id === selectedRouteId.value) || null
))
const routeFilterTypeOptions = computed<RouteListSelectOption[]>(() => (
    normalizeRouteListValues([
        ...routeTypeOptions,
        ...stationRoutes.value.map((route) => route.type),
        routeForm.value.type,
    ]).map((id) => ({ id, name: id }))
))
const autoRouteNodeOptions = computed<RouteListSelectOption[]>(() => {
    const optionsById = new Map<string, RouteListSelectOption>()

    for (const option of routeObjectOptions.value.nodeList) {
        if (!option.id || optionsById.has(option.id)) continue
        optionsById.set(option.id, option)
    }

    for (const id of normalizeRouteListValues([
        ...autoRouteStartNodeIds.value,
        ...autoRouteEndNodeIds.value,
    ])) {
        if (optionsById.has(id)) continue
        optionsById.set(id, getRouteListFallbackOption(id))
    }

    return sortRouteListOptions(Array.from(optionsById.values()))
})
const routeFiltersActive = computed(() => (
    Object.values(routeFilters.value).some((values) => values.length > 0)
))
const filteredStationRoutes = computed(() => (
    stationRoutes.value.filter((route) => routeMatchesFilters(route))
))
const routeEndFiltersActive = computed(() => selectedRouteEndTypeFilters.value.length > 0)
const filteredRouteEnds = computed(() => (
    routeEnds.value.filter((routeEnd) => routeEndMatchesFilters(routeEnd))
))
const routeEndTypeFilterOptions = computed<RouteListSelectOption[]>(() => {
    const optionsById = new Map<string, RouteListSelectOption>()

    for (const option of routeEndTypeOptions) {
        optionsById.set(option.value, {
            id: option.value,
            name: t(option.labelKey),
        })
    }

    for (const type of normalizeRouteListValues([
        ...routeEnds.value.map((routeEnd) => routeEnd.type),
        ...selectedRouteEndTypeFilters.value,
    ])) {
        if (optionsById.has(type)) continue
        optionsById.set(type, { id: type, name: getRouteEndTypeLabel(type) || type })
    }

    return Array.from(optionsById.values())
})
const stationRouteTableEmptyText = computed(() => (
    routeFiltersActive.value
        ? t('routeDesign.stationRoute.filter.empty')
        : t('routeDesign.stationRoute.empty')
))
const routeEndTableEmptyText = computed(() => (
    routeEndFiltersActive.value
        ? t('routeDesign.routeEnd.filter.empty')
        : t('routeDesign.routeEnd.empty')
))
const stationRouteListSummary = computed(() => (
    selectedRouteSelectionIds.value.length > 0
        ? t('routeDesign.stationRoute.selection.count', {
            selected: selectedRouteSelectionIds.value.length,
            total: stationRoutes.value.length,
        })
        : routeFiltersActive.value
            ? t('routeDesign.stationRoute.filter.count', {
                filtered: filteredStationRoutes.value.length,
                total: stationRoutes.value.length,
            })
            : t('routeDesign.stationRoute.count', { count: stationRoutes.value.length })
))
const routeNodePickTarget = computed(() => {
    if (routeNodePickStage.value === 'start') return 'stationRouteStartNode'
    if (routeNodePickStage.value === 'end') return 'stationRouteEndNode'
    return ''
})
const selectedRouteEnd = computed(() => (
    routeEnds.value.find((item) => item.id === selectedRouteEndId.value) || null
))
const routeEndPickTarget = computed(() => {
    if (routeEndPickingNode.value) return 'stationRouteEndBindingNode'
    if (showRouteEndCard.value) return 'stationRouteEndBoundNode'
    return ''
})
const autoRoutePickTarget = computed(() => {
    if (autoRoutePickStage.value === 'start') return 'autoRouteStartNode'
    if (autoRoutePickStage.value === 'end') return 'autoRouteEndNode'
    return ''
})
const routePickTarget = computed(() => routeNodePickTarget.value || autoRoutePickTarget.value || routeEndPickTarget.value)
const selectedRouteCandidate = computed(() => (
    routeSearchCandidates.value.find((item) => item.index === selectedRouteCandidateIndex.value) || null
))
const highlightedRoutePathNodeIds = computed(() => {
    const candidate = routeSearchDialogVisible.value ? selectedRouteCandidate.value : null
    const routeNodeIds = normalizeRouteListValues(candidate?.nodeIds || parseRouteIdText(routeForm.value.nodeList))
    if (routeNodeIds.length > 0) return routeNodeIds

    const startNodeID = routeForm.value.startNodeID || selectedStationRoute.value?.startNodeID || ''
    const endNodeID = routeForm.value.endNodeID || selectedStationRoute.value?.endNodeID || ''
    return normalizeRouteListValues([startNodeID, endNodeID])
})
const highlightedRouteNodeIds = computed(() => {
    const ids = new Set<string>()
    for (const id of highlightedRoutePathNodeIds.value) {
        if (id) ids.add(id)
    }

    const startNodeID = routeForm.value.startNodeID || selectedStationRoute.value?.startNodeID || ''
    const endNodeID = routeForm.value.endNodeID || selectedStationRoute.value?.endNodeID || ''
    if (startNodeID) ids.add(startNodeID)
    if (endNodeID) ids.add(endNodeID)

    const routeEndNodeId = routeEndForm.value.bindingNodeID || selectedRouteEnd.value?.bindingNodeID || ''
    if (routeEndNodeId) ids.add(routeEndNodeId)

    for (const id of [...autoRouteStartNodeIds.value, ...autoRouteEndNodeIds.value]) {
        if (id) ids.add(id)
    }

    return Array.from(ids)
})
const highlightedRouteArrowNodeIds = computed(() => highlightedRoutePathNodeIds.value)
const highlightedRouteLinkIds = computed(() => {
    const candidate = routeSearchDialogVisible.value ? selectedRouteCandidate.value : null
    return candidate?.linkIds || parseRouteIdText(routeForm.value.linkList)
})
const highlightedStationRouteType = computed(() => (
    routeForm.value.type ||
    selectedStationRoute.value?.type ||
    (
        highlightedRoutePathNodeIds.value.length >= 2
            ? getAutoGeneratedRouteType(
                highlightedRoutePathNodeIds.value[0] || '',
                highlightedRoutePathNodeIds.value[highlightedRoutePathNodeIds.value.length - 1] || ''
            )
            : ''
    )
))
const highlightedRouteColor = computed(() => getStationRouteHighlightColor(highlightedStationRouteType.value))
const highlightedRouteArrowVisible = computed(() => highlightedRouteArrowNodeIds.value.length >= 2)
const stationRouteHeaderText = computed(() => {
    if (routeNodePickStage.value === 'start') return t('routeDesign.stationRoute.messages.pickStart')
    if (routeNodePickStage.value === 'end') return t('routeDesign.stationRoute.messages.pickEnd')
    if (routeFiltersActive.value) {
        return t('routeDesign.stationRoute.filter.count', {
            filtered: filteredStationRoutes.value.length,
            total: stationRoutes.value.length,
        })
    }
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
    !generatingRouteDescription.value &&
    Boolean(routeForm.value.startNodeID.trim()) &&
    Boolean(routeForm.value.endNodeID.trim()) &&
    (routeEditMode.value === 'create' || Boolean(routeForm.value.id.trim()))
))
const routeTagOptions = computed<Record<StationRouteTagListField, RouteListSelectOption[]>>(() => ({
    allowanceTags: buildRouteTagOptions('allowanceTags'),
    forbiddenTags: buildRouteTagOptions('forbiddenTags'),
}))
const routeSearchDialogSubtitle = computed(() => (
    t('routeDesign.stationRoute.searchDialog.count', { count: routeSearchCandidates.value.length })
))
const visibleRoutePanelCount = computed(() => (
    Number(showStationRouteCard.value) +
    Number(showAutoRouteGenerateCard.value) +
    Number(showRouteEndCard.value)
))
const autoRouteGenerateHeaderText = computed(() => {
    if (autoRoutePickStage.value === 'start') return t('routeDesign.autoRoute.messages.pickStart')
    if (autoRoutePickStage.value === 'end') return t('routeDesign.autoRoute.messages.pickEnd')
    return t('routeDesign.autoRoute.count', {
        start: autoRouteStartNodeIds.value.length,
        end: autoRouteEndNodeIds.value.length,
        pairs: autoRoutePairCount.value,
    })
})
const routeEndHeaderText = computed(() => (
    routeEndPickingNode.value
        ? t('routeDesign.routeEnd.messages.pickNode')
        : routeEndFiltersActive.value
            ? t('routeDesign.routeEnd.filter.count', {
                filtered: filteredRouteEnds.value.length,
                total: routeEnds.value.length,
            })
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
let previousStationRouteStackBodyCursor = ''
let previousStationRouteStackBodyUserSelect = ''
let previousRouteEndStackBodyCursor = ''
let previousRouteEndStackBodyUserSelect = ''

function readString(source: any, ...keys: string[]): string {
    if (!source || typeof source !== 'object') return ''

    for (const key of keys) {
        const value = source[key]
        if (value !== undefined && value !== null) return String(value)
    }

    return ''
}

function normalizeStationRouteType(type: string): string {
    return String(type || '').trim().replace(/\s+/g, '').toLowerCase()
}

function getStationRouteHighlightColor(type: string): string {
    const normalizedType = normalizeStationRouteType(type)
    if (normalizedType === 'arrival' || normalizedType === '接车' || normalizedType === '接车进路') {
        return routeHighlightColors.arrival
    }
    if (normalizedType === 'departure' || normalizedType === '发车' || normalizedType === '发车进路') {
        return routeHighlightColors.departure
    }
    if (
        normalizedType === 'locomotive' ||
        normalizedType === '机车出入段' ||
        normalizedType === '机车出入段进路' ||
        normalizedType === '机车走行'
    ) {
        return routeHighlightColors.locomotive
    }
    if (normalizedType === 'shunting' || normalizedType === '调车' || normalizedType === '调车进路') {
        return routeHighlightColors.shunting
    }

    return routeHighlightColors.shunting
}

function createEmptyRouteObjectOptions(): RouteObjectOptionMap {
    return {
        nodeList: [],
        linkList: [],
        switchList: [],
        cellList: [],
        signalList: [],
    }
}

function createEmptyRouteListFilterQueries(): RouteListFilterQueryMap {
    return {
        nodeList: '',
        linkList: '',
        switchList: '',
        cellList: '',
        signalList: '',
        allowanceTags: '',
        forbiddenTags: '',
    }
}

function createEmptyRouteFilters(): StationRouteFilters {
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

function isRouteObjectListField(field: StationRouteListField): field is StationRouteObjectListField {
    return field !== 'allowanceTags' && field !== 'forbiddenTags'
}

function normalizeRouteListOption(item: any): RouteListSelectOption | null {
    const id = readString(item, 'id', 'ID').trim()
    if (!id) return null

    const name = readString(item, 'name', 'Name').trim() || id
    return { id, name }
}

function getRouteEndTypeLabel(type: string) {
    const normalizedType = type.trim()
    if (!normalizedType) return ''

    const option = routeEndTypeOptions.find((item) => item.value === normalizedType)
    return option ? t(option.labelKey) : normalizedType
}

function sortRouteListOptions(options: RouteListSelectOption[]): RouteListSelectOption[] {
    return [...options].sort((left, right) => (
        left.name.localeCompare(right.name, undefined, { numeric: true, sensitivity: 'base' }) ||
        left.id.localeCompare(right.id, undefined, { numeric: true, sensitivity: 'base' })
    ))
}

function buildRouteListOptions(...sources: any[]): RouteListSelectOption[] {
    const optionsById = new Map<string, RouteListSelectOption>()

    for (const source of sources) {
        if (!Array.isArray(source)) continue

        for (const item of source) {
            const option = normalizeRouteListOption(item)
            if (!option || optionsById.has(option.id)) continue
            optionsById.set(option.id, option)
        }
    }

    return sortRouteListOptions(Array.from(optionsById.values()))
}

function buildRouteObjectOptions(layoutData: any): RouteObjectOptionMap {
    return {
        nodeList: buildRouteListOptions(layoutData?.nodes),
        linkList: buildRouteListOptions(layoutData?.tracks, layoutData?.links),
        switchList: buildRouteListOptions(layoutData?.switches),
        cellList: buildRouteListOptions(layoutData?.cells),
        signalList: buildRouteListOptions(layoutData?.signals),
    }
}

function normalizeRouteEndAutoSource(item: any): RouteEndAutoSource | null {
    const id = readString(item, 'id', 'ID').trim()
    const bindingNodeID = readString(item, 'bindingNodeID', 'BindingNodeID', 'nodeID', 'NodeID').trim()
    const type = readString(item, 'type', 'Type', 'signalType', 'SignalType').trim()
    const name = readString(item, 'name', 'Name').trim()
    if (!id && !bindingNodeID && !type) return null

    return { id, name, type, bindingNodeID }
}

function getLayoutRouteEndAutoSources(layoutData: any, key: 'signals' | 'bufferStops'): RouteEndAutoSource[] {
    const source = Array.isArray(layoutData?.[key]) ? layoutData[key] : []
    return source
        .map((item: any) => normalizeRouteEndAutoSource(item))
        .filter((item: RouteEndAutoSource | null): item is RouteEndAutoSource => item !== null)
}

function normalizeBooleanDisplayValue(value: unknown, fallback: boolean) {
    if (typeof value === 'boolean') return value
    if (typeof value === 'number') return value !== 0
    if (typeof value === 'string') {
        const normalizedValue = value.trim().toLowerCase()
        if (['true', '1', 'yes', 'y'].includes(normalizedValue)) return true
        if (['false', '0', 'no', 'n'].includes(normalizedValue)) return false
    }

    return fallback
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

function getLayoutGridVisible(layoutData: any) {
    const gridSettings = getLayoutGridSettings(layoutData)
    return normalizeBooleanDisplayValue(gridSettings.showGrid ?? gridSettings.ShowGrid, true)
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

function normalizeRouteListValues(values: unknown): string[] {
    const result: string[] = []
    const seen = new Set<string>()
    const source = Array.isArray(values) ? values : []

    for (const value of source) {
        const id = String(value ?? '').trim()
        if (!id || seen.has(id)) continue

        seen.add(id)
        result.push(id)
    }

    return result
}

function getRouteListValue(field: StationRouteListField): string[] {
    return parseRouteIdText(routeForm.value[field])
}

function setRouteListValue(field: StationRouteListField, values: unknown) {
    routeForm.value[field] = serializeRouteIdList(normalizeRouteListValues(values))
}

function buildRouteTagOptions(field: StationRouteTagListField): RouteListSelectOption[] {
    const values = [
        ...stationRoutes.value.flatMap((route) => parseRouteIdText(route[field])),
        ...parseRouteIdText(routeForm.value[field]),
    ]

    return normalizeRouteListValues(values).map((id) => ({ id, name: id }))
}

function getRouteListBaseOptions(field: StationRouteListField): RouteListSelectOption[] {
    if (isRouteObjectListField(field)) return routeObjectOptions.value[field]
    return routeTagOptions.value[field]
}

function getRouteListFallbackOption(id: string): RouteListSelectOption {
    return { id, name: id }
}

function optionMatchesRouteListQuery(option: RouteListSelectOption, query: string): boolean {
    const normalizedQuery = query.trim().toLocaleLowerCase()
    if (!normalizedQuery) return true

    return option.name.toLocaleLowerCase().includes(normalizedQuery) ||
        option.id.toLocaleLowerCase().includes(normalizedQuery)
}

function getRouteListSelectOptions(field: StationRouteListField): RouteListSelectOption[] {
    const selectedIds = getRouteListValue(field)
    const selectedIdSet = new Set(selectedIds)
    const optionsById = new Map<string, RouteListSelectOption>()

    for (const option of getRouteListBaseOptions(field)) {
        if (!option.id || optionsById.has(option.id)) continue
        optionsById.set(option.id, option)
    }

    for (const id of selectedIds) {
        if (optionsById.has(id)) continue
        optionsById.set(id, getRouteListFallbackOption(id))
    }

    const query = routeListFilterQueries.value[field]
    return Array.from(optionsById.values()).filter((option) => (
        selectedIdSet.has(option.id) || optionMatchesRouteListQuery(option, query)
    ))
}

function setRouteListFilterQuery(field: StationRouteListField, query: string) {
    routeListFilterQueries.value[field] = query
}

function handleRouteListVisibleChange(field: StationRouteListField, visible: boolean) {
    if (visible) return

    routeListFilterQueries.value[field] = ''
}

function getRouteFilterReferencedIds(field: StationRouteObjectFilterField): string[] {
    if (field === 'startNodeIds') return stationRoutes.value.map((route) => route.startNodeID)
    if (field === 'endNodeIds') return stationRoutes.value.map((route) => route.endNodeID)
    if (field === 'nodeIds') {
        return stationRoutes.value.flatMap((route) => [
            route.startNodeID,
            route.endNodeID,
            ...parseRouteIdText(route.nodeList),
        ])
    }
    if (field === 'linkIds') return stationRoutes.value.flatMap((route) => parseRouteIdText(route.linkList))
    if (field === 'cellIds') return stationRoutes.value.flatMap((route) => parseRouteIdText(route.cellList))
    if (field === 'switchIds') return stationRoutes.value.flatMap((route) => parseRouteIdText(route.switchList))
    return stationRoutes.value.flatMap((route) => parseRouteIdText(route.signalList))
}

function getRouteFilterSelectOptions(control: RouteFilterControl): RouteListSelectOption[] {
    const optionsById = new Map<string, RouteListSelectOption>()

    for (const option of routeObjectOptions.value[control.optionField]) {
        if (!option.id || optionsById.has(option.id)) continue
        optionsById.set(option.id, option)
    }

    for (const id of normalizeRouteListValues([
        ...getRouteFilterReferencedIds(control.field),
        ...routeFilters.value[control.field],
    ])) {
        if (optionsById.has(id)) continue
        optionsById.set(id, getRouteListFallbackOption(id))
    }

    return sortRouteListOptions(Array.from(optionsById.values()))
}

function clearRouteFilters() {
    routeFilters.value = createEmptyRouteFilters()
}

function clearRouteEndFilters() {
    selectedRouteEndTypeFilters.value = []
}

function routeMatchesScalarFilter(selectedIds: string[], value: string): boolean {
    if (selectedIds.length === 0) return true

    const normalizedValue = String(value || '').trim()
    return selectedIds.includes(normalizedValue)
}

function routeMatchesListFilter(selectedIds: string[], routeIds: string[]): boolean {
    if (selectedIds.length === 0) return true

    const routeIdSet = new Set(normalizeRouteListValues(routeIds))
    return selectedIds.some((id) => routeIdSet.has(id))
}

function routeMatchesFilters(route: StationRoute): boolean {
    const filters = routeFilters.value
    if (!routeMatchesScalarFilter(filters.types, route.type)) return false
    if (!routeMatchesScalarFilter(filters.startNodeIds, route.startNodeID)) return false
    if (!routeMatchesScalarFilter(filters.endNodeIds, route.endNodeID)) return false

    if (!routeMatchesListFilter(filters.nodeIds, [
        route.startNodeID,
        route.endNodeID,
        ...parseRouteIdText(route.nodeList),
    ])) {
        return false
    }
    if (!routeMatchesListFilter(filters.linkIds, parseRouteIdText(route.linkList))) return false
    if (!routeMatchesListFilter(filters.cellIds, parseRouteIdText(route.cellList))) return false
    if (!routeMatchesListFilter(filters.switchIds, parseRouteIdText(route.switchList))) return false
    return routeMatchesListFilter(filters.signalIds, parseRouteIdText(route.signalList))
}

function routeEndMatchesFilters(routeEnd: StationRouteEnd): boolean {
    return routeMatchesScalarFilter(selectedRouteEndTypeFilters.value, routeEnd.type)
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

function normalizeEquipmentTypeKey(value: string): string {
    return String(value || '').trim().replace(/[\s_-]+/g, '').toLowerCase()
}

function getAutoRouteEndTypeForSignal(signalType: string): string {
    const key = normalizeEquipmentTypeKey(signalType)
    if (!key) return ''
    if (key === 'home' || key === '进站信号机' || key.startsWith('homesignal')) {
        return 'StationEntranceAndExit'
    }
    if (key === 'departure' || key === '出站信号机' || key.startsWith('departuresignal')) {
        return 'DepartureSignal'
    }
    if (key === 'shunting' || key === '调车信号机' || key.startsWith('shuntingsignal')) {
        return 'ShuntingSignal'
    }

    return ''
}

function getAutoRouteEndSidingTagForDepartureSignal(signalName: string): string {
    const name = String(signalName || '').trim()
    if (name.length < 2) return ''

    const directionKey = name.charAt(0).toUpperCase()
    const trackName = name.slice(1).trim()
    if (!trackName) return ''
    if (directionKey === 'X') return `${trackName}道下行`
    if (directionKey === 'S') return `${trackName}道上行`
    return ''
}

function getAutoRouteEndSegmentTagForStationEntrance(signalName: string): string {
    const name = String(signalName || '').trim()
    return name ? `${name}方向` : ''
}

function buildRouteEndAutoPlan(): RouteEndAutoPlan {
    const existingNodeIDs = new Set(routeEnds.value.map((routeEnd) => routeEnd.bindingNodeID.trim()).filter(Boolean))
    const candidateNodeIDs = new Set<string>()
    const plan: RouteEndAutoPlan = {
        candidates: [],
        scanned: 0,
        skippedExisting: 0,
        skippedDuplicate: 0,
        skippedUnbound: 0,
    }

    const addCandidate = (source: RouteEndAutoSource, type: string, sourceKind: RouteEndAutoCandidate['sourceKind']) => {
        plan.scanned++
        const bindingNodeID = source.bindingNodeID.trim()
        if (!bindingNodeID) {
            plan.skippedUnbound++
            return
        }
        if (existingNodeIDs.has(bindingNodeID)) {
            plan.skippedExisting++
            return
        }
        if (candidateNodeIDs.has(bindingNodeID)) {
            plan.skippedDuplicate++
            return
        }

        candidateNodeIDs.add(bindingNodeID)
        plan.candidates.push({
            bindingNodeID,
            type,
            segmentTag: type === 'StationEntranceAndExit'
                ? getAutoRouteEndSegmentTagForStationEntrance(source.name)
                : '',
            sidingTag: type === 'DepartureSignal'
                ? getAutoRouteEndSidingTagForDepartureSignal(source.name)
                : '',
            sourceId: source.id,
            sourceName: source.name || source.id,
            sourceKind,
        })
    }

    for (const signal of layoutSignals.value) {
        const routeEndType = getAutoRouteEndTypeForSignal(signal.type)
        if (!routeEndType) continue

        addCandidate(signal, routeEndType, 'signal')
    }

    for (const bufferStop of layoutBufferStops.value) {
        addCandidate(bufferStop, 'bufferStop', 'bufferStop')
    }

    return plan
}

function buildRouteEndPayloadFromAutoCandidate(candidate: RouteEndAutoCandidate) {
    return {
        instanceID: selectedInstanceId.value.trim(),
        stationSchemeID: currentStationSchemeId.value.trim(),
        originalID: '',
        id: '',
        bindingNodeID: candidate.bindingNodeID,
        type: candidate.type,
        segmentTag: candidate.segmentTag,
        sidingTag: candidate.sidingTag,
    }
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

function getRouteIdsTooltip(ids: string[]): string {
    if (ids.length === 0) return ''

    return ids.join(', ')
}

function createEmptyStationRouteForm(): StationRoute {
    return {
        instanceID: selectedInstanceId.value,
        stationSchemeID: currentStationSchemeId.value.trim(),
        id: '',
        type: '',
        description: '',
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
        description: readString(item, 'description', 'Description').trim(),
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

function clearAutoRouteGenerateForm() {
    autoRouteStartNodeIds.value = []
    autoRouteEndNodeIds.value = []
    autoRoutePickStage.value = 'none'
    autoRouteGenerationStatus.value = ''
}

function clearStationRoutes() {
    routeLoadVersion++
    stationRoutes.value = []
    selectedRouteId.value = ''
    selectedRouteSelectionIds.value = []
    routeOriginalId.value = ''
    routeEditMode.value = 'none'
    routeNodePickStage.value = 'none'
    routeSearchLoading.value = false
    autoRoutePickStage.value = 'none'
    autoRouteGenerationLoading.value = false
    clearAutoRouteGenerateForm()
    routeForm.value = createEmptyStationRouteForm()
    routeFilters.value = createEmptyRouteFilters()
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

function handleStationRouteSelectionChange(rows: StationRoute[]) {
    selectedRouteSelectionIds.value = normalizeRouteListValues(rows.map((row) => row.id))
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
    selectedRouteEndSelectionIds.value = []
    clearRouteEndFilters()
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

function scrollRouteEndIntoView(id: string) {
    const normalizedId = String(id || '').trim()
    if (!normalizedId) return

    void nextTick(() => {
        const row = routeEnds.value.find((item) => item.id === normalizedId)
        const table = routeEndTableRef.value
        if (!row || !table) return

        table.setCurrentRow?.(row)

        const tableElement = table.$el as HTMLElement | undefined
        const scrollWrapper = tableElement?.querySelector('.el-table__body-wrapper .el-scrollbar__wrap') as HTMLElement | null
        const currentRow = tableElement?.querySelector('.el-table__body-wrapper .el-table__row.current-row') as HTMLElement | null
        if (scrollWrapper && currentRow) {
            const padding = 8
            const rowTop = currentRow.offsetTop
            const rowBottom = rowTop + currentRow.offsetHeight
            const visibleTop = scrollWrapper.scrollTop
            const visibleBottom = visibleTop + scrollWrapper.clientHeight

            if (rowTop < visibleTop) {
                scrollWrapper.scrollTop = Math.max(0, rowTop - padding)
            } else if (rowBottom > visibleBottom) {
                scrollWrapper.scrollTop = Math.max(0, rowBottom - scrollWrapper.clientHeight + padding)
            }
            return
        }

        const rowIndex = filteredRouteEnds.value.findIndex((item) => item.id === normalizedId)
        if (rowIndex >= 0) table.setScrollTop?.(Math.max(0, rowIndex * 36 - 36))
    })
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

function selectRouteEndByBindingNodeID(nodeID: string): boolean {
    const row = getRouteEndForNode(nodeID)
    if (!row) return false

    if (routeEndFiltersActive.value && !routeEndMatchesFilters(row)) {
        const rowType = row.type.trim()
        selectedRouteEndTypeFilters.value = rowType
            ? normalizeRouteListValues([...selectedRouteEndTypeFilters.value, rowType])
            : []
    }
    selectRouteEnd(row)
    scrollRouteEndIntoView(row.id)
    return true
}

function handleRouteEndSelectionChange(rows: StationRouteEnd[]) {
    selectedRouteEndSelectionIds.value = normalizeRouteListValues(rows.map((row) => row.id))
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
        const routeIdSet = new Set(stationRoutes.value.map((route) => route.id))
        selectedRouteSelectionIds.value = selectedRouteSelectionIds.value.filter((id) => routeIdSet.has(id))
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
        const routeEndIdSet = new Set(routeEnds.value.map((routeEnd) => routeEnd.id))
        selectedRouteEndSelectionIds.value = selectedRouteEndSelectionIds.value.filter((id) => routeEndIdSet.has(id))
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

function toggleAutoRouteGenerateCard() {
    if (!canEditRoutes.value) {
        ElMessage.warning(t('routeDesign.stationRoute.messages.selectScheme'))
        return
    }

    showAutoRouteGenerateCard.value = !showAutoRouteGenerateCard.value
    if (showAutoRouteGenerateCard.value) {
        routeNodePickStage.value = 'none'
        routeEndPickingNode.value = false
        routeSearchDialogVisible.value = false
    } else {
        autoRoutePickStage.value = 'none'
    }
}

function closeAutoRouteGenerateCard() {
    showAutoRouteGenerateCard.value = false
    autoRoutePickStage.value = 'none'
}

function startAutoRouteNodePick(stage: Exclude<AutoRouteNodePickStage, 'none'>) {
    if (!canEditRoutes.value) {
        ElMessage.warning(t('routeDesign.stationRoute.messages.selectScheme'))
        return
    }

    showAutoRouteGenerateCard.value = true
    routeNodePickStage.value = 'none'
    routeEndPickingNode.value = false
    routeSearchDialogVisible.value = false
    autoRoutePickStage.value = stage
    ElMessage.info(t(stage === 'start'
        ? 'routeDesign.autoRoute.messages.pickStart'
        : 'routeDesign.autoRoute.messages.pickEnd'))
}

function addAutoRouteNode(stage: Exclude<AutoRouteNodePickStage, 'none'>, nodeId: string) {
    const target = stage === 'start' ? autoRouteStartNodeIds.value : autoRouteEndNodeIds.value
    if (target.includes(nodeId)) {
        ElMessage.warning(t('routeDesign.autoRoute.messages.duplicateNode', { nodeId }))
        return
    }

    target.push(nodeId)
    autoRouteGenerationStatus.value = ''
    ElMessage.success(t(stage === 'start'
        ? 'routeDesign.autoRoute.messages.startPicked'
        : 'routeDesign.autoRoute.messages.endPicked', { nodeId }))
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
    autoRoutePickStage.value = 'none'
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
    autoRoutePickStage.value = 'none'
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

function parseRouteNodeIdNumber(nodeID: string): number | null {
    const nodeNumber = Number(String(nodeID || '').trim())
    return Number.isInteger(nodeNumber) ? nodeNumber : null
}

function readSearchRouteRows(responseData: any): any[] {
    return Array.isArray(responseData?.routes)
        ? responseData.routes
        : Array.isArray(responseData?.Routes)
            ? responseData.Routes
            : []
}

async function fetchStationRouteCandidates(startNodeID: string, endNodeID: string): Promise<StationRouteSearchCandidate[]> {
    const instanceID = selectedInstanceId.value
    const stationSchemeID = currentStationSchemeId.value.trim()
    if (!instanceID || !stationSchemeID) {
        throw new Error(t('routeDesign.stationRoute.messages.selectScheme'))
    }

    const startNodeNumber = parseRouteNodeIdNumber(startNodeID)
    const endNodeNumber = parseRouteNodeIdNumber(endNodeID)
    if (startNodeNumber == null || endNodeNumber == null) {
        throw new Error(t('routeDesign.stationRoute.messages.nodeIdMustBeInteger'))
    }

    const response = await axios.post('/StationLayout/SearchRoutes', {
        instanceID,
        stationSchemeID,
        startNodeId: startNodeNumber,
        endNodeId: endNodeNumber,
    }, {
        params: { instanceID, stationSchemeID },
    })

    return readSearchRouteRows(response.data).map((route: any, index: number) => (
        normalizeStationRouteSearchCandidate(route, index, startNodeID, endNodeID)
    ))
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

    if (parseRouteNodeIdNumber(startNodeID) == null || parseRouteNodeIdNumber(endNodeID) == null) {
        ElMessage.warning(t('routeDesign.stationRoute.messages.nodeIdMustBeInteger'))
        return
    }

    routeSearchLoading.value = true
    clearRouteSearchCandidates()
    try {
        const candidates = await fetchStationRouteCandidates(startNodeID, endNodeID)
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

    const existingRouteId = routeOriginalId.value.trim() || selectedRouteId.value.trim() || routeForm.value.id.trim()
    const isEditingExistingRoute = routeEditMode.value === 'edit' && Boolean(existingRouteId)
    if (isEditingExistingRoute) {
        routeEditMode.value = 'edit'
        routeOriginalId.value = existingRouteId
        if (!selectedRouteId.value) selectedRouteId.value = existingRouteId
    } else {
        routeEditMode.value = 'create'
        selectedRouteId.value = ''
        routeOriginalId.value = ''
    }

    routeNodePickStage.value = 'none'
    routeForm.value = {
        ...createEmptyStationRouteForm(),
        id: routeForm.value.id || existingRouteId,
        type: routeForm.value.type,
        description: routeForm.value.description,
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

function buildStationRoutePayloadFromRoute(route: StationRoute, originalID = '') {
    return {
        instanceID: route.instanceID.trim(),
        stationSchemeID: route.stationSchemeID.trim(),
        originalID: originalID.trim(),
        id: route.id.trim(),
        type: route.type.trim(),
        description: route.description.trim(),
        nodeList: route.nodeList.trim(),
        linkList: route.linkList.trim(),
        switchList: route.switchList.trim(),
        cellList: route.cellList.trim(),
        signalList: route.signalList.trim(),
        allowanceTags: route.allowanceTags.trim(),
        forbiddenTags: route.forbiddenTags.trim(),
        startNodeID: route.startNodeID.trim(),
        endNodeID: route.endNodeID.trim(),
    }
}

function buildStationRoutePayload() {
    syncStationRouteFormScope()
    return buildStationRoutePayloadFromRoute(routeForm.value, routeOriginalId.value.trim())
}

async function generateStationRouteDescriptionText(payload: any): Promise<string> {
    const response = await axios.post('/StationLayout/GenerateStationRouteDescription', payload)
    const description = readString(response.data, 'description', 'Description').trim()
    if (!description) {
        throw new Error('Generated route description is empty.')
    }

    return description
}

async function generateStationRouteDescription() {
    if (!canEditRoutes.value) {
        ElMessage.warning(t('routeDesign.stationRoute.messages.selectScheme'))
        return
    }

    const payload = buildStationRoutePayload()
    if (!payload.type) {
        ElMessage.warning(t('routeDesign.stationRoute.messages.typeRequired'))
        return
    }

    if (!payload.startNodeID || !payload.endNodeID) {
        ElMessage.warning(t('routeDesign.stationRoute.messages.startEndRequired'))
        return
    }

    generatingRouteDescription.value = true
    try {
        routeForm.value.description = await generateStationRouteDescriptionText(payload)
        ElMessage.success(t('routeDesign.stationRoute.messages.descriptionGenerated'))
    } catch (error) {
        console.error('Failed to generate station route description:', error)
        ElMessage.error(getHttpErrorMessage(
            error,
            t('routeDesign.stationRoute.messages.descriptionGenerateFailed')
        ))
    } finally {
        generatingRouteDescription.value = false
    }
}

function getRouteEndForNode(nodeID: string): StationRouteEnd | null {
    const normalizedNodeID = String(nodeID || '').trim()
    if (!normalizedNodeID) return null

    return routeEnds.value.find((routeEnd) => routeEnd.bindingNodeID.trim() === normalizedNodeID) || null
}

function normalizeAutoRouteEndType(type: string): string {
    const key = String(type || '').trim().replace(/[\s_-]/g, '').toLowerCase()
    if (key === 'stationentrance' || key === 'entrance') return 'entrance'
    if (key === 'stationexit' || key === 'exit') return 'exit'
    if (key === 'stationentranceandexit' || key === 'entranceandexit') return 'entranceAndExit'
    if (key === 'departuresignal') return 'departureSignal'
    if (key === 'locomotivedepot') return 'locomotiveDepot'
    if (key === 'locomotivewaitingline') return 'locomotiveWaitingLine'
    return key
}

function isAutoRouteStationEntranceType(type: string): boolean {
    return type === 'entrance' || type === 'exit' || type === 'entranceAndExit'
}

function isAutoRouteLocomotiveType(type: string): boolean {
    return type === 'locomotiveDepot' || type === 'locomotiveWaitingLine'
}

function getAutoGeneratedRouteType(startNodeID: string, endNodeID: string): string {
    const startType = normalizeAutoRouteEndType(getRouteEndForNode(startNodeID)?.type || '')
    const endType = normalizeAutoRouteEndType(getRouteEndForNode(endNodeID)?.type || '')

    if (isAutoRouteLocomotiveType(startType) || isAutoRouteLocomotiveType(endType)) return 'Locomotive'
    if (isAutoRouteStationEntranceType(startType) && endType === 'departureSignal') return 'Arrival'
    if (startType === 'departureSignal' && isAutoRouteStationEntranceType(endType)) return 'Departure'
    return 'Shunting'
}

function getAutoRouteTypeDescription(routeType: string): string {
    const normalizedType = routeType.trim().toLowerCase()
    if (normalizedType === 'arrival') return t('routeDesign.autoRoute.typeDescriptions.arrival')
    if (normalizedType === 'departure') return t('routeDesign.autoRoute.typeDescriptions.departure')
    if (normalizedType === 'locomotive') return t('routeDesign.autoRoute.typeDescriptions.locomotive')
    if (normalizedType === 'shunting') return t('routeDesign.autoRoute.typeDescriptions.shunting')
    return routeType
}

function getAutoRouteEndTag(nodeID: string): string {
    const routeEnd = getRouteEndForNode(nodeID)
    if (!routeEnd) return t('routeDesign.autoRoute.messages.missingRouteEndTag')

    const tag = `${routeEnd.segmentTag}${routeEnd.sidingTag}`.trim()
    return tag || t('routeDesign.autoRoute.messages.missingRouteEndTag')
}

function buildAutoRouteFallbackDescription(startNodeID: string, endNodeID: string, routeType: string): string {
    return t('routeDesign.autoRoute.generatedDescription', {
        start: getAutoRouteEndTag(startNodeID),
        end: getAutoRouteEndTag(endNodeID),
        type: getAutoRouteTypeDescription(routeType),
    })
}

function buildAutoGeneratedStationRoute(
    candidate: StationRouteSearchCandidate,
    routeType: string,
    description = ''
): StationRoute {
    return {
        ...createEmptyStationRouteForm(),
        type: routeType,
        description,
        startNodeID: candidate.startNodeID,
        endNodeID: candidate.endNodeID,
        nodeList: candidate.nodeList,
        linkList: candidate.linkList,
        switchList: candidate.switchList,
        cellList: candidate.cellList,
        signalList: candidate.signalList,
    }
}

async function autoGenerateStationRoutes() {
    if (!canEditRoutes.value) {
        ElMessage.warning(t('routeDesign.stationRoute.messages.selectScheme'))
        return
    }

    autoRouteStartNodeIds.value = normalizeRouteListValues(autoRouteStartNodeIds.value)
    autoRouteEndNodeIds.value = normalizeRouteListValues(autoRouteEndNodeIds.value)
    if (autoRouteStartNodeIds.value.length === 0 || autoRouteEndNodeIds.value.length === 0) {
        ElMessage.warning(t('routeDesign.autoRoute.messages.startEndRequired'))
        return
    }

    const invalidNodeID = [...autoRouteStartNodeIds.value, ...autoRouteEndNodeIds.value]
        .find((nodeID) => parseRouteNodeIdNumber(nodeID) == null)
    if (invalidNodeID) {
        ElMessage.warning(t('routeDesign.stationRoute.messages.nodeIdMustBeInteger'))
        return
    }

    const pairs: Array<{ startNodeID: string; endNodeID: string }> = []
    let skipped = 0
    for (const startNodeID of autoRouteStartNodeIds.value) {
        for (const endNodeID of autoRouteEndNodeIds.value) {
            if (startNodeID === endNodeID) {
                skipped++
                continue
            }

            pairs.push({ startNodeID, endNodeID })
        }
    }

    if (pairs.length === 0) {
        ElMessage.warning(t('routeDesign.autoRoute.messages.noValidPairs'))
        return
    }

    autoRouteGenerationLoading.value = true
    autoRoutePickStage.value = 'none'
    let created = 0
    let noRoute = 0
    let failed = 0
    const createdRouteIds: string[] = []

    try {
        for (const [index, pair] of pairs.entries()) {
            autoRouteGenerationStatus.value = t('routeDesign.autoRoute.messages.generatingPair', {
                current: index + 1,
                total: pairs.length,
                start: pair.startNodeID,
                end: pair.endNodeID,
            })

            try {
                const candidates = await fetchStationRouteCandidates(pair.startNodeID, pair.endNodeID)
                const firstCandidate = candidates[0]
                if (!firstCandidate) {
                    noRoute++
                    continue
                }

                const routeType = getAutoGeneratedRouteType(pair.startNodeID, pair.endNodeID)
                const draft = buildAutoGeneratedStationRoute(firstCandidate, routeType)
                const payload = buildStationRoutePayloadFromRoute(draft)
                try {
                    payload.description = await generateStationRouteDescriptionText(payload)
                } catch (descriptionError) {
                    console.error('Failed to generate station route description for auto route:', descriptionError)
                    payload.description = buildAutoRouteFallbackDescription(pair.startNodeID, pair.endNodeID, routeType)
                }

                const response = await axios.post('/StationLayout/CreateStationRoute', payload)
                const saved = normalizeStationRoute(response.data)
                if (saved?.id) createdRouteIds.push(saved.id)
                created++
            } catch (error) {
                failed++
                console.error('Failed to auto-generate station route:', error)
            }
        }

        await loadStationRoutes()
        if (createdRouteIds[0]) selectStationRouteById(createdRouteIds[0])

        const finishMessage = t(created > 0
            ? 'routeDesign.autoRoute.messages.generateFinished'
            : 'routeDesign.autoRoute.messages.generateEmpty', {
            created,
            noRoute,
            failed,
            skipped,
        })
        autoRouteGenerationStatus.value = finishMessage
        if (created > 0) {
            ElMessage.success(finishMessage)
        } else {
            ElMessage.warning(finishMessage)
        }
    } finally {
        autoRouteGenerationLoading.value = false
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
        await deleteStationRouteById(id)
        ElMessage.success(t('routeDesign.stationRoute.messages.deleteSuccess'))
        selectedRouteId.value = ''
        selectedRouteSelectionIds.value = selectedRouteSelectionIds.value.filter((selectedId) => selectedId !== id)
        routeOriginalId.value = ''
        await loadStationRoutes()
    } catch (error) {
        console.error('Failed to delete station route:', error)
        ElMessage.error(getHttpErrorMessage(error, t('routeDesign.stationRoute.messages.deleteFailed')))
    } finally {
        savingRoute.value = false
    }
}

async function deleteStationRouteById(id: string) {
    await axios.delete('/StationLayout/DeleteStationRoute', {
        params: {
            instanceID: selectedInstanceId.value,
            stationSchemeID: currentStationSchemeId.value.trim(),
            id,
        },
    })
}

async function deleteSelectedStationRoutes() {
    const ids = normalizeRouteListValues(selectedRouteSelectionIds.value)
    if (ids.length === 0 || !canEditRoutes.value) {
        ElMessage.warning(t('routeDesign.stationRoute.messages.batchDeleteRequired'))
        return
    }

    try {
        await ElMessageBox.confirm(
            t('routeDesign.stationRoute.messages.batchDeleteConfirm', { count: ids.length }),
            t('routeDesign.stationRoute.messages.batchDeleteTitle'),
            {
                confirmButtonText: t('routeDesign.stationRoute.actions.batchDelete'),
                cancelButtonText: t('routeDesign.stationRoute.actions.cancel'),
                type: 'warning',
            }
        )
    } catch {
        return
    }

    savingRoute.value = true
    let deleted = 0
    let failed = 0
    try {
        for (const id of ids) {
            try {
                await deleteStationRouteById(id)
                deleted++
            } catch (error) {
                failed++
                console.error('Failed to delete station route in batch:', error)
            }
        }

        if (ids.includes(selectedRouteId.value)) {
            selectedRouteId.value = ''
            routeOriginalId.value = ''
        }
        selectedRouteSelectionIds.value = []
        await loadStationRoutes()

        if (failed > 0) {
            ElMessage.warning(t('routeDesign.stationRoute.messages.batchDeletePartial', { deleted, failed }))
        } else {
            ElMessage.success(t('routeDesign.stationRoute.messages.batchDeleteSuccess', { count: deleted }))
        }
    } finally {
        savingRoute.value = false
    }
}

async function autoConfigureRouteEnds() {
    if (!canEditRouteEnds.value) {
        ElMessage.warning(t('routeDesign.routeEnd.messages.selectScheme'))
        return
    }

    const plan = buildRouteEndAutoPlan()
    const skipped = plan.skippedExisting + plan.skippedDuplicate + plan.skippedUnbound
    if (plan.scanned === 0) {
        ElMessage.warning(t('routeDesign.routeEnd.messages.autoConfigureNoEquipment'))
        return
    }
    if (plan.candidates.length === 0) {
        ElMessage.warning(t('routeDesign.routeEnd.messages.autoConfigureNoCandidates', { skipped }))
        return
    }

    try {
        await ElMessageBox.confirm(
            t('routeDesign.routeEnd.messages.autoConfigureConfirm', {
                count: plan.candidates.length,
                skipped,
            }),
            t('routeDesign.routeEnd.messages.autoConfigureTitle'),
            {
                confirmButtonText: t('routeDesign.routeEnd.actions.autoConfigure'),
                cancelButtonText: t('routeDesign.routeEnd.actions.cancel'),
                type: 'warning',
            }
        )
    } catch {
        return
    }

    savingRouteEnd.value = true
    routeEndPickingNode.value = false
    routeNodePickStage.value = 'none'
    autoRoutePickStage.value = 'none'
    let created = 0
    let failed = 0
    const createdIds: string[] = []

    try {
        for (const candidate of plan.candidates) {
            try {
                const response = await axios.post('/StationLayout/CreateStationRouteEnd', buildRouteEndPayloadFromAutoCandidate(candidate))
                const saved = normalizeStationRouteEnd(response.data)
                if (saved?.id) createdIds.push(saved.id)
                created++
            } catch (error) {
                failed++
                console.error('Failed to auto-configure route end:', error)
            }
        }

        await loadRouteEnds()
        if (createdIds[0]) selectRouteEndById(createdIds[0])

        const message = t(failed > 0
            ? 'routeDesign.routeEnd.messages.autoConfigurePartial'
            : 'routeDesign.routeEnd.messages.autoConfigureSuccess', {
            created,
            failed,
            skipped,
        })
        if (failed > 0) {
            ElMessage.warning(message)
        } else {
            ElMessage.success(message)
        }
    } finally {
        savingRouteEnd.value = false
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
    autoRoutePickStage.value = 'none'
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
    autoRoutePickStage.value = 'none'
    routeSearchDialogVisible.value = false
    ElMessage.info(t('routeDesign.routeEnd.messages.pickNode'))
}

async function handleRouteNodePick(payload: RouteNodePickPayload) {
    const nodeId = readString(payload, 'nodeId', 'nodeID').trim()
    if (!nodeId) return

    if (autoRoutePickStage.value !== 'none' && payload?.target === autoRoutePickTarget.value) {
        addAutoRouteNode(autoRoutePickStage.value, nodeId)
        return
    }

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

    if (routeEndPickingNode.value && payload?.target === routeEndPickTarget.value) {
        routeEndForm.value.bindingNodeID = nodeId
        syncRouteEndFormScope()
        routeEndPickingNode.value = false
        ElMessage.success(t('routeDesign.routeEnd.messages.nodePicked', { nodeId }))
        return
    }

    if (showRouteEndCard.value && payload?.target === routeEndPickTarget.value) {
        selectRouteEndByBindingNodeID(nodeId)
    }
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
        await deleteRouteEndById(id)
        ElMessage.success(t('routeDesign.routeEnd.messages.deleteSuccess'))
        selectedRouteEndId.value = ''
        selectedRouteEndSelectionIds.value = selectedRouteEndSelectionIds.value.filter((selectedId) => selectedId !== id)
        routeEndOriginalId.value = ''
        await loadRouteEnds()
    } catch (error) {
        console.error('Failed to delete station route end:', error)
        ElMessage.error(getHttpErrorMessage(error, t('routeDesign.routeEnd.messages.deleteFailed')))
    } finally {
        savingRouteEnd.value = false
    }
}

async function deleteRouteEndById(id: string) {
    await axios.delete('/StationLayout/DeleteStationRouteEnd', {
        params: {
            instanceID: selectedInstanceId.value,
            stationSchemeID: currentStationSchemeId.value.trim(),
            id,
        },
    })
}

async function deleteSelectedRouteEnds() {
    const ids = normalizeRouteListValues(selectedRouteEndSelectionIds.value)
    if (ids.length === 0 || !canEditRouteEnds.value) {
        ElMessage.warning(t('routeDesign.routeEnd.messages.batchDeleteRequired'))
        return
    }

    try {
        await ElMessageBox.confirm(
            t('routeDesign.routeEnd.messages.batchDeleteConfirm', { count: ids.length }),
            t('routeDesign.routeEnd.messages.batchDeleteTitle'),
            {
                confirmButtonText: t('routeDesign.routeEnd.actions.batchDelete'),
                cancelButtonText: t('routeDesign.routeEnd.actions.cancel'),
                type: 'warning',
            }
        )
    } catch {
        return
    }

    savingRouteEnd.value = true
    let deleted = 0
    let failed = 0
    try {
        for (const id of ids) {
            try {
                await deleteRouteEndById(id)
                deleted++
            } catch (error) {
                failed++
                console.error('Failed to delete station route end in batch:', error)
            }
        }

        if (ids.includes(selectedRouteEndId.value)) {
            selectedRouteEndId.value = ''
            routeEndOriginalId.value = ''
        }
        selectedRouteEndSelectionIds.value = []
        await loadRouteEnds()

        if (failed > 0) {
            ElMessage.warning(t('routeDesign.routeEnd.messages.batchDeletePartial', { deleted, failed }))
        } else {
            ElMessage.success(t('routeDesign.routeEnd.messages.batchDeleteSuccess', { count: deleted }))
        }
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
    layoutCells.value = []
    layoutSignals.value = []
    layoutBufferStops.value = []
    layoutGridSpacing.value = 20
    layoutScaleX.value = 1
    layoutScaleY.value = 1
    showLayoutGrid.value = true
    showLayoutNodes.value = true
    showLayoutCurveArc.value = true
    showLayoutCellNames.value = false
    routeObjectOptions.value = createEmptyRouteObjectOptions()
    routeListFilterQueries.value = createEmptyRouteListFilterQueries()
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
        layoutCells.value = getLayoutCells(response.data)
        layoutSignals.value = getLayoutRouteEndAutoSources(response.data, 'signals')
        layoutBufferStops.value = getLayoutRouteEndAutoSources(response.data, 'bufferStops')
        layoutGridSpacing.value = getLayoutGridSpacing(response.data)
        showLayoutGrid.value = getLayoutGridVisible(response.data)
        routeObjectOptions.value = buildRouteObjectOptions(response.data)
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
    clearAutoRouteGenerateForm()
    clearRouteFilters()
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

function getStationRouteStackLimits(containerHeight: number) {
    return {
        minList: 160,
        minForm: 220,
        resizerHeight: 8,
        containerHeight,
    }
}

function clampStationRouteStackListHeight(height: number) {
    const containerHeight = stationRouteContentRef.value?.clientHeight || 0
    const { minList, minForm, resizerHeight } = getStationRouteStackLimits(containerHeight)
    if (containerHeight <= 0) return Math.max(minList, height)

    const maxList = Math.max(minList, containerHeight - minForm - resizerHeight)
    return Math.min(maxList, Math.max(minList, height))
}

function onStationRouteStackResizeMouseMove(event: MouseEvent) {
    if (!isStationRouteStackResizing.value) return

    const rect = stationRouteContentRef.value?.getBoundingClientRect()
    if (!rect) return

    stationRouteStackListHeight.value = clampStationRouteStackListHeight(event.clientY - rect.top)
}

function finishStationRouteStackResize() {
    if (!isStationRouteStackResizing.value) return

    isStationRouteStackResizing.value = false
    window.removeEventListener('mousemove', onStationRouteStackResizeMouseMove)
    window.removeEventListener('mouseup', finishStationRouteStackResize)
    document.body.style.cursor = previousStationRouteStackBodyCursor
    document.body.style.userSelect = previousStationRouteStackBodyUserSelect
}

function startStationRouteStackResize(event: MouseEvent) {
    event.preventDefault()
    isStationRouteStackResizing.value = true
    previousStationRouteStackBodyCursor = document.body.style.cursor
    previousStationRouteStackBodyUserSelect = document.body.style.userSelect
    document.body.style.cursor = 'row-resize'
    document.body.style.userSelect = 'none'
    window.addEventListener('mousemove', onStationRouteStackResizeMouseMove)
    window.addEventListener('mouseup', finishStationRouteStackResize)
}

function resetStationRouteStackResize() {
    stationRouteStackListHeight.value = 0
}

function getRouteEndStackLimits(containerHeight: number) {
    return {
        minList: 118,
        minForm: 220,
        resizerHeight: 8,
        containerHeight,
    }
}

function clampRouteEndStackListHeight(height: number) {
    const containerHeight = routeEndContentRef.value?.clientHeight || 0
    const { minList, minForm, resizerHeight } = getRouteEndStackLimits(containerHeight)
    if (containerHeight <= 0) return Math.max(minList, height)

    const maxList = Math.max(minList, containerHeight - minForm - resizerHeight)
    return Math.min(maxList, Math.max(minList, height))
}

function onRouteEndStackResizeMouseMove(event: MouseEvent) {
    if (!isRouteEndStackResizing.value) return

    const rect = routeEndContentRef.value?.getBoundingClientRect()
    if (!rect) return

    routeEndStackListHeight.value = clampRouteEndStackListHeight(event.clientY - rect.top)
}

function finishRouteEndStackResize() {
    if (!isRouteEndStackResizing.value) return

    isRouteEndStackResizing.value = false
    window.removeEventListener('mousemove', onRouteEndStackResizeMouseMove)
    window.removeEventListener('mouseup', finishRouteEndStackResize)
    document.body.style.cursor = previousRouteEndStackBodyCursor
    document.body.style.userSelect = previousRouteEndStackBodyUserSelect
}

function startRouteEndStackResize(event: MouseEvent) {
    event.preventDefault()
    isRouteEndStackResizing.value = true
    previousRouteEndStackBodyCursor = document.body.style.cursor
    previousRouteEndStackBodyUserSelect = document.body.style.userSelect
    document.body.style.cursor = 'row-resize'
    document.body.style.userSelect = 'none'
    window.addEventListener('mousemove', onRouteEndStackResizeMouseMove)
    window.addEventListener('mouseup', finishRouteEndStackResize)
}

function resetRouteEndStackResize() {
    routeEndStackListHeight.value = 0
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

    finishStationRouteStackResize()
    routeNodePickStage.value = 'none'
    routeSearchDialogVisible.value = false
    showAutoRouteGenerateCard.value = false
    autoRoutePickStage.value = 'none'
})

watch(showRouteEndCard, (visible) => {
    if (visible) return

    finishRouteEndStackResize()
    routeEndPickingNode.value = false
})

watch(showAutoRouteGenerateCard, (visible) => {
    if (visible) return

    autoRoutePickStage.value = 'none'
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
    finishStationRouteStackResize()
    finishRouteEndStackResize()
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
    flex-wrap: wrap;
    gap: 12px;
    flex: 0 0 auto;
    min-height: 40px;
    padding: 6px 10px;
    border-bottom: 1px solid #d8e2ef;
    background: #f7fafc;
}

.route-design-toolbar-left,
.route-design-toolbar-right,
.route-design-display-toolbar,
.route-design-scheme-control,
.route-design-scale-control,
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
    gap: 10px;
    flex-wrap: wrap;
    flex: 0 0 auto;
}

.route-design-scheme-control {
    gap: 6px;
}

.route-design-switch-control {
    gap: 6px;
    white-space: nowrap;
}

.route-design-display-toolbar {
    gap: 10px;
    flex-wrap: wrap;
    min-height: 28px;
    padding: 2px 8px;
    border: 1px solid #dbe5f0;
    border-radius: 6px;
    background: #ffffff;
}

.route-design-scale-control {
    gap: 5px;
    white-space: nowrap;
}

.route-design-scale-label {
    color: #627184;
    font-size: 12px;
    line-height: 1;
}

.route-design-scale-slider {
    width: 96px;
    flex: 0 0 96px;
}

.route-design-scale-value {
    width: 32px;
    color: #4c5968;
    font-size: 12px;
    font-variant-numeric: tabular-nums;
    line-height: 1;
    text-align: right;
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

.route-search-count {
    color: #2563eb;
    cursor: help;
    text-decoration: underline;
    text-decoration-style: dotted;
    text-underline-offset: 3px;
}

.station-route-card {
    container-type: inline-size;
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

.station-route-content {
    display: grid;
    grid-template-columns: minmax(280px, 0.92fr) minmax(300px, 1.08fr);
    flex: 1 1 auto;
    min-width: 0;
    min-height: 0;
}

.station-route-list-panel {
    display: flex;
    flex-direction: column;
    min-width: 0;
    min-height: 0;
    border-right: 1px solid #e1e8f0;
}

.station-route-stack-resizer {
    display: none;
}

.station-route-list-toolbar {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 8px;
    flex: 0 0 auto;
    min-height: 38px;
    padding: 6px 8px;
    border-bottom: 1px solid #e1e8f0;
    background: #fbfdff;
}

.station-route-list-summary {
    min-width: 0;
    color: #536273;
    font-size: 12px;
    line-height: 1.2;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
}

.station-route-list-actions {
    display: inline-flex;
    align-items: center;
    gap: 6px;
    flex: 0 0 auto;
}

.station-route-filter-panel {
    display: grid;
    grid-template-columns: repeat(2, minmax(0, 1fr));
    gap: 6px;
    min-width: 0;
}

.station-route-filter-control {
    width: 100%;
}

.station-route-filter-control :deep(.el-select__wrapper) {
    min-height: 28px;
}

.station-route-filter-control :deep(.el-select__tags-text) {
    display: inline-block;
    max-width: 82px;
    overflow: hidden;
    text-overflow: ellipsis;
    vertical-align: bottom;
    white-space: nowrap;
}

.station-route-filter-clear {
    justify-self: end;
    grid-column: 1 / -1;
}

:global(.station-route-filter-popover) {
    max-width: calc(100vw - 24px);
}

.station-route-table-wrap {
    flex: 1 1 auto;
    min-height: 0;
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

.station-route-description-control {
    display: grid;
    grid-template-columns: minmax(0, 1fr) 32px;
    align-items: flex-start;
    gap: 6px;
    width: 100%;
}

.station-route-full-control {
    width: 100%;
}

.station-route-input-tag {
    width: 100%;
}

.station-route-input-tag :deep(.el-select__wrapper) {
    align-items: flex-start;
    min-height: 32px;
    padding-top: 2px;
    padding-bottom: 2px;
}

.station-route-input-tag :deep(.el-select__selection) {
    flex-wrap: wrap;
    row-gap: 4px;
}

.station-route-input-tag :deep(.el-tag) {
    max-width: 100%;
}

.station-route-input-tag :deep(.el-select__tags-text) {
    display: inline-block;
    max-width: 170px;
    overflow: hidden;
    text-overflow: ellipsis;
    vertical-align: bottom;
    white-space: nowrap;
}

.station-route-select-option {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 10px;
    width: 100%;
    min-width: 0;
}

.station-route-select-option-name {
    flex: 1 1 auto;
    min-width: 0;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
}

.station-route-select-option-id {
    flex: 0 0 auto;
    color: #8a98a8;
    font-size: 12px;
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

.auto-route-card {
    display: flex;
    flex-direction: column;
    flex: 0 0 320px;
    min-height: 0;
    min-width: 280px;
    max-width: 360px;
    border: 1px solid #d7e2ee;
    border-radius: 8px;
    background: #ffffff;
    overflow: hidden;
}

.route-design-data-pane.is-single-card .auto-route-card {
    flex: 1 1 auto;
    max-width: none;
}

.auto-route-card-header {
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

.auto-route-title-group {
    display: flex;
    flex-direction: column;
    gap: 3px;
    min-width: 0;
}

.auto-route-title-group h2 {
    margin: 0;
    color: #1f2d3d;
    font-size: 15px;
    font-weight: 650;
    line-height: 1.2;
}

.auto-route-subtitle {
    color: #718096;
    font-size: 12px;
    line-height: 1.2;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
}

.auto-route-card-actions {
    display: inline-flex;
    align-items: center;
    gap: 6px;
    flex: 0 0 auto;
}

.auto-route-form-panel {
    display: flex;
    flex-direction: column;
    gap: 10px;
    flex: 1 1 auto;
    min-height: 0;
    padding: 10px;
    overflow: auto;
}

.auto-route-form {
    display: grid;
    grid-template-columns: minmax(0, 1fr);
    gap: 0;
}

.auto-route-form :deep(.el-form-item) {
    margin-bottom: 9px;
}

.auto-route-form :deep(.el-form-item__label) {
    margin-bottom: 3px;
    color: #536273;
    font-size: 12px;
    line-height: 1.2;
}

.auto-route-node-control {
    display: grid;
    grid-template-columns: minmax(0, 1fr) 32px;
    gap: 6px;
    width: 100%;
}

.auto-route-node-select {
    width: 100%;
}

.auto-route-node-select :deep(.el-select__wrapper) {
    align-items: flex-start;
    min-height: 32px;
    padding-top: 2px;
    padding-bottom: 2px;
}

.auto-route-node-select :deep(.el-select__selection) {
    flex-wrap: wrap;
    row-gap: 4px;
}

.auto-route-node-select :deep(.el-tag) {
    max-width: 100%;
}

.auto-route-node-select :deep(.el-select__tags-text) {
    display: inline-block;
    max-width: 150px;
    overflow: hidden;
    text-overflow: ellipsis;
    vertical-align: bottom;
    white-space: nowrap;
}

.auto-route-summary,
.auto-route-status {
    color: #536273;
    font-size: 12px;
    line-height: 1.35;
}

.auto-route-status {
    padding: 7px 8px;
    border: 1px solid #dbe5f0;
    border-radius: 6px;
    background: #f8fbff;
}

.auto-route-form-actions {
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

.route-end-filter-panel {
    display: grid;
    grid-template-columns: minmax(0, 1fr);
    gap: 6px;
    min-width: 0;
}

.route-end-filter-control {
    width: 100%;
}

.route-end-filter-control :deep(.el-select__wrapper) {
    min-height: 28px;
}

.route-end-filter-control :deep(.el-select__tags-text) {
    display: inline-block;
    max-width: 128px;
    overflow: hidden;
    text-overflow: ellipsis;
    vertical-align: bottom;
    white-space: nowrap;
}

.route-end-filter-clear {
    justify-self: end;
}

:global(.route-end-filter-popover) {
    max-width: calc(100vw - 24px);
}

.route-end-content {
    display: flex;
    flex-direction: column;
    flex: 1 1 auto;
    min-height: 0;
}

.route-end-table-wrap {
    flex: 0 0 var(--route-end-list-height, 32%);
    min-height: 118px;
    overflow: hidden;
}

.route-end-table-wrap :deep(.el-table) {
    font-size: 12px;
}

.route-end-stack-resizer {
    position: relative;
    flex: 0 0 8px;
    min-height: 8px;
    background: #dbe5f0;
    cursor: row-resize;
}

.route-end-stack-resizer::before {
    content: "";
    position: absolute;
    top: 3px;
    right: 0;
    left: 0;
    height: 2px;
    background: #a9b8ca;
}

.route-end-stack-resizer:hover,
.route-end-content.is-stack-resizing .route-end-stack-resizer {
    background: #c7d8ea;
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

@container (max-width: 660px) {
    .station-route-content {
        grid-template-columns: minmax(0, 1fr);
        grid-template-rows: var(--station-route-list-height, minmax(220px, 0.9fr)) 8px minmax(260px, 1.1fr);
    }

    .station-route-list-panel {
        border-right: 0;
    }

    .station-route-stack-resizer {
        position: relative;
        display: block;
        min-height: 8px;
        background: #dbe5f0;
        cursor: row-resize;
    }

    .station-route-stack-resizer::before {
        content: "";
        position: absolute;
        top: 3px;
        right: 0;
        left: 0;
        height: 2px;
        background: #a9b8ca;
    }

    .station-route-stack-resizer:hover,
    .station-route-content.is-stack-resizing .station-route-stack-resizer {
        background: #c7d8ea;
    }

    .station-route-filter-panel {
        grid-template-columns: minmax(0, 1fr);
    }
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

    .route-design-display-toolbar {
        width: 100%;
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
    .auto-route-card,
    .route-end-card {
        min-width: 260px;
    }

    .route-end-table-wrap {
        flex-basis: var(--route-end-list-height, 30%);
        min-height: 104px;
    }

    .route-search-result-popover {
        left: 8px;
        bottom: 8px;
        width: calc(100% - 16px);
    }
}
</style>
