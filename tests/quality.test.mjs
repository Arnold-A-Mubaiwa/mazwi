import test from 'node:test';
import assert from 'node:assert/strict';
import { readChecks, runChecks } from '../.codex/skills/repo-quality/scripts/check.mjs';

test('commands come only from the quality section; TODO and duplicates block execution', () => {
  const checks = readChecks('## 2. Install, run, build and test\n- Lint: `node lint.mjs`\n- Type-check: TODO\n- Test: `node --test`\n- Test: `other`\n## 3. Style\n- Type-check: `wrong`\n');
  assert.deepEqual(checks, [
    { name: 'Lint', command: 'node lint.mjs' },
    { name: 'Type-check', command: null },
    { name: 'Test', command: null },
  ]);
});

test('a failed check does not prevent later checks; missing checks never run', () => {
  let calls = 0;
  const results = runChecks([
    { name: 'Lint', command: 'lint' }, { name: 'Type-check', command: null },
    { name: 'Test', command: 'test' },
  ], () => ({ status: ++calls === 1 ? 2 : 0, stdout: 'evidence' }));
  assert.deepEqual(results.map(({ status }) => status), ['FAIL', 'BLOCKED', 'PASS']);
  assert.equal(calls, 2);
  assert.equal(results[0].exitCode, 2);
  assert.equal(results[0].output, 'evidence');
});

test('timeouts and command launch errors fail even without an exit code', () => {
  const [result] = runChecks([{ name: 'Test', command: 'test' }], () => ({
    status: null, error: new Error('timeout'),
  }));
  assert.equal(result.status, 'FAIL');
  assert.equal(result.exitCode, null);
  assert.match(result.output, /timeout/);
});

test('real shell execution preserves native failure codes and continues', () => {
  const results = runChecks([
    { name: 'failure probe', command: 'node -e "process.exit(7)"' },
    { name: 'success probe', command: 'node --version' },
  ]);
  assert.equal(results[0].status, 'FAIL');
  assert.equal(results[0].exitCode, 7);
  assert.equal(results[1].status, 'PASS');
});
