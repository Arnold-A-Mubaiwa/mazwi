import { test, expect, type APIRequestContext } from "@playwright/test";
import { randomUUID } from "node:crypto";
import { unlink } from "node:fs/promises";
import { resolve } from "node:path";

const uploaded: string[] = [];
async function createStory(request: APIRequestContext): Promise<string> {
  const response = await request.post("/api/stories", { multipart: {
    title: "A manual story draft", artistName: "Test storyteller", community: "Shona", country: "ZW", type: "folktale", language: "sn", permission: "true",
    recording: { name: "draft.wav", mimeType: "audio/wav", buffer: Buffer.from("RIFF0000WAVEdata0000") },
  } });
  expect(response.status()).toBe(201);
  const { id } = await response.json();
  uploaded.push(id);
  return id;
}
test.afterEach(async () => {
  for (const id of uploaded.splice(0)) {
    if (/^[0-9a-f-]{36}$/i.test(id)) await unlink(resolve(`../.local/browser-media/${id.replaceAll("-", "")}.wav`)).catch(() => {});
  }
});

test("Drafts_RealApi_SaveAndReopenOriginalAndTwoLanguages", async ({ page, request }) => {
  const id = await createStory(request);
  await page.goto(`/story/${id}/review`);
  await expect(page.getByLabel("Translation language", { exact: true })).toBeDisabled();
  await page.getByLabel("Original transcript", { exact: true }).fill("Tsuro na Gudo\n\nSekuru Rudo.");
  await page.getByRole("button", { name: "Save original transcript", exact: true }).click();
  await expect(page.getByRole("main").getByRole("status")).toContainText("Original transcript saved");
  await expect(page.getByLabel("Translation language", { exact: true }).getByRole("option", { name: "Shona", exact: true })).toHaveCount(0);
  await page.getByLabel("Translation language", { exact: true }).selectOption("xh");
  await page.getByLabel("Translation text", { exact: true }).fill("Manual isiXhosa draft\nSecond line.");
  await page.getByRole("button", { name: "Save translation draft", exact: true }).click();
  await expect(page.getByRole("main").getByRole("status")).toContainText("isiXhosa translation saved as a draft");
  await page.getByLabel("Translation language", { exact: true }).selectOption("en");
  await page.getByLabel("Translation text", { exact: true }).fill("Manual English draft");
  await page.getByRole("button", { name: "Save translation draft", exact: true }).click();
  await expect(page.getByRole("main").getByRole("status")).toContainText("English translation saved as a draft");
  await page.reload();
  await expect(page.getByLabel("Original transcript", { exact: true })).toHaveValue("Tsuro na Gudo\n\nSekuru Rudo.");
  await expect(page.getByLabel("Translation text", { exact: true })).toHaveValue("Manual isiXhosa draft\nSecond line.");
  await page.getByLabel("Translation language", { exact: true }).selectOption("en");
  await expect(page.getByLabel("Translation text", { exact: true })).toHaveValue("Manual English draft");
  await page.getByLabel("Original transcript", { exact: true }).fill("Revised original.");
  await expect(page.getByRole("button", { name: "Save translation draft", exact: true })).toBeDisabled();
  await page.getByRole("button", { name: "Save original transcript", exact: true }).click();
  await expect(page.getByRole("main").getByRole("status")).toContainText("Original transcript saved");
  await page.reload();
  const saved = await (await request.get(`/api/stories/${id}/drafts`)).json();
  expect(saved.transcript).toBe("Revised original.");
  expect(saved.translations).toHaveLength(2);
  expect(saved.status).toBe("unpublished");
  expect(saved.translations.every((translation: { status: string }) => translation.status === "draft")).toBe(true);
  await expect(page.getByLabel("Translation text", { exact: true })).toHaveValue("Manual isiXhosa draft\nSecond line.");
  await page.screenshot({ path: "../.local/mazwi-draft-editor-desktop.png", fullPage: true });
});

test("Drafts_FailedSave_KeepsTextAndCanRetrySuccessfully", async ({ page, request }) => {
  const id = await createStory(request);
  await page.route(`**/api/stories/${id}/drafts/transcript`, route => route.fulfill({ status: 503, body: "{}" }));
  await page.goto(`/story/${id}/review`);
  await page.getByLabel("Original transcript", { exact: true }).fill("Do not lose these words.");
  await page.getByRole("button", { name: "Save original transcript", exact: true }).click();
  await expect(page.getByRole("main").getByRole("alert")).toContainText("Any text you entered is still here");
  await expect(page.getByLabel("Original transcript", { exact: true })).toHaveValue("Do not lose these words.");
  await page.unroute(`**/api/stories/${id}/drafts/transcript`);
  await page.getByRole("button", { name: "Save original transcript", exact: true }).click();
  await expect(page.getByRole("main").getByRole("status")).toContainText("Original transcript saved");
});

test("Drafts_FailedLoad_RetryLoadsSavedStory", async ({ page, request }) => {
  const id = await createStory(request);
  const route = `**/api/stories/${id}/drafts`;
  await page.route(route, intercept => intercept.fulfill({ status: 503, body: "{}" }));
  await page.goto(`/story/${id}/review`);
  await expect(page.getByRole("main").getByRole("alert")).toContainText("Please try again");
  await page.unroute(route);
  await page.getByRole("button", { name: "Try loading again" }).click();
  await expect(page.getByLabel("Original transcript", { exact: true })).toBeVisible();
});

test("Drafts_MissingStory_ShowsUsefulNotFoundMessage", async ({ page }) => {
  await page.goto(`/story/${randomUUID()}/review`);
  await expect(page.getByRole("main").getByRole("alert")).toContainText("could not be found");
  await expect(page.getByRole("main").getByRole("link", { name: "Upload a story", exact: true })).toHaveAttribute("href", "/upload");
});

test("Drafts_MobileEditor_NoHorizontalOverflow", async ({ page, request }) => {
  const id = await createStory(request);
  await request.put(`/api/stories/${id}/drafts/transcript`, { data: { text: "Original words\nSecond line." } });
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto(`/story/${id}/review`);
  await expect(page.getByLabel("Original transcript", { exact: true })).toHaveValue("Original words\nSecond line.");
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  await page.screenshot({ path: "../.local/mazwi-draft-editor-mobile.png", fullPage: true });
});
