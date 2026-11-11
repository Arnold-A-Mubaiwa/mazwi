import { test, expect } from "@playwright/test";
import { randomUUID } from "node:crypto";
import { access } from "node:fs/promises";
import { resolve } from "node:path";

function wav() {
  const b = Buffer.alloc(32044); b.write("RIFF"); b.writeUInt32LE(b.length - 8, 4); b.write("WAVEfmt ", 8); b.writeUInt32LE(16, 16);
  b.writeUInt16LE(1, 20); b.writeUInt16LE(1, 22); b.writeUInt32LE(16000, 24); b.writeUInt32LE(32000, 28);
  b.writeUInt16LE(2, 32); b.writeUInt16LE(16, 34); b.write("data", 36); b.writeUInt32LE(b.length - 44, 40); return b;
}
const exists = (file: string) => access(resolve(`../.local/browser-media/${file}`)).then(() => true, () => false);

test("Removal_ArtistUnpublishesThenDeletesAStory_StoryAndRecordingsAreGone", async ({ page, request }) => {
  const title = `Removal fixture ${randomUUID().slice(0, 8)}`;
  const { id } = await (await request.post("/api/stories", { multipart: {
    title, artistName: "Mazwi browser fixture", community: "Shona, Zimbabwe", country: "ZW", type: "story", language: "sn", permission: "true",
    recording: { name: "original.wav", mimeType: "audio/wav", buffer: wav() },
  } })).json();
  let draft = await (await request.put(`/api/stories/${id}/drafts/transcript`, { data: { text: "Kare kare" } })).json();
  draft = await (await request.put(`/api/stories/${id}/drafts/translations/xh`, { data: { text: "Kudala" } })).json();
  draft = await (await request.post(`/api/stories/${id}/recordings/xh`, { multipart: { reviewVersion: draft.reviewVersion, permission: "true", recording: { name: "xh.wav", mimeType: "audio/wav", buffer: wav() } } })).json();
  await request.post(`/api/stories/${id}/approvals/sn`, { data: { reviewVersion: draft.reviewVersion } });
  const files = [`${id.replaceAll("-", "")}.wav`, `${draft.translations[0].recordingId.replaceAll("-", "")}.wav`];
  expect(await exists(files[0])).toBe(true); expect(await exists(files[1])).toBe(true);
  expect((await request.get(`/api/feed/${id}`)).ok()).toBe(true);

  await page.goto(`/story/${id}/review`);
  await page.getByRole("button", { name: "Unpublish story", exact: true }).click();
  await expect(page.getByRole("main").getByRole("status")).toContainText("Story unpublished");
  await expect(page.getByText("This story is not published. Viewers cannot see it.")).toBeVisible();
  expect((await request.get(`/api/feed/${id}`)).status()).toBe(404);

  const confirm = page.getByLabel(/Type the title/);
  const remove = page.getByRole("button", { name: "Delete story forever", exact: true });
  await expect(remove).toBeDisabled();
  await confirm.fill("not the title");
  await expect(remove).toBeDisabled();
  await confirm.fill(title.toUpperCase());
  await page.locator(".story-removal").screenshot({ path: "../.local/mazwi-story-removal.png" });
  await remove.click();
  await expect(page).toHaveURL(/\/studio$/);
  await page.getByLabel("Filter this page", { exact: true }).fill(title);
  await expect(page.locator(".studio-card").filter({ hasText: title })).toHaveCount(0);
  expect((await request.get(`/api/stories/${id}/drafts`)).status()).toBe(404);
  expect(await exists(files[0])).toBe(false); expect(await exists(files[1])).toBe(false);
});

test.describe("a new artist", () => {
  test.use({ storageState: { cookies: [], origins: [] } });

  test("Removal_ArtistDeletesTheirAccount_StoriesGoAndTheyCannotSignIn", async ({ page }) => {
    const email = `delete-me-${randomUUID().slice(0, 8)}@mazwi.test`;
    await page.goto("/signup?next=%2Faccount");
    await page.getByLabel("Email address", { exact: true }).fill(email);
    await page.getByLabel("Password", { exact: true }).fill("a long enough password");
    await page.getByRole("button", { name: "Create account", exact: true }).click();
    await expect(page).toHaveURL(/\/account$/);
    await expect(page.getByText(`Signed in as ${email}`)).toBeVisible();
    const { id } = await (await page.request.post("/api/stories", { multipart: {
      title: "Account removal fixture", artistName: "Mazwi browser fixture", community: "Shona, Zimbabwe", country: "ZW", type: "story", language: "sn", permission: "true",
      recording: { name: "original.wav", mimeType: "audio/wav", buffer: wav() },
    } })).json();
    expect(await exists(`${id.replaceAll("-", "")}.wav`)).toBe(true);

    await page.getByLabel("Current password", { exact: true }).fill("a long enough password");
    await page.getByRole("button", { name: "Delete my account and stories", exact: true }).click();
    await expect(page.getByRole("main").getByRole("alert")).toContainText("Tick the box");
    await page.getByRole("checkbox").check();
    await page.getByLabel("Current password", { exact: true }).fill("wrong password!");
    await page.getByRole("button", { name: "Delete my account and stories", exact: true }).click();
    await expect(page.getByRole("main").getByRole("alert")).toContainText("did not match");
    await page.getByLabel("Current password", { exact: true }).fill("a long enough password");
    await page.screenshot({ path: "../.local/mazwi-account-delete.png", fullPage: true });
    await page.getByRole("button", { name: "Delete my account and stories", exact: true }).click();
    await expect(page).toHaveURL(/127\.0\.0\.1:3000\/$/);
    await expect(page.getByRole("link", { name: "Sign in", exact: true })).toBeVisible();
    expect(await exists(`${id.replaceAll("-", "")}.wav`)).toBe(false);
    expect((await page.request.post("/api/account/signin", { data: { email, password: "a long enough password" } })).status()).toBe(401);
  });
});
