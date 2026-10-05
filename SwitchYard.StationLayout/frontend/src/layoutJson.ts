import type { LayoutEntity, StationLayoutDocument } from './gateway';

/** File identity is separate from metadata.revision, which belongs to the database. */
export const STATION_LAYOUT_JSON_FORMAT = 'switchyard.station-layout';
export const STATION_LAYOUT_JSON_VERSION = 1;
export const STATION_LAYOUT_COLLECTIONS = [
  'tracks', 'curves', 'nodes', 'signals', 'insulationJoints', 'bufferStops',
  'platforms', 'switches', 'cells', 'annotations',
] as const;

export type StationLayoutCollection = typeof STATION_LAYOUT_COLLECTIONS[number];
export type StationLayoutArchive = StationLayoutDocument & {
  format: typeof STATION_LAYOUT_JSON_FORMAT;
  formatVersion: typeof STATION_LAYOUT_JSON_VERSION;
} & Record<StationLayoutCollection, LayoutEntity[]>;

type JsonObject = Record<string, unknown>;
const TEXT_FIELDS: Partial<Record<StationLayoutCollection, readonly string[]>> = {
  tracks: ['name', 'arrowDirection', 'arrowType'],
  signals: ['name', 'type', 'direction'], insulationJoints: ['type'],
  bufferStops: ['type', 'direction'], platforms: ['name'], switches: ['name', 'type'],
  cells: ['instanceID', 'stationSchemeID', 'name'],
  annotations: ['text', 'fontFamily', 'fontWeight', 'fontStyle', 'textColor'],
};
const own = (value: JsonObject, key: string) => Object.prototype.hasOwnProperty.call(value, key);
const isObject = (value: unknown): value is JsonObject =>
  value !== null && typeof value === 'object' && !Array.isArray(value)
  && Object.prototype.toString.call(value) === '[object Object]';

function invalid(path: string, reason: string): never {
  throw new Error(`Invalid station layout JSON: ${path} ${reason}.`);
}

/** Reject values JSON.stringify would silently omit or turn into null. */
function validateJsonValue(value: unknown, path: string, ancestors = new Set<object>()): void {
  if (value === null || typeof value === 'string' || typeof value === 'boolean') return;
  if (typeof value === 'number') {
    if (!Number.isFinite(value)) invalid(path, 'must be a finite number');
    return;
  }
  if (!Array.isArray(value) && !isObject(value)) invalid(path, 'must contain only JSON values');
  if (ancestors.has(value)) invalid(path, 'contains a circular reference');
  if (Object.getOwnPropertySymbols(value).length) invalid(path, 'contains unsupported symbol properties');
  ancestors.add(value);
  if (Array.isArray(value)) {
    for (let index = 0; index < value.length; index++) {
      validateJsonValue(value[index], `${path}[${index}]`, ancestors);
    }
  } else {
    for (const [key, child] of Object.entries(value)) validateJsonValue(child, `${path}.${key}`, ancestors);
  }
  ancestors.delete(value);
}

function numberField(value: JsonObject, key: string, path: string, required = false): void {
  if (!own(value, key) && !required) return;
  if (typeof value[key] !== 'number' || !Number.isFinite(value[key])) {
    invalid(`${path}.${key}`, 'must be a finite number');
  }
}

function stringField(value: JsonObject, key: string, path: string): void {
  if (own(value, key) && value[key] !== null && typeof value[key] !== 'string') {
    invalid(`${path}.${key}`, 'must be a string or null');
  }
}

function integerField(value: JsonObject, key: string, path: string, maximum: number, nullable = false): void {
  if (!own(value, key) || (nullable && value[key] === null)) return;
  numberField(value, key, path);
  const number = value[key] as number;
  if (!Number.isInteger(number) || number < 0 || number > maximum) {
    invalid(`${path}.${key}`, `must be an integer between 0 and ${maximum}`);
  }
}

function displayNumberField(value: JsonObject, key: string, path: string): void {
  if (!own(value, key) || value[key] === null) return;
  const number = value[key];
  // Settings have historically accepted numeric strings; never coerce objects.
  if ((typeof number !== 'number' && typeof number !== 'string') || !Number.isFinite(Number(number))) {
    invalid(`${path}.${key}`, 'must be a finite number, numeric string, or null');
  }
}

