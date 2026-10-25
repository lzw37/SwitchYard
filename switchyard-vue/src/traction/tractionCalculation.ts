/**
 * Traction API models, saved-result summaries and chart interpolation only.
 * Units: m, s, t, kN, km/h. Davis A+Bv+Cv² uses km/h.
 * The speed solver and occupancy calculations run in SwitchYard.SpeedProfile on the server.
 */
export interface ForcePoint { speedKmh: number; forceKn: number }
export interface TrainParameters {
    lengthM: number; massT: number; maxSpeedKmh: number
    /** Stored/API inertial-mass multiplier 1 + gamma; the UI edits gamma itself. */
    rotatingMassFactor: number
    tractionCurve: ForcePoint[]; brakingCurve: ForcePoint[]; resistanceCurve: ForcePoint[]; maxBrakeForceKn: number
    resistanceA: number; resistanceB: number; resistanceC: number
}
/** Convert the UI coefficient gamma and the existing stored multiplier without decimal noise. */
export const rotatingMassCoefficientFromFactor = (factor: number): number => Number((factor - 1).toFixed(12))
export const rotatingMassFactorFromCoefficient = (coefficient: number): number => Number((1 + coefficient).toFixed(12))

export interface SignalParameters { routeSettingS: number; reactionS: number; clearanceS: number }
export type TractionMode = 'arrival' | 'departure' | 'through' | 'shunting'
export interface TractionStop { positionM: number; dwellS: number }
export interface TractionRunOptions {
    /** Entry/exit speeds are desired upper bounds; actual endpoint speeds come from result samples. */
    mode: TractionMode; initialSpeedKmh: number; finalSpeedKmh: number; stepM: number
    /** User-entered length of the first departure circuit; 0 disables the extension. */
    departureSectionLengthM?: number
    /** Optional arrival/through braking reserve. Empty is automatic; zero is an explicit value. */
    brakingDistanceM?: number | null
    /** Empty extension speed limits use the train maximum. */
    brakingMaxSpeedKmh?: number | null
    departureSectionMaxSpeedKmh?: number | null
    maxPointCount: number; stops: TractionStop[]
}
export interface TractionRouteSegment {
    startM: number; endM: number; speedLimitKmh: number
    gradientPermille?: number; additionalResistanceKn?: number
}
export interface TractionDevice { id: string; label: string; type: string; positionM: number }
export interface TractionJoint { id: string; label: string; positionM: number }
export interface TractionCell {
    id: string; label: string; startM: number; endM: number
    /** False when the displayed range is clipped inside the physical circuit. */
    entryBoundaryKnown?: boolean; exitBoundaryKnown?: boolean
}
export interface TractionRouteProfile {
    lengthM: number; segments: TractionRouteSegment[]; devices?: TractionDevice[]
    joints: TractionJoint[]; cells?: TractionCell[]
}
export interface TractionSample { positionM: number; speedKmh: number; limitKmh: number; timeS: number }
export interface TractionJointTime extends TractionJoint {
    headTimeS: number; tailTimeS: number | null; releaseTimeS: number | null
}
export interface TractionCellOccupancy extends TractionCell {
    entryTimeS: number | null; exitTimeS: number | null; releaseTimeS: number | null
}
export interface TractionResult {
    /** Effective arrival/through braking reserve returned by the server; absent on older snapshots. */
    approachDistanceM?: number
    departureSectionLengthM?: number
    samples: TractionSample[]; jointTimes: TractionJointTime[]; cellOccupancies: TractionCellOccupancy[]
    lengthM: number; trainLengthM: number; mode: TractionMode; durationS: number
    motionStartS: number; totalTimeS: number; maxSpeedKmh: number; warnings: string[]
    runningTimeS: number; dwellTimeS: number; engine: 'SwitchYard.SpeedProfile'
}
/** Coordinate translation only: braking distance is supplied by the backend. */
export function withBrakingApproach(profile: TractionRouteProfile, distanceM: number, speedLimitKmh: number): TractionRouteProfile {
    if (!(distanceM > 0)) return profile
    const entry = profile.segments[0]!
    return {
        lengthM: profile.lengthM + distanceM,
        segments: [{ ...entry, startM: 0, endM: distanceM, speedLimitKmh },
            ...profile.segments.map(s => ({ ...s, startM: s.startM + distanceM, endM: s.endM + distanceM }))],
        devices: profile.devices?.map(d => ({ ...d, positionM: d.positionM + distanceM })),
        joints: profile.joints.map(j => ({ ...j, positionM: j.positionM + distanceM })),
        cells: profile.cells?.map(c => ({ ...c, startM: c.startM + distanceM, endM: c.endM + distanceM })),
    }
}
/** Shared synthetic identity; it cannot be confused with a station CellID occurrence. */
export function departureSectionCell(profile: Pick<TractionRouteProfile, 'lengthM' | 'cells'>, distanceM: number): TractionCell {
    const ids = new Set(profile.cells?.map(cell => cell.id))
    let id = '__first-departure__'
    while (ids.has(id)) id += '_'
    return { id, label: '一离去', startM: profile.lengthM, endM: profile.lengthM + distanceM,
        entryBoundaryKnown: true, exitBoundaryKnown: true }
}
/** Display translation only; the backend solves the full route and appended circuit. */
export function withDepartureSection(profile: TractionRouteProfile, distanceM: number, speedLimitKmh: number): TractionRouteProfile {
    if (!Number.isFinite(distanceM) || !(distanceM > 0)) return profile
    const cell = departureSectionCell(profile, distanceM)
    return { ...profile, lengthM: cell.endM,
        segments: [...profile.segments, { ...profile.segments[profile.segments.length - 1]!, startM: cell.startM, endM: cell.endM, speedLimitKmh }],
        cells: [...(profile.cells ?? []), cell] }
}
/** Resolve saved or editable extension settings without changing the train's global speed limit. */
export function extensionSpeedLimits(train: Pick<TrainParameters, 'maxSpeedKmh'>,
    options: Pick<TractionRunOptions, 'brakingMaxSpeedKmh' | 'departureSectionMaxSpeedKmh'>) {
    return {
        brakingMaxSpeedKmh: Math.min(train.maxSpeedKmh, options.brakingMaxSpeedKmh ?? train.maxSpeedKmh),
        departureSectionMaxSpeedKmh: Math.min(train.maxSpeedKmh, options.departureSectionMaxSpeedKmh ?? train.maxSpeedKmh),
    }
}
/** Generate table rows; editing generator inputs never changes existing curve data. */
export function generateCharacteristic(maxSpeedKmh: number, force: (speed: number) => number): ForcePoint[] {
    if (!Number.isFinite(maxSpeedKmh) || maxSpeedKmh <= 0 || maxSpeedKmh > 1000) throw new Error('Invalid maximum speed.')
    const speeds = Array.from({ length: Math.ceil(maxSpeedKmh / 10) }, (_, i) => i * 10)
    speeds.push(maxSpeedKmh)
    return speeds.map(speedKmh => ({ speedKmh, forceKn: force(speedKmh) }))
}
export const generateBrakingCurve = (train: Pick<TrainParameters, 'maxSpeedKmh' | 'maxBrakeForceKn'>) =>
    generateCharacteristic(train.maxSpeedKmh, () => train.maxBrakeForceKn)
