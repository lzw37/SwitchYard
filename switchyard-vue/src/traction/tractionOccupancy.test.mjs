import assert from 'node:assert/strict'
import test from 'node:test'
import { buildTractionOccupancyUpdates as build, mergeTractionOccupancyTimes as merge } from './tractionOccupancy.ts'
import { cellTotalOccupancyInterval, defaultSignalParameters, departureSectionCell } from './tractionCalculation.ts'

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

function departureFixture() {
    const f = fixture()
    f.result.mode = 'departure'
    f.result.samples[0].speedKmh = 0
    return f
}

test('shunting updates use the original route and end a still-occupied terminal circuit at the stop', () => {
    const f = fixture(), expected = calculate(f)
    f.result.mode = 'shunting'
    assert.deepEqual(calculate(f), expected)
    f.result.samples.at(-1).speedKmh = 0
    f.result.cellOccupancies.at(-1).exitTimeS = null
    f.result.cellOccupancies.at(-1).releaseTimeS = null
    const updates = calculate(f)
    assert.deepEqual(updates[0], expected[0])
    assert.deepEqual(updates[1].times, [row('B', -28, -2), row('C', -28, 0)])
    assert.deepEqual(updates[1].skippedCellIDs, [])
    f.result.approachDistanceM = 50
    assert.throws(() => calculate(f), /braking approach/)
    f.result.approachDistanceM = 0
    f.result.departureSectionLengthM = 50
    assert.throws(() => calculate(f), /departure section/)
})

for (const mode of ['arrival','departure','through','shunting']) test(`${mode} reserves all cells beneath the stopped train until one terminal arrival across route references`, () => {
    const f=fixture()
    Object.assign(f.result,{mode,approachDistanceM:0,trainLengthM:120})
    f.result.samples.at(-1).speedKmh=0
    f.result.samples.push({...f.result.samples.at(-1),timeS:98})
    f.result.cellOccupancies.slice(1).forEach(cell=>{cell.exitTimeS=null;cell.releaseTimeS=null})
    const before=structuredClone(f),updates=calculate(f)
    assert.deepEqual(updates.map(update=>update.skippedCellIDs),[[],[]])
    assert.deepEqual(updates[0].times,[row('A',-18,-3),row('B',-18,10)])
    assert.deepEqual(updates[1].times,[row('B',-28,0),row('C',-28,0)])
    for (const update of updates) for (const time of update.times.filter(time=>time.cellID!=='A')) {
        const cell=f.result.cellOccupancies.find(cell=>cell.id===`${time.cellID}@1`)
        assert.equal(update.endTimeS+time.endOccupationShift,38)
        assert.equal(cellTotalOccupancyInterval(f.result,cell,defaultSignalParameters()).endTimeS,38)
    }
    assert.deepEqual(f,before)
})

test('a stop in the appended departure circuit ends any still-occupied station circuits at that later stop', () => {
    const f=departureFixture(),extension=departureSectionCell(f.profile,100)
    Object.assign(f.result,{lengthM:300,departureSectionLengthM:100,trainLengthM:150})
    f.result.samples.push({positionM:300,timeS:58,speedKmh:0,limitKmh:60})
    f.result.samples.push({...f.result.samples.at(-1),timeS:118})
    Object.assign(f.result.cellOccupancies[2],{exitTimeS:null,releaseTimeS:null})
    f.result.cellOccupancies.push({...extension,entryTimeS:38,exitTimeS:null,releaseTimeS:null})
    const updates=calculate(f)
    assert.deepEqual(updates[1].times.at(-1),row('C',-28,20))
    assert.equal(updates[1].endTimeS+updates[1].times.at(-1).endOccupationShift,58)
    assert.equal(cellTotalOccupancyInterval(f.result,f.result.cellOccupancies.at(-1),defaultSignalParameters()).endTimeS,58)
    assert.ok(updates.every(update=>update.times.every(time=>time.cellID!==extension.id)))
})

