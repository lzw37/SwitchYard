import type { ReportFigure } from './wordReport'

export interface StationPlanReportViewState {
    scaleX: number
    scaleY: number
    lineMode: 'straight' | 'orthogonal'
    expandedTrackKeys: string[]
    autoFit: boolean
    width: number
    height: number
    viewportWidth?: number
    viewportHeight?: number
}

export interface StationPlanReportLabels {
    rows?: Record<string, string>
    trains?: Record<string, string>
}

const SVG_NS = 'http://www.w3.org/2000/svg'
// Inline only SVG presentation properties. In particular, computed CSS width/height
// and the default CSS transform must not replace SVG geometry attributes.
const PRESENTATION_PROPERTIES = [
    'color', 'fill', 'fill-opacity', 'fill-rule', 'stroke', 'stroke-width', 'stroke-opacity',
    'stroke-linecap', 'stroke-linejoin', 'stroke-miterlimit', 'stroke-dasharray', 'stroke-dashoffset',
    'opacity', 'paint-order', 'vector-effect', 'clip-path', 'clip-rule', 'mask', 'filter',
    'marker-start', 'marker-mid', 'marker-end', 'stop-color', 'stop-opacity',
    'font-family', 'font-size', 'font-style', 'font-weight', 'font-stretch', 'font-variant',
    'font-kerning', 'font-feature-settings', 'font-variation-settings', 'letter-spacing', 'word-spacing',
    'text-anchor', 'text-decoration', 'text-rendering', 'dominant-baseline', 'alignment-baseline',
    'baseline-shift', 'white-space', 'direction', 'unicode-bidi', 'visibility', 'display',
    'shape-rendering', 'image-rendering', 'overflow',
] as const

function localReference(value: string, document: Document): string {
    return value.replace(/url\(\s*(['"]?)(.*?)\1\s*\)/gi, (_match, _quote, reference: string) => {
        if (reference.startsWith('#')) return `url(${reference})`
        try {
            const url = new URL(reference, document.baseURI)
            const base = new URL(document.baseURI)
            if (url.hash && url.origin === base.origin && url.pathname === base.pathname && url.search === base.search) return `url(${url.hash})`
        } catch { /* An invalid or external reference cannot be part of a portable SVG. */ }
        return 'none'
    })
}

export function createSvgElement<K extends keyof SVGElementTagNameMap>(document: Document, name: K, attributes: Record<string, string | number> = {}): SVGElementTagNameMap[K] {
    const element = document.createElementNS(SVG_NS, name)
    for (const [key, value] of Object.entries(attributes)) element.setAttribute(key, String(value))
    return element
}

/** A lazily mounted but display:none tab has DOM nodes, not measurable axes. */
export function hasRenderedLayout(element: Element): boolean {
    const bounds = element.getBoundingClientRect()
    return Number.isFinite(bounds.width) && Number.isFinite(bounds.height) && bounds.width > 0 && bounds.height > 0
}

/** Match CSS ellipsis without treating fractional glyph widths as real overflow. */
export function snapshotHtmlText(text: string, element: HTMLElement, measure: (text: string) => number, remeasure = false, widthOverride?: number): string {
    // clientWidth is rounded to whole CSS pixels. Comparing a Range's fractional
    // width against it incorrectly truncates labels that fit their actual box.
    if (!remeasure && element.scrollWidth <= element.clientWidth) return text
    const style = element.ownerDocument.defaultView!.getComputedStyle(element)
    if (style.getPropertyValue('text-overflow') !== 'ellipsis') return text
    const inset = ['padding-left', 'padding-right', 'border-left-width', 'border-right-width']
        .reduce((sum, key) => sum + (Number.parseFloat(style.getPropertyValue(key)) || 0), 0)
    const width = Math.max(0, widthOverride ?? element.getBoundingClientRect().width - inset)
    if (measure(text) <= width) return text
    if (measure('…') > width) return ''
    const characters = Array.from(text)
    let start = 0, end = characters.length
    while (start < end) {
        const middle = Math.ceil((start + end) / 2)
        if (measure(characters.slice(0, middle).join('') + '…') <= width) start = middle
        else end = middle - 1
    }
    return characters.slice(0, start).join('') + '…'
}

/** Remove application-only descriptions and object keys from a detached report SVG. */
export function stripReportInteractionMetadata(svg: SVGElement): void {
    for (const element of Array.from(svg.querySelectorAll('title, desc'))) element.remove()
    for (const element of [svg, ...Array.from(svg.querySelectorAll('*'))]) {
        for (const attribute of Array.from(element.attributes)) {
            if (/^(aria-|data-)/i.test(attribute.name) || ['role', 'tabindex'].includes(attribute.name.toLowerCase())) element.removeAttribute(attribute.name)
        }
    }
}

/** Clone the rendered vectors without changing the live chart or its CSS. */
export function cloneSvgWithStyles<T extends SVGElement>(source: T, options: { exclude?: string } = {}): T {
    const document = source.ownerDocument
    const view = document.defaultView
    if (!view) throw new Error('图形尚未挂载，无法读取图形样式。')
    const clone = source.cloneNode(false) as T
    // Do not retain custom properties or event handlers from the application.
    clone.removeAttribute('style')
    for (const attribute of Array.from(clone.attributes)) {
        if (/^on/i.test(attribute.name) || attribute.name === 'tabindex') clone.removeAttribute(attribute.name)
        else if (attribute.localName === 'href' && !attribute.value.startsWith('#') && !/^data:image\//i.test(attribute.value)) clone.removeAttribute(attribute.name)
        else if (/url\(/i.test(attribute.value)) clone.setAttribute(attribute.name, localReference(attribute.value, document))
    }
    const style = view.getComputedStyle(source)
    for (const property of PRESENTATION_PROPERTIES) {
        const value = style.getPropertyValue(property)
        if (value) clone.style.setProperty(property, localReference(value, document))
    }
    // A transform attribute already captures SVG geometry. A stylesheet transform
    // is copied only when it is the source of the transform (otherwise an inline
    // `transform: none` would erase rotated labels and translated process nodes).
    const transform = style.getPropertyValue('transform')
    if (transform && transform !== 'none' && (!source.hasAttribute('transform') || source.style.getPropertyValue('transform'))) {
        clone.style.setProperty('transform', transform)
        for (const property of ['transform-origin', 'transform-box']) {
            const value = style.getPropertyValue(property)
            if (value) clone.style.setProperty(property, value)
        }
    }
    for (const child of Array.from(source.childNodes)) {
        if (child.nodeType !== 1) { clone.appendChild(child.cloneNode(true)); continue }
        const element = child as SVGElement
        if (['script', 'style', 'foreignobject', 'iframe', 'object', 'embed'].includes(element.localName.toLowerCase()) ||
            (options.exclude && element.matches(options.exclude))) continue
        clone.appendChild(cloneSvgWithStyles(element, options))
    }
    return clone
}

/** Finish a portable vector figure; the caption belongs to Word, not the chart. */
export function serializeReportSvg(svg: SVGSVGElement, caption: string, width: number, height: number): ReportFigure {
    svg.setAttribute('xmlns', SVG_NS)
    svg.setAttribute('width', String(width))
    svg.setAttribute('height', String(height))
    if (!svg.hasAttribute('viewBox')) svg.setAttribute('viewBox', `0 0 ${width} ${height}`)
    const Serializer = svg.ownerDocument.defaultView?.XMLSerializer || XMLSerializer
    return { caption, width, height, svg: new Serializer().serializeToString(svg) }
}
