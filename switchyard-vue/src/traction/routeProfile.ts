import type { LayoutEntity, StationLayoutDocument } from '@switchyard/station-layout'

export interface TractionStationRoute {
    id: string
    startNodeID: string
    endNodeID: string
    nodeList?: string | string[]
    linkList: string | string[]
    cellList?: string | string[]
    signalList?: string | string[]
    switchList?: string | string[]
}
export interface ProfileIssue { code: string; entityId?: string; details?: string }
export interface RouteProfileSegment {
    id: string; linkId: string; startM: number; endM: number; lengthM: number
    speedLimitKmh?: number; gradientPermille: number
}
export interface RouteProfileDevice {
    id: string; label: string; type: 'switch' | 'curve' | 'signal' | 'insulationJoint'
    positionM: number; endM?: number
}
export interface RouteProfileJoint { id: string; label: string; positionM: number }
export interface RouteProfileCell {
    id: string; label: string; startM: number; endM: number
    /** False when the selected path starts/ends inside the actual circuit. */
    entryBoundaryKnown?: boolean; exitBoundaryKnown?: boolean
}
export interface RouteProfile {
    routeIds: string[]; lengthM: number; segments: RouteProfileSegment[]
    devices: RouteProfileDevice[]; joints: RouteProfileJoint[]; cells: RouteProfileCell[]
    warnings: ProfileIssue[]; errors: ProfileIssue[]
}
export interface RouteProfileOptions {
    /** Metres per CURRENT document coordinate unit, explicitly calibrated by the user. */
    metersPerUnit?: number
}
interface Point { x: number; y: number }
interface PathLink {
    track: LayoutEntity; id: string; from: string; to: string; reverse: boolean
    start: Point | null; end: Point | null; geometryLength: number; measuredLength?: number
    trimStart: number; trimEnd: number; arcStart: number; arcEnd: number
}
interface PathCurve { entity: LayoutEntity; index: number; arcLength: number; measured: boolean }
const key = (value: unknown): string => String(value ?? '').trim()
const finite = (value: unknown): number | undefined => {
    if (value === null || value === undefined || value === '') return undefined
    const number = Number(value)
    return Number.isFinite(number) ? number : undefined
}
const positive = (value: unknown): number | undefined => {
    const number = finite(value)
    return number !== undefined && number > 0 ? number : undefined
}
const record = (value: unknown): LayoutEntity => value && typeof value === 'object' ? value as LayoutEntity : {}
const point = (value: unknown): Point | null => {
    const object = record(value)
    const x = finite(object.x), y = finite(object.y)
    return x === undefined || y === undefined ? null : { x, y }
}
const distance = (a: Point, b: Point) => Math.hypot(a.x - b.x, a.y - b.y)
const label = (entity: LayoutEntity) => key(entity.name) || key(entity.id)

export function parseRouteIds(value: unknown): string[] {
    if (Array.isArray(value)) return value.map(key).filter(Boolean)
    const text = key(value)
    if (!text) return []
    try {
        const parsed: unknown = JSON.parse(text)
        if (Array.isArray(parsed)) return parsed.map(key).filter(Boolean)
    } catch { /* Route design also accepts delimited lists. */ }
    return text.split(/(?:\s*->\s*)|(?:\s*[,\n;]\s*)|\s+/).map(key).filter(Boolean)
}

/**
 * Unfold topology into physical chainage. Coordinates have no implied SI unit.
 * Manual scale refers to current document coordinates; metadata.coordinateUnit
 * refers to source coordinates before metadata.coordinateTransform. Measured
 * track.lengthM takes precedence. Never infer limits from switch types/radii.
 */
