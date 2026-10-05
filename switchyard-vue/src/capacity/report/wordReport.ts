import { strToU8, zipSync } from 'fflate'

export interface ReportFigure {
    svg: string
    width: number
    height: number
    caption: string
}

export type ReportBlock =
    | { kind: 'paragraph'; text: string; keepNext?: boolean }
    | { kind: 'heading'; text: string; level: 1 | 2 }
    | { kind: 'table'; headers: string[]; rows: string[][]; widths?: number[] }
    | { kind: 'figure'; figure: ReportFigure }

export interface WordReport {
    title: string
    subtitle: string
    generatedAt: string
    blocks: ReportBlock[]
}

/** Injectable so the OOXML package can also be verified without a browser. */
export type FigurePngRenderer = (figure: ReportFigure) => Promise<Uint8Array>

const XML_HEADER = '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
const WORD_NS = 'http://schemas.openxmlformats.org/wordprocessingml/2006/main'
const REL_NS = 'http://schemas.openxmlformats.org/officeDocument/2006/relationships'
const PACKAGE_REL_NS = 'http://schemas.openxmlformats.org/package/2006/relationships'
const CONTENT_TYPES_NS = 'http://schemas.openxmlformats.org/package/2006/content-types'
const SVG_NS = 'http://www.w3.org/2000/svg'
const DOCX_MIME = 'application/vnd.openxmlformats-officedocument.wordprocessingml.document'
const PAGE_WIDTH = 11906
const PAGE_HEIGHT = 16838
const MARGIN = 1134
const BODY_WIDTH = PAGE_WIDTH - MARGIN * 2
const EMUS_PER_TWIP = 635
const EMUS_PER_PIXEL = 9525
// Reserve room for the figure caption and paragraph spacing on an A4 page.
const MAX_FIGURE_HEIGHT = (PAGE_HEIGHT - MARGIN * 2 - 1600) * EMUS_PER_TWIP

function xml(value: string): string {
    // XML 1.0 disallows control characters and unpaired UTF-16 surrogates.
    const clean = Array.from(String(value)).map(character => {
        const point = character.codePointAt(0)!
        return point === 9 || point === 10 || point === 13 ||
            (point >= 0x20 && point <= 0xd7ff) || (point >= 0xe000 && point <= 0xfffd) || point >= 0x10000
            ? character : '\ufffd'
    }).join('')
    return clean.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;').replace(/'/g, '&apos;')
}

function textRuns(text: string, properties = ''): string {
    return String(text).replace(/\r\n?/g, '\n').split('\n').map((line, index) =>
        `${index ? '<w:r><w:br/></w:r>' : ''}<w:r>${properties ? `<w:rPr>${properties}</w:rPr>` : ''}<w:t xml:space="preserve">${xml(line)}</w:t></w:r>`,
    ).join('')
}

function paragraph(text: string, style = 'Normal', properties = ''): string {
    return `<w:p><w:pPr><w:pStyle w:val="${style}"/>${properties}</w:pPr>${textRuns(text)}</w:p>`
}

function figureSize(figure: ReportFigure): { cx: number; cy: number } {
    if (!Number.isFinite(figure.width) || !Number.isFinite(figure.height) || figure.width <= 0 || figure.height <= 0) {
        throw new Error('报告图形的宽度和高度必须为正数。')
    }
    const scale = Math.min(BODY_WIDTH * EMUS_PER_TWIP / figure.width, MAX_FIGURE_HEIGHT / figure.height, EMUS_PER_PIXEL)
    return { cx: Math.max(1, Math.round(figure.width * scale)), cy: Math.max(1, Math.round(figure.height * scale)) }
}

