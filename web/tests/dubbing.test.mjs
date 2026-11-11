import test from "node:test";
import assert from "node:assert/strict";
import { canDub, dubbedLabel, resyncTarget, DRIFT_TOLERANCE } from "../lib/dubbing.ts";

const story = { id: "s", title: "T", artistName: "Rudo", community: "C", country: "ZW", type: "story", originalLanguage: "sn", textLanguage: "xh", audioLanguage: "xh",
  text: "x", recordingExtension: ".wav", originalRecordingExtension: ".mp4", reviewVersion: "v", availableLanguages: ["sn", "xh"], mediaUrl: "/api/feed/s/recording?" };

test("CanDub_VideoOriginalWithTranslatedAudio_IsTrue", () => {
  assert.equal(canDub(story), true);
  assert.equal(canDub({ ...story, originalRecordingExtension: ".webm", recordingExtension: ".m4a" }), true);
});
test("CanDub_AudioOriginalOrVideoTranslationOrOriginalAudio_IsFalse", () => {
  assert.equal(canDub({ ...story, originalRecordingExtension: ".wav" }), false);
  assert.equal(canDub({ ...story, recordingExtension: ".mp4" }), false);
  assert.equal(canDub({ ...story, audioLanguage: "sn", recordingExtension: ".mp4" }), false);
  assert.equal(canDub({ ...story, textLanguage: "sn", audioLanguage: "sn" }), false);
  assert.equal(canDub({ ...story, originalRecordingExtension: undefined }), false);
});
test("DubbedLabel_SaysWhoseVoiceAndThatOriginalSoundIsOff", () => {
  assert.equal(dubbedLabel(story), "Dubbed: artist-provided isiXhosa recording, approved by Rudo. The original video plays without its sound.");
  assert.match(dubbedLabel({ ...story, audioVoice: "standard" }), /^Dubbed: AI voice, approved by Rudo\./);
});
test("ResyncTarget_SmallDrift_LeavesVoiceAlone", () => {
  assert.equal(resyncTarget(5, 5 + DRIFT_TOLERANCE, 60), null);
  assert.equal(resyncTarget(5, 4.9, 60), null);
});
test("ResyncTarget_LargeDrift_MovesVoiceToVideoTime", () => {
  assert.equal(resyncTarget(5, 5.5, 60), 5);
  assert.equal(resyncTarget(10, 2, 60), 10);
  assert.equal(resyncTarget(3, 0, NaN), 3);
});
test("ResyncTarget_PastShorterVoiceOrInvalidTime_ReturnsNull", () => {
  assert.equal(resyncTarget(61, 0, 60), null);
  assert.equal(resyncTarget(60, 0, 60), null);
  assert.equal(resyncTarget(NaN, 0, 60), null);
  assert.equal(resyncTarget(-1, 0, 60), null);
});
