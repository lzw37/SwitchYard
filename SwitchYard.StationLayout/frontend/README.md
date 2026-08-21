# @switchyard/station-layout

共享的站场图定义前端模块。模块不包含 HTTP、认证或路由实现；宿主必须提供完整的 `StationLayoutGateway`。

```ts
import { StationLayout } from "@switchyard/station-layout";
import type { StationLayoutGateway } from "@switchyard/station-layout";
import "@switchyard/station-layout/style.css";
```

`gateway` 是必填属性，并由 TypeScript 强制为 `StationLayoutGateway`。`translate` 与 `formatError` 可选；不提供 `translate` 时使用内置中文。`readonly` 为 `true` 时，模块只允许查看、选择、缩放、加载和导出，不允许绘图、编辑、导入、方案管理、保存或 DWG 提取。

宿主还需安装并全局注册 Vue 3 与 Element Plus。离线分发使用 `npm run build` 后生成的 `npm pack` 包；样式必须通过上面的显式 CSS import 引入。`./style.css` 同时在 package exports 中声明了 CSS 文件和 `style.d.ts` 类型入口。
