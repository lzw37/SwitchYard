import assert from 'node:assert/strict'
import test from 'node:test'
import { buildStationPlanTrains } from './stationPlanView.ts'
import { defaultStationPlanColor, stationPlanColorGroups, stationPlanColorKey } from './stationPlanColors.ts'

const trains = [
    { id: 'T1', label: 'G1', color: 'old-a', trainType: '客车' },
    { id: 'T2', label: 'G2', color: 'old-b', trainType: '客车' },
    { id: 'T3', label: 'F3', color: 'old-c', trainType: '货车' },
]
const routes = [
    { id: 'R1', name: '接车', type: ' arr ', startNodeID: 'A', endNodeID: 'B', cellIDs: [] },
    { id: 'R2', name: '停留', type: 'Dwelling', startNodeID: 'B', endNodeID: 'C', cellIDs: [] },
]
const movements = trains.flatMap(train => ['R1', 'R2'].map((routeID, index) => ({
    trainID: train.id, movementID: `M${index}`, routeID: '', routeIDList: JSON.stringify([routeID]),
    name: '活动', startMinutes: index * 10, endMinutes: index * 10 + 5, sortOrder: index,
})))
const snapshots = trains.map((train, index) => ({
    trainID: train.id, sourceTemplateID: index < 2 ? 'P1' : 'P2', sourceName: index === 1 ? '客运新名称' : '同名过程',
    sourceRevision: index + 1, activityMovementMap: { a: 'M0', b: 'M1' },
    process: { activities: [
        { id: 'a', startEvent: 'e1', endEvent: 'e2', type: 'Departure' },
        { id: 'b', startEvent: 'e3', endEvent: 'e4', type: 'Dwelling' },
    ], precedences: [] },
}))
const build = (r = routes, s = snapshots) => buildStationPlanTrains(trains, movements, r, s)
const colorFor = (groups, train, mode, movementID) => groups.find(group => group.key === stationPlanColorKey(train, mode, movementID))?.color

test('同一来源过程跨列车、版本和改名同色；同名但不同来源的过程分色，输入顺序不影响颜色', () => {
    const built = build(), groups = stationPlanColorGroups(built, 'process')
    assert.equal(groups.length, 2)
    assert.equal(colorFor(groups, built[0], 'process'), colorFor(groups, built[1], 'process'))
    assert.notEqual(colorFor(groups, built[0], 'process'), colorFor(groups, built[2], 'process'))
    assert.deepEqual(stationPlanColorGroups([...built].reverse(), 'process'), groups)
})

test('进路配色读取当前选定进路，兼容 RouteIDList、类型别名及没有进路的旧过程活动', () => {
    const built = build(), groups = stationPlanColorGroups(built, 'routeType')
    assert.deepEqual(groups.map(group => group.value), ['Arrival', 'Dwelling'])
    assert.equal(colorFor(groups, built[0], 'routeType', 'M0'), colorFor(groups, built[2], 'routeType', 'M0'))
    assert.notEqual(colorFor(groups, built[0], 'routeType', 'M0'), colorFor(groups, built[0], 'routeType', 'M1'))
    // The current route overrides the original activity type; changing it updates the group.
    const changed = build(routes.map(route => ({ ...route, type: 'Departure' })))
    assert.deepEqual(stationPlanColorGroups(changed, 'routeType').map(group => group.value), ['Departure'])
    const legacyMovements = movements.map(movement => ({ ...movement, routeIDList: '', startNodeID: 'A', endNodeID: 'B' }))
    const legacy = buildStationPlanTrains(trains, legacyMovements, [], snapshots)
    assert.deepEqual(stationPlanColorGroups(legacy, 'routeType').map(group => group.value), ['Departure', 'Dwelling'])
})

test('列车种类默认红色，修改某种类只影响同种列车，切换模式互不覆盖且可随报告序列化', () => {
    const built = build(), before = structuredClone(built)
    const defaults = stationPlanColorGroups(built, 'trainType')
    assert.equal(defaults.length, 2)
    assert.ok(defaults.every(group => group.color === defaultStationPlanColor))
    const state = { colorMode: 'trainType', colorOverrides: { [stationPlanColorKey(built[0], 'trainType')]: '#123456' } }
    const snapshot = JSON.parse(JSON.stringify({ trains: built, state }))
    const groups = stationPlanColorGroups(snapshot.trains, snapshot.state.colorMode, snapshot.state.colorOverrides)
    assert.equal(colorFor(groups, built[0], 'trainType'), '#123456')
    assert.equal(colorFor(groups, built[1], 'trainType'), '#123456')
    assert.equal(colorFor(groups, built[2], 'trainType'), defaultStationPlanColor)
    for (const mode of ['process', 'routeType'])
        assert.deepEqual(stationPlanColorGroups(built, mode, state.colorOverrides), stationPlanColorGroups(built, mode))
    assert.deepEqual(built, before)
})

test('缺失分类信息和来源旧快照均可绘图；超过基础调色板数量的过程继续分色', () => {
    const unknown = buildStationPlanTrains([{ id: 'T1', label: '1', color: 'old' }], movements, routes)
    for (const mode of ['process', 'trainType']) {
        const groups = stationPlanColorGroups(unknown, mode)
        assert.equal(groups[0].value, '')
        assert.equal(groups[0].color, defaultStationPlanColor)
    }
    const many = Array.from({ length: 15 }, (_, index) => ({ ...build()[0], id: `${index}`,
        processConstraints: { ...snapshots[0], sourceTemplateID: `P${index}` } }))
    const groups = stationPlanColorGroups(many, 'process')
    assert.equal(new Set(groups.map(group => group.color)).size, 15)
    assert.ok(groups.every(group => /^#[0-9a-f]{6}$/i.test(group.color)))
})
