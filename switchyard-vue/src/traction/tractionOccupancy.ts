import { headTimeAt, departureSectionCell, cellTerminalStopTime } from './tractionCalculation.ts'
import type { TractionResult, TractionCellOccupancy } from './tractionCalculation'
import { parseRouteIds } from './routeProfile.ts'
import type { RouteProfile, RouteProfileCell, TractionStationRoute } from './routeProfile'

/** Integer seconds relative to the movement's separate start and end events. */
export interface StationRouteTimeInput {
    cellID: string
    startOccupationShift: number | null
    endOccupationShift: number | null
}
export interface RouteOccupancyUpdate {
    routeID: string
    startTimeS: number
    endTimeS: number
    times: StationRouteTimeInput[]
    skippedCellIDs: string[]
}
const POSITION_EPSILON = 1e-6
const samePosition = (left: number, right: number) => Math.abs(left - right) <= POSITION_EPSILON
const finite = (value: unknown): value is number => typeof value === 'number' && Number.isFinite(value)
const validID = (value: unknown): value is string => typeof value === 'string' && value.trim().length > 0
function fail(message: string): never { throw new Error(message) }
const int32 = (value: unknown): value is number => finite(value) && Number.isInteger(value)
    && value >= -2147483648 && value <= 2147483647

/** Resolve first arrival or last departure without treating a station dwell as running time. */
function boundaryTime(result: TractionResult, positionM: number, departure: boolean): number {
    let low = 0, high = result.samples.length
    while (low < high) {
        const middle = low + Math.floor((high - low) / 2)
        if (result.samples[middle]!.positionM < positionM - POSITION_EPSILON) low = middle + 1
        else high = middle
    }
    const first = result.samples[low]
    if (first && samePosition(first.positionM, positionM)) {
        let index = low
        if (departure) {
            while (index + 1 < result.samples.length && samePosition(result.samples[index + 1]!.positionM, positionM)) index++
        }
        return result.samples[index]!.timeS
    }
    const time = headTimeAt(result, positionM)
    if (!finite(time)) return fail('Missing route boundary time.')
    return time
}

/** Stop still-occupied circuits at terminal arrival; keep all other unknown timing unknown. */
function reservationReleaseTime(result: TractionResult, occurrence: RouteProfileCell,
    occupancy: TractionCellOccupancy): number | undefined {
    const entryKnown = occurrence.entryBoundaryKnown !== false && occupancy.entryBoundaryKnown !== false
    const exitKnown = occurrence.exitBoundaryKnown !== false && occupancy.exitBoundaryKnown !== false
    const { entryTimeS, exitTimeS, releaseTimeS } = occupancy
    const last = result.samples[result.samples.length - 1]!
    if (entryKnown ? !finite(entryTimeS) || entryTimeS < 0 || entryTimeS > last.timeS : entryTimeS !== null) return undefined
    if (finite(exitTimeS) && finite(releaseTimeS)) {
        return exitKnown && exitTimeS >= (entryTimeS ?? 0) && releaseTimeS >= exitTimeS ? releaseTimeS : undefined
    }
    if (exitTimeS !== null || releaseTimeS !== null) return undefined
    return cellTerminalStopTime(result, occupancy) ?? undefined
}

/**
 * All routes reserve traversed cells from route setting (time zero). Arrival and
 * through runs reference first head entry into the route and first arrival at
 * its end; departure retains its origin-departure reference. Circuits still
 * occupied at the final stop end their reservation at first terminal arrival.
 * Saved physical clearance and release events remain unchanged.
 */
