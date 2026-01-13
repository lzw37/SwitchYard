import axios from "axios";
import type {
    InternalAxiosRequestConfig,
    AxiosResponse,
    AxiosError,
} from "axios";
import { ElMessage } from "element-plus";
import config from "../config.json";

// 配置全局axios默认设置
axios.defaults.baseURL = config.serverurl;
axios.defaults.timeout = 15000;
axios.defaults.headers.common["Content-Type"] = "application/json";

// 请求拦截器：自动添加JWT Token
axios.interceptors.request.use(
    (config: InternalAxiosRequestConfig) => {
        // 从localStorage获取token
        const token = localStorage.getItem("token");
        const tokenType = localStorage.getItem("tokenType") || "Bearer";

        // 如果token存在，添加到请求头
        if (token && config.headers) {
            config.headers.Authorization = `${tokenType} ${token}`;
        }

        return config;
    },
    (error: AxiosError) => {
        console.error("请求错误:", error);
        return Promise.reject(error);
    }
);

// 响应拦截器：统一处理响应和错误
axios.interceptors.response.use(
    (response: AxiosResponse) => {
        return response;
    },
    (error: AxiosError) => {
        if (error.response) {
            switch (error.response.status) {
                case 401:
                    // Token过期或无效
                    // ElMessage.error("登录已过期，请重新登录");
                    localStorage.removeItem("token");
                    localStorage.removeItem("tokenType");
                    localStorage.removeItem("username");
                    localStorage.removeItem("role");
                    // 跳转到登录页
                    setTimeout(() => {
                        window.location.href = "/login";
                    }, 2000);
                    break;
                case 403:
                    ElMessage.error("没有权限访问该资源");
                    break;
                case 404:
                    ElMessage.error("请求的资源不存在");
                    break;
                case 500:
                    ElMessage.error("服务器内部错误");
                    break;
                default:
                    ElMessage.error(
                        (error.response.data as any)?.message || "请求失败"
                    );
            }
        } else if (error.request) {
            ElMessage.error("网络错误，请检查网络连接");
        } else {
            ElMessage.error("请求配置错误");
        }

        return Promise.reject(error);
    }
);

export default axios;
