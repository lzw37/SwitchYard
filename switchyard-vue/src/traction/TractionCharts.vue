<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, useId } from "vue";
import { useI18n } from "vue-i18n";
import PaneDivider from "../components/ui/PaneDivider.vue";
import {
    headTimeAt,
    type TractionResult,
    type TractionRouteProfile,
} from "./tractionCalculation";

const props = defineProps<{
    profile: TractionRouteProfile;
    result: TractionResult;
    trainLengthM: number;
}>();
const { locale } = useI18n();
const ui = (zh: string, en: string) => locale.value.startsWith("zh") ? zh : en;
const hoverPosition = ref<number | null>(null);
const chartId = useId();
const gradientId = chartId + '-speed-fill';
const patternId = chartId + '-uncleared';

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
const speedBottom = computed(() => speedHeight.value - 84);
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
    speedHeight.value = Math.round(Math.max(240,
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
    scheduleMeasure();
});
onBeforeUnmount(() => {
    resizeObserver?.disconnect();
    cancelAnimationFrame(resizeFrame);
    window.removeEventListener("resize", scheduleMeasure);
});
const length = computed(() => props.profile.lengthM > 0 ? props.profile.lengthM : 1);
const x = (position: number) => left + position / length.value * plotWidth.value;
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
        Math.max(20, props.result.maxSpeedKmh),
    );
    return props.result.samples.reduce(
        (maximum, sample) => Math.max(maximum, sample.limitKmh), segmentMaximum,
    );
});
const speedStep = computed(() => niceStep(speedExtent.value));
const speedMax = computed(() => Math.ceil(speedExtent.value / speedStep.value) * speedStep.value);
const speedY = (speed: number) => speedBottom.value - speed / speedMax.value * (speedBottom.value - speedTop);
const timeExtent = computed(() => Math.max(
    1,
    props.result.totalTimeS,
    headTimeAt(props.result, length.value + props.trainLengthM) ?? 0,
    ...props.result.jointTimes.map(joint => joint.releaseTimeS ?? joint.tailTimeS ?? joint.headTimeS ?? 0),
    ...props.result.cellOccupancies.map(cell => cell.releaseTimeS ?? cell.exitTimeS ?? cell.entryTimeS ?? 0),
));
const timeStep = computed(() => niceStep(timeExtent.value));
const timeMax = computed(() => Math.ceil(timeExtent.value / timeStep.value) * timeStep.value);
const timeY = (time: number) => timeTop + time / timeMax.value * (timeBottom.value - timeTop);
const ticks = (maximum: number, step: number) => Array.from(
    { length: Math.floor(maximum / step) + 1 },
    (_, index) => Math.round(index * step * 1e6) / 1e6,
);
const speedTicks = computed(() => ticks(speedMax.value, speedStep.value));
const timeTicks = computed(() => ticks(timeMax.value, timeStep.value));
const positionTicks = computed(() => {
    const values = ticks(length.value, niceStep(length.value, 6));
    if ((values[values.length - 1] ?? 0) < length.value * 0.92) values.push(length.value);
    return values;
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
const speedPath = computed(() => pathFrom(props.result.samples.map(sample => ({
    x: x(sample.positionM), y: speedY(sample.speedKmh),
}))));
const speedArea = computed(() => {
    const first = props.result.samples[0];
    const last = props.result.samples[props.result.samples.length - 1];
    return first && last
        ? speedPath.value + " L" + x(last.positionM) + "," + speedBottom.value +
            " L" + x(first.positionM) + "," + speedBottom.value + " Z"
        : "";
});
const limitPath = computed(() => {
    const samples = props.result.samples;
    if (!samples.length) return "";
    const points: Array<{ x: number; y: number }> = [];
    samples.forEach((sample, index) => {
        const previous = samples[index - 1];
        if (previous && previous.limitKmh !== sample.limitKmh) {
            points.push({ x: x(sample.positionM), y: speedY(previous.limitKmh) });
        }
        points.push({ x: x(sample.positionM), y: speedY(sample.limitKmh) });
    });
    return pathFrom(points);
});
const headPath = computed(() => pathFrom(props.result.samples.map(sample => ({
    x: x(sample.positionM), y: timeY(sample.timeS),
}))));
const tailPoints = computed(() => {
    // Shift the server's head samples by train length for display. Keep both
    // samples at a stop so the tail line includes the vertical dwell segment.
    const points = props.result.samples
        .filter(sample => sample.positionM >= props.trainLengthM && sample.positionM <= length.value + props.trainLengthM)
        .map(sample => ({ positionM: sample.positionM - props.trainLengthM, timeS: sample.timeS as number | null }));
    if (!points.length || points[0]!.positionM > 0) {
        points.unshift({ positionM: 0, timeS: headTimeAt(props.result, props.trainLengthM) });
    }
    if (points[points.length - 1]!.positionM < length.value) {
        points.push({ positionM: length.value, timeS: headTimeAt(props.result, length.value + props.trainLengthM) });
    }
    return points.map(point => ({ x: x(point.positionM), y: point.timeS === null ? null : timeY(point.timeS) }));
});
const tailPath = computed(() => pathFrom(tailPoints.value));
const cells = computed(() => props.result.cellOccupancies.filter(cell =>
    cell.endM >= 0 && cell.startM <= length.value,
).map(cell => {
    const unknownEntry = cell.entryBoundaryKnown === false || !finite(cell.entryTimeS);
    const unknownExitBoundary = cell.exitBoundaryKnown === false;
    // An unknown entry continues from above the visible time window. It is
    // labelled explicitly rather than assigning a fictitious zero entry time.
    const start = unknownEntry ? 0 : cell.entryTimeS!;
    const end = unknownExitBoundary ? timeMax.value : cell.releaseTimeS ?? timeMax.value;
    return {
        ...cell,
        x: x(Math.max(0, cell.startM)),
        width: Math.max(0, x(Math.min(length.value, cell.endM)) - x(Math.max(0, cell.startM))),
        y: timeY(start),
        height: Math.max(0, timeY(end) - timeY(start)),
        incomplete: unknownExitBoundary || !finite(cell.releaseTimeS),
        unknownEntry,
        unknownExitBoundary,
    };
}));
const joints = computed(() => props.result.jointTimes.filter(joint =>
    joint.positionM >= 0 && joint.positionM <= length.value && finite(joint.headTimeS),
));
const devices = computed(() => {
    const groups = new Map<string, NonNullable<TractionRouteProfile["devices"]>>();
    for (const device of props.profile.devices ?? []) {
        if (device.positionM < 0 || device.positionM > length.value) continue;
        const key = device.positionM.toFixed(6);
        const group = groups.get(key);
        if (group) group.push(device);
        else groups.set(key, [device]);
    }
    return [...groups.values()].map(group => ({
        ...group[0]!,
        id: group.map(device => device.id).join("|"),
        label: group.map(device => device.label).join(" · "),
    })).sort((a, b) => a.positionM - b.positionM);
});
const deviceColor = (type: string) => {
    const normalized = type.toLowerCase();
    if (/signal|信号/.test(normalized)) return "#6ee7a8";
    if (/switch|turnout|道岔/.test(normalized)) return "#ffbf69";
    if (/curve|曲线/.test(normalized)) return "#c4a3ff";
    if (/joint|insulat|绝缘/.test(normalized)) return "#f4a8d9";
    return "#90a9c5";
};
const deviceLabel = (label: string) => label.length > 14 ? label.slice(0, 13) + "…" : label;
const nearestSample = computed(() => {
    const position = hoverPosition.value;
    const samples = props.result.samples;
    if (position === null || !samples.length) return null;
    let low = 0;
    let high = samples.length - 1;
    while (low < high) {
        const middle = Math.floor((low + high) / 2);
        if (samples[middle]!.positionM < position) low = middle + 1;
        else high = middle;
    }
    const after = samples[low]!;
    const before = samples[Math.max(0, low - 1)]!;
    const span = after.positionM - before.positionM;
    const fraction = span > 0 ? Math.min(1, Math.max(0, (position - before.positionM) / span)) : 0;
    return {
        speedKmh: Math.sqrt(Math.max(0,
            before.speedKmh ** 2 + fraction * (after.speedKmh ** 2 - before.speedKmh ** 2))),
        limitKmh: position < after.positionM ? before.limitKmh : after.limitKmh,
        headTimeS: headTimeAt(props.result, position),
        tailTimeS: headTimeAt(props.result, position + props.trainLengthM),
    };
});
const selectedCell = computed(() => hoverPosition.value === null ? null :
    props.result.cellOccupancies.find(cell =>
        hoverPosition.value! >= cell.startM && hoverPosition.value! <= cell.endM) ?? null,
);
const handlePointer = (event: PointerEvent) => {
    const svg = event.currentTarget as SVGSVGElement;
    const matrix = svg.getScreenCTM();
    if (!matrix) return;
    const point = new DOMPoint(event.clientX, event.clientY).matrixTransform(matrix.inverse());
    hoverPosition.value = Math.max(0, Math.min(length.value, (point.x - left) / plotWidth.value * length.value));
};
const handleKey = (event: KeyboardEvent) => {
    if (event.key === "Escape") {
        hoverPosition.value = null;
        return;
    }
    if (!["ArrowLeft", "ArrowRight", "Home", "End"].includes(event.key)) return;
    event.preventDefault();
    const direction = event.key === "ArrowRight" ? 1 : -1;
    hoverPosition.value = event.key === "Home" ? 0 : event.key === "End" ? length.value :
        Math.max(0, Math.min(length.value, (hoverPosition.value ?? 0) + direction * length.value / 100));
};
const cellTitle = (cell: (typeof props.result.cellOccupancies)[number]) => {
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
    <div ref="chartRoot" class="traction-charts">
        <section ref="speedPane" class="chart-panel chart-pane speed-pane" :style="{ flexBasis: speedPaneHeight + 'px' }" @pointerleave="hoverPosition = null">
            <header class="chart-heading">
                <div>
                    <h3>{{ ui("速度–位置曲线", "Speed–position profile") }}</h3>
                </div>
                <div class="chart-legend">
                    <span><i class="legend-line speed-line"></i>{{ ui("列车速度", "Train speed") }}</span>
                    <span><i class="legend-line limit-line"></i>{{ ui("允许速度", "Speed limit") }}</span>
                    <span><i class="legend-device"></i>{{ ui("关键设备", "Key devices") }}</span>
                </div>
            </header>
            <svg
                class="plot speed-svg"
                ref="speedSvg" :viewBox="'0 0 ' + width + ' ' + speedHeight" :style="{ height: speedHeight + 'px' }"
                role="img"
                tabindex="0"
                :aria-label="ui('速度位置图，左右方向键可查看沿线数值', 'Speed by position. Use arrow keys to inspect the route.')"
                @pointermove="handlePointer"
                @pointerdown="handlePointer"
                @keydown="handleKey"
                @blur="hoverPosition = null"
            >
                <defs>
                    <linearGradient :id="gradientId" x1="0" y1="0" x2="0" y2="1">
                        <stop offset="0%" stop-color="#44e2e0" stop-opacity="0.19" />
                        <stop offset="100%" stop-color="#44e2e0" stop-opacity="0.015" />
                    </linearGradient>
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
                <g v-for="(device, index) in devices" :key="device.id">
                    <line :x1="x(device.positionM)" :x2="x(device.positionM)" :y1="speedTop" :y2="speedBottom" :stroke="deviceColor(device.type)" class="device-guide" />
                    <path
                        :d="'M' + x(device.positionM) + ',' + (speedBottom + 28) + ' l-4,7 h8 Z'"
                        :fill="deviceColor(device.type)"
                    />
                    <text
                        :x="x(device.positionM)"
                        :y="speedBottom + (index % 2 === 0 ? 45 : 62)"
                        :fill="deviceColor(device.type)"
                        class="device-label"
                        :text-anchor="device.positionM < length * 0.08 ? 'start' : device.positionM > length * 0.92 ? 'end' : 'middle'"
                    >{{ deviceLabel(device.label) }}</text>
                    <title>{{ device.label }} · {{ distanceLabel(device.positionM) }} m</title>
                </g>
                <path :d="speedArea" :fill="'url(#' + gradientId + ')'" />
                <path :d="limitPath" class="limit-curve" />
                <path :d="speedPath" class="speed-curve" />
                <g v-if="hoverPosition !== null && nearestSample">
                    <line :x1="x(hoverPosition)" :x2="x(hoverPosition)" :y1="speedTop" :y2="speedBottom" class="cursor-line" />
                    <circle :cx="x(hoverPosition)" :cy="speedY(nearestSample.speedKmh)" r="5" class="speed-cursor" />
                    <rect :x="Math.min(width - 152, Math.max(left, x(hoverPosition) - 57))" y="30" width="120" height="26" rx="6" class="cursor-label-bg" />
                    <text :x="Math.min(width - 152, Math.max(left, x(hoverPosition) - 57)) + 60" y="47" text-anchor="middle" class="cursor-label">{{ numberLabel(nearestSample.speedKmh) }} km/h</text>
                </g>
            </svg>

            <div class="chart-inspector" role="status" aria-live="polite">
                <span><b>{{ hoverPosition === null ? "—" : distanceLabel(hoverPosition) }}</b> m</span>
                <span>{{ ui("速度", "Speed") }} <b class="cyan">{{ nearestSample ? numberLabel(nearestSample.speedKmh) : "—" }}</b> / {{ nearestSample ? numberLabel(nearestSample.limitKmh) : "—" }} km/h</span>
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
                    <span><i class="legend-block"></i>{{ ui("区段占用至释放", "Section until release") }}</span>
                    <span><i class="legend-joint"></i>{{ ui("绝缘占用", "Joint occupancy") }}</span>
                </div>
            </header>
            <svg
                class="plot time-svg"
                ref="timeSvg" :viewBox="'0 0 ' + width + ' ' + timeHeight" :style="{ height: timeHeight + 'px' }"
                role="img"
                tabindex="0"
                :aria-label="ui('位置时间图，显示车头车尾时刻和区段占用', 'Position–time diagram showing head, tail and section occupation.')"
                @pointermove="handlePointer"
                @pointerdown="handlePointer"
                @keydown="handleKey"
                @blur="hoverPosition = null"
            >
                <defs>
                    <pattern :id="patternId" width="9" height="9" patternUnits="userSpaceOnUse" patternTransform="rotate(45)">
                        <line x1="0" y1="0" x2="0" y2="9" stroke="#fbbf69" stroke-opacity=".19" stroke-width="2" />
                    </pattern>
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
                <g v-for="cell in cells" :key="cell.id">
                    <rect :x="cell.x" :y="cell.y" :width="cell.width" :height="cell.height" :class="['occupancy-cell', { incomplete: cell.incomplete }]" />
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
                <path :d="headPath" class="speed-curve" />
                <path :d="tailPath" class="tail-curve" />
                <g v-if="hoverPosition !== null && nearestSample">
                    <line :x1="x(hoverPosition)" :x2="x(hoverPosition)" :y1="timeTop" :y2="timeBottom" class="cursor-line" />
                    <circle v-if="nearestSample.headTimeS !== null" :cx="x(hoverPosition)" :cy="timeY(nearestSample.headTimeS)" r="5" class="speed-cursor" />
                    <circle v-if="nearestSample.tailTimeS !== null" :cx="x(hoverPosition)" :cy="timeY(nearestSample.tailTimeS)" r="5" class="tail-cursor" />
                </g>
            </svg>
        </section>
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
.legend-line { display: inline-block; width: 20px; height: 0; border-top: 2px solid; }
.speed-line { border-color: #44e2e0; }
.limit-line { border-color: #ffbf69; border-top-style: dashed; }
.tail-line { border-color: #c4a3ff; }
.legend-device { width: 7px; height: 7px; background: #6ee7a8; transform: rotate(45deg); }
.legend-block { width: 12px; height: 10px; background: #4db6a124; border: 1px solid #4db6a179; }
.legend-joint { width: 4px; height: 12px; background: #c4a3ffb3; border-radius: 2px; }
.plot { display: block; width: 100%; height: auto; outline: none; touch-action: pan-y; }
.plot:focus-visible { box-shadow: inset 0 0 0 2px #44e2e0; }
.plot-background { fill: #0c1523; }
.grid-line { stroke: #26354a; stroke-width: .75; }
.axis text, .axis-unit { fill: #879ab2; font: 11px system-ui, sans-serif; }
.axis-unit { font-size: 10px; }
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
.chart-inspector { display: flex; align-items: center; flex-wrap: nowrap; height: 42px; min-height: 42px; max-height: 42px; box-sizing: border-box; gap: 18px; padding: 0 20px; overflow-x: auto; overflow-y: hidden; scrollbar-width: thin; border-top: 1px solid #26354b; color: #95a8bf; font-size: 11px; font-variant-numeric: tabular-nums; }
.chart-inspector > span { flex: 0 0 auto; white-space: nowrap; }
.chart-inspector b { color: #e1edfb; font-weight: 600; }
.chart-inspector .cyan { color: #44e2e0; }
.chart-inspector .purple { color: #c4a3ff; }
.inspector-cell { margin-left: auto; }
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