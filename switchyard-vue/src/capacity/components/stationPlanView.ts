import { getMovementCellOccupationShifts, readMovementCellOccupationOverrides } from '../movementCellOccupation.ts'

export interface StationPlanAxisRow {
    key: string
    sourceID: string
    label: string
    kind: 'cell' | 'endpoint'
}

export interface StationPlanVisit {
    rowKey: string
    arriveMinutes: number
    departMinutes: number
    detail: string
    movementID?: string
    sourceCellID?: string
    isAdjusted?: boolean
    pinDeparture?: boolean
    arriveAnchors?: StationPlanTimeAnchor[]
    departAnchors?: StationPlanTimeAnchor[]
}

export interface StationPlanTimeAnchor {
    movementID: string
    kind: 'cell' | 'movement'
    cellID?: string
    edge: 'start' | 'end'
}

function uniqueAnchors(anchors: StationPlanTimeAnchor[]) {
    return [...new Map(anchors.map(anchor => [JSON.stringify(anchor), anchor])).values()]
}

export interface StationPlanEditableSegment {
    rowKey: string
    visitIndexes: number[]
    startMinutes: number
    endMinutes: number
    startAnchors: StationPlanTimeAnchor[]
    endAnchors: StationPlanTimeAnchor[]
    x1: number
    x2: number
    y: number
}

export interface StationPlanSegmentEdit {
    trainID: string
    segment: StationPlanEditableSegment
    mode: 'start' | 'end' | 'row'
    startMinutes: number
    endMinutes: number
    rowKey: string
}

export interface StationPlanTrain {
    id: string
    label: string
    color: string
    visits: StationPlanVisit[]
}

export interface StationPlanMovement {
    trainID: string
    movementID: string
    name: string
    routeID: string
    routeIDList?: string
    startMinutes: number
    endMinutes: number
    sortOrder: number | null
    cellOccupationOverridesJson?: string | null
}

export interface StationPlanRoute {
    id: string
    name: string
    cellIDs: string[]
    startNodeID: string
    endNodeID: string
}

export interface StationPlanCellTime {
    cellID: string
    startOccupationShift: number | null
    endOccupationShift: number | null
    isInterruptCell: boolean
}

/** Only the outer endpoints of each train; intermediate route endpoints are not station entrances. */
export function stationPlanBoundaryEndpointIDs(trains: StationPlanTrain[]) {
    const ids = new Set<string>()
    for (const train of trains) {
        for (const visit of [train.visits[0], train.visits[train.visits.length - 1]]) {
            if (visit?.rowKey.startsWith('node:')) ids.add(visit.rowKey.slice(5))
        }
    }
    return [...ids]
}

/** Older plans can store a single route only in RouteIDList, as supported by playback. */
export function resolveStationPlanMovementRouteID(routeID: string, routeIDList = '') {
    if (routeID.trim()) return routeID.trim()
    let values: unknown[] = routeIDList.split(/(?:\s*->\s*)|(?:\s*[,，、\n\r]\s*)|\s+/)
    try {
        const parsed: unknown = JSON.parse(routeIDList)
        if (Array.isArray(parsed)) values = parsed
    } catch { /* Plain-text route lists are also supported. */ }
    return values.map(value => String(value ?? '').trim()).find(Boolean) || ''
}

