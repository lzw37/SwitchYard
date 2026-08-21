import type { StationLayoutTranslate } from "./gateway";
export declare const stationLayoutMessages: {
    readonly zh: {
        readonly stationLayout: {
            readonly toolbar: {
                readonly edit: "编辑工具栏";
                readonly compact: "简洁模式";
                readonly full: "完整模式";
            };
            readonly menu: {
                readonly stationScheme: "车站方案";
                readonly file: "文件";
                readonly loadData: "载入数据";
                readonly saveData: "保存数据";
                readonly gridSnap: "网格追踪";
                readonly objectSnap: "对象追踪";
                readonly snapDistance: "吸附阈值";
                readonly showGrid: "显示网格";
                readonly gridSpacing: "网格间距";
                readonly undo: "撤销";
                readonly redo: "重做";
                readonly clearSelection: "清除选择";
                readonly deleteSelection: "删除选择";
            };
            readonly placeholders: {
                readonly selectInstance: "请先选择车站";
                readonly selectStationScheme: "选择车站方案";
            };
            readonly schemeManager: {
                readonly manage: "管理";
                readonly title: "车站方案管理";
                readonly add: "新增";
                readonly edit: "编辑";
                readonly save: "保存";
                readonly cancel: "取消";
                readonly delete: "删除";
                readonly close: "关闭";
                readonly confirm: "确定";
                readonly id: "方案 ID";
                readonly name: "方案名称";
                readonly operation: "操作";
                readonly namePlaceholder: "方案名称";
                readonly idRequired: "方案 ID 不能为空";
                readonly nameRequired: "方案名称不能为空";
                readonly createSuccess: "车站方案已新增";
                readonly createFailed: "新增车站方案失败";
                readonly updateSuccess: "车站方案已更新";
                readonly updateFailed: "更新车站方案失败";
                readonly deleteSuccess: "车站方案已删除";
                readonly deleteFailed: "删除车站方案失败";
                readonly deleteTitle: "删除车站方案";
                readonly deleteConfirm: "确定删除“{name}”及其全部布置图数据吗？";
            };
            readonly group: {
                readonly drawingObject: "绘图对象";
                readonly curveDisplay: "曲线显示";
                readonly displayScale: "显示比例";
            };
            readonly scale: {
                readonly x: "X";
                readonly y: "Y";
            };
            readonly mode: {
                readonly select: "选择";
                readonly draw: "绘图";
            };
            readonly draw: {
                readonly line: "线";
                readonly node: "节点";
                readonly signal: "信号机";
                readonly switch: "道岔";
                readonly insulation: "钢轨绝缘";
                readonly route: "进路";
                readonly buffer: "车挡";
                readonly platform: "站台";
                readonly annotation: "注释";
            };
            readonly tools: {
                readonly showCrossPoint: "显示交点";
                readonly hideCrossPoint: "隐藏交点";
                readonly snapLine: "处理虚接";
                readonly separateLine: "线路分段";
                readonly generateNode: "节点生成";
                readonly generateSwitch: "道岔生成";
                readonly generateCurve: "曲线生成";
                readonly fitFullView: "显示全图";
            };
            readonly curveDisplay: {
                readonly arc: "圆弧";
                readonly tangent: "切线";
            };
            readonly messages: {
                readonly saveSuccess: "保存成功：";
                readonly saveFailed: "保存失败：";
                readonly loadFailed: "获取失败：";
                readonly loadSchemesFailed: "加载车站方案失败";
                readonly readonly: "当前为只读模式，不能执行写操作";
            };
        };
    };
    readonly en: {
        readonly stationLayout: {
            readonly toolbar: {
                readonly edit: "Edit toolbar";
                readonly compact: "Compact";
                readonly full: "Full";
            };
            readonly menu: {
                readonly stationScheme: "Station Scheme";
                readonly file: "File";
                readonly loadData: "Load";
                readonly saveData: "Save";
                readonly gridSnap: "Grid Snap";
                readonly objectSnap: "Object Snap";
                readonly snapDistance: "Snap Distance";
                readonly showGrid: "Show Grid";
                readonly gridSpacing: "Grid Spacing";
                readonly undo: "Undo";
                readonly redo: "Redo";
                readonly clearSelection: "Clear Selection";
                readonly deleteSelection: "Delete Selection";
            };
            readonly placeholders: {
                readonly selectInstance: "Please select a station";
                readonly selectStationScheme: "Select station scheme";
            };
            readonly schemeManager: {
                readonly manage: "Manage";
                readonly title: "Station Scheme Manager";
                readonly add: "Add";
                readonly edit: "Edit";
                readonly save: "Save";
                readonly cancel: "Cancel";
                readonly delete: "Delete";
                readonly close: "Close";
                readonly confirm: "Confirm";
                readonly id: "Scheme ID";
                readonly name: "Scheme Name";
                readonly operation: "Operation";
                readonly namePlaceholder: "Scheme name";
                readonly idRequired: "Scheme ID is required";
                readonly nameRequired: "Scheme name is required";
                readonly createSuccess: "Station scheme created";
                readonly createFailed: "Failed to create station scheme";
                readonly updateSuccess: "Station scheme updated";
                readonly updateFailed: "Failed to update station scheme";
                readonly deleteSuccess: "Station scheme deleted";
                readonly deleteFailed: "Failed to delete station scheme";
                readonly deleteTitle: "Delete station scheme";
                readonly deleteConfirm: "Delete \"{name}\" and all of its layout data?";
            };
            readonly group: {
                readonly drawingObject: "Drawing Object";
                readonly curveDisplay: "Curve View";
                readonly displayScale: "Display Scale";
            };
            readonly scale: {
                readonly x: "X";
                readonly y: "Y";
            };
            readonly mode: {
                readonly select: "Select";
                readonly draw: "Draw";
            };
            readonly draw: {
                readonly line: "Line";
                readonly node: "Node";
                readonly signal: "Signal";
                readonly switch: "Switch";
                readonly insulation: "Insulation";
                readonly route: "Route";
                readonly buffer: "Buffer";
                readonly platform: "Platform";
                readonly annotation: "Annotation";
            };
            readonly tools: {
                readonly showCrossPoint: "Show Cross";
                readonly hideCrossPoint: "Hide Cross";
                readonly snapLine: "Snap Line";
                readonly separateLine: "Separate";
                readonly generateNode: "Generate Node";
                readonly generateSwitch: "Generate Switch";
                readonly generateCurve: "Generate Curve";
                readonly fitFullView: "Fit All";
            };
            readonly curveDisplay: {
                readonly arc: "Arc";
                readonly tangent: "Tangent";
            };
            readonly messages: {
                readonly saveSuccess: "Save successful: ";
                readonly saveFailed: "Save failed: ";
                readonly loadFailed: "Load failed: ";
                readonly loadSchemesFailed: "Failed to load station schemes";
                readonly readonly: "Read-only mode does not allow write operations";
            };
        };
    };
};
export declare function createStationLayoutTranslator(locale?: keyof typeof stationLayoutMessages): StationLayoutTranslate;
