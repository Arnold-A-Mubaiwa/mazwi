import test from "node:test";
import assert from "node:assert/strict";
import { generateVoice, readDraft } from "../lib/story-drafts.ts";
import { playbackLabel } from "../lib/feed.ts";

const id = "11111111-1111-1111-1111-111111111111";
const draft = { id, title: "Story", artistName: "Artist", language: "sn", status: "published", transcript: "Original", translations: [{ language: "xh", text: "Molo", status: "draft", recordingId: id, recordingExtension: ".wav", recordingVoice: "standard" }], reviewVersion: id, originalApproved: true, recordingExtension: ".wav" };

test("GenerateVoice_ValidRequest_PostsVersionOnlyAndPassesCancellation", async t => {
  const controller = new AbortController();
  const request = t.mock.method(globalThis, "fetch", async (url, options) => {
    assert.equal(url, `/api/stories/${id}/drafts/translations/xh/ai-voice`);
    assert.equal(options.method, "POST"); assert.equal(options.signal, controller.signal);
    assert.deepEqual(JSON.parse(options.body), { reviewVersion: id });
    return Response.json(draft);
  });
  const result = await generateVoice(id, "xh", id, controller.signal);
  assert.equal(result.translations[0].recordingVoice, "standard");
  assert.equal(request.mock.callCount(), 1);
});

test("GenerateVoice_Errors_ShowVoiceMessagesWithoutProviderBodyOrRetry", async t => {
  const request = t.mock.method(globalThis, "fetch");
  const expected = { 422: /1,800 characters/, 409: /changed/, 502: /AI voice is unavailable/, 504: /AI voice is unavailable/ };
  for (const [status, message] of Object.entries(expected)) {
    request.mock.mockImplementation(async () => new Response("private provider key", { status: Number(status) }));
    await assert.rejects(generateVoice(id, "xh", id), error => message.test(error.message) && !error.message.includes("private provider"));
  }
  assert.equal(request.mock.callCount(), 4);
});

test("ReadDraft_UnknownRecordingVoice_RejectsMalformedResponse", async () => {
  const invalid = { ...draft, translations: [{ ...draft.translations[0], recordingVoice: "robot" }] };
  await assert.rejects(readDraft(Response.json(invalid), id), /unexpected story/);
});

test("PlaybackLabel_AiVoice_NamesApprovingArtist", () => {
  const story = { id, title: "Story", artistName: "Gogo Nomsa", originalLanguage: "sn", textLanguage: "xh", audioLanguage: "xh", audioVoice: "standard" };
  assert.equal(playbackLabel(story), "AI voice, approved by Gogo Nomsa");
  assert.match(playbackLabel({ ...story, audioVoice: null }), /Artist-provided isiXhosa/);
  assert.equal(playbackLabel({ ...story, audioLanguage: "sn" }), "Original Shona audio");
});
