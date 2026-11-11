// Pure scoring for the Voice Stamp feasibility check. No model or file access here, so it is unit tested.

export function normalise(vector) {
  const norm = Math.sqrt(vector.reduce((sum, value) => sum + value * value, 0));
  if (!Number.isFinite(norm) || norm === 0) throw new Error("A voice embedding cannot be empty or all zeros.");
  return Float32Array.from(vector, value => value / norm);
}

export function cosine(a, b) {
  if (a.length !== b.length) throw new Error("Embeddings must have the same size.");
  let dot = 0, na = 0, nb = 0;
  for (let i = 0; i < a.length; i++) { dot += a[i] * b[i]; na += a[i] * a[i]; nb += b[i] * b[i]; }
  return dot / Math.sqrt(na * nb);
}

/** A voiceprint ("stamp"): the normalised mean of a speaker's enrolment embeddings. */
export function voiceprint(embeddings) {
  if (!embeddings.length) throw new Error("Enrol at least one recording.");
  const sum = new Float32Array(embeddings[0].length);
  for (const embedding of embeddings) for (let i = 0; i < sum.length; i++) sum[i] += embedding[i] / Math.sqrt(embedding.reduce((s, v) => s + v * v, 0));
  return normalise(sum);
}

/** Deterministic shuffle so runs are repeatable. */
export function shuffled(items, seed = 1) {
  const result = [...items];
  let state = seed >>> 0 || 1;
  const next = () => { state ^= state << 13; state >>>= 0; state ^= state >>> 17; state ^= state << 5; state >>>= 0; return state / 2 ** 32; };
  for (let i = result.length - 1; i > 0; i--) { const j = Math.floor(next() * (i + 1)); [result[i], result[j]] = [result[j], result[i]]; }
  return result;
}

/**
 * Splits speakers into stamped artists (enrolled with `enrol` clips, the rest used as their own uploads) and
 * unstamped uploaders (never enrolled), as Mazwi would see them.
 */
export function splitSpeakers(clips, { enrol = 3, minClips = enrol + 2, unstampedShare = 0.3, seed = 1 } = {}) {
  const bySpeaker = new Map();
  for (const clip of clips) bySpeaker.set(clip.speaker, [...(bySpeaker.get(clip.speaker) ?? []), clip]);
  const speakers = shuffled([...bySpeaker.keys()].filter(speaker => bySpeaker.get(speaker).length >= minClips).sort(), seed);
  const unstampedCount = Math.round(speakers.length * unstampedShare);
  const stamped = speakers.slice(unstampedCount).map(speaker => {
    const own = shuffled(bySpeaker.get(speaker), seed);
    return { speaker, enrolment: own.slice(0, enrol), uploads: own.slice(enrol) };
  });
  const unstamped = speakers.slice(0, unstampedCount).map(speaker => ({ speaker, uploads: bySpeaker.get(speaker) }));
  return { stamped, unstamped };
}

/**
 * Scores every upload against every stamp.
 * - target: an artist's own upload against their own stamp (should match);
 * - impostor: an upload against another artist's stamp (should not match);
 * - bestForeign: per upload, the highest score against any stamp that is not the uploader's. This is what decides
 *   whether Mazwi would wrongly hold an upload for moderation.
 */
export function scoreTrials(split, rawEmbed, { center = false } = {}) {
  // Centring subtracts the mean of all enrolment embeddings (the "average stamp"). It removes what every recording
  // shares (microphone, room, language), a common fix for domain mismatch. Only enrolment clips are used for it.
  const mean = center ? meanOf(split.stamped.flatMap(artist => artist.enrolment.map(clip => normalise(rawEmbed(clip))))) : null;
  const embed = clip => mean ? subtract(normalise(rawEmbed(clip)), mean) : rawEmbed(clip);
  const stamps = split.stamped.map(artist => ({ speaker: artist.speaker, print: voiceprint(artist.enrolment.map(embed)) }));
  const target = [], impostor = [], bestForeign = [];
  const uploads = [...split.stamped.flatMap(artist => artist.uploads), ...split.unstamped.flatMap(person => person.uploads)];
  for (const upload of uploads) {
    const embedding = embed(upload);
    let best = -1;
    for (const stamp of stamps) {
      const score = cosine(embedding, stamp.print);
      if (stamp.speaker === upload.speaker) target.push(score);
      else { impostor.push(score); best = Math.max(best, score); }
    }
    bestForeign.push(best);
  }
  return { stamps: stamps.length, uploads: uploads.length, target, impostor, bestForeign };
}

export function meanOf(vectors) {
  const sum = new Float32Array(vectors[0].length);
  for (const vector of vectors) for (let i = 0; i < sum.length; i++) sum[i] += vector[i] / vectors.length;
  return sum;
}

export function subtract(vector, mean) {
  return Float32Array.from(vector, (value, i) => value - mean[i]);
}

