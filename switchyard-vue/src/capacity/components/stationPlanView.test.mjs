import assert from 'node:assert/strict'
import test from 'node:test'
import { buildStationPlanTrains, previewStationPlanSegmentEdit, stationPlanTrainPath } from './stationPlanView.ts'

const rows = ['A', 'B', 'C', 'D', 'E', 'F'].map(id => ({ key: `node:${id}`, sourceID: id, label: id, kind: 'node' }))
function fixture() {
    const movements = [
        { movementID: 'M1', startNodeID: 'A', endNodeID: 'B', startMinutes: 1, endMinutes: 2 },
        { movementID: 'M2', startNodeID: 'C', endNodeID: 'D', startMinutes: 5, endMinutes: 7 },
        { movementID: 'M3', startNodeID: 'E', endNodeID: 'F', startMinutes: 4, endMinutes: 8 },
    ].map((movement, sortOrder) => ({ ...movement, trainID: 'T', name: movement.movementID, routeID: '', sortOrder }))
    const precedences = [
        { id: 'end-start', leadingEvent: 'M1:end', followingEvent: 'M3:start', minIntervalSeconds: 120, isExact: true },
        { id: 'start-start', leadingEvent: 'M1:start', followingEvent: 'M2:start', minIntervalSeconds: 240 },
        { id: 'end-end', leadingEvent: 'M2:end', followingEvent: 'M3:end', minIntervalSeconds: 60 },
    ]
    const snapshot = { trainID: 'T', activityMovementMap: { a1: 'M1', a2: 'M2', a3: 'M3' }, precedences,
        process: { activities: movements.map((movement, index) => ({ id: `a${index + 1}`,
            startEvent: `${movement.movementID}:start`, endEvent: `${movement.movementID}:end` })),
            precedences: precedences.map(item => ({ ...item, interval: item.minIntervalSeconds / 60 })) } }
    const build = (snapshots = [snapshot]) => buildStationPlanTrains([{ id: 'T', label: 'T', color: 'red' }], movements, [], snapshots)[0]
    return { movements, snapshot, build }
}
const geometry = (train, visibleRows = rows, mode = 'straight') => stationPlanTrainPath(train, visibleRows, 0, 10, 20, { x: 0, y: 0 }, mode)

test('活动独立成线，细线按保存的事件次序连接，支持分支、跨活动及不同事件端点', () => {
    const train = fixture().build()
    for (const mode of ['straight', 'orthogonal']) {
        const result = geometry(train, rows, mode)
        assert.deepEqual(result.movementPaths.map(item => item.movementID), ['M1', 'M2', 'M3'])
        assert.equal(result.path.match(/M /g).length, 3)
        assert.deepEqual(result.connectorPaths, [
            { precedenceID: 'end-start', isExact: true, path: 'M 20 30 L 40 90 ' },
            { precedenceID: 'start-start', isExact: false, path: 'M 10 10 L 50 50 ' },
            { precedenceID: 'end-end', isExact: false, path: 'M 70 70 L 80 110 ' },
        ])
        assert.deepEqual(result.editableSegments.map(segment => [segment.movementID, segment.x1, segment.y1, segment.x2, segment.y2]), [
            ['M1', 10, 10, 20, 30], ['M2', 50, 50, 70, 70], ['M3', 40, 90, 80, 110],
        ])
        assert.equal(result.editableSegments.flatMap(segment => [...segment.startAnchors, ...segment.endAnchors]).length, 6)
    }
})

test('过程没有次序时不根据活动排列补线；旧快照使用其保存的过程次序', () => {
    const f = fixture()
    f.snapshot.precedences = []
    assert.equal(geometry(f.build()).connectorPath, '')
    delete f.snapshot.precedences
    assert.equal(geometry(f.build()).connectorPaths.length, 3)
    // Ordinary plans without a process retain their existing implicit sequence.
    assert.equal(geometry(f.build([])).connectorPath, 'M 20 30 L 50 50 M 70 70 L 40 90 ')
})

test('移动活动抓手时，次序线使用当前事件坐标，不使用生成计划时的旧时刻', () => {
    const train = fixture().build(), before = structuredClone(train)
    const segment = geometry(train).editableSegments[0]
    const preview = previewStationPlanSegmentEdit(train, { trainID: 'T', segment, mode: 'start', shiftFollowing: false,
        startMinutes: 1.5, endMinutes: 2, rowKey: segment.rowKey })
    assert.equal(geometry(preview).connectorPaths[1].path, 'M 15 10 L 50 50 ')
    assert.equal(geometry(preview).connectorPaths[0].path, 'M 20 30 L 40 90 ')
    assert.deepEqual(train, before)
})

test('隐藏事件所在行时不把次序线转接到相邻活动，仍保留可见端点之间的约束', () => {
    const result = geometry(fixture().build(), rows.filter(row => row.sourceID !== 'B'))
    assert.deepEqual(result.movementPaths.map(item => item.movementID), ['M2', 'M3'])
    assert.deepEqual(result.connectorPaths.map(item => item.precedenceID), ['start-start', 'end-end'])
    assert.equal(result.connectorPaths[0].path, 'M 10 10 L 50 30 ')
})

test('共用事件只绘制一次次序线，仍保留每项活动的起止抓手', () => {
    const f = fixture()
    f.snapshot.process.activities[1].startEvent = 'M1:end'
    f.movements[1].startMinutes = 2
    f.movements[1].startNodeID = 'B'
    f.snapshot.precedences = [f.snapshot.precedences[0]]
    const result = geometry(f.build())
    assert.equal(result.connectorPaths.length, 1)
    assert.equal(result.connectorPath, 'M 20 30 L 40 90 ')
    assert.equal(result.editableSegments.length, 3)
    assert.equal(result.editableSegments[0].x2, result.editableSegments[1].x1)
    assert.equal(result.editableSegments[0].y2, result.editableSegments[1].y1)
})

test('股道端点折叠后活动保持独立，约束只映射纵坐标', () => {
    const folded = [{ key: 'track:1', sourceID: '1', label: '1道', kind: 'track', nodeIDs: ['B', 'C'] }, ...rows.filter(row => !['B', 'C'].includes(row.sourceID))]
    const result = geometry(fixture().build(), folded)
    assert.equal(result.movementPaths.length, 3)
    assert.equal(result.connectorPaths[0].path, 'M 20 10 L 40 70 ')
    assert.equal(result.connectorPaths[1].path, 'M 10 30 L 50 10 ')
})
