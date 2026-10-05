import type { StationPlanAxisRow, StationPlanTrack } from '../components/stationPlanView'
import type { StationPlanReportLabels } from './svgSnapshot'
import type { GanttReportLabels } from './ganttSvgSnapshot'
import type { ProcessAnchor, ProcessCatalog } from '../operationProcess'

/** Remove generated ID decorations only; never replace substrings in business names or numbers. */
export function reportObjectName(value: string | null | undefined, id: string, fallback = '名称未提供'): string {
    let name = value?.trim() || ''
    if (id) {
        for (const suffix of [`(${id})`, `（${id}）`, `[${id}]`]) {
            if (name.endsWith(suffix)) name = name.slice(0, -suffix.length).trim()
        }
        if (name === id) name = ''
    }
    return name || fallback
}

interface NamedTrain { id: string; trainNumber: string; name: string }
export function reportTrainName(train: NamedTrain | undefined): string {
    // A train number is reader-facing business data, even if it equals an internal key.
    return train?.trainNumber.trim() || reportObjectName(train?.name, train?.id || '', '未命名列车')
}

export function reportAnchorName(anchor: ProcessAnchor | undefined, catalog: ProcessCatalog): string {
    if (!anchor) return '锚名称未提供'
    if ([`${anchor.trackID}锚`, `轨道 ${anchor.trackID}锚`, `Track ${anchor.trackID} anchor`, `${anchor.trackID} anchor`].includes(anchor.name.trim())) {
        const track = catalog.tracks.find(track => track.id === anchor.trackID)
        const trackName = reportObjectName(track?.name, anchor.trackID, '')
        return trackName ? `${trackName}锚` : '锚名称未提供'
    }
    return reportObjectName(anchor.name, anchor.id, '锚名称未提供')
}

export function stationReportLabels(rows: StationPlanAxisRow[], tracks: StationPlanTrack[], trains: NamedTrain[]): StationPlanReportLabels {
    const labels: Record<string, string> = {}
    const visit = (row: StationPlanAxisRow) => {
        const track = row.kind === 'track' ? tracks.find(track => track.id === row.sourceID) : undefined
        let name = track?.name || row.label
        if (row.kind === 'track') {
            const ids = row.nodeIDs || row.children?.map(child => child.sourceID) || []
            for (const suffix of [`（${ids.join(',')}）`, `(${ids.join(',')})`]) {
                if (ids.length && name.endsWith(suffix)) name = name.slice(0, -suffix.length).trim()
            }
        }
        labels[row.key] = reportObjectName(name, row.sourceID, row.kind === 'track' ? '股道名称未提供' : '节点名称未提供')
        row.children?.forEach(visit)
    }
    rows.forEach(visit)
    return { rows: labels, trains: Object.fromEntries(trains.map(train => [train.id, reportTrainName(train)])) }
}

interface NamedOccupancyRow {
    cellID: string
    cellName: string
    bars: { key: string; trainID: string; trainNumber: string; movementID: string; movementName: string; routeID: string; routeName: string }[]
}
export function occupancyReportLabels(rows: NamedOccupancyRow[], trains: NamedTrain[]): GanttReportLabels {
    const byID = new Map(trains.map(train => [train.id, train]))
    return {
        rows: Object.fromEntries(rows.map(row => [row.cellID, reportObjectName(row.cellName, row.cellID, '资源名称未提供')])),
        blocks: Object.fromEntries(rows.flatMap(row => row.bars.map(bar => {
            const trainName = bar.trainNumber.trim() || reportTrainName(byID.get(bar.trainID))
            const routeName = reportObjectName(bar.routeName, bar.routeID, '')
            const movementName = reportObjectName(bar.movementName, bar.movementID, '作业名称未提供')
            return [bar.key, `${trainName}-${reportObjectName(routeName, bar.movementID, movementName)}`]
        }))),
    }
}
