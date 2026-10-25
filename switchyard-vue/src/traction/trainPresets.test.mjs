import assert from 'node:assert/strict'
import test from 'node:test'
import { defaultTrainParameters } from './tractionCalculation.ts'
import {
    createDefaultTrainPresetCatalog as createCatalog, cloneTrainParameters, cloneTrainPresetCatalog,
    validateTrainPresetCatalog as validate, parseTrainPresetCatalog as parse,
} from './trainPresets.ts'

const onePreset = () => {
    const catalog = createCatalog()
    catalog.presets = [catalog.presets[0]]
    return catalog
}
const withParameter = (field, value) => {
    const catalog = onePreset()
    catalog.presets[0].parameters[field] = value
    return catalog
}

test('default catalog contains three explicitly labeled examples with stable IDs', () => {
    const catalog = createCatalog()
    assert.equal(validate(catalog), '')
    assert.equal(catalog.version, 1)
    assert.deepEqual(catalog.presets.map(preset => preset.id), [
        'example-short-passenger', 'example-long-passenger', 'example-freight',
    ])
    assert.equal(catalog.selectedId, catalog.presets[0].id)
    for (const preset of catalog.presets) {
        assert.match(preset.name, /示例/)
        assert.match(preset.nameEn, /example/i)
    }
    assert.deepEqual(catalog.presets[0].parameters, defaultTrainParameters())
    const [short, long, freight] = catalog.presets.map(preset => preset.parameters)
    assert.equal(long.lengthM, 400)
    assert.equal(long.massT, 880)
    assert.equal(long.maxSpeedKmh, 250)
    assert.equal(long.maxBrakeForceKn, short.maxBrakeForceKn * 2)
    for (const coefficient of ['resistanceA', 'resistanceB', 'resistanceC']) assert.equal(long[coefficient], short[coefficient] * 2)
    assert.ok(freight.massT > long.massT)
    assert.ok(freight.maxSpeedKmh < short.maxSpeedKmh)
})

test('each default factory call and every preset own independent curve objects', () => {
    const first = createCatalog(), second = createCatalog()
    first.presets[0].parameters.tractionCurve[0].forceKn = 1
    first.presets[1].parameters.tractionCurve.push({ speedKmh: 300, forceKn: 100 })
    first.presets[0].parameters.massT = 1
    assert.deepEqual(second, createCatalog())
    assert.equal(first.presets[1].parameters.tractionCurve[0].forceKn, 600)
    assert.equal(first.presets[2].parameters.tractionCurve[0].forceKn, 600)
})

test('parameter cloning isolates nested edits while preserving every model field', () => {
    const train = defaultTrainParameters(), copy = cloneTrainParameters(train)
    assert.deepEqual(copy, train)
    copy.tractionCurve[0].forceKn = 50
    copy.tractionCurve.splice(1, 1)
    copy.resistanceC = 0
    assert.deepEqual(train, defaultTrainParameters())
})

test('catalog clone isolates selection, names, parameters and even aliased input curves', () => {
    const source = createCatalog()
    source.presets[1].parameters = source.presets[0].parameters
    const copy = cloneTrainPresetCatalog(source)
    assert.deepEqual(copy, source)
    copy.selectedId = copy.presets[1].id
    copy.presets[0].name = 'Changed'
    copy.presets[0].parameters.tractionCurve[0].forceKn = 5
    assert.equal(source.selectedId, 'example-short-passenger')
    assert.equal(source.presets[0].name, '短编组客运（示例）')
    assert.equal(source.presets[0].parameters.tractionCurve[0].forceKn, 300)
    assert.equal(copy.presets[1].parameters.tractionCurve[0].forceKn, 300)
})

test('single remaining user preset is valid, including absent English translation', () => {
    const catalog = onePreset()
    catalog.presets[0].id = 'custom-1'
    catalog.presets[0].name = '自定义列车'
    delete catalog.presets[0].nameEn
    catalog.selectedId = 'custom-1'
    assert.equal(validate(catalog), '')
    assert.deepEqual(parse(JSON.stringify(catalog)), catalog)
})

test('catalog rejects missing structure, unsupported versions and zero or excessive presets', () => {
    for (const value of [null, undefined, false, 1, '', []]) assert.equal(validate(value), 'invalid-catalog')
    for (const version of [undefined, null, '1', 0, 2]) assert.equal(validate({ ...onePreset(), version }), 'invalid-version')
    for (const presets of [undefined, null, {}, [], Array(1001).fill(onePreset().presets[0])]) {
        assert.equal(validate({ ...onePreset(), presets }), 'invalid-preset-count')
    }
    const catalog = onePreset()
    catalog.presets = Array.from({ length: 1000 }, (_, index) => ({
        id: `id-${index}`, name: `预设 ${index}`, parameters: defaultTrainParameters(),
    }))
    catalog.selectedId = 'id-99'
    assert.equal(validate(catalog), '')
})

