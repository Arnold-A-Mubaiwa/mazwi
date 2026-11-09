import { existsSync, readFileSync, statSync } from 'node:fs';
import { extname, resolve } from 'node:path';

export const languageNames = { sn: 'Shona', xh: 'isiXhosa', sw: 'Swahili', en: 'English' };
export const defaultModels = {
  transcription: 'gpt-transcribe', translation: 'gpt-6-luna', speech: 'gpt-realtime-2.1-mini',
};

export function loadEnvironment(environment = process.env, root = process.cwd()) {
  const result = { ...environment };
  for (const file of ['.env', '.env.local']) {
    const path = resolve(root, file);
    if (!existsSync(path)) continue;
    for (const line of readFileSync(path, 'utf8').split(/\r?\n/)) {
      if (!line.trim() || line.trimStart().startsWith('#')) continue;
      const entry = line.match(/^([A-Za-z_][A-Za-z0-9_]*)=(.*)$/);
      if (!entry) throw new Error(`Invalid ${file} entry; use NAME=value.`);
      let value = entry[2].trim();
      if (value.length >= 2 && ((value.startsWith('"') && value.endsWith('"')) || (value.startsWith("'") && value.endsWith("'")))) value = value.slice(1, -1);
      if (!(entry[1] in environment)) result[entry[1]] = value;
    }
  }
  return result;
}

export function parseArguments(args, environment) {
  const options = {
    live: false, consent: false, english: false, help: false, clips: {},
    models: {
      transcription: environment.MAZWI_TRANSCRIPTION_MODEL || defaultModels.transcription,
      translation: environment.MAZWI_TRANSLATION_MODEL || defaultModels.translation,
      speech: environment.MAZWI_SPEECH_MODEL || defaultModels.speech,
    },
  };
  for (let index = 0; index < args.length; index += 1) {
    const argument = args[index];
    if (['--live', '--consent', '--english', '--help'].includes(argument)) {
      options[argument.slice(2)] = true;
    } else if (['--sn', '--xh', '--sw'].includes(argument)) {
      const value = args[++index];
      if (!value || value.startsWith('--') || options.clips[argument.slice(2)]) throw new Error('Provide each clip path once, after --sn, --xh, or --sw.');
      options.clips[argument.slice(2)] = resolve(value);
    } else {
      throw new Error('Unknown option. Use --help.');
    }
  }
  return options;
}

export function preflight(options, environment, inspect = statSync) {
  const blockers = [];
  if (!environment.OPENAI_API_KEY?.trim()) blockers.push('Configure OPENAI_API_KEY locally; never paste it into chat or tracked files.');
  if (!options.consent) blockers.push('Confirm permission to process both recordings using --consent.');
  const sources = Object.keys(options.clips);
  if (sources.length !== 2) blockers.push('Provide exactly two different source languages: --sn and --xh are the primary pair; --sw is optional instead of either.');
  for (const language of sources) {
    const path = options.clips[language];
    if (!path) { blockers.push(`Provide the ${languageNames[language]} clip with --${language}.`); continue; }
    if (!['.mp3', '.mp4', '.mpeg', '.mpga', '.m4a', '.wav', '.webm'].includes(extname(path).toLowerCase())) {
      blockers.push(`${languageNames[language]} clip: unsupported file extension.`); continue;
    }
    try {
      const details = inspect(path);
      if (!details.isFile() || details.size <= 0 || details.size > 25_000_000) blockers.push(`${languageNames[language]} clip must be a non-empty file no larger than 25 MB.`);
    } catch {
      blockers.push(`${languageNames[language]} clip is missing or unreadable.`);
    }
  }
  if (sources.length === 2 && options.clips[sources[0]] === options.clips[sources[1]]) blockers.push('Use separate recordings for the two source languages.');
  return {
    mode: options.live ? 'LIVE' : 'PREFLIGHT', status: blockers.length ? 'BLOCKED' : 'READY',
    models: options.models, blockers,
    languages: sources,
    plannedCalls: { transcriptions: 2, translations: options.english ? 4 : 2, speechGenerations: options.english ? 6 : 4 },
    qualityVerdict: 'NOT_REVIEWED',
    notes: ['Preflight makes no network calls and writes no output files.', 'Model access and actual language quality are not verified by preflight.'],
  };
}

export function failureCode(error) {
  if (error?.status === 401 || error?.status === 403) return 'ACCESS_DENIED';
  if (error?.status === 429) return 'RATE_LIMIT_OR_QUOTA';
  if (error?.status === 400 || error?.status === 404) return 'MODEL_OR_INPUT_REJECTED';
  if (error?.name === 'AbortError' || error?.name === 'TimeoutError') return 'TIMEOUT';
  return 'REQUEST_OR_OUTPUT_FAILED';
}

function requireText(text) {
  if (typeof text !== 'string' || !text.trim() || text.length > 6000) throw new Error('Invalid text output.');
  return text.trim();
}

export async function evaluate(options, services, output) {
  const report = { processingStatus: 'PASS', qualityVerdict: 'NOT_REVIEWED', models: options.models, clips: [] };
  const sources = Object.keys(options.clips);
  for (const source of sources) {
    const clip = { source, stages: [], review: { transcription: null, reviewer: null }, versions: [] };
    report.clips.push(clip);
    let transcript;
    try {
      transcript = requireText(await services.transcribe(options.clips[source], source));
      await output(`${source}/transcript.txt`, transcript);
      clip.stages.push({ stage: 'transcription', status: 'PASS' });
    } catch (error) {
      clip.stages.push({ stage: 'transcription', status: 'FAIL', code: failureCode(error) });
      report.processingStatus = 'FAIL';
      continue;
    }
    const targets = [source, ...sources.filter((language) => language !== source), ...(options.english ? ['en'] : [])];
    for (const target of targets) {
      const version = { target, status: 'PASS', review: { meaning: null, namesAndProverbs: null, voicePronunciation: null, voiceNaturalness: null }, notes: [] };
      clip.versions.push(version);
      let text = transcript;
      if (target !== source) {
        try {
          const translated = await services.translate(transcript, source, target);
          text = requireText(translated.translation);
          if (!Array.isArray(translated.notes) || translated.notes.some((note) => typeof note !== 'string')) throw new Error('Invalid translation notes.');
          version.notes = translated.notes;
          clip.stages.push({ stage: 'translation', target, status: 'PASS' });
        } catch (error) {
          clip.stages.push({ stage: 'translation', target, status: 'FAIL', code: failureCode(error) });
          version.status = report.processingStatus = 'FAIL';
          continue;
        }
      }
      try {
        await output(`${source}/${target}/text.txt`, text);
        const speech = await services.speak(text, target);
        if (!Buffer.isBuffer(speech.wav) || speech.wav.length <= 44) throw new Error('Empty speech audio.');
        await output(`${source}/${target}/speech.wav`, speech.wav);
        await output(`${source}/${target}/spoken-transcript.txt`, requireText(speech.transcript));
        version.audio = `${source}/${target}/speech.wav`;
        clip.stages.push({ stage: 'speech', target, status: 'PASS' });
      } catch (error) {
        clip.stages.push({ stage: 'speech', target, status: 'FAIL', code: failureCode(error) });
        version.status = report.processingStatus = 'FAIL';
      }
    }
  }
  return report;
}
