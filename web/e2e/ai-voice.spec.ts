import { test, expect } from "@playwright/test";

test("AiVoice_FeatureFlagAndFakedApi_RevokesApprovalAndLabelsFeed", async ({ page }) => {
  const id = "55555555-5555-5555-5555-555555555555";
  let voiceCalls = 0;
  const story = { id, title: "AI voice test", artistName: "Gogo Nomsa", language: "sn", status: "published", transcript: "Original", reviewVersion: id, originalApproved: true, recordingExtension: ".wav",
    translations: [{ language: "xh", text: "Molo. Unjani?", status: "approved", notes: [] as string[], recordingId: null as string | null, recordingExtension: null as string | null, recordingVoice: null as string | null }] };
  await page.route(`**/api/stories/${id}/**`, async route => {
    const request = route.request(); const path = new URL(request.url()).pathname;
    if (path.includes("/recordings/")) return route.fulfill({ status: 404 });
    if (path.endsWith("/ai-voice")) {
      voiceCalls++;
      expect(request.postDataJSON()).toEqual({ reviewVersion: story.reviewVersion });
      Object.assign(story.translations[0], { status: "draft", recordingId: id, recordingExtension: ".wav", recordingVoice: "standard" });
    } else if (path.includes("/approvals/")) story.translations[0].status = "approved";
    return route.fulfill({ json: story });
  });
  await page.goto(`/story/${id}/review`);
  await page.getByLabel("Translation language", { exact: true }).selectOption("xh");
  const button = page.getByRole("button", { name: "Generate voice", exact: true });
  if (process.env.NEXT_PUBLIC_MAZWI_AI_VOICE !== "true") {
    await expect(page.getByLabel("Translated recording", { exact: true })).toBeVisible();
    await expect(button).toHaveCount(0);
    expect(voiceCalls).toBe(0);
    return;
  }
  await expect(page.getByRole("button", { name: "Translation is approved", exact: true })).toBeDisabled();
  await button.click();
  await expect(page.getByText(/AI voice saved as this language/)).toBeVisible();
  await expect(page.getByText("AI voice translated recording")).toBeVisible();
  await expect(page.getByRole("button", { name: "Approve translation", exact: true })).toBeEnabled();
  await page.getByRole("button", { name: "Approve translation", exact: true }).click();
  await expect(page.getByRole("button", { name: "Translation is approved", exact: true })).toBeDisabled();
  expect(voiceCalls).toBe(1);

  const viewer = { id, title: story.title, artistName: story.artistName, community: "Shona", country: "ZW", type: "story", originalLanguage: "sn", textLanguage: "xh", audioLanguage: "xh",
    text: "Molo. Unjani?", recordingExtension: ".wav", originalRecordingExtension: ".wav", reviewVersion: id, availableLanguages: ["sn", "xh"], audioVoice: "standard",
    mediaUrl: `/api/feed/${id}/recording?language=xh&version=${id}&originalAudio=false` };
  await page.route("**/api/feed?*", route => route.fulfill({ json: { items: [viewer], nextOffset: null } }));
  await page.route(`**/api/feed/${id}/recording*`, route => route.fulfill({ status: 404 }));
  await page.goto("/");
  await expect(page.getByText("AI voice, approved by Gogo Nomsa")).toBeVisible();
});
