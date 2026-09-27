import test from 'node:test'
import assert from 'node:assert/strict'
import { createExampleTemplate, createEmptyTemplate, demoCatalog, deriveEventNodeLists, syncEventNodeLists, dwellingTrackNames, namedTracks, reconcileDwellingTracks, validateTemplate, renameActivity, removeActivity, removeEvent, removeAnchor, getPrecedenceEndCandidates, getPrecedenceStartCandidates } from '../src/capacity/operationProcess.ts'

const scope = { instanceID: 'instance', stationSchemeID: 'station' }
const example = () => createExampleTemplate(scope, demoCatalog)

test('precedence candidates contain activity ends in activity order, excluding start-only and standalone events', () => {
    const template = example()
    template.events.push({ id: 'standalone', name: '独立事件', time: null, nodeID: null, anchorList: [], selectedAnchor: null })
    const expected = template.activities.map(activity => ({ activityID: activity.id, eventID: activity.endEvent }))

    const candidates = getPrecedenceEndCandidates(template)
    assert.deepEqual(candidates, expected)
    assert.ok(candidates.every(candidate => candidate.eventID !== 'standalone'))
    assert.ok(template.activities.every(activity => !candidates.some(candidate => candidate.eventID === activity.startEvent)))
    assert.deepEqual(getPrecedenceEndCandidates(createEmptyTemplate(scope)), [])
})

test('precedence candidates exclude every activity sharing the source end while preserving other shared ends', () => {
    const template = example()
    const [source, sourcePeer, target, targetPeer, remaining] = template.activities
    sourcePeer.endEvent = source.endEvent
    targetPeer.endEvent = target.endEvent
    const pair = activity => ({ activityID: activity.id, eventID: activity.endEvent })

    assert.deepEqual(getPrecedenceEndCandidates(template), template.activities.map(pair))
    assert.deepEqual(getPrecedenceEndCandidates(template, source.id), [target, targetPeer, remaining].map(pair))
    assert.deepEqual(getPrecedenceEndCandidates(template, sourcePeer.id), [target, targetPeer, remaining].map(pair))
    assert.deepEqual(getPrecedenceEndCandidates(template, target.id), [source, sourcePeer, remaining].map(pair))
})

test('precedence candidates skip empty or missing endpoint references and reject unknown source activities', () => {
    const template = example()
    const [missing, empty, removed, ...valid] = template.activities
    missing.endEvent = 'missing-event'
    empty.endEvent = ''
    template.events = template.events.filter(event => event.id !== removed.endEvent)
    const expected = valid.map(activity => ({ activityID: activity.id, eventID: activity.endEvent }))

    assert.deepEqual(getPrecedenceEndCandidates(template), expected)
    assert.deepEqual(getPrecedenceEndCandidates(template, missing.id), expected)
    assert.deepEqual(getPrecedenceEndCandidates(template, 'missing-activity'), [])
    assert.deepEqual(getPrecedenceEndCandidates(template, ''), [])
})

test('precedence candidates retain targets that graph validation would reject as a cycle', () => {
    const template = example()
    template.events.forEach(event => { event.time = null })
    const [arrival, dwelling] = template.activities
    assert.ok(getPrecedenceEndCandidates(template, dwelling.id).some(candidate =>
        candidate.activityID === arrival.id && candidate.eventID === arrival.endEvent))

    template.precedences.push({ id: 'cycle-between-ends', leadingEvent: dwelling.endEvent, followingEvent: arrival.endEvent, interval: 0 })
    assert.ok(validateTemplate(template, demoCatalog).some(error => error.includes('循环')))
})

test('precedence candidate reads never mutate the template and return detached result objects', () => {
    const template = example()
    const before = structuredClone(template)
    const freeze = value => {
        if (value && typeof value === 'object' && !Object.isFrozen(value)) {
            Object.freeze(value)
            Object.values(value).forEach(freeze)
        }
    }
    freeze(template)
    for (const sourceID of [undefined, template.activities[0].id, template.activities.at(-1).id, 'missing-activity']) {
        const first = getPrecedenceEndCandidates(template, sourceID)
        const second = getPrecedenceEndCandidates(template, sourceID)
        assert.notEqual(first, second)
        assert.deepEqual(first, second)
        if (first.length) {
            assert.notEqual(first[0], second[0])
            first[0].activityID = 'changed-result-activity'
            first[0].eventID = 'changed-result-event'
            assert.deepEqual(getPrecedenceEndCandidates(template, sourceID), second)
        }
        assert.deepEqual(template, before)
    }
})

