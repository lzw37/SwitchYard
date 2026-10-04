import type { MovementCellOccupation } from '../movementCellOccupation.ts'

export interface StationPlanAxisRow {
    key: string
    sourceID: string
    label: string
    kind: 'node' | 'track'
    isTrackNode?: boolean
    nodeIDs?: string[]
    children?: StationPlanAxisRow[]
}

export interface StationPlanTrack {
    id: string
    name: string
    fromNodeID: string
    toNodeID: string
}

export type StationPlanLineMode = 'straight' | 'orthogonal'

/** Collapse disjoint named Link endpoints while keeping each real node available for expansion. */
export function buildStationPlanAxisGroups(
    nodes: StationPlanAxisRow[], tracks: StationPlanTrack[], orderedNodeIDs: string[] | null = null,
): StationPlanAxisRow[] {
    const nodeMap = new Map<string, StationPlanAxisRow>()
    for (const node of nodes) {
        const id = node.sourceID.trim()
        if (node.kind === 'node' && id && !nodeMap.has(id)) nodeMap.set(id, node)
    }
    const candidates = tracks.map(track => ({ ...track, id: track.id.trim(),
        fromNodeID: track.fromNodeID.trim(), toNodeID: track.toNodeID.trim() }))
        .filter(track => track.id && track.name.trim() && track.fromNodeID !== track.toNodeID &&
            nodeMap.has(track.fromNodeID) && nodeMap.has(track.toNodeID))
        .sort((a, b) => {
            for (const field of ['id', 'fromNodeID', 'toNodeID', 'name'] as const) {
                if (a[field] !== b[field]) return a[field] < b[field] ? -1 : 1
            }
            return 0
        })
    const groupsByNode = new Map<string, StationPlanAxisRow>()
    const usedTrackIDs = new Set<string>()
    for (const track of candidates) {
        // Endpoint identity survives expansion and also applies to Links omitted because they overlap another pair.
        for (const id of [track.fromNodeID, track.toNodeID]) nodeMap.set(id, { ...nodeMap.get(id)!, isTrackNode: true })
    }
    for (const track of candidates) {
        // Shared endpoints belong to the first Link by ID; overlapping Links never form a transitive group.
        if (usedTrackIDs.has(track.id) || groupsByNode.has(track.fromNodeID) || groupsByNode.has(track.toNodeID)) continue
        const nodeIDs = [track.fromNodeID, track.toNodeID]
        const group: StationPlanAxisRow = { key: `track:${track.id}`, sourceID: track.id,
            label: `${track.name}（${track.fromNodeID},${track.toNodeID}）`, kind: 'track', nodeIDs,
            children: nodeIDs.map(id => nodeMap.get(id)!) }
        usedTrackIDs.add(track.id)
        nodeIDs.forEach(id => groupsByNode.set(id, group))
    }
    const groups: StationPlanAxisRow[] = []
    const emitted = new Set<string>()
    nodeMap.forEach((node, id) => {
        const row = groupsByNode.get(id) || node
        if (!emitted.has(row.key)) { groups.push(row); emitted.add(row.key) }
    })
    const order = new Map<string, number>()
    orderedNodeIDs?.forEach((id, index) => {
        const nodeID = id.trim()
        if (nodeID && !order.has(nodeID)) order.set(nodeID, index)
    })
    return groups.map((row, index) => ({ row, index,
        rank: Math.min(...(row.nodeIDs || [row.sourceID]).map(id => order.get(id) ?? Infinity)) }))
        .sort((a, b) => a.rank === b.rank ? a.index - b.index : a.rank - b.rank)
        .map(item => item.row)
}

export interface StationPlanVisit {
    rowKey: string
    arriveMinutes: number
    departMinutes: number
    detail: string
    movementID?: string
    arriveAnchors?: StationPlanTimeAnchor[]
    departAnchors?: StationPlanTimeAnchor[]
}

