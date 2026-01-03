<template>
    <div class="course-shell">
        <header class="hero">
            <div>
                <p class="eyebrow">课程平台 · 学习快线</p>
                <h1>精选课程，一站式学习与练习</h1>
                <p class="subtitle">浏览课程、观看讲解视频、查看PPT、聚焦学习重点，并通过自动评分练习巩固所学。</p>
            </div>
            <div class="hero-actions">
                <div class="pill">实时练习反馈</div>
                <div class="pill pill-accent">自动评分 · 即时结果</div>
            </div>
        </header>

        <div class="layout">
            <aside class="sidebar">
                <div class="sidebar-header">
                    <h2>课程管理列表</h2>
                    <input v-model="searchTerm" class="search" placeholder="搜索课程 / 讲师 / 关键词" type="search" />
                </div>
                <div class="course-list">
                    <button v-for="course in filteredCourses" :key="course.id"
                        :class="['course-item', { active: course.id === selectedCourseId }]"
                        @click="selectCourse(course.id)">
                        <div class="course-item__head">
                            <span class="badge">{{ course.category }}</span>
                            <span class="duration">{{ course.duration }}</span>
                        </div>
                        <div class="course-item__title">{{ course.title }}</div>
                        <div class="course-item__meta">
                            <span>{{ course.instructor }}</span>
                            <span>难度 {{ course.level }}</span>
                            <span>{{ course.lessons }} 节</span>
                        </div>
                        <div class="course-item__foot">
                            <span class="pill-sm">进度 {{ course.progress }}%</span>
                            <span class="pill-sm ghost">测验 {{ course.quiz.length }} 题</span>
                        </div>
                    </button>
                </div>
            </aside>

            <main class="main">
                <section v-if="selectedCourse" class="course-details">
                    <header class="course-head">
                        <div>
                            <p class="eyebrow">当前课程</p>
                            <h2>{{ selectedCourse.title }}</h2>
                            <div class="tag-row">
                                <span class="pill">{{ selectedCourse.category }}</span>
                                <span class="pill ghost">{{ selectedCourse.level }}</span>
                                <span class="pill ghost">{{ selectedCourse.duration }}</span>
                            </div>
                        </div>
                        <div class="metrics">
                            <div class="metric">
                                <p>完成度</p>
                                <strong>{{ selectedCourse.progress }}%</strong>
                            </div>
                            <div class="metric">
                                <p>练习得分</p>
                                <strong>
                                    <span v-if="currentGrade.graded">{{ currentGrade.correct }}/{{ currentGrade.total
                                    }}</span>
                                    <span v-else>待评分</span>
                                </strong>
                            </div>
                        </div>
                    </header>

                    <section class="player-grid">
                        <div class="panel">
                            <div class="panel-head">
                                <div>
                                    <p class="eyebrow">讲解视频</p>
                                    <h3>{{ selectedCourse.title }}</h3>
                                </div>
                                <span class="pill-sm">支持倍速与全屏</span>
                            </div>
                            <video class="video" controls controlsList="nodownload" :src="selectedCourse.videoUrl">
                                您的浏览器不支持视频播放。
                            </video>
                        </div>

                        <div class="panel">
                            <div class="panel-head">
                                <div>
                                    <p class="eyebrow">PPT 预览</p>
                                    <h3>{{ selectedCourse.pptTitle }}</h3>
                                </div>
                                <span class="pill-sm">第 {{ slideIndex + 1 }} / {{ selectedCourse.pptSlides.length }}
                                    页</span>
                            </div>
                            <div class="ppt-frame">
                                <img :src="selectedCourse.pptSlides[slideIndex]" :alt="selectedCourse.pptTitle" />
                            </div>
                            <div class="ppt-controls">
                                <button @click="prevSlide" :disabled="slideIndex === 0">上一页</button>
                                <div class="stepper">{{ slideIndex + 1 }} / {{ selectedCourse.pptSlides.length }}</div>
                                <button @click="nextSlide"
                                    :disabled="slideIndex === selectedCourse.pptSlides.length - 1">下一页</button>
                            </div>
                            <div class="thumbs">
                                <button v-for="(slide, idx) in selectedCourse.pptSlides" :key="slide"
                                    :class="['thumb', { active: idx === slideIndex }]" @click="jumpTo(idx)">
                                    <img :src="slide" :alt="`幻灯片 ${idx + 1}`" />
                                    <span>第 {{ idx + 1 }} 页</span>
                                </button>
                            </div>
                        </div>
                    </section>

                    <section class="info-grid">
                        <div class="panel">
                            <div class="panel-head">
                                <p class="eyebrow">学习重点</p>
                                <h3>重点笔记</h3>
                            </div>
                            <ul class="keypoints">
                                <li v-for="point in selectedCourse.keyPoints" :key="point">{{ point }}</li>
                            </ul>
                        </div>

                        <div class="panel quiz">
                            <div class="panel-head quiz-head">
                                <div>
                                    <p class="eyebrow">习题练习</p>
                                    <h3>自动评分</h3>
                                </div>
                                <button class="primary" @click="gradeQuiz">评分</button>
                            </div>
                            <div class="quiz-summary">
                                <span>已选 {{ answeredCount }} / {{ selectedCourse.quiz.length }} 题</span>
                                <span v-if="currentGrade.graded">得分：{{ currentGrade.correct }} / {{ currentGrade.total
                                }}</span>
                                <span v-else>提交后显示得分</span>
                            </div>

                            <div v-for="(question, qIdx) in selectedCourse.quiz" :key="question.question"
                                class="question">
                                <div class="question-title">{{ qIdx + 1 }}. {{ question.question }}</div>
                                <div class="options">
                                    <label v-for="(option, oIdx) in question.options" :key="option"
                                        :class="['option', optionState(qIdx, oIdx)]">
                                        <input type="radio" :name="`q-${qIdx}`" :value="oIdx"
                                            :checked="userAnswers[selectedCourseId]?.[qIdx] === oIdx"
                                            @change="selectOption(qIdx, oIdx)" />
                                        <span class="option-text">{{ option }}</span>
                                        <span v-if="isCorrectChoice(qIdx, oIdx)" class="chip good">正确答案</span>
                                        <span v-else-if="isUserWrong(qIdx, oIdx)" class="chip warn">已选</span>
                                    </label>
                                </div>
                                <div v-if="showFeedback(qIdx)" class="feedback"
                                    :class="{ good: isQuestionCorrect(qIdx), warn: !isQuestionCorrect(qIdx) }">
                                    <span v-if="isQuestionCorrect(qIdx)">回答正确！</span>
                                    <span v-else>正确答案：{{ question.options[question.correctIndex] }}</span>
                                    <p class="explain">{{ question.explanation }}</p>
                                </div>
                            </div>
                        </div>
                    </section>
                </section>

                <section v-else class="empty">
                    <p>暂无课程，请先创建课程数据。</p>
                </section>
            </main>
        </div>
    </div>
