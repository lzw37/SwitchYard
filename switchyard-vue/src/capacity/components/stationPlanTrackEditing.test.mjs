import assert from 'node:assert/strict'
import test from 'node:test'
import { editStationPlanTrack, previewStationPlanTrackEdit } from './stationPlanEditing.ts'
import { buildStationPlanTrains, stationPlanTrainPath } from './stationPlanView.ts'
import { planOperationPlanTrackEdit, resolveOperationPlanTrackSource } from './operationPlanTrackEditing.ts'
import { StationPlanMovementAction } from './stationPlanActions.ts'
import { ActionStack } from '../../utils/actionStack.ts'

const tracks = [
    { id: 'T1', name: '1道', fromNodeID: 'B', toNodeID: 'C' },
    { id: 'T2', name: '2道', fromNodeID: 'D', toNodeID: 'E' },
]
const rows = ['A', 'B', 'C', 'D', 'E', 'Z', 'L', 'Q', 'U', 'V'].map(id => ({ key: `node:${id}`, sourceID: id, label: id, kind: 'node' }))
const makeRoute = (id, type, startNodeID, endNodeID, linkIDs = []) => ({
    id, name: id, type, startNodeID, endNodeID, cellIDs: [`cell-${id}`], linkIDs,
})
function fixture() {
    const routes = [
        makeRoute('arr1', 'Arrival', 'A', 'B'), makeRoute('arr2', 'Arrival', 'A', 'D'),
        makeRoute('stay1', 'Dwelling', 'B', 'C', ['T1']), makeRoute('stay2', 'Dwelling', 'D', 'E', ['T2']),
        makeRoute('dep1', 'Departure', 'C', 'Z'), makeRoute('dep2', 'Departure', 'E', 'Z'),
        makeRoute('loco1', 'Locomotive', 'C', 'L'), makeRoute('loco2', 'Locomotive', 'E', 'L'),
        makeRoute('shunt1', 'Shunting', 'L', 'B'), makeRoute('shunt2', 'Shunting', 'L', 'D'),
        makeRoute('unrelated1', 'Departure', 'C', 'Q'), makeRoute('unrelated2', 'Departure', 'E', 'Q'),
    ]
    const movements = ['loco', 'arr', 'stay', 'unrelated', 'shunt', 'dep'].map((id, index) => ({
        trainID: 'T', movementID: id, name: id, routeID: `${id}1`, routeIDList: `${id}1`, sortOrder: index,
        startMinutes: index * 10, endMinutes: index * 10 + 5,
        cellOccupations: [{ cellID: 'old-cell' }], cellOccupationOverridesJson: '{"old":true}',
    }))
    const activities = movements.map(m => ({ id: m.movementID, type: routes.find(r => r.id === m.routeID).type,
        startEvent: `${m.movementID}:start`, endEvent: `${m.movementID}:end` }))
    const rootEvents = ['arr:end', 'stay:start', 'stay:end', 'dep:start', 'loco:start', 'shunt:end']
    const snapshot = { trainID: 'T', activityMovementMap: Object.fromEntries(movements.map(m => [m.movementID, m.movementID])),
        process: { activities, precedences: [],
            events: activities.flatMap(a => [a.startEvent, a.endEvent]).map(id => ({ id,
                anchorList: rootEvents.includes(id) ? ['anchor1'] : [], selectedAnchor: null })),
            anchors: [{ id: 'anchor1', routeIDs: ['stay1', 'stay2'], dwellingActivityID: 'stay' }], routeAnchors: [] } }
    function build() {
        const train = buildStationPlanTrains([{ id: 'T', label: 'T', color: 'red' }], movements, routes, [snapshot])[0]
        const segment = stationPlanTrainPath(train, rows, 0, 1, 1, { x: 0, y: 0 }).editableSegments.find(s => s.movementID === 'stay')
        return { train, edit: { trainID: 'T', segment, sourceTrackID: 'T1', targetTrackID: 'T2' } }
    }
    return { routes, movements, snapshot, build }
}
const apply = f => { const { train, edit } = f.build(); return editStationPlanTrack(train, edit, f.movements, f.routes, tracks) }
const selections = changes => Object.fromEntries(changes.map(m => [m.movementID, m.routeID]))

