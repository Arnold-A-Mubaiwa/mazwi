import test from "node:test";
import assert from "node:assert/strict";
import { decideTwin, getTwins, setTwinsAllowed, twinLine } from "../lib/twins.ts";

const twin = { storyId: "a", storyTitle: "Tsuro na Gudo", twinStoryId: "b", twinTitle: "Ìjàpá", twinArtistName: "ABS", twinLanguage: "yo", twinCommunity: "Yoruba, Nigeria", status: "suggested" };

test("getTwins_ValidList_ReturnsTwins", async (t) => {
  t.mock.method(globalThis, "fetch", async (url, init) => { assert.equal(url, "/api/studio/twins"); assert.equal(init.cache, "no-store"); return Response.json([twin]); });
  assert.deepEqual(await getTwins(), [twin]);
});

test("getTwins_ErrorsOrBadShape_Throw", async (t) => {
  t.mock.method(globalThis, "fetch", async () => new Response("", { status: 401 }));
  await assert.rejects(getTwins(), /Sign in/);
  t.mock.method(globalThis, "fetch", async () => new Response("", { status: 500 }));
  await assert.rejects(getTwins(), /could not be loaded/);
  for (const bad of [{ ...twin, status: "rejected" }, { ...twin, twinLanguage: "de" }, { ...twin, gist: undefined, storyId: 1 }]) {
    t.mock.method(globalThis, "fetch", async () => Response.json([bad]));
    await assert.rejects(getTwins(), /unexpected Story Twins/);
  }
});

test("decideTwin_Responses_MapToMessages", async (t) => {
  t.mock.method(globalThis, "fetch", async (url, init) => { assert.equal(url, "/api/stories/a/twins/b/confirm"); assert.equal(init.method, "POST"); return new Response(null, { status: 204 }); });
  await decideTwin("a", "b", "confirm");
  t.mock.method(globalThis, "fetch", async () => new Response(null, { status: 404 }));
  await assert.rejects(decideTwin("a", "b", "reject"), /no longer available/);
  t.mock.method(globalThis, "fetch", async () => new Response(null, { status: 403 }));
  await assert.rejects(decideTwin("a", "b", "reject"), /Only the story/);
  t.mock.method(globalThis, "fetch", async () => new Response(null, { status: 500 }));
  await assert.rejects(decideTwin("a", "b", "reject"), /did not work/);
});

test("setTwinsAllowed_Choice_SendsItAndReadsDraft", async (t) => {
  const draft = { id: "a", title: "T", artistName: "A", language: "sn", status: "published", reviewVersion: "v", originalApproved: true, recordingExtension: ".wav", transcript: "x", translations: [], twinsAllowed: false };
  t.mock.method(globalThis, "fetch", async (url, init) => { assert.equal(url, "/api/stories/a/twinning"); assert.equal(init.method, "PUT"); assert.equal(init.body, JSON.stringify({ allowed: false })); return Response.json(draft); });
  assert.equal((await setTwinsAllowed("a", false)).twinsAllowed, false);
  t.mock.method(globalThis, "fetch", async () => Response.json({ ...draft, twinsAllowed: "no" }));
  await assert.rejects(setTwinsAllowed("a", false), /unexpected story/);
});

test("twinLine_Twin_NamesLanguageArtistAndCommunity", () => {
  assert.equal(twinLine(twin), "Told in Yoruba by ABS (Yoruba, Nigeria)");
  assert.equal(twinLine({ ...twin, twinLanguage: "xx" }), "Told in xx by ABS (Yoruba, Nigeria)");
});
