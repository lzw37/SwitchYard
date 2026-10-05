import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { runInNewContext } from 'node:vm';
import test from 'node:test';
import { parse, compileScript, compileTemplate } from '@vue/compiler-sfc';
import ts from 'typescript';
import * as vue from 'vue';
import * as bufferStopStyles from '../src/assets/stationLayoutBufferStopStyles.js';

const read = path => readFileSync(new URL(path, import.meta.url), 'utf8');
const plain = value => JSON.parse(JSON.stringify(value));
function evaluate(source, require = () => { throw new Error('Unexpected import'); }) {
    const exports = {};
    const code = ts.transpileModule(source, {
        compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
    }).outputText;
    runInNewContext(code, {
        exports, require, console,
        window: { addEventListener() {}, removeEventListener() {} },
        document: { createElement: () => ({ getContext: () => ({ measureText: text => ({ width: text.length * 8 }) }) }) },
    });
    return exports;
}
const messages = evaluate(read('../src/messages.ts'));
const signalStyles = evaluate(read('../src/assets/stationLayoutSignalStyles.js')
    .replace(/import\.meta\.glob\([\s\S]*?\}\)/g, '({})'));
const { descriptor } = parse(read('../src/components/StationLayoutEditor.vue'));
const compiled = compileScript(descriptor, { id: 'layout-json-editor-test' });
const component = evaluate(compiled.content, name => {
    if (name === 'vue') return { ...vue, onMounted() {}, onBeforeUnmount() {} };
    if (name === '../messages') return messages;
    if (name === '../assets/stationLayoutSignalStyles') return signalStyles;
    if (name === '../assets/stationLayoutBufferStopStyles') return bufferStopStyles;
    throw new Error(`Unexpected import: ${name}`);
}).default;
const template = compileTemplate({
    source: descriptor.template.content,
    filename: 'StationLayoutEditor.vue', id: 'layout-json-editor-test',
    compilerOptions: { bindingMetadata: compiled.bindings },
});
assert.deepEqual(template.errors, []);
const render = evaluate(template.code, name => {
    assert.equal(name, 'vue');
    return { ...vue, withDirectives: node => node, resolveDynamicComponent: tag => tag };
}).render;

function documentFixture() {
    const extension = () => ({ future: { nullable: null, values: [false, 0, '', '扩展值'], nested: { version: 2 } } });
    return {
        metadata: {
            stationSchemeID: 'scheme-original', revision: 7, latestElementID: 0,
            gridSettings: { showGrid: false, spacing: 13.125, originX: -1.1234567890123, originY: 27.6543210987654, ...extension() },
            ...extension(),
        },
        vendorData: extension(),
        stationName: '既有站场方案',
        tracks: [{ id: '0', name: '', fromNodeID: '2', toNodeID: '3', x1: -20.1234567890123, y1: 19, x2: 400, y2: 200, arrowType: 'LR', ...extension() }],
        curves: [{ id: '1', nodeID: '2', tangentLinkID1: '0', tangentLinkID2: '0', radius: 77.123456789, angle: 122.987654321,
            tangentDistance: 13.1111111, start: { x: -3.111111, y: 30, ...extension() }, end: { x: 15, y: 38 }, center: { x: 40, y: 80 },
            largeArcFlag: 1, sweepFlag: 0, ...extension() }],
        nodes: [
            { id: '2', x: -20.1234567890123, y: 19, adjacentLineIDList: ['persisted-topology', '0'], ...extension() },
            { id: '3', x: 400, y: 200, adjacentLineIDList: [], ...extension() },
        ],
        // Intentionally offset from the bound node. Loading an archive must not
        // replace explicit equipment coordinates or persisted switch branches.
        signals: [{ id: '4', name: ' 信号 S1 ', bindingNodeID: '2', position: { x: 101.125, y: 102.25, ...extension() }, direction: 'w', type: 'future-signal', ...extension() }],
        insulationJoints: [{ id: '5', BindingNodeID: '2', position: { x: 105, y: 106 }, ...extension() }],
        bufferStops: [{ id: '6', bindingNodeID: '3', position: { x: 201, y: 202 }, direction: 'future-direction', type: 'future-buffer', ...extension() }],
        platforms: [{ id: '7', name: '站台', x: 103, y: 170, width: 200.123456789, height: 40, ...extension() }],
        switches: [{ id: '8', name: '', bindingNodeID: '2', position: { x: 300.3, y: 301.4 }, branchVectorList: [{ lineID: '0', x: 3.33, y: 4.44, ...extension() }], ...extension() }],
        annotations: [{ id: '9', text: '线路说明\n第二行', position: { x: 113, y: 114, ...extension() }, fontFamily: 'SimSun', fontSize: 13.3333333,
            fontWeight: '700', fontStyle: 'italic', angle: -37.7777777, textColor: '#12aBcD', ...extension() }],
        cells: [{ id: '10', name: '轨道电路', linkIDList: ['0'], nodeIDList: ['2', '3'], sourceOnly: { keep: true }, ...extension() }],
    };
}
function fixture(t) {
    const defaults = Object.fromEntries(Object.entries(component.props).map(([key, spec]) => [
        key, typeof spec.default === 'function' && spec.type !== Function ? spec.default() : spec.default,
    ]));
    const props = vue.reactive({ ...defaults, showGrid: false, gridSpacing: 13.125,
        cells: [{ id: 'host-cell', name: 'Host projection', linkIDList: '0' }] });
    const scope = vue.effectScope();
    const state = scope.run(() => component.setup(props, { expose() {}, emit() {} }));
    t.after(() => scope.stop());
    return { state, props, snapshot: () => JSON.parse(state.buildJsonData()),
        render: () => render(vue.proxyRefs(state), [], props, vue.proxyRefs(state), {}, {}) };
}

