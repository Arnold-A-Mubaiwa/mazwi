import { readFileSync } from 'node:fs';
import { spawnSync } from 'node:child_process';
import { pathToFileURL } from 'node:url';

export function readChecks(markdown) {
  const section = markdown.match(/^## 2\.[^\n]*\n([\s\S]*?)(?=^## |$(?![\s\S]))/m)?.[1] ?? '';
  return ['Lint', 'Type-check', 'Test'].map((name) => {
    const lines = section.split(/\r?\n/).filter((line) => line.startsWith(`- ${name}:`));
    const command = lines.length === 1 ? lines[0].match(/^- [^:]+: `([^`]+)`\s*$/)?.[1] : undefined;
    return { name, command: command ?? null };
  });
}

export function runChecks(checks, run = spawnSync) {
  return checks.map(({ name, command }) => {
    if (!command) {
      return { name, command: null, status: 'BLOCKED', exitCode: null, output: 'No unique executable command in AGENTS.md; resolve the TODO or declaration.' };
    }
    const windows = process.platform === 'win32';
    const result = run(windows ? 'powershell.exe' : '/bin/sh', windows
      ? ['-NoProfile', '-NonInteractive', '-Command', `${command}\n$qualitySucceeded = $?\nif ($null -ne $LASTEXITCODE -and $LASTEXITCODE -ne 0) { exit $LASTEXITCODE }\nif (-not $qualitySucceeded) { exit 1 }`]
      : ['-c', command], {
      cwd: process.cwd(), encoding: 'utf8', timeout: 120000, maxBuffer: 4 * 1024 * 1024,
    });
    return {
      name, command, status: !result.error && result.status === 0 ? 'PASS' : 'FAIL',
      exitCode: result.status ?? null,
      output: [result.stdout, result.stderr, result.error?.message].filter(Boolean).join('\n').trim(),
    };
  });
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  try {
    const checks = runChecks(readChecks(readFileSync('AGENTS.md', 'utf8')));
    const status = checks.every((check) => check.status === 'PASS') ? 'PASS' : 'FAIL';
    console.log(JSON.stringify({ status, checks }, null, 2));
    process.exitCode = status === 'PASS' ? 0 : 1;
  } catch (error) {
    console.error(`FAIL: ${error.message}`);
    process.exitCode = 1;
  }
}
