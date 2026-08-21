import type {
    ExtractDwgFileResult,
    SaveJsonResult,
    SearchRoutesResult,
    StationLayoutDocument,
    StationLayoutErrorFormatter,
    StationLayoutGateway,
    StationScheme,
} from "@switchyard/station-layout";
import axios from "@/utils/axios";

type UnknownRecord = Record<string, unknown>;

function asRecord(value: unknown): UnknownRecord {
    return value !== null && typeof value === "object"
        ? value as UnknownRecord
        : {};
}

function optionalNumber(value: unknown): number | undefined {
    const normalized = typeof value === "string"
        ? value.trim().replace(/^W\//i, "").replace(/^\"|\"$/g, "")
        : value;
    const result = Number(normalized);
    return Number.isSafeInteger(result) && result >= 0 ? result : undefined;
}

function responseRevision(headers: unknown): number | undefined {
    const row = asRecord(headers);
    return optionalNumber(
        row["x-station-layout-revision"]
        ?? row["X-Station-Layout-Revision"]
        ?? row.etag
        ?? row.ETag,
    );
}

function normalizeScheme(value: unknown): StationScheme {
    const row = asRecord(value);
    const id = String(row.id ?? row.ID ?? "").trim();
    const name = String(row.name ?? row.Name ?? id).trim() || id;
    const revision = optionalNumber(row.revision ?? row.Revision);
    return revision === undefined ? { id, name } : { id, name, revision };
}

function normalizeSaveResult(value: unknown, fallback: {
    instanceId: string;
    stationSchemeId?: string;
}): SaveJsonResult {
    const row = asRecord(value);
    const result: SaveJsonResult = {
        message: String(row.message ?? row.Message ?? "OK"),
        instanceId: String(row.instanceId ?? row.instanceID ?? row.InstanceID ?? fallback.instanceId),
        stationSchemeId: String(
            row.stationSchemeId
            ?? row.stationSchemeID
            ?? row.StationSchemeID
            ?? fallback.stationSchemeId
            ?? "",
        ),
    };

    const numericFields: Array<[keyof SaveJsonResult, unknown]> = [
        ["revision", row.revision ?? row.Revision],
        ["nodeCount", row.nodeCount ?? row.NodeCount],
        ["linkCount", row.linkCount ?? row.LinkCount],
        ["curveCount", row.curveCount ?? row.CurveCount],
        ["signalCount", row.signalCount ?? row.SignalCount],
        ["insulationJointCount", row.insulationJointCount ?? row.InsulationJointCount],
        ["bufferStopCount", row.bufferStopCount ?? row.BufferStopCount],
        ["platformCount", row.platformCount ?? row.PlatformCount],
        ["switchCount", row.switchCount ?? row.SwitchCount],
        ["switchBranchVectorCount", row.switchBranchVectorCount ?? row.SwitchBranchVectorCount],
        ["cellCount", row.cellCount ?? row.CellCount],
        ["annotationCount", row.annotationCount ?? row.AnnotationCount],
    ];
    for (const [field, rawValue] of numericFields) {
        const numberValue = optionalNumber(rawValue);
        if (numberValue !== undefined) {
            (result as unknown as UnknownRecord)[field] = numberValue;
        }
    }

    return result;
}

export const switchyardStationLayoutGateway: StationLayoutGateway = {
    async getStationSchemes({ instanceId }) {
        const { data } = await axios.get<unknown[]>("/StationLayout/GetStationSchemes", {
            params: { instanceID: instanceId },
        });
        return (Array.isArray(data) ? data : [])
            .map(normalizeScheme)
            .filter((scheme) => scheme.id.length > 0);
    },

    async createStationScheme({ instanceId, name }) {
        const { data } = await axios.post("/StationLayout/CreateStationScheme", {
            instanceID: instanceId,
            name,
        });
        return normalizeScheme(data);
    },

    async editStationScheme({ instanceId, originalId, name }) {
        const { data } = await axios.put("/StationLayout/EditStationScheme", {
            instanceID: instanceId,
            originalID: originalId,
            name,
        });
        const scheme = normalizeScheme(data);
        return scheme.id ? scheme : { id: originalId, name };
    },

    async deleteStationScheme({ instanceId, stationSchemeId }) {
        await axios.delete("/StationLayout/DeleteStationScheme", {
            params: {
                instanceID: instanceId,
                stationSchemeID: stationSchemeId,
            },
        });
    },

    async getJson({ instanceId, stationSchemeId }) {
        const response = await axios.post<StationLayoutDocument>(
            "/StationLayout/GetJson",
            null,
            {
                params: {
                    instanceID: instanceId,
                    ...(stationSchemeId ? { stationSchemeID: stationSchemeId } : {}),
                },
            },
        );
        const revision = responseRevision(response.headers);
        if (revision === undefined) return response.data;

        const document = asRecord(response.data);
        return {
            ...document,
            metadata: {
                ...asRecord(document.metadata),
                revision,
            },
        } as StationLayoutDocument;
    },

    async saveJson(request) {
        const params = {
            instanceID: request.instanceId,
            ...(request.stationSchemeId ? { stationSchemeID: request.stationSchemeId } : {}),
        };
        const headers = request.expectedRevision === undefined
            ? undefined
            : { "If-Match": `"${request.expectedRevision}"` };
        const response = await axios.post("/StationLayout/SaveJson", {
            json: request.json,
            instanceID: request.instanceId,
            stationSchemeID: request.stationSchemeId,
            expectedRevision: request.expectedRevision,
        }, { params, headers });
        const result = normalizeSaveResult(response.data, request);
        if (result.revision === undefined) {
            result.revision = responseRevision(response.headers);
        }
        return result;
    },

    async searchRoutes(request) {
        const params = {
            instanceID: request.instanceId,
            ...(request.stationSchemeId ? { stationSchemeID: request.stationSchemeId } : {}),
        };
        const { data } = await axios.post<UnknownRecord>("/StationLayout/SearchRoutes", {
            instanceID: request.instanceId,
            stationSchemeID: request.stationSchemeId,
            startNodeId: request.startNodeId,
            endNodeId: request.endNodeId,
        }, { params });
        const row = asRecord(data);
        return {
            instanceId: String(row.instanceId ?? row.instanceID ?? row.InstanceID ?? request.instanceId),
            stationSchemeId: String(
                row.stationSchemeId
                ?? row.stationSchemeID
                ?? row.StationSchemeID
                ?? request.stationSchemeId
                ?? "",
            ),
            startNodeId: Number(row.startNodeId ?? row.StartNodeId ?? request.startNodeId),
            endNodeId: Number(row.endNodeId ?? row.EndNodeId ?? request.endNodeId),
            routes: (Array.isArray(row.routes) ? row.routes : Array.isArray(row.Routes) ? row.Routes : []),
        } as SearchRoutesResult;
    },

    async extractDwgFile({ file, layerName }) {
        const formData = new FormData();
        formData.append("file", file);
        formData.append("layerName", layerName || "0");
        const { data } = await axios.post<ExtractDwgFileResult>(
            "/StationLayout/ExtractDwgFile",
            formData,
            { headers: { "Content-Type": "multipart/form-data" } },
        );
        return data;
    },
};

export const formatSwitchYardStationLayoutError: StationLayoutErrorFormatter = (
    error,
    fallback,
) => {
    const candidate = asRecord(error);
    const response = asRecord(candidate.response);
    const data = response.data;
    if (typeof data === "string" && data.trim()) return data.trim();

    const payload = asRecord(data);
    for (const value of [payload.message, payload.detail, candidate.message]) {
        if (typeof value === "string" && value.trim()) return value.trim();
    }
    return fallback;
};

export default switchyardStationLayoutGateway;
