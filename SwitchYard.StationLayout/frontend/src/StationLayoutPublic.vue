<script setup lang="ts">
import type { PropType } from "vue";
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
</script>

<template>
  <StationLayoutImpl
    :selected-instance-id="props.selectedInstanceId"
    :gateway="props.gateway"
    :translate="props.translate"
    :format-error="props.formatError"
    :readonly="props.readonly"
    v-bind="$attrs"
  />
</template>
