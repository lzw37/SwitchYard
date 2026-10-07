import assert from 'node:assert/strict'
import test from 'node:test'
import { anchorConnectsNodes, createDemoCatalog, createExampleTemplate, deriveEventNodeLists, removeActivity, syncDwellingAnchors, validateTemplate } from './operationProcess.ts'
import { createProcessTrainSelections, processTrainActivityRows, updateProcessTrainSelection,
    validateProcessTrainSelections } from './components/processTrainCreation.ts'

function fixture() {
    const catalog = createDemoCatalog()
    const source = createExampleTemplate({ instanceID: 's', stationSchemeID: 'p' }, catalog)
    source.revision = 1
    const stop = source.activities.find(activity => activity.type === 'Dwelling')
    return { catalog, source, stop }
}
test('all five activity types use real routes and dwelling endpoints participate in event locations', () => {
    const { source, catalog, stop } = fixture()
    assert.deepEqual(validateTemplate(source, catalog), [])
    assert.deepEqual(stop.routeList, ['R5'])
    assert.equal('trackList' in stop, false)
    assert.ok(source.anchors.every(anchor => anchor.routeIDs.length && !('trackID' in anchor) && !('routeID' in anchor)))
    const nodes = deriveEventNodeLists(source, catalog)
    assert.deepEqual(nodes.get(stop.startEvent), ['N2', 'N3'])
    assert.deepEqual(nodes.get(stop.endEvent), ['N3', 'N2'])
    const selections = createProcessTrainSelections(source, catalog)
    assert.ok(selections.every(selection => selection.routeID && !selection.trackID))
    assert.deepEqual(validateProcessTrainSelections(source, catalog, selections), [])
    assert.equal(processTrainActivityRows(source, catalog, selections).find(row => row.activity === stop).routeLocked, true)
})

test('dwelling choices reject wrong-type, missing, out-of-list and changed fixed routes', () => {
    const { source, catalog, stop } = fixture()
    const selections = createProcessTrainSelections(source, catalog)
    for (const routeID of ['', 'R1', 'deleted']) {
        const invalid = selections.map(selection => selection.activityID === stop.id ? { ...selection, routeID } : selection)
        assert.ok(validateProcessTrainSelections(source, catalog, invalid).some(issue => issue.activityID === stop.id))
        assert.equal(updateProcessTrainSelection(source, catalog, selections, stop.id, 'routeID', routeID), selections)
    }
    stop.routeList = ['R1']; stop.selectedRoute = 'R1'
    assert.ok(validateTemplate(source, catalog).some(message => message.includes('进路类型必须与活动类型一致')))
})

test('unfixed dwelling route selection records route ID without a track requirement', () => {
    const { source, catalog, stop } = fixture()
    catalog.routes.push({ ...catalog.routes.find(route => route.id === 'R5'), id: 'R6', name: '另一个停留进路' })
    stop.routeList.push('R6'); stop.selectedRoute = null
    let selections = createProcessTrainSelections(source, catalog)
    assert.equal(selections.find(selection => selection.activityID === stop.id).routeID, '')
    selections = updateProcessTrainSelection(source, catalog, selections, stop.id, 'routeID', 'R6')
    assert.deepEqual(selections.find(selection => selection.activityID === stop.id), { activityID: stop.id, routeID: 'R6' })
    assert.deepEqual(validateProcessTrainSelections(source, catalog, selections), [])
})

test('anchors require a dwelling route, and empty stop choices cannot fall back to tracks', () => {
    const { source, catalog, stop } = fixture()
    source.anchors[0].routeIDs = ['R1']
    assert.ok(validateTemplate(source, catalog).length)
    stop.routeList = []; stop.selectedRoute = null
    assert.ok(validateTemplate(source, catalog).some(message => message.includes('请选择备选进路')))
})

