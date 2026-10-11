#!/usr/bin/env python3
"""Check repository documentation without dependencies or network access.

Supports this repository's ATX/setext headings, fenced code, Markdown inline
and reference links, and GitHub-style heading/line anchors. HTML resources and
full CommonMark parsing are deliberately outside its scope.
"""

import html
import re
import subprocess
import sys
from collections import Counter
from pathlib import Path
from urllib.parse import unquote, urlsplit

ROOT = Path(__file__).resolve().parents[1]
ENTRIES = (
    "README.md", "README.en.md", "switchyard-vue/README.md",
    "SwitchYard.StationLayout/README.md",
    "SwitchYard.StationLayout/frontend/README.md",
    "SwitchYard.WebApi/SwitchYard.CapacityAgent/README.md",
)
FENCE = re.compile(r"^\s*(`{3,}|~{3,})(.*)$")
CODE = re.compile(r"(?<!`)(`+)(?!`)[\s\S]*?(?<!`)\1(?!`)")
LABEL = r"(?:\\.|[^\]\\\n])*"
DEST = r"<(?:\\.|[^>\n])*>|(?:\\.|\([^()\n]*\)|[^()\n])*"
LINK = re.compile(r"!?\[(" + LABEL + r")\](?:\((" + DEST + r")\)|\[(" + LABEL + r")\])?")
DEFINITION = re.compile(r"^ {0,3}\[(" + LABEL + r")\]:\s*(.+)$", re.M)
SOURCE_EXTENSIONS = {
    ".cs", ".py", ".ps1", ".sh", ".ts", ".tsx", ".js", ".jsx", ".mjs",
    ".cjs", ".vue", ".sql", ".json", ".yml", ".yaml", ".xml", ".csproj", ".css",
}


def blank(match):
    return re.sub(r"[^\n]", " ", match.group(0))


def label(value):
    return " ".join(value.split()).casefold()


def destination(value):
    """Extract a link destination, allowing optional Markdown titles."""
    value = value.strip()
    match = re.fullmatch(r'<([^>]+)>(?:\s+(?:".*"|\'.*\'|\(.*\)))?', value)
    if not match:
        match = re.fullmatch(r'(\S+?)(?:\s+(?:".*"|\'.*\'|\(.*\)))?', value)
    if not match:
        return None
    return html.unescape(re.sub(r"\\([!\"#$%&'()*+,\-./:;<=>?@\[\]^_`{|}~\\])", r"\1", match[1]))


