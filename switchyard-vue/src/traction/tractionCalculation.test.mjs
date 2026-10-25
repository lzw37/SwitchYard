import assert from 'node:assert/strict'
import test from 'node:test'
import axios from 'axios'
import * as models from './tractionCalculation.ts'
import { buildTractionApiRequest, calculateTractionRemote } from './tractionApi.ts'

const profile = () => ({
    lengthM: 1000,
    segments: [{ startM: 0, endM: 1000, speedLimitKmh: 80, gradientPermille: -5, additionalResistanceKn: 2 }],
    devices: [{ id: 'S', label: 'Signal', type: 'signal', positionM: 100, endM: 120 }],
    joints: [{ id: 'J', label: 'Joint', positionM: 500 }],
    cells: [{ id: 'C', label: 'Circuit', startM: 0, endM: 1000, entryBoundaryKnown: false }],
})
const serverResult = () => ({
    engine: 'SwitchYard.SpeedProfile', lengthM: 1000, trainLengthM: 200, mode: 'arrival',
    durationS: 120, runningTimeS: 90, dwellTimeS: 30, motionStartS: 18, totalTimeS: 138,
    maxSpeedKmh: 80, warnings: ['point-mass-gradient'],
    samples: [
        { positionM: 0, speedKmh: 0, limitKmh: 80, timeS: 18 },
        { positionM: 1000, speedKmh: 0, limitKmh: 80, timeS: 138 },
    ],
    jointTimes: [{ id: 'J', label: 'Joint', positionM: 500, headTimeS: 60, tailTimeS: 100, releaseTimeS: 105 }],
    cellOccupancies: [{ id: 'C', label: 'Circuit', startM: 0, endM: 1000,
        entryTimeS: null, exitTimeS: null, releaseTimeS: null, entryBoundaryKnown: false, exitBoundaryKnown: true }],
})
const response = (config, data) => ({ data, config, status: 200, statusText: 'OK', headers: {} })

test('browser module has no traction solver and defaults match the backend input model', () => {
    assert.equal(models.calculateTraction, undefined)
    assert.equal(models.interpolateForce, undefined)
    const train = models.defaultTrainParameters(), run = models.defaultRunOptions()
    assert.equal(train.maxBrakeForceKn, 310)
    assert.equal(train.brakingCurve.length, 17)
    assert.equal(train.resistanceCurve[1].forceKn, 5.7)
    assert.equal(run.maxPointCount, 100001)
    assert.equal(run.stepM, 5)
    assert.deepEqual(run.stops, [])
    const another = models.defaultRunOptions()
    run.stops.push({ positionM: 1, dwellS: 1 })
    assert.deepEqual(another.stops, [])
})

test('shunting defaults to rest at both ends and preserves user-edited desired speeds', () => {
    const run = models.defaultRunOptions('shunting')
    assert.equal(run.mode, 'shunting')
    assert.equal(run.initialSpeedKmh, 0)
    assert.equal(run.finalSpeedKmh, 0)
    run.initialSpeedKmh = 18
    run.finalSpeedKmh = 24
    const request = buildTractionApiRequest(profile(), models.defaultTrainParameters(), models.defaultSignalParameters(), run)
    assert.equal(request.options.mode, 'shunting')
    assert.equal(request.options.initialSpeedKmh, 18)
    assert.equal(request.options.finalSpeedKmh, 24)
    assert.equal(request.profile.lengthM, 1000)
    assert.deepEqual(models.defaultRunOptions('shunting').stops, [])
})

test('fresh shunting routes select the shunting mode while saved movement defaults remain compatible', () => {
    for (const type of ['shunting', 'Shunting', 'shunt', '调车', '调车进路']) assert.equal(models.tractionModeForRoute(type), 'shunting')
    assert.equal(models.tractionModeForRoute('Arrival'), 'arrival')
    assert.equal(models.tractionModeForRoute('Departure'), 'departure')
    assert.equal(models.tractionModeForRoute('shunting', true), 'through')
    assert.equal(models.tractionModeForRoute('unknown'), 'through')
    assert.equal(models.defaultRunOptions('arrival').finalSpeedKmh, 0)
    assert.equal(models.defaultRunOptions('departure').initialSpeedKmh, 0)
    assert.equal(models.defaultRunOptions('through').finalSpeedKmh, 40)
})

test('request snapshot preserves engineering units, all missing inputs and circuit-boundary flags', () => {
    const train = models.defaultTrainParameters(), run = models.defaultRunOptions()
    run.stops.push({ positionM: 333.3, dwellS: 42 })
    train.brakingCurve = [{ speedKmh: 0, forceKn: 1 }]
    const input = profile()
    const request = buildTractionApiRequest(input, train, models.defaultSignalParameters(), run)
    assert.equal(request.train.massT, 440)
    assert.equal(request.train.resistanceB, 0.05)
    assert.equal(request.train.resistanceC, 0.002)
    assert.equal(request.train.tractionCurve[1].speedKmh, 40)
    assert.deepEqual(request.train.brakingCurve, [{ speedKmh: 0, forceKn: 1 }])
    assert.notEqual(request.train.brakingCurve, train.brakingCurve)
    assert.equal(request.profile.devices[0].endM, undefined)
    assert.equal(request.profile.segments[0].additionalResistanceKn, 2)
    assert.equal(request.profile.cells[0].entryBoundaryKnown, false)
    assert.equal(request.profile.cells[0].exitBoundaryKnown, true)
    assert.deepEqual(request.options.stops, [{ positionM: 333.3, dwellS: 42 }])
    input.segments[0].speedLimitKmh = 10
    train.tractionCurve[0].forceKn = 0
    run.stops[0].dwellS = 900
    assert.equal(request.profile.segments[0].speedLimitKmh, 80)
    assert.equal(request.train.tractionCurve[0].forceKn, 300)
    assert.equal(request.options.stops[0].dwellS, 42)
})

