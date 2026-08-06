<script setup lang="ts">
import { computed } from 'vue'
import { Aim, Expand, Fold, Grid, Location } from '@element-plus/icons-vue'
import { useI18n } from 'vue-i18n'

const props = defineProps({
    showDisplayControls: { type: Boolean, default: true },
    showGrid: { type: Boolean, default: true },
    showNodes: { type: Boolean, default: true },
    showCurveArc: { type: Boolean, default: true },
    showCellNames: { type: Boolean, default: true },
    scaleX: { type: Number, default: 1 },
    scaleY: { type: Number, default: 1 },
    fitDisabled: { type: Boolean, default: false },
})

defineSlots<{
    context(): unknown
    primary(): unknown
    actions(): unknown
    details(): unknown
}>()

const emit = defineEmits([
    'update:showGrid',
    'update:showNodes',
    'update:showCurveArc',
    'update:showCellNames',
    'update:scaleX',
    'update:scaleY',
    'fit',
])

const { t } = useI18n()
const density = defineModel('density', { type: String, default: 'compact' })
const isFull = computed(() => density.value === 'full')

function toggleDensity() {
    density.value = isFull.value ? 'compact' : 'full'
}
</script>

<template>
    <section
        class="station-layout-toolbar station-layout-view-toolbar"
        :class="{ 'is-full': isFull, 'is-compact': !isFull }"
        :aria-label="t('stationLayout.toolbar.view')"
    >
        <div class="station-layout-toolbar__row station-layout-toolbar__row--main">
            <div class="station-layout-toolbar__identity">
                <slot name="context" />
            </div>

            <div class="station-layout-toolbar__primary">
                <slot name="primary" />
                <template v-if="showDisplayControls">
                    <el-button
                        size="small"
                        :icon="Grid"
                        :type="showGrid ? 'primary' : 'default'"
                        plain
                        @click="emit('update:showGrid', !showGrid)"
                    >
                        {{ t('routeDesign.toolbar.showGrid') }}
                    </el-button>
                    <el-button
                        size="small"
                        :icon="Location"
                        :type="showNodes ? 'primary' : 'default'"
                        plain
                        @click="emit('update:showNodes', !showNodes)"
                    >
                        {{ t('routeDesign.toolbar.showNodes') }}
                    </el-button>
                    <el-button type="primary" size="small" :icon="Aim" :disabled="fitDisabled" @click="emit('fit')">
                        {{ t('stationLayout.tools.fitFullView') }}
                    </el-button>
                </template>
            </div>

            <div class="station-layout-toolbar__actions">
                <slot name="actions" />
                <el-button
                    class="station-layout-toolbar__density"
                    size="small"
                    text
                    :icon="isFull ? Fold : Expand"
                    :title="isFull ? t('stationLayout.toolbar.compact') : t('stationLayout.toolbar.full')"
                    @click="toggleDensity"
                >
                    {{ isFull ? t('stationLayout.toolbar.compact') : t('stationLayout.toolbar.full') }}
                </el-button>
            </div>
        </div>

        <div v-show="isFull" class="station-layout-toolbar__row station-layout-toolbar__row--details">
            <div v-if="showDisplayControls" class="station-view-display-group">
                <span class="station-toolbar-group__label">{{ t('routeDesign.toolbar.layoutDisplay') }}</span>
                <div class="station-view-switch-control">
                    <span>{{ t('routeDesign.toolbar.curveDisplay') }}</span>
                    <el-switch
                        :model-value="showCurveArc"
                        size="small"
                        @update:model-value="emit('update:showCurveArc', $event)"
                    />
                    <span class="station-view-switch-state">
                        {{ showCurveArc ? t('stationLayout.curveDisplay.arc') : t('stationLayout.curveDisplay.tangent') }}
                    </span>
                </div>
                <div class="station-view-switch-control">
                    <span>{{ t('routeDesign.toolbar.showCellNames') }}</span>
                    <el-switch
                        :model-value="showCellNames"
                        size="small"
                        @update:model-value="emit('update:showCellNames', $event)"
                    />
                </div>
            </div>

            <div v-if="showDisplayControls" class="station-view-scale-group">
                <span class="station-toolbar-group__label">{{ t('routeDesign.toolbar.displayScale') }}</span>
                <label class="station-view-scale-control">
                    <span>{{ t('stationLayout.scale.x') }}</span>
                    <el-slider
                        :model-value="scaleX"
                        size="small"
                        :min="0.25"
                        :max="4"
                        :step="0.05"
                        @update:model-value="emit('update:scaleX', $event)"
                    />
                    <strong>{{ scaleX.toFixed(2) }}</strong>
                </label>
                <label class="station-view-scale-control">
                    <span>{{ t('stationLayout.scale.y') }}</span>
                    <el-slider
                        :model-value="scaleY"
                        size="small"
                        :min="0.25"
                        :max="4"
                        :step="0.05"
                        @update:model-value="emit('update:scaleY', $event)"
                    />
                    <strong>{{ scaleY.toFixed(2) }}</strong>
                </label>
            </div>

            <div class="station-layout-toolbar__secondary">
                <slot name="details" />
            </div>
        </div>
    </section>
