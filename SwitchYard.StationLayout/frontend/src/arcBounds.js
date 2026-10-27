const FULL_TURN = Math.PI * 2;

function finiteNumber(value) {
    const number = Number(value);
    return Number.isFinite(number) ? number : 0;
}

function positiveAngle(angle) {
    return ((angle % FULL_TURN) + FULL_TURN) % FULL_TURN;
}

/**
 * Bounds of the circular SVG A command used by the station layout renderer.
 * SVG derives the center from the endpoints, radius and flags, so a stored
 * center must not enlarge the bounds of a short arc to those of a full circle.
 * Coordinates are read only; zero or invalid radii use the renderer's fallback.
 */
export function getCircularArcBounds(curve, fallbackRadius = 100) {
    const startX = finiteNumber(curve?.start?.x);
    const startY = finiteNumber(curve?.start?.y);
    const endX = finiteNumber(curve?.end?.x);
    const endY = finiteNumber(curve?.end?.y);
    const bounds = {
        minX: Math.min(startX, endX),
        minY: Math.min(startY, endY),
        maxX: Math.max(startX, endX),
        maxY: Math.max(startY, endY),
    };
    // SVG omits an arc whose endpoints coincide.
    if (startX === endX && startY === endY) return bounds;

    const halfDx = startX / 2 - endX / 2;
    const halfDy = startY / 2 - endY / 2;
    const halfChord = Math.hypot(halfDx, halfDy);
    if (!Number.isFinite(halfChord) || halfChord === 0) return bounds;
    // SVG expands a radius that cannot span the chord. The renderer clamps
    // negative radii to a small positive value before emitting the command.
    const requestedRadius = Math.max(0.001, finiteNumber(curve?.radius) || finiteNumber(fallbackRadius) || 100);
    const radius = Math.max(requestedRadius, halfChord);
    const ratio = halfChord / radius;
    const centerDistance = radius * Math.sqrt(Math.max(0, (1 - ratio) * (1 + ratio)));
    const largeArc = Number(curve?.largeArcFlag) === 1;
    const sweep = Number(curve?.sweepFlag) === 1;
    const centerSide = largeArc === sweep ? -1 : 1;
    const centerX = startX / 2 + endX / 2 + centerSide * (halfDy / halfChord) * centerDistance;
    const centerY = startY / 2 + endY / 2 - centerSide * (halfDx / halfChord) * centerDistance;
    const startAngle = Math.atan2(startY - centerY, startX - centerX);
    const minorAngle = 2 * Math.asin(Math.min(1, ratio));
    const sweepAngle = largeArc ? FULL_TURN - minorAngle : minorAngle;

    // A circular arc can reach an axis extremum only at one of these angles.
    // Using exact cardinal coordinates avoids trigonometric rounding at axes.
    const extrema = [
        [0, centerX + radius, centerY],
        [Math.PI / 2, centerX, centerY + radius],
        [Math.PI, centerX - radius, centerY],
        [Math.PI * 1.5, centerX, centerY - radius],
    ];
    for (const [angle, x, y] of extrema) {
        const distance = positiveAngle(sweep ? angle - startAngle : startAngle - angle);
        if (distance > sweepAngle + 1e-12 || !Number.isFinite(x) || !Number.isFinite(y)) continue;
        bounds.minX = Math.min(bounds.minX, x);
        bounds.minY = Math.min(bounds.minY, y);
        bounds.maxX = Math.max(bounds.maxX, x);
        bounds.maxY = Math.max(bounds.maxY, y);
    }
    return bounds;
}
