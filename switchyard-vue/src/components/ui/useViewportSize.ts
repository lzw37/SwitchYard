import { onBeforeUnmount, onMounted, ref } from 'vue'

export function useViewportSize() {
    const width = ref(typeof window === 'undefined' ? 1280 : window.innerWidth)
    const height = ref(typeof window === 'undefined' ? 800 : window.innerHeight)
    const update = () => { width.value = window.innerWidth; height.value = window.innerHeight }
    onMounted(() => window.addEventListener('resize', update))
    onBeforeUnmount(() => window.removeEventListener('resize', update))
    return { width, height }
}
