import test from 'node:test'
import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { runInNewContext } from 'node:vm'
import { createServer } from 'node:http'
import { parse, compileScript } from '@vue/compiler-sfc'
import ts from 'typescript'
import * as vue from 'vue'
import axios from 'axios'
import * as calculation from './tractionCalculation.ts'
import * as presets from './trainPresets.ts'
import { importTrainCharacteristics } from './trainCharacteristicsExcel.ts'

const curves = () => ({
    tractionCurve: [{ speedKmh: 0, forceKn: 200 }, { speedKmh: 90.5, forceKn: 100.25 }],
    brakingCurve: [{ speedKmh: 0, forceKn: 80 }, { speedKmh: 40, forceKn: 110 }],
    resistanceCurve: [{ speedKmh: 0, forceKn: 0 }, { speedKmh: 90, forceKn: 12.5 }],
})
const file = () => new File(['test workbook'], 'characteristics.xlsx')
function adapter(t, callback) {
    const previous = axios.defaults.adapter
    axios.defaults.adapter = callback
    t.after(() => { axios.defaults.adapter = previous })
}
const response = (config, data) => ({ config, data, status: 200, statusText: 'OK', headers: {} })

test('Excel API uploads the original file and returns independently owned, complete curves', async t => {
    const upload = file(), data = curves(), signal = new AbortController().signal
    adapter(t, async config => {
        assert.equal(config.url, '/api/SpeedProfile/train-characteristics/import')
        assert.equal(config.method, 'post')
        assert.equal(config.data.get('file'), upload)
        assert.equal(config.signal, signal)
        return response(config, data)
    })
    const result = await importTrainCharacteristics(upload, signal)
    assert.deepEqual(result, data)
    result.tractionCurve[0].forceKn = 1
    assert.equal(data.tractionCurve[0].forceKn, 200)
})