/** Cuts leading and trailing quiet audio (20 ms frames more than `belowPeakDb` under the loudest frame). */
export function trimSilence(samples, sampleRate, belowPeakDb = 40) {
  const frame = Math.max(1, Math.round(sampleRate * 0.02));
  const energies = [];
  for (let start = 0; start < samples.length; start += frame) {
    let sum = 0;
    const end = Math.min(samples.length, start + frame);
    for (let i = start; i < end; i++) sum += samples[i] * samples[i];
    energies.push(sum / (end - start));
  }
  const peak = Math.max(...energies);
  if (!(peak > 0)) return samples;
  const floor = peak / 10 ** (belowPeakDb / 10);
  const first = energies.findIndex(energy => energy >= floor);
  const last = energies.length - 1 - [...energies].reverse().findIndex(energy => energy >= floor);
  return samples.subarray(first * frame, Math.min(samples.length, (last + 1) * frame));
}

export function ratesAt(threshold, trials) {
  const share = (values, test) => values.length ? values.filter(test).length / values.length : 0;
  return {
    threshold,
    missedMatch: share(trials.target, score => score < threshold),
    falseMatch: share(trials.impostor, score => score >= threshold),
    wronglyHeld: share(trials.bestForeign, score => score >= threshold),
  };
}

/** Equal error rate between missed matches and false matches (pairwise). */
export function equalErrorRate(trials) {
  const candidates = [...new Set([...trials.target, ...trials.impostor])].sort((a, b) => a - b);
  let best = { threshold: 0, rate: 1, gap: Infinity };
  for (const threshold of candidates) {
    const { missedMatch, falseMatch } = ratesAt(threshold, trials);
    const gap = Math.abs(missedMatch - falseMatch);
    if (gap < best.gap) best = { threshold, rate: (missedMatch + falseMatch) / 2, gap };
  }
  return { threshold: best.threshold, rate: best.rate };
}

/** The lowest threshold that keeps uploads wrongly held for moderation at or below `rate`. */
export function thresholdForWronglyHeld(trials, rate) {
  const candidates = [...new Set(trials.bestForeign)].sort((a, b) => a - b);
  for (const threshold of candidates) if (ratesAt(threshold, trials).wronglyHeld <= rate) return threshold;
  return candidates.length ? candidates.at(-1) + 1e-6 : 1;
}

/** Splits clips into two groups of different speakers: one to choose the threshold, one to report on. */
export function speakerHalves(clips, seed = 5) {
  const speakers = shuffled([...new Set(clips.map(clip => clip.speaker))].sort(), seed);
  const first = new Set(speakers.slice(0, Math.ceil(speakers.length / 2)));
  return [clips.filter(clip => first.has(clip.speaker)), clips.filter(clip => !first.has(clip.speaker))];
}

/** The lowest threshold whose per-pair false-match rate is at most `rate`. */
export function thresholdForFalseMatch(trials, rate) {
  const candidates = [...new Set(trials.impostor)].sort((a, b) => a - b);
  for (const threshold of candidates) if (ratesAt(threshold, trials).falseMatch <= rate) return threshold;
  return candidates.length ? candidates.at(-1) + 1e-6 : 1;
}

/** Per-pair false-match rate for which an upload compared with `stamps` stamps is wrongly held at `heldRate`. */
export function perPairRateFor(heldRate, stamps) {
  return 1 - (1 - heldRate) ** (1 / stamps);
}

/** Share of uploads by unstamped people wrongly held when compared with `stamps` stamps (independent pairs). */
export function projectWronglyHeld(perPairRate, stamps) {
  return 1 - (1 - perPairRate) ** stamps;
}

/** A small stable number per string, for per-clip noise seeds. */
export function hashSeed(text) {
  let hash = 2166136261;
  for (const char of text) hash = Math.imul(hash ^ char.codePointAt(0), 16777619) >>> 0;
  return hash || 1;
}

export function summarise(values) {
  const sorted = [...values].sort((a, b) => a - b);
  const at = share => sorted.length ? sorted[Math.min(sorted.length - 1, Math.floor(share * sorted.length))] : NaN;
  const mean = sorted.reduce((sum, value) => sum + value, 0) / (sorted.length || 1);
  return { count: sorted.length, mean, p05: at(0.05), median: at(0.5), p95: at(0.95) };
}

/** Adds white noise at a signal-to-noise ratio, deterministic per seed. */
export function addNoise(samples, snrDb, seed = 7) {
  const power = samples.reduce((sum, value) => sum + value * value, 0) / (samples.length || 1);
  const noisePower = power / 10 ** (snrDb / 10);
  let state = seed >>> 0 || 1;
  const uniform = () => { state = (Math.imul(state, 1664525) + 1013904223) >>> 0; return (state + 1) / 2 ** 32; };
  return Float32Array.from(samples, value => {
    const gaussian = Math.sqrt(-2 * Math.log(uniform())) * Math.cos(2 * Math.PI * uniform());
    return value + gaussian * Math.sqrt(noisePower);
  });
}

/** Keeps at most `seconds` of audio from the start. */
export function truncate(samples, sampleRate, seconds) {
  return samples.subarray(0, Math.min(samples.length, Math.round(sampleRate * seconds)));
}

/** "xho_0050_9982464550.wav" -> "xho_0050" (the OpenSLR speaker id). */
export function speakerFromFile(name, pattern = /^([a-z]+_\d+)_/) {
  const match = name.match(pattern);
  return match ? match[1] : null;
}
