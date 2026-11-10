import { test, expect } from "@playwright/test";
import { randomUUID } from "node:crypto";
import { unlink } from "node:fs/promises";
import { resolve } from "node:path";

const fixtures: string[] = [];
const files: string[] = [];
function wav() {
  const b = Buffer.alloc(32044); b.write("RIFF"); b.writeUInt32LE(b.length - 8, 4); b.write("WAVEfmt ", 8); b.writeUInt32LE(16, 16);
  b.writeUInt16LE(1, 20); b.writeUInt16LE(1, 22); b.writeUInt32LE(16000, 24); b.writeUInt32LE(32000, 28);
  b.writeUInt16LE(2, 32); b.writeUInt16LE(16, 34); b.write("data", 36); b.writeUInt32LE(b.length - 44, 40); return b;
}
test.afterEach(async ({ request }) => {
  for (const id of fixtures.splice(0)) expect((await request.put(`/api/stories/${id}/drafts/transcript`, { data: { text: `Closed complete fixture ${randomUUID()}` } })).ok()).toBe(true);
  for (const id of files.splice(0)) if (/^[0-9a-f-]{36}$/i.test(id)) await unlink(resolve(`../.local/browser-media/${id.replaceAll("-", "")}.wav`)).catch(() => {});
});

test("ManualDemo_UploadStudioEditApproveWatchAndRevoke_WorksWithoutAI", async ({ page, request }) => {
  const title = `Manual demo ${randomUUID().slice(0, 8)}`;
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
  await page.getByRole("link", { name: /See all my stories/ }).click();
  await page.getByLabel("Filter this page", { exact: true }).fill(title);
  const card = page.locator(".studio-card").filter({ hasText: title });
  await expect(card).toContainText("Unpublished");
  await card.getByRole("link", { name: /Edit and approve/ }).click();
  await page.getByLabel("Original transcript", { exact: true }).fill("Original story words\nSecond line.");
  await page.getByRole("button", { name: "Save original transcript", exact: true }).click();
  await expect(page.getByRole("main").getByRole("status")).toContainText("Original transcript saved");
  await page.getByLabel("Translation text", { exact: true }).fill("Manually translated words\nSecond line.");
  await page.getByRole("button", { name: "Save translation draft", exact: true }).click();
  await expect(page.getByRole("main").getByRole("status")).toContainText("translation saved as a draft");
  await page.getByLabel("Translated recording", { exact: true }).setInputFiles({ name: "translated.wav", mimeType: "audio/wav", buffer: wav() });
  await page.getByRole("checkbox").check();
  await page.getByRole("button", { name: "Save translated recording", exact: true }).click();
  await expect(page.getByRole("main").getByRole("status")).toContainText("Translated recording saved as a draft");
  files.push((await (await request.get(`/api/stories/${id}/drafts`)).json()).translations[0].recordingId);
  await page.getByRole("button", { name: "Approve original", exact: true }).click();
  await expect(page.getByRole("button", { name: "Original is approved", exact: true })).toBeDisabled();
  await page.getByRole("button", { name: "Approve translation", exact: true }).click();
  await expect(page.getByRole("button", { name: "Translation is approved", exact: true })).toBeDisabled();
  await page.getByRole("link", { name: /Back to your studio/ }).click();
  await page.getByLabel("Filter this page", { exact: true }).fill(title);
  await expect(card).toContainText("Original approved");
  await page.screenshot({ path: "../.local/mazwi-studio-desktop.png", fullPage: true });
  await card.getByRole("link", { name: /Watch story/ }).click();
  await page.getByRole("combobox", { name: "Your language", exact: true }).selectOption("xh");
  await expect(page.locator(".viewer-text")).toContainText("Manually translated words");
  await expect(page.getByText("Artist-provided isiXhosa recording", { exact: true })).toBeVisible();
  const duration = await page.locator("audio").evaluate(async (audio: HTMLAudioElement) => { audio.muted = true; await audio.play(); audio.pause(); return audio.duration; });
  expect(duration).toBeGreaterThan(0.5);
  const view = await (await request.get(`/api/feed/${id}?language=xh`)).json();
  await page.getByRole("link", { name: "Studio", exact: true }).click();
  await page.getByLabel("Filter this page", { exact: true }).fill(title);
  await card.getByRole("link", { name: /Edit and approve/ }).click();
  await page.getByLabel("Original transcript", { exact: true }).fill("Changed words need approval again.");
  await page.getByRole("button", { name: "Save original transcript", exact: true }).click();
  await expect(page.getByRole("button", { name: "Approve original", exact: true })).toBeEnabled();
  expect((await request.get(view.mediaUrl)).status()).toBe(404);
  await page.getByRole("link", { name: /Back to your studio/ }).click();
  await page.getByLabel("Filter this page", { exact: true }).fill(title);
  await expect(card).toContainText("Unpublished"); await expect(card.getByRole("link", { name: /Watch story/ })).toHaveCount(0);
  await page.setViewportSize({ width: 390, height: 844 });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  await page.screenshot({ path: "../.local/mazwi-studio-mobile.png", fullPage: true });
});

test("Studio_EmptyLocalLibrary_ShowsUploadAction", async ({ page }) => {
  await page.route("**/api/studio?*", route => route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ items: [], nextOffset: null }) }));
  await page.goto("/studio");
  await expect(page.getByRole("heading", { name: "Your first story starts here." })).toBeVisible();
  await expect(page.getByRole("button", { name: "Next page", exact: true })).toBeDisabled();
});

test("Studio_FailedLoad_RetriesWithoutLosingAccessToUpload", async ({ page }) => {
  await page.route("**/api/studio?*", route => route.fulfill({ status: 503, body: "{}" }));
  await page.goto("/studio");
  await expect(page.getByRole("main").getByRole("alert")).toContainText("Please try again");
  await page.unroute("**/api/studio?*");
  await page.getByRole("button", { name: "Try studio again", exact: true }).click();
  await expect(page.getByLabel("Filter this page", { exact: true })).toBeVisible();
});
