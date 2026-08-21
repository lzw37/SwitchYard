export type LayoutEntity = Record<string, unknown>;

export interface StationLayoutMetadata {
  instanceID?: string;
  stationSchemeID?: string;
  revision?: number;
  latestElementID?: number;
  displayStyles?: Record<string, unknown>;
  gridSettings?: Record<string, unknown>;
  [key: string]: unknown;
}

export interface StationLayoutDocument {
  metadata?: StationLayoutMetadata;
  tracks?: LayoutEntity[];
  curves?: LayoutEntity[];
  nodes?: LayoutEntity[];
  signals?: LayoutEntity[];
  insulationJoints?: LayoutEntity[];
  bufferStops?: LayoutEntity[];
  platforms?: LayoutEntity[];
  switches?: LayoutEntity[];
  cells?: LayoutEntity[];
  annotations?: LayoutEntity[];
  [key: string]: unknown;
}

export interface StationScheme {
  id: string;
  name: string;
  revision?: number;
}

export interface StationLayoutScope {
  instanceId: string;
  stationSchemeId?: string;
}

export interface SaveJsonRequest extends StationLayoutScope {
  json: string;
  expectedRevision?: number;
}

export interface SaveJsonResult {
  message: string;
  instanceId: string;
  stationSchemeId: string;
  revision?: number;
  nodeCount?: number;
  linkCount?: number;
  curveCount?: number;
  signalCount?: number;
  insulationJointCount?: number;
  bufferStopCount?: number;
  platformCount?: number;
  switchCount?: number;
  switchBranchVectorCount?: number;
  cellCount?: number;
  annotationCount?: number;
}

export interface SearchRoutesRequest extends StationLayoutScope {
  startNodeId: number;
  endNodeId: number;
}

export interface StationRouteSearchItem {
  direction: string;
  nodeIds: number[];
  linkIds: number[];
  switchIds?: string[];
  cellIds: string[];
  signalIds?: string[];
  nodes?: LayoutEntity[];
  links?: LayoutEntity[];
  switches?: LayoutEntity[];
  cells?: LayoutEntity[];
  signals?: LayoutEntity[];
  [key: string]: unknown;
}

export interface SearchRoutesResult extends StationLayoutScope {
  startNodeId: number;
  endNodeId: number;
  routes: StationRouteSearchItem[];
}

export interface ExtractDwgFileRequest {
  file: File;
  layerName: string;
}

export interface ExtractDwgFileResult {
  message: string;
  segmentCount: number;
  layout: StationLayoutDocument;
}

export interface StationLayoutGateway {
  getStationSchemes(request: { instanceId: string }): Promise<StationScheme[]>;
  createStationScheme(request: { instanceId: string; name: string }): Promise<StationScheme>;
  editStationScheme(request: {
    instanceId: string;
    originalId: string;
    name: string;
  }): Promise<StationScheme>;
  deleteStationScheme(request: {
    instanceId: string;
    stationSchemeId: string;
  }): Promise<void>;
  getJson(request: StationLayoutScope): Promise<StationLayoutDocument>;
  saveJson(request: SaveJsonRequest): Promise<SaveJsonResult>;
  searchRoutes(request: SearchRoutesRequest): Promise<SearchRoutesResult>;
  extractDwgFile(request: ExtractDwgFileRequest): Promise<ExtractDwgFileResult>;
}

export type StationLayoutTranslate = (
  key: string,
  parameters?: Record<string, unknown>,
) => string;

export type StationLayoutErrorFormatter = (
  error: unknown,
  fallback: string,
) => string;
