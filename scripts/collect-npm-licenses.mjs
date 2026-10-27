#!/usr/bin/env node
// Offline evidence collection; package declarations do not prove bundle compliance.
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const app = path.join(root, 'switchyard-vue');
const output = path.join(root, 'docs', 'legal');
const args = new Set(process.argv.slice(2));
if ([...args].some(arg => !['--check', '--strict'].includes(arg))) {
  console.error('Usage: node scripts/collect-npm-licenses.mjs [--check] [--strict]');
  process.exit(2);
}
const posix = value => value.replaceAll(path.sep, '/');
const relative = value => posix(path.relative(root, value));
const sha256 = value => crypto.createHash('sha256').update(value).digest('hex');
const readJson = file => JSON.parse(fs.readFileSync(file, 'utf8').replace(/^\uFEFF/, ''));
const withinRoot = file => {
  const rel = path.relative(root, file);
  return rel !== '..' && !rel.startsWith(`..${path.sep}`) && !path.isAbsolute(rel);
};
const lockPath = path.join(app, 'package-lock.json');
const lockBytes = fs.readFileSync(lockPath);
const lock = JSON.parse(lockBytes.toString('utf8'));
if (lock.lockfileVersion !== 3 || !lock.packages) {
  throw new Error('Expected switchyard-vue/package-lock.json lockfileVersion 3.');
}
const manifests = ['switchyard-vue/package.json', 'SwitchYard.StationLayout/frontend/package.json'];
const projects = manifests.map(file => {
  const bytes = fs.readFileSync(path.join(root, file));
  const data = JSON.parse(bytes.toString('utf8'));
  return { file, sha256: sha256(bytes), data };
});
const declaredLicense = data => data.license ?? data.licenses?.map(item => item.type).join(' OR ') ?? null;
const noticeName = /^(?:licen[sc]es?|copying|notices?|copyrights?)(?:[._ -].*|$)|^third[-_ ]?party.*(?:notice|licen[sc]e)/i;
const texts = new Map();
const findings = [];
const packages = [];
const ordered = value => Array.isArray(value) ? value.map(ordered)
  : value && typeof value === 'object' ? Object.fromEntries(Object.keys(value).sort().map(key => [key, ordered(value[key])])) : value;
const same = (left, right) => JSON.stringify(ordered(left)) === JSON.stringify(ordered(right));
for (const project of projects) {
  const key = posix(path.relative(app, path.dirname(path.join(root, project.file))));
  const entry = lock.packages[key];
  if (!entry) {
    findings.push({ package: project.data.name, version: project.data.version, lockPath: key,
      code: 'manifest-lock-entry-missing', structural: true });
    continue;
  }
  for (const field of ['name', 'version', 'license', 'dependencies', 'devDependencies',
    'peerDependencies', 'optionalDependencies', 'peerDependenciesMeta']) {
    const manifestValue = project.data[field] ?? (field.endsWith('Dependencies') || field === 'peerDependenciesMeta' ? {} : null);
    const lockValue = entry[field] ?? (field.endsWith('Dependencies') || field === 'peerDependenciesMeta' ? {} : null);
    if (!same(manifestValue, lockValue)) findings.push({ package: project.data.name,
      version: project.data.version, lockPath: key, code: 'manifest-lock-mismatch',
      structural: true, field, manifestValue, lockValue });
  }
}
const licensePaths = ['LICENSE', 'SwitchYard.StationLayout/frontend/LICENSE'];
const projectLicense = licensePaths.map(file => ({ file,
  sha256: fs.existsSync(path.join(root, file)) ? sha256(fs.readFileSync(path.join(root, file))) : null }));
if (!projectLicense[0].sha256 || projectLicense[0].sha256 !== projectLicense[1].sha256) {
  findings.push({ code: 'project-license-copy-mismatch', structural: true, files: projectLicense });
}

