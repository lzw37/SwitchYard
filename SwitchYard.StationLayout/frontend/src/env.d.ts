interface ImportMeta {
  glob<T = unknown>(
    pattern: string | string[],
    options?: {
      eager?: boolean;
      import?: string;
      query?: string | Record<string, string | number | boolean>;
    },
  ): Record<string, T>;
}

declare module "*.vue" {
  import type { DefineComponent } from "vue";
  const component: DefineComponent<Record<string, unknown>, Record<string, unknown>, unknown>;
  export default component;
}
