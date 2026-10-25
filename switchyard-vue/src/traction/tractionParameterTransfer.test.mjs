import assert from 'node:assert/strict'
import test from 'node:test'
import axios from 'axios'
import { createDefaultTrainPresetCatalog } from './trainPresets.ts'
import { defaultRunOptions, defaultSignalParameters } from './tractionCalculation.ts'
import { tractionBatchProfile } from './tractionBatch.ts'
import { buildTractionApiRequest } from './tractionApi.ts'
import { prepareTractionParameterTransfers as prepare, writeTractionParameterTransfers as write } from './tractionParameterTransfer.ts'

const clone = value => structuredClone(value)
const target = { instanceID:'I', stationSchemeID:'S', calculationParameterSetID:'parameters-2', trainTypeID:'' }
function fixture() {
    const routes = [
        {id:'R1',type:'through',description:'First',startNodeID:'A',endNodeID:'B',nodeList:'A,B',linkList:'AB',cellList:'C1'},
        {id:'R2',type:'through',description:'Second',startNodeID:'B',endNodeID:'C',nodeList:'B,C',linkList:'BC',cellList:'C2'},
    ]
    const context = {scope:{instanceID:'I',stationSchemeID:'S',calculationSetID:'traction-1'},routes,cases:[],
        document:{nodes:['A','B','C'].map((id,index)=>({id,x:index*100,y:0})),
            tracks:routes.map((route,index)=>({id:route.linkList,fromNodeID:route.startNodeID,toNodeID:route.endNodeID,x1:index*100,y1:0,x2:(index+1)*100,y2:0,lengthM:100,speedLimitKmh:60,gradientPermille:0})),
            cells:routes.map(route=>({id:route.cellList,linkIDList:route.linkList})),insulationJoints:[],switches:[],signals:[]},
        seed:{catalog:createDefaultTrainPresetCatalog(),settings:{selectedRouteId:'R1',continuationId:'',run:defaultRunOptions('through'),signal:defaultSignalParameters(),distanceMode:'auto',metersPerUnit:1,defaultLimit:80},overrides:[],speedLimitPoints:[]}}
    const workspaces = new Map(), calculations = new Map()
    function addCase(id,routeId,continuationId='') {
        const row={id,routeId,continuationId,revision:1,isSelected:false,trainName:'Test',savedAtUtc:'2026-10-25T01:00:00Z',durationS:10,isCurrent:true}
        context.cases.push(row)
        const workspace={revision:1,...clone(context.seed)}
        Object.assign(workspace.settings,{selectedRouteId:routeId,continuationId})
        const {profile}=tractionBatchProfile(context,workspace), train=workspace.catalog.presets[0].parameters
        const request=buildTractionApiRequest(profile,train,workspace.settings.signal,workspace.settings.run)
        const result={engine:'SwitchYard.SpeedProfile',mode:'through',lengthM:profile.lengthM,trainLengthM:train.lengthM,motionStartS:18,
            totalTimeS:18+profile.lengthM/10,durationS:profile.lengthM/10,runningTimeS:profile.lengthM/10,dwellTimeS:0,maxSpeedKmh:36,
            samples:[0,...profile.segments.map(segment=>segment.endM)].map(positionM=>({positionM,timeS:18+positionM/10,speedKmh:36,limitKmh:60})),
            cellOccupancies:profile.cells.map(cell=>({...cell,entryTimeS:18+cell.startM/10,exitTimeS:18+(cell.endM+train.lengthM)/10,releaseTimeS:23+(cell.endM+train.lengthM)/10})),jointTimes:[],warnings:[]}
        workspaces.set(id,workspace)
        calculations.set(id,{revision:1,currentWorkspaceRevision:1,isCurrent:true,savedAtUtc:row.savedAtUtc,selectedRouteId:routeId,continuationId,request,result})
        return row
    }
    const rows=[addCase('one','R1'),addCase('two','R2')]
    return {context,workspaces,calculations,rows,addCase}
}
function mock(t,f,hooks={}) {
    const old=axios.defaults.adapter, calls=[], existing=new Map(), written=new Map()
    t.after(()=>{axios.defaults.adapter=old})
    axios.defaults.adapter=async config=>{
        const body=config.data?JSON.parse(config.data):null
        calls.push({url:config.url,method:config.method,params:clone(config.params),body:clone(body)})
        const reply=data=>({config,data:clone(data),status:200,statusText:'OK',headers:{}})
        if(config.url.endsWith('/workspace/calculation')) return reply(f.calculations.get(config.params.routeCaseID))
        if(config.url.endsWith('/workspace')) return reply(f.workspaces.get(config.params.routeCaseID))
        if(config.url.endsWith('/GetStationRouteTimes')) return reply(existing.get(config.params.routeID)??[])
        if(config.url.endsWith('/SaveStationRouteTimes')) {
            if(hooks.failRoute===body.routeID) throw new Error('save failed')
            written.set(body.routeID,clone(body))
            await hooks.afterWrite?.(body)
            return reply({times:config.params.operationTimeOnly ? existing.get(body.routeID)??[] : body.times,operationTimeS:body.operationTimeS+(hooks.badAcknowledgement?1:0)})
        }
        throw new Error(`Unexpected ${config.url}`)
    }
    return {calls,existing,written}
}

