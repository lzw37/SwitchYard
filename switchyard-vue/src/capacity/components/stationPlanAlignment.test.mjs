import assert from 'node:assert/strict'
import test from 'node:test'
import { alignStationPlanEvents, StationPlanAlignmentError } from './stationPlanAlignment.ts'
import { buildStationPlanTrains, stationPlanTrainPath, stationPlanTimeLabel } from './stationPlanView.ts'
import { StationPlanMovementAction } from './stationPlanActions.ts'
import { ActionStack } from '../../utils/actionStack.ts'

const rows = ['A', 'B'].map(id => ({ key: `node:${id}`, sourceID: id, label: id, kind: 'node' }))
const relation = (leadingEvent, followingEvent, minutes = 0, isExact = true) => ({
    id: `${leadingEvent}/${followingEvent}`, leadingEvent, followingEvent, minIntervalSeconds: minutes * 60, isExact,
})
function fixture(specs, precedences) {
    const movements = specs.map(([movementID, startMinutes, endMinutes, hidden], index) => ({
        trainID: 'T', movementID, name: movementID, routeID: '', startMinutes, endMinutes,
        sortOrder: index, startNodeID: hidden ? '' : 'A', endNodeID: hidden ? '' : 'B',
    }))
    const snapshot = { trainID: 'T', activityMovementMap: Object.fromEntries(movements.map(m => [m.movementID, m.movementID])),
        precedences, process: { activities: movements.map(m => ({ id: m.movementID, startEvent: `${m.movementID}:start`, endEvent: `${m.movementID}:end` })),
            precedences: precedences.map(p => ({ ...p, interval: p.minIntervalSeconds / 60 })) } }
    const select = (movementID, mode = 'end') => {
        const train = buildStationPlanTrains([{ id: 'T', label: 'T', color: 'red' }], movements, [], [snapshot])[0]
        const segment = stationPlanTrainPath(train, rows, 0, 1, 1, { x: 0, y: 0 }).editableSegments.find(s => s.movementID === movementID)
        assert.ok(segment)
        return { train, edit: { trainID: 'T', segment, mode, startMinutes: segment.startMinutes,
            endMinutes: segment.endMinutes, rowKey: segment.rowKey } }
    }
    return { movements, snapshot, select }
}
const align = (f, id, mode = 'end') => {
    const { train, edit } = f.select(id, mode)
    return alignStationPlanEvents(train, edit, f.movements, f.snapshot)
}
const times = changed => Object.fromEntries(changed.map(m => [m.movementID, [m.startMinutes, m.endMinutes]]))

test('双击固定当前事件：跨列表次序对齐后序，沿活动和次序平移隐藏分支，忽略无关系的更晚事件', () => {
    const f = fixture([['later', 100, 110], ['C', 5, 8, true], ['A', 0, 10], ['B', 15, 20]], [
        relation('A:end', 'B:start', 2), relation('B:end', 'C:start', 1, false),
    ])
    f.movements.push({ ...f.movements[0], trainID: 'other' })
    const before = structuredClone(f.movements)
    assert.deepEqual(times(align(f, 'A')), { B: [12, 17], C: [2, 5] })
    assert.deepEqual(f.movements, before)
})

test('双击后序事件按间隔反向对齐前序，只沿明确关系联动，回到基准事件时停止平移', () => {
    const f = fixture([['A', 0, 10], ['B', 20, 25], ['branch', 30, 40]], [
        relation('A:end', 'B:start', 2), relation('A:end', 'branch:start', 0, false),
    ])
    assert.deepEqual(times(align(f, 'B', 'start')), { A: [0, 18], branch: [38, 48] })
})

test('开始到开始、结束到结束、零间隔和连续严格次序均以当前事件为基准', () => {
    const f = fixture([['A', 10, 20], ['B', 30, 40], ['C', 50, 60]], [
        relation('A:start', 'B:start'), relation('B:start', 'C:end', 3),
    ])
    assert.deepEqual(times(align(f, 'A', 'start')), { B: [10, 20], C: [50, 13] })
    const g = fixture([['A', 10, 20], ['B', 30, 40]], [relation('A:end', 'B:end', 2)])
    assert.deepEqual(times(align(g, 'A')), { B: [30, 22] })
})

test('分支汇合和有向环的同一事件只平移一次，结果与记录顺序无关', () => {
    const f = fixture([['A', 0, 10], ['B', 20, 25], ['C', 30, 35], ['D', 40, 45]], [
        relation('A:end', 'B:start'), relation('B:start', 'C:start', 0, false),
        relation('B:end', 'D:start', 0, false), relation('C:end', 'D:start', 0, false),
        relation('D:end', 'C:start', 0, false),
    ])
    const expected = { B: [10, 15], C: [20, 25], D: [30, 35] }
    assert.deepEqual(times(align(f, 'A')), expected)
    f.movements.reverse()
    f.snapshot.precedences.reverse()
    assert.deepEqual(times(align(f, 'A')), expected)
})

