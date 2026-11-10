import { readdirSync, readFileSync } from 'node:fs';
import { basename, join } from 'node:path';
import { pathToFileURL } from 'node:url';

const dependencies = { Domain: [], Application: ['Domain'], Repository: ['Domain'], Ai: ['Domain'], WebApi: ['Application', 'Repository', 'Ai'] };
const sqlPattern = /\b(?:SELECT\s+.+?\s+FROM|INSERT\s+INTO|UPDATE\s+\w+\s+SET|DELETE\s+FROM|CREATE\s+TABLE|ALTER\s+TABLE|DROP\s+TABLE)\b/is;

export function inspectProject(layer, xml, files) {
  const failures = [];
  const references = [...xml.matchAll(/<ProjectReference\b[^>]*Include="([^"]+)"/g)]
    .map((match) => match[1].replaceAll('\\', '/').split('/').at(-1));
  const expected = dependencies[layer].map((name) => `Mazwi.${name}.csproj`);
  if (references.length !== expected.length || expected.some((name) => !references.includes(name))) {
    failures.push(`${layer}: expected project references [${expected.join(', ')}], found [${references.join(', ')}]`);
  }
  const packages = [...xml.matchAll(/<PackageReference\b[^>]*Include="([^"]+)"/g)].map((match) => match[1]);
  for (const name of packages) {
    const allowed = layer === 'Repository' && ['dapper', 'npgsql', 'dbup-postgresql'].includes(name.toLowerCase());
    if (!allowed) failures.push(`${layer}: package ${name} is outside this layer's allowlist`);
  }
  for (const [path, text] of files) {
    if (layer !== 'Repository' && (path.endsWith('.sql') || sqlPattern.test(text))) {
      failures.push(`${path}: SQL belongs only in Repository`);
    }
    if (layer === 'Repository' && sqlPattern.test(text) && /\$"|\$@"|@\$"|"\s*\+/.test(text)) {
      failures.push(`${path}: review interpolated or concatenated SQL; use parameters`);
    }
    if (layer === 'Domain' && /\b(?:Dapper|Npgsql|Microsoft\.AspNetCore|DbUp)\b/.test(text)) {
      failures.push(`${path}: infrastructure dependency in Domain`);
    }
    if (layer === 'Application' && /\b(?:Dapper|Npgsql|Microsoft\.AspNetCore|DbUp|Mazwi\.Repository|Mazwi\.Ai)\b/.test(text)) {
      failures.push(`${path}: infrastructure dependency in Application`);
    }
    if (layer === 'Ai' && /\b(?:Dapper|Npgsql|Microsoft\.AspNetCore|DbUp|Mazwi\.Repository|Mazwi\.Application|Mazwi\.WebApi)\b/.test(text)) {
      failures.push(`${path}: forbidden infrastructure or outward dependency in Ai`);
    }
  }
  return failures;
}

function sourceFiles(directory) {
  return readdirSync(directory, { withFileTypes: true }).flatMap((entry) => {
    if (['bin', 'obj'].includes(entry.name) || entry.isSymbolicLink()) return [];
    const path = join(directory, entry.name);
    return entry.isDirectory() ? sourceFiles(path)
      : /\.(cs|sql)$/.test(path) ? [[path, readFileSync(path, 'utf8')]] : [];
  });
}

export function checkArchitecture(root = '.') {
  const failures = [];
  const projectDirectories = readdirSync(join(root, 'src'), { withFileTypes: true }).filter((entry) => entry.isDirectory());
  if (projectDirectories.length !== 5) failures.push('Expected exactly five production projects.');
  for (const entry of projectDirectories) {
    const layer = entry.name.replace('Mazwi.', '');
    if (!(layer in dependencies)) { failures.push(`Unexpected production project: ${entry.name}`); continue; }
    const directory = join(root, 'src', entry.name);
    const xml = readFileSync(join(directory, `${basename(directory)}.csproj`), 'utf8');
    failures.push(...inspectProject(layer, xml, sourceFiles(directory)));
  }
  return failures;
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  const failures = checkArchitecture();
  console.log(failures.length ? failures.join('\n') : 'PASS: inward project references, package boundaries, and SQL location checks');
  process.exitCode = failures.length ? 1 : 0;
}
