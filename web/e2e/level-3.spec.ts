import { test, expect, type Page } from "@playwright/test";
import { randomUUID } from "node:crypto";
import { access, readdir, readFile } from "node:fs/promises";
import { resolve } from "node:path";
import { OTHER_ARTIST } from "./artists";

// Level 3 journey against the real API, database, files and local outbox, with a brand-new artist:
// sign up → confirm email → upload and publish → a viewer reports it → a moderator hides it → the artist sees it hidden
// → the moderator restores it → the artist forgets their password and resets it → unpublishes and deletes the story
// → deletes the account. Nothing is faked.
test.use({ storageState: { cookies: [], origins: [] } });

function wav() {
  const b = Buffer.alloc(32044); b.write("RIFF"); b.writeUInt32LE(b.length - 8, 4); b.write("WAVEfmt ", 8); b.writeUInt32LE(16, 16);
  b.writeUInt16LE(1, 20); b.writeUInt16LE(1, 22); b.writeUInt32LE(16000, 24); b.writeUInt32LE(32000, 28);
  b.writeUInt16LE(2, 32); b.writeUInt16LE(16, 34); b.write("data", 36); b.writeUInt32LE(b.length - 44, 40); return b;
}
const outbox = resolve("../.local/browser-outbox");
async function latestLink(email: string, path: string): Promise<string> {
  let link = "";
  await expect.poll(async () => {
    for (const name of (await readdir(outbox).catch(() => [] as string[])).filter(file => file.endsWith(".eml")).sort().reverse()) {
      const content = await readFile(resolve(outbox, name), "utf8");
      const match = content.match(new RegExp(`http://127\\.0\\.0\\.1:3000${path}\\?token=[A-Za-z0-9_-]+`));
      if (content.includes(`To: ${email}`) && match) { link = match[0]; return true; }
    }
    return false;
  }).toBe(true);
  return link;
}
async function signInAs(page: Page, email: string, password: string) {
  await page.goto("/signin?next=%2Fstudio");
  await page.getByLabel("Email address", { exact: true }).fill(email);
  await page.getByLabel("Password", { exact: true }).fill(password);
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(page).toHaveURL(/\/studio$/);
}

