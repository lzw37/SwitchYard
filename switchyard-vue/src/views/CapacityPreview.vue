<script setup lang="ts">
import { RouterLink } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { ArrowRight, Check, Connection, DataAnalysis, Film, MapLocation, Operation, SetUp } from '@element-plus/icons-vue'
import CapacityPreviewMedia from '@/components/CapacityPreviewMedia.vue'
import overviewImage from '@/assets/capacity_fig/2.png'
import stationLayoutImage from '@/assets/capacity_fig/Station_layout_and_route_design.png'
import operationProcessImage from '@/assets/capacity_fig/Operation_process_editor.png'
import operationPlanImage from '@/assets/capacity_fig/Train_operation_plan.png'
import sectionOccupancyImage from '@/assets/capacity_fig/Operation_chart_and_section_occupancy.png'
import capacitySummaryImage from '@/assets/capacity_fig/Bottleneck_analysis_and_capacity summaries.png'
import playback2DImage from '@/assets/capacity_fig/2D_operation_playback.gif'
import playback3DImage from '@/assets/capacity_fig/3D_operation_playback.gif'

const { t, locale } = useI18n({ useScope: 'global' })
const features = [
    { key: 'layout', icon: MapLocation },
    { key: 'routes', icon: Connection },
    { key: 'planning', icon: Operation },
    { key: 'solving', icon: SetUp },
    { key: 'analysis', icon: DataAnalysis },
    { key: 'playback', icon: Film },
]

const overviewMedia = { src: overviewImage }
const showcases = [
    { key: 'layout', images: [{ key: 'station', src: stationLayoutImage }] },
    { key: 'planning', images: [{ key: 'process', src: operationProcessImage }, { key: 'plan', src: operationPlanImage }] },
    { key: 'analysis', images: [{ key: 'occupancy', src: sectionOccupancyImage }, { key: 'capacity', src: capacitySummaryImage }] },
    { key: 'playback', images: [{ key: 'twoDimensional', src: playback2DImage }, { key: 'threeDimensional', src: playback3DImage }] },
] as const

function toggleLocale() {
    locale.value = locale.value === 'zh' ? 'en' : 'zh'
    localStorage.setItem('locale', locale.value)
}

</script>

