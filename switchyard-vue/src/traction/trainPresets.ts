import { defaultTrainParameters, generateBrakingCurve, generateResistanceCurve } from './tractionCalculation.ts'
import type { TrainParameters } from './tractionCalculation'

export interface TrainPreset {
    id: string
    name: string
    nameEn?: string
    parameters: TrainParameters
}
export interface TrainPresetCatalog {
    version: 1
    selectedId: string
    presets: TrainPreset[]
}

/** Synthetic examples for exploring the calculation, not specifications of actual train models. */
export function createDefaultTrainPresetCatalog(): TrainPresetCatalog {
    const short = defaultTrainParameters()
    const long: TrainParameters = {
        ...cloneTrainParameters(short), lengthM: 400, massT: 880, maxSpeedKmh: 250,
        tractionCurve: [
            ...short.tractionCurve.map(point => ({ speedKmh: point.speedKmh, forceKn: point.forceKn * 2 })),
            { speedKmh: 250, forceKn: 134.4 },
        ],
        maxBrakeForceKn: short.maxBrakeForceKn * 2,
        resistanceA: short.resistanceA * 2, resistanceB: short.resistanceB * 2, resistanceC: short.resistanceC * 2,
    }
    const freight: TrainParameters = {
        brakingCurve: [], resistanceCurve: [],
        lengthM: 750, massT: 5000, rotatingMassFactor: 1.06, maxSpeedKmh: 120,
        tractionCurve: [
            { speedKmh: 0, forceKn: 600 }, { speedKmh: 20, forceKn: 600 },
            { speedKmh: 40, forceKn: 500 }, { speedKmh: 60, forceKn: 350 },
            { speedKmh: 80, forceKn: 260 }, { speedKmh: 100, forceKn: 210 }, { speedKmh: 120, forceKn: 175 },
        ],
        maxBrakeForceKn: 1800, resistanceA: 20, resistanceB: 0.35, resistanceC: 0.006,
    }
    for (const train of [long, freight]) {
        train.brakingCurve = generateBrakingCurve(train)
        train.resistanceCurve = generateResistanceCurve(train)
    }
    return {
        version: 1, selectedId: 'example-short-passenger', presets: [
            { id: 'example-short-passenger', name: '短编组客运（示例）', nameEn: 'Short passenger consist (example)', parameters: short },
            { id: 'example-long-passenger', name: '长编组客运（示例）', nameEn: 'Long passenger consist (example)', parameters: long },
            { id: 'example-freight', name: '货运（示例）', nameEn: 'Freight consist (example)', parameters: freight },
        ],
    }
}

/** Copy only model fields, with no shared traction-point objects or arrays. */
export function cloneTrainParameters(train: TrainParameters): TrainParameters {
    return {
        lengthM: train.lengthM, massT: train.massT, rotatingMassFactor: train.rotatingMassFactor,
        maxSpeedKmh: train.maxSpeedKmh, maxBrakeForceKn: train.maxBrakeForceKn,
        resistanceA: train.resistanceA, resistanceB: train.resistanceB, resistanceC: train.resistanceC,
        brakingCurve: (train.brakingCurve ?? generateBrakingCurve(train)).map(point => ({ speedKmh: point.speedKmh, forceKn: point.forceKn })),
        resistanceCurve: (train.resistanceCurve ?? generateResistanceCurve(train)).map(point => ({ speedKmh: point.speedKmh, forceKn: point.forceKn })),
        tractionCurve: train.tractionCurve.map(point => ({ speedKmh: point.speedKmh, forceKn: point.forceKn })),
    }
}

export function cloneTrainPresetCatalog(catalog: TrainPresetCatalog): TrainPresetCatalog {
    return {
        version: catalog.version, selectedId: catalog.selectedId,
        presets: catalog.presets.map(preset => ({
            id: preset.id, name: preset.name,
            ...(preset.nameEn !== undefined ? { nameEn: preset.nameEn } : {}),
            parameters: cloneTrainParameters(preset.parameters),
        })),
    }
}

type UnknownRecord = Record<string, unknown>
const isRecord = (value: unknown): value is UnknownRecord => value !== null && typeof value === 'object' && !Array.isArray(value)
const inRange = (value: unknown, minimum: number, maximum: number): value is number =>
    typeof value === 'number' && Number.isFinite(value) && value >= minimum && value <= maximum
