import test from "node:test";
import assert from "node:assert/strict";
import { createPlayTracker, getStudioStats, playsSummary, recordPlay, PLAY_THRESHOLD_MS } from "../lib/plays.ts";

test("PlayTracker_ThreeSecondsOfListening_FiresOnce", () => {
  let plays = 0;
  const tracker = createPlayTracker(() => { plays += 1; });
  for (let ms = 0; ms <= PLAY_THRESHOLD_MS + 1000; ms += 250) tracker.update(ms, true);
  assert.equal(plays, 1);
});
test("PlayTracker_JustUnderThreshold_DoesNotFire", () => {
  let plays = 0;
  const tracker = createPlayTracker(() => { plays += 1; });
  for (let ms = 0; ms < PLAY_THRESHOLD_MS; ms += 250) tracker.update(ms, true);
  assert.equal(plays, 0);
  tracker.update(PLAY_THRESHOLD_MS, true);
  assert.equal(plays, 1);
});
test("PlayTracker_SeeksPausesAndRewinds_DoNotCount", () => {
  let plays = 0;
  const tracker = createPlayTracker(() => { plays += 1; });
  tracker.update(0, true);
  tracker.update(60_000, true);
  tracker.update(60_250, false);
  tracker.update(62_000, false);
  tracker.update(500, true);
  tracker.update(750, true);
  assert.equal(plays, 0);
});
test("RecordPlay_PostsLanguageAndVersionWithKeepalive", async (t) => {
  t.mock.method(globalThis, "fetch", async (url, options) => {
    assert.equal(url, "/api/feed/story%2F1/plays"); assert.equal(options.method, "POST"); assert.equal(options.keepalive, true);
    assert.deepEqual(JSON.parse(options.body), { language: "xh", version: "v1" });
    return new Response(null, { status: 204 });
  });
  assert.equal(await recordPlay("story/1", "xh", "v1"), true);
});
test("RecordPlay_RejectedOrOffline_ReturnsFalseWithoutThrowing", async (t) => {
  t.mock.method(globalThis, "fetch", async () => new Response("", { status: 404 }));
  assert.equal(await recordPlay("s", "sn", "v"), false);
  t.mock.method(globalThis, "fetch", async () => { throw new TypeError("offline"); });
  assert.equal(await recordPlay("s", "sn", "v"), false);
});
test("GetStudioStats_ValidResponse_MapsByStory", async (t) => {
  t.mock.method(globalThis, "fetch", async (url, options) => {
    assert.equal(url, "/api/studio/stats"); assert.equal(options.cache, "no-store");
    return Response.json([{ storyId: "a", plays: 3, playsLast7Days: 1, languages: [{ language: "sn", plays: 3, playsLast7Days: 1 }] }]);
  });
  assert.equal((await getStudioStats()).get("a").plays, 3);
});
test("GetStudioStats_ErrorOrBadShape_Throws", async (t) => {
  t.mock.method(globalThis, "fetch", async () => new Response("", { status: 401 }));
  await assert.rejects(getStudioStats(), /could not be loaded/);
  t.mock.method(globalThis, "fetch", async () => Response.json([{ storyId: "a", plays: -1, playsLast7Days: 0, languages: [] }]));
  await assert.rejects(getStudioStats(), /unexpected play counts/);
  t.mock.method(globalThis, "fetch", async () => Response.json([{ storyId: "a", plays: 1, playsLast7Days: 0, languages: [{ language: "fr", plays: 1, playsLast7Days: 0 }] }]));
  await assert.rejects(getStudioStats(), /unexpected play counts/);
});
test("PlaysSummary_CountsAndLanguages_ReadNaturally", () => {
  const name = code => ({ sn: "Shona", xh: "isiXhosa" })[code];
  assert.equal(playsSummary(undefined, name), "No plays yet");
  assert.equal(playsSummary({ storyId: "a", plays: 1, playsLast7Days: 1, languages: [{ language: "sn", plays: 1, playsLast7Days: 1 }] }, name), "1 play · 1 this week");
  assert.equal(playsSummary({ storyId: "a", plays: 1200, playsLast7Days: 5, languages: [{ language: "xh", plays: 1000, playsLast7Days: 5 }, { language: "sn", plays: 200, playsLast7Days: 0 }] }, name),
    "1,200 plays · 5 this week · isiXhosa 1,000 · Shona 200");
});
