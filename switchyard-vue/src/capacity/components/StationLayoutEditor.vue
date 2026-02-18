<script setup>
import { computed, onMounted, ref } from "vue";

const props = defineProps({
    width: { type: Number, default: 1920 },
    height: { type: Number, default: 1080 },
});

const svgRef = ref(null);

const editModeCode = ref(0);
const drawingObject = ref("l");
const mouseGridSnapModeCode = ref(1);
const mouseObjectSnapModeCode = ref(1);
const snapDistance = ref(10);
const autoSeparateLineTolerance = ref(10);

const grid = { visible: true, verticalSpace: 20, horizontalSpace: 20 };
const cursorParam = ref({ size: 10, barVisible: false, barLength: 100, x: 200, y: 200 });
const anchorParam = { size: 10 };

const latestElementID = ref(0);

const tracks = ref([]);
const nodes = ref([]);
const signals = ref([]);
const insulationJoints = ref([]);
const platforms = ref([]);
const switches = ref([]);

const selectedLineIds = ref(new Set());
const selectedNodeIds = ref(new Set());
const selectedSignalIds = ref(new Set());
const selectedInsulationJointIds = ref(new Set());
const selectedSwitchIds = ref(new Set());
const selectedPlatformIds = ref(new Set());

const crossPoints = ref([]);
const perpendicularPoint = ref(null);

const tempLine = ref(null);
const tempSignal = ref({ visible: false, direction: "w", x: 0, y: 0 });
const tempInsulationJoint = ref({ visible: false, x: 0, y: 0 });
const tempNode = ref({ visible: false, x: 0, y: 0 });
const tempPlatformPosition = ref(null);

const movingAnchor = ref(null);

const finishedCmdList = ref([]);
const revokedCmdList = ref([]);

const anchorRects = computed(() => {
    const list = [];
    for (const line of tracks.value) {
        if (!selectedLineIds.value.has(line.id)) continue;
        const half = anchorParam.size / 2;
        const sp = { id: `sp${line.id}`, lineId: line.id, type: "sp", x: Number(line.x1) - half, y: Number(line.y1) - half };
        const ep = { id: `ep${line.id}`, lineId: line.id, type: "ep", x: Number(line.x2) - half, y: Number(line.y2) - half };
        const cp = {
            id: `cp${line.id}`,
            lineId: line.id,
            type: "cp",
            x: (Number(line.x1) + Number(line.x2)) / 2 - half,
            y: (Number(line.y1) + Number(line.y2)) / 2 - half,
        };
        list.push(sp, ep, cp);
    }
    return list;
});

const gridDots = computed(() => {
    const dots = [];
    if (!grid.visible) return dots;
    for (let x = 0; x < props.width; x += grid.verticalSpace) {
        for (let y = 0; y < props.height; y += grid.horizontalSpace) {
            dots.push({ x, y });
        }
    }
    return dots;
});

function cloneState() {
    return JSON.parse(
        JSON.stringify({
            latestElementID: latestElementID.value,
            tracks: tracks.value,
            nodes: nodes.value,
            signals: signals.value,
            insulationJoints: insulationJoints.value,
            platforms: platforms.value,
            switches: switches.value,
            selectedLineIds: [...selectedLineIds.value],
            selectedNodeIds: [...selectedNodeIds.value],
            selectedSignalIds: [...selectedSignalIds.value],
            selectedInsulationJointIds: [...selectedInsulationJointIds.value],
            selectedSwitchIds: [...selectedSwitchIds.value],
            selectedPlatformIds: [...selectedPlatformIds.value],
        })
    );
}

function applyState(state) {
    latestElementID.value = state.latestElementID;
    tracks.value = state.tracks || [];
    nodes.value = state.nodes || [];
    signals.value = state.signals || [];
    insulationJoints.value = state.insulationJoints || [];
    platforms.value = state.platforms || [];
    switches.value = state.switches || [];
    selectedLineIds.value = new Set(state.selectedLineIds || []);
    selectedNodeIds.value = new Set(state.selectedNodeIds || []);
    selectedSignalIds.value = new Set(state.selectedSignalIds || []);
    selectedInsulationJointIds.value = new Set(state.selectedInsulationJointIds || []);
    selectedSwitchIds.value = new Set(state.selectedSwitchIds || []);
    selectedPlatformIds.value = new Set(state.selectedPlatformIds || []);
}

function executeMutation(mutator) {
    finishedCmdList.value.push(cloneState());
    if (finishedCmdList.value.length > 30) {
        finishedCmdList.value.shift();
    }
    revokedCmdList.value = [];
    mutator();
}

function revoke() {
    if (finishedCmdList.value.length === 0) return;
    revokedCmdList.value.push(cloneState());
    const prev = finishedCmdList.value.pop();
    applyState(prev);
}

function redo() {
    if (revokedCmdList.value.length === 0) return;
    finishedCmdList.value.push(cloneState());
    const next = revokedCmdList.value.pop();
    applyState(next);
}

function nextId() {
    const id = String(latestElementID.value);
    latestElementID.value += 1;
    return id;
}

function clearSelectedLines() {
    selectedLineIds.value = new Set();
    movingAnchor.value = null;
}