const validID = (value: unknown): value is string => typeof value === 'string' && value.length > 0 && value === value.trim()
const validName = (value: unknown): value is string =>
    typeof value === 'string' && value.trim().length > 0 && Array.from(value).length <= 80
const nameKey = (value: string): string => value.trim().toLowerCase()

/** Mirrors SpeedProfileCalculationService's HTTP limits, including its 128-point curve cap. */
function validateParameters(value: unknown): string {
    if (!isRecord(value)) return 'invalid-train-parameters'
    if (!inRange(value.lengthM, 0.001, 100_000)) return 'invalid-train-length'
    if (!inRange(value.massT, 0.001, 10_000_000)) return 'invalid-train-mass'
    if (!inRange(value.rotatingMassFactor, 1, 3)) return 'invalid-rotating-mass-factor'
    if (!inRange(value.maxSpeedKmh, 0.001, 1000)) return 'invalid-train-max-speed'
    if (!inRange(value.maxBrakeForceKn, 0.001, 10_000_000)) return 'invalid-brake-force'
    if (!inRange(value.resistanceA, 0, 10_000_000) || !inRange(value.resistanceB, 0, 10_000_000)
        || !inRange(value.resistanceC, 0, 10_000_000)) return 'invalid-resistance'
    for (const name of ['brakingCurve', 'resistanceCurve'] as const) {
        const curve = value[name]
        if (curve === undefined) continue // Upgrade legacy scalar-only records when cloning.
        if (!Array.isArray(curve) || curve.length < 1 || curve.length > 128) return 'invalid-' + name
        let previous = -1
        for (const point of curve) {
            if (!isRecord(point) || !inRange(point.speedKmh, 0, 10000) || !inRange(point.forceKn, 0, 10000000)
                || (previous < 0 && point.speedKmh !== 0) || point.speedKmh <= previous) return 'invalid-' + name
            previous = point.speedKmh
        }
    }
    if (!Array.isArray(value.tractionCurve) || value.tractionCurve.length < 1 || value.tractionCurve.length > 128) return 'invalid-traction-curve'
    let previousSpeed = -1, previousForce = Infinity
    for (const point of value.tractionCurve) {
        if (!isRecord(point) || !inRange(point.speedKmh, 0, 10_000) || !inRange(point.forceKn, 0, 10_000_000)
            || (previousSpeed < 0 && point.speedKmh !== 0) || point.speedKmh <= previousSpeed || point.forceKn > previousForce) {
            return 'invalid-traction-curve'
        }
        previousSpeed = point.speedKmh
        previousForce = point.forceKn
    }
    return ''
}

/**
 * Return a stable error code, or an empty string for a complete valid catalog.
 * Names are unique after trimming and case folding, separately for each language.
 * A valid parameter set need not make every route feasible; the solver checks that.
 */
export function validateTrainPresetCatalog(value: unknown): string {
    if (!isRecord(value)) return 'invalid-catalog'
    if (value.version !== 1) return 'invalid-version'
    if (!Array.isArray(value.presets) || value.presets.length < 1 || value.presets.length > 1000) return 'invalid-preset-count'
    const ids = new Set<string>(), names = new Set<string>(), englishNames = new Set<string>()
    for (const preset of value.presets) {
        if (!isRecord(preset) || !validID(preset.id)) return 'invalid-preset-id'
        if (ids.has(preset.id)) return 'duplicate-preset-id'
        ids.add(preset.id)
        if (!validName(preset.name) || (preset.nameEn !== undefined && !validName(preset.nameEn))) return 'invalid-preset-name'
        const name = nameKey(preset.name), englishName = typeof preset.nameEn === 'string' ? nameKey(preset.nameEn) : undefined
        if (names.has(name) || (englishName !== undefined && englishNames.has(englishName))) return 'duplicate-preset-name'
        names.add(name)
        if (englishName !== undefined) englishNames.add(englishName)
        const parameterError = validateParameters(preset.parameters)
        if (parameterError) return parameterError
    }
    return validID(value.selectedId) && ids.has(value.selectedId) ? '' : 'invalid-selected-preset'
}

/** Parse persisted data without reading storage or silently replacing invalid data with defaults. */
export function parseTrainPresetCatalog(raw: string | null): TrainPresetCatalog | null {
    if (typeof raw !== 'string' || !raw.trim()) return null
    try {
        const value: unknown = JSON.parse(raw)
        return validateTrainPresetCatalog(value) ? null : cloneTrainPresetCatalog(value as TrainPresetCatalog)
    } catch {
        return null
    }
}
