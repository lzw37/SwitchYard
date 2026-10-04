<template>
    <canvas
        ref="canvasRef"
        class="simulation-train-overlay"
        role="img"
        :aria-label="t('operationSimulation.trainOverlay', { count: trainCount })"
        :data-car-count="cars.length"
        :data-train-keys="trainKeys"
    />
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'

const { t } = useI18n()

interface TrainCar {
    key: string
    x: number
    y: number
    angle: number
    length: number
    width: number
    fill: string
    stroke: string
    label?: string
}

const props = defineProps<{
    cars: TrainCar[]
    viewportEl: HTMLElement | null
    sceneWidth: number
    sceneHeight: number
    projection: object
    animate: boolean
    resetRevision: number
}>()

const canvasRef = ref<HTMLCanvasElement | null>(null)
const trainKeys = computed(() => JSON.stringify(props.cars.filter(car => car.label).map(car => car.key)))
const trainCount = computed(() => props.cars.filter(car => car.label).length)
const transitionDuration = 90
const headlightHalfSpreadRatio = 0.174
let context: CanvasRenderingContext2D | null = null
let headlightTexture: HTMLCanvasElement | null = null
let viewport: HTMLElement | null = null
let resizeObserver: ResizeObserver | null = null
let frameId: number | null = null
let mounted = false
let scrollLeft = 0
let scrollTop = 0
let viewportWidth = 0
let viewportHeight = 0
let pixelRatio = 1
let targetCars: TrainCar[] = []
let sourceCars = new Map<string, TrainCar>()
let transitionStartedAt = 0
let transitioning = false

function samePose(left: TrainCar, right: TrainCar) {
    return left.x === right.x && left.y === right.y && left.angle === right.angle &&
        left.length === right.length && left.width === right.width
}

function poseAt(car: TrainCar, progress: number): TrainCar {
    const source = sourceCars.get(car.key)
    if (!source || progress >= 1) return car
    return {
        ...car,
        x: source.x + (car.x - source.x) * progress,
        y: source.y + (car.y - source.y) * progress,
        angle: source.angle + (car.angle - source.angle) * progress,
        length: source.length + (car.length - source.length) * progress,
        width: source.width + (car.width - source.width) * progress,
    }
}

function animationProgress(now: number) {
    return transitioning ? Math.min(1, Math.max(0, (now - transitionStartedAt) / transitionDuration)) : 1
}

function updateCars(cars: TrainCar[], immediate: boolean) {
    const now = performance.now()
    const previousTargets = new Map(targetCars.map(car => [car.key, car]))
    const changed = cars.some(car => {
        const previous = previousTargets.get(car.key)
        return previous && !samePose(previous, car)
    })

    if (immediate) {
        transitioning = false
        sourceCars.clear()
    } else if (changed) {
        const progress = animationProgress(now)
        sourceCars = new Map(targetCars.map(car => [car.key, poseAt(car, progress)]))
        transitioning = true
        transitionStartedAt = now
    }
    targetCars = cars
    requestDraw()
}

function requestDraw() {
    if (!mounted || frameId !== null) return
    frameId = requestAnimationFrame(draw)
}

function measureViewport() {
    if (!viewport) return
    viewportWidth = viewport.clientWidth
    viewportHeight = viewport.clientHeight
    pixelRatio = Math.min(2, Math.max(1, window.devicePixelRatio || 1))
    handleScroll()
}

function handleScroll() {
    if (!viewport) return
    scrollLeft = Math.max(0, viewport.scrollLeft)
    scrollTop = Math.max(0, viewport.scrollTop)
    requestDraw()
}

function attachViewport(element: HTMLElement | null) {
    viewport?.removeEventListener('scroll', handleScroll)
    resizeObserver?.disconnect()
    viewport = element
    if (viewport) {
        viewport.addEventListener('scroll', handleScroll, { passive: true })
        resizeObserver?.observe(viewport)
        measureViewport()
    }
}

