<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, useId, watch } from "vue";
import { useI18n } from "vue-i18n";
import PaneDivider from "../components/ui/PaneDivider.vue";
import {
    headTimeAt,
    cellApproachingInterval,
    approachPreparationIntervals,
    type SignalParameters,
    type TractionResult,
    type TractionCellOccupancy,
    type TractionRouteProfile,
} from "./tractionCalculation";

import { pointsFromSegments, limitAtPosition, maximumSpeedLimitPoints, type SpeedLimitPoint } from './speedLimitPoints';

const props = defineProps<{
    profile: TractionRouteProfile;
    result: TractionResult | null;
    /** Signal settings captured with this result, not the editable parameter panel. */
    calculatedSignal: SignalParameters | null;
    limitPoints?: SpeedLimitPoint[];
    limitsEditable?: boolean;
    /** Automatic approach is fixed; route speed-limit editing starts here. */
    editableStartM?: number;
    /** Display-only crop in saved result coordinates; never changes calculation inputs. */
    visibleStartM?: number;
    trainLengthM: number;
}>();
const emit = defineEmits<{ 'update:limitPoints': [points: SpeedLimitPoint[]] }>();
const { locale } = useI18n();
const ui = (zh: string, en: string) => locale.value.startsWith("zh") ? zh : en;
const hoverPosition = ref<number | null>(null);
const selectedLimitIndex = ref(0);
const editingLimits = ref(false);
const limitMenu = ref<{ index: number; x: number; y: number } | null>(null);
const limitMenuElement = ref<HTMLElement | null>(null);
let anchorPointerUp = false;
const draftLimits = ref<SpeedLimitPoint[] | null>(null);
let limitDrag: { index: number; pointerId: number; maximum: number; original: SpeedLimitPoint[] } | null = null;
const limitPoints = computed(() => draftLimits.value ?? props.limitPoints ?? pointsFromSegments(props.profile.segments));
const samples = computed(() => props.result?.samples ?? []);
const headAt = (position: number) => props.result ? headTimeAt(props.result, position) : null;
const chartId = useId();
const gradientId = chartId + '-speed-fill';
const patternId = chartId + '-uncleared';
const speedClipId = chartId + '-speed-clip';
const timeClipId = chartId + '-time-clip';

// Both panes use a common pixel-wide viewBox and a stable scrollbar gutter.
// Their heights change independently without distorting text or the shared X axis.
const chartRoot = ref<HTMLElement | null>(null);
const speedPane = ref<HTMLElement | null>(null);
const occupancyPane = ref<HTMLElement | null>(null);
const speedSvg = ref<SVGSVGElement | null>(null);
const timeSvg = ref<SVGSVGElement | null>(null);
const availableHeight = ref(600);
const splitRatio = defineModel<number>("splitRatio", { default: 0.5 });
const paneSpace = computed(() => Math.max(0, availableHeight.value - 8));
const minimumPaneHeight = computed(() => Math.min(140, paneSpace.value / 3));
const maximumSpeedPaneHeight = computed(() => paneSpace.value - minimumPaneHeight.value);
const speedPaneHeight = computed(() => Math.round(Math.min(
    maximumSpeedPaneHeight.value,
    Math.max(minimumPaneHeight.value, paneSpace.value * splitRatio.value),
)));
const measuredWidth = ref(1000);
const width = computed(() => measuredWidth.value);
const speedHeight = ref(260);
const timeHeight = ref(260);
const left = 66;
const right = 28;
const plotWidth = computed(() => width.value - left - right);
const speedTop = 24;
const speedBottom = computed(() => speedHeight.value - speedAxisHeight.value);
const timeTop = 24;
const timeBottom = computed(() => timeHeight.value - 58);
let resizeObserver: ResizeObserver | null = null;
let resizeFrame = 0;
const heightOf = (pane: HTMLElement, selector: string) =>
    pane.querySelector<HTMLElement>(selector)?.getBoundingClientRect().height ?? 0;
const measureCharts = () => {
    const root = chartRoot.value;
    const speed = speedPane.value;
    const occupancy = occupancyPane.value;
    if (!root || !speed || !occupancy || !speedSvg.value || !timeSvg.value) return;
    const nextHeight = root.clientHeight;
    const nextWidth = Math.min(
        speedSvg.value.getBoundingClientRect().width,
        timeSvg.value.getBoundingClientRect().width,
    );
    // Hidden tabs have no usable dimensions. Retain the last valid SVG geometry.
    if (nextHeight <= 8 || nextWidth <= left + right) return;
    availableHeight.value = nextHeight;
    measuredWidth.value = nextWidth;
    // Keep charts useful when a pane is made small; overflow belongs to that pane.
    speedHeight.value = Math.round(Math.max(144 + speedAxisHeight.value,
        speed.clientHeight - heightOf(speed, ".chart-heading") - heightOf(speed, ".chart-inspector"),
    ));
    timeHeight.value = Math.round(Math.max(240,
        occupancy.clientHeight - heightOf(occupancy, ".chart-heading"),
    ));
};
const scheduleMeasure = () => {
    cancelAnimationFrame(resizeFrame);
    resizeFrame = requestAnimationFrame(measureCharts);
};
const resizeSpeedPane = (height: number) => {
    if (paneSpace.value <= 0) return;
    splitRatio.value = Math.min(maximumSpeedPaneHeight.value, Math.max(minimumPaneHeight.value, height)) / paneSpace.value;
    scheduleMeasure();
};
const resetSplit = () => {
    splitRatio.value = 0.5;
    scheduleMeasure();
};
onMounted(() => {
    resizeObserver = new ResizeObserver(scheduleMeasure);
    for (const element of [chartRoot.value, speedPane.value, occupancyPane.value]) {
        if (element) resizeObserver.observe(element);
    }
    chartRoot.value?.querySelectorAll(".chart-heading, .chart-inspector").forEach(
        element => resizeObserver?.observe(element),
    );
    window.addEventListener("resize", scheduleMeasure);
    window.addEventListener("pointerdown", dismissLimitMenu);
    window.addEventListener("keydown", handleEditorEscape);
    scheduleMeasure();
});
onBeforeUnmount(() => {
    resizeObserver?.disconnect();
    cancelAnimationFrame(resizeFrame);
    window.removeEventListener("resize", scheduleMeasure);
    window.removeEventListener("pointerdown", dismissLimitMenu);
    window.removeEventListener("keydown", handleEditorEscape);
});
const length = computed(() => props.profile.lengthM > 0 ? props.profile.lengthM : 1);
const viewStart = computed(() => Number.isFinite(props.visibleStartM)
    ? Math.max(0, Math.min(length.value - .001, props.visibleStartM!)) : 0);
const viewLength = computed(() => length.value - viewStart.value);
const x = (position: number) => left + (position - viewStart.value) / viewLength.value * plotWidth.value;
const positionAtX = (pixel: number) => viewStart.value + (pixel - left) / plotWidth.value * viewLength.value;
const finite = (value: number | null | undefined): value is number =>
    typeof value === "number" && Number.isFinite(value);
