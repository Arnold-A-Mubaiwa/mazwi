import { test, expect } from "@playwright/test";
import { randomUUID } from "node:crypto";
import { unlink } from "node:fs/promises";
import { resolve } from "node:path";
import { ARTIST, OTHER_ARTIST } from "./artists";

const files: string[] = [];
const signedOut = { cookies: [], origins: [] };
test.afterEach(async () => {
  for (const id of files.splice(0)) if (/^[0-9a-f-]{36}$/i.test(id)) await unlink(resolve(`../.local/browser-media/${id.replaceAll("-", "")}.wav`)).catch(() => {});
});

test.describe("signed out", () => {
  test.use({ storageState: signedOut });

  test("Access_SignedOutArtist_IsSentToSignInAndBroughtBack", async ({ page }) => {
    await page.goto("/upload");
    await expect(page).toHaveURL(/\/signin\?next=%2Fupload$/);
    await expect(page.getByRole("heading", { level: 1 })).toContainText("still yours");
    await page.screenshot({ path: "../.local/mazwi-signin-desktop.png", fullPage: true });
    await page.setViewportSize({ width: 390, height: 844 });
    await page.screenshot({ path: "../.local/mazwi-signin-mobile.png", fullPage: true });
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
    await page.setViewportSize({ width: 1280, height: 720 });
    await page.getByLabel("Email address", { exact: true }).fill(ARTIST.email);
    await page.getByLabel("Password", { exact: true }).fill("not the right password");
    await page.getByRole("button", { name: "Sign in", exact: true }).click();
    await expect(page.getByRole("main").getByRole("alert")).toContainText("do not match an account");
    await page.getByLabel("Password", { exact: true }).fill(ARTIST.password);
    await page.getByRole("button", { name: "Sign in", exact: true }).click();
    await expect(page).toHaveURL(/\/upload$/);
    await expect(page.getByLabel("Story title", { exact: true })).toBeVisible();
    await expect(page.getByRole("button", { name: "Sign out", exact: true })).toBeVisible();
  });

  test("Access_SignedOutViewer_StillSeesFeedAndAbout", async ({ page }) => {
    await page.goto("/");
    await expect(page.getByRole("link", { name: "Sign in", exact: true })).toBeVisible();
    await page.goto("/about");
    await expect(page).toHaveURL(/\/about$/);
  });

  test("Access_SignUpWithTakenEmailOrShortPassword_ShowsHelpfulErrors", async ({ page }) => {
    await page.goto("/signup");
    await page.getByLabel("Email address", { exact: true }).fill(ARTIST.email);
    await page.getByLabel("Password", { exact: true }).fill("short");
    await page.getByRole("button", { name: "Create account", exact: true }).click();
    await expect(page.getByRole("main").getByRole("alert")).toContainText("10–128 characters");
    await page.getByLabel("Password", { exact: true }).fill("a long enough password");
    await page.getByRole("button", { name: "Create account", exact: true }).click();
    await expect(page.getByRole("main").getByRole("alert")).toContainText("already exists");
  });
});

test("Access_OtherArtist_CannotSeeOrEditAStory", async ({ page, request, browser }) => {
  const title = `Access fixture ${randomUUID().slice(0, 8)}`;
  const recording = Buffer.from("RIFF0000WAVEdata0000");
  const created = await request.post("/api/stories", { multipart: {
    title, artistName: "Mazwi browser fixture", community: "Shona, Zimbabwe", country: "ZW", type: "story", language: "sn", permission: "true",
    recording: { name: "original.wav", mimeType: "audio/wav", buffer: recording },
  } });
  expect(created.status()).toBe(201);
  const { id } = await created.json(); files.push(id);
  await page.goto("/studio");
  await expect(page.locator(".studio-card").filter({ hasText: title })).toBeVisible();

  const other = await browser.newContext({ storageState: signedOut });
  const otherPage = await other.newPage();
  await otherPage.goto("/signin?next=%2Fstudio");
  await otherPage.getByLabel("Email address", { exact: true }).fill(OTHER_ARTIST.email);
  await otherPage.getByLabel("Password", { exact: true }).fill(OTHER_ARTIST.password);
  await otherPage.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(otherPage).toHaveURL(/\/studio$/);
  await expect(otherPage.getByRole("heading", { level: 1 })).toBeVisible();
  await expect(otherPage.locator(".studio-card").filter({ hasText: title })).toHaveCount(0);
  await otherPage.goto(`/story/${id}/review`);
  await expect(otherPage.getByRole("main").getByRole("alert")).toContainText("belongs to another artist");
  expect((await otherPage.request.put(`/api/stories/${id}/drafts/transcript`, { data: { text: "Not mine" } })).status()).toBe(403);
  await other.close();
});
