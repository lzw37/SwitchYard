import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { runInNewContext } from "node:vm";
import { randomUUID } from "node:crypto";
import { test } from "node:test";
import { parse, compileScript, compileTemplate } from "@vue/compiler-sfc";
import ts from "typescript";
import * as vue from "vue";

function evaluate(
  source,
  require = () => {
    throw new Error("Unexpected import");
  },
) {
  const exports = {};
  runInNewContext(
    ts.transpileModule(source, {
      compilerOptions: {
        module: ts.ModuleKind.CommonJS,
        target: ts.ScriptTarget.ES2022,
      },
    }).outputText,
    {
      exports,
      require,
      console,
      crypto: { randomUUID },
      window: {},
      localStorage: { setItem() {} },
    },
  );
  return exports;
}
const types = evaluate(
  readFileSync(new URL("./types.ts", import.meta.url), "utf8"),
);
const crossingAnalysis = evaluate(
  readFileSync(new URL("./crossingAnalysis.ts", import.meta.url), "utf8"),
);
const analysisOverlay = evaluate(
  readFileSync(new URL("./analysisOverlay.ts", import.meta.url), "utf8"),
);
const filletGeometry = evaluate(
  readFileSync(new URL("./filletGeometry.ts", import.meta.url), "utf8"),
);
const linkGeometry = evaluate(
  readFileSync(new URL("./linkGeometry.ts", import.meta.url), "utf8"),
  (name) => {
    if (name === "./filletGeometry") return filletGeometry;
    throw new Error(`Unexpected import: ${name}`);
  },
);
const crossings = evaluate(
  readFileSync(new URL("./crossings.ts", import.meta.url), "utf8"),
  (name) => {
    if (name === "./filletGeometry") return filletGeometry;
    throw new Error(`Unexpected import: ${name}`);
  },
);
const geometry = evaluate(
  readFileSync(new URL("./geometry.ts", import.meta.url), "utf8"),
  (name) => {
    if (name === "./linkGeometry") return linkGeometry;
    throw new Error(`Unexpected import: ${name}`);
  },
);
const polygonEditing = evaluate(
  readFileSync(new URL("./polygonEditing.ts", import.meta.url), "utf8"),
  (name) => {
    if (name === "./geometry") return geometry;
    throw new Error(`Unexpected import: ${name}`);
  },
);
const polygonGeometry = evaluate(
  readFileSync(new URL("./polygonGeometry.ts", import.meta.url), "utf8"),
  (name) => {
    if (name === "./filletGeometry") return filletGeometry;
    if (name === "./polygonEditing") return polygonEditing;
    throw new Error(`Unexpected import: ${name}`);
  },
);
const grid = evaluate(
  readFileSync(new URL("./grid.ts", import.meta.url), "utf8"),
);
const viewport = evaluate(readFileSync(new URL("./viewport.ts", import.meta.url), "utf8"));
const colors = evaluate(
  readFileSync(new URL("./colors.ts", import.meta.url), "utf8"),
);
const doc = () => ({
  name: "test",
  description: "",
  facilities: [],
  links: [],
  plans: [],
});
function component(file, props, extra = {}) {
  const { descriptor } = parse(
    readFileSync(new URL(file, import.meta.url), "utf8"),
  );
  const errors = [],
    events = [];
  const result = evaluate(
    compileScript(descriptor, { id: "sketch-tests" }).content,
    (name) => {
      if (extra[name]) return extra[name];
      if (name === "vue")
        return { ...vue, onMounted() {}, onBeforeUnmount() {} };
      if (name === "vue-i18n")
        return { useI18n: () => ({ locale: vue.ref("zh") }) };
      if (name === "element-plus")
        return {
          ElMessage: {
            warning: (v) => errors.push(v),
            info: (v) => errors.push(v),
            success() {},
          },
          ElMessageBox: {
            confirm: async () => {},
            prompt: async () => ({ value: "test" }),
          },
        };
      if (name === "./types") return types;
      if (name === "./geometry") return geometry;
      if (name === "./linkGeometry") return linkGeometry;
      if (name === "./crossings") return crossings;
      if (name === "./crossingAnalysis") return crossingAnalysis;
      if (name === "./analysisOverlay") return analysisOverlay;
      if (name === "./polygonEditing") return polygonEditing;
      if (name === "./polygonGeometry") return polygonGeometry;
      if (name === "./grid") return grid;
      if (name === "./viewport") return viewport;
      if (name === "./colors") return colors;
      if (name === "@/components/ui/actionIcons") return { actionIcons: {} };
      if (name.endsWith(".vue")) return {};
      throw new Error(`Unexpected import: ${name}`);
    },
  ).default;
  const state = result.setup(vue.reactive(props), {
    expose() {},
    emit: (...a) => events.push(a),
  });
  return { state, errors, events };
}
function canvas(document = doc(), settings = { snapToGrid: false }) {
  if (settings)
    document.gridSettings = { ...grid.DEFAULT_GRID_SETTINGS, ...settings };
  const c = component("./SketchCanvas.vue", {
    document,
    process: false,
    highlighted: [],
    highlightedLinks: [],
  });
  c.state.svg.value = {
    createSVGPoint: () => ({
      x: 0,
      y: 0,
      matrixTransform() {
        return { x: this.x, y: this.y };
      },
    }),
    getScreenCTM: () => ({
      inverse() {
        return {};
      },
    }),
    setPointerCapture() {},
    hasPointerCapture() {
      return false;
    },
  };
  return c;
}
const event = (x, y) => ({ clientX: x, clientY: y, button: 0, pointerId: 1 });
test("draw, close, and drag polygon; reject a self-intersecting vertex move", () => {
  const { state: s, errors } = canvas();
  s.tool.value = "polygon";
  for (const [x, y] of [
    [100, 100],
    [200, 100],
    [200, 200],
    [100, 200],
  ])
    s.background(event(x, y));
  s.finish();
  assert.equal(s.polygon.value.length, 0);
  assert.equal(s.facility.value.vertices.length, 4);
  s.tool.value = "select";
  const id = s.facility.value.id;
  s.start(event(150, 150), "facility", id);
  s.move(event(200, 180));
  s.stop(event(200, 180));
  assert.equal(s.facility.value.vertices[0].x, 150);
  assert.equal(s.facility.value.vertices[0].y, 130);
  s.start(event(150, 130), "vertex", id, 0);
  s.move(event(300, 200));
  s.stop(event(300, 200));
  assert.ok(errors.length);
  assert.equal(s.facility.value.vertices[0].x, 150);
});
test("connect circle, polygon vertex and midpoint; anchors survive dragging", () => {
  const d = doc();
  d.facilities = [
    {
      id: "a",
      name: "gate",
      kind: "circle",
      center: { x: 30, y: 40 },
      radius: 10,
      vertices: [],
    },
    {
      id: "b",
      name: "facility",
      kind: "polygon",
      center: { x: 0, y: 0 },
      radius: 10,
      vertices: [
        { x: 100, y: 100 },
        { x: 200, y: 100 },
        { x: 200, y: 200 },
      ],
    },
  ];
  const { state: s } = canvas(d);
  s.tool.value = "link";
  s.pickPort({ facilityId: "a", kind: "center", index: 0 });
  s.background(event(50, 150));
  s.pickPort({ facilityId: "b", kind: "midpoint", index: 0 });
  assert.equal(d.links.length, 1);
  assert.equal(d.links[0].anchors[0].y, 150);
  assert.equal(geometry.portPoint(d.links[0].to, d.facilities).x, 150);
  s.tool.value = "select";
  s.start(event(50, 150), "anchor", d.links[0].id, 0);
  s.move(event(60, 170));
  s.stop(event(60, 170));
  assert.equal(d.links[0].anchors[0].y, 170);
  assert.match(
    geometry.curvePath(geometry.linkPoints(d.links[0], d.facilities)),
    / A /,
  );
});
test("deletion protects referenced facilities and removes unreferenced incident links", () => {
  const d = doc();
  d.facilities = [
    {
      id: "a",
      name: "gate",
      kind: "circle",
      center: { x: 50, y: 50 },
      radius: 10,
      vertices: [],
    },
  ];
  d.links = [{ id: "l", from: { facilityId: "a" }, to: { facilityId: "b" } }];
  d.plans = [{ chains: [{ steps: [{ facilityId: "a" }] }] }];
  const { state: s, errors } = canvas(d);
  s.selected.value = "a";
  s.remove();
  assert.equal(d.facilities.length, 1);
  assert.equal(errors.length, 1);
  d.plans = [];
  s.remove();
  assert.equal(d.facilities.length, 0);
  assert.equal(d.links.length, 0);
});
test("self-intersecting polygons and zero area are rejected; straight links stay straight", () => {
  assert.equal(
    geometry.simplePolygon([
      { x: 0, y: 0 },
      { x: 10, y: 10 },
      { x: 0, y: 10 },
      { x: 10, y: 0 },
    ]),
    false,
  );
  assert.equal(
    geometry.simplePolygon([
      { x: 0, y: 0 },
      { x: 5, y: 0 },
      { x: 10, y: 0 },
    ]),
    false,
  );
  assert.equal(
    geometry.curvePath([
      { x: 0, y: 0 },
      { x: 10, y: 10 },
    ]),
    "M 0 0 L 10 10",
  );
});
const main = (api) =>
  component(
    "./SketchLayoutMain.vue",
    {},
    {
      "@/utils/axios": { default: api },
      "vue-router": {
        useRouter: () => ({ push() {} }),
        onBeforeRouteLeave() {},
      },
      "@/stores/auth": {
        useAuthStore: () => ({ hydrateFromStorage() {}, username: "test" }),
      },
    },
  ).state;
test("save marks the submitted document clean despite JSON property reordering", async () => {
  let resolve;
  const s = main({
    put: () => new Promise((r) => (resolve = r)),
    get: async () => ({ data: [] }),
  });
  s.accept({ id: "s", revision: 1, document: doc() });
  s.doc.value.description = "changed";
  const saving = s.save();
  resolve({
    data: {
      id: "s",
      revision: 2,
      document: {
        plans: [],
        links: [],
        facilities: [],
        description: "changed",
        name: "test",
      },
    },
  });
  assert.equal(await saving, true);
  assert.equal(s.dirty.value, false);
  s.doc.value.name = "new";
  s.record();
  s.undo();
  assert.equal(s.doc.value.name, "test");
  s.redo();
  assert.equal(s.doc.value.name, "new");
});
test("in-flight edits remain dirty after saving an earlier snapshot", async () => {
  let resolve;
  const s = main({
    put: () => new Promise((r) => (resolve = r)),
    get: async () => ({ data: [] }),
  });
  s.accept({ id: "s", revision: 1, document: doc() });
  s.doc.value.name = "submitted";
  const saving = s.save();
  s.doc.value.name = "later";
  resolve({
    data: { id: "s", revision: 2, document: { ...doc(), name: "submitted" } },
  });
  await saving;
  assert.equal(s.dirty.value, true);
});
test("process drafting only accepts connected steps and honors alternate links", () => {
  const s = main({});
  const d = doc();
  d.facilities = [{ id: "a" }, { id: "b" }, { id: "c" }];
  d.links = [{ id: "ab", from: { facilityId: "a" }, to: { facilityId: "b" } }];
  s.accept({ id: "s", revision: 1, document: d });
  s.newPlan();
  s.addChain();
  s.pick("a");
  s.pick("c");
  assert.equal(s.chain.value.steps.length, 1);
  s.pick("b");
  assert.equal(s.chain.value.steps.length, 2);
  assert.equal(s.chain.value.steps[1].incomingLinkId, "ab");
  s.newPlan(true);
  assert.equal(s.doc.value.plans.length, 2);
  assert.notEqual(
    s.doc.value.plans[0].chains[0].id,
    s.doc.value.plans[1].chains[0].id,
  );
});

test("parallel chains share plan quantity while steps and selection remain independent", async () => {
  const s = main({});
  const d = doc();
  d.facilities = [
    { id: "a", name: "入口" },
    { id: "b", name: "工位" },
  ];
  d.links = [{ id: "ab", from: { facilityId: "a" }, to: { facilityId: "b" } }];
  s.accept({ id: "s", revision: 1, document: d });
  s.newPlan();
  assert.equal(s.plan.value.executionMode, "parallel");
  s.addChain();
  const firstId = s.chain.value.id;
  s.pick("a");
  s.pick("b");
  s.plan.value.quantity = 12;
  s.chain.value.steps[1].dwellSeconds = 80;
  s.record();
  const first = JSON.stringify(s.chain.value);
  s.addChain();
  const secondId = s.chain.value.id;
  s.pick("b");
  s.pick("a");
  s.plan.value.quantity = 7;
  s.chain.value.steps[1].travelSeconds = 25;
  s.record();
  assert.equal(s.plan.value.quantity, 7);
  assert.ok(s.plan.value.chains.every((c) => !("quantity" in c)));
  assert.equal(s.draftChains.value, 0);
  assert.equal(s.chainDuration(s.chain.value), 85);
  assert.equal(s.chainDwell(s.chain.value), 60);
  assert.equal(s.chainTravel(s.chain.value), 25);
  assert.equal(JSON.stringify(s.plan.value.chains[0]), first);
  s.selectChain(firstId);
  assert.equal(s.plan.value.quantity, 7);
  assert.deepEqual(Array.from(s.highlighted.value), ["a", "b"]);
  s.addChain(true);
  const copyId = s.chain.value.id;
  assert.notEqual(copyId, firstId);
  s.chain.value.steps[1].dwellSeconds = 99;
  s.record();
  assert.equal(JSON.stringify(s.plan.value.chains[0]), first);
  await s.deleteChain();
  await vue.nextTick();
  assert.equal(s.plan.value.chains.length, 2);
  assert.ok(s.chain.value);
  assert.equal(s.plan.value.chains[1].id, secondId);
  s.undo();
  await vue.nextTick();
  assert.equal(
    s.plan.value.chains.find((c) => c.id === copyId).steps[1].dwellSeconds,
    99,
  );
  s.redo();
  await vue.nextTick();
  assert.equal(s.plan.value.chains.length, 2);
  s.newPlan(true);
  assert.equal(s.plan.value.chains.length, 2);
  assert.ok(
    s.plan.value.chains.every((c) => ![firstId, secondId].includes(c.id)),
  );
  s.chain.value.steps[0].dwellSeconds = 200;
  assert.equal(JSON.stringify(s.doc.value.plans[0].chains[0]), first);
});

test("bulk plan quantities apply atomically by ID, update traffic, support one undo, and save together", async () => {
  let submitted;
  const s = main({
    put: async (_url, body) => {
      submitted = body.document;
      return { data: { id: "s", revision: 2, document: submitted } };
    },
    get: async () => ({ data: [] }),
  });
  s.accept({ id: "s", revision: 1, document: riskFixture() });
  s.planId.value = "p2";
  await vue.nextTick();
  const chainId = s.chainId.value;
  const originalChains = JSON.stringify(s.doc.value.plans.map(p => p.chains));
  s.result.value = { summary: "previous analysis" };
  s.openQuantitySettings();
  assert.equal(s.quantitySettingsOpen.value, true);
  s.quantityDraft.value.reverse();
  s.quantityDraft.value.find(p => p.id === "p").quantity = 10;
  s.quantityDraft.value.find(p => p.id === "p2").quantity = 25;
  assert.deepEqual(Array.from(s.doc.value.plans, p => p.quantity), [2, 3]);
  assert.equal(s.dirty.value, false);
  s.applyQuantitySettings();
  assert.equal(s.quantitySettingsOpen.value, false);
  assert.deepEqual(Array.from(s.doc.value.plans, p => p.quantity), [10, 25]);
  assert.equal(s.plan.value.quantity, 25);
  assert.equal(s.chainId.value, chainId);
  assert.equal(s.result.value, undefined);
  assert.equal(s.past.value.length, 1);
  const traffic = crossingAnalysis.crossingTraffic(s.doc.value);
  assert.equal(traffic.get("ab").passages, 35);
  assert.equal(traffic.get("cd").passages, 25);
  assert.equal(JSON.stringify(s.doc.value.plans.map(p => p.chains)), originalChains);
  s.undo();
  assert.deepEqual(Array.from(s.doc.value.plans, p => p.quantity), [2, 3]);
  assert.equal(s.dirty.value, false);
  s.redo();
  assert.deepEqual(Array.from(s.doc.value.plans, p => p.quantity), [10, 25]);
  await s.save();
  assert.deepEqual(Array.from(submitted.plans, p => p.quantity), [10, 25]);
  assert.equal(s.dirty.value, false);
});

test("bulk quantity cancellation and unchanged apply leave documents, history, and analysis untouched", () => {
  const s = main({});
  s.accept({ id: "s", revision: 1, document: riskFixture() });
  const original = JSON.stringify(s.doc.value);
  s.result.value = { summary: "previous analysis" };
  s.openQuantitySettings();
  s.quantityDraft.value[0].quantity = 99;
  s.cancelQuantitySettings();
  s.applyQuantitySettings();
  assert.equal(JSON.stringify(s.doc.value), original);
  s.openQuantitySettings();
  assert.equal(s.quantityDraft.value[0].quantity, 2);
  s.applyQuantitySettings();
  assert.equal(JSON.stringify(s.doc.value), original);
  assert.equal(s.dirty.value, false);
  assert.equal(s.past.value.length, 0);
  assert.equal(s.result.value.summary, "previous analysis");
});

