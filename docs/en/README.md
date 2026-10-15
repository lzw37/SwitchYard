# SwitchYard English documentation

[中文](../zh-CN/README.md) · [Bilingual index](../README.md) · [Project home](../../README.en.md)

This manual is for teachers, students, planners, deployment maintainers, and developers. It describes the repository implementation as of 11 October 2026, including workflows, calculation assumptions, persistence scope, and current limitations. The Chinese edition provides the same chapters with matching numbers.

The Docker deployment chapter was reviewed on 15 October 2026; each chapter's review date identifies its applicability.

## Read by task

| Goal | Suggested reading order |
| --- | --- |
| Understand and start using the application | 01 Overview → 02 User guide → relevant business chapter |
| Design a hump and analyze rolling behavior | 03 Hump design and simulation |
| Build station and train plans and calculate capacity | 04 Station capacity analysis → 05 Operation processes |
| Analyze facility relationships, conflicts, and queues | 06 Sketch layout analysis |
| Run the source locally | 07 Installation and first run |
| Deploy, upgrade, back up, restore, or migrate | 08 Deployment and operations |
| Deploy frontend and backend with Docker | 12 Docker deployment → 08 Deployment and operations |
| Develop the frontend or embed station layouts | 09 Development and integration |
| Integrate APIs or investigate revisions and related deletion | 10 API and data reference → HTTP API inventory |
| Distribute the application or update third party notices | 11 Licensing and third party notices |

## Complete contents

| Chapter | Coverage |
| --- | --- |
| [01 Overview](01-overview.md) | Entry points, workflows, architecture, directories, implementation boundaries, terminology, and license |
| [02 User guide](02-user-guide.md) | Login, registration, language, common controls, account management, courses, and permissions |
| [03 Hump design and simulation](03-hump.md) | Instances, planar/vertical profiles, wagons, conditions, energy heights, speed/time, headway, and playback |
| [04 Station capacity analysis](04-capacity.md) | Layouts, routes, occupancy timing, train plans, charts, solving, saturated plans, and simulation |
| [05 Operation processes](05-operation-process.md) | Activities, events, precedence, anchors, templates, generation, snapshots, and edit semantics |
| [06 Sketch layout analysis](06-sketch-layout.md) | Drawing facilities and routes, workflows, SLP, resource conflicts, crossing probabilities, and queues |
| [07 Installation and first run](07-installation.md) | Prerequisites, three databases, initialization, startup, API addresses, and the calculation agent |
| [08 Deployment and operations](08-deployment-operations.md) | Publishing, reverse proxy, systemd, course resources, upgrades, backups, migration, and troubleshooting |
| [09 Development and integration](09-development-integration.md) | Frontend structure, UI, test availability, standalone station Gateway, and host services |
| [10 API and data reference](10-api-data-reference.md) | Authentication, examples, errors, concurrent revisions, databases, copying, and deletion scope |
| [11 Open source licensing and third party notices](11-licensing.md) | Project MIT license, dependencies and assets, packaging, maintenance commands, and outstanding checks |
| [12 Docker deployment](12-docker-deployment.md) | Frontend/backend images, Compose, environment variables, external MySQL, HTTPS, persistence, and offline delivery |
| [Appendix HTTP API inventory](appendix-api-routes.md) | All 175 main service HTTP operations, standalone module endpoints, and SignalR notes |

## Archive and maintenance

The [documentation center](../README.md) organizes the current manual, historical material, and maintenance rules. Current workflows, calculations, deployment, and development documentation are maintained only in this directory and its Chinese counterpart. Root and module READMEs provide brief introductions and navigation.

The 17 original Markdown documents and one H5 asset are centralized in `docs/archive/`. Original paragraphs are retained, with historical notices and adjusted links in Markdown; the H5 file is preserved unchanged. Use the [archive index and relocation map](../archive/README.md) to find the original project descriptions, topic guides, module development notes, review records, promotional material, and their current chapter counterparts.

Older model versions, test outcomes, and code line references have historical context. Some test resources and demonstration GIFs were already absent before this reorganization; see the [missing-resource record](../archive/README.md#missing-resources). Follow the [documentation maintenance rules](../MAINTENANCE.md) when updating documentation.

## Reading conventions

Commands identify their working directory; `<...>` values are placeholders to replace. Instance, scheme, plan, template, and train IDs belong to different scopes and are not interchangeable. Units and formulas describe each module's actual behavior and do not change with the interface language.

“Implemented” means the current code provides the feature, not that every dataset or deployment has been validated. Historical test reports are not results for your current deployment. When the application changes, update matching numbered chapters in both languages and retain useful historical material with its original context.
