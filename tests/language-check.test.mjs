import test from 'node:test';
import assert from 'node:assert/strict';
import { EventEmitter } from 'node:events';
import { mkdirSync, mkdtempSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import { resolve, join } from 'node:path';
import { spawnSync } from 'node:child_process';
import { evaluate, loadEnvironment, parseArguments, preflight } from '../tools/language-check/core.mjs';
import { collectSpeech, pcmToWav } from '../tools/language-check/speech.mjs';
import { createServices } from '../tools/language-check/openai-adapter.mjs';

const configured = { OPENAI_API_KEY: 'test-secret-never-send' };
const audioFile = () => ({ isFile: () => true, size: 100 });
const options = () => parseArguments(['--sn', 'shona.mp3', '--xh', 'xhosa.wav', '--consent'], configured);

function temporaryDirectory(t) {
  const root = resolve('.local/language-check-tests');
  mkdirSync(root, { recursive: true });
  const directory = mkdtempSync(join(root, 'run-'));
  t.after(() => {
    if (!resolve(directory).startsWith(root + '\\') && !resolve(directory).startsWith(root + '/')) throw new Error('Unsafe test cleanup path.');
    rmSync(directory, { recursive: true, force: true });
  });
  return directory;
}

test('Preflight_MissingInputs_ReportsBlockersWithoutSecrets', () => {
  const report = preflight(parseArguments([], {}), {});
  assert.equal(report.status, 'BLOCKED');
  assert.equal(report.mode, 'PREFLIGHT');
  assert.equal(report.qualityVerdict, 'NOT_REVIEWED');
  assert.equal(report.blockers.length, 3);
});

test('Preflight_ValidShonaXhosa_ReportsReadyAndPlannedCalls', () => {
  const report = preflight(options(), configured, audioFile);
  assert.equal(report.status, 'READY');
  assert.deepEqual(report.languages, ['sn', 'xh']);
  assert.deepEqual(report.plannedCalls, { transcriptions: 2, translations: 2, speechGenerations: 4 });
  assert.ok(!JSON.stringify(report).includes(configured.OPENAI_API_KEY));
});

test('Preflight_InvalidClipSizesFormatsAndIdentity_RejectsInput', () => {
  for (const size of [0, 25_000_001]) assert.equal(preflight(options(), configured, () => ({ isFile: () => true, size })).status, 'BLOCKED');
  assert.equal(preflight(options(), configured, () => ({ isFile: () => false, size: 100 })).status, 'BLOCKED');
  assert.equal(preflight(options(), configured, () => { throw new Error('missing'); }).status, 'BLOCKED');
  const invalid = options();
  invalid.clips.xh = invalid.clips.sn;
  assert.equal(preflight(invalid, configured, audioFile).status, 'BLOCKED');
  invalid.clips.xh = 'program.exe';
  assert.equal(preflight(invalid, configured, audioFile).status, 'BLOCKED');
});

test('Arguments_UnknownDuplicateOrMissingValue_RejectsArguments', () => {
  for (const args of [['--unknown'], ['--sn'], ['--sn', '--live'], ['--sn', 'a.mp3', '--sn', 'b.mp3']]) assert.throws(() => parseArguments(args, {}));
  assert.equal(preflight(parseArguments(['--sn', 'a.mp3', '--xh', 'b.mp3', '--sw', 'c.mp3', '--consent'], configured), configured, audioFile).status, 'BLOCKED');
});

test('Environment_LocalOverridesBaseAndProcessWins_NoShellEvaluation', (t) => {
  const root = temporaryDirectory(t);
  writeFileSync(join(root, '.env'), 'OPENAI_API_KEY=base\nMODEL=base\nLITERAL=$(do-not-run)\n');
  writeFileSync(join(root, '.env.local'), 'MODEL="local"\n');
  const environment = loadEnvironment({ OPENAI_API_KEY: 'process' }, root);
  assert.equal(environment.OPENAI_API_KEY, 'process');
  assert.equal(environment.MODEL, 'local');
  assert.equal(environment.LITERAL, '$(do-not-run)');
  writeFileSync(join(root, '.env.local'), 'invalid entry');
  assert.throws(() => loadEnvironment({}, root), /Invalid .env.local entry/);
});

test('CLI_DefaultReadyPreflight_DoesNotWriteOrConnect', (t) => {
  const root = temporaryDirectory(t);
  const sn = join(root, 'sn.wav');
  const xh = join(root, 'xh.wav');
  writeFileSync(sn, 'offline fixture, not real audio');
  writeFileSync(xh, 'offline fixture, not real audio');
  const before = readdirSync(root);
  const result = spawnSync(process.execPath, [resolve('tools/language-check/cli.mjs'), '--sn', sn, '--xh', xh, '--consent'], {
    cwd: root, encoding: 'utf8', env: { ...process.env, ...configured, HTTP_PROXY: 'http://127.0.0.1:1', HTTPS_PROXY: 'http://127.0.0.1:1' }, timeout: 5000,
  });
  assert.equal(result.status, 0, result.stderr);
  assert.equal(JSON.parse(result.stdout).status, 'READY');
  assert.ok(!result.stdout.includes(configured.OPENAI_API_KEY));
  assert.deepEqual(readdirSync(root), before);
});

test('CLI_BlockedLiveMode_DoesNotWriteOutputsOrSendRecordings', (t) => {
  const root = temporaryDirectory(t);
  const result = spawnSync(process.execPath, [resolve('tools/language-check/cli.mjs'), '--live'], {
    cwd: root, encoding: 'utf8', env: { ...process.env, OPENAI_API_KEY: '' }, timeout: 5000,
  });
  assert.equal(result.status, 2);
  assert.equal(JSON.parse(result.stdout).status, 'BLOCKED');
  assert.deepEqual(readdirSync(root), []);
});

test('Evaluation_SuccessfulGeneration_LeavesAllQualityScoresUnreviewed', async () => {
  const files = new Map();
  const report = await evaluate(options(), {
    transcribe: async (_, language) => `${language} source`,
    translate: async (_, source, target) => ({ translation: `${source} to ${target}`, notes: ['Review this proverb.'] }),
    speak: async (text) => ({ wav: pcmToWav(Buffer.alloc(20)), transcript: text }),
  }, async (name, data) => files.set(name, data));
  assert.equal(report.processingStatus, 'PASS');
  assert.equal(report.qualityVerdict, 'NOT_REVIEWED');
  assert.equal(files.size, 14);
  assert.ok(files.has('sn/xh/speech.wav'));
  assert.ok(files.has('xh/sn/speech.wav'));
  assert.ok(report.clips.every((clip) => clip.review.transcription === null));
  assert.ok(report.clips.flatMap((clip) => clip.versions).every((version) => version.review.voicePronunciation === null));
});

test('Evaluation_OneTranscriptionFails_ContinuesOtherClipWithoutLeakingErrors', async () => {
  const calls = [];
  const report = await evaluate(options(), {
    transcribe: async (_, source) => { calls.push(source); if (source === 'sn') throw Object.assign(new Error(configured.OPENAI_API_KEY), { status: 401 }); return 'xhosa source'; },
    translate: async () => ({ translation: 'translated', notes: [] }),
    speak: async (text) => ({ wav: pcmToWav(Buffer.alloc(20)), transcript: text }),
  }, async () => {});
  assert.equal(report.processingStatus, 'FAIL');
  assert.deepEqual(calls, ['sn', 'xh']);
  assert.equal(report.clips[0].stages[0].code, 'ACCESS_DENIED');
  assert.ok(!JSON.stringify(report).includes(configured.OPENAI_API_KEY));
});

test('Evaluation_InvalidTranslationOrSpeech_RecordsFailureWithoutPretendingSuccess', async () => {
  for (const translation of [{ translation: '', notes: [] }, { translation: 'text', notes: [42] }]) {
    const report = await evaluate(options(), {
      transcribe: async () => 'source', translate: async () => translation,
      speak: async () => ({ wav: Buffer.alloc(0), transcript: '' }),
    }, async () => {});
    assert.equal(report.processingStatus, 'FAIL');
    assert.ok(report.clips.every((clip) => clip.stages.some((stage) => stage.stage === 'translation' && stage.status === 'FAIL')));
  }
});

test('Wave_ValidPcm_WritesPlayableHeaderAndRejectsInvalidAudio', () => {
  const pcm = Buffer.alloc(48);
  const wav = pcmToWav(pcm);
  assert.equal(wav.toString('ascii', 0, 4), 'RIFF');
  assert.equal(wav.readUInt32LE(24), 24000);
  assert.equal(wav.readUInt32LE(40), 48);
  assert.equal(wav.length, 92);
  assert.throws(() => pcmToWav(Buffer.alloc(0)));
  assert.throws(() => pcmToWav(Buffer.alloc(3)));
});

class FakeSocket extends EventEmitter {
  sent = [];
  send(message) { this.sent.push(JSON.parse(message)); }
  close() { this.emit('close'); }
  event(event) { this.emit('message', Buffer.from(JSON.stringify(event))); }
}

test('Speech_CompleteEventFlow_CollectsAudioAndWaitsForSessionUpdate', async () => {
  const socket = new FakeSocket();
  const response = collectSpeech(socket, 'quoted story', 'isiXhosa');
  socket.event({ type: 'session.created' });
  assert.equal(socket.sent.length, 1);
  socket.event({ type: 'session.updated' });
  socket.event({ type: 'session.updated' });
  assert.equal(socket.sent.length, 3);
  socket.event({ type: 'response.output_audio.delta', delta: Buffer.alloc(12).toString('base64') });
  socket.event({ type: 'response.output_audio_transcript.delta', delta: 'spoken ' });
  socket.event({ type: 'response.output_audio_transcript.done', transcript: 'spoken story' });
  socket.event({ type: 'response.done', response: { status: 'completed' } });
  const speech = await response;
  assert.equal(speech.wav.length, 56);
  assert.equal(speech.transcript, 'spoken story');
});

test('Speech_DisconnectErrorIncompleteResponseOrTimeout_Rejects', async () => {
  for (const event of [{ type: 'error', error: { message: configured.OPENAI_API_KEY } }, { type: 'response.done', response: { status: 'incomplete' } }]) {
    const socket = new FakeSocket();
    const response = collectSpeech(socket, 'text', 'Shona');
    socket.event(event);
    await assert.rejects(response, (error) => !error.message.includes(configured.OPENAI_API_KEY));
  }
  const socket = new FakeSocket();
  const response = collectSpeech(socket, 'text', 'Shona');
  socket.emit('close');
  await assert.rejects(response, /before completion/);
  await assert.rejects(collectSpeech(new FakeSocket(), 'text', 'Shona', 5), { name: 'TimeoutError' });
});

test('Adapter_Translation_UsesStructuredOutputAndRejectsIncompleteResponses', async () => {
  let request;
  const client = { responses: { create: async (body) => {
    request = body;
    return { status: 'completed', output_text: '{"translation":"translated","notes":[]}' };
  } } };
  const services = createServices(configured.OPENAI_API_KEY, options().models, { client });
  const result = await services.translate('source', 'sn', 'xh');
  assert.equal(result.translation, 'translated');
  assert.equal(request.text.format.type, 'json_schema');
  assert.equal(request.text.format.strict, true);
  assert.match(request.instructions, /Shona to isiXhosa/);
  assert.equal(request.input, '{"passage":"source"}');
  client.responses.create = async () => ({ status: 'incomplete', output_text: '' });
  await assert.rejects(services.translate('source', 'sn', 'xh'), /Incomplete/);
});

test('Adapter_Transcription_UsesCurrentLanguageHintsWithoutLegacyField', async (t) => {
  const root = temporaryDirectory(t);
  const path = join(root, 'source.wav');
  writeFileSync(path, pcmToWav(Buffer.alloc(20)));
  const services = createServices(configured.OPENAI_API_KEY, options().models, { client: { audio: { transcriptions: { create: async (body) => {
    assert.deepEqual(body.languages, ['xh']);
    assert.equal(body.language, undefined);
    assert.equal(body.model, 'gpt-transcribe');
    assert.equal(body.file.name, 'source.wav');
    assert.equal(Buffer.from(await body.file.arrayBuffer()).toString('ascii', 0, 4), 'RIFF');
    return { text: 'transcribed' };
  } } } } });
  assert.equal(await services.transcribe(path, 'xh'), 'transcribed');
});
