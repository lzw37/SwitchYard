# @switchyard/station-layout

共享的站场图定义前端模块。模块不包含 HTTP、认证或路由实现；宿主必须提供完整的 `StationLayoutGateway`。

```ts
import { StationLayout } from "@switchyard/station-layout";
import type { StationLayoutGateway } from "@switchyard/station-layout";
import "@switchyard/station-layout/style.css";
```

`gateway` 是必填属性，并由 TypeScript 强制为 `StationLayoutGateway`。`translate` 与 `formatError` 可选；不提供 `translate` 时使用内置中文。`readonly` 为 `true` 时，模块只允许查看、选择、缩放、加载和导出，不允许绘图、编辑、导入、方案管理、保存或 DWG 提取。

车站方案成功加载并渲染后自动执行一次“显示全图”，首次加载、切换方案和重新载入均适用。隐藏标签页会等图面容器可见后再适配；执行完毕后保留用户的手动缩放，不随普通尺寸变化反复重置。

绘制直线、站台等需要多次点击的对象时，按 Esc 丢弃当前未完成图形，保留绘图模式和当前工具，下次点击从新的第一点开始。已完成的对象不受影响，取消草稿不产生撤销记录。

车站方案管理中，“新建”先在表格插入一行待保存方案；输入名称并点击“保存”后才调用后端创建接口，方案 ID 由宿主生成。“取消”或关闭对话框会丢弃未保存行。每个已保存方案提供“复制”按钮，通过 `copyStationScheme` Gateway 方法生成独立副本并切换到副本。

选择模式下，双击线路／股道、信号机、道岔、站台的图元或名称可就地重命名；双击轨道电路名称或文字标注也可直接编辑。Enter 或点击别处确认，Esc 取消，确认后通过“保存数据”持久化。设备及文字标注编辑沿用图面的撤销／重做；轨道电路编辑同步侧栏表单。只读、绘图和进路起终点拾取模式不启用重命名。重命名只更改已有名称或标注文字，不更改对象 ID、绑定或拓扑；没有名称字段的节点、曲线、钢轨绝缘和车挡不提供此操作。

仅使用 `StationLayoutEditor` 的宿主需监听 `cell-rename`（`{ id, name }`）更新自己持有的 `cells` 和相关表单，完整 `StationLayout` 已包含接线。浏览器检查页为主应用 `/dev/station-layout-rename.html`，使用完整共享组件和内存 Gateway，可验证双击、确认／取消、保存后载入而不写入用户数据库。

宿主还需安装并全局注册 Vue 3 与 Element Plus。离线分发使用 `npm run build` 后生成的 `npm pack` 包；样式必须通过上面的显式 CSS import 引入。`./style.css` 同时在 package exports 中声明了 CSS 文件和 `style.d.ts` 类型入口。