function roundedCarPath(ctx: CanvasRenderingContext2D, length: number, width: number) {
    const left = -length / 2
    const top = -width / 2
    const right = length / 2
    const bottom = width / 2
    const radius = Math.min(2, length / 2, width / 2)
    ctx.beginPath()
    ctx.moveTo(left + radius, top)
    ctx.lineTo(right - radius, top)
    ctx.quadraticCurveTo(right, top, right, top + radius)
    ctx.lineTo(right, bottom - radius)
    ctx.quadraticCurveTo(right, bottom, right - radius, bottom)
    ctx.lineTo(left + radius, bottom)
    ctx.quadraticCurveTo(left, bottom, left, bottom - radius)
    ctx.lineTo(left, top + radius)
    ctx.quadraticCurveTo(left, top, left + radius, top)
    ctx.closePath()
}

function createHeadlightTexture() {
    // Build the soft beam once. Playback only stamps this small texture, without
    // allocating gradients, DOM nodes or blur filters for each arriving train.
    const texture = document.createElement('canvas')
    texture.width = 256
    texture.height = 160
    const textureContext = texture.getContext('2d')
    if (!textureContext) return null
    const pixels = textureContext.createImageData(texture.width, texture.height)
    const smoothFade = (value: number, start: number, end: number) => {
        const t = Math.max(0, Math.min(1, (value - start) / (end - start)))
        return 1 - t * t * (3 - 2 * t)
    }
    const widestSection = 0.8 * 0.8 * Math.sqrt(0.2)
    for (let x = 0; x < texture.width; x++) {
        const forward = x / (texture.width - 1)
        // A narrow tail grows into a rounded bulb, then closes with a curved
        // front. The tail is hidden inside the cab when the texture is drawn.
        const halfWidth = forward * forward * Math.sqrt(1 - forward) / widestSection
        const frontFade = smoothFade(forward, 0.78, 1)
        for (let y = 0; y < texture.height; y++) {
            const lateral = y / (texture.height - 1) * 2 - 1
            const across = Math.abs(lateral) / Math.max(halfWidth, 0.000001)
            const edgeFade = smoothFade(across, 0.25, 1)
            const intensity = 0.92 * (1 - 0.2 * forward) * edgeFade * frontFade
            const offset = (y * texture.width + x) * 4
            pixels.data[offset] = 255
            pixels.data[offset + 1] = 248
            pixels.data[offset + 2] = 218
            pixels.data[offset + 3] = Math.round(255 * intensity)
        }
    }
    textureContext.putImageData(pixels, 0, 0)
    return texture
}

function drawHeadlight(ctx: CanvasRenderingContext2D, car: TrainCar, width: number, height: number) {
    if (!headlightTexture) return
    const x = car.x - scrollLeft
    const y = car.y - scrollTop
    const reach = Math.min(260, Math.max(52, car.length * 4.2)) * 0.5
    const halfSpread = reach * headlightHalfSpreadRatio
    const nose = car.length / 2 - 1
    // Hide the droplet's tail inside the cab. The opaque carriage is drawn
    // afterwards, leaving a beam with a finite width at its front edge.
    const origin = nose - Math.min(car.length * 0.6, reach * 0.35)
    // Include the beam when culling: an offscreen locomotive may still cast
    // visible light into the viewport, including when travelling right to left.
    const radius = Math.max(Math.abs(origin), Math.hypot(origin + reach, halfSpread))
    if (x + radius < 0 || y + radius < 0 || x - radius > width || y - radius > height) return
    ctx.save()
    ctx.translate(x, y)
    ctx.rotate(car.angle * Math.PI / 180)
    ctx.drawImage(headlightTexture, origin, -halfSpread, reach, halfSpread * 2)
    ctx.restore()
}

function drawCar(ctx: CanvasRenderingContext2D, car: TrainCar, width: number, height: number) {
    const x = car.x - scrollLeft
    const y = car.y - scrollTop
    const radius = Math.max(Math.hypot(car.length, car.width) / 2 + 4, (car.label?.length || 0) * 6 + 3)
    if (x + radius < 0 || y + radius < 0 || x - radius > width || y - radius > height) return

    ctx.save()
    ctx.translate(x, y)
    ctx.rotate(car.angle * Math.PI / 180)
    roundedCarPath(ctx, car.length, car.width)
    ctx.fillStyle = car.fill || '#2563eb'
    ctx.strokeStyle = car.stroke || '#eff6ff'
    ctx.lineWidth = 1.5
    ctx.shadowColor = 'rgba(15, 23, 42, 0.35)'
    ctx.shadowBlur = 2
    ctx.shadowOffsetY = 1
    ctx.fill()
    ctx.stroke()
    ctx.shadowColor = 'transparent'
    if (car.label) {
        ctx.beginPath()
        ctx.moveTo(car.length / 2 - 4, -car.width / 2 + 2)
        ctx.lineTo(car.length / 2 - 4, car.width / 2 - 2)
        ctx.strokeStyle = 'rgba(255, 255, 255, 0.88)'
        ctx.lineWidth = 2
        ctx.lineCap = 'round'
        ctx.stroke()
    }
    ctx.restore()

    if (car.label) {
        ctx.font = '700 10px Arial, "Microsoft YaHei", sans-serif'
        ctx.textAlign = 'center'
        ctx.textBaseline = 'middle'
        ctx.lineJoin = 'round'
        ctx.lineWidth = 3
        ctx.strokeStyle = 'rgba(15, 23, 42, 0.72)'
        ctx.strokeText(car.label, x, y)
        ctx.fillStyle = '#ffffff'
        ctx.fillText(car.label, x, y)
    }
}