test('remote calculation posts one request and returns server timing without solving locally', async t => {
    const original = axios.defaults.adapter
    t.after(() => { axios.defaults.adapter = original })
    const expected = serverResult(), cancellation = new AbortController()
    let calls = 0
    axios.defaults.adapter = async config => {
        calls++
        assert.equal(config.url, '/api/SpeedProfile/calculate')
        assert.equal(config.method, 'post')
        assert.equal(config.signal, cancellation.signal)
        assert.equal(config.timeout, 30000)
        const body = JSON.parse(config.data)
        assert.equal(body.train.massT, 440)
        assert.equal(body.train.maxBrakeForceKn, 310)
        assert.equal(body.options.maxPointCount, 100001)
        return response(config, expected)
    }
    // This deliberately nonphysical fixture would fail any local solver; it must pass through unchanged.
    const result = await calculateTractionRemote(profile(), models.defaultTrainParameters(),
        models.defaultSignalParameters(), models.defaultRunOptions(), cancellation.signal)
    assert.deepEqual(result, expected)
    assert.equal(calls, 1)
})

test('HTTP infeasibility is propagated with field detail and never triggers a local fallback', async t => {
    const original = axios.defaults.adapter
    t.after(() => { axios.defaults.adapter = original })
    const failure = { code: 'infeasible-profile', message: 'The train cannot move from rest.', errors: { 'train.tractionCurve': ['insufficient traction'] } }
    axios.defaults.adapter = async config => {
        throw new axios.AxiosError('Unprocessable Entity', 'ERR_BAD_REQUEST', config, null,
            { ...response(config, failure), status: 422 })
    }
    await assert.rejects(calculateTractionRemote(profile(), models.defaultTrainParameters(),
        models.defaultSignalParameters(), models.defaultRunOptions()), error =>
        error.response.status === 422 && error.response.data === failure)
})

test('network failure is propagated and incompatible 200 response is rejected', async t => {
    const original = axios.defaults.adapter
    t.after(() => { axios.defaults.adapter = original })
    axios.defaults.adapter = async () => { throw new axios.AxiosError('Network Error', 'ERR_NETWORK') }
    await assert.rejects(calculateTractionRemote(profile(), models.defaultTrainParameters(),
        models.defaultSignalParameters(), models.defaultRunOptions()), error => error.code === 'ERR_NETWORK')
    axios.defaults.adapter = async config => response(config, '<html>login</html>')
    await assert.rejects(calculateTractionRemote(profile(), models.defaultTrainParameters(),
        models.defaultSignalParameters(), models.defaultRunOptions()), error => error.code === 'invalid-response')
})

test('pre-cancelled and pending HTTP calls honor AbortSignal', async t => {
    const original = axios.defaults.adapter
    t.after(() => { axios.defaults.adapter = original })
    const pre = new AbortController()
    pre.abort()
    let calls = 0
    axios.defaults.adapter = async config => { calls++; return response(config, serverResult()) }
    await assert.rejects(calculateTractionRemote(profile(), models.defaultTrainParameters(),
        models.defaultSignalParameters(), models.defaultRunOptions(), pre.signal), axios.isCancel)
    assert.equal(calls, 0)
    const pending = new AbortController()
    axios.defaults.adapter = config => new Promise((resolve, reject) => {
        config.signal.addEventListener('abort', () => reject(new axios.CanceledError()), { once: true })
        queueMicrotask(() => pending.abort())
    })
    await assert.rejects(calculateTractionRemote(profile(), models.defaultTrainParameters(),
        models.defaultSignalParameters(), models.defaultRunOptions(), pending.signal), axios.isCancel)
})

const chartResult = (samples, extra = {}) => ({ ...serverResult(), mode: 'through', samples, ...extra })
test('chart cursor uses server samples for kinematic interpolation and constant-speed extension', () => {
    const samples = [
        { positionM: 0, speedKmh: 0, limitKmh: 36, timeS: 10 },
        { positionM: 50, speedKmh: 36, limitKmh: 36, timeS: 20 },
        { positionM: 100, speedKmh: 36, limitKmh: 36, timeS: 25 },
    ]
    const result = chartResult(samples)
    assert.equal(models.headTimeAt(result, 0), 10)
    assert.ok(Math.abs(models.headTimeAt(result, 12.5) - 15) < 1e-10)
    assert.equal(models.headTimeAt(result, 75), 22.5)
    assert.equal(models.headTimeAt(result, 120), 27)
    assert.equal(models.headTimeAt(result, -1), null)
    assert.equal(models.headTimeAt(result, NaN), null)
    assert.equal(models.headTimeAt({ ...result, mode: 'arrival' }, 120), null)
})

test('chart cursor returns stop arrival and interpolates onward from departure after dwell', () => {
    const samples = [
        { positionM: 0, speedKmh: 36, limitKmh: 36, timeS: 0 },
        { positionM: 50, speedKmh: 0, limitKmh: 36, timeS: 10 },
        { positionM: 50, speedKmh: 0, limitKmh: 36, timeS: 40 },
        { positionM: 100, speedKmh: 36, limitKmh: 36, timeS: 50 },
        { positionM: 150, speedKmh: 0, limitKmh: 36, timeS: 60 },
        { positionM: 150, speedKmh: 0, limitKmh: 36, timeS: 90 },
    ]
    const result = chartResult(samples)
    assert.equal(models.headTimeAt(result, 50), 10)
    assert.ok(Math.abs(models.headTimeAt(result, 62.5) - 45) < 1e-10)
    assert.equal(models.headTimeAt(result, 150), 60)
    assert.equal(models.headTimeAt(result, 151), null)
})


