import { EMU_DIMENSIONS } from './emuTrain.ts'

type LayoutLink = { x1: number; y1: number; x2: number; y2: number }
type RouteLayoutLink = LayoutLink & { id: string; fromNodeID: string; toNodeID: string }

/** Measure each original Link before rendering trims or splits it for curves/turnouts. */
export function getLongestLinkLength(links: readonly LayoutLink[]): number {
    let longest = 0
    for (const link of links) {
        const length = Math.hypot(link.x2 - link.x1, link.y2 - link.y1)
        if (Number.isFinite(length)) longest = Math.max(longest, length)
    }
    return longest
}

/** Measure complete Links belonging to this route, without a station-wide fallback. */
export function getLongestRouteLinkLength(
    links: readonly RouteLayoutLink[],
    route: { linkIds: readonly string[]; nodeIds: readonly string[] },
): number {
    if (route.linkIds.length > 0) {
        const selectedIDs = new Set(route.linkIds)
        return getLongestLinkLength(links.filter(link => selectedIDs.has(link.id)))
    }

    // Node-only routes follow consecutive pairs. A Link between two other route
    // nodes is a chord, not evidence that the route traverses that Link.
    const neighbors = new Map<string, Set<string>>()
    for (let index = 1; index < route.nodeIds.length; index++) {
        const previous = route.nodeIds[index - 1]!
        const current = route.nodeIds[index]!
        if (!previous || !current || previous === current) continue
        if (!neighbors.has(previous)) neighbors.set(previous, new Set())
        if (!neighbors.has(current)) neighbors.set(current, new Set())
        neighbors.get(previous)!.add(current)
        neighbors.get(current)!.add(previous)
    }
    return getLongestLinkLength(links.filter(link => link.fromNodeID !== link.toNodeID
        && neighbors.get(link.fromNodeID)?.has(link.toNodeID)))
}

/** All distances use the same layout (or world) units as trackGauge. */
export function getEmuConsistSizing(trackGauge: number, longestLinkLength: number, carCount: number) {
    if (!Number.isFinite(trackGauge) || trackGauge <= 0
        || !Number.isFinite(longestLinkLength) || longestLinkLength <= 0
        || !Number.isSafeInteger(carCount) || carCount < 1) return null

    const unitsPerMeter = trackGauge / EMU_DIMENSIONS.railGauge
    const nominalCarLength = unitsPerMeter * EMU_DIMENSIONS.length
    const nominalGap = unitsPerMeter * 0.4
    const nominalTotal = carCount * nominalCarLength + (carCount - 1) * nominalGap
    const targetLength = longestLinkLength * 0.75
    // Set the entire consist to the route's target length, including gaps.
    // Longitudinal dimensions may grow or shrink; retain width and wheel gauge.
    const lengthScale = targetLength / nominalTotal
    const longitudinalUnitsPerMeter = unitsPerMeter * lengthScale
    const carLength = nominalCarLength * lengthScale
    const carGap = nominalGap * lengthScale
    return {
        carLength,
        carWidth: unitsPerMeter * EMU_DIMENSIONS.width,
        carGap,
        carPitch: carLength + carGap,
        totalLength: carCount * carLength + (carCount - 1) * carGap,
        targetLength,
        lengthScale,
        longitudinalUnitsPerMeter,
    }
}
