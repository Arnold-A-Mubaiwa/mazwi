import { test, expect, type APIRequestContext, type Page, type Route } from "@playwright/test";
import { randomUUID } from "node:crypto";
import { unlink } from "node:fs/promises";
import { resolve } from "node:path";
import { ARTIST, AUTH_FILE } from "./artists";

// Level 2 journey against the real API and database, starting signed out:
// sign in → upload a video → AI transcript → correct → time subtitles → approve → AI translation → AI voice
// → time the translation → approve → watch dubbed with subtitles → the play shows in Studio → share.
// Only the three OpenAI-backed endpoints are faked, as in the Level 1 journey; each fake writes through the real API.
const fixtures: { id: string; files: string[] }[] = [];
test.use({ storageState: { cookies: [], origins: [] } });

function wav(seconds: number) {
  const b = Buffer.alloc(44 + 32000 * seconds); b.write("RIFF"); b.writeUInt32LE(b.length - 8, 4); b.write("WAVEfmt ", 8); b.writeUInt32LE(16, 16);
  b.writeUInt16LE(1, 20); b.writeUInt16LE(1, 22); b.writeUInt32LE(16000, 24); b.writeUInt32LE(32000, 28);
  b.writeUInt16LE(2, 32); b.writeUInt16LE(16, 34); b.write("data", 36); b.writeUInt32LE(b.length - 44, 40); return b;
}
async function webm(page: Page, milliseconds: number): Promise<Buffer> {
  const base64 = await page.evaluate(async duration => {
    const canvas = Object.assign(document.createElement("canvas"), { width: 160, height: 90 });
    const context = canvas.getContext("2d")!;
    const recorder = new MediaRecorder(canvas.captureStream(25), { mimeType: "video/webm" });
    const chunks: Blob[] = [];
    recorder.ondataavailable = event => chunks.push(event.data);
    const stopped = new Promise(done => { recorder.onstop = done; });
    let frame = 0;
    const timer = setInterval(() => { context.fillStyle = `hsl(${(frame += 9) % 360} 60% 50%)`; context.fillRect(0, 0, 160, 90); }, 40);
    recorder.start(100);
    await new Promise(done => setTimeout(done, duration));
    recorder.stop(); await stopped; clearInterval(timer);
    const bytes = new Uint8Array(await new Blob(chunks, { type: "video/webm" }).arrayBuffer());
    let text = ""; for (const byte of bytes) text += String.fromCharCode(byte);
    return btoa(text);
  }, milliseconds);
  return Buffer.from(base64, "base64");
}
test.afterEach(async ({ playwright }) => {
  const request = await playwright.request.newContext({ baseURL: "http://127.0.0.1:3000", storageState: AUTH_FILE });
  for (const fixture of fixtures.splice(0)) {
    expect((await request.put(`/api/stories/${fixture.id}/drafts/transcript`, { data: { text: `Closed level 2 fixture ${randomUUID()}` } })).ok()).toBe(true);
    for (const file of fixture.files) await unlink(resolve(`../.local/browser-media/${file}`)).catch(() => {});
  }
  await request.dispose();
});

async function fakeAi(route: Route, request: APIRequestContext, calls: string[], files: string[]) {
  const path = new URL(route.request().url()).pathname;
  const id = path.split("/")[3];
  const drafts = `/api/stories/${id}/drafts`;
  if (path.endsWith("/transcript/ai-draft")) {
    calls.push("transcript");
    expect((await request.put(`${drafts}/transcript`, { data: { text: "Kare kare zvako\nAIdraft line two" } })).ok()).toBe(true);
  } else if (path.endsWith("/translations/xh/ai-draft")) {
    calls.push("translation");
    expect((await request.put(`${drafts}/translations/xh`, { data: { text: "Kudala kwakukho inkwenkwe.\nUmgca wesibini." } })).ok()).toBe(true);
  } else if (path.endsWith("/translations/xh/ai-voice")) {
    calls.push("voice");
    const { reviewVersion } = route.request().postDataJSON();
    expect((await request.post(`/api/stories/${id}/recordings/xh`, { multipart: { reviewVersion, permission: "true", recording: { name: "voice.wav", mimeType: "audio/wav", buffer: wav(3) } } })).ok()).toBe(true);
  } else return route.continue();
  const draft = await (await request.get(drafts)).json();
  if (path.endsWith("/ai-voice")) {
    const xh = draft.translations.find((t: { language: string }) => t.language === "xh");
    files.push(`${xh.recordingId.replaceAll("-", "")}.wav`); xh.recordingVoice = "standard";
  }
  return route.fulfill({ json: draft });
}