test('selected saved results produce scoped operation and occupancy updates without recalculating or selecting routes',async t=>{
    const f=fixture(),server=mock(t,f),updates=await prepare(f.context,f.rows)
    assert.deepEqual(updates.map(update=>[update.routeID,update.startTimeS,update.endTimeS]),[['R1',18,28],['R2',18,28]])
    assert.deepEqual(updates.map(update=>update.times),[[{cellID:'C1',startOccupationShift:-18,endOccupationShift:25}],[{cellID:'C2',startOccupationShift:-18,endOccupationShift:25}]])
    assert.equal(server.calls.length,4)
    assert.ok(server.calls.every(call=>call.method==='get'&&call.params.calculationSetID==='traction-1'))
    assert.deepEqual(new Set(server.calls.map(call=>call.params.routeCaseID)),new Set(['one','two']))
})

test('stale, missing, changed or foreign snapshots cannot be transferred',async t=>{
    const f=fixture(),server=mock(t,f),original=clone(f.calculations.get('one'))
    for(const replacement of [null,{...original,isCurrent:false},{...original,revision:2},{...original,savedAtUtc:'2026-10-25T02:00:00Z'},{...original,selectedRouteId:'foreign'}]) {
        f.calculations.set('one',replacement)
        await assert.rejects(prepare(f.context,[f.rows[0]]))
    }
    assert.ok(server.calls.every(call=>call.method==='get'))
})

test('changed geometry or per-route inputs require recalculation before transfer',async t=>{
    const f=fixture();mock(t,f)
    f.context.document.tracks[0].lengthM=101
    await assert.rejects(prepare(f.context,[f.rows[0]]),/不一致/)
    f.context.document.tracks[0].lengthM=100
    f.workspaces.get('one').settings.signal.routeSettingS=20
    await assert.rejects(prepare(f.context,[f.rows[0]]),/不一致/)
})

test('equivalent database and DateTime JSON timestamps compare by instant despite fractional zero trimming',async t=>{
    const f=fixture();mock(t,f)
    f.rows[0].savedAtUtc='2026-10-25T01:00:00.1234000Z'
    f.calculations.get('one').savedAtUtc='2026-10-25T01:00:00.1234Z'
    assert.equal((await prepare(f.context,[f.rows[0]])).length,1)
})

test('matching duplicate route parameters are deduplicated and conflicting continuation results are rejected',async t=>{
    const f=fixture(),joined=f.addCase('joined','R1','R2');mock(t,f)
    const updates=await prepare(f.context,[f.rows[0],joined])
    assert.deepEqual(updates.map(update=>update.routeID),['R1','R2'])
    await assert.rejects(prepare(f.context,[f.rows[1],joined]),/R2.*不同的时间/)
})

test('replacement writes include operation time, preserve unknown and indirect cells and target only the chosen set',async t=>{
    const f=fixture(),server=mock(t,f),updates=await prepare(f.context,[f.rows[0]])
    const retained=[{cellID:'unknown',startOccupationShift:null,endOccupationShift:7},{cellID:'indirect',startOccupationShift:12,endOccupationShift:null}]
    server.existing.set('R1',[{cellID:'C1',startOccupationShift:0,endOccupationShift:0},...retained])
    const saved=[];await write(target,updates,()=>true,update=>saved.push(update.routeID))
    assert.deepEqual(saved,['R1'])
    const body=server.written.get('R1')
    assert.equal(body.operationTimeS,10);assert.deepEqual(body.times.slice(1),retained)
    assert.equal(body.calculationParameterSetID,'parameters-2');assert.equal(body.trainTypeID,'')
    assert.equal(body.calculationSetID,undefined);assert.equal(server.written.has('R2'),false)
    const request=server.calls.find(call=>call.method==='put')
    assert.deepEqual(request.params,{includeOperationTime:true})
})

test('all destination rows are validated before the first replacement write',async t=>{
    const f=fixture(),server=mock(t,f),updates=await prepare(f.context,f.rows)
    server.existing.set('R2',[{cellID:'C2',startOccupationShift:'invalid',endOccupationShift:0}])
    await assert.rejects(write(target,updates,()=>true,()=>{}),/Invalid stored/)
    assert.equal(server.written.size,0)
})

