import test from "node:test";
import assert from "node:assert/strict";
import { speakerNote, viewingNote } from "../lib/feed.ts";
import { approveStory } from "../lib/story-drafts.ts";

const id = "11111111-1111-1111-1111-111111111111";
const story = { id, title: "Story", artistName: "Gogo", community: "Shona", country: "ZW", type: "story", originalLanguage: "sn", textLanguage: "xh", audioLanguage: "xh", text: "Kudala", recordingExtension: ".wav", reviewVersion: id, availableLanguages: ["sn", "xh"], mediaUrl: `/api/feed/${id}/recording?language=xh` };

test("SpeakerNote_NotCheckedOrMissing_SaysNoSpeakerChecked", () => {
  assert.equal(speakerNote({ ...story, speakerChecked: false }), "Approved by Gogo. Not checked by an isiXhosa speaker.");
  assert.equal(speakerNote(story), "Approved by Gogo. Not checked by an isiXhosa speaker.");
  assert.equal(speakerNote({ ...story, textLanguage: "sw" }), "Approved by Gogo. Not checked by a Kiswahili speaker.");
});

test("SpeakerNote_Checked_SaysArtistChecked", () => {
  assert.equal(speakerNote({ ...story, speakerChecked: true }), "Approved by Gogo, who checked the isiXhosa.");
});

test("ViewingNote_TranslationAndOriginal_ShowSpeakerNoteOnlyForTranslations", () => {
  assert.equal(viewingNote({ ...story, speakerChecked: false }, "xh"), "Approved by Gogo. Not checked by an isiXhosa speaker.");
  assert.match(viewingNote({ ...story, audioLanguage: "sn" }, "xh"), /original Shona audio\. No translated recording is available\. Approved by Gogo\. Not checked/);
  assert.equal(viewingNote({ ...story, textLanguage: "sn", audioLanguage: "sn" }, "sn"), "Artist-approved Shona version.");
});

test("ApproveStory_SpeakerCheck_SendsFlagAndDefaultsToFalse", async t => {
  const bodies = [];
  t.mock.method(globalThis, "fetch", async (_url, options) => { bodies.push(JSON.parse(options.body)); return Response.json({ id, title: "S", artistName: "A", language: "sn", status: "published", transcript: "K", translations: [{ language: "xh", text: "Kudala", status: "approved", speakerChecked: true }], reviewVersion: id, originalApproved: true, recordingExtension: ".wav" }); });
  const result = await approveStory(id, "xh", id, undefined, true);
  await approveStory(id, "xh", id);
  assert.deepEqual(bodies, [{ reviewVersion: id, speakerChecked: true }, { reviewVersion: id, speakerChecked: false }]);
  assert.equal(result.translations[0].speakerChecked, true);
});
