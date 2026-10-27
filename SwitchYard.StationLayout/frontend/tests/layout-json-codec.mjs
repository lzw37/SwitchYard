import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { runInNewContext } from 'node:vm';
import test from 'node:test';
import ts from 'typescript';

const exports = {};
const source = readFileSync(new URL('../src/layoutJson.ts', import.meta.url), 'utf8');
runInNewContext(ts.transpileModule(source, {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
}).outputText, { exports });
const {
  STATION_LAYOUT_JSON_FORMAT, STATION_LAYOUT_JSON_VERSION, STATION_LAYOUT_COLLECTIONS,
  serializeStationLayoutJson: serialize, parseStationLayoutJson: parse,
  validateStationLayoutJson: validate, isStationLayoutArchive: isArchive,
} = exports;
const plain = value => JSON.parse(JSON.stringify(value));
const clone = value => structuredClone(value);
const point = (x = 1.2345678901234567, y = -9876.54321098765) => ({ x, y, coordinateExtension: '坐标扩展' });

function layout() {
  return {
    metadata: {
      instanceID: '车站A', stationSchemeID: 'scheme-计划一', revision: 17, latestElementID: 309,
      displayStyles: { track: { color: '#123456', strokeWidth: 1.123456789 }, custom: { enabled: true } },
      gridSettings: { originX: -27.25, originY: 0.001, verticalSpace: 20, horizontalSpace: 30, future: '网格' },
      coordinateTransform: { applied: true, minX: -231.3456789123, minY: 1.111111111, scale: 0.987654321 },
      futureMetadata: ['顺序', { value: null }],
    },
    rootExtension: { notes: '保留 Unicode、换行\n与引号“” 🚆', values: [true, null, -1.0123456789012345] },
    tracks: [
      { id: '线路-2', name: '第二股道', x1: 1.2345678901234567, y1: -2.000000000000001, x2: 40, y2: 50,
        fromNodeID: '节点-1', toNodeID: '节点-2', arrowDirection: 'reverse', extension: { order: [3, 2, 1] } },
      { id: '线路-1', x1: 40, y1: 50, x2: 60, y2: 80, fromNodeID: '节点-2', toNodeID: '节点-3' },
    ],
    curves: [{ id: 'curve', nodeID: '节点-2', tangentLinkID1: '线路-2', tangentLinkID2: '线路-1',
      radius: 21.65432109876543, angle: 133.23456789, tangentDistance: 5.12345678,
      start: point(), end: point(30, 10), center: point(40, 15), largeArcFlag: 0, sweepFlag: 1,
      futureCurve: { color: '#abc' } }],
    nodes: [
      { id: '节点-1', x: 1.2345678901234567, y: -2.000000000000001, adjacentLineIDList: ['线路-2'] },
      { id: '节点-2', x: 40, y: 50, adjacentLineIDList: ['线路-2', '线路-1'], futureNode: true },
      { id: '节点-3', x: 60, y: 80, adjacentLineIDList: ['线路-1'] },
    ],
    signals: [{ id: 'signal', name: '入站信号', type: 'DepartureSignal', direction: 'e',
      bindingNodeID: '节点-1', position: point(), extension: { key: 'signal' } }],
    insulationJoints: [{ id: 'insulation', type: 'Normal', bindingNodeID: '节点-2', position: point(), custom: 1 }],
    bufferStops: [{ id: 'buffer', type: 'normal', direction: 'left', bindingNodeID: '节点-3', position: point(), custom: 2 }],
    platforms: [{ id: 'platform', name: '站台 α', x: -100.123456789, y: 25.23456789,
      width: 132.123456789, height: 4.345678912, custom: { future: '平台' } }],
    switches: [{ id: 'switch', name: '道岔 1', bindingNodeID: '节点-2', position: point(), custom: '道岔',
      branchVectorList: [{ x: -0.123456789, y: 1.23456789, lineID: '线路-2', branchExtension: 99 },
        { x: 0.987654321, y: -1.23456789, lineID: '线路-1' }] }],
    cells: [{ id: 'cell', name: '轨道电路', linkIDList: '线路-2,线路-1', instanceID: '车站A', custom: ['cell'] },
      { id: 'TEMP_CELL_1', name: '未保存电路', linkIDList: '', isNew: true }],
    annotations: [{ id: 'annotation', text: '完整标注\n第二行 <>& 🚉', position: point(), fontFamily: '思源黑体',
      fontSize: 16.123456789, fontWeight: 'bold', fontStyle: 'italic', angle: -31.23456789,
      textColor: '#ffeeaa', custom: { futureAnnotation: true } }],
  };
}

