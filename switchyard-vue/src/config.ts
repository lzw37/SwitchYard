// 配置管理器 - 根据环境自动加载配置
import configDev from "./config.development.json";
import configProd from "./config.production.json";

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
            break;
    }

    // 如果环境变量中有API URL配置，优先使用环境变量
    const envApiUrl = import.meta.env.VITE_API_BASE_URL;
    if (envApiUrl) {
        config = { ...config, serverurl: envApiUrl };
        console.log(`使用环境变量API URL: ${envApiUrl}`);
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
