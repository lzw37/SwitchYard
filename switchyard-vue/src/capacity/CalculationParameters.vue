<template>
    <section class="calc-params-page" v-loading="loadingData">
        <div class="calc-params-toolbar">
            <div class="calc-scheme-control">
                <span class="calc-control-label">{{ t('stationLayout.menu.stationScheme') }}</span>
                <el-select
                    v-model="currentStationSchemeId"
                    size="small"
                    filterable
                    class="calc-scheme-select"
                    :loading="loadingStationSchemes"
                    :disabled="!selectedInstanceId || loadingStationSchemes || loadingData"
                    :placeholder="t('stationLayout.placeholders.selectStationScheme')"
                    @change="handleStationSchemeChange"
                >
                    <el-option v-for="option in stationSchemeOptions" :key="option.id" :label="option.name" :value="option.id" />
                </el-select>
                <el-button
                    :icon="SetUp"
                    type="primary"
                    size="small"
                    :disabled="!canBatchSetRouteTimes"
                    @click="openBatchSetDialog"
                >
                    {{ t('calculationParameters.batchSet.button') }}
                </el-button>
                <el-button
                    :icon="DataAnalysis"
                    type="success"
                    size="small"
                    :disabled="!canOpenTractionCalculation"
                    @click="openTractionCalculationDialog"
                >
                    {{ t('calculationParameters.traction.button') }}
                </el-button>
            </div>

            <div class="calc-display-toolbar">
                <span class="calc-control-label">{{ t('routeDesign.toolbar.layoutDisplay') }}</span>
                <div class="calc-switch-control">
                    <span>{{ t('routeDesign.toolbar.showGrid') }}</span>
                    <el-switch v-model="showLayoutGrid" size="small" />
                </div>
                <div class="calc-switch-control">
                    <span>{{ t('routeDesign.toolbar.showNodes') }}</span>
                    <el-switch v-model="showLayoutNodes" size="small" />
                </div>
                <div class="calc-switch-control">
                    <span>{{ t('routeDesign.toolbar.curveDisplay') }}</span>
                    <el-switch
                        v-model="showLayoutCurveArc"
                        size="small"
                        inline-prompt
                        :active-text="t('stationLayout.curveDisplay.arc')"
                        :inactive-text="t('stationLayout.curveDisplay.tangent')"
                    />
                </div>
                <div class="calc-switch-control">
                    <span>{{ t('routeDesign.toolbar.showCellNames') }}</span>
                    <el-switch v-model="showLayoutCellNames" size="small" />
                </div>
                <div class="calc-scale-control">
                    <span>{{ t('routeDesign.toolbar.displayScale') }}</span>
                    <span>{{ t('stationLayout.scale.x') }}</span>
                    <el-slider v-model="layoutScaleX" size="small" :min="0.25" :max="4" :step="0.05" class="calc-scale-slider" />
                    <span class="calc-scale-value">{{ layoutScaleX.toFixed(2) }}</span>
                    <span>{{ t('stationLayout.scale.y') }}</span>
                    <el-slider v-model="layoutScaleY" size="small" :min="0.25" :max="4" :step="0.05" class="calc-scale-slider" />
                    <span class="calc-scale-value">{{ layoutScaleY.toFixed(2) }}</span>
                </div>
                <el-button :icon="Aim" size="small" @click="fitFullLayout">
                    {{ t('stationLayout.tools.fitFullView') }}
                </el-button>
            </div>
        </div>

        <div ref="bodyRef" class="calc-params-body" :class="{ 'is-resizing': isResizing }" :style="calcBodyStyle">
            <aside class="calc-route-pane" v-loading="loadingRoutes">
                <header class="calc-pane-header">
                    <div>
                        <h2>{{ t('calculationParameters.routes.title') }}</h2>
                        <span>{{ stationRouteListSummary }}</span>
                    </div>
                    <div class="calc-header-actions">
                        <el-tooltip :content="t('routeDesign.stationRoute.actions.refresh')" placement="top">
                            <el-button :icon="Refresh" circle size="small" :disabled="!canLoadRoutes" @click="refreshRouteList" />
                        </el-tooltip>
                        <el-popover placement="bottom-start" trigger="click" width="360" popper-class="station-route-filter-popover">
                            <template #reference>
                                <el-button :icon="Filter" circle size="small" :type="routeFiltersActive ? 'primary' : 'default'" />
                            </template>
                            <div class="calc-filter-panel">
                                <el-select v-model="routeFilters.types" multiple filterable clearable collapse-tags size="small" :placeholder="t('routeDesign.stationRoute.filter.type')">
                                    <el-option v-for="option in routeFilterTypeOptions" :key="option.id" :label="option.name" :value="option.id" />
                                </el-select>
                                <el-select
                                    v-for="filter in routeFilterFieldControls"
                                    :key="filter.field"
                                    v-model="routeFilters[filter.field]"
                                    multiple
                                    filterable
                                    clearable
                                    collapse-tags
                                    size="small"
                                    :placeholder="t(filter.placeholderKey)"
                                >
                                    <el-option v-for="option in getRouteFilterSelectOptions(filter)" :key="option.id" :label="option.name" :value="option.id" />
                                </el-select>
                                <el-button :icon="Close" size="small" :disabled="!routeFiltersActive" @click="clearRouteFilters">
                                    {{ t('routeDesign.stationRoute.actions.clearFilters') }}
                                </el-button>
                            </div>
                        </el-popover>
                    </div>
                </header>

                <div class="calc-route-quick-filters">
                    <div class="calc-route-filter-row">
                        <span class="calc-route-filter-label">{{ t('calculationParameters.routes.quickFilters.type') }}</span>
                        <div class="calc-route-type-toggle-wrap">
                            <el-radio-group v-model="routeQuickTypeFilter" class="calc-route-type-toggle" size="small">
                                <el-radio-button value="">
                                    {{ t('calculationParameters.routes.quickFilters.allTypes') }}
                                </el-radio-button>
                                <el-radio-button v-for="option in routeQuickTypeOptions" :key="option.id" :value="option.id">
                                    {{ option.name }}
                                </el-radio-button>
                            </el-radio-group>
                        </div>
                    </div>
                    <div class="calc-route-filter-row calc-route-end-row">
                        <span class="calc-route-filter-label">{{ t('calculationParameters.routes.quickFilters.routeEnd') }}</span>
                        <div class="calc-route-end-selects">
                            <el-select
                                v-model="routeQuickStartRouteEndIds"
                                multiple
                                filterable
                                clearable
                                collapse-tags
                                collapse-tags-tooltip
                                size="small"
                                class="calc-route-end-filter"
                                :loading="loadingRouteEnds"
                                :placeholder="t('calculationParameters.routes.quickFilters.startRouteEnd')"
                            >
                                <el-option v-for="option in routeEndFilterOptions" :key="`start-${option.id}`" :label="option.name" :value="option.id" />
                            </el-select>
                            <el-select
                                v-model="routeQuickEndRouteEndIds"
                                multiple
                                filterable
                                clearable
                                collapse-tags
                                collapse-tags-tooltip
                                size="small"
                                class="calc-route-end-filter"
                                :loading="loadingRouteEnds"
                                :placeholder="t('calculationParameters.routes.quickFilters.endRouteEnd')"
                            >
                                <el-option v-for="option in routeEndFilterOptions" :key="`end-${option.id}`" :label="option.name" :value="option.id" />
                            </el-select>
                        </div>
                    </div>
                </div>

                <el-table
                    :data="filteredStationRoutes"
                    size="small"
                    height="100%"
                    class="calc-route-table"
                    row-key="id"
                    highlight-current-row
                    :current-row-key="selectedRouteId"
                    :empty-text="stationRouteTableEmptyText"
                    @row-click="selectStationRoute"
                >
                    <el-table-column width="34" align="center" class-name="calc-route-status-column">
                        <template #default="{ row }">
                            <span
                                :class="['calc-route-status-dot', { 'is-configured': routeHasOccupancyTime(row) }]"
                                role="img"
                                :aria-label="routeHasOccupancyTime(row) ? t('calculationParameters.routes.occupancyConfigured') : t('calculationParameters.routes.occupancyNotConfigured')"
                                :title="routeHasOccupancyTime(row) ? t('calculationParameters.routes.occupancyConfigured') : t('calculationParameters.routes.occupancyNotConfigured')"
                            />
                        </template>
                    </el-table-column>
                    <el-table-column prop="id" :label="t('routeDesign.stationRoute.fields.id')" width="106" show-overflow-tooltip />
                    <el-table-column prop="type" :label="t('routeDesign.stationRoute.fields.type')" width="96" show-overflow-tooltip>
                        <template #default="{ row }">
                            {{ getStationRouteTypeLabel(row.type) }}
                        </template>
                    </el-table-column>
                    <el-table-column prop="description" :label="t('routeDesign.stationRoute.fields.description')" min-width="150" show-overflow-tooltip />
                </el-table>
            </aside>
            <div
                class="calc-vertical-resizer"
                role="separator"
                aria-orientation="vertical"
                @mousedown="startColumnResize('left', $event)"
                @dblclick="resetRoutePaneWidth"
            />

            <main ref="centerPaneRef" class="calc-center-pane" :style="calcCenterStyle">
                <section class="calc-layout-pane">
                    <div ref="layoutViewportRef" class="calc-layout-scroll">
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
                            :highlighted-route-node-ids="highlightedRouteNodeIds"
                            :highlighted-route-link-ids="highlightedRouteLinkIds"
                            :highlighted-route-arrow-node-ids="highlightedRouteArrowNodeIds"
                            :highlighted-route-color="highlightedRouteColor"
                            :highlighted-route-arrow-visible="highlightedRouteArrowVisible"
                        />
                    </div>
                </section>
                <div
                    class="calc-horizontal-resizer"
                    role="separator"
                    aria-orientation="horizontal"
                    @mousedown="startRowResize"
                    @dblclick="resetLayoutPaneHeight"
                />
                <section class="calc-occupancy-pane">
                    <header class="calc-occupancy-header">
                        <h2>{{ t('calculationParameters.occupancy.title') }}</h2>
                        <div class="calc-occupancy-controls">
                            <div class="calc-gantt-scale-control">
                                <span>{{ t('calculationParameters.occupancy.horizontalScale') }}</span>
                                <el-slider
                                    v-model="occupancyGanttScaleX"
                                    size="small"
                                    :min="0.01"
                                    :max="4"
                                    :step="0.01"
                                    :disabled="occupancyGanttAutoFit"
                                    class="calc-gantt-scale-slider"
                                />
                                <span class="calc-scale-value">{{ occupancyGanttScaleX.toFixed(2) }}</span>
                            </div>
                            <div class="calc-switch-control">
                                <span>{{ t('calculationParameters.occupancy.autoFit') }}</span>
                                <el-switch v-model="occupancyGanttAutoFit" size="small" />
                            </div>
                        </div>
                    </header>
                    <OccupationTimeGantt
                        class="calc-occupation-gantt"
                        v-model:scale-x="occupancyGanttScaleX"
                        :cells="ganttCells"
                        :times="routeTimes"
                        :disabled="loadingRouteTimes || savingRouteTimes"
                        :auto-fit="occupancyGanttAutoFit"
                        :empty-text="occupancyGanttEmptyText"
                        :cell-axis-label="t('calculationParameters.manager.fields.cellID')"
                        :time-axis-label="t('calculationParameters.occupancy.timeAxis')"
                        :start-handle-label="t('calculationParameters.occupancy.startHandle')"
                        :end-handle-label="t('calculationParameters.occupancy.endHandle')"
                        @change="handleGanttTimeChange"
                    />
                </section>
            </main>
            <div
                class="calc-vertical-resizer"
                role="separator"
                aria-orientation="vertical"
                @mousedown="startColumnResize('right', $event)"
                @dblclick="resetParamPaneWidth"
            />

            <aside class="calc-param-pane">
                <header class="calc-param-header">
                    <div>
                        <h2>{{ t('calculationParameters.manager.title') }}</h2>
                        <span>{{ selectedRouteId || t('calculationParameters.manager.noRoute') }}</span>
                    </div>
                    <div class="calc-param-actions">
                        <el-button
                            :icon="Plus"
                            type="primary"
                            size="small"
                            :disabled="!canCreateRouteTimes"
                            :loading="creatingRouteTimes"
                            @click="createRouteTimes"
                        >
                            {{ t('calculationParameters.manager.createOccupancyTime') }}
                        </el-button>
                        <el-button
                            :icon="Check"
                            type="success"
                            size="small"
                            :disabled="!canSaveRouteTimes"
                            :loading="savingRouteTimes"
                            @click="saveRouteTimes"
                        >
                            {{ t('calculationParameters.manager.saveOccupancyTime') }}
                        </el-button>
                    </div>
                </header>
                <div class="calc-uniform-shift-panel">
                    <span class="calc-uniform-shift-title">{{ t('calculationParameters.manager.uniformShift.title') }}</span>
                    <label class="calc-uniform-shift-field">
                        <span>{{ t('calculationParameters.manager.fields.startShift') }}</span>
                        <el-input-number
                            v-model="uniformStartOccupationShift"
                            size="small"
                            :precision="0"
                            :step="1"
                            controls-position="right"
                            :disabled="loadingRouteTimes || creatingRouteTimes || savingRouteTimes"
                        />
                    </label>
                    <label class="calc-uniform-shift-field">
                        <span>{{ t('calculationParameters.manager.fields.endShift') }}</span>
                        <el-input-number
                            v-model="uniformEndOccupationShift"
                            size="small"
                            :precision="0"
                            :step="1"
                            controls-position="right"
                            :disabled="loadingRouteTimes || creatingRouteTimes || savingRouteTimes"
                        />
                    </label>
                    <el-button
                        size="small"
                        :disabled="!canApplyUniformRouteTimes"
                        @click="applyUniformRouteTimeShifts"
                    >
                        {{ t('calculationParameters.manager.uniformShift.apply') }}
                    </el-button>
                </div>
                <el-table
                    :data="routeTimes"
                    size="small"
                    height="100%"
                    v-loading="loadingRouteTimes"
                    :empty-text="t('calculationParameters.manager.emptyRouteTimes')"
                >
                    <el-table-column :label="t('calculationParameters.manager.fields.cellID')" min-width="120" show-overflow-tooltip>
                        <template #default="{ row }">
                            <span class="calc-route-time-cell-name">{{ getCellDisplayName(row.cellID) }}</span>
                            <el-tag
                                v-if="row.isInterruptCell"
                                class="calc-route-time-cell-tag"
                                size="small"
                                type="warning"
                                effect="plain"
                            >
                                {{ t('calculationParameters.manager.directInterrupt') }}
                            </el-tag>
                        </template>
                    </el-table-column>
                    <el-table-column :label="t('calculationParameters.manager.fields.startShift')" width="118">
                        <template #default="{ row }">
                            <el-input-number
                                v-model="row.startOccupationShift"
                                class="calc-route-time-input"
                                size="small"
                                :precision="0"
                                :step="1"
                                controls-position="right"
                                :disabled="savingRouteTimes"
                                @change="syncUniformShiftDraftFromRows"
                            />
                        </template>
                    </el-table-column>
                    <el-table-column :label="t('calculationParameters.manager.fields.endShift')" width="118">
                        <template #default="{ row }">
                            <el-input-number
                                v-model="row.endOccupationShift"
                                class="calc-route-time-input"
                                size="small"
                                :precision="0"
                                :step="1"
                                controls-position="right"
                                :disabled="savingRouteTimes"
                                @change="syncUniformShiftDraftFromRows"
                            />
                        </template>
                    </el-table-column>
                </el-table>
            </aside>
        </div>

        <el-dialog
            v-model="batchSetDialogVisible"
            :title="t('calculationParameters.batchSet.title')"
            width="760px"
            destroy-on-close
            :close-on-click-modal="!batchSettingRouteTimes"
            @closed="batchRouteListDialogVisible = false"
        >
            <el-table
                :data="batchRouteTimeSettings"
                size="small"
                max-height="360"
                :empty-text="t('calculationParameters.batchSet.empty')"
            >
                <el-table-column prop="type" :label="t('calculationParameters.batchSet.routeType')" min-width="130" show-overflow-tooltip />
                <el-table-column prop="routeCount" :label="t('calculationParameters.batchSet.routeCount')" width="92" align="center" />
                <el-table-column :label="t('calculationParameters.batchSet.routes')" width="150" align="center">
                    <template #default="{ row }">
                        <el-button size="small" :disabled="batchSettingRouteTimes" @click="openBatchRouteList(row.type)">
                            {{ t('calculationParameters.batchSet.routeList', { selected: row.selectedRouteIds.length, total: row.routeCount }) }}
                        </el-button>
                    </template>
                </el-table-column>
                <el-table-column :label="t('calculationParameters.manager.fields.startShift')" width="150">
                    <template #default="{ row }">
                        <el-input-number
                            v-model="row.startOccupationShift"
                            class="calc-batch-shift-input"
                            size="small"
                            :precision="0"
                            :step="1"
                            controls-position="right"
                            :disabled="batchSettingRouteTimes"
                        />
                    </template>
                </el-table-column>
                <el-table-column :label="t('calculationParameters.manager.fields.endShift')" width="150">
                    <template #default="{ row }">
                        <el-input-number
                            v-model="row.endOccupationShift"
                            class="calc-batch-shift-input"
                            size="small"
                            :precision="0"
                            :step="1"
                            controls-position="right"
                            :disabled="batchSettingRouteTimes"
                        />
                    </template>
                </el-table-column>
            </el-table>
            <template #footer>
                <el-button :disabled="batchSettingRouteTimes" @click="batchSetDialogVisible = false">
                    {{ t('calculationParameters.batchSet.cancel') }}
                </el-button>
                <el-button
                    type="primary"
                    :loading="batchSettingRouteTimes"
                    :disabled="batchRouteTimeSettings.length === 0"
                    @click="applyBatchSetRouteTimes"
                >
                    {{ t('calculationParameters.batchSet.apply') }}
                </el-button>
            </template>
        </el-dialog>

        <el-dialog
            v-model="batchRouteListDialogVisible"
            :title="t('calculationParameters.batchSet.routeListTitle', { type: activeBatchRouteType })"
            width="520px"
            append-to-body
        >
            <div v-if="activeBatchRouteSetting" class="calc-batch-route-list">
                <div class="calc-batch-route-list-toolbar">
                    <el-checkbox
                        :model-value="isActiveBatchRouteListAllSelected"
                        :indeterminate="isActiveBatchRouteListIndeterminate"
                        @change="toggleActiveBatchRouteListAll"
                    >
                        {{ t('calculationParameters.batchSet.selectAll') }}
                    </el-checkbox>
                    <span>
                        {{ t('calculationParameters.batchSet.selectedCount', { selected: activeBatchRouteSetting.selectedRouteIds.length, total: activeBatchRouteSetting.routeCount }) }}
                    </span>
                </div>
                <el-checkbox-group v-model="activeBatchRouteSetting.selectedRouteIds" class="calc-batch-route-checkboxes">
                    <el-checkbox v-for="route in activeBatchRouteSetting.routes" :key="route.id" :label="route.id">
                        {{ route.name }}
                    </el-checkbox>
                </el-checkbox-group>
            </div>
            <template #footer>
                <el-button type="primary" @click="batchRouteListDialogVisible = false">
                    {{ t('calculationParameters.batchSet.confirmRoutes') }}
                </el-button>
            </template>
        </el-dialog>

        <el-dialog
            v-model="tractionDialogVisible"
            :title="t('calculationParameters.traction.title')"
            width="1080px"
            class="calc-traction-dialog"
            destroy-on-close
        >
            <div v-if="tractionResult" class="calc-traction-content">
                <div class="calc-traction-summary">
                    <div>
                        <span>{{ t('calculationParameters.traction.currentRoute') }}</span>
                        <strong>{{ tractionResult.routeLabel }}</strong>
                    </div>
                    <el-tag type="info" size="small">{{ t('calculationParameters.traction.sampleTag') }}</el-tag>
                </div>

                <div class="calc-traction-charts">
                    <section class="calc-traction-panel">
                        <header>
                            <h3>{{ t('calculationParameters.traction.velocityDistance') }}</h3>
                            <span>{{ formatDistance(tractionResult.totalDistance) }}</span>
                        </header>
                        <svg class="calc-traction-chart" :viewBox="`0 0 ${tractionResult.velocityCurve.width} ${tractionResult.velocityCurve.height}`">
                            <line
                                v-for="tick in tractionResult.velocityCurve.xTicks"
                                :key="`vx-${tick.value}`"
                                class="calc-traction-grid-line"
                                :x1="tick.x"
                                :x2="tick.x"
                                :y1="tractionResult.velocityCurve.plotTop"
                                :y2="tractionResult.velocityCurve.plotBottom"
                            />
                            <line
                                v-for="tick in tractionResult.velocityCurve.yTicks"
                                :key="`vy-${tick.value}`"
                                class="calc-traction-grid-line"
                                :x1="tractionResult.velocityCurve.plotLeft"
                                :x2="tractionResult.velocityCurve.plotRight"
                                :y1="tick.y"
                                :y2="tick.y"
                            />
                            <polyline class="calc-traction-velocity-line" :points="tractionResult.velocityCurve.points" />
                            <text v-for="tick in tractionResult.velocityCurve.xTicks" :key="`vxl-${tick.value}`" class="calc-traction-axis-label" :x="tick.x" :y="tractionResult.velocityCurve.height - 10" text-anchor="middle">
                                {{ formatDistanceTick(tick.value) }}
                            </text>
                            <text v-for="tick in tractionResult.velocityCurve.yTicks" :key="`vyl-${tick.value}`" class="calc-traction-axis-label" :x="8" :y="tick.y + 4">
                                {{ formatSpeedTick(tick.value) }}
                            </text>
                        </svg>
                    </section>

                    <section class="calc-traction-panel">
                        <header>
                            <h3>{{ t('calculationParameters.traction.timeDistance') }}</h3>
                            <span>{{ formatTime(tractionResult.totalTime) }}</span>
                        </header>
                        <svg class="calc-traction-chart" :viewBox="`0 0 ${tractionResult.timeCurve.width} ${tractionResult.timeCurve.height}`">
                            <line
                                v-for="tick in tractionResult.timeCurve.xTicks"
                                :key="`tx-${tick.value}`"
                                class="calc-traction-grid-line"
                                :x1="tick.x"
                                :x2="tick.x"
                                :y1="tractionResult.timeCurve.plotTop"
                                :y2="tractionResult.timeCurve.plotBottom"
                            />
                            <line
                                v-for="tick in tractionResult.timeCurve.yTicks"
                                :key="`ty-${tick.value}`"
                                class="calc-traction-grid-line"
                                :x1="tractionResult.timeCurve.plotLeft"
                                :x2="tractionResult.timeCurve.plotRight"
                                :y1="tick.y"
                                :y2="tick.y"
                            />
                            <polyline class="calc-traction-time-line" :points="tractionResult.timeCurve.points" />
                            <text v-for="tick in tractionResult.timeCurve.xTicks" :key="`txl-${tick.value}`" class="calc-traction-axis-label" :x="tick.x" :y="tractionResult.timeCurve.height - 10" text-anchor="middle">
                                {{ formatDistanceTick(tick.value) }}
                            </text>
                            <text v-for="tick in tractionResult.timeCurve.yTicks" :key="`tyl-${tick.value}`" class="calc-traction-axis-label" :x="8" :y="tick.y + 4">
                                {{ formatTimeTick(tick.value) }}
                            </text>
                        </svg>
                    </section>
                </div>

                <div class="calc-traction-tables">
                    <section class="calc-traction-panel">
                        <header><h3>{{ t('calculationParameters.traction.linkTimes') }}</h3></header>
                        <el-table :data="tractionResult.linkResults" size="small" max-height="240">
                            <el-table-column prop="linkName" :label="t('calculationParameters.traction.fields.link')" min-width="120" show-overflow-tooltip />
                            <el-table-column :label="t('calculationParameters.traction.fields.length')" width="90" align="right">
                                <template #default="{ row }">{{ formatDistance(row.length) }}</template>
                            </el-table-column>
                            <el-table-column :label="t('calculationParameters.traction.fields.entryTime')" width="98" align="right">
                                <template #default="{ row }">{{ formatTime(row.entryTime) }}</template>
                            </el-table-column>
                            <el-table-column :label="t('calculationParameters.traction.fields.exitTime')" width="98" align="right">
                                <template #default="{ row }">{{ formatTime(row.exitTime) }}</template>
                            </el-table-column>
                            <el-table-column :label="t('calculationParameters.traction.fields.speed')" width="116" align="right">
                                <template #default="{ row }">{{ formatSpeed(row.speedIn) }} / {{ formatSpeed(row.speedOut) }}</template>
                            </el-table-column>
                        </el-table>
                    </section>

                    <section class="calc-traction-panel">
                        <header><h3>{{ t('calculationParameters.traction.cellOccupations') }}</h3></header>
                        <el-table :data="tractionResult.cellResults" size="small" max-height="240">
                            <el-table-column prop="cellName" :label="t('calculationParameters.manager.fields.cellID')" min-width="150" show-overflow-tooltip />
                            <el-table-column :label="t('calculationParameters.traction.fields.startTime')" width="110" align="right">
                                <template #default="{ row }">{{ formatTime(row.startTime) }}</template>
                            </el-table-column>
                            <el-table-column :label="t('calculationParameters.traction.fields.endTime')" width="110" align="right">
                                <template #default="{ row }">{{ formatTime(row.endTime) }}</template>
                            </el-table-column>
                            <el-table-column :label="t('calculationParameters.traction.fields.duration')" width="100" align="right">
                                <template #default="{ row }">{{ formatTime(row.endTime - row.startTime) }}</template>
                            </el-table-column>
                        </el-table>
                    </section>
                </div>
            </div>
            <el-empty v-else :description="t('calculationParameters.traction.noRoute')" />
        </el-dialog>
    </section>
