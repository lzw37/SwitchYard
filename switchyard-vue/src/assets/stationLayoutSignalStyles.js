const signalSvgUrls = import.meta.glob("./signal_svg/*.svg", {
    eager: true,
    import: "default",
});

const signalSvgRawFiles = import.meta.glob("./signal_svg/*.svg", {
    eager: true,
    query: "?raw",
    import: "default",
});

export const DEFAULT_SIGNAL_TYPE = "DepartureSignal";

const signalNameLabels = {
    departuresignal: "出站信号机",
    homesignal: "进站信号机",
    shuntingsignal: "调车信号机",
    humpsignal: "驼峰信号机",
};

const signalBaseTypes = {
    departuresignal: "DepartureSignal",
    homesignal: "HomeSignal",
    shuntingsignal: "ShuntingSignal",
    humpsignal: "HumpSignal",
};

const signalCategoryOrder = ["DepartureSignal", "HomeSignal", "ShuntingSignal", "HumpSignal"];
const signalCategoryLeafTypes = new Set(["ShuntingSignal", "HumpSignal"]);

const signalPoleLabels = {
    high: "高柱",
    low: "矮柱",
};

const signalPoleTypes = {
    high: "High",
    low: "Low",
};

const placeholderSignalStyleAssets = {
    DepartureSignal: {
        className: "signal-departure",
        elements: [
            { tag: "circle", attrs: { cx: 38, cy: 17, r: 16, fill: "#fff" } },
            { tag: "circle", attrs: { cx: 38, cy: 17, r: 8, fill: "#fff" } },
            { tag: "circle", attrs: { cx: 70, cy: 17, r: 16, fill: "#009a3e" } },
            { tag: "circle", attrs: { cx: 103, cy: 17, r: 16, fill: "#e60012" } },
            { tag: "line", attrs: { x1: 22, y1: 17, x2: 1, y2: 17, fill: "none" } },
            { tag: "line", attrs: { x1: 1, y1: 1, x2: 1, y2: 33, fill: "none" } },
        ],
    },
    HomeSignal: {
        className: "signal-home",
        elements: [
            { tag: "line", attrs: { x1: 1, y1: 1, x2: 1, y2: 41, fill: "none" } },
            { tag: "line", attrs: { x1: 1, y1: 21, x2: 20, y2: 21, fill: "none" } },
            { tag: "rect", attrs: { x: 20, y: 5, width: 84, height: 32, rx: 4, fill: "#111827" } },
            { tag: "circle", attrs: { cx: 38, cy: 21, r: 10, fill: "#fff" } },
            { tag: "circle", attrs: { cx: 64, cy: 21, r: 10, fill: "#009a3e" } },
            { tag: "circle", attrs: { cx: 90, cy: 21, r: 10, fill: "#e60012" } },
        ],
    },
    ShuntingSignal: {
        className: "signal-shunting",
        elements: [
            { tag: "line", attrs: { x1: 1, y1: 12, x2: 20, y2: 12, fill: "none" } },
            { tag: "line", attrs: { x1: 1, y1: 1, x2: 1, y2: 33, fill: "none" } },
            { tag: "polygon", attrs: { points: "20,12 44,0 68,12 44,24", fill: "#1f2937" } },
            { tag: "circle", attrs: { cx: 36, cy: 12, r: 6, fill: "#fff" } },
            { tag: "circle", attrs: { cx: 52, cy: 12, r: 6, fill: "#60a5fa" } },
        ],
    },
    HumpSignal: {
        className: "signal-hump",
        elements: [
            { tag: "line", attrs: { x1: 1, y1: 1, x2: 1, y2: 39, fill: "none" } },
            { tag: "line", attrs: { x1: 1, y1: 20, x2: 20, y2: 20, fill: "none" } },
            { tag: "path", attrs: { d: "M20 36 L44 4 L68 36 Z", fill: "#facc15" } },
            { tag: "circle", attrs: { cx: 44, cy: 23, r: 8, fill: "#e60012" } },
            { tag: "line", attrs: { x1: 28, y1: 36, x2: 60, y2: 36, fill: "none" } },
        ],
    },
};