test("bulk quantities reject any invalid row without partially applying other rows", () => {
  const s = main({});
  s.accept({ id: "s", revision: 1, document: riskFixture() });
  s.openQuantitySettings();
  s.quantityDraft.value[0].quantity = 42;
  for (const invalid of [undefined, null, NaN, Infinity, 0, -1, 1.5, 1000001]) {
    s.quantityDraft.value[1].quantity = invalid;
    s.applyQuantitySettings();
    assert.equal(s.quantitySettingsOpen.value, true);
    assert.deepEqual(Array.from(s.doc.value.plans, p => p.quantity), [2, 3]);
    assert.equal(s.past.value.length, 0);
  }
  s.quantityDraft.value[0].quantity = 1;
  s.quantityDraft.value[1].quantity = 1000000;
  s.applyQuantitySettings();
  assert.deepEqual(Array.from(s.doc.value.plans, p => p.quantity), [1, 1000000]);
});

test("quantity dialog handles empty plans, busy state, list changes, and scheme switches safely", () => {
  const s = main({});
  s.openQuantitySettings();
  assert.equal(s.quantitySettingsOpen.value, false);
  s.accept({ id: "empty", revision: 1, document: doc() });
  s.openQuantitySettings();
  assert.equal(s.quantitySettingsOpen.value, false);
  s.accept({ id: "s", revision: 1, document: riskFixture() });
  s.busy.value = true;
  s.openQuantitySettings();
  assert.equal(s.quantitySettingsOpen.value, false);
  s.busy.value = false;
  s.openQuantitySettings();
  s.quantityDraft.value[0].quantity = 42;
  s.busy.value = true;
  s.applyQuantitySettings();
  assert.equal(s.doc.value.plans[0].quantity, 2);
  s.busy.value = false;
  s.doc.value.plans.pop();
  s.applyQuantitySettings();
  assert.equal(s.doc.value.plans[0].quantity, 2);
  s.accept({ id: "other", revision: 1, document: riskFixture() });
  assert.equal(s.quantitySettingsOpen.value, false);
  s.applyQuantitySettings();
  assert.deepEqual(Array.from(s.doc.value.plans, p => p.quantity), [2, 3]);
});

test("legacy single-chain documents stay clean and save all added peers", async () => {
  let submitted;
  const s = main({
    put: async (_url, body) => {
      submitted = body.document;
      return { data: { id: "s", revision: 2, document: submitted } };
    },
    get: async () => ({ data: [] }),
  });
  const d = doc();
  d.plans = [
    {
      id: "p",
      name: "旧方案",
      chains: [{ id: "c", name: "旧链路", quantity: 4, steps: [] }],
    },
  ];
  s.accept({ id: "s", revision: 1, document: d });
  await vue.nextTick();
  assert.equal(s.dirty.value, false);
  assert.equal(s.chain.value.id, "c");
  assert.equal(s.plan.value.quantity, 4);
  s.addChain();
  s.plan.value.quantity = 9;
  await s.save();
  assert.equal(submitted.plans.length, 1);
  assert.equal(submitted.plans[0].quantity, 9);
  assert.equal(submitted.plans[0].chains.length, 2);
  assert.ok(submitted.plans[0].chains.every((c) => !("quantity" in c)));
  assert.equal(s.dirty.value, false);
});

test("chain limits prevent unsupported copies without altering existing work", () => {
  const s = main({});
  s.accept({ id: "s", revision: 1, document: doc() });
  s.newPlan();
  s.addChain();
  s.chain.value.steps = Array.from({ length: 100 }, () => ({
    facilityId: "a",
    dwellSeconds: 0,
    travelSeconds: 0,
    incomingLinkId: null,
  }));
  for (let i = 0; i < 9; i++) s.addChain(true);
  assert.equal(s.plan.value.chains.length, 10);
  s.addChain(true);
  assert.equal(s.plan.value.chains.length, 10);
  s.pick("a");
  assert.equal(s.chain.value.steps.length, 100);
  for (let i = 0; i < 95; i++) s.addChain();
  assert.equal(s.plan.value.chains.length, 100);
});

test("shared plan quantity migration removes chain counts and honors an explicit plan count", () => {
  const d = doc();
  d.plans = [
    { id: "old", chains: [{ quantity: 4, steps: [] }, { quantity: 9, steps: [] }] },
    { id: "explicit", quantity: 7, chains: [{ quantity: 2, steps: [] }] },
    { id: "empty", chains: [] },
  ];
  types.normalizePlanQuantities(d);
  assert.deepEqual(d.plans.map((p) => p.quantity), [4, 7, 1]);
  assert.ok(d.plans.flatMap((p) => p.chains).every((c) => !("quantity" in c)));
  d.plans[0].chains.shift();
  types.normalizePlanQuantities(d);
  assert.equal(d.plans[0].quantity, 4);
});

test("editing a plan count updates all route traffic and supports history and plan copies", () => {
  const s = main({});
  s.accept({ id: "s", revision: 1, document: riskFixture() });
  s.planId.value = "p2";
  s.plan.value.quantity = 5;
  s.record();
  let traffic = crossingAnalysis.crossingTraffic(s.doc.value);
  assert.equal(traffic.get("ab").passages, 7);
  assert.equal(traffic.get("cd").passages, 5);
  s.undo();
  assert.equal(s.plan.value.quantity, 3);
  s.redo();
  assert.equal(s.plan.value.quantity, 5);
  s.newPlan(true);
  assert.equal(s.plan.value.quantity, 5);
  s.plan.value.quantity = 8;
  assert.equal(s.doc.value.plans[1].quantity, 5);
  assert.ok(s.doc.value.plans.flatMap((p) => p.chains).every((c) => !("quantity" in c)));
});

test("analysis combines every plan and blocks a draft in any plan", () => {
  const d = doc();
  const plan = {
    id: "p",
    name: "第一组",
    quantity: 3,
    chains: [{ id: "a", name: "同名链路", steps: [{}, {}] }],
  };
  const other = {
    id: "other",
    name: "第二组",
    quantity: 7,
    chains: [{ id: "a", name: "同名链路", steps: [{}] }],
  };
  d.plans = [plan, other];
  const { state: s } = component("./SketchAnalysis.vue", {
    document: d,
    busy: false,
    dirty: false,
  });
  assert.equal(s.totalQuantity.value, 10);
  assert.equal(s.ready.value, false);
  assert.equal(s.chainName("p", "a"), "第一组 / 同名链路");
  assert.equal(s.chainName("other", "a"), "第二组 / 同名链路");
  assert.equal(s.draftCount.value, 1);
  vue.reactive(other).chains[0].steps.push({});
  assert.equal(s.ready.value, true);
  vue.reactive(d).plans = [];
  assert.equal(s.ready.value, false);
});

test("shared analysis settings migrate without dirtiness and remain independent of plan edits", async () => {
  const d = doc();
  d.plans = [
    { id: "p", name: "old", horizonSeconds: 200, aThreshold: 0.9, chains: [] },
  ];
  assert.equal(types.resolveAnalysisSettings(d).horizonSeconds, 200);
  d.plans.push({ ...d.plans[0], id: "p2" });
  assert.equal(types.resolveAnalysisSettings(d).aThreshold, 0.9);
  d.plans[1].horizonSeconds = 400;
  assert.equal(types.resolveAnalysisSettings(d).horizonSeconds, 3600);
  d.plans.reverse();
  assert.equal(types.resolveAnalysisSettings(d).horizonSeconds, 3600);
  const s = main({});
  s.accept({ id: "s", revision: 1, document: d });
  assert.equal(s.dirty.value, false);
  const { state: analysis, events } = component("./SketchAnalysis.vue", {
    document: s.doc.value,
    busy: false,
    dirty: false,
  });
  analysis.updateSettings("horizonSeconds", 90);
  s.record();
  assert.equal(s.doc.value.analysisSettings.horizonSeconds, 90);
  assert.equal(s.doc.value.plans[0].horizonSeconds, 400);
  assert.equal(events.filter((e) => e[0] === "change").length, 1);
  s.undo();
  assert.equal(s.doc.value.analysisSettings.horizonSeconds, 3600);
  s.redo();
  assert.equal(s.doc.value.analysisSettings.horizonSeconds, 90);
  s.newPlan();
  assert.equal(s.doc.value.analysisSettings.horizonSeconds, 90);
  assert.equal(s.plan.value.horizonSeconds, undefined);
});

test("analysis requests the whole layout regardless of the current editing plan", async () => {
  const calls = [];
  let submitted;
  const output = {
    scope: "all-plans",
    planCount: 2,
    chainCount: 2,
    jobCount: 10,
  };
  const s = main({
    post: async (url) => {
      calls.push(url);
      return { data: output };
    },
    put: async (_url, request) => {
      submitted = request.document;
      return { data: { id: "s", revision: 2, document: submitted } };
    },
    get: async () => ({ data: [] }),
  });
  const d = doc();
  d.plans = [
    {
      id: "p",
      name: "first",
      chains: [{ id: "same", quantity: 3, steps: [{}, {}] }],
    },
    {
      id: "p2",
      name: "second",
      chains: [{ id: "same", quantity: 7, steps: [{}, {}] }],
    },
  ];
  s.accept({ id: "s", revision: 1, document: d });
  await vue.nextTick();
  await s.analyze();
  assert.equal(calls[0], "/SketchLayout/schemes/s/analysis");
  assert.equal(s.result.value.scope, "all-plans");
  s.planId.value = "p2";
  await vue.nextTick();
  assert.equal(s.result.value.scope, "all-plans");
  s.doc.value.plans[0].quantity = 4;
  assert.equal(s.result.value, undefined);
  await s.analyze();
  assert.equal(submitted.plans.length, 2);
  assert.deepEqual(
    Array.from(submitted.plans, (p) => p.quantity),
    [4, 7],
  );
  assert.equal(calls[1], "/SketchLayout/schemes/s/analysis");
});

test("legacy schemes use StationLayout grid defaults without becoming dirty", () => {
  const d = doc(),
    { state: s, events } = canvas(d, null);
  assert.equal(s.grid.value.spacing, 20);
  assert.equal(s.grid.value.snapToGrid, true);
  assert.equal(d.gridSettings, undefined);
  s.updateGrid({ spacing: 35.5, showGrid: false });
  assert.equal(d.gridSettings.spacing, 35.5);
  assert.equal(d.gridSettings.snapToGrid, true);
  assert.equal(events.filter((e) => e[0] === "change").length, 1);
  s.updateGridSpacing(undefined);
  assert.equal(d.gridSettings.spacing, 35.5);
});

test("polygon creation and cursor preview snap to the same world grid even when hidden", () => {
  const { state: s } = canvas(doc(), { spacing: 20, showGrid: false });
  s.tool.value = "polygon";
  s.move(event(113, 107));
  s.background(event(113, 107));
  assert.equal(s.cursor.value.x, 120);
  assert.equal(s.polygon.value[0].x, s.cursor.value.x);
  assert.equal(s.polygon.value[0].y, 100);
  s.updateGrid({ spacing: 50 });
  s.background(event(176, 163));
  assert.equal(s.polygon.value[1].x, 200);
  assert.equal(s.polygon.value[1].y, 150);
  s.updateGrid({ snapToGrid: false });
  s.background(event(126.25, 82.75));
  assert.equal(s.polygon.value[2].x, 126.25);
});

test("circle placement snaps without a fixed canvas edge", () => {
  const d = doc(),
    { state: s } = canvas(d, { spacing: 20 });
  s.tool.value = "circle";
  s.background(event(0, 0));
  assert.equal(d.facilities[0].center.x, 0);
  assert.equal(d.facilities[0].center.y, 0);
  s.background(event(2400, 1600));
  assert.equal(d.facilities[1].center.x, 2400);
  assert.equal(d.facilities[1].center.y, 1600);
  s.background(event(-10007, 25009));
  nearPoint(d.facilities[2].center, -10000, 25000);
});

test("moving a facility snaps its reference point without distorting its polygon", () => {
  const d = doc();
  d.facilities = [
    {
      id: "p",
      name: "polygon",
      kind: "polygon",
      center: { x: 0, y: 0 },
      radius: 24,
      vertices: [
        { x: 113, y: 107 },
        { x: 203, y: 119 },
        { x: 187, y: 211 },
        { x: 101, y: 196 },
      ],
    },
  ];
  const { state: s } = canvas(d, { spacing: 20 });
  s.start(event(150, 150), "facility", "p");
  s.move(event(150, 150));
  s.stop(event(150, 150));
  assert.equal(
    d.facilities[0].vertices[0].x,
    113,
    "selection must not move the polygon",
  );
  s.start(event(150, 150), "facility", "p");
  s.move(event(163, 163));
  s.stop(event(163, 163));
  assert.equal(d.facilities[0].vertices[0].x, 120);
  assert.equal(d.facilities[0].vertices[0].y, 120);
  assert.equal(
    d.facilities[0].vertices[1].x - d.facilities[0].vertices[0].x,
    90,
  );
  assert.equal(
    d.facilities[0].vertices[1].y - d.facilities[0].vertices[0].y,
    12,
  );
});

test("vertex editing and curve anchors follow the grid; linked ports remain exact", () => {
  const d = doc();
  d.facilities = [
    {
      id: "a",
      name: "a",
      kind: "circle",
      center: { x: 33, y: 47 },
      radius: 10,
      vertices: [],
    },
    {
      id: "b",
      name: "b",
      kind: "polygon",
      center: { x: 0, y: 0 },
      radius: 24,
      vertices: [
        { x: 100, y: 100 },
        { x: 200, y: 100 },
        { x: 200, y: 200 },
        { x: 100, y: 200 },
      ],
    },
  ];
  const { state: s } = canvas(d, { spacing: 20 });
  s.tool.value = "link";
  s.pickPort({ facilityId: "a", kind: "center", index: 0 });
  s.background(event(61, 157));
  s.pickPort({ facilityId: "b", kind: "midpoint", index: 0 });
  assert.equal(d.links[0].anchors[0].x, 60);
  assert.equal(d.links[0].anchors[0].y, 160);
  assert.equal(
    geometry.portPoint(d.links[0].from, d.facilities).x,
    33,
    "existing ports are not resnapped",
  );
  s.tool.value = "select";
  s.start(event(60, 160), "anchor", d.links[0].id, 0);
  s.move(event(86, 178));
  s.stop(event(86, 178));
  assert.equal(d.links[0].anchors[0].x, 80);
  assert.equal(d.links[0].anchors[0].y, 180);
  s.start(event(100, 100), "vertex", "b", 0);
  s.move(event(112, 107));
  s.stop(event(112, 107));
  assert.equal(d.facilities[1].vertices[0].x, 120);
  assert.equal(d.facilities[1].vertices[0].y, 100);
  s.addAnchor(event(131, 91), d.links[0].id);
  assert.ok(d.links[0].anchors.some((p) => p.x === 140 && p.y === 100));
});

test("grid origin and zoom decimation do not change snap spacing", () => {
  const settings = grid.gridSettings({ spacing: 20, originX: 5, originY: 7 });
  const p = grid.gridPoint({ x: 33, y: 31 }, settings);
  assert.equal(p.x, 25);
  assert.equal(p.y, 27);
  assert.equal(grid.displayGridSpacing(20, 0.1), 80);
  assert.equal(grid.displayGridSpacing(20, 2), 20);
  const { state: s } = canvas(doc(), settings);
  s.tool.value = "polygon";
  s.canvasSize.value = { width: 240, height: 160 };
  s.zoom(2);
  assert.equal(s.visibleGridSpacing.value, 160);
  s.svg.value.createSVGPoint = () => ({
    x: 0,
    y: 0,
    matrixTransform() {
      return { x: this.x * 2 + 5, y: this.y * 2 + 7 };
    },
  });
  s.background(event(16, 12));
  assert.equal(s.polygon.value[0].x, 45);
  assert.equal(s.polygon.value[0].y, 27);
});

test("grid preferences participate in undo and redo", () => {
  const s = main({});
  s.accept({ id: "s", revision: 1, document: doc() });
  s.doc.value.gridSettings = {
    ...grid.DEFAULT_GRID_SETTINGS,
    spacing: 50,
    showGrid: false,
  };
  s.record();
  s.undo();
  assert.equal(s.doc.value.gridSettings, undefined);
  s.redo();
  assert.equal(s.doc.value.gridSettings.spacing, 50);
  assert.equal(s.doc.value.gridSettings.showGrid, false);
});

test("clicking back into the first grid cell closes without duplicating the first vertex", () => {
  for (const showGrid of [true, false]) {
    const d = doc();
    const { state: s, errors } = canvas(d, { spacing: 20, showGrid });
    s.tool.value = "polygon";
    for (const [x, y] of [
      [100, 100],
      [200, 100],
      [200, 200],
      [100, 200],
    ])
      s.background(event(x, y));
    // Outside the first vertex's 12-unit hit circle, but snaps back to (100, 100).
    s.background(event(109, 109));
    assert.equal(d.facilities.length, 1);
    assert.equal(d.facilities[0].vertices.length, 4);
    assert.equal(s.polygon.value.length, 0);
    assert.equal(errors.length, 0);
  }
});

