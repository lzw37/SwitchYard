<script setup lang="ts">
import {
  computed,
  nextTick,
  onMounted,
  onBeforeUnmount,
  ref,
  watch,
} from "vue";
import { ElMessage } from "element-plus";
import { useI18n } from "vue-i18n";
import ActionButton from "@/components/ui/ActionButton.vue";
import PaneDivider from "@/components/ui/PaneDivider.vue";
import SketchObjectProperties from "./SketchObjectProperties.vue";
import { actionIcons } from "@/components/ui/actionIcons";
import {
  center,
  curvePath,
  linkPoints,
  portPoint,
  ports,
  referenced,
  simplePolygon,
  gradeColor,
} from "./geometry";
import { connectionGeometry } from "./linkGeometry";
import { findRouteCrossings, MAX_CROSSINGS } from "./crossings";
import { polygonGeometry, polygonEdgeHit } from "./polygonGeometry";
import { clone, uid } from "./types";
import { crossingProbability, crossingLoadRatio, linkRelationMap, loadColor, UNKNOWN_ANALYSIS_COLOR } from "./analysisOverlay";
import {
  DEFAULT_FILL_COLOR,
  DEFAULT_STROKE_COLOR,
  DEFAULT_LINK_COLOR,
  normalizeColor,
  selectedColor,
  polygonTextColor,
} from "./colors";
import { insertPolygonVertex, removePolygonVertex } from "./polygonEditing";
import type { PolygonEditError } from "./polygonEditing";
import type {
  AnalysisResult,
  Facility,
  GridSettings,
  LogisticsLink,
  Point,
  Port,
  SchemeDocument,
} from "./types";
import {
  displayGridSpacing,
  gridPoint,
  gridSettings,
} from "./grid";
import { DEFAULT_VIEW, visibleWorld, fitDocument } from "./viewport";
const props = withDefaults(
  defineProps<{
    document: SchemeDocument;
    process?: boolean;
    analysisMode?: boolean;
    fillHeight?: boolean;
    analysisResult?: AnalysisResult;
    highlighted?: string[];
    highlightedLinks?: string[];
  }>(),
  { highlighted: () => [], highlightedLinks: () => [] },
);
const emit = defineEmits<{
  change: [];
  pick: [id: string];
}>();
const { locale } = useI18n();
const tr = (zh: string, en: string) => (locale.value === "zh" ? zh : en);
const readOnly = computed(() => props.process || props.analysisMode);
const svg = ref<SVGSVGElement>();
const tool = ref("select");
const selected = ref("");
const pointMenu = ref<{
  kind: "vertex" | "anchor";
  id: string;
  index: number;
  x: number;
  y: number;
}>();
const pointMenuElement = ref<HTMLElement>();
const pointMenuButton = ref<HTMLButtonElement>();
const inspectorWidth = ref(320);
const drawing = computed(() => !readOnly.value && tool.value !== "select");
const polygon = ref<Point[]>([]);
const source = ref<Port>();
const anchors = ref<Point[]>([]);
const cursor = ref<Point>({ x: 0, y: 0 });
const cursorVisible = ref(false);
const grid = computed(() => gridSettings(props.document.gridSettings));
const showLinkNames = computed(() => props.document.showLinkNames ?? false);
function updateLinkNameDisplay(value: boolean) {
  if (readOnly.value || value === showLinkNames.value) return;
  props.document.showLinkNames = value;
  emit("change");
}
const showCurveArc = computed(() => props.document.showCurveArc ?? true);
function updateCurveDisplay(value: boolean) {
  if (readOnly.value || value === showCurveArc.value) return;
  props.document.showCurveArc = value;
  emit("change");
}
const showPolygonFillets = computed(
  () => props.document.showPolygonFillets ?? true,
);
function updatePolygonFillets(value: boolean) {
  if (readOnly.value || value === showPolygonFillets.value) return;
  props.document.showPolygonFillets = value;
  emit("change");
}
const showCrossings = computed(() => props.analysisMode || (props.document.showCrossings ?? true));
const crossing = computed(() =>
  props.document.crossings?.find((c) => c.id === selected.value),
);
function updateCrossingDisplay(value: boolean) {
  if (readOnly.value || value === showCrossings.value) return;
  if (!value && crossing.value) selected.value = "";
  props.document.showCrossings = value;
  emit("change");
}
const polygonPaths = computed(
  () =>
    new Map(
      props.document.facilities
        .filter((f) => f.kind === "polygon")
        .map((f) => [
          f.id,
          polygonGeometry(f.vertices, showPolygonFillets.value).path,
        ]),
    ),
);
const gridPatternId = `sketch-grid-${uid()}`;
const canvasSize = ref({ width: DEFAULT_VIEW.w, height: DEFAULT_VIEW.h });
const displayScale = computed(() =>
  Math.max(
    Number.EPSILON,
    Math.min(
      canvasSize.value.width / view.value.w,
      canvasSize.value.height / view.value.h,
    ),
  ),
);
const visibleGridSpacing = computed(() =>
  displayGridSpacing(grid.value.spacing, displayScale.value),
);
const showSnapCursor = computed(
  () =>
    !readOnly.value &&
    grid.value.snapToGrid &&
    cursorVisible.value &&
    !dragging.value &&
    (tool.value === "polygon" ||
      tool.value === "circle" ||
      (tool.value === "link" && !!source.value)),
);
const dragging = ref(false);
const view = ref({ ...DEFAULT_VIEW });
const visibleArea = computed(() => visibleWorld(view.value, canvasSize.value));
function fitAll() {
  analysisHover.value = undefined;
  view.value = fitDocument(props.document, canvasSize.value, showCrossings.value);
}
let resizeObserver: ResizeObserver | undefined;
onMounted(() => {
  let fitted = false;
  const updateSize = () => {
    const bounds = svg.value?.getBoundingClientRect();
    if (bounds && bounds.width > 0 && bounds.height > 0) {
      canvasSize.value = { width: bounds.width, height: bounds.height };
      if (!fitted) { fitted = true; fitAll(); }
    }
  };
  updateSize();
  resizeObserver = new ResizeObserver(updateSize);
  if (svg.value) resizeObserver.observe(svg.value);
  document.addEventListener("pointerdown", dismissPointMenu, true);
  window.addEventListener("resize", closePointMenu);
  window.addEventListener("scroll", closePointMenu, true);
});
onBeforeUnmount(() => {
  resizeObserver?.disconnect();
  document.removeEventListener("pointerdown", dismissPointMenu, true);
  window.removeEventListener("resize", closePointMenu);
  window.removeEventListener("scroll", closePointMenu, true);
});
function updateGrid(change: Partial<GridSettings>) {
  if (readOnly.value) return;
  // Older schemes use defaults without becoming dirty merely by being opened.
  props.document.gridSettings = gridSettings({ ...grid.value, ...change });
  emit("change");
}
function updateGridSpacing(value: number | undefined) {
  if (value != null && Number.isFinite(value)) updateGrid({ spacing: value });
}
function drawingPoint(p: Point): Point {
  return gridPoint(p, grid.value);
}
const viewBox = computed(
  () => `${view.value.x} ${view.value.y} ${view.value.w} ${view.value.h}`,
);
const facility = computed(() =>
  props.document.facilities.find((f) => f.id === selected.value),
);
const link = computed(() =>
  props.document.links.find((l) => l.id === selected.value),
);
const hint = computed(() =>
  props.analysisMode
    ? tr("悬停查看作业等级、负荷率及冲突概率；拖动空白平移，滚轮缩放。", "Hover for flow grades, load ratios and conflict probabilities; drag background to pan and scroll to zoom.")
    : props.process
    ? tr(
        "依次点击设施添加到当前作业内容；拖动画板可平移。",
        "Click facilities in order to build the selected chain; drag background to pan.",
      )
    : tool.value === "polygon"
      ? tr(
          "逐点绘制，点击首点或 Enter 闭合；Esc 退出绘图模式。",
          "Click to add vertices; click the first vertex or Enter to close. Esc returns to normal mode.",
        )
      : tool.value === "circle"
        ? tr(
            "点击画板添加出入口圆圈；Esc 退出绘图模式。",
            "Click to add an entrance / exit. Esc returns to normal mode.",
          )
        : tool.value === "link"
          ? tr(
              "点击圆心、顶点或边中点连接，空白处添加拐点；Esc 退出绘图模式。",
              "Connect centers, vertices or midpoints; click background for corners. Esc returns to normal mode.",
            )
          : tr(
              "双击图元或作业进路重命名；双击多边形边添加顶点；Shift＋双击作业进路添加锚点；右键顶点或锚点删除。",
              "Double-click an object or link to rename; double-click polygon edges for vertices; Shift+double-click links for anchors; right-click handles to delete.",
            ),
);
watch(
  () => props.document,
  () => {
    selected.value = "";
    cancel();
  },
);
watch(tool, () => cancel(), { flush: "sync" });
watch(selected, closePointMenu, { flush: "sync" });
watch(
  () => readOnly.value,
  () => {
    cancel();
    selected.value = "";
    tool.value = "select";
  },
);
function cancel() {
  closePointMenu();
  polygon.value = [];
  source.value = undefined;
  anchors.value = [];
}
function closePointMenu() {
  pointMenu.value = undefined;
}
function dismissPointMenu(e: PointerEvent) {
  if (!pointMenuElement.value?.contains(e.target as Node)) closePointMenu();
}
function openPointMenu(
  e: MouseEvent,
  kind: "vertex" | "anchor",
  id: string,
  index: number,
) {
  if (readOnly.value || tool.value !== "select") return;
  selected.value = id;
  pointMenu.value = {
    kind,
    id,
    index,
    x: Math.max(8, Math.min(e.clientX, (window.innerWidth || 1024) - 164)),
    y: Math.max(8, Math.min(e.clientY, (window.innerHeight || 768) - 52)),
  };
  nextTick(() => pointMenuButton.value?.focus({ preventScroll: true }));
}
function reportPolygonEditError(error: PolygonEditError) {
  const messages = {
    minimum: tr(
      "多边形至少需要保留 3 个顶点。",
      "A polygon must retain at least three vertices.",
    ),
    limit: tr(
      "每个多边形最多支持 100 个顶点。",
      "A polygon supports up to 100 vertices.",
    ),
    connected: tr(
      "该顶点被作业进路引用，请先调整或删除相应连接。",
      "This vertex is a link endpoint. Adjust or remove that connection first.",
    ),
    invalid: tr(
      "修改会使多边形自交、重叠或面积为零，未应用修改。",
      "This change would create an invalid polygon and was not applied.",
    ),
  };
  ElMessage.warning(messages[error]);
}
const rename = ref<{ id: string; draft: string; position?: Point }>();
const renameInput = ref<HTMLInputElement>();
const renameTarget = computed(() => {
  const id = rename.value?.id;
  return props.document.facilities.find(f => f.id === id)
    ?? props.document.links.find(l => l.id === id)
    ?? props.document.crossings?.find(c => c.id === id);
});
const renamePosition = computed(() => {
  if (rename.value?.position) return rename.value.position;
  const id = rename.value?.id;
  const f = props.document.facilities.find(f => f.id === id);
  if (f) {
    const p = center(f);
    return { x: p.x, y: p.y + (f.kind === "circle" ? f.radius + 28 : 5) };
  }
  const c = props.document.crossings?.find(c => c.id === id);
  if (c) return { x: c.center.x, y: c.center.y - c.radius - 6 / displayScale.value };
  const middle = renderedLinks.value.find(item => item.link.id === id)?.middle;
  return middle ? { x: middle.x, y: middle.y - 12 / displayScale.value } : undefined;
});
const renameBox = computed(() => {
  const position = renamePosition.value;
  if (!position) return undefined;
  const scale = displayScale.value;
  const area = visibleArea.value;
  const margin = 6 / scale;
  const width = Math.max(1, Math.min(200, canvasSize.value.width - 12)) / scale;
  const height = Math.max(1, Math.min(34, canvasSize.value.height - 12)) / scale;
  return {
    x: Math.max(area.x + margin, Math.min(position.x - width / 2, area.x + area.w - width - margin)),
    y: Math.max(area.y + margin, Math.min(position.y - 24 / scale, area.y + area.h - height - margin)),
    width, height,
  };
});
function cancelRename() {
  rename.value = undefined;
}
function beginRename(id: string, event?: MouseEvent) {
  if (readOnly.value || tool.value !== "select") return;
  const target = props.document.facilities.find(f => f.id === id)
    ?? props.document.links.find(l => l.id === id)
    ?? props.document.crossings?.find(c => c.id === id);
  if (!target) return;
  closePointMenu();
  selected.value = id;
  rename.value = { id, draft: target.name, position: event ? point(event) : undefined };
  const session = rename.value;
  nextTick(() => {
    if (rename.value !== session) return;
    renameInput.value?.focus({ preventScroll: true });
    renameInput.value?.select();
  });
}
function commitRename(onBlur = false) {
  if (!rename.value) return;
  const target = renameTarget.value;
  if (!target || readOnly.value || tool.value !== "select") {
    cancelRename();
    return;
  }
  const name = rename.value.draft.trim();
  if (!name || name.length > 120) {
    ElMessage.warning(tr("名称不能为空，且最多 120 字符。", "Enter a name of 1–120 characters."));
    if (onBlur) cancelRename();
    else renameInput.value?.focus({ preventScroll: true });
    return;
  }
  cancelRename();
  if (name !== target.name) {
    target.name = name;
    emit("change");
  }
}
function doubleClickLink(e: MouseEvent, id: string) {
  if (e.shiftKey) addAnchor(e, id);
  else beginRename(id, e);
}
function doubleClickFacility(e: MouseEvent, id: string) {
  if (readOnly.value || tool.value !== "select") return;
  const f = props.document.facilities.find(f => f.id === id);
  if (!f) return;
  if (f.kind === "polygon") {
    const hit = polygonEdgeHit(f.vertices, point(e), showPolygonFillets.value);
    if (hit && hit.distance * displayScale.value <= 8) {
      addVertexOnEdge(e, id);
      return;
    }
  }
  beginRename(id);
}
watch(tool, cancelRename, { flush: "sync" });
watch(() => props.document, cancelRename, { flush: "sync" });
watch(readOnly, cancelRename, { flush: "sync" });
watch(selected, (id) => {
  if (rename.value && rename.value.id !== id) commitRename(true);
}, { flush: "sync" });
function addVertexOnEdge(e: MouseEvent, id = selected.value) {
  if (readOnly.value || tool.value !== "select") return;
  const f = props.document.facilities.find((f) => f.id === id);
  if (!f || f.kind !== "polygon") return;
  const hit = polygonEdgeHit(f.vertices, point(e), showPolygonFillets.value);
  if (!hit || hit.distance * displayScale.value > 8) return;
  if (
    f.vertices.some(
      (p) =>
        Math.hypot(p.x - hit.point.x, p.y - hit.point.y) * displayScale.value <=
        6,
    )
  )
    return;
  closePointMenu();
  const error = insertPolygonVertex(props.document, id, hit.index, hit.point);
  if (error) reportPolygonEditError(error);
  else {
    selected.value = id;
    emit("change");
  }
}
function removeCurveAnchor(id: string, index: number) {
  if (readOnly.value || tool.value !== "select") return;
  const l = props.document.links.find((l) => l.id === id);
  if (!l || index < 0 || index >= l.anchors.length) return;
  l.anchors.splice(index, 1);
  emit("change");
}
function deletePointFromMenu() {
  const target = pointMenu.value;
  closePointMenu();
  if (!target || readOnly.value || tool.value !== "select") return;
  if (target.kind === "anchor") removeCurveAnchor(target.id, target.index);
  else {
    const error = removePolygonVertex(props.document, target.id, target.index);
    if (error) reportPolygonEditError(error);
    else emit("change");
  }
  svg.value?.focus?.({ preventScroll: true });
}
function activateTool(next: string) {
  if (readOnly.value) return;
  cancel();
  selected.value = "";
  moved = false;
  tool.value = next;
  svg.value?.focus?.({ preventScroll: true });
}
function exitDrawingMode() {
  cancel();
  tool.value = "select";
}
const renderedLinks = computed(() =>
  props.document.links.map((link) => {
    const points = linkPoints(link, props.document.facilities);
    return { link, ...connectionGeometry(points, showCurveArc.value) };
  }),
);
function generateCrossings() {
  if (readOnly.value || drawing.value) return;
  const paths = renderedLinks.value;
  const points = findRouteCrossings(paths);
  if (!points || points.length > MAX_CROSSINGS) {
    ElMessage.warning(
      tr(
        "最多支持 1000 个进路交叉标记，请减少进路后再生成。原有标记保持不变。",
        "A scheme supports up to 1000 crossing markers. Reduce links first. Existing markers are unchanged.",
      ),
    );
    return;
  }
  const hadCrossings = !!props.document.crossings?.length;
  const selectedCrossing = !!crossing.value;
  const generated = points.map(({ x, y, pathIndices }, index) => ({
    id: uid(),
    name: tr("进路交叉 ", "Route crossing ") + (index + 1),
    center: { x, y },
    radius: 24,
    sourceLinks: pathIndices.map((index) => ({
      linkId: paths[index]!.link.id,
      name: paths[index]!.link.name,
    })),
  }));
  // Replace the entire collection in one undoable change after validation.
  if (hadCrossings || generated.length) {
    props.document.crossings = generated;
    if (generated.length) props.document.showCrossings = true;
    if (generated[0]) selected.value = generated[0].id;
    else if (selectedCrossing) selected.value = "";
    emit("change");
  }
  if (generated.length)
    ElMessage.success(
      tr(
        `已清空原有交叉，重新生成 ${generated.length} 个进路交叉标记。`,
        `Cleared previous crossings and generated ${generated.length} route crossing markers.`,
      ),
    );
  else
    ElMessage.info(
      tr(
        "当前没有交点，进路交叉标记已清空。",
        "No intersections. Route crossing markers have been cleared.",
      ),
    );
}
function linkColor(l: LogisticsLink) {
  if (props.analysisMode)
    return gradeColor[analysisRelations.value.get(l.id)?.grade ?? ""] ?? UNKNOWN_ANALYSIS_COLOR;
  const color = normalizeColor(l.color);
  return selected.value === l.id
    ? color
      ? selectedColor(color)
      : "#cf9250"
    : props.highlightedLinks.includes(l.id)
      ? "#498e75"
      : (color ?? DEFAULT_LINK_COLOR);
}
function polygonFill(f: Facility) {
  return props.highlighted.includes(f.id)
    ? "#e2f0e9"
    : (normalizeColor(f.fillColor) ?? DEFAULT_FILL_COLOR);
}
function polygonStroke(f: Facility) {
  const color = normalizeColor(f.strokeColor);
  return selected.value === f.id
    ? color
      ? selectedColor(color)
      : "#cc9452"
    : props.highlighted.includes(f.id)
      ? "#498e75"
      : (color ?? DEFAULT_STROKE_COLOR);
}
function point(e: PointerEvent | MouseEvent | WheelEvent): Point {
  const p = svg.value!.createSVGPoint();
  p.x = e.clientX;
  p.y = e.clientY;
  const q = p.matrixTransform(svg.value!.getScreenCTM()!.inverse());
  return { x: q.x, y: q.y };
}
function finish() {
  if (readOnly.value) return;
  if (!simplePolygon(polygon.value)) {
    ElMessage.warning(
      tr(
        "多边形不能自交、重叠或面积为零，且至少需要 3 个顶点。",
        "Use at least three vertices with a nonzero area and no self-intersections.",
      ),
    );
    return;
  }
  const f: Facility = {
    id: uid(),
    name: tr("设施 ", "Facility ") + (props.document.facilities.length + 1),
    kind: "polygon",
    vertices: clone(polygon.value),
    center: { x: 0, y: 0 },
    radius: 24,
  };
  props.document.facilities.push(f);
  selected.value = f.id;
  cancel();
  emit("change");
}
function background(e: MouseEvent) {
  if (moved) {
    moved = false;
    return;
  }
  if (readOnly.value) return;
  const p = drawingPoint(point(e));
  if (tool.value === "polygon") {
    const samePoint = (q: Point | undefined) =>
      q != null && Math.abs(p.x - q.x) + Math.abs(p.y - q.y) < 1e-6;
    // Snapping can return to the first vertex even outside its visible hit circle.
    if (polygon.value.length > 1 && samePoint(polygon.value[0])) finish();
    else if (!samePoint(polygon.value[polygon.value.length - 1]))
      polygon.value.push(p);
  } else if (tool.value === "circle") {
    const f: Facility = {
      id: uid(),
      name: tr("出入口 ", "Gate ") + (props.document.facilities.length + 1),
      kind: "circle",
      vertices: [],
      center: p,
      radius: 24,
    };
    props.document.facilities.push(f);
    selected.value = f.id;
    emit("change");
  } else if (tool.value === "link" && source.value) {
    if (anchors.value.length < 24) anchors.value.push(p);
  } else selected.value = "";
}
function pickPort(p: Port) {
  if (readOnly.value || tool.value !== "link") return;
  if (!source.value) {
    source.value = clone(p);
    return;
  }
  if (source.value.facilityId === p.facilityId) {
    ElMessage.warning(tr("请选择另一处设施。", "Choose another facility."));
    return;
  }
  const l: LogisticsLink = {
    id: uid(),
    name: tr("作业进路 ", "Link ") + (props.document.links.length + 1),
    from: clone(source.value),
    to: clone(p),
    anchors: clone(anchors.value),
    direction: "both",
  };
  props.document.links.push(l);
  selected.value = l.id;
  cancel();
  emit("change");
}
let drag:
  | {
      kind: "facility" | "vertex" | "anchor" | "crossing" | "pan";
      captureTarget?: Element;
      id: string;
      index: number;
      start: Point;
      clientStart: Point;
      scale: number;
      original: SchemeDocument;
      view: {
        x: number;
        y: number;
        w: number;
        h: number;
      };
    }
  | undefined;