function normalizeSignalTypeKey(value) {
    return String(value ?? "")
        .trim()
        .replace(/[\s_-]+/g, "")
        .toLowerCase();
}

function toPascalCase(value) {
    return String(value ?? "")
        .split(/[\s_-]+/g)
        .filter(Boolean)
        .map((part) => part.charAt(0).toUpperCase() + part.slice(1).toLowerCase())
        .join("");
}

function getSignalSvgFileName(path) {
    return String(path).split("/").pop()?.replace(/\.svg$/i, "") || "";
}

function getSignalSvgSize(rawSvg) {
    const viewBoxMatch = String(rawSvg || "").match(/viewBox=["']\s*([-\d.]+)\s+([-\d.]+)\s+([-\d.]+)\s+([-\d.]+)\s*["']/i);
    if (viewBoxMatch) {
        return {
            width: Math.max(1, Number(viewBoxMatch[3]) || 1),
            height: Math.max(1, Number(viewBoxMatch[4]) || 1),
        };
    }

    const widthMatch = String(rawSvg || "").match(/\bwidth=["']([-\d.]+)/i);
    const heightMatch = String(rawSvg || "").match(/\bheight=["']([-\d.]+)/i);
    return {
        width: Math.max(1, Number(widthMatch?.[1]) || 48),
        height: Math.max(1, Number(heightMatch?.[1]) || 48),
    };
}

function parseSignalSvgFileName(fileName) {
    const parts = fileName.toLowerCase().split(/[\s_-]+/g).filter(Boolean);
    const baseKey = parts[0] || "";
    const baseType = signalBaseTypes[baseKey] || toPascalCase(baseKey);
    const categoryLabel = signalNameLabels[baseKey] || baseType;
    const labels = [categoryLabel];
    const typeParts = [baseType];

    const aspectIndex = parts.findIndex((part, index) => /^\d+$/.test(part) && parts[index + 1] === "aspect");
    const aspectCount = aspectIndex >= 0 ? parts[aspectIndex] : "";
    if (aspectIndex >= 0) {
        labels.push(`${aspectCount}显示`);
        typeParts.push(`${aspectCount}Aspect`);
    }

    const poleKey = parts.find((part) => signalPoleTypes[part]);
    if (poleKey) {
        labels.push(signalPoleLabels[poleKey]);
        typeParts.push(signalPoleTypes[poleKey]);
    }

    return {
        type: typeParts.join(""),
        label: labels.join(" "),
        categoryType: baseType,
        categoryLabel,
        poleKey: poleKey || "",
        poleLabel: poleKey ? signalPoleLabels[poleKey] : "",
        aspectCount,
        aspectLabel: aspectCount ? `${aspectCount}显示` : "",
    };
}

function parseSvgDeclarations(block) {
    return String(block || "")
        .split(";")
        .map((item) => item.trim())
        .filter(Boolean)
        .reduce((attrs, item) => {
            const separatorIndex = item.indexOf(":");
            if (separatorIndex <= 0) return attrs;

            const key = item.slice(0, separatorIndex).trim();
            const value = item.slice(separatorIndex + 1).trim();
            if (key && value) attrs[key] = value;
            return attrs;
        }, {});
}

function parseSvgClassRules(rawSvg) {
    const rules = {};
    const styleText = Array.from(String(rawSvg || "").matchAll(/<style[^>]*>([\s\S]*?)<\/style>/gi))
        .map((match) => match[1])
        .join("\n");
    const ruleRegex = /([^{}]+)\{([^{}]*)\}/g;
    let match;
    while ((match = ruleRegex.exec(styleText)) !== null) {
        const declarations = parseSvgDeclarations(match[2]);
        for (const selector of match[1].split(",")) {
            const classMatch = selector.trim().match(/^\.([A-Za-z0-9_-]+)$/);
            if (!classMatch) continue;

            const className = classMatch[1];
            rules[className] = {
                ...(rules[className] || {}),
                ...declarations,
            };
        }
    }

    return rules;
}

function parseSvgElementAttributes(attributeText, classRules, rotationTransform) {
    const attrs = {};
    const inlineStyleAttrs = {};
    let classValue = "";
    const attrRegex = /([:@A-Za-z0-9_.-]+)\s*=\s*(?:"([^"]*)"|'([^']*)')/g;
    let match;
    while ((match = attrRegex.exec(attributeText)) !== null) {
        const name = match[1];
        const value = match[2] ?? match[3] ?? "";
        if (name === "class") {
            classValue = value;
            continue;
        }
        if (name === "style") {
            Object.assign(inlineStyleAttrs, parseSvgDeclarations(value));
            continue;
        }

        attrs[name] = value;
    }

    const classAttrs = {};
    for (const className of classValue.split(/\s+/g).filter(Boolean)) {
        Object.assign(classAttrs, classRules[className] || {});
    }

    return {
        ...classAttrs,
        ...attrs,
        ...inlineStyleAttrs,
        transform: attrs.transform ? `${attrs.transform} ${rotationTransform}` : rotationTransform,
    };
}

function createFallbackSvgImageElement(href, size, rotationTransform) {
    return {
        tag: "image",
        attrs: {
            href,
            x: 0,
            y: 0,
            width: size.width,
            height: size.height,
            transform: rotationTransform,
        },
    };
}

function createInlineSvgElements(rawSvg, href, size, rotationTransform) {
    const classRules = parseSvgClassRules(rawSvg);
    const elementRegex = /<(line|circle|ellipse|rect|path|polygon|polyline)\b([^>]*)\/?>/gi;
    const elements = [];
    let match;
    while ((match = elementRegex.exec(String(rawSvg || ""))) !== null) {
        elements.push({
            tag: match[1].toLowerCase(),
            attrs: parseSvgElementAttributes(match[2], classRules, rotationTransform),
        });
    }

    return elements.length > 0
        ? elements
        : [createFallbackSvgImageElement(href, size, rotationTransform)];
}

function createSvgSignalEntries() {
    return Object.entries(signalSvgUrls)
        .map(([path, href]) => {
            const fileName = getSignalSvgFileName(path);
            if (!fileName || !href) return null;

            const signalMeta = parseSignalSvgFileName(fileName);
            const { type, label } = signalMeta;
            const rawSvg = signalSvgRawFiles[path];
            const size = getSignalSvgSize(rawSvg);
            const rotatedBounds = {
                minX: size.width - size.height,
                minY: size.height,
                maxX: size.width,
                maxY: size.height + size.width,
                width: size.height,
                height: size.width,
            };
            const rotationTransform = `rotate(-90 ${size.width} ${size.height})`;
            return {
                fileName,
                type,
                ...signalMeta,
                option: { label, value: type },
                asset: {
                    className: `signal-${fileName}`,
                    placement: "quadrant",
                    width: size.width,
                    height: size.height,
                    bounds: rotatedBounds,
                    elements: createInlineSvgElements(rawSvg, href, size, rotationTransform),
                },
            };
        })
        .filter(Boolean)
        .sort((a, b) => a.type.localeCompare(b.type, "en"));
}

function getSignalVariantLabel(entry) {
    const parts = [entry.poleLabel, entry.aspectLabel].filter(Boolean);
    return parts.length ? parts.join(" ") : "通用";
}

function getSignalVariantSortValue(entry) {
    const poleOrder = entry.poleKey === "high" ? 0 : entry.poleKey === "low" ? 1 : 2;
    const aspectOrder = Number(entry.aspectCount || Number.MAX_SAFE_INTEGER);
    return poleOrder * 1000 + (Number.isFinite(aspectOrder) ? aspectOrder : Number.MAX_SAFE_INTEGER);
}

function createSignalTypeMenuOptions(entries) {
    const byCategory = new Map();
    for (const entry of entries) {
        const categoryType = entry.categoryType || entry.type;
        if (!byCategory.has(categoryType)) {
            byCategory.set(categoryType, {
                label: entry.categoryLabel || entry.label,
                value: categoryType,
                order: signalCategoryOrder.includes(categoryType) ? signalCategoryOrder.indexOf(categoryType) : signalCategoryOrder.length,
                children: [],
            });
        }

        byCategory.get(categoryType).children.push({
            label: getSignalVariantLabel(entry),
            value: entry.type,
            sortValue: getSignalVariantSortValue(entry),
        });
    }

    return [...byCategory.values()]
        .sort((a, b) => a.order - b.order || a.label.localeCompare(b.label, "zh-Hans-CN"))
        .map((category) => {
            const children = category.children
                .sort((a, b) => a.sortValue - b.sortValue || a.label.localeCompare(b.label, "zh-Hans-CN"));
            if (signalCategoryLeafTypes.has(category.value)) {
                return {
                    label: category.label,
                    value: children[0]?.value || category.value,
                };
            }

            return {
                label: category.label,
                value: category.value,
                children: children.map(({ sortValue, ...child }) => child),
            };
        });
}

const svgSignalEntries = createSvgSignalEntries();
const svgSignalStyleAssets = Object.fromEntries(svgSignalEntries.map((entry) => [entry.type, entry.asset]));
const defaultDepartureSignalType = svgSignalEntries.find((entry) => entry.type.startsWith("DepartureSignal"))?.type || "DepartureSignal";
const defaultHomeSignalType = svgSignalEntries.find((entry) => entry.type.startsWith("HomeSignal"))?.type || "HomeSignal";
const defaultShuntingSignalType = svgSignalEntries.find((entry) => entry.type.startsWith("ShuntingSignal"))?.type || "ShuntingSignal";

export const signalStyleAssets = {
    ...placeholderSignalStyleAssets,
    ...svgSignalStyleAssets,
    DepartureSignal: svgSignalStyleAssets[defaultDepartureSignalType] || placeholderSignalStyleAssets.DepartureSignal,
    HomeSignal: svgSignalStyleAssets[defaultHomeSignalType] || placeholderSignalStyleAssets.HomeSignal,
    ShuntingSignal: svgSignalStyleAssets[defaultShuntingSignalType] || placeholderSignalStyleAssets.ShuntingSignal,
};

const placeholderSignalEntries = [
    {
        type: "HumpSignal",
        label: "驼峰信号机 通用",
        categoryType: "HumpSignal",
        categoryLabel: "驼峰信号机",
        poleKey: "",
        poleLabel: "",
        aspectCount: "",
        aspectLabel: "",
        option: { label: "驼峰信号机 通用", value: "HumpSignal" },
    },
];

const signalMenuEntries = [...svgSignalEntries, ...placeholderSignalEntries];

export const signalTypeOptions = signalMenuEntries.map((entry) => entry.option);
export const signalTypeMenuOptions = createSignalTypeMenuOptions(signalMenuEntries);

const svgSignalTypeAliases = Object.fromEntries(
    svgSignalEntries.flatMap((entry) => [
        [normalizeSignalTypeKey(entry.fileName), entry.type],
        [normalizeSignalTypeKey(entry.type), entry.type],
    ]),
);

const signalTypeAliases = {
    departure: defaultDepartureSignalType,
    departuresignal: defaultDepartureSignalType,
    home: defaultHomeSignalType,
    homesignal: defaultHomeSignalType,
    shunting: defaultShuntingSignalType,
    shuntingsignal: defaultShuntingSignalType,
    hump: "HumpSignal",
    humpsignal: "HumpSignal",
    ...svgSignalTypeAliases,
};

export function normalizeSignalType(value) {
    const rawType = String(value ?? "").trim();
    if (!rawType) return DEFAULT_SIGNAL_TYPE;
    return signalTypeAliases[normalizeSignalTypeKey(rawType)] || rawType;
}

export function getSignalStyleAsset(type) {
    const signalType = normalizeSignalType(type);
    return signalStyleAssets[signalType] || signalStyleAssets[DEFAULT_SIGNAL_TYPE];
}