export const generateResistanceCurve = (train: Pick<TrainParameters, 'maxSpeedKmh' | 'resistanceA' | 'resistanceB' | 'resistanceC'>) =>
    generateCharacteristic(train.maxSpeedKmh, v => train.resistanceA + train.resistanceB * v + train.resistanceC * v * v)
export function defaultTrainParameters(): TrainParameters {
    return { lengthM: 200, massT: 440, rotatingMassFactor: 1.08, maxSpeedKmh: 160,
        tractionCurve: [{ speedKmh: 0, forceKn: 300 }, { speedKmh: 40, forceKn: 300 },
            { speedKmh: 80, forceKn: 210 }, { speedKmh: 120, forceKn: 140 }, { speedKmh: 160, forceKn: 105 }],
        maxBrakeForceKn: 310, resistanceA: 5, resistanceB: 0.05, resistanceC: 0.002,
        brakingCurve: generateCharacteristic(160, () => 310), resistanceCurve: generateCharacteristic(160, v => 5 + .05 * v + .002 * v * v) }
}
export function defaultSignalParameters(): SignalParameters { return { routeSettingS: 15, reactionS: 3, clearanceS: 5 } }
export function defaultRunOptions(mode: TractionMode = 'arrival'): TractionRunOptions {
    return { mode, initialSpeedKmh: mode === 'departure' || mode === 'shunting' ? 0 : 40,
        finalSpeedKmh: mode === 'arrival' || mode === 'shunting' ? 0 : 40, departureSectionLengthM: 0, brakingDistanceM: null,
        brakingMaxSpeedKmh: null, departureSectionMaxSpeedKmh: null, stepM: 5, maxPointCount: 100001, stops: [] }
}
/** Select a new route's movement default without changing existing saved records. */
export function tractionModeForRoute(type: string, hasContinuation = false): TractionMode {
    if (hasContinuation) return 'through'
    const normalized = type.trim().toLowerCase()
    if (['dep', 'departure', '发车', '发车进路'].includes(normalized)) return 'departure'
    if (['arr', 'arrival', '接车', '接车进路'].includes(normalized)) return 'arrival'
    if (['shunt', 'shunting', '调车', '调车进路'].includes(normalized)) return 'shunting'
    return 'through'
}

