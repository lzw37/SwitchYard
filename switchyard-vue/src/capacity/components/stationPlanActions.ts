import type { Action } from '../../utils/actionStack.ts'
import type { StationPlanSegmentEdit } from './stationPlanView.ts'

interface MovementIdentity { trainID: string; movementID: string }
type SaveMovements<T> = (expected: T[], desired: T[]) => Promise<T[]>
type StationPlanActionKind = StationPlanSegmentEdit['mode'] | 'track' | 'dwelling'

/** Keep server-returned movement snapshots for concurrency checks when undoing or redoing plan edits. */
export class StationPlanMovementAction<T extends MovementIdentity> implements Action {
    readonly kind: StationPlanActionKind
    private before: T[]
    private after: T[]
    private save: SaveMovements<T>

    constructor(kind: StationPlanActionKind, before: T[], after: T[], save: SaveMovements<T>) {
        this.kind = kind
        this.before = before.map(row => ({ ...row }))
        this.after = after.map(row => ({ ...row }))
        this.save = save
    }

    async execute() {
        this.after = (await this.save(this.before.map(row => ({ ...row })), this.after.map(row => ({ ...row })))).map(row => ({ ...row }))
    }

    async undo() {
        this.before = (await this.save(this.after.map(row => ({ ...row })), this.before.map(row => ({ ...row })))).map(row => ({ ...row }))
    }
}

export function stationPlanHistoryShortcut(event: {
    key: string; ctrlKey: boolean; altKey: boolean; shiftKey: boolean; metaKey: boolean
    repeat?: boolean; isComposing?: boolean; defaultPrevented?: boolean
}, editingText = false): 'undo' | 'redo' | null {
    if (editingText || event.defaultPrevented || event.repeat || event.isComposing || !event.ctrlKey || event.altKey || event.shiftKey || event.metaKey) return null
    const key = event.key.toLowerCase()
    return key === 'z' ? 'undo' : key === 'y' ? 'redo' : null
}
