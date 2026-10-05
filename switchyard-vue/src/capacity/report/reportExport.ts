import type { ProcessTemplate } from '../operationProcess'
import type { ReportOccupancyRow } from './stationCapacityReport'

/** Configuration is read independently of the lazily mounted chart selector. */
export function reportOccupancyCharts(value: unknown, rows: ReportOccupancyRow[]) {
    if (!value || typeof value !== 'object') throw new Error('Invalid occupancy chart response')
    const data = value as Record<string, unknown>
    if (typeof data.isConfigured !== 'boolean' || !Array.isArray(data.charts)) throw new Error('Invalid occupancy chart response')
    if (!data.isConfigured) return [{ name: '资源占用图', rows }]
    if (!data.charts.length) throw new Error('Missing configured occupancy charts')
    const byID = new Map(rows.map(row => [row.cellID, row]))
    return data.charts.map((value: unknown) => {
        if (!value || typeof value !== 'object') throw new Error('Invalid occupancy chart')
        const chart = value as Record<string, unknown>
        if (typeof chart.chartName !== 'string' || !chart.chartName.trim() || !Array.isArray(chart.cellIDs)
            || !chart.cellIDs.every(id => typeof id === 'string')) throw new Error('Invalid occupancy chart')
        return { name: chart.chartName.trim(), rows: [...new Set((chart.cellIDs as string[]).map(id => id.trim()).filter(Boolean))].flatMap(id => {
            const row = byID.get(id)
            return row ? [row] : []
        }) }
    })
}

/** The saved process version used by the plan takes priority over the template library. */
export function reportProcesses(templates: ProcessTemplate[], used: ProcessTemplate[], scope: { instanceID: string; stationSchemeID: string }) {
    const seen = new Set<string>()
    return [...used, ...templates].filter(template => {
        if (template.instanceID !== scope.instanceID || template.stationSchemeID !== scope.stationSchemeID) return false
        const key = JSON.stringify([template.id, template.revision])
        if (seen.has(key)) return false
        seen.add(key)
        return true
    })
}

export function reportFilename(scheme: string, plan: string, date: Date) {
    const part = (value: string) => value.replace(/[<>:"/\\|?*\u0000-\u001f]/g, '_').replace(/[. ]+$/g, '').slice(0, 60) || '未命名'
    const stamp = [date.getFullYear(), String(date.getMonth() + 1).padStart(2, '0'), String(date.getDate()).padStart(2, '0')].join('')
    return `${part(scheme)}_${part(plan)}_车站通过能力分析报告_${stamp}.docx`
}

export function downloadWordReport(blob: Blob, filename: string) {
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = filename
    document.body.append(link)
    try { link.click() } finally {
        link.remove()
        // Allow the browser to consume the download before releasing the object URL.
        window.setTimeout(() => URL.revokeObjectURL(url), 30000)
    }
}
