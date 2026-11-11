import { test, expect, type Page } from "@playwright/test";
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
    expect((await request.put(`/api/stories/${id}/drafts/transcript`, { data: { text: `Closed share fixture ${randomUUID()}` } })).ok()).toBe(true);
    await unlink(resolve(`../.local/browser-media/${id.replaceAll("-", "")}.wav`)).catch(() => {});
  }
});
async function controlled(page: Page) {
  await page.goto("/about");
  await page.waitForFunction(async () => (await navigator.serviceWorker.ready) && navigator.serviceWorker.controller !== null, undefined, { timeout: 15000 })
    .catch(async () => { await page.reload(); await page.waitForFunction(() => navigator.serviceWorker.controller !== null); });
}

test("Pwa_ManifestIconsAndWorker_AreInstallable", async ({ page, request }) => {
  const manifest = await (await request.get("/manifest.webmanifest")).json();
  expect(manifest.share_target.action).toBe("/share-target");
  for (const icon of manifest.icons) {
    const response = await request.get(icon.src);
    expect(response.headers()["content-type"]).toBe("image/png");
  }
  const worker = await request.get("/sw.js");
  expect(worker.headers()["cache-control"]).toContain("no-store");
  await page.goto("/");
  await expect(page.locator('link[rel="manifest"]')).toHaveAttribute("href", "/manifest.webmanifest");
  await controlled(page);
  await page.goto("/");
  const cached = await page.evaluate(async () => {
    const urls: string[] = [];
    for (const key of await caches.keys()) for (const entry of await (await caches.open(key)).keys()) urls.push(new URL(entry.url).pathname);
    return urls;
  });
  expect(cached).toContain("/offline.html");
  expect(cached.some(path => path.startsWith("/api/"))).toBe(false);
});

test("Pwa_RecordingSharedFromAnotherApp_OpensInUpload", async ({ page }) => {
  await controlled(page);
  const redirected = await page.evaluate(async bytes => {
    const form = new FormData();
    form.append("title", "From my phone");
    form.append("recording", new File([new Uint8Array(bytes)], "shared story.wav", { type: "audio/wav" }));
    const response = await fetch("/share-target", { method: "POST", body: form });
    return response.url;
  }, [...wav()]);
  expect(redirected).toMatch(/\/upload\?shared=1$/);
  await page.goto("/upload?shared=1");
  await expect(page.getByText("Shared recording ready: shared story.wav", { exact: true })).toBeVisible();
  expect(await page.getByLabel("Your recording", { exact: true }).evaluate((input: HTMLInputElement) => input.files?.[0]?.name)).toBe("shared story.wav");
  await page.reload();
  await expect(page.getByText(/Shared recording ready/)).toHaveCount(0);
});

test("Pwa_ShareButton_UsesShareSheetOrCopiesTheLink", async ({ page, request, browser }) => {
  const created = await request.post("/api/stories", { multipart: {
    title: `Share fixture ${randomUUID().slice(0, 8)}`, artistName: "Mazwi browser fixture", community: "Shona, Zimbabwe", country: "ZW", type: "story", language: "sn", permission: "true",
    recording: { name: "original.wav", mimeType: "audio/wav", buffer: wav() },
  } });
  const { id } = await created.json(); fixtures.push(id);
  const draft = await (await request.put(`/api/stories/${id}/drafts/transcript`, { data: { text: "Kare kare" } })).json();
  await request.post(`/api/stories/${id}/approvals/sn`, { data: { reviewVersion: draft.reviewVersion } });

  await page.addInitScript(() => {
    Object.defineProperty(navigator, "share", { configurable: true, value: async (data: ShareData) => { (window as unknown as { shared: ShareData }).shared = data; } });
  });
  await page.goto(`/story/${id}`);
  await page.getByRole("button", { name: "Share this story", exact: true }).click();
  await expect(page.getByText("Thanks for sharing this story.", { exact: true })).toBeVisible();
  const shared = await page.evaluate(() => (window as unknown as { shared: ShareData }).shared);
  expect(shared.url).toBe(`http://127.0.0.1:3000/story/${id}`);

  const desktop = await browser.newContext({ storageState: { cookies: [], origins: [] } });
  const viewer = await desktop.newPage();
  await viewer.addInitScript(() => {
    Object.defineProperty(navigator, "share", { configurable: true, value: undefined });
    Object.defineProperty(navigator, "clipboard", { configurable: true, value: { writeText: async (text: string) => { (window as unknown as { copied: string }).copied = text; } } });
  });
  await viewer.goto(`http://127.0.0.1:3000/story/${id}`);
  await viewer.getByRole("button", { name: "Share this story", exact: true }).click();
  await expect(viewer.getByText("Link copied. Paste it anywhere to share this story.", { exact: true })).toBeVisible();
  expect(await viewer.evaluate(() => (window as unknown as { copied: string }).copied)).toBe(`http://127.0.0.1:3000/story/${id}`);
  await desktop.close();
});
