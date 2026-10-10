import type { Point } from "./types";
import type { Arc, Line, Segment } from "./filletGeometry";
import { distance } from "./filletGeometry";

export const MAX_CROSSINGS = 1000;
export interface CrossingPoint extends Point {
  pathIndices: number[];
}
const EPS = 1e-7;
export const sameCrossingPoint = (a: Point, b: Point) => distance(a, b) <= 1e-4;
const cross = (a: Point, b: Point) => a.x * b.y - a.y * b.x;
const subtract = (a: Point, b: Point) => ({ x: a.x - b.x, y: a.y - b.y });
function onArc(p: Point, arc: Arc, interior = false) {
  const angle = Math.atan2(p.y - arc.center.y, p.x - arc.center.x);
  let turn =
    (Math.sign(arc.sweep) * (angle - arc.startAngle) + Math.PI * 4) %
    (Math.PI * 2);
  if (Math.PI * 2 - turn < EPS) turn = 0;
  return interior
    ? turn > EPS && turn < Math.abs(arc.sweep) - EPS
    : turn <= Math.abs(arc.sweep) + EPS;
}
function lineLine(a: Line, b: Line): Point[] | null {
  const r = subtract(a.end, a.start),
    s = subtract(b.end, b.start),
    q = subtract(b.start, a.start);
  const denominator = cross(r, s);
  if (Math.abs(denominator) <= EPS * a.length * b.length) {
    if (Math.abs(cross(q, r)) > EPS * a.length) return [];
    const dot = (p: Point) => (p.x * r.x + p.y * r.y) / (a.length * a.length);
    const t0 = dot(q),
      t1 = dot(subtract(b.end, a.start));
    const lo = Math.max(0, Math.min(t0, t1)),
      hi = Math.min(1, Math.max(t0, t1));
    // Overlapping spans have infinitely many points, not a discrete crossing.
    if (hi < lo - EPS) return [];
    if ((hi - lo) * a.length > EPS) return null;
    return [{ x: a.start.x + lo * r.x, y: a.start.y + lo * r.y }];
  }
  const t = cross(q, s) / denominator,
    u = cross(q, r) / denominator;
  if (t < -EPS || t > 1 + EPS || u < -EPS || u > 1 + EPS) return [];
  return [{ x: a.start.x + t * r.x, y: a.start.y + t * r.y }];
}
function lineArc(line: Line, arc: Arc): Point[] {
  const dx = (line.end.x - line.start.x) / line.length,
    dy = (line.end.y - line.start.y) / line.length;
  const q = subtract(arc.center, line.start),
    along = q.x * dx + q.y * dy;
  const perpendicular = q.x * dy - q.y * dx;
  const square = arc.radius * arc.radius - perpendicular * perpendicular;
  if (square < -EPS) return [];
  const offset = Math.sqrt(Math.max(0, square));
  return [along - offset, along + offset]
    .filter((t) => t >= -EPS && t <= line.length + EPS)
    .map((t) => ({ x: line.start.x + dx * t, y: line.start.y + dy * t }))
    .filter((p) => onArc(p, arc));
}
function arcArc(a: Arc, b: Arc): Point[] | null {
  const d = distance(a.center, b.center);
  if (d < EPS) {
    if (Math.abs(a.radius - b.radius) > EPS) return [];
    const midpoint = (arc: Arc) => ({
      x: arc.center.x + arc.radius * Math.cos(arc.startAngle + arc.sweep / 2),
      y: arc.center.y + arc.radius * Math.sin(arc.startAngle + arc.sweep / 2),
    });
    if (
      [a.start, a.end, midpoint(a)].some((p) => onArc(p, b, true)) ||
      [b.start, b.end, midpoint(b)].some((p) => onArc(p, a, true))
    )
      return null;
    return [a.start, a.end].filter((p) => onArc(p, b));
  }
  if (d > a.radius + b.radius + EPS || d < Math.abs(a.radius - b.radius) - EPS)
    return [];
  const along = (a.radius * a.radius - b.radius * b.radius + d * d) / (2 * d);
  const square = a.radius * a.radius - along * along;
  if (square < -EPS) return [];
  const h = Math.sqrt(Math.max(0, square));
  const dx = (b.center.x - a.center.x) / d,
    dy = (b.center.y - a.center.y) / d;
  return [-1, 1]
    .map((sign) => ({
      x: a.center.x + along * dx - sign * h * dy,
      y: a.center.y + along * dy + sign * h * dx,
    }))
    .filter((p) => onArc(p, a) && onArc(p, b));
}
function intersections(a: Segment, b: Segment) {
  if (a.kind === "line")
    return b.kind === "line" ? lineLine(a, b) : lineArc(a, b);
  return b.kind === "line" ? lineArc(b, a) : arcArc(a, b);
}
function bounds(segment: Segment) {
  const points = [segment.start, segment.end];
  if (segment.kind === "arc")
    for (const angle of [0, Math.PI / 2, Math.PI, (3 * Math.PI) / 2]) {
      const p = {
        x: segment.center.x + segment.radius * Math.cos(angle),
        y: segment.center.y + segment.radius * Math.sin(angle),
      };
      if (onArc(p, segment)) points.push(p);
    }
  return {
    minX: Math.min(...points.map((p) => p.x)),
    maxX: Math.max(...points.map((p) => p.x)),
    minY: Math.min(...points.map((p) => p.y)),
    maxY: Math.max(...points.map((p) => p.y)),
  };
}

