import assert from 'node:assert/strict'
import test from 'node:test'
import { editStationPlanDwelling, resolveStationPlanDwellingTarget } from './stationPlanDwelling.ts'
import { buildStationPlanTrains, stationPlanTrainPath, stationPlanTimeLabel } from './stationPlanView.ts'
import { StationPlanMovementAction } from './stationPlanActions.ts'
import { ActionStack } from '../../utils/actionStack.ts'

const routes = [
    { id: 'arr', name: 'arrival', type: 'arrival', startNodeID: 'A', endNodeID: 'B', cellIDs: [] },
    { id: 'dw', name: 'stay', type: 'dwelling', startNodeID: 'B', endNodeID: 'C', cellIDs: [] },
    { id: 'dep', name: 'departure', type: 'departure', startNodeID: 'C', endNodeID: 'D', cellIDs: [] },
]
const tracks = [{ id: 'track', name: '1道', fromNodeID: 'B', toNodeID: 'C' }]
const rows = ['A', 'B', 'C', 'D'].map(id => ({ key: `node:${id}`, sourceID: id, label: id, kind: 'node' }))
function fixture(start = 480, duration = 5, routeLess = false) {
    const make = (movementID, routeID, startMinutes, endMinutes, sortOrder) => ({
        trainID: 'T', movementID, name: movementID, routeID, startMinutes, endMinutes, sortOrder,
    })
    // Deliberately shuffled, with a following movement that has no visible graph endpoints.
    const movements = [make('dep', 'dep', start + duration, start + duration + 10, 2),
        make('hidden', '', start + duration + 13, start + duration + 15, 3),
        { ...make('other', 'dep', start, start + 10, 0), trainID: 'other' },
        make('arr', 'arr', start - 10, start, 0),
        { ...make('stay', routeLess ? '' : 'dw', start, start + duration, 1), startNodeID: 'B', endNodeID: 'C' }]
    const train = buildStationPlanTrains([{ id: 'T', label: 'T', color: 'red' }], movements, routes)[0]
    const segments = stationPlanTrainPath(train, rows, 0, 1, 1, { x: 0, y: 0 }).editableSegments
    const segment = segments.find(segment => segment.movementID === 'stay')
    const dwellingIDs = routeLess ? ['stay'] : []
    const target = resolveStationPlanDwellingTarget(train, segment, 'end', movements, routes, tracks, dwellingIDs)
    assert.ok(target)
    return { movements, train, segments, target, dwellingIDs }
}
const edit = f => editStationPlanDwelling(f.train, f.target, f.movements, routes, tracks, f.dwellingIDs)
const times = changed => changed.map(row => [row.movementID, row.startMinutes, row.endMinutes])

test('到开变通过：按作业顺序提前后续时间，保持时长、间隔和其他列车不变', () => {
    const f = fixture(), before = structuredClone(f.movements)
    assert.deepEqual(times(edit(f)), [['stay', 480, 480], ['dep', 480, 490], ['hidden', 493, 495]])
    assert.deepEqual(f.movements, before)
})
test('通过变到开：保留现有默认两分钟停留，并顺延所有后续作业', () => {
    const f = fixture(480, 0)
    assert.deepEqual(times(edit(f)), [['stay', 480, 482], ['dep', 482, 492], ['hidden', 495, 497]])
})
test('跨日和秒级时间按相同差值平移', () => {
    const f = fixture(1439.5, 0)
    const changed = edit(f)
    assert.deepEqual(times(changed), [['stay', 1439.5, 1441.5], ['dep', 1441.5, 1451.5], ['hidden', 1454.5, 1456.5]])
    assert.equal(stationPlanTimeLabel(changed[1].startMinutes), 'D+1 00:01:30')
})
test('无进路的过程停留活动也平移后续作业', () => {
    assert.deepEqual(times(edit(fixture(480, 5, true))), [['stay', 480, 480], ['dep', 480, 490], ['hidden', 493, 495]])
})
test('从接车结束点或发车开始点切换时定位到同一停留活动', () => {
    const f = fixture()
    for (const [movementID, edge] of [['arr', 'end'], ['dep', 'start']]) {
        const target = resolveStationPlanDwellingTarget(f.train, f.segments.find(row => row.movementID === movementID), edge, f.movements, routes, tracks)
        assert.equal(target.movementID, 'stay')
        assert.deepEqual(times(edit({ ...f, target })), times(edit(f)))
    }
})
test('过期的切换目标拒绝修改', () => {
    const f = fixture()
    f.movements.find(row => row.movementID === 'stay').endMinutes++
    assert.throws(() => edit(f), /stale/)
})
test('停留和后续作业作为一个批次保存、撤销和重做', async () => {
    const f = fixture(), changed = edit(f), ids = new Set(changed.map(row => row.movementID))
    const before = changed.map(row => f.movements.find(original => original.movementID === row.movementID))
    let stored = structuredClone(f.movements), calls = 0
    const save = async (expected, desired) => {
        assert.equal(expected.length, 3); assert.equal(desired.length, 3)
        expected.forEach(row => assert.deepEqual(stored.find(item => item.movementID === row.movementID), row))
        stored = stored.map(row => ids.has(row.movementID) ? { ...desired.find(item => item.movementID === row.movementID) } : row)
        calls++; return desired
    }
    const history = new ActionStack()
    await history.execute(new StationPlanMovementAction('dwelling', before, changed, save))
    assert.equal(history.undoCount, 1)
    await history.undo(); assert.deepEqual(stored, f.movements)
    await history.redo(); assert.equal(stored.find(row => row.movementID === 'dep').startMinutes, 480)
    assert.equal(calls, 3)
})
