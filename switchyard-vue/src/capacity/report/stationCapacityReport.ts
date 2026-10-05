import type { ProcessCatalog, ProcessTemplate } from '../operationProcess'
import type { StationPlanAxisRow, StationPlanTrain } from '../components/stationPlanView'
import type { WordReport, ReportBlock, ReportFigure } from './wordReport'
import { reportAnchorName, reportObjectName as objectName } from './reportNames'

/** Captured from the same Vue components that draw the application views. */
export interface RenderedReportFigures {
    processes: Record<string, ReportFigure>
    stationPlans: ReportFigure[]
    occupancies: ReportFigure[]
}

export function processReportFigureKey(process: Pick<ProcessTemplate, 'id' | 'revision'>) {
    return JSON.stringify([process.id, process.revision])
}

export interface ReportOccupancyRow {
    cellID: string
    cellName: string
    laneCount: number
    bars: {
        startMinutes: number
        endMinutes: number
        lane: number
        label: string
        color: string
        isInterruptCell: boolean
    }[]
}

export interface ReportOccupationRow {
    rowKey: string
    rowType: 'group' | 'route' | 'fixed-total' | 'total' | 'utilization'
    sequence: number | string
    routeID: string
    routeName: string
    operationCount: number | string
    cellDurations: Record<string, number>
    interruptCellDurations: Record<string, number>
    isFixedOperation?: boolean
    children?: ReportOccupationRow[]
}

export interface ReportBottleneckRow {
    routeID: string
    routeName: string
    operationCount: number
    bottleneckCellID: string
    bottleneckCellName: string
    bottleneckUtilization: number | null
    throughputCapacity: number | null
}

export interface ReportSummaryRow {
    categoryID: string
    groupKey: string
    groupText: string
    routeIDs: string[]
    routeCount: number
    operationCount: number
    capacityTotal: number | null
    capacityAverage: number | null
}

export interface StationCapacityReportInput {
    schemeName: string
    planName: string
    instanceID: string
    stationSchemeID: string
    operationPlanID: string
    generatedAt: string
    startMinutes: number
    endMinutes: number
    totalTimeSeconds: number | null
    emptyWasteFactor: number | null
    snapshotDate?: string
    snapshot: boolean
    warnings: string[]
    trains: { id: string; trainNumber: string; name: string; trainType: string; isFixedOperation: boolean }[]
    movements: { trainID: string; movementID: string; name: string; earliestStartTime: string; latestEndTime: string; route: string; routeIDList: string }[]
    processes: ProcessTemplate[]
    catalog?: ProcessCatalog
    stationCharts: { name: string; rows: StationPlanAxisRow[] }[]
    stationTrains: StationPlanTrain[]
    occupancyCharts: { name: string; rows: ReportOccupancyRow[] }[]
    cells: { id: string; name: string }[]
    occupationRows: ReportOccupationRow[]
    bottleneckRows: ReportBottleneckRow[]
    summaryRows: ReportSummaryRow[]
    occupationTables: { name: string; cellIds: string[] }[]
    renderedFigures?: RenderedReportFigures
}

const activityNames: Record<string, string> = {
    Arrival: '接车', Departure: '发车', Shunting: '调车', Locomotive: '机车出入段', Dwelling: '停留',
}
const missing = '—'
const finite = (value: unknown): value is number => typeof value === 'number' && Number.isFinite(value)
const number = (value: number | null | undefined, digits = 2) => finite(value)
    ? value.toLocaleString('zh-CN', { maximumFractionDigits: digits, useGrouping: false }) : missing
const percent = (value: number | null | undefined) => finite(value) ? `${number(value * 100)}%` : missing
const label = (value: string | null | undefined) => value?.trim() || missing
function routeReferences(value: string): string[] {
    return [...new Set(value.split(/[,，;；\n\r]+/).map(item => item.trim()).filter(Boolean))]
}

/** Keep negative and cross-day times intact, including second precision. */
function time(minutes: number): string {
    if (!finite(minutes)) return missing
    const total = Math.round(minutes * 60)
    const day = Math.floor(total / 86400)
    const remainder = total - day * 86400
    const hours = Math.floor(remainder / 3600)
    const mins = Math.floor(remainder % 3600 / 60)
    const seconds = remainder % 60
    return `${day ? `D${day > 0 ? '+' : ''}${day} ` : ''}${String(hours).padStart(2, '0')}:${String(mins).padStart(2, '0')}${seconds ? `:${String(seconds).padStart(2, '0')}` : ''}`
}

