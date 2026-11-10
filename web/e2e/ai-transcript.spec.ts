import { test, expect } from "@playwright/test";

test("AiTranscript_FeatureFlagAndFakedApi_ConfirmsReplaceAndRevokesApproval", async ({ page }) => {
  const id = "66666666-6666-6666-6666-666666666666";
  let transcriptCalls = 0;
  const story = { id, title: "AI transcript test", artistName: "Artist", language: "sn", status: "published", transcript: "Human words", reviewVersion: id, originalApproved: true, recordingExtension: ".wav",
    translations: [{ language: "xh", text: "Molo", status: "approved", notes: [] as string[] }] };
  await page.route(`**/api/stories/${id}/**`, async route => {
    const request = route.request(); const path = new URL(request.url()).pathname;
    if (path.includes("/recordings/")) return route.fulfill({ status: 404 });
    if (path.endsWith("/transcript/ai-draft")) {
      transcriptCalls++;
      expect(request.postDataJSON()).toEqual({ reviewVersion: story.reviewVersion });
      Object.assign(story, { transcript: "Kare kare zvako", originalApproved: false, status: "unpublished" });
      story.translations[0].status = "draft";
    }
    return route.fulfill({ json: story });
  });
  await page.goto(`/story/${id}/review`);
  const button = page.getByRole("button", { name: "AI first draft", exact: true });
  if (process.env.NEXT_PUBLIC_MAZWI_AI_TRANSCRIPT === "false") {
    await expect(page.getByLabel("Original transcript", { exact: true })).toHaveValue("Human words");
    await expect(button).toHaveCount(0);
    expect(transcriptCalls).toBe(0);
    return;
  }
  await expect(page.getByText("Check and correct this before approving.")).toBeVisible();

  page.once("dialog", dialog => { expect(dialog.message()).toContain("Replace your saved transcript"); void dialog.dismiss(); });
  await button.click();
  await expect(page.getByLabel("Original transcript", { exact: true })).toHaveValue("Human words");
  expect(transcriptCalls).toBe(0);

  page.once("dialog", dialog => void dialog.accept());
  await button.click();
  await expect(page.getByLabel("Original transcript", { exact: true })).toHaveValue("Kare kare zvako");
  await expect(page.getByText(/AI transcript saved as a draft/)).toBeVisible();
  await expect(page.getByRole("button", { name: "Approve original", exact: true })).toBeEnabled();
  expect(transcriptCalls).toBe(1);
});