let moved = false;
function start(
  e: PointerEvent,
  kind: "facility" | "vertex" | "anchor" | "crossing" | "pan",
  id = "",
  index = 0,
) {
  if (e.button !== 0 || (!readOnly.value && tool.value !== "select")) return;
  if (readOnly.value && kind !== "pan") return;
  if (kind !== "pan") selected.value = id;
  const matrix = svg.value?.getScreenCTM();
  drag = {
    kind,
    captureTarget: (e.currentTarget as Element | null) ?? svg.value,
    id,
    index,
    start: point(e),
    clientStart: { x: e.clientX, y: e.clientY },
    scale: matrix
      ? Math.hypot(matrix.a, matrix.b) || displayScale.value
      : displayScale.value,
    original: clone(props.document),
    view: { ...view.value },
  };
  dragging.value = true;
  moved = false;
}
function leaveCanvas() {
  cursorVisible.value = false;
  analysisHover.value = undefined;
  // A click that leaves the canvas before crossing the drag threshold has no
  // pointer capture. Discard it so a later hover cannot move the old target.
  if (drag && !moved) {
    drag = undefined;
    dragging.value = false;
  }
}
function move(e: PointerEvent) {
  cursorVisible.value = true;
  cursor.value = drawingPoint(point(e));
  if (!drag) return;
  const screenDx = e.clientX - drag.clientStart.x,
    screenDy = e.clientY - drag.clientStart.y;
  if (!moved && Math.hypot(screenDx, screenDy) > 4) {
    moved = true;
    // Leave ordinary clicks untouched. Capturing on pointerdown can retarget
    // the click sequence to an SVG group and suppress the native double-click.
    drag.captureTarget?.setPointerCapture(e.pointerId);
  }
  if (drag.kind === "pan") {
    if (moved)
      view.value = {
        ...drag.view,
        x: drag.view.x - screenDx / drag.scale,
        y: drag.view.y - screenDy / drag.scale,
      };
    return;
  }
  const p = point(e),
    dx = p.x - drag.start.x,
    dy = p.y - drag.start.y;
  // Selecting an off-grid element must not move it until the pointer actually drags.
  if (!moved) return;
  if (drag.kind === "crossing") {
    const target = props.document.crossings?.find((c) => c.id === drag!.id);
    const original = drag.original.crossings?.find((c) => c.id === drag!.id);
    if (target && original)
      target.center = gridPoint(
        { x: original.center.x + dx, y: original.center.y + dy },
        grid.value,
      );
    return;
  }
  if (drag.kind === "anchor") {
    const l = props.document.links.find((l) => l.id === drag!.id)!;
    const q = drag.original.links.find((l) => l.id === drag!.id)!.anchors[
      drag.index
    ]!;
    l.anchors[drag.index] = gridPoint({ x: q.x + dx, y: q.y + dy }, grid.value);
    return;
  }
  const f = props.document.facilities.find((f) => f.id === drag!.id)!,
    old = drag.original.facilities.find((f) => f.id === drag!.id)!;
  if (drag.kind === "vertex") {
    const q = old.vertices[drag.index]!;
    f.vertices[drag.index] = gridPoint(
      { x: q.x + dx, y: q.y + dy },
      grid.value,
    );
    return;
  }
  // Translate the shape as a whole: snap its first vertex (or circle center), preserving all dimensions.
  const reference = old.kind === "circle" ? old.center : old.vertices[0]!;
  const target = gridPoint(
    { x: reference.x + dx, y: reference.y + dy },
    grid.value,
  );
  const ox = target.x - reference.x,
    oy = target.y - reference.y;
  if (f.kind === "circle")
    f.center = { x: old.center.x + ox, y: old.center.y + oy };
  else f.vertices = old.vertices.map((p) => ({ x: p.x + ox, y: p.y + oy }));
}
function stop(e: PointerEvent) {
  if (!drag) return;
  if (
    drag.kind === "vertex" &&
    facility.value &&
    !simplePolygon(facility.value.vertices)
  ) {
    facility.value.vertices = drag.original.facilities.find(
      (f) => f.id === drag!.id,
    )!.vertices;
    ElMessage.warning(
      tr(
        "修改会使多边形自交，已还原。",
        "The polygon would intersect itself; change reverted.",
      ),
    );
  }
  const changed = drag.kind !== "pan" && moved;
  // Pointer capture retargets a background click to the SVG, so clear selection here.
  if (drag.kind === "pan" && !moved && !readOnly.value) selected.value = "";
  const captureTarget = drag.captureTarget;
  drag = undefined;
  dragging.value = false;
  if (captureTarget?.hasPointerCapture(e.pointerId))
    captureTarget.releasePointerCapture(e.pointerId);
  if (changed) emit("change");
}
function selectFacility(e: MouseEvent, id: string) {
  if (props.analysisMode) return;
  if (moved) {
    moved = false;
    return;
  }
  if (props.process) {
    emit("pick", id);
    return;
  }
  if (tool.value === "select") selected.value = id;
  else if (tool.value === "polygon") background(e);
  else if (tool.value === "link") {
    const f = props.document.facilities.find((f) => f.id === id)!;
    if (f.kind === "circle") pickPort(ports(f)[0]!);
  }
}
function selectLink(e: MouseEvent, id: string) {
  if (moved) {
    moved = false;
    return;
  }
  if (readOnly.value) return;
  if (tool.value === "select") selected.value = id;
  else if (tool.value === "polygon") background(e);
}
function addAnchor(e: MouseEvent, id: string) {
  if (readOnly.value || tool.value !== "select") return;
  const l = props.document.links.find((l) => l.id === id)!;
  if (l.anchors.length >= 24) return;
  const p = gridPoint(point(e), grid.value);
  const pts = linkPoints(l, props.document.facilities);
  let at = 0,
    best = Infinity;
  for (let i = 0; i < pts.length - 1; i++) {
    const a = pts[i]!,
      b = pts[i + 1]!,
      vx = b.x - a.x,
      vy = b.y - a.y;
    const t = Math.max(
      0,
      Math.min(
        1,
        ((p.x - a.x) * vx + (p.y - a.y) * vy) / (vx * vx + vy * vy || 1),
      ),
    );
    const dist = (p.x - a.x - t * vx) ** 2 + (p.y - a.y - t * vy) ** 2;
    if (dist < best) {
      best = dist;
      at = i;
    }
  }
  l.anchors.splice(at, 0, p);
  selected.value = id;
  emit("change");
}
function remove() {
  if (readOnly.value) return;
  if (crossing.value) {
    props.document.crossings = props.document.crossings!.filter(
      (c) => c.id !== selected.value,
    );
    selected.value = "";
    emit("change");
    return;
  }
  const f = facility.value,
    l = link.value;
  if (!f && !l) return;
  if (
    referenced(props.document, f ? "facility" : "link", selected.value) ||
    (f &&
      props.document.links.some(
        (l) =>
          (l.from.facilityId === f.id || l.to.facilityId === f.id) &&
          referenced(props.document, "link", l.id),
      ))
  ) {
    ElMessage.warning(
      tr(
        "该元素被作业流程引用，请先修改相应流程。",
        "This element is used by a process. Edit that process first.",
      ),
    );
    return;
  }
  if (f) {
    props.document.facilities = props.document.facilities.filter(
      (x) => x.id !== f.id,
    );
    props.document.links = props.document.links.filter(
      (x) => x.from.facilityId !== f.id && x.to.facilityId !== f.id,
    );
  } else
    props.document.links = props.document.links.filter((x) => x.id !== l!.id);
  selected.value = "";
  emit("change");
}
function zoom(
  factor: number,
  p: Point = {
    x: view.value.x + view.value.w / 2,
    y: view.value.y + view.value.h / 2,
  },
) {
  analysisHover.value = undefined;
  if (!Number.isFinite(factor) || factor <= 0) return;
  const w = Math.max(40, view.value.w * factor),
    scale = w / view.value.w;
  const next = {
    x: p.x - (p.x - view.value.x) * scale,
    y: p.y - (p.y - view.value.y) * scale,
    w,
    h: view.value.h * scale,
  };
  if (Object.values(next).every(Number.isFinite) && next.h > 0) view.value = next;
}
function keyboard(e: KeyboardEvent) {
  if (e.key === "Escape") exitDrawingMode();
  if (e.key === "Enter" && polygon.value.length) finish();
  if (e.key === "Delete" && !readOnly.value) remove();
}
const analysisRelations = computed(() => linkRelationMap(props.document, props.analysisResult));
const analysisRisks = computed(() => new Map(props.analysisResult?.crossingRisks?.map((r) => [r.crossingId, r])));
const analysisHover = ref<{ kind: "link" | "crossing"; id: string; x: number; y: number }>();
const formatAnalysisPercent = (value: number) =>
  (value * 100).toLocaleString(locale.value, { maximumFractionDigits: 4 }) + "%";
