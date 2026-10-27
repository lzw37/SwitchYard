/**
 * Visible representatives of the EMU families used by the station simulation.
 * The section coordinates are hand-built visual approximations of the linked
 * train photographs on china-emu.cn, not manufacturing drawings. They share the existing
 * 25 m modelling envelope so route-based train sizing remains independent of type.
 */
export type EmuModelId =
    | 'CR400AF' | 'CR400BF' | 'CR200J' | 'CR450'
    | 'CRH1' | 'CRH2' | 'CRH5' | 'CRH3C' | 'CRH380A' | 'CRH380B' | 'CRH6'

export type EmuNoseSection = readonly [x: number, widthScale: number, bottom: number, top: number]

export interface EmuModelProfile {
    id: EmuModelId
    label: string
    variant: string
    brand: '复兴号' | '和谐号'
    sourceUrl: string
    description: string
    bodyColor: number
    roofColor: number
    stripeColor: number
    secondaryColor: number
    /** Last section is the full coupling-cover rim; the mesh adds its rounded end cap. */
    nose: readonly EmuNoseSection[]
    /** Continuous side silhouette; when present, the rings specify transverse widths. */
    noseOval?: { startX: number; tipY: number; skew: number }
    /** Front glazing extent on the loft; u = 0.5 is the roof centreline. */
    cabGlass: readonly [xStart: number, xEnd: number, uLow: number, uHigh: number]
    cabSideWindow: readonly [xStart: number, xEnd: number]
    lampStyle: 'swept' | 'round' | 'vertical' | 'bar'
    lampX: number
    lampU: number
    windowBand: boolean
    windowWidth: number
    windowHeight: number
    windowPitch: number
    doorXs: readonly number[]
    cabDoorXs: readonly number[]
    roofStyle: 'faired' | 'pods' | 'ribbed' | 'power'
    /** Zero-based vehicle indices in the representative formation. */
    pantographCars: readonly number[]
    carCount: number
    livery: 'af' | 'bf' | 'jade' | 'arrow' | 'blue' | 'metro'
    noseCove: number
}

