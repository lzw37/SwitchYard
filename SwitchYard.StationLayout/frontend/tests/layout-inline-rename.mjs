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
// Vite owns SVG imports. Keep the real asset normalizers and placeholder geometry
// while replacing only these bundler-specific globs in the Node runtime.
const signalStyles = evaluate(read('../src/assets/stationLayoutSignalStyles.js')
    .replace(/import\.meta\.glob\([\s\S]*?\}\)/g, '({})'));
const { descriptor } = parse(read('../src/components/StationLayoutEditor.vue'));
const compiled = compileScript(descriptor, { id: 'layout-inline-rename-test' });
const component = evaluate(compiled.content, name => {
    if (name === 'vue') return { ...vue, onMounted() {}, onBeforeUnmount() {} };
    if (name === '../messages') return messages;
    if (name === '../assets/stationLayoutSignalStyles') return signalStyles;
    if (name === '../assets/stationLayoutBufferStopStyles') return bufferStopStyles;
    throw new Error(`Unexpected import: ${name}`);
}).default;
const template = compileTemplate({
    source: descriptor.template.content,
    filename: 'StationLayoutEditor.vue', id: 'layout-inline-rename-test',
    compilerOptions: { bindingMetadata: compiled.bindings },
});
assert.deepEqual(template.errors, []);
const render = evaluate(template.code, name => {
    assert.equal(name, 'vue');
    // A DOM renderer applies directives; these checks invoke the real compiled
    // VNode listeners, with browser interaction verified separately in the app.
    return { ...vue, withDirectives: node => node, resolveDynamicComponent: tag => tag };
}).render;

function layout() {
    return {
        metadata: { stationSchemeID: 'scheme', revision: 7, latestElementID: 30 },
        nodes: [{ id: 'n1', x: 100, y: 100 }, { id: 'n2', x: 400, y: 100 }, { id: 'n3', x: 600, y: 200 }],
        tracks: [
            { id: 'l1', name: '1道', fromNodeID: 'n1', toNodeID: 'n2', x1: 100, y1: 100, x2: 400, y2: 100 },
            { id: 'l2', name: '2道', fromNodeID: 'n2', toNodeID: 'n3', x1: 400, y1: 100, x2: 600, y2: 200 },
        ],
        signals: [{ id: 's1', name: 'S1', bindingNodeID: 'n1', position: { x: 100, y: 100 }, direction: 'w' }],
        switches: [{ id: 'w1', name: 'W1', bindingNodeID: 'n2', position: { x: 400, y: 100 } }],
        platforms: [{ id: 'p1', name: '站台一', position: { x: 100, y: 130 }, width: 200, height: 40 }],
        annotations: [{ id: 'a1', text: '原标注', position: { x: 120, y: 190 }, angle: 30 }],
    };
}
function fixture(t, overrides = {}) {
    const defaults = Object.fromEntries(Object.entries(component.props).map(([key, spec]) => [
        key, typeof spec.default === 'function' && spec.type !== Function ? spec.default() : spec.default,
    ]));
    const props = vue.reactive({ ...defaults, showCellNames: true, cells: [
        { id: 'c1', name: '原轨道电路', linkIDList: 'l1' },
        { id: 'c2', name: '其他电路', linkIDList: 'l2' },
    ], ...overrides });
    const emitted = [];
    const scope = vue.effectScope();
    const state = scope.run(() => component.setup(props, { expose() {}, emit: (...args) => emitted.push(args) }));
    let focusCount = 0;
    state.svgRef.value = {
        focus() { focusCount += 1; },
        getBoundingClientRect: () => ({ left: -100, top: -200, right: 1820, bottom: 880 }),
        parentElement: { getBoundingClientRect: () => ({ left: 10, top: 20, right: 810, bottom: 620 }) },
    };
    state.loadDataFromJson(layout());
    state.finishedCmdList.value = [];
    state.revokedCmdList.value = [];
    emitted.length = 0;
    t.after(() => scope.stop());
    return { state, props, emitted, focusCount: () => focusCount,
        snapshot: () => JSON.parse(state.buildJsonData()),
        render: () => render(vue.proxyRefs(state), [], props, vue.proxyRefs(state), {}, {}),
    };
}
function event(extra = {}) {
    return { button: 0, clientX: 300, clientY: 200, preventDefault() {}, stopPropagation() {}, ...extra };
}
function begin(state, kind = 'link', id = 'l1') {
    state.beginInlineRename(event(), kind, id);
    assert.ok(state.inlineRename.value, `Expected editable ${kind}:${id}`);
}
function findAll(node, predicate, matches = []) {
    if (!node || typeof node !== 'object') return matches;
    if (predicate(node)) matches.push(node);
    for (const child of Array.isArray(node.children) ? node.children : []) findAll(child, predicate, matches);
    return matches;
}