test('one anchor accepts multiple dwelling routes and matches endpoints from any of them', () => {
    const { source, catalog } = fixture()
    catalog.routes.push({ id: 'R6', name: '另一股道停留', type: 'Dwelling', startNodeID: 'N5', endNodeID: 'N6' })
    const anchor = source.anchors[0]
    anchor.routeIDs = ['R6', 'R5']
    assert.deepEqual(validateTemplate(source, catalog), [])
    for (const node of ['N2', 'N3', 'N5', 'N6']) assert.equal(anchorConnectsNodes(anchor, [node], catalog), true)
    assert.equal(anchorConnectsNodes(anchor, ['N1', 'N4'], catalog), false)
    const loco = source.activities.find(activity => activity.type === 'Locomotive')
    source.events.find(event => event.id === loco.startEvent).anchorList = [anchor.id]
    source.routeAnchors.find(binding => binding.routeID === 'R4').startAnchor = anchor.id
    assert.deepEqual(validateTemplate(source, catalog), [])
    anchor.routeIDs = ['R5']
    assert.ok(validateTemplate(source, catalog).some(message => message.includes('不连接任何候选节点')))
})

test('anchor route lists reject empty, duplicate, missing and non-dwelling routes', () => {
    for (const routeIDs of [[], ['R5', 'R5'], ['R5', 'missing'], ['R5', 'R1']]) {
        const { source, catalog } = fixture()
        source.anchors[0].routeIDs = routeIDs
        assert.ok(validateTemplate(source, catalog).length, JSON.stringify(routeIDs))
    }
})

function automaticFixture() {
    const result = fixture()
    result.source.anchors = []
    result.source.routeAnchors = []
    result.source.precedences = []
    for (const event of result.source.events) { event.anchorList = []; event.selectedAnchor = null }
    return result
}

test('dwelling creation owns one stable anchor with exactly its candidate routes and read-only event anchors', () => {
    const { source, stop } = automaticFixture()
    stop.routeList = []
    syncDwellingAnchors(source)
    const anchor = source.anchors.find(anchor => anchor.dwellingActivityID === stop.id)
    assert.ok(anchor)
    assert.deepEqual(anchor.routeIDs, [])
    stop.routeList = ['R5', 'R6']
    syncDwellingAnchors(source)
    assert.deepEqual(anchor.routeIDs, stop.routeList)
    for (const event of source.events.filter(event => [stop.startEvent, stop.endEvent].includes(event.id))) {
        assert.deepEqual(event.anchorList, [anchor.id])
        event.anchorList = ['tampered']; event.selectedAnchor = 'tampered'
    }
    anchor.routeIDs = ['tampered']
    syncDwellingAnchors(source)
    assert.deepEqual(anchor.routeIDs, ['R5', 'R6'])
    assert.ok(source.events.filter(event => [stop.startEvent, stop.endEvent].includes(event.id))
        .every(event => event.selectedAnchor === null && event.anchorList[0] === anchor.id && event.anchorList.length === 1))
    const before = JSON.stringify(source)
    syncDwellingAnchors(source)
    assert.equal(JSON.stringify(source), before)
    const loaded = structuredClone(source)
    syncDwellingAnchors(loaded)
    assert.equal(JSON.stringify(loaded), before)
})

test('direct dwelling connections add and remove only automatic anchors on the corresponding events', () => {
    const { source, stop, catalog } = automaticFixture()
    const arrival = source.activities.find(activity => activity.type === 'Arrival')
    const departure = source.activities.find(activity => activity.type === 'Departure')
    const arrivalEnd = source.events.find(event => event.id === arrival.endEvent)
    const departureStart = source.events.find(event => event.id === departure.startEvent)
    source.anchors.push({ id: 'manual', name: '手工锚', routeIDs: ['R5'] })
    arrivalEnd.anchorList = ['manual']
    source.precedences = [
        { id: 'enter', leadingEvent: arrival.endEvent, followingEvent: stop.startEvent, interval: 0 },
        { id: 'leave', leadingEvent: stop.endEvent, followingEvent: departure.startEvent, interval: 0 },
    ]
    syncDwellingAnchors(source)
    const anchor = source.anchors.find(anchor => anchor.dwellingActivityID === stop.id)
    assert.deepEqual(arrivalEnd.anchorList, ['manual', anchor.id])
    assert.deepEqual(departureStart.anchorList, [anchor.id])
    assert.deepEqual(source.events.find(event => event.id === arrival.startEvent).anchorList, [])
    assert.deepEqual(validateTemplate(source, catalog), [])
    source.precedences = source.precedences.filter(link => link.id !== 'enter')
    syncDwellingAnchors(source)
    assert.deepEqual(arrivalEnd.anchorList, ['manual'])
    assert.deepEqual(arrivalEnd.automaticAnchorIDs, [])
    assert.deepEqual(departureStart.anchorList, [anchor.id])
    removeActivity(source, stop.id)
    syncDwellingAnchors(source)
    assert.deepEqual(departureStart.anchorList, [])
    assert.deepEqual(source.anchors.map(anchor => anchor.id), ['manual'])
})

