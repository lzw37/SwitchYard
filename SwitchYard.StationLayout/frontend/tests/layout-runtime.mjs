import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { runInNewContext } from 'node:vm';
import { parse, compileScript } from '@vue/compiler-sfc';
import ts from 'typescript';
import * as vue from 'vue';

const resizeObservers = [];
class TestResizeObserver {
    constructor(callback) {
        this.callback = callback;
        this.targets = [];
        this.disconnected = false;
        resizeObservers.push(this);
    }
    observe(target) { this.targets.push(target); }
    disconnect() { this.disconnected = true; }
    // Permit a queued callback even after disconnect, as browsers may already
    // have scheduled one. The component must also guard the pending fit itself.
    notify() { this.callback(this.targets.map(target => ({ target })), this); }
}

function evaluate(source, require = () => { throw new Error('Unexpected runtime import'); }) {
    const exports = {};
    const code = ts.transpileModule(source, {
        compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
    }).outputText;
    runInNewContext(code, { exports, require, console, ResizeObserver: TestResizeObserver });
    return exports;
}
const messages = evaluate(readFileSync(new URL('../src/messages.ts', import.meta.url), 'utf8'));
const layoutJson = evaluate(readFileSync(new URL('../src/layoutJson.ts', import.meta.url), 'utf8'));
const { descriptor } = parse(readFileSync(new URL('../src/StationLayout.vue', import.meta.url), 'utf8'));
const script = compileScript(descriptor, { id: 'layout-runtime-test' }).content;
const errors = [];
// Run the real component setup with Vue refs and computed values. Rendering and
// browser event registration are omitted; the gateway and canvas are test doubles.
const component = evaluate(script, name => {
    if (name === 'vue') return { ...vue, onMounted() {}, onBeforeUnmount() {} };
    if (name === 'element-plus') return {
        ElMessage: { error: text => errors.push(text), warning() {}, info() {}, success() {} },
        ElMessageBox: { confirm: async () => {} },
    };
    if (name === './messages') return messages;
    if (name === './layoutJson') return layoutJson;
    if (name === './assets/stationLayoutBufferStopStyles') return {
        DEFAULT_BUFFER_STOP_DIRECTION: 'left', DEFAULT_BUFFER_STOP_TYPE: 'normal',
        bufferStopDirectionOptions: [], bufferStopTypeOptions: [],
    };
    if (name === './assets/stationLayoutSignalStyles') return {
        DEFAULT_SIGNAL_TYPE: 'DepartureSignal', normalizeSignalType: value => value,
        signalTypeMenuOptions: [], signalTypeOptions: [],
    };
    if (name.endsWith('.vue') || name === '@element-plus/icons-vue') return {};
    throw new Error(`Unexpected runtime import: ${name}`);
}).default;