function validateDisplaySettings(metadata: JsonObject): void {
  const grid = metadata.gridSettings;
  if (isObject(grid)) {
    for (const key of ['originX', 'originY', 'OriginX', 'OriginY', 'spacing', 'Spacing', 'gridSpacing', 'GridSpacing',
      'verticalSpace', 'horizontalSpace']) displayNumberField(grid, key, 'metadata.gridSettings');
    for (const key of ['showGrid', 'ShowGrid']) {
      if (own(grid, key) && grid[key] !== null && typeof grid[key] !== 'boolean') {
        invalid(`metadata.gridSettings.${key}`, 'must be a boolean or null');
      }
    }
  }
  const styles = metadata.displayStyles;
  if (!isObject(styles)) return;
  const textStyles = ['switchName', 'platformName', 'signalName', 'lineName', 'cellName'];
  for (const key of [...textStyles, 'track', 'curve', 'platform', 'signal', 'switch', 'node']) {
    if (!own(styles, key) || styles[key] === null) continue;
    const style = styles[key];
    const path = `metadata.displayStyles.${key}`;
    if (!isObject(style)) invalid(path, 'must be an object or null');
    if (textStyles.includes(key)) {
      displayNumberField(style, 'fontSize', path);
      for (const field of ['fontFamily', 'fontStyle', 'color']) stringField(style, field, path);
      // CSS permits both named font weights and finite numeric weights.
      if (typeof style.fontWeight === 'number') numberField(style, 'fontWeight', path);
      else stringField(style, 'fontWeight', path);
    } else {
      const numberKey = key === 'signal' ? 'scale' : key === 'node' ? 'radius' : 'strokeWidth';
      displayNumberField(style, numberKey, path);
      if (key !== 'signal') stringField(style, 'color', path);
    }
  }
}

function positionField(value: JsonObject, key: string, path: string, required: boolean): void {
  if (!own(value, key) && !required) return;
  const point = value[key];
  if (!isObject(point)) invalid(`${path}.${key}`, 'must be a position object');
  numberField(point, 'x', `${path}.${key}`, required);
  numberField(point, 'y', `${path}.${key}`, required);
}

function idValue(value: unknown, path: string, allowEmpty = false, archive = false): string {
  if (archive && typeof value !== 'string') invalid(path, 'must be an ID string');
  if (allowEmpty && (value === null || value === '')) return '';
  if ((typeof value !== 'string' && typeof value !== 'number')
    || (typeof value === 'number' && !Number.isFinite(value))) {
    invalid(path, 'must be an ID string or number');
  }
  const id = String(value);
  if (!id.trim() && !allowEmpty) invalid(path, 'must not be empty');
  return id.trim() ? id : '';
}

function reference(value: unknown, ids: Set<string>, path: string, archive: boolean): void {
  // Direct references are nullable in the backend document contract.
  if (value === null) return;
  const id = idValue(value, path, true, archive);
  if (id && !ids.has(id)) invalid(path, `references a missing element (${id})`);
}

function referenceField(value: JsonObject, key: string, ids: Set<string>, path: string, archive: boolean): void {
  if (own(value, key)) reference(value[key], ids, `${path}.${key}`, archive);
}

function validateEntityGeometry(entity: JsonObject, collection: StationLayoutCollection, path: string, archive: boolean): void {
  if (collection === 'tracks') {
    for (const key of ['x1', 'y1', 'x2', 'y2']) numberField(entity, key, path, archive);
  } else if (collection === 'nodes' || collection === 'platforms') {
    for (const key of ['x', 'y']) numberField(entity, key, path, archive);
    if (collection === 'platforms') {
      for (const key of ['width', 'height']) numberField(entity, key, path, archive);
    }
  } else if (collection === 'curves') {
    numberField(entity, 'radius', path, archive);
    for (const key of ['angle', 'tangentDistance']) numberField(entity, key, path);
    for (const key of ['start', 'end', 'center']) positionField(entity, key, path, archive);
    // Older documents store the three points as flat coordinates.
    for (const key of ['startX', 'startY', 'endX', 'endY', 'centerX', 'centerY']) numberField(entity, key, path);
    for (const key of ['largeArcFlag', 'sweepFlag']) {
      if (own(entity, key) && entity[key] !== 0 && entity[key] !== 1) invalid(`${path}.${key}`, 'must be 0 or 1');
    }
  } else if (collection !== 'cells') {
    positionField(entity, 'position', path, archive);
    if (collection === 'annotations') {
      for (const key of ['fontSize', 'angle']) numberField(entity, key, path);
    }
  }
}