/** Occupation overlaps are locking margins: hand over to the next route cell at its entry time. */
export function buildStationPlanTrains(
    trains: { id: string; label: string; color: string }[],
    movements: StationPlanMovement[],
    routes: StationPlanRoute[],
    timesForMovement: (movement: StationPlanMovement) => StationPlanCellTime[],
): StationPlanTrain[] {
    const routeMap = new Map(routes.map(route => [route.id, route]))
    return trains.map(train => {
        const visits: StationPlanVisit[] = []
        const trainMovements = movements.filter(movement => movement.trainID === train.id &&
            Number.isFinite(movement.startMinutes) && Number.isFinite(movement.endMinutes))
        const adjusted = trainMovements.some(movement => Object.values(readMovementCellOccupationOverrides(movement.cellOccupationOverridesJson))
            .some(window => window.stationPlanCellID !== undefined || window.stationPlanArriveMinutes !== undefined || window.stationPlanDepartMinutes !== undefined))
        const ordered = trainMovements.filter(movement => adjusted || movement.endMinutes >= movement.startMinutes).sort((a, b) => adjusted
            ? (a.sortOrder ?? movements.indexOf(a)) - (b.sortOrder ?? movements.indexOf(b))
            : a.startMinutes - b.startMinutes || (a.sortOrder ?? 0) - (b.sortOrder ?? 0) || a.movementID.localeCompare(b.movementID))
        let cursor = -Infinity
        let cursorAnchors: StationPlanTimeAnchor[] = []
        const append = (rowKey: string, arriveMinutes: number, departMinutes: number, detail: string, movementID: string,
            arriveSource: StationPlanTimeAnchor[], departSource: StationPlanTimeAnchor[], sourceCellID?: string, isAdjusted = false, pinDeparture = false) => {
            const arrive = isAdjusted ? arriveMinutes : Math.max(cursor, arriveMinutes)
            const depart = isAdjusted ? departMinutes : Math.max(arrive, departMinutes)
            const arriveAnchors = uniqueAnchors([...arriveSource, ...(!isAdjusted && cursor >= arriveMinutes ? cursorAnchors : [])])
            const departAnchors = uniqueAnchors([...departSource, ...(!isAdjusted && arrive >= departMinutes ? arriveAnchors : [])])
            visits.push({ rowKey, arriveMinutes: arrive, departMinutes: depart, detail, movementID, arriveAnchors, departAnchors, sourceCellID, isAdjusted, pinDeparture })
            cursor = depart
            cursorAnchors = departAnchors
        }
        for (const source of ordered) {
            const movement = { ...source, routeID: resolveStationPlanMovementRouteID(source.routeID, source.routeIDList) }
            const route = routeMap.get(movement.routeID)
            if (!route) continue
            const timings = timesForMovement(movement)
            const timingMap = new Map(timings.map(time => [time.cellID, time]))
            const overrides = readMovementCellOccupationOverrides(movement.cellOccupationOverridesJson)
            // Interrupt cells constrain capacity, but are not traversed by this train.
            const cellIDs = [...new Set(route.cellIDs.length ? route.cellIDs : timings.filter(time => !time.isInterruptCell).map(time => time.cellID))]
            const windows = cellIDs.map(cellID => {
                const shift = getMovementCellOccupationShifts(movement.cellOccupationOverridesJson, route.id, cellID, timingMap.get(cellID) || {})
                const start = movement.startMinutes + shift.startOccupationShift / 60
                return { cellID, start, end: Math.max(start, movement.endMinutes + shift.endOccupationShift / 60) }
            })
            const detail = [movement.name, route.name || route.id].filter(Boolean).join(' · ')
            const anchor = (edge: 'start' | 'end', cellID?: string): StationPlanTimeAnchor =>
                ({ movementID: movement.movementID, kind: cellID ? 'cell' : 'movement', cellID, edge })
            if (route.startNodeID) {
                const time = Math.min(movement.startMinutes, windows[0]?.start ?? movement.startMinutes)
                const sources = [anchor('start', windows[0] && windows[0].start < movement.startMinutes ? windows[0].cellID : undefined)]
                append(`node:${route.startNodeID}`, time, time, detail, movement.movementID, sources, sources)
            }
            windows.forEach((window, index) => {
                const next = windows[index + 1]
                const override = overrides[window.cellID]?.routeID === route.id ? overrides[window.cellID] : undefined
                const isAdjusted = override?.stationPlanArriveMinutes !== undefined || override?.stationPlanDepartMinutes !== undefined || override?.stationPlanCellID !== undefined
                append(`cell:${override?.stationPlanCellID || window.cellID}`, override?.stationPlanArriveMinutes ?? window.start,
                    override?.stationPlanDepartMinutes ?? (next ? Math.max(window.start, next.start) : window.end), detail,
                    movement.movementID, [anchor('start', window.cellID)], [anchor(next ? 'start' : 'end', next?.cellID || window.cellID)],
                    window.cellID, isAdjusted, override?.stationPlanDepartMinutes !== undefined)
            })
            if (route.endNodeID) {
                const time = Math.max(cursor, movement.endMinutes)
                const sources = [anchor('end'), ...(cursor >= movement.endMinutes ? cursorAnchors : [])]
                append(`node:${route.endNodeID}`, time, time, detail, movement.movementID, sources, sources)
            }
        }
        return { ...train, visits }
    }).filter(train => train.visits.length > 0)
}

export function stationPlanTimeLabel(minutes: number, hoursOnly = false) {
    const seconds = Math.round(minutes * 60)
    const day = Math.floor(seconds / 86400)
    const inDay = seconds - day * 86400
    const hours = Math.floor(inDay / 3600)
    const minute = Math.floor((inDay % 3600) / 60)
    const second = inDay % 60
    const prefix = day === 0 ? '' : `D${day > 0 ? '+' : ''}${day} `
    if (hoursOnly) return `${prefix}${hours}`
    return `${prefix}${String(hours).padStart(2, '0')}:${String(minute).padStart(2, '0')}${second ? `:${String(second).padStart(2, '0')}` : ''}`
}

