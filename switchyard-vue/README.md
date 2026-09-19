# switchyard-vue 前端开发说明

这是 SwitchYard 的 Vue 3 + TypeScript 前端，包含通过能力、驼峰、课程和账号页面。项目整体入口见[根 README](../README.md)，面向使用者的操作说明见[使用指南](../doc/User-Guide.md)。

## 环境与目录

- 构建环境：Node.js `^20.19.0 || >=22.12.0`、npm。仓库的 Node 原生 TypeScript 测试使用 Node 22.18+ 或 24。
- 保留 `switchyard-vue` 与 `SwitchYard.StationLayout` 的相邻目录关系。`package.json` 使用 `file:../SwitchYard.StationLayout/frontend`，Vite 将包入口映射到共享模块源码。
- 在本目录执行下面的 npm 和 Node 命令。依赖版本以 `package-lock.json` 为准，首次安装使用 `npm ci`。

| 路径 | 内容 |
| --- | --- |
| `src/capacity/` | 通过能力工作区、计划编排、求解、二维/三维仿真 |
| `src/hump/` | 驼峰平面与纵断面、工况、检算与仿真 |
| `src/course/` | 课程内容、视频、PDF、填空练习 |
| `src/views/` | 登录、用户信息与管理等页面 |
| `src/components/ui/` | `ActionButton`、`PaneDivider` 和共享操作图标 |
| `src/assets/workspace.css` | 工作区颜色、细边框及共享界面样式 |
| `src/locales/`、`src/i18n.ts` | 中英文词条及语言选择 |
| `src/stores/auth.ts`、`src/utils/axios.ts` | 登录状态、请求令牌、刷新及通用错误处理 |
| `src/config.ts`、`src/config.*.json` | API 地址与环境配置 |
| `src/router/index.ts` | History 路由和页面权限守卫 |
| `tests/`、`checks/` | 几何、组件生命周期、仿真、分隔条与作业过程测试 |
| `tests/fixtures/`、`dev/` | 直接挂载生产组件的本地预览入口 |

## 开发与 API 配置

```powershell
npm ci
npm run dev -- --host 127.0.0.1
```

Vite 开发端口固定为 `5173`，已启用 `strictPort`；占用时会报错，不会自动切换到其他端口。默认监听所有网卡；上述 `--host 127.0.0.1` 命令仅用于本机访问，局域网访问可使用 `npm run dev`。

开发环境默认使用浏览器地址中的主机名和 HTTP `7297` 端口。例如从 `http://localhost:5173` 打开页面时访问 `http://localhost:7297`，通过局域网地址打开时访问同一主机的 `7297`。无需把网卡 IP 写入配置，网络地址变化后可继续使用原端口。

需要指定其他 API 时，创建 `.env.development.local`，填写已启动服务的根地址。例如显式固定到本机：

```dotenv
VITE_API_BASE_URL=http://localhost:7297
```

`VITE_API_BASE_URL` 优先于默认地址和主机名匹配；设置后会固定使用该地址。生产构建未显式覆盖时使用 production JSON 中的 `serverurl`。修改环境文件后重启 Vite。后端默认开发绑定为 `http://0.0.0.0:7297`，另保留 `http://localhost:5102` 兼容入口。

这里填写服务根地址，不额外加 `/api`。认证路径为 `/api/Auth/...`，业务路径包括 `/Capacity/...`、`/Hump/...`、`/StationLayout/...` 和 `/OperationPlan/...`。Axios 通常直接使用配置的绝对根地址；`vite.config.ts` 中仅 `/api` 的开发代理指向 `http://127.0.0.1:7297`，不等于所有业务接口都会被代理。

完整后端配置、空数据库限制和账号准备见[首次启动说明](../doc/Getting-Started.md)。本说明中的命令不会替你创建可用教学数据。

## 构建与预览

```powershell
npm run type-check
node node_modules/vite/bin/vite.js build --mode production
npm run preview -- --host 127.0.0.1
```

输出目录为 `dist/`，生产预览默认端口为 `4173`。`vite preview` 只提供静态构建产物，不启动业务 API。

