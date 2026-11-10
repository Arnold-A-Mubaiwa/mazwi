import { createHash } from 'node:crypto';

const languages = ['sn', 'xh', 'sw', 'en'];
const maximumText = 6000;
function fail(message) { throw Object.assign(new Error(message), { safeMessage: message }); }
function text(value) {
  if (typeof value !== 'string' || !value.trim() || value.length > maximumText || /[\u0000-\u0008\u000b\u000c\u000e-\u001f\u007f-\u009f]/.test(value)) fail('Text must contain 1–6,000 characters without invalid control characters.');
  return value.trim();
}
function language(value) { if (!languages.includes(value)) fail('Unsupported source or target language.'); return value; }
function terms(value = []) {
  if (!Array.isArray(value) || value.length > 30 || value.some(term => typeof term !== 'string' || !term.trim() || term.length > 100)) fail('Protected terms must be an array of up to 30 short names/places.');
  return [...new Set(value.map(term => term.trim()))];
}
function stable(value) {
  if (Array.isArray(value)) return value.map(stable);
  if (value && typeof value === 'object') return Object.fromEntries(Object.keys(value).sort().map(key => [key, stable(value[key])]));
  return value;
}
export function contentHash(artifact) {
  const body = { ...artifact }; delete body.review;
  return createHash('sha256').update(JSON.stringify(stable(body))).digest('hex');
}
function audioHash(audio) { return createHash('sha256').update(audio).digest('hex'); }
function normalized(value) { return value.normalize('NFKC').toLowerCase().replace(/[^\p{L}\p{N}]+/gu, ' ').trim().replace(/\s+/g, ' '); }

export function translationProblems(source, target, translated) {
  const problems = [];
  if (translated.uncertain !== false) problems.push('MODEL_UNCERTAIN');
  if (translated.sourceLanguage !== source.source) problems.push('SOURCE_LANGUAGE_MISMATCH');
  if (translated.targetLanguage !== target) problems.push('TARGET_LANGUAGE_MISMATCH');
  if (!Array.isArray(translated.notes) || translated.notes.length > 20 || translated.notes.some(note => typeof note !== 'string' || note.length > 1000)) problems.push('INVALID_NOTES');
  if (Array.isArray(translated.notes) && translated.notes.some(note => /unclear|uncertain|unintelligible|cannot reliably|not confident|mixed.language|untranslated|copied.*source/i.test(note))) problems.push('UNCERTAINTY_NOTES');
  const value = text(translated.translation);
  for (const term of source.protectedTerms) if (!value.includes(term)) problems.push('PROTECTED_TERM_MISSING');
  let original = normalized(source.text), result = normalized(value);
  if (original === result) problems.push('UNCHANGED_SOURCE');
  for (const term of source.protectedTerms) { original = original.replaceAll(normalized(term), ' '); result = result.replaceAll(normalized(term), ' '); }
  const originalWords = original.split(/\s+/).filter(Boolean);
  const resultWords = new Set(result.split(/\s+/).filter(Boolean));
  if (originalWords.length >= 5 && originalWords.filter(word => resultWords.has(word)).length / originalWords.length >= 0.7) problems.push('HIGH_SOURCE_OVERLAP');
  return [...new Set(problems)];
}

export function validateArtifact(artifact) {
  if (!artifact || artifact.schemaVersion !== 2 || !['transcript', 'translation', 'voice'].includes(artifact.kind)) fail('Use a current staged artifact; legacy automatic reports cannot grant approval.');
  language(artifact.source);
  if (artifact.kind === 'transcript') {
    text(artifact.text);
    if (!Array.isArray(artifact.protectedTerms)) fail('Protected-term metadata is missing.');
    terms(artifact.protectedTerms);
    if (artifact.referenceEnglish !== null) text(artifact.referenceEnglish);
  } else {
    validateArtifact(artifact.sourceArtifact);
    if (artifact.sourceArtifact.kind !== 'transcript' || artifact.sourceArtifact.source !== artifact.source) fail('Translation source artifact is inconsistent.');
    language(artifact.target);
    if (artifact.source === artifact.target) fail('A translation target must differ from its source.');
    text(artifact.text);
    if (!Array.isArray(artifact.blockedReasons)) fail('Translation assessment is missing.');
    if (artifact.kind === 'voice') {
      validateArtifact(artifact.translationArtifact);
      if (artifact.translationArtifact.kind !== 'translation' || artifact.translationArtifact.text !== artifact.text || artifact.translationArtifact.target !== artifact.target) fail('Voice text must match its reviewed translation.');
      if (artifact.audioFile !== 'speech.wav' || !/^[a-f0-9]{64}$/.test(artifact.audioSha256)) fail('Voice audio metadata is invalid.');
      text(artifact.spokenTranscript);
    }
  }
  return artifact;
}

