import assert from 'node:assert/strict'
import test from 'node:test'
import { buildRouteProfile, parseRouteIds } from './routeProfile.ts'

const layout = () => ({
    nodes: [{ id: 'A', x: 0, y: 0 }, { id: 'B', x: 100, y: 0 }, { id: 'C', x: 300, y: 0 }],
    tracks: [
        { id: 'AB', fromNodeID: 'A', toNodeID: 'B', x1: 0, y1: 0, x2: 100, y2: 0, speedLimitKmh: 80, gradientPermille: 4 },
        { id: 'BC', fromNodeID: 'B', toNodeID: 'C', x1: 100, y1: 0, x2: 300, y2: 0 },
    ],
    switches: [{ id: 'S1', name: '1', bindingNodeID: 'B' }],
    signals: [{ id: 'X', name: 'X', bindingNodeID: 'A' }, { id: 'C-SIG', bindingNodeID: 'C' }],
    insulationJoints: [{ id: 'J1', bindingNodeID: 'B' }],
    cells: [{ id: 'CG1', name: '1G', linkIDList: 'AB' }, { id: 'CG2', name: '2G', linkIDList: 'BC' }],
})
const route = (values = {}) => ({ id: 'R', startNodeID: 'A', endNodeID: 'C', nodeList: '["A","B","C"]', linkList: 'AB, BC', ...values })
const build = (document = layout(), selected = route(), options = { metersPerUnit: 2 }) => buildRouteProfile(document, selected, options)
const codes = issues => issues.map(issue => issue.code)

test('ID parsing preserves persisted route formats', () => {
    for (const value of ['["A", "B", "C"]', 'A -> B -> C', 'A,B;C', ['A', 'B', 'C']]) {
        assert.deepEqual(parseRouteIds(value), ['A', 'B', 'C'])
    }
})

test('unfolds actual topology and equipment with calibrated distances', () => {
    const result = build()
    assert.deepEqual(result.errors, [])
    assert.equal(result.lengthM, 600)
    assert.deepEqual(result.segments.map(segment => [segment.linkId, segment.startM, segment.endM]), [['AB', 0, 200], ['BC', 200, 600]])
    assert.deepEqual(result.joints, [{ id: 'J1', label: 'J1', positionM: 200 }])
    assert.equal(result.devices.find(device => device.type === 'switch').positionM, 200)
    assert.deepEqual(result.cells.map(cell => [cell.label, cell.startM, cell.endM]), [['1G', 0, 200], ['2G', 200, 600]])
    assert.equal(result.segments[0].speedLimitKmh, 80)
    assert.equal(result.segments[1].speedLimitKmh, undefined)
    assert.ok(codes(result.warnings).includes('manualDistanceScale'))
    assert.ok(codes(result.warnings).includes('missingSpeedLimits'))
})

test('reverse routes reverse chainage, speed sections and gradient sign', () => {
    const result = build(layout(), route({ startNodeID: 'C', endNodeID: 'A' }))
    assert.deepEqual(result.errors, [])
    assert.equal(result.lengthM, 600)
    assert.deepEqual(result.segments.map(segment => segment.linkId), ['BC', 'AB'])
    assert.equal(result.joints[0].positionM, 400)
    assert.equal(result.segments[1].gradientPermille, -4)
    assert.equal(result.devices.find(device => device.id === 'X').positionM, 600)
})

test('drawing coordinates are never silently interpreted as metres', () => {
    const result = build(layout(), route(), {})
    assert.ok(codes(result.errors).includes('missingDistanceScale'))
    assert.equal(result.lengthM, 0)
    assert.equal(result.segments.length, 0)
    for (const metersPerUnit of [0, -2, NaN, Infinity]) {
        assert.ok(codes(build(layout(), route(), { metersPerUnit }).errors).includes('invalidDistanceScale'))
    }
})

test('explicit measured link lengths take precedence over drawing geometry', () => {
    const document = layout()
    document.tracks[0].lengthM = 750
    document.tracks[1].lengthM = 1250
    const result = build(document, route(), {})
    assert.deepEqual(result.errors, [])
    assert.equal(result.lengthM, 2000)
    assert.equal(result.joints[0].positionM, 750)
    assert.equal(build(document).lengthM, 2000)
})

test('known source units undo declared display transform; manual scale refers to current coordinates', () => {
    const document = layout()
    document.metadata = { coordinateUnit: 'mm', coordinateTransform: { applied: true, scale: .01 } }
    assert.equal(build(document, route(), {}).lengthM, 30)
    assert.equal(build(document, route(), { metersPerUnit: 3 }).lengthM, 900)
    document.metadata.coordinateTransform.scale = 0
    assert.ok(codes(build(document, route(), {}).errors).includes('invalidCoordinateTransform'))
})