test('共用事件同时更新各活动端点，后续事件不重复平移', () => {
    const f = fixture([['A', 0, 10], ['B', 20, 30], ['C', 30, 40]], [relation('A:end', 'B:start')])
    f.snapshot.process.activities[2].startEvent = 'B:end'
    assert.deepEqual(times(align(f, 'A')), { B: [10, 20], C: [20, 30] })
})

test('使用计划保存的秒数和实时事件时刻，兼容旧快照分钟间隔，保留跨日小数秒', () => {
    const f = fixture([['A', 1430, 1439.5], ['B', 1441, 1442]], [relation('A:end', 'B:start', 90.5 / 60)])
    const changed = align(f, 'A')
    assert.equal(stationPlanTimeLabel(changed[0].startMinutes), 'D+1 00:01:00.5')
    assert.equal(stationPlanTimeLabel(changed[0].endMinutes), 'D+1 00:02:00.5')
    delete f.snapshot.precedences
    f.snapshot.process.precedences.forEach(p => delete p.minIntervalSeconds)
    assert.deepEqual(align(f, 'A'), changed)
    const previousDay = fixture([['A', 0, 1], ['B', 0, 0.5]], [relation('A:end', 'B:start', 2)])
    assert.equal(stationPlanTimeLabel(align(previousDay, 'B', 'start')[0].endMinutes), 'D-1 23:58')
})

test('无严格次序、显式空次序、无过程的旧计划或已经对齐均不产生修改', () => {
    for (const exact of [false, undefined]) {
        const p = relation('A:end', 'B:start'); p.isExact = exact
        const f = fixture([['A', 0, 10], ['B', 20, 30]], [p])
        assert.deepEqual(align(f, 'A'), [])
    }
    const f = fixture([['A', 0, 10], ['B', 10, 20]], [relation('A:end', 'B:start')])
    assert.deepEqual(align(f, 'A'), [])
    f.movements[1].startMinutes = 15
    f.snapshot.precedences = []
    assert.deepEqual(align(f, 'A'), [])
    const { train, edit } = f.select('A')
    assert.deepEqual(alignStationPlanEvents(train, edit, f.movements), [])
})

test('过期抓手、缺失的受影响活动或无效时刻整次拒绝，不能跳过隐藏活动', () => {
    const f = fixture([['A', 0, 10], ['B', 20, 30, true]], [relation('A:end', 'B:start')])
    const { train, edit } = f.select('A')
    f.movements[0].endMinutes++
    assert.throws(() => alignStationPlanEvents(train, edit, f.movements, f.snapshot), /stale/)
    f.movements[0].endMinutes--
    f.movements[1].endMinutes = null
    assert.throws(() => align(f, 'A'), /stale/)
    f.movements.pop()
    assert.throws(() => align(f, 'A'), /stale/)
})

test('矛盾严格次序或不同平移量汇合时拒绝整次修改', () => {
    const f = fixture([['A', 0, 10], ['B', 20, 30], ['C', 40, 50]], [
        relation('A:end', 'B:start'), relation('A:end', 'B:start', 1),
    ])
    assert.throws(() => align(f, 'A'), StationPlanAlignmentError)
    f.snapshot.precedences = [relation('A:end', 'B:start'), relation('A:end', 'C:start'),
        relation('B:end', 'C:end', 0, false)]
    const before = structuredClone(f.movements)
    assert.throws(() => align(f, 'A'), StationPlanAlignmentError)
    assert.deepEqual(f.movements, before)
})

test('一次对齐批量保存，单步撤销／重做恢复所有变化且保存失败不进入历史', async () => {
    const f = fixture([['A', 0, 10], ['B', 20, 30], ['C', 40, 50]], [
        relation('A:end', 'B:start', 2), relation('B:end', 'C:start', 0, false),
    ])
    const after = align(f, 'A'), before = after.map(m => f.movements.find(row => row.movementID === m.movementID))
    let stored = structuredClone(before), calls = 0
    const save = async (expected, desired) => {
        assert.deepEqual(expected, stored)
        stored = structuredClone(desired); calls++; return stored
    }
    const history = new ActionStack()
    await history.execute(new StationPlanMovementAction('align', before, after, save))
    assert.deepEqual(stored, after); assert.equal(history.undoCount, 1)
    await history.undo(); assert.deepEqual(stored, before)
    await history.redo(); assert.deepEqual(stored, after); assert.equal(calls, 3)
    await assert.rejects(() => history.execute(new StationPlanMovementAction('align', after, before, async () => { throw Error('conflict') })), /conflict/)
    assert.equal(history.undoCount, 1)
})
