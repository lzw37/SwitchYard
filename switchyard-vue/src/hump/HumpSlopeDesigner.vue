<template>
    <div class="container">
        <div class="side-menu-top">
            <div class="center-section">
                <div class="toolbar-section selection-section">
                    <div class="control-group select-group">
                        <span>{{ t('humpSlopeDesigner.longitudinalSectionScheme') }}</span>
                        <el-select v-model="currentHumpSchemeID"
                            :placeholder="t('humpSlopeDesigner.placeholder.selectHumpScheme')" size="small">
                            <el-option v-for="scheme in humpSchemes" :key="scheme.id" :label="scheme.name"
                                :value="scheme.id" />
                        </el-select>
                        <el-button type="primary" size="small" @click="editSlopeLayout">{{ t('humpSlopeDesigner.buttons.save') }}</el-button>
                        <el-button type="primary" size="small" @click="showSchemeManager = true">...</el-button>
                    </div>
                    <div class="control-group select-group">
                        <span>{{ t('humpSlopeDesigner.calculationCondition') }}</span>
                        <el-select v-model="currentHumpCalculationID"
                            :placeholder="t('humpSlopeDesigner.selectCalculationCondition')" size="small">
                            <el-option v-for="calculation in humpCalculations" :key="calculation.id"
                                :label="getCalculationDisplayLabel(calculation)" :value="calculation.id" />
                        </el-select>
                        <el-button type="primary" size="small" @click="showConditionManager = true">...</el-button>
                    </div>
                    <el-button class="execute-btn" type="primary" size="small" @click="executeCalculation"
                        :loading="calculationExecuting" :disabled="calculationExecuting">
                        {{ calculationExecuting ? t('humpSlopeDesigner.calculation.executing') :
                            t('humpSlopeDesigner.calculation.executeButton') }}
                    </el-button>
                </div>
                <div class="toolbar-section visibility-section">
                    <div class="control-group toggle-group">
                        <span>{{ t('humpSlopeDesigner.initialKineticEnergyLine') }}</span>
                        <el-switch v-model="showInitialKinetic" size="small"></el-switch>
                    </div>
                    <div class="control-group toggle-group">
                        <span>{{ t('humpSlopeDesigner.resistanceEnergyLine') }}</span>
                        <el-switch v-model="showResistance" size="small"></el-switch>
                    </div>
                    <div class="control-group toggle-group">
                        <span>{{ t('humpSlopeDesigner.kineticEnergyLine') }}</span>
                        <el-switch v-model="showKinetic" size="small"></el-switch>
                    </div>
                    <div class="control-group toggle-group">
                        <span>{{ t('humpSlopeDesigner.brakingEnergyLine') }}</span>
                        <el-switch v-model="showBreaking" size="small"></el-switch>
                    </div>
                </div>
                <div class="toolbar-section scale-section">
                    <el-popover placement="bottom" :width="280" trigger="click" popper-class="scale-popover">
                        <template #reference>
                            <el-button size="small" type="primary">{{ t('humpSlopeDesigner.scale') }}</el-button>
                        </template>
                        <div class="scale-popover-body">
                            <div class="control-group slider-group">
                                <span>{{ t('humpSlopeDesigner.xScale') }}</span>
                                <el-slider v-model="globalScaleX" :min="0.1" :max="5" :step="0.01"></el-slider>
                            </div>
                            <div class="control-group slider-group">
                                <span>{{ t('humpSlopeDesigner.yScale') }}</span>
                                <el-slider v-model="globalScaleY" :min="5" :max="100" :step="0.1"></el-slider>
                            </div>
                        </div>
                    </el-popover>
                </div>
            </div>
        </div>
        <div class="condition-info">
            <span class="condition-item">
                <span class="condition-label">{{ t('humpSlopeDesigner.wagonType') }}</span>
                <span class="condition-value">{{ currentCalculateCondition.wagonTypeName }}</span>
            </span>
            <span class="condition-item">
                <span class="condition-label">{{ t('humpSlopeDesigner.slopeLine') }}</span>
                <span class="condition-value">{{ currentCalculateCondition.slopeLineName }}</span>
            </span>
            <span class="condition-item">
                <span class="condition-label">{{ t('humpSlopeDesigner.humpVelocity') }}</span>
                <span class="condition-value">{{ currentCalculateCondition.wagonVelocityOnTop }}m/s</span>
            </span>
            <span class="condition-item">
                <span class="condition-label">{{ t('humpSlopeDesigner.slopeVelocity') }}</span>
                <span class="condition-value">{{ currentCalculateCondition.wagonVelocityOnSlope }}m/s</span>
            </span>
            <span class="condition-item">
                <span class="condition-label">{{ t('humpSlopeDesigner.yardVelocity') }}</span>
                <span class="condition-value">{{ currentCalculateCondition.wagonVelocityOnYard }}m/s</span>
            </span>
            <span class="condition-item">
                <span class="condition-label">{{ t('humpSlopeDesigner.windSpeed') }}</span>
                <span class="condition-value">{{ currentCalculateCondition.windVelocity }}m/s（{{
                    currentCalculateCondition.isHeadWind ? t('humpSlopeDesigner.headWind') :
                        t('humpSlopeDesigner.tailWind') }}）</span>
            </span>
            <span class="condition-item">
                <span class="condition-label">{{ t('humpSlopeDesigner.airDensity') }}</span>
                <span class="condition-value">{{ currentCalculateCondition.airDensity }} {{ t('units.kg_s2_m4') }}</span>
            </span>
            <span class="condition-item">
                <span class="condition-label">{{ t('humpSlopeDesigner.temperature') }}</span>
                <span class="condition-value">{{ currentCalculateCondition.temperature }}°C</span>
            </span>
        </div>
        <div class="main-ctrl">
            <HumpSlopeCtrl ref="humpSlopeCtrlRef" v-model:slope-layout="slopeLayout" :flat-layout="flatLayout"
                :retarder-status-list="currentRetarderStatusList"
                :resistance-energy-height-data="resistanceEnergyHeightData"
                :kinetic-energy-height-data="kineticEnergyHeightData"
                :breaking-energy-height-data="breakingEnergyHeightData" :global-scale-x="globalScaleX"
                :global-scale-y="globalScaleY" :element-visibility="elementVisibility" :global-cursor-x="globalCursorX"
                :g_="currentWagonEffectiveG" @updateGlobalCursorX="updateGlobalCursorX" @horizontal-scroll="syncHorizontalScroll"
                @wheel-scale-x="handleWheelScaleX" @update-retarder-status-list="handleInlineRetarderStatusUpdate"
                @resistance-click="handleResistanceClick"
                @control-point-drag-end="handleControlPointDragEnd" />
            <div v-if="isCurrentHumpSchemeEmpty" class="empty-slope-layout-notice">
                {{ t('humpSlopeDesigner.messages.emptyHumpScheme') }}
            </div>
            <HumpSlopeSketchBlock ref="humpSlopeSketchBlockRef" v-model:slope-layout="slopeLayout" style="height:auto"
                :global-scale-x="globalScaleX" :global-cursor-x="globalCursorX"
                :horizontal-scroll-left="horizontalScrollLeft" @updateGlobalCursorX="updateGlobalCursorX"
                @horizontal-scroll="syncHorizontalScroll" />
            <HumpLayoutCtrl ref="humpLayoutCtrlRef" v-model:flat-layout="flatLayout" :is-toolbar-display="false"
                style="height:auto" :global-scale-x="globalScaleX" :global-cursor-x="globalCursorX"
                @update:global-cursor-x="updateGlobalCursorX" @horizontal-scroll="syncHorizontalScroll" />
        </div>
        <button class="drawer-tab drawer-tab-left" :class="{ 'drawer-tab-open': leftVisible }"
            @click="toggleLeft" :title="t('humpSlopeDesigner.tool')" type="button">
            <span class="drawer-tab-arrow">{{ leftVisible ? '◀' : '▶' }}</span>
        </button>
        <button class="drawer-tab drawer-tab-right" :class="{ 'drawer-tab-open': rightVisible }"
            @click="toggleRight" :title="t('humpSlopeDesigner.data')" type="button">
            <span class="drawer-tab-arrow">{{ rightVisible ? '▶' : '◀' }}</span>
        </button>
        <div class="side-menu-left" v-show="leftVisible">
            <div class="side-menu-container">
                <div class="left-panel-title">{{ t('humpSlopeDesigner.displayElements') }}</div>
                <div class="left-toggle-item">
                    <span>{{ t('humpSlopeDesigner.showRetarder') }}</span>
                    <el-switch v-model="showRetarder" size="small"></el-switch>
                </div>
                <div class="left-toggle-item">
                    <span>{{ t('humpSlopeDesigner.showResistanceNumber') }}</span>
                    <el-switch v-model="showResistanceNumber" size="small"></el-switch>
                </div>
                <div class="left-toggle-item">
                    <span>{{ t('humpSlopeDesigner.showKineticNumber') }}</span>
                    <el-switch v-model="showKineticNumber" size="small"></el-switch>
                </div>
                <div class="left-toggle-item">
                    <span>{{ t('humpSlopeDesigner.showPointHeightNumber') }}</span>
                    <el-switch v-model="showPointHeightNumber" size="small"></el-switch>
                </div>
                <div class="left-toggle-item">
                    <span>{{ t('humpSlopeDesigner.showCursorPositionLabel') }}</span>
                    <el-switch v-model="showCursorPositionLabel" size="small"></el-switch>
                </div>
            </div>
        </div>
        <div class="side-menu-right" v-show="rightVisible">
            <div class="side-menu-container">
                <el-tabs v-model="activeTab" @tab-click="handleTabClick">
                    <el-tab-pane :label="t('humpSlopeDesigner.positionPoints')" name="vposition">
                        <el-table :data="slopeLayout?.positionList || []" style="width: 100%">
                            <el-table-column prop="id" :label="t('humpSlopeDesigner.table.positionID')"
                                width="100"></el-table-column>
                            <el-table-column prop="x" :label="t('humpSlopeDesigner.positionX')" width="140">
                                <template #default="{ row }">
                                    <el-input-number v-model="row.x" :controls="false" :precision="3" size="small"
                                        style="width: 100%" />
                                </template>
                            </el-table-column>
                            <el-table-column prop="height" :label="t('humpSlopeDesigner.height')" width="140">
                                <template #default="{ row }">
                                    <el-input-number v-model="row.height" :controls="false" :precision="3" size="small"
                                        style="width: 100%" />
                                </template>
                            </el-table-column>
                        </el-table>
                    </el-tab-pane>
                    <el-tab-pane :label="t('humpSlopeDesigner.positionSegments')" name="vpositionsegment">
                        <el-table :data="slopeLayout?.positionSegmentList || []" style="width: 100%">
                            <!-- <el-table-column prop="id" :label="t('humpSlopeDesigner.table.operationConditionID')"
                                width="100"></el-table-column> -->
                            <el-table-column prop="startPositionID" :label="t('humpSlopeDesigner.startPositionID')"
                                width="120"></el-table-column>
                            <el-table-column prop="endPositionID" :label="t('humpSlopeDesigner.endPositionID')"
                                width="120"></el-table-column>
                            <el-table-column prop="length" :label="t('humpSlopeDesigner.length')"
                                width="100"></el-table-column>
                            <el-table-column prop="gradient" :label="t('humpSlopeDesigner.gradient')"
                                width="120"></el-table-column>
                            <el-table-column prop="height" :label="t('humpSlopeDesigner.height')"
                                width="120"></el-table-column>
                        </el-table>
                    </el-tab-pane>
                    <el-tab-pane :label="t('humpSlopeDesigner.energyHeight')" name="energyHeight">
                        <el-table :data="energyHeightTableRows" class="energy-height-table" style="width: 100%">
                            <el-table-column prop="positionID" :label="t('humpSlopeDesigner.energyHeightTable.positionID')"
                                width="110"></el-table-column>
                            <el-table-column :label="t('humpSlopeDesigner.energyHeightTable.positionX')"
                                width="115">
                                <template #default="{ row }">
                                    {{ formatEnergyTableNumber(row.x) }}
                                </template>
                            </el-table-column>
                            <el-table-column :label="t('humpSlopeDesigner.energyHeightTable.height')" width="110">
                                <template #default="{ row }">
                                    {{ formatEnergyTableNumber(row.height) }}
                                </template>
                            </el-table-column>
                            <el-table-column :label="t('humpSlopeDesigner.energyHeightTable.resistanceEnergyHeight')"
                                width="130">
                                <template #default="{ row }">
                                    {{ formatEnergyTableNumber(row.resistanceEnergyHeight) }}
                                </template>
                            </el-table-column>
                            <el-table-column :label="t('humpSlopeDesigner.energyHeightTable.kineticEnergyHeight')"
                                width="120">
                                <template #default="{ row }">
                                    {{ formatEnergyTableNumber(row.kineticEnergyHeight) }}
                                </template>
                            </el-table-column>
                            <el-table-column :label="t('humpSlopeDesigner.energyHeightTable.instantaneousVelocity')"
                                width="170">
                                <template #default="{ row }">
                                    {{ formatEnergyTableNumber(row.velocity, 2) }}
                                </template>
                            </el-table-column>
                        </el-table>
                    </el-tab-pane>
                </el-tabs>
            </div>
        </div>
        <div class="side-menu-bottom">BOTTOM MENU</div>

        <!-- 阻力能高分项浮窗 -->
        <div v-if="resistanceDetailPopover.visible" class="resistance-detail-popover"
            :style="{ left: resistanceDetailPopover.x + 'px', top: resistanceDetailPopover.y + 'px' }"
            @click.stop>
            <div class="resistance-detail-header">
                <span class="resistance-detail-title">{{ t('humpSlopeDesigner.resistanceDetail.title') }}</span>
                <span class="resistance-detail-close" @click="closeResistanceDetail">×</span>
            </div>
            <div class="resistance-detail-x-editor">
                <span class="resistance-detail-label">x=</span>
                <el-input-number v-model="resistanceDetailXInput" class="resistance-detail-x-input" size="small"
                    :controls="false" :precision="3" :min="0" @keydown.enter.prevent="confirmResistanceDetailX" />
                <span class="resistance-detail-unit">m</span>
                <el-button size="small" type="primary" :loading="resistanceDetailLoading"
                    :disabled="!isResistanceDetailXValid" @click="confirmResistanceDetailX">
                    {{ t('humpSlopeDesigner.buttons.confirm') }}
                </el-button>
            </div>
            <div class="resistance-detail-body" v-if="resistanceDetailPopover.detail && !resistanceDetailLoading">
                <div class="resistance-detail-row">
                    <span class="resistance-detail-label">{{ t('humpSlopeDesigner.resistanceDetail.total') }}</span>
                    <span class="resistance-detail-value resistance-detail-total">
                        {{ formatHeight(resistanceDetailPopover.detail.totalHeight) }} m
                    </span>
                </div>

                <el-tooltip placement="left" effect="light" :show-arrow="true" :raw-content="false">
                    <template #content>
                        <div class="resistance-formula" v-html="t('humpSlopeDesigner.resistanceDetail.tooltip.pure', pureFormulaParams)"></div>
                    </template>
                    <div class="resistance-detail-row resistance-detail-hoverable">
                        <span class="resistance-detail-label">{{ t('humpSlopeDesigner.resistanceDetail.row.pure') }}</span>
                        <span class="resistance-detail-value">{{ formatHeight(resistanceDetailPopover.detail.pureResistance.energyHeight) }} m</span>
                    </div>
                </el-tooltip>

                <el-tooltip placement="left" effect="light" :show-arrow="true">
                    <template #content>
                        <div class="resistance-formula" v-html="t('humpSlopeDesigner.resistanceDetail.tooltip.air', airFormulaParams)"></div>
                    </template>
                    <div class="resistance-detail-row resistance-detail-hoverable">
                        <span class="resistance-detail-label">{{ t('humpSlopeDesigner.resistanceDetail.row.air') }}</span>
                        <span class="resistance-detail-value">{{ formatHeight(resistanceDetailPopover.detail.airResistance.energyHeight) }} m</span>
                    </div>
                </el-tooltip>

                <el-tooltip placement="left" effect="light" :show-arrow="true">
                    <template #content>
                        <div class="resistance-formula" v-html="t('humpSlopeDesigner.resistanceDetail.tooltip.switch', switchFormulaParams)"></div>
                    </template>
                    <div class="resistance-detail-row resistance-detail-hoverable">
                        <span class="resistance-detail-label">{{ t('humpSlopeDesigner.resistanceDetail.row.switch') }}</span>
                        <span class="resistance-detail-value">{{ formatHeight(resistanceDetailPopover.detail.switchResistance.energyHeight) }} m</span>
                    </div>
                </el-tooltip>

                <el-tooltip placement="left" effect="light" :show-arrow="true">
                    <template #content>
                        <div class="resistance-formula" v-html="t('humpSlopeDesigner.resistanceDetail.tooltip.curve', curveFormulaParams)"></div>
                    </template>
                    <div class="resistance-detail-row resistance-detail-hoverable">
                        <span class="resistance-detail-label">{{ t('humpSlopeDesigner.resistanceDetail.row.curve') }}</span>
                        <span class="resistance-detail-value">{{ formatHeight(resistanceDetailPopover.detail.curveResistance.energyHeight) }} m</span>
                    </div>
                </el-tooltip>
            </div>
            <div v-else class="resistance-detail-loading">{{ t('humpSlopeDesigner.resistanceDetail.loading') }}</div>
        </div>

        <!-- 驼峰方案管理对话框 -->
        <el-dialog v-model="showSchemeManager" :title="t('humpSlopeDesigner.dialog.schemeManagement')" width="80%"
            :close-on-click-modal="false">
            <div style="margin-bottom: 16px;">
                <el-button type="primary" @click="handleAddScheme">{{ t('humpSlopeDesigner.buttons.addScheme')
                    }}</el-button>
            </div>
            <el-table :data="humpSchemes" style="width: 100%" v-loading="tableLoading">
                <el-table-column prop="id" :label="t('humpSlopeDesigner.table.schemeId')" width="200"></el-table-column>
                <el-table-column prop="name" :label="t('humpSlopeDesigner.table.schemeName')">
                    <template #default="{ row, $index }">
                        <el-input v-if="editingIndex === $index" v-model="editingScheme.name" size="small" />
                        <span v-else>{{ row.name }}</span>
                    </template>
                </el-table-column>
                <el-table-column prop="instanceID" :label="t('humpSlopeDesigner.table.instanceId')"
                    width="200"></el-table-column>
                <el-table-column :label="t('humpSlopeDesigner.table.operation')" width="200">
                    <template #default="{ row, $index }">
                        <div v-if="editingIndex === $index">
                            <el-button type="success" size="small" @click="handleSaveScheme">{{
                                t('humpSlopeDesigner.buttons.save') }}</el-button>
                            <el-button size="small" @click="handleCancelEdit">{{ t('humpSlopeDesigner.buttons.cancel')
                                }}</el-button>
                        </div>
                        <div v-else>
                            <el-button type="primary" size="small" @click="handleEditScheme(row, $index)">{{
                                t('humpSlopeDesigner.buttons.edit') }}</el-button>
                            <el-button type="success" size="small" @click="handleCopyScheme(row)">{{
                                t('humpSlopeDesigner.buttons.copy') }}</el-button>
                            <el-button type="danger" size="small" @click="handleDeleteScheme(row)"
                                :disabled="humpSchemes.length <= 1">{{ t('humpSlopeDesigner.buttons.delete')
                                }}</el-button>
                        </div>
                    </template>
                </el-table-column>
            </el-table>

            <template #footer>
                <el-button @click="showSchemeManager = false">{{ t('humpSlopeDesigner.dialog.close') }}</el-button>
            </template>
        </el-dialog>

        <!-- 计算条件管理对话框 -->
        <el-dialog v-model="showConditionManager" :title="t('humpSlopeDesigner.dialog.conditionManagement')" width="90%"
            :close-on-click-modal="false" @open="loadDropdownData">
            <div style="margin-bottom: 16px;">
                <el-button type="primary" @click="handleAddCalculation">{{ t('humpSlopeDesigner.buttons.addCondition')
                    }}</el-button>
            </div>
            <el-table :data="humpCalculations" style="width: 100%" v-loading="calculationTableLoading">
                <el-table-column prop="id" :label="t('humpSlopeDesigner.table.calculationConditionID')"
                    width="180"></el-table-column>
                <el-table-column prop="wagonType" :label="t('humpSlopeDesigner.table.wagonType')" width="120">
                    <template #default="{ row, $index }">
                        <el-select v-if="editingCalculationIndex === $index" v-model="editingCalculation.wagonType"
                            size="small" :placeholder="t('humpSlopeDesigner.placeholder.selectWagonType')">
                            <el-option v-for="wagon in wagonConcepts" :key="wagon.id || wagon.typeName"
                                :label="wagon.typeName" :value="wagon.typeName" />
                        </el-select>
                        <span v-else>{{ row.wagonType }}</span>
                    </template>
                </el-table-column>
                <el-table-column prop="operationConditionID" :label="t('humpSlopeDesigner.table.operationCondition')"
                    width="150">
                    <template #default="{ row, $index }">
                        <el-select v-if="editingCalculationIndex === $index"
                            v-model="editingCalculation.operationConditionID" size="small"
                            :placeholder="t('humpSlopeDesigner.placeholder.selectOperationCondition')">
                            <el-option v-for="condition in operationConditions" :key="condition.id"
                                :label="condition.name || condition.id" :value="condition.id" />
                        </el-select>
                        <span v-else>{{operationConditions.find(c => c.id === row.operationConditionID)?.name ||
                            row.operationConditionID}}</span>
                    </template>
                </el-table-column>
                <el-table-column prop="slopeLineID" :label="t('humpSlopeDesigner.table.slopeLine')" width="150">
                    <template #default="{ row, $index }">
                        <el-select v-if="editingCalculationIndex === $index" v-model="editingCalculation.slopeLineID"
                            size="small" :placeholder="t('humpSlopeDesigner.placeholder.selectSlopeLine')">
                            <el-option v-for="slopeLine in slopeLines" :key="slopeLine.id"
                                :label="slopeLine.name || slopeLine.id" :value="slopeLine.id" />
                        </el-select>
                        <span v-else>{{slopeLines.find(s => s.id === row.slopeLineID)?.name || row.slopeLineID
                            }}</span>
                    </template>
                </el-table-column>
                <el-table-column prop="humpSchemeID" :label="t('humpSlopeDesigner.table.humpScheme')" width="180">
                    <template #default="{ row, $index }">
                        <el-select v-if="editingCalculationIndex === $index" v-model="editingCalculation.humpSchemeID"
                            size="small" :placeholder="t('humpSlopeDesigner.placeholder.selectHumpScheme')">
                            <el-option v-for="scheme in humpSchemes" :key="scheme.id" :label="scheme.name"
                                :value="scheme.id" />
                        </el-select>
                        <span v-else>{{humpSchemes.find(s => s.id === row.humpSchemeID)?.name || row.humpSchemeID
                        }}</span>
                    </template>
                </el-table-column>
                <el-table-column :label="t('humpSlopeDesigner.table.retarderStatus')" width="120">
                    <template #default="{ row }">
                        <el-button type="info" size="small" @click="handleEditRetarderStatus(row)">
                            {{ t('humpSlopeDesigner.buttons.retarderStatus') }}
                        </el-button>
                    </template>
                </el-table-column>
                <el-table-column :label="t('humpSlopeDesigner.table.operation')" width="200">
                    <template #default="{ row, $index }">
                        <div v-if="editingCalculationIndex === $index">
                            <el-button type="success" size="small" @click="handleSaveCalculation">{{
                                t('humpSlopeDesigner.buttons.save') }}</el-button>
                            <el-button size="small" @click="handleCancelCalculationEdit">{{
                                t('humpSlopeDesigner.buttons.cancel') }}</el-button>
                        </div>
                        <div v-else>
                            <el-button type="primary" size="small" @click="handleEditCalculation(row, $index)">{{
                                t('humpSlopeDesigner.buttons.edit') }}</el-button>
                            <el-button type="danger" size="small" @click="handleDeleteCalculation(row)">{{
                                t('humpSlopeDesigner.buttons.delete') }}</el-button>
                        </div>
                    </template>
                </el-table-column>
            </el-table>

            <template #footer>
                <el-button @click="showConditionManager = false">{{ t('humpSlopeDesigner.dialog.close') }}</el-button>
            </template>
        </el-dialog>

        <!-- 减速器工作状态编辑对话框 -->
        <el-dialog v-model="showRetarderStatusDialog" :title="t('humpSlopeDesigner.dialog.retarderStatusManagement')"
            width="800px" :close-on-click-modal="false">
            <div style="margin-bottom: 16px;">
                <el-button type="primary" @click="handleAddRetarderStatus">
                    {{ t('humpSlopeDesigner.buttons.addRetarderStatus') }}
                </el-button>
            </div>
            <el-table :data="editingRetarderStatusList" style="width: 100%">
                <el-table-column prop="retarderID" :label="t('humpSlopeDesigner.retarderStatus.retarderID')"
                    min-width="180">
                    <template #default="{ row }">
                        <el-select v-model="row.retarderID" size="small" style="width: 100%"
                            :loading="retarderOptionsLoading"
                            :placeholder="t('humpSlopeDesigner.retarderStatus.selectRetarder')">
                            <el-option v-for="opt in retarderOptions" :key="opt.id" :label="opt.label"
                                :value="opt.id" />
                        </el-select>
                    </template>
                </el-table-column>
                <el-table-column prop="isActivated" :label="t('humpSlopeDesigner.retarderStatus.isActivated')"
                    width="110" align="center">
                    <template #default="{ row }">
                        <el-switch v-model="row.isActivated" />
                    </template>
                </el-table-column>
                <el-table-column prop="output" :label="t('humpSlopeDesigner.retarderStatus.output')" width="160">
                    <template #default="{ row }">
                        <el-input-number v-model="row.output" :min="0" :max="1" :step="0.1" :precision="2" size="small"
                            style="width: 130px" />
                    </template>
                </el-table-column>
                <el-table-column prop="totalEnergyHeight"
                    :label="t('humpSlopeDesigner.retarderStatus.totalEnergyHeight')" width="160">
                    <template #default="{ row }">
                        <el-input-number v-model="row.totalEnergyHeight" :min="0" :step="0.01" :precision="3"
                            size="small" style="width: 130px" />
                    </template>
                </el-table-column>
                <el-table-column :label="t('humpSlopeDesigner.table.operation')" width="85" fixed="right">
                    <template #default="{ $index }">
                        <el-button type="danger" size="small" @click="handleRemoveRetarderStatus($index)">
                            {{ t('humpSlopeDesigner.buttons.delete') }}
                        </el-button>
                    </template>
                </el-table-column>
            </el-table>
            <template #footer>
                <el-button @click="showRetarderStatusDialog = false">{{ t('humpSlopeDesigner.dialog.close')
                    }}</el-button>
                <el-button type="primary" @click="handleSaveRetarderStatus" :loading="retarderStatusSaving">
                    {{ t('humpSlopeDesigner.buttons.save') }}
                </el-button>
            </template>
        </el-dialog>
    </div>