test("repeated clicks in one grid cell do not create zero-length polygon edges", () => {
  for (const close of ["enter", "button"]) {
    const d = doc();
    const { state: s, errors } = canvas(d, { spacing: 50 });
    s.tool.value = "polygon";
    for (const [x, y] of [
      [100, 100],
      [119, 118],
      [200, 100],
      [211, 113],
      [200, 200],
      [213, 209],
      [100, 200],
      [117, 217],
    ])
      s.background(event(x, y));
    assert.equal(s.polygon.value.length, 4);
    if (close === "enter") s.keyboard({ key: "Enter" });
    else s.finish();
    assert.equal(d.facilities.length, 1);
    assert.equal(d.facilities[0].vertices.length, 4);
    assert.equal(errors.length, 0);
  }
});

test("snapped closure still rejects crossing edges, zero area and too few vertices", () => {
  for (const points of [
    [
      [100, 100],
      [200, 200],
      [100, 200],
      [200, 100],
    ],
    [
      [100, 100],
      [200, 100],
      [300, 100],
    ],
    [
      [100, 100],
      [200, 100],
    ],
  ]) {
    const d = doc();
    const { state: s, errors } = canvas(d, { spacing: 20 });
    s.tool.value = "polygon";
    for (const [x, y] of points) s.background(event(x, y));
    s.background(event(109, 109));
    assert.equal(d.facilities.length, 0);
    assert.equal(s.polygon.value.length, points.length);
    assert.equal(errors.length, 1);
  }
});

const propertyFixture = () => {
  const polygon = {
    id: "p",
    name: "库区",
    kind: "polygon",
    radius: 24,
    center: { x: 0, y: 0 },
    vertices: [
      { x: 100, y: 100 },
      { x: 300, y: 100 },
      { x: 300, y: 300 },
      { x: 100, y: 300 },
    ],
  };
  const circle = {
    id: "c",
    name: "入口",
    kind: "circle",
    radius: 20,
    center: { x: 50, y: 200 },
    vertices: [],
  };
  const link = {
    id: "l",
    name: "通道",
    from: { facilityId: "c", kind: "center", index: 0 },
    to: { facilityId: "p", kind: "midpoint", index: 3 },
    anchors: [],
  };
  return vue.reactive({
    ...doc(),
    facilities: [polygon, circle],
    links: [link],
  });
};
const properties = (d, index = 0) =>
  component("./SketchObjectProperties.vue", {
    facility: index >= 0 ? d.facilities[index] : undefined,
    link: index < 0 ? d.links[0] : undefined,
    facilities: d.facilities,
  });

test("canvas selects the matching property object without mutating the document", () => {
  const d = propertyFixture();
  const { state: s, events } = canvas(d);
  s.selectFacility(event(200, 200), "p");
  assert.equal(s.facility.value.kind, "polygon");
  assert.equal(s.link.value, undefined);
  s.selectFacility(event(50, 200), "c");
  assert.equal(s.facility.value.kind, "circle");
  s.selectLink(event(75, 200), "l");
  assert.equal(s.link.value.id, "l");
  assert.equal(s.facility.value, undefined);
  s.background(event(600, 600));
  assert.equal(s.selected.value, "");
  assert.equal(events.length, 0);
});

test("property names trim whitespace, reject empty names and resync after external edits", async () => {
  const d = propertyFixture();
  const { state: s, errors, events } = properties(d);
  s.name.value = " 新库区 ";
  s.applyName();
  assert.equal(d.facilities[0].name, "新库区");
  s.name.value = "  ";
  s.applyName();
  assert.equal(s.name.value, "新库区");
  assert.equal(events.length, 1);
  assert.equal(errors.length, 1);
  d.facilities[0].name = "撤销后的名称";
  await vue.nextTick();
  assert.equal(s.name.value, "撤销后的名称");
});

test("polygon property edits preserve connected ports and reject invalid geometry", () => {
  const d = propertyFixture();
  const { state: s, errors, events } = properties(d);
  s.draftFacility.value.vertices[0].x = 80;
  s.applyFacility();
  assert.equal(d.facilities[0].vertices.length, 4);
  assert.equal(d.links[0].to.index, 3);
  assert.equal(geometry.portPoint(d.links[0].to, d.facilities).x, 90);
  s.draftFacility.value.vertices[0] = { x: 400, y: 200 };
  s.applyFacility();
  assert.equal(d.facilities[0].vertices[0].x, 80);
  assert.equal(s.draftFacility.value.vertices[0].x, 80);
  s.draftFacility.value.vertices[0].y = undefined;
  s.applyFacility();
  assert.equal(d.facilities[0].vertices[0].y, 100);
  assert.equal(events.length, 1);
  assert.equal(errors.length, 2);
});

test("entrance properties update the connected center and reject invalid radii", async () => {
  const d = propertyFixture();
  const { state: s, errors, events } = properties(d, 1);
  s.draftFacility.value.center.x = 75;
  s.draftFacility.value.radius = 30;
  s.applyFacility();
  assert.equal(geometry.portPoint(d.links[0].from, d.facilities).x, 75);
  assert.equal(d.facilities[1].radius, 30);
  s.draftFacility.value.radius = 201;
  s.applyFacility();
  assert.equal(d.facilities[1].radius, 30);
  assert.equal(s.draftFacility.value.radius, 30);
  assert.equal(errors.length, 1);
  assert.equal(events.length, 1);
  d.facilities[1].center.x = 100;
  await vue.nextTick();
  assert.equal(s.draftFacility.value.center.x, 100);
});

test("link properties edit curve anchors while preserving endpoint and process references", () => {
  const d = propertyFixture();
  d.plans = [
    {
      chains: [
        {
          steps: [
            { facilityId: "c" },
            { facilityId: "p", incomingLinkId: "l" },
          ],
        },
      ],
    },
  ];
  const endpoints = JSON.stringify([d.links[0].from, d.links[0].to]);
  const { state: s, errors, events } = properties(d, -1);
  s.addAnchor();
  assert.equal(d.links[0].anchors.length, 1);
  s.draftLink.value.anchors[0].y = 250;
  s.applyAnchors();
  assert.equal(d.links[0].anchors[0].y, 250);
  s.draftLink.value.anchors[0].x = NaN;
  s.applyAnchors();
  assert.ok(Number.isFinite(d.links[0].anchors[0].x));
  assert.ok(Number.isFinite(s.draftLink.value.anchors[0].x));
  s.addAnchor();
  s.removeAnchor(0);
  assert.equal(d.links[0].anchors.length, 1);
  s.straighten();
  assert.equal(d.links[0].anchors.length, 0);
  assert.equal(JSON.stringify([d.links[0].from, d.links[0].to]), endpoints);
  assert.equal(d.plans[0].chains[0].steps[1].incomingLinkId, "l");
  assert.equal(errors.length, 1);
  assert.equal(events.length, 5);
});

test("Esc cancels unfinished drawing and restores normal selection for every drawing tool", () => {
  const d = propertyFixture();
  const { state: s, events } = canvas(d);
  const original = JSON.stringify(d);
  assert.equal(s.drawing.value, false);
  s.activateTool("polygon");
  s.background(event(500, 500));
  s.background(event(700, 500));
  assert.equal(s.drawing.value, true);
  s.keyboard({ key: "Escape" });
  assert.equal(s.tool.value, "select");
  assert.equal(s.polygon.value.length, 0);
  s.activateTool("link");
  s.pickPort({ facilityId: "c", kind: "center", index: 0 });
  s.background(event(75, 400));
  s.keyboard({ key: "Escape" });
  assert.equal(s.source.value, undefined);
  assert.equal(s.anchors.value.length, 0);
  s.activateTool("circle");
  s.keyboard({ key: "Escape" });
  s.background(event(800, 800));
  assert.equal(s.drawing.value, false);
  s.selectFacility(event(200, 200), "p");
  assert.equal(s.selected.value, "p");
  assert.equal(JSON.stringify(d), original);
  assert.equal(events.length, 0);
});

test("switching drawing tools cancels only the draft; Esc keeps finished objects", () => {
  const d = doc();
  const { state: s, events } = canvas(d);
  s.activateTool("polygon");
  s.background(event(100, 100));
  s.activateTool("circle");
  assert.equal(s.polygon.value.length, 0);
  s.background(event(200, 200));
  s.keyboard({ key: "Escape" });
  assert.equal(d.facilities.length, 1);
  assert.equal(d.facilities[0].kind, "circle");
  assert.equal(s.selected.value, d.facilities[0].id);
  assert.equal(s.tool.value, "select");
  assert.equal(events.length, 1);
});

test("normal-mode background drags pan at the current zoom without drift or document edits", () => {
  const d = propertyFixture();
  const { state: s, events } = canvas(d);
  s.selected.value = "p";
  s.zoom(0.5);
  s.svg.value.getScreenCTM = () => ({ a: 0.5, b: 0, inverse: () => ({}) });
  const original = JSON.stringify(d);
  s.start(event(100, 100), "pan");
  s.move(event(125, 110));
  assert.equal(s.view.value.x, 550);
  assert.equal(s.view.value.y, 380);
  s.move(event(150, 130));
  assert.equal(s.view.value.x, 500);
  assert.equal(s.view.value.y, 340);
  s.stop(event(150, 130));
  s.background(event(150, 130));
  assert.equal(
    s.selected.value,
    "p",
    "a pan must not clear selection on pointer release",
  );
  assert.equal(JSON.stringify(d), original);
  assert.equal(events.length, 0);
  s.start(event(400, 400), "pan");
  s.stop(event(400, 400));
  assert.equal(
    s.selected.value,
    "",
    "pointer capture must not swallow background deselection",
  );
  s.background(event(400, 400));
  assert.equal(
    s.selected.value,
    "",
    "a plain background click clears selection",
  );
});

test("drawing-mode background gestures add geometry instead of panning", () => {
  const { state: s } = canvas();
  const originalView = JSON.stringify(s.view.value);
  s.activateTool("polygon");
  s.start(event(100, 100), "pan");
  s.move(event(200, 200));
  s.stop(event(200, 200));
  s.background(event(200, 200));
  assert.equal(JSON.stringify(s.view.value), originalView);
  assert.equal(s.polygon.value.length, 1);
});

const plain = (value) => JSON.parse(JSON.stringify(value));
function attachAllPorts(d, excludedVertex = -1) {
  const template = plain(d.links[0]);
  d.links = geometry
    .ports(d.facilities[0])
    .filter((p) => p.kind !== "vertex" || p.index !== excludedVertex)
    .map((port, i) => ({ ...plain(template), id: `l${i}`, to: port }));
}

test("edge insertion preserves every existing vertex and unaffected midpoint attachment, including the closing edge", () => {
  for (let edge = 0; edge < 4; edge++) {
    const d = propertyFixture();
    attachAllPorts(d);
    const before = plain(d);
    const a = before.facilities[0].vertices[edge],
      b = before.facilities[0].vertices[(edge + 1) % 4];
    const point = { x: (a.x + b.x) / 2, y: (a.y + b.y) / 2 };
    assert.equal(
      polygonEditing.insertPolygonVertex(d, "p", edge, point),
      undefined,
    );
    assert.equal(d.facilities[0].vertices.length, 5);
    for (let i = 0; i < d.links.length; i++) {
      assert.deepEqual(plain(d.links[i].from), before.links[i].from);
      assert.deepEqual(
        plain(geometry.portPoint(d.links[i].to, d.facilities)),
        plain(geometry.portPoint(before.links[i].to, before.facilities)),
      );
      if (
        before.links[i].to.kind === "midpoint" &&
        before.links[i].to.index === edge
      ) {
        assert.equal(d.links[i].to.kind, "vertex");
        assert.equal(d.links[i].to.index, edge + 1);
      }
    }
  }
});

test("splitting away from a connected midpoint keeps it on the nearer replacement edge", () => {
  for (const [x, expectedIndex, expectedX] of [
    [150, 1, 225],
    [250, 0, 175],
  ]) {
    const d = propertyFixture();
    d.links[0].to.index = 0;
    assert.equal(
      polygonEditing.insertPolygonVertex(d, "p", 0, { x, y: 105 }),
      undefined,
    );
    assert.equal(
      d.facilities[0].vertices[1].y,
      100,
      "project onto the original edge",
    );
    assert.equal(d.links[0].to.kind, "midpoint");
    assert.equal(d.links[0].to.index, expectedIndex);
    assert.equal(geometry.portPoint(d.links[0].to, d.facilities).x, expectedX);
  }
});

test("vertex deletion reindexes retained ports and joins adjacent midpoint attachments for every corner", () => {
  for (let index = 0; index < 4; index++) {
    const d = propertyFixture();
    attachAllPorts(d, index);
    const before = plain(d);
    assert.equal(polygonEditing.removePolygonVertex(d, "p", index), undefined);
    assert.equal(d.facilities[0].vertices.length, 3);
    const a = before.facilities[0].vertices[(index + 3) % 4],
      b = before.facilities[0].vertices[(index + 1) % 4];
    for (let i = 0; i < d.links.length; i++) {
      const old = before.links[i].to;
      const adjacent =
        old.kind === "midpoint" &&
        (old.index === index || old.index === (index + 3) % 4);
      const expected = adjacent
        ? { x: (a.x + b.x) / 2, y: (a.y + b.y) / 2 }
        : plain(geometry.portPoint(old, before.facilities));
      assert.deepEqual(
        plain(geometry.portPoint(d.links[i].to, d.facilities)),
        expected,
      );
      assert.deepEqual(plain(d.links[i].from), before.links[i].from);
    }
  }
});

test("invalid polygon edits and deletion of a connected vertex leave the whole document unchanged", () => {
  const d = propertyFixture();
  d.links[0].to = { facilityId: "p", kind: "vertex", index: 0 };
  let before = JSON.stringify(d);
  assert.equal(polygonEditing.removePolygonVertex(d, "p", 0), "connected");
  assert.equal(
    polygonEditing.insertPolygonVertex(d, "p", 0, { x: 100, y: 100 }),
    "invalid",
  );
  assert.equal(JSON.stringify(d), before);
  d.links = [];
  // Removing the lower-left corner of a U joins across both sides of the notch.
  d.facilities[0].vertices = [
    [100, 100],
    [500, 100],
    [500, 500],
    [400, 500],
    [400, 200],
    [200, 200],
    [200, 500],
    [100, 500],
  ].map(([x, y]) => ({ x, y }));
  assert.equal(geometry.simplePolygon(d.facilities[0].vertices), true);
  before = JSON.stringify(d);
  assert.equal(polygonEditing.removePolygonVertex(d, "p", 0), "invalid");
  assert.equal(JSON.stringify(d), before);
  d.facilities[0].vertices = [
    { x: 100, y: 100 },
    { x: 200, y: 100 },
    { x: 200, y: 200 },
  ];
  before = JSON.stringify(d);
  assert.equal(polygonEditing.removePolygonVertex(d, "p", 0), "minimum");
  assert.equal(JSON.stringify(d), before);
});

test("edge insertion respects the server's 100-vertex limit without changing endpoints", () => {
  const d = propertyFixture();
  d.facilities[0].vertices = Array.from({ length: 100 }, (_, i) => ({
    x: 200 + 100 * Math.cos((2 * Math.PI * i) / 100),
    y: 200 + 100 * Math.sin((2 * Math.PI * i) / 100),
  }));
  const before = JSON.stringify(d);
  assert.equal(
    polygonEditing.insertPolygonVertex(d, "p", 0, { x: 299, y: 203 }),
    "limit",
  );
  assert.equal(JSON.stringify(d), before);
});

test("canvas double-click inserts only near an edge and preserves sloping edges with grid snapping enabled", () => {
  const d = propertyFixture();
  d.facilities[0].vertices[1].y = 200;
  const {
    state: s,
    errors,
    events,
  } = canvas(d, { spacing: 50, snapToGrid: true });
  s.selected.value = "p";
  s.addVertexOnEdge(event(200, 230)); // interior
  s.addVertexOnEdge(event(101, 101)); // too close to existing vertex
  assert.equal(d.facilities[0].vertices.length, 4);
  s.addVertexOnEdge(event(173, 136.5));
  assert.equal(d.facilities[0].vertices.length, 5);
  assert.deepEqual(plain(d.facilities[0].vertices[1]), { x: 173, y: 136.5 });
  assert.equal(errors.length, 0);
  assert.equal(events.length, 1);
  s.activateTool("polygon");
  s.addVertexOnEdge(event(200, 150), "p");
  s.openPointMenu(event(100, 100), "vertex", "p", 0);
  assert.equal(s.pointMenu.value, undefined);
  assert.equal(d.facilities[0].vertices.length, 5);
  s.props.process = true;
  s.tool.value = "select";
  s.addVertexOnEdge(event(200, 150), "p");
  s.openPointMenu(event(100, 100), "vertex", "p", 0);
  assert.equal(s.pointMenu.value, undefined);
  assert.equal(d.facilities[0].vertices.length, 5);
});

