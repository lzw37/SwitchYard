/** The two stored offsets use different reference events: start=0 and end=duration. */
export function operationOccupancyWindow(startShift: number | null, endShift: number | null, duration: number | null) {
    if (duration === null || !Number.isFinite(duration) || duration < 0
        || startShift === null || !Number.isFinite(startShift) || endShift === null || !Number.isFinite(endShift)) return null
    return { start: startShift, operationStart: 0, operationEnd: duration, release: duration + endShift }
}

/** Compare absolute positions: the end offset is relative to operation end. */
export function isOperationOccupancyOrderValid(start: number | null, end: number | null, duration: number | null) {
    const window = operationOccupancyWindow(start, end, duration)
    return window !== null && window.release >= window.start
}

export function dragOperationOccupancy(start: number, end: number,
    mode: 'start' | 'end' | 'move', delta: number) {
    return {
        startOccupationShift: mode === 'start' ? start + delta : mode === 'move' ? start + delta : start,
        endOccupationShift: mode === 'end' ? end + delta : mode === 'move' ? end + delta : end,
    }
}
