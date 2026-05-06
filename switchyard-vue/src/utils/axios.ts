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

interface RefreshResponse {
    token: string;
    tokenType?: string;
    expiresIn?: number;
    refreshToken?: string;
    refreshTokenExpiresIn?: number;
}

interface RetriableRequestConfig extends InternalAxiosRequestConfig {
    _retry?: boolean;
    skipAuthRefresh?: boolean;
}

const authStore = useAuthStore(pinia);
const REFRESH_BUFFER_SECONDS = 60;
const refreshClient = axios.create({
    baseURL: config.serverurl,
    timeout: 15000,
    headers: {
        "Content-Type": "application/json",
    },
});

let refreshPromise: Promise<string> | null = null;

axios.defaults.baseURL = config.serverurl;
axios.defaults.timeout = 15000;
axios.defaults.headers.common["Content-Type"] = "application/json";

const isLoginRequest = (url?: string) => url?.includes("/api/Auth/login") === true;
const isRefreshRequest = (url?: string) =>
    url?.includes("/api/Auth/refresh") === true;

const shouldSkipRefresh = (requestConfig?: {
    url?: string;
    skipAuthRefresh?: boolean;
}) =>
    requestConfig?.skipAuthRefresh === true ||
    isLoginRequest(requestConfig?.url) ||
    isRefreshRequest(requestConfig?.url);

const isTimeoutError = (error: AxiosError) =>
    error.code === "ECONNABORTED" ||
    error.message.toLowerCase().includes("timeout");

const getTimeoutMessage = () =>
    String(i18n.global.locale.value).startsWith("zh")
        ? "请求超时，请稍后重试"
        : "Request timed out, please try again later";

const redirectToLogin = () => {
    authStore.clearAuth();

    const currentPath = window.location.pathname.replace(/\/+$/, "");
    if (!currentPath.endsWith("/login")) {
        window.location.replace("/login");
    }
};

const setAuthorizationHeader = (
    requestConfig: InternalAxiosRequestConfig,
    token: string,
    tokenType: string,
) => {
    if (requestConfig.headers) {
        requestConfig.headers.Authorization = `${tokenType} ${token}`;
    }
};

const refreshAccessToken = async (): Promise<string> => {
    if (!refreshPromise) {
        authStore.hydrateFromStorage();

        if (authStore.isRefreshTokenExpired()) {
            throw new Error("Refresh token expired");
        }

        refreshPromise = refreshClient
            .post<RefreshResponse>(
                "/api/Auth/refresh",
                { refreshToken: authStore.refreshToken },
            )
            .then(({ data }) => {
                if (!data.token) {
                    throw new Error("Refresh response missing access token");
                }

                authStore.setAuth({
                    token: data.token,
                    tokenType: data.tokenType || "Bearer",
                    expiresIn: data.expiresIn,
                    refreshToken: data.refreshToken,
                    refreshTokenExpiresIn: data.refreshTokenExpiresIn,
                    username: authStore.username,
                    role: authStore.role,
                    mustChangePassword: authStore.mustChangePassword,
                });

                return data.token;
            })
            .finally(() => {
                refreshPromise = null;
            });
    }

    return refreshPromise;
};

// Attach JWT token from Pinia before each request.
axios.interceptors.request.use(
    async (config: InternalAxiosRequestConfig) => {
        authStore.hydrateFromStorage();

        if (
            !shouldSkipRefresh(config) &&
            authStore.token &&
            authStore.isTokenExpired(REFRESH_BUFFER_SECONDS) &&
            !authStore.isRefreshTokenExpired()
        ) {
            try {
                await refreshAccessToken();
                authStore.hydrateFromStorage();
            } catch (error) {
                redirectToLogin();
                return Promise.reject(error);
            }
        }

        if (authStore.token) {
            setAuthorizationHeader(
                config,
                authStore.token,
                authStore.tokenType || "Bearer",
            );
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
    async (error: AxiosError) => {
        const originalRequest = error.config as RetriableRequestConfig | undefined;

        if (error.response) {
            switch (error.response.status) {
                case 401: {
                    if (
                        originalRequest &&
                        !originalRequest._retry &&
                        !shouldSkipRefresh(originalRequest)
                    ) {
                        authStore.hydrateFromStorage();

                        if (!authStore.isRefreshTokenExpired()) {
                            try {
                                originalRequest._retry = true;
                                await refreshAccessToken();
                                authStore.hydrateFromStorage();
                                setAuthorizationHeader(
                                    originalRequest,
                                    authStore.token,
                                    authStore.tokenType || "Bearer",
                                );
                                return axios(originalRequest);
                            } catch (refreshError) {
                                redirectToLogin();
                                return Promise.reject(refreshError);
                            }
                        }
                    }

                    redirectToLogin();
                    return Promise.reject(error);
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
        } else if (isTimeoutError(error)) {
            ElMessage.error(getTimeoutMessage());
        } else if (error.request) {
            ElMessage.error(i18n.global.t("axios.networkError") as string);
        } else {
            ElMessage.error(i18n.global.t("axios.requestConfigError") as string);
        }

        return Promise.reject(error);
    },
);

export default axios;
