import axios from 'axios'
import type { ForcePoint, TrainParameters } from './tractionCalculation'

export type TrainCharacteristics = Pick<TrainParameters, 'tractionCurve' | 'brakingCurve' | 'resistanceCurve'>
export const MAX_CHARACTERISTICS_FILE_BYTES = 2 * 1024 * 1024

/** The parser endpoint has no persistence side effects; the dialog owns the project-save step. */
export async function importTrainCharacteristics(file: File, signal?: AbortSignal): Promise<TrainCharacteristics> {
    const body = new FormData()
    body.append('file', file)
    const { data } = await axios.post<TrainCharacteristics>('/api/SpeedProfile/train-characteristics/import', body, {
        // Override the app's JSON default; Axios adds the multipart boundary when sending the file.
        headers: { 'Content-Type': 'multipart/form-data' },
        signal, timeout: 30_000,
    })
    const result = {} as TrainCharacteristics
    for (const key of ['tractionCurve', 'brakingCurve', 'resistanceCurve'] as const) {
        const points = data?.[key]
        if (!Array.isArray(points) || points.length < 1 || points.length > 128 || points.some((point: ForcePoint, index) =>
            !point || !Number.isFinite(point.speedKmh) || point.speedKmh < 0 || point.speedKmh > 10000 ||
            !Number.isFinite(point.forceKn) || point.forceKn < 0 || point.forceKn > 10000000 ||
            (index === 0 ? point.speedKmh !== 0 : point.speedKmh <= points[index - 1]!.speedKmh) ||
            (key === 'tractionCurve' && index > 0 && point.forceKn > points[index - 1]!.forceKn))) {
            throw new Error('Invalid train characteristics import response.')
        }
        result[key] = points.map(point => ({ speedKmh: point.speedKmh, forceKn: point.forceKn }))
    }
    return result
}