test('appended first departure circuit is validated while original station occupancy updates remain usable', () => {
    const f = departureFixture(), original = calculate(f), cell = departureSectionCell(f.profile, 100)
    Object.assign(f.result, { lengthM: 300, departureSectionLengthM: 100 })
    f.result.samples.push({ positionM: 300, timeS: 48, speedKmh: 36, limitKmh: 80 })
    f.result.cellOccupancies.push({ ...cell, entryTimeS: 38, exitTimeS: 49, releaseTimeS: 54 })
    assert.deepEqual(calculate(f), original, 'a calculation-only circuit must not become an invented station CellID parameter')
    f.result.cellOccupancies.at(-1).endM++
    assert.throws(() => calculate(f), /cell scope/)
    f.result.cellOccupancies.at(-1).endM--
    f.result.mode = 'arrival'
    assert.throws(() => calculate(f), /departure section/)
})

test('through occupancy updates keep station route references when both approach and departure sections are added', () => {
    const f = fixture(), original = calculate(f), approach = 50, departureLength = 100
    const cell = departureSectionCell(f.profile, departureLength)
    Object.assign(f.result, { lengthM: 350, approachDistanceM: approach, departureSectionLengthM: departureLength })
    f.result.samples = [
        { positionM: 0, timeS: 18, speedKmh: 36, limitKmh: 80 },
        ...f.result.samples.map(s => ({ ...s, positionM: s.positionM + approach, timeS: s.timeS + 5 })),
        { positionM: 350, timeS: 53, speedKmh: 36, limitKmh: 80 },
    ]
    f.result.cellOccupancies = [...f.result.cellOccupancies, { ...cell, entryTimeS: 38, exitTimeS: 49, releaseTimeS: 54 }]
        .map(c => ({ ...c, startM: c.startM + approach, endM: c.endM + approach,
            entryTimeS: c.entryTimeS + 5, exitTimeS: c.exitTimeS + 5, releaseTimeS: c.releaseTimeS + 5 }))
    const updates = calculate(f)
    assert.deepEqual(updates, original.map(update => ({ ...update, startTimeS: update.startTimeS + 5, endTimeS: update.endTimeS + 5,
        times: update.times.map(time => ({ ...time, startOccupationShift: time.startOccupationShift - 5 })) })))
    const total = cellTotalOccupancyInterval(f.result, f.result.cellOccupancies.at(-1), defaultSignalParameters())
    assert.equal(total.startTimeS, 0)
    assert.equal(total.durationS, 59, 'the added circuit shares the braking-range approach and route-setting origin')
})

test('through routes reserve from setting using separate start and end event references', () => {
    const f = fixture(), before = structuredClone(f), output = calculate(f)
    assert.deepEqual(output, [
        { routeID: 'R1', startTimeS: 18, endTimeS: 28, times: [row('A', -18, -3), row('B', -18, 8)], skippedCellIDs: [] },
        { routeID: 'R2', startTimeS: 28, endTimeS: 38, times: [row('B', -28, -2), row('C', -28, 7)], skippedCellIDs: [] },
    ])
    for (const update of output) {
        for (const time of update.times) {
            assert.equal(update.startTimeS + time.startOccupationShift, 0)
            const occupancy = f.result.cellOccupancies.find(cell => cell.id === `${time.cellID}@1`)
            assert.equal(update.endTimeS + time.endOccupationShift, occupancy.releaseTimeS)
            const chart = cellTotalOccupancyInterval(f.result, occupancy, defaultSignalParameters())
            assert.equal(update.startTimeS + time.startOccupationShift, chart.startTimeS)
            assert.equal(update.endTimeS + time.endOccupationShift, chart.endTimeS)
        }
    }
    assert.deepEqual(f, before, 'preview and conversion preserve the saved physical events')
})

