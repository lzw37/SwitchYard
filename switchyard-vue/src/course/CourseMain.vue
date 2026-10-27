<template>
    <div class="course-shell">
        <header class="course-header"><h1>{{ t('course.title') }}</h1></header>

        <div ref="layoutElement" class="layout" :class="{ 'is-compact': isCompact }">
            <aside class="sidebar" :style="isCompact ? { height: sidebarHeight + 'px' } : { width: sidebarWidth + 'px' }">
                <div class="sidebar-header">
                    <h2>{{ t('course.directory') }}</h2>
                </div>

                <div class="tree">
                    <div v-for="part in courseTree" :key="part.id">
                        <button class="tree-btn level-1" :class="{ active: isPartSelected(part.id) }"
                            @click="onPartClick(part)">
                            <span>{{ isPartExpanded(part.id) ? "▾" : "▸" }}</span>
                            <span>{{ part.displayName }}</span>
                        </button>

                        <div v-if="isPartExpanded(part.id)" class="children">
                            <div v-if="part.chapters.length === 0" class="leaf-empty">{{ t('course.emptyChapters') }}</div>

                            <div v-for="chapter in part.chapters" :key="chapter.id">
                                <button class="tree-btn level-2"
                                    :class="{ active: isChapterSelected(part.id, chapter.id) }"
                                    @click="onChapterClick(part, chapter)">
                                    <span>{{ isChapterExpanded(chapter.id) ? "▾" : "▸" }}</span>
                                    <span>{{ chapter.displayName }}</span>
                                </button>

                                <div v-if="isChapterExpanded(chapter.id)" class="children">
                                    <div v-if="chapter.sections.length === 0" class="leaf-empty">{{ t('course.emptySections') }}</div>

                                    <button v-for="section in chapter.sections" :key="section.id"
                                        class="tree-btn level-3"
                                        :class="{ active: isSectionSelected(part.id, chapter.id, section.id) }"
                                        @click="onSectionClick(part, chapter, section)">
                                        <span>{{ section.displayName }}</span>
                                    </button>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            </aside>
            <PaneDivider v-model="sidebarSize" :direction="isCompact ? 'vertical' : 'horizontal'" :min="isCompact ? 100 : 180" :max="sidebarMax" />

            <main class="main">
                <section v-if="selected.level === 'none'" class="empty">
                    <p>{{ t('course.selectContent') }}</p>
                </section>

                <section v-else-if="selected.level === 'part' || selected.level === 'chapter'" class="focus-only">
                    <header class="focus-header">
                        <div class="focus-title-wrap">
                            <h2>{{ focusTitle }}</h2>
                        </div>
                    </header>
                    <div v-if="focusLoading" class="muted">{{ t('course.loadingGuide') }}</div>
                    <div v-else class="focus-content">
                        <section class="focus-block">
                            <p class="eyebrow">{{ t('course.keyPoints') }}</p>
                            <div v-if="displayFocusPoints.length === 0" class="muted">{{ t('course.emptyKeyPoints') }}</div>
                            <ul v-else class="keypoints">
                                <li v-for="(point, idx) in displayFocusPoints" :key="`k-${idx}`">{{ idx + 1 }}. {{ point }}</li>
                            </ul>
                        </section>

                        <section class="focus-block">
                            <p class="eyebrow">{{ t('course.difficultPoints') }}</p>
                            <div v-if="displayFocusDifficultPoints.length === 0" class="muted">{{ t('course.emptyDifficultPoints') }}</div>
                            <ul v-else class="keypoints">
                                <li v-for="(point, idx) in displayFocusDifficultPoints" :key="`d-${idx}`">{{ idx + 1 }}. {{ point }}</li>
                            </ul>
                        </section>
                    </div>
                </section>

                <section v-else-if="selected.level === 'section' && selectedSection" class="section-details">
                    <header class="section-head">
                        <div>
                            <h2>{{ selectedSection.displayName }}</h2>
                        </div>
                    </header>

                    <section class="player-grid">
                        <div class="panel">
                            <div class="panel-head">
                                <h3 class="panel-title">{{ t('course.video') }}</h3>
                            </div>
                            <div v-if="selectedSection.videoFiles.length > 1" class="video-switch">
                                <button v-for="(video, idx) in selectedSection.videoFiles" :key="video.url"
                                    :class="['video-tab', { active: idx === activeVideoIndex }]"
                                    @click="activeVideoIndex = idx">
                                    {{ t('course.videoNumber', { index: idx + 1 }) }}
                                </button>
                            </div>
                            <video v-if="activeVideo?.url" class="video-player" :src="activeVideo.url" controls preload="metadata"></video>
                            <div v-else class="video-placeholder">{{ t('course.emptyVideo') }}</div>
                        </div>

                        <div class="panel">
                            <div class="panel-head">
                                <h3 class="panel-title">{{ t('course.slides') }}</h3>
                            </div>
                            <div v-if="selectedSection.pdfFiles.length === 0" class="muted">{{ t('course.emptyPdf') }}</div>
                            <template v-else>
                                <div v-if="selectedSection.pdfFiles.length > 1" class="pdf-file-switch">
                                    <button v-for="(pdf, idx) in selectedSection.pdfFiles" :key="pdf.url"
                                        :class="['pdf-file-tab', { active: idx === activePdfIndex }]"
                                        @click="setActivePdf(idx)">
                                        {{ pdf.name }}
                                    </button>
                                </div>
                                <div class="ppt-frame">
                                    <img v-if="activePdfPageImage" class="pdf-main-image" :src="activePdfPageImage"
                                        :alt="t('course.page', { page: activePdfPageIndex + 1 })" />
                                    <div v-else-if="pdfRendering" class="empty-frame">{{ t('course.renderingSlides') }}</div>
                                    <div v-else class="empty-frame">{{ pdfRenderError || t('course.emptyPreview') }}</div>
                                </div>

                                <div class="ppt-nav">
                                    <ActionButton :label="t('course.previousPage')" :icon="actionIcons.back" :disabled="!hasPrevPdfPage" @click="goPrevPdfPage" />
                                    <span class="ppt-nav-status">{{ pdfPageStatus }}</span>
                                    <ActionButton :label="t('course.nextPage')" :icon="actionIcons.forward" :disabled="!hasNextPdfPage" @click="goNextPdfPage" />
                                </div>

                                <div v-if="pdfNeighborSlots.length > 0" class="ppt-thumbs">
                                    <button v-for="slot in pdfNeighborSlots" :key="slot.key"
                                        :class="['ppt-thumb', { active: slot.pageIndex === activePdfPageIndex, empty: slot.pageIndex === null }]"
                                        :disabled="slot.pageIndex === null"
                                        @click="jumpToPdfPage(slot.pageIndex)">
                                        <img v-if="slot.image" class="ppt-thumb-preview" :src="slot.image" :alt="t('course.thumbnail', { page: slot.pageText })" />
                                        <div v-else class="ppt-thumb-placeholder"></div>
                                        <span v-if="slot.pageText" class="ppt-thumb-page">{{ slot.pageText }}</span>
                                    </button>
                                </div>
                            </template>
                        </div>
                    </section>

                    <CourseFillBlankQuiz
                        :quizDocUrl="selectedQuizDocUrl"
                        :cacheKey="selectedSectionId"
                    />
                </section>
            </main>
        </div>
    </div>