</template>
<script setup lang="ts">
import HumpSlopeCtrl from './HumpSlopeCtrl.vue';
import HumpSlopeSketchBlock from './HumpSlopeSketchBlock.vue';
import { computed, nextTick, onMounted, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n'
import { ElMessageBox, ElMessage } from 'element-plus'
import HumpLayoutCtrl from './HumpLayoutCtrl.vue';
import axios from '@/utils/axios';
import { FlatLayout, SlopeLayout, CurveDirections } from './humplayoutctrl';

// 定义 props
interface Props {
    selectedInstanceId?: string | null
    activationKey?: number
}
const props = withDefaults(defineProps<Props>(), {
    selectedInstanceId: null,
    activationKey: 0
})

type HorizontalScrollSyncApi = {
    setScrollLeft: (scrollLeft: number) => void
}

// HumpScheme 接口
interface HumpScheme {
    id: string
    instanceID: string
    name: string
}

// RetarderStatus 接口
interface RetarderStatus {
    retarderID: string
    isActivated: boolean
    output: number
    totalEnergyHeight: number
}

// HumpCalculation 接口
interface HumpCalculation {
    id: string
    instanceID: string
    humpSchemeID: string
    wagonType: string
    operationConditionID: string
    slopeLineID: string
    data: any // 对应后端的 HumpCalculationData
    retarderStatusList?: RetarderStatus[]
}

interface WagonConcept {
    id?: string
    typeName: string
    netMass: number
    loadingMass: number
    grossMass?: number
    axleNumber: number
    g?: number
}

type BreakingEnergyHeightPoint = {
    x: number
    breakingEnergyHeight: number
    gravityEnergyHeight: number
    kineticEnergyHeight: number
    display: boolean
}

type KineticEnergyHeightResult = {
    positionID?: string
    PositionID?: string
    orgKineticEnergyHeight?: number
    OrgKineticEnergyHeight?: number
    gravitationHeight?: number
    GravitationHeight?: number
    resistanceHeight?: number
    ResistanceHeight?: number
    breakingHeight?: number
    BreakingHeight?: number
    kineticEnergyHeight?: number
    KineticEnergyHeight?: number
    velocity?: number
    Velocity?: number
}

type KineticEnergyHeightPoint = {
    x: number
    X?: number
    result: KineticEnergyHeightResult
}

type EnergyHeightTableRow = {
    positionID: string
    x: number
    height: number
    resistanceEnergyHeight: number | null
    kineticEnergyHeight: number | null
    velocity: number | null
}

const humpSchemes = ref<HumpScheme[]>([])
const currentHumpSchemeID = ref("");

const humpCalculations = ref<HumpCalculation[]>([])
const currentHumpCalculationID = ref("")
const currentRetarderStatusList = computed<RetarderStatus[]>(() => {
    if (!currentHumpCalculationID.value) {
        return []
    }
    const currentCalculation = humpCalculations.value.find(calc => calc.id === currentHumpCalculationID.value)
    return currentCalculation?.retarderStatusList || []
})

// 下拉菜单选项数据
const wagonConcepts = ref<WagonConcept[]>([])
const operationConditions = ref<any[]>([])
const slopeLines = ref<any[]>([])

const currentWagonEffectiveG = computed(() => {
    const currentCalculation = humpCalculations.value.find(calc => calc.id === currentHumpCalculationID.value)
    const currentWagon = wagonConcepts.value.find(wagon => wagon.typeName === currentCalculation?.wagonType)
    if (!currentWagon) {
        return undefined
    }

    const baseG = Number(currentWagon.g)
    const g = Number.isFinite(baseG) && baseG > 0 ? baseG : 9.8
    const grossMass = Number(currentWagon.grossMass ?? (Number(currentWagon.netMass) + Number(currentWagon.loadingMass)))
    const axleNumber = Number(currentWagon.axleNumber)

    if (!Number.isFinite(grossMass) || grossMass <= 0 || !Number.isFinite(axleNumber)) {
        return g
    }

    return g / (1 + 0.42 * axleNumber / grossMass)
})

// 生成计算条件显示标签
const getCalculationDisplayLabel = (calculation: HumpCalculation) => {
    const wagonType = wagonConcepts.value.find(w => w.typeName === calculation.wagonType)?.typeName || calculation.wagonType
    const operationCondition = operationConditions.value.find(c => c.id === calculation.operationConditionID)?.name || calculation.operationConditionID
    const slopeLine = slopeLines.value.find(s => s.id === calculation.slopeLineID)?.name || calculation.slopeLineID
    return `${wagonType}-${operationCondition}-${slopeLine}`
}

// 方案管理相关状态
const showSchemeManager = ref(false)
const showConditionManager = ref(false)
const tableLoading = ref(false)
const editingIndex = ref(-1)
const editingScheme = ref<HumpScheme>({ id: '', instanceID: '', name: '' })

// 计算条件管理相关状态
const calculationTableLoading = ref(false)
const calculationExecuting = ref(false)
const editingCalculationIndex = ref(-1)
const editingCalculation = ref<HumpCalculation>({
    id: '',
    instanceID: '',
    humpSchemeID: '',
    wagonType: '',
    operationConditionID: '',
    slopeLineID: '',
    data: {}
})

// 减速器工作状态编辑相关状态
const showRetarderStatusDialog = ref(false)
const retarderStatusSaving = ref(false)
const retarderOptionsLoading = ref(false)
const editingRetarderStatusCalculation = ref<HumpCalculation | null>(null)
const editingRetarderStatusList = ref<RetarderStatus[]>([])
const retarderOptions = ref<{ id: string; label: string }[]>([])

const slopeLayout = ref<SlopeLayout | null>(null);
const flatLayout = ref<FlatLayout | null>(null);
const humpSlopeCtrlRef = ref<HorizontalScrollSyncApi | null>(null);
const humpSlopeSketchBlockRef = ref<HorizontalScrollSyncApi | null>(null);
const humpLayoutCtrlRef = ref<HorizontalScrollSyncApi | null>(null);
const syncingHorizontalScroll = ref(false);
const horizontalScrollLeft = ref(0);
const activeTab = ref('vposition');
const leftVisible = ref(false);
const rightVisible = ref(false);

const { t } = useI18n()

const globalLeftMargin = ref(0);

function updateGlobalCursorX(value: number) {
    globalCursorX.value = value;
}

function syncHorizontalScroll(scrollLeft: number) {
    if (syncingHorizontalScroll.value) return;
    syncingHorizontalScroll.value = true;
    horizontalScrollLeft.value = scrollLeft;

    humpSlopeCtrlRef.value?.setScrollLeft(scrollLeft);
    humpSlopeSketchBlockRef.value?.setScrollLeft(scrollLeft);
    humpLayoutCtrlRef.value?.setScrollLeft(scrollLeft);

    requestAnimationFrame(() => {
        syncingHorizontalScroll.value = false;
    });
}

function handleWheelScaleX(payload: { scaleX: number, scrollLeft: number }) {
    globalScaleX.value = payload.scaleX;
    syncHorizontalScroll(payload.scrollLeft);
}

const resistanceEnergyHeightData = ref<{ x: number, height: number }[] | null>(null);
const kineticEnergyHeightData = ref<KineticEnergyHeightPoint[] | null>(null);
const breakingEnergyHeightData = ref<BreakingEnergyHeightPoint[] | null>(null);

const selectedCondition = ref('condition1');
const globalScaleX = ref(3.5);
const globalScaleY = ref(80);
const globalCursorX = ref(0);

const showInitialKinetic = ref(false);
const showResistance = ref(false);
const showKinetic = ref(false);
const showBreaking = ref(false);
const showRetarder = ref(true);
const showResistanceNumber = ref(true);
const showKineticNumber = ref(true);
const showPointHeightNumber = ref(true);
const showCursorPositionLabel = ref(true);
const suppressEnergyLineAutoLoad = ref(false);
const handlingSchemeSelectionChange = ref(false);
let energyHeightRefreshSequence = 0;

const clearEnergyHeightData = () => {
    resistanceEnergyHeightData.value = null
    kineticEnergyHeightData.value = null
    breakingEnergyHeightData.value = null
    closeResistanceDetail()
}

function getObjectValue(source: unknown, ...keys: string[]): unknown {
    if (!source || typeof source !== 'object') return undefined
    const record = source as Record<string, unknown>
    for (const key of keys) {
        if (record[key] !== undefined && record[key] !== null) {
            return record[key]
        }
    }
    return undefined
}

function normalizeTableID(value: unknown): string {
    return value === undefined || value === null ? '' : String(value)
}

function toFiniteNumberOrNull(value: unknown): number | null {
    if (value === undefined || value === null || value === '') return null
    const numericValue = Number(value)
    return Number.isFinite(numericValue) ? numericValue : null
}

function getKineticResultNumber(result: KineticEnergyHeightResult | undefined, ...keys: string[]): number | null {
    return toFiniteNumberOrNull(getObjectValue(result, ...keys))
}

function findKineticEnergyPoint(positionPointID: string, x: unknown): KineticEnergyHeightPoint | undefined {
    const data = kineticEnergyHeightData.value || []
    const idMatchedPoint = data.find(point => {
        const pointPositionID = normalizeTableID(getObjectValue(point.result, 'positionID', 'PositionID'))
        return pointPositionID !== '' && pointPositionID === positionPointID
    })
    if (idMatchedPoint) return idMatchedPoint

    const targetX = toFiniteNumberOrNull(x)
    if (targetX === null) return undefined

    return data.find(point => {
        const pointX = toFiniteNumberOrNull(getObjectValue(point, 'x', 'X'))
        return pointX !== null && Math.abs(pointX - targetX) < 1e-6
    })
}

const energyHeightTableRows = computed<EnergyHeightTableRow[]>(() => {
    return (slopeLayout.value?.positionList || []).map(position => {
        const positionPointID = normalizeTableID(position.id)
        const kineticPoint = findKineticEnergyPoint(positionPointID, position.x)
        const result = kineticPoint?.result
        const positionID = normalizeTableID(getObjectValue(result, 'positionID', 'PositionID')) || positionPointID

        return {
            positionID,
            x: toFiniteNumberOrNull(position.x) ?? 0,
            height: toFiniteNumberOrNull(position.height) ?? 0,
            resistanceEnergyHeight: getKineticResultNumber(result, 'resistanceHeight', 'ResistanceHeight'),
            kineticEnergyHeight: getKineticResultNumber(result, 'kineticEnergyHeight', 'KineticEnergyHeight'),
            velocity: getKineticResultNumber(result, 'velocity', 'Velocity')
        }
    })
})

const isCurrentHumpSchemeEmpty = computed(() => {
    if (!currentHumpSchemeID.value || !slopeLayout.value) return false
    return getSlopeControlPointCount() <= 1
})

function getSlopeControlPointCount() {
    return Array.isArray(slopeLayout.value?.positionList) ? slopeLayout.value.positionList.length : 0
}

function shouldSkipCalculationForEmptyScheme(options: { notify?: boolean } = {}) {
    if (!currentHumpSchemeID.value || getSlopeControlPointCount() > 1) {
        return false
    }

    if (options.notify) {
        ElMessage.warning({
            message: t('humpSlopeDesigner.messages.emptyHumpScheme'),
            duration: 2500
        })
    }
    clearEnergyHeightData()
    calculationExecuting.value = false
    return true
}

function formatEnergyTableNumber(value: unknown, precision = 3): string {
    if (value === undefined || value === null || value === '') return '--'
    const numericValue = Number(value)
    if (!Number.isFinite(numericValue)) return '--'
    return numericValue.toFixed(precision)
}

const setAllEnergyLinesVisible = async (visible: boolean) => {
    suppressEnergyLineAutoLoad.value = true
    showInitialKinetic.value = visible
    showResistance.value = visible
    showKinetic.value = visible
    showBreaking.value = visible
    await nextTick()
    suppressEnergyLineAutoLoad.value = false
}

const hideAllEnergyLinesForRecalculation = async () => {
    clearEnergyHeightData()
    await setAllEnergyLinesVisible(false)
}

const showAllEnergyLinesAfterRecalculation = async () => {
    await setAllEnergyLinesVisible(true)
}

const defaultCalculateCondition = () => ({
    wagonTypeName: "--",
    slopeLineName: "--",
    wagonVelocityOnTop: "--",
    wagonVelocityOnSlope: "--",
    wagonVelocityOnYard: "--",
    windVelocity: "--",
    isHeadWind: "--",
    airDensity: "--",
    temperature: "--",
    g: 9.8,
    retarderActivation: {},
    retarderOutput: {}
})

const currentCalculateCondition = ref(defaultCalculateCondition())

const clearSlopeBindingData = () => {
    slopeLayout.value = null
    flatLayout.value = null
    humpCalculations.value = []
    currentHumpCalculationID.value = ""
    clearEnergyHeightData()
    currentCalculateCondition.value = defaultCalculateCondition()
}

watch(showInitialKinetic, (newVal) => {
    if (suppressEnergyLineAutoLoad.value) return
    if (newVal === true) {
        loadKineticEnergyHeight();
    }
});

watch(showResistance, (newVal) => {
    if (suppressEnergyLineAutoLoad.value) return
    if (newVal === true) {
        loadResistanceEnergyHeight();
    }
});

watch(showKinetic, (newVal) => {
    if (suppressEnergyLineAutoLoad.value) return
    if (newVal === true) {
        loadKineticEnergyHeight();
    }
});

watch(showBreaking, (newVal) => {
    if (suppressEnergyLineAutoLoad.value) return
    if (newVal === true) {
        loadBreakingEnergyHeight();
    }
});

const elementVisibility = computed(() => {
    return {
        initialKinetic: showInitialKinetic.value,
        resistance: showResistance.value,
        kinetic: showKinetic.value,
        breaking: showBreaking.value,
        retarder: showRetarder.value,
        resistanceNumber: showResistanceNumber.value,
        kineticNumber: showKineticNumber.value,
        pointHeightNumber: showPointHeightNumber.value,
        cursorPositionLabel: showCursorPositionLabel.value
    };
});

// 加载纵断面设计数据
const loadSlopeLayout = async () => {
    if (!props.selectedInstanceId || !currentHumpSchemeID.value) {
        slopeLayout.value = null
        return
    }

    try {
        const response = await axios.get('/Hump/GetSlopeLayout', {
            params: {
                instanceID: props.selectedInstanceId,
                humpSchemeID: currentHumpSchemeID.value
            }
        })
        if (response.data) {
            const layout = response.data as SlopeLayout
            layout.positionList = layout.positionList || []
            layout.positionSegmentList = layout.positionSegmentList || []
            slopeLayout.value = layout
            console.log('Slope layout loaded:', slopeLayout.value)
        } else {
            slopeLayout.value = new SlopeLayout()
        }
    } catch (error) {
        console.error('加载纵断面设计数据失败:', error)
        slopeLayout.value = null
    }
}

// 保存编辑的纵断面数据
const editSlopeLayout = async () => {
    if (!props.selectedInstanceId || !currentHumpSchemeID.value) {
        ElMessage.error({
            message: t('humpSlopeDesigner.messages.selectInstanceAndScheme'),
            duration: 3000
        })
        return
    }

    if (!slopeLayout.value) {
        ElMessage.error({
            message: t('humpSlopeDesigner.messages.slopeLayoutNotExist'),
            duration: 3000
        })
        return
    }

    try {
        // 后端期望 slopeLayout 对象直接作为请求体（Pascal case属性名）
        const requestData = {
            PositionList: slopeLayout.value.positionList,
            PositionSegmentList: slopeLayout.value.positionSegmentList
        }

        const response = await axios.put('/Hump/EditSlopeLayout', requestData, {
            params: {
                instanceID: props.selectedInstanceId,
                humpSchemeID: currentHumpSchemeID.value
            }
        })

        if (response.status === 200) {
            ElMessage.success({
                message: t('humpSlopeDesigner.messages.slopeLayoutSaveSuccess'),
                duration: 3000
            })
            // 重新加载数据以确保与后端同步
            await loadSlopeLayout()
        }
    } catch (error: any) {
        console.error('保存纵断面失败:', error)
        const errorMessage = error?.response?.data || t('humpSlopeDesigner.messages.slopeLayoutSaveFailed')
        ElMessage.error({
            message: errorMessage,
            duration: 3000
        })
    }
}

function getCurrentCalculation(): HumpCalculation | undefined {
    return humpCalculations.value.find(calc => calc.id === currentHumpCalculationID.value)
}

function buildEnergyHeightRequestData(currentCalculation: HumpCalculation) {
    return {
        ID: currentHumpCalculationID.value,
        InstanceID: props.selectedInstanceId,
        HumpSchemeID: currentHumpSchemeID.value,
        WagonTypeName: currentCalculation.wagonType,
        OperationConditionID: currentCalculation.operationConditionID,
        SlopeLineID: currentCalculation.slopeLineID
    }
}

function buildEnergyHeightLineParams(currentCalculation: HumpCalculation) {
    return {
        instanceID: props.selectedInstanceId,
        humpSchemeID: currentHumpSchemeID.value,
        id: currentCalculation.id,
        slopeLineID: currentCalculation.slopeLineID,
        wagonTypeName: currentCalculation.wagonType,
        operationConditionID: currentCalculation.operationConditionID,
        retarderStatusID: null,
        retarderStatusList: currentCalculation.retarderStatusList || [],
        wagon: {
            typeName: currentCalculation.wagonType
        }
    }
}

function buildEnergyHeightRequestKey(currentCalculation?: HumpCalculation) {
    return [
        props.selectedInstanceId ?? '',
        currentHumpSchemeID.value,
        currentHumpCalculationID.value,
        currentCalculation?.id ?? '',
        currentCalculation?.slopeLineID ?? '',
        currentCalculation?.wagonType ?? '',
        currentCalculation?.operationConditionID ?? ''
    ].join('|')
}

function isCurrentEnergyHeightRequest(requestKey: string) {
    return requestKey === buildEnergyHeightRequestKey(getCurrentCalculation())
}

async function runCurrentEnergyHeightCalculation() {
    const currentCalculation = getCurrentCalculation()
    if (!currentCalculation || !props.selectedInstanceId || !currentHumpSchemeID.value || !currentHumpCalculationID.value) {
        return false
    }
    if (shouldSkipCalculationForEmptyScheme()) {
        return false
    }

    const requestData = buildEnergyHeightRequestData(currentCalculation)
    console.log('执行驼峰计算，请求参数:', requestData)
    await axios.post('/Hump/ExecuteEnergyHeightCalculation', requestData)
    return true
}

async function loadAllEnergyHeightData() {
    await Promise.all([
        loadResistanceEnergyHeight(),
        loadKineticEnergyHeight(),
        loadBreakingEnergyHeight()
    ])
}

function invalidateEnergyHeightRefresh() {
    energyHeightRefreshSequence += 1
}

async function recalculateAndShowAllEnergyLines(options: { hideFirst?: boolean } = {}) {
    const refreshSequence = ++energyHeightRefreshSequence
    if (options.hideFirst ?? true) {
        await hideAllEnergyLinesForRecalculation()
    }

    if (!props.selectedInstanceId || !currentHumpSchemeID.value || !currentHumpCalculationID.value || !getCurrentCalculation()) {
        if (refreshSequence === energyHeightRefreshSequence) {
            calculationExecuting.value = false
        }
        return
    }
    if (shouldSkipCalculationForEmptyScheme()) {
        if (refreshSequence === energyHeightRefreshSequence) {
            calculationExecuting.value = false
        }
        return
    }

    try {
        calculationExecuting.value = true
        const calculated = await runCurrentEnergyHeightCalculation()
        if (!calculated || refreshSequence !== energyHeightRefreshSequence) return

        await loadAllEnergyHeightData()
        if (refreshSequence !== energyHeightRefreshSequence) return

        await showAllEnergyLinesAfterRecalculation()
    } catch (error: any) {
        console.error('自动刷新能高线失败:', error)
        const errorMessage = error.response?.data?.message || error.message || t('humpSlopeDesigner.messages.calculationUnknownError')
        ElMessage.error(`${t('humpSlopeDesigner.messages.calculationFailed')}: ${errorMessage}`)
    } finally {
        if (refreshSequence === energyHeightRefreshSequence) {
            calculationExecuting.value = false
        }
    }
}

// 加载阻力能高线数据
async function loadResistanceEnergyHeight() {
    if (!props.selectedInstanceId || !currentHumpSchemeID.value || !currentHumpCalculationID.value) {
        resistanceEnergyHeightData.value = null;
        return;
    }

    // 根据当前选择的计算条件ID获取完整的计算条件信息
    const currentCalculation = getCurrentCalculation();
    if (!currentCalculation) {
        console.error("未找到当前选择的计算条件");
        return;
    }

    const params = buildEnergyHeightLineParams(currentCalculation);
    const requestKey = buildEnergyHeightRequestKey(currentCalculation);

    try {
        const response = await axios.post(`/hump/getresistanceenergyheight`, params)
        if (!isCurrentEnergyHeightRequest(requestKey)) return
        if (response.data) {
            console.log('Resistance energy height data loaded:', response.data);
            resistanceEnergyHeightData.value = response.data as { x: number, height: number }[];
        }
    } catch (error) {
        console.error("加载阻力能高度数据失败:", error);
    }
}

// 阻力能高分项明细类型
interface ResistanceEnergyHeightDetailDto {
    x: number
    totalHeight: number
    pureResistance: {
        energyHeight: number
        unitResistanceOnSlope: number
        unitResistanceOnYard: number
        lengthOnSlope: number
        lengthOnYard: number
        wagonMass: number
        temperature: number
        wagonVelocityOnSlope: number
        wagonVelocityOnYard: number
        carTypeParam: number
    }
    airResistance: {
        energyHeight: number
        unitResistanceOnSlope: number
        unitResistanceOnYard: number
        lengthOnSlope: number
        lengthOnYard: number
        wagonMass: number
        airDensity: number
        windwardArea: number
        wagonVelocityOnSlope: number
        wagonVelocityOnYard: number
        windVelocity: number
        isHeadWind: number
    }
    switchResistance: {
        energyHeight: number
        power: number
        reverseCount: number
        forwardCount: number
        diamondCount: number
        slipCount: number
    }
    curveResistance: {
        energyHeight: number
        power: number
        pureCurveCorner: number
        switchCurveDegree: number
        totalCurveDegree: number
    }
}

const resistanceDetailPopover = ref<{
    visible: boolean
    x: number
    y: number
    detail: ResistanceEnergyHeightDetailDto | null
}>({ visible: false, x: 0, y: 0, detail: null })
const resistanceDetailXInput = ref<number | undefined>(undefined)
const resistanceDetailLoading = ref(false)
let resistanceDetailRequestSequence = 0

const isResistanceDetailXValid = computed(() => {
    const x = Number(resistanceDetailXInput.value)
    return Number.isFinite(x) && x >= 0
})

function formatHeight(value: unknown): string {
    const v = Number(value)
    return Number.isFinite(v) ? v.toFixed(4) : '0.0000'
}

function formatNumber(value: unknown): string {
    const v = Number(value)
    if (!Number.isFinite(v)) return '0'
    if (Math.abs(v) >= 100 || Math.abs(v - Math.round(v)) < 1e-9) {
        return v.toFixed(2)
    }
    return v.toFixed(3)
}

function closeResistanceDetail() {
    resistanceDetailRequestSequence += 1
    resistanceDetailXInput.value = undefined
    resistanceDetailLoading.value = false
    resistanceDetailPopover.value = { visible: false, x: 0, y: 0, detail: null }
}

const pureFormulaParams = computed(() => {
    const r = resistanceDetailPopover.value.detail?.pureResistance
    if (!r) return {}
    return {
        Q: r.wagonMass,
        temp: r.temperature,
        n: r.carTypeParam,
        vSlope: r.wagonVelocityOnSlope,
        vYard: r.wagonVelocityOnYard,
        r0Slope: formatNumber(r.unitResistanceOnSlope),
        r0Yard: formatNumber(r.unitResistanceOnYard),
        lSlope: r.lengthOnSlope,
        lYard: r.lengthOnYard
    }
})

const airFormulaParams = computed(() => {
    const r = resistanceDetailPopover.value.detail?.airResistance
    if (!r) return {}
    return {
        Q: r.wagonMass,
        rho: r.airDensity,
        f: r.windwardArea,
        vWind: r.windVelocity,
        xi: r.isHeadWind === 1
            ? t('humpSlopeDesigner.resistanceDetail.windParam.headWind')
            : t('humpSlopeDesigner.resistanceDetail.windParam.tailWind'),
        vSlope: r.wagonVelocityOnSlope,
        vYard: r.wagonVelocityOnYard,
        rwSlope: formatNumber(r.unitResistanceOnSlope),
        rwYard: formatNumber(r.unitResistanceOnYard),
        lSlope: r.lengthOnSlope,
        lYard: r.lengthOnYard
    }
})

const switchFormulaParams = computed(() => {
    const r = resistanceDetailPopover.value.detail?.switchResistance
    if (!r) return {}
    return {
        nReverse: r.reverseCount,
        nForward: r.forwardCount,
        nDiamond: r.diamondCount,
        nSlip: r.slipCount,
        Es: formatNumber(r.power)
    }
})

const curveFormulaParams = computed(() => {
    const r = resistanceDetailPopover.value.detail?.curveResistance
    if (!r) return {}
    return {
        pureCurve: formatNumber(r.pureCurveCorner),
        switchCurve: formatNumber(r.switchCurveDegree),
        totalCurve: formatNumber(r.totalCurveDegree),
        Ec: formatNumber(r.power)
    }
})

async function loadResistanceEnergyHeightDetail(xValue: number, options: { closeOnError?: boolean } = {}) {
    const x = Number(xValue)
    if (!Number.isFinite(x) || x < 0) return

    if (!props.selectedInstanceId || !currentHumpSchemeID.value || !currentHumpCalculationID.value) {
        ElMessage.warning(t('humpSlopeDesigner.resistanceDetail.messages.selectFirst'))
        return
    }
    const currentCalculation = getCurrentCalculation()
    if (!currentCalculation) return

    const requestSequence = ++resistanceDetailRequestSequence
    resistanceDetailLoading.value = true

    try {
        const response = await axios.post(`/hump/getresistanceenergyheightdetail`, buildEnergyHeightLineParams(currentCalculation), {
            params: { x }
        })
        if (requestSequence !== resistanceDetailRequestSequence || !resistanceDetailPopover.value.visible) return
        if (response.data) {
            const detail = response.data as ResistanceEnergyHeightDetailDto
            resistanceDetailPopover.value.detail = detail
            const detailX = Number(detail.x)
            resistanceDetailXInput.value = Number.isFinite(detailX) ? detailX : x
        }
    } catch (error) {
        console.error('Failed to load resistance energy height detail:', error)
        ElMessage.error(t('humpSlopeDesigner.resistanceDetail.messages.loadDetailFailed'))
        if (options.closeOnError) {
            closeResistanceDetail()
        }
    } finally {
        if (requestSequence === resistanceDetailRequestSequence) {
            resistanceDetailLoading.value = false
        }
    }
}

async function confirmResistanceDetailX() {
    if (!isResistanceDetailXValid.value || resistanceDetailLoading.value) return
    await loadResistanceEnergyHeightDetail(Number(resistanceDetailXInput.value))
}

function handleResistanceClick(payload: { x: number, clientX: number, clientY: number }) {
    if (!props.selectedInstanceId || !currentHumpSchemeID.value || !currentHumpCalculationID.value) {
        ElMessage.warning(t('humpSlopeDesigner.resistanceDetail.messages.selectFirst'))
        return
    }
    const currentCalculation = getCurrentCalculation()
    if (!currentCalculation) return
    const x = Number(payload.x)
    if (!Number.isFinite(x) || x < 0) return

    // 弹出位置：相对视口的固定坐标。向右下方稍微偏移以避免遮挡光标点。
    const popoverWidth = 320
    const offsetX = 12
    const offsetY = 12
    let left = payload.clientX + offsetX
    let top = payload.clientY + offsetY
    if (typeof window !== 'undefined') {
        if (left + popoverWidth > window.innerWidth - 8) {
            left = Math.max(8, payload.clientX - popoverWidth - offsetX)
        }
        if (top + 240 > window.innerHeight - 8) {
            top = Math.max(8, window.innerHeight - 240 - 8)
        }
    }

    resistanceDetailXInput.value = Math.round(x * 1000) / 1000
    resistanceDetailPopover.value = { visible: true, x: left, y: top, detail: null }

    void loadResistanceEnergyHeightDetail(x, { closeOnError: true })
}

// 加载动能高线
async function loadKineticEnergyHeight() {
    if (!props.selectedInstanceId || !currentHumpSchemeID.value || !currentHumpCalculationID.value) {
        kineticEnergyHeightData.value = null;
        return;
    }

    // 根据当前选择的计算条件ID获取完整的计算条件信息
    const currentCalculation = getCurrentCalculation();
    if (!currentCalculation) {
        console.error("未找到当前选择的计算条件");
        return;
    }

    const params = buildEnergyHeightLineParams(currentCalculation);
    const requestKey = buildEnergyHeightRequestKey(currentCalculation);

    try {
        const response = await axios.post(`/hump/getkineticenergyheight`, params)
        if (!isCurrentEnergyHeightRequest(requestKey)) return
        if (response.data) {
            console.log('Kinetic energy height data loaded:', response.data);
            kineticEnergyHeightData.value = response.data as KineticEnergyHeightPoint[];
        }
    } catch (error) {
        console.error("加载动能高度数据失败:", error);
    }
}

async function loadBreakingEnergyHeight() {
    if (!props.selectedInstanceId || !currentHumpSchemeID.value || !currentHumpCalculationID.value) {
        breakingEnergyHeightData.value = null;
        return;
    }

    const currentCalculation = getCurrentCalculation();
    if (!currentCalculation) {
        console.error("Current hump calculation not found.");
        return;
    }

    const params = buildEnergyHeightLineParams(currentCalculation);
    const requestKey = buildEnergyHeightRequestKey(currentCalculation);

    const toFiniteNumber = (value: unknown): number => {
        const n = Number(value);
        return Number.isFinite(n) ? n : 0;
    };

    const normalizeItem = (xValue: unknown, item: any): BreakingEnergyHeightPoint => {
        const x = toFiniteNumber(xValue);
        return {
            x,
            breakingEnergyHeight: toFiniteNumber(item?.breakingEnergyHeight ?? item?.BreakingEnergyHeight),
            gravityEnergyHeight: toFiniteNumber(item?.gravityEnergyHeight ?? item?.GravityEnergyHeight),
            kineticEnergyHeight: toFiniteNumber(item?.kineticEnergyHeight ?? item?.KineticEnergyHeight),
            display: item.display ?? false
        };
    };

    try {
        const response = await axios.post(`/hump/getbreakingenergyheight`, params)
        if (!isCurrentEnergyHeightRequest(requestKey)) return

        const raw = response.data;
        if (!raw) {
            breakingEnergyHeightData.value = [];
            return;
        }

        const parsed = Array.isArray(raw)
            ? raw.map((item: any) => normalizeItem(item?.x, item))
            : Object.entries(raw as Record<string, any>).map(([x, item]) => normalizeItem(x, item));

        breakingEnergyHeightData.value = parsed
            .filter(item => Number.isFinite(item.x))
            .sort((a, b) => a.x - b.x);
        console.log('Breaking energy height data loaded:', breakingEnergyHeightData.value);
    } catch (error) {
        console.error("Failed to load breaking energy height data:", error);
    }
}

// 加载下拉菜单选项数据
const loadWagonConcepts = async () => {
    if (!props.selectedInstanceId) {
        wagonConcepts.value = []
        return
    }

    try {
        const response = await axios.get('/Hump/GetWagonConcept', {
            params: { instanceID: props.selectedInstanceId }
        })
        wagonConcepts.value = response.data || []
        console.log('Wagon concepts loaded:', wagonConcepts.value)
    } catch (error) {
        console.error('加载车辆概念失败:', error)
        wagonConcepts.value = []
    }
}

const loadOperationConditions = async () => {
    if (!props.selectedInstanceId) {
        operationConditions.value = []
        return
    }

    try {
        const response = await axios.get('/Hump/GetOperationConditions', {
            params: { instanceID: props.selectedInstanceId }
        })
        operationConditions.value = response.data || []
        console.log('Operation conditions loaded:', operationConditions.value)
    } catch (error) {
        console.error('加载运行条件失败:', error)
        operationConditions.value = []
    }
}

const loadSlopeLines = async () => {
    if (!props.selectedInstanceId) {
        slopeLines.value = []
        return
    }

    try {
        const response = await axios.get('/Hump/GetSlopeLines', {
            params: { instanceID: props.selectedInstanceId }
        })
        slopeLines.value = response.data || []
        console.log('Slope lines loaded:', slopeLines.value)
    } catch (error) {
        console.error('加载溜放线失败:', error)
        slopeLines.value = []
    }
}

// 加载驼峰计算条件数据
const loadHumpCalculations = async (options: { preserveSelection?: boolean, reloadDropdowns?: boolean } = {}) => {
    if (!props.selectedInstanceId || !currentHumpSchemeID.value) {
        humpCalculations.value = []
        currentHumpCalculationID.value = ""
        return
    }

    const { preserveSelection = false, reloadDropdowns = true } = options
    const previousCalculationID = preserveSelection ? currentHumpCalculationID.value : ""

    try {
        // 同时加载下拉菜单数据以支持显示标签
        if (reloadDropdowns) {
            await Promise.all([
                loadWagonConcepts(),
                loadOperationConditions(),
                loadSlopeLines()
            ])
        }

        const response = await axios.get('/Hump/GetHumpCalculations', {
            params: {
                instanceID: props.selectedInstanceId,
                humpSchemeID: currentHumpSchemeID.value
            }
        })
        humpCalculations.value = response.data || []

        // 如果有计算条件数据，默认选择第一个
        const matchedCalculation = previousCalculationID
            ? humpCalculations.value.find(calculation => calculation.id === previousCalculationID)
            : undefined
        currentHumpCalculationID.value = matchedCalculation?.id || humpCalculations.value[0]?.id || ""

        console.log('Hump calculations loaded:', humpCalculations.value)
    } catch (error) {
        console.error('加载驼峰计算条件失败:', error)
        humpCalculations.value = []
        currentHumpCalculationID.value = ""
    }
}

// 加载驼峰方案数据
const loadHumpSchemes = async (options: { preserveSelection?: boolean } = {}) => {
    if (!props.selectedInstanceId) {
        humpSchemes.value = []
        currentHumpSchemeID.value = ""
        clearSlopeBindingData()
        return
    }

    const previousSchemeID = options.preserveSelection ? currentHumpSchemeID.value : ""

    try {
        const response = await axios.get('/Hump/GetHumpSchemes', {
            params: { instanceID: props.selectedInstanceId }
        })
        humpSchemes.value = response.data || []

        // 如果有方案数据，默认选择第一个
        const matchedScheme = previousSchemeID
            ? humpSchemes.value.find(scheme => scheme.id === previousSchemeID)
            : undefined
        currentHumpSchemeID.value = matchedScheme?.id || humpSchemes.value[0]?.id || ""

        if (!currentHumpSchemeID.value) {
            clearSlopeBindingData()
        }

        console.log('Hump schemes loaded:', humpSchemes.value)
    } catch (error) {
        console.error('加载驼峰方案失败:', error)
        humpSchemes.value = []
        currentHumpSchemeID.value = ""
        clearSlopeBindingData()
    }
}

// 主 tab 激活时刷新下拉选项
const refreshDropdownDataOnActivate = async () => {
    if (!props.selectedInstanceId) {
        return
    }

    const previousSchemeID = currentHumpSchemeID.value
    const previousCalculationID = currentHumpCalculationID.value

    await Promise.all([
        loadWagonConcepts(),
        loadOperationConditions(),
        loadSlopeLines(),
        loadHumpSchemes({ preserveSelection: true })
    ])

    if (!currentHumpSchemeID.value || currentHumpSchemeID.value !== previousSchemeID) {
        return
    }

    await loadHumpCalculations({
        preserveSelection: true,
        reloadDropdowns: false
    })

    if (!currentHumpCalculationID.value || currentHumpCalculationID.value !== previousCalculationID) {
        return
    }

    await updateCurrentCalculateCondition()
}

// 监听 selectedInstanceId 变化
watch(() => props.selectedInstanceId, (newInstanceId) => {
    console.log('Selected instance changed:', newInstanceId)
    humpSchemes.value = []
    currentHumpSchemeID.value = ""
    clearSlopeBindingData()
    if (!newInstanceId) {
        return
    }
    loadHumpSchemes()
}, { immediate: true })

// 监听主 tab 激活
watch(() => props.activationKey, () => {
    if (!props.selectedInstanceId) {
        return
    }

    void refreshDropdownDataOnActivate()
})

// 监听 currentHumpSchemeID 变化
watch(currentHumpSchemeID, async (newSchemeId, oldSchemeId) => {
    console.log('Current hump scheme changed from', oldSchemeId, 'to', newSchemeId)
    invalidateEnergyHeightRefresh()
    await hideAllEnergyLinesForRecalculation()

    if (newSchemeId && props.selectedInstanceId) {
        handlingSchemeSelectionChange.value = true
        try {
            await Promise.all([
                loadSlopeLayout(),
                loadHumpCalculations()
            ])
            await Promise.all([
                loadFlatLayout(),
                updateCurrentCalculateCondition()
            ])
            await recalculateAndShowAllEnergyLines({ hideFirst: false })
        } finally {
            handlingSchemeSelectionChange.value = false
        }
    } else {
        clearSlopeBindingData()
        calculationExecuting.value = false
    }
})

// 监听 currentHumpCalculationID 变化
watch(currentHumpCalculationID, async (newCalculationId, oldCalculationId) => {
    console.log('Current hump calculation changed from', oldCalculationId, 'to', newCalculationId)
    if (handlingSchemeSelectionChange.value) {
        return
    }

    invalidateEnergyHeightRefresh()
    await hideAllEnergyLinesForRecalculation()

    if (newCalculationId && props.selectedInstanceId) {
        await Promise.all([
            loadFlatLayout(),
            updateCurrentCalculateCondition()
        ])
        await recalculateAndShowAllEnergyLines({ hideFirst: false })
    } else {
        calculationExecuting.value = false
    }
})

// 更新当前计算条件信息
const updateCurrentCalculateCondition = async () => {
    if (!currentHumpCalculationID.value || !props.selectedInstanceId) {
        return
    }

    const currentCalculation = humpCalculations.value.find(calc => calc.id === currentHumpCalculationID.value)
    if (!currentCalculation) {
        return
    }

    // 确保运行条件已加载
    if (operationConditions.value.length === 0) {
        await loadOperationConditions()
    }

    // 从已加载的运行条件列表中查找对应的条件
    const operationCondition = operationConditions.value.find(
        condition => condition.id === currentCalculation.operationConditionID
    )

    if (operationCondition) {
        // 更新当前计算条件显示信息
        currentCalculateCondition.value = {
            slopeLineName: slopeLines.value.find(s => s.id === currentCalculation.slopeLineID)?.name || currentCalculation.slopeLineID || '--',
            wagonTypeName: currentCalculation.wagonType || '--',
            wagonVelocityOnTop: operationCondition.wagonVelocityOnTop !== undefined ? operationCondition.wagonVelocityOnTop : '--',
            wagonVelocityOnSlope: operationCondition.wagonVelocityOnSlope !== undefined ? operationCondition.wagonVelocityOnSlope : '--',
            wagonVelocityOnYard: operationCondition.wagonVelocityOnYard !== undefined ? operationCondition.wagonVelocityOnYard : '--',
            windVelocity: operationCondition.windVelocity !== undefined ? operationCondition.windVelocity : '--',
            isHeadWind: operationCondition.isHeadWind !== undefined ? operationCondition.isHeadWind : '--',
            airDensity: operationCondition.airDensity !== undefined ? operationCondition.airDensity : '--',
            temperature: operationCondition.temperature !== undefined ? operationCondition.temperature : '--',
            g: currentWagonEffectiveG.value ?? 9.8,
            retarderActivation: operationCondition.retarderActivation || {},
            retarderOutput: operationCondition.retarderOutput || {}
        }
        console.log('Updated calculation condition:', currentCalculateCondition.value)
    } else {
        console.warn('Operation condition not found:', currentCalculation.operationConditionID)
        // 如果找不到运行条件，至少更新车辆类型
        currentCalculateCondition.value.wagonTypeName = currentCalculation.wagonType || '--'
    }
}

// 加载平面布置图数据 
const loadFlatLayout = async () => {
    if (!props.selectedInstanceId) {
        flatLayout.value = null
        return
    }

    try {
        // 首先获取该实例的所有溜放线
        const slopeLinesResponse = await axios.get('/Hump/GetSlopeLines', {
            params: { instanceID: props.selectedInstanceId }
        })
        const slopeLines = slopeLinesResponse.data || []

        const currentCal = humpCalculations.value.filter(calc => calc.id === currentHumpCalculationID.value)[0] // 过滤出与第一条溜放线相关的计算条件
        const currentSlopeLineID = currentCal?.slopeLineID

        // 如果有溜放线，使用第一条线获取平面图
        if (slopeLines.length > 0) {
            const slopeLineID = slopeLines[0].id
            const response = await axios.get('/Hump/GetFlatLayout', {
                params: {
                    instanceID: props.selectedInstanceId,
                    slopeLineID: currentSlopeLineID || slopeLineID // 优先使用与计算条件相关的溜放线ID
                }
            })

            if (response.data) {
                flatLayout.value = response.data
                if (flatLayout.value?.positionSegmentList) {
                    flatLayout.value.positionSegmentList.forEach(seg => {
                        if (seg.curveDegree === 0) {
                            seg.curveDirection = CurveDirections.None
                        }
                    })
                }
                console.log('Flat layout loaded:', flatLayout.value)
            }
        } else {
            console.warn('No slope lines found for instance:', props.selectedInstanceId)
            flatLayout.value = null
        }
    } catch (error) {
        console.error('加载平面展开图数据失败:', error)
        flatLayout.value = null
    }
}

function handleTabClick(tab: any) {
    const tabName = String(tab?.paneName ?? tab?.props?.name ?? tab?.name ?? '')
    if (tabName === 'energyHeight' && !kineticEnergyHeightData.value) {
        void loadKineticEnergyHeight()
    }
}

function toggleLeft() {
    leftVisible.value = !leftVisible.value;
}

function toggleRight() {
    rightVisible.value = !rightVisible.value;
}

onMounted(() => {
    loadSlopeLayout();
    loadFlatLayout();
    loadHumpSchemes();
});

// 驼峰计算方法
const executeCalculation = async (options: { showSuccessDialog?: boolean } = {}) => {
    if (!props.selectedInstanceId || !currentHumpSchemeID.value || !currentHumpCalculationID.value) {
        ElMessageBox.alert(t('humpSlopeDesigner.messages.selectInstanceSchemeCondition'), t('humpSlopeDesigner.messages.tip'), {
            confirmButtonText: t('humpSlopeDesigner.buttons.confirm'),
            type: 'warning'
        })
        return
    }

    // 获取当前计算条件的完整信息
    const currentCalculation = humpCalculations.value.find(calc => calc.id === currentHumpCalculationID.value)
    if (!currentCalculation) {
        ElMessageBox.alert(t('humpSlopeDesigner.messages.calculationConditionNotFound'), t('humpSlopeDesigner.messages.error'), {
            confirmButtonText: t('humpSlopeDesigner.buttons.confirm'),
            type: 'error'
        })
        return
    }
    if (shouldSkipCalculationForEmptyScheme({ notify: true })) {
        return
    }

    try {
        calculationExecuting.value = true

        const requestData = {
            ID: currentHumpCalculationID.value,
            InstanceID: props.selectedInstanceId,
            HumpSchemeID: currentHumpSchemeID.value,
            WagonTypeName: currentCalculation.wagonType,
            OperationConditionID: currentCalculation.operationConditionID,
            SlopeLineID: currentCalculation.slopeLineID
        }

        console.log('执行驼峰计算，请求参数:', requestData)

        const response = await axios.post('/Hump/ExecuteEnergyHeightCalculation', requestData)

        if (response.status === 200) {
            if (options.showSuccessDialog ?? true) {
                await ElMessageBox.alert(
                    t('humpSlopeDesigner.messages.calculationCompleted'),
                    t('humpSlopeDesigner.messages.calculationCompletedTitle'),
                    {
                        confirmButtonText: t('humpSlopeDesigner.buttons.confirm'),
                        type: 'success'
                    }
                )
            }
            console.log('驼峰计算完成:', response.data)

            // 重新加载相关数据
            if (showResistance.value) {
                loadResistanceEnergyHeight()
            }
            if (showKinetic.value || showInitialKinetic.value) {
                loadKineticEnergyHeight()
            }
            if (showBreaking.value) {
                loadBreakingEnergyHeight()
            }
        }
    } catch (error: any) {
        console.error('驼峰计算失败:', error)
        const errorMessage = error.response?.data?.message || error.message || t('humpSlopeDesigner.messages.calculationUnknownError')
        await ElMessageBox.alert(
            `${t('humpSlopeDesigner.messages.calculationFailed')}: ${errorMessage}`,
            t('humpSlopeDesigner.messages.calculationFailed'),
            {
                confirmButtonText: t('humpSlopeDesigner.buttons.confirm'),
                type: 'error'
            }
        )
    } finally {
        calculationExecuting.value = false
    }
}

// 拖动控制点松开后自动保存并重新计算
const handleControlPointDragEnd = async () => {
    if (!props.selectedInstanceId || !currentHumpSchemeID.value || !slopeLayout.value) return

    // 1. 保存纵断面
    try {
        const requestData = {
            PositionList: slopeLayout.value.positionList,
            PositionSegmentList: slopeLayout.value.positionSegmentList
        }
        await axios.put('/Hump/EditSlopeLayout', requestData, {
            params: {
                instanceID: props.selectedInstanceId,
                humpSchemeID: currentHumpSchemeID.value
            }
        })
        await loadSlopeLayout()
        ElMessage.success({ message: t('humpSlopeDesigner.messages.slopeLayoutSaveSuccess'), duration: 2000 })
    } catch (error: any) {
        console.error('自动保存纵断面失败:', error)
        return
    }

    // 2. 执行计算（静默，不弹对话框）
    if (shouldSkipCalculationForEmptyScheme()) return
    if (!currentHumpCalculationID.value) return
    const currentCalculation = humpCalculations.value.find(calc => calc.id === currentHumpCalculationID.value)
    if (!currentCalculation) return

    try {
        const requestData = {
            ID: currentHumpCalculationID.value,
            InstanceID: props.selectedInstanceId,
            HumpSchemeID: currentHumpSchemeID.value,
            WagonTypeName: currentCalculation.wagonType,
            OperationConditionID: currentCalculation.operationConditionID,
            SlopeLineID: currentCalculation.slopeLineID
        }
        await axios.post('/Hump/ExecuteEnergyHeightCalculation', requestData)
        if (showResistance.value) loadResistanceEnergyHeight()
        if (showKinetic.value || showInitialKinetic.value) loadKineticEnergyHeight()
        if (showBreaking.value) loadBreakingEnergyHeight()
    } catch (error: any) {
        console.error('自动计算失败:', error)
    }
}

// 方案管理相关方法
const handleAddScheme = async () => {
    if (!props.selectedInstanceId) {
        console.error('No selected instance')
        return
    }

    const newScheme: HumpScheme = {
        id: '', // 后端会生成
        instanceID: props.selectedInstanceId,
        name: t('humpSlopeDesigner.scheme.newSchemeName')
    }

    try {
        tableLoading.value = true
        const response = await axios.post('/Hump/CreateHumpScheme', newScheme)
        if (response.data) {
            const createdSchemeID = response.data?.id || response.data?.ID || ""
            await loadHumpSchemes() // 重新加载列表
            if (createdSchemeID) {
                currentHumpSchemeID.value = createdSchemeID
            }
            console.log(t('humpSlopeDesigner.scheme.createSuccess'))
        }
    } catch (error) {
        console.error('创建方案失败:', error)
    } finally {
        tableLoading.value = false
    }
}

const handleEditScheme = (scheme: HumpScheme, index: number) => {
    editingIndex.value = index
    editingScheme.value = { ...scheme }
}

const handleSaveScheme = async () => {
    if (!editingScheme.value.name.trim()) {
        console.error(t('humpSlopeDesigner.scheme.schemeNameRequired'))
        return
    }

    try {
        tableLoading.value = true
        const response = await axios.put('/Hump/EditHumpScheme', editingScheme.value)
        if (response.status === 200) {
            await loadHumpSchemes() // 重新加载列表
            handleCancelEdit()
            console.log(t('humpSlopeDesigner.scheme.updateSuccess'))
        }
    } catch (error) {
        console.error('更新方案失败:', error)
    } finally {
        tableLoading.value = false
    }
}

const handleDeleteScheme = async (scheme: HumpScheme) => {
    if (humpSchemes.value.length <= 1) {
        console.error('至少需要保留一个方案')
        return
    }

    try {
        await ElMessageBox.confirm(
            t('humpSlopeDesigner.messages.deleteSchemeConfirm', { name: scheme.name }),
            t('humpSlopeDesigner.buttons.deleteConfirm'),
            {
                confirmButtonText: t('humpSlopeDesigner.buttons.confirm'),
                cancelButtonText: t('humpSlopeDesigner.buttons.cancel'),
                type: 'warning',
            }
        )

        tableLoading.value = true
        const response = await axios.delete(`/Hump/DeleteHumpScheme?id=${scheme.id}`)
        if (response.status === 200) {
            await loadHumpSchemes() // 重新加载列表
            console.log(t('humpSlopeDesigner.scheme.deleteSuccess'))
        }
    } catch (error) {
        if (error === 'cancel') {
            console.log(t('humpSlopeDesigner.common.cancelDelete'))
        } else {
            console.error('删除方案失败:', error)
        }
    } finally {
        tableLoading.value = false
    }
}

const handleCancelEdit = () => {
    editingIndex.value = -1
    editingScheme.value = { id: '', instanceID: '', name: '' }
}

const handleCopyScheme = async (scheme: HumpScheme) => {
    if (!scheme.id) return

    try {
        tableLoading.value = true
        const response = await axios.post('/Hump/CopyHumpScheme', {
            SourceHumpSchemeID: scheme.id,
            NewName: `${scheme.name}副本`
        })
        if (response.status === 200) {
            await loadHumpSchemes()
            ElMessage.success(t('humpSlopeDesigner.scheme.copySuccess'))
        }
    } catch (error) {
        console.error('复制方案失败:', error)
        ElMessage.error(t('humpSlopeDesigner.scheme.copyError'))
    } finally {
        tableLoading.value = false
    }
}

// 计算条件管理相关方法
const handleAddCalculation = async () => {
    if (!props.selectedInstanceId || !currentHumpSchemeID.value) {
        console.error('No selected instance or hump scheme')
        return
    }

    // 加载下拉菜单选项数据
    await Promise.all([
        loadWagonConcepts(),
        loadOperationConditions(),
        loadSlopeLines()
    ])

    const newCalculation: HumpCalculation = {
        id: '', // 后端会生成
        instanceID: props.selectedInstanceId,
        humpSchemeID: currentHumpSchemeID.value,
        wagonType: wagonConcepts.value[0]?.typeName || 'P70H',
        operationConditionID: operationConditions.value.length > 0 ? operationConditions.value[0].id : 'default',
        slopeLineID: slopeLines.value.length > 0 ? slopeLines.value[0].id : 'default',
        data: [] // 提供必需的 Data 字段作为数组
    }

    try {
        calculationTableLoading.value = true
        const response = await axios.post('/Hump/CreateHumpCalculation', newCalculation)
        if (response.data) {
            await loadHumpCalculations() // 重新加载列表
            console.log(t('humpSlopeDesigner.condition.createSuccess'))
        }
    } catch (error) {
        console.error('创建计算条件失败:', error)
    } finally {
        calculationTableLoading.value = false
    }
}

const handleEditCalculation = async (calculation: HumpCalculation, index: number) => {
    // 加载下拉菜单选项数据
    await Promise.all([
        loadWagonConcepts(),
        loadOperationConditions(),
        loadSlopeLines()
    ])

    editingCalculationIndex.value = index
    editingCalculation.value = { ...calculation }
}

const handleSaveCalculation = async () => {
    if (!editingCalculation.value.wagonType.trim()) {
        console.error(t('humpSlopeDesigner.condition.wagonTypeRequired'))
        return
    }

    try {
        calculationTableLoading.value = true
        // 将字段名转换为Pascal case格式以匹配API期望
        const apiRequest = {
            ID: editingCalculation.value.id,
            InstanceID: editingCalculation.value.instanceID,
            HumpSchemeID: editingCalculation.value.humpSchemeID,
            WagonType: editingCalculation.value.wagonType,
            OperationConditionID: editingCalculation.value.operationConditionID,
            SlopeLineID: editingCalculation.value.slopeLineID,
            Data: editingCalculation.value.data
        }
        const response = await axios.put('/Hump/EditHumpCalculation', apiRequest)
        if (response.status === 200) {
            await loadHumpCalculations() // 重新加载列表
            handleCancelCalculationEdit()
            console.log(t('humpSlopeDesigner.condition.updateSuccess'))
        }
    } catch (error) {
        console.error('更新计算条件失败:', error)
    } finally {
        calculationTableLoading.value = false
    }
}

const handleDeleteCalculation = async (calculation: HumpCalculation) => {
    try {
        await ElMessageBox.confirm(
            t('humpSlopeDesigner.messages.deleteConditionConfirm', { name: `${calculation.wagonType} - ${calculation.operationConditionID}` }),
            t('humpSlopeDesigner.buttons.deleteConfirm'),
            {
                confirmButtonText: t('humpSlopeDesigner.buttons.confirm'),
                cancelButtonText: t('humpSlopeDesigner.buttons.cancel'),
                type: 'warning',
            }
        )

        calculationTableLoading.value = true
        const response = await axios.delete('/Hump/DeleteHumpCalculation', {
            params: {
                instanceID: calculation.instanceID,
                humpSchemeID: calculation.humpSchemeID,
                id: calculation.id
            }
        })
        if (response.status === 200) {
            await loadHumpCalculations() // 重新加载列表
            console.log(t('humpSlopeDesigner.condition.deleteSuccess'))
        }
    } catch (error) {
        if (error === 'cancel') {
            console.log(t('humpSlopeDesigner.common.cancelDelete'))
        } else {
            console.error('删除计算条件失败:', error)
        }
    } finally {
        calculationTableLoading.value = false
    }
}

// 加载下拉菜单数据的统一函数
const loadDropdownData = async () => {
    await Promise.all([
        loadWagonConcepts(),
        loadOperationConditions(),
        loadSlopeLines()
    ])
}

const handleCancelCalculationEdit = () => {
    editingCalculationIndex.value = -1
    editingCalculation.value = {
        id: '',
        instanceID: '',
        humpSchemeID: '',
        wagonType: '',
        operationConditionID: '',
        slopeLineID: '',
        data: []
    }
}

// 减速器工作状态相关方法
const handleEditRetarderStatus = async (calculation: HumpCalculation) => {
    editingRetarderStatusCalculation.value = calculation
    editingRetarderStatusList.value = calculation.retarderStatusList
        ? JSON.parse(JSON.stringify(calculation.retarderStatusList))
        : []

    // 加载该溜放线上的减速器选项
    retarderOptions.value = []
    if (props.selectedInstanceId && calculation.slopeLineID) {
        try {
            retarderOptionsLoading.value = true
            const response = await axios.get('/Hump/GetFlatLayout', {
                params: {
                    instanceID: props.selectedInstanceId,
                    slopeLineID: calculation.slopeLineID
                }
            })
            const list = response.data?.retarderList || []
            retarderOptions.value = list.map((r: any) => ({
                id: r.id,
                label: r.numbers ? `${r.id} (${r.numbers})` : r.id
            }))
        } catch (error) {
            console.error('加载减速器列表失败:', error)
        } finally {
            retarderOptionsLoading.value = false
        }
    }

    showRetarderStatusDialog.value = true
}

const handleAddRetarderStatus = () => {
    editingRetarderStatusList.value.push({
        retarderID: retarderOptions.value.length > 0 ? (retarderOptions.value[0]?.id ?? '') : '',
        isActivated: true,
        output: 1.0,
        totalEnergyHeight: 0
    })
}

const handleRemoveRetarderStatus = (index: number) => {
    editingRetarderStatusList.value.splice(index, 1)
}

const handleInlineRetarderStatusUpdate = async (retarderStatusList: RetarderStatus[]) => {
    const calc = humpCalculations.value.find(c => c.id === currentHumpCalculationID.value)
    if (!calc) return

    const normalizedList = (retarderStatusList || []).map(r => ({
        retarderID: r.retarderID,
        isActivated: Boolean(r.isActivated),
        output: Math.max(0, Math.min(1, Number(r.output ?? 0))),
        totalEnergyHeight: Math.max(0, Number(r.totalEnergyHeight ?? 0))
    }))

    const backupList = calc.retarderStatusList ? JSON.parse(JSON.stringify(calc.retarderStatusList)) : []
    calc.retarderStatusList = normalizedList

    try {
        const apiRequest = {
            ID: calc.id,
            InstanceID: calc.instanceID,
            HumpSchemeID: calc.humpSchemeID,
            WagonType: calc.wagonType,
            OperationConditionID: calc.operationConditionID,
            SlopeLineID: calc.slopeLineID,
            Data: calc.data,
            RetarderStatusList: normalizedList.map(r => ({
                RetarderID: r.retarderID,
                IsActivated: r.isActivated,
                Output: r.output,
                TotalEnergyHeight: r.totalEnergyHeight
            }))
        }
        await axios.put('/Hump/EditHumpCalculation', apiRequest)
        await executeCalculation({ showSuccessDialog: false })
    } catch (error) {
        calc.retarderStatusList = backupList
        console.error('保存减速器状态失败:', error)
        ElMessage.error(t('humpSlopeDesigner.retarderStatus.saveError'))
    }
}

const handleSaveRetarderStatus = async () => {
    if (!editingRetarderStatusCalculation.value) return

    try {
        retarderStatusSaving.value = true
        const calc = editingRetarderStatusCalculation.value
        const apiRequest = {
            ID: calc.id,
            InstanceID: calc.instanceID,
            HumpSchemeID: calc.humpSchemeID,
            WagonType: calc.wagonType,
            OperationConditionID: calc.operationConditionID,
            SlopeLineID: calc.slopeLineID,
            Data: calc.data,
            RetarderStatusList: editingRetarderStatusList.value.map(r => ({
                RetarderID: r.retarderID,
                IsActivated: r.isActivated,
                Output: r.output,
                TotalEnergyHeight: r.totalEnergyHeight
            }))
        }
        const response = await axios.put('/Hump/EditHumpCalculation', apiRequest)
        if (response.status === 200) {
            await loadHumpCalculations()
            showRetarderStatusDialog.value = false
            ElMessage.success(t('humpSlopeDesigner.retarderStatus.saveSuccess'))
        }
    } catch (error) {
        console.error('保存减速器状态失败:', error)
        ElMessage.error(t('humpSlopeDesigner.retarderStatus.saveError'))
    } finally {
        retarderStatusSaving.value = false
    }
}

</script>
<style scoped lang="css">
.resistance-detail-popover {
    position: fixed;
    z-index: 2000;
    width: 320px;
    background: #ffffff;
    border: 1px solid #d0d7de;
    border-radius: 6px;
    box-shadow: 0 6px 20px rgba(0, 0, 0, 0.18);
    font-size: 13px;
    color: #1f2328;
}

.resistance-detail-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    padding: 8px 12px;
    border-bottom: 1px solid #eaecef;
    background: #f6f8fa;
    border-radius: 6px 6px 0 0;
}

