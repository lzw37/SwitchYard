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
            alias: [
                {
                    find: /^vue$/,
                    replacement: fileURLToPath(
                        new URL("./node_modules/vue/dist/vue.runtime.esm-bundler.js", import.meta.url),
                    ),
                },
                {
                    find: /^element-plus$/,
                    replacement: fileURLToPath(
                        new URL("./node_modules/element-plus/es/index.mjs", import.meta.url),
                    ),
                },
                {
                    find: /^@element-plus\/icons-vue$/,
                    replacement: fileURLToPath(
                        new URL("./node_modules/@element-plus/icons-vue/dist/index.js", import.meta.url),
                    ),
                },
                {
                    find: /^@switchyard\/station-layout$/,
                    replacement: fileURLToPath(
                        new URL(
                            "../SwitchYard.StationLayout/frontend/src/index.ts",
                            import.meta.url,
                        ),
                    ),
                },
                {
                    find: "@",
                    replacement: fileURLToPath(new URL("./src", import.meta.url)),
                },
            ],
            dedupe: ["vue", "element-plus", "@element-plus/icons-vue"],
        },
        // 生产环境构建优化
        build: {
            // 构建输出目录
            outDir: "dist",
            // 确保资源文件路径正确
            assetsDir: "assets",
            // 禁用生产环境 source map，避免源码泄露
            sourcemap: mode !== "production",
            chunkSizeWarningLimit: 1000,
            rollupOptions: {
                output: {
                    manualChunks: {
                        vue: ["vue", "vue-router", "pinia", "vue-i18n"],
                        elementPlus: ["element-plus", "@element-plus/icons-vue"],
                        pdf: ["pdfjs-dist"],
                    },
                },
            },
        },
        // 生产构建时移除console.log
        esbuild:
            mode === "production"
                ? {
                      drop: ["console", "debugger"],
                  }
                : undefined,
        // 开发服务器配置
        server: {
            host: "0.0.0.0",
            port: 5173,
            strictPort: true,
            // 开发环境下的代理配置（如果需要）
            proxy:
                mode === "development"
                    ? {
                          "/api": {
                              target: "http://127.0.0.1:7297",
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