export interface StationPlanTimeAnchor {
    movementID: string
    kind: 'movement'
    edge: 'start' | 'end'
}

export interface StationPlanEditableSegment {
    movementID: string
    detail: string
    startRowKey: string
    endRowKey: string
    rowKey: string
    visitIndexes: number[]
    startMinutes: number
    endMinutes: number
    startAnchors: StationPlanTimeAnchor[]
    endAnchors: StationPlanTimeAnchor[]
    x1: number
    x2: number
    y1: number
    y2: number
    y: number
}

function stationPlanAnchorKey(anchor: StationPlanTimeAnchor) {
    return JSON.stringify([anchor.kind, anchor.movementID, anchor.edge])
}

/** Capture coincident endpoints from one train's geometry before a drag or keyboard edit begins. */
export function stationPlanCoincidentAnchors(
    segments: StationPlanEditableSegment[], segment: StationPlanEditableSegment, edge: 'start' | 'end',
): StationPlanTimeAnchor[] {
    const time = edge === 'start' ? segment.startMinutes : segment.endMinutes
    const pointX = edge === 'start' ? segment.x1 : segment.x2
    const pointY = edge === 'start' ? segment.y1 : segment.y2
    const anchors = new Map<string, StationPlanTimeAnchor>()
    const add = (values: StationPlanTimeAnchor[]) => values.forEach(anchor => anchors.set(stationPlanAnchorKey(anchor), { ...anchor }))
    add(edge === 'start' ? segment.startAnchors : segment.endAnchors)
    for (const candidate of segments) {
        if (candidate.startMinutes === time && candidate.x1 === pointX && candidate.y1 === pointY) add(candidate.startAnchors)
        if (candidate.endMinutes === time && candidate.x2 === pointX && candidate.y2 === pointY) add(candidate.endAnchors)
    }
    return [...anchors.values()]
}

export interface StationPlanSegmentEdit {
    trainID: string
    segment: StationPlanEditableSegment
    mode: 'start' | 'end'
    shiftFollowing?: boolean
    coincidentAnchors?: StationPlanTimeAnchor[]
    startMinutes: number
    endMinutes: number
    rowKey: string
}

export interface StationPlanTrackSegment extends StationPlanEditableSegment {
    isConnector?: boolean
}

export interface StationPlanTrackEdit {
    trainID: string
    segment: StationPlanTrackSegment
    sourceTrackID: string
    targetTrackID: string
}