for (const [kind, collection, id, field] of [
    ['link', 'tracks', 'l1', 'name'], ['signal', 'signals', 's1', 'name'],
    ['switch', 'switches', 'w1', 'name'], ['platform', 'platforms', 'p1', 'name'],
    ['annotation', 'annotations', 'a1', 'text'],
]) {
    test(`${kind} rename changes only its text, serializes, and is one undoable mutation`, t => {
        const { state, snapshot } = fixture(t);
        state.selectedLineIds.value = new Set(['l1', 'l2']);
        const before = snapshot();
        begin(state, kind, id);
        state.inlineRename.value.value = '新名称';
        state.handleInlineRenameKeydown(event({ key: 'Enter' }));
        state.commitInlineRename(); // A later blur must not create a second mutation.
        assert.equal(state.inlineRename.value, null);
        assert.equal(state.finishedCmdList.value.length, 1);
        const expected = plain(before);
        expected[collection].find(item => item.id === id)[field] = '新名称';
        assert.deepEqual(snapshot(), expected, 'IDs, bindings, geometry, and all other objects stay unchanged');
        state.revoke();
        assert.deepEqual(snapshot(), before);
        state.redo();
        assert.deepEqual(snapshot(), expected);
    });
}

test('Cell rename emits once to its owner without mutating prop Cells or editor history', t => {
    const { state, props, emitted, snapshot } = fixture(t);
    const before = snapshot();
    const cells = plain(props.cells);
    begin(state, 'cell', 'c1');
    state.inlineRename.value.value = '  新电路  ';
    state.commitInlineRename();
    state.commitInlineRename();
    assert.deepEqual(plain(emitted.filter(item => item[0] === 'cell-rename')), [['cell-rename', { id: 'c1', name: '新电路' }]]);
    assert.deepEqual(plain(props.cells), cells);
    assert.deepEqual(snapshot(), before);
    assert.equal(state.finishedCmdList.value.length, 0);
});

test('no-op, cancelled and composing input never create a history entry', t => {
    const { state, snapshot } = fixture(t);
    const before = snapshot();
    begin(state);
    state.inlineRename.value.value = '  1道  ';
    state.commitInlineRename();
    begin(state);
    state.inlineRename.value.value = '不会保存';
    state.handleInlineRenameKeydown(event({ key: 'Enter', isComposing: true }));
    assert.ok(state.inlineRename.value);
    state.handleInlineRenameKeydown(event({ key: 'Enter', keyCode: 229 }));
    assert.ok(state.inlineRename.value);
    state.handleInlineRenameKeydown(event({ key: 'Escape' }));
    state.commitInlineRename();
    begin(state);
    state.inlineRename.value.value = '取消';
    state.cancelInlineRename();
    assert.equal(state.inlineRename.value, null);
    assert.deepEqual(snapshot(), before);
    assert.equal(state.finishedCmdList.value.length, 0);
});

test('blank equipment names fall back to IDs while track names and annotation text keep their semantics', t => {
    const { state } = fixture(t);
    for (const [kind, collection, id] of [
        ['signal', 'signals', 's1'], ['switch', 'switches', 'w1'], ['platform', 'platforms', 'p1'],
    ]) {
        begin(state, kind, id);
        state.inlineRename.value.value = '   ';
        state.commitInlineRename();
        assert.equal(state[collection].value[0].name, id);
    }
    begin(state);
    state.inlineRename.value.value = '   ';
    state.commitInlineRename();
    assert.equal(state.tracks.value[0].name, '', 'An unnamed Link remains supported');
    begin(state, 'annotation', 'a1');
    state.inlineRename.value.value = '  标注空格  ';
    state.commitInlineRename();
    assert.equal(state.annotations.value[0].text, '  标注空格  ');
});

test('selection mode is required; readonly, route picking, unsupported targets and buttons reject rename', t => {
    const { state, props } = fixture(t);
    for (const mode of [1, 2]) {
        state.setEditMode(mode);
        state.beginInlineRename(event(), 'link', 'l1');
        assert.equal(state.inlineRename.value, null);
    }
    state.setEditMode(0);
    props.readonly = true;
    state.beginInlineRename(event(), 'link', 'l1');
    assert.equal(state.inlineRename.value, null);
    props.readonly = false;
    props.routePickTarget = 'start';
    state.beginInlineRename(event(), 'link', 'l1');
    assert.equal(state.inlineRename.value, null);
    props.routePickTarget = '';
    for (const [kind, id] of [['node', 'n1'], ['link', 'missing'], ['bufferStop', 'missing']]) {
        state.beginInlineRename(event(), kind, id);
        assert.equal(state.inlineRename.value, null);
    }
    state.beginInlineRename(event({ button: 2 }), 'link', 'l1');
    assert.equal(state.inlineRename.value, null);
    assert.equal(state.finishedCmdList.value.length, 0);
});

