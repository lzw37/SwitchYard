# 前端开发与独立站场模块接入

[文档目录](README.md) · [English](../en/09-development-integration.md)

本文依据 2026-10-11 的项目文件、网关契约和实际路由说明开发、复用与验证。
环境安装、数据库和发布操作请从文档目录进入相应章节；本篇不要求访问业务数据库。
保留的旧文档见文末，其中已过时的接口数量和缺失测试入口在本文明确说明。

## 1. 目录与运行边界

| 路径 | 责任 |
| --- | --- |
| `switchyard-vue/src/capacity` | 能力页面、计划、求解及仿真宿主 |
| `switchyard-vue/src/hump` | 驼峰页面、绘图、检算及仿真 |
| `switchyard-vue/src/course` | 课程、视频、PDF、练习 |
| `switchyard-vue/src/views` | 账号及其他路由页面 |
| `switchyard-vue/src/components/ui` | 共享按钮、分隔条与图标 |
| `switchyard-vue/src/locales` | 中英文词条 |
| `SwitchYard.StationLayout/frontend` | 可复用 Vue 站场布局组件 |
| `SwitchYard.StationLayout/backend` | 宿主无关的布局契约与应用逻辑 |
| `SwitchYard.WebApi/SwitchYard.Service/StationLayout` | SwitchYard 的数据库、权限、迁移和 ID 适配 |

主前端与 `SwitchYard.StationLayout` 必须保持相邻目录关系。
`package.json` 使用 `file:../SwitchYard.StationLayout/frontend`，Vite 将包入口直接映射到共享源码。
共享包不内置宿主 HTTP 地址、Axios 单例、用户表或数据库连接。
新宿主通过 Gateway 和 Repository 接入，可使用自己的认证与存储，支持独立离线部署。

## 2. 主前端启动与构建

`switchyard-vue/package.json` 声明 Node.js `^20.19.0 || >=22.12.0`。
源码中直接导入 TypeScript 的 Node 测试需使用 Node `22.18+` 或 `24`；构建最低版本与测试版本不要混淆。
在 `switchyard-vue` 目录执行：

```powershell
npm ci
npm run dev -- --host 127.0.0.1
```

开发服务器固定端口 `5173`，`strictPort=true`；占用时失败，不自动递增端口。
默认 `npm run dev` 监听 `0.0.0.0`；上例只开放本机访问。
开发配置的本地主机名会替换为浏览器当前主机名，API 端口为 `7297`。
在局域网 IP 上打开前端时，默认连接同一主机的 API 端口。

覆盖 API 地址时创建 `.env.development.local`：

```dotenv
VITE_API_BASE_URL=http://localhost:7297
```

该值应为服务根地址，不额外加 `/api`；修改后重启 Vite。
认证路径以 `/api/Auth` 开头，业务还包括 `/Hump`、`/Capacity`、`/StationLayout`、`/OperationPlan` 等。
Axios 通常使用绝对 API 根地址；Vite 的 `/api` 代理不等于所有业务接口都会被代理。
生产构建前在 `.env.production.local` 设置实际地址，或使用生产 JSON 配置。

```powershell
npm run type-check
node node_modules/vite/bin/vite.js build --mode production
npm run preview -- --host 127.0.0.1
```

输出为 `dist/`，预览端口默认 `4173`，预览命令不会启动后端。
`build` / `build:prod` 组合类型检查和构建；分步命令便于区分 Windows/npm 环境中的脚本问题。
`build-only` 不检查类型，`preview:prod` 不重新编译，`dev:prod` 是读取生产配置的开发服务器。
`config.ts` 在 `import.meta.env.DEV=false` 时选生产 JSON，因此 `build --mode development` 仍可能连接生产地址。
`VITE_API_BASE_URL` 优先级最高且写入构建产物；发布后改变服务器环境变量不会更新已生成的 JavaScript。

## 3. 公共界面与双语维护

优先使用 `workspace.css` 中的 `--sy-surface`、`--sy-surface-muted`、`--sy-border`、`--sy-text`、`--sy-text-muted`、`--sy-radius`。
Element Plus 基础 CSS 在工作区 CSS 之前加载。
公共选择器应限制作用范围；驼峰和独立站场工具栏保留自身布局。

