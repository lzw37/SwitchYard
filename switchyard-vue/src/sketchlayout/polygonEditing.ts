import type { Point, SchemeDocument } from "./types";
import { simplePolygon } from "./geometry";

export type PolygonEditError = "invalid" | "limit" | "minimum" | "connected";
export function projectToSegment(p: Point, a: Point, b: Point) {
  const dx = b.x - a.x,
    dy = b.y - a.y;
  const t = Math.max(
    0,
    Math.min(
      1,
      ((p.x - a.x) * dx + (p.y - a.y) * dy) / (dx * dx + dy * dy || 1),
    ),
  );
  const point = { x: a.x + t * dx, y: a.y + t * dy };
  return { point, distance: Math.hypot(p.x - point.x, p.y - point.y) };
}
export function nearestPolygonEdge(vertices: Point[], p: Point) {
  let result: { index: number; point: Point; distance: number } | undefined;
  vertices.forEach((a, index) => {
    const hit = projectToSegment(
      p,
      a,
      vertices[(index + 1) % vertices.length]!,
    );
    if (!result || hit.distance < result.distance) result = { index, ...hit };
  });
  return result;
}
function facilityPorts(d: SchemeDocument, id: string) {
  return d.links
    .flatMap((l) => [l.from, l.to])
    .filter((p) => p.facilityId === id);
}
function midpoint(a: Point, b: Point): Point {
  return { x: (a.x + b.x) / 2, y: (a.y + b.y) / 2 };
}
const distance = (a: Point, b: Point) => Math.hypot(a.x - b.x, a.y - b.y);

export function insertPolygonVertex(
  d: SchemeDocument,
  id: string,
  edge: number,
  point: Point,
): PolygonEditError | undefined {
  const f = d.facilities.find((f) => f.id === id);
  if (
    !f ||
    f.kind !== "polygon" ||
    !Number.isInteger(edge) ||
    edge < 0 ||
    edge >= f.vertices.length ||
    !Number.isFinite(point.x) ||
    !Number.isFinite(point.y)
  )
    return "invalid";
  if (f.vertices.length >= 100) return "limit";
  const a = f.vertices[edge]!,
    b = f.vertices[(edge + 1) % f.vertices.length]!;
  // Keep the inserted point on the original edge, including sloping/off-grid edges.
  const inserted = projectToSegment(point, a, b).point;
  const at = edge + 1;
  const vertices = [...f.vertices];
  vertices.splice(at, 0, inserted);
  if (!simplePolygon(vertices)) return "invalid";
  const oldMidpoint = midpoint(a, b);
  for (const port of facilityPorts(d, id)) {
    if (port.kind === "midpoint" && port.index === edge) {
      if (distance(inserted, oldMidpoint) < 1e-6) {
        // A split at the connected midpoint keeps that endpoint at exactly the same location.
        port.kind = "vertex";
        port.index = at;
      } else {
        // Otherwise keep the midpoint attachment on the nearer of the two replacement edges.
        port.index =
          distance(oldMidpoint, midpoint(a, inserted)) <=
          distance(oldMidpoint, midpoint(inserted, b))
            ? edge
            : at;
      }
    } else if (port.index > edge) port.index++;
  }
  f.vertices = vertices;
}

export function removePolygonVertex(
  d: SchemeDocument,
  id: string,
  index: number,
): PolygonEditError | undefined {
  const f = d.facilities.find((f) => f.id === id);
  if (
    !f ||
    f.kind !== "polygon" ||
    !Number.isInteger(index) ||
    index < 0 ||
    index >= f.vertices.length
  )
    return "invalid";
  if (f.vertices.length <= 3) return "minimum";
  const ports = facilityPorts(d, id);
  if (ports.some((p) => p.kind === "vertex" && p.index === index))
    return "connected";
  const vertices = f.vertices.filter((_, i) => i !== index);
  if (!simplePolygon(vertices)) return "invalid";
  const previousEdge = (index - 1 + f.vertices.length) % f.vertices.length;
  const mergedEdge = (index - 1 + vertices.length) % vertices.length;
  for (const port of ports) {
    if (
      port.kind === "midpoint" &&
      (port.index === previousEdge || port.index === index)
    )
      port.index = mergedEdge;
    else if (port.index > index) port.index--;
  }
  f.vertices = vertices;
}
