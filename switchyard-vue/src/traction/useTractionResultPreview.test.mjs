import assert from 'node:assert/strict'
import test from 'node:test'
import axios from 'axios'
import { effectScope, ref } from 'vue'
import { useTractionResultPreview } from './useTractionResultPreview.ts'
import { calculatedTractionProfile, defaultRunOptions, defaultSignalParameters, defaultTrainParameters } from './tractionCalculation.ts'

const tick = () => new Promise(resolve => setImmediate(resolve))
const row = (id, routeId = 'R', continuationId = '', extra = {}) => ({
    id, routeId, continuationId, revision: 2, isSelected: false, isCurrent: true,
    trainName: 'Train', savedAtUtc: '2026-10-28T00:00:00Z', durationS: 90, ...extra,
})
const catalog = { selectedId: 'default', sets: [{ id: 'default', name: 'Default', revision: 1 }, { id: 'other', name: 'Other', revision: 1 }] }
const snapshot = (routeId = 'R', continuationId = '') => ({
    revision: 2, currentWorkspaceRevision: 2, isCurrent: true, savedAtUtc: '2026-10-28T00:00:00Z',
    selectedRouteId: routeId, continuationId,
    request: {
        profile: { lengthM: 1000, segments: [{ startM: 0, endM: 1000, speedLimitKmh: 80 }],
            devices: [{ id: 'S', label: 'Signal', type: 'signal', positionM: 100 }], joints: [],
            cells: [{ id: 'C', label: 'Circuit', startM: 0, endM: 1000 }] },
        train: defaultTrainParameters(), signal: defaultSignalParameters(), options: defaultRunOptions(),
    },
    result: { engine: 'SwitchYard.SpeedProfile', mode: 'arrival', lengthM: 1000, trainLengthM: 200,
        durationS: 90, runningTimeS: 90, dwellTimeS: 0, motionStartS: 18, totalTimeS: 108, maxSpeedKmh: 60,
        samples: [{ positionM: 0, speedKmh: 60, limitKmh: 80, timeS: 18 }, { positionM: 1000, speedKmh: 0, limitKmh: 80, timeS: 108 }],
        jointTimes: [], cellOccupancies: [], warnings: [] },
})
function setup(t, handler, initial = { instanceID: 'i', stationSchemeID: 's', routeID: 'R' }) {
    t.mock.method(axios, 'get', handler)
    t.mock.method(axios, 'post', () => assert.fail('Preview must not calculate or change selection'))
    t.mock.method(axios, 'put', () => assert.fail('Preview must not save or change selection'))
    const scope = effectScope(), selection = ref(initial)
    const state = scope.run(() => useTractionResultPreview(() => selection.value))
    t.after(() => scope.stop())
    return { state, selection, scope }
}
function apiHandler({ rows = [row('single')], saved = snapshot(), calls = [] } = {}) {
    return async (url, config) => {
        calls.push({ url, params: config.params })
        if (url.endsWith('/sets')) return { data: catalog }
        if (url.endsWith('/routes')) return { data: rows }
        return saved ? { data: saved } : { status: 204 }
    }
}

test('loads real saved samples using the current instance, station, set and route case without writes', async t => {
    const calls = [], saved = snapshot()
    const { state } = setup(t, apiHandler({ saved, calls }))
    await tick()
    assert.equal(state.calculation.value.result, saved.result)
    assert.deepEqual(calls.map(call => call.params), [
        { instanceID: 'i', stationSchemeID: 's' },
        { instanceID: 'i', stationSchemeID: 's', calculationSetID: 'default' },
        { instanceID: 'i', stationSchemeID: 's', calculationSetID: 'default', routeCaseID: 'single' },
    ])
    assert.equal(state.loading.value, false)
})