test('braking-reserve chart translates original geometry without changing inputs or inventing circuits', () => {
    const input = profile(), before = structuredClone(input)
    const translated = models.withBrakingApproach(input, 1250, 180)
    assert.deepEqual(input, before)
    assert.equal(translated.lengthM, 2250)
    assert.deepEqual(translated.segments[0], { ...input.segments[0], startM: 0, endM: 1250, speedLimitKmh: 180 })
    assert.equal(translated.segments[1].startM, 1250)
    assert.equal(translated.devices[0].positionM, 1350)
    assert.equal(translated.joints[0].positionM, 1750)
    assert.equal(translated.cells.length, 1)
    assert.equal(translated.cells[0].startM, 1250)
    assert.equal(translated.cells[0].endM, 2250)
    assert.equal(translated.cells[0].entryBoundaryKnown, false)
    assert.deepEqual(models.withBrakingApproach(input, 1250, 180), translated)
    assert.equal(models.withBrakingApproach(input, 0, 180), input)
})

test('first departure section uses its own speed limit and inherits terminal grade and resistance without changing the route', () => {
    const input = profile(), before = structuredClone(input)
    const expanded = models.withDepartureSection(input, 850.5, 160)
    assert.deepEqual(input, before)
    assert.equal(expanded.lengthM, 1850.5)
    assert.deepEqual(expanded.segments.at(-1), { ...input.segments.at(-1), startM: 1000, endM: 1850.5, speedLimitKmh: 160 })
    assert.deepEqual(expanded.cells.at(-1), { id: '__first-departure__', label: '一离去', startM: 1000,
        endM: 1850.5, entryBoundaryKnown: true, exitBoundaryKnown: true })
    assert.deepEqual(expanded.devices, input.devices)
    assert.deepEqual(expanded.joints, input.joints)
    assert.equal(models.withDepartureSection(input, 0, 160), input)
    assert.deepEqual(models.withDepartureSection(input, 850.5, 160), expanded, 'rebuild from the saved request does not append twice')
    const collision = { ...input, cells: [...input.cells, { ...input.cells[0], id: '__first-departure__' }] }
    assert.equal(models.withDepartureSection(collision, 20, 160).cells.at(-1).id, '__first-departure___')
    const run = { ...models.defaultRunOptions(), mode: 'departure', initialSpeedKmh: 0, departureSectionLengthM: 850.5 }
    const request = buildTractionApiRequest(input, models.defaultTrainParameters(), models.defaultSignalParameters(), run)
    assert.equal(request.options.departureSectionLengthM, 850.5)
    assert.equal(request.profile.lengthM, 1000, 'the server receives original station geometry plus a separate length option')
})

const approachingResult = () => chartResult([
    { positionM: 0, speedKmh: 36, limitKmh: 36, timeS: 18 },
    { positionM: 100, speedKmh: 36, limitKmh: 36, timeS: 28 },
    { positionM: 300, speedKmh: 36, limitKmh: 36, timeS: 48 },
], { mode: 'arrival', approachDistanceM: 100, lengthM: 300 })
const approachingCell = (startM = 100) => ({ id: '1G', label: '1G', startM, endM: startM + 50,
    entryTimeS: 18 + startM / 10, exitTimeS: null, releaseTimeS: null })
test('shunting occupancy uses the native route origin and keeps unknown releases unknown', () => {
    const result = { ...approachingResult(), mode: 'shunting', approachDistanceM: 0,
        cellOccupancies: [{ ...approachingCell(0), entryTimeS: 18, exitTimeS: 33, releaseTimeS: 38 }, approachingCell(250)] }
    assert.deepEqual(models.cellApproachingInterval(result, result.cellOccupancies[0]), {
        startPositionM: 0, startTimeS: 18, endTimeS: 18, durationS: 0,
    })
    assert.deepEqual(models.cellApproachingInterval(result, result.cellOccupancies[1]), {
        startPositionM: 0, startTimeS: 18, endTimeS: 43, durationS: 25,
    })
    assert.equal(models.cellTotalOccupancyInterval(result, result.cellOccupancies[0], models.defaultSignalParameters()).durationS, 38)
    assert.equal(models.cellTotalOccupancyInterval(result, result.cellOccupancies[1], models.defaultSignalParameters()), null)
    assert.equal(models.summarizeTractionResult(result, { ...profile(), lengthM: 300 }, models.defaultSignalParameters()).brakingDistanceM, 0)
})

test('all movement modes end occupied terminal circuits at first stop before dwell and release delay', () => {
    for (const mode of ['arrival','departure','through','shunting']) {
        const result={...serverResult(),mode,approachDistanceM:0}
        Object.assign(result.cellOccupancies[0],{entryTimeS:18,entryBoundaryKnown:true})
        result.samples.push({...result.samples.at(-1),timeS:198})
        const before=structuredClone(result),cell=result.cellOccupancies[0],signal={...models.defaultSignalParameters(),clearanceS:99}
        assert.equal(models.cellTerminalStopTime(result,cell),138)
        assert.equal(models.cellOccupancyEndTime(result,cell),138)
        assert.deepEqual(models.cellTotalOccupancyInterval(result,cell,signal),{startTimeS:0,endTimeS:138,durationS:138})
        const summary=models.summarizeTractionResult(result,profile(),signal)
        assert.equal(summary.longestOccupancy.durationS,138)
        assert.equal(summary.unknownOccupancyCount,0)
        assert.equal(models.formatTractionDuration(summary.longestOccupancy.durationS),'02:18')
        assert.deepEqual(result,before,'the occupation convention preserves saved physical clearance and release')
    }
})

