// Voice Stamp feasibility check: runs an open speaker-embedding model on ONNX Runtime (through sherpa-onnx)
// over a folder of WAV recordings from many speakers, and reports how well "stamped" artists can be told apart.
//
// node cli.mjs --model <speaker-model.onnx> --wavs <folder> [--enrol 3] [--max-per-speaker 12] [--max-speakers 200]
//              [--unstamped 0.3] [--threads 2] [--out report.json]
//
// Method: speakers are split into a calibration half and an evaluation half. The threshold is chosen on the
// calibration half and every reported rate comes from the evaluation half (different people).
// Nothing is uploaded anywhere: models and recordings stay on this machine.
import { createRequire } from "node:module";
import { readdirSync, statSync, writeFileSync } from "node:fs";
import { basename, join, resolve } from "node:path";
import {
  addNoise, equalErrorRate, hashSeed, perPairRateFor, projectWronglyHeld, ratesAt, scoreTrials, speakerFromFile, speakerHalves,
  splitSpeakers, summarise, thresholdForFalseMatch, trimSilence, truncate,
} from "./core.mjs";

const require = createRequire(import.meta.url);
const sherpa = require("sherpa-onnx-node");

function option(name, fallback) {
  const index = process.argv.indexOf(`--${name}`);
  return index >= 0 && process.argv[index + 1] ? process.argv[index + 1] : fallback;
}

if (!option("model", "") || !option("wavs", "")) {
  console.error("Usage: node cli.mjs --model <speaker-model.onnx> --wavs <folder of WAV files> [--enrol 3] [--out report.json]");
  process.exit(2);
}
const model = resolve(option("model", ""));
const wavs = resolve(option("wavs", ""));
const enrol = Number(option("enrol", 3));
const maxPerSpeaker = Number(option("max-per-speaker", 12));
const maxSpeakers = Number(option("max-speakers", 200));
const unstampedShare = Number(option("unstamped", 0.3));
const threads = Number(option("threads", 2));
const out = option("out", null);

// The decision rule's operating point: at most 5% of uploads by people without a stamp wrongly held, with 500 stamps.
const HELD_TARGET = 0.05, STAMPS_TARGET = 500;
const perPairTarget = perPairRateFor(HELD_TARGET, STAMPS_TARGET);

// Gather clips per speaker (OpenSLR names: <lang>_<speaker>_<utterance>.wav).
const bySpeaker = new Map();
for (const name of readdirSync(wavs).filter(file => file.toLowerCase().endsWith(".wav")).sort()) {
  const speaker = speakerFromFile(name);
  if (!speaker) continue;
  const list = bySpeaker.get(speaker) ?? [];
  if (list.length < maxPerSpeaker) list.push({ speaker, file: join(wavs, name) });
  bySpeaker.set(speaker, list);
}

// Read every clip once at 16 kHz with quiet edges trimmed. Unreadable, cut-off or very short files are skipped.
const waves = new Map();
for (const clip of [...bySpeaker.values()].flat()) {
  try {
    const { samples, sampleRate } = sherpa.readWave(clip.file);
    const resampled = trimSilence(sampleRate === 16000 ? samples : new sherpa.LinearResampler(sampleRate, 16000).flush(samples), 16000);
    if (resampled.length >= 16000) waves.set(clip.file, { samples: resampled, sampleRate: 16000 });
  } catch { /* skipped */ }
}
for (const [speaker, list] of bySpeaker) bySpeaker.set(speaker, list.filter(clip => waves.has(clip.file)));
const speakers = [...bySpeaker.keys()].filter(speaker => bySpeaker.get(speaker).length >= enrol + 2).slice(0, maxSpeakers);
const clips = speakers.flatMap(speaker => bySpeaker.get(speaker));

const loadStart = performance.now();
const extractor = new sherpa.SpeakerEmbeddingExtractor({ model, numThreads: threads, debug: false, provider: "cpu" });
const loadMs = performance.now() - loadStart;

const timing = { clips: 0, audioSeconds: 0, computeMs: 0 };
function embedSamples(samples, sampleRate) {
  const start = performance.now();
  const stream = extractor.createStream();
  stream.acceptWaveform({ sampleRate, samples });
  stream.inputFinished();
  const embedding = Float32Array.from(extractor.compute(stream));
  timing.clips++; timing.audioSeconds += samples.length / sampleRate; timing.computeMs += performance.now() - start;
  return embedding;
}

// Upload conditions. Enrolment always uses the clean recording; uploads may be short or noisy (noise differs per clip).
const conditions = {
  clean: samples => samples,
  "short 3 s": (samples, rate) => truncate(samples, rate, 3),
  "noisy 10 dB": (samples, _rate, clip) => addNoise(samples, 10, hashSeed(clip.file)),
  "noisy 5 dB": (samples, _rate, clip) => addNoise(samples, 5, hashSeed(clip.file)),
};

