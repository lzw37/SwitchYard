import assert from 'node:assert/strict'
import test from 'node:test'
import { buildPlanarTrainScene, interpolatePlanarTrainCar, samplePlanarTrainScene } from './simulationTrainMotion.ts'
import { projectTrainCarToViewport } from './simulationViewport.ts'

const viewport = { minX: 0, minY: 0, scaleX: 1, scaleY: 1 }
function path(points) {
    let totalLength = 0
    const segments = points.slice(1).map((to, index) => {
        const from = points[index]
        const length = Math.hypot(to.x - from.x, to.y - from.y)
        const segment = { from, to, length, startDistance: totalLength,
            angle: Math.atan2(to.y - from.y, to.x - from.x) * 180 / Math.PI }
        totalLength += length
        return segment
    })
    return { points, segments, totalLength }
}
const line = (from, to, y = 0) => path([{ x: from, y }, { x: to, y }])
const run = (key, type, routePath, startSeconds, endSeconds, trainID = 'G101') => ({
    key, train: { id: trainID }, route: { type }, path: routePath, startSeconds, endSeconds,
    lockSeconds: type === 'Dw' ? 0 : 2,
})
function journey() {
    return [run('arrival', 'Arr', line(0, 400), 0, 120),
        run('stop', 'Dw', line(400, 600), 120, 240),
        run('departure', 'Dep', line(600, 1000), 235, 360)]
}
function near(actual, expected, tolerance = 0.00001) {
    assert.ok(Math.abs(actual - expected) < tolerance, `${actual} != ${expected}`)
}
function samePoses(first, second, tolerance = 0.00001) {
    assert.equal(first.length, second.length)
    first.forEach((car, index) => {
        assert.equal(car.key, second[index].key)
        near(car.x, second[index].x, tolerance)
        near(car.y, second[index].y, tolerance)
        near(Math.cos(car.angle * Math.PI / 180), Math.cos(second[index].angle * Math.PI / 180), tolerance)
        near(Math.sin(car.angle * Math.PI / 180), Math.sin(second[index].angle * Math.PI / 180), tolerance)
    })
}

test('one rigid formation and stable carriage identities survive arrival, short stops and departure', () => {
    const runs = journey()
    runs.push(run('shorter-stop', 'Dw', line(1000, 1100), 360, 400))
    const scene = buildPlanarTrainScene(runs, viewport)
    const baseline = samplePlanarTrainScene(scene, 0, true)
    for (const seconds of [60, 119.999, 120, 200, 239.999, 240, 300, 360, 399, 400]) {
        const cars = samplePlanarTrainScene(scene, seconds, true)
        assert.equal(cars.length, 8)
        cars.forEach((car, index) => {
            assert.equal(car.key, baseline[index].key)
            assert.equal(car.length, baseline[index].length)
            assert.equal(car.width, baseline[index].width)
            near(car.length / car.width, 34 / 14)
        })
    }
    const cars = samplePlanarTrainScene(scene, 380, true)
    near(cars[0].x - cars[7].x + cars[0].length, 75)
})

test('arrival, dwelling and departure meet at the same position without jumping', () => {
    const scene = buildPlanarTrainScene(journey(), viewport)
    for (const boundary of [120, 240]) {
        samePoses(samplePlanarTrainScene(scene, boundary - 0.0001, true),
            samplePlanarTrainScene(scene, boundary + 0.0001, true))
    }
    const parked = samplePlanarTrainScene(scene, 180, true)
    near((parked[0].x + parked[7].x) / 2, 500)
    assert.ok(parked[0].x + parked[0].length / 2 < 600)
    assert.ok(parked[7].x - parked[7].length / 2 > 400)
})

test('entry and exit extend the terminal track instead of stacking cars at an endpoint', () => {
    const scene = buildPlanarTrainScene([run('through', 'Pass', line(0, 1000), 0, 100)], viewport)
    for (const seconds of [0, 1, 25, 50, 99, 100]) {
        const cars = samplePlanarTrainScene(scene, seconds, true)
        for (let index = 1; index < cars.length; index++) near(cars[index - 1].x - cars[index].x, 38)
        near(cars[0].x - cars[7].x + cars[0].length, 300)
    }
})

