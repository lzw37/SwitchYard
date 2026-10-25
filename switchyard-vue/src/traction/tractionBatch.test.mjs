import assert from 'node:assert/strict'
import test from 'node:test'
import axios from 'axios'
import { createDefaultTrainPresetCatalog, validateTrainPresetCatalog } from './trainPresets.ts'
import { defaultRunOptions, defaultSignalParameters } from './tractionCalculation.ts'
import { overrideKey } from './tractionWorkspace.ts'
import { tractionBatchCandidates, loadTractionBatchJob, tractionBatchProfile, tractionBatchIssues, setBatchLineParameter,
    setBatchLineLimit, applyTractionBatchParameters, runTractionBatch } from './tractionBatch.ts'

function fixture() {
    const routes = [
        { id: 'R1', type: 'arrival', description: 'Arrival', startNodeID: 'A', endNodeID: 'B', nodeList: 'A,B', linkList: 'AB', cellList: 'C1' },
        { id: 'R2', type: 'departure', description: 'Departure', startNodeID: 'B', endNodeID: 'C', nodeList: 'B,C', linkList: 'BC', cellList: 'C2' },
        { id: 'R3', type: 'shunting', description: 'Shunting', startNodeID: 'C', endNodeID: 'D', nodeList: 'C,D', linkList: 'CD', cellList: 'C3' },
    ]
    return { scope: { instanceID: 'I', stationSchemeID: 'S', calculationSetID: 'set' }, routes, cases: [],
        document: { nodes: ['A','B','C','D'].map((id, index) => ({ id, x: index * 100, y: 0 })),
            tracks: routes.map((route, index) => ({ id: route.linkList, fromNodeID: route.startNodeID, toNodeID: route.endNodeID,
                x1: index * 100, y1: 0, x2: (index + 1) * 100, y2: 0, lengthM: 100, speedLimitKmh: 60, gradientPermille: index + 1 })),
            cells: routes.map((route, index) => ({ id: `C${index + 1}`, linkIDList: route.linkList })), insulationJoints: [], switches: [], signals: [] },
        seed: { catalog: createDefaultTrainPresetCatalog(), settings: { selectedRouteId: 'R1', continuationId: '',
            signal: defaultSignalParameters(), run: defaultRunOptions(), distanceMode: 'auto', metersPerUnit: 1, defaultLimit: 80 },
            speedLimitPoints: [], overrides: [] },
    }
}
const clone = value => structuredClone(value)
const reply = (config, data, status = 200) => ({ config, data: clone(data), status, statusText: 'OK', headers: {} })
function mockServer(t, context, hooks = {}) {
    const old = axios.defaults.adapter
    t.after(() => { axios.defaults.adapter = old })
    const calls = [], workspaces = new Map(), records = new Map(context.cases.map(row => [JSON.stringify([row.routeId, row.continuationId]), clone(row)]))
    let index = 0
    axios.defaults.adapter = async config => {
        const body = config.data ? JSON.parse(config.data) : null
        calls.push({ method: config.method, url: config.url, params: clone(config.params), body: clone(body) })
        if (config.url.endsWith('/routes/open')) {
            assert.equal(body.select, false, 'batch preparation never selects its route')
            const key = JSON.stringify([body.routeId, body.continuationId])
            if (!records.has(key)) records.set(key, { id: `case-${++index}`, routeId: body.routeId, continuationId: body.continuationId, revision: 1, isSelected: false })
            return reply(config, records.get(key))
        }
        if (config.method === 'get') {
            const saved = workspaces.get(config.params.routeCaseID)
            return reply(config, saved ?? null, saved ? 200 : 204)
        }
        if (config.method === 'put') {
            const previous = workspaces.get(body.routeCaseID)
            if (body.workspace.revision !== (previous?.revision ?? 0)) throw new axios.AxiosError('conflict', 'ERR_BAD_REQUEST', config, {}, reply(config, {}, 409))
            const saved = { ...body.workspace, revision: body.workspace.revision + 1 }
            workspaces.set(body.routeCaseID, clone(saved))
            return reply(config, saved)
        }
        if (config.url.endsWith('/calculate')) {
            const workspace = workspaces.get(body.routeCaseID)
            await hooks.calculate?.(body, workspace)
            if (hooks.failRoute === workspace.settings.selectedRouteId) throw new axios.AxiosError('infeasible', 'ERR_BAD_REQUEST', config, {}, reply(config, { message: 'No feasible run' }, 422))
            return reply(config, { revision: body.revision, currentWorkspaceRevision: body.revision, isCurrent: true,
                savedAtUtc: '2026-10-25T00:00:00Z', selectedRouteId: hooks.wrongIdentity ? 'foreign' : workspace.settings.selectedRouteId,
                continuationId: workspace.settings.continuationId, request: body.request,
                result: { engine: 'SwitchYard.SpeedProfile', mode: body.request.options.mode, lengthM: body.request.profile.lengthM,
                    trainLengthM: body.request.train.lengthM, durationS: 20, runningTimeS: 20, dwellTimeS: 0, motionStartS: 18, totalTimeS: 38,
                    maxSpeedKmh: 40, warnings: [], jointTimes: [], cellOccupancies: [],
                    samples: [{ positionM: 0, timeS: 18, speedKmh: body.request.options.initialSpeedKmh, limitKmh: 60 },
                        { positionM: body.request.profile.lengthM, timeS: 38, speedKmh: body.request.options.finalSpeedKmh, limitKmh: 60 }] } })
        }
        throw new Error(`Unexpected request ${config.method} ${config.url}`)
    }
    return { calls, workspaces, records }
}

