import { test, expect } from "@playwright/test";

test("SpeakerCheck_ApproveWithoutAndWithTick_SendsFlagAndShowsStatus", async ({ page }) => {
  const id = "77777777-7777-7777-7777-777777777777";
  const approvals: unknown[] = [];
  const story = { id, title: "Speaker check test", artistName: "Gogo", language: "sn", status: "published", transcript: "Kare kare", reviewVersion: id, originalApproved: true, recordingExtension: ".wav",
    translations: [{ language: "xh", text: "Kudala", status: "draft", notes: [] as string[], speakerChecked: false }] };
  await page.route(`**/api/stories/${id}/**`, async route => {
    const request = route.request(); const path = new URL(request.url()).pathname;
    if (path.includes("/recordings/")) return route.fulfill({ status: 404 });
    if (path.endsWith("/approvals/xh")) {
      const body = request.postDataJSON(); approvals.push(body);
      Object.assign(story.translations[0], { status: "approved", speakerChecked: body.speakerChecked });
    }
    return route.fulfill({ json: story });
  });
  await page.goto(`/story/${id}/review`);
  await page.getByLabel("Translation language", { exact: true }).selectOption("xh");
  const box = page.getByLabel("I speak isiXhosa and checked this translation");
  await expect(box).not.toBeChecked();
  await expect(page.getByText(/You can approve without speaking isiXhosa/)).toBeVisible();
  await page.getByRole("button", { name: "Approve translation", exact: true }).click();
  await expect(page.getByText("Not checked by a speaker of isiXhosa. Viewers will see this.")).toBeVisible();
  await expect(page.getByRole("button", { name: "Translation is approved", exact: true })).toBeDisabled();
  await expect(box).toHaveCount(0);

  Object.assign(story.translations[0], { status: "draft", speakerChecked: false });
  await page.reload();
  await page.getByLabel("Translation language", { exact: true }).selectOption("xh");
  await page.getByLabel("I speak isiXhosa and checked this translation").check();
  await page.getByRole("button", { name: "Approve translation", exact: true }).click();
  await expect(page.getByText("You marked this as checked by a speaker of isiXhosa.")).toBeVisible();
  expect(approvals).toEqual([{ reviewVersion: id, speakerChecked: false }, { reviewVersion: id, speakerChecked: true }]);
});