test('terminal-stop ending requires positive overlap with the stopped train and supports clipped exits', () => {
    const result={...serverResult(),mode:'shunting',trainLengthM:400}
    const cell={...result.cellOccupancies[0],entryTimeS:18,entryBoundaryKnown:true}
    for (const bounds of [{startM:500,endM:800},{startM:800,endM:1000},{startM:500,endM:800,exitBoundaryKnown:false}])
        assert.equal(models.cellTerminalStopTime(result,{...cell,...bounds}),138)
    for (const bounds of [{startM:0,endM:500},{startM:500,endM:600},{startM:1000,endM:1100},{startM:1001,endM:1100}])
        assert.equal(models.cellTerminalStopTime(result,{...cell,...bounds}),null)
    assert.equal(models.cellTerminalStopTime(result,{...cell,entryBoundaryKnown:false,entryTimeS:null}),138)
})

test('moving endpoints, missing events and malformed data never borrow the terminal-stop convention', () => {
    const result={...serverResult(),mode:'shunting'},cell={...result.cellOccupancies[0],entryTimeS:18,entryBoundaryKnown:true}
    for (const change of [{entryTimeS:null},{entryTimeS:999},{entryTimeS:NaN},{exitTimeS:undefined},{releaseTimeS:undefined},
        {exitTimeS:NaN},{releaseTimeS:Infinity},{entryBoundaryKnown:false},{startM:-1},{endM:1001}])
        assert.equal(models.cellTerminalStopTime(result,{...cell,...change}),null)
    for (const changed of [{...result,trainLengthM:NaN},{...result,trainLengthM:0},{...result,lengthM:999},
        {...result,samples:result.samples.map(p=>({...p,speedKmh:36}))},{...result,samples:[]}])
        assert.equal(models.cellTerminalStopTime(changed,cell),null)
    const cleared={...cell,startM:0,endM:500,exitTimeS:100,releaseTimeS:143}
    assert.equal(models.cellTerminalStopTime(result,cleared),null)
    assert.equal(models.cellOccupancyEndTime(result,cleared),143,'an already-cleared circuit retains its real release delay')
})
test('all arrival circuits share braking-range entry time and end at their own head entry', () => {
    const result = approachingResult(), original = structuredClone(result)
    assert.deepEqual(models.cellApproachingInterval(result, approachingCell()), {
        startPositionM: 0, startTimeS: 18, endTimeS: 28, durationS: 10,
    })
    assert.deepEqual(models.cellApproachingInterval(result, approachingCell(250)), {
        startPositionM: 0, startTimeS: 18, endTimeS: 43, durationS: 25,
    })
    assert.deepEqual(models.cellApproachingInterval(result, { ...approachingCell(), releaseTimeS: 999, exitBoundaryKnown: false }),
        models.cellApproachingInterval(result, approachingCell()), 'release and unknown exit do not affect approaching time')
    assert.deepEqual(result, original, 'saved result stays unchanged')
})
test('shared approaching start includes subsequent dwell and retains saved head-entry times', () => {
    const result = chartResult([
        { positionM: 0, speedKmh: 36, limitKmh: 36, timeS: 0 },
        { positionM: 50, speedKmh: 0, limitKmh: 36, timeS: 10 },
        { positionM: 50, speedKmh: 0, limitKmh: 36, timeS: 40 },
        { positionM: 100, speedKmh: 36, limitKmh: 36, timeS: 50 },
        { positionM: 150, speedKmh: 0, limitKmh: 36, timeS: 60 },
    ], { mode: 'arrival', approachDistanceM: 50, motionStartS: 0, lengthM: 150 })
    assert.deepEqual(models.cellApproachingInterval(result, { ...approachingCell(100), entryTimeS: 50 }), {
        startPositionM: 0, startTimeS: 0, endTimeS: 50, durationS: 50,
    })
    const endTimeS = models.headTimeAt(result, 112.5)
    const interval = models.cellApproachingInterval(result, { ...approachingCell(112.5), entryTimeS: endTimeS })
    assert.equal(interval.startTimeS, 0)
    assert.equal(interval.endTimeS, endTimeS)
    assert.equal(interval.durationS, endTimeS)
    const originDwell = { ...result, samples: [
        { positionM: 0, speedKmh: 0, limitKmh: 36, timeS: 0 },
        { positionM: 0, speedKmh: 0, limitKmh: 36, timeS: 5 },
        ...result.samples.slice(1),
    ] }
    assert.equal(models.cellApproachingInterval(originDwell, { ...approachingCell(100), entryTimeS: 50 }).startTimeS, 0,
        'shared start uses first arrival at the origin, before any dwell')
})
test('arrival approaching rectangles require a saved braking reservation and known entrance events', () => {
    const result = approachingResult(), cell = approachingCell()
    for (const approachDistanceM of [undefined, -1, NaN, Infinity]) assert.equal(models.cellApproachingInterval({ ...result, approachDistanceM }, cell), null)
    for (const invalid of [ { ...cell, entryTimeS: null }, { ...cell, entryBoundaryKnown: false },
        { ...cell, entryTimeS: 17 }, { ...cell, startM: 99 }, { ...cell, startM: 301 } ])
        assert.equal(models.cellApproachingInterval(result, invalid), null)
    assert.equal(models.cellApproachingInterval(null, cell), null)
    assert.equal(models.cellApproachingInterval({ ...result, samples: [] }, cell), null)
    assert.equal(models.cellApproachingInterval({ ...result, samples: result.samples.slice(1) }, cell), null,
        'a clipped saved run cannot supply the shared start')
    assert.equal(models.cellApproachingInterval(result, { ...cell, startM: 100 - 1e-8 }).startTimeS, 18, 'tiny coordinate roundoff is absorbed')
})

