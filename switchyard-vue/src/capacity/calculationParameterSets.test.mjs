import assert from 'node:assert/strict'
import test from 'node:test'
import axios from 'axios'
import { effectScope, nextTick, ref } from 'vue'
import { normalizeCalculationParameterSet, useCalculationParameterSets } from './calculationParameterSets.ts'
import { buildProcessPlanBatch, createProcessPlanSources } from './processPlanBatch.ts'

test('process generation carries an independent parameter set choice for each source', () => {
    const scope = { instanceID: 'i', stationSchemeID: 's', operationPlanID: 'p' }
    const sources = [
        { ...scope, id: 'one', revision: 1, trainCount: 2, calculationParameterSetID: 'weekday' },
        { ...scope, id: 'two', revision: 3, trainCount: 1, calculationParameterSetID: 'weekend' },
    ]
    const refreshed = createProcessPlanSources(sources.map(source => ({ ...source, revision: source.revision + 1 })), sources, scope)
    assert.deepEqual(buildProcessPlanBatch(refreshed), [
        { processTemplateID: 'one', revision: 2, trainCount: 2, calculationParameterSetID: 'weekday' },
        { processTemplateID: 'two', revision: 4, trainCount: 1, calculationParameterSetID: 'weekend' },
    ])
    sources[0].calculationParameterSetID = ''
    assert.equal('calculationParameterSetID' in buildProcessPlanBatch(sources)[0], false)
})

test('parameter metadata supports API casing and keeps the default flag', () => {
    assert.deepEqual(normalizeCalculationParameterSet({ ID: 'a', Name: '参数 A', IsDefault: true }), { id: 'a', name: '参数 A', isDefault: true })
    assert.deepEqual(normalizeCalculationParameterSet({ id: 'b', name: '参数 B', isDefault: false }), { id: 'b', name: '参数 B', isDefault: false })
})

test('late parameter lists cannot replace the newly selected station scheme', async context => {
    const requests = []
    context.mock.method(axios, 'get', (_url, config) => new Promise(resolve => requests.push({ config, resolve })))
    const scope = effectScope()
    const scheme = ref('first')
    const state = scope.run(() => useCalculationParameterSets(() => 'i', () => scheme.value))
    scheme.value = 'second'
    await nextTick()
    requests[1].resolve({ data: [{ id: 'b', name: 'B', isDefault: true }] })
    await new Promise(resolve => setImmediate(resolve))
    requests[0].resolve({ data: [{ id: 'a', name: 'A', isDefault: true }] })
    await new Promise(resolve => setImmediate(resolve))
    assert.equal(state.sets.value[0].id, 'b')
    assert.equal(state.loading.value, false)
    scope.stop()
})