export function buildTractionOccupancyUpdates(
    routes: readonly TractionStationRoute[], profile: RouteProfile, result: TractionResult,
): RouteOccupancyUpdate[] {
    if (!Array.isArray(routes) || !routes.length || !profile || !result
        || !['arrival', 'departure', 'through', 'shunting'].includes(result.mode)
        || !Array.isArray(profile.routeIds) || profile.routeIds.length !== routes.length
        || !Array.isArray(profile.segments) || !Array.isArray(profile.cells)
        || !Array.isArray(profile.errors) || profile.errors.length
        || !finite(profile.lengthM) || profile.lengthM <= 0) fail('Invalid route profile scope.')
    const routeIDs = new Set<string>()
    for (let index = 0; index < routes.length; index++) {
        const route = routes[index]!
        if (!route || !validID(route.id) || route.id !== route.id.trim() || routeIDs.has(route.id)
            || profile.routeIds[index] !== route.id || !validID(route.startNodeID) || !validID(route.endNodeID)
            || route.startNodeID === route.endNodeID
            || (index > 0 && routes[index - 1]!.endNodeID !== route.startNodeID)) fail('Invalid route scope.')
        routeIDs.add(route.id)
    }
    const approach = result.approachDistanceM ?? 0
    if (!finite(approach) || approach < 0 || (approach > 0 && (result.mode === 'departure' || result.mode === 'shunting')))
        fail('Invalid braking approach.')
    const departureLength = result.departureSectionLengthM ?? 0
    if (!finite(departureLength) || departureLength < 0 || departureLength > 100000
        || (departureLength > 0 && result.mode !== 'departure' && result.mode !== 'through')) fail('Invalid departure section.')
    if (!finite(result.lengthM) || !samePosition(result.lengthM, profile.lengthM + approach + departureLength)
        || !finite(result.motionStartS) || result.motionStartS < 0
        || !Array.isArray(result.samples) || result.samples.length < 2
        || !Array.isArray(result.cellOccupancies)) fail('Invalid traction result scope.')
    for (let index = 0; index < result.samples.length; index++) {
        const sample = result.samples[index]!, previous = result.samples[index - 1]
        if (!sample || !finite(sample.positionM) || !finite(sample.timeS) || sample.timeS < 0
            || !finite(sample.speedKmh) || sample.speedKmh < 0
            || (previous && (sample.positionM < previous.positionM || sample.timeS < previous.timeS))) {
            fail('Invalid traction result samples.')
        }
    }
    const firstSample = result.samples[0]!, lastSample = result.samples[result.samples.length - 1]!
    if (!samePosition(firstSample.positionM, 0) || !samePosition(lastSample.positionM, result.lengthM)
        || Math.abs(firstSample.timeS - result.motionStartS) > 1e-6) fail('Missing route boundary time.')

    let segmentIndex = 0, previousEnd = 0
    const ranges = routes.map(route => {
        const links = parseRouteIds(route.linkList)
        const linkSet = new Set(links)
        const selected = profile.segments.slice(segmentIndex, segmentIndex + links.length)
        if (!links.length || linkSet.size !== links.length || selected.length !== links.length
            || new Set(selected.map(segment => segment.linkId)).size !== links.length
            || selected.some(segment => !linkSet.has(segment.linkId))) fail('Invalid route segment scope.')
        for (const segment of selected) {
            if (!finite(segment.startM) || !finite(segment.endM) || segment.endM <= segment.startM
                || !samePosition(segment.startM, previousEnd)) fail('Invalid route segment boundaries.')
            previousEnd = segment.endM
        }
        segmentIndex += links.length
        return { startM: selected[0]!.startM, endM: selected[selected.length - 1]!.endM }
    })
    if (segmentIndex !== profile.segments.length || !samePosition(previousEnd, profile.lengthM)) {
        fail('Invalid route segment scope.')
    }

    const sourceCellIDs = new Set(routes.flatMap(route => parseRouteIds(route.cellList)))
    const profileByID = new Map<string, RouteProfileCell>()
    const occurrencesBySource = new Map<string, RouteProfileCell[]>()
    for (const cell of profile.cells) {
        if (!cell || !validID(cell.id) || profileByID.has(cell.id)
            || !finite(cell.startM) || !finite(cell.endM) || cell.startM < 0
            || cell.endM <= cell.startM || cell.endM > profile.lengthM + POSITION_EPSILON) fail('Invalid profile cell boundaries.')
        profileByID.set(cell.id, cell)
        const delimiter = cell.id.lastIndexOf('@')
        const sourceID = cell.id.slice(0, delimiter)
        // The source must be a known CellList member; labels and arbitrary suffixes are not IDs.
        if (delimiter < 1 || !/^\d+$/.test(cell.id.slice(delimiter + 1)) || !sourceCellIDs.has(sourceID)) continue
        const occurrences = occurrencesBySource.get(sourceID) ?? []
        occurrences.push(cell)
        occurrencesBySource.set(sourceID, occurrences)
    }
    const occupancyByID = new Map<string, TractionCellOccupancy>()
    if (departureLength > 0) {
        const cell = departureSectionCell(profile, departureLength)
        // Calculation-only circuit has no station CellID to write into the parameter set.
        profileByID.set(cell.id, cell)
    }
    for (const cell of result.cellOccupancies) {
        const expected = cell && profileByID.get(cell.id)
        if (!expected || occupancyByID.has(cell.id) || !finite(cell.startM) || !finite(cell.endM)
            || !samePosition(cell.startM, expected.startM + approach) || !samePosition(cell.endM, expected.endM + approach)) {
            fail('Invalid traction cell scope.')
        }
        occupancyByID.set(cell.id, cell)
    }

    return routes.map((route, index) => {
        const range = ranges[index]!
        const arrivalReference = result.mode !== 'departure'
        const startTimeS = arrivalReference ? boundaryTime(result, range.startM + approach, false)
            : index === 0 && approach === 0 ? result.motionStartS : boundaryTime(result, range.startM + approach, true)
        const endTimeS = boundaryTime(result, range.endM + approach, false)
        if (!finite(startTimeS) || !finite(endTimeS) || startTimeS > endTimeS) fail('Invalid route boundary time.')
        const update: RouteOccupancyUpdate = { routeID: route.id, startTimeS, endTimeS, times: [], skippedCellIDs: [] }
        for (const cellID of new Set(parseRouteIds(route.cellList))) {
            const occurrences = (occurrencesBySource.get(cellID) ?? []).filter(cell =>
                cell.startM < range.endM - POSITION_EPSILON && cell.endM > range.startM + POSITION_EPSILON)
            let release = -Infinity, complete = occurrences.length > 0
            for (const occurrence of occurrences) {
                const occupancy = occupancyByID.get(occurrence.id)
                const end = occupancy ? reservationReleaseTime(result, occurrence, occupancy) : undefined
                if (end === undefined) complete = false
                else release = Math.max(release, end)
            }
            const startOccupationShift = Math.floor(0 - startTimeS)
            const endOccupationShift = Math.ceil(release - endTimeS)
            if (!complete || !int32(startOccupationShift) || !int32(endOccupationShift)) {
                update.skippedCellIDs.push(cellID)
                continue
            }
            update.times.push({ cellID, startOccupationShift, endOccupationShift })
        }
        return update
    })
}