function clearSelectedNodes() {
    selectedNodeIds.value = new Set();
}

function clearSelectedEquipment() {
    selectedSignalIds.value = new Set();
    selectedInsulationJointIds.value = new Set();
    selectedSwitchIds.value = new Set();
    selectedPlatformIds.value = new Set();
}

function setEditMode(code) {
    editModeCode.value = Number(code);
}

function setDrawingObject(obj) {
    drawingObject.value = obj;
    if (obj === "s") {
        startDrawingSignal();
    } else if (obj === "i") {
        startDrawingInsulationJoint();
    } else if (obj === "n") {
        startDrawingNode();
    } else if (obj === "p") {
        startDrawingPlatform();
    }
}

function setMouseGridSnapModeCode(code) {
    mouseGridSnapModeCode.value = Number(code);
}

function setMouseObjectSnapModeCode(code) {
    mouseObjectSnapModeCode.value = Number(code);
}

function getSnapObjects() {
    const list = [];
    for (const n of nodes.value) {
        list.push({ x: Number(n.x), y: Number(n.y) });
    }
    for (const a of anchorRects.value) {
        list.push({ x: Number(a.x) + anchorParam.size / 2, y: Number(a.y) + anchorParam.size / 2 });
    }
    return list;
}

function updateCursorPosition(clientX, clientY) {
    if (!svgRef.value) return;
    const rect = svgRef.value.getBoundingClientRect();
    const x = clientX - rect.left;
    const y = clientY - rect.top;

    let snapX = x;
    let snapY = y;

    if (mouseGridSnapModeCode.value === 1) {
        snapX = Math.round(x / grid.horizontalSpace) * grid.horizontalSpace;
        snapY = Math.round(y / grid.verticalSpace) * grid.verticalSpace;
    }

    if (mouseObjectSnapModeCode.value === 1) {
        const snapObjs = getSnapObjects();
        for (const obj of snapObjs) {
            const dist = Math.hypot(obj.x - x, obj.y - y);
            if (dist <= snapDistance.value) {
                snapX = obj.x;
                snapY = obj.y;
                break;
            }
        }
    }

    cursorParam.value.x = snapX;
    cursorParam.value.y = snapY;

    if (editModeCode.value === 1) {
        snapPointToLine(x, y);
    }
}

function beginDrawLine(x, y) {
    tempLine.value = { x1: x, y1: y, x2: x, y2: y };
}

function drawingLineMouseMove(x, y) {
    if (!tempLine.value) return;
    tempLine.value.x2 = x;
    tempLine.value.y2 = y;
}

function endDrawLine() {
    if (!tempLine.value) return;
    const line = {
        id: nextId(),
        x1: tempLine.value.x1,
        y1: tempLine.value.y1,
        x2: tempLine.value.x2,
        y2: tempLine.value.y2,
        fromNodeID: "",
        toNodeID: "",
    };
    executeMutation(() => {
        tracks.value.push(line);
    });
    tempLine.value = null;
}

function selectLine(lineId) {
    selectedLineIds.value = new Set([...selectedLineIds.value, lineId]);
}

function deleteLine() {
    if (selectedLineIds.value.size === 0) return;
    executeMutation(() => {
        tracks.value = tracks.value.filter((line) => !selectedLineIds.value.has(line.id));
        selectedLineIds.value = new Set();
        movingAnchor.value = null;
    });
}

function deleteNode() {
    if (selectedNodeIds.value.size === 0) return;
    executeMutation(() => {
        nodes.value = nodes.value.filter((n) => !selectedNodeIds.value.has(n.id));
        selectedNodeIds.value = new Set();
    });
}

function deleteEquipment() {
    executeMutation(() => {
        signals.value = signals.value.filter((s) => !selectedSignalIds.value.has(s.id));
        insulationJoints.value = insulationJoints.value.filter((i) => !selectedInsulationJointIds.value.has(i.id));
        switches.value = switches.value.filter((s) => !selectedSwitchIds.value.has(s.id));
        platforms.value = platforms.value.filter((p) => !selectedPlatformIds.value.has(p.id));
        clearSelectedEquipment();
    });
}

function getLineList() {
    return tracks.value.map((line) => ({ ...line }));
}

function getNodeList() {
    return nodes.value.map((node) => ({ ...node }));
}

function calculatePointDist(p1, p2) {
    return Math.trunc(Math.hypot(Number(p2.x) - Number(p1.x), Number(p2.y) - Number(p1.y)));
}

function calculateCrossPoint(l1, l2) {
    const denominatorX = (l2.x1 - l2.x2) * (l1.y1 - l1.y2) - (l1.x1 - l1.x2) * (l2.y1 - l2.y2);
    const denominatorY = (l2.y1 - l2.y2) * (l1.x1 - l1.x2) - (l1.y1 - l1.y2) * (l2.x1 - l2.x2);
    if (denominatorX === 0 || denominatorY === 0) return null;

    const x = Math.round(((l2.x1 - l2.x2) * (l1.x2 * l1.y1 - l1.x1 * l1.y2) - (l1.x1 - l1.x2) * (l2.x2 * l2.y1 - l2.x1 * l2.y2)) / denominatorX);
    const y = Math.round(((l2.y1 - l2.y2) * (l1.y2 * l1.x1 - l1.y1 * l1.x2) - (l1.y1 - l1.y2) * (l2.y2 * l2.x1 - l2.y1 * l2.x2)) / denominatorY);
    return { x, y };
}