test('loading and mode, readonly, route-pick or viewport changes cancel an open rename', async t => {
    const { state, props, snapshot } = fixture(t);
    const before = snapshot();
    for (const change of [
        () => state.setEditMode(1),
        () => { props.readonly = true; },
        () => { props.routePickTarget = 'end'; },
        () => { props.editorState = 'cell_editing'; },
        () => { props.displayScaleX = 2; },
        () => state.loadDataFromJson(layout()),
    ]) {
        begin(state);
        state.inlineRename.value.value = '不应保存';
        change();
        await vue.nextTick();
        assert.equal(state.inlineRename.value, null);
        state.commitInlineRename();
        assert.deepEqual(snapshot(), before);
        props.readonly = false;
        props.routePickTarget = '';
        props.editorState = '';
        props.displayScaleX = 1;
        state.setEditMode(0);
        await vue.nextTick();
    }
});

test('a stale or deleted target cannot overwrite changes made while the input was open', t => {
    const { state } = fixture(t);
    begin(state);
    state.inlineRename.value.value = '旧输入';
    state.tracks.value[0].name = '后来的更新';
    state.commitInlineRename();
    assert.equal(state.tracks.value[0].name, '后来的更新');
    begin(state);
    state.inlineRename.value.value = '已删除';
    state.tracks.value = state.tracks.value.filter(item => item.id !== 'l1');
    state.commitInlineRename();
    assert.equal(state.tracks.value.length, 1);
    assert.equal(state.finishedCmdList.value.length, 0);
});

test('real rendered double-click handlers open all supported targets and input handlers commit/cancel', async t => {
    const { state, render: renderEditor } = fixture(t);
    const handlers = findAll(renderEditor(), node => typeof node.props?.onDblclick === 'function');
    const encountered = new Set();
    for (const node of handlers) {
        state.cancelInlineRename();
        node.props.onDblclick(event());
        if (state.inlineRename.value) encountered.add(state.inlineRename.value.kind);
    }
    assert.deepEqual([...encountered].sort(), ['annotation', 'cell', 'link', 'platform', 'signal', 'switch']);
    begin(state);
    await vue.nextTick();
    const input = findAll(renderEditor(), node => node.type === 'input')[0];
    assert.ok(input, 'The opened editor must render an actual text input');
    input.props['onUpdate:modelValue']('由输入保存');
    input.props.onKeydown(event({ key: 'Enter' }));
    input.props.onBlur(event());
    assert.equal(state.tracks.value[0].name, '由输入保存');
    assert.equal(state.finishedCmdList.value.length, 1);
    begin(state);
    const cancelInput = findAll(renderEditor(), node => node.type === 'input')[0];
    cancelInput.props['onUpdate:modelValue']('由输入取消');
    cancelInput.props.onKeydown(event({ key: 'Escape' }));
    cancelInput.props.onBlur(event());
    assert.equal(state.tracks.value[0].name, '由输入保存');
    assert.equal(state.finishedCmdList.value.length, 1);
});

for (const trigger of ['outside mousedown', 'blur']) {
    test(`${trigger} commits visible composing text before v-model catches up`, t => {
        const { state, render: renderEditor } = fixture(t);
        begin(state);
        const input = findAll(renderEditor(), node => node.type === 'input')[0];
        assert.ok(input);
        // During composition the DOM has the current Chinese text while Vue's
        // v-model still contains the value from before composition started.
        state.inlineRename.value.value = '旧草稿';
        state.inlineRenameInputRef.value = { value: '  正在组词的新名称  ', focus() {}, select() {} };
        state.handleInlineRenameKeydown(event({ key: 'Enter', isComposing: true }));
        assert.equal(state.inlineRename.value.value, '旧草稿');
        assert.equal(state.tracks.value[0].name, '1道');
        if (trigger === 'outside mousedown') {
            const inside = {};
            state.inlineRenameContainerRef.value = { contains: target => target === inside };
            state.handleInlineRenameOutside(event({ target: inside }));
            assert.ok(state.inlineRename.value, 'Clicking inside the input leaves composition open');
            state.handleInlineRenameOutside(event({ target: {} }));
        } else {
            input.props.onBlur(event());
        }
        assert.equal(state.tracks.value[0].name, '正在组词的新名称');
        assert.equal(state.inlineRename.value, null);
        input.props.onBlur(event());
        assert.equal(state.finishedCmdList.value.length, 1, 'The later blur cannot commit a second time');
        state.revoke();
        assert.equal(state.tracks.value[0].name, '1道');
        state.redo();
        assert.equal(state.tracks.value[0].name, '正在组词的新名称');
    });
}

test('input coordinates stay within the visible scrolled canvas area', t => {
    const { state } = fixture(t, { displayScaleX: 2, displayScaleY: 2 });
    for (const [clientX, clientY] of [[-1000, -1000], [3000, 3000], [300, 200]]) {
        state.beginInlineRename(event({ clientX, clientY }), 'link', 'l1');
        const input = state.inlineRename.value;
        assert.ok(input.x >= 110 && input.x + input.width <= 910);
        assert.ok(input.y >= 220 && input.y + 32 <= 820);
    }
});
