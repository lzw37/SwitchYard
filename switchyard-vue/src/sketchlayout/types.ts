export interface Point {
  x: number;
  y: number;
}
export interface Facility {
  id: string;
  name: string;
  kind: "polygon" | "circle";
  vertices: Point[];
  center: Point;
  radius: number;
  fillColor?: string | null;
  strokeColor?: string | null;
}
export interface Port {
  facilityId: string;
  kind: "center" | "vertex" | "midpoint";
  index: number;
}
export type LinkDirection = "forward" | "reverse" | "both";
export interface LogisticsLink {
  id: string;
  name: string;
  from: Port;
  to: Port;
  anchors: Point[];
  direction?: LinkDirection;
  color?: string | null;
}
export interface ProcessStep {
  facilityId: string;
  // Occupation inside this facility; independent of travel on the incoming link.
  dwellSeconds: number;
  // Stored with the destination for compatibility, displayed as a separate preceding operation.
  incomingLinkId: string | null;
  travelSeconds: number;
}
export interface ProcessChain {
  id: string;
  name: string;
  // Ordered visits; steps have per-job durations but no independent quantity.
  steps: ProcessStep[];
}
export interface ProcessPlan {
  id: string;
  name: string;
  // One quantity shared by all parallel chains and their dwell/travel operations.
  quantity: number;
  // Peer chains may run concurrently in the shared analysis window.
  // Omitted by older documents, which have the same parallel semantics.
  executionMode?: "parallel";
  // Legacy fields only; analysisSettings on the layout now owns these values.
  horizonSeconds?: number | null;
  aThreshold?: number | null;
  eThreshold?: number | null;
  iThreshold?: number | null;
  chains: ProcessChain[];
}
export interface AnalysisSettings {
  horizonSeconds: number;
  aThreshold: number;
  eThreshold: number;
  iThreshold: number;
  crossingModel?: CrossingProbabilityModel;
}
export type CrossingProbabilityModel =
  | "poisson-fixed"
  | "uniform-fixed"
  | "poisson-exponential";
export interface GridSettings {
  showGrid: boolean;
  snapToGrid: boolean;
  spacing: number;
  originX: number;
  originY: number;
}
export interface RouteCrossing {
  id: string;
  name: string;
  center: Point;
  radius: number;
  sourceLinks?: CrossingSourceLink[];
}
export interface CrossingSourceLink {
  linkId: string;
  name: string;
  // Null/omitted uses the quantity-weighted travel time from all process plans.
  occupancySeconds?: number | null;
}
export interface SchemeDocument {
  name: string;
  description: string;
  facilities: Facility[];
  links: LogisticsLink[];
  plans: ProcessPlan[];
  analysisSettings?: AnalysisSettings | null;
  gridSettings?: GridSettings | null;
  showLinkNames?: boolean;
  showCurveArc?: boolean;
  showPolygonFillets?: boolean;
  crossings?: RouteCrossing[];
  showCrossings?: boolean;
}
export interface SchemeSummary {
  id: string;
  ownerId: string;
  ownerName: string;
  name: string;
  description: string;
  revision: number;
  createdAt: string;
  updatedAt: string;
}
export interface SchemeDetail {
  id: string;
  ownerId: string;
  ownerName: string;
  revision: number;
  createdAt: string;
  updatedAt: string;
  document: SchemeDocument;
}
export interface FlowRelation {
  fromId: string;
  toId: string;
  forward: number;
  reverse: number;
  total: number;
  intensity: number;
  grade: string;
}
export interface ConflictResource {
  kind: string;
  firstId: string;
  secondId: string;
  name: string;
}
export interface ProcessConflict {
  firstPlanId: string;
  firstChainId: string;
  secondPlanId: string;
  secondChainId: string;
  pairProbability: number;
  jobPairs: number;
  expectedConflictingPairs: number;
  resources: ConflictResource[];
}
export interface AnalysisResult {
  scope: "all-plans";
  planCount: number;
  chainCount: number;
  jobCount: number;
  horizonSeconds: number;
  model: string;
  relations: FlowRelation[];
  conflicts: ProcessConflict[];
  expectedConflictingPairs: number;
  warnings: string[];
  crossingRisks: CrossingRisk[];
}
export interface CrossingRouteRisk {
  linkId: string;
  name: string;
  missingLink: boolean;
  passages: number;
  ratePerHour: number;
  occupancySeconds: number | null;
  usesTravelTime: boolean;
  conflictProbability: number | null;
}
export interface CrossingRisk {
  crossingId: string;
  name: string;
  model: CrossingProbabilityModel;
  status:
    | "ready"
    | "missing-sources"
    | "missing-links"
    | "no-traffic"
    | "calculation-limit";
  passages: number;
  conflictProbability: number | null;
  routes: CrossingRouteRisk[];
  correlatedRoutes: boolean;
  queue?: CrossingQueueMetrics;
}
export interface CrossingQueueMetrics {
  model: "mg1-fcfs" | "finite-uniform-fcfs";
  status: "ready" | "unstable" | "no-traffic" | "missing-sources" | "missing-links" | "calculation-limit";
  loadRatio: number | null;
  meanDelaySeconds: number | null;
  meanQueueLength: number | null;
  simulationRuns: number;
  delayStandardErrorSeconds: number | null;
  queueLengthStandardError: number | null;
}
export const uid = () => crypto.randomUUID();
export const clone = <T>(value: T): T => JSON.parse(JSON.stringify(value));
export function normalizePlanQuantities(document: SchemeDocument) {
  for (const plan of document.plans) {
    const chains = plan.chains as (ProcessChain & { quantity?: number })[];
    // Old documents used per-chain counts. Preserve the first as the shared count.
    plan.quantity ??= chains[0]?.quantity ?? 1;
    for (const chain of chains) delete chain.quantity;
  }
}
export function resolveAnalysisSettings(
  document: SchemeDocument,
): AnalysisSettings {
  if (document.analysisSettings)
    return { crossingModel: "poisson-fixed", ...document.analysisSettings };
  const defaults: AnalysisSettings = {
    horizonSeconds: 3600,
    aThreshold: 0.8,
    eThreshold: 0.5,
    iThreshold: 0.2,
    crossingModel: "poisson-fixed",
  };
  const legacy = document.plans.map((p) => ({
    horizonSeconds: p.horizonSeconds ?? defaults.horizonSeconds,
    aThreshold: p.aThreshold ?? defaults.aThreshold,
    eThreshold: p.eThreshold ?? defaults.eThreshold,
    iThreshold: p.iThreshold ?? defaults.iThreshold,
    crossingModel: defaults.crossingModel,
  }));
  const first = legacy[0];
  return first &&
    legacy.every((s) =>
      (Object.keys(defaults) as (keyof AnalysisSettings)[]).every(
        (key) => s[key] === first[key],
      ),
    )
    ? first
    : defaults;
}
