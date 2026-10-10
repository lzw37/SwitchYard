import type { Point } from "./types";
import {
  EPSILON,
  distance,
  filletCorner,
  filletArc,
  segmentCommand,
} from "./filletGeometry";
import type { Segment } from "./filletGeometry";
const LINE_FIT_RATIO = 0.98;

export function connectionGeometry(input: Point[], rounded = true) {
  // Repeated draft/anchor points must not create zero-length tangents.
  const points = input.filter(
    (p, i) => !i || distance(p, input[i - 1]!) > EPSILON,
  );
  const corners = points.map((vertex, i) => {
    if (!rounded || !i || i === points.length - 1) return undefined;
    return filletCorner(points[i - 1]!, vertex, points[i + 1]!);
  });
  // Adjacent fillets share a segment: shrink both rather than overlap/backtrack.
  for (let i = 1; i < points.length - 2; i++) {
    const a = corners[i],
      b = corners[i + 1];
    if (!a || !b) continue;
    const available = distance(points[i]!, points[i + 1]!) * LINE_FIT_RATIO;
    if (a.trim + b.trim > available) {
      const scale = available / (a.trim + b.trim);
      a.trim *= scale;
      b.trim *= scale;
    }
  }
  const segments: Segment[] = [];
  let cursor = points[0];
  function line(end: Point) {
    if (cursor && distance(cursor, end) > EPSILON)
      segments.push({
        kind: "line",
        start: cursor,
        end,
        length: distance(cursor, end),
      });
    cursor = end;
  }
  for (let i = 1; i < points.length - 1; i++) {
    const c = corners[i];
    if (!c) {
      line(points[i]!);
      continue;
    }
    const arc = filletArc(c);
    line(arc.start);
    segments.push(arc);
    cursor = arc.end;
  }
  if (points.length) line(points[points.length - 1]!);
  const path = points.length
    ? `M ${points[0]!.x} ${points[0]!.y}` +
      segments.map(segmentCommand).join("")
    : "";
  const length = segments.reduce((sum, s) => sum + s.length, 0);
  let middle: (Point & { angle: number; length: number }) | undefined;
  let remaining = length / 2;
  if (length > EPSILON)
    for (const segment of segments) {
      if (remaining > segment.length) {
        remaining -= segment.length;
        continue;
      }
      const t = remaining / segment.length;
      if (segment.kind === "line") {
        middle = {
          x: segment.start.x + (segment.end.x - segment.start.x) * t,
          y: segment.start.y + (segment.end.y - segment.start.y) * t,
          angle:
            (Math.atan2(
              segment.end.y - segment.start.y,
              segment.end.x - segment.start.x,
            ) *
              180) /
            Math.PI,
          length,
        };
      } else {
        const angle = segment.startAngle + segment.sweep * t;
        middle = {
          x: segment.center.x + segment.radius * Math.cos(angle),
          y: segment.center.y + segment.radius * Math.sin(angle),
          angle:
            ((angle + (Math.sign(segment.sweep) * Math.PI) / 2) * 180) /
            Math.PI,
          length,
        };
      }
      break;
    }
  return { path, middle, segments };
}
