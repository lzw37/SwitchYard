<template>
  <section class="quiz-shell">
    <div class="quiz-top">
      <div class="quiz-headline">
        <h3>{{ t('course.quiz.title') }}</h3>
        <div class="quiz-summary">
          <span v-if="loading">{{ t('course.quiz.loading') }}</span>
          <template v-else>
            <span>{{ t('course.quiz.answered', { answered: answeredCount, total: items.length }) }}</span>
            <span v-if="gradeResult.graded">{{ t('course.quiz.score', { correct: gradeResult.correct, total: items.length }) }}</span>
          </template>
        </div>
      </div>

      <div class="quiz-action">
        <ActionButton :label="t('course.quiz.grade')" :icon="actionIcons.save" :disabled="loading || items.length === 0" @click="grade" />
      </div>
    </div>

    <div v-if="loadError" class="muted">{{ loadError }}</div>
    <div v-else-if="!loading && items.length === 0" class="muted">{{ t('course.quiz.empty') }}</div>

    <div class="question-list">
      <div v-for="(it, idx) in items" :key="it.id" class="question">
        <div class="question-title">{{ idx + 1 }}. {{ it.question }}</div>

        <div class="blank-row">
          <input
            v-model="userInputs[it.id]"
            class="blank-input"
            type="text"
            :placeholder="t('course.quiz.answerPlaceholder')" :aria-label="t('course.quiz.answerPlaceholder')"
          />
        </div>

        <div v-if="gradeResult.graded" class="feedback" :class="feedbackClass(it.id)">
          <div class="feedback-line">
            <span class="badge" :class="feedbackBadgeClass(it.id)">{{ feedbackText(it.id) }}</span>
            <span class="muted">{{ t('course.quiz.similarity', { percent: formatPercent(gradeResult.details[it.id]?.similarity ?? 0) }) }}</span>
          </div>
          <p class="explain">{{ t('course.quiz.answer') }}: <span class="answer">{{ it.answer }}</span></p>
        </div>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { computed, reactive, ref, watch } from "vue";
import { useI18n } from 'vue-i18n';
import ActionButton from '@/components/ui/ActionButton.vue';
import { actionIcons } from '@/components/ui/actionIcons';

const { t } = useI18n();

const props = defineProps<{
  quizDocUrl: string;
  cacheKey: string;
}>();

type FillBlankItem = { id: string; question: string; answer: string };
type GradeLevel = "correct" | "partial" | "wrong";
const BLANK_PLACEHOLDER = "＿＿＿＿＿";

const items = ref<FillBlankItem[]>([]);
const loading = ref(false);
const loadError = ref("");

const userInputs = reactive<Record<string, string>>({});
const gradeResult = reactive<{
  graded: boolean;
  correct: number;
  details: Record<string, { similarity: number; level: GradeLevel }>;
}>({
  graded: false,
  correct: 0,
  details: {}
});

const docCache = new Map<string, Promise<string>>();

watch(
  () => props.quizDocUrl,
  () => {
    void loadQuiz();
  },
  { immediate: true }
);

const answeredCount = computed(() => items.value.filter((it) => (userInputs[it.id] ?? "").trim().length > 0).length);

function formatPercent(value: number) {
  const v = Number(value);
  if (Number.isNaN(v)) return "0.0";
  return (v * 100).toFixed(1);
}

async function loadQuiz() {
  items.value = [];
  loadError.value = "";
  gradeResult.graded = false;
  gradeResult.correct = 0;
  gradeResult.details = {};

  const url = props.quizDocUrl?.trim();
  if (!url) return;

  loading.value = true;
  try {
    const text = await readDocAsText(url);
    const parsed = parseFillBlankQuiz(text);
    items.value = parsed;
    for (const it of parsed) {
      if (userInputs[it.id] === undefined) userInputs[it.id] = "";
    }
  } catch (error) {
    loadError.value = t('course.quiz.loadFailed', { error: error instanceof Error ? error.message : t('common.unknownError') });
  } finally {
    loading.value = false;
  }
}

