# 11 Open source licensing and third party notices

[中文](../zh-CN/11-licensing.md) · [English contents](README.md) · [Documentation center](../README.md)

SwitchYard retains its existing MIT license. The additions cover independent dependency and asset notices, license files in distributed packages, and maintenance when dependencies change. This chapter reflects the licensing review of 11 October 2026; the accompanying inventories identify the exact package versions.

## Project license scope

The root [LICENSE](../../LICENSE) is the project license text and retains `Copyright (c) 2025 廖正文`. The project's own software and accompanying documentation are provided under that MIT text. Separately identified third party code, templates, icons, and dependencies retain their own licenses. A new calendar year or documentation reorganization does not require a replacement license or replacing upstream copyright holders with the project author.

MIT permits use, modification, and redistribution, including commercial use, subject to conditions including retention of the copyright and permission notices. Preserve the disclaimer as part of the full text. This chapter explains maintenance and does not add or remove license terms. See the [official MIT text](https://opensource.org/license/mit).

The root MIT file alone does not establish permissions for course videos, teaching materials, business data, or photographs and publications referenced on external sites. Ignored local resources are outside this source licensing inventory. Record their sources and permitted uses when distributing them separately.

## Notices and evidence

Supporting material is centralized in the [legal directory](../legal/README.md), rather than maintained as lengthy duplicate module documents.

| Material | Contents |
| --- | --- |
| [Source asset notices](../legal/source-assets-NOTICES.txt) | Sources, pinned upstream revisions, and licenses for icons, styles, create-vue, and the Git ignore template |
| [npm inventory](../legal/npm-inventory.json) / [packaged license texts](../legal/npm-NOTICES.txt) | Direct, transitive, development, and optional dependencies from the main lock file; local version checks and extracted notices |
| [NuGet inventory](../legal/nuget-inventory.json) / [packaged license texts](../legal/nuget-NOTICES.txt) | Version-controlled projects, current restore results, package metadata, native files, and locally supplied license texts |
| [npm upstream supplement](../legal/upstream-npm-NOTICES.txt) | Additional texts from verified upstream versions and unresolved items |
| [NuGet upstream supplement](../legal/upstream-nuget-NOTICES.txt) | Licenses and source records obtained from pinned upstream versions when packages omit the text |
| [Apache 2.0](../legal/licenses/Apache-2.0.txt), [Vue Theme MIT](../legal/licenses/vue-theme-LICENSE.txt), [template CC0](../legal/licenses/create-vue-template-CC0.txt) | Full original license texts for source assets |

Development dependencies and optional platform packages in the JSON inventories are not proof that those components appear in a deployed bundle. Conversely, a package-level SPDX identifier does not replace licenses for embedded fonts, native libraries, or other assets. Inventories retain states such as not installed, version mismatch, and missing packaged text. Upstream supplements do not rewrite those original observations.

## Components requiring separate treatment

- `IconTooling.vue` identifies MaterialDesign and Apache-2.0; the full license has been added. The Vue Theme palette in `base.css` retains Evan You's copyright and upstream MIT text.
- Verified create-vue starter files use CC0-1.0, with the icon's specific Apache notice retained separately. Vue's logo and favicon should not be described as original SwitchYard branding.
- In addition to its top-level Apache-2.0 license, `pdfjs-dist` contains CMap, font, ICC, and decoder licenses. The collector preserves them recursively; copying only its root `LICENSE` does not document all related resources.
- JSZip offers a choice including MIT and GPL; this project uses its MIT option for redistribution. The MIT and Zlib terms for `pako` are cumulative, not alternatives. The full texts are retained in the npm notices.
- The currently resolved NuGet version of `SixLabors.Fonts` is `1.0.0`, whose package declares Apache-2.0. Do not infer its terms from another version, and review the license again when upgrading.
- OR-Tools' package-level Apache-2.0 declaration does not complete a component-by-component review of its native libraries. See the remaining checks below and the NuGet inventory.

Redistribution of Apache-2.0 components requires the license text and applicable copyright and attribution notices, including upstream NOTICE material where supplied, as well as notices of changes to modified files. The actual requirements are in [Apache 2.0 section 4](https://www.apache.org/licenses/LICENSE-2.0). Preserve authoritative upstream license texts; translated maintenance guidance is not a substitute.

## What to include in distributions

Both npm manifests now declare `license: MIT`, with matching project records in the main lock file. The standalone station frontend's [LICENSE](../../SwitchYard.StationLayout/frontend/LICENSE) is a byte-for-byte copy of the root license for npm packaging; synchronize it when the root file changes. The main application retains `private: true`.

The standalone station backend's [project file](../../SwitchYard.StationLayout/backend/SwitchYard.StationLayout.csproj) declares `PackageLicenseExpression` as `MIT` and packs the root `LICENSE`. Its NuGet dependencies retain their own licenses; the host package's MIT declaration does not relicense them. See the field definitions in [npm documentation](https://docs.npmjs.com/cli/v11/configuring-npm/package-json/#license) and [NuGet documentation](https://learn.microsoft.com/en-us/nuget/reference/msbuild-targets#packing-a-license-expression-or-a-license-file).

When distributing application `dist`, API, Agent, self-contained packages, or container images, include the root `LICENSE`, third party notices applicable to the actual distributed content, and their referenced license texts. You can ship `docs/legal/` as the licensing material directory, preserving its relative `licenses/` structure; address the outstanding review items below first. Current `npm build` / `dotnet publish` commands do not automatically copy this entire directory into application output, so the publisher must do so. The verified npm and NuGet packaging checks establish only that the standalone station packages include the project's MIT metadata and file.

## Refreshing and checking

Prepare dependencies for the intended release environment using the installation guide. Run these commands at the repository root. They do not use the network, install or restore dependencies, start services, or access databases:

```bash
node scripts/collect-npm-licenses.mjs
python scripts/collect-nuget-licenses.py
node scripts/collect-npm-licenses.mjs --check
python scripts/collect-nuget-licenses.py --check
python scripts/check-docs.py
```

The first two commands regenerate local inventories and extracted texts. `--check` compares current evidence with saved outputs without writing them. It detects stale material, not licensing completeness of a release. The npm collector checks manifests against the lock file, installed package names and versions, and the project's MIT copy; structural mismatches cause failure. It also accepts `--strict` to fail on issues such as missing license text in installed packages; absent optional platform packages are recorded separately. The NuGet collector fails on structural issues such as mismatches between manifests, project references, and restore results, or missing restore assets, while recording missing packaged license texts in the inventory.

Collectors do not overwrite manually reviewed `source-assets-NOTICES.txt`, `upstream-*-NOTICES.txt`, or `licenses/` material. Preserve exact versions, source URLs, and original copyright statements when updating upstream supplements. Review notices after restoring a new target runtime, adding dependencies, replacing images or fonts, copying external code, or changing the lock file. Inventory `scope`, `limitations`, `findings`, and project issue fields describe the evidence boundaries.

## Remaining checks before distribution

1. **OR-Tools native components.** The current Windows runtime package includes native files for Abseil, bzip2, HiGHS, Protobuf, SCIP, UTF-8 validation, RE2, and zlib, without a complete set of individual license texts. Upstream builds also involve components such as Eigen. Establish actual contents, versions, and notices for the target platform and build options. An upstream list of possible dependencies is not proof that all are enabled, and the top-level Apache-2.0 declaration does not establish the absence of other obligations. See the [OR-Tools v9.15 build dependencies](https://github.com/google/or-tools/blob/v9.15/cmake/dependencies/CMakeLists.txt).
2. **Full notices for a small number of npm packages.** `dingbat-to-unicode 1.0.1` declares BSD-2-Clause, but full license and copyright text was not obtained from its corresponding official source revision. `lodash-unified 1.0.3` declares MIT, but its release metadata does not identify a verifiable source repository and commit. Both remain listed in the npm upstream supplement. Obtain the text from maintainers or trustworthy materials for the same release; do not invent copyright holders.
3. **Platforms and release contents.** Optional npm packages for other platforms, unbuilt release targets, self-contained .NET runtimes, and system software are not verified by the current local cache. Review the actual build, especially components embedded in native binaries. A package-level MIT file for canvas also does not replace notices for embedded Skia, ICU, and other components.
4. **Teaching resources and data not supplied with the repository.** Confirm their licenses separately if including them in a distribution. Source reference links are not permission to redistribute external material.

These are evidence boundaries and unresolved checks, not reasons to replace the project's MIT license. Passing the snapshot checks does not mark them complete.
