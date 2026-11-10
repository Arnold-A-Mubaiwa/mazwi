import test from 'node:test';
import assert from 'node:assert/strict';
import { EventEmitter } from 'node:events';
import { mkdirSync, mkdtempSync, readdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { resolve, join } from 'node:path';
import { spawnSync } from 'node:child_process';
import { evaluate, loadEnvironment, parseArguments, preflight, prepare } from '../tools/language-check/core.mjs';
import { contentHash, requireApproved, summarizeArtifact, translationProblems } from '../tools/language-check/workflow.mjs';
import { collectSpeech, pcmToWav } from '../tools/language-check/speech.mjs';
import { createServices, uploadMetadata } from '../tools/language-check/openai-adapter.mjs';

const configured = { OPENAI_API_KEY: 'test-secret-never-send' };
const models = parseArguments([], {}).models;
const inspectAudio = () => ({ isFile: () => true, size: 100 });
const sourceText = 'Mhoro shamwari. Zita rangu ndiNyasha. Ndinogara kuMutare.';
const targetText = 'Molo mhlobo. Igama lam nguNyasha. Ndihlala eMutare.';
function temporaryDirectory(t) {
  const root = resolve('.local/language-check-tests'); mkdirSync(root, { recursive: true });
  const directory = mkdtempSync(join(root, 'run-'));
  t.after(() => { if (!resolve(directory).startsWith(root + '\\') && !resolve(directory).startsWith(root + '/')) throw new Error('Unsafe cleanup.'); rmSync(directory, { recursive: true, force: true }); });
  return directory;
}
function transcript() {
  return { schemaVersion: 2, kind: 'transcript', source: 'sn', text: sourceText, referenceEnglish: 'Hello friend. My name is Nyasha. I live in Mutare.', protectedTerms: ['Nyasha', 'Mutare'], model: 'human-reference', detectedLanguages: [], createdAt: 'fixed', review: null };
}
function approve(artifact) {
  const result = structuredClone(artifact);
  result.review = { decision: 'approve', reviewer: 'native test reviewer', language: result.kind === 'transcript' ? result.source : result.target, contentHash: contentHash(result) };
  return result;
}
function translation() {
  return { schemaVersion: 2, kind: 'translation', source: 'sn', target: 'xh', text: targetText,
    sourceArtifact: approve(transcript()), notes: [], model: models.translation, blockedReasons: [], review: null };
}
async function run(stage, inputs = {}, overrides = {}, services = {}) {
  const files = new Map();
  const options = { stage, models, clips: {}, ...overrides };
  const report = await evaluate(options, services, async (name, value) => files.set(name, value), inputs);
  return { report, files };
}
const outputArtifact = (files, name) => JSON.parse(files.get(name));

test('Preflight_TranscribeOneOrTwoClips_PlansOnlyTranscription', () => {
  const options = parseArguments(['--sn', 'shona.m4a', '--xh', 'xhosa.wav', '--consent'], configured);
  const ready = preflight(options, configured, inspectAudio);
  assert.equal(ready.status, 'READY'); assert.deepEqual(ready.plannedCalls, { transcriptions: 2, translations: 0, speechGenerations: 0 });
  assert.equal(ready.demoReady, false); assert.ok(!JSON.stringify(ready).includes(configured.OPENAI_API_KEY));
  assert.equal(preflight(parseArguments(['--sn', 'one.mp3', '--consent'], configured), configured, inspectAudio).status, 'READY');
});
test('Preflight_InvalidSizesFormatsDuplicatesAndModes_BlockWithoutCalls', () => {
  const options = parseArguments(['--sn', 'clip.wav', '--consent'], configured);
  for (const size of [0, 25_000_001]) assert.equal(preflight(options, configured, () => ({ isFile: () => true, size })).status, 'BLOCKED');
  assert.equal(preflight({ ...options, clips: { sn: 'a.wav', xh: 'a.wav' } }, configured, inspectAudio).status, 'BLOCKED');
  assert.equal(preflight({ ...options, clips: { sn: 'file.exe' } }, configured, inspectAudio).status, 'BLOCKED');
  assert.equal(preflight({ ...options, apply: true }, configured, inspectAudio).status, 'BLOCKED');
  assert.equal(preflight(parseArguments([], {}), {}).status, 'BLOCKED');
  for (const args of [['--unknown'], ['--toString', 'a.mp3'], ['--sn'], ['--sn', 'a.mp3', '--sn', 'b.mp3'], ['--english'], ['--stage', 'all']]) assert.throws(() => parseArguments(args, {}));
});
test('Environment_ProcessWinsAndNoShellExpressionsExecute', t => {
  const root = temporaryDirectory(t);
  writeFileSync(join(root, '.env'), 'OPENAI_API_KEY=base\nMODEL=base\nLITERAL=$(do-not-run)\n');
  writeFileSync(join(root, '.env.local'), 'MODEL="local"\n');
  const env = loadEnvironment({ OPENAI_API_KEY: 'process' }, root);
  assert.equal(env.OPENAI_API_KEY, 'process'); assert.equal(env.MODEL, 'local'); assert.equal(env.LITERAL, '$(do-not-run)');
  writeFileSync(join(root, '.env.local'), 'invalid entry'); assert.throws(() => loadEnvironment({}, root), /Invalid local environment/);
});
test('CLI_DefaultPreflight_IsReadOnlyAndSecretSafe', t => {
  const root = temporaryDirectory(t); const path = join(root, 'clip.wav'); writeFileSync(path, 'offline metadata fixture');
  const before = readdirSync(root);
  const child = spawnSync(process.execPath, [resolve('tools/language-check/cli.mjs'), '--sn', path, '--consent'], { cwd: root, encoding: 'utf8', env: { ...process.env, ...configured }, timeout: 5000 });
  assert.equal(child.status, 0, child.stderr); assert.equal(JSON.parse(child.stdout).status, 'READY');
  assert.deepEqual(readdirSync(root), before); assert.ok(!child.stdout.includes(configured.OPENAI_API_KEY));
});
test('CLI_UnreviewedLiveTranslation_DoesNotWriteOrContactApi', t => {
  const root = temporaryDirectory(t); const input = join(root, 'transcript.json'); writeFileSync(input, JSON.stringify(transcript()));
  const before = readdirSync(root);
  const child = spawnSync(process.execPath, [resolve('tools/language-check/cli.mjs'), '--stage', 'translate', '--input', input, '--target', 'xh', '--consent', '--live'], {
    cwd: root, encoding: 'utf8', env: { ...process.env, ...configured, HTTP_PROXY: 'http://127.0.0.1:1', HTTPS_PROXY: 'http://127.0.0.1:1' }, timeout: 5000,
  });
  assert.equal(child.status, 2); assert.equal(JSON.parse(child.stdout).status, 'BLOCKED'); assert.deepEqual(readdirSync(root), before);
  assert.ok(!child.stdout.includes(sourceText)); assert.ok(!child.stdout.includes(configured.OPENAI_API_KEY));
});
test('CLI_HumanReferenceReview_IsOfflineAndRequiresExplicitApply', t => {
  const root = temporaryDirectory(t); const input = join(root, 'verified.txt'); writeFileSync(input, sourceText);
  const args = [resolve('tools/language-check/cli.mjs'), '--stage', 'review-transcript', '--source', 'sn', '--text', input, '--language', 'sn', '--decision', 'approve', '--reviewer', 'native speaker'];
  const env = { ...process.env, OPENAI_API_KEY: '' };
  const preview = spawnSync(process.execPath, args, { cwd: root, encoding: 'utf8', env, timeout: 5000 });
  assert.equal(preview.status, 0); assert.equal(readdirSync(root).length, 1);
  const applied = spawnSync(process.execPath, [...args, '--apply'], { cwd: root, encoding: 'utf8', env, timeout: 5000 });
  assert.equal(applied.status, 0, applied.stderr);
  const results = applied.stdout.trim().split(/\r?\n/); assert.ok(results.length > 1); assert.ok(!applied.stdout.includes(sourceText));
  const generated = readdirSync(join(root, '.local/language-check'))[0];
  const approved = JSON.parse(readFileSync(join(root, '.local/language-check', generated, 'transcript.json'), 'utf8'));
  assert.equal(requireApproved(approved).source, 'sn');
});
test('Transcription_StopsBeforeTranslationAndVoice_EvenWhenApiSucceeds', async () => {
  let downstream = 0;
  const { report, files } = await run('transcribe', {}, { clips: { sn: 'clip.wav' } }, {
    transcribe: async () => ({ text: 'uncertain generated transcript', detectedLanguages: ['en'] }),
    translate: async () => { downstream++; }, speak: async () => { downstream++; },
  });
  assert.equal(downstream, 0); assert.equal(report.demoReady, false);
  const generated = outputArtifact(files, 'transcript-sn.json'); assert.equal(generated.review, null); assert.deepEqual(generated.detectedLanguages, ['en']);
});
test('Transcription_IndependentFailure_IsClassifiedWithoutLeakingSecrets', async () => {
  const { report } = await run('transcribe', {}, { clips: { sn: 'one.wav', xh: 'two.wav' } }, {
    transcribe: async (_, source) => { if (source === 'sn') throw Object.assign(new Error(configured.OPENAI_API_KEY), { status: 401 }); return { text: 'other transcript' }; },
  });
  assert.equal(report.processingStatus, 'FAIL'); assert.equal(report.artifacts.length, 1); assert.equal(report.failures[0].code, 'ACCESS_DENIED');
  assert.ok(!JSON.stringify(report).includes(configured.OPENAI_API_KEY));
});
test('TranscriptReview_CorrectionsReferencesAndTerms_BindExactContent', async () => {
  const { files } = await run('review-transcript', { text: sourceText, meaning: 'Verified English meaning', terms: ['Nyasha', 'Mutare'] }, { source: 'sn', language: 'sn', reviewer: 'human', decision: 'approve' });
  const artifact = outputArtifact(files, 'transcript.json');
  assert.equal(artifact.model, 'human-reference'); assert.equal(artifact.referenceEnglish, 'Verified English meaning');
  requireApproved(artifact); artifact.text += ' changed'; assert.throws(() => requireApproved(artifact), /stale/);
});
test('Translation_UnreviewedRejectedOrEditedSource_MakesNoApiCalls', async () => {
  for (const artifact of [transcript(), { ...approve(transcript()), review: { decision: 'reject' } }, { ...approve(transcript()), text: 'changed reference' }]) {
    let calls = 0;
    await assert.rejects(run('translate', { artifact }, { target: 'xh' }, { translate: async () => { calls++; } }));
    assert.equal(calls, 0);
  }
});
test('Translation_ApprovedReference_IsTextOnlyAndNeverAutoApproved', async () => {
  let voiceCalls = 0;
  const { report, files } = await run('translate', { artifact: approve(transcript()) }, { target: 'xh' }, {
    translate: async (value, source, target, reference) => {
      assert.equal(value, sourceText); assert.equal(reference.referenceEnglish, transcript().referenceEnglish); assert.deepEqual(reference.protectedTerms, ['Nyasha', 'Mutare']);
      assert.equal(source, 'sn'); assert.equal(target, 'xh');
      return { translation: targetText, notes: [], uncertain: false, sourceLanguage: 'sn', targetLanguage: 'xh', review: { decision: 'approve' } };
    }, speak: async () => { voiceCalls++; },
  });
  assert.equal(voiceCalls, 0); assert.equal(report.demoReady, false); assert.equal(report.qualityVerdict, 'NOT_REVIEWED');
  assert.equal(outputArtifact(files, 'translation.json').review, null);
});
test('TranslationAssessment_UncertainWrongLanguageCopiedAndLostNames_AreFlagged', () => {
  const baseline = { translation: targetText, notes: [], uncertain: false, sourceLanguage: 'sn', targetLanguage: 'xh' };
  for (const [change, expected] of [[{ uncertain: true }, 'MODEL_UNCERTAIN'], [{ targetLanguage: 'en' }, 'TARGET_LANGUAGE_MISMATCH'], [{ sourceLanguage: 'unknown' }, 'SOURCE_LANGUAGE_MISMATCH'], [{ translation: sourceText }, 'UNCHANGED_SOURCE'], [{ translation: 'Molo. Ndihlala eKugudu.' }, 'PROTECTED_TERM_MISSING'], [{ notes: ['The source is unclear.'] }, 'UNCERTAINTY_NOTES']]) {
    assert.ok(translationProblems(approve(transcript()), 'xh', { ...baseline, ...change }).includes(expected));
  }
});
test('TranslationReview_RequiresNativeLanguageConfirmationAndCorrectionsForFlags', async () => {
  const artifact = translation(); artifact.blockedReasons = ['MODEL_UNCERTAIN'];
  await assert.rejects(run('review-translation', { artifact }, { language: 'xh', reviewer: 'human', decision: 'approve' }), /Correct flagged/);
  await assert.rejects(run('review-translation', { artifact, text: targetText }, { language: 'en', reviewer: 'human', decision: 'approve' }), /confirm/);
  const { files } = await run('review-translation', { artifact, text: targetText }, { language: 'xh', reviewer: 'native speaker', decision: 'approve' });
  requireApproved(outputArtifact(files, 'translation.json'));
});
test('Voice_UnreviewedRejectedEditedOrFlaggedTranslation_NeverCallsSpeech', async () => {
  for (const artifact of [translation(), { ...approve(translation()), text: 'Changed target' }, approve({ ...translation(), blockedReasons: ['TARGET_LANGUAGE_MISMATCH'] }), { ...approve(translation()), review: { decision: 'reject' } }]) {
    let calls = 0;
    await assert.rejects(run('voice', { artifact }, {}, { speak: async () => { calls++; } })); assert.equal(calls, 0);
  }
});
test('Voice_ApprovedExactTranslation_StillNeedsNativeVoiceReview', async () => {
  const audio = pcmToWav(Buffer.alloc(20));
  const { report, files } = await run('voice', { artifact: approve(translation()) }, {}, { speak: async (value, target) => { assert.equal(value, targetText); assert.equal(target, 'xh'); return { wav: audio, transcript: targetText }; } });
  assert.equal(report.demoReady, false); const artifact = outputArtifact(files, 'voice.json');
  const approved = await run('review-voice', { artifact, audio }, { language: 'xh', reviewer: 'native listener', decision: 'approve', pronunciation: 'good', naturalness: 'ok' });
  assert.equal(approved.report.demoReady, true); assert.equal(approved.report.qualityVerdict, 'PASS');
  await assert.rejects(run('review-voice', { artifact, audio: Buffer.alloc(100) }, { language: 'xh', reviewer: 'human', decision: 'approve', pronunciation: 'good', naturalness: 'good' }), /changed/);
  await assert.rejects(run('review-voice', { artifact, audio }, { language: 'xh', reviewer: 'human', decision: 'approve', pronunciation: 'bad', naturalness: 'good' }), /scores/);
});
test('Voice_SpokenTextMismatchOrHumanRejection_CannotBeDemoReady', async () => {
  const audio = pcmToWav(Buffer.alloc(20));
  const { files } = await run('voice', { artifact: approve(translation()) }, {}, { speak: async () => ({ wav: audio, transcript: 'Unexpected introduction and different words' }) });
  const artifact = outputArtifact(files, 'voice.json'); assert.deepEqual(artifact.blockedReasons, ['SPOKEN_TEXT_MISMATCH']);
  await assert.rejects(run('review-voice', { artifact, audio }, { language: 'xh', reviewer: 'human', decision: 'approve', pronunciation: 'good', naturalness: 'good' }));
  const rejected = await run('review-voice', { artifact, audio }, { language: 'xh', reviewer: 'human', decision: 'reject', pronunciation: 'bad' });
  assert.equal(rejected.report.qualityVerdict, 'FAIL'); assert.equal(rejected.report.demoReady, false);
});
test('Prepare_MalformedOrLegacyInputs_BlockBeforeWritingOrConnecting', t => {
  const root = temporaryDirectory(t); const input = join(root, 'old.json'); writeFileSync(input, JSON.stringify({ processingStatus: 'PASS' }));
  const opts = parseArguments(['--stage', 'translate', '--input', input, '--target', 'xh', '--consent'], configured);
  assert.equal(prepare(opts, configured).readiness.status, 'BLOCKED');
  assert.equal(summarizeArtifact(approve(transcript())).demoReady, false);
});

test('Wave_ValidPcm_WritesHeaderAndRejectsInvalidAudio', () => {
  const wav = pcmToWav(Buffer.alloc(48)); assert.equal(wav.toString('ascii', 0, 4), 'RIFF'); assert.equal(wav.readUInt32LE(24), 24000); assert.equal(wav.length, 92);
  assert.throws(() => pcmToWav(Buffer.alloc(0))); assert.throws(() => pcmToWav(Buffer.alloc(3)));
});
class FakeSocket extends EventEmitter {
  sent = []; send(message) { this.sent.push(JSON.parse(message)); } close() { this.emit('close'); } event(event) { this.emit('message', Buffer.from(JSON.stringify(event))); }
}
test('Speech_CompleteEventFlow_WaitsForConfigurationAndCollectsAudio', async () => {
  const socket = new FakeSocket(); const response = collectSpeech(socket, 'quoted text', 'isiXhosa');
  socket.event({ type: 'session.created' }); assert.equal(socket.sent.length, 1);
  socket.event({ type: 'session.updated' }); socket.event({ type: 'session.updated' }); assert.equal(socket.sent.length, 3);
  socket.event({ type: 'response.output_audio.delta', delta: Buffer.alloc(12).toString('base64') });
  socket.event({ type: 'response.output_audio_transcript.done', transcript: 'spoken text' }); socket.event({ type: 'response.done', response: { status: 'completed' } });
  assert.equal((await response).wav.length, 56);
});
test('Speech_ErrorsDisconnectOrTimeout_RejectWithoutLeakingSecrets', async () => {
  for (const event of [{ type: 'error', error: { message: configured.OPENAI_API_KEY } }, { type: 'response.done', response: { status: 'incomplete' } }]) {
    const socket = new FakeSocket(); const result = collectSpeech(socket, 'text', 'Shona'); socket.event(event);
    await assert.rejects(result, error => !error.message.includes(configured.OPENAI_API_KEY));
  }
  const socket = new FakeSocket(); const result = collectSpeech(socket, 'text', 'Shona'); socket.emit('close'); await assert.rejects(result);
  await assert.rejects(collectSpeech(new FakeSocket(), 'text', 'Shona', 5), { name: 'TimeoutError' });
});
test('Adapter_MinimalTranscription_UsesMediaMetadataAndNoRejectedHintFields', async t => {
  const root = temporaryDirectory(t); const path = join(root, 'source.wav'); writeFileSync(path, pcmToWav(Buffer.alloc(20)));
  const services = createServices(configured.OPENAI_API_KEY, models, { client: { audio: { transcriptions: { create: async body => {
    assert.equal(body.languages, undefined); assert.equal(body.language, undefined); assert.equal(body.prompt, undefined);
    assert.equal(body.file.name, 'source.wav'); assert.equal(body.file.type, 'audio/wav'); return { text: 'transcribed', languages: [{ code: 'sn' }] };
  } } } } });
  assert.deepEqual(await services.transcribe(path, 'sn'), { text: 'transcribed', detectedLanguages: ['sn'] });
  assert.deepEqual(uploadMetadata('S1.m4a.mp4', Buffer.from('0000ftypM4A ')), { name: 'S1.m4a', type: 'audio/mp4' });
});
test('Adapter_Translation_UsesVerifiedMeaningProtectedNamesAndStrictUncertaintyFields', async () => {
  let request;
  const client = { responses: { create: async body => { request = body; return { status: 'completed', output_text: JSON.stringify({ translation: targetText, notes: [], uncertain: false, sourceLanguage: 'sn', targetLanguage: 'xh' }) }; } } };
  const services = createServices(configured.OPENAI_API_KEY, models, { client });
  await services.translate(sourceText, 'sn', 'xh', transcript());
  assert.deepEqual(JSON.parse(request.input).protectedTerms, ['Nyasha', 'Mutare']); assert.equal(JSON.parse(request.input).verifiedEnglishMeaning, transcript().referenceEnglish);
  assert.match(request.instructions, /Shona to isiXhosa/); assert.ok(request.text.format.schema.required.includes('uncertain'));
  client.responses.create = async () => ({ status: 'incomplete', output_text: '' });
  await assert.rejects(services.translate(sourceText, 'sn', 'xh'), /Incomplete/);
});