/**
 * Interpolate a server-produced profile for the chart cursor/tail line.
 * It never constructs a speed profile or supplies joint/circuit results.
 * At a stop return first arrival; after it use the last (departure) sample.
 */
export function headTimeAt(result: TractionResult, positionM: number): number | null {
    const samples = result.samples
    if (!Number.isFinite(positionM) || positionM < 0 || samples.length === 0) return null
    const last = samples[samples.length - 1]!
    if (positionM > last.positionM) {
        return result.mode !== 'arrival' && last.speedKmh > 0
            ? last.timeS + (positionM - last.positionM) / (last.speedKmh / 3.6) : null
    }
    let low = 0, high = samples.length
    while (low < high) {
        const middle = low + Math.floor((high - low) / 2)
        if (samples[middle]!.positionM < positionM) low = middle + 1
        else high = middle
    }
    const to = samples[low]
    if (!to) return null
    if (to.positionM === positionM) return to.timeS
    const from = samples[low - 1]
    if (!from || to.positionM <= from.positionM) return null
    const distance = positionM - from.positionM
    const fraction = distance / (to.positionM - from.positionM)
    const initial = from.speedKmh / 3.6, final = to.speedKmh / 3.6
    const speed = Math.sqrt((1 - fraction) * initial * initial + fraction * final * final)
    return initial + speed > 0 ? from.timeS + 2 * distance / (initial + speed) : null
}

export interface ApproachPreparationInterval {
    kind: 'route-setting' | 'reaction'
    startTimeS: number
    endTimeS: number
    durationS: number
}

/** Split the saved pre-approach time; never use unsolved, editable signal parameters. */
export function approachPreparationIntervals(approaching: CellApproachingInterval | null,
    signal: SignalParameters | null | undefined, mode: TractionMode = 'arrival'): ApproachPreparationInterval[] {
    if (!validPreparationTiming(approaching, signal, mode) || !signal) return []
    const { routeSettingS, reactionS } = signal
    const intervals: ApproachPreparationInterval[] = [
        { kind: 'route-setting', startTimeS: 0, endTimeS: routeSettingS, durationS: routeSettingS },
        { kind: 'reaction', startTimeS: routeSettingS, endTimeS: routeSettingS + reactionS, durationS: reactionS },
    ]
    return intervals.filter(interval => interval.durationS > 0)
}