async function time(page: Page, language: string, starts: string[], end: string) {
  const timer = page.locator(".subtitle-timer").filter({ hasText: `Time ${language} subtitles` });
  await timer.getByText(new RegExp(`Time ${language} subtitles · Not timed`)).click();
  for (const [index, start] of starts.entries()) await timer.getByLabel(new RegExp(`Start of line ${index + 1}`)).fill(start);
  await timer.getByLabel("End of the last line (seconds)", { exact: true }).fill(end);
  await timer.getByRole("button", { name: `Save ${language} timing`, exact: true }).click();
  await expect(page.getByRole("main").getByRole("status")).toContainText(`${language} subtitle timing saved`);
}

test("Level2_SignInUploadVideoTimeTranslateVoiceApprove_ViewerWatchesDubbedWithSubtitlesAndArtistSeesThePlay", async ({ page }) => {
  test.skip(process.env.NEXT_PUBLIC_MAZWI_AI_TRANSCRIPT === "false" || process.env.NEXT_PUBLIC_MAZWI_AI_TRANSLATION === "false" || process.env.NEXT_PUBLIC_MAZWI_AI_VOICE === "false", "The journey uses all AI features.");
  const calls: string[] = [];
  const files: string[] = [];
  await page.route(/\/api\/stories\/[^/]+\/drafts\/(transcript|translations\/xh)\/ai-(draft|voice)$/, route => fakeAi(route, page.request, calls, files));
  // The fake voice is an ordinary uploaded file, so mark the feed version as AI voice like the real adapter would.
  await page.route(/\/api\/feed(\/[0-9a-f-]{36})?\?/, async route => {
    const response = await route.fetch(); const body = await response.json();
    for (const item of body.items ?? [body]) if (item?.audioLanguage === "xh") item.audioVoice = "standard";
    return route.fulfill({ response, json: body });
  });
  await page.addInitScript(() => {
    Object.defineProperty(navigator, "share", { configurable: true, value: async (data: ShareData) => { (window as unknown as { shared: ShareData }).shared = data; } });
  });

  // Signed out, Upload asks the artist to sign in and brings them back.
  await page.goto("/upload");
  await expect(page).toHaveURL(/\/signin\?next=%2Fupload$/);
  const video = await webm(page, 2500);
  await page.getByLabel("Email address", { exact: true }).fill(ARTIST.email);
  await page.getByLabel("Password", { exact: true }).fill(ARTIST.password);
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(page).toHaveURL(/\/upload$/);

  const title = `Level 2 ${randomUUID().slice(0, 8)}`;
  await page.getByLabel("Your recording", { exact: true }).setInputFiles({ name: "original.webm", mimeType: "video/webm", buffer: video });
  await page.getByLabel("Story title", { exact: true }).fill(title);
  await page.getByLabel("Artist name", { exact: true }).fill("Mazwi browser fixture");
  await page.getByLabel("Community", { exact: true }).fill("Shona, Zimbabwe");
  await page.getByLabel("Country code", { exact: true }).fill("ZW");
  await page.getByRole("checkbox").check();
  const uploaded = page.waitForResponse(response => response.url().endsWith("/api/stories") && response.request().method() === "POST");
  await page.getByRole("button", { name: "Save my story", exact: true }).click();
  const { id } = await (await uploaded).json();
  fixtures.push({ id, files: [`${id.replaceAll("-", "")}.webm`] });
  await page.getByRole("link", { name: /Write transcript and translations/ }).click();

  // AI transcript, corrected by the artist, timed, then approved.
  await page.getByRole("button", { name: "AI first draft", exact: true }).click();
  const transcript = page.getByLabel("Original transcript", { exact: true });
  await expect(transcript).toHaveValue(/AIdraft line two/);
  await transcript.fill("Kare kare zvako\nAkanga akangwara.");
  await page.getByRole("button", { name: "Save original transcript", exact: true }).click();
  await expect(page.getByRole("main").getByRole("status")).toContainText("Original transcript saved");
  await time(page, "Shona", ["0", "1"], "2");
  await page.getByRole("button", { name: "Approve original", exact: true }).click();
  await expect(page.getByRole("button", { name: "Original is approved", exact: true })).toBeDisabled();

  // AI translation and AI voice, the voice timed, then approved by an artist who does not speak isiXhosa.
  await page.getByLabel("Translation language", { exact: true }).selectOption("xh");
  await page.getByRole("button", { name: "AI draft", exact: true }).click();
  await expect(page.getByLabel("Translation text", { exact: true })).toHaveValue(/Kudala kwakukho inkwenkwe/);
  await page.getByRole("button", { name: "Generate voice", exact: true }).click();
  await expect(page.getByText("AI voice translated recording")).toBeVisible();
  await time(page, "isiXhosa", ["0", "1.2"], "2.8");
  await page.getByRole("button", { name: "Approve translation", exact: true }).click();
  await expect(page.getByText("Not checked by a speaker of isiXhosa. Viewers will see this.")).toBeVisible();
  expect(calls).toEqual(["transcript", "translation", "voice"]);
  fixtures[fixtures.length - 1].files.push(...files);

  // A viewer watches the video dubbed in isiXhosa, with the artist's subtitles.
  await page.goto(`/story/${id}`);
  await page.getByRole("combobox", { name: "Your language", exact: true }).selectOption("xh");
  await expect(page.getByText(/^Dubbed: AI voice, approved by Mazwi browser fixture\./)).toBeVisible();
  await expect(page.getByText("Approved by Mazwi browser fixture. Not checked by an isiXhosa speaker.", { exact: true })).toBeVisible();
  const counted = page.waitForResponse(response => response.url().endsWith(`/api/feed/${id}/plays`) && response.request().method() === "POST");
  await page.locator(".dubbed-player video").evaluate(async (element: HTMLVideoElement) => {
    if (element.readyState < 1) await new Promise(done => element.addEventListener("loadedmetadata", done, { once: true }));
    element.muted = true; await element.play();
  });
  expect((await counted).status()).toBe(204);
  await page.locator(".dubbed-player video").evaluate((element: HTMLVideoElement) => { element.pause(); element.currentTime = 1.5; });
  await expect(page.getByTestId("subtitle-line")).toHaveText("Umgca wesibini.");
  await page.screenshot({ path: "../.local/mazwi-level-2-viewer.png", fullPage: true });

  // Sharing uses the share sheet with the story link.
  await page.getByRole("button", { name: "Share this story", exact: true }).click();
  await expect(page.getByText("Thanks for sharing this story.", { exact: true })).toBeVisible();
  expect((await page.evaluate(() => (window as unknown as { shared: ShareData }).shared)).url).toBe(`http://127.0.0.1:3000/story/${id}`);

  // The artist sees the play in Studio.
  await page.goto("/studio");
  await page.getByLabel("Filter this page", { exact: true }).fill(title);
  await expect(page.locator(".studio-card").filter({ hasText: title }).getByTestId("studio-plays")).toHaveText("1 play · 1 this week");
});
