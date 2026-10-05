import type { ActivityType, PrecedenceEndpoint, ProcessActivity, ProcessCatalog, ProcessEvent, ProcessTemplate } from '../operationProcess.ts'
import { dwellingTrackNames } from '../operationProcess.ts'

export type ProcessObjectKind = 'activity' | 'event' | 'precedence' | 'anchor'
export type ProcessSelection = { kind: ProcessObjectKind | 'template'; id: string }
export type ProcessEventSide = 'start' | 'end' | 'standalone'
type EventSide = ProcessEventSide
type GraphPort = { x: number; y: number; bottom: number; side: 'left' | 'right' }
export const CARD_WIDTH = 174
export const CARD_HEIGHT = 86
export const CANDIDATE_LINE_HEIGHT = 18
export const processTypeColors: Record<ActivityType, { fill: string; border: string; ink: string }> = {
    Arrival: { fill: '#edf5ff', border: '#a4c5ee', ink: '#3374c8' },
    Departure: { fill: '#eef9f4', border: '#a5d6bf', ink: '#268263' },
    Shunting: { fill: '#fff7e9', border: '#e7c992', ink: '#ad7b24' },
    Locomotive: { fill: '#f4f0ff', border: '#c6b6eb', ink: '#8360c1' },
    Dwelling: { fill: '#f0f5fa', border: '#b9c8d8', ink: '#657e98' },
}

