<script setup lang="ts">
import { computed, ref, watch } from "vue";
import { ElMessage } from "element-plus";
import { useI18n } from "vue-i18n";
import ActionButton from "@/components/ui/ActionButton.vue";
import { actionIcons } from "@/components/ui/actionIcons";
import { clone } from "./types";
import type {
  Facility,
  LogisticsLink,
  LinkDirection,
  Point,
  Port,
  ProcessPlan,
  RouteCrossing,
} from "./types";
import { canTraverse, portPoint, simplePolygon } from "./geometry";
import {
  DEFAULT_FILL_COLOR,
  DEFAULT_STROKE_COLOR,
  DEFAULT_LINK_COLOR,
  PRESET_COLORS,
  normalizeColor,
} from "./colors";

const props = defineProps<{
  facility?: Facility;
  link?: LogisticsLink;
  crossing?: RouteCrossing;
  links?: LogisticsLink[];
  facilities: Facility[];
  plans?: ProcessPlan[];
}>();
const emit = defineEmits<{ change: []; close: []; remove: [] }>();
const { locale } = useI18n();
const tr = (zh: string, en: string) => (locale.value === "zh" ? zh : en);
const object = computed(() => props.facility ?? props.link ?? props.crossing);
const name = ref("");
const draftFacility = ref<Facility>();
const draftLink = ref<LogisticsLink>();
const draftCrossing = ref<RouteCrossing>();
const crossingSources = computed(() =>
  (props.crossing?.sourceLinks ?? []).map((source) => {
    const current = props.links?.find((link) => link.id === source.linkId);
    return {
      ...source,
      currentName: current?.name ?? source.name,
      deleted: !current,
      renamed: !!current && current.name !== source.name,
    };
  }),
);
type ColorField = "fillColor" | "strokeColor" | "color";
const colorDefaults = {
  fillColor: DEFAULT_FILL_COLOR,
  strokeColor: DEFAULT_STROKE_COLOR,
  color: DEFAULT_LINK_COLOR,
};
const draftColors = ref<Record<ColorField, string | null>>({
  ...colorDefaults,
});
const colorFields = computed<{ key: ColorField; label: string }[]>(() =>
  props.facility?.kind === "polygon"
    ? [
        { key: "fillColor", label: tr("填充颜色", "Fill color") },
        { key: "strokeColor", label: tr("边框颜色", "Border color") },
      ]
    : props.link
      ? [{ key: "color", label: tr("线条与箭头颜色", "Line and arrow color") }]
      : [],
);
const objectType = computed(() =>
  props.facility
    ? props.facility.kind === "polygon"
      ? tr("多边形设施", "Polygon facility")
      : tr("出入口", "Entrance / exit")
    : props.crossing
      ? tr("进路交叉", "Route crossing")
      : tr("作业进路", "Logistics link"),
);

function resetDraft() {
  name.value = object.value?.name ?? "";
  draftFacility.value = props.facility ? clone(props.facility) : undefined;
  draftLink.value = props.link ? clone(props.link) : undefined;
  draftCrossing.value = props.crossing ? clone(props.crossing) : undefined;
  if (draftLink.value) draftLink.value.direction ??= "both";
  draftColors.value = {
    fillColor: normalizeColor(props.facility?.fillColor) ?? DEFAULT_FILL_COLOR,
    strokeColor:
      normalizeColor(props.facility?.strokeColor) ?? DEFAULT_STROKE_COLOR,
    color: normalizeColor(props.link?.color) ?? DEFAULT_LINK_COLOR,
  };
}
watch(object, resetDraft, { deep: true, immediate: true });

