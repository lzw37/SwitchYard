export interface TrackOccupancyInterval {
    cellID: string
    startSeconds: number
    endSeconds: number
    color: string
}

export interface TrackOccupancyCell {
    id: string
    linkIDList: string
}

export interface TrackOccupancyTimeline {
    sample(seconds: number): ReadonlyMap<string, readonly string[]>
}

function parseLinkIds(value: string): string[] {
    const text = String(value || '').trim()
    if (!text) return []
    let values: unknown[] = text.split(/(?:\s*->\s*)|(?:\s*[,，、\n\r]\s*)|\s+/)
    try {
        const parsed: unknown = JSON.parse(text)
        if (Array.isArray(parsed)) values = parsed
    } catch {
        // Cell Link lists also use the same plain-text format as route lists.
    }
    return [...new Set(values.map(value => String(value ?? '').trim()).filter(Boolean))]
}

/** Resolve cells once; playback only rebuilds occupied links when a time boundary is crossed. */
export function createTrackOccupancyTimeline(
    intervals: readonly TrackOccupancyInterval[],
    cells: readonly TrackOccupancyCell[],
): TrackOccupancyTimeline {
    const linksByCell = new Map<string, Set<string>>()
    for (const cell of cells) {
        const cellID = String(cell.id || '').trim()
        if (!cellID) continue
        const links = linksByCell.get(cellID) || new Set<string>()
        for (const linkID of parseLinkIds(cell.linkIDList)) links.add(linkID)
        linksByCell.set(cellID, links)
    }

    const resolved: { start: number; end: number; color: string; links: readonly string[] }[] = []
    const events = new Set<number>()
    for (const interval of intervals) {
        const { startSeconds: start, endSeconds: end } = interval
        const color = String(interval.color || '').trim()
        const links = linksByCell.get(String(interval.cellID || '').trim())
        if (!Number.isFinite(start) || !Number.isFinite(end) || end <= start || !color || !links?.size) continue
        resolved.push({ start, end, color, links: [...links] })
        events.add(start)
        events.add(end)
    }

    const boundaries = [...events].sort((left, right) => left - right)
    const empty: ReadonlyMap<string, readonly string[]> = new Map()
    let cachedBoundaryIndex = -1
    let cached: ReadonlyMap<string, readonly string[]> = empty

    return {
        sample(seconds) {
            if (!Number.isFinite(seconds)) return empty
            // Upper bound implements inclusive starts and exclusive ends, including
            // simultaneous release/occupation events and arbitrary backward seeks.
            let low = 0
            let high = boundaries.length
            while (low < high) {
                const middle = (low + high) >>> 1
                if (boundaries[middle]! <= seconds) low = middle + 1
                else high = middle
            }
            if (low === cachedBoundaryIndex) return cached
            cachedBoundaryIndex = low

            const colorsByLink = new Map<string, Set<string>>()
            for (const interval of resolved) {
                if (seconds < interval.start || seconds >= interval.end) continue
                for (const linkID of interval.links) {
                    const colors = colorsByLink.get(linkID) || new Set<string>()
                    colors.add(interval.color)
                    colorsByLink.set(linkID, colors)
                }
            }
            cached = colorsByLink.size > 0
                ? new Map([...colorsByLink.entries()]
                    .sort(([left], [right]) => left < right ? -1 : left > right ? 1 : 0)
                    .map(([linkID, colors]) => [linkID, Object.freeze([...colors].sort())]))
                : empty
            return cached
        },
    }
}