function linkAnalysisText(id: string) {
  const target = props.document.links.find((l) => l.id === id);
  const relation = analysisRelations.value.get(id);
  return [target?.name ?? id, ...(relation ? [
    tr(`SLP 等级：${relation.grade}`, `SLP grade: ${relation.grade}`),
    tr(`相对作业强度：${formatAnalysisPercent(relation.intensity)}`, `Relative flow intensity: ${formatAnalysisPercent(relation.intensity)}`),
    tr(`设施对双向作业量：${relation.total}`, `Facility-pair bidirectional flow: ${relation.total}`),
  ] : [tr("暂无作业关系数据，请生成分析", "No flow data. Run analysis first.")])];
}
function crossingAnalysisText(id: string) {
  const target = props.document.crossings?.find((c) => c.id === id);
  const risk = analysisRisks.value.get(id);
  const probability = crossingProbability(risk);
  const load = crossingLoadRatio(risk);
  const status = risk?.status === "no-traffic" ? tr("无通行作业", "No passages")
    : risk?.status === "missing-sources" ? tr("缺少来源进路", "Missing source routes")
    : risk?.status === "missing-links" ? tr("来源进路已删除", "Source routes deleted")
    : risk?.status === "calculation-limit" ? tr("计算规模超限", "Calculation limit reached")
    : tr("待生成分析", "Run analysis first");
  const sourceNames = (target?.sourceLinks ?? []).map((s) =>
    props.document.links.find((l) => l.id === s.linkId)?.name ?? s.name);
  return [target?.name ?? id,
    load === undefined ? tr("负荷率：未计算 / 无数据", "Load: pending / no data")
      : tr(`负荷率 ρ：${formatAnalysisPercent(load)}`, `Load ρ: ${formatAnalysisPercent(load)}`),
    probability === undefined ? status : tr(`单次通行冲突概率：${formatAnalysisPercent(probability)}`, `Conflict per passage: ${formatAnalysisPercent(probability)}`),
    tr(`参与进路：${sourceNames.join(" / ") || "—"}`, `Source routes: ${sourceNames.join(" / ") || "—"}`),
  ];
}
function crossingAnalysisColor(id: string) {
  return loadColor(crossingLoadRatio(analysisRisks.value.get(id)));
}
const analysisTip = computed(() => !analysisHover.value ? [] : analysisHover.value.kind === "link"
  ? linkAnalysisText(analysisHover.value.id) : crossingAnalysisText(analysisHover.value.id));
