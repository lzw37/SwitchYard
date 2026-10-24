/**
 * Traction API models and chart interpolation only.
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
export type TractionMode = 'arrival' | 'departure' | 'through'
export interface TractionStop { positionM: number; dwellS: number }
export interface TractionRunOptions {
    mode: TractionMode; initialSpeedKmh: number; finalSpeedKmh: number; stepM: number
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
    /** Automatic arrival approach computed by the server; absent on older snapshots. */
    approachDistanceM?: number
    samples: TractionSample[]; jointTimes: TractionJointTime[]; cellOccupancies: TractionCellOccupancy[]
    lengthM: number; trainLengthM: number; mode: TractionMode; durationS: number
    motionStartS: number; totalTimeS: number; maxSpeedKmh: number; warnings: string[]
    runningTimeS: number; dwellTimeS: number; engine: 'SwitchYard.SpeedProfile'
}
/** Coordinate translation only: braking distance is supplied by the backend. */
export function withArrivalApproach(profile: TractionRouteProfile, distanceM: number, speedLimitKmh: number): TractionRouteProfile {
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
export function defaultRunOptions(): TractionRunOptions {
    return { mode: 'arrival', initialSpeedKmh: 40, finalSpeedKmh: 0, stepM: 5, maxPointCount: 100001, stops: [] }
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
    signal: SignalParameters | null | undefined): ApproachPreparationInterval[] {
    if (!approaching || !signal) return []
    const { routeSettingS, reactionS } = signal
    if (![routeSettingS, reactionS, approaching.startTimeS].every(value => Number.isFinite(value) && value >= 0)
        || Math.abs(routeSettingS + reactionS - approaching.startTimeS) > 1e-6) return []
    const intervals: ApproachPreparationInterval[] = [
        { kind: 'route-setting', startTimeS: 0, endTimeS: routeSettingS, durationS: routeSettingS },
        { kind: 'reaction', startTimeS: routeSettingS, endTimeS: approaching.startTimeS, durationS: reactionS },
    ]
    return intervals.filter(interval => interval.durationS > 0)
}

export interface CellApproachingInterval {
    startPositionM: number
    startTimeS: number
    endTimeS: number
    durationS: number
}

/**
 * Display interval from the saved arrival braking reservation and saved head samples.
 * All circuits share the head arrival at the route braking reservation origin; each
 * interval ends at its own saved head-entry event. No force solver or extrapolation.
 */
export function cellApproachingInterval(result: TractionResult | null, cell: TractionCellOccupancy): CellApproachingInterval | null {
    const distance = result?.approachDistanceM
    if (!result || result.mode !== 'arrival' || typeof distance !== 'number' || !Number.isFinite(distance) || distance <= 0
        || cell.entryBoundaryKnown === false || cell.entryTimeS === null || !Number.isFinite(cell.entryTimeS)
        || !Number.isFinite(cell.startM) || !result.samples.length) return null
    const first = result.samples[0]!, last = result.samples[result.samples.length - 1]!
    // The expanded arrival run starts where the head enters the shared braking range.
    // Require that origin to be saved; a clipped run cannot supply its arrival time.
    if (!Number.isFinite(first.positionM) || Math.abs(first.positionM) > 1e-6
        || cell.startM < distance - 1e-6 || cell.startM > last.positionM) return null
    const startPositionM = 0
    const startTimeS = first.timeS
    const endTimeS = cell.entryTimeS
    if (startTimeS === null || !Number.isFinite(startTimeS) || startTimeS > endTimeS) return null
    return { startPositionM, startTimeS, endTimeS, durationS: endTimeS - startTimeS }
}
