import assert from 'node:assert/strict'
import test from 'node:test'
import { findOccupationConflicts, conflictMovementKey, conflictOccupationKey } from './operationPlanConflicts.ts'
import { buildStationPlanTrains, stationPlanTrainPath } from './components/stationPlanView.ts'

const occupation = (trainID, movementID, cellID, startSeconds, endSeconds) => ({
    key: conflictOccupationKey(trainID, movementID, cellID), trainID, movementID, cellID, startSeconds, endSeconds,
})
const movement = (result, trainID, movementID) => result.movements.get(conflictMovementKey(trainID, movementID))

test('同一 Cell 的实际重叠使两个作业和占用块使用同一颜色', () => {
    const a = occupation('T1', 'M1', 'C1', 60, 180), b = occupation('T2', 'M2', 'C1', 120, 240)
    const result = findOccupationConflicts([a, b])
    assert.equal(result.movements.size, 2)
    assert.equal(result.occupations.size, 2)
    const conflict = movement(result, 'T1', 'M1')
    assert.equal(conflict.color, movement(result, 'T2', 'M2').color)
    assert.equal(conflict.color, result.occupations.get(a.key).color)
    assert.equal(conflict.color, result.occupations.get(b.key).color)
    assert.deepEqual(conflict.details, [{ trainID: 'T2', movementID: 'M2', cellID: 'C1', startSeconds: 120, endSeconds: 180 }])
})

test('相邻边界、不同 Cell、零时长及无效时段不产生冲突', () => {
    const result = findOccupationConflicts([
        occupation('A', '1', 'C', 60, 120), occupation('B', '1', 'C', 120, 180),
        occupation('C', '1', 'other', 60, 180), occupation('D', '1', 'C', 90, 90),
        occupation('E', '1', 'C', 180, 60), occupation('F', '1', 'C', NaN, 180),
        occupation('G', '1', 'C', 60, Infinity), occupation('H', '1', '', 60, 180),
    ])
    assert.equal(result.movements.size, 0)
    assert.equal(result.occupations.size, 0)
})

test('不同作业即使属于同一列车仍判冲突，同一作业不与自身冲突', () => {
    const a = occupation('T', 'M1', 'C', 60, 180)
    assert.equal(findOccupationConflicts([a, { ...a, key: 'duplicate' }]).movements.size, 0)
    assert.equal(findOccupationConflicts([a, occupation('T', 'M2', 'C', 100, 180)]).movements.size, 2)
})

test('连锁冲突跨 Cell 共享颜色，提示只包括直接冲突作业', () => {
    const result = findOccupationConflicts([
        occupation('A', '1', 'C1', 0, 20), occupation('B', '1', 'C1', 10, 30),
        occupation('B', '1', 'C2', 30, 50), occupation('C', '1', 'C2', 40, 60),
        occupation('A', '1', 'safe', 0, 100),
    ])
    assert.equal(movement(result, 'A', '1').color, movement(result, 'C', '1').color)
    assert.deepEqual(movement(result, 'A', '1').details.map(row => row.trainID), ['B'])
    assert.deepEqual(movement(result, 'B', '1').details.map(row => row.trainID), ['A', 'C'])
    assert.ok(!result.occupations.has(conflictOccupationKey('A', '1', 'safe')))
})

test('包含关系和多列车同时占用均检测到，不仅比较相邻区间', () => {
    const result = findOccupationConflicts([
        occupation('A', '1', 'C', 0, 100), occupation('B', '1', 'C', 10, 20),
        occupation('C', '1', 'C', 30, 40), occupation('D', '1', 'C', 35, 50),
    ])
    assert.deepEqual(movement(result, 'A', '1').details.map(row => row.trainID), ['B', 'C', 'D'])
    assert.deepEqual(movement(result, 'C', '1').details.map(row => row.trainID), ['A', 'D'])
})

