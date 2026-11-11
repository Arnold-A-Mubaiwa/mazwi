import { test, expect } from "@playwright/test";
import { randomUUID } from "node:crypto";
import { unlink } from "node:fs/promises";
import { resolve } from "node:path";

const fixtures: string[] = [];
function wav() {
  const b = Buffer.alloc(32044); b.write("RIFF"); b.writeUInt32LE(b.length - 8, 4); b.write("WAVEfmt ", 8); b.writeUInt32LE(16, 16);
  b.writeUInt16LE(1, 20); b.writeUInt16LE(1, 22); b.writeUInt32LE(16000, 24); b.writeUInt32LE(32000, 28);
  b.writeUInt16LE(2, 32); b.writeUInt16LE(16, 34); b.write("data", 36); b.writeUInt32LE(b.length - 44, 40); return b;
}
test.afterEach(async ({ request }) => {
  for (const id of fixtures.splice(0)) {
    expect((await request.put(`/api/stories/${id}/drafts/transcript`, { data: { text: `Closed subtitle fixture ${randomUUID()}` } })).ok()).toBe(true);
    await unlink(resolve(`../.local/browser-media/${id.replaceAll("-", "")}.wav`)).catch(() => {});
  }
});

test("Subtitles_ArtistTimesLinesAndApproves_ViewerSeesTheLineBeingSpoken", async ({ page, request }) => {
  const created = await request.post("/api/stories", { multipart: {
    title: `Subtitle fixture ${randomUUID().slice(0, 8)}`, artistName: "Mazwi browser fixture", community: "Shona, Zimbabwe", country: "ZW", type: "story", language: "sn", permission: "true",
    recording: { name: "original.wav", mimeType: "audio/wav", buffer: wav() },
  } });
  const { id } = await created.json(); fixtures.push(id);
  expect((await request.put(`/api/stories/${id}/drafts/transcript`, { data: { text: "Kare kare\n\nPane imwe nguva" } })).ok()).toBe(true);

  await page.goto(`/story/${id}/review`);
  await page.getByText(/Time Shona subtitles · Not timed/).click();
  await page.getByRole("button", { name: "Save Shona timing", exact: true }).click();
  await expect(page.getByRole("main").getByRole("alert")).toContainText("Give every line a start time");
  await page.getByLabel(/Start of line 1/).fill("0");
  await page.getByLabel(/Start of line 2/).fill("0.4");
  await page.getByLabel("End of the last line (seconds)", { exact: true }).fill("0.9");
  await page.getByRole("button", { name: "Save Shona timing", exact: true }).click();
  await expect(page.getByRole("main").getByRole("status")).toContainText("Shona subtitle timing saved");
  await expect(page.getByText(/Time Shona subtitles · Timed/)).toBeVisible();
  await page.getByText(/Time Shona subtitles · Timed/).click();
  await expect(page.getByLabel(/Start of line 2/)).toHaveValue("0.4");
  await page.locator(".subtitle-timer").first().screenshot({ path: "../.local/mazwi-subtitle-timer-desktop.png" });
  await page.getByRole("button", { name: "Approve original", exact: true }).click();
  await expect(page.getByRole("button", { name: "Original is approved", exact: true })).toBeDisabled();

  await page.goto(`/story/${id}`);
  const line = page.getByTestId("subtitle-line");
  await expect(line).toBeVisible();
  await page.locator("audio").evaluate(async (audio: HTMLAudioElement) => {
    if (audio.readyState < 1) await new Promise(done => audio.addEventListener("loadedmetadata", done, { once: true }));
    audio.currentTime = 0.5;
  });
  await expect(line).toHaveText("Pane imwe nguva");
  await page.locator("audio").evaluate((audio: HTMLAudioElement) => { audio.currentTime = 0.1; });
  await expect(line).toHaveText("Kare kare");

  const view = await (await request.get(`/api/feed/${id}?language=sn`)).json();
  const vtt = await (await request.get(view.subtitlesUrl)).text();
  expect(vtt).toBe("WEBVTT\n\n00:00:00.000 --> 00:00:00.400\nKare kare\n\n00:00:00.400 --> 00:00:00.900\nPane imwe nguva\n");
  await page.setViewportSize({ width: 390, height: 844 });
  await page.screenshot({ path: "../.local/mazwi-subtitles-mobile.png", fullPage: true });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
});
