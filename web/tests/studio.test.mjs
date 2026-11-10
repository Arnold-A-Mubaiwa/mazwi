import test from "node:test";
import assert from "node:assert/strict";
import { getStudio } from "../lib/studio.ts";

test("GetStudio_LocalDrafts_PassesPaginationAndCancellation", async (t) => {
  const controller = new AbortController();
  t.mock.method(globalThis, "fetch", async (url, options) => {
    assert.equal(url, "/api/studio?offset=20&limit=20"); assert.equal(options.cache, "no-store"); assert.equal(options.signal, controller.signal);
    return Response.json({ items: [{ id: "story", title: "Story", artistName: "Artist", community: "Shona", language: "sn", status: "unpublished" }], nextOffset: 40 });
  });
  assert.equal((await getStudio(20, controller.signal)).items[0].status, "unpublished");
});
test("GetStudio_ServerErrorOrInvalidBody_DoesNotClaimLoadedStories", async (t) => {
  const request = t.mock.method(globalThis, "fetch", async () => new Response("", { status: 500 }));
  await assert.rejects(getStudio(), /Please try again/);
  request.mock.mockImplementation(async () => Response.json({ items: [{ status: "unknown" }], nextOffset: null }));
  await assert.rejects(getStudio(), /unexpected local stories/);
});
