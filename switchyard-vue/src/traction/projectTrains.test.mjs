import test from 'node:test'
import assert from 'node:assert/strict'
import axios from 'axios'
import { loadProjectTrains,saveProjectTrains,useProjectTrains,rebaseProjectTrainSource } from './projectTrains.ts'
import { createDefaultTrainPresetCatalog } from './trainPresets.ts'
import { defaultRunOptions,defaultSignalParameters } from './tractionCalculation.ts'
const clone=value=>structuredClone(value)
const scope={instanceID:'I',stationSchemeID:'S',calculationSetID:'set',routeCaseID:'route'}
const library=()=>({instanceID:'I',revision:2,catalog:createDefaultTrainPresetCatalog()})
const workspace=()=>({revision:7,catalogRevision:1,catalog:createDefaultTrainPresetCatalog(),settings:{selectedRouteId:'R',continuationId:'',signal:defaultSignalParameters(),run:defaultRunOptions(),distanceMode:'auto',metersPerUnit:1,defaultLimit:80},overrides:[],speedLimitPoints:[]})
function mock(t,adapter){const old=axios.defaults.adapter;axios.defaults.adapter=adapter;t.after(()=>{axios.defaults.adapter=old})}
const response=(config,data,status=200)=>({config,data:clone(data),status,statusText:'OK',headers:{}})

test('project library reads use project/station authorization scope and never carry a route or calculation set',async t=>{
    const project=library(),calls=[]
    mock(t,async config=>{calls.push(config);return response(config,project)})
    const loaded=await loadProjectTrains(scope)
    assert.deepEqual(calls[0].params,{instanceID:'I',stationSchemeID:'S'})
    assert.deepEqual(loaded,project)
    loaded.catalog.presets[0].parameters.massT=1
    assert.notEqual(project.catalog.presets[0].parameters.massT,1)
})
test('an uninitialized project stays explicit and never falls back to another route library',async t=>{
    mock(t,async config=>response(config,null,204))
    assert.equal(await loadProjectTrains(scope),null)
})
test('project saves own an immutable library snapshot and require acknowledged identity, revision and complete definitions',async t=>{
    const project=library(),catalog=createDefaultTrainPresetCatalog(),original=clone(catalog)
    mock(t,async config=>{const body=JSON.parse(config.data);assert.deepEqual(body,{instanceID:'I',stationSchemeID:'S',revision:1,catalog:original});catalog.presets[0].parameters.massT=1;return response(config,project)})
    const saved=await saveProjectTrains(scope,1,catalog)
    assert.deepEqual(saved.catalog,original)
    for(const invalid of [{...project,instanceID:'foreign'},{...project,revision:1},{...project,catalog:{...project.catalog,presets:[]}}]){
        mock(t,async config=>response(config,invalid))
        await assert.rejects(saveProjectTrains(scope,1,original))
    }
})
test('route selections remain independent while all definitions come from the one shared project library',()=>{
    const project=library(),a=workspace(),b=workspace()
    b.catalog.selectedId=b.catalog.presets[1].id
    project.catalog.presets[0].parameters.massT+=10
    const first=useProjectTrains(a,project),second=useProjectTrains(b,project)
    assert.equal(first.catalogRevision,2);assert.equal(second.catalogRevision,2)
    assert.equal(first.catalog.selectedId,a.catalog.selectedId);assert.equal(second.catalog.selectedId,b.catalog.selectedId)
    assert.deepEqual(first.catalog.presets,second.catalog.presets)
    first.catalog.presets[0].parameters.massT=1
    assert.notEqual(second.catalog.presets[0].parameters.massT,1)
    assert.notEqual(project.catalog.presets[0].parameters.massT,1)
    assert.deepEqual(first.settings,a.settings)
})
test('batch source rebasing mirrors selective server invalidation and preserves unrelated route fields',()=>{
    const project=library(),a=workspace(),b=workspace()
    b.catalog.selectedId=b.catalog.presets[1].id
    project.catalog.presets[0].parameters.massT+=10
    const changed=rebaseProjectTrainSource(a,project),unchanged=rebaseProjectTrainSource(b,project)
    assert.equal(changed.revision,8);assert.equal(unchanged.revision,7)
    assert.deepEqual(changed.settings,a.settings)
    assert.equal(a.revision,7)
    const renamed=library();renamed.catalog.presets[0].name='Renamed'
    assert.equal(rebaseProjectTrainSource(a,renamed).revision,7)
})
test('deleting a chosen train reassigns only that route to the project default and invalidates its result',()=>{
    const project=library(),a=workspace(),chosen=a.catalog.selectedId
    project.catalog.presets=project.catalog.presets.filter(p=>p.id!==chosen)
    project.catalog.selectedId=project.catalog.presets[0].id
    const next=rebaseProjectTrainSource(a,project)
    assert.equal(next.catalog.selectedId,project.catalog.selectedId);assert.equal(next.revision,8)
})
