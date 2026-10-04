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
    kind?: 'track' | 'cell'
    trackNames?: string[]
    height?: number
    blocks: TrackOccupancyGanttBlock[]
}

export interface TrackOccupancyGanttRowHit {
    rowKey: string
    rowIndex: number
    /** Scaled content coordinates, including the fixed-height time axis. */
    rowTop: number
    rowHeight: number
}

export interface TrackOccupancyGanttViewportGeometry {
    left: number
    top: number
    /** The viewport's client dimensions exclude its scrollbars. */
    width: number
    height: number
    scrollLeft: number
    scrollTop: number
    scaleX: number
    scaleY: number
    timelineWidth: number
}

/** Hit the visible timeline only; sticky labels, the time axis and empty space are never row targets. */
export function hitTestTrackOccupancyGanttRow(
    rows: TrackOccupancyGanttRow[], geometry: TrackOccupancyGanttViewportGeometry,
    clientX: number, clientY: number, allowedRowKeys?: ReadonlySet<string>,
): TrackOccupancyGanttRowHit | null {
    const { left, top, width, height, scrollLeft, scrollTop, scaleX, scaleY, timelineWidth } = geometry
    if (![clientX, clientY, left, top, width, height, scrollLeft, scrollTop, scaleX, scaleY, timelineWidth].every(Number.isFinite) ||
        width <= 0 || height <= 0 || scaleX <= 0 || scaleY <= 0 || timelineWidth <= 0) return null
    const x = clientX - left, y = clientY - top
    if (x < trackOccupancyGanttMetrics.sidebarWidth || x >= width || y < trackOccupancyGanttMetrics.axisHeight || y >= height) return null
    const timelineX = x + scrollLeft - trackOccupancyGanttMetrics.sidebarWidth
    if (timelineX < 0 || timelineX >= timelineWidth * scaleX) return null
    const contentY = y + scrollTop
    let rowTop: number = trackOccupancyGanttMetrics.axisHeight
    for (const [rowIndex, row] of rows.entries()) {
        const rowHeight = (row.height ?? trackOccupancyGanttMetrics.rowHeight) * scaleY
        if (!Number.isFinite(rowHeight) || rowHeight < 0) return null
        if (contentY >= rowTop && contentY < rowTop + rowHeight) {
            return !allowedRowKeys || allowedRowKeys.has(row.key) ? { rowKey: row.key, rowIndex, rowTop, rowHeight } : null
        }
        rowTop += rowHeight
    }
    return null
}

export type TrackOccupancyGanttDragMode = 'start' | 'end' | 'move'

export interface TrackOccupancyGanttDragStart {
    event: PointerEvent
    rowKey: string
    blockKey: string
    mode: TrackOccupancyGanttDragMode
}
