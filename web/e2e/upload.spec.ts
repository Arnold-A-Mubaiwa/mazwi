import { test, expect, type Page } from "@playwright/test";
import { readFile, unlink } from "node:fs/promises";
import { resolve } from "node:path";
import { createHash } from "node:crypto";

function wav(bytes = 32044): Buffer {
  const data = Buffer.alloc(bytes);
  data.write("RIFF", 0); data.writeUInt32LE(bytes - 8, 4); data.write("WAVEfmt ", 8);
  data.writeUInt32LE(16, 16); data.writeUInt16LE(1, 20); data.writeUInt16LE(1, 22);
  data.writeUInt32LE(16000, 24); data.writeUInt32LE(32000, 28); data.writeUInt16LE(2, 32);
  data.writeUInt16LE(16, 34); data.write("data", 36); data.writeUInt32LE(bytes - 44, 40);
  return data;
}
async function fill(page: Page, recording = wav()) {
  await page.getByLabel("Your recording", { exact: true }).setInputFiles({ name: "story.wav", mimeType: "audio/wav", buffer: recording });
  await page.getByLabel("Story title", { exact: true }).fill("A browser test story");
  await page.getByLabel("Artist name", { exact: true }).fill("Test storyteller");
  await page.getByLabel("Community", { exact: true }).fill("Shona, Zimbabwe");
  await page.getByLabel("Country code", { exact: true }).fill("ZW");
  await page.getByLabel("Story type", { exact: true }).selectOption("folktale");
  await page.getByLabel("Original language", { exact: true }).selectOption("sn");
  await page.getByRole("checkbox").check();
}

test("Upload_RealApiAndPostgres_SavesUnpublishedStoryAndPrivateRecording", async ({ page, request }) => {
  await page.goto("/upload");
  await fill(page);
  await page.screenshot({ path: "../.local/mazwi-upload-desktop.png", fullPage: true });
  const result = page.waitForResponse(response => response.url().endsWith("/api/stories") && response.request().method() === "POST");
  await page.getByRole("button", { name: "Save my story" }).click();
  const response = await result;
  expect(response.status()).toBe(201);
  const story = await response.json();
  await expect(page.getByRole("heading", { name: "Your story has a home." })).toBeVisible();
  await expect(page.getByRole("status")).toContainText("No AI processing has run");
  const metadata = await request.get(`/api/stories/${story.id}`);
  expect((await metadata.json()).status).toBe("unpublished");
  const file = resolve(`../.local/browser-media/${story.id.replaceAll("-", "")}.wav`);
  expect(await readFile(file)).toEqual(wav());
  expect((await request.get(`/media/${story.id}.wav`)).status()).toBe(404);
  await unlink(file);
  await page.getByRole("button", { name: "Upload another story" }).click();
  await expect(page.getByLabel("Story title", { exact: true })).toHaveValue("");
});

test("Upload_FileOverTenMegabytes_ReachesBackendWithoutProxyTruncation", async ({ page }) => {
  await page.goto("/upload");
  const recording = wav(10_500_044);
  await fill(page, recording);
  const result = page.waitForResponse(response => response.url().endsWith("/api/stories") && response.request().method() === "POST");
  await page.getByRole("button", { name: "Save my story" }).click();
  const response = await result;
  expect(response.status()).toBe(201);
  const story = await response.json();
  const file = resolve(`../.local/browser-media/${story.id.replaceAll("-", "")}.wav`);
  const saved = await readFile(file);
  expect(saved.length).toBe(recording.length);
  expect(createHash("sha256").update(saved).digest("hex")).toBe(createHash("sha256").update(recording).digest("hex"));
  await unlink(file);
});

test("Upload_NoPermission_BrowserBlocksSubmission", async ({ page }) => {
  await page.goto("/upload");
  await fill(page);
  await page.getByRole("checkbox").uncheck();
  let submitted = false;
  page.on("request", request => { if (request.url().endsWith("/api/stories")) submitted = true; });
  await page.getByRole("button", { name: "Save my story" }).click();
  expect(await page.getByRole("checkbox").evaluate((element: HTMLInputElement) => element.validity.valueMissing)).toBe(true);
  expect(submitted).toBe(false);
});

test("Upload_SaveFails_PreservesFieldsAndAllowsRetry", async ({ page }) => {
  await page.route("**/api/stories", route => route.fulfill({ status: 503, body: "{}" }));
  await page.goto("/upload");
  await fill(page);
  await page.getByRole("button", { name: "Save my story" }).click();
  await expect(page.getByRole("main").getByRole("alert")).toContainText("Please try again");
  await expect(page.getByLabel("Story title", { exact: true })).toHaveValue("A browser test story");
  await expect(page.getByRole("button", { name: "Save my story" })).toBeEnabled();
});

test("Upload_MobileForm_NoHorizontalOverflow", async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto("/upload");
  await fill(page);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  await page.screenshot({ path: "../.local/mazwi-upload-mobile.png", fullPage: true });
});