function getCrossPointRelation(l1, l2, crossPoint) {
    let onl1 = false;
    let onl2 = false;

    const crossPointOnl1Start = calculatePointDist(crossPoint, { x: l1.x1, y: l1.y1 }) === 0;
    const crossPointOnl1End = calculatePointDist(crossPoint, { x: l1.x2, y: l1.y2 }) === 0;
    const crossPointOnl2Start = calculatePointDist(crossPoint, { x: l2.x1, y: l2.y1 }) === 0;
    const crossPointOnl2End = calculatePointDist(crossPoint, { x: l2.x2, y: l2.y2 }) === 0;

    const distl21 = calculatePointDist(crossPoint, { x: l2.x1, y: l2.y1 });
    const distl22 = calculatePointDist(crossPoint, { x: l2.x2, y: l2.y2 });
    const distl11 = calculatePointDist(crossPoint, { x: l1.x1, y: l1.y1 });
    const distl12 = calculatePointDist(crossPoint, { x: l1.x2, y: l1.y2 });

    const snapPointList = [];
    if (distl11 < autoSeparateLineTolerance.value) snapPointList.push({ line: l1, point: 1 });
    if (distl12 < autoSeparateLineTolerance.value) snapPointList.push({ line: l1, point: 2 });
    if (distl21 < autoSeparateLineTolerance.value) snapPointList.push({ line: l2, point: 1 });
    if (distl22 < autoSeparateLineTolerance.value) snapPointList.push({ line: l2, point: 2 });

    if (Math.min(l1.x1, l1.x2) <= crossPoint.x && crossPoint.x <= Math.max(l1.x1, l1.x2) && Math.min(l1.y1, l1.y2) <= crossPoint.y && crossPoint.y <= Math.max(l1.y1, l1.y2)) {
        onl1 = true;
    }
    if (Math.min(l2.x1, l2.x2) <= crossPoint.x && crossPoint.x <= Math.max(l2.x1, l2.x2) && Math.min(l2.y1, l2.y2) <= crossPoint.y && crossPoint.y <= Math.max(l2.y1, l2.y2)) {
        onl2 = true;
    }

    if (onl1 && onl2) {
        if ((crossPointOnl1Start || crossPointOnl1End) && (crossPointOnl2Start || crossPointOnl2End)) {
            return { code: 0 };
        }
        if (crossPointOnl1Start || crossPointOnl1End || crossPointOnl2Start || crossPointOnl2End) {
            const breakingLine = !crossPointOnl1Start && !crossPointOnl1End ? l1 : l2;
            return { code: 1, breakingLineList: [breakingLine] };
        }
        if (!crossPointOnl1Start && !crossPointOnl1End && !crossPointOnl2Start && !crossPointOnl2End) {
            if (Math.min(distl11, distl12, distl21, distl22) < autoSeparateLineTolerance.value) {
                return { code: 3, snapPointList };
            }
            return { code: 2, breakingLineList: [l1, l2] };
        }
    }

    if (!onl2 && !onl1 && Math.min(distl11, distl12) <= autoSeparateLineTolerance.value && Math.min(distl21, distl22) <= autoSeparateLineTolerance.value) {
        return { code: 4, snapPointList };
    }

    return { code: 6 };
}

function markCrossPoint() {
    const lineSet = getLineList();
    const points = [];
    for (let i = 0; i < lineSet.length; i += 1) {
        for (let j = i + 1; j < lineSet.length; j += 1) {
            const l1 = lineSet[i];
            const l2 = lineSet[j];
            const crossPoint = calculateCrossPoint(l1, l2);
            if (!crossPoint) continue;
            const relation = getCrossPointRelation(l1, l2, crossPoint);
            crossPoint.relation = relation;
            if (relation.code <= 4) {
                points.push({ ...crossPoint, code: relation.code, relation });
            }
        }
    }
    crossPoints.value = points;
    return points;
}

function removeCrossPoint() {
    crossPoints.value = [];
}

function tryLineSnapToCrossPoint(line, pointid, crossPoint) {
    const target = tracks.value.find((item) => item.id === line.id);
    if (!target) return;
    target[`x${pointid}`] = crossPoint.x;
    target[`y${pointid}`] = crossPoint.y;
}

function snapLine() {
    executeMutation(() => {
        const cps = markCrossPoint();
        for (const cp of cps) {
            if (cp.relation.snapPointList) {
                for (const r of cp.relation.snapPointList) {
                    tryLineSnapToCrossPoint(r.line, r.point, cp);
                }
            }
        }
        markCrossPoint();
    });
}