test('batch candidates include all station routes and existing continuations without duplicate pairs', () => {
    const context = fixture()
    context.cases = [{ id: 'saved', routeId: 'R1', continuationId: '', isCurrent: true }, { id: 'joined', routeId: 'R1', continuationId: 'R2' },
        { id: 'removed', routeId: 'missing', continuationId: '' }, { id: 'draft', routeId: '', continuationId: '' }]
    const candidates = tractionBatchCandidates(context.routes, context.cases)
    assert.equal(candidates.length, 5)
    assert.equal(candidates.filter(row => row.routeId === 'R1' && !row.continuationId).length, 1)
    assert.equal(candidates.find(row => row.record?.id === 'removed').available, false)
    assert.equal(candidates.find(row => row.record?.id === 'joined').continuationId, 'R2')
})

test('new route drafts use movement defaults and never inherit another route stops or speed anchors', async t => {
    const context = fixture(), original = clone(context)
    context.seed.settings.run.stops = [{ positionM: 25, dwellS: 30 }]
    context.seed.speedLimitPoints = [{ positionM: 0, speedLimitKmh: 30 }]
    const server = mockServer(t, context)
    const candidates = tractionBatchCandidates(context.routes, [])
    const jobs = await Promise.all(candidates.map(candidate => loadTractionBatchJob(context, candidate)))
    assert.deepEqual(jobs.map(job => [job.workspace.settings.run.mode, job.workspace.settings.run.initialSpeedKmh, job.workspace.settings.run.finalSpeedKmh]),
        [['arrival',40,0],['departure',0,40],['shunting',0,0]])
    assert.deepEqual(jobs[1].workspace.settings.run.stops, [])
    assert.deepEqual(jobs[1].workspace.speedLimitPoints, [])
    jobs[0].workspace.catalog.presets[0].parameters.massT = 1
    assert.equal(context.seed.catalog.presets[0].parameters.massT, original.seed.catalog.presets[0].parameters.massT)
    assert.deepEqual(server.calls, [], 'planning new routes does not create or calculate records')
})