</template>

<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { ElMessage } from 'element-plus'
import { Aim, Check, Close, DataAnalysis, Filter, Plus, Refresh, SetUp } from '@element-plus/icons-vue'
import axios from '@/utils/axios'
import OccupationTimeGantt from './components/OccupationTimeGantt.vue'
import StationLayoutEditor from './components/StationLayoutEditor.vue'

interface StationSchemeOption { id: string; name: string }
interface RouteListSelectOption { id: string; name: string }
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
    interruptCellList: string
    signalList: string
    allowanceTags: string
    forbiddenTags: string
    startNodeID: string
    endNodeID: string
    occupancyTimeConfigured: boolean
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
type StationRouteObjectListField = 'nodeList' | 'linkList' | 'switchList' | 'cellList' | 'signalList'
type StationRouteFilterField = 'types' | 'startNodeIds' | 'endNodeIds' | 'nodeIds' | 'linkIds' | 'cellIds' | 'switchIds' | 'signalIds'
type StationRouteObjectFilterField = Exclude<StationRouteFilterField, 'types'>
type StationRouteFilters = Record<StationRouteFilterField, string[]>
type RouteObjectOptionMap = Record<StationRouteObjectListField, RouteListSelectOption[]>
interface RouteFilterControl { field: StationRouteObjectFilterField; placeholderKey: string; optionField: StationRouteObjectListField }
interface DataRect { minX: number; minY: number; maxX: number; maxY: number }
interface StationRouteTime {
    instanceID: string
    stationSchemeID: string
    routeID: string
    trainTypeID: string
    cellID: string
    startOccupationShift: number | null
    endOccupationShift: number | null
    isInterruptCell: boolean
}
interface GanttCell { id: string; name: string }
interface GanttTimeChange {
    timeIndex: number
    cellID: string
    startOccupationShift: number
    endOccupationShift: number
}
interface BatchRouteOption { id: string; name: string }
interface BatchRouteTypeSummary {
    type: string
    routes: BatchRouteOption[]
}
interface BatchRouteTimeSetting {
    type: string
    routeCount: number
    routes: BatchRouteOption[]
    selectedRouteIds: string[]
    startOccupationShift: number | null
    endOccupationShift: number | null
}
interface TractionPoint { distance: number; value: number }
interface TractionTick { value: number; x: number; y: number }
interface TractionCurveRender {
    width: number
    height: number
    plotLeft: number
    plotRight: number
    plotTop: number
    plotBottom: number
    points: string
    xTicks: TractionTick[]
    yTicks: TractionTick[]
}
interface TractionLinkResult {
    linkID: string
    linkName: string
    length: number
    distanceStart: number
    distanceEnd: number
    entryTime: number
    exitTime: number
    speedIn: number
    speedOut: number
}
interface TractionCellResult {
    cellID: string
    cellName: string
    startTime: number
    endTime: number
}
interface TractionCalculationResult {
    routeLabel: string
    totalDistance: number
    totalTime: number
    velocityCurve: TractionCurveRender
    timeCurve: TractionCurveRender
    linkResults: TractionLinkResult[]
    cellResults: TractionCellResult[]
}