/** Identify the supported archive marker; call validate before applying its contents. */
export function isStationLayoutArchive(value: unknown): value is StationLayoutArchive {
  return isObject(value) && value.format === STATION_LAYOUT_JSON_FORMAT
    && value.formatVersion === STATION_LAYOUT_JSON_VERSION;
}

/** Validate without normalizing, pruning extensions, assigning IDs, or changing topology. */
export function validateStationLayoutJson(value: unknown): asserts value is StationLayoutDocument {
  if (!isObject(value)) invalid('$', 'must be an object');
  validateJsonValue(value, '$');
  const hasVersion = own(value, 'format') || own(value, 'formatVersion');
  if (hasVersion && value.format !== STATION_LAYOUT_JSON_FORMAT) invalid('format', 'is not a station layout format');
  if (hasVersion && value.formatVersion !== STATION_LAYOUT_JSON_VERSION) invalid('formatVersion', 'is unsupported');
  if (!STATION_LAYOUT_COLLECTIONS.some(key => own(value, key))) invalid('$', 'does not contain a layout collection');

  if (own(value, 'metadata') && value.metadata !== null) {
    if (!isObject(value.metadata)) invalid('metadata', 'must be an object');
    integerField(value.metadata, 'latestElementID', 'metadata', 2147483647);
    integerField(value.metadata, 'revision', 'metadata', Number.MAX_SAFE_INTEGER, true);
    for (const key of ['instanceID', 'stationSchemeID']) stringField(value.metadata, key, 'metadata');
    for (const key of ['displayStyles', 'gridSettings', 'coordinateTransform']) {
      if (own(value.metadata, key) && value.metadata[key] !== null && !isObject(value.metadata[key])) {
        invalid(`metadata.${key}`, 'must be an object');
      }
    }
    const transform = value.metadata.coordinateTransform;
    if (isObject(transform)) {
      if (own(transform, 'applied') && typeof transform.applied !== 'boolean') {
        invalid('metadata.coordinateTransform.applied', 'must be a boolean');
      }
      for (const key of ['minX', 'minY', 'scale', 'padding']) numberField(transform, key, 'metadata.coordinateTransform');
    }
    validateDisplaySettings(value.metadata);
  }

  const collections = {} as Record<StationLayoutCollection, JsonObject[]>;
  const ids = {} as Record<StationLayoutCollection, Set<string>>;
  for (const key of STATION_LAYOUT_COLLECTIONS) {
    const entries = value[key];
    if (!own(value, key) && !hasVersion) {
      collections[key] = [];
      ids[key] = new Set();
      continue;
    }
    if (!Array.isArray(entries)) invalid(key, 'must be an array');
    collections[key] = entries;
    ids[key] = new Set();
    entries.forEach((entity, index) => {
      const path = `${key}[${index}]`;
      if (!isObject(entity)) invalid(path, 'must be an object');
      if (own(entity, 'id') || hasVersion) {
        // Empty Cell IDs are legitimate pending records awaiting database assignment.
        const id = idValue(entity.id, `${path}.id`, key === 'cells', hasVersion);
        if (id && ids[key].has(id)) invalid(`${path}.id`, `duplicates ID ${id}`);
        if (id) ids[key].add(id);
      }
      validateEntityGeometry(entity, key, path, hasVersion);
      // Known text fields are typed strings by the save API. Extensions remain unrestricted.
      for (const field of TEXT_FIELDS[key] ?? []) {
        stringField(entity, field, path);
        // Parent panels read these older aliases when the canonical value is absent.
        const alias = field[0]!.toUpperCase() + field.slice(1);
        if (!hasVersion && entity[field] == null) stringField(entity, alias, path);
      }
      if (!hasVersion && entity.id == null && own(entity, 'ID')) idValue(entity.ID, `${path}.ID`, key === 'cells');
      if (!hasVersion && key === 'tracks') {
        for (const field of ['fromNodeID', 'toNodeID']) {
          const alias = field[0]!.toUpperCase() + field.slice(1);
          if (entity[field] == null && own(entity, alias)) idValue(entity[alias], `${path}.${alias}`, true);
        }
      }
      if (!hasVersion && key === 'cells' && entity.linkIDList == null && own(entity, 'LinkIDList')) {
        const list = entity.LinkIDList;
        if (Array.isArray(list)) list.forEach((id, index) => idValue(id, `${path}.LinkIDList[${index}]`, true));
        else stringField(entity, 'LinkIDList', path);
      }
    });
  }

  for (const key of STATION_LAYOUT_COLLECTIONS) {
    collections[key].forEach((entity, index) => {
      const path = `${key}[${index}]`;
      if (key === 'tracks') {
        for (const field of ['fromNodeID', 'toNodeID']) referenceField(entity, field, ids.nodes, path, hasVersion);
      } else if (key === 'curves') {
        for (const field of ['nodeID', 'vertexNodeID']) referenceField(entity, field, ids.nodes, path, hasVersion);
        for (const field of ['tangentLinkID1', 'tangentLinkID2', 'linkID1', 'linkID2']) referenceField(entity, field, ids.tracks, path, hasVersion);
      } else if (['signals', 'insulationJoints', 'bufferStops', 'switches'].includes(key)) {
        for (const field of ['bindingNodeID', 'BindingNodeID', 'bindingNodeId', 'BindingNodeId']) {
          referenceField(entity, field, ids.nodes, path, hasVersion);
        }
      }
      if (key === 'nodes' && own(entity, 'adjacentLineIDList')) {
        if (!Array.isArray(entity.adjacentLineIDList)) invalid(`${path}.adjacentLineIDList`, 'must be an array');
        entity.adjacentLineIDList.forEach((id, i) => reference(id, ids.tracks, `${path}.adjacentLineIDList[${i}]`, hasVersion));
      }
      if (key === 'switches' && own(entity, 'branchVectorList')) {
        if (!Array.isArray(entity.branchVectorList)) invalid(`${path}.branchVectorList`, 'must be an array');
        entity.branchVectorList.forEach((branch, i) => {
          const branchPath = `${path}.branchVectorList[${i}]`;
          if (!isObject(branch)) invalid(branchPath, 'must be an object');
          for (const coordinate of ['x', 'y']) numberField(branch, coordinate, branchPath, hasVersion);
          referenceField(branch, 'lineID', ids.tracks, branchPath, hasVersion);
        });
      }
      if (key === 'cells' && own(entity, 'linkIDList')) {
        const list = entity.linkIDList;
        // Legacy hosts accept arrays, but the versioned save contract requires text.
        if (hasVersion) stringField(entity, 'linkIDList', path);
        if (list !== null && typeof list !== 'string' && !Array.isArray(list)) invalid(`${path}.linkIDList`, 'must be a string or array');
        const links = Array.isArray(list) ? list : (list === null ? [] : list.split(/[,，;；\s]+/).filter(Boolean));
        links.forEach((id, i) => reference(id, ids.tracks, `${path}.linkIDList[${i}]`, hasVersion));
      }
    });
  }
}

/** Accept UTF-8 BOMs and unversioned backend JSON; return every field unchanged. */
export function parseStationLayoutJson(text: string): StationLayoutDocument {
  if (typeof text !== 'string') invalid('$', 'must be JSON text');
  const document: unknown = JSON.parse(text.replace(/^\uFEFF/, ''));
  validateStationLayoutJson(document);
  return document;
}

/** Export one complete, versioned document, preserving array order and JSON values. */
export function serializeStationLayoutJson(document: StationLayoutDocument): string {
  validateStationLayoutJson(document);
  const archive: StationLayoutDocument = {
    ...document,
    format: STATION_LAYOUT_JSON_FORMAT,
    formatVersion: STATION_LAYOUT_JSON_VERSION,
  };
  for (const key of STATION_LAYOUT_COLLECTIONS) archive[key] = document[key] ?? [];
  validateStationLayoutJson(archive);
  return `${JSON.stringify(archive, null, 2)}\n`;
}
