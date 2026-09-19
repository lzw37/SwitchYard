import axios from 'axios'

/** Durations and event times are in minutes; time is relative to the template origin. */
export const activityTypes = ['Arrival', 'Departure', 'Shunting', 'Locomotive', 'Dwelling'] as const
export type ActivityType = typeof activityTypes[number]
export const activityLabels: Record<ActivityType, string> = {
    Arrival: '接车', Departure: '发车', Shunting: '调车', Locomotive: '机车出入段', Dwelling: '停留',
}
export interface ProcessScope { instanceID: string; stationSchemeID: string; operationPlanID: string }
export interface ProcessActivity {
    id: string; name: string; type: ActivityType; minDuration: number; maxDuration: number
    startEvent: string; endEvent: string; routeList: string[]; selectedRoute: string | null
    trackList: string[]; selectedTrack: string | null; x: number; y: number
}
export interface ProcessEvent {
    id: string; name: string; time: number | null; nodeID: string | null
    /** Derived route endpoint candidates; absent in templates saved before this field existed. */
    nodeList?: string[]
    anchorList: string[]; selectedAnchor: string | null
}
export interface ProcessPrecedence { id: string; leadingEvent: string; followingEvent: string; interval: number }
export interface PrecedenceEndpoint { activityID: string; eventID: string }
export interface ProcessAnchor { id: string; name: string; trackID: string }
export interface ProcessRouteAnchors { routeID: string; startAnchor: string | null; endAnchor: string | null }
export interface ProcessTemplate extends ProcessScope {
    id: string; name: string; description: string; revision: number
    activities: ProcessActivity[]; events: ProcessEvent[]; precedences: ProcessPrecedence[]
    anchors: ProcessAnchor[]; routeAnchors: ProcessRouteAnchors[]
}
export interface ProcessCatalog {
    nodes: { id: string; name: string }[]
    /** Existing station Link objects are the track references in this initial module. */
    tracks: { id: string; name: string; fromNodeID: string; toNodeID: string }[]
    routes: { id: string; name: string; type: string; startNodeID: string | null; endNodeID: string | null; trackIDs: string[] }[]
}
/** Candidate precedence endpoints are existing activity ends; graph validation handles cycles. */
export function getPrecedenceEndCandidates(template: ProcessTemplate, leadingActivityID?: string): PrecedenceEndpoint[] {
    const leading = leadingActivityID === undefined ? undefined : template.activities.find(activity => activity.id === leadingActivityID)
    if (leadingActivityID !== undefined && !leading) return []
    const eventIDs = new Set(template.events.map(event => event.id))
    const candidates: PrecedenceEndpoint[] = []
    for (const activity of template.activities) {
        if (!activity.endEvent || !eventIDs.has(activity.endEvent)) continue
        if (leading && (activity.id === leading.id || activity.endEvent === leading.endEvent)) continue
        candidates.push({ activityID: activity.id, eventID: activity.endEvent })
    }
    return candidates
}
/** After selecting an activity end, offer other activities' existing starts as targets. */
export function getPrecedenceStartCandidates(template: ProcessTemplate, leadingActivityID: string): PrecedenceEndpoint[] {
    const leading = template.activities.find(activity => activity.id === leadingActivityID)
    const eventIDs = new Set(template.events.map(event => event.id))
    if (!leading || !leading.endEvent || !eventIDs.has(leading.endEvent)) return []
    const candidates: PrecedenceEndpoint[] = []
    for (const activity of template.activities) {
        if (activity.id === leading.id || !activity.startEvent || !eventIDs.has(activity.startEvent)) continue
        if (activity.startEvent === leading.endEvent) continue
        candidates.push({ activityID: activity.id, eventID: activity.startEvent })
    }
    return candidates
}
/** Derive every event's possible locations from compatible movement route candidates only. */
export function deriveEventNodeLists(template: ProcessTemplate, catalog: ProcessCatalog): Map<string, string[]> {
    const nodes = new Set(catalog.nodes.map(node => node.id))
    const routes = new Map(catalog.routes.map(route => [route.id, route]))
    const candidates = new Map(template.events.map(event => [event.id, new Set<string>()]))
    function add(eventID: string, nodeID: string | null) {
        if (nodeID && nodes.has(nodeID)) candidates.get(eventID)?.add(nodeID)
    }
    for (const activity of template.activities) {
        if (activity.type === 'Dwelling') continue
        for (const routeID of activity.routeList) {
            const route = routes.get(routeID)
            if (!route || route.type.toLowerCase() !== activity.type.toLowerCase()) continue
            add(activity.startEvent, route.startNodeID)
            add(activity.endEvent, route.endNodeID)
        }
    }
    return new Map([...candidates].map(([eventID, nodeIDs]) => [eventID, [...nodeIDs]]))
}
/** Materialize derived locations for persistence without rewriting unchanged event arrays. */
export function syncEventNodeLists(template: ProcessTemplate, catalog: ProcessCatalog): boolean {
    const derived = deriveEventNodeLists(template, catalog)
    let changed = false
    for (const event of template.events) {
        const nodeList = derived.get(event.id) || []
        if (!event.nodeList || event.nodeList.length !== nodeList.length || event.nodeList.some((id, index) => id !== nodeList[index])) {
            event.nodeList = nodeList
            changed = true
        }
    }
    return changed
}
/** Only genuine station track names qualify for dwelling candidates; display fallbacks do not. */
export function namedTracks(catalog: ProcessCatalog) {
    return catalog.tracks.filter(track => (track.name ?? '').trim().length > 0)
}
export function dwellingTrackNames(activity: ProcessActivity, catalog: ProcessCatalog): string[] {
    const names = new Map(namedTracks(catalog).map(track => [track.id, track.name]))
    return activity.trackList.filter(id => names.has(id)).map(id => names.get(id)!)
}
/** Reconcile only after a successful catalog load. The caller retains the saved snapshot. */
export function reconcileDwellingTracks(template: ProcessTemplate, catalog: ProcessCatalog): boolean {
    const allowed = new Set(namedTracks(catalog).map(track => track.id))
    let changed = false
    for (const activity of template.activities) {
        if (activity.type !== 'Dwelling') continue
        const candidates = activity.trackList.filter(id => allowed.has(id))
        if (candidates.length !== activity.trackList.length) {
            activity.trackList = candidates
            changed = true
        }
        if (activity.selectedTrack !== null && !candidates.includes(activity.selectedTrack)) {
            activity.selectedTrack = null
            changed = true
        }
    }
    return changed
}
export function makeID(prefix: string) { return `${prefix}-${crypto.randomUUID()}` }
export function createEmptyTemplate(scope: ProcessScope): ProcessTemplate {
    return { ...scope, id: makeID('process'), name: '新建作业过程模板', description: '', revision: 0,
        activities: [], events: [], precedences: [], anchors: [], routeAnchors: [] }
}

