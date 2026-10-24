import assert from 'node:assert/strict'
import test from 'node:test'
import { createPinia, setActivePinia } from 'pinia'
import { computed, watch } from 'vue'
import { useCapacityStore } from './capacity.ts'

test('the first parameter-set selection immediately updates synchronous computed readers', () => {
    setActivePinia(createPinia())
    const store = useCapacityStore()
    const selected = computed(() => store.calculationParameterSetId('station', 'scheme'))
    const changes = []
    const stop = watch(selected, value => changes.push(value), { flush: 'sync' })
    assert.equal(selected.value, '')
    store.selectCalculationParameterSet('station', 'scheme', 'default')
    assert.equal(selected.value, 'default')
    assert.deepEqual(changes, ['default'])
    store.selectCalculationParameterSet('station', 'another', 'other')
    store.selectCalculationParameterSet('station', 'scheme', 'edited')
    store.selectCalculationParameterSet('station', 'scheme', '')
    assert.equal(selected.value, 'edited')
    assert.equal(store.calculationParameterSetId('station', 'another'), 'other')
    stop()
})
