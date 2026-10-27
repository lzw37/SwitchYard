import type { ReportFigure } from './wordReport'

export interface GanttReportViewState {
    scaleX: number
    scaleY: number
    /** Component dimensions, including the toolbar, used when mounting an off-screen copy. */
    width: number
    height: number
    autoFit?: boolean
    viewportWidth?: number
    viewportHeight?: number
}

/** Report-only display names. Keys identify rendered objects and never enter SVG text. */
export interface GanttReportLabels {
    rows?: Record<string, string>
    blocks?: Record<string, string>
}

const escape = (value: string) => value.replace(/[\u0000-\u0008\u000b\u000c\u000e-\u001f]/g, '')
    .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;')
const number = (value: number) => String(Math.round(value * 1000) / 1000)
const px = (value: string | undefined) => Number.parseFloat(value || '') || 0
const transparent = (color: string) => !color || color === 'transparent' || /rgba\([^)]*,\s*0\s*\)$/.test(color)

/** Chromium serializes color-mix() as color(srgb ...), which older SVG readers cannot display. */
export function ganttSvgColor(value: string): string {
    const match = /^color\(srgb\s+([\d.e+-]+)\s+([\d.e+-]+)\s+([\d.e+-]+)(?:\s*\/\s*([\d.e+-]+))?\)$/i.exec(value)
    if (!match) return value
    const channels = match.slice(1, 4).map(channel => Math.round(Math.max(0, Math.min(1, Number(channel))) * 255))
    return match[4] == null ? `rgb(${channels.join(', ')})` : `rgba(${channels.join(', ')}, ${Number(match[4])})`
}

function splitCssList(value: string): string[] {
    let depth = 0, start = 0
    const parts: string[] = []
    for (let index = 0; index < value.length; index++) {
        if (value[index] === '(') depth++
        else if (value[index] === ')') depth--
        else if (value[index] === ',' && depth === 0) { parts.push(value.slice(start, index).trim()); start = index + 1 }
    }
    parts.push(value.slice(start).trim())
    return parts
}

interface Box { x: number; y: number; width: number; height: number }

/**
 * Vectorizes the rendered Gantt, rather than maintaining another chart layout for reports.
 * A synchronous scroll reset exposes the natural positions of the sticky header/labels;
 * restoring it before yielding avoids changing the user's viewport or captured scale.
 */
