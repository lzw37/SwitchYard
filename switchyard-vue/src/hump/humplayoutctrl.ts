// TypeScript equivalents of backend classes from Position.cs

import $ from "jquery";
import { createSemanticDiagnosticsBuilderProgram } from "typescript";

export enum SwitchTypes {
    Single = 0,
    Slip = 1,
    Diamond = 2,
    None = 3,
}

export enum SwitchDirections {
    Reverse = 0,
    Forward = 1,
    None = 2,
}

export enum SwitchSides {
    Left = 0,
    Right = 1,
    None = 2,
}

export enum CurveDirections {
    Left = 0,
    Right = 1,
    None = 2,
}

export class Position {
    id: string;
    x: number;
    height: number;

    constructor(id = "", x = 0, height = 0) {
        this.id = id;
        this.x = x;
        this.height = height;
    }
}

export class PositionSegment {
    id: string = "";
    startPositionID: string;
    endPositionID: string;
    length: number;
    curveDegree: number;
    curveDirection: CurveDirections;
    locationParam: number;

    constructor(
        id = "",
        startPositionID = "",
        endPositionID = "",
        length = 0,
        curveDegree = 0,
        locationParam = 0,
        curveDirection = CurveDirections.None
    ) {
        this.id = id;
        this.startPositionID = startPositionID;
        this.endPositionID = endPositionID;
        this.length = length;
        this.curveDegree = curveDegree;
        this.locationParam = locationParam;
        this.curveDirection = curveDirection;
    }
}

export class Switch {
    bindingPositionID?: string;
    bindingPositionSegmentID?: string;
    type: SwitchTypes;
    direction: SwitchDirections;
    side: SwitchSides;

    constructor(opts: Partial<Switch> = {}) {
        this.bindingPositionID = opts.bindingPositionID;
        this.bindingPositionSegmentID = opts.bindingPositionSegmentID;
        this.type = opts.type ?? SwitchTypes.Single;
        this.direction = opts.direction ?? SwitchDirections.Forward;
        this.side = opts.side ?? SwitchSides.Left;
    }
}

export class Retarder {
    bindingPositionSegment?: PositionSegment;
    numberArray: number[];

    constructor(
        bindingPositionSegment?: PositionSegment,
        numberArray: number[] = []
    ) {
        this.bindingPositionSegment = bindingPositionSegment;
        this.numberArray = numberArray;
    }
}

export class SwitchCount {
    reverseCount: number;
    forwardCount: number;
    slipCount: number;
    diamondCount: number;

    constructor(
        reverseCount = 0,
        forwardCount = 0,
        slipCount = 0,
        diamondCount = 0
    ) {
        this.reverseCount = reverseCount;
        this.forwardCount = forwardCount;
        this.slipCount = slipCount;
        this.diamondCount = diamondCount;
    }
}

export class FlatLayout {
    positionList: Position[];
    positionSegmentList: PositionSegment[];
    switchList: Switch[];
    retarderList: Retarder[];

    constructor() {
        this.positionList = [];
        this.positionSegmentList = [];
        this.switchList = [];
        this.retarderList = [];
    }
}