export function requireApproved(artifact) {
  validateArtifact(artifact);
  if (!artifact.review || artifact.review.decision !== 'approve' || typeof artifact.review.reviewer !== 'string' || !artifact.review.reviewer.trim()
    || artifact.review.language !== (artifact.kind === 'transcript' ? artifact.source : artifact.target)
    || artifact.review.contentHash !== contentHash(artifact)) fail('Human approval is missing, rejected, or stale. Review the exact saved content first.');
  if (artifact.kind !== 'transcript') {
    requireApproved(artifact.sourceArtifact);
    if (artifact.blockedReasons.length) fail('Uncertainty, language or content flags must be corrected before proceeding.');
  }
  if (artifact.kind === 'voice') {
    requireApproved(artifact.translationArtifact);
    if (!['good', 'ok'].includes(artifact.review.pronunciation) || !['good', 'ok'].includes(artifact.review.naturalness)) fail('Native voice review has not passed.');
  }
  return artifact;
}

export function assertRunnable(options, inputs) {
  const stage = options.stage;
  if (!['transcribe', 'review-transcript', 'translate', 'review-translation', 'voice', 'review-voice'].includes(stage)) fail('Unknown evaluation stage.');
  if (stage === 'transcribe') return;
  let artifact = inputs.artifact;
  if (stage === 'review-transcript' && !artifact) {
    language(options.source); text(inputs.text);
    artifact = { kind: 'transcript', source: options.source };
  }
  if (!artifact) fail('This stage requires an input artifact.');
  if (stage === 'translate') {
    requireApproved(artifact);
    if (artifact.kind !== 'transcript') fail('Translation requires a reviewed transcript.');
    language(options.target);
    if (artifact.source === options.target) fail('Choose a different target language.');
  } else if (stage === 'voice') {
    requireApproved(artifact);
    if (artifact.kind !== 'translation') fail('Speech requires a reviewed translation.');
  } else {
    const kind = stage.slice('review-'.length);
    if (artifact.kind !== kind) fail('The artifact does not match this review stage.');
    if (!['approve', 'reject'].includes(options.decision) || typeof options.reviewer !== 'string' || !options.reviewer.trim() || options.reviewer.length > 100) fail('A human reviewer and explicit decision are required.');
    if (options.language !== (kind === 'transcript' ? artifact.source : artifact.target)) fail('The reviewer must confirm the expected language.');
    if (inputs.text !== undefined) text(inputs.text);
    if (kind === 'transcript') {
      if (inputs.meaning !== undefined) text(inputs.meaning);
      terms(inputs.terms ?? artifact.protectedTerms);
    }
    if (kind === 'translation') {
      requireApproved(artifact.sourceArtifact);
      if (options.decision === 'approve' && artifact.blockedReasons.length && inputs.text === undefined) fail('Correct flagged translation text in a file before approving it.');
    }
    if (kind === 'voice') {
      requireApproved(artifact.translationArtifact);
      if (!Buffer.isBuffer(inputs.audio) || audioHash(inputs.audio) !== artifact.audioSha256) fail('Voice audio is missing or changed since generation.');
      if (options.decision === 'approve' && (artifact.blockedReasons.length || !['good', 'ok'].includes(options.pronunciation) || !['good', 'ok'].includes(options.naturalness))) fail('Voice approval requires matching spoken text and human pronunciation/naturalness scores of good or ok.');
      if (inputs.text !== undefined) fail('Voice audio cannot be corrected by editing text. Regenerate from reviewed text.');
    }
  }
}

function reviewed(artifact, options) {
  const result = structuredClone(artifact);
  result.review = { decision: options.decision, reviewer: options.reviewer.trim(), language: options.language,
    reviewedAt: new Date().toISOString(), contentHash: contentHash(result) };
  if (result.kind === 'voice') { result.review.pronunciation = options.pronunciation ?? null; result.review.naturalness = options.naturalness ?? null; }
  return result;
}
function base(kind, source) { return { schemaVersion: 2, kind, source, createdAt: new Date().toISOString(), review: null }; }
export function summarizeArtifact(artifact) {
  const rejected = artifact.review?.decision === 'reject';
  let approved = false;
  try { requireApproved(artifact); approved = true; } catch { /* Pending/stale/flagged content is not approved. */ }
  return { kind: artifact.kind, source: artifact.source, target: artifact.target ?? null,
    reviewStatus: rejected ? 'rejected' : approved ? 'approved' : 'pending',
    qualityVerdict: rejected ? 'FAIL' : artifact.kind === 'voice' && approved ? 'PASS' : 'NOT_REVIEWED',
    demoReady: artifact.kind === 'voice' && approved,
    blockedReasons: artifact.blockedReasons ?? [] };
}
export function failureCode(error) {
  if (error?.status === 401 || error?.status === 403) return 'ACCESS_DENIED';
  if (error?.status === 429) return 'RATE_LIMIT_OR_QUOTA';
  if (error?.status === 400 || error?.status === 404) return 'MODEL_OR_INPUT_REJECTED';
  if (error?.name === 'AbortError' || error?.name === 'TimeoutError') return 'TIMEOUT';
  return 'REQUEST_OR_OUTPUT_FAILED';
}