const [calibrationClips, evaluationClips] = speakerHalves(clips, 5);
const calibration = splitSpeakers(calibrationClips, { enrol, unstampedShare, seed: 11 });
const evaluation = splitSpeakers(evaluationClips, { enrol, unstampedShare, seed: 13 });
const enrolled = new Set([...calibration.stamped, ...evaluation.stamped].flatMap(artist => artist.enrolment.map(clip => clip.file)));
const cache = new Map();
const results = {};
const counted = (rate, total) => ({ rate, count: Math.round(rate * total), total });
for (const [name, transform] of Object.entries(conditions)) {
  const embed = clip => {
    const clean = enrolled.has(clip.file);
    const key = `${clean ? "clean" : name}|${clip.file}`;
    if (!cache.has(key)) {
      const { samples, sampleRate } = waves.get(clip.file);
      cache.set(key, embedSamples(clean ? samples : transform(samples, sampleRate, clip), sampleRate));
    }
    return cache.get(key);
  };
  results[name] = {};
  for (const scoring of ["raw", "centred"]) {
    const center = scoring === "centred";
    const calibrationTrials = scoreTrials(calibration, embed, { center });
    const trials = scoreTrials(evaluation, embed, { center });
    const threshold = thresholdForFalseMatch(calibrationTrials, perPairTarget);
    const rates = ratesAt(threshold, trials);
    results[name][scoring] = {
      threshold,
      evaluation: { stamps: trials.stamps, uploads: trials.uploads },
      missedOwnVoice: counted(rates.missedMatch, trials.target.length),
      perPairFalseMatch: counted(rates.falseMatch, trials.impostor.length),
      wronglyHeldObserved: counted(rates.wronglyHeld, trials.bestForeign.length),
      projectedWronglyHeld: Object.fromEntries([50, 500, 5000].map(stamps => [stamps, projectWronglyHeld(rates.falseMatch, stamps)])),
      equalErrorRate: equalErrorRate(trials),
      target: summarise(trials.target), impostor: summarise(trials.impostor),
    };
  }
}

const report = {
  model: basename(model), modelBytes: statSync(model).size, dimensions: extractor.dim, threads,
  operatingPoint: { wronglyHeld: HELD_TARGET, stamps: STAMPS_TARGET, perPairFalseMatch: perPairTarget },
  data: {
    folder: wavs, speakers: speakers.length, clips: clips.length, enrolClips: enrol,
    calibration: { stamped: calibration.stamped.length, unstamped: calibration.unstamped.length },
    evaluation: { stamped: evaluation.stamped.length, unstamped: evaluation.unstamped.length },
  },
  speed: { loadMs: Math.round(loadMs), clips: timing.clips, audioSeconds: Math.round(timing.audioSeconds), msPerClip: +(timing.computeMs / timing.clips).toFixed(1), realTimeFactor: +(timing.computeMs / 1000 / timing.audioSeconds).toFixed(4) },
  results,
};
if (out) writeFileSync(resolve(out), JSON.stringify(report, null, 2));

const pct = value => `${(value * 100).toFixed(1)}%`;
const withCount = value => `${pct(value.rate)} (${value.count}/${value.total})`;
console.log(`\n${report.model}: ${report.dimensions} dims, ${(report.modelBytes / 1e6).toFixed(1)} MB, load ${report.speed.loadMs} ms, ${report.speed.msPerClip} ms per clip (real-time factor ${report.speed.realTimeFactor})`);
console.log(`${report.data.speakers} speakers, ${report.data.clips} clips. Calibration: ${report.data.calibration.stamped} stamped + ${report.data.calibration.unstamped} unstamped; evaluation: ${report.data.evaluation.stamped} stamped + ${report.data.evaluation.unstamped} unstamped (${enrol} enrolment clips each).`);
console.log(`Threshold chosen on calibration speakers for a per-pair false match of ${perPairTarget.toExponential(2)} (5% wrongly held at 500 stamps).\n`);
console.log("| Upload | Scoring | EER | Threshold | Missed own voice | Per-pair false match | Wrongly held: 50 / 500 / 5000 stamps |");
console.log("| --- | --- | --- | --- | --- | --- | --- |");
for (const [name, byScoring] of Object.entries(results)) for (const [scoring, result] of Object.entries(byScoring)) {
  const projected = Object.values(result.projectedWronglyHeld).map(pct).join(" / ");
  console.log(`| ${name} | ${scoring} | ${pct(result.equalErrorRate.rate)} | ${result.threshold.toFixed(3)} | ${withCount(result.missedOwnVoice)} | ${withCount(result.perPairFalseMatch)} | ${projected} |`);
}