type JsonRow = Record<string, unknown>
function field(row: JsonRow, camel: string, pascal: string): unknown {
    const hasCamel = Object.prototype.hasOwnProperty.call(row, camel)
    const hasPascal = Object.prototype.hasOwnProperty.call(row, pascal)
    if (hasCamel && hasPascal && row[camel] !== row[pascal]) fail('Conflicting stored occupancy fields.')
    return hasCamel ? row[camel] : row[pascal]
}
function readTimes(value: unknown, routeID: string, allowNull: boolean): StationRouteTimeInput[] {
    if (!Array.isArray(value)) return fail('Invalid stored occupancy response.')
    const ids = new Set<string>()
    return value.map(item => {
        if (!item || typeof item !== 'object' || Array.isArray(item)) fail('Invalid occupancy row.')
        const row = item as JsonRow
        const id = field(row, 'cellID', 'CellID')
        const storedRoute = field(row, 'routeID', 'RouteID')
        const trainType = field(row, 'trainTypeID', 'TrainTypeID')
        const start = field(row, 'startOccupationShift', 'StartOccupationShift')
        const end = field(row, 'endOccupationShift', 'EndOccupationShift')
        if (!validID(id) || id !== id.trim() || ids.has(id)
            || (storedRoute !== undefined && storedRoute !== null && storedRoute !== '' && storedRoute !== routeID)
            || (trainType !== undefined && trainType !== null && trainType !== '')
            || !(int32(start) || (allowNull && start === null))
            || !(int32(end) || (allowNull && end === null))) fail('Invalid stored occupancy values or scope.')
        ids.add(id)
        return { cellID: id, startOccupationShift: start as number | null, endOccupationShift: end as number | null }
    })
}

/** Merge before the replacement PUT: absent/unknown/indirect cells keep their existing rows. */
export function mergeTractionOccupancyTimes(existing: unknown, update: RouteOccupancyUpdate): StationRouteTimeInput[] {
    if (!update || !validID(update.routeID) || !Array.isArray(update.skippedCellIDs)) fail('Invalid occupancy update scope.')
    const current = readTimes(existing, update.routeID, true)
    const updated = readTimes(update.times, update.routeID, false)
    const skipped = new Set(update.skippedCellIDs)
    if (updated.some(row => skipped.has(row.cellID))) fail('An unknown cell cannot be updated.')
    const replacement = new Map(updated.map(row => [row.cellID, row]))
    const merged = current.map(row => {
        const value = replacement.get(row.cellID)
        replacement.delete(row.cellID)
        return value ?? row
    })
    return [...merged, ...replacement.values()]
}



