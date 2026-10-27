import type {
  ExtractDwgFileResult,
  SaveJsonResult,
  SearchRoutesResult,
  StationLayoutDocument,
  StationLayoutErrorFormatter,
  StationLayoutGateway,
  StationLayoutTranslate,
  StationScheme,
} from "../src/gateway";

const scheme: StationScheme = { id: "scheme-1", name: "Default", revision: 2 };
const layout: StationLayoutDocument = {
  metadata: { stationSchemeID: scheme.id, revision: scheme.revision },
};
const saveResult: SaveJsonResult = {
  message: "OK",
  instanceId: "instance-1",
  stationSchemeId: scheme.id,
  revision: 3,
};
const searchResult: SearchRoutesResult = {
  instanceId: "instance-1",
  stationSchemeId: scheme.id,
  startNodeId: 1,
  endNodeId: 2,
  routes: [],
};
const dwgResult: ExtractDwgFileResult = {
  message: "OK",
  segmentCount: 0,
  layout,
};

export const gatewayContractFixture: StationLayoutGateway = {
  async getStationSchemes(request) {
    void request.instanceId;
    return [scheme];
  },
  async createStationScheme(request) {
    void request.instanceId;
    void request.name;
    return scheme;
  },
  async copyStationScheme(request) {
    void request.instanceId;
    void request.sourceStationSchemeId;
    void request.name;
    return scheme;
  },
  async editStationScheme(request) {
    void request.instanceId;
    void request.originalId;
    void request.name;
    return scheme;
  },
  async deleteStationScheme(request) {
    void request.instanceId;
    void request.stationSchemeId;
  },
  async getJson(request) {
    void request.instanceId;
    void request.stationSchemeId;
    return layout;
  },
  async saveJson(request) {
    const revision: number | undefined = request.expectedRevision;
    void revision;
    return saveResult;
  },
  async searchRoutes(request) {
    void request.instanceId;
    void request.stationSchemeId;
    void request.startNodeId;
    void request.endNodeId;
    return searchResult;
  },
  async extractDwgFile(request) {
    void request.file;
    void request.layerName;
    return dwgResult;
  },
};

const translate: StationLayoutTranslate = (key) => key;
const formatError: StationLayoutErrorFormatter = (_error, fallback) => fallback;
void translate;
void formatError;
