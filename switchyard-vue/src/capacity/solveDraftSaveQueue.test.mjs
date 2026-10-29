import assert from 'node:assert/strict'
import test from 'node:test'
import fs from 'node:fs'
import ts from 'typescript'
const source = fs.readFileSync(new URL('./solveDraftSaveQueue.ts', import.meta.url), 'utf8')
const compiled = ts.transpileModule(source, { compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.ES2022 } }).outputText
const { SolveDraftSaveQueue } = await import('data:text/javascript;base64,' + Buffer.from(compiled).toString('base64'))

const value = (name, instanceId = 'A') => ({ instanceId, name, stationSchemeId: 'scheme', operationPlanId: 'plan', agentId: 'agent', modelId: 'second-solver', input: { instanceId, name } })
const tick = async () => { await Promise.resolve(); await Promise.resolve() }
function fixture() {
    const writes = [], saved = []
    let active = 0, maximum = 0
    const queue = new SolveDraftSaveQueue((id, snapshot, revision) => {
        active++; maximum = Math.max(maximum, active)
        return new Promise((resolve, reject) => writes.push({ id, snapshot, revision,
            resolve: nextRevision => { active--; resolve({ jobId: id, name: snapshot.name, revision: nextRevision, status: 'draft' }) },
            reject: error => { active--; reject(error) },
        }))
    }, () => {}, (id, job) => saved.push({ id, job }))
    return { queue, writes, saved, maximum: () => maximum }
}

test('edits during a save are serialized using the returned revision', async () => {
    const f = fixture()
    f.queue.schedule('snowflake-id', value('first'), 1, 60000)
    const saving = f.queue.flush('snowflake-id')
    assert.equal(f.writes.length, 1)
    f.queue.schedule('snowflake-id', value('second'), 1, 60000)
    f.writes[0].resolve(2)
    await tick()
    assert.equal(f.writes.length, 2)
    assert.equal(f.writes[1].revision, 2)
    assert.equal(f.writes[1].snapshot.name, 'second')
    f.writes[1].resolve(3)
    await saving
    assert.equal(f.maximum(), 1)
    assert.equal(f.queue.state('snowflake-id'), 'saved')
    assert.equal(f.queue.revision('snowflake-id', 1), 3)
    f.queue.forget('snowflake-id')
})

test('flush waits for the last edit before starting or leaving a task', async () => {
    const f = fixture()
    f.queue.schedule('draft', value('before'), 4, 60000)
    const first = f.queue.flush('draft')
    f.queue.schedule('draft', value('latest'), 4, 60000)
    const leaving = f.queue.flush('draft')
    let completed = false
    leaving.then(() => { completed = true })
    f.writes[0].resolve(5)
    await tick()
    assert.equal(completed, false)
    f.writes[1].resolve(6)
    await Promise.all([first, leaving])
    assert.equal(completed, true)
    assert.equal(f.saved.at(-1).job.name, 'latest')
    f.queue.forget('draft')
})

test('failed saves stay dirty, block flush and can be retried', async () => {
    const f = fixture()
    f.queue.schedule('draft', value('unsaved'), 1, 60000)
    const failed = f.queue.flush('draft')
    f.writes[0].reject(new Error('network failure'))
    await assert.rejects(failed, /network failure/)
    assert.equal(f.queue.state('draft'), 'error')
    assert.equal(f.saved.length, 0)
    const retry = f.queue.flush('draft')
    assert.equal(f.writes[1].revision, 1)
    f.writes[1].resolve(2)
    await retry
    assert.equal(f.queue.state('draft'), 'saved')
    f.queue.forget('draft')
})

test('switching instances keeps the pending payload and revision of each draft', async () => {
    const f = fixture()
    f.queue.schedule('draft-A', value('A task', 'A'), 1, 60000)
    f.queue.schedule('draft-B', value('B task', 'B'), 8, 60000)
    const flush = f.queue.flushAll()
    assert.deepEqual(f.writes.map(w => [w.id, w.snapshot.instanceId, w.revision]), [['draft-A', 'A', 1], ['draft-B', 'B', 8]])
    f.writes[0].resolve(2); f.writes[1].resolve(9)
    await flush
    assert.equal(f.queue.revision('draft-A', 1), 2)
    assert.equal(f.queue.revision('draft-B', 1), 9)
    f.queue.forget('draft-A'); f.queue.forget('draft-B')
})

test('forgetting a deleted draft cancels its pending write', async () => {
    const f = fixture()
    f.queue.schedule('deleted', value('deleted'), 1, 60000)
    f.queue.forget('deleted')
    await f.queue.flushAll()
    assert.equal(f.writes.length, 0)
})