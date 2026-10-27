import type { Point } from "./types";

export const EPSILON = 1e-7;
// StationLayout uses tangent circular arcs with a default radius of 100.
const CORNER_RADIUS = 100;
const LINE_FIT_RATIO = 0.98;
export type Line = { kind: "line"; start: Point; end: Point; length: number };
export type Arc = {
  kind: "arc";
  start: Point;
  end: Point;
  center: Point;
  radius: number;
  startAngle: number;
  sweep: number;
  length: number;
};
export type Segment = Line | Arc;
export const distance = (a: Point, b: Point) =>
  Math.hypot(b.x - a.x, b.y - a.y);

export function filletCorner(
  a: Point,
  vertex: Point,
  b: Point,
  radius = CORNER_RADIUS,
) {
  const beforeLength = distance(vertex, a),
    afterLength = distance(vertex, b);
  const before = {
    x: (a.x - vertex.x) / beforeLength,
    y: (a.y - vertex.y) / beforeLength,
  };
  const after = {
    x: (b.x - vertex.x) / afterLength,
    y: (b.y - vertex.y) / afterLength,
  };
  const angle = Math.acos(
    Math.max(-1, Math.min(1, before.x * after.x + before.y * after.y)),
  );
  // Straight continuations and U-turns have no well-defined circular fillet.
  if (angle < 1e-4 || Math.PI - angle < 1e-4) return undefined;
  return {
    vertex,
    before,
    after,
    angle,
    trim: Math.min(
      radius / Math.tan(angle / 2),
      beforeLength * LINE_FIT_RATIO,
      afterLength * LINE_FIT_RATIO,
    ),
  };
}
export type FilletCorner = NonNullable<ReturnType<typeof filletCorner>>;
export function filletArc(c: FilletCorner): Arc {
  const radius = c.trim * Math.tan(c.angle / 2);
  const start = {
    x: c.vertex.x + c.before.x * c.trim,
    y: c.vertex.y + c.before.y * c.trim,
  };
  const end = {
    x: c.vertex.x + c.after.x * c.trim,
    y: c.vertex.y + c.after.y * c.trim,
  };
  const bx = c.before.x + c.after.x,
    by = c.before.y + c.after.y;
  const centerDistance = radius / Math.sin(c.angle / 2);
  const center = {
    x: c.vertex.x + (bx / Math.hypot(bx, by)) * centerDistance,
    y: c.vertex.y + (by / Math.hypot(bx, by)) * centerDistance,
  };
  const sweep =
    (Math.PI - c.angle) *
    (c.before.x * c.after.y - c.before.y * c.after.x < 0 ? 1 : -1);
  return {
    kind: "arc",
    start,
    end,
    center,
    radius,
    startAngle: Math.atan2(start.y - center.y, start.x - center.x),
    sweep,
    length: radius * Math.abs(sweep),
  };
}
export function segmentCommand(s: Segment): string {
  return s.kind === "line"
    ? ` L ${s.end.x} ${s.end.y}`
    : ` A ${s.radius} ${s.radius} 0 0 ${s.sweep > 0 ? 1 : 0} ${s.end.x} ${s.end.y}`;
}
