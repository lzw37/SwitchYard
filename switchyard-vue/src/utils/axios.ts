import axios from "axios";
import type {
    InternalAxiosRequestConfig,
    AxiosError,
    AxiosResponse,
} from "axios";
import { ElMessage } from "element-plus";
import { i18n } from "../i18n";
import config from "../config";
import pinia from "@/stores";
import { useAuthStore } from "@/stores/auth";

const authStore = useAuthStore(pinia);

axios.defaults.baseURL = config.serverurl;
axios.defaults.timeout = 15000;
axios.defaults.headers.common["Content-Type"] = "application/json";

// Attach JWT token from Pinia before each request.
axios.interceptors.request.use(
    (config: InternalAxiosRequestConfig) => {
        authStore.hydrateFromStorage();

        const token = authStore.token;
        const tokenType = authStore.tokenType || "Bearer";

        if (token && config.headers) {
            config.headers.Authorization = `${tokenType} ${token}`;
        }

        return config;
    },
    (error: AxiosError) => {
        console.error(i18n.global.t("axios.requestError"), error);
        return Promise.reject(error);
    },
);

// Handle auth and common HTTP errors in one place.
axios.interceptors.response.use(
    (response: AxiosResponse) => response,
    (error: AxiosError) => {
        if (error.response) {
            switch (error.response.status) {
                case 401: {
                    authStore.clearAuth();
                    const currentPath = window.location.pathname.replace(/\/+$/, "");
                    if (!currentPath.endsWith("/login")) {
                        window.location.replace("/login");
                    }
                    break;
                }
                case 403:
                    ElMessage.error(i18n.global.t("axios.noPermission") as string);
                    break;
                case 404:
                    ElMessage.error(i18n.global.t("axios.notFound") as string);
                    break;
                case 500:
                    ElMessage.error(i18n.global.t("axios.serverError") as string);
                    break;
                default:
                    ElMessage.error(
                        (error.response.data as any)?.message ||
                            (i18n.global.t("axios.requestFailed") as string),
                    );
            }
        } else if (error.request) {
            ElMessage.error(i18n.global.t("axios.networkError") as string);
        } else {
            ElMessage.error(i18n.global.t("axios.requestConfigError") as string);
        }

        return Promise.reject(error);
    },
);

export default axios;
