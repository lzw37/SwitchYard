export interface MovementCellOccupationOverride {
    routeID: string
    startOccupationShift: number
    endOccupationShift: number
    stationPlanCellID?: string
    stationPlanArriveMinutes?: number
    stationPlanDepartMinutes?: number
}

export function readMovementCellOccupationOverrides(json: string | null | undefined): Record<string, MovementCellOccupationOverride> {
    if (!json) return {}
    try {
        const entries = JSON.parse(json)
        if (!entries || typeof entries !== 'object' || Array.isArray(entries)) return {}
        return Object.fromEntries(Object.entries(entries).flatMap(([cellID, value]) => {
            const row = value as Partial<MovementCellOccupationOverride>
            return row && typeof row.routeID === 'string' && Number.isInteger(row.startOccupationShift) && Number.isInteger(row.endOccupationShift)
                ? [[cellID, row as MovementCellOccupationOverride]] : []
        }))
    } catch {
        return {}
    }
}

export function getMovementCellOccupationShifts(
    json: string | null | undefined,
    routeID: string,
    cellID: string,
    defaults: { startOccupationShift?: number | null; endOccupationShift?: number | null },
) {
    const override = readMovementCellOccupationOverrides(json)[cellID]
    return override?.routeID === routeID ? override : {
        startOccupationShift: defaults.startOccupationShift ?? 0,
        endOccupationShift: defaults.endOccupationShift ?? 0,
    }
}

export function setMovementCellOccupationOverride(
    json: string | null | undefined,
    cellID: string,
    override: MovementCellOccupationOverride,
) {
    return JSON.stringify({ ...readMovementCellOccupationOverrides(json), [cellID]: { ...override } })
}

export function mergeMovementCellOccupationTimes<T extends { cellID: string; startOccupationShift: number | null; endOccupationShift: number | null }>(
    json: string | null | undefined, routeID: string, defaults: T[],
): (T | { cellID: string; startOccupationShift: number; endOccupationShift: number })[] {
    const rows = new Map(defaults.map(row => [row.cellID, row] as const))
    const extra: { cellID: string; startOccupationShift: number; endOccupationShift: number }[] = []
    for (const [cellID, override] of Object.entries(readMovementCellOccupationOverrides(json))) {
        if (override.routeID !== routeID) continue
        const row = rows.get(cellID)
        if (row) rows.set(cellID, { ...row, ...override })
        else extra.push({ cellID, ...override })
    }
    return [...rows.values(), ...extra]
}
