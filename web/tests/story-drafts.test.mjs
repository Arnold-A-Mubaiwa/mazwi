import test from "node:test";
import assert from "node:assert/strict";
import { storyTextError, getStoryDraft, saveStoryText } from "../lib/story-drafts.ts";

const id = "11111111-1111-1111-1111-111111111111";
const draft = { id, title: "Story", artistName: "Artist", language: "sn", status: "unpublished", transcript: null, translations: [] };

test("StoryTextError_EmptyOverlongAndControlCharacters_RejectsInput", () => {
  for (const text of ["", " \n\t ", "a".repeat(20001), "text\0text", "text\u0085text"]) assert.notEqual(storyTextError(text), null);
});
test("StoryTextError_BoundariesAndPoemFormatting_AcceptsInput", () => {
  for (const text of ["a", "a".repeat(20000), " Tsuro\n\nna Gudo\tSekuru "]) assert.equal(storyTextError(text), null);
});
test("GetStoryDraft_ValidResponse_DisablesCacheAndPassesCancellation", async (t) => {
  const controller = new AbortController();
  t.mock.method(globalThis, "fetch", async (url, options) => {
    assert.equal(url, `/api/stories/${id}/drafts`);
    assert.equal(options.cache, "no-store");
    assert.equal(options.signal, controller.signal);
    return Response.json(draft);
  });
  assert.deepEqual(await getStoryDraft(id, controller.signal), draft);
});
test("SaveStoryText_TranscriptAndTranslation_PutsExactTextAndPassesCancellation", async (t) => {
  const controller = new AbortController();
  const requests = [];
  t.mock.method(globalThis, "fetch", async (url, options) => {
    requests.push(url);
    assert.equal(options.method, "PUT");
    assert.equal(options.headers["Content-Type"], "application/json");
    assert.equal(options.signal, controller.signal);
    assert.equal(JSON.parse(options.body).text, "Line one\nLine two");
    return Response.json({ ...draft, transcript: "Line one\nLine two" });
  });
  await saveStoryText(id, "transcript", "Line one\nLine two", controller.signal);
  await saveStoryText(id, "xh", "Line one\nLine two", controller.signal);
  assert.deepEqual(requests, [`/api/stories/${id}/drafts/transcript`, `/api/stories/${id}/drafts/translations/xh`]);
});
test("SaveStoryText_InvalidText_DoesNotMakeRequest", async (t) => {
  const request = t.mock.method(globalThis, "fetch", async () => { throw Error("unexpected request"); });
  await assert.rejects(saveStoryText(id, "transcript", " "), /1–20,000/);
  assert.equal(request.mock.callCount(), 0);
});
test("GetStoryDraft_NotFoundAndServerErrors_AreActionableAndSafe", async (t) => {
  const request = t.mock.method(globalThis, "fetch", async () => new Response("", { status: 404 }));
  await assert.rejects(getStoryDraft(id), /could not be found/);
  request.mock.mockImplementation(async () => new Response("private database detail", { status: 500 }));
  await assert.rejects(getStoryDraft(id), /Any text you entered is still here/);
  request.mock.mockImplementation(async () => Response.json({ detail: "Save the original first." }, { status: 400 }));
  await assert.rejects(getStoryDraft(id), /Save the original first/);
  request.mock.mockImplementation(async () => new Response("not json", { status: 400 }));
  await assert.rejects(getStoryDraft(id), /Check the text/);
});
test("GetStoryDraft_WrongStoryOrApprovedResponse_DoesNotClaimSavedDrafts", async (t) => {
  const request = t.mock.method(globalThis, "fetch", async () => Response.json({ ...draft, id: "wrong" }));
  await assert.rejects(getStoryDraft(id), /unexpected story/);
  request.mock.mockImplementation(async () => Response.json({ ...draft, status: "published" }));
  await assert.rejects(getStoryDraft(id), /unexpected story/);
  request.mock.mockImplementation(async () => Response.json({ ...draft, translations: [{ language: "xh", text: "Words", status: "approved" }] }));
  await assert.rejects(getStoryDraft(id), /unexpected story/);
});
