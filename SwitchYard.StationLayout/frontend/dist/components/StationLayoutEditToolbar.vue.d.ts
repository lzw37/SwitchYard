import type { StationLayoutTranslate } from '../gateway';
type __VLS_Props = {
    translate?: StationLayoutTranslate;
};
type __VLS_Slots = {
    context(): unknown;
    primary(): unknown;
    actions(): unknown;
    essential(): unknown;
    advanced(): unknown;
};
declare const density: import("vue").ModelRef<string, string, string, string>;
type __VLS_ModelProps = {
    'density'?: typeof density['value'];
};
type __VLS_PublicProps = __VLS_Props & __VLS_ModelProps;
declare const __VLS_base: import("vue").DefineComponent<__VLS_PublicProps, {}, {}, {}, {}, import("vue").ComponentOptionsMixin, import("vue").ComponentOptionsMixin, {
    "update:density": (value: string) => any;
}, string, import("vue").PublicProps, Readonly<__VLS_PublicProps> & Readonly<{
    "onUpdate:density"?: ((value: string) => any) | undefined;
}>, {}, {}, {}, {}, string, import("vue").ComponentProvideOptions, false, {}, any>;
declare const __VLS_export: __VLS_WithSlots<typeof __VLS_base, __VLS_Slots>;
declare const _default: typeof __VLS_export;
export default _default;
type __VLS_WithSlots<T, S> = T & {
    new (): {
        $slots: S;
    };
};