</template>

<script setup lang="ts">
import { computed, reactive, ref, watch } from "vue";

type QuizItem = {
    question: string;
    options: string[];
    correctIndex: number;
    explanation: string;
};

type Course = {
    id: string;
    title: string;
    instructor: string;
    duration: string;
    category: string;
    level: string;
    lessons: number;
    progress: number;
    videoUrl: string;
    pptTitle: string;
    pptSlides: string[];
    keyPoints: string[];
    quiz: QuizItem[];
};

const courses = ref<Course[]>([
    {
        id: "fe-engineering",
        title: "前端工程化与组件化",
        instructor: "陈晓帆",
        duration: "4h 30m",
        category: "前端",
        level: "中级",
        lessons: 12,
        progress: 65,
        videoUrl: "https://interactive-examples.mdn.mozilla.net/media/cc0-videos/flower.mp4",
        pptTitle: "工程化设计要点",
        pptSlides: [
            "https://images.unsplash.com/photo-1521737604893-d14cc237f11d?auto=format&fit=crop&w=900&q=80",
            "https://images.unsplash.com/photo-1504639725590-34d4982c0773?auto=format&fit=crop&w=900&q=80",
            "https://images.unsplash.com/photo-1521737604893-d14cc237f11d?auto=format&fit=crop&w=900&q=80&sat=-30",
        ],
        keyPoints: [
            "理解构建链路：打包、分发与性能预算",
            "掌握组件拆分、状态提升与可复用性",
            "建立设计规范：色板、间距、排版与交互反馈",
            "掌握自动化测试与 CI/CD 基础",
            "常见工程化陷阱与回溯手段",
        ],
        quiz: [
            {
                question: "选择工程化能带来的首要收益",
                options: ["统一代码风格，减少认知成本", "提升产品上新速度", "让代码更炫酷", "隐藏技术债务"],
                correctIndex: 0,
                explanation: "统一标准能降低团队协作摩擦，是工程化的直接收益之一。",
            },
            {
                question: "组件拆分时优先考虑的原则是?",
                options: ["视觉独特性", "状态与数据流边界", "文件大小", "是否能复用动画"],
                correctIndex: 1,
                explanation: "先围绕状态与数据边界拆分，再考虑样式与动画。",
            },
            {
                question: "CI 中最应该最先落地的一步是?",
                options: ["自动部署生产", "自动化测试", "自动写文档", "生成彩色报表"],
                correctIndex: 1,
                explanation: "可靠的测试是所有自动化的基础，没有测试自动部署风险极高。",
            },
        ],
    },
    {
        id: "data-viz",
        title: "数据可视化与叙事",
        instructor: "林安琪",
        duration: "3h 10m",
        category: "数据可视化",
        level: "中级",
        lessons: 9,
        progress: 40,
        videoUrl: "https://interactive-examples.mdn.mozilla.net/media/cc0-videos/flower.mp4",
        pptTitle: "视觉编码与版式",
        pptSlides: [
            "https://images.unsplash.com/photo-1504384308090-c894fdcc538d?auto=format&fit=crop&w=900&q=80",
            "https://images.unsplash.com/photo-1500530855697-b586d89ba3ee?auto=format&fit=crop&w=900&q=80",
            "https://images.unsplash.com/photo-1483478550801-ceba5fe50e8e?auto=format&fit=crop&w=900&q=80",
        ],
        keyPoints: [
            "选择合适的图形语法与通道映射",
            "控制信息密度，避免过度装饰",
            "突出故事线：比较、变化、组成与分布",
            "用色彩与留白建立节奏感",
            "可访问性：对比度、标注与键盘操作",
        ],
        quiz: [
            {
                question: "当需要比较多个类别且总量一致时，首选的图表是?",
                options: ["饼图", "堆叠面积图", "并列条形图", "气泡图"],
                correctIndex: 2,
                explanation: "并列条形图最能清晰比较类别差异，避免角度误判。",
            },
            {
                question: "强调时间趋势时，最需要控制的要素是?",
                options: ["线条粗细", "图例位置", "坐标刻度与间距", "网格线颜色"],
                correctIndex: 2,
                explanation: "均匀刻度与合适间距能保证趋势阅读的稳定性。",
            },
        ],
    },
    {
        id: "ml-foundation",
        title: "机器学习基础模型",
        instructor: "唐瀚文",
        duration: "5h 00m",
        category: "机器学习",
        level: "进阶",
        lessons: 14,
        progress: 20,
        videoUrl: "https://interactive-examples.mdn.mozilla.net/media/cc0-videos/flower.mp4",
        pptTitle: "模型选择与评估",
        pptSlides: [
            "https://images.unsplash.com/photo-1545239351-1141bd82e8a6?auto=format&fit=crop&w=900&q=80",
            "https://images.unsplash.com/photo-1555949963-aa79dcee981c?auto=format&fit=crop&w=900&q=80",
            "https://images.unsplash.com/photo-1531297484001-80022131f5a1?auto=format&fit=crop&w=900&q=80",
        ],
        keyPoints: [
            "监督学习 vs 无监督学习的典型场景",
            "避免过拟合：正则化、交叉验证、早停",
            "评估指标：精确率、召回率、F1、AUC",
            "特征工程与数据清洗的重要性",
            "常见陷阱：数据泄漏与分布漂移",
        ],
        quiz: [
            {
                question: "为了防止过拟合，以下哪种方法最直接?",
                options: ["增加模型深度", "使用交叉验证", "减小训练数据", "只看训练集精度"],
                correctIndex: 1,
                explanation: "交叉验证可以稳定评估泛化能力，并帮助选择合适的超参数。",
            },
            {
                question: "在二分类问题中，若正负样本极度不平衡，首选的指标是?",
                options: ["准确率", "精确率/召回率", "均方误差", "训练时间"],
                correctIndex: 1,
                explanation: "不平衡场景应关注精确率与召回率，或 AUC/PR 曲线。",
            },
        ],
    },
]);