test('an end selected in the first step offers only other activity starts in activity order', () => {
    const template = example()
    template.events.push({ id: 'standalone-start', name: '独立事件', time: null, nodeID: null, anchorList: [], selectedAnchor: null })
    const source = template.activities[0]
    assert.ok(getPrecedenceEndCandidates(template).some(candidate =>
        candidate.activityID === source.id && candidate.eventID === source.endEvent))

    const candidates = getPrecedenceStartCandidates(template, source.id)
    assert.deepEqual(candidates, template.activities.slice(1).map(activity => ({ activityID: activity.id, eventID: activity.startEvent })))
    assert.ok(candidates.every(candidate => candidate.eventID !== 'standalone-start'))
    assert.ok(template.activities.every(activity => !candidates.some(candidate => candidate.eventID === activity.endEvent)))
})

test('start candidates exclude the source and same event, retaining shared starts and peers sharing only its end', () => {
    const template = example()
    const [source, sharedEndPeer, sameEventTarget, sharedStartPeer, remaining] = template.activities
    sharedEndPeer.endEvent = source.endEvent
    sameEventTarget.startEvent = source.endEvent
    sharedStartPeer.startEvent = sharedEndPeer.startEvent

    assert.deepEqual(getPrecedenceStartCandidates(template, source.id), [sharedEndPeer, sharedStartPeer, remaining]
        .map(activity => ({ activityID: activity.id, eventID: activity.startEvent })))
})

test('start candidates require an existing source activity and an existing nonempty source end', () => {
    const template = example()
    const source = template.activities[0]
    const originalEnd = source.endEvent
    assert.deepEqual(getPrecedenceStartCandidates(template, 'missing-activity'), [])
    assert.deepEqual(getPrecedenceStartCandidates(template, ''), [])
    assert.deepEqual(getPrecedenceStartCandidates(createEmptyTemplate(scope), source.id), [])
    source.endEvent = ''
    assert.deepEqual(getPrecedenceStartCandidates(template, source.id), [])
    source.endEvent = 'missing-source-end'
    assert.deepEqual(getPrecedenceStartCandidates(template, source.id), [])
    source.endEvent = originalEnd
    template.events = template.events.filter(event => event.id !== originalEnd)
    assert.deepEqual(getPrecedenceStartCandidates(template, source.id), [])
})

test('start candidates skip empty or missing starts without requiring unrelated target end references', () => {
    const template = example()
    const [source, missing, empty, removed, validStart] = template.activities
    missing.startEvent = 'missing-target-start'
    empty.startEvent = ''
    template.events = template.events.filter(event => event.id !== removed.startEvent)
    validStart.endEvent = 'unrelated-missing-target-end'

    assert.deepEqual(getPrecedenceStartCandidates(template, source.id), [{ activityID: validStart.id, eventID: validStart.startEvent }])
})

test('start candidate reads are pure and return detached arrays and endpoint objects', () => {
    const template = example()
    const before = structuredClone(template)
    const freeze = value => {
        if (value && typeof value === 'object' && !Object.isFrozen(value)) {
            Object.freeze(value)
            Object.values(value).forEach(freeze)
        }
    }
    freeze(template)
    for (const sourceID of [template.activities[0].id, template.activities.at(-1).id, 'missing-activity']) {
        const first = getPrecedenceStartCandidates(template, sourceID)
        const second = getPrecedenceStartCandidates(template, sourceID)
        assert.notEqual(first, second)
        assert.deepEqual(first, second)
        if (first.length) {
            assert.notEqual(first[0], second[0])
            first[0].activityID = 'changed-result-activity'
            first[0].eventID = 'changed-result-event'
            assert.deepEqual(getPrecedenceStartCandidates(template, sourceID), second)
        }
        assert.deepEqual(template, before)
    }
})