function reject(message: string) {
  resetDraft();
  ElMessage.warning(message);
}
function applyName() {
  const value = name.value.trim();
  if (!value || value.length > 120) {
    reject(
      tr(
        "名称不能为空，且最多 120 字符。",
        "Enter a name of 1–120 characters.",
      ),
    );
    return;
  }
  if (object.value && object.value.name !== value) {
    object.value.name = value;
    emit("change");
  }
  name.value = value;
}
function applyColor(key: ColorField, value: string | null) {
  const target =
    key === "color"
      ? props.link
      : props.facility?.kind === "polygon"
        ? props.facility
        : undefined;
  if (!target) return;
  const color =
    value == null || value.trim() === "" ? null : normalizeColor(value);
  if (color === undefined) {
    reject(
      tr(
        "请输入有效的十六进制颜色，例如 #2563eb。",
        "Enter a valid hex color, such as #2563eb.",
      ),
    );
    return;
  }
  const previous = key === "color" ? props.link?.color : props.facility?.[key];
  draftColors.value[key] = color ?? colorDefaults[key];
  if ((normalizeColor(previous) ?? null) === color) return;
  if (key === "color") props.link!.color = color;
  else props.facility![key] = color;
  emit("change");
}
function validPoint(p: Point) {
  return Number.isFinite(p.x) && Number.isFinite(p.y);
}
function applyFacility() {
  const f = draftFacility.value;
  const target = props.facility;
  if (!f || !target) return;
  if (f.kind === "polygon") {
    if (!f.vertices.every(validPoint) || !simplePolygon(f.vertices)) {
      reject(
        tr(
          "顶点坐标必须为有限数值，且不能使多边形自交、重叠或面积为零。",
          "Vertex coordinates must be finite and form a nonzero polygon without intersections.",
        ),
      );
      return;
    }
    if (JSON.stringify(f.vertices) === JSON.stringify(target.vertices)) return;
    // Keep the vertex order and count so connected ports retain their identities.
    target.vertices = clone(f.vertices);
  } else {
    if (
      !validPoint(f.center) ||
      !Number.isFinite(f.radius) ||
      f.radius < 5 ||
      f.radius > 200
    ) {
      reject(
        tr(
          "圆心坐标必须为有限数值，半径范围为 5–200。",
          "Center coordinates must be finite, with a radius of 5–200.",
        ),
      );
      return;
    }
    if (
      f.center.x === target.center.x &&
      f.center.y === target.center.y &&
      f.radius === target.radius
    )
      return;
    target.center = clone(f.center);
    target.radius = f.radius;
  }
  emit("change");
}
function applyCrossing() {
  const value = draftCrossing.value,
    target = props.crossing;
  if (!value || !target) return;
  if (
    !validPoint(value.center) ||
    !Number.isFinite(value.radius) ||
    value.radius < 5 ||
    value.radius > 200
  ) {
    reject(
      tr(
        "标记中心坐标必须为有限数值，半径范围为 5–200。",
        "Marker center coordinates must be finite, with a radius of 5–200.",
      ),
    );
    return;
  }
  if (
    value.center.x === target.center.x &&
    value.center.y === target.center.y &&
    value.radius === target.radius
  )
    return;
  target.center = clone(value.center);
  target.radius = value.radius;
  emit("change");
}
function applyAnchors() {
  const l = draftLink.value;
  if (!l || !props.link) return;
  if (l.anchors.length > 24 || !l.anchors.every(validPoint)) {
    reject(
      tr(
        "最多设置 24 个锚点，坐标必须为有限数值。",
        "Use up to 24 anchors with finite coordinates.",
      ),
    );
    return;
  }
  if (JSON.stringify(l.anchors) === JSON.stringify(props.link.anchors)) return;
  props.link.anchors = clone(l.anchors);
  emit("change");
}
function applyDirection(direction: LinkDirection) {
  const target = props.link;
  if (!target || !["forward", "reverse", "both"].includes(direction)) return;
  if ((target.direction ?? "both") === direction) return;
  const candidate = { ...target, direction };
  const incompatible = props.plans?.some((plan) =>
    plan.chains.some((chain) =>
      chain.steps.some(
        (step, i) =>
          i > 0 &&
          step.incomingLinkId === target.id &&
          !canTraverse(
            candidate,
            chain.steps[i - 1]!.facilityId,
            step.facilityId,
          ),
      ),
    ),
  );
  if (incompatible) {
    reject(
      tr(
        "已有作业流程沿相反方向使用此连接，请先调整相应流程。",
        "An existing process uses this link in the opposite direction. Adjust that process first.",
      ),
    );
    return;
  }
  target.direction = direction;
  emit("change");
}
function addAnchor() {
  const l = draftLink.value;
  if (!l || l.anchors.length >= 24) return;
  const from =
    l.anchors[l.anchors.length - 1] ?? portPoint(l.from, props.facilities);
  const to = portPoint(l.to, props.facilities);
  l.anchors.push({ x: (from.x + to.x) / 2, y: (from.y + to.y) / 2 });
  applyAnchors();
}
function removeAnchor(index: number) {
  draftLink.value?.anchors.splice(index, 1);
  applyAnchors();
}
function straighten() {
  if (!draftLink.value) return;
  draftLink.value.anchors = [];
  applyAnchors();
}
function portLabel(port: Port) {
  const f = props.facilities.find((f) => f.id === port.facilityId);
  const location =
    port.kind === "center"
      ? tr("圆心", "Center")
      : port.kind === "vertex"
        ? `${tr("顶点", "Vertex")} ${port.index + 1}`
        : `${tr("边中点", "Edge midpoint")} ${port.index + 1}`;
  return `${f?.name ?? "—"} · ${location}`;
}
</script>

