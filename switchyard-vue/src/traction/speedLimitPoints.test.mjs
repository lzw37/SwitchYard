import test from 'node:test'
import assert from 'node:assert/strict'
import { pointsFromSegments, limitAtPosition, applySpeedLimitPoints, setIntervalLimit, validateSpeedLimitPoints } from './speedLimitPoints.ts'
const segments = [
    { startM: 0, endM: 400, speedLimitKmh: 80, gradientPermille: 5, additionalResistanceKn: 2, id: 'A' },
    { startM: 400, endM: 800, speedLimitKmh: 45, gradientPermille: -3, additionalResistanceKn: 4, id: 'B' },
    { startM: 800, endM: 1400, speedLimitKmh: 80, gradientPermille: 0, additionalResistanceKn: 1, id: 'C' },
]
const points = [{ positionM: 0, speedLimitKmh: 90 }, { positionM: 250.125, speedLimitKmh: 35 }, { positionM: 700, speedLimitKmh: 60 }]
test('a point controls the following half-open interval through the route end', () => {
    for (const [position, speed] of [[0,90],[250.124,90],[250.125,35],[699.999,35],[700,60],[1400,60]]) assert.equal(limitAtPosition(points, position), speed)
})
test('new limit boundaries split physical sections and preserve gradient and resistance', () => {
    const split = applySpeedLimitPoints(segments, points)
    assert.deepEqual(split.map(s => [s.startM, s.endM, s.speedLimitKmh]), [[0,250.125,90],[250.125,400,35],[400,700,35],[700,800,60],[800,1400,60]])
    for (const s of split) {
        const physical = segments.find(p => p.id === s.id)
        assert.equal(s.gradientPermille, physical.gradientPermille)
        assert.equal(s.additionalResistanceKn, physical.additionalResistanceKn)
    }
    assert.equal(segments[0].speedLimitKmh,80)
})
test('deleting an anchor extends the preceding limit until the next point', () => {
    const remaining = points.filter((_,i) => i !== 1)
    assert.equal(limitAtPosition(remaining,600),90)
    assert.equal(limitAtPosition(remaining,700),60)
    assert.deepEqual(applySpeedLimitPoints(segments,remaining).map(s => s.speedLimitKmh), [90,90,60,60])
})
test('points coinciding with physical boundaries never create empty segments', () => {
    assert.deepEqual(applySpeedLimitPoints(segments,pointsFromSegments(segments)),segments)
    assert.deepEqual(pointsFromSegments([...segments,{...segments[2],startM:1400,endM:1600}]).map(p=>p.positionM),[0,400,800])
})
test('no saved anchors retains legacy limits, and points beyond the route do not extend it', () => {
    assert.deepEqual(applySpeedLimitPoints(segments,[]),segments)
    const split = applySpeedLimitPoints(segments,[...points,{positionM:1400,speedLimitKmh:10},{positionM:2000,speedLimitKmh:20}])
    assert.equal(split.at(-1).endM,1400)
    assert.equal(split.at(-1).speedLimitKmh,60)
})
test('editing a table interval changes that interval only, including exact existing boundaries', () => {
    const edited = setIntervalLimit(points,400,700,20,1400)
    assert.deepEqual(edited,[points[0],points[1],{positionM:400,speedLimitKmh:20},points[2]])
    assert.equal(limitAtPosition(edited,399),35)
    assert.equal(limitAtPosition(edited,400),20)
    assert.equal(limitAtPosition(edited,700),60)
    const last = setIntervalLimit(edited,700,1400,75,1400)
    assert.deepEqual(last.at(-1),{positionM:700,speedLimitKmh:75})
    assert.ok(validateSpeedLimitPoints(last))
})
test('validation requires a zero origin, sorted unique positions and positive finite speeds', () => {
    assert.ok(validateSpeedLimitPoints([])); assert.ok(validateSpeedLimitPoints(points))
    for (const invalid of [points.slice(1), [...points].reverse(), [points[0],points[0]], [{positionM:0,speedLimitKmh:0}], [{positionM:0,speedLimitKmh:Infinity}], [{positionM:NaN,speedLimitKmh:20}], [points[0],{positionM:10000001,speedLimitKmh:20}], Array.from({length:1001},(_,i)=>({positionM:i,speedLimitKmh:20}))]) assert.equal(validateSpeedLimitPoints(invalid),false)
})
