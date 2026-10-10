import { connectionGeometry } from "./linkGeometry";
import type {
  Facility,
  LogisticsLink,
  Point,
  Port,
  SchemeDocument,
} from "./types";
export function center(f: Facility): Point {
  if (f.kind === "circle") return f.center;
  return {
    x: f.vertices.reduce((n, p) => n + p.x, 0) / f.vertices.length,
    y: f.vertices.reduce((n, p) => n + p.y, 0) / f.vertices.length,
  };
}
export function ports(f: Facility): Port[] {
  return f.kind === "circle"
    ? [{ facilityId: f.id, kind: "center", index: 0 }]
    : f.vertices.flatMap((_, index) => [
        { facilityId: f.id, kind: "vertex" as const, index },
        { facilityId: f.id, kind: "midpoint" as const, index },
      ]);
}
export function portPoint(p: Port, facilities: Facility[]): Point {
  const f = facilities.find((f) => f.id === p.facilityId);
  if (!f) return { x: 0, y: 0 };
  if (f.kind === "circle") return f.center;
  const a = f.vertices[p.index]!,
    b = f.vertices[(p.index + 1) % f.vertices.length]!;
  return p.kind === "vertex" ? a : { x: (a.x + b.x) / 2, y: (a.y + b.y) / 2 };
}
export function linkPoints(l: LogisticsLink, facilities: Facility[]) {
  return [
    portPoint(l.from, facilities),
    ...l.anchors,
    portPoint(l.to, facilities),
  ];
}
export function curvePath(points: Point[], rounded = true): string {
  return connectionGeometry(points, rounded).path;
}
export function curveMidpoint(points: Point[], rounded = true) {
  return connectionGeometry(points, rounded).middle;
}
export function simplePolygon(p: Point[]): boolean {
  if (p.length < 3) return false;
  const cross = (a: Point, b: Point, c: Point) =>
    (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
  const on = (a: Point, b: Point, c: Point) =>
    Math.abs(cross(a, b, c)) < 1e-7 &&
    c.x >= Math.min(a.x, b.x) - 1e-7 &&
    c.x <= Math.max(a.x, b.x) + 1e-7 &&
    c.y >= Math.min(a.y, b.y) - 1e-7 &&
    c.y <= Math.max(a.y, b.y) + 1e-7;
  let area = 0;
  for (let i = 0; i < p.length; i++) {
    const a = p[i]!,
      b = p[(i + 1) % p.length]!;
    if (Math.abs(a.x - b.x) + Math.abs(a.y - b.y) < 1e-6) return false;
    area += cross(p[0]!, a, b);
    for (let j = i + 1; j < p.length; j++) {
      if (j === i + 1 || (i === 0 && j === p.length - 1)) continue;
      const c = p[j]!,
        d = p[(j + 1) % p.length]!;
      if (
        (cross(a, b, c) * cross(a, b, d) < 0 &&
          cross(c, d, a) * cross(c, d, b) < 0) ||
        on(a, b, c) ||
        on(a, b, d) ||
        on(c, d, a) ||
        on(c, d, b)
      )
        return false;
    }
  }
  return Math.abs(area) > 1e-4;
}
export function connectingLinks(d: SchemeDocument, a: string, b: string) {
  return d.links.filter((l) => canTraverse(l, a, b));
}
export function canTraverse(l: LogisticsLink, from: string, to: string) {
  const direction = l.direction ?? "both";
  return (
    (direction !== "reverse" &&
      l.from.facilityId === from &&
      l.to.facilityId === to) ||
    (direction !== "forward" &&
      l.to.facilityId === from &&
      l.from.facilityId === to)
  );
}
export function referenced(
  d: SchemeDocument,
  kind: "facility" | "link",
  id: string,
) {
  return d.plans.some((p) =>
    p.chains.some((c) =>
      c.steps.some((s) =>
        kind === "facility" ? s.facilityId === id : s.incomingLinkId === id,
      ),
    ),
  );
}
export const gradeColor: Record<string, string> = {
  A: "#cb5b57",
  E: "#c58b36",
  I: "#55956e",
  O: "#4776a8",
  U: "#b5bdc8",
};
