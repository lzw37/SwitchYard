import type { StationLayoutTranslate } from "./gateway";

export const stationLayoutMessages = {
  zh: {
    stationLayout: {
      toolbar: {
        edit: "编辑工具栏",
        compact: "简洁模式",
        full: "完整模式",
      },
      menu: {
        stationScheme: "车站方案",
        file: "文件",
        loadData: "载入数据",
        saveData: "保存数据",
        gridSnap: "网格追踪",
        objectSnap: "对象追踪",
        snapDistance: "吸附阈值",
        showGrid: "显示网格",
        gridSpacing: "网格间距",
        undo: "撤销",
        redo: "重做",
        clearSelection: "清除选择",
        deleteSelection: "删除选择",
      },
      placeholders: {
        selectInstance: "请先选择车站",
        selectStationScheme: "选择车站方案",
      },
      schemeManager: {
        manage: "管理",
        title: "车站方案管理",
        add: "新增",
        edit: "编辑",
        save: "保存",
        cancel: "取消",
        delete: "删除",
        close: "关闭",
        confirm: "确定",
        id: "方案 ID",
        name: "方案名称",
        operation: "操作",
        namePlaceholder: "方案名称",
        idRequired: "方案 ID 不能为空",
        nameRequired: "方案名称不能为空",
        createSuccess: "车站方案已新增",
        createFailed: "新增车站方案失败",
        updateSuccess: "车站方案已更新",
        updateFailed: "更新车站方案失败",
        deleteSuccess: "车站方案已删除",
        deleteFailed: "删除车站方案失败",
        deleteTitle: "删除车站方案",
        deleteConfirm: "确定删除“{name}”及其全部布置图数据吗？",
      },
      group: {
        drawingObject: "绘图对象",
        curveDisplay: "曲线显示",
        displayScale: "显示比例",
      },
      scale: { x: "X", y: "Y" },
      mode: { select: "选择", draw: "绘图" },
      draw: {
        line: "线",
        node: "节点",
        signal: "信号机",
        switch: "道岔",
        insulation: "钢轨绝缘",
        route: "进路",
        buffer: "车挡",
        platform: "站台",
        annotation: "注释",
      },
      tools: {
        showCrossPoint: "显示交点",
        hideCrossPoint: "隐藏交点",
        snapLine: "处理虚接",
        separateLine: "线路分段",
        generateNode: "节点生成",
        generateSwitch: "道岔生成",
        generateCurve: "曲线生成",
        fitFullView: "显示全图",
      },
      curveDisplay: { arc: "圆弧", tangent: "切线" },
      messages: {
        saveSuccess: "保存成功：",
        saveFailed: "保存失败：",
        loadFailed: "获取失败：",
        loadSchemesFailed: "加载车站方案失败",
        readonly: "当前为只读模式，不能执行写操作",
      },
    },
  },
  en: {
    stationLayout: {
      toolbar: {
        edit: "Edit toolbar",
        compact: "Compact",
        full: "Full",
      },
      menu: {
        stationScheme: "Station Scheme",
        file: "File",
        loadData: "Load",
        saveData: "Save",
        gridSnap: "Grid Snap",
        objectSnap: "Object Snap",
        snapDistance: "Snap Distance",
        showGrid: "Show Grid",
        gridSpacing: "Grid Spacing",
        undo: "Undo",
        redo: "Redo",
        clearSelection: "Clear Selection",
        deleteSelection: "Delete Selection",
      },
      placeholders: {
        selectInstance: "Please select a station",
        selectStationScheme: "Select station scheme",
      },
      schemeManager: {
        manage: "Manage",
        title: "Station Scheme Manager",
        add: "Add",
        edit: "Edit",
        save: "Save",
        cancel: "Cancel",
        delete: "Delete",
        close: "Close",
        confirm: "Confirm",
        id: "Scheme ID",
        name: "Scheme Name",
        operation: "Operation",
        namePlaceholder: "Scheme name",
        idRequired: "Scheme ID is required",
        nameRequired: "Scheme name is required",
        createSuccess: "Station scheme created",
        createFailed: "Failed to create station scheme",
        updateSuccess: "Station scheme updated",
        updateFailed: "Failed to update station scheme",
        deleteSuccess: "Station scheme deleted",
        deleteFailed: "Failed to delete station scheme",
        deleteTitle: "Delete station scheme",
        deleteConfirm: "Delete \"{name}\" and all of its layout data?",
      },
      group: {
        drawingObject: "Drawing Object",
        curveDisplay: "Curve View",
        displayScale: "Display Scale",
      },
      scale: { x: "X", y: "Y" },
      mode: { select: "Select", draw: "Draw" },
      draw: {
        line: "Line",
        node: "Node",
        signal: "Signal",
        switch: "Switch",
        insulation: "Insulation",
        route: "Route",
        buffer: "Buffer",
        platform: "Platform",
        annotation: "Annotation",
      },
      tools: {
        showCrossPoint: "Show Cross",
        hideCrossPoint: "Hide Cross",
        snapLine: "Snap Line",
        separateLine: "Separate",
        generateNode: "Generate Node",
        generateSwitch: "Generate Switch",
        generateCurve: "Generate Curve",
        fitFullView: "Fit All",
      },
      curveDisplay: { arc: "Arc", tangent: "Tangent" },
      messages: {
        saveSuccess: "Save successful: ",
        saveFailed: "Save failed: ",
        loadFailed: "Load failed: ",
        loadSchemesFailed: "Failed to load station schemes",
        readonly: "Read-only mode does not allow write operations",
      },
    },
  },
} as const;

export function createStationLayoutTranslator(
  locale: keyof typeof stationLayoutMessages = "zh",
): StationLayoutTranslate {
  const root = stationLayoutMessages[locale] as unknown as Record<string, unknown>;
  return (key, parameters) => {
    let value: unknown = root;
    for (const segment of key.split(".")) {
      value = value && typeof value === "object"
        ? (value as Record<string, unknown>)[segment]
        : undefined;
    }

    const template = typeof value === "string" ? value : key;
    return template.replace(/\{([^{}]+)\}/g, (_match, name: string) =>
      String(parameters?.[name] ?? `{${name}}`));
  };
}
