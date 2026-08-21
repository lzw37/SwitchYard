declare const _default: typeof __VLS_export;
export default _default;
declare const __VLS_export: import("vue").DefineComponent<import("vue").ExtractPropTypes<{
    selectedInstanceId: {
        type: StringConstructor;
        default: string;
    };
    gateway: {
        type: ObjectConstructor;
        required: true;
    };
    translate: {
        type: FunctionConstructor;
        default: null;
    };
    formatError: {
        type: FunctionConstructor;
        default: null;
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
        type: ObjectConstructor;
        required: true;
    };
    translate: {
        type: FunctionConstructor;
        default: null;
    };
    formatError: {
        type: FunctionConstructor;
        default: null;
    };
    readonly: {
        type: BooleanConstructor;
        default: boolean;
    };
}>> & Readonly<{}>, {
    translate: Function;
    selectedInstanceId: string;
    formatError: Function;
    readonly: boolean;
}, {}, {}, {}, string, import("vue").ComponentProvideOptions, true, {}, any>;