const locale = vue.ref('en');
const translators = { zh: messages.createStationLayoutTranslator('zh'), en: messages.createStationLayoutTranslator('en') };
const document = {
    metadata: {
        stationSchemeID: 'scheme', revision: 1,
        displayStyles: { switchName: { fontSize: 18, color: '#123456' }, track: { strokeWidth: 3 } },
    },
    tracks: ['L1', 'L2', 'L3', 'L4'].map((id, i) => ({ id, name: id, x1: i * 100, y1: 0, x2: (i + 1) * 100, y2: 0 })),
    nodes: [], cells: [],
};
let loaded, saved;
const props = vue.reactive({
    selectedInstanceId: 'station', readonly: false,
    translate: (key, parameters) => translators[locale.value](key, parameters),
    gateway: {
        getJson: async () => document,
        getStationSchemes: async () => [{ id: 'scheme', name: 'Scheme', revision: 1 }],
        saveJson: async request => { saved = JSON.parse(request.json); return { stationSchemeId: 'scheme', revision: 2 }; },
    },
});
const scope = vue.effectScope();
try {
    const state = scope.run(() => component.setup(props, { expose() {}, emit() {} }));
    state.stationLayoutEditorRef.value = {
        loadDataFromJson: value => { loaded = value; },
        buildJsonData: () => JSON.stringify(document),
    };
    for (const language of ['en', 'zh']) {
        locale.value = language;
        assert.equal(state.layoutTextStyleRows.value[0].label, language === 'en' ? 'Switch number' : '道岔编号');
        state.getData();
        await new Promise(resolve => setImmediate(resolve));
        assert.equal(errors.length, 0, errors.join('\n'));
        assert.ok(loaded, 'The layout must reach the canvas after loading its display styles');
        assert.equal(state.loadingData.value, false);
        assert.equal(state.layoutDisplayStyles.value.switchName.fontSize, 18);
        assert.equal(state.layoutDisplayStyles.value.switchName.fontFamily, 'Arial');
        assert.equal(state.layoutDisplayStyles.value.track.strokeWidth, 3);

        assert.equal(await state.saveData({ silent: true }), true);
        assert.equal(errors.length, 0, errors.join('\n'));
        assert.equal(saved.metadata.displayStyles.switchName.color, '#123456');
        assert.equal(saved.metadata.displayStyles.track.strokeWidth, 3);
    }
    state.applyLayoutDisplayStyles();
    assert.equal(state.layoutDisplayStyles.value.switchName.fontSize, 8);
    assert.equal(state.layoutDisplayStyles.value.track.strokeWidth, 2);
    console.log('Layout load/save and display-style defaults passed in English and Chinese.');

    // Invoke the listener wired to the real canvas, so a missing parent binding
    // cannot pass by calling an otherwise unused setup function directly.
    function findEditor(node) {
        if (node.tag === 'StationLayoutEditor') return node;
        return (node.children || []).map(findEditor).find(Boolean);
    }
    const editor = findEditor(descriptor.template.ast);
    const renameBinding = editor?.props.find(prop => prop.type === 7 && prop.name === 'on'
        && prop.arg?.content === 'cell-rename');
    assert.ok(renameBinding, 'The layout must receive Cell renames from its canvas');
    const renameFromCanvas = payload => state[renameBinding.exp.content](payload);
    state.setCellsFromLayout({ cells: [
        { id: 'c1', name: 'First cell', linkIDList: 'L1' },
        { id: 'c2', name: 'Second cell', linkIDList: 'L2' },
    ] });
    const cell = id => state.cells.value.find(item => item.id === id);

    state.cellForm.value.name = 'Unsubmitted old name';
    state.cellForm.value.linkIDList = 'L1,L3';
    renameFromCanvas({ id: 'c1', name: '  Renamed first  ' });
    assert.equal(cell('c1').name, 'Renamed first');
    assert.equal(state.cellForm.value.name, 'Renamed first');
    assert.equal(state.layoutEditorCells.value[0].name, 'Renamed first');
    assert.equal(state.cellForm.value.linkIDList, 'L1,L3');
    assert.equal(state.selectedCellId.value, 'c1');

    state.cellForm.value.name = 'Draft first';
    state.cellForm.value.linkIDList = 'L1,L3,L4';
    renameFromCanvas({ id: 'c2', name: 'Renamed second' });
    assert.equal(cell('c1').name, 'Draft first', 'Renaming another Cell preserves the selected Cell draft');
    assert.equal(cell('c2').name, 'Renamed second');
    assert.equal(state.cellForm.value.name, 'Draft first');
    assert.equal(state.selectedCellId.value, 'c1', 'Renaming does not change the current selection');
    assert.equal(saved.cells.length, 0, 'Renaming is local until the existing save action runs');
    assert.equal(await state.saveData({ silent: true }), true);
    assert.equal(saved.cells.find(item => item.id === 'c1').name, 'Draft first');
    assert.equal(saved.cells.find(item => item.id === 'c1').linkIDList, 'L1,L3,L4');
    assert.equal(saved.cells.find(item => item.id === 'c2').name, 'Renamed second');

    state.cellForm.value.name = 'Still editing';
    for (const payload of [null, { id: 'missing', name: 'Ignored' }, { id: 'c1' }]) renameFromCanvas(payload);
    assert.equal(cell('c1').name, 'Draft first', 'Invalid targets do not commit an unrelated form draft');
    for (const blocked of [props, state.loadingData, state.savingData]) {
        if (blocked === props) props.readonly = true;
        else blocked.value = true;
        renameFromCanvas({ id: 'c1', name: 'Blocked' });
        assert.equal(cell('c1').name, 'Draft first');
        assert.equal(state.cellForm.value.name, 'Still editing');
        if (blocked === props) props.readonly = false;
        else blocked.value = false;
    }
    state.cellForm.value.id = 'c2';
    renameFromCanvas({ id: 'c2', name: 'Blocked by duplicate Cell ID' });
    assert.equal(cell('c2').name, 'Renamed second', 'An invalid current Cell form blocks the rename');
    state.cellForm.value.id = 'c1';

    state.createCell();
    const temporaryID = state.selectedCellId.value;
    renameFromCanvas({ id: temporaryID, name: 'New cell name' });
    assert.equal(state.selectedCellId.value, temporaryID);
    assert.equal(state.cellForm.value.name, 'New cell name');
    const pending = state.buildCellsForJson().find(item => item.name === 'New cell name');
    assert.equal(pending.id, '', 'Renaming a new Cell keeps backend ID assignment intact');
    assert.equal(errors.length, 0, errors.join('\n'));
    console.log('Canvas Cell renames preserve drafts, selection, save payloads and pending IDs; readonly and busy states reject edits.');
} finally {
    scope.stop();
}