<template>
    <div class="capacity-preview">
        <a class="skip-link" href="#capacity-content">{{ t('capacityPreview.nav.features') }}</a>
        <header class="preview-header">
            <nav class="container header-content" :aria-label="t('capacityPreview.nav.label')">
                <RouterLink to="/" class="brand" :aria-label="`SwitchYard · ${t('capacityPreview.nav.home')}`">
                    <span class="brand-icon" aria-hidden="true">🚂</span>
                    <span>SwitchYard</span>
                </RouterLink>
                <div class="header-actions">
                    <div class="section-links">
                        <a href="#capacity-features">{{ t('capacityPreview.nav.features') }}</a>
                        <a href="#capacity-workflow">{{ t('capacityPreview.nav.workflow') }}</a>
                        <a href="#capacity-showcase">{{ t('capacityPreview.nav.showcase') }}</a>
                        <a href="#capacity-roadmap">{{ t('capacityPreview.nav.roadmap') }}</a>
                    </div>
                    <RouterLink to="/" class="home-link">{{ t('capacityPreview.nav.home') }}</RouterLink>
                    <button type="button" class="lang-toggle" @click="toggleLocale"
                        :aria-label="locale === 'zh' ? t('home.lang.switchToEn') : t('home.lang.switchToZh')">
                        {{ locale === 'zh' ? 'EN' : '中文' }}
                    </button>
                </div>
            </nav>
        </header>

        <main id="capacity-content">
            <section class="preview-hero" aria-labelledby="capacity-title">
                <div class="container hero-grid">
                    <div class="hero-copy">
                        <p class="eyebrow">{{ t('capacityPreview.hero.eyebrow') }}</p>
                        <p class="product-label">{{ t('capacityPreview.hero.label') }}</p>
                        <h1 id="capacity-title">{{ t('capacityPreview.hero.title') }}<span>{{ t('capacityPreview.hero.accent') }}</span></h1>
                        <p class="hero-description">{{ t('capacityPreview.hero.description') }}</p>
                        <a href="#capacity-roadmap" class="release-note">{{ t('capacityPreview.roadmap.preview') }}</a>
                        <div class="hero-actions">
                            <a href="#capacity-features" class="button button-outline">{{ t('capacityPreview.hero.secondary') }}</a>
                        </div>
                        <ul class="hero-tags">
                            <li v-for="index in 3" :key="index"><Check aria-hidden="true" />{{ t(`capacityPreview.hero.tag${index}`) }}</li>
                        </ul>
                    </div>
                    <CapacityPreviewMedia class="hero-media" kind="image" v-bind="overviewMedia"
                        :title="t('capacityPreview.hero.mediaTitle')" :caption="t('capacityPreview.hero.mediaCaption')" />
                </div>
            </section>

            <section id="capacity-features" class="section section-tinted" aria-labelledby="features-title">
                <div class="container">
                    <div class="section-heading">
                        <p class="eyebrow">{{ t('capacityPreview.features.eyebrow') }}</p>
                        <h2 id="features-title">{{ t('capacityPreview.features.title') }}</h2>
                        <p>{{ t('capacityPreview.features.description') }}</p>
                    </div>
                    <div class="features-grid">
                        <article v-for="feature in features" :key="feature.key" class="feature-card">
                            <span class="feature-icon"><component :is="feature.icon" aria-hidden="true" /></span>
                            <h3>{{ t(`capacityPreview.features.${feature.key}.title`) }}</h3>
                            <p>{{ t(`capacityPreview.features.${feature.key}.description`) }}</p>
                        </article>
                    </div>
                </div>
            </section>

            <section id="capacity-workflow" class="section" aria-labelledby="workflow-title">
                <div class="container">
                    <div class="section-heading">
                        <p class="eyebrow">{{ t('capacityPreview.workflow.eyebrow') }}</p>
                        <h2 id="workflow-title">{{ t('capacityPreview.workflow.title') }}</h2>
                        <p>{{ t('capacityPreview.workflow.description') }}</p>
                    </div>
                    <ol class="workflow-grid">
                        <li v-for="index in 4" :key="index" class="workflow-step">
                            <span class="step-number" aria-hidden="true">0{{ index }}</span>
                            <h3>{{ t(`capacityPreview.workflow.steps.${index - 1}.title`) }}</h3>
                            <p>{{ t(`capacityPreview.workflow.steps.${index - 1}.description`) }}</p>
                        </li>
                    </ol>
                </div>
            </section>

            <section id="capacity-showcase" class="section section-tinted" aria-labelledby="showcase-title">
                <div class="container">
                    <div class="section-heading">
                        <p class="eyebrow">{{ t('capacityPreview.showcase.eyebrow') }}</p>
                        <h2 id="showcase-title">{{ t('capacityPreview.showcase.title') }}</h2>
                        <p>{{ t('capacityPreview.showcase.description') }}</p>
                    </div>
                    <article v-for="item in showcases" :key="item.key" :id="`capacity-${item.key}`" class="showcase-row"
                        :class="{ 'has-image-pair': item.images.length > 1 }">
                        <div class="showcase-copy">
                            <p class="eyebrow">{{ t(`capacityPreview.showcase.${item.key}.label`) }}</p>
                            <h3>{{ t(`capacityPreview.showcase.${item.key}.title`) }}</h3>
                            <p>{{ t(`capacityPreview.showcase.${item.key}.description`) }}</p>
                            <ul class="showcase-points">
                                <li v-for="point in 3" :key="point"><Check aria-hidden="true" />
                                    <span>{{ t(`capacityPreview.showcase.${item.key}.points.${point - 1}`) }}</span>
                                </li>
                            </ul>
                        </div>
                        <div class="showcase-images">
                            <CapacityPreviewMedia v-for="image in item.images" :key="image.key" kind="image" :src="image.src"
                                :title="t(`capacityPreview.showcase.${item.key}.images.${image.key}.title`)"
                                :caption="t(`capacityPreview.showcase.${item.key}.images.${image.key}.caption`)" />
                        </div>
                    </article>
                </div>
            </section>

            <section id="capacity-roadmap" class="section" aria-labelledby="roadmap-title">
                <div class="container">
                    <div class="section-heading">
                        <p class="eyebrow">{{ t('capacityPreview.roadmap.eyebrow') }}</p>
                        <h2 id="roadmap-title">{{ t('capacityPreview.roadmap.title') }}</h2>
                        <p>{{ t('capacityPreview.roadmap.description') }}</p>
                    </div>
                    <p class="roadmap-release">{{ t('capacityPreview.roadmap.preview') }}</p>
                    <ol class="roadmap-grid">
                        <li v-for="index in 3" :key="index" class="roadmap-phase">
                            <p class="phase-label">
                                <span class="phase-number" aria-hidden="true">0{{ index }}</span>
                                {{ t(`capacityPreview.roadmap.phases.${index - 1}.label`) }}
                            </p>
                            <h3>{{ t(`capacityPreview.roadmap.phases.${index - 1}.title`) }}</h3>
                            <p class="phase-description">{{ t(`capacityPreview.roadmap.phases.${index - 1}.description`) }}</p>
                            <ul class="phase-points">
                                <li v-for="point in 3" :key="point">{{ t(`capacityPreview.roadmap.phases.${index - 1}.points.${point - 1}`) }}</li>
                            </ul>
                        </li>
                    </ol>
                    <article class="roadmap-outlook">
                        <div>
                            <p class="eyebrow">{{ t('capacityPreview.roadmap.outlook.label') }}</p>
                            <h3>{{ t('capacityPreview.roadmap.outlook.title') }}</h3>
                        </div>
                        <p>{{ t('capacityPreview.roadmap.outlook.description') }}</p>
                    </article>
                </div>
            </section>

            <section class="section closing-section" aria-labelledby="start-title">
                <div class="container closing-content">
                    <p class="eyebrow">{{ t('capacityPreview.cta.eyebrow') }}</p>
                    <h2 id="start-title">{{ t('capacityPreview.cta.title') }}</h2>
                    <p class="closing-description">{{ t('capacityPreview.cta.description') }}</p>
                    <p class="usage-note">{{ t('capacityPreview.cta.note') }}</p>
                </div>
            </section>
        </main>

        <footer class="preview-footer">
            <div class="container footer-content">
                <p>{{ t('capacityPreview.footer') }}</p>
                <RouterLink to="/">{{ t('capacityPreview.nav.home') }}<ArrowRight aria-hidden="true" /></RouterLink>
            </div>
        </footer>
    </div>
