import i18n from '@/i18n'

const MISSING_REFERENCE_CODES = new Set([
    'HUMP_REFERENCE_MISSING',
    'HUMP_CALCULATION_DEPENDENCY_MISSING'
])

type MissingDependencyPayload = {
    type?: string
    Type?: string
    label?: string
    Label?: string
    value?: string | null
    Value?: string | null
}

const getResponseData = (error: unknown): any => {
    return (error as any)?.response?.data
}

const normalizeDependency = (item: MissingDependencyPayload) => {
    return {
        type: item.type || item.Type || 'unknown',
        label: item.label || item.Label || '',
        value: item.value ?? item.Value ?? ''
    }
}

const getCode = (data: any) => {
    return data?.code || data?.Code || data?.legacyCode || data?.LegacyCode
}

const getLegacyCode = (data: any) => {
    return data?.legacyCode || data?.LegacyCode
}

const getMissingDependencies = (data: any) => {
    const dependencies = data?.missingDependencies || data?.MissingDependencies
    if (!Array.isArray(dependencies)) {
        return []
    }

    return dependencies.map(normalizeDependency)
}

export const isHumpMissingReferenceError = (error: unknown) => {
    const data = getResponseData(error)
    return MISSING_REFERENCE_CODES.has(getCode(data)) || MISSING_REFERENCE_CODES.has(getLegacyCode(data))
}

export const getHumpMissingReferenceMessage = (
    error: unknown,
    fallbackKey = 'hump.referenceMissing.fallback'
) => {
    const data = getResponseData(error)
    if (!isHumpMissingReferenceError(error)) {
        return i18n.global.t(fallbackKey)
    }

    const dependencies = getMissingDependencies(data)
    if (dependencies.length === 0) {
        return data?.message || data?.Message || i18n.global.t(fallbackKey)
    }

    const labels: string[] = []
    const items = dependencies.map(item => {
        const typeLabel = i18n.global.t(`hump.referenceMissing.labels.${item.type}`)
        const label = item.label || (typeLabel === `hump.referenceMissing.labels.${item.type}`
            ? (item.label || i18n.global.t('hump.referenceMissing.labels.unknown'))
            : typeLabel)
        if (!labels.includes(label)) {
            labels.push(label)
        }

        const value = item.value || i18n.global.t('hump.referenceMissing.valueEmpty')
        return i18n.global.t('hump.referenceMissing.item', { label, value })
    }).join(i18n.global.t('hump.referenceMissing.separator'))
    const targets = labels.join(i18n.global.t('hump.referenceMissing.targetSeparator'))

    return i18n.global.t('hump.referenceMissing.message', { items, targets })
}