test('end-to-start candidates leave cycle rejection to existing graph validation', () => {
    const template = example()
    template.events.forEach(event => { event.time = null })
    const [arrival, dwelling] = template.activities
    assert.ok(getPrecedenceStartCandidates(template, dwelling.id).some(candidate =>
        candidate.activityID === arrival.id && candidate.eventID === arrival.startEvent))

    template.precedences.push({ id: 'cycle-end-to-start', leadingEvent: dwelling.endEvent, followingEvent: arrival.startEvent, interval: 0 })
    assert.ok(validateTemplate(template, demoCatalog).some(error => error.includes('循环')))
})

test('five activity types, paired events and route endpoint anchors form a valid sample', () => {
    const template = example()
    assert.equal(new Set(template.activities.map(item => item.type)).size, 5)
    assert.equal(template.events.length, 10)
    assert.equal(template.precedences.length, 5)
    assert.equal(template.routeAnchors.length, 4)
    assert.deepEqual(validateTemplate(template, demoCatalog), [])
})

test('real empty station never receives fabricated nodes, tracks or routes', () => {
    const catalog = { nodes: [], tracks: [], routes: [] }
    const template = createExampleTemplate(scope, catalog)
    assert.deepEqual(validateTemplate(template, catalog), [])
    assert.equal(template.anchors.length, 0)
    assert.ok(template.events.every(item => item.nodeID === null && item.selectedAnchor === null))
    assert.ok(template.activities.every(item => item.selectedRoute === null && item.selectedTrack === null))
})

test('nullable times and endpoint anchors can remain unspecified in a reusable template', () => {
    const template = example()
    template.events.forEach(item => { item.time = null; item.selectedAnchor = null })
    template.routeAnchors.forEach(item => { item.startAnchor = null; item.endAnchor = null })
    assert.deepEqual(validateTemplate(template, demoCatalog), [])
})

test('deleting activity cleans its owned events and connected arrows but keeps unrelated references', () => {
    const template = example()
    const activity = template.activities.find(item => item.type === 'Arrival')
    const eventIDs = new Set([activity.startEvent, activity.endEvent])
    assert.equal(removeEvent(template, activity.startEvent), false)
    removeActivity(template, activity.id)
    assert.equal(template.activities.length, 4)
    assert.equal(template.events.length, 8)
    assert.ok(template.precedences.every(item => !eventIDs.has(item.leadingEvent) && !eventIDs.has(item.followingEvent)))
    assert.deepEqual(validateTemplate(template, demoCatalog), [])
})

test('removing anchor clears candidate, selected and route endpoint references', () => {
    const template = example()
    const id = template.events[0].selectedAnchor
    assert.ok(id)
    removeAnchor(template, id)
    assert.ok(template.events.every(item => item.selectedAnchor !== id && !item.anchorList.includes(id)))
    assert.ok(template.routeAnchors.every(item => item.startAnchor !== id && item.endAnchor !== id))
    assert.deepEqual(validateTemplate(template, demoCatalog), [])
})

test('cycle detection includes activity start-to-end edges', () => {
    const template = example()
    template.events.forEach(item => { item.time = null })
    const arrival = template.activities[0]
    template.precedences.push({ id: 'backwards', leadingEvent: arrival.endEvent, followingEvent: arrival.startEvent, interval: 0 })
    assert.ok(validateTemplate(template, demoCatalog).some(error => error.includes('循环')))
})

test('rejects incompatible routes, absent selected candidates, dangling nodes and duration violations', () => {
    const template = example()
    template.activities[0].routeList = ['R2']
    template.activities[0].maxDuration = 2
    template.events[0].nodeID = 'foreign-node'
    const errors = validateTemplate(template, demoCatalog).join('\n')
    assert.match(errors, /类型必须/)
    assert.match(errors, /选定值必须/)
    assert.match(errors, /最小值/)
    assert.match(errors, /节点不属于/)
})

test('enforces relative event timing and minimum precedence interval', () => {
    const template = example()
    const departure = template.activities.find(item => item.type === 'Departure')
    const start = template.events.find(item => item.id === departure.startEvent)
    const end = template.events.find(item => item.id === departure.endEvent)
    start.time = 26
    end.time = 32
    assert.ok(validateTemplate(template, demoCatalog).some(error => error.includes('不满足最小间隔')))
})