function autoSeparateLine() {
    executeMutation(() => {
        const cps = markCrossPoint();
        const candidateLineDict = {};

        for (const p of cps) {
            if (p.relation.code === 1 || p.relation.code === 2) {
                if (!p.relation.breakingLineList) continue;
                for (const bl of p.relation.breakingLineList) {
                    if (!candidateLineDict[bl.id]) {
                        candidateLineDict[bl.id] = { line: bl, pointList: [] };
                    }
                    candidateLineDict[bl.id].pointList.push(p);
                }
            }
        }

        const nextTracks = [...tracks.value.filter((line) => !candidateLineDict[line.id])];

        for (const lineID of Object.keys(candidateLineDict)) {
            const line = candidateLineDict[lineID].line;
            const pList = candidateLineDict[lineID].pointList;
            pList.push({ x: line.x1, y: line.y1 });
            pList.push({ x: line.x2, y: line.y2 });

            for (const p of pList) {
                p.positionRate = line.x2 === line.x1 ? (p.y - line.y1) / (line.y2 - line.y1 || 1) : (p.x - line.x1) / (line.x2 - line.x1);
            }
            pList.sort((a, b) => (a.positionRate < b.positionRate ? -1 : 1));

            for (let idx = 0; idx < pList.length - 1; idx += 1) {
                const p1 = pList[idx];
                const p2 = pList[idx + 1];
                nextTracks.push({
                    id: `${line.id}s${idx}`,
                    x1: p1.x,
                    y1: p1.y,
                    x2: p2.x,
                    y2: p2.y,
                    fromNodeID: "",
                    toNodeID: "",
                });
            }
        }

        tracks.value = nextTracks;
        markCrossPoint();
    });
}

function startDrawingSignal() {
    tempSignal.value.visible = true;
}

function drawingSignalMouseMove(x, y) {
    if (!tempSignal.value.visible) return;
    tempSignal.value.x = x;
    tempSignal.value.y = y;
}

function drawingSignalMouseDown(x, y) {
    const bindingNode = nodes.value.find((n) => Number(n.x) === Number(x) && Number(n.y) === Number(y));
    if (!bindingNode) return;
    executeMutation(() => {
        signals.value.push({
            id: nextId(),
            type: "departure",
            position: { x, y },
            direction: tempSignal.value.direction,
            bindingNodeID: bindingNode.id,
        });
    });
}

function startDrawingInsulationJoint() {
    tempInsulationJoint.value.visible = true;
}

function drawingInsulationJointMouseMove(x, y) {
    if (!tempInsulationJoint.value.visible) return;
    tempInsulationJoint.value.x = x;
    tempInsulationJoint.value.y = y;
}

function drawingInsulationJointMouseDown(x, y) {
    const bindingNode = nodes.value.find((n) => Number(n.x) === Number(x) && Number(n.y) === Number(y));
    if (!bindingNode) return;
    executeMutation(() => {
        insulationJoints.value.push({
            id: nextId(),
            type: "normal",
            position: { x, y },
            bindingNodeID: bindingNode.id,
        });
    });
}

function startDrawingNode() {
    tempNode.value.visible = true;
}

function getPerpendicular(x1, y1, x2, y2, x0, y0) {
    if (y1 === y2) return { x: x0, y: y1 };
    if (x1 === x2) return { x: x1, y: y0 };
    const a = y1 - y2;
    const b = -(x1 - x2);
    const c = -x2 * (y1 - y2) + y2 * (x1 - x2);
    const x = (b * b * x0 - a * b * y0 - a * c) / (a * a + b * b);
    const y = (a * a * y0 - a * b * x0 - b * c) / (a * a + b * b);
    return { x, y };
}

function calculatePointLineSnap(x1, y1, x2, y2, x0, y0) {
    const perpendicular = getPerpendicular(x1, y1, x2, y2, x0, y0);
    if (perpendicular.x < Math.min(x1, x2) || perpendicular.x > Math.max(x1, x2)) return null;
    if (perpendicular.y < Math.min(y1, y2) || perpendicular.y > Math.max(y1, y2)) return null;
    const dist = calculatePointDist(perpendicular, { x: x0, y: y0 });
    if (dist > 30) return null;
    return perpendicular;
}

function snapPointToLine(x, y) {
    let found = null;
    for (const line of tracks.value) {
        const p = calculatePointLineSnap(Number(line.x1), Number(line.y1), Number(line.x2), Number(line.y2), x, y);
        if (p) {
            found = p;
            break;
        }
    }
    perpendicularPoint.value = found;
    return found;
}

function drawingNodeMouseMove(x, y) {
    if (!tempNode.value.visible) return;
    tempNode.value.x = x;
    tempNode.value.y = y;
}

function drawingNodeMouseDown(x, y) {
    const lineList = getLineList();
    let minDistLine = null;
    let minSnapPoint = null;
    let minDist = Number.MAX_SAFE_INTEGER;

    for (const line of lineList) {
        const snapPoint = calculatePointLineSnap(Number(line.x1), Number(line.y1), Number(line.x2), Number(line.y2), x, y);
        if (!snapPoint) continue;
        const dist = calculatePointDist(snapPoint, { x, y });
        if (dist < minDist) {
            minDist = dist;
            minDistLine = line;
            minSnapPoint = snapPoint;
        }
    }

    if (!minSnapPoint || !minDistLine) return;

    executeMutation(() => {
        const n = { id: nextId(), x, y, adjacentLineIDList: [`${minDistLine.id}s1`, `${minDistLine.id}s2`] };

        const l1 = {
            id: `${minDistLine.id}s1`,
            x1: minDistLine.x1,
            y1: minDistLine.y1,
            x2: x,
            y2: y,
            fromNodeID: minDistLine.fromNodeID,
            toNodeID: n.id,
        };
        const l2 = {
            id: `${minDistLine.id}s2`,
            x1: x,
            y1: y,
            x2: minDistLine.x2,
            y2: minDistLine.y2,
            fromNodeID: n.id,
            toNodeID: minDistLine.toNodeID,
        };

        tracks.value = tracks.value.filter((line) => line.id !== minDistLine.id);
        tracks.value.push(l1, l2);
        nodes.value.push(n);
    });
}

