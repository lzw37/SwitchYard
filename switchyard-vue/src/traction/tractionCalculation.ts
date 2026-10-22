/**
 * Traction API models and chart interpolation only.
 * Units: m, s, t, kN, km/h. Davis A+Bv+Cv² uses km/h.
 * The speed solver and occupancy calculations run in SwitchYard.SpeedProfile on the server.
 */
export interface ForcePoint { speedKmh: number; forceKn: number }
export interface TrainParameters {
    lengthM: number; massT: number; rotatingMassFactor: number; maxSpeedKmh: number
    tractionCurve: ForcePoint[]; maxBrakeForceKn: number
    resistanceA: number; resistanceB: number; resistanceC: number
}
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
    samples: TractionSample[]; jointTimes: TractionJointTime[]; cellOccupancies: TractionCellOccupancy[]
    lengthM: number; trainLengthM: number; mode: TractionMode; durationS: number
    motionStartS: number; totalTimeS: number; maxSpeedKmh: number; warnings: string[]
    runningTimeS: number; dwellTimeS: number; engine: 'SwitchYard.SpeedProfile'
}
export function defaultTrainParameters(): TrainParameters {
    return { lengthM: 200, massT: 440, rotatingMassFactor: 1.08, maxSpeedKmh: 160,
        tractionCurve: [{ speedKmh: 0, forceKn: 300 }, { speedKmh: 40, forceKn: 300 },
            { speedKmh: 80, forceKn: 210 }, { speedKmh: 120, forceKn: 140 }, { speedKmh: 160, forceKn: 105 }],
        maxBrakeForceKn: 310, resistanceA: 5, resistanceB: 0.05, resistanceC: 0.002 }
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