test('new templates use distinct IDs and belong to the scheme without a plan', () => {
    const first = createEmptyTemplate(scope), second = createEmptyTemplate({ ...scope, operationPlanID: 'legacy-plan' })
    assert.notEqual(first.id, second.id)
    assert.equal(first.instanceID, scope.instanceID)
    assert.equal(first.stationSchemeID, scope.stationSchemeID)
    assert.equal(Object.hasOwn(first, 'operationPlanID'), false)
    assert.equal(Object.hasOwn(second, 'operationPlanID'), false)
    assert.equal(first.revision, 0)
})

test('event anchors must connect to derived candidates and route endpoint anchors to their own endpoint', () => {
    const template = example()
    const wrongAnchor = template.anchors.find(item => item.trackID === 'T4')
    template.events[0].anchorList = [wrongAnchor.id]
    template.events[0].selectedAnchor = wrongAnchor.id
    template.routeAnchors[0].startAnchor = wrongAnchor.id
    assert.ok(validateTemplate(template, demoCatalog).filter(error => error.includes('不连接')).length >= 2)
})

test('sample endpoint anchors tolerate shuffled track lists in an existing station', () => {
    const catalog = structuredClone(demoCatalog)
    catalog.routes.forEach(route => route.trackIDs.reverse())
    const template = createExampleTemplate(scope, catalog)
    assert.deepEqual(validateTemplate(template, catalog), [])
})

test('dwelling candidates exclude null, empty and whitespace names, keeping real names', () => {
    const catalog = structuredClone(demoCatalog)
    catalog.tracks[0].name = null
    catalog.tracks[1].name = ''
    catalog.tracks[2].name = ' \t\n '
    catalog.tracks[3].name = '  机车走行线  '
    assert.deepEqual(namedTracks(catalog).map(track => track.id), ['T4', 'T5'])
    const template = createExampleTemplate(scope, catalog)
    const dwelling = template.activities.find(activity => activity.type === 'Dwelling')
    assert.deepEqual(dwelling.trackList, ['T4'])
    assert.deepEqual(validateTemplate(template, catalog), [])
})

test('unnamed tracks are rejected for dwelling but still work for movement and anchors', () => {
    const catalog = structuredClone(demoCatalog)
    catalog.tracks[0].name = ''
    const template = createExampleTemplate(scope, catalog)
    assert.ok(template.anchors.some(anchor => anchor.trackID === 'T1'))
    assert.deepEqual(validateTemplate(template, catalog), [])
    template.activities.find(activity => activity.type === 'Dwelling').trackList.push('T1')
    assert.ok(validateTemplate(template, catalog).some(error => error.includes('备选轨道必须具名')))
})

test('example leaves dwelling track candidates empty when the station has only unnamed tracks', () => {
    const catalog = structuredClone(demoCatalog)
    catalog.tracks.forEach(track => { track.name = ' ' })
    const template = createExampleTemplate(scope, catalog)
    const dwelling = template.activities.find(activity => activity.type === 'Dwelling')
    assert.deepEqual(dwelling.trackList, [])
    assert.equal(dwelling.selectedTrack, null)
    assert.deepEqual(validateTemplate(template, catalog), [])
})

test('legacy dwelling selections lose unnamed and missing links while retaining other graph references', () => {
    const catalog = structuredClone(demoCatalog)
    catalog.tracks[0].name = ''
    catalog.tracks[2].name = ' \t '
    const template = example()
    const dwelling = template.activities.find(activity => activity.type === 'Dwelling')
    dwelling.trackList = ['T1', 'T2', 'T3', 'missing-link']
    dwelling.selectedTrack = 'T1'
    const movement = structuredClone(template.activities.find(activity => activity.type === 'Arrival'))
    const anchors = structuredClone(template.anchors)
    const events = structuredClone(template.events)
    const persistedSnapshot = JSON.stringify(template)
    assert.equal(reconcileDwellingTracks(template, catalog), true)
    assert.deepEqual(dwelling.trackList, ['T2'])
    assert.equal(dwelling.selectedTrack, null)
    assert.deepEqual(template.activities.find(activity => activity.type === 'Arrival'), movement)
    assert.deepEqual(template.anchors, anchors)
    assert.deepEqual(template.events, events)
    assert.deepEqual(JSON.parse(persistedSnapshot).activities.find(activity => activity.type === 'Dwelling').trackList, ['T1', 'T2', 'T3', 'missing-link'])
    assert.equal(reconcileDwellingTracks(template, catalog), false)
})

