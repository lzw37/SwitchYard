<script setup lang="ts">
import { computed, onMounted, onBeforeUnmount, ref, watch } from "vue";
import { onBeforeRouteLeave, useRouter } from "vue-router";
import { useI18n } from "vue-i18n";
import { ElMessage, ElMessageBox } from "element-plus";
import axios from "@/utils/axios";
import { useAuthStore } from "@/stores/auth";
import ActionButton from "@/components/ui/ActionButton.vue";
import PaneDivider from "@/components/ui/PaneDivider.vue";
import { actionIcons } from "@/components/ui/actionIcons";
import SketchCanvas from "./SketchCanvas.vue";
import SketchAnalysis from "./SketchAnalysis.vue";
import { connectingLinks } from "./geometry";
import { clone, uid, resolveAnalysisSettings, normalizePlanQuantities } from "./types";
import type {
  AnalysisResult,
  ProcessChain,
  ProcessPlan,
  SchemeDetail,
  SchemeDocument,
  SchemeSummary,
} from "./types";
const { locale } = useI18n();
const tr = (zh: string, en: string) => (locale.value === "zh" ? zh : en);
const router = useRouter(),
  auth = useAuthStore();
auth.hydrateFromStorage();
const base = "/SketchLayout/schemes",
  schemes = ref<SchemeSummary[]>([]),
  detail = ref<SchemeDetail>(),
  doc = ref<SchemeDocument>();
const saved = ref(""),
  busy = ref(false),
  manager = ref(false),
  search = ref(""),
  tab = ref("layout"),
  planId = ref(""),
  chainId = ref(""),
  result = ref<AnalysisResult>();
const editingScheme = ref<SchemeDetail>(),
  newScheme = ref<SchemeSummary>(),
  metadataDraft = ref({ name: "", description: "" });
const quantitySettingsOpen = ref(false);
const quantityDraft = ref<{ id: string; name: string; quantity: number | undefined }[]>([]);
const editingMetadata = computed(() => !!editingScheme.value || !!newScheme.value);
const isEditingRow = (row: SchemeSummary) =>
  row.id === editingScheme.value?.id || row.id === newScheme.value?.id;
const past = ref<string[]>([]),
  future = ref<string[]>([]),
  snapshot = ref("");
const chainListWidth = ref(260),
  processPanelWidth = ref(360);
const chainRightMin = computed(() =>
  Math.max(220, chainListWidth.value + processPanelWidth.value - 520),
);
const chainRightMax = computed(() =>
  Math.min(500, chainListWidth.value + processPanelWidth.value - 300),
);
function resizeChainRight(value: number) {
  // Keep the left edge fixed: exchange width only with the properties panel.
  const next = Math.max(
    chainRightMin.value,
    Math.min(chainRightMax.value, value),
  );
  processPanelWidth.value += chainListWidth.value - next;
  chainListWidth.value = next;
}
function resetProcessWidths() {
  chainListWidth.value = 260;
  processPanelWidth.value = 360;
}
const dirty = computed(
  () => !!doc.value && JSON.stringify(doc.value) !== saved.value,
);
const plan = computed(() =>
    doc.value?.plans.find((p) => p.id === planId.value),
  ),
  chain = computed(() =>
    plan.value?.chains.find((c) => c.id === chainId.value),
  );
const filtered = computed(() =>
  schemes.value.filter((s) =>
    (s.name + " " + s.description + " " + s.ownerName)
      .toLowerCase()
      .includes(search.value.toLowerCase()),
  ),
);
const managerRows = computed(() =>
  newScheme.value ? [newScheme.value, ...filtered.value] : filtered.value,
);
const highlighted = computed(
    () => chain.value?.steps.map((s) => s.facilityId) || [],
  ),
  highlightedLinks = computed(
    () =>
      chain.value?.steps
        .map((s) => s.incomingLinkId)
        .filter((s): s is string => !!s) || [],
  );
const facilityName = (id: string) =>
  doc.value?.facilities.find((f) => f.id === id)?.name || id;
const linkName = (id: string | null) =>
  doc.value?.links.find((l) => l.id === id)?.name ||
  id ||
  tr("未选择进路", "No link selected");
const draftChains = computed(
  () => plan.value?.chains.filter((c) => c.steps.length < 2).length || 0,
);
const chainDuration = (c: ProcessChain) =>
  c.steps.reduce((total, s) => total + s.dwellSeconds + s.travelSeconds, 0);
const chainDwell = (c: ProcessChain) =>
  c.steps.reduce((total, s) => total + s.dwellSeconds, 0);
const chainTravel = (c: ProcessChain) =>
  c.steps.reduce((total, s) => total + s.travelSeconds, 0);
