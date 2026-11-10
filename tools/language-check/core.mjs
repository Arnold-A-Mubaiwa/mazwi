import { existsSync, readFileSync, statSync } from 'node:fs';
import { dirname, extname, resolve } from 'node:path';
import { assertRunnable, validateArtifact, evaluate } from './workflow.mjs';

export { evaluate };
export const languageNames = { sn: 'Shona', xh: 'isiXhosa', sw: 'Swahili', en: 'English' };
export const defaultModels = { transcription: 'gpt-transcribe', translation: 'gpt-6-luna', speech: 'gpt-realtime-2.1-mini' };
export const stages = ['transcribe', 'review-transcript', 'translate', 'review-translation', 'voice', 'review-voice'];
const networkStages = ['transcribe', 'translate', 'voice'];

export function loadEnvironment(environment = process.env, root = process.cwd()) {
  const result = { ...environment };
  for (const file of ['.env', '.env.local']) {
    const path = resolve(root, file);
    if (!existsSync(path)) continue;
    for (const line of readFileSync(path, 'utf8').split(/\r?\n/)) {
      if (!line.trim() || line.trimStart().startsWith('#')) continue;
      const entry = line.match(/^([A-Za-z_][A-Za-z0-9_]*)=(.*)$/);
      if (!entry) throw new Error('Invalid local environment syntax; use NAME=value.');
      let value = entry[2].trim();
      if (value.length >= 2 && ((value.startsWith('"') && value.endsWith('"')) || (value.startsWith("'") && value.endsWith("'")))) value = value.slice(1, -1);
      if (!(entry[1] in environment)) result[entry[1]] = value;
    }
  }
  return result;
}

export function parseArguments(args, environment = {}) {
  const options = {
    stage: 'transcribe', live: false, apply: false, consent: false, help: false, clips: {},
    models: { transcription: environment.MAZWI_TRANSCRIPTION_MODEL || defaultModels.transcription,
      translation: environment.MAZWI_TRANSLATION_MODEL || defaultModels.translation,
      speech: environment.MAZWI_SPEECH_MODEL || defaultModels.speech },
  };
  const values = new Set(['stage', 'input', 'text', 'meaning', 'terms', 'source', 'target', 'language', 'reviewer', 'decision', 'pronunciation', 'naturalness']);
  const paths = new Set(['input', 'text', 'meaning', 'terms']);
  const seen = new Set();
  for (let index = 0; index < args.length; index++) {
    const argument = args[index];
    if (!argument.startsWith('--') || seen.has(argument)) throw new Error('Unknown or duplicate option. Use --help.');
    seen.add(argument);
    const key = argument.slice(2);
    if (['live', 'apply', 'consent', 'help'].includes(key)) options[key] = true;
    else if (values.has(key) || Object.hasOwn(languageNames, key)) {
      const value = args[++index];
      if (!value || value.startsWith('--')) throw new Error('An option value is missing.');
      if (Object.hasOwn(languageNames, key)) options.clips[key] = resolve(value);
      else options[key] = paths.has(key) ? resolve(value) : value;
    } else throw new Error('Unknown option. Use --help. The former --english pipeline is no longer available.');
  }
  if (!stages.includes(options.stage)) throw new Error('Choose a documented evaluation stage.');
  return options;
}