const niceStep = (maximum: number, ticks = 5) => {
    const raw = Math.max(maximum, 1) / ticks;
    const magnitude = 10 ** Math.floor(Math.log10(raw));
    const fraction = raw / magnitude;
    return (fraction <= 1 ? 1 : fraction <= 2 ? 2 : fraction <= 2.5 ? 2.5 : fraction <= 5 ? 5 : 10) * magnitude;
};
const speedExtent = computed(() => {
    const segmentMaximum = props.profile.segments.reduce(
        (maximum, segment) => Math.max(maximum, segment.speedLimitKmh),
        Math.max(20, props.result?.maxSpeedKmh ?? 0, ...limitPoints.value.map(point => point.speedLimitKmh)),
    );
    return samples.value.reduce(
        (maximum, sample) => Math.max(maximum, sample.limitKmh), segmentMaximum,
    );
});
const speedStep = computed(() => niceStep(speedExtent.value));
const speedMax = computed(() => limitDrag && draftLimits.value ? limitDrag.maximum : Math.ceil(speedExtent.value / speedStep.value) * speedStep.value);
const speedY = (speed: number) => speedBottom.value - speed / speedMax.value * (speedBottom.value - speedTop);
const timeExtent = computed(() => Math.max(
    1,
    props.result?.totalTimeS ?? 0,
    headAt(length.value + props.trainLengthM) ?? 0,
    ...(props.result?.jointTimes ?? []).map(joint => joint.releaseTimeS ?? joint.tailTimeS ?? joint.headTimeS ?? 0),
    ...(props.result?.cellOccupancies ?? []).map(cell => cell.releaseTimeS ?? cell.exitTimeS ?? cell.entryTimeS ?? 0),
));
const timeStep = computed(() => niceStep(timeExtent.value));
const timeMax = computed(() => Math.ceil(timeExtent.value / timeStep.value) * timeStep.value);
const timeY = (time: number) => timeTop + time / timeMax.value * (timeBottom.value - timeTop);
const ticks = (maximum: number, step: number) => Array.from(
    { length: Math.floor(maximum / step) + 1 },
    (_, index) => Math.round(index * step * 1e6) / 1e6,
);
const speedTicks = computed(() => ticks(speedMax.value, speedStep.value));
const timeTicks = computed(() => props.result ? ticks(timeMax.value, timeStep.value) : []);
const positionTicks = computed(() => {
    const step = niceStep(viewLength.value, 6);
    const firstTick = Math.ceil(viewStart.value / step) * step;
    const values = ticks(viewLength.value, step).map(tick => Math.round((firstTick + tick) * 1e6) / 1e6)
        .filter(tick => tick > viewStart.value + viewLength.value * .06 && tick < length.value - viewLength.value * .06);
    return [viewStart.value, ...values, length.value];
});
const distanceLabel = (position: number) =>
    position.toLocaleString(locale.value, { maximumFractionDigits: 0 });
const numberLabel = (value: number, digits = 1) =>
    value.toLocaleString(locale.value, { maximumFractionDigits: digits });
const timeLabel = (time: number | null | undefined) => {
    if (!finite(time)) return ui("未通过", "Not passed");
    const rounded = Math.round(time);
    return Math.floor(rounded / 60) + ":" + String(rounded % 60).padStart(2, "0");
};
const pathFrom = (points: Array<{ x: number; y: number | null }>) => {
    let started = false;
    return points.map(point => {
        if (!finite(point.y)) {
            started = false;
            return "";
        }
        const command = started ? "L" : "M";
        started = true;
        return command + point.x.toFixed(2) + "," + point.y.toFixed(2);
    }).join(" ");
};
const speedPath = computed(() => pathFrom(samples.value.map(sample => ({
    x: x(sample.positionM), y: speedY(sample.speedKmh),
}))));
const speedArea = computed(() => {
    const first = samples.value[0];
    const last = samples.value[samples.value.length - 1];
    return first && last
        ? speedPath.value + " L" + x(last.positionM) + "," + speedBottom.value +
            " L" + x(first.positionM) + "," + speedBottom.value + " Z"
        : "";
});
const limitPath = computed(() => {
    const points: Array<{ x: number; y: number }> = [];
    limitPoints.value.forEach((point, index) => {
        if (index) points.push({ x: x(point.positionM), y: speedY(limitPoints.value[index - 1]!.speedLimitKmh) });
        points.push({ x: x(point.positionM), y: speedY(point.speedLimitKmh) });
    });
    const last = limitPoints.value[limitPoints.value.length - 1];
    if (last) points.push({ x: x(length.value), y: speedY(last.speedLimitKmh) });
    return pathFrom(points);
});
const limitSegments = computed(() => limitPoints.value.map((point, index) => ({
    ...point, endM: limitPoints.value[index + 1]?.positionM ?? length.value,
})).filter(segment => segment.endM > viewStart.value && segment.positionM < length.value)
    .map(segment => ({ ...segment, positionM: Math.max(viewStart.value, segment.positionM) })));