test("right-click menus delete only their targeted point and dismiss on selection, outside click and Esc", () => {
  const d = propertyFixture();
  d.links[0].anchors = [
    { x: 75, y: 100 },
    { x: 75, y: 150 },
  ];
  const { state: s, events } = canvas(d);
  s.openPointMenu(event(120, 120), "vertex", "p", 1);
  assert.equal(s.selected.value, "p");
  s.deletePointFromMenu();
  assert.equal(d.facilities[0].vertices.length, 3);
  assert.equal(s.pointMenu.value, undefined);
  assert.equal(events.length, 1);
  s.openPointMenu(event(75, 100), "anchor", "l", 0);
  assert.equal(s.selected.value, "l");
  s.deletePointFromMenu();
  assert.deepEqual(plain(d.links[0].anchors), [{ x: 75, y: 150 }]);
  assert.equal(d.links.length, 1);
  assert.equal(events.length, 2);
  for (const close of [
    () => (s.selected.value = "p"),
    () => s.dismissPointMenu({ target: {} }),
    () => s.keyboard({ key: "Escape" }),
  ]) {
    s.openPointMenu(event(75, 150), "anchor", "l", 0);
    close();
    assert.equal(s.pointMenu.value, undefined);
    assert.equal(d.links[0].anchors.length, 1);
  }
});

test("deleting a protected vertex from its menu warns without recording a change", () => {
  const d = propertyFixture();
  d.links[0].to = { facilityId: "p", kind: "vertex", index: 0 };
  const { state: s, errors, events } = canvas(d);
  s.openPointMenu(event(100, 100), "vertex", "p", 0);
  s.deletePointFromMenu();
  assert.equal(d.facilities[0].vertices.length, 4);
  assert.equal(errors.length, 1);
  assert.equal(events.length, 0);
  assert.equal(s.pointMenu.value, undefined);
});

test("polygon edits undo and redo geometry and link indices together", () => {
  const s = main({});
  s.accept({ id: "s", revision: 1, document: propertyFixture() });
  const original = JSON.stringify(s.doc.value);
  polygonEditing.insertPolygonVertex(s.doc.value, "p", 0, { x: 200, y: 100 });
  s.record();
  const inserted = JSON.stringify(s.doc.value);
  assert.equal(s.doc.value.links[0].to.index, 4);
  s.undo();
  assert.equal(JSON.stringify(s.doc.value), original);
  s.redo();
  assert.equal(JSON.stringify(s.doc.value), inserted);
  polygonEditing.removePolygonVertex(s.doc.value, "p", 1);
  s.record();
  assert.equal(JSON.stringify(s.doc.value), original);
  s.undo();
  assert.equal(JSON.stringify(s.doc.value), inserted);
});

test("arrow midpoint handles straight, vertical, reversed and collapsed connections", () => {
  for (const [points, expected] of [
    [
      [
        { x: 0, y: 0 },
        { x: 100, y: 0 },
      ],
      { x: 50, y: 0, angle: 0 },
    ],
    [
      [
        { x: 100, y: 0 },
        { x: 0, y: 0 },
      ],
      { x: 50, y: 0, angle: 180 },
    ],
    [
      [
        { x: 10, y: 0 },
        { x: 10, y: 100 },
      ],
      { x: 10, y: 50, angle: 90 },
    ],
  ]) {
    const middle = geometry.curveMidpoint(points);
    for (const key of ["x", "y", "angle"])
      assert.ok(Math.abs(middle[key] - expected[key]) < 1e-8);
    assert.equal(middle.length, 100);
  }
  assert.equal(geometry.curveMidpoint([]), undefined);
  assert.equal(
    geometry.curveMidpoint([
      { x: 5, y: 5 },
      { x: 5, y: 5 },
      { x: 5, y: 5 },
    ]),
    undefined,
  );
});

test("right-angle corners use tangent circular fillets and arrows follow exact arc length", () => {
  const points = [
    { x: 100, y: 100 },
    { x: 300, y: 100 },
    { x: 300, y: 300 },
  ];
  const before = JSON.stringify(points);
  const g = linkGeometry.connectionGeometry(points);
  const arc = g.segments.find((s) => s.kind === "arc");
  const near = (a, b) => assert.ok(Math.abs(a - b) < 1e-8, `${a} != ${b}`);
  near(arc.radius, 100);
  near(arc.start.x, 200);
  near(arc.start.y, 100);
  near(arc.end.x, 300);
  near(arc.end.y, 200);
  near(arc.center.x, 200);
  near(arc.center.y, 200);
  near(g.middle.x, 200 + 100 / Math.sqrt(2));
  near(g.middle.y, 200 - 100 / Math.sqrt(2));
  near(g.middle.angle, 45);
  near(g.middle.length, 200 + 50 * Math.PI);
  assert.match(g.path, / A /);
  assert.doesNotMatch(g.path, / C /);
  assert.equal(JSON.stringify(points), before);
  const reverse = geometry.curveMidpoint([...points].reverse());
  near(reverse.x, g.middle.x);
  near(reverse.y, g.middle.y);
  near(Math.abs(reverse.angle - g.middle.angle), 180);
  const polyline = linkGeometry.connectionGeometry(points, false);
  assert.equal(polyline.path, "M 100 100 L 300 100 L 300 300");
  assert.deepEqual(plain(polyline.middle), {
    x: 300,
    y: 100,
    angle: 0,
    length: 400,
  });
});

test("uneven fillet segments place arrows at half total length", () => {
  const g = linkGeometry.connectionGeometry([
    { x: 0, y: 0 },
    { x: 300, y: 0 },
    { x: 300, y: 200 },
  ]);
  const angle = -Math.PI / 2 + ((300 + 50 * Math.PI) / 2 - 200) / 100;
  assert.ok(Math.abs(g.middle.x - (200 + 100 * Math.cos(angle))) < 1e-8);
  assert.ok(Math.abs(g.middle.y - (100 + 100 * Math.sin(angle))) < 1e-8);
  assert.ok(
    Math.abs(g.middle.angle - ((angle + Math.PI / 2) * 180) / Math.PI) < 1e-8,
  );
});

test("short neighboring corners shrink without overlapping and remain tangent", () => {
  const points = [
    { x: 0, y: 0 },
    { x: 100, y: 0 },
    { x: 100, y: 10 },
    { x: 200, y: 10 },
  ];
  const g = linkGeometry.connectionGeometry(points);
  const arcs = g.segments.filter((s) => s.kind === "arc");
  assert.equal(arcs.length, 2);
  assert.ok(arcs[0].end.y < arcs[1].start.y);
  assert.ok(arcs.every((s) => s.radius > 0 && s.radius < 5));
  for (let i = 1; i < g.segments.length - 1; i++) {
    const arc = g.segments[i];
    if (arc.kind !== "arc") continue;
    const incoming = g.segments[i - 1],
      outgoing = g.segments[i + 1];
    for (const [point, line] of [
      [arc.start, incoming],
      [arc.end, outgoing],
    ]) {
      const dot =
        (point.x - arc.center.x) * (line.end.x - line.start.x) +
        (point.y - arc.center.y) * (line.end.y - line.start.y);
      assert.ok(Math.abs(dot) < 1e-8);
    }
  }
  assert.doesNotMatch(g.path, /NaN|Infinity/);
});

test("duplicate, collinear and reversing anchors remain finite without spurious arcs", () => {
  for (const points of [
    [],
    [{ x: 0, y: 0 }],
    [
      { x: 0, y: 0 },
      { x: 0, y: 0 },
    ],
    [
      { x: 0, y: 0 },
      { x: 50, y: 0 },
      { x: 100, y: 0 },
    ],
    [
      { x: 0, y: 0 },
      { x: 100, y: 0 },
      { x: 0, y: 0 },
    ],
  ]) {
    const g = linkGeometry.connectionGeometry(points);
    assert.doesNotMatch(g.path, /NaN|Infinity| A /);
    assert.ok(g.segments.every((s) => s.length > 0));
  }
  const duplicate = [
    { x: 0, y: 0 },
    { x: 100, y: 0 },
    { x: 100, y: 0 },
    { x: 100, y: 100 },
  ];
  assert.match(geometry.curvePath(duplicate), / A /);
});

test("link name display is undoable, preserves content, and cannot be changed in read-only canvases", () => {
  const d = propertyFixture(), c = canvas(d), m = main({});
  const original = JSON.stringify({ facilities: d.facilities, links: d.links, plans: d.plans });
  m.accept({ id: "s", revision: 1, document: d });
  assert.equal(c.state.showLinkNames.value, false);
  c.state.updateLinkNameDisplay(false);
  assert.equal(c.events.length, 0);
  c.state.updateLinkNameDisplay(true);
  c.state.updateLinkNameDisplay(true);
  assert.equal(d.showLinkNames, true);
  assert.equal(c.events.length, 1);
  m.doc.value.showLinkNames = true;
  m.record();
  m.undo();
  assert.equal(m.doc.value.showLinkNames ?? false, false);
  m.redo();
  assert.equal(m.doc.value.showLinkNames, true);
  for (const mode of ["process", "analysisMode"]) {
    const ro = component("./SketchCanvas.vue", { document: d, [mode]: true, highlighted: [], highlightedLinks: [] });
    assert.equal(ro.state.showLinkNames.value, true);
    ro.state.updateLinkNameDisplay(false);
    assert.equal(d.showLinkNames, true);
    assert.equal(ro.events.length, 0);
  }
  c.state.updateLinkNameDisplay(false);
  assert.equal(d.showLinkNames, false);
  assert.equal(c.events.length, 2);
  assert.equal(JSON.stringify({ facilities: d.facilities, links: d.links, plans: d.plans }), original);
});

test("link labels show all or only selected names and preserve inline rename and process steps", () => {
  const d = propertyFixture();
  d.links.push({ ...plain(d.links[0]), id: "second", name: "另一通道" });
  const { descriptor } = parse(readFileSync(new URL("./SketchCanvas.vue", import.meta.url), "utf8"));
  const compiled = compileScript(descriptor, { id: "link-names" });
  const template = compileTemplate({
    source: descriptor.template.content, filename: "SketchCanvas.vue", id: "link-names",
    compilerOptions: { bindingMetadata: compiled.bindings },
  });
  assert.deepEqual(template.errors, []);
  const render = evaluate(template.code, name => {
    assert.equal(name, "vue");
    return { ...vue, withDirectives: node => node, resolveComponent: tag => tag };
  }).render;
  function labels(s, extra = {}) {
    const props = { document: d, process: false, analysisMode: false, highlighted: [], highlightedLinks: [], ...extra };
    const root = render(vue.proxyRefs(s), [], props, vue.proxyRefs(s), {}, {});
    const found = [];
    function visit(node) {
      if (node?.props?.class === "link-label") found.push(node);
      if (Array.isArray(node?.children)) node.children.forEach(visit);
    }
    visit(root);
    return found;
  }
  const { state: s } = canvas(d);
  assert.equal(labels(s).length, 0);
  s.selected.value = "l";
  assert.equal(labels(s).length, 1);
  assert.equal(labels(s)[0].props["font-weight"], 700);
  s.selected.value = "p";
  assert.equal(labels(s).length, 0);
  s.updateLinkNameDisplay(true);
  assert.equal(labels(s).length, 2);
  assert.ok(labels(s).every(label => label.props["font-weight"] === 400));
  s.selected.value = "l";
  assert.deepEqual(labels(s).map(label => label.props["font-weight"]), [700, 400]);
  s.beginRename("l");
  assert.equal(labels(s)[0].props.visibility, "hidden");
  s.cancelRename();
  assert.equal(labels(s)[0].props.visibility, undefined);
  s.selected.value = "";
  assert.equal(labels(s).length, 2);
  s.updateLinkNameDisplay(false);
  assert.equal(labels(s).length, 0);
  const processProps = { process: true, highlightedLinks: ["l"] };
  const process = component("./SketchCanvas.vue", { document: d, highlighted: [], ...processProps });
  assert.equal(labels(process.state, processProps).length, 1);
  s.updateLinkNameDisplay(true);
  assert.equal(labels(process.state, processProps).length, 2);
  const text = node => typeof node?.children === "string" ? node.children : Array.isArray(node?.children) ? node.children.map(text).join("") : "";
  assert.match(text(labels(process.state, processProps)[0]), /步骤 2/);
  assert.doesNotMatch(text(labels(process.state, processProps)[1]), /步骤/);
});

test("curve switch preserves anchors, is undoable and is shared by process rendering", () => {
  const d = propertyFixture();
  d.links[0].anchors = [{ x: 75, y: 350 }];
  d.links[0].color = "#7c3aed";
  d.links[0].direction = "reverse";
  const original = JSON.stringify(d.links);
  const c = canvas(d),
    s = main({});
  s.accept({ id: "s", revision: 1, document: d });
  assert.equal(c.state.showCurveArc.value, true);
  assert.match(c.state.renderedLinks.value[0].path, / A /);
  c.state.updateCurveDisplay(false);
  assert.equal(d.showCurveArc, false);
  assert.doesNotMatch(c.state.renderedLinks.value[0].path, / A /);
  c.state.updateCurveDisplay(false);
  assert.equal(c.events.filter((e) => e[0] === "change").length, 1);
  s.doc.value.showCurveArc = false;
  s.record();
  s.undo();
  assert.equal(s.doc.value.showCurveArc ?? true, true);
  s.redo();
  assert.equal(s.doc.value.showCurveArc, false);
  const process = component("./SketchCanvas.vue", {
    document: d,
    process: true,
    highlighted: [],
    highlightedLinks: [],
  });
  assert.doesNotMatch(process.state.renderedLinks.value[0].path, / A /);
  process.state.updateCurveDisplay(true);
  assert.equal(d.showCurveArc, false);
  c.state.updateCurveDisplay(true);
  assert.match(c.state.renderedLinks.value[0].path, / A /);
  assert.equal(JSON.stringify(d.links), original);
});

test("direction property defaults old links to both and updates without changing their geometry", async () => {
  const d = propertyFixture(),
    original = plain(d.links[0]);
  const { state: s, events, errors } = properties(d, -1);
  assert.equal(s.draftLink.value.direction, "both");
  assert.equal(d.links[0].direction, undefined);
  s.applyDirection("both");
  assert.equal(events.length, 0);
  for (const direction of ["forward", "reverse", "both"]) {
    s.applyDirection(direction);
    await vue.nextTick();
    assert.equal(d.links[0].direction, direction);
    assert.equal(s.draftLink.value.direction, direction);
    assert.deepEqual(plain(d.links[0]), { ...original, direction });
  }
  assert.equal(events.length, 3);
  assert.equal(errors.length, 0);
});

test("direction edits protect opposite uses across every process plan", () => {
  const d = propertyFixture();
  d.plans = [
    { chains: [] },
    {
      chains: [
        {
          steps: [
            { facilityId: "p" },
            { facilityId: "c", incomingLinkId: "l" },
          ],
        },
      ],
    },
  ];
  const {
    state: s,
    events,
    errors,
  } = component("./SketchObjectProperties.vue", {
    link: d.links[0],
    facilities: d.facilities,
    plans: d.plans,
  });
  s.draftLink.value.direction = "forward";
  s.applyDirection("forward");
  assert.equal(d.links[0].direction, undefined);
  assert.equal(s.draftLink.value.direction, "both");
  assert.equal(events.length, 0);
  assert.equal(errors.length, 1);
  s.applyDirection("reverse");
  assert.equal(d.links[0].direction, "reverse");
  assert.equal(events.length, 1);
});

test("process selection filters single-direction links while legacy links remain bidirectional", () => {
  const d = propertyFixture();
  assert.equal(geometry.connectingLinks(d, "c", "p").length, 1);
  assert.equal(geometry.connectingLinks(d, "p", "c").length, 1);
  d.links[0].direction = "forward";
  assert.equal(geometry.connectingLinks(d, "c", "p").length, 1);
  assert.equal(geometry.connectingLinks(d, "p", "c").length, 0);
  d.links[0].direction = "reverse";
  assert.equal(geometry.connectingLinks(d, "c", "p").length, 0);
  assert.equal(geometry.connectingLinks(d, "p", "c").length, 1);
  const s = main({});
  s.accept({ id: "s", revision: 1, document: d });
  s.newPlan();
  s.addChain();
  s.pick("c");
  s.pick("p");
  assert.equal(s.chain.value.steps.length, 1);
  s.doc.value.links.push({
    ...plain(d.links[0]),
    id: "alternate",
    direction: "forward",
  });
  s.pick("p");
  assert.equal(s.chain.value.steps[1].incomingLinkId, "alternate");
});

test("direction changes participate in undo and redo", () => {
  const s = main({});
  s.accept({ id: "s", revision: 1, document: propertyFixture() });
  s.doc.value.links[0].direction = "reverse";
  s.record();
  s.undo();
  assert.equal(s.doc.value.links[0].direction, undefined);
  s.redo();
  assert.equal(s.doc.value.links[0].direction, "reverse");
});

test("rendered link midpoint reacts to anchor and endpoint edits", () => {
  const d = propertyFixture();
  const { state: s } = canvas(d);
  assert.equal(s.renderedLinks.value[0].middle.x, 75);
  d.links[0].anchors.push({ x: 75, y: 350 });
  const curved = plain(s.renderedLinks.value[0]);
  assert.ok(curved.middle.y > 200 && curved.middle.y < 350);
  d.facilities[1].center.x = 10;
  assert.notEqual(s.renderedLinks.value[0].middle.x, curved.middle.x);
  assert.notEqual(s.renderedLinks.value[0].path, curved.path);
});

