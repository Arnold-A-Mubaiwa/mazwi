import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { spawnSync } from 'node:child_process';

// Dependency-free baseline: whitespace and JavaScript syntax, not semantic linting.
const failures = [];
let checked = 0;
function visit(directory) {
  for (const entry of readdirSync(directory, { withFileTypes: true })) {
    if (['.git', 'node_modules'].includes(entry.name) || entry.isSymbolicLink()) continue;
    const path = join(directory, entry.name);
    if (entry.isDirectory()) { visit(path); continue; }
    if (!/\.(md|mjs)$/.test(path)) continue;
    checked += 1;
    const text = readFileSync(path, 'utf8');
    text.split(/\r?\n/).forEach((line, index) => {
      if (/[\t ]+$/.test(line)) failures.push(`${path}:${index + 1}: trailing whitespace`);
    });
    if (!text.endsWith('\n')) failures.push(`${path}: missing final newline`);
    if (path.endsWith('.mjs')) {
      const result = spawnSync(process.execPath, ['--check', path], { encoding: 'utf8' });
      if (result.error || result.status !== 0) failures.push(`${path}: ${result.error?.message ?? result.stderr}`);
    }
  }
}
for (const root of process.argv.slice(2).length ? process.argv.slice(2) : ['.']) visit(root);
console.log(failures.length ? failures.join('\n') : `PASS: baseline whitespace and syntax checks (${checked} files)`);
process.exitCode = failures.length ? 1 : 0;
