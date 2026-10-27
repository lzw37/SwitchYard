import assert from 'node:assert/strict'
import test from 'node:test'
import { createDemoCatalog, createExampleTemplate, validateTemplate } from './operationProcess.ts'

function fixture() {
    const catalog = createDemoCatalog()
    const source = createExampleTemplate({ instanceID: 's', stationSchemeID: 'p' }, catalog)
    const precedence = source.precedences[1]
    const leading = source.events.find(event => event.id === precedence.leadingEvent)
    const following = source.events.find(event => event.id === precedence.followingEvent)
    precedence.interval = following.time - leading.time
    return { catalog, source, precedence }
}

test('旧次序与最小间隔允许更长时差，严格次序同时检查上下界', () => {
    const { source, catalog, precedence } = fixture()
    const exactInterval = precedence.interval
    assert.deepEqual(validateTemplate(source, catalog), [])
    precedence.interval = exactInterval - 0.125
    assert.deepEqual(validateTemplate(source, catalog), [])
    precedence.isExact = true
    assert.ok(validateTemplate(source, catalog).some(error => error.includes('严格等于')))
    precedence.interval = exactInterval
    assert.deepEqual(validateTemplate(source, catalog), [])
    precedence.interval = exactInterval + 0.125
    assert.ok(validateTemplate(source, catalog).some(error => error.includes('严格等于')))
    precedence.isExact = false
    assert.ok(validateTemplate(source, catalog).some(error => error.includes('最小间隔')))
})

test('严格零间隔可连接同时发生的事件，未定时的草稿不被当成时刻冲突', () => {
    const { source, catalog } = fixture()
    const precedence = source.precedences[0]
    precedence.isExact = true
    precedence.interval = 0
    assert.deepEqual(validateTemplate(source, catalog), [])
    precedence.interval = 0.01
    assert.ok(validateTemplate(source, catalog).some(error => error.includes('严格等于')))
    source.events.find(event => event.id === precedence.followingEvent).time = null
    assert.deepEqual(validateTemplate(source, catalog), [])
})
