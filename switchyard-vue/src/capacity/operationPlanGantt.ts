import type { TrackOccupancyGanttDragMode } from './components/trackOccupancyGantt'

export interface OperationPlanGanttWindow {
    startMinutes: number
    endMinutes: number
    minDurationSeconds: number | null
    minimumStartMinutes?: number | null
}

// Apply a delta to one time window. Cell occupancies may extend before midnight.
// Work in whole seconds to preserve imported occupancy precision and day boundaries.
export function adjustOperationPlanGanttWindow(
    window: OperationPlanGanttWindow,
    mode: TrackOccupancyGanttDragMode,
    deltaMinutes: number,
): { startMinutes: number; endMinutes: number } {
    const start = Math.round(window.startMinutes * 60)
    const end = Math.round(window.endMinutes * 60)
    const delta = Math.round(deltaMinutes * 60)
    const minDuration = Math.max(0, Math.ceil(window.minDurationSeconds || 0))
    const minimumStart = window.minimumStartMinutes === null ? -Infinity : Math.round((window.minimumStartMinutes ?? 0) * 60)
    if (mode === 'move') {
        const appliedDelta = Math.max(minimumStart - start, delta)
        return { startMinutes: (start + appliedDelta) / 60, endMinutes: (end + appliedDelta) / 60 }
    }
    if (mode === 'start') {
        return { startMinutes: Math.max(minimumStart, Math.min(start + delta, end - minDuration)) / 60, endMinutes: end / 60 }
    }
    return { startMinutes: start / 60, endMinutes: Math.max(start + minDuration, end + delta) / 60 }
}