构建前设置实际部署地址，例如在 `.env.production.local` 中填写：

```dotenv
VITE_API_BASE_URL=https://api.example.com
```

该地址会进入前端产物；发布后修改服务器的环境变量不能改变已经生成的 JavaScript，需要重新构建。

| 已有脚本 | 用途及注意事项 |
| --- | --- |
| `dev` | 开发服务器，使用 development 模式 |
| `dev:prod` | 以 production 模式启动开发服务器，读取生产地址配置 |
| `type-check` | `vue-tsc --build` 类型检查 |
| `build`、`build:prod` | 组合运行类型检查和构建；部分 Windows/npm 环境可使用上面的分步命令 |
| `build:dev` | 使用 development 模式构建，但不保证 API 地址来自 development JSON |
| `build-only` | 仅 Vite 构建，不运行类型检查 |
| `preview`、`preview:prod` | 预览已有构建，不重新编译 |

`config.ts` 在 `import.meta.env.DEV` 为 false 时选择生产配置，因此 `build --mode development` 仍会选择 production JSON，除非设置了 `VITE_API_BASE_URL`。不要仅凭脚本名称判断产物将连接哪个服务。

## 工作区界面约定

能力分析、课程等功能区采用小圆按钮、单层浅色面板和细边框。导航页签、数据字段和必要状态保留可读文字；已在页签中出现的标题不在面板里重复。驼峰模块保留原有按钮、标题、工具栏和面板样式，不套用公共圆形按钮与分隔条。独立车站布置图控件保留自身的工具栏设计。公共样式应限制在采用该设计的工作区内，避免通过全局选择器覆盖这两个模块。

颜色和尺寸优先使用 `workspace.css` 中的 `--sy-surface`、`--sy-surface-muted`、`--sy-border`、`--sy-text`、`--sy-text-muted` 和 `--sy-radius`。Element Plus 的基础 CSS 应先于工作区样式加载。

### 圆形操作按钮

```vue
<script setup lang="ts">
import { Check } from '@element-plus/icons-vue'
import { useI18n } from 'vue-i18n'
import ActionButton from '@/components/ui/ActionButton.vue'

const { t } = useI18n()
const props = defineProps<{ saving: boolean; canSave: boolean }>()
const emit = defineEmits<{ save: [] }>()
</script>

<template>
    <ActionButton :label="t('common.actions.save')" :icon="Check"
        :loading="props.saving" :disabled="!props.canSave"
        @click="emit('save')" />
</template>
```

- `label` 必填，用于悬停提示和 `aria-label`；按钮直径为 30 px。
- 使用图标组件或短文字插槽，避免把长说明放进圆按钮。
- `active` 用于开关型操作，同时生成 `aria-pressed`；`disabled` 和 `loading` 保留原有业务条件。
- 事件会传出 `MouseEvent`。业务方法若接收可选配置对象，写成 `@click="refreshData()"`，避免将鼠标事件误当成配置。
- `class`、`style` 等属性传到内部按钮。需要 `grid-column`、占据剩余空间等外层布局时，使用独立容器，不依赖按钮外部的 tooltip 包装结构。

### 可调分隔条

```vue
<PaneDivider v-model="sidebarWidth" direction="horizontal"
    :min="240" :max="maxSidebarWidth" :reset-value="320"
    reverse :label="t('common.resize.horizontal')" />
```

在脚本中导入 `@/components/ui/PaneDivider.vue`，并将 `sidebarWidth` 绑定到实际面板的宽度或 `flex-basis`。组件只发出尺寸变更，不会自动寻找或调整相邻面板。

| 属性 / 事件 | 含义 |
| --- | --- |
| `v-model` | 面板尺寸，单位 px |
| `direction="horizontal"` | 左右拖动，调整宽度；这是默认方向 |
| `direction="vertical"` | 上下拖动，调整高度 |
| `reverse` | 被调面板位于分隔条右侧或下侧时使用 |
| `min`、`max` | 当前容器内允许的最小和最大尺寸 |
| `resetValue` | Enter 或双击时恢复的尺寸；可随容器变化更新 |
| `label` | 可访问名称；未指定时使用通用双语名称 |
| `reset` | 复位后通知父组件，可用于恢复按比例自动布局 |