const flushLayout = async () => {
    await vue.nextTick();
    await new Promise(resolve => setImmediate(resolve));
    await vue.nextTick();
};
const plain = value => JSON.parse(JSON.stringify(value));
function deferred() {
    let resolve;
    const promise = new Promise(accept => { resolve = accept; });
    return { promise, resolve };
}
function layoutForScheme(id) {
    return {
        metadata: { stationSchemeID: id, revision: 1 },
        tracks: [{ id: `${id}-link`, name: id, fromNodeID: `${id}-start`, toNodeID: `${id}-end` }],
        nodes: [], cells: [{ id: `${id}-cell`, name: id, linkIDList: `${id}-link` }],
    };
}
function fitFixture({ width = 1596, height = 846, stationSchemeId } = {}) {
    const effectScope = vue.effectScope();
    const requests = [];
    const loadedLayouts = [];
    const fittedRects = [];
    const scrolledRects = [];
    const emitted = [];
    const observerStart = resizeObservers.length;
    const frame = vue.markRaw({ clientWidth: width, clientHeight: height });
    const props = vue.reactive({
        selectedInstanceId: 'station-fit', stationSchemeId, readonly: false,
        translate: translators.en,
        gateway: {
            getStationSchemes: async () => [],
            getJson(request) {
                const response = deferred();
                requests.push({ ...request, ...response });
                return response.promise;
            },
        },
    });
    const state = effectScope.run(() => component.setup(props, { expose() {}, emit: (...args) => emitted.push(args) }));
    state.stationLayoutEditorFrameRef.value = frame;
    state.stationLayoutEditorRef.value = {
        clearElements() { loadedLayouts.length = 0; },
        loadDataFromJson(value) { loadedLayouts.push(plain(value)); },
        getFullViewRect() {
            assert.ok(loadedLayouts.length > 0, 'Only fit geometry after it has loaded into the canvas');
            const rect = { minX: -50, minY: -30, maxX: 950, maxY: 470 };
            fittedRects.push(rect);
            return rect;
        },
        scrollDataRectIntoView(rect, options) { scrolledRects.push({ rect: plain(rect), options: plain(options) }); },
    };
    return { state, props, frame, requests, loadedLayouts, fittedRects, scrolledRects, emitted,
        observers: () => resizeObservers.slice(observerStart), stop: () => effectScope.stop() };
}