test('the current standalone result takes priority over combined runs, with no route switching interface', async t => {
    const calls = []
    const rows = [row('unrelated', 'X'), row('continuation', 'A', 'R'), row('through', 'R', 'D'), row('single')]
    const { state } = setup(t, apiHandler({ rows, calls }))
    await tick()
    assert.equal(state.selectedCaseID.value, 'single')
    assert.equal(state.selectCase, undefined)
    assert.equal(state.routeCases, undefined)
    assert.equal(calls.at(-1).params.routeCaseID, 'single')
    assert.equal(calls.filter(call => call.url.endsWith('/calculation')).length, 1)
})

test('missing standalone records fall back to combined runs with the current route at either end', async t => {
    for (const [primary, continuation] of [['R', 'D'], ['A', 'R']]) {
        await t.test(`${primary} → ${continuation}`, async t => {
            const calls = [], saved = snapshot(primary, continuation)
            const { state } = setup(t, apiHandler({ rows: [row('unrelated', 'X', 'Y'), row('combined', primary, continuation)], saved, calls }))
            await tick()
            assert.equal(state.selectedCaseID.value, 'combined')
            assert.equal(state.calculation.value.result, saved.result)
            assert.equal(state.calculation.value.selectedRouteId, primary)
            assert.equal(state.calculation.value.continuationId, continuation)
            assert.deepEqual(calls.at(-1).params, { instanceID: 'i', stationSchemeID: 's', calculationSetID: 'default', routeCaseID: 'combined' })
        })
    }
})

test('a standalone record without data tries current and newer combined results until data is found', async t => {
    const calls = []
    const normal = apiHandler({ rows: [
        row('unrelated', 'X', 'Y'), row('single'),
        row('older', 'A', 'R', { savedAtUtc: '2026-10-26T00:00:00Z' }),
        row('stale', 'R', 'D', { isCurrent: false, savedAtUtc: '2026-10-29T00:00:00Z' }),
        row('newer', 'R', 'D', { savedAtUtc: '2026-10-28T00:00:00Z' }),
    ], calls })
    const { state } = setup(t, (url, config) => {
        if (!url.endsWith('/calculation')) return normal(url, config)
        calls.push({ url, params: config.params })
        return Promise.resolve(config.params.routeCaseID === 'older' ? { data: snapshot('A', 'R') } : { status: 204 })
    })
    await tick()
    assert.deepEqual(calls.filter(call => call.url.endsWith('/calculation')).map(call => call.params.routeCaseID), ['single', 'newer', 'older'])
    assert.equal(state.selectedCaseID.value, 'older')
    assert.equal(state.calculation.value.continuationId, 'R')
})

test('missing snapshots for all matching standalone and combined records remain uncalculated', async t => {
    const { state } = setup(t, apiHandler({ rows: [row('single'), row('combined', 'A', 'R')], saved: null }))
    await tick()
    assert.equal(state.calculation.value, null)
    assert.equal(state.selectedCaseID.value, '')
    assert.equal(state.error.value, null)
})

test('a late combined result cannot overwrite a different scheme or start another fallback request', async t => {
    let resolveOld
    const calls = []
    const normal = apiHandler({ rows: [row('first', 'R', 'D'), row('next', 'A', 'R')] })
    const { state } = setup(t, (url, config) => {
        calls.push({ url, params: config.params })
        if (url.endsWith('/calculation') && config.params.calculationSetID === 'default') {
            return new Promise(resolve => { resolveOld = resolve })
        }
        return normal(url, config)
    })
    await tick()
    t.mock.method(axios, 'get', apiHandler())
    await state.selectSet('other')
    resolveOld({ status: 204 })
    await tick()
    assert.equal(state.selectedSetID.value, 'other')
    assert.equal(state.selectedCaseID.value, 'single')
    assert.equal(state.calculation.value.selectedRouteId, 'R')
    assert.equal(calls.filter(call => call.url.endsWith('/calculation')).length, 1)
})