test('IDs must be nonempty, exact and unique; selection must point to an existing preset', () => {
    for (const id of [undefined, null, 1, '', ' ', ' padded ']) {
        const catalog = onePreset(); catalog.presets[0].id = id
        assert.equal(validate(catalog), 'invalid-preset-id')
    }
    const duplicate = createCatalog(); duplicate.presets[1].id = duplicate.presets[0].id
    assert.equal(validate(duplicate), 'duplicate-preset-id')
    for (const selectedId of [undefined, null, '', 'missing', ' example-short-passenger ']) {
        assert.equal(validate({ ...onePreset(), selectedId }), 'invalid-selected-preset')
    }
})

test('Chinese and optional English names require 1–80 characters and reject duplicate visible names', () => {
    for (const field of ['name', 'nameEn']) {
        for (const name of [null, 1, '', ' \t ', '字'.repeat(81)]) {
            const catalog = onePreset(); catalog.presets[0][field] = name
            assert.equal(validate(catalog), 'invalid-preset-name')
        }
        const catalog = onePreset(); catalog.presets[0][field] = '字'.repeat(80)
        assert.equal(validate(catalog), '')
        const duplicate = createCatalog()
        duplicate.presets[0][field] = 'Freight'
        duplicate.presets[1][field] = ' freight '
        assert.equal(validate(duplicate), 'duplicate-preset-name')
    }
    const unicode = onePreset(); unicode.presets[0].name = '🚆'.repeat(80)
    assert.equal(validate(unicode), '')
    unicode.presets[0].name += '🚆'
    assert.equal(validate(unicode), 'invalid-preset-name')
})

test('missing train objects and every missing required scalar are rejected', () => {
    for (const value of [null, undefined, [], 1, 'train']) {
        const catalog = onePreset(); catalog.presets[0].parameters = value
        assert.equal(validate(catalog), 'invalid-train-parameters')
    }
    for (const field of Object.keys(defaultTrainParameters()).filter(key => !['brakingCurve', 'resistanceCurve'].includes(key))) {
        const catalog = onePreset(); delete catalog.presets[0].parameters[field]
        assert.notEqual(validate(catalog), '', field)
    }
})

test('all scalar ranges match inclusive backend HTTP limits with strict numeric types', () => {
    const ranges = [
        ['lengthM', 0.001, 100000, 'invalid-train-length'],
        ['massT', 0.001, 10000000, 'invalid-train-mass'],
        ['rotatingMassFactor', 1, 3, 'invalid-rotating-mass-factor'],
        ['maxSpeedKmh', 0.001, 1000, 'invalid-train-max-speed'],
        ['maxBrakeForceKn', 0.001, 10000000, 'invalid-brake-force'],
        ...['resistanceA', 'resistanceB', 'resistanceC'].map(field => [field, 0, 10000000, 'invalid-resistance']),
    ]
    for (const [field, minimum, maximum, code] of ranges) {
        for (const valid of [minimum, maximum]) assert.equal(validate(withParameter(field, valid)), '', `${field}=${valid}`)
        for (const invalid of [minimum - .001, maximum + 1, NaN, Infinity, -Infinity, '1', null, undefined, true]) {
            assert.equal(validate(withParameter(field, invalid)), code, `${field}=${String(invalid)}`)
        }
    }
})

test('a single zero-speed point and zero traction are legal although a run may be infeasible', () => {
    assert.equal(validate(withParameter('tractionCurve', [{ speedKmh: 0, forceKn: 0 }])), '')
    assert.equal(validate(withParameter('tractionCurve', [{ speedKmh: 0, forceKn: 10000000 }, { speedKmh: 10000, forceKn: 0 }])), '')
})

test('traction curve requires 1–128 complete points and never sorts or repairs invalid input', () => {
    const maximumCurve = Array.from({ length: 128 }, (_, index) => ({ speedKmh: index, forceKn: 300 - index }))
    assert.equal(validate(withParameter('tractionCurve', maximumCurve)), '')
    const invalidCurves = [undefined, null, {}, [], [...maximumCurve, { speedKmh: 128, forceKn: 1 }],
        [null], [undefined], [{}], [{ speedKmh: 0 }], [{ forceKn: 5 }],
        [{ speedKmh: 1, forceKn: 10 }],
        [{ speedKmh: 0, forceKn: 10 }, { speedKmh: 0, forceKn: 5 }],
        [{ speedKmh: 0, forceKn: 10 }, { speedKmh: 20, forceKn: 5 }, { speedKmh: 10, forceKn: 2 }],
        [{ speedKmh: 0, forceKn: 10 }, { speedKmh: 20, forceKn: 11 }],
    ]
    for (const curve of invalidCurves) assert.equal(validate(withParameter('tractionCurve', curve)), 'invalid-traction-curve')
})

