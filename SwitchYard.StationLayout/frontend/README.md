# @switchyard/station-layout

共享的站场图定义前端模块。模块不包含 HTTP、认证或路由实现；宿主必须提供完整的 `StationLayoutGateway`。

```ts
import { StationLayout } from "@switchyard/station-layout";
import type { StationLayoutGateway } from "@switchyard/station-layout";
import "@switchyard/station-layout/style.css";
```

`gateway` 是必填属性，并由 TypeScript 强制为 `StationLayoutGateway`。`translate` 与 `formatError` 可选；不提供 `translate` 时使用内置中文。`readonly` 为 `true` 时，模块只允许查看、选择、缩放、加载和导出，不允许绘图、编辑、导入、方案管理、保存或 DWG 提取。

“文件 → 导出 JSON / 导入 JSON”使用统一的 `switchyard.station-layout` 格式，`formatVersion` 为 `1`。文件包含线路、曲线、节点、信号机、钢轨绝缘、车挡、站台、道岔、轨道电路、文字标注，以及坐标变换、显示样式、网格、元素 ID 和关联。保留小数精度、元素顺序和扩展字段；未保存的轨道电路也保留临时 ID。旧版无格式标识的布局 JSON 仍可导入。

导入替换当前图面，保持当前实例和车站方案作为保存目标，使用目标方案的版本号；不会根据文件中的来源方案 ID 自动切换方案。点击“保存数据”后才持久化。格式、版本、元素、坐标和关联检查通过后才应用文件，失败保留当前图面。导入及载入会清空旧图的撤销历史。文件保存图面内容，不包含选中状态、临时绘图草稿、滚动位置或撤销栈。

宿主可通过完整 `StationLayout` 组件的 ref 调用同一套方法，也可单独使用纯 JSON 编解码函数：

```ts
import { parseStationLayoutJson, serializeStationLayoutJson } from "@switchyard/station-layout";

const json = layoutRef.value!.exportJson();
await layoutRef.value!.importJson(json); // 本地导入；只读状态拒绝导入
const document = parseStationLayoutJson(json);
const archive = serializeStationLayoutJson(document);
```

单独使用底层 `StationLayoutEditor` 时，先用 `parseStationLayoutJson` 校验，再调用 `loadDataFromJson(document, { preserveDocument: true, resetHistory: true })`；通过 `buildJsonData()` 取得全部图元。完整组件额外负责同步侧栏轨道电路、显示设置及当前方案作用域。离线宿主的 Repository 也必须完整保存文档（含扩展字段），不能仅依赖有精度限制的关系表重建图面。

车站方案成功加载并渲染后自动执行一次“显示全图”，首次加载、切换方案和重新载入均适用。隐藏标签页会等图面容器可见后再适配；执行完毕后保留用户的手动缩放，不随普通尺寸变化反复重置。

绘制直线、站台等需要多次点击的对象时，按 Esc 丢弃当前未完成图形，保留绘图模式和当前工具，下次点击从新的第一点开始。已完成的对象不受影响，取消草稿不产生撤销记录。

车站方案管理中，“新建”先在表格插入一行待保存方案；输入名称并点击“保存”后才调用后端创建接口，方案 ID 由宿主生成。“取消”或关闭对话框会丢弃未保存行。每个已保存方案提供“复制”按钮，通过 `copyStationScheme` Gateway 方法生成独立副本并切换到副本。

选择模式下，双击线路／股道、信号机、道岔、站台的图元或名称可就地重命名；双击轨道电路名称或文字标注也可直接编辑。Enter 或点击别处确认，Esc 取消，确认后通过“保存数据”持久化。设备及文字标注编辑沿用图面的撤销／重做；轨道电路编辑同步侧栏表单。只读、绘图和进路起终点拾取模式不启用重命名。重命名只更改已有名称或标注文字，不更改对象 ID、绑定或拓扑；没有名称字段的节点、曲线、钢轨绝缘和车挡不提供此操作。

仅使用 `StationLayoutEditor` 的宿主需监听 `cell-rename`（`{ id, name }`）更新自己持有的 `cells` 和相关表单，完整 `StationLayout` 已包含接线。浏览器检查页为主应用 `/dev/station-layout-rename.html`，使用完整共享组件和内存 Gateway，可验证双击、确认／取消、保存后载入而不写入用户数据库。

宿主还需安装并全局注册 Vue 3 与 Element Plus。离线分发使用 `npm run build` 后生成的 `npm pack` 包；样式必须通过上面的显式 CSS import 引入。`./style.css` 同时在 package exports 中声明了 CSS 文件和 `style.d.ts` 类型入口。
