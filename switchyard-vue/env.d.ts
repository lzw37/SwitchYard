/// <reference types="vite/client" />

interface Window {
    __SWITCHYARD_RUNTIME_CONFIG__?: {
        // Empty means the browser's current origin; explicit values must be HTTP(S) origins.
        apiBaseUrl?: string;
    };
}

declare module "*.vue" {
    import type { DefineComponent } from "vue";
    const component: DefineComponent<{}, {}, any>;
    export default component;
}