class Checker:
    def __init__(self):
        self.errors = []
        self.documents = {}
        self.local_links = 0
        self.line_counts = {}

    def error(self, path, line, message):
        try:
            path = path.relative_to(ROOT)
        except ValueError:
            pass
        self.errors.append(f"{path.as_posix()}:{line}: {message}")

    def read(self, path):
        if path in self.documents:
            return self.documents[path]
        try:
            raw = path.read_text(encoding="utf-8-sig")
        except (OSError, UnicodeError) as exc:
            self.error(path, 1, f"Cannot read UTF-8 Markdown: {exc}")
            raw = ""
        lines, opened = [], None
        for number, line in enumerate(raw.splitlines(), 1):
            marker = FENCE.match(line)
            if opened:
                if marker and marker[1][0] == opened[0] and len(marker[1]) >= opened[1] and not marker[2].strip():
                    opened = None
                lines.append("")
            elif marker:
                opened = (marker[1][0], len(marker[1]), number)
                lines.append("")
            else:
                lines.append(line)
        if opened:
            self.error(path, opened[2], "Unclosed fenced code block")
        visible = "\n".join(lines)
        anchors = set(re.findall(r'<a\s+[^>]*(?:id|name)=["\']([^"\']+)["\']', visible, re.I))
        levels, occurrences = [], Counter()
        for index, line in enumerate(lines):
            heading = re.match(r"^ {0,3}(#{1,6})\s+(.+?)(?:\s+#+\s*)?$", line)
            if heading:
                level, title = len(heading[1]), heading[2]
            elif index and lines[index - 1].strip() and re.fullmatch(r" {0,3}(?:=+|-+)\s*", line):
                level, title = (1 if line.strip()[0] == "=" else 2), lines[index - 1]
            else:
                continue
            levels.append(level)
            title = LINK.sub(lambda match: match[1], title)
            title = html.unescape(re.sub(r"<[^>]+>", "", title))
            slug = re.sub(r"[^\w\- ]", "", title.lower()).replace(" ", "-")
            anchor = slug if not occurrences[slug] else f"{slug}-{occurrences[slug]}"
            while anchor in anchors:
                occurrences[slug] += 1
                anchor = f"{slug}-{occurrences[slug]}"
            occurrences[slug] += 1
            anchors.add(anchor)
        visible = CODE.sub(blank, visible)
        references = {label(m[1]): (m[2], visible.count("\n", 0, m.start()) + 1) for m in DEFINITION.finditer(visible)}
        body = DEFINITION.sub(blank, visible)
        links = [(value, number) for value, number in references.values()]
        for match in LINK.finditer(body):
            number = body.count("\n", 0, match.start()) + 1
            if match[2] is not None:
                links.append((match[2], number))
            elif match[3] is not None and label(match[3] or match[1]) not in references:
                self.error(path, number, f"Undefined reference: {match[3] or match[1]}")
        document = {"raw": raw, "anchors": anchors, "levels": levels, "links": links, "targets": set()}
        self.documents[path] = document
        return document

    def check_links(self, path):
        document = self.read(path)
        for value, line in document["links"]:
            target = destination(value)
            if target is None:
                self.error(path, line, f"Unsupported link destination; use <...> for spaces: {value}")
                continue
            if re.match(r"^(?:https?|mailto|ftp|tel|data):|^//", target, re.I):
                continue
            try:
                parsed = urlsplit(target)
                target_path = unquote(parsed.path).replace("\\", "/")
                resolved = (path.parent / target_path).resolve() if target_path else path
                fragment = unquote(parsed.fragment)
            except (ValueError, OSError) as exc:
                self.error(path, line, f"Invalid local link {target}: {exc}")
                continue
            self.local_links += 1
            document["targets"].add(resolved)
            if parsed.scheme or not resolved.exists():
                self.error(path, line, f"Missing/unsupported local target: {target}")
            elif fragment and resolved.suffix.lower() == ".md":
                if fragment not in self.read(resolved)["anchors"]:
                    self.error(path, line, f"Missing Markdown heading anchor: {target}")
            elif fragment and resolved.suffix.lower() in SOURCE_EXTENSIONS:
                anchor = re.fullmatch(r"L([1-9]\d*)(?:-L([1-9]\d*))?", fragment)
                if not anchor:
                    self.error(path, line, f"Unsupported source line anchor: {target}")
                    continue
                if resolved not in self.line_counts:
                    try:
                        self.line_counts[resolved] = len(resolved.read_bytes().splitlines())
                    except OSError as exc:
                        self.error(path, line, f"Cannot read source target {target}: {exc}")
                        continue
                first, last = int(anchor[1]), int(anchor[2] or anchor[1])
                if not 1 <= first <= last <= self.line_counts[resolved]:
                    self.error(path, line, f"Source line anchor out of range: {target}")

    def check_layout(self):
        try:
            result = subprocess.run(["git", "ls-files", "-z", "--cached", "--others", "--exclude-standard", "--", "*.md"], cwd=ROOT, capture_output=True, check=True)
            for name in set(result.stdout.decode("utf-8").split("\0")) - {""}:
                parts = Path(name).parts
                if not (ROOT / name).exists() or name.startswith("docs/") or name in ENTRIES:
                    continue
                if any(part.startswith(".") for part in parts) or parts[-1] in {"AGENTS.md", "SKILL.md"}:
                    continue
                self.error(ROOT / name, 1, "Documentation outside docs/; move full content into docs/ and use an approved short entry point")
        except (OSError, UnicodeError, subprocess.CalledProcessError) as exc:
            self.error(ROOT, 1, f"Cannot inspect Git documentation inventory: {exc}")
        for name in ENTRIES:
            path = ROOT / name
            if sum(bool(line.strip()) for line in self.read(path)["raw"].splitlines()) > 80:
                self.error(path, 1, "Entry point exceeds 80 nonempty lines; keep full documentation in docs/")
        editions = {lang: {p.relative_to(ROOT / "docs" / lang): p for p in (ROOT / "docs" / lang).rglob("*.md")} for lang in ("zh-CN", "en")}
        for lang, files in editions.items():
            if not files:
                self.error(ROOT / "docs" / lang, 1, "Missing bilingual documentation directory or Markdown files")
        for name in editions["zh-CN"].keys() | editions["en"].keys():
            pair = [editions[lang].get(name) for lang in ("zh-CN", "en")]
            if None in pair:
                self.error(next(p for p in pair if p), 1, "Missing corresponding bilingual document")
                continue
            left, right = (self.read(path) for path in pair)
            if left["levels"] != right["levels"]:
                self.error(pair[0], 1, f"Heading levels differ from docs/en/{name.as_posix()}")
            for source, other in (pair, pair[::-1]):
                if other.resolve() not in self.read(source)["targets"]:
                    self.error(source, 1, "Missing language-switch link to the corresponding document")
        if self.read(ROOT / "README.md")["levels"] != self.read(ROOT / "README.en.md")["levels"]:
            self.error(ROOT / "README.md", 1, "Root bilingual README heading levels differ")

    def run(self):
        paths = sorted(set((ROOT / "docs").rglob("*.md")) | {ROOT / name for name in ENTRIES})
        for path in paths:
            self.check_links(path)
        self.check_layout()
        for error in self.errors:
            print(f"ERROR {error}")
        print(f"{'FAIL' if self.errors else 'PASS'}: {len(paths)} Markdown files, {self.local_links} local links, {len(self.errors)} errors")
        print("Scope: UTF-8, fences, Markdown links/anchors, bilingual structure, centralization; HTML resources and external URLs are not checked.")
        return int(bool(self.errors))


if __name__ == "__main__":
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    sys.exit(Checker().run())