export function preflight(options, environment, inspect = statSync) {
  const blockers = [];
  const network = networkStages.includes(options.stage);
  if (network && !environment.OPENAI_API_KEY?.trim()) blockers.push('Configure OPENAI_API_KEY locally; never paste it into chat or tracked files.');
  if (network && !options.consent) blockers.push('Confirm permission to send this stage’s recordings/text using --consent.');
  if (network && options.apply) blockers.push('--apply is only for offline human review.');
  if (!network && options.live) blockers.push('Human review is offline; use --apply instead of --live.');
  if (options.target && options.stage !== 'translate') blockers.push('--target belongs only to the translate stage.');
  if ((options.pronunciation || options.naturalness) && options.stage !== 'review-voice') blockers.push('Voice scores belong only to review-voice.');
  if (network && (options.reviewer || options.decision || options.language)) blockers.push('Human review options cannot be used in a network stage.');
  if (options.input && options.source) blockers.push('--source is only for importing a reference without --input.');
  const sources = Object.keys(options.clips);
  const checkFile = (path, maximum, label) => {
    try {
      const details = inspect(path);
      if (!details.isFile() || details.size <= 0 || details.size > maximum) blockers.push(`${label} is empty, oversized, or not a regular file.`);
    } catch { blockers.push(`${label} is missing or unreadable.`); }
  };
  if (options.stage === 'transcribe') {
    if (sources.length < 1 || sources.length > 2) blockers.push('Provide one or two different recordings with --sn, --xh, --sw or --en.');
    for (const source of sources) {
      if (!['.mp3', '.mp4', '.mpeg', '.mpga', '.m4a', '.wav', '.webm'].includes(extname(options.clips[source]).toLowerCase())) blockers.push('Unsupported recording extension.');
      else checkFile(options.clips[source], 25_000_000, 'Recording');
    }
    if (new Set(Object.values(options.clips)).size !== sources.length) blockers.push('Use a separate recording for each source language.');
    if (options.input || options.text || options.meaning || options.terms || options.source || options.target) blockers.push('Transcription accepts recordings, not text artifacts.');
  } else {
    if (sources.length) blockers.push('Recordings are only accepted by the transcribe stage.');
    if (!options.input && !(options.stage === 'review-transcript' && options.text && options.source)) blockers.push('Provide --input, or import a corrected transcript with --source and --text.');
    if (options.input) checkFile(options.input, 200_000, 'Input artifact');
    for (const key of ['text', 'meaning', 'terms']) if (options[key]) checkFile(options[key], 40_000, 'Review reference file');
    if (options.stage === 'translate' && !Object.hasOwn(languageNames, options.target)) blockers.push('Choose the target language with --target sn|xh|sw|en.');
    if (!network) {
      if (!['approve', 'reject'].includes(options.decision) || !options.reviewer?.trim()) blockers.push('Human review requires --decision approve|reject and --reviewer.');
      if (!Object.hasOwn(languageNames, options.language)) blockers.push('Confirm the reviewed language with --language sn|xh|sw|en.');
    } else if (options.text || options.meaning || options.terms || options.source || options.decision || options.reviewer || options.language) blockers.push('Network stages consume reviewed artifacts; corrections belong in an offline review stage.');
    if (options.stage !== 'review-transcript' && (options.meaning || options.terms || options.source)) blockers.push('Source references belong in review-transcript.');
  }
  return {
    stage: options.stage, mode: options.live ? 'LIVE' : options.apply ? 'LOCAL_REVIEW' : 'PREFLIGHT',
    status: blockers.length ? 'BLOCKED' : 'READY', blockers, models: options.models,
    plannedCalls: { transcriptions: options.stage === 'transcribe' ? sources.length : 0,
      translations: options.stage === 'translate' ? 1 : 0, speechGenerations: options.stage === 'voice' ? 1 : 0 },
    qualityVerdict: 'NOT_REVIEWED', demoReady: false,
    notes: ['Each invocation runs one stage only. API completion is not language-quality approval.', 'Without --live or --apply this is read-only preflight. Private content is never printed.'],
  };
}

export function prepare(options, environment, inspect = statSync, read = readFileSync) {
  const readiness = preflight(options, environment, inspect);
  const inputs = {};
  if (readiness.status === 'BLOCKED') return { readiness, inputs };
  try {
    if (options.input) inputs.artifact = validateArtifact(JSON.parse(read(options.input, 'utf8').replace(/^\uFEFF/, '')));
    if (options.text) inputs.text = read(options.text, 'utf8').replace(/^\uFEFF/, '');
    if (options.meaning) inputs.meaning = read(options.meaning, 'utf8').replace(/^\uFEFF/, '');
    if (options.terms) inputs.terms = JSON.parse(read(options.terms, 'utf8').replace(/^\uFEFF/, ''));
    if (options.stage === 'review-voice') {
      const audio = resolve(dirname(options.input), 'speech.wav');
      const details = inspect(audio);
      if (!details.isFile() || details.size < 45 || details.size > 48_000 * 120 + 44) throw new Error('Voice audio is missing or oversized.');
      inputs.audio = read(audio);
    }
    assertRunnable(options, inputs);
  } catch (error) {
    readiness.status = 'BLOCKED';
    readiness.blockers.push(error?.safeMessage || 'Input artifact or human review is invalid, unreviewed, rejected, changed, or unreadable.');
  }
  return { readiness, inputs };
}
