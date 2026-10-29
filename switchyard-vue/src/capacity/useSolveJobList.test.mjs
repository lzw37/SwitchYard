import assert from 'node:assert/strict'
import test from 'node:test'
import { effectScope, nextTick, ref } from 'vue'
import { useSolveJobList } from './useSolveJobList.ts'

const flush = async () => { await Promise.resolve(); await nextTick() }
const job = (instanceId, jobId) => ({ instanceId, jobId, status: 'completed' })

function fixture(initialInstance = '') {
    const instance = ref(initialInstance)
    const autoSelect = ref(true)
    const requests = []
    const selections = []
    const scope = effectScope()
    const list = scope.run(() => useSolveJobList({
        instanceId: () => instance.value,
        selectedJobId: () => '',
        autoSelect: () => autoSelect.value,
        loadPage: query => new Promise((resolve, reject) => requests.push({ query, resolve, reject })),
        onSelect: row => selections.push(row.jobId),
        loadErrorMessage: () => 'load failed',
    }))
    return { instance, autoSelect, requests, selections, list, stop: () => scope.stop() }
}

test('no selected instance makes no unscoped request', async t => {
    const f = fixture()
    t.after(f.stop)
    await f.list.refresh(false)
    assert.equal(f.requests.length, 0)
    f.instance.value = 'A'
    assert.deepEqual(f.requests[0].query, { instanceId: 'A', status: '', offset: 0, limit: 10 })
    f.requests[0].resolve({ items: [job('A', 'a1')], total: 1 })
    await flush()
    assert.equal(f.list.jobs.value[0].jobId, 'a1')
    assert.deepEqual(f.selections, ['a1'])
})

test('switching instance clears rows immediately and ignores the previous response', async t => {
    const f = fixture('A')
    t.after(f.stop)
    f.requests[0].resolve({ items: [job('A', 'a1')], total: 12 })
    await flush()
    const pendingRefresh = f.list.refresh(false)
    f.instance.value = 'B'
    assert.deepEqual(f.list.jobs.value, [])
    assert.equal(f.list.total.value, 0)
    assert.equal(f.requests[2].query.instanceId, 'B')
    f.requests[2].resolve({ items: [job('B', 'b1')], total: 1 })
    await flush()
    f.requests[1].resolve({ items: [job('A', 'a2')], total: 50 })
    await pendingRefresh
    assert.equal(f.list.jobs.value[0].jobId, 'b1')
    assert.equal(f.list.total.value, 1)
    assert.deepEqual(f.selections, ['a1', 'b1'])
})

test('rapid A to B to A navigation ignores old responses and errors', async t => {
    const f = fixture('A')
    t.after(f.stop)
    f.instance.value = 'B'
    f.instance.value = 'A'
    f.requests[2].resolve({ items: [job('A', 'new-a')], total: 1 })
    await flush()
    f.requests[0].reject(new Error('stale A failure'))
    f.requests[1].resolve({ items: [job('B', 'b1')], total: 2 })
    await flush()
    assert.deepEqual(f.selections, ['new-a'])
    assert.equal(f.list.jobs.value[0].jobId, 'new-a')
    assert.equal(f.list.loadError.value, '')
    assert.equal(f.list.loading.value, false)
})

test('pagination and status filters stay inside the instance and navigation resets the page', async t => {
    const f = fixture('A')
    t.after(f.stop)
    f.requests[0].resolve({ items: [job('A', 'a1')], total: 30 })
    await flush()
    f.list.page.value = 2
    await flush()
    assert.equal(f.requests[1].query.offset, 10)
    assert.equal(f.requests[1].query.instanceId, 'A')
    f.list.status.value = 'failed'
    await flush()
    assert.deepEqual(f.requests[2].query, { instanceId: 'A', status: 'failed', offset: 0, limit: 10 })
    f.requests[2].resolve({ items: [], total: 0 })
    await flush()
    f.instance.value = 'B'
    assert.equal(f.list.page.value, 1)
    assert.deepEqual(f.requests[3].query, { instanceId: 'B', status: 'failed', offset: 0, limit: 10 })
    f.requests[1].resolve({ items: [job('A', 'old-page')], total: 30 })
    f.requests[3].resolve({ items: [], total: 0 })
    await flush()
    assert.deepEqual(f.list.jobs.value, [])
    assert.equal(f.list.total.value, 0)
})

test('clearing the instance invalidates pending requests and hides prior errors', async t => {
    const f = fixture('A')
    t.after(f.stop)
    f.requests[0].reject(new Error('current failure'))
    await flush()
    assert.equal(f.list.loadError.value, 'load failed')
    const pending = f.list.refresh(false)
    f.instance.value = ''
    assert.equal(f.list.loadError.value, '')
    assert.equal(f.list.loading.value, false)
    f.requests[1].resolve({ items: [job('A', 'late')], total: 1 })
    await pending
    await f.list.refresh(false)
    assert.equal(f.requests.length, 2)
    assert.deepEqual(f.list.jobs.value, [])
    assert.equal(f.list.total.value, 0)
})

test('unmounting the list prevents late responses from selecting tasks', async () => {
    const f = fixture('A')
    f.stop()
    f.requests[0].resolve({ items: [job('A', 'late')], total: 1 })
    await flush()
    assert.deepEqual(f.selections, [])
    assert.deepEqual(f.list.jobs.value, [])
})

test('new-task drafts are not replaced by automatic task selection', async t => {
    const f = fixture('A')
    t.after(f.stop)
    f.autoSelect.value = false
    f.requests[0].resolve({ items: [job('A', 'existing')], total: 1 })
    await flush()
    assert.deepEqual(f.selections, [])
    assert.equal(f.list.jobs.value.length, 1)
    f.autoSelect.value = true
    const refresh = f.list.refresh(false)
    f.requests[1].resolve({ items: [job('A', 'existing')], total: 1 })
    await refresh
    assert.deepEqual(f.selections, ['existing'])
})