test('optional braking-distance requests distinguish blank, explicit zero and entered distances', () => {
    const input = profile(), train = models.defaultTrainParameters(), signal = models.defaultSignalParameters()
    for (const [entered, expected] of [[undefined, null], [null, null], [0, 0], [123.45, 123.45]]) {
        const run = { ...models.defaultRunOptions(), brakingDistanceM: entered }
        assert.equal(buildTractionApiRequest(input, train, signal, run).options.brakingDistanceM, expected)
    }
    assert.equal(models.defaultRunOptions().brakingDistanceM, null, 'the empty input keeps automatic calculation')
    const result = { ...approachingResult(), approachDistanceM: 0,
        cellOccupancies: [{ ...approachingCell(0), entryTimeS: 18, exitTimeS: 38, releaseTimeS: 43 }] }
    assert.deepEqual(models.cellApproachingInterval(result, result.cellOccupancies[0]), {
        startPositionM: 0, startTimeS: 18, endTimeS: 18, durationS: 0,
    })
    assert.deepEqual(models.cellTotalOccupancyInterval(result, result.cellOccupancies[0], signal), {
        startTimeS: 0, endTimeS: 43, durationS: 43,
    }, 'an explicit zero reserve still includes route setting, physical occupancy and release')
})

const departureResult = () => chartResult([
    { positionM: 0, speedKmh: 0, limitKmh: 72, timeS: 18 },
    { positionM: 100, speedKmh: 72, limitKmh: 72, timeS: 28 },
    { positionM: 300, speedKmh: 72, limitKmh: 72, timeS: 38 },
], { mode: 'departure', lengthM: 300, cellOccupancies: [
    { id: 'B', label: 'B', startM: 100, endM: 300, entryTimeS: 28, exitTimeS: 48, releaseTimeS: 53 },
    { id: 'A', label: 'A', startM: 0, endM: 100, entryTimeS: 18, exitTimeS: 38, releaseTimeS: 43 },
] })

test('departure approach starts at the first circuit by position, including its zero approach duration', () => {
    const result = departureResult(), before = structuredClone(result)
    assert.deepEqual(models.cellApproachingInterval(result, result.cellOccupancies[0]), {
        startPositionM: 0, startTimeS: 18, endTimeS: 28, durationS: 10,
    })
    assert.deepEqual(models.cellApproachingInterval(result, result.cellOccupancies[1]), {
        startPositionM: 0, startTimeS: 18, endTimeS: 18, durationS: 0,
    })
    assert.deepEqual(result, before, 'display phases never change saved entry or release events')
})

test('departure phases join from route setting through reaction and approach to physical occupancy and release', () => {
    const result = departureResult(), signal = models.defaultSignalParameters()
    for (const cell of result.cellOccupancies) {
        const approach = models.cellApproachingInterval(result, cell)
        const preparation = models.approachPreparationIntervals(approach, signal, result.mode)
        assert.deepEqual(preparation, [
            { kind: 'route-setting', startTimeS: 0, endTimeS: 15, durationS: 15 },
            { kind: 'reaction', startTimeS: 15, endTimeS: 18, durationS: 3 },
        ])
        const total = models.cellTotalOccupancyInterval(result, cell, signal)
        assert.deepEqual(total, { startTimeS: 0, endTimeS: cell.releaseTimeS, durationS: cell.releaseTimeS })
        assert.equal(preparation[1].endTimeS, approach.startTimeS)
        assert.equal(approach.endTimeS, cell.entryTimeS)
        assert.equal(total.durationS, signal.routeSettingS + signal.reactionS + approach.durationS
            + cell.exitTimeS - cell.entryTimeS + cell.releaseTimeS - cell.exitTimeS)
    }
})

test('departure keeps the saved request origin when the first circuit is beyond the sample origin', () => {
    const result = departureResult(), signal = models.defaultSignalParameters()
    result.cellOccupancies = [result.cellOccupancies[0]]
    const cell = result.cellOccupancies[0]
    const approach = models.cellApproachingInterval(result, cell)
    assert.deepEqual(approach, { startPositionM: 100, startTimeS: 28, endTimeS: 28, durationS: 0 })
    assert.deepEqual(models.approachPreparationIntervals(approach, signal, result.mode), [
        { kind: 'route-setting', startTimeS: 0, endTimeS: 15, durationS: 15 },
        { kind: 'reaction', startTimeS: 15, endTimeS: 18, durationS: 3 },
    ])
    assert.deepEqual(models.cellTotalOccupancyInterval(result, cell, signal), {
        startTimeS: 0, endTimeS: 53, durationS: 53,
    })
})

test('unknown first departure entry never falls back to a later circuit or an invented origin', () => {
    for (const change of [{ entryTimeS: null }, { entryBoundaryKnown: false }, { entryTimeS: NaN },
        { entryTimeS: -1 }, { startM: -1 }, { entryTimeS: 29 }]) {
        const result = departureResult()
        Object.assign(result.cellOccupancies[1], change)
        assert.equal(models.cellApproachingInterval(result, result.cellOccupancies[0]), null)
        assert.equal(models.cellTotalOccupancyInterval(result, result.cellOccupancies[0], models.defaultSignalParameters()), null)
    }
    const result = departureResult(), cell = result.cellOccupancies[0]
    for (const change of [{ entryTimeS: null }, { entryBoundaryKnown: false }, { entryTimeS: 17 }, { startM: 301 }])
        assert.equal(models.cellApproachingInterval(result, { ...cell, ...change }), null)
    assert.equal(models.cellApproachingInterval({ ...result, samples: [] }, cell), null)
    assert.equal(models.cellApproachingInterval({ ...result, samples: result.samples.slice(1) }, cell), null)
    assert.equal(models.cellApproachingInterval({ ...result, cellOccupancies: [] }, cell), null)
})

