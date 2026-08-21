export function normalizeSignalType(value: any): any;
export function getSignalStyleAsset(type: any): any;
export const DEFAULT_SIGNAL_TYPE: "DepartureSignal";
export namespace signalStyleAssets {
    let DepartureSignal: {
        className: string;
        placement: string;
        width: number;
        height: number;
        bounds: {
            minX: number;
            minY: number;
            maxX: number;
            maxY: number;
            width: number;
            height: number;
        };
        elements: {
            tag: string;
            attrs: {
                transform: any;
            };
        }[];
    } | {
        className: string;
        width: number;
        height: number;
        bounds: {
            minX: number;
            minY: number;
            maxX: number;
            maxY: number;
        };
        elements: ({
            tag: string;
            attrs: {
                cx: number;
                cy: number;
                r: number;
                fill: string;
                x1?: undefined;
                y1?: undefined;
                x2?: undefined;
                y2?: undefined;
            };
        } | {
            tag: string;
            attrs: {
                x1: number;
                y1: number;
                x2: number;
                y2: number;
                fill: string;
                cx?: undefined;
                cy?: undefined;
                r?: undefined;
            };
        })[];
    };
    let HomeSignal: {
        className: string;
        placement: string;
        width: number;
        height: number;
        bounds: {
            minX: number;
            minY: number;
            maxX: number;
            maxY: number;
            width: number;
            height: number;
        };
        elements: {
            tag: string;
            attrs: {
                transform: any;
            };
        }[];
    } | {
        className: string;
        width: number;
        height: number;
        bounds: {
            minX: number;
            minY: number;
            maxX: number;
            maxY: number;
        };
        elements: ({
            tag: string;
            attrs: {
                x1: number;
                y1: number;
                x2: number;
                y2: number;
                fill: string;
                x?: undefined;
                y?: undefined;
                width?: undefined;
                height?: undefined;
                rx?: undefined;
                cx?: undefined;
                cy?: undefined;
                r?: undefined;
            };
        } | {
            tag: string;
            attrs: {
                x: number;
                y: number;
                width: number;
                height: number;
                rx: number;
                fill: string;
                x1?: undefined;
                y1?: undefined;
                x2?: undefined;
                y2?: undefined;
                cx?: undefined;
                cy?: undefined;
                r?: undefined;
            };
        } | {
            tag: string;
            attrs: {
                cx: number;
                cy: number;
                r: number;
                fill: string;
                x1?: undefined;
                y1?: undefined;
                x2?: undefined;
                y2?: undefined;
                x?: undefined;
                y?: undefined;
                width?: undefined;
                height?: undefined;
                rx?: undefined;
            };
        })[];
    };
    let ShuntingSignal: {
        className: string;
        placement: string;
        width: number;
        height: number;
        bounds: {
            minX: number;
            minY: number;
            maxX: number;
            maxY: number;
            width: number;
            height: number;
        };
        elements: {
            tag: string;
            attrs: {
                transform: any;
            };
        }[];
    } | {
        className: string;
        width: number;
        height: number;
        bounds: {
            minX: number;
            minY: number;
            maxX: number;
            maxY: number;
        };
        elements: ({
            tag: string;
            attrs: {
                x1: number;
                y1: number;
                x2: number;
                y2: number;
                fill: string;
                points?: undefined;
                cx?: undefined;
                cy?: undefined;
                r?: undefined;
            };
        } | {
            tag: string;
            attrs: {
                points: string;
                fill: string;
                x1?: undefined;
                y1?: undefined;
                x2?: undefined;
                y2?: undefined;
                cx?: undefined;
                cy?: undefined;
                r?: undefined;
            };
        } | {
            tag: string;
            attrs: {
                cx: number;
                cy: number;
                r: number;
                fill: string;
                x1?: undefined;
                y1?: undefined;
                x2?: undefined;
                y2?: undefined;
                points?: undefined;
            };
        })[];
    };
    namespace HumpSignal {
        let className: string;
        let width: number;
        let height: number;
        namespace bounds {
            let minX: number;
            let minY: number;
            let maxX: number;
            let maxY: number;
        }
        let elements: ({
            tag: string;
            attrs: {
                x1: number;
                y1: number;
                x2: number;
                y2: number;
                fill: string;
                d?: undefined;
                cx?: undefined;
                cy?: undefined;
                r?: undefined;
            };
        } | {
            tag: string;
            attrs: {
                d: string;
                fill: string;
                x1?: undefined;
                y1?: undefined;
                x2?: undefined;
                y2?: undefined;
                cx?: undefined;
                cy?: undefined;
                r?: undefined;
            };
        } | {
            tag: string;
            attrs: {
                cx: number;
                cy: number;
                r: number;
                fill: string;
                x1?: undefined;
                y1?: undefined;
                x2?: undefined;
                y2?: undefined;
                d?: undefined;
            };
        })[];
    }
}
export const signalTypeOptions: ({
    label: string;
    value: string;
} | {
    label: string;
    value: string;
})[];
export const signalTypeMenuOptions: ({
    label: any;
    value: any;
    children?: undefined;
} | {
    label: any;
    value: any;
    children: any;
})[];