function validPreparationTiming(approaching: CellApproachingInterval | null,
    signal: SignalParameters | null | undefined, mode: TractionMode): boolean {
    if (!approaching || !signal) return false
    const { routeSettingS, reactionS } = signal
    if (![routeSettingS, reactionS, approaching.startTimeS].every(value => Number.isFinite(value) && value >= 0)) return false
    const gapS = approaching.startTimeS - routeSettingS - reactionS
    // Keep t=0 at the saved route request, even if the first circuit starts farther along the route.
    return mode === 'departure' ? gapS >= -1e-6 : Math.abs(gapS) <= 1e-6
}

export interface CellApproachingInterval {
    startPositionM: number
    startTimeS: number
    endTimeS: number
    durationS: number
}

/**
 * Arrival and through circuits share the braking-range entry at the saved run
 * origin (legacy through runs may have no reserve). Shunting uses the original
 * route origin. Departure shares first circuit
 * entry. Each interval ends at its saved head-entry event, without extrapolation.
 */
export function cellApproachingInterval(result: TractionResult | null, cell: TractionCellOccupancy): CellApproachingInterval | null {
    if (!result
        || cell.entryBoundaryKnown === false || cell.entryTimeS === null || !Number.isFinite(cell.entryTimeS)
        || !Number.isFinite(cell.startM) || !result.samples.length) return null
    const first = result.samples[0]!, last = result.samples[result.samples.length - 1]!
    if (result.mode === 'departure') {
        const origin = result.cellOccupancies.reduce<TractionCellOccupancy | null>((earliest, candidate) =>
            !earliest || candidate.startM < earliest.startM ? candidate : earliest, null)
        if (!origin || origin.entryBoundaryKnown === false || origin.entryTimeS === null
            || !Number.isFinite(origin.entryTimeS) || origin.entryTimeS < 0 || !Number.isFinite(origin.startM)
            || !Number.isFinite(first.positionM) || origin.startM < first.positionM - 1e-6
            || !Number.isFinite(first.timeS) || origin.entryTimeS < first.timeS
            || !Number.isFinite(last.positionM)
            || cell.startM < origin.startM - 1e-6 || cell.startM > last.positionM
            || origin.entryTimeS > cell.entryTimeS) return null
        return { startPositionM: origin.startM, startTimeS: origin.entryTimeS, endTimeS: cell.entryTimeS,
            durationS: cell.entryTimeS - origin.entryTimeS }
    }
    const distance = result.approachDistanceM ?? (result.mode === 'through' || result.mode === 'shunting' ? 0 : NaN)
    if (!Number.isFinite(distance) || distance < 0) return null
    // Require the shared origin to be saved; a clipped run cannot supply its arrival time.
    if (!Number.isFinite(first.positionM) || Math.abs(first.positionM) > 1e-6
        || cell.startM < distance - 1e-6 || cell.startM > last.positionM) return null
    const startPositionM = 0
    const startTimeS = first.timeS
    const endTimeS = cell.entryTimeS
    if (startTimeS === null || !Number.isFinite(startTimeS) || startTimeS > endTimeS) return null
    return { startPositionM, startTimeS, endTimeS, durationS: endTimeS - startTimeS }
}