test('known operation time still transfers when every occupancy time is unknown, without replacing existing occupancy',async t=>{
    const f=fixture(),server=mock(t,f)
    const cell=f.calculations.get('one').result.cellOccupancies[0]
    cell.exitTimeS=null;cell.releaseTimeS=null
    const updates=await prepare(f.context,[f.rows[0]])
    assert.deepEqual(updates[0].times,[]);assert.deepEqual(updates[0].skippedCellIDs,['C1'])
    const original=[{cellID:'C1',startOccupationShift:null,endOccupationShift:7}]
    server.existing.set('R1',clone(original))
    const saved=[];await write(target,updates,()=>true,update=>saved.push(update.routeID))
    const request=server.calls.find(call=>call.method==='put')
    assert.deepEqual(request.params,{includeOperationTime:true,operationTimeOnly:true})
    assert.deepEqual(request.body.times,[]);assert.equal(request.body.operationTimeS,10)
    assert.deepEqual(server.existing.get('R1'),original);assert.deepEqual(saved,['R1'])
})

test('a saved terminal stop now transfers its occupied circuit instead of skipping it as incomplete',async t=>{
    const f=fixture(),server=mock(t,f),workspace=f.workspaces.get('one'),calculation=f.calculations.get('one')
    Object.assign(workspace.settings.run,{mode:'shunting',initialSpeedKmh:0,finalSpeedKmh:0})
    const {profile}=tractionBatchProfile(f.context,workspace)
    calculation.request=buildTractionApiRequest(profile,workspace.catalog.presets[0].parameters,workspace.settings.signal,workspace.settings.run)
    calculation.result.mode='shunting'
    calculation.result.samples[0].speedKmh=0;calculation.result.samples.at(-1).speedKmh=0
    calculation.result.samples.push({...calculation.result.samples.at(-1),timeS:88})
    Object.assign(calculation.result.cellOccupancies[0],{exitTimeS:null,releaseTimeS:null})
    const original=clone(calculation),updates=await prepare(f.context,[f.rows[0]])
    assert.deepEqual(updates[0].skippedCellIDs,[])
    assert.deepEqual(updates[0].times,[{cellID:'C1',startOccupationShift:-18,endOccupationShift:0}])
    const saved=[];await write(target,updates,()=>true,update=>saved.push(update.routeID))
    const request=server.calls.find(call=>call.method==='put')
    assert.equal(request.params.operationTimeOnly,undefined)
    assert.equal(request.body.operationTimeS,10,'the end is first arrival, before terminal dwell')
    assert.deepEqual(request.body.times,updates[0].times)
    assert.deepEqual(calculation,original,'stored physical release remains unknown')
    assert.deepEqual(saved,['R1'])
})

test('operation time can be initialized without manufacturing any circuit rows',async t=>{
    const f=fixture(),server=mock(t,f),updates=await prepare(f.context,[f.rows[0]])
    updates[0].times=[];updates[0].skippedCellIDs=['C1']
    const saved=[];await write(target,updates,()=>true,update=>saved.push(update.routeID))
    assert.deepEqual(server.calls.find(call=>call.method==='put').body.times,[])
    assert.deepEqual(saved,['R1'])
})

test('partial failure reports only acknowledged routes and retains the failed route for retry',async t=>{
    const f=fixture(),server=mock(t,f,{failRoute:'R2'}),updates=await prepare(f.context,f.rows),saved=[]
    await assert.rejects(write(target,updates,()=>true,update=>saved.push(update.routeID)),/save failed/)
    assert.deepEqual(saved,['R1']);assert.deepEqual([...server.written.keys()],['R1'])
})

test('an incomplete save acknowledgement is never reported as a successful transfer',async t=>{
    const f=fixture();mock(t,f,{badAcknowledgement:true});const updates=await prepare(f.context,[f.rows[0]]),saved=[]
    await assert.rejects(write(target,updates,()=>true,update=>saved.push(update.routeID)),/保存响应/)
    assert.deepEqual(saved,[])
})

test('a changed target or closed scope prevents subsequent route writes',async t=>{
    const f=fixture();let current=true
    const server=mock(t,f,{afterWrite:async()=>{current=false}}),updates=await prepare(f.context,f.rows),saved=[]
    await assert.rejects(write(target,updates,()=>current,update=>saved.push(update.routeID)),/目标参数集已改变/)
    assert.deepEqual(saved,['R1']);assert.deepEqual([...server.written.keys()],['R1'])
})

test('empty selections, repeated records and missing targets never write parameters',async t=>{
    const f=fixture(),server=mock(t,f)
    await assert.rejects(prepare(f.context,[]),/请选择/)
    await assert.rejects(prepare(f.context,[f.rows[0],f.rows[0]]),/请选择/)
    await assert.rejects(write({...target,calculationParameterSetID:''},[],()=>true,()=>{}),/参数集/)
    assert.equal(server.calls.length,0)
})