`ActionButton` 的 `label` 必填，提供可访问名称；默认 `variant="icon"` 为 30 px 圆按钮并提供悬停提示。
当前还支持 `variant="text"` 和 `variant="icon-text"`，适合表单和对话框中的可见动作名称。
`active` 表示切换状态并提供 `aria-pressed`；保留业务上的 `disabled` 和 `loading` 条件。
点击事件传入 `MouseEvent`，若业务方法接收可选配置对象，使用 `@click="refreshData()"` 避免误传事件。
依赖按钮外层布局时增加自己的容器，不依赖 tooltip 包装结构。

`PaneDivider` 只更新绑定值，宿主仍需把该值绑定到面板宽高或 `flex-basis`：

```vue
<PaneDivider v-model="sidebarWidth" direction="horizontal"
  :min="240" :max="maxSidebarWidth" :reset-value="320"
  reverse :label="t('common.resize.horizontal')" />
```

`horizontal` 调整宽度，`vertical` 调整高度；`reverse` 用于右侧或下侧面板。
可操作宽度 8 px，方向键步长 10 px，Shift 步长 40 px，Home/End 到边界，Enter/双击复位。
父容器计算有效上下限，异步尺寸初始化完成后再挂载；需要滚动的子面板设置 `min-width:0`、`min-height:0`。
组件不持久保存尺寸；复位事件可由宿主用来恢复比例布局。

`zh.json` 与 `en.json` 保持相同键和插值参数；提示、校验、空状态及可访问名称也需双语。
部分过程编辑文本仍使用 `ui(zh, en)`，需同时维护两种文本。
用户名称和业务枚举不应在切换语言时改写；共享站场的兜底翻译在 `messages.ts`。

## 4. 共享站场包与前端挂载

当前 npm 包 `@switchyard/station-layout` 与 .NET 包 `SwitchYard.StationLayout` 均为 `0.1.0-alpha.2`。
前端 peer dependencies：Vue `>=3.5.0 <4`、Element Plus `>=2.10.0 <3`、icons `>=2.3.0 <3`。
宿主全局安装 Vue 与 Element Plus，并在 Vite 设置 `resolve.dedupe: ['vue', 'element-plus', '@element-plus/icons-vue']`。
跨仓库应安装固定版本 `.tgz`，不要把另一个仓库的任意 `.vue` 路径作为发布接口。

以下宿主骨架假定已实现自己的 `stationLayoutGateway`；`scopeId` 来自宿主当前项目选择。

```vue
<script setup lang="ts">
import { ref } from 'vue'
import { StationLayout, createStationLayoutTranslator } from '@switchyard/station-layout'
import '@switchyard/station-layout/style.css'
import { stationLayoutGateway } from './stationLayoutGateway'
const scopeId = ref('')
const schemeId = ref<string | undefined>()
const readonly = ref(false) // 从宿主实际权限计算
const translate = createStationLayoutTranslator('zh')
</script>
<template>
  <div class="layout-host">
    <StationLayout :key="scopeId" :selected-instance-id="scopeId"
      v-model:station-scheme-id="schemeId" :gateway="stationLayoutGateway"
      :translate="translate" :readonly="readonly" />
  </div>
</template>
<style scoped>
.layout-host { height: 720px; min-height: 0; overflow: hidden; }
</style>
```

`gateway` 必填，`translate`、`formatError` 和 `readonly` 可选；不提供翻译时默认中文。
`stationSchemeId` / `update:stationSchemeId` 可与宿主的方案选择同步。
`selectedInstanceId` 为空显示未选择状态；宿主需明确容器高度并显式导入打包 CSS。
同时按项目和 scope 隔离时，可使用 `${projectId}:${scopeId}` 作为组件 key。
`readonly=true` 仍允许查看、选择、缩放、加载和导出，但禁止绘图、编辑、导入、保存、方案管理及 DWG 提取。
只读属性是界面约束，服务端授权仍须独立实现。

## 5. Gateway 的完整契约

当前为 **9 个必选方法和 1 个可选方法**，旧说明中的“8 个方法”已经过时。
方法参数使用 `instanceId`、`stationSchemeId`；兼容 HTTP 参数常使用 `instanceID`、`stationSchemeID`。

