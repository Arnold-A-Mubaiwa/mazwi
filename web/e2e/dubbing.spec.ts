import { test, expect, type Page } from "@playwright/test";
import { randomUUID } from "node:crypto";
import { unlink } from "node:fs/promises";
import { resolve } from "node:path";

const fixtures: { id: string; files: string[] }[] = [];
function wav(seconds = 2) {
  const b = Buffer.alloc(44 + 32000 * seconds); b.write("RIFF"); b.writeUInt32LE(b.length - 8, 4); b.write("WAVEfmt ", 8); b.writeUInt32LE(16, 16);
  b.writeUInt16LE(1, 20); b.writeUInt16LE(1, 22); b.writeUInt32LE(16000, 24); b.writeUInt32LE(32000, 28);
  b.writeUInt16LE(2, 32); b.writeUInt16LE(16, 34); b.write("data", 36); b.writeUInt32LE(b.length - 44, 40); return b;
}
// No ffmpeg here: record a short WebM from a canvas inside the browser.
async function webm(page: Page, milliseconds = 2500): Promise<Buffer> {
  await page.goto("/about");
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
test.afterEach(async ({ request }) => {
  for (const fixture of fixtures.splice(0)) {
    expect((await request.put(`/api/stories/${fixture.id}/drafts/transcript`, { data: { text: `Closed dubbing fixture ${randomUUID()}` } })).ok()).toBe(true);
    for (const file of fixture.files) await unlink(resolve(`../.local/browser-media/${file}`)).catch(() => {});
  }
});

test("Dubbing_VideoOriginalWithTranslatedVoice_PlaysVideoWithVoiceInSync", async ({ page, request }) => {
  const created = await request.post("/api/stories", { multipart: {
    title: `Dubbing fixture ${randomUUID().slice(0, 8)}`, artistName: "Mazwi browser fixture", community: "Shona, Zimbabwe", country: "ZW", type: "story", language: "sn", permission: "true",
    recording: { name: "original.webm", mimeType: "video/webm", buffer: await webm(page) },
  } });
  expect(created.status()).toBe(201);
  const { id } = await created.json();
  const fixture = { id, files: [`${id.replaceAll("-", "")}.webm`] }; fixtures.push(fixture);
  const drafts = `/api/stories/${id}/drafts`;
  await request.put(`${drafts}/transcript`, { data: { text: "Kare kare" } });
  let draft = await (await request.put(`${drafts}/translations/xh`, { data: { text: "Kudala" } })).json();
  expect((await request.post(`/api/stories/${id}/approvals/sn`, { data: { reviewVersion: draft.reviewVersion } })).ok()).toBe(true);
  draft = await (await request.post(`/api/stories/${id}/recordings/xh`, { multipart: {
    reviewVersion: draft.reviewVersion, permission: "true", recording: { name: "voice.wav", mimeType: "audio/wav", buffer: wav() },
  } })).json();
  fixture.files.push(`${draft.translations[0].recordingId.replaceAll("-", "")}.wav`);
  expect((await request.post(`/api/stories/${id}/approvals/xh`, { data: { reviewVersion: draft.reviewVersion } })).ok()).toBe(true);

  await page.goto(`/story/${id}`);
  await page.getByRole("combobox", { name: "Your language", exact: true }).selectOption("xh");
  await expect(page.getByText(/^Dubbed: artist-provided isiXhosa recording, approved by Mazwi browser fixture\./)).toBeVisible();
  const video = page.locator(".dubbed-player video");
  const voice = page.getByTestId("dubbed-voice");
  await expect(video).toHaveAttribute("data-original-sound", /routed|muted/);
  await video.evaluate(async (element: HTMLVideoElement) => {
    if (element.readyState < 1) await new Promise(done => element.addEventListener("loadedmetadata", done, { once: true }));
    element.muted = true; await element.play();
  });
  await page.waitForTimeout(700);
  const playing = await page.evaluate(() => {
    const picture = document.querySelector<HTMLVideoElement>(".dubbed-player video")!;
    const sound = document.querySelector<HTMLAudioElement>("[data-testid=dubbed-voice]")!;
    return { voicePaused: sound.paused, drift: Math.abs(picture.currentTime - sound.currentTime), videoTime: picture.currentTime };
  });
  expect(playing.voicePaused).toBe(false);
  expect(playing.videoTime).toBeGreaterThan(0.2);
  expect(playing.drift).toBeLessThan(0.35);
  await video.evaluate((element: HTMLVideoElement) => element.pause());
  await expect.poll(() => voice.evaluate((element: HTMLAudioElement) => element.paused)).toBe(true);
  await video.evaluate((element: HTMLVideoElement) => { element.currentTime = 0.2; });
  await expect.poll(() => voice.evaluate((element: HTMLAudioElement) => Math.abs(element.currentTime - 0.2) < 0.1)).toBe(true);

  await page.getByRole("button", { name: "Watch with original sound", exact: true }).click();
  await expect(page.locator(".dubbed-player")).toHaveCount(0);
  await expect(page.locator("video")).toBeVisible();
  await page.getByRole("button", { name: "Watch dubbed", exact: true }).click();
  await expect(page.locator(".dubbed-player video")).toBeVisible();
  await page.screenshot({ path: "../.local/mazwi-dubbed-desktop.png", fullPage: true });
});
