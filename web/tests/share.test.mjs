import test from "node:test";
import assert from "node:assert/strict";
import { shareMessage, shareStory, storyShareData, takeSharedRecording, SHARE_CACHE, SHARE_KEY } from "../lib/share.ts";
import manifest, { SHARE_TARGET_ACTION, SHARED_RECORDING_FIELD } from "../app/manifest.ts";

const data = storyShareData("Tsuro", "Rudo", "https://mazwi.example/story/1");

test("StoryShareData_TitleArtistAndUrl_AreIncluded", () => {
  assert.deepEqual(data, { title: "Tsuro | Mazwi", text: "Tsuro by Rudo, an African story on Mazwi.", url: "https://mazwi.example/story/1" });
});
test("ShareStory_ShareSheetAvailable_UsesIt", async () => {
  let shared = null;
  assert.equal(await shareStory(data, { share: async value => { shared = value; } }), "shared");
  assert.equal(shared, data);
});
test("ShareStory_ViewerCancels_DoesNotCopy", async () => {
  let copied = false;
  const result = await shareStory(data, { share: async () => { throw Object.assign(new Error("cancel"), { name: "AbortError" }); }, clipboard: { writeText: async () => { copied = true; } } });
  assert.equal(result, "cancelled"); assert.equal(copied, false);
});
test("ShareStory_NoShareSheetOrShareFails_CopiesTheLink", async () => {
  let copied = "";
  const clipboard = { writeText: async text => { copied = text; } };
  assert.equal(await shareStory(data, { clipboard }), "copied");
  assert.equal(copied, data.url);
  assert.equal(await shareStory(data, { share: async () => { throw new Error("NotAllowedError"); }, clipboard }), "copied");
});
test("ShareStory_NothingWorks_Fails", async () => {
  assert.equal(await shareStory(data, {}), "failed");
  assert.equal(await shareStory(data, { clipboard: { writeText: async () => { throw new Error("denied"); } } }), "failed");
});
test("ShareMessage_EachResult_TellsTheViewerWhatHappened", () => {
  assert.match(shareMessage("shared", data.url), /Thanks/);
  assert.match(shareMessage("copied", data.url), /Link copied/);
  assert.equal(shareMessage("cancelled", data.url), "");
  assert.match(shareMessage("failed", data.url), /mazwi\.example\/story\/1/);
});
test("TakeSharedRecording_StoredFile_ReturnsItOnceWithItsName", async () => {
  const entries = new Map([[SHARE_KEY, new Response(new Blob(["RIFF"], { type: "audio/wav" }), { headers: { "X-File-Name": encodeURIComponent("my story.wav") } })]]);
  const storage = { open: async name => { assert.equal(name, SHARE_CACHE); return { match: async key => entries.get(key), delete: async key => entries.delete(key) }; } };
  const file = await takeSharedRecording(storage);
  assert.equal(file.name, "my story.wav"); assert.equal(file.type, "audio/wav"); assert.equal(file.size, 4);
  assert.equal(await takeSharedRecording(storage), null);
  assert.equal(await takeSharedRecording(undefined), null);
});
test("Manifest_Installable_HasIconsScopeAndRecordingShareTarget", () => {
  const value = manifest();
  assert.equal(value.display, "standalone"); assert.equal(value.start_url, "/"); assert.equal(value.scope, "/");
  assert.deepEqual(value.icons.map(icon => icon.sizes), ["192x192", "512x512", "512x512"]);
  assert.ok(value.icons.some(icon => icon.purpose === "maskable"));
  assert.equal(value.share_target.action, SHARE_TARGET_ACTION);
  assert.equal(value.share_target.method, "POST"); assert.equal(value.share_target.enctype, "multipart/form-data");
  const files = value.share_target.params.files[0];
  assert.equal(files.name, SHARED_RECORDING_FIELD);
  assert.ok(files.accept.includes("audio/*") && files.accept.includes("video/*"));
});
