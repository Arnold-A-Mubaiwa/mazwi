import { test, expect } from "@playwright/test";
import { randomUUID } from "node:crypto";
import { unlink } from "node:fs/promises";
import { resolve } from "node:path";

const fixtures: string[] = [];
function wav(seconds: number) {
  const b = Buffer.alloc(44 + 32000 * seconds); b.write("RIFF"); b.writeUInt32LE(b.length - 8, 4); b.write("WAVEfmt ", 8); b.writeUInt32LE(16, 16);
  b.writeUInt16LE(1, 20); b.writeUInt16LE(1, 22); b.writeUInt32LE(16000, 24); b.writeUInt32LE(32000, 28);
  b.writeUInt16LE(2, 32); b.writeUInt16LE(16, 34); b.write("data", 36); b.writeUInt32LE(b.length - 44, 40); return b;
}
test.afterEach(async ({ request }) => {
  for (const id of fixtures.splice(0)) {
    expect((await request.put(`/api/stories/${id}/drafts/transcript`, { data: { text: `Closed play fixture ${randomUUID()}` } })).ok()).toBe(true);
    await unlink(resolve(`../.local/browser-media/${id.replaceAll("-", "")}.wav`)).catch(() => {});
  }
});

test("Plays_ViewerListensThreeSeconds_ArtistSeesOnePlayInStudio", async ({ page, request }) => {
  const title = `Play fixture ${randomUUID().slice(0, 8)}`;
  const created = await request.post("/api/stories", { multipart: {
    title, artistName: "Mazwi browser fixture", community: "Shona, Zimbabwe", country: "ZW", type: "story", language: "sn", permission: "true",
    recording: { name: "original.wav", mimeType: "audio/wav", buffer: wav(5) },
  } });
  const { id } = await created.json(); fixtures.push(id);
  const draft = await (await request.put(`/api/stories/${id}/drafts/transcript`, { data: { text: "Kare kare" } })).json();
  expect((await request.post(`/api/stories/${id}/approvals/sn`, { data: { reviewVersion: draft.reviewVersion } })).ok()).toBe(true);

  await page.goto("/studio");
  await page.getByLabel("Filter this page", { exact: true }).fill(title);
  await expect(page.locator(".studio-card").filter({ hasText: title }).getByTestId("studio-plays")).toHaveText("No plays yet");

  await page.goto(`/story/${id}`);
  const counted = page.waitForResponse(response => response.url().endsWith(`/api/feed/${id}/plays`) && response.request().method() === "POST");
  await page.locator("audio").evaluate(async (audio: HTMLAudioElement) => { audio.muted = true; await audio.play(); });
  expect((await counted).status()).toBe(204);
  await page.waitForTimeout(1200);
  await page.locator("audio").evaluate((audio: HTMLAudioElement) => { audio.currentTime = 0; });

  await page.goto("/studio");
  await page.getByLabel("Filter this page", { exact: true }).fill(title);
  await expect(page.locator(".studio-card").filter({ hasText: title }).getByTestId("studio-plays")).toHaveText("1 play · 1 this week");
});