const props = withDefaults(defineProps<{ selectedInstanceId?: string | null }>(), { selectedInstanceId: '' })
const { t } = useI18n()

const stationLayoutEditorRef = ref<any>(null)
const layoutViewportRef = ref<HTMLElement | null>(null)
const bodyRef = ref<HTMLElement | null>(null)
const centerPaneRef = ref<HTMLElement | null>(null)
const currentStationSchemeId = ref('')
const stationSchemeOptions = ref<StationSchemeOption[]>([])
const loadingStationSchemes = ref(false)
const loadingData = ref(false)
const loadingRoutes = ref(false)
const loadingRouteEnds = ref(false)
const loadingRouteTimes = ref(false)
const creatingRouteTimes = ref(false)
const savingRouteTimes = ref(false)
const batchSetDialogVisible = ref(false)
const batchRouteListDialogVisible = ref(false)
const batchSettingRouteTimes = ref(false)
const activeBatchRouteType = ref('')
const tractionDialogVisible = ref(false)
const layoutDisplayStyles = ref<Record<string, unknown>>({})
const layoutCells = ref<any[]>([])
const layoutData = ref<any>({})
const layoutGridSpacing = ref(20)
const layoutScaleX = ref(1)
const layoutScaleY = ref(1)
const showLayoutGrid = ref(true)
const showLayoutNodes = ref(true)
const showLayoutCurveArc = ref(true)
const showLayoutCellNames = ref(false)
const routeObjectOptions = ref<RouteObjectOptionMap>(createEmptyRouteObjectOptions())
const stationRoutes = ref<StationRoute[]>([])
const stationRouteEndOptions = ref<StationRouteEndOption[]>([])
const selectedRouteId = ref('')
const routeFilters = ref<StationRouteFilters>(createEmptyRouteFilters())
const routeQuickTypeFilter = ref('')
const routeQuickStartRouteEndIds = ref<string[]>([])
const routeQuickEndRouteEndIds = ref<string[]>([])
const routeTimes = ref<StationRouteTime[]>([])
const batchRouteTimeSettings = ref<BatchRouteTimeSetting[]>([])
const uniformStartOccupationShift = ref<number | null>(0)
const uniformEndOccupationShift = ref<number | null>(0)
const occupancyGanttScaleX = ref(1)
const occupancyGanttAutoFit = ref(false)
const routePaneWidth = ref(320)
const paramPaneWidth = ref(300)
const layoutPaneHeight = ref(0)
const isResizing = ref(false)

const routeHighlightColors = { arrival: '#ef4444', departure: '#2563eb', locomotive: '#16a34a', shunting: '#facc15' }
const routeTypeOptions = ['Arrival', 'Departure', 'Shunting', 'Locomotive']
const stationRouteTypeLabelKeys: Record<string, string> = {
    arrival: 'routeDesign.stationRoute.types.arrival',
    '接车': 'routeDesign.stationRoute.types.arrival',
    '接车进路': 'routeDesign.stationRoute.types.arrival',
    departure: 'routeDesign.stationRoute.types.departure',
    '发车': 'routeDesign.stationRoute.types.departure',
    '发车进路': 'routeDesign.stationRoute.types.departure',
    locomotive: 'routeDesign.stationRoute.types.locomotive',
    '机车出入段': 'routeDesign.stationRoute.types.locomotive',
    '机车出入段进路': 'routeDesign.stationRoute.types.locomotive',
    '机车走行': 'routeDesign.stationRoute.types.locomotive',
    shunting: 'routeDesign.stationRoute.types.shunting',
    '调车': 'routeDesign.stationRoute.types.shunting',
    '调车进路': 'routeDesign.stationRoute.types.shunting',
}
const routeFilterFieldControls: RouteFilterControl[] = [
    { field: 'startNodeIds', placeholderKey: 'routeDesign.stationRoute.filter.startNode', optionField: 'nodeList' },
    { field: 'endNodeIds', placeholderKey: 'routeDesign.stationRoute.filter.endNode', optionField: 'nodeList' },
    { field: 'nodeIds', placeholderKey: 'routeDesign.stationRoute.filter.node', optionField: 'nodeList' },
    { field: 'linkIds', placeholderKey: 'routeDesign.stationRoute.filter.link', optionField: 'linkList' },
    { field: 'cellIds', placeholderKey: 'routeDesign.stationRoute.filter.cell', optionField: 'cellList' },
    { field: 'switchIds', placeholderKey: 'routeDesign.stationRoute.filter.switch', optionField: 'switchList' },
    { field: 'signalIds', placeholderKey: 'routeDesign.stationRoute.filter.signal', optionField: 'signalList' },
]

const selectedInstanceId = computed(() => props.selectedInstanceId || '')
const canLoadRoutes = computed(() => Boolean(selectedInstanceId.value && currentStationSchemeId.value.trim()))
const canCreateRouteTimes = computed(() => Boolean(selectedInstanceId.value && currentStationSchemeId.value.trim() && selectedRouteId.value && !loadingRouteTimes.value && !creatingRouteTimes.value && !savingRouteTimes.value))
const canSaveRouteTimes = computed(() => Boolean(selectedInstanceId.value && currentStationSchemeId.value.trim() && selectedRouteId.value && routeTimes.value.length > 0 && !loadingRouteTimes.value && !creatingRouteTimes.value && !savingRouteTimes.value))
const canApplyUniformRouteTimes = computed(() => Boolean(routeTimes.value.length > 0 && !loadingRouteTimes.value && !creatingRouteTimes.value && !savingRouteTimes.value))
const routeTypeSummaries = computed<BatchRouteTypeSummary[]>(() => {
    const summaries = new Map<string, BatchRouteTypeSummary>()
    for (const route of stationRoutes.value) {
        const type = String(route.type || '').trim()
        if (!type) continue
        let summary = summaries.get(type)
        if (!summary) {
            summary = { type, routes: [] }
            summaries.set(type, summary)
        }
        summary.routes.push({
            id: route.id,
            name: route.description ? `${route.id} - ${route.description}` : route.id,
        })
    }
    return [...summaries.values()]
        .map((summary) => ({
            ...summary,
            routes: summary.routes.sort((a, b) => a.id.localeCompare(b.id, undefined, { numeric: true })),
        }))
        .sort((a, b) => a.type.localeCompare(b.type, undefined, { numeric: true }))
})
const canBatchSetRouteTimes = computed(() => Boolean(
    selectedInstanceId.value &&
    currentStationSchemeId.value.trim() &&
    routeTypeSummaries.value.length > 0 &&
    !loadingRoutes.value &&
    !batchSettingRouteTimes.value
))
const canOpenTractionCalculation = computed(() => Boolean(selectedStationRoute.value))
const activeBatchRouteSetting = computed(() => batchRouteTimeSettings.value.find((setting) => setting.type === activeBatchRouteType.value) || null)
const isActiveBatchRouteListAllSelected = computed(() => Boolean(
    activeBatchRouteSetting.value &&
    activeBatchRouteSetting.value.routeCount > 0 &&
    activeBatchRouteSetting.value.selectedRouteIds.length === activeBatchRouteSetting.value.routeCount
))
const isActiveBatchRouteListIndeterminate = computed(() => Boolean(
    activeBatchRouteSetting.value &&
    activeBatchRouteSetting.value.selectedRouteIds.length > 0 &&
    activeBatchRouteSetting.value.selectedRouteIds.length < activeBatchRouteSetting.value.routeCount
))
const selectedStationRoute = computed(() => stationRoutes.value.find((item) => item.id === selectedRouteId.value) || null)
const stationRouteEndByBindingNodeId = computed(() => {
    const map = new Map<string, StationRouteEndOption>()
    for (const routeEnd of stationRouteEndOptions.value) {
        const bindingNodeID = routeEnd.bindingNodeID.trim()
        if (bindingNodeID) map.set(bindingNodeID, routeEnd)
    }
    return map
})
const routeEndFilterOptions = computed<RouteListSelectOption[]>(() => {
    const optionsById = new Map<string, RouteListSelectOption>()
    for (const routeEnd of stationRouteEndOptions.value) {
        if (!optionsById.has(routeEnd.id)) {
            optionsById.set(routeEnd.id, { id: routeEnd.id, name: getStationRouteEndDisplayName(routeEnd) })
        }
    }
    for (const id of normalizeRouteListValues([...routeQuickStartRouteEndIds.value, ...routeQuickEndRouteEndIds.value])) {
        if (!optionsById.has(id)) optionsById.set(id, { id, name: id })
    }
    return [...optionsById.values()].sort((a, b) => a.name.localeCompare(b.name, undefined, { numeric: true }))
})
const routeQuickTypeOptions = computed(() => buildRouteTypeFilterOptions([...routeTypeOptions, ...stationRoutes.value.map((route) => route.type)]))
const advancedRouteFiltersActive = computed(() => Object.values(routeFilters.value).some((values) => values.length > 0))
const routeQuickFiltersActive = computed(() => Boolean(
    routeQuickTypeFilter.value ||
    routeQuickStartRouteEndIds.value.length > 0 ||
    routeQuickEndRouteEndIds.value.length > 0
))
const routeFiltersActive = computed(() => advancedRouteFiltersActive.value || routeQuickFiltersActive.value)
const filteredStationRoutes = computed(() => stationRoutes.value.filter(routeMatchesFilters))
const routeFilterTypeOptions = computed(() => routeQuickTypeOptions.value)
const stationRouteTableEmptyText = computed(() => routeFiltersActive.value ? t('routeDesign.stationRoute.filter.empty') : t('routeDesign.stationRoute.empty'))
const stationRouteListSummary = computed(() => routeFiltersActive.value
    ? t('routeDesign.stationRoute.filter.count', { filtered: filteredStationRoutes.value.length, total: stationRoutes.value.length })
    : t('routeDesign.stationRoute.count', { count: stationRoutes.value.length }))