test('through reservation with zero preparation still starts at route setting', () => {
    const f = fixture()
    f.result.motionStartS = 0
    f.result.samples.forEach(sample => { sample.timeS -= 18 })
    f.result.cellOccupancies.forEach(cell => {
        cell.entryTimeS -= 18; cell.exitTimeS -= 18; cell.releaseTimeS -= 18
    })
    assert.deepEqual(calculate(f)[0].times, [row('A', 0, -3), row('B', 0, 8)])
})

test('through can reserve an entrance-clipped circuit and end an occupied circuit at the terminal stop', () => {
    const f = fixture()
    f.profile.cells[0].entryBoundaryKnown = false
    Object.assign(f.result.cellOccupancies[0], { entryBoundaryKnown: false, entryTimeS: null })
    f.result.samples.at(-1).speedKmh = 0
    Object.assign(f.result.cellOccupancies[2], { exitTimeS: null, releaseTimeS: null })
    const output = calculate(f)
    assert.deepEqual(output[0].times, [row('A', -18, -3), row('B', -18, 8)])
    assert.deepEqual(output[1].skippedCellIDs, [])
    assert.deepEqual(merge([row('B', 0, 0), row('C', 5, 7)], output[1]), [row('B', -28, -2), row('C', -28, 0)])
})

test('start rounds down and release rounds up, including negative fractional seconds', () => {
    const f = fixture()
    f.result.motionStartS += .25
    f.result.samples.forEach(sample => { sample.timeS += .25 })
    f.result.cellOccupancies.forEach(cell => {
        cell.entryTimeS += .25; cell.exitTimeS += .25; cell.releaseTimeS += .25
    })
    f.result.cellOccupancies[1].releaseTimeS = 36.5
    const output = calculate(f)
    assert.deepEqual(output[0].times[1], row('B', -19, 9))
    assert.deepEqual(output[1].times[0], row('B', -29, -1))
})

test('through reservation includes setup and reaction instead of cancelling their delay', () => {
    const f = fixture()
    f.result.motionStartS += 12
    f.result.samples.forEach(sample => { sample.timeS += 12 })
    f.result.cellOccupancies.forEach(cell => {
        cell.entryTimeS += 12; cell.exitTimeS += 12; cell.releaseTimeS += 12
    })
    assert.deepEqual(calculate(f).map(value => value.times), [
        [row('A', -30, -3), row('B', -30, 8)],
        [row('B', -40, -2), row('C', -40, 7)],
    ])
})

test('through route boundaries use first arrival like arrival mode, including shared dwell', () => {
    const f = fixture()
    f.result.samples[1].speedKmh = 0
    f.result.samples.splice(2, 0, { positionM: 100, timeS: 58, speedKmh: 0, limitKmh: 80 })
    f.result.samples[3].timeS = 68
    Object.assign(f.result.cellOccupancies[1], { exitTimeS: 65, releaseTimeS: 66 })
    Object.assign(f.result.cellOccupancies[2], { entryTimeS: 63, exitTimeS: 74, releaseTimeS: 75 })
    const [arrival, departure] = calculate(f)
    assert.equal(arrival.endTimeS, 28)
    assert.equal(departure.startTimeS, 28)
    assert.equal(departure.endTimeS, 68)
    assert.deepEqual(departure.times, [row('B', -28, -2), row('C', -28, 7)])
    const expected = calculate(f)
    f.result.mode = 'arrival'
    assert.deepEqual(calculate(f), expected, 'through and arrival use the same reference events')
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
    assert.deepEqual(output.times, [row('A', -18, -13), row('B', -18, -2), row('C', -18, 7)])
})

test('source IDs containing @ are matched only through the known CellList', () => {
    const f = fixture()
    f.routes[0].cellList = 'A@7,B'
    f.profile.cells[0].id = 'A@7@1'
    f.result.cellOccupancies[0].id = 'A@7@1'
    f.result.cellOccupancies[0].label = 'B'
    assert.deepEqual(calculate(f)[0].times[0], row('A@7', -18, -3))
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
    assert.deepEqual(output[1].times, [row('C', -28, 7)])
})