function autoGenerateNodes() {
    executeMutation(() => {
        nodes.value = [];
        const nodeList = [];
        const getOrCreateNode = (x, y) => {
            const found = nodeList.find((n) => Number(n.x) === Number(x) && Number(n.y) === Number(y));
            if (found) return found;
            const n = { id: nextId(), x: Number(x), y: Number(y), adjacentLineIDList: [] };
            nodeList.push(n);
            return n;
        };

        for (const l of tracks.value) {
            const n1 = getOrCreateNode(l.x1, l.y1);
            const n2 = getOrCreateNode(l.x2, l.y2);
            l.fromNodeID = n1.id;
            l.toNodeID = n2.id;
            n1.adjacentLineIDList.push(l.id);
            n2.adjacentLineIDList.push(l.id);
        }

        nodes.value = nodeList;
    });
}

function buildSwitch(node) {
    const adjacentLines = tracks.value.filter((line) => node.adjacentLineIDList.includes(line.id));
    const vectorList = [];

    for (const line of adjacentLines) {
        if (line.fromNodeID === node.id) {
            vectorList.push({ x: Number(line.x2) - Number(line.x1), y: Number(line.y2) - Number(line.y1), lineID: line.id });
        } else {
            vectorList.push({ x: Number(line.x1) - Number(line.x2), y: Number(line.y1) - Number(line.y2), lineID: line.id });
        }
    }

    let acuteAngleNum = 0;
    let obtuseAngleNum = 0;
    let isSingleSwitch = false;

    for (let i = 0; i < vectorList.length; i += 1) {
        for (let j = i + 1; j < vectorList.length; j += 1) {
            const innerProduct = vectorList[i].x * vectorList[j].x + vectorList[i].y * vectorList[j].y;
            const absI = Math.hypot(vectorList[i].x, vectorList[i].y);
            const absJ = Math.hypot(vectorList[j].x, vectorList[j].y);
            const cos = innerProduct / (absI * absJ);
            if (Math.abs(cos + 1) < 0.01) isSingleSwitch = true;
            if (cos > 0 && cos < 1) acuteAngleNum += 1;
            else if (cos < 0 && cos >= -1) obtuseAngleNum += 1;
        }
    }

    let switchType = "unknown";
    if (acuteAngleNum === 1 && obtuseAngleNum === 2) {
        switchType = isSingleSwitch ? "single" : "symmetrical";
    } else if (acuteAngleNum === 2) {
        switchType = "slip";
    }

    return {
        id: nextId(),
        type: switchType,
        position: { x: node.x, y: node.y },
        bindingNodeID: node.id,
        branchVectorList: vectorList,
    };
}

function autoGenerateSwitches() {
    executeMutation(() => {
        const generated = [];
        for (const n of nodes.value) {
            if (n.adjacentLineIDList.length === 3 || n.adjacentLineIDList.length === 4) {
                generated.push(buildSwitch(n));
            }
        }
        switches.value = generated;
    });
}

function startDrawingPlatform() {
    tempPlatformPosition.value = null;
}

function drawingPlatformMouseMove(x, y) {
    if (!tempPlatformPosition.value) return;
    tempPlatformPosition.value.endX = x;
    tempPlatformPosition.value.endY = y;
}

function drawingPlatformMouseDown(x, y) {
    if (!tempPlatformPosition.value) {
        tempPlatformPosition.value = { startX: x, startY: y, endX: x, endY: y };
        return;
    }

    executeMutation(() => {
        const startX = tempPlatformPosition.value.startX;
        const endX = tempPlatformPosition.value.endX;
        const startY = tempPlatformPosition.value.startY;
        const endY = tempPlatformPosition.value.endY;

        platforms.value.push({
            id: nextId(),
            x: Math.min(startX, endX),
            y: Math.min(startY, endY),
            width: Math.abs(endX - startX),
            height: Math.abs(endY - startY),
        });
    });

    tempPlatformPosition.value = null;
}

const tempPlatformView = computed(() => {
    if (!tempPlatformPosition.value) {
        return { x: 0, y: 0, width: 0, height: 0 };
    }
    const startX = tempPlatformPosition.value.startX;
    const endX = tempPlatformPosition.value.endX;
    const startY = tempPlatformPosition.value.startY;
    const endY = tempPlatformPosition.value.endY;
    return {
        x: Math.min(startX, endX),
        y: Math.min(startY, endY),
        width: Math.abs(endX - startX),
        height: Math.abs(endY - startY),
    };
});

