export interface LayoutCoordinate {
    x: number
    y: number
}

export interface CoordinateCurve {
    start: LayoutCoordinate
    end: LayoutCoordinate
    center: LayoutCoordinate
    radius: number
    largeArcFlag: number
    sweepFlag: number
    /** Samples after a display-coordinate transform; the result need not be circular. */
    displayPoints?: readonly LayoutCoordinate[]
}

export interface CoordinateLayout {
    tracks: readonly { x1: number; y1: number; x2: number; y2: number }[]
    nodes: readonly LayoutCoordinate[]
    curves: readonly CoordinateCurve[]
    signals: readonly { position: LayoutCoordinate }[]
    switches: readonly {
        position: LayoutCoordinate
        branchVectorList: readonly LayoutCoordinate[]
    }[]
    platforms: readonly { x: number; y: number; width: number; height: number }[]
}

/** Shared centerline samples for rail geometry, layout bounds and train paths. */
export function sampleCurveCoordinates(curve: CoordinateCurve, preferredSegments = 24): LayoutCoordinate[] {
    if (curve.displayPoints && curve.displayPoints.length >= 2) {
        return curve.displayPoints.map(point => ({ ...point }))
    }

    const startAngle = Math.atan2(curve.start.y - curve.center.y, curve.start.x - curve.center.x)
    const endAngle = Math.atan2(curve.end.y - curve.center.y, curve.end.x - curve.center.x)
    let delta = endAngle - startAngle

    if (curve.sweepFlag === 1 && delta < 0) delta += Math.PI * 2
    if (curve.sweepFlag === 0 && delta > 0) delta -= Math.PI * 2

    const absoluteDelta = Math.abs(delta)
    if (curve.largeArcFlag === 1 && absoluteDelta < Math.PI) {
        delta += delta >= 0 ? Math.PI * 2 : -Math.PI * 2
    } else if (curve.largeArcFlag === 0 && absoluteDelta > Math.PI) {
        delta += delta >= 0 ? -Math.PI * 2 : Math.PI * 2
    }

    const segmentCount = Math.max(8, Math.min(56, Math.ceil(Math.abs(delta) / (Math.PI / preferredSegments))))
    const points: LayoutCoordinate[] = []
    for (let i = 0; i <= segmentCount; i++) {
        const angle = startAngle + delta * i / segmentCount
        points.push({
            x: curve.center.x + Math.cos(angle) * curve.radius,
            y: curve.center.y + Math.sin(angle) * curve.radius,
        })
    }
    points[0] = { ...curve.start }
    points[points.length - 1] = { ...curve.end }
    return points
}

/**
 * Derive display coordinates from the original layout, never from the previous
 * display result. This changes centerlines and footprints before model creation;
 * it does not stretch meshes or change their physical cross sections.
 */
export function transformLayoutCoordinates<T extends CoordinateLayout>(source: T, scaleX: number): T {
    if (!Number.isFinite(scaleX) || scaleX <= 0) {
        throw new RangeError('Layout X display scale must be finite and positive')
    }
    const transformPoint = <P extends LayoutCoordinate>(point: P): P => ({ ...point, x: point.x * scaleX })

    return {
        ...source,
        tracks: source.tracks.map(track => ({
            ...track,
            x1: track.x1 * scaleX,
            x2: track.x2 * scaleX,
        })),
        nodes: source.nodes.map(transformPoint),
        curves: source.curves.map(curve => ({
            ...curve,
            start: transformPoint(curve.start),
            end: transformPoint(curve.end),
            center: transformPoint(curve.center),
            // A circular source arc becomes an ellipse after unequal X/Y scaling.
            // Sampling first preserves its tangent connections to both Links.
            displayPoints: sampleCurveCoordinates(curve, 48).map(transformPoint),
        })),
        signals: source.signals.map(signal => ({ ...signal, position: transformPoint(signal.position) })),
        switches: source.switches.map(device => ({
            ...device,
            position: transformPoint(device.position),
            branchVectorList: device.branchVectorList.map(transformPoint),
        })),
        platforms: source.platforms.map(platform => ({
            ...platform,
            x: platform.x * scaleX,
            width: platform.width * scaleX,
        })),
    }
}
