import test from "node:test";
import assert from "node:assert/strict";
import { recordingError, uploadStory } from "../lib/stories.ts";

test("RecordingError_MissingEmptyUnsupportedAndOversizedFiles_Rejects", () => {
  assert.match(recordingError(null), /non-empty/);
  assert.match(recordingError(new File([], "clip.wav")), /non-empty/);
  assert.match(recordingError(new File(["data"], "clip.exe")), /MP3/);
  assert.match(recordingError(new File([new Uint8Array(25_000_001)], "clip.mp4")), /25 MB/);
});
test("RecordingError_SupportedFilesAndBoundary_Accepts", () => {
  for (const ext of ["mp3", "wav", "M4A", "mp4", "webm"]) assert.equal(recordingError(new File(["data"], `clip.${ext}`)), null);
  assert.equal(recordingError(new File([new Uint8Array(25_000_000)], "clip.mp4")), null);
});
test("UploadStory_ValidResponse_PassesMultipartAndCancellation", async (t) => {
  const form = new FormData();
  const controller = new AbortController();
  const story = { id: "story-id", title: "Story", artistName: "Artist", status: "unpublished" };
  t.mock.method(globalThis, "fetch", async (url, options) => {
    assert.equal(url, "/api/stories");
    assert.equal(options.body, form);
    assert.equal(options.method, "POST");
    assert.equal(options.signal, controller.signal);
    assert.equal(options.headers, undefined); // Browser supplies the multipart boundary.
    return Response.json(story, { status: 201 });
  });
  assert.deepEqual(await uploadStory(form, controller.signal), story);
});
test("UploadStory_FailedRequest_ReportsValidationAndSafeServerErrors", async (t) => {
  const request = t.mock.method(globalThis, "fetch", async () => Response.json({ detail: "Permission is required." }, { status: 400 }));
  await assert.rejects(uploadStory(new FormData()), /Permission is required/);
  request.mock.mockImplementation(async () => new Response("private server details", { status: 500 }));
  await assert.rejects(uploadStory(new FormData()), /form has been kept/);
  request.mock.mockImplementation(async () => new Response("", { status: 413 }));
  await assert.rejects(uploadStory(new FormData()), /too large/);
  request.mock.mockImplementation(async () => new Response("not json", { status: 400 }));
  await assert.rejects(uploadStory(new FormData()), /Check all fields/);
});
test("UploadStory_UnexpectedSuccessBody_DoesNotClaimConfirmation", async (t) => {
  t.mock.method(globalThis, "fetch", async () => Response.json({ status: "published" }));
  await assert.rejects(uploadStory(new FormData()), /couldn’t confirm/);
});
