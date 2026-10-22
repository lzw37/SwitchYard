import assert from 'node:assert/strict'
import test from 'node:test'
import { buildTractionOccupancyUpdates as build, mergeTractionOccupancyTimes as merge } from './tractionOccupancy.ts'

function fixture() {
    const routes = [
        { id: 'R1', startNodeID: 'N0', endNodeID: 'N1', linkList: 'L1', cellList: 'A,B' },
        { id: 'R2', startNodeID: 'N1', endNodeID: 'N2', linkList: 'L2', cellList: '["B","C"]' },
    ]
    const cells = [
        { id: 'A@1', label: 'First', startM: 0, endM: 50 },
        { id: 'B@1', label: 'Shared', startM: 50, endM: 150 },
        { id: 'C@1', label: 'Last', startM: 150, endM: 200 },
    ]
    const profile = { routeIds: ['R1', 'R2'], lengthM: 200, errors: [], warnings: [], devices: [], joints: [], cells,
        segments: [
            { id: 'L1@1', linkId: 'L1', startM: 0, endM: 100, lengthM: 100, gradientPermille: 0 },
            { id: 'L2@1', linkId: 'L2', startM: 100, endM: 200, lengthM: 100, gradientPermille: 0 },
        ] }
    const result = { engine: 'SwitchYard.SpeedProfile', mode: 'through', lengthM: 200, trainLengthM: 10,
        motionStartS: 18, totalTimeS: 38, durationS: 20, runningTimeS: 20, dwellTimeS: 0, maxSpeedKmh: 36,
        warnings: [], jointTimes: [],
        samples: [0, 100, 200].map(positionM => ({ positionM, timeS: 18 + positionM / 10, speedKmh: 36, limitKmh: 80 })),
        cellOccupancies: cells.map((cell, i) => ({ ...cell,
            entryTimeS: [18, 23, 33][i], exitTimeS: [24, 35, 44][i], releaseTimeS: [25, 36, 45][i],
        })),
    }
    return { routes, profile, result }
}
const calculate = f => build(f.routes, f.profile, f.result)
const row = (cellID, startOccupationShift, endOccupationShift) => ({ cellID, startOccupationShift, endOccupationShift })

test('each route uses separate start and end event references, including negative shifts', () => {
    const output = calculate(fixture())
    assert.deepEqual(output, [
        { routeID: 'R1', startTimeS: 18, endTimeS: 28, times: [row('A', 0, -3), row('B', 5, 8)], skippedCellIDs: [] },
        { routeID: 'R2', startTimeS: 28, endTimeS: 38, times: [row('B', -5, -2), row('C', 5, 7)], skippedCellIDs: [] },
    ])
    // Different reference events mean a start shift may exceed an end shift.
    assert.ok(output[0].times[0].startOccupationShift > output[0].times[0].endOccupationShift)
})

test('start rounds down and release rounds up, including negative fractional seconds', () => {
    const f = fixture()
    Object.assign(f.result.cellOccupancies[1], { entryTimeS: 22.2, releaseTimeS: 36.2 })
    const output = calculate(f)
    assert.deepEqual(output[0].times[1], row('B', 4, 9))
    assert.deepEqual(output[1].times[0], row('B', -6, -1))
})

test('setup/reaction origin delay cancels while clearance remains', () => {
    const f = fixture(), expected = calculate(f).map(value => value.times)
    f.result.motionStartS += 12
    f.result.samples.forEach(sample => { sample.timeS += 12 })
    f.result.cellOccupancies.forEach(cell => {
        cell.entryTimeS += 12; cell.exitTimeS += 12; cell.releaseTimeS += 12
    })
    assert.deepEqual(calculate(f).map(value => value.times), expected)
})

test('a stop shared by consecutive routes uses first arrival and last departure', () => {
    const f = fixture()
    f.result.samples[1].speedKmh = 0
    f.result.samples.splice(2, 0, { positionM: 100, timeS: 58, speedKmh: 0, limitKmh: 80 })
    f.result.samples[3].timeS = 68
    Object.assign(f.result.cellOccupancies[1], { exitTimeS: 65, releaseTimeS: 66 })
    Object.assign(f.result.cellOccupancies[2], { entryTimeS: 63, exitTimeS: 74, releaseTimeS: 75 })
    const [arrival, departure] = calculate(f)
    assert.equal(arrival.endTimeS, 28)
    assert.equal(departure.startTimeS, 58)
    assert.equal(departure.endTimeS, 68)
    assert.deepEqual(departure.times, [row('B', -35, -2), row('C', 5, 7)])
})