export const EMU_MODEL_PROFILES: Record<EmuModelId, EmuModelProfile> = {
    CR400AF: {
        id: 'CR400AF', label: 'CR400AF', variant: 'CR400AF', brand: '复兴号',
        sourceUrl: 'https://www.china-emu.cn/Trains/Model/Detail-12002-101-S.html',
        description: '银红涂装、饱满低位鼻罩、红色环绕驾驶舱与后掠灯组。',
        bodyColor: 0xcbd0d1, roofColor: 0x8c979d, stripeColor: 0xce132d, secondaryColor: 0xecc388,
        // Preserve the established cab shoulder; fill the lower hood and coupling cover.
        nose: [
            [2.0, 1.000, 0.96, 3.88], [3.8, 0.997, 0.95, 3.84],
            [5.2, 0.983, 0.95, 3.67], [6.6, 0.935, 0.96, 3.35],
            [8.0, 0.840, 0.99, 2.99], [9.4, 0.765, 1.00, 2.73],
            [10.5, 0.665, 1.02, 2.49], [11.3, 0.550, 1.05, 2.29],
            [11.8, 0.450, 1.08, 2.17], [12.10, 0.400, 1.10, 2.13],
        ],
        cabGlass: [4.55, 7.12, 0.389, 0.611], cabSideWindow: [2.87, 5.35],
        lampStyle: 'swept', lampX: 6.56, lampU: 0.308,
        windowBand: true, windowWidth: 1.11, windowHeight: 0.74, windowPitch: 1.54,
        doorXs: [-10.5, 10.5], cabDoorXs: [-10.5, 1.9],
        roofStyle: 'faired', pantographCars: [1, 4], carCount: 8, livery: 'af', noseCove: 0.08,
    },
    CR400BF: {
        id: 'CR400BF', label: 'CR400BF', variant: 'CR400BF', brand: '复兴号',
        sourceUrl: 'https://www.china-emu.cn/Trains/Model/Detail-13032-101-S.html',
        description: '白金涂装、宽钝鼻罩、清晰肩部棱线和上挑凤眼车灯。',
        bodyColor: 0xdbe0dd, roofColor: 0x899499, stripeColor: 0xd4a52f, secondaryColor: 0x9f7528,
        // A higher, flatter hood and pronounced upper shoulder distinguish BF from AF.
        nose: [
            [2.4, 1.000, 0.96, 3.88], [3.7, 0.995, 0.96, 3.82],
            [5.0, 0.980, 0.97, 3.65], [6.4, 0.940, 0.99, 3.30],
            [7.8, 0.860, 1.02, 2.97], [9.1, 0.760, 1.06, 2.73],
            [10.3, 0.650, 1.09, 2.54], [11.2, 0.535, 1.13, 2.38],
            [11.8, 0.455, 1.16, 2.29], [12.10, 0.420, 1.18, 2.26],
        ],
        cabGlass: [4.95, 7.25, 0.388, 0.612], cabSideWindow: [3.1, 5.3],
        lampStyle: 'swept', lampX: 7.6, lampU: 0.32,
        windowBand: true, windowWidth: 1.15, windowHeight: 0.72, windowPitch: 1.55,
        doorXs: [-10.45, 10.45], cabDoorXs: [-10.45, 2.15],
        roofStyle: 'faired', pantographCars: [2, 5], carCount: 8, livery: 'bf', noseCove: 0.12,
    },
    CR200J: {
        id: 'CR200J', label: 'CR200J', variant: 'CR200J', brand: '复兴号',
        sourceUrl: 'https://www.china-emu.cn/Trains/Model/Detail-10021-101-S.html',
        description: '青绿色车身、鲜黄腰线与黑色客窗带；高额头、陡斜前窗与连续椭圆前鼻，侧肩向前收拢，动力车设独立格栅。',
        bodyColor: 0x06ae71, roofColor: 0x8e9b9b, stripeColor: 0xffdf19, secondaryColor: 0xe6e8de,
        // Visual proportions from the supplied three-quarter photograph: the
        // swept-back cheeks are not part of the foremost cover's width. Taper
        // continuously through the cab and lamp stations to a half-width hood.
        nose: [
            [7.7, 1.000, 0.96, 3.88], [8.6, 0.975, 0.96, 3.88],
            [9.15, 0.935, 0.96, 3.85], [9.8, 0.875, 0.96, 3.68],
            [10.45, 0.795, 0.965, 3.17], [11.0, 0.700, 0.97, 2.63],
            [11.55, 0.605, 0.98, 2.12], [11.88, 0.550, 0.985, 1.90],
            [12.10, 0.510, 0.99, 1.80],
        ],
        noseOval: { startX: 8.9, tipY: 1.22, skew: -0.85 },
        cabGlass: [9.95, 11.15, 0.38, 0.62], cabSideWindow: [8.3, 9.75],
        lampStyle: 'vertical', lampX: 11.18, lampU: 0.315,
        windowBand: true, windowWidth: 1.26, windowHeight: 0.82, windowPitch: 1.68,
        doorXs: [-10.65, 10.65], cabDoorXs: [-10.65, 7.3],
        roofStyle: 'power', pantographCars: [0], carCount: 9, livery: 'jade', noseCove: 0.01,
    },
    CR450: {
        id: 'CR450', label: 'CR450', variant: 'CR450AF', brand: '复兴号',
        sourceUrl: 'https://www.china-emu.cn/Trains/Model/CR450AF/1',
        description: '以 CR450AF 样车为原型：长流线车头、宽厚前罩、大面积黑色面罩与高位后掠灯。',
        bodyColor: 0xcfd5d8, roofColor: 0x77838c, stripeColor: 0xcd1831, secondaryColor: 0xd5a02c,
        // The long hood retains a substantial coupling cover rather than ending in a spear.
        nose: [
            [-1.1, 1.000, 0.96, 3.88], [0.8, 0.998, 0.96, 3.86],
            [2.7, 0.985, 0.96, 3.70], [4.4, 0.950, 0.97, 3.38],
            [6.1, 0.875, 0.98, 3.00], [7.8, 0.770, 0.99, 2.68],
            [9.3, 0.670, 1.00, 2.46], [10.6, 0.565, 1.01, 2.29],
            [11.5, 0.485, 1.02, 2.20], [12.10, 0.440, 1.03, 2.15],
        ],
        cabGlass: [2.8, 5.65, 0.386, 0.614], cabSideWindow: [0.1, 3.2],
        lampStyle: 'swept', lampX: 5.3, lampU: 0.32,
        windowBand: true, windowWidth: 1.18, windowHeight: 0.72, windowPitch: 1.56,
        doorXs: [-10.5, 10.5], cabDoorXs: [-10.5, -1.1],
        roofStyle: 'faired', pantographCars: [1, 6], carCount: 8, livery: 'arrow', noseCove: 0.15,
    },
    CRH1: {
        id: 'CRH1', label: 'CRH1', variant: 'CRH1A', brand: '和谐号',
        sourceUrl: 'https://www.china-emu.cn/Trains/Model/Detail-21001-101-S.html',
        description: '不锈钢银色车身、短陡驾驶室前脸、低位宽鼻罩、黑色窗带与外露车顶设备。',
        bodyColor: 0xb9c3c9, roofColor: 0x7c8992, stripeColor: 0x285890, secondaryColor: 0x477aac,
        // Regina-derived short cab: retain full body height almost to the nose.
        nose: [
            [8.4, 1.000, 0.96, 3.88], [9.5, 0.997, 0.97, 3.88],
            [10.3, 0.990, 0.98, 3.80], [10.9, 0.975, 1.00, 3.50],
            [11.35, 0.960, 1.01, 3.04], [11.7, 0.930, 1.02, 2.55],
            [12.0, 0.880, 1.05, 2.10], [12.25, 0.800, 1.08, 1.98],
        ],
        cabGlass: [10.0, 11.72, 0.37, 0.63], cabSideWindow: [8.8, 10.2],
        lampStyle: 'vertical', lampX: 11.75, lampU: 0.258,
        windowBand: true, windowWidth: 1.31, windowHeight: 0.80, windowPitch: 1.62,
        doorXs: [-8.85, 8.85], cabDoorXs: [-8.85, 6.7],
        roofStyle: 'ribbed', pantographCars: [1, 6], carCount: 8, livery: 'blue', noseCove: 0,
    },
    CRH2: {
        id: 'CRH2', label: 'CRH2', variant: 'CRH2A', brand: '和谐号',
        sourceUrl: 'https://www.china-emu.cn/Trains/Model/Detail-22001-101-S.html',
        description: '白底双蓝腰线、宽扁鸭嘴车头、环抱式驾驶窗与窗上中央灯组。',
        bodyColor: 0xdde2df, roofColor: 0x9ca8ad, stripeColor: 0x2057a0, secondaryColor: 0x4a87bc,
        nose: [
            [4.3, 1.000, 0.96, 3.88], [5.25, 0.998, 0.96, 3.79],
            [6.25, 0.977, 0.96, 3.41], [7.2, 0.945, 0.97, 2.91],
            [8.15, 0.910, 0.98, 2.44], [9.1, 0.850, 0.99, 2.16],
            [10.15, 0.735, 1.00, 2.04], [11.1, 0.605, 1.01, 1.99],
            [11.7, 0.505, 1.01, 1.97], [12.10, 0.440, 1.02, 1.96],
        ],
        cabGlass: [5.9, 7.4, 0.399, 0.601], cabSideWindow: [4.85, 6.15],
        lampStyle: 'bar', lampX: 5.77, lampU: 0.5,
        windowBand: false, windowWidth: 1.15, windowHeight: 0.66, windowPitch: 1.54,
        doorXs: [-10.35, 10.35], cabDoorXs: [-10.35, 3.4],
        roofStyle: 'pods', pantographCars: [1, 6], carCount: 8, livery: 'blue', noseCove: 0.025,
    },
    CRH5: {
        id: 'CRH5', label: 'CRH5', variant: 'CRH5A', brand: '和谐号',
        sourceUrl: 'https://www.china-emu.cn/Trains/Model/Detail-25001-101-S.html',
        description: '宽厚椭圆车头、包覆式黑色单片前窗、白底蓝腰线与两侧倾斜竖向灯组。',
        bodyColor: 0xd3dadb, roofColor: 0x87939b, stripeColor: 0x2869ad, secondaryColor: 0x6898c4,
        // The broad egg-shaped face carries the continuous black cab mask.
        nose: [
            [7.2, 1.000, 0.96, 3.88], [8.4, 0.999, 0.96, 3.88],
            [9.2, 0.995, 0.96, 3.83], [9.8, 0.990, 0.98, 3.70],
            [10.35, 0.970, 1.00, 3.40], [10.85, 0.920, 1.02, 2.95],
            [11.35, 0.860, 1.05, 2.55], [11.75, 0.730, 1.08, 2.29],
            [12.10, 0.580, 1.13, 2.23],
        ],
        cabGlass: [9.9, 11.18, 0.36, 0.64], cabSideWindow: [8.1, 9.6],
        lampStyle: 'vertical', lampX: 11.2, lampU: 0.375,
        windowBand: false, windowWidth: 1.24, windowHeight: 0.72, windowPitch: 1.61,
        doorXs: [-9.65, 9.65], cabDoorXs: [-9.65, 5.7],
        roofStyle: 'pods', pantographCars: [2, 5], carCount: 8, livery: 'blue', noseCove: 0.005,
    },
    CRH3C: {
        id: 'CRH3C', label: 'CRH3C', variant: 'CRH3C', brand: '和谐号',
        sourceUrl: 'https://www.china-emu.cn/Trains/Model/Detail-23011-101-S.html',
        description: '白蓝涂装、饱满椭圆车头、宽厚前罩、椭圆双灯及连续深色车窗带。',
        bodyColor: 0xdce1df, roofColor: 0x89949b, stripeColor: 0x215b9d, secondaryColor: 0x659cc5,
        nose: [
            [5.7, 1.000, 0.96, 3.88], [6.5, 0.995, 0.97, 3.82],
            [7.3, 0.980, 0.99, 3.69], [8.1, 0.947, 1.01, 3.48],
            [8.9, 0.895, 1.04, 3.23], [9.7, 0.815, 1.08, 3.00],
            [10.5, 0.710, 1.13, 2.78], [11.2, 0.595, 1.19, 2.62],
            [11.8, 0.480, 1.25, 2.53], [12.10, 0.430, 1.28, 2.50],
        ],
        cabGlass: [7.25, 9.25, 0.37, 0.63], cabSideWindow: [5.85, 7.35],
        lampStyle: 'round', lampX: 10.82, lampU: 0.35,
        windowBand: true, windowWidth: 1.17, windowHeight: 0.77, windowPitch: 1.57,
        doorXs: [-10.3, 10.3], cabDoorXs: [-10.3, 4.65],
        roofStyle: 'pods', pantographCars: [2, 5], carCount: 8, livery: 'blue', noseCove: 0.025,
    },
    CRH380A: {
        id: 'CRH380A', label: 'CRH380A', variant: 'CRH380A', brand: '和谐号',
        sourceUrl: 'https://www.china-emu.cn/Trains/Model/Detail-30001-101-S.html',
        description: '银灰蓝线、宽扁长鸭嘴、饱满圆钝前罩、流线驾驶舱和后掠车灯。',
        bodyColor: 0xc7d1d5, roofColor: 0x8d9ba3, stripeColor: 0x2468ae, secondaryColor: 0x70a3c9,
        // A low, broad hood after the abrupt windscreen slope is the A-family cue.
        nose: [
            [1.8, 1.000, 0.96, 3.88], [3.2, 0.998, 0.96, 3.84],
            [4.7, 0.982, 0.96, 3.60], [6.0, 0.942, 0.97, 3.12],
            [7.2, 0.910, 0.98, 2.73], [8.5, 0.840, 0.99, 2.42],
            [9.7, 0.735, 1.00, 2.23], [10.75, 0.600, 1.01, 2.12],
            [11.6, 0.475, 1.03, 2.07], [12.10, 0.410, 1.04, 2.05],
        ],
        cabGlass: [4.55, 6.75, 0.388, 0.612], cabSideWindow: [2.65, 4.7],
        lampStyle: 'swept', lampX: 7.65, lampU: 0.325,
        windowBand: true, windowWidth: 1.14, windowHeight: 0.69, windowPitch: 1.54,
        doorXs: [-10.4, 10.4], cabDoorXs: [-10.4, 1.65],
        roofStyle: 'faired', pantographCars: [3, 5], carCount: 8, livery: 'blue', noseCove: 0.07,
    },
    CRH380B: {
        id: 'CRH380B', label: 'CRH380B', variant: 'CRH380B', brand: '和谐号',
        sourceUrl: 'https://www.china-emu.cn/Trains/Model/Detail-31002-101-S.html',
        description: '白蓝车身、延长椭圆头、较高的饱满前罩和靠近鼻端的椭圆灯组。',
        bodyColor: 0xd6dddc, roofColor: 0x89959c, stripeColor: 0x2466a6, secondaryColor: 0x6799c2,
        nose: [
            [3.4, 1.000, 0.96, 3.88], [4.7, 0.996, 0.97, 3.83],
            [6.0, 0.985, 0.98, 3.73], [7.2, 0.945, 1.01, 3.48],
            [8.35, 0.885, 1.05, 3.20], [9.45, 0.790, 1.09, 2.95],
            [10.4, 0.680, 1.14, 2.71], [11.15, 0.565, 1.19, 2.55],
            [11.75, 0.460, 1.24, 2.45], [12.10, 0.405, 1.26, 2.42],
        ],
        cabGlass: [5.75, 8.05, 0.371, 0.629], cabSideWindow: [4.15, 6.1],
        lampStyle: 'round', lampX: 10.45, lampU: 0.348,
        windowBand: true, windowWidth: 1.18, windowHeight: 0.76, windowPitch: 1.58,
        doorXs: [-10.4, 10.4], cabDoorXs: [-10.4, 3.3],
        roofStyle: 'faired', pantographCars: [2, 5], carCount: 8, livery: 'blue', noseCove: 0.04,
    },
    CRH6: {
        id: 'CRH6', label: 'CRH6', variant: 'CRH6A', brand: '和谐号',
        sourceUrl: 'https://www.china-emu.cn/Trains/Model/Detail-26001-201-S.html',
        description: '白色城际车身、黑色窗带与窗上橙色细线、短直鼻头、大黑前脸和宽客门。',
        bodyColor: 0xd9e0dc, roofColor: 0x89999c, stripeColor: 0xe69724, secondaryColor: 0xf2b85d,
        nose: [
            [8.2, 1.000, 0.96, 3.88], [8.85, 0.997, 0.96, 3.84],
            [9.45, 0.985, 0.97, 3.70], [10.0, 0.945, 0.98, 3.35],
            [10.6, 0.870, 0.99, 2.90], [11.15, 0.770, 1.01, 2.55],
            [11.6, 0.680, 1.03, 2.32], [11.9, 0.615, 1.05, 2.24],
            [12.10, 0.580, 1.06, 2.20],
        ],
        cabGlass: [9.1, 11.0, 0.35, 0.65], cabSideWindow: [8.25, 9.65],
        lampStyle: 'bar', lampX: 11.26, lampU: 0.5,
        windowBand: true, windowWidth: 1.4, windowHeight: 0.85, windowPitch: 1.85,
        doorXs: [-8.2, 8.2], cabDoorXs: [-8.2, 4.4],
        roofStyle: 'pods', pantographCars: [1, 6], carCount: 8, livery: 'metro', noseCove: 0.02,
    },
}