test('reconnection, multiple dwelling connections and shared events follow their dwelling owners', () => {
    const { source, stop } = automaticFixture()
    const arrival = source.activities.find(activity => activity.type === 'Arrival')
    const second = { ...stop, id: 'second-stop', name: '另一次停留', startEvent: 'second-start', endEvent: 'second-end', routeList: ['R6'] }
    source.activities.push(second)
    for (const id of [second.startEvent, second.endEvent]) source.events.push({ id, name: id, time: null, nodeID: null, anchorList: [], selectedAnchor: null })
    source.precedences = [{ id: 'enter', leadingEvent: arrival.endEvent, followingEvent: stop.startEvent, interval: 0 }]
    syncDwellingAnchors(source)
    const firstID = source.anchors.find(anchor => anchor.dwellingActivityID === stop.id).id
    const secondID = source.anchors.find(anchor => anchor.dwellingActivityID === second.id).id
    const arrivalEnd = source.events.find(event => event.id === arrival.endEvent)
    source.precedences[0].followingEvent = second.startEvent
    syncDwellingAnchors(source)
    assert.deepEqual(arrivalEnd.anchorList, [secondID])
    source.precedences.push({ id: 'enter-again', leadingEvent: arrival.endEvent, followingEvent: stop.startEvent, interval: 0 })
    syncDwellingAnchors(source)
    assert.deepEqual(arrivalEnd.anchorList, [secondID, firstID])
    second.type = 'Shunting'
    syncDwellingAnchors(source)
    assert.deepEqual(arrivalEnd.anchorList, [firstID])
    assert.equal(source.anchors.some(anchor => anchor.id === secondID), false)
    source.precedences = []
    arrival.endEvent = stop.startEvent
    syncDwellingAnchors(source)
    assert.deepEqual(source.events.find(event => event.id === arrival.endEvent).anchorList, [firstID])
})

test('legacy dwelling anchors expand to all routes and stale endpoint bindings do not override connections', () => {
    const { source, stop, catalog } = fixture()
    const originalID = source.anchors[0].id
    catalog.routes.push({ ...catalog.routes.find(route => route.id === 'R5'), id: 'R6' })
    stop.routeList.push('R6')
    syncDwellingAnchors(source)
    assert.equal(source.anchors.find(anchor => anchor.dwellingActivityID === stop.id).id, originalID)
    assert.deepEqual(source.anchors[0].routeIDs, ['R5', 'R6'])
    source.anchors.push({ id: 'old-bound', name: '旧端点锚', routeIDs: ['R5'] })
    source.routeAnchors.find(binding => binding.routeID === 'R5').startAnchor = 'old-bound'
    syncDwellingAnchors(source)
    assert.equal(source.routeAnchors.find(binding => binding.routeID === 'R5').startAnchor, null)
    assert.deepEqual(validateTemplate(source, catalog), [])
    stop.routeList = ['R6']; stop.selectedRoute = null
    syncDwellingAnchors(source)
    assert.equal(source.routeAnchors.find(binding => binding.routeID === 'R5').endAnchor, null)
    assert.deepEqual(source.anchors[0].routeIDs, ['R6'])
    assert.deepEqual(validateTemplate(source, catalog), [])
})
