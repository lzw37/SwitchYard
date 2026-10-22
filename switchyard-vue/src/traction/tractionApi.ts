// main.ts initializes the shared axios authentication/refresh interceptors before mounting the app.
import axios from 'axios'
import type {
    SignalParameters, TrainParameters, TractionRouteProfile, TractionRunOptions, TractionResult,
} from './tractionCalculation'

export interface TractionApiRequest {
    profile: TractionRouteProfile; train: TrainParameters; signal: SignalParameters; options: TractionRunOptions
}

/** Copy only the documented fields; station geometry and old local-storage fields are not API input. */
export function buildTractionApiRequest(
    profile: TractionRouteProfile, train: TrainParameters, signal: SignalParameters, options: TractionRunOptions,
): TractionApiRequest {
    return {
        profile: {
            lengthM: profile.lengthM,
            segments: profile.segments.map(s => ({
                startM: s.startM, endM: s.endM, speedLimitKmh: s.speedLimitKmh,
                gradientPermille: s.gradientPermille ?? 0, additionalResistanceKn: s.additionalResistanceKn ?? 0,
            })),
            devices: (profile.devices ?? []).map(p => ({ id: p.id, label: p.label, type: p.type, positionM: p.positionM })),
            joints: profile.joints.map(p => ({ id: p.id, label: p.label, positionM: p.positionM })),
            cells: (profile.cells ?? []).map(c => ({
                id: c.id, label: c.label, startM: c.startM, endM: c.endM,
                entryBoundaryKnown: c.entryBoundaryKnown ?? true, exitBoundaryKnown: c.exitBoundaryKnown ?? true,
            })),
        },
        train: {
            lengthM: train.lengthM, massT: train.massT, rotatingMassFactor: train.rotatingMassFactor,
            maxSpeedKmh: train.maxSpeedKmh, maxBrakeForceKn: train.maxBrakeForceKn,
            tractionCurve: train.tractionCurve.map(p => ({ speedKmh: p.speedKmh, forceKn: p.forceKn })),
            resistanceA: train.resistanceA, resistanceB: train.resistanceB, resistanceC: train.resistanceC,
        },
        signal: { routeSettingS: signal.routeSettingS, reactionS: signal.reactionS, clearanceS: signal.clearanceS },
        options: {
            mode: options.mode, initialSpeedKmh: options.initialSpeedKmh, finalSpeedKmh: options.finalSpeedKmh,
            stepM: options.stepM, maxPointCount: options.maxPointCount,
            stops: options.stops.map(s => ({ positionM: s.positionM, dwellS: s.dwellS })),
        },
    }
}

export async function calculateTractionRemote(
    profile: TractionRouteProfile, train: TrainParameters, signal: SignalParameters,
    options: TractionRunOptions, abortSignal?: AbortSignal,
): Promise<TractionResult> {
    const request = buildTractionApiRequest(profile, train, signal, options)
    const { data } = await axios.post<TractionResult>('/api/SpeedProfile/calculate', request, {
        signal: abortSignal, timeout: 30_000,
    })
    // Reject an HTML proxy/login response or incompatible API instead of drawing misleading data.
    if (!data || data.engine !== 'SwitchYard.SpeedProfile' ||
        !Array.isArray(data.samples) || data.samples.length < 2 ||
        !Array.isArray(data.jointTimes) || !Array.isArray(data.cellOccupancies) || !Array.isArray(data.warnings) ||
        ![data.durationS, data.runningTimeS, data.dwellTimeS, data.motionStartS, data.totalTimeS,
            data.lengthM, data.trainLengthM, data.maxSpeedKmh].every(Number.isFinite)) {
        throw Object.assign(new Error('Invalid speed profile API response.'), { code: 'invalid-response' })
    }
    return data
}