export function stationPlanTimeGrid(start: number, end: number) {
    const ticks: { time: number; type: 'hour' | 'half-hour' | 'normal'; label: string }[] = []
    for (let time = Math.ceil(start / 10) * 10; time <= end; time += 10) {
        const type = time % 60 === 0 ? 'hour' : time % 30 === 0 ? 'half-hour' : 'normal'
        ticks.push({ time, type, label: type === 'hour' ? stationPlanTimeLabel(time, true) : '' })
    }
    return ticks
}

export function stationPlanDomain(trains: StationPlanTrain[], start: number | null, end: number | null) {
    let minimum = Infinity, maximum = -Infinity
    const include = (value: number | null) => {
        if (value === null || !Number.isFinite(value)) return
        minimum = Math.min(minimum, value)
        maximum = Math.max(maximum, value)
    }
    for (const train of trains) for (const visit of train.visits) {
        include(visit.arriveMinutes)
        include(visit.departMinutes)
    }
    include(start)
    include(end)
    if (minimum === Infinity) { minimum = 0; maximum = 60 }
    return { start: Math.floor(minimum / 60) * 60, end: Math.max(Math.floor(minimum / 60) * 60 + 60, Math.ceil(maximum / 60) * 60) }
}

export interface StationPlanPoint { x: number; y: number }
export interface StationPlanHorizontalSegment { x1: number; x2: number; y: number }
export interface StationPlanTerminal {
    kind: 'entry' | 'exit'
    from: StationPlanPoint
    to: StationPlanPoint
    arrowPath: string
    label: StationPlanPoint & { angle: number }
}

function stationPlanTerminalLeg(label: string) {
    const textWidth = Array.from(label).reduce((width, character) => width + (character.charCodeAt(0) > 255 ? 11 : 7), 0)
    return Math.max(36, Math.ceil(textWidth / Math.SQRT2) + 10)
}

/** Keep terminal lines and rotated labels inside the canvas, including at the time-domain edges. */
export function stationPlanChartPadding(trains: StationPlanTrain[]) {
    let padding = 56
    for (const train of trains) padding = Math.max(padding, stationPlanTerminalLeg(train.label) + 20)
    return { x: padding, y: padding }
}

