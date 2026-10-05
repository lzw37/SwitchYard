import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { runInNewContext } from 'node:vm';
import { parse, compileScript } from '@vue/compiler-sfc';
import ts from 'typescript';
import * as vue from 'vue';
import * as bufferStopStyles from '../src/assets/stationLayoutBufferStopStyles.js';

const read = path => readFileSync(new URL(path, import.meta.url), 'utf8');
const plain = value => JSON.parse(JSON.stringify(value));
const notices = [];
function evaluate(source, require = () => { throw new Error('Unexpected import'); }) {
    const exports = {};
    runInNewContext(ts.transpileModule(source, {
        compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
    }).outputText, {
        exports, require, console,
        window: { addEventListener() {}, removeEventListener() {} },
        document: { createElement: () => ({ getContext: () => ({ measureText: text => ({ width: text.length * 8 }) }) }) },
    });
    return exports;
}
const messages = evaluate(read('../src/messages.ts'));
const codec = evaluate(read('../src/layoutJson.ts'));
const signalStyles = evaluate(read('../src/assets/stationLayoutSignalStyles.js')
    .replace(/import\.meta\.glob\([\s\S]*?\}\)/g, '({})'));
function component(path) {
    const { descriptor } = parse(read(path));
    return evaluate(compileScript(descriptor, { id: path }).content, name => {
        if (name === 'vue') return { ...vue, onMounted() {}, onBeforeUnmount() {} };
        if (name.endsWith('/messages')) return messages;
        if (name.endsWith('/layoutJson')) return codec;
        if (name.endsWith('/assets/stationLayoutSignalStyles')) return signalStyles;
        if (name.endsWith('/assets/stationLayoutBufferStopStyles')) return bufferStopStyles;
        if (name === 'element-plus') return {
            ElMessage: Object.fromEntries(['error', 'warning', 'info', 'success'].map(kind => [kind, text => notices.push({ kind, text })])),
            ElMessageBox: { confirm: async () => {} },
        };
        if (name.endsWith('.vue') || name === '@element-plus/icons-vue') return {};
        throw new Error(`Unexpected import: ${name}`);
    }).default;
}
const pageComponent = component('../src/StationLayout.vue');
const editorComponent = component('../src/components/StationLayoutEditor.vue');
const defaultProps = component => Object.fromEntries(Object.entries(component.props).map(([key, spec]) => [
    key, typeof spec.default === 'function' && spec.type !== Function ? spec.default() : spec.default,
]));
const scope = vue.effectScope();
const original = {
    metadata: {
        instanceID: 'source-instance', stationSchemeID: 'source-scheme', revision: 99, latestElementID: 1,
        coordinateTransform: { applied: true, scale: 1.125, padding: 17, minX: -123.456789, minY: 2 },
        displayStyles: { track: { strokeWidth: 3.125, color: '#AABBCC', dash: [1, 2] }, customStyle: { enabled: true } },
        gridSettings: { spacing: 31.125, showGrid: false, originX: -0.123456789, originY: 12.3456789, customGrid: '保留' },
        extra: { chinese: '站场 🚆', flags: [false, null, 0] },
    },
    nodes: [{ id: '1', x: -10.123456789, y: 10.125, adjacentLineIDList: ['3'] }, { id: '2', x: 250.123456789, y: 10.125, adjacentLineIDList: ['3'] }],
    tracks: [{ id: '3', name: '  一股道  ', x1: -10.123456789, y1: 10.125, x2: 250.123456789, y2: 10.125,
        fromNodeID: '1', toNodeID: '2', arrowDirection: 'right', arrowType: 'single', custom: ['a', 0] }],
    curves: [{ id: '4', nodeID: '1', tangentLinkID1: '3', tangentLinkID2: '3', radius: 12.3456789, angle: 110.25,
        tangentDistance: 1.25, start: { x: 5.25, y: 6.125 }, end: { x: 7.5, y: 8.75 }, center: { x: 8, y: 9 },
        largeArcFlag: 0, sweepFlag: 1, custom: { curve: true } }],
    signals: [{ id: '5', name: '', type: 'DepartureSignal', position: { x: 12.123456789, y: 16.25 }, bindingNodeID: '1', direction: 'w' }],
    insulationJoints: [{ id: '6', type: 'normal', position: { x: 23.45, y: 24.5 }, bindingNodeID: '', custom: 'free' }],
    bufferStops: [{ id: '7', type: 'normal', position: { x: 36.25, y: 39 }, bindingNodeID: '', direction: 'left' }],
    platforms: [{ id: '8', name: '站台', x: -2.125, y: 43.25, width: 240.125, height: 20.75, extra: 0 }],
    switches: [{ id: '9', name: '道岔', type: 'simple', position: { x: 41.25, y: 42.5 }, bindingNodeID: '2',
        branchVectorList: [{ lineID: '3', x: 0.123456789, y: -0.23456789, extra: true }] }],
    cells: [{ id: 'c2', name: '  区段乙  ', linkIDList: '3', custom: { keep: 'all' } },
        { id: 'c1', name: '', linkIDList: '3', custom: 0, isNew: false }],
    annotations: [{ id: '10', text: '中文\n第二行 🚆', position: { x: 10.125, y: 20.5, z: 3 }, fontFamily: '宋体', fontSize: 12.125,
        fontWeight: 'bold', fontStyle: 'italic', textColor: '#ABCDEF', angle: -30.123456789, extra: ['note'] }],
    extensions: { custom: [1, '二', true] },
};
let stored;
const saves = [];
const props = vue.reactive({ ...defaultProps(pageComponent), selectedInstanceId: 'target-instance', stationSchemeId: 'target-scheme',
    gateway: {
        getStationSchemes: async () => [{ id: 'target-scheme', name: 'Target', revision: saves.length ? 8 : 7 }],
        getJson: async () => plain(stored),
        saveJson: async request => {
            saves.push(plain(request));
            stored = JSON.parse(request.json);
            stored.metadata.revision = 8;
            return { stationSchemeId: 'target-scheme', revision: 8 };
        },
    },
});
let page, editor, exposed;
try {
    scope.run(() => {
        page = pageComponent.setup(props, { expose: value => { exposed = value; }, emit() {} });
        const editorProps = vue.reactive(defaultProps(editorComponent));
        editor = editorComponent.setup(editorProps, { expose() {}, emit() {} });
        page.stationLayoutEditorRef.value = editor;
        vue.watchEffect(() => {
            editorProps.showGrid = page.showGrid.value;
            editorProps.gridSpacing = page.gridSpacing.value;
            editorProps.displayStyles = page.layoutDisplayStyles.value;
            editorProps.cells = page.layoutEditorCells.value;
            editorProps.readonly = props.readonly;
        });
    });
    page.stationSchemeOptions.value = [{ id: 'target-scheme', name: 'Target', revision: 7 }];
    const input = codec.serializeStationLayoutJson(original);
    await exposed.importJson(input);
    assert.equal(saves.length, 0, 'Import is local until the user saves');
    const output = JSON.parse(exposed.exportJson());
    const expected = JSON.parse(input);
    expected.metadata.instanceID = 'target-instance';
    expected.metadata.stationSchemeID = 'target-scheme';
    expected.metadata.revision = 7;
    expected.cells = expected.cells.map(cell => ({ ...cell, instanceID: 'target-instance', stationSchemeID: 'target-scheme' }));
    assert.deepEqual(output, expected, 'All element fields, precision, whitespace, order and extensions survive the complete component');
    assert.equal(page.currentStationSchemeId.value, 'target-scheme', 'Never adopt the source scheme or revision');
    assert.equal(editor.finishedCmdList.value.length, 0, 'Import resets history across the whole document boundary');
    await exposed.importJson(exposed.exportJson());
    assert.deepEqual(JSON.parse(exposed.exportJson()), expected, 'Repeated file roundtrips are stable');

    for (const value of [null, {}, undefined]) {
        const sparse = plain(expected);
        if (value === undefined) {
            delete sparse.metadata.gridSettings;
            delete sparse.metadata.displayStyles;
        } else {
            sparse.metadata.gridSettings = value;
            sparse.metadata.displayStyles = value;
        }
        await exposed.importJson(JSON.stringify(sparse));
        assert.deepEqual(JSON.parse(exposed.exportJson()), sparse, 'Empty and absent settings do not acquire new values on roundtrip');
    }
    const caseExtensions = plain(expected);
    delete caseExtensions.tracks[0].name;
    caseExtensions.tracks[0].Name = { toString: null, value: 'case-sensitive extension' };
    await exposed.importJson(JSON.stringify(caseExtensions));
    assert.deepEqual(JSON.parse(exposed.exportJson()), caseExtensions, 'Case-distinct extensions cannot be coerced into legacy text while importing an archive');
    await exposed.importJson(input);

    const beforeInterrupted = exposed.exportJson();
    const interruptedDocument = JSON.parse(input);
    interruptedDocument.metadata.displayStyles = { track: { color: '#F00F00' } };
    interruptedDocument.metadata.gridSettings.spacing = 99;
    const interrupted = exposed.importJson(JSON.stringify(interruptedDocument));
    page.currentStationSchemeId.value = 'another-scheme';
    await assert.rejects(interrupted, 'Switching schemes during import cancels application');
    page.currentStationSchemeId.value = 'target-scheme';
    assert.equal(exposed.exportJson(), beforeInterrupted, 'A cancelled import cannot leave its styles or grid on the preceding geometry');

    const beforeInvalid = exposed.exportJson();
    for (const invalid of ['{}', '[]', '{', input.replace('"formatVersion": 1', '"formatVersion": 999'),
        JSON.stringify({ ...expected, tracks: [null] }), JSON.stringify({ ...expected, nodes: [...expected.nodes, expected.nodes[0]] }),
        JSON.stringify({ ...original, tracks: [{ ...original.tracks[0], x1: 777, name: { toString: null } }] })]) {
        await assert.rejects(exposed.importJson(invalid));
        assert.equal(exposed.exportJson(), beforeInvalid, 'Invalid imports leave the canvas and all panels unchanged');
    }
    props.readonly = true;
    await vue.nextTick();
    await assert.rejects(exposed.importJson(input));
    assert.equal(exposed.exportJson(), beforeInvalid, 'Readonly still permits a complete export');
    props.readonly = false;
    await vue.nextTick();
    page.savingData.value = true;
    await assert.rejects(exposed.importJson(input));
    assert.throws(() => exposed.exportJson());
    page.savingData.value = false;

    assert.equal(await page.saveData({ silent: true }), true);
    assert.equal(saves[0].stationSchemeId, 'target-scheme');
    assert.equal(saves[0].instanceId, 'target-instance');
    assert.equal(saves[0].expectedRevision, 7, 'Use the target CAS revision, never the source revision');
    assert.deepEqual(JSON.parse(saves[0].json), expected);
    page.getData();
    await new Promise(resolve => setImmediate(resolve));
    await vue.nextTick();
    expected.metadata.revision = 8;
    assert.deepEqual(JSON.parse(exposed.exportJson()), expected, 'The gateway reload also takes the preserving path');

    page.createCell();
    const pendingId = page.selectedCellId.value;
    const pendingExport = exposed.exportJson();
    assert.ok(pendingId.startsWith('TEMP_CELL_'));
    await exposed.importJson(pendingExport);
    assert.deepEqual(JSON.parse(exposed.exportJson()), JSON.parse(pendingExport), 'Unsaved track circuits retain identity across file roundtrips');
    assert.equal(page.buildCellsForJson().at(-1).id, '', 'Database saves retain host-assigned track circuit IDs');
    assert.equal(page.buildCellsForJson({ forFile: true }).at(-1).id, pendingId);
    const afterPending = exposed.exportJson();
    page.cellForm.value.id = 'c1';
    assert.throws(() => exposed.exportJson(), 'An invalid cell draft blocks export instead of silently discarding edits');
    page.cellForm.value.id = page.selectedCellId.value;
    assert.equal(exposed.exportJson(), afterPending);
    console.log('Complete component JSON imports, exports, target scope/revision, pending cells, readonly, failures and gateway reload passed.');
} finally {
    scope.stop();
}