const searchTerm = ref("");
const selectedCourseId = ref(courses.value[0]?.id ?? "");
const slideIndex = ref(0);
const userAnswers = reactive<Record<string, Record<number, number | null>>>({});
const gradeState = reactive<Record<string, { graded: boolean; correct: number; total: number }>>({});

watch(
    () => selectedCourseId.value,
    (id) => {
        slideIndex.value = 0;
        if (!userAnswers[id]) {
            userAnswers[id] = {};
        }
    },
    { immediate: true }
);

const filteredCourses = computed(() => {
    const term = searchTerm.value.trim().toLowerCase();
    if (!term) return courses.value;
    return courses.value.filter((course) => {
        const haystack = [
            course.title,
            course.instructor,
            course.category,
            course.level,
            ...course.keyPoints,
        ].join(" ").toLowerCase();
        return haystack.includes(term);
    });
});

const selectedCourse = computed(() =>
    courses.value.find((c) => c.id === selectedCourseId.value)
);

const currentGrade = computed(() => {
    const grade = gradeState[selectedCourseId.value];
    const total = selectedCourse.value?.quiz.length ?? 0;
    if (!grade) {
        return { graded: false, correct: 0, total };
    }
    return grade;
});

const answeredCount = computed(() => {
    const answers = userAnswers[selectedCourseId.value] ?? {};
    return Object.values(answers).filter((v) => v !== null && v !== undefined).length;
});

