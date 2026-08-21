<script setup lang="ts">
import {
    StationLayout as SharedStationLayout,
    createStationLayoutTranslator,
    type StationLayoutTranslate,
} from "@switchyard/station-layout";
import { useI18n } from "vue-i18n";
import {
    formatSwitchYardStationLayoutError,
    switchyardStationLayoutGateway,
} from "./switchyardStationLayoutGateway";

const props = defineProps({
    selectedInstanceId: {
        type: String,
        default: "",
    },
});

const { t, te, locale } = useI18n();
const fallbackTranslators = {
    zh: createStationLayoutTranslator("zh"),
    en: createStationLayoutTranslator("en"),
};
const getFallbackTranslate = () => (
    String(locale.value || "zh").toLowerCase().startsWith("en")
        ? fallbackTranslators.en
        : fallbackTranslators.zh
);
const translate: StationLayoutTranslate = (key, parameters) => (
    te(key)
        ? String(t(key, parameters as any))
        : getFallbackTranslate()(key, parameters)
);
</script>

<template>
    <SharedStationLayout
        :selected-instance-id="props.selectedInstanceId"
        :gateway="switchyardStationLayoutGateway"
        :translate="translate"
        :format-error="formatSwitchYardStationLayoutError"
    />
</template>