function buildJsonData() {
    return JSON.stringify({
        metadata: { latestElementID: latestElementID.value },
        tracks: tracks.value,
        nodes: nodes.value,
        signals: signals.value,
        insulationJoints: insulationJoints.value,
        platforms: platforms.value,
        switches: switches.value,
    });
}

function clearElements() {
    tracks.value = [];
    nodes.value = [];
    signals.value = [];
    insulationJoints.value = [];
    platforms.value = [];
    switches.value = [];
    clearSelectedLines();
    clearSelectedNodes();
    clearSelectedEquipment();
}

function loadDataFromJson(jsonObj) {
    executeMutation(() => {
        clearElements();
        latestElementID.value = Number(jsonObj?.metadata?.latestElementID || 0);
        tracks.value = (jsonObj?.tracks || []).map((track) => ({ ...track }));
        nodes.value = (jsonObj?.nodes || []).map((node) => ({ ...node }));
        signals.value = (jsonObj?.signals || []).map((signal) => ({ ...signal }));
        insulationJoints.value = (jsonObj?.insulationJoints || []).map((ij) => ({ ...ij }));
        platforms.value = (jsonObj?.platforms || []).map((platform) => ({ ...platform }));
        switches.value = (jsonObj?.switches || []).map((sw) => ({ ...sw }));
    });
}

function handleLineClick(lineId) {
    if (editModeCode.value !== 0) return;
    selectLine(lineId);
}

function handleNodeClick(nodeId) {
    if (editModeCode.value !== 0) return;
    selectedNodeIds.value = new Set([...selectedNodeIds.value, nodeId]);
}

function handleSignalClick(signalId) {
    if (editModeCode.value !== 0) return;
    selectedSignalIds.value = new Set([...selectedSignalIds.value, signalId]);
}

function handleInsulationJointClick(id) {
    if (editModeCode.value !== 0) return;
    selectedInsulationJointIds.value = new Set([...selectedInsulationJointIds.value, id]);
}

function handleSwitchClick(id) {
    if (editModeCode.value !== 0) return;
    selectedSwitchIds.value = new Set([...selectedSwitchIds.value, id]);
}

function handlePlatformClick(id) {
    if (editModeCode.value !== 0) return;
    selectedPlatformIds.value = new Set([...selectedPlatformIds.value, id]);
}

function handleAnchorDown(anchor) {
    if (editModeCode.value !== 0) return;
    movingAnchor.value = anchor;
}

function moveSelectedAnchor(x, y) {
    if (!movingAnchor.value) return;
    executeMutation(() => {
        const line = tracks.value.find((item) => item.id === movingAnchor.value.lineId);
        if (!line) return;

        if (movingAnchor.value.type === "sp") {
            line.x1 = x;
            line.y1 = y;
        } else if (movingAnchor.value.type === "ep") {
            line.x2 = x;
            line.y2 = y;
        } else {
            const oldCX = (Number(line.x1) + Number(line.x2)) / 2;
            const oldCY = (Number(line.y1) + Number(line.y2)) / 2;
            const dx = x - oldCX;
            const dy = y - oldCY;
            line.x1 = Number(line.x1) + dx;
            line.y1 = Number(line.y1) + dy;
            line.x2 = Number(line.x2) + dx;
            line.y2 = Number(line.y2) + dy;
        }
    });
    movingAnchor.value = null;
}

function onMouseMove(event) {
    updateCursorPosition(event.clientX, event.clientY);
    svgRef.value?.focus({ preventScroll: true });
    const x = cursorParam.value.x;
    const y = cursorParam.value.y;

    if (editModeCode.value === 0) {
        return;
    }

    if (drawingObject.value === "l") {
        drawingLineMouseMove(x, y);
    } else if (drawingObject.value === "s") {
        drawingSignalMouseMove(x, y);
    } else if (drawingObject.value === "i") {
        drawingInsulationJointMouseMove(x, y);
    } else if (drawingObject.value === "n") {
        drawingNodeMouseMove(x, y);
    } else if (drawingObject.value === "p") {
        drawingPlatformMouseMove(x, y);
    }
}

function onMouseDown() {
    const x = cursorParam.value.x;
    const y = cursorParam.value.y;

    if (editModeCode.value === 0) {
        if (movingAnchor.value) {
            moveSelectedAnchor(x, y);
        }
        return;
    }

    if (drawingObject.value === "l") {
        if (!tempLine.value) beginDrawLine(x, y);
        else endDrawLine();
    } else if (drawingObject.value === "s") {
        drawingSignalMouseDown(x, y);
    } else if (drawingObject.value === "i") {
        drawingInsulationJointMouseDown(x, y);
    } else if (drawingObject.value === "n") {
        drawingNodeMouseDown(x, y);
    } else if (drawingObject.value === "p") {
        drawingPlatformMouseDown(x, y);
    }
}

