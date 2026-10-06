import assert from 'node:assert/strict'
import fs from 'node:fs'
import ts from 'typescript'

const source = fs.readFileSync(new URL('./analysisSaveQueue.ts', import.meta.url), 'utf8')
const compiled = ts.transpileModule(source, { compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.ES2022 } }).outputText
const { AnalysisSaveQueue } = await import(`data:text/javascript;base64,${Buffer.from(compiled).toString('base64')}`)
let release
const writes = [], errors = []
let active = 0, maxActive = 0
const queue = new AnalysisSaveQueue(async value => {
    active++; maxActive = Math.max(maxActive, active); writes.push(value)
    try {
        if (value === 2) await new Promise(resolve => { release = resolve })
        if (value === 'fail') throw new Error('expected')
    } finally { active-- }
}, error => errors.push(error))
queue.schedule('plan', 1)
queue.schedule('plan', 2)
const first = queue.flush()
assert.deepEqual(writes, [2], 'Rapid edits must coalesce')
queue.schedule('plan', 3)
queue.schedule('plan', 4)
queue.schedule('other-plan', 5)
const leaving = queue.flush()
assert.deepEqual(writes, [2], 'An older write must finish before its replacement')
release()
await Promise.all([first, leaving])
assert.deepEqual(writes, [2, 4, 5], 'Flush must preserve the latest value of each plan when navigating away')
assert.equal(maxActive, 1)
queue.schedule('plan', 'fail'); queue.schedule('other-plan', 6)
await queue.flush()
assert.equal(errors.length, 1)
assert.equal(writes.at(-1), 6, 'A failed write must not strand the following plan')
queue.schedule('cancelled', 7); queue.cancelPending('cancelled'); await queue.flush()
assert.equal(writes.at(-1), 6)
console.log('PASS coalescing, serialized writes, scope isolation, exit flush, cancellation and failure recovery')