test('跨日、负时间和秒级精度按绝对时刻比较', () => {
    const result = findOccupationConflicts([
        occupation('A', '1', 'C', 86390, 86401.5), occupation('B', '1', 'C', 86401, 86410),
        occupation('C', '1', 'C', -60, 10), occupation('D', '1', 'C', -5, 0),
    ])
    assert.equal(movement(result, 'A', '1').details[0].endSeconds - movement(result, 'A', '1').details[0].startSeconds, 0.5)
    assert.equal(movement(result, 'C', '1').details[0].startSeconds, -5)
})

test('重排输入不会改变分组、颜色或提示，且不修改输入', () => {
    const rows = [occupation('T1', 'a', 'C1', 0, 10), occupation('T2', 'a', 'C1', 5, 15),
        occupation('T3', 'b', 'C2', 0, 10), occupation('T4', 'b', 'C2', 5, 15)]
    const original = structuredClone(rows)
    const summarize = result => [...result.movements].sort(([a], [b]) => a.localeCompare(b))
    assert.deepEqual(summarize(findOccupationConflicts(rows)), summarize(findOccupationConflicts([...rows].reverse())))
    assert.deepEqual(rows, original)
    assert.notEqual(movement(findOccupationConflicts(rows), 'T1', 'a').color, movement(findOccupationConflicts(rows), 'T3', 'b').color)
})

test('复合字符串 ID 不串作业，重复占用不重复列出同一冲突', () => {
    assert.notEqual(conflictMovementKey('a-b', 'c'), conflictMovementKey('a', 'b-c'))
    assert.notEqual(conflictOccupationKey('a-b', 'c', 'd'), conflictOccupationKey('a', 'b-c', 'd'))
    const a = occupation('a-b', 'c', 'C', 0, 10), b = occupation('a', 'b-c', 'C', 5, 15)
    const result = findOccupationConflicts([a, b, { ...b }])
    assert.equal(movement(result, 'a-b', 'c').details.length, 1)
})

test('保存后改变时间或删除作业会消除冲突', () => {
    const a = occupation('A', '1', 'C', 0, 10), b = occupation('B', '1', 'C', 5, 15)
    assert.equal(findOccupationConflicts([a, b]).movements.size, 2)
    assert.equal(findOccupationConflicts([a, { ...b, startSeconds: 10 }]).movements.size, 0)
    assert.equal(findOccupationConflicts([a]).movements.size, 0)
})

test('大表按作业返回准确折线路径，支持直线、直角和隐藏节点', () => {
    const routes = [{ id: 'R1', name: 'arrival', startNodeID: 'N1', endNodeID: 'N2' },
        { id: 'R2', name: 'departure', startNodeID: 'N2', endNodeID: 'N3' }]
    const train = buildStationPlanTrains([{ id: 'T', label: 'T', color: '#333' }], [
        { trainID: 'T', movementID: 'M1', routeID: 'R1', name: 'arrival', startMinutes: 1, endMinutes: 2 },
        { trainID: 'T', movementID: 'M2', routeID: 'R2', name: 'departure', startMinutes: 3, endMinutes: 4 },
    ], routes)[0]
    const rows = ['N1', 'N2', 'N3'].map(id => ({ key: `node:${id}`, sourceID: id, kind: 'node', label: id }))
    for (const mode of ['straight', 'orthogonal']) {
        const result = stationPlanTrainPath(train, rows, 0, 10, 20, { x: 0, y: 0 }, mode)
        assert.deepEqual(result.movementPaths.map(row => row.movementID), ['M1', 'M2'])
        assert.equal(result.movementPaths.map(row => row.path).join(''), result.path)
        assert.equal(result.movementPaths[0].path, mode === 'straight' ? 'M 10 10 L 20 30 ' : 'M 10 10 L 20 10 L 20 30 ')
        const hidden = stationPlanTrainPath(train, rows.slice(1), 0, 10, 20, { x: 0, y: 0 }, mode)
        assert.deepEqual(hidden.movementPaths.map(row => row.movementID), ['M2'])
    }
})

test('大量互不重叠占用不产生冲突', () => {
    const rows = Array.from({ length: 10000 }, (_, index) => occupation(`T${index}`, 'M', 'C', index * 10, index * 10 + 5))
    assert.equal(findOccupationConflicts(rows).movements.size, 0)
})