function decode(bytes) {
  try {
    return { text: new TextDecoder('utf-8', { fatal: true }).decode(bytes), encoding: 'utf-8' };
  } catch {
    // Legacy package license files can contain Windows-1252 copyright names.
    return { text: new TextDecoder('windows-1252').decode(bytes), encoding: 'windows-1252 (fallback; review)' };
  }
}

function addEvidence(file, kind, textOverride) {
  const bytes = fs.readFileSync(file);
  const decoded = decode(bytes);
  const text = textOverride ?? decoded.text;
  const textId = sha256(Buffer.from(text, 'utf8'));
  const source = relative(file);
  if (!texts.has(textId)) texts.set(textId, { id: textId, text, sources: [] });
  texts.get(textId).sources.push(source);
  return { file: source, sourceSha256: sha256(bytes), textId, encoding: decoded.encoding, kind };
}

function collectLicenseFiles(directory) {
  const result = [];
  function walk(current) {
    for (const entry of fs.readdirSync(current, { withFileTypes: true }).sort((a, b) => a.name.localeCompare(b.name, 'en'))) {
      const file = path.join(current, entry.name);
      // Each nested dependency is handled at its exact lock-file path.
      if (entry.isSymbolicLink() || entry.name === 'node_modules' || entry.name === '.git') continue;
      if (entry.isDirectory()) walk(file);
      else if (entry.isFile() && noticeName.test(entry.name)) result.push(addEvidence(file, 'license-or-notice-file'));
    }
  }
  walk(directory);
  return result;
}