test('departure zero preparation or release delay stays valid; missing clearance or signal leaves total unknown', () => {
    const result = departureResult(), cell = result.cellOccupancies[0]
    const signal = { routeSettingS: 0, reactionS: 0, clearanceS: 0 }
    const approach = models.cellApproachingInterval(result, cell)
    assert.deepEqual(models.approachPreparationIntervals(approach, signal, result.mode), [])
    assert.deepEqual(models.cellTotalOccupancyInterval(result, { ...cell, releaseTimeS: cell.exitTimeS }, signal), {
        startTimeS: 0, endTimeS: 48, durationS: 48,
    })
    for (const change of [{ exitTimeS: null }, { releaseTimeS: null }, { exitBoundaryKnown: false },
        { exitTimeS: 27 }, { releaseTimeS: 47 }, { exitTimeS: NaN }, { releaseTimeS: Infinity }])
        assert.equal(models.cellTotalOccupancyInterval(result, { ...cell, ...change }, signal), null)
    for (const invalid of [null, { ...signal, reactionS: 19 }, { ...signal, routeSettingS: -1 }, { ...signal, reactionS: NaN }])
        assert.equal(models.cellTotalOccupancyInterval(result, cell, invalid), null)
})

test('through shares braking-range entry and displays preparation, approach and total occupancy like arrival', () => {
    const result = { ...approachingResult(), mode: 'through' }
    const before = structuredClone(result), signal = models.defaultSignalParameters()
    for (const startM of [100, 250]) {
        const cell = { ...approachingCell(startM), exitTimeS: 60, releaseTimeS: 65 }
        const approach = models.cellApproachingInterval(result, cell)
        assert.deepEqual(approach, { startPositionM: 0, startTimeS: 18,
            endTimeS: cell.entryTimeS, durationS: cell.entryTimeS - 18 })
        assert.deepEqual(models.approachPreparationIntervals(approach, signal, result.mode), [
            { kind: 'route-setting', startTimeS: 0, endTimeS: 15, durationS: 15 },
            { kind: 'reaction', startTimeS: 15, endTimeS: 18, durationS: 3 },
        ])
        assert.deepEqual(models.cellTotalOccupancyInterval(result, cell, signal), {
            startTimeS: 0, endTimeS: 65, durationS: 65,
        })
        assert.deepEqual(approach, models.cellApproachingInterval(approachingResult(), cell))
    }
    assert.equal(models.cellApproachingInterval(result, approachingCell(0)), null, 'the reserve itself contains no invented circuit')
    assert.equal(models.cellApproachingInterval({ ...result, samples: result.samples.slice(1) }, approachingCell(250)), null)
    assert.equal(models.cellApproachingInterval({ ...result, approachDistanceM: -1 }, approachingCell(250)), null)
    assert.deepEqual(result, before)
})

test('legacy through approach keeps the saved run origin without adding a fictitious reserve', () => {
    const result = { ...approachingResult(), mode: 'through', approachDistanceM: undefined,
        cellOccupancies: [{ ...approachingCell(0), entryTimeS: null, entryBoundaryKnown: false }] }
    assert.deepEqual(models.cellApproachingInterval(result, approachingCell(250)), {
        startPositionM: 0, startTimeS: 18, endTimeS: 43, durationS: 25,
    })
    assert.equal(models.cellApproachingInterval({ ...result, samples: result.samples.slice(1) }, approachingCell(250)), null)
    assert.equal(models.cellApproachingInterval({ ...result, samples: [] }, approachingCell(250)), null)
    for (const change of [{ entryTimeS: null }, { entryTimeS: 17 }, { entryBoundaryKnown: false }, { startM: 301 }])
        assert.equal(models.cellApproachingInterval(result, { ...approachingCell(100), ...change }), null)
})

test('through zero phases stay valid and incomplete or incompatible saved events leave totals unknown', () => {
    const result = { ...approachingResult(), mode: 'through', approachDistanceM: 0 }
    const cell = { ...approachingCell(100), exitTimeS: 50, releaseTimeS: 50 }
    const signal = models.defaultSignalParameters()
    assert.deepEqual(models.cellTotalOccupancyInterval(result, cell, signal), {
        startTimeS: 0, endTimeS: 50, durationS: 50,
    })
    for (const change of [{ exitTimeS: null }, { releaseTimeS: null }, { exitBoundaryKnown: false },
        { exitTimeS: 27 }, { releaseTimeS: 49 }, { releaseTimeS: Infinity }])
        assert.equal(models.cellTotalOccupancyInterval(result, { ...cell, ...change }, signal), null)
    for (const invalid of [null, { ...signal, reactionS: 0 }, { ...signal, reactionS: 19 }])
        assert.equal(models.cellTotalOccupancyInterval(result, cell, invalid), null)
    result.samples = result.samples.map(sample => ({ ...sample, timeS: sample.timeS - 18 }))
    cell.entryTimeS -= 18; cell.exitTimeS -= 18; cell.releaseTimeS -= 18
    const zero = { routeSettingS: 0, reactionS: 0, clearanceS: 0 }
    assert.deepEqual(models.approachPreparationIntervals(models.cellApproachingInterval(result, cell), zero, result.mode), [])
    assert.deepEqual(models.cellTotalOccupancyInterval(result, cell, zero), { startTimeS: 0, endTimeS: 32, durationS: 32 })
})