function validateSvg(figure: ReportFigure): void {
    figureSize(figure)
    if (!/<svg(?:\s|>)/i.test(figure.svg)) throw new Error('报告图形缺少 SVG 内容。')
    // XMLSerializer escapes quotes inside style attributes, including url("#marker").
    // Inspect the decoded reference without changing the SVG bytes stored in Word.
    const portableReference = (value: string) => {
        const reference = value.replace(/&quot;/gi, '"').replace(/&apos;/gi, "'").trim()
            .replace(/^(["'])(.*)\1$/, '$2').trim()
        return reference.startsWith('#') || /^data:image\//i.test(reference)
    }
    // Report SVGs must be standalone; external assets would disappear after download.
    if (/<(?:script|foreignObject)\b|<!DOCTYPE|<!ENTITY|@import\b/i.test(figure.svg) ||
        /(?:href|src)\s*=\s*["'](?!#|data:image\/)[^"']+/i.test(figure.svg) ||
        [...figure.svg.matchAll(/url\(([^)]*)\)/gi)].some(match => !portableReference(match[1] || ''))) {
        throw new Error('报告图形必须为不依赖外部资源的 SVG。')
    }
}

function table(block: Extract<ReportBlock, { kind: 'table' }>): string {
    if (!block.headers.length || block.rows.some(row => row.length !== block.headers.length)) {
        throw new Error('报告表格的列数不一致。')
    }
    const weights = block.widths ?? block.headers.map(() => 1)
    if (weights.length !== block.headers.length || weights.some(weight => !Number.isFinite(weight) || weight <= 0)) {
        throw new Error('报告表格的列宽必须与列数一致且均为正数。')
    }
    const total = weights.reduce((sum, weight) => sum + weight, 0)
    let assigned = 0
    const widths = weights.map((weight, index) => {
        const width = index === weights.length - 1 ? BODY_WIDTH - assigned : Math.floor(BODY_WIDTH * weight / total)
        assigned += width
        return width
    })
    const borders = ['top', 'left', 'bottom', 'right', 'insideH', 'insideV'].map(side => `<w:${side} w:val="single" w:sz="4" w:color="D9D9D9"/>`).join('')
    const rows = [block.headers, ...block.rows].map((row, rowIndex) => {
        const header = rowIndex === 0
        const cells = row.map((cell, column) => {
            const fill = header ? 'DCE6F1' : rowIndex % 2 === 0 ? 'F4F7FA' : 'FFFFFF'
            const numeric = /^\s*[+-]?[\d.,]+\s*(?:%|％|分|分钟|列|对|次|小时)?\s*$/.test(cell)
            return `<w:tc><w:tcPr><w:tcW w:w="${widths[column]}" w:type="dxa"/><w:shd w:val="clear" w:color="auto" w:fill="${fill}"/><w:vAlign w:val="center"/></w:tcPr>` +
                `<w:p><w:pPr><w:pStyle w:val="TableText"/><w:jc w:val="${header || numeric ? 'center' : 'left'}"/></w:pPr>` +
                textRuns(cell, header ? '<w:b/><w:color w:val="000000"/>' : '') + '</w:p></w:tc>'
        }).join('')
        // Allow very long data rows to continue on another page; repeat the header.
        return `<w:tr>${header ? '<w:trPr><w:tblHeader/><w:cantSplit/></w:trPr>' : ''}${cells}</w:tr>`
    }).join('')
    return `<w:tbl><w:tblPr><w:tblW w:w="${BODY_WIDTH}" w:type="dxa"/><w:tblBorders>${borders}</w:tblBorders><w:tblLayout w:type="fixed"/><w:tblCellMar><w:top w:w="90" w:type="dxa"/><w:left w:w="100" w:type="dxa"/><w:bottom w:w="90" w:type="dxa"/><w:right w:w="100" w:type="dxa"/></w:tblCellMar></w:tblPr><w:tblGrid>${widths.map(width => `<w:gridCol w:w="${width}"/>`).join('')}</w:tblGrid>${rows}</w:tbl>` +
        '<w:p><w:pPr><w:spacing w:after="100" w:line="40" w:lineRule="exact"/></w:pPr></w:p>'
}

function figureDrawing(figure: ReportFigure, id: number): string {
    const { cx, cy } = figureSize(figure)
    return `<w:p><w:pPr><w:keepNext/><w:spacing w:before="120" w:after="60" w:line="240" w:lineRule="auto"/><w:jc w:val="center"/></w:pPr><w:r><w:drawing>` +
        `<wp:inline distT="0" distB="0" distL="0" distR="0"><wp:extent cx="${cx}" cy="${cy}"/><wp:effectExtent l="0" t="0" r="0" b="0"/>` +
        `<wp:docPr id="${id}" name="图 ${id}" descr="${xml(figure.caption)}"/><wp:cNvGraphicFramePr><a:graphicFrameLocks noChangeAspect="1"/></wp:cNvGraphicFramePr>` +
        `<a:graphic><a:graphicData uri="http://schemas.openxmlformats.org/drawingml/2006/picture"><pic:pic><pic:nvPicPr><pic:cNvPr id="0" name="figure-${id}.svg"/><pic:cNvPicPr/></pic:nvPicPr>` +
        `<pic:blipFill><a:blip r:embed="rPng${id}"><a:extLst><a:ext uri="{96DAC541-7B7A-43D3-8B79-37D633B846F1}"><asvg:svgBlip r:embed="rSvg${id}"/></a:ext></a:extLst></a:blip><a:stretch><a:fillRect/></a:stretch></pic:blipFill>` +
        `<pic:spPr><a:xfrm><a:off x="0" y="0"/><a:ext cx="${cx}" cy="${cy}"/></a:xfrm><a:prstGeom prst="rect"><a:avLst/></a:prstGeom></pic:spPr>` +
        '</pic:pic></a:graphicData></a:graphic></wp:inline></w:drawing></w:r></w:p>' + paragraph(figure.caption, 'Caption')
}

function relationship(id: string, type: string, target: string): string {
    return `<Relationship Id="${id}" Type="${REL_NS}/${type}" Target="${xml(target)}"/>`
}

function relationships(items: string[]): string {
    return `${XML_HEADER}<Relationships xmlns="${PACKAGE_REL_NS}">${items.join('')}</Relationships>`
}

function styles(): string {
    const font = '<w:rFonts w:ascii="Calibri" w:hAnsi="Calibri" w:eastAsia="宋体" w:cs="Calibri"/>'
    const headingFont = '<w:rFonts w:ascii="Calibri" w:hAnsi="Calibri" w:eastAsia="黑体"/>'
    return `${XML_HEADER}<w:styles xmlns:w="${WORD_NS}">` +
        `<w:docDefaults><w:rPrDefault><w:rPr>${font}<w:color w:val="000000"/><w:sz w:val="22"/><w:szCs w:val="22"/><w:lang w:val="zh-CN" w:eastAsia="zh-CN"/></w:rPr></w:rPrDefault><w:pPrDefault><w:pPr><w:widowControl/><w:spacing w:after="120" w:line="320" w:lineRule="auto"/></w:pPr></w:pPrDefault></w:docDefaults>` +
        '<w:style w:type="paragraph" w:default="1" w:styleId="Normal"><w:name w:val="Normal"/><w:qFormat/></w:style>' +
        `<w:style w:type="paragraph" w:styleId="Title"><w:name w:val="Title"/><w:basedOn w:val="Normal"/><w:next w:val="Subtitle"/><w:qFormat/><w:pPr><w:keepNext/><w:spacing w:before="240" w:after="200"/><w:jc w:val="center"/></w:pPr><w:rPr>${headingFont}<w:b/><w:color w:val="000000"/><w:sz w:val="40"/></w:rPr></w:style>` +
        '<w:style w:type="paragraph" w:styleId="Subtitle"><w:name w:val="Subtitle"/><w:basedOn w:val="Normal"/><w:pPr><w:keepNext/><w:spacing w:after="140"/><w:jc w:val="center"/></w:pPr><w:rPr><w:color w:val="000000"/><w:sz w:val="24"/></w:rPr></w:style>' +
        [1, 2].map(level => `<w:style w:type="paragraph" w:styleId="Heading${level}"><w:name w:val="heading ${level}"/><w:basedOn w:val="Normal"/><w:next w:val="Normal"/><w:qFormat/><w:pPr><w:keepNext/><w:keepLines/><w:spacing w:before="${level === 1 ? 320 : 220}" w:after="140"/><w:outlineLvl w:val="${level - 1}"/></w:pPr><w:rPr>${headingFont}<w:b/><w:color w:val="000000"/><w:sz w:val="${level === 1 ? 30 : 25}"/></w:rPr></w:style>`).join('') +
        '<w:style w:type="paragraph" w:styleId="Caption"><w:name w:val="caption"/><w:basedOn w:val="Normal"/><w:pPr><w:keepLines/><w:spacing w:after="160" w:line="260" w:lineRule="auto"/><w:jc w:val="center"/></w:pPr><w:rPr><w:sz w:val="19"/></w:rPr></w:style>' +
        '<w:style w:type="paragraph" w:styleId="TableText"><w:name w:val="Table text"/><w:basedOn w:val="Normal"/><w:pPr><w:wordWrap w:val="1"/><w:spacing w:after="0" w:line="260" w:lineRule="auto"/></w:pPr><w:rPr><w:sz w:val="19"/></w:rPr></w:style>' +
        '<w:style w:type="paragraph" w:styleId="Footer"><w:name w:val="footer"/><w:basedOn w:val="Normal"/><w:pPr><w:spacing w:after="0"/><w:jc w:val="center"/></w:pPr><w:rPr><w:sz w:val="18"/></w:rPr></w:style></w:styles>'
}

/** Build a completely self-contained OOXML package, retaining each original SVG. */
export async function buildWordReport(report: WordReport, renderPng: FigurePngRenderer): Promise<Uint8Array> {
    const files: Record<string, Uint8Array> = {}
    const putXml = (path: string, content: string) => { files[path] = strToU8(content) }
    const documentRelationships = [relationship('rStyles', 'styles', 'styles.xml'), relationship('rSettings', 'settings', 'settings.xml'), relationship('rFooter', 'footer', 'footer1.xml')]
    const contents = [paragraph(report.title, 'Title'), paragraph(report.subtitle, 'Subtitle'), paragraph(`生成时间：${report.generatedAt}`, 'Normal', '<w:spacing w:after="240"/><w:jc w:val="center"/>')]
    let figureId = 0
    for (const block of report.blocks) {
        switch (block.kind) {
            case 'paragraph': contents.push(paragraph(block.text, 'Normal', block.keepNext ? '<w:keepNext/>' : '')); break
            case 'heading': contents.push(paragraph(block.text, `Heading${block.level}`)); break
            case 'table': contents.push(table(block)); break
            case 'figure': {
                validateSvg(block.figure)
                const id = ++figureId
                const png = await renderPng(block.figure)
                if (png.length < 8 || ![137, 80, 78, 71, 13, 10, 26, 10].every((byte, index) => png[index] === byte)) {
                    throw new Error('报告图形的兼容预览生成失败。')
                }
                files[`word/media/figure-${id}.svg`] = strToU8(block.figure.svg)
                files[`word/media/figure-${id}.png`] = png
                documentRelationships.push(relationship(`rSvg${id}`, 'image', `media/figure-${id}.svg`), relationship(`rPng${id}`, 'image', `media/figure-${id}.png`))
                contents.push(figureDrawing(block.figure, id))
                break
            }
        }
    }
    const section = `<w:sectPr><w:footerReference w:type="default" r:id="rFooter"/><w:pgSz w:w="${PAGE_WIDTH}" w:h="${PAGE_HEIGHT}"/><w:pgMar w:top="${MARGIN}" w:right="${MARGIN}" w:bottom="${MARGIN}" w:left="${MARGIN}" w:header="480" w:footer="480" w:gutter="0"/></w:sectPr>`
    putXml('word/document.xml', `${XML_HEADER}<w:document xmlns:w="${WORD_NS}" xmlns:r="${REL_NS}" xmlns:wp="http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" xmlns:pic="http://schemas.openxmlformats.org/drawingml/2006/picture" xmlns:asvg="http://schemas.microsoft.com/office/drawing/2016/SVG/main" xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006" mc:Ignorable="asvg"><w:body>${contents.join('')}${section}</w:body></w:document>`)
    putXml('word/styles.xml', styles())
    putXml('word/settings.xml', `${XML_HEADER}<w:settings xmlns:w="${WORD_NS}"><w:updateFields w:val="true"/><w:compat><w:compatSetting w:name="compatibilityMode" w:uri="http://schemas.microsoft.com/office/word" w:val="15"/></w:compat><w:doNotAutoCompressPictures/></w:settings>`)
    const pageField = (name: string) => `<w:fldSimple w:instr=" ${name} "><w:r><w:t>1</w:t></w:r></w:fldSimple>`
    putXml('word/footer1.xml', `${XML_HEADER}<w:ftr xmlns:w="${WORD_NS}"><w:p><w:pPr><w:pStyle w:val="Footer"/></w:pPr>${textRuns('第 ')}${pageField('PAGE')}${textRuns(' 页 / 共 ')}${pageField('NUMPAGES')}${textRuns(' 页')}</w:p></w:ftr>`)
    putXml('word/_rels/document.xml.rels', relationships(documentRelationships))
    putXml('_rels/.rels', relationships([relationship('rDocument', 'officeDocument', 'word/document.xml'), `<Relationship Id="rCore" Type="http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties" Target="docProps/core.xml"/>`, relationship('rApp', 'extended-properties', 'docProps/app.xml')]))
    const generated = new Date(report.generatedAt)
    const created = Number.isNaN(generated.valueOf()) ? '' : `<dcterms:created xsi:type="dcterms:W3CDTF">${generated.toISOString()}</dcterms:created>`
    putXml('docProps/core.xml', `${XML_HEADER}<cp:coreProperties xmlns:cp="http://schemas.openxmlformats.org/package/2006/metadata/core-properties" xmlns:dc="http://purl.org/dc/elements/1.1/" xmlns:dcterms="http://purl.org/dc/terms/" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"><dc:title>${xml(report.title)}</dc:title><dc:subject>${xml(report.subtitle)}</dc:subject><dc:creator>SwitchYard</dc:creator>${created}</cp:coreProperties>`)
    putXml('docProps/app.xml', `${XML_HEADER}<Properties xmlns="http://schemas.openxmlformats.org/officeDocument/2006/extended-properties"><Application>SwitchYard</Application></Properties>`)
    const overrides = [
        ['/word/document.xml', 'application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml'],
        ['/word/styles.xml', 'application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml'],
        ['/word/settings.xml', 'application/vnd.openxmlformats-officedocument.wordprocessingml.settings+xml'],
        ['/word/footer1.xml', 'application/vnd.openxmlformats-officedocument.wordprocessingml.footer+xml'],
        ['/docProps/core.xml', 'application/vnd.openxmlformats-package.core-properties+xml'],
        ['/docProps/app.xml', 'application/vnd.openxmlformats-officedocument.extended-properties+xml'],
    ]
    putXml('[Content_Types].xml', `${XML_HEADER}<Types xmlns="${CONTENT_TYPES_NS}"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Default Extension="png" ContentType="image/png"/><Default Extension="svg" ContentType="image/svg+xml"/>${overrides.map(([part, type]) => `<Override PartName="${part}" ContentType="${type}"/>`).join('')}</Types>`)
    return zipSync(files, { level: 6 })
}

async function renderFigurePng(figure: ReportFigure): Promise<Uint8Array> {
    const documentSvg = new DOMParser().parseFromString(figure.svg, 'image/svg+xml')
    if (documentSvg.querySelector('parsererror') || documentSvg.documentElement.localName !== 'svg' || documentSvg.documentElement.namespaceURI !== SVG_NS) {
        throw new Error('报告图形的 SVG 格式无效。')
    }
    // Render only a compatibility preview. The unmodified vector SVG is also stored.
    const { cx, cy } = figureSize(figure)
    const width = Math.max(1, Math.ceil(cx / EMUS_PER_PIXEL * 2))
    const height = Math.max(1, Math.ceil(cy / EMUS_PER_PIXEL * 2))
    documentSvg.documentElement.setAttribute('width', String(figure.width))
    documentSvg.documentElement.setAttribute('height', String(figure.height))
    if (!documentSvg.documentElement.hasAttribute('viewBox')) {
        documentSvg.documentElement.setAttribute('viewBox', `0 0 ${figure.width} ${figure.height}`)
    }
    const blobUrl = URL.createObjectURL(new Blob([new XMLSerializer().serializeToString(documentSvg)], { type: 'image/svg+xml' }))
    try {
        const image = new Image()
        await new Promise<void>((resolve, reject) => {
            const timeout = setTimeout(() => reject(new Error('报告图形预览生成超时，请重试。')), 15000)
            image.onload = () => { clearTimeout(timeout); resolve() }
            image.onerror = () => { clearTimeout(timeout); reject(new Error('报告图形预览生成失败。')) }
            image.src = blobUrl
        })
        const canvas = document.createElement('canvas')
        canvas.width = width
        canvas.height = height
        const context = canvas.getContext('2d')
        if (!context) throw new Error('当前浏览器不支持报告图形导出。')
        context.fillStyle = '#ffffff'
        context.fillRect(0, 0, width, height)
        context.drawImage(image, 0, 0, width, height)
        const png = await new Promise<Blob>((resolve, reject) => canvas.toBlob(value => value ? resolve(value) : reject(new Error('报告图形预览生成失败。')), 'image/png'))
        return new Uint8Array(await png.arrayBuffer())
    } finally {
        URL.revokeObjectURL(blobUrl)
    }
}

/** Generate the downloadable Word document in the browser without a server. */
export async function createWordReport(report: WordReport): Promise<Blob> {
    const bytes = await buildWordReport(report, renderFigurePng)
    // Own the ArrayBuffer for TypeScript's BlobPart and avoid shared buffers.
    return new Blob([new Uint8Array(bytes)], { type: DOCX_MIME })
}
