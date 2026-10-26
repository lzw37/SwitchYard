import assert from 'node:assert/strict'
import test from 'node:test'
import { buildRouteProfile } from './routeProfile.ts'
import { tractionBatchProfile } from './tractionBatch.ts'
import { buildTractionApiRequest } from './tractionApi.ts'
import { createDefaultTrainPresetCatalog } from './trainPresets.ts'
import { defaultRunOptions, defaultSignalParameters, defaultTrainParameters } from './tractionCalculation.ts'

const layout = () => ({
    nodes: [{ id: 'A', x: 0, y: 0 }, { id: 'B', x: 100, y: 0, actualX: 150.5 }, { id: 'C', x: 300, y: 0, actualX: 500 }],
    tracks: [
        { id: 'AB', fromNodeID: 'A', toNodeID: 'B', x1: 0, y1: 0, x2: 100, y2: 0, speedLimitKmh: 80, gradientPermille: 4 },
        { id: 'BC', fromNodeID: 'B', toNodeID: 'C', x1: 100, y1: 0, x2: 300, y2: 0, speedLimitKmh: 60, gradientPermille: 0 },
    ],
    switches: [{ id: 'S1', bindingNodeID: 'B' }],
    signals: [{ id: 'X', bindingNodeID: 'A' }, { id: 'C-SIG', bindingNodeID: 'C' }],
    insulationJoints: [{ id: 'J1', bindingNodeID: 'B' }],
    cells: [{ id: 'CG1', linkIDList: 'AB' }, { id: 'CG2', linkIDList: 'BC' }],
})
const route = (patch = {}) => ({ id: 'R', type: 'arrival', description: 'Route',
    startNodeID: 'A', endNodeID: 'C', nodeList: 'A,B,C', linkList: 'AB,BC', ...patch })
const build = (document, selected = route(), options = { metersPerUnit: 1 }) => buildRouteProfile(document, selected, options)

test('actual node X overrides stored track endpoints and updates device and circuit chainage', () => {
    const result = build(layout())
    assert.deepEqual(result.errors, [])
    assert.equal(result.lengthM, 500)
    assert.deepEqual(result.segments.map(segment => [segment.linkId, segment.startM, segment.endM]), [
        ['AB', 0, 150.5], ['BC', 150.5, 500],
    ])
    assert.equal(result.joints[0].positionM, 150.5)
    assert.equal(result.devices.find(device => device.id === 'S1').positionM, 150.5)
    assert.equal(result.devices.find(device => device.id === 'C-SIG').positionM, 500)
    assert.deepEqual(result.cells.map(cell => [cell.startM, cell.endM]), [[0, 150.5], [150.5, 500]])
})

test('zero and negative actual X values are valid, and clearing returns to original geometry', () => {
    const document = layout()
    document.nodes[0].actualX = -50.25
    document.nodes[1].actualX = 0
    document.nodes[2].actualX = null
    let result = build(document)
    assert.deepEqual(result.errors, [])
    assert.deepEqual(result.segments.map(segment => segment.lengthM), [50.25, 300])
    assert.equal(result.lengthM, 350.25)
    document.nodes[0].actualX = null
    delete document.nodes[1].actualX
    result = build(document)
    assert.equal(result.lengthM, 300)
    assert.equal(result.joints[0].positionM, 100)
})

test('missing actual X retains endpoint geometry and the existing vertical distance', () => {
    const document = {
        nodes: [{ id: 'A', x: 10, y: 10 }, { id: 'B', x: 100, y: 10, actualX: 3 }],
        tracks: [{ id: 'AB', fromNodeID: 'A', toNodeID: 'B', x1: 0, y1: 0, x2: 100, y2: 4 }],
    }
    const selected = route({ endNodeID: 'B', nodeList: 'A,B', linkList: 'AB' })
    const result = build(document, selected)
    assert.deepEqual(result.errors, [])
    assert.equal(result.lengthM, 5, 'Only X is corrected; unchanged endpoint X and Y retain their existing sources')

    delete document.tracks[0].x1
    delete document.tracks[0].y1
    delete document.tracks[0].x2
    delete document.tracks[0].y2
    const fallback = build(document, selected)
    assert.deepEqual(fallback.errors, [])
    assert.equal(fallback.lengthM, 7, 'Missing endpoint geometry falls back to node positions with actual X applied')
})