test('all ten collections, metadata, extensions and full grid precision survive repeated archive round trips', t => {
    const { state, snapshot, render } = fixture(t);
    const original = documentFixture();
    for (let cycle = 0; cycle < 6; cycle++) {
        state.loadDataFromJson(cycle === 0 ? original : snapshot(), { preserveDocument: true });
        assert.deepEqual(snapshot(), original, `Round trip ${cycle + 1} must preserve every explicit field`);
        assert.doesNotThrow(render, 'The real compiled template renders the archived document');
    }
    assert.equal(state.grid.originX, original.metadata.gridSettings.originX);
    assert.equal(state.grid.originY, original.metadata.gridSettings.originY);
});

test('import and history own deep copies, including raw cells and nested extension fields', t => {
    const { state, props, snapshot } = fixture(t);
    const source = documentFixture();
    const expected = plain(source);
    state.loadDataFromJson(source, { preserveDocument: true });
    source.metadata.future.nested.version = 9;
    source.vendorData.future.values.push('mutated');
    source.curves[0].start.future.values[0] = true;
    source.signals[0].position.x = 999;
    source.switches[0].branchVectorList[0].x = 999;
    source.cells[0].sourceOnly.keep = false;
    props.cells[0].name = 'Changed projection';
    assert.deepEqual(snapshot(), expected);
    const exported = snapshot();
    exported.annotations[0].future.nested.version = 99;
    exported.cells[0].linkIDList.push('unrelated');
    assert.deepEqual(snapshot(), expected);
});

test('failed imports leave geometry, metadata, selection and both history stacks untouched', t => {
    const { state, snapshot } = fixture(t);
    state.loadDataFromJson(documentFixture(), { preserveDocument: true });
    state.selectedLineIds.value = new Set(['0']);
    state.executeMutation(() => { state.tracks.value[0].name = 'temporary'; });
    state.revoke();
    const before = snapshot();
    const history = plain([state.finishedCmdList.value, state.revokedCmdList.value]);
    const circular = documentFixture();
    circular.vendorData.circular = circular;
    for (const invalid of [null, [], { metadata: [] }, { tracks: {} }, { curves: [null] }, { cells: ['invalid'] }, circular]) {
        assert.throws(() => state.loadDataFromJson(invalid, { preserveDocument: true, resetHistory: true }));
        assert.deepEqual(snapshot(), before);
        assert.deepEqual(plain([state.finishedCmdList.value, state.revokedCmdList.value]), history);
        assert.deepEqual([...state.selectedLineIds.value], ['0']);
    }
    assert.throws(() => state.loadDataFromJson({ signals: {}, tracks: [] }));
    assert.deepEqual(snapshot(), before, 'Legacy preparation failures are also atomic');
});

test('new IDs skip every imported collection and host cell without changing imported counters prematurely', t => {
    const { state, props, snapshot } = fixture(t);
    for (const counter of [0, undefined, '0', 2147483647, Number.MAX_SAFE_INTEGER]) {
        const source = documentFixture();
        if (counter === undefined) delete source.metadata.latestElementID;
        else source.metadata.latestElementID = counter;
        props.cells = [{ id: '11' }];
        state.loadDataFromJson(source, { preserveDocument: true });
        assert.deepEqual(snapshot(), source);
        assert.equal(state.nextId(), '12');
        assert.equal(state.nextId(), '13');
        assert.equal(snapshot().metadata.latestElementID, 14);
    }
});

