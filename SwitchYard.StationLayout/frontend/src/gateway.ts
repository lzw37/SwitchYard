export type LayoutEntity = Record<string, unknown>;

export interface StationLayoutMetadata {
  instanceID?: string;
  stationSchemeID?: string;
  revision?: number | null;
  latestElementID?: number;
  displayStyles?: Record<string, unknown> | null;
  gridSettings?: Record<string, unknown> | null;
  [key: string]: unknown;
}

export interface StationLayoutDocument {
  metadata?: StationLayoutMetadata | null;
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
  savedJson?: string | null;
  idMappings?: Record<string, Record<string, string>>;
  repairCount?: number;
  repairs?: string[];
  repairedJson?: string | null;
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

export interface RepairJsonResult {
  json: string;
  repairCount: number;
  repairs: string[];
}

export interface SearchRoutesRequest extends StationLayoutScope {
  startNodeId: string;
  endNodeId: string;
}

export interface StationRouteSearchItem {
  direction: string;
  nodeIds: string[];
  linkIds: string[];
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
  startNodeId: string;
  endNodeId: string;
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
  copyStationScheme(request: {
    instanceId: string;
    sourceStationSchemeId: string;
    name: string;
  }): Promise<StationScheme>;
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
  /** Preview server-side binding repairs without persisting the document. */
  repairJson?(request: SaveJsonRequest): Promise<RepairJsonResult>;
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