</template>

<script setup lang="ts">
import { computed, reactive, ref, watch, onMounted, onBeforeUnmount } from "vue";
import { getDocument, GlobalWorkerOptions } from "pdfjs-dist";
import pdfWorkerSrc from "pdfjs-dist/build/pdf.worker.min.mjs?url";
import CourseFillBlankQuiz from "@/course/CourseFillBlankQuiz.vue";
import { useI18n } from 'vue-i18n';
import ActionButton from '@/components/ui/ActionButton.vue';
import PaneDivider from '@/components/ui/PaneDivider.vue';
import { actionIcons } from '@/components/ui/actionIcons';
import config from "@/config";

const { t } = useI18n();
const layoutElement = ref<HTMLElement | null>(null);
const layoutWidth = ref(1000);
const layoutHeight = ref(700);
const sidebarWidth = ref(260);
const sidebarHeight = ref(200);
const isCompact = computed(() => layoutWidth.value < 700);
const sidebarSize = computed({ get: () => isCompact.value ? sidebarHeight.value : sidebarWidth.value, set: (value: number) => { if (isCompact.value) sidebarHeight.value = value; else sidebarWidth.value = value; } });
const sidebarMax = computed(() => isCompact.value ? Math.max(100, layoutHeight.value - 220) : Math.max(180, Math.min(520, layoutWidth.value - 320)));
let layoutObserver: ResizeObserver | undefined;
onBeforeUnmount(() => layoutObserver?.disconnect());

type AssetFile = { name: string; url: string };
type TeachingManifestItem = { name: string; path: string; url: string };
type VideoManifestItem = { name: string; path: string; url: string };
type SectionNode = {
    id: string;
    label: string;
    displayName: string;
    path: string;
    textDocUrl?: string;
    quizDocUrl?: string;
    pdfFiles: AssetFile[];
    videoFiles: AssetFile[];
    keyPoints: string[] | null;
};
type ChapterNode = {
    id: string;
    label: string;
    displayName: string;
    path: string;
    guideDocUrl?: string;
    keyPoints: string[] | null;
    difficultPoints: string[] | null;
    sections: SectionNode[];
};
type PartNode = {
    id: string;
    label: string;
    displayName: string;
    path: string;
    guideDocUrl?: string;
    keyPoints: string[] | null;
    difficultPoints: string[] | null;
    chapters: ChapterNode[];
};
type SelectionState =
    | { level: "none" }
    | { level: "part"; partId: string }
    | { level: "chapter"; partId: string; chapterId: string }
    | { level: "section"; partId: string; chapterId: string; sectionId: string };

const partDisplayOrder = ["绪论", "第一篇", "第二篇", "第三篇", "第四篇", "第五篇", "第六篇", "第七篇"];
const courseTree = ref<PartNode[]>([]);
const selected = ref<SelectionState>({ level: "none" });
const expandedParts = reactive<Record<string, boolean>>({});
const expandedChapters = reactive<Record<string, boolean>>({});
const focusPoints = ref<string[]>([]);
const focusDifficultPoints = ref<string[]>([]);
const focusTitle = ref("");
const focusLoading = ref(false);
const sectionLoading = ref(false);
const activeVideoIndex = ref(0);
const activePdfIndex = ref(0);
const activePdfPageIndex = ref(0);
const pdfPageImages = ref<string[]>([]);
const pdfRendering = ref(false);
const pdfRenderError = ref("");
const pdfRenderTicket = ref(0);
const docSegmentCache = new Map<string, Promise<string[]>>();
const pdfPageCache = new Map<string, Promise<string[]>>();

GlobalWorkerOptions.workerSrc = pdfWorkerSrc;

onMounted(() => {
    if (layoutElement.value) {
        layoutObserver = new ResizeObserver(([entry]) => {
            if (!entry) return;
            layoutWidth.value = entry.contentRect.width;
            layoutHeight.value = entry.contentRect.height;
        });
        layoutObserver.observe(layoutElement.value);
    }
    void initializeCourseData();
});

const selectedPart = computed(() => {
    const state = selected.value;
    if (state.level === "none") return null;
    return courseTree.value.find((part) => part.id === state.partId) ?? null;
});

const selectedChapter = computed(() => {
    const state = selected.value;
    if (state.level === "none" || state.level === "part") return null;
    return selectedPart.value?.chapters.find((chapter) => chapter.id === state.chapterId) ?? null;
});

const selectedSection = computed(() => {
    const state = selected.value;
    if (state.level !== "section") return null;
    return selectedChapter.value?.sections.find((section) => section.id === state.sectionId) ?? null;
});
const selectedPartName = computed(() => selectedPart.value?.displayName ?? "");
const selectedChapterName = computed(() => selectedChapter.value?.displayName ?? "");
const selectedQuizDocUrl = computed(() => selectedSection.value?.quizDocUrl ?? "");
const selectedSectionId = computed(() => selectedSection.value?.id ?? "");
const displayFocusPoints = computed(() =>
    focusPoints.value.map((point) => (typeof point === "string" ? point : String(point ?? "")))
);
const displayFocusDifficultPoints = computed(() =>
    focusDifficultPoints.value.map((point) => (typeof point === "string" ? point : String(point ?? "")))
);