test('candidate labels are exact Link.Name values, never IDs, prefixes or selected-track fallback', () => {
    const catalog = structuredClone(demoCatalog)
    catalog.tracks[0].name = ''
    catalog.tracks[1].name = '  Ⅰ道（原始 Link 名称）  '
    catalog.tracks[2].name = '3'
    const activity = example().activities.find(item => item.type === 'Dwelling')
    activity.trackList = ['T2', 'T1', 'T3', 'missing-link']
    activity.selectedTrack = 'T1'
    assert.deepEqual(dwellingTrackNames(activity, catalog), ['  Ⅰ道（原始 Link 名称）  ', '3'])
    assert.equal(namedTracks(catalog).find(track => track.id === 'T2').name, '  Ⅰ道（原始 Link 名称）  ')
})

test('renaming an activity changes only its name and endpoint labels, preserving graph and event properties', () => {
    const template = example()
    const activity = template.activities.find(item => item.type === 'Dwelling')
    const start = template.events.find(item => item.id === activity.startEvent)
    const end = template.events.find(item => item.id === activity.endEvent)
    start.name = '原开始事件名称'
    end.name = '原结束事件名称'
    const expected = structuredClone(template)
    expected.activities.find(item => item.id === activity.id).name = 'Ⅰ道待避 / 技术检查'
    expected.events.find(item => item.id === start.id).name = 'Ⅰ道待避 / 技术检查开始'
    expected.events.find(item => item.id === end.id).name = 'Ⅰ道待避 / 技术检查结束'

    assert.equal(renameActivity(template, activity.id, 'Ⅰ道待避 / 技术检查'), true)
    assert.deepEqual(template, expected)
    assert.deepEqual(validateTemplate(template, demoCatalog), [])
})

test('reapplying the same activity name preserves independently edited endpoint labels', () => {
    const template = example()
    const activity = template.activities[0]
    template.events.find(item => item.id === activity.startEvent).name = '到达接车信号机'
    template.events.find(item => item.id === activity.endEvent).name = '列车停稳'
    const before = structuredClone(template)

    assert.equal(renameActivity(template, activity.id, activity.name), false)
    assert.deepEqual(template, before)
    assert.equal(renameActivity(template, 'missing-activity', '不存在的活动'), false)
    assert.deepEqual(template, before)
})

test('event node candidates use valid compatible route endpoints, preserving order and deduplicating', () => {
    const template = example()
    const catalog = structuredClone(demoCatalog)
    const arrival = template.activities.find(activity => activity.type === 'Arrival')
    catalog.routes.push(
        { id: 'alternative', name: '接车备选', type: 'arrival', startNodeID: 'N3', endNodeID: 'N4', trackIDs: [] },
        { id: 'same-endpoints', name: '相同端点', type: 'Arrival', startNodeID: 'N1', endNodeID: 'N2', trackIDs: [] },
        { id: 'invalid-endpoints', name: '无有效端点', type: 'Arrival', startNodeID: 'missing-node', endNodeID: null, trackIDs: [] },
    )
    arrival.routeList = ['alternative', 'R1', 'same-endpoints', 'R1', 'R2', 'missing-route', 'invalid-endpoints']
    const before = structuredClone(template)
    const beforeCatalog = structuredClone(catalog)
    const derived = deriveEventNodeLists(template, catalog)

    assert.deepEqual(derived.get(arrival.startEvent), ['N3', 'N1'])
    assert.deepEqual(derived.get(arrival.endEvent), ['N4', 'N2'])
    assert.deepEqual([...derived.keys()], template.events.map(event => event.id))
    assert.deepEqual(template, before)
    assert.deepEqual(catalog, beforeCatalog)
})

test('shared events aggregate start and end candidates without adding dangling event references', () => {
    const template = example()
    const arrival = template.activities.find(activity => activity.type === 'Arrival')
    const departure = template.activities.find(activity => activity.type === 'Departure')
    const originalDepartureStart = departure.startEvent
    departure.startEvent = arrival.endEvent
    const locomotive = template.activities.find(activity => activity.type === 'Locomotive')
    locomotive.startEvent = 'missing-event'
    const derived = deriveEventNodeLists(template, demoCatalog)

    assert.deepEqual(derived.get(arrival.endEvent), ['N2', 'N3'])
    assert.deepEqual(derived.get(originalDepartureStart), [])
    assert.equal(derived.has('missing-event'), false)
    assert.equal(derived.size, template.events.length)
})

