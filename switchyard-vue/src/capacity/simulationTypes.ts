import type { MovementCellOccupation } from './movementCellOccupation'

export interface StationRouteOption {
    id: string
    name: string
    type: string
    nodeList: string
    linkList: string
    startNodeID: string
    endNodeID: string
}

export interface TrainOperationPlanTrain {
    id: string
    trainTemplateID: string
    trainNumber: string
    name: string
    trainType: string
    isFixedOperation: boolean
}

export interface TrainOperationPlanMovement {
    trainID: string
    trainTemplateID: string
    movementID: string
    name: string
    routeIDList: string
    minDuration: number | null
    earliestStartTime: string
    latestEndTime: string
    cellOccupationOverridesJson?: string | null
    cellOccupations?: MovementCellOccupation[]
    route: string
    tag: string
    sortOrder: number | null
}

export interface Point {
    x: number
    y: number
    nodeId?: string
}

export interface PathSegment {
    from: Point
    to: Point
    length: number
    startDistance: number
    angle: number
}

export interface PolylinePath {
    points: Point[]
    segments: PathSegment[]
    totalLength: number
}

export interface RouteGeometry {
    path: PolylinePath
    nodeIds: string[]
    linkIds: string[]
}

export interface RouteRun {
    key: string
    train: TrainOperationPlanTrain
    movement: TrainOperationPlanMovement
    route: StationRouteOption
    path: PolylinePath
    nodeIds: string[]
    linkIds: string[]
    startSeconds: number
    endSeconds: number
    lockSeconds: number
    usesPlanTime: boolean
    absoluteStartSeconds: number
    absoluteEndSeconds: number
    color: string
}

