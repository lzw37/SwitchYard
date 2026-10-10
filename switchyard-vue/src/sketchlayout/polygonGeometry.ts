import type { Point } from "./types";
import {
  distance,
  filletArc,
  filletCorner,
  segmentCommand,
} from "./filletGeometry";
import type { Segment } from "./filletGeometry";
import { nearestPolygonEdge, projectToSegment } from "./polygonEditing";

const POLYGON_CORNER_RADIUS = 150;

export function polygonGeometry(points: Point[], rounded = true) {
  const count = points.length;
  const area = points.reduce((sum, p, i) => {
    const next = points[(i + 1) % count]!;
    const origin = points[0]!;
    return sum + (p.x - origin.x) * (next.y - origin.y) - (next.x - origin.x) * (p.y - origin.y);
  }, 0);
  const corners = points.map((p, i) => {
    if (!rounded || count < 3) return undefined;
    const c = filletCorner(
      points[(i + count - 1) % count]!,
      p,
      points[(i + 1) % count]!,
      POLYGON_CORNER_RADIUS,
    );
    // Use the actual interior angle: reflex (concave) corners are not obtuse.
    if (
      !c ||
      c.angle < Math.PI / 2 - 1e-10 ||
      (c.before.x * c.after.y - c.before.y * c.after.x) * area >= 0
    )
      return undefined;
    return c;
  });
  // Fit both ends of every edge, including the closing edge. Simultaneous
  // scaling makes the result independent of winding and the first vertex.
  const scales = corners.map(() => 1);
  corners.forEach((a, i) => {
    const j = (i + 1) % count,
      b = corners[j];
    if (!a || !b) return;
    const scale = Math.min(
      1,
      (distance(points[i]!, points[j]!) * 0.98) / (a.trim + b.trim),
    );
    scales[i] = Math.min(scales[i]!, scale);
    scales[j] = Math.min(scales[j]!, scale);
  });
  const arcs = corners.map((c, i) => {
    if (!c) return undefined;
    c.trim *= scales[i]!;
    return filletArc(c);
  });
  const segments: Segment[] = [];
  for (let i = 0; i < count; i++) {
    const arc = arcs[i],
      next = (i + 1) % count;
    if (arc) segments.push(arc);
    const start = arc?.end ?? points[i]!,
      end = arcs[next]?.start ?? points[next]!;
    const length = distance(start, end);
    if (length > 1e-7) segments.push({ kind: "line", start, end, length });
  }
  const start = arcs[0]?.start ?? points[0];
  return {
    path: start
      ? `M ${start.x} ${start.y}` + segments.map(segmentCommand).join("") + " Z"
      : "",
    segments,
  };
}

// Hit-test the visible rounded edge, but insert vertices on its original
// polygon edge so existing port indices and polygon validation still apply.
export function polygonEdgeHit(points: Point[], p: Point, rounded: boolean) {
  const hit = nearestPolygonEdge(points, p);
  if (!hit || !rounded) return hit;
  const distances = polygonGeometry(points, true).segments.map((s) => {
    if (s.kind === "line") return projectToSegment(p, s.start, s.end).distance;
    const angle = Math.atan2(p.y - s.center.y, p.x - s.center.x);
    const turn =
      (Math.sign(s.sweep) * (angle - s.startAngle) + Math.PI * 4) %
      (Math.PI * 2);
    return turn <= Math.abs(s.sweep)
      ? Math.abs(distance(p, s.center) - s.radius)
      : Math.min(distance(p, s.start), distance(p, s.end));
  });
  return { ...hit, distance: Math.min(...distances) };
}
