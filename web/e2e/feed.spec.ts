import { test, expect, type APIRequestContext } from "@playwright/test";
import { randomUUID } from "node:crypto";
import { unlink } from "node:fs/promises";
import { resolve } from "node:path";

const fixtures: string[] = [];
const files: string[] = [];
function wav() {
  const b = Buffer.alloc(32044); b.write("RIFF"); b.writeUInt32LE(b.length - 8, 4); b.write("WAVEfmt ", 8);
  b.writeUInt32LE(16, 16); b.writeUInt16LE(1, 20); b.writeUInt16LE(1, 22); b.writeUInt32LE(16000, 24); b.writeUInt32LE(32000, 28);
  b.writeUInt16LE(2, 32); b.writeUInt16LE(16, 34); b.write("data", 36); b.writeUInt32LE(b.length - 44, 40); return b;
}
async function create(request: APIRequestContext, title: string, approved = true, translated = false) {
  const response = await request.post("/api/stories", { multipart: { title, artistName: "Mazwi browser fixture", community: "Shona, Zimbabwe", country: "ZW", type: "story", language: "sn", permission: "true", recording: { name: "story.wav", mimeType: "audio/wav", buffer: wav() } } });
  expect(response.status()).toBe(201); const { id } = await response.json(); fixtures.push(id); files.push(id);
  await request.put(`/api/stories/${id}/drafts/transcript`, { data: { text: `Original ${title}` } });
  await request.put(`/api/stories/${id}/drafts/translations/xh`, { data: { text: `Translated ${title}` } });
  if (translated) {
    const before = await (await request.get(`/api/stories/${id}/drafts`)).json();
    expect((await request.post(`/api/stories/${id}/recordings/xh`, { multipart: { reviewVersion: before.reviewVersion, permission: "true", recording: { name: "voice.wav", mimeType: "audio/wav", buffer: wav() } } })).ok()).toBe(true);
    const attached = await (await request.get(`/api/stories/${id}/drafts`)).json(); files.push(attached.translations[0].recordingId);
  }
  if (approved) {
    const draft = await (await request.get(`/api/stories/${id}/drafts`)).json();
    expect((await request.post(`/api/stories/${id}/approvals/sn`, { data: { reviewVersion: draft.reviewVersion } })).ok()).toBe(true);
    expect((await request.post(`/api/stories/${id}/approvals/xh`, { data: { reviewVersion: draft.reviewVersion } })).ok()).toBe(true);
  }
  return id;
}
test.afterEach(async ({ request }) => {
  for (const id of fixtures.splice(0)) expect((await request.put(`/api/stories/${id}/drafts/transcript`, { data: { text: `Closed viewer fixture ${randomUUID()}` } })).ok()).toBe(true);
  for (const id of files.splice(0)) if (/^[0-9a-f-]{36}$/i.test(id)) await unlink(resolve(`../.local/browser-media/${id.replaceAll("-", "")}.wav`)).catch(() => {});
});

test("Feed_ApprovedVersionsLanguageNavigationAndRevocation_WorkEndToEnd", async ({ page, request }) => {
  const first = await create(request, "First approved story");
  const second = await create(request, "Second approved story", true, true);
  const hidden = await create(request, "Hidden draft story", false);
  expect((await request.get(`/api/feed/${hidden}`)).status()).toBe(404);
  await page.goto("/");
  await page.getByRole("combobox", { name: "Your language", exact: true }).selectOption("xh");
  await expect(page.locator(".viewer-card").getByRole("heading", { name: "Second approved story" })).toBeVisible();
  await expect(page.getByText("Artist-provided isiXhosa recording", { exact: true })).toBeVisible();
  await page.getByRole("button", { name: "Hear original", exact: true }).click();
  await expect(page.getByText("Original Shona audio", { exact: true })).toBeVisible();
  await page.getByRole("button", { name: "Hear translated recording", exact: true }).click();
  await page.getByRole("article", { name: "Story player" }).focus();
  await page.keyboard.press("ArrowDown");
  await expect(page.locator(".viewer-card").getByRole("heading", { name: "First approved story" })).toBeVisible();
  await expect(page.getByText(/Approved isiXhosa text with original Shona audio/)).toBeVisible();
  await expect(page.getByText(/Approved by Mazwi browser fixture. Not checked by an isiXhosa speaker./)).toBeVisible();
  await expect(page.locator(".viewer-text")).toContainText("Translated First approved story");
  await page.getByRole("button", { name: "Previous story", exact: true }).click();
  await expect(page.locator(".viewer-card").getByRole("heading", { name: "Second approved story" })).toBeVisible();
  const view = await (await request.get(`/api/feed/${second}?language=xh`)).json();
  const media = await request.get(view.mediaUrl, { headers: { Range: "bytes=0-3" } });
  expect(media.status()).toBe(206); expect(await media.body()).toEqual(Buffer.from("RIFF"));
  await request.put(`/api/stories/${second}/drafts/transcript`, { data: { text: "Changed source awaiting approval" } });
  expect((await request.get(view.mediaUrl)).status()).toBe(404);
  await page.getByRole("button", { name: "Refresh stories", exact: true }).click();
  await expect(page.locator(".viewer-card").getByRole("heading", { name: "First approved story" })).toBeVisible();
  await page.goto(`/story/${first}`);
  await expect(page.locator(".viewer-text")).toContainText("Translated First approved story");
  await expect(page.getByRole("combobox", { name: "Your language", exact: true })).toHaveValue("xh");
  await page.getByRole("combobox", { name: "Your language", exact: true }).selectOption("en");
  await expect(page.getByText(/An approved English version isn’t available/)).toBeVisible();
  await expect(page.locator(".viewer-text")).toContainText("Original First approved story");
  await page.screenshot({ path: "../.local/mazwi-story-viewer-desktop.png", fullPage: true });
});

test("Feed_MobileStory_HasNoOverflow", async ({ page, request }) => {
  const id = await create(request, "A mobile story");
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto(`/story/${id}`);
  await expect(page.locator(".viewer-card")).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  await page.screenshot({ path: "../.local/mazwi-story-viewer-mobile.png", fullPage: true });
});

test("Feed_FailedRequest_ShowsRetryAndRecovers", async ({ page }) => {
  await page.route("**/api/feed?*", route => route.fulfill({ status: 503, body: "{}" }));
  await page.goto("/");
  await expect(page.getByRole("main").getByRole("alert")).toContainText("Please try again");
  await page.unroute("**/api/feed?*");
  await page.getByRole("button", { name: "Try stories again", exact: true }).click();
  await expect(page.getByRole("heading", { name: /first voices/ })).toBeVisible();
});