test('all ten collections, precision, Unicode, identity, ordering and extensions round-trip without mutation', () => {
  const original = layout();
  const before = clone(original);
  validate(original);
  const text = serialize(original);
  assert.match(text, /\n  "metadata": \{/);
  assert.ok(text.includes('未保存电路'));
  const imported = parse(text);
  assert.equal(imported.format, STATION_LAYOUT_JSON_FORMAT);
  assert.equal(imported.formatVersion, STATION_LAYOUT_JSON_VERSION);
  assert.equal(imported.metadata.revision, 17, 'Archive version must not overwrite backend revision');
  assert.equal(isArchive(imported), true);
  const expected = { ...original, format: STATION_LAYOUT_JSON_FORMAT, formatVersion: STATION_LAYOUT_JSON_VERSION };
  assert.deepEqual(plain(imported), expected);
  assert.deepEqual(original, before);
  assert.equal(serialize(imported), text);
  for (const key of STATION_LAYOUT_COLLECTIONS) assert.ok(Array.isArray(imported[key]), key);
});

test('serialization fills missing collections, including a completely empty station scheme', () => {
  const input = { nodes: [], metadata: { latestElementID: 0 }, rootExtension: { enabled: false } };
  const imported = parse(serialize(input));
  for (const key of STATION_LAYOUT_COLLECTIONS) assert.deepEqual(plain(imported[key]), []);
  assert.deepEqual(input, { nodes: [], metadata: { latestElementID: 0 }, rootExtension: { enabled: false } });
});

test('BOM and legacy sparse documents retain data for existing loader defaults', () => {
  const legacy = { tracks: [{ id: 'old-link', x1: 0 }], nodes: [{ id: 'old-node' }],
    annotations: [{ text: '旧标注', fontWeight: '700' }],
    curves: [{ startX: 1.111111111, startY: 2, endX: 3, endY: 4, centerX: 5, centerY: 6 }],
    cells: [{ id: '', name: '待保存', linkIDList: ['old-link'] }] };
  assert.deepEqual(plain(parse(`\uFEFF${JSON.stringify(legacy)}`)), legacy);
  assert.deepEqual(plain(parse(`\uFEFF${serialize(layout())}`)), plain(parse(serialize(layout()))));
  assert.equal(isArchive(legacy), false);
});

test('invalid roots, empty unrelated JSON and format/version mismatches cannot clear a layout', () => {
  for (const value of [null, true, 5, 'layout', [], {}, { metadata: {} }, { other: [] }]) {
    assert.throws(() => parse(JSON.stringify(value)), /Invalid station layout JSON/);
  }
  for (const patch of [
    { format: 'another-format', formatVersion: 1 },
    { format: STATION_LAYOUT_JSON_FORMAT, formatVersion: 2 },
    { format: STATION_LAYOUT_JSON_FORMAT }, { formatVersion: 1 },
  ]) assert.throws(() => parse(JSON.stringify({ nodes: [], ...patch })), /format/);
  assert.throws(() => parse('{invalid'), /JSON|position|property/i);
  assert.throws(() => parse(''), /JSON/i);
  assert.throws(() => parse({}), /JSON text/);
});

test('each collection rejects malformed arrays, entries and duplicate IDs', () => {
  for (const key of STATION_LAYOUT_COLLECTIONS) {
    for (const entries of [null, {}, '[]', [null], [1], [[]]]) {
      assert.throws(() => validate({ [key]: entries }), /must be an (array|object)/, key);
    }
    assert.throws(() => validate({ [key]: [{ id: 'duplicate' }, { id: 'duplicate' }] }), /duplicates ID/);
  }
  const archive = plain(parse(serialize(layout())));
  delete archive.nodes;
  assert.throws(() => validate(archive), /nodes must be an array/);
  const sameIdAcrossKinds = { nodes: [{ id: '1' }], tracks: [{ id: '1' }] };
  assert.doesNotThrow(() => validate(sameIdAcrossKinds));
  assert.throws(() => validate({ nodes: [{ id: 1 }, { id: '1' }] }), /duplicates ID/);
});

test('archive geometry is required and all supplied geometry must be finite numbers', () => {
  const cases = [
    ['tracks', 'x1'], ['nodes', 'y'], ['platforms', 'width'], ['curves', 'radius'],
    ['curves', 'tangentDistance'], ['annotations', 'fontSize'], ['annotations', 'angle'],
  ];
  for (const [key, field] of cases) {
    for (const bad of [null, '1.2', true, NaN, Infinity, -Infinity]) {
      const value = layout();
      value[key][0][field] = bad;
      assert.throws(() => validate(value), /finite number/, `${key}.${field}`);
    }
  }
  const archive = plain(parse(serialize(layout())));
  delete archive.tracks[0].x1;
  assert.throws(() => validate(archive), /tracks\[0\].x1/);
  for (const key of ['signals', 'switches', 'bufferStops', 'insulationJoints', 'annotations']) {
    const value = layout();
    value[key][0].position = { x: 'bad', y: 0 };
    assert.throws(() => validate(value), /position.x/);
  }
  const curve = layout();
  curve.curves[0].start = { x: 0, y: false };
  assert.throws(() => validate(curve), /start.y/);
  curve.curves[0].start = point();
  curve.curves[0].sweepFlag = 3;
  assert.throws(() => validate(curve), /sweepFlag/);
});

test('all node and track references are checked; unbound equipment and pending Cells remain valid', () => {
  const patches = [
    value => { value.tracks[0].fromNodeID = 'missing'; },
    value => { value.tracks[0].toNodeID = 'missing'; },
    value => { value.curves[0].nodeID = 'missing'; },
    value => { value.curves[0].tangentLinkID1 = 'missing'; },
    value => { value.curves[0].tangentLinkID2 = 'missing'; },
    value => { value.nodes[0].adjacentLineIDList.push('missing'); },
    value => { value.signals[0].bindingNodeID = 'missing'; },
    value => { value.insulationJoints[0].bindingNodeID = 'missing'; },
    value => { value.bufferStops[0].bindingNodeID = 'missing'; },
    value => { value.switches[0].bindingNodeID = 'missing'; },
    value => { value.switches[0].branchVectorList[0].lineID = 'missing'; },
    value => { value.cells[0].linkIDList = '线路-1,missing'; },
  ];
  for (const patch of patches) {
    const value = layout();
    patch(value);
    const before = clone(value);
    assert.throws(() => validate(value), /references a missing element/);
    assert.deepEqual(value, before, 'Failed validation must not alter the layout');
  }
  const value = layout();
  for (const key of ['signals', 'insulationJoints', 'bufferStops', 'switches']) value[key][0].bindingNodeID = '';
  value.cells[0].linkIDList = '线路-2，线路-1；线路-2';
  value.cells.push({ id: '', linkIDList: '', isNew: true }, { id: '', linkIDList: '', isNew: true });
  assert.doesNotThrow(() => serialize(value));
});

test('malformed nested topology, metadata, and non-JSON extensions fail without silently losing data', () => {
  for (const patch of [
    value => { value.nodes[0].adjacentLineIDList = '线路-1'; },
    value => { value.switches[0].branchVectorList = {}; },
    value => { value.switches[0].branchVectorList = [null]; },
    value => { value.switches[0].branchVectorList[0].x = 'wrong'; },
    value => { value.cells[0].linkIDList = {}; },
    value => { value.metadata = []; },
    value => { value.metadata.revision = '1'; },
    value => { value.metadata.gridSettings = 'wrong'; },
    value => { value.rootExtension.values.push(undefined); },
    value => { value.rootExtension.unsupported = () => 1; },
    value => { value.rootExtension.big = 1n; },
    value => { value.rootExtension.bad = NaN; },
    value => { value.rootExtension.date = new Date(); },
    value => { value.rootExtension.self = value; },
  ]) {
    const value = layout();
    patch(value);
    assert.throws(() => serialize(value), /Invalid station layout JSON/);
  }
});

test('archive IDs and references use the backend string contract without converting legacy IDs', () => {
  const numericLegacy = { nodes: [{ id: 1, x: 10, y: 20 }] };
  assert.deepEqual(plain(parse(JSON.stringify(numericLegacy))), numericLegacy);
  assert.throws(() => serialize(numericLegacy), /nodes\[0\].id must be an ID string/);
  for (const patch of [
    value => { value.nodes[0].id = 1; },
    value => { value.tracks[0].fromNodeID = 1; },
    value => { value.signals[0].bindingNodeID = 1; },
    value => { value.nodes[0].adjacentLineIDList = [1]; },
    value => { value.switches[0].branchVectorList[0].lineID = 1; },
    value => { value.cells[0].id = null; },
  ]) {
    const value = plain(parse(serialize(layout())));
    patch(value);
    assert.throws(() => parse(JSON.stringify(value)), /must be an ID string/);
  }
  const value = layout();
  value.signals[0].bindingNodeID = null;
  assert.equal(parse(serialize(value)).signals[0].bindingNodeID, null);
});

test('Cell link arrays are legacy-only; archives preserve string or null using the save API contract', () => {
  const value = layout();
  value.cells[0].linkIDList = ['线路-2', '线路-1'];
  assert.doesNotThrow(() => parse(JSON.stringify(value)));
  assert.throws(() => serialize(value), /cells\[0\].linkIDList must be a string or null/);
  const archive = plain(parse(serialize(layout())));
  archive.cells[0].linkIDList = [];
  assert.throws(() => parse(JSON.stringify(archive)), /linkIDList must be a string or null/);
  value.cells[0].linkIDList = null;
  assert.equal(parse(serialize(value)).cells[0].linkIDList, null);
});

test('metadata integers match backend bounds and nullable fields preserve their values', () => {
  for (const latestElementID of [-1, 0.5, 2147483648, null]) {
    assert.throws(() => validate({ nodes: [], metadata: { latestElementID } }), /latestElementID/);
  }
  for (const revision of [-1, 0.5, Number.MAX_SAFE_INTEGER + 1]) {
    assert.throws(() => validate({ nodes: [], metadata: { revision } }), /revision/);
  }
  for (const metadata of [null, { revision: null }, { revision: Number.MAX_SAFE_INTEGER, latestElementID: 2147483647 },
    { revision: 0, latestElementID: 0, instanceID: null, stationSchemeID: null, coordinateTransform: null }]) {
    assert.deepEqual(plain(parse(serialize({ nodes: [], metadata }))).metadata, metadata);
  }
  for (const coordinateTransform of [{ applied: 'yes' }, { applied: null }, { scale: '1' }, { minX: null }]) {
    assert.throws(() => validate({ nodes: [], metadata: { coordinateTransform } }), /coordinateTransform/);
  }
});

test('known text fields in both formats reject values the typed backend cannot save while extensions stay open', () => {
  const fields = {
    tracks: ['name', 'arrowDirection', 'arrowType'], signals: ['name', 'type', 'direction'],
    insulationJoints: ['type'], bufferStops: ['type', 'direction'], platforms: ['name'],
    switches: ['name', 'type'], cells: ['name', 'instanceID', 'stationSchemeID'],
    annotations: ['text', 'fontFamily', 'fontWeight', 'fontStyle', 'textColor'],
  };
  for (const [key, names] of Object.entries(fields)) {
    for (const name of names) {
      const value = plain(parse(serialize(layout())));
      value[key][0][name] = 700;
      assert.throws(() => parse(JSON.stringify(value)), /must be a string or null/, `${key}.${name}`);
      delete value.format;
      delete value.formatVersion;
      assert.throws(() => parse(JSON.stringify(value)), /must be a string or null/, `legacy ${key}.${name}`);
      value[key][0][name] = null;
      assert.doesNotThrow(() => validate(value));
    }
  }
  for (const key of ['instanceID', 'stationSchemeID']) {
    assert.throws(() => validate({ nodes: [], metadata: { [key]: 1 } }), /must be a string or null/);
  }
  const value = layout();
  value.annotations[0].custom.fontWeight = 700;
  assert.equal(parse(serialize(value)).annotations[0].custom.fontWeight, 700);
  const archive = plain(parse(serialize(layout())));
  archive.tracks[0].name = null;
  archive.tracks[0].Name = { toString: null, extension: true };
  archive.annotations[0].ID = { extension: true };
  assert.deepEqual(plain(parse(JSON.stringify(archive))), archive, 'Archive case variants are extensions, not legacy aliases');
});

test('malformed legacy text, aliases and nested settings are rejected before canvas replacement', () => {
  const invalidString = { toString: null };
  const malformed = { tracks: [{ id: 'track', x1: 777, name: invalidString }] };
  assert.throws(() => parse(JSON.stringify(malformed)), /tracks\[0\].name must be a string or null/);
  for (const entity of [{ Name: invalidString }, { ID: invalidString }, { FromNodeID: invalidString }]) {
    assert.throws(() => parse(JSON.stringify({ tracks: [entity] })), /Invalid station layout JSON/);
  }
  assert.throws(() => parse(JSON.stringify({ cells: [{ LinkIDList: [invalidString] }] })), /LinkIDList/);
  for (const metadata of [
    { gridSettings: { originX: invalidString } }, { gridSettings: { OriginY: invalidString } },
    { gridSettings: { spacing: invalidString } }, { gridSettings: { showGrid: invalidString } },
    { displayStyles: { switchName: { fontFamily: invalidString } } },
    { displayStyles: { signalName: { fontSize: invalidString } } },
    { displayStyles: { platformName: { fontWeight: invalidString } } },
    { displayStyles: { track: { color: invalidString } } },
    { displayStyles: { curve: { strokeWidth: invalidString } } },
    { displayStyles: { signal: { scale: invalidString } } },
    { displayStyles: { node: { radius: invalidString } } },
  ]) {
    const value = { ...layout(), metadata };
    assert.throws(() => parse(JSON.stringify(value)), /metadata\.(gridSettings|displayStyles)/);
    value.format = STATION_LAYOUT_JSON_FORMAT;
    value.formatVersion = STATION_LAYOUT_JSON_VERSION;
    assert.throws(() => parse(JSON.stringify(value)), /metadata\.(gridSettings|displayStyles)/);
  }
  const safe = { nodes: [], metadata: {
    gridSettings: { originX: '1.125', spacing: null, custom: invalidString },
    displayStyles: { switchName: { fontWeight: 700, fontSize: '12.5', fontFamily: null },
      track: null, custom: invalidString },
  } };
  assert.deepEqual(plain(parse(JSON.stringify(safe))), safe, 'Unknown extensions and supported setting scalars remain unchanged');
});