test("legacy color controls show defaults without modifying the scheme", () => {
  const d = propertyFixture(),
    before = JSON.stringify(d);
  const p = properties(d),
    l = properties(d, -1);
  assert.equal(p.state.draftColors.value.fillColor, "#edf3fa");
  assert.equal(p.state.draftColors.value.strokeColor, "#6689af");
  assert.equal(l.state.draftColors.value.color, "#000000");
  p.state.applyColor("fillColor", null);
  l.state.applyColor("color", null);
  assert.equal(JSON.stringify(d), before);
  assert.equal(p.events.length + l.events.length, 0);
});

test("polygon and link colors normalize, reject invalid values and reset without altering geometry", async () => {
  const d = propertyFixture(),
    before = plain(d);
  const p = properties(d),
    l = properties(d, -1);
  p.state.applyColor("fillColor", "#ABC");
  p.state.applyColor("strokeColor", "#123456");
  l.state.applyColor("color", "#7C3AED");
  await vue.nextTick();
  assert.equal(d.facilities[0].fillColor, "#aabbcc");
  assert.equal(p.state.draftColors.value.strokeColor, "#123456");
  assert.equal(d.links[0].color, "#7c3aed");
  for (const bad of ["#12345z", "red", "url(#paint)"]) {
    p.state.draftColors.value.fillColor = bad;
    p.state.applyColor("fillColor", bad);
    assert.equal(p.state.draftColors.value.fillColor, "#aabbcc");
    assert.equal(d.facilities[0].fillColor, "#aabbcc");
  }
  assert.equal(p.errors.length, 3);
  assert.equal(p.events.length, 2);
  assert.deepEqual(
    plain(d.facilities[0].vertices),
    before.facilities[0].vertices,
  );
  assert.deepEqual(plain(d.links[0]), { ...before.links[0], color: "#7c3aed" });
  p.state.applyColor("fillColor", null);
  l.state.applyColor("color", null);
  await vue.nextTick();
  assert.equal(d.facilities[0].fillColor, null);
  assert.equal(d.links[0].color, null);
  assert.equal(p.state.draftColors.value.fillColor, "#edf3fa");
  assert.equal(l.state.draftColors.value.color, "#000000");
});

test("custom colors retain selection feedback and readable text while process highlights take precedence", () => {
  const d = propertyFixture();
  const { state: s } = canvas(d);
  const f = d.facilities[0],
    l = d.links[0];
  f.fillColor = "#000000";
  f.strokeColor = "#16a34a";
  l.color = "#7c3aed";
  assert.equal(s.polygonFill(f), "#000000");
  assert.equal(s.polygonStroke(f), "#16a34a");
  assert.equal(s.linkColor(l), "#7c3aed");
  assert.equal(colors.polygonTextColor(s.polygonFill(f)), "#ffffff");
  s.selected.value = "p";
  assert.notEqual(s.polygonStroke(f), f.strokeColor);
  s.selected.value = "l";
  assert.notEqual(s.linkColor(l), l.color);
  const previousSelectionColor = s.linkColor(l);
  l.color = "#dc2626";
  assert.notEqual(s.linkColor(l), previousSelectionColor);
  s.selected.value = "";
  s.props.highlighted = ["p"];
  s.props.highlightedLinks = ["l"];
  assert.equal(s.polygonFill(f), "#e2f0e9");
  assert.equal(s.linkColor(l), "#498e75");
  assert.equal(f.fillColor, "#000000");
  assert.equal(l.color, "#dc2626");
});

test("color changes and default resets are restored through undo and redo", () => {
  const s = main({});
  s.accept({ id: "s", revision: 1, document: propertyFixture() });
  const before = JSON.stringify(s.doc.value);
  const p = properties(s.doc.value),
    l = properties(s.doc.value, -1);
  p.state.applyColor("fillColor", "#dcfce7");
  p.state.applyColor("strokeColor", "#16a34a");
  l.state.applyColor("color", "#dc2626");
  s.record();
  const colored = JSON.stringify(s.doc.value);
  s.undo();
  assert.equal(JSON.stringify(s.doc.value), before);
  s.redo();
  assert.equal(JSON.stringify(s.doc.value), colored);
  properties(s.doc.value, -1).state.applyColor("color", null);
  s.record();
  s.undo();
  assert.equal(s.doc.value.links[0].color, "#dc2626");
  s.redo();
  assert.equal(s.doc.value.links[0].color, null);
});

test("polygon fillets round only right and obtuse interior angles in either winding", () => {
  const shapes = [
    [
      [
        [100, 100],
        [500, 100],
        [500, 500],
        [100, 500],
      ],
      4,
    ],
    [
      [
        [100, 100],
        [500, 100],
        [100, 500],
      ],
      1,
    ],
    [
      [
        [100, 100],
        [500, 100],
        [200, 200],
      ],
      1,
    ],
    [
      [
        [100, 100],
        [500, 100],
        [500, 300],
        [300, 300],
        [300, 500],
        [100, 500],
      ],
      5,
    ],
    [
      [
        [100, 100],
        [300, 100],
        [500, 100],
        [500, 500],
        [100, 500],
      ],
      4,
    ],
    [
      [
        [100, 100],
        [500, 100],
        [100.001, 500],
      ],
      0,
    ],
  ];
  for (const [coords, expected] of shapes) {
    const points = coords.map(([x, y]) => ({ x, y }));
    for (const vertices of [points, [...points].reverse()]) {
      const before = JSON.stringify(vertices);
      const g = polygonGeometry.polygonGeometry(vertices);
      assert.equal(g.segments.filter((s) => s.kind === "arc").length, expected);
      assert.match(g.path, / Z$/);
      assert.doesNotMatch(g.path, /NaN|Infinity/);
      assert.equal(JSON.stringify(vertices), before);
    }
  }
});

test("closed polygon fillets meet their edges tangentially, including the first vertex", () => {
  const points = [
    { x: 100, y: 100 },
    { x: 500, y: 100 },
    { x: 500, y: 500 },
    { x: 100, y: 500 },
  ];
  const g = polygonGeometry.polygonGeometry(points);
  assert.equal(g.segments.length, 8);
  assert.equal(g.segments[0].kind, "arc");
  assert.ok(Math.abs(g.segments[0].radius - 150) < 1e-8);
  assert.deepEqual(plain(g.segments.at(-1).end), plain(g.segments[0].start));
  for (let i = 0; i < g.segments.length; i += 2) {
    const arc = g.segments[i],
      before = g.segments[(i + 7) % 8],
      after = g.segments[(i + 1) % 8];
    for (const [p, line] of [
      [arc.start, before],
      [arc.end, after],
    ]) {
      assert.ok(
        Math.abs(
          (p.x - arc.center.x) * (line.end.x - line.start.x) +
            (p.y - arc.center.y) * (line.end.y - line.start.y),
        ) < 1e-8,
      );
    }
  }
  const sharp = polygonGeometry.polygonGeometry(points, false);
  assert.doesNotMatch(sharp.path, / A /);
  assert.equal(sharp.segments.length, 4);
  assert.deepEqual(plain(sharp.segments.map((s) => s.start)), points);
});

test("short closed edges shrink adjacent fillets consistently across rotation and winding", () => {
  const points = [
    { x: 100, y: 100 },
    { x: 500, y: 100 },
    { x: 500, y: 110 },
    { x: 100, y: 110 },
  ];
  const signature = (vertices) => {
    const g = polygonGeometry.polygonGeometry(vertices);
    assert.ok(
      g.segments.filter((s) => s.kind === "line").every((s) => s.length > 0),
    );
    return g.segments
      .filter((s) => s.kind === "arc")
      .map((s) => {
        assert.ok(s.radius > 0 && s.radius < 5);
        return [s.center.x, s.center.y, s.radius]
          .map((n) => n.toFixed(6))
          .join(",");
      })
      .sort();
  };
  const expected = signature(points);
  for (let i = 0; i < points.length; i++) {
    const rotated = [...points.slice(i), ...points.slice(0, i)];
    assert.deepEqual(signature(rotated), expected);
    assert.deepEqual(signature(rotated.reverse()), expected);
  }
});

test("polygon fillet toggle is independent, persists in undo/redo and preserves vertices and ports", () => {
  const d = propertyFixture();
  d.showCurveArc = false;
  const original = JSON.stringify(d);
  const { state: s, events } = canvas(d);
  assert.equal(s.showPolygonFillets.value, true);
  assert.match(s.polygonPaths.value.get("p"), / A /);
  s.updatePolygonFillets(true);
  assert.equal(events.length, 0);
  s.updatePolygonFillets(false);
  assert.doesNotMatch(s.polygonPaths.value.get("p"), / A /);
  assert.equal(d.showCurveArc, false);
  s.updateCurveDisplay(true);
  assert.equal(d.showPolygonFillets, false);
  s.updatePolygonFillets(true);
  assert.match(s.polygonPaths.value.get("p"), / A /);
  const before = JSON.parse(original);
  assert.deepEqual(plain(d.facilities), before.facilities);
  assert.deepEqual(plain(d.links), before.links);
  const m = main({});
  m.accept({ id: "s", revision: 1, document: d });
  m.doc.value.showPolygonFillets = false;
  m.record();
  m.undo();
  assert.equal(m.doc.value.showPolygonFillets, true);
  m.redo();
  assert.equal(m.doc.value.showPolygonFillets, false);
  const process = component("./SketchCanvas.vue", {
    document: m.doc.value,
    process: true,
    highlighted: [],
    highlightedLinks: [],
  });
  assert.doesNotMatch(process.state.polygonPaths.value.get("p"), / A /);
  process.state.updatePolygonFillets(true);
  assert.equal(m.doc.value.showPolygonFillets, false);
});

test("rounded polygon edges stay editable and hits follow the visible arc", () => {
  const d = propertyFixture();
  d.facilities[0].vertices = [
    { x: 100, y: 100 },
    { x: 500, y: 100 },
    { x: 500, y: 500 },
    { x: 100, y: 500 },
  ];
  const { state: s, errors } = canvas(d);
  s.selected.value = "p";
  s.addVertexOnEdge(event(100, 120)); // original edge was cut away by the fillet
  assert.equal(d.facilities[0].vertices.length, 4);
  const p = { x: 250 - 150 / Math.sqrt(2), y: 250 - 150 / Math.sqrt(2) };
  const hit = polygonGeometry.polygonEdgeHit(d.facilities[0].vertices, p, true);
  assert.ok(hit.distance < 1e-8);
  s.addVertexOnEdge(event(p.x, p.y));
  assert.equal(d.facilities[0].vertices.length, 5);
  assert.equal(geometry.simplePolygon(d.facilities[0].vertices), true);
  assert.equal(errors.length, 0);
  const previous = s.polygonPaths.value.get("p");
  d.facilities[0].vertices[2].x += 20;
  assert.notEqual(s.polygonPaths.value.get("p"), previous);
});

const route = (points, rounded = false) =>
  linkGeometry.connectionGeometry(
    points.map(([x, y]) => ({ x, y })),
    rounded,
  );
const intersectionsOf = (...paths) =>
  plain(crossings.findRouteCrossings(paths)?.map(({ x, y }) => ({ x, y })));
const nearPoint = (point, x, y) =>
  assert.ok(Math.hypot(point.x - x, point.y - y) < 1e-7, JSON.stringify(point));

test("route crossings are deduplicated across multiple links and polyline segment boundaries", () => {
  const horizontal = route([
    [100, 200],
    [200, 200],
    [300, 200],
  ]);
  const vertical = route([
    [200, 100],
    [200, 300],
  ]);
  const diagonal = route([
    [100, 100],
    [300, 300],
  ]);
  assert.deepEqual(intersectionsOf(horizontal, vertical, diagonal), [
    { x: 200, y: 200 },
  ]);
  assert.deepEqual(
    intersectionsOf(
      route([
        [100, 100],
        [300, 300],
        [100, 300],
        [300, 100],
      ]),
    ),
    [],
  );
  assert.deepEqual(
    intersectionsOf(
      horizontal,
      route([
        [100, 250],
        [300, 250],
      ]),
    ),
    [],
  );
});

test("shared route terminals and overlapping spans are excluded, while T junctions are included", () => {
  const a = route([
    [100, 200],
    [300, 200],
  ]);
  assert.deepEqual(
    intersectionsOf(
      a,
      route([
        [300, 200],
        [300, 400],
      ]),
    ),
    [],
  );
  assert.deepEqual(
    intersectionsOf(
      a,
      route([
        [200, 200],
        [400, 200],
      ]),
    ),
    [],
  );
  assert.deepEqual(
    intersectionsOf(
      a,
      route([
        [200, 200],
        [200, 400],
      ]),
    ),
    [{ x: 200, y: 200 }],
  );
  assert.deepEqual(
    intersectionsOf(
      route(
        [
          [100, 100],
          [300, 100],
          [300, 300],
        ],
        true,
      ),
      route(
        [
          [300, 300],
          [300, 100],
          [100, 100],
        ],
        true,
      ),
    ),
    [],
  );
});

test("rounded crossings use exact line-circle intersections rather than polyline corners", () => {
  const rounded = route(
    [
      [100, 100],
      [300, 100],
      [300, 300],
    ],
    true,
  );
  const vertical = route([
    [260, 90],
    [260, 200],
  ]);
  const result = intersectionsOf(rounded, vertical);
  assert.equal(result.length, 1);
  nearPoint(result[0], 260, 120);
  assert.deepEqual(
    intersectionsOf(
      route([
        [100, 100],
        [300, 100],
        [300, 300],
      ]),
      vertical,
    ),
    [{ x: 260, y: 100 }],
  );
  const tangent = route([
    [250, 100],
    [350, 100],
  ]);
  assert.deepEqual(intersectionsOf(rounded, tangent), []); // tangent to the supporting circle outside its arc
});

test("arc crossings include exact circle-circle intersections and a single tangency", () => {
  const arc = (x, y, r, angle, sweep) => ({
    segments: [
      {
        kind: "arc",
        center: { x, y },
        radius: r,
        startAngle: angle,
        sweep,
        start: { x: x + r * Math.cos(angle), y: y + r * Math.sin(angle) },
        end: {
          x: x + r * Math.cos(angle + sweep),
          y: y + r * Math.sin(angle + sweep),
        },
        length: r * Math.abs(sweep),
      },
    ],
  });
  const a = arc(200, 200, 100, -Math.PI / 2, Math.PI);
  const b = arc(300, 200, 100, Math.PI / 2, Math.PI);
  const result = intersectionsOf(a, b);
  assert.equal(result.length, 2);
  nearPoint(result[0], 250, 200 - 50 * Math.sqrt(3));
  nearPoint(result[1], 250, 200 + 50 * Math.sqrt(3));
  assert.deepEqual(
    intersectionsOf(
      a,
      route([
        [300, 50],
        [300, 350],
      ]),
    ),
    [{ x: 300, y: 200 }],
  );
  assert.deepEqual(
    intersectionsOf(a, arc(400, 200, 100, Math.PI / 2, Math.PI)),
    [{ x: 300, y: 200 }],
  );
});

test("crossing generation enforces the annotation limit without returning a partial result", () => {
  const paths = [];
  for (let i = 0; i < 32; i++) {
    paths.push(
      route([
        [50, 100 + i * 10],
        [500, 100 + i * 10],
      ]),
    );
    paths.push(
      route([
        [100 + i * 10, 50],
        [100 + i * 10, 500],
      ]),
    );
  }
  assert.equal(crossings.findRouteCrossings(paths), undefined);
});

const crossingFixture = () => ({
  ...doc(),
  facilities: [
    {
      id: "a",
      name: "a",
      kind: "circle",
      center: { x: 100, y: 200 },
      radius: 10,
      vertices: [],
    },
    {
      id: "b",
      name: "b",
      kind: "circle",
      center: { x: 300, y: 200 },
      radius: 10,
      vertices: [],
    },
    {
      id: "c",
      name: "c",
      kind: "circle",
      center: { x: 200, y: 100 },
      radius: 10,
      vertices: [],
    },
    {
      id: "d",
      name: "d",
      kind: "circle",
      center: { x: 200, y: 300 },
      radius: 10,
      vertices: [],
    },
  ],
  links: [
    {
      id: "ab",
      name: "ab",
      from: { facilityId: "a", kind: "center", index: 0 },
      to: { facilityId: "b", kind: "center", index: 0 },
      anchors: [],
    },
    {
      id: "cd",
      name: "cd",
      from: { facilityId: "c", kind: "center", index: 0 },
      to: { facilityId: "d", kind: "center", index: 0 },
      anchors: [],
    },
  ],
});