test('joins arrival and departure paths without duplicating their shared boundary', () => {
    const selected = [
        route({ id: 'arrival', startNodeID: 'A', endNodeID: 'B', nodeList: 'A,B', linkList: 'AB' }),
        route({ id: 'departure', startNodeID: 'B', endNodeID: 'C', nodeList: 'B,C', linkList: 'BC' }),
    ]
    const result = build(layout(), selected)
    assert.deepEqual(result.errors, [])
    assert.deepEqual(result.routeIds, ['arrival', 'departure'])
    assert.equal(result.lengthM, 600)
    assert.equal(result.joints.length, 1)
    const discontinuous = structuredClone(selected)
    discontinuous[1].startNodeID = 'C'
    discontinuous[1].endNodeID = 'B'
    assert.ok(codes(build(layout(), discontinuous).errors).includes('disconnectedRoutes'))
})

test('missing, repeated or disconnected links cannot produce a partial route', () => {
    for (const [selected, code] of [
        [route({ linkList: 'AB,MISSING' }), 'missingRouteLink'],
        [route({ linkList: 'AB,AB,BC' }), 'duplicateRouteLink'],
        [route({ nodeList: '', linkList: 'BC' }), 'disconnectedRoutePath'],
        [route({ nodeList: 'A,C,B' }), 'invalidRouteNodeOrder'],
    ]) {
        const result = build(layout(), selected)
        assert.ok(codes(result.errors).includes(code))
        assert.equal(result.lengthM, 0)
        assert.deepEqual(result.segments, [])
    }
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

test('circular geometry replaces both tangents with the actual arc in both directions', () => {
    const document = curvedLayout()
    for (const selected of [route(), route({ startNodeID: 'C', endNodeID: 'A' })]) {
        const result = build(document, selected, { metersPerUnit: 1 })
        assert.deepEqual(result.errors, [])
        assert.ok(Math.abs(result.lengthM - (160 + 10 * Math.PI)) < 1e-9)
        const curve = result.devices.find(device => device.type === 'curve')
        assert.ok(Math.abs(curve.positionM - 80) < 1e-9)
        assert.ok(Math.abs(curve.endM - (80 + 10 * Math.PI)) < 1e-9)
    }
})

test('invalid curve geometry and measured length are explicit errors', () => {
    const document = curvedLayout()
    document.curves[0].radius = 40
    assert.ok(codes(build(document).errors).includes('invalidCurveGeometry'))
    const measured = layout()
    measured.tracks[0].lengthM = -1
    assert.ok(codes(build(measured).errors).includes('invalidMeasuredLength'))
})

test('missing insulation and circuit coverage remain missing, with actionable warnings', () => {
    const document = layout()
    delete document.insulationJoints
    delete document.cells
    const result = build(document)
    assert.deepEqual(result.joints, [])
    assert.deepEqual(result.cells, [])
    assert.ok(codes(result.warnings).includes('missingInsulationJoints'))
    assert.ok(codes(result.warnings).includes('missingCellCoverage'))
})

test('unbound devices do not snap onto nearby parallel tracks', () => {
    const document = layout()
    document.signals.push({ id: 'UNBOUND', position: { x: 100, y: 0 } })
    const result = build(document)
    assert.equal(result.devices.some(device => device.id === 'UNBOUND'), false)
    assert.ok(result.warnings.some(issue => issue.code === 'unlocatedDevice' && issue.entityId === 'UNBOUND'))
})

test('disjoint visits to the same circuit are separate intervals', () => {
    const document = layout()
    document.nodes.push({ id: 'D', x: 400, y: 0 })
    document.tracks.push({ id: 'CD', fromNodeID: 'C', toNodeID: 'D', x1: 300, y1: 0, x2: 400, y2: 0 })
    document.cells[0].linkIDList = 'AB,CD'
    const result = build(document, route({ endNodeID: 'D', nodeList: 'A,B,C,D', linkList: 'AB,BC,CD' }))
    assert.deepEqual(result.cells.filter(cell => cell.label === '1G').map(cell => [cell.startM, cell.endM]), [[0, 200], [600, 800]])
})

test('source document and route are never mutated', () => {
    const document = curvedLayout(), selected = route()
    const before = JSON.stringify({ document, selected })
    build(document, selected)
    assert.equal(JSON.stringify({ document, selected }), before)
})

test('topology orders shuffled links when node order is absent, but rejects ambiguity', () => {
    const result = build(layout(), route({ nodeList: '', linkList: 'BC,AB' }))
    assert.deepEqual(result.errors, [])
    assert.deepEqual(result.segments.map(segment => segment.linkId), ['AB', 'BC'])
    const document = layout()
    document.tracks.push({ id: 'AC', fromNodeID: 'A', toNodeID: 'C', lengthM: 100 })
    assert.ok(codes(build(document, route({ nodeList: '', linkList: 'AB,BC,AC' })).errors).includes('ambiguousRoutePath'))
})

test('curves on another turnout branch do not shorten the selected path', () => {
    const document = curvedLayout()
    document.curves[0].tangentLinkID2 = 'OTHER'
    const result = build(document, route(), { metersPerUnit: 1 })
    assert.equal(result.lengthM, 200)
    assert.equal(result.devices.length, 0)
})

test('measured path lengths are not shortened again by drawing fillets', () => {
    const document = curvedLayout()
    document.tracks[0].lengthM = 1000
    document.tracks[1].lengthM = 2000
    const result = build(document, route(), {})
    assert.deepEqual(result.errors, [])
    assert.equal(result.lengthM, 3000)
    assert.equal(result.devices[0].positionM, 1000)
    assert.equal(result.devices[0].endM, undefined)
    assert.ok(codes(result.warnings).includes('curveUsesMeasuredLinks'))
})

test('rejects immediate reversal when a continuation traverses the same link backwards', () => {
    const selected = [
        route({ id: 'arrival', startNodeID: 'A', endNodeID: 'B', nodeList: 'A,B', linkList: 'AB' }),
        route({ id: 'reverse', startNodeID: 'B', endNodeID: 'A', nodeList: 'B,A', linkList: 'AB' }),
    ]
    const result = build(layout(), selected)
    assert.ok(codes(result.errors).includes('reverseContinuation'))
    assert.equal(result.lengthM, 0)
    assert.deepEqual(result.segments, [])
})

test('a route ending inside a circuit does not invent a circuit exit at its endpoint', () => {
    const document = layout()
    document.cells = [{ id: 'WHOLE', linkIDList: 'AB,BC' }]
    document.insulationJoints = []
    const result = build(document, route({ endNodeID: 'B', nodeList: 'A,B', linkList: 'AB' }))
    assert.deepEqual(result.errors, [])
    assert.equal(result.cells[0].entryBoundaryKnown, true)
    assert.equal(result.cells[0].exitBoundaryKnown, false)
    assert.ok(codes(result.warnings).includes('partialCellCoverage'))
    assert.deepEqual(result.joints, [])
})

test('a route starting inside a circuit marks its entry unknown without hiding its real exit', () => {
    const document = layout()
    document.cells = [{ id: 'WHOLE', linkIDList: 'AB,BC' }]
    const result = build(document, route({ startNodeID: 'B', nodeList: 'B,C', linkList: 'BC' }))
    assert.deepEqual(result.errors, [])
    assert.equal(result.cells[0].entryBoundaryKnown, false)
    assert.equal(result.cells[0].exitBoundaryKnown, true)
})

test('partial circuit entry and exit flags reverse with travel direction', () => {
    const document = layout()
    document.cells = [{ id: 'WHOLE', linkIDList: 'AB,BC' }]
    const result = build(document, route({ startNodeID: 'B', endNodeID: 'A', nodeList: 'B,A', linkList: 'AB' }))
    assert.deepEqual(result.errors, [])
    assert.equal(result.cells[0].entryBoundaryKnown, false)
    assert.equal(result.cells[0].exitBoundaryKnown, true)
})

test('unused turnout branches inside the travelled circuit do not make its boundaries unknown', () => {
    const document = layout()
    document.nodes.push({ id: 'D', x: 200, y: 100 })
    document.tracks.push({ id: 'BD', fromNodeID: 'B', toNodeID: 'D', lengthM: 150 })
    document.cells = [{ id: 'WHOLE', linkIDList: 'AB,BC,BD' }]
    const result = build(document)
    assert.deepEqual(result.errors, [])
    assert.equal(result.cells[0].entryBoundaryKnown, true)
    assert.equal(result.cells[0].exitBoundaryKnown, true)
    assert.ok(!codes(result.warnings).includes('partialCellCoverage'))
})

test('a route covering only an interior link reports both circuit boundaries as unknown', () => {
    const document = layout()
    document.nodes.push({ id: 'D', x: 400, y: 0 })
    document.tracks.push({ id: 'CD', fromNodeID: 'C', toNodeID: 'D', lengthM: 100 })
    document.cells = [{ id: 'WHOLE', linkIDList: 'AB,BC,CD' }]
    const result = build(document, route({ startNodeID: 'B', endNodeID: 'C', nodeList: 'B,C', linkList: 'BC' }))
    assert.deepEqual(result.errors, [])
    assert.equal(result.cells[0].entryBoundaryKnown, false)
    assert.equal(result.cells[0].exitBoundaryKnown, false)
})
