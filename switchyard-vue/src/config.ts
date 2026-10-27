// 配置管理器 - 根据环境自动加载配置
import configDev from "./config.development.json";
import configProd from "./config.production.json";

// Docker writes this public configuration when the container starts. Use an
// absolute origin because course media and version requests call new URL(path, base).
const getRuntimeApiOrigin = (): string | undefined => {
    if (typeof window === "undefined") return undefined;

    const configuredOrigin = window.__SWITCHYARD_RUNTIME_CONFIG__?.apiBaseUrl;
    if (configuredOrigin === undefined) return undefined;
    if (!configuredOrigin.trim()) return window.location.origin;

    try {
        const apiUrl = new URL(configuredOrigin.trim());
        if (
            !["http:", "https:"].includes(apiUrl.protocol) ||
            apiUrl.username || apiUrl.password || apiUrl.search || apiUrl.hash ||
            apiUrl.pathname !== "/"
        ) {
            throw new Error("API address must be an HTTP(S) origin without a path");
        }
        return apiUrl.origin;
    } catch {
        console.warn("Invalid SWITCHYARD_API_BASE_URL; using the current site origin.");
        return window.location.origin;
    }
};

// 获取当前环境
const getEnvironment = (): "development" | "production" => {
    // 在构建时，Vite会设置import.meta.env.MODE
    const mode = import.meta.env.MODE;

    // 也可以通过检查是否是开发服务器来判断
    const isDev = import.meta.env.DEV;

    if (mode === "production" || !isDev) {
        return "production";
    }

    return "development";
};

// 根据环境选择配置
const getConfig = () => {
    const env = getEnvironment();

    console.log(`当前环境: ${env}`);

    let config: typeof configDev;

    switch (env) {
        case "production":
            config = configProd;
            break;
        case "development":
        default:
            config = configDev;
            // 开发 API 保持 7297 端口，随页面主机名连接，避免更换网络后仍请求旧 IP。
            if (typeof window !== "undefined") {
                const apiUrl = new URL(configDev.serverurl);
                if (["localhost", "127.0.0.1"].includes(apiUrl.hostname)) {
                    apiUrl.hostname = window.location.hostname;
                    config = { ...configDev, serverurl: apiUrl.origin };
                }
            }
            break;
    }

    // Vite 环境变量是构建时配置；Docker 的容器启动配置在下面具有更高优先级。
    const envApiUrl = import.meta.env.VITE_API_BASE_URL;
    if (envApiUrl) {
        config = { ...config, serverurl: envApiUrl };
        console.log(`使用环境变量API URL: ${envApiUrl}`);
    }

    const runtimeApiOrigin = getRuntimeApiOrigin();
    if (runtimeApiOrigin !== undefined) {
        config = { ...config, serverurl: runtimeApiOrigin };
    } else if (!config.serverurl && typeof window !== "undefined") {
        config = { ...config, serverurl: window.location.origin };
    }

    return config;
};

// 导出配置对象
const config = getConfig();

export default config;

// 导出环境相关的工具函数
export const isProduction = () => getEnvironment() === "production";
export const isDevelopment = () => getEnvironment() === "development";
export const getCurrentEnvironment = getEnvironment;

// 导出环境信息用于调试
export const getEnvInfo = () => ({
    mode: import.meta.env.MODE,
    isDev: import.meta.env.DEV,
    isProd: import.meta.env.PROD,
    baseUrl: import.meta.env.BASE_URL,
    apiUrl: import.meta.env.VITE_API_BASE_URL,
    environment: getEnvironment(),
    config: config,
});