{
    const fixture = fitFixture({ stationSchemeId: 'shared-scheme' });
    const { state, props, requests, loadedLayouts, emitted } = fixture;
    try {
        assert.equal(state.currentStationSchemeId.value, 'shared-scheme');
        state.getData();
        assert.equal(requests[0].stationSchemeId, 'shared-scheme');
        props.stationSchemeId = 'other-module-scheme';
        await flushLayout();
        assert.equal(requests[1].stationSchemeId, 'other-module-scheme');
        requests[0].resolve(layoutForScheme('shared-scheme'));
        requests[1].resolve(layoutForScheme('other-module-scheme'));
        await flushLayout();
        assert.equal(state.currentStationSchemeId.value, 'other-module-scheme');
        assert.equal(loadedLayouts.length, 1, 'Ignore a layout response for an earlier shared selection');
        assert.equal(loadedLayouts[0].metadata.stationSchemeID, 'other-module-scheme');
        state.currentStationSchemeId.value = 'local-selection';
        assert.deepEqual(emitted.at(-1), ['update:stationSchemeId', 'local-selection']);
        props.stationSchemeId = '';
        await flushLayout();
        assert.equal(loadedLayouts.length, 0, 'An explicitly cleared shared scheme clears the canvas');
        assert.deepEqual(emitted.at(-1), ['update:stationSchemeId', '']);
        props.selectedInstanceId = 'another-instance';
        props.stationSchemeId = 'remembered-scheme';
        await flushLayout();
        assert.equal(state.currentStationSchemeId.value, 'remembered-scheme');
        assert.equal(requests.at(-1).instanceId, 'another-instance');
        assert.equal(requests.at(-1).stationSchemeId, 'remembered-scheme');
    } finally {
        fixture.stop();
    }
}

{
    const fixture = fitFixture({ stationSchemeId: 'deleted-scheme' });
    const { state, props, emitted } = fixture;
    const deletion = deferred();
    props.gateway.deleteStationScheme = () => deletion.promise;
    props.gateway.getStationSchemes = async () => [{ id: 'new-selection', name: 'New selection' }];
    try {
        const pending = state.deleteStationScheme({ id: 'deleted-scheme' });
        await flushLayout();
        props.stationSchemeId = 'new-selection';
        await flushLayout();
        deletion.resolve();
        await pending;
        assert.equal(state.currentStationSchemeId.value, 'new-selection');
        assert.ok(!emitted.some(([, scheme]) => scheme === ''), 'Deleting the previous scheme must not clear a newer shared selection');
    } finally {
        fixture.stop();
    }
}

{
    const fixture = fitFixture();
    const { state, requests, fittedRects, scrolledRects } = fixture;
    try {
        state.getData({ stationSchemeId: 'visible' });
        assert.equal(state.loadingData.value, true);
        requests[0].resolve(layoutForScheme('visible'));
        await flushLayout();
        assert.equal(state.loadingData.value, false);
        assert.equal(fittedRects.length, 1);
        assert.equal(scrolledRects.length, 1);
        assert.deepEqual(scrolledRects[0], {
            rect: { minX: -50, minY: -30, maxX: 950, maxY: 470 },
            options: { screenMargin: 48, padding: 160 },
        });
        assert.equal(state.layoutScaleX.value, 1.5, 'Fit uses the new layout bounds and current viewport');
        assert.equal(state.layoutScaleY.value, 1.5);
        state.layoutScaleX.value = 2.35;
        state.layoutScaleY.value = 1.75;
        fixture.frame.clientWidth = 1600;
        fixture.frame.clientHeight = 1000;
        for (const observer of fixture.observers()) observer.notify();
        await flushLayout();
        assert.equal(fittedRects.length, 1, 'A later resize must not auto-fit an already fitted layout');
        assert.equal(scrolledRects.length, 1);
        assert.equal(state.layoutScaleX.value, 2.35);
        assert.equal(state.layoutScaleY.value, 1.75);
        assert.ok(fixture.observers().every(observer => observer.disconnected));
    } finally {
        fixture.stop();
    }
}