function showAnalysisTip(event: MouseEvent | FocusEvent, kind: "link" | "crossing", id: string) {
  if (!props.analysisMode || dragging.value) return;
  const bounds = svg.value?.getBoundingClientRect();
  if (!bounds) return;
  const target = (event.currentTarget as Element).getBoundingClientRect();
  const x = "clientX" in event ? event.clientX : target.x + target.width / 2;
  const y = "clientY" in event ? event.clientY : target.y + target.height / 2;
  analysisHover.value = { kind, id,
    x: Math.max(8, Math.min(x - bounds.left + 14, bounds.width - 288)),
    y: Math.max(8, Math.min(y - bounds.top + 14, bounds.height - 128)),
  };
}
const tools = computed(() => [
  { id: "polygon", name: tr("多边形设施", "Polygon") },
  { id: "circle", name: tr("出入口", "Entrance / exit") },
  { id: "link", name: tr("作业进路", "Connect") },
]);
</script>
<template>
  <div
    class="canvas-workspace"
    :class="{ 'process-workspace': readOnly, 'analysis-workspace': analysisMode, 'fill-workspace': fillHeight }"
    :style="{ '--inspector-width': `${inspectorWidth}px` }"
    @keydown.esc.stop="exitDrawingMode"
  >
    <div class="canvas-shell">
      <div class="canvas-toolbar">
        <div
          v-if="!readOnly"
          class="drawing-tools"
          :aria-label="tr('添加图元', 'Drawing tools')"
        >
          <el-button
            v-for="item in tools"
            :key="item.id"
            size="small"
            :type="tool === item.id ? 'primary' : 'default'"
            :aria-pressed="tool === item.id"
            @click="activateTool(item.id)"
            >{{ item.name }}</el-button
          >
        </div>
        <el-button
          v-if="!readOnly"
          size="small"
          :disabled="drawing"
          :title="
            tr(
              '清空原有进路交叉，按当前作业进路重新生成；可撤销恢复',
              'Clear previous route crossings and regenerate from current links; undo to restore',
            )
          "
          @click="generateCrossings"
          >{{ tr("进路交叉", "Route crossings") }}</el-button
        >
        <ActionButton
          :label="tr('放大', 'Zoom in')"
          :icon="actionIcons.zoomIn"
          @click="zoom(0.8)"
        />
        <ActionButton
          :label="tr('缩小', 'Zoom out')"
          :icon="actionIcons.zoomOut"
          @click="zoom(1.25)"
        />
        <ActionButton
          :label="tr('全图', 'Fit canvas')"
          :icon="actionIcons.fit"
          @click="fitAll"
        />
        <ActionButton
          v-if="!readOnly"
          :label="tr('删除选中元素', 'Delete selection')"
          :icon="actionIcons.delete"
          :disabled="!selected"
          @click="remove"
        />
        <el-button v-if="polygon.length >= 3" size="small" @click="finish">{{
          tr("闭合多边形", "Close polygon")
        }}</el-button>
        <span
          v-if="!readOnly"
          class="mode-indicator"
          :class="{ 'is-drawing': drawing }"
        >
          {{
            drawing
              ? tr("绘图模式 · Esc 退出", "Drawing · Esc to exit")
              : tr("普通模式", "Normal mode")
          }}
        </span>
      </div>
      <div v-if="!readOnly" class="grid-toolbar">
        <span class="grid-toolbar-title">{{
          tr("捕捉与网格", "Snapping and grid")
        }}</span>
        <label>
          <span>{{ tr("显示网格", "Show grid") }}</span>
          <el-switch
            :model-value="grid.showGrid"
            size="small"
            :aria-label="tr('显示网格', 'Show grid')"
            @change="updateGrid({ showGrid: Boolean($event) })"
          />
        </label>
        <label>
          <span>{{ tr("网格追踪", "Grid snap") }}</span>
          <el-switch
            :model-value="grid.snapToGrid"
            size="small"
            :aria-label="tr('网格追踪', 'Grid snap')"
            @change="updateGrid({ snapToGrid: Boolean($event) })"
          />
        </label>
        <label>
          <span>{{ tr("网格间距", "Grid spacing") }}</span>
          <el-input-number
            :model-value="grid.spacing"
            :min="1"
            :max="500"
            :step="1"
            size="small"
            controls-position="right"
            :aria-label="tr('网格间距', 'Grid spacing')"
            @change="updateGridSpacing"
          />
        </label>
        <label>
          <span>{{ tr("显示作业进路名称", "Show link names") }}</span>
          <el-switch
            :model-value="showLinkNames"
            size="small"
            :aria-label="tr('显示作业进路名称', 'Show link names')"
            @change="updateLinkNameDisplay(Boolean($event))"
          />
        </label>
        <label>
          <span>{{ tr("曲线显示", "Rounded corners") }}</span>
          <el-switch
            :model-value="showCurveArc"
            size="small"
            :aria-label="tr('曲线显示', 'Rounded corners')"
            @change="updateCurveDisplay(Boolean($event))"
          />
          <span>{{
            showCurveArc ? tr("圆角曲线", "Rounded") : tr("折线", "Polyline")
          }}</span>
        </label>
        <label
          :title="
            tr(
              '仅对直角和钝角倒角，保留锐角与凹角',
              'Round right and obtuse corners; keep acute and reflex corners',
            )
          "
        >
          <span>{{ tr("多边形倒角", "Polygon fillets") }}</span>
          <el-switch
            :model-value="showPolygonFillets"
            size="small"
            :aria-label="tr('多边形倒角', 'Polygon fillets')"
            @change="updatePolygonFillets(Boolean($event))"
          />
        </label>
        <span
          class="grid-density-note"
          v-if="grid.showGrid && visibleGridSpacing > grid.spacing"
        >
          {{
            tr(
              "缩小视图已稀疏显示，吸附仍使用设定间距",
              "Grid dots are thinned at this zoom; snapping uses the configured spacing",
            )
          }}
        </span>
        <label>
          <span>{{ tr("显示进路交叉", "Show route crossings") }}</span>
          <el-switch
            :model-value="showCrossings"
            size="small"
            :aria-label="tr('显示进路交叉', 'Show route crossings')"
            @change="updateCrossingDisplay(Boolean($event))"
          />
        </label>
      </div>
      <div class="canvas-stage">
        <svg
          ref="svg"
          class="sketch-canvas"
          :class="{ 'is-drawing': drawing, 'is-panning': dragging && !drawing }"
          :viewBox="viewBox"
          tabindex="0"
          role="application"
          :aria-label="analysisMode ? tr('布置图分析画板', 'Layout analysis canvas') : tr('布置图画板', 'Layout drawing canvas')"
          @click.self="moved = false"
          @dblclick="addVertexOnEdge($event)"
          @keydown="keyboard"
          @pointermove="move"
          @pointerleave="leaveCanvas"
          @pointerup="stop"
          @pointercancel="stop"
          @wheel.prevent="zoom($event.deltaY > 0 ? 1.1 : 0.9, point($event))"
        >
          <defs>
            <pattern
              :id="gridPatternId"
              :width="visibleGridSpacing"
              :height="visibleGridSpacing"
              :x="grid.originX - visibleGridSpacing / 2"
              :y="grid.originY - visibleGridSpacing / 2"
              :viewBox="`0 0 ${visibleGridSpacing} ${visibleGridSpacing}`"
              patternUnits="userSpaceOnUse"
            >
              <circle
                :cx="visibleGridSpacing / 2"
                :cy="visibleGridSpacing / 2"
                :r="1 / displayScale"
                fill="#aab8c9"
              />
            </pattern>
          </defs>
          <rect
            data-canvas-background
            :x="visibleArea.x"
            :y="visibleArea.y"
            :width="visibleArea.w"
            :height="visibleArea.h"
            fill="white"
            @click="background"
            @pointerdown="
              (process || tool === 'select') && start($event, 'pan')
            "
          />
          <rect
            v-if="grid.showGrid"
            data-grid-overlay
            :x="visibleArea.x"
            :y="visibleArea.y"
            :width="visibleArea.w"
            :height="visibleArea.h"
            :fill="`url(#${gridPatternId})`"
            pointer-events="none"
          />
          <g v-for="{ link: l, path, middle } in renderedLinks" :key="l.id">
            <path
              :d="path"
              fill="none"
              :stroke="linkColor(l)"
              :stroke-width="analysisMode ? 4 : highlightedLinks.includes(l.id) ? 5 : 2"
              vector-effect="non-scaling-stroke"
              pointer-events="none"
            />
            <g
              v-if="middle"
              class="link-direction-arrow"
              :data-direction="l.direction ?? 'both'"
              :transform="`translate(${middle.x} ${middle.y}) rotate(${middle.angle + (l.direction === 'reverse' ? 180 : 0)}) scale(${Math.min(1 / displayScale, middle.length / 28)})`"
              :fill="linkColor(l)"
              pointer-events="none"
              aria-hidden="true"
            >
              <path
                :d="
                  (l.direction ?? 'both') === 'both'
                    ? 'M 11 -5 L 3 0 L 11 5 Z M -11 -5 L -3 0 L -11 5 Z'
                    : 'M -4 -5 L 6 0 L -4 5 Z'
                "
              />
            </g>
            <path
              :d="path"
              fill="none"
              stroke="transparent"
              stroke-width="16"
              vector-effect="non-scaling-stroke"
              class="link-hit-target"
              :role="analysisMode ? 'img' : 'button'"
              :tabindex="process && !analysisMode ? -1 : 0"
              :aria-label="analysisMode ? linkAnalysisText(l.id).join('；') : l.name"
              @pointerenter="showAnalysisTip($event, 'link', l.id)"
              @pointermove="showAnalysisTip($event, 'link', l.id)"
              @pointerleave="analysisHover = undefined"
              @focus="showAnalysisTip($event, 'link', l.id)"
              @blur="analysisHover = undefined"
              @keydown.enter.stop.prevent="!readOnly && (selected = l.id)"
              @click.stop="selectLink($event, l.id)"
              @dblclick.stop.prevent="doubleClickLink($event, l.id)"
            />
            <text
              v-if="
                (showLinkNames || selected === l.id ||
                  (process && highlightedLinks.includes(l.id))) &&
                middle
              "
              class="link-label"
              :x="middle.x"
              :y="middle.y + (process ? 22 : -12) / displayScale"
              text-anchor="middle"
              :font-size="process ? 12 / displayScale : 24"
              :font-weight="selected === l.id || (process && highlightedLinks.includes(l.id)) ? 700 : 400"
              fill="#334155"
              stroke="white"
              stroke-width="4"
              paint-order="stroke"
              :pointer-events="readOnly ? 'none' : 'auto'"
              :visibility="rename?.id === l.id ? 'hidden' : undefined"
              @pointerdown.stop
              @click.stop="selectLink($event, l.id)"
              @dblclick.stop="beginRename(l.id)"
            >
              <template v-if="process && highlightedLinks.includes(l.id)">
                {{ tr("步骤", "Step") }}
                {{
                  highlightedLinks
                    .map((id, i) => (id === l.id ? (i + 1) * 2 : null))
                    .filter(Boolean)
                    .join(" · ")
                }}
                ·
              </template>
              {{ l.name }}
            </text>
          </g>
          <g
            v-for="f in document.facilities"
            :key="f.id"
            class="facility"
            :role="analysisMode ? 'img' : 'button'"
            :tabindex="analysisMode ? -1 : 0"
            :aria-label="f.name"
            :aria-pressed="analysisMode ? undefined : selected === f.id"
            @keydown.enter.prevent="
              !analysisMode && (process ? emit('pick', f.id) : (selected = f.id))
            "
            :class="{ 'is-picked': highlighted.includes(f.id) }"
            @pointerdown.stop="start($event, 'facility', f.id)"
            @click.stop="selectFacility($event, f.id)"
            @dblclick.stop="doubleClickFacility($event, f.id)"
          >
            <path
              v-if="f.kind === 'polygon'"
              :d="polygonPaths.get(f.id)"
              :fill="polygonFill(f)"
              :stroke="polygonStroke(f)"
              stroke-width="2"
              vector-effect="non-scaling-stroke"
            />
            <circle
              v-else
              :cx="f.center.x"
              :cy="f.center.y"
              :r="f.radius"
              fill="#f5ecdc"
              :stroke="selected === f.id ? '#cc9452' : '#b2996a'"
              stroke-width="2"
              vector-effect="non-scaling-stroke"
            />
            <path
              v-if="f.kind === 'polygon' && !readOnly && tool === 'select'"
              :d="polygonPaths.get(f.id)"
              fill="none"
              stroke="transparent"
              stroke-width="16"
              vector-effect="non-scaling-stroke"
              pointer-events="stroke"
              class="polygon-edge-target"
            />
            <text
              :visibility="rename?.id === f.id ? 'hidden' : undefined"
              @dblclick.stop="beginRename(f.id)"
              :x="center(f).x"
              :y="center(f).y + (f.kind === 'circle' ? f.radius + (analysisMode ? 16 / displayScale : 28) : analysisMode ? 4 / displayScale : 5)"
              text-anchor="middle"
              :font-size="analysisMode ? 12 / displayScale : 24"
              :font-weight="selected === f.id ? 700 : 400"
              :fill="
                f.kind === 'polygon'
                  ? polygonTextColor(polygonFill(f))
                  : '#334155'
              "
            >
              {{ f.name }}
            </text>
            <text
              v-if="highlighted.includes(f.id)"
              :x="center(f).x"
              :y="center(f).y - 25"
              text-anchor="middle"
              font-size="20"
              fill="#38765d"
            >
              {{
                (process ? tr("步骤 ", "Step ") : "") +
                highlighted
                  .map((id, i) =>
                    id === f.id ? (process ? i * 2 + 1 : i + 1) : null,
                  )
                  .filter(Boolean)
                  .join(" · ")
              }}
            </text>
          </g>
          <g
            v-if="
              !readOnly && (tool === 'link' || (tool === 'select' && facility))
            "
          >
            <template
              v-for="f in document.facilities.filter(
                (f) => tool === 'link' || f.id === selected,
              )"
              :key="f.id"
            >
              <circle
                v-for="p in ports(f).filter(
                  (p) => tool === 'link' || p.kind !== 'midpoint',
                )"
                :key="`${p.kind}-${p.index}`"
                :cx="portPoint(p, document.facilities).x"
                :cy="portPoint(p, document.facilities).y"
                :r="(p.kind === 'midpoint' ? 4 : 5) / displayScale"
                :fill="p.kind === 'midpoint' ? '#a7c2df' : 'white'"
                stroke="#4776a8"
                stroke-width="2"
                vector-effect="non-scaling-stroke"
                class="port"
                :class="{ 'edit-handle': tool === 'select' }"
                :pointer-events="tool === 'select' && p.kind === 'center' ? 'none' : undefined"
                @click.stop="pickPort(p)"
                @dblclick.stop
                @contextmenu.stop.prevent="
                  p.kind === 'vertex' &&
                  openPointMenu($event, 'vertex', f.id, p.index)
                "
                @pointerdown.stop="
                  p.kind === 'vertex'
                    ? start($event, 'vertex', f.id, p.index)
                    : p.kind === 'center' && start($event, 'facility', f.id)
                "
              />
            </template>
          </g>
          <g v-if="showCrossings" class="crossing-layer">
            <g
              v-for="c in document.crossings ?? []"
              :key="c.id"
              class="route-crossing"
              :role="analysisMode ? 'img' : process ? undefined : 'button'"
              :tabindex="process && !analysisMode ? -1 : 0"
              :aria-label="analysisMode ? crossingAnalysisText(c.id).join('；') : c.name"
              @pointerenter="showAnalysisTip($event, 'crossing', c.id)"
              @pointermove="showAnalysisTip($event, 'crossing', c.id)"
              @pointerleave="analysisHover = undefined"
              @focus="showAnalysisTip($event, 'crossing', c.id)"
              @blur="analysisHover = undefined"
              :aria-pressed="analysisMode ? undefined : selected === c.id"
              :pointer-events="(process && !analysisMode) || drawing ? 'none' : undefined"
              @keydown.enter.stop.prevent="
                !readOnly && !drawing && (selected = c.id)
              "
              @pointerdown.stop="start($event, 'crossing', c.id)"
              @click.stop="selectLink($event, c.id)"
              @dblclick.stop="beginRename(c.id)"
            >
              <circle
                :cx="c.center.x"
                :cy="c.center.y"
                :r="c.radius"
                :fill="analysisMode ? crossingAnalysisColor(c.id) : 'transparent'"
                :fill-opacity="analysisMode ? 0.32 : 1"
                :stroke="analysisMode ? crossingAnalysisColor(c.id) : selected === c.id ? '#991b1b' : '#dc2626'"
                :stroke-width="selected === c.id ? 3 : 2"
                vector-effect="non-scaling-stroke"
              />
              <text
                :visibility="rename?.id === c.id ? 'hidden' : undefined"
                :x="c.center.x"
                :y="c.center.y - c.radius - 6 / displayScale"
                text-anchor="middle"
                :font-size="12 / displayScale"
                :font-weight="selected === c.id ? 700 : 400"
                fill="#b91c1c"
                stroke="white"
                stroke-width="3"
                paint-order="stroke"
                vector-effect="non-scaling-stroke"
                :pointer-events="readOnly ? 'none' : 'auto'"
              >
                {{ c.name }}
              </text>
            </g>
          </g>
          <g v-if="!readOnly && link && tool === 'select'">
            <circle
              v-for="(p, i) in link.anchors"
              :key="i"
              :cx="p.x"
              :cy="p.y"
              :r="5 / displayScale"
              fill="#fff4dc"
              stroke="#bc9348"
              stroke-width="2"
              vector-effect="non-scaling-stroke"
              class="edit-handle"
              @pointerdown.stop="start($event, 'anchor', link.id, i)"
              @contextmenu.stop.prevent="
                openPointMenu($event, 'anchor', link.id, i)
              "
              @dblclick.stop.prevent="doubleClickLink($event, link.id)"
            />
          </g>
          <polyline
            v-if="polygon.length"
            :points="[...polygon, cursor].map((p) => `${p.x},${p.y}`).join(' ')"
            fill="#e7f0fa88"
            stroke="#4776a8"
            stroke-width="3"
            stroke-dasharray="8 6"
            pointer-events="none"
          />
          <circle
            v-if="polygon[0]"
            :cx="polygon[0].x"
            :cy="polygon[0].y"
            r="12"
            fill="white"
            stroke="#4776a8"
            stroke-width="3"
            @click.stop="grid.snapToGrid ? background($event) : finish()"
          />
          <path
            v-if="source"
            :d="
              curvePath(
                [portPoint(source, document.facilities), ...anchors, cursor],
                showCurveArc,
              )
            "
            fill="none"
            stroke="#bc9348"
            stroke-width="3"
            stroke-dasharray="8 6"
            pointer-events="none"
          />
          <circle
            v-if="showSnapCursor"
            class="grid-snap-cursor"
            :cx="cursor.x"
            :cy="cursor.y"
            :r="4 / displayScale"
            fill="none"
            stroke="#4776a8"
            stroke-width="1.5"
            vector-effect="non-scaling-stroke"
            pointer-events="none"
          />
          <foreignObject
            v-if="rename && renameTarget && renameBox"
            :x="renameBox.x"
            :y="renameBox.y"
            :width="renameBox.width"
            :height="renameBox.height"
            @pointerdown.stop @click.stop @dblclick.stop @wheel.stop @keydown.stop
          >
            <input
              ref="renameInput"
              v-model="rename.draft"
              class="inline-name-input"
              :aria-label="tr('图元名称', 'Object name')"
              maxlength="120"
              :style="{
                fontSize: `${14 / displayScale}px`,
                padding: `${4 / displayScale}px ${6 / displayScale}px`,
                borderWidth: `${1 / displayScale}px`,
                borderRadius: `${4 / displayScale}px`,
              }"
              @keydown.enter.stop.prevent="!$event.isComposing && commitRename()"
              @keydown.esc.stop.prevent="cancelRename()"
              @blur="commitRename(true)"
            />
          </foreignObject>
        </svg>
        <div v-if="analysisMode && analysisHover" class="analysis-tooltip" role="tooltip"
          :style="{ left: `${analysisHover.x}px`, top: `${analysisHover.y}px` }">
          <strong>{{ analysisTip[0] }}</strong>
          <div v-for="(line, index) in analysisTip.slice(1)" :key="index">{{ line }}</div>
        </div>
      </div>
      <div class="canvas-status">
        <span>{{ hint }}</span
        ><span v-if="!readOnly" class="grid-status">
          {{ tr("网格", "Grid") }} {{ grid.spacing }} ·
          {{
            grid.snapToGrid
              ? tr("追踪开启", "Snap on")
              : tr("自由绘制", "Free drawing")
          }}
          <span v-if="cursorVisible">
            · X {{ cursor.x.toFixed(1) }} / Y {{ cursor.y.toFixed(1) }}</span
          > </span
        ><span
          >{{ tr("无限画布", "Infinite canvas") }} · {{ document.facilities.length }}
          {{ tr("设施", "facilities") }} / {{ document.links.length }}
          {{ tr("进路", "links") }}</span
        >
      </div>
    </div>
    <template v-if="!readOnly">
      <PaneDivider
        v-model="inspectorWidth"
        reverse
        :min="260"
        :max="520"
        :reset-value="320"
        :label="tr('调整属性面板宽度', 'Resize properties panel')"
      />
      <div class="canvas-inspector">
        <SketchObjectProperties
          v-if="facility || link || crossing"
          :key="selected"
          class="canvas-object-properties"
          :facility="facility"
          :link="link"
          :crossing="crossing"
          :links="document.links"
          :facilities="document.facilities"
          :plans="document.plans"
          @change="emit('change')"
          @close="selected = ''"
          @remove="remove"
        />
        <aside
          v-else
          class="properties-empty"
          :aria-label="tr('对象属性', 'Object properties')"
        >
          <h3>{{ tr("对象属性", "Object properties") }}</h3>
          <p>
            {{
              tr(
                "点击图中的设施、出入口、作业进路或进路交叉标记，设置对应属性。",
                "Select a facility, gate, link or route crossing marker to edit its properties.",
              )
            }}
          </p>
        </aside>
      </div>
    </template>
  </div>
  <Teleport to="body">
    <div
      v-if="pointMenu"
      ref="pointMenuElement"
      class="point-context-menu"
      role="menu"
      :aria-label="tr('编辑点菜单', 'Edit point menu')"
      :style="{ left: `${pointMenu.x}px`, top: `${pointMenu.y}px` }"
      @contextmenu.prevent
      @keydown.esc.stop="closePointMenu"
    >
      <button
        ref="pointMenuButton"
        type="button"
        role="menuitem"
        @click="deletePointFromMenu"
      >
        {{
          pointMenu.kind === "vertex"
            ? tr("删除顶点", "Delete vertex")
            : tr("删除锚点", "Delete anchor")
        }}
      </button>
    </div>
  </Teleport>
