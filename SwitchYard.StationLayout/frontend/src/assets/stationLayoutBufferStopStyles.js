export const DEFAULT_BUFFER_STOP_DIRECTION = "right";
export const DEFAULT_BUFFER_STOP_TYPE = "normal";

export const bufferStopDirectionOptions = [
    { label: "向左", value: "left" },
    { label: "向右", value: "right" },
];

export const bufferStopTypeOptions = [
    { label: "普通", value: "normal" },
    { label: "延伸式", value: "ext" },
];

const bufferStopDirectionAliases = {
    l: "left",
    left: "left",
    左: "left",
    向左: "left",
    r: "right",
    right: "right",
    右: "right",
    向右: "right",
};

const bufferStopTypeAliases = {
    n: "normal",
    normal: "normal",
    普通: "normal",
    ext: "ext",
    e: "ext",
    extend: "ext",
    extended: "ext",
    extension: "ext",
    延伸: "ext",
    延申: "ext",
};

export const bufferStopStyleAssets = {
    normal: {
        className: "bufferstop-normal",
        width: 26.7659,
        height: 11.22,
        elements: [
            { tag: "line", attrs: { y1: 5.7064, x2: 16.9215, y2: 5.7064 } },
            { tag: "line", attrs: { x1: 16.9215, y1: 10.72, x2: 16.9215, y2: 0.5 } },
            { tag: "line", attrs: { x1: 16.9215, y1: 0.5, x2: 26.7659, y2: 0.5 } },
            { tag: "line", attrs: { x1: 16.9215, y1: 10.72, x2: 26.7659, y2: 10.72 } },
        ],
    },
    ext: {
        className: "bufferstop-ext",
        width: 40.7659,
        height: 11.22,
        elements: [
            { tag: "line", attrs: { y1: 5.61, x2: 4.617, y2: 5.61 } },
            { tag: "line", attrs: { x1: 30.9215, y1: 10.72, x2: 30.9215, y2: 0.5 } },
            { tag: "line", attrs: { x1: 30.9215, y1: 0.5, x2: 40.7659, y2: 0.5 } },
            { tag: "line", attrs: { x1: 30.9215, y1: 10.72, x2: 40.7659, y2: 10.72 } },
            { tag: "line", attrs: { x1: 4.617, y1: 5.61, x2: 8.4607, y2: 0.4123 } },
            { tag: "line", attrs: { x1: 8.4607, y1: 0.4123, x2: 10.3008, y2: 10.6111 } },
            { tag: "line", attrs: { x1: 10.3008, y1: 10.6111, x2: 13.8158, y2: 5.4508 } },
            { tag: "line", attrs: { x1: 13.8158, y1: 5.4508, x2: 30.9215, y2: 5.4508 } },
        ],
    },
};

export function normalizeBufferStopDirection(value) {
    const rawDirection = String(value ?? "").trim();
    if (!rawDirection) return DEFAULT_BUFFER_STOP_DIRECTION;
    return bufferStopDirectionAliases[rawDirection.toLowerCase()] || rawDirection;
}

export function normalizeBufferStopType(value) {
    const rawType = String(value ?? "").trim();
    if (!rawType) return DEFAULT_BUFFER_STOP_TYPE;
    const normalizedType = bufferStopTypeAliases[rawType.toLowerCase()] || rawType;
    return bufferStopStyleAssets[normalizedType] ? normalizedType : DEFAULT_BUFFER_STOP_TYPE;
}

export function getBufferStopStyleAsset(type) {
    const normalizedType = normalizeBufferStopType(type);
    return bufferStopStyleAssets[normalizedType] || bufferStopStyleAssets[DEFAULT_BUFFER_STOP_TYPE];
}