function onKeydown(event) {
    if (event.key === "Escape") {
        clearSelectedEquipment();
        clearSelectedLines();
        clearSelectedNodes();
    }
    if (event.ctrlKey && event.key === "z") {
        event.preventDefault();
        revoke();
    }
    if (event.ctrlKey && event.key === "y") {
        event.preventDefault();
        redo();
    }
    if (editModeCode.value === 1 && drawingObject.value === "s") {
        const candidateDirection = ["w", "e", "s", "d"];
        if (candidateDirection.includes(event.key)) {
            tempSignal.value.direction = event.key;
            drawingSignalMouseMove(cursorParam.value.x, cursorParam.value.y);
        }
    }
}

function isSignalSelected(id) {
    return selectedSignalIds.value.has(id);
}

function isLineSelected(id) {
    return selectedLineIds.value.has(id);
}

function isNodeSelected(id) {
    return selectedNodeIds.value.has(id);
}

function isInsulationJointSelected(id) {
    return selectedInsulationJointIds.value.has(id);
}

function isSwitchSelected(id) {
    return selectedSwitchIds.value.has(id);
}

function isPlatformSelected(id) {
    return selectedPlatformIds.value.has(id);
}

function signalTransform(signal) {
    const directionView = {
        e: { coefScaleX: 1, coefShiftY: 1 },
        w: { coefScaleX: -1, coefShiftY: 1 },
        s: { coefScaleX: -1, coefShiftY: 0 },
        d: { coefScaleX: 1, coefShiftY: 0 },
    };
    const d = directionView[signal.direction || "e"];
    return `translate(${signal.position.x},${signal.position.y - 20 * d.coefShiftY})scale(${0.5 * d.coefScaleX},0.5)`;
}

function tempSignalTransform() {
    return signalTransform({ position: { x: tempSignal.value.x, y: tempSignal.value.y }, direction: tempSignal.value.direction });
}

function switchTransform(sw) {
    return `translate(${sw.position.x},${sw.position.y})`;
}

function switchBranch(sw, lineVec) {
    const len = 10;
    if (lineVec.x === 0) {
        return { x1: 0, y1: 0, x2: 0, y2: lineVec.y > 0 ? len : -len };
    }

    const k = lineVec.y / lineVec.x;
    const solx1 = Math.sqrt((len * len) / (1 + k * k));
    const soly1 = solx1 * k;
    const solx2 = -Math.sqrt((len * len) / (1 + k * k));
    const soly2 = solx2 * k;

    const innerProduct = solx1 * lineVec.x + soly1 * lineVec.y;
    const absVec = Math.hypot(lineVec.x, lineVec.y);
    const absSol = Math.hypot(solx1, soly1);
    const cos = innerProduct / (absVec * absSol);

    if (Math.abs(cos - 1) < 0.01) {
        return { x1: 0, y1: 0, x2: solx1, y2: soly1 };
    }
    return { x1: 0, y1: 0, x2: solx2, y2: soly2 };
}

onMounted(() => {
    svgRef.value?.focus({ preventScroll: true });
});

defineExpose({
    editModeCode,
    drawingObject,
    mouseGridSnapModeCode,
    mouseObjectSnapModeCode,
    setEditMode,
    setDrawingObject,
    setMouseGridSnapModeCode,
    setMouseObjectSnapModeCode,
    clearSelectedLines,
    clearSelectedNodes,
    clearSelectedEquipment,
    deleteLine,
    deleteNode,
    deleteEquipment,
    revoke,
    redo,
    buildJsonData,
    loadDataFromJson,
    autoSeparateLine,
    markCrossPoint,
    removeCrossPoint,
    snapLine,
    autoGenerateNodes,
    autoGenerateSwitches,
    startDrawingSignal,
    startDrawingInsulationJoint,
    startDrawingNode,
    startDrawingPlatform,
    clearElements,
});
</script>

