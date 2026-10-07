import type { StationPlanTrain } from './stationPlanView.ts'

export type StationPlanColorMode = 'process' | 'routeType' | 'trainType'
export interface StationPlanColorGroup { key: string; value: string; name: string; color: string }
export const defaultStationPlanColor = '#dc2626'
export const stationPlanConstraintColor = '#64748b'

const processColors = ['#2563eb', '#16a34a', '#9333ea', '#ea580c', '#0891b2', '#db2777', '#a16207', '#4f46e5']
const routeColors: Record<string, string> = {
    Arrival: '#3374c8', Departure: '#268263', Shunting: '#ad7b24', Locomotive: '#8360c1', Dwelling: '#657e98',
}
const routeAliases: Record<string, string> = {
    arrival: 'Arrival', arr: 'Arrival', '接车': 'Arrival', '接车进路': 'Arrival',
    departure: 'Departure', dep: 'Departure', '发车': 'Departure', '发车进路': 'Departure',
    shunting: 'Shunting', shunt: 'Shunting', '调车': 'Shunting', '调车进路': 'Shunting',
    locomotive: 'Locomotive', loco: 'Locomotive', '机车出入段': 'Locomotive', '机车出入段进路': 'Locomotive', '机车走行': 'Locomotive',
    dwelling: 'Dwelling', dw: 'Dwelling', '停留': 'Dwelling', '停留进路': 'Dwelling',
}

function category(train: StationPlanTrain, mode: StationPlanColorMode, movementID?: string) {
    if (mode === 'process') {
        const snapshot = train.processConstraints
        const name = snapshot?.sourceName?.trim() || snapshot?.process.name?.trim() || ''
        return { value: snapshot?.sourceTemplateID?.trim() || snapshot?.process.id?.trim() || name, name }
    }
    const raw = (mode === 'trainType' ? train.trainType : train.movementRouteTypes?.[movementID || ''])?.trim() || ''
    const value = mode === 'routeType' ? routeAliases[raw.replace(/\s+/g, '').toLowerCase()] || raw : raw
    return { value, name: value }
}

export function stationPlanColorKey(train: StationPlanTrain, mode: StationPlanColorMode, movementID?: string) {
    return JSON.stringify([mode, category(train, mode, movementID).value])
}

/** Group by source identity, actual route type, or train type; never by train ID. */
export function stationPlanColorGroups(trains: StationPlanTrain[], mode: StationPlanColorMode, overrides: Record<string, string> = {}): StationPlanColorGroup[] {
    const groups = new Map<string, Omit<StationPlanColorGroup, 'color'>>()
    for (const train of trains) {
        const movements = mode === 'routeType' ? [...new Set(train.visits.map(visit => visit.movementID))] : [undefined]
        for (const movementID of movements) {
            const item = category(train, mode, movementID)
            const key = stationPlanColorKey(train, mode, movementID)
            const previous = groups.get(key)
            // A renamed process can appear in several saved revisions; use a deterministic legend name.
            if (!previous || (item.name && (!previous.name || item.name < previous.name))) groups.set(key, { key, ...item })
        }
    }
    let processIndex = 0
    return [...groups.values()].sort((a, b) => a.key < b.key ? -1 : a.key > b.key ? 1 : 0).map(group => {
        let color = defaultStationPlanColor
        if (mode === 'routeType') color = routeColors[group.value] || stationPlanConstraintColor
        if (mode === 'process' && group.value) {
            const index = processIndex++
            color = processColors[index] || processHue(index)
        }
        const override = overrides[group.key]
        return { ...group, color: override && /^#[0-9a-f]{6}$/i.test(override) ? override : color }
    })
}

// Native color inputs require hexadecimal values, including for large process catalogs.
function processHue(index: number) {
    const hue = ((index * 137.508) % 360) / 60
    const chroma = 0.64, x = chroma * (1 - Math.abs(hue % 2 - 1))
    const channels = [[chroma, x, 0], [x, chroma, 0], [0, chroma, x], [0, x, chroma], [x, 0, chroma], [chroma, 0, x]][Math.floor(hue)]!
    return '#' + channels.map(value => Math.round((value + 0.08) * 255).toString(16).padStart(2, '0')).join('')
}