function selectCourse(id: string) {
    selectedCourseId.value = id;
}

function prevSlide() {
    slideIndex.value = Math.max(0, slideIndex.value - 1);
}

function nextSlide() {
    if (!selectedCourse.value) return;
    slideIndex.value = Math.min(
        selectedCourse.value.pptSlides.length - 1,
        slideIndex.value + 1
    );
}

function jumpTo(idx: number) {
    slideIndex.value = idx;
}

function selectOption(qIdx: number, oIdx: number) {
    const courseId = selectedCourseId.value;
    if (!userAnswers[courseId]) userAnswers[courseId] = {};
    userAnswers[courseId][qIdx] = oIdx;
    const total = selectedCourse.value?.quiz.length ?? 0;
    gradeState[courseId] = { graded: false, correct: 0, total };
}

function gradeQuiz() {
    const course = selectedCourse.value;
    if (!course) return;
    const answers = userAnswers[course.id] ?? {};
    let correct = 0;
    course.quiz.forEach((q, idx) => {
        if (answers[idx] === q.correctIndex) correct += 1;
    });
    gradeState[course.id] = { graded: true, correct, total: course.quiz.length };
}

function optionState(qIdx: number, oIdx: number) {
    const grade = gradeState[selectedCourseId.value];
    if (!grade?.graded) return "";
    const course = selectedCourse.value;
    if (!course) return "";
    const isCorrect = course.quiz[qIdx]?.correctIndex === oIdx;
    const selected = userAnswers[selectedCourseId.value]?.[qIdx] === oIdx;
    if (isCorrect && selected) return "is-correct";
    if (!isCorrect && selected) return "is-wrong";
    if (isCorrect) return "is-answer";
    return "";
}

