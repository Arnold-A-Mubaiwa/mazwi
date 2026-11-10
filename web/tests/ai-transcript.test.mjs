import test from "node:test";
import assert from "node:assert/strict";
import { draftTranscript } from "../lib/story-drafts.ts";

const id = "11111111-1111-1111-1111-111111111111";
const draft = { id, title: "Story", artistName: "Artist", language: "sn", status: "unpublished", transcript: "Kare kare", translations: [], reviewVersion: id, originalApproved: false, recordingExtension: ".m4a" };

test("DraftTranscript_ValidRequest_PostsVersionOnlyAndPassesCancellation", async t => {
  const controller = new AbortController();
  const request = t.mock.method(globalThis, "fetch", async (url, options) => {
    assert.equal(url, `/api/stories/${id}/drafts/transcript/ai-draft`);
    assert.equal(options.method, "POST"); assert.equal(options.signal, controller.signal);
    assert.deepEqual(JSON.parse(options.body), { reviewVersion: id });
    return Response.json(draft);
  });
  const result = await draftTranscript(id, id, controller.signal);
  assert.equal(result.transcript, "Kare kare");
  assert.equal(result.originalApproved, false);
  assert.equal(request.mock.callCount(), 1);
});

test("DraftTranscript_Errors_ShowTranscriptMessagesWithoutProviderBodyOrRetry", async t => {
  const request = t.mock.method(globalThis, "fetch");
  const expected = { 404: /could not be found/, 409: /changed/, 422: /recording is missing/, 502: /AI transcription is unavailable/, 504: /AI transcription is unavailable/ };
  for (const [status, message] of Object.entries(expected)) {
    request.mock.mockImplementation(async () => new Response("private provider key", { status: Number(status) }));
    await assert.rejects(draftTranscript(id, id), error => message.test(error.message) && !error.message.includes("private provider"));
  }
  assert.equal(request.mock.callCount(), 5);
});
