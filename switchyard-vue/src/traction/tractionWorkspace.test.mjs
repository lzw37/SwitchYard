import test from 'node:test'
import assert from 'node:assert/strict'
import axios from 'axios'
import { createDefaultTrainPresetCatalog } from './trainPresets.ts'
import { defaultTrainParameters, defaultRunOptions, defaultSignalParameters } from './tractionCalculation.ts'
import { loadTractionRoutes, openTractionRoute, deleteTractionRoute, loadTractionSets, selectTractionSet, manageTractionSet, TractionWorkspaceSession, loadTractionWorkspace, loadTractionCalculation, calculateStoredTraction, workspaceFingerprint, overrideKey } from './tractionWorkspace.ts'
import { buildTractionApiRequest } from './tractionApi.ts'
const scope = { instanceID: 'instance', stationSchemeID: 'scheme' }
const content = () => ({ catalog: createDefaultTrainPresetCatalog(), settings: { selectedRouteId: 'R', continuationId: '',
    signal: defaultSignalParameters(), run: defaultRunOptions(), distanceMode: 'auto', metersPerUnit: 1, defaultLimit: 80 }, overrides: [], speedLimitPoints: [] })
const response = (config,data,status=200) => ({config,data,status,statusText:'OK',headers:{}})
function adapter(t, handler) { const old=axios.defaults.adapter; t.after(()=>{axios.defaults.adapter=old}); axios.defaults.adapter=handler }

test('database load distinguishes an uninitialized scope from persisted data', async t => {
    adapter(t,async config=>response(config,undefined,204))
    assert.equal(await loadTractionWorkspace(scope),null)
    adapter(t,async config=>response(config,{revision:3,...content()}))
    const saved=await loadTractionWorkspace(scope)
    assert.equal(saved.revision,3)
    assert.equal(saved.catalog.presets.length,3)
})

test('rapid saves are serialized by acknowledged revision and own immutable snapshots', async t=>{
    const calls=[]
    let release
    adapter(t,async config=>{ const body=JSON.parse(config.data); calls.push(body); if(calls.length===1) await new Promise(resolve=>release=resolve); return response(config,{...body.workspace,revision:body.workspace.revision+1}) })
    const session=new TractionWorkspaceSession(scope,null), first=content(), second=content()
    second.settings.signal.reactionS=9
    const a=session.save(first), b=session.save(second)
    first.catalog.presets[0].parameters.tractionCurve[0].forceKn=1
    await new Promise(resolve=>setTimeout(resolve,0))
    assert.equal(calls.length,1)
    release()
    assert.equal(await a,1); assert.equal(await b,2)
    assert.equal(calls[0].workspace.catalog.presets[0].parameters.tractionCurve[0].forceKn,300)
    assert.equal(calls[1].workspace.revision,1)
    assert.equal(calls[1].workspace.settings.signal.reactionS,9)
    assert.equal(await session.save(second),2)
    assert.equal(calls.length,2)
})

test('failed write keeps the acknowledged revision and can be retried',async t=>{
    let fail=true,calls=0
    adapter(t,async config=>{calls++; if(fail)throw new axios.AxiosError('offline','ERR_NETWORK',config);const body=JSON.parse(config.data);return response(config,{...body.workspace,revision:body.workspace.revision+1})})
    const session=new TractionWorkspaceSession(scope,null)
    await assert.rejects(session.save(content()),/offline/)
    assert.equal(session.revision,0);assert.equal(session.fingerprint,'')
    fail=false
    assert.equal(await session.save(content()),1);assert.equal(calls,2)
})

test('revision conflict blocks stale automatic retries until database is reloaded',async t=>{
    let calls=0
    adapter(t,async config=>{calls++;throw new axios.AxiosError('conflict','ERR_BAD_REQUEST',config,{},response(config,{},409))})
    const session=new TractionWorkspaceSession(scope,{revision:2,...content()}), next=content()
    next.settings.defaultLimit=90
    await assert.rejects(session.save(next),/conflict/)
    next.settings.defaultLimit=100
    await assert.rejects(session.save(next),/conflict/)
    assert.equal(calls,1);assert.equal(session.revision,2)
})

