import axios from 'axios'
import { createClientId } from '@/utils/clientId'

/** Durations and event times are in minutes; time is relative to the template origin. */
export const activityTypes = ['Arrival', 'Departure', 'Shunting', 'Locomotive', 'Dwelling'] as const
export type ActivityType = typeof activityTypes[number]
export const activityLabels: Record<ActivityType, string> = {
    Arrival: '接车', Departure: '发车', Shunting: '调车', Locomotive: '机车出入段', Dwelling: '停留',
}
export interface ProcessScope { instanceID: string; stationSchemeID: string }
export interface ProcessActivity {
    id: string; name: string; type: ActivityType; minDuration: number; maxDuration: number
    startEvent: string; endEvent: string; routeList: string[]; selectedRoute: string | null
    x: number; y: number
}
export interface ProcessEvent {
    id: string; name: string; time: number | null; nodeID: string | null
    /** Derived route endpoint candidates; absent in templates saved before this field existed. */
    nodeList?: string[]
    anchorList: string[]; selectedAnchor: string | null
    /** Derived references added by dwelling ownership or a direct connection to a dwelling event. */
    automaticAnchorIDs?: string[]
}
export interface ProcessPrecedence { id: string; leadingEvent: string; followingEvent: string; interval: number; isExact?: boolean }
export interface PrecedenceEndpoint { activityID: string; eventID: string }
export interface ProcessAnchor { id: string; name: string; routeIDs: string[]; dwellingActivityID?: string | null }
export interface ProcessRouteAnchors { routeID: string; startAnchor: string | null; endAnchor: string | null }
export interface ProcessTemplate extends ProcessScope {
    id: string; name: string; description: string; revision: number
    activities: ProcessActivity[]; events: ProcessEvent[]; precedences: ProcessPrecedence[]
    anchors: ProcessAnchor[]; routeAnchors: ProcessRouteAnchors[]
}
export interface ProcessCatalog {
    nodes: { id: string; name: string }[]
    routes: { id: string; name: string; type: string; startNodeID: string | null; endNodeID: string | null }[]
}
/** An anchor can connect at either endpoint of any of its dwelling routes. */
export function anchorConnectsNodes(anchor: ProcessAnchor | undefined, nodeIDs: string[], catalog: ProcessCatalog): boolean {
    return !!anchor && catalog.routes.some(route => anchor.routeIDs.includes(route.id) && route.type === 'Dwelling' &&
        nodeIDs.some(id => route.startNodeID === id || route.endNodeID === id))
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
        for (const routeID of activity.routeList) {
            const route = routes.get(routeID)
            if (!route || route.type.toLowerCase() !== activity.type.toLowerCase()) continue
            add(activity.startEvent, route.startNodeID)
            add(activity.endEvent, route.endNodeID)
            if (activity.type === 'Dwelling') {
                add(activity.startEvent, route.endNodeID)
                add(activity.endEvent, route.startNodeID)
            }
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

/** Reconcile editable templates only; execution snapshots retain their original constraints. */
export function syncDwellingAnchors(template: ProcessTemplate): void {
    const same = (left: string[] | undefined, right: string[]) => !!left && left.length === right.length && left.every((id, i) => id === right[i])
    const dwellings = template.activities.filter(activity => activity.type === 'Dwelling')
    const dwellingIDs = new Set(dwellings.map(activity => activity.id))
    for (const anchor of [...template.anchors]) {
        if (anchor.dwellingActivityID && !dwellingIDs.has(anchor.dwellingActivityID)) removeAnchor(template, anchor.id)
    }
    const owned = new Map<string, string[]>()
    for (const activity of dwellings) {
        let anchor = template.anchors.find(item => item.dwellingActivityID === activity.id)
        const endpoints = template.events.filter(event => event.id === activity.startEvent || event.id === activity.endEvent)
        if (!anchor) {
            // Reuse an exclusive legacy anchor and expand its route subset to all dwelling candidates.
            anchor = template.anchors.find(item => !item.dwellingActivityID &&
                item.routeIDs.length > 0 && new Set(item.routeIDs).size === item.routeIDs.length &&
                item.routeIDs.every(id => activity.routeList.includes(id)) && endpoints.length === 2 &&
                endpoints.every(event => event.anchorList.length === 1 && event.anchorList[0] === item.id) &&
                !dwellings.some(other => other.id !== activity.id && template.events.some(event =>
                    (event.id === other.startEvent || event.id === other.endEvent) && event.anchorList.includes(item.id))))
            if (anchor) anchor.dwellingActivityID = activity.id
            else {
                anchor = { id: makeID('anchor'), name: `${activity.name.slice(0, 199)}锚`, routeIDs: [...activity.routeList], dwellingActivityID: activity.id }
                template.anchors.push(anchor)
            }
        }
        if (!same(anchor.routeIDs, activity.routeList)) anchor.routeIDs = [...activity.routeList]
        for (const event of endpoints) {
            const ids = owned.get(event.id) || []
            if (!ids.includes(anchor.id)) ids.push(anchor.id)
            owned.set(event.id, ids)
        }
    }
    const inherited = new Map<string, string[]>()
    const activityEvents = new Set(template.activities.flatMap(activity => [activity.startEvent, activity.endEvent]))
    function connect(sourceID: string, targetID: string) {
        if (!activityEvents.has(targetID)) return
        const ids = inherited.get(targetID) || []
        for (const id of owned.get(sourceID) || []) if (!ids.includes(id)) ids.push(id)
        inherited.set(targetID, ids)
    }
    for (const link of template.precedences) {
        connect(link.leadingEvent, link.followingEvent)
        connect(link.followingEvent, link.leadingEvent)
    }
    for (const event of template.events) {
        const manual = event.anchorList.filter(id => !event.automaticAnchorIDs?.includes(id))
        const derived = owned.has(event.id)
            ? [...new Set([...owned.get(event.id)!, ...(inherited.get(event.id) || [])])]
            : (inherited.get(event.id) || []).filter(id => !manual.includes(id))
        const anchors = owned.has(event.id) ? derived : [...manual, ...derived]
        if (!same(event.anchorList, anchors)) event.anchorList = anchors
        if (!same(event.automaticAnchorIDs, derived)) event.automaticAnchorIDs = [...derived]
        if (owned.has(event.id) || derived.length || (event.selectedAnchor && !anchors.includes(event.selectedAnchor))) event.selectedAnchor = null
    }
    // Old endpoint bindings cannot override automatic events or retain a removed dwelling candidate.
    const events = new Map(template.events.map(event => [event.id, event]))
    const required = new Map<string, Set<string>>()
    for (const activity of template.activities) {
        for (const [eventID, endpoint] of [[activity.startEvent, 'startAnchor'], [activity.endEvent, 'endAnchor']] as const) {
            const automatic = events.get(eventID)?.automaticAnchorIDs || []
            if (!automatic.length) continue
            for (const routeID of activity.routeList) {
                for (const side of activity.type === 'Dwelling' ? ['startAnchor', 'endAnchor'] as const : [endpoint]) {
                    const key = `${routeID}\u001f${side}`
                    const prior = required.get(key)
                    required.set(key, new Set(prior ? automatic.filter(id => prior.has(id)) : automatic))
                }
            }
        }
    }
    const managed = new Set(template.anchors.filter(anchor => anchor.dwellingActivityID).map(anchor => anchor.id))
    for (const binding of template.routeAnchors) {
        for (const side of ['startAnchor', 'endAnchor'] as const) {
            const allowed = required.get(`${binding.routeID}\u001f${side}`)
            const bound = binding[side]
            if (bound && (allowed ? !allowed.has(bound) : managed.has(bound))) binding[side] = null
        }
    }
}
export function makeID(prefix: string) { return `${prefix}-${createClientId()}` }
export type ProcessTranslate = (key: string, parameters?: Record<string, string | number>) => string
function processText(translate: ProcessTranslate | undefined, key: string, fallback: string, parameters?: Record<string, string | number>) {
    return translate?.(`operationProcess.defaults.${key}`, parameters) ?? fallback
}
export function createEmptyTemplate(scope: ProcessScope, translate?: ProcessTranslate): ProcessTemplate {
    return { instanceID: scope.instanceID, stationSchemeID: scope.stationSchemeID, id: makeID('process'), name: processText(translate, 'newTemplate', '新建作业过程模板'), description: '', revision: 0,
        activities: [], events: [], precedences: [], anchors: [], routeAnchors: [] }
}

export function createDemoCatalog(translate?: ProcessTranslate): ProcessCatalog {
    const text = (key: string, fallback: string) => processText(translate, key, fallback)
    return {
        nodes: [
            { id: 'N1', name: text('nodes.westEntrance', '西咽喉入口') }, { id: 'N2', name: text('nodes.trackWest', 'Ⅰ道西端') },
            { id: 'N3', name: text('nodes.trackEast', 'Ⅰ道东端') }, { id: 'N4', name: text('nodes.eastExit', '东咽喉出口') },
            { id: 'N5', name: text('nodes.depotExit', '机务段出口') }, { id: 'N6', name: text('nodes.headshuntEnd', '牵出线端') },
        ],
        routes: [
            { id: 'R1', name: text('routes.arrival', '西咽喉 → Ⅰ道接车'), type: 'Arrival', startNodeID: 'N1', endNodeID: 'N2' },
            { id: 'R2', name: text('routes.departure', 'Ⅰ道 → 东咽喉发车'), type: 'Departure', startNodeID: 'N3', endNodeID: 'N4' },
            { id: 'R3', name: text('routes.shunting', '牵出线 → Ⅰ道调车'), type: 'Shunting', startNodeID: 'N6', endNodeID: 'N2' },
            { id: 'R4', name: text('routes.locomotive', '机务段 → 牵出线'), type: 'Locomotive', startNodeID: 'N5', endNodeID: 'N6' },
            { id: 'R5', name: text('routes.dwelling', 'Ⅰ道停留进路'), type: 'Dwelling', startNodeID: 'N2', endNodeID: 'N3' },
        ],
    }
}
export const demoCatalog = createDemoCatalog()

/** Uses only references from the supplied station catalog. Never adds demo infrastructure to a real station. */
export function createExampleTemplate(scope: ProcessScope, catalog: ProcessCatalog, translate?: ProcessTranslate): ProcessTemplate {
    const text = (key: string, fallback: string, parameters?: Record<string, string | number>) => processText(translate, key, fallback, parameters)
    const template = createEmptyTemplate(scope, translate)
    template.name = text('exampleName', '接车—停留—发车（含机车与调车协同）')
    template.description = text('exampleDescription', '五类活动示例。时刻从模板起点起算，单位为分钟；次序间隔为最小间隔。可编辑后另存为当前车站方案共享的过程模板。')
    const anchorByRoute = new Map<string, string>()
    function anchorFor(nodeID: string | null | undefined) {
        const route = catalog.routes.find(route => route.type === 'Dwelling' && (route.startNodeID === nodeID || route.endNodeID === nodeID))
        if (!route) return null
        if (!anchorByRoute.has(route.id)) {
            const id = makeID('anchor')
            template.anchors.push({ id, name: `${route.name}锚`, routeIDs: [route.id] })
            anchorByRoute.set(route.id, id)
        }
        return anchorByRoute.get(route.id)!
    }
    const specs: [ActivityType, string, number, number, number, number, number, number][] = [
        ['Arrival', text('activities.arrival', '列车接入'), 0, 6, 4, 8, 65, 85],
        ['Dwelling', text('activities.dwelling', '站内停留 / 技术作业'), 6, 26, 15, 25, 395, 85],
        ['Departure', text('activities.departure', '列车发出'), 28, 34, 4, 8, 725, 85],
        ['Locomotive', text('activities.locomotive', '机车出段'), 6, 10, 3, 6, 65, 295],
        ['Shunting', text('activities.shunting', '调车转线'), 12, 20, 5, 10, 395, 295],
    ]
    for (const [type, name, start, end, minDuration, maxDuration, x, y] of specs) {
        const candidates = catalog.routes.filter(route => route.type.toLowerCase() === type.toLowerCase())
        const route = candidates[0]
        const startAnchor = anchorFor(route?.startNodeID)
        const endAnchor = anchorFor(route?.endNodeID)
        const startEvent = makeID('event'), endEvent = makeID('event')
        template.events.push(
            { id: startEvent, name: text('startEvent', `${name}开始`, { name }), time: start, nodeID: route?.startNodeID || null,
                anchorList: startAnchor ? [startAnchor] : [], selectedAnchor: startAnchor },
            { id: endEvent, name: text('endEvent', `${name}结束`, { name }), time: end, nodeID: route?.endNodeID || null,
                anchorList: endAnchor ? [endAnchor] : [], selectedAnchor: endAnchor },
        )
        template.activities.push({ id: makeID('activity'), name, type, minDuration, maxDuration,
            startEvent, endEvent, routeList: candidates.map(item => item.id), selectedRoute: route?.id ?? null,
            x, y })
        if (route && !template.routeAnchors.some(item => item.routeID === route.id)) {
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
export function renameActivity(template: ProcessTemplate, activityID: string, name: string, translate?: ProcessTranslate): boolean {
    const activity = template.activities.find(item => item.id === activityID)
    if (!activity || activity.name === name) return false
    activity.name = name
    const start = template.events.find(item => item.id === activity.startEvent)
    const end = template.events.find(item => item.id === activity.endEvent)
    if (start) start.name = processText(translate, 'startEvent', `${name}开始`, { name })
    if (end) end.name = processText(translate, 'endEvent', `${name}结束`, { name })
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
        if (event.automaticAnchorIDs) event.automaticAnchorIDs = event.automaticAnchorIDs.filter(anchor => anchor !== id)
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
    const nodes = new Set(catalog.nodes.map(item => item.id))
    const eventNodeLists = deriveEventNodeLists(template, catalog)
    function anchorAtNodes(anchorID: string, nodeIDs: string[], label: string) {
        const anchor = template.anchors.find(item => item.id === anchorID)
        if (nodeIDs.length && anchor && !anchorConnectsNodes(anchor, nodeIDs, catalog)) errors.push(`${label}：锚的停留进路不连接任何候选节点。`)
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
        if (!activity.routeList.length) errors.push(`${activity.name}：请选择备选进路。`)
        if (activity.routeList.some(id => routes.get(id)?.type.toLowerCase() !== activity.type.toLowerCase())) errors.push(`${activity.name}：进路类型必须与活动类型一致。`)
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
        if (leading?.time != null && following?.time != null) {
            const elapsed = following.time - leading.time
            if (elapsed < precedence.interval - 1e-8 || (precedence.isExact && elapsed > precedence.interval + 1e-8))
                errors.push(`${leading.name} → ${following.name}：${precedence.isExact ? '时刻差必须严格等于规定间隔。' : '时刻不满足最小间隔。'}`)
        }
        edge(precedence.leadingEvent, precedence.followingEvent)
    }
    for (const anchor of template.anchors) {
        if (!anchor.name.trim() || anchor.name.length > 200) errors.push('锚名称必须为 1–200 个字符。')
        checkList(anchor.routeIDs, null, new Set(routes.keys()), `${anchor.name}备选进路`)
        if (!anchor.routeIDs.length || anchor.routeIDs.some(id => routes.get(id)?.type !== 'Dwelling')) errors.push(`${anchor.name}：请选择当前站场方案的停留进路。`)
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
