import type { PropType } from "vue";
import type { StationLayoutErrorFormatter, StationLayoutGateway, StationLayoutTranslate } from "./gateway";
declare const __VLS_export: import("vue").DefineComponent<import("vue").ExtractPropTypes<{
    selectedInstanceId: {
        type: StringConstructor;
        default: string;
    };
    gateway: {
        type: PropType<StationLayoutGateway>;
        required: true;
    };
    translate: {
        type: PropType<StationLayoutTranslate>;
        default: undefined;
    };
    formatError: {
        type: PropType<StationLayoutErrorFormatter>;
        default: undefined;
    };
    readonly: {
        type: BooleanConstructor;
        default: boolean;
    };
}>, {}, {}, {}, {}, import("vue").ComponentOptionsMixin, import("vue").ComponentOptionsMixin, {}, string, import("vue").PublicProps, Readonly<import("vue").ExtractPropTypes<{
    selectedInstanceId: {
        type: StringConstructor;
        default: string;
    };
    gateway: {
        type: PropType<StationLayoutGateway>;
        required: true;
    };
    translate: {
        type: PropType<StationLayoutTranslate>;
        default: undefined;
    };
    formatError: {
        type: PropType<StationLayoutErrorFormatter>;
        default: undefined;
    };
    readonly: {
        type: BooleanConstructor;
        default: boolean;
    };
}>> & Readonly<{}>, {
    translate: StationLayoutTranslate;
    selectedInstanceId: string;
    formatError: StationLayoutErrorFormatter;
    readonly: boolean;
}, {}, {}, {}, string, import("vue").ComponentProvideOptions, true, {}, any>;
declare const _default: typeof __VLS_export;
export default _default;
