import { fileURLToPath, URL } from "node:url";

import { defineConfig } from "vite";
import vue from "@vitejs/plugin-vue";
import vueDevTools from "vite-plugin-vue-devtools";

// https://vite.dev/config/
export default defineConfig(({ mode }) => {
    return {
        plugins: [
            vue(),
            // 只在开发环境使用devtools
            ...(mode === "development" ? [vueDevTools()] : []),
        ],
        resolve: {
            alias: {
                "@": fileURLToPath(new URL("./src", import.meta.url)),
            },
        },
        // 生产环境构建优化
        build: {
            // 生产构建时移除console.log
            terserOptions:
                mode === "production"
                    ? {
                          compress: {
                              drop_console: true,
                              drop_debugger: true,
                          },
                      }
                    : undefined,
            // 构建输出目录
            outDir: "dist",
            // 确保资源文件路径正确
            assetsDir: "assets",
        },
        // 开发服务器配置
        server: {
            host: "0.0.0.0",
            port: 5173,
            // 开发环境下的代理配置（如果需要）
            proxy:
                mode === "development"
                    ? {
                          "/api": {
                              target: "http://localhost:5000",
                              changeOrigin: true,
                              secure: false,
                          },
                      }
                    : undefined,
        },
        // 预览服务器配置
        preview: {
            port: 4173,
            host: "0.0.0.0",
        },
    };
});