const highlightedRoutePathNodeIds = computed(() => {
    const route = selectedStationRoute.value
    if (!route) return []
    const ids = parseRouteIdText(route.nodeList)
    return ids.length > 0 ? ids : normalizeRouteListValues([route.startNodeID, route.endNodeID])
})
const highlightedRouteNodeIds = computed(() => normalizeRouteListValues([
    ...highlightedRoutePathNodeIds.value,
    selectedStationRoute.value?.startNodeID,
    selectedStationRoute.value?.endNodeID,
]))
const highlightedRouteArrowNodeIds = computed(() => highlightedRoutePathNodeIds.value)
const highlightedRouteLinkIds = computed(() => selectedStationRoute.value ? parseRouteIdText(selectedStationRoute.value.linkList) : [])
const highlightedRouteColor = computed(() => getStationRouteHighlightColor(selectedStationRoute.value?.type || ''))
const highlightedRouteArrowVisible = computed(() => highlightedRouteArrowNodeIds.value.length >= 2)
const selectedRouteCellIds = computed(() => selectedStationRoute.value ? parseRouteIdText(selectedStationRoute.value.cellList) : [])
const selectedRouteInterruptCellIds = computed(() => {
    const route = selectedStationRoute.value
    if (!route) return []
    const occupiedCellSet = new Set(selectedRouteCellIds.value.map((id) => id.toLowerCase()))
    return normalizeRouteListValues(parseRouteIdText(route.interruptCellList))
        .filter((id) => !occupiedCellSet.has(id.toLowerCase()))
})
const selectedRouteOccupancyCellIds = computed(() => normalizeRouteListValues([
    ...selectedRouteCellIds.value,
    ...selectedRouteInterruptCellIds.value,
]))
const ganttCells = computed<GanttCell[]>(() => {
    const cellIds = selectedRouteOccupancyCellIds.value.length > 0
        ? selectedRouteOccupancyCellIds.value
        : routeTimes.value.map((row) => row.cellID)
    const interruptCellSet = new Set(selectedRouteInterruptCellIds.value.map((id) => id.toLowerCase()))
    return cellIds.map((id) => ({
        id,
        name: interruptCellSet.has(id.toLowerCase())
            ? `${getCellDisplayName(id) || id}（${t('calculationParameters.manager.directInterrupt')}）`
            : (getCellDisplayName(id) || id),
    }))
})
const occupancyGanttEmptyText = computed(() => selectedRouteId.value
    ? t('calculationParameters.occupancy.emptyCells')
    : t('calculationParameters.occupancy.selectRoute'))
const calcBodyStyle = computed(() => ({
    '--calc-route-pane-width': `${routePaneWidth.value}px`,
    '--calc-param-pane-width': `${paramPaneWidth.value}px`,
}))
const calcCenterStyle = computed(() => (
    layoutPaneHeight.value > 0
        ? { gridTemplateRows: `${layoutPaneHeight.value}px 8px minmax(150px, 1fr)` }
        : {}
))
const tractionResult = computed(() => buildSampleTractionResult(selectedStationRoute.value))

let stationSchemeLoadVersion = 0
let layoutLoadVersion = 0
let routeLoadVersion = 0
let routeEndLoadVersion = 0
let routeTimeLoadVersion = 0
let resizeTarget: 'left' | 'right' | 'row' | '' = ''
let previousBodyCursor = ''
let previousBodyUserSelect = ''

function readString(source: any, ...keys: string[]) {
    for (const key of keys) {
        const value = source?.[key]
        if (value !== undefined && value !== null) return String(value)
    }
    return ''
}

function normalizeStationRouteType(type: string) {
    return String(type || '').trim().replace(/\s+/g, '').toLowerCase()
}

function getStationRouteTypeLabelKey(type: string): string {
    return stationRouteTypeLabelKeys[normalizeStationRouteType(type)] || ''
}

function getStationRouteTypeLabel(type: string): string {
    const routeType = String(type || '').trim()
    if (!routeType) return ''

    const labelKey = getStationRouteTypeLabelKey(routeType)
    return labelKey ? t(labelKey) : routeType
}

function routeTypeValuesMatch(selectedType: string, routeType: string) {
    const selectedText = String(selectedType || '').trim()
    const routeText = String(routeType || '').trim()
    if (!selectedText) return true
    if (selectedText === routeText) return true

    const selectedLabelKey = getStationRouteTypeLabelKey(selectedText)
    const routeLabelKey = getStationRouteTypeLabelKey(routeText)
    return Boolean(selectedLabelKey && routeLabelKey && selectedLabelKey === routeLabelKey)
}

function buildRouteTypeFilterOptions(values: unknown[]): RouteListSelectOption[] {
    const optionsByKey = new Map<string, RouteListSelectOption>()
    for (const id of normalizeRouteListValues(values)) {
        const labelKey = getStationRouteTypeLabelKey(id)
        const key = labelKey || `raw:${id}`
        if (!optionsByKey.has(key)) optionsByKey.set(key, { id, name: getStationRouteTypeLabel(id) || id })
    }
    return [...optionsByKey.values()]
}

function getStationRouteHighlightColor(type: string) {
    const normalizedType = normalizeStationRouteType(type)
    if (normalizedType === 'arrival' || normalizedType === '接车' || normalizedType === '接车进路') return routeHighlightColors.arrival
    if (normalizedType === 'departure' || normalizedType === '发车' || normalizedType === '发车进路') return routeHighlightColors.departure
    if (normalizedType === 'locomotive' || normalizedType === '机车出入段' || normalizedType === '机车出入段进路' || normalizedType === '机车走行') return routeHighlightColors.locomotive
    return routeHighlightColors.shunting
}

function createEmptyRouteObjectOptions(): RouteObjectOptionMap {
    return { nodeList: [], linkList: [], switchList: [], cellList: [], signalList: [] }
}

function createEmptyRouteFilters(): StationRouteFilters {
    return { types: [], startNodeIds: [], endNodeIds: [], nodeIds: [], linkIds: [], cellIds: [], switchIds: [], signalIds: [] }
}

function parseRouteIdText(value: unknown): string[] {
    const text = String(value || '').trim()
    if (!text) return []
    try {
        const parsed = JSON.parse(text)
        if (Array.isArray(parsed)) return parsed.map((id) => String(id).trim()).filter(Boolean)
    } catch {}
    return text.split(/(?:\s*->\s*)|(?:\s*[,\n;]\s*)|\s+/).map((id) => id.trim()).filter(Boolean)
}

function normalizeRouteListValues(values: unknown[]): string[] {
    const seen = new Set<string>()
    return values.map((value) => String(value ?? '').trim()).filter((id) => id && !seen.has(id) && seen.add(id))
}

function normalizeRouteListOption(item: any): RouteListSelectOption | null {
    const id = readString(item, 'id', 'ID').trim()
    if (!id) return null
    return { id, name: readString(item, 'name', 'Name').trim() || id }
}

function buildRouteListOptions(...sources: any[]): RouteListSelectOption[] {
    const optionsById = new Map<string, RouteListSelectOption>()
    for (const source of sources) {
        for (const item of Array.isArray(source) ? source : []) {
            const option = normalizeRouteListOption(item)
            if (option && !optionsById.has(option.id)) optionsById.set(option.id, option)
        }
    }
    return [...optionsById.values()].sort((a, b) => a.name.localeCompare(b.name, undefined, { numeric: true }))
}

function buildRouteObjectOptions(data: any): RouteObjectOptionMap {
    return {
        nodeList: buildRouteListOptions(data?.nodes),
        linkList: buildRouteListOptions(data?.tracks, data?.links),
        switchList: buildRouteListOptions(data?.switches),
        cellList: buildRouteListOptions(data?.cells),
        signalList: buildRouteListOptions(data?.signals),
    }
}

function getRouteFilterReferencedIds(field: StationRouteObjectFilterField) {
    if (field === 'startNodeIds') return stationRoutes.value.map((route) => route.startNodeID)
    if (field === 'endNodeIds') return stationRoutes.value.map((route) => route.endNodeID)
    if (field === 'nodeIds') return stationRoutes.value.flatMap((route) => [route.startNodeID, route.endNodeID, ...parseRouteIdText(route.nodeList)])
    if (field === 'linkIds') return stationRoutes.value.flatMap((route) => parseRouteIdText(route.linkList))
    if (field === 'cellIds') return stationRoutes.value.flatMap((route) => parseRouteIdText(route.cellList))
    if (field === 'switchIds') return stationRoutes.value.flatMap((route) => parseRouteIdText(route.switchList))
    return stationRoutes.value.flatMap((route) => parseRouteIdText(route.signalList))
}

function getRouteFilterSelectOptions(control: RouteFilterControl) {
    const optionsById = new Map<string, RouteListSelectOption>()
    for (const option of routeObjectOptions.value[control.optionField]) optionsById.set(option.id, option)
    for (const id of normalizeRouteListValues([...getRouteFilterReferencedIds(control.field), ...routeFilters.value[control.field]])) {
        if (!optionsById.has(id)) optionsById.set(id, { id, name: id })
    }
    return [...optionsById.values()].sort((a, b) => a.name.localeCompare(b.name, undefined, { numeric: true }))
}

function clearRouteFilters() {
    routeFilters.value = createEmptyRouteFilters()
    clearRouteQuickFilters()
}

function clearRouteQuickFilters() {
    routeQuickTypeFilter.value = ''
    routeQuickStartRouteEndIds.value = []
    routeQuickEndRouteEndIds.value = []
}

function routeMatchesScalarFilter(selectedIds: string[], value: string) {
    return selectedIds.length === 0 || selectedIds.includes(String(value || '').trim())
}

function routeMatchesTypeFilter(selectedTypes: string[], routeType: string) {
    return selectedTypes.length === 0 || selectedTypes.some((selectedType) => routeTypeValuesMatch(selectedType, routeType))
}

function routeMatchesListFilter(selectedIds: string[], routeIds: string[]) {
    if (selectedIds.length === 0) return true
    const routeIdSet = new Set(normalizeRouteListValues(routeIds))
    return selectedIds.some((id) => routeIdSet.has(id))
}

function getStationRouteEndIdByNodeId(nodeID: string) {
    return stationRouteEndByBindingNodeId.value.get(String(nodeID || '').trim())?.id || ''
}

function routeMatchesQuickFilters(route: StationRoute) {
    return routeMatchesTypeFilter(routeQuickTypeFilter.value ? [routeQuickTypeFilter.value] : [], route.type) &&
        routeMatchesScalarFilter(routeQuickStartRouteEndIds.value, getStationRouteEndIdByNodeId(route.startNodeID)) &&
        routeMatchesScalarFilter(routeQuickEndRouteEndIds.value, getStationRouteEndIdByNodeId(route.endNodeID))
}