</template>

<style scoped>
.station-layout-toolbar {
    container-type: inline-size;
    flex: 0 0 auto;
    display: flex;
    flex-direction: column;
    width: 100%;
    min-width: 0;
    color: var(--el-text-color-primary);
    background: linear-gradient(180deg, #fff 0%, #fafbfc 100%);
    border-bottom: 1px solid var(--el-border-color-light);
    box-shadow: 0 1px 2px rgb(15 23 42 / 4%);
}

.station-layout-toolbar__row {
    display: flex;
    align-items: center;
    flex-wrap: wrap;
    gap: 8px;
    min-width: 0;
    padding: 6px 12px;
}

.station-layout-toolbar__row--main {
    min-height: 44px;
}

.station-layout-toolbar__row--details {
    border-top: 1px solid var(--el-border-color-lighter);
    background: var(--el-fill-color-extra-light);
}

.station-layout-toolbar__identity,
.station-layout-toolbar__primary,
.station-layout-toolbar__actions,
.station-layout-toolbar__secondary,
.station-view-display-group,
.station-view-scale-group {
    display: flex;
    align-items: center;
    flex-wrap: wrap;
    gap: 8px;
    min-width: 0;
}

.station-layout-toolbar__identity {
    flex: 1 1 320px;
}

.station-layout-toolbar__primary {
    flex: 0 1 auto;
}

.station-layout-toolbar__actions {
    flex: 0 0 auto;
    margin-left: auto;
}

.station-layout-toolbar__secondary {
    flex: 1 1 auto;
}

.station-view-display-group,
.station-view-scale-group {
    padding-right: 10px;
}

.station-view-display-group + .station-view-scale-group,
.station-layout-toolbar__secondary:not(:empty) {
    padding-left: 10px;
    border-left: 1px solid var(--el-border-color-light);
}

.station-toolbar-group__label {
    color: var(--el-text-color-secondary);
    font-size: 12px;
    font-weight: 600;
    white-space: nowrap;
}

.station-view-switch-control,
.station-view-scale-control {
    display: inline-flex;
    align-items: center;
    gap: 6px;
    color: var(--el-text-color-regular);
    font-size: 12px;
    white-space: nowrap;
}

.station-view-switch-state {
    min-width: 24px;
    color: var(--el-text-color-secondary);
    text-align: left;
}

.station-layout-toolbar :deep(.el-switch--small .el-switch__core) {
    width: 32px;
    min-width: 32px;
}

.station-layout-toolbar :deep(.station-toolbar-switch-control) {
    display: inline-flex;
    align-items: center;
    gap: 6px;
    color: var(--el-text-color-regular);
    font-size: 12px;
    white-space: nowrap;
}

.station-layout-toolbar :deep(.station-toolbar-switch-control__state) {
    min-width: 24px;
    color: var(--el-text-color-secondary);
}

.station-view-scale-control {
    width: 194px;
}

.station-view-scale-control :deep(.el-slider) {
    flex: 1 1 auto;
    min-width: 90px;
}

.station-view-scale-control strong {
    width: 34px;
    color: var(--el-text-color-regular);
    font-size: 12px;
    font-variant-numeric: tabular-nums;
    text-align: right;
}

.station-layout-toolbar :deep(.el-button + .el-button) {
    margin-left: 0;
}

@container (max-width: 920px) {
    .station-layout-toolbar__identity {
        flex-basis: 240px;
    }

    .station-layout-toolbar__primary {
        order: 3;
        flex: 1 0 100%;
    }

    .station-view-scale-group {
        flex: 1 1 100%;
        padding-left: 0 !important;
        border-left: 0 !important;
    }
}

@container (max-width: 620px) {
    .station-layout-toolbar__row {
        padding-inline: 8px;
    }

    .station-layout-toolbar__identity {
        flex-basis: 180px;
    }

    .station-layout-toolbar__density {
        font-size: 0;
    }

    .station-layout-toolbar__density :deep(.el-icon) {
        margin-right: 0;
        font-size: 14px;
    }

    .station-view-display-group,
    .station-view-scale-group,
    .station-layout-toolbar__secondary {
        flex: 1 1 100%;
        padding: 0;
        border-left: 0 !important;
    }

    .station-view-scale-control {
        flex: 1 1 100%;
        width: auto;
    }
}
</style>
