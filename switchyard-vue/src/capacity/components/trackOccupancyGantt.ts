// Shared geometry keeps bars and overlapping lanes consistent in every Capacity view.
export const trackOccupancyGanttMetrics = {
    sidebarWidth: 168,
    rowHeight: 38,
    axisHeight: 44,
    barHeight: 24,
    barInset: 7,
    lanePitch: 28,
    minBarWidth: 8,
} as const

export function fitTrackOccupancyGantt(width: number, height: number, timelineWidth: number, rowsHeight: number) {
    return {
        scaleX: width > trackOccupancyGanttMetrics.sidebarWidth && timelineWidth > 0
            ? (width - trackOccupancyGanttMetrics.sidebarWidth) / timelineWidth : 1,
        scaleY: height > trackOccupancyGanttMetrics.axisHeight && rowsHeight > 0
            ? (height - trackOccupancyGanttMetrics.axisHeight) / rowsHeight : 1,
    }
}

export function getTrackOccupancyGanttTimeScale(timeSpan: number, minimumWidth: number, targetScale: number) {
    const span = Math.max(1, timeSpan)
    return Math.max(minimumWidth, span * targetScale) / span
}

export interface TrackOccupancyGanttTick {
    key: string | number
    left: number
    label: string | number
    major?: boolean
    zero?: boolean
}

export interface TrackOccupancyGanttBlock {
    key: string
    left: number
    width: number
    top?: number
    label: string
    title?: string
    color?: string
    className?: string
    editable?: boolean
}

export interface TrackOccupancyGanttRow {
    key: string
    label: string
    height?: number
    blocks: TrackOccupancyGanttBlock[]
}

export type TrackOccupancyGanttDragMode = 'start' | 'end' | 'move'

export interface TrackOccupancyGanttDragStart {
    event: PointerEvent
    rowKey: string
    blockKey: string
    mode: TrackOccupancyGanttDragMode
}