export function snapshotGanttSvg(viewport: HTMLElement, caption: string, labels?: GanttReportLabels): ReportFigure | null {
    const content = viewport.querySelector<HTMLElement>('.track-occupancy-gantt-content')
    const view = viewport.ownerDocument.defaultView
    if (!content || !view) return null
    const scrollLeft = viewport.scrollLeft, scrollTop = viewport.scrollTop
    try {
        viewport.scrollLeft = 0
        viewport.scrollTop = 0
        const origin = content.getBoundingClientRect()
        const width = Math.max(origin.width, content.scrollWidth), height = Math.max(origin.height, content.scrollHeight)
        if (!(width > 0 && height > 0)) return null
        const definitions: string[] = []
        let nextID = 0
        const context = viewport.ownerDocument.createElement('canvas').getContext('2d')
        const clip = (box: Box, radius = 0) => {
            const id = `gantt-clip-${nextID++}`
            definitions.push(`<clipPath id="${id}"><rect x="${number(box.x)}" y="${number(box.y)}" width="${number(box.width)}" height="${number(box.height)}" rx="${number(radius)}"/></clipPath>`)
            return `url(#${id})`
        }
        const rect = (box: Box, attributes: string) => `<rect x="${number(box.x)}" y="${number(box.y)}" width="${number(Math.max(0, box.width))}" height="${number(Math.max(0, box.height))}" ${attributes}/>`
        const boxFor = (element: Element): Box => {
            const bounds = element.getBoundingClientRect()
            return { x: bounds.left - origin.left, y: bounds.top - origin.top, width: bounds.width, height: bounds.height }
        }
        const line = (x1: number, y1: number, x2: number, y2: number, color: string, thickness: number, style: string) => {
            if (thickness <= 0 || style === 'none' || transparent(color)) return ''
            const dash = style === 'dashed' ? ` stroke-dasharray="${number(thickness * 3)} ${number(thickness * 3)}"` : style === 'dotted' ? ` stroke-dasharray="${number(thickness)} ${number(thickness)}"` : ''
            return `<line x1="${number(x1)}" y1="${number(y1)}" x2="${number(x2)}" y2="${number(y2)}" stroke="${escape(ganttSvgColor(color))}" stroke-width="${number(thickness)}"${dash}/>`
        }
        const background = (style: CSSStyleDeclaration, box: Box, radius: number) => {
            let result = transparent(style.backgroundColor) ? '' : rect(box, `fill="${escape(ganttSvgColor(style.backgroundColor))}" rx="${number(radius)}"`)
            const gradient = /^linear-gradient\((.*)\)$/.exec(style.backgroundImage)
            if (!gradient) return result
            const entries = splitCssList(gradient[1] || '')
            let angle = 180
            if (/deg$/.test(entries[0] || '')) angle = px(entries.shift())
            else if (/^to /.test(entries[0] || '')) {
                const direction = entries.shift() || ''
                angle = direction.includes('right') ? 90 : direction.includes('left') ? 270 : direction.includes('top') ? 0 : 180
            }
            const radians = angle * Math.PI / 180
            const dx = Math.sin(radians), dy = -Math.cos(radians)
            const length = Math.abs(box.width * dx) + Math.abs(box.height * dy)
            const stops = entries.map(entry => {
                const match = /^(.*?)(?:\s+(-?[\d.]+)(%|px))?$/.exec(entry)
                return { color: match?.[1] || entry, position: match?.[2] == null ? null : Number(match[2]) / (match[3] === '%' ? 100 : length || 1) }
            })
            if (!stops.length) return result
            stops[0]!.position ??= 0
            stops[stops.length - 1]!.position ??= 1
            for (let start = 0; start < stops.length - 1;) {
                let end = start + 1
                while (end < stops.length - 1 && stops[end]!.position == null) end++
                const a = stops[start]!.position || 0, b = Math.max(a, stops[end]!.position || 0)
                stops[end]!.position = b
                for (let index = start + 1; index < end; index++) stops[index]!.position = a + (b - a) * (index - start) / (end - start)
                start = end
            }
            const id = `gantt-gradient-${nextID++}`
            definitions.push(`<linearGradient id="${id}" gradientUnits="userSpaceOnUse" x1="${number(box.x + box.width / 2 - dx * length / 2)}" y1="${number(box.y + box.height / 2 - dy * length / 2)}" x2="${number(box.x + box.width / 2 + dx * length / 2)}" y2="${number(box.y + box.height / 2 + dy * length / 2)}">${stops.map(stop => `<stop offset="${number(stop.position || 0)}" stop-color="${escape(ganttSvgColor(stop.color))}"/>`).join('')}</linearGradient>`)
            result += rect(box, `fill="url(#${id})" rx="${number(radius)}"`)
            return result
        }
        const shadows = (style: CSSStyleDeclaration, box: Box, radius: number, inset: boolean) => splitCssList(style.boxShadow || 'none').map(shadow => {
            if (shadow === 'none') return ''
            if (shadow.includes('inset') !== inset) return ''
            const color = /(?:rgba?\([^)]*\)|color\([^)]*\)|#[\da-f]+)/i.exec(shadow)?.[0]
            if (!color) return ''
            const dimensions = shadow.replace(color, '').match(/-?[\d.]+px/g)?.map(px) || []
            const [x = 0, y = 0, blur = 0, spread = 0] = dimensions
            if (blur !== 0) return '' // Current Gantt shadows are crisp; no bitmap filters are required.
            const paint = escape(ganttSvgColor(color))
            if (shadow.includes('inset')) {
                let result = ''
                if (x > 0 || spread > 0) result += rect({ ...box, width: Math.min(box.width, Math.max(0, x + spread)) }, `fill="${paint}"`)
                if (x < 0 || spread > 0) result += rect({ ...box, x: box.x + box.width - Math.max(0, spread - x), width: Math.max(0, spread - x) }, `fill="${paint}"`)
                if (y > 0 || spread > 0) result += rect({ ...box, height: Math.min(box.height, Math.max(0, y + spread)) }, `fill="${paint}"`)
                if (y < 0 || spread > 0) result += rect({ ...box, y: box.y + box.height - Math.max(0, spread - y), height: Math.max(0, spread - y) }, `fill="${paint}"`)
                return result
            }
            return rect({ x: box.x + x - spread, y: box.y + y - spread, width: box.width + spread * 2, height: box.height + spread * 2 }, `fill="${paint}" rx="${number(radius + spread)}"`)
        }).join('')
        const reportLabel = (element: HTMLElement): string | undefined => {
            if (!labels) return undefined
            let values: Record<string, string> | undefined, key: string | null | undefined
            if (element.classList.contains('track-occupancy-gantt-block-label')) {
                values = labels.blocks
                key = element.parentElement?.getAttribute('data-block-key')
            } else {
                const rowLabel = element.classList.contains('track-occupancy-gantt-lane-label') ? element
                    : element.parentElement?.classList.contains('track-occupancy-gantt-lane-label') ? element.parentElement : null
                if (!rowLabel) return undefined
                values = labels.rows
                key = rowLabel.parentElement?.getAttribute('data-row-key')
            }
            return key != null && values && Object.prototype.hasOwnProperty.call(values, key) && typeof values[key] === 'string'
                ? values[key] : undefined
        }
        const text = (element: HTMLElement, style: CSSStyleDeclaration, box: Box) => {
            const nodes = Array.from(element.childNodes).filter(node => node.nodeType === 3 && node.textContent?.trim())
            if (!nodes.length) return ''
            const fontSize = px(style.fontSize), left = px(style.paddingLeft) + px(style.borderLeftWidth), right = px(style.paddingRight) + px(style.borderRightWidth)
            const available = Math.max(0, box.width - left - right)
            if (context) {
                context.font = `${style.fontStyle || 'normal'} ${style.fontWeight || '400'} ${style.fontSize} ${style.fontFamily}`
                context.fontKerning = style.fontKerning as CanvasFontKerning
            }
            return nodes.map(node => {
                const range = viewport.ownerDocument.createRange()
                range.selectNodeContents(node)
                const bounds = range.getBoundingClientRect()
                const source = node.textContent || ''
                const replacement = reportLabel(element)
                const replaced = replacement !== undefined && replacement !== source
                let value = replaced ? replacement : source
                const measure = (value: string) => (context?.measureText(value).width ?? value.length * fontSize * 0.6) + Math.max(0, value.length - 1) * px(style.letterSpacing)
                const shortened = style.textOverflow === 'ellipsis' && (replaced ? measure(value) : bounds.width) > available
                if (shortened) {
                    const characters = Array.from(value)
                    let lo = 0, hi = characters.length
                    while (lo < hi) {
                        const mid = Math.ceil((lo + hi) / 2)
                        if (measure(characters.slice(0, mid).join('') + '…') <= available) lo = mid
                        else hi = mid - 1
                    }
                    value = measure('…') <= available ? characters.slice(0, lo).join('') + '…' : ''
                }
                const metrics = context?.measureText(value)
                const ascent = metrics?.fontBoundingBoxAscent ?? fontSize * 0.8
                const descent = metrics?.fontBoundingBoxDescent ?? fontSize * 0.2
                let x = bounds.left - origin.left
                if (replaced) {
                    // The source Range belongs to the UI label (possibly a much longer ID).
                    // Re-measure the report label inside its unchanged box and preserve the
                    // CSS alignment, including centered editable bars and right-aligned text.
                    const textWidth = measure(value)
                    x = box.x + left
                    const align = style.textAlign
                    if (align === 'center') x += (available - textWidth) / 2
                    else if (align === 'right' || (align === 'end' && style.direction !== 'rtl') || (align === 'start' && style.direction === 'rtl')) x += available - textWidth
                    if (style.direction === 'rtl') x += textWidth
                } else if (shortened) x = box.x + left
                const y = bounds.top - origin.top + (bounds.height - ascent - descent) / 2 + ascent
                const writing = style.writingMode && style.writingMode !== 'horizontal-tb' ? ` writing-mode="${escape(style.writingMode)}" text-orientation="${escape(style.textOrientation)}"` : ''
                return `<text x="${number(x)}" y="${number(y)}" fill="${escape(ganttSvgColor(style.color))}" font-family="${escape(style.fontFamily)}" font-size="${number(fontSize)}" font-weight="${escape(style.fontWeight)}" font-style="${escape(style.fontStyle)}" letter-spacing="${escape(style.letterSpacing)}" direction="${escape(style.direction)}" xml:space="preserve"${writing}>${escape(value)}</text>`
            }).join('')
        }
        const render = (element: HTMLElement): string => {
            // Handles and toolbars are controls; keeping the text's measured position still
            // preserves the visual padding of editable blocks without exporting drag affordances.
            if (element.classList.contains('track-occupancy-gantt-handle')) return ''
            const style = view.getComputedStyle(element)
            if (style.display === 'none' || style.visibility === 'hidden' || style.opacity === '0') return ''
            const box = boxFor(element), radius = px(style.borderTopLeftRadius)
            let result = shadows(style, box, radius, false) + background(style, box, radius) + shadows(style, box, radius, true)
            const borders = [style.borderTopWidth, style.borderRightWidth, style.borderBottomWidth, style.borderLeftWidth].map(px)
            const colors = [style.borderTopColor, style.borderRightColor, style.borderBottomColor, style.borderLeftColor]
            const styles = [style.borderTopStyle, style.borderRightStyle, style.borderBottomStyle, style.borderLeftStyle]
            const [top = 0, right = 0, bottom = 0, left = 0] = borders
            if (top > 0 && styles[0] !== 'none' && borders.every(value => value === top) && colors.every(value => value === colors[0]) && styles.every(value => value === styles[0])) {
                const dash = styles[0] === 'dashed' ? ` stroke-dasharray="${number(top * 3)} ${number(top * 3)}"` : styles[0] === 'dotted' ? ` stroke-dasharray="${number(top)} ${number(top)}"` : ''
                result += rect({ x: box.x + top / 2, y: box.y + top / 2, width: box.width - top, height: box.height - top }, `fill="none" stroke="${escape(ganttSvgColor(colors[0] || 'transparent'))}" stroke-width="${number(top)}" rx="${number(Math.max(0, radius - top / 2))}"${dash}`)
            } else {
                result += line(box.x, box.y + top / 2, box.x + box.width, box.y + top / 2, colors[0] || '', top, styles[0] || '')
                result += line(box.x + box.width - right / 2, box.y, box.x + box.width - right / 2, box.y + box.height, colors[1] || '', right, styles[1] || '')
                result += line(box.x, box.y + box.height - bottom / 2, box.x + box.width, box.y + box.height - bottom / 2, colors[2] || '', bottom, styles[2] || '')
                result += line(box.x + left / 2, box.y, box.x + left / 2, box.y + box.height, colors[3] || '', left, styles[3] || '')
            }
            const children = Array.from(element.children) as HTMLElement[]
            children.sort((a, b) => px(view.getComputedStyle(a).zIndex) - px(view.getComputedStyle(b).zIndex))
            let foreground = text(element, style, box) + children.map(render).join('')
            if (style.overflowX === 'hidden' || style.overflowY === 'hidden' || style.overflow === 'hidden') foreground = `<g clip-path="${clip(box, radius)}">${foreground}</g>`
            result += foreground
            return `<g opacity="${escape(style.opacity || '1')}">${result}</g>`
        }
        const rendered = render(content)
        const inheritedOpacity = view.getComputedStyle(viewport.parentElement || viewport).opacity || '1'
        return {
            caption, width, height,
            svg: `<svg xmlns="http://www.w3.org/2000/svg" width="${number(width)}" height="${number(height)}" viewBox="0 0 ${number(width)} ${number(height)}" role="img"><title>${escape(caption)}</title><defs>${definitions.join('')}</defs><rect width="100%" height="100%" fill="#ffffff"/><g opacity="${escape(inheritedOpacity)}">${rendered}</g></svg>`,
        }
    } finally {
        viewport.scrollLeft = scrollLeft
        viewport.scrollTop = scrollTop
    }
}