/** Find this stay's consecutive source-track endpoints without reaching a later visit to the same track. */
export function stationPlanTrackVisitIndexes(train: StationPlanTrain, segment: StationPlanTrackSegment, track: StationPlanTrack): number[] {
    if (!track.id.trim() || !track.name.trim() || !track.fromNodeID.trim() || !track.toNodeID.trim() || track.fromNodeID.trim() === track.toNodeID.trim()) return []
    const order = new Map((train.movementOrder || [...new Set(train.visits.map(visit => visit.movementID || ''))]).map((id, index) => [id, index]))
    const adjacent = (leftIndex: number, rightIndex: number) => {
        const left = train.visits[leftIndex]!.movementID, right = train.visits[rightIndex]!.movementID
        if (!left || !right) return false
        if (left === right) return true
        const leftOrder = order.get(left), rightOrder = order.get(right)
        return leftOrder !== undefined && rightOrder === leftOrder + 1
    }
    let first: number, last: number
    if (segment.isConnector) {
        if (segment.visitIndexes.length !== 2) return []
        first = segment.visitIndexes[0]!
        last = segment.visitIndexes[1]!
        if (!Number.isInteger(first) || first < 0 || first % 2 !== 1 || last !== first + 1 || !train.visits[first] || !train.visits[last]) return []
        const previous = train.visits[first]!, next = train.visits[last]!
        if (!previous.movementID || previous.movementID !== segment.movementID || !next.movementID ||
            previous.movementID === next.movementID || !adjacent(first, last)) return []
        for (const [index, edge, anchors, rowKey, time] of [
            [first, 'end', segment.startAnchors, segment.startRowKey, segment.startMinutes],
            [last, 'start', segment.endAnchors, segment.endRowKey, segment.endMinutes],
        ] as const) {
            const visit = train.visits[index]!
            const matches = (anchor: StationPlanTimeAnchor | undefined) => anchor?.kind === 'movement' && anchor.movementID === visit.movementID && anchor.edge === edge
            if (anchors.length !== 1 || !matches(anchors[0]) || visit.arriveAnchors?.length !== 1 || !matches(visit.arriveAnchors[0]) ||
                visit.departAnchors?.length !== 1 || !matches(visit.departAnchors[0]) || visit.rowKey !== rowKey ||
                !Number.isFinite(time) || visit.arriveMinutes !== time || visit.departMinutes !== time) return []
        }
    } else {
        const context = getStationPlanSegmentEditContext(train, { trainID: train.id, segment, mode: 'start', shiftFollowing: false,
            startMinutes: segment.startMinutes, endMinutes: segment.endMinutes, rowKey: segment.rowKey })
        if (!context) return []
        first = context.startVisitIndex
        last = first + 1
    }
    const trackRows = new Set([`node:${track.fromNodeID.trim()}`, `node:${track.toNodeID.trim()}`])
    if (!trackRows.has(segment.startRowKey) || !trackRows.has(segment.endRowKey)) return []
    while (first > 0 && trackRows.has(train.visits[first - 1]!.rowKey) && adjacent(first - 1, first)) first--
    while (last + 1 < train.visits.length && trackRows.has(train.visits[last + 1]!.rowKey) && adjacent(last, last + 1)) last++
    return Array.from({ length: last - first + 1 }, (_, index) => first + index)
}

export interface StationPlanTrain {
    id: string
    label: string
    color: string
    visits: StationPlanVisit[]
    movementOrder?: string[]
}

export interface StationPlanMovement {
    trainID: string
    movementID: string
    name: string
    routeID: string
    routeIDList?: string
    startNodeID?: string
    endNodeID?: string
    startMinutes: number
    endMinutes: number
    sortOrder: number | null
    cellOccupationOverridesJson?: string | null
    cellOccupations?: MovementCellOccupation[]
}