const activePdf = computed(() => {
    if (!selectedSection.value) return null;
    return selectedSection.value.pdfFiles[activePdfIndex.value] ?? null;
});
const activeVideo = computed(() => {
    if (!selectedSection.value) return null;
    return selectedSection.value.videoFiles[activeVideoIndex.value] ?? null;
});
const activePdfPageImage = computed(() => pdfPageImages.value[activePdfPageIndex.value] ?? "");
const hasPrevPdfPage = computed(() => activePdfPageIndex.value > 0);
const hasNextPdfPage = computed(() => {
    const total = pdfPageImages.value.length;
    return total > 0 && activePdfPageIndex.value < total - 1;
});
const pdfPageStatus = computed(() => {
    if (pdfPageImages.value.length === 0) return "0/0";
    return `${activePdfPageIndex.value + 1}/${pdfPageImages.value.length}`;
});
const pdfNeighborSlots = computed(() => {
    if (pdfPageImages.value.length === 0) return [];
    const total = pdfPageImages.value.length;
    const current = activePdfPageIndex.value;

    let indexes: number[] = [];
    if (total === 1) {
        indexes = [0, 1, 2];
    } else if (current === 0) {
        indexes = [0, 1, 2];
    } else if (current === total - 1) {
        indexes = [total - 3, total - 2, total - 1];
    } else {
        indexes = [current - 1, current, current + 1];
    }
    return indexes.map((pageIndex, slotIndex) => {
        const valid = pageIndex >= 0 && pageIndex < total;
        return {
            key: `slot-${slotIndex}-${pageIndex}`,
            pageIndex: valid ? pageIndex : null,
            pageText: valid ? t('course.page', { page: pageIndex + 1 }) : "",
            image: valid ? pdfPageImages.value[pageIndex] ?? "" : ""
        };
    });
});

const sectionPoints = computed(() => selectedSection.value?.keyPoints ?? []);

watch(
    () => activePdf.value?.url ?? "",
    (url) => {
        void loadPdfPages(url);
    }
);