{
    const fixture = fitFixture({ width: 0, height: 0 });
    const { state, requests, fittedRects, scrolledRects, frame } = fixture;
    try {
        state.getData({ stationSchemeId: 'hidden' });
        requests[0].resolve(layoutForScheme('hidden'));
        await flushLayout();
        assert.equal(scrolledRects.length, 0, 'A hidden viewport cannot be fitted yet');
        assert.equal(state.layoutScaleX.value, 1);
        assert.equal(state.layoutScaleY.value, 1);
        assert.equal(state.loadingData.value, false, 'Waiting for visibility does not keep data loading');
        assert.equal(fixture.observers().length, 1);
        const observer = fixture.observers()[0];
        assert.ok(observer.targets.includes(frame));
        assert.equal(observer.disconnected, false);
        frame.clientWidth = 1096;
        observer.notify();
        await flushLayout();
        assert.equal(scrolledRects.length, 0, 'Both viewport dimensions must be usable');
        frame.clientHeight = 596;
        observer.notify();
        await flushLayout();
        assert.equal(fittedRects.length, 1);
        assert.equal(scrolledRects.length, 1);
        assert.equal(observer.disconnected, true);
        state.layoutScaleX.value = 3;
        state.layoutScaleY.value = 2;
        frame.clientWidth = 2000;
        frame.clientHeight = 1500;
        observer.notify();
        await flushLayout();
        assert.equal(scrolledRects.length, 1, 'A queued notification after disconnect cannot fit twice');
        assert.equal(fittedRects.length, 1);
        assert.equal(state.layoutScaleX.value, 3);
        assert.equal(state.layoutScaleY.value, 2);
    } finally {
        fixture.stop();
    }
}
console.log('Loaded layouts fit once after rendering, wait for a visible viewport, and preserve later manual zoom.');

for (const oldRespondsFirst of [true, false]) {
    const fixture = fitFixture();
    const { state, requests, loadedLayouts, fittedRects, scrolledRects } = fixture;
    try {
        state.getData({ stationSchemeId: 'old' });
        state.getData({ stationSchemeId: 'latest' });
        assert.equal(requests.length, 2);
        if (oldRespondsFirst) {
            requests[0].resolve(layoutForScheme('old'));
            await flushLayout();
            assert.equal(state.loadingData.value, true, 'The obsolete response cannot finish the latest request');
            assert.equal(loadedLayouts.length, 0);
            assert.equal(fittedRects.length, 0);
            assert.equal(scrolledRects.length, 0);
        }
        requests[1].resolve(layoutForScheme('latest'));
        await flushLayout();
        assert.equal(state.loadingData.value, false);
        assert.equal(state.currentStationSchemeId.value, 'latest');
        assert.deepEqual(loadedLayouts.map(layout => layout.metadata.stationSchemeID), ['latest']);
        assert.equal(state.cells.value[0].id, 'latest-cell');
        assert.equal(fittedRects.length, 1);
        assert.equal(scrolledRects.length, 1);
        if (!oldRespondsFirst) {
            state.layoutScaleX.value = 2.8;
            state.layoutScaleY.value = 1.4;
            requests[0].resolve(layoutForScheme('old'));
            await flushLayout();
            assert.equal(state.currentStationSchemeId.value, 'latest');
            assert.deepEqual(loadedLayouts.map(layout => layout.metadata.stationSchemeID), ['latest']);
            assert.equal(state.cells.value[0].id, 'latest-cell');
            assert.equal(fittedRects.length, 1);
            assert.equal(scrolledRects.length, 1);
            assert.equal(state.layoutScaleX.value, 2.8);
            assert.equal(state.layoutScaleY.value, 1.4);
        }
    } finally {
        fixture.stop();
    }
}
assert.equal(errors.length, 0, errors.join('\n'));
console.log('Concurrent scheme loads ignore obsolete responses without overwriting geometry, fitting twice, or ending newer loading.');
