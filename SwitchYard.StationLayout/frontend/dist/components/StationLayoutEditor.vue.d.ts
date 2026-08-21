declare const _default: typeof __VLS_export;
export default _default;
declare const __VLS_export: import("vue").DefineComponent<import("vue").ExtractPropTypes<{
    width: {
        type: NumberConstructor;
        default: number;
    };
    height: {
        type: NumberConstructor;
        default: number;
    };
    displayScaleX: {
        type: NumberConstructor;
        default: number;
    };
    displayScaleY: {
        type: NumberConstructor;
        default: number;
    };
    showCurveArc: {
        type: BooleanConstructor;
        default: boolean;
    };
    showNodes: {
        type: BooleanConstructor;
        default: boolean;
    };
    showGrid: {
        type: BooleanConstructor;
        default: boolean;
    };
    gridSpacing: {
        type: NumberConstructor;
        default: number;
    };
    objectSnapDistance: {
        type: NumberConstructor;
        default: number;
    };
    displayStyles: {
        type: ObjectConstructor;
        default: () => {};
    };
    editorState: {
        type: StringConstructor;
        default: string;
    };
    cellLinkMembershipCounts: {
        type: ObjectConstructor;
        default: () => {};
    };
    cells: {
        type: ArrayConstructor;
        default: () => never[];
    };
    showCellNames: {
        type: BooleanConstructor;
        default: boolean;
    };
    routePickTarget: {
        type: StringConstructor;
        default: string;
    };
    highlightedRouteLinkIds: {
        type: ArrayConstructor;
        default: () => never[];
    };
    highlightedRouteNodeIds: {
        type: ArrayConstructor;
        default: () => never[];
    };
    highlightedRouteArrowNodeIds: {
        type: ArrayConstructor;
        default: () => never[];
    };
    highlightedRouteColor: {
        type: StringConstructor;
        default: string;
    };
    highlightedRouteArrowVisible: {
        type: BooleanConstructor;
        default: boolean;
    };
    autoGenerateTopology: {
        type: BooleanConstructor;
        default: boolean;
    };
    readonly: {
        type: BooleanConstructor;
        default: boolean;
    };
}>, {
    editModeCode: import("vue").Ref<number, number>;
    drawingObject: import("vue").Ref<string, string>;
    mouseGridSnapModeCode: import("vue").Ref<number, number>;
    mouseObjectSnapModeCode: import("vue").Ref<number, number>;
    setEditMode: typeof setEditMode;
    setDrawingObject: typeof setDrawingObject;
    setDrawingSignalType: typeof setDrawingSignalType;
    setDrawingBufferStopDirection: typeof setDrawingBufferStopDirection;
    setDrawingBufferStopType: typeof setDrawingBufferStopType;
    setMouseGridSnapModeCode: typeof setMouseGridSnapModeCode;
    setMouseObjectSnapModeCode: typeof setMouseObjectSnapModeCode;
    clearSelectedLines: typeof clearSelectedLines;
    clearSelectedNodes: typeof clearSelectedNodes;
    clearSelectedEquipment: typeof clearSelectedEquipment;
    deleteLine: typeof deleteLine;
    getSelectedNodeDeletePlan: typeof getSelectedNodeDeletePlan;
    deleteNode: typeof deleteNode;
    deleteEquipment: typeof deleteEquipment;
    revoke: typeof revoke;
    redo: typeof redo;
    buildJsonData: typeof buildJsonData;
    loadDataFromJson: typeof loadDataFromJson;
    autoSeparateLine: typeof autoSeparateLine;
    markCrossPoint: typeof markCrossPoint;
    removeCrossPoint: typeof removeCrossPoint;
    snapLine: typeof snapLine;
    autoMergeNode: typeof autoMergeNode;
    autoGenerateNodes: typeof autoGenerateNodes;
    getEquipmentBindingNodeCorrectionPlan: typeof getEquipmentBindingNodeCorrectionPlan;
    applyEquipmentBindingNodeCorrections: typeof applyEquipmentBindingNodeCorrections;
    correctEquipmentBindingNodesByPosition: typeof correctEquipmentBindingNodesByPosition;
    getAutoGenerateSwitchPlan: typeof getAutoGenerateSwitchPlan;
    autoGenerateSwitches: typeof autoGenerateSwitches;
    autoGenerateCurves: typeof autoGenerateCurves;
    startDrawingSignal: typeof startDrawingSignal;
    startDrawingInsulationJoint: typeof startDrawingInsulationJoint;
    startDrawingBufferStop: typeof startDrawingBufferStop;
    startDrawingNode: typeof startDrawingNode;
    startDrawingPlatform: typeof startDrawingPlatform;
    updateSelectedAnnotation: typeof updateSelectedAnnotation;
    updateSelectedEquipment: typeof updateSelectedEquipment;
    updateSelectedEquipmentBatch: typeof updateSelectedEquipmentBatch;
    clearElements: typeof clearElements;
    getFullViewRect: typeof getFullViewRect;
    scrollDataRectIntoView: typeof scrollDataRectIntoView;
    getCanvasViewportState: typeof getCanvasViewportState;
}, {}, {}, {}, import("vue").ComponentOptionsMixin, import("vue").ComponentOptionsMixin, {
    "selected-annotation-change": (...args: any[]) => void;
    "selected-equipment-change": (...args: any[]) => void;
    "route-node-pick": (...args: any[]) => void;
    "cell-name-click": (...args: any[]) => void;
    "delete-selection-request": (...args: any[]) => void;
    "topology-rebuilt": (...args: any[]) => void;
}, string, import("vue").PublicProps, Readonly<import("vue").ExtractPropTypes<{
    width: {
        type: NumberConstructor;
        default: number;
    };
    height: {
        type: NumberConstructor;
        default: number;
    };
    displayScaleX: {
        type: NumberConstructor;
        default: number;
    };
    displayScaleY: {
        type: NumberConstructor;
        default: number;
    };
    showCurveArc: {
        type: BooleanConstructor;
        default: boolean;
    };
    showNodes: {
        type: BooleanConstructor;
        default: boolean;
    };
    showGrid: {
        type: BooleanConstructor;
        default: boolean;
    };
    gridSpacing: {
        type: NumberConstructor;
        default: number;
    };
    objectSnapDistance: {
        type: NumberConstructor;
        default: number;
    };
    displayStyles: {
        type: ObjectConstructor;
        default: () => {};
    };
    editorState: {
        type: StringConstructor;
        default: string;
    };
    cellLinkMembershipCounts: {
        type: ObjectConstructor;
        default: () => {};
    };
    cells: {
        type: ArrayConstructor;
        default: () => never[];
    };
    showCellNames: {
        type: BooleanConstructor;
        default: boolean;
    };
    routePickTarget: {
        type: StringConstructor;
        default: string;
    };
    highlightedRouteLinkIds: {
        type: ArrayConstructor;
        default: () => never[];
    };
    highlightedRouteNodeIds: {
        type: ArrayConstructor;
        default: () => never[];
    };
    highlightedRouteArrowNodeIds: {
        type: ArrayConstructor;
        default: () => never[];
    };
    highlightedRouteColor: {
        type: StringConstructor;
        default: string;
    };
    highlightedRouteArrowVisible: {
        type: BooleanConstructor;
        default: boolean;
    };
    autoGenerateTopology: {
        type: BooleanConstructor;
        default: boolean;
    };
    readonly: {
        type: BooleanConstructor;
        default: boolean;
    };
}>> & Readonly<{
    "onSelected-annotation-change"?: ((...args: any[]) => any) | undefined;
    "onSelected-equipment-change"?: ((...args: any[]) => any) | undefined;
    "onRoute-node-pick"?: ((...args: any[]) => any) | undefined;
    "onCell-name-click"?: ((...args: any[]) => any) | undefined;
    "onDelete-selection-request"?: ((...args: any[]) => any) | undefined;
    "onTopology-rebuilt"?: ((...args: any[]) => any) | undefined;
}>, {
    displayStyles: Record<string, any>;
    cells: unknown[];
    readonly: boolean;
    width: number;
    height: number;
    displayScaleX: number;
    displayScaleY: number;
    showCurveArc: boolean;
    showNodes: boolean;
    showGrid: boolean;
    gridSpacing: number;
    objectSnapDistance: number;
    editorState: string;
    cellLinkMembershipCounts: Record<string, any>;
    showCellNames: boolean;
    routePickTarget: string;
    highlightedRouteLinkIds: unknown[];
    highlightedRouteNodeIds: unknown[];
    highlightedRouteArrowNodeIds: unknown[];
    highlightedRouteColor: string;
    highlightedRouteArrowVisible: boolean;
    autoGenerateTopology: boolean;
}, {}, {}, {}, string, import("vue").ComponentProvideOptions, true, {}, any>;
declare function setEditMode(code: any): void;
declare function setDrawingObject(obj: any): void;
declare function setDrawingSignalType(type: any): void;
declare function setDrawingBufferStopDirection(direction: any): void;
declare function setDrawingBufferStopType(type: any): void;
declare function setMouseGridSnapModeCode(code: any): void;
declare function setMouseObjectSnapModeCode(code: any): void;
declare function clearSelectedLines(): void;
declare function clearSelectedNodes(): void;
declare function clearSelectedEquipment(): void;
declare function deleteLine(): void;
declare function getSelectedNodeDeletePlan(): {
    nodeIds: any[];
    boundEquipment: any[];
    counts: any;
    requiresConfirmation: boolean;
};
declare function deleteNode(options?: {}): {
    requiresConfirmation: boolean;
    nodeIds: any[];
    boundEquipment: any[];
    counts: any;
    deleted?: undefined;
} | {
    deleted: boolean;
    nodeIds: any[];
    boundEquipment: any[];
    counts: any;
    requiresConfirmation?: undefined;
} | undefined;
declare function deleteEquipment(): void;
declare function revoke(): void;
declare function redo(): void;
declare function buildJsonData(): string;
declare function loadDataFromJson(jsonObj: any): void;
declare function autoSeparateLine(): {
    topologyChanged: boolean;
    rewiredExistingTopology: boolean;
    removedLineIds: any[];
    removedNodeIds: any[];
    changedEquipmentBindings: {
        kind: string;
        id: string;
        previousBindingNodeId: any;
        currentBindingNodeId: string;
    }[];
    missingEquipmentBindings: {
        kind: string;
        id: string;
        bindingNodeId: string;
    }[];
    cellsMayNeedRebuild: any;
    equipmentMayNeedRepair: boolean;
    curvesMayNeedRepair: boolean;
    switchesMayNeedRepair: boolean;
    routesMayNeedReview: boolean;
    requiresRepair: any;
} | null;
declare function markCrossPoint(): {
    code: number;
    relation: {
        code: number;
        breakingLineList?: undefined;
        snapPointList?: undefined;
    } | {
        code: number;
        breakingLineList: any[];
        snapPointList?: undefined;
    } | {
        code: number;
        snapPointList: {
            line: any;
            point: number;
        }[];
        breakingLineList?: undefined;
    };
    x: number;
    y: number;
}[];
declare function removeCrossPoint(): void;
declare function snapLine(): void;
declare function autoMergeNode(): {
    topologyChanged: boolean;
    rewiredExistingTopology: boolean;
    removedLineIds: any[];
    removedNodeIds: any[];
    changedEquipmentBindings: {
        kind: string;
        id: string;
        previousBindingNodeId: any;
        currentBindingNodeId: string;
    }[];
    missingEquipmentBindings: {
        kind: string;
        id: string;
        bindingNodeId: string;
    }[];
    cellsMayNeedRebuild: any;
    equipmentMayNeedRepair: boolean;
    curvesMayNeedRepair: boolean;
    switchesMayNeedRepair: boolean;
    routesMayNeedReview: boolean;
    requiresRepair: any;
} | null | undefined;
declare function autoGenerateNodes(): {
    topologyChanged: boolean;
    rewiredExistingTopology: boolean;
    removedLineIds: any[];
    removedNodeIds: any[];
    changedEquipmentBindings: {
        kind: string;
        id: string;
        previousBindingNodeId: any;
        currentBindingNodeId: string;
    }[];
    missingEquipmentBindings: {
        kind: string;
        id: string;
        bindingNodeId: string;
    }[];
    cellsMayNeedRebuild: any;
    equipmentMayNeedRepair: boolean;
    curvesMayNeedRepair: boolean;
    switchesMayNeedRepair: boolean;
    routesMayNeedReview: boolean;
    requiresRepair: any;
} | null;
declare function getEquipmentBindingNodeCorrectionPlan(): {
    totalCount: number;
    items: any[];
    alreadyCorrectCount: number;
    unmatchedCount: number;
};
declare function applyEquipmentBindingNodeCorrections(items?: any[]): {
    requestedCount: number;
    fixedCount: number;
    alreadyCorrectCount: number;
    unmatchedCount: number;
    fixedEquipment?: undefined;
} | {
    requestedCount: number;
    fixedCount: number;
    alreadyCorrectCount: number;
    unmatchedCount: number;
    fixedEquipment: {
        kind: string;
        id: any;
        previousBindingNodeID: string;
        nextBindingNodeID: string;
    }[];
};
declare function correctEquipmentBindingNodesByPosition(): {
    requestedCount: number;
    fixedCount: number;
    alreadyCorrectCount: number;
    unmatchedCount: number;
    fixedEquipment?: undefined;
    totalCount: number;
    items: any[];
} | {
    requestedCount: number;
    fixedCount: number;
    alreadyCorrectCount: number;
    unmatchedCount: number;
    fixedEquipment: {
        kind: string;
        id: any;
        previousBindingNodeID: string;
        nextBindingNodeID: string;
    }[];
    totalCount: number;
    items: any[];
};
declare function getAutoGenerateSwitchPlan(): {
    createItems: {
        nodeId: string;
        nodeName: string;
        switchId: string;
        switchName: string;
        existingSwitchIds: string[];
        previousLineIds: any;
        nextLineIds: any;
        candidate: any;
    }[];
    reconstructItems: {
        nodeId: string;
        nodeName: string;
        switchId: string;
        switchName: string;
        existingSwitchIds: string[];
        previousLineIds: any;
        nextLineIds: any;
        candidate: any;
    }[];
    createCount: number;
    reconstructCount: number;
    requiresConfirmation: boolean;
};
declare function autoGenerateSwitches(options?: {}): any;
declare function autoGenerateCurves(): number;
declare function startDrawingSignal(): void;
declare function startDrawingInsulationJoint(): void;
declare function startDrawingBufferStop(): void;
declare function startDrawingNode(): void;
declare function startDrawingPlatform(): void;
declare function updateSelectedAnnotation(patch: any): void;
declare function updateSelectedEquipment(kind: any, id: any, patch: any): void;
declare function updateSelectedEquipmentBatch(kind: any, ids: any, patch: any): void;
declare function clearElements(): void;
declare function getFullViewRect(options?: {}): {
    minX: number;
    minY: number;
    maxX: number;
    maxY: number;
} | null;
declare function scrollDataRectIntoView(rect: any, options?: {}): boolean;
declare function getCanvasViewportState(): {
    minX: number;
    minY: number;
    maxX: number;
    maxY: number;
    width: number;
    height: number;
    scaleX: number;
    scaleY: number;
};
