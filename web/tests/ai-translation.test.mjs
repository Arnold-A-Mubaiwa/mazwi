import test from "node:test";
import assert from "node:assert/strict";
import { draftTranslation, saveStoryText, readDraft } from "../lib/story-drafts.ts";

const id = "11111111-1111-1111-1111-111111111111";
const draft = { id, title: "Story", artistName: "Artist", language: "sn", status: "published", transcript: "Original", translations: [{ language: "xh", text: "Molo", status: "draft", notes: ["Name retained"] }], reviewVersion: id, originalApproved: true, recordingExtension: ".wav" };

test("DraftTranslation_ValidRequest_PostsVersionOnlyAndPassesCancellation", async t => {
  const controller = new AbortController();
  const request = t.mock.method(globalThis, "fetch", async (url, options) => {
    assert.equal(url, `/api/stories/${id}/drafts/translations/xh/ai-draft`);
    assert.equal(options.method, "POST"); assert.equal(options.signal, controller.signal);
    assert.deepEqual(JSON.parse(options.body), { reviewVersion: id });
    return Response.json(draft);
  });
  assert.deepEqual(await draftTranslation(id, "xh", id, controller.signal), draft);
  assert.equal(request.mock.callCount(), 1);
});

test("SaveStoryText_NotesEditedOrDismissed_SendsExactNotes", async t => {
  const notes = [["Edited note"], []];
  let index = 0;
  t.mock.method(globalThis, "fetch", async (_, options) => {
    assert.deepEqual(JSON.parse(options.body).notes, notes[index++]);
    return Response.json(draft);
  });
  for (const values of notes) await saveStoryText(id, "xh", "Molo", undefined, values);
});

test("DraftTranslation_Errors_DoNotExposeProviderBodyOrRetry", async t => {
  const request = t.mock.method(globalThis, "fetch");
  for (const status of [422, 409, 502, 504]) {
    request.mock.mockImplementation(async () => new Response("private provider key", { status }));
    await assert.rejects(draftTranslation(id, "xh", id), error => !error.message.includes("private provider"));
  }
  assert.equal(request.mock.callCount(), 4);
});

test("ReadDraft_InvalidNotes_RejectsMalformedResponse", async () => {
  for (const notes of ["wrong", [null], ["x".repeat(1001)], Array(21).fill("note")]) {
    await assert.rejects(readDraft(Response.json({ ...draft, translations: [{ ...draft.translations[0], notes }] }), id), /unexpected story/);
  }
});
