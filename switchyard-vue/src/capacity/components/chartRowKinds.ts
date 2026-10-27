interface ChartCellReference {
    id: string
    linkIDList?: string
    linkIDs?: string[]
}

/** Identify named tracks from physical Cell-to-Link references, never from a Cell's display name. */
export function namedTrackCellNames(cells: ChartCellReference[], tracks: { id: string; name: string }[]): Map<string, string[]> {
    const namesByLink = new Map(tracks.map(track => [track.id.trim(), track.name.trim()] as const)
        .filter(([id, name]) => id && name))
    const result = new Map<string, string[]>()
    for (const cell of cells) {
        const id = cell.id.trim()
        if (!id) continue
        const linkIDs = cell.linkIDs ?? parseLinkIDs(cell.linkIDList || '')
        const names = new Set(result.get(id) || [])
        for (const linkID of linkIDs) {
            const name = namesByLink.get(linkID.trim())
            if (name) names.add(name)
        }
        if (names.size) result.set(id, [...names])
    }
    return result
}

function parseLinkIDs(value: string): string[] {
    try {
        const parsed: unknown = JSON.parse(value)
        if (Array.isArray(parsed)) return parsed.map(item => String(item).trim()).filter(Boolean)
    } catch { /* Existing layouts also store delimited Link IDs. */ }
    return value.split(/(?:\s*->\s*)|[,，;；\s]+/).map(id => id.trim()).filter(Boolean)
}