function normalizePath(value: string) {
    return value.replace(/\\/g, "/");
}
function toAbsoluteUrl(url: string) {
    if (/^https?:\/\//i.test(url)) return url;
    if (typeof window === "undefined") return url;
    return new URL(url, window.location.origin).href;
}
function toAbsoluteApiUrl(url: string) {
    if (!url) return url;
    if (/^https?:\/\//i.test(url)) return url;
    return new URL(url, config.serverurl).toString();
}
function normalizeTeachingRelativePath(path: string) {
    return normalizePath(path).replace(/^\/+/, "").replace(/^教学文档\//, "");
}
function toTeachingAssetMap(manifest: TeachingManifestItem[]) {
    const map: Record<string, string> = {};
    for (const item of manifest) {
        const relative = normalizeTeachingRelativePath(item.path);
        if (!relative) continue;
        map[`/教学文档/${relative}`] = toAbsoluteApiUrl(item.url);
    }
    return map;
}
async function fetchTeachingManifest() {
    try {
        const manifestUrl = new URL("/Course/GetTeachingManifest", config.serverurl);
        const response = await fetch(manifestUrl.toString());
        if (!response.ok) {
            console.warn("[CourseMain] 获取教学资源清单失败:", response.status, response.statusText);
            return [] as TeachingManifestItem[];
        }
        return (await response.json()) as TeachingManifestItem[];
    } catch (error) {
        console.warn("[CourseMain] 加载教学资源清单异常:", error);
        return [] as TeachingManifestItem[];
    }
}
async function fetchVideoManifest() {
    try {
        const manifestUrl = new URL("/Course/GetVideoManifest", config.serverurl);

        const response = await fetch(manifestUrl.toString());
        if (!response.ok) {
            console.warn("[CourseMain] 获取视频清单失败:", response.status, response.statusText);
            return [] as VideoManifestItem[];
        }

        return (await response.json()) as VideoManifestItem[];
    } catch (error) {
        console.warn("[CourseMain] 加载视频清单异常:", error);
        return [] as VideoManifestItem[];
    }
}
async function initializeCourseData() {
    const [teachingManifest, videoManifest] = await Promise.all([fetchTeachingManifest(), fetchVideoManifest()]);

    const nextTree = buildTreeFromAssets(toTeachingAssetMap(teachingManifest));
    attachVideosToTree(nextTree, videoManifest);
    sortCourseTree(nextTree);
    courseTree.value = nextTree;

    void hydratePartTitles();
}
async function loadPdfPages(url: string) {
    const ticket = ++pdfRenderTicket.value;
    activePdfPageIndex.value = 0;
    pdfRenderError.value = "";

    if (!url) {
        pdfPageImages.value = [];
        pdfRendering.value = false;
        return;
    }

    pdfRendering.value = true;
    try {
        const pages = await renderPdfToImages(url);
        if (ticket !== pdfRenderTicket.value) return;
        pdfPageImages.value = pages;
        if (pages.length === 0) pdfRenderError.value = t('course.emptyPdfPages');
    } catch (error) {
        if (ticket !== pdfRenderTicket.value) return;
        pdfPageImages.value = [];
        pdfRenderError.value = t('course.pdfRenderFailed', { error: error instanceof Error ? error.message : t('common.unknownError') });
    } finally {
        if (ticket === pdfRenderTicket.value) pdfRendering.value = false;
    }
}
async function renderPdfToImages(url: string) {
    if (!pdfPageCache.has(url)) {
        pdfPageCache.set(
            url,
            (async () => {
                const absoluteUrl = toAbsoluteUrl(url);
                const loadingTask = getDocument(absoluteUrl);
                const pdfDocument = await loadingTask.promise;
                const pages: string[] = [];

                for (let pageNumber = 1; pageNumber <= pdfDocument.numPages; pageNumber += 1) {
                    const page = await pdfDocument.getPage(pageNumber);
                    const viewport = page.getViewport({ scale: 1.25 });
                    const canvas = document.createElement("canvas");
                    const context = canvas.getContext("2d");
                    if (!context) continue;

                    canvas.width = Math.max(1, Math.floor(viewport.width));
                    canvas.height = Math.max(1, Math.floor(viewport.height));
                    await page.render({ canvas, canvasContext: context, viewport }).promise;
                    pages.push(canvas.toDataURL("image/jpeg", 0.88));
                }
                return pages;
            })()
        );
    }
    return (await pdfPageCache.get(url)) ?? [];
}

function buildTreeFromAssets(assetMap: Record<string, string>) {
    const partMap = new Map<string, PartNode>();
    for (const [rawPath, rawUrl] of Object.entries(assetMap)) {
        const fullPath = normalizePath(rawPath);
        const url = normalizePath(rawUrl);
        const relative = fullPath
            .replace(/^\/src\/assets\/教学文档\//, "")
            .replace(/^\/assets\/教学文档\//, "")
            .replace(/^\/教学文档\//, "");
        if (relative === fullPath) continue;

        const segments = relative.split("/");
        if (segments.length < 2) continue;

        const partName = segments[0];
        const chapterName = segments[1];
        const sectionName = segments[2];
        if (!partName) continue;
        const leafName = segments[segments.length - 1];
        if (!leafName || leafName.startsWith("~$")) continue;

        const part = ensurePart(partMap, partName);
        if (segments.length === 2) {
            if (isGuideDoc(leafName)) part.guideDocUrl = url;
            else if (isPdf(leafName)) {
                const chapter = ensureChapter(part, part.label);
                const section = ensureSection(chapter, stripFileExtension(leafName));
                section.path = `${part.path}/${chapter.label}/${section.label}`;
                addPdfFile(section, leafName, url);
            }
            continue;
        }
        if (segments.length === 3) {
            if (!chapterName) continue;
            const chapter = ensureChapter(part, chapterName);
            if (isGuideDoc(leafName)) chapter.guideDocUrl = url;
            else if (isPdf(leafName)) {
                const section = ensureSection(chapter, stripFileExtension(leafName));
                section.path = `${part.path}/${chapter.label}/${section.label}`;
                addPdfFile(section, leafName, url);
            }
            continue;
        }
        if (segments.length === 4) {
            if (!chapterName || !sectionName) continue;
            const chapter = ensureChapter(part, chapterName);
            const section = ensureSection(chapter, sectionName);
            section.path = `${part.path}/${chapter.label}/${section.label}`;
            if (isPdf(leafName)) addPdfFile(section, leafName, url);
            else if (isQuizDoc(leafName)) section.quizDocUrl = url;
            else if (isTextDoc(leafName)) section.textDocUrl = url;
        }
    }

    const parts = Array.from(partMap.values());
    sortCourseTree(parts);
    return parts;
}

function ensurePart(map: Map<string, PartNode>, label: string) {
    if (!map.has(label)) {
        map.set(label, { id: label, label, displayName: label, path: label, keyPoints: null, difficultPoints: null, chapters: [] });
    }
    return map.get(label)!;
}

function ensureChapter(part: PartNode, label: string) {
    let chapter = part.chapters.find((item) => item.label === label);
    if (!chapter) {
        chapter = { id: `${part.id}/${label}`, label, displayName: label, path: `${part.path}/${label}`, keyPoints: null, difficultPoints: null, sections: [] };
        part.chapters.push(chapter);
    }
    return chapter;
}

function ensureSection(chapter: ChapterNode, label: string) {
    let section = chapter.sections.find((item) => item.label === label);
    if (!section) {
        section = {
            id: `${chapter.id}/${label}`,
            label,
            displayName: label,
            path: `${chapter.path}/${label}`,
            pdfFiles: [],
            videoFiles: [],
            keyPoints: null
        };
        chapter.sections.push(section);
    }
    return section;
}
function stripFileExtension(filename: string) {
    return filename.replace(/\.[^/.]+$/, "");
}
function addPdfFile(section: SectionNode, name: string, url: string) {
    if (section.pdfFiles.some((item) => item.url === url)) return;
    section.pdfFiles.push({ name, url });
}
function addVideoFile(section: SectionNode, name: string, url: string) {
    if (section.videoFiles.some((item) => item.url === url)) return;
    section.videoFiles.push({ name, url });
}
function sortCourseTree(parts: PartNode[]) {
    parts.sort((a, b) => comparePart(a.label, b.label));
    parts.forEach((part) => {
        part.chapters.sort((a, b) => compareNumberedLabel(a.label, b.label, "章"));
        part.chapters.forEach((chapter) => {
            chapter.sections.sort((a, b) => compareNumberedLabel(a.label, b.label, "节"));
            chapter.sections.forEach((section) => {
                section.pdfFiles.sort((aFile, bFile) => aFile.name.localeCompare(bFile.name, "zh-Hans-CN"));
                section.videoFiles.sort((aFile, bFile) => compareVideoFileByOrder(aFile.name, bFile.name));
            });
        });
    });
}
function attachVideosToTree(parts: PartNode[], videoAssets: VideoManifestItem[]) {
    const partMap = new Map(parts.map((part) => [part.label, part] as const));

    for (const videoAsset of videoAssets) {
        const relative = normalizePath(videoAsset.path).replace(/^\/+/, "");
        if (!relative) continue;

        const url = toAbsoluteApiUrl(videoAsset.url);
        if (!url) continue;

        let segments = relative.split("/").filter(Boolean);
        if (segments.length < 2) continue;

        let partName = segments[0] ?? "";
        let part = partMap.get(partName);
        if (!part && segments.length >= 3) {
            segments = segments.slice(1);
            partName = segments[0] ?? "";
            part = partMap.get(partName);
        }
        if (!part && partName) {
            part = {
                id: partName,
                label: partName,
                displayName: partName,
                path: partName,
                guideDocUrl: undefined,
                keyPoints: null,
                difficultPoints: null,
                chapters: []
            };
            partMap.set(partName, part);
            parts.push(part);
        }
        if (!part) continue;

        const leafName = segments[segments.length - 1] ?? "";
        if (!leafName || !isVideoFile(leafName)) continue;

        if (partName === "绪论") {
            const chapter = part.chapters[0] ?? ensureChapter(part, part.label);
            const section = chapter.sections[0] ?? ensureSection(chapter, "绪论");
            addVideoFile(section, leafName, url);
            continue;
        }

        const chapterFolder = segments[1] ?? "";
        const parsed = parseVideoRefFromFilename(leafName);
        const chapterNoByFolder = parseChapterNumberFromLabel(chapterFolder);
        const chapterNo = chapterNoByFolder ?? parsed.chapterNo;
        const sectionNo = parsed.sectionNo;
        if (chapterNo === null || sectionNo === null) continue;

        const chapter =
            findChapterByOrder(part, chapterNo) ??
            part.chapters.find((item) => item.label === chapterFolder) ??
            ensureChapter(part, chapterFolder || `第${chapterNo}章`);

        const section = findSectionByOrder(chapter, sectionNo) ?? ensureSection(chapter, `第${sectionNo}节`);

        addVideoFile(section, leafName, url);
    }
}
function isVideoFile(filename: string) {
    return /\.(mp4|webm|m4v|mov|avi|mkv)$/i.test(filename);
}
function findChapterByOrder(part: PartNode, order: number) {
    return part.chapters.find((chapter) => extractOrder(chapter.label, "章") === order) ?? null;
}
function findSectionByOrder(chapter: ChapterNode, order: number) {
    return chapter.sections.find((section) => extractOrder(section.label, "节") === order) ?? null;
}
function parseChapterNumberFromLabel(label: string) {
    const match = label.match(/^第(.+?)章/);
    if (!match || !match[1]) return null;
    const parsed = parseNumberToken(match[1]);
    return parsed === null ? null : parsed;
}
function parseVideoRefFromFilename(filename: string) {
    const stem = stripFileExtension(filename).trim();
    const match = stem.match(/^([一二三四五六七八九十两零\d]+)\s*[.．。]\s*([一二三四五六七八九十两零\d]+)(?:\s*[.．。]\s*([一二三四五六七八九十两零\d]+))?/);
    const chapterNo = parseNumberToken(match?.[1] ?? "");
    const sectionNo = parseNumberToken(match?.[2] ?? "");
    const clipNo = parseNumberToken(match?.[3] ?? "") ?? 0;
    return { chapterNo, sectionNo, clipNo };
}
function parseNumberToken(token: string) {
    const value = token.trim();
    if (!value) return null;
    if (/^\d+$/.test(value)) return Number(value);
    const parsed = chineseNumberToInt(value);
    return Number.isFinite(parsed) && parsed !== Number.MAX_SAFE_INTEGER ? parsed : null;
}
function compareVideoFileByOrder(aName: string, bName: string) {
    const aRef = parseVideoRefFromFilename(aName);
    const bRef = parseVideoRefFromFilename(bName);
    if (aRef.chapterNo !== bRef.chapterNo) return (aRef.chapterNo ?? 0) - (bRef.chapterNo ?? 0);
    if (aRef.sectionNo !== bRef.sectionNo) return (aRef.sectionNo ?? 0) - (bRef.sectionNo ?? 0);
    if (aRef.clipNo !== bRef.clipNo) return aRef.clipNo - bRef.clipNo;
    return aName.localeCompare(bName, "zh-Hans-CN");
}

function isGuideDoc(filename: string) {
    return filename.includes("学习指导") && isDoc(filename);
}
function isQuizDoc(filename: string) {
    return filename.includes("习题测试") && isDoc(filename);
}
function isTextDoc(filename: string) {
    return filename.includes("文字教材") && isDoc(filename);
}
function isDoc(filename: string) {
    return /\.docx?$/i.test(filename);
}
function isPdf(filename: string) {
    return /\.pdf$/i.test(filename);
}
function comparePart(a: string, b: string) {
    const aIndex = partDisplayOrder.indexOf(a);
    const bIndex = partDisplayOrder.indexOf(b);
    if (aIndex !== -1 && bIndex !== -1) return aIndex - bIndex;
    if (aIndex !== -1) return -1;
    if (bIndex !== -1) return 1;
    return a.localeCompare(b, "zh-Hans-CN");
}
function compareNumberedLabel(a: string, b: string, unit: "章" | "节") {
    return extractOrder(a, unit) - extractOrder(b, unit);
}
function extractOrder(label: string, unit: "章" | "节") {
    const match = label.match(new RegExp(`^第(.+?)${unit}`));
    if (!match) return Number.MAX_SAFE_INTEGER;
    const value = match[1];
    if (!value) return Number.MAX_SAFE_INTEGER;
    return chineseNumberToInt(value);
}
function chineseNumberToInt(value: string) {
    if (/^\d+$/.test(value)) return Number(value);
    const map: Record<string, number> = { 零: 0, 一: 1, 二: 2, 两: 2, 三: 3, 四: 4, 五: 5, 六: 6, 七: 7, 八: 8, 九: 9, 十: 10 };
    if (value === "十") return 10;
    if (value.includes("十")) {
        const [ten, one] = value.split("十");
        const tenValue = ten ? map[ten] ?? 0 : 1;
        const oneValue = one ? map[one] ?? 0 : 0;
        return tenValue * 10 + oneValue;
    }
    return map[value] ?? Number.MAX_SAFE_INTEGER;
}

async function readDocSegments(url: string) {
    if (!docSegmentCache.has(url)) {
        docSegmentCache.set(
            url,
            (async () => {
                try {
                    const response = await fetch(url);
                    if (!response.ok) return [];

                    const buffer = await response.arrayBuffer();
                    const text = extractTextFromDocBuffer(buffer);
                    return extractTeachingSegments(text);
                } catch (error) {
                    console.warn("[CourseMain] 读取 doc 失败:", url, error);
                    return [];
                }
            })()
        );
    }
    return (await docSegmentCache.get(url)) ?? [];
}
function extractTextFromDocBuffer(buffer: ArrayBuffer) {
    const bytes = new Uint8Array(buffer);
    const utf16Snippets = [...extractUtf16Runs(bytes, 0), ...extractUtf16Runs(bytes, 1)];
    const gbText = decodeBytes(bytes, "gb18030");
    const utf8Text = decodeBytes(bytes, "utf-8");
    return [...utf16Snippets, gbText, utf8Text].join("\n");
}
function decodeBytes(bytes: Uint8Array, encoding: string) {
    try {
        return new TextDecoder(encoding as "utf-8", { fatal: false }).decode(bytes);
    } catch {
        return "";
    }
}
function extractUtf16Runs(bytes: Uint8Array, parity: 0 | 1) {
    const snippets: string[] = [];
    let start = -1;
    for (let i = parity; i + 1 < bytes.length; i += 2) {
        const low = bytes[i] ?? 0;
        const high = bytes[i + 1] ?? 0;
        const code = low | (high << 8);
        if (isLikelyWordChar(code)) {
            if (start < 0) start = i;
        } else if (start >= 0) {
            pushUtf16Run(snippets, bytes, start, i);
            start = -1;
        }
    }
    if (start >= 0) pushUtf16Run(snippets, bytes, start, bytes.length - ((bytes.length - start) % 2));
    return snippets;
}
function pushUtf16Run(target: string[], bytes: Uint8Array, start: number, end: number) {
    if (end - start < 16) return;
    const run = bytes.slice(start, end);
    const text = decodeBytes(run, "utf-16le")
        .replace(/\0/g, "")
        .trim();
    if (text.length >= 6) target.push(text);
}
function isLikelyWordChar(code: number) {
    return (
        (code >= 0x4e00 && code <= 0x9fff) ||
        (code >= 0x3400 && code <= 0x4dbf) ||
        (code >= 0x20 && code <= 0x7e) ||
        (code >= 0x3000 && code <= 0x303f) ||
        (code >= 0xff01 && code <= 0xff5e) ||
        code === 0x0009 ||
        code === 0x000a ||
        code === 0x000d
    );
}
function extractTeachingSegments(text: string) {
    const lines = text
        .split(/\r\n|\r|\n/)
        .map((line) => line.trim())
        .filter((line) => line.length > 0)
        .filter((line) => /[\u4e00-\u9fff]/.test(line))
        .filter((line) => !looksGarbled(line));

    const title = extractGuideTitleFromLines(lines);
    const keyPoints = extractKeyPointsFromLines(lines);
    const difficultPoints = extractDifficultPointsFromLines(lines);
    if (title || keyPoints.length > 0 || difficultPoints.length > 0) {
        return [title, "重点知识", ...keyPoints, "难点知识", ...difficultPoints].filter(Boolean);
    }
    return lines.slice(0, 80);
}
function looksGarbled(segment: string) {
    const noisy = (segment.match(/[^\u4e00-\u9fffA-Za-z0-9，。；：、（）()【】《》“”‘’：:,.!?%\- ]/g) ?? []).length;
    return noisy > 0 && noisy / segment.length > 0.15;
}
function extractGuideTitleFromLines(lines: string[]) {
    const headLines = lines.slice(0, 5);
    const preferredLines = headLines.filter((line) => !/学习指导|文字教材|习题测试/.test(line));
    const sourceLines = preferredLines.length > 0 ? preferredLines : headLines;
    const section = sourceLines.map((line) => extractUnitTitleFromLine(line, "节")).find(Boolean);
    if (section) return section;
    const chapter = sourceLines.map((line) => extractUnitTitleFromLine(line, "章")).find(Boolean);
    if (chapter) return chapter;
    const part = sourceLines.map((line) => extractUnitTitleFromLine(line, "篇")).find(Boolean);
    if (part) return part;
    return "";
}
function extractUnitTitleFromLine(line: string, unit: "篇" | "章" | "节") {
    const number = "[一二三四五六七八九十百零两\\d]+";
    const pattern = new RegExp(
        `([第]?${number}\\s*${unit}(?:\\s*(?![第]?${number}\\s*[篇章节])[^\\r\\n，。；;：:])*)`,
        "g"
    );
    const matched = line.match(pattern);
    if (!matched || matched.length === 0) return "";
    const picked = matched[matched.length - 1] ?? "";
    return picked
        .replace(/学习指导|文字教材|习题测试/g, "")
        .replace(/\s+/g, " ")
        .trim();
}
function extractKeyPointsFromLines(lines: string[]) {
    return extractPointsFromLinesByHeader(
        lines,
        "重点知识",
        /难点知识|学习目的|教学要求|学习安排|预备知识/
    );
}
function extractDifficultPointsFromLines(lines: string[]) {
    return extractPointsFromLinesByHeader(
        lines,
        "难点知识",
        /重点知识|学习目的|教学要求|学习安排|预备知识/
    );
}
function extractPointsFromLinesByHeader(lines: string[], header: "重点知识" | "难点知识", stopPattern: RegExp) {
    const start = lines.findIndex((line) => line.includes(header));
    if (start < 0) return [];

    const points: string[] = [];
    const chunk: string[] = [];
    for (let i = start; i < lines.length; i += 1) {
        const line = lines[i];
        if (!line) continue;
        if (i > start && stopPattern.test(line)) break;
        chunk.push(line);
    }
    const merged = chunk.join(" ").replace(/\s+/g, " ").trim();
    const afterHeader = merged.replace(new RegExp(`^.*?${header}[：:]\\s*`), "");
    const numberedMatches = afterHeader.match(/(?:\d+|[一二三四五六七八九十]+)[、.．]\s*[\s\S]*?(?=(?:\d+|[一二三四五六七八九十]+)[、.．]|$)/g) ?? [];
    numberedMatches.forEach((item) => {
        const cleaned = item
            .replace(/^(?:\d+|[一二三四五六七八九十]+)[、.．]\s*/, "")
            .replace(/[。；;]+$/, "")
            .trim();
        if (cleaned) points.push(cleaned);
    });
    if (points.length === 0) {
        chunk.forEach((line, index) => {
            const normalized = (index === 0 ? line.replace(new RegExp(`^.*?${header}[：:]\\s*`), "") : line).trim();
            if (!normalized) return;
            points.push(...splitToPoints(normalized));
        });
    }
    return points
        .map((point) => point.replace(/\s+/g, " ").trim())
        .filter((point) => point.length >= 4 && point.length <= 120);
}
function extractGuideTitle(segments: string[], fallback: string) {
    const start = segments.findIndex((line) => line.includes("学习指导"));
    const pool = start >= 0 ? segments.slice(start + 1) : segments;
    return (
        pool.find((line) =>
            !line.includes("重点知识") &&
            !line.includes("难点知识") &&
            !line.includes("学习目的") &&
            !line.includes("学习安排") &&
            line.length <= 40 &&
            /[\u4e00-\u9fff]/.test(line)
        ) ?? fallback
    );
}

function splitToPoints(line: string) {
    return line
        .split(/[；;。]/)
        .map((item) => item.replace(/^\d+[、.．]?\s*/, "").trim())
        .filter((item) => item.length >= 4 && !/学习指导|学习安排|默认段落|页眉|页脚/.test(item));
}

function extractGuidePoints(segments: string[]) {
    const start = segments.findIndex((line) => line.includes("重点知识"));
    const points: string[] = [];
    if (start >= 0) {
        for (let i = start + 1; i < segments.length; i += 1) {
            const line = segments[i];
            if (!line) continue;
            if (/难点知识|学习目的|预备知识|学习安排/.test(line)) break;
            points.push(...splitToPoints(line));
        }
    }
    if (points.length === 0) {
        segments.forEach((line) => {
            if (/重点知识|难点知识|学习指导/.test(line)) return;
            points.push(...splitToPoints(line));
        });
    }
    return Array.from(new Set(points)).slice(0, 12);
}
function extractGuideDifficultPoints(segments: string[]) {
    const start = segments.findIndex((line) => line.includes("难点知识"));
    const points: string[] = [];
    if (start >= 0) {
        for (let i = start + 1; i < segments.length; i += 1) {
            const line = segments[i];
            if (!line) continue;
            if (/重点知识|学习目的|预备知识|学习安排/.test(line)) break;
            points.push(...splitToPoints(line));
        }
    }
    return Array.from(new Set(points)).slice(0, 12);
}

function extractSectionPoints(segments: string[]) {
    const points = extractGuidePoints(segments);
    return points.length > 0 ? points : segments.filter((line) => line.length <= 60).slice(0, 10);
}

async function hydratePartTitles() {
    for (const part of courseTree.value) {
        if (!part.guideDocUrl) continue;
        const segments = await readDocSegments(part.guideDocUrl);
        part.displayName = extractGuideTitle(segments, part.label);
    }
}
async function hydrateChapterTitles(part: PartNode) {
    for (const chapter of part.chapters) {
        if (!chapter.guideDocUrl || chapter.displayName !== chapter.label) continue;
        const segments = await readDocSegments(chapter.guideDocUrl);
        chapter.displayName = extractGuideTitle(segments, chapter.label);
    }
}
async function hydrateSectionTitles(chapter: ChapterNode) {
    for (const section of chapter.sections) {
        if (section.displayName !== section.label) continue;
        const source = section.textDocUrl ?? section.quizDocUrl;
        if (!source) continue;
        const segments = await readDocSegments(source);
        section.displayName = extractGuideTitle(segments, section.label);
    }
}

async function loadPartFocus(part: PartNode) {
    focusTitle.value = part.displayName;
    focusLoading.value = true;
    if ((part.keyPoints === null || part.difficultPoints === null) && part.guideDocUrl) {
        const segments = await readDocSegments(part.guideDocUrl);
        part.keyPoints = extractGuidePoints(segments);
        part.difficultPoints = extractGuideDifficultPoints(segments);
        part.displayName = extractGuideTitle(segments, part.displayName);
    }
    focusPoints.value = part.keyPoints ?? [];
    focusDifficultPoints.value = part.difficultPoints ?? [];
    focusLoading.value = false;
}

async function loadChapterFocus(part: PartNode, chapter: ChapterNode) {
    focusTitle.value = chapter.displayName;
    focusLoading.value = true;
    if ((chapter.keyPoints === null || chapter.difficultPoints === null) && chapter.guideDocUrl) {
        const segments = await readDocSegments(chapter.guideDocUrl);
        chapter.keyPoints = extractGuidePoints(segments);
        chapter.difficultPoints = extractGuideDifficultPoints(segments);
        chapter.displayName = extractGuideTitle(segments, chapter.displayName);
    }
    focusTitle.value = chapter.displayName;
    focusPoints.value = chapter.keyPoints ?? [];
    focusDifficultPoints.value = chapter.difficultPoints ?? [];
    focusLoading.value = false;
}

async function loadSectionDetails(section: SectionNode) {
    sectionLoading.value = true;
    if (section.displayName === section.label) {
        const titleSource = section.textDocUrl ?? section.quizDocUrl;
        if (titleSource) section.displayName = extractGuideTitle(await readDocSegments(titleSource), section.displayName);
    }
    if (section.keyPoints === null && section.textDocUrl) section.keyPoints = extractSectionPoints(await readDocSegments(section.textDocUrl));
    if (section.keyPoints === null) section.keyPoints = [];
    sectionLoading.value = false;
}

function isPartExpanded(partId: string) { return !!expandedParts[partId]; }
function isChapterExpanded(chapterId: string) { return !!expandedChapters[chapterId]; }
function isPartSelected(partId: string) {
    const state = selected.value;
    return state.level === "part" && state.partId === partId;
}
function isChapterSelected(partId: string, chapterId: string) {
    const state = selected.value;
    return state.level === "chapter" && state.partId === partId && state.chapterId === chapterId;
}
function isSectionSelected(partId: string, chapterId: string, sectionId: string) {
    const state = selected.value;
    return (
        state.level === "section" &&
        state.partId === partId &&
        state.chapterId === chapterId &&
        state.sectionId === sectionId
    );
}
function resolvePartEntrySection(part: PartNode) {
    if (part.label !== "绪论") return null;

    for (const chapter of part.chapters) {
        const preferred = chapter.sections.find((section) =>
            section.videoFiles.length > 0 || section.pdfFiles.length > 0 || !!section.textDocUrl || !!section.quizDocUrl
        );
        if (preferred) return { chapter, section: preferred };
        const firstSection = chapter.sections[0];
        if (firstSection) return { chapter, section: firstSection };
    }
    return null;
}
function onPartClick(part: PartNode) {
    const entry = resolvePartEntrySection(part);
    if (entry) {
        expandedParts[part.id] = false;
        expandedChapters[entry.chapter.id] = false;
        void hydrateChapterTitles(part);
        void hydrateSectionTitles(entry.chapter);
        onSectionClick(part, entry.chapter, entry.section, false);
        return;
    }

    expandedParts[part.id] = !expandedParts[part.id];
    selected.value = { level: "part", partId: part.id };
    void hydrateChapterTitles(part);
    void loadPartFocus(part);
}
function onChapterClick(part: PartNode, chapter: ChapterNode) {
    expandedParts[part.id] = true;
    expandedChapters[chapter.id] = !expandedChapters[chapter.id];
    selected.value = { level: "chapter", partId: part.id, chapterId: chapter.id };
    void hydrateSectionTitles(chapter);
    void loadChapterFocus(part, chapter);
}
function onSectionClick(part: PartNode, chapter: ChapterNode, section: SectionNode, shouldExpand = true) {
    const prevPdfUrl = activePdf.value?.url ?? "";
    expandedParts[part.id] = shouldExpand;
    expandedChapters[chapter.id] = shouldExpand;
    selected.value = { level: "section", partId: part.id, chapterId: chapter.id, sectionId: section.id };
    activeVideoIndex.value = 0;
    activePdfIndex.value = 0;
    activePdfPageIndex.value = 0;
    pdfPageImages.value = [];
    pdfRenderError.value = "";
    void loadSectionDetails(section);

    const nextPdfUrl = section.pdfFiles[0]?.url ?? "";
    if (nextPdfUrl === prevPdfUrl) void loadPdfPages(nextPdfUrl);
}
function setActivePdf(index: number) {
    if (!selectedSection.value) return;
    if (index < 0 || index >= selectedSection.value.pdfFiles.length) return;
    activePdfIndex.value = index;
}
function goPrevPdfPage() {
    if (!hasPrevPdfPage.value) return;
    activePdfPageIndex.value -= 1;
}
function goNextPdfPage() {
    if (!hasNextPdfPage.value) return;
    activePdfPageIndex.value += 1;
}
function jumpToPdfPage(pageIndex: number | null) {
    if (pageIndex === null) return;
    if (pageIndex < 0 || pageIndex >= pdfPageImages.value.length) return;
    activePdfPageIndex.value = pageIndex;
}
</script>

<style scoped>
.course-shell {
    --stroke: var(--sy-border, #dfe4ea);
    --text: var(--el-text-color-primary, #303133);
    --muted: var(--el-text-color-secondary, #909399);
    --accent: var(--el-color-primary, #4776a8);
    --good: var(--el-color-success, #529b64);
    --warn: var(--el-color-warning, #b98739);
    display: flex;
    flex-direction: column;
    height: 100dvh;
    min-height: 360px;
    padding: 12px;
    color: var(--text);
    background: var(--sy-background, #f5f7fa);
    box-sizing: border-box;
}
.course-header { flex: 0 0 auto; padding: 4px 4px 12px; }
.course-header h1 { margin: 0; font-size: 18px; font-weight: 600; }
.layout { display: flex; flex: 1; min-width: 0; min-height: 0; }
.sidebar, .main { border: 1px solid var(--stroke); border-radius: 6px; background: var(--sy-surface, #fff); box-sizing: border-box; }
.sidebar { flex: 0 0 auto; min-width: 0; overflow: auto; padding: 12px; }
.sidebar-header h2 { margin: 0 0 10px; font-size: 13px; font-weight: 600; }
.main { flex: 1; min-width: 0; overflow: auto; padding: 16px; }
.tree, .children { display: flex; flex-direction: column; gap: 4px; }
.children { margin: 4px 0 4px 8px; padding-left: 8px; border-left: 1px solid var(--stroke); }
.tree-btn { width: 100%; display: flex; align-items: center; gap: 6px; padding: 7px 8px; border: 0; border-radius: 4px; background: transparent; color: var(--text); text-align: left; font: inherit; cursor: pointer; }
.tree-btn span:last-child { min-width: 0; overflow-wrap: anywhere; }
.tree-btn:hover { background: var(--el-fill-color-light, #f5f7fa); }
.tree-btn.active { color: var(--accent); background: var(--el-color-primary-light-9, #edf3fa); }
.tree-btn:focus-visible, .video-tab:focus-visible, .pdf-file-tab:focus-visible, .ppt-thumb:focus-visible { outline: 2px solid var(--accent); outline-offset: 1px; }
.level-1 { font-weight: 600; }
.level-2, .level-3 { font-size: 13px; }
.leaf-empty, .muted, .empty, .empty-frame { color: var(--muted); }
.leaf-empty { font-size: 12px; padding: 4px 8px; }
.empty { padding: 48px 12px; text-align: center; }
.focus-only, .focus-content, .focus-block { display: grid; gap: 12px; }
.focus-header, .section-head { padding-bottom: 12px; border-bottom: 1px solid var(--stroke); }
.focus-header h2, .section-head h2 { margin: 0; font-size: 16px; font-weight: 600; }
.eyebrow { margin: 0; font-size: 13px; font-weight: 600; }
.keypoints { list-style: none; padding: 0; margin: 0; }
.keypoints li { padding: 8px 0; border-bottom: 1px solid var(--el-border-color-lighter, #ebeef5); }
.player-grid { margin-top: 16px; display: grid; gap: 16px; grid-template-columns: repeat(2, minmax(0, 1fr)); }
.panel { border: 1px solid var(--stroke); border-radius: 6px; padding: 12px; min-width: 0; }
.panel-head { display: flex; align-items: center; justify-content: space-between; margin-bottom: 10px; }
.panel-title { margin: 0; font-size: 14px; font-weight: 600; }
.video-switch, .pdf-file-switch { display: flex; flex-wrap: wrap; gap: 6px; margin-bottom: 8px; }
.video-tab, .pdf-file-tab { background: transparent; color: var(--text); border: 1px solid var(--stroke); border-radius: 4px; padding: 4px 8px; font: inherit; cursor: pointer; }
.video-tab.active, .pdf-file-tab.active { border-color: var(--accent); color: var(--accent); }
.video-player, .video-placeholder { display: block; width: 100%; height: clamp(200px, 35vh, 340px); border-radius: 4px; background: #111827; object-fit: contain; }
.video-placeholder { display: grid; place-items: center; background: var(--el-fill-color-light, #f5f7fa); color: var(--muted); }
.ppt-frame { display: grid; place-items: center; overflow: hidden; width: 100%; height: clamp(200px, 38vh, 380px); border: 1px solid var(--stroke); border-radius: 4px; background: var(--el-fill-color-light, #f5f7fa); }
.pdf-main-image { width: 100%; height: 100%; display: block; object-fit: contain; }
.ppt-nav { margin-top: 10px; display: flex; align-items: center; justify-content: space-between; gap: 10px; }
.ppt-nav-status { font-size: 12px; color: var(--muted); font-variant-numeric: tabular-nums; }
.ppt-thumbs { margin-top: 10px; display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 8px; }
.ppt-thumb { display: grid; gap: 4px; border: 1px solid var(--stroke); border-radius: 4px; padding: 4px; background: transparent; color: var(--text); cursor: pointer; }
.ppt-thumb.active { border-color: var(--accent); }
.ppt-thumb.empty { opacity: .45; cursor: default; }
.ppt-thumb-page { font-size: 11px; text-align: center; }
.ppt-thumb-preview, .ppt-thumb-placeholder { width: 100%; aspect-ratio: 4 / 3; object-fit: contain; background: var(--el-fill-color-light, #f5f7fa); }
.layout.is-compact { flex-direction: column; }
.layout.is-compact .sidebar { width: 100%; }
@media (max-width: 1300px) { .player-grid { grid-template-columns: 1fr; } }
</style>
