import { mkdir, realpath, writeFile } from 'node:fs/promises';
import { randomUUID } from 'node:crypto';
import { dirname, isAbsolute, join, relative, resolve } from 'node:path';
import { evaluate, loadEnvironment, parseArguments, prepare } from './core.mjs';

try {
  const environment = loadEnvironment();
  const options = parseArguments(process.argv.slice(2), environment);
  if (options.help) {
    console.log('One stage per invocation: transcribe, review-transcript, translate, review-translation, voice, review-voice.');
    console.log('transcribe: --sn <audio> [--xh <audio>]; one or two sources, no automatic downstream calls.');
    console.log('review-transcript: --input <artifact.json> [--text <corrected.txt>] OR --source sn --text <verified.txt>; optional --meaning <English.txt> --terms <names.json>.');
    console.log('translate: --input <approved-transcript.json> --target xh.');
    console.log('review-translation: --input <translation.json> [--text <corrected.txt>].');
    console.log('voice: --input <approved-translation.json>.');
    console.log('review-voice: --input <voice.json> --pronunciation good|ok|bad --naturalness good|ok|bad.');
    console.log('Human reviews require --decision approve|reject --reviewer <name> --language <confirmed-code> --apply.');
    console.log('Network stages require --consent --live and incur API charges. Without --live/--apply, preflight is read-only.');
    console.log('Approvals bind exact content. Human language and voice review, not API success, determine demo readiness.');
  } else {
    const { readiness, inputs } = prepare(options, environment);
    console.log(JSON.stringify(readiness, null, 2));
    if (readiness.status === 'BLOCKED') process.exitCode = 2;
    else if (options.live || options.apply) {
      const root = resolve('.local/language-check');
      await mkdir(root, { recursive: true });
      const realRoot = await realpath(root);
      const workspaceRelative = relative(await realpath('.'), realRoot);
      if (isAbsolute(workspaceRelative) || workspaceRelative.startsWith('..')) throw new Error('Unsafe output root.');
      const outputDirectory = join(realRoot, `${new Date().toISOString().replaceAll(':', '-')}-${randomUUID()}-${options.stage}`);
      await mkdir(outputDirectory);
      const services = options.live ? (await import('./openai-adapter.mjs')).createServices(environment.OPENAI_API_KEY, options.models) : {};
      const output = async (name, content) => {
        const path = join(outputDirectory, name);
        const inside = relative(outputDirectory, path);
        if (isAbsolute(inside) || inside.startsWith('..')) throw new Error('Unsafe output file.');
        await mkdir(dirname(path), { recursive: true });
        await writeFile(path, content, { flag: 'wx' });
      };
      const report = await evaluate(options, services, output, inputs);
      report.generatedAt = new Date().toISOString();
      report.reviewInstructions = 'Review transcript, then translation, then voice separately. Do not claim language quality from API completion. Generated files are private; no content is published to Mazwi.';
      await output('review.json', JSON.stringify(report, null, 2) + '\n');
      console.log(JSON.stringify({ ...report, outputDirectory }, null, 2));
      process.exitCode = report.processingStatus === 'PASS' ? 0 : 1;
    }
  }
} catch {
  console.error('Language stage failed. Check the documented inputs, explicit human decisions, local environment and dependencies. Raw errors and private text are suppressed.');
  process.exitCode = 1;
}
