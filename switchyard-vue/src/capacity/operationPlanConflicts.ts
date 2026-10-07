export interface ConflictOccupation {
    key: string
    trainID: string
    movementID: string
    cellID: string
    startSeconds: number
    endSeconds: number
}

export interface OccupationConflictDetail {
    trainID: string
    movementID: string
    cellID: string
    startSeconds: number
    endSeconds: number
}

export interface OccupationConflict {
    groupID: string
    color: string
    details: OccupationConflictDetail[]
}

export interface ResourceConflictMarker { color: string; title: string }

export const conflictMovementKey = (trainID: string, movementID: string) => JSON.stringify([trainID, movementID])
export const conflictOccupationKey = (trainID: string, movementID: string, cellID: string) => JSON.stringify([trainID, movementID, cellID])

// Distinct saturated colors stay visible on both pale chart rows and train lines.
const conflictColors = ['#dc2626', '#0891b2', '#9333ea', '#16a34a', '#ea580c', '#2563eb', '#db2777', '#a16207']

/** Compare absolute Cell intervals before any diagram filtering. End-to-start contact is not an overlap. */
export function findOccupationConflicts(occupations: readonly ConflictOccupation[]) {
    const cells = new Map<string, ConflictOccupation[]>()
    for (const occupation of occupations) {
        if (!occupation.cellID || !occupation.trainID || !occupation.movementID ||
            !Number.isFinite(occupation.startSeconds) || !Number.isFinite(occupation.endSeconds) ||
            occupation.endSeconds <= occupation.startSeconds) continue
        const rows = cells.get(occupation.cellID) || []
        rows.push(occupation)
        cells.set(occupation.cellID, rows)
    }
    const parents = new Map<string, string>()
    const root = (key: string): string => {
        const parent = parents.get(key)
        if (!parent) { parents.set(key, key); return key }
        if (parent === key) return key
        const result = root(parent)
        parents.set(key, result)
        return result
    }
    const movementDetails = new Map<string, Map<string, OccupationConflictDetail>>()
    const occupationDetails = new Map<string, Map<string, OccupationConflictDetail>>()
    const occupationMovements = new Map<string, string>()
    const add = (map: Map<string, Map<string, OccupationConflictDetail>>, key: string, detail: OccupationConflictDetail) => {
        const details = map.get(key) || new Map<string, OccupationConflictDetail>()
        details.set(JSON.stringify([detail.trainID, detail.movementID, detail.cellID, detail.startSeconds, detail.endSeconds]), detail)
        map.set(key, details)
    }
    for (const rows of cells.values()) {
        rows.sort((a, b) => a.startSeconds - b.startSeconds || a.endSeconds - b.endSeconds || a.key.localeCompare(b.key))
        let active: ConflictOccupation[] = []
        for (const current of rows) {
            active = active.filter(other => other.endSeconds > current.startSeconds)
            const currentKey = conflictMovementKey(current.trainID, current.movementID)
            for (const other of active) {
                const otherKey = conflictMovementKey(other.trainID, other.movementID)
                if (currentKey === otherKey) continue
                const first = root(currentKey), second = root(otherKey)
                // A connected group has one color in both charts; tooltips retain only direct conflicts.
                if (first !== second) parents.set(first < second ? second : first, first < second ? first : second)
                const startSeconds = current.startSeconds, endSeconds = Math.min(current.endSeconds, other.endSeconds)
                for (const [own, peer, key] of [[current, other, currentKey], [other, current, otherKey]] as const) {
                    const detail = { trainID: peer.trainID, movementID: peer.movementID, cellID: own.cellID, startSeconds, endSeconds }
                    add(movementDetails, key, detail)
                    add(occupationDetails, own.key, detail)
                    occupationMovements.set(own.key, key)
                }
            }
            active.push(current)
        }
    }
    const colors = new Map<string, string>()
    const groups = [...new Set([...movementDetails.keys()].map(root))].sort()
    groups.forEach((group, index) => {
        // Continue around the full hue circle instead of repeating the palette
        // when a plan contains many independent conflict groups.
        colors.set(group, conflictColors[index] ?? `hsl(${((index * 137.508) % 360).toFixed(3)} 80% 38%)`)
    })
    const marker = (movementKey: string, details: Map<string, OccupationConflictDetail>): OccupationConflict => {
        const groupID = root(movementKey)
        return { groupID, color: colors.get(groupID)!, details: [...details.values()].sort((a, b) =>
            a.trainID.localeCompare(b.trainID) || a.movementID.localeCompare(b.movementID) ||
            a.cellID.localeCompare(b.cellID) || a.startSeconds - b.startSeconds || a.endSeconds - b.endSeconds) }
    }
    return {
        movements: new Map([...movementDetails].map(([key, details]) => [key, marker(key, details)])),
        occupations: new Map([...occupationDetails].map(([key, details]) => [key, marker(occupationMovements.get(key)!, details)])),
    }
}
