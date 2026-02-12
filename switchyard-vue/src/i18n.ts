import { createI18n } from "vue-i18n";
import zh from "./locales/zh.json";
import en from "./locales/en.json";

const messages = {
    zh,
    en,
};

function getDefaultLocale(): "en" | "zh" {
    const hasWindow = typeof window !== "undefined";
    const saved =
        hasWindow && window.localStorage
            ? window.localStorage.getItem("locale")
            : null;
    if (saved === "en" || saved === "zh") return saved;

    // Prefer navigator.languages (array of user preferred languages)
    const langs =
        hasWindow &&
        navigator &&
        navigator.languages &&
        navigator.languages.length
            ? navigator.languages
            : hasWindow && navigator && navigator.language
              ? [navigator.language]
              : ["en"];

    for (const l of langs) {
        if (!l) continue;
        const lower = String(l).toLowerCase();
        if (lower.startsWith("zh")) return "zh";
        if (lower.startsWith("en")) return "en";
    }

    // fallback
    return "en";
}

const i18n = createI18n({
    legacy: false,
    locale: getDefaultLocale(),
    fallbackLocale: "en",
    messages,
});

// If user hasn't chosen a locale (no saved), update when system/browser language changes
if (
    !localStorage.getItem("locale") &&
    typeof window !== "undefined" &&
    "onlanguagechange" in window
) {
    window.addEventListener("languagechange", () => {
        try {
            const newLocale = getDefaultLocale();
            i18n.global.locale.value = newLocale;
        } catch (e) {
            // ignore
        }
    });
}

export default i18n;
export { i18n };