分隔条的可操作范围为 8 px，支持鼠标、触控和键盘。方向键调整 10 px，Shift + 方向键调整 40 px，Home/End 调至边界，Enter 或双击复位。

父组件应根据容器实际尺寸计算上限，并为主工作区留出可用空间；需要滚动的子面板设置 `min-width: 0`、`min-height: 0` 和相应 overflow。异步初始化宽度时先取得有效尺寸，再挂载分隔条；不要让尚未初始化的 `0` 被写成最小宽度。公共组件不持久化面板尺寸。

## 国际化与独立站场模块

普通页面从 `src/locales/zh.json` 和 `en.json` 读取文案，两种语言的键名及插值参数应保持一致。新按钮提示、空状态、表单校验和可访问名称也需要翻译。语言选择记录在浏览器的 `locale` 项中；无手动选择时根据浏览器首选语言选择。

`OperationProcessEditor.vue` 当前还使用随全局 locale 切换的本地 `ui(zh, en)` 辅助函数。维护该组件时应同时更新两种文本，不要改动用作业务值的枚举或用户数据。

`src/capacity/StationLayout.vue` 是宿主适配层，实际控件在 `../SwitchYard.StationLayout/frontend`。宿主提供网关、错误格式化与翻译；`StationLayoutGateway` 不应被替换为控件内部直接调用宿主 Axios。共享控件的兜底词条位于其 `src/messages.ts`，跨项目复用流程见[接入指南](../SwitchYard.StationLayout/INTEGRATION_GUIDE.zh-CN.md)。

## 本地预览与回归

在开发服务器启动后，使用终端实际端口访问：

| 地址路径 | 用途 |
| --- | --- |
| `/dev/workspace-style.html` | 生产能力工作区的本地样式预览 |
| `/dev/workspace-style.html?page=hump&lang=en` | 驼峰英文工作区 |
| `/dev/workspace-style.html?page=course&lang=en` | 无服务端资源的课程英文空状态与响应式布局 |
| `/dev/operation-process.html?lang=en` | 独立过程示例、拖动对象与属性编辑 |
| `/dev/operation-process-layout.html` | 真实父页面和两级选项卡中的过程布局 |
| `/dev/operation-process-links.html` | 活动/事件次序交互示例 |
| `/tests/fixtures/route-design-preview.html` | 长名称、多选标签和进路侧栏布局 |
| `/tests/fixtures/station-component-preview.html` | 真实三维组件和本地列车数据 |
| `/tests/fixtures/operation-simulation-preview.html` | 二维运行仿真预览 |

这些入口使用本地示例或内存适配器；刷新会重置示例，不能据此认定真实后端保存成功。入口不是正式构建页面。具体参数和覆盖范围见[验证说明](tests/README.md)与[作业过程文档](../docs/operation-process.md)。

界面与作业过程相关回归：

```powershell
node --test --test-isolation=none tests/paneDivider.test.mjs tests/simulationPerformance.test.mjs tests/simulationViewport.test.mjs tests/threePageLifecycle.test.mjs checks/operationProcess.test.mjs
```

更改完成后运行与改动相关的测试、类型检查和正式构建。布局改动还应实测中文/英文、桌面/窄窗口、按钮提示和菜单入口、拖动与复位。三维改动需验证离开页面后的资源释放，具体检查见测试说明。

## 路由与上线

主要页面为 `/hump`、`/capacity`、`/courses`、`/login`、`/createuser`、`/userinfo`、`/usermanagement` 和 `/hump/instancemanagement`。管理入口有路由守卫，业务操作还受后端授权控制。

应用使用 `createWebHistory`，静态服务器需要将前端页面路径回退到 `index.html`；API 请求应转发到真实服务，不应回退为 HTML。部署子路径时还需一致设置 Vite base 和服务器路径，见[部署说明](../doc/Deploy-Instruction.md)。
