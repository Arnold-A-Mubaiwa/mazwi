import test from "node:test";
import assert from "node:assert/strict";
import { addNoise, cosine, equalErrorRate, hashSeed, meanOf, perPairRateFor, projectWronglyHeld, speakerHalves, thresholdForFalseMatch, normalise, ratesAt, scoreTrials, shuffled, speakerFromFile, splitSpeakers, subtract, summarise, thresholdForWronglyHeld, trimSilence, truncate, voiceprint } from "./core.mjs";

test("cosine_and_normalise_basic_geometry", () => {
  assert.ok(Math.abs(cosine([1, 0], [2, 0]) - 1) < 1e-9);
  assert.ok(Math.abs(cosine([1, 0], [0, 3])) < 1e-9);
  assert.deepEqual([...normalise([3, 4])].map(v => +v.toFixed(3)), [0.6, 0.8]);
  assert.throws(() => normalise([0, 0]), /all zeros/);
  assert.throws(() => cosine([1], [1, 2]), /same size/);
});

test("voiceprint_is_normalised_mean_of_unit_embeddings", () => {
  const print = voiceprint([[1, 0], [0, 10]]);
  assert.ok(Math.abs(print[0] - print[1]) < 1e-6);
  assert.ok(Math.abs(Math.hypot(...print) - 1) < 1e-6);
  assert.throws(() => voiceprint([]), /at least one/);
});

test("shuffled_is_deterministic_and_complete", () => {
  const items = Array.from({ length: 20 }, (_, i) => i);
  assert.deepEqual(shuffled(items, 3), shuffled(items, 3));
  assert.notDeepEqual(shuffled(items, 3), items);
  assert.deepEqual([...shuffled(items, 3)].sort((a, b) => a - b), items);
});

test("splitSpeakers_keeps_unstamped_speakers_apart_and_skips_small_ones", () => {
  const clips = ["a", "b", "c", "d", "e", "f", "g", "h", "i", "j"].flatMap(speaker => Array.from({ length: 6 }, (_, i) => ({ speaker, id: `${speaker}${i}` })));
  clips.push({ speaker: "tiny", id: "t0" });
  const split = splitSpeakers(clips, { enrol: 3, unstampedShare: 0.3 });
  assert.equal(split.unstamped.length, 3);
  assert.equal(split.stamped.length, 7);
  assert.ok(split.stamped.every(artist => artist.enrolment.length === 3 && artist.uploads.length === 3));
  const stamped = new Set(split.stamped.map(artist => artist.speaker));
  assert.ok(split.unstamped.every(person => !stamped.has(person.speaker)));
  assert.ok(![...stamped].includes("tiny"));
});

test("scoreTrials_and_rates_separate_perfect_speakers", () => {
  const direction = { a: [1, 0, 0], b: [0, 1, 0], c: [0, 0, 1] };
  const clips = Object.keys(direction).flatMap(speaker => Array.from({ length: 5 }, (_, i) => ({ speaker, jitter: i / 100 })));
  const split = splitSpeakers(clips, { enrol: 2, unstampedShare: 0.34 });
  const trials = scoreTrials(split, clip => direction[clip.speaker].map(v => v + clip.jitter));
  assert.equal(trials.stamps, 2);
  assert.ok(trials.target.every(score => score > 0.99));
  assert.ok(trials.impostor.every(score => score < 0.1));
  const rates = ratesAt(0.5, trials);
  assert.deepEqual([rates.missedMatch, rates.falseMatch, rates.wronglyHeld], [0, 0, 0]);
  assert.equal(equalErrorRate(trials).rate, 0);
  assert.ok(thresholdForWronglyHeld(trials, 0) <= 0.5);
});

test("ratesAt_counts_errors_on_each_side", () => {
  const trials = { target: [0.9, 0.4], impostor: [0.2, 0.6, 0.7, 0.1], bestForeign: [0.7, 0.2] };
  assert.deepEqual(ratesAt(0.5, trials), { threshold: 0.5, missedMatch: 0.5, falseMatch: 0.5, wronglyHeld: 0.5 });
  assert.ok(thresholdForWronglyHeld(trials, 0) > 0.7);
});