test('interpolates a route boundary inside a constant-acceleration sample interval', () => {
    const f = fixture()
    f.result.samples.splice(1, 1)
    assert.equal(calculate(f)[0].endTimeS, 28)
})

test('uses accumulated profile segment count even when persisted links have another order', () => {
    const f = fixture()
    f.routes = [{ id: 'R1', startNodeID: 'N0', endNodeID: 'N2', linkList: 'L2,L1', cellList: 'A,B,C' }]
    f.profile.routeIds = ['R1']
    const [output] = calculate(f)
    assert.equal(output.startTimeS, 18)
    assert.equal(output.endTimeS, 38)
    assert.deepEqual(output.times, [row('A', 0, -13), row('B', 5, -2), row('C', 15, 7)])
})

test('source IDs containing @ are matched only through the known CellList', () => {
    const f = fixture()
    f.routes[0].cellList = 'A@7,B'
    f.profile.cells[0].id = 'A@7@1'
    f.result.cellOccupancies[0].id = 'A@7@1'
    f.result.cellOccupancies[0].label = 'B'
    assert.deepEqual(calculate(f)[0].times[0], row('A@7', 0, -3))
})

test('an arbitrary result suffix or matching label is not treated as a source CellID', () => {
    const f = fixture()
    f.profile.cells[0].id = 'A'; f.result.cellOccupancies[0].id = 'A'
    assert.deepEqual(calculate(f)[0].skippedCellIDs, ['A'])
})

test('only positive position overlap contributes to a route', () => {
    const f = fixture()
    f.profile.cells[1].endM = 100; f.result.cellOccupancies[1].endM = 100
    const output = calculate(f)
    assert.deepEqual(output[0].skippedCellIDs, [])
    assert.deepEqual(output[1].skippedCellIDs, ['B'])
    assert.deepEqual(output[1].times, [row('C', 5, 7)])
})

test('repeated Cell occurrences use the earliest entry and latest release envelope', () => {
    const f = fixture()
    f.profile.cells.push({ id: 'A@2', label: 'Again', startM: 70, endM: 90 })
    f.result.cellOccupancies.push({ ...f.profile.cells[3], entryTimeS: 25, exitTimeS: 30, releaseTimeS: 31 })
    assert.deepEqual(calculate(f)[0].times[0], row('A', 0, 3))
})

test('one unknown repeated occurrence prevents updating the whole Cell', () => {
    const f = fixture()
    f.profile.cells.push({ id: 'A@2', label: 'Again', startM: 70, endM: 90 })
    f.result.cellOccupancies.push({ ...f.profile.cells[3], entryTimeS: 25, exitTimeS: null, releaseTimeS: null })
    const output = calculate(f)[0]
    assert.deepEqual(output.skippedCellIDs, ['A'])
    assert.deepEqual(output.times, [row('B', 5, 8)])
})

test('unknown profile or server boundaries and stopped tails are preserved', () => {
    for (const target of ['profile', 'result']) {
        for (const flag of ['entryBoundaryKnown', 'exitBoundaryKnown']) {
            const f = fixture()
            ;(target === 'profile' ? f.profile.cells : f.result.cellOccupancies)[0][flag] = false
            assert.deepEqual(calculate(f)[0].skippedCellIDs, ['A'])
        }
    }
    const f = fixture()
    f.result.mode = 'arrival'
    f.result.cellOccupancies[2].exitTimeS = null; f.result.cellOccupancies[2].releaseTimeS = null
    assert.deepEqual(calculate(f)[1].skippedCellIDs, ['C'])
})

test('missing, null, nonfinite or inverted occupancy values are skipped, never zero-filled', () => {
    for (const invalid of [null, undefined, NaN, Infinity, -Infinity, '18', -1]) {
        const f = fixture()
        f.result.cellOccupancies[0].entryTimeS = invalid
        assert.deepEqual(calculate(f)[0].skippedCellIDs, ['A'])
    }
    for (const change of [{ exitTimeS: 0 }, { releaseTimeS: 20 }, { releaseTimeS: 3e10 }]) {
        const f = fixture(); Object.assign(f.result.cellOccupancies[0], change)
        assert.deepEqual(calculate(f)[0].skippedCellIDs, ['A'])
    }
    const f = fixture(); f.result.cellOccupancies.shift()
    assert.deepEqual(calculate(f)[0].skippedCellIDs, ['A'])
})

