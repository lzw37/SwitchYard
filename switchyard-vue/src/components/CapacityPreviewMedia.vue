<script setup lang="ts">
import { ref, watch } from 'vue'

const props = withDefaults(defineProps<{
    kind: 'image' | 'video'
    title: string
    caption: string
    src?: string
    poster?: string
}>(), { src: '', poster: '' })

const failed = ref(false)
watch(() => props.src, () => { failed.value = false })
</script>

<template>
    <figure v-if="src && !failed" class="preview-media">
        <div class="media-frame">
            <video v-if="kind === 'video'" :src="src" :poster="poster || undefined"
                :aria-label="title" controls playsinline preload="metadata" @error="failed = true" />
            <img v-else :src="src" :alt="title" loading="lazy" decoding="async" @error="failed = true" />
        </div>
        <figcaption>{{ caption }}</figcaption>
    </figure>
</template>

<style scoped>
.preview-media {
    width: 100%;
    min-width: 0;
    margin: 0;
    padding: 12px;
    color: #1e3a8a;
    background: #fff;
    border: 1px solid #dce7f5;
    border-radius: 18px;
    box-shadow: 0 16px 42px rgba(30, 58, 138, 0.08);
}
.media-frame {
    position: relative;
    aspect-ratio: 16 / 9;
    overflow: hidden;
    border-radius: 10px;
    background: #f0f9ff;
}
img, video { display: block; width: 100%; height: 100%; object-fit: contain; background: #eef4fb; }
figcaption { padding: 12px 6px 2px; color: #64748b; font-size: 12px; line-height: 1.6; }
@media (max-width: 480px) {
    .preview-media { padding: 8px; border-radius: 14px; }
}
</style>