test("explicit generation replaces static markers; visibility and link edits never recompute them", () => {
  const d = crossingFixture();
  const { state: s, events } = canvas(d);
  assert.equal(d.crossings, undefined);
  s.updateCrossingDisplay(false);
  s.updateCrossingDisplay(true);
  assert.equal(d.crossings, undefined);
  s.generateCrossings();
  assert.equal(d.crossings.length, 1);
  nearPoint(d.crossings[0].center, 200, 200);
  d.crossings[0].name = "custom";
  const oldId = d.crossings[0].id,
    count = events.length;
  s.generateCrossings();
  assert.equal(events.length, count + 1);
  assert.equal(d.crossings.length, 1);
  assert.notEqual(d.crossings[0].id, oldId);
  assert.equal(d.crossings[0].name, "进路交叉 1");
  const snapshot = JSON.stringify(d.crossings);
  s.props.document.facilities[2].center.x = 250;
  s.props.document.facilities[3].center.x = 250;
  s.updateCurveDisplay(false);
  assert.equal(JSON.stringify(d.crossings), snapshot);
  s.updateCrossingDisplay(false);
  assert.equal(s.selected.value, "");
  s.generateCrossings();
  assert.equal(d.crossings.length, 1);
  assert.equal(d.showCrossings, true);
  assert.equal(d.crossings[0].name, "进路交叉 1");
  nearPoint(d.crossings[0].center, 250, 200);
  d.links = [];
  assert.equal(d.crossings.length, 1);
});

test("generation clears obsolete markers and their selection even with fewer than two routes", () => {
  for (const remaining of [0, 1, 2]) {
    const d = crossingFixture();
    const { state: s, events } = canvas(d);
    s.generateCrossings();
    s.props.document.links.splice(remaining);
    s.props.document.facilities[2].center.x = 400;
    s.props.document.facilities[3].center.x = 400;
    const count = events.length;
    s.generateCrossings();
    assert.equal(d.crossings.length, 0);
    assert.equal(s.selected.value, "");
    assert.equal(s.crossing.value, undefined);
    assert.equal(events.length, count + 1);
    s.generateCrossings();
    assert.equal(events.length, count + 1);
  }
});

test("crossing annotations support selection, dragging, property edits and deletion independently", async () => {
  const d = crossingFixture();
  const { state: s } = canvas(d);
  s.generateCrossings();
  const marker = d.crossings[0],
    original = JSON.stringify([d.facilities, d.links]);
  assert.equal(s.crossing.value.id, marker.id);
  s.start(event(200, 200), "crossing", marker.id);
  s.move(event(220, 230));
  s.stop(event(220, 230));
  nearPoint(marker.center, 220, 230);
  const p = component("./SketchObjectProperties.vue", {
    crossing: marker,
    facilities: d.facilities,
    plans: [],
  });
  assert.equal(p.state.objectType.value, "进路交叉");
  p.state.name.value = "  主交叉口  ";
  p.state.applyName();
  assert.equal(marker.name, "主交叉口");
  p.state.draftCrossing.value.center.x = 225;
  p.state.draftCrossing.value.radius = 40;
  p.state.applyCrossing();
  assert.equal(marker.center.x, 225);
  assert.equal(marker.radius, 40);
  await vue.nextTick();
  p.state.draftCrossing.value.radius = NaN;
  p.state.applyCrossing();
  assert.equal(marker.radius, 40);
  assert.equal(p.errors.length, 1);
  p.state.name.value = " ";
  p.state.applyName();
  assert.equal(marker.name, "主交叉口");
  s.remove();
  assert.equal(d.crossings.length, 0);
  assert.equal(JSON.stringify([d.facilities, d.links]), original);
});

test("crossing generation, names, visibility and deletion survive undo/redo", () => {
  const m = main({});
  m.accept({ id: "s", revision: 1, document: crossingFixture() });
  const c = canvas(m.doc.value);
  c.state.generateCrossings();
  m.record();
  m.undo();
  assert.equal(m.doc.value.crossings, undefined);
  m.redo();
  assert.equal(m.doc.value.crossings.length, 1);
  m.doc.value.crossings[0].name = "Renamed";
  m.doc.value.showCrossings = false;
  m.record();
  m.undo();
  assert.notEqual(m.doc.value.crossings[0].name, "Renamed");
  m.redo();
  assert.equal(m.doc.value.crossings[0].name, "Renamed");
  assert.equal(m.doc.value.showCrossings, false);
  const next = canvas(m.doc.value);
  next.state.selected.value = m.doc.value.crossings[0].id;
  next.state.remove();
  m.record();
  m.undo();
  assert.equal(m.doc.value.crossings[0].name, "Renamed");
  m.redo();
  assert.equal(m.doc.value.crossings.length, 0);
});

test("process and drawing modes cannot generate markers; replaced markers do not count toward the limit", () => {
  const d = crossingFixture();
  const { state: s, errors } = canvas(d);
  s.tool.value = "link";
  s.generateCrossings();
  assert.equal(d.crossings, undefined);
  const p = component("./SketchCanvas.vue", {
    document: d,
    process: true,
    highlighted: [],
    highlightedLinks: [],
  });
  p.state.generateCrossings();
  assert.equal(d.crossings, undefined);
  s.tool.value = "select";
  d.crossings = Array.from({ length: 1000 }, (_, i) => ({
    id: `m${i}`,
    name: `m${i}`,
    center: { x: 0, y: i },
    radius: 24,
  }));
  s.generateCrossings();
  assert.equal(d.crossings.length, 1);
  nearPoint(d.crossings[0].center, 200, 200);
  assert.equal(errors.length, 0);
});

test("generation exceeding the limit preserves the previous collection and selection atomically", () => {
  const d = doc();
  for (let i = 0; i < 32; i++) {
    for (const [axis, from, to] of [
      ["h", { x: 50, y: 100 + i * 10 }, { x: 500, y: 100 + i * 10 }],
      ["v", { x: 100 + i * 10, y: 50 }, { x: 100 + i * 10, y: 500 }],
    ]) {
      const id = `${axis}${i}`;
      for (const [end, center] of [["from", from], ["to", to]])
        d.facilities.push({
          id: `${id}-${end}`, name: end, kind: "circle", center,
          radius: 10, vertices: [],
        });
      d.links.push({
        id, name: id, anchors: [],
        from: { facilityId: `${id}-from`, kind: "center", index: 0 },
        to: { facilityId: `${id}-to`, kind: "center", index: 0 },
      });
    }
  }
  d.crossings = [
    { id: "old", name: "Old", center: { x: 200, y: 200 }, radius: 40 },
  ];
  const { state: s, errors, events } = canvas(d);
  s.selected.value = "old";
  const before = JSON.stringify(d.crossings);
  s.generateCrossings();
  assert.equal(JSON.stringify(d.crossings), before);
  assert.equal(s.selected.value, "old");
  assert.equal(events.length, 0);
  assert.equal(errors.length, 1);
});

test("crossing provenance retains every participating route once after pair filtering", () => {
  const paths = [
    route([
      [100, 200],
      [200, 200],
      [300, 200],
    ]),
    route([
      [200, 100],
      [200, 300],
    ]),
    route([
      [100, 100],
      [300, 300],
    ]),
    route([
      [400, 100],
      [400, 300],
    ]),
  ];
  const hits = crossings.findRouteCrossings(paths);
  assert.equal(hits.length, 1);
  assert.deepEqual(plain(hits[0].pathIndices), [0, 1, 2]);
  const d = crossingFixture();
  d.facilities.push(
    { ...d.facilities[0], id: "e", center: { x: 100, y: 100 } },
    { ...d.facilities[1], id: "f", center: { x: 300, y: 300 } },
  );
  d.links.push({
    id: "diagonal",
    name: "斜向进路",
    from: { facilityId: "e", kind: "center", index: 0 },
    to: { facilityId: "f", kind: "center", index: 0 },
    anchors: [],
  });
  const { state: s } = canvas(d);
  s.generateCrossings();
  assert.deepEqual(plain(d.crossings[0].sourceLinks), [
    { linkId: "ab", name: "ab" },
    { linkId: "cd", name: "cd" },
    { linkId: "diagonal", name: "斜向进路" },
  ]);
  assert.deepEqual(Object.keys(d.crossings[0].center).sort(), ["x", "y"]);
});

test("source snapshots remain intact through route renaming, deletion and marker movement", () => {
  const d = vue.reactive(crossingFixture());
  const { state: s } = canvas(d);
  s.generateCrossings();
  const marker = d.crossings[0],
    snapshot = JSON.stringify(marker.sourceLinks);
  const p = component("./SketchObjectProperties.vue", {
    crossing: marker,
    links: d.links,
    facilities: d.facilities,
  });
  assert.deepEqual(
    plain(p.state.crossingSources.value).map((s) => s.currentName),
    ["ab", "cd"],
  );
  d.links[0].name = "主干进路";
  assert.equal(p.state.crossingSources.value[0].currentName, "主干进路");
  assert.equal(p.state.crossingSources.value[0].name, "ab");
  assert.equal(p.state.crossingSources.value[0].renamed, true);
  d.links.splice(0, 1);
  assert.equal(p.state.crossingSources.value[0].deleted, true);
  assert.equal(p.state.crossingSources.value[0].currentName, "ab");
  s.start(event(200, 200), "crossing", marker.id);
  s.move(event(220, 230));
  s.stop(event(220, 230));
  assert.equal(JSON.stringify(marker.sourceLinks), snapshot);
});

test("generation replaces legacy and moved markers with fresh names, geometry and current source routes", () => {
  const d = crossingFixture();
  d.crossings = [
    { id: "legacy", name: "用户名称", center: { x: 200, y: 200 }, radius: 40 },
    {
      id: "moved",
      name: "移开的标记",
      center: { x: 180, y: 180 },
      radius: 24,
      sourceLinks: [],
    },
  ];
  const { state: s, events } = canvas(d);
  s.selected.value = "legacy";
  assert.equal(s.crossing.value.sourceLinks, undefined);
  s.updateCrossingDisplay(false);
  s.updateCrossingDisplay(true);
  assert.equal(d.crossings[0].sourceLinks, undefined);
  d.links[0].name = "主干进路";
  s.generateCrossings();
  assert.equal(d.crossings.length, 1);
  assert.equal(d.crossings[0].name, "进路交叉 1");
  assert.equal(d.crossings[0].radius, 24);
  assert.notEqual(d.crossings[0].id, "legacy");
  assert.notEqual(d.crossings[0].id, "moved");
  assert.equal(s.selected.value, d.crossings[0].id);
  nearPoint(d.crossings[0].center, 200, 200);
  assert.deepEqual(plain(d.crossings[0].sourceLinks), [
    { linkId: "ab", name: "主干进路" },
    { linkId: "cd", name: "cd" },
  ]);
  const count = events.length;
  s.generateCrossings();
  assert.equal(events.length, count + 1);
  assert.equal(d.crossings.length, 1);
});

test("replacing and clearing crossings restore all old properties with one undo and invalidate analysis", () => {
  const d = crossingFixture();
  d.crossings = [
    {
      id: "old", name: "Old", center: { x: 180, y: 180 }, radius: 40,
      sourceLinks: [{ linkId: "ab", name: "旧进路名", occupancySeconds: 7 }],
    },
  ];
  d.showCrossings = false;
  const oldMarkers = JSON.stringify(d.crossings);
  const m = main({});
  m.accept({ id: "s", revision: 1, document: d });
  m.result.value = { crossingRisks: [{ crossingId: "old" }] };
  canvas(m.doc.value).state.generateCrossings();
  m.record();
  assert.equal(m.result.value, undefined);
  assert.equal(m.doc.value.crossings[0].sourceLinks.length, 2);
  assert.equal(m.doc.value.showCrossings, true);
  const newMarkers = JSON.stringify(m.doc.value.crossings);
  m.undo();
  assert.equal(JSON.stringify(m.doc.value.crossings), oldMarkers);
  assert.equal(m.doc.value.showCrossings, false);
  m.redo();
  assert.equal(JSON.stringify(m.doc.value.crossings), newMarkers);
  m.doc.value.links = [];
  m.record();
  canvas(m.doc.value).state.generateCrossings();
  m.record();
  assert.equal(m.doc.value.crossings.length, 0);
  m.undo();
  assert.equal(JSON.stringify(m.doc.value.crossings), newMarkers);
  m.redo();
  assert.equal(m.doc.value.crossings.length, 0);
});

function riskFixture() {
  const d = crossingFixture();
  canvas(d).state.generateCrossings();
  const chain = (id, link, travelSeconds) => ({
    id,
    name: id,
    steps: [
      {
        facilityId: "a",
        dwellSeconds: 0,
        travelSeconds: 0,
        incomingLinkId: null,
      },
      { facilityId: "b", dwellSeconds: 0, travelSeconds, incomingLinkId: link },
    ],
  });
  d.plans = [
    { id: "p", name: "first", quantity: 2, chains: [chain("same", "ab", 10)] },
    {
      id: "p2",
      name: "second",
      quantity: 3,
      chains: [chain("same", "ab", 20), chain("other", "cd", 30)],
    },
  ];
  d.analysisSettings = {
    horizonSeconds: 100,
    aThreshold: 0.8,
    eThreshold: 0.5,
    iThreshold: 0.2,
  };
  return d;
}
test("crossing traffic preview aggregates all plans and repeated traversals", () => {
  const d = riskFixture();
  d.plans[0].chains[0].steps.push({ incomingLinkId: "ab", travelSeconds: 40 });
  const traffic = crossingAnalysis.crossingTraffic(d);
  assert.deepEqual(plain(traffic.get("ab")), {
    passages: 7,
    weightedSeconds: 160,
  });
  assert.equal(traffic.get("cd").passages, 3);
});
test("crossing models default older saved settings without dirtying and invalidate results on edit", () => {
  const m = main({});
  m.accept({ id: "s", revision: 1, document: riskFixture() });
  assert.equal(m.dirty.value, false);
  assert.equal(m.doc.value.analysisSettings.crossingModel, "poisson-fixed");
  m.result.value = { crossingRisks: [] };
  const c = component("./SketchCrossingAnalysis.vue", {
    document: m.doc.value,
  });
  c.state.updateModel("poisson-exponential");
  m.record();
  assert.equal(m.result.value, undefined);
  assert.equal(
    m.doc.value.analysisSettings.crossingModel,
    "poisson-exponential",
  );
  assert.equal(m.dirty.value, true);
  c.state.updateModel("not-a-model");
  assert.equal(c.events.length, 1);
  m.undo();
  assert.equal(m.doc.value.analysisSettings.crossingModel, "poisson-fixed");
  m.redo();
  assert.equal(
    m.doc.value.analysisSettings.crossingModel,
    "poisson-exponential",
  );
});
test("crossing occupancy overrides and reset persist through history and preserve other sources", () => {
  const m = main({});
  m.accept({ id: "s", revision: 1, document: riskFixture() });
  const c = component("./SketchCrossingAnalysis.vue", {
    document: m.doc.value,
  });
  const id = m.doc.value.crossings[0].id;
  assert.equal(c.state.rows.value[0].routes[0].seconds, 16);
  assert.equal(c.state.rows.value[0].routes[0].ratePerHour, 180);
  assert.equal(c.state.rows.value[0].passages, 8);
  c.state.updateOccupancy(id, "ab", 5);
  m.record();
  assert.equal(c.state.rows.value[0].routes[0].seconds, 5);
  assert.equal(
    m.doc.value.crossings[0].sourceLinks[1].occupancySeconds,
    undefined,
  );
  for (const invalid of [NaN, Infinity, -1, 0, 31536001, "5"])
    c.state.updateOccupancy(id, "ab", invalid);
  assert.equal(c.events.length, 1);
  c.state.updateOccupancy(id, "ab", null);
  m.record();
  assert.equal(c.state.rows.value[0].routes[0].seconds, 16);
  m.undo();
  assert.equal(m.doc.value.crossings[0].sourceLinks[0].occupancySeconds, 5);
  m.redo();
  assert.equal(m.doc.value.crossings[0].sourceLinks[0].occupancySeconds, null);
});
test("crossing previews explain missing sources and deleted routes and never regenerate markers", () => {
  const d = vue.reactive(riskFixture());
  const c = component("./SketchCrossingAnalysis.vue", { document: d });
  d.crossings.push({
    id: "old",
    name: "old",
    center: { x: 0, y: 0 },
    radius: 24,
  });
  assert.equal(c.state.rows.value[1].status, "missing-sources");
  d.links.splice(0, 1);
  assert.equal(c.state.rows.value[0].status, "missing-links");
  d.showCrossings = false;
  assert.equal(c.state.rows.value.length, 2);
  assert.equal(d.crossings[1].sourceLinks, undefined);
  d.plans = [];
  assert.equal(c.state.rows.value[0].routes[0].seconds, null);
  assert.equal(c.events.length, 0);
});
test("crossing risk associates results by identity, including legitimate zero probabilities", () => {
  const d = riskFixture();
  const c = component("./SketchCrossingAnalysis.vue", {
    document: d,
    results: [
      {
        crossingId: d.crossings[0].id,
        status: "ready",
        conflictProbability: 0,
        routes: [
          { linkId: "cd", conflictProbability: 0.35 },
          { linkId: "ab", conflictProbability: 0.15 },
        ],
      },
    ],
  });
  assert.equal(c.state.rows.value[0].routes[0].probability, 0.15);
  assert.equal(c.state.rows.value[0].routes[1].probability, 0.35);
  assert.equal(c.state.percent(0), "0%");
  assert.equal(c.state.percent(null), "—");
});
test("editing saved marker geometry preserves occupation overrides; regeneration resets them", () => {
  const d = riskFixture();
  const marker = d.crossings[0];
  marker.sourceLinks[0].occupancySeconds = 7;
  const p = component("./SketchObjectProperties.vue", {
    crossing: marker,
    document: d,
  });
  p.state.draftCrossing.value.radius = 40;
  p.state.applyCrossing();
  assert.equal(marker.sourceLinks[0].occupancySeconds, 7);
  canvas(d).state.generateCrossings();
  assert.notEqual(d.crossings[0].id, marker.id);
  assert.equal(d.crossings[0].sourceLinks[0].occupancySeconds, undefined);
  assert.equal(d.crossings[0].radius, 24);
});