test('incomplete or changed database acknowledgement is not reported as saved',async t=>{
    adapter(t,async config=>{const body=JSON.parse(config.data);body.workspace.catalog.presets.pop();return response(config,{...body.workspace,revision:1})})
    const session=new TractionWorkspaceSession(scope,null)
    await assert.rejects(session.save(content()),/acknowledge/)
    assert.equal(session.revision,0);assert.equal(session.fingerprint,'')
})

test('optional SQL null columns and relational row ordering normalize without data changes',async t=>{
    const row={routeId:'R',routeStartNodeId:'A',routeEndNodeId:'B',continuationId:'',segmentIndex:0,segmentId:'L',speedLimitKmh:90}
    const initial=content();delete initial.catalog.presets[0].nameEn;initial.overrides.push(row,{...row,segmentIndex:1,segmentId:'L2',speedLimitKmh:100})
    adapter(t,async config=>{const body=JSON.parse(config.data);body.workspace.catalog.presets[0].nameEn=null;body.workspace.overrides.reverse().forEach(r=>{r.gradientPermille=null;r.additionalResistanceKn=null});return response(config,{...body.workspace,revision:1})})
    const session=new TractionWorkspaceSession(scope,null)
    assert.equal(await session.save(initial),1)
    assert.equal(session.fingerprint,workspaceFingerprint(initial))
    assert.notEqual(overrideKey(row),overrideKey({...row,routeId:'R:A',routeStartNodeId:''}))
})

test('scopes keep independent revisions and payloads',async t=>{
    const calls=[]
    adapter(t,async config=>{const body=JSON.parse(config.data);calls.push(body);return response(config,{...body.workspace,revision:body.workspace.revision+1})})
    const a=new TractionWorkspaceSession(scope,null), b=new TractionWorkspaceSession({...scope,stationSchemeID:'other'},null)
    await Promise.all([a.save(content()),b.save(content())])
    assert.deepEqual(calls.map(c=>c.stationSchemeID).sort(),['other','scheme'])
    assert.equal(a.revision,1);assert.equal(b.revision,1)
})

test('calculation storage failure is propagated without a stateless fallback',async t=>{
    const calls=[]
    adapter(t,async config=>{calls.push(config.url);throw new axios.AxiosError('storage failed','ERR_BAD_RESPONSE',config,{},response(config,{},500))})
    await assert.rejects(calculateStoredTraction(scope,1,{}),/storage failed/)
    assert.deepEqual(calls,['/api/SpeedProfile/workspace/calculate'])
})

const calculationSnapshot = () => ({
    revision: 2, currentWorkspaceRevision: 2, isCurrent: true, savedAtUtc: '2026-10-23T00:00:00Z', selectedRouteId: 'R-old', continuationId: '',
    request: buildTractionApiRequest({ lengthM: 1000, segments: [{ startM: 0, endM: 1000, speedLimitKmh: 80 }], joints: [], cells: [] },
        defaultTrainParameters(), defaultSignalParameters(), defaultRunOptions()),
    result: { engine: 'SwitchYard.SpeedProfile', lengthM: 1000, trainLengthM: 200, mode: 'arrival',
        durationS: 100, runningTimeS: 100, dwellTimeS: 0, motionStartS: 18, totalTimeS: 118, maxSpeedKmh: 60, warnings: [],
        samples: [{positionM:0,speedKmh:40,limitKmh:80,timeS:18},{positionM:1000,speedKmh:0,limitKmh:80,timeS:118}], jointTimes: [], cellOccupancies: [] },
})

test('loading an older calculation retains its original request and route without submitting calculation', async t => {
    const calls=[], original=calculationSnapshot(); original.currentWorkspaceRevision=5; original.isCurrent=false
    adapter(t,async config=>{calls.push([config.method,config.url]);return response(config,original)})
    const loaded=await loadTractionCalculation(scope)
    assert.equal(loaded.isCurrent,false); assert.equal(loaded.revision,2); assert.equal(loaded.selectedRouteId,'R-old')
    assert.deepEqual(loaded.request,original.request); assert.deepEqual(loaded.result,original.result)
    assert.deepEqual(calls,[['get','/api/SpeedProfile/workspace/calculation']])
})