test('停留换股道调整所有共锚进路：跨作业次序和时间，保留非锚端点与无关相邻进路', () => {
    const f = fixture(), before = structuredClone({ movements: f.movements, snapshot: f.snapshot })
    f.movements.push({ ...f.movements[0], trainID: 'other' })
    const changes = apply(f)
    assert.deepEqual(selections(changes), { loco: 'loco2', arr: 'arr2', stay: 'stay2', shunt: 'shunt2', dep: 'dep2' })
    for (const changed of changes) {
        const original = f.movements.find(m => m.trainID === 'T' && m.movementID === changed.movementID)
        assert.equal(changed.startMinutes, original.startMinutes)
        assert.equal(changed.endMinutes, original.endMinutes)
        assert.deepEqual(changed.cellOccupations, [])
        assert.equal(changed.cellOccupationOverridesJson, '{}')
        const route = f.routes.find(r => r.id === changed.routeID)
        assert.equal(changed.startNodeID, route.startNodeID)
        assert.equal(changed.endNodeID, route.endNodeID)
    }
    assert.deepEqual(f.movements.slice(0, -1), before.movements)
    assert.deepEqual(f.snapshot, before.snapshot)
})

test('拖动预览使用同一组实际匹配进路，包含非连续的调车和机车端点', () => {
    const f = fixture(), { train, edit } = f.build(), before = structuredClone(train)
    const preview = previewStationPlanTrackEdit(train, edit, f.movements, f.routes, tracks)
    const nodes = Object.fromEntries(preview.visits.map(v => [v.eventID, v.rowKey]))
    assert.equal(nodes['loco:start'], 'node:E'); assert.equal(nodes['loco:end'], 'node:L')
    assert.equal(nodes['shunt:start'], 'node:L'); assert.equal(nodes['shunt:end'], 'node:D')
    assert.equal(nodes['unrelated:start'], 'node:C')
    assert.deepEqual(train, before)
})

test('任一共锚进路无目标方案，整批取消并指出缺失作业', () => {
    const f = fixture()
    f.routes = f.routes.filter(r => r.id !== 'loco2')
    // The captured geometry is built from the same unchanged source routes.
    const before = structuredClone(f.movements)
    assert.throws(() => apply(f), error => error.reason === 'missing-route' && error.movementNames.includes('loco'))
    assert.deepEqual(f.movements, before)
})

test('以当前计划进路判断股道，换道后可再次换回，源过程及其原选定进路不改写', () => {
    const f = fixture(), changes = apply(f)
    for (const changed of changes) Object.assign(f.movements.find(m => m.movementID === changed.movementID), changed)
    const { train, edit } = f.build()
    const returned = editStationPlanTrack(train, { ...edit, sourceTrackID: 'T2', targetTrackID: 'T1' }, f.movements, f.routes, tracks)
    assert.deepEqual(selections(returned), { loco: 'loco1', arr: 'arr1', stay: 'stay1', shunt: 'shunt1', dep: 'dep1' })
})

test('按明确选定锚和当前进路端点锚绑定处理，不跨候选锚扩散到另一组', () => {
    const f = fixture()
    f.snapshot.process.anchors.push({ id: 'other-anchor', routeIDs: ['stay1'] })
    const event = id => f.snapshot.process.events.find(e => e.id === id)
    event('loco:start').anchorList = ['anchor1', 'other-anchor']
    event('loco:start').selectedAnchor = 'other-anchor'
    event('shunt:end').anchorList = []
    f.snapshot.process.routeAnchors.push({ routeID: 'shunt1', startAnchor: null, endAnchor: 'anchor1' })
    event('unrelated:start').anchorList = ['other-anchor']
    assert.deepEqual(selections(apply(f)), { arr: 'arr2', stay: 'stay2', shunt: 'shunt2', dep: 'dep2' })
})

test('旧锚没有停留所有者时仍按停留起终事件的锚联动', () => {
    const f = fixture()
    delete f.snapshot.process.anchors[0].dwellingActivityID
    assert.equal(apply(f).length, 5)
})

test('事件有多个备选锚时，只移动当前属于源股道的事件，不合并另一停靠组', () => {
    const f = fixture()
    f.snapshot.process.anchors.push({ id: 'other-anchor', routeIDs: ['other-stay'] })
    f.routes.push(makeRoute('other-stay', 'Dwelling', 'U', 'V'), makeRoute('other-loco', 'Locomotive', 'U', 'L'))
    f.movements.find(m => m.movementID === 'loco').routeID = 'other-loco'
    f.snapshot.process.events.find(e => e.id === 'loco:start').anchorList = ['anchor1', 'other-anchor']
    f.snapshot.process.events.find(e => e.id === 'shunt:end').anchorList = ['anchor1', 'other-anchor']
    assert.deepEqual(selections(apply(f)), { arr: 'arr2', stay: 'stay2', shunt: 'shunt2', dep: 'dep2' })
})