| Gateway 方法 | 对应 HTTP 操作 | 关键返回值 |
| --- | --- | --- |
| `getStationSchemes` | GET `GetStationSchemes` | 方案数组 |
| `createStationScheme` | POST `CreateStationScheme` | 新方案 |
| `copyStationScheme` | POST `CopyStationScheme` | 独立副本 |
| `editStationScheme` | PUT `EditStationScheme` | 改名后方案 |
| `deleteStationScheme` | DELETE `DeleteStationScheme` | 无正文依赖 |
| `getJson` | POST `GetJson` | 完整布局及修订号 |
| `saveJson` | POST `SaveJson` | 新修订号、最终文档及 ID 映射 |
| `searchRoutes` | POST `SearchRoutes` | 字符串节点/线路 ID 的路径列表 |
| `extractDwgFile` | POST `ExtractDwgFile` | 解析后的布局 |
| `repairJson`（可选） | POST `RepairJson` | `{ json, repairCount, repairs }` |

默认 HTTP 前缀为 `/StationLayout`，当前共享 Endpoint 一共映射 **10 个**操作。
Gateway 复用宿主 HTTP 客户端以继承认证、令牌刷新、项目上下文和错误处理。
`getJson` 应读取 `ETag` / `X-Station-Layout-Revision` 到 `metadata.revision`。
保存时发送 `expectedRevision` 或强 ETag `If-Match: "<revision>"`，并接收保存后的新版本。
版本冲突为 HTTP `409`，应重新加载核对，不能静默丢弃版本检查重试覆盖。
跨域时暴露 `ETag`、`X-Station-Layout-Revision`、`X-Station-Layout-Exists` 响应头。
应兼容 PascalCase/camelCase 响应并整理纯文本及 problem-details 错误。
完整参考实现见[宿主 Gateway](../../switchyard-vue/src/capacity/switchyardStationLayoutGateway.ts)。

## 6. JSON 导入、身份与修复

导出文件顶层标记为 `format: "switchyard.station-layout"`、`formatVersion: 1`，不是仅凭 `schemaVersion` 识别。
保留十个集合：`tracks`、`curves`、`nodes`、`signals`、`insulationJoints`、`bufferStops`、`platforms`、`switches`、`cells`、`annotations`。
文档还保留几何、拓扑、坐标变换、显示/网格设置、小数精度、数组顺序和扩展字段。
旧版没有格式标记的布局 JSON 可导入；不支持的格式或版本会拒绝。
导入替换当前画布，绑定当前实例/目标方案，并保留目标方案修订号；不会切换到文件中的源方案。
导入通过检查后才应用，失败保留原画布；成功导入/载入清空旧图撤销历史，保存后才持久化。
导出不包含选中状态、绘制草稿、滚动位置和撤销栈。

完整组件 ref 提供 `exportJson()` 和异步 `importJson(json)`。
纯函数 `parseStationLayoutJson` / `serializeStationLayoutJson` 与文件菜单使用同一编解码逻辑。
仅用底层 Editor 时，先解析校验，再 `loadDataFromJson(document, { preserveDocument: true, resetHistory: true })`；用 `buildJsonData()` 取得全部图元。
完整组件额外同步轨道电路侧栏、显示设置与当前作用域；底层宿主需自行处理这些状态。

所有对象 ID、端点 ID、搜索输入和路径返回 ID 都应保持字符串。
前导零和超出 JavaScript 安全整数范围的 ID 不可经 `Number`、`parseInt` 或数字数据库列转换。
SwitchYard 保存时为目标方案中的新对象分配雪花字符串 ID，已保存对象保留 ID，并在同一事务改写引用。
保存响应的 `savedJson` 与按集合划分的 `idMappings` 必须同步到画布、选择、Cells 和撤销/重做历史。
保存期间产生的新编辑应保留，不能用旧请求的服务器快照覆盖新编辑。

保存前会尝试按位置修复悬空引用，默认 `TopologyRepairTolerance=1` 个布局坐标单位。
只接受唯一匹配；有效引用和几何保留，无法确定或多候选会拒绝整份保存。
`RepairJson` 仅预览校验/修复、不写库；实现可选 Gateway 后导入/加载也可使用此功能。
响应通过 `repairCount`、`repairs` 说明改动，发生修复时还可返回 `repairedJson`。
只读加载或本地导入中看到修复不代表数据库已经保存。