function draw(now: number) {
    frameId = null
    const canvas = canvasRef.value
    if (!canvas || !context || !viewport) return
    // The bitmap covers only the visible part of the scene, regardless of the
    // station's full scaled width. Scrolling moves this window over the scene.
    const width = Math.max(0, Math.min(viewportWidth, props.sceneWidth - scrollLeft))
    const height = Math.max(0, Math.min(viewportHeight, props.sceneHeight - scrollTop))
    const bitmapWidth = Math.ceil(width * pixelRatio)
    const bitmapHeight = Math.ceil(height * pixelRatio)
    if (canvas.width !== bitmapWidth) canvas.width = bitmapWidth
    if (canvas.height !== bitmapHeight) canvas.height = bitmapHeight
    canvas.style.width = `${width}px`
    canvas.style.height = `${height}px`
    canvas.style.transform = `translate(${scrollLeft}px, ${scrollTop}px)`
    context.setTransform(1, 0, 0, 1, 0, 0)
    context.clearRect(0, 0, bitmapWidth, bitmapHeight)
    context.setTransform(pixelRatio, 0, 0, pixelRatio, 0, 0)

    const progress = animationProgress(now)
    if (width > 0 && height > 0) {
        // All beams sit beneath all carriages and their labels. Only index zero
        // carries a train label and leads the route in the positive local X axis.
        for (const car of targetCars) {
            if (car.label) drawHeadlight(context, poseAt(car, progress), width, height)
        }
        for (const car of targetCars) drawCar(context, poseAt(car, progress), width, height)
    }
    if (progress < 1 && props.animate) {
        requestDraw()
    } else {
        transitioning = false
        sourceCars.clear()
    }
}

watch(
    [() => props.cars, () => props.projection, () => props.animate, () => props.resetRevision],
    ([cars, projection, animate, resetRevision], previous) => {
        const projectionChanged = projection !== previous?.[1]
        const playheadReset = resetRevision !== previous?.[3]
        updateCars(cars, !animate || projectionChanged || playheadReset)
        if (projectionChanged) measureViewport()
    },
    { immediate: true, flush: 'post' },
)
watch(() => props.viewportEl, element => { if (mounted) attachViewport(element) }, { flush: 'post' })
watch([() => props.sceneWidth, () => props.sceneHeight], measureViewport, { flush: 'post' })

onMounted(() => {
    mounted = true
    context = canvasRef.value?.getContext('2d') || null
    headlightTexture = createHeadlightTexture()
    resizeObserver = new ResizeObserver(measureViewport)
    attachViewport(props.viewportEl)
    window.addEventListener('resize', measureViewport)
    requestDraw()
})

onBeforeUnmount(() => {
    mounted = false
    if (frameId !== null) cancelAnimationFrame(frameId)
    frameId = null
    viewport?.removeEventListener('scroll', handleScroll)
    window.removeEventListener('resize', measureViewport)
    resizeObserver?.disconnect()
    resizeObserver = null
    viewport = null
    context = null
    if (headlightTexture) {
        headlightTexture.width = 0
        headlightTexture.height = 0
        headlightTexture = null
    }
    sourceCars.clear()
    targetCars = []
})
</script>

<style scoped>
.simulation-train-overlay {
    position: absolute;
    top: 0;
    left: 0;
    z-index: 3;
    display: block;
    pointer-events: none;
}
</style>