test('reverse and continued routes use corrected lengths consistently', () => {
    const document = layout()
    const reverse = build(document, route({ startNodeID: 'C', endNodeID: 'A' }))
    assert.deepEqual(reverse.errors, [])
    assert.equal(reverse.lengthM, 500)
    assert.deepEqual(reverse.segments.map(segment => [segment.linkId, segment.lengthM]), [['BC', 349.5], ['AB', 150.5]])
    assert.equal(reverse.joints[0].positionM, 349.5)
    assert.equal(reverse.segments[1].gradientPermille, -4)

    const continued = build(document, [
        route({ id: 'arr', endNodeID: 'B', nodeList: 'A,B', linkList: 'AB' }),
        route({ id: 'dep', startNodeID: 'B', nodeList: 'B,C', linkList: 'BC' }),
    ])
    assert.deepEqual(continued.errors, [])
    assert.equal(continued.lengthM, 500)
    assert.equal(continued.joints.length, 1)
    assert.equal(continued.joints[0].positionM, 150.5)
})

test('actual X follows the configured distance scale and explicit measured lengths remain authoritative', () => {
    const document = layout()
    document.metadata = { coordinateUnit: 'mm', coordinateTransform: { applied: true, scale: .01 } }
    assert.equal(build(document, route(), {}).lengthM, 50)
    assert.equal(build(document, route(), { metersPerUnit: 3 }).lengthM, 1500)
    delete document.metadata
    assert.ok(build(document, route(), {}).errors.some(issue => issue.code === 'missingDistanceScale'))

    document.tracks[0].lengthM = 750
    document.tracks[1].lengthM = 1250
    const measured = build(document, route(), {})
    assert.deepEqual(measured.errors, [])
    assert.equal(measured.lengthM, 2000)
    assert.equal(measured.joints[0].positionM, 750)
})

test('profile calculation never rewrites drawing X, actual X, or track geometry', () => {
    const document = layout(), selected = route()
    const original = structuredClone({ document, selected })
    build(document, selected)
    build(document, route({ startNodeID: 'C', endNodeID: 'A' }))
    assert.deepEqual({ document, selected }, original)
})

test('batch and API profiles carry corrected chainage into the server calculation request', () => {
    const selected = route()
    const workspace = { catalog: createDefaultTrainPresetCatalog(), settings: {
        selectedRouteId: selected.id, continuationId: '', signal: defaultSignalParameters(), run: defaultRunOptions(),
        distanceMode: 'manual', metersPerUnit: 2, defaultLimit: 80,
    }, overrides: [], speedLimitPoints: [] }
    const context = { scope: { instanceID: 'I', stationSchemeID: 'S', calculationSetID: 'set' },
        document: layout(), routes: [selected], cases: [], seed: workspace }
    const { raw, profile } = tractionBatchProfile(context, workspace)
    assert.deepEqual(raw.errors, [])
    const request = buildTractionApiRequest(profile, defaultTrainParameters(), workspace.settings.signal, workspace.settings.run)
    assert.equal(request.profile.lengthM, 1000)
    assert.deepEqual(request.profile.segments.map(segment => [segment.startM, segment.endM]), [[0, 301], [301, 1000]])
    assert.equal(request.profile.joints[0].positionM, 301)
    assert.deepEqual(request.profile.cells.map(cell => [cell.startM, cell.endM]), [[0, 301], [301, 1000]])
    assert.equal(request.profile.devices.find(device => device.id === 'C-SIG').positionM, 1000)
})