/** End the reservation at first terminal arrival when the stopped train still occupies this circuit. */
export function cellTerminalStopTime(result: TractionResult | null, cell: TractionCellOccupancy): number | null {
    if (!result || result.samples.length < 2 || cell.exitTimeS !== null || cell.releaseTimeS !== null
        || !Number.isFinite(result.motionStartS) || result.motionStartS < 0
        || !Number.isFinite(result.trainLengthM) || result.trainLengthM <= 0
        || !Number.isFinite(cell.startM) || !Number.isFinite(cell.endM) || cell.startM < 0 || cell.endM <= cell.startM) return null
    const last = result.samples[result.samples.length - 1]!
    if (last.speedKmh !== 0 || !Number.isFinite(last.positionM) || !Number.isFinite(last.timeS)
        || !Number.isFinite(result.lengthM) || Math.abs(last.positionM - result.lengthM) > 1e-6
        || cell.endM > last.positionM + 1e-6
        || cell.startM >= last.positionM - 1e-6 || cell.endM <= last.positionM - result.trainLengthM + 1e-6) return null
    // headTimeAt uses first arrival at a position, before any terminal dwell samples.
    const stoppedAt = headTimeAt(result, last.positionM)
    if (stoppedAt === null || !Number.isFinite(stoppedAt) || stoppedAt < result.motionStartS || stoppedAt > last.timeS
        || (cell.entryBoundaryKnown === false ? cell.entryTimeS !== null
            : cell.entryTimeS === null || !Number.isFinite(cell.entryTimeS) || cell.entryTimeS < 0 || cell.entryTimeS > stoppedAt)) return null
    return stoppedAt
}

/** Reservation end follows saved release, or the terminal-stop convention; physical clearance stays unchanged. */
export function cellOccupancyEndTime(result: TractionResult | null, cell: TractionCellOccupancy): number | null {
    const stoppedAt = cellTerminalStopTime(result, cell)
    if (stoppedAt !== null) return stoppedAt
    if (cell.exitBoundaryKnown === false || cell.exitTimeS === null || cell.releaseTimeS === null
        || !Number.isFinite(cell.exitTimeS) || !Number.isFinite(cell.releaseTimeS)
        || cell.exitTimeS < (cell.entryTimeS ?? 0) || cell.releaseTimeS < cell.exitTimeS) return null
    return cell.releaseTimeS
}

/** Reservation envelope includes preparation and ends at saved release or the terminal stop. */
export function cellTotalOccupancyInterval(result: TractionResult | null, cell: TractionCellOccupancy,
    signal: SignalParameters | null | undefined): Omit<CellApproachingInterval, 'startPositionM'> | null {
    if (!validPreparationTiming(cellApproachingInterval(result, cell), signal, result?.mode ?? 'through')) return null
    const endTimeS = cellOccupancyEndTime(result, cell)
    return endTimeS === null ? null : { startTimeS: 0, endTimeS, durationS: endTimeS }
}

/** Summarize one saved calculation, using the same complete intervals as the occupancy chart. */
export function summarizeTractionResult(result: TractionResult, profile: TractionRouteProfile,
    signal: SignalParameters | null | undefined) {
    const first = result.samples[0], last = result.samples[result.samples.length - 1]
    const journeyTimeS = first && last ? last.timeS - first.timeS : null
    let longestOccupancy: { cell: TractionCellOccupancy; durationS: number } | null = null
    let unknownOccupancyCount = 0
    for (const cell of result.cellOccupancies) {
        const total = cellTotalOccupancyInterval(result, cell, signal)
        if (!total) unknownOccupancyCount++
        else if (!longestOccupancy || total.durationS > longestOccupancy.durationS) {
            longestOccupancy = { cell, durationS: total.durationS }
        }
    }
    return {
        brakingDistanceM: result.mode === 'departure' || result.mode === 'shunting' ? 0 : result.approachDistanceM
            ?? Math.max(0, result.lengthM - profile.lengthM - (result.departureSectionLengthM ?? 0)),
        routeLengthM: profile.lengthM,
        entrySpeedKmh: first?.speedKmh ?? null,
        exitSpeedKmh: last?.speedKmh ?? null,
        journeyTimeS: journeyTimeS !== null && Number.isFinite(journeyTimeS) && journeyTimeS >= 0 ? journeyTimeS : null,
        longestOccupancy, unknownOccupancyCount,
    }
}

/** Display elapsed time consistently in the summary strip and circuit labels. */
export function formatTractionDuration(seconds: number | null | undefined): string {
    if (seconds === null || seconds === undefined || !Number.isFinite(seconds) || seconds < 0) return '--:--'
    const rounded = Math.round(seconds)
    return String(Math.floor(rounded / 60)).padStart(2, '0') + ':' + String(rounded % 60).padStart(2, '0')
}
