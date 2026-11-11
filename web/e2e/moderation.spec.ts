import { test, expect } from "@playwright/test";
import { randomUUID } from "node:crypto";
import { unlink } from "node:fs/promises";
import { resolve } from "node:path";
import { OTHER_ARTIST } from "./artists";

const fixtures: string[] = [];
function wav() {
  const b = Buffer.alloc(32044); b.write("RIFF"); b.writeUInt32LE(b.length - 8, 4); b.write("WAVEfmt ", 8); b.writeUInt32LE(16, 16);
  b.writeUInt16LE(1, 20); b.writeUInt16LE(1, 22); b.writeUInt32LE(16000, 24); b.writeUInt32LE(32000, 28);
  b.writeUInt16LE(2, 32); b.writeUInt16LE(16, 34); b.write("data", 36); b.writeUInt32LE(b.length - 44, 40); return b;
}
test.afterEach(async ({ request }) => {
  for (const id of fixtures.splice(0)) {
    await request.put(`/api/stories/${id}/drafts/transcript`, { data: { text: `Closed moderation fixture ${randomUUID()}` } });
    await unlink(resolve(`../.local/browser-media/${id.replaceAll("-", "")}.wav`)).catch(() => {});
  }
});

test("Moderation_ViewerReportsModeratorHidesThenRestores_ArtistSeesTheState", async ({ page, request, browser }) => {
  const title = `Moderation fixture ${randomUUID().slice(0, 8)}`;
  const { id } = await (await request.post("/api/stories", { multipart: {
    title, artistName: "Mazwi browser fixture", community: "Shona, Zimbabwe", country: "ZW", type: "story", language: "sn", permission: "true",
    recording: { name: "original.wav", mimeType: "audio/wav", buffer: wav() },
  } })).json();
  fixtures.push(id);
  const draft = await (await request.put(`/api/stories/${id}/drafts/transcript`, { data: { text: "Kare kare" } })).json();
  await request.post(`/api/stories/${id}/approvals/sn`, { data: { reviewVersion: draft.reviewVersion } });

  // An anonymous viewer reports the story.
  const viewerContext = await browser.newContext({ storageState: { cookies: [], origins: [] } });
  const viewer = await viewerContext.newPage();
  await viewer.goto(`http://127.0.0.1:3000/story/${id}`);
  await viewer.getByRole("button", { name: "Report this story", exact: true }).click();
  await viewer.getByLabel("Reason", { exact: true }).selectOption("Harmful");
  await viewer.getByLabel(/Anything moderators should know/).fill("Contains hateful words.");
  await viewer.getByRole("button", { name: "Send report", exact: true }).click();
  await expect(viewer.getByText("Thank you. Mazwi moderators will look at this story.")).toBeVisible();
  await viewerContext.close();

  // The fixture artist is not a moderator.
  expect((await request.get("/api/moderation/reports")).status()).toBe(403);

  // The moderator hides it.
  const moderatorContext = await browser.newContext({ storageState: { cookies: [], origins: [] }, baseURL: "http://127.0.0.1:3000" });
  const moderator = await moderatorContext.newPage();
  expect((await moderator.request.post("/api/account/signin", { data: { email: OTHER_ARTIST.email, password: OTHER_ARTIST.password } })).ok()).toBe(true);
  await moderator.goto("/moderation");
  await expect(moderator.getByRole("link", { name: "Moderation", exact: true })).toBeVisible();
  const card = moderator.locator(".moderation-card").filter({ hasText: title });
  await expect(card.getByTestId("report-summary")).toHaveText("1 report · Hateful, violent or harmful");
  await expect(card).toContainText("Contains hateful words.");
  await card.getByRole("button", { name: "Hide for review", exact: true }).click();
  await expect(moderator.getByRole("main").getByRole("status")).toContainText("Hidden from the feed for review");
  await expect(card).toContainText("Hidden for review");
  await moderator.screenshot({ path: "../.local/mazwi-moderation.png", fullPage: true });
  expect((await request.get(`/api/feed/${id}`)).status()).toBe(404);
  expect((await request.post(`/api/feed/${id}/reports`, { data: { reason: "Other" } })).status()).toBe(404);

  // The artist sees it is hidden.
  await page.goto("/studio");
  await page.getByLabel("Filter this page", { exact: true }).fill(title);
  await expect(page.locator(".studio-card").filter({ hasText: title })).toContainText("Hidden for review");
  await page.goto(`/story/${id}/review`);
  await expect(page.getByText(/moderators have hidden this story/)).toBeVisible();

  // The moderator restores it; the reports close.
  await card.getByRole("button", { name: "Restore to feed", exact: true }).click();
  await expect(moderator.getByRole("main").getByRole("status")).toContainText("Back in the feed");
  await expect(card).toHaveCount(0);
  expect((await request.get(`/api/feed/${id}`)).ok()).toBe(true);
  await moderatorContext.close();
});