const curvedLayout = () => ({
    nodes: [{ id: 'A', x: 0, y: 0 }, { id: 'B', x: 100, y: 0 }, { id: 'C', x: 100, y: 100 }],
    tracks: [
        { id: 'AB', fromNodeID: 'A', toNodeID: 'B', x1: 0, y1: 0, x2: 100, y2: 0 },
        { id: 'BC', fromNodeID: 'B', toNodeID: 'C', x1: 100, y1: 0, x2: 100, y2: 100 },
    ],
    curves: [{ id: 'CV1', nodeID: 'B', tangentLinkID1: 'AB', tangentLinkID2: 'BC', radius: 20,
        start: { x: 80, y: 0 }, end: { x: 100, y: 20 }, center: { x: 80, y: 20 }, largeArcFlag: 0, sweepFlag: 1 }],
})
const close = (actual, expected) => assert.ok(Math.abs(actual - expected) < 1e-9, `${actual} != ${expected}`)

test('corrected curve endpoints retain the stored radius and restore the original drawing geometry', () => {
    const document = curvedLayout()
    document.nodes[1].actualX = 200
    document.nodes[2].actualX = 200
    const original = structuredClone(document)
    for (const selected of [route(), route({ startNodeID: 'C', endNodeID: 'A' })]) {
        const result = build(document, selected)
        assert.deepEqual(result.errors, [])
        close(result.lengthM, 260 + 10 * Math.PI)
        const curve = result.devices.find(device => device.id === 'CV1')
        close(curve.endM - curve.positionM, 10 * Math.PI)
    }
    assert.deepEqual(document, original)
    delete document.nodes[1].actualX
    delete document.nodes[2].actualX
    close(build(document).lengthM, 160 + 10 * Math.PI)
})

test('a changed corner angle recalculates tangent trims and arc lengths in both directions', () => {
    const document = curvedLayout()
    document.nodes[1].actualX = 200
    const original = structuredClone(document)
    const trim = 20 / Math.tan(Math.PI / 8)
    const expectedArc = 20 * 3 * Math.PI / 4
    for (const selected of [route(), route({ startNodeID: 'C', endNodeID: 'A' })]) {
        const result = build(document, selected)
        assert.deepEqual(result.errors, [])
        close(result.lengthM, 200 + Math.sqrt(20000) - 2 * trim + expectedArc)
        const curve = result.devices.find(device => device.id === 'CV1')
        close(curve.endM - curve.positionM, expectedArc)
    }
    assert.deepEqual(document, original)
})

test('a corrected segment too short for its curve reports an error without inventing a smaller radius', () => {
    const document = curvedLayout()
    document.nodes[1].actualX = 10
    document.nodes[2].actualX = 10
    const original = structuredClone(document)
    const result = build(document)
    assert.ok(result.errors.some(issue => issue.code === 'invalidCurveGeometry' && issue.entityId === 'CV1'))
    assert.equal(result.lengthM, 0)
    assert.deepEqual(result.segments, [])
    assert.deepEqual(document, original)
})

test('corrected major curves preserve the complementary arc choice', () => {
    const document = curvedLayout()
    document.nodes[1].actualX = 200
    document.curves[0].largeArcFlag = 1
    const original = structuredClone(document)
    const trim = 20 / Math.tan(Math.PI / 8)
    const expectedArc = 20 * 5 * Math.PI / 4
    for (const selected of [route(), route({ startNodeID: 'C', endNodeID: 'A' })]) {
        const result = build(document, selected)
        assert.deepEqual(result.errors, [])
        close(result.lengthM, 200 + Math.sqrt(20000) - 2 * trim + expectedArc)
        const curve = result.devices.find(device => device.id === 'CV1')
        close(curve.endM - curve.positionM, expectedArc)
    }
    assert.deepEqual(document, original)
})