</template>
<style scoped>
.point-context-menu {
  position: fixed;
  z-index: 3000;
  min-width: 148px;
  padding: 4px;
  background: white;
  border: 1px solid var(--sy-border, #dde5ef);
  border-radius: 6px;
  box-shadow: 0 4px 18px #263b5326;
}
.point-context-menu button {
  display: block;
  width: 100%;
  border: 0;
  background: none;
  padding: 8px 12px;
  border-radius: 4px;
  text-align: left;
  font: inherit;
  font-size: 13px;
  color: #c45656;
  cursor: pointer;
}
.point-context-menu button:hover,
.point-context-menu button:focus-visible {
  background: #fef0f0;
  outline: none;
}
.canvas-stage { position: relative; }
.inline-name-input { box-sizing: border-box; width: 100%; height: 100%; text-align: center; color: #334155; background: white; border-style: solid; border-color: #4776a8; outline: none; }
.analysis-tooltip {
  position: absolute;
  z-index: 3;
  width: 256px;
  max-width: calc(100% - 40px);
  pointer-events: none;
  padding: 10px 12px;
  background: #ffffffed;
  border: 1px solid var(--sy-border);
  border-radius: 6px;
  box-shadow: 0 4px 18px #263b5326;
  color: #334155;
  font-size: 12px;
  line-height: 1.8;
  overflow-wrap: anywhere;
}
.analysis-tooltip strong { display: block; }
.analysis-workspace .sketch-canvas { height: clamp(360px, 52vh, 720px); }
.analysis-workspace .route-crossing { outline: none; }
.analysis-workspace .route-crossing:focus-visible circle { stroke-width: 3; }
.analysis-workspace .link-hit-target:focus-visible { stroke: transparent; }
.canvas-workspace {
  display: grid;
  grid-template-columns: minmax(320px, 1fr) 8px var(--inspector-width);
  align-items: stretch;
  min-width: 0;
  overflow-x: auto;
}
.canvas-workspace.process-workspace {
  grid-template-columns: minmax(0, 1fr);
}
.fill-workspace {
  flex: 1;
  min-height: 0;
  grid-template-rows: minmax(0, 1fr);
}
.fill-workspace .canvas-shell {
  display: flex;
  flex-direction: column;
  min-height: 0;
}
.fill-workspace .canvas-toolbar,
.fill-workspace .grid-toolbar,
.fill-workspace .canvas-status { flex-shrink: 0; }
.fill-workspace .canvas-stage { flex: 1; min-height: 0; }
.fill-workspace .sketch-canvas { height: 100%; }
.canvas-inspector {
  display: flex;
  min-width: 0;
  min-height: 0;
  contain: size;
}
.canvas-inspector .canvas-object-properties,
.properties-empty {
  flex: 1;
  min-width: 0;
  min-height: 0;
  box-shadow: none;
}
.properties-empty {
  border: 1px solid var(--sy-border);
  border-radius: 6px;
  background: white;
}
.properties-empty h3 {
  margin: 0;
  padding: 14px;
  border-bottom: 1px solid var(--sy-border);
  font-size: 14px;
}
.properties-empty p {
  padding: 6px 14px;
  color: var(--sy-text-muted);
  font-size: 12px;
  line-height: 1.8;
}
.drawing-tools {
  display: flex;
  gap: 6px;
  flex-wrap: wrap;
}
.drawing-tools .el-button {
  margin: 0;
}
.mode-indicator {
  margin-left: auto;
  font-size: 12px;
  color: var(--sy-text-muted);
}
.mode-indicator.is-drawing {
  color: #4776a8;
}
.canvas-shell {
  border: 1px solid var(--sy-border);
  border-radius: 6px;
  overflow: hidden;
  background: white;
  min-width: 0;
}
.canvas-toolbar {
  padding: 8px;
  display: flex;
  gap: 6px;
  align-items: center;
  flex-wrap: wrap;
  border-bottom: 1px solid var(--sy-border);
}
.grid-toolbar {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 10px 20px;
  padding: 8px 12px;
  border-bottom: 1px solid var(--sy-border);
  background: #fafbfc;
  font-size: 12px;
}
.grid-toolbar label {
  display: inline-flex;
  align-items: center;
  gap: 8px;
}
.grid-toolbar .el-input-number {
  width: 110px;
}
.grid-toolbar-title,
.grid-density-note {
  color: var(--sy-text-muted);
}
.grid-status {
  font-variant-numeric: tabular-nums;
}
.sketch-canvas {
  display: block;
  width: 100%;
  height: clamp(420px, 65vh, 900px);
  touch-action: none;
  user-select: none;
  outline: none;
  cursor: grab;
}
.sketch-canvas.is-drawing {
  cursor: crosshair;
}
.sketch-canvas.is-panning {
  cursor: grabbing;
}
.sketch-canvas:focus-visible {
  outline: 2px solid #4776a8;
  outline-offset: -2px;
}
.facility,
.link-hit-target,
.port {
  cursor: pointer;
}
.edit-handle {
  cursor: move;
}
.facility,
.link-hit-target {
  outline: none;
}
.facility:focus-visible text {
  font-weight: 700;
}
.link-hit-target:focus-visible {
  stroke: #cf9250;
  stroke-width: 2;
}
.canvas-status {
  display: flex;
  flex-wrap: wrap;
  justify-content: space-between;
  gap: 12px;
  padding: 8px 10px;
  color: var(--sy-text-muted);
  background: #fafbfc;
  border-top: 1px solid var(--sy-border);
  font-size: 12px;
}
.canvas-status span:last-child {
  white-space: nowrap;
}
</style>