<template>
  <aside
    class="object-properties"
    :aria-label="tr('对象属性', 'Object properties')"
  >
    <header class="properties-header">
      <div>
        <h3>{{ tr("对象属性", "Object properties") }}</h3>
        <span>{{ objectType }}</span>
      </div>
      <ActionButton
        :icon="actionIcons.close"
        :label="tr('关闭对象属性', 'Close object properties')"
        @click="emit('close')"
      />
    </header>
    <div class="properties-body">
      <el-form label-position="top" size="small" @submit.prevent>
        <el-form-item :label="tr('名称', 'Name')">
          <el-input
            v-model="name"
            :aria-label="tr('对象名称', 'Object name')"
            maxlength="120"
            @change="applyName"
          />
        </el-form-item>
        <el-form-item
          v-for="field in colorFields"
          :key="field.key"
          :label="field.label"
        >
          <div class="color-setting">
            <el-color-picker
              v-model="draftColors[field.key]"
              color-format="hex"
              :predefine="PRESET_COLORS"
              :aria-label="`${field.label}${tr('选择器', ' picker')}`"
              @change="applyColor(field.key, $event)"
            />
            <el-input
              v-model="draftColors[field.key]"
              maxlength="7"
              :aria-label="`${field.label}${tr('色值', ' hex value')}`"
              @change="applyColor(field.key, $event)"
            />
            <el-button
              text
              size="small"
              :aria-label="`${tr('恢复默认', 'Reset ')}${field.label}`"
              @click="applyColor(field.key, null)"
              >{{ tr("默认", "Reset") }}</el-button
            >
          </div>
        </el-form-item>
        <template v-if="draftCrossing">
          <div class="section-heading">
            {{ tr("参与交叉的进路", "Source routes")
            }}<span>{{ crossingSources.length }}</span>
          </div>
          <ul v-if="crossingSources.length" class="crossing-sources">
            <li v-for="source in crossingSources" :key="source.linkId">
              <div>
                {{ source.currentName
                }}<span v-if="source.deleted">{{
                  tr("（已删除）", " (deleted)")
                }}</span>
              </div>
              <small v-if="source.renamed"
                >{{ tr("生成时：", "Recorded as: ") }}{{ source.name }}</small
              >
            </li>
          </ul>
          <p v-else class="properties-hint">
            {{
              tr(
                "旧标记未记录参与进路。点击“进路交叉”可为仍匹配当前交点的标记补全来源。",
                "This older marker has no source routes recorded. Click Route crossings to fill them in if its position still matches a current intersection.",
              )
            }}
          </p>
          <div class="coordinate-pair">
            <el-form-item :label="tr('中心 X', 'Center X')"
              ><el-input-number
                v-model="draftCrossing.center.x"
                :aria-label="tr('交叉中心 X', 'Crossing center X')"
                controls-position="right"
                @change="applyCrossing"
            /></el-form-item>
            <el-form-item :label="tr('中心 Y', 'Center Y')"
              ><el-input-number
                v-model="draftCrossing.center.y"
                :aria-label="tr('交叉中心 Y', 'Crossing center Y')"
                controls-position="right"
                @change="applyCrossing"
            /></el-form-item>
          </div>
          <el-form-item :label="tr('红圈半径', 'Marker radius')"
            ><el-input-number
              v-model="draftCrossing.radius"
              :min="5"
              :max="200"
              :aria-label="tr('红圈半径', 'Marker radius')"
              controls-position="right"
              @change="applyCrossing"
          /></el-form-item>
          <p class="properties-hint">
            {{
              tr(
                "这是点击“进路交叉”时生成的独立标记，不会随进路线自动更新。再次生成可补充新交点或旧标记来源；不需要的标记可单独删除。",
                "This independent marker is a snapshot generated by Route crossings. Link edits do not update it. Generate again to add new intersections or fill missing source routes; remove unwanted markers individually.",
              )
            }}
          </p>
        </template>
        <template v-if="draftFacility?.kind === 'circle'">
          <div class="coordinate-pair">
            <el-form-item :label="tr('圆心 X', 'Center X')"
              ><el-input-number
                v-model="draftFacility.center.x"
                :aria-label="tr('圆心 X', 'Center X')"
                controls-position="right"
                @change="applyFacility"
            /></el-form-item>
            <el-form-item :label="tr('圆心 Y', 'Center Y')"
              ><el-input-number
                v-model="draftFacility.center.y"
                :aria-label="tr('圆心 Y', 'Center Y')"
                controls-position="right"
                @change="applyFacility"
            /></el-form-item>
          </div>
          <el-form-item :label="tr('半径', 'Radius')"
            ><el-input-number
              v-model="draftFacility.radius"
              :min="5"
              :max="200"
              :aria-label="tr('半径', 'Radius')"
              controls-position="right"
              @change="applyFacility"
          /></el-form-item>
        </template>
        <template v-if="draftFacility?.kind === 'polygon'">
          <div class="section-heading">
            {{ tr("顶点坐标", "Vertex coordinates")
            }}<span>{{ draftFacility.vertices.length }}</span>
          </div>
          <div
            v-for="(p, i) in draftFacility.vertices"
            :key="i"
            class="point-row"
          >
            <span class="point-index">{{ i + 1 }}</span>
            <el-input-number
              v-model="p.x"
              :aria-label="`${tr('顶点', 'Vertex')} ${i + 1} X`"
              controls-position="right"
              @change="applyFacility"
            />
            <el-input-number
              v-model="p.y"
              :aria-label="`${tr('顶点', 'Vertex')} ${i + 1} Y`"
              controls-position="right"
              @change="applyFacility"
            />
          </div>
          <p class="properties-hint">
            {{
              tr(
                "左列为 X，右列为 Y；顶点按绘制顺序排列。",
                "X on the left, Y on the right; vertices follow drawing order.",
              )
            }}
          </p>
        </template>
        <template v-if="draftLink">
          <dl class="connection-info">
            <dt>{{ tr("起点", "From") }}</dt>
            <dd>{{ portLabel(draftLink.from) }}</dd>
            <dt>{{ tr("终点", "To") }}</dt>
            <dd>{{ portLabel(draftLink.to) }}</dd>
          </dl>
          <el-form-item :label="tr('连接方向', 'Link direction')">
            <el-select
              v-model="draftLink.direction"
              :aria-label="tr('连接方向', 'Link direction')"
              @change="applyDirection"
            >
              <el-option
                value="forward"
                :label="tr('单向：起点 → 终点', 'One way: From → To')"
              />
              <el-option
                value="reverse"
                :label="tr('单向：终点 → 起点', 'One way: To → From')"
              />
              <el-option
                value="both"
                :label="tr('双向：起点 ↔ 终点', 'Both ways: From ↔ To')"
              />
            </el-select>
          </el-form-item>
          <div class="section-heading">
            {{ tr("锚点（折线拐点）", "Anchors (polyline corners)")
            }}<span>{{ draftLink.anchors.length }} / 24</span>
          </div>
          <div
            v-for="(p, i) in draftLink.anchors"
            :key="i"
            class="point-row anchor-row"
          >
            <span class="point-index">{{ i + 1 }}</span>
            <el-input-number
              v-model="p.x"
              :aria-label="`${tr('锚点', 'Anchor')} ${i + 1} X`"
              controls-position="right"
              @change="applyAnchors"
            />
            <el-input-number
              v-model="p.y"
              :aria-label="`${tr('锚点', 'Anchor')} ${i + 1} Y`"
              controls-position="right"
              @change="applyAnchors"
            />
            <ActionButton
              :icon="actionIcons.delete"
              :label="`${tr('删除锚点', 'Remove anchor')} ${i + 1}`"
              @click="removeAnchor(i)"
            />
          </div>
          <p class="properties-hint">
            {{
              draftLink.anchors.length
                ? tr(
                    "左列为 X，右列为 Y。锚点确定折线拐点；开启画板中的“曲线显示”后，拐角以相切圆弧过渡。",
                    "X on the left, Y on the right. Anchors define polyline corners. Turn on Rounded corners in the canvas toolbar for tangent circular arcs.",
                  )
                : tr(
                    "当前为直线，添加锚点可设置拐点。画板中的“曲线显示”开关可切换折线与圆角曲线。",
                    "This link is straight. Add anchors for corners, then use Rounded corners in the canvas toolbar to switch between polylines and circular arcs.",
                  )
            }}
          </p>
          <div class="anchor-actions">
            <el-button
              size="small"
              :disabled="draftLink.anchors.length >= 24"
              @click="addAnchor"
              >{{ tr("添加锚点", "Add anchor") }}</el-button
            ><el-button
              v-if="draftLink.anchors.length"
              size="small"
              @click="straighten"
              >{{ tr("恢复直线", "Make straight") }}</el-button
            >
          </div>
        </template>
      </el-form>
    </div>
    <footer class="properties-footer">
      <span>{{ tr("修改后保存方案", "Save the scheme to keep edits") }}</span
      ><ActionButton
        type="danger"
        :icon="actionIcons.delete"
        :label="tr('删除当前对象', 'Delete current object')"
        @click="emit('remove')"
      />
    </footer>
  </aside>
