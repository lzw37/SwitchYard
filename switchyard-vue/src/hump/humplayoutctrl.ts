// TypeScript equivalents of backend classes from Position.cs

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

export enum LocationParam {
    HumpSection = 0, // 溜放部分
    YardSection = 1, // 调车场
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

export class HPosition extends Position {
    // 水平位置点，继承 Position
}

export class VPosition extends Position {
    // 垂直位置点，继承 Position
}

export class PositionSegment {
    id: string = "";
    startPositionID: string;
    endPositionID: string;
    length: number;
    curveDegree: number;
    curveDirection: CurveDirections;
    locationParam: LocationParam;

    constructor(
        id = "",
        startPositionID = "",
        endPositionID = "",
        length = 0,
        curveDegree = 0,
        locationParam = LocationParam.YardSection,
        curveDirection = CurveDirections.None,
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

export class HPositionSegment extends PositionSegment {
    // 水平位置区间，继承 PositionSegment，已有 curveDegree 等
}

export class VPositionSegment extends PositionSegment {
    gradient: number; // 坡度/‰
    height: number; // 高度/m

    constructor(
        id = "",
        startPositionID = "",
        endPositionID = "",
        length = 0,
        gradient = 0,
        height = 0,
    ) {
        super(id, startPositionID, endPositionID, length);
        this.gradient = gradient;
        this.height = height;
    }
}

export class Switch {
    id: string;
    bindingPositionID?: string;
    bindingPositionSegmentID?: string;
    type: SwitchTypes;
    direction: SwitchDirections;
    side: SwitchSides;
    curveDegree: number;

    constructor(opts: Partial<Switch> = {}) {
        this.id = opts.id ?? "";
        this.bindingPositionID = opts.bindingPositionID;
        this.bindingPositionSegmentID = opts.bindingPositionSegmentID;
        this.type = opts.type ?? SwitchTypes.Single;
        this.direction = opts.direction ?? SwitchDirections.Forward;
        this.side = opts.side ?? SwitchSides.Left;
        this.curveDegree = opts.curveDegree ?? 0;
    }
}

export class Retarder {
    id: string;
    bindingPositionSegment?: PositionSegment;
    numberArray: number[];

    constructor(
        bindingPositionSegment?: PositionSegment,
        numberArray: number[] = [],
    ) {
        this.id = "";
        this.bindingPositionSegment = bindingPositionSegment;
        this.numberArray = numberArray;
    }

    get numbers(): string {
        return this.numberArray ? this.numberArray.join("+") : "";
    }

    set numbers(value: string) {
        if (value) {
            this.numberArray = value.split("+").map((s) => parseInt(s));
        } else {
            this.numberArray = [];
        }
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
        diamondCount = 0,
    ) {
        this.reverseCount = reverseCount;
        this.forwardCount = forwardCount;
        this.slipCount = slipCount;
        this.diamondCount = diamondCount;
    }
}

export class FlatLayout {
    lineID: string;
    positionList: Position[];
    positionSegmentList: PositionSegment[];
    switchList: Switch[];
    retarderList: Retarder[];

    constructor() {
        this.lineID = "";
        this.positionList = [];
        this.positionSegmentList = [];
        this.switchList = [];
        this.retarderList = [];
    }
}

export class SlopeLayout {
    positionList: VPosition[];
    positionSegmentList: VPositionSegment[];

    constructor() {
        this.positionList = [];
        this.positionSegmentList = [];
    }
}
