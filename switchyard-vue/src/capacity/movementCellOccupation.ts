/** Absolute times returned by the plan API; the browser only converts seconds for drawing. */
export interface MovementCellOccupation {
    cellID: string
    routeID: string
    startSeconds: number
    endSeconds: number
    isInterruptCell: boolean
    displayCellID?: string | null
    isEdited: boolean
}

export function normalizeMovementCellOccupations(value: unknown): MovementCellOccupation[] {
    if (!Array.isArray(value)) return []
    return value.flatMap(row => {
        const cellID = String(row?.cellID ?? row?.CellID ?? '').trim()
        const startSeconds = row?.startSeconds ?? row?.StartSeconds
        const endSeconds = row?.endSeconds ?? row?.EndSeconds
        return cellID && Number.isFinite(startSeconds) && Number.isFinite(endSeconds) ? [{
            cellID, routeID: String(row.routeID ?? row.RouteID ?? ''), startSeconds, endSeconds,
            isInterruptCell: Boolean(row.isInterruptCell ?? row.IsInterruptCell),
            displayCellID: row.displayCellID ?? row.DisplayCellID ?? null,
            isEdited: Boolean(row.isEdited ?? row.IsEdited),
        }] : []
    })
}