function routeMatchesFilters(route: StationRoute) {
    const filters = routeFilters.value
    return routeMatchesQuickFilters(route) &&
        routeMatchesTypeFilter(filters.types, route.type) &&
        routeMatchesScalarFilter(filters.startNodeIds, route.startNodeID) &&
        routeMatchesScalarFilter(filters.endNodeIds, route.endNodeID) &&
        routeMatchesListFilter(filters.nodeIds, [route.startNodeID, route.endNodeID, ...parseRouteIdText(route.nodeList)]) &&
        routeMatchesListFilter(filters.linkIds, parseRouteIdText(route.linkList)) &&
        routeMatchesListFilter(filters.cellIds, parseRouteIdText(route.cellList)) &&
        routeMatchesListFilter(filters.switchIds, parseRouteIdText(route.switchList)) &&
        routeMatchesListFilter(filters.signalIds, parseRouteIdText(route.signalList))
}

function normalizeStationSchemeOption(item: any): StationSchemeOption | null {
    const id = readString(item, 'id', 'ID').trim()
    if (!id) return null
    return { id, name: readString(item, 'name', 'Name').trim() || id }
}

function normalizeBooleanFlag(value: unknown, fallback = false) {
    if (typeof value === 'boolean') return value
    if (typeof value === 'number') return value !== 0
    if (typeof value === 'string') {
        const text = value.trim().toLowerCase()
        if (['true', '1', 'yes', 'y'].includes(text)) return true
        if (['false', '0', 'no', 'n'].includes(text)) return false
    }
    return fallback
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
        interruptCellList: readString(item, 'interruptCellList', 'InterruptCellList').trim(),
        signalList: readString(item, 'signalList', 'SignalList').trim(),
        allowanceTags: readString(item, 'allowanceTags', 'AllowanceTags').trim(),
        forbiddenTags: readString(item, 'forbiddenTags', 'ForbiddenTags').trim(),
        startNodeID: readString(item, 'startNodeID', 'StartNodeID').trim(),
        endNodeID: readString(item, 'endNodeID', 'EndNodeID').trim(),
        occupancyTimeConfigured: normalizeBooleanFlag(
            item?.occupancyTimeConfigured ??
            item?.OccupancyTimeConfigured ??
            item?.hasOccupancyTime ??
            item?.HasOccupancyTime ??
            item?.occupancyConfigured ??
            item?.OccupancyConfigured,
        ),
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

function getStationRouteEndDisplayName(routeEnd: StationRouteEndOption | null) {
    if (!routeEnd) return ''
    const tag = `${routeEnd.segmentTag || ''}${routeEnd.sidingTag || ''}`.trim()
    return tag ? `${tag} (${routeEnd.id})` : routeEnd.id
}

function routeHasOccupancyTime(route: StationRoute) {
    return route.occupancyTimeConfigured
}

function normalizeNullableInteger(value: unknown): number | null {
    if (value === null || value === undefined || value === '') return null
    const number = Number(value)
    return Number.isFinite(number) ? Math.trunc(number) : null
}

function normalizeStationRouteTime(item: any): StationRouteTime | null {
    const cellID = readString(item, 'cellID', 'CellID').trim()
    if (!cellID) return null
    return {
        instanceID: readString(item, 'instanceID', 'InstanceID').trim(),
        stationSchemeID: readString(item, 'stationSchemeID', 'StationSchemeID').trim(),
        routeID: readString(item, 'routeID', 'RouteID').trim(),
        trainTypeID: readString(item, 'trainTypeID', 'TrainTypeID').trim(),
        cellID,
        startOccupationShift: normalizeNullableInteger(item?.startOccupationShift ?? item?.StartOccupationShift),
        endOccupationShift: normalizeNullableInteger(item?.endOccupationShift ?? item?.EndOccupationShift),
        isInterruptCell: normalizeBooleanFlag(item?.isInterruptCell ?? item?.IsInterruptCell),
    }
}

function getRouteTimeDefaultWindow(rows: StationRouteTime[], interruptCellSet: Set<string>) {
    const routeRows = rows.filter((row) => !interruptCellSet.has(row.cellID.toLowerCase()))
    const startValues = routeRows
        .map((row) => normalizeNullableInteger(row.startOccupationShift))
        .filter((value): value is number => value !== null)
    const endValues = routeRows
        .map((row) => normalizeNullableInteger(row.endOccupationShift))
        .filter((value): value is number => value !== null)
    return {
        startOccupationShift: startValues.length > 0 ? Math.min(...startValues) : 0,
        endOccupationShift: endValues.length > 0 ? Math.max(...endValues) : 0,
    }
}

function fillInterruptRouteTime(
    row: StationRouteTime,
    interruptCellSet: Set<string>,
    defaultWindow: { startOccupationShift: number; endOccupationShift: number },
): StationRouteTime {
    const isInterruptCell = row.isInterruptCell || interruptCellSet.has(row.cellID.toLowerCase())
    if (!isInterruptCell) return { ...row, isInterruptCell }

    const startOccupationShift = normalizeNullableInteger(row.startOccupationShift)
    const endOccupationShift = normalizeNullableInteger(row.endOccupationShift)
    const hasDefaultZeroShift =
        (startOccupationShift ?? 0) === 0 &&
        (endOccupationShift ?? 0) === 0 &&
        (defaultWindow.startOccupationShift !== 0 || defaultWindow.endOccupationShift !== 0)
    return {
        ...row,
        startOccupationShift: hasDefaultZeroShift
            ? defaultWindow.startOccupationShift
            : (startOccupationShift ?? defaultWindow.startOccupationShift),
        endOccupationShift: hasDefaultZeroShift
            ? defaultWindow.endOccupationShift
            : (endOccupationShift ?? defaultWindow.endOccupationShift),
        isInterruptCell,
    }
}

function mergeRouteTimesWithSelectedRouteCells(rows: StationRouteTime[], includeMissingCells = rows.length > 0) {
    const cellIDs = selectedRouteOccupancyCellIds.value
    const interruptCellSet = new Set(selectedRouteInterruptCellIds.value.map((id) => id.toLowerCase()))
    const defaultWindow = getRouteTimeDefaultWindow(rows, interruptCellSet)
    if (cellIDs.length === 0) {
        return rows.map((row) => fillInterruptRouteTime(row, interruptCellSet, defaultWindow))
    }

    const usedIndexes = new Set<number>()
    const mergedRows: StationRouteTime[] = []
    cellIDs.forEach((cellID) => {
        const lowerCellID = cellID.toLowerCase()
        const rowIndex = rows.findIndex((row, index) => !usedIndexes.has(index) && row.cellID.toLowerCase() === lowerCellID)
        if (rowIndex >= 0) {
            const row = rows[rowIndex]
            if (!row) return
            usedIndexes.add(rowIndex)
            mergedRows.push(fillInterruptRouteTime({
                ...row,
                cellID,
                isInterruptCell: row.isInterruptCell || interruptCellSet.has(lowerCellID),
            }, interruptCellSet, defaultWindow))
            return
        }

        if (includeMissingCells) {
            mergedRows.push({
                instanceID: selectedInstanceId.value,
                stationSchemeID: currentStationSchemeId.value.trim(),
                routeID: selectedRouteId.value,
                trainTypeID: '',
                cellID,
                startOccupationShift: interruptCellSet.has(lowerCellID) ? defaultWindow.startOccupationShift : 0,
                endOccupationShift: interruptCellSet.has(lowerCellID) ? defaultWindow.endOccupationShift : 0,
                isInterruptCell: interruptCellSet.has(lowerCellID),
            })
        }
    })

    if (cellIDs.length === 0) {
        rows.forEach((row, index) => {
            if (usedIndexes.has(index)) return
            mergedRows.push(fillInterruptRouteTime({
                ...row,
                isInterruptCell: row.isInterruptCell || interruptCellSet.has(row.cellID.toLowerCase()),
            }, interruptCellSet, defaultWindow))
        })
    }
    return mergedRows
}

function getUniformRouteTimeShift(field: 'startOccupationShift' | 'endOccupationShift') {
    if (routeTimes.value.length === 0) return 0
    const first = routeTimes.value[0]?.[field] ?? null
    return routeTimes.value.every((row) => row[field] === first) ? first : null
}

function syncUniformShiftDraftFromRows() {
    uniformStartOccupationShift.value = getUniformRouteTimeShift('startOccupationShift')
    uniformEndOccupationShift.value = getUniformRouteTimeShift('endOccupationShift')
}

function setSelectedRouteOccupancyConfigured(configured: boolean) {
    const route = selectedStationRoute.value
    if (route) route.occupancyTimeConfigured = configured
}

function getCellDisplayName(cellID: string) {
    const id = String(cellID || '').trim()
    if (!id) return ''
    const cell = layoutCells.value.find((item) => String(item.id || '').trim() === id)
    return String(cell?.name || id)
}

function getLayoutTrackItems() {
    const data = layoutData.value || {}
    return [
        ...(Array.isArray(data.tracks) ? data.tracks : []),
        ...(Array.isArray(data.links) ? data.links : []),
    ]
}

function getLinkDisplayName(linkID: string) {
    const id = String(linkID || '').trim()
    if (!id) return ''
    const link = mapById(getLayoutTrackItems()).get(id)
    return readString(link, 'name', 'Name').trim() || id
}

function getRouteLinkLength(linkID: string, index: number) {
    const link = mapById(getLayoutTrackItems()).get(linkID)
    const x1 = Number(link?.x1 ?? link?.X1)
    const y1 = Number(link?.y1 ?? link?.Y1)
    const x2 = Number(link?.x2 ?? link?.X2)
    const y2 = Number(link?.y2 ?? link?.Y2)
    const length = Math.hypot(x2 - x1, y2 - y1)
    return Number.isFinite(length) && length > 1 ? Math.max(30, length) : 80 + (index % 4) * 18
}

function getCellLinkIDs(cellID: string) {
    const cell = layoutCells.value.find((item) => String(item.id || '').trim() === cellID)
    return parseRouteIdText(cell?.linkIDList)
}

function getSampleVelocity(distance: number, totalDistance: number) {
    const ratio = totalDistance > 0 ? clampValue(distance / totalDistance, 0, 1) : 0
    const wave = Math.sin(Math.PI * ratio)
    const ripple = Math.sin(ratio * Math.PI * 5) * 3
    return Math.max(8, Number((18 + 58 * wave + ripple).toFixed(1)))
}

function buildSampleTractionResult(route: StationRoute | null): TractionCalculationResult | null {
    if (!route) return null
    const cellIDs = parseRouteIdText(route.cellList)
    const fallbackLinkIDs = cellIDs.flatMap((cellID) => getCellLinkIDs(cellID))
    const linkIDs = normalizeRouteListValues(parseRouteIdText(route.linkList).length > 0 ? parseRouteIdText(route.linkList) : fallbackLinkIDs)
    if (linkIDs.length === 0) return null

    const linkLengths = linkIDs.map((linkID, index) => getRouteLinkLength(linkID, index))
    const totalDistance = linkLengths.reduce((sum, length) => sum + length, 0)
    let distance = 0
    let time = 0
    const velocityPoints: TractionPoint[] = [{ distance: 0, value: getSampleVelocity(0, totalDistance) }]
    const timePoints: TractionPoint[] = [{ distance: 0, value: 0 }]
    const linkResults = linkIDs.map((linkID, index) => {
        const length = linkLengths[index] ?? getRouteLinkLength(linkID, index)
        const distanceStart = distance
        const distanceEnd = distance + length
        const speedIn = getSampleVelocity(distanceStart, totalDistance)
        const speedOut = getSampleVelocity(distanceEnd, totalDistance)
        const averageSpeed = Math.max(8, (speedIn + speedOut) / 2)
        const entryTime = time
        const exitTime = time + length / (averageSpeed / 3.6)
        distance = distanceEnd
        time = exitTime
        velocityPoints.push({ distance: distanceEnd, value: speedOut })
        timePoints.push({ distance: distanceEnd, value: exitTime })
        return {
            linkID,
            linkName: getLinkDisplayName(linkID),
            length,
            distanceStart,
            distanceEnd,
            entryTime,
            exitTime,
            speedIn,
            speedOut,
        }
    })

    const cellResults = buildSampleTractionCellResults(cellIDs, linkResults)
    const maxVelocity = Math.max(...velocityPoints.map((point) => point.value), 1)
    const maxTime = Math.max(...timePoints.map((point) => point.value), 1)
    return {
        routeLabel: route.description ? `${route.id} - ${route.description}` : route.id,
        totalDistance,
        totalTime: time,
        velocityCurve: buildTractionCurveRender(velocityPoints, totalDistance, maxVelocity),
        timeCurve: buildTractionCurveRender(timePoints, totalDistance, maxTime),
        linkResults,
        cellResults,
    }
}

function buildSampleTractionCellResults(cellIDs: string[], linkResults: TractionLinkResult[]) {
    const routeLinkIDSet = new Set(linkResults.map((link) => link.linkID))
    return cellIDs.map((cellID, index) => {
        let matchedLinks = getCellLinkIDs(cellID)
            .filter((linkID) => routeLinkIDSet.has(linkID))
            .map((linkID) => linkResults.find((link) => link.linkID === linkID))
            .filter((link): link is TractionLinkResult => Boolean(link))

        if (matchedLinks.length === 0 && cellIDs.length > 0) {
            const startIndex = Math.floor(index * linkResults.length / cellIDs.length)
            const endIndex = Math.max(startIndex, Math.ceil((index + 1) * linkResults.length / cellIDs.length) - 1)
            matchedLinks = linkResults.slice(startIndex, endIndex + 1)
        }

        const startTime = matchedLinks.length > 0 ? Math.min(...matchedLinks.map((link) => link.entryTime)) : 0
        const endTime = matchedLinks.length > 0 ? Math.max(...matchedLinks.map((link) => link.exitTime)) : startTime
        return {
            cellID,
            cellName: getCellDisplayName(cellID) || cellID,
            startTime,
            endTime,
        }
    })
}

function createTractionTicks(maxValue: number, count = 4) {
    const safeMax = Math.max(1, maxValue)
    return Array.from({ length: count + 1 }, (_, index) => Number((safeMax * index / count).toFixed(1)))
}

function buildTractionCurveRender(points: TractionPoint[], maxDistance: number, maxValue: number): TractionCurveRender {
    const width = 480
    const height = 220
    const plotLeft = 42
    const plotRight = width - 16
    const plotTop = 14
    const plotBottom = height - 34
    const safeDistance = Math.max(1, maxDistance)
    const safeValue = Math.max(1, maxValue)
    const xOf = (distance: number) => plotLeft + (distance / safeDistance) * (plotRight - plotLeft)
    const yOf = (value: number) => plotBottom - (value / safeValue) * (plotBottom - plotTop)
    return {
        width,
        height,
        plotLeft,
        plotRight,
        plotTop,
        plotBottom,
        points: points.map((point) => `${xOf(point.distance).toFixed(1)},${yOf(point.value).toFixed(1)}`).join(' '),
        xTicks: createTractionTicks(safeDistance).map((value) => ({ value, x: xOf(value), y: 0 })),
        yTicks: createTractionTicks(safeValue).map((value) => ({ value, x: 0, y: yOf(value) })),
    }
}

function openTractionCalculationDialog() {
    if (!selectedStationRoute.value) {
        ElMessage.warning(t('calculationParameters.manager.messages.selectRoute'))
        return
    }
    tractionDialogVisible.value = true
}

function formatDistance(value: number) {
    return `${Number(value || 0).toFixed(1)} m`
}

function formatDistanceTick(value: number) {
    return Number(value || 0).toFixed(0)
}

function formatSpeed(value: number) {
    return `${Number(value || 0).toFixed(1)} km/h`
}

function formatSpeedTick(value: number) {
    return Number(value || 0).toFixed(0)
}

function formatTime(value: number) {
    return `${Number(value || 0).toFixed(1)} s`
}

function formatTimeTick(value: number) {
    return Number(value || 0).toFixed(0)
}

function handleGanttTimeChange(change: GanttTimeChange) {
    const row = routeTimes.value[change.timeIndex]
    if (!row || row.cellID !== change.cellID) return
    row.startOccupationShift = change.startOccupationShift
    row.endOccupationShift = change.endOccupationShift
    syncUniformShiftDraftFromRows()
}

function applyUniformRouteTimeShifts() {
    const startShift = normalizeNullableInteger(uniformStartOccupationShift.value)
    const endShift = normalizeNullableInteger(uniformEndOccupationShift.value)
    routeTimes.value = routeTimes.value.map((row) => ({
        ...row,
        startOccupationShift: startShift,
        endOccupationShift: endShift,
    }))
    syncUniformShiftDraftFromRows()
    ElMessage.success(t('calculationParameters.manager.uniformShift.applied'))
}

function openBatchSetDialog() {
    if (!selectedInstanceId.value || !currentStationSchemeId.value.trim()) {
        ElMessage.warning(t('calculationParameters.batchSet.selectScheme'))
        return
    }
    if (routeTypeSummaries.value.length === 0) {
        ElMessage.warning(t('calculationParameters.batchSet.empty'))
        return
    }

    const previousByType = new Map(batchRouteTimeSettings.value.map((setting) => [setting.type, setting]))
    batchRouteTimeSettings.value = routeTypeSummaries.value.map(({ type, routes }) => {
        const previous = previousByType.get(type)
        const availableRouteIds = new Set(routes.map((route) => route.id))
        return {
            type,
            routeCount: routes.length,
            routes,
            selectedRouteIds: previous
                ? previous.selectedRouteIds.filter((routeId) => availableRouteIds.has(routeId))
                : routes.map((route) => route.id),
            startOccupationShift: previous?.startOccupationShift ?? 0,
            endOccupationShift: previous?.endOccupationShift ?? 0,
        }
    })
    batchSetDialogVisible.value = true
}

function openBatchRouteList(type: string) {
    activeBatchRouteType.value = type
    batchRouteListDialogVisible.value = true
}

function toggleActiveBatchRouteListAll(value: unknown) {
    const setting = activeBatchRouteSetting.value
    if (!setting) return
    setting.selectedRouteIds = value === true ? setting.routes.map((route) => route.id) : []
}

function readResponseCount(source: any, ...keys: string[]) {
    for (const key of keys) {
        const value = Number(source?.[key])
        if (Number.isFinite(value)) return value
    }
    return 0
}

async function applyBatchSetRouteTimes() {
    const instanceID = selectedInstanceId.value
    const stationSchemeID = currentStationSchemeId.value.trim()
    if (!instanceID || !stationSchemeID) {
        ElMessage.warning(t('calculationParameters.batchSet.selectScheme'))
        return
    }
    if (batchRouteTimeSettings.value.length === 0) {
        ElMessage.warning(t('calculationParameters.batchSet.empty'))
        return
    }
    const selectedSettings = batchRouteTimeSettings.value.filter((setting) => setting.selectedRouteIds.length > 0)
    if (selectedSettings.length === 0) {
        ElMessage.warning(t('calculationParameters.batchSet.emptySelection'))
        return
    }

    batchSettingRouteTimes.value = true
    try {
        const response = await axios.put('/StationLayout/BatchSetStationRouteTimes', {
            instanceID,
            stationSchemeID,
            trainTypeID: '',
            settings: selectedSettings.map((setting) => ({
                type: setting.type,
                routeIDs: setting.selectedRouteIds,
                startOccupationShift: normalizeNullableInteger(setting.startOccupationShift),
                endOccupationShift: normalizeNullableInteger(setting.endOccupationShift),
            })),
        })
        batchSetDialogVisible.value = false
        ElMessage.success(t('calculationParameters.batchSet.success', {
            routeCount: readResponseCount(response.data, 'routeCount', 'RouteCount'),
            rowCount: readResponseCount(response.data, 'rowCount', 'RowCount'),
        }))
        await loadStationRoutes()
        if (selectedRouteId.value) await loadRouteTimes()
    } catch (error) {
        console.error('Failed to batch set station route times:', error)
        ElMessage.error(t('calculationParameters.batchSet.failed'))
    } finally {
        batchSettingRouteTimes.value = false
    }
}

function getLayoutDisplayStyles(data: any) {
    const styles = data?.metadata?.displayStyles
    return styles && typeof styles === 'object' && !Array.isArray(styles) ? styles : {}
}

function getLayoutGridSettings(data: any) {
    const settings = data?.metadata?.gridSettings
    return settings && typeof settings === 'object' && !Array.isArray(settings) ? settings : {}
}

function getLayoutGridSpacing(data: any) {
    const settings = getLayoutGridSettings(data)
    const value = Number(settings.spacing ?? settings.Spacing ?? 20)
    return Number.isFinite(value) && value > 0 ? value : 20
}

function getLayoutGridVisible(data: any) {
    const settings = getLayoutGridSettings(data)
    const value = settings.showGrid ?? settings.ShowGrid
    if (typeof value === 'boolean') return value
    if (typeof value === 'string') return !['false', '0', 'no'].includes(value.toLowerCase())
    return true
}

function getLayoutCells(data: any) {
    return (Array.isArray(data?.cells) ? data.cells : [])
        .map((cell: any) => ({
            id: readString(cell, 'id', 'ID').trim(),
            name: readString(cell, 'name', 'Name').trim() || readString(cell, 'id', 'ID').trim(),
            linkIDList: readString(cell, 'linkIDList', 'LinkIDList').trim(),
        }))
        .filter((cell: any) => cell.id || cell.name || cell.linkIDList)
}

function toFiniteNumber(value: unknown) {
    const number = Number(value)
    return Number.isFinite(number) ? number : 0
}

function createEmptyRect(): DataRect {
    return { minX: Infinity, minY: Infinity, maxX: -Infinity, maxY: -Infinity }
}

function isRectEmpty(rect: DataRect) {
    return !Number.isFinite(rect.minX) || !Number.isFinite(rect.minY) || !Number.isFinite(rect.maxX) || !Number.isFinite(rect.maxY)
}

function includePoint(rect: DataRect, point: any) {
    if (!point) return
    const x = Number(point.x ?? point.X)
    const y = Number(point.y ?? point.Y)
    if (!Number.isFinite(x) || !Number.isFinite(y)) return
    rect.minX = Math.min(rect.minX, x)
    rect.minY = Math.min(rect.minY, y)
    rect.maxX = Math.max(rect.maxX, x)
    rect.maxY = Math.max(rect.maxY, y)
}

function includeTrack(rect: DataRect, track: any) {
    includePoint(rect, { x: track?.x1 ?? track?.X1, y: track?.y1 ?? track?.Y1 })
    includePoint(rect, { x: track?.x2 ?? track?.X2, y: track?.y2 ?? track?.Y2 })
}

function mapById(items: any[]) {
    return new Map((Array.isArray(items) ? items : []).map((item) => [readString(item, 'id', 'ID').trim(), item]))
}

function buildRouteDataRect(route: StationRoute | null) {
    if (!route) return null
    const data = layoutData.value || {}
    const rect = createEmptyRect()
    const nodesById = mapById(data.nodes)
    const tracksById = mapById(data.tracks || data.links)
    const switchesById = mapById(data.switches)
    const signalsById = mapById(data.signals)
    const cellsById = mapById(data.cells)

    for (const id of normalizeRouteListValues([route.startNodeID, route.endNodeID, ...parseRouteIdText(route.nodeList)])) {
        includePoint(rect, nodesById.get(id))
    }
    for (const id of parseRouteIdText(route.linkList)) includeTrack(rect, tracksById.get(id))
    for (const id of parseRouteIdText(route.switchList)) includePoint(rect, switchesById.get(id)?.position)
    for (const id of parseRouteIdText(route.signalList)) includePoint(rect, signalsById.get(id)?.position)
    for (const cellId of parseRouteIdText(route.cellList)) {
        for (const linkId of parseRouteIdText(readString(cellsById.get(cellId), 'linkIDList', 'LinkIDList'))) {
            includeTrack(rect, tracksById.get(linkId))
        }
    }

    return isRectEmpty(rect) ? null : rect
}

function fitDataRectInLayout(rect: DataRect | null, options: { screenMargin?: number; padding?: number } = {}) {
    if (!rect) return
    const screenMargin = Math.max(0, Number(options.screenMargin ?? 72))
    const viewport = layoutViewportRef.value
    if (viewport) {
        const width = Math.max(1, rect.maxX - rect.minX)
        const height = Math.max(1, rect.maxY - rect.minY)
        const scale = Math.max(0.25, Math.min(4, Math.min((viewport.clientWidth - screenMargin * 2) / width, (viewport.clientHeight - screenMargin * 2) / height)))
        layoutScaleX.value = Number(scale.toFixed(2))
        layoutScaleY.value = Number(scale.toFixed(2))
    }
    nextTick(() => stationLayoutEditorRef.value?.scrollDataRectIntoView(rect, {
        screenMargin,
        padding: options.padding ?? 180,
    }))
}

function fitRouteInLayout(rect: DataRect | null) {
    fitDataRectInLayout(rect, { screenMargin: 72, padding: 180 })
}

function fitFullLayout() {
    const rect = stationLayoutEditorRef.value?.getFullViewRect?.({ screenMargin: 80 })
    fitDataRectInLayout(rect, { screenMargin: 48, padding: 160 })
}

function selectStationRoute(row: StationRoute) {
    selectedRouteId.value = row.id
    fitRouteInLayout(buildRouteDataRect(row))
    void loadRouteTimes()
}

function selectStationRouteById(id: string) {
    const row = stationRoutes.value.find((item) => item.id === id) || stationRoutes.value[0]
    if (row) selectStationRoute(row)
    else selectedRouteId.value = ''
}

function clearLayout() {
    layoutDisplayStyles.value = {}
    layoutCells.value = []
    layoutData.value = {}
    layoutGridSpacing.value = 20
    layoutScaleX.value = 1
    layoutScaleY.value = 1
    showLayoutGrid.value = true
    showLayoutNodes.value = true
    showLayoutCurveArc.value = true
    showLayoutCellNames.value = false
    routeObjectOptions.value = createEmptyRouteObjectOptions()
    stationLayoutEditorRef.value?.clearElements()
}

function clearStationRoutes() {
    routeLoadVersion++
    routeTimeLoadVersion++
    stationRoutes.value = []
    selectedRouteId.value = ''
    routeTimes.value = []
    syncUniformShiftDraftFromRows()
    clearRouteFilters()
}

function clearStationRouteEnds() {
    routeEndLoadVersion++
    stationRouteEndOptions.value = []
    loadingRouteEnds.value = false
    routeQuickStartRouteEndIds.value = []
    routeQuickEndRouteEndIds.value = []
}

async function loadStationSchemes() {
    const instanceID = selectedInstanceId.value
    const loadVersion = ++stationSchemeLoadVersion
    if (!instanceID) {
        stationSchemeOptions.value = []
        currentStationSchemeId.value = ''
        return
    }
    loadingStationSchemes.value = true
    try {
        const response = await axios.get('/StationLayout/GetStationSchemes', { params: { instanceID } })
        if (loadVersion !== stationSchemeLoadVersion || instanceID !== selectedInstanceId.value) return
        stationSchemeOptions.value = (Array.isArray(response.data) ? response.data : []).map(normalizeStationSchemeOption).filter((item): item is StationSchemeOption => item !== null)
    } catch (error) {
        console.error('Failed to load calculation parameter station schemes:', error)
        ElMessage.error(t('stationLayout.messages.loadSchemesFailed'))
    } finally {
        if (loadVersion === stationSchemeLoadVersion) loadingStationSchemes.value = false
    }
}

async function loadLayout() {
    const instanceID = selectedInstanceId.value
    const stationSchemeID = currentStationSchemeId.value.trim()
    if (!instanceID) {
        clearLayout()
        return
    }
    const loadVersion = ++layoutLoadVersion
    loadingData.value = true
    try {
        const params: Record<string, string> = { instanceID }
        if (stationSchemeID) params.stationSchemeID = stationSchemeID
        const response = await axios.post('/StationLayout/GetJson', null, { params })
        if (loadVersion !== layoutLoadVersion || instanceID !== selectedInstanceId.value) return
        currentStationSchemeId.value = readString(response.data?.metadata, 'stationSchemeID', 'StationSchemeID').trim() || currentStationSchemeId.value
        layoutData.value = response.data || {}
        layoutDisplayStyles.value = getLayoutDisplayStyles(response.data)
        layoutCells.value = getLayoutCells(response.data)
        layoutGridSpacing.value = getLayoutGridSpacing(response.data)
        showLayoutGrid.value = getLayoutGridVisible(response.data)
        routeObjectOptions.value = buildRouteObjectOptions(response.data)
        await nextTick()
        stationLayoutEditorRef.value?.loadDataFromJson(response.data)
    } catch (error) {
        console.error('Failed to load calculation parameter layout:', error)
        clearLayout()
        ElMessage.error(t('routeDesign.messages.loadFailed'))
    } finally {
        if (loadVersion === layoutLoadVersion) loadingData.value = false
    }
}

async function loadStationRoutes() {
    const instanceID = selectedInstanceId.value
    const stationSchemeID = currentStationSchemeId.value.trim()
    if (!instanceID || !stationSchemeID) {
        clearStationRoutes()
        return
    }
    const loadVersion = ++routeLoadVersion
    const previousId = selectedRouteId.value
    loadingRoutes.value = true
    try {
        const response = await axios.get('/StationLayout/GetStationRoutes', { params: { instanceID, stationSchemeID } })
        if (loadVersion !== routeLoadVersion || instanceID !== selectedInstanceId.value || stationSchemeID !== currentStationSchemeId.value.trim()) return
        stationRoutes.value = (Array.isArray(response.data) ? response.data : []).map(normalizeStationRoute).filter((item): item is StationRoute => item !== null)
        await nextTick()
        selectStationRouteById(previousId)
    } catch (error) {
        console.error('Failed to load calculation parameter routes:', error)
        stationRoutes.value = []
        selectedRouteId.value = ''
        routeTimes.value = []
        syncUniformShiftDraftFromRows()
        ElMessage.error(t('routeDesign.stationRoute.messages.loadFailed'))
    } finally {
        if (loadVersion === routeLoadVersion) loadingRoutes.value = false
    }
}

async function loadStationRouteEnds() {
    const instanceID = selectedInstanceId.value
    const stationSchemeID = currentStationSchemeId.value.trim()
    if (!instanceID || !stationSchemeID) {
        clearStationRouteEnds()
        return
    }

    const loadVersion = ++routeEndLoadVersion
    loadingRouteEnds.value = true
    try {
        const response = await axios.get('/StationLayout/GetStationRouteEnds', {
            params: { instanceID, stationSchemeID },
        })
        if (loadVersion !== routeEndLoadVersion || instanceID !== selectedInstanceId.value || stationSchemeID !== currentStationSchemeId.value.trim()) return
        stationRouteEndOptions.value = (Array.isArray(response.data) ? response.data : [])
            .map(normalizeStationRouteEndOption)
            .filter((item): item is StationRouteEndOption => item !== null)
    } catch (error) {
        if (loadVersion !== routeEndLoadVersion || instanceID !== selectedInstanceId.value || stationSchemeID !== currentStationSchemeId.value.trim()) return
        console.error('Failed to load calculation parameter route ends:', error)
        stationRouteEndOptions.value = []
        ElMessage.error(t('routeDesign.routeEnd.messages.loadFailed'))
    } finally {
        if (loadVersion === routeEndLoadVersion) loadingRouteEnds.value = false
    }
}

async function loadRouteTimes() {
    const instanceID = selectedInstanceId.value
    const stationSchemeID = currentStationSchemeId.value.trim()
    const routeID = selectedRouteId.value
    if (!instanceID || !stationSchemeID || !routeID) {
        routeTimes.value = []
        syncUniformShiftDraftFromRows()
        return
    }

    const loadVersion = ++routeTimeLoadVersion
    loadingRouteTimes.value = true
    try {
        const response = await axios.get('/StationLayout/GetStationRouteTimes', {
            params: { instanceID, stationSchemeID, routeID, trainTypeID: '' },
        })
        if (loadVersion !== routeTimeLoadVersion || routeID !== selectedRouteId.value) return
        routeTimes.value = mergeRouteTimesWithSelectedRouteCells((Array.isArray(response.data) ? response.data : [])
            .map(normalizeStationRouteTime)
            .filter((item): item is StationRouteTime => item !== null))
        syncUniformShiftDraftFromRows()
        setSelectedRouteOccupancyConfigured(routeTimes.value.length > 0)
    } catch (error) {
        if (loadVersion !== routeTimeLoadVersion) return
        console.error('Failed to load station route times:', error)
        routeTimes.value = []
        syncUniformShiftDraftFromRows()
        ElMessage.error(t('calculationParameters.manager.messages.loadFailed'))
    } finally {
        if (loadVersion === routeTimeLoadVersion) loadingRouteTimes.value = false
    }
}

async function createRouteTimes() {
    const instanceID = selectedInstanceId.value
    const stationSchemeID = currentStationSchemeId.value.trim()
    const routeID = selectedRouteId.value
    if (!instanceID || !stationSchemeID || !routeID) {
        ElMessage.warning(t('calculationParameters.manager.messages.selectRoute'))
        return
    }

    routeTimeLoadVersion++
    creatingRouteTimes.value = true
    try {
        const response = await axios.post('/StationLayout/CreateStationRouteTimes', {
            instanceID,
            stationSchemeID,
            routeID,
            trainTypeID: '',
        })
        routeTimes.value = mergeRouteTimesWithSelectedRouteCells((Array.isArray(response.data) ? response.data : [])
            .map(normalizeStationRouteTime)
            .filter((item): item is StationRouteTime => item !== null), true)
        syncUniformShiftDraftFromRows()
        setSelectedRouteOccupancyConfigured(routeTimes.value.length > 0)
        ElMessage.success(t('calculationParameters.manager.messages.createSuccess', { count: routeTimes.value.length }))
    } catch (error) {
        console.error('Failed to create station route times:', error)
        ElMessage.error(t('calculationParameters.manager.messages.createFailed'))
    } finally {
        creatingRouteTimes.value = false
    }
}

async function saveRouteTimes() {
    const instanceID = selectedInstanceId.value
    const stationSchemeID = currentStationSchemeId.value.trim()
    const routeID = selectedRouteId.value
    if (!instanceID || !stationSchemeID || !routeID) {
        ElMessage.warning(t('calculationParameters.manager.messages.selectRoute'))
        return
    }
    if (routeTimes.value.length === 0) {
        ElMessage.warning(t('calculationParameters.manager.messages.emptySave'))
        return
    }

    routeTimeLoadVersion++
    savingRouteTimes.value = true
    try {
        const response = await axios.put('/StationLayout/SaveStationRouteTimes', {
            instanceID,
            stationSchemeID,
            routeID,
            trainTypeID: '',
            times: routeTimes.value.map((row) => ({
                cellID: row.cellID,
                startOccupationShift: normalizeNullableInteger(row.startOccupationShift),
                endOccupationShift: normalizeNullableInteger(row.endOccupationShift),
            })),
        })
        routeTimes.value = mergeRouteTimesWithSelectedRouteCells((Array.isArray(response.data) ? response.data : [])
            .map(normalizeStationRouteTime)
            .filter((item): item is StationRouteTime => item !== null), true)
        syncUniformShiftDraftFromRows()
        setSelectedRouteOccupancyConfigured(routeTimes.value.length > 0)
        ElMessage.success(t('calculationParameters.manager.messages.saveSuccess'))
    } catch (error) {
        console.error('Failed to save station route times:', error)
        ElMessage.error(t('calculationParameters.manager.messages.saveFailed'))
    } finally {
        savingRouteTimes.value = false
    }
}

async function refreshForInstance() {
    await loadStationSchemes()
    await loadLayout()
    await Promise.all([loadStationRoutes(), loadStationRouteEnds()])
}

async function refreshRouteList() {
    await Promise.all([loadStationRoutes(), loadStationRouteEnds()])
}

async function handleStationSchemeChange() {
    clearRouteFilters()
    await loadLayout()
    await Promise.all([loadStationRoutes(), loadStationRouteEnds()])
}

function clampValue(value: number, min: number, max: number) {
    return Math.max(min, Math.min(max, value))
}

function startResize(target: 'left' | 'right' | 'row', cursor: string, event: MouseEvent) {
    event.preventDefault()
    resizeTarget = target
    isResizing.value = true
    previousBodyCursor = document.body.style.cursor
    previousBodyUserSelect = document.body.style.userSelect
    document.body.style.cursor = cursor
    document.body.style.userSelect = 'none'
    window.addEventListener('mousemove', onResizeMouseMove)
    window.addEventListener('mouseup', finishResize)
}

function startColumnResize(target: 'left' | 'right', event: MouseEvent) {
    startResize(target, 'col-resize', event)
}

function startRowResize(event: MouseEvent) {
    startResize('row', 'row-resize', event)
}

function onResizeMouseMove(event: MouseEvent) {
    if (!resizeTarget) return

    if (resizeTarget === 'left' || resizeTarget === 'right') {
        const rect = bodyRef.value?.getBoundingClientRect()
        if (!rect) return
        const centerMin = 360
        const resizerTotal = 16
        if (resizeTarget === 'left') {
            const maxWidth = rect.width - paramPaneWidth.value - centerMin - resizerTotal
            routePaneWidth.value = clampValue(event.clientX - rect.left, 240, Math.max(240, maxWidth))
        } else {
            const maxWidth = rect.width - routePaneWidth.value - centerMin - resizerTotal
            paramPaneWidth.value = clampValue(rect.right - event.clientX, 240, Math.max(240, maxWidth))
        }
        return
    }

    const rect = centerPaneRef.value?.getBoundingClientRect()
    if (!rect) return
    const maxHeight = rect.height - 150 - 8
    layoutPaneHeight.value = clampValue(event.clientY - rect.top, 180, Math.max(180, maxHeight))
}

function finishResize() {
    if (!resizeTarget) return
    resizeTarget = ''
    isResizing.value = false
    window.removeEventListener('mousemove', onResizeMouseMove)
    window.removeEventListener('mouseup', finishResize)
    document.body.style.cursor = previousBodyCursor
    document.body.style.userSelect = previousBodyUserSelect
}

function resetRoutePaneWidth() {
    routePaneWidth.value = 320
}

function resetParamPaneWidth() {
    paramPaneWidth.value = 300
}

function resetLayoutPaneHeight() {
    layoutPaneHeight.value = 0
}

watch(() => props.selectedInstanceId, () => {
    currentStationSchemeId.value = ''
    clearStationRoutes()
    clearStationRouteEnds()
    void refreshForInstance()
}, { immediate: true })

onBeforeUnmount(() => {
    finishResize()
})
</script>

<style scoped>
.calc-params-page {
    display: flex;
    flex-direction: column;
    width: 100%;
    height: calc(100dvh - 118px);
    min-height: 420px;
    border: 1px solid #d8e2ef;
    background: #fff;
    overflow: hidden;
}

.calc-params-toolbar {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 12px;
    min-height: 40px;
    padding: 6px 10px;
    border-bottom: 1px solid #d8e2ef;
    background: #f7fafc;
}

.calc-scheme-control,
.calc-display-toolbar,
.calc-switch-control,
.calc-scale-control {
    display: inline-flex;
    align-items: center;
    min-width: 0;
}

.calc-scheme-control,
.calc-switch-control,
.calc-scale-control {
    gap: 6px;
}

.calc-display-toolbar {
    gap: 10px;
    flex-wrap: wrap;
    padding: 2px 8px;
    border: 1px solid #dbe5f0;
    border-radius: 6px;
    background: #fff;
    color: #4c5968;
    font-size: 12px;
}

.calc-control-label,
.calc-switch-control span,
.calc-scale-control span {
    color: #4c5968;
    font-size: 12px;
    white-space: nowrap;
}

.calc-scheme-select {
    width: 220px;
}

.calc-scale-slider {
    width: 88px;
    flex: 0 0 88px;
}

.calc-scale-value {
    width: 32px;
    text-align: right;
    font-variant-numeric: tabular-nums;
}

.calc-params-body {
    display: grid;
    grid-template-columns:
        var(--calc-route-pane-width, 320px)
        8px
        minmax(0, 1fr)
        8px
        var(--calc-param-pane-width, 300px);
    flex: 1 1 auto;
    min-height: 0;
    overflow: hidden;
}

.calc-route-pane,
.calc-param-pane,
.calc-center-pane {
    min-width: 0;
    min-height: 0;
}

.calc-route-pane,
.calc-param-pane {
    display: flex;
    flex-direction: column;
    background: #fbfdff;
}

.calc-param-pane {
    padding: 12px;
    gap: 10px;
}

.calc-param-header {
    display: flex;
    align-items: flex-start;
    justify-content: space-between;
    gap: 10px;
    flex: 0 0 auto;
}

.calc-param-header > div {
    min-width: 0;
}

.calc-param-header span {
    display: block;
    margin-top: 4px;
    overflow: hidden;
    color: #6b7785;
    font-size: 12px;
    text-overflow: ellipsis;
    white-space: nowrap;
}

.calc-param-actions {
    display: flex;
    flex: 0 0 auto;
    flex-wrap: wrap;
    justify-content: flex-end;
    gap: 8px;
}

.calc-param-actions :deep(.el-button + .el-button) {
    margin-left: 0;
}

.calc-uniform-shift-panel {
    display: flex;
    align-items: center;
    flex: 0 0 auto;
    flex-wrap: wrap;
    gap: 8px;
    padding: 8px;
    border: 1px solid #d8e2ef;
    border-radius: 6px;
    background: #fff;
}

.calc-uniform-shift-title {
    color: #44515f;
    font-size: 12px;
    font-weight: 600;
    white-space: nowrap;
}

.calc-uniform-shift-field {
    display: inline-flex;
    align-items: center;
    gap: 6px;
    color: #4c5968;
    font-size: 12px;
    white-space: nowrap;
}

.calc-uniform-shift-field :deep(.el-input-number) {
    width: 92px;
}

.calc-batch-shift-input {
    width: 116px;
}

.calc-batch-route-list {
    display: flex;
    flex-direction: column;
    gap: 10px;
}

.calc-batch-route-list-toolbar {
    display: flex;
    align-items: center;
    justify-content: space-between;
    color: #536171;
    font-size: 12px;
}

.calc-batch-route-checkboxes {
    display: grid;
    grid-template-columns: 1fr;
    gap: 8px;
    max-height: 360px;
    overflow: auto;
    padding: 8px;
    border: 1px solid #d8e2ef;
    border-radius: 6px;
    background: #f8fafc;
}

.calc-batch-route-checkboxes :deep(.el-checkbox) {
    height: auto;
    margin-right: 0;
    white-space: normal;
}

.calc-traction-content {
    display: flex;
    flex-direction: column;
    gap: 12px;
}

.calc-traction-summary {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 12px;
    padding: 8px 10px;
    border: 1px solid #d8e2ef;
    border-radius: 6px;
    background: #f8fafc;
}

.calc-traction-summary div {
    display: inline-flex;
    align-items: center;
    min-width: 0;
    gap: 8px;
    color: #526071;
    font-size: 12px;
}

.calc-traction-summary strong {
    min-width: 0;
    overflow: hidden;
    color: #1f2937;
    font-size: 13px;
    text-overflow: ellipsis;
    white-space: nowrap;
}

.calc-traction-charts,
.calc-traction-tables {
    display: grid;
    grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);
    gap: 12px;
}