test('ID allocation wraps at the persisted Int32 counter limit without collisions or an unsavable counter', t => {
    const { state, snapshot } = fixture(t);
    const source = documentFixture();
    source.metadata.latestElementID = 2147483646;
    state.loadDataFromJson(source, { preserveDocument: true });
    assert.deepEqual(snapshot(), source, 'An untouched imported counter remains unchanged');
    assert.equal(state.nextId(), '2147483646');
    assert.equal(snapshot().metadata.latestElementID, 2147483647);
    assert.equal(state.nextId(), '11', 'Wrapping skips existing IDs across all ten collections');
    assert.equal(snapshot().metadata.latestElementID, 12);

    source.annotations.push({ id: '2147483646', text: 'Reserved near the counter limit' });
    state.loadDataFromJson(source, { preserveDocument: true });
    assert.equal(state.nextId(), '11', 'A collision at the last usable counter also wraps safely');
    assert.equal(snapshot().metadata.latestElementID, 12);
});

test('undo and redo restore metadata, cells, extensions and geometry from the corresponding document', t => {
    const { state, snapshot } = fixture(t);
    const first = documentFixture();
    const second = documentFixture();
    second.metadata.stationSchemeID = 'other-scheme';
    second.metadata.gridSettings.originX = 0.987654321012345;
    second.metadata.future.values = ['another'];
    second.vendorData = { secondOnly: true };
    second.cells[0].sourceOnly.keep = false;
    second.curves[0].radius = 23.987654321;
    state.loadDataFromJson(first, { preserveDocument: true });
    state.loadDataFromJson(second, { preserveDocument: true });
    state.revoke();
    assert.deepEqual(snapshot(), first);
    state.redo();
    assert.deepEqual(snapshot(), second);
    state.executeMutation(() => { state.signals.value[0].name = 'changed'; });
    state.revoke();
    assert.deepEqual(snapshot(), second, 'Undo must not sync bound positions or regenerate switch vectors');
});

test('explicit grid edits change their own fields while preserving grid extensions and untouched precision', t => {
    const { state, props, snapshot } = fixture(t);
    const source = documentFixture();
    state.loadDataFromJson(source, { preserveDocument: true });
    props.showGrid = true;
    props.gridSpacing = 24;
    const expected = plain(source);
    expected.metadata.gridSettings.showGrid = true;
    expected.metadata.gridSettings.spacing = 24;
    assert.deepEqual(snapshot(), expected);
    state.grid.originX = -123.123456789012345;
    expected.metadata.gridSettings.originX = state.grid.originX;
    assert.deepEqual(snapshot(), expected);
});

test('missing optional render fields remain absent and imports can explicitly reset undo history', t => {
    const { state, snapshot, render } = fixture(t);
    const minimal = documentFixture();
    for (const name of ['signals', 'insulationJoints', 'bufferStops', 'switches', 'annotations']) {
        delete minimal[name][0].position;
    }
    delete minimal.switches[0].branchVectorList;
    delete minimal.curves[0].start;
    state.loadDataFromJson(minimal, { preserveDocument: true, resetHistory: true });
    assert.doesNotThrow(render);
    assert.deepEqual(snapshot(), minimal);
    assert.equal(state.finishedCmdList.value.length, 0);
    assert.equal(state.revokedCmdList.value.length, 0);
    state.revoke();
    assert.deepEqual(snapshot(), minimal);
});

test('clearing resets document metadata, cells, root extensions, grid origins and ID counter', t => {
    const { state, snapshot } = fixture(t);
    state.loadDataFromJson(documentFixture(), { preserveDocument: true });
    state.clearElements();
    const cleared = snapshot();
    assert.equal(cleared.vendorData, undefined);
    assert.equal(cleared.stationName, undefined);
    assert.equal(cleared.metadata.stationSchemeID, undefined);
    assert.equal(cleared.metadata.latestElementID, 0);
    assert.equal(cleared.metadata.gridSettings.originX, 0);
    assert.equal(cleared.metadata.gridSettings.originY, 0);
    assert.equal(cleared.metadata.gridSettings.future, undefined);
    for (const name of ['tracks', 'curves', 'nodes', 'signals', 'insulationJoints', 'bufferStops', 'platforms', 'switches', 'annotations', 'cells']) {
        assert.deepEqual(cleared[name], []);
    }
});