test('missing or unreadable saved results never initiate a calculation fallback', async t => {
    const calls=[]
    adapter(t,async config=>{calls.push(config.method);return response(config,undefined,204)})
    assert.equal(await loadTractionCalculation(scope),null)
    adapter(t,async config=>{calls.push(config.method);return response(config,'<html>unavailable</html>')})
    await assert.rejects(loadTractionCalculation(scope),{code:'invalid-response'})
    adapter(t,async config=>{calls.push(config.method);throw new Error('read failed')})
    await assert.rejects(loadTractionCalculation(scope),/read failed/)
    assert.deepEqual(calls,['get','get','get'])
})

test('explicit calculation returns the committed input/result snapshot and route identity', async t => {
    const expected=calculationSnapshot(), calls=[]
    adapter(t,async config=>{calls.push([config.method,config.url,JSON.parse(config.data)]);return response(config,expected)})
    assert.deepEqual(await calculateStoredTraction(scope,2,expected.request),expected)
    assert.deepEqual(calls,[['post','/api/SpeedProfile/workspace/calculate',{...scope,revision:2,request:expected.request}]])
})

test('a calculation acknowledgement for different inputs or revision cannot replace the displayed snapshot', async t => {
    const expected=calculationSnapshot(), changed=calculationSnapshot();changed.request.signal.reactionS++
    adapter(t,async config=>response(config,changed))
    await assert.rejects(calculateStoredTraction(scope,2,expected.request),{code:'invalid-response'})
    adapter(t,async config=>response(config,{...expected,revision:3}))
    await assert.rejects(calculateStoredTraction(scope,2,expected.request),{code:'invalid-response'})
})


test('calculation sets in the same station have independent write queues and IDs in every request',async t=>{
    const calls=[]
    adapter(t,async config=>{const body=JSON.parse(config.data);calls.push(body);return response(config,{...body.workspace,revision:body.workspace.revision+1})})
    const a=new TractionWorkspaceSession({...scope,calculationSetID:'a'},null),b=new TractionWorkspaceSession({...scope,calculationSetID:'b'},null)
    await Promise.all([a.save(content()),b.save(content())])
    assert.deepEqual(calls.map(c=>c.calculationSetID).sort(),['a','b']);assert.equal(a.revision,1);assert.equal(b.revision,1)
})

test('listing and selecting calculation sets never submit a calculation',async t=>{
    const calls=[],catalog={selectedId:'b',sets:[{id:'a',name:'甲',revision:1},{id:'b',name:'乙',revision:2}]}
    adapter(t,async config=>{calls.push([config.method,config.url,config.data&&JSON.parse(config.data)]);return response(config,config.method==='get'?catalog:null)})
    assert.deepEqual(await loadTractionSets(scope),catalog)
    await selectTractionSet({...scope,calculationSetID:'b'})
    assert.deepEqual(calls,[['get','/api/SpeedProfile/workspace/sets',undefined],['put','/api/SpeedProfile/workspace/sets/selection',{...scope,calculationSetID:'b'}]])
})

test('set management sends explicit source/target IDs and acknowledged metadata revisions',async t=>{
    const calls=[]
    adapter(t,async config=>{calls.push([config.method,config.url,JSON.parse(config.data)]);return response(config,{id:'copy',name:'复制',revision:1})})
    await manageTractionSet(scope,{kind:'create',name:'复制',sourceId:'a',revision:8})
    await manageTractionSet(scope,{kind:'rename',id:'copy',name:'改名',revision:1})
    await manageTractionSet(scope,{kind:'delete',id:'copy',revision:2})
    assert.deepEqual(calls,[['post','/api/SpeedProfile/workspace/sets',{...scope,name:'复制',sourceId:'a',revision:8}],['put','/api/SpeedProfile/workspace/sets/name',{...scope,calculationSetID:'copy',revision:1,name:'改名'}],['delete','/api/SpeedProfile/workspace/sets',{...scope,calculationSetID:'copy',revision:2}]])
})