/** The timed path uses M/H/V; diagonal terminal annotations do not represent occupation time. */
export function stationPlanTrainPath(train: StationPlanTrain, rows: StationPlanAxisRow[], start: number, pixelsPerMinute: number, rowPitch: number, padding = { x: 12, y: 16 }) {
    const rowIndexes = new Map(rows.map((row, index) => [row.key, index]))
    const visits = train.visits.filter(visit => rowIndexes.has(visit.rowKey))
    const round = (value: number) => Math.round(value * 1000) / 1000
    const x = (time: number) => round(padding.x + (time - start) * pixelsPerMinute)
    const y = (rowKey: string) => round(padding.y + ((rowIndexes.get(rowKey) ?? 0) + 0.5) * rowPitch)
    const horizontalSegments: StationPlanHorizontalSegment[] = []
    const segmentSources: { rowKey: string; visitIndexes: number[]; startMinutes: number; endMinutes: number; startAnchors: StationPlanTimeAnchor[]; endAnchors: StationPlanTimeAnchor[] }[] = []
    const appendHorizontal = (from: number, to: number, rowKey: string, owner: StationPlanVisit,
        startAnchors: StationPlanTimeAnchor[], endAnchors: StationPlanTimeAnchor[], keepPoint = false) => {
        if (from === to && !keepPoint) return
        const x1 = x(from), x2 = x(to), lineY = y(rowKey)
        const visitIndex = train.visits.indexOf(owner)
        const previous = horizontalSegments[horizontalSegments.length - 1]
        if (previous && previous.y === lineY && previous.x2 === x1) {
            previous.x2 = x2
            const source = segmentSources[segmentSources.length - 1]!
            if (!source.visitIndexes.includes(visitIndex)) source.visitIndexes.push(visitIndex)
            source.endMinutes = to
            source.endAnchors = endAnchors
        } else {
            horizontalSegments.push({ x1, x2, y: lineY })
            segmentSources.push({ rowKey, visitIndexes: [visitIndex], startMinutes: from, endMinutes: to, startAnchors, endAnchors })
        }
    }
    let path = ''
    let cursor = -Infinity
    let point: StationPlanPoint | null = null
    let previousVisit: StationPlanVisit | null = null
    const adjusted = train.visits.some(visit => visit.isAdjusted)
    let firstVerticalDirection = 0, lastVerticalDirection = 0
    for (const visit of visits) {
        const arrive = adjusted ? visit.arriveMinutes : Math.max(cursor, visit.arriveMinutes)
        const depart = adjusted ? visit.departMinutes : Math.max(arrive, visit.departMinutes)
        const arriveX = x(arrive), rowY = y(visit.rowKey)
        if (point) {
            if (previousVisit?.pinDeparture && previousVisit.rowKey !== visit.rowKey) {
                appendHorizontal(cursor, arrive, visit.rowKey, visit, previousVisit.departAnchors || [], visit.arriveAnchors || [])
            } else if (previousVisit) {
                appendHorizontal(cursor, arrive, previousVisit.rowKey, previousVisit, previousVisit.departAnchors || [],
                    [...(previousVisit.departAnchors || []), ...(visit.arriveAnchors || [])])
            }
            const direction = Math.sign(rowY - point.y)
            if (direction) {
                if (!firstVerticalDirection) firstVerticalDirection = direction
                lastVerticalDirection = direction
            }
        }
        path += path ? previousVisit?.pinDeparture && previousVisit.rowKey !== visit.rowKey
            ? ` V ${rowY} H ${arriveX}` : ` H ${arriveX} V ${rowY}` : `M ${arriveX} ${rowY}`
        path += ` H ${x(depart)}`
        appendHorizontal(arrive, depart, visit.rowKey, visit, visit.arriveAnchors || [], visit.departAnchors || [], visit.isAdjusted && visit.rowKey.startsWith('cell:'))
        point = { x: x(depart), y: rowY }
        cursor = depart
        previousVisit = visit
    }
    const first = visits[0] ? { x: x(visits[0].arriveMinutes), y: y(visits[0].rowKey) } : null
    const terminals: StationPlanTerminal[] = []
    const addTerminal = (kind: StationPlanTerminal['kind'], anchor: StationPlanPoint, direction: number) => {
        const leg = stationPlanTerminalLeg(train.label)
        const from = kind === 'entry' ? { x: anchor.x - leg, y: anchor.y - direction * leg } : anchor
        const to = kind === 'exit' ? { x: anchor.x + leg, y: anchor.y + direction * leg } : anchor
        const ux = 1 / Math.SQRT2, uy = direction / Math.SQRT2
        const wing1 = { x: round(to.x - 6 * ux + 3 * uy), y: round(to.y - 6 * uy - 3 * ux) }
        const wing2 = { x: round(to.x - 6 * ux - 3 * uy), y: round(to.y - 6 * uy + 3 * ux) }
        terminals.push({ kind, from, to, arrowPath: `M ${wing1.x} ${wing1.y} L ${to.x} ${to.y} L ${wing2.x} ${wing2.y}`,
            label: { x: round((from.x + to.x) / 2 + 7 * uy), y: round((from.y + to.y) / 2 - 7 * ux), angle: direction * 45 } })
    }
    // A filtered-out entrance must not turn an intermediate cell or route endpoint into a terminal.
    if (first && visits[0] === train.visits[0] && visits[0]?.rowKey.startsWith('node:')) addTerminal('entry', first, firstVerticalDirection || 1)
    if (point && visits[visits.length - 1] === train.visits[train.visits.length - 1] && visits[visits.length - 1]?.rowKey.startsWith('node:')) addTerminal('exit', point, lastVerticalDirection || 1)
    const editableSegments: StationPlanEditableSegment[] = horizontalSegments.flatMap((segment, index) => {
        const source = segmentSources[index]!
        return rows.find(row => row.key === source.rowKey)?.kind === 'cell'
            ? [{ ...segment, ...source, startAnchors: uniqueAnchors(source.startAnchors), endAnchors: uniqueAnchors(source.endAnchors) }] : []
    })
    return { path, first, horizontalSegments, editableSegments, terminals }
}

/** A drag changes one run; shared boundary timestamps keep its vertical connectors attached. */
export function previewStationPlanSegmentEdit(train: StationPlanTrain, edit: StationPlanSegmentEdit): StationPlanTrain {
    const members = new Set(edit.segment.visitIndexes)
    const first = edit.segment.visitIndexes[0] ?? -1
    const last = edit.segment.visitIndexes[edit.segment.visitIndexes.length - 1] ?? -1
    return { ...train, visits: train.visits.map((visit, index) => {
        if (edit.mode === 'row') return members.has(index) ? { ...visit, rowKey: edit.rowKey, isAdjusted: true } : visit
        const original = edit.mode === 'start' ? edit.segment.startMinutes : edit.segment.endMinutes
        const time = edit.mode === 'start' ? edit.startMinutes : edit.endMinutes
        const boundary = edit.mode === 'start' ? first : last
        const adjust = (value: number) => Math.abs(value - original) < 0.001 && Math.abs(index - boundary) <= 1 ? time : value
        let arriveMinutes = adjust(visit.arriveMinutes), departMinutes = adjust(visit.departMinutes)
        if (members.has(index)) {
            if (index === first) arriveMinutes = edit.startMinutes
            if (index === last) departMinutes = edit.endMinutes
        }
        return { ...visit, arriveMinutes, departMinutes, isAdjusted: members.has(index) || visit.isAdjusted, pinDeparture: index === last || visit.pinDeparture }
    }) }
}
