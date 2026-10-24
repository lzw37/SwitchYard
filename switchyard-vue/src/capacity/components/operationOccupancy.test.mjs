import assert from 'node:assert/strict'
import test from 'node:test'
import { operationOccupancyWindow as window, dragOperationOccupancy as drag, isOperationOccupancyOrderValid as valid } from './operationOccupancy.ts'

test('operation end offset is anchored to duration while start uses zero', () => {
    assert.deepEqual(window(-78, 12, 106.355), { start: -78, operationStart: 0, operationEnd: 106.355, release: 118.355 })
    assert.deepEqual(window(-78, -64, 106), { start: -78, operationStart: 0, operationEnd: 106, release: 42 })
    assert.deepEqual(window(-78, 0, 106), { start: -78, operationStart: 0, operationEnd: 106, release: 106 })
})
test('unknown duration is never replaced by zero, but an actual zero duration is valid', () => {
    for (const duration of [null, -1, Infinity, NaN]) assert.equal(window(-10, 5, duration), null)
    assert.equal(window(null, 5, 10), null)
    assert.equal(window(-10, null, 10), null)
    assert.equal(window(-10, 5, 0).release, 5)
})
test('dragging uses separate reference events and supports start shift greater than end shift', () => {
    assert.deepEqual(drag(5, -20, 'start', 3), { startOccupationShift: 8, endOccupationShift: -20 })
    assert.deepEqual(drag(5, -20, 'end', -3), { startOccupationShift: 5, endOccupationShift: -23 })
    assert.deepEqual(drag(-78, 0, 'move', 10), { startOccupationShift: -68, endOccupationShift: 10 })
})
test('crossing an anchor is reported using absolute time instead of silently clamping the offset', () => {
    const start = drag(5, -20, 'start', 200)
    assert.deepEqual(start, { startOccupationShift: 205, endOccupationShift: -20 })
    assert.equal(valid(start.startOccupationShift, start.endOccupationShift, 100), false)
    const end = drag(5, -20, 'end', -200)
    assert.deepEqual(end, { startOccupationShift: 5, endOccupationShift: -220 })
    assert.equal(valid(end.startOccupationShift, end.endOccupationShift, 100), false)
})
test('negative end offsets are valid until the absolute release crosses start; equality is allowed', () => {
    assert.equal(valid(5, -20, 100), true)
    assert.equal(valid(5, -95, 100), true)
    assert.equal(valid(5, -96, 100), false)
    assert.equal(valid(5, -95, 100.5), true)
    assert.equal(valid(5, -95, 99.5), false)
    assert.equal(valid(0, 0, 0), true)
    assert.equal(valid(-30, 5, null), false)
    assert.equal(valid(null, 5, 100), false)
})