## 7. 后端宿主接入

共享后端目标框架为 `net8.0;net10.0`，依赖 ASP.NET Core 与 ACadSharp `3.4.29`。
它不直接读取连接串、执行 SQL 或规定宿主表名。
宿主须实现以下接口，签名以[StationLayoutAbstractions.cs](../../SwitchYard.StationLayout/backend/StationLayoutAbstractions.cs)为准：

| 接口 | 宿主责任 |
| --- | --- |
| `IStationLayoutRepository` | 列表、读取、新建、复制、改名、删除、原子替换布局 |
| `IStationLayoutAuthorization` | 作用域及全局授权 |
| `IStationLayoutIdGenerator` | 产生宿主拥有的唯一方案 ID |
| `IStationLayoutArtifactSink`（可选） | DWG 完成后的宿主副作用；默认无操作 |

权限为 `View`、`ManageSchemes`、`SaveLayout`、`ImportDwg`；返回 Allowed、Unauthenticated、Forbidden 或 NotFound。
`scopeId` 是宿主的不透明资源 ID，前端称 `selectedInstanceId`，兼容 HTTP 称 `instanceID`。
多项目或租户系统应结合可信服务端上下文隔离数据，不能仅信任浏览器传来的项目标识。

以下为新宿主注册骨架，`App...` 类型和认证方案由宿主实现：

```csharp
builder.Services.AddScoped<IStationLayoutRepository, AppLayoutRepository>();
builder.Services.AddScoped<IStationLayoutAuthorization, AppLayoutAuthorization>();
builder.Services.AddSingleton<IStationLayoutIdGenerator, AppLayoutIdGenerator>();
builder.Services.AddSwitchYardStationLayout(options =>
{
    options.LegacyV1Compatibility = true;
    options.TopologyRepairTolerance = 1;
});
// 宿主先配置认证方案、AddAuthorization 和其他基础服务。
var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapSwitchYardStationLayout("/StationLayout");
```

共享 Endpoint 整组要求认证，但不会替宿主选择 JWT 或 Cookie 方案。
**当前 SwitchYard 使用 `AddSwitchYardStationLayoutLegacyHost()` 和 `MapControllers()`，公共路由仍由兼容 Controller 提供。**
不要在此宿主上额外映射相同 Endpoint，否则会发生路由冲突；新宿主选择其中一种公开路由方式。
DWG 请求使用 multipart `file`、`layerName`，默认上限 20 MiB，提取不等同于最终保存布局。

## 8. 存储、并发和发布

Repository 返回脱离跟踪状态的快照；关联表及修订号应在一致的读事务中读取。
完整保存和 `ExpectedRevision` 比较须处于同一事务，冲突抛 `StationLayoutConflictException`，成功只增加一次版本。
复制原子执行并从 revision `0` 开始；删除需原子清理方案及所属图元，存储异常不能吞掉后返回成功。
独立宿主可保存完整 JSON 聚合；若用关系表重建，必须保留全部属性、精度及扩展字段。
SwitchYard 当前以关系表为唯一来源，读取时组装 JSON；`LayoutMetadata`/`LayoutExtensions` 只保存元数据和根扩展。
迁移将旧整数对象 ID 改为文本，利用历史存档恢复身份并更新进路/过程引用，成功后移除旧 `LayoutDocument`。
历史拓扑冲突会停止迁移并保留原资料，不能将迁移失败当成可忽略日志。
各宿主维护自己的 DDL 和版本化迁移；跨程序交换用 JSON，不直接共享业务表或互写数据库。

离线发布应同时固定 npm/NuGet 版本，记录 SHA-256，并提交宿主 lockfile。
在共享前端目录运行 `npm ci`、`npm run test:contract`、`npm run type-check`、`npm run build`、`npm pack`。
在仓库根目录使用 `dotnet pack SwitchYard.StationLayout/backend/SwitchYard.StationLayout.csproj -c Release -o artifacts/nuget` 生成后端包。
前后端版本同步升级，不覆盖已发布的同版本制品；在宿主中使用实际制品再构建验证。
本地包更新后重启 Vite，必要时检查预打包缓存，单纯刷新业务数据不会替换旧组件代码。

