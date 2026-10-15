# SwitchYard 文档中心 / Documentation center

本套文档对应 2026-10-11 的程序实现，涵盖使用、计算、安装、部署、运维、开发接入和数据接口。中英文版本采用相同章节结构，可以逐章切换。

This documentation describes the implementation as of 11 October 2026, covering usage, calculations, installation, deployment, operations, integration, and APIs. Chinese and English editions have matching chapters and language links.

Docker 部署说明核对至 2026-10-15；各章以其核对日期为准。The Docker deployment chapter was reviewed on 15 October 2026; see each chapter's review date for its scope.

**[中文完整目录](zh-CN/README.md)** · **[Complete English manual](en/README.md)**

## 从这里开始 / Start here

| 任务 / Task | 中文 | English |
| --- | --- | --- |
| 使用程序 / Use the application | [用户指南](zh-CN/02-user-guide.md) | [User guide](en/02-user-guide.md) |
| 安装启动 / Install and run | [安装与首次启动](zh-CN/07-installation.md) | [Installation and first run](en/07-installation.md) |
| 部署维护 / Deploy and maintain | [部署与运维](zh-CN/08-deployment-operations.md) | [Deployment and operations](en/08-deployment-operations.md) |
| Docker 部署 / Deploy with Docker | [Docker 部署](zh-CN/12-docker-deployment.md) | [Docker deployment](en/12-docker-deployment.md) |
| 开发接入 / Develop and integrate | [开发与接入](zh-CN/09-development-integration.md) | [Development and integration](en/09-development-integration.md) |
| 查找旧文档 / Find an older document | [归档与迁移对照](archive/README.md) | [Archive and relocation map](archive/README.md) |
| 修改文档 / Update documentation | [文档维护约定](MAINTENANCE.md) | [Documentation maintenance](MAINTENANCE.md) |
| 查看许可与分发要求 / Review licenses and redistribution | [开源许可与第三方声明](zh-CN/11-licensing.md) | [Licensing and third party notices](en/11-licensing.md) |

## 完整手册 / Complete manual

| 章节 / Chapter | 中文 | English |
| --- | --- | --- |
| 01 | [程序总览](zh-CN/01-overview.md) | [Overview](en/01-overview.md) |
| 02 | [用户指南](zh-CN/02-user-guide.md) | [User guide](en/02-user-guide.md) |
| 03 | [驼峰设计与仿真](zh-CN/03-hump.md) | [Hump design and simulation](en/03-hump.md) |
| 04 | [通过能力分析](zh-CN/04-capacity.md) | [Station capacity analysis](en/04-capacity.md) |
| 05 | [作业过程编排](zh-CN/05-operation-process.md) | [Operation processes](en/05-operation-process.md) |
| 06 | [布置图分析](zh-CN/06-sketch-layout.md) | [Sketch layout analysis](en/06-sketch-layout.md) |
| 07 | [安装与首次启动](zh-CN/07-installation.md) | [Installation and first run](en/07-installation.md) |
| 08 | [部署与运维](zh-CN/08-deployment-operations.md) | [Deployment and operations](en/08-deployment-operations.md) |
| 09 | [开发与接入](zh-CN/09-development-integration.md) | [Development and integration](en/09-development-integration.md) |
| 10 | [接口与数据参考](zh-CN/10-api-data-reference.md) | [API and data reference](en/10-api-data-reference.md) |
| 11 | [开源许可与第三方声明](zh-CN/11-licensing.md) | [Licensing and third party notices](en/11-licensing.md) |
| 12 | [Docker 部署](zh-CN/12-docker-deployment.md) | [Docker deployment](en/12-docker-deployment.md) |
| 附录 / Appendix | [HTTP 接口清单](zh-CN/appendix-api-routes.md) | [HTTP API inventory](en/appendix-api-routes.md) |

## 文档放在哪里 / Where documentation lives

| 类别 / Type | 位置 / Location | 用途 / Purpose |
| --- | --- | --- |
| 现行说明 / Current documentation | [中文](zh-CN/README.md)、[English](en/README.md) | 唯一维护的功能、操作、部署和开发说明正文；matching chapters for current behavior, workflows, deployment, and development. |
| 历史与补充资料 / Historical and supporting material | [archive/](archive/README.md) | 原项目说明、专题指南、开发说明、审阅记录和宣传素材，按来源分类；original documents organized by source and purpose. |
| 许可资料 / Licensing material | [legal/](legal/README.md) | 第三方声明、许可原文和版本清单；third party notices, original license texts, and version inventories. |
| 就近入口 / Local entry points | 仓库根目录和模块目录的 `README` / repository and module READMEs | 简短介绍与指向本中心的链接；short introductions and links into this center. |

原有 17 份 Markdown 文档和 1 份 H5 素材已集中到 `archive/`。Markdown 原文段落保留，补充了历史标注并调整链接；H5 原样保留。根目录和模块 README 改为简短入口，其原有长篇内容可在归档中查阅。原 `doc/` 目录和分散的专题说明不再作为维护位置。完整的“原路径 → 归档位置 → 当前章节”对照只在[归档索引](archive/README.md)维护。

The 17 original Markdown documents and one H5 asset are centralized in `archive/`. Markdown paragraphs are retained with historical notices and adjusted links; the H5 file is preserved unchanged. Root and module READMEs are short entry points, with their former full content available in the archive. The former `doc/` directory and scattered topic guides are no longer maintenance locations. The [archive index](archive/README.md) is the single relocation map from original paths to archived copies and current chapters.

旧资料中的模型版本、测试命令、历史行号及验证结论仅代表当时情况；部分引用资源在本次整理前就未随仓库提供，详见[缺失资源记录](archive/README.md#missing-resources)。现行行为以中英文手册和代码为准。后续新增或修改资料请遵循[维护约定](MAINTENANCE.md)。

Older model versions, test commands, line references, and validation outcomes describe their historical context. Some referenced resources were not shipped with the repository before this reorganization; see the [missing-resource record](archive/README.md#missing-resources). Use the bilingual manual and code for current behavior, and follow the [maintenance rules](MAINTENANCE.md) when adding or updating documentation.