// Called only by the explicit Generate action. No annotation depends on live geometry.
export function findRouteCrossings(
  paths: { segments: Segment[] }[],
): CrossingPoint[] | undefined {
  const entries = paths
    .flatMap((path, pathIndex) =>
      path.segments.map((segment) => ({
        pathIndex,
        segment,
        ...bounds(segment),
      })),
    )
    .sort((a, b) => a.minX - b.minX);
  let active: typeof entries = [];
  const candidates: { point: Point; pairs: Set<string> }[] = [];
  const buckets = new Map<string, typeof candidates>();
  const overlaps = new Map<string, [Segment, Segment][]>();
  const onSegment = (p: Point, s: Segment) => {
    if (s.kind === "arc")
      return Math.abs(distance(p, s.center) - s.radius) < 1e-4 && onArc(p, s);
    const r = subtract(s.end, s.start),
      q = subtract(p, s.start);
    const projection = (q.x * r.x + q.y * r.y) / s.length;
    return (
      Math.abs(cross(q, r)) / s.length < 1e-4 &&
      projection >= -EPS &&
      projection <= s.length + EPS
    );
  };
  const atTerminal = (p: Point, index: number) => {
    const segments = paths[index]!.segments;
    return (
      sameCrossingPoint(p, segments[0]!.start) ||
      sameCrossingPoint(p, segments[segments.length - 1]!.end)
    );
  };
  for (const entry of entries) {
    active = active.filter((a) => a.maxX >= entry.minX - EPS);
    for (const other of active) {
      if (
        entry.pathIndex === other.pathIndex ||
        other.maxY < entry.minY - EPS ||
        other.minY > entry.maxY + EPS
      )
        continue;
      const pair = [entry.pathIndex, other.pathIndex]
        .sort((a, b) => a - b)
        .join(":");
      const hits = intersections(entry.segment, other.segment);
      if (hits === null) {
        const spans = overlaps.get(pair) ?? [];
        spans.push([entry.segment, other.segment]);
        overlaps.set(pair, spans);
        continue;
      }
      for (const p of hits) {
        // A common route endpoint belongs to a facility rather than a crossing.
        if (atTerminal(p, entry.pathIndex) && atTerminal(p, other.pathIndex))
          continue;
        if (!Number.isFinite(p.x) || !Number.isFinite(p.y)) continue;
        const point = p;
        const x = Math.floor(point.x / 1e-4),
          y = Math.floor(point.y / 1e-4);
        let existing: (typeof candidates)[number] | undefined;
        for (let dx = -1; dx <= 1 && !existing; dx++)
          for (let dy = -1; dy <= 1 && !existing; dy++)
            existing = buckets
              .get(`${x + dx}:${y + dy}`)
              ?.find((c) => sameCrossingPoint(c.point, point));
        if (existing) existing.pairs.add(pair);
        else {
          const candidate = { point, pairs: new Set([pair]) };
          candidates.push(candidate);
          const key = `${x}:${y}`,
            bucket = buckets.get(key) ?? [];
          bucket.push(candidate);
          buckets.set(key, bucket);
        }
      }
    }
    active.push(entry);
  }
  // Suppress artificial hits where adjacent segments meet on overlapping
  // routes, but retain a third route crossing that same location.
  const points = candidates
    .map((c) => {
      const pairs = [...c.pairs].filter(
        (pair) =>
          !overlaps
            .get(pair)
            ?.some(([a, b]) => onSegment(c.point, a) && onSegment(c.point, b)),
      );
      // Keep only routes from actual crossing pairs, excluding overlapping spans.
      const pathIndices = [
        ...new Set(pairs.flatMap((pair) => pair.split(":").map(Number))),
      ].sort((a, b) => a - b);
      return { ...c.point, pathIndices };
    })
    .filter((c) => c.pathIndices.length > 0);
  return points.length > MAX_CROSSINGS
    ? undefined
    : points.sort((a, b) => a.x - b.x || a.y - b.y);
}