async function readDocAsText(url: string) {
  if (!docCache.has(url)) {
    docCache.set(
      url,
      (async () => {
        const response = await fetch(url);
        if (!response.ok) return "";
        const buffer = await response.arrayBuffer();
        return extractTextFromDocBuffer(buffer);
      })()
    );
  }
  return (await docCache.get(url)) ?? "";
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
  const text = decodeBytes(run, "utf-16le").replace(/\0/g, "").trim();
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

function parseFillBlankQuiz(rawText: string): FillBlankItem[] {
  const text = normalizeRawText(rawText);
  if (!text) return [];

  const lines = text
    .split(/\r\n|\r|\n/)
    .map((line) => line.trim())
    .filter((line) => line.length > 0);
  if (lines.length === 0) return [];

  // 答案开始标记：答案: / 参考答案:
  const answerStartRegex = /(参考)?答案\s*[:：]?/;

  const results: FillBlankItem[] = [];
  let questionBuffer = "";
  let answerBuffer = "";
  let mode: "question" | "answer" = "question";
  let order = 0;

  const flushCurrent = () => {
    const question = cleanQuestion(questionBuffer);
    const answer = cleanAnswer(answerBuffer);
    if (!question || !answer) return;
    if (looksGarbled(question) || looksGarbled(answer)) return;

    const item = {
      id: `${props.cacheKey}-${order}-${hashLite(`${question}|${answer}`)}`,
      question,
      answer
    };
    order += 1;
    results.push(item);
  };

  for (let i = 0; i < lines.length; i += 1) {
    const line = lines[i] ?? "";
    if (!line) continue;
    if (looksGarbled(line)) break;

    if (mode === "question") {
      if (answerStartRegex.test(line)) {
        mode = "answer";
        const answerLine = line.replace(answerStartRegex, "").trim();
        if (answerLine) answerBuffer += `${answerLine} `;
        continue;
      }

      questionBuffer += `${line} `;
      continue;
    }

    if (answerStartRegex.test(line)) {
      const answerLine = line.replace(answerStartRegex, "").trim();
      if (answerLine) answerBuffer += `${answerLine} `;
      continue;
    }

    // 答案进入后，只有“(数字)”开头的行继续归属当前答案；否则视为下一题开始。
    if (isAnswerContinuationLine(line)) {
      answerBuffer += `${line} `;
      continue;
    }

    flushCurrent();
    questionBuffer = `${line} `;
    answerBuffer = "";
    mode = "question";
  }

  flushCurrent();

  const dedup = new Set<string>();
  return results.filter((it) => {
    const key = `${it.question}||${it.answer}`;
    if (dedup.has(key)) return false;
    dedup.add(key);
    return true;
  });
}

function cleanQuestion(input: string) {
  return input
    .replace(/\u3000/g, " ")
    .replace(/ {2,}/g, BLANK_PLACEHOLDER)
    .replace(/\s*([、，。；：,.;:!?])\s*/g, "$1")
    .replace(/\s+/g, " ")
    .trim();
}

function cleanAnswer(input: string) {
  return input.replace(/\s+/g, " ").replace(/[。；;]+$/g, "").trim();
}

function normalizeRawText(input: string) {
  return (input ?? "")
    .replace(/\r\n/g, "\n")
    .replace(/\u00a0/g, " ")
    .replace(/\s+\n/g, "\n")
    .trim();
}

function isAnswerContinuationLine(line: string) {
  return /^\s*[（(]\d+\s*[)）]/.test(line);
}

function looksGarbled(segment: string) {
  const s = (segment ?? "").trim();
  if (!s) return true;
  if (isKnownMojibake(s)) return true;
  const normalized = s.replace(/＿+/g, "_");

  const noisy = (normalized.match(/[^\u4e00-\u9fffA-Za-z0-9_，。；：、（）()【】《》“”‘’:,.!?%\- ]/g) ?? []).length;
  if (noisy > 0 && noisy / normalized.length > 0.15) return true;

  const rareCjk = (normalized.match(/[\u3400-\u4dbf]/g) ?? []).length;
  return rareCjk >= 2 && rareCjk / normalized.length > 0.08;
}

function isKnownMojibake(text: string) {
  // 常见于 doc 解码异常时的乱码片段
  return /(漀挀甀洀攀渀琀|匀甀洀洀愀爀礀|䤀渀)/.test(text);
}

function hashLite(input: string) {
  let h = 2166136261;
  for (let i = 0; i < input.length; i += 1) {
    h ^= input.charCodeAt(i);
    h = Math.imul(h, 16777619);
  }
  return (h >>> 0).toString(16);
}

function grade() {
  gradeResult.graded = true;
  gradeResult.correct = 0;
  gradeResult.details = {};

  for (const it of items.value) {
    const user = (userInputs[it.id] ?? "").trim();
    const standard = (it.answer ?? "").trim();

    const sim = similarity(user, standard);
    const level: GradeLevel = sim >= 0.9 ? "correct" : sim >= 0.5 ? "partial" : "wrong";

    gradeResult.details[it.id] = { similarity: sim, level };
    if (level === "correct") gradeResult.correct += 1;
  }
}

function similarity(a: string, b: string) {
  const A = normalizeForCompare(a);
  const B = normalizeForCompare(b);
  if (!A || !B) return 0;
  if (A === B) return 1;

  if (A.length < 2 || B.length < 2) {
    const setA = new Set(A.split(""));
    const setB = new Set(B.split(""));
    let inter = 0;
    setA.forEach((ch) => {
      if (setB.has(ch)) inter += 1;
    });
    const uni = setA.size + setB.size - inter;
    return uni === 0 ? 0 : inter / uni;
  }

  const gramsA = toBigrams(A);
  const gramsB = toBigrams(B);
  const counter = new Map<string, number>();
  gramsA.forEach((g) => counter.set(g, (counter.get(g) ?? 0) + 1));

  let inter = 0;
  gramsB.forEach((g) => {
    const c = counter.get(g) ?? 0;
    if (c > 0) {
      inter += 1;
      counter.set(g, c - 1);
    }
  });

  return (2 * inter) / (gramsA.length + gramsB.length);
}

function normalizeForCompare(s: string) {
  return (s ?? "")
    .toLowerCase()
    .replace(/\s+/g, "")
    .replace(/[，。；：、,.!?;:'"“”‘’（）()\[\]【】《》<>/\\|_\-]/g, "")
    .trim();
}

function toBigrams(s: string) {
  const out: string[] = [];
  for (let i = 0; i < s.length - 1; i += 1) out.push(s.slice(i, i + 2));
  return out;
}

function feedbackClass(id: string) {
  const level = gradeResult.details[id]?.level;
  return level === "correct" ? "good" : level === "partial" ? "warn" : "bad";
}

function feedbackBadgeClass(id: string) {
  return feedbackClass(id);
}

function feedbackText(id: string) {
  const level = gradeResult.details[id]?.level;
  if (level === "correct") return t('course.quiz.correct');
  if (level === "partial") return t('course.quiz.partial');
  return t('course.quiz.wrong');
}
</script>

<style scoped>
.quiz-shell { margin-top: 16px; border: 1px solid var(--stroke, #dfe4ea); border-radius: 6px; padding: 12px; background: var(--sy-surface, #fff); }
.quiz-headline h3 { margin: 0; font-size: 14px; font-weight: 600; }
.quiz-top { display: flex; align-items: flex-start; justify-content: space-between; gap: 12px; }
.quiz-summary { display: flex; flex-wrap: wrap; gap: 12px; margin: 6px 0; color: var(--muted, #909399); font-size: 12px; }
.quiz-action { display: flex; align-items: center; }
.muted { color: var(--muted, #909399); }
.question-list { margin-top: 8px; }
.question { padding: 12px 0; border-top: 1px solid var(--stroke, #dfe4ea); }
.question-title { margin-bottom: 8px; font-weight: 500; }
.blank-row { margin-top: 8px; }
.blank-input { box-sizing: border-box; width: 100%; border: 1px solid var(--stroke, #dfe4ea); border-radius: 4px; padding: 8px 10px; background: var(--sy-surface, #fff); color: var(--text, #303133); font: inherit; outline: none; }
.blank-input:focus { border-color: var(--accent, #4776a8); }
.feedback { margin-top: 10px; border-left: 2px solid var(--stroke, #dfe4ea); padding: 4px 10px; }
.feedback.good { border-color: var(--good, #529b64); }
.feedback.warn { border-color: var(--warn, #b98739); }
.feedback.bad { border-color: var(--el-color-danger, #c45656); }
.feedback-line { display: flex; gap: 10px; align-items: center; justify-content: space-between; flex-wrap: wrap; font-size: 12px; }
.answer { font-weight: 500; }
.explain { margin: 6px 0; }
.badge.good { color: var(--good, #529b64); }
.badge.warn { color: var(--warn, #b98739); }
.badge.bad { color: var(--el-color-danger, #c45656); }
</style>