function collectReadmeLicense(directory) {
  const result = [];
  for (const entry of fs.readdirSync(directory, { withFileTypes: true })) {
    if (!entry.isFile() || !/^readme(?:\.|$)/i.test(entry.name)) continue;
    const file = path.join(directory, entry.name);
    const { text } = decode(fs.readFileSync(file));
    const lines = text.split(/\r?\n/);
    const start = lines.findIndex((line, index) => /^#{1,6}\s+(?:licen[sc]e|copyright)(?:\s|$)/i.test(line)
      || (/^(?:licen[sc]e|copyright)\s*$/i.test(line) && /^[=-]{3,}\s*$/.test(lines[index + 1] ?? '')));
    if (start < 0) continue;
    const atx = lines[start].match(/^#+/);
    const level = atx ? atx[0].length : lines[start + 1].startsWith('=') ? 1 : 2;
    let end = start + (atx ? 1 : 2);
    while (end < lines.length) {
      const heading = lines[end].match(/^(#{1,6})\s/);
      if (heading && heading[1].length <= level) break;
      if (lines[end].trim() && /^[=-]{3,}\s*$/.test(lines[end + 1] ?? '')
          && (lines[end + 1].startsWith('=') ? 1 : 2) <= level) break;
      end++;
    }
    const section = lines.slice(start, end).join('\n').trim();
    // Preserve short attribution sections too, but never call them a full license.
    const kind = /permission is hereby granted/i.test(section) && /(?:software|program) is provided/i.test(section)
      ? 'readme-license-text' : 'readme-attribution-only';
    result.push(addEvidence(file, kind, section));
  }
  return result;
}

for (const [key, entry] of Object.entries(lock.packages).sort(([a], [b]) => a.localeCompare(b, 'en'))) {
  if (!key || entry.link) continue;
  const directory = path.resolve(app, key);
  if (!withinRoot(directory)) throw new Error(`Lock path outside workspace: ${key}`);
  const file = path.join(directory, 'package.json');
  const installed = fs.existsSync(file);
  if (installed && !withinRoot(fs.realpathSync(directory))) throw new Error(`Package symlink outside workspace: ${key}`);
  const local = installed ? readJson(file) : null;
  const pathName = key.split('node_modules/').at(-1);
  const name = entry.name ?? pathName;
  const installName = key.includes('node_modules/') ? pathName : name;
  const npmAlias = installName !== name;
  const nameMatching = installed && name === local.name;
  const matching = installed && entry.version === local.version && nameMatching;
  const aliases = Object.entries(lock.packages)
    .filter(([, value]) => value.link && value.resolved === key).map(([link]) => link);
  const declarations = projects.flatMap(project => ['dependencies', 'devDependencies', 'peerDependencies']
    .filter(kind => project.data[kind]?.[installName])
    .map(kind => ({ manifest: project.file, kind, range: project.data[kind][installName] })));
  const item = {
    name, installName, npmAlias, version: entry.version ?? null, lockPath: key,
    localPackage: !key.includes('node_modules/'), aliases,
    mainDirect: [key, ...aliases].includes(`node_modules/${installName}`)
      && ['dependencies', 'devDependencies'].some(kind => projects[0].data[kind]?.[installName]),
    dev: entry.dev === true, optional: entry.optional === true, devOptional: entry.devOptional === true,
    declarations, lockLicense: entry.license ?? null,
    installedLicense: local ? declaredLicense(local) : null,
    installedVersion: local?.version ?? null,
    repository: local?.repository ?? null,
    installation: !installed ? 'not-installed' : !nameMatching ? 'name-mismatch'
      : matching ? 'version-matched' : 'version-mismatch',
    notices: [], evidence: 'not-collected',
  };
  if (!installed) {
    item.evidence = 'not-installed';
    findings.push({ package: name, version: item.version, lockPath: key,
      code: item.optional ? 'optional-package-not-installed' : 'package-not-installed',
      ...(item.optional ? {} : { structural: true }) });
  } else if (!nameMatching) {
    item.evidence = 'name-mismatch';
    findings.push({ package: name, version: item.version, lockPath: key,
      code: 'package-name-mismatch', structural: true, installedName: local.name ?? null });
  } else if (!matching) {
    item.evidence = 'version-mismatch';
    findings.push({ package: name, version: item.version, lockPath: key,
      code: 'version-mismatch', structural: true, installedVersion: local.version });
  } else {
    item.notices = collectLicenseFiles(directory);
    // A third-party notice nested inside a package is not its own license.
    const rootNotice = item.notices.some(notice => path.dirname(path.join(root, notice.file)) === directory
      && /^(?:licen[sc]es?|copying)(?:[._ -].*|$)/i.test(path.basename(notice.file)));
    if (!rootNotice) item.notices.push(...collectReadmeLicense(directory));
    item.evidence = rootNotice ? 'license-files-collected'
      : item.notices.some(notice => notice.kind === 'readme-license-text') ? 'readme-license-text-collected'
      : item.notices.length ? 'partial-notices-only' : 'license-text-missing';
    if (['partial-notices-only', 'license-text-missing'].includes(item.evidence)) {
      findings.push({ package: name, version: item.version, lockPath: key, code: item.evidence });
    }
    if (!item.lockLicense && !item.installedLicense) findings.push({ package: name,
      version: item.version, lockPath: key, code: 'license-metadata-missing' });
    if (item.lockLicense && item.installedLicense && item.lockLicense !== item.installedLicense) {
      findings.push({ package: name, version: item.version, lockPath: key,
        code: 'license-metadata-mismatch', structural: true,
        lockLicense: item.lockLicense, installedLicense: item.installedLicense });
    }
    for (const notice of item.notices) if (notice.encoding !== 'utf-8') {
      findings.push({ package: name, version: item.version, lockPath: key,
        code: 'legacy-text-encoding-review', file: notice.file });
    }
  }
  packages.push(item);
}

const summary = {
  packages: packages.length,
  versionMatched: packages.filter(item => item.installation === 'version-matched').length,
  optionalNotInstalled: findings.filter(item => item.code === 'optional-package-not-installed').length,
  missingOrPartialLicenseText: packages.filter(item => ['partial-notices-only', 'license-text-missing'].includes(item.evidence)).length,
  noticeSources: packages.reduce((sum, item) => sum + item.notices.length, 0),
  distinctNoticeTexts: texts.size,
  structuralFailures: findings.filter(item => item.structural).length,
};
const inventory = {
  schemaVersion: 1,
  generator: 'scripts/collect-npm-licenses.mjs',
  scope: 'Main npm lock file, including direct/transitive, development, optional and local packages. This is dependency evidence, not a deployed bundle inventory or a compliance certificate.',
  limitations: [
    'No network access or package installation. Missing optional platform packages remain listed without license text.',
    'Only version-matched installed packages are read. A named license file is evidence, not proof that every embedded component is covered.',
    'License/notice/copyright files are collected recursively, excluding nested node_modules and symlinks; README license sections are a fallback.',
    'Manually copied source/assets and software outside this npm lock file require separate review.',
    'License expressions are preserved as declared; OR choices and additional asset licenses require review for the shipped artifact.',
    'Package identity is verified against lock.name when present (including npm aliases), otherwise against the node_modules path.',
  ],
  lock: { file: relative(lockPath), version: lock.lockfileVersion, sha256: sha256(lockBytes) },
  manifests: projects.map(({ file, sha256, data }) => ({ file, sha256, name: data.name,
    version: data.version, license: declaredLicense(data) })),
  projectLicense,
  summary, findings, packages,
};
const notices = [
  'SwitchYard npm third-party license evidence',
  'Generated by scripts/collect-npm-licenses.mjs. Do not edit this file manually.',
  `Lock file SHA-256: ${inventory.lock.sha256}`,
  '',
  'This collection preserves locally installed, version-matched package notices.',
  'It includes development/optional dependencies and unused bundled assets; it is',
  'not a statement that every listed item is shipped or that coverage is complete.',
  'See npm-inventory.json for versions, paths, license expressions, hashes and gaps.',
  '',
  'FINDINGS (including platform packages unavailable on this machine)',
  ...findings.map(item => `${item.package ? `${item.package}@${item.version}: ` : ''}${item.code}${item.field ? ` [${item.field}]` : ''}${item.file ? ` (${item.file})` : ''}`),
  '',
  'PACKAGE EVIDENCE INDEX',
  ...packages.flatMap(item => [
    `${item.name}@${item.version} [${item.lockPath}]`,
    `  Declared license: ${item.lockLicense ?? item.installedLicense ?? 'NOT DECLARED'}; evidence: ${item.evidence}`,
    ...item.notices.map(notice => `  ${notice.file} -> SHA-256 ${notice.textId}`),
  ]),
  '',
  ...[...texts.values()].sort((a, b) => a.id.localeCompare(b.id, 'en')).flatMap(item => [
    '='.repeat(78), `NOTICE TEXT SHA-256 ${item.id}`, ...[...new Set(item.sources)].sort(),
    '-'.repeat(78), item.text.trimEnd(), '',
  ]),
].join('\n') + '\n';
const outputs = new Map([
  ['npm-inventory.json', JSON.stringify(inventory, null, 2) + '\n'],
  ['npm-NOTICES.txt', notices],
]);
let outdated = false;
if (!args.has('--check')) fs.mkdirSync(output, { recursive: true });
for (const [name, content] of outputs) {
  const file = path.join(output, name);
  if (args.has('--check')) {
    if (!fs.existsSync(file) || fs.readFileSync(file, 'utf8') !== content) {
      console.error(`OUTDATED: ${relative(file)}`);
      outdated = true;
    }
  } else fs.writeFileSync(file, content, 'utf8');
}
console.log(JSON.stringify(summary));
const blocking = findings.filter(item => !['optional-package-not-installed', 'legacy-text-encoding-review'].includes(item.code));
if (findings.length) console.warn(`Recorded ${findings.length} findings (${blocking.length} non-platform/encoding findings); coverage is not certified complete.`);
if (outdated || summary.structuralFailures || (args.has('--strict') && blocking.length)) process.exitCode = 1;