test("summarise_reports_percentiles", () => {
  const summary = summarise(Array.from({ length: 100 }, (_, i) => i));
  assert.equal(summary.count, 100);
  assert.equal(summary.median, 50);
  assert.equal(summary.p05, 5);
  assert.equal(summary.p95, 95);
});

test("addNoise_hits_the_requested_snr_and_truncate_limits_length", () => {
  const clean = Float32Array.from({ length: 16000 }, (_, i) => Math.sin(i / 10));
  const noisy = addNoise(clean, 10);
  const signal = clean.reduce((s, v) => s + v * v, 0);
  const noise = noisy.reduce((s, v, i) => s + (v - clean[i]) ** 2, 0);
  assert.ok(Math.abs(10 * Math.log10(signal / noise) - 10) < 0.5);
  assert.equal(truncate(clean, 16000, 0.5).length, 8000);
  assert.equal(truncate(clean, 16000, 5).length, 16000);
});

test("speakerFromFile_reads_openslr_ids", () => {
  assert.equal(speakerFromFile("xho_0050_9982464550.wav"), "xho_0050");
  assert.equal(speakerFromFile("readme.txt"), null);
});

test("centring_removes_what_every_recording_shares", () => {
  // Every speaker shares a large common component (the "room"), plus a small personal direction.
  const personal = { a: [0, 1, 0, 0], b: [0, 0, 1, 0], c: [0, 0, 0, 1], d: [0, -1, 0, 0] };
  const clips = Object.keys(personal).flatMap(speaker => Array.from({ length: 5 }, (_, i) => ({ speaker, i })));
  const embed = clip => personal[clip.speaker].map((v, k) => (k === 0 ? 5 : 0) + v + (clip.i % 2 ? 0.05 : -0.05) * (k === 1 ? 1 : 0));
  const split = splitSpeakers(clips, { enrol: 2, unstampedShare: 0 });
  const raw = scoreTrials(split, embed);
  const centred = scoreTrials(split, embed, { center: true });
  assert.ok(Math.min(...raw.impostor) > 0.9);
  assert.ok(Math.max(...centred.impostor) < Math.min(...centred.target));
  assert.deepEqual([...meanOf([[1, 3], [3, 5]])], [2, 4]);
  assert.deepEqual([...subtract([3, 3], [1, 2])], [2, 1]);
});

test("trimSilence_cuts_quiet_edges_only", () => {
  const rate = 1000;
  const samples = Float32Array.from({ length: 3000 }, (_, i) => (i >= 1000 && i < 2000 ? Math.sin(i) : 0.00001));
  const trimmed = trimSilence(samples, rate);
  assert.ok(trimmed.length >= 1000 && trimmed.length <= 1040);
  assert.equal(trimSilence(new Float32Array(100), rate).length, 100);
});

test("speakerHalves_never_share_a_speaker", () => {
  const clips = Array.from({ length: 40 }, (_, i) => ({ speaker: "s" + (i % 10), i }));
  const [a, b] = speakerHalves(clips);
  const left = new Set(a.map(clip => clip.speaker));
  assert.equal(left.size, 5);
  assert.ok(b.every(clip => !left.has(clip.speaker)));
  assert.equal(a.length + b.length, 40);
});

test("projection_and_per_pair_rate_are_inverse", () => {
  const p = perPairRateFor(0.05, 500);
  assert.ok(Math.abs(projectWronglyHeld(p, 500) - 0.05) < 1e-9);
  assert.ok(projectWronglyHeld(0.01, 500) > 0.99);
  assert.equal(projectWronglyHeld(0, 5000), 0);
});

test("thresholdForFalseMatch_finds_lowest_safe_threshold", () => {
  const trials = { target: [0.9], impostor: [0.1, 0.2, 0.3, 0.8], bestForeign: [] };
  assert.equal(thresholdForFalseMatch(trials, 0.25), 0.8);
  assert.equal(thresholdForFalseMatch(trials, 0.5), 0.3);
  assert.ok(thresholdForFalseMatch(trials, 0) > 0.8);
});

test("hashSeed_is_stable_and_varies", () => {
  assert.equal(hashSeed("a.wav"), hashSeed("a.wav"));
  assert.notEqual(hashSeed("a.wav"), hashSeed("b.wav"));
});