.calc-traction-panel {
    min-width: 0;
    overflow: hidden;
    border: 1px solid #d8e2ef;
    border-radius: 6px;
    background: #fff;
}

.calc-traction-panel header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 8px;
    padding: 8px 10px;
    border-bottom: 1px solid #e4ecf5;
    background: #f8fafc;
}

.calc-traction-panel h3 {
    margin: 0;
    color: #243244;
    font-size: 13px;
    font-weight: 700;
}

.calc-traction-panel header span {
    color: #607080;
    font-size: 12px;
    white-space: nowrap;
}

.calc-traction-chart {
    display: block;
    width: 100%;
    height: 230px;
    background: #fff;
}

.calc-traction-grid-line {
    stroke: #e6edf5;
    stroke-width: 1;
}

.calc-traction-velocity-line,
.calc-traction-time-line {
    fill: none;
    stroke-linecap: round;
    stroke-linejoin: round;
    stroke-width: 3;
}

.calc-traction-velocity-line {
    stroke: #16a34a;
}

.calc-traction-time-line {
    stroke: #2563eb;
}

.calc-traction-axis-label {
    fill: #64748b;
    font-size: 10px;
}

.calc-route-time-input {
    width: 98px;
}

.calc-route-time-cell-name {
    vertical-align: middle;
}

