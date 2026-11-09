import { mkdir, realpath, writeFile } from 'node:fs/promises';
import { randomUUID } from 'node:crypto';
import { dirname, isAbsolute, join, relative, resolve } from 'node:path';
import { evaluate, loadEnvironment, parseArguments, preflight } from './core.mjs';

try {
  const environment = loadEnvironment();
  const options = parseArguments(process.argv.slice(2), environment);
  if (options.help) {
    console.log('Usage: node tools/language-check/cli.mjs --sn <file> --xh <file> --consent [--english] [--live]');
    console.log('Optional comparison: use --sw instead of one source. Exactly two source recordings are required.');
    console.log('Default: read-only offline preflight. --live sends both clips/text to OpenAI and incurs API charges. Outputs contain private content and stay under ignored .local/language-check/.');
  } else {
    const readiness = preflight(options, environment);
    console.log(JSON.stringify(readiness, null, 2));
    if (readiness.status === 'BLOCKED') {
      process.exitCode = 2;
    } else if (options.live) {
      const root = resolve('.local/language-check');
      await mkdir(root, { recursive: true });
      const realRoot = await realpath(root);
      const workspaceRelative = relative(await realpath('.'), realRoot);
      if (isAbsolute(workspaceRelative) || workspaceRelative.startsWith('..')) throw new Error('Output root must remain in the repository.');
      const outputDirectory = join(realRoot, `${new Date().toISOString().replaceAll(':', '-')}-${randomUUID()}`);
      await mkdir(outputDirectory);
      const { createServices } = await import('./openai-adapter.mjs');
      const services = createServices(environment.OPENAI_API_KEY, options.models);
      const output = async (name, content) => {
        const path = join(outputDirectory, name);
        await mkdir(dirname(path), { recursive: true });
        await writeFile(path, content, { flag: 'wx' });
      };
      const report = await evaluate(options, services, output);
      report.generatedAt = new Date().toISOString();
      report.reviewInstructions = 'A native speaker must score null fields good/ok/bad, record reviewer/consent, compare intended and spoken text, and decide audio vs subtitle fallback. Generated output is not evidence of language quality.';
      await output('review.json', JSON.stringify(report, null, 2) + '\n');
      console.log(JSON.stringify({ processingStatus: report.processingStatus, qualityVerdict: report.qualityVerdict, outputDirectory }, null, 2));
      process.exitCode = report.processingStatus === 'PASS' ? 0 : 1;
    }
  }
} catch {
  console.error('Language check failed. Check arguments, local environment syntax, installed dependencies, and output permissions. Raw errors are suppressed to avoid exposing credentials or private content.');
  process.exitCode = 1;
}