.resistance-detail-title {
    font-weight: 600;
    color: #24292f;
}

.resistance-detail-close {
    cursor: pointer;
    font-size: 18px;
    line-height: 1;
    color: #57606a;
    padding: 0 4px;
}

.resistance-detail-close:hover {
    color: #cf222e;
}

.resistance-detail-body {
    padding: 6px 0;
}

.resistance-detail-x-editor {
    display: flex;
    align-items: center;
    gap: 6px;
    padding: 8px 12px;
    border-bottom: 1px solid #eaecef;
}

.resistance-detail-x-input {
    flex: 1 1 auto;
    min-width: 0;
}

.resistance-detail-unit {
    color: #57606a;
    font-size: 12px;
}

.resistance-detail-row {
    display: flex;
    justify-content: space-between;
    padding: 6px 12px;
}

.resistance-detail-hoverable {
    cursor: help;
}

.resistance-detail-hoverable:hover {
    background: #f6f8fa;
}

.resistance-detail-label {
    color: #57606a;
}

.resistance-detail-value {
    font-family: 'Consolas', 'Menlo', monospace;
    color: #1f2328;
}

.resistance-detail-total {
    font-weight: 700;
    color: #0969da;
}

.resistance-detail-loading {
    padding: 20px;
    text-align: center;
    color: #57606a;
}

