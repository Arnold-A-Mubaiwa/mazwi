import { test, expect, type APIRequestContext, type Route } from "@playwright/test";
import { randomUUID } from "node:crypto";
import { unlink } from "node:fs/promises";
import { resolve } from "node:path";

// Level 1 journey against the real API and database. Only the three OpenAI-backed endpoints are faked:
// each fake writes the equivalent text or recording through the real API, so saving, approval, revocation
// and the feed are all real. The AI-voice marker is added by the fake (media_voice is covered by API tests).
const fixtures: string[] = [];
const files: string[] = [];
function wav() {
  const b = Buffer.alloc(32044); b.write("RIFF"); b.writeUInt32LE(b.length - 8, 4); b.write("WAVEfmt ", 8); b.writeUInt32LE(16, 16);
  b.writeUInt16LE(1, 20); b.writeUInt16LE(1, 22); b.writeUInt32LE(16000, 24); b.writeUInt32LE(32000, 28);
  b.writeUInt16LE(2, 32); b.writeUInt16LE(16, 34); b.write("data", 36); b.writeUInt32LE(b.length - 44, 40); return b;
}
test.afterEach(async ({ request }) => {
  for (const id of fixtures.splice(0)) expect((await request.put(`/api/stories/${id}/drafts/transcript`, { data: { text: `Closed level 1 fixture ${randomUUID()}` } })).ok()).toBe(true);
  for (const id of files.splice(0)) if (/^[0-9a-f-]{36}$/i.test(id)) await unlink(resolve(`../.local/browser-media/${id.replaceAll("-", "")}.wav`)).catch(() => {});
});

async function fakeAi(route: Route, request: APIRequestContext, calls: string[]) {
  const path = new URL(route.request().url()).pathname;
  const id = path.split("/")[3];
  const drafts = `/api/stories/${id}/drafts`;
  if (path.endsWith("/transcript/ai-draft")) {
    calls.push("transcript");
    expect((await request.put(`${drafts}/transcript`, { data: { text: "Kare kare zvako paive nemukomana\nAIdraft line two" } })).ok()).toBe(true);
  } else if (path.endsWith("/translations/xh/ai-draft")) {
    calls.push("translation");
    expect((await request.put(`${drafts}/translations/xh`, { data: { text: "Kudala kwakukho inkwenkwe.\nUmgca wesibini." } })).ok()).toBe(true);
  } else if (path.endsWith("/translations/xh/ai-voice")) {
    calls.push("voice");
    const { reviewVersion } = route.request().postDataJSON();
    expect((await request.post(`/api/stories/${id}/recordings/xh`, { multipart: { reviewVersion, permission: "true", recording: { name: "voice.wav", mimeType: "audio/wav", buffer: wav() } } })).ok()).toBe(true);
  } else return route.continue();
  const draft = await (await request.get(drafts)).json();
  if (path.endsWith("/ai-voice")) {
    const xh = draft.translations.find((t: { language: string }) => t.language === "xh");
    files.push(xh.recordingId); xh.recordingVoice = "standard";
  }
  return route.fulfill({ json: draft });
}

test("Level1_UploadAiTranscriptTranslateVoiceApprove_ShowsLabelledStoryInFeed", async ({ page, request }) => {
  test.skip(process.env.NEXT_PUBLIC_MAZWI_AI_TRANSCRIPT === "false" || process.env.NEXT_PUBLIC_MAZWI_AI_TRANSLATION === "false" || process.env.NEXT_PUBLIC_MAZWI_AI_VOICE === "false", "Level 1 needs all AI features on.");
  const calls: string[] = [];
  await page.route(/\/api\/stories\/[^/]+\/drafts\/(transcript|translations\/xh)\/ai-(draft|voice)$/, route => fakeAi(route, request, calls));
  // The fake voice is an ordinary uploaded file, so mark the feed version as AI voice like the real adapter would.
  await page.route(/\/api\/feed(\/[0-9a-f-]{36})?\?/, async route => {
    const response = await route.fetch(); const body = await response.json();
    for (const item of body.items ?? [body]) if (item?.audioLanguage === "xh") item.audioVoice = "standard";
    return route.fulfill({ response, json: body });
  });

  const title = `Level 1 ${randomUUID().slice(0, 8)}`;
  await page.goto("/upload");
  await page.getByLabel("Your recording", { exact: true }).setInputFiles({ name: "original.wav", mimeType: "audio/wav", buffer: wav() });
  await page.getByLabel("Story title", { exact: true }).fill(title);
  await page.getByLabel("Artist name", { exact: true }).fill("Mazwi browser fixture");
  await page.getByLabel("Community", { exact: true }).fill("Shona, Zimbabwe");
  await page.getByLabel("Country code", { exact: true }).fill("ZW");
  await page.getByRole("checkbox").check();
  const uploaded = page.waitForResponse(response => response.url().endsWith("/api/stories") && response.request().method() === "POST");
  await page.getByRole("button", { name: "Save my story", exact: true }).click();
  const { id } = await (await uploaded).json(); fixtures.push(id); files.push(id);
  await page.getByRole("link", { name: /Write transcript and translations/ }).click();

  // AI transcript first draft, then the artist corrects it.
  await page.getByRole("button", { name: "AI first draft", exact: true }).click();
  const transcript = page.getByLabel("Original transcript", { exact: true });
  await expect(transcript).toHaveValue(/AIdraft line two/);
  await transcript.fill("Kare kare zvako paive nemukomana\nAkanga akangwara.");
  await page.getByRole("button", { name: "Save original transcript", exact: true }).click();
  await expect(page.getByRole("main").getByRole("status")).toContainText("Original transcript saved");
  await page.getByRole("button", { name: "Approve original", exact: true }).click();
  await expect(page.getByRole("button", { name: "Original is approved", exact: true })).toBeDisabled();

  // AI translation draft, approved by an artist who does not speak isiXhosa.
  await page.getByLabel("Translation language", { exact: true }).selectOption("xh");
  await page.getByRole("button", { name: "AI draft", exact: true }).click();
  await expect(page.getByLabel("Translation text", { exact: true })).toHaveValue(/Kudala kwakukho inkwenkwe/);
  await expect(page.getByLabel("I speak isiXhosa and checked this translation")).not.toBeChecked();
  await page.getByRole("button", { name: "Approve translation", exact: true }).click();
  await expect(page.getByRole("button", { name: "Translation is approved", exact: true })).toBeDisabled();

  // AI voice replaces the recording, which needs approval again.
  await page.getByRole("button", { name: "Generate voice", exact: true }).click();
  await expect(page.getByText("AI voice translated recording")).toBeVisible();
  await page.getByRole("button", { name: "Approve translation", exact: true }).click();
  await expect(page.getByText("Not checked by a speaker of isiXhosa. Viewers will see this.")).toBeVisible();
  expect(calls).toEqual(["transcript", "translation", "voice"]);

  // The viewer sees the isiXhosa version with honest labels.
  await page.goto(`/story/${id}`);
  await page.getByRole("combobox", { name: "Your language", exact: true }).selectOption("xh");
  await expect(page.locator(".viewer-card").getByRole("heading", { name: title })).toBeVisible();
  await expect(page.locator(".viewer-text")).toContainText("Kudala kwakukho inkwenkwe.");
  await expect(page.getByText("AI voice, approved by Mazwi browser fixture", { exact: true })).toBeVisible();
  await expect(page.getByText("Approved by Mazwi browser fixture. Not checked by an isiXhosa speaker.", { exact: true })).toBeVisible();
});