</template>

<style scoped>
.capacity-preview {
    --preview-blue: #1e3a8a;
    --preview-muted: #64748b;
    color: #1e293b;
    background: #fff;
    font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', 'PingFang SC', 'Hiragino Sans GB', 'Microsoft YaHei', sans-serif;
    line-height: 1.7;
}
.capacity-preview a { text-decoration: none; }
.capacity-preview a:focus-visible, .capacity-preview button:focus-visible {
    outline: 3px solid #60a5fa;
    outline-offset: 5px;
}
.container { max-width: 1200px; margin: 0 auto; padding: 0 24px; }
.preview-header {
    position: sticky;
    top: 0;
    z-index: 100;
    color: #fff;
    background: rgba(30, 58, 138, 0.97);
    backdrop-filter: blur(10px);
    box-shadow: 0 2px 10px rgba(0, 0, 0, 0.1);
}
.header-content, .header-actions, .section-links { display: flex; align-items: center; }
.header-content { min-height: 80px; justify-content: space-between; gap: 24px; }
.brand { display: inline-flex; align-items: center; gap: 10px; font-size: 24px; font-weight: 700; }
.brand span { font-weight: inherit; }
.brand-icon { font-size: 32px; }
.preview-header a { color: #fff; }
.header-actions { gap: 26px; font-size: 14px; }
.section-links { gap: 26px; }
.preview-header a:hover { color: #bfdbfe; }
.home-link { white-space: nowrap; }
.lang-toggle {
    min-width: 42px;
    min-height: 36px;
    padding: 4px 10px;
    color: #fff;
    background: rgba(255, 255, 255, 0.12);
    border: 1px solid rgba(255, 255, 255, 0.24);
    border-radius: 8px;
    font: inherit;
    font-weight: 600;
    cursor: pointer;
}
.lang-toggle:hover { background: rgba(255, 255, 255, 0.22); }
.skip-link { position: fixed; top: -100px; left: 16px; z-index: 101; padding: 12px; background: #fff; color: #1e3a8a; }
.skip-link:focus { top: 12px; }
.preview-hero {
    position: relative;
    padding: 90px 0 94px;
    color: #fff;
    background: radial-gradient(ellipse at 80% 100%, rgba(96, 165, 250, 0.3), transparent 60%), linear-gradient(135deg, #1e3a8a, #3b82f6);
}
.hero-grid { display: grid; grid-template-columns: 1.08fr 1fr; align-items: center; gap: 48px; }
.hero-copy { min-width: 0; }
.eyebrow { color: #2563eb; font-size: 12px; font-weight: 700; letter-spacing: 0.15em; }
.hero-copy .eyebrow, .closing-content .eyebrow { color: #bfdbfe; }
.product-label { margin-top: 24px; font-size: 17px; color: #dbeafe; }
h1 { margin: 12px 0 22px; font-size: clamp(34px, 3.6vw, 50px); font-weight: 700; line-height: 1.3; letter-spacing: -0.035em; }
h1 span { display: block; margin-top: 4px; color: #bfdbfe; font-weight: inherit; }
.hero-description { max-width: 570px; color: #e2ecff; font-size: 16px; line-height: 1.9; }
.release-note { display: inline-block; margin-top: 18px; padding-bottom: 2px; border-bottom: 1px solid #93bbff; color: #dbeafe; font-size: 13px; }
.release-note:hover { color: #fff; }
.hero-actions { display: flex; flex-wrap: wrap; align-items: center; gap: 12px; margin: 30px 0 24px; }
.button { display: inline-flex; justify-content: center; align-items: center; gap: 10px; min-height: 48px; padding: 11px 22px; border: 1px solid transparent; border-radius: 50px; font-size: 14px; font-weight: 600; transition: transform 0.2s, box-shadow 0.2s; }
.button-outline { color: #fff; border-color: #93bbff; }
.button-outline:hover { color: #fff; background: rgba(255, 255, 255, 0.1); transform: translateY(-2px); }
.hero-tags { display: flex; flex-wrap: wrap; gap: 16px; padding: 0; list-style: none; color: #dbeafe; font-size: 12px; }
.hero-tags li { display: inline-flex; align-items: center; gap: 5px; }
.hero-tags svg { width: 14px; height: 14px; }
.hero-media { transform: rotate(-1deg); box-shadow: 0 24px 70px rgba(11, 29, 78, 0.23); }
.section { padding: 80px 0; }
.section-tinted { background: #f0f9ff; }
section[id], main[id], .showcase-row[id] { scroll-margin-top: 100px; }
.section-heading { max-width: 760px; margin: 0 auto 42px; text-align: center; }
h2 { color: var(--preview-blue); font-size: clamp(25px, 2.7vw, 36px); font-weight: 700; line-height: 1.45; }
.section-heading h2 { margin: 10px 0 14px; }
.section-heading > p:last-child { color: var(--preview-muted); font-size: 15px; }
.features-grid { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 22px; }
.feature-card { padding: 28px; background: #fff; border: 1px solid #e0eaf6; border-radius: 15px; box-shadow: 0 2px 10px rgba(0, 0, 0, 0.025); }
.feature-icon { display: inline-flex; align-items: center; justify-content: center; width: 46px; height: 46px; margin-bottom: 18px; color: #2563eb; background: #eff6ff; border-radius: 12px; }
.feature-icon svg { width: 25px; height: 25px; }
.feature-card h3, .workflow-step h3 { margin-bottom: 10px; color: var(--preview-blue); font-size: 19px; font-weight: 600; }
.feature-card p, .workflow-step p { color: var(--preview-muted); font-size: 14px; }
.workflow-grid { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 30px; padding: 0; list-style: none; }
.workflow-step { position: relative; border-top: 2px solid #dbeafe; padding-top: 24px; }
.step-number { display: inline-block; margin-bottom: 14px; color: #3b82f6; font-size: 30px; font-weight: 700; line-height: 1.2; }
.showcase-row { display: grid; grid-template-columns: 0.9fr 1.1fr; align-items: center; gap: 64px; margin-top: 64px; }
.showcase-row + .showcase-row { margin-top: 88px; }
.showcase-images { display: grid; min-width: 0; gap: 24px; }
.showcase-row.has-image-pair { display: block; }
.has-image-pair .showcase-copy { max-width: 760px; margin-bottom: 28px; }
.has-image-pair .showcase-images { grid-template-columns: repeat(2, minmax(0, 1fr)); }
.showcase-copy h3 { margin: 12px 0 18px; color: var(--preview-blue); font-size: 28px; font-weight: 700; line-height: 1.5; }
.showcase-copy > p:not(.eyebrow) { color: var(--preview-muted); font-size: 15px; }
.showcase-points { display: grid; gap: 12px; margin-top: 22px; padding: 0; list-style: none; }
.showcase-points li { display: flex; align-items: flex-start; gap: 10px; font-size: 14px; }
.showcase-points svg { flex: 0 0 18px; width: 18px; height: 18px; margin-top: 3px; color: #2563eb; }
.roadmap-release { margin-bottom: 28px; padding: 14px 20px; border: 1px solid #c8dcf3; border-radius: 10px; color: var(--preview-blue); background: #eff6ff; font-size: 15px; text-align: center; }
.roadmap-grid { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 24px; padding: 0; list-style: none; }
.roadmap-phase { padding: 28px; border: 1px solid #dce7f5; border-top: 3px solid #3b82f6; border-radius: 15px; background: #fff; }
.phase-label { display: flex; align-items: center; gap: 12px; margin-bottom: 18px; color: #2563eb; font-size: 14px; font-weight: 600; }
.phase-number { color: #3b82f6; font-size: 30px; font-weight: 700; line-height: 1.2; }
.roadmap-phase h3, .roadmap-outlook h3 { color: var(--preview-blue); font-size: 20px; font-weight: 600; line-height: 1.5; }
.phase-description { margin-top: 14px; color: var(--preview-muted); font-size: 14px; }
.phase-points { display: grid; gap: 8px; margin-top: 20px; padding-left: 18px; font-size: 14px; }
.phase-points li::marker { color: #3b82f6; }
.roadmap-outlook { display: grid; grid-template-columns: 1fr 1.5fr; align-items: center; gap: 32px; margin-top: 28px; padding: 28px; border-radius: 15px; background: #f0f9ff; }
.roadmap-outlook h3 { margin-top: 8px; }
.roadmap-outlook > p { color: var(--preview-muted); font-size: 15px; }
.closing-section { color: #fff; background: linear-gradient(135deg, #1e3a8a, #2563eb); text-align: center; }
.closing-content { max-width: 850px; }
.closing-content h2 { margin: 12px 0 18px; color: #fff; }
.closing-description { color: #dbeafe; }
.usage-note { max-width: 680px; margin: 24px auto 0; font-size: 12px; color: #dbeafe; }
.preview-footer { padding: 26px 0; color: #cbd5e1; background: #152b65; font-size: 12px; }
.footer-content { display: flex; justify-content: space-between; align-items: center; gap: 18px; }
.preview-footer a { display: inline-flex; align-items: center; gap: 8px; color: #fff; white-space: nowrap; }
.preview-footer a:hover { color: #bfdbfe; }
.preview-footer svg { width: 15px; height: 15px; }
@media (max-width: 1024px) {
    .header-actions, .section-links { gap: 18px; }
    .hero-grid { gap: 28px; }
    .preview-hero { padding: 70px 0; }
    .showcase-row { gap: 36px; }
    .features-grid { gap: 16px; }
    .feature-card { padding: 24px; }
}
@media (max-width: 800px) {
    .section-links { display: none; }
    .hero-grid { grid-template-columns: 1fr; gap: 38px; }
    .hero-media { max-width: 600px; transform: none; }
    .hero-description { max-width: none; }
    h1 { font-size: clamp(34px, 6vw, 48px); }
    .features-grid, .workflow-grid { grid-template-columns: repeat(2, minmax(0, 1fr)); }
    .showcase-row { grid-template-columns: 1fr; gap: 26px; }
    .has-image-pair .showcase-images { grid-template-columns: 1fr; }
    .roadmap-grid, .roadmap-outlook { grid-template-columns: 1fr; }
    .roadmap-outlook { gap: 16px; }
    .section { padding: 60px 0; }
    .showcase-row + .showcase-row { margin-top: 60px; }
}
@media (max-width: 480px) {
    .container { padding: 0 20px; }
    .header-content { min-height: 68px; gap: 10px; }
    .brand { gap: 6px; font-size: 19px; }
    .brand-icon { font-size: 26px; }
    .header-actions { gap: 12px; font-size: 12px; }
    .lang-toggle { min-width: 36px; padding: 4px 7px; }
    .preview-hero { padding: 46px 0; }
    .hero-actions { align-items: stretch; flex-direction: column; }
    .hero-tags { gap: 12px; font-size: 11px; }
    .hero-description { font-size: 15px; }
    .features-grid { grid-template-columns: 1fr; }
    .workflow-grid { gap: 24px 18px; }
    .workflow-step h3 { font-size: 17px; }
    .showcase-copy h3 { font-size: 25px; }
    .footer-content { align-items: flex-start; flex-direction: column; }
}
@media (prefers-reduced-motion: reduce) {
    .capacity-preview *, .capacity-preview a { transition: none; }
    .button:hover { transform: none; }
}
</style>
