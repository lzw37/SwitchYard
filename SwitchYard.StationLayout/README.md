# SwitchYard StationLayout

可复用的 Vue 站场绘图控件与宿主无关的 .NET 后端模块，提供站场文档、拓扑、方案、进路搜索和 DWG 导入。认证和数据存储由宿主接入。

A reusable Vue station layout component and host-independent .NET backend for layout documents, topology, schemes, route search, and DWG import. The host supplies authentication and persistence.

完整说明统一在源码仓库 `docs/` 中维护。本 README 随 NuGet 包发布，因此下列入口使用在线仓库地址。

Full documentation is maintained in the repository's `docs/` directory. This README ships in the NuGet package, so its links use repository URLs.

- [中文开发与接入](https://gitee.com/lzw37/SwitchYard/blob/master/docs/zh-CN/09-development-integration.md)
- [English development and integration](https://gitee.com/lzw37/SwitchYard/blob/master/docs/en/09-development-integration.md)
- [双语文档中心 / Documentation center](https://gitee.com/lzw37/SwitchYard/blob/master/docs/README.md)

旧模块说明和接入指南已移入 `docs/archive/development/`。在线文档随仓库发布更新；离线源码用户直接查阅上述 `docs/` 路径。

Previous module and integration guides are preserved in `docs/archive/development/`. Online documentation follows repository releases; offline source users can open the same paths locally.

## 打包准备 / Prerequisites

- 前端需要符合 Vite 7 要求的 Node.js（`^20.19.0 || >=22.12.0`）及 npm；后端需要 .NET 10 SDK，以构建 `net8.0` 和 `net10.0` 两个目标框架。
- 前后端当前版本均为 `0.1.0-alpha.2`。发布新版本前，同步修改 `frontend/package.json` 的 `version` 和 `backend/SwitchYard.StationLayout.csproj` 的 `Version`；下文产物文件名和安装命令也应使用对应版本。
- 首次安装或还原依赖需要可用的 npm / NuGet 源；离线环境需提前准备依赖缓存或本地源。以下打包步骤均从源码仓库根目录开始。

## Vue 组件打包 / Vue component package

```sh
cd SwitchYard.StationLayout/frontend
npm install
npm run test:contract
npm run type-check
npm run build
npm run pack:offline
cd ../..
```

当前组件目录未提交 `package-lock.json`，首次安装使用 `npm install`；若后续维护并提交了 lockfile，可改用 `npm ci`。`build` 生成 JavaScript、CSS 和 TypeScript 类型声明。`pack:offline` 仅执行 `npm pack`，不会自动构建，必须在构建成功后执行；打包前可用 `npm pack --dry-run` 检查文件清单。

产物为 `SwitchYard.StationLayout/frontend/switchyard-station-layout-0.1.0-alpha.2.tgz`，包含 `dist/`、包元数据、README 和 LICENSE。关键入口为 `dist/index.js`、`dist/index.d.ts` 和 `dist/station-layout.css`。

在宿主 Vue 项目目录安装（将路径替换为实际 `.tgz` 路径）：

```sh
npm install /path/to/switchyard-station-layout-0.1.0-alpha.2.tgz
npm install 'vue@>=3.5.0 <4' 'element-plus@>=2.10.0 <3' '@element-plus/icons-vue@>=2.3.0 <3'
```

宿主需注册 Element Plus，并显式导入组件及样式：

```ts
import { StationLayout } from '@switchyard/station-layout';
import 'element-plus/dist/index.css';
import '@switchyard/station-layout/style.css';
```

Gateway、容器尺寸等接入要求见上方开发与接入文档。

## 后端 NuGet 打包 / Backend NuGet package

在源码仓库根目录执行；`dotnet pack` 会自动还原依赖并构建 Release 配置：

```sh
dotnet pack SwitchYard.StationLayout/backend/SwitchYard.StationLayout.csproj -c Release -o artifacts/nuget
```

产物为 `artifacts/nuget/SwitchYard.StationLayout.0.1.0-alpha.2.nupkg`，同一个包内含 `lib/net8.0`、`lib/net10.0` 两套程序集，以及本 README 和许可证。它是可复用类库包，宿主负责提供认证、存储及 ASP.NET Core 运行环境。

本地安装时，在宿主后端项目目录执行（将源路径替换为实际的 `artifacts/nuget` 目录）：

```sh
dotnet nuget add source /path/to/switchyard/artifacts/nuget --name SwitchYardLocal
dotnet add package SwitchYard.StationLayout --version 0.1.0-alpha.2
```

本地源只需添加一次；保留可还原 `ACadSharp` 等依赖的 NuGet 源，完全离线时也需准备这些依赖包。分发时同步提供前后端对应版本的 `.tgz` / `.nupkg`，在宿主中使用实际制品完成构建验证，且不要覆盖已发布的同版本制品。
