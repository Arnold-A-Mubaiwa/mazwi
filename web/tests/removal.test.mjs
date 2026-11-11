import test from "node:test";
import assert from "node:assert/strict";
import { deleteAccount, deleteStory, titleConfirmed, unpublishStory } from "../lib/removal.ts";

const ID = "8f6b1a52-3d2c-4b8e-9a1f-0c2d3e4f5a6b";

test("TitleConfirmed_SameTitleIgnoringCaseAndSpacing_IsTrue", () => {
  assert.equal(titleConfirmed("  tsuro   NA  gudo ", "Tsuro na Gudo"), true);
});
test("TitleConfirmed_EmptyOrDifferent_IsFalse", () => {
  assert.equal(titleConfirmed("", "Tsuro"), false);
  assert.equal(titleConfirmed("   ", "   "), false);
  assert.equal(titleConfirmed("Tsuro na", "Tsuro na Gudo"), false);
});
test("DeleteStory_Success_SendsDeleteWithVersion", async (t) => {
  t.mock.method(globalThis, "fetch", async (url, options) => {
    assert.equal(url, `/api/stories/${ID}?version=v%201`); assert.equal(options.method, "DELETE");
    return new Response(null, { status: 204 });
  });
  await deleteStory(ID, "v 1");
});
test("DeleteStory_Failures_ExplainWhatHappened", async (t) => {
  for (const [status, message] of [[404, /already deleted/], [409, /changed since/], [401, /signed out/], [403, /another artist/], [500, /Nothing was removed/]]) {
    t.mock.method(globalThis, "fetch", async () => new Response("", { status }));
    await assert.rejects(deleteStory(ID, "v"), message);
  }
});
test("UnpublishStory_PostsReviewVersionAndReadsDraft", async (t) => {
  t.mock.method(globalThis, "fetch", async (url, options) => {
    assert.equal(url, `/api/stories/${ID}/unpublish`); assert.deepEqual(JSON.parse(options.body), { reviewVersion: "v" });
    return Response.json({ id: ID, title: "T", artistName: "A", language: "sn", status: "unpublished", reviewVersion: "v2", originalApproved: false, recordingExtension: ".wav", transcript: "x", translations: [] });
  });
  assert.equal((await unpublishStory(ID, "v")).status, "unpublished");
});
test("DeleteAccount_PasswordRequiredAndFailuresExplained", async (t) => {
  await assert.rejects(deleteAccount(""), /Enter your password/);
  t.mock.method(globalThis, "fetch", async (url, options) => {
    assert.equal(url, "/api/account/delete"); assert.deepEqual(JSON.parse(options.body), { password: "long password" });
    return new Response(null, { status: 204 });
  });
  await deleteAccount("long password");
  for (const [status, message] of [[400, /did not match/], [401, /signed out/], [429, /Too many/], [500, /could not be deleted/]]) {
    t.mock.method(globalThis, "fetch", async () => new Response("", { status }));
    await assert.rejects(deleteAccount("x"), message);
  }
});
