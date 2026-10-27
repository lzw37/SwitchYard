export interface Action {
    execute(): Promise<void>
    undo(): Promise<void>
}

/** A successful action occupies one entry, including when it is waiting to be redone. */
export class ActionStack {
    readonly capacity = 20
    busy = false
    private actions: Action[] = []
    private position = 0
    private generation = 0

    get undoCount() { return this.position }
    get redoCount() { return this.actions.length - this.position }
    get canUndo() { return !this.busy && this.undoCount > 0 }
    get canRedo() { return !this.busy && this.redoCount > 0 }

    clear() {
        this.actions = []
        this.position = 0
        this.generation++
    }

    async execute(action: Action) {
        if (this.busy) return false
        const generation = this.generation
        this.busy = true
        try {
            await action.execute()
            if (generation !== this.generation) return false
            this.actions = [...this.actions.slice(0, this.position), action].slice(-this.capacity)
            this.position = this.actions.length
            return true
        } finally { this.busy = false }
    }

    async undo() {
        if (!this.canUndo) return false
        const generation = this.generation
        this.busy = true
        try {
            await this.actions[this.position - 1]!.undo()
            if (generation !== this.generation) return false
            this.position--
            return true
        } finally { this.busy = false }
    }

    async redo() {
        if (!this.canRedo) return false
        const generation = this.generation
        this.busy = true
        try {
            await this.actions[this.position]!.execute()
            if (generation !== this.generation) return false
            this.position++
            return true
        } finally { this.busy = false }
    }
}
