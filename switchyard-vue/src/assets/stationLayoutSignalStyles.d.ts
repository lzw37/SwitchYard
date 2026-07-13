export interface StationLayoutSignalStyleElement {
    tag: string
    attrs: Record<string, string | number>
}

export interface StationLayoutSignalStyleAsset {
    className: string
    placement?: string
    width?: number
    height?: number
    bounds?: {
        minX: number
        minY: number
        maxX: number
        maxY: number
        width: number
        height: number
    }
    elements: StationLayoutSignalStyleElement[]
}

export const DEFAULT_SIGNAL_TYPE: string
export const signalTypeOptions: Array<{ label: string; value: string }>
export const signalTypeMenuOptions: Array<{
    label: string
    value: string
    children?: Array<{ label: string; value: string }>
}>

export function normalizeSignalType(value: unknown): string
export function getSignalStyleAsset(type: unknown): StationLayoutSignalStyleAsset