test('existing route parameters load as isolated drafts and line edits preserve physical segment scope', async t => {
    const context = fixture()
    context.cases = [{ id: 'saved', routeId: 'R1', continuationId: '', revision: 2 }]
    const server = mockServer(t, context), stored = { revision: 3, ...clone(context.seed) }
    stored.settings.run.stops = [{ positionM: 25, dwellS: 9 }]
    server.workspaces.set('saved', clone(stored))
    const job = await loadTractionBatchJob(context, tractionBatchCandidates(context.routes, context.cases)[0])
    assert.equal(job.source.revision, 3)
    assert.deepEqual(job.workspace.settings.run.stops, [{ positionM: 25, dwellS: 9 }])
    const raw = tractionBatchProfile(context, job.workspace)
    setBatchLineParameter(context, job.workspace, raw.rows[0].id, 0, 'gradientPermille', 8)
    setBatchLineLimit(context, job.workspace, 20, 80, 30)
    const built = tractionBatchProfile(context, job.workspace)
    assert.deepEqual(built.profile.segments.map(row => [row.startM,row.endM,row.speedLimitKmh,row.gradientPermille]), [[0,20,60,8],[20,80,30,8],[80,100,60,8]])
    assert.equal(overrideKey(job.workspace.overrides[0]), overrideKey({ routeId:'R1',routeStartNodeId:'A',routeEndNodeId:'B',continuationId:'',segmentIndex:0,segmentId:raw.rows[0].id }))
    assert.deepEqual(server.workspaces.get('saved'), stored, 'editing does not write to the database')
    assert.deepEqual(tractionBatchIssues(context, job.workspace), [])
})

test('common train selection uses the project library and preserves route-specific stops and geometry', () => {
    const context = fixture(), target = clone(context.seed), common = clone(context.seed)
    target.settings.run = { ...defaultRunOptions('arrival'), stops: [{ positionM:25,dwellS:7 }] }
    target.speedLimitPoints = [{ positionM:0,speedLimitKmh:35 }]
    common.catalog.selectedId = common.catalog.presets[1].id
    common.settings.run = { ...defaultRunOptions('through'), initialSpeedKmh:20,finalSpeedKmh:30,brakingDistanceM:125,stops:[{positionM:75,dwellS:99}] }
    const before = clone(target), next = applyTractionBatchParameters(target, common, ['train','run'])
    assert.equal(next.settings.run.mode, 'arrival')
    assert.equal(next.settings.run.initialSpeedKmh, 20)
    assert.equal(next.settings.run.finalSpeedKmh, 0)
    assert.deepEqual(next.settings.run.stops, target.settings.run.stops)
    assert.deepEqual(next.speedLimitPoints, target.speedLimitPoints)
    assert.equal(next.catalog.presets.length, target.catalog.presets.length)
    assert.equal(next.catalog.selectedId, common.catalog.selectedId)
    assert.deepEqual(next.catalog.presets[0], target.catalog.presets[0])
    assert.equal(validateTrainPresetCatalog(next.catalog), '')
    assert.deepEqual(target, before)
    const changed=clone(common);changed.catalog.presets[0].parameters.massT+=10
    assert.throws(()=>applyTractionBatchParameters(target,changed,['train']),/项目列车参数已更新/)
    const modeChanged = applyTractionBatchParameters(target, { ...common, settings:{...common.settings,run:defaultRunOptions('shunting')} }, ['mode'])
    assert.equal(modeChanged.settings.run.mode,'shunting'); assert.equal(modeChanged.settings.run.initialSpeedKmh,0); assert.equal(modeChanged.settings.run.finalSpeedKmh,0)
})

test('batch commits selected routes in order and a failed calculation does not stop other routes', async t => {
    const context = fixture(), server = mockServer(t, context, { failRoute:'R2' })
    const jobs = await Promise.all(tractionBatchCandidates(context.routes, []).map(candidate => loadTractionBatchJob(context, candidate)))
    const report = await runTractionBatch(context, jobs, () => false)
    assert.deepEqual(report.jobs.map(job => job.status), ['success','failed','success'])
    assert.match(report.jobs[1].error,/No feasible run/)
    assert.equal(server.workspaces.size,3)
    const calculates = server.calls.filter(call => call.url.endsWith('/calculate'))
    assert.deepEqual(calculates.map(call => call.body.request.options.mode), ['arrival','departure','shunting'])
    assert.ok(server.calls.every(call => (call.body ?? call.params).calculationSetID === 'set'))
    assert.ok(report.jobs.every(job => job.source.revision === 1), 'acknowledged workspace versions remain available for a retry')
})