export function buildRouteProfile(
    document: StationLayoutDocument,
    routeOrRoutes: TractionStationRoute | readonly TractionStationRoute[],
    options: RouteProfileOptions = {},
): RouteProfile {
    const routes: readonly TractionStationRoute[] = Array.isArray(routeOrRoutes)
        ? routeOrRoutes : [routeOrRoutes as TractionStationRoute]
    const result: RouteProfile = {
        routeIds: routes.map(route => route.id), lengthM: 0,
        segments: [], devices: [], joints: [], cells: [], warnings: [], errors: [],
    }
    const issue = (severity: 'warnings' | 'errors', code: string, entityId?: string, details?: string) => {
        if (!result[severity].some(existing => existing.code === code && existing.entityId === entityId)) {
            result[severity].push({ code, ...(entityId ? { entityId } : {}), ...(details ? { details } : {}) })
        }
    }
    const indexEntities = (entities: LayoutEntity[] | undefined, collection: string) => {
        const map = new Map<string, LayoutEntity>()
        for (const entity of entities ?? []) {
            const id = key(entity.id)
            if (!id) continue
            if (map.has(id)) issue('errors', 'duplicateEntityId', id, collection)
            map.set(id, entity)
        }
        return map
    }
    const tracks = indexEntities(document.tracks, 'tracks')
    const nodes = indexEntities(document.nodes, 'nodes')
    const path: PathLink[] = []
    if (!routes.length) issue('errors', 'emptyRoute')
    for (let routeIndex = 0; routeIndex < routes.length; routeIndex++) {
        const route = routes[routeIndex]!
        const start = key(route.startNodeID), end = key(route.endNodeID)
        if (!start || !end || start === end) { issue('errors', 'invalidRouteEndpoints', route.id); continue }
        if (routeIndex > 0 && key(routes[routeIndex - 1]!.endNodeID) !== start) {
            issue('errors', 'disconnectedRoutes', route.id); continue
        }
        const ids = parseRouteIds(route.linkList)
        if (!ids.length) { issue('errors', 'emptyRouteLinks', route.id); continue }
        if (new Set(ids).size !== ids.length) { issue('errors', 'duplicateRouteLink', route.id); continue }
        const missing = ids.filter(id => !tracks.has(id))
        if (missing.length) { missing.forEach(id => issue('errors', 'missingRouteLink', id)); continue }
        let nodeIds = parseRouteIds(route.nodeList)
        if (nodeIds[0] === end && nodeIds[nodeIds.length - 1] === start) nodeIds = [...nodeIds].reverse()
        if (nodeIds.length && (nodeIds.length !== ids.length + 1 || nodeIds[0] !== start || nodeIds[nodeIds.length - 1] !== end)) {
            issue('errors', 'invalidRouteNodeOrder', route.id); continue
        }
        const remaining = new Set(ids)
        let current = start
        for (let offset = 0; offset < ids.length; offset++) {
            const nextExpected = nodeIds[offset + 1]
            const candidates = [...remaining].filter(id => {
                const track = tracks.get(id)!
                const from = key(track.fromNodeID), to = key(track.toNodeID)
                const next = from === current ? to : to === current ? from : undefined
                return next !== undefined && (!nextExpected || next === nextExpected)
            })
            if (candidates.length !== 1) {
                issue('errors', candidates.length ? 'ambiguousRoutePath' : 'disconnectedRoutePath', route.id); break
            }
            const id = candidates[0]!, track = tracks.get(id)!
            remaining.delete(id)
            const reverse = key(track.toNodeID) === current
            const next = key(reverse ? track.fromNodeID : track.toNodeID)
            if (!nodes.has(current) || !nodes.has(next)) issue('errors', 'missingRouteNode', !nodes.has(current) ? current : next)
            const fromPoint = point({ x: track.x1, y: track.y1 }) ?? point(nodes.get(key(track.fromNodeID)))
            const toPoint = point({ x: track.x2, y: track.y2 }) ?? point(nodes.get(key(track.toNodeID)))
            const geometryLength = fromPoint && toPoint ? distance(fromPoint, toPoint) : 0
            const measuredLength = positive(track.lengthM)
            if (track.lengthM !== undefined && measuredLength === undefined) issue('errors', 'invalidMeasuredLength', id)
            if (!measuredLength && !(geometryLength > 0)) issue('errors', 'invalidTrackGeometry', id)
            path.push({
                track, id, from: current, to: next, reverse,
                start: reverse ? toPoint : fromPoint, end: reverse ? fromPoint : toPoint,
                geometryLength, measuredLength, trimStart: 0, trimEnd: 0, arcStart: 0, arcEnd: 0,
            })
            current = next
        }
        if (remaining.size || current !== end) issue('errors', 'incompleteRoutePath', route.id)
    }
    for (let index = 1; index < path.length; index++) {
        const previous = path[index - 1]!, current = path[index]!
        if (previous.id === current.id && previous.from === current.to && previous.to === current.from) {
            issue('errors', 'reverseContinuation', current.id)
        }
    }
    if (result.errors.length) return result
    let scale: number | undefined
    if (options.metersPerUnit !== undefined) {
        scale = positive(options.metersPerUnit)
        if (!scale) issue('errors', 'invalidDistanceScale')
        else if (path.some(link => link.measuredLength === undefined)) issue('warnings', 'manualDistanceScale')
    } else {
        const metadata = document.metadata ?? {}
        scale = positive(metadata.metersPerUnit)
        if (!scale) {
            const units: Record<string, number> = { m: 1, metre: 1, meter: 1, metres: 1, meters: 1, mm: .001, cm: .01, km: 1000 }
            scale = units[key(metadata.coordinateUnit).toLowerCase()]
            if (scale) {
                const transform = record(metadata.coordinateTransform)
                if (transform.applied === true) {
                    const transformScale = positive(transform.scale)
                    if (!transformScale) issue('errors', 'invalidCoordinateTransform')
                    else scale /= transformScale
                }
            }
        }
    }
    if (!scale && path.some(link => link.measuredLength === undefined)) issue('errors', 'missingDistanceScale')
    if (result.errors.length) return result

    const curves: PathCurve[] = []
    for (let index = 0; index < path.length - 1; index++) {
        const before = path[index]!, after = path[index + 1]!
        const matches = (document.curves ?? []).filter(curve =>
            key(curve.nodeID) === before.to &&
            new Set([key(curve.tangentLinkID1), key(curve.tangentLinkID2)]).size === 2 &&
            [key(curve.tangentLinkID1), key(curve.tangentLinkID2)].includes(before.id) &&
            [key(curve.tangentLinkID1), key(curve.tangentLinkID2)].includes(after.id))
        if (matches.length > 1) { issue('errors', 'ambiguousCurve', before.to); continue }
        const entity = matches[0]
        if (!entity) continue
        if (before.measuredLength !== undefined || after.measuredLength !== undefined) {
            // Measured lengths already represent travelled distance; do not shorten twice.
            curves.push({ entity, index, arcLength: 0, measured: true })
            issue('warnings', 'curveUsesMeasuredLinks', key(entity.id)); continue
        }
        const a = point(entity.start), b = point(entity.end), center = point(entity.center)
        const radius = positive(entity.radius)
        if (!a || !b || !center || !radius || !before.start || !before.end || !after.start || !after.end) {
            issue('errors', 'invalidCurveGeometry', key(entity.id)); continue
        }
        const tolerance = Math.max(.01, radius * .001)
        const liesOn = (p: Point, segment: PathLink) =>
            Math.abs(distance(segment.start!, p) + distance(p, segment.end!) - segment.geometryLength) <= tolerance
        let entry: Point, exit: Point
        if (liesOn(a, before) && liesOn(b, after)) { entry = a; exit = b }
        else if (liesOn(b, before) && liesOn(a, after)) { entry = b; exit = a }
        else { issue('errors', 'invalidCurveGeometry', key(entity.id)); continue }
        if (Math.abs(distance(a, center) - radius) > tolerance || Math.abs(distance(b, center) - radius) > tolerance) {
            issue('errors', 'invalidCurveGeometry', key(entity.id)); continue
        }
        const ax = a.x - center.x, ay = a.y - center.y, bx = b.x - center.x, by = b.y - center.y
        let angle = Math.acos(Math.max(-1, Math.min(1, (ax * bx + ay * by) / (radius * radius))))
        if (Number(entity.largeArcFlag) === 1) angle = Math.PI * 2 - angle
        const arcLength = radius * angle
        if (!(arcLength > 0)) { issue('errors', 'invalidCurveGeometry', key(entity.id)); continue }
        before.trimEnd = distance(entry, before.end)
        after.trimStart = distance(after.start, exit)
        before.arcEnd = arcLength / 2
        after.arcStart = arcLength / 2
        curves.push({ entity, index, arcLength: arcLength * scale!, measured: false })
    }
    if (result.errors.length) return result

    const nodePositions = new Map<string, number[]>()
    const addNodePosition = (id: string, positionM: number) => {
        const positions = nodePositions.get(id) ?? []
        if (!positions.some(value => Math.abs(value - positionM) < 1e-6)) positions.push(positionM)
        nodePositions.set(id, positions)
    }
    for (let index = 0; index < path.length; index++) {
        const link = path[index]!
        const straight = link.geometryLength - link.trimStart - link.trimEnd
        if (straight < -1e-6) { issue('errors', 'overlappingCurves', link.id); continue }
        const lengthM = link.measuredLength ?? (Math.max(0, straight) + link.arcStart + link.arcEnd) * scale!
        if (!(lengthM > 0) || !Number.isFinite(lengthM)) { issue('errors', 'invalidMeasuredLength', link.id); continue }
        const speedLimitKmh = positive(link.track.speedLimitKmh)
        if (link.track.speedLimitKmh !== undefined && speedLimitKmh === undefined) issue('errors', 'invalidSpeedLimit', link.id)
        const gradient = finite(link.track.gradientPermille)
        if (link.track.gradientPermille !== undefined && gradient === undefined) issue('errors', 'invalidGradient', link.id)
        if (gradient === undefined) issue('warnings', 'assumedLevelTrack')
        if (speedLimitKmh === undefined) issue('warnings', 'missingSpeedLimits')
        const startM = result.lengthM, endM = startM + lengthM
        result.segments.push({
            id: index + ':' + link.id, linkId: link.id, startM, endM, lengthM,
            ...(speedLimitKmh === undefined ? {} : { speedLimitKmh }),
            gradientPermille: (gradient ?? 0) * (link.reverse ? -1 : 1),
        })
        addNodePosition(link.from, startM)
        addNodePosition(link.to, endM)
        result.lengthM = endM
    }
    if (result.errors.length) return { ...result, lengthM: 0, segments: [] }
    for (const curve of curves) {
        const positionM = result.segments[curve.index]!.endM
        result.devices.push({
            id: key(curve.entity.id), label: label(curve.entity), type: 'curve',
            positionM: positionM - curve.arcLength / 2,
            ...(curve.measured ? {} : { endM: positionM + curve.arcLength / 2 }),
        })
    }
    const expectedDevices = new Set(routes.flatMap(route => [...parseRouteIds(route.signalList), ...parseRouteIds(route.switchList)]))
    for (const [type, entities] of [
        ['switch', document.switches], ['signal', document.signals], ['insulationJoint', document.insulationJoints],
    ] as const) {
        for (const entity of entities ?? []) {
            const id = key(entity.id), nodeId = key(entity.bindingNodeID)
            const positions = nodePositions.get(nodeId)
            if (!positions) {
                if (expectedDevices.has(id) || !nodeId) issue('warnings', 'unlocatedDevice', id)
                continue
            }
            for (let index = 0; index < positions.length; index++) {
                const positionM = positions[index]!
                const occurrenceId = positions.length > 1 ? id + '@' + (index + 1) : id
                result.devices.push({ id: occurrenceId, label: label(entity), type, positionM })
                if (type === 'insulationJoint') result.joints.push({ id: occurrenceId, label: label(entity), positionM })
            }
        }
    }
    if (!result.joints.length) issue('warnings', 'missingInsulationJoints')
    result.devices.sort((a, b) => a.positionM - b.positionM || a.id.localeCompare(b.id))
    result.joints.sort((a, b) => a.positionM - b.positionM || a.id.localeCompare(b.id))

    // Only actual cell membership is used. Repeated visits become separate intervals.
    const covered = new Set<number>()
    const travelledLinks = new Set(path.map(link => link.id))
    for (const entity of document.cells ?? []) {
        const links = new Set(parseRouteIds(entity.linkIDList))
        const boundaryKnown = (nodeId: string) => ![...links].some(id => {
            if (travelledLinks.has(id)) return false
            const track = tracks.get(id)
            return track !== undefined && [key(track.fromNodeID), key(track.toNodeID)].includes(nodeId)
        })
        let active: RouteProfileCell | null = null
        let occurrence = 0
        for (let index = 0; index < result.segments.length; index++) {
            const segment = result.segments[index]!
            if (!links.has(segment.linkId)) { active = null; continue }
            if (covered.has(index)) issue('warnings', 'overlappingCells', key(entity.id))
            covered.add(index)
            if (active) {
                active.endM = segment.endM
                active.exitBoundaryKnown = boundaryKnown(path[index]!.to)
            }
            else {
                occurrence++
                active = {
                    id: key(entity.id) + '@' + occurrence, label: label(entity),
                    startM: segment.startM, endM: segment.endM,
                    entryBoundaryKnown: boundaryKnown(path[index]!.from),
                    exitBoundaryKnown: boundaryKnown(path[index]!.to),
                }
                result.cells.push(active)
            }
        }
    }
    for (const cell of result.cells) {
        if (cell.entryBoundaryKnown === false || cell.exitBoundaryKnown === false) {
            issue('warnings', 'partialCellCoverage', cell.id)
        }
    }
    if (covered.size !== result.segments.length) issue('warnings', 'missingCellCoverage')
    result.cells.sort((a, b) => a.startM - b.startM)
    return result
}
