import assert from 'node:assert/strict'
import test from 'node:test'
import { allowedRouteTrainTypes, changeRouteTrainTypes, inferRouteTrainTypes, readRouteTrainTypes } from './routeTrainTypes.ts'

test('arrival and departure aliases only offer trains; all other routes only offer rolling stock', () => {
    for (const type of ['Arrival', ' ARR ', '接车', '接车进路', 'Departure', 'dep', '发车', '发车进路']) {
        assert.deepEqual(allowedRouteTrainTypes(type), ['PassengerTrain', 'FreightTrain'])
    }
    for (const type of ['Shunting', 'Locomotive', 'Dwelling', '调车进路', '停留进路', 'custom', '']) {
        assert.deepEqual(allowedRouteTrainTypes(type), ['PassengerCar', 'FreightCar'])
    }
})

test('saved multiple selections round trip and explicit empty selections stay empty', () => {
    for (const selected of [[], ['PassengerTrain'], ['FreightTrain'], ['PassengerTrain', 'FreightTrain']]) {
        assert.deepEqual(readRouteTrainTypes(JSON.stringify(selected), 'Arrival'), selected)
    }
    assert.deepEqual(readRouteTrainTypes('["FreightCar","PassengerCar"]', 'Dwelling'), ['PassengerCar', 'FreightCar'])
    assert.deepEqual(readRouteTrainTypes(null, 'Departure'), ['PassengerTrain', 'FreightTrain'])
    assert.deepEqual(readRouteTrainTypes('["PassengerTrain","FreightCar","FreightCar"]', 'Shunting'), ['FreightCar'])
    assert.deepEqual(readRouteTrainTypes('{bad json}', 'Arrival'), [])
})

test('changing route categories resets incompatible choices, while same-category changes preserve edits', () => {
    assert.deepEqual(changeRouteTrainTypes(['FreightTrain'], 'Arrival', 'Departure'), ['FreightTrain'])
    assert.deepEqual(changeRouteTrainTypes([], 'Arrival', 'Departure'), [])
    assert.deepEqual(changeRouteTrainTypes(['FreightTrain'], 'Arrival', 'Dwelling'), ['PassengerCar', 'FreightCar'])
    assert.deepEqual(changeRouteTrainTypes(['PassengerCar'], 'Dwelling', 'Arrival'), ['PassengerTrain', 'FreightTrain'])
    assert.deepEqual(changeRouteTrainTypes(['PassengerCar'], 'Dwelling', 'Locomotive'), ['PassengerCar'])
    assert.deepEqual(changeRouteTrainTypes(['PassengerTrain'], 'Arrival', ''), ['PassengerCar', 'FreightCar'])
})

const track = (id, name, arrowType, fromNodeID, toNodeID) => ({ id, name, arrowType, fromNodeID, toNodeID })

test('arrivals inherit the types of named tracks on their own path, ignoring unnamed and unrelated tracks', () => {
    const tracks = [
        track('approach', '', 'F', 'entrance', 'a'),
        track('receiving', '1', 'P', 'a', 'signal'),
        track('other', '2', 'F', 'a', 'other-signal'),
    ]
    for (const type of ['Arrival', ' ARR ', '接车', '接车进路']) {
        assert.deepEqual(inferRouteTrainTypes(type, ['approach', 'receiving'], 'entrance', tracks), ['PassengerTrain'])
    }
    tracks[1].arrowType = ' f '
    assert.deepEqual(inferRouteTrainTypes('Arrival', ['approach', 'receiving'], 'entrance', tracks), ['FreightTrain'])
    tracks[1].arrowType = 'PF'
    assert.deepEqual(inferRouteTrainTypes('Arrival', ['approach', 'receiving'], 'entrance', tracks), ['PassengerTrain', 'FreightTrain'])
})

test('an arrival spanning several named tracks must be permitted on all of them', () => {
    const tracks = [track('one', '1', 'PF', 'a', 'b'), track('two', '2', 'F', 'b', 'c')]
    assert.deepEqual(inferRouteTrainTypes('Arrival', ['one', 'two'], 'a', tracks), ['FreightTrain'])
    tracks[0].arrowType = 'P'
    assert.deepEqual(inferRouteTrainTypes('Arrival', ['one', 'two'], 'a', tracks), [])
})

test('departures use the named track before the start node, rather than named tracks within or after the route', () => {
    const tracks = [
        track('berth', '1', 'F', 'opposite-signal', 'start'),
        track('first', '', '', 'start', 'a'),
        track('inside', '2', 'P', 'a', 'end'),
        track('beyond', '3', 'P', 'end', 'outside'),
    ]
    for (const type of ['Departure', ' dep ', '发车', '发车进路']) {
        assert.deepEqual(inferRouteTrainTypes(type, ['first', 'inside'], 'start', tracks), ['FreightTrain'])
    }
    tracks[0].fromNodeID = 'start'; tracks[0].toNodeID = 'opposite-signal'
    tracks[0].arrowType = 'P'
    assert.deepEqual(inferRouteTrainTypes('Departure', ['inside', 'first'], 'start', tracks), ['PassengerTrain'])
    tracks[0].arrowType = 'PF'
    assert.deepEqual(inferRouteTrainTypes('Departure', ['first', 'inside'], 'start', tracks), ['PassengerTrain', 'FreightTrain'])
    tracks[0].arrowType = 'OF'
    assert.deepEqual(inferRouteTrainTypes('Departure', ['first', 'inside'], 'start', tracks), ['FreightTrain'])
})

test('missing or ambiguous departure tracks cannot borrow a type from the route or a distant track', () => {
    const tracks = [track('route', 'route', 'P', 'start', 'end'), track('far', 'far', 'F', 'unrelated', 'other')]
    assert.equal(inferRouteTrainTypes('Departure', ['route'], 'start', tracks), null)
    tracks.push(track('one', '1', 'F', 'a', 'start'), track('two', '2', 'P', 'start', 'b'))
    assert.equal(inferRouteTrainTypes('Departure', ['route'], 'start', tracks), null)
    assert.equal(inferRouteTrainTypes('Departure', ['route'], '', tracks), null)
    assert.equal(inferRouteTrainTypes('Departure', ['missing'], 'start', tracks), null)
})

test('unclassified tracks and other route categories retain the caller’s defaults or manual choices', () => {
    for (const arrowType of ['', 'LO', 'LI', 'LIRO', 'LORI', 'unknown']) {
        assert.equal(inferRouteTrainTypes('Arrival', ['one'], 'a', [track('one', '1', arrowType, 'a', 'b')]), null)
    }
    const tracks = [track('one', '1', 'P', 'a', 'b')]
    assert.equal(inferRouteTrainTypes('Arrival', [], 'a', tracks), null)
    assert.equal(inferRouteTrainTypes('Arrival', ['missing'], 'a', tracks), null)
    for (const type of ['Shunting', 'Locomotive', 'Dwelling', 'custom', '']) {
        assert.equal(inferRouteTrainTypes(type, ['one'], 'a', tracks), null)
    }
})