test("crossing queue results follow marker identity and preserve zero, overload and missing values", () => {
  const d = riskFixture();
  const marker = d.crossings[0];
  const queue = { model: "mg1-fcfs", status: "ready", loadRatio: 1.2, meanDelaySeconds: 0, meanQueueLength: 0, simulationRuns: 0 };
  const { state: s } = component("./SketchCrossingAnalysis.vue", {
    document: d,
    results: [
      { crossingId: "other", routes: [], queue: { ...queue, meanDelaySeconds: 999 } },
      { crossingId: marker.id, status: "ready", routes: [], queue },
    ],
  });
  assert.deepEqual(plain(s.rows.value[0].risk.queue), queue);
  assert.equal(s.queueValue(queue, "meanDelaySeconds"), "0");
  assert.equal(s.queueValue(undefined, "meanQueueLength"), "—");
  assert.equal(s.percent(queue.loadRatio), "120%");
  assert.equal(s.queueValue({ ...queue, status: "unstable" }, "meanDelaySeconds"), "无有限稳态");
  assert.equal(s.queueStatus({ ...queue, status: "calculation-limit" }), "排队仿真规模超限");
  assert.match(s.queueStatus({ ...queue, model: "finite-uniform-fcfs", simulationRuns: 2048 }), /2048/);
});

test("analysis spectra distinguish zero probability, unavailable results and intermediate values", () => {
  for (const stop of analysisOverlay.probabilityStops)
    assert.equal(analysisOverlay.probabilityColor(stop.value), stop.color);
  assert.notEqual(analysisOverlay.probabilityColor(0.1), analysisOverlay.probabilityColor(0));
  for (const invalid of [null, undefined, NaN, Infinity, -0.1, 1.1])
    assert.equal(analysisOverlay.probabilityColor(invalid), analysisOverlay.UNKNOWN_ANALYSIS_COLOR);
  assert.equal(analysisOverlay.crossingProbability({ status: "ready", conflictProbability: 0 }), 0);
  for (const status of ["no-traffic", "missing-sources", "missing-links", "calculation-limit"])
    assert.equal(analysisOverlay.crossingProbability({ status, conflictProbability: 0.5 }), undefined);
});

test("load colors use an independent scale capped at 75 percent and retain available overloaded or limited results", () => {
  for (const stop of analysisOverlay.loadStops)
    assert.equal(analysisOverlay.loadColor(stop.value), stop.color);
  const highest = analysisOverlay.loadColor(0.75);
  assert.notEqual(analysisOverlay.loadColor(0.7), highest);
  for (const value of [0.75, 0.76, 1, 1.2, 100])
    assert.equal(analysisOverlay.loadColor(value), highest);
  for (const invalid of [null, undefined, NaN, Infinity, -0.1])
    assert.equal(analysisOverlay.loadColor(invalid), analysisOverlay.UNKNOWN_ANALYSIS_COLOR);
  assert.notEqual(analysisOverlay.loadColor(0), analysisOverlay.UNKNOWN_ANALYSIS_COLOR);
  for (const status of ["ready", "unstable", "calculation-limit", "no-traffic"])
    assert.equal(analysisOverlay.crossingLoadRatio({ queue: { status, loadRatio: 0.8 } }), 0.8);
  for (const status of ["missing-links", "missing-sources"])
    assert.equal(analysisOverlay.crossingLoadRatio({ queue: { status, loadRatio: 0.8 } }), undefined);
  assert.equal(analysisOverlay.crossingLoadRatio({ status: "ready", conflictProbability: 1 }), undefined);
  assert.equal(analysisOverlay.crossingLoadRatio({ queue: { status: "no-traffic", loadRatio: 0 } }), 0);
  assert.equal(analysisOverlay.crossingLoadRatio({ queue: { status: "ready", loadRatio: null } }), undefined);
});

test("analysis canvas matches reverse facility pairs and crossing IDs, then clears stale colors", () => {
  const d = crossingFixture();
  const { state: s } = canvas(d);
  s.generateCrossings();
  d.links[0].color = "#ff00ff";
  d.showCrossings = false;
  s.props.analysisMode = true;
  s.props.analysisResult = {
    relations: [{ fromId: "b", toId: "a", grade: "E", intensity: 0.6, total: 12 }],
    crossingRisks: [
      { crossingId: "unrelated", status: "ready", conflictProbability: 1, queue: { status: "ready", loadRatio: 0 } },
      { crossingId: d.crossings[0].id, status: "ready", conflictProbability: 0, queue: { status: "ready", loadRatio: 0.8 } },
    ],
  };
  assert.equal(s.linkColor(d.links[0]), geometry.gradeColor.E);
  assert.match(s.linkAnalysisText("ab").join(" "), /E.*60%.*12/);
  assert.equal(s.crossingAnalysisColor(d.crossings[0].id), analysisOverlay.loadColor(0.8));
  assert.match(s.crossingAnalysisText(d.crossings[0].id).join(" "), /负荷率 ρ：80%.*单次通行冲突概率：0%/);
  s.props.analysisResult.crossingRisks[1].status = "calculation-limit";
  s.props.analysisResult.crossingRisks[1].conflictProbability = null;
  assert.equal(s.crossingAnalysisColor(d.crossings[0].id), analysisOverlay.loadColor(0.8));
  assert.equal(s.showCrossings.value, true);
  s.props.analysisResult = undefined;
  assert.equal(s.linkColor(d.links[0]), analysisOverlay.UNKNOWN_ANALYSIS_COLOR);
  assert.equal(s.crossingAnalysisColor(d.crossings[0].id), analysisOverlay.UNKNOWN_ANALYSIS_COLOR);
  assert.match(s.crossingAnalysisText(d.crossings[0].id).join(" "), /待生成分析/);
  assert.equal(d.links[0].color, "#ff00ff");
  assert.equal(d.showCrossings, false);
  s.props.analysisMode = false;
  s.selected.value = "";
  assert.equal(s.linkColor(d.links[0]), "#ff00ff");
});

test("analysis canvas blocks geometry and settings edits while retaining pan and zoom", () => {
  const d = crossingFixture();
  const { state: s, events } = canvas(d);
  s.props.analysisMode = true;
  const before = JSON.stringify(d);
  s.selected.value = "ab";
  s.remove();
  s.generateCrossings();
  s.updateGrid({ spacing: 99 });
  s.updateCurveDisplay(false);
  s.updatePolygonFillets(false);
  s.updateCrossingDisplay(false);
  s.activateTool("circle");
  s.background(event(100, 100));
  s.selectFacility(event(100, 100), "a");
  s.selectLink(event(100, 100), "ab");
  s.addAnchor(event(200, 200), "ab");
  s.start(event(100, 200), "facility", "a");
  s.move(event(120, 220));
  s.stop(event(120, 220));
  s.polygon.value = [{ x: 1, y: 1 }, { x: 20, y: 1 }, { x: 20, y: 20 }];
  s.finish();
  assert.equal(JSON.stringify(d), before);
  assert.equal(events.length, 0);
  const initial = { ...s.view.value };
  s.start(event(0, 0), "pan");
  s.move(event(50, 40));
  s.stop(event(50, 40));
  assert.notEqual(s.view.value.x, initial.x);
  s.zoom(0.8);
  assert.ok(s.view.value.w < initial.w);
  assert.equal(JSON.stringify(d), before);
  assert.equal(events.length, 0);
});

test("activity relationship chart has one diamond per unordered facility pair with no self-relations", () => {
  const facilities = ["a", "b", "c", "d"].map((id) => ({ id, name: id }));
  const { state: s } = component("./SketchRelationshipChart.vue", { facilities, relations: [] });
  assert.equal(s.cells.value.length, 6);
  assert.equal(new Set(s.cells.value.map((c) => c.key)).size, 6);
  for (const cell of s.cells.value) {
    assert.ok(cell.i < cell.j);
    assert.equal(cell.y, (s.rowY(cell.i) + s.rowY(cell.j)) / 2);
    assert.ok(cell.x + s.half < s.chartWidth.value);
    assert.ok(cell.y - s.half >= 0 && cell.y + s.half < s.chartHeight.value);
  }
  const [ab, ac, ad, bc] = s.cells.value;
  assert.equal(ab.x, bc.x);
  assert.ok(ab.x < ac.x && ac.x < ad.x);
});

test("activity relationship chart matches IDs, aligns flow direction and distinguishes U from missing data", () => {
  const { state: s } = component("./SketchRelationshipChart.vue", {
    facilities: ["a", "b", "c"].map((id) => ({ id, name: "同名设施" })),
    relations: [
      { fromId: "b", toId: "a", grade: "A", forward: 8, reverse: 2, total: 10, intensity: 1 },
      { fromId: "c", toId: "a", grade: "U", forward: 0, reverse: 0, total: 0, intensity: 0 },
    ],
  });
  const [ab, ac, bc] = s.cells.value;
  assert.equal(ab.color, geometry.gradeColor.A);
  assert.equal(ab.forward, 2);
  assert.equal(ab.reverse, 8);
  assert.match(s.description(ab), /1\. 同名设施 ↔ 2\. 同名设施.*A.*总量 10.*100.0%.*2 \/ 8/);
  assert.equal(ac.relation.grade, "U");
  assert.equal(ac.relation.total, 0);
  assert.equal(ac.color, geometry.gradeColor.U);
  assert.match(s.description(ac), /U.*总量 0/);
  assert.equal(bc.relation, undefined);
  assert.match(s.description(bc), /无分析数据/);
});

test("activity relationship chart updates after facility reorder, new results and result clearing without mutations", () => {
  const facilities = ["a", "b", "c"].map((id) => ({ id, name: id }));
  const relations = [{ fromId: "a", toId: "b", grade: "E", forward: 7, reverse: 1, total: 8, intensity: 0.5 }];
  const before = JSON.stringify({ facilities, relations });
  const { state: s, events } = component("./SketchRelationshipChart.vue", { facilities, relations });
  s.focusedKey.value = s.cells.value[0].key;
  assert.equal(s.activeCell.value.relation.grade, "E");
  s.hoveredKey.value = s.cells.value[1].key;
  assert.equal(s.activeCell.value.second.id, "c");
  s.focusCell(s.cells.value[0].key);
  assert.equal(s.hoveredKey.value, undefined);
  assert.equal(s.activeCell.value.second.id, "b");
  assert.equal(JSON.stringify({ facilities, relations }), before);
  assert.equal(events.length, 0);
  s.props.facilities = [facilities[2], facilities[1], facilities[0]];
  assert.equal(s.activeCell.value.first.id, "b");
  assert.equal(s.activeCell.value.forward, 1);
  s.props.relations = [{ ...relations[0], grade: "A", intensity: 1 }];
  assert.equal(s.activeCell.value.color, geometry.gradeColor.A);
  s.props.relations = [];
  assert.match(s.description(s.activeCell.value), /无分析数据/);
  s.props.facilities = [facilities[0]];
  assert.equal(s.cells.value.length, 0);
  assert.equal(s.activeCell.value, undefined);
});

test("scheme metadata save preserves unsaved drawing changes and their undo/redo history", async () => {
  let request;
  const stored = { ...doc(), facilities: [{ id: "f", name: "original", center: { x: 10, y: 20 } }] };
  const s = main({
    put: async (url, body) => {
      request = { url, ...plain(body) };
      return { data: { id: "s", revision: 8, updatedAt: "2026-10-10", document: body.document } };
    },
  });
  s.accept({ id: "s", revision: 7, document: stored });
  s.schemes.value = [{ id: "s", revision: 7, name: "test", description: "" }];
  s.doc.value.facilities[0].name = "draft";
  s.record();
  s.doc.value.facilities[0].center.x = 80;
  s.record();
  s.undo();
  await s.editScheme(s.schemes.value[0]);
  s.metadataDraft.value = { name: "  renamed  ", description: "new description" };
  assert.equal(await s.saveSchemeMetadata(), true);
  assert.equal(request.url, "/SketchLayout/schemes/s");
  assert.equal(request.revision, 7);
  assert.deepEqual(request.document.facilities, stored.facilities);
  assert.equal(request.document.name, "renamed");
  assert.equal(s.detail.value.revision, 8);
  assert.equal(s.schemes.value[0].name, "renamed");
  assert.equal(s.doc.value.facilities[0].name, "draft");
  assert.equal(s.dirty.value, true);
  s.undo();
  assert.equal(s.doc.value.facilities[0].name, "original");
  assert.equal(s.doc.value.name, "renamed");
  assert.equal(s.dirty.value, false);
  s.redo();
  s.redo();
  assert.equal(s.doc.value.facilities[0].center.x, 80);
  assert.equal(s.doc.value.description, "new description");
  assert.equal(s.editingScheme.value, undefined);
});

test("metadata updates a clean scheme without invalidating its analysis", async () => {
  const s = main({
    put: async (_url, body) => ({ data: { id: "s", revision: 2, document: body.document } }),
  });
  s.accept({ id: "s", revision: 1, document: doc() });
  s.result.value = { relations: [], crossingRisks: [] };
  const analysis = s.result.value;
  await s.editScheme({ id: "s" });
  s.metadataDraft.value.name = "renamed";
  assert.equal(await s.saveSchemeMetadata(), true);
  assert.equal(s.doc.value.name, "renamed");
  assert.equal(s.dirty.value, false);
  assert.equal(s.result.value, analysis);
  assert.equal(s.past.value.length, 0);
});

test("editing another scheme saves its latest revision without switching the active drawing", async () => {
  let request;
  const other = { id: "other", revision: 9, document: { ...doc(), name: "other" } };
  const s = main({
    get: async (url) => {
      assert.equal(url, "/SketchLayout/schemes/other");
      return { data: other };
    },
    put: async (url, body) => {
      request = { url, ...plain(body) };
      return { data: { ...other, revision: 10, document: body.document } };
    },
  });
  s.accept({ id: "current", revision: 3, document: doc() });
  s.doc.value.facilities.push({ id: "draft" });
  s.record();
  s.schemes.value = [{ id: "other", name: "other", description: "", revision: 8 }];
  const before = JSON.stringify(s.doc.value);
  await s.editScheme(s.schemes.value[0]);
  s.metadataDraft.value.description = "edited in manager";
  assert.equal(await s.saveSchemeMetadata(), true);
  assert.equal(request.url, "/SketchLayout/schemes/other");
  assert.equal(request.revision, 9);
  assert.equal(s.detail.value.id, "current");
  assert.equal(s.detail.value.revision, 3);
  assert.equal(JSON.stringify(s.doc.value), before);
  assert.equal(s.dirty.value, true);
  assert.equal(s.schemes.value[0].description, "edited in manager");
});

test("metadata cancel, validation and save conflicts keep local data and revision intact", async () => {
  let requests = 0;
  const s = main({ put: async () => { requests++; throw new Error("revision conflict"); } });
  s.accept({ id: "s", revision: 4, document: doc() });
  const before = JSON.stringify(s.doc.value);
  await s.editScheme({ id: "s" });
  s.metadataDraft.value.name = "cancelled";
  s.cancelSchemeEdit();
  assert.equal(JSON.stringify(s.doc.value), before);
  await s.editScheme({ id: "s" });
  assert.equal(s.metadataDraft.value.name, "test");
  for (const name of [" ", "a".repeat(121)]) {
    s.metadataDraft.value.name = name;
    assert.equal(await s.saveSchemeMetadata(), false);
  }
  s.metadataDraft.value = { name: "valid", description: "a".repeat(2001) };
  assert.equal(await s.saveSchemeMetadata(), false);
  assert.equal(requests, 0);
  s.metadataDraft.value.description = "retry me";
  assert.equal(await s.saveSchemeMetadata(), false);
  assert.equal(requests, 1);
  assert.equal(s.editingScheme.value.id, "s");
  assert.equal(s.metadataDraft.value.description, "retry me");
  assert.equal(s.detail.value.revision, 4);
  assert.equal(s.editingScheme.value.revision, 4);
  assert.equal(JSON.stringify(s.doc.value), before);
  assert.equal(s.busy.value, false);
  assert.equal(s.dirty.value, false);
});