function isQuestionCorrect(qIdx: number) {
    const course = selectedCourse.value;
    if (!course) return false;
    const selected = userAnswers[selectedCourseId.value]?.[qIdx];
    return selected === course.quiz[qIdx]?.correctIndex;
}

function showFeedback(qIdx: number) {
    return gradeState[selectedCourseId.value]?.graded && !!selectedCourse.value?.quiz[qIdx];
}

function isCorrectChoice(qIdx: number, oIdx: number) {
    if (!gradeState[selectedCourseId.value]?.graded) return false;
    const course = selectedCourse.value;
    if (!course) return false;
    return course.quiz[qIdx]?.correctIndex === oIdx;
}

function isUserWrong(qIdx: number, oIdx: number) {
    const course = selectedCourse.value;
    if (!course) return false;
    const selected = userAnswers[selectedCourseId.value]?.[qIdx];
    const graded = gradeState[selectedCourseId.value]?.graded;
    return graded && selected === oIdx && oIdx !== course.quiz[qIdx]?.correctIndex;
}
</script>

<style scoped>
@import url("https://fonts.googleapis.com/css2?family=Space+Grotesk:wght@400;500;600;700&display=swap");

:global(:root) {
    --bg: #0b1021;
    --panel: rgba(255, 255, 255, 0.04);
    --panel-strong: rgba(255, 255, 255, 0.07);
    --stroke: rgba(255, 255, 255, 0.12);
    --accent: #6ee7ff;
    --accent-strong: #22d3ee;
    --text: #eaf4ff;
    --muted: #9bb1d1;
    --warn: #fbbf24;
    --good: #34d399;
}

* {
    box-sizing: border-box;
}

