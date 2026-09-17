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
    const longitudinalScale = Math.hypot(dx, dy)
    const screenAngle = Math.atan2(dy, dx) * 180 / Math.PI
    // Use one scale for both dimensions, including the minimum visible size.
    // Following the projected length also keeps carriage spacing along the track.
    const bodyScale = Math.max(longitudinalScale, 8 / car.length, 4 / car.width)
    return {
        x: (car.x - viewport.minX) * viewport.scaleX,
        y: (car.y - viewport.minY) * viewport.scaleY,
        // Retain continuous rotations when a train crosses the +/-180° seam.
        angle: screenAngle + 360 * Math.round((car.angle - screenAngle) / 360),
        length: car.length * bodyScale,
        width: car.width * bodyScale,
    }
}