const limitLabelY = (speed: number) => {
    const y = speedY(speed);
    return y - 8 >= speedTop + 12 ? y - 8 : y + 16;
};
const headPath = computed(() => pathFrom(samples.value.map(sample => ({
    x: x(sample.positionM), y: timeY(sample.timeS),
}))));
const tailPoints = computed(() => {
    // Shift the server's head samples by train length for display. Keep both
    // samples at a stop so the tail line includes the vertical dwell segment.
    if (!props.result) return [];
    const points = samples.value
        .filter(sample => sample.positionM >= viewStart.value + props.trainLengthM && sample.positionM <= length.value + props.trainLengthM)
        .map(sample => ({ positionM: sample.positionM - props.trainLengthM, timeS: sample.timeS as number | null }));
    if (!points.length || points[0]!.positionM > viewStart.value) {
        points.unshift({ positionM: viewStart.value, timeS: headAt(viewStart.value + props.trainLengthM) });
    }
    if (points[points.length - 1]!.positionM < length.value) {
        points.push({ positionM: length.value, timeS: headTimeAt(props.result, length.value + props.trainLengthM) });
    }
    return points.map(point => ({ x: x(point.positionM), y: point.timeS === null ? null : timeY(point.timeS) }));
});
const tailPath = computed(() => pathFrom(tailPoints.value));
const cells = computed(() => (props.result?.cellOccupancies ?? []).filter(cell =>
    cell.endM > viewStart.value && cell.startM < length.value,
).map(cell => {
    const approaching = cellApproachingInterval(props.result, cell);
    const unknownEntry = cell.entryBoundaryKnown === false || !finite(cell.entryTimeS);
    const unknownExitBoundary = cell.exitBoundaryKnown === false;
    // An unknown entry continues from above the visible time window. It is
    // labelled explicitly rather than assigning a fictitious zero entry time.
    const start = unknownEntry ? 0 : cell.entryTimeS!;
    const end = unknownExitBoundary || !finite(cell.exitTimeS) ? timeMax.value : cell.exitTimeS;
    // Physical occupancy ends at tail exit; the saved release delay is a separate phase.
    const release = !unknownExitBoundary && finite(cell.exitTimeS) && finite(cell.releaseTimeS)
        && cell.exitTimeS >= start && cell.releaseTimeS > cell.exitTimeS
        ? { startTimeS: cell.exitTimeS, endTimeS: cell.releaseTimeS, durationS: cell.releaseTimeS - cell.exitTimeS }
        : null;
    return {
        ...cell,
        x: x(Math.max(viewStart.value, cell.startM)),
        width: Math.max(0, x(Math.min(length.value, cell.endM)) - x(Math.max(viewStart.value, cell.startM))),
        approaching,
        preparation: approachPreparationIntervals(approaching, props.calculatedSignal).map(interval => ({
            ...interval, y: timeY(interval.startTimeS),
            height: Math.max(0, timeY(interval.endTimeS) - timeY(interval.startTimeS)),
        })),
        approachingY: approaching ? timeY(approaching.startTimeS) : 0,
        approachingHeight: approaching ? Math.max(0, timeY(approaching.endTimeS) - timeY(approaching.startTimeS)) : 0,
        y: timeY(start),
        height: Math.max(0, timeY(end) - timeY(start)),
        release,
        releaseY: release ? timeY(release.startTimeS) : 0,
        releaseHeight: release ? Math.max(0, timeY(release.endTimeS) - timeY(release.startTimeS)) : 0,
        incomplete: unknownExitBoundary || !finite(cell.exitTimeS),
        unknownEntry,
        unknownExitBoundary,
    };
}));
// Circuit ranges and names always share one row on the common position scale.
// Fit names inside their ranges; the title retains the complete name and mileage.
const circuitAxis = computed(() => {
    const source = (props.profile.cells ?? props.result?.cellOccupancies ?? [])
        .filter(cell => finite(cell.startM) && finite(cell.endM) && cell.endM > viewStart.value && cell.startM < length.value)
        .map(cell => ({ ...cell, startM: Math.max(viewStart.value, cell.startM), endM: Math.min(length.value, cell.endM) }))
        .filter(cell => cell.endM > cell.startM)
        .sort((a, b) => a.startM - b.startM || a.endM - b.endM || a.id.localeCompare(b.id));
    return source.map((cell, index) => {
        const start = x(cell.startM), end = x(cell.endM), center = (start + end) / 2;
        const name = cell.label || cell.id;
        const characters = Array.from(name);
        const characterWidth = (character: string) => character.codePointAt(0)! > 255 ? 12 : 7;
        const availableWidth = Math.max(0, end - start - 8);
        const fullWidth = characters.reduce((sum, character) => sum + characterWidth(character), 0);
        let label = name;
        if (fullWidth > availableWidth) {
            label = ''; let textWidth = 0;
            if (availableWidth >= 12) {
                for (const character of characters) {
                    if (textWidth + characterWidth(character) > availableWidth - 12) break;
                    label += character; textWidth += characterWidth(character);
                }
                label += '…';
            }
        }
        return { ...cell, name, label, start, end, center, color: index % 2 ? '#8dbdff' : '#6ddac9' };
    });
});
const circuitAxisHeight = computed(() => circuitAxis.value.length ? 38 : 0);
const circuitY = computed(() => speedBottom.value + 28);
const joints = computed(() => (props.result?.jointTimes ?? []).filter(joint =>
    joint.positionM >= viewStart.value && joint.positionM <= length.value && finite(joint.headTimeS),
));
const deviceKind = (type: string) => /switch|turnout|道岔/i.test(type) ? 'turnout' : /signal|信号/i.test(type) ? 'signal' : 'other';
const devices = computed(() => {
    const groups = new Map<string, NonNullable<TractionRouteProfile["devices"]>>();
    for (const device of props.profile.devices ?? []) {
        if (device.positionM < viewStart.value || device.positionM > length.value || deviceKind(device.type) === 'other') continue;
        const key = device.positionM.toFixed(6) + ':' + device.type;
        const group = groups.get(key);
        if (group) group.push(device);
        else groups.set(key, [device]);
    }
    return [...groups.values()].map(group => ({
        ...group[0]!,
        id: group.map(device => device.id).join("|"),
        label: group.map(device => /switch|turnout|道岔/i.test(device.type) && !device.label.startsWith('岔') ? ui('岔', 'SW ') + device.label : device.label).join(" · "),
    })).sort((a, b) => a.positionM - b.positionM);
});
const deviceLabel = (label: string) => label.length > 14 ? label.slice(0, 13) + "…" : label;
const deviceLabelRowHeight = 16;
let deviceTextContext: CanvasRenderingContext2D | null | undefined;
const deviceTextWidth = (label: string) => {
    if (deviceTextContext === undefined) {
        deviceTextContext = typeof document === 'undefined' ? null : document.createElement('canvas').getContext('2d');
        if (deviceTextContext) deviceTextContext.font = '10px system-ui, sans-serif';
    }
    return deviceTextContext?.measureText(label).width ?? Array.from(label).reduce((sum, character) => sum + (character.codePointAt(0)! > 255 ? 10 : 6), 0);
};
const deviceLabelLayout = computed(() => {
    const rowEnds = [-Infinity, -Infinity];
    const items = devices.value.map(device => {
        const displayLabel = deviceLabel(device.label), halfWidth = deviceTextWidth(displayLabel) / 2;
        const center = x(device.positionM), start = center - halfWidth, end = center + halfWidth;
        // Keep arrows aligned; only overlapping names use the second text row.
        const labelRow = start >= rowEnds[0]! + 6 ? 0 : start >= rowEnds[1]! + 6 ? 1 : rowEnds[0]! <= rowEnds[1]! ? 0 : 1;
        rowEnds[labelRow] = Math.max(rowEnds[labelRow]!, end);
        return { ...device, displayLabel, labelRow };
    });
    return { items, rows: items.some(device => device.labelRow === 1) ? 2 : items.length ? 1 : 0 };
});
const speedAxisHeight = computed(() => circuitAxisHeight.value + (deviceLabelLayout.value.rows ? 76 + (deviceLabelLayout.value.rows - 1) * deviceLabelRowHeight : 36));
const deviceArrowY = computed(() => speedBottom.value + circuitAxisHeight.value + 28);
watch(speedAxisHeight, scheduleMeasure);
const deviceColor = (type: string) => {
    const normalized = type.toLowerCase();
    if (/signal|信号/.test(normalized)) return "#6ee7a8";
    if (/switch|turnout|道岔/.test(normalized)) return "#ffbf69";
    return "#90a9c5";
};
const nearestSample = computed(() => {
    const position = hoverPosition.value;
    const points = samples.value;
    if (position === null || !points.length) return null;
    let low = 0;
    let high = points.length - 1;
    while (low < high) {
        const middle = Math.floor((low + high) / 2);
        if (points[middle]!.positionM < position) low = middle + 1;
        else high = middle;
    }
    const after = points[low]!;
    const before = points[Math.max(0, low - 1)]!;
    const span = after.positionM - before.positionM;
    const fraction = span > 0 ? Math.min(1, Math.max(0, (position - before.positionM) / span)) : 0;
    return {
        speedKmh: Math.sqrt(Math.max(0,
            before.speedKmh ** 2 + fraction * (after.speedKmh ** 2 - before.speedKmh ** 2))),
        limitKmh: position < after.positionM ? before.limitKmh : after.limitKmh,
        headTimeS: headAt(position),
        tailTimeS: headAt(position + props.trainLengthM),
    };
});
const selectedCell = computed(() => hoverPosition.value === null ? null :
    (props.result?.cellOccupancies ?? []).find(cell =>
        hoverPosition.value! >= cell.startM && hoverPosition.value! <= cell.endM) ?? null,
);
const handlePointer = (event: PointerEvent) => {
    const svg = event.currentTarget as SVGSVGElement;
    const matrix = svg.getScreenCTM();
    if (!matrix) return;
    const point = new DOMPoint(event.clientX, event.clientY).matrixTransform(matrix.inverse());
    hoverPosition.value = Math.max(viewStart.value, Math.min(length.value, positionAtX(point.x)));
};
const handleKey = (event: KeyboardEvent) => {
    if (event.key === "Escape") {
        exitLimitEdit(); hoverPosition.value = null;
        return;
    }
    if (!["ArrowLeft", "ArrowRight", "Home", "End"].includes(event.key)) return;
    event.preventDefault();
    const direction = event.key === "ArrowRight" ? 1 : -1;
    hoverPosition.value = event.key === "Home" ? viewStart.value : event.key === "End" ? length.value :
        Math.max(viewStart.value, Math.min(length.value, (hoverPosition.value ?? viewStart.value) + direction * viewLength.value / 100));
};
const firstEditableIndex = computed(() => (props.editableStartM ?? 0) > 0 ? 1 : 0);
const pointBounds = (index: number) => ({
    min: index <= firstEditableIndex.value ? (index === 0 ? 0 : props.editableStartM!) : limitPoints.value[index - 1]!.positionM + .001,
    max: index <= firstEditableIndex.value ? (index === 0 ? 0 : props.editableStartM!) : (limitPoints.value[index + 1]?.positionM ?? length.value) - .001,
});
function handleSpeedClick() {
    // Pointer capture sends the drag-release click to the SVG.
    if (anchorPointerUp) { anchorPointerUp = false; return; }
    exitLimitEdit();
}
function enterLimitEdit() {
    if (!props.limitsEditable) return;
    editingLimits.value = true; limitMenu.value = null; anchorPointerUp = false;
    speedSvg.value?.focus({ preventScroll: true });
}
function exitLimitEdit() {
    stopLimitDrag(false); editingLimits.value = false; limitMenu.value = null; anchorPointerUp = false;
}
function handleEditorEscape(event: KeyboardEvent) {
    if (event.key === 'Escape' && editingLimits.value) exitLimitEdit();
}
function dismissLimitMenu(event: PointerEvent) {
    anchorPointerUp = false;
    if (limitMenu.value && !limitMenuElement.value?.contains(event.target as Node)) limitMenu.value = null;
}
function openLimitMenu(event: MouseEvent, index: number) {
    if (!props.limitsEditable || !editingLimits.value) return;
    selectedLimitIndex.value = index;
    limitMenu.value = { index, x: Math.max(8, Math.min(event.clientX, window.innerWidth - 168)), y: Math.max(8, Math.min(event.clientY, window.innerHeight - 52)) };
}
function svgPoint(event: MouseEvent | PointerEvent) {
    const matrix = speedSvg.value?.getScreenCTM();
    return matrix ? new DOMPoint(event.clientX, event.clientY).matrixTransform(matrix.inverse()) : null;
}
function commitLimitPoints(points: SpeedLimitPoint[], selected = selectedLimitIndex.value) {
    if (!props.limitsEditable) return;
    selectedLimitIndex.value = Math.min(Math.max(0, selected), points.length - 1);
    emit('update:limitPoints', points);
}
function updateLimitPoint(index: number, positionM: number, speedLimitKmh: number) {
    if (!props.limitsEditable || index < firstEditableIndex.value || !finite(positionM) || !finite(speedLimitKmh) || !limitPoints.value[index]) return;
    const bounds = pointBounds(index), points = limitPoints.value.map(point => ({ ...point }));
    points[index] = { positionM: Math.max(bounds.min, Math.min(bounds.max, positionM)), speedLimitKmh: Math.max(.001, Math.min(1000, speedLimitKmh)) };
    commitLimitPoints(points, index);
}
function addLimitPoint(event: MouseEvent) {
    if (!props.limitsEditable || !editingLimits.value) return;
    const point = svgPoint(event);
    if (!point || point.x < left || point.x > width.value - right || point.y < speedTop || point.y > speedBottom.value) return;
    const positionM = Math.max(viewStart.value, Math.min(length.value - .001, Math.round(positionAtX(point.x) * 10) / 10));
    if (positionM < (props.editableStartM ?? 0)) return;
    const existing = limitPoints.value.findIndex(item => Math.abs(item.positionM - positionM) < .001);
    if (existing >= 0) { selectedLimitIndex.value = existing; return; }
    if (limitPoints.value.length - firstEditableIndex.value >= maximumSpeedLimitPoints) return;
    // Inserting on the line keeps the existing limit until the user drags the new point.
    const speedLimitKmh = limitAtPosition(limitPoints.value, positionM);
    const points = [...limitPoints.value.map(item => ({ ...item })), { positionM, speedLimitKmh }].sort((a, b) => a.positionM - b.positionM);
    commitLimitPoints(points, points.findIndex(item => item.positionM === positionM));
    limitMenu.value = null;
}
function removeLimitPoint() {
    if (selectedLimitIndex.value <= firstEditableIndex.value || !props.limitsEditable || !editingLimits.value) return;
    limitMenu.value = null;
    commitLimitPoints(limitPoints.value.filter((_, index) => index !== selectedLimitIndex.value), selectedLimitIndex.value - 1);
}
function startLimitDrag(event: PointerEvent, index: number) {
    selectedLimitIndex.value = index; anchorPointerUp = false;
    if (!props.limitsEditable || index < firstEditableIndex.value || event.button !== 0) return;
    (event.currentTarget as SVGElement).focus();
    limitMenu.value = null;
    limitDrag = { index, pointerId: event.pointerId, maximum: speedMax.value, original: limitPoints.value.map(point => ({ ...point })) };
    draftLimits.value = limitDrag.original.map(point => ({ ...point }));
    speedSvg.value?.setPointerCapture(event.pointerId);
}
function handleSpeedPointer(event: PointerEvent) {
    handlePointer(event);
    if (!limitDrag || event.pointerId !== limitDrag.pointerId || !draftLimits.value) return;
    const point = svgPoint(event); if (!point) return;
    const bounds = pointBounds(limitDrag.index);
    const positionM = Math.round(positionAtX(point.x) * 10) / 10;
    const speedLimitKmh = Math.round((speedBottom.value - point.y) / (speedBottom.value - speedTop) * limitDrag.maximum * 10) / 10;
    draftLimits.value[limitDrag.index] = { positionM: Math.max(bounds.min, Math.min(bounds.max, positionM)), speedLimitKmh: Math.max(.001, Math.min(limitDrag.maximum, speedLimitKmh)) };
}
function stopLimitDrag(commit: boolean) {
    if (!limitDrag) return;
    const drag = limitDrag, points = draftLimits.value;
    anchorPointerUp = commit;
    limitDrag = null; draftLimits.value = null;
    if (speedSvg.value?.hasPointerCapture(drag.pointerId)) speedSvg.value.releasePointerCapture(drag.pointerId);
    if (commit && points && JSON.stringify(points) !== JSON.stringify(drag.original)) commitLimitPoints(points, drag.index);
}
function limitKey(event: KeyboardEvent, index: number) {
    selectedLimitIndex.value = index;
    if (event.key === 'Delete' || event.key === 'Backspace') { event.preventDefault(); removeLimitPoint(); }
    else if (event.key.startsWith('Arrow')) {
        event.preventDefault(); const point = limitPoints.value[index]!; const step = event.shiftKey ? 10 : 1;
        updateLimitPoint(index, point.positionM + (event.key === 'ArrowRight' ? step : event.key === 'ArrowLeft' ? -step : 0),
            point.speedLimitKmh + (event.key === 'ArrowUp' ? step : event.key === 'ArrowDown' ? -step : 0));
    } else if (event.key === 'Escape') exitLimitEdit();
}
watch(() => props.limitPoints, () => { stopLimitDrag(false); selectedLimitIndex.value = Math.max(0, Math.min(selectedLimitIndex.value, limitPoints.value.length - 1)); });
watch(() => props.limitsEditable, editable => { if (!editable) exitLimitEdit(); });
watch(viewStart, () => { exitLimitEdit(); hoverPosition.value = null; });
const inspectedLimit = computed(() => hoverPosition.value === null ? null : limitAtPosition(limitPoints.value, hoverPosition.value));
const cellTitle = (cell: TractionCellOccupancy) => {
    const entry = cell.entryBoundaryKnown === false || !finite(cell.entryTimeS)
        ? ui("起点前占用（起始时刻未知）", "Occupied before route start (entry time unknown)")
        : timeLabel(cell.entryTimeS);
    const release = cell.exitBoundaryKnown === false
        ? ui("边界未覆盖（出清、释放时刻未知）", "Boundary outside route (clearance and release unknown)")
        : finite(cell.releaseTimeS) ? timeLabel(cell.releaseTimeS) : ui("未出清", "Not cleared");
    return cell.label + " · " + ui("占用", "Occupied") + " " + entry + " → " + release;
};
</script>