/** Group parents are presentation summaries, never additional occupation records. */
function leafOccupationRows(rows: ReportOccupationRow[], fixed?: boolean): ReportOccupationRow[] {
    return rows.flatMap(row => {
        const isFixed = row.isFixedOperation ?? fixed
        if (row.rowType === 'group') return leafOccupationRows(row.children || [], isFixed)
        return [{ ...row, isFixedOperation: row.rowType === 'fixed-total' ? true : isFixed }]
    })
}

function rowKind(row: ReportOccupationRow): string {
    if (row.rowType === 'fixed-total') return '固定作业小计'
    if (row.rowType === 'total') return '全部占用合计'
    if (row.rowType === 'utilization') return '利用率 K'
    return row.isFixedOperation === undefined ? '未标注' : row.isFixedOperation ? '固定作业' : '非固定作业'
}

export function buildStationCapacityReport(input: StationCapacityReportInput): WordReport {
    const blocks: ReportBlock[] = []
    let figureNumber = 0
    let tableNumber = 0
    const paragraph = (text: string) => blocks.push({ kind: 'paragraph', text })
    const heading = (text: string, level: 1 | 2 = 1) => blocks.push({ kind: 'heading', text, level })
    const table = (caption: string, headers: string[], rows: string[][], widths?: number[]) => {
        if (!rows.length) return
        blocks.push({ kind: 'paragraph', text: `表 ${++tableNumber}　${caption}`, keepNext: true })
        blocks.push({ kind: 'table', headers, rows, ...(widths ? { widths } : {}) })
    }
    const figures = (items: ReportFigure[]) => items.forEach(figure => blocks.push({
        kind: 'figure', figure: { ...figure, caption: `图 ${++figureNumber}　${figure.caption}` },
    }))
    const rows = leafOccupationRows(input.occupationRows)
    const routeRows = rows.filter(row => row.rowType === 'route')
    const utilization = rows.find(row => row.rowType === 'utilization')
    const total = rows.find(row => row.rowType === 'total')
    const fixedTotal = rows.find(row => row.rowType === 'fixed-total')
    const trainMap = new Map(input.trains.map(train => [train.id, train]))
    const schemeName = objectName(input.schemeName, input.stationSchemeID, '未命名车站方案')
    const planName = objectName(input.planName, input.operationPlanID, '未命名作业计划')
    const routeNames = new Map<string, string>()
    for (const item of [...(input.catalog?.routes || []), ...routeRows.map(row => ({ id: row.routeID, name: row.routeName })), ...input.bottleneckRows.map(row => ({ id: row.routeID, name: row.routeName }))]) {
        const name = objectName(item.name, item.id, '')
        if (name && !routeNames.has(item.id)) routeNames.set(item.id, name)
    }
    const routeName = (id: string, name?: string) => objectName(name, id, '') || routeNames.get(id) || '进路名称未提供'
    const trackNames = new Map(input.catalog?.tracks.map(track => [track.id, objectName(track.name, track.id, '轨道名称未提供')]) || [])
    const nodeNames = new Map(input.catalog?.nodes.map(node => [node.id, objectName(node.name, node.id, '节点名称未提供')]) || [])
    const trackName = (id: string) => trackNames.get(id) || '轨道名称未提供'
    const nodeName = (id: string) => nodeNames.get(id) || '节点名称未提供'
    const cellMap = new Map(input.cells.map(cell => [cell.id, objectName(cell.name, cell.id, '资源名称未提供')]))
    const cellName = (id: string, name?: string) => objectName(name, id, '') || cellMap.get(id) || '资源名称未提供'
    const cells = [...new Set([...input.cells.map(cell => cell.id), ...rows.flatMap(row => Object.keys(row.cellDurations))])]
        .filter(Boolean).map(id => ({ id, name: cellName(id) }))

    paragraph(`本报告分析“${schemeName}”车站方案下的“${planName}”作业计划，依照系统已生成的作业过程、计划图、资源占用表、瓶颈分析表及通过能力汇总表编制。报告包含当前方案与计划的全部已提供列车、作业和分析表行，不受页面搜索、选中列车或表格展开状态影响。`)
    table('报告范围与计算口径', ['项目', '内容'], [
        ['车站方案', schemeName],
        ['作业计划', planName],
        ['报告生成时间', label(input.generatedAt)],
        ['统计总时长 T', finite(input.totalTimeSeconds) ? `${number(input.totalTimeSeconds / 60)} 分钟（${number(input.totalTimeSeconds)} 秒）` : '未提供'],
        ['计划图时间窗口', `${time(input.startMinutes)}—${time(input.endMinutes)}`],
        ['空费系数 α', input.snapshot ? '历史快照未记录，无法核验' : finite(input.emptyWasteFactor) ? number(input.emptyWasteFactor, 4) : '未提供'],
        ['分析数据来源', input.snapshot ? `已保存分析快照；保存时间：${input.snapshotDate || '未记录'}` : '当前作业计划及页面计算结果'],
    ], [24, 76])
    paragraph('统计总时长用于利用率和能力计算；计划图时间窗口用于图形展示，两者含义不同。占用分析沿用系统已有结果，不按图形窗口重新裁剪占用时段。占用表统一以分钟表示，利用率以百分比表示，通过能力单位为“次/统计时段”。空值以“—”表示，不作为零处理。')
    if (input.snapshot) paragraph(`本报告的占用、瓶颈和能力汇总引用已保存快照${input.snapshotDate ? `（${input.snapshotDate}）` : ''}，并未按当前计划或当前空费系数重新计算。计划明细及过程模板可能与快照保存时间不同，阅读时应结合数据日期。`)
    const warnings = [...new Set(input.warnings.map(value => value.trim()).filter(Boolean))]
    if (warnings.length) {
        paragraph('数据完整性说明：')
        warnings.forEach(warning => paragraph(warning))
    }

    heading('一、车站作业的过程分析')
    if (!input.processes.length) {
        paragraph('当前没有可用的作业过程模板或计划采用的过程快照。本章保留数据缺失说明，无法据此推断活动衔接关系、时序约束或资源选择。已生成的列车作业仍在下一章完整列示。')
    } else {
        paragraph(`本章列示 ${input.processes.length} 个作业过程记录，包括提供给报告的共享模板及计划采用的过程版本。活动定义接车、停留、发车等作业环节，事件表示作业边界，次序约束限定事件之间的最小间隔。模板中的时间均为相对于模板起点的分钟数，不能直接视为实际列车时刻。不同版本分别保留，避免用较新的模板覆盖原计划依据。`)
        table('作业过程记录概览', ['过程名称', '版本', '活动数', '事件数', '次序约束数'], input.processes.map(process => [
            objectName(process.name, process.id, '未命名过程'), number(process.revision, 0), String(process.activities.length), String(process.events.length), String(process.precedences.length),
        ]))
        input.processes.forEach((process, index) => {
            const processName = objectName(process.name, process.id, '未命名过程')
            heading(`1.${index + 1} ${processName}（版本 ${number(process.revision, 0)}）`, 2)
            if (process.description.trim()) paragraph(process.description.trim())
            const names = new Map(process.events.map(event => [event.id, objectName(event.name, event.id, '未命名事件')]))
            const eventName = (id: string) => names.get(id) || '事件名称未提供'
            const anchorNames = new Map(process.anchors.map(anchor => [anchor.id, reportAnchorName(anchor, input.catalog || { nodes: [], tracks: [], routes: [] })]))
            const anchorName = (id: string) => anchorNames.get(id) || '锚名称未提供'
            paragraph(`该过程包含 ${process.activities.length} 项活动和 ${process.precedences.length} 项次序约束。活动图保留模板中的活动布局和衔接关系；资源引用展示对应的进路、轨道及锚名称。候选资源不等同于已选资源，未选定项明确标注。`)
            const processFigure = input.renderedFigures?.processes[processReportFigureKey(process)]
            if (processFigure) figures([processFigure])
            else paragraph(process.activities.length || process.events.length ? '当前未取得该作业过程的界面图形，活动与约束详见下表。' : '该过程没有活动和事件，暂无可绘制的过程图。')
            table(`${processName}活动定义`, ['活动名称', '类型', '时长范围（分）', '起点 → 终点事件', '资源引用'], process.activities.map(activity => [
                objectName(activity.name, activity.id, '未命名活动'), activityNames[activity.type] || activity.type,
                `${number(activity.minDuration)}—${number(activity.maxDuration)}`,
                `${eventName(activity.startEvent)} → ${eventName(activity.endEvent)}`,
                activity.type === 'Dwelling'
                    ? `轨道候选：${activity.trackList.map(trackName).join('、') || '无'}；已选：${activity.selectedTrack ? trackName(activity.selectedTrack) : '未选定'}`
                    : `进路候选：${activity.routeList.map(id => routeName(id)).join('、') || '无'}；已选：${activity.selectedRoute ? routeName(activity.selectedRoute) : '未选定'}`,
            ]), [23, 11, 13, 25, 28])
            table(`${processName}事件定义`, ['事件名称', '相对时刻（分）', '节点', '锚引用'], process.events.map(event => [
                eventName(event.id), number(event.time),
                event.nodeID ? nodeName(event.nodeID) : (event.nodeList?.length ? `候选：${event.nodeList.map(nodeName).join('、')}` : '未指定'),
                `候选：${event.anchorList.map(anchorName).join('、') || '无'}；已选：${event.selectedAnchor ? anchorName(event.selectedAnchor) : '未选定'}`,
            ]), [33, 15, 22, 30])
            if (process.precedences.length) table(`${processName}时序约束`, ['前序事件', '后序事件', '最小间隔（分）'], process.precedences.map(item => [
                eventName(item.leadingEvent), eventName(item.followingEvent), number(item.interval),
            ]))
            else paragraph('该过程未设置显式次序约束；活动起止事件关系见活动表。')
            if (process.anchors.length) table(`${processName}锚与轨道对应`, ['锚名称', '轨道名称'], process.anchors.map(anchor => [anchorName(anchor.id), anchor.trackID ? trackName(anchor.trackID) : '未指定']))
        })
    }

    heading('二、车站作业计划')
    const fixedTrainCount = input.trains.filter(train => train.isFixedOperation).length
    paragraph(`当前计划共列示 ${input.trains.length} 列车、${input.movements.length} 项作业，其中固定作业列车 ${fixedTrainCount} 列，非固定作业列车 ${input.trains.length - fixedTrainCount} 列。以下计划图按已配置的节点或轨道范围排列，列车和作业明细保留全部记录。图内缺少可解析端点的作业不能形成有效轨迹，其原始记录仍保留在作业明细中。`)
    if (input.renderedFigures?.stationPlans.length) {
        figures(input.renderedFigures.stationPlans)
        paragraph('计划图直接采用界面组件生成的 SVG 图形，保留界面的节点顺序、轨道展开状态、时间网格、线型、颜色和文字样式；文档中按页面宽度等比缩放。')
    } else paragraph('当前没有可绘制的车站计划轨迹或计划图配置；以下以已有列车、作业明细反映计划内容。')
    table('列车计划明细', ['车次', '名称', '类型', '作业类别', '作业数'], input.trains.map(train => [
        label(train.trainNumber), objectName(train.name, train.id, '未命名列车'), label(train.trainType), train.isFixedOperation ? '固定作业' : '非固定作业',
        String(input.movements.filter(movement => movement.trainID === train.id).length),
    ]))
    if (!input.trains.length) paragraph('没有列车明细数据。')
    table('列车作业明细', ['车次', '作业名称', '开始时刻', '结束时刻', '进路或候选进路'], input.movements.map(movement => [
        trainMap.get(movement.trainID)?.trainNumber.trim() || objectName(trainMap.get(movement.trainID)?.name, movement.trainID, '列车名称未提供'),
        objectName(movement.name, movement.movementID, '未命名作业'), label(movement.earliestStartTime), label(movement.latestEndTime),
        movement.route.trim() ? routeName(movement.route.trim()) : (routeReferences(movement.routeIDList).length ? `未选定；候选：${routeReferences(movement.routeIDList).map(id => routeName(id)).join('、')}` : '未指定'),
    ]), [19, 29, 13, 13, 26])
    if (!input.movements.length) paragraph('没有作业明细数据。')

    heading('三、车站作业占用分析')
    paragraph(`占用分析覆盖 ${cells.length} 个资源单元及 ${routeRows.length} 条按固定／非固定作业区分的进路统计记录。占用时间按系统各资源占用条的持续时长累计，重叠区间不做合并，因此不能将累计占用直接理解为无重叠的忙碌时长。干扰占用已包含在总占用中；表内括号另列干扰部分，不再次加总。`)
    if (!input.snapshot) paragraph('系统利用率口径为 K = 非固定占用时间 ÷ [(1−α) × (T−固定占用时间)]。固定作业先扣减可用时间，再按空费系数 α 折减；T 为统计总时长。报告直接引用页面利用率，未重新计算。K 大于 100% 表示该统计口径下需求超过有效可用时间，不单独构成具体时刻冲突的判定。')
    else paragraph('本章利用率引用历史快照，快照未记录空费系数，无法据当前参数复核分母。历史结果与本章展示的当前占用图可能来自不同时间，应以报告范围中的来源标注为准。')
    if (input.renderedFigures?.occupancies.length) figures(input.renderedFigures.occupancies)
    else paragraph('没有可导出的资源占用界面图形。')
    if (rows.length && cells.length) {
        const cellIDs = new Set(cells.map(cell => cell.id))
        const selectedIDs = new Set<string>()
        const tables = input.occupationTables.map(item => ({ name: item.name, cellIds: [...new Set(item.cellIds)].filter(id => cellIDs.has(id)) }))
        tables.forEach(item => item.cellIds.forEach(id => selectedIDs.add(id)))
        const remaining = cells.filter(cell => !selectedIDs.has(cell.id)).map(cell => cell.id)
        if (remaining.length) tables.push({ name: tables.length ? '其他资源占用明细' : '资源占用明细', cellIds: remaining })
        tables.forEach(item => {
            if (!item.cellIds.length) { paragraph(`“${item.name}”未选择有效资源列。`); return }
            for (let offset = 0; offset < item.cellIds.length; offset += 4) {
                const chunk = item.cellIds.slice(offset, offset + 4)
                table(`${item.name}${item.cellIds.length > 4 ? `（第 ${Math.floor(offset / 4) + 1} 组资源列）` : ''}；占用单位：分钟`,
                    ['作业类别', '进路 / 汇总项', '作业次数', ...chunk.map(id => cellName(id))],
                    rows.map(row => [rowKind(row), row.rowType === 'route' ? routeName(row.routeID, row.routeName) : label(row.routeName), String(row.operationCount ?? '') || missing,
                        ...chunk.map(id => {
                            const value = row.cellDurations[id]
                            if (row.rowType === 'utilization') return percent(value)
                            if (!finite(value)) return missing
                            const interrupt = row.interruptCellDurations[id]
                            return `${number(value / 60)}${finite(interrupt) && interrupt > 0 ? `（干扰 ${number(interrupt / 60)}）` : ''}`
                        })]), [15, 35, 10, ...chunk.map(() => 10)])
            }
        })
        const cellStats = cells.map(cell => ({ cell, total: total?.cellDurations[cell.id], fixed: fixedTotal?.cellDurations[cell.id], k: utilization?.cellDurations[cell.id] }))
        table('资源占用与利用率汇总', ['资源单元', '占用合计（分）', '固定占用（分）', '利用率 K'], cellStats.map(item => [
            item.cell.name, finite(item.total) ? number(item.total / 60) : missing,
            finite(item.fixed) ? number(item.fixed / 60) : missing, percent(item.k),
        ]))
        const valid = cellStats.filter(item => finite(item.k)).sort((left, right) => right.k! - left.k!)
        const maximum = valid[0]
        if (maximum) paragraph(`已提供利用率的资源中，${maximum.cell.name}最高，为 ${percent(maximum.k)}。其中 ${valid.filter(item => item.k! > 1).length} 个资源的 K 超过 100%。建议结合相应进路、固定作业占用及同一时间段内的活动衔接，定位造成集中占用的作业。`)
        else paragraph('当前占用表没有可用的利用率，无法进行资源负荷排序。')
    } else paragraph('当前没有可用的资源占用统计表，无法输出占用汇总或判断资源利用率。')

    heading('四、车站作业瓶颈分析')
    paragraph('系统将每条进路涉及资源中利用率 K 最大的单元识别为该进路的瓶颈，按“进路作业次数 ÷ 瓶颈利用率”给出通过能力。该结果反映既定作业结构、统计时长和资源口径下的测算值；没有有效利用率的进路保留空值。报告不将缺失能力推定为零，也不据此宣称无限能力。')
    if (input.bottleneckRows.length) {
        table('进路瓶颈与通过能力', ['进路名称', '作业次数', '瓶颈资源', '瓶颈 K', '能力（次/统计时段）'], input.bottleneckRows.map(row => [
            routeName(row.routeID, row.routeName), number(row.operationCount, 0), row.bottleneckCellID || row.bottleneckCellName ? cellName(row.bottleneckCellID, row.bottleneckCellName) : missing,
            percent(row.bottleneckUtilization), number(row.throughputCapacity),
        ]), [32, 10, 27, 13, 18])
        const ranked = input.bottleneckRows.filter(row => finite(row.bottleneckUtilization)).sort((left, right) => right.bottleneckUtilization! - left.bottleneckUtilization!)
        const highest = ranked[0]
        if (highest) paragraph(`瓶颈利用率最高的已列进路为 ${routeName(highest.routeID, highest.routeName)}，其瓶颈资源为 ${highest.bottleneckCellID || highest.bottleneckCellName ? cellName(highest.bottleneckCellID, highest.bottleneckCellName) : '未标注'}，K 为 ${percent(highest.bottleneckUtilization)}，测算通过能力为 ${finite(highest.throughputCapacity) ? `${number(highest.throughputCapacity)} 次/统计时段` : '未提供'}。优先核对该资源上的作业分配、固定占用与时序安排，可为后续方案比较提供依据。`)
        const unknownCount = input.bottleneckRows.filter(row => !finite(row.throughputCapacity)).length
        if (unknownCount) paragraph(`共有 ${unknownCount} 条进路未提供有效通过能力，原因需结合占用与利用率数据核查。`)
    } else paragraph('当前没有进路瓶颈分析结果，无法识别限制通过能力的资源或给出进路能力测算。')

    heading('五、车站通过能力汇总')
    paragraph('下表依照系统已配置的进路分类汇总。分类能力合计为组内已知进路能力之和，平均值为组内已知能力的平均；缺少能力的进路不视为零。不同进路可能共用资源，同一进路也可能属于多个分类，因此类别合计或跨类别合计不能直接解释为全站能够独立实现的通过能力。')
    if (input.summaryRows.length) {
        table('分类通过能力汇总', ['分类名称', '包含进路', '进路数', '作业次数', '能力合计（次/统计时段）', '能力均值（次/统计时段）'], input.summaryRows.map(row => [
            objectName(objectName(row.groupText, row.categoryID, ''), row.groupKey, '未命名分类'), row.routeIDs.map(id => routeName(id)).join('、') || missing, number(row.routeCount, 0), number(row.operationCount, 0),
            number(row.capacityTotal), number(row.capacityAverage),
        ]), [18, 28, 9, 11, 17, 17])
        const membership = new Map<string, number>()
        input.summaryRows.forEach(row => new Set(row.routeIDs).forEach(id => membership.set(id, (membership.get(id) || 0) + 1)))
        const repeated = [...membership].filter(([, count]) => count > 1).map(([id]) => id)
        if (repeated.length) paragraph(`以下进路出现在多个分类中：${repeated.map(id => routeName(id)).join('、')}。分类间存在重复归属，报告保留原分类结果，不再相加形成全站总数。`)
        const ungrouped = input.bottleneckRows.filter(row => !membership.has(row.routeID))
        if (ungrouped.length) paragraph(`另有 ${ungrouped.length} 条已分析进路尚未归入汇总分类：${ungrouped.map(row => routeName(row.routeID, row.routeName)).join('、')}。这些进路的结果见上一章。`)
    } else paragraph('当前尚无通过能力汇总分类或已保存的分类结果。本报告保留上一章的进路能力，不自行创建分类或虚构全站总能力。')
    paragraph(input.snapshot
        ? '后续方案评估应先确认计划明细、资源占用和分析快照属于同一版本，再更新计算并比较相同统计时段下的瓶颈与能力变化。'
        : '后续方案评估可围绕高利用率资源调整作业时序或资源分配，并在相同统计时段、空费系数和固定作业口径下重新生成分析。改善效果应同时比较资源占用、瓶颈位置和分类能力，避免仅凭单项能力合计作判断。')

    return { title: '车站通过能力分析报告', subtitle: `${schemeName} · ${planName}`, generatedAt: input.generatedAt, blocks }
}
