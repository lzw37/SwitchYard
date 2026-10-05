import type { ProcessCatalog, ProcessTemplate } from '../operationProcess'
import type { StationPlanAxisRow, StationPlanTrack, StationPlanTrain } from '../components/stationPlanView'
import type { TrackOccupancyGanttRow, TrackOccupancyGanttTick } from '../components/trackOccupancyGantt'
import type { StationPlanReportViewState, StationPlanReportLabels } from './svgSnapshot'
import type { GanttReportViewState, GanttReportLabels } from './ganttSvgSnapshot'
import type { ReportFigure } from './wordReport'
import type { RenderedReportFigures } from './stationCapacityReport'

export interface ReportRenderJob {
    processes: ProcessTemplate[]
    catalog: ProcessCatalog
    currentProcess?: { model: ProcessTemplate; catalog: ProcessCatalog; figure: ReportFigure | null; zoom: number } | null
    stationPlans: {
        name: string
        rows: StationPlanAxisRow[]
        trains: StationPlanTrain[]
        tracks: StationPlanTrack[]
        startMinutes: number | null
        endMinutes: number | null
        view: StationPlanReportViewState
        labels?: StationPlanReportLabels
        figure?: ReportFigure | null
    }[]
    occupancies: {
        name: string
        rows: TrackOccupancyGanttRow[]
        ticks: TrackOccupancyGanttTick[]
        timelineWidth: number
        disabled?: boolean
        view: GanttReportViewState
        labels?: GanttReportLabels
        figure?: ReportFigure | null
    }[]
    cellAxisLabel: string
    timeAxisLabel: string
}

export interface ReportFigureRenderer {
    render: () => Promise<RenderedReportFigures>
}