<template>
    <div ref="chartRoot" class="traction-charts" :data-view-start="viewStart" :data-view-end="length" @click="handleSpeedClick">
        <section ref="speedPane" class="chart-panel chart-pane speed-pane" :style="{ flexBasis: speedPaneHeight + 'px' }" @pointerleave="hoverPosition = null">
            <header class="chart-heading">
                <div>
                    <h3>{{ ui("速度–位置曲线", "Speed–position profile") }}</h3>
                </div>
                <div class="chart-legend">
                    <span><i class="legend-line speed-line"></i>{{ ui("列车速度", "Train speed") }}</span>
                    <span><i class="legend-line limit-line"></i>{{ ui("允许速度", "Speed limit") }}</span>
                    <span><i class="legend-device"></i>{{ ui("关键设备", "Key devices") }}</span>
                    <span><i class="legend-circuit"></i>{{ ui("轨道电路", "Track circuits") }}</span>
                </div>
            </header>
            <svg
                class="plot speed-svg" :class="{ 'editing-limit': editingLimits }"
                ref="speedSvg" :viewBox="'0 0 ' + width + ' ' + speedHeight" :style="{ height: speedHeight + 'px' }"
                role="img"
                tabindex="0"
                :aria-label="ui('速度位置图，横轴标注轨道电路范围和名称，左右方向键可查看沿线数值', 'Speed by position with track circuit ranges and names. Use arrow keys to inspect the route.')"
                @pointermove="handleSpeedPointer"
                @pointerup="stopLimitDrag(true)" @pointercancel="stopLimitDrag(false)" @lostpointercapture="stopLimitDrag(false)"
                @pointerdown="handlePointer"
                @keydown="handleKey"
                @blur="hoverPosition = null"
            >
                <defs>
                    <linearGradient :id="gradientId" x1="0" y1="0" x2="0" y2="1">
                        <stop offset="0%" stop-color="#44e2e0" stop-opacity="0.19" />
                        <stop offset="100%" stop-color="#44e2e0" stop-opacity="0.015" />
                    </linearGradient>
                    <clipPath :id="speedClipId"><rect :x="left" :y="speedTop" :width="plotWidth" :height="speedBottom - speedTop" /></clipPath>
                </defs>
                <rect :x="left" :y="speedTop" :width="plotWidth" :height="speedBottom - speedTop" class="plot-background" />
                <g v-for="tick in speedTicks" :key="'speed-' + tick" class="axis">
                    <line :x1="left" :x2="width - right" :y1="speedY(tick)" :y2="speedY(tick)" class="grid-line" />
                    <text :x="left - 12" :y="speedY(tick) + 4" text-anchor="end">{{ numberLabel(tick) }}</text>
                </g>
                <g v-for="tick in positionTicks" :key="'position-' + tick" class="axis">
                    <line :x1="x(tick)" :x2="x(tick)" :y1="speedTop" :y2="speedBottom" class="grid-line" />
                    <text :x="x(tick)" :y="speedBottom + 19" text-anchor="middle">{{ distanceLabel(tick) }}</text>
                </g>
                <text :x="left" y="14" class="axis-unit">km/h</text>
                <text :x="width - right" :y="speedHeight - 10" text-anchor="end" class="axis-unit">{{ ui("进路位置", "Route position") }} / m</text>
                <g class="circuit-axis" :aria-label="ui('轨道电路区段范围', 'Track circuit extents')">
                    <g v-for="cell in circuitAxis" :key="cell.id" class="circuit-axis-cell" :data-cell-id="cell.id"
                        :class="{ 'circuit-active': hoverPosition !== null && hoverPosition >= cell.startM && hoverPosition <= cell.endM }" :style="{ color: cell.color }">
                        <title>{{ cell.name }} · {{ numberLabel(cell.startM) }}–{{ numberLabel(cell.endM) }} m</title>
                        <line :x1="cell.start" :x2="cell.start" :y1="speedBottom" :y2="circuitY" class="circuit-boundary-guide" />
                        <line :x1="cell.end" :x2="cell.end" :y1="speedBottom" :y2="circuitY" class="circuit-boundary-guide" />
                        <rect :x="cell.start" :y="circuitY" :width="cell.end - cell.start" height="7" class="circuit-range" />
                        <path :d="'M' + cell.start + ',' + (circuitY - 2) + ' v11 M' + cell.end + ',' + (circuitY - 2) + ' v11'" class="circuit-range-edge" />
                        <path :d="'M' + cell.center + ',' + (circuitY + 7) + ' L' + cell.center + ',' + (circuitY + 12)" class="circuit-name-guide" />
                        <text :x="cell.center" :y="circuitY + 24" text-anchor="middle" class="circuit-name">{{ cell.label }}</text>
                    </g>
                </g>
                <g v-for="device in deviceLabelLayout.items" :key="device.id" class="device-marker" :data-label-row="device.labelRow" :data-device-kind="deviceKind(device.type)">
                    <line :x1="x(device.positionM)" :x2="x(device.positionM)" :y1="speedTop" :y2="speedBottom" :stroke="deviceColor(device.type)" class="device-guide" />
                    <path
                        :d="'M' + x(device.positionM) + ',' + deviceArrowY + ' l-4,7 h8 Z'"
                        :fill="deviceColor(device.type)"
                    />
                    <text
                        :x="x(device.positionM)"
                        :y="deviceArrowY + 20 + device.labelRow * deviceLabelRowHeight"
                        :fill="deviceColor(device.type)"
                        class="device-label"
                        text-anchor="middle"
                    >{{ device.displayLabel }}</text>
                    <title>{{ device.label }} · {{ distanceLabel(device.positionM) }} m</title>
                </g>
                <path :d="speedArea" :fill="'url(#' + gradientId + ')'" :clip-path="'url(#' + speedClipId + ')'" />
                <path :d="limitPath" class="limit-curve" :clip-path="'url(#' + speedClipId + ')'" />
                <path :d="speedPath" class="speed-curve" :clip-path="'url(#' + speedClipId + ')'" />
                <path v-if="limitsEditable" :d="limitPath" class="limit-hit-target" :clip-path="'url(#' + speedClipId + ')'" @click.stop="enterLimitEdit" @dblclick.stop="addLimitPoint($event)">
                    <title>{{ ui('点击限速线进入编辑，双击增加点，拖动调整，右键删除；Esc 或点击其他位置退出。', 'Click to edit, double-click to add, drag to adjust, right-click to delete. Escape or click elsewhere to finish.') }}</title>
                </path>
                <g v-if="editingLimits && limitsEditable" class="limit-segment-labels" pointer-events="none">
                    <text v-for="(segment, index) in limitSegments" :key="index"
                        :x="x((segment.positionM + segment.endM) / 2)" :y="limitLabelY(segment.speedLimitKmh)"
                        :data-start="segment.positionM" :data-end="segment.endM" :data-limit="segment.speedLimitKmh"
                        text-anchor="middle" class="limit-segment-value">{{ numberLabel(segment.speedLimitKmh, 3) }}</text>
                </g>
                <g v-if="editingLimits && limitsEditable" class="limit-handles">
                <circle v-for="(point, index) in limitPoints" v-show="index >= firstEditableIndex && point.positionM >= viewStart && point.positionM <= length" :key="index" :cx="x(point.positionM)" :cy="speedY(point.speedLimitKmh)" r="5" class="limit-anchor" :class="{ selected: index === selectedLimitIndex, readonly: !limitsEditable }"
                    :data-position="point.positionM" :data-limit="point.speedLimitKmh" role="button" :tabindex="limitsEditable ? 0 : -1" :aria-disabled="!limitsEditable"
                    :aria-label="ui('允许速度点', 'Speed limit point') + ' ' + (index + 1) + ': ' + numberLabel(point.positionM) + ' m, ' + numberLabel(point.speedLimitKmh) + ' km/h'"
                    @pointerdown.stop.prevent="startLimitDrag($event, index)" @click.stop="selectedLimitIndex = index" @dblclick.stop @contextmenu.stop.prevent="openLimitMenu($event, index)" @keydown.stop="limitKey($event, index)">
                    <title>{{ numberLabel(point.positionM) }} m · {{ numberLabel(point.speedLimitKmh) }} km/h</title>
                </circle>
                </g>
                <g v-if="hoverPosition !== null && nearestSample" pointer-events="none">
                    <line :x1="x(hoverPosition)" :x2="x(hoverPosition)" :y1="speedTop" :y2="speedBottom" class="cursor-line" />
                    <circle :cx="x(hoverPosition)" :cy="speedY(nearestSample.speedKmh)" r="5" class="speed-cursor" />
                    <template v-if="!editingLimits">
                        <rect :x="Math.min(width - 152, Math.max(left, x(hoverPosition) - 57))" y="30" width="120" height="26" rx="6" class="cursor-label-bg" />
                        <text :x="Math.min(width - 152, Math.max(left, x(hoverPosition) - 57)) + 60" y="47" text-anchor="middle" class="cursor-label">{{ numberLabel(nearestSample.speedKmh) }} km/h</text>
                    </template>
                </g>
            </svg>

            <div class="chart-inspector" role="status" aria-live="polite">
                <span><b>{{ hoverPosition === null ? "—" : distanceLabel(hoverPosition) }}</b> m</span>
                <span>{{ ui("速度", "Speed") }} <b class="cyan">{{ nearestSample ? numberLabel(nearestSample.speedKmh) : "—" }}</b> / {{ inspectedLimit === null ? "—" : numberLabel(inspectedLimit) }} km/h</span>
                <span>{{ ui("车头", "Head") }} <b class="cyan">{{ nearestSample ? timeLabel(nearestSample.headTimeS) : "—" }}</b></span>
                <span>{{ ui("车尾", "Tail") }} <b class="purple">{{ nearestSample ? timeLabel(nearestSample.tailTimeS) : "—" }}</b></span>
                <span class="inspector-cell" :title="selectedCell ? cellTitle(selectedCell) : undefined">{{ selectedCell ? cellTitle(selectedCell) : ui("区段 —", "Section —") }}</span>
            </div>
        </section>

        <PaneDivider
            class="chart-divider"
            direction="vertical"
            :model-value="speedPaneHeight"
            :min="minimumPaneHeight"
            :max="maximumSpeedPaneHeight"
            :reset-value="paneSpace / 2"
            :label="ui('调整上下图表高度', 'Resize speed and occupancy charts')"
            @update:model-value="resizeSpeedPane"
            @reset="resetSplit"
        />

        <section ref="occupancyPane" class="chart-panel chart-pane occupancy-pane" @pointerleave="hoverPosition = null">
            <header class="chart-heading">
                <div>
                    <h3>{{ ui("轨道电路占用时间", "Track circuit occupancy") }}</h3>
                </div>
                <div class="chart-legend">
                    <span><i class="legend-line speed-line"></i>{{ ui("车头", "Train head") }}</span>
                    <span><i class="legend-line tail-line"></i>{{ ui("车尾", "Train tail") }}</span>
                    <span v-if="result?.mode === 'arrival' && calculatedSignal"><i class="legend-route-setting"></i>{{ ui("进路办理", "Route setting") }}</span>
                    <span v-if="result?.mode === 'arrival' && calculatedSignal"><i class="legend-reaction"></i>{{ ui("反应时间", "Reaction time") }}</span>
                    <span v-if="result?.mode === 'arrival'"><i class="legend-approaching"></i>{{ ui("接近时间", "Approaching time") }}</span>
                    <span><i class="legend-block"></i>{{ ui("区段占用", "Section occupancy") }}</span>
                    <span><i class="legend-release"></i>{{ ui("释放时间", "Release time") }}</span>
                    <span><i class="legend-joint"></i>{{ ui("绝缘占用", "Joint occupancy") }}</span>
                </div>
            </header>
            <svg
                class="plot time-svg"
                ref="timeSvg" :viewBox="'0 0 ' + width + ' ' + timeHeight" :style="{ height: timeHeight + 'px' }"
                role="img"
                tabindex="0"
                :aria-label="ui('位置时间图，显示车头车尾时刻、进路办理、反应时间、接近时间、区段占用和释放时间', 'Position–time diagram showing head, tail, route setting, reaction time, approaching time, section occupation and release time.')"
                @pointermove="handlePointer"
                @pointerdown="handlePointer"
                @keydown="handleKey"
                @blur="hoverPosition = null"
            >
                <defs>
                    <pattern :id="patternId" width="9" height="9" patternUnits="userSpaceOnUse" patternTransform="rotate(45)">
                        <line x1="0" y1="0" x2="0" y2="9" stroke="#fbbf69" stroke-opacity=".19" stroke-width="2" />
                    </pattern>
                    <clipPath :id="timeClipId"><rect :x="left" :y="timeTop" :width="plotWidth" :height="timeBottom - timeTop" /></clipPath>
                </defs>
                <rect :x="left" :y="timeTop" :width="plotWidth" :height="timeBottom - timeTop" class="plot-background" />
                <g v-for="tick in timeTicks" :key="'time-' + tick" class="axis">
                    <line :x1="left" :x2="width - right" :y1="timeY(tick)" :y2="timeY(tick)" class="grid-line" />
                    <text :x="left - 12" :y="timeY(tick) + 4" text-anchor="end">{{ timeLabel(tick) }}</text>
                </g>
                <g v-for="tick in positionTicks" :key="'position-' + tick" class="axis">
                    <line :x1="x(tick)" :x2="x(tick)" :y1="timeTop" :y2="timeBottom" class="grid-line" />
                    <text :x="x(tick)" :y="timeBottom + 21" text-anchor="middle">{{ distanceLabel(tick) }}</text>
                </g>
                <text :x="left" y="14" class="axis-unit">{{ ui("分:秒", "min:sec") }}</text>
                <text :x="width - right" :y="timeHeight - 11" text-anchor="end" class="axis-unit">{{ ui("进路位置", "Route position") }} / m</text>
                <g v-for="cell in cells" :key="cell.id" :data-cell-id="cell.id">
                    <rect v-for="interval in cell.preparation" :key="interval.kind"
                        :x="cell.x" :y="interval.y" :width="cell.width" :height="interval.height"
                        :class="interval.kind + '-cell'" :data-start-time="interval.startTimeS" :data-end-time="interval.endTimeS">
                        <title>{{ cell.label }} · {{ interval.kind === 'route-setting' ? ui("进路办理", "Route setting") : ui("反应时间", "Reaction time") }} {{ timeLabel(interval.startTimeS) }} → {{ timeLabel(interval.endTimeS) }} · {{ numberLabel(interval.durationS) }} s</title>
                    </rect>
                    <rect v-if="cell.approaching && cell.approachingHeight > 0"
                        :x="cell.x" :y="cell.approachingY" :width="cell.width" :height="cell.approachingHeight"
                        class="approaching-cell" :data-start-time="cell.approaching.startTimeS" :data-end-time="cell.approaching.endTimeS">
                        <title>{{ cell.label }} · {{ ui("接近时间", "Approaching time") }} {{ timeLabel(cell.approaching.startTimeS) }} → {{ timeLabel(cell.approaching.endTimeS) }} · {{ numberLabel(cell.approaching.durationS) }} s</title>
                    </rect>
                    <rect :x="cell.x" :y="cell.y" :width="cell.width" :height="cell.height" :class="['occupancy-cell', { incomplete: cell.incomplete }]"
                        :data-start-time="cell.unknownEntry ? undefined : cell.entryTimeS" :data-end-time="cell.incomplete ? undefined : cell.exitTimeS" />
                    <rect v-if="cell.release && cell.releaseHeight > 0"
                        :x="cell.x" :y="cell.releaseY" :width="cell.width" :height="cell.releaseHeight"
                        class="release-cell" :data-start-time="cell.release.startTimeS" :data-end-time="cell.release.endTimeS">
                        <title>{{ cell.label }} · {{ ui("释放时间", "Release time") }} {{ timeLabel(cell.release.startTimeS) }} → {{ timeLabel(cell.release.endTimeS) }} · {{ numberLabel(cell.release.durationS) }} s</title>
                    </rect>
                    <rect v-if="cell.incomplete" :x="cell.x" :y="cell.y" :width="cell.width" :height="cell.height" :fill="'url(#' + patternId + ')'" />
                    <text v-if="cell.width > 54 && cell.height > 22" :x="cell.x + cell.width / 2" :y="cell.y + Math.min(18, cell.height / 2 + 4)" text-anchor="middle" class="cell-label">{{ deviceLabel(cell.label) }}</text>
                    <text v-if="cell.unknownEntry && cell.width > 64 && cell.height > 48" :x="cell.x + cell.width / 2" :y="cell.y + 32" text-anchor="middle" class="unknown-entry-label">{{ ui("起点前占用", "Entry time unknown") }}</text>
                    <template v-if="cell.incomplete && cell.width > 64 && cell.height > 42">
                        <text v-if="cell.unknownExitBoundary && cell.height > 66" :x="cell.x + cell.width / 2" :y="timeBottom - 22" text-anchor="middle" class="incomplete-label">{{ ui("边界未覆盖", "Boundary outside route") }}</text>
                        <text :x="cell.x + cell.width / 2" :y="timeBottom - 8" text-anchor="middle" class="incomplete-label">{{ cell.unknownExitBoundary ? ui("出清未知", "Clearance unknown") : ui("未出清", "Not cleared") }}</text>
                    </template>
                    <title>{{ cellTitle(cell) }}</title>
                </g>
                <g v-for="joint in joints" :key="joint.id">
                    <line :x1="x(joint.positionM)" :x2="x(joint.positionM)" :y1="timeTop" :y2="timeBottom" class="joint-guide" />
                    <line :x1="x(joint.positionM)" :x2="x(joint.positionM)" :y1="timeY(joint.headTimeS!)" :y2="timeY(joint.tailTimeS ?? timeMax)" :class="['joint-occupied', { 'joint-incomplete': joint.tailTimeS === null }]" />
                    <circle :cx="x(joint.positionM)" :cy="timeY(joint.headTimeS!)" r="3" fill="#44e2e0" />
                    <circle v-if="joint.tailTimeS !== null" :cx="x(joint.positionM)" :cy="timeY(joint.tailTimeS)" r="3" fill="#c4a3ff" />
                    <title>{{ joint.label }} · {{ distanceLabel(joint.positionM) }} m · {{ ui("车头", "Head") }} {{ timeLabel(joint.headTimeS) }} · {{ ui("车尾", "Tail") }} {{ timeLabel(joint.tailTimeS) }} · {{ ui("释放", "Release") }} {{ joint.releaseTimeS === null ? ui("未出清", "Not cleared") : timeLabel(joint.releaseTimeS) }}</title>
                </g>
                <text v-if="!result" :x="left + plotWidth / 2" :y="(timeTop + timeBottom) / 2" text-anchor="middle" class="empty-time-label">{{ ui("尚无计算结果", "No saved calculation") }}</text>
                <path :d="headPath" class="speed-curve" :clip-path="'url(#' + timeClipId + ')'" />
                <path :d="tailPath" class="tail-curve" :clip-path="'url(#' + timeClipId + ')'" />
                <g v-if="hoverPosition !== null && nearestSample">
                    <line :x1="x(hoverPosition)" :x2="x(hoverPosition)" :y1="timeTop" :y2="timeBottom" class="cursor-line" />
                    <circle v-if="nearestSample.headTimeS !== null" :cx="x(hoverPosition)" :cy="timeY(nearestSample.headTimeS)" r="5" class="speed-cursor" />
                    <circle v-if="nearestSample.tailTimeS !== null" :cx="x(hoverPosition)" :cy="timeY(nearestSample.tailTimeS)" r="5" class="tail-cursor" />
                </g>
            </svg>
        </section>
        <Teleport to="body">
            <div v-if="limitMenu" ref="limitMenuElement" class="limit-context-menu" role="menu" :style="{ left: limitMenu.x + 'px', top: limitMenu.y + 'px' }" @contextmenu.prevent>
                <button role="menuitem" :disabled="limitMenu.index <= firstEditableIndex" :title="limitMenu.index <= firstEditableIndex ? ui('进路起点保留，可拖动修改限速', 'The route origin is retained; drag to change its limit') : undefined" @click="removeLimitPoint">{{ ui('删除允许速度点', 'Delete speed limit point') }}</button>
            </div>
        </Teleport>
    </div>