## 9. 当前可复现检查与缺口

以下是文件存在性和 Git 跟踪状态核对，**不是本次执行后的通过报告**。

| 检查项 | 当前状态 |
| --- | --- |
| 主前端 `src/capacity/*.test.mjs` 及 `src/capacity/components/*.test.mjs` | 已跟踪，包括过程、冲突、仿真和站场图表测试 |
| `src/sketchlayout/sketchlayout.test.mjs` | 已跟踪 |
| 共享前端 `gateway-contract.ts` | 已跟踪，供 `test:contract` 使用 |
| 共享前端 runtime、inline-rename、json-codec、json-editor、json-integration 测试 | 这 5 个 `.mjs` 文件已跟踪 |
| 共享 `test:runtime` 引用的 full-view、topology、repair、identity 测试 | 当前文件缺失，不能宣称整条脚本可完整运行 |
| 主前端 `checks/i18n.mjs` | 当前工作区存在，但 `checks/` 被忽略，未跟踪 |
| `switchyard-vue/tests/README.md`、paneDivider 等旧测试入口 | 当前缺失且未跟踪 |
| `SwitchYard.StationLayout/tests/...Tests.csproj`、`SwitchYard.Service.Tests/...Tests.csproj` | 旧 README 引用路径当前不存在 |
| 旧 SmokeTests / Service.StationLayout.IntegrationTests 目录 | 当前本地存在，但被 `.gitignore` 排除，未随仓库跟踪 |

可从已跟踪文件选择与改动相关的检查，例如在主前端目录：

```powershell
node --test src/capacity/operationProcessRoutes.test.mjs
node --test src/capacity/operationProcessPrecedence.test.mjs
npm run type-check
```

共享前端可分别执行已存在的 `node tests/layout-json-codec.mjs` 或 `node tests/layout-runtime.mjs`，而不是假定缺失的综合脚本已通过。
开发预览已跟踪入口包括 `/dev/workspace-style.html`、`/dev/operation-process.html`、`/dev/operation-process-layout.html`、`/dev/operation-process-links.html`。
旧 README 中的 `/tests/fixtures/...`、`/dev/operation-simulation.html` 和 `/dev/station-layout-rename.html` 不应视为当前可复现入口。
预览使用本地/内存示例，不证明真实服务已保存，也不是正式构建入口。
界面改动应补充双语、窄窗口、按钮提示、拖动复位、页面离开清理的人工验证。

## 10. 接入问题与保留文档

| 问题 | 优先检查 |
| --- | --- |
| 页面空白或零高度 | 容器高度、scope/方案选择、异步可见性 |
| 没有样式 | 是否显式导入打包 CSS，是否被宿主全局选择器覆盖 |
| 保存总是 409 | 读取/发送/更新 revision，跨域是否暴露修订响应头 |
| 401 / 403 / 404 | 认证中间件、Gateway 凭据、权限映射与资源作用域 |
| duplicated Vue 或 ref 异常 | peer dependency 和 Vite dedupe 是否一致 |
| ID 丢失精度或保存后绑定错乱 | 是否始终使用字符串并应用所有 ID 映射 |
| 读取得到 HTML | API 反代是否错误回退至前端 `index.html` |

History 路由要求前端页面回退至 `index.html`，API 路径单独转发；部署子路径还需统一 Vite base 与服务器路径。
旧文档均保留：[主前端 README](../archive/development/frontend.md)、[模块 README](../archive/development/station-layout.md)、[原中文接入指南](../archive/development/station-layout-integration.zh-CN.md)、[共享前端 README](../archive/development/station-layout-frontend.md)。
进一步核对：[Gateway 类型](../../SwitchYard.StationLayout/frontend/src/gateway.ts)、[JSON 编解码](../../SwitchYard.StationLayout/frontend/src/layoutJson.ts)、[Endpoint 映射](../../SwitchYard.StationLayout/backend/StationLayoutEndpointExtensions.cs)、[宿主注册](../../SwitchYard.WebApi/SwitchYard.Service/StationLayout/LegacyStationLayoutServiceCollectionExtensions.cs)。
