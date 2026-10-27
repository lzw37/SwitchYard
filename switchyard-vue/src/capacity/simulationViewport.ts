import type { TrainPath } from './three/trainPath.ts'

export interface SimulationViewport {
    minX: number
    minY: number
    scaleX: number
    scaleY: number
}

export interface TrainCarPose {
    x: number
    y: number
    angle: number
    length: number
    width: number
}

/** Follow the station's projected centerline without stretching the carriage. */
export function projectTrainCarToViewport(car: TrainCarPose, viewport: SimulationViewport): TrainCarPose {
    const radians = car.angle * Math.PI / 180
    const dx = Math.cos(radians) * viewport.scaleX
    const dy = Math.sin(radians) * viewport.scaleY
    const screenAngle = Math.atan2(dy, dx) * 180 / Math.PI
    // A rigid body has one scale for the whole viewport, independent of its
    // heading. Otherwise anisotropic track scaling resizes it on every bend.
    const bodyScale = Math.max(Math.min(viewport.scaleX, viewport.scaleY), 8 / car.length, 4 / car.width)
    return {
        x: (car.x - viewport.minX) * viewport.scaleX,
        y: (car.y - viewport.minY) * viewport.scaleY,
        // Retain continuous rotations when a train crosses the +/-180° seam.
        angle: screenAngle + 360 * Math.round((car.angle - screenAngle) / 360),
        length: car.length * bodyScale,
        width: car.width * bodyScale,
    }
}

/** Sample carriage spacing on the displayed centreline, in the same units as its body. */
export function projectTrainPathToViewport(path: TrainPath, viewport: SimulationViewport): TrainPath {
    const points = path.points.map(point => ({
        ...point,
        x: (point.x - viewport.minX) * viewport.scaleX,
        y: (point.y - viewport.minY) * viewport.scaleY,
    }))
    const segments: TrainPath['segments'][number][] = []
    let totalLength = 0
    for (let index = 1; index < points.length; index++) {
        const from = points[index - 1]!
        const to = points[index]!
        const length = Math.hypot(to.x - from.x, to.y - from.y)
        if (length <= 0) continue
        segments.push({ from, to, length, startDistance: totalLength,
            angle: Math.atan2(to.y - from.y, to.x - from.x) * 180 / Math.PI })
        totalLength += length
    }
    return { points, segments, totalLength }
}