function selectChain(id: string) {
  record();
  chainId.value = id;
}
function openQuantitySettings() {
  if (busy.value || !doc.value?.plans.length) return;
  quantityDraft.value = doc.value.plans.map(({ id, name, quantity }) => ({ id, name, quantity }));
  quantitySettingsOpen.value = true;
}
function cancelQuantitySettings() {
  quantitySettingsOpen.value = false;
}
function applyQuantitySettings() {
  if (busy.value || !quantitySettingsOpen.value || !doc.value) return;
  const invalid = quantityDraft.value.find(({ quantity }) =>
    !Number.isInteger(quantity) || quantity! < 1 || quantity! > 1000000,
  );
  if (invalid) {
    ElMessage.warning(tr(
      `“${invalid.name}”的作业数量须为 1–1000000 的整数。`,
      `The quantity for “${invalid.name}” must be an integer from 1 to 1,000,000.`,
    ));
    return;
  }
  const quantities = new Map(quantityDraft.value.map(row => [row.id, row.quantity!]));
  if (quantities.size !== doc.value.plans.length || doc.value.plans.some(p => !quantities.has(p.id))) {
    ElMessage.warning(tr("流程列表已变化，请重新打开作业数量设置。", "The process list has changed. Reopen job quantity settings."));
    return;
  }
  record();
  for (const p of doc.value.plans) p.quantity = quantities.get(p.id)!;
  record();
  cancelQuantitySettings();
}
function record() {
  if (!doc.value) return;
  const next = JSON.stringify(doc.value);
  if (next === snapshot.value) return;
  past.value.push(snapshot.value);
  if (past.value.length > 50) past.value.shift();
  future.value = [];
  snapshot.value = next;
  result.value = undefined;
}
function undo() {
  record();
  const next = past.value.pop();
  if (!next) return;
  future.value.push(snapshot.value);
  snapshot.value = next;
  doc.value = JSON.parse(next);
  result.value = undefined;
}
function redo() {
  const next = future.value.pop();
  if (!next) return;
  past.value.push(snapshot.value);
  snapshot.value = next;
  doc.value = JSON.parse(next);
  result.value = undefined;
}
watch(
  doc,
  () => {
    result.value = undefined;
  },
  { deep: true, flush: "sync" },
);
watch(
  () => doc.value?.plans.map((p) => p.id).join(","),
  () => {
    if (!doc.value?.plans.some((p) => p.id === planId.value))
      planId.value = doc.value?.plans[0]?.id || "";
  },
);
watch(
  () => [planId.value, plan.value?.chains.map((c) => c.id).join(",")],
  () => {
    if (!plan.value?.chains.some((c) => c.id === chainId.value))
      chainId.value = plan.value?.chains[0]?.id || "";
  },
);
async function confirmDiscard() {
  if (!dirty.value) return true;
  try {
    await ElMessageBox.confirm(
      tr(
        "当前方案有未保存的修改，是否放弃这些修改？",
        "Discard unsaved changes to this scheme?",
      ),
      tr("未保存修改", "Unsaved changes"),
      {
        type: "warning",
        confirmButtonText: tr("放弃修改", "Discard"),
        cancelButtonText: tr("继续编辑", "Keep editing"),
      },
    );
    return true;
  } catch {
    return false;
  }
}
function accept(data: SchemeDetail) {
  cancelQuantitySettings();
  detail.value = data;
  doc.value = clone(data.document);
  normalizePlanQuantities(doc.value);
  doc.value.analysisSettings = resolveAnalysisSettings(doc.value);
  saved.value = JSON.stringify(doc.value);
  snapshot.value = saved.value;
  past.value = [];
  future.value = [];
  result.value = undefined;
}
async function list() {
  const response = await axios.get<SchemeSummary[]>(base);
  schemes.value = response.data;
}
async function load(id: string) {
  if (busy.value || !(await confirmDiscard())) return;
  busy.value = true;
  try {
    accept((await axios.get<SchemeDetail>(`${base}/${id}`)).data);
    manager.value = false;
  } catch {
  } finally {
    busy.value = false;
  }
}
function create() {
  if (busy.value || editingMetadata.value) return;
  search.value = "";
  metadataDraft.value = { name: "", description: "" };
  newScheme.value = {
    id: `draft:${uid()}`,
    ownerId: "",
    ownerName: auth.username,
    name: "",
    description: "",
    revision: 0,
    createdAt: "",
    updatedAt: "",
  };
  manager.value = true;
}
async function save(): Promise<boolean> {
  if (!doc.value || !detail.value) return false;
  record();
  const submitted = clone(doc.value);
  busy.value = true;
  try {
    const response = await axios.put<SchemeDetail>(
      `${base}/${detail.value.id}`,
      { revision: detail.value.revision, document: submitted },
    );
    detail.value = response.data;
    saved.value = JSON.stringify(submitted);
    await list();
    ElMessage.success(tr("方案已保存", "Scheme saved"));
    return true;
  } catch {
    return false;
  } finally {
    busy.value = false;
  }
}
async function editScheme(s: SchemeSummary) {
  if (busy.value || editingMetadata.value) return;
  busy.value = true;
  try {
    const current = detail.value?.id === s.id;
    // Use the loaded revision for the current scheme so a concurrent update
    // cannot silently advance the revision of an unsaved local drawing.
    editingScheme.value = current
      ? clone(detail.value!)
      : (await axios.get<SchemeDetail>(`${base}/${s.id}`)).data;
    const metadata = current && doc.value ? doc.value : editingScheme.value.document;
    metadataDraft.value = { name: metadata.name, description: metadata.description };
  } catch {
  } finally {
    busy.value = false;
  }
}
function cancelSchemeEdit() {
  if (busy.value) return;
  editingScheme.value = undefined;
  newScheme.value = undefined;
  metadataDraft.value = { name: "", description: "" };
}
function replaceSnapshotMetadata(value: string, metadata: SchemeDocument) {
  if (!value) return value;
  const document = JSON.parse(value) as SchemeDocument;
  document.name = metadata.name;
  document.description = metadata.description;
  return JSON.stringify(document);
}
async function saveSchemeMetadata(): Promise<boolean> {
  if (busy.value || !editingMetadata.value) return false;
  const name = metadataDraft.value.name.trim();
  const description = metadataDraft.value.description;
  if (!name || name.length > 120 || description.length > 2000) {
    ElMessage.warning(tr(
      "名称必填且最多 120 字符，说明最多 2000 字符。",
      "A name of up to 120 characters is required; the description allows up to 2000 characters.",
    ));
    return false;
  }
  const source = editingScheme.value;
  busy.value = true;
  try {
    if (newScheme.value) {
      const { data: created } = await axios.post<SchemeDetail>(base, {
        name, description, facilities: [], links: [], plans: [],
      });
      const { document, ...summary } = created;
      schemes.value = [{ ...summary, name: document.name, description: document.description }, ...schemes.value];
      // Creating a scheme must not discard the open drawing or its history.
      // The first scheme can be opened immediately; otherwise use Open.
      if (!detail.value) accept(created);
      newScheme.value = undefined;
      metadataDraft.value = { name: "", description: "" };
      ElMessage.success(tr("方案已创建", "Scheme created"));
      return true;
    }
    if (!source) return false;
    const response = await axios.put<SchemeDetail>(`${base}/${source.id}`, {
      revision: source.revision,
      document: { ...clone(source.document), name, description },
    });
    const updated = response.data;
    if (detail.value?.id === updated.id && doc.value) {
      const analysisResult = result.value;
      detail.value = updated;
      doc.value.name = updated.document.name;
      doc.value.description = updated.document.description;
      // Metadata is already saved. Retain local geometry/process changes and
      // their undo history without undoing this separate metadata save.
      saved.value = replaceSnapshotMetadata(saved.value, updated.document);
      snapshot.value = replaceSnapshotMetadata(snapshot.value, updated.document);
      past.value = past.value.map((value) => replaceSnapshotMetadata(value, updated.document));
      future.value = future.value.map((value) => replaceSnapshotMetadata(value, updated.document));
      result.value = analysisResult;
    }
    schemes.value = schemes.value.map((item) => item.id === updated.id ? {
      ...item,
      name: updated.document.name,
      description: updated.document.description,
      revision: updated.revision,
      updatedAt: updated.updatedAt,
    } : item);
    editingScheme.value = undefined;
    metadataDraft.value = { name: "", description: "" };
    ElMessage.success(tr("方案信息已保存", "Scheme details saved"));
    return true;
  } catch {
    return false;
  } finally {
    busy.value = false;
  }
}
async function removeScheme(s: SchemeSummary) {
  try {
    await ElMessageBox.confirm(
      tr(
        `删除“${s.name}”及其全部流程方案？此操作无法撤销。`,
        `Delete “${s.name}” and all its process plans? This cannot be undone.`,
      ),
      tr("删除方案", "Delete scheme"),
      { type: "warning" },
    );
  } catch {
    return;
  }
  busy.value = true;
  try {
    await axios.delete(`${base}/${s.id}`, { params: { revision: s.revision } });
    if (detail.value?.id === s.id) {
      detail.value = undefined;
      doc.value = undefined;
      saved.value = "";
      past.value = [];
      future.value = [];
    }
    await list();
  } catch {
  } finally {
    busy.value = false;
  }
}
async function analyze() {
  if (!doc.value || !detail.value || busy.value) return;
  if (dirty.value && !(await save())) return;
  busy.value = true;
  try {
    result.value = (
      await axios.post<AnalysisResult>(
        `${base}/${detail.value.id}/analysis`,
        {},
        { timeout: 60000 },
      )
    ).data;
  } catch {
  } finally {
    busy.value = false;
  }
}
function newPlan(copy = false) {
  if (!doc.value) return;
  if (doc.value.plans.length >= 50) {
    ElMessage.warning(
      tr(
        "每个布置图最多 50 套流程方案。",
        "A layout supports up to 50 process plans.",
      ),
    );
    return;
  }
  const p: ProcessPlan =
    copy && plan.value
      ? clone(plan.value)
      : {
          id: uid(),
          name: tr("流程方案 ", "Process plan ") + (doc.value.plans.length + 1),
          executionMode: "parallel",
          quantity: 1,
          chains: [],
        };
  if (copy) {
    p.id = uid();
    p.name = p.name.slice(0, 110) + tr(" 副本", " copy");
    p.executionMode = "parallel";
    p.chains.forEach((c) => (c.id = uid()));
  }
  doc.value.plans.push(p);
  planId.value = p.id;
  chainId.value = p.chains[0]?.id || "";
  record();
}
async function deletePlan() {
  if (!plan.value || !doc.value) return;
  try {
    await ElMessageBox.confirm(
      tr(
        "删除当前流程方案及其中所有作业内容？",
        "Delete this process plan and all its chains?",
      ),
      tr("删除流程方案", "Delete process plan"),
      { type: "warning" },
    );
  } catch {
    return;
  }
  doc.value.plans = doc.value.plans.filter((p) => p.id !== planId.value);
  record();
}
function addChain(copy = false) {
  if (!plan.value) return;
  if (copy && !chain.value) return;
  if (
    plan.value.chains.length >= 100 ||
    plan.value.chains.reduce((n, c) => n + c.steps.length, 0) +
      (copy ? chain.value!.steps.length : 0) >
      1000
  ) {
    ElMessage.warning(
      tr(
        "每套流程方案最多 100 项作业内容、1000 个步骤。",
        "Each plan supports up to 100 chains and 1,000 steps.",
      ),
    );
    return;
  }
  record();
  let number = plan.value.chains.length + 1;
  const prefix = tr("作业内容 ", "Process chain ");
  while (plan.value.chains.some((c) => c.name === prefix + number)) number++;
  const c: ProcessChain = copy
    ? clone(chain.value!)
    : {
        id: "",
        name: prefix + number,
        steps: [],
      };
  c.id = uid();
  if (copy) c.name = c.name.slice(0, 110) + tr(" 副本", " copy");
  plan.value.chains.push(c);
  chainId.value = c.id;
  record();
}
async function deleteChain() {
  if (!plan.value || !chain.value) return;
  const targetPlan = plan.value;
  const target = chain.value;
  try {
    await ElMessageBox.confirm(
      tr(
        `删除作业内容“${target.name}”？同方案的其他作业内容将保留。`,
        `Delete “${target.name}”? Other chains in this plan will be kept.`,
      ),
      tr("删除作业内容", "Delete chain"),
      { type: "warning" },
    );
  } catch {
    return;
  }
  record();
  targetPlan.chains = targetPlan.chains.filter((c) => c.id !== target.id);
  record();
}
function pick(id: string) {
  if (!chain.value || !doc.value) {
    ElMessage.info(
      tr(
        "请先创建并选择一项作业内容。",
        "Create and select a process chain first.",
      ),
    );
    return;
  }
  const last = chain.value.steps[chain.value.steps.length - 1];
  if (
    chain.value.steps.length >= 100 ||
    plan.value!.chains.reduce((n, c) => n + c.steps.length, 0) >= 1000
  ) {
    ElMessage.warning(
      tr(
        "每项作业内容最多 100 步，每套方案最多 1000 步。",
        "Up to 100 steps per chain and 1,000 per plan.",
      ),
    );
    return;
  }
  const options = last ? connectingLinks(doc.value, last.facilityId, id) : [];
  if (last && !options.length) {
    ElMessage.warning(
      tr(
        "这两处设施之间没有可沿当前方向通行的作业进路，请检查连接及其方向。",
        "No logistics link permits travel in this direction. Check the connection and its direction.",
      ),
    );
    return;
  }
  chain.value.steps.push({
    facilityId: id,
    dwellSeconds: 30,
    incomingLinkId: options[0]?.id || null,
    travelSeconds: last ? 10 : 0,
  });
  record();
}
function linksAt(index: number) {
  if (!doc.value || !chain.value || index === 0) return [];
  return connectingLinks(
    doc.value,
    chain.value.steps[index - 1]!.facilityId,
    chain.value.steps[index]!.facilityId,
  );
}
async function truncate(index: number) {
  if (!chain.value) return;
  try {
    await ElMessageBox.confirm(
      tr(
        "删除此设施及后续步骤？连接到此设施的走行步骤也会一并删除。",
        "Remove this facility and all following steps, including travel into this facility?",
      ),
      tr("截断作业内容", "Truncate chain"),
      { type: "warning" },
    );
  } catch {
    return;
  }
  chain.value.steps.splice(index);
  record();
}
async function demo() {
  if (!doc.value) return;
  if (doc.value.facilities.length || doc.value.plans.length) {
    ElMessage.info(
      tr(
        "示例只可添加到空方案。",
        "The example can only be added to an empty scheme.",
      ),
    );
    return;
  }
  doc.value.facilities = [
    {
      id: uid(),
      name: tr("收货入口", "Receiving"),
      kind: "circle",
      center: { x: 280, y: 700 },
      radius: 45,
      vertices: [],
    },
    {
      id: uid(),
      name: tr("暂存区", "Storage"),
      kind: "polygon",
      center: { x: 0, y: 0 },
      radius: 24,
      vertices: [
        { x: 650, y: 500 },
        { x: 1050, y: 500 },
        { x: 1050, y: 880 },
        { x: 650, y: 880 },
      ],
    },
    {
      id: uid(),
      name: tr("加工区", "Processing"),
      kind: "polygon",
      center: { x: 0, y: 0 },
      radius: 24,
      vertices: [
        { x: 1350, y: 500 },
        { x: 1750, y: 500 },
        { x: 1750, y: 880 },
        { x: 1350, y: 880 },
      ],
    },
    {
      id: uid(),
      name: tr("发货出口", "Shipping"),
      kind: "circle",
      center: { x: 2100, y: 700 },
      radius: 45,
      vertices: [],
    },
  ];
  const f = doc.value.facilities;
  doc.value.links = [
    {
      id: uid(),
      name: tr("收货通道", "Inbound"),
      from: { facilityId: f[0]!.id, kind: "center", index: 0 },
      to: { facilityId: f[1]!.id, kind: "midpoint", index: 3 },
      anchors: [],
    },
    {
      id: uid(),
      name: tr("加工通道", "Transfer"),
      from: { facilityId: f[1]!.id, kind: "midpoint", index: 1 },
      to: { facilityId: f[2]!.id, kind: "midpoint", index: 3 },
      anchors: [{ x: 1200, y: 1050 }],
    },
    {
      id: uid(),
      name: tr("发货通道", "Outbound"),
      from: { facilityId: f[2]!.id, kind: "midpoint", index: 1 },
      to: { facilityId: f[3]!.id, kind: "center", index: 0 },
      anchors: [],
    },
  ];
  newPlan();
  addChain();
  const firstChain = chain.value!;
  firstChain.name = tr("正向配送", "Forward delivery");
  plan.value!.quantity = 20;
  chain.value!.steps = f.map((f, i) => ({
    facilityId: f.id,
    dwellSeconds: i === 1 ? 120 : 30,
    incomingLinkId: i ? doc.value!.links[i - 1]!.id : null,
    travelSeconds: i ? 20 : 0,
  }));
  addChain();
  chain.value!.name = tr("反向回送", "Return delivery");
  chain.value!.steps = [...f].reverse().map((facility, i) => ({
    facilityId: facility.id,
    dwellSeconds: 30,
    incomingLinkId: i ? doc.value!.links[f.length - 1 - i]!.id : null,
    travelSeconds: i ? 20 : 0,
  }));
  chainId.value = firstChain.id;
  record();
}
function beforeUnload(e: BeforeUnloadEvent) {
  if (dirty.value) {
    e.preventDefault();
    e.returnValue = "";
  }
}
function shortcuts(e: KeyboardEvent) {
  if (manager.value || quantitySettingsOpen.value) return;
  if (
    (e.target as HTMLElement)?.closest(
      'input,textarea,[contenteditable="true"]',
    )
  )
    return;
  if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === "z") {
    e.preventDefault();
    if (!busy.value) (e.shiftKey ? redo : undo)();
  }
}
onBeforeRouteLeave(() => (busy.value ? false : confirmDiscard()));
onMounted(async () => {
  window.addEventListener("beforeunload", beforeUnload);
  window.addEventListener("keydown", shortcuts);
  busy.value = true;
  try {
    await list();
    if (schemes.value[0])
      accept(
        (await axios.get<SchemeDetail>(`${base}/${schemes.value[0].id}`)).data,
      );
  } catch {
  } finally {
    busy.value = false;
  }
});
onBeforeUnmount(() => {
  window.removeEventListener("beforeunload", beforeUnload);
  window.removeEventListener("keydown", shortcuts);
});
function toggleLocale() {
  locale.value = locale.value === "zh" ? "en" : "zh";
  localStorage.setItem("locale", locale.value);
}
</script>
<template>
  <section class="sketch-main" v-loading="busy">
    <header class="workspace-topbar">
      <div class="left-controls">
        <ActionButton
          :label="tr('返回首页', 'Home')"
          :icon="actionIcons.back"
          @click="router.push('/')"
        /><span class="module-title">{{
          tr("布置图分析", "Sketch Layout Planning")
        }}</span
        ><ActionButton
          :label="tr('方案管理', 'Manage schemes')"
          :icon="actionIcons.folder"
          :disabled="busy"
          @click="manager = true"
        /><el-select
          :model-value="detail?.id"
          :placeholder="tr('选择布置图方案', 'Select a scheme')"
          :disabled="busy"
          filterable
          class="scheme-select"
          @change="load"
          ><el-option
            v-for="s in schemes"
            :key="s.id"
            :value="s.id"
            :label="s.name" /></el-select
        ><ActionButton
          :label="tr('新建方案', 'New scheme')"
          :icon="actionIcons.add"
          :disabled="busy"
          @click="create"
        />
      </div>
      <div class="right-controls">
        <span v-if="doc" class="save-status" :class="{ dirty }">{{
          dirty ? tr("未保存", "Unsaved") : tr("已保存", "Saved")
        }}</span
        ><ActionButton
          :label="tr('撤销', 'Undo')"
          :icon="actionIcons.reset"
          :disabled="
            busy || (!past.length && (!doc || JSON.stringify(doc) === snapshot))
          "
          @click="undo"
        /><ActionButton
          :label="tr('重做', 'Redo')"
          :icon="actionIcons.forward"
          :disabled="busy || !future.length"
          @click="redo"
        /><ActionButton
          :label="tr('保存方案', 'Save scheme')"
          variant="icon-text"
          type="primary"
          :icon="actionIcons.save"
          :disabled="!doc || busy || !dirty"
          @click="save"
        /><ActionButton :label="tr('English', '中文')" @click="toggleLocale"
          ><span>{{ locale === "zh" ? "EN" : "中" }}</span></ActionButton
        ><ActionButton
          :label="auth.username"
          :icon="actionIcons.user"
          @click="router.push('/userinfo')"
        />
      </div>
    </header>
    <main :inert="busy">
      <template v-if="doc">
        <el-tabs v-model="tab" class="workspace-tabs">
          <el-tab-pane :label="tr('布置图设计', 'Layout design')" name="layout" class="design-pane"
            ><div v-if="!doc.facilities.length" class="pane-heading">
              <el-button
                size="small"
                @click="demo"
                >{{ tr("载入示例", "Load example") }}</el-button
              >
            </div>
            <SketchCanvas
              v-if="tab === 'layout'"
              :key="detail?.id"
              fill-height
              :document="doc"
              @change="record"
          /></el-tab-pane>
          <el-tab-pane
            :label="tr('作业流程设计', 'Process design')"
            name="process"
            class="design-pane"
          >
            <div class="plan-toolbar">
              <el-select
                v-model="planId"
                :aria-label="tr('当前流程方案', 'Current process plan')"
                :placeholder="tr('选择流程方案', 'Select process plan')"
                ><el-option
                  v-for="p in doc.plans"
                  :key="p.id"
                  :value="p.id"
                  :label="p.name" /></el-select
              ><ActionButton
                :label="tr('新增流程方案', 'New process plan')"
                :icon="actionIcons.add"
                variant="icon-text"
                :disabled="doc.plans.length >= 50"
                @click="newPlan(false)"
              /><ActionButton
                :label="tr('复制流程方案', 'Duplicate process plan')"
                :icon="actionIcons.copy"
                :disabled="!plan || doc.plans.length >= 50"
                @click="newPlan(true)"
              /><ActionButton
                :label="tr('删除流程方案', 'Delete process plan')"
                :icon="actionIcons.delete"
                :disabled="!plan"
                @click="deletePlan"
              /><el-input
                v-if="plan"
                v-model="plan.name"
                maxlength="120"
                :aria-label="tr('流程方案名称', 'Process plan name')"
                class="plan-name"
                @change="record"
              />
              <ActionButton
                :label="tr('作业数量设置', 'Job quantity settings')"
                :icon="actionIcons.settings"
                variant="icon-text"
                :disabled="busy || !doc.plans.length"
                @click="openQuantitySettings"
              />
            </div>

            <div
              v-if="plan"
              class="process-grid"
              :style="{
                '--chain-list-width': `${chainListWidth}px`,
                '--process-panel-width': `${processPanelWidth}px`,
              }"
            >
              <div class="process-canvas">
                <div class="process-canvas-heading">
                  <strong>{{
                    chain
                      ? tr("正在编排：", "Editing: ") + chain.name
                      : tr("请先新增作业内容", "Add a chain first")
                  }}</strong>
                  <span>{{
                    tr(
                      "点击设施仅追加到当前作业内容",
                      "Facility clicks append only to the selected chain",
                    )
                  }}</span>
                </div>
                <SketchCanvas
                  v-if="tab === 'process'"
                  :key="detail?.id"
                  fill-height
                  :document="doc"
                  process
                  :highlighted="highlighted"
                  :highlighted-links="highlightedLinks"
                  @pick="pick"
                />
              </div>
              <PaneDivider
                v-model="chainListWidth"
                reverse
                :min="220"
                :max="500"
                :reset-value="260"
                :label="tr('调整作业内容列表左边界', 'Resize chain list left edge')"
                @reset="resetProcessWidths"
              />
              <section
                class="parallel-chains"
                :aria-label="
                  tr('方案内的作业内容', 'Parallel chains in this plan')
                "
              >
                <div class="parallel-heading">
                  <div>
                    <h3>
                      {{ tr("作业内容", "Parallel process chains") }}
                      <span>{{ plan.chains.length }}</span>
                    </h3>
                    <p>
                      {{
                        tr(
                          "本流程方案的全部作业内容共用一个作业数量。选中卡片后编排步骤。",
                          "All parallel chains in this plan share one quantity. Select a card to edit its steps.",
                        )
                      }}
                    </p>
                  </div>
                  <label class="plan-quantity">
                    <span>{{ tr("作业数量", "Job quantity") }}</span>
                    <el-input-number
                      v-model="plan.quantity"
                      :aria-label="tr('流程方案作业数量', 'Process plan job quantity')"
                      :min="1"
                      :max="1000000"
                      :precision="0"
                      size="small"
                      @change="record"
                    />
                  </label>
                  <ActionButton
                    :label="tr('新增作业内容', 'Add parallel chain')"
                    :icon="actionIcons.add"
                    variant="icon-text"
                    type="primary"
                    :disabled="plan.chains.length >= 100"
                    @click="addChain(false)"
                  />
                </div>
                <div v-if="plan.chains.length" class="chain-cards">
                  <div
                    v-for="(c, i) in plan.chains"
                    :key="c.id"
                    class="chain-card"
                    :class="{ active: chainId === c.id }"
                    @click="selectChain(c.id)"
                  >
                    <button
                      type="button"
                      class="chain-card-heading"
                      :aria-pressed="chainId === c.id"
                      :aria-label="tr('编辑作业内容：', 'Edit chain: ') + c.name"
                      ><span class="chain-index">{{ i + 1 }}</span
                      ><strong>{{ c.name }}</strong
                      ><span
                        class="chain-state"
                        :class="{ draft: c.steps.length < 2 }"
                        >{{
                          c.steps.length < 2
                            ? tr("草稿", "Draft")
                            : tr("完整作业内容", "Complete")
                        }}</span
                      ></button
                    >
                    <span class="chain-card-metrics">
                      <span
                        :title="`${tr('每次耗时', 'Time per job')}：${chainDuration(c)} ${tr('秒', 's')}`"
                        ><span>{{ tr("单次", "Each") }}</span
                        ><b
                          >{{
                            chainDuration(c).toLocaleString(undefined, {
                              maximumFractionDigits: 2,
                            })
                          }}
                          {{ tr("秒", "s") }}</b
                        ></span
                      >
                      <span
                        :title="`${tr('总耗时', 'Total time')}：${chainDuration(c) * plan.quantity} ${tr('秒', 's')}`"
                        ><span>{{ tr("总计", "Total") }}</span
                        ><b
                          >{{
                            (chainDuration(c) * plan.quantity).toLocaleString(
                              undefined,
                              { maximumFractionDigits: 2 },
                            )
                          }}
                          {{ tr("秒", "s") }}</b
                        ></span
                      >
                    </span>
                  </div>
                </div>
                <p v-else class="muted">
                  {{
                    tr(
                      "点击“新增作业内容”，为本方案添加第一项作业内容。",
                      "Add a parallel chain to start this plan.",
                    )
                  }}
                </p>
                <p v-if="draftChains" class="draft-note">
                  {{
                    tr(
                      `有 ${draftChains} 项草稿作业内容。可先保存，全部作业内容至少包含两处设施后即可分析。`,
                      `${draftChains} draft chain(s). Save now, or add at least two facilities to every chain before analysis.`,
                    )
                  }}
                </p>
              </section>
              <PaneDivider
                :model-value="chainListWidth"
                :min="chainRightMin"
                :max="chainRightMax"
                :reset-value="260"
                :label="
                  tr('调整作业内容列表右边界', 'Resize chain list right edge')
                "
                @update:model-value="resizeChainRight"
                @reset="resetProcessWidths"
              />
              <aside class="process-panel">
                <div class="panel-heading">
                  <h3>
                    {{ tr("作业内容属性与步骤", "Chain properties and steps") }}
                  </h3>
                  <ActionButton
                    :label="tr('复制当前作业内容', 'Duplicate current chain')"
                    :icon="actionIcons.copy"
                    :disabled="!chain || plan.chains.length >= 100"
                    @click="addChain(true)"
                  /><ActionButton
                    :label="tr('删除作业内容', 'Delete chain')"
                    :icon="actionIcons.delete"
                    :disabled="!chain"
                    @click="deleteChain"
                  />
                </div>
                <template v-if="chain"
                  ><div class="chain-fields">
                    <label
                      >{{ tr("作业内容名称", "Chain name")
                      }}<el-input
                        v-model="chain.name"
                        maxlength="120"
                        :aria-label="tr('作业内容名称', 'Chain name')"
                        @change="record" /></label
                    >
                  </div>
                  <p class="muted">
                    {{
                      tr(
                        "依次点击设施添加步骤；设施停留与进路走行时间均为单次耗时，以秒计。所有作业内容统一使用左侧的作业数量。",
                        "Click facilities to add steps; facility dwell and link travel durations are seconds per job. All parallel chains share the quantity set on the left.",
                      )
                    }}
                  </p>
                  <ol
                    class="steps"
                    :aria-label="
                      tr('设施与进路作业步骤', 'Facility and link operations')
                    "
                  >
                    <template v-for="(step, i) in chain.steps" :key="i">
                      <li v-if="i" class="step step-link" data-step-kind="link">
                        <div class="step-kind">
                          <span>{{
                            tr("作业进路 · 走行", "Logistics link · Travel")
                          }}</span>
                        </div>
                        <div class="step-heading">
                          <b>{{ tr("步骤", "Step") }} {{ i * 2 }}</b
                          ><strong>{{ linkName(step.incomingLinkId) }}</strong>
                        </div>
                        <p class="step-direction">
                          {{ facilityName(chain.steps[i - 1]!.facilityId) }} →
                          {{ facilityName(step.facilityId) }}
                        </p>
                        <label v-if="linksAt(i).length > 1" class="link-choice"
                          >{{ tr("切换进路", "Choose link")
                          }}<el-select
                            v-model="step.incomingLinkId"
                            :aria-label="
                              tr(
                                `第 ${i} 段作业进路`,
                                `Travel leg ${i} logistics link`,
                              )
                            "
                            size="small"
                            @change="record"
                            ><el-option
                              v-for="l in linksAt(i)"
                              :key="l.id"
                              :value="l.id"
                              :label="l.name" /></el-select
                        ></label>
                        <label class="step-time"
                          >{{ tr("走行时间（秒）", "Travel time (seconds)")
                          }}<el-input-number
                            v-model="step.travelSeconds"
                            :aria-label="
                              tr(
                                `第 ${i} 段走行时间（秒）`,
                                `Travel leg ${i} time (seconds)`,
                              )
                            "
                            :min="0.01"
                            :max="31536000"
                            :step="5"
                            size="small"
                            @change="record"
                        /></label>
                      </li>
                      <li class="step step-facility" data-step-kind="facility">
                        <div class="step-kind">
                          <span>{{
                            tr("设施 · 停留", "Facility · Dwell")
                          }}</span>
                        </div>
                        <div class="step-heading">
                          <b>{{ tr("步骤", "Step") }} {{ i * 2 + 1 }}</b
                          ><strong>{{ facilityName(step.facilityId) }}</strong
                          ><ActionButton
                            :label="
                              tr(
                                '删除此设施及后续',
                                'Remove this facility and following steps',
                              )
                            "
                            :icon="actionIcons.delete"
                            @click="truncate(i)"
                          />
                        </div>
                        <label class="step-time"
                          >{{ tr("停留时间（秒）", "Dwell time (seconds)")
                          }}<el-input-number
                            v-model="step.dwellSeconds"
                            :aria-label="
                              tr(
                                `第 ${i + 1} 处设施停留时间（秒）`,
                                `Facility ${i + 1} dwell time (seconds)`,
                              )
                            "
                            :min="0"
                            :max="31536000"
                            :step="5"
                            size="small"
                            @change="record"
                        /></label>
                      </li>
                    </template>
                  </ol>
                  <el-empty
                    v-if="!chain.steps.length"
                    :description="
                      tr('点击设施开始编排', 'Click a facility to start')
                    "
                  />
                  <div class="chain-total">
                    <span
                      >{{ tr("设施停留", "Facility dwell") }}
                      <b>{{ chainDwell(chain) }}</b> {{ tr("秒", "s") }}</span
                    >
                    <span
                      >{{ tr("进路走行", "Link travel") }}
                      <b>{{ chainTravel(chain) }}</b> {{ tr("秒", "s") }}</span
                    >
                    <strong
                      >{{ tr("单次总时长", "Total per job") }}
                      {{ chainDuration(chain) }} {{ tr("秒", "s") }}</strong
                    >
                    <el-tag
                      v-if="chain.steps.length < 2"
                      type="warning"
                      size="small"
                      >{{
                        tr(
                          "草稿：至少需要两处设施",
                          "Draft: at least two facilities",
                        )
                      }}</el-tag
                    >
                  </div> </template
                ><el-empty
                  v-else
                  :description="
                    tr(
                      '新建作业内容后，在图中依次点选设施',
                      'Add a chain, then click facilities in sequence',
                    )
                  "
                />
              </aside>
            </div>
            <el-empty
              v-else
              :description="
                tr(
                  '每个布置图可建立多套独立流程方案',
                  'Create multiple independent process plans for this layout',
                )
              "
            />
          </el-tab-pane>
          <el-tab-pane
            :label="tr('布置图形分析', 'Layout analysis')"
            name="analysis"
          >
            <SketchAnalysis
              :key="detail?.id"
              :document="doc"
              :result="result"
              :busy="busy"
              :dirty="dirty"
              @analyze="analyze"
              @change="record"
          /></el-tab-pane>
        </el-tabs>
      </template>
      <el-empty
        v-else
        :description="
          tr(
            '创建第一份布置图方案，开始规划设施与作业流程。',
            'Create your first layout scheme to plan facilities and processes.',
          )
        "
        ><el-button type="primary" @click="create">{{
          tr("新建布置图方案", "New layout scheme")
        }}</el-button></el-empty
      >
    </main>
    <el-dialog
      v-model="quantitySettingsOpen"
      :title="tr('作业数量设置', 'Job quantity settings')"
      width="min(640px,94vw)"
      :close-on-click-modal="false"
      @closed="quantityDraft = []"
    >
      <p class="quantity-settings-hint">{{ tr(
        '每项作业流程的数量统一应用于其全部作业内容。应用后请保存方案。',
        'Each quantity applies to every parallel chain in that process plan. Save the scheme after applying.',
      ) }}</p>
      <el-table :data="quantityDraft" row-key="id" max-height="50vh"
        :empty-text="tr('暂无作业流程', 'No process plans')">
        <el-table-column type="index" :label="tr('序号', 'No.')" width="64" />
        <el-table-column prop="name" :label="tr('作业流程', 'Process plan')" min-width="180" show-overflow-tooltip />
        <el-table-column :label="tr('作业数量', 'Job quantity')" width="180">
          <template #default="{ row }">
            <el-input-number v-model="row.quantity" :min="1" :max="1000000" :step="1" :precision="0"
              controls-position="right" size="small" :disabled="busy"
              :aria-label="`${row.name} · ${tr('作业数量', 'Job quantity')}`" />
          </template>
        </el-table-column>
      </el-table>
      <template #footer>
        <div class="quantity-settings-actions">
          <ActionButton :label="tr('取消', 'Cancel')" :icon="actionIcons.close"
            variant="icon-text" @click="cancelQuantitySettings" />
          <ActionButton :label="tr('应用', 'Apply')" :icon="actionIcons.save"
            variant="icon-text" type="primary" :disabled="busy || !quantityDraft.length"
            @click="applyQuantitySettings" />
        </div>
      </template>
    </el-dialog>
    <el-dialog
      v-model="manager"
      :title="tr('布置图方案管理', 'Layout scheme manager')"
      width="min(1000px,94vw)"
      :close-on-press-escape="!busy" :close-on-click-modal="!busy" :show-close="!busy"
      @closed="cancelSchemeEdit"
      ><div class="manager-tools">
        <el-input
          v-model="search"
          :disabled="busy || editingMetadata"
          :placeholder="
            tr('搜索名称、说明或创建者', 'Search name, description or owner')
          "
          clearable
        /><ActionButton :label="tr('新建方案', 'New scheme')"
          :icon="actionIcons.add" :disabled="busy || editingMetadata" @click="create" />
      </div>
      <el-table :data="managerRows" row-key="id" :empty-text="tr('暂无方案', 'No schemes')"
        ><el-table-column
          prop="name"
          :label="tr('名称', 'Name')"
          min-width="180">
          <template #default="{ row }">
            <el-input v-if="isEditingRow(row)" v-model="metadataDraft.name"
              maxlength="120" :disabled="busy" :aria-label="tr('方案名称', 'Scheme name')"
              :placeholder="tr('名称必填', 'Name is required')"
              @keydown.enter.prevent="saveSchemeMetadata" @keydown.esc.stop.prevent="cancelSchemeEdit" />
            <span v-else>{{ row.name }}</span>
          </template>
        </el-table-column><el-table-column
          prop="description"
          :label="tr('说明', 'Description')"
          min-width="180"
          :show-overflow-tooltip="!editingMetadata">
          <template #default="{ row }">
            <el-input v-if="isEditingRow(row)" v-model="metadataDraft.description" type="textarea"
              :autosize="{ minRows: 2, maxRows: 5 }" maxlength="2000" :disabled="busy"
              :aria-label="tr('方案说明', 'Scheme description')"
              :placeholder="tr('说明（可选）', 'Description (optional)')"
              @keydown.ctrl.enter.prevent="saveSchemeMetadata" @keydown.meta.enter.prevent="saveSchemeMetadata"
              @keydown.esc.stop.prevent="cancelSchemeEdit" />
            <span v-else>{{ row.description }}</span>
          </template>
        </el-table-column><el-table-column
          prop="ownerName"
          :label="tr('创建者', 'Owner')"
          width="120" /><el-table-column
          :label="tr('更新时间', 'Updated')"
          width="170"
          ><template #default="{ row }">{{
            row.id === newScheme?.id ? tr('待保存', 'Not saved') : new Date(row.updatedAt).toLocaleString()
          }}</template></el-table-column
        ><el-table-column :label="tr('操作', 'Actions')" width="130"
          ><template #default="{ row }"
            ><div class="manager-actions">
              <template v-if="isEditingRow(row)">
                <ActionButton :label="tr('保存', 'Save')" :icon="actionIcons.save"
                  :disabled="busy || !metadataDraft.name.trim()" @click="saveSchemeMetadata" />
                <ActionButton :label="tr('取消', 'Cancel')" :icon="actionIcons.close"
                  :disabled="busy" @click="cancelSchemeEdit" />
              </template>
              <template v-else><ActionButton
              :label="tr('打开', 'Open')"
              :icon="actionIcons.folder"
              :disabled="busy || editingMetadata"
              @click="load(row.id)" /><ActionButton
              :label="tr('编辑', 'Edit')"
              :icon="actionIcons.edit"
              :disabled="busy || editingMetadata"
              @click="editScheme(row)" /><ActionButton
              :label="tr('删除', 'Delete')"
              :icon="actionIcons.delete"
              :disabled="busy || editingMetadata"
              @click="
                removeScheme(row)
              " /></template></div></template></el-table-column></el-table
    ></el-dialog>
  </section>
