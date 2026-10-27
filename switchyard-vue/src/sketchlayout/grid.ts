import type { GridSettings, Point } from "./types";

export const DEFAULT_GRID_SETTINGS: GridSettings = {
  showGrid: true,
  snapToGrid: true,
  spacing: 20,
  originX: 0,
  originY: 0,
};

export function gridSettings(
  value?: Partial<GridSettings> | null,
): GridSettings {
  return {
    showGrid: value?.showGrid !== false,
    snapToGrid: value?.snapToGrid !== false,
    spacing: Number.isFinite(value?.spacing)
      ? Math.max(1, Math.min(500, value!.spacing!))
      : DEFAULT_GRID_SETTINGS.spacing,
    originX: Number.isFinite(value?.originX) ? value!.originX! : 0,
    originY: Number.isFinite(value?.originY) ? value!.originY! : 0,
  };
}

// Match StationLayout's minimum 8px dot separation. Rendering density never changes snapping.
export function displayGridSpacing(spacing: number, scale: number): number {
  return (
    spacing * Math.max(1, Math.ceil(8 / (spacing * Math.max(Number.EPSILON, scale))))
  );
}

function coordinate(
  value: number,
  origin: number,
  grid: GridSettings,
): number {
  if (!grid.snapToGrid) return value;
  const index = Math.round((value - origin) / grid.spacing);
  return Number((origin + index * grid.spacing).toFixed(9));
}

export function gridPoint(
  point: Point,
  grid: GridSettings,
): Point {
  return {
    x: coordinate(point.x, grid.originX, grid),
    y: coordinate(point.y, grid.originY, grid),
  };
}
