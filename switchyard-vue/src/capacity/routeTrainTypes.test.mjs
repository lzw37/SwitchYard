import assert from 'node:assert/strict'
import test from 'node:test'
import { allowedRouteTrainTypes, changeRouteTrainTypes, readRouteTrainTypes } from './routeTrainTypes.ts'

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
