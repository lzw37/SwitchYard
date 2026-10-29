export interface SolveSettings {
    timeLimitSeconds: number
    threadCount: number
    leftShiftToleranceSeconds: number
    rightShiftToleranceSeconds: number
    objective: 'min-end-time' | 'min-duration' | 'min-start-and-end'
}

export function normalizeSolveSettings(value?: Partial<SolveSettings> | null): SolveSettings {
    const integer = (number: unknown, fallback: number, min: number, max: number) =>
        typeof number === 'number' && Number.isFinite(number) ? Math.max(min, Math.min(max, Math.round(number))) : fallback
    const objective = value?.objective
    return {
        timeLimitSeconds: integer(value?.timeLimitSeconds, 3600, 1, 86400),
        threadCount: integer(value?.threadCount, 12, 1, 128),
        leftShiftToleranceSeconds: integer(value?.leftShiftToleranceSeconds, 50400, 0, 172800),
        rightShiftToleranceSeconds: integer(value?.rightShiftToleranceSeconds, 50400, 0, 172800),
        objective: objective === 'min-duration' || objective === 'min-start-and-end' ? objective : 'min-end-time',
    }
}

export function applySolveSettings(input: unknown, settings: SolveSettings): unknown {
    if (!input || typeof input !== 'object' || Array.isArray(input)) return input
    const fields = Object.entries(input).filter(([key]) => key.toLowerCase() !== 'settings')
    return { ...Object.fromEntries(fields), settings: { ...settings } }
}