export const demoCatalog: ProcessCatalog = {
    nodes: [
        { id: 'N1', name: '西咽喉入口' }, { id: 'N2', name: 'Ⅰ道西端' },
        { id: 'N3', name: 'Ⅰ道东端' }, { id: 'N4', name: '东咽喉出口' },
        { id: 'N5', name: '机务段出口' }, { id: 'N6', name: '牵出线端' },
    ],
    tracks: [
        { id: 'T1', name: '西咽喉', fromNodeID: 'N1', toNodeID: 'N2' },
        { id: 'T2', name: 'Ⅰ道（到发线）', fromNodeID: 'N2', toNodeID: 'N3' },
        { id: 'T3', name: '东咽喉', fromNodeID: 'N3', toNodeID: 'N4' },
        { id: 'T4', name: '机车走行线', fromNodeID: 'N5', toNodeID: 'N6' },
        { id: 'T5', name: '牵出线', fromNodeID: 'N6', toNodeID: 'N2' },
    ],
    routes: [
        { id: 'R1', name: '西咽喉 → Ⅰ道接车', type: 'Arrival', startNodeID: 'N1', endNodeID: 'N2', trackIDs: ['T1', 'T2'] },
        { id: 'R2', name: 'Ⅰ道 → 东咽喉发车', type: 'Departure', startNodeID: 'N3', endNodeID: 'N4', trackIDs: ['T2', 'T3'] },
        { id: 'R3', name: '牵出线 → Ⅰ道调车', type: 'Shunting', startNodeID: 'N6', endNodeID: 'N2', trackIDs: ['T5', 'T2'] },
        { id: 'R4', name: '机务段 → 牵出线', type: 'Locomotive', startNodeID: 'N5', endNodeID: 'N6', trackIDs: ['T4'] },
    ],
}

