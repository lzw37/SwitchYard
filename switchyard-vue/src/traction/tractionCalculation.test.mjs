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
    const failure = { code: 'infeasible-profile', message: 'Final speed cannot be reached.', errors: { 'options.finalSpeedKmh': ['unreachable'] } }
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


test('arrival chart translates original geometry without changing inputs or inventing circuits', () => {
    const input = profile(), before = structuredClone(input)
    const translated = models.withArrivalApproach(input, 1250, 180)
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
    assert.deepEqual(models.withArrivalApproach(input, 1250, 180), translated)
    assert.equal(models.withArrivalApproach(input, 0, 180), input)
})

const approachingResult = () => chartResult([
    { positionM: 0, speedKmh: 36, limitKmh: 36, timeS: 18 },
    { positionM: 100, speedKmh: 36, limitKmh: 36, timeS: 28 },
    { positionM: 300, speedKmh: 36, limitKmh: 36, timeS: 48 },
], { mode: 'arrival', approachDistanceM: 100, lengthM: 300 })
const approachingCell = (startM = 100) => ({ id: '1G', label: '1G', startM, endM: startM + 50,
    entryTimeS: 18 + startM / 10, exitTimeS: null, releaseTimeS: null })
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
test('approaching rectangles are omitted for other modes, old snapshots and unknown entrance events', () => {
    const result = approachingResult(), cell = approachingCell()
    for (const mode of ['departure', 'through']) assert.equal(models.cellApproachingInterval({ ...result, mode }, cell), null)
    for (const approachDistanceM of [undefined, 0, -1, NaN, Infinity]) assert.equal(models.cellApproachingInterval({ ...result, approachDistanceM }, cell), null)
    for (const invalid of [ { ...cell, entryTimeS: null }, { ...cell, entryBoundaryKnown: false },
        { ...cell, entryTimeS: 17 }, { ...cell, startM: 99 }, { ...cell, startM: 301 } ])
        assert.equal(models.cellApproachingInterval(result, invalid), null)
    assert.equal(models.cellApproachingInterval(null, cell), null)
    assert.equal(models.cellApproachingInterval({ ...result, samples: [] }, cell), null)
    assert.equal(models.cellApproachingInterval({ ...result, samples: result.samples.slice(1) }, cell), null,
        'a clipped saved run cannot supply the shared start')
    assert.equal(models.cellApproachingInterval(result, { ...cell, startM: 100 - 1e-8 }).startTimeS, 18, 'tiny coordinate roundoff is absorbed')
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
