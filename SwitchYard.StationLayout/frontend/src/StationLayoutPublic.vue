<script setup lang="ts">
import { ref, type PropType } from "vue";
import StationLayoutImpl from "./StationLayout.vue";
import type {
  StationLayoutErrorFormatter,
  StationLayoutGateway,
  StationLayoutTranslate,
} from "./gateway";

defineOptions({ inheritAttrs: false });

const props = defineProps({
  selectedInstanceId: {
    type: String,
    default: "",
  },
  stationSchemeId: {
    type: String,
    default: undefined,
  },
  gateway: {
    type: Object as PropType<StationLayoutGateway>,
    required: true,
  },
  translate: {
    type: Function as PropType<StationLayoutTranslate>,
    default: undefined,
  },
  formatError: {
    type: Function as PropType<StationLayoutErrorFormatter>,
    default: undefined,
  },
  readonly: {
    type: Boolean,
    default: false,
  },
});
const emit = defineEmits<{
  'update:stationSchemeId': [stationSchemeId: string];
}>();
const layoutRef = ref<InstanceType<typeof StationLayoutImpl> | null>(null);
/** Export every element and setting in the current editable layout. */
function exportJson(): string {
  if (!layoutRef.value) throw new Error("Station layout is not mounted.");
  return layoutRef.value.exportJson();
}
/** Replace the current local layout; persistence remains the host save action. */
async function importJson(json: string): Promise<void> {
  if (!layoutRef.value) throw new Error("Station layout is not mounted.");
  await layoutRef.value.importJson(json);
}
defineExpose({ exportJson, importJson });
</script>

<template>
  <StationLayoutImpl
    ref="layoutRef"
    :selected-instance-id="props.selectedInstanceId"
    :station-scheme-id="props.stationSchemeId"
    @update:station-scheme-id="emit('update:stationSchemeId', $event)"
    :gateway="props.gateway"
    :translate="props.translate"
    :format-error="props.formatError"
    :readonly="props.readonly"
    v-bind="$attrs"
  />
</template>
