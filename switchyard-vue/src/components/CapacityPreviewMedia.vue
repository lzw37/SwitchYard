<script setup lang="ts">
import { Picture, VideoCamera } from '@element-plus/icons-vue'
import { useI18n } from 'vue-i18n'

withDefaults(defineProps<{
    kind: 'image' | 'video'
    title: string
    caption: string
    src?: string
    poster?: string
}>(), { src: '', poster: '' })

const { t } = useI18n({ useScope: 'global' })
</script>

<template>
    <figure class="preview-media">
        <div class="media-frame" :class="{ 'is-placeholder': !src }">
            <video v-if="src && kind === 'video'" :src="src" :poster="poster || undefined"
                :aria-label="title" controls playsinline preload="metadata" />
            <img v-else-if="src" :src="src" :alt="title" loading="lazy" decoding="async" />
            <template v-else>
                <span class="media-type">{{ t(`capacityPreview.media.${kind}`) }}</span>
                <div class="placeholder-content">
                    <span class="placeholder-icon" aria-hidden="true">
                        <VideoCamera v-if="kind === 'video'" />
                        <Picture v-else />
                    </span>
                    <p class="placeholder-title">{{ title }}</p>
                    <p class="placeholder-note">{{ t('capacityPreview.media.pending') }}</p>
                </div>
                <span class="media-ratio">{{ t('capacityPreview.media.ratio') }}</span>
            </template>
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
.is-placeholder {
    display: grid;
    place-items: center;
    border: 1px dashed #a9c3e4;
    background-image: linear-gradient(#dce9f6 1px, transparent 1px), linear-gradient(90deg, #dce9f6 1px, transparent 1px);
    background-size: 28px 28px;
}
.placeholder-content { position: relative; padding: 44px 18px; text-align: center; }
.placeholder-icon {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    width: 56px;
    height: 56px;
    margin-bottom: 12px;
    border: 1px solid #c8dcf3;
    border-radius: 16px;
    background: #fff;
}
.placeholder-icon svg { width: 28px; height: 28px; }
.placeholder-title { font-size: 16px; font-weight: 600; }
.placeholder-note { margin-top: 5px; color: #64748b; font-size: 13px; }
.media-type, .media-ratio { position: absolute; font-size: 11px; letter-spacing: 0.03em; }
.media-type { top: 12px; left: 12px; padding: 3px 9px; border-radius: 5px; background: #fff; }
.media-ratio { right: 12px; bottom: 10px; color: #64748b; }
img, video { display: block; width: 100%; height: 100%; object-fit: contain; background: #eef4fb; }
figcaption { padding: 12px 6px 2px; color: #64748b; font-size: 12px; line-height: 1.6; }
@media (max-width: 480px) {
    .preview-media { padding: 8px; border-radius: 14px; }
    .placeholder-icon { width: 40px; height: 40px; margin-bottom: 6px; border-radius: 12px; }
    .placeholder-icon svg { width: 22px; height: 22px; }
    .placeholder-title { font-size: 14px; }
    .placeholder-note { font-size: 11px; }
    .media-type { top: 8px; left: 8px; }
}
</style>