test("new scheme inserts a single cancellable row without saving or changing the drawing", async () => {
  const s = main({});
  s.accept({ id: "current", revision: 3, document: doc() });
  s.schemes.value = [{ id: "current", name: "test", description: "", ownerName: "test" }];
  s.doc.value.facilities.push({ id: "unsaved" });
  s.record();
  const before = JSON.stringify(s.doc.value);
  s.search.value = "no match";
  s.create();
  assert.equal(s.manager.value, true);
  assert.equal(s.search.value, "");
  assert.equal(s.managerRows.value.length, 2);
  const draft = s.managerRows.value[0];
  assert.equal(s.isEditingRow(draft), true);
  assert.equal(s.isEditingRow(s.schemes.value[0]), false);
  s.metadataDraft.value.name = "in progress";
  s.create();
  await s.editScheme(s.schemes.value[0]);
  assert.equal(s.managerRows.value[0].id, draft.id);
  assert.equal(s.metadataDraft.value.name, "in progress");
  assert.equal(s.editingScheme.value, undefined);
  s.cancelSchemeEdit();
  assert.equal(s.managerRows.value.length, 1);
  assert.equal(s.editingMetadata.value, false);
  assert.equal(JSON.stringify(s.doc.value), before);
  assert.equal(s.dirty.value, true);
  s.undo();
  assert.equal(s.dirty.value, false);
  await s.editScheme(s.schemes.value[0]);
  s.create();
  assert.equal(s.newScheme.value, undefined);
  assert.equal(s.editingScheme.value.id, "current");
});

test("saving a new scheme adds its server summary and preserves the active drawing and history", async () => {
  let request;
  const s = main({
    post: async (url, body) => {
      request = { url, body: plain(body) };
      return { data: {
        id: "created", ownerId: "owner", ownerName: "test", revision: 1,
        createdAt: "2026-10-10", updatedAt: "2026-10-10", document: body,
      } };
    },
  });
  s.accept({ id: "current", revision: 4, document: doc() });
  s.schemes.value = [{ id: "current", name: "test", description: "", ownerName: "test" }];
  s.doc.value.facilities.push({ id: "unsaved" });
  s.record();
  s.doc.value.facilities[0].name = "renamed";
  s.record();
  s.undo();
  s.result.value = { relations: [] };
  const before = plain({
    document: s.doc.value, detail: s.detail.value, saved: s.saved.value,
    past: s.past.value, future: s.future.value, snapshot: s.snapshot.value, result: s.result.value,
  });
  s.create();
  s.metadataDraft.value = { name: "  new scheme  ", description: "line 1\nline 2" };
  assert.equal(await s.saveSchemeMetadata(), true);
  assert.deepEqual(request, { url: "/SketchLayout/schemes", body: {
    name: "new scheme", description: "line 1\nline 2", facilities: [], links: [], plans: [],
  } });
  assert.deepEqual(plain(s.schemes.value[0]), {
    id: "created", ownerId: "owner", ownerName: "test", revision: 1,
    createdAt: "2026-10-10", updatedAt: "2026-10-10", name: "new scheme", description: "line 1\nline 2",
  });
  assert.equal(s.schemes.value.length, 2);
  assert.equal(s.manager.value, true);
  assert.equal(s.editingMetadata.value, false);
  assert.deepEqual(plain({
    document: s.doc.value, detail: s.detail.value, saved: s.saved.value,
    past: s.past.value, future: s.future.value, snapshot: s.snapshot.value, result: s.result.value,
  }), before);
  assert.equal(await s.saveSchemeMetadata(), false);
});

test("first scheme creation validates fields, retains failed drafts and prevents duplicate submissions", async () => {
  let calls = 0, complete;
  const s = main({ post: async (_url, body) => {
    calls++;
    if (calls === 1) throw new Error("network error");
    await new Promise((resolve) => { complete = resolve; });
    return { data: { id: "first", revision: 1, ownerName: "test", document: body } };
  } });
  s.create();
  for (const fields of [
    { name: " ", description: "" },
    { name: "a".repeat(121), description: "" },
    { name: "valid", description: "a".repeat(2001) },
  ]) {
    s.metadataDraft.value = fields;
    assert.equal(await s.saveSchemeMetadata(), false);
  }
  assert.equal(calls, 0);
  s.metadataDraft.value = { name: "first", description: "keep this draft" };
  const draftId = s.newScheme.value.id;
  assert.equal(await s.saveSchemeMetadata(), false);
  assert.equal(s.newScheme.value.id, draftId);
  assert.equal(s.metadataDraft.value.description, "keep this draft");
  assert.equal(s.doc.value, undefined);
  const saving = s.saveSchemeMetadata();
  assert.equal(s.busy.value, true);
  assert.equal(await s.saveSchemeMetadata(), false);
  s.cancelSchemeEdit();
  s.create();
  assert.equal(s.newScheme.value.id, draftId);
  assert.equal(calls, 2);
  complete();
  assert.equal(await saving, true);
  assert.equal(s.detail.value.id, "first");
  assert.equal(s.doc.value.name, "first");
  assert.equal(s.dirty.value, false);
  assert.equal(s.schemes.value.length, 1);
  assert.equal(s.newScheme.value, undefined);
  assert.equal(s.busy.value, false);
});

test("infinite canvas draws and drags polygons, circles and anchors across the former edges", () => {
  const d = doc();
  const { state: s, errors } = canvas(d, { spacing: 20 });
  s.tool.value = "polygon";
  for (const [x, y] of [[-5010, 10000], [-4800, 10000], [-4800, 10200], [-5010, 10000]]) s.background(event(x, y));
  assert.equal(d.facilities.length, 1);
  const f = d.facilities[0];
  nearPoint(f.vertices[0], -5000, 10000);
  s.tool.value = "select";
  s.start(event(-5000, 10000), "facility", f.id);
  s.move(event(15000, -10000)); s.stop(event(15000, -10000));
  nearPoint(f.vertices[0], 15000, -10000);
  nearPoint(f.vertices[1], 15200, -10000);
  s.activateTool("circle"); s.background(event(-4000, -5000));
  const circle = d.facilities[1];
  s.tool.value = "link";
  s.pickPort({ facilityId: f.id, kind: "vertex", index: 0 });
  s.background(event(-6000, -6000));
  s.pickPort({ facilityId: circle.id, kind: "center", index: 0 });
  nearPoint(d.links[0].anchors[0], -6000, -6000);
  s.tool.value = "select";
  s.start(event(-6000, -6000), "anchor", d.links[0].id, 0);
  s.move(event(-7000, -8000)); s.stop(event(-7000, -8000));
  nearPoint(d.links[0].anchors[0], -7000, -8000);
  assert.equal(errors.length, 0);
});

test("property editors accept negative and remote coordinates but reject nonfinite values", () => {
  const d = propertyFixture();
  const polygon = properties(d).state;
  polygon.draftFacility.value.vertices.forEach(p => { p.x -= 10000; p.y += 20000; });
  polygon.applyFacility();
  assert.ok(d.facilities[0].vertices.every(p => p.x < 0 && p.y > 1600));
  const circle = properties(d, 1).state;
  circle.draftFacility.value.center = { x: -3000, y: 9000 }; circle.applyFacility();
  nearPoint(d.facilities[1].center, -3000, 9000);
  circle.draftFacility.value.center.x = Infinity; circle.applyFacility();
  nearPoint(d.facilities[1].center, -3000, 9000);
  const link = component("./SketchObjectProperties.vue", { link: d.links[0], facilities: d.facilities }).state;
  link.draftLink.value.anchors = [{ x: -8000, y: 20000 }]; link.applyAnchors();
  nearPoint(d.links[0].anchors[0], -8000, 20000);
  const marker = { id: "x", name: "crossing", center: { x: 0, y: 0 }, radius: 24 };
  const crossing = component("./SketchObjectProperties.vue", { crossing: marker, facilities: [] }).state;
  crossing.draftCrossing.value.center = { x: -5000, y: -5000 }; crossing.applyCrossing();
  nearPoint(marker.center, -5000, -5000);
});

test("crossings and polygon rounding remain valid after translating far from the original board", () => {
  for (const [dx, dy] of [[-10000, -20000], [30000, 40000]]) {
    const paths = [route([[dx, dy], [dx + 200, dy + 200]]), route([[dx, dy + 200], [dx + 200, dy]])];
    nearPoint(intersectionsOf(...paths)[0], dx + 100, dy + 100);
  }
  const points = [[0, 0], [100, 0], [100, 100], [0, 100]].map(([x, y]) => ({ x: x + 1e9, y: y - 1e9 }));
  assert.equal(geometry.simplePolygon(points), true);
  assert.match(polygonGeometry.polygonGeometry(points).path, / A /);
});

test("fit canvas includes remote facilities, link anchors and crossing radii without changing the document", () => {
  const d = propertyFixture();
  d.facilities[1].center = { x: -12000, y: 8000 };
  d.links[0].anchors = [{ x: 50000, y: -20000 }];
  d.crossings = [{ id: "c", name: "c", center: { x: -30000, y: 60000 }, radius: 100 }];
  const before = JSON.stringify(d);
  const view = viewport.fitDocument(d, { width: 900, height: 500 });
  for (const p of [...d.facilities[0].vertices, d.facilities[1].center, ...d.links[0].anchors, { x: -30100, y: 60100 }]) {
    assert.ok(p.x > view.x && p.x < view.x + view.w);
    assert.ok(p.y > view.y && p.y < view.y + view.h);
  }
  assert.equal(JSON.stringify(d), before);
  assert.deepEqual(plain(viewport.fitDocument(doc(), { width: 900, height: 500 })), plain(viewport.DEFAULT_VIEW));
  const { state: s } = canvas(d);
  s.view.value = { x: 1e6, y: 1e6, w: 400, h: 300 }; s.fitAll();
  assert.ok(s.view.value.x < -30000);
  assert.ok(s.view.value.w > 80000);
});

test("visible background covers letterboxing and grid density stays usable beyond the former zoom limit", () => {
  const wide = viewport.visibleWorld({ x: -5000, y: 7000, w: 1000, h: 1000 }, { width: 1000, height: 500 });
  assert.deepEqual(plain(wide), { x: -5500, y: 7000, w: 2000, h: 1000 });
  const tall = viewport.visibleWorld({ x: 90000, y: -100000, w: 1000, h: 1000 }, { width: 500, height: 1000 });
  assert.deepEqual(plain(tall), { x: 90000, y: -100500, w: 1000, h: 2000 });
  const { state: s } = canvas(doc());
  s.canvasSize.value = { width: 1000, height: 500 };
  s.zoom(1e5);
  assert.ok(s.view.value.w > 4800);
  assert.ok(s.visibleGridSpacing.value * s.displayScale.value >= 8 - 1e-9);
  const before = JSON.stringify(s.view.value);
  for (const invalid of [NaN, Infinity, -1, 0]) s.zoom(invalid);
  assert.equal(JSON.stringify(s.view.value), before);
});


test("inline rename updates every object kind once without changing geometry or references", () => {
  const d = propertyFixture();
  d.crossings = [{ id: "x", name: "交叉", center: { x: 150, y: 200 }, radius: 10, linkIds: ["l"] }];
  const { state: s, events } = canvas(d);
  const geometryBefore = JSON.stringify(d.facilities.map(f => ({ ...f, name: undefined })));
  const linksBefore = JSON.stringify(d.links.map(l => ({ ...l, name: undefined })));
  for (const id of ["p", "c", "l", "x"]) {
    s.beginRename(id);
    assert.equal(s.selected.value, id);
    s.rename.value.draft = `  新名称 ${id}  `;
    s.commitRename();
    assert.equal([...d.facilities, ...d.links, ...d.crossings].find(o => o.id === id).name, `新名称 ${id}`);
    assert.equal(s.rename.value, undefined);
  }
  assert.equal(events.length, 4);
  assert.equal(JSON.stringify(d.facilities.map(f => ({ ...f, name: undefined }))), geometryBefore);
  assert.equal(JSON.stringify(d.links.map(l => ({ ...l, name: undefined }))), linksBefore);
  assert.equal(JSON.stringify(d.crossings[0].linkIds), '["l"]');
  s.beginRename("p");
  s.commitRename();
  assert.equal(events.length, 4);
});

test("inline rename validates, cancels, and cannot modify drawing or read-only canvases", () => {
  const d = propertyFixture();
  const { state: s, events, errors } = canvas(d);
  s.beginRename("p");
  s.rename.value.draft = "  ";
  s.commitRename();
  assert.ok(s.rename.value);
  assert.equal(d.facilities[0].name, "库区");
  s.rename.value.draft = "x".repeat(121);
  s.commitRename(true);
  assert.equal(s.rename.value, undefined);
  assert.equal(errors.length, 2);
  s.beginRename("p");
  s.rename.value.draft = "取消的名称";
  s.cancelRename();
  assert.equal(d.facilities[0].name, "库区");
  s.beginRename("p");
  s.activateTool("polygon");
  assert.equal(s.rename.value, undefined);
  s.beginRename("p");
  assert.equal(s.rename.value, undefined);
  assert.equal(events.length, 0);
  for (const mode of ["process", "analysisMode"]) {
    const { state: ro } = component("./SketchCanvas.vue", { document: d, [mode]: true, highlighted: [], highlightedLinks: [] });
    ro.beginRename("p");
    assert.equal(ro.rename.value, undefined);
  }
});

test("facility double-click renames its interior while polygon edges keep vertex insertion", () => {
  const d = propertyFixture();
  const { state: s, events } = canvas(d);
  s.doubleClickFacility(event(200, 200), "p");
  assert.equal(s.rename.value.id, "p");
  assert.equal(d.facilities[0].vertices.length, 4);
  s.cancelRename();
  s.doubleClickFacility(event(200, 100), "p");
  assert.equal(s.rename.value, undefined);
  assert.equal(d.facilities[0].vertices.length, 5);
  assert.equal(events.length, 1);
  s.doubleClickFacility(event(50, 200), "c");
  assert.equal(s.rename.value.id, "c");
});


test("clicks leave pointer capture untouched; actual drags capture and release once", () => {
  const { state: s, events } = canvas(propertyFixture());
  const captured = [];
  let active = false;
  const target = {
    setPointerCapture(id) { active = true; captured.push(["capture", id]); },
    hasPointerCapture() { return active; },
    releasePointerCapture(id) { active = false; captured.push(["release", id]); },
  };
  for (let i = 0; i < 2; i++) {
    s.start({ ...event(200, 200), currentTarget: target }, "facility", "p");
    s.move(event(202, 201));
    s.stop(event(202, 201));
  }
  assert.equal(captured.length, 0, "a double-click with slight hand movement must not become a drag");
  assert.equal(events.length, 0);
  s.start({ ...event(200, 200), currentTarget: target }, "facility", "p");
  s.leaveCanvas();
  s.move(event(230, 220));
  assert.equal(captured.length, 0, "leaving before capture must discard the pending drag");
  assert.equal(s.dragging.value, false);
  s.start({ ...event(200, 200), currentTarget: target }, "facility", "p");
  s.move(event(220, 210));
  s.move(event(230, 220));
  s.stop(event(230, 220));
  assert.equal(JSON.stringify(captured), '[["capture",1],["release",1]]');
  assert.equal(events.length, 1);
});

test("rendered logistics line double-click opens rename at the pointer without adding an anchor", () => {
  const d = propertyFixture();
  const { state: s, events } = canvas(d);
  const { descriptor } = parse(readFileSync(new URL("./SketchCanvas.vue", import.meta.url), "utf8"));
  const compiled = compileScript(descriptor, { id: "rename-events" });
  const template = compileTemplate({
    source: descriptor.template.content, filename: "SketchCanvas.vue", id: "rename-events",
    compilerOptions: { bindingMetadata: compiled.bindings },
  });
  assert.deepEqual(template.errors, []);
  const render = evaluate(template.code, name => {
    assert.equal(name, "vue");
    return { ...vue, withDirectives: node => node, resolveComponent: tag => tag };
  }).render;
  const props = { document: d, process: false, analysisMode: false, highlighted: [], highlightedLinks: [] };
  const root = render(vue.proxyRefs(s), [], props, vue.proxyRefs(s), {}, {});
  function find(node, className) {
    if (node?.props?.class === className) return node;
    if (Array.isArray(node?.children)) {
      for (const child of node.children) {
        const match = find(child, className);
        if (match) return match;
      }
    }
  }
  const line = find(root, "link-hit-target");
  assert.ok(line);
  const e = { ...event(70, 200), stopPropagation() {}, preventDefault() {} };
  line.props.onDblclick(e);
  assert.equal(s.rename.value.id, "l");
  assert.equal(JSON.stringify(s.renamePosition.value), '{"x":70,"y":200}');
  assert.equal(d.links[0].anchors.length, 0);
  assert.equal(events.length, 0);
  s.rename.value.draft = "新通道";
  s.commitRename();
  assert.equal(d.links[0].name, "新通道");
  assert.equal(events.length, 1);
  line.props.onDblclick({ ...e, shiftKey: true });
  assert.equal(d.links[0].anchors.length, 1);
  assert.equal(s.rename.value, undefined);
});


test("inline rename stays visible when the target label is outside a zoomed viewport", () => {
  const { state: s } = canvas(propertyFixture());
  s.canvasSize.value = { width: 800, height: 400 };
  s.view.value = { x: 5000, y: -5000, w: 1600, h: 800 };
  s.beginRename("p");
  const box = s.renameBox.value;
  const area = s.visibleArea.value;
  assert.ok(box.x >= area.x && box.y >= area.y);
  assert.ok(box.x + box.width <= area.x + area.w);
  assert.ok(box.y + box.height <= area.y + area.h);
  assert.equal(box.width * s.displayScale.value, 200);
});
