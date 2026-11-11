import { test, expect } from "@playwright/test";
import { LANGUAGES } from "../lib/languages";

test("Languages_WebCatalogue_MatchesTheApiCatalogue", async ({ request }) => {
  const response = await request.get("/api/languages");
  expect(response.status()).toBe(200);
  const api = await response.json();
  expect(api).toEqual(LANGUAGES.map(({ code, name, nativeName, greeting, aiChecked }) => ({ code, name, nativeName, greeting, aiChecked })));
});

test("Languages_PickerAndUpload_OfferEveryAfricanLanguage", async ({ page }) => {
  await page.goto("/");
  const picker = page.getByRole("combobox", { name: "Your language", exact: true });
  await expect(picker.locator("option")).toHaveCount(25);
  await picker.selectOption("yo");
  await expect(page.getByText("Ẹ n lẹ. Make yourself at home.")).toBeVisible();
  await page.reload();
  await expect(picker).toHaveValue("yo");
  await picker.selectOption("am");
  await expect(page.getByText("ሰላም. Make yourself at home.")).toBeVisible();
  await page.goto("/upload");
  const original = page.getByRole("combobox", { name: "Original language" });
  await expect(original.locator("option")).toHaveCount(25);
  await expect(original.locator("option[value=ha]")).toHaveText("Hausa");
  await expect(original.locator("option[value=wo]")).toHaveText("Wolof");
});

test("Languages_TranslateIntoYoruba_ShowsUntestedAiNoticeAndViewerSeesYoruba", async ({ page, request }) => {
  const form = {
    title: `Languages fixture ${Date.now()}`, artistName: "Mazwi browser fixture", community: "Shona", country: "ZW", type: "folktale", language: "sn", permission: "true",
    recording: { name: "clip.wav", mimeType: "audio/wav", buffer: Buffer.from("RIFF0000WAVEdata0000") },
  };
  const created = await request.post("/api/stories", { multipart: form });
  expect(created.status()).toBe(201);
  const { id } = await created.json();
  try {
    await page.goto(`/story/${id}/review`);
    await page.getByLabel("Original transcript", { exact: true }).fill("Kare kare");
    await page.getByRole("button", { name: "Save original transcript", exact: true }).click();
    await expect(page.getByRole("main").getByRole("status")).toContainText("Original transcript saved");
    await page.getByRole("button", { name: "Approve original", exact: true }).click();
    await expect(page.getByRole("button", { name: "Original is approved", exact: true })).toBeDisabled();
    const target = page.getByLabel("Translation language", { exact: true });
    await target.selectOption("xh");
    await expect(page.getByTestId("ai-quality-notice")).toHaveCount(0);
    await target.selectOption("yo");
    await expect(page.getByTestId("ai-quality-notice")).toContainText("Mazwi has not checked AI quality in Yoruba yet.");
    await page.getByLabel("Translation text", { exact: true }).fill("Ní ayé àtijọ́");
    await page.getByRole("button", { name: "Save translation draft", exact: true }).click();
    await expect(page.getByRole("main").getByRole("status")).toContainText("Yoruba translation saved");
    await page.getByLabel("I speak Yoruba and checked this translation").check();
    await page.getByRole("button", { name: "Approve translation", exact: true }).click();
    await expect(page.getByText("You marked this as checked by a speaker of Yoruba.")).toBeVisible();

    await page.goto(`/story/${id}`);
    await page.getByRole("combobox", { name: "Your language", exact: true }).selectOption("yo");
    await expect(page.getByText("Ní ayé àtijọ́")).toBeVisible();
    await expect(page.getByText(/Approved Yoruba text with original Shona audio/)).toBeVisible();
  } finally {
    const draft = await (await request.get(`/api/stories/${id}/drafts`)).json();
    expect((await request.delete(`/api/stories/${id}?version=${draft.reviewVersion}`)).status()).toBe(204);
  }
});
