export function normalizeBufferStopDirection(value: any): any;
export function normalizeBufferStopType(value: any): any;
export function getBufferStopStyleAsset(type: any): any;
export const DEFAULT_BUFFER_STOP_DIRECTION: "right";
export const DEFAULT_BUFFER_STOP_TYPE: "normal";
export const bufferStopDirectionOptions: {
    label: string;
    value: string;
}[];
export const bufferStopTypeOptions: {
    label: string;
    value: string;
}[];
export namespace bufferStopStyleAssets {
    namespace normal {
        let className: string;
        let width: number;
        let height: number;
        let elements: ({
            tag: string;
            attrs: {
                y1: number;
                x2: number;
                y2: number;
                x1?: undefined;
            };
        } | {
            tag: string;
            attrs: {
                x1: number;
                y1: number;
                x2: number;
                y2: number;
            };
        })[];
    }
    namespace ext {
        let className_1: string;
        export { className_1 as className };
        let width_1: number;
        export { width_1 as width };
        let height_1: number;
        export { height_1 as height };
        let elements_1: ({
            tag: string;
            attrs: {
                y1: number;
                x2: number;
                y2: number;
                x1?: undefined;
            };
        } | {
            tag: string;
            attrs: {
                x1: number;
                y1: number;
                x2: number;
                y2: number;
            };
        })[];
        export { elements_1 as elements };
    }
}
