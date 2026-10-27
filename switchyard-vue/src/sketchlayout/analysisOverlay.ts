import type { AnalysisResult, CrossingRisk, SchemeDocument } from "./types";

export const UNKNOWN_ANALYSIS_COLOR = "#94a3b8";
export const probabilityStops = [
  { value: 0, color: "#2563eb" },
  { value: 0.25, color: "#0f766e" },
  { value: 0.5, color: "#a16207" },
  { value: 0.75, color: "#c2410c" },
  { value: 1, color: "#991b1b" },
];
export const LOAD_COLOR_LIMIT = 0.75;
export const loadStops = [
  { value: 0, color: "#3b82f6" },
  { value: LOAD_COLOR_LIMIT * 0.25, color: "#0d9488" },
  { value: LOAD_COLOR_LIMIT * 0.5, color: "#ca8a04" },
  { value: LOAD_COLOR_LIMIT * 0.75, color: "#ea580c" },
  { value: LOAD_COLOR_LIMIT, color: "#e53935" },
];
export const loadGradient = `linear-gradient(to right, ${loadStops
  .map(({ value, color }) => `${color} ${value / LOAD_COLOR_LIMIT * 100}%`).join(", ")})`;

function spectrumColor(value: number, stops: typeof probabilityStops): string {
  const upper = stops.findIndex((stop) => stop.value >= value);
  if (upper === 0) return stops[0]!.color;
  const a = stops[upper - 1]!, b = stops[upper]!;
  const ratio = (value - a.value) / (b.value - a.value);
  return "#" + [1, 3, 5].map((offset) => {
    const start = parseInt(a.color.slice(offset, offset + 2), 16);
    const end = parseInt(b.color.slice(offset, offset + 2), 16);
    return Math.round(start + (end - start) * ratio).toString(16).padStart(2, "0");
  }).join("");
}

export function probabilityColor(value: number | null | undefined): string {
  return value == null || !Number.isFinite(value) || value < 0 || value > 1
    ? UNKNOWN_ANALYSIS_COLOR : spectrumColor(value, probabilityStops);
}

export function loadColor(value: number | null | undefined): string {
  return value == null || !Number.isFinite(value) || value < 0
    ? UNKNOWN_ANALYSIS_COLOR : spectrumColor(Math.min(value, LOAD_COLOR_LIMIT), loadStops);
}

export function crossingLoadRatio(risk: CrossingRisk | undefined) {
  const queue = risk?.queue, value = queue?.loadRatio;
  return queue && ["ready", "unstable", "no-traffic", "calculation-limit"].includes(queue.status)
    && value != null && Number.isFinite(value) && value >= 0 ? value : undefined;
}

export function crossingProbability(risk: CrossingRisk | undefined) {
  const value = risk?.conflictProbability;
  return risk?.status === "ready" && value != null && Number.isFinite(value)
    && value >= 0 && value <= 1 ? value : undefined;
}

const pairKey = (a: string, b: string) => JSON.stringify([a, b].sort());
export function linkRelationMap(document: SchemeDocument, result?: AnalysisResult) {
  const relations = new Map(result?.relations.map((r) => [pairKey(r.fromId, r.toId), r]));
  return new Map(document.links.map((link) => [
    link.id, relations.get(pairKey(link.from.facilityId, link.to.facilityId)),
  ]));
}