.resistance-formula {
    max-width: 380px;
    line-height: 1.6;
}

.resistance-formula-title {
    font-weight: 600;
    margin-bottom: 4px;
    color: #1f2328;
}

.resistance-formula-line {
    font-family: 'Consolas', 'Menlo', monospace;
    margin: 2px 0;
}

.resistance-formula-params {
    margin-top: 6px;
    font-size: 12px;
    color: #57606a;
    line-height: 1.7;
}

.resistance-formula b {
    color: #0969da;
    font-weight: 700;
}

.container {
    position: relative;
    min-height: 100vh;
    height: auto;
    display: flex;
    flex-direction: column;
    overflow-y: auto;
    overflow-x: hidden;
}

.side-menu-top {
    height: auto;
    background-color: #f0f0f0;
    display: flex;
    align-items: center;
    flex-wrap: wrap;
    gap: 6px;
    padding: 6px 8px;
}

.left-section,
.right-section {
    flex: 0 0 auto;
    display: flex;
    align-items: center;
}

.center-section {
    flex: 1 1 auto;
    display: flex;
    align-items: center;
    justify-content: flex-start;
    flex-wrap: wrap;
    row-gap: 6px;
    column-gap: 6px;
    gap: 6px;
    padding: 6px 8px;
    margin: 0 4px;
    border: 1px solid #dbe3f1;
    border-radius: 2px;
    background: linear-gradient(135deg, #f8fafc, #eef3ff);
    box-shadow: 0 2px 8px rgba(15, 23, 42, 0.08);
    min-height: 36px;
    height: auto;
    min-width: 0;
    box-sizing: border-box;
}

.main-ctrl {
    flex: 1 1 auto;
    min-height: 0;
    background-color: #ffffff;
    position: relative;
    /* display: flex; */
}

.empty-slope-layout-notice {
    position: absolute;
    top: 58px;
    left: 50%;
    transform: translateX(-50%);
    z-index: 6;
    max-width: min(92vw, 560px);
    padding: 8px 14px;
    border: 1px solid #c8d5e8;
    border-radius: 4px;
    background: rgba(255, 255, 255, 0.95);
    color: #1f2a44;
    font-size: 14px;
    line-height: 1.4;
    text-align: center;
    box-shadow: 0 4px 14px rgba(15, 23, 42, 0.12);
    pointer-events: none;
}

.side-menu-left {
    position: absolute;
    top: 50px;
    left: 0;
    width: 200px;
    height: calc(100vh - 100px);
    background-color: white;
    opacity: 0.9;
    z-index: 10;
}

.side-menu-right {
    position: absolute;
    top: 50px;
    right: 0;
    width: 500px;
    height: calc(100vh - 100px);
    background-color: white;
    z-index: 10;
    opacity: 0.9;

}

.side-menu-bottom {
    height: 50px;
    background-color: white;
}

.side-menu-container {
    margin: 5px;
    padding: 10px;
    height: 100%;
    box-sizing: border-box;
    overflow-y: auto;
}

.left-panel-title {
    font-size: 14px;
    font-weight: 600;
    color: #1f2a44;
    margin-bottom: 10px;
}

.left-toggle-item {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 12px;
    padding: 8px 0;
    border-bottom: 1px solid #d6dbe5;
}

.left-toggle-item:last-child {
    border-bottom: none;
}

.left-toggle-item span {
    font-size: 13px;
    color: #303133;
    line-height: 1.4;
}

.control-group {
    display: flex;
    align-items: center;
    gap: 4px;
    min-width: 0;
    max-width: 100%;
}

.control-group span {
    font-size: small;
    font-weight: 600;
    line-height: 1.2;
    margin-right: 0;
    white-space: nowrap;
    flex: 0 0 auto;
}

.toolbar-section {
    display: flex;
    align-items: center;
    align-content: center;
    flex-wrap: wrap;
    gap: 6px;
    min-width: 0;
}

.selection-section {
    flex: 1 1 auto;
    min-width: 0;
}

.visibility-section {
    flex: 0 1 auto;
    min-width: 0;
}

.select-group {
    flex: 1 1 200px;
    min-width: 0;
}

.select-group :deep(.el-select) {
    flex: 1 1 100px;
    width: clamp(100px, 12vw, 200px);
    min-width: 100px;
    max-width: 200px;
}

.center-section :deep(.el-button + .el-button) {
    margin-left: 0;
}

.execute-btn {
    flex: 0 0 auto;
}

.toggle-group {
    flex: 0 0 auto;
}

.scale-popover-body {
    display: flex;
    flex-direction: column;
    gap: 10px;
    padding: 4px 2px;
}

.scale-popover-body .slider-group {
    width: 100%;
}

.scale-popover-body .slider-group :deep(.el-slider) {
    flex: 1 1 auto;
    width: auto;
    min-width: 140px;
}

.slider-group {
    flex: 1 1 160px;
    min-width: 160px;
}

.slider-group :deep(.el-slider) {
    flex: 1 1 110px;
    width: auto;
    min-width: 110px;
}

.scale-section {
    flex: 0 0 auto;
}

@media (max-width: 1200px) {
    .center-section {
        margin: 0;
        padding: 6px 8px;
        gap: 6px;
    }
}

@media (max-width: 768px) {
    .container {
        overflow-y: auto;
    }

    .main-ctrl {
        min-height: 320px;
        overflow: auto;
    }

    .center-section {
        gap: 8px;
    }

    .toolbar-section {
        flex: 1 1 100%;
    }

    .select-group {
        display: grid;
        grid-template-columns: max-content minmax(0, 1fr) auto auto;
        width: 100%;
    }

    .select-group :deep(.el-select) {
        width: 100% !important;
        min-width: 0;
        max-width: none;
    }

    .execute-btn {
        flex: 1 1 100%;
        width: 100%;
    }

    .toggle-group {
        flex: 1 1 calc(50% - 8px);
        min-width: 210px;
        justify-content: space-between;
    }

    .slider-group {
        flex: 1 1 100%;
        min-width: 0;
    }

    .slider-group :deep(.el-slider) {
        min-width: 0;
    }
}

@media (max-width: 480px) {
    .select-group {
        grid-template-columns: minmax(0, 1fr) auto auto;
    }

    .select-group span {
        grid-column: 1 / -1;
    }

    .toggle-group {
        flex-basis: 100%;
        min-width: 0;
    }
}

.drawer-tab {
    position: absolute;
    top: 50%;
    transform: translateY(-50%);
    width: 22px;
    height: 56px;
    padding: 0;
    border: 1px solid #c6d1e8;
    background: linear-gradient(135deg, #5b8def, #3d6fd8);
    color: #ffffff;
    cursor: pointer;
    z-index: 50;
    display: flex;
    align-items: center;
    justify-content: center;
    box-shadow: 0 2px 8px rgba(15, 23, 42, 0.18);
    transition: background 0.2s ease, transform 0.2s ease, left 0.25s ease, right 0.25s ease;
}

.drawer-tab:hover {
    background: linear-gradient(135deg, #6ea0ff, #4a7ee8);
}

.drawer-tab-left {
    left: 0;
    border-left: none;
    border-radius: 0 28px 28px 0;
}

.drawer-tab-left.drawer-tab-open {
    left: 200px;
}

.drawer-tab-right {
    right: 0;
    border-right: none;
    border-radius: 28px 0 0 28px;
}

.drawer-tab-right.drawer-tab-open {
    right: 500px;
}

.drawer-tab-arrow {
    font-size: 12px;
    line-height: 1;
    user-select: none;
}

.condition-info {
    display: flex;
    flex-wrap: wrap;
    justify-content: center;
    align-items: center;
    gap: 6px 18px;
    width: 100%;
    margin: 5px auto 0;
    padding: 0 10px;
    box-sizing: border-box;
    text-align: center;
}

.condition-item {
    display: inline-flex;
    align-items: baseline;
    justify-content: center;
    gap: 4px;
    min-width: 0;
    line-height: 1.5;
    white-space: normal;
    overflow-wrap: anywhere;
    word-break: break-word;
}

.condition-label {
    color: #6b7280;
    font-size: 13px;
    font-weight: 500;
}

.condition-value {
    color: #1f2937;
    font-size: 14px;
    font-weight: 700;
}
</style>

<!-- 阻力能高分项浮窗中 el-tooltip 内容会被 teleport 到 body 之外，使用未 scoped 的样式确保生效 -->
<style lang="css">
.resistance-formula {
    max-width: 420px;
    line-height: 1.6;
    color: #1f2328;
}

.resistance-formula-title {
    font-weight: 600;
    margin-bottom: 4px;
    color: #1f2328;
}

.resistance-formula-line {
    font-family: 'Consolas', 'Menlo', monospace;
    margin: 2px 0;
}

.resistance-formula-params {
    margin-top: 6px;
    font-size: 12px;
    color: #57606a;
    line-height: 1.7;
}

.resistance-formula b {
    color: #0969da;
    font-weight: 700;
}
</style>
