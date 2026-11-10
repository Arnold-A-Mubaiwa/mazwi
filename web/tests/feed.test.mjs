import test from "node:test";
import assert from "node:assert/strict";
import { getFeed, getViewerStory, playbackLabel, viewingNote } from "../lib/feed.ts";

const id = "11111111-1111-1111-1111-111111111111";
const story = { id, title: "Story", artistName: "Artist", community: "Shona", country: "ZW", type: "story", originalLanguage: "sn", textLanguage: "xh", audioLanguage: "sn", text: "Approved words", recordingExtension: ".wav", reviewVersion: id, availableLanguages: ["sn", "xh"], mediaUrl: `/api/feed/${id}/recording?language=xh` };
test("ViewerLabels_OriginalAudioAndMissingPreferredVersion_AreExplicit", () => {
  assert.equal(playbackLabel(story), "Original Shona audio");
  assert.match(viewingNote(story, "xh"), /isiXhosa text with original Shona audio/);
  assert.match(viewingNote({ ...story, textLanguage: "sn" }, "xh"), /isn’t available/);
  assert.match(playbackLabel({ ...story, audioLanguage: "xh" }), /Artist-provided isiXhosa/);
});
test("GetFeed_ApprovedPage_PassesLanguagePaginationAndCancellation", async (t) => {
  const controller = new AbortController();
  t.mock.method(globalThis, "fetch", async (url, options) => {
    assert.equal(url, "/api/feed?language=xh&offset=20&limit=20"); assert.equal(options.cache, "no-store"); assert.equal(options.signal, controller.signal);
    return Response.json({ items: [story], nextOffset: 40 });
  });
  assert.equal((await getFeed("xh", 20, controller.signal)).items[0].text, "Approved words");
});
test("GetFeed_InvalidPaginationOrExternalMedia_RejectsResponse", async (t) => {
  const request = t.mock.method(globalThis, "fetch", async () => Response.json({ items: [story], nextOffset: 0 }));
  await assert.rejects(getFeed("sn"), /unexpected stories/);
  request.mock.mockImplementation(async () => Response.json({ items: [{ ...story, mediaUrl: "https://other.example/media" }], nextOffset: null }));
  await assert.rejects(getFeed("sn"), /unexpected stories/);
});
test("GetViewerStory_UnpublishedOrWrongId_DoesNotClaimAvailable", async (t) => {
  const request = t.mock.method(globalThis, "fetch", async () => new Response("", { status: 404 }));
  await assert.rejects(getViewerStory(id, "sn"), /unpublished or awaiting approval/);
  request.mock.mockImplementation(async () => Response.json({ ...story, id: "other" }));
  await assert.rejects(getViewerStory(id, "sn"), /unexpected story/);
});