export interface StationPlanRoute {
    id: string
    name: string
    cellIDs: string[]
    linkIDs?: string[]
    startNodeID: string
    endNodeID: string
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

function sortStationPlanMovementSequence<T extends StationPlanMovement>(movements: T[], trainID: string): T[] {
    return movements.map((movement, index) => ({ movement, index }))
        .filter(({ movement }) => movement.trainID === trainID)
        .sort((a, b) => (a.movement.sortOrder ?? a.index) - (b.movement.sortOrder ?? b.index) || a.index - b.index)
        .map(item => item.movement)
}

/** Use saved movement order and preserve input order for ties or missing sort values. */
export function orderStationPlanMovements<T extends StationPlanMovement>(movements: T[], trainID: string): T[] {
    return sortStationPlanMovementSequence(movements, trainID)
        .filter(movement => Number.isFinite(movement.startMinutes) && Number.isFinite(movement.endMinutes))
}

/** Each movement contributes its own start and end node, in operation order. */
export function buildStationPlanTrains(
    trains: { id: string; label: string; color: string; movementOrder?: string[] }[],
    movements: StationPlanMovement[],
    routes: StationPlanRoute[],
): StationPlanTrain[] {
    const routeMap = new Map(routes.map(route => [route.id, route]))
    return trains.map(train => {
        const visits: StationPlanVisit[] = []
        const ordered = sortStationPlanMovementSequence(movements, train.id)
        for (const source of ordered) {
            if (!Number.isFinite(source.startMinutes) || !Number.isFinite(source.endMinutes)) continue
            const movement = { ...source, routeID: resolveStationPlanMovementRouteID(source.routeID, source.routeIDList) }
            const route = routeMap.get(movement.routeID)
            const startNodeID = route?.startNodeID.trim() || movement.startNodeID?.trim()
            const endNodeID = route?.endNodeID.trim() || movement.endNodeID?.trim()
            if (!startNodeID || !endNodeID) continue
            const detail = [movement.name, route?.name || movement.routeID].filter(Boolean).join(' · ')
            for (const edge of ['start', 'end'] as const) {
                const time = edge === 'start' ? movement.startMinutes : movement.endMinutes
                const anchors: StationPlanTimeAnchor[] = [{ movementID: movement.movementID, kind: 'movement', edge }]
                visits.push({ rowKey: `node:${edge === 'start' ? startNodeID : endNodeID}`,
                    arriveMinutes: time, departMinutes: time, detail, movementID: movement.movementID,
                    arriveAnchors: anchors, departAnchors: anchors })
            }
        }
        return { ...train, movementOrder: [...(train.movementOrder || ordered.map(movement => movement.movementID))], visits }
    }).filter(train => train.visits.length > 0)
}

export function stationPlanTimeLabel(minutes: number, hoursOnly = false) {
    const seconds = Number((minutes * 60).toFixed(9))
    let wholeSeconds = Math.floor(seconds)
    let fraction = Math.round((seconds - wholeSeconds) * 1e9)
    if (fraction === 1e9) { wholeSeconds += 1; fraction = 0 }
    const day = Math.floor(wholeSeconds / 86400)
    const inDay = wholeSeconds - day * 86400
    const hours = Math.floor(inDay / 3600)
    const minute = Math.floor((inDay % 3600) / 60)
    const second = inDay % 60
    const secondText = String(second).padStart(2, '0') + (fraction ? `.${String(fraction).padStart(9, '0').replace(/0+$/, '')}` : '')
    const prefix = day === 0 ? '' : `D${day > 0 ? '+' : ''}${day} `
    if (hoursOnly) return `${prefix}${hours}`
    return `${prefix}${String(hours).padStart(2, '0')}:${String(minute).padStart(2, '0')}${second || fraction ? `:${secondText}` : ''}`
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

/** Draw each movement between its node/time endpoints, without merging adjacent movements. */
export function stationPlanTrainPath(train: StationPlanTrain, rows: StationPlanAxisRow[], start: number, pixelsPerMinute: number, rowPitch: number, padding = { x: 12, y: 16 }, mode: StationPlanLineMode = 'straight') {
    const rowIndexes = new Map(rows.map((row, index) => [row.key, index]))
    const trackNodeKeys = new Set(rows.filter(row => row.kind === 'node' && row.isTrackNode).map(row => row.key))
    rows.forEach((row, index) => {
        if (row.kind !== 'track') return
        row.nodeIDs?.forEach(id => {
            const key = `node:${id}`
            trackNodeKeys.add(key)
            if (!rowIndexes.has(key)) rowIndexes.set(key, index)
        })
    })
    const round = (value: number) => Math.round(value * 1000) / 1000
    const x = (time: number) => round(padding.x + (time - start) * pixelsPerMinute)
    const y = (rowKey: string) => round(padding.y + ((rowIndexes.get(rowKey) ?? 0) + 0.5) * rowPitch)
    const horizontalSegments: StationPlanHorizontalSegment[] = []
    const editableSegments: StationPlanEditableSegment[] = []
    let path = ''
    let connectorPath = ''
    let point: StationPlanPoint | null = null
    let firstVerticalDirection = 0, lastVerticalDirection = 0
    const includeDirection = (fromY: number, toY: number) => {
        const direction = Math.sign(toY - fromY)
        if (direction) {
            if (!firstVerticalDirection) firstVerticalDirection = direction
            lastVerticalDirection = direction
        }
    }
    for (let index = 0; index < train.visits.length; index += 2) {
        const from = train.visits[index], to = train.visits[index + 1]
        const startAnchor = from?.arriveAnchors?.find(anchor => anchor.kind === 'movement' && anchor.edge === 'start')
        const endAnchor = to?.arriveAnchors?.find(anchor => anchor.kind === 'movement' && anchor.edge === 'end')
        if (!from || !to || !startAnchor || !endAnchor || startAnchor.movementID !== endAnchor.movementID ||
            !rowIndexes.has(from.rowKey) || !rowIndexes.has(to.rowKey)) {
            // Hidden endpoints must not create a new connection across omitted movements.
            point = null
            continue
        }
        const x1 = x(from.arriveMinutes), y1 = y(from.rowKey), x2 = x(to.arriveMinutes), y2 = y(to.rowKey)
        if (point && (point.x !== x1 || point.y !== y1)) {
            connectorPath += `M ${point.x} ${point.y} L ${x1} ${y1} `
            includeDirection(point.y, y1)
        }
        const startIsTrack = trackNodeKeys.has(from.rowKey), endIsTrack = trackNodeKeys.has(to.rowKey)
        const orthogonal = mode === 'orthogonal' && !(startIsTrack && endIsTrack) && x1 !== x2 && y1 !== y2
        if (orthogonal) {
            // Put the horizontal leg on the non-track endpoint, preferring the start when both qualify.
            const horizontalY = startIsTrack ? y2 : y1
            path += startIsTrack
                ? `M ${x1} ${y1} L ${x1} ${y2} L ${x2} ${y2} `
                : `M ${x1} ${y1} L ${x2} ${y1} L ${x2} ${y2} `
            horizontalSegments.push({ x1, x2, y: horizontalY })
        } else path += `M ${x1} ${y1} L ${x2} ${y2} `
        includeDirection(y1, y2)
        editableSegments.push({ movementID: startAnchor.movementID, detail: from.detail,
            startRowKey: from.rowKey, endRowKey: to.rowKey, rowKey: from.rowKey, visitIndexes: [index, index + 1],
            startMinutes: from.arriveMinutes, endMinutes: to.arriveMinutes, startAnchors: [startAnchor], endAnchors: [endAnchor],
            x1, x2, y1, y2, y: y1 })
        if (y1 === y2) horizontalSegments.push({ x1, x2, y: y1 })
        point = { x: x2, y: y2 }
    }
    const firstVisit = train.visits[0], lastVisit = train.visits[train.visits.length - 1]
    const first = firstVisit && rowIndexes.has(firstVisit.rowKey) ? { x: x(firstVisit.arriveMinutes), y: y(firstVisit.rowKey) } : null
    const last = lastVisit && rowIndexes.has(lastVisit.rowKey) ? { x: x(lastVisit.arriveMinutes), y: y(lastVisit.rowKey) } : null
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
    // Only the train's original first/last node receives a terminal, even when rows are hidden.
    if (first && firstVisit?.rowKey.startsWith('node:')) addTerminal('entry', first, firstVerticalDirection || 1)
    if (last && lastVisit?.rowKey.startsWith('node:')) addTerminal('exit', last, lastVerticalDirection || 1)
    return { path, connectorPath, first, horizontalSegments, editableSegments, terminals }
}

/** Validate the captured endpoints and share one time-shift rule between preview and persistence. */
export function getStationPlanSegmentEditContext(train: StationPlanTrain, edit: StationPlanSegmentEdit) {
    if (train.id !== edit.trainID || (edit.mode !== 'start' && edit.mode !== 'end')) return null
    const { segment } = edit
    if (segment.visitIndexes.length !== 2) return null
    const startVisitIndex = segment.visitIndexes[0]!, endVisitIndex = segment.visitIndexes[1]!
    if (!Number.isInteger(startVisitIndex) || startVisitIndex < 0 || startVisitIndex % 2 !== 0 || endVisitIndex !== startVisitIndex + 1) return null
    for (const edge of ['start', 'end'] as const) {
        const anchors = edge === 'start' ? segment.startAnchors : segment.endAnchors
        const visit = train.visits[edge === 'start' ? startVisitIndex : endVisitIndex]
        const originalTime = edge === 'start' ? segment.startMinutes : segment.endMinutes
        const expectedRowKey = edge === 'start' ? segment.startRowKey : segment.endRowKey
        const matches = (anchor: StationPlanTimeAnchor | undefined) => anchor?.kind === 'movement' &&
            anchor.movementID === segment.movementID && anchor.edge === edge
        if (anchors.length !== 1 || !matches(anchors[0]) || !visit || visit.movementID !== segment.movementID ||
            visit.arriveAnchors?.length !== 1 || !matches(visit.arriveAnchors[0]) ||
            visit.departAnchors?.length !== 1 || !matches(visit.departAnchors[0]) || visit.rowKey !== expectedRowKey ||
            !Number.isFinite(originalTime) || visit.arriveMinutes !== originalTime || visit.departMinutes !== originalTime) return null
    }
    const originalMinutes = edit.mode === 'start' ? segment.startMinutes : segment.endMinutes
    const targetMinutes = edit.mode === 'start' ? edit.startMinutes : edit.endMinutes
    const delta = targetMinutes - originalMinutes
    if (!Number.isFinite(targetMinutes) || !Number.isFinite(delta)) return null
    const coincident = new Map<string, StationPlanTimeAnchor>()
    if (edit.shiftFollowing !== false && edit.coincidentAnchors?.length) {
        const visitsByAnchor = new Map<string, StationPlanVisit>()
        for (const visit of train.visits) {
            const anchor = visit.arriveAnchors?.[0]
            if (anchor) visitsByAnchor.set(stationPlanAnchorKey(anchor), visit)
        }
        for (const anchor of edit.coincidentAnchors) {
            if (!anchor || anchor.kind !== 'movement' || !anchor.movementID || (anchor.edge !== 'start' && anchor.edge !== 'end')) return null
            const key = stationPlanAnchorKey(anchor), visit = visitsByAnchor.get(key)
            if (!visit || visit.movementID !== anchor.movementID || visit.arriveAnchors?.length !== 1 ||
                visit.departAnchors?.length !== 1 || stationPlanAnchorKey(visit.departAnchors[0]!) !== key ||
                visit.arriveMinutes !== originalMinutes || visit.departMinutes !== originalMinutes) return null
            coincident.set(key, { ...anchor })
        }
    }
    return { movementID: segment.movementID, startVisitIndex, delta, originalMinutes, coincidentAnchors: [...coincident.values()],
        shiftTime(time: number, edge: 'start' | 'end', relativeMovementOrder: number, movementID?: string) {
            if (movementID && coincident.has(stationPlanAnchorKey({ kind: 'movement', movementID, edge }))) return targetMinutes
            if (relativeMovementOrder === 0 && edge === edit.mode) return targetMinutes
            if (edit.shiftFollowing !== false && (relativeMovementOrder > 0 || (relativeMovementOrder === 0 && edit.mode === 'start')))
                return time + delta
            return time
        } }
}

/** Shift the selected point and following movement points; Ctrl edits only the selected endpoint. */
export function previewStationPlanSegmentEdit(train: StationPlanTrain, edit: StationPlanSegmentEdit): StationPlanTrain {
    const context = getStationPlanSegmentEditContext(train, edit)
    if (!context) return train
    return { ...train, visits: train.visits.map((visit, index) => {
        const anchor = visit.arriveAnchors?.[0]
        if (!anchor) return visit
        const time = context.shiftTime(visit.arriveMinutes, anchor.edge, Math.floor(index / 2) - context.startVisitIndex / 2, anchor.movementID)
        return time !== visit.arriveMinutes ? { ...visit, arriveMinutes: time, departMinutes: time } : visit
    }) }
}
