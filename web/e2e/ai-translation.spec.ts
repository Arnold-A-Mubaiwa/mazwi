import { test, expect } from "@playwright/test";

test("AiDraft_FeatureFlagAndFakedApi_ProtectReviewFlow", async ({ page }) => {
  const id = "44444444-4444-4444-4444-444444444444";
  let aiCalls = 0;
  const story = { id, title: "AI draft test", artistName: "Artist", language: "sn", status: "published", transcript: "Original", translations: [] as { language: string; text: string; status: string; notes: string[] }[], reviewVersion: id, originalApproved: false, recordingExtension: ".wav" };
  await page.route(`**/api/stories/${id}/**`, async route => {
    const request = route.request(); const path = new URL(request.url()).pathname;
    if (path.includes("/recordings/")) return route.fulfill({ status: 404 });
    if (path.endsWith("/ai-draft")) {
      aiCalls++;
      expect(request.postDataJSON()).toEqual({ reviewVersion: story.reviewVersion });
      story.translations = [{ language: "xh", text: "Molo", status: "draft", notes: ["Keep name", "Proverb meaning"] }];
    } else if (request.method() === "PUT") {
      const input = request.postDataJSON();
      story.translations = [{ language: "xh", text: input.text, notes: input.notes, status: "draft" }];
    } else if (path.includes("/approvals/")) {
      if (path.endsWith("/sn")) story.originalApproved = true;
      else story.translations[0].status = "approved";
    }
    return route.fulfill({ json: story });
  });
  await page.goto(`/story/${id}/review`);
  const button = page.getByRole("button", { name: "AI draft", exact: true });
  if (process.env.NEXT_PUBLIC_MAZWI_AI_TRANSLATION === "false") {
    await expect(page.getByLabel("Translation text", { exact: true })).toBeVisible();
    await expect(button).toHaveCount(0);
    expect(aiCalls).toBe(0);
    return;
  }
  await expect(button).toBeVisible(); await expect(button).toBeDisabled();
  await page.getByRole("button", { name: "Approve original", exact: true }).click();
  await button.click();
  await expect(page.getByLabel("Translation text", { exact: true })).toHaveValue("Molo");
  await expect(page.getByRole("button", { name: "Approve translation", exact: true })).toBeEnabled();
  expect(story.translations[0].status).toBe("draft");
  await page.getByLabel("Translation note 1", { exact: true }).fill("Corrected note");
  await page.getByRole("button", { name: "Dismiss note 2", exact: true }).click();
  await expect(button).toBeDisabled();
  await page.getByRole("button", { name: "Save translation draft", exact: true }).click();
  expect(story.translations[0].notes).toEqual(["Corrected note"]);
  await page.getByRole("button", { name: "Approve translation", exact: true }).click();
  await expect(page.getByRole("button", { name: "Translation is approved", exact: true })).toBeDisabled();
  await page.reload(); await expect(page.getByLabel("Translation note 1", { exact: true })).toHaveValue("Corrected note");
  expect(aiCalls).toBe(1);
  await page.screenshot({ path: "../.local/mazwi-ai-draft.png", fullPage: true });
});