test('anisotropic viewport scaling cannot change body dimensions or spacing as the train turns', () => {
    const view = { minX: -15, minY: 20, scaleX: 0.4, scaleY: 2.5 }
    const nominal = { x: 40, y: 60, length: 34, width: 14 }
    const first = projectTrainCarToViewport({ ...nominal, angle: 0 }, view)
    for (const angle of [0, 30, 45, 90, 135, 180, 270, 359, 361]) {
        const projected = projectTrainCarToViewport({ ...nominal, angle }, view)
        near(projected.length, first.length)
        near(projected.width, first.width)
        near(projected.x, 22)
        near(projected.y, 100)
        const radians = angle * Math.PI / 180
        const scene = buildPlanarTrainScene([run('track', 'Pass',
            path([{ x: 0, y: 0 }, { x: 2000 * Math.cos(radians), y: 2000 * Math.sin(radians) }]), 0, 100)], view)
        const cars = samplePlanarTrainScene(scene, 50, true)
        for (let index = 1; index < cars.length; index++) {
            near(Math.hypot(cars[index].x - cars[index - 1].x, cars[index].y - cars[index - 1].y), 38 * 0.4)
        }
        near(cars[0].length, first.length)
    }
})

test('bends keep dimensions fixed and motion continuous under unequal X/Y scaling', () => {
    const scene = buildPlanarTrainScene([run('bend', 'Pass', path([
        { x: 0, y: 0 }, { x: 1000, y: 0 }, { x: 1600, y: 300 }, { x: 1600, y: 1300 },
    ]), 0, 120)], { ...viewport, scaleX: 0.5, scaleY: 2 })
    let previous = samplePlanarTrainScene(scene, 0, true)
    for (let seconds = 0.1; seconds <= 120; seconds += 0.1) {
        const cars = samplePlanarTrainScene(scene, seconds, true)
        cars.forEach((car, index) => {
            assert.equal(car.length, previous[index].length)
            assert.equal(car.width, previous[index].width)
            assert.ok(Math.hypot(car.x - previous[index].x, car.y - previous[index].y) < 6)
        })
        previous = cars
    }
})

test('reversed route descriptions preserve physical carriage order at both stop boundaries', () => {
    const runs = journey()
    runs[1].path = line(600, 400)
    const scene = buildPlanarTrainScene(runs, viewport)
    for (const boundary of [120, 240]) {
        samePoses(samplePlanarTrainScene(scene, boundary - 0.0001, true),
            samplePlanarTrainScene(scene, boundary + 0.0001, true))
    }
})

test('all-train playback retains a train through gaps and never duplicates overlapping operations', () => {
    const runs = journey()
    runs[1].startSeconds = 130
    runs[2].startSeconds = 235
    runs.push(...journey().map(item => ({ ...item, key: `other-${item.key}`, train: { id: 'G102' },
        path: path(item.path.points.map(point => ({ ...point, y: point.y + 100 }))) })))
    const scene = buildPlanarTrainScene(runs, viewport)
    for (const seconds of [0, 119, 120, 125, 130, 236, 239, 240, 359]) {
        const cars = samplePlanarTrainScene(scene, seconds, false)
        assert.equal(cars.length, 16)
        assert.equal(new Set(cars.map(car => car.key)).size, 16)
        for (const trainID of ['G101', 'G102']) assert.equal(cars.filter(car => car.trainID === trainID).length, 8)
    }
    assert.equal(samplePlanarTrainScene(scene, -1, false).length, 0)
    assert.equal(samplePlanarTrainScene(scene, 360, false).length, 0)
})

test('seeking and replaying sample the same pose without accumulated animation state', () => {
    const scene = buildPlanarTrainScene(journey(), viewport)
    const expected = samplePlanarTrainScene(scene, 60, true)
    for (const seconds of [300, 0, 240, 180, 119, 60]) samplePlanarTrainScene(scene, seconds, true)
    assert.deepEqual(samplePlanarTrainScene(scene, 60, true), expected)
    assert.deepEqual(samplePlanarTrainScene(buildPlanarTrainScene([], viewport), 0, true), [])
})

test('overlay transitions move through intermediate positions and resume from the displayed pose', () => {
    const source = { x: 0, y: 20, angle: 350, length: 34, width: 14 }
    const target = { ...source, x: 100, y: 60, angle: 10 }
    const middle = interpolatePlanarTrainCar(source, target, 0.5)
    near(middle.x, 50)
    near(middle.y, 40)
    near(middle.angle, 360)
    assert.equal(middle.length, 34)
    assert.equal(middle.width, 14)
    const retargeted = { ...target, x: 200, y: 100 }
    assert.deepEqual(interpolatePlanarTrainCar(middle, retargeted, 0), middle)
    const next = interpolatePlanarTrainCar(middle, retargeted, 0.5)
    near(next.x, 125)
    near(next.y, 70)
    near(next.angle, 365)
    near(interpolatePlanarTrainCar(middle, retargeted, 1).x, 200)
})