test('Excel upload remains multipart with intact binary data when the app defaults to JSON', async t => {
    const original = {
        baseURL: axios.defaults.baseURL,
        adapter: axios.defaults.adapter,
        contentType: axios.defaults.headers.common['Content-Type'],
    }
    const bytes = new Uint8Array([80, 75, 0, 255, 128, 0, 42])
    const upload = new File([bytes], '列车特性.xlsx', { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' })
    let received, serverError
    const server = createServer(async (request, response) => {
        try {
            if (!/^multipart\/form-data;\s*boundary=/i.test(request.headers['content-type'] ?? '')) {
                request.resume()
                response.writeHead(415).end('Unsupported Media Type')
                return
            }
            const chunks = []
            for await (const chunk of request) chunks.push(chunk)
            const form = await new Request('http://localhost' + request.url, {
                method: 'POST', headers: request.headers, body: Buffer.concat(chunks),
            }).formData()
            const file = form.get('file')
            received = { url: request.url, name: file.name, bytes: new Uint8Array(await file.arrayBuffer()) }
            response.writeHead(200, { 'Content-Type': 'application/json' }).end(JSON.stringify(curves()))
        } catch (error) {
            serverError = error
            response.writeHead(500).end('Invalid upload')
        }
    })
    t.after(async () => {
        axios.defaults.baseURL = original.baseURL
        axios.defaults.adapter = original.adapter
        axios.defaults.headers.common['Content-Type'] = original.contentType
        await new Promise(resolve => server.close(resolve))
    })
    await new Promise((resolve, reject) => { server.once('error', reject); server.listen(0, '127.0.0.1', resolve) })
    axios.defaults.baseURL = `http://127.0.0.1:${server.address().port}`
    axios.defaults.adapter = 'http'
    // Match src/utils/axios.ts, which initializes the shared client in main.ts.
    axios.defaults.headers.common['Content-Type'] = 'application/json'
    assert.deepEqual(await importTrainCharacteristics(upload), curves())
    assert.equal(serverError, undefined)
    assert.equal(received.url, '/api/SpeedProfile/train-characteristics/import')
    assert.equal(received.name, upload.name)
    assert.deepEqual(received.bytes, bytes)
})

test('Excel API rejects incomplete, nonnumeric, out-of-range and misordered responses before returning data', async t => {
    let data
    adapter(t, async config => response(config, data))
    for (const invalid of [
        '<html>sign in</html>', { ...curves(), brakingCurve: [] }, { ...curves(), resistanceCurve: null },
        { ...curves(), tractionCurve: [{ speedKmh: 1, forceKn: 1 }] },
        { ...curves(), tractionCurve: [{ speedKmh: 0, forceKn: 1 }, { speedKmh: 1, forceKn: 2 }] },
        { ...curves(), brakingCurve: [{ speedKmh: 0, forceKn: 1 }, { speedKmh: 0, forceKn: 1 }] },
        { ...curves(), resistanceCurve: [{ speedKmh: 0, forceKn: '0' }] },
        { ...curves(), brakingCurve: [{ speedKmh: 0, forceKn: -1 }] },
        { ...curves(), resistanceCurve: Array.from({ length: 129 }, (_, i) => ({ speedKmh: i, forceKn: 0 })) },
    ]) {
        data = invalid
        await assert.rejects(importTrainCharacteristics(file()), /Invalid train characteristics/)
    }
})

const { descriptor } = parse(readFileSync(new URL('./TrainParametersDialog.vue', import.meta.url), 'utf8'))
const script = compileScript(descriptor, { id: 'train-excel-test' }).content.replace('import.meta.env.BASE_URL', "'/'")
function fixture(t, importer) {
    const exports = {}, hooks = [], events = []
    runInNewContext(ts.transpileModule(script, { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 } }).outputText, {
        exports, console, AbortController,
        require(name) {
            if (name === 'vue') return { ...vue, onBeforeUnmount: fn => hooks.push(fn) }
            if (name === 'vue-i18n') return { useI18n: () => ({ locale: vue.ref('zh') }) }
            if (name === './tractionCalculation') return calculation
            if (name === './trainPresets') return presets
            if (name === './trainCharacteristicsExcel') return { importTrainCharacteristics: importer, MAX_CHARACTERISTICS_FILE_BYTES: 2 * 1024 * 1024 }
            throw new Error('Unexpected import: ' + name)
        },
    })
    const props = vue.reactive({ modelValue: true, catalog: presets.createDefaultTrainPresetCatalog(), saving: false })
    const scope = vue.effectScope()
    const state = scope.run(() => exports.default.setup(props, { expose() {}, emit: (...event) => events.push(event) }))
    t.after(() => { for (const hook of hooks) hook(); scope.stop() })
    const upload = (value = file()) => {
        const input = { files: [value], value: 'selected.xlsx' }
        return { input, promise: state.importExcel({ target: input }) }
    }
    return { props, state, events, upload }
}

test('dialog imports all three curves into only the selected draft and keeps the explicit project-save boundary', async t => {
    const { props, state, events, upload } = fixture(t, async () => curves())
    const original = structuredClone(vue.toRaw(props.catalog))
    const { input, promise } = upload()
    await promise
    assert.equal(input.value, '', 'The same file can be selected again')
    assert.deepEqual(JSON.parse(JSON.stringify(state.current.value.parameters.tractionCurve)), curves().tractionCurve)
    assert.equal(state.current.value.parameters.massT, original.presets[0].parameters.massT)
    assert.equal(state.current.value.parameters.maxBrakeForceKn, original.presets[0].parameters.maxBrakeForceKn)
    assert.deepEqual(JSON.parse(JSON.stringify(state.draft.value.presets.slice(1))), original.presets.slice(1))
    assert.deepEqual(vue.toRaw(props.catalog), original)
    assert.equal(events.length, 0, 'Uploading never commits the project library')
    assert.match(state.importSuccess.value, /保存到项目/)
    state.save()
    assert.equal(events[0][0], 'save')
    assert.deepEqual(JSON.parse(JSON.stringify(events[0][1].presets[0].parameters.brakingCurve)), curves().brakingCurve)
})

test('failed files preserve every draft value and report the server sheet/row error', async t => {
    const { state, upload } = fixture(t, async () => { throw { response: { data: { message: 'Sheet“制动特性”第 3 行：速度不可重复。' } } } })
    const before = JSON.stringify(state.draft.value)
    await upload().promise
    assert.equal(JSON.stringify(state.draft.value), before)
    assert.match(state.importError.value, /制动特性.*第 3 行/)
    assert.equal(state.importing.value, false)
})

for (const change of ['selection', 'close', 'reopen']) {
    test(`pending imports cannot affect a different dialog state after ${change}`, async t => {
        let resolve, signal
        const { props, state, events, upload } = fixture(t, (_file, abortSignal) => {
            signal = abortSignal
            return new Promise(done => { resolve = done })
        })
        const before = JSON.stringify(state.draft.value.presets)
        const { promise } = upload()
        state.save()
        assert.equal(events.length, 0, 'Saving is disabled during file parsing')
        if (change === 'selection') state.draft.value.selectedId = state.draft.value.presets[1].id
        else props.modelValue = false
        await vue.nextTick()
        if (change === 'reopen') { props.modelValue = true; await vue.nextTick() }
        assert.equal(signal.aborted, true)
        resolve(curves())
        await promise
        assert.equal(JSON.stringify(state.draft.value.presets), before)
        assert.equal(state.importing.value, false)
    })
}

test('invalid file selection never starts an upload or changes draft data', async t => {
    let calls = 0
    const { state, upload } = fixture(t, async () => { calls++; return curves() })
    const before = JSON.stringify(state.draft.value)
    for (const invalid of [new File(['data'], 'file.xls'), new File([], 'empty.xlsx'), new File([new Uint8Array(2 * 1024 * 1024 + 1)], 'large.xlsx')]) {
        await upload(invalid).promise
        assert.ok(state.importError.value)
    }
    assert.equal(calls, 0)
    assert.equal(JSON.stringify(state.draft.value), before)
})