test('目标停留进路反向时按真实进路端点预览和保存，不按股道端点序号映射', () => {
    const f = fixture(), stay = f.routes.find(r => r.id === 'stay2')
    stay.startNodeID = 'E'; stay.endNodeID = 'D'
    const { train, edit } = f.build(), preview = previewStationPlanTrackEdit(train, edit, f.movements, f.routes, tracks)
    assert.equal(preview.visits.find(v => v.eventID === 'stay:start').rowKey, 'node:E')
    assert.equal(preview.visits.find(v => v.eventID === 'stay:end').rowKey, 'node:D')
    const changed = apply(f).find(m => m.movementID === 'stay')
    assert.equal(changed.startNodeID, 'E'); assert.equal(changed.endNodeID, 'D')
})

test('共用一个事件的活动端点也联动；相同时刻、位置或次序关系本身不替代锚', () => {
    const f = fixture()
    f.snapshot.process.activities.find(a => a.id === 'unrelated').startEvent = 'stay:end'
    assert.equal(selections(apply(f)).unrelated, 'unrelated2')
    f.snapshot.process.activities.find(a => a.id === 'unrelated').startEvent = 'unrelated:start'
    f.snapshot.process.precedences.push({ id: 'p', leadingEvent: 'stay:end', followingEvent: 'unrelated:start', isExact: true, interval: 0 })
    assert.equal(selections(apply(f)).unrelated, undefined)
})

test('图中未显示的共锚进路仍被调整；受影响的作业缺失时不能部分保存', () => {
    const f = fixture(), { train, edit } = f.build()
    // The hidden route follows the selected stay, so its absence does not change the captured indexes.
    train.visits = train.visits.filter(v => v.movementID !== 'shunt')
    assert.equal(selections(editStationPlanTrack(train, edit, f.movements, f.routes, tracks)).shunt, 'shunt2')
    assert.throws(() => editStationPlanTrack(train, edit, f.movements.filter(m => m.movementID !== 'shunt'), f.routes, tracks), /stale/)
})

test('源抓手过期或目标股道不存在时拒绝；同股道不产生修改', () => {
    const f = fixture(), { train, edit } = f.build()
    assert.deepEqual(editStationPlanTrack(train, { ...edit, targetTrackID: 'T1' }, f.movements, f.routes, tracks), [])
    assert.throws(() => editStationPlanTrack(train, { ...edit, targetTrackID: 'unknown' }, f.movements, f.routes, tracks), /stale/)
    f.movements.find(m => m.movementID === 'stay').endMinutes++
    assert.throws(() => editStationPlanTrack(train, edit, f.movements, f.routes, tracks), /stale/)
})

test('无过程快照的计划继续使用连续停靠关系', () => {
    const f = fixture(), { train, edit } = f.build()
    delete train.processConstraints
    assert.deepEqual(selections(editStationPlanTrack(train, edit, f.movements, f.routes, tracks)),
        { arr: 'arr2', stay: 'stay2', unrelated: 'unrelated2' })
})

test('资源占用图复用相同的锚联动换道规则', () => {
    const f = fixture(), { train } = f.build(), cells = [{ id: 'C1', linkIDs: ['T1'] }, { id: 'C2', linkIDs: ['T2'] }]
    const source = resolveOperationPlanTrackSource(train, 'stay', 'C1', f.movements, f.routes, tracks, cells)
    assert.ok(source)
    const plan = planOperationPlanTrackEdit(train, source, 'C2', f.movements, f.routes, tracks, cells)
    assert.deepEqual(selections(plan.changed), selections(apply(f)))
})

test('共锚换道整批保存和单步撤销／重做，保存失败不消耗历史动作', async () => {
    const f = fixture(), after = apply(f), before = after.map(m => f.movements.find(row => row.movementID === m.movementID))
    let stored = structuredClone(before), calls = 0
    const save = async (expected, desired) => {
        assert.deepEqual(expected, stored)
        assert.equal(desired.length, 5)
        stored = structuredClone(desired); calls++; return stored
    }
    const history = new ActionStack()
    await history.execute(new StationPlanMovementAction('track', before, after, save))
    assert.deepEqual(stored, after); assert.equal(history.undoCount, 1)
    await history.undo(); assert.deepEqual(stored, before)
    await history.redo(); assert.deepEqual(stored, after); assert.equal(calls, 3)
    await assert.rejects(() => history.execute(new StationPlanMovementAction('track', after, before, async () => { throw Error('conflict') })), /conflict/)
    assert.equal(history.undoCount, 1)
})