.calc-route-time-cell-tag {
    margin-left: 6px;
    vertical-align: middle;
}

.calc-vertical-resizer,
.calc-horizontal-resizer {
    position: relative;
    background: #dbe5f0;
    z-index: 2;
}

.calc-vertical-resizer {
    cursor: col-resize;
}

.calc-horizontal-resizer {
    cursor: row-resize;
}

.calc-vertical-resizer::before,
.calc-horizontal-resizer::before {
    content: "";
    position: absolute;
    background: #a9b8ca;
}

.calc-vertical-resizer::before {
    top: 0;
    bottom: 0;
    left: 3px;
    width: 2px;
}

.calc-horizontal-resizer::before {
    top: 3px;
    right: 0;
    left: 0;
    height: 2px;
}

.calc-vertical-resizer:hover,
.calc-horizontal-resizer:hover,
.calc-params-body.is-resizing .calc-vertical-resizer,
.calc-params-body.is-resizing .calc-horizontal-resizer {
    background: #c7d8ea;
}

.calc-pane-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 8px;
    padding: 10px;
    border-bottom: 1px solid #d8e2ef;
}

.calc-pane-header h2,
.calc-param-pane h2,
.calc-occupancy-pane h2 {
    margin: 0;
    color: #1f2d3d;
    font-size: 15px;
    font-weight: 600;
}