export async function evaluate(options, services, output, inputs = {}) {
  assertRunnable(options, inputs);
  const artifacts = [];
  const failures = [];
  const save = async (name, artifact) => { validateArtifact(artifact); await output(name, JSON.stringify(artifact, null, 2) + '\n'); artifacts.push({ file: name, ...summarizeArtifact(artifact) }); };
  const stage = options.stage;
  if (stage === 'transcribe') {
    for (const [source, path] of Object.entries(options.clips)) {
      try {
        const response = await services.transcribe(path, source);
        const artifact = { ...base('transcript', source), text: text(response.text), referenceEnglish: null, protectedTerms: [], model: options.models.transcription,
          detectedLanguages: Array.isArray(response.detectedLanguages) ? response.detectedLanguages : [] };
        await save(`transcript-${source}.json`, artifact);
      } catch (error) { failures.push({ source, code: failureCode(error) }); }
    }
  } else if (stage === 'review-transcript') {
    const artifact = inputs.artifact ? structuredClone(inputs.artifact) : { ...base('transcript', options.source), text: text(inputs.text), referenceEnglish: null, protectedTerms: [], model: 'human-reference', detectedLanguages: [] };
    if (inputs.text !== undefined) artifact.text = text(inputs.text);
    if (inputs.meaning !== undefined) artifact.referenceEnglish = text(inputs.meaning);
    artifact.protectedTerms = terms(inputs.terms ?? artifact.protectedTerms);
    if (options.decision === 'approve' && artifact.protectedTerms.some(term => !artifact.text.toLowerCase().includes(term.toLowerCase()))) fail('A protected name/place is absent from the reviewed source.');
    await save('transcript.json', reviewed(artifact, options));
  } else if (stage === 'translate') {
    try {
      const source = inputs.artifact;
      const response = await services.translate(source.text, source.source, options.target, source);
      const artifact = { ...base('translation', source.source), target: options.target, text: text(response.translation), sourceArtifact: source,
        notes: response.notes, model: options.models.translation, blockedReasons: translationProblems(source, options.target, response) };
      await save('translation.json', artifact);
    } catch (error) { failures.push({ code: failureCode(error) }); }
  } else if (stage === 'review-translation') {
    const artifact = structuredClone(inputs.artifact);
    if (inputs.text !== undefined) {
      artifact.text = text(inputs.text);
      artifact.humanCorrected = true;
      artifact.blockedReasons = translationProblems(artifact.sourceArtifact, artifact.target, { translation: artifact.text, notes: [], uncertain: false, sourceLanguage: artifact.source, targetLanguage: artifact.target });
    }
    if (options.decision === 'approve' && artifact.blockedReasons.length) fail('Translation content is still flagged. Correct it before approving.');
    await save('translation.json', reviewed(artifact, options));
  } else if (stage === 'voice') {
    try {
      const translation = inputs.artifact;
      const speech = await services.speak(translation.text, translation.target);
      if (!Buffer.isBuffer(speech.wav) || speech.wav.length <= 44 || speech.wav.length > 48_000 * 120 + 44) fail('Invalid generated voice audio.');
      const artifact = { ...base('voice', translation.source), target: translation.target, text: translation.text, sourceArtifact: translation.sourceArtifact,
        translationArtifact: translation, spokenTranscript: text(speech.transcript), model: options.models.speech,
        audioFile: 'speech.wav', audioSha256: audioHash(speech.wav), blockedReasons: normalized(speech.transcript) === normalized(translation.text) ? [] : ['SPOKEN_TEXT_MISMATCH'] };
      await output('speech.wav', speech.wav);
      await save('voice.json', artifact);
    } catch (error) { failures.push({ code: failureCode(error) }); }
  } else if (stage === 'review-voice') {
    await output('speech.wav', inputs.audio);
    await save('voice.json', reviewed(inputs.artifact, options));
  }
  return { stage, processingStatus: failures.length ? 'FAIL' : 'PASS', artifacts, failures,
    qualityVerdict: artifacts.some(a => a.qualityVerdict === 'FAIL') ? 'FAIL' : artifacts.length > 0 && artifacts.every(a => a.demoReady) ? 'PASS' : 'NOT_REVIEWED',
    demoReady: artifacts.length > 0 && artifacts.every(a => a.demoReady) };
}