test('repeated Cell occurrences reserve from setting to their latest release', () => {
    const f = fixture()
    f.profile.cells.push({ id: 'A@2', label: 'Again', startM: 70, endM: 90 })
    f.result.cellOccupancies.push({ ...f.profile.cells[3], entryTimeS: 25, exitTimeS: 30, releaseTimeS: 31 })
    assert.deepEqual(calculate(f)[0].times[0], row('A', -18, 3))
})

test('one unknown repeated occurrence prevents updating the whole Cell', () => {
    const f = fixture()
    f.profile.cells.push({ id: 'A@2', label: 'Again', startM: 70, endM: 90 })
    f.result.cellOccupancies.push({ ...f.profile.cells[3], entryTimeS: 25, exitTimeS: null, releaseTimeS: null })
    const output = calculate(f)[0]
    assert.deepEqual(output.skippedCellIDs, ['A'])
    assert.deepEqual(output.times, [row('B', -18, 8)])
})

test('unknown profile or server boundaries and stopped tails in other modes are preserved', () => {
    for (const target of ['profile', 'result']) {
        for (const flag of ['entryBoundaryKnown', 'exitBoundaryKnown']) {
            const f = fixture()
            ;(target === 'profile' ? f.profile.cells : f.result.cellOccupancies)[0][flag] = false
            assert.deepEqual(calculate(f)[0].skippedCellIDs, ['A'])
        }
    }
    const f = fixture()
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
        f => { f.result.mode = 'invalid' },
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
    assert.deepEqual(merge(existing, update), [row('A', -70, 80), row('B', -18, 8), row('interference', null, 90)])
    assert.deepEqual(existing, snapshot)
    assert.deepEqual(merge([], update), [row('B', -18, 8)])
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


test('departure reserves every circuit from route setting while retaining separate movement references', () => {
    const f = departureFixture(), before = structuredClone(f)
    const output = calculate(f)
    assert.deepEqual(output, [
        { routeID: 'R1', startTimeS: 18, endTimeS: 28, times: [row('A', -18, -3), row('B', -18, 8)], skippedCellIDs: [] },
        { routeID: 'R2', startTimeS: 28, endTimeS: 38, times: [row('B', -28, -2), row('C', -28, 7)], skippedCellIDs: [] },
    ])
    for (const update of output) {
        assert.equal(update.endTimeS - update.startTimeS, 10, 'operation time keeps its existing movement basis')
        for (const time of update.times) {
            const occupancy = f.result.cellOccupancies.find(cell => cell.id === `${time.cellID}@1`)
            assert.equal(update.startTimeS + time.startOccupationShift, 0)
            assert.equal(update.endTimeS + time.endOccupationShift, occupancy.releaseTimeS)
        }
    }
    assert.deepEqual(f, before, 'updating parameters must not alter saved physical entry or release events')
})

test('departure includes additional setup and reaction time instead of cancelling that delay', () => {
    const f = departureFixture()
    f.result.motionStartS += 12
    f.result.samples.forEach(sample => { sample.timeS += 12 })
    f.result.cellOccupancies.forEach(cell => {
        cell.entryTimeS += 12; cell.exitTimeS += 12; cell.releaseTimeS += 12
    })
    const output = calculate(f)
    assert.deepEqual(output.map(update => update.times), [
        [row('A', -30, -3), row('B', -30, 8)],
        [row('B', -40, -2), row('C', -40, 7)],
    ])
})

test('departure reservation rounds starts down and released ends up using each route reference', () => {
    const f = departureFixture()
    f.result.motionStartS += .25
    f.result.samples.forEach(sample => { sample.timeS += .25 })
    f.result.cellOccupancies.forEach(cell => {
        cell.entryTimeS += .25; cell.exitTimeS += .25; cell.releaseTimeS += .25
    })
    f.result.cellOccupancies[1].releaseTimeS = 36.5
    assert.deepEqual(calculate(f).map(update => update.times), [
        [row('A', -19, -3), row('B', -19, 9)],
        [row('B', -29, -1), row('C', -29, 7)],
    ])
})

test('departure can reserve an entrance-clipped circuit when clearance and release are known', () => {
    const f = departureFixture()
    f.profile.cells[0].entryBoundaryKnown = false
    Object.assign(f.result.cellOccupancies[0], { entryBoundaryKnown: false, entryTimeS: null })
    const update = calculate(f)[0]
    assert.deepEqual(update.times, [row('A', -18, -3), row('B', -18, 8)])
    assert.deepEqual(update.skippedCellIDs, [])
    assert.deepEqual(merge([row('A', 0, 0), row('B', 5, 8), row('indirect', -5, 9)], update),
        [row('A', -18, -3), row('B', -18, 8), row('indirect', -5, 9)])
})

test('departure repeated circuit occurrences reserve from setting through their latest release', () => {
    const f = departureFixture()
    f.profile.cells.push({ id: 'A@2', label: 'Again', startM: 70, endM: 90 })
    f.result.cellOccupancies.push({ ...f.profile.cells.at(-1), entryTimeS: 25, exitTimeS: 30, releaseTimeS: 31 })
    assert.deepEqual(calculate(f)[0].times[0], row('A', -18, 3))
    f.result.cellOccupancies.at(-1).releaseTimeS = null
    assert.deepEqual(calculate(f)[0].skippedCellIDs, ['A'])
})

test('departure keeps existing parameters for missing, malformed or unknown clearance and release events', () => {
    const mutations = [
        f => { f.result.cellOccupancies.shift() },
        f => { f.result.cellOccupancies[0].entryTimeS = null },
        f => { f.result.cellOccupancies[0].entryTimeS = 999 },
        f => { f.result.cellOccupancies[0].exitTimeS = null },
        f => { f.result.cellOccupancies[0].releaseTimeS = null },
        f => { f.result.cellOccupancies[0].exitTimeS = NaN },
        f => { f.result.cellOccupancies[0].releaseTimeS = Infinity },
        f => { f.result.cellOccupancies[0].exitTimeS = 17 },
        f => { f.result.cellOccupancies[0].releaseTimeS = 23 },
        f => { f.profile.cells[0].exitBoundaryKnown = false },
        f => { f.result.cellOccupancies[0].exitBoundaryKnown = false },
    ]
    const existing = [row('A', -77, 88), row('B', 0, 0), row('indirect', -5, 9)]
    for (const mutation of mutations) {
        const f = departureFixture(); mutation(f)
        const update = calculate(f)[0]
        assert.deepEqual(update.skippedCellIDs, ['A'])
        assert.deepEqual(merge(existing, update), [row('A', -77, 88), row('B', -18, 8), row('indirect', -5, 9)])
    }
    const f = departureFixture()
    f.result.samples.at(-1).speedKmh = 0
    Object.assign(f.result.cellOccupancies[2], { exitTimeS: null, releaseTimeS: null })
    assert.deepEqual(calculate(f)[1].skippedCellIDs, [], 'departure uses the same terminal-stop occupation-end convention')
    assert.deepEqual(calculate(f)[1].times.at(-1), row('C', -28, 0))
})

for (const mode of ['arrival', 'through']) test(`${mode} braking reservation starts at route setting and uses station entry and route end references`, () => {
    const f = fixture()
    f.result.mode = mode
    f.result.approachDistanceM = 1250
    f.result.lengthM += 1250
    f.result.totalTimeS += 100
    f.result.samples.forEach(p => { p.positionM += 1250; p.timeS += 100 })
    f.result.samples.unshift({ positionM: 0, timeS: 18, speedKmh: 36, limitKmh: 180 })
    f.result.cellOccupancies.forEach(c => {
        c.startM += 1250; c.endM += 1250
        c.entryTimeS += 100; c.exitTimeS += 100; c.releaseTimeS += 100
    })
    const before = structuredClone(f)
    const actual = calculate(f)
    assert.deepEqual(f, before)
    assert.deepEqual(actual.map(r => r.times), [
        [row('A', -118, -3), row('B', -118, 8)],
        [row('B', -128, -2), row('C', -128, 7)],
    ])
    assert.equal(actual[0].startTimeS, 118)
    assert.equal(actual[0].endTimeS, 128)
    assert.equal(actual[1].endTimeS, 138)
    if (mode === 'through') {
        for (const update of actual) for (const time of update.times) {
            const cell = f.result.cellOccupancies.find(cell => cell.id === `${time.cellID}@1`)
            const chart = cellTotalOccupancyInterval(f.result, cell, defaultSignalParameters())
            assert.equal(update.startTimeS + time.startOccupationShift, chart.startTimeS)
            assert.equal(update.endTimeS + time.endOccupationShift, chart.endTimeS)
        }
        f.result.mode = 'departure'
        assert.throws(() => calculate(f), /approach/)
        f.result.mode = mode
    }
    f.result.approachDistanceM = 0
    assert.throws(() => calculate(f), /scope/)
})

function arrivalFixture() {
    const f = fixture()
    f.routes = [{ id: 'R1', startNodeID: 'N0', endNodeID: 'N2', linkList: 'L1,L2', cellList: 'A,B,C' }]
    f.profile.routeIds = ['R1']
    Object.assign(f.result, { mode: 'arrival', approachDistanceM: 1000, lengthM: 1200, totalTimeS: 138 })
    f.result.samples.forEach(p => { p.positionM += 1000; p.timeS += 100 })
    f.result.samples.unshift({ positionM: 0, timeS: 18, speedKmh: 36, limitKmh: 80 })
    f.result.samples.at(-1).speedKmh = 0
    f.result.cellOccupancies.forEach(c => {
        c.startM += 1000; c.endM += 1000
        c.entryTimeS += 100; c.exitTimeS += 100; c.releaseTimeS += 100
    })
    Object.assign(f.result.cellOccupancies[2], { exitTimeS: null, releaseTimeS: null })
    return f
}

test('arrival includes setting, reaction and approach before station entry and zeros the occupied terminal cell', () => {
    const f = arrivalFixture(), before = structuredClone(f)
    assert.deepEqual(calculate(f), [{ routeID: 'R1', startTimeS: 118, endTimeS: 138,
        times: [row('A', -118, -13), row('B', -118, -2), row('C', -118, 0)], skippedCellIDs: [] }])
    assert.deepEqual(f, before, 'parameter convention must not change saved release events')
    f.result.motionStartS += 12
    f.result.samples.forEach(p => { p.timeS += 12 })
    f.result.cellOccupancies.forEach(c => {
        for (const field of ['entryTimeS', 'exitTimeS', 'releaseTimeS']) if (c[field] !== null) c[field] += 12
    })
    assert.deepEqual(calculate(f)[0].times, [row('A', -130, -13), row('B', -130, -2), row('C', -130, 0)])
})

test('arrival first head entry and first route-end arrival exclude subsequent dwell', () => {
    const f = arrivalFixture()
    f.result.samples[1].speedKmh = 0
    f.result.samples.slice(2).forEach(p => { p.timeS += 30 })
    f.result.samples.splice(2, 0, { ...f.result.samples[1], timeS: 148 })
    f.result.samples.push({ ...f.result.samples.at(-1), timeS: 198 })
    const output = calculate(f)[0]
    assert.equal(output.startTimeS, 118)
    assert.equal(output.endTimeS, 168)
    assert.deepEqual(output.times, [row('A', -118, -43), row('B', -118, -32), row('C', -118, 0)])
})

test('arrival offsets retain outward integer rounding and both signs for known releases', () => {
    const f = arrivalFixture()
    f.result.samples.slice(1).forEach(p => { p.timeS += .25 })
    f.result.cellOccupancies[0].releaseTimeS = 138.5
    f.result.cellOccupancies[1].releaseTimeS = 136.5
    assert.deepEqual(calculate(f)[0].times, [row('A', -119, 1), row('B', -119, -1), row('C', -119, 0)])
})

test('arrival can reserve an entrance-clipped circuit and zero a terminal-clipped circuit', () => {
    const f = arrivalFixture()
    f.profile.cells[0].entryBoundaryKnown = false
    Object.assign(f.result.cellOccupancies[0], { entryBoundaryKnown: false, entryTimeS: null })
    f.profile.cells[2].exitBoundaryKnown = false
    f.result.cellOccupancies[2].exitBoundaryKnown = false
    assert.deepEqual(calculate(f)[0].times, [row('A', -118, -13), row('B', -118, -2), row('C', -118, 0)])
})

test('arrival repeated occurrences use latest known release or zero if one remains occupied', () => {
    const f = arrivalFixture()
    f.profile.cells.push({ id: 'A@2', label: 'Again', startM: 170, endM: 200 })
    f.result.cellOccupancies.push({ ...f.profile.cells.at(-1), startM: 1170, endM: 1200,
        entryTimeS: 135, exitTimeS: null, releaseTimeS: null })
    assert.deepEqual(calculate(f)[0].times[0], row('A', -118, 0))
    Object.assign(f.result.cellOccupancies.at(-1), { exitTimeS: 139, releaseTimeS: 145 })
    assert.deepEqual(calculate(f)[0].times[0], row('A', -118, 7))
})

test('arrival never substitutes zero for missing records, malformed times or unoccupied unknown boundaries', () => {
    const mutations = [
        f => { f.result.cellOccupancies.pop() },
        f => { f.result.cellOccupancies[2].releaseTimeS = undefined },
        f => { f.result.cellOccupancies[2].exitTimeS = 140 },
        f => { f.result.cellOccupancies[2].exitTimeS = NaN },
        f => { f.result.cellOccupancies[2].entryTimeS = null },
        f => { f.result.cellOccupancies[2].entryTimeS = -1 },
        f => { f.result.cellOccupancies[2].entryTimeS = 999 },
        f => { f.result.cellOccupancies[2].entryTimeS = '133' },
        f => { f.result.cellOccupancies[2].releaseTimeS = Infinity },
        f => { f.result.cellOccupancies[2].releaseTimeS = 0 },
        f => { f.result.trainLengthM = NaN },
        f => { f.result.samples.at(-1).speedKmh = 36 },
    ]
    for (const mutation of mutations) {
        const f = arrivalFixture(); mutation(f)
        assert.deepEqual(calculate(f)[0].skippedCellIDs, ['C'])
    }
    const f = arrivalFixture()
    Object.assign(f.result.cellOccupancies[0], { exitTimeS: null, releaseTimeS: null, exitBoundaryKnown: false })
    f.profile.cells[0].exitBoundaryKnown = false
    assert.deepEqual(calculate(f)[0].skippedCellIDs, ['A'])
    const existing = [row('A', -77, 88), row('B', 0, 0), row('C', -50, 75), row('indirect', -5, 9)]
    assert.deepEqual(merge(existing, calculate(f)[0]),
        [row('A', -77, 88), row('B', -118, -2), row('C', -118, 0), row('indirect', -5, 9)])
})

test('arrival with no exterior reservation uses the saved route origin and still includes preparation', () => {
    const f = fixture()
    f.result.mode = 'arrival'
    assert.equal(calculate(f)[0].startTimeS, 18)
    assert.deepEqual(calculate(f)[0].times, [row('A', -18, -3), row('B', -18, 8)])
})