test('traction point values reject nonfinite, coerced, negative and out-of-range values', () => {
    for (const field of ['speedKmh', 'forceKn']) {
        const invalidValues = [NaN, Infinity, -Infinity, -1, '0', undefined, null, true,
            field === 'speedKmh' ? 10001 : 10000001]
        for (const value of invalidValues) {
            const point = { speedKmh: 0, forceKn: 300 }; point[field] = value
            assert.equal(validate(withParameter('tractionCurve', [point])), 'invalid-traction-curve')
        }
    }
})

test('JSON persistence preserves a user-selected edited catalog without sharing later edits', () => {
    const original = createCatalog()
    original.selectedId = original.presets[2].id
    original.presets[2].name = '用户保存的货运方案'
    original.presets[2].parameters.lengthM = 900
    const raw = JSON.stringify(original), restored = parse(raw)
    assert.deepEqual(restored, original)
    restored.presets[2].parameters.tractionCurve[0].forceKn = 500
    restored.presets[2].name = 'Later draft'
    assert.deepEqual(parse(raw), original)
})

test('parser rejects malformed, partial, incompatible and invalid stored data without fallback', () => {
    for (const raw of [null, undefined, '', ' ', 'null', '[]', '{}', '{', 'false', '1', '"text"']) assert.equal(parse(raw), null)
    const invalidCatalogs = [
        { ...onePreset(), version: 2 }, { ...onePreset(), selectedId: 'deleted' },
        withParameter('massT', -1), withParameter('maxBrakeForceKn', null),
    ]
    for (const catalog of invalidCatalogs) assert.equal(parse(JSON.stringify(catalog)), null)
})

test('validation and cloning leave input unchanged and omit unrelated persisted properties', () => {
    const original = createCatalog()
    const before = JSON.stringify(original)
    assert.equal(validate(original), '')
    cloneTrainPresetCatalog(original)
    assert.equal(JSON.stringify(original), before)
    const contaminated = JSON.parse(before)
    contaminated.extra = 'catalog metadata'
    contaminated.presets[0].extra = 'preset metadata'
    contaminated.presets[0].parameters.extra = 'unknown train field'
    contaminated.presets[0].parameters.tractionCurve[0].extra = 'unknown point field'
    Object.defineProperty(contaminated.presets[0].parameters, '__proto__', { value: { polluted: true }, enumerable: true })
    const clean = parse(JSON.stringify(contaminated))
    assert.deepEqual(clean, original)
    assert.equal({}.polluted, undefined)
})

test('braking and resistance curves preserve rising and falling force, zero values and isolated edits', () => {
    const catalog = onePreset(), train = catalog.presets[0].parameters
    for (const key of ['brakingCurve', 'resistanceCurve']) train[key] = [{ speedKmh: 0, forceKn: 0 }, { speedKmh: 10, forceKn: 20 }, { speedKmh: 20, forceKn: 5 }]
    assert.equal(validate(catalog), '')
    const copy = cloneTrainPresetCatalog(catalog)
    copy.presets[0].parameters.brakingCurve[1].forceKn = 99
    copy.presets[0].parameters.resistanceCurve[1].speedKmh = 12
    assert.equal(train.brakingCurve[1].forceKn, 20)
    assert.equal(train.resistanceCurve[1].speedKmh, 10)
    for (const key of ['brakingCurve', 'resistanceCurve']) {
        for (const invalid of [[], null, [{ speedKmh: 1, forceKn: 1 }], [{ speedKmh: 0, forceKn: -1 }], [{ speedKmh: 0, forceKn: 1 }, { speedKmh: 0, forceKn: 2 }]])
            assert.notEqual(validate(withParameter(key, invalid)), '')
    }
})
test('legacy generators materialize once and never replace an existing discrete table', () => {
    const catalog = onePreset(), train = catalog.presets[0].parameters
    delete train.brakingCurve; delete train.resistanceCurve
    train.maxSpeedKmh = 25; train.maxBrakeForceKn = 123
    train.resistanceA = 1; train.resistanceB = 2; train.resistanceC = .1
    const upgraded = parse(JSON.stringify(catalog)).presets[0].parameters
    assert.deepEqual(upgraded.brakingCurve.map(p => p.speedKmh), [0, 10, 20, 25])
    assert.deepEqual(upgraded.brakingCurve.map(p => p.forceKn), [123,123,123,123])
    assert.deepEqual(upgraded.resistanceCurve.map(p => p.forceKn), [1,31,81,113.5])
    upgraded.maxBrakeForceKn = 900; upgraded.resistanceA = 200
    const clone = cloneTrainParameters(upgraded)
    assert.deepEqual(clone.brakingCurve, upgraded.brakingCurve)
    assert.deepEqual(clone.resistanceCurve, upgraded.resistanceCurve)
})