</template>

<style scoped>
.object-properties {
  display: flex;
  flex-direction: column;
  background: #fff;
  border: 1px solid var(--sy-border, #dde5ef);
  border-radius: 6px;
  box-shadow: 0 6px 24px #263b5314;
  overflow: hidden;
  color: var(--sy-text, #334155);
}
.properties-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 12px 14px;
  border-bottom: 1px solid var(--sy-border, #dde5ef);
  flex-shrink: 0;
}
.properties-header h3 {
  margin: 0 0 4px;
  font-size: 14px;
  font-weight: 650;
}
.properties-header span,
.properties-hint,
.properties-footer,
.point-index,
.connection-info dt {
  color: var(--sy-text-muted, #6b7b91);
  font-size: 12px;
}
.properties-body {
  flex: 1;
  min-height: 0;
  overflow-y: auto;
  padding: 14px;
}
.properties-body :deep(.el-form-item) {
  margin-bottom: 16px;
}
.properties-body :deep(.el-form-item__label) {
  font-size: 12px;
  color: var(--sy-text-muted, #6b7b91);
  margin-bottom: 6px;
}
.properties-body :deep(.el-input-number) {
  width: 100%;
  min-width: 0;
}
.properties-body :deep(.el-input-number .el-input__inner) {
  text-align: left;
}
.coordinate-pair {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 10px;
}
.crossing-sources {
  list-style: none;
  margin: 0 0 16px;
  padding: 0;
  font-size: 12px;
}
.crossing-sources li {
  padding: 7px 10px;
  background: #f8fafc;
  border: 1px solid var(--sy-border, #dde5ef);
  border-radius: 4px;
  margin-bottom: 6px;
  overflow-wrap: anywhere;
}
.crossing-sources span,
.crossing-sources small {
  color: var(--sy-text-muted, #6b7b91);
}
.color-setting {
  display: flex;
  align-items: center;
  gap: 8px;
  width: 100%;
}
.color-setting .el-input {
  flex: 1;
  min-width: 0;
}
.color-setting .el-button {
  margin: 0;
  padding: 4px;
}
.section-heading {
  display: flex;
  justify-content: space-between;
  margin: 4px 0 12px;
  font-size: 12px;
  font-weight: 600;
}
.section-heading span {
  color: var(--sy-text-muted, #6b7b91);
  font-weight: 400;
}
.point-row {
  display: grid;
  grid-template-columns: 18px minmax(0, 1fr) minmax(0, 1fr);
  align-items: center;
  gap: 6px;
  margin-bottom: 8px;
}
.anchor-row {
  grid-template-columns: 14px minmax(0, 1fr) minmax(0, 1fr) 26px;
}
.properties-hint {
  line-height: 1.6;
  margin: 10px 0;
}
.connection-info {
  margin: 0 0 18px;
}
.connection-info dd {
  font-size: 12px;
  line-height: 1.6;
  margin: 4px 0 10px;
  overflow-wrap: anywhere;
}
.anchor-actions {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}
.anchor-actions .el-button {
  margin: 0;
}
.properties-footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 8px 14px;
  border-top: 1px solid var(--sy-border, #dde5ef);
  flex-shrink: 0;
}
</style>