.course-shell {
    font-family: "Space Grotesk", "Segoe UI", sans-serif;
    color: var(--text);
    background: radial-gradient(circle at 20% 20%, rgba(110, 231, 255, 0.08), transparent 32%),
        radial-gradient(circle at 80% 0%, rgba(34, 211, 238, 0.12), transparent 38%),
        linear-gradient(135deg, #060915, #0f1629 55%, #0b1021);
    min-height: 100vh;
    padding: 32px;
}

.hero {
    display: flex;
    align-items: flex-end;
    justify-content: space-between;
    gap: 16px;
    padding: 24px;
    border: 1px solid var(--stroke);
    border-radius: 16px;
    background: var(--panel);
    backdrop-filter: blur(12px);
    box-shadow: 0 18px 40px rgba(0, 0, 0, 0.35);
    margin-bottom: 20px;
}

.hero h1 {
    font-size: 28px;
    margin: 6px 0 4px;
    letter-spacing: -0.5px;
}

.subtitle {
    color: var(--muted);
    margin: 0;
}

.hero-actions {
    display: flex;
    gap: 10px;
    align-items: center;
}

.layout {
    display: grid;
    grid-template-columns: 300px 1fr;
    gap: 16px;
}

.sidebar {
    border: 1px solid var(--stroke);
    border-radius: 16px;
    background: var(--panel);
    padding: 16px;
    height: fit-content;
    box-shadow: 0 14px 36px rgba(0, 0, 0, 0.28);
}

.sidebar-header h2 {
    margin: 0 0 8px;
    font-size: 18px;
}

.search {
    width: 100%;
    background: var(--panel-strong);
    border: 1px solid var(--stroke);
    color: var(--text);
    padding: 10px 12px;
    border-radius: 12px;
    outline: none;
}

.search:focus {
    border-color: var(--accent);
    box-shadow: 0 0 0 3px rgba(110, 231, 255, 0.15);
}

.course-list {
    margin-top: 12px;
    display: flex;
    flex-direction: column;
    gap: 10px;
}

.course-item {
    width: 100%;
    text-align: left;
    border: 1px solid var(--stroke);
    border-radius: 14px;
    padding: 12px;
    background: var(--panel-strong);
    color: var(--text);
    cursor: pointer;
    transition: border-color 0.2s ease, transform 0.12s ease;
}

.course-item:hover {
    border-color: var(--accent);
    transform: translateY(-2px);
}

.course-item.active {
    border-color: var(--accent-strong);
    box-shadow: 0 0 0 2px rgba(110, 231, 255, 0.18);
}

.course-item__head,
.course-item__meta,
.course-item__foot {
    display: flex;
    gap: 8px;
    align-items: center;
    flex-wrap: wrap;
    color: var(--muted);
    font-size: 12px;
}

.course-item__title {
    font-size: 16px;
    margin: 6px 0;
    color: var(--text);
}

.course-item__foot {
    margin-top: 4px;
}

.main {
    border: 1px solid var(--stroke);
    border-radius: 16px;
    background: var(--panel);
    padding: 18px;
    box-shadow: 0 18px 40px rgba(0, 0, 0, 0.32);
}

.course-head {
    display: flex;
    justify-content: space-between;
    align-items: center;
    gap: 12px;
    border-bottom: 1px solid var(--stroke);
    padding-bottom: 12px;
}

.course-head h2 {
    margin: 4px 0;
}

.metrics {
    display: flex;
    gap: 12px;
}

.metric {
    min-width: 120px;
    padding: 10px 12px;
    border: 1px solid var(--stroke);
    border-radius: 12px;
    background: var(--panel-strong);
    text-align: right;
}

.metric strong {
    display: block;
    font-size: 20px;
}

.player-grid {
    margin-top: 16px;
    display: grid;
    gap: 16px;
    grid-template-columns: 1.1fr 0.9fr;
}

.panel {
    border: 1px solid var(--stroke);
    border-radius: 16px;
    padding: 14px;
    background: rgba(255, 255, 255, 0.03);
}

.panel-head {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 10px;
    margin-bottom: 10px;
}

.video {
    width: 100%;
    border-radius: 12px;
    border: 1px solid var(--stroke);
    background: #000;
    min-height: 260px;
}

.ppt-frame {
    border: 1px solid var(--stroke);
    border-radius: 12px;
    overflow: hidden;
    background: #0a0f1f;
    height: 260px;
}

.ppt-frame img {
    width: 100%;
    height: 100%;
    object-fit: cover;
    display: block;
}

.ppt-controls {
    margin-top: 10px;
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 8px;
}

.ppt-controls button {
    background: var(--panel-strong);
    color: var(--text);
    border: 1px solid var(--stroke);
    padding: 8px 12px;
    border-radius: 10px;
    cursor: pointer;
}

.ppt-controls button:disabled {
    opacity: 0.4;
    cursor: not-allowed;
}

.stepper {
    color: var(--muted);
}

.thumbs {
    margin-top: 10px;
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(120px, 1fr));
    gap: 8px;
}

.thumb {
    border: 1px solid var(--stroke);
    border-radius: 10px;
    background: var(--panel-strong);
    padding: 6px;
    text-align: left;
    color: var(--text);
    cursor: pointer;
    transition: border-color 0.2s ease;
}

.thumb.active {
    border-color: var(--accent);
}

.thumb img {
    width: 100%;
    height: 70px;
    object-fit: cover;
    border-radius: 6px;
    margin-bottom: 4px;
}

.info-grid {
    margin-top: 16px;
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: 16px;
}

.keypoints {
    list-style: none;
    padding: 0;
    margin: 0;
    display: grid;
    gap: 8px;
}