test('all unknown produces an empty update without manufacturing rows', () => {
    const f = fixture(); f.result.cellOccupancies = []
    assert.deepEqual(calculate(f).map(update => [update.times, update.skippedCellIDs]), [[[], ['A', 'B']], [[], ['B', 'C']]])
})

test('invalid routes, stale profile or unexpected result geometry are rejected', () => {
    const mutate = [
        f => { f.routes = [] },
        f => { f.profile.routeIds.reverse() },
        f => { f.routes[1].id = 'R1' },
        f => { f.routes[1].startNodeID = 'N9' },
        f => { f.profile.errors.push({ code: 'bad' }) },
        f => { f.routes[0].linkList = 'Other' },
        f => { f.routes[0].linkList = 'L1,L1' },
        f => { f.profile.segments[1].startM = 101 },
        f => { f.result.lengthM = 201 },
        f => { f.result.samples[0].positionM = 1 },
        f => { f.result.samples[1].timeS = NaN },
        f => { f.result.samples[1].timeS = 15 },
        f => { f.result.samples[1].speedKmh = -1 },
        f => { f.result.motionStartS = 19 },
        f => { f.result.cellOccupancies[0].startM = 1 },
        f => { f.result.cellOccupancies.push({ ...f.result.cellOccupancies[0] }) },
    ]
    for (const mutation of mutate) {
        const f = fixture(); mutation(f)
        assert.throws(() => calculate(f), Error)
    }
})

test('replacement merge preserves unknown and indirect rows, and does not mutate inputs', () => {
    const f = fixture(); f.result.cellOccupancies[0].releaseTimeS = null
    const update = calculate(f)[0]
    const existing = [row('A', -70, 80), row('B', 0, 0), row('interference', null, 90)]
    const snapshot = structuredClone(existing)
    assert.deepEqual(merge(existing, update), [row('A', -70, 80), row('B', 5, 8), row('interference', null, 90)])
    assert.deepEqual(existing, snapshot)
    assert.deepEqual(merge([], update), [row('B', 5, 8)])
})

test('merge accepts PascalCase responses and appends newly known cells', () => {
    const update = calculate(fixture())[0]
    const existing = [{ RouteID: 'R1', TrainTypeID: '', CellID: 'interference', StartOccupationShift: -30, EndOccupationShift: 7 }]
    assert.deepEqual(merge(existing, update), [row('interference', -30, 7), ...update.times])
})

test('merge rejects malformed or foreign stored values instead of coercing data to zero', () => {
    const update = calculate(fixture())[0]
    const badValues = [undefined, null, {}, [null], [row(' ', 0, 0)], [row('A', '0', 0)], [row('A', NaN, 0)],
        [row('A', Infinity, 0)], [row('A', .5, 0)], [row('A', 2147483648, 0)],
        [row('A', 0, 0), row('A', 1, 1)], [{ cellID: 'A', startOccupationShift: 0 }],
        [{ ...row('A', 0, 0), routeID: 'R2' }], [{ ...row('A', 0, 0), trainTypeID: 'Passenger' }],
        [{ ...row('A', 0, 0), CellID: 'Other' }]]
    for (const value of badValues) assert.throws(() => merge(value, update), Error)
})

test('merge refuses unknown or nullable updated times, but retains nullable existing rows', () => {
    const update = calculate(fixture())[0]
    assert.throws(() => merge([], { ...update, skippedCellIDs: ['A'] }), Error)
    assert.throws(() => merge([], { ...update, times: [row('A', null, 1)] }), Error)
    assert.deepEqual(merge([row('unmapped', null, null)], { ...update, times: [], skippedCellIDs: ['A', 'B'] }), [row('unmapped', null, null)])
})

test('backend synthesized rows may omit route scope with null or empty values', () => {
    const update = calculate(fixture())[0]
    for (const routeID of [null, '']) {
        assert.deepEqual(merge([{ ...row('interference', 0, 0), routeID, trainTypeID: null }], update),
            [row('interference', 0, 0), ...update.times])
    }
})
