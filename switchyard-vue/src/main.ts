import "./assets/main.css";

import { createApp } from "vue";
import App from "./App.vue";
import router from "./router";
import ElementPlus from "element-plus";
import "element-plus/dist/index.css";
import "./utils/axios"; // 导入axios配置，使全局axios生效

const app = createApp(App);

app.use(router);
app.use(ElementPlus);
app.mount("#app");
