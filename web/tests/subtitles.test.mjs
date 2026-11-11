import test from "node:test";
import assert from "node:assert/strict";
import { cueAt, formatSeconds, isCue, isTiming, parseSeconds, saveSubtitleTiming, subtitleLines, timingError, MAX_SUBTITLE_END_MS } from "../lib/subtitles.ts";
import { getViewerStory } from "../lib/feed.ts";
import { readDraft } from "../lib/story-drafts.ts";

const ID = "8f6b1a52-3d2c-4b8e-9a1f-0c2d3e4f5a6b";

test("SubtitleLines_BlankLinesAndCarriageReturns_MatchApiRule", () => {
  assert.deepEqual(subtitleLines("Kare kare\r\n\n  Pane imwe nguva  \n"), ["Kare kare", "Pane imwe nguva"]);
  assert.deepEqual(subtitleLines("   \n"), []);
});
test("ParseSeconds_TypedValues_BecomeMillisecondsOrNull", () => {
  assert.equal(parseSeconds("1.5"), 1500);
  assert.equal(parseSeconds(" 0 "), 0);
  assert.equal(parseSeconds("12.345"), 12345);
  for (const value of ["", "-1", "1,5", "abc", "1.2345", "1e3"]) assert.equal(parseSeconds(value), null);
  assert.equal(formatSeconds(1540), "1.5");
});
test("TimingError_ValidTiming_ReturnsNull", () => {
  assert.equal(timingError([0, 1500], 3000, 2), null);
  assert.equal(timingError([0], MAX_SUBTITLE_END_MS, 1), null);
});
test("TimingError_InvalidTiming_ExplainsTheProblem", () => {
  assert.match(timingError([], 10, 0), /Save some text/);
  assert.match(timingError([0, null], 10, 2), /every line/);
  assert.match(timingError([0], 10, 2), /every line/);
  assert.match(timingError([500, 500], 1000, 2), /after the line before/);
  assert.match(timingError([0, 500], 500, 2), /end time after/);
  assert.match(timingError([0], null, 1), /end time after/);
  assert.match(timingError([0], MAX_SUBTITLE_END_MS + 1, 1), /four hours/);
});
test("CueAt_Boundaries_StartIsInclusiveEndIsExclusive", () => {
  const cues = [{ startMs: 0, endMs: 1000, text: "One" }, { startMs: 1000, endMs: 2000, text: "Two" }];
  assert.equal(cueAt(cues, 0).text, "One");
  assert.equal(cueAt(cues, 999).text, "One");
  assert.equal(cueAt(cues, 1000).text, "Two");
  assert.equal(cueAt(cues, 2000), null);
  assert.equal(cueAt(null, 10), null);
});
test("Validators_RejectMalformedShapes", () => {
  assert.equal(isTiming({ startsMs: [0, 1], endMs: 2 }), true);
  assert.equal(isTiming({ startsMs: [0.5], endMs: 2 }), false);
  assert.equal(isTiming(null), false);
  assert.equal(isCue({ startMs: 0, endMs: 1, text: "a" }), true);
  assert.equal(isCue({ startMs: 0, endMs: 1 }), false);
});
test("SaveSubtitleTiming_ValidTiming_PutsTimesWithReviewVersion", async (t) => {
  const controller = new AbortController();
  t.mock.method(globalThis, "fetch", async (url, options) => {
    assert.equal(url, `/api/stories/${ID}/subtitles/xh`); assert.equal(options.method, "PUT"); assert.equal(options.signal, controller.signal);
    assert.deepEqual(JSON.parse(options.body), { startsMs: [0, 1500], endMs: 3000, reviewVersion: "v1" });
    return Response.json({ id: ID, title: "T", artistName: "A", language: "sn", status: "unpublished", reviewVersion: "v2", originalApproved: false, recordingExtension: ".wav", transcript: "a\nb",
      translations: [{ language: "xh", text: "c\nd", status: "draft", timing: { startsMs: [0, 1500], endMs: 3000 } }] });
  });
  const draft = await saveSubtitleTiming(ID, "xh", { startsMs: [0, 1500], endMs: 3000 }, "v1", controller.signal);
  assert.deepEqual(draft.translations[0].timing, { startsMs: [0, 1500], endMs: 3000 });
});
test("SaveSubtitleTiming_ApiRejects_ShowsDetailOrConflict", async (t) => {
  t.mock.method(globalThis, "fetch", async () => Response.json({ detail: "Time every line: the text has 2 lines but 1 times were sent." }, { status: 400 }));
  await assert.rejects(saveSubtitleTiming(ID, "sn", { startsMs: [0], endMs: 10 }, "v1"), /Time every line/);
  t.mock.method(globalThis, "fetch", async () => new Response("", { status: 409 }));
  await assert.rejects(saveSubtitleTiming(ID, "sn", { startsMs: [0], endMs: 10 }, "v1"), /This story changed/);
});
test("ReadDraft_MalformedTiming_DoesNotClaimSaved", async () => {
  const body = { id: ID, title: "T", artistName: "A", language: "sn", status: "unpublished", reviewVersion: "v", originalApproved: false, recordingExtension: ".wav", transcript: "a", translations: [], originalTiming: { startsMs: "0", endMs: 1 } };
  await assert.rejects(readDraft(Response.json(body), ID), /unexpected story/);
});
test("GetViewerStory_CuesAndSubtitlesUrl_AreValidated", async (t) => {
  const story = { id: ID, title: "T", artistName: "A", community: "C", country: "ZW", type: "story", originalLanguage: "sn", textLanguage: "sn", audioLanguage: "sn", text: "a\nb",
    recordingExtension: ".wav", reviewVersion: "v", availableLanguages: ["sn"], mediaUrl: `/api/feed/${ID}/recording?language=sn`,
    cues: [{ startMs: 0, endMs: 1000, text: "a" }], subtitlesUrl: `/api/feed/${ID}/subtitles?language=sn` };
  t.mock.method(globalThis, "fetch", async () => Response.json(story));
  assert.equal((await getViewerStory(ID, "sn")).cues[0].text, "a");
  t.mock.method(globalThis, "fetch", async () => Response.json({ ...story, subtitlesUrl: "https://evil.example/x.vtt" }));
  await assert.rejects(getViewerStory(ID, "sn"), /unexpected story/);
  t.mock.method(globalThis, "fetch", async () => Response.json({ ...story, cues: [{ startMs: "0" }] }));
  await assert.rejects(getViewerStory(ID, "sn"), /unexpected story/);
});