test('node candidates never fall back to legacy node IDs, selected routes, stored lists or dwelling tracks', () => {
    const template = example()
    template.activities.forEach(activity => { activity.routeList = [] })
    template.events.forEach(event => { event.nodeList = ['N5']; event.nodeID = 'N6' })
    template.events.push({ id: 'standalone', name: '独立事件', time: null, nodeID: 'N1', nodeList: ['N2'], anchorList: [], selectedAnchor: null })
    const dwelling = template.activities.find(activity => activity.type === 'Dwelling')
    assert.ok(dwelling.trackList.length)
    assert.ok(template.activities.some(activity => activity.selectedRoute))
    const derived = deriveEventNodeLists(template, demoCatalog)

    assert.equal(derived.size, template.events.length)
    assert.ok([...derived.values()].every(nodeIDs => nodeIDs.length === 0))
})

test('synchronizing node lists preserves event and graph data and does not rewrite unchanged arrays', () => {
    const template = example()
    const expected = structuredClone(template)
    const derived = deriveEventNodeLists(template, demoCatalog)
    expected.events.forEach(event => { event.nodeList = derived.get(event.id) })
    assert.equal(syncEventNodeLists(template, demoCatalog), true)
    assert.deepEqual(template, expected)
    const arrays = template.events.map(event => event.nodeList)
    assert.equal(syncEventNodeLists(template, demoCatalog), false)
    template.events.forEach((event, index) => assert.equal(event.nodeList, arrays[index]))

    const arrival = template.activities.find(activity => activity.type === 'Arrival')
    arrival.routeList = []
    assert.equal(syncEventNodeLists(template, demoCatalog), true)
    for (const event of template.events) {
        if (event.id === arrival.startEvent || event.id === arrival.endEvent) assert.deepEqual(event.nodeList, [])
        else assert.equal(event.nodeList, arrays[template.events.indexOf(event)])
        const original = expected.events.find(item => item.id === event.id)
        assert.deepEqual({ ...event, nodeList: undefined }, { ...original, nodeList: undefined })
    }
})

test('event anchor validation accepts any derived endpoint and ignores stored node lists and legacy selections', () => {
    const template = example()
    const catalog = structuredClone(demoCatalog)
    const arrival = template.activities.find(activity => activity.type === 'Arrival')
    const start = template.events.find(event => event.id === arrival.startEvent)
    catalog.routes.push({ id: 'arrival-alternative', name: '另一端点接车', type: 'Arrival', startNodeID: 'N5', endNodeID: 'N2', trackIDs: [] })
    arrival.routeList.push('arrival-alternative')
    const anchor = template.anchors.find(item => item.trackID === 'T4')
    start.anchorList = [anchor.id]
    start.selectedAnchor = anchor.id
    start.nodeID = 'N3'
    start.nodeList = ['N1']
    assert.equal(arrival.selectedRoute, 'R1')
    assert.deepEqual(validateTemplate(template, catalog), [])

    arrival.routeList = ['R1']
    start.nodeID = 'N5'
    start.nodeList = ['N5']
    assert.ok(validateTemplate(template, catalog).some(error => error.includes(start.name) && error.includes('不连接任何候选节点')))
})

test('empty derived locations impose no event anchor restriction and legacy node IDs only check catalog membership', () => {
    const template = example()
    const anchor = template.anchors.find(item => item.trackID === 'T4')
    template.activities.forEach(activity => { activity.routeList = []; activity.selectedRoute = null })
    template.events.forEach(event => { event.nodeID = 'N1'; event.anchorList = [anchor.id]; event.selectedAnchor = anchor.id })
    template.events.push({ id: 'standalone', name: '独立事件', time: null, nodeID: 'N1', nodeList: ['N1'], anchorList: [anchor.id], selectedAnchor: anchor.id })
    assert.deepEqual(validateTemplate(template, demoCatalog), [])

    template.events[0].nodeID = 'missing-node'
    const errors = validateTemplate(template, demoCatalog)
    assert.equal(errors.length, 1)
    assert.match(errors[0], /节点不属于当前站场方案/)
})