export const EMU_MODEL_OPTIONS = Object.values(EMU_MODEL_PROFILES).map(profile => ({
    value: profile.id,
    label: profile.label,
    subtitle: profile.variant === profile.label ? profile.brand : `${profile.brand} · ${profile.variant}`,
}))

/** Resolve family names and subtype/vehicle suffixes from imported train data. */
export function resolveEmuModel(value: unknown): EmuModelId {
    if (typeof value !== 'string') return 'CR400AF'
    const normalized = value.normalize('NFKC').toUpperCase().replace(/[\s_\-–—]+/g, '')
    // Match the entire model token: CRH11 / CRH20 and unknown letter suffixes
    // must not silently select a different family. Known subtypes and 3–5 digit
    // vehicle numbers remain accepted, including Chinese labels around a token.
    const tokens = normalized.match(/CR[A-Z0-9]+/g) ?? []
    const families: readonly [EmuModelId, RegExp][] = [
        ['CR400AF', /^CR400AF(?:A[ESZ]?|B[SZ]?|C|G|S|Z)?(?:\d{3,5})?$/],
        ['CR400BF', /^CR400BF(?:A[ESZ]?|B[SZ]?|C|GZ?|S|Z)?(?:\d{3,5})?$/],
        ['CR200J', /^CR200J(?:[ABC])?(?:\d{1,5})?$/],
        ['CR450', /^CR450(?:AF|BF)?(?:\d{3,5})?$/],
        ['CRH380A', /^CRH380A(?:L|M|J)?(?:\d{3,5})?$/],
        ['CRH380B', /^CRH380B(?:G|L)?(?:\d{3,5})?$/],
        ['CRH3C', /^CRH3C(?:\d{3,5})?$/],
        ['CRH1', /^CRH1(?:AA?|B|E)?(?:\d{3,5})?$/],
        ['CRH2', /^CRH2(?:[ABCEGJ])?(?:\d{3,5})?$/],
        ['CRH5', /^CRH5(?:[AEGJ])?(?:\d{3,5})?$/],
        ['CRH6', /^CRH6(?:[AF]A?)?(?:\d{3,5})?$/],
    ]
    for (const token of tokens) {
        const family = families.find(([, pattern]) => pattern.test(token))
        if (family) return family[0]
    }
    return 'CR400AF'
}
