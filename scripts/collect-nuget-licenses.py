#!/usr/bin/env python3
"""Collect local NuGet licensing evidence without restoring or contacting a server.

Run after restoring the intended release frameworks/RIDs. This is an evidence
inventory, not a legal compatibility decision or a complete native-library audit.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys
import xml.etree.ElementTree as ET


LICENSE_NAME = re.compile(
    r"^(licen[sc]es?|copying|copyright|notices?|third[-_ ]?party[-_ ]?(licen[sc]es?|notices?))($|[._ -])",
    re.IGNORECASE,
)


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def local_name(element: ET.Element) -> str:
    return element.tag.rsplit("}", 1)[-1]


def children_text(element: ET.Element) -> dict[str, str]:
    return {local_name(child): (child.text or "").strip() for child in element}


def contained(base: Path, relative: str) -> Path | None:
    candidate = (base / relative.replace("\\", "/")).resolve()
    try:
        candidate.relative_to(base.resolve())
        return candidate
    except ValueError:
        return None


def requirement(version: str) -> str:
    """Normalize the plain minimum versions used by this repository."""
    if re.fullmatch(r"\d+(?:\.\d+)+(?:[-+][A-Za-z0-9.-]+)?", version):
        return f"[{version}, )"
    return version


def reference_path(root: Path, project_dir: Path, value: str) -> str | None:
    """Normalize references without exposing external paths in generated evidence."""
    if not value or any(token in value for token in ("$", "*", "?", ";")):
        return None
    path = Path(value.replace("\\", "/"))
    if not path.is_absolute():
        path = project_dir / path
    try:
        return path.resolve().relative_to(root.resolve()).as_posix()
    except ValueError:
        return None


def project_manifest(root: Path, path: Path) -> tuple[list[str], list[dict], list[str], list[str]]:
    tree = ET.parse(path).getroot()
    frameworks = []
    references = []
    project_references = []
    issues = []
    for element in tree.iter():
        name = local_name(element)
        if name in ("TargetFramework", "TargetFrameworks"):
            frameworks.extend((element.text or "").split(";"))
        if name == "PackageReference":
            values = children_text(element)
            package = element.get("Include") or element.get("Update") or ""
            version = element.get("Version") or values.get("Version", "")
            references.append({"id": package, "requestedVersion": version})
            if not package or not version or "$" in version:
                issues.append(f"Cannot statically evaluate PackageReference: {package}")
        if name == "ProjectReference":
            value = element.get("Include") or element.get("Update") or ""
            normalized = reference_path(root, path.parent, value)
            if normalized is None:
                issues.append("Cannot statically evaluate a ProjectReference, or its target is outside the repository.")
            else:
                project_references.append(normalized)
        if element.get("Condition"):
            issues.append("Conditional MSBuild elements require manual evaluation.")
    frameworks = sorted(set(filter(None, (item.strip() for item in frameworks))))
    if not frameworks or any("$" in value for value in frameworks):
        issues.append("Cannot statically evaluate target frameworks.")
    return frameworks, sorted(references, key=lambda item: item["id"].lower()), sorted(set(project_references)), sorted(set(issues))


def read_package(package_id: str, version: str, path: Path | None) -> tuple[dict, dict]:
    record = {
        "id": package_id,
        "version": version,
        "cachePath": f"<NuGet package folder>/{package_id.lower()}/{version.lower()}",
        "license": None,
        "licenseUrl": None,
        "authors": None,
        "copyright": None,
        "repository": None,
        "projectUrl": None,
        "licenseFiles": [],
        "nativeAssets": [],
        "issues": [],
    }
    texts = {}
    if path is None:
        record["issues"].append("Package is not available in the asset file's local package folders.")
        record["licenseTextStatus"] = "missing-package"
        return record, texts
    nuspecs = sorted(path.glob("*.nuspec"))
    explicit_license = None
    if not nuspecs:
        record["issues"].append("Local package has no nuspec metadata.")
    else:
        nuspec = nuspecs[0]
        record["nuspec"] = {"file": nuspec.name, "sha256": sha256(nuspec.read_bytes())}
        metadata = next((e for e in ET.parse(nuspec).getroot() if local_name(e) == "metadata"), None)
        if metadata is not None:
            for element in metadata:
                name = local_name(element)
                value = (element.text or "").strip()
                if name == "license":
                    record["license"] = {"type": element.get("type"), "value": value}
                    if element.get("type") == "file":
                        explicit_license = contained(path, value)
                        if explicit_license is None or not explicit_license.is_file():
                            record["issues"].append("The nuspec's declared license file is missing or outside the package.")
                elif name == "repository":
                    record["repository"] = dict(element.attrib)
                elif name in ("licenseUrl", "authors", "copyright", "projectUrl"):
                    record[name] = value
    for candidate in sorted(path.rglob("*")):
        if not candidate.is_file():
            continue
        relative = candidate.relative_to(path).as_posix()
        is_notice = "notice" in candidate.name.lower()
        in_license_dir = any(p.lower() in ("license", "licenses", "licences") for p in candidate.relative_to(path).parts[:-1])
        if candidate != explicit_license and not LICENSE_NAME.match(candidate.name) and not in_license_dir:
            continue
        data = candidate.read_bytes()
        digest = sha256(data)
        try:
            text = data.decode("utf-8-sig").replace("\r\n", "\n").replace("\r", "\n")
        except UnicodeDecodeError:
            record["issues"].append(f"License/notice file could not be decoded as UTF-8: {relative}")
            continue
        kind = "notice" if is_notice else "license-or-copyright"
        record["licenseFiles"].append({"file": relative, "kind": kind, "sha256": digest})
        texts[digest] = text
    own_license = any(entry["kind"] == "license-or-copyright" for entry in record["licenseFiles"])
    record["licenseTextStatus"] = "local-text-found-review-scope" if own_license else "not-present-in-local-package"
    if not own_license:
        record["issues"].append("No standalone license/copyright text found; nuspec expressions and URLs are not license texts.")
    if record["license"] is None:
        record["issues"].append("No modern nuspec license declaration; inspect local text and upstream version.")
    return record, texts


def collect(root: Path, project_paths: list[str]) -> tuple[dict, str]:
    projects = []
    packages = {}
    text_bodies = {}
    for relative in sorted(project_paths):
        project_path = root / relative
        frameworks, references, project_references, issues = project_manifest(root, project_path)
        assets_path = project_path.parent / "obj" / "project.assets.json"
        project = {
            "project": Path(relative).as_posix(),
            "projectSha256": sha256(project_path.read_bytes()),
            "targetFrameworks": frameworks,
            "directReferences": references,
            "projectReferences": project_references,
            "assetsProjectReferences": {},
            "assets": assets_path.relative_to(root).as_posix(),
            "resolvedTargets": [],
            "resolvedPackages": [],
            "issues": issues,
        }
        projects.append(project)
        if not assets_path.is_file():
            project["issues"].append("Missing obj/project.assets.json; this project is not inventoried.")
            continue
        assets = json.loads(assets_path.read_text(encoding="utf-8-sig"))
        assets_frameworks = assets.get("project", {}).get("frameworks", {})
        restore_frameworks = assets.get("project", {}).get("restore", {}).get("frameworks", {})
        project["resolvedTargets"] = sorted(assets.get("targets", {}))
        if sorted(assets_frameworks) != frameworks:
            project["issues"].append("Assets target frameworks do not match the current csproj.")
        for framework in frameworks:
            if framework not in restore_frameworks:
                project["issues"].append(f"Missing restored ProjectReference metadata for {framework}.")
                continue
            actual_references = []
            for key, details in restore_frameworks[framework].get("projectReferences", {}).items():
                normalized = reference_path(root, project_path.parent, details.get("projectPath", key))
                if normalized is None:
                    project["issues"].append(f"An assets ProjectReference cannot be normalized inside the repository for {framework}.")
                else:
                    actual_references.append(normalized)
            project["assetsProjectReferences"][framework] = sorted(set(actual_references))
            if {Path(value) for value in actual_references} != {Path(value) for value in project_references}:
                project["issues"].append(f"Assets ProjectReference paths do not match the current csproj for {framework}.")
        expected = {item["id"].lower(): requirement(item["requestedVersion"]) for item in references}
        for framework, details in assets_frameworks.items():
            actual = {key.lower(): value.get("version", "") for key, value in details.get("dependencies", {}).items() if value.get("target") == "Package"}
            if actual != expected:
                project["issues"].append(f"Assets direct dependency requirements do not match the current csproj for {framework}.")
        folders = [Path(folder) for folder in assets.get("packageFolders", {})]
        for identity, library in sorted(assets.get("libraries", {}).items()):
            if library.get("type") != "package":
                continue
            package_id, version = identity.rsplit("/", 1)
            project["resolvedPackages"].append(identity)
            if identity not in packages:
                paths = [contained(folder, library.get("path", "")) for folder in folders]
                local = next((path for path in paths if path is not None and path.is_dir()), None)
                record, texts = read_package(package_id, version, local)
                record["projects"] = []
                record["directIn"] = []
                record["nativeAssets"] = sorted(value for value in library.get("files", []) if "/native/" in value)
                if record["nativeAssets"]:
                    record["issues"].append("Native assets require a separate audit of bundled/static dependencies; the NuGet license is not a complete component inventory.")
                packages[identity] = record
                text_bodies.update(texts)
            record = packages[identity]
            record["projects"].append(project["project"])
            if package_id.lower() in expected:
                record["directIn"].append(project["project"])
    package_list = [packages[key] for key in sorted(packages, key=str.lower)]
    result = {
        "schemaVersion": 1,
        "generator": "scripts/collect-nuget-licenses.py",
        "scope": "Git-tracked csproj files, their current local obj/project.assets.json, and local NuGet cache metadata/texts. No restore, build, network request, or platform completeness assertion.",
        "limitations": [
            "A local restore may omit other target runtime identifiers or publication modes; repeat for each actual release artifact.",
            "Static csproj comparison does not evaluate arbitrary imported MSBuild props/targets; review custom or conditional builds separately.",
            "Package license metadata does not enumerate bundled native, generated, embedded, or statically linked components.",
            "Absence of a local license/NOTICE file does not remove upstream redistribution obligations.",
            "This inventory is evidence, not a certification that a binary distribution is ready for release.",
        ],
        "summary": {
            "projects": len(projects),
            "projectsWithIssues": sum(bool(p["issues"]) for p in projects),
            "uniquePackageVersions": len(package_list),
            "packagesWithoutStandaloneLicenseText": sum(p["licenseTextStatus"] != "local-text-found-review-scope" for p in package_list),
            "uniqueLocalTextFiles": len(text_bodies),
            "packagesWithNativeAssets": sum(bool(p["nativeAssets"]) for p in package_list),
        },
        "projects": projects,
        "packages": package_list,
    }
    notice = [
        "SwitchYard - local NuGet license and NOTICE evidence",
        "Generated by scripts/collect-nuget-licenses.py. Do not edit manually.",
        "",
        "This collection is INCOMPLETE for redistribution whenever upstream texts or",
        "bundled native dependencies have not been supplied and reviewed. See the",
        "companion nuget-inventory.json for exact scope, metadata, and missing evidence.",
        "No dependency has been relicensed under SwitchYard's MIT license.",
        "",
        "PACKAGE DECLARATIONS (nuspec metadata; not a substitute for license texts)",
    ]
    for package in package_list:
        license_data = package["license"]
        declaration = f"{license_data['type']}: {license_data['value']}" if license_data else "No license element (inspect upstream/local texts)"
        notice.extend(["", f"{package['id']} {package['version']}", f"License declaration: {declaration}"])
        for field in ("copyright", "authors", "licenseUrl", "projectUrl"):
            if package[field]:
                notice.append(f"{field}: {package[field]}")
        notice.append(f"Local standalone text: {package['licenseTextStatus']}")
    notice.extend(["", "LOCAL LICENSE/NOTICE TEXTS (identical raw files deduplicated by SHA-256; line endings normalized)"])
    for digest in sorted(text_bodies):
        sources = [f"{p['id']}/{p['version']}: {file['file']}" for p in package_list for file in p["licenseFiles"] if file["sha256"] == digest]
        notice.extend(["", "=" * 78, f"SHA-256: {digest}", *sources, "-" * 78, text_bodies[digest].rstrip(), ""])
    return result, "\n".join(notice) + "\n"


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="Check generated evidence against the current local restore without writing.")
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    try:
        output = subprocess.check_output(["git", "-C", str(root), "ls-files", "-z", "--", "*.csproj"])
        projects = [path for path in output.decode("utf-8").split("\0") if path]
        if not projects:
            raise ValueError("No Git-tracked csproj files were found.")
        inventory, notice = collect(root, projects)
    except (OSError, ValueError, ET.ParseError, subprocess.CalledProcessError) as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 1
    outputs = {
        root / "docs/legal/nuget-inventory.json": json.dumps(inventory, ensure_ascii=False, indent=2) + "\n",
        root / "docs/legal/nuget-NOTICES.txt": notice,
    }
    changed = []
    for path, text in outputs.items():
        if args.check:
            if not path.is_file() or path.read_text(encoding="utf-8") != text:
                changed.append(path.relative_to(root).as_posix())
        else:
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(text, encoding="utf-8", newline="\n")
    summary = inventory["summary"]
    print(json.dumps(summary, ensure_ascii=False))
    if changed:
        print("STALE: " + ", ".join(changed))
        return 1
    if summary["projectsWithIssues"]:
        print("ERROR: Project evidence is missing, stale, or requires manual MSBuild evaluation; inspect inventory projects[].issues.")
        return 1
    print("Evidence matches local restore." if args.check else "Evidence written. Review missing upstream texts and bundled native dependencies before redistribution.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