test('saved preparation intervals join route setting, reaction and the shared approach start', () => {
    const approaching = models.cellApproachingInterval(approachingResult(), approachingCell())
    const signal = models.defaultSignalParameters(), before = structuredClone(signal)
    assert.deepEqual(models.approachPreparationIntervals(approaching, signal), [
        { kind: 'route-setting', startTimeS: 0, endTimeS: 15, durationS: 15 },
        { kind: 'reaction', startTimeS: 15, endTimeS: 18, durationS: 3 },
    ])
    assert.deepEqual(signal, before)
    for (const [routeSettingS, reactionS, kinds] of [[0, 18, ['reaction']], [18, 0, ['route-setting']], [0, 0, []]]) {
        const intervals = models.approachPreparationIntervals({ ...approaching, startTimeS: routeSettingS + reactionS },
            { ...signal, routeSettingS, reactionS })
        assert.deepEqual(intervals.map(interval => interval.kind), kinds)
        assert.ok(intervals.every(interval => interval.endTimeS > interval.startTimeS))
    }
})

test('missing or incompatible saved preparation timing does not invent rectangles', () => {
    const approaching = models.cellApproachingInterval(approachingResult(), approachingCell())
    const signal = models.defaultSignalParameters()
    assert.deepEqual(models.approachPreparationIntervals(null, signal), [])
    assert.deepEqual(models.approachPreparationIntervals(approaching, null), [])
    for (const invalid of [{ ...signal, reactionS: 19 }, { ...signal, routeSettingS: -1 },
        { ...signal, reactionS: NaN }, { ...signal, routeSettingS: Infinity }])
        assert.deepEqual(models.approachPreparationIntervals(approaching, invalid), [])
})

test('run summary separates the original route and reserve, uses actual speeds and includes approach and dwell', () => {
    const result = { ...approachingResult(), mode: 'through', samples: [
        { positionM: 0, speedKmh: 36, limitKmh: 80, timeS: 18 },
        { positionM: 100, speedKmh: 36, limitKmh: 80, timeS: 28 },
        { positionM: 150, speedKmh: 0, limitKmh: 80, timeS: 38 },
        { positionM: 150, speedKmh: 0, limitKmh: 80, timeS: 68 },
        { positionM: 300, speedKmh: 18, limitKmh: 80, timeS: 98 },
    ], cellOccupancies: [
        { ...approachingCell(250), id: 'B', label: 'B', entryTimeS: 88, exitTimeS: 108, releaseTimeS: 113 },
        { ...approachingCell(100), id: 'A', label: 'A', entryTimeS: 28, exitTimeS: 48, releaseTimeS: 53 },
    ] }
    const before = structuredClone(result)
    const summary = models.summarizeTractionResult(result, { ...profile(), lengthM: 200 }, models.defaultSignalParameters())
    assert.equal(summary.brakingDistanceM, 100)
    assert.equal(summary.routeLengthM, 200)
    assert.equal(summary.entrySpeedKmh, 36)
    assert.equal(summary.exitSpeedKmh, 18)
    assert.equal(summary.journeyTimeS, 80, 'head travels from the reserve origin to the final point; no preparation or tail extension')
    assert.equal(summary.longestOccupancy.cell.id, 'B', 'the maximum is independent of array order')
    assert.equal(summary.longestOccupancy.durationS, 113, 'use the complete reservation, including preparation and release')
    assert.equal(summary.unknownOccupancyCount, 0)
    assert.deepEqual(result, before, 'summary never modifies the saved result')
})

test('summary maximum agrees with plotted totals in all movement modes and preserves unknown circuits', () => {
    for (const mode of ['arrival', 'through', 'departure']) {
        const result = mode === 'departure' ? departureResult() : { ...approachingResult(), mode,
            cellOccupancies: [
                { ...approachingCell(100), id: 'A', exitTimeS: 48, releaseTimeS: 53 },
                { ...approachingCell(250), id: 'B', exitTimeS: 60, releaseTimeS: 65 },
            ] }
        result.cellOccupancies.push({ ...approachingCell(280), id: 'uncleared', entryTimeS: 47 })
        const signal = models.defaultSignalParameters()
        const summary = models.summarizeTractionResult(result, { ...profile(), lengthM: mode === 'departure' ? 300 : 200 }, signal)
        const known = result.cellOccupancies.map(cell => models.cellTotalOccupancyInterval(result, cell, signal)).filter(Boolean)
        assert.equal(summary.longestOccupancy.durationS, Math.max(...known.map(total => total.durationS)))
        assert.equal(summary.unknownOccupancyCount, 1)
        assert.equal(summary.brakingDistanceM, mode === 'departure' ? 0 : 100)
        const unknown = models.summarizeTractionResult({ ...result, cellOccupancies: result.cellOccupancies.map(cell => ({ ...cell, releaseTimeS: null })) }, profile(), signal)
        assert.equal(unknown.longestOccupancy, null, 'missing releases are not replaced by zero or journey end')
    }
    const result = approachingResult()
    const empty = models.summarizeTractionResult({ ...result, samples: [], cellOccupancies: [] }, profile(), null)
    assert.equal(empty.journeyTimeS, null)
    assert.equal(empty.entrySpeedKmh, null)
    assert.equal(empty.exitSpeedKmh, null)
    assert.equal(empty.longestOccupancy, null)
})

test('summary and circuit duration labels share padded mm:ss rounding and unknown handling', () => {
    for (const [seconds, expected] of [[0, '00:00'], [9.4, '00:09'], [59.5, '01:00'],
        [75, '01:15'], [3600, '60:00'], [6000, '100:00'], [null, '--:--'], [undefined, '--:--'],
        [NaN, '--:--'], [Infinity, '--:--'], [-1, '--:--']]) {
        assert.equal(models.formatTractionDuration(seconds), expected)
    }
})