test('empty results stay empty, and pending snapshots retain their stale status', async t => {
    const { state } = setup(t, apiHandler({ saved: null }))
    await tick()
    assert.equal(state.calculation.value, null)
    assert.equal(state.error.value, null)
    const saved = { ...snapshot(), isCurrent: false, currentWorkspaceRevision: 3 }
    t.mock.method(axios, 'get', apiHandler({ saved }))
    await state.reload()
    assert.equal(state.calculation.value.isCurrent, false)
    t.mock.method(axios, 'get', apiHandler({ rows: [row('unrelated', 'X')] }))
    await state.selectSet('other')
    assert.equal(state.selectedCaseID.value, '')
    assert.equal(state.calculation.value, null)
})

test('load failures and mismatched results clear previous curves and expose an error', async t => {
    const { state } = setup(t, apiHandler())
    await tick()
    assert.ok(state.calculation.value)
    t.mock.method(axios, 'get', async () => { throw new Error('Network failure') })
    await state.reload()
    assert.equal(state.calculation.value, null)
    assert.ok(state.error.value)
    t.mock.method(axios, 'get', apiHandler({ saved: snapshot('unrelated') }))
    await state.reload()
    assert.equal(state.calculation.value, null)
    assert.match(state.error.value.message, /another route/)
})

test('late set responses cannot replace a newly selected station or its loading state', async t => {
    let resolveOld
    const normal = apiHandler()
    const { state, selection } = setup(t, (url, config) => config.params.stationSchemeID === 's'
        ? new Promise(resolve => { resolveOld = resolve }) : normal(url, config))
    selection.value = { instanceID: 'i', stationSchemeID: 'new-station', routeID: 'R' }
    await tick()
    assert.ok(state.calculation.value)
    resolveOld({ data: { ...catalog, sets: [{ id: 'old', name: 'Old', revision: 1 }], selectedId: 'old' } })
    await tick()
    assert.equal(state.selectedSetID.value, 'default')
    assert.equal(state.loading.value, false)
})

test('late result responses cannot replace another set or update a closed preview', async t => {
    let resolveOld
    const normal = apiHandler()
    const { state, scope } = setup(t, (url, config) => url.endsWith('/calculation') && config.params.calculationSetID === 'default'
        ? new Promise(resolve => { resolveOld = resolve }) : normal(url, config))
    await tick()
    const other = { ...snapshot(), revision: 8 }
    t.mock.method(axios, 'get', apiHandler({ saved: other }))
    await state.selectSet('other')
    resolveOld({ data: snapshot() })
    await tick()
    assert.equal(state.selectedSetID.value, 'other')
    assert.equal(state.calculation.value.revision, 8)
    let resolveClosed
    t.mock.method(axios, 'get', (url, config) => url.endsWith('/calculation')
        ? new Promise(resolve => { resolveClosed = resolve }) : normal(url, config))
    const pending = state.selectSet('default')
    await tick()
    scope.stop()
    resolveClosed({ data: snapshot() })
    await pending
    assert.equal(state.calculation.value, null)
})

test('saved chart coordinates include both backend extensions and their saved speed caps without mutating inputs', () => {
    const saved = snapshot()
    saved.result = { ...saved.result, mode: 'through', approachDistanceM: 150, departureSectionLengthM: 300, lengthM: 1450 }
    saved.request.options = { ...saved.request.options, mode: 'through', brakingMaxSpeedKmh: 35, departureSectionMaxSpeedKmh: 55 }
    const original = structuredClone(saved)
    const profile = calculatedTractionProfile(saved)
    assert.equal(profile.lengthM, saved.result.lengthM)
    assert.equal(profile.devices[0].positionM, 250)
    assert.deepEqual(profile.cells.map(cell => [cell.startM, cell.endM]), [[150, 1150], [1150, 1450]])
    assert.deepEqual(profile.segments.map(segment => segment.speedLimitKmh), [35, 80, 55])
    assert.deepEqual(saved, original)
    delete saved.result.approachDistanceM
    assert.deepEqual(calculatedTractionProfile(saved), profile, 'legacy snapshots use the same inferred braking reserve')
})