</template>
<style scoped>
.sketch-main {
  display: flex;
  flex-direction: column;
  height: 100dvh;
  min-width: 0;
  background: #f6f8fa;
  color: var(--sy-text, #334155);
  font-size: 13px;
}
.workspace-topbar {
  flex-shrink: 0;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  padding: 12px 20px;
  background: #fff;
  border-bottom: 1px solid var(--sy-border);
}
.left-controls,
.right-controls {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}
.module-title {
  font-weight: 500;
  margin-right: 12px;
  white-space: nowrap;
}
.scheme-select {
  width: 230px;
}
.save-status {
  font-size: 12px;
  color: #81908c;
}
.save-status.dirty {
  color: #bb8841;
}
main {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-width: 0;
  min-height: 0;
  overflow: hidden;
  padding: 16px 20px 24px;
}
.workspace-tabs {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-width: 0;
  min-height: 0;
}
.workspace-tabs :deep(> .el-tabs__header) { flex-shrink: 0; }
.workspace-tabs :deep(> .el-tabs__content) {
  flex: 1;
  min-width: 0;
  min-height: 0;
  overflow: auto;
}
.design-pane {
  display: flex;
  flex-direction: column;
  height: 100%;
  min-height: 0;
  overflow: auto;
}
.design-pane > :deep(.canvas-workspace) { min-height: 360px; }
.manager-actions { display: flex; align-items: center; gap: 4px; }
.pane-heading {
  flex-shrink: 0;
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 8px;
  font-size: 12px;
  color: #758195;
}
.plan-toolbar {
  flex-shrink: 0;
  display: flex;
  gap: 8px;
  align-items: center;
  margin-bottom: 14px;
  flex-wrap: wrap;
}
.plan-toolbar > .el-select {
  width: 240px;
}
.plan-name {
  width: 240px;
}
.quantity-settings-hint {
  margin: 0 0 16px;
  color: var(--sy-text-muted, #758195);
  font-size: 13px;
  line-height: 1.6;
}
.quantity-settings-actions {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
}
.parallel-chains {
  display: flex;
  flex-direction: column;
  padding: 12px;
  min-width: 0;
  min-height: 0;
  overflow: auto;
  align-self: stretch;
  background: white;
  border: 1px solid var(--sy-border);
  border-radius: 6px;
}
.parallel-heading {
  flex-shrink: 0;
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 12px;
}
.parallel-heading > div {
  flex-basis: 100%;
  min-width: 0;
}
.parallel-heading h3 {
  margin: 0;
  font-size: 14px;
  font-weight: 600;
}
.parallel-heading h3 span {
  margin-left: 6px;
  color: #4776a8;
}
.parallel-heading p {
  margin: 6px 0 0;
  font-size: 12px;
  color: #758195;
  line-height: 1.6;
}
.plan-quantity {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  width: 100%;
  font-size: 12px;
  color: #526477;
}
.chain-cards {
  display: flex;
  flex-direction: column;
  gap: 6px;
  margin-top: 14px;
  flex: 1;
  min-height: 0;
  overflow-y: auto;
  padding: 3px;
}
.chain-card {
  display: flex;
  flex-direction: column;
  gap: 6px;
  padding: 8px 10px;
  text-align: left;
  min-width: 0;
  font: inherit;
  color: inherit;
  background: #fafbfd;
  border: 1px solid #e0e7ee;
  border-radius: 6px;
  cursor: pointer;
  flex-shrink: 0;
}
.chain-card:hover,
.chain-card.active {
  border-color: #6e8fb4;
  background: #f0f5fb;
}
.chain-card:focus-within {
  outline: 2px solid #4776a8;
  outline-offset: 1px;
}
.chain-card-heading {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 0;
  border: 0;
  background: transparent;
  font: inherit;
  color: inherit;
  text-align: left;
  cursor: pointer;
}
.chain-card-heading:focus-visible {
  outline: none;
}
.chain-card-heading strong {
  flex: 1;
  min-width: 0;
  overflow-wrap: anywhere;
}
.chain-index {
  color: #4776a8;
  font-size: 12px;
}
.chain-state {
  color: #548370;
  font-size: 11px;
  white-space: nowrap;
}
.chain-state.draft,
.draft-note {
  flex-shrink: 0;
  color: #a27839;
}
.chain-card-metrics,
.draft-note {
  font-size: 12px;
}
.chain-card-metrics {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 6px;
  color: #758195;
  line-height: 1.6;
}
.chain-card-metrics > span {
  display: flex;
  min-width: 0;
  align-items: baseline;
  gap: 3px;
  white-space: nowrap;
}
.chain-card-metrics > span > span {
  flex-shrink: 0;
}
.chain-card-metrics b {
  font-weight: 500;
  color: #526477;
  overflow: hidden;
  text-overflow: ellipsis;
  text-align: right;
}
.draft-note {
  margin: 12px 0 0;
}
.process-grid {
  display: grid;
  flex: 1;
  min-height: 360px;
  grid-template-rows: minmax(0, 1fr);
  grid-template-columns: minmax(320px, 1fr) 8px var(--chain-list-width) 8px var(
      --process-panel-width
    );
  gap: 0;
  align-items: stretch;
  overflow-x: auto;
  padding-bottom: 4px;
}
.process-canvas {
  display: flex;
  flex-direction: column;
  min-width: 0;
  min-height: 0;
}
.process-canvas-heading {
  flex-shrink: 0;
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 8px 16px;
  margin: 0 0 10px;
  overflow-wrap: anywhere;
}
.process-canvas-heading span {
  font-size: 12px;
  color: #758195;
}
.process-panel {
  display: flex;
  flex-direction: column;
  background: white;
  border: 1px solid var(--sy-border);
  border-radius: 6px;
  padding: 14px;
  min-width: 0;
  min-height: 0;
  overflow: auto;
}
.process-panel > :not(.steps) { flex-shrink: 0; }
.panel-heading {
  display: flex;
  gap: 6px;
  align-items: center;
  margin-bottom: 12px;
}
.panel-heading h3 {
  font-size: 14px;
  font-weight: 500;
  margin: 0 auto 0 0;
}
.chain-fields {
  display: flex;
  gap: 12px;
  margin-top: 14px;
}
.chain-fields label {
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 6px;
  font-size: 12px;
}
.chain-fields label:first-child {
  flex: 1;
}
.muted {
  font-size: 12px;
  line-height: 1.6;
  color: #758195;
  margin: 12px 0;
}
.steps {
  flex: 1;
  min-height: 0;
  overflow-y: auto;
  list-style: none;
  padding: 0 4px 0 0;
  margin: 0;
}
.step {
  position: relative;
  border: 1px solid #e0e7ee;
  border-radius: 5px;
  padding: 10px;
  margin-bottom: 18px;
  background: #fafbfd;
}
.step:not(:first-child)::before {
  content: "↓";
  position: absolute;
  top: -19px;
  left: calc(50% - 5px);
  color: #94a3b8;
  line-height: 18px;
}
.step:last-child {
  margin-bottom: 0;
}
.step-facility {
  border-left: 3px solid #6e8fb4;
}
.step-link {
  margin-left: 14px;
  margin-right: 14px;
  border-color: #cfe2d9;
  border-left: 3px solid #6c9c87;
  background: #f3f8f5;
}
.step-kind {
  font-size: 11px;
  color: #4776a8;
  margin-bottom: 7px;
}
.step-link .step-kind {
  color: #548370;
}
.step-direction {
  font-size: 12px;
  color: #64748b;
  margin: 0 0 10px;
  overflow-wrap: anywhere;
}
.step-heading {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 10px;
}
.step-heading > b {
  font-size: 12px;
  background: #e4edf6;
  color: #4776a8;
  border-radius: 4px;
  padding: 0 6px;
  white-space: nowrap;
  height: 22px;
  text-align: center;
  line-height: 22px;
}
.step-heading > strong {
  font-weight: 500;
  flex: 1;
  overflow-wrap: anywhere;
  min-width: 0;
}
.step-heading > b {
  flex-shrink: 0;
}
.step-link .step-heading > b {
  background: #e2eee7;
  color: #548370;
}
.step-time,
.link-choice {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 10px;
  font-size: 12px;
  min-width: 0;
}
.link-choice {
  margin-bottom: 10px;
}
.link-choice .el-select {
  flex: 1;
  min-width: 0;
}
.step-time .el-input-number {
  width: 150px;
  max-width: 60%;
}
.chain-total {
  padding-top: 10px;
  display: flex;
  gap: 12px;
  align-items: center;
  flex-wrap: wrap;
  font-size: 12px;
  color: #758195;
}
.chain-total > strong {
  flex-basis: 100%;
  font-weight: 500;
  color: #526477;
}
.manager-tools {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 16px;
}
.manager-tools > .el-input {
  max-width: 400px;
}
@media (max-width: 1100px) {
  .workspace-topbar {
    align-items: flex-start;
    flex-direction: column;
  }
}
@media (max-width: 600px) {
  main {
    padding: 12px;
  }
  .module-title {
    width: 100%;
  }
}
</style>