/** Geometry used by both the interactive editor and its exported SVG. */
export function createProcessDiagram(model: ProcessTemplate, catalog: ProcessCatalog, english = false) {
    const ui = (zh: string, en: string) => english ? en : zh
    const routeName = (id: string) => catalog.routes.find(item => item.id === id)?.name || (id ? ui('进路不可用', 'Route unavailable') : ui('未选择进路', 'No route selected'))
    const candidateNames = (activity: ProcessActivity) => activity.type === 'Dwelling' ? dwellingTrackNames(activity, catalog) : activity.routeList.map(routeName)
    const activityCardLabels = new Map((model.activities || [])
        .map(activity => {
            const text = `${activity.type === 'Dwelling' ? ui('备选股道', 'Tracks') : ui('备选进路', 'Routes')}：${candidateNames(activity).join('、') || ui('未设置', 'Not set')}`
            // Thirteen full-width characters fit within the 144px card content area.
            const characters = Array.from(text)
            const lines: string[] = []
            for (let index = 0; index < characters.length; index += 13) lines.push(characters.slice(index, index + 13).join(''))
            return [activity.id, { text, lines }] as const
        }))
    function activityHeight(activity: ProcessActivity) { return CARD_HEIGHT + (activityCardLabels.get(activity.id)?.lines.length || 0) * CANDIDATE_LINE_HEIGHT }
    const standaloneEvents = model.events.filter(event => !model.activities.some(activity => activity.startEvent === event.id || activity.endEvent === event.id))
    const standaloneStartY = Math.max(160, ...(model.activities.map(item => item.y + activityHeight(item) + 150) || []))
    const canvasWidth = Math.max(1000, ...(model.activities.map(item => item.x + CARD_WIDTH + 110) || []))
    const canvasHeight = Math.max(390, standaloneStartY + (standaloneEvents.length ? Math.ceil(standaloneEvents.length / 5) * 125 : 0))
    const eventPositions = (() => {
        const positions = new Map<string, { x: number; y: number }>()
        for (const activity of model.activities || []) {
            if (!positions.has(activity.startEvent)) positions.set(activity.startEvent, { x: activity.x, y: activity.y + activityHeight(activity) / 2 })
            if (!positions.has(activity.endEvent)) positions.set(activity.endEvent, { x: activity.x + CARD_WIDTH, y: activity.y + activityHeight(activity) / 2 })
        }
        standaloneEvents.forEach((event, index) => positions.set(event.id, { x: 85 + index % 5 * 180, y: standaloneStartY + Math.floor(index / 5) * 125 }))
        return positions
    })()
    function eventPosition(id: string) { return eventPositions.get(id) || { x: 40, y: 100 } }
    const renderedEvents = (() => {
        const points: { event: ProcessEvent; x: number; y: number; top: number; bottom: number; key: string; activityID: string | null; side: EventSide }[] = []
        const events = new Map(model.events.map(event => [event.id, event]) || [])
        for (const activity of model.activities || []) {
            const start = events.get(activity.startEvent), end = events.get(activity.endEvent)
            const height = activityHeight(activity)
            const bounds = { y: activity.y + height / 2, top: activity.y, bottom: activity.y + height }
            if (start) points.push({ event: start, x: activity.x, ...bounds, key: `${activity.id}-start`, activityID: activity.id, side: 'start' })
            if (end) points.push({ event: end, x: activity.x + CARD_WIDTH, ...bounds, key: `${activity.id}-end`, activityID: activity.id, side: 'end' })
        }
        for (const event of standaloneEvents) {
            const position = eventPosition(event.id)
            points.push({ event, ...position, top: position.y - CARD_HEIGHT / 2, bottom: position.y + CARD_HEIGHT / 2, key: event.id, activityID: null, side: 'standalone' })
        }
        return points
    })()
    function graphPort(eventID: string, preferredSide: 'left' | 'right' = 'left'): GraphPort {
        // Shared events use an end port for the source and a start port for the target.
        const starting = model.activities.find(activity => activity.startEvent === eventID)
        const ending = model.activities.find(activity => activity.endEvent === eventID)
        const useEnd = preferredSide === 'right' ? !!ending : !starting && !!ending
        const activity = useEnd ? ending : starting
        if (activity) return { x: activity.x + (useEnd ? CARD_WIDTH : 0), y: activity.y + activityHeight(activity) / 2, bottom: activity.y + activityHeight(activity), side: useEnd ? 'right' : 'left' }
        const position = eventPosition(eventID)
        return { ...position, bottom: position.y + CARD_HEIGHT / 2, side: preferredSide }
    }
    function precedenceGeometry(start: GraphPort, end: GraphPort) {
        const sourceOnLeft = start.side === 'left'
        const targetOnRight = end.side === 'right'
        const sx = start.x + (sourceOnLeft ? -9 : 9)
        const ex = end.x + (targetOnRight ? 11 : -11)
        let path: string, labelX: number, labelY: number
        if (!sourceOnLeft && !targetOnRight && end.x - start.x >= 60) {
            const reach = Math.max(30, (end.x - start.x) / 2)
            path = `M ${sx} ${start.y} C ${sx + reach} ${start.y}, ${ex - reach} ${end.y}, ${ex} ${end.y}`
            labelX = (sx + ex) / 2
            labelY = (start.y + end.y) / 2
        } else if (!sourceOnLeft && targetOnRight && Math.abs(start.y - end.y) > 80) {
            const rail = Math.max(start.x, end.x) + 67
            path = `M ${sx} ${start.y} H ${rail - 10} Q ${rail} ${start.y} ${rail} ${start.y + (end.y > start.y ? 10 : -10)} V ${end.y + (end.y > start.y ? -10 : 10)} Q ${rail} ${end.y} ${rail - 10} ${end.y} H ${ex}`
            labelX = rail
            labelY = (start.y + end.y) / 2
        } else {
            const sourceRail = sourceOnLeft ? Math.max(12, start.x - 53) : start.x + 37
            const targetRail = targetOnRight ? end.x + 53 : Math.max(12, end.x - 53)
            const betweenRows = Math.abs(start.y - end.y) > 160
            const bendY = betweenRows ? (start.y + end.y) / 2 : Math.max(start.bottom, end.bottom) + 69
            path = `M ${sx} ${start.y} H ${sourceRail} V ${bendY} H ${targetRail} V ${end.y} H ${ex}`
            labelX = (sourceRail + targetRail) / 2
            labelY = bendY
        }
        return { path, labelX, labelY }
    }
    const graphEdges = model.precedences.map(item => ({ item, ...precedenceGeometry(graphPort(item.leadingEvent, 'right'), graphPort(item.followingEvent)) }))
    const precedencePreview = (source: PrecedenceEndpoint | null, target: PrecedenceEndpoint | null) => source && target
        ? precedenceGeometry(graphPort(source.eventID, 'right'), graphPort(target.eventID)) : null
    return { activityCardLabels, activityHeight, canvasWidth, canvasHeight, renderedEvents, graphEdges, precedencePreview }
}