<template>
    <svg id="layout-editor-svg" ref="svgRef" tabindex="0" @mousemove="onMouseMove" @mousedown="onMouseDown"
        @keydown="onKeydown">
        <g id="grid">
            <circle v-for="(dot, idx) in gridDots" :key="`g-${idx}`" class="griddot" :cx="dot.x" :cy="dot.y" r="0.5" />
        </g>

        <g id="linegroup">
            <line v-for="line in tracks" :id="line.id" :key="`line-${line.id}`" class="track"
                :class="{ 'track-selected': isLineSelected(line.id) }" :x1="line.x1" :y1="line.y1" :x2="line.x2"
                :y2="line.y2" @click.stop="handleLineClick(line.id)" />

            <line v-if="tempLine" class="track track-temp" :x1="tempLine.x1" :y1="tempLine.y1" :x2="tempLine.x2"
                :y2="tempLine.y2" />

            <rect v-for="anchor in anchorRects" :id="anchor.id" :key="anchor.id" class="anchor snapobj" :x="anchor.x"
                :y="anchor.y" :width="anchorParam.size" :height="anchorParam.size"
                @mousedown.stop="handleAnchorDown(anchor)" />

            <circle v-for="(cp, idx) in crossPoints" :key="`cp-${idx}`" class="crosspoint" :class="`rela${cp.code}`"
                :cx="cp.x" :cy="cp.y" r="4" />

            <circle v-if="perpendicularPoint" class="perpendicular" :cx="perpendicularPoint.x"
                :cy="perpendicularPoint.y" r="4" />
        </g>

        <g id="nodegroup">
            <circle v-for="node in nodes" :id="node.id" :key="`node-${node.id}`" class="node snapobj"
                :class="{ 'node-selected': isNodeSelected(node.id) }" :cx="node.x" :cy="node.y" r="5"
                @mousedown.stop="handleNodeClick(node.id)" />

            <circle v-if="tempNode.visible" class="node node-temp" :cx="tempNode.x" :cy="tempNode.y" r="5" />
        </g>

        <g id="signalgroup">
            <g v-for="signal in signals" :id="String(signal.id)" :key="`signal-${signal.id}`"
                class="signal signal-departure" :class="{ 'signal-selected': isSignalSelected(signal.id) }"
                :transform="signalTransform(signal)" @mousedown.stop="handleSignalClick(signal.id)">
                <circle cx="38" cy="17" r="16" style="fill:#fff;" />
                <circle cx="38" cy="17" r="8" style="fill:#fff;" />
                <circle cx="70" cy="17" r="16" style="fill:#009a3e;" />
                <circle cx="103" cy="17" r="16" style="fill:#e60012;" />
                <line x1="22" y1="17" x2="1" y2="17" style="fill:none;" />
                <line x1="1" y1="1" x2="1" y2="33" style="fill:none;" />
                <text class="signalname" x="0" y="45">SIGNAL</text>
            </g>

            <g v-if="tempSignal.visible" id="tempsignal" class="signal signal-departure signal-temp"
                :transform="tempSignalTransform()">
                <circle cx="38" cy="17" r="16" style="fill:#fff;" />
                <circle cx="38" cy="17" r="8" style="fill:#fff;" />
                <circle cx="70" cy="17" r="16" style="fill:#009a3e;" />
                <circle cx="103" cy="17" r="16" style="fill:#e60012;" />
                <line x1="22" y1="17" x2="1" y2="17" style="fill:none;" />
                <line x1="1" y1="1" x2="1" y2="33" style="fill:none;" />
            </g>
        </g>

        <g id="insulationjointgroup">
            <g v-for="ij in insulationJoints" :id="String(ij.id)" :key="`ij-${ij.id}`"
                class="insulationjoint insulationjoint-normal"
                :class="{ 'insulationjoint-selected': isInsulationJointSelected(ij.id) }"
                :transform="`translate(${ij.position.x},${ij.position.y})`"
                @mousedown.stop="handleInsulationJointClick(ij.id)">
                <line x1="0" y1="-5" x2="0" y2="5" />
            </g>

            <g v-if="tempInsulationJoint.visible" id="tempinsulationjoint" class="insulationjoint insulationjoint-temp">
                <line :x1="tempInsulationJoint.x" :x2="tempInsulationJoint.x" :y1="tempInsulationJoint.y - 5"
                    :y2="tempInsulationJoint.y + 5" />
            </g>
        </g>

        <g id="switchgroup">
            <g v-for="sw in switches" :id="String(sw.id)" :key="`sw-${sw.id}`" class="switch"
                :class="{ 'switch-selected': isSwitchSelected(sw.id) }" :transform="switchTransform(sw)"
                @mousedown.stop="handleSwitchClick(sw.id)">
                <line v-for="(lineVec, idx) in sw.branchVectorList" :key="`sw-line-${sw.id}-${idx}`"
                    class="switchbranch" :x1="switchBranch(sw, lineVec).x1" :y1="switchBranch(sw, lineVec).y1"
                    :x2="switchBranch(sw, lineVec).x2" :y2="switchBranch(sw, lineVec).y2" />
                <text class="switchname" x="4" y="-4">SWITCH</text>
            </g>
        </g>

        <g id="platformgroup">
            <g v-for="platform in platforms" :id="String(platform.id)" :key="`platform-${platform.id}`" class="platform"
                :class="{ 'platform-selected': isPlatformSelected(platform.id) }"
                @mousedown.stop="handlePlatformClick(platform.id)">
                <rect :x="platform.x" :y="platform.y" :width="platform.width" :height="platform.height" />
                <text class="platformname" :x="platform.x + platform.width / 2" :y="platform.y + platform.height / 2">
                    PLATFORM
                </text>
            </g>

            <rect class="platform platform-temp" :x="tempPlatformView.x" :y="tempPlatformView.y"
                :width="tempPlatformView.width" :height="tempPlatformView.height" />
        </g>

        <g id="cursor">
            <rect class="cursor" :x="cursorParam.x - cursorParam.size / 2" :y="cursorParam.y - cursorParam.size / 2"
                :width="cursorParam.size" :height="cursorParam.size" />
            <line id="cursorlineh" class="cursor" :x1="cursorParam.x - cursorParam.barLength / 2"
                :x2="cursorParam.x + cursorParam.barLength / 2" :y1="cursorParam.y" :y2="cursorParam.y" />
            <line id="cursorlinev" class="cursor" :x1="cursorParam.x" :x2="cursorParam.x"
                :y1="cursorParam.y - cursorParam.barLength / 2" :y2="cursorParam.y + cursorParam.barLength / 2" />
        </g>
    </svg>
</template>

<style scoped>
@import "./StationLayoutEditor.css";
</style>
