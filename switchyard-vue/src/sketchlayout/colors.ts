export const DEFAULT_FILL_COLOR = "#edf3fa";
export const DEFAULT_STROKE_COLOR = "#6689af";
export const DEFAULT_LINK_COLOR = "#000000";
export const PRESET_COLORS = [
  "#edf3fa",
  "#dbeafe",
  "#dcfce7",
  "#fef3c7",
  "#fee2e2",
  "#ede9fe",
  "#6689af",
  "#2563eb",
  "#16a34a",
  "#d97706",
  "#dc2626",
  "#7c3aed",
  "#334155",
  "#000000",
  "#ffffff",
];
export function normalizeColor(value: unknown): string | undefined {
  if (typeof value !== "string") return undefined;
  const color = value.trim().toLowerCase();
  if (/^#[0-9a-f]{6}$/.test(color)) return color;
  if (/^#[0-9a-f]{3}$/.test(color))
    return "#" + [...color.slice(1)].map((c) => c + c).join("");
  return undefined;
}
function channels(color: string) {
  return [1, 3, 5].map((i) => parseInt(color.slice(i, i + 2), 16));
}
function luminance(color: string) {
  const [r = 0, g = 0, b = 0] = channels(color).map((v) => {
    const c = v / 255;
    return c <= 0.04045 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4;
  });
  return 0.2126 * r + 0.7152 * g + 0.0722 * b;
}
export function selectedColor(color: string) {
  // Keep a custom hue visible while giving selected edges a distinct tone.
  const target = luminance(color) < 0.18 ? 255 : 0;
  return (
    "#" +
    channels(color)
      .map((c) =>
        Math.round(c * 0.65 + target * 0.35)
          .toString(16)
          .padStart(2, "0"),
      )
      .join("")
  );
}
export function polygonTextColor(fill: string) {
  const background = luminance(fill),
    dark = luminance("#334155");
  const darkContrast =
    (Math.max(background, dark) + 0.05) / (Math.min(background, dark) + 0.05);
  return darkContrast >= 4.5
    ? "#334155"
    : background > 0.179
      ? "#000000"
      : "#ffffff";
}
