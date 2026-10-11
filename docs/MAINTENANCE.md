# 文档维护约定 / Documentation maintenance

[文档中心 / Documentation center](README.md) · [中文手册](zh-CN/README.md) · [English manual](en/README.md) · [归档 / Archive](archive/README.md)

## 中文

1. **现行说明只维护一套双语正文。** 功能、操作、计算、安装、部署、运维、开发和接口说明放在 `docs/zh-CN/` 与 `docs/en/` 的对应章节。修改功能时同步更新两个目录中的同名文件，保持编号、标题层级、语言切换和目录链接对应；适用日期应与实际核对范围一致。
2. **入口只做导航。** `docs/README.md` 是总入口；仓库根目录和模块 README 只保留必要的简短介绍及导航链接，通常使用相对链接。随 NuGet/npm 包发布的站场模块 README 使用仓库文档的绝对网址，保证安装包内的导航有明确目标。检查脚本的 `ENTRIES` 列表登记了现有 6 个入口，每个入口最多 80 个非空行；确需新增模块入口时同步更新该列表。不要在入口重复整章操作或部署正文，也不要重建 `doc/` 等平行文档目录。
3. **归档保留历史上下文。** 需要保留的旧内容按需移入 `docs/archive/`，标明来源、归档日期和现行章节，更新[迁移对照](archive/README.md)。沿用 `project/`、`guides/`、`development/`、`reviews/`、`materials/` 分类。补充审阅记录、宣传素材等放入相应类别并标明性质；不要把同一份内容复制到多个位置。历史测试结论、旧行号及未入库资源应明确记录，不能写成当前验证结果。
4. **资源集中、链接相对。** 新增图片、动图和附件建议放在 `docs/assets/`，按主题设子目录并使用仓库内相对链接。目录在有实际资源时创建。移动文档时同步调整引用；缺失资源应说明状态，不能用不存在的路径冒充有效链接。
5. **提交前检查。** 更新相应目录和中英文链接，在仓库根目录运行下方检查命令。检查脚本使用 Python 3.10 或更高版本，无第三方依赖。链接检查不能代替对程序行为、命令、参数和计算描述的核对；按变更内容完成必要验证后再记录实际结果。
6. **许可材料按来源维护。** 项目根 `LICENSE` 保留标准许可正文；第三方声明、许可原文及生成清单集中在 `docs/legal/`。依赖或素材变化时按[许可维护说明](zh-CN/11-licensing.md)更新。供 npm 打包的站场前端 `LICENSE` 是根文件的同步副本；NuGet 直接打包根文件。这些发行用副本不作为独立文档正文维护。

## English

1. **Maintain one bilingual source for current documentation.** Keep feature, usage, calculation, installation, deployment, operations, development, and API documentation in matching chapters under `docs/zh-CN/` and `docs/en/`. Update same-named files together, preserving matching numbering, heading levels, language switches, and index links. Applicability dates must reflect what was actually reviewed.
2. **Keep entry points brief.** `docs/README.md` is the central index. Root and module READMEs contain only necessary short introductions and navigation links, normally relative. Station-layout READMEs distributed in NuGet/npm packages use absolute repository documentation URLs so that packaged navigation has an explicit destination. The checker's `ENTRIES` list registers the six existing entry points, each limited to 80 nonempty lines; update that list if a new module entry point is needed. Do not repeat full workflows or deployment chapters there, or recreate parallel documentation directories such as `doc/`.
3. **Preserve historical context in archives.** Move older material worth retaining into `docs/archive/` when needed. Record its source, archive date, and current chapter, and update the [relocation map](archive/README.md). Use the existing `project/`, `guides/`, `development/`, `reviews/`, and `materials/` categories. Place supporting review records and promotional assets in the appropriate category and identify their purpose. Do not duplicate the same content across locations. Label historical test results, old line references, and absent resources explicitly; do not present them as current validation.
4. **Centralize assets and use relative links.** Put new images, animations, and attachments under `docs/assets/`, with topic subdirectories and repository-relative links. Create directories when actual assets are added. Update references when moving documentation, and describe missing resources instead of presenting unavailable paths as working links.
5. **Check before submitting.** Update the relevant indexes and language links, then run the command below from the repository root. The checker requires Python 3.10 or newer and no third-party dependencies. Link checking does not replace reviewing behavior, commands, parameters, or calculation descriptions. Perform the validation needed for the changes and record only observed results.
6. **Maintain licensing material by source.** Preserve the standard project license in the root `LICENSE`; centralize third party notices, original license texts, and generated inventories in `docs/legal/`. Follow the [licensing guide](en/11-licensing.md) when dependencies or assets change. The station frontend's npm `LICENSE` is a synchronized copy of the root file, while NuGet packs the root file directly. These distribution copies are not separate documentation sources.

## 检查命令 / Check command

在仓库根目录执行 / Run from the repository root:

```bash
python scripts/check-docs.py
```

检查脚本 / Checker: [scripts/check-docs.py](../scripts/check-docs.py)

检查范围包括 UTF-8、代码围栏闭合、本地 Markdown 链接与锚点、代码行号范围、双语文件与标题层级对应，以及正文是否散落在 `docs/` 之外。成功返回 `0`，发现问题返回 `1`。脚本不会联网检查外部网址，也不检查 HTML 内嵌资源；这些内容需要另行核对。Git 忽略的本地资料不属于集中管理清单。

Checks cover UTF-8, closed code fences, local Markdown links and anchors, source line ranges, matching bilingual files and heading levels, and documentation outside `docs/`. The exit code is `0` on success and `1` when issues are found. External URLs and embedded HTML resources require separate review; the checker makes no network requests. Git-ignored local material is outside the managed inventory.
