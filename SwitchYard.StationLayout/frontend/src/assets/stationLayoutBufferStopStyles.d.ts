export interface StationLayoutBufferStopStyleElement {
    tag: string
    attrs: Record<string, string | number>
}

export interface StationLayoutBufferStopStyleAsset {
    className: string
    width: number
    height: number
    elements: StationLayoutBufferStopStyleElement[]
}

export const DEFAULT_BUFFER_STOP_DIRECTION: string
export const DEFAULT_BUFFER_STOP_TYPE: string
export const bufferStopDirectionOptions: Array<{ label: string; value: string }>
export const bufferStopTypeOptions: Array<{ label: string; value: string }>
export const bufferStopStyleAssets: Record<string, StationLayoutBufferStopStyleAsset>

export function normalizeBufferStopDirection(value: unknown): string
export function normalizeBufferStopType(value: unknown): string
export function getBufferStopStyleAsset(type: unknown): StationLayoutBufferStopStyleAsset