.keypoints li {
    padding: 10px 12px;
    border: 1px solid var(--stroke);
    border-radius: 10px;
    background: var(--panel-strong);
}

.quiz-head {
    align-items: center;
}

.quiz-summary {
    display: flex;
    justify-content: space-between;
    color: var(--muted);
    margin-bottom: 8px;
}

.question {
    border: 1px solid var(--stroke);
    border-radius: 12px;
    padding: 10px 12px;
    background: var(--panel-strong);
    margin-bottom: 10px;
}

.question-title {
    margin-bottom: 8px;
    font-weight: 600;
}

.options {
    display: grid;
    gap: 8px;
}

.option {
    display: flex;
    align-items: center;
    gap: 8px;
    border: 1px solid var(--stroke);
    border-radius: 10px;
    padding: 8px 10px;
    cursor: pointer;
}

.option input {
    accent-color: var(--accent);
}

.option-text {
    flex: 1;
}

.option.is-correct {
    border-color: var(--good);
    background: rgba(52, 211, 153, 0.1);
}

.option.is-answer {
    border-color: var(--accent);
}

.option.is-wrong {
    border-color: var(--warn);
    background: rgba(251, 191, 36, 0.08);
}

.feedback {
    margin-top: 8px;
    border-radius: 8px;
    padding: 8px 10px;
    border: 1px solid var(--stroke);
}

.feedback.good {
    border-color: var(--good);
}

.feedback.warn {
    border-color: var(--warn);
}

.explain {
    color: var(--muted);
    margin: 4px 0 0;
}

.empty {
    text-align: center;
    color: var(--muted);
}

.eyebrow {
    text-transform: uppercase;
    letter-spacing: 0.08em;
    font-size: 12px;
    color: var(--muted);
    margin: 0;
}

.pill {
    display: inline-flex;
    align-items: center;
    gap: 6px;
    padding: 6px 10px;
    border-radius: 20px;
    background: rgba(110, 231, 255, 0.08);
    border: 1px solid rgba(110, 231, 255, 0.26);
    color: var(--text);
    font-size: 13px;
}

.pill-sm {
    display: inline-flex;
    align-items: center;
    padding: 4px 8px;
    border-radius: 12px;
    border: 1px solid var(--stroke);
    background: var(--panel-strong);
    color: var(--muted);
    font-size: 12px;
}

.pill-sm.ghost {
    background: transparent;
}

.pill-accent {
    background: rgba(110, 231, 255, 0.16);
    border-color: var(--accent);
}

.badge {
    display: inline-flex;
    padding: 4px 8px;
    border-radius: 10px;
    background: rgba(110, 231, 255, 0.14);
    border: 1px solid rgba(110, 231, 255, 0.32);
    font-size: 12px;
    color: var(--text);
}

.duration {
    color: var(--muted);
}

.tag-row {
    display: flex;
    gap: 8px;
    flex-wrap: wrap;
}

.primary {
    background: linear-gradient(120deg, #22d3ee, #6ee7ff);
    border: none;
    color: #03111a;
    font-weight: 700;
    padding: 8px 14px;
    border-radius: 12px;
    cursor: pointer;
    box-shadow: 0 12px 30px rgba(110, 231, 255, 0.28);
}

.primary:hover {
    transform: translateY(-1px);
}

.chip {
    border-radius: 10px;
    padding: 2px 6px;
    font-size: 11px;
    border: 1px solid var(--stroke);
}

.chip.good {
    border-color: var(--good);
    color: var(--good);
}

.chip.warn {
    border-color: var(--warn);
    color: var(--warn);
}

@media (max-width: 1024px) {
    .layout {
        grid-template-columns: 1fr;
    }

    .player-grid,
    .info-grid {
        grid-template-columns: 1fr;
    }

    .metrics {
        width: 100%;
        justify-content: flex-start;
    }
}
</style>