test('stopping completes the current route and makes no requests for subsequent routes', async t => {
    const context = fixture(); let stop = false
    const server = mockServer(t, context, { calculate:async()=>{stop=true} })
    const jobs = await Promise.all(tractionBatchCandidates(context.routes, []).map(candidate => loadTractionBatchJob(context,candidate)))
    await runTractionBatch(context,jobs,()=>stop)
    assert.deepEqual(jobs.map(job=>job.status),['success','stopped','stopped'])
    assert.equal(server.calls.filter(call=>call.url.endsWith('/routes/open')).length,1)
    assert.equal(server.workspaces.size,1)
})

test('an existing revision conflict never overwrites a newer workspace', async t => {
    const context = fixture(); context.cases=[{id:'saved',routeId:'R1',continuationId:'',revision:1}]
    const server=mockServer(t,context)
    server.workspaces.set('saved',{revision:1,...clone(context.seed)})
    const job=await loadTractionBatchJob(context,tractionBatchCandidates(context.routes,context.cases)[0])
    server.workspaces.get('saved').revision=2
    await runTractionBatch(context,[job],()=>false)
    assert.equal(job.status,'failed'); assert.equal(job.conflict,true)
    assert.equal(server.calls.filter(call=>call.method==='put'||call.url.endsWith('/calculate')).length,0)
    assert.equal(server.workspaces.get('saved').revision,2)
})

test('preflight rejects invalid parameters before creating a record, and wrong result identity is never accepted', async t => {
    const context=fixture(), server=mockServer(t,context,{wrongIdentity:true})
    const [candidate]=tractionBatchCandidates(context.routes,[])
    const invalid=await loadTractionBatchJob(context,candidate)
    invalid.workspace.settings.run.stops=[{positionM:200,dwellS:0}]
    await runTractionBatch(context,[invalid],()=>false)
    assert.equal(invalid.status,'failed'); assert.equal(server.calls.length,0)
    const job=await loadTractionBatchJob(context,candidate)
    await runTractionBatch(context,[job],()=>false)
    assert.equal(job.status,'failed'); assert.equal(job.calculation,undefined)
    assert.match(job.error,/归属/)
})

test('a running batch owns immutable per-route input snapshots', async t => {
    const context=fixture(); let jobs
    const server=mockServer(t,context,{calculate:async()=>{jobs[1].workspace.settings.signal.reactionS=99}})
    jobs=await Promise.all(tractionBatchCandidates(context.routes,[]).slice(0,2).map(candidate=>loadTractionBatchJob(context,candidate)))
    await runTractionBatch(context,jobs,()=>false)
    const calls=server.calls.filter(call=>call.url.endsWith('/calculate'))
    assert.equal(calls[1].body.request.signal.reactionS,3)
    assert.equal(jobs[1].source.settings.signal.reactionS,3)
})

test('retry after a calculation failure reuses the saved revision and only submits that route', async t => {
    const context=fixture(), hooks={failRoute:'R2'}, server=mockServer(t,context,hooks)
    const jobs=await Promise.all(tractionBatchCandidates(context.routes,[]).map(candidate=>loadTractionBatchJob(context,candidate)))
    await runTractionBatch(context,jobs,()=>false)
    assert.deepEqual(jobs.map(job=>job.status),['success','failed','success'])
    const failed=jobs[1], previousVersion=failed.source.revision, requestCount=server.calls.length
    hooks.failRoute=undefined
    await runTractionBatch(context,[failed],()=>false)
    const retried=server.calls.slice(requestCount)
    assert.equal(failed.status,'success')
    assert.equal(failed.source.revision,previousVersion)
    assert.equal(retried.filter(call=>call.method==='put').length,0,'unchanged saved inputs need no extra write')
    assert.deepEqual(retried.filter(call=>call.url.endsWith('/routes/open')).map(call=>call.body.routeId),['R2'])
    assert.equal(retried.filter(call=>call.url.endsWith('/calculate')).length,1)
    assert.deepEqual(jobs.map(job=>job.status),['success','success','success'])
})
