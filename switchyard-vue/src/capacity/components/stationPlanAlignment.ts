import { getStationPlanSegmentEditContext, type StationPlanProcessConstraints, type StationPlanSegmentEdit,
    type StationPlanTrain } from './stationPlanView.ts'
import { StationPlanEditingError } from './stationPlanEditing.ts'

interface AlignmentMovement {
    trainID: string
    movementID: string
    startMinutes: number | null
    endMinutes: number | null
}

export class StationPlanAlignmentError extends Error {
    constructor() { super('alignment-conflict') }
}

const equal = (a: number, b: number) => Math.abs(a - b) < 1e-8

/** Keep the clicked event fixed, satisfy its exact links, then translate directed descendants. */
export function alignStationPlanEvents<T extends AlignmentMovement>(
    train: StationPlanTrain, edit: StationPlanSegmentEdit, movements: T[], snapshot?: StationPlanProcessConstraints,
): (T & { startMinutes: number; endMinutes: number })[] {
    const context = getStationPlanSegmentEditContext(train, { ...edit, shiftFollowing: false })
    if (!context) throw new StationPlanEditingError()
    if (!snapshot) return []
    if (snapshot.trainID !== train.id) throw new StationPlanEditingError()
    const scoped = movements.filter(movement => movement.trainID === train.id)
    const byID = new Map(scoped.map(movement => [movement.movementID, movement]))
    const selected = byID.get(context.movementID)
    if (byID.size !== scoped.length || !selected || selected.startMinutes !== edit.segment.startMinutes ||
        selected.endMinutes !== edit.segment.endMinutes) throw new StationPlanEditingError()

    const endpoints = new Map<string, { movementID: string; edge: 'startMinutes' | 'endMinutes' }[]>()
    const following = new Map<string, Set<string>>()
    const exact = new Map<string, { eventID: string; offset: number }[]>()
    const addFollowing = (from: string, to: string) => {
        const next = following.get(from) || new Set<string>()
        next.add(to)
        following.set(from, next)
    }
    let referenceID = ''
    for (const activity of snapshot.process.activities) {
        const movementID = snapshot.activityMovementMap[activity.id]
        if (!movementID) throw new StationPlanEditingError()
        for (const [eventID, edge] of [[activity.startEvent, 'startMinutes'], [activity.endEvent, 'endMinutes']] as const) {
            const refs = endpoints.get(eventID) || []
            refs.push({ movementID, edge })
            endpoints.set(eventID, refs)
            if (movementID === selected.movementID && edge === `${edit.mode}Minutes`) referenceID = eventID
        }
        addFollowing(activity.startEvent, activity.endEvent)
    }
    if (!referenceID) throw new StationPlanEditingError()
    for (const precedence of snapshot.precedences ?? snapshot.process.precedences) {
        addFollowing(precedence.leadingEvent, precedence.followingEvent)
        if (precedence.isExact !== true) continue
        // New snapshots use seconds; older snapshots retain the process's minute interval.
        const interval = precedence.minIntervalSeconds !== undefined ? precedence.minIntervalSeconds / 60 : precedence.interval
        if (typeof interval !== 'number' || !Number.isFinite(interval) || interval < 0) throw new StationPlanEditingError()
        for (const [from, to, offset] of [[precedence.leadingEvent, precedence.followingEvent, interval],
            [precedence.followingEvent, precedence.leadingEvent, -interval]] as const) {
            const links = exact.get(from) || []
            links.push({ eventID: to, offset })
            exact.set(from, links)
        }
    }
    if (!exact.has(referenceID)) return []

    const currentTimes = new Map<string, number>()
    function currentTime(eventID: string): number {
        const cached = currentTimes.get(eventID)
        if (cached !== undefined) return cached
        const refs = endpoints.get(eventID)
        if (!refs?.length) throw new StationPlanEditingError()
        let time: number | undefined
        for (const ref of refs) {
            const value = byID.get(ref.movementID)?.[ref.edge]
            if (typeof value !== 'number' || !Number.isFinite(value) || (time !== undefined && !equal(time, value)))
                throw new StationPlanEditingError()
            time = value
        }
        currentTimes.set(eventID, time!)
        return time!
    }

    // Exact links are bidirectional: incoming links subtract the prescribed interval.
    const aligned = new Map([[referenceID, currentTime(referenceID)]])
    const pending = [referenceID]
    for (let index = 0; index < pending.length; index++) {
        const from = pending[index]!
        for (const link of exact.get(from) || []) {
            const target = aligned.get(from)! + link.offset
            if (aligned.has(link.eventID)) {
                if (!equal(aligned.get(link.eventID)!, target)) throw new StationPlanAlignmentError()
            } else {
                aligned.set(link.eventID, target)
                pending.push(link.eventID)
            }
        }
    }

    const shifts = new Map<string, number>()
    const queue: string[] = []
    for (const [eventID, target] of aligned) {
        const delta = target - currentTime(eventID)
        shifts.set(eventID, delta)
        if (!equal(delta, 0)) queue.push(eventID)
    }
    for (let index = 0; index < queue.length; index++) {
        const from = queue[index]!, delta = shifts.get(from)!
        for (const to of following.get(from) || []) {
            // Each aligned event starts its own displacement; never move the reference again.
            if (aligned.has(to)) continue
            if (shifts.has(to)) {
                if (!equal(shifts.get(to)!, delta)) throw new StationPlanAlignmentError()
                continue
            }
            currentTime(to)
            shifts.set(to, delta)
            queue.push(to)
        }
    }

    const changed = new Map<string, T & { startMinutes: number; endMinutes: number }>()
    for (const [eventID, delta] of shifts) {
        if (equal(delta, 0)) continue
        const target = aligned.get(eventID) ?? currentTime(eventID) + delta
        if (!Number.isFinite(target)) throw new StationPlanAlignmentError()
        for (const ref of endpoints.get(eventID)!) {
            const movement = byID.get(ref.movementID)!
            if (typeof movement.startMinutes !== 'number' || !Number.isFinite(movement.startMinutes) ||
                typeof movement.endMinutes !== 'number' || !Number.isFinite(movement.endMinutes)) throw new StationPlanEditingError()
            const updated = changed.get(ref.movementID) || { ...movement,
                startMinutes: movement.startMinutes, endMinutes: movement.endMinutes }
            updated[ref.edge] = target
            changed.set(ref.movementID, updated)
        }
    }
    return [...changed.values()]
}
