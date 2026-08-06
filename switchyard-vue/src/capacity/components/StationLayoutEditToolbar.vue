<script setup lang="ts">
import { computed } from 'vue'
import { Expand, Fold } from '@element-plus/icons-vue'
import { useI18n } from 'vue-i18n'

const { t } = useI18n()
defineSlots<{
    context(): unknown
    primary(): unknown
    actions(): unknown
    essential(): unknown
    advanced(): unknown
}>()
const density = defineModel('density', { type: String, default: 'compact' })
const isFull = computed(() => density.value === 'full')

function toggleDensity() {
    density.value = isFull.value ? 'compact' : 'full'
}
</script>

<template>
    <section
        class="station-layout-toolbar station-layout-edit-toolbar"
        :class="{ 'is-full': isFull, 'is-compact': !isFull }"
        :aria-label="t('stationLayout.toolbar.edit')"
    >
        <div class="station-layout-toolbar__row station-layout-toolbar__row--main">
            <div class="station-layout-toolbar__identity">
                <slot name="context" />
            </div>
            <div class="station-layout-toolbar__primary">
                <slot name="primary" />
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

        <div class="station-layout-toolbar__row station-layout-toolbar__row--essential">
            <slot name="essential" />
        </div>

        <div v-show="isFull" class="station-layout-toolbar__row station-layout-toolbar__row--advanced">
            <slot name="advanced" />
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

.station-layout-toolbar__row + .station-layout-toolbar__row {
    border-top: 1px solid var(--el-border-color-lighter);
}

.station-layout-toolbar__row--main {
    min-height: 44px;
}

.station-layout-toolbar__row--essential {
    background: var(--el-fill-color-extra-light);
}

.station-layout-toolbar__row--advanced {
    align-items: flex-start;
    background: #fff;
}

.station-layout-toolbar__identity,
.station-layout-toolbar__primary,
.station-layout-toolbar__actions {
    display: flex;
    align-items: center;
    flex-wrap: wrap;
    gap: 8px;
    min-width: 0;
}

.station-layout-toolbar__identity {
    flex: 1 1 340px;
}

.station-layout-toolbar__primary {
    flex: 0 1 auto;
}

.station-layout-toolbar__actions {
    flex: 0 0 auto;
    margin-left: auto;
}

.station-layout-toolbar :deep(.station-toolbar-group) {
    display: inline-flex;
    align-items: center;
    flex-wrap: wrap;
    gap: 6px;
    min-width: 0;
    padding-right: 8px;
}

.station-layout-toolbar :deep(.station-toolbar-group + .station-toolbar-group) {
    padding-left: 8px;
    border-left: 1px solid var(--el-border-color-light);
}

.station-layout-toolbar :deep(.station-toolbar-group__label) {
    display: inline-flex;
    align-items: center;
    min-height: 24px;
    color: var(--el-text-color-secondary);
    font-size: 12px;
    font-weight: 600;
    white-space: nowrap;
}

.station-layout-toolbar :deep(.el-button-group) {
    display: inline-flex;
    flex-wrap: wrap;
    row-gap: 4px;
}

.station-layout-toolbar :deep(.el-button + .el-button) {
    margin-left: 0;
}

.station-layout-toolbar :deep(.station-toolbar-switch-control) {
    display: inline-flex;
    align-items: center;
    gap: 6px;
    min-height: 24px;
    color: var(--el-text-color-regular);
    font-size: 12px;
    white-space: nowrap;
}

.station-layout-toolbar :deep(.station-toolbar-switch-control__label) {
    color: var(--el-text-color-regular);
}

.station-layout-toolbar :deep(.station-toolbar-switch-control__state) {
    min-width: 24px;
    color: var(--el-text-color-secondary);
}

.station-layout-toolbar :deep(.el-switch--small .el-switch__core) {
    width: 32px;
    min-width: 32px;
}

@container (max-width: 880px) {
    .station-layout-toolbar__identity {
        flex-basis: 260px;
    }

    .station-layout-toolbar__primary {
        order: 3;
        flex: 1 0 100%;
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

    .station-layout-toolbar :deep(.station-toolbar-group) {
        flex: 1 1 100%;
        padding: 0;
        border-left: 0 !important;
    }
}
</style>