test('departure summary includes the added circuit in duration and minimum interval without calling it braking distance', () => {
    const result = departureResult(), original = { ...profile(), lengthM: 300, cells: result.cellOccupancies }
    const cell = models.departureSectionCell(original, 250)
    Object.assign(result, { lengthM: 550, departureSectionLengthM: 250 })
    result.samples.push({ positionM: 550, speedKmh: 36, limitKmh: 72, timeS: 70 })
    result.cellOccupancies.push({ ...cell, entryTimeS: 38, exitTimeS: 90, releaseTimeS: 95 })
    const summary = models.summarizeTractionResult(result, original, models.defaultSignalParameters())
    assert.equal(summary.brakingDistanceM, 0)
    assert.equal(summary.routeLengthM, 300)
    assert.equal(summary.journeyTimeS, 52)
    assert.equal(summary.longestOccupancy.cell.label, '一离去')
    assert.equal(summary.longestOccupancy.durationS, 95)
})

test('through chart and summary keep the braking reserve separate from the departure circuit', () => {
    const original = profile(), departureLength = 850.5, approach = 125
    const chart = models.withBrakingApproach(models.withDepartureSection(original, departureLength, 160), approach, 160)
    assert.equal(chart.lengthM, 1975.5)
    assert.equal(chart.cells.at(-1).startM, 1125)
    assert.equal(chart.cells.at(-1).endM, 1975.5)
    assert.equal(chart.cells[0].startM, 125, 'original circuits shift only by the braking reserve')
    const result = { ...serverResult(), mode: 'through', approachDistanceM: approach, departureSectionLengthM: departureLength,
        lengthM: chart.lengthM, cellOccupancies: [{ ...chart.cells.at(-1), entryTimeS: 100, exitTimeS: 200, releaseTimeS: 205 }] }
    result.samples[1].positionM = result.lengthM
    const summary = models.summarizeTractionResult(result, original, models.defaultSignalParameters())
    assert.equal(summary.brakingDistanceM, 125)
    assert.equal(summary.routeLengthM, 1000)
    assert.equal(summary.journeyTimeS, 120)
    assert.equal(summary.longestOccupancy.cell.label, '一离去')
    assert.equal(summary.longestOccupancy.durationS, 205)
    const legacy = { ...result, approachDistanceM: undefined }
    assert.equal(models.summarizeTractionResult(legacy, original, models.defaultSignalParameters()).brakingDistanceM, 125,
        'distance fallback must subtract the departure section before reporting braking distance')
})

test('extension speed limits default to the train maximum and apply independently within vehicle limits', () => {
    const train = models.defaultTrainParameters(), before = structuredClone(train)
    assert.deepEqual(models.extensionSpeedLimits(train, {}), { brakingMaxSpeedKmh: 160, departureSectionMaxSpeedKmh: 160 })
    const run = { ...models.defaultRunOptions(), brakingMaxSpeedKmh: 36, departureSectionMaxSpeedKmh: 90 }
    assert.deepEqual(models.extensionSpeedLimits(train, run), { brakingMaxSpeedKmh: 36, departureSectionMaxSpeedKmh: 90 })
    assert.deepEqual(models.extensionSpeedLimits(train, { brakingMaxSpeedKmh: 500, departureSectionMaxSpeedKmh: null }),
        { brakingMaxSpeedKmh: 160, departureSectionMaxSpeedKmh: 160 })
    const original = profile(), originalCopy = structuredClone(original)
    const speeds = models.extensionSpeedLimits(train, run)
    const chart = models.withBrakingApproach(models.withDepartureSection(original, 850, speeds.departureSectionMaxSpeedKmh), 125, speeds.brakingMaxSpeedKmh)
    assert.equal(chart.segments[0].speedLimitKmh, 36)
    assert.equal(chart.segments[1].speedLimitKmh, 80)
    assert.equal(chart.segments.at(-1).speedLimitKmh, 90)
    assert.deepEqual(train, before, 'a braking speed cap must not become the train global maximum')
    assert.deepEqual(original, originalCopy, 'station limits remain independent of extension limits')
})

test('API snapshots preserve optional extension speeds and normalize cleared or legacy inputs to null', () => {
    const train = models.defaultTrainParameters(), signal = models.defaultSignalParameters()
    for (const [brakingMaxSpeedKmh, departureSectionMaxSpeedKmh] of [[36, 90], [null, null], [undefined, undefined]]) {
        const run = { ...models.defaultRunOptions(), brakingMaxSpeedKmh, departureSectionMaxSpeedKmh }
        const request = buildTractionApiRequest(profile(), train, signal, run)
        assert.equal(request.options.brakingMaxSpeedKmh, brakingMaxSpeedKmh ?? null)
        assert.equal(request.options.departureSectionMaxSpeedKmh, departureSectionMaxSpeedKmh ?? null)
    }
})

test('gamma display and edits preserve historical inertial multipliers across repeated saves', () => {
    for (const [factor, coefficient] of [[1, 0], [1.06, 0.06], [1.08, 0.08], [1.1, 0.1], [1.1234, 0.1234], [3, 2]]) {
        const train = models.defaultTrainParameters()
        train.rotatingMassFactor = factor
        assert.equal(models.rotatingMassCoefficientFromFactor(factor), coefficient)
        for (let i = 0; i < 20; i++) {
            train.rotatingMassFactor = models.rotatingMassFactorFromCoefficient(models.rotatingMassCoefficientFromFactor(train.rotatingMassFactor))
        }
        assert.equal(train.rotatingMassFactor, factor, 'opening and saving never applies +1 twice or accumulates decimal error')
        const request = buildTractionApiRequest(profile(), train, models.defaultSignalParameters(), models.defaultRunOptions())
        assert.equal(request.train.rotatingMassFactor, factor, 'API still receives the total inertial multiplier')
    }
    assert.ok(Number.isNaN(models.rotatingMassFactorFromCoefficient(NaN)), 'an empty coefficient must remain invalid, not become zero')
})
