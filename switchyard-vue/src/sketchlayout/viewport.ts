import type { Point, SchemeDocument } from "./types";

export interface CanvasView { x: number; y: number; w: number; h: number }
export interface CanvasSize { width: number; height: number }
// An initial camera position, not a boundary for drawing or storage.
export const DEFAULT_VIEW: CanvasView = { x: 0, y: 0, w: 2400, h: 1600 };

export function visibleWorld(view: CanvasView, size: CanvasSize): CanvasView {
  const scale = Math.min(size.width / view.w, size.height / view.h);
  const w = size.width / scale, h = size.height / scale;
  // Cover the letterboxed portions of an SVG using xMidYMid meet as well.
  return { x: view.x + (view.w - w) / 2, y: view.y + (view.h - h) / 2, w, h };
}

export function fitDocument(document: SchemeDocument, size: CanvasSize, showCrossings = true): CanvasView {
  const points: Point[] = [];
  const circle = (center: Point, radius: number) => {
    points.push({ x: center.x - radius, y: center.y - radius },
      { x: center.x + radius, y: center.y + radius });
  };
  for (const f of document.facilities) {
    if (f.kind === "circle") circle(f.center, f.radius);
    else points.push(...f.vertices);
  }
  for (const l of document.links) points.push(...l.anchors);
  if (showCrossings) for (const c of document.crossings ?? []) circle(c.center, c.radius);
  if (!points.length) return { ...DEFAULT_VIEW };
  const minX = Math.min(...points.map(p => p.x)), maxX = Math.max(...points.map(p => p.x));
  const minY = Math.min(...points.map(p => p.y)), maxY = Math.max(...points.map(p => p.y));
  // Leave room for labels and editing handles, even for a single small object.
  const width = Math.max(400, (maxX - minX) * 1.2 + 80);
  const height = Math.max(280, (maxY - minY) * 1.2 + 80);
  const aspect = size.width / size.height;
  const w = Math.max(width, height * aspect), h = w / aspect;
  return { x: minX + (maxX - minX - w) / 2, y: minY + (maxY - minY - h) / 2, w, h };
}
