import { createI18n } from "vue-i18n";
import zh from "./locales/zh.json";
import en from "./locales/en.json";

const messages = {
    zh,
    en,
};

function getDefaultLocale() {
    const saved = localStorage.getItem("locale");
    if (saved) return saved;
    const nav = navigator.language || "en";
    if (nav.startsWith("zh")) return "zh";
    return "en";
}

const i18n = createI18n({
    legacy: false,
    locale: getDefaultLocale(),
    fallbackLocale: "en",
    messages,
});

export default i18n;
export { i18n };
