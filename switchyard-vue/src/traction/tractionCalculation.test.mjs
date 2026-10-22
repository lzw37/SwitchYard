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
    assert.equal(train.brakingCurve, undefined)
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
    assert.equal(request.train.brakingCurve, undefined)
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