test("Level3_NewArtistLifecycle_ConfirmPublishReportModerateResetAndDelete", async ({ page, browser }) => {
  const email = `level3-${randomUUID().slice(0, 8)}@mazwi.test`;
  const title = `Level 3 ${randomUUID().slice(0, 8)}`;

  // Sign up and confirm the email from the outbox.
  await page.goto("/signup?next=%2Fupload");
  await page.getByLabel("Email address", { exact: true }).fill(email);
  await page.getByLabel("Password", { exact: true }).fill("first long password");
  await page.getByRole("button", { name: "Create account", exact: true }).click();
  await expect(page).toHaveURL(/\/upload$/);
  await page.goto(await latestLink(email, "/verify-email"));
  await expect(page.getByText(/Email confirmed/)).toBeVisible();

  // Upload, write the words and publish.
  await page.goto("/upload");
  await page.getByLabel("Your recording", { exact: true }).setInputFiles({ name: "original.wav", mimeType: "audio/wav", buffer: wav() });
  await page.getByLabel("Story title", { exact: true }).fill(title);
  await page.getByLabel("Artist name", { exact: true }).fill("Mazwi browser fixture");
  await page.getByLabel("Community", { exact: true }).fill("Shona, Zimbabwe");
  await page.getByLabel("Country code", { exact: true }).fill("ZW");
  await page.getByRole("checkbox").check();
  const uploaded = page.waitForResponse(response => response.url().endsWith("/api/stories") && response.request().method() === "POST");
  await page.getByRole("button", { name: "Save my story", exact: true }).click();
  const { id } = await (await uploaded).json();
  const file = `${id.replaceAll("-", "")}.wav`;
  await page.getByRole("link", { name: /Write transcript and translations/ }).click();
  await page.getByLabel("Original transcript", { exact: true }).fill("Kare kare zvako");
  await page.getByRole("button", { name: "Save original transcript", exact: true }).click();
  await expect(page.getByRole("main").getByRole("status")).toContainText("Original transcript saved");
  await page.getByRole("button", { name: "Approve original", exact: true }).click();
  await expect(page.getByRole("button", { name: "Original is approved", exact: true })).toBeDisabled();

  // An anonymous viewer reports it; the moderator hides it.
  const viewerContext = await browser.newContext({ storageState: { cookies: [], origins: [] } });
  const viewer = await viewerContext.newPage();
  await viewer.goto(`http://127.0.0.1:3000/story/${id}`);
  await viewer.getByRole("button", { name: "Report this story", exact: true }).click();
  await viewer.getByLabel("Reason", { exact: true }).selectOption("NoPermission");
  await viewer.getByRole("button", { name: "Send report", exact: true }).click();
  await expect(viewer.getByText(/Mazwi moderators will look at this story/)).toBeVisible();
  const moderatorContext = await browser.newContext({ storageState: { cookies: [], origins: [] }, baseURL: "http://127.0.0.1:3000" });
  const moderator = await moderatorContext.newPage();
  await signInAs(moderator, OTHER_ARTIST.email, OTHER_ARTIST.password);
  await moderator.goto("/moderation");
  const card = moderator.locator(".moderation-card").filter({ hasText: title });
  await expect(card.getByTestId("report-summary")).toHaveText("1 report · Shared without permission");
  await card.getByRole("button", { name: "Hide for review", exact: true }).click();
  await expect(card).toContainText("Hidden for review");
  expect((await viewer.request.get(`/api/feed/${id}`)).status()).toBe(404);

  // The artist sees it hidden; the moderator restores it.
  await page.goto(`/story/${id}/review`);
  await expect(page.getByText(/moderators have hidden this story/)).toBeVisible();
  await card.getByRole("button", { name: "Restore to feed", exact: true }).click();
  await expect(card).toHaveCount(0);
  expect((await viewer.request.get(`/api/feed/${id}`)).ok()).toBe(true);
  await viewerContext.close(); await moderatorContext.close();

  // The artist forgets their password and resets it.
  await page.getByRole("button", { name: "Sign out", exact: true }).click();
  await page.goto("/forgot-password");
  await page.getByLabel("Email address", { exact: true }).fill(email);
  await page.getByRole("button", { name: "Send reset link", exact: true }).click();
  await expect(page.getByText(/If an account uses that email/)).toBeVisible();
  await page.goto(await latestLink(email, "/reset-password"));
  await page.getByLabel("New password", { exact: true }).fill("second long password");
  await page.getByLabel("Repeat new password", { exact: true }).fill("second long password");
  await page.getByRole("button", { name: "Change password", exact: true }).click();
  await expect(page.getByText(/Your password has changed/)).toBeVisible();
  await signInAs(page, email, "second long password");

  // Unpublish, then delete the story and its recording.
  await page.goto(`/story/${id}/review`);
  await page.getByRole("button", { name: "Unpublish story", exact: true }).click();
  await expect(page.getByRole("main").getByRole("status")).toContainText("Story unpublished");
  await page.getByLabel(/Type the title/).fill(title);
  await page.getByRole("button", { name: "Delete story forever", exact: true }).click();
  await expect(page).toHaveURL(/\/studio$/);
  expect(await access(resolve(`../.local/browser-media/${file}`)).then(() => true, () => false)).toBe(false);

  // Delete the account.
  await page.goto("/account");
  await page.getByLabel("Current password", { exact: true }).fill("second long password");
  await page.getByRole("checkbox").check();
  await page.getByRole("button", { name: "Delete my account and stories", exact: true }).click();
  await expect(page).toHaveURL(/127\.0\.0\.1:3000\/$/);
  expect((await page.request.post("/api/account/signin", { data: { email, password: "second long password" } })).status()).toBe(401);
});