.calc-pane-header span {
    color: #6b7785;
    font-size: 12px;
}

.calc-header-actions {
    display: inline-flex;
    gap: 6px;
}

.calc-route-status-dot {
    display: inline-block;
    width: 10px;
    height: 10px;
    border: 1.5px solid #9aa7b4;
    border-radius: 50%;
    background: #fff;
    box-shadow: 0 0 0 1px rgba(255, 255, 255, 0.9);
}

.calc-route-status-dot.is-configured {
    border-color: #16a34a;
    background: #16a34a;
    box-shadow: 0 0 0 2px rgba(22, 163, 74, 0.14);
}

.calc-route-pane :deep(.calc-route-status-column .cell) {
    display: flex;
    align-items: center;
    justify-content: center;
    padding-right: 0;
    padding-left: 0;
}

.calc-route-quick-filters {
    display: grid;
    flex: 0 0 auto;
    grid-template-columns: 1fr;
    gap: 8px;
    padding: 8px 10px;
    border-bottom: 1px solid #d8e2ef;
    background: #f6f9fd;
}

.calc-route-filter-row {
    display: flex;
    align-items: center;
    min-width: 0;
    gap: 8px;
}

.calc-route-end-row {
    align-items: flex-start;
}

.calc-route-filter-label {
    flex: 0 0 auto;
    color: #4b5a6a;
    font-size: 12px;
    font-weight: 600;
    line-height: 24px;
    white-space: nowrap;
}

.calc-route-type-toggle-wrap {
    flex: 1 1 auto;
    min-width: 0;
    overflow-x: auto;
    overflow-y: hidden;
}

.calc-route-type-toggle {
    white-space: nowrap;
}

.calc-route-type-toggle :deep(.el-radio-button__inner) {
    padding: 4px 8px;
    font-size: 12px;
}

.calc-route-end-selects {
    display: grid;
    flex: 1 1 auto;
    min-width: 0;
    grid-template-columns: 1fr;
    gap: 6px;
}

.calc-route-end-filter {
    width: 100%;
}

.calc-route-table {
    flex: 1 1 auto;
    min-height: 0;
}

.calc-filter-panel {
    display: grid;
    grid-template-columns: 1fr;
    gap: 8px;
}

.calc-center-pane {
    display: grid;
    grid-template-rows: minmax(220px, 2fr) 8px minmax(150px, 1fr);
    background: #fff;
    overflow: hidden;
}

.calc-layout-pane {
    min-height: 0;
    background: #31363f;
    overflow: hidden;
}

.calc-layout-scroll {
    width: 100%;
    height: 100%;
    overflow: auto;
}

.calc-occupancy-pane {
    display: flex;
    flex-direction: column;
    min-width: 0;
    min-height: 0;
    padding: 12px;
    background: #f8fafc;
    overflow: hidden;
}

.calc-occupancy-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    flex: 0 0 auto;
    gap: 10px;
    min-width: 0;
}

.calc-occupancy-controls,
.calc-gantt-scale-control {
    display: inline-flex;
    align-items: center;
    min-width: 0;
}

.calc-occupancy-controls {
    justify-content: flex-end;
    flex-wrap: wrap;
    gap: 10px;
}

.calc-gantt-scale-control {
    gap: 6px;
    color: #4c5968;
    font-size: 12px;
    white-space: nowrap;
}

.calc-gantt-scale-slider {
    width: 120px;
    flex: 0 0 120px;
}

.calc-occupation-gantt {
    flex: 1 1 auto;
    width: 100%;
    max-width: 100%;
    min-width: 0;
    min-height: 0;
    margin-top: 10px;
}

.calc-param-pane :deep(.el-empty),
.calc-occupancy-pane :deep(.el-empty) {
    flex: 1 1 auto;
}
</style>