/** Uses only references from the supplied station catalog. Never adds demo infrastructure to a real station. */
export function createExampleTemplate(scope: ProcessScope, catalog: ProcessCatalog): ProcessTemplate {
    const template = createEmptyTemplate(scope)
    template.name = '接车—停留—发车（含机车与调车协同）'
    template.description = '五类活动示例。时刻从模板起点起算，单位为分钟；次序间隔为最小间隔。可编辑后另存为当前作业计划的过程模板。'
    const anchorByTrack = new Map<string, string>()
    function anchorFor(trackID: string | undefined) {
        if (!trackID || !catalog.tracks.some(track => track.id === trackID)) return null
        if (!anchorByTrack.has(trackID)) {
            const id = makeID('anchor')
            const trackName = catalog.tracks.find(track => track.id === trackID)!.name?.trim() || `轨道 ${trackID}`
            template.anchors.push({ id, name: `${trackName}锚`, trackID })
            anchorByTrack.set(trackID, id)
        }
        return anchorByTrack.get(trackID)!
    }
    const specs: [ActivityType, string, number, number, number, number, number, number][] = [
        ['Arrival', '列车接入', 0, 6, 4, 8, 65, 85],
        ['Dwelling', '站内停留 / 技术作业', 6, 26, 15, 25, 395, 85],
        ['Departure', '列车发出', 28, 34, 4, 8, 725, 85],
        ['Locomotive', '机车出段', 6, 10, 3, 6, 65, 295],
        ['Shunting', '调车转线', 12, 20, 5, 10, 395, 295],
    ]
    for (const [type, name, start, end, minDuration, maxDuration, x, y] of specs) {
        const candidates = catalog.routes.filter(route => route.type.toLowerCase() === type.toLowerCase())
        const route = candidates[0]
        const arrival = catalog.routes.find(item => item.type.toLowerCase() === 'arrival')
        const dwellingTracks = namedTracks(catalog)
        const dwellTrack = dwellingTracks.find(track => track.id === arrival?.trackIDs[arrival.trackIDs.length - 1]) ?? dwellingTracks[0]
        const isDwell = type === 'Dwelling'
        const endpointTrack = (nodeID: string | null | undefined) => route?.trackIDs.find(id =>
            catalog.tracks.some(track => track.id === id && (track.fromNodeID === nodeID || track.toNodeID === nodeID)))
        const startAnchor = anchorFor(isDwell ? dwellTrack?.id : endpointTrack(route?.startNodeID))
        const endAnchor = anchorFor(isDwell ? dwellTrack?.id : endpointTrack(route?.endNodeID))
        const startEvent = makeID('event'), endEvent = makeID('event')
        template.events.push(
            { id: startEvent, name: `${name}开始`, time: start, nodeID: (isDwell ? dwellTrack?.fromNodeID : route?.startNodeID) || null,
                anchorList: startAnchor ? [startAnchor] : [], selectedAnchor: startAnchor },
            { id: endEvent, name: `${name}结束`, time: end, nodeID: (isDwell ? dwellTrack?.toNodeID : route?.endNodeID) || null,
                anchorList: endAnchor ? [endAnchor] : [], selectedAnchor: endAnchor },
        )
        template.activities.push({ id: makeID('activity'), name, type, minDuration, maxDuration,
            startEvent, endEvent, routeList: isDwell ? [] : candidates.map(item => item.id), selectedRoute: isDwell ? null : route?.id ?? null,
            trackList: isDwell && dwellTrack ? [dwellTrack.id] : [], selectedTrack: isDwell ? dwellTrack?.id ?? null : null, x, y })
        if (!isDwell && route && !template.routeAnchors.some(item => item.routeID === route.id)) {
            template.routeAnchors.push({ routeID: route.id, startAnchor, endAnchor })
        }
    }
    const [arrival, dwell, departure, locomotive, shunting] = template.activities
    if (arrival && dwell && departure && locomotive && shunting) {
        for (const [leadingEvent, followingEvent, interval] of [
            [arrival.endEvent, dwell.startEvent, 0], [dwell.endEvent, departure.startEvent, 2],
            [arrival.endEvent, locomotive.startEvent, 0], [locomotive.endEvent, shunting.startEvent, 2],
            [shunting.endEvent, dwell.endEvent, 6],
        ] as [string, string, number][]) template.precedences.push({ id: makeID('precedence'), leadingEvent, followingEvent, interval })
    }
    return template
}

/** Renaming an activity updates its existing endpoint labels without replacing their identities. */
export function renameActivity(template: ProcessTemplate, activityID: string, name: string): boolean {
    const activity = template.activities.find(item => item.id === activityID)
    if (!activity || activity.name === name) return false
    activity.name = name
    const start = template.events.find(item => item.id === activity.startEvent)
    const end = template.events.find(item => item.id === activity.endEvent)
    if (start) start.name = `${name}开始`
    if (end) end.name = `${name}结束`
    return true
}

