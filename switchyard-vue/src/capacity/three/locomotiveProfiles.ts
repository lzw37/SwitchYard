/**
 * Representative standard liveries for the station simulation's electric locomotives.
 * Dimensions use metres. These are visual models, not manufacturing drawings:
 * cab surfaces, detail placement and colours are approximated from the linked
 * manufacturer's photographs. The raised-pantograph envelope is a modelling value.
 */
export type LocomotiveModelId = 'HXD1D' | 'HXD3D' | 'HXD3C'

export interface LocomotiveProfile {
    id: LocomotiveModelId
    label: string
    brand: '和谐电力机车'
    variant: string
    description: string
    sourceUrl: string
    dimensions: {
        /** Front-to-rear coupler-centre distance; the body is shorter. */
        length: number
        width: number
        /** Visual envelope including the raised pantograph. */
        height: number
        railGauge: number
        /** Carbody roof height above the rail; excludes electrical equipment. */
        roofHeight: number
    }
    /** Longitudinal centres of the two three-axle bogies. */
    bogieCenters: readonly [number, number]
    /** Three local axle positions; the second bogie mirrors these offsets. */
    axleOffsets: readonly [number, number, number]
    bodyColor: number
    roofColor: number
    accentColor: number
    secondaryColor: number
    cabStyle: 'angled' | 'rounded' | 'flat'
    livery: 'red-silver' | 'red-cream' | 'blue-white'
    grilleStyle: 'horizontal' | 'vertical' | 'modular'
    windowStyle: 'split' | 'panoramic'
    headlampStyle: 'twin-upper' | 'triple' | 'paired'
    carCount: 1
}

export const LOCOMOTIVE_PROFILES: Record<LocomotiveModelId, LocomotiveProfile> = {
    HXD1D: {
        id: 'HXD1D', label: 'HXD1D', brand: '和谐电力机车', variant: 'HXD1D 标准红灰涂装',
        sourceUrl: 'https://www.crrcgc.cc/zj/2013-11/24/article_C08B65DE6AF243A1B41B1828EA04EE2A.html',
        description: '红色车体与银灰下裙、折面双司机室、分体前窗、上部双灯及横向通风百叶。',
        dimensions: { length: 22.446, width: 3.098, height: 6.0, railGauge: 1.435, roofHeight: 4.1 },
        // Passenger-locomotive running gear, with visually approximated pivot positions.
        bogieCenters: [-5.88, 5.88], axleOffsets: [-2.075, 0.075, 2.075],
        bodyColor: 0xb91e2d, roofColor: 0x849198, accentColor: 0xd5d9d7, secondaryColor: 0x59636b,
        cabStyle: 'angled', livery: 'red-silver', grilleStyle: 'horizontal',
        windowStyle: 'split', headlampStyle: 'twin-upper', carCount: 1,
    },
    HXD3D: {
        id: 'HXD3D', label: 'HXD3D', brand: '和谐电力机车', variant: 'HXD3D 标准红白涂装',
        sourceUrl: 'https://www.crrcgc.cc/dl/2016-04/20/article_D801B2B30B0E487880A9ED77E479BC3B.html',
        description: '红色车体、奶白腰带与圆润宽车头，分体驾驶窗、中央上灯及两侧灯组、分组散热格栅。',
        // CRRC Dalian: 22989 × 3100 × 4100 mm; bogie wheelbase 2350 + 2000 mm.
        dimensions: { length: 22.989, width: 3.1, height: 6.0, railGauge: 1.435, roofHeight: 4.1 },
        bogieCenters: [-6.15, 6.15], axleOffsets: [-2.175, 0.175, 2.175],
        bodyColor: 0xbf2436, roofColor: 0x7f8890, accentColor: 0xebe8dd, secondaryColor: 0x586570,
        cabStyle: 'rounded', livery: 'red-cream', grilleStyle: 'modular',
        windowStyle: 'split', headlampStyle: 'triple', carCount: 1,
    },
    HXD3C: {
        id: 'HXD3C', label: 'HXD3C', brand: '和谐电力机车', variant: 'HXD3C 标准蓝白涂装',
        sourceUrl: 'https://www.crrcgc.cc/dl/2016-04/20/article_0EA2A47D78B54CDB8810B68035FF5438.html',
        description: '蓝白车体、方正双司机室、双片前窗、成对车灯、竖向侧墙通风格栅及黄黑排障器。',
        // CRRC Dalian: 20846 × 3100 × 4100 mm; wheelbase 2250 + 2000 mm.
        // Bogie centres: https://www.crrcgc.cc/dl/2017-12/26/article_01537740EDE7456FBFE3EF0053C892FA.html
        dimensions: { length: 20.846, width: 3.1, height: 6.0, railGauge: 1.435, roofHeight: 4.1 },
        bogieCenters: [-5.26, 5.26], axleOffsets: [-2.125, 0.125, 2.125],
        bodyColor: 0x245683, roofColor: 0x8b98a0, accentColor: 0xe4e9e5, secondaryColor: 0x596a78,
        cabStyle: 'flat', livery: 'blue-white', grilleStyle: 'vertical',
        windowStyle: 'split', headlampStyle: 'paired', carCount: 1,
    },
}

export const LOCOMOTIVE_MODEL_OPTIONS = Object.values(LOCOMOTIVE_PROFILES).map(profile => ({
    value: profile.id,
    label: profile.label,
    subtitle: profile.brand,
}))

/** Resolve imported model labels and four-digit locomotive numbers without guessing a family. */
export function resolveLocomotiveModel(value: unknown): LocomotiveModelId | null {
    if (typeof value !== 'string') return null
    const normalized = value.normalize('NFKC').toUpperCase()
        .replace(/[\s_\-–—]+/g, '')
        .replace(/和谐(?:号)?(?:电力机车|电)?([一三13])([CD])/g, (_match, digit: string, suffix: string) =>
            `HXD${digit === '一' ? '1' : digit === '三' ? '3' : digit}${suffix}`)
    // Consume the complete alphanumeric token so HXD3CA, HXD3, HXD1 and
    // similarly named unsupported types cannot fall through to another model.
    const tokens = normalized.match(/HXD[A-Z0-9]+/g) ?? []
    for (const token of tokens) {
        const match = /^(HXD1D|HXD3D|HXD3C)(?:\d{4})?$/.exec(token)
        if (match) return match[1] as LocomotiveModelId
    }
    return null
}