test('route records within one set keep independent save queues and carry route identity on reads and calculations',async t=>{
    const calls=[]
    adapter(t,async config=>{const body=config.data?JSON.parse(config.data):undefined;calls.push({method:config.method,url:config.url,scope:body||config.params});return response(config,config.method==='put'?{...body.workspace,revision:body.workspace.revision+1}:config.url.endsWith('/calculate')||config.url.endsWith('/calculation')?calculationSnapshot():{revision:1,...content()})})
    const a={...scope,calculationSetID:'same-set',routeCaseID:'a'}, b={...a,routeCaseID:'b'}
    const sa=new TractionWorkspaceSession(a,null),sb=new TractionWorkspaceSession(b,null)
    await Promise.all([sa.save(content()),sb.save(content())])
    await loadTractionWorkspace(a);await loadTractionCalculation(b);await calculateStoredTraction(a,2,calculationSnapshot().request)
    assert.deepEqual(calls.map(c=>c.scope.routeCaseID),['a','b','a','b','a'])
    assert.ok(calls.every(c=>c.scope.calculationSetID==='same-set'));assert.equal(sa.revision,1);assert.equal(sb.revision,1)
})
test('listing opening and deleting route records never submit calculations',async t=>{
    const calls=[],set={...scope,calculationSetID:'plan'},row={id:'route-case',routeId:'R',continuationId:'R2',revision:5,isSelected:true}
    adapter(t,async config=>{calls.push([config.method,config.url,config.data?JSON.parse(config.data):config.params]);return response(config,config.method==='get'?[row]:row)})
    assert.deepEqual(await loadTractionRoutes(set),[row]);assert.deepEqual(await openTractionRoute(set,'R','R2'),row);await deleteTractionRoute(set,row)
    assert.deepEqual(calls,[['get','/api/SpeedProfile/workspace/routes',set],['post','/api/SpeedProfile/workspace/routes/open',{...set,routeId:'R',continuationId:'R2'}],['delete','/api/SpeedProfile/workspace/routes',{...set,routeCaseID:'route-case',revision:5}]])
})
test('a wrong route acknowledgement and failed route read do not initiate computation',async t=>{
    const calls=[]
    adapter(t,async config=>{calls.push(config.url);return response(config,{id:'wrong',routeId:'different',continuationId:''})})
    await assert.rejects(openTractionRoute(scope,'R',''),/Invalid route record/)
    await assert.rejects(loadTractionRoutes(scope),/Invalid route result list/)
    assert.ok(calls.every(url=>!url.endsWith('/calculate')))
})

test('legacy workspaces without anchors load as empty while saved anchor snapshots are immutable',async t=>{
    const legacy=content();delete legacy.speedLimitPoints
    adapter(t,async config=>response(config,{revision:1,...legacy}))
    assert.deepEqual((await loadTractionWorkspace(scope)).speedLimitPoints,[])
    const calls=[];adapter(t,async config=>{const body=JSON.parse(config.data);calls.push(body);return response(config,{...body.workspace,revision:body.workspace.revision+1})})
    const input=content();input.speedLimitPoints=[{positionM:0,speedLimitKmh:80},{positionM:250.125,speedLimitKmh:45}]
    const session=new TractionWorkspaceSession(scope,null),pending=session.save(input)
    input.speedLimitPoints[1].speedLimitKmh=60
    await pending
    assert.equal(calls[0].workspace.speedLimitPoints[1].speedLimitKmh,45)
    assert.notEqual(session.fingerprint,workspaceFingerprint(input))
})
test('invalid database anchors are rejected instead of silently using line defaults',async t=>{
    for(const points of [[null],[{positionM:10,speedLimitKmh:80}],[{positionM:0,speedLimitKmh:80},{positionM:0,speedLimitKmh:45}]]){
        adapter(t,async config=>response(config,{revision:1,...content(),speedLimitPoints:points}))
        await assert.rejects(loadTractionWorkspace(scope),/Invalid speed limit points/)
    }
})
