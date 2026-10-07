import assert from 'node:assert/strict'
import test from 'node:test'
import { countActiveRoutePhases, getHighlightedRouteRunIndices, getRouteRunPhase } from './simulationRouteActivity.ts'

const run = (key, type, startSeconds, endSeconds, trainID = 'G101') => ({
    key, train: { id: trainID }, route: { id: key, type }, startSeconds, endSeconds, lockSeconds: 10,
})
const concurrent = () => [
    run('arrival', 'Arrival', 0, 120),
    run('stop', 'Dwelling', 0, 240),
    run('departure', 'Departure', 0, 360),
]

test('单列车的停留与其他进路并行时全部高亮，停留预览索引不能截断活动列表', () => {
    const runs = concurrent()
    assert.deepEqual(getHighlightedRouteRunIndices(runs, 0, 1), [0, 1, 2])
    assert.deepEqual(getHighlightedRouteRunIndices(runs, 60, 1), [0, 1, 2])
    assert.deepEqual(countActiveRoutePhases(runs, 0), { locking: 2, moving: 0, dwelling: 1 })
    assert.deepEqual(countActiveRoutePhases(runs, 60), { locking: 0, moving: 2, dwelling: 1 })
})

test('全站回放按活动计数，同一列车及其他列车的进路均不因停留而被隐藏', () => {
    const runs = [...concurrent(), run('other-arrival', 'Arr', 0, 120, 'G102')]
    assert.deepEqual(getHighlightedRouteRunIndices(runs, 0), [0, 1, 2, 3])
    assert.deepEqual(countActiveRoutePhases(runs, 0), { locking: 3, moving: 0, dwelling: 1 })
    // Multiple activities on the same route still have independent time windows.
    runs.push({ ...run('other-departure', 'Dep', 50, 180), route: runs[2].route })
    assert.deepEqual(getHighlightedRouteRunIndices(runs, 55), [0, 1, 2, 3, 4])
    assert.deepEqual(countActiveRoutePhases(runs, 55), { locking: 1, moving: 3, dwelling: 1 })
})

test('按每条进路独立开始和结束，结束边界不继续标记占用，支持多个停留活动', () => {
    const runs = [...concurrent(), run('second-stop', '停留进路', 120, 180)]
    assert.deepEqual(getHighlightedRouteRunIndices(runs, 119.9), [0, 1, 2])
    assert.deepEqual(getHighlightedRouteRunIndices(runs, 120), [1, 2, 3])
    assert.deepEqual(countActiveRoutePhases(runs, 120), { locking: 0, moving: 1, dwelling: 2 })
    assert.deepEqual(getHighlightedRouteRunIndices(runs, 180), [1, 2])
    assert.deepEqual(getHighlightedRouteRunIndices(runs, 240), [2])
    assert.deepEqual(getHighlightedRouteRunIndices(runs, 360), [])
})

test('单列车在作业间隙保留预览，但不计为正在办理；全站没有活动时不预览', () => {
    const runs = [run('first', 'Arr', 10, 20), run('next', 'Dep', 40, 60)]
    for (const seconds of [0, 30, 60]) {
        assert.deepEqual(getHighlightedRouteRunIndices(runs, seconds, 0), [0])
        assert.deepEqual(getHighlightedRouteRunIndices(runs, seconds), [])
        assert.deepEqual(countActiveRoutePhases(runs, seconds), { locking: 0, moving: 0, dwelling: 0 })
    }
    assert.deepEqual(getHighlightedRouteRunIndices(runs, 40, 0), [1])
    assert.deepEqual(getHighlightedRouteRunIndices([], 0, 0), [])
    assert.deepEqual(getHighlightedRouteRunIndices(runs, 30, 5), [])
})

test('保留办理、走行及停留阶段判断，类型别名兼容旧进路', () => {
    const arrival = run('arrival', 'Arrival', 10, 60)
    assert.equal(getRouteRunPhase(null, 0), 'waiting')
    assert.equal(getRouteRunPhase(arrival, 9), 'waiting')
    assert.equal(getRouteRunPhase(arrival, 10), 'locking')
    assert.equal(getRouteRunPhase(arrival, 20), 'locking')
    assert.equal(getRouteRunPhase(arrival, 20.1), 'moving')
    assert.equal(getRouteRunPhase(arrival, 60), 'finished')
    for (const type of ['Dw', 'Dwelling', '停留', '停留进路']) {
        const stop = run('stop', type, 10, 60)
        assert.equal(getRouteRunPhase(stop, 10), 'dwelling')
        assert.equal(getRouteRunPhase(stop, 60), 'finished')
    }
})
