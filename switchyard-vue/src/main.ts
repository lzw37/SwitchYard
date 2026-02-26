import "./assets/main.css";

import { createApp } from "vue";
import App from "./App.vue";
import router from "./router";
import pinia from "./stores";
import { useAuthStore } from "./stores/auth";
import ElementPlus from "element-plus";
import "element-plus/dist/index.css";
import "./utils/axios"; // Initialize axios interceptors globally.
import i18n from "./i18n";

const app = createApp(App);
const authStore = useAuthStore(pinia);

authStore.hydrateFromStorage();

app.use(pinia);
app.use(router);
app.use(ElementPlus);
app.use(i18n);
app.mount("#app");