</template>

<style scoped>
.traction-charts { display: flex; flex-direction: column; flex: 1 1 0; min-width: 0; min-height: 0; overflow: hidden; color: #dce8f7; }
.chart-panel { border: 1px solid #26354b; border-radius: 12px; background: #101a2a; box-shadow: 0 8px 24px #00000010; }
.chart-pane { box-sizing: border-box; min-width: 0; min-height: 0; overflow-x: hidden; overflow-y: auto; scrollbar-gutter: stable; overscroll-behavior-y: contain; }
.speed-pane { flex: 0 0 auto; }
.occupancy-pane { flex: 1 1 0; }
.chart-divider { --sy-border: #30455f; --el-color-primary: #44e2e0; flex: 0 0 8px; }
@supports not (scrollbar-gutter: stable) {
    .chart-pane { overflow-y: scroll; }
}
.chart-heading { display: flex; align-items: center; justify-content: space-between; gap: 16px; padding: 19px 22px 8px; }
.chart-heading h3 { margin: 0; color: #e8f1fd; font-size: 15px; font-weight: 650; letter-spacing: .02em; }
.chart-legend { display: flex; flex-wrap: wrap; justify-content: flex-end; gap: 10px 17px; font-size: 11px; color: #a8b9cf; }
.chart-legend > span { display: inline-flex; align-items: center; gap: 6px; white-space: nowrap; }
.limit-hit-target { fill: none; stroke: transparent; stroke-width: 16; cursor: pointer; }
.limit-segment-value { fill: #ffbf69; font: 600 11px system-ui, sans-serif; paint-order: stroke; stroke: #0c1523; stroke-width: 4px; stroke-linejoin: round; }
.limit-anchor { fill: #101a2a; stroke: #ffbf69; stroke-width: 2; cursor: grab; touch-action: none; }
.limit-anchor.selected, .limit-anchor:focus-visible { fill: #ffbf69; stroke: #fff0d6; outline: none; }
.limit-anchor:active { cursor: grabbing; }.limit-anchor.readonly { cursor: default; }
.editing-limit .limit-hit-target { cursor: crosshair; }
.limit-context-menu { position: fixed; z-index: 4000; min-width: 152px; padding: 4px; border: 1px solid #3f536e; border-radius: 6px; background: #17263b; box-shadow: 0 6px 20px #0005; }
.limit-context-menu button { display: block; width: 100%; padding: 8px 12px; border: 0; border-radius: 3px; background: transparent; color: #ffb4ab; text-align: left; cursor: pointer; font: 12px system-ui, sans-serif; }
.limit-context-menu button:hover, .limit-context-menu button:focus-visible { background: #304158; outline: none; }
.limit-context-menu button:disabled { color: #879ab2; cursor: default; background: transparent; }
.empty-time-label { fill: #879ab2; font: 12px system-ui, sans-serif; }
.legend-line { display: inline-block; width: 20px; height: 0; border-top: 2px solid; }
.speed-line { border-color: #44e2e0; }
.limit-line { border-color: #ffbf69; border-top-style: dashed; }
.tail-line { border-color: #c4a3ff; }
.legend-device { width: 7px; height: 7px; background: #6ee7a8; transform: rotate(45deg); }
.legend-route-setting { width: 12px; height: 10px; background: #4ade8033; border: 1px solid #4ade80b3; }
.legend-reaction { width: 12px; height: 10px; background: #9ca3af44; border: 1px solid #9ca3afb3; }
.legend-approaching { width: 12px; height: 10px; background: #facc1533; border: 1px solid #facc15b3; }
.legend-release { width: 12px; height: 10px; background: #f8717133; border: 1px solid #f87171b3; }
.legend-block { width: 12px; height: 10px; background: #4db6a124; border: 1px solid #4db6a179; }
.legend-circuit { width: 16px; height: 6px; background: #6ddac933; border: 1px solid #6ddac9; }
.legend-joint { width: 4px; height: 12px; background: #c4a3ffb3; border-radius: 2px; }
.plot { display: block; width: 100%; height: auto; outline: none; touch-action: pan-y; }
.plot:focus-visible { box-shadow: inset 0 0 0 2px #44e2e0; }
.plot-background { fill: #0c1523; }
.grid-line { stroke: #26354a; stroke-width: .75; }
.axis text, .axis-unit { fill: #879ab2; font: 11px system-ui, sans-serif; }
.axis-unit { font-size: 10px; }
.circuit-range { fill: currentColor; fill-opacity: .18; stroke: currentColor; stroke-opacity: .7; stroke-width: .8; }
.circuit-range-edge { fill: none; stroke: currentColor; stroke-width: 1; }
.circuit-boundary-guide { stroke: currentColor; stroke-width: .7; stroke-dasharray: 2 3; opacity: .3; }
.circuit-name-guide { fill: none; stroke: currentColor; stroke-width: .8; opacity: .65; }
.circuit-name { fill: currentColor; font: 11px system-ui, sans-serif; }
.circuit-active .circuit-range { fill-opacity: .4; stroke-opacity: 1; }
.circuit-active .circuit-name { fill: #edfaff; font-weight: 600; }
.device-guide { stroke-width: .8; stroke-dasharray: 3 5; opacity: .28; }
.device-label { font: 10px system-ui, sans-serif; }
.speed-curve, .limit-curve, .tail-curve { fill: none; stroke-linejoin: round; stroke-linecap: round; vector-effect: non-scaling-stroke; }
.speed-curve { stroke: #44e2e0; stroke-width: 2.4; }
.limit-curve { stroke: #ffbf69; stroke-width: 1.7; stroke-dasharray: 7 5; }
.tail-curve { stroke: #c4a3ff; stroke-width: 2.1; }
.cursor-line { stroke: #ecf5ff; stroke-width: 1; stroke-dasharray: 3 4; opacity: .7; }
.speed-cursor { fill: #44e2e0; stroke: #0c1523; stroke-width: 2; }
.tail-cursor { fill: #c4a3ff; stroke: #0c1523; stroke-width: 2; }
.cursor-label-bg { fill: #1b3045; stroke: #3f637a; stroke-width: .8; }
.cursor-label { fill: #bafbf7; font: 12px system-ui, sans-serif; font-weight: 650; }
.chart-inspector { position: sticky; bottom: 0; z-index: 1; background: #101a2a; display: flex; align-items: center; flex-wrap: nowrap; height: 42px; min-height: 42px; max-height: 42px; box-sizing: border-box; gap: 18px; padding: 0 20px; overflow-x: auto; overflow-y: hidden; scrollbar-width: thin; border-top: 1px solid #26354b; color: #95a8bf; font-size: 11px; font-variant-numeric: tabular-nums; }
.chart-inspector > span { flex: 0 0 auto; white-space: nowrap; }
.chart-inspector b { color: #e1edfb; font-weight: 600; }
.chart-inspector .cyan { color: #44e2e0; }
.chart-inspector .purple { color: #c4a3ff; }
.inspector-cell { margin-left: auto; }
.route-setting-cell { fill: #4ade8033; stroke: #4ade80b3; stroke-width: .8; }
.reaction-cell { fill: #9ca3af44; stroke: #9ca3afb3; stroke-width: .8; }
.approaching-cell { fill: #facc1533; stroke: #facc15b3; stroke-width: .8; }
.release-cell { fill: #f8717133; stroke: #f87171b3; stroke-width: .8; }
.occupancy-cell { fill: #4db6a11a; stroke: #4db6a164; stroke-width: .8; }
.occupancy-cell.incomplete { fill: #ffbf690b; stroke: #ffbf6960; }
.cell-label { fill: #9abcb9; font: 10px system-ui, sans-serif; }
.incomplete-label { fill: #d9b17b; font: 10px system-ui, sans-serif; }
.unknown-entry-label { fill: #9dbacd; font: 10px system-ui, sans-serif; }
.joint-guide { stroke: #bda2d3; stroke-width: .8; stroke-dasharray: 2 5; opacity: .3; }
.joint-occupied { stroke: #c4a3ff; stroke-width: 5; opacity: .6; }
.joint-incomplete { stroke: #ffbf69; stroke-dasharray: 5 3; opacity: .7; }
@media (max-width: 1100px) {
    .chart-heading { align-items: flex-start; padding: 16px 15px 8px; }
    .chart-legend { max-width: 50%; gap: 8px 12px; }
}
@media (max-width: 680px) {
    .chart-heading { flex-direction: column; gap: 13px; }
    .chart-legend { max-width: none; justify-content: flex-start; }
    .chart-inspector { gap: 12px; padding-inline: 12px; }
    .inspector-cell { margin-left: 0; }
}
</style>