export function removeActivity(template: ProcessTemplate, id: string) {
    const activity = template.activities.find(item => item.id === id)
    template.activities = template.activities.filter(item => item.id !== id)
    if (!activity) return
    for (const eventID of [activity.startEvent, activity.endEvent]) removeEvent(template, eventID)
}
export function removeEvent(template: ProcessTemplate, id: string): boolean {
    if (template.activities.some(item => item.startEvent === id || item.endEvent === id)) return false
    template.events = template.events.filter(item => item.id !== id)
    template.precedences = template.precedences.filter(item => item.leadingEvent !== id && item.followingEvent !== id)
    return true
}
export function removeAnchor(template: ProcessTemplate, id: string) {
    template.anchors = template.anchors.filter(item => item.id !== id)
    for (const event of template.events) {
        event.anchorList = event.anchorList.filter(anchor => anchor !== id)
        if (event.selectedAnchor === id) event.selectedAnchor = null
    }
    for (const route of template.routeAnchors) {
        if (route.startAnchor === id) route.startAnchor = null
        if (route.endAnchor === id) route.endAnchor = null
    }
}

export function validateTemplate(template: ProcessTemplate, catalog: ProcessCatalog): string[] {
    const errors: string[] = []
    if (!template.name.trim() || template.name.length > 200) errors.push('模板名称必须为 1–200 个字符。')
    if (template.description.length > 4000) errors.push('模板说明最多 4000 个字符。')
    const collections = [template.activities, template.events, template.precedences, template.anchors]
    for (const items of collections) {
        if (items.some(item => !item.id.trim()) || new Set(items.map(item => item.id)).size !== items.length) errors.push('对象 ID 不能为空或重复。')
    }
    const events = new Map(template.events.map(item => [item.id, item]))
    const anchors = new Set(template.anchors.map(item => item.id))
    const routes = new Map(catalog.routes.map(item => [item.id, item]))
    const tracks = new Set(catalog.tracks.map(item => item.id))
    const nodes = new Set(catalog.nodes.map(item => item.id))
    const eventNodeLists = deriveEventNodeLists(template, catalog)
    function anchorAtNodes(anchorID: string, nodeIDs: string[], label: string) {
        const anchor = template.anchors.find(item => item.id === anchorID)
        const track = catalog.tracks.find(item => item.id === anchor?.trackID)
        if (nodeIDs.length && track && !nodeIDs.some(id => track.fromNodeID === id || track.toNodeID === id)) errors.push(`${label}：锚的轨道不连接任何候选节点。`)
    }
    const nonnegative = (value: number) => Number.isFinite(value) && value >= 0
    const adjacency = new Map<string, string[]>(template.events.map(item => [item.id, []]))
    function edge(from: string, to: string) { adjacency.get(from)?.push(to) }
    function checkList(list: string[], selected: string | null, available: Set<string>, label: string) {
        if (list.some(id => !available.has(id))) errors.push(`${label}包含不存在的对象。`)
        if (new Set(list).size !== list.length) errors.push(`${label}不能重复。`)
        if (selected && !list.includes(selected)) errors.push(`${label}的选定值必须属于备选列表。`)
    }
    for (const activity of template.activities) {
        if (!activity.name.trim()) errors.push('活动名称不能为空。')
        if (!activityTypes.includes(activity.type)) errors.push(`${activity.name}：活动类型无效。`)
        if (!nonnegative(activity.minDuration) || !nonnegative(activity.maxDuration) || activity.minDuration > activity.maxDuration) errors.push(`${activity.name}：持续时间应满足 0 ≤ 最小值 ≤ 最大值。`)
        if (!Number.isFinite(activity.x) || !Number.isFinite(activity.y)) errors.push(`${activity.name}：画布位置无效。`)
        const start = events.get(activity.startEvent), end = events.get(activity.endEvent)
        if (!start || !end || start.id === end.id) errors.push(`${activity.name}：请指定两个不同且有效的起止事件。`)
        if (start?.time != null && end?.time != null) {
            const duration = end.time - start.time
            if (duration < activity.minDuration - 1e-8 || duration > activity.maxDuration + 1e-8) errors.push(`${activity.name}：起止时刻与持续时间范围不一致。`)
        }
        checkList(activity.routeList, activity.selectedRoute, new Set(routes.keys()), `${activity.name}备选进路`)
        checkList(activity.trackList, activity.selectedTrack, tracks, `${activity.name}备选轨道`)
        if (activity.type === 'Dwelling') {
            if (activity.routeList.length || activity.selectedRoute) errors.push(`${activity.name}：停留活动仅使用轨道。`)
            const namedTrackIDs = new Set(namedTracks(catalog).map(track => track.id))
            if (activity.trackList.some(id => tracks.has(id) && !namedTrackIDs.has(id))) errors.push(`${activity.name}：备选轨道必须具名，名称为空的轨道不能用于停留活动。`)
        } else {
            if (activity.trackList.length || activity.selectedTrack) errors.push(`${activity.name}：移动活动仅使用进路。`)
            if (activity.routeList.some(id => routes.get(id)?.type.toLowerCase() !== activity.type.toLowerCase())) errors.push(`${activity.name}：进路类型必须与活动类型一致。`)
        }
        edge(activity.startEvent, activity.endEvent)
    }
    for (const event of template.events) {
        if (!event.name.trim() || event.name.length > 200) errors.push('事件名称必须为 1–200 个字符。')
        if (event.time !== null && !nonnegative(event.time)) errors.push(`${event.name}：时刻必须是非负分钟数或留空。`)
        if (event.nodeID && !nodes.has(event.nodeID)) errors.push(`${event.name}：节点不属于当前站场方案。`)
        checkList(event.anchorList, event.selectedAnchor, anchors, `${event.name}备选锚`)
        event.anchorList.forEach(id => anchorAtNodes(id, eventNodeLists.get(event.id) || [], event.name))
    }
    for (const precedence of template.precedences) {
        const leading = events.get(precedence.leadingEvent), following = events.get(precedence.followingEvent)
        if (!leading || !following || leading.id === following.id) errors.push('次序需要两个不同且有效的事件。')
        if (!nonnegative(precedence.interval)) errors.push('次序间隔必须是非负分钟数。')
        if (leading?.time != null && following?.time != null && following.time - leading.time < precedence.interval - 1e-8) errors.push(`${leading.name} → ${following.name}：时刻不满足最小间隔。`)
        edge(precedence.leadingEvent, precedence.followingEvent)
    }
    for (const anchor of template.anchors) {
        if (!anchor.name.trim() || anchor.name.length > 200) errors.push('锚名称必须为 1–200 个字符。')
        if (!tracks.has(anchor.trackID)) errors.push(`${anchor.name}：请选择当前站场方案的轨道。`)
    }
    if (new Set(template.routeAnchors.map(item => item.routeID)).size !== template.routeAnchors.length) errors.push('同一进路的起终点锚配置不能重复。')
    for (const route of template.routeAnchors) {
        if (!routes.has(route.routeID)) errors.push('进路锚引用了不存在的进路。')
        if ([route.startAnchor, route.endAnchor].some(id => id && !anchors.has(id))) errors.push('进路起终点锚必须存在或留空。')
        const resource = routes.get(route.routeID)
        if (route.startAnchor) anchorAtNodes(route.startAnchor, resource?.startNodeID ? [resource.startNodeID] : [], '进路起点')
        if (route.endAnchor) anchorAtNodes(route.endAnchor, resource?.endNodeID ? [resource.endNodeID] : [], '进路终点')
    }
    const indegree = new Map(template.events.map(item => [item.id, 0]))
    for (const list of adjacency.values()) for (const id of list) if (indegree.has(id)) indegree.set(id, indegree.get(id)! + 1)
    const queue = [...indegree].filter(([, count]) => count === 0).map(([id]) => id)
    let visited = 0
    for (let index = 0; index < queue.length; index++) {
        const id = queue[index]!
        visited++
        for (const target of adjacency.get(id) ?? []) {
            if (!indegree.has(target)) continue
            indegree.set(target, indegree.get(target)! - 1)
            if (indegree.get(target) === 0) queue.push(target)
        }
    }
    if (visited !== events.size) errors.push('活动和次序关系形成了循环，请检查箭头方向。')
    return [...new Set(errors)]
}

const base = '/OperationProcess'
export const api = {
    async list(scope: ProcessScope) { return (await axios.get<ProcessTemplate[]>(`${base}/GetTemplates`, { params: scope })).data },
    async catalog(scope: ProcessScope) { return (await axios.get<ProcessCatalog>(`${base}/GetCatalog`, { params: scope })).data },
    async create(template: ProcessTemplate) { return (await axios.post<ProcessTemplate>(`${base}/CreateTemplate`, template)).data },
    async update(template: ProcessTemplate) { return (await axios.put<ProcessTemplate>(`${base}/UpdateTemplate`, template)).data },
    async remove(scope: ProcessScope, templateID: string, revision: number) { await axios.delete(`${base}/DeleteTemplate`, { params: { ...scope, templateID, revision } }) },
}
