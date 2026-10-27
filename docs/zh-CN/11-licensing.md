# 11 开源许可与第三方声明

[English](../en/11-licensing.md) · [中文目录](README.md) · [文档中心](../README.md)

SwitchYard 保留现有 MIT 许可证。需要补充的是依赖和素材的独立声明、发布包中的许可证，以及随依赖升级维护这些材料的方法。本章对应 2026-10-11 的许可核查；具体包版本以随附清单为准。

## 项目许可范围

根目录 [LICENSE](../../LICENSE) 是项目的许可正文，保留原版权声明 `Copyright (c) 2025 廖正文`。项目自身的软件与随附文档按该 MIT 文本提供；已单独注明的第三方代码、模板、图标及依赖适用各自许可。年度变化或文档整理不要求另换许可证，也不应把第三方版权人替换成项目作者。

MIT 允许使用、修改和再分发，包括商业使用，条件包括保留版权及许可声明；原文中的免责声明也应随完整文本保留。本章是维护说明，不增删许可条件。参见 [MIT 官方文本](https://opensource.org/license/mit)。

课程视频、课件、业务数据，以及从外部站点引用的照片或出版物不能仅凭项目根目录的 MIT 文件判断授权。当前被忽略的本地资源没有纳入这份源码许可清单；另行提供时应登记其来源与允许的使用范围。

## 声明和证据的位置

所有补充材料集中在 [legal 目录](../legal/README.md)，不在各模块复制维护长篇说明。

| 材料 | 内容 |
| --- | --- |
| [源码素材声明](../legal/source-assets-NOTICES.txt) | 图标、样式、create-vue 和 Git 忽略模板的来源、固定上游版本及适用许可 |
| [npm 清单](../legal/npm-inventory.json) / [随包许可文本](../legal/npm-NOTICES.txt) | 主锁文件的直接、传递、开发和可选依赖；本地版本匹配结果及提取的原始声明 |
| [NuGet 清单](../legal/nuget-inventory.json) / [随包许可文本](../legal/nuget-NOTICES.txt) | 受版本管理的项目、当前还原结果、包元数据、原生文件及本地许可正文 |
| [npm 上游补充](../legal/upstream-npm-NOTICES.txt) | 从可核实上游版本取得的补充正文及仍未解决的项目 |
| [NuGet 上游补充](../legal/upstream-nuget-NOTICES.txt) | 包内未带正文时，从固定上游版本取得的许可及来源记录 |
| [Apache 2.0](../legal/licenses/Apache-2.0.txt)、[Vue Theme MIT](../legal/licenses/vue-theme-LICENSE.txt)、[模板 CC0](../legal/licenses/create-vue-template-CC0.txt) | 源码素材对应的完整许可原文 |

JSON 清单中的开发依赖或某个平台的可选包不等于已进入最终部署文件。反过来，包顶层的 SPDX 标识也不能代替其内部字体、原生库或其他素材的许可。清单保留“本地未安装”“版本不一致”“包内缺正文”等状态；上游补充正文不篡改这些原始状态。

## 需要区别处理的内容

- `IconTooling.vue` 自带 MaterialDesign 的 Apache-2.0 声明，已补全文。`base.css` 的 Vue Theme 配色按上游 MIT 保留 Evan You 的版权声明。
- 经文件比对确认的 create-vue 初始模板采用 CC0-1.0；其中图标的专属 Apache 声明仍单列保留。Vue 的 logo 和 favicon 不能因此被描述为项目原创标识。
- `pdfjs-dist` 除顶层 Apache-2.0 外，包内还有 CMap、字体、ICC 和解码器许可。采集器递归保留这些声明；只复制顶层 `LICENSE` 不足以说明所有相关资源。
- JSZip 的许可表达式包含 MIT 与 GPL 的选择，本项目分发时采用其提供的 MIT 选项。`pako` 的 MIT 与 Zlib 是并列要求，不是二选一；完整文本保留在 npm 声明中。
- NuGet 中 `SixLabors.Fonts` 当前解析版本为 `1.0.0`，该包声明 Apache-2.0；不能用其他版本的许可推断本版本，升级时也须重新核对。
- OR-Tools 包的 Apache-2.0 声明不覆盖对全部原生组件的逐项核验。当前原生文件及其上游材料缺口见下节和 NuGet 清单。

对 Apache-2.0 组件再分发时，除提供许可文本外，还应保留适用的版权与归属声明、随上游提供的 NOTICE，并标明对其文件的修改。实际要求以 [Apache 2.0 第 4 节](https://www.apache.org/licenses/LICENSE-2.0)为准。第三方许可的权威原文按上游保留，不把中文说明当成替代文本。

## 发布包应携带什么

两个 npm 清单现已声明 `license: MIT`，主锁文件中对应的项目记录同步更新。独立站场前端包根目录的 [LICENSE](../../SwitchYard.StationLayout/frontend/LICENSE) 是根许可证的逐字节副本，供 npm 打包使用；修改根文件后应同步副本。`private: true` 仍保留在主应用中。

独立站场后端的 [项目文件](../../SwitchYard.StationLayout/backend/SwitchYard.StationLayout.csproj)声明 `PackageLicenseExpression` 为 `MIT`，并将根目录 `LICENSE` 打进 NuGet 包。NuGet 依赖各自保留原许可，不能因宿主包声明 MIT 而改写。相关字段规则见 [npm 文档](https://docs.npmjs.com/cli/v11/configuring-npm/package-json/#license)和 [NuGet 文档](https://learn.microsoft.com/en-us/nuget/reference/msbuild-targets#packing-a-license-expression-or-a-license-file)。

发布应用的 `dist`、API、Agent、自包含运行包或容器镜像时，应同时携带根 `LICENSE`、适用于实际分发内容的第三方声明及其引用的许可原文。可将 `docs/legal/` 作为许可资料目录随包提供，保持 `licenses/` 相对结构；先处理下述尚未完成的核验项目。当前 `npm build` / `dotnet publish` 不会自动把整份资料目录复制进应用产物，发布者必须完成这一步。已验证的 npm 和 NuGet 包装检查仅证明独立站场包的项目 MIT 文件与元数据存在。

## 更新与检查

先按安装文档准备与发布环境匹配的依赖。以下命令均在仓库根目录运行，不联网、不安装、不还原、不启动服务，也不接触数据库：

```bash
node scripts/collect-npm-licenses.mjs
python scripts/collect-nuget-licenses.py
node scripts/collect-npm-licenses.mjs --check
python scripts/collect-nuget-licenses.py --check
python scripts/check-docs.py
```

前两个命令重新生成本地依赖清单与提取文本。`--check` 只比对当前证据与已保存结果；它检查材料是否过期，不判定整个发行包的许可是否完备。npm 采集器核对清单与锁文件、实际包名与版本、项目 MIT 副本；结构不一致返回失败。它还提供 `--strict`，可将已安装包的正文缺口等问题作为失败返回；未安装的可选平台包另行标记。NuGet 采集器遇到项目声明、项目引用与还原结果不匹配、缺少还原结果等结构问题返回失败；包内未带许可全文则记入清单。

采集脚本不会覆盖 `source-assets-NOTICES.txt`、`upstream-*-NOTICES.txt` 和 `licenses/` 中人工核对的材料。更新上游补充时须保留精确版本、来源 URL 和原版权声明。恢复了新的目标运行时、添加依赖、替换图片/字体、复制外部代码或修改锁文件后，都要重新核对适用声明。现有材料覆盖范围由清单的 `scope`、`limitations`、`findings` 或项目问题字段说明。

## 发布前尚需核验的项目

1. **OR-Tools 原生组件。** 当前 Windows 运行包含 Abseil、bzip2、HiGHS、Protobuf、SCIP、UTF-8 校验、RE2、zlib 等原生文件；包内没有完整附带其逐项许可。上游构建还涉及 Eigen 等组件。必须按实际目标平台和构建选项确定随包内容、版本及对应声明，不能把上游“可能依赖列表”当成已启用清单，也不能仅凭顶层 Apache-2.0 宣称不存在其他义务。参见 [OR-Tools v9.15 构建依赖](https://github.com/google/or-tools/blob/v9.15/cmake/dependencies/CMakeLists.txt)。
2. **少量 npm 包的完整声明。** `dingbat-to-unicode 1.0.1` 声明 BSD-2-Clause，但对应官方源码版本也未取得完整许可与版权正文；`lodash-unified 1.0.3` 声明 MIT，发布元数据却未提供可核验的源码仓库与提交。这两项已登记在 npm 上游补充中，需要从维护者或可信的同版本发行材料取得文本，不能猜测版权人补写。
3. **平台与发行内容。** npm 的其他平台可选包、本地未构建的发行目标、.NET 自包含运行时和系统软件不由当前本地缓存证明。针对实际构建补充核对，特别留意原生二进制内嵌组件；canvas 等包的上游 MIT 文件也不能替代内嵌 Skia、ICU 等组件的声明。
4. **未随仓库提供的教学资料和数据。** 拟纳入发布包时另行确认其许可。源码中的引用链接不是对外部材料的再分发授权。

这些项目是许可材料的核验范围与缺口记录，不是更换项目 MIT 许可证的理由；当前快照检查通过也不表示它们已完成。